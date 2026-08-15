using System;
using nel;

namespace Polaris.API
{
    /// <summary>
    /// 玩家角色，共通能力继承自 <see cref="GameCharacter"/>，这里只放玩家独有的状态机。
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
        public bool IsChanting => ReadPr(static p => p.magic_chanting, false);

        /// <summary>判断该玩家当前是否可以执行游戏动作（复用游戏自己的判定，演出/菜单/读档中均为 <c>false</c>）。</summary>
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

        /// <summary>切换该玩家到指定状态。<b>高权限</b>操作：会绕过前后摇、无敌帧、技能锁等正常状态迁移流程，慎用。</summary>
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
        public bool IsNormalState() => ReadPr(static p => p.isNormalState(), false);

        /// <summary>判断该玩家是否处于魔法相关状态。</summary>
        public bool IsMagicState() => ReadPr(static p => p.isMagicState(), false);

        /// <summary><see cref="GameCharacter.Read"/> 的玩家版：省去每处自己判空与兜异常。</summary>
        TValue ReadPr<TValue>(Func<PR, TValue> read, TValue fallback) => Read(Pr, read, fallback);

        /// <summary>也供构造函数调用，那时 <see cref="GameInstance.IsValid"/> 还没法用，故直接收原生对象。</summary>
        static TValue Read<TValue>(PR pr, Func<PR, TValue> read, TValue fallback)
        {
            if (pr == null)
            {
                return fallback;
            }

            try
            {
                return read(pr);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        static GamePlayerState ReadState(PR pr)
            => Read(pr, static p => (GamePlayerState)(int)p.state, GamePlayerState.Offline);

        static bool ReadAlive(PR pr) => Read(pr, static p => p.is_alive, false);

        /// <summary>每帧差分，发布状态变化/死亡/复活三条实例回调；用轮询而非打补丁，因为死亡复活入口太多，读字段更简单可靠。</summary>
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
