using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>每帧循环、物理步进和游戏自身帧号推进。</summary>
    public sealed class LoopCallbacks
    {
        internal LoopCallbacks() { }

        static readonly GameFastSignal updatingSignal = DeclareFast(GameCallbackKind.Updating, GameCallbackPrecision.Exact);
        static readonly GameFastSignal lateUpdatingSignal = DeclareFast(GameCallbackKind.LateUpdating, GameCallbackPrecision.Exact);
        static readonly GameFastSignal fixedUpdatingSignal = DeclareFast(GameCallbackKind.FixedUpdating, GameCallbackPrecision.Exact);

        static readonly GameSignal<GameFrameAdvancedEvent> gameFrameAdvancedSignal =
            Declare<GameFrameAdvancedEvent>(GameCallbackKind.GameFrameAdvanced, GameCallbackPrecision.Coalesced);

        public GameFastSignal Updating => updatingSignal;
        public GameFastSignal LateUpdating => lateUpdatingSignal;
        public GameFastSignal FixedUpdating => fixedUpdatingSignal;
        public GameSignal<GameFrameAdvancedEvent> GameFrameAdvanced => gameFrameAdvancedSignal;

        static GameFastSignal DeclareFast(GameCallbackKind kind, GameCallbackPrecision precision)
        {
            CallbackRegistry.Declare(kind, GameCallbackAvailability.Available, precision);
            return new GameFastSignal(kind);
        }

        static GameSignal<T> Declare<T>(GameCallbackKind kind, GameCallbackPrecision precision) where T : class
        {
            CallbackRegistry.Declare(kind, GameCallbackAvailability.Degraded, precision,
                "Derived by comparing XX.IN.totalframe across frames; may skip frames where the game itself doesn't advance.");
            return new GameSignal<T>(kind);
        }

        internal static void RaiseUpdating() => updatingSignal.Raise();
        internal static void RaiseLateUpdating() => lateUpdatingSignal.Raise();
        internal static void RaiseFixedUpdating() => fixedUpdatingSignal.Raise();

        static int lastGameFrame = -1;

        /// <summary>由 <see cref="GameStateAPI.Pump"/> 每帧调用：比较 <c>XX.IN.totalframe</c> 是否推进。</summary>
        internal static void PumpGameFrame()
        {
            if (!gameFrameAdvancedSignal.HasSubscribers)
            {
                return;
            }

            int current = PolarisAPI.Game.Loop.GameFrameCount;
            if (lastGameFrame < 0)
            {
                lastGameFrame = current;
                return;
            }

            if (current == lastGameFrame)
            {
                return;
            }

            int previous = lastGameFrame;
            lastGameFrame = current;
            gameFrameAdvancedSignal.Publish(new GameFrameAdvancedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.Coalesced), previous, current));
        }
    }

    /// <summary>游戏自己的帧号（<c>XX.IN.totalframe</c>）推进了。</summary>
    public sealed class GameFrameAdvancedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int PreviousGameFrame { get; }
        public int CurrentGameFrame { get; }

        internal GameFrameAdvancedEvent(GameCallbackStamp stamp, int previousGameFrame, int currentGameFrame)
        {
            Stamp = stamp;
            PreviousGameFrame = previousGameFrame;
            CurrentGameFrame = currentGameFrame;
        }
    }
}
