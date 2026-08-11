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
                    "Enable hang detection (a background thread watches whether the main thread is still advancing frames). Crash detection stays active when this is off.");

                warnSeconds = file.Bind(WatchdogSection, "WarnSeconds", DefaultWarnSeconds,
                    "How many seconds of no main-thread progress before a warning line is written to the BepInEx log. Log only -- no report file, no player-facing notice.");

                reportSeconds = file.Bind(WatchdogSection, "ReportSeconds", DefaultReportSeconds,
                    "How many seconds of no main-thread progress before it is judged a suspected hang: a report file is written"
                    + " and the next session's title screen tells the player about it."
                    + " Lowering it is more sensitive, but normal long operations such as loading a save or switching scenes are then more easily misjudged.");

                bootReportSeconds = file.Bind(WatchdogSection, "BootReportSeconds", DefaultBootReportSeconds,
                    "Separate threshold used during game startup (before the first Update). In that stretch every plugin's"
                    + " Awake, the first scene load, and the game's own asset init have not finished, so long gaps before Update are normal.");

                killOnHang = file.Bind(WatchdogSection, "KillOnHang", DefaultKillOnHang,
                    "Whether to kill the game process outright once a hang is judged. Off by default: one false positive"
                    + " costs the player this session's progress, which is worse than hanging; a hung player can close the"
                    + " window themselves, and the report was already written the moment it was judged.");

                stormWindowSeconds = file.Bind(StormSection, "WindowSeconds", DefaultStormWindowSeconds,
                    "Detection window for an exception storm, in seconds. The same class of error occurring more than Threshold times inside this window counts as a persistent failure.");

                stormThreshold = file.Bind(StormSection, "Threshold", DefaultStormThreshold,
                    "Occurrence threshold for an exception storm. Throwing once per frame is roughly 60 times per second, so the default is about three seconds of throwing every frame.");
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning(
                    $"[Polaris] Failed to open {FileName}; diagnostics thresholds fall back to defaults for this session: {e.Message}");
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
