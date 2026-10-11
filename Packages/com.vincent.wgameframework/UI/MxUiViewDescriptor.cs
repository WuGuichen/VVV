namespace MxFramework.UI
{
    public sealed class MxUiViewDescriptor
    {
        public MxUiViewDescriptor(
            MxUiViewId id,
            string packageKey,
            string componentName,
            MxUiLayer layer)
        {
            Id = id;
            PackageKey = packageKey ?? string.Empty;
            ComponentName = componentName ?? string.Empty;
            Layer = layer;
            InputScope = string.Empty;
        }

        public MxUiViewId Id { get; }
        public string PackageKey { get; }
        public string ComponentName { get; }
        public MxUiLayer Layer { get; }
        public bool Modal { get; set; }
        public bool KeepAlive { get; set; }
        public bool CloseOnSceneChange { get; set; }
        public string InputScope { get; set; }
    }
}
