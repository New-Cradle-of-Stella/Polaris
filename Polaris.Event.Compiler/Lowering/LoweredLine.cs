namespace Polaris.Event.Compiler.Lowering
{
    /// <summary>降级后的一行底层哈语言，带上来源位置——直接就是 .cmd 的一行和 .hmap.json 的一条记录。</summary>
    public sealed class LoweredLine
    {
        public string Text { get; }
        public string SourceFilePath { get; }
        public int SourceLine { get; }
        public int SourceColumn { get; }

        public LoweredLine(string text, string sourceFilePath, int sourceLine, int sourceColumn)
        {
            Text = text;
            SourceFilePath = sourceFilePath;
            SourceLine = sourceLine;
            SourceColumn = sourceColumn;
        }
    }
}
