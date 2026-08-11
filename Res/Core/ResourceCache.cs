using System;
using System.Collections.Generic;

namespace Polaris.Res.Core
{
    /// <summary>
    /// 全局资源缓存主表。<see cref="ResourceId"/> → <see cref="ResourceCacheEntry"/>。
    /// <para>
    /// M1 阶段只提供 <see cref="AcquireSync{T}"/>：加载器同步跑完，成功即缓存，失败直接
    /// 向上抛异常且不进缓存（下次调用会重新尝试，不会把一次性的 IO 错误固化下来）。
    /// M4 会加一条异步获取路径，复用同一套 <see cref="ResourceCacheEntry"/>/<see cref="Lease{T}"/>，
    /// 届时 <see cref="ResourceState.Loading"/>/<see cref="ResourceState.Faulted"/> 才会真正用上。
    /// </para>
    /// <para>
    /// 引用计数归零目前是立即卸载（调用 <see cref="ResourceCacheEntry.Unloader"/> 并从表里移除）；
    /// M6 会加宽限期，避免同一个界面反复开关时反复加载/卸载。所有变更都只应在主线程发生——
    /// 唯一的例外是 <see cref="Lease{T}"/> 的终结器，它会把减引用计数动作排到主线程再执行。
    /// </para>
    /// </summary>
    internal static class ResourceCache
    {
        private static readonly Dictionary<ResourceId, ResourceCacheEntry> entries =
            new Dictionary<ResourceId, ResourceCacheEntry>();

        /// <summary>
        /// 取或建一个缓存条目并返回租约。<paramref name="loader"/> 只在条目不存在时调用一次，
        /// 应同步跑完并返回最终值 + 对应的卸载动作（没有可传 null，比如 <c>byte[]</c> 不需要
        /// 显式清理）；抛出的异常会原样向上传播，不落进缓存。
        /// <para>
        /// 之所以让 loader 把值和卸载动作一起返回，而不是分两个参数传：像 <c>Image</c>
        /// 这种要在内部再持有一个 <c>Texture</c> 租约的场景，卸载动作需要捕获这个内部租约，
        /// 而这个内部租约本身就是 loader 执行过程中才产生的中间状态——写成一个返回值元组，
        /// loader 想在闭包里捕获什么中间状态都行，不用另外发明一套"中间状态"参数。
        /// </para>
        /// </summary>
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
                Plugin.Logger.LogError($"[PolarisRes] 卸载 {entry.Id} 时出现异常：{ex}");
            }
        }

        /// <summary>供 M1 里程碑验证用："统计归零"——引用计数应在最后一个租约释放后回到 0。</summary>
        internal static int DebugRefCount(ResourceId id) =>
            entries.TryGetValue(id, out ResourceCacheEntry entry) ? entry.RefCount : 0;

        internal static int DebugEntryCount => entries.Count;
    }
}
