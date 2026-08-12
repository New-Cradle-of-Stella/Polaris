using System;

namespace Polaris.PUI
{
    /// <summary>
    /// 标注在 .puisln 生成的静态图类上（须暴露 <c>public static PUIGraphDefinition Definition</c>）。<see cref="PUIManager.Init"/> 会登记该图并自动创建一份默认共享实例。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PUISolutionAutoRegistrationAttribute : Attribute
    {
    }
}
