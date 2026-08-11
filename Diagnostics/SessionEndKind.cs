namespace Polaris.Diagnostics
{
    /// <summary>上一局是怎么结束的，由 <see cref="SessionSentinel"/> 在启动时判定。</summary>
    public enum SessionEndKind
    {
        /// <summary>无从判断：这是第一次装 Polaris，或者哨兵文件写不出来（目录只读）。</summary>
        Unknown,

        /// <summary>正常退出：走完了 <c>OnApplicationQuit</c>，该存的都存了。</summary>
        Clean,

        /// <summary>
        /// 没有正常退出。<b>刻意不叫 Crashed</b>：进程没了而 <c>OnApplicationQuit</c> 没跑，
        /// 原生崩溃、<c>StackOverflowException</c>、内存耗尽、玩家用任务管理器结束进程、
        /// Steam 强杀……在这一层全都长得一模一样，我们分不出来，就不该对玩家说"你崩溃了"。
        /// </summary>
        NotClosed,

        /// <summary>
        /// 卡死：<see cref="Watchdog"/> 在上一局亲眼看到主线程长时间停止推进，并留下了记录。
        /// 比 <see cref="NotClosed"/> 强得多——这条是有证据的，还带着"卡在谁身上"。
        /// </summary>
        Hung,
    }
}
