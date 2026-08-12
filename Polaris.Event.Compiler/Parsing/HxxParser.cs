using System.Collections.Generic;
using Polaris.Event.Compiler.Diagnostics;
using Polaris.Event.Compiler.Syntax;
using Polaris.Event.Compiler.Text;

namespace Polaris.Event.Compiler.Parsing
{
    /// <summary>
    /// 按行 + 缩进的递归下降解析器。缩进块只用于 <c>@if</c>/<c>@else</c>（本轮冻结范围不含 <c>@choice</c>，
    /// 见 PolarisEvent-实现计划.md §11）。命令名与别名大小写不敏感（哈++ v2.0 §3.2），因此命令名统一转小写。
    /// </summary>
    public sealed class HxxParser
    {
        static readonly Dictionary<string, string> EmptyNamed = new Dictionary<string, string>();
        static readonly HashSet<string> EmptyFlags = new HashSet<string>();

        readonly IReadOnlyList<HxxRawLine> lines;
        readonly string filePath;
        readonly DiagnosticBag diagnostics;

        public HxxParser(IReadOnlyList<HxxRawLine> lines, string filePath, DiagnosticBag diagnostics)
        {
            this.lines = lines;
            this.filePath = filePath;
            this.diagnostics = diagnostics;
        }

        public IReadOnlyList<HxxNode> ParseDocument()
        {
            int index = 0;
            return ParseBlock(ref index, 0);
        }

        List<HxxNode> ParseBlock(ref int index, int requiredIndent)
        {
            var nodes = new List<HxxNode>();
            while (index < lines.Count)
            {
                var line = lines[index];
                if (line.Content.Length == 0)
                {
                    index++;
                    continue;
                }

                if (line.Indent < requiredIndent)
                {
                    break;
                }

                if (line.Indent > requiredIndent)
                {
                    diagnostics.Report(new HppDiagnostic(
                        DiagnosticCodes.UnexpectedIndent,
                        DiagnosticSeverity.Error,
                        $"Unexpected indentation ({line.Indent} spaces; expected {requiredIndent}).",
                        Span(line)));
                    // 按 requiredIndent 继续解析这一行，避免因为缩进错误卡死在原地。
                }

                var node = ParseLine(ref index, requiredIndent);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }

            return nodes;
        }

        HxxNode ParseLine(ref int index, int requiredIndent)
        {
            var line = lines[index];
            string content = line.Content;

            if (content[0] == ';')
            {
                index++;
                return null; // 注释：不产生节点
            }

            if (content[0] == '#')
            {
                index++;
                return new LabelNode(content.Substring(1).Trim(), Span(line));
            }

            if (content[0] == '@')
            {
                return ParseCommandOrControl(ref index, requiredIndent);
            }

            index++;
            return ParseTextLine(content, line);
        }

        static readonly System.Text.RegularExpressions.Regex DialoguePrefix = new System.Text.RegularExpressions.Regex(
            @"^(?<actor>[A-Za-z_][A-Za-z0-9_]*)(\.(?<pose>[A-Za-z_][A-Za-z0-9_]*))?:\s?(?<text>.*)$");

        TextNode ParseTextLine(string content, HxxRawLine line)
        {
            string actor = null;
            string pose = null;
            string text = content;

            var match = DialoguePrefix.Match(content);
            if (match.Success)
            {
                actor = match.Groups["actor"].Value;
                pose = match.Groups["pose"].Success ? match.Groups["pose"].Value : null;
                text = match.Groups["text"].Value;
            }

            string idKey = null;
            int idMarker = text.LastIndexOf(" #id:", System.StringComparison.Ordinal);
            if (idMarker >= 0)
            {
                string candidate = text.Substring(idMarker + 5);
                if (candidate.Length > 0 && candidate.IndexOf(' ') < 0)
                {
                    idKey = candidate;
                    text = text.Substring(0, idMarker);
                }
            }

            return new TextNode(actor, pose, text, idKey, Span(line));
        }

