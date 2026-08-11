using HarmonyLib;
using m2d;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>镜像 <see cref="Patch_M2Attackable_cureHp_Callbacks"/>，MP 版本。</summary>
    [HarmonyPatch(typeof(M2Attackable), nameof(M2Attackable.cureMp), new[] { typeof(int) })]
    [PolarisPatchFeature(GameCallbackKind.RecoveryApplied)]
    internal static class Patch_M2Attackable_cureMp_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(M2Attackable __instance, out int __state) => __state = __instance.mp;

        [HarmonyPostfix]
        static void Postfix(M2Attackable __instance, int __state)
        {
            int delta = __instance.mp - __state;
            if (delta > 0)
            {
                CombatCallbacks.PublishRecoveryApplied(CharacterGameAPI.HandleOf(__instance), 0, delta);
            }
        }
    }
}
