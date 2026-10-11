using MxFramework.Runtime;

namespace MxFramework.Story.Runtime
{
    /// <summary>Terminal numeric status for an enveloped Story Runtime command.</summary>
    public enum StoryRuntimeCommandResultStatus
    {
        None = 0,
        Succeeded = 1,
        Rejected = 2
    }

    /// <summary>Flat numeric terminal result emitted by Story Runtime command ownership.</summary>
    public readonly struct StoryRuntimeCommandResult
    {
        /// <remarks>NoAlloc.</remarks>
        public StoryRuntimeCommandResult(
            RuntimeFrame frame,
            int sourceId,
            int commandId,
            int targetId,
            int commandPayload0,
            int commandPayload1,
            int commandPayload2,
            long commandSequence,
            bool traceIdIsEmpty,
            RuntimeCommandEnvelope envelope,
            StoryRuntimeCommandResultStatus status,
            StoryDirectorResultCode directorResultCode,
            long resultPayload0,
            long resultPayload1,
            long resultPayload2)
        {
            Frame = frame;
            SourceId = sourceId;
            CommandId = commandId;
            TargetId = targetId;
            CommandPayload0 = commandPayload0;
            CommandPayload1 = commandPayload1;
            CommandPayload2 = commandPayload2;
            CommandSequence = commandSequence;
            TraceIdIsEmpty = traceIdIsEmpty;
            Envelope = envelope;
            Status = status;
            DirectorResultCode = directorResultCode;
            ResultPayload0 = resultPayload0;
            ResultPayload1 = resultPayload1;
            ResultPayload2 = resultPayload2;
        }

        public RuntimeFrame Frame { get; }
        public int SourceId { get; }
        public int CommandId { get; }
        public int TargetId { get; }
        public int CommandPayload0 { get; }
        public int CommandPayload1 { get; }
        public int CommandPayload2 { get; }
        public long CommandSequence { get; }
        public bool TraceIdIsEmpty { get; }
        public RuntimeCommandEnvelope Envelope { get; }
        public StoryRuntimeCommandResultStatus Status { get; }
        public StoryDirectorResultCode DirectorResultCode { get; }
        public long ResultPayload0 { get; }
        public long ResultPayload1 { get; }
        public long ResultPayload2 { get; }
    }

    /// <summary>Receives one target-owned terminal result for an enveloped Story command.</summary>
    public interface IStoryRuntimeCommandResultSink
    {
        /// <remarks>NoAlloc, no-throw and no-retry.</remarks>
        void Publish(in StoryRuntimeCommandResult result);
    }
}


