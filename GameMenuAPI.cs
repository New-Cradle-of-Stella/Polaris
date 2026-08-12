using System;
using System.Collections.Generic;
using nel;
using nel.gm;
using Polaris.API;

namespace Polaris
{
    /// <summary>游戏内 ESC 菜单的分类扩展 API，以及原版 ESC 菜单的打开/关闭与暂停世界控制。</summary>
    public class GameMenuAPI
    {
        internal GameMenuAPI() { }

        /// <summary>原版 ESC 菜单当前是否已经激活；待处理的打开请求不计入。</summary>
        public bool IsOpen => GameBinding.NelM2D?.GM?.isActive() ?? false;

        /// <summary>普通 ESC 菜单打开时是否应暂停世界；默认 <c>true</c>，仅在当前进程有效。</summary>
        public bool PauseWorldWhileOpen => GameMenuPauseRuntime.PauseWorldWhileOpen;

        /// <summary>请求打开原版 ESC 菜单（等同玩家按 ESC）。成功仅表示请求被接受，菜单要到本帧稍后才真正激活，此时 <see cref="IsOpen"/> 可能仍为 <c>false</c>。</summary>
        public PolarisActionResult Pause()
        {
            try
            {
                NelM2DBase m2d = GameBinding.NelM2D;
                if (m2d == null || m2d.GM == null || m2d.curMap == null || m2d.PlayerNoel == null)
                {
                    return PolarisActionResult.Fail(PolarisActionStatus.TargetUnavailable, "The game menu is not ready.");
                }

                if (m2d.GM.isActive() || m2d.menu_open_ == NelM2DBase.MENU_OPEN.OPEN)
                {
                    return PolarisActionResult.Ok();
                }

                if (!CanRequestNormalMenu(m2d))
                {
                    return PolarisActionResult.Fail(PolarisActionStatus.RejectedByState, "The current game state does not allow the ESC menu.");
                }

                m2d.menu_open = NelM2DBase.MENU_OPEN.OPEN;
                if (m2d.menu_open_ != NelM2DBase.MENU_OPEN.OPEN)
                {
                    return PolarisActionResult.Fail(PolarisActionStatus.RejectedByState, "The game rejected the ESC menu request.");
                }

                return PolarisActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameMenu.Pause", typeof(GameMenuAPI).Assembly);
                return PolarisActionResult.Fail(PolarisActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>取消待处理的 ESC 菜单打开请求，或关闭已激活的菜单；不会恢复事件/转场等其它系统的暂停。</summary>
        public PolarisActionResult Resume()
        {
            try
            {
                NelM2DBase m2d = GameBinding.NelM2D;
                if (m2d == null || m2d.GM == null)
                {
                    return PolarisActionResult.Fail(PolarisActionStatus.TargetUnavailable, "The game menu is not ready.");
                }

                if (!m2d.GM.isActive() && m2d.menu_open_ == NelM2DBase.MENU_OPEN.OPEN)
                {
                    m2d.menu_open = NelM2DBase.MENU_OPEN.NONE;
                    return PolarisActionResult.Ok();
                }

                if (!m2d.GM.isActive())
                {
                    return PolarisActionResult.Ok();
                }

                if (!CanCloseAsEscMenu(m2d.GM))
                {
                    return PolarisActionResult.Fail(PolarisActionStatus.RejectedByState, "The active menu is in a non-interruptible state.");
                }

                m2d.GM.deactivate(false);
                return PolarisActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameMenu.Resume", typeof(GameMenuAPI).Assembly);
                return PolarisActionResult.Fail(PolarisActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>设置 ESC 菜单打开时是否暂停世界（默认暂停）。进程级全局状态，不持久化。</summary>
        public PolarisActionResult SetWorldPause(bool enabled)
        {
            if (!GameMenuPauseRuntime.FeatureAvailable)
            {
                return PolarisActionResult.Unsupported("ESC-menu world-pause control is unavailable in this game version.");
            }

            try
            {
                GameMenuPauseRuntime.SetPolicy(enabled);
                return PolarisActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameMenu.SetWorldPause", typeof(GameMenuAPI).Assembly);
                return PolarisActionResult.Fail(PolarisActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>由 <see cref="Plugin.Update"/> 每帧调用：外部暂停（事件/转场）结束后的所有权对账。</summary>
        internal void Pump() => GameMenuPauseRuntime.Pump();

        static bool CanRequestNormalMenu(NelM2DBase m2d)
            => m2d.can_open_gamemenu
               && m2d.pre_map_active
               && !m2d.transferring_game_stopping
               && !m2d.Freezer.isPausing()
               && !m2d.GM.isActive();

        static bool CanCloseAsEscMenu(UiGameMenu gm)
            => !gm.isClosingGame()
               && gm.postype != UiGameMenu.POSTYPE.BENCH
               && !GAMEOVER.isActive();

        /// <summary>一次 <see cref="AddCategory"/> 注册的完整信息。</summary>
        internal sealed class CategoryRegistration
        {
            public string Name;
            public string DisplayName;
            public Action<UiBoxDesigner> BuildContent;
            public Func<bool> CanEnter;
        }

        /// <summary>原版分类数量（STAT..CONFIG，CATEG 的 0..9）；自定义分类从这个值开始顺序编号。</summary>
        internal const int VanillaCategoryCount = 10;

        // CATEG 值不单独存字段，按 VanillaCategoryCount + 位置现算，插入中间时自动跟着挪动。
        readonly List<CategoryRegistration> categories = [];

        /// <summary>在游戏菜单左侧追加一个分类，内容用 <paramref name="buildContent"/> 通过 Designer API 填充。</summary>
        /// <param name="name">分类内部标识，仅作标记，不要求唯一</param>
        /// <param name="displayName">左侧分类按钮显示文案（skin_title，不经本地化表）</param>
        /// <param name="buildContent">分类被打开时如何填充内容区</param>
        /// <param name="canEnter">是否允许进入该分类；返回 false 时表现为原版"锁定"效果</param>
        /// <param name="insertIndex">插入位置（0 为原版分类之后第一个），-1 表示追加到最后</param>
        /// <returns>分配到的 CATEG 整数值（10, 11, 12, ...）</returns>
        /// <exception cref="ArgumentException">插入位置非法时抛出</exception>
        public int AddCategory(string name, string displayName, Action<UiBoxDesigner> buildContent, Func<bool> canEnter = null, int insertIndex = -1)
        {
            var registration = new CategoryRegistration
            {
                Name = name,
                DisplayName = displayName,
                BuildContent = buildContent ?? throw new ArgumentNullException(nameof(buildContent)),
                CanEnter = canEnter ?? (() => true),
            };

            int position;
            if (insertIndex == -1)
            {
                categories.Add(registration);
                position = categories.Count - 1;
            }
            else
            {
                if (insertIndex < 0 || insertIndex > categories.Count)
                {
                    throw new ArgumentException("Illegal category insert position", nameof(insertIndex));
                }
                categories.Insert(insertIndex, registration);
                position = insertIndex;
            }

            return VanillaCategoryCount + position;
        }

        internal IReadOnlyList<CategoryRegistration> Categories => categories;

        internal bool TryGetCategory(int index, out CategoryRegistration reg)
        {
            int position = index - VanillaCategoryCount;
            if (position < 0 || position >= categories.Count)
            {
                reg = null;
                return false;
            }

            reg = categories[position];
            return true;
        }

        // 超过此阈值后行高不再压缩，改为固定行高 + 滚动条（视觉判断，不对外暴露）。
        internal const int ScrollThreshold = 14;

        internal static int TotalCategoryCount => VanillaCategoryCount + PolarisAPI.GameMenu.categories.Count;

        /// <summary>供 transpiler 替换原版硬编码行高除数；超过阈值后固定行高，改用滚动条。</summary>
        public static float CategoryRowDivisor() => Math.Min(TotalCategoryCount, ScrollThreshold);

        internal static bool ShouldScrollCategories => TotalCategoryCount > ScrollThreshold;
    }
}
