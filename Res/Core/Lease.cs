using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Polaris.Res.Core
{
    /// <summary><see cref="IResourceLease{T}"/> 的唯一实现，被所有加载路径（同步/异步）共用。</summary>
    internal sealed class Lease<T> : IResourceLease<T>
    {
        private readonly ResourceCacheEntry entry;
        private int disposedFlag; // 0 = 活跃, 1 = 已释放

#pragma warning disable 67 // Reloaded 目前从不触发；提前定义在公开接口里避免以后加接口成员。
        public event Action<int> Reloaded;
#pragma warning restore 67

        internal Lease(ResourceCacheEntry entry)
        {
            this.entry = entry;
        }

        public ResourceId Id => entry.Id;

        public int Version => entry.Version;

        public bool IsDisposed => Volatile.Read(ref disposedFlag) != 0;

        public T Value
        {
            get
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(nameof(Lease<T>), $"Lease already released: {entry.Id}");
                }

                switch (entry.State)
                {
                    case ResourceState.Ready:
                        return (T)entry.Value;

                    case ResourceState.Faulted:
                        // 原样重新抛出已知的诊断异常（保留堆栈），不再套一层 ResourceLoadException。
                        if (entry.Error is ResourceNotFoundException || entry.Error is ResourceLoadException)
                        {
                            ExceptionDispatchInfo.Capture(entry.Error).Throw();
                        }

                        throw new ResourceLoadException(entry.Id, $"Load failed: {entry.Error?.Message}", entry.Error);

                    default:
                        // 同步加载路径不会走到这里；留给异步的 Loading/Pending 状态。
                        throw new InvalidOperationException($"Resource is not ready yet: {entry.Id} (current state {entry.State})");
                }
            }
        }

        public void Dispose()
        {
            // CAS 只可能成功一次，保证重复 Dispose（using/手动/终结器）无害。
            if (Interlocked.Exchange(ref disposedFlag, 1) != 0)
            {
                return;
            }

            GC.SuppressFinalize(this);
            ResourceCache.Release(entry);
        }

        ~Lease()
        {
            if (Volatile.Read(ref disposedFlag) != 0)
            {
                return;
            }

            // 终结器线程不能直接触碰非线程安全的 ResourceCache；把减引用计数动作排到主线程（ResPump 每帧 Drain）执行。
            try
            {
                Plugin.Logger.LogWarning($"[PolarisRes] Detected an unreleased lease (reclaimed by the finalizer): {entry.Id}");
            }
            catch
            {
                // 终结器里绝不能再抛异常。
            }

            Polaris.Res.Runtime.MainThreadDispatcher.Enqueue(() => ResourceCache.Release(entry));
        }
    }
}
