using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 会话哨兵：让"这一局是怎么结束的"变成一个盘上的事实，而不是一件必须靠退出回调才能记下来的事。
    /// <para>
    /// 起因是一个结构性的盲区：<c>OnApplicationQuit</c> 只在<b>正常</b>退出时才会被调用。原生崩溃、
    /// <c>StackOverflowException</c>、内存耗尽、进程被强杀，全都不会走到那里——于是
    /// <see cref="PolarisErrorNotice.PersistPending"/> 也不会执行，<b>信息最值钱的那一局恰好是唯一
    /// 什么都留不下的一局</b>：玩家下次启动看到的是一片安静，会以为一切正常。
    /// </para>
    /// <para>
    /// 做法很土但可靠：启动时在 <see cref="Infra.PathsAPI.StateDir"/> 下写一个标记文件，运行期由
    /// <see cref="Watchdog"/> 线程每几秒把心跳（时间、帧号、场景、面包屑、本局错误摘要）刷进去，
    /// 正常退出时把它删掉。<b>下次启动时文件还在，就说明上一局没有正常结束</b>；文件里的内容则
    /// 交代了它是停在哪一刻、卡在谁身上。
    /// </para>
    /// <para>
    /// 写盘用 <c>File.WriteAllText</c> 直接盖，不搞"写临时文件再改名"的原子替换。理由是这里的
    /// 失败模式必须朝安全的方向倒：原子替换有个"旧的已删、新的未成"的窗口，在那一瞬崩溃就会
    /// 让下一局误判成"正常退出"（漏报）；而直接盖最坏只是留下一个内容截断的文件——<b>文件存在
    /// 本身就是结论</b>，内容只是细节，解析器对缺字段一律容忍。
    /// </para>
    /// </summary>
    internal static class SessionSentinel
    {
        /// <summary>
        /// 文件名带 pid。玩家同时开两份游戏是允许的，共用一个固定文件名会让两个进程互相盖写，
        /// 而且第二个进程一启动就会把第一个进程正在用的哨兵当成"上一局崩了"。
        /// </summary>
        const string FilePrefix = "_session_";
        const string FileSuffix = ".txt";

        /// <summary>心跳里最多带几条错误摘要，和告知页能显示的条数对齐。</summary>
        const int MaxErrorLines = 5;

        /// <summary>
        /// 判不出对应进程还在不在时，超过这个天数的哨兵文件一律当成陈旧的清掉。
        /// 没有这一条，"拿不准就当它还活着"的保守策略会让这些文件永远堆在目录里。
        /// </summary>
        const int StaleAfterDays = 1;

        const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

        static readonly object Gate = new object();

        static bool installed;
        static bool disabled;
        static string path;

        static DateTime processStart;
        static int processId;

        // 卡死判定的结果。被判过一次就一直带着——哪怕主线程后来又恢复了，
        // "这一局卡过 34 秒"仍然是玩家应该知道的事实。
        static bool hung;
        static double hungStallSeconds;
        static string hungActivity;

        /// <summary>上一局的结局；上一局正常退出（或这是第一次运行）时为 null。</summary>
        internal static LastSessionInfo LastSession { get; private set; }

        /// <summary>
        /// 读上一局的哨兵时就没能读成（状态目录不存在、权限不足）。这时候"没找到哨兵文件"
        /// 不等于"上一局正常退出"，只等于我们什么都没看见——见 <see cref="LastEnd"/>。
        /// </summary>
        static bool readFailed;

        /// <summary>
        /// 上一局是怎么结束的，四个结论都能出现（<see cref="LastSession"/> 只在出事时才非 null，
        /// 分不出"正常退出"和"没看见"）。
        /// <para>
        /// <b><see cref="SessionEndKind.Clean"/> 也包含"这是第一次装 Polaris"</b>：第一次运行和
        /// 上一局正常退出留下的东西完全一样——什么都没有——这一层区分不出来，也没有必要区分。
        /// 真正的 <see cref="SessionEndKind.Unknown"/> 留给"我们连看都没看成"那种情况。
        /// </para>
        /// </summary>
        internal static SessionEndKind LastEnd
        {
            get
            {
                if (LastSession != null)
                {
                    return LastSession.Kind;
                }

                return !installed || readFailed ? SessionEndKind.Unknown : SessionEndKind.Clean;
            }
        }

        /// <summary>
        /// 由 <c>Plugin.Awake</c> 尽早调用：先把上一局留下的哨兵读掉，再为本局写一个新的。
        /// 顺序不能反——本局的文件名虽然带 pid，但 pid 是会被系统回收再分配的，
        /// 先写就有可能把恰好和我们同号的那份上一局记录盖掉。
        /// </summary>
        internal static void Install()
        {
            if (installed)
            {
                return;
            }

            installed = true;

            try
            {
                Process self = Process.GetCurrentProcess();
                processId = self.Id;
                processStart = self.StartTime;
            }
            catch (Exception)
            {
                processId = 0;
                processStart = DateTime.Now;
            }

            LastSession = ReadStale();
            Flush();
        }

        /// <summary>
        /// 刷一次心跳。由 <see cref="Watchdog"/> 线程每几秒调用一次，也在判定卡死的那一刻立刻调用。
        /// <para>
        /// 心跳落盘刻意放在看门狗线程上，不放在 <c>Update</c> 里：这件事每几秒才做一次，
        /// 但它是一次同步写盘，摊在主线程上就是每隔几秒给玩家一个可能被磁盘卡住的机会。
        /// </para>
        /// </summary>
        internal static void Flush()
        {
            if (disabled)
            {
                return;
            }

            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(PolarisAPI.Paths.StateDir);
                    File.WriteAllText(FilePath(), Compose(), Encoding.UTF8);
                }
                catch (Exception)
                {
                    // 写不了盘（目录只读、磁盘满、被杀毒锁住）不会自己好，别每几秒重试一次。
                    // 这里也不记日志：这个方法跑在后台线程上，而它唯一的失败原因是文件系统，
                    // 每几秒往控制台刷一行同样的抱怨对谁都没有帮助。
                    disabled = true;
                }
            }
        }

        /// <summary>
        /// 记下"本局被判定为卡死"，并立刻落盘。落盘不能等下一次周期性心跳：判定之后玩家很可能
        /// 直接去任务管理器把游戏结束掉，那之后我们再没有机会写任何东西。
        /// </summary>
        internal static void MarkHung(HangReport report)
        {
            if (report == null)
            {
                return;
            }

            hung = true;
            hungStallSeconds = report.StallSeconds;
            hungActivity = report.Activity;

            Flush();
        }

        /// <summary>
        /// 正常退出时把哨兵删掉，这是"上一局是正常结束的"唯一表达方式。
        /// 必须排在 <see cref="PolarisErrorNotice.PersistPending"/> 之后——那一步才是正常退出路径下
        /// 真正把错误摘要交给下一局的地方，在它之前删掉哨兵，中间万一出事就两边都没留下。
        /// </summary>
        internal static void Close()
        {
            lock (Gate)
            {
                try
                {
                    string file = FilePath();
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception)
                {
                    // 删不掉只会让下一局多报一次"上一局没有正常退出"。不好，但不值得为它做任何事——
                    // 这一刻进程正在退出，再抛任何东西都只会盖掉真正的退出流程。
                }
            }
        }

        static string FilePath()
        {
            if (path == null)
            {
                path = Path.Combine(PolarisAPI.Paths.StateDir, FilePrefix + processId + FileSuffix);
            }

            return path;
        }

        // ================== 写 ==================

        static string Compose()
        {
            var b = new StringBuilder();

            b.Append("polaris_session=1\n");
            b.Append("pid=").Append(processId).Append('\n');
            b.Append("started=").Append(processStart.ToString(TimeFormat, CultureInfo.InvariantCulture)).Append('\n');
            b.Append("polaris=").Append(MyPluginInfo.PLUGIN_VERSION).Append('\n');
            b.Append("kind=").Append(hung ? "hung" : "running").Append('\n');
            b.Append("alive=").Append(DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture)).Append('\n');
            b.Append("frame=").Append(MainThreadBeat.LastFrame).Append('\n');
            b.Append("scene=").Append(Clean(MainThreadBeat.SceneName)).Append('\n');

            // 卡死判定时的面包屑要固定住，不能改用"现在"的——主线程可能已经恢复并走到别处了，
            // 而报告要说的是它当时卡在哪。没被判过卡死时才用实时值。
            b.Append("activity=").Append(Clean(hung ? hungActivity : MainThreadBeat.ActivityChain())).Append('\n');
            b.Append("stall=").Append(hungStallSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append('\n');
            b.Append("report=").Append(Clean(ErrorReportWriter.LastWrittenPath)).Append('\n');

            ErrorRegistry.Snapshot snapshot = ErrorRegistry.Take(MaxErrorLines);
            b.Append("errors=").Append(snapshot.Kinds).Append('\n');
            b.Append("more=").Append(snapshot.More).Append('\n');
            b.Append("storms=").Append(snapshot.Storms).Append('\n');

            for (int i = 0; i < snapshot.Lines.Count; i++)
            {
                b.Append("error").Append(i + 1).Append('=').Append(Clean(snapshot.Lines[i])).Append('\n');
            }

            return b.ToString();
        }

        /// <summary>
        /// 值里出现换行就会把 <c>键=值</c> 这个格式撕开。异常消息带换行是常事，
        /// 所以每个值都过一遍这里。
        /// </summary>
        static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            return value.Replace('\r', ' ').Replace('\n', ' ');
        }

        // ================== 读 ==================

        /// <summary>
        /// 扫一遍状态目录，找出上一局留下的哨兵。找到的（确认不属于任何在跑的进程的）一律读完就删——
        /// 它的使命只是活过一次进程边界，留着只会让下一局重复告知同一件事。
        /// </summary>
        static LastSessionInfo ReadStale()
        {
            List<string> files;
            try
            {
                if (!Directory.Exists(PolarisAPI.Paths.StateDir))
                {
                    // 目录本该在这之前就由 EnsureDirectories 建好了。它不在，说明那一步也失败了
                    // （只读安装、权限不足）——那我们既没写过上一局的哨兵，也读不到，只能说不知道。
                    readFailed = true;
                    return null;
                }

                files = new List<string>(
                    Directory.GetFiles(PolarisAPI.Paths.StateDir, FilePrefix + "*" + FileSuffix));
            }
            catch (Exception)
            {
                readFailed = true;
                return null;
            }

            LastSessionInfo newest = null;
            var consumed = new List<string>();

            foreach (string file in files)
            {
                Dictionary<string, string> fields = TryParse(file);
                if (fields == null)
                {
                    continue;
                }

                int pid = Int(fields, "pid");
                DateTime started = Time(fields, "started");
                DateTime alive = Time(fields, "alive");

                // 自己的 pid 不可能属于"另一个还在跑的实例"：那个实例就是我们，而我们刚启动。
                bool mine = pid == processId;
                bool old = alive != DateTime.MinValue && (DateTime.Now - alive).TotalDays >= StaleAfterDays;

                if (!mine && !old && StillRunning(pid, started))
                {
                    // 玩家同时开着另一份游戏。它的哨兵不是崩溃证据，别读也别删。
                    continue;
                }

                consumed.Add(file);

                LastSessionInfo info = Build(fields, alive, started);
                if (newest == null || info.LastAliveAt > newest.LastAliveAt)
                {
                    newest = info;
                }
            }

            foreach (string file in consumed)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception)
                {
                    // 删不掉就下次再删。此时 LastSession 已经读出来了，功能不受影响；
                    // 唯一的后果是同一条告知可能再出现一次。
                }
            }

            return newest;
        }

        static LastSessionInfo Build(Dictionary<string, string> fields, DateTime alive, DateTime started)
        {
            bool wasHung = string.Equals(Str(fields, "kind"), "hung", StringComparison.Ordinal);

            var lines = new List<string>(MaxErrorLines);
            for (int i = 1; i <= MaxErrorLines; i++)
            {
                string line = Str(fields, "error" + i);
                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }

            return new LastSessionInfo
            {
                Kind = wasHung ? SessionEndKind.Hung : SessionEndKind.NotClosed,
                StartedAt = started,
                LastAliveAt = alive,
                LastFrame = Int(fields, "frame"),
                Scene = Str(fields, "scene"),
                Activity = Str(fields, "activity"),
                StallSeconds = Num(fields, "stall"),
                ReportPath = Str(fields, "report"),
                PolarisVersion = Str(fields, "polaris"),
                ErrorKinds = Int(fields, "errors"),
                MoreErrorKinds = Int(fields, "more"),
                StormKinds = Int(fields, "storms"),
                ErrorLines = lines,
            };
        }

        static Dictionary<string, string> TryParse(string file)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (Exception)
            {
                return null;
            }

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                int split = line.IndexOf('=');
                if (split > 0)
                {
                    fields[line.Substring(0, split)] = line.Substring(split + 1);
                }
            }

            // 认不出格式就不认——目录里可能有别的东西，把随便一个文件当成哨兵读出来会更糟。
            return fields.ContainsKey("polaris_session") ? fields : null;
        }

        /// <summary>
        /// 这个 pid 是不是还对应着上一局那个进程。<b>判不出来时一律回答"是"</b>：
        /// 把另一个正在玩的实例误判成崩溃，比漏报一次崩溃要糟糕得多——前者是对着玩家胡说，
        /// 后者只是少说一句。真正陈旧的文件由 <see cref="StaleAfterDays"/> 兜底清理。
        /// </summary>
        static bool StillRunning(int pid, DateTime startedAt)
        {
            if (pid <= 0)
            {
                return false;
            }

            try
            {
                Process process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    return false;
                }

                try
                {
                    // pid 会被系统回收再分配，光看"有这个 pid"不够，启动时间也得对得上。
                    if (startedAt != DateTime.MinValue
                        && Math.Abs((process.StartTime - startedAt).TotalSeconds) > 5d)
                    {
                        return false;
                    }
                }
                catch (Exception)
                {
                    // 读别的进程的启动时间可能因权限被拒。那就只按"pid 还在"算。
                }

                return true;
            }
            catch (ArgumentException)
            {
                // 没有这个 pid——这是最常见的分支，也正是"上一局真的没了"。
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        static string Str(Dictionary<string, string> fields, string key)
        {
            if (fields.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return null;
        }

        static int Int(Dictionary<string, string> fields, string key)
            => fields.TryGetValue(key, out string value)
               && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;

        static double Num(Dictionary<string, string> fields, string key)
            => fields.TryGetValue(key, out string value)
               && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : 0d;

        static DateTime Time(Dictionary<string, string> fields, string key)
            => fields.TryGetValue(key, out string value)
               && DateTime.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture,
                   DateTimeStyles.None, out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
    }
}
