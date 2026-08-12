using HarmonyLib;
using nel.title;

namespace Polaris.Patch
{
    /// <summary>
    /// 把 <see cref="TitleOverlays"/>（Polaris 的一次性标题告知页）接入原版"进标题菜单前先弹告知框"的闸门：
    /// <c>errorAnnounceBox</c> 返回 true 时顶部按钮行不会激活，本补丁在原版没有告知要弹时接管返回值，
    /// 让 Polaris 告知页占住同一个位置。
    /// </summary>
    [HarmonyPatch(typeof(SceneTitleTemp), nameof(SceneTitleTemp.errorAnnounceBox))]
    internal static class Patch_SceneTitleTemp_errorAnnounceBox
    {
        [HarmonyPostfix]
        static void Postfix(SceneTitleTemp __instance, bool switch_to_top_state, ref bool __result)
        {
            // 原版已有告知框要弹，先让它弹完，之后再走一次本闸门才轮到我们。
            if (__result)
            {
                return;
            }

            // switch_to_top_state: true 的调用点是把状态机拨回 TOP，不是放行询问，不能接管。
            if (switch_to_top_state || __instance.state != SceneTitleTemp.STATE.TOP)
            {
                return;
            }

            if (TitleOverlays.Gate(__instance))
            {
                __result = true;
            }
        }
    }
}
