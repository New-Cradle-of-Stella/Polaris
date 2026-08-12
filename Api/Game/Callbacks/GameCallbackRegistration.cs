using System;

namespace Polaris.API
{
    /// <summary>
    /// 一次回调注册的句柄；<see cref="Dispose"/> 是取消注册的唯一方式。
    /// 变为非活跃的时机：显式 Dispose、Once 触发一次、或绑定的实例失效。
    /// </summary>
    public sealed class GameCallbackRegistration : IDisposable
    {
        readonly Action onDispose;
        volatile bool active = true;

        internal GameCallbackRegistration(Action onDispose, string ownerPluginGuid, string debugName)
        {
            this.onDispose = onDispose;
            OwnerPluginGuid = ownerPluginGuid;
            DebugName = debugName;
        }

        /// <summary>是否仍会收到事件。</summary>
        public bool IsActive => active;

        /// <summary>注册方所在的 BepInEx 插件 GUID；无法映射时是程序集名。</summary>
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

        /// <summary>由派发核心在 <c>Once</c> 触发或实例失效后调用：只改标志，不再走一次移除逻辑。</summary>
        internal void MarkInactiveOnly() => active = false;
    }
}
