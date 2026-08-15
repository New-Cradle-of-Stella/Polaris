namespace Polaris.Components
{
    /// <summary>
    /// Polaris 功能组件的统一生命周期。组件是普通类库，由 PolarisCore 发现并驱动；
    /// 组件程序集本身不得声明 BepInEx 插件入口。
    /// </summary>
    public abstract class PolarisComponent
    {
        public abstract string Id { get; }

        public virtual int Order => 0;

        /// <summary>
        /// Core 启动基础设施前的极早期注册阶段。仅用于提供 Core 契约的模块（例如诊断后端）；
        /// 普通组件应继续使用 <see cref="Awake"/>。
        /// </summary>
        public virtual void Bootstrap() { }

        public virtual void Awake() { }

        public virtual void Start() { }

        public virtual void Update() { }

        public virtual void LateUpdate() { }

        public virtual void Shutdown() { }
    }
}
