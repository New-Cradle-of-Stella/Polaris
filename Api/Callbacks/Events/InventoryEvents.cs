namespace Polaris.API
{
    /// <summary>某个 Storage 具体是哪一个；目前只能确定是不是玩家主背包，其它一律 <c>Unknown</c>。</summary>
    public enum GameStorageKind
    {
        Unknown = 0,
        PlayerInventory = 1,
        HouseInventory = 2,
        PreciousInventory = 3,
        Temporary = 4,
    }

    /// <summary>某个 Storage 里的物品数量变化。</summary>
    public sealed class InventoryChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public GameStorageKind Storage { get; }
        public string ItemKey { get; }
        public int Grade { get; }
        public int Delta { get; }

        internal InventoryChangedEvent(GameCallbackStamp stamp, GameStorageKind storage, string itemKey, int grade, int delta)
        {
            Stamp = stamp;
            Storage = storage;
            ItemKey = itemKey;
            Grade = grade;
            Delta = delta;
        }
    }

    /// <summary>两个 Storage 之间的物品转移；不要把它拆读成一次增一次减，那样看不出关联。</summary>
    public sealed class ItemsTransferredEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int ItemCount { get; }

        internal ItemsTransferredEvent(GameCallbackStamp stamp, int itemCount)
        {
            Stamp = stamp;
            ItemCount = itemCount;
        }
    }

    /// <summary>玩家的"获得记录"增加（图鉴/统计用途），与某个 Storage 是否真的多了这件物品是两件事。</summary>
    public sealed class ItemObtainedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public string ItemKey { get; }
        public int Count { get; }

        internal ItemObtainedEvent(GameCallbackStamp stamp, string itemKey, int count)
        {
            Stamp = stamp;
            ItemKey = itemKey;
            Count = count;
        }
    }

    /// <summary>某种货币变化。</summary>
    public sealed class MoneyChangedEvent
    {
        public GameCallbackStamp Stamp { get; }
        public int CurrencyType { get; }
        public int PreviousValue { get; }
        public int CurrentValue { get; }
        public int Delta { get; }

        internal MoneyChangedEvent(GameCallbackStamp stamp, int currencyType, int previousValue, int currentValue, int delta)
        {
            Stamp = stamp;
            CurrencyType = currencyType;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            Delta = delta;
        }
    }
}
