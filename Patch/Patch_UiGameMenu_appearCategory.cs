using HarmonyLib;
using nel.gm;
using XX;

namespace Polaris.Patch
{
    /// <summary>
    /// ct 落在 GameMenuAPI 注册的自定义分类范围内时接管显示（原版 switch 对未知 CATEG 值
    /// 只会落到 default 分支显示"施工中"占位文案）；否则放行给原版 0-9 分支。
    /// <para>
    /// Prefix 完全跳过原版方法（<c>return false</c>），所以要把原版在 switch 前后做的状态
    /// 同步补全——尤其是 <c>appear_categ = ct</c>：原版后续按确认/取消返回、重复点击同一
    /// 分类、以及 <c>waiting_categ_for_</c> 等待流程，全部拿 <c>appear_categ</c> 跟
    /// <c>select_categ</c>/<c>waiting_categ_for_</c> 比较；这里不写回的话游戏会一直以为
    /// 当前分类还是 <c>_NOUSE</c>，导致键盘确认、焦点、返回、重复切换互相打架。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(UiGameMenu), nameof(UiGameMenu.appearCategory))]
    internal static class Patch_UiGameMenu_appearCategory
    {
        [HarmonyPrefix]
        static bool Prefix(UiGameMenu __instance, CATEG ct, bool force)
        {
            if (!PolarisAPI.GameMenu.TryGetCategory((int)ct, out GameMenuAPI.CategoryRegistration reg))
            {
                return true;
            }

            IN.clearPushDown(true);
            if (!force && __instance.appear_categ == ct)
            {
                return false;
            }
            if (!force && __instance.appear_categ != ct && __instance.af >= (float)(__instance.BXR_DELAYT + 2))
            {
                SND.Ui.play("tool_changegear", false);
            }

            __instance.quitAppearCategory();
            __instance.EditFocusInitTo = null;
            __instance.appear_categ = ct;
            __instance.AppearC = __instance.AGmcCache[(int)ct] ??= new GameMenuCategoryController(__instance, ct, reg);
            __instance.BxRRemake(force);
            __instance.AppearC?.initAppearWhole();

            if (__instance.waiting_categ_for_ != CATEG._NOUSE)
            {
                if (ct != __instance.waiting_categ_for_)
                {
                    __instance.BxR.hide();
                }
                else
                {
                    __instance.BxR.bind();
                }
            }

            return false;
        }
    }
}
