namespace Polaris.API
{
    /// <summary>当前地图实例变化；<see cref="GameCallbackStamp.MapGeneration"/> 已经推进过。</summary>
    public sealed class MapChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string PreviousMapKey { get; }
        public string CurrentMapKey { get; }
        public bool EnteredWorld { get; }
        public bool ExitedWorld { get; }

        internal MapChangedEvent(GameCallbackStamp stamp, string previousMapKey, string currentMapKey,
            bool enteredWorld, bool exitedWorld)
        {
            Stamp = stamp;
            PreviousMapKey = previousMapKey;
            CurrentMapKey = currentMapKey;
            EnteredWorld = enteredWorld;
            ExitedWorld = exitedWorld;
        }
    }

    /// <summary>从标题/加载阶段进入世界，或离开当前游戏世界回到标题/加载阶段。</summary>
    public sealed class WorldTransitionEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string MapKey { get; }

        internal WorldTransitionEvent(GameCallbackStamp stamp, string mapKey)
        {
            Stamp = stamp;
            MapKey = mapKey;
        }
    }

    /// <summary>白天/夜晚切换。</summary>
    public sealed class DayNightChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public bool IsNight { get; }

        internal DayNightChangedEvent(GameCallbackStamp stamp, bool isNight)
        {
            Stamp = stamp;
            IsNight = isNight;
        }
    }

    /// <summary>天气位集合变化；<c>Bits</c> 与 <see cref="WorldGameAPI.HasWeather"/> 的位定义一致。</summary>
    public sealed class WeatherChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int PreviousBits { get; }
        public int CurrentBits { get; }

        internal WeatherChangedEvent(GameCallbackStamp stamp, int previousBits, int currentBits)
        {
            Stamp = stamp;
            PreviousBits = previousBits;
            CurrentBits = currentBits;
        }
    }

    /// <summary>最终危险度（含手动附加值，即 <see cref="WorldGameAPI.DangerLevel"/>）变化。</summary>
    public sealed class DangerLevelChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int PreviousLevel { get; }
        public int CurrentLevel { get; }

        internal DangerLevelChangedEvent(GameCallbackStamp stamp, int previousLevel, int currentLevel)
        {
            Stamp = stamp;
            PreviousLevel = previousLevel;
            CurrentLevel = currentLevel;
        }
    }
}
