using System;
using m2d;

namespace Polaris.API
{
    /// <summary>
    /// 场上的一个角色（玩家、敌人、NPC 共通的那一部分）：位置、速度、朝向、体力与魔力，
    /// 以及位移、治疗和伤害。玩家独有的状态机在 <see cref="GamePlayer"/>，
    /// 敌人独有的在 <see cref="GameEnemy"/>。
    /// <para>
    /// 取得实例的入口是 <see cref="GameMap.FindCharacter"/> 与
    /// <see cref="PolarisAPI.Game.World"/> 下的玩家属性；不要缓存游戏对象本身——
    /// 游戏的角色是对象池复用的，切图之后同一个池对象会变成另一个角色。
    /// 包装器替你处理了这件事：地图一换，上一张图发出去的包装器整体失效。
    /// </para>
    /// </summary>
    public class GameCharacter : GameInstance
    {
        static readonly InstanceTable<M2Attackable, GameCharacter> Table = new();

        readonly M2Attackable target;

        private protected GameCharacter(M2Attackable target)
        {
            this.target = target;
        }

        /// <summary>本层内部取包装器的唯一入口。玩家与敌人有各自的子类表，这里只管通用角色。</summary>
        internal static GameCharacter Wrap(M2Attackable native)
        {
            if (native == null)
            {
                return null;
            }

            // 玩家和敌人有更具体的包装器，优先给出具体类型：下游拿到 GameCharacter 之后
            // 还要再 as 一次才能用状态机，那等于把这层判断推给了每一个调用方。
            if (native is nel.PR pr)
            {
                return GamePlayer.Wrap(pr);
            }

            if (native is nel.NelEnemy enemy)
            {
                return GameEnemy.Wrap(enemy);
            }

            return Table.Get(native, static n => new GameCharacter(n));
        }

        internal static void InvalidateAll() => Table.InvalidateAll();

        internal static void SweepTable() => Table.Sweep();

        /// <summary>子类访问底层对象的唯一通道；已失效时为 <c>null</c>。</summary>
        private protected M2Attackable Native => IsValid ? target : null;

        private protected override bool IsNativeAlive
        {
            get
            {
                // Unity 对象被销毁之后 == null 为真而引用本身不为 null，这里必须用 Unity 的相等语义。
                if (target == null)
                {
                    return false;
                }

                return GameRuntime.IsCurrentGeneration(this);
            }
        }

        private protected override string Describe() => $"GameCharacter({target?.GetType().Name})";

        /// <summary>这个角色是在哪一代地图上取到的。地图一换，整代包装器作废。</summary>
        internal int MapGeneration { get; } = GameBinding.MapGeneration;

        // ── 只读查询：任何时刻都能安全地问，失效时给零值 ────────────────────────

        /// <summary>该角色的横向坐标。</summary>
        public float X => Read(static t => t.x, 0f);

        /// <summary>该角色的纵向坐标。</summary>
        public float Y => Read(static t => t.y, 0f);

        /// <summary>该角色的横向速度。</summary>
        public float VelocityX => Read(static t => t.vx, 0f);

        /// <summary>该角色的纵向速度。</summary>
        public float VelocityY => Read(static t => t.vy, 0f);

        /// <summary>该角色碰撞矩形的宽度。</summary>
        public float Width => Read(static t => t.getSpWidth(), 0f);

        /// <summary>该角色碰撞矩形的高度。</summary>
        public float Height => Read(static t => t.getSpHeight(), 0f);

        /// <summary>该角色当前朝向。</summary>
        public GameFacing Facing => Read(static t => t.is_right ? GameFacing.Right : GameFacing.Left, GameFacing.Right);

        /// <summary>该角色当前体力值。</summary>
        public int Hp => (int)Read(static t => t.get_hp(), 0f);

        /// <summary>该角色体力值上限。</summary>
        public int MaxHp => (int)Read(static t => t.get_maxhp(), 0f);

        /// <summary>该角色当前魔力值。</summary>
        public int Mp => (int)Read(static t => t.get_mp(), 0f);

        /// <summary>该角色魔力值上限。</summary>
        public int MaxMp => (int)Read(static t => t.get_maxmp(), 0f);

        /// <summary>该角色当前是否存活。</summary>
        public bool IsAlive => Read(static t => t.is_alive, false);

        // ── 动作：失效时抛，不安静作用到别的对象上 ──────────────────────────────

        /// <summary>把该角色直接移动到目标坐标。硬设位置，不做寻路，也不做碰撞回退。</summary>
        public void Teleport(GameVector2 position)
        {
            EnsureUsable();
            Act("Teleport", t => t.setTo(position.X, position.Y));
        }

