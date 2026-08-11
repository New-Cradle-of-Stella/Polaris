using System;
using nel;

namespace Polaris.API
{
    /// <summary>
    /// 物品的身份。现在只包住原版 item key，做成结构体而不是直接用 <c>string</c>，是为了以后
    /// 加入 Addon 自定义物品时不用改所有签名——那时它会多一个命名空间段，而调用方的代码不动。
    /// </summary>
    public readonly struct ItemIdentity
    {
        /// <summary>原版物品 key。</summary>
        public string Key { get; }

        public ItemIdentity(string key)
        {
            Key = key;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Key);

        public static implicit operator ItemIdentity(string key) => new ItemIdentity(key);

        public override string ToString() => Key ?? "(empty)";
    }

    /// <summary>
    /// 玩家背包。
    /// <para>
    /// grade（品级）在游戏里是 0–4，本层强制校验：旧接口把越界的 grade 直接传给游戏，
    /// 结果是静默落到别的格子里或者写坏一行库存。
    /// </para>
    /// <para>
    /// "使用/丢弃/提交"三件事本版本不开放，见 <see cref="GameCapabilities"/>：它们各自带着
    /// 一段 UI 流程和使用者上下文，从外部裸调会算错消耗、或者让物品消失而效果没发生。
    /// </para>
    /// </summary>
    public sealed class InventoryGameAPI
    {
        /// <summary>物品 key 在本版本游戏里存不存在。注册内容前先问一句，比事后 catch 便宜。</summary>
        public bool Exists(ItemIdentity item) => Resolve(item) != null;

