using HarmonyLib;
using m2d;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    [HarmonyPatch(typeof(PR), nameof(PR.addKnockbackVelocity),
        new[] { typeof(float), typeof(AttackInfo), typeof(M2Attackable), typeof(FOCTYPE) })]
    [PolarisPatchFeature(GameCallbackKind.KnockbackApplied)]
    internal static class Patch_PR_addKnockbackVelocity_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(PR __instance) => CombatCallbacks.PublishKnockbackApplied(CharacterGameAPI.HandleOf(__instance));
    }
}
