using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Polaris.Event.Compiler.Aliases;
using Polaris.Event.Compiler.Binding;
using Polaris.Event.Compiler.Diagnostics;
using Polaris.Event.Compiler.Parsing;
using Polaris.Event.Compiler.Text;

namespace Polaris.Event.Compiler.Lowering
{
    /// <summary>
    /// 把绑定后的 AST 降级为底层哈语言行。命令 -> 底层展开的映射严格照抄《哈++简明指令集-v2.0.csv》
    /// 对应行；具体假设（MSG heredoc 分隔符、@set 的 +=/-= 脱糖方式等）在没有阶段0游戏内验证的情况下
    /// 只能是"忠实读表后的最合理选择"，已在相应方法上标注，交付说明里也会重复强调。
    /// </summary>
    public sealed class HxxLowerer
    {
        // 0.29j 的 MSG heredoc 分隔符实际写法未见诸已提供的素材（CSV 只写"MSG heredoc"，没给具体 token），
        // 这里选用一个不会与台词正文冲突的自定义分隔符，需要阶段0在游戏里核实真实语法后替换。
        const string MessageHeredocDelimiter = "HPPEOF";

        static readonly Regex AssignmentPattern = new Regex(
            @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?<op>\+=|-=|\?=|=)\s*(?<value>.+)$");

        readonly AliasBinder binder;
        readonly DiagnosticBag diagnostics;
        readonly bool strictRaw;
        readonly string filePath;
        readonly HashSet<string> seenLabels = new HashSet<string>();
        readonly List<LoweredLine> output = new List<LoweredLine>();
        HashSet<string> allLabels = new HashSet<string>();

        /// <summary>
        /// 角色最近一次成功解析出的站位，从最近一次 <c>@char</c>/对话行开始沿用，直到被显式 <c>pos:</c>
        /// 覆盖为止。没有这份状态的话，每一句不带 <c>.Pose</c> 的对话行都会重新退回
        /// <see cref="ActorAlias.DefaultPos"/>/全局 <c>Settings.DefaultPosition</c>，导致刚用 <c>@char
        /// ... pos:X</c> 站好位的角色，下一句台词又报"未知站位"——两者对不上是因为压根没人记录"这个角色
        /// 现在站在哪"这件事。
        /// </summary>
        readonly Dictionary<ActorAlias, PositionAlias> currentPositionByActor = new Dictionary<ActorAlias, PositionAlias>();

        public HxxLowerer(AliasBinder binder, DiagnosticBag diagnostics, bool strictRaw, string filePath)
        {
            this.binder = binder;
            this.diagnostics = diagnostics;
            this.strictRaw = strictRaw;
            this.filePath = filePath;
        }

        public IReadOnlyList<LoweredLine> Lower(IReadOnlyList<HxxNode> nodes)
        {
            allLabels = LabelCollector.Collect(nodes);

            // 顶层块跑完之后如果还"可达"（没被一个顶层、不在任何 @if/@else 里的 @return 截断），说明
            // 这份事件脚本没有一个明确的结束标记。刻意不去看 @if/@else 内部是否两支都 return 了——
            // 那需要真正的穷尽性分支分析，而且就算两支都 return，也不代表作者是"故意"用它们收尾的；
            // 强制要求顶层必须有一句显式 @return，行为上更好预测，也是默认模板一直在用的写法。
            bool stillReachableAtEnd = LowerBlock(nodes);
            if (stillReachableAtEnd)
            {
                var span = nodes.Count > 0 ? nodes[nodes.Count - 1].Span : new SourceSpan(filePath, 1, 1);
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.MissingReturn,
                    DiagnosticSeverity.Error,
                    "This event does not end with an explicit @return. Every .phxx file must finish with a "
                        + "top-level @return so the event has an unambiguous exit point.",
                    span));
            }

