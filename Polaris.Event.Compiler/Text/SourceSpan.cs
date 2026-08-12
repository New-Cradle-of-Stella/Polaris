namespace Polaris.Event.Compiler.Text
{
    /// <summary>诊断和 source map 共用的位置信息：1-based 行列。</summary>
    public readonly struct SourceSpan
    {
        public string FilePath { get; }
        public int Line { get; }
        public int Column { get; }

        public SourceSpan(string filePath, int line, int column)
        {
            FilePath = filePath;
            Line = line;
            Column = column;
        }

        public override string ToString() => $"{FilePath}:{Line}:{Column}";
    }
}
