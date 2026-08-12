using Polaris.PUI.Wire;
using UnityEngine;

namespace Polaris.PUI.HotReload
{
    /// <summary>游戏侧把中立的 <see cref="PuiColor"/> 转成 Unity 的 <see cref="Color32"/>。</summary>
    internal static class PuiColorExtensions
    {
        internal static Color32 ToColor32(this PuiColor color) => new(color.R, color.G, color.B, color.A);
    }
}
