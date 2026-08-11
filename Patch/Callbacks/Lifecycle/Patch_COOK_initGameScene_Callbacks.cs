using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>COOK.initGameScene</c> 是"读档或落回新游戏"的顶层入口：<c>__result</c> 为 <c>true</c>
    /// 表示成功读到了存档内容，<c>false</c> 表示读档失败并已经在方法内部落回 <c>newGame</c>。
    /// </summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.initGameScene), new[] { typeof(NelM2DBase) })]
    [PolarisPatchFeature(GameCallbackKind.GameSceneStarting)]
    [PolarisPatchFeature(GameCallbackKind.GameSceneStarted)]
    internal static class Patch_COOK_initGameScene_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix() => LifecycleCallbacks.PublishGameSceneStarting();

        [HarmonyPostfix]
        static void Postfix(bool __result) => LifecycleCallbacks.PublishGameSceneStarted(__result);
    }
}
