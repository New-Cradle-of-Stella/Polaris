using System;
using System.Collections.Generic;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 本局致命错误的登记处：落日志、写报告、给标题画面的
    /// <see cref="PolarisFatalNotice"/> 上膛。
    /// <para>
    /// 与 <see cref="ErrorRegistry"/> 分开而不是复用它的一条分支：那边整套机制
    /// （指纹去重、限流、上限、"纯原版错误只计数"的过滤）都是为"每帧可能来 60 次的异常"
    /// 设计的，而致命错误一局最多来几条，且<b>一条都不能被过滤掉</b>——它决定的是玩家能不能
    /// 继续玩，被限流吞掉一条就等于放着坏环境不管。
    /// </para>
    /// </summary>
    internal static class FatalRegistry
    {
        /// <summary>
        /// 最多留几条完整记录。上限只是防"某个模组在循环里报致命错误"把内存吃光——
        /// 超出的仍然计入 <see cref="Count"/>，玩家不会被瞒着。
        /// </summary>
        const int MaxRetained = 8;

        static readonly List<FatalError> retained = new(MaxRetained);

        /// <summary>
        /// 并发闸。致命错误几乎都在主线程的初始化阶段报出，但 API 是公开的，模组从后台线程
        /// 调进来是允许的——两条同时进来会把 <see cref="retained"/> 撕坏，而这份列表正是
        /// 告知页要读的东西。
        /// </summary>
        static readonly object Gate = new object();

        /// <summary>
        /// 防重入：写报告失败会记日志，日志不该绕回来再报一条致命错误。
        /// <c>ThreadStatic</c> 而不是普通静态字段（同 <see cref="ErrorRegistry"/>）——它拦的是
        /// "同一条调用链绕回自己"，别的线程正好也在报一条不该被它顺手吞掉，那条由
        /// <see cref="Gate"/> 排队处理。
        /// </summary>
        [ThreadStatic]
        static bool inside;

        /// <summary>本局报出过的致命错误总数（含超出 <see cref="MaxRetained"/> 未留存的）。</summary>
        internal static int Count { get; private set; }

        /// <summary>本局是否已经被判死刑。</summary>
        internal static bool Any => Count > 0;

        /// <summary>
        /// 第一条致命错误，也是告知页展示的那一条。取第一条而不是最后一条：后面的往往是
        /// 第一条的连锁反应，最先报出来的那个离病根最近。
        /// </summary>
        internal static FatalError First => retained.Count > 0 ? retained[0] : null;

        /// <summary>除 <see cref="First"/> 之外还有几条；告知页据此显示"另有 N 条"。</summary>
        internal static int OtherCount => Math.Max(0, Count - 1);

        /// <summary>报告文件路径；一次都没写成功过为 null。</summary>
        internal static string ReportPath { get; private set; }

        internal static void Raise(FatalError fatal)
        {
            if (fatal == null || inside)
            {
                return;
            }

            inside = true;
            try
            {
                lock (Gate)
                {
                    Count++;
                    if (retained.Count < MaxRetained)
                    {
                        retained.Add(fatal);
                    }

                    // 先落盘再打日志：日志最后一行要报出报告文件的位置，写失败时不能对着玩家
                    // 撒谎说"已写入"（和 ErrorRegistry.Record 里同一个理由）。
                    ErrorReportWriter.AppendFatal(fatal);
                    ReportPath = ErrorReportWriter.LastWrittenPath;

                    Log(fatal);
                }
            }
            catch (Exception)
            {
                // 致命错误的登记本身炸了。这里不能再往外抛（调用点通常是某个模块的初始化，
                // 抛出去只会变成一条归因到该模块的普通异常，把真正的问题盖掉），也不能记日志。
                // 静默放弃这一条的记录，但 Count 已经加过——告知页照样会拦住玩家。
            }
            finally
            {
                inside = false;
            }
        }

        static void Log(FatalError fatal)
        {
            // 用 LogError 而不是 LogFatal：LogFatal 会被 ErrorCapture 的日志监听器当成
            // "插件报出的严重错误"再建一条普通事件档（见 PolarisLogListener），同一件事在
            // 报告里出现两遍。这里本来就已经自己写过报告了。
            Plugin.Logger.LogError(
                $"[Polaris] 致命错误（由 {fatal.Source} 报出）：{fatal.Reason?.ForReport}");

            foreach (string detail in fatal.Details)
            {
                Plugin.Logger.LogError($"[Polaris]   · {detail}");
            }

            Plugin.Logger.LogError(
                "[Polaris] 本局不会继续：标题画面会拦住菜单并请玩家退出游戏。"
                + (ReportPath != null ? $"报告：{ReportPath}" : "（报告文件写入失败，详情见上面几行）"));
        }
    }
}
