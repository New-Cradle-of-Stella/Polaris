using System;

namespace Polaris.Res
{
    /// <summary>
    /// 资源在所有已注册挂载点下都找不到。<see cref="Exception.Message"/> 会枚举每一个
    /// 探测过的候选（挂载路径 + 每个扩展名 + 是否存在），这是本库里最有价值的一条诊断信息，
    /// 构造方式见 <see cref="Mounts.MountProbeLog.BuildMessage"/>。
    /// </summary>
    public sealed class ResourceNotFoundException : Exception
    {
        public ResourceId Id { get; }

        public ResourceNotFoundException(ResourceId id, string message) : base(message)
        {
            Id = id;
        }
    }

    /// <summary>资源被找到，但解析/构造过程本身失败（文件损坏、格式不对等）。</summary>
    public sealed class ResourceLoadException : Exception
    {
        public ResourceId Id { get; }

        public ResourceLoadException(ResourceId id, string message, Exception inner = null)
            : base(message, inner)
        {
            Id = id;
        }
    }
}
