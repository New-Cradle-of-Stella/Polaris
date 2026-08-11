using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using nel.title;

namespace Polaris.Patch
{
    /// <summary>
    /// 将 MainMenuAPI 中注册的按钮列表写入主菜单，替换游戏原有的固定4按钮布局
    /// </summary>
    [HarmonyPatch(typeof(SceneTitleTemp), "initButtons")]
    internal static class Patch_SceneTitleTemp_initButtons
    {
        // Atop_btn_keys 是 readonly 字段：Publicizer 只放宽可见性，不会去掉 readonly，
        // C# 语言层面仍然禁止在声明类型之外给它赋值，因此这里必须继续走
        // FieldInfo.SetValue（Harmony 生成 Ldfld IL 指令本身也要求一个 FieldInfo
        // 操作数，transpiler 里的 LoadButtonCount 用得到）；字段已经公开，用 nameof
        // 让这个字符串编译期可校验。
        static readonly FieldInfo AtopBtnKeysField = AccessTools.Field(typeof(SceneTitleTemp), nameof(SceneTitleTemp.Atop_btn_keys));

        [HarmonyPrefix]
        static void Prefix(SceneTitleTemp __instance)
        {
            PolarisAPI.MainMenu.Current = __instance;
            AtopBtnKeysField.SetValue(__instance, PolarisAPI.MainMenu.BuildButtonKeys());
        }

        /// <summary>
        /// 按钮创建/重建完成后修正换行末行的居中位置。initButtons 本身有
        /// `if (!(BxTop != null)) return;` 守卫，只有首次真正建好按钮的那次调用才会创建
        /// BConTop；这里无条件调用 MainMenuAPI.CenterTopRow，非首次调用时它内部会因为
        /// BConTop 未变化而重复算出同样的结果，是幂等的，没有副作用。语言切换触发的
        /// 重建修正见 Patch_SceneTitleTemp_fineTexts。
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(SceneTitleTemp __instance)
        {
            MainMenuAPI.CenterTopRow(__instance);
        }

        /// <summary>
        /// 原方法里以下几处都按原版固定 4 个按钮硬编码，这里改为在运行时跟随实际注册的按钮
        /// 数量动态计算：
        /// - 顶部容器纵向定位（134px）与高度（54px）：按钮数超过每行上限后要换行，容器要跟着
        ///   变高；见 MainMenuAPI.TopRowY/TopRowHeight 的注释，保持底边不动、向上增高。
        /// - 按钮列数（clms=4）与按钮宽度分母（/4f）：不能直接用总按钮数，否则按钮数一多，
        ///   列数跟着无限增多、单行挤下所有按钮，按钮越加越窄——改成
        ///   MainMenuAPI.ButtonColumns 算出的、不超过 MaxButtonsPerRow 的列数，超过上限后
        ///   自动换行，按钮尺寸就不再随之收缩。
        /// - 按钮池容量（new List&lt;aBtn&gt;(4)）：这个是全部按钮的容量，不是列数，维持原来的
        ///   "总按钮数" 语义不变，继续用 Atop_btn_keys.Length。
        /// </summary>
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            // pixel_y 的 134f -> MainMenuAPI.TopRowY(Atop_btn_keys.Length)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldsfld),
                                          new CodeMatch(OpCodes.Neg),
                                          new CodeMatch(OpCodes.Ldc_R4))
                       .ThrowIfInvalid("未找到顶部按钮容器纵向定位常量 134 的 IL 模式")
                       .Advance(2)
                       .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                       .InsertAndAdvance(LoadButtonCount())
                       .InsertAndAdvance(CodeInstruction.Call(typeof(MainMenuAPI), nameof(MainMenuAPI.TopRowY)));

            // pixel_h 的 54f -> MainMenuAPI.TopRowHeight(Atop_btn_keys.Length)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Conv_R4),
                                          new CodeMatch(OpCodes.Ldc_R4),
                                          new CodeMatch(OpCodes.Ldc_I4_1))
                       .ThrowIfInvalid("未找到顶部按钮容器高度常量 54 的 IL 模式")
                       .Advance(1)
                       .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                       .InsertAndAdvance(LoadButtonCount())
                       .InsertAndAdvance(CodeInstruction.Call(typeof(MainMenuAPI), nameof(MainMenuAPI.TopRowHeight)));

            // clms = 4 -> clms = MainMenuAPI.ButtonColumns(Atop_btn_keys.Length)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Stfld),
                                          new CodeMatch(OpCodes.Dup),
                                          new CodeMatch(OpCodes.Ldc_I4_4))
                       .Advance(2)
                       .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                       .InsertAndAdvance(LoadButtonColumns());

            // w = (num - 40f - 4f) / 4f -> 分母改为 (float)MainMenuAPI.ButtonColumns(Atop_btn_keys.Length)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Sub),
                                          new CodeMatch(OpCodes.Ldc_R4),
                                          new CodeMatch(OpCodes.Sub),
                                          new CodeMatch(OpCodes.Ldc_R4))
                       .Advance(3)
                       .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                       .InsertAndAdvance(LoadButtonColumns())
                       .InsertAndAdvance(new CodeInstruction(OpCodes.Conv_R4));

            // new List<aBtn>(4) -> new List<aBtn>(Atop_btn_keys.Length)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldfld),
                                          new CodeMatch(OpCodes.Ldc_I4_4))
                       .Advance(1)
                       .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                       .InsertAndAdvance(LoadButtonCount());

            return codeMatcher.Instructions();
        }

        /// <summary>
        /// 发出 `Atop_btn_keys.Length` 的 IL（承接前一条 Ldarg_0）。每次调用都返回全新的
        /// CodeInstruction 实例——CodeMatcher 会给插入的指令挂标签/改写内容，复用同一批
        /// 实例会让多个插入点互相干扰。
        /// </summary>
        static CodeInstruction[] LoadButtonCount()
        {
            return
            [
                new CodeInstruction(OpCodes.Ldfld, AtopBtnKeysField),
                new CodeInstruction(OpCodes.Ldlen),
                new CodeInstruction(OpCodes.Conv_I4),
            ];
        }

        /// <summary>
        /// 发出 `MainMenuAPI.ButtonColumns(Atop_btn_keys.Length)` 的 IL（承接前一条 Ldarg_0），
        /// 即换行后每行实际使用的列数（不超过 MaxButtonsPerRow）。
        /// </summary>
        static CodeInstruction[] LoadButtonColumns()
        {
            return
            [
                new CodeInstruction(OpCodes.Ldfld, AtopBtnKeysField),
                new CodeInstruction(OpCodes.Ldlen),
                new CodeInstruction(OpCodes.Conv_I4),
                CodeInstruction.Call(typeof(MainMenuAPI), nameof(MainMenuAPI.ButtonColumns)),
            ];
        }
    }
}
