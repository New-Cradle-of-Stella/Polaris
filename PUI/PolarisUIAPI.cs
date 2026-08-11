namespace Polaris.PUI
{
    /// <summary>
    /// PolarisUI 对外的唯一入口，与 <c>PolarisResAPI</c>/<c>PolarisLangAPI</c> 形态一致
    /// （一模组一门面）。之前公开面散在 <c>PUIManager</c>/<c>MainMenuPUI</c>/<c>GameMenuPUI</c>
    /// 三个静态类上，模组作者要打开一个 PUI 窗口得同时认识三个名字。
    /// <para>
    /// 门面只做转发与创建，不含业务逻辑。<see cref="PUIRuntime"/>/<see cref="PUISolution"/>/
    /// <see cref="IPUI"/> 这些是被返回/被实现的对象类型，不是入口，仍然直接公开。
    /// </para>
    /// <para>
    /// 这里刻意<b>不</b>转发 Polaris 已有的主菜单/游戏菜单能力（加普通按钮、设置底部
    /// 命令按钮条、写操作提示行）——那些用 <c>PolarisAPI.MainMenu.*</c>/<c>PolarisAPI.GameMenu.*</c>。
    /// 本门面下的 <see cref="MainMenu"/>/<see cref="GameMenu"/> 只负责"把 PUI 接到那些能力上"
    /// 这件 PolarisUI 自己才懂的事。
    /// </para>
    /// </summary>
    public static class PolarisUIAPI
    {
        /// <summary>PUI 实例与 .puisln 图的注册、查询与显示控制。</summary>
        public static PuiRegistry Pui { get; } = new();

        /// <summary>把 PUI / PUI 状态机接到主菜单按钮上（点击打开、ESC 关闭、底部按钮条联动）。</summary>
        public static MainMenuPuiFacade MainMenu { get; } = new();

        /// <summary>把 PUI 的内容接到游戏内 ESC 菜单的分类内容区上。</summary>
        public static GameMenuPuiFacade GameMenu { get; } = new();
    }
}
