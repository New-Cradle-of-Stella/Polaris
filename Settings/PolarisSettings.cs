using Polaris.Localization;

namespace Polaris.Settings
{
    /// <summary>
    /// Polaris 自己暴露给玩家的设置项。字段本身就是值的真身，
    /// <see cref="SettingsAttributeScanner"/> 在 <c>Plugin.Start</c> 阶段把上次存的值写回这里。
    /// <para>
    /// 收得很紧是刻意的：这里只放<b>玩家真会想改</b>的东西，也就是"标题画面上多出来的这两样
    /// 东西我不想看"。排障用的旋钮（卡死判定阈值、异常风暴窗口……）一律留在
    /// <see cref="Diagnostics.DiagnosticsConfig"/> 的 <c>_polaris_diagnostics.cfg</c> 里，理由见那里：
    /// 它们不是玩家偏好，而且看门狗在 <c>Awake</c> 就要带着阈值起跑，等不到特性轨扫描。
    /// </para>
    /// <para>
    /// 两项都不带 <c>OnChanged</c>：它们的取值点（标题画面建版本行、告知页问"要不要弹"）
    /// 本来就是每次用时现读字段，没有需要跟着同步的运行状态。
    /// </para>
    /// </summary>
    [PolarisSettingGroup("polaris", "Polaris", Order = -100)]
    internal static class PolarisSettings
    {
        [PolarisSetting(PolarisStrings.TitleVersionLine, Desc = PolarisStrings.TitleVersionLineDesc)]
        public static bool ShowTitleVersionLine = true;

        [PolarisSetting(PolarisStrings.ErrorNotice, Desc = PolarisStrings.ErrorNoticeDesc)]
        public static bool ShowErrorNotice = true;
    }
}
