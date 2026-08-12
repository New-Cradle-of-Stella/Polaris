namespace Polaris
{
    /// <summary>
    /// Polaris 自建 UI 该摆在哪个 z 的唯一约定：游戏 UI 全在同一 render queue，z 越小越靠前，无
    /// Canvas/sortingOrder 概念。标题场景 z 分布：logo +0.1、常驻文本 z=0、BxCon -0.125、语言按钮
    /// -0.2、全屏覆盖层 -1 及更前。
    /// </summary>
    internal static class UiDepth
    {
        /// <summary>Polaris 自建窗口（模组管理页、PUI 窗口）在标题场景里的宿主 z；-0.5 盖住常驻 UI 又不越过全屏覆盖层，与原版 <c>UiGMC.BxR</c> 取值一致。</summary>
        internal const float Window = -0.5f;
    }
}
