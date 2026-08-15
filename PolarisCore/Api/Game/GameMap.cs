using System;
using m2d;

namespace Polaris.API
{
    /// <summary>一张已加载的地图，生命周期是从打开到关闭的这一段；切图后旧实例失效，回调随之停止。</summary>
    public sealed class GameMap : GameInstance
    {
        static readonly InstanceTable<Map2d, GameMap> Table = new();

        readonly Map2d map;
        readonly int openedAtGameFrame;

        GameMap(Map2d map)
        {
            this.map = map;
            openedAtGameFrame = SafeGameFrame();
        }

        internal static GameMap Wrap(Map2d native) => Table.Get(native, static n => new GameMap(n));

        /// <summary>已经建过包装器就返回它，没建过返回 <c>null</c>。地图关闭回调用它，避免为一张已经没了的图新建包装器。</summary>
        internal static GameMap Peek(Map2d native) => Table.Peek(native);

        internal static void Invalidate(Map2d native) => Table.Invalidate(native);

        internal static void SweepMaps() => Table.Sweep();

        Map2d Native => IsValid ? map : null;

        private protected override bool IsNativeAlive
        {
            get
            {
                if (map == null)
                {
                    return false;
                }

                try
                {
                    // 用游戏自己的关闭标记，比"是不是 curMap"更准确——子地图本来就不是 curMap。
                    return !map.closed;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private protected override string Describe() => $"GameMap({SafeKey()})";

        /// <summary>获取该地图实例的唯一键名。</summary>
        public string Key => SafeKey();

        /// <summary>获取该地图累计运行的游戏时间（秒），基于游戏帧计数而非墙上时间，暂停/读档/演出期间不走。</summary>
        public float Time
        {
            get
            {
                if (!IsValid)
                {
                    return 0f;
                }

                int elapsed = SafeGameFrame() - openedAtGameFrame;
                return elapsed <= 0 ? 0f : elapsed / 60f;
            }
        }

        /// <summary>获取该地图中的移动对象数量。</summary>
        public int MoverCount => Read(static m => m.count_movers, 0);

        /// <summary>获取该地图中的玩家对象数量。</summary>
        public int PlayerCount => Read(static m => m.count_players, 0);

        /// <summary>判断该地图是否处于黑暗区域。</summary>
        public bool IsDark
        {
            get
            {
                if (!IsValid)
                {
                    return false;
                }

                try
                {
                    return M2DBase.Instance?.map_dark_area ?? false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>获取该地图的显示标题；没有标题时为 <c>null</c>。</summary>
        public string Title
        {
            get
            {
                Map2d m = Native;
                if (m == null)
                {
                    return null;
                }

                try
                {
                    return M2DBase.Instance?.getMapTitle(m);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>把当前鼠标位置转换为该地图的坐标。</summary>
        public GameVector2 MousePosition
        {
            get
            {
                if (!IsValid)
                {
                    return GameVector2.Zero;
                }

                try
                {
                    M2DBase instance = M2DBase.Instance;
                    return instance == null ? GameVector2.Zero : (GameVector2)instance.getMousePosToMapPos();
                }
                catch (Exception)
                {
                    return GameVector2.Zero;
                }
            }
        }

        /// <summary>判断该地图中的指定矩形是否位于摄像机可见范围内；<paramref name="marginPixels"/> 把判定范围向外扩一圈。</summary>
        public bool IsInCamera(float x, float y, float width, float height, float marginPixels = 0)
        {
            Map2d m = Native;
            if (m == null)
            {
                return false;
            }

            try
            {
                return m.isinCamera(x, y, width, height, marginPixels);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>按名字从该地图取出角色实例（若实际是玩家/敌人会给出对应的具体子类）；找不到返回 <c>null</c>。</summary>
        public GameCharacter FindCharacter(string key)
        {
            M2Mover mover = FindMover(key);
            return mover as M2Attackable == null ? null : GameCharacter.Wrap((M2Attackable)mover);
        }

        /// <summary>按名字从该地图取出敌人实例；名字对应的不是敌人时返回 <c>null</c>。</summary>
        public GameEnemy FindEnemy(string key)
        {
            M2Mover mover = FindMover(key);
            return mover is nel.NelEnemy enemy ? GameEnemy.Wrap(enemy) : null;
        }

        M2Mover FindMover(string key)
        {
            Map2d m = Native;
            if (m == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            try
            {
                // no_error: true——查不到是正常分支，不该让游戏往日志里写错误。
                return m.getMoverByName(key, true);
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
                return map?.key;
            }
            catch (Exception)
            {
                return null;
            }
        }

        TValue Read<TValue>(Func<Map2d, TValue> read, TValue fallback)
        {
            Map2d m = Native;
            if (m == null)
            {
                return fallback;
            }

            try
            {
                return read(m);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        static int SafeGameFrame()
        {
            try
            {
                return XX.IN.totalframe;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
