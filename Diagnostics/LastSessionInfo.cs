using System;
using System.Collections.Generic;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 上一局的结局。由 <see cref="SessionSentinel"/> 在启动时从上一局留下的哨兵文件里读出来，
    /// 只在<b>上一局没有正常结束</b>时才存在（正常退出的那一局会把哨兵文件删掉，没什么可说的）。
    /// <para>
    /// 这个对象存在的意义是：崩溃与卡死是唯一一类"出事的那一局自己没机会汇报"的问题——
    /// 进程已经没了，<c>OnApplicationQuit</c> 不会来，异常捕获那三条通道一条都没响。所以只能
    /// 换个方向：上一局边跑边把状态留在盘上，下一局启动时回头读。
    /// </para>
    /// </summary>
    public sealed class LastSessionInfo
    {
        internal LastSessionInfo() { }

        /// <summary>怎么结束的。</summary>
        public SessionEndKind Kind { get; internal set; }

        /// <summary>上一局的进程启动时间；读不到为 <see cref="DateTime.MinValue"/>。</summary>
        public DateTime StartedAt { get; internal set; }

        /// <summary>
        /// 上一局最后一次被看到还活着的时间（哨兵每几秒刷一次盘）。它和
        /// <see cref="StartedAt"/> 的差值就是"上一局玩了多久"，和当前时间无关。
        /// </summary>
        public DateTime LastAliveAt { get; internal set; }

        /// <summary>最后一次推进到的帧号；0 表示上一局连一帧都没跑到（卡在启动阶段）。</summary>
        public int LastFrame { get; internal set; }

        /// <summary>最后所在的场景名；读不到为 null。</summary>
        public string Scene { get; internal set; }

        /// <summary>
        /// 停止响应时主线程正在执行什么（<see cref="MainThreadBeat"/> 的面包屑链）。
        /// 为 null 表示当时不在任何 Polaris 埋点里——这本身也是线索，说明卡的地方不是
        /// 由 Polaris 转发出去的模组代码。
        /// </summary>
        public string Activity { get; internal set; }

        /// <summary>
        /// 判定卡死时主线程已经停了多少秒；<see cref="Kind"/> 不是
        /// <see cref="SessionEndKind.Hung"/> 时为 0。
        /// </summary>
        public double StallSeconds { get; internal set; }

        /// <summary>上一局的报告文件路径；上一局没写出过报告为 null。</summary>
        public string ReportPath { get; internal set; }

        /// <summary>上一局跑的 Polaris 版本。玩家换过版本时，这一条能省掉一轮猜。</summary>
        public string PolarisVersion { get; internal set; }

        /// <summary>上一局归档过的错误种类数。</summary>
        public int ErrorKinds { get; internal set; }

        /// <summary>
        /// 上一局的错误一行式摘要（哨兵只留前几条）。<b>这是崩溃检测顺带救回来的东西</b>：
        /// 以前这份摘要只在 <c>OnApplicationQuit</c> 里写，于是崩溃那一局——信息最值钱的那一局——
        /// 恰好是唯一什么都留不下的一局。
        /// </summary>
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
                : $"（最后一次活动：{LastAliveAt:yyyy-MM-dd HH:mm:ss}）";

            switch (Kind)
            {
                case SessionEndKind.Hung:
                    return $"上一局疑似卡死：主线程停止推进约 {StallSeconds:0} 秒{when}。";

                case SessionEndKind.NotClosed:
                    return $"上一局没有正常退出{when}。";

                default:
                    return "上一局的结束方式无从判断。";
            }
        }

        /// <summary>
        /// 报告与告知页共用的一行现场描述（帧号 / 场景 / 当时在执行什么）。
        /// 刻意是语言中性的：这里面全是帧号、场景名、模块名这类不该被翻译的东西。
        /// </summary>
        internal string Where()
        {
            var parts = new List<string>(3);

            parts.Add(LastFrame > 0 ? $"frame {LastFrame}" : "frame 0（还没进入主循环）");

            if (!string.IsNullOrEmpty(Scene))
            {
                parts.Add($"scene {Scene}");
            }

            if (!string.IsNullOrEmpty(Activity))
            {
                parts.Add(Activity);
            }

            return string.Join(" · ", parts.ToArray());
        }
    }
}
