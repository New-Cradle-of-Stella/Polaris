using Polaris.Components;

namespace Polaris.Sandbox
{
    /// <summary>沙盒隔离与实验性能力的组件入口。</summary>
    public sealed class PolarisSandboxComponent : PolarisComponent
    {
        public override string Id => "PolarisSandbox";
        public override int Order => 600;
    }
}
