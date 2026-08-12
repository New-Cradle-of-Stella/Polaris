using m2d;
using nel;

namespace Polaris.API
{
    /// <summary>
    /// Harmony 补丁发布 v2 回调的唯一入口。
    /// <para>
    /// 补丁层刻意不直接碰 <see cref="GameCallbackHub"/>：补丁的职责是"认出这件事发生了"
    /// （那是跟着游戏版本走的知识），把原生对象翻译成包装器、判断有没有人在听、构造负荷
    /// 这些属于回调层，集中在这里一次就够。
    /// </para>
    /// </summary>
    internal static class GameCallbackPublishers
    {
        // ── 角色与战斗 ─────────────────────────────────────────────────────────

        internal static void HpDamage(M2Attackable target, int amount, int hpAfter)
        {
            GameCharacter character = GameCharacter.Wrap(target);
            if (character == null)
            {
                return;
            }

            GameCallbackHub.PublishInstance(
                GameInstanceCallbackKind.HpDamageApplied,
                character,
                () => new HpDamageAppliedCallbackData(character, amount, hpAfter));
        }

        internal static void MpDamage(M2Attackable target, int amount, int mpAfter)
        {
            GameCharacter character = GameCharacter.Wrap(target);
            if (character == null)
            {
                return;
            }

            GameCallbackHub.PublishInstance(
                GameInstanceCallbackKind.MpDamageApplied,
                character,
                () => new MpDamageAppliedCallbackData(character, amount, mpAfter));
        }

        internal static void DamageApplied(M2Attackable target, int hpDealt, int mpDealt)
        {
            GameCharacter character = GameCharacter.Wrap(target);
            if (character == null)
            {
                return;
            }

            GameCallbackHub.PublishInstance(
                GameInstanceCallbackKind.DamageApplied,
                character,
                () => new DamageAppliedCallbackData(character, hpDealt, mpDealt));
        }

        internal static void Recovery(M2Attackable target, int hp, int mp)
            => GameCharacter.PublishRecovery(GameCharacter.Wrap(target), hp, mp);

        internal static void Knockback(M2Attackable target, float velocity)
        {
            GameCharacter character = GameCharacter.Wrap(target);
            if (character == null)
            {
                return;
            }

            GameCallbackHub.PublishInstance(
                GameInstanceCallbackKind.KnockbackApplied,
                character,
                () => new KnockbackAppliedCallbackData(character, velocity));
        }

        internal static void Status(M2Attackable target, GameInstanceCallbackKind kind, int statusId)
        {
            GameCharacter character = GameCharacter.Wrap(target);
            if (character == null)
            {
                return;
            }

            GameCallbackHub.PublishInstance(kind, character, () => new StatusChangedCallbackData(character, statusId));
        }

        // ── 生命周期与存读档 ───────────────────────────────────────────────────

        internal static void GameSceneStarted(bool fromSave)
            => GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.GameSceneStarted, () => new GameSceneStartedCallbackData(fromSave));

