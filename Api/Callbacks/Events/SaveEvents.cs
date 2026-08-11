namespace Polaris.API
{
    public sealed class GameSceneStartingEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal GameSceneStartingEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }

    /// <summary>游戏场景初始化完成；<see cref="LoadedExistingSave"/> 区分"读到了存档"还是"落回新游戏"。</summary>
    public sealed class GameSceneStartedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public bool LoadedExistingSave { get; }

        internal GameSceneStartedEvent(GameCallbackStamp stamp, bool loadedExistingSave)
        {
            Stamp = stamp;
            LoadedExistingSave = loadedExistingSave;
        }
    }

    public sealed class NewGameStartingEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal NewGameStartingEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }

    public sealed class NewGameStartedEvent
    {
        public GameCallbackStamp Stamp { get; }
        internal NewGameStartedEvent(GameCallbackStamp stamp) => Stamp = stamp;
    }

    /// <summary>正在读取某个存档槽；此刻还不知道会不会成功。</summary>
    public sealed class SaveLoadingEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int SlotIndex { get; }

        internal SaveLoadingEvent(GameCallbackStamp stamp, int slotIndex)
        {
            Stamp = stamp;
            SlotIndex = slotIndex;
        }
    }

    /// <summary>存档内容已回灌到游戏内存。</summary>
    public sealed class SaveLoadedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int SlotIndex { get; }

        internal SaveLoadedEvent(GameCallbackStamp stamp, int slotIndex)
        {
            Stamp = stamp;
            SlotIndex = slotIndex;
        }
    }

    /// <summary>存档加载未成功；<see cref="Reason"/> 直通游戏自己的失败提示文本，不保证稳定。</summary>
    public sealed class SaveFailedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int SlotIndex { get; }
        public string Reason { get; }

        internal SaveFailedEvent(GameCallbackStamp stamp, int slotIndex, string reason)
        {
            Stamp = stamp;
            SlotIndex = slotIndex;
            Reason = reason;
        }
    }

    /// <summary>正在把当前游戏状态序列化成存档二进制数据。</summary>
    public sealed class SaveSerializingEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int SlotIndex { get; }

        internal SaveSerializingEvent(GameCallbackStamp stamp, int slotIndex)
        {
            Stamp = stamp;
            SlotIndex = slotIndex;
        }
    }

    /// <summary>内存里的存档二进制数据已生成；<b>不代表已经落盘</b>，落盘结果看 <see cref="SaveWrittenEvent"/>。</summary>
    public sealed class SaveSerializedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int SlotIndex { get; }
        public int ByteCount { get; }

        internal SaveSerializedEvent(GameCallbackStamp stamp, int slotIndex, int byteCount)
        {
            Stamp = stamp;
            SlotIndex = slotIndex;
            ByteCount = byteCount;
        }
    }

    /// <summary>即将把序列化好的数据落盘。</summary>
    public sealed class SaveWritingEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int SlotIndex { get; }

        internal SaveWritingEvent(GameCallbackStamp stamp, int slotIndex)
        {
            Stamp = stamp;
            SlotIndex = slotIndex;
        }
    }

    /// <summary>落盘函数已经返回；这才是"存档真的写到磁盘上了没有"的最终答案。</summary>
    public sealed class SaveWrittenEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int SlotIndex { get; }
        public bool Succeeded { get; }
        public string FailureReason { get; }

        internal SaveWrittenEvent(GameCallbackStamp stamp, int slotIndex, bool succeeded, string failureReason)
        {
            Stamp = stamp;
            SlotIndex = slotIndex;
            Succeeded = succeeded;
            FailureReason = failureReason;
        }
    }

    public sealed class AutoSaveStartingEvent
    {
        public GameCallbackStamp Stamp { get; }
        public bool IsBench { get; }

        internal AutoSaveStartingEvent(GameCallbackStamp stamp, bool isBench)
        {
            Stamp = stamp;
            IsBench = isBench;
        }
    }

    public sealed class AutoSaveCompletedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public bool IsBench { get; }
        public bool Succeeded { get; }

        internal AutoSaveCompletedEvent(GameCallbackStamp stamp, bool isBench, bool succeeded)
        {
            Stamp = stamp;
            IsBench = isBench;
            Succeeded = succeeded;
        }
    }
}
