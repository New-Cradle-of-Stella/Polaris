using Polaris.Components;

namespace Polaris.PUI
{
    public sealed class PolarisUIComponent : PolarisComponent
    {
        public override string Id => "PolarisUI";

        public override int Order => 300;

        public override void Start()
        {
            // 必须先占住标题菜单入口，再扫描设置项，最后启动 PUI 运行时。
            PolarisManagementUI.RegisterButton();
            Settings.SettingsAttributeScanner.ScanAll();
            PUIManager.Init();
        }
    }
}
