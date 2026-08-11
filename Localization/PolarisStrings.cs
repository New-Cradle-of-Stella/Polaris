namespace Polaris.Localization
{
    /// <summary>
    /// Polaris 自己那几条设置项文案的内置翻译。
    /// <para>
    /// 为什么写在代码里而不是做成 <c>.plang</c>：设置项在 <c>Plugin.Awake</c> 阶段绑定
    /// 配置文件时就已经在查表了（说明文字要写进 <c>.cfg</c> 注释），而 <c>.plang</c> 的
    /// 注册与 resolver 挂载要到 <c>Start</c> 才发生——真做成 <c>.plang</c>，设置界面上的
    /// 标签就会变成一串 key。同样的理由适用于 <c>Lang</c>/<c>Res</c> 两个子系统自己的设置项
    /// （见各自的 <c>*Strings</c>），但<b>不适用于给玩家装的模组</b>——那些应该用
    /// <c>.plang</c>，那边有编辑器、有 key 冲突检查，也不必把文案写进代码。
    /// </para>
    /// <para>
    /// 三种语言的取舍与标题告知页一致（见 <see cref="NoticeLocale"/>）：中性值填英文，
    /// 中文/日文各覆盖一份，其余语言退回英文。<c>"zh"</c> 一条就同时覆盖
    /// <c>zh-cn</c> 与 <c>zh-tw</c>，<c>"ja"</c> 也会被游戏的默认语言 <c>"_"</c> 命中，
    /// 理由见 <see cref="LocalizedText.Pick"/>。
    /// </para>
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

        /// <summary>
        /// 由 <c>Plugin.Awake</c> 调一次。必须早于设置项扫描（<c>Plugin.Start</c>）——
        /// 绑定配置文件时要拿说明文字去写 <c>.cfg</c> 注释，那时表里就得有货。
        /// </summary>
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