        /// <summary>
        /// 让该角色按坐标偏移移动。<paramref name="checkFoot"/> 为真时走游戏自己的带碰撞位移
        /// （会被墙挡住，返回是否真的走完），为假时是硬设位置。
        /// </summary>
        public bool MoveBy(GameVector2 delta, bool checkFoot = true)
        {
            EnsureUsable();

            if (!checkFoot)
            {
                Act("MoveBy", t => t.setTo(t.x + delta.X, t.y + delta.Y));
                return true;
            }

            M2Attackable t = Native;
            if (t == null)
            {
                return false;
            }

            try
            {
                return t.moveWithFoot(delta.X, delta.Y, null, null, null, false, false);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameCharacter.MoveBy");
                return false;
            }
        }

        /// <summary>
        /// 设置该角色的移动速度。会覆盖角色这一帧自己算出来的速度，适合击退、弹射一类的效果；
        /// 想让角色"走过去"请自己做位移插值，那属于上层补间，不是本层的能力。
        /// </summary>
        public void SetVelocity(GameVector2 velocity)
        {
            EnsureUsable();
            Act("SetVelocity", t => t.setVelocityForce(velocity.X, velocity.Y));
        }

        /// <summary>
        /// 设置该角色的朝向。<paramref name="forceSprite"/> 为真时连同当前显示的图像一起翻过去，
        /// 否则只改逻辑朝向、让图像按原本的过渡动画自己转。
        /// </summary>
        public void SetFacing(GameFacing facing, bool forceSprite = false)
        {
            EnsureUsable();

            // 走游戏自己的 setAim 而不是直接写 is_right：转身在游戏里带着一段图像过渡，
            // 只改布尔字段会让角色的朝向和它正在显示的图像对不上，直到下一次动画刷新才修正。
            XX.AIM aim = facing == GameFacing.Right ? XX.AIM.R : XX.AIM.L;
            Act("SetFacing", t => t.setAim(aim, forceSprite));
        }

        /// <summary>恢复该角色的体力值。实际回了多少由游戏的上限与溢出规则决定。</summary>
        public void HealHp(int amount)
        {
            EnsureUsable();
            if (amount <= 0)
            {
                return;
            }

            M2Attackable t = Native;
            if (t == null)
            {
                return;
            }

            try
            {
                float before = t.get_hp();
                t.cureHp(amount);
                PublishRecovery(this, (int)(t.get_hp() - before), 0);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameCharacter.HealHp");
            }
        }

        /// <summary>恢复该角色的魔力值。</summary>
        public void HealMp(int amount)
        {
            EnsureUsable();
            if (amount <= 0)
            {
                return;
            }

            M2Attackable t = Native;
            if (t == null)
            {
                return;
            }

            try
            {
                float before = t.get_mp();
                t.cureMp(amount);
                PublishRecovery(this, 0, (int)(t.get_mp() - before));
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameCharacter.HealMp");
            }
        }

        /// <summary>
        /// 对该角色造成体力值伤害，返回<b>实际</b>扣掉的数值——请求量不等于到账量，
        /// 抗性、护盾与无敌帧都由游戏裁剪。<paramref name="force"/> 无视这些判定。
        /// </summary>
        public int DamageHp(int amount, bool force = false)
        {
            EnsureUsable();
            if (amount <= 0)
            {
                return 0;
            }

            M2Attackable t = Native;
            if (t == null)
            {
                return 0;
            }

            try
            {
                return t.applyHpDamage(amount, force, null);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameCharacter.DamageHp");
                return 0;
            }
        }

        /// <summary>对该角色造成魔力值伤害，返回实际扣掉的数值。</summary>
        public int DamageMp(int amount, bool force = false)
        {
            EnsureUsable();
            if (amount <= 0)
            {
                return 0;
            }

            M2Attackable t = Native;
            if (t == null)
            {
                return 0;
            }

            try
            {
                return t.applyMpDamage(amount, force, null);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameCharacter.DamageMp");
                return 0;
            }
        }

        // ── 内部工具 ───────────────────────────────────────────────────────────

        /// <summary>只读访问的统一包装：失效或读取抛异常时给默认值，不把异常丢给调用方。</summary>
        private protected TValue Read<TValue>(Func<M2Attackable, TValue> read, TValue fallback)
        {
            M2Attackable t = Native;
            if (t == null)
            {
                return fallback;
            }

            try
            {
                return read(t);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        /// <summary>写操作的统一包装：调用方已经过 <see cref="GameInstance.EnsureUsable"/>。</summary>
        private protected void Act(string what, Action<M2Attackable> action)
        {
            M2Attackable t = Native;
            if (t == null)
            {
                return;
            }

            try
            {
                action(t);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, $"GameCharacter.{what}");
            }
        }

        internal static void PublishRecovery(GameCharacter character, int hp, int mp)
        {
            if (character == null || (hp == 0 && mp == 0))
            {
                return;
            }

            GameCallbackHub.PublishInstance(
                GameInstanceCallbackKind.RecoveryApplied,
                character,
                () => new RecoveryAppliedCallbackData(character, hp, mp));
        }
    }
}
