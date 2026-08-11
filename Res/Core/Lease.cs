using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Polaris.Res.Core
{
    /// <summary>
    /// <see cref="IResourceLease{T}"/> 的唯一实现，被 M1 起的所有加载路径（同步/异步）共用。
    /// </summary>
    internal sealed class Lease<T> : IResourceLease<T>
    {
        private readonly ResourceCacheEntry entry;
        private int disposedFlag; // 0 = 活跃, 1 = 已释放

#pragma warning disable 67 // Reloaded 要等 M8 热重载落地才会真正触发；提前定义在公开接口里避免以后加接口成员。
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
                        // 找不到/加载失败这两类异常已经是成品诊断信息，原样重新抛出（保留堆栈），
                        // 不再套一层无意义的 ResourceLoadException。
                        if (entry.Error is ResourceNotFoundException || entry.Error is ResourceLoadException)
                        {
                            ExceptionDispatchInfo.Capture(entry.Error).Throw();
                        }

                        throw new ResourceLoadException(entry.Id, $"Load failed: {entry.Error?.Message}", entry.Error);

                    default:
                        // M1/M2 的同步加载路径不会走到这里；留给 M4 的异步 Loading/Pending 状态。
                        throw new InvalidOperationException($"Resource is not ready yet: {entry.Id} (current state {entry.State})");
                }
            }
        }

        public void Dispose()
        {
            // CAS 只可能成功一次；重复 Dispose 天然无害——using、手动 Dispose、终结器
            // 三条路径可能同时到达同一个租约。
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

            // 终结器线程绝不能直接触碰 ResourceCache——它内部是个普通 Dictionary，
            // 不是线程安全的，主线程同时在读写会导致数据损坏。真正的减引用计数动作
            // 排到主线程（ResPump 每帧会 Drain）执行；完整的泄漏堆栈上报留给 M6 的
            // LeaseRegistry，这里先保证引用计数本身不会因为忘记 Dispose 而永久泄漏。
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
