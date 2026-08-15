using System;

namespace Polaris.Lang
{
    /// <summary>
    /// 标注在某个 <see cref="IPlangRegistrar"/> 实现上，参与 <see cref="PlangRegistryScanner.ScanAll"/> 的自动扫描注册。
    /// 被标注的类型必须是 <c>public</c> 且有公开的无参构造函数（供 <c>Activator.CreateInstance</c> 使用）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PlangAutoRegistrationAttribute : Attribute
    {
    }
}
