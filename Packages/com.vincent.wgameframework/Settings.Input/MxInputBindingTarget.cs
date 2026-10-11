namespace MxFramework.Settings.Input
{
    public readonly struct MxInputBindingTarget
    {
        public MxInputBindingTarget(string actionId, int bindingIndex)
        {
            ActionId = actionId ?? string.Empty;
            BindingIndex = bindingIndex;
        }

        public string ActionId { get; }
        public int BindingIndex { get; }
    }

    public readonly struct MxInputBindingInfo
    {
        public MxInputBindingInfo(MxInputBindingTarget target, string displayString)
        {
            Target = target;
            DisplayString = displayString ?? string.Empty;
        }

        public MxInputBindingTarget Target { get; }
        public string DisplayString { get; }
    }
}
