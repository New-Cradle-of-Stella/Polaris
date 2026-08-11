using HarmonyLib;
using nel;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// 重新打开设置界面时，把设置项的当前值拨回控件显示，并重新拍一张回滚快照。
    /// <para>
    /// 原版的 <c>UiCFG</c> 实例只 new 一次，之后标题画面和 ESC 菜单都走 <c>resume()</c> 复用，
    /// 所以两次打开之间模组自己改了值、或者上次是"取消"退出的，界面上还留着旧显示——
    /// 得在这里同步回来。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(UiCFG), nameof(UiCFG.resume))]
    internal static class Patch_UiCFG_resume
    {
        static void Postfix(UiCFG __instance)
        {
            PolarisSettingsScreen.Sync(__instance);
            SettingsStore.Snapshot();

            // 标题画面从按键设置页退回来：UiCFG 没被 destruct，内容还在，只要把搜索框亮回来。
            // 游戏内不用管——ESC 菜单的搜索框在原版底部子区里，跟着菜单一起收放。
            if (__instance.is_title)
            {
                SettingsSearchWindow.Resume();
            }
        }
    }
}
