using System;
using nel;

namespace Polaris.API
{
    /// <summary>
    /// 一个物品存储容器：主背包、贵重品、强化品与住宅仓库各是一个实例，
    /// 入口在 <c>PolarisAPI.Game.Inventory</c> 下。
    /// <para>
    /// 每个存储都有自己的容量、分级规则与可否装水的策略，所以它们是<b>四个不同的实例</b>
    /// 而不是一个带枚举参数的门面——后者会让"这个操作作用在哪个容器上"藏在参数里，
    /// 而实例回调也就没法只发给真正变化的那一个容器。
    /// </para>
    /// <para>
    /// grade（品级）在游戏里是 0–4。本层会校验：越界的 grade 传给游戏会静默落到别的格子里，
    /// 或者写坏一行库存。
    /// </para>
    /// </summary>
    public sealed class GameStorage : GameInstance
    {
        /// <summary>与游戏的 <c>NelItem.GRADE_MAX</c> 对齐。</summary>
        const int MaxGrade = 4;

        static readonly InstanceTable<ItemStorage, GameStorage> Table = new();

        readonly ItemStorage storage;

        GameStorage(ItemStorage storage)
        {
            this.storage = storage;
        }

        internal static GameStorage Wrap(ItemStorage native) => Table.Get(native, static n => new GameStorage(n));

        internal static GameStorage Peek(ItemStorage native) => Table.Peek(native);

        internal static void SweepStorages() => Table.Sweep();

        internal static void InvalidateAllStorages() => Table.InvalidateAll();

        ItemStorage Native => IsValid ? storage : null;

        private protected override bool IsNativeAlive => storage != null;

        private protected override string Describe() => $"GameStorage({SafeName()})";

        /// <summary>获取该存储容器可容纳的行数。</summary>
        public int CapacityRows => Read(static s => s.row_max, 0);

        /// <summary>判断该存储容器是否按物品等级分组存放。</summary>
        public bool SplitsByGrade => Read(static s => s.grade_split, false);

        /// <summary>获取或设置该存储容器是否允许存放水类物品。</summary>
        public bool AcceptsWater
        {
            get => Read(static s => s.water_stockable, false);
            set
            {
                EnsureUsable();
                ItemStorage s = Native;
                if (s == null)
                {
                    return;
                }

                try
                {
                    s.water_stockable = value;
                }
                catch (Exception ex)
                {
                    PolarisAPI.Errors.Report(ex, "GameStorage.AcceptsWater");
                }
            }
        }

