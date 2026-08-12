using System.Collections.Generic;
using Polaris.Event.Compiler.Text;

namespace Polaris.Event.Compiler.Parsing
{
    public abstract class HxxNode
    {
        protected HxxNode(SourceSpan span)
        {
            Span = span;
        }

        public SourceSpan Span { get; }
    }

    /// <summary><c># Label</c>。</summary>
    public sealed class LabelNode : HxxNode
    {
        public string Name { get; }

        public LabelNode(string name, SourceSpan span) : base(span)
        {
            Name = name;
        }
    }

    /// <summary>普通文本或 <c>Actor[.Pose]: 文本</c>。<see cref="Actor"/> 为空表示旁白。</summary>
    public sealed class TextNode : HxxNode
    {
        public string Actor { get; }
        public string Pose { get; }
        public string Text { get; }

        /// <summary>行尾 <c>#id:key</c>，用于 textMode:catalog；本轮编译器只支持 heredoc，暂不消费。</summary>
        public string IdKey { get; }

        public TextNode(string actor, string pose, string text, string idKey, SourceSpan span) : base(span)
        {
            Actor = actor;
            Pose = pose;
            Text = text;
            IdKey = idKey;
        }
    }

    /// <summary>
    /// 一条 <c>@命令 [主参数] [key:value] [flag!]</c>。<c>@set</c> 的赋值表达式和 <c>@if</c> 的条件表达式
    /// 不走这套 key:value/flag 语法，分别放在 <see cref="MainArgument"/>（@set）或 <see cref="IfNode.Condition"/>
    /// （@if）里保留原始文本，交给各自的降级逻辑再解析。
    /// </summary>
    public sealed class CommandNode : HxxNode
    {
        public string Name { get; }
        public string MainArgument { get; }
        public IReadOnlyDictionary<string, string> NamedArguments { get; }
        public IReadOnlyCollection<string> Flags { get; }

        public CommandNode(
            string name,
            string mainArgument,
            IReadOnlyDictionary<string, string> namedArguments,
            IReadOnlyCollection<string> flags,
            SourceSpan span)
            : base(span)
        {
            Name = name;
            MainArgument = mainArgument;
            NamedArguments = namedArguments;
            Flags = flags;
        }
    }

    /// <summary><c>@if Expr</c> ... 可选 <c>@else</c> ...，缩进块由解析器识别。</summary>
    public sealed class IfNode : HxxNode
    {
        public string Condition { get; }
        public IReadOnlyList<HxxNode> ThenBody { get; }

        /// <summary>没有 <c>@else</c> 时为 null，区别于"有 @else 但块是空的"。</summary>
        public IReadOnlyList<HxxNode> ElseBody { get; }

        public IfNode(string condition, IReadOnlyList<HxxNode> thenBody, IReadOnlyList<HxxNode> elseBody, SourceSpan span)
            : base(span)
        {
            Condition = condition;
            ThenBody = thenBody;
            ElseBody = elseBody;
        }
    }
}
