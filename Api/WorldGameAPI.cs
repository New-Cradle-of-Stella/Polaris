using System;

namespace Polaris.API
{
    /// <summary>
    /// 世界状态：当前地图、危险度、日夜与天气。
    /// <para>
    /// 旧 LuaAiC 把这些散在 <c>laic.var.map</c>/<c>laic.var.danger</c>/<c>GetWetherState</c> 三处，
    /// 拼写还不一致。这里统一成一个 <see cref="Snapshot"/> 加几个明确的动作。
    /// </para>
    /// </summary>
    public sealed class WorldGameAPI
    {
        /// <summary>
        /// 当前地图 key（如 <c>forest_01</c>）。没有加载地图（标题画面、读档中）时为 <c>null</c>。
        /// </summary>
        public string MapKey
        {
            get
            {
                try
                {
                    return GameBinding.CurrentMap?.key;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 当前危险度，<b>就是玩家在状态页/传送确认框/危险度计上看到的那个数</b>（含手动附加值，
        /// 上限 160）。取不到（没进游戏）时返回 0。
        /// <para>
        /// 注意别和 <see cref="DangerDayScale"/> 搞混：游戏内部另有一个把这个数除以 16
        /// （一天 16 点）得到的 0–10 浮点值，那个是算敌人强度用的，不是显示值。
        /// </para>
        /// </summary>
        public int DangerLevel => MeterVal(real: false);

        /// <summary>
        /// 不含手动附加值的危险度。游戏在记录"达到过的最高夜等级"和文案求值时用的是这一份。
        /// </summary>
        public int DangerBaseLevel => MeterVal(real: true);

        /// <summary>手动附加值本身（0–45），即 <see cref="DangerLevel"/> 与 <see cref="DangerBaseLevel"/> 之差。</summary>
        public int DangerBonus
        {
            get
            {
                try
                {
                    return GameBinding.Night?.getDangerAddedVal() ?? 0;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// 危险度的"天数尺度"值：<c>(危险度) / 16</c>，上限 10。敌人强度、召唤数量、天气强度都按它算，
        /// 玩家看不到这个数。要显示给玩家请用 <see cref="DangerLevel"/>。
        /// </summary>
        public float DangerDayScale
        {
            get
            {
                try
                {
                    return GameBinding.Night?.getDangerLevel() ?? 0f;
                }
                catch (Exception)
                {
                    return 0f;
                }
            }
        }

        static int MeterVal(bool real)
        {
            try
            {
                return GameBinding.Night?.getDangerMeterVal(real) ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>现在是不是夜晚。</summary>
        public bool IsNight
        {
            get
            {
                try
                {
                    return GameBinding.Night?.isNight() ?? false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>某种天气现在是不是生效中。可以同时有多种。</summary>
        public bool HasWeather(GameWeather weather)
        {
            nel.NightController Night = GameBinding.Night;
            if (Night == null)
            {
                return false;
            }

            try
            {
                return Night.hasWeather((nel.WeatherItem.WEATHER)(uint)weather);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>一次性读齐整个世界状态。逐项读也可以，但那样几项之间可能跨帧、彼此不自洽。</summary>
        public WorldSnapshot Snapshot()
        {
            var Snap = new WorldSnapshot
            {
                MapKey = MapKey,
                DangerLevel = DangerLevel,
                DangerBonus = DangerBonus,
                DangerDayScale = DangerDayScale,
                IsNight = IsNight,
                NightLevel = 0f,
                WeatherBits = 0,
            };

            nel.NightController Night = GameBinding.Night;
            if (Night != null)
            {
                try
                {
                    Snap.NightLevel = Night.night_level;
                    Snap.WeatherBits = Night.current_weather_bit;
                }
                catch (Exception)
                {
                    // 快照里的可选项，读不到就保持默认值，不值得让整次快照失败。
                }
            }

            return Snap;
        }

        /// <summary>
        /// 设置危险度的<b>手动附加值</b>（不是相对增量，是直接改写那一份；游戏内部会截到 45）。
        /// <para>
        /// 基础危险度由日夜推进、战斗次数与事件共同算出来，没有可以从外部安全改写的入口——
        /// 直接写基础值会在下一次推进时被抹掉。附加值是游戏自己留出来的那一格，
        /// 也是 <see cref="DangerLevel"/> 里可以由外部负责的那一部分。
        /// </para>
        /// </summary>
        public GameActionResult SetDangerBonus(int value)
        {
            if (value < 0)
            {
                return GameActionResult.Fail(GameActionStatus.InvalidArgument, "The added value cannot be negative.");
            }

            nel.NightController Night = GameBinding.Night;
            if (Night == null)
            {
                return GameActionResult.Fail(GameActionStatus.TargetUnavailable, "The game world has not been entered yet.");
            }

            try
            {
                Night.setAdditionalDangerLevelManual(value);
                return GameActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "World.SetDangerBonus");
                return GameActionResult.Fail(GameActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>
        /// 设置天气。<b>本版本未支持</b>：游戏只提供了天气的读取口，写入是日夜控制器自己的
        /// 推进结果，没有可以从外部安全调用的 setter。
        /// </summary>
        public GameActionResult SetWeather(GameWeather weather, bool enabled)
            => GameActionResult.Unsupported("This game version has no usable weather-write entry point.");

        /// <summary>
        /// 切换地图。<b>本版本未支持</b>：游戏的切图带着一整套事件、淡入淡出与存档时机，
        /// 从外部直接触发会把游戏留在半切图状态。这是高权限动作，需要单独设计再开放。
        /// </summary>
        public GameActionResult MoveToMap(string mapKey)
            => GameActionResult.Unsupported("This game version has no usable map-change entry point.");
    }
}
