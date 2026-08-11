using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using nel;
using nel.gm;
using XX;

namespace Polaris.Patch
{
    /// <summary>
    /// 在原版左侧分类列表（categ_0..categ_9）之后追加通过 GameMenuAPI.AddCategory 注册的
    /// 自定义分类；分类过多时改用固定行高 + 滚动条，而不是无限压缩行高。
    /// </summary>
    [HarmonyPatch(typeof(UiGameMenu), nameof(UiGameMenu.remakeLeftCategories))]
    internal static class Patch_UiGameMenu_remakeLeftCategories
    {
        [HarmonyPrefix]
        static void Prefix(UiGameMenu __instance)
        {
            __instance.BxCategory.use_scroll = GameMenuAPI.ShouldScrollCategories;

            // 原版只在 remaking=true 时才 Clear()+init()；这里强制做一次，保证刚设置的
            // use_scroll 在本次重建里就生效，而不是等下一次语言切换之类的 remaking=true 调用。
            __instance.BxCategory.Clear();
            __instance.BxCategory.init();
        }

        /// <summary>
        /// 追加自定义分类按钮。title 和原版一样用 "categ_" + 下标前缀，从而可以直接复用
        /// 原版的 fnHoverCategory/fnClickCategory/fnOutCategory 三个回调——它们只按
        /// title 解析整数，没有"必须在 0-9 之间"的检查，选中/悬停高亮、select_categ 同步、
        /// waiting_categ_for 门控都天然正确，不需要额外补丁去修复状态不同步的问题。
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(UiGameMenu __instance)
        {
            UiBoxDesigner bxCategory = __instance.BxCategory;
            IReadOnlyList<GameMenuAPI.CategoryRegistration> categories = PolarisAPI.GameMenu.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                GameMenuAPI.CategoryRegistration reg = categories[i];
                int categIndex = GameMenuAPI.VanillaCategoryCount + i;
                bxCategory.addButton(new DsnDataButton
                {
                    name = "categ_" + categIndex,
                    title = "categ_" + categIndex,
                    skin = "ui_category",
                    skin_title = reg.DisplayName,
                    w = bxCategory.use_w,
                    h = (bxCategory.h - bxCategory.margin_in_tb) / GameMenuAPI.CategoryRowDivisor() - 8f,
                    hover_to_select = true,
                    navi_auto_fill = false,
                    fnHover = __instance.fnHoverCategory,
                    fnOut = __instance.fnOutCategory,
                    fnClick = __instance.fnClickCategory,
                });
                bxCategory.Br();
            }
        }

        /// <summary>
        /// 原方法里分类行高的分母硬编码为 10（对应原版固定 10 个分类）；这里改为在运行时
        /// 调用 GameMenuAPI.CategoryRowDivisor()，使行高跟随实际注册的分类总数（并在超过
        /// 滚动阈值后不再继续压缩）。不改循环上界——原版 0..9 那 10 个按钮仍由原循环自己建，
        /// 自定义分类只在 Postfix 里追加，不侵入这个循环。
        /// </summary>
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            // (BxCategory.h - BxCategory.margin_in_tb) / 10f -> ... / GameMenuAPI.CategoryRowDivisor()
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld),
                                          new CodeMatch(OpCodes.Ldfld),
                                          new CodeMatch(OpCodes.Sub),
                                          new CodeMatch(OpCodes.Ldc_R4))
                       .ThrowIfInvalid("Could not find the IL pattern for the remakeLeftCategories row height divisor")
                       .Advance(3)
                       .SetInstructionAndAdvance(CodeInstruction.Call(typeof(GameMenuAPI), nameof(GameMenuAPI.CategoryRowDivisor)));

            return codeMatcher.Instructions();
        }
    }
}
