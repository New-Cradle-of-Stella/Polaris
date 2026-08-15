using System;
using m2d;
using nel;
using Polaris.API;
using XX;

namespace Polaris
{
    public static partial class PolarisAPI
    {
        /// <summary>
        /// 游戏能力层的入口：静态 API 只回答全局状态、提供入口和获取实例，具体操作交给
        /// <see cref="API.GamePlayer"/>/<see cref="API.GameCharacter"/> 等实例完成。
        /// 查询无副作用、取不到返回空值/零值、不抛异常；公开签名不出现游戏类型。
        /// </summary>
        public static class Game
        {
            /// <summary>只读查询的统一包装：取不到（游戏内部还没建好、或读取本身抛异常）就给回退值，绝不把异常漏给调用方。</summary>
            static TValue Safe<TValue>(Func<TValue> read, TValue fallback)
            {
                try
                {
                    return read();
                }
                catch (Exception)
                {
                    return fallback;
                }
            }

            /// <summary>游戏循环。</summary>
            public static class Loop
            {
                /// <summary>获取游戏自己的帧计数（非 Unity 帧数），读档/演出/暂停期间不推进。</summary>
                public static int FrameCount => Safe(static () => IN.totalframe, 0);

                /// <summary>判断游戏窗口当前是否持有输入焦点。</summary>
                public static bool HasFocus => Safe(static () => IN.application_focus, true);
            }

            /// <summary>玩家输入的只读查询：只暴露游戏动作而非虚拟键码，天然跟随改键设置，键鼠手柄同一套代码。</summary>
            public static class Input
            {
                /// <summary>获取鼠标当前的屏幕坐标（游戏的 GUI 坐标系，1280×720 基准）。</summary>
                public static GameVector2 MousePosition => Safe(static () => (GameVector2)IN.Mouse, GameVector2.Zero);

                /// <summary>获取本帧鼠标滚轮的滚动量。</summary>
                public static GameVector2 MouseWheelDelta => Safe(static () => (GameVector2)IN.MouseWheel, GameVector2.Zero);

                /// <summary>判断指定输入动作当前是否正被按下。</summary>
                public static bool IsHeld(GameInputAction action) => InputBinding.Value(action) > 0f;

                /// <summary>判断指定输入动作是否在本帧或缓冲帧内刚刚按下；<paramref name="bufferFrames"/> 为输入缓冲窗口，传 1 表示仅严格按下沿。</summary>
                public static bool WasPressed(GameInputAction action, int bufferFrames = 1)
                {
                    float v = InputBinding.Value(action);
                    int window = bufferFrames < 1 ? 1 : bufferFrames;
                    return v > 0f && v <= window;
                }

                /// <summary>判断指定输入动作是否在本帧刚刚释放；<paramref name="heldFrames"/> 要求按住时长达标（区分轻点与长按），传 0 表示不限制。</summary>
                public static bool WasReleased(GameInputAction action, int heldFrames = 0)
                {
                    float v = InputBinding.Value(action);

                    // 负值表示刚松开，绝对值为松开后帧数；超出 -1024 视为未初始化。
                    bool justReleased = v < 0f && v >= -1f && v > -1024f;
                    if (!justReleased)
                    {
                        return false;
                    }

                    return heldFrames <= 0 || InputBinding.LastHeldFrames(action) >= heldFrames;
                }

                /// <summary>获取当前方向输入合成的向量，X/Y 各取 -1/0/1。</summary>
                public static GameVector2 GetDirection()
                {
                    float x = (IsHeld(GameInputAction.Right) ? 1f : 0f) - (IsHeld(GameInputAction.Left) ? 1f : 0f);
                    float y = (IsHeld(GameInputAction.Down) ? 1f : 0f) - (IsHeld(GameInputAction.Up) ? 1f : 0f);
                    return new GameVector2(x, y);
                }

