using System;

namespace MxFramework.UI
{
    public readonly struct MxUiCommand
    {
        public MxUiCommand(MxUiViewId sourceViewId, string commandId, object payload)
            : this(sourceViewId, commandId, payload, invocationId: 0L)
        {
        }

        public MxUiCommand(
            MxUiViewId sourceViewId,
            string commandId,
            object payload,
            long invocationId)
        {
            if (invocationId < 0L)
                throw new ArgumentOutOfRangeException(nameof(invocationId));

            SourceViewId = sourceViewId;
            CommandId = commandId ?? string.Empty;
            Payload = payload;
            InvocationId = invocationId;
        }

        public MxUiViewId SourceViewId { get; }
        public string CommandId { get; }
        public object Payload { get; }
        public long InvocationId { get; }

        /// <summary>
        /// Returns a copy associated with a routed UI invocation.
        /// NoAlloc.
        /// </summary>
        public MxUiCommand WithInvocationId(long invocationId)
        {
            if (invocationId <= 0L)
                throw new ArgumentOutOfRangeException(nameof(invocationId));

            return new MxUiCommand(SourceViewId, CommandId, Payload, invocationId);
        }
    }

    public interface IMxUiCommandSink
    {
        void Enqueue(MxUiCommand command);
    }
}
