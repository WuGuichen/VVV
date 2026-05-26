namespace MxFramework.UI
{
    public readonly struct MxUiCommand
    {
        public MxUiCommand(MxUiViewId sourceViewId, string commandId, object payload)
        {
            SourceViewId = sourceViewId;
            CommandId = commandId ?? string.Empty;
            Payload = payload;
        }

        public MxUiViewId SourceViewId { get; }
        public string CommandId { get; }
        public object Payload { get; }
    }

    public interface IMxUiCommandSink
    {
        void Enqueue(MxUiCommand command);
    }
}
