using System;
using System.Collections.Generic;
using System.Text;

namespace Polaris.Diagnostics
{
    /// <summary>一类错误在本局的完整记录；按 <see cref="Fingerprint"/> 归并同类错误，累加 <see cref="Count"/>。</summary>
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

        /// <summary>是否已判定为持续性故障（异常风暴）：短时间内反复发生，游戏实际已不可玩。判定见 <see cref="ErrorRegistry"/>。</summary>
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

        /// <summary>上报时的上下文（如 "PUI子系统初始化"）；全局兜底抓到的异常没有，为 null。</summary>
        public string Context { get; internal set; }

        /// <summary>归因结论。</summary>
        public ErrorVerdict Verdict { get; internal set; }

        /// <summary>标注好归属的堆栈帧。</summary>
        public IReadOnlyList<ErrorFrame> Frames { get; internal set; } = new List<ErrorFrame>();

        /// <summary>原始堆栈文本，原样保留供模组作者自行核实归属判断。</summary>
        public string RawStackTrace { get; internal set; }

        /// <summary>完整异常链文本（含 InnerException），展示用，定责仅看最内层。</summary>
        public string ExceptionChain { get; internal set; }

        /// <summary>控制台与告知页共用的一行摘要，如 <c>NullReferenceException — 疑似模组「WeNeedMoreNoels」</c>。</summary>
        internal string OneLine()
        {
            string type = ExceptionType;
            if (string.IsNullOrEmpty(type))
            {
                type = "unknown exception";
            }
            else
            {
                int dot = type.LastIndexOf('.');
                if (dot >= 0 && dot < type.Length - 1)
                {
                    type = type.Substring(dot + 1);
                }
            }

            return $"{type} -- {Verdict.Headline()}";
        }

        // ================== 指纹 ==================

        /// <summary>
        /// 指纹 = 异常类型 + 最内 <see cref="FingerprintFrames"/> 个可归属帧的"类型.方法"，不含消息/行号。
        /// 用 FNV-1a 而非 <c>string.GetHashCode</c>，因为该值要跨启动写入配置文件比对，须保证稳定。
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
