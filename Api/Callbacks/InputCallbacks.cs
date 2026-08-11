using System;
using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>玩家输入相关回调，按游戏动作而不是按键码。</summary>
    public sealed class InputCallbacks
    {
        internal InputCallbacks() { }

        static readonly GameSignal<ActionPressedEvent> actionPressedSignal = Declare<ActionPressedEvent>(
            GameCallbackKind.ActionPressed);

        static readonly GameSignal<ActionReleasedEvent> actionReleasedSignal = Declare<ActionReleasedEvent>(
            GameCallbackKind.ActionReleased);

        public GameSignal<ActionPressedEvent> ActionPressed => actionPressedSignal;
        public GameSignal<ActionReleasedEvent> ActionReleased => actionReleasedSignal;

        static readonly GameInputAction[] allActions = (GameInputAction[])Enum.GetValues(typeof(GameInputAction));

        static GameSignal<T> Declare<T>(GameCallbackKind kind) where T : class
        {
            CallbackRegistry.Declare(kind, GameCallbackAvailability.Available, GameCallbackPrecision.Exact,
                "Derived by scanning GameInputAction each frame; only runs when there are subscribers.");
            return new GameSignal<T>(kind);
        }

        /// <summary>由 <see cref="GameStateAPI.Pump"/> 每帧调用。没有订阅者时完全不遍历动作枚举。</summary>
        internal static void Pump()
        {
            bool wantPressed = actionPressedSignal.HasSubscribers;
            bool wantReleased = actionReleasedSignal.HasSubscribers;
            if (!wantPressed && !wantReleased)
            {
                return;
            }

            InputGameAPI input = PolarisAPI.Game.Input;
            foreach (GameInputAction action in allActions)
            {
                if (wantPressed && input.WasPressed(action))
                {
                    actionPressedSignal.Publish(new ActionPressedEvent(
                        CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.Exact), action));
                }

                if (wantReleased && input.WasReleased(action))
                {
                    actionReleasedSignal.Publish(new ActionReleasedEvent(
                        CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.Exact), action));
                }
            }
        }
    }
}
