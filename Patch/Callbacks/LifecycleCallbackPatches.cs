using HarmonyLib;
using nel;
using PixelLiner.PixelLinerLib;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>"读档或落回新游戏"的顶层入口；<c>__result == true</c> 表示读档成功，否则已在内部落回 <c>newGame</c>。</summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.initGameScene), new[] { typeof(NelM2DBase) })]
    [PolarisPatchFeature("GameSceneStarted")]
    internal static class Patch_COOK_initGameScene_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(bool __result) => GameCallbackPublishers.GameSceneStarted(__result);
    }

    /// <summary>新游戏初始化的唯一入口，读档失败时也会落到这里。</summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.newGame), new[] { typeof(NelM2DBase), typeof(bool) })]
    [PolarisPatchFeature("NewGameStarted")]
    internal static class Patch_COOK_newGame_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix() => GameCallbackPublishers.NewGameStarted();
    }

    /// <summary>"存档二进制 -&gt; 内存"唯一入口；<c>__result == false</c> 时 <c>COOK.load_failure_announce</c> 带失败原因。</summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.readBinaryContent), new[] { typeof(ByteArray), typeof(SVD.sFile), typeof(NelM2DBase) })]
    [PolarisPatchFeature("SaveLoaded")]
    [PolarisPatchFeature("SaveFailed")]
    internal static class Patch_COOK_readBinaryContent_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(bool __result, SVD.sFile Sf)
        {
            int slot = Sf?.index ?? -1;
            if (__result)
            {
                GameCallbackPublishers.SaveLoaded(slot);
            }
            else
            {
                GameCallbackPublishers.SaveFailed(slot, COOK.load_failure_announce);
            }
        }
    }

    /// <summary>只把游戏状态序列化为内存二进制，<b>不代表已落盘</b>——落盘结果看 <c>SVD.saveBinary</c>。</summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.createBinary),
        new[] { typeof(ByteArray), typeof(SVD.sFile), typeof(NelM2DBase), typeof(bool), typeof(bool) })]
    [PolarisPatchFeature("SaveSerialized")]
    internal static class Patch_COOK_createBinary_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(ByteArray __result) => GameCallbackPublishers.SaveSerialized((int)(__result?.Length ?? 0));
    }

    /// <summary>返回值是存档写入成败的最终答案：<c>null</c> 为成功，非空字符串是失败原因；序列化成功不代表这里也成功。</summary>
    [HarmonyPatch(typeof(SVD), nameof(SVD.saveBinary), new[] { typeof(SVD.sFile), typeof(ByteArray) })]
    [PolarisPatchFeature("SaveWritten")]
    internal static class Patch_SVD_saveBinary_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(string __result, SVD.sFile Sf)
            => GameCallbackPublishers.SaveWritten(Sf?.index ?? -1, __result == null);
    }

    /// <summary>包了整套自动存档流程，只发粗粒度完成事件；细粒度两步由 <c>createBinary</c>/<c>saveBinary</c> 的补丁负责。</summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.autoSave), new[] { typeof(NelM2DBase), typeof(bool), typeof(bool) })]
    [PolarisPatchFeature("AutoSaveCompleted")]
    internal static class Patch_COOK_autoSave_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(UILogRow __result)
            => GameCallbackPublishers.AutoSaveCompleted(__result != null && COOK.save_failure_announce == "");
    }
}
