namespace Polaris
{
    /// <summary>
    /// Polaris 自建 UI 该摆在哪个 z 的唯一一份约定。
    /// <para>
    /// 游戏这套 UI 没有 Canvas、也不用 <c>sortingOrder</c>（<c>unsafeAssem</c> 里一次都没出现），
    /// 全部是同一个 render queue（3000）里的透明网格，前后顺序只由到相机的距离决定：<b>z 越小
    /// 越靠前</b>。也就是说"我的窗口有没有被原版的文字压住"这件事，完全取决于宿主 GameObject
    /// 的 z——而 <c>new GameObject()</c> 默认在 z=0，正好和标题场景里那些常驻文本平局，平局时
    /// 谁在前纯看运气。
    /// </para>
    /// <para>
    /// 标题场景（<c>nel.title.SceneTitleTemp</c>）的 z 分布，反编译核实过：
    /// <list type="bullet">
    /// <item>标题 logo 那一大坨网格（<c>MdLogo</c>）在 <b>+0.1 一带</b>，比什么都靠后；</item>
    /// <item>版本号文本 <c>TxVer</c>（"ver 0.29j / (26/02/10 Early Access Version XI)"）、版权行
    /// 等 <c>SceneTitleTemp</c> 的直接子对象在 <b>z=0</b>；</item>
    /// <item>标题自己的按钮/说明框容器 <c>BxCon</c> 在 <b>-0.125</b>，语言按钮在 <b>-0.2</b>；</item>
    /// <item>真正的全屏覆盖层要靠前得多：调试提示 -1、初次启动询问 -2、按键设置 -4.25。</item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class UiDepth
    {
        /// <summary>
        /// Polaris 自建的窗口（模组管理页、PUI 窗口）在标题场景里的宿主 z。
        /// <para>
        /// -0.5 稳稳盖住标题界面所有常驻 UI（最靠前的语言按钮才 -0.2），又不会越过那些真正的
        /// 全屏覆盖层（-1 及更前，见类注释）。原版自己的分类内容区 <c>UiGMC.BxR</c> 也正是
        /// setZ(-0.5f)，所以这个取值同时也跟原版的习惯对齐。
        /// </para>
        /// <para>
        /// 注意 <c>XX.Designer</c> 的圆角底板（<c>MdKadomaru</c>）是挂在自己身上 +0.008 处的：
        /// 宿主留在默认的 z=0 时，底板落到 +0.008，于是被 z=0 的版本号文本压住——截图里
        /// "ver 0.29j / Early Access Version" 糊在面板上就是这么来的。
        /// </para>
        /// </summary>
        internal const float Window = -0.5f;
    }
}
