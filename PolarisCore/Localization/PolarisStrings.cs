namespace Polaris.Localization
{
    /// <summary>
    /// Polaris 自己那几条设置项文案的内置翻译，写在代码里而非 <c>.plang</c>：
    /// 设置项在 <c>Plugin.Awake</c> 绑定配置文件时就要查表（写进 <c>.cfg</c> 注释），早于 <c>.plang</c> 在 <c>Start</c> 才生效的注册。
    /// </summary>
    internal static class PolarisStrings
    {
        /// <summary>key 前缀。带 <c>polaris.</c> 是为了和模组自己的 key 分开，不会互相顶掉。</summary>
        const string P = "polaris.settings.";

        internal const string TitleVersionLine = "&" + P + "title_version";
        internal const string TitleVersionLineDesc = "&" + P + "title_version.desc";
        internal const string ErrorNotice = "&" + P + "error_notice";
        internal const string ErrorNoticeDesc = "&" + P + "error_notice.desc";

        static bool registered;

        /// <summary>由 <c>Plugin.Awake</c> 调一次，须早于设置项扫描（<c>Plugin.Start</c>）以便绑定配置文件时表里已有文案。</summary>
        internal static void Register()
        {
            if (registered)
            {
                return;
            }

            registered = true;

            LocalizationAPI loc = PolarisAPI.Localization;

            loc.Register(P + "title_version", new LocalizedText("Version line on title screen")
            {
                ["zh"] = "标题画面版本行",
                ["ja"] = "タイトル画面のバージョン表記",
            });

            loc.Register(P + "title_version.desc", new LocalizedText(
                "Show a \"Polaris vX.Y.Z\" line under the game version on the title screen.\n"
                + "Hiding it changes nothing else.")
            {
                ["zh"] = "在标题画面的游戏版本号下面显示一行 \"Polaris vX.Y.Z\"。\n"
                       + "关掉只是不显示这一行，别的不受影响。",
                ["ja"] = "タイトル画面のバージョン表記の下に「Polaris vX.Y.Z」を表示します。\n"
                       + "オフにしても表示が消えるだけです。",
            });

            loc.Register(P + "error_notice", new LocalizedText("Report previous run's errors")
            {
                ["zh"] = "提示上一局的错误",
                ["ja"] = "前回のエラーを通知",
            });

            loc.Register(P + "error_notice.desc", new LocalizedText(
                "If the previous run hit mod errors, crashed or froze, show a summary on the "
                + "title screen.\nReports go to BepInEx/Polaris/reports either way.")
            {
                ["zh"] = "上一局出现模组错误、崩溃或卡死时，在标题画面列出摘要。\n"
                       + "无论开关，报告都会写进 BepInEx/Polaris/reports。",
                ["ja"] = "前回の実行でMODエラー・クラッシュ・フリーズがあった場合、"
                       + "タイトル画面に概要を表示します。\n"
                       + "レポートはどちらでも BepInEx/Polaris/reports に出力されます。",
            });
        }
    }
}
