using System;
using nel;

namespace Polaris.API
{
    /// <summary>
    /// 场上的一个敌人。位置、体力、伤害这些共通能力继承自 <see cref="GameCharacter"/>，
    /// 这里只放敌人独有的种类编号、状态机与击退。
    /// </summary>
    public sealed class GameEnemy : GameCharacter
    {
        /// <summary><c>ENEMYID</c> 最高位标记"狂暴形态"，非独立敌人种类，读编号时需先剥掉。</summary>
        const long OverdriveFlag = 2147483648L;

        static readonly InstanceTable<NelEnemy, GameEnemy> Table = new();

        GameEnemyState lastState;
        bool lastAlive = true;

        GameEnemy(NelEnemy target) : base(target)
        {
            lastState = ReadState(target);
            lastAlive = ReadAlive(target);
        }

        internal static GameEnemy Wrap(NelEnemy native) => Table.Get(native, static n => new GameEnemy(n));

        internal static void InvalidateAllEnemies() => Table.InvalidateAll();

        internal static void SweepEnemies() => Table.Sweep();

        /// <summary>遍历当前被人持有的敌人包装器。没人取过的敌人不产生任何轮询开销。</summary>
        internal static void EachLive(Action<GameEnemy> visit) => Table.Each(visit);

        NelEnemy Enemy => Native as NelEnemy;

        private protected override string Describe() => $"GameEnemy({EnemyId})";

        /// <summary>获取该敌人的种类编号。狂暴形态不体现在这里，看 <see cref="State"/>。</summary>
        public GameEnemyId EnemyId
            => Read(Enemy, static e => (GameEnemyId)(long)((long)e.id & ~OverdriveFlag), default);

        /// <summary>获取该敌人当前状态。</summary>
        public GameEnemyState State => ReadState(Enemy);

        /// <summary>切换该敌人到目标状态；绕过正常迁移条件，与 <see cref="GamePlayer.ChangeState"/> 一样是高权限动作。</summary>
        public void ChangeState(GameEnemyState state)
        {
            EnsureUsable();

            NelEnemy e = Enemy;
            if (e == null)
            {
                return;
            }

            try
            {
                e.changeState((NelEnemy.STATE)(int)state);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameEnemy.ChangeState");
            }
        }

        /// <summary>
        /// 对该敌人造成一次伤害，返回实际扣掉的体力值；魔力伤害同样结算但不在返回值中，
        /// 需要请订阅 <see cref="GameInstanceCallbackKind.MpDamageApplied"/>。
        /// </summary>
        public int ApplyDamage(EnemyDamageRequest request)
        {
            EnsureUsable();

            int hp = request.HpDamage > 0 ? DamageHp(request.HpDamage, request.Force) : 0;
            if (request.MpDamage > 0)
            {
                DamageMp(request.MpDamage, request.Force);
            }

            return hp;
        }

        /// <summary>给该敌人追加击退速度，走游戏自身击退通道，因此仍受其抗击退判定影响。</summary>
        public void AddKnockback(KnockbackRequest request)
        {
            EnsureUsable();

            NelEnemy e = Enemy;
            if (e == null)
            {
                return;
            }

            float velocity = Math.Abs(request.Velocity);
            if (velocity <= 0f)
            {
                return;
            }

            try
            {
                // 朝向决定推的方向：来自右侧就往左推。
                e.is_right = request.FromRight;
                e.addKnockbackVelocity(velocity, null, null, default);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameEnemy.AddKnockback");
            }
        }

        /// <summary>也供构造函数调用，那时 <see cref="GameInstance.IsValid"/> 还没法用，故直接收原生对象。</summary>
        static TValue Read<TValue>(NelEnemy e, Func<NelEnemy, TValue> read, TValue fallback)
        {
            if (e == null)
            {
                return fallback;
            }

            try
            {
                return read(e);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        static GameEnemyState ReadState(NelEnemy e)
            => Read(e, static x => (GameEnemyState)(int)x.state, GameEnemyState.Stand);

        static bool ReadAlive(NelEnemy e) => Read(e, static x => x.is_alive, false);

        /// <summary>每帧差分：状态变化与死亡两条实例回调。理由同 <see cref="GamePlayer.PumpState"/>。</summary>
        internal void PumpState()
        {
            NelEnemy e = Enemy;
            if (e == null)
            {
                return;
            }

            GameEnemyState current = ReadState(e);
            if (current != lastState)
            {
                GameEnemyState previous = lastState;
                lastState = current;
                GameCallbackHub.PublishInstance(
                    GameInstanceCallbackKind.EnemyStateChanged,
                    this,
                    () => new EnemyStateChangedCallbackData(this, previous, current));
            }

            bool alive = ReadAlive(e);
            if (alive == lastAlive)
            {
                return;
            }

            lastAlive = alive;
            if (!alive)
            {
                GameCallbackHub.PublishInstance(
                    GameInstanceCallbackKind.EnemyDied, this, () => new EnemyDiedCallbackData(this));
            }
        }
    }
}
