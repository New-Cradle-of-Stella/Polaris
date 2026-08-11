using HarmonyLib;
using nel.title;

namespace Polaris.Patch
{
    /// <summary>
    /// 标题告知页（见 <see cref="TitleOverlays"/>）显示期间，屏蔽 LTab/RTab 这对换语言的快捷键。
    /// <para>
    /// <see cref="TitleChrome"/> 把语言切换行的 alpha 压成 0 之后，鼠标已经点不到那排按钮了
    /// （<c>aBtn.clickable</c> 里有一条 <c>Skin.alpha &gt; 0f</c>），但键盘/手柄这条路不经过命中
    /// 测试：原版 <c>runIRD</c> 在 TOP/错误态里直接读 <c>IN.isLTabU()</c> / <c>IN.isRTabU()</c>，
    /// 转手调 <c>languageShift</c> → <c>aBtn.ExecuteOnClick()</c>，而后者只查 <c>active &gt; 0</c>，
    /// 跟 alpha 无关。不拦的话，玩家在告知页上按一下肩键就能把游戏语言换掉。
    /// </para>
    /// <para>
    /// 拦在 <c>languageShift</c> 而不是拦按键读取：这一处是"换语言"唯一的键盘入口，
    /// 而按键读取那句还兼管别的分支。判据用 <see cref="TitleOverlays.IsShowing"/>——
    /// 它在同一次 <c>runIRD</c> 里由更早的原版闸门（<c>errorAnnounceBox</c>，见
    /// <see cref="Patch_SceneTitleTemp_errorAnnounceBox"/>）刷新过，本帧的值已经是新的。
    /// </para>
    /// <para>
    /// 注意告知页自己的左右方向键换语言（<c>PolarisModWarning.PollLanguageKeys</c>）走的是
    /// <c>IN.isLP()</c> / <c>IN.isRP()</c>，和这里的 LTab/RTab 是两组不同的键，不受影响。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(SceneTitleTemp), "languageShift")]
    internal static class Patch_SceneTitleTemp_languageShift
    {
        [HarmonyPrefix]
        static bool Prefix() => !TitleOverlays.IsShowing;
    }
}
