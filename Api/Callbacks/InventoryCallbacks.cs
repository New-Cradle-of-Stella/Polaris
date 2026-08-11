using nel;
using Polaris.Infra;

namespace Polaris.API
{
    /// <summary>背包、掉落、物品使用和经济回调。第一批覆盖增减/转移/获得记录与货币；
    /// 掉落生成/拾取、商店成交与制作系统留待后续版本（候选锚点还需要进一步 IL 审计）。</summary>
    public sealed class InventoryCallbacks
    {
        internal InventoryCallbacks() { }

        static readonly GameSignal<InventoryChangedEvent> itemAddedSignal = new(GameCallbackKind.ItemAdded);
        static readonly GameSignal<InventoryChangedEvent> itemRemovedSignal = new(GameCallbackKind.ItemRemoved);
        static readonly GameSignal<ItemsTransferredEvent> itemsTransferredSignal = new(GameCallbackKind.ItemsTransferred);
        static readonly GameSignal<ItemObtainedEvent> itemObtainedSignal = new(GameCallbackKind.ItemObtained);
        static readonly GameSignal<MoneyChangedEvent> moneyChangedSignal = new(GameCallbackKind.MoneyChanged);

        public GameSignal<InventoryChangedEvent> ItemAdded => itemAddedSignal;
        public GameSignal<InventoryChangedEvent> ItemRemoved => itemRemovedSignal;
        public GameSignal<ItemsTransferredEvent> ItemsTransferred => itemsTransferredSignal;
        public GameSignal<ItemObtainedEvent> ItemObtained => itemObtainedSignal;
        public GameSignal<MoneyChangedEvent> MoneyChanged => moneyChangedSignal;

        static bool moneyListenersInstalled;

        /// <summary>
        /// 由 <see cref="LifecycleCallbacks.PublishReady"/> 首次就绪时调用一次：<c>CoinStorage</c>
        /// 自己就有监听者机制，不需要 Harmony——但只能在它的 <c>Aentry</c> 表建好之后注册，
        /// 提前调用会在游戏还没 init 时打到空表上。
        /// </summary>
        internal static void InstallMoneyListenersOnce()
        {
            if (moneyListenersInstalled)
            {
                return;
            }

            moneyListenersInstalled = true;
            try
            {
                CoinStorage.addListener(CoinStorage.CTYPE.GOLD, (CEntry, added, addValue) => OnMoneyChanged(CoinStorage.CTYPE.GOLD, CEntry, added, addValue));
                CoinStorage.addListener(CoinStorage.CTYPE.CRAFTS, (CEntry, added, addValue) => OnMoneyChanged(CoinStorage.CTYPE.CRAFTS, CEntry, added, addValue));
                CoinStorage.addListener(CoinStorage.CTYPE.JUICE, (CEntry, added, addValue) => OnMoneyChanged(CoinStorage.CTYPE.JUICE, CEntry, added, addValue));
            }
            catch (System.Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "InventoryCallbacks.InstallMoneyListenersOnce", typeof(InventoryCallbacks).Assembly);
            }
        }

        static void OnMoneyChanged(CoinStorage.CTYPE ctype, CoinEntry entry, int added, bool addValue)
        {
            if (!moneyChangedSignal.HasSubscribers || added == 0)
            {
                return;
            }

            int delta = addValue ? added : -added;
            int current = (int)entry.Get();
            int previous = current - delta;
            moneyChangedSignal.Publish(new MoneyChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), (int)ctype, previous, current, delta));
        }

        internal static void PublishItemAdded(GameStorageKind storage, string itemKey, int grade, int delta)
        {
            if (!itemAddedSignal.HasSubscribers) { return; }
            itemAddedSignal.Publish(new InventoryChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), storage, itemKey, grade, delta));
        }

        internal static void PublishItemRemoved(GameStorageKind storage, string itemKey, int grade, int delta)
        {
            if (!itemRemovedSignal.HasSubscribers) { return; }
            itemRemovedSignal.Publish(new InventoryChangedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), storage, itemKey, grade, delta));
        }

        internal static void PublishItemsTransferred(int itemCount)
        {
            if (!itemsTransferredSignal.HasSubscribers || itemCount == 0) { return; }
            itemsTransferredSignal.Publish(new ItemsTransferredEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), itemCount));
        }

        internal static void PublishItemObtained(string itemKey, int count)
        {
            if (!itemObtainedSignal.HasSubscribers) { return; }
            itemObtainedSignal.Publish(new ItemObtainedEvent(
                CallbackRuntime.NextStamp(GameCallbackOrigin.Vanilla, GameCallbackPrecision.Exact), itemKey, count));
        }
    }
}