                /// <summary>清除指定输入动作的当前按键状态；<paramref name="onlyPressDown"/> 为真时只清"刚按下"沿，保留持续按住状态。</summary>
                public static void ClearState(string key, bool onlyPressDown = true)
                {
                    if (string.IsNullOrEmpty(key))
                    {
                        return;
                    }

                    try
                    {
                        IN.clearKeyState(key, onlyPressDown);
                    }
                    catch (Exception ex)
                    {
                        Errors.Report(ex, "Game.Input.ClearState");
                    }
                }
            }

            /// <summary>游戏资源的加载进度。</summary>
            public static class Assets
            {
                /// <summary>
                /// 获取 <c>MTRX</c> 内部加载阶段值，7 表示全部就绪（之前访问 <c>MTRX.OMI</c> 等会 NullReferenceException）。
                /// 刻意读字段而非 <c>MTRX.prepared</c> 属性，因为该 getter 有副作用会推进加载阶段。
                /// </summary>
                public static int LoadStage => Safe(static () => MTRX.loaded, 0);
            }

            /// <summary>游戏语言。</summary>
            public static class Localization
            {
                /// <summary>获取游戏当前使用的语言区域代码（如 <c>"_"</c>/<c>"en"</c>/<c>"zh-cn"</c>）。</summary>
                public static string CurrentLocale => Safe(static () => TX.getCurrentFamilyName(), null);

                /// <summary>获取游戏默认的语言区域代码。</summary>
                public static string DefaultLocale => Safe(static () => TX.default_family, null);

                /// <summary>切换游戏当前使用的语言。</summary>
                public static void Change(string locale)
                {
                    if (string.IsNullOrEmpty(locale))
                    {
                        return;
                    }

                    try
                    {
                        TX.changeFamily(locale);
                    }
                    catch (Exception ex)
                    {
                        Errors.Report(ex, "Game.Localization.Change");
                    }
                }

                /// <summary>判断指定语言是否为当前语言。</summary>
                public static bool IsCurrent(string locale)
                    => !string.IsNullOrEmpty(locale) && Safe(() => TX.familyIs(locale), false);
            }

            /// <summary>世界状态：地图、日夜、天气与危险度。</summary>
            public static class World
            {
                /// <summary>
                /// 取得当前地图实例；没有加载地图（标题画面、读档中）时为 <c>null</c>。
                /// 地图的分段查询与操作在 <see cref="API.GameMap"/> 上。
                /// </summary>
                public static GameMap CurrentMap => GameMap.Wrap(GameBinding.CurrentMap);

                /// <summary>取得当前玩家实例；玩家不在场时为 <c>null</c>。查询/操作见 <see cref="API.GamePlayer"/>。</summary>
                public static GamePlayer CurrentPlayer => GamePlayer.Wrap(GameBinding.Player);

                /// <summary>切换到指定地图并返回新地图实例（高权限操作，触发事件/淡入淡出/存档时机）；本版本没有该地图时抛 <see cref="ArgumentException"/>。</summary>
                public static GameMap ChangeMap(string mapKey)
                {
                    if (string.IsNullOrEmpty(mapKey))
                    {
                        throw new ArgumentException("Map key cannot be empty.", nameof(mapKey));
                    }

                    M2DBase m2d = SafeM2D();
                    if (m2d == null)
                    {
                        throw new InvalidOperationException("The game world has not been entered yet; there is nothing to change maps from.");
                    }

                    Map2d target;
                    try
                    {
                        target = m2d.getMapObject()?.Get(mapKey);
                    }
                    catch (Exception ex)
                    {
                        Errors.Report(ex, "Game.World.ChangeMap");
                        throw new InvalidOperationException($"The game refused to look up the map: {mapKey}.", ex);
                    }

                    if (target == null)
                    {
                        throw new ArgumentException($"No such map in this game version: {mapKey}.", nameof(mapKey));
                    }

                    try
                    {
                        return GameMap.Wrap(m2d.changeMap(target) ?? target);
                    }
                    catch (Exception ex)
                    {
                        Errors.Report(ex, "Game.World.ChangeMap");
                        throw new InvalidOperationException($"The game refused to change to the map: {mapKey}.", ex);
                    }
                }

