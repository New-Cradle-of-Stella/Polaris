using Polaris.Settings;

namespace Polaris.Res
{
    /// <summary>诊断覆盖层的热键候选；不直接绑定 <c>UnityEngine.KeyCode</c>（太大，设置界面体验差），只列常用功能键。</summary>
    public enum DiagnosticsHotkey
    {
        F8,
        F9,
        F10,
        F11,
        F12,
    }

    /// <summary>PolarisRes 的全局设置；字段本身就是值的真身，<see cref="SettingsAttributeScanner"/> 在启动时写回，玩家改动设置界面时也直接改这里。</summary>
    [PolarisSettingGroup("polarisres", ResStrings.Group, OnLoaded = nameof(Apply))]
    internal static class ResSettings
    {
        [PolarisSetting(ResStrings.StrictMode, Desc = ResStrings.StrictModeDesc)]
        public static bool StrictMode = false;

        [PolarisSetting(ResStrings.FrameBudget, Min = 0.5, Max = 16, Step = 0.5,
            Desc = ResStrings.FrameBudgetDesc)]
        public static float FrameBudgetMilliseconds = 2.0f;

        /// <summary>启动加载完配置后调用一次（此时所有字段都已是上次退出时的值）。</summary>
        private static void Apply()
        {
            Plugin.Logger.LogInfo("[PolarisRes] Settings loaded.");
        }
    }
}
