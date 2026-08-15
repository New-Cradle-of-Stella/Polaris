namespace Polaris.PUI
{
    /// <summary>
    /// 单个 PUI 运行时状态机的状态。
    /// </summary>
    public enum PUIState
    {
        /// <summary>已注册但尚未创建 GameObject/UiBoxDesignerFamily。</summary>
        Unbuilt,

        /// <summary>已构建且当前可见/可交互。</summary>
        Shown,

        /// <summary>已构建但当前隐藏。</summary>
        Hidden,

        /// <summary>已销毁，不可再使用（终态）。</summary>
        Destroyed
    }

    /// <summary>
    /// 驱动 <see cref="PUIState"/> 迁移的触发动作。
    /// </summary>
    public enum PUITrigger
    {
        Show,
        Hide,
        Destroy
    }
}
