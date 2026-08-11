using System;

namespace Polaris.Res
{
    /// <summary>
    /// 一次性的资源租约。<see cref="Dispose"/> 减少内部引用计数；引用计数归零时资源才会
    /// 真正被卸载（M1 阶段是立即卸载，M6 起会加宽限期）。重复 <see cref="Dispose"/> 必须无害
    /// ——using、手动 Dispose、终结器三条路径可能同时到达同一个租约。
    /// </summary>
    public interface IResourceLease<out T> : IDisposable
    {
        ResourceId Id { get; }

        /// <summary>
        /// 已释放 → <see cref="ObjectDisposedException"/>；加载失败 →
        /// <see cref="ResourceLoadException"/>（或更具体的 <see cref="ResourceNotFoundException"/>）；
        /// 仍在加载（异步路径专用，M1/M2 的同步 API 不会走到这个状态）→
        /// <see cref="InvalidOperationException"/>。
        /// </summary>
        T Value { get; }

        bool IsDisposed { get; }

        /// <summary>每次热重载 +1。M8 之前恒为 0。</summary>
        int Version { get; }

        /// <summary>热重载完成后触发，参数是新的 <see cref="Version"/>。M8 之前不会触发。</summary>
        event Action<int> Reloaded;
    }
}
