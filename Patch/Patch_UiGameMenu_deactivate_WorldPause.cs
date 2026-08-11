using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using m2d;
using nel.gm;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// 把 <c>UiGameMenu.deactivate()</c> 里唯一一次 <c>M2D.ResumeMem(true)</c> 调用改接到
    /// <see cref="GameMenuPauseRuntime.OnMenuResumeMemory"/>：只有 Polaris 确认本次菜单路径
    /// 真正执行过 PauseMem，这里才有资格配对调用 ResumeMem；否则跳过，把恢复权交给外部暂停路径。
    /// </summary>
    [HarmonyPatch(typeof(UiGameMenu), nameof(UiGameMenu.deactivate))]
    internal static class Patch_UiGameMenu_deactivate_WorldPause
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var resumeMem = AccessTools.Method(typeof(M2DBase), nameof(M2DBase.ResumeMem), new[] { typeof(bool) });
            var replacement = AccessTools.Method(typeof(GameMenuPauseRuntime), nameof(GameMenuPauseRuntime.OnMenuResumeMemory));

            var codeMatcher = new CodeMatcher(instructions);

            codeMatcher.MatchStartForward(new CodeMatch(ins =>
                    (ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt) && ins.OperandIs(resumeMem)))
                .ThrowIfInvalid("Could not find the M2D.ResumeMem(bool) call inside UiGameMenu.deactivate")
                .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, replacement));

            GameMenuPauseRuntime.ReportPatchApplied(GameMenuPauseRuntime.PatchTarget.Deactivate);
            return codeMatcher.Instructions();
        }
    }
}
