namespace Polaris.Res.Core
{
    /// <summary>
    /// 缓存条目的生命周期状态。M1/M2 的同步加载路径只会用到 <see cref="Ready"/>——
    /// 失败直接向上抛异常，不进缓存（见 <see cref="ResourceCache.AcquireSync{T}"/> 的注释）。
    /// <see cref="Loading"/>/<see cref="Faulted"/> 是留给 M4 异步加载的状态，届时同一个
    /// <see cref="Lease{T}"/> 实现会被两条加载路径共用。
    /// </summary>
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
