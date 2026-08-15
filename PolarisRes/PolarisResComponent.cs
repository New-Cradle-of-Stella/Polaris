using Polaris.Components;

namespace Polaris.Res
{
    public sealed class PolarisResComponent : PolarisComponent
    {
        public override string Id => "PolarisRes";

        public override int Order => 100;

        public override void Awake() => ResStrings.Register();

        public override void Start() => Runtime.ResRuntime.Init();
    }
}
