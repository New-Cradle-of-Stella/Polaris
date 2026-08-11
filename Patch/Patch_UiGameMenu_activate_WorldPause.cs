using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using m2d;
using nel.gm;
using Polaris.API;

namespace Polaris.Patch
{
    /// <summary>
    /// 把 <c>UiGameMenu.activate()</c> 里唯一一次 <c>M2D.PauseMem(true)</c> 调用改接到
    /// <see cref="GameMenuPauseRuntime.OnMenuPauseMemory"/>：策略为 <c>false</c> 时跳过这次
    /// PauseMem，让世界继续跑；其它初始化代码原样执行。
    /// </summary>
    [HarmonyPatch(typeof(UiGameMenu), nameof(UiGameMenu.activate))]
    internal static class Patch_UiGameMenu_activate_WorldPause
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var pauseMem = AccessTools.Method(typeof(M2DBase), nameof(M2DBase.PauseMem), new[] { typeof(bool) });
            var replacement = AccessTools.Method(typeof(GameMenuPauseRuntime), nameof(GameMenuPauseRuntime.OnMenuPauseMemory));

            var codeMatcher = new CodeMatcher(instructions);

            codeMatcher.MatchStartForward(new CodeMatch(ins =>
                    (ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt) && ins.OperandIs(pauseMem)))
                .ThrowIfInvalid("Could not find the M2D.PauseMem(bool) call inside UiGameMenu.activate")
                .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, replacement));

            GameMenuPauseRuntime.ReportPatchApplied(GameMenuPauseRuntime.PatchTarget.Activate);
            return codeMatcher.Instructions();
        }
    }
}