        /// <summary>
        /// 背包里有多少个。<paramref name="grade"/> 传 -1 表示不分品级合计。
        /// 物品不存在或背包还没建好时返回 0。
        /// </summary>
        public int Count(ItemIdentity item, int grade = -1)
        {
            NelItem Itm = Resolve(item);
            ItemStorage Storage = GameBinding.Inventory;
            if (Itm == null || Storage == null)
            {
                return 0;
            }

            try
            {
                return Storage.getCount(Itm, grade);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// 这次给予实际能进去多少个（不真的给）。背包满时会小于 <paramref name="count"/>。
        /// 走的是游戏自己的入库演算的空跑模式，不是自己按容量估算，所以和真给的结果一致。
        /// </summary>
        public int CanGive(ItemIdentity item, int count, int grade = 0)
        {
            NelItem Itm = Resolve(item);
            ItemStorage Storage = GameBinding.Inventory;
            if (Itm == null || Storage == null || count <= 0 || !ValidGrade(grade))
            {
                return 0;
            }

            try
            {
                return Storage.Add(Itm, count, grade, add_row: false, execute: false);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// 给玩家物品。背包放不下时是<b>部分成功</b>：结果里的 <see cref="ItemChangeResult.Count"/>
        /// 是真正进去的数量，调用方要按它结算（比如奖励发放要据此决定是否还欠玩家东西）。
        /// </summary>
        public ItemChangeResult Give(ItemIdentity item, int count, int grade = 0)
        {
            if (count <= 0)
            {
                return new ItemChangeResult(
                    GameActionResult.Fail(GameActionStatus.InvalidArgument, "Count must be positive."), 0);
            }

            if (!ValidGrade(grade))
            {
                return new ItemChangeResult(
                    GameActionResult.Fail(GameActionStatus.InvalidArgument, $"Grade out of range: {grade}, expected 0-4."), 0);
            }

            NelItem Itm = Resolve(item);
            if (Itm == null)
            {
                return new ItemChangeResult(
                    GameActionResult.Fail(GameActionStatus.InvalidArgument, $"No such item: {item}."), 0);
            }

            ItemStorage Storage = GameBinding.Inventory;
            if (Storage == null)
            {
                return new ItemChangeResult(
                    GameActionResult.Fail(GameActionStatus.TargetUnavailable, "The inventory is not ready yet."), 0);
            }

            try
            {
                int added = Storage.Add(Itm, count, grade);
                return new ItemChangeResult(
                    added > 0
                        ? GameActionResult.Ok()
                        : GameActionResult.Fail(GameActionStatus.InsufficientResource, "The inventory has no room."),
                    added);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Inventory.Give");
                return new ItemChangeResult(GameActionResult.Fail(GameActionStatus.Failed, ex.Message), 0);
            }
        }

        /// <summary>
        /// 从背包里扣除。数量不够时<b>不扣</b>并返回 <see cref="GameActionStatus.InsufficientResource"/>
        /// ——半扣一半在物品消耗场景里等同于坑玩家，宁可整笔失败让调用方重试。
        /// </summary>
        public ItemChangeResult Take(ItemIdentity item, int count, int grade = 0)
        {
            if (count <= 0)
            {
                return new ItemChangeResult(
                    GameActionResult.Fail(GameActionStatus.InvalidArgument, "Count must be positive."), 0);
            }

            if (!ValidGrade(grade))
            {
                return new ItemChangeResult(
                    GameActionResult.Fail(GameActionStatus.InvalidArgument, $"Grade out of range: {grade}, expected 0-4."), 0);
            }

            NelItem Itm = Resolve(item);
            ItemStorage Storage = GameBinding.Inventory;
            if (Itm == null)
            {
                return new ItemChangeResult(
                    GameActionResult.Fail(GameActionStatus.InvalidArgument, $"No such item: {item}."), 0);
            }

            if (Storage == null)
            {
                return new ItemChangeResult(
                    GameActionResult.Fail(GameActionStatus.TargetUnavailable, "The inventory is not ready yet."), 0);
            }

            try
            {
                if (Storage.getCount(Itm, grade) < count)
                {
                    return new ItemChangeResult(
                        GameActionResult.Fail(GameActionStatus.InsufficientResource, "Not enough of that item in the inventory."), 0);
                }

                return Storage.Reduce(Itm, count, grade)
                    ? new ItemChangeResult(GameActionResult.Ok(), count)
                    : new ItemChangeResult(
                        GameActionResult.Fail(GameActionStatus.RejectedByState, "The game rejected this deduction."), 0);
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "Inventory.Take");
                return new ItemChangeResult(GameActionResult.Fail(GameActionStatus.Failed, ex.Message), 0);
            }
        }

        /// <summary>使用物品。<b>本版本未支持</b>，理由见类型说明。</summary>
        public GameActionResult Use(ItemIdentity item, int grade = 0)
            => GameActionResult.Unsupported("This game version has no usable item-use entry point.");

        /// <summary>丢弃物品。<b>本版本未支持</b>。</summary>
        public GameActionResult Drop(ItemIdentity item, int count, int grade = 0)
            => GameActionResult.Unsupported("This game version has no usable item-drop entry point.");

        /// <summary>把物品提交给 NPC。<b>本版本未支持</b>。</summary>
        public GameActionResult Submit(ItemIdentity item, int count, int grade, CharacterHandle npc)
            => GameActionResult.Unsupported("This game version has no usable item-submit entry point.");

        static bool ValidGrade(int grade) => grade >= 0 && grade <= 4;

        static NelItem Resolve(ItemIdentity item)
        {
            if (item.IsEmpty)
            {
                return null;
            }

            try
            {
                // no_error: true——查不到是调用方的正常分支（"这个物品在本版本有没有"），
                // 不该让游戏自己往日志里写一条错误。
                return NelItem.GetById(item.Key, no_error: true);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>背包变更的结果。<see cref="Count"/> 是<b>实际</b>进出的数量。</summary>
    public readonly struct ItemChangeResult
    {
        public GameActionResult Outcome { get; }

        public int Count { get; }

        internal ItemChangeResult(GameActionResult outcome, int count)
        {
            Outcome = outcome;
            Count = count;
        }

        public bool Succeeded => Outcome.Succeeded;

        public override string ToString() => $"{Outcome} x{Count}";
    }
}