            return output;
        }

        void Emit(string text, SourceSpan span) => output.Add(new LoweredLine(text, span.FilePath, span.Line, span.Column));

        /// <summary>降级一个顺序执行的语句块，返回"块跑完之后是否仍然可达"（true=没遇到过
        /// 这个块自己层面的 @return，调用方如果是顶层就该报缺 @return；false=块内某句顶层
        /// @return 已经把后面的兄弟节点标成不可达）。</summary>
        bool LowerBlock(IReadOnlyList<HxxNode> nodes)
        {
            bool reachable = true;
            foreach (var node in nodes)
            {
                if (!reachable)
                {
                    diagnostics.Report(new HppDiagnostic(
                        DiagnosticCodes.UnreachableCode,
                        DiagnosticSeverity.Warning,
                        "Unreachable: an earlier @return already ends the event before this line can run.",
                        node.Span));
                }

                if (LowerNode(node))
                {
                    reachable = false;
                }
            }

            return reachable;
        }

        /// <summary>返回 true 当且仅当这个节点本身就是一句顶层 @return——只有这种情况才应该让
        /// 后面的兄弟节点被标记为不可达。@if/@else 内部各自独立跑 <see cref="LowerBlock"/>
        /// 判断自己块内的可达性，但不把结果传给外层块（见 <see cref="LowerIf"/> 的注释）。</summary>
        bool LowerNode(HxxNode node)
        {
            switch (node)
            {
                case LabelNode label:
                    LowerLabel(label);
                    return false;
                case TextNode text:
                    LowerText(text);
                    return false;
                case CommandNode command:
                    return LowerCommand(command);
                case IfNode ifNode:
                    LowerIf(ifNode);
                    return false;
                default:
                    return false;
            }
        }

        void LowerLabel(LabelNode label)
        {
            if (!seenLabels.Add(label.Name))
            {
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.DuplicateLabel,
                    DiagnosticSeverity.Error,
                    $"Label '{label.Name}' is defined more than once in this file.",
                    label.Span));
                return;
            }

            Emit($"LABEL {label.Name}", label.Span);
        }

        void LowerText(TextNode text)
        {
            if (text.Actor != null)
            {
                if (!binder.TryResolveActor(text.Actor, text.Span, out var actor))
                {
                    EmitMessage(text.Text, text.Span);
                    return;
                }

                var position = ResolvePosition(actor, null, text.Span);

                if (text.Pose != null)
                {
                    if (binder.TryResolvePose(actor, text.Actor, text.Pose, text.Span, out var rawPose))
                    {
                        EmitCharacterDisplay(actor, position, rawPose, text.Span);
                    }
                }
                else
                {
                    Emit($"TALKER {actor.Raw} {PosToken(position)}", text.Span);
                }
            }

            EmitMessage(text.Text, text.Span);
        }

        bool LowerCommand(CommandNode command)
        {
            switch (command.Name)
            {
                case "char":
                    LowerChar(command);
                    return false;
                case "wait":
                    LowerWait(command);
                    return false;
                case "sfx":
                    LowerSfx(command);
                    return false;
                case "set":
                    LowerSet(command);
                    return false;
                case "goto":
                    LowerGoto(command);
                    return false;
                case "call":
                    LowerCall(command);
                    return false;
                case "return":
                    Emit("SEEK_END", command.Span);
                    return true;
                case "raw":
                    LowerRaw(command);
                    return false;
                default:
                    diagnostics.Report(new HppDiagnostic(
                        DiagnosticCodes.UnknownCommand,
                        DiagnosticSeverity.Error,
                        $"Unknown command '@{command.Name}'. This preliminary compiler only implements the frozen "
                            + "v1 command set (char/wait/sfx/set/if/else/goto/call/return/raw).",
                        command.Span));
                    return false;
            }
        }

        void LowerChar(CommandNode command)
        {
            if (command.MainArgument == null)
            {
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.MissingMainArgument,
                    DiagnosticSeverity.Error,
                    "@char requires an Actor[.Pose] argument.",
                    command.Span));
                return;
            }

            string actorName = command.MainArgument;
            string poseName = null;
            int dot = command.MainArgument.IndexOf('.');
            if (dot >= 0)
            {
                actorName = command.MainArgument.Substring(0, dot);
                poseName = command.MainArgument.Substring(dot + 1);
            }

            if (!binder.TryResolveActor(actorName, command.Span, out var actor))
            {
                return;
            }

            string explicitPosition = command.NamedArguments.TryGetValue("pos", out var pos) ? pos : null;
            var position = ResolvePosition(actor, explicitPosition, command.Span);

            if (poseName != null)
            {
                if (binder.TryResolvePose(actor, actorName, poseName, command.Span, out var rawPose))
                {
                    EmitCharacterDisplay(actor, position, rawPose, command.Span);
                }
            }
            else
            {
                Emit($"TALKER {actor.Raw} {PosToken(position)}", command.Span);
            }
        }

        PositionAlias ResolvePosition(ActorAlias actor, string explicitPositionName, SourceSpan span)
        {
            PositionAlias position;
            if (explicitPositionName != null)
            {
                binder.TryResolvePosition(explicitPositionName, span, out position); // 失败时已经报出诊断
            }
            else if (currentPositionByActor.TryGetValue(actor, out var lastPosition))
            {
                position = lastPosition; // 沿用这个角色最近一次站好的位置，不重新查表也不会重复报错
            }
            else
            {
                string fallbackName = actor.DefaultPos ?? binder.Aliases.Settings.DefaultPosition;
                binder.TryResolvePosition(fallbackName, span, out position); // 失败时已经报出诊断
            }

            if (position != null)
            {
                currentPositionByActor[actor] = position;
            }

            return position;
        }

        static string PosToken(PositionAlias position) => position?.Talker ?? "C";

        void EmitCharacterDisplay(ActorAlias actor, PositionAlias position, string rawPose, SourceSpan span)
        {
            string talkerPos = PosToken(position);
            Emit($"TALKER {actor.Raw} {talkerPos}", span);
            Emit($"PIC {actor.Raw} {rawPose}", span);

            var hkdsTokens = new List<string> { actor.Raw, talkerPos };
            if (!string.IsNullOrEmpty(position?.From))
            {
                hkdsTokens.Add(position.From);
            }

            string boxToken = ResolveBoxToken(actor);
            if (!string.IsNullOrEmpty(boxToken))
            {
                hkdsTokens.Add(boxToken);
            }

            Emit("HKDS " + string.Join(" ", hkdsTokens), span);
        }

        string ResolveBoxToken(ActorAlias actor)
        {
            if (string.IsNullOrEmpty(actor.Box))
            {
                return null;
            }

            return binder.Aliases.BoxStyles.TryGetValue(actor.Box, out var token) ? token : null;
        }

        void EmitMessage(string text, SourceSpan span)
        {
            Emit("MSG <<" + MessageHeredocDelimiter, span);
            Emit(text, span);
            Emit(MessageHeredocDelimiter, span);
        }

        void LowerWait(CommandNode command)
        {
            if (command.MainArgument == null || !int.TryParse(command.MainArgument, out int frames) || frames < 0)
            {
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.InvalidWaitFrames,
                    DiagnosticSeverity.Error,
                    $"@wait requires a non-negative integer frame count, got '{command.MainArgument}'.",
                    command.Span));
                return;
            }

            Emit($"WAIT {frames}", command.Span);
        }

        void LowerSfx(CommandNode command)
        {
            if (command.MainArgument == null)
            {
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.MissingMainArgument, DiagnosticSeverity.Error, "@sfx requires a sound alias.", command.Span));
                return;
            }

            if (binder.TryResolveSfx(command.MainArgument, command.Span, out var raw))
            {
                Emit($"SND {raw}", command.Span);
            }
        }

        void LowerSet(CommandNode command)
        {
            var match = command.MainArgument == null ? Match.Empty : AssignmentPattern.Match(command.MainArgument);
            if (!match.Success)
            {
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.MalformedCommandLine,
                    DiagnosticSeverity.Error,
                    $"@set expects 'name = value' (also supports += / -=), got '{command.MainArgument}'.",
                    command.Span));
                return;
            }

            string name = match.Groups["name"].Value;
            string op = match.Groups["op"].Value;
            string value = match.Groups["value"].Value.Trim();

            switch (op)
            {
                case "=":
                    Emit($"{name}={value}", command.Span);
                    break;
                case "+=":
                    Emit($"{name}=~{name}+{value}", command.Span);
                    break;
                case "-=":
                    Emit($"{name}=~{name}-{value}", command.Span);
                    break;
                default:
                    // "?=" 的底层展开在现有素材里没有确切依据，宁可报一条诊断也不要猜错语义。
                    diagnostics.Report(new HppDiagnostic(
                        DiagnosticCodes.UnknownNamedArgument,
                        DiagnosticSeverity.Warning,
                        $"@set operator '{op}' is not implemented in this preliminary compiler yet; only =, +=, -= are supported.",
                        command.Span));
                    break;
            }
        }

        void LowerGoto(CommandNode command)
        {
            if (command.MainArgument == null || command.MainArgument[0] != '#')
            {
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.MalformedCommandLine,
                    DiagnosticSeverity.Error,
                    "This preliminary compiler only supports local labels for @goto (e.g. '@goto #Leave'); "
                        + "cross-event 'EventAlias#Label' targets are not implemented yet.",
                    command.Span));
                return;
            }

            string label = command.MainArgument.Substring(1);
            if (!allLabels.Contains(label))
            {
                string suggestion = SpellingSuggestions.Suggest(allLabels, label);
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.UnknownLabel,
                    DiagnosticSeverity.Error,
                    $"Label '#{label}' is not defined in this file.",
                    command.Span,
                    suggestion == null ? null : $"Did you mean: #{suggestion}?"));
                return;
            }

            string gotoLine = $"GOTO {label}";
            if (command.NamedArguments.TryGetValue("if", out var expr))
            {
                Emit($"IF '{expr}' {{", command.Span);
                Emit(gotoLine, command.Span);
                Emit("}", command.Span);
            }
            else
            {
                Emit(gotoLine, command.Span);
            }
        }

        void LowerCall(CommandNode command)
        {
            if (command.MainArgument == null)
            {
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.MissingMainArgument, DiagnosticSeverity.Error, "@call requires an event alias.", command.Span));
                return;
            }

            if (!binder.TryResolveEvent(command.MainArgument, command.Span, out var raw))
            {
                return;
            }

            string line = $"CHANGE_EVENT2 {raw}";
            if (command.NamedArguments.TryGetValue("args", out var argsList) && !string.IsNullOrEmpty(argsList))
            {
                line += " " + string.Join(" ", argsList.Split(','));
            }

            Emit(line, command.Span);
        }

        void LowerRaw(CommandNode command)
        {
            if (command.MainArgument == null)
            {
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.MissingMainArgument, DiagnosticSeverity.Error, "@raw requires a quoted line of legacy script.", command.Span));
                return;
            }

            if (strictRaw)
            {
                diagnostics.Report(new HppDiagnostic(
                    DiagnosticCodes.RawInStrictMode,
                    DiagnosticSeverity.Warning,
                    "@raw bypasses alias resolution and diagnostics; avoid it once a proper command exists for this line.",
                    command.Span));
            }

            Emit(command.MainArgument, command.Span);
        }

        void LowerIf(IfNode node)
        {
            // 每个分支各自独立跑一遍 LowerBlock 的可达性检查（分支内部提前 @return 之后的行照样会被
            // 标成 Unreachable），但两支的返回值都被丢弃，不往外层块传播：哪怕 then/else 两支都以
            // @return 结尾，也不代表 @if 之后的代码就"不可达"——分支条件本身在运行时可能走也可能不走，
            // 这不是一个可以用简单顺序扫描判断穷尽性的问题。
            Emit($"IF '{node.Condition}' {{", node.Span);
            LowerBlock(node.ThenBody);
            Emit("}", node.Span);

            if (node.ElseBody != null)
            {
                Emit("ELSE {", node.Span);
                LowerBlock(node.ElseBody);
                Emit("}", node.Span);
            }
        }
    }
}
