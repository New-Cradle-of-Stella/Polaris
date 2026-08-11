using HarmonyLib;
using nel.title;

namespace Polaris.Patch
{
    /// <summary>
    /// 语言切换会触发 SceneTitleTemp.fineTexts 里的 BConTop.RemakeT&lt;aBtnNel&gt;(null)，
    /// 那会用引擎原版网格公式（XX.Designer.reboundCarrForBtnMulti）重新摆放全部顶部按钮，
    /// 把 Patch_SceneTitleTemp_initButtons 对换行末行做的居中修正冲掉——这里在 fineTexts
    /// 跑完后重新应用一次同样的修正。fineTexts 不是每次调用都会走到 RemakeT 那个分支
    /// （只有 state 处于 TOP/错误态且 BxTop 激活时才会），但无条件调用
    /// MainMenuAPI.CenterTopRow 是幂等的，不会有副作用。
    /// <para>
    /// 同一个 fineTexts 还会无条件执行 <c>TxVer.text_content = NEL.version</c>，把
    /// <see cref="Patch_SceneTitleTemp_initTitleLogo"/> 追加在版本号下面的 Polaris 版本行
    /// 整块冲掉——这就是"换语言后 Polaris 版本信息消失"的原因，所以这里重新追加一次。
    /// 只在游戏走了 else 分支（即 <c>state != SENSITIVE_ANNOUNCE</c>）时才补：
    /// SENSITIVE_ANNOUNCE 状态下 <c>TxVer</c> 被复用去显示首次启动的敏感内容告知
    /// （<c>TX.Get("Title_Announce_For_Sensitive")</c>），此时那里根本不是版本号。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(SceneTitleTemp), nameof(SceneTitleTemp.fineTexts))]
    internal static class Patch_SceneTitleTemp_fineTexts
    {
        [HarmonyPostfix]
        static void Postfix(SceneTitleTemp __instance)
        {
            MainMenuAPI.CenterTopRow(__instance);

            if (__instance.state != SceneTitleTemp.STATE.SENSITIVE_ANNOUNCE)
            {
                TitleVersionLine.Append(__instance);
            }
        }
    }
}
