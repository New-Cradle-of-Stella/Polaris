namespace Polaris
{
    /// <summary>
    /// 全仓库共用的几个"关于 Polaris 自己"的常量。单独收在这里是因为它们出现在互不相干的
    /// 地方（标题画面的模组环境警示页、错误报告文件的结尾、控制台日志），而它们必须永远一致——
    /// 三处各写一份的下场就是改了一处忘了另外两处。
    /// </summary>
    internal static class PolarisMeta
    {
        /// <summary>
        /// Polaris 自身错误报告的提交去处。<see cref="PolarisModWarning"/> 的中日英三段正文与
        /// <see cref="Diagnostics.ErrorReportWriter"/> 写出的报告结尾共用这一处。
        /// </summary>
        internal const string ReportTarget = "https://github.com/New-Cradle-of-Stella/Polaris/issues";

        /// <summary>Polaris 项目主页。</summary>
        internal const string ProjectUrl = "https://github.com/AAAA9731";

        /// <summary>
        /// 官方的《Game Program Modifying &amp; Mod Creation Limitation》规则页。Polaris 能够公开
        /// 发布的前提正是遵守这一页——标题画面的模组环境警示页
        /// （<see cref="PolarisModWarning"/>）在三种语言下都会把这个地址原样列出，并配一个按钮
        /// 直接交给系统浏览器打开，让玩家自己能查。
        /// <para>
        /// 该页目前仍标注为草案（draft），并声明"以本页最新版本为准"。改动这里之前请先核对线上
        /// 版本：如果规则对声明措辞提出了新要求，警示页的三段声明文案要跟着一起改。
        /// </para>
        /// </summary>
        internal const string ModGuidelinesUrl =
            "https://docs.nanamehacha.dev/en/alice_in_cradle/license/game_program_modifying_limitation";
    }
}
