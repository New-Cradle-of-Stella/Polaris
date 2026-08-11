using System;
using System.IO;
using BepInEx.Configuration;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 崩溃/卡死检测的几个阈值与开关，存在 <c>BepInEx/config/Polaris/_polaris_diagnostics.cfg</c>。
    /// <para>
    /// <b>刻意不走 <see cref="Settings.PolarisSettingAttribute"/></b>，也就是不出现在原版设置界面里。
    /// 两个理由：一是这些不是玩家偏好而是排障旋钮（"主线程停多少秒算卡死"对玩家没有意义，
    /// 对正在追一个 bug 的模组作者才有），把它摆进游戏设置只会占着玩家的注意力；二是时机对不上——
    /// 特性轨的设置项在 <c>Plugin.Start</c> 才加载完，而看门狗必须在 <c>Awake</c> 就带着阈值起跑，
    /// 那时特性轨还没扫描。
    /// </para>
    /// <para>
    /// 绑定固定在主线程（<see cref="Resolve"/> 由 <c>Plugin.Awake</c> 调一次）；之后看门狗线程
    /// 只读 <c>ConfigEntry.Value</c>，那是一次字段读取，跨线程读是安全的。绑不上（目录只读、
    /// 文件被锁）就整体退回下面这些默认值，检测功能照常工作。
    /// </para>
    /// </summary>
    internal static class DiagnosticsConfig
    {
        const string FileName = "_polaris_diagnostics.cfg";

        const string WatchdogSection = "Watchdog";
        const string StormSection = "Storm";

        // ================== 默认值 ==================
        //
        // 这几个数字是整套卡死检测的成败所在：调得太小就会把正常的长加载判成卡死，
        // 而"Polaris 老误报"这个名声一旦传出去，之后真正的卡死报告也没人相信了。
        // 所以默认值一律偏保守——宁可漏报一次 12 秒的真卡顿，也不要错报一次 12 秒的读档。

        const bool DefaultEnabled = true;

        /// <summary>只在控制台记一行警告的阈值。到这一步不写报告、不惊扰玩家。</summary>
        const float DefaultWarnSeconds = 10f;

        /// <summary>写报告、给下一局的告知页上膛的阈值。</summary>
        const float DefaultReportSeconds = 30f;

        /// <summary>
        /// 首个 <c>Update</c> 之前用的阈值。启动期本来就会长时间不进 <c>Update</c>
        /// （所有插件的 <c>Awake</c>、首个场景加载、MTRX 建图标与 shader 都在这一段里），
        /// 用游戏中的 30 秒去量它必然误报。
        /// </summary>
        const float DefaultBootReportSeconds = 90f;

        const bool DefaultKillOnHang = false;

        const float DefaultStormWindowSeconds = 5f;
        const int DefaultStormThreshold = 200;

        // ================== 状态 ==================

        static ConfigFile file;
        static bool resolved;

        static ConfigEntry<bool> enabled;
        static ConfigEntry<float> warnSeconds;
        static ConfigEntry<float> reportSeconds;
        static ConfigEntry<float> bootReportSeconds;
        static ConfigEntry<bool> killOnHang;
        static ConfigEntry<float> stormWindowSeconds;
        static ConfigEntry<int> stormThreshold;

        /// <summary>
        /// 由 <c>Plugin.Awake</c> 在装看门狗之前调用一次。失败不抛：读不到配置只意味着
        /// 全部用默认值，不该让一个 cfg 文件挡住整个诊断系统。
        /// </summary>
        internal static void Resolve()
        {
            if (resolved)
            {
                return;
            }

            resolved = true;

            try
            {
                Directory.CreateDirectory(PolarisAPI.Paths.ConfigDir);
                file = new ConfigFile(Path.Combine(PolarisAPI.Paths.ConfigDir, FileName), saveOnInit: true);

                enabled = file.Bind(WatchdogSection, "Enabled", DefaultEnabled,
                    "是否启用卡死检测（一条后台线程盯着主线程还在不在推进帧）。关掉之后崩溃检测仍然有效。");

                warnSeconds = file.Bind(WatchdogSection, "WarnSeconds", DefaultWarnSeconds,
                    "主线程停止推进多少秒后，在 BepInEx 日志里记一行警告。只记日志，不写报告、不打扰玩家。");

                reportSeconds = file.Bind(WatchdogSection, "ReportSeconds", DefaultReportSeconds,
                    "主线程停止推进多少秒后判定为疑似卡死：写报告文件，并让下一局启动时的标题画面告知玩家。"
                    + "调小会更灵敏，但读档、场景切换这类正常的长耗时也更容易被错判。");

                bootReportSeconds = file.Bind(WatchdogSection, "BootReportSeconds", DefaultBootReportSeconds,
                    "游戏启动阶段（首个 Update 之前）单独使用的判定阈值。这一段里所有插件的 Awake、"
                    + "首个场景加载、游戏自己的资源初始化都还没跑完，本来就会长时间不进 Update。");

                killOnHang = file.Bind(WatchdogSection, "KillOnHang", DefaultKillOnHang,
                    "判定为卡死后是否直接结束游戏进程。默认关闭：一次误判就会让玩家丢掉这一局的进度，"
                    + "比卡在那里更糟；卡死了玩家自己也能关掉窗口，而报告在判定的那一刻就已经写好了。");

                stormWindowSeconds = file.Bind(StormSection, "WindowSeconds", DefaultStormWindowSeconds,
                    "异常风暴的判定窗口（秒）。同一类错误在这个窗口内发生次数超过 Threshold 就算持续性故障。");

                stormThreshold = file.Bind(StormSection, "Threshold", DefaultStormThreshold,
                    "异常风暴的次数阈值。每帧抛一次异常大约是每秒 60 次，默认值相当于连续三秒多每帧都在抛。");
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning(
                    $"[Polaris] 打开 {FileName} 失败，诊断阈值本局全部使用默认值：{e.Message}");
                file = null;
            }
        }

        // ================== 读取 ==================
        //
        // 一律 ?? 默认值：Resolve 失败时这些 entry 全是 null，调用方不该为此各写一遍判空。

        internal static bool WatchdogEnabled => enabled?.Value ?? DefaultEnabled;

        internal static float WarnSeconds => Sane(warnSeconds?.Value ?? DefaultWarnSeconds, 2f, DefaultWarnSeconds);

        internal static float ReportSeconds
            => Sane(reportSeconds?.Value ?? DefaultReportSeconds, 5f, DefaultReportSeconds);

        internal static float BootReportSeconds
            => Sane(bootReportSeconds?.Value ?? DefaultBootReportSeconds, 15f, DefaultBootReportSeconds);

        internal static bool KillOnHang => killOnHang?.Value ?? DefaultKillOnHang;

        internal static float StormWindowSeconds
            => Sane(stormWindowSeconds?.Value ?? DefaultStormWindowSeconds, 0.5f, DefaultStormWindowSeconds);

        internal static int StormThreshold
        {
            get
            {
                int value = stormThreshold?.Value ?? DefaultStormThreshold;
                return value >= 10 ? value : DefaultStormThreshold;
            }
        }

        /// <summary>
        /// 手改 cfg 的人可能填出 0 或负数。把这种值当成"没填"退回默认，而不是让看门狗
        /// 从此每秒报一次卡死——一个笔误不该把诊断系统变成噪音发生器。
        /// </summary>
        static float Sane(float value, float minimum, float fallback)
            => value >= minimum ? value : fallback;
    }
}
