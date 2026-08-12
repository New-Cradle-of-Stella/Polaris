using HarmonyLib;
using nel;
using PixelLiner.PixelLinerLib;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>COOK.initGameScene</c> 是"读档或落回新游戏"的顶层入口：<c>__result</c> 为 <c>true</c>
    /// 表示成功读到了存档内容，<c>false</c> 表示读档失败并已经在方法内部落回 <c>newGame</c>。
    /// </summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.initGameScene), new[] { typeof(NelM2DBase) })]
    [PolarisPatchFeature("GameSceneStarted")]
    internal static class Patch_COOK_initGameScene_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(bool __result) => GameCallbackPublishers.GameSceneStarted(__result);
    }

    /// <summary>
    /// <c>COOK.newGame</c> 是新游戏初始化的唯一入口（<c>initGameScene</c> 读档失败时也会落到这里）。
    /// </summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.newGame), new[] { typeof(NelM2DBase), typeof(bool) })]
    [PolarisPatchFeature("NewGameStarted")]
    internal static class Patch_COOK_newGame_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix() => GameCallbackPublishers.NewGameStarted();
    }

    /// <summary>
    /// <c>COOK.readBinaryContent</c> 是私有方法（Publicizer 已经让它在本项目里可见），
    /// 是"存档二进制 -&gt; 内存"这一步唯一的真实入口。<c>__result</c> 为 <c>false</c> 时
    /// <c>COOK.load_failure_announce</c> 已经带着失败原因。
    /// </summary>
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

    /// <summary>
    /// <c>COOK.createBinary</c> 只把当前游戏状态序列化成内存里的二进制数据，<b>不代表已经落盘</b>——
    /// 落盘结果要看 <c>SVD.saveBinary</c> 的返回值。
    /// </summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.createBinary),
        new[] { typeof(ByteArray), typeof(SVD.sFile), typeof(NelM2DBase), typeof(bool), typeof(bool) })]
    [PolarisPatchFeature("SaveSerialized")]
    internal static class Patch_COOK_createBinary_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(ByteArray __result) => GameCallbackPublishers.SaveSerialized((int)(__result?.Length ?? 0));
    }

    /// <summary>
    /// <c>SVD.saveBinary</c> 的返回值就是"存档到底写没写成功"的最终答案：<c>null</c> 表示成功，
    /// 非空字符串是失败原因。序列化完成不等于这里也成功。
    /// </summary>
    [HarmonyPatch(typeof(SVD), nameof(SVD.saveBinary), new[] { typeof(SVD.sFile), typeof(ByteArray) })]
    [PolarisPatchFeature("SaveWritten")]
    internal static class Patch_SVD_saveBinary_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(string __result, SVD.sFile Sf)
            => GameCallbackPublishers.SaveWritten(Sf?.index ?? -1, __result == null);
    }

    /// <summary>
    /// <c>COOK.autoSave</c> 包了一整套"能不能存 -&gt; 序列化 -&gt; 落盘 -&gt; 失败回滚/成功提示"。
    /// 这里只发粗粒度的完成事件；细粒度的两步由 <c>createBinary</c>/<c>saveBinary</c> 的补丁负责。
    /// <para>
    /// <c>__result == null</c> 覆盖两种情况：没有强制且 <c>canSave()</c> 为假（根本没试着存），
    /// 以及少数内部早退路径；<c>COOK.save_failure_announce</c> 非空覆盖"试了但失败"。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.autoSave), new[] { typeof(NelM2DBase), typeof(bool), typeof(bool) })]
    [PolarisPatchFeature("AutoSaveCompleted")]
    internal static class Patch_COOK_autoSave_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(UILogRow __result)
            => GameCallbackPublishers.AutoSaveCompleted(__result != null && COOK.save_failure_announce == "");
    }
}