                /// <summary>判断当前是否处于夜晚状态。</summary>
                public static bool IsNight() => Night(static n => n.isNight(), false);

                /// <summary>判断当前是否正在下指定天气。可以同时有多种天气生效。</summary>
                public static bool HasWeather(GameWeather weather)
                    => Night(n => n.hasWeather((WeatherItem.WEATHER)(uint)weather), false);

                /// <summary>强制设置指定天气，返回是否设置成功；走游戏的"临时天气"通道，直接改写基础天气会在下次日夜推进时被抹掉。</summary>
                public static bool SetWeather(GameWeather weather)
                    => NightAct("SetWeather", n => n.initTemporaryWeather(TemporaryWeatherFlagKey, (int)weather));

                /// <summary>获取当前危险等级：算敌人强度用的 0–10 浮点尺度，非玩家状态页显示值（那个用 <see cref="GetDangerMeter"/>）。</summary>
                public static float DangerLevel => Night(static n => n.getDangerLevel(), 0f);

                /// <summary>
                /// 获取当前危险度计数值，也就是玩家在状态页/传送确认框上看到的那个数。
                /// <paramref name="real"/> 为真时不含手动附加值。
                /// </summary>
                public static int GetDangerMeter(bool real = true, bool raw = false)
                    => Night(n => n.getDangerMeterVal(real, raw), 0);

                /// <summary>获取或设置手动附加的危险度（0–45，游戏内部会截断）；基础危险度无法从外部安全改写。</summary>
                public static int DangerBonus
                {
                    get => Night(static n => n.getDangerAddedVal(), 0);
                    set
                    {
                        if (value >= 0)
                        {
                            NightAct("DangerBonus", n => n.setAdditionalDangerLevelManual(value));
                        }
                    }
                }

                /// <summary>清除当前天气效果。</summary>
                public static void ClearWeather() => NightAct("ClearWeather", static n => n.clearWeather());

                /// <summary>随机重新选择当前天气。</summary>
                public static void ShuffleWeather() => NightAct("ShuffleWeather", static n => n.weatherShuffle());

                /// <summary>设置游戏菜单打开时是否继续模拟世界（<c>false</c> 为原版暂停行为）；需要四个 Harmony 补丁全部生效才有效，否则是空操作。</summary>
                public static void SetPauseSimulation(bool simulation) => GameMenuPauseRuntime.SetPolicy(!simulation);

                /// <summary>设置夜晚系统记录的战斗次数。危险度的推进会参考它。</summary>
                public static int BattleCount
                {
                    set => NightAct("BattleCount", n => n.setBattleCount(value));
                }

                /// <summary>Polaris 设置临时天气时使用的旗标 key，避免和游戏自己的临时天气互相顶掉。</summary>
                const string TemporaryWeatherFlagKey = "polaris_weather";

                static TValue Night<TValue>(Func<NightController, TValue> read, TValue fallback)
                {
                    NightController night = GameBinding.Night;
                    if (night == null)
                    {
                        return fallback;
                    }

                    try
                    {
                        return read(night);
                    }
                    catch (Exception)
                    {
                        return fallback;
                    }
                }

                /// <summary>对日夜控制器的写操作的统一包装；控制器缺席或调用抛异常时返回 <c>false</c>，不把异常漏给调用方。</summary>
                static bool NightAct(string what, Action<NightController> act)
                {
                    NightController night = GameBinding.Night;
                    if (night == null)
                    {
                        return false;
                    }

                    try
                    {
                        act(night);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Errors.Report(ex, $"Game.World.{what}");
                        return false;
                    }
                }

                static M2DBase SafeM2D() => Safe(static () => M2DBase.Instance, null);
            }

            /// <summary>物品定义的查询入口。</summary>
            public static class Items
            {
                /// <summary>按物品键名取得 <see cref="API.GameItem"/> 实例；本版本没有这个物品时返回 <c>null</c>。</summary>
                public static GameItem Resolve(string itemKey) => GameItem.Resolve(itemKey);
            }

