using System;

namespace Polaris.PUI
{
    /// <summary>
    /// 标注在某个 <see cref="IPUI"/> 实现上，表示它参与自动注册：<see cref="PUIManager.Init"/>
    /// 会扫描所有已加载程序集，为每个带这个特性的类型创建一份实例并按
    /// <see cref="IPUI.Name"/> 注册为进程级共享实例，同时把 Name -&gt; 类型登记进类型目录，
    /// 供 .puisln 图节点（<see cref="PUINodeDefinition.PuiName"/>）解析出具体类型。
    /// 因此被标注的类型必须有公开的无参构造函数。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PUIAutoRegistrationAttribute : Attribute
    {
    }
}
