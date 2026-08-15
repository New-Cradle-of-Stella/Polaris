using HarmonyLib;
using nel;
using Polaris.Settings;
using XX;

namespace Polaris.Patch
{
    /// <summary>
    /// 把 Polaris 的设置项渲染挂进原版设置界面，通过改写构造函数的 <c>ref</c> 参数
    /// <c>_FnDesignerCreateAfter</c>（原版扩展口）实现，链式调用而非替换，且排在原委托之前。
    /// </summary>
    [HarmonyPatch(typeof(UiCFG), MethodType.Constructor,
        typeof(UiBoxDesigner), typeof(UiBoxDesigner), typeof(Designer), typeof(bool), typeof(bool),
        typeof(UiCFG.FnCfgTabCreateAfter), typeof(bool))]
    internal static class Patch_UiCFG_Constructor
    {
        static void Prefix(UiCFG __instance, UiBoxDesigner _Bx, bool _is_title,
                           ref UiCFG.FnCfgTabCreateAfter _FnDesignerCreateAfter)
        {
            UiCFG.FnCfgTabCreateAfter original = _FnDesignerCreateAfter;

            _FnDesignerCreateAfter = (Designer tab, string key) =>
            {
                if (key == UiCFG.tab_main)
                {
                    PolarisSettingsScreen.Append(__instance);
                }

                original?.Invoke(tab, key);
            };

            // 此刻的值即"取消"要回滚到的基准。
            SettingsStore.Snapshot();

            // 必须在构造函数用 BxOut.use_h 定高之前缩面板，否则滚动区不会跟着缩。
            if (SettingsSearchWindow.Wanted(_is_title))
            {
                SettingsSearchWindow.ShrinkPanel(_Bx);
            }
        }

        /// <summary>设置项已画完，登记表是新鲜的，可以摆出搜索框了。</summary>
        static void Postfix(UiBoxDesigner _Bx, bool _is_title)
        {
            // 条件须与 Prefix 一致，否则缩了面板却不摆搜索框会留白。
            if (SettingsSearchWindow.Wanted(_is_title))
            {
                SettingsSearchWindow.ShowUnder(_Bx);
            }
        }
    }
}
