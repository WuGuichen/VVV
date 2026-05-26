namespace MxFramework.UI
{
    public interface IMxUiViewRegistry
    {
        // Returns true when the id is registered. A null view means creation failed.
        bool TryCreate(MxUiViewId id, out IMxUiView view);
    }
}
