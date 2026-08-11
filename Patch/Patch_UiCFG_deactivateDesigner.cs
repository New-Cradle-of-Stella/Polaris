using HarmonyLib;
using nel;
using Polaris.Settings;

namespace Polaris.Patch
{
    /// <summary>
    /// 设置界面收起：清掉搜索框里的查询、把被过滤掉的行放回来，标题画面那边再顺手收起搜索框窗口。
    /// <para>
    /// 挂在 <c>deactivateDesigner</c> 上是因为它是标题画面与 ESC 菜单<b>共同</b>的收起入口
    /// （<c>SceneTitleTemp.changeState</c> 离开 CONFIG 时、<c>UiGMCCfg.quitEdit</c> 里各调一次），
    /// 而且此刻 <c>BxOut</c> 还活着——撤销过滤要靠它查块的行记录。
    /// </para>
    /// <para>
    /// 去按键设置页（KEYCON）也会路过这里，于是搜索会被清掉。这是刻意选的：那条路很少走，
    /// 而"关掉设置界面之后搜索还留着"会让下次打开时看到一份缺了大半的设置页，代价大得多。
    /// 回来时走 <c>resume()</c>，见 <see cref="Patch_UiCFG_resume"/>。
    /// </para>
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
