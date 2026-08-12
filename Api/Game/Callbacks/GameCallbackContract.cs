using System;
using System.Collections.Generic;

namespace Polaris.API
{
    /// <summary>"回调种类 ↔ 负荷类型"的唯一真相表，让 <c>Register&lt;TData&gt;</c> 在注册时就能否掉类型不匹配的调用（而非等到派发时才报错，那时错误已定位不到注册代码）。</summary>
    internal static class GameCallbackContract
    {
        static readonly Dictionary<GameStaticCallbackKind, Type> StaticData = new()
        {
            [GameStaticCallbackKind.GameSceneStarted] = typeof(GameSceneStartedCallbackData),
            [GameStaticCallbackKind.NewGameStarted] = typeof(NewGameStartedCallbackData),
            [GameStaticCallbackKind.SaveLoaded] = typeof(SaveLoadedCallbackData),
            [GameStaticCallbackKind.SaveFailed] = typeof(SaveFailedCallbackData),
            [GameStaticCallbackKind.SaveSerialized] = typeof(SaveSerializedCallbackData),
            [GameStaticCallbackKind.SaveWritten] = typeof(SaveWrittenCallbackData),
            [GameStaticCallbackKind.AutoSaveCompleted] = typeof(AutoSaveCompletedCallbackData),
            [GameStaticCallbackKind.LocaleChanged] = typeof(LocaleChangedCallbackData),
            [GameStaticCallbackKind.ActionPressed] = typeof(ActionPressedCallbackData),
            [GameStaticCallbackKind.ActionReleased] = typeof(ActionReleasedCallbackData),
            [GameStaticCallbackKind.MapChanged] = typeof(MapChangedCallbackData),
            [GameStaticCallbackKind.MapOpened] = typeof(MapOpenedCallbackData),
            [GameStaticCallbackKind.DayNightChanged] = typeof(DayNightChangedCallbackData),
            [GameStaticCallbackKind.NightLevelChanged] = typeof(NightLevelChangedCallbackData),
            [GameStaticCallbackKind.DangerLevelChanged] = typeof(DangerLevelChangedCallbackData),
            [GameStaticCallbackKind.WeatherChanged] = typeof(WeatherChangedCallbackData),
            [GameStaticCallbackKind.EventOpened] = typeof(EventOpenedCallbackData),
            [GameStaticCallbackKind.ItemObtained] = typeof(ItemObtainedCallbackData),
            [GameStaticCallbackKind.DropCreated] = typeof(DropCreatedCallbackData),
            [GameStaticCallbackKind.MoneyChanged] = typeof(MoneyChangedCallbackData),
            [GameStaticCallbackKind.QuestStarted] = typeof(QuestChangedCallbackData),
            [GameStaticCallbackKind.FocusedQuestChanged] = typeof(FocusedQuestChangedCallbackData),
            [GameStaticCallbackKind.StoryFlagChanged] = typeof(StoryFlagChangedCallbackData),
            [GameStaticCallbackKind.GameMenuOpened] = typeof(GameMenuOpenedCallbackData),
            [GameStaticCallbackKind.MusicChanged] = typeof(MusicChangedCallbackData),
            [GameStaticCallbackKind.MusicPlaybackChanged] = typeof(MusicPlaybackChangedCallbackData),
            [GameStaticCallbackKind.SoundPlayed] = typeof(SoundPlayedCallbackData),
            [GameStaticCallbackKind.VolumeChanged] = typeof(VolumeChangedCallbackData),
        };

        static readonly Dictionary<GameInstanceCallbackKind, Type> InstanceData = new()
        {
            [GameInstanceCallbackKind.MapClosed] = typeof(MapClosedCallbackData),
            [GameInstanceCallbackKind.MapActionInitialized] = typeof(MapActionInitializedCallbackData),
            [GameInstanceCallbackKind.MapActionClosed] = typeof(MapActionClosedCallbackData),
            [GameInstanceCallbackKind.EventClosed] = typeof(EventClosedCallbackData),
            [GameInstanceCallbackKind.PlayerStateChanged] = typeof(PlayerStateChangedCallbackData),
            [GameInstanceCallbackKind.PlayerDied] = typeof(PlayerDiedCallbackData),
            [GameInstanceCallbackKind.PlayerRevived] = typeof(PlayerRevivedCallbackData),
            [GameInstanceCallbackKind.EnemyStateChanged] = typeof(EnemyStateChangedCallbackData),
            [GameInstanceCallbackKind.EnemyDied] = typeof(EnemyDiedCallbackData),
            [GameInstanceCallbackKind.KnockbackApplied] = typeof(KnockbackAppliedCallbackData),
            [GameInstanceCallbackKind.StatusAdded] = typeof(StatusChangedCallbackData),
            [GameInstanceCallbackKind.StatusRefreshed] = typeof(StatusChangedCallbackData),
            [GameInstanceCallbackKind.StatusRemoved] = typeof(StatusChangedCallbackData),
            [GameInstanceCallbackKind.DamageApplied] = typeof(DamageAppliedCallbackData),
            [GameInstanceCallbackKind.HpDamageApplied] = typeof(HpDamageAppliedCallbackData),
            [GameInstanceCallbackKind.MpDamageApplied] = typeof(MpDamageAppliedCallbackData),
            [GameInstanceCallbackKind.RecoveryApplied] = typeof(RecoveryAppliedCallbackData),
            [GameInstanceCallbackKind.ItemAdded] = typeof(InventoryChangedCallbackData),
            [GameInstanceCallbackKind.ItemRemoved] = typeof(InventoryChangedCallbackData),
            [GameInstanceCallbackKind.ItemsTransferred] = typeof(ItemsTransferredCallbackData),
            [GameInstanceCallbackKind.StorageCleared] = typeof(StorageClearedCallbackData),
            [GameInstanceCallbackKind.ItemUsed] = typeof(ItemUsedCallbackData),
            [GameInstanceCallbackKind.QuestUpdated] = typeof(QuestChangedCallbackData),
            [GameInstanceCallbackKind.QuestCompleted] = typeof(QuestChangedCallbackData),
            [GameInstanceCallbackKind.QuestRemoved] = typeof(QuestRemovedCallbackData),
            [GameInstanceCallbackKind.GameMenuClosed] = typeof(GameMenuClosedCallbackData),
        };

