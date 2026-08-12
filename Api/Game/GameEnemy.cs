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
        /// <summary>
        /// 游戏用 <c>ENEMYID</c> 的最高位标记"处于狂暴形态"，它不是一个独立的敌人种类。
        /// 读种类编号时要先剥掉，否则每个调用方都得自己记得做这件事。
        /// </summary>
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
        {
            get
            {
                NelEnemy e = Enemy;
                if (e == null)
                {
                    return default;
                }

                try
                {
                    return (GameEnemyId)(long)((long)e.id & ~OverdriveFlag);
                }
                catch (Exception)
                {
                    return default;
                }
            }
        }

        /// <summary>获取该敌人当前状态。</summary>
        public GameEnemyState State => ReadState(Enemy);

        /// <summary>
        /// 切换该敌人到目标状态。与 <see cref="GamePlayer.ChangeState"/> 一样是高权限动作，
        /// 会绕过原本通往该状态的迁移条件。
        /// </summary>
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
        /// 根据请求参数对该敌人造成一次伤害，返回<b>实际</b>扣掉的体力值。
        /// <para>
        /// 魔力伤害同样会结算，但不体现在返回值里——一次调用只能返回一个数，
        /// 而"这一下打掉多少血"是绝大多数调用方真正要判断的量（例如"打死了没有"）。
        /// 需要魔力那一份请订阅
        /// <see cref="GameInstanceCallbackKind.MpDamageApplied"/>。
        /// </para>
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

        /// <summary>
        /// 给该敌人追加击退速度，走的是游戏自己的击退通道
        /// （因此会照常受该敌人的抗击退判定影响，而不是硬改速度）。
        /// </summary>
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
                // 朝向决定推的方向：游戏按"攻击来自哪一侧"算，来自右侧就往左推。
                e.is_right = request.FromRight;
                e.addKnockbackVelocity(velocity, null, null, default);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameEnemy.AddKnockback");
            }
        }

        static GameEnemyState ReadState(NelEnemy e)
        {
            if (e == null)
            {
                return GameEnemyState.Stand;
            }

            try
            {
                return (GameEnemyState)(int)e.state;
            }
            catch (Exception)
            {
                return GameEnemyState.Stand;
            }
        }

        static bool ReadAlive(NelEnemy e)
        {
            if (e == null)
            {
                return false;
            }

            try
            {
                return e.is_alive;
            }
            catch (Exception)
            {
                return false;
            }
        }

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
