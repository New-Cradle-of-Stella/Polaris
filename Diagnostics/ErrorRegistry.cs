using System;
using System.Collections.Generic;
using System.Reflection;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 本局所有错误的登记处：去重、限流、存档，然后把新事件推给三个出口
    /// （BepInEx 日志 / 报告文件 / 下游订阅者）。
    /// <para>
    /// <b>整套系统的保命逻辑都在这里。</b>错误分析跑在"已经出事了"的现场，它自己再出问题
    /// 就是雪上加霜——所以这里有防重入闸、有并发锁、有硬上限、有限流，并且每一层都宁可
    /// 少记一条也不肯把游戏拖下水。
    /// </para>
    /// </summary>
    internal static class ErrorRegistry
    {
        /// <summary>
        /// 本局最多归档多少<b>种</b>错误。到顶之后只累计一个计数，不再分析新的种类——
        /// 一旦某个模组开始每帧抛不同的异常，无上限的表会把内存和日志一起吃光。
        /// </summary>
        const int MaxDistinctIncidents = 64;

        /// <summary>每秒最多分析多少<b>新</b>指纹。挡的是启动期成片失败造成的雪崩。</summary>
        const int NewIncidentsPerSecond = 8;

        static readonly object Gate = new object();
        static readonly Dictionary<string, ErrorIncident> byFingerprint =
            new Dictionary<string, ErrorIncident>(StringComparer.Ordinal);
        static readonly List<ErrorIncident> incidents = new List<ErrorIncident>();

        /// <summary>
        /// 防重入。写报告失败会记一条 error，那条 error 又会被 BepInEx 日志监听器抓到，
        /// 于是又走一遍这里——不拦就是死循环。<c>ThreadStatic</c> 而不是普通静态字段：
        /// 后台线程的异常不该被主线程正在处理的那一条挡掉。
        /// </summary>
        [ThreadStatic]
        static bool inside;

        static DateTime windowStart;
        static int windowCount;

        /// <summary>被上限挡掉、没有归档的错误种类数。</summary>
        internal static int Suppressed { get; private set; }

        /// <summary>已经被判定为持续性故障（异常风暴）的错误种类数。见 <see cref="NoteRepeat"/>。</summary>
        internal static int Storms { get; private set; }

        /// <summary>纯原版错误的累计次数。只计数不建档，退出时汇总成一行。</summary>
        internal static long VanillaOnly { get; private set; }

        /// <summary>
        /// <c>Debug.LogError</c> / 插件 <c>LogError</c> 这类"报了个错但没抛异常"的次数。
        /// 同样只计数：把它们当异常对待会把报告淹掉，但完全不提又会让玩家以为一切正常。
        /// </summary>
        internal static long LoggedErrors { get; private set; }

        /// <summary>记一次非异常的错误日志。</summary>
        internal static void CountLoggedError()
        {
            lock (Gate)
            {
                LoggedErrors++;
            }
        }

        /// <summary>本局已归档的错误（按指纹去重），按首次出现顺序。</summary>
        internal static IReadOnlyList<ErrorIncident> Incidents => incidents;

        /// <summary>有新错误归档时触发。订阅者抛异常会被吞掉，不影响其它订阅者与后续流程。</summary>
        internal static event Action<ErrorIncident> Recorded;

        // ================== 提交 ==================

        /// <summary>提交一个异常对象。<paramref name="culprit"/> 可空，非空表示调用方直接点名。</summary>
        internal static void Submit(Exception exception, string context, Assembly culprit)
        {
            if (exception == null || inside)
            {
                return;
            }

            inside = true;
            try
            {
                lock (Gate)
                {
                    Record(() => ErrorAnalyzer.Analyze(exception, context, culprit));
                }
            }
            catch (Exception)
            {
                // 分析本身炸了。这里绝对不能再往外抛，也不能记日志（会绕回监听器）——
                // 静默放弃这一条，比让错误系统把游戏一起带走要好。
            }
            finally
            {
                inside = false;
            }
        }

        /// <summary>提交一条只有文本的错误（Unity 日志回调那一路）。</summary>
        internal static void Submit(string condition, string stackTrace, string context)
        {
            if (string.IsNullOrEmpty(condition) || inside)
            {
                return;
            }

            inside = true;
            try
            {
                lock (Gate)
                {
                    Record(() => ErrorAnalyzer.Analyze(condition, stackTrace, context));
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                inside = false;
            }
        }

        // ================== 归档 ==================

        static void Record(Func<ErrorIncident> analyze)
        {
            ErrorIncident incident = analyze();
            if (incident == null)
            {
                return;
            }

            // 已经见过这一类：只累加。日志和报告都不再重复——Update() 里的异常一秒 60 次，
            // 每次都写一遍等于把真正有用的信息埋掉。
            if (byFingerprint.TryGetValue(incident.Fingerprint, out ErrorIncident existing))
            {
                existing.Count++;
                existing.LastSeen = incident.LastSeen;
                NoteRepeat(existing);
                return;
            }

            // 和模组无关的错误只计数。这就是"全局兜底 + 智能过滤"里的过滤：原版自己的毛病
            // 不归 Polaris 管，把它摆到玩家面前只会让人误以为是模组坏了。
            if (!incident.Verdict.IsModRelated)
            {
                VanillaOnly++;
                return;
            }

            if (incidents.Count >= MaxDistinctIncidents || !WithinRateLimit())
            {
                Suppressed++;
                return;
            }

            incident.Index = incidents.Count + 1;
            byFingerprint[incident.Fingerprint] = incident;
            incidents.Add(incident);

            // 先落盘再打日志：日志最后一行要报出报告文件的位置，而报告写失败时
            // 不能对着玩家撒谎说"已写入"。
            ErrorReportWriter.Append(incident);
            ErrorLogFormatter.Log(incident);
            Raise(incident);
        }

        /// <summary>
        /// 一条已归档的错误又来了一次：看它是不是已经变成<b>持续性故障</b>——同一类错误在
        /// <see cref="DiagnosticsConfig.StormWindowSeconds"/> 的窗口内发生超过
        /// <see cref="DiagnosticsConfig.StormThreshold"/> 次。
        /// <para>
        /// 这是"游戏还能玩吗"和"有个功能坏了"之间的分界线。去重机制把两者压成了同一条记录，
        /// 差别只剩下 <see cref="ErrorIncident.Count"/> 里那个数字——每帧都在抛的异常意味着那个
        /// 模组的这条代码路径已经彻底不工作了，而这件事不该指望玩家自己去读一个计数。
        /// </para>
        /// <para>
        /// 一类错误只判一次（<see cref="ErrorIncident.IsStorm"/> 一旦为 true 就再不进来）：
        /// 风暴的定义就是"次数极多"，每次都重新判定等于每帧写一次报告。调用方已经持有
        /// <see cref="Gate"/>，且 <c>inside</c> 为 true，所以这里记日志绕不回自己。
        /// </para>
        /// </summary>
        static void NoteRepeat(ErrorIncident existing)
        {
            if (existing.IsStorm)
            {
                return;
            }

            DateTime now = existing.LastSeen;

            if (existing.StormWindowCount == 0
                || (now - existing.StormWindowStart).TotalSeconds > DiagnosticsConfig.StormWindowSeconds)
            {
                existing.StormWindowStart = now;
                existing.StormWindowCount = 1;
                return;
            }

            if (++existing.StormWindowCount < DiagnosticsConfig.StormThreshold)
            {
                return;
            }

            existing.IsStorm = true;
            existing.StormDetectedAt = now;
            existing.StormBurst = existing.StormWindowCount;
            Storms++;

            ErrorReportWriter.AppendStorm(existing);

            Plugin.Logger.LogError(
                $"[Polaris] 事件 #{existing.Index} 正在持续反复发生"
                + $"（{DiagnosticsConfig.StormWindowSeconds:0.#} 秒内 {existing.StormBurst} 次，累计 {existing.Count} 次）："
                + $"{existing.OneLine()}");
            Plugin.Logger.LogError(
                "[Polaris] 这类错误多半长在每帧都会走的代码上，对应的功能这一局已经完全不工作了。");
        }

        static bool WithinRateLimit()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - windowStart).TotalSeconds >= 1d)
            {
                windowStart = now;
                windowCount = 0;
            }

            return ++windowCount <= NewIncidentsPerSecond;
        }

        static void Raise(ErrorIncident incident)
        {
            Action<ErrorIncident> handlers = Recorded;
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<ErrorIncident>)handler)(incident);
                }
                catch (Exception)
                {
                    // 订阅者写坏了不该连累其它订阅者，更不该反过来再触发一次归档
                    // （inside 此刻还是 true，就算它调 Report 也进不来）。
                }
            }
        }

        // ================== 快照 ==================

        /// <summary>
        /// 本局错误情况的一份不可变快照。存在的理由只有一个：
        /// <see cref="SessionSentinel"/> 在<b>看门狗线程</b>上刷心跳，而
        /// <see cref="incidents"/> 是主线程在 <see cref="Gate"/> 保护下增删的列表——
        /// 后台线程直接遍历它，迟早会撞上一次正在扩容的 <c>List</c>。
        /// </summary>
        internal sealed class Snapshot
        {
            internal int Kinds;
            internal int More;
            internal int Storms;
            internal List<string> Lines = new List<string>();
        }

        /// <summary>取一份快照，最多带 <paramref name="maxLines"/> 条一行式摘要。可从任意线程调用。</summary>
        internal static Snapshot Take(int maxLines)
        {
            var snapshot = new Snapshot();

            lock (Gate)
            {
                snapshot.Kinds = incidents.Count;
                snapshot.Storms = Storms;
                snapshot.More = Math.Max(0, incidents.Count - maxLines);

                int take = Math.Min(maxLines, incidents.Count);
                for (int i = 0; i < take; i++)
                {
                    snapshot.Lines.Add(incidents[i].OneLine());
                }
            }

            return snapshot;
        }

        // ================== 汇总 ==================

        /// <summary>
        /// 退出时的一行汇总。没有任何模组相关错误时返回 null——<b>没出错的一局，
        /// 错误系统必须一个字都不说</b>。
        /// </summary>
        internal static string Summary()
        {
            lock (Gate)
            {
                if (incidents.Count == 0)
                {
                    // 没有任何模组相关的错误：一个字都不说。哪怕原版自己刷了几百条 LogError，
                    // 那也不是 Polaris 该在控制台里替它嚷嚷的事。
                    return null;
                }

                long total = 0;
                foreach (ErrorIncident incident in incidents)
                {
                    total += incident.Count;
                }

                string text = $"[Polaris] 本局共记录 {incidents.Count} 类错误，累计发生 {total} 次。";

                if (Storms > 0)
                {
                    text += $"其中 {Storms} 类在持续反复发生。";
                }

                if (Suppressed > 0)
                {
                    text += $"另有 {Suppressed} 类因超出上限未记录。";
                }

                if (VanillaOnly > 0)
                {
                    text += $"另有 {VanillaOnly} 次与模组无关的报错未归档。";
                }

                return text;
            }
        }
    }
}
