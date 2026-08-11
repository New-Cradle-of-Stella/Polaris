using HarmonyLib;
using m2d;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    [HarmonyPatch(typeof(NelEnemy), nameof(NelEnemy.addKnockbackVelocity),
        new[] { typeof(float), typeof(AttackInfo), typeof(M2Attackable), typeof(FOCTYPE) })]
    [PolarisPatchFeature(GameCallbackKind.KnockbackApplied)]
    internal static class Patch_NelEnemy_addKnockbackVelocity_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(NelEnemy __instance) => CombatCallbacks.PublishKnockbackApplied(CharacterGameAPI.HandleOf(__instance));
    }
}
