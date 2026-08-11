using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>COOK.autoSave</c> 包了一整套"能不能存 -&gt; 序列化 -&gt; 落盘 -&gt; 失败回滚/成功提示"。
    /// 这里只发粗粒度的开始/完成；<c>createBinary</c>/<c>saveBinary</c> 的补丁已经发了细粒度的那两步。
    /// <para>
    /// <c>__result == null</c> 覆盖两种情况：没有强制且 <c>canSave()</c> 为假（根本没试着存），
    /// 以及少数内部早退路径；<c>COOK.save_failure_announce</c> 非空覆盖"试了但失败"。
    /// 两者都不算成功，用一个表达式即可覆盖，不需要分别处理。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.autoSave), new[] { typeof(NelM2DBase), typeof(bool), typeof(bool) })]
    [PolarisPatchFeature(GameCallbackKind.AutoSaveStarting)]
    [PolarisPatchFeature(GameCallbackKind.AutoSaveCompleted)]
    internal static class Patch_COOK_autoSave_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(bool is_bench) => LifecycleCallbacks.PublishAutoSaveStarting(is_bench);

        [HarmonyPostfix]
        static void Postfix(UILogRow __result, bool is_bench)
            => LifecycleCallbacks.PublishAutoSaveCompleted(is_bench, __result != null && COOK.save_failure_announce == "");
    }
}
