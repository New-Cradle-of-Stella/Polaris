namespace Polaris.API
{
    /// <summary>
    /// 一次游戏动作的结局。<b>没有“静默失败”这一项</b>——这是与旧 LuaAiC 接口最大的语义差别：
    /// 旧接口里 <c>Player:Recover()</c> 之类的调用无论玩家在不在场、状态允不允许都不返回任何东西，
    /// 作者只能靠再读一次属性来猜是否生效。新 API 一律返回 <see cref="GameActionResult"/>。
    /// </summary>
    public enum GameActionStatus
    {
        /// <summary>动作已执行（或已开始执行）。</summary>
        Started = 0,

        /// <summary>当前游戏状态不允许：玩家在受伤、演出、被吞、地图切换中等。</summary>
        RejectedByState,

        /// <summary>目标不存在或已失效：句柄过期、玩家不在场、地图已卸载。</summary>
        TargetUnavailable,

        /// <summary>参数不合法：物品 key 不存在、grade 越界、数量为负。</summary>
        InvalidArgument,

        /// <summary>资源不足：MP、金钱、背包容量。</summary>
        InsufficientResource,

        /// <summary>
        /// 当前游戏版本没有这条能力。<b>这不是错误</b>：调用方应当据此优雅降级，
        /// 而不是把它当异常上报。哪些能力在本局可用，见 <see cref="GameCapabilities"/>。
        /// </summary>
        UnsupportedInCurrentVersion,

        /// <summary>调用到达了游戏内部但抛了异常，异常已经过 <c>PolarisAPI.Errors</c> 归因。</summary>
        Failed,
    }

    /// <summary>
    /// 动作结果。<see cref="Reason"/> 是给日志和调试用的一句话，不是给玩家看的文案，
    /// 也不要拿它做分支判断——分支判断用 <see cref="Status"/>。
    /// </summary>
    public readonly struct GameActionResult
    {
        public GameActionStatus Status { get; }

        /// <summary>失败时的一句话原因；成功时为 <c>null</c>。</summary>
        public string Reason { get; }

        private GameActionResult(GameActionStatus status, string reason)
        {
            Status = status;
            Reason = reason;
        }

        public bool Succeeded => Status == GameActionStatus.Started;

        public static GameActionResult Ok() => new GameActionResult(GameActionStatus.Started, null);

        public static GameActionResult Fail(GameActionStatus status, string reason)
            => new GameActionResult(status, reason);

        /// <summary>玩家/地图不在场时最常用的一条，措辞统一，方便日志过滤。</summary>
        public static GameActionResult NoPlayer()
            => new GameActionResult(GameActionStatus.TargetUnavailable, "当前没有在场的玩家角色。");

        /// <summary>本局游戏版本上这条能力没有可用入口。</summary>
        public static GameActionResult Unsupported(string what)
            => new GameActionResult(GameActionStatus.UnsupportedInCurrentVersion, what);

        public override string ToString() => Reason == null ? Status.ToString() : $"{Status}：{Reason}";
    }
}
