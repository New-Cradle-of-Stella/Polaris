using System.Reflection;
using HarmonyLib;
using m2d;
using nel;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>PR 和 NelEnemy 共用的底层 HP 伤害入口，打一处即可覆盖双方；<c>__result</c> 是实际扣血量。</summary>
    [HarmonyPatch(typeof(M2Attackable), nameof(M2Attackable.applyHpDamage), new[] { typeof(int), typeof(bool), typeof(AttackInfo) })]
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

    /// <summary>镜像 HP 版本，MP 伤害入口。</summary>
    [HarmonyPatch(typeof(M2Attackable), nameof(M2Attackable.applyMpDamage), new[] { typeof(int), typeof(bool), typeof(AttackInfo) })]
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

    /// <summary>唯一真正改 <c>hp</c> 字段的地方，打这一处即可覆盖 PR 和 NelEnemy 两边。</summary>
    [HarmonyPatch(typeof(M2Attackable), nameof(M2Attackable.cureHp), new[] { typeof(int) })]
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

    /// <summary><c>M2Ser.Add</c> 同时处理新增和刷新状态异常两种情况；Prefix 先查找是否已存在，Postfix 据此判断发哪种事件。</summary>
    [HarmonyPatch(typeof(M2Ser), nameof(M2Ser.Add))]
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

    /// <summary><c>M2Ser.removeBit</c> 无条件清位，只在真正发生"从有到无"翻转时才算一次状态移除。</summary>
    [HarmonyPatch(typeof(M2Ser), nameof(M2Ser.removeBit))]
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
    internal static class Patch_NelEnemy_addKnockbackVelocity_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(NelEnemy __instance, float v0) => GameCallbackPublishers.Knockback(__instance, v0);
    }

    /// <summary>玩家侧的击退入口。</summary>
    [HarmonyPatch(typeof(PR), nameof(PR.addKnockbackVelocity),
        new[] { typeof(float), typeof(AttackInfo), typeof(M2Attackable), typeof(FOCTYPE) })]
    internal static class Patch_PR_addKnockbackVelocity_Callbacks
    {
        [HarmonyPostfix]
        static void Postfix(PR __instance, float v0) => GameCallbackPublishers.Knockback(__instance, v0);
    }

    /// <summary>玩家攻击的顶层入口；用 hp/mp 前后差算实际伤害而非信任 <c>__result</c>。目标方法带 <c>ref</c> 参数，用 <c>TargetMethod</c> 在运行时解析。</summary>
    [HarmonyPatch]
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
