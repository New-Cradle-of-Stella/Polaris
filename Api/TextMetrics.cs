using XX;

namespace Polaris.API
{
    /// <summary>
    /// 文本块的实测尺寸。收在 <c>Polaris.API</c> 下的理由见 <see cref="GameStateAPI"/>：
    /// 这里要碰 <c>FillBlock</c> 的私有字段 <c>Tm</c>，那是只有 Publicizer 才触达得到的
    /// 游戏内部结构，整个系列只在这一处做。
    /// </summary>
    internal static class TextMetrics
    {
        /// <summary>
        /// 文本<b>实际占用</b>的高度（像素，含块自己的上下留白）。
        /// <para>
        /// 不能用公开的 <c>FillBlock.get_sheight_px()</c>：它返回的是
        /// <c>Mx(文本高度 + margin, heightPixel)</c>——对固定高度的块来说永远等于那个固定高度，
        /// 正好把"文案比框高"这件唯一想知道的事抹掉了。<c>Tm.get_sheight_px()</c> 才是量出来的
        /// 那一份（内部会先把待重绘的排版结算掉，所以刚改完文案/字号立刻问也是准的）。
        /// </para>
        /// <para>
        /// 建块时文案为空的话游戏根本不会创建 <c>Tm</c>，此时返回 0——没有文本，也就谈不上放不下。
        /// </para>
        /// </summary>
        internal static float TextHeightOf(FillBlock block)
        {
            TextRenderer renderer = block?.Tm;
            return renderer == null ? 0f : renderer.get_sheight_px() + block.margin_y * 2f;
        }
    }
}
