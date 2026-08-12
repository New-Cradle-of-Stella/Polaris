using System;
using m2d;
using nel;

namespace Polaris.API
{
    /// <summary>
    /// 本层<b>唯一</b>接触游戏内部结构的地方：从游戏里取出玩家、当前地图、背包、按键对象这四样
    /// 根引用，别的文件一律通过这里拿。理由和 <see cref="GameSessionRuntime"/> 顶上写的一样——
    /// 换游戏版本时要改的假设集中在一处，而不是散在十来个门面里各写一遍
    /// <c>M2DBase.Instance as NelM2DBase</c>。
    /// <para>
    /// 这里的每个取值方法都必须能在"游戏还没起来 / 已经卸载地图 / 正在切图"的任意时刻被调用
    /// 而不抛异常：上层门面是给下游模组用的公开 API，模组作者不该为了读一个血量去写 try。
    /// </para>
    /// </summary>
    internal static class GameBinding
    {
        /// <summary>
        /// 地图代数。每次 <see cref="CurrentMap"/> 换成另一个实例就 +1，用来让上一张图里发出去的
        /// <see cref="GameCharacter"/> 包装器整体失效——游戏的 mover 是对象池复用的，
        /// 只比对引用相等会让"同一个池对象换了个角色"被误认成同一个目标。
        /// </summary>
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

        /// <summary>
        /// 在场的玩家角色；不在场时返回 <c>null</c>。走 <c>curMap.getKeyPr()</c> 而不是缓存一个
        /// 静态引用：切图会重建玩家对象，缓存下来的那个会变成"看得见摸不着"的僵尸引用。
        /// </summary>
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

        /// <summary>
        /// 当前生效的按键映射对象。游戏把每个动作的输入状态记成一个 float
        /// （见 <see cref="InputBinding"/> 顶上对 mv 值语义的说明），全都挂在这个对象上。
        /// </summary>
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

        /// <summary>
        /// 由 <see cref="GameSessionRuntime.Pump"/> 每帧调用：只负责推进地图代数。
        /// 放在 Polaris 自己的泵里而不是给游戏打补丁，是因为"地图换了"这件事读一个引用就能知道，
        /// 不值得为它维护一个跟着游戏版本走的 Harmony 补丁。
        /// </summary>
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
