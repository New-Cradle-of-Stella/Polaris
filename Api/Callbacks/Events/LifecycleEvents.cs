namespace Polaris.API
{
    /// <summary>本进程首次完全就绪（<see cref="GameStateAPI.IsMtrxReady"/> 从 false 变 true）。</summary>
    public sealed class ReadyEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal ReadyEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }

    /// <summary>当前语言变化，与现有 <see cref="GameStateAPI.LocaleChanged"/> 共享同一次探测结果。</summary>
    public sealed class LocaleChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string PreviousLocale { get; }
        public string CurrentLocale { get; }

        internal LocaleChangedEvent(GameCallbackStamp stamp, string previousLocale, string currentLocale)
        {
            Stamp = stamp;
            PreviousLocale = previousLocale;
            CurrentLocale = currentLocale;
        }
    }

    /// <summary>窗口焦点变化。</summary>
    public sealed class FocusChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public bool HasFocus { get; }

        internal FocusChangedEvent(GameCallbackStamp stamp, bool hasFocus)
        {
            Stamp = stamp;
            HasFocus = hasFocus;
        }
    }

    /// <summary>操作系统挂起/恢复；不等于游戏暂停。</summary>
    public sealed class ApplicationPauseChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public bool IsPaused { get; }

        internal ApplicationPauseChangedEvent(GameCallbackStamp stamp, bool isPaused)
        {
            Stamp = stamp;
            IsPaused = isPaused;
        }
    }

    /// <summary>进程即将退出。只适合做快速收尾。</summary>
    public sealed class StoppingEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal StoppingEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }

    /// <summary>Unity 场景加载完成。</summary>
    public sealed class SceneLoadedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string SceneName { get; }
        public int SceneBuildIndex { get; }
        public bool IsAdditive { get; }

        internal SceneLoadedEvent(GameCallbackStamp stamp, string sceneName, int sceneBuildIndex, bool isAdditive)
        {
            Stamp = stamp;
            SceneName = sceneName;
            SceneBuildIndex = sceneBuildIndex;
            IsAdditive = isAdditive;
        }
    }

    /// <summary>Unity 场景卸载完成。</summary>
    public sealed class SceneUnloadedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string SceneName { get; }
        public int SceneBuildIndex { get; }

        internal SceneUnloadedEvent(GameCallbackStamp stamp, string sceneName, int sceneBuildIndex)
        {
            Stamp = stamp;
            SceneName = sceneName;
            SceneBuildIndex = sceneBuildIndex;
        }
    }

    /// <summary>活动 Unity 场景变化。</summary>
    public sealed class ActiveSceneChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string PreviousSceneName { get; }
        public string CurrentSceneName { get; }

        internal ActiveSceneChangedEvent(GameCallbackStamp stamp, string previousSceneName, string currentSceneName)
        {
            Stamp = stamp;
            PreviousSceneName = previousSceneName;
            CurrentSceneName = currentSceneName;
        }
    }
}
