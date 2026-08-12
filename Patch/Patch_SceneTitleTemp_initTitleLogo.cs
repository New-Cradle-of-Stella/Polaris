using HarmonyLib;
using nel.title;

namespace Polaris.Patch
{
    /// <summary>
    /// 在标题画面右下角版本号（<c>TxVer</c>，<c>aligny = BOTTOM</c>）下面追加一行 Polaris 版本号；
    /// 直接复用该 <c>TextRenderer</c> 而不另起一个，以沿用其淡入动画和每帧跟随 logo 的定位。
    /// 换语言会重置该文本，由 <see cref="Patch_SceneTitleTemp_fineTexts"/> 再补一次，文本见 <see cref="TitleVersionLine"/>。
    /// </summary>
    [HarmonyPatch(typeof(SceneTitleTemp), nameof(SceneTitleTemp.initTitleLogo))]
    internal static class Patch_SceneTitleTemp_initTitleLogo
    {
        [HarmonyPostfix]
        static void Postfix(SceneTitleTemp __instance)
        {
            TitleVersionLine.Append(__instance);
        }
    }
}
