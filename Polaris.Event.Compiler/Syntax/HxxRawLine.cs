namespace Polaris.Event.Compiler.Syntax
{
    /// <summary>词法阶段的最小产物：一行去掉了缩进和尾随空白后的内容，以及缩进宽度。</summary>
    public sealed class HxxRawLine
    {
        public int LineNumber { get; }
        public int Indent { get; }

        /// <summary>去掉前导缩进、去掉尾随空白后的内容；空行为空字符串。</summary>
        public string Content { get; }

        public HxxRawLine(int lineNumber, int indent, string content)
        {
            LineNumber = lineNumber;
            Indent = indent;
            Content = content;
        }
    }
}
