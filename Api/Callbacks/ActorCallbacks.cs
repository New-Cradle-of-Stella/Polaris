using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>角色、玩家和状态相关回调。第一批只落地玩家在场/离场，其余留给 M3。</summary>
    public sealed class ActorCallbacks
    {
        internal ActorCallbacks() { }

        static readonly GameSignal<PlayerAvailableEvent> playerAvailableSignal = Declare<PlayerAvailableEvent>(
            GameCallbackKind.PlayerAvailable);

        static readonly GameSignal<PlayerUnavailableEvent> playerUnavailableSignal = Declare<PlayerUnavailableEvent>(
            GameCallbackKind.PlayerUnavailable);

        public GameSignal<PlayerAvailableEvent> PlayerAvailable => playerAvailableSignal;
        public GameSignal<PlayerUnavailableEvent> PlayerUnavailable => playerUnavailableSignal;

        // ── M3：由 Harmony 补丁登记，见 Patch/Callbacks/Actors/ ──────────────────────────────
        static readonly GameSignal<PlayerStateChangedEvent> playerStateChangedSignal = new(GameCallbackKind.PlayerStateChanged);
        static readonly GameSignal<EnemyStateChangedEvent> enemyStateChangedSignal = new(GameCallbackKind.EnemyStateChanged);
        static readonly GameSignal<EnemyDiedEvent> enemyDiedSignal = new(GameCallbackKind.EnemyDied);

        public GameSignal<PlayerStateChangedEvent> PlayerStateChanged => playerStateChangedSignal;
        public GameSignal<EnemyStateChangedEvent> EnemyStateChanged => enemyStateChangedSignal;
        public GameSignal<EnemyDiedEvent> EnemyDied => enemyDiedSignal;

        static readonly GameSignal<PlayerDeathStartingEvent> playerDeathStartingSignal = new(GameCallbackKind.PlayerDeathStarting);
        static readonly GameSignal<PlayerDiedEvent> playerDiedSignal = new(GameCallbackKind.PlayerDied);
        static readonly GameSignal<PlayerRevivedEvent> playerRevivedSignal = new(GameCallbackKind.PlayerRevived);
        static readonly GameSignal<StatusChangedEvent> statusAddedSignal = new(GameCallbackKind.StatusAdded);
        static readonly GameSignal<StatusChangedEvent> statusRefreshedSignal = new(GameCallbackKind.StatusRefreshed);
        static readonly GameSignal<StatusChangedEvent> statusRemovedSignal = new(GameCallbackKind.StatusRemoved);

        public GameSignal<PlayerDeathStartingEvent> PlayerDeathStarting => playerDeathStartingSignal;
        public GameSignal<PlayerDiedEvent> PlayerDied => playerDiedSignal;
        public GameSignal<PlayerRevivedEvent> PlayerRevived => playerRevivedSignal;
        public GameSignal<StatusChangedEvent> StatusAdded => statusAddedSignal;
        public GameSignal<StatusChangedEvent> StatusRefreshed => statusRefreshedSignal;
        public GameSignal<StatusChangedEvent> StatusRemoved => statusRemovedSignal;

        static GameSignal<T> Declare<T>(GameCallbackKind kind) where T : class
        {
            CallbackRegistry.Declare(kind, GameCallbackAvailability.Available, GameCallbackPrecision.NextPump,
                "Derived from GameBinding.Player reference becoming non-null/null.");
            return new GameSignal<T>(kind);
        }

        internal static void PublishPlayerStateChanged(string previousState, string currentState)
        {
            if (!playerStateChangedSignal.HasSubscribers) { return; }
            playerStateChangedSignal.Publish(new PlayerStateChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), previousState, currentState));
        }

        internal static void PublishEnemyStateChanged(CharacterHandle enemy, string previousState, string currentState)
        {
            if (!enemyStateChangedSignal.HasSubscribers) { return; }
            enemyStateChangedSignal.Publish(new EnemyStateChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), enemy, previousState, currentState));
        }

        internal static void PublishEnemyDied(CharacterHandle enemy)
        {
            if (!enemyDiedSignal.HasSubscribers) { return; }
            enemyDiedSignal.Publish(new EnemyDiedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), enemy));
        }

        internal static void PublishPlayerDeathStarting()
        {
            if (!playerDeathStartingSignal.HasSubscribers) { return; }
            playerDeathStartingSignal.Publish(new PlayerDeathStartingEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact)));
        }

        internal static void PublishPlayerDied()
        {
            if (!playerDiedSignal.HasSubscribers) { return; }
            playerDiedSignal.Publish(new PlayerDiedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact)));
        }

        internal static void PublishPlayerRevived()
        {
            if (!playerRevivedSignal.HasSubscribers) { return; }
            playerRevivedSignal.Publish(new PlayerRevivedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact)));
        }

        internal static bool WantsStatusEvents => statusAddedSignal.HasSubscribers || statusRefreshedSignal.HasSubscribers;

        internal static void PublishStatusAdded(CharacterHandle target, int serId)
        {
            if (!statusAddedSignal.HasSubscribers) { return; }
            statusAddedSignal.Publish(new StatusChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), target, serId));
        }

        internal static void PublishStatusRefreshed(CharacterHandle target, int serId)
        {
            if (!statusRefreshedSignal.HasSubscribers) { return; }
            statusRefreshedSignal.Publish(new StatusChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), target, serId));
        }

        internal static void PublishStatusRemoved(CharacterHandle target, int serId)
        {
            if (!statusRemovedSignal.HasSubscribers) { return; }
            statusRemovedSignal.Publish(new StatusChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), target, serId));
        }

        static bool lastPresent;

        /// <summary>由 <see cref="GameStateAPI.Pump"/> 每帧调用，在 <c>GameBinding.Pump</c> 之后。</summary>
        internal static void Pump()
        {
            bool present = PolarisAPI.Game.Player.IsPresent;
            if (present == lastPresent)
            {
                return;
            }

            lastPresent = present;

            if (present)
            {
                if (playerAvailableSignal.HasSubscribers)
                {
                    playerAvailableSignal.Publish(new PlayerAvailableEvent(
                        CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.NextPump),
                        PolarisAPI.Game.Player.Handle));
                }
            }
            else if (playerUnavailableSignal.HasSubscribers)
            {
                playerUnavailableSignal.Publish(new PlayerUnavailableEvent(
                    CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.NextPump)));
            }
        }
    }
}
