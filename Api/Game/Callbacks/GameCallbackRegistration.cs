using System;

namespace Polaris.API
{
    /// <summary>
    /// 一次回调注册的句柄。<see cref="Dispose"/> 是取消注册的唯一方式——不要求调用方
    /// 自己配对 <c>+=</c>/<c>-=</c>，也就不会出现"退订时传了另一个等价委托实例导致退不掉"
    /// 这种只在运行期才暴露的问题。
    /// <para>
    /// 三种情况会让它变为非活跃：显式 <see cref="Dispose"/>；
    /// <see cref="GameCallbackOptions.Once"/> 触发过一次；
    /// 以及（实例回调）它绑定的游戏实例已经失效。
    /// </para>
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