            /// <summary>四个物品存储容器。玩家不在场或存档还没读进来时都为 <c>null</c>。</summary>
            public static class Inventory
            {
                /// <summary>取得主物品栏的 <see cref="API.GameStorage"/> 实例。</summary>
                public static GameStorage Main => GameStorage.Wrap(GameBinding.Inventory);

                /// <summary>取得贵重物品栏的 <see cref="API.GameStorage"/> 实例。</summary>
                public static GameStorage Precious => GameStorage.Wrap(GameBinding.PreciousStorage);

                /// <summary>取得强化物品栏的 <see cref="API.GameStorage"/> 实例。</summary>
                public static GameStorage Enhancer => GameStorage.Wrap(GameBinding.EnhancerStorage);

                /// <summary>取得住宅仓库的 <see cref="API.GameStorage"/> 实例。</summary>
                public static GameStorage House => GameStorage.Wrap(GameBinding.HouseStorage);
            }

            /// <summary>游戏内 ESC 菜单。</summary>
            public static class Menu
            {
                /// <summary>取得当前游戏菜单实例；菜单未打开时返回 <c>null</c>。</summary>
                public static GameMenu Current
                {
                    get
                    {
                        GameMenu menu = API.GameMenu.Wrap(GameBinding.Menu);
                        return menu != null && menu.IsValid ? menu : null;
                    }
                }

                /// <summary>打开游戏菜单并返回实例；菜单已开着时直接返回当前实例，不重复打开。</summary>
                public static GameMenu Open()
                {
                    nel.gm.UiGameMenu native = GameBinding.Menu;
                    if (native == null)
                    {
                        throw new InvalidOperationException("The game menu is not available yet; the world has not been entered.");
                    }

                    GameMenu existing = Current;
                    if (existing != null)
                    {
                        return existing;
                    }

                    try
                    {
                        native.activate();
                    }
                    catch (Exception ex)
                    {
                        Errors.Report(ex, "Game.Menu.Open");
                        throw new InvalidOperationException("The game refused to open the menu.", ex);
                    }

                    return API.GameMenu.Wrap(native);
                }
            }

            /// <summary>剧情事件。</summary>
            public static class Events
            {
                /// <summary>取得当前正在执行的 <see cref="API.GameEvent"/> 实例；没有事件在跑时为 <c>null</c>。</summary>
                public static GameEvent Current => GameEventRuntime.Current;

                /// <summary>启动指定事件，返回新的 <see cref="API.GameEvent"/> 实例。</summary>
                public static GameEvent Start(string eventKey)
                {
                    if (string.IsNullOrEmpty(eventKey))
                    {
                        throw new ArgumentException("Event key cannot be empty.", nameof(eventKey));
                    }

                    try
                    {
                        evt.EV.stack(eventKey, 0, -1, null, null);
                        evt.EV.evStart();
                    }
                    catch (Exception ex)
                    {
                        Errors.Report(ex, "Game.Events.Start");
                        throw new InvalidOperationException($"The game refused to start the event: {eventKey}.", ex);
                    }

                    return GameEvent.Wrap(eventKey);
                }

                /// <summary>切换到指定事件，返回新的 <see cref="API.GameEvent"/> 实例。</summary>
                public static GameEvent Change(string eventKey)
                {
                    if (string.IsNullOrEmpty(eventKey))
                    {
                        throw new ArgumentException("Event key cannot be empty.", nameof(eventKey));
                    }

                    try
                    {
                        evt.EV.changeEvent(eventKey, 0, null);
                    }
                    catch (Exception ex)
                    {
                        Errors.Report(ex, "Game.Events.Change");
                        throw new InvalidOperationException($"The game refused to change to the event: {eventKey}.", ex);
                    }

                    return GameEvent.Wrap(eventKey);
                }
            }

            /// <summary>任务。</summary>
            public static class Quests
            {
                /// <summary>按任务键名取得 <see cref="API.GameQuest"/> 实例；本版本没有这个任务时返回 <c>null</c>。</summary>
                public static GameQuest Get(string questKey) => GameQuest.Resolve(questKey);

