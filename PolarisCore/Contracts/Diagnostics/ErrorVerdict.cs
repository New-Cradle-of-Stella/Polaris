using System.Collections.Generic;

namespace Polaris.Diagnostics
{
    /// <summary>归因结论：这次错误该找谁、凭什么这么说、有多确定。是各处展示统一渲染的唯一数据来源。</summary>
    public sealed class ErrorVerdict
    {
        internal ErrorVerdict() { }

        /// <summary>主责。判不出来时为 null（此时 <see cref="Kind"/> 是 <see cref="OwnerKind.Unknown"/>）。</summary>
        public AssemblyOwner Culprit { get; internal set; }

        /// <summary>主责类别；<see cref="Culprit"/> 为 null 时也有值，用来区分"原版"和"真不知道"。</summary>
        public OwnerKind Kind { get; internal set; }

        /// <summary>置信度。</summary>
        public ErrorConfidence Confidence { get; internal set; }

        /// <summary>为什么这么判，一句人话。</summary>
        public string Reason { get; internal set; }

        /// <summary>其它嫌疑人，按可疑程度排序。可能为空。</summary>
        public IReadOnlyList<ErrorSuspect> Suspects { get; internal set; } = new List<ErrorSuspect>();

        /// <summary>异常形状诊断（见 <see cref="ExceptionShapes"/>），比归因更具体的线索；没有时为 null。</summary>
        public string Diagnosis { get; internal set; }

        /// <summary>建议玩家做什么。没有针对性建议时为 null，由报告按主责给出通用文案。</summary>
        public string SuggestedAction { get; internal set; }

        /// <summary>这次错误是否和模组有关；只有为 true 才建档写报告，纯原版报错只计数。</summary>
        public bool IsModRelated
            => (Culprit != null && Culprit.IsBlamable) || Suspects.Count > 0;

        /// <summary>置信度标签。</summary>
        public string ConfidenceLabel
        {
            get
            {
                switch (Confidence)
                {
                    case ErrorConfidence.High: return "high";
                    case ErrorConfidence.Medium: return "medium";
                    case ErrorConfidence.Low: return "low";
                    default: return "undetermined";
                }
            }
        }

        /// <summary>一行结论；措辞随置信度变化，证据确凿才直接点名，否则用"疑似"以免冤枉作者。</summary>
        public string Headline()
        {
            if (Culprit != null)
            {
                string prefix = Confidence == ErrorConfidence.High ? "Responsible" : "Suspected";
                return $"{prefix}: {Culprit.Describe()}";
            }

            switch (Kind)
            {
                case OwnerKind.Vanilla:
                    return "Responsible: the vanilla game (no mod code in the stack)";
                case OwnerKind.Framework:
                    return "Responsible: the BepInEx / Harmony framework";
                default:
                    return "Responsible: could not be determined";
            }
        }
    }
}
