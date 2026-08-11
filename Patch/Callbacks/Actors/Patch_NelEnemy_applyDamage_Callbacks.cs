using System.Reflection;
using HarmonyLib;
using m2d;
using nel;
using Polaris.API;
using Polaris.Infra;

namespace Polaris.Patch
{
    /// <summary>
    /// 敌人一侧的顶层伤害入口，镜像 <see cref="Patch_PR_applyDamage_Callbacks"/> 的做法：
    /// 用 hp 前后差算实际伤害，不信任 <c>__result</c> 的具体语义。同样用 <c>TargetMethod</c>
    /// 解析目标——<c>ref</c> 参数的 <c>MakeByRefType()</c> 不能直接写进 <c>[HarmonyPatch]</c>。
    /// </summary>
    [HarmonyPatch]
    [PolarisPatchFeature(GameCallbackKind.DamageApplied)]
    internal static class Patch_NelEnemy_applyDamage_Callbacks
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(typeof(NelEnemy), nameof(NelEnemy.applyDamage), new[] { typeof(NelAttackInfo), typeof(HITTYPE).MakeByRefType(), typeof(bool) });

        [HarmonyPrefix]
        static void Prefix(NelEnemy __instance, out int __state)
        {
            CallbackOperationScope.Enter();
            __state = __instance.hp;
        }

        [HarmonyPostfix]
        static void Postfix(NelEnemy __instance, int __state)
        {
            int hpBefore = __state;
            int hpAfter = __instance.hp;
            int actual = hpBefore - hpAfter;
            if (actual != 0)
            {
                CombatCallbacks.PublishDamageApplied(CallbackOperationScope.CurrentId,
                    CharacterGameAPI.HandleOf(__instance), actual, !__instance.is_alive);
            }
        }

        [HarmonyFinalizer]
        static void Finalizer() => CallbackOperationScope.Exit();
    }
}
