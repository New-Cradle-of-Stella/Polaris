using System.Reflection;
using HarmonyLib;
using m2d;
using nel;
using Polaris.API;
using Polaris.Infra;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>PR.applyDamage(NelAttackInfo, ref HITTYPE, bool)</c> 是玩家一次语义攻击的顶层入口
    /// （内部转发给 <c>DMG.applyDamage</c>）。用 hp 前后差算实际伤害，而不是信任 <c>__result</c>
    /// 的语义——没有核对过 <c>DMG.applyDamage</c> 内部对返回值的约定，hp 差值才是地面真相。
    /// <see cref="CallbackOperationScope"/> 在这里开一个作用域，往下调用的
    /// <c>M2Attackable.applyHpDamage</c>/<c>applyMpDamage</c> 补丁复用同一个 OperationId。
    /// <para>
    /// 目标方法带 <c>ref</c> 参数，<c>MakeByRefType()</c> 不是特性实参允许的常量表达式，
    /// 所以用 <c>TargetMethod</c> 让 Harmony 在运行时解析目标，而不是写在 <c>[HarmonyPatch]</c> 里。
    /// </para>
    /// </summary>
    [HarmonyPatch]
    [PolarisPatchFeature(GameCallbackKind.DamageApplied)]
    internal static class Patch_PR_applyDamage_Callbacks
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(typeof(PR), nameof(PR.applyDamage), new[] { typeof(NelAttackInfo), typeof(HITTYPE).MakeByRefType(), typeof(bool) });

        [HarmonyPrefix]
        static void Prefix(PR __instance, out int __state)
        {
            CallbackOperationScope.Enter();
            __state = __instance.hp;
        }

        [HarmonyPostfix]
        static void Postfix(PR __instance, int __state)
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
