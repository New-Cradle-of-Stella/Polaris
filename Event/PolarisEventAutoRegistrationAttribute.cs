using System;

namespace Polaris.Event
{
    /// <summary>
    /// 标在 MSBuild 生成的 registrar 类上，携带该 registrar 所属项目的 <c>PolarisEventNamespace</c>。
    /// 命名空间随特性一起走，而不是要求 <see cref="IPolarisEventRegistrar.Register"/> 的调用方另外传参——
    /// 那个方法的调用方是生成代码，改签名等于要求所有下游模组重新生成一遍代码才能升级 Polaris
    /// （同样的取舍见 <c>Lang\PlangConflictGuard.cs</c> 里 <c>CurrentSource</c> 的注释）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PolarisEventAutoRegistrationAttribute : Attribute
    {
        public string Namespace { get; }

        public PolarisEventAutoRegistrationAttribute(string @namespace)
        {
            Namespace = @namespace;
        }
    }
}
