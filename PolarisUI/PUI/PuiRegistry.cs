namespace Polaris.PUI
{
    /// <summary>PUI 实例与 .puisln 图的注册/查询/显示控制；只做转发，实际逻辑在内部 <c>PUIManager</c>。</summary>
    public sealed class PuiRegistry
    {
        internal PuiRegistry() { }

        /// <summary>手动注册一个 PUI 实例为按名字的进程级共享实例。</summary>
        /// <exception cref="System.ArgumentException">同名 PUI 已注册</exception>
        public PUIRuntime Register(IPUI pui) => PUIManager.Register(pui);

        /// <summary>查询按名字共享注册的 PUI 运行时实例；未注册则抛出。</summary>
        /// <exception cref="System.ArgumentException">name 未注册</exception>
        public PUIRuntime Get(string name) => PUIManager.Get(name);

        /// <summary>查询按名字共享注册的 PUI 运行时实例；未注册返回 false。</summary>
        public bool TryGet(string name, out PUIRuntime runtime) => PUIManager.TryGet(name, out runtime);

        /// <summary>指定名称的 PUI 是否已按名字注册（自动注册或手动注册）。</summary>
        public bool IsRegistered(string name) => PUIManager.IsRegistered(name);

        /// <summary>查询一份已编译 .puisln 图的不可变蓝图；未注册返回 false。</summary>
        public bool TryGetGraph(string graphName, out PUIGraphDefinition definition) =>
            PUIManager.TryGetGraph(graphName, out definition);

        /// <summary>初始化时为该图自动创建的默认共享 <see cref="PUISolution"/> 实例。</summary>
        /// <exception cref="System.ArgumentException">graphName 未注册</exception>
        public PUISolution GetDefaultSolution(string graphName) => PUIManager.GetDefaultSolution(graphName);

        /// <summary>显示指定按名字共享的 PUI；尚未构建会先构建（GetUIWindow + BuildUI）再激活。</summary>
        /// <exception cref="System.ArgumentException">name 未注册</exception>
        public void Show(string name) => PUIManager.ShowUI(name);

        /// <summary>隐藏指定按名字共享的 PUI（不销毁，可再次 <see cref="Show"/>）。</summary>
        /// <exception cref="System.ArgumentException">name 未注册</exception>
        public void Hide(string name) => PUIManager.HideUI(name);

        /// <summary>销毁指定按名字共享的 PUI 的运行时对象；销毁后不可再显示。</summary>
        /// <exception cref="System.ArgumentException">name 未注册</exception>
        public void Close(string name) => PUIManager.CloseUI(name);

        /// <summary>让指定按名字共享的 PUI 抢占引擎输入焦点。</summary>
        /// <exception cref="System.ArgumentException">name 未注册</exception>
        public void Focus(string name) => PUIManager.FocusUI(name);

        /// <summary>查询指定按名字共享的 PUI 当前所处的生命周期状态。</summary>
        /// <exception cref="System.ArgumentException">name 未注册</exception>
        public PUIState GetState(string name) => PUIManager.GetState(name);
    }
}