                /// <summary>获取当前任务列表表头的进度摘要；追踪列表为空时为 <c>null</c>。</summary>
                public static GameQuestProgressView Head
                {
                    get
                    {
                        QuestTracker tracker = GameBinding.Quests;
                        if (tracker == null)
                        {
                            return null;
                        }

                        try
                        {
                            QuestTracker.QuestProgress head = tracker.getHeadQuest();
                            string key = head?.Q?.key;
                            if (string.IsNullOrEmpty(key))
                            {
                                return null;
                            }

                            GameQuest quest = GameQuest.Wrap(key);
                            GameQuestProgress progress = quest?.GetProgress();
                            return progress == null ? null : new GameQuestProgressView(quest, progress.Phase, progress.Finished);
                        }
                        catch (Exception)
                        {
                            return null;
                        }
                    }
                }
            }

            /// <summary>
            /// 金钱。<c>Add</c> 与 <c>Spend</c> 刻意分开而不是一个带正负号的方法：
            /// "付不起"是调用方必须处理的正常分支，不是一次失败。
            /// </summary>
            public static class Economy
            {
                /// <summary>获取单种货币在游戏中的最大持有量。</summary>
                public static uint MaxAmount => Safe(static () => CoinStorage.MAX_COUNT, 0u);

                /// <summary>获取指定货币的当前持有量。</summary>
                public static uint GetAmount(GameCurrency currency)
                    => Safe(() => (uint)CoinStorage.getCount((CoinStorage.CTYPE)(int)currency), 0u);

                /// <summary>增加指定货币并返回变动后的余额。<paramref name="amount"/> 非正时不做任何事。</summary>
                public static uint Add(GameCurrency currency, int amount)
                {
                    if (amount <= 0)
                    {
                        return GetAmount(currency);
                    }

                    try
                    {
                        CoinStorage.addCount(amount, (CoinStorage.CTYPE)(int)currency);
                    }
                    catch (Exception ex)
                    {
                        Errors.Report(ex, "Game.Economy.Add");
                    }

                    return GetAmount(currency);
                }

                /// <summary>扣除指定货币并返回是否成功；余额不足时一分不扣并返回 <c>false</c>，避免部分扣款无法回滚。</summary>
                public static bool Spend(GameCurrency currency, int amount)
                {
                    if (amount <= 0)
                    {
                        return false;
                    }

                    if (GetAmount(currency) < (uint)amount)
                    {
                        return false;
                    }

                    try
                    {
                        CoinStorage.reduceCount(amount, (CoinStorage.CTYPE)(int)currency);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Errors.Report(ex, "Game.Economy.Spend");
                        return false;
                    }
                }
            }

            /// <summary>音效与音量。背景音乐在 <see cref="Bgm"/>。</summary>
            public static class Audio
            {
                /// <summary>判断音频系统是否已经初始化完成。</summary>
                public static bool IsReady => Safe(static () => SND.loaded, false);

                /// <summary>获取或设置音效音量。</summary>
                public static int SfxVolume
                {
                    get => Safe(static () => SND.volume, 0);
                    set => WriteVolume(GameVolumeChannel.Sfx, value, static v => SND.volume = v);
                }

                /// <summary>获取或设置语音音量。</summary>
                public static int VoiceVolume
                {
                    get => Safe(static () => SND.voice_volume, 0);
                    set => WriteVolume(GameVolumeChannel.Voice, value, static v => SND.voice_volume = v);
                }

                /// <summary>获取或设置背景音乐音量。</summary>
                public static int BgmVolume
                {
                    get => Safe(static () => SND.bgm_volume, 0);
                    set => WriteVolume(GameVolumeChannel.Bgm, value, static v => SND.bgm_volume = v);
                }

                /// <summary>获取或设置主音量。</summary>
                public static int MasterVolume
                {
                    get => Safe(static () => SND.master_volume, 0);
                    set => WriteVolume(GameVolumeChannel.Master, value, static v => SND.master_volume = v);
                }

                /// <summary>播放指定音效并返回可控制的实例；播放失败或并发音效已达上限时返回 <c>null</c>。</summary>
                public static GameAudioPlayback Play(string cue, bool loop = false)
                    => GameAudioRuntime.Play(cue, loop);

