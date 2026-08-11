using HarmonyLib;
using nel.gm;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// 纯通知，不改变任何行为——与 <c>Patch_UiGameMenu_activate_WorldPause</c>（世界暂停 transpiler）
    /// 是两个独立的补丁类，Harmony 允许同一方法叠加多个 Prefix/Postfix/Transpiler。
    /// </summary>
    [HarmonyPatch(typeof(UiGameMenu), nameof(UiGameMenu.activate))]
    [PolarisPatchFeature(GameCallbackKind.GameMenuOpening)]
    [PolarisPatchFeature(GameCallbackKind.GameMenuOpened)]
    internal static class Patch_UiGameMenu_activate_Notify
    {
        [HarmonyPrefix]
        static void Prefix() => UiCallbacks.PublishGameMenuOpening();

        [HarmonyPostfix]
        static void Postfix() => UiCallbacks.PublishGameMenuOpened();
    }
}
