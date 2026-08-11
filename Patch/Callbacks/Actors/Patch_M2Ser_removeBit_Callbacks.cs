using HarmonyLib;
using m2d;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary><c>M2Ser.removeBit</c> 无条件清位，不管之前是不是已经清过——只在真正发生
    /// "从有到无"这次翻转时才算一次状态移除。</summary>
    [HarmonyPatch(typeof(M2Ser), nameof(M2Ser.removeBit))]
    [PolarisPatchFeature(GameCallbackKind.StatusRemoved)]
    internal static class Patch_M2Ser_removeBit_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(M2Ser __instance, SER ser, out bool __state)
            => __state = (__instance.ser_bits & (ulong)(1L << (int)ser)) != 0;

        [HarmonyPostfix]
        static void Postfix(M2Ser __instance, SER ser, bool __state)
        {
            if (!__state || __instance.Mv == null)
            {
                return;
            }

            ActorCallbacks.PublishStatusRemoved(CharacterGameAPI.HandleOf(__instance.Mv), (int)ser);
        }
    }
}
