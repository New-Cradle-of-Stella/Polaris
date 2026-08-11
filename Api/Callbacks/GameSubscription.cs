using System;

namespace Polaris.API
{
    /// <summary>
    /// 一次订阅的句柄。<see cref="Dispose"/> 是取消订阅的唯一方式，不要求下游手动配对 <c>+=/-=</c>。
    /// </summary>
    public sealed class GameSubscription : IDisposable
    {
        readonly Action onDispose;
        volatile bool active = true;

        internal GameSubscription(Action onDispose, string ownerPluginGuid, string debugName)
        {
            this.onDispose = onDispose;
            OwnerPluginGuid = ownerPluginGuid;
            DebugName = debugName;
        }

        /// <summary>是否仍会收到事件；<see cref="Dispose"/> 或 <c>Once</c> 触发后为 <c>false</c>。</summary>
        public bool IsActive => active;

        /// <summary>订阅方所在的 BepInEx 插件 GUID；无法映射时是程序集名。</summary>
        public string OwnerPluginGuid { get; }

        /// <summary>调用方传入的可读名字；未提供时为 <c>null</c>。</summary>
        public string DebugName { get; }

        public void Dispose()
        {
            if (!active)
            {
                return;
            }

            active = false;
            onDispose?.Invoke();
        }

        /// <summary>由 <see cref="GameSignal{T}"/> 在 <c>Once</c> 触发后调用，只改标志，不再触发一次移除逻辑。</summary>
        internal void MarkInactiveOnly() => active = false;
    }
}
