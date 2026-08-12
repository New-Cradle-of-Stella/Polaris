using HarmonyLib;
using nel;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// 设置界面"确定"时把 Polaris 的设置项落盘，对应原版 <c>submitData</c> 里的 <c>CFG.saveSdFile()</c>。
    /// 值在玩家改动时已即时生效，这里只负责写磁盘（<see cref="SettingsStore"/> 已关闭自动保存）。
    /// </summary>
    [HarmonyPatch(typeof(UiCFG), nameof(UiCFG.submitData))]
    internal static class Patch_UiCFG_submitData
    {
        static void Postfix() => SettingsStore.Commit();
    }
}
