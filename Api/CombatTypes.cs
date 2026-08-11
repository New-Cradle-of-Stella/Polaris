namespace Polaris.API
{
    /// <summary>
    /// 一次恢复请求。HP 与 MP 分开填，只想回一样就把另一样留 0。
    /// <para>
    /// 恢复和"负数伤害"<b>不是</b>同一件事，本层也不接受用负值互相冒充：游戏里两者走的是
    /// 不同的处理链（伤害有护盾、抗性、击退与硬直，恢复有溢出与蓄能槽），事件也不一样。
    /// </para>
    /// </summary>
    public readonly struct RecoveryRequest
    {
        public int Hp { get; }

        public int Mp { get; }

        public RecoveryRequest(int hp, int mp = 0)
        {
            Hp = hp;
            Mp = mp;
        }
    }

    /// <summary>恢复结果。实际回了多少要看上限与当时的状态，不一定等于请求值。</summary>
    public readonly struct RecoveryResult
    {
        public GameActionResult Outcome { get; }

        /// <summary>实际回复的 HP。</summary>
        public float HpRestored { get; }

        /// <summary>实际回复的 MP。</summary>
        public float MpRestored { get; }

        internal RecoveryResult(GameActionResult outcome, float hpRestored, float mpRestored)
        {
            Outcome = outcome;
            HpRestored = hpRestored;
            MpRestored = mpRestored;
        }

        public bool Succeeded => Outcome.Succeeded;

        public override string ToString() => $"{Outcome} hp+{HpRestored:0} mp+{MpRestored:0}";
    }

    /// <summary>
    /// 一次伤害请求。
    /// <para>
    /// 目前只带最基础的两项数值。属性、状态异常、击退、阵营与责任 Addon 这些字段要等
    /// <c>AttackInfo</c> 的构造路径核对清楚之后再加——现在就把字段列出来但内部忽略，
    /// 比暂时不提供更糟：调用方会以为自己设置的属性生效了。
    /// </para>
    /// </summary>
    public readonly struct DamageRequest
    {
        public int HpDamage { get; }

        public int MpDamage { get; }

        /// <summary>无视无敌帧与减伤判定强制生效。调试与事件脚本用，普通内容不要开。</summary>
        public bool Force { get; }

        public DamageRequest(int hpDamage, int mpDamage = 0, bool force = false)
        {
            HpDamage = hpDamage;
            MpDamage = mpDamage;
            Force = force;
        }
    }

    /// <summary>伤害结果。实际扣了多少由游戏决定（抗性、护盾、无敌帧）。</summary>
    public readonly struct DamageResult
    {
        public GameActionResult Outcome { get; }

        public int HpDealt { get; }

        public int MpDealt { get; }

        internal DamageResult(GameActionResult outcome, int hpDealt, int mpDealt)
        {
            Outcome = outcome;
            HpDealt = hpDealt;
            MpDealt = mpDealt;
        }

        public bool Succeeded => Outcome.Succeeded;

        public override string ToString() => $"{Outcome} hp-{HpDealt} mp-{MpDealt}";
    }
}
