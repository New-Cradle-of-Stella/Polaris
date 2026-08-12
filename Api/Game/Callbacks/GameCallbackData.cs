namespace Polaris.API
{
    /// <summary>
    /// 所有回调负荷的公共基类。存在的意义有两个：让回调注册表能在编译期把
    /// <c>TData</c> 约束成"确实是一种回调数据"，以及给每一条事件带上统一的帧号，
    /// 免得每个订阅者各自去读 <c>Time.frameCount</c> 再猜自己读到的是不是事件发生的那一帧。
    /// </summary>
    public abstract class GameCallbackData
    {
        private protected GameCallbackData()
        {
            Frame = SafeFrame();
        }

        /// <summary>事件<b>发生</b>时的 Unity 帧号（不是派发给订阅者的那一帧）。</summary>
        public int Frame { get; }

        static int SafeFrame()
        {
            try
            {
                return UnityEngine.Time.frameCount;
            }
            catch (System.Exception)
            {
                return 0;
            }
        }
    }

    // ── 静态回调负荷 ───────────────────────────────────────────────────────────

    /// <summary>游戏场景完成初始化。</summary>
    public sealed class GameSceneStartedCallbackData : GameCallbackData
    {
        internal GameSceneStartedCallbackData(bool fromSave)
        {
            FromSave = fromSave;
        }

        /// <summary>这次进入场景是不是由读档触发（否则是新游戏）。</summary>
        public bool FromSave { get; }
    }

    /// <summary>新游戏初始化完成。</summary>
    public sealed class NewGameStartedCallbackData : GameCallbackData
    {
        internal NewGameStartedCallbackData() { }
    }

    /// <summary>存档成功读取并应用。</summary>
    public sealed class SaveLoadedCallbackData : GameCallbackData
    {
        internal SaveLoadedCallbackData(int slot)
        {
            Slot = slot;
        }

        /// <summary>存档槽位；游戏没有给出槽位信息时为 -1。</summary>
        public int Slot { get; }
    }

    /// <summary>存档读取失败。</summary>
    public sealed class SaveFailedCallbackData : GameCallbackData
    {
        internal SaveFailedCallbackData(int slot, string reason)
        {
            Slot = slot;
            Reason = reason;
        }

        /// <summary>存档槽位；未知时为 -1。</summary>
        public int Slot { get; }

        /// <summary>失败原因的一句话描述；游戏没有给出原因时为 <c>null</c>。</summary>
        public string Reason { get; }
    }

    /// <summary>存档数据在内存中序列化完成。</summary>
    public sealed class SaveSerializedCallbackData : GameCallbackData
    {
        internal SaveSerializedCallbackData(int byteCount)
        {
            ByteCount = byteCount;
        }

        /// <summary>序列化结果的字节数；未知时为 0。</summary>
        public int ByteCount { get; }
    }

    /// <summary>存档文件写入完成。</summary>
    public sealed class SaveWrittenCallbackData : GameCallbackData
    {
        internal SaveWrittenCallbackData(int slot, bool succeeded)
        {
            Slot = slot;
            Succeeded = succeeded;
        }

        /// <summary>存档槽位；未知时为 -1。</summary>
        public int Slot { get; }

        public bool Succeeded { get; }
    }

    /// <summary>自动保存流程结束。</summary>
    public sealed class AutoSaveCompletedCallbackData : GameCallbackData
    {
        internal AutoSaveCompletedCallbackData(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }

    /// <summary>游戏语言切换完成。</summary>
    public sealed class LocaleChangedCallbackData : GameCallbackData
    {
        internal LocaleChangedCallbackData(string previous, string current)
        {
            Previous = previous;
            Current = current;
        }

        /// <summary>切换前的语言代码；首次探到语言时为 <c>null</c>。</summary>
        public string Previous { get; }

        public string Current { get; }
    }

    /// <summary>某个输入动作被按下。</summary>
    public sealed class ActionPressedCallbackData : GameCallbackData
    {
        internal ActionPressedCallbackData(GameInputAction action)
        {
            Action = action;
        }

        public GameInputAction Action { get; }
    }

    /// <summary>某个输入动作被释放。</summary>
    public sealed class ActionReleasedCallbackData : GameCallbackData
    {
        internal ActionReleasedCallbackData(GameInputAction action, int heldFrames)
        {
            Action = action;
            HeldFrames = heldFrames;
        }

        public GameInputAction Action { get; }

        /// <summary>这次按住持续了多少帧。</summary>
        public int HeldFrames { get; }
    }

    /// <summary>当前地图切换完成。</summary>
    public sealed class MapChangedCallbackData : GameCallbackData
    {
        internal MapChangedCallbackData(string previousKey, GameMap current)
        {
            PreviousKey = previousKey;
            Current = current;
        }

        /// <summary>切换前的地图 key；此前没有地图时为 <c>null</c>。</summary>
        public string PreviousKey { get; }

        /// <summary>切换后的地图实例；切成"没有地图"时为 <c>null</c>。</summary>
        public GameMap Current { get; }
    }

    /// <summary>某张地图打开完成。</summary>
    public sealed class MapOpenedCallbackData : GameCallbackData
    {
        internal MapOpenedCallbackData(GameMap map)
        {
            Map = map;
        }

        public GameMap Map { get; }
    }

    /// <summary>昼夜状态变化。</summary>
    public sealed class DayNightChangedCallbackData : GameCallbackData
    {
        internal DayNightChangedCallbackData(bool isNight)
        {
            IsNight = isNight;
        }

        public bool IsNight { get; }
    }

    /// <summary>夜晚等级变化。</summary>
    public sealed class NightLevelChangedCallbackData : GameCallbackData
    {
        internal NightLevelChangedCallbackData(float previous, float current)
        {
            Previous = previous;
            Current = current;
        }

        public float Previous { get; }

        public float Current { get; }
    }

    /// <summary>危险度变化。</summary>
    public sealed class DangerLevelChangedCallbackData : GameCallbackData
    {
        internal DangerLevelChangedCallbackData(float previous, float current)
        {
            Previous = previous;
            Current = current;
        }

        public float Previous { get; }

        public float Current { get; }
    }

    /// <summary>当前天气组合变化。</summary>
    public sealed class WeatherChangedCallbackData : GameCallbackData
    {
        internal WeatherChangedCallbackData(int previousBits, int currentBits)
        {
            PreviousBits = previousBits;
            CurrentBits = currentBits;
        }

        /// <summary>变化前的天气位掩码，逐位对应 <see cref="GameWeather"/>。</summary>
        public int PreviousBits { get; }

        /// <summary>变化后的天气位掩码。</summary>
        public int CurrentBits { get; }

        /// <summary>变化后是否包含指定天气。</summary>
        public bool Has(GameWeather weather) => (CurrentBits & (1 << (int)weather)) != 0;
    }

    /// <summary>某个事件成功打开。</summary>
    public sealed class EventOpenedCallbackData : GameCallbackData
    {
        internal EventOpenedCallbackData(GameEvent gameEvent)
        {
            Event = gameEvent;
        }

        public GameEvent Event { get; }
    }

    /// <summary>某件物品被记录为已获得。</summary>
    public sealed class ItemObtainedCallbackData : GameCallbackData
    {
        internal ItemObtainedCallbackData(GameItem item, int count, int grade)
        {
            Item = item;
            Count = count;
            Grade = grade;
        }

        public GameItem Item { get; }

        public int Count { get; }

        public int Grade { get; }
    }

    /// <summary>地图上生成了掉落物。</summary>
    public sealed class DropCreatedCallbackData : GameCallbackData
    {
        internal DropCreatedCallbackData(GameDrop drop)
        {
            Drop = drop;
        }

        public GameDrop Drop { get; }
    }

    /// <summary>某种货币余额实际变化。</summary>
    public sealed class MoneyChangedCallbackData : GameCallbackData
    {
        internal MoneyChangedCallbackData(GameCurrency currency, uint previous, uint current)
        {
            Currency = currency;
            Previous = previous;
            Current = current;
        }

        public GameCurrency Currency { get; }

        public uint Previous { get; }

        public uint Current { get; }

        /// <summary>本次变化量（可为负）。</summary>
        public long Delta => (long)Current - Previous;
    }

    /// <summary>
    /// 任务阶段发生变化。<see cref="GameStaticCallbackKind.QuestStarted"/> 与实例侧的
    /// <see cref="GameInstanceCallbackKind.QuestUpdated"/>/<see cref="GameInstanceCallbackKind.QuestCompleted"/>
    /// 共用这一种负荷。
    /// </summary>
    public sealed class QuestChangedCallbackData : GameCallbackData
    {
        internal QuestChangedCallbackData(GameQuest quest, int previousPhase, int currentPhase)
        {
            Quest = quest;
            PreviousPhase = previousPhase;
            CurrentPhase = currentPhase;
        }

        public GameQuest Quest { get; }

        /// <summary>变化前的阶段；此前不在追踪列表里时为 -1。</summary>
        public int PreviousPhase { get; }

        public int CurrentPhase { get; }
    }

    /// <summary>重点追踪任务变化。</summary>
    public sealed class FocusedQuestChangedCallbackData : GameCallbackData
    {
        internal FocusedQuestChangedCallbackData(GameQuest quest)
        {
            Quest = quest;
        }

        /// <summary>新的重点任务；被清空时为 <c>null</c>。</summary>
        public GameQuest Quest { get; }
    }

    /// <summary>剧情旗标值实际变化。</summary>
    public sealed class StoryFlagChangedCallbackData : GameCallbackData
    {
        internal StoryFlagChangedCallbackData(string key, int previous, int current)
        {
            Key = key;
            Previous = previous;
            Current = current;
        }

        public string Key { get; }

        public int Previous { get; }

        public int Current { get; }
    }

    /// <summary>游戏菜单打开完成。</summary>
    public sealed class GameMenuOpenedCallbackData : GameCallbackData
    {
        internal GameMenuOpenedCallbackData(GameMenu menu)
        {
            Menu = menu;
        }

        public GameMenu Menu { get; }
    }

    /// <summary>当前背景音乐曲目变化。</summary>
    public sealed class MusicChangedCallbackData : GameCallbackData
    {
        internal MusicChangedCallbackData(GameBgmTrack previous, GameBgmTrack current)
        {
            Previous = previous;
            Current = current;
        }

        /// <summary>变化前的曲目；此前没有曲目时为 <c>null</c>。</summary>
        public GameBgmTrack Previous { get; }

        /// <summary>变化后的曲目；变成"没有曲目"时为 <c>null</c>。</summary>
        public GameBgmTrack Current { get; }
    }

    /// <summary>背景音乐播放/停止状态变化。</summary>
    public sealed class MusicPlaybackChangedCallbackData : GameCallbackData
    {
        internal MusicPlaybackChangedCallbackData(bool isPlaying)
        {
            IsPlaying = isPlaying;
        }

        public bool IsPlaying { get; }
    }

    /// <summary>某个音效成功开始播放。</summary>
    public sealed class SoundPlayedCallbackData : GameCallbackData
    {
        internal SoundPlayedCallbackData(string cue, GameAudioPlayback playback)
        {
            Cue = cue;
            Playback = playback;
        }

        public string Cue { get; }

        public GameAudioPlayback Playback { get; }
    }

    /// <summary>某个音量通道数值变化。</summary>
    public sealed class VolumeChangedCallbackData : GameCallbackData
    {
        internal VolumeChangedCallbackData(GameVolumeChannel channel, int previous, int current)
        {
            Channel = channel;
            Previous = previous;
            Current = current;
        }

        public GameVolumeChannel Channel { get; }

        public int Previous { get; }

        public int Current { get; }
    }

    /// <summary>音量通道。</summary>
    public enum GameVolumeChannel
    {
        Master,
        Sfx,
        Voice,
        Bgm,
    }

    // ── 实例回调负荷 ───────────────────────────────────────────────────────────

    /// <summary>地图实例关闭完成。</summary>
    public sealed class MapClosedCallbackData : GameCallbackData
    {
        internal MapClosedCallbackData(GameMap map)
        {
            Map = map;
        }

        public GameMap Map { get; }
    }

    /// <summary>地图实例的动作逻辑初始化完成。</summary>
    public sealed class MapActionInitializedCallbackData : GameCallbackData
    {
        internal MapActionInitializedCallbackData(GameMap map)
        {
            Map = map;
        }

        public GameMap Map { get; }
    }

    /// <summary>地图实例的动作逻辑关闭完成。</summary>
    public sealed class MapActionClosedCallbackData : GameCallbackData
    {
        internal MapActionClosedCallbackData(GameMap map)
        {
            Map = map;
        }

        public GameMap Map { get; }
    }

    /// <summary>事件实例成功关闭。</summary>
    public sealed class EventClosedCallbackData : GameCallbackData
    {
        internal EventClosedCallbackData(GameEvent gameEvent, bool completed)
        {
            Event = gameEvent;
            Completed = completed;
        }

        public GameEvent Event { get; }

        /// <summary>是正常演完（<c>true</c>）还是被中途停止（<c>false</c>）。</summary>
        public bool Completed { get; }
    }

    /// <summary>玩家实例状态变化。</summary>
    public sealed class PlayerStateChangedCallbackData : GameCallbackData
    {
        internal PlayerStateChangedCallbackData(GamePlayer player, GamePlayerState previous, GamePlayerState current)
        {
            Player = player;
            Previous = previous;
            Current = current;
        }

        public GamePlayer Player { get; }

        public GamePlayerState Previous { get; }

        public GamePlayerState Current { get; }
    }

    /// <summary>玩家实例首次进入死亡状态。</summary>
    public sealed class PlayerDiedCallbackData : GameCallbackData
    {
        internal PlayerDiedCallbackData(GamePlayer player)
        {
            Player = player;
        }

        public GamePlayer Player { get; }
    }

    /// <summary>玩家实例从死亡状态恢复。</summary>
    public sealed class PlayerRevivedCallbackData : GameCallbackData
    {
        internal PlayerRevivedCallbackData(GamePlayer player)
        {
            Player = player;
        }

        public GamePlayer Player { get; }
    }

    /// <summary>敌人实例状态变化。</summary>
    public sealed class EnemyStateChangedCallbackData : GameCallbackData
    {
        internal EnemyStateChangedCallbackData(GameEnemy enemy, GameEnemyState previous, GameEnemyState current)
        {
            Enemy = enemy;
            Previous = previous;
            Current = current;
        }

        public GameEnemy Enemy { get; }

        public GameEnemyState Previous { get; }

        public GameEnemyState Current { get; }
    }

    /// <summary>敌人实例首次进入死亡状态。</summary>
    public sealed class EnemyDiedCallbackData : GameCallbackData
    {
        internal EnemyDiedCallbackData(GameEnemy enemy)
        {
            Enemy = enemy;
        }

        public GameEnemy Enemy { get; }
    }

    /// <summary>角色实例被施加击退速度。</summary>
    public sealed class KnockbackAppliedCallbackData : GameCallbackData
    {
        internal KnockbackAppliedCallbackData(GameCharacter character, float velocity)
        {
            Character = character;
            Velocity = velocity;
        }

        public GameCharacter Character { get; }

        /// <summary>本次施加的击退初速度。</summary>
        public float Velocity { get; }
    }

    /// <summary>
    /// 角色实例的状态效果变化。<see cref="GameInstanceCallbackKind.StatusAdded"/>、
    /// <see cref="GameInstanceCallbackKind.StatusRefreshed"/> 与
    /// <see cref="GameInstanceCallbackKind.StatusRemoved"/> 共用这一种负荷。
    /// </summary>
    public sealed class StatusChangedCallbackData : GameCallbackData
    {
        internal StatusChangedCallbackData(GameCharacter character, int statusId)
        {
            Character = character;
            StatusId = statusId;
        }

        public GameCharacter Character { get; }

        /// <summary>状态效果在游戏内部的编号。</summary>
        public int StatusId { get; }
    }

    /// <summary>角色实例的一次伤害结算完成。</summary>
    public sealed class DamageAppliedCallbackData : GameCallbackData
    {
        internal DamageAppliedCallbackData(GameCharacter character, int hpDealt, int mpDealt)
        {
            Character = character;
            HpDealt = hpDealt;
            MpDealt = mpDealt;
        }

        public GameCharacter Character { get; }

        public int HpDealt { get; }

        public int MpDealt { get; }
    }

    /// <summary>角色实例实际损失体力值。</summary>
    public sealed class HpDamageAppliedCallbackData : GameCallbackData
    {
        internal HpDamageAppliedCallbackData(GameCharacter character, int amount, int hpAfter)
        {
            Character = character;
            Amount = amount;
            HpAfter = hpAfter;
        }

        public GameCharacter Character { get; }

        /// <summary>实际扣掉的体力值（已经过抗性、护盾与无敌帧裁剪）。</summary>
        public int Amount { get; }

        public int HpAfter { get; }
    }

    /// <summary>角色实例实际损失魔力值。</summary>
    public sealed class MpDamageAppliedCallbackData : GameCallbackData
    {
        internal MpDamageAppliedCallbackData(GameCharacter character, int amount, int mpAfter)
        {
            Character = character;
            Amount = amount;
            MpAfter = mpAfter;
        }

        public GameCharacter Character { get; }

        public int Amount { get; }

        public int MpAfter { get; }
    }

    /// <summary>角色实例实际恢复体力值或魔力值。</summary>
    public sealed class RecoveryAppliedCallbackData : GameCallbackData
    {
        internal RecoveryAppliedCallbackData(GameCharacter character, int hpRestored, int mpRestored)
        {
            Character = character;
            HpRestored = hpRestored;
            MpRestored = mpRestored;
        }

        public GameCharacter Character { get; }

        public int HpRestored { get; }

        public int MpRestored { get; }
    }

    /// <summary>
    /// 存储实例的物品增减。<see cref="GameInstanceCallbackKind.ItemAdded"/> 与
    /// <see cref="GameInstanceCallbackKind.ItemRemoved"/> 共用这一种负荷。
    /// </summary>
    public sealed class InventoryChangedCallbackData : GameCallbackData
    {
        internal InventoryChangedCallbackData(GameStorage storage, GameItem item, int count, int grade)
        {
            Storage = storage;
            Item = item;
            Count = count;
            Grade = grade;
        }

        public GameStorage Storage { get; }

        public GameItem Item { get; }

        /// <summary><b>实际</b>进出的数量，永远为正；方向看回调种类。</summary>
        public int Count { get; }

        public int Grade { get; }
    }

    /// <summary>存储实例之间的物品转移完成。</summary>
    public sealed class ItemsTransferredCallbackData : GameCallbackData
    {
        internal ItemsTransferredCallbackData(GameStorage source, GameStorage destination)
        {
            Source = source;
            Destination = destination;
        }

        public GameStorage Source { get; }

        public GameStorage Destination { get; }
    }

    /// <summary>非空存储实例被清空。</summary>
    public sealed class StorageClearedCallbackData : GameCallbackData
    {
        internal StorageClearedCallbackData(GameStorage storage, int newCapacityRows)
        {
            Storage = storage;
            NewCapacityRows = newCapacityRows;
        }

        public GameStorage Storage { get; }

        /// <summary>清空后设定的容量行数；未改动容量时为 -1。</summary>
        public int NewCapacityRows { get; }
    }

    /// <summary>物品实例实际被使用。</summary>
    public sealed class ItemUsedCallbackData : GameCallbackData
    {
        internal ItemUsedCallbackData(GameItem item, int grade, int result)
        {
            Item = item;
            Grade = grade;
            Result = result;
        }

        public GameItem Item { get; }

        public int Grade { get; }

        /// <summary>游戏返回的使用结果码；含义由物品自身决定，非零一般表示确实生效。</summary>
        public int Result { get; }
    }

    /// <summary>任务实例从追踪列表移除。</summary>
    public sealed class QuestRemovedCallbackData : GameCallbackData
    {
        internal QuestRemovedCallbackData(GameQuest quest, bool consideredFinished)
        {
            Quest = quest;
            ConsideredFinished = consideredFinished;
        }

        public GameQuest Quest { get; }

        /// <summary>这次移除是不是按"已完成"处理。</summary>
        public bool ConsideredFinished { get; }
    }

    /// <summary>菜单实例关闭完成。</summary>
    public sealed class GameMenuClosedCallbackData : GameCallbackData
    {
        internal GameMenuClosedCallbackData(GameMenu menu)
        {
            Menu = menu;
        }

        public GameMenu Menu { get; }
    }
}
