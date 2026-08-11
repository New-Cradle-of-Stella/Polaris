using HarmonyLib;
using m2d;
using Polaris.API;
using Polaris.Infra;

namespace Polaris.Patch
{
    /// <summary>镜像 <see cref="Patch_M2Attackable_applyHpDamage_Callbacks"/>：PR 和 NelEnemy 的
    /// <c>applyMpDamage</c> 覆写链最终都调用 <c>base.applyMpDamage</c>，只需要打这一处。</summary>
    [HarmonyPatch(typeof(M2Attackable), nameof(M2Attackable.applyMpDamage), new[] { typeof(int), typeof(bool), typeof(AttackInfo) })]
    [PolarisPatchFeature(GameCallbackKind.MpDamageApplied)]
    internal static class Patch_M2Attackable_applyMpDamage_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(M2Attackable __instance, int __result)
        {
            if (__result == 0)
            {
                return;
            }

            CombatCallbacks.PublishMpDamageApplied(CallbackOperationScope.CurrentId,
                CharacterGameAPI.HandleOf(__instance), __result, __instance.mp);
        }
    }
}
