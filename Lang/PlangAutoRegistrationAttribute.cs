using System;

namespace Polaris.Lang
{
    /// <summary>
    /// 标注在某个 <see cref="IPlangRegistrar"/> 实现上，表示它参与自动注册：
    /// <see cref="PlangRegistryScanner.ScanAll"/> 会扫描已加载的插件程序集，为每个带这个特性
    /// 的类型创建一份实例并调用 <see cref="IPlangRegistrar.Register"/>。写法仿
    /// <c>Polaris.PUI.PUIAutoRegistrationAttribute</c>——同一套"特性标记 + 类型扫描 +
    /// Activator.CreateInstance"在这个系列里已经用在设置项扫描和 PUI 自动注册上，这里不再
    /// 发明一套新机制。因此被标注的类型必须是 <c>public</c> 且有公开的无参构造函数
    /// （<c>Activator.CreateInstance</c> 默认只调公开构造函数）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PlangAutoRegistrationAttribute : Attribute
    {
    }
}
