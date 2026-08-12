namespace Polaris.PUI
{
    /// <summary>PolarisUI 对外的唯一入口，只做转发与创建，不含业务逻辑。</summary>
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
