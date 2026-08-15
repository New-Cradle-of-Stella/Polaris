namespace Polaris
{
    /// <summary>一次 Polaris 产品级动作的结果分类。</summary>
    public enum PolarisActionStatus
    {
        Ok,

        /// <summary>参数本身不合法，重试也不会成功。</summary>
        InvalidArgument,

        /// <summary>目标还不存在或已经消失（游戏还没起来、菜单还没建好）。</summary>
        TargetUnavailable,

        /// <summary>目标存在，但当前状态不接受这次请求。</summary>
        RejectedByState,

        /// <summary>本游戏版本没有可用的入口，这不是一次失败而是一项能力缺失。</summary>
        Unsupported,

        /// <summary>调用过程中抛了异常。</summary>
        Failed,
    }

    /// <summary>一次动作的结果；用于 Polaris 自身产品 API（菜单扩展、设置、资源），游戏 API v2 不用。</summary>
    public readonly struct PolarisActionResult
    {
        PolarisActionResult(PolarisActionStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public PolarisActionStatus Status { get; }

        /// <summary>失败时的一句话原因；成功时为 <c>null</c>。</summary>
        public string Message { get; }

        public bool Succeeded => Status == PolarisActionStatus.Ok;

        public static PolarisActionResult Ok() => new(PolarisActionStatus.Ok, null);

        public static PolarisActionResult Fail(PolarisActionStatus status, string message) => new(status, message);

        public static PolarisActionResult Unsupported(string message)
            => new(PolarisActionStatus.Unsupported, message);

        public override string ToString() => Succeeded ? "Ok" : $"{Status}: {Message}";
    }
}
