namespace Polaris.API
{
    /// <summary>低层伤害的大致来源分类。<c>Unknown</c> 覆盖尚未单独拆出锚点的伤害路径
    /// （地图机关、持续伤害、气体、挤压、吸收——见实施计划 7.5，这些仍是 Unsupported）。</summary>
    public enum GameDamageKind
    {
        Unknown = 0,
        Normal = 1,
    }

    /// <summary>
    /// 一次语义攻击的最终结算（<c>PR.applyDamage</c>/<c>NelEnemy.applyDamage</c> 顶层返回时）。
    /// 同一次攻击往下调用 <c>M2Attackable.applyHpDamage</c>/<c>applyMpDamage</c> 产生的
    /// <see cref="HpDamageAppliedEvent"/>/<see cref="MpDamageAppliedEvent"/> 与这条事件共享同一个
    /// <see cref="OperationId"/>。
    /// </summary>
    public sealed class DamageAppliedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public long OperationId { get; }
        public CharacterHandle Target { get; }
        public int ActualHpDamage { get; }
        public GameDamageKind Kind { get; }
        public bool WasLethal { get; }

        internal DamageAppliedEvent(GameCallbackStamp stamp, long operationId, CharacterHandle target,
            int actualHpDamage, GameDamageKind kind, bool wasLethal)
        {
            Stamp = stamp;
            OperationId = operationId;
            Target = target;
            ActualHpDamage = actualHpDamage;
            Kind = kind;
            WasLethal = wasLethal;
        }
    }

    /// <summary>低层 HP 伤害（<c>M2Attackable.applyHpDamage</c>），可以在一次攻击里多次出现。</summary>
    public sealed class HpDamageAppliedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public long OperationId { get; }
        public CharacterHandle Target { get; }
        public int Amount { get; }
        public int HpAfter { get; }

        internal HpDamageAppliedEvent(GameCallbackStamp stamp, long operationId, CharacterHandle target, int amount, int hpAfter)
        {
            Stamp = stamp;
            OperationId = operationId;
            Target = target;
            Amount = amount;
            HpAfter = hpAfter;
        }
    }

    /// <summary>低层 MP 伤害（<c>M2Attackable.applyMpDamage</c>）。</summary>
    public sealed class MpDamageAppliedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public long OperationId { get; }
        public CharacterHandle Target { get; }
        public int Amount { get; }
        public int MpAfter { get; }

        internal MpDamageAppliedEvent(GameCallbackStamp stamp, long operationId, CharacterHandle target, int amount, int mpAfter)
        {
            Stamp = stamp;
            OperationId = operationId;
            Target = target;
            Amount = amount;
            MpAfter = mpAfter;
        }
    }

    /// <summary>实际 HP/MP 恢复（<c>M2Attackable.cureHp</c>/<c>cureMp</c>），两者中至少一个非零。</summary>
    public sealed class RecoveryAppliedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public CharacterHandle Target { get; }
        public int HpDelta { get; }
        public int MpDelta { get; }

        internal RecoveryAppliedEvent(GameCallbackStamp stamp, CharacterHandle target, int hpDelta, int mpDelta)
        {
            Stamp = stamp;
            Target = target;
            HpDelta = hpDelta;
            MpDelta = mpDelta;
        }
    }

    /// <summary>角色受到击退。</summary>
    public sealed class KnockbackAppliedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public CharacterHandle Target { get; }

        internal KnockbackAppliedEvent(GameCallbackStamp stamp, CharacterHandle target)
        {
            Stamp = stamp;
            Target = target;
        }
    }

    /// <summary>状态异常新增/刷新/移除，<c>Ser</c> 与游戏内部 <c>SER</c> 枚举同序，用整数传递避免暴露游戏类型。</summary>
    public sealed class StatusChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public CharacterHandle Target { get; }
        public int SerId { get; }

        internal StatusChangedEvent(GameCallbackStamp stamp, CharacterHandle target, int serId)
        {
            Stamp = stamp;
            Target = target;
            SerId = serId;
        }
    }

    public sealed class PlayerDeathStartingEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal PlayerDeathStartingEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }

    public sealed class PlayerDiedEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal PlayerDiedEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }

    /// <summary>玩家从死亡/倒地恢复（<c>PR.cureHp</c> 探测到 <c>is_alive</c> 从 false 变 true）。</summary>
    public sealed class PlayerRevivedEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal PlayerRevivedEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }
}
