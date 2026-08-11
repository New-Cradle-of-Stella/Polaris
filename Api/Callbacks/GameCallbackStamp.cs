namespace Polaris.API
{
    /// <summary>事件是从哪条路径产生的：影响下游对"这条数据有多可信"的判断，不代表优先级。</summary>
    public enum GameCallbackOrigin
    {
        Unknown = 0,

        /// <summary>直接来自原版方法调用（Harmony Prefix/Postfix 观察到的真实调用）。</summary>
        Vanilla = 1,

        /// <summary>由某个 <c>PolarisAPI.Game.*</c> 写操作触发（见 <c>GameActionOriginScope</c>）。</summary>
        PolarisAction = 2,

        /// <summary>由 Polaris 每帧比较前后状态推断得出，不是某一次方法调用的直接结果。</summary>
        StateDifference = 3,

        /// <summary>来自 Unity 自身的生命周期回调（<c>SceneManager</c>、<c>OnApplicationFocus</c> 等）。</summary>
        UnityLifecycle = 4,
    }

    /// <summary>事件相对"真实发生时刻"的精确度。</summary>
    public enum GameCallbackPrecision
    {
        /// <summary>就是原版方法调用的那一刻，没有延迟或合并。</summary>
        Exact = 0,

        /// <summary>推迟到同一帧结束时才知道最终结果。</summary>
        EndOfFrame = 1,

        /// <summary>推迟到下一次 <see cref="CallbackRuntime"/> 泵才被观察到，可能落后一帧。</summary>
        NextPump = 2,

        /// <summary>同一帧内的多次变化被合并成了一条，只保留累计增量与最终值。</summary>
        Coalesced = 3,
    }

    /// <summary>
    /// 每个回调事件参数都带的不可变时间戳。<see cref="Sequence"/> 是全局唯一且单调递增的，
    /// 跨领域也可比较先后——这是"伤害事件先于它引发的死亡事件"这类跨领域因果顺序的唯一保证来源。
    /// </summary>
    public sealed class GameCallbackStamp
    {
        /// <summary>全进程单调递增的全局序号，跨越所有 <see cref="GameCallbackKind"/>。</summary>
        public long Sequence { get; }

        /// <summary>产生时的 Unity <c>Time.frameCount</c>。</summary>
        public int UnityFrame { get; }

        /// <summary>产生时的游戏自身帧计数（<c>XX.IN.totalframe</c>），读不到时为 0。</summary>
        public int GameFrame { get; }

        /// <summary>产生时的地图代数，见 <c>GameBinding.MapGeneration</c>。</summary>
        public int MapGeneration { get; }

        public GameCallbackOrigin Origin { get; }

        public GameCallbackPrecision Precision { get; }

        internal GameCallbackStamp(long sequence, int unityFrame, int gameFrame, int mapGeneration,
            GameCallbackOrigin origin, GameCallbackPrecision precision)
        {
            Sequence = sequence;
            UnityFrame = unityFrame;
            GameFrame = gameFrame;
            MapGeneration = mapGeneration;
            Origin = origin;
            Precision = precision;
        }
    }
}
