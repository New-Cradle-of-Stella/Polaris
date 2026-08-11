using Polaris.Localization;
using XX;

namespace Polaris.Settings
{
    /// <summary>
    /// 设置界面底部那条搜索栏。栏本身的画法在 <see cref="PolarisSearchRow"/>（和模组管理页共用），
    /// 这里只管两件设置界面特有的事：这条栏占多高，以及它接到 <see cref="SettingsSearchFilter"/> 上。
    /// <para>
    /// 标题画面与 ESC 菜单共用同一个实例，区别只在"这条栏画在哪个 designer 上"——
    /// 标题画面是 <see cref="SettingsSearchWindow"/> 自建的窗口，游戏内是原版游戏菜单的底部子区
    /// （见 <see cref="Patch.Patch_UiGMC_Constructor"/>）。同一时刻只可能有一个设置界面立着，
    /// 所以一个静态实例就够。
    /// </para>
    /// </summary>
    internal static class SettingsSearchBox
    {
        /// <summary>
        /// 搜索栏自身的高度。标题画面按它把原版设置面板缩短，游戏内按它反解子区的行高倍率
        /// （见 <see cref="SubareaRowScale"/>），两边看起来才是同一条栏。
        /// </summary>
        internal const float StripHeight = 42f;

        /// <summary>搜索栏与设置面板之间的留白。取 6 是为了和游戏菜单子区的 <c>margin_h</c> 对齐。</summary>
        internal const float StripGap = 6f;

        /// <summary>原版 <c>UiGameMenuTopTab</c> 的行高与行间距，用于反解 <see cref="SubareaRowScale"/>。</summary>
        const float SubareaRowHeight = 32f;
        const float SubareaMarginH = 6f;

        /// <summary>
        /// 游戏菜单底部子区的行高倍率。原版的换算是
        /// <c>cur_row_height = row_h * scale + margin_h * (scale - 1)</c>，
        /// 反解出让子区高度正好等于 <see cref="StripHeight"/> 的那个倍率。
        /// </summary>
        internal static float SubareaRowScale =>
            (StripHeight + SubareaMarginH) / (SubareaRowHeight + SubareaMarginH);

        static readonly PolarisSearchRow row = new PolarisSearchRow(
            "plrs:settings:search", SearchStrings.HintSettings, Filter);

        /// <summary>过滤并返回命中条数，交给搜索栏写状态文字。</summary>
        static int Filter(string query)
        {
            SettingsSearchFilter.Apply(query);
            return SettingsSearchFilter.MatchCount;
        }

        /// <summary>
        /// 把搜索栏画进 <paramref name="box"/>。调用方负责保证这个 designer 已经 <c>init()</c> 过、
        /// 并且确实有东西可搜（一个模组都没注册过设置项时这条栏整个不出现）。
        /// </summary>
        internal static void Build(Designer box) => row.Build(box);

        /// <summary>清空搜索并把所有行放回来。设置界面收起时调用。</summary>
        internal static void Reset() => row.Reset();

        /// <summary>界面整个没了：松开对控件的引用。</summary>
        internal static void Forget() => row.Forget();
    }
}
