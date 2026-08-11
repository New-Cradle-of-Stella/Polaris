using System.Collections.Generic;

namespace Polaris.Diagnostics
{
    /// <summary>
    /// 归因结论：这次错误该找谁、凭什么这么说、有多确定。
    /// <para>
    /// 这是整套系统对外交付的东西。用户看到的每一句话——控制台那一行、报告文件的"判定"段、
    /// 标题画面告知页上的一条——都是这个对象的不同渲染，而不是各处再各拼一遍字符串。
    /// </para>
    /// </summary>
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

        /// <summary>
        /// 异常形状诊断（见 <see cref="ExceptionShapes"/>）。比归因更具体的那种线索：
        /// <c>MissingMethodException</c> 基本就等于"某个模组是照另一个版本编译的"。没有时为 null。
        /// </summary>
        public string Diagnosis { get; internal set; }

        /// <summary>建议玩家做什么。没有针对性建议时为 null，由报告按主责给出通用文案。</summary>
        public string SuggestedAction { get; internal set; }

        /// <summary>
        /// 这次错误是否和模组有关系——堆栈里出现了模组/Polaris 的代码，或者沿途的原版方法
        /// 被模组改过。
        /// <para>
        /// 这是"全局兜底 + 智能过滤"里的那个过滤器：Polaris 什么都抓，但只有这个为 true 的
        /// 才建档、写报告、弹告知页。纯原版的报错只计数——原版自己的毛病不该由 Polaris
        /// 拿到玩家面前去，那既不是我们的职责，也只会制造噪音。
        /// </para>
        /// </summary>
        public bool IsModRelated
            => (Culprit != null && Culprit.IsBlamable) || Suspects.Count > 0;

        /// <summary>置信度的中文标签。</summary>
        public string ConfidenceLabel
        {
            get
            {
                switch (Confidence)
                {
                    case ErrorConfidence.High: return "高";
                    case ErrorConfidence.Medium: return "中";
                    case ErrorConfidence.Low: return "低";
                    default: return "无法判定";
                }
            }
        }

        /// <summary>
        /// 一行结论，控制台和告知页都用它。措辞随置信度变：证据确凿时直接点名，
        /// 间接证据时说"疑似"——Polaris 说得越像回事，玩家越可能照着去骂一个无辜的作者。
        /// </summary>
        public string Headline()
        {
            if (Culprit != null)
            {
                string prefix = Confidence == ErrorConfidence.High ? "责任方" : "疑似";
                return $"{prefix}：{Culprit.Describe()}";
            }

            switch (Kind)
            {
                case OwnerKind.Vanilla:
                    return "责任方：原版游戏（堆栈里没有任何模组代码）";
                case OwnerKind.Framework:
                    return "责任方：BepInEx / Harmony 框架";
                default:
                    return "责任方：无法判定";
            }
        }
    }
}
