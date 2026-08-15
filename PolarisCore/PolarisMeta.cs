namespace Polaris
{
    /// <summary>全仓库共用的"关于 Polaris 自己"的常量；集中一处避免多个地方各写一份而失去同步。</summary>
    internal static class PolarisMeta
    {
        /// <summary>Polaris 自身错误报告的提交去处；<see cref="PolarisModWarning"/> 与报告文件结尾共用。</summary>
        internal const string ReportTarget = "https://github.com/New-Cradle-of-Stella/Polaris/issues";

        /// <summary>官方《Game Program Modifying &amp; Mod Creation Limitation》规则页；改动前请核对线上最新版本，规则若变声明文案需同步更新。</summary>
        internal const string ModGuidelinesUrl =
            "https://docs.nanamehacha.dev/en/alice_in_cradle/license/game_program_modifying_limitation";
    }
}
