using System;
using System.Reflection;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 一次"主线程疑似卡死"的现场记录，由 <see cref="Watchdog"/> 在后台线程构造。
    /// <para>
    /// 刻意<b>不带主线程的托管堆栈</b>。抓另一个线程的堆栈在 Mono 上只有
    /// <c>Thread.Suspend()</c> + <c>new StackTrace(thread)</c> 这一条路，那对 API 早已过时、
    /// 在 Unity 的 Mono 上既可能拿到一份骗人的堆栈，也可能当场把进程带走——为了一份可能是错的
    /// 线索，去冒把正在卡着但没死的游戏彻底弄死的风险，不划算。
    /// </para>
    /// <para>
    /// 换成的方案是 <see cref="MainThreadBeat"/> 的面包屑：让主线程在进入模组代码之前自己留一句
    /// "我要去执行谁的什么"。这和 Polaris 归因一贯的偏好是一致的——
    /// <b>调用方直接点名，永远比事后从堆栈里推断更准</b>（见 <see cref="Infra.ErrorsAPI"/>）。
    /// </para>
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

        /// <summary>
        /// 当时主线程正在执行什么（面包屑链，由外到内）。为 null 表示不在任何 Polaris 埋点里——
        /// 那说明卡住的地方不是经 Polaris 转发出去的模组代码，可能是原版逻辑、也可能是某个模组
        /// 自己挂的 Harmony 补丁或 MonoBehaviour。
        /// </summary>
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
