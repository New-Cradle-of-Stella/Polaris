namespace Polaris.API
{
    /// <summary>某个游戏动作这一帧刚按下（按下沿）。</summary>
    public sealed class ActionPressedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public GameInputAction Action { get; }

        internal ActionPressedEvent(GameCallbackStamp stamp, GameInputAction action)
        {
            Stamp = stamp;
            Action = action;
        }
    }

    /// <summary>某个游戏动作这一帧刚松开（松开沿）。</summary>
    public sealed class ActionReleasedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public GameInputAction Action { get; }

        internal ActionReleasedEvent(GameCallbackStamp stamp, GameInputAction action)
        {
            Stamp = stamp;
            Action = action;
        }
    }
}
