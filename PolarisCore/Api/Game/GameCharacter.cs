using System;
using m2d;

namespace Polaris.API
{
    /// <summary>
    /// 场上角色（玩家/敌人/NPC 共通部分）：位置、速度、朝向、体力与魔力，以及位移、治疗和伤害。
    /// 玩家、敌人的独有状态机分别在 <see cref="GamePlayer"/>、<see cref="GameEnemy"/>。
    /// 角色对象是对象池复用的，不要缓存原生对象；包装器会随地图切换整批失效。
    /// </summary>
    public class GameCharacter : GameInstance
    {
        static readonly InstanceTable<M2Attackable, GameCharacter> Table = new();

        readonly M2Attackable target;

        private protected GameCharacter(M2Attackable target)
        {
            this.target = target;
        }

        /// <summary>取包装器的唯一入口。</summary>
        internal static GameCharacter Wrap(M2Attackable native)
        {
            if (native == null)
            {
                return null;
            }

            // 玩家/敌人优先返回更具体的子类包装器，省得下游再做一次类型判断。
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
                // 须用 Unity 的相等语义：对象被销毁后 == null 为真，但引用本身不为 null。
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

        // ── 只读查询：失效时给零值 ────────────────────────

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

        // ── 动作：失效时不生效 ──────────────────────────

        /// <summary>把该角色直接移动到目标坐标。硬设位置，不做寻路，也不做碰撞回退。</summary>
        public void Teleport(GameVector2 position)
        {
            EnsureUsable();
            Act("Teleport", t => t.setTo(position.X, position.Y));
        }

        /// <summary>按坐标偏移移动该角色；<paramref name="checkFoot"/> 为真走带碰撞的位移，为假直接硬设位置。</summary>
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

        /// <summary>设置该角色的移动速度，覆盖本帧自算速度，适合击退、弹射等效果。</summary>
        public void SetVelocity(GameVector2 velocity)
        {
            EnsureUsable();
            Act("SetVelocity", t => t.setVelocityForce(velocity.X, velocity.Y));
        }

        /// <summary>设置该角色的朝向；<paramref name="forceSprite"/> 为真时图像立即跟着翻转，否则走原本的过渡动画。</summary>
        public void SetFacing(GameFacing facing, bool forceSprite = false)
        {
            EnsureUsable();

            // 走游戏的 setAim 而非直接写 is_right，避免图像朝向与逻辑朝向脱节。
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

        /// <summary>对该角色造成体力值伤害，返回实际扣掉的数值（抗性/护盾/无敌帧会裁剪）；<paramref name="force"/> 无视这些判定。</summary>
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

        // ── 内部工具 ──────────────────

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
