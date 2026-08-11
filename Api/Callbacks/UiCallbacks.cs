using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>游戏内 UI 生命周期回调。第一批只覆盖 ESC 菜单本身的开始打开/打开完成/开始关闭/关闭完成
    /// （复用 <c>UiGameMenu.activate</c>/<c>deactivate</c>，与 <see cref="GameMenuPauseRuntime"/> 的
    /// 世界暂停 transpiler 是两组独立的补丁，互不影响）。长椅、存读档、对话与制作 UI 的锚点还需要
    /// 进一步 IL 审计，留待后续版本。</summary>
    public sealed class UiCallbacks
    {
        internal UiCallbacks() { }

        static readonly GameSignal<GameMenuOpeningEvent> gameMenuOpeningSignal = new(GameCallbackKind.GameMenuOpening);
        static readonly GameSignal<GameMenuOpenedEvent> gameMenuOpenedSignal = new(GameCallbackKind.GameMenuOpened);
        static readonly GameSignal<GameMenuClosingEvent> gameMenuClosingSignal = new(GameCallbackKind.GameMenuClosing);
        static readonly GameSignal<GameMenuClosedEvent> gameMenuClosedSignal = new(GameCallbackKind.GameMenuClosed);

        public GameSignal<GameMenuOpeningEvent> GameMenuOpening => gameMenuOpeningSignal;
        public GameSignal<GameMenuOpenedEvent> GameMenuOpened => gameMenuOpenedSignal;
        public GameSignal<GameMenuClosingEvent> GameMenuClosing => gameMenuClosingSignal;
        public GameSignal<GameMenuClosedEvent> GameMenuClosed => gameMenuClosedSignal;

        internal static void PublishGameMenuOpening()
        {
            if (!gameMenuOpeningSignal.HasSubscribers) { return; }
            gameMenuOpeningSignal.Publish(new GameMenuOpeningEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact)));
        }

        internal static void PublishGameMenuOpened()
        {
            if (!gameMenuOpenedSignal.HasSubscribers) { return; }
            gameMenuOpenedSignal.Publish(new GameMenuOpenedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact)));
        }

        internal static void PublishGameMenuClosing()
        {
            if (!gameMenuClosingSignal.HasSubscribers) { return; }
            gameMenuClosingSignal.Publish(new GameMenuClosingEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact)));
        }

        internal static void PublishGameMenuClosed()
        {
            if (!gameMenuClosedSignal.HasSubscribers) { return; }
            gameMenuClosedSignal.Publish(new GameMenuClosedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact)));
        }
    }
}