        /// <summary>统计该存储容器中的指定物品数量。<paramref name="grade"/> 传 -1 表示不分品级合计。</summary>
        public int Count(GameItem item, int grade = -1)
        {
            ItemStorage s = Native;
            NelItem native = item?.NativeItem;
            if (s == null || native == null)
            {
                return 0;
            }

            try
            {
                return s.getCount(native, grade);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// 判断指定物品能否放入该存储容器。走的是游戏自己的入库演算的空跑模式，
        /// 不是自己按容量估算，因此和真的放一次的结果一致。
        /// </summary>
        public bool CanAdd(GameItem item, int count = 1, int grade = 0)
        {
            ItemStorage s = Native;
            NelItem native = item?.NativeItem;
            if (s == null || native == null || count <= 0 || !ValidGrade(grade))
            {
                return false;
            }

            try
            {
                return s.Add(native, count, grade, false, false) >= count;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 向该存储容器加入物品，返回<b>实际</b>加入的数量。
        /// <para>
        /// 放不下时是<b>部分成功</b>：调用方要按返回值结算（例如奖励发放要据此决定是否还欠玩家东西），
        /// 而不是假设一定全进去了。
        /// </para>
        /// </summary>
        public int Add(GameItem item, int count = 1, int grade = 0)
        {
            EnsureUsable();

            ItemStorage s = Native;
            NelItem native = item?.NativeItem;
            if (s == null || native == null || count <= 0 || !ValidGrade(grade))
            {
                return 0;
            }

            try
            {
                return s.Add(native, count, grade, true, true);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameStorage.Add");
                return 0;
            }
        }

        /// <summary>
        /// 从该存储容器移除指定数量的物品。
        /// <para>
        /// 数量不够时<b>一个都不扣</b>并返回 <c>false</c>：半扣一半在物品消耗场景里等同于坑玩家，
        /// 宁可整笔失败让调用方重试。
        /// </para>
        /// </summary>
        public bool Reduce(GameItem item, int count = 1, int grade = -1)
        {
            EnsureUsable();

            ItemStorage s = Native;
            NelItem native = item?.NativeItem;
            if (s == null || native == null || count <= 0)
            {
                return false;
            }

            if (grade != -1 && !ValidGrade(grade))
            {
                return false;
            }

            try
            {
                if (s.getCount(native, grade) < count)
                {
                    return false;
                }

                return s.Reduce(native, count, grade, true);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameStorage.Reduce");
                return false;
            }
        }

        /// <summary>
        /// 清空该存储容器中的全部物品。<paramref name="newCapacity"/> 传 -1 表示保持当前容量。
        /// </summary>
        public void Clear(int newCapacity = -1)
        {
            EnsureUsable();

            ItemStorage s = Native;
            if (s == null)
            {
                return;
            }

            try
            {
                bool wasEmpty = s.row_max <= 0 || !HasAnything(s);
                s.clearAllItems(newCapacity);

                // 只有本来非空的容器被清空才算一次事件：空容器再清一次什么都没发生，
                // 发出去只会让订阅者误以为玩家丢了东西。
                if (!wasEmpty)
                {
                    GameCallbackHub.PublishInstance(
                        GameInstanceCallbackKind.StorageCleared,
                        this,
                        () => new StorageClearedCallbackData(this, newCapacity));
                }
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameStorage.Clear");
            }
        }

        /// <summary>
        /// 使用该存储容器中的指定物品，返回游戏给出的使用结果码（含义由物品自身决定，
        /// 非零一般表示确实生效）。玩家不在场时不做任何事并返回 0。
        /// </summary>
        public int Use(GameItem item, int grade = 0)
        {
            EnsureUsable();

            ItemStorage s = Native;
            NelItem native = item?.NativeItem;
            if (s == null || native == null || !ValidGrade(grade))
            {
                return 0;
            }

            PR player = GameBinding.Player;
            if (player == null)
            {
                return 0;
            }

            try
            {
                int result = native.Use(player, s, grade, null);
                GameItem.PublishUsed(item, grade, result);
                return result;
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameStorage.Use");
                return 0;
            }
        }

        /// <summary>
        /// 从该存储容器取出物品，在当前地图生成掉落物。
        /// 数量不够、没有地图或没有玩家时不做任何事并返回 <c>null</c>。
        /// </summary>
        public GameDrop Drop(GameItem item, int count = 1, int grade = 0)
        {
            EnsureUsable();

            ItemStorage s = Native;
            NelItem native = item?.NativeItem;
            if (s == null || native == null || count <= 0 || !ValidGrade(grade))
            {
                return null;
            }

            NelItemManager manager = GameBinding.ItemManager;
            PR player = GameBinding.Player;
            if (manager == null || player == null)
            {
                return null;
            }

            try
            {
                if (s.getCount(native, grade) < count || !s.Reduce(native, count, grade, true))
                {
                    return null;
                }

                float x = player.x;
                float y = player.y;
                manager.dropManual(native, count, grade, x, y, 0f, 0f, null, false, default);
                return new GameDrop(item, count, grade, new GameVector2(x, y));
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameStorage.Drop");
                return null;
            }
        }

        static bool ValidGrade(int grade) => grade >= 0 && grade <= MaxGrade;

        /// <summary>
        /// 容器里现在有没有东西。读的是物品行列表本身，不是 UI 按钮数量——按钮只有在这个容器
        /// 正被某个界面显示时才存在，拿它判空会让"后台容器被清空"这条事件永远发不出来。
        /// </summary>
        static bool HasAnything(ItemStorage s)
        {
            try
            {
                return s.ARow != null && s.ARow.Count > 0;
            }
            catch (Exception)
            {
                // 问不出来就当作非空：漏发一条"清空"事件比无中生有地发一条更难查。
                return true;
            }
        }

        string SafeName()
        {
            try
            {
                return storage?.localized_name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        TValue Read<TValue>(Func<ItemStorage, TValue> read, TValue fallback)
        {
            ItemStorage s = Native;
            if (s == null)
            {
                return fallback;
            }

            try
            {
                return read(s);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
