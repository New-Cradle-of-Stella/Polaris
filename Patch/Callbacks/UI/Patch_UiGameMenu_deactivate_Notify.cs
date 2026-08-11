using HarmonyLib;
using nel.gm;
using Polaris.API;

namespace Polaris.Patch
{
    [HarmonyPatch(typeof(UiGameMenu), nameof(UiGameMenu.deactivate))]
    [PolarisPatchFeature(GameCallbackKind.GameMenuClosing)]
    [PolarisPatchFeature(GameCallbackKind.GameMenuClosed)]
    internal static class Patch_UiGameMenu_deactivate_Notify
    {
        [HarmonyPrefix]
        static void Prefix() => UiCallbacks.PublishGameMenuClosing();

        [HarmonyPostfix]
        static void Postfix() => UiCallbacks.PublishGameMenuClosed();
    }
}
