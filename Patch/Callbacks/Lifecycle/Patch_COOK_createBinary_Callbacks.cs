using HarmonyLib;
using nel;
using PixelLiner.PixelLinerLib;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>COOK.createBinary</c> 只把当前游戏状态序列化成内存里的二进制数据，<b>不代表已经落盘</b>——
    /// 落盘结果要看 <c>SVD.saveBinary</c> 的返回值（见 <see cref="Patch_SVD_saveBinary_Callbacks"/>）。
    /// </summary>
    [HarmonyPatch(typeof(COOK), nameof(COOK.createBinary),
        new[] { typeof(ByteArray), typeof(SVD.sFile), typeof(NelM2DBase), typeof(bool), typeof(bool) })]
    [PolarisPatchFeature(GameCallbackKind.SaveSerializing)]
    [PolarisPatchFeature(GameCallbackKind.SaveSerialized)]
    internal static class Patch_COOK_createBinary_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(SVD.sFile Sf) => LifecycleCallbacks.PublishSaveSerializing(Sf?.index ?? -1);

        [HarmonyPostfix]
        static void Postfix(ByteArray __result, SVD.sFile Sf)
            => LifecycleCallbacks.PublishSaveSerialized(Sf?.index ?? -1, (int)(__result?.Length ?? 0));
    }
}
