namespace MxFramework.UI
{
    public sealed class MxUiCommandDescriptor
    {
        public MxUiCommandDescriptor()
        {
            CommandId = string.Empty;
            PayloadType = string.Empty;
            RiskLevel = string.Empty;
            Owner = string.Empty;
        }

        public string CommandId { get; set; }
        public string PayloadType { get; set; }
        public string RiskLevel { get; set; }
        public bool RequiresConfirmation { get; set; }
        public bool IsReadOnly { get; set; }
        public string Owner { get; set; }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(CommandId);
        }
    }
}
