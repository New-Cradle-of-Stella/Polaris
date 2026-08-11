namespace Polaris.Localization
{
    /// <summary>
    /// 搜索栏（<see cref="PolarisSearchRow"/>）的界面文案。设置界面底部那条和模组管理页列表上方
    /// 那条共用一张表，只有提示语各用各的。
    /// <para>
    /// 单独一张表而不是并进 <see cref="PolarisStrings"/>：那边装的是 Polaris 自己那几条<b>设置项</b>
    /// 的标签与说明，必须赶在 <c>Plugin.Awake</c> 的配置绑定之前登记；这几条是界面外壳，
    /// 要到玩家打开界面才第一次被查到，登记时机宽松得多（同 <see cref="ModManagerStrings"/>）。
    /// </para>
    /// <para>
    /// 语言取舍同 <see cref="PolarisStrings"/>：中性值填英文，中文/日文各覆盖一份，其余语言退回英文。
    /// </para>
    /// </summary>
    internal static class SearchStrings
    {
        /// <summary>key 前缀，与设置项的 <c>polaris.settings.</c>、管理页的 <c>polaris.manager.</c> 分开。</summary>
        const string P = "polaris.search.";

        /// <summary>搜索框左侧的标签。</summary>
        internal const string Label = "label";

        /// <summary>搜索框为空时的提示语（设置界面用）。</summary>
        internal const string HintSettings = "hint_settings";

        /// <summary>搜索框为空时的提示语（模组管理页用）。</summary>
        internal const string HintMods = "hint_mods";

        /// <summary>有查询时的状态文字，<c>{0}</c> 是命中的条数。</summary>
        internal const string Result = "result";

        /// <summary>一条都没命中时的状态文字。</summary>
        internal const string NoResult = "no_result";

        static bool registered;

        /// <summary>查一条本栏文案。<paramref name="key"/> 用本类上的常量，不要写字面量。</summary>
        internal static string Text(string key)
        {
            return PolarisAPI.Localization.Text(LocalizedString.Sigil + P + key);
        }

        /// <summary>
        /// 由 <see cref="PolarisSearchRow.Build"/> 在第一次画搜索栏时调用。幂等，重复调用是空操作。
        /// </summary>
        internal static void Register()
        {
            if (registered)
            {
                return;
            }

            registered = true;

            LocalizationAPI loc = PolarisAPI.Localization;

            loc.Register(P + Label, new LocalizedText("Search")
            {
                ["zh"] = "搜索",
                ["ja"] = "検索",
            });

            // 两条提示语和下面的状态文字都得压在状态文字那一行里（宽度约 130px、字号 12），
            // 别往长里写。
            loc.Register(P + HintSettings, new LocalizedText("mod name or setting")
            {
                ["zh"] = "模组名或设置项",
                ["ja"] = "MOD名・設定名",
            });

            loc.Register(P + HintMods, new LocalizedText("mod name or author")
            {
                ["zh"] = "模组名或作者",
                ["ja"] = "MOD名・作者",
            });

            loc.Register(P + Result, new LocalizedText("{0} match(es)")
            {
                ["zh"] = "命中 {0} 项",
                ["ja"] = "{0} 件",
            });

            loc.Register(P + NoResult, new LocalizedText("no match")
            {
                ["zh"] = "无匹配",
                ["ja"] = "該当なし",
            });
        }
    }
}
