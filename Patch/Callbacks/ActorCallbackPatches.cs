using System.Reflection;
using HarmonyLib;
using m2d;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// <c>M2Attackable.applyHpDamage</c> 是 PR 和 NelEnemy 共用的低层 HP 伤害入口——两者都没有覆写
    /// 这个 3 参数重载（NelEnemy 自己的 4 参数版本内部转发到这一个），所以只需要在这一处打补丁
    /// 就能覆盖双方。<c>__result</c> 就是实际扣掉的血量。
    /// </summary>
    [HarmonyPatch(typeof(M2Attackable), nameof(M2Attackable.applyHpDamage), new[] { typeof(int), typeof(bool), typeof(AttackInfo) })]
    [PolarisPatchFeature("HpDamageApplied")]
    internal static class Patch_M2Attackable_applyHpDamage_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(M2Attackable __instance, int __result)
        {
            if (__result == 0)
            {
                return;
            }

            GameCallbackPublishers.HpDamage(__instance, __result, __instance.hp);
        }
    }

    /// <summary>镜像 HP 版本：PR 和 NelEnemy 的 <c>applyMpDamage</c> 覆写链最终都调用
    /// <c>base.applyMpDamage</c>，只需要打这一处。</summary>
    [HarmonyPatch(typeof(M2Attackable), nameof(M2Attackable.applyMpDamage), new[] { typeof(int), typeof(bool), typeof(AttackInfo) })]
    [PolarisPatchFeature("MpDamageApplied")]
    internal static class Patch_M2Attackable_applyMpDamage_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(M2Attackable __instance, int __result)
        {
            if (__result == 0)
            {
                return;
            }

            GameCallbackPublishers.MpDamage(__instance, __result, __instance.mp);
        }
    }

    /// <summary>
    /// <c>M2Attackable.cureHp(int)</c> 是唯一真正改 <c>hp</c> 字段的地方；PR/NelEnemy 各自的
    /// <c>cureHp</c> 覆写都经过若干层最终调用 <c>base.cureHp(val)</c>，打这一处即可覆盖两边。
    /// </summary>
    [HarmonyPatch(typeof(M2Attackable), nameof(M2Attackable.cureHp), new[] { typeof(int) })]
    [PolarisPatchFeature("RecoveryApplied")]
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
                GameCallbackPublishers.Recovery(__instance, delta, 0);
            }
        }
    }

    /// <summary>镜像 <see cref="Patch_M2Attackable_cureHp_Callbacks"/>，MP 版本。</summary>
    [HarmonyPatch(typeof(M2Attackable), nameof(M2Attackable.cureMp), new[] { typeof(int) })]
    [PolarisPatchFeature("RecoveryApplied")]
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
                GameCallbackPublishers.Recovery(__instance, 0, delta);
            }
        }
    }

    /// <summary>
    /// <c>M2Ser.Add</c> 一个方法同时处理"这个状态异常本来没有"（新增）和"已经有，刷新持续时间/层级"
    /// （刷新）两种情况，内部用 <c>Find(ser)</c> 是否为 null 分支。Prefix 提前做同一次查找，
    /// Postfix 据此决定发哪一种事件——不需要跟着它内部的分支逻辑走。
    /// </summary>
    [HarmonyPatch(typeof(M2Ser), nameof(M2Ser.Add))]
    [PolarisPatchFeature("StatusAdded")]
    [PolarisPatchFeature("StatusRefreshed")]
    internal static class Patch_M2Ser_Add_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(M2Ser __instance, SER ser, out bool __state) => __state = __instance.Find(ser) != null;

        [HarmonyPostfix]
        static void Postfix(M2Ser __instance, SER ser, bool __state)
        {
            if (__instance.Mv is not M2Attackable target)
            {
                return;
            }

            GameCallbackPublishers.Status(
                target,
                __state ? GameInstanceCallbackKind.StatusRefreshed : GameInstanceCallbackKind.StatusAdded,
                (int)ser);
        }
    }

    /// <summary><c>M2Ser.removeBit</c> 无条件清位，不管之前是不是已经清过——只在真正发生
    /// "从有到无"这次翻转时才算一次状态移除。</summary>
    [HarmonyPatch(typeof(M2Ser), nameof(M2Ser.removeBit))]
    [PolarisPatchFeature("StatusRemoved")]
    internal static class Patch_M2Ser_removeBit_Callbacks
    {
        [HarmonyPrefix]
        static void Prefix(M2Ser __instance, SER ser, out bool __state)
            => __state = (__instance.ser_bits & (ulong)(1L << (int)ser)) != 0;

        [HarmonyPostfix]
        static void Postfix(M2Ser __instance, SER ser, bool __state)
        {
            if (!__state || __instance.Mv is not M2Attackable target)
            {
                return;
            }

            GameCallbackPublishers.Status(target, GameInstanceCallbackKind.StatusRemoved, (int)ser);
        }
    }

    /// <summary>敌人侧的击退入口。</summary>
    [HarmonyPatch(typeof(NelEnemy), nameof(NelEnemy.addKnockbackVelocity),
        new[] { typeof(float), typeof(AttackInfo), typeof(M2Attackable), typeof(FOCTYPE) })]
    [PolarisPatchFeature("KnockbackApplied")]
    internal static class Patch_NelEnemy_addKnockbackVelocity_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(NelEnemy __instance, float v0) => GameCallbackPublishers.Knockback(__instance, v0);
    }

    /// <summary>玩家侧的击退入口。</summary>
    [HarmonyPatch(typeof(PR), nameof(PR.addKnockbackVelocity),
        new[] { typeof(float), typeof(AttackInfo), typeof(M2Attackable), typeof(FOCTYPE) })]
    [PolarisPatchFeature("KnockbackApplied")]
    internal static class Patch_PR_addKnockbackVelocity_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(PR __instance, float v0) => GameCallbackPublishers.Knockback(__instance, v0);
    }

    /// <summary>
    /// <c>PR.applyDamage(NelAttackInfo, ref HITTYPE, bool)</c> 是玩家一次语义攻击的顶层入口
    /// （内部转发给 <c>DMG.applyDamage</c>）。用 hp/mp 前后差算实际伤害，而不是信任 <c>__result</c>
    /// 的语义——没有核对过 <c>DMG.applyDamage</c> 内部对返回值的约定，字段差值才是地面真相。
    /// <para>
    /// 目标方法带 <c>ref</c> 参数，<c>MakeByRefType()</c> 不是特性实参允许的常量表达式，
    /// 所以用 <c>TargetMethod</c> 让 Harmony 在运行时解析目标。
    /// </para>
    /// </summary>
    [HarmonyPatch]
    [PolarisPatchFeature("DamageApplied")]
    internal static class Patch_PR_applyDamage_Callbacks
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(typeof(PR), nameof(PR.applyDamage),
                new[] { typeof(NelAttackInfo), typeof(HITTYPE).MakeByRefType(), typeof(bool) });

        [HarmonyPrefix]
        static void Prefix(PR __instance, out int[] __state) => __state = new[] { __instance.hp, __instance.mp };

        [HarmonyPostfix]
        static void Postfix(PR __instance, int[] __state)
        {
            int hp = __state[0] - __instance.hp;
            int mp = __state[1] - __instance.mp;
            if (hp != 0 || mp != 0)
            {
                GameCallbackPublishers.DamageApplied(__instance, hp, mp);
            }
        }
    }

    /// <summary>敌人一侧的顶层伤害入口，镜像 <see cref="Patch_PR_applyDamage_Callbacks"/> 的做法。</summary>
    [HarmonyPatch]
    [PolarisPatchFeature("DamageApplied")]
    internal static class Patch_NelEnemy_applyDamage_Callbacks
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(typeof(NelEnemy), nameof(NelEnemy.applyDamage),
                new[] { typeof(NelAttackInfo), typeof(HITTYPE).MakeByRefType(), typeof(bool) });

        [HarmonyPrefix]
        static void Prefix(NelEnemy __instance, out int[] __state) => __state = new[] { __instance.hp, __instance.mp };

        [HarmonyPostfix]
        static void Postfix(NelEnemy __instance, int[] __state)
        {
            int hp = __state[0] - __instance.hp;
            int mp = __state[1] - __instance.mp;
            if (hp != 0 || mp != 0)
            {
                GameCallbackPublishers.DamageApplied(__instance, hp, mp);
            }
        }
    }
}
