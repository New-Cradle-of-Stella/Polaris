using nel;

namespace Polaris.PUI
{
    public interface IPUI
    {
        public string Name { get; }

        public UiBoxDesigner GetUIWindow(UiBoxDesignerFamily source);

        public void BuildUI(UiBoxDesigner designer);
    }
}
