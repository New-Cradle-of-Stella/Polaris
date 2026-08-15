using System;
using System.Collections.Generic;

namespace Polaris.Res.Core
{
    /// <summary>
    /// 全局资源缓存主表（<see cref="ResourceId"/> → <see cref="ResourceCacheEntry"/>）。加载失败不进缓存，下次调用会重新尝试。
    /// 引用计数归零立即卸载；所有变更只应在主线程发生（<see cref="Lease{T}"/> 的终结器会把操作转发到主线程）。
    /// </summary>
    internal static class ResourceCache
    {
        private static readonly Dictionary<ResourceId, ResourceCacheEntry> entries =
            new Dictionary<ResourceId, ResourceCacheEntry>();

        /// <summary>取或建缓存条目并返回租约；<paramref name="loader"/> 只在条目不存在时调用一次，同步返回值+卸载动作，异常原样向上传播不进缓存。</summary>
        internal static IResourceLease<T> AcquireSync<T>(ResourceId id, Func<(T Value, Action Unloader)> loader)
        {
            if (!entries.TryGetValue(id, out ResourceCacheEntry entry))
            {
                (T value, Action unloader) = loader();
                entry = new ResourceCacheEntry
                {
                    Id = id,
                    State = ResourceState.Ready,
                    Value = value,
                    Unloader = unloader,
                };
                entries[id] = entry;
            }

            entry.RefCount++;
            return new Lease<T>(entry);
        }

        /// <summary>由 <see cref="Lease{T}.Dispose"/> 调用；只应在主线程执行。</summary>
        internal static void Release(ResourceCacheEntry entry)
        {
            entry.RefCount--;
            if (entry.RefCount > 0)
            {
                return;
            }

            entries.Remove(entry.Id);
            entry.State = ResourceState.Unloaded;

            try
            {
                entry.Unloader?.Invoke();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[PolarisRes] Exception while unloading {entry.Id}: {ex}");
            }
        }
    }
}
