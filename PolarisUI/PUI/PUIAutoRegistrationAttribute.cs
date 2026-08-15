using System;

namespace Polaris.PUI
{
    /// <summary>
    /// 标注在 <see cref="IPUI"/> 实现上以参与自动注册：<see cref="PUIManager.Init"/> 会创建实例并按 <see cref="IPUI.Name"/> 注册为共享实例。要求有公开无参构造函数。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PUIAutoRegistrationAttribute : Attribute
    {
    }
}
