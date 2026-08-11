using System;
using System.Collections.Generic;
using System.Text;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 一类错误在本局游戏里的完整记录。<b>一类</b>不是一次：<c>Update()</c> 里抛的异常
    /// 一秒钟来 60 次，按次建档只会把日志和报告冲垮，所以按
    /// <see cref="Fingerprint"/> 归并，同一指纹只留一条、累加 <see cref="Count"/>。
    /// </summary>
    public sealed class ErrorIncident
    {
        internal ErrorIncident() { }

        /// <summary>本局内的序号，从 1 开始，报告里的"事件 #N"。</summary>
        public int Index { get; internal set; }

        /// <summary>去重指纹，见 <see cref="ComputeFingerprint"/>。</summary>
        public string Fingerprint { get; internal set; }

        /// <summary>首次发生时间（本地时间）。</summary>
        public DateTime FirstSeen { get; internal set; }

        /// <summary>最近一次发生时间。</summary>
        public DateTime LastSeen { get; internal set; }

        /// <summary>累计发生次数。</summary>
        public int Count { get; internal set; }

        /// <summary>
        /// 这一类错误是否已经被判定为<b>持续性故障</b>（"异常风暴"）：短时间内反复发生，
        /// 通常意味着它长在 <c>Update</c> 这类每帧都会走的地方，游戏实际上已经不能玩了。
        /// <para>
        /// 单独判一次是有必要的：去重机制让"启动时抛了一次"和"每帧抛 60 次"在报告里长得
        /// 一模一样（都是一条记录），只有 <see cref="Count"/> 不同——而后者对玩家的意义完全不同，
        /// 不该指望玩家自己去读那个数字。判定见 <see cref="ErrorRegistry"/>。
        /// </para>
        /// </summary>
        public bool IsStorm { get; internal set; }

        /// <summary>被判定为持续性故障的时刻；没判定过为 <see cref="DateTime.MinValue"/>。</summary>
        public DateTime StormDetectedAt { get; internal set; }

        /// <summary>判定为持续性故障时，窗口内已经发生了多少次。</summary>
        public int StormBurst { get; internal set; }

        // 滑动窗口的游标。只由 ErrorRegistry 在锁内改动。
        internal DateTime StormWindowStart;
        internal int StormWindowCount;

        /// <summary>异常类型全名；Unity 只给字符串时是从消息头解析出来的。</summary>
        public string ExceptionType { get; internal set; }

        /// <summary>异常消息。</summary>
        public string Message { get; internal set; }

        /// <summary>
        /// 上报时的上下文，例如 <c>PUI子系统初始化</c>。由 Polaris 自己的 catch 点
        /// 或模组通过 <see cref="Infra.ErrorsAPI.Report(Exception, string)"/> 提供；
        /// 全局兜底抓到的异常没有上下文，为 null。
        /// </summary>
        public string Context { get; internal set; }

        /// <summary>归因结论。</summary>
        public ErrorVerdict Verdict { get; internal set; }

        /// <summary>标注好归属的堆栈帧。</summary>
        public IReadOnlyList<ErrorFrame> Frames { get; internal set; } = new List<ErrorFrame>();

        /// <summary>
        /// 原始堆栈文本。归属标注是我们的解读，可能解读错；原文必须原样留在报告里，
        /// 模组作者拿到报告后要能自己判断。
        /// </summary>
        public string RawStackTrace { get; internal set; }

        /// <summary>
        /// 完整异常链的文本（含 InnerException）。定责只看最内层，但展示要给全——
        /// <c>TypeInitializationException</c> 这类外壳异常本身就携带"哪个静态构造函数炸了"的信息。
        /// </summary>
        public string ExceptionChain { get; internal set; }

        /// <summary>
        /// 控制台与标题告知页共用的一行摘要，例如 <c>NullReferenceException — 疑似模组「WeNeedMoreNoels」</c>。
        /// 只留异常类名（不带命名空间），一行放不下 <c>System.NullReferenceException</c> 那种全名。
        /// </summary>
        internal string OneLine()
        {
            string type = ExceptionType;
            if (string.IsNullOrEmpty(type))
            {
                type = "未知异常";
            }
            else
            {
                int dot = type.LastIndexOf('.');
                if (dot >= 0 && dot < type.Length - 1)
                {
                    type = type.Substring(dot + 1);
                }
            }

            return $"{type} — {Verdict.Headline()}";
        }

        // ================== 指纹 ==================

        /// <summary>
        /// 指纹 = 异常类型 + 最内 <see cref="FingerprintFrames"/> 个可归属帧的"类型.方法"。
        /// <para>
        /// 不含异常消息：消息里常带坐标、对象名、帧号这类每次都不同的东西，算进去等于没去重。
        /// 不含行号：Release 构建下本来就没有。
        /// </para>
        /// <para>
        /// 用 FNV-1a 而不是 <c>string.GetHashCode</c>：后者不保证跨进程稳定，而这个值要写进
        /// 配置文件跨启动比对（告知页要认出"上次那条错误"）。
        /// </para>
        /// </summary>
        internal const int FingerprintFrames = 5;

        internal static string ComputeFingerprint(string exceptionType, IReadOnlyList<ErrorFrame> frames)
        {
            var builder = new StringBuilder(exceptionType ?? "?");

            int used = 0;
            foreach (ErrorFrame frame in frames)
            {
                if (frame.Owner != null && frame.Owner.Kind == OwnerKind.Runtime)
                {
                    continue;
                }

                builder.Append('|').Append(frame.TypeName).Append('.').Append(frame.MethodName);
                if (++used >= FingerprintFrames)
                {
                    break;
                }
            }

            return Fnv1a(builder.ToString());
        }

        static string Fnv1a(string value)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;

            uint hash = offset;
            foreach (char c in value)
            {
                hash = (hash ^ c) * prime;
            }

            return hash.ToString("x8");
        }
    }
}
