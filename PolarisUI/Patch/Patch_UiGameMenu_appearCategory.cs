using HarmonyLib;
using nel.gm;
using XX;

namespace Polaris.Patch
{
    /// <summary>
    /// ct 落在 GameMenuAPI 注册的自定义分类范围内时接管显示并 <c>return false</c> 跳过原版方法
    /// （否则未知 CATEG 值只会落到 default 分支显示占位文案）；因此要手动补全原版在 switch
    /// 前后做的状态同步（尤其是 <c>appear_categ = ct</c>），否则确认/焦点/重复切换会互相打架。
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
