using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using nel;
using nel.gm;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// 把 <c>runPostForDraw</c> 里决定是否继续绘制世界的 <c>GM.isStoppingGame()</c> 替换成
    /// <see cref="GameMenuPauseRuntime.ShouldStopWorld"/>，须与 <see cref="Patch_NelM2DBase_run_GameMenuWorldPause"/>
    /// 保持一致，否则会出现逻辑跑、画面却冻结的半工作状态。
    /// </summary>
    [HarmonyPatch(typeof(NelM2DBase), nameof(NelM2DBase.runPostForDraw), new[] { typeof(float), typeof(bool), typeof(bool) })]
    internal static class Patch_NelM2DBase_runPostForDraw_GameMenuWorldPause
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var isStoppingGame = AccessTools.Method(typeof(UiGameMenu), nameof(UiGameMenu.isStoppingGame));
            var replacement = AccessTools.Method(typeof(GameMenuPauseRuntime), nameof(GameMenuPauseRuntime.ShouldStopWorld));

            var codeMatcher = new CodeMatcher(instructions);

            codeMatcher.MatchStartForward(new CodeMatch(ins =>
                    (ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt) && ins.OperandIs(isStoppingGame)))
                .ThrowIfInvalid("Could not find the GM.isStoppingGame() call inside NelM2DBase.runPostForDraw")
                .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, replacement));

            GameMenuPauseRuntime.ReportPatchApplied(GameMenuPauseRuntime.PatchTarget.RunPostForDraw);
            return codeMatcher.Instructions();
        }
    }
}
