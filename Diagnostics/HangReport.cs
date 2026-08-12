using System;
using System.Reflection;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 一次"主线程疑似卡死"的现场记录，由 <see cref="Watchdog"/> 在后台线程构造。
    /// 刻意不抓主线程堆栈（在 Mono 上不安全，可能弄死正卡住但没死的进程），
    /// 改用 <see cref="MainThreadBeat"/> 的面包屑记录主线程正在做什么。
    /// </summary>
    public sealed class HangReport
    {
        internal HangReport() { }

        /// <summary>判定时刻（本地时间）。</summary>
        public DateTime DetectedAt { get; internal set; }

        /// <summary>判定时主线程已经停了多少秒。</summary>
        public double StallSeconds { get; internal set; }

        /// <summary>主线程最后推进到的帧号；0 表示还没进入主循环，卡在启动阶段。</summary>
        public int LastFrame { get; internal set; }

        /// <summary>当时的场景名；不知道为 null。</summary>
        public string Scene { get; internal set; }

        /// <summary>当时主线程正在执行什么（面包屑链）；为 null 表示不在任何 Polaris 埋点里。</summary>
        public string Activity { get; internal set; }

        /// <summary>面包屑栈顶那一层的责任程序集；埋点没给出责任方时为 null。</summary>
        public Assembly Culprit { get; internal set; }

        /// <summary>本局第几次判定卡死，从 1 开始。</summary>
        public int Index { get; internal set; }

        /// <summary>是否发生在首个 <c>Update</c> 之前（启动阶段卡住，游戏根本没跑起来）。</summary>
        public bool DuringBoot { get; internal set; }

        /// <summary>控制台与报告共用的一行摘要。</summary>
        internal string OneLine()
        {
            string where = Activity ?? "(was not inside any Polaris instrumentation point)";
            return DuringBoot
                ? $"stuck for about {StallSeconds:0}s during startup: {where}"
                : $"main thread stopped advancing for about {StallSeconds:0}s (frame {LastFrame}): {where}";
        }
    }
}
