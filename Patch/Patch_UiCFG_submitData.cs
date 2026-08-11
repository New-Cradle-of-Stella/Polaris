using HarmonyLib;
using nel;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// 设置界面"确定"时把 Polaris 的设置项落盘，对应原版 <c>submitData</c> 里的 <c>CFG.saveSdFile()</c>。
    /// 标题画面点确定、ESC 菜单 <c>UiGMCCfg.quitEdit</c> 退出时都会走到这里。
    /// <para>
    /// 值本身在玩家改动的那一刻就已经生效了（与原版拖音量条即时听到效果一致），
    /// 这里只负责写磁盘——所以 <see cref="SettingsStore"/> 把 ConfigFile 的
    /// <c>SaveOnConfigSet</c> 关掉了。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(UiCFG), nameof(UiCFG.submitData))]
    internal static class Patch_UiCFG_submitData
    {
        static void Postfix() => SettingsStore.Commit();
    }
}
