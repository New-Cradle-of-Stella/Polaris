using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>NelEnemy.initDeath()</c> returns <c>true</c> both when it just killed the enemy AND when
    /// it was already dead (no-op fast path) — <c>__result</c> alone can't tell those apart.
    /// Capture "was already DIE" in the Prefix and only fire when that flips from false to true.
    /// </summary>
    [HarmonyPatch(typeof(NelEnemy), nameof(NelEnemy.initDeath), new System.Type[0])]
    [PolarisPatchFeature(GameCallbackKind.EnemyDied)]
    internal static class Patch_NelEnemy_initDeath_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(NelEnemy __instance, out bool __state) => __state = __instance.state == NelEnemy.STATE.DIE;

        [HarmonyPostfix]
        static void Postfix(NelEnemy __instance, bool __result, bool __state)
        {
            if (__state || !__result)
            {
                return;
            }

            ActorCallbacks.PublishEnemyDied(CharacterGameAPI.HandleOf(__instance));
        }
    }
}
