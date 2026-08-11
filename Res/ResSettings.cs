using Polaris.Settings;

namespace Polaris.Res
{
    /// <summary>
    /// 诊断覆盖层的热键候选。没有直接绑定 <c>UnityEngine.KeyCode</c>——那是个几百项的大枚举，
    /// 塞进设置界面的枚举选择器里体验很差；这里只列出几个常用的功能键，
    /// 需要更多时再扩充这个小枚举即可。
    /// </summary>
    public enum DiagnosticsHotkey
    {
        F8,
        F9,
        F10,
        F11,
        F12,
    }

    /// <summary>
    /// PolarisRes 的全局设置。字段本身就是值的真身：<see cref="SettingsAttributeScanner"/>
    /// 在 <c>Plugin.Start</c> 阶段把上次存的值写回这里，玩家在设置界面改动时
    /// 也直接改这里，库内其它代码照常读字段。
    /// <para>
    /// 注意：这里只声明"要有哪些旋钮"，不实现旋钮对应的行为——行为在各自的里程碑
    /// （热重载、生命周期、诊断……）里逐步接上，未接上之前改动这些字段暂时不产生效果。
    /// </para>
    /// </summary>
    [PolarisSettingGroup("polarisres", ResStrings.Group, OnLoaded = nameof(Apply))]
    internal static class ResSettings
    {
        [PolarisSetting(ResStrings.StrictMode, Desc = ResStrings.StrictModeDesc)]
        public static bool StrictMode = false;

        [PolarisSetting(ResStrings.FrameBudget, Min = 0.5, Max = 16, Step = 0.5,
            Desc = ResStrings.FrameBudgetDesc)]
        public static float FrameBudgetMilliseconds = 2.0f;

        // ────────────────────────────────────────────────────────────────────
        //  以下旋钮的子系统还没落地，因此**刻意不标 [PolarisSetting]**：设置界面上
        //  一个改了没有任何反应的开关，比没有这个开关更糟——玩家会以为是坏的。
        //  对应里程碑接上之后，把 [PolarisSetting] 加回去即可（描述文案已经写好在这里）。
        //
        //   UnloadGraceSeconds      "卸载宽限秒数"    引用计数归零后延迟卸载   —— 待 M6 延迟卸载
        //   LoadTimeoutSeconds      "加载超时秒数"    单个任务的硬性超时       —— 待 M4 异步任务
        //   RecordLeaseStackTraces  "记录租约堆栈"    定位租约泄漏             —— 待 M7 诊断
        //   ModAudioVolume          "模组音量"        原始音频的整体倍率       —— 待 M9 音量桥
        //   HotReloadEnabled        "热重载"          监听素材目录变化         —— 待 M8 watcher
        //   StructuralPxlsHotReload "PXLS 结构热重载"  重建整个角色对象         —— 待 M8 watcher
        //   DiagnosticsOverlayHotkey "诊断覆盖层热键"  唤出诊断覆盖层           —— 待 M7 诊断
        // ────────────────────────────────────────────────────────────────────

        public static float UnloadGraceSeconds = 5f;
        public static float LoadTimeoutSeconds = 30f;
        public static bool RecordLeaseStackTraces = false;
        public static float ModAudioVolume = 1.0f;
        public static bool HotReloadEnabled = false;
        public static bool StructuralPxlsHotReload = false;
        public static DiagnosticsHotkey DiagnosticsOverlayHotkey = DiagnosticsHotkey.F9;

        /// <summary>
        /// 启动加载完配置后调用一次（此时所有字段都已是上次退出时的值）。
        /// 后续里程碑（音频音量桥、热重载 watcher 等）接入时在这里补上对应的同步调用。
        /// </summary>
        private static void Apply()
        {
            Plugin.Logger.LogInfo("[PolarisRes] Settings loaded.");
        }
    }
}
