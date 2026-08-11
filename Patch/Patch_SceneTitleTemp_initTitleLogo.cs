using HarmonyLib;
using nel.title;

namespace Polaris.Patch
{
    /// <summary>
    /// 在标题画面右下角的版本号下面追加一行 Polaris 版本号。
    /// <para>
    /// 已反编译确认：<c>SceneTitleTemp.initTitleLogo</c>（开场 logo 动画播到 10 帧、
    /// 且 <c>MdLogo == null</c> 时只调用这一次）把 <c>TxVer</c> 这个 <c>TextRenderer</c>
    /// 从"首次启动敏感内容告知"复用成版本号显示：<c>size = 14</c>、<c>html_mode = true</c>、
    /// <c>alignx = RIGHT</c>、<c>aligny = BOTTOM</c>，内容是 <c>NEL.version</c>——
    /// <c>"ver " + Application.version + "\n&lt;font size=\"10\"&gt; (日期 Early Access Version XI)&lt;/font&gt;"</c>，
    /// 也就是本来就是两行的富文本块。<c>aligny = BOTTOM</c> 意味着整块的底边固定在
    /// <c>redrawLogo</c> 每帧算出的锚点上，多加一行只会让块整体向上长高，不会盖住原有内容，
    /// 视觉上"新的一行"正好接在原有两行下面。
    /// </para>
    /// <para>
    /// 直接在同一个 <c>TxVer</c> 上追加文本，而不是另起一个 <c>TextRenderer</c>：
    /// 淡入动画、字体、每帧跟随 logo 的重新定位（<c>redrawLogo</c> 里的
    /// <c>IN.PosP2(TxVer.transform, 342f, -139f + num4)</c>）全部是这一个对象在做，
    /// 另起一个还要在 <c>redrawLogo</c> 上再打一个补丁去同步位置和透明度，纯属多余。
    /// </para>
    /// <para>
    /// 这不是唯一的追加点：换语言会走 <c>fineTexts</c> 把 <c>TxVer</c> 整块重置回
    /// <c>NEL.version</c>，那边由 <see cref="Patch_SceneTitleTemp_fineTexts"/> 再补一次，
    /// 文本本身见 <see cref="TitleVersionLine"/>。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(SceneTitleTemp), nameof(SceneTitleTemp.initTitleLogo))]
    internal static class Patch_SceneTitleTemp_initTitleLogo
    {
        [HarmonyPostfix]
        static void Postfix(SceneTitleTemp __instance)
        {
            TitleVersionLine.Append(__instance);
        }
    }
}
