using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>任务、剧情和成长回调。第一批覆盖任务开始/推进/完成/移除/聚焦切换与剧情 flag；
    /// 成就、技能解锁与图鉴击败记录的最终入口还需要进一步 IL 审计，留待后续版本。</summary>
    public sealed class ProgressionCallbacks
    {
        internal ProgressionCallbacks() { }

        static readonly GameSignal<QuestChangedEvent> questStartedSignal = new(GameCallbackKind.QuestStarted);
        static readonly GameSignal<QuestChangedEvent> questUpdatedSignal = new(GameCallbackKind.QuestUpdated);
        static readonly GameSignal<QuestChangedEvent> questCompletedSignal = new(GameCallbackKind.QuestCompleted);
        static readonly GameSignal<QuestRemovedEvent> questRemovedSignal = new(GameCallbackKind.QuestRemoved);
        static readonly GameSignal<FocusedQuestChangedEvent> focusedQuestChangedSignal = new(GameCallbackKind.FocusedQuestChanged);
        static readonly GameSignal<StoryFlagChangedEvent> storyFlagChangedSignal = new(GameCallbackKind.StoryFlagChanged);

        public GameSignal<QuestChangedEvent> QuestStarted => questStartedSignal;
        public GameSignal<QuestChangedEvent> QuestUpdated => questUpdatedSignal;
        public GameSignal<QuestChangedEvent> QuestCompleted => questCompletedSignal;
        public GameSignal<QuestRemovedEvent> QuestRemoved => questRemovedSignal;
        public GameSignal<FocusedQuestChangedEvent> FocusedQuestChanged => focusedQuestChangedSignal;
        public GameSignal<StoryFlagChangedEvent> StoryFlagChanged => storyFlagChangedSignal;

        internal static bool WantsQuestEvents
            => questStartedSignal.HasSubscribers || questUpdatedSignal.HasSubscribers || questCompletedSignal.HasSubscribers;

        internal static void PublishQuestStarted(string questKey, int phase)
        {
            if (!questStartedSignal.HasSubscribers) { return; }
            questStartedSignal.Publish(new QuestChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), questKey, -1, phase));
        }

        internal static void PublishQuestUpdated(string questKey, int previousPhase, int currentPhase)
        {
            if (!questUpdatedSignal.HasSubscribers) { return; }
            questUpdatedSignal.Publish(new QuestChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), questKey, previousPhase, currentPhase));
        }

        internal static void PublishQuestCompleted(string questKey, int finalPhase)
        {
            if (!questCompletedSignal.HasSubscribers) { return; }
            questCompletedSignal.Publish(new QuestChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), questKey, -1, finalPhase));
        }

        internal static void PublishQuestRemoved(string questKey)
        {
            if (!questRemovedSignal.HasSubscribers) { return; }
            questRemovedSignal.Publish(new QuestRemovedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), questKey));
        }

        internal static void PublishFocusedQuestChanged(string questKey)
        {
            if (!focusedQuestChangedSignal.HasSubscribers) { return; }
            focusedQuestChangedSignal.Publish(new FocusedQuestChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), questKey));
        }

        internal static void PublishStoryFlagChanged(string key, int previousValue, int currentValue)
        {
            if (!storyFlagChangedSignal.HasSubscribers) { return; }
            storyFlagChangedSignal.Publish(new StoryFlagChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), key, previousValue, currentValue));
        }
    }
}
