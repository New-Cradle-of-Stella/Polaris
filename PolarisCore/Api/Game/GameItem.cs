using System;
using nel;

namespace Polaris.API
{
    /// <summary>
    /// 一种物品的定义（"回复药"这一类，而非背包里具体的某一格，那是 <see cref="GameStorage"/> 的事）。
    /// 取得实例的入口是 <c>PolarisAPI.Game.Items.Resolve(key)</c>；生命周期与存档、地图无关，一局游戏内始终有效。
    /// </summary>
    public sealed class GameItem : GameInstance
    {
        static readonly InstanceTable<NelItem, GameItem> Table = new();

        readonly NelItem item;

        GameItem(NelItem item)
        {
            this.item = item;
        }

        internal static GameItem Wrap(NelItem native) => Table.Get(native, static n => new GameItem(n));

        /// <summary>按 key 解析。查不到返回 <c>null</c>。</summary>
        internal static GameItem Resolve(string itemKey)
        {
            if (string.IsNullOrEmpty(itemKey))
            {
                return null;
            }

            try
            {
                // no_error: true——查无此物品是正常分支，不应产生日志错误。
                return Wrap(NelItem.GetById(itemKey, true));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>本层内部拿回底层物品对象；存储与掉落都要用它去调游戏的接口。</summary>
        internal NelItem NativeItem => item;

        private protected override bool IsNativeAlive => item != null;

        private protected override string Describe() => $"GameItem({SafeKey()})";

        /// <summary>获取该物品的稳定键名。</summary>
        public string Key => SafeKey();

        /// <summary>获取该物品的原版数值编号。</summary>
        public ushort Id => Read(static i => i.id, (ushort)0);

        /// <summary>获取该物品的基础价格（0 级）。带品级的价格用游戏自己的换算，不是简单倍乘。</summary>
        public int Price => Read(static i => i.price, 0);

        /// <summary>获取该物品的最大堆叠数量（一行能放多少个）。</summary>
        public int StackLimit => Read(static i => i.stock, 0);

        /// <summary>获取该物品的所属分类。这是<b>位标志</b>，一个物品可以同时属于多个分类。</summary>
        public GameItemCategory Category => Read(static i => (GameItemCategory)(int)i.category, GameItemCategory.Other);

        /// <summary>获取该物品的原版数值参数。具体含义由分类决定（回复量、材料等级……）。</summary>
        public float Value => Read(static i => i.value, 0f);

        /// <summary>判断该物品是否可以使用。</summary>
        public bool IsUsable => Read(static i => i.useable, false);

        /// <summary>判断该物品是否属于贵重物品。</summary>
        public bool IsPrecious => Read(static i => i.is_precious, false);

        /// <summary>判断该物品是否属于食物。</summary>
        public bool IsFood => Read(static i => i.is_food, false);

        /// <summary>判断该物品是否属于工具。</summary>
        public bool IsTool => Read(static i => i.is_tool, false);

        /// <summary>判断该物品是否属于炸弹。</summary>
        public bool IsBomb => Read(static i => i.is_bomb, false);

        /// <summary>获取该物品指定等级的本地化显示名称，跟随玩家当前语言，不缓存。</summary>
        public string GetLocalizedName(int grade = 0)
        {
            if (item == null)
            {
                return null;
            }

            try
            {
                return item.getLocalizedName(grade);
            }
            catch (Exception)
            {
                return null;
            }
        }

        string SafeKey()
        {
            try
            {
                return item?.key;
            }
            catch (Exception)
            {
                return null;
            }
        }

        TValue Read<TValue>(Func<NelItem, TValue> read, TValue fallback)
        {
            if (item == null)
            {
                return fallback;
            }

            try
            {
                return read(item);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        /// <summary>由存储的 <c>Use</c> 与物品使用补丁调用，发布实例回调。</summary>
        internal static void PublishUsed(GameItem item, int grade, int result)
        {
            if (item == null)
            {
                return;
            }

            GameCallbackHub.PublishInstance(
                GameInstanceCallbackKind.ItemUsed, item, () => new ItemUsedCallbackData(item, grade, result));
        }
    }
}
