namespace Polaris.API
{
    /// <summary>
    /// 对敌人造成一次伤害的请求。
    /// <para>
    /// 体力与魔力分成两个字段而不是"一个带正负号的数"：它们在游戏里走不同的处理链
    /// （体力伤害有护盾、抗性、击退与硬直，魔力伤害没有），混成一个字段会让调用方
    /// 写出在其中一条链上不成立的假设。
    /// </para>
    /// </summary>
    public readonly struct EnemyDamageRequest
    {
        public EnemyDamageRequest(int hpDamage, int mpDamage = 0, bool force = false)
        {
            HpDamage = hpDamage;
            MpDamage = mpDamage;
            Force = force;
        }

        public int HpDamage { get; }

        public int MpDamage { get; }

        /// <summary>无视无敌帧与减伤判定强制生效。调试与事件脚本用，普通内容不要开。</summary>
        public bool Force { get; }
    }

    /// <summary>
    /// 追加击退速度的请求。<see cref="Velocity"/> 是初速度，方向由
    /// <see cref="FromRight"/> 决定——直接给一个带符号的速度也能表达方向，
    /// 但那样"速度大小"和"往哪边打"就绑死在一个数上，写内容时很容易漏掉负号。
    /// </summary>
    public readonly struct KnockbackRequest
    {
        public KnockbackRequest(float velocity, bool fromRight = false)
        {
            Velocity = velocity;
            FromRight = fromRight;
        }

        /// <summary>击退初速度，非负。</summary>
        public float Velocity { get; }

        /// <summary>攻击来自右侧（因此目标被往左推）。</summary>
        public bool FromRight { get; }
    }

    /// <summary>
    /// 更新任务阶段时的可选行为。全部留默认值就是"正常推进一个阶段并照常提示玩家"。
    /// </summary>
    public readonly struct QuestUpdateOptions
    {
        public QuestUpdateOptions(bool hidden = false, bool setFocus = false, bool fillTargetItem = false)
        {
            Hidden = hidden;
            SetFocus = setFocus;
            FillTargetItem = fillTargetItem;
        }

        /// <summary>不弹出任务变化提示。批量修正存档状态时用。</summary>
        public bool Hidden { get; }

        /// <summary>顺便把这个任务设成当前重点追踪任务。</summary>
        public bool SetFocus { get; }

        /// <summary>按新阶段的收集目标，立即用玩家已有的物品把进度补满。</summary>
        public bool FillTargetItem { get; }
    }

    /// <summary>任务的当前追踪进度。</summary>
    public sealed class GameQuestProgress
    {
        internal GameQuestProgress(string key, int phase, bool finished)
        {
            Key = key;
            Phase = phase;
            Finished = finished;
        }

        /// <summary>任务的稳定键。</summary>
        public string Key { get; }

        /// <summary>当前阶段编号。</summary>
        public int Phase { get; }

        /// <summary>是否已经完成。</summary>
        public bool Finished { get; }

        public override string ToString() => $"{Key} phase={Phase}{(Finished ? " (finished)" : string.Empty)}";
    }

    /// <summary>
    /// 任务列表表头的摘要视图。和 <see cref="GameQuestProgress"/> 分成两个类型，
    /// 是因为它回答的是"玩家现在正盯着哪个任务"，而不是某个具体任务的进度——
    /// 用同一个类型会让"取表头"看起来像"取某个任务的进度"。
    /// </summary>
    public sealed class GameQuestProgressView
    {
        internal GameQuestProgressView(GameQuest quest, int phase, bool finished)
        {
            Quest = quest;
            Phase = phase;
            Finished = finished;
        }

        /// <summary>对应的任务实例；表头为空时整个视图就是 <c>null</c>，这里不会是 <c>null</c>。</summary>
        public GameQuest Quest { get; }

        public int Phase { get; }

        public bool Finished { get; }

        public override string ToString() => $"head={Quest?.Key} phase={Phase}";
    }

    /// <summary>当前前台背景音乐的曲目信息。</summary>
    public sealed class GameBgmTrack
    {
        internal GameBgmTrack(string timing, string cue)
        {
            Timing = timing;
            Cue = cue;
        }

        /// <summary>曲目所属的音频 sheet（游戏称之为 timing）。</summary>
        public string Timing { get; }

        /// <summary>曲目的 cue 名。</summary>
        public string Cue { get; }

        public override string ToString() => $"{Timing}/{Cue}";
    }

    /// <summary>地图上的一份掉落物。</summary>
    public sealed class GameDrop
    {
        internal GameDrop(GameItem item, int count, int grade, GameVector2 position)
        {
            Item = item;
            Count = count;
            Grade = grade;
            Position = position;
        }

        public GameItem Item { get; }

        public int Count { get; }

        public int Grade { get; }

        /// <summary>掉落时的地图坐标。</summary>
        public GameVector2 Position { get; }

        public override string ToString() => $"{Item?.Key} x{Count} @{Position}";
    }
}
