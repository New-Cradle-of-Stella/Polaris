using HarmonyLib;
using nel.title;

namespace Polaris.Patch
{
    /// <summary>
    /// 把 <see cref="TitleOverlays"/>（Polaris 自己的一次性标题告知页）挂进原版自己的
    /// "进标题菜单之前先弹告知框"闸门。
    /// <para>
    /// 原版 <c>runIRD</c> 在 <c>STATE.TOP</c> 分支里是这么写的：
    /// <code>
    /// if (!BxTop.isActive())
    ///     if ((IN.kettei3() || t >= FIRST_LOGO_DELAY) &amp;&amp; !errorAnnounceBox(switch_to_top_state: false))
    ///         BxTop.activate();
    /// </code>
    /// 也就是说 <c>errorAnnounceBox</c> 返回 true 就不激活顶部按钮行——它本来是给
    /// 崩溃日志 / 声卡错误 / 文件校验失败这三种告知框用的。本补丁在它"没有任何原版告知要弹"
    /// 时接管返回值，让 Polaris 的告知页占住同一个位置：出现时机、遮挡范围都和原版告知框
    /// 一致，玩家确认之前碰不到"开始游戏 / 读取"那一排按钮。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(SceneTitleTemp), nameof(SceneTitleTemp.errorAnnounceBox))]
    internal static class Patch_SceneTitleTemp_errorAnnounceBox
    {
        [HarmonyPostfix]
        static void Postfix(SceneTitleTemp __instance, bool switch_to_top_state, ref bool __result)
        {
            // __result 为 true 说明原版自己有告知框要弹（状态已经切去 ERRORLOG 之类），
            // 让它先弹完；玩家确认后会再走一次本闸门，那时才轮到我们。
            if (__result)
            {
                return;
            }

            // switch_to_top_state: true 的那个调用点在错误告知框的"确定"回调里，职责是
            // 把状态机拨回 TOP，不是"要不要放行按钮行"的询问——接管它只会让状态切不回去。
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
