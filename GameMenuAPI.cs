using System;
using System.Collections.Generic;
using nel;
using nel.gm;
using Polaris.API;

namespace Polaris
{
    /// <summary>
    /// 游戏内 ESC 菜单（<see cref="nel.gm.UiGameMenu"/>）的分类扩展 API，以及原版 ESC 菜单本身的
    /// 打开/关闭与"打开时是否暂停世界"控制。
    /// </summary>
    public class GameMenuAPI
    {
        internal GameMenuAPI() { }

        /// <summary>原版 ESC 菜单当前是否已经激活；待处理的打开请求不计入。</summary>
        public bool IsOpen => GameBinding.NelM2D?.GM?.isActive() ?? false;

        /// <summary>普通 ESC 菜单打开时是否应暂停世界；默认 <c>true</c>，仅在当前进程有效。</summary>
        public bool PauseWorldWhileOpen => GameMenuPauseRuntime.PauseWorldWhileOpen;

        /// <summary>
        /// 请求打开原版 ESC 菜单，语义等同于玩家按下 ESC——不是"无条件冻结世界"，是否暂停世界
        /// 由 <see cref="PauseWorldWhileOpen"/> 决定。成功仅表示原版接受了这次请求：菜单会在本帧
        /// 稍后的 <c>NelM2DBase.runPost()</c> 中才真正激活，调用后立即读 <see cref="IsOpen"/>
        /// 可能仍是 <c>false</c>。
        /// </summary>
        public GameActionResult Pause()
        {
            try
            {
                NelM2DBase m2d = GameBinding.NelM2D;
                if (m2d == null || m2d.GM == null || m2d.curMap == null || m2d.PlayerNoel == null)
                {
                    return GameActionResult.Fail(GameActionStatus.TargetUnavailable, "The game menu is not ready.");
                }

                if (m2d.GM.isActive() || m2d.menu_open_ == NelM2DBase.MENU_OPEN.OPEN)
                {
                    return GameActionResult.Ok();
                }

                if (!CanRequestNormalMenu(m2d))
                {
                    return GameActionResult.Fail(GameActionStatus.RejectedByState, "The current game state does not allow the ESC menu.");
                }

                m2d.menu_open = NelM2DBase.MENU_OPEN.OPEN;
                if (m2d.menu_open_ != NelM2DBase.MENU_OPEN.OPEN)
                {
                    return GameActionResult.Fail(GameActionStatus.RejectedByState, "The game rejected the ESC menu request.");
                }

                return GameActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameMenu.Pause", typeof(GameMenuAPI).Assembly);
                return GameActionResult.Fail(GameActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>
        /// 取消待处理的 ESC 菜单打开请求，或关闭已激活的原版 ESC 菜单（<c>UiGameMenu.deactivate(false)</c>，
        /// 完整原版收尾）。不会恢复事件、转场或其它系统各自拥有的暂停。
        /// </summary>
        public GameActionResult Resume()
        {
            try
            {
                NelM2DBase m2d = GameBinding.NelM2D;
                if (m2d == null || m2d.GM == null)
                {
                    return GameActionResult.Fail(GameActionStatus.TargetUnavailable, "The game menu is not ready.");
                }

                if (!m2d.GM.isActive() && m2d.menu_open_ == NelM2DBase.MENU_OPEN.OPEN)
                {
                    m2d.menu_open = NelM2DBase.MENU_OPEN.NONE;
                    return GameActionResult.Ok();
                }

                if (!m2d.GM.isActive())
                {
                    return GameActionResult.Ok();
                }

                if (!CanCloseAsEscMenu(m2d.GM))
                {
                    return GameActionResult.Fail(GameActionStatus.RejectedByState, "The active menu is in a non-interruptible state.");
                }

                m2d.GM.deactivate(false);
                return GameActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameMenu.Resume", typeof(GameMenuAPI).Assembly);
                return GameActionResult.Fail(GameActionStatus.Failed, ex.Message);
            }
        }

        /// <summary>
        /// 设置 ESC 菜单打开时是否暂停世界。<c>true</c>（默认）保持原版行为；<c>false</c> 时菜单
        /// 打开期间地图、角色、物理与世界绘制继续推进，菜单仍占用 UI 输入。这是进程级全局状态，
        /// 不区分调用方，也不持久化。
        /// </summary>
        public GameActionResult SetWorldPause(bool enabled)
        {
            if (!GameMenuPauseRuntime.FeatureAvailable)
            {
                return GameActionResult.Unsupported("ESC-menu world-pause control is unavailable in this game version.");
            }

            try
            {
                GameMenuPauseRuntime.SetPolicy(enabled);
                return GameActionResult.Ok();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "GameMenu.SetWorldPause", typeof(GameMenuAPI).Assembly);
                return GameActionResult.Fail(GameActionStatus.Failed, ex.Message);
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

        // 分配给自定义分类的 CATEG 整数值不单独存字段，而是每次按"VanillaCategoryCount +
        // 在 categories 里的位置"现算——这样 insertIndex 插到中间时，后面几个分类的
        // CATEG 值自然跟着挪动，不需要额外维护一份重编号逻辑。
        readonly List<CategoryRegistration> categories = [];

        /// <summary>
        /// 在游戏菜单左侧追加一个分类。分类内容用 <paramref name="buildContent"/> 直接往该
        /// 分类专属的内容区（等价于原版 UiGMC.BxR）里摆放控件——与游戏本体写
        /// UiGMCXxx.initAppearMain() 时用的是同一套 Designer API
        /// （BxR.addButton/addP/addHr/...）。
        /// </summary>
        /// <param name="name">分类内部标识（供以后查询用；不要求唯一，仅作标记）</param>
        /// <param name="displayName">左侧分类按钮显示文案，直接作为 skin_title，不经过 TX 本地化表</param>
        /// <param name="buildContent">分类被打开时（每次 appearCategory）如何填充内容区</param>
        /// <param name="canEnter">是否允许进入该分类；默认始终允许。返回 false 时表现与原版
        /// "锁定"分类一致（画面震动 + locked 音效）</param>
        /// <param name="insertIndex">在已注册的自定义分类中的插入位置（0 表示插在原版 10 个
        /// 分类之后的第一个），-1 为在最后追加（默认为-1）；插到中间会顺带挪动排在它后面的
        /// 自定义分类的 CATEG 值</param>
        /// <returns>分配到的 CATEG 整数值（10, 11, 12, ...）</returns>
        /// <exception cref="ArgumentException">当插入位置非法时抛出</exception>
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

        // 分类越多，每行越矮；矮到阈值以下就不再压缩，改为固定行高 + 滚动条。
        // 阈值本身不对外暴露：这是"看起来还能不能点"的视觉判断，不是需要 mod 决定的语义。
        internal const int ScrollThreshold = 14;

        internal static int TotalCategoryCount => VanillaCategoryCount + PolarisAPI.GameMenu.categories.Count;

        /// <summary>
        /// 供 Patch_UiGameMenu_remakeLeftCategories 的 transpiler 替换原版硬编码的行高除数
        /// "10f"：分类数在阈值内按总数等比压缩；超过阈值后固定在阈值对应的行高，多出的分类
        /// 交给 <c>BxCategory.use_scroll</c>。
        /// </summary>
        public static float CategoryRowDivisor() => Math.Min(TotalCategoryCount, ScrollThreshold);

        internal static bool ShouldScrollCategories => TotalCategoryCount > ScrollThreshold;
    }
}
