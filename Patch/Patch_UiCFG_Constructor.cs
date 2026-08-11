using HarmonyLib;
using nel;
using Polaris.Settings;
using XX;

namespace Polaris.Patch
{
    /// <summary>
    /// 把 Polaris 的设置项渲染挂进原版设置界面。
    /// <para>
    /// <c>UiCFG</c> 的构造函数第 6 个参数 <c>FnCfgTabCreateAfter</c> 是原版自己留的扩展口：
    /// 它被存进 <c>public readonly FnDesignerCreateAfter</c> 字段，并在
    /// <c>createBoxDesignerContentMain</c> 的末尾（<c>MainBoxRelink()</c> 之前）以
    /// <c>(CurTab, "MAIN")</c> 调用。所以这里只要在 Prefix 里改写这个 <c>ref</c> 参数即可，
    /// 既不用 transpiler，也绕开了字段 readonly 的问题（Publicizer 只放宽可见性，不去掉 readonly）。
    /// </para>
    /// <para>
    /// 必须是<b>链式</b>而不是替换：标题画面（<c>SceneTitleTemp</c>）传的是 null，
    /// 但 ESC 菜单（<c>UiGMCCfg</c>）传了一个非 null 的委托，用来在主页尾部加"返回标题"按钮。
    /// 顺序上 Polaris 排在原委托<b>之前</b>，否则设置项会跑到那个按钮下面去。
    /// </para>
    /// <para>
    /// Prefix 里捕获 <c>__instance</c> 是安全的：构造函数先给 <c>BxOut</c>/<c>BxDesc</c> 赋值，
    /// 之后才调用 <c>createBoxDesignerContentMain</c>，委托真正执行时这些字段已经就绪。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(UiCFG), MethodType.Constructor,
        typeof(UiBoxDesigner), typeof(UiBoxDesigner), typeof(Designer), typeof(bool), typeof(bool),
        typeof(UiCFG.FnCfgTabCreateAfter), typeof(bool))]
    internal static class Patch_UiCFG_Constructor
    {
        static void Prefix(UiCFG __instance, ref UiCFG.FnCfgTabCreateAfter _FnDesignerCreateAfter)
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

            // 界面即将建起来，此刻的值就是"取消"要回滚到的基准。
            SettingsStore.Snapshot();
        }
    }
}
