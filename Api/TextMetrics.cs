using XX;

namespace Polaris.API
{
    /// <summary>
    /// 文本块的实测尺寸；需要通过 Publicizer 访问 <c>FillBlock</c> 的私有字段 <c>Tm</c>。
    /// </summary>
    internal static class TextMetrics
    {
        /// <summary>
        /// 文本实际占用的高度（像素，含上下留白）。用 <c>Tm.get_sheight_px()</c> 而非公开的
        /// <c>FillBlock.get_sheight_px()</c>，因为后者对固定高度的块总返回固定值。文案为空时返回 0。
        /// </summary>
        internal static float TextHeightOf(FillBlock block)
        {
            TextRenderer renderer = block?.Tm;
            return renderer == null ? 0f : renderer.get_sheight_px() + block.margin_y * 2f;
        }
    }
}
