using System;

namespace Polaris.PUI
{
    /// <summary>
    /// 标注在 .puisln 生成的静态图类上（形如 {{FileName}}_Solution），该类须暴露
    /// <c>public static PUIGraphDefinition Definition</c>。<see cref="PUIManager"/> 在
    /// <see cref="PUIManager.Init"/> 时会扫描所有带这个特性的类型，把 Definition 登记进图目录，
    /// 并自动 CreateSolution() 一份默认共享实例，保留"编译完 .puisln 就能用"的零代码体验；
    /// 需要额外独立实例的 mod 可再调用 Definition.CreateSolution()。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PUISolutionAutoRegistrationAttribute : Attribute
    {
    }
}
