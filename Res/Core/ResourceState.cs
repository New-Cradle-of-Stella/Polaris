namespace Polaris.Res.Core
{
    /// <summary>缓存条目的生命周期状态。同步加载路径只使用 <see cref="Ready"/>；失败直接抛异常，不进缓存。</summary>
    internal enum ResourceState
    {
        Pending,
        Loading,
        Ready,
        Faulted,
        Unloading,
        Unloaded,
    }
}
