using nel;

namespace Polaris.PUI
{
    public interface IPUI
    {
        string Name { get; }

        UiBoxDesigner GetUIWindow(UiBoxDesignerFamily source);

        void BuildUI(UiBoxDesigner designer);
    }
}
