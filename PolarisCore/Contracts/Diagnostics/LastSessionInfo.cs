using System;
using System.Collections.Generic;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 上一局的结局，由 <see cref="SessionSentinel"/> 从哨兵文件读出，只在上一局未正常结束时存在
    /// （正常退出会删掉哨兵文件；崩溃/卡死时进程已死，没有别的机会汇报）。
    /// </summary>
    public sealed class LastSessionInfo
    {
        internal LastSessionInfo() { }

        /// <summary>怎么结束的。</summary>
        public SessionEndKind Kind { get; internal set; }

        /// <summary>上一局的进程启动时间；读不到为 <see cref="DateTime.MinValue"/>。</summary>
        public DateTime StartedAt { get; internal set; }

        /// <summary>上一局最后一次被看到还活着的时间；与 <see cref="StartedAt"/> 的差值就是玩了多久。</summary>
        public DateTime LastAliveAt { get; internal set; }

        /// <summary>最后一次推进到的帧号；0 表示上一局连一帧都没跑到（卡在启动阶段）。</summary>
        public int LastFrame { get; internal set; }

        /// <summary>最后所在的场景名；读不到为 null。</summary>
        public string Scene { get; internal set; }

        /// <summary>停止响应时主线程正在执行什么（面包屑链）；为 null 表示不在任何 Polaris 埋点里。</summary>
        public string Activity { get; internal set; }

        /// <summary>判定卡死时主线程已停了多少秒；非 <see cref="SessionEndKind.Hung"/> 时为 0。</summary>
        public double StallSeconds { get; internal set; }

        /// <summary>上一局的报告文件路径；上一局没写出过报告为 null。</summary>
        public string ReportPath { get; internal set; }

        /// <summary>上一局跑的 Polaris 版本。玩家换过版本时，这一条能省掉一轮猜。</summary>
        public string PolarisVersion { get; internal set; }

        /// <summary>上一局归档过的错误种类数。</summary>
        public int ErrorKinds { get; internal set; }

        /// <summary>上一局的错误一行式摘要（哨兵只留前几条），是崩溃检测顺带救回的信息。</summary>
        public IReadOnlyList<string> ErrorLines { get; internal set; } = new List<string>();

        /// <summary>超出 <see cref="ErrorLines"/> 之外还有几类。</summary>
        public int MoreErrorKinds { get; internal set; }

        /// <summary>上一局有几类错误被判定为持续反复发生（见 <see cref="ErrorRegistry"/> 的风暴判定）。</summary>
        public int StormKinds { get; internal set; }

        /// <summary>控制台用的一行结论。</summary>
        internal string OneLine()
        {
            string when = LastAliveAt == DateTime.MinValue
                ? ""
                : $" (last activity: {LastAliveAt:yyyy-MM-dd HH:mm:ss})";

            switch (Kind)
            {
                case SessionEndKind.Hung:
                    return $"The previous session probably hung: the main thread stopped advancing for about {StallSeconds:0}s{when}.";

                case SessionEndKind.NotClosed:
                    return $"The previous session did not exit cleanly{when}.";

                default:
                    return "How the previous session ended cannot be determined.";
            }
        }

        /// <summary>报告与告知页共用的一行现场描述（帧号 / 场景 / 当时在执行什么），保持语言中性。</summary>
        internal string Where()
        {
            var parts = new List<string>(3);

            parts.Add(LastFrame > 0 ? $"frame {LastFrame}" : "frame 0 (main loop not entered yet)");

            if (!string.IsNullOrEmpty(Scene))
            {
                parts.Add($"scene {Scene}");
            }

            if (!string.IsNullOrEmpty(Activity))
            {
                parts.Add(Activity);
            }

            return string.Join(" | ", parts.ToArray());
        }
    }
}
