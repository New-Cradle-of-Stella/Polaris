namespace Polaris
{
    /// <summary>游戏内富文本用于渲染键位图标的转义标签；用于拼接操作提示行文本。</summary>
    public static class KeyHint
    {
        public const string Up = "<key ua/>";
        public const string Down = "<key da/>";
        public const string Left = "<key la/>";
        public const string Right = "<key ra/>";
        public const string Submit = "<key submit/>";
        public const string Cancel = "<key cancel/>";
    }
}
