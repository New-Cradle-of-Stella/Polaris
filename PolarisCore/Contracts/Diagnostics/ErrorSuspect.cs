namespace Polaris.Diagnostics
{
    /// <summary>一个嫌疑人：有理由怀疑、但证据不足以定为主责的归属。</summary>
    public sealed class ErrorSuspect
    {
        internal ErrorSuspect() { }

        /// <summary>嫌疑人。</summary>
        public AssemblyOwner Owner { get; internal set; }

        /// <summary>为什么上榜，例如 <c>改写了原版方法 nel.title.SceneTitleTemp.initButtons（transpiler）</c>。</summary>
        public string Reason { get; internal set; }

        public string Describe() => $"{Owner?.Describe() ?? "unknown"} -- {Reason}";

        public override string ToString() => Describe();
    }
}
