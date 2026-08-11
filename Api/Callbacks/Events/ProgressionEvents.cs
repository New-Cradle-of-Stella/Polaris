namespace Polaris.API
{
    /// <summary>任务进入追踪表、阶段推进或完成。</summary>
    public sealed class QuestChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string QuestKey { get; }
        public int PreviousPhase { get; }
        public int CurrentPhase { get; }

        internal QuestChangedEvent(GameCallbackStamp stamp, string questKey, int previousPhase, int currentPhase)
        {
            Stamp = stamp;
            QuestKey = questKey;
            PreviousPhase = previousPhase;
            CurrentPhase = currentPhase;
        }
    }

    /// <summary>任务从追踪表移除。</summary>
    public sealed class QuestRemovedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string QuestKey { get; }

        internal QuestRemovedEvent(GameCallbackStamp stamp, string questKey)
        {
            Stamp = stamp;
            QuestKey = questKey;
        }
    }

    /// <summary>当前聚焦任务变化；<c>null</c> 表示取消聚焦。</summary>
    public sealed class FocusedQuestChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string QuestKey { get; }

        internal FocusedQuestChangedEvent(GameCallbackStamp stamp, string questKey)
        {
            Stamp = stamp;
            QuestKey = questKey;
        }
    }

    /// <summary>剧情 flag 旧值 -&gt; 新值。<c>Key</c> 是游戏内部字符串，不保证跨版本稳定。</summary>
    public sealed class StoryFlagChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string Key { get; }
        public int PreviousValue { get; }
        public int CurrentValue { get; }

        internal StoryFlagChangedEvent(GameCallbackStamp stamp, string key, int previousValue, int currentValue)
        {
            Stamp = stamp;
            Key = key;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }
    }
}
