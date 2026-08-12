using System;
using nel;

namespace Polaris.API
{
    /// <summary>
    /// 玩家角色。位置、体力、位移这些与其它角色共通的能力继承自 <see cref="GameCharacter"/>，
    /// 这里只放玩家独有的状态机。
    /// <para>
    /// 状态机是判断"玩家现在能不能动"最可靠的依据，比自己去看速度和姿势准得多：
    /// 受伤、演出、被吞、坐板凳在游戏里都是各自独立的状态。
    /// </para>
    /// </summary>
    public sealed class GamePlayer : GameCharacter
    {
        static readonly InstanceTable<PR, GamePlayer> Table = new();

        GamePlayerState lastState;
        bool lastAlive = true;

        GamePlayer(PR target) : base(target)
        {
            lastState = ReadState(target);
            lastAlive = ReadAlive(target);
        }

        internal static GamePlayer Wrap(PR native) => Table.Get(native, static n => new GamePlayer(n));

        internal static void InvalidateAllPlayers() => Table.InvalidateAll();

        internal static void SweepPlayers() => Table.Sweep();

        PR Pr => Native as PR;

        private protected override string Describe() => "GamePlayer";

        /// <summary>获取该玩家当前状态。游戏在新版本里加入的未知状态会原样以数值形式返回。</summary>
        public GamePlayerState State => ReadState(Pr);

        /// <summary>判断该玩家是否正在咏唱魔法。</summary>
        public bool IsChanting
        {
            get
            {
                PR pr = Pr;
                if (pr == null)
                {
                    return false;
                }

                try
                {
                    return pr.magic_chanting;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 判断该玩家当前是否可以执行游戏动作。问的是游戏自己的那套判定
        /// （<c>Map2d.playerActionUseable</c>），因此演出、菜单、读档中都会得到 <c>false</c>，
        /// 不需要调用方自己去枚举"哪些状态算不能动"。
        /// </summary>
        public bool CanAct()
        {
            if (!IsValid)
            {
                return false;
            }

            try
            {
                return GameBinding.CurrentMap?.playerActionUseable() ?? false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 切换该玩家到指定状态。
        /// <para>
        /// 这是<b>高权限</b>动作：状态迁移在游戏里带着前后摇、无敌帧、技能锁与消耗，
        /// 直接写状态会把这一串全部绕过去。内容脚本请优先用游戏自己的触发路径，
        /// 只有明确知道自己在做什么时才用这个方法。
        /// </para>
        /// </summary>
        public void ChangeState(GamePlayerState state)
        {
            EnsureUsable();

            PR pr = Pr;
            if (pr == null)
            {
                return;
            }

            try
            {
                pr.changeState((PR.STATE)(int)state);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GamePlayer.ChangeState");
            }
        }

        /// <summary>判断该玩家是否处于普通状态（可自由行动，没有在受伤/演出/被吞/坐板凳）。</summary>
        public bool IsNormalState()
        {
            PR pr = Pr;
            if (pr == null)
            {
                return false;
            }

            try
            {
                return pr.isNormalState();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>判断该玩家是否处于魔法相关状态。</summary>
        public bool IsMagicState()
        {
            PR pr = Pr;
            if (pr == null)
            {
                return false;
            }

            try
            {
                return pr.isMagicState();
            }
            catch (Exception)
            {
                return false;
            }
        }

        static GamePlayerState ReadState(PR pr)
        {
            if (pr == null)
            {
                return GamePlayerState.Offline;
            }

            try
            {
                return (GamePlayerState)(int)pr.state;
            }
            catch (Exception)
            {
                return GamePlayerState.Offline;
            }
        }

        static bool ReadAlive(PR pr)
        {
            if (pr == null)
            {
                return false;
            }

            try
            {
                return pr.is_alive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 每帧差分：状态变化、死亡与复活三条实例回调都由这里发布。
        /// <para>
        /// 走轮询而不是给 <c>PR.changeState</c> 打补丁，是因为死亡与复活在游戏里有多条入口
        /// （伤害致死、事件强制、游戏结束恢复、替身猫复活），逐条打补丁要跟着游戏版本追一整串
        /// 内部调用链；而"状态字段变了"读一个字段就能知道。
        /// </para>
        /// </summary>
        internal void PumpState()
        {
            PR pr = Pr;
            if (pr == null)
            {
                return;
            }

            GamePlayerState current = ReadState(pr);
            if (current != lastState)
            {
                GamePlayerState previous = lastState;
                lastState = current;
                GameCallbackHub.PublishInstance(
                    GameInstanceCallbackKind.PlayerStateChanged,
                    this,
                    () => new PlayerStateChangedCallbackData(this, previous, current));
            }

            bool alive = ReadAlive(pr);
            if (alive == lastAlive)
            {
                return;
            }

            lastAlive = alive;
            if (alive)
            {
                GameCallbackHub.PublishInstance(
                    GameInstanceCallbackKind.PlayerRevived, this, () => new PlayerRevivedCallbackData(this));
            }
            else
            {
                GameCallbackHub.PublishInstance(
                    GameInstanceCallbackKind.PlayerDied, this, () => new PlayerDiedCallbackData(this));
            }
        }
    }
}