        /// <summary>实例回调允许挂在哪一种实例上，用于在注册时否掉类型不匹配的挂载（否则会安静地永远收不到事件）。</summary>
        static readonly Dictionary<GameInstanceCallbackKind, Type> InstanceOwner = new()
        {
            [GameInstanceCallbackKind.MapClosed] = typeof(GameMap),
            [GameInstanceCallbackKind.MapActionInitialized] = typeof(GameMap),
            [GameInstanceCallbackKind.MapActionClosed] = typeof(GameMap),
            [GameInstanceCallbackKind.EventClosed] = typeof(GameEvent),
            [GameInstanceCallbackKind.PlayerStateChanged] = typeof(GamePlayer),
            [GameInstanceCallbackKind.PlayerDied] = typeof(GamePlayer),
            [GameInstanceCallbackKind.PlayerRevived] = typeof(GamePlayer),
            [GameInstanceCallbackKind.EnemyStateChanged] = typeof(GameEnemy),
            [GameInstanceCallbackKind.EnemyDied] = typeof(GameEnemy),
            [GameInstanceCallbackKind.KnockbackApplied] = typeof(GameCharacter),
            [GameInstanceCallbackKind.StatusAdded] = typeof(GameCharacter),
            [GameInstanceCallbackKind.StatusRefreshed] = typeof(GameCharacter),
            [GameInstanceCallbackKind.StatusRemoved] = typeof(GameCharacter),
            [GameInstanceCallbackKind.DamageApplied] = typeof(GameCharacter),
            [GameInstanceCallbackKind.HpDamageApplied] = typeof(GameCharacter),
            [GameInstanceCallbackKind.MpDamageApplied] = typeof(GameCharacter),
            [GameInstanceCallbackKind.RecoveryApplied] = typeof(GameCharacter),
            [GameInstanceCallbackKind.ItemAdded] = typeof(GameStorage),
            [GameInstanceCallbackKind.ItemRemoved] = typeof(GameStorage),
            [GameInstanceCallbackKind.ItemsTransferred] = typeof(GameStorage),
            [GameInstanceCallbackKind.StorageCleared] = typeof(GameStorage),
            [GameInstanceCallbackKind.ItemUsed] = typeof(GameItem),
            [GameInstanceCallbackKind.QuestUpdated] = typeof(GameQuest),
            [GameInstanceCallbackKind.QuestCompleted] = typeof(GameQuest),
            [GameInstanceCallbackKind.QuestRemoved] = typeof(GameQuest),
            [GameInstanceCallbackKind.GameMenuClosed] = typeof(GameMenu),
        };

        internal static void EnsureStatic<TData>(GameStaticCallbackKind kind) where TData : GameCallbackData
        {
            if (!StaticData.TryGetValue(kind, out Type expected))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown static callback kind.");
            }

            if (expected != typeof(TData))
            {
                throw new ArgumentException(
                    $"Callback kind {kind} delivers {expected.Name}, but the registration asked for {typeof(TData).Name}.",
                    nameof(kind));
            }
        }

        internal static void EnsureInstance<TData>(GameInstanceCallbackKind kind, GameInstance owner) where TData : GameCallbackData
        {
            if (!InstanceData.TryGetValue(kind, out Type expected))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown instance callback kind.");
            }

            if (expected != typeof(TData))
            {
                throw new ArgumentException(
                    $"Callback kind {kind} delivers {expected.Name}, but the registration asked for {typeof(TData).Name}.",
                    nameof(kind));
            }

            Type ownerType = InstanceOwner[kind];
            if (!ownerType.IsInstanceOfType(owner))
            {
                throw new ArgumentException(
                    $"Callback kind {kind} can only be registered on a {ownerType.Name}, not on a {owner.GetType().Name}.",
                    nameof(kind));
            }
        }
    }
}
