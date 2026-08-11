using Polaris.PUI.Wire;
using UnityEngine;

namespace Polaris.PUI.HotReload
{
    /// <summary>
    /// 游戏侧把线协议的中立 <see cref="PuiColor"/> 转成 Unity 的 <see cref="Color32"/>。
    /// <para>
    /// 转换刻意留在协议文件<b>之外</b>：<c>PuiWireProtocol.cs</c> 要同时编进 net472 的
    /// 编辑器 VSIX，不能引用 UnityEngine。编辑器那边有它自己的一份到 WPF 颜色的转换。
    /// </para>
    /// </summary>
    internal static class PuiColorExtensions
    {
        internal static Color32 ToColor32(this PuiColor color) => new(color.R, color.G, color.B, color.A);
    }
}