        internal static void NewGameStarted()
        {
            // 新游戏会把整个世界重建一遍：上一局的实例包装器全部作废，
            // 否则玩家读了新档之后，旧包装器会安静地指向新世界里的对象。
            GameSessionRuntime.ResetWorld();
            GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.NewGameStarted, () => new NewGameStartedCallbackData());
        }

        internal static void SaveLoaded(int slot)
        {
            GameSessionRuntime.ResetWorld();
            GameCallbackHub.PublishStatic(GameStaticCallbackKind.SaveLoaded, () => new SaveLoadedCallbackData(slot));
        }

        internal static void SaveFailed(int slot, string reason)
            => GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.SaveFailed, () => new SaveFailedCallbackData(slot, reason));

        internal static void SaveSerialized(int byteCount)
            => GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.SaveSerialized, () => new SaveSerializedCallbackData(byteCount));

        internal static void SaveWritten(int slot, bool succeeded)
            => GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.SaveWritten, () => new SaveWrittenCallbackData(slot, succeeded));

        internal static void AutoSaveCompleted(bool succeeded)
            => GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.AutoSaveCompleted, () => new AutoSaveCompletedCallbackData(succeeded));

        // ── 背包与掉落 ─────────────────────────────────────────────────────────

        internal static void ItemAdded(ItemStorage storage, NelItem item, int count, int grade)
            => InventoryChange(GameInstanceCallbackKind.ItemAdded, storage, item, count, grade);

        internal static void ItemRemoved(ItemStorage storage, NelItem item, int count, int grade)
            => InventoryChange(GameInstanceCallbackKind.ItemRemoved, storage, item, count, grade);

        static void InventoryChange(
            GameInstanceCallbackKind kind, ItemStorage storage, NelItem item, int count, int grade)
        {
            // Peek 而不是 Wrap：没人取过的存储容器不值得为一次内部变动新建包装器，
            // 反正也没有订阅者挂在上面。
            GameStorage wrapper = GameStorage.Peek(storage);
            if (wrapper == null || count <= 0)
            {
                return;
            }

            GameItem gameItem = GameItem.Wrap(item);
            if (gameItem == null)
            {
                return;
            }

            GameCallbackHub.PublishInstance(
                kind, wrapper, () => new InventoryChangedCallbackData(wrapper, gameItem, count, grade));
        }

        internal static void ItemsTransferred(ItemStorage source, ItemStorage destination)
        {
            GameStorage from = GameStorage.Peek(source);
            GameStorage to = GameStorage.Peek(destination);
            if (from == null && to == null)
            {
                return;
            }

            if (from != null)
            {
                GameCallbackHub.PublishInstance(
                    GameInstanceCallbackKind.ItemsTransferred, from, () => new ItemsTransferredCallbackData(from, to));
            }

            if (to != null && !ReferenceEquals(from, to))
            {
                GameCallbackHub.PublishInstance(
                    GameInstanceCallbackKind.ItemsTransferred, to, () => new ItemsTransferredCallbackData(from, to));
            }
        }

        internal static void ItemObtained(NelItem item, int count, int grade)
        {
            GameItem gameItem = GameItem.Wrap(item);
            if (gameItem == null || count <= 0)
            {
                return;
            }

            GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.ItemObtained, () => new ItemObtainedCallbackData(gameItem, count, grade));
        }

        internal static void DropCreated(NelItem item, int count, int grade, float x, float y)
        {
            GameItem gameItem = GameItem.Wrap(item);
            if (gameItem == null)
            {
                return;
            }

            GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.DropCreated,
                () => new DropCreatedCallbackData(new GameDrop(gameItem, count, grade, new GameVector2(x, y))));
        }

        internal static void ItemUsed(NelItem item, int grade, int result)
            => GameItem.PublishUsed(GameItem.Wrap(item), grade, result);

        // ── 剧情与任务 ─────────────────────────────────────────────────────────

        internal static void StoryFlagChanged(string key, int previous, int current)
            => GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.StoryFlagChanged, () => new StoryFlagChangedCallbackData(key, previous, current));

        internal static void QuestStarted(string questKey, int phase)
        {
            GameQuest quest = GameQuest.Wrap(questKey);
            if (quest == null)
            {
                return;
            }

            GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.QuestStarted, () => new QuestChangedCallbackData(quest, -1, phase));
        }

        internal static void QuestUpdated(string questKey, int previousPhase, int currentPhase, bool completed)
            => GameQuest.PublishUpdated(GameQuest.Wrap(questKey), previousPhase, currentPhase, completed);

        internal static void QuestRemoved(string questKey, bool consideredFinished)
            => GameQuest.PublishRemoved(GameQuest.Wrap(questKey), consideredFinished);

        // ── 菜单 ───────────────────────────────────────────────────────────────

        internal static void GameMenuOpened(nel.gm.UiGameMenu native)
        {
            GameMenu menu = GameMenu.Wrap(native);
            if (menu == null)
            {
                return;
            }

            GameCallbackHub.PublishStatic(
                GameStaticCallbackKind.GameMenuOpened, () => new GameMenuOpenedCallbackData(menu));
        }

        internal static void GameMenuClosed(nel.gm.UiGameMenu native)
        {
            GameMenu menu = GameMenu.Peek(native);
            if (menu == null)
            {
                return;
            }

            GameCallbackHub.PublishInstance(
                GameInstanceCallbackKind.GameMenuClosed, menu, () => new GameMenuClosedCallbackData(menu));

            GameMenu.Invalidate(native);
        }
    }
}
