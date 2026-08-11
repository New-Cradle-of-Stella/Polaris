namespace Polaris.API
{
    /// <summary>
    /// 一次 <see cref="GameSignal{T}.Subscribe(System.Action{T}, GameCallbackOptions)"/> 的可选行为。
    /// </summary>
    public sealed class GameCallbackOptions
    {
        /// <summary>数值小的先执行；相同优先级按注册序号执行。约定范围 -1000..1000，超出会被夹紧。</summary>
        public int Priority { get; }

        /// <summary>只执行一次：调用前立即标记失效，防止回调内部重入导致同一次事件执行两次。</summary>
        public bool Once { get; }

        /// <summary>诊断用的可读名字；不填时诊断页显示委托的方法名。</summary>
        public string DebugName { get; }

        public GameCallbackOptions(int priority = 0, bool once = false, string debugName = null)
        {
            Priority = priority < -1000 ? -1000 : priority > 1000 ? 1000 : priority;
            Once = once;
            DebugName = debugName;
        }

        internal static readonly GameCallbackOptions Default = new();
    }
}
