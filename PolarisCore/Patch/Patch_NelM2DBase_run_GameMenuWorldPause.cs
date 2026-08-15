using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using nel;
using nel.gm;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// 把 <c>NelM2DBase.run()</c> 里两处 <c>GM.isStoppingGame()</c> 调用都替换成
    /// <see cref="GameMenuPauseRuntime.ShouldStopWorld"/>；其它分支（Game Over、读档、事件等）保持原版不变。
    /// </summary>
    [HarmonyPatch(typeof(NelM2DBase), nameof(NelM2DBase.run), new[] { typeof(float) })]
    internal static class Patch_NelM2DBase_run_GameMenuWorldPause
    {
        const int ExpectedMatchCount = 2;

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var isStoppingGame = AccessTools.Method(typeof(UiGameMenu), nameof(UiGameMenu.isStoppingGame));
            var replacement = AccessTools.Method(typeof(GameMenuPauseRuntime), nameof(GameMenuPauseRuntime.ShouldStopWorld));

            var codeMatcher = new CodeMatcher(instructions);

            for (int i = 0; i < ExpectedMatchCount; i++)
            {
                codeMatcher.MatchStartForward(new CodeMatch(ins =>
                        (ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt) && ins.OperandIs(isStoppingGame)))
                    .ThrowIfInvalid($"Could not find GM.isStoppingGame() call #{i + 1} inside NelM2DBase.run")
                    .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, replacement));
            }

            GameMenuPauseRuntime.ReportPatchApplied(GameMenuPauseRuntime.PatchTarget.Run);
            return codeMatcher.Instructions();
        }
    }
}
