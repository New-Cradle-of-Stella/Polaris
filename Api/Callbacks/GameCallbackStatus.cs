namespace Polaris.API
{
    /// <summary>某个 <see cref="GameCallbackKind"/> 本局到底通不通。</summary>
    public enum GameCallbackAvailability
    {
        /// <summary>精确补丁或确定的 Unity 生命周期入口可用。</summary>
        Available = 0,

        /// <summary>通过状态差分提供，可能延迟一帧或把多次变化合并成一条。</summary>
        Degraded = 1,

        /// <summary>本游戏版本没有入口，或对应补丁本局安装失败；也覆盖"这条回调本 Polaris
        /// 构建里还没实现"的情况——两者对下游的意义是一样的：不要等这条回调。</summary>
        Unsupported = 2,
    }

    /// <summary>查询单个回调种类的状态。</summary>
    public sealed class GameCallbackStatus
    {
        public GameCallbackKind Kind { get; }
        public GameCallbackAvailability Availability { get; }
        public GameCallbackPrecision Precision { get; }
        public string Reason { get; }

        internal GameCallbackStatus(GameCallbackKind kind, GameCallbackAvailability availability,
            GameCallbackPrecision precision, string reason)
        {
            Kind = kind;
            Availability = availability;
            Precision = precision;
            Reason = reason;
        }
    }

    /// <summary>诊断页/报告用的完整一条：比 <see cref="GameCallbackStatus"/> 多一个可读名字。</summary>
    public sealed class GameCallbackDescriptor
    {
        public GameCallbackKind Kind { get; }
        public string Name { get; }
        public GameCallbackAvailability Availability { get; }
        public GameCallbackPrecision Precision { get; }
        public string Reason { get; }

        internal GameCallbackDescriptor(GameCallbackKind kind, GameCallbackAvailability availability,
            GameCallbackPrecision precision, string reason)
        {
            Kind = kind;
            Name = kind.ToString();
            Availability = availability;
            Precision = precision;
            Reason = reason;
        }
    }
}
