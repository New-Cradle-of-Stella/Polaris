using System;
using nel;

namespace Polaris.API
{
    /// <summary>
    /// 一个任务。实例代表任务定义本身而非追踪列表中的一行，移出追踪列表后实例仍有效，
    /// 只是 <see cref="GetProgress"/> 会返回 <c>null</c>。
    /// </summary>
    public sealed class GameQuest : GameInstance
    {
        static readonly InstanceTable<string, GameQuest> Table = new();

        readonly string key;

        GameQuest(string key)
        {
            this.key = key;
        }

        internal static GameQuest Wrap(string questKey)
            => string.IsNullOrEmpty(questKey) ? null : Table.Get(string.Intern(questKey), static k => new GameQuest(k));

        internal static void InvalidateAllQuests() => Table.InvalidateAll();

        /// <summary>按 key 解析；本版本没有这个任务时返回 <c>null</c>。</summary>
        internal static GameQuest Resolve(string questKey)
        {
            if (string.IsNullOrEmpty(questKey))
            {
                return null;
            }

            QuestTracker tracker = GameBinding.Quests;
            if (tracker == null)
            {
                return null;
            }

            try
            {
                // no_error: true——"本版本有没有这个任务"是调用方的正常分支。
                return tracker.Get(questKey, true) == null ? null : Wrap(questKey);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private protected override bool IsNativeAlive => !string.IsNullOrEmpty(key) && GameBinding.Quests != null;

        private protected override string Describe() => $"GameQuest({key})";

        /// <summary>获取该任务的稳定键名。</summary>
        public string Key => key;

        /// <summary>
        /// 获取该任务当前的追踪进度；不在追踪列表里（或只在已完成列表里而
        /// <paramref name="includeFinished"/> 为假）时返回 <c>null</c>。
        /// </summary>
        public GameQuestProgress GetProgress(bool includeFinished = true)
        {
            QuestTracker tracker = GameBinding.Quests;
            if (tracker == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            try
            {
                int phase = tracker.getProgress(key, includeFinished);
                if (phase < 0)
                {
                    return null;
                }

                // 游戏没有直接的 "finished?" 查询，用"只在含已完成时查得到"间接判断。
                bool finished = includeFinished && tracker.getProgress(key, false) < 0;
                return new GameQuestProgress(key, phase, finished);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>更新该任务的阶段与提示选项。</summary>
        public void Update(int phase, QuestUpdateOptions options = default)
        {
            EnsureUsable();

            QuestTracker tracker = GameBinding.Quests;
            if (tracker == null)
            {
                return;
            }

            try
            {
                tracker.updateQuest(key, phase, options.Hidden, options.FillTargetItem, options.SetFocus, true, true);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameQuest.Update");
            }
        }

        /// <summary>把该任务从追踪列表移除。<paramref name="considerFinished"/> 为真时按"已完成"处理。</summary>
        public void Remove(bool considerFinished = true)
        {
            EnsureUsable();

            QuestTracker tracker = GameBinding.Quests;
            if (tracker == null)
            {
                return;
            }

            try
            {
                tracker.remove(key, considerFinished);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameQuest.Remove");
            }
        }

        /// <summary>把该任务设置为当前重点追踪任务。</summary>
        public void SetFocused()
        {
            EnsureUsable();

            QuestTracker tracker = GameBinding.Quests;
            if (tracker == null)
            {
                return;
            }

            try
            {
                QuestTracker.Quest quest = tracker.Get(key, true);
                if (quest == null)
                {
                    return;
                }

                QuestTracker.QuestProgress progress = tracker.getProgressObject(quest, true);
                if (progress != null)
                {
                    tracker.setFocusedQuest(progress);
                }
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameQuest.SetFocused");
            }
        }

        /// <summary>判断指定物品是否是该任务的收集目标。</summary>
        public bool IsTargetItem(GameItem item, int grade = 0)
        {
            QuestTracker tracker = GameBinding.Quests;
            NelItem native = item?.NativeItem;
            if (tracker == null || native == null)
            {
                return false;
            }

            try
            {
                // 游戏只有"是不是任一任务的目标"的查询，故先确认本任务在追踪列表里再问，
                // 避免已交掉的任务因别的任务收同种材料而误判为 true。
                return GetProgress(false) != null && tracker.isQuestTargetItem(native, grade);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>由任务补丁调用，发布"阶段变化 / 完成 / 移除"三条实例回调。</summary>
        internal static void PublishUpdated(GameQuest quest, int previousPhase, int currentPhase, bool completed)
        {
            if (quest == null)
            {
                return;
            }

            GameCallbackHub.PublishInstance(
                GameInstanceCallbackKind.QuestUpdated,
                quest,
                () => new QuestChangedCallbackData(quest, previousPhase, currentPhase));

            if (completed)
            {
                GameCallbackHub.PublishInstance(
                    GameInstanceCallbackKind.QuestCompleted,
                    quest,
                    () => new QuestChangedCallbackData(quest, previousPhase, currentPhase));
            }
        }

        internal static void PublishRemoved(GameQuest quest, bool consideredFinished)
        {
            if (quest == null)
            {
                return;
            }

            GameCallbackHub.PublishInstance(
                GameInstanceCallbackKind.QuestRemoved,
                quest,
                () => new QuestRemovedCallbackData(quest, consideredFinished));
        }
    }
}
