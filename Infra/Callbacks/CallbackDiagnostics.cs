using System.Collections.Generic;
using System.Text;

namespace Polaris.Infra
{
    /// <summary>
    /// 每个订阅者的耗时与异常统计。只在主线程写（派发本身就是主线程独占的），不需要锁。
    /// 默认只记录，不自动禁用任何订阅——一次慢调用不代表这个模组坏了。
    /// </summary>
    internal static class CallbackDiagnostics
    {
        const double SlowWarnMillis = 8.0;

        internal sealed class Stat
        {
            public long Calls;
            public double MaxMillis;
            public long ExceptionCount;
        }

        static readonly Dictionary<string, Stat> stats = new();

        internal static void RecordInvocation(string ownerGuid, string context, double millis)
        {
            Stat stat = GetOrCreate(ownerGuid, context);
            stat.Calls++;
            if (millis > stat.MaxMillis)
            {
                stat.MaxMillis = millis;
            }

            if (millis >= SlowWarnMillis)
            {
                Plugin.Logger.LogWarning(
                    $"[Polaris] Callback '{context}' (owner {ownerGuid}) took {millis:F1}ms this call.");
            }
        }

        internal static void RecordException(string ownerGuid, string context)
        {
            GetOrCreate(ownerGuid, context).ExceptionCount++;
        }

        static Stat GetOrCreate(string ownerGuid, string context)
        {
            string key = $"{ownerGuid}|{context}";
            if (!stats.TryGetValue(key, out Stat stat))
            {
                stat = new Stat();
                stats[key] = stat;
            }

            return stat;
        }

        /// <summary>诊断页/报告用的只读快照：键是 "owner|context"。</summary>
        internal static IReadOnlyDictionary<string, Stat> Snapshot() => stats;

        internal static string Summary()
        {
            if (stats.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (KeyValuePair<string, Stat> kv in stats)
            {
                sb.Append(kv.Key)
                  .Append(" calls=").Append(kv.Value.Calls)
                  .Append(" maxMs=").Append(kv.Value.MaxMillis.ToString("F2"))
                  .Append(" exceptions=").Append(kv.Value.ExceptionCount)
                  .Append(System.Environment.NewLine);
            }

            return sb.ToString();
        }
    }
}
