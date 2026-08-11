using System;
using nel;

namespace Polaris.API
{
    /// <summary>
    /// 玩家角色。位置、血蓝一类和其它角色共通的部分在 <see cref="CharacterGameAPI"/>，
    /// 这里只放玩家独有的：姿势、状态机、咏唱。
    /// <para>
    /// 全部是只读查询加少数几个明确的动作。旧 LuaAiC 允许直接写 <c>Player.atk = 999999</c>
    /// 这类字段，问题是游戏会在装备、料理、技能任一变化时重算这些值，写进去的东西要么下一帧
    /// 就没了，要么永久留在存档里再也去不掉。数值修改将来走带 source key、可撤销的 Modifier 通道，
    /// 不会以裸字段的形式出现在这里。
    /// </para>
    /// </summary>
    public sealed class PlayerGameAPI
    {
        /// <summary>玩家现在在不在场。标题画面、读档中、切图途中都为 <c>false</c>。</summary>
        public bool IsPresent => GameBinding.Player != null;

        /// <summary>玩家的角色句柄；不在场时为 <see cref="CharacterHandle.None"/>。</summary>
        public CharacterHandle Handle => CharacterGameAPI.HandleOf(GameBinding.Player);

        /// <summary>读一份玩家快照；不在场时返回 <c>null</c>。</summary>
        public PlayerSnapshot Snapshot()
        {
            PR Pr = GameBinding.Player;
            if (Pr == null)
            {
                return null;
            }

            CharacterHandle handle = CharacterGameAPI.HandleOf(Pr);
            CharacterSnapshot Base = CharacterGameAPI.Capture(Pr, handle);

            var Snap = new PlayerSnapshot
            {
                Handle = handle,
                Position = Base.Position,
                Velocity = Base.Velocity,
                Hp = Base.Hp,
                MaxHp = Base.MaxHp,
                Mp = Base.Mp,
                MaxMp = Base.MaxMp,
                IsAlive = Base.IsAlive,
                PoseTitle = PoseTitle,
                StateName = StateName,
                IsChanting = IsChanting,
                ChantProgress = ChantProgress,
            };

            return Snap;
        }

        /// <summary>当前姿势名；读不到时为 <c>null</c>。</summary>
        public string PoseTitle
        {
            get
            {
                try
                {
                    return GameBinding.Player?.Anm?.pose_title;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 状态机当前状态的名字（<c>NORMAL</c>/<c>EVADE</c>/<c>DAMAGE</c>…）。判断"现在能不能动"
        /// 用它，比自己去看速度和姿势可靠。不在场时为 <c>null</c>。
        /// </summary>
        public string StateName
        {
            get
            {
                PR Pr = GameBinding.Player;
                if (Pr == null)
                {
                    return null;
                }

                try
                {
                    return Pr.state.ToString();
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>是不是处在可自由行动的普通状态。受伤、演出、被吞、坐板凳都为 <c>false</c>。</summary>
        public bool IsInNormalState
        {
            get
            {
                PR Pr = GameBinding.Player;
                if (Pr == null)
                {
                    return false;
                }

                try
                {
                    return Pr.state == PR.STATE.NORMAL;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>正在咏唱魔法。</summary>
        public bool IsChanting
        {
            get
            {
                try
                {
                    return GameBinding.Player?.magic_chanting ?? false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>咏唱完成度 0–1；没在咏唱时为 0。</summary>
        public float ChantProgress
        {
            get
            {
                PR Pr = GameBinding.Player;
                if (Pr == null)
                {
                    return 0f;
                }

                try
                {
                    return Pr.Skill?.getChantCompletedRatio() ?? 0f;
                }
                catch (Exception)
                {
                    return 0f;
                }
            }
        }

        /// <summary>
        /// 换一个姿势。<paramref name="poseTitle"/> 是游戏内部的 pose title；给一个不存在的名字
        /// 由游戏自己处理（通常是保持原样），本方法不做名字校验——校验需要读姿势表，
        /// 那是资源层的事。
        /// </summary>
        public GameActionResult ChangePose(string poseTitle)
        {
            if (string.IsNullOrEmpty(poseTitle))
            {
                return GameActionResult.Fail(GameActionStatus.InvalidArgument, "姿势名不能为空。");
            }

            PR Pr = GameBinding.Player;
            if (Pr == null)
            {
                return GameActionResult.NoPlayer();
            }

            try
            {
                if (Pr.Anm == null)
                {
                    return GameActionResult.Fail(GameActionStatus.TargetUnavailable, "玩家动画器还没就绪。");
                }

                return Pr.Anm.setPose(poseTitle)
                    ? GameActionResult.Ok()
                    : GameActionResult.Fail(GameActionStatus.RejectedByState, $"游戏拒绝了姿势切换：{poseTitle}。");
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Player.ChangePose");
                return GameActionResult.Fail(GameActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>
        /// 触发一个玩家动作（近战、滑铲、回避、各种突进）。<b>本版本未支持</b>。
        /// <para>
        /// 这些动作在游戏里是状态机迁移的结果，前后摇、无敌帧、技能锁和消耗都挂在迁移路径上；
        /// 从外部直接写状态会把这一串全部绕过去，看起来动了，实际上打不出判定也不吃冷却。
        /// 需要逐个动作核对可用入口之后再逐条开放，届时这里换成返回真实结果。
        /// </para>
        /// </summary>
        public GameActionResult TryAction(string actionKey)
            => GameActionResult.Unsupported($"本版本还没有核对玩家动作入口：{actionKey}。");
    }
}
