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
        // Atop_btn_keys 是 readonly 字段，Publicizer 不会去掉 readonly，所以仍需通过 FieldInfo.SetValue 赋值。
        static readonly FieldInfo AtopBtnKeysField = AccessTools.Field(typeof(SceneTitleTemp), nameof(SceneTitleTemp.Atop_btn_keys));

        [HarmonyPrefix]
        static void Prefix(SceneTitleTemp __instance)
        {
            PolarisAPI.MainMenu.Current = __instance;
            AtopBtnKeysField.SetValue(__instance, PolarisAPI.MainMenu.BuildButtonKeys());
        }

        /// <summary>
        /// 按钮创建/重建完成后修正换行末行的居中位置；CenterTopRow 是幂等的，重复调用无副作用。
        /// 语言切换触发的重建修正见 <see cref="Patch_SceneTitleTemp_fineTexts"/>。
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(SceneTitleTemp __instance)
        {
            MainMenuAPI.CenterTopRow(__instance);
        }

        /// <summary>
        /// 原方法按固定 4 按钮硬编码容器定位/高度、列数、按钮宽度分母、列表容量，这里改为按
        /// <c>Atop_btn_keys.Length</c>（实际注册的按钮数）动态计算，列数额外经 <c>MainMenuAPI.ButtonColumns</c>
        /// 限制在 MaxButtonsPerRow 以内，避免按钮数一多导致单行挤爆、按钮越加越窄。
        /// </summary>
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            // pixel_y 的 134f -> MainMenuAPI.TopRowY(Atop_btn_keys.Length)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Ldsfld),
                                          new CodeMatch(OpCodes.Neg),
                                          new CodeMatch(OpCodes.Ldc_R4))
                       .ThrowIfInvalid("Could not find the IL pattern for the top button container vertical position constant 134")
                       .Advance(2)
                       .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                       .InsertAndAdvance(LoadButtonCount())
                       .InsertAndAdvance(CodeInstruction.Call(typeof(MainMenuAPI), nameof(MainMenuAPI.TopRowY)));

            // pixel_h 的 54f -> MainMenuAPI.TopRowHeight(Atop_btn_keys.Length)
            codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Conv_R4),
                                          new CodeMatch(OpCodes.Ldc_R4),
                                          new CodeMatch(OpCodes.Ldc_I4_1))
                       .ThrowIfInvalid("Could not find the IL pattern for the top button container height constant 54")
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
        /// 发出 `Atop_btn_keys.Length` 的 IL（承接前一条 Ldarg_0）。每次调用返回全新实例，
        /// 避免多个插入点共享同一批 CodeInstruction 而互相干扰。
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
