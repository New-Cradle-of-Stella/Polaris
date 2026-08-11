using System;
using m2d;

namespace Polaris.API
{
    /// <summary>
    /// 场上角色的查询与位移。玩家专属的东西（姿势、咏唱、状态机）在 <see cref="PlayerGameAPI"/>。
    /// <para>
    /// 所有方法收发 <see cref="CharacterHandle"/>，不收发游戏对象。每次调用都会先验证句柄
    /// ——地图切换或对象池复用之后，旧句柄一律解析失败，而不是安静地作用到新住客身上。
    /// </para>
    /// </summary>
    public sealed class CharacterGameAPI
    {
        /// <summary>句柄现在还指向一个在场的角色吗。</summary>
        public bool IsAlive(CharacterHandle handle)
        {
            M2Attackable Target = CharacterRegistry.Resolve(handle);
            if (Target == null)
            {
                return false;
            }

            try
            {
                return Target.is_alive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>读一份快照。句柄失效时返回 <c>null</c>。</summary>
        public CharacterSnapshot Snapshot(CharacterHandle handle)
        {
            M2Attackable Target = CharacterRegistry.Resolve(handle);
            return Target == null ? null : Capture(Target, handle);
        }

        /// <summary>把角色瞬移到地图坐标。这是硬设位置，不做寻路也不做碰撞回退。</summary>
        public GameActionResult Teleport(CharacterHandle handle, GameVector2 position)
        {
            M2Attackable Target = CharacterRegistry.Resolve(handle);
            if (Target == null)
            {
                return Expired();
            }

            try
            {
                Target.setTo(position.X, position.Y);
                return GameActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Character.Teleport");
                return GameActionResult.Fail(GameActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>相对当前位置瞬移一段。与 <see cref="Teleport"/> 同样是硬设位置。</summary>
        public GameActionResult MoveBy(CharacterHandle handle, GameVector2 delta)
        {
            M2Attackable Target = CharacterRegistry.Resolve(handle);
            if (Target == null)
            {
                return Expired();
            }

            try
            {
                Target.setTo(Target.x + delta.X, Target.y + delta.Y);
                return GameActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Character.MoveBy");
                return GameActionResult.Fail(GameActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>
        /// 直接改写速度。会覆盖角色这一帧自己算出来的速度，适合击退、弹射一类的效果；
        /// 想让角色"走过去"请用位移插值（那属于上层的补间，不是本层的能力）。
        /// </summary>
        public GameActionResult SetVelocity(CharacterHandle handle, GameVector2 velocity)
        {
            M2Attackable Target = CharacterRegistry.Resolve(handle);
            if (Target == null)
            {
                return Expired();
            }

            try
            {
                Target.setVelocityForce(velocity.X, velocity.Y);
                return GameActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Character.SetVelocity");
                return GameActionResult.Fail(GameActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>把一个游戏角色对象登记成句柄。本层内部与其它门面共用。</summary>
        internal static CharacterHandle HandleOf(M2Attackable Target) => CharacterRegistry.Handle(Target);

        /// <summary>快照的唯一构造处，玩家快照也复用这里的读法，避免两处各读一遍读出不同结果。</summary>
        internal static CharacterSnapshot Capture(M2Attackable Target, CharacterHandle handle)
        {
            var Snap = new CharacterSnapshot { Handle = handle };

            try
            {
                Snap.Position = new GameVector2(Target.x, Target.y);
                Snap.Velocity = new GameVector2(Target.vx, Target.vy);
                Snap.Hp = Target.get_hp();
                Snap.MaxHp = Target.get_maxhp();
                Snap.Mp = Target.get_mp();
                Snap.MaxMp = Target.get_maxmp();
                Snap.IsAlive = Target.is_alive;
            }
            catch (Exception ex)
            {
                // 快照是只读操作，读到一半失败没有副作用，返回已经填好的部分比抛给调用方有用。
                PolarisAPI.Errors.Report(ex, "Character.Snapshot");
            }

            return Snap;
        }

        internal static GameActionResult Expired()
            => GameActionResult.Fail(GameActionStatus.TargetUnavailable, "角色句柄已失效（地图已切换或目标已离场）。");
    }
}
