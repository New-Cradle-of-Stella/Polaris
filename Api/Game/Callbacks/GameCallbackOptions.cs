namespace Polaris.API
{
    /// <summary>
    /// 一次回调注册的可选行为。三项都留默认就是"按注册顺序、每次都收、诊断页显示方法名"。
    /// </summary>
    public sealed class GameCallbackOptions
    {
        public GameCallbackOptions(int priority = 0, bool once = false, string debugName = null)
        {
            // 夹紧而不是抛：优先级是个相对量，写大了只说明调用方想"尽量靠前/靠后"，
            // 为此让注册失败没有意义。
            Priority = priority < -1000 ? -1000 : priority > 1000 ? 1000 : priority;
            Once = once;
            DebugName = debugName;
        }

        /// <summary>数值小的先执行；相同优先级按注册先后执行。约定范围 -1000..1000，超出会被夹紧。</summary>
        public int Priority { get; }

        /// <summary>只执行一次：调用前立即标记失效，防止回调内部重入导致同一次事件执行两次。</summary>
        public bool Once { get; }

        /// <summary>诊断用的可读名字；不填时诊断页显示委托的方法名。</summary>
        public string DebugName { get; }

        internal static readonly GameCallbackOptions Default = new();
    }
}
