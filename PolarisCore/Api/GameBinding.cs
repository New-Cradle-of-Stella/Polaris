using System;
using m2d;
using nel;

namespace Polaris.API
{
    /// <summary>
    /// 本层唯一接触游戏内部结构的地方：统一取出玩家、当前地图、背包、按键对象等根引用，
    /// 集中换版本时要改的假设。每个取值方法在任意时刻调用都不应抛异常，供上层公开 API 直接用。
    /// </summary>
    internal static class GameBinding
    {
        /// <summary>地图代数：<see cref="CurrentMap"/> 换实例即 +1，用于让旧地图的 <see cref="GameCharacter"/> 包装器整体失效（mover 是对象池复用的，仅比引用会误认目标）。</summary>
        internal static int MapGeneration { get; private set; }

        static Map2d lastMap;

        /// <summary>当前地图；没有加载地图（标题画面、读档中）时返回 <c>null</c>。</summary>
        internal static Map2d CurrentMap => Safe(static () => M2DBase.Instance?.curMap);

        /// <summary>Nel 侧的 M2D。天气、危险度、背包都挂在它下面。</summary>
        internal static NelM2DBase NelM2D => Safe(static () => M2DBase.Instance as NelM2DBase);

        /// <summary>在场的玩家角色，不在场时为 <c>null</c>；不缓存引用是因为切图会重建玩家对象。</summary>
        internal static PR Player => Safe(static () => CurrentMap?.getKeyPr() as PR);

        /// <summary>玩家主背包；没有玩家或物品管理器还没建好时返回 <c>null</c>。</summary>
        internal static ItemStorage Inventory => Safe(static () => ItemManager?.getInventory());

        /// <summary>物品管理器：四个存储容器和掉落物都挂在它下面。</summary>
        internal static NelItemManager ItemManager => Safe(static () => NelM2D?.IMNG);

        /// <summary>贵重品存储。</summary>
        internal static ItemStorage PreciousStorage => Safe(static () => ItemManager?.StPrecious);

        /// <summary>强化品存储。</summary>
        internal static ItemStorage EnhancerStorage => Safe(static () => ItemManager?.StEnhancer);

        /// <summary>住宅仓库。</summary>
        internal static ItemStorage HouseStorage => Safe(static () => ItemManager?.StHouseInventory);

        /// <summary>任务追踪器。</summary>
        internal static QuestTracker Quests => Safe(static () => NelM2D?.QUEST);

        /// <summary>当前的游戏内菜单对象；菜单没建好时返回 <c>null</c>。</summary>
        internal static nel.gm.UiGameMenu Menu => Safe(static () => NelM2D?.GM);

        /// <summary>日夜/天气/危险度控制器。</summary>
        internal static NightController Night => Safe(static () => NelM2D?.NightCon);

        /// <summary>当前生效的按键映射对象；每个动作的输入状态记成一个 float（语义见 <see cref="InputBinding"/>）。</summary>
        internal static XX.KEY KeyAssign => Safe(static () => XX.IN.getCurrentKeyAssignObject());

        /// <summary>取根引用的统一包装：读不出来（游戏内部还没建好、或读取本身抛异常）一律当作"没有"，绝不把异常漏给上层。</summary>
        static T Safe<T>(Func<T> read) where T : class
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>由 <see cref="GameSessionRuntime.Pump"/> 每帧调用，推进地图代数；用轮询而非补丁，因为读一个引用就够了。</summary>
        internal static void Pump()
        {
            Map2d map = CurrentMap;
            if (!ReferenceEquals(map, lastMap))
            {
                lastMap = map;
                MapGeneration++;
            }
        }
    }
}
