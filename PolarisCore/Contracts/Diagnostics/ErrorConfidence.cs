namespace Polaris.Diagnostics
{
    /// <summary>归因结论的可信程度，四档而非百分数，方便直接展示给玩家。</summary>
    public enum ErrorConfidence
    {
        /// <summary>没有结论。</summary>
        Unknown = 0,

        /// <summary>有嫌疑人但分不出主次，例如同一个原版方法被好几个模组同时改过。</summary>
        Low,

        /// <summary>间接证据成立，例如堆栈里没有模组的帧，但沿途的原版方法只被某一个模组改过。</summary>
        Medium,

        /// <summary>直接证据：堆栈里就有责任人自己的帧，或者调用方在上报时直接点了名。</summary>
        High,
    }
}
