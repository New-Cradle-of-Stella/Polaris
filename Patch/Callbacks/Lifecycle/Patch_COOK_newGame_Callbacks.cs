using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>COOK.newGame</c> 是新游戏初始化的唯一入口（<c>initGameScene</c> 读档失败时也会落到这里）。
    /// Prefix/Postfix 各发一次，不改变原版行为。
    /// </summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.newGame), new[] { typeof(NelM2DBase), typeof(bool) })]
    [PolarisPatchFeature(GameCallbackKind.NewGameStarting)]
    [PolarisPatchFeature(GameCallbackKind.NewGameStarted)]
    internal static class Patch_COOK_newGame_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix() => LifecycleCallbacks.PublishNewGameStarting();

        [HarmonyPostfix]
        static void Postfix() => LifecycleCallbacks.PublishNewGameStarted();
    }
}
