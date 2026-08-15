using System;

namespace Polaris.PUI
{
    /// <summary>
    /// 标注在 mod 的 BepInPlugin 类上以启用该程序集内 PUI 的热重载（改用 <see cref="HotReload.PUIHotReloadRuntime"/> 驱动并开放命名管道服务端）；未标注则行为不变。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PUIHotFixEnabledAttribute : Attribute
    {
    }
}
