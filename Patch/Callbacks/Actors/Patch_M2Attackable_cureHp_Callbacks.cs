using HarmonyLib;
using m2d;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>M2Attackable.cureHp(int)</c> 是唯一真正改 <c>hp</c> 字段的地方；PR/NelEnemy 各自的
    /// <c>cureHp</c> 覆写都经过若干层最终调用 <c>base.cureHp(val)</c>，打这一处即可覆盖两边。
    /// </summary>
    [HarmonyPatch(typeof(M2Attackable), nameof(M2Attackable.cureHp), new[] { typeof(int) })]
    [PolarisPatchFeature(GameCallbackKind.RecoveryApplied)]
    internal static class Patch_M2Attackable_cureHp_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(M2Attackable __instance, out int __state) => __state = __instance.hp;

        [HarmonyPostfix]
        static void Postfix(M2Attackable __instance, int __state)
        {
            int delta = __instance.hp - __state;
            if (delta > 0)
            {
                CombatCallbacks.PublishRecoveryApplied(CharacterGameAPI.HandleOf(__instance), delta, 0);
            }
        }
    }
}
