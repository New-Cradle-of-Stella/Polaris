using System;

namespace Polaris.Res
{
    /// <summary>一次性的资源租约；<see cref="Dispose"/> 减少引用计数，归零时卸载资源。重复 Dispose 必须无害。</summary>
    public interface IResourceLease<out T> : IDisposable
    {
        ResourceId Id { get; }

        /// <summary>已释放抛 <see cref="ObjectDisposedException"/>；加载失败抛 <see cref="ResourceLoadException"/>/<see cref="ResourceNotFoundException"/>；仍在加载中抛 <see cref="InvalidOperationException"/>。</summary>
        T Value { get; }

        bool IsDisposed { get; }

        /// <summary>每次热重载递增。</summary>
        int Version { get; }

        /// <summary>热重载完成后触发，参数是新的 <see cref="Version"/>。</summary>
        event Action<int> Reloaded;
    }
}
