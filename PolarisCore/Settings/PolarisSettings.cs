using Polaris.Localization;

namespace Polaris.Settings
{
    /// <summary>
    /// Polaris 自己暴露给玩家的设置项，刻意收得很紧，只放玩家真会想改的显示偏好。
    /// 排障用的旋钮在 <see cref="Diagnostics.DiagnosticsConfig"/> 里单独管理（看门狗须在 <c>Awake</c> 前带阈值起跑，等不到这里的特性扫描）。
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
