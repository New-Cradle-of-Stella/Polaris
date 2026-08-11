namespace Polaris.Diagnostics
{
    /// <summary>
    /// 一个嫌疑人：有理由怀疑、但证据不足以定为主责的归属。
    /// <para>
    /// 嫌疑人名单本身就是有价值的产出，不只是"主责"的副产品。堆栈里全是原版帧、
    /// 而沿途某个原版方法被三个模组同时改过时，Polaris 不该硬点一个名——把三个都摆出来，
    /// 玩家逐个关掉验证，比一个自信满满的错误结论有用得多。
    /// </para>
    /// </summary>
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
