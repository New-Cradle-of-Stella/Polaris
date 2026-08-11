using HarmonyLib;
using nel;
using PixelLiner.PixelLinerLib;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>SVD.saveBinary</c> 的返回值就是"存档到底写没写成功"的最终答案：<c>null</c> 表示成功，
    /// 非空字符串是失败原因。<c>createBinary</c> 序列化完成不等于这里也成功。
    /// </summary>
    [HarmonyPatch(typeof(SVD), nameof(SVD.saveBinary), new[] { typeof(SVD.sFile), typeof(ByteArray) })]
    [PolarisPatchFeature(GameCallbackKind.SaveWriting)]
    [PolarisPatchFeature(GameCallbackKind.SaveWritten)]
    internal static class Patch_SVD_saveBinary_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(SVD.sFile Sf) => LifecycleCallbacks.PublishSaveWriting(Sf?.index ?? -1);

        [HarmonyPostfix]
        static void Postfix(string __result, SVD.sFile Sf)
            => LifecycleCallbacks.PublishSaveWritten(Sf?.index ?? -1, __result == null, __result);
    }
}
