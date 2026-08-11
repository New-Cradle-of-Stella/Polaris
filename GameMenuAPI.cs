using System;
using System.Collections.Generic;
using nel;

namespace Polaris
{
    /// <summary>
    /// 游戏内 ESC 菜单（<see cref="nel.gm.UiGameMenu"/>）的分类扩展 API。
    /// </summary>
    public class GameMenuAPI
    {
        internal GameMenuAPI() { }

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
