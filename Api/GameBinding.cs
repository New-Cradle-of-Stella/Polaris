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
        internal static Map2d CurrentMap
        {
            get
            {
                try
                {
                    return M2DBase.Instance?.curMap;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>Nel 侧的 M2D。天气、危险度、背包都挂在它下面。</summary>
        internal static NelM2DBase NelM2D
        {
            get
            {
                try
                {
                    return M2DBase.Instance as NelM2DBase;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>在场的玩家角色，不在场时为 <c>null</c>；不缓存引用是因为切图会重建玩家对象。</summary>
        internal static PR Player
        {
            get
            {
                try
                {
                    return CurrentMap?.getKeyPr() as PR;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>玩家主背包；没有玩家或物品管理器还没建好时返回 <c>null</c>。</summary>
        internal static ItemStorage Inventory
        {
            get
            {
                try
                {
                    return NelM2D?.IMNG?.getInventory();
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>物品管理器：四个存储容器和掉落物都挂在它下面。</summary>
        internal static NelItemManager ItemManager
        {
            get
            {
                try
                {
                    return NelM2D?.IMNG;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>贵重品存储。</summary>
        internal static ItemStorage PreciousStorage => Storage(static m => m.StPrecious);

        /// <summary>强化品存储。</summary>
        internal static ItemStorage EnhancerStorage => Storage(static m => m.StEnhancer);

        /// <summary>住宅仓库。</summary>
        internal static ItemStorage HouseStorage => Storage(static m => m.StHouseInventory);

        static ItemStorage Storage(Func<NelItemManager, ItemStorage> pick)
        {
            try
            {
                NelItemManager manager = ItemManager;
                return manager == null ? null : pick(manager);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>任务追踪器。</summary>
        internal static QuestTracker Quests
        {
            get
            {
                try
                {
                    return NelM2D?.QUEST;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>当前的游戏内菜单对象；菜单没建好时返回 <c>null</c>。</summary>
        internal static nel.gm.UiGameMenu Menu
        {
            get
            {
                try
                {
                    return NelM2D?.GM;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>日夜/天气/危险度控制器。</summary>
        internal static NightController Night
        {
            get
            {
                try
                {
                    return NelM2D?.NightCon;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>当前生效的按键映射对象；每个动作的输入状态记成一个 float（语义见 <see cref="InputBinding"/>）。</summary>
        internal static XX.KEY KeyAssign
        {
            get
            {
                try
                {
                    return XX.IN.getCurrentKeyAssignObject();
                }
                catch (Exception)
                {
                    return null;
                }
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
