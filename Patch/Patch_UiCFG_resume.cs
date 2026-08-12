using HarmonyLib;
using nel;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// 重新打开设置界面时把设置项当前值拨回控件显示，并重拍回滚快照。
    /// 需要这一步是因为 <c>UiCFG</c> 实例只 new 一次、之后靠 <c>resume()</c> 复用，界面可能还留着旧显示。
    /// </summary>
    [HarmonyPatch(typeof(UiCFG), nameof(UiCFG.resume))]
    internal static class Patch_UiCFG_resume
    {
        static void Postfix(UiCFG __instance)
        {
            PolarisSettingsScreen.Sync(__instance);
            SettingsStore.Snapshot();

            // 标题画面从按键设置页退回来时把搜索框亮回来；ESC 菜单的搜索框跟菜单一起收放，无需处理。
            if (__instance.is_title)
            {
                SettingsSearchWindow.Resume();
            }
        }
    }
}
