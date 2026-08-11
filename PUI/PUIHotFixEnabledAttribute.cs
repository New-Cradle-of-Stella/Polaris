using System;

namespace Polaris.PUI
{
    /// <summary>
    /// 标注在某个 mod 自己的 BepInPlugin 类上，表示"这个程序集里的 PUI 支持热重载"：
    /// <see cref="PUIManager"/> 会用 <see cref="PUIHotReloadRuntime"/>（而不是普通的
    /// <see cref="PUIRuntime"/>）驱动它旗下的每一个 PUI，并在游戏进程里起一个命名管道
    /// 服务端，接收可视化编辑器推送过来的热重载指令。
    /// 不标注这个特性的 mod 完全不受影响：既有的自动注册流程、<see cref="PUIRuntime"/>
    /// 行为、开销都跟没有这个功能时一样。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PUIHotFixEnabledAttribute : Attribute
    {
    }
}
