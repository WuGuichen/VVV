namespace MxFramework.UI
{
    public interface IMxUiNavigator
    {
        MxUiOpenResult Open<TArgs>(MxUiViewId id, TArgs args);
        MxUiOpenOperation OpenAsync<TArgs>(MxUiViewId id, TArgs args);
        bool Close(MxUiViewId id);
        bool IsOpen(MxUiViewId id);
    }
}
