using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>世界、地图、日夜与天气回调。第一批全部走状态差分，标记为 Degraded。</summary>
    public sealed class WorldCallbacks
    {
        internal WorldCallbacks() { }

        static readonly GameSignal<WorldTransitionEvent> worldEnteredSignal = Declare<WorldTransitionEvent>(
            GameCallbackKind.WorldEntered, "Derived from GameBinding.CurrentMap becoming non-null.");

        static readonly GameSignal<WorldTransitionEvent> worldExitedSignal = Declare<WorldTransitionEvent>(
            GameCallbackKind.WorldExited, "Derived from GameBinding.CurrentMap becoming null.");

        static readonly GameSignal<MapChangedEvent> mapChangedSignal = Declare<MapChangedEvent>(
            GameCallbackKind.MapChanged, "Derived from GameBinding.CurrentMap reference comparison; upgrades to Exact once MapChanging is patched.");

        static readonly GameSignal<DayNightChangedEvent> dayNightChangedSignal = Declare<DayNightChangedEvent>(
            GameCallbackKind.DayNightChanged, "Derived from NightController.isNight() comparison.");

        static readonly GameSignal<WeatherChangedEvent> weatherChangedSignal = Declare<WeatherChangedEvent>(
            GameCallbackKind.WeatherChanged, "Derived from NightController.current_weather_bit comparison.");

        static readonly GameSignal<DangerLevelChangedEvent> dangerLevelChangedSignal = Declare<DangerLevelChangedEvent>(
            GameCallbackKind.DangerLevelChanged, "Derived from WorldGameAPI.DangerLevel comparison.");

        public GameSignal<WorldTransitionEvent> WorldEntered => worldEnteredSignal;
        public GameSignal<WorldTransitionEvent> WorldExited => worldExitedSignal;
        public GameSignal<MapChangedEvent> MapChanged => mapChangedSignal;
        public GameSignal<DayNightChangedEvent> DayNightChanged => dayNightChangedSignal;
        public GameSignal<WeatherChangedEvent> WeatherChanged => weatherChangedSignal;
        public GameSignal<DangerLevelChangedEvent> DangerLevelChanged => dangerLevelChangedSignal;

        static GameSignal<T> Declare<T>(GameCallbackKind kind, string reason) where T : class
        {
            CallbackRegistry.Declare(kind, GameCallbackAvailability.Degraded, GameCallbackPrecision.NextPump, reason);
            return new GameSignal<T>(kind);
        }

        static string lastMapKey;
        static bool lastInWorld;
        static bool mapKnown;

        static bool lastIsNight;
        static bool nightKnown;

        static int lastWeatherBits;
        static bool weatherKnown;

        static int lastDangerLevel;
        static bool dangerKnown;

        /// <summary>由 <see cref="GameStateAPI.Pump"/> 每帧调用，在 <c>GameBinding.Pump</c> 之后。</summary>
        internal static void Pump()
        {
            PumpMap();
            PumpDayNight();
            PumpWeather();
            PumpDanger();
        }

        static void PumpMap()
        {
            string currentKey = SafeMapKey();
            bool inWorld = currentKey != null;

            if (!mapKnown)
            {
                mapKnown = true;
                lastMapKey = currentKey;
                lastInWorld = inWorld;
                return;
            }

            if (currentKey == lastMapKey && inWorld == lastInWorld)
            {
                return;
            }

            string previousKey = lastMapKey;
            bool wasInWorld = lastInWorld;
            lastMapKey = currentKey;
            lastInWorld = inWorld;

            if (mapChangedSignal.HasSubscribers)
            {
                mapChangedSignal.Publish(new MapChangedEvent(
                    CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.NextPump),
                    previousKey, currentKey, !wasInWorld && inWorld, wasInWorld && !inWorld));
            }

            if (!wasInWorld && inWorld && worldEnteredSignal.HasSubscribers)
            {
                worldEnteredSignal.Publish(new WorldTransitionEvent(
                    CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.NextPump), currentKey));
            }
            else if (wasInWorld && !inWorld && worldExitedSignal.HasSubscribers)
            {
                worldExitedSignal.Publish(new WorldTransitionEvent(
                    CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.NextPump), previousKey));
            }
        }

        static void PumpDayNight()
        {
            if (!dayNightChangedSignal.HasSubscribers)
            {
                nightKnown = false; // 没订阅者时不追踪，避免"订阅那一刻立刻收到一条历史变化"的错觉
                return;
            }

            bool isNight = PolarisAPI.Game.World.IsNight;
            if (!nightKnown)
            {
                nightKnown = true;
                lastIsNight = isNight;
                return;
            }

            if (isNight == lastIsNight)
            {
                return;
            }

            lastIsNight = isNight;
            dayNightChangedSignal.Publish(new DayNightChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.NextPump), isNight));
        }

        static void PumpWeather()
        {
            if (!weatherChangedSignal.HasSubscribers)
            {
                weatherKnown = false;
                return;
            }

            int bits = SafeWeatherBits();
            if (!weatherKnown)
            {
                weatherKnown = true;
                lastWeatherBits = bits;
                return;
            }

            if (bits == lastWeatherBits)
            {
                return;
            }

            int previous = lastWeatherBits;
            lastWeatherBits = bits;
            weatherChangedSignal.Publish(new WeatherChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.NextPump), previous, bits));
        }

        static void PumpDanger()
        {
            if (!dangerLevelChangedSignal.HasSubscribers)
            {
                dangerKnown = false;
                return;
            }

            int level = PolarisAPI.Game.World.DangerLevel;
            if (!dangerKnown)
            {
                dangerKnown = true;
                lastDangerLevel = level;
                return;
            }

            if (level == lastDangerLevel)
            {
                return;
            }

            int previous = lastDangerLevel;
            lastDangerLevel = level;
            dangerLevelChangedSignal.Publish(new DangerLevelChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.StateDifference, GameCallbackPrecision.NextPump), previous, level));
        }

        static string SafeMapKey()
        {
            try { return PolarisAPI.Game.World.MapKey; }
            catch (System.Exception) { return null; }
        }

        static int SafeWeatherBits()
        {
            try { return GameBinding.Night?.current_weather_bit ?? 0; }
            catch (System.Exception) { return 0; }
        }
    }
}
