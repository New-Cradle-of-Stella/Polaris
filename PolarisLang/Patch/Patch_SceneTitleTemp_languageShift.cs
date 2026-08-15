using HarmonyLib;
using nel.title;

namespace Polaris.Patch
{
    /// <summary>
    /// 标题告知页（见 <see cref="TitleOverlays"/>）显示期间，屏蔽 LTab/RTab 这对换语言快捷键：
    /// 告知页只压低了按钮的 alpha，键盘/手柄输入不经过命中测试仍会触发 <c>languageShift</c>，
    /// 这是"换语言"唯一的键盘入口，故拦在此处而非按键读取处。告知页自身的方向键换语言不受影响。
    /// </summary>
    [HarmonyPatch(typeof(SceneTitleTemp), "languageShift")]
    internal static class Patch_SceneTitleTemp_languageShift
    {
        [HarmonyPrefix]
        static bool Prefix() => !TitleOverlays.IsShowing;
    }
}
