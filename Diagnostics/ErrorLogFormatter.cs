using System.Linq;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 把一条 <see cref="ErrorIncident"/> 写成 BepInEx 控制台里的几行结论。
    /// <para>
    /// 沿用仓库既有的两条日志约定：以 <c>[Polaris]</c> 开头，以及每条消息都要说清楚
    /// <b>玩家因此失去了什么、接下来该做什么</b>（"它负责的功能本局不可用"那种从句）。
    /// 完整堆栈不往控制台倒——那是报告文件的活儿，控制台只放结论。
    /// </para>
    /// </summary>
    internal static class ErrorLogFormatter
    {
        /// <summary>嫌疑人最多念几个，多了控制台就没法看了。</summary>
        const int MaxSuspectLines = 4;

        internal static void Log(ErrorIncident incident)
        {
            ErrorVerdict verdict = incident.Verdict;

            string head = incident.Context == null
                ? $"[Polaris] 捕获到错误：{Short(incident.ExceptionType)}"
                : $"[Polaris] {incident.Context}时出错：{Short(incident.ExceptionType)}";

            if (!string.IsNullOrEmpty(incident.Message))
            {
                head += $" —— {Clip(incident.Message, 160)}";
            }

            Plugin.Logger.LogError(head);
            Plugin.Logger.LogError($"[Polaris] {verdict.Headline()}（置信度：{verdict.ConfidenceLabel}）{verdict.Reason}");

            if (verdict.Diagnosis != null)
            {
                Plugin.Logger.LogError($"[Polaris] 诊断：{verdict.Diagnosis}");
            }

            // 主责已经点名时不再复述嫌疑人；只有"点不出主责"或"还有别的嫌疑人"才值得列。
            var others = verdict.Suspects.Where(s => s.Owner != verdict.Culprit).ToList();
            if (others.Count > 0)
            {
                Plugin.Logger.LogError($"[Polaris] 其它嫌疑：{Join(others.Take(MaxSuspectLines))}"
                                       + (others.Count > MaxSuspectLines ? $" 等 {others.Count} 个" : string.Empty));
            }

            string report = ErrorReportWriter.LastWrittenPath;
            Plugin.Logger.LogError(report != null
                ? $"[Polaris] 完整报告（含堆栈与已装模组清单）：{report}"
                : "[Polaris] 报告文件写入失败，完整堆栈只能从本日志和 Unity 的 Player.log 里找。");
        }

        static string Join(System.Collections.Generic.IEnumerable<ErrorSuspect> suspects)
            => string.Join("、", suspects.Select(s => s.Owner.Describe()).ToArray());

        /// <summary>只留类名，控制台一行放不下 <c>System.NullReferenceException</c> 那种全名。</summary>
        static string Short(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return "未知异常";
            }

            int dot = typeName.LastIndexOf('.');
            return dot >= 0 && dot < typeName.Length - 1 ? typeName.Substring(dot + 1) : typeName;
        }

        static string Clip(string text, int max)
        {
            string flat = text.Replace('\r', ' ').Replace('\n', ' ');
            return flat.Length <= max ? flat : flat.Substring(0, max) + "…";
        }
    }
}
