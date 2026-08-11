namespace Polaris.API
{
    /// <summary>玩家状态机真正变化（不是每次 <c>changeState</c> 调用都算——很多调用因为前置条件提前返回）。</summary>
    public sealed class PlayerStateChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string PreviousState { get; }
        public string CurrentState { get; }

        internal PlayerStateChangedEvent(GameCallbackStamp stamp, string previousState, string currentState)
        {
            Stamp = stamp;
            PreviousState = previousState;
            CurrentState = currentState;
        }
    }

    /// <summary>敌人状态机真正变化。</summary>
    public sealed class EnemyStateChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public CharacterHandle Enemy { get; }
        public string PreviousState { get; }
        public string CurrentState { get; }

        internal EnemyStateChangedEvent(GameCallbackStamp stamp, CharacterHandle enemy, string previousState, string currentState)
        {
            Stamp = stamp;
            Enemy = enemy;
            PreviousState = previousState;
            CurrentState = currentState;
        }
    }

    /// <summary>敌人死亡确定（第一次真正从"活着"变为 <c>STATE.DIE</c>，不是重复调用）。</summary>
    public sealed class EnemyDiedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public CharacterHandle Enemy { get; }

        internal EnemyDiedEvent(GameCallbackStamp stamp, CharacterHandle enemy)
        {
            Stamp = stamp;
            Enemy = enemy;
        }
    }
}
