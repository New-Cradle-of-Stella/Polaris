using HarmonyLib;
using nel;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// 设置界面收起时清掉搜索查询、还原被过滤的行，标题画面顺带收起搜索框窗口。
    /// 去按键设置页也会路过这里并清空搜索，是刻意的取舍（回来靠 <see cref="Patch_UiCFG_resume"/> 重建）。
    /// </summary>
    [HarmonyPatch(typeof(UiCFG), nameof(UiCFG.deactivateDesigner))]
    internal static class Patch_UiCFG_deactivateDesigner
    {
        static void Postfix(UiCFG __instance)
        {
            SettingsSearchBox.Reset();

            if (__instance.is_title)
            {
                SettingsSearchWindow.Hide();
            }
        }
    }
}
