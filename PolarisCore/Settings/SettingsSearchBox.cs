using Polaris.Localization;
using XX;

namespace Polaris.Settings
{
    /// <summary>
    /// 设置界面底部那条搜索栏。画法在 <see cref="PolarisSearchRow"/>（与模组管理页共用），这里只管栏高与接到 <see cref="SettingsSearchFilter"/>。
    /// 标题画面与 ESC 菜单共用同一静态实例，因同一时刻只有一个设置界面立着。
    /// </summary>
    internal static class SettingsSearchBox
    {
        /// <summary>搜索栏高度；两边（标题画面、游戏内菜单）据此对齐显示为同一条栏。</summary>
        internal const float StripHeight = 42f;

        /// <summary>搜索栏与设置面板之间的留白。取 6 是为了和游戏菜单子区的 <c>margin_h</c> 对齐。</summary>
        internal const float StripGap = 6f;

        /// <summary>原版 <c>UiGameMenuTopTab</c> 的行高与行间距，用于反解 <see cref="SubareaRowScale"/>。</summary>
        const float SubareaRowHeight = 32f;
        const float SubareaMarginH = 6f;

        /// <summary>游戏菜单底部子区的行高倍率，反解自原版换算公式使子区高度等于 <see cref="StripHeight"/>。</summary>
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

        /// <summary>把搜索栏画进 <paramref name="box"/>；调用方须保证 designer 已 <c>init()</c> 且确实有东西可搜。</summary>
        internal static void Build(Designer box) => row.Build(box);

        /// <summary>清空搜索并把所有行放回来。设置界面收起时调用。</summary>
        internal static void Reset() => row.Reset();

        /// <summary>界面整个没了：松开对控件的引用。</summary>
        internal static void Forget() => row.Forget();
    }
}
