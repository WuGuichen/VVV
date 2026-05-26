namespace MxFramework.UI
{
    public interface IMxUiViewRegistry
    {
        bool TryCreate(MxUiViewId id, out IMxUiView view);
    }
}
