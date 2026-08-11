using HarmonyLib;
using m2d;
using Polaris.API;
using Polaris.Infra;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>M2Attackable.applyHpDamage</c> 是 PR 和 NelEnemy 共用的低层 HP 伤害入口——两者都没有覆写
    /// 这个 3 参数重载（NelEnemy 自己的 4 参数 <c>applyHpDamage(int, ref int, bool, NelAttackInfo)</c>
    /// 内部转发到这一个），所以只需要在这一处打补丁就能覆盖双方。<c>__result</c> 就是实际扣掉的血量。
    /// </summary>
    [HarmonyPatch(typeof(M2Attackable), nameof(M2Attackable.applyHpDamage), new[] { typeof(int), typeof(bool), typeof(AttackInfo) })]
    [PolarisPatchFeature(GameCallbackKind.HpDamageApplied)]
    internal static class Patch_M2Attackable_applyHpDamage_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(M2Attackable __instance, int __result)
        {
            if (__result == 0 || !CombatCallbacks.WantsHpMpDetail)
            {
                return;
            }

            CombatCallbacks.PublishHpDamageApplied(CallbackOperationScope.CurrentId,
                CharacterGameAPI.HandleOf(__instance), __result, __instance.hp);
        }
    }
}
