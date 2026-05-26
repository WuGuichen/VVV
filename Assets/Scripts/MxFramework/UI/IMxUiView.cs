namespace MxFramework.UI
{
    public interface IMxUiView
    {
        MxUiViewId Id { get; }
        MxUiLifecycle Lifecycle { get; }
        void Show();
        void Hide();
        void Dispose();
    }

    public interface IMxUiView<in TViewModel> : IMxUiView
    {
        void Bind(TViewModel model);
    }
}
