using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>生命周期与存读档相关回调。第一批只落地不依赖任何新增 Harmony 补丁的部分。</summary>
    public sealed class LifecycleCallbacks
    {
        internal LifecycleCallbacks() { }

        static readonly GameSignal<ReadyEvent> readySignal = Declare<ReadyEvent>(
            GameCallbackKind.Ready, GameCallbackPrecision.Exact);

        static readonly GameSignal<LocaleChangedEvent> localeChangedSignal = Declare<LocaleChangedEvent>(
            GameCallbackKind.LocaleChanged, GameCallbackPrecision.NextPump);

        static readonly GameSignal<FocusChangedEvent> focusChangedSignal = Declare<FocusChangedEvent>(
            GameCallbackKind.FocusChanged, GameCallbackPrecision.Exact);

        static readonly GameSignal<ApplicationPauseChangedEvent> applicationPauseChangedSignal = Declare<ApplicationPauseChangedEvent>(
            GameCallbackKind.ApplicationPauseChanged, GameCallbackPrecision.Exact);

        static readonly GameSignal<StoppingEvent> stoppingSignal = Declare<StoppingEvent>(
            GameCallbackKind.Stopping, GameCallbackPrecision.Exact);

        static readonly GameSignal<SceneLoadedEvent> sceneLoadedSignal = Declare<SceneLoadedEvent>(
            GameCallbackKind.UnitySceneLoaded, GameCallbackPrecision.Exact);

        static readonly GameSignal<SceneUnloadedEvent> sceneUnloadedSignal = Declare<SceneUnloadedEvent>(
            GameCallbackKind.UnitySceneUnloaded, GameCallbackPrecision.Exact);

        static readonly GameSignal<ActiveSceneChangedEvent> activeSceneChangedSignal = Declare<ActiveSceneChangedEvent>(
            GameCallbackKind.ActiveSceneChanged, GameCallbackPrecision.Exact);

        // ── M2：存读档与游戏场景生命周期，全部由 Harmony 补丁登记，见 Patch/Callbacks/Lifecycle/ ──
        static readonly GameSignal<GameSceneStartingEvent> gameSceneStartingSignal = new(GameCallbackKind.GameSceneStarting);
        static readonly GameSignal<GameSceneStartedEvent> gameSceneStartedSignal = new(GameCallbackKind.GameSceneStarted);
        static readonly GameSignal<NewGameStartingEvent> newGameStartingSignal = new(GameCallbackKind.NewGameStarting);
        static readonly GameSignal<NewGameStartedEvent> newGameStartedSignal = new(GameCallbackKind.NewGameStarted);
        static readonly GameSignal<SaveLoadingEvent> saveLoadingSignal = new(GameCallbackKind.SaveLoading);
        static readonly GameSignal<SaveLoadedEvent> saveLoadedSignal = new(GameCallbackKind.SaveLoaded);
        static readonly GameSignal<SaveFailedEvent> saveFailedSignal = new(GameCallbackKind.SaveFailed);
        static readonly GameSignal<SaveSerializingEvent> saveSerializingSignal = new(GameCallbackKind.SaveSerializing);
        static readonly GameSignal<SaveSerializedEvent> saveSerializedSignal = new(GameCallbackKind.SaveSerialized);
        static readonly GameSignal<SaveWritingEvent> saveWritingSignal = new(GameCallbackKind.SaveWriting);
        static readonly GameSignal<SaveWrittenEvent> saveWrittenSignal = new(GameCallbackKind.SaveWritten);
        static readonly GameSignal<AutoSaveStartingEvent> autoSaveStartingSignal = new(GameCallbackKind.AutoSaveStarting);
        static readonly GameSignal<AutoSaveCompletedEvent> autoSaveCompletedSignal = new(GameCallbackKind.AutoSaveCompleted);

        public GameSignal<ReadyEvent> Ready => readySignal;
        public GameSignal<LocaleChangedEvent> LocaleChanged => localeChangedSignal;
        public GameSignal<FocusChangedEvent> FocusChanged => focusChangedSignal;
        public GameSignal<ApplicationPauseChangedEvent> ApplicationPauseChanged => applicationPauseChangedSignal;
        public GameSignal<StoppingEvent> Stopping => stoppingSignal;
        public GameSignal<SceneLoadedEvent> UnitySceneLoaded => sceneLoadedSignal;
        public GameSignal<SceneUnloadedEvent> UnitySceneUnloaded => sceneUnloadedSignal;
        public GameSignal<ActiveSceneChangedEvent> ActiveSceneChanged => activeSceneChangedSignal;

        public GameSignal<GameSceneStartingEvent> GameSceneStarting => gameSceneStartingSignal;
        public GameSignal<GameSceneStartedEvent> GameSceneStarted => gameSceneStartedSignal;
        public GameSignal<NewGameStartingEvent> NewGameStarting => newGameStartingSignal;
        public GameSignal<NewGameStartedEvent> NewGameStarted => newGameStartedSignal;
        public GameSignal<SaveLoadingEvent> SaveLoading => saveLoadingSignal;
        public GameSignal<SaveLoadedEvent> SaveLoaded => saveLoadedSignal;
        public GameSignal<SaveFailedEvent> SaveFailed => saveFailedSignal;
        public GameSignal<SaveSerializingEvent> SaveSerializing => saveSerializingSignal;
        public GameSignal<SaveSerializedEvent> SaveSerialized => saveSerializedSignal;
        public GameSignal<SaveWritingEvent> SaveWriting => saveWritingSignal;
        public GameSignal<SaveWrittenEvent> SaveWritten => saveWrittenSignal;
        public GameSignal<AutoSaveStartingEvent> AutoSaveStarting => autoSaveStartingSignal;
        public GameSignal<AutoSaveCompletedEvent> AutoSaveCompleted => autoSaveCompletedSignal;

        static GameSignal<T> Declare<T>(GameCallbackKind kind, GameCallbackPrecision precision) where T : class
        {
            CallbackRegistry.Declare(kind, GameCallbackAvailability.Available, precision);
            return new GameSignal<T>(kind);
        }

        // ── 内部发布入口：只由 GameStateAPI 的 Pump 与 Plugin 的 Unity 生命周期回调调用 ──────

        internal static void PublishReady()
        {
            // 与是否有订阅者无关：CoinStorage 的监听表只能装一次，且必须等 Aentry 建好之后。
            InventoryCallbacks.InstallMoneyListenersOnce();

            if (!readySignal.HasSubscribers) { return; }
            readySignal.Publish(new ReadyEvent(CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.Exact)));
        }

        internal static void PublishLocaleChanged(string previous, string current)
        {
            if (!localeChangedSignal.HasSubscribers) { return; }
            localeChangedSignal.Publish(new LocaleChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.NextPump), previous, current));
        }

        internal static void PublishFocusChanged(bool hasFocus)
        {
            if (!focusChangedSignal.HasSubscribers) { return; }
            focusChangedSignal.Publish(new FocusChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.UnityLifecycle, GameCallbackPrecision.Exact), hasFocus));
        }

        internal static void PublishApplicationPauseChanged(bool isPaused)
        {
            if (!applicationPauseChangedSignal.HasSubscribers) { return; }
            applicationPauseChangedSignal.Publish(new ApplicationPauseChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.UnityLifecycle, GameCallbackPrecision.Exact), isPaused));
        }

        internal static void PublishStopping()
        {
            if (!stoppingSignal.HasSubscribers) { return; }
            // 进程正在退出：不排队，直接同步派发，否则这个事件永远等不到下一次 Drain。
            stoppingSignal.RaiseNow(new StoppingEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.UnityLifecycle, GameCallbackPrecision.Exact)));
        }

        internal static void PublishSceneLoaded(string sceneName, int buildIndex, bool additive)
        {
            if (!sceneLoadedSignal.HasSubscribers) { return; }
            sceneLoadedSignal.Publish(new SceneLoadedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.UnityLifecycle, GameCallbackPrecision.Exact), sceneName, buildIndex, additive));
        }

        internal static void PublishSceneUnloaded(string sceneName, int buildIndex)
        {
            if (!sceneUnloadedSignal.HasSubscribers) { return; }
            sceneUnloadedSignal.Publish(new SceneUnloadedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.UnityLifecycle, GameCallbackPrecision.Exact), sceneName, buildIndex));
        }

        internal static void PublishActiveSceneChanged(string previous, string current)
        {
            if (!activeSceneChangedSignal.HasSubscribers) { return; }
            activeSceneChangedSignal.Publish(new ActiveSceneChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.UnityLifecycle, GameCallbackPrecision.Exact), previous, current));
        }

        // ── M2 发布入口：只由对应的 Harmony 补丁调用（见 Patch/Callbacks/Lifecycle/） ──────────

        internal static void PublishGameSceneStarting()
        {
            if (!gameSceneStartingSignal.HasSubscribers) { return; }
            gameSceneStartingSignal.Publish(new GameSceneStartingEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact)));
        }

        internal static void PublishGameSceneStarted(bool loadedExistingSave)
        {
            if (!gameSceneStartedSignal.HasSubscribers) { return; }
            gameSceneStartedSignal.Publish(new GameSceneStartedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), loadedExistingSave));
        }

        internal static void PublishNewGameStarting()
        {
            if (!newGameStartingSignal.HasSubscribers) { return; }
            newGameStartingSignal.Publish(new NewGameStartingEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact)));
        }

        internal static void PublishNewGameStarted()
        {
            if (!newGameStartedSignal.HasSubscribers) { return; }
            newGameStartedSignal.Publish(new NewGameStartedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact)));
        }

        internal static void PublishSaveLoading(int slotIndex)
        {
            if (!saveLoadingSignal.HasSubscribers) { return; }
            saveLoadingSignal.Publish(new SaveLoadingEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), slotIndex));
        }

        internal static void PublishSaveLoaded(int slotIndex)
        {
            if (!saveLoadedSignal.HasSubscribers) { return; }
            saveLoadedSignal.Publish(new SaveLoadedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), slotIndex));
        }

        internal static void PublishSaveFailed(int slotIndex, string reason)
        {
            if (!saveFailedSignal.HasSubscribers) { return; }
            saveFailedSignal.Publish(new SaveFailedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), slotIndex, reason));
        }

        internal static void PublishSaveSerializing(int slotIndex)
        {
            if (!saveSerializingSignal.HasSubscribers) { return; }
            saveSerializingSignal.Publish(new SaveSerializingEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), slotIndex));
        }

        internal static void PublishSaveSerialized(int slotIndex, int byteCount)
        {
            if (!saveSerializedSignal.HasSubscribers) { return; }
            saveSerializedSignal.Publish(new SaveSerializedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), slotIndex, byteCount));
        }

        internal static void PublishSaveWriting(int slotIndex)
        {
            if (!saveWritingSignal.HasSubscribers) { return; }
            saveWritingSignal.Publish(new SaveWritingEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), slotIndex));
        }

        internal static void PublishSaveWritten(int slotIndex, bool succeeded, string failureReason)
        {
            if (!saveWrittenSignal.HasSubscribers) { return; }
            saveWrittenSignal.Publish(new SaveWrittenEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), slotIndex, succeeded, failureReason));
        }

        internal static void PublishAutoSaveStarting(bool isBench)
        {
            if (!autoSaveStartingSignal.HasSubscribers) { return; }
            autoSaveStartingSignal.Publish(new AutoSaveStartingEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), isBench));
        }

        internal static void PublishAutoSaveCompleted(bool isBench, bool succeeded)
        {
            if (!autoSaveCompletedSignal.HasSubscribers) { return; }
            autoSaveCompletedSignal.Publish(new AutoSaveCompletedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), isBench, succeeded));
        }
    }
}
