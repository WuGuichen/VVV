namespace MxFramework.Demo.FairyGui
{
    public sealed class UiFoundationDemoViewModel
    {
        public string StatusText { get; set; } = "等待操作";
        public string PrimaryLabel { get; set; } = "主要操作";
        public string SecondaryLabel { get; set; } = "次要操作";
        public string CloseLabel { get; set; } = "关闭";
        public bool CanInteract { get; set; } = true;
    }
}