        HxxNode ParseCommandOrControl(ref int index, int requiredIndent)
        {
            var line = lines[index];
            string content = line.Content.Substring(1); // 去掉 '@'
            int sp = content.IndexOf(' ');
            string name = (sp < 0 ? content : content.Substring(0, sp)).ToLowerInvariant();
            string rest = sp < 0 ? string.Empty : content.Substring(sp + 1).TrimStart();

            switch (name)
            {
                case "if":
                {
                    index++;
                    var thenBody = ParseChildBlock(ref index, requiredIndent);

                    IReadOnlyList<HxxNode> elseBody = null;
                    if (TryPeekElse(index, requiredIndent, out int elseLineIndex))
                    {
                        index = elseLineIndex + 1;
                        elseBody = ParseChildBlock(ref index, requiredIndent);
                    }

                    return new IfNode(rest, thenBody, elseBody, Span(line));
                }

                case "else":
                    // 只有紧跟在 @if 块后面、由上面的 case "if" 消费掉的 @else 才是合法的；
                    // 单独走到这里说明它前面没有匹配的 @if 块（缩进不对或压根没有）。
                    diagnostics.Report(new HppDiagnostic(
                        DiagnosticCodes.DanglingElse,
                        DiagnosticSeverity.Error,
                        "@else without a preceding @if block at the same indentation.",
                        Span(line)));
                    index++;
                    return null;

                case "set":
                    // @set 是 "name = value"/"name += value" 形式的赋值表达式，不是 key:value 语法，
                    // 整段原样交给 Lowering 阶段用专门的正则解析。
                    index++;
                    return new CommandNode(name, rest, EmptyNamed, EmptyFlags, Span(line));

                default:
                {
                    index++;
                    var (mainArg, namedArgs, flags) = ParseCommandArguments(rest, Span(line));
                    return new CommandNode(name, mainArg, namedArgs, flags, Span(line));
                }
            }
        }

        bool TryPeekElse(int fromIndex, int requiredIndent, out int elseLineIndex)
        {
            int j = fromIndex;
            while (j < lines.Count && lines[j].Content.Length == 0)
            {
                j++;
            }

            if (j < lines.Count && lines[j].Indent == requiredIndent)
            {
                string c = lines[j].Content;
                if (c.Length > 0 && c[0] == '@' && c.Substring(1).TrimEnd().Equals("else", System.StringComparison.OrdinalIgnoreCase))
                {
                    elseLineIndex = j;
                    return true;
                }
            }

            elseLineIndex = -1;
            return false;
        }

        List<HxxNode> ParseChildBlock(ref int index, int parentIndent)
        {
            int j = index;
            while (j < lines.Count && lines[j].Content.Length == 0)
            {
                j++;
            }

            if (j >= lines.Count || lines[j].Indent <= parentIndent)
            {
                return new List<HxxNode>();
            }

            index = j;
            return ParseBlock(ref index, lines[j].Indent);
        }

        (string MainArgument, IReadOnlyDictionary<string, string> Named, IReadOnlyCollection<string> Flags) ParseCommandArguments(string rest, SourceSpan span)
        {
            if (string.IsNullOrEmpty(rest))
            {
                return (null, EmptyNamed, EmptyFlags);
            }

            var tokens = TokenizeArgs(rest);
            string mainArg = null;
            var named = new Dictionary<string, string>();
            var flags = new HashSet<string>();

            foreach (var tok in tokens)
            {
                int colon = tok.IndexOf(':');
                if (tok.Length > 1 && tok[tok.Length - 1] == '!' && colon < 0)
                {
                    flags.Add(tok.Substring(0, tok.Length - 1));
                }
                else if (colon > 0)
                {
                    named[tok.Substring(0, colon)] = tok.Substring(colon + 1);
                }
                else if (mainArg == null)
                {
                    mainArg = tok;
                }
                else
                {
                    diagnostics.Report(new HppDiagnostic(
                        DiagnosticCodes.MalformedCommandLine,
                        DiagnosticSeverity.Error,
                        $"Unexpected extra argument '{tok}': each command has at most one unnamed main argument.",
                        span));
                }
            }

            return (mainArg, named, flags);
        }

        /// <summary>按空白切分，<c>"..."</c> 内的空白不切分（供 @raw、@char 等使用）。</summary>
        static List<string> TokenizeArgs(string text)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                if (i >= text.Length)
                {
                    break;
                }

                if (text[i] == '"')
                {
                    i++;
                    var sb = new System.Text.StringBuilder();
                    while (i < text.Length && text[i] != '"')
                    {
                        sb.Append(text[i]);
                        i++;
                    }

                    tokens.Add(sb.ToString());
                    if (i < text.Length)
                    {
                        i++; // 跳过闭合引号
                    }
                }
                else
                {
                    int start = i;
                    while (i < text.Length && !char.IsWhiteSpace(text[i]))
                    {
                        i++;
                    }

                    tokens.Add(text.Substring(start, i - start));
                }
            }

            return tokens;
        }

        SourceSpan Span(HxxRawLine line) => new SourceSpan(filePath, line.LineNumber, line.Indent + 1);
    }
}
