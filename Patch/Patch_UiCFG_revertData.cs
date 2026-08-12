using HarmonyLib;
using nel;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// 设置界面"取消"时回滚 Polaris 的设置项，只改内存不写盘（对应原版从快照恢复 <c>CFG</c> 的那一步）。
    /// 控件显示不在这里拨正，留给下次 <c>resume()</c>（见 <see cref="Patch_UiCFG_resume"/>）统一同步。
    /// </summary>
    [HarmonyPatch(typeof(UiCFG), nameof(UiCFG.revertData))]
    internal static class Patch_UiCFG_revertData
    {
        static void Postfix() => SettingsStore.Revert();
    }
}
