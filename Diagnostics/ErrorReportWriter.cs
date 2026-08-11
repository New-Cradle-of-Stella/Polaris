using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 把错误写成一份玩家可以直接交出去的纯文本报告，落在
    /// <see cref="Infra.PathsAPI.ReportsDir"/>。
    /// <para>
    /// <b>一局一个文件、追加写</b>，不是一事件一文件：同一局里的几个错误往往互为因果，
    /// 散在几十个文件里反而看不出名堂，玩家也不知道该交哪一个。
    /// </para>
    /// <para>
    /// <b>纯文本而非 JSON。</b>这份东西的读者是玩家和模组作者，玩家要能直接整段贴进 issue
    /// 或群里。<c>Newtonsoft.Json</c> 虽然躺在游戏的 Managed 目录里，但那是<i>游戏的</i>程序集，
    /// 依赖它等于凭空给自己加一条随游戏版本漂移的耦合，换来的只是一个没人想读的格式。
    /// </para>
    /// </summary>
    internal static class ErrorReportWriter
    {
        /// <summary>报告目录里最多留几份，超出的按修改时间从旧到新删。</summary>
        const int KeepReports = 20;

        /// <summary>
        /// 串行化所有写入。<see cref="ErrorRegistry"/> 那边已经有自己的锁，但它保护的是它自己的表，
        /// 而这里的 <see cref="path"/>/<see cref="headerWritten"/>/<see cref="disabled"/> 与
        /// <c>File.AppendAllText</c> 是另一份共享状态——<see cref="Watchdog"/> 从后台线程写卡死报告
        /// 之后，这份状态就有了两个不同的写入方：不锁的下场是文件头写两遍、两条记录交错成半行、
        /// 或者一次偶发的写失败把 <c>disabled</c> 永久置上。
        /// </summary>
        static readonly object Gate = new object();

        static string path;
        static bool headerWritten;

        /// <summary>
        /// 写盘失败过一次就不再尝试。目录只读、磁盘满、被杀毒锁住这类问题不会自己好，
        /// 每条错误都重试一次只会让日志里多几十条同样的抱怨。
        /// </summary>
        static bool disabled;

        /// <summary>最近一次写入成功的报告路径；从没写成功过为 null。</summary>
        internal static string LastWrittenPath { get; private set; }

        /// <summary>本进程的报告文件路径（文件不一定已经存在）。</summary>
        static string ReportPath
        {
            get
            {
                if (path == null)
                {
                    string stamp = ProcessStart.ToString("yyyyMMdd_HHmmss");
                    path = System.IO.Path.Combine(
                        PolarisAPI.Paths.ReportsDir, $"polaris-report_{stamp}.txt");
                }

                return path;
            }
        }

        /// <summary>
        /// 进程启动时间。取一次就固定住：报告文件名与文件头都用它，中途重算会让同一局
        /// 写出两个文件名。
        /// </summary>
        static readonly DateTime ProcessStart = DateTime.Now;

        // ================== 写入 ==================

        internal static void Append(ErrorIncident incident)
            => Write(() => BuildIncident(incident));

        /// <summary>
        /// 写一条致命错误（见 <see cref="FatalError"/>）。排版与
        /// <see cref="BuildIncident"/> 那种"事件 #N"刻意不同：致命错误决定的是这一局能不能
        /// 继续，玩家打开报告第一眼就该看到它，而不是在一串异常堆栈里翻找。
        /// </summary>
        internal static void AppendFatal(FatalError fatal)
        {
            if (fatal == null)
            {
                return;
            }

            Write(() => BuildFatal(fatal));
        }

        /// <summary>
        /// 写一次疑似卡死（见 <see cref="HangReport"/>）。<b>由看门狗线程调用</b>——
        /// 这是这个类唯一一个不来自主线程的入口，也是 <see cref="Gate"/> 存在的原因。
        /// </summary>
        internal static void AppendHang(HangReport report)
        {
            if (report == null)
            {
                return;
            }

            Write(() => BuildHang(report));
        }

        /// <summary>写一条"这类错误正在持续反复发生"的加注（见 <see cref="ErrorIncident.IsStorm"/>）。</summary>
        internal static void AppendStorm(ErrorIncident incident)
        {
            if (incident == null)
            {
                return;
            }

            Write(() => BuildStorm(incident));
        }

        /// <summary>
        /// 写一段"关于上一局"：上一局没有正常结束时，由本局启动时调用一次。
        /// <para>
        /// 写进<b>本局</b>的报告而不是去追上一局那份文件，是因为上一局很可能根本没写出过报告
        /// （崩溃前一个异常都没抛的情况很常见），那就没有文件可追。写在这里，玩家手上永远只有
        /// 一份"最新的报告"要交，而里面清楚地标着哪一段说的是上一局。
        /// </para>
        /// </summary>
        internal static void AppendPreviousSession(LastSessionInfo info)
        {
            if (info == null)
            {
                return;
            }

            Write(() => BuildPreviousSession(info));
        }

        /// <summary>
        /// 所有写入的唯一出口：补文件头、追加正文、记住路径，全程持锁。
        /// <paramref name="body"/> 是延迟求值的——拿不到锁之前不该先去拼一大段字符串。
        /// </summary>
        static void Write(Func<string> body)
        {
            if (disabled)
            {
                return;
            }

            lock (Gate)
            {
                if (disabled)
                {
                    return;
                }

                try
                {
                    Directory.CreateDirectory(PolarisAPI.Paths.ReportsDir);

                    if (!headerWritten)
                    {
                        Cleanup();
                        File.AppendAllText(ReportPath, BuildHeader(), Encoding.UTF8);
                        headerWritten = true;
                    }

                    File.AppendAllText(ReportPath, body(), Encoding.UTF8);
                    LastWrittenPath = ReportPath;
                }
                catch (Exception)
                {
                    // 这里绝对不能记日志：日志会被自己的 BepInEx 监听器抓到，绕回
                    // ErrorRegistry 再写一次报告，再失败，再记日志……ErrorRegistry 的 inside
                    // 闸门拦得住，但根本不该走到那一步。
                    disabled = true;
                    LastWrittenPath = null;
                }
            }
        }

        /// <summary>报告目录只留最近 <see cref="KeepReports"/> 份，免得跑几百局之后堆成一片。</summary>
        static void Cleanup()
        {
            try
            {
                var files = new DirectoryInfo(PolarisAPI.Paths.ReportsDir)
                    .GetFiles("polaris-report_*.txt")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Skip(KeepReports - 1)
                    .ToList();

                foreach (FileInfo file in files)
                {
                    file.Delete();
                }
            }
            catch (Exception)
            {
                // 清不掉旧报告不影响写新报告，忽略。
            }
        }

        // ================== 环境信息 ==================

        static string gameVersion;
        static string unityVersion;
        static string operatingSystem;
        static string playerLog;

        /// <summary>
        /// 在主线程上把文件头要用的几个 Unity 属性先取好，由 <c>Plugin.Awake</c> 调一次。
        /// <para>
        /// 必须提前取：<see cref="AppendHang"/> 走在看门狗线程上，而 <c>Application.version</c>
        /// 这类属性只允许主线程访问——真到了写卡死报告那一刻现取，拿到的只会是一串"读取失败"，
        /// 偏偏那是最需要知道游戏版本的时候。<see cref="Safe"/> 兜底仍然留着：万一这个方法
        /// 没被调到，行为就退回原来的样子，而不是留一片空白。
        /// </para>
        /// </summary>
        internal static void PrimeEnvironment()
        {
            gameVersion = Safe(() => Application.version);
            unityVersion = Safe(() => Application.unityVersion);
            operatingSystem = Safe(() => SystemInfo.operatingSystem);
            playerLog = Safe(PlayerLogPath);
        }

        static string Env(string cached, Func<string> get) => cached ?? Safe(get);

        // ================== 文件头 ==================

        static string BuildHeader()
        {
            var b = new StringBuilder();

            b.AppendLine("==================================================================");
            b.AppendLine(" Polaris 错误报告 / Polaris Error Report");
            b.AppendLine("==================================================================");
            b.AppendLine();
            b.AppendLine("这份文件由 Polaris 自动生成，用来帮你分清出问题的是模组、Polaris 还是游戏本体。");
            b.AppendLine("交给谁看，见每条事件末尾的「该怎么办」。");
            b.AppendLine();
            b.AppendLine($"生成时间      ：{ProcessStart:yyyy-MM-dd HH:mm:ss}");
            b.AppendLine($"Polaris 版本  ：{MyPluginInfo.PLUGIN_VERSION}");
            b.AppendLine($"游戏版本      ：{Env(gameVersion, () => Application.version)}");
            b.AppendLine($"Unity 版本    ：{Env(unityVersion, () => Application.unityVersion)}");
            b.AppendLine($"操作系统      ：{Env(operatingSystem, () => SystemInfo.operatingSystem)}");
            b.AppendLine($"游戏内语言    ：{Safe(() => PolarisAPI.Game.CurrentLocale)}");
            b.AppendLine();
            b.AppendLine("其它日志：");
            b.AppendLine($"  Unity 玩家日志：{Env(playerLog, PlayerLogPath)}");
            b.AppendLine("  BepInEx 日志  ：BepInEx/LogOutput.log");
            b.AppendLine();
            b.AppendLine("------------------------------------------------------------------");
            b.AppendLine(" 已加载的模组");
            b.AppendLine("------------------------------------------------------------------");

            foreach (string line in ModLines())
            {
                b.AppendLine(line);
            }

            b.AppendLine();
            b.AppendLine("被禁用（本局未加载）：");
            foreach (string line in DisabledLines())
            {
                b.AppendLine(line);
            }

            b.AppendLine();
            return b.ToString();
        }

        static IEnumerable<string> ModLines()
        {
            // yield return 不能出现在 catch 子句体内，只能先把结果/失败状态存起来，
            // 出了 try/catch 之后再决定 yield 什么。
            List<AssemblyOwner> mods = null;
            bool failed = false;
            try
            {
                mods = AssemblyOwnerIndex.LoadedMods().ToList();
            }
            catch (Exception)
            {
                failed = true;
            }

            if (failed)
            {
                yield return "  （读取模组清单失败）";
                yield break;
            }

            if (mods.Count == 0)
            {
                yield return "  （没有检测到任何已加载的插件）";
                yield break;
            }

            foreach (AssemblyOwner mod in mods)
            {
                var line = new StringBuilder($"  [{mod.KindLabel}] {mod.FileName ?? mod.DisplayName}");

                if (mod.ModInfo?.Version != null)
                {
                    line.Append($"  v{mod.ModInfo.Version}");
                }

                if (mod.PluginGuid != null)
                {
                    line.Append($"  GUID={mod.PluginGuid}");
                }

                if (mod.ModInfo?.Author != null)
                {
                    line.Append($"  作者={mod.ModInfo.Author}");
                }

                if (mod.ModInfo?.Url != null)
                {
                    line.Append($"  主页={mod.ModInfo.Url}");
                }

                yield return line.ToString();

                // 完整路径单独另起一行：同名 dll 放在不同目录（比如玩家手动拷贝了两份）
                // 只看文件名分不清是哪一个，这是唯一能定位到具体文件的信息。
                if (mod.FullPath != null)
                {
                    yield return $"      路径={mod.FullPath}";
                }
            }
        }

        /// <summary>
        /// 被改名成 <c>.dll.disabled</c> 的那些。列出来是有意义的：玩家排查时最常问的就是
        /// "我上次到底关了哪个"。
        /// </summary>
        static IEnumerable<string> DisabledLines()
        {
            List<UserModRecord> disabledMods = null;
            bool failed = false;
            try
            {
                disabledMods = UserModToggleManager.Scan().Where(r => !r.Enabled).ToList();
            }
            catch (Exception)
            {
                failed = true;
            }

            if (failed)
            {
                yield return "  （读取失败）";
                yield break;
            }

            if (disabledMods.Count == 0)
            {
                yield return "  （无）";
                yield break;
            }

            foreach (UserModRecord record in disabledMods)
            {
                yield return $"  {record.DisplayName}";
            }
        }

        // ================== 单条事件 ==================

        static string BuildIncident(ErrorIncident incident)
        {
            ErrorVerdict verdict = incident.Verdict;
            var b = new StringBuilder();

            b.AppendLine("==================================================================");
            b.AppendLine($" 事件 #{incident.Index}    {incident.FirstSeen:HH:mm:ss}");
            b.AppendLine("==================================================================");

            if (incident.Context != null)
            {
                b.AppendLine($"发生在：{incident.Context}");
            }

            b.AppendLine($"判定  ：{verdict.Headline()}");
            b.AppendLine($"置信度：{verdict.ConfidenceLabel}");
            b.AppendLine($"理由  ：{verdict.Reason}");

            if (verdict.Diagnosis != null)
            {
                b.AppendLine($"诊断  ：{verdict.Diagnosis}");
            }

            if (verdict.Suspects.Count > 0)
            {
                b.AppendLine("嫌疑人：");
                foreach (ErrorSuspect suspect in verdict.Suspects)
                {
                    b.AppendLine($"  · {suspect.Describe()}");
                }
            }

            b.AppendLine();
            b.AppendLine("异常：");
            b.AppendLine(Indent(incident.ExceptionChain, "  "));

            if (incident.Frames.Count > 0)
            {
                b.AppendLine();
                b.AppendLine("堆栈（方括号是 Polaris 标注的归属，不是原始文本）：");
                foreach (ErrorFrame frame in incident.Frames)
                {
                    b.AppendLine("  " + frame.Describe());
                }
            }

            if (!string.IsNullOrEmpty(incident.RawStackTrace))
            {
                b.AppendLine();
                b.AppendLine("原始堆栈：");
                b.AppendLine(Indent(incident.RawStackTrace, "  "));
            }

            b.AppendLine();
            b.AppendLine("--- 该怎么办 ---");
            b.AppendLine(Advice(verdict));
            b.AppendLine();

            return b.ToString();
        }

        /// <summary>
        /// 按主责分叉的行动建议。这一段才是整份报告存在的理由——
        /// <see cref="PolarisModWarning"/> 那三段正文向玩家承诺的就是它。
        /// </summary>
        static string Advice(ErrorVerdict verdict)
        {
            var b = new StringBuilder();

            if (verdict.SuggestedAction != null)
            {
                b.AppendLine($"· {verdict.SuggestedAction}");
            }

            switch (verdict.Kind)
            {
                case OwnerKind.Mod:
                    b.AppendLine("· 先自己验证一次：在标题画面的 Polaris 页里关掉这个模组，重启游戏看问题是否消失。");
                    b.AppendLine($"· 确认之后请把这份报告交给它的作者{Contact(verdict.Culprit)}。");
                    b.AppendLine("· 请不要把这个问题反馈给游戏原作者或官方渠道——它不是原版的问题。");
                    break;

                case OwnerKind.Polaris:
                    b.AppendLine($"· 这是 Polaris 自己的问题，请把这份报告提交到 {PolarisMeta.ReportTarget}。");
                    b.AppendLine("· 同样请不要反馈给游戏原作者。");
                    break;

                case OwnerKind.Vanilla:
                    b.AppendLine("· 堆栈里没有任何模组代码，这一条可能不是模组导致的。");
                    b.AppendLine("· 但在用一份干净的、没装任何模组的游戏本体复现之前，请不要报给游戏作者——");
                    b.AppendLine("  模组带来的连锁影响未必都会出现在堆栈上。");
                    break;

                case OwnerKind.Framework:
                    b.AppendLine("· 多半是某个模组的 Harmony 补丁没能应用。看看 BepInEx 日志里同一时间的补丁失败记录。");
                    break;

                default:
                    b.AppendLine("· Polaris 无法判定责任方。请用二分法排查：关掉一半模组重启，看问题是否还在，逐轮缩小范围。");
                    b.AppendLine("· 全部模组都关掉后仍然复现，再用一份干净的游戏本体确认一次。");
                    break;
            }

            if (verdict.Suspects.Count > 1)
            {
                b.AppendLine("· 上面列了多个嫌疑人，请逐个关掉验证，不要同时关。");
            }

            return b.ToString().TrimEnd();
        }

        static string Contact(AssemblyOwner culprit)
        {
            string author = culprit?.ModInfo?.Author;
            string url = culprit?.ModInfo?.Url;

            if (author == null && url == null)
            {
                return "（该模组没有声明作者信息，请到你下载它的地方去找）";
            }

            var parts = new List<string>();
            if (author != null)
            {
                parts.Add(author);
            }

            if (url != null)
            {
                parts.Add(url);
            }

            return $"（{string.Join(" / ", parts.ToArray())}）";
        }

        // ================== 致命错误 ==================

        static string BuildFatal(FatalError fatal)
        {
            var b = new StringBuilder();

            b.AppendLine("##################################################################");
            b.AppendLine($" 致命错误    {DateTime.Now:HH:mm:ss}    由 {fatal.Source} 报出");
            b.AppendLine("##################################################################");
            b.AppendLine();
            b.AppendLine("这一局被判定为不能继续：模组环境本身有问题，继续玩下去只会得到错误的结果或");
            b.AppendLine("更难排查的故障。Polaris 已在标题画面拦住菜单并请玩家退出游戏。");
            b.AppendLine();
            b.AppendLine($"原因  ：{fatal.Reason?.ForReport}");

            if (fatal.Details.Count > 0)
            {
                b.AppendLine();
                b.AppendLine("明细：");
                foreach (string detail in fatal.Details)
                {
                    b.AppendLine($"  · {detail}");
                }
            }

            b.AppendLine();
            b.AppendLine("涉及的模组：");
            foreach (string line in CulpritLines(fatal))
            {
                b.AppendLine(line);
            }

            b.AppendLine();
            b.AppendLine("--- 该怎么办 ---");
            b.AppendLine(fatal.Action?.ForReport ?? DefaultFatalAction);
            b.AppendLine("· 这不是原版游戏的问题，请不要反馈给游戏原作者或官方渠道。");
            b.AppendLine();

            return b.ToString();
        }

        const string DefaultFatalAction =
            "· 把上面列出的模组关掉（标题画面的 Polaris 页）再启动，只留一个能正常玩的组合。\n"
            + "· 然后把这份报告交给它们的作者——这类冲突通常只有作者能修。";

        /// <summary>
        /// 致命错误的"涉及的模组"段。<see cref="ErrorIncident"/> 那边只有一个主责，这里天生
        /// 可能有多个（两个模组撞了同一个 key，谁都不算无辜），所以每个都把联系方式带上。
        /// </summary>
        static IEnumerable<string> CulpritLines(FatalError fatal)
        {
            if (fatal.Culprits.Count == 0)
            {
                yield return "  （调用方没有点名具体的模组，请对照上面的明细自行判断）";
                yield break;
            }

            foreach (Assembly culprit in fatal.Culprits)
            {
                if (culprit == null)
                {
                    continue;
                }

                AssemblyOwner owner = null;
                try
                {
                    owner = AssemblyOwnerIndex.Of(culprit);
                }
                catch (Exception)
                {
                    // 查不出归属不影响这一段的价值，退回程序集名。
                }

                if (owner == null)
                {
                    yield return $"  · {SafeAssemblyName(culprit)}";
                    continue;
                }

                yield return $"  · {owner.Describe()}{Contact(owner)}";

                if (owner.FullPath != null)
                {
                    yield return $"      路径={owner.FullPath}";
                }
            }
        }

        static string SafeAssemblyName(Assembly assembly)
        {
            try
            {
                return assembly.GetName().Name;
            }
            catch (Exception)
            {
                return "（未知程序集）";
            }
        }

        // ================== 卡死 ==================

        static string BuildHang(HangReport report)
        {
            var b = new StringBuilder();

            b.AppendLine("##################################################################");
            b.AppendLine($" 疑似卡死 #{report.Index}    {report.DetectedAt:HH:mm:ss}");
            b.AppendLine("##################################################################");
            b.AppendLine();
            b.AppendLine($"主线程已经 {report.StallSeconds:0} 秒没有推进任何一帧。这不是异常——没有任何异常被抛出，");
            b.AppendLine("所以它不会出现在上面那些「事件」里，也不会出现在 Unity 的 Player.log 里。");
            b.AppendLine("典型病因是死循环、死锁，或者一次永远等不到结果的等待。");
            b.AppendLine();
            b.AppendLine($"停在何处：{(report.DuringBoot ? "游戏启动阶段（还没进入主循环）" : $"frame {report.LastFrame}")}");

            if (!string.IsNullOrEmpty(report.Scene))
            {
                b.AppendLine($"场景    ：{report.Scene}");
            }

            b.AppendLine($"判定时刻：{report.DetectedAt:yyyy-MM-dd HH:mm:ss}");
            b.AppendLine();

            if (report.Activity != null)
            {
                b.AppendLine("停止响应时主线程正在执行：");
                b.AppendLine($"  {report.Activity}");
                b.AppendLine();
                b.AppendLine("涉及的模组：");
                foreach (string line in HangCulpritLines(report))
                {
                    b.AppendLine(line);
                }
            }
            else
            {
                b.AppendLine("停止响应时主线程不在任何 Polaris 埋点里。");
                b.AppendLine("这本身就是一条线索：卡住的地方不是由 Polaris 转发出去的模组代码，");
                b.AppendLine("更可能是原版逻辑、某个模组自己挂的 Harmony 补丁，或它自己的 MonoBehaviour/协程。");
            }

            b.AppendLine();
            b.AppendLine("--- 该怎么办 ---");

            if (report.Activity != null)
            {
                b.AppendLine("· 上面点名的模组是第一嫌疑人：在标题画面的 Polaris 页把它关掉，重启游戏看还会不会卡。");
                b.AppendLine("· 确认之后请把这份报告交给它的作者。");
            }
            else
            {
                b.AppendLine("· 用二分法排查：关掉一半模组重启，看还会不会卡在同一个地方，逐轮缩小范围。");
                b.AppendLine("· 上面的「停在何处」是关键信息——同一个场景、同一个操作能稳定复现的话，请写进反馈里。");
            }

            b.AppendLine("· 卡死的判定阈值可以在 BepInEx/config/Polaris/_polaris_diagnostics.cfg 里调整。");
            b.AppendLine("  如果游戏其实只是在读一个很大的存档、而不是真的卡住了，请把 ReportSeconds 调大。");
            b.AppendLine();

            return b.ToString();
        }

        static IEnumerable<string> HangCulpritLines(HangReport report)
        {
            if (report.Culprit == null)
            {
                yield return "  （埋点没有点名具体的模组，请对照上面那一行自行判断）";
                yield break;
            }

            AssemblyOwner owner = null;
            try
            {
                owner = AssemblyOwnerIndex.Of(report.Culprit);
            }
            catch (Exception)
            {
                // 查不出归属不影响这一段的价值，退回程序集名。
            }

            if (owner == null)
            {
                yield return $"  · {SafeAssemblyName(report.Culprit)}";
                yield break;
            }

            yield return $"  · {owner.Describe()}{Contact(owner)}";

            if (owner.FullPath != null)
            {
                yield return $"      路径={owner.FullPath}";
            }
        }

        // ================== 持续性故障 ==================

        static string BuildStorm(ErrorIncident incident)
        {
            var b = new StringBuilder();

            b.AppendLine("------------------------------------------------------------------");
            b.AppendLine($" 加注：事件 #{incident.Index} 正在持续反复发生    {incident.StormDetectedAt:HH:mm:ss}");
            b.AppendLine("------------------------------------------------------------------");
            b.AppendLine($"这一类错误在 {DiagnosticsConfig.StormWindowSeconds:0.#} 秒内发生了 {incident.StormBurst} 次"
                         + $"（截至此刻累计 {incident.Count} 次）。");
            b.AppendLine("这个频率意味着它长在每帧都会走的代码上（Update、OnGUI、逐帧的协程），");
            b.AppendLine("对应的功能这一局已经不是「偶尔出错」而是「完全不工作」了。");
            b.AppendLine("详细的判定与堆栈见上面的「事件 #" + incident.Index + "」。");
            b.AppendLine();

            return b.ToString();
        }

        // ================== 上一局 ==================

        static string BuildPreviousSession(LastSessionInfo info)
        {
            var b = new StringBuilder();

            b.AppendLine("##################################################################");
            b.AppendLine(info.Kind == SessionEndKind.Hung
                ? " 关于上一局：疑似卡死"
                : " 关于上一局：没有正常退出");
            b.AppendLine("##################################################################");
            b.AppendLine();
            b.AppendLine("以下内容描述的不是这一局，而是上一次运行——它没能走完正常的退出流程，");
            b.AppendLine("所以只能由这一局回过头来替它汇报。");
            b.AppendLine();

            if (info.Kind == SessionEndKind.Hung)
            {
                b.AppendLine($"结论    ：主线程停止推进约 {info.StallSeconds:0} 秒，被判定为卡死。");
            }
            else
            {
                b.AppendLine("结论    ：进程消失，且没有走 OnApplicationQuit。");
                b.AppendLine("          原生崩溃、栈溢出、内存耗尽、被任务管理器结束、被 Steam 强杀，");
                b.AppendLine("          在 Polaris 这一层看起来完全一样，无法进一步区分。");
            }

            if (info.StartedAt != DateTime.MinValue)
            {
                b.AppendLine($"上一局启动：{info.StartedAt:yyyy-MM-dd HH:mm:ss}");
            }

            if (info.LastAliveAt != DateTime.MinValue)
            {
                b.AppendLine($"最后活动  ：{info.LastAliveAt:yyyy-MM-dd HH:mm:ss}");
            }

            b.AppendLine($"停在何处  ：{info.Where()}");

            if (info.PolarisVersion != null)
            {
                b.AppendLine($"Polaris 版本：{info.PolarisVersion}");
            }

            if (info.ReportPath != null)
            {
                b.AppendLine($"上一局的报告：{info.ReportPath}");
            }

            if (info.ErrorKinds > 0)
            {
                b.AppendLine();
                b.AppendLine($"上一局归档过 {info.ErrorKinds} 类错误"
                             + (info.StormKinds > 0 ? $"（其中 {info.StormKinds} 类在持续反复发生）" : "")
                             + "：");
                foreach (string line in info.ErrorLines)
                {
                    b.AppendLine($"  · {line}");
                }

                if (info.MoreErrorKinds > 0)
                {
                    b.AppendLine($"  …… 另有 {info.MoreErrorKinds} 类，见上一局的报告文件。");
                }
            }

            b.AppendLine();
            b.AppendLine("--- 该怎么办 ---");

            if (info.Kind == SessionEndKind.Hung && !string.IsNullOrEmpty(info.Activity))
            {
                b.AppendLine("· 上面「停在何处」那一行里点到的模组是第一嫌疑人，先关掉它试一次。");
            }
            else
            {
                b.AppendLine("· 这类问题没有堆栈可看，只能靠复现来定位：想想上一局最后在做什么操作。");
                b.AppendLine("· 能稳定复现的话，用二分法关模组缩小范围；关光了还复现，再用一份干净的游戏本体确认。");
            }

            b.AppendLine("· 如果上面列了错误，先从它们查起：崩溃前抛出的异常经常就是同一个病根的前兆。");
            b.AppendLine($"· Unity 自己的崩溃转储与日志在：{Env(playerLog, PlayerLogPath)} 旁边。");
            b.AppendLine();

            return b.ToString();
        }

        // ================== 杂项 ==================

        static string PlayerLogPath()
            => System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                @"AppData\LocalLow", Application.companyName, Application.productName, "Player.log");

        static string Safe(Func<string> get)
        {
            try
            {
                return get() ?? "（未知）";
            }
            catch (Exception)
            {
                return "（读取失败）";
            }
        }

        static string Indent(string text, string prefix)
        {
            if (string.IsNullOrEmpty(text))
            {
                return prefix + "（无）";
            }

            return string.Join("\n", text.Replace("\r\n", "\n").Split('\n')
                .Select(line => prefix + line).ToArray());
        }
    }
}
