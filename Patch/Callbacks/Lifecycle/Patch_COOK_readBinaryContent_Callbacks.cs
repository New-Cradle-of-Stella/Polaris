using HarmonyLib;
using nel;
using PixelLiner.PixelLinerLib;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>COOK.readBinaryContent</c> 是私有方法（Publicizer 已经让它在本项目里可见），
    /// 是"存档二进制 -&gt; 内存"这一步唯一的真实入口。<c>__result</c> 为 <c>false</c> 时
    /// <c>COOK.load_failure_announce</c> 已经带着失败原因。
    /// </summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.readBinaryContent), new[] { typeof(ByteArray), typeof(SVD.sFile), typeof(NelM2DBase) })]
    [PolarisPatchFeature(GameCallbackKind.SaveLoading)]
    [PolarisPatchFeature(GameCallbackKind.SaveLoaded)]
    [PolarisPatchFeature(GameCallbackKind.SaveFailed)]
    internal static class Patch_COOK_readBinaryContent_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(SVD.sFile Sf) => LifecycleCallbacks.PublishSaveLoading(Sf?.index ?? -1);

        [HarmonyPostfix]
        static void Postfix(bool __result, SVD.sFile Sf)
        {
            int slot = Sf?.index ?? -1;
            if (__result)
            {
                LifecycleCallbacks.PublishSaveLoaded(slot);
            }
            else
            {
                LifecycleCallbacks.PublishSaveFailed(slot, COOK.load_failure_announce);
            }
        }
    }
}
