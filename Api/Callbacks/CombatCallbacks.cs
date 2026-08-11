using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>战斗、伤害和恢复回调。第一批覆盖 HP/MP 伤害与恢复的通用低层入口，
    /// 环境/持续/气体/挤压/吸收等细分伤害种类与魔法系统留待后续版本。</summary>
    public sealed class CombatCallbacks
    {
        internal CombatCallbacks() { }

        static readonly GameSignal<DamageAppliedEvent> damageAppliedSignal = new(GameCallbackKind.DamageApplied);
        static readonly GameSignal<HpDamageAppliedEvent> hpDamageAppliedSignal = new(GameCallbackKind.HpDamageApplied);
        static readonly GameSignal<MpDamageAppliedEvent> mpDamageAppliedSignal = new(GameCallbackKind.MpDamageApplied);
        static readonly GameSignal<RecoveryAppliedEvent> recoveryAppliedSignal = new(GameCallbackKind.RecoveryApplied);
        static readonly GameSignal<KnockbackAppliedEvent> knockbackAppliedSignal = new(GameCallbackKind.KnockbackApplied);

        public GameSignal<DamageAppliedEvent> DamageApplied => damageAppliedSignal;
        public GameSignal<HpDamageAppliedEvent> HpDamageApplied => hpDamageAppliedSignal;
        public GameSignal<MpDamageAppliedEvent> MpDamageApplied => mpDamageAppliedSignal;
        public GameSignal<RecoveryAppliedEvent> RecoveryApplied => recoveryAppliedSignal;
        public GameSignal<KnockbackAppliedEvent> KnockbackApplied => knockbackAppliedSignal;

        internal static bool WantsHpMpDetail => hpDamageAppliedSignal.HasSubscribers || mpDamageAppliedSignal.HasSubscribers || damageAppliedSignal.HasSubscribers;

        internal static void PublishDamageApplied(long operationId, CharacterHandle target, int actualHpDamage, bool wasLethal)
        {
            if (!damageAppliedSignal.HasSubscribers) { return; }
            damageAppliedSignal.Publish(new DamageAppliedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact),
                operationId, target, actualHpDamage, GameDamageKind.Normal, wasLethal));
        }

        internal static void PublishHpDamageApplied(long operationId, CharacterHandle target, int amount, int hpAfter)
        {
            if (!hpDamageAppliedSignal.HasSubscribers) { return; }
            hpDamageAppliedSignal.Publish(new HpDamageAppliedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), operationId, target, amount, hpAfter));
        }

        internal static void PublishMpDamageApplied(long operationId, CharacterHandle target, int amount, int mpAfter)
        {
            if (!mpDamageAppliedSignal.HasSubscribers) { return; }
            mpDamageAppliedSignal.Publish(new MpDamageAppliedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), operationId, target, amount, mpAfter));
        }

        internal static void PublishRecoveryApplied(CharacterHandle target, int hpDelta, int mpDelta)
        {
            if (!recoveryAppliedSignal.HasSubscribers) { return; }
            recoveryAppliedSignal.Publish(new RecoveryAppliedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), target, hpDelta, mpDelta));
        }

        internal static void PublishKnockbackApplied(CharacterHandle target)
        {
            if (!knockbackAppliedSignal.HasSubscribers) { return; }
            knockbackAppliedSignal.Publish(new KnockbackAppliedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), target));
        }
    }
}
