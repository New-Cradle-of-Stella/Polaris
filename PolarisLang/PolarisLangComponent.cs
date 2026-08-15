using Polaris.Components;

namespace Polaris.Lang
{
    public sealed class PolarisLangComponent : PolarisComponent
    {
        public override string Id => "PolarisLang";

        public override int Order => 200;

        public override void Awake() => LangStrings.Register();

        public override void Start()
        {
            PlangRegistryScanner.ScanAll();
            PolarisAPI.Localization.RegisterResolver(PlangRuntime.Get);
        }
    }
}
