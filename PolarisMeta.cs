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
        /// Polaris 自身错误报告的提交去处。
        /// <para>
        /// <b>这里仍是占位符</b>：换成真实地址（issue 页 / 群号 / 邮箱皆可）之后，
        /// <see cref="PolarisModWarning"/> 的中日英三段正文与
        /// <see cref="Diagnostics.ErrorReportWriter"/> 写出的报告结尾会同时生效，只改这一处。
        /// </para>
        /// </summary>
        internal const string ReportTarget = "【待填写 / TBA】";

        /// <summary>Polaris 项目主页。</summary>
        internal const string ProjectUrl = "https://github.com/AAAA9731";
    }
}