                /// <summary>背景音乐。</summary>
                public static class Bgm
                {
                    /// <summary>加载指定背景音乐资源。<paramref name="timing"/> 是它所属的音频 sheet。</summary>
                    public static void Load(string timing, string cue)
                    {
                        if (string.IsNullOrEmpty(cue))
                        {
                            return;
                        }

                        Guard("Load", () => BGM.load(timing, cue, true));
                    }

                    /// <summary>开始播放已加载的背景音乐。</summary>
                    public static void Play() => Guard("Play", static () => BGM.play(0f));

                    /// <summary>停止当前背景音乐。</summary>
                    public static void Stop() => Guard("Stop", static () => BGM.stop(false, true));

                    /// <summary>让当前背景音乐渐入播放。</summary>
                    public static void FadeIn(float seconds) => Guard("FadeIn", () => BGM.fadein(100f, ToFrames(seconds)));

                    /// <summary>让当前背景音乐渐出停止。</summary>
                    public static void FadeOut(float seconds) => Guard("FadeOut", () => BGM.fadeout(0f, ToFrames(seconds), true));

                    /// <summary>把当前背景音乐替换为指定曲目；<paramref name="immediate"/> 为真时不做淡入淡出，直接切换。</summary>
                    public static void Replace(string timing, string cue, bool immediate = false)
                    {
                        if (string.IsNullOrEmpty(cue))
                        {
                            return;
                        }

                        Guard("Replace", () =>
                        {
                            BGM.load(timing, cue, true);
                            float fade = immediate ? 0f : DefaultFadeFrames;
                            BGM.replace(fade, fade, true, true);
                        });
                    }

                    /// <summary>判断背景音乐当前是否正在播放。</summary>
                    public static bool IsPlaying() => Safe(static () => BGM.isFrontPlaying(), false);

                    /// <summary>获取当前前台背景音乐的曲目信息；没有曲目时为 <c>null</c>。</summary>
                    public static GameBgmTrack CurrentTrack => Safe(
                        static () =>
                        {
                            BGM.getFrontBgm(out string timing, out string cue);
                            return string.IsNullOrEmpty(cue) ? null : new GameBgmTrack(timing, cue);
                        },
                        null);

                    /// <summary>不指定时长时的淡入淡出帧数（2 秒）。</summary>
                    const float DefaultFadeFrames = 120f;

                    /// <summary>游戏的淡入淡出参数以帧计（60 帧 = 1 秒）；公开签名用秒，在这里换算一次。</summary>
                    static float ToFrames(float seconds) => seconds <= 0f ? 0f : seconds * 60f;

                    static void Guard(string what, Action action)
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            Errors.Report(ex, $"Game.Audio.Bgm.{what}");
                        }
                    }
                }

                static void WriteVolume(GameVolumeChannel channel, int value, Action<int> write)
                {
                    try
                    {
                        write(value);
                    }
                    catch (Exception ex)
                    {
                        Errors.Report(ex, $"Game.Audio.{channel}Volume");
                    }
                }
            }

            /// <summary>全局静态回调的注册入口。实例回调注册在对应实例自己身上。</summary>
            public static class Callbacks
            {
                /// <summary>
                /// 注册全局静态回调；返回的句柄不会自动回收，需在模组卸载时显式 <see cref="GameCallbackRegistration.Dispose"/>。
                /// <typeparamref name="TData"/> 与 <paramref name="kind"/> 不匹配时抛 <see cref="ArgumentException"/>。
                /// </summary>
                public static GameCallbackRegistration Register<TData>(
                    GameStaticCallbackKind kind, Action<TData> callback, GameCallbackOptions options = default)
                    where TData : GameCallbackData
                {
                    if (callback == null)
                    {
                        throw new ArgumentNullException(nameof(callback));
                    }

                    GameCallbackContract.EnsureStatic<TData>(kind);
                    return GameCallbackHub.RegisterStatic(kind, callback, options);
                }
            }
        }
    }
}
