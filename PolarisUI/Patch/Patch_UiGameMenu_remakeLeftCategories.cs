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

            // 强制 Clear()+init() 一次，让刚设置的 use_scroll 在本次重建里就生效。
            __instance.BxCategory.Clear();
            __instance.BxCategory.init();
        }

        /// <summary>
        /// 追加自定义分类按钮，title 沿用原版 "categ_" + 下标前缀，从而复用原版三个回调
        /// （它们只按 title 解析整数，无范围检查），选中/高亮/状态同步都天然正确。
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
        /// 把行高计算里硬编码的分母 10 换成运行时的 GameMenuAPI.CategoryRowDivisor()，
        /// 使行高跟随实际注册的分类总数；不改循环上界，原版 0..9 按钮仍由原循环自己建。
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
