using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    /// <summary>Stable RuntimeCommand ids owned by Gameplay Triggers.</summary>
    public static class GameplayTriggerRuntimeCommandIds
    {
        public const int SetEnabled = 1002001;
        public const int Reset = 1002002;
        public const int SubmitSignalBatch = 1002003;
        public const int SubmitOccupancyBatch = 1002004;
        public const int SubmitScalarBatch = 1002005;
    }

    /// <summary>Creates lifecycle commands that fit the current replayable RuntimeCommand payload.</summary>
    public static class GameplayTriggerRuntimeCommandFactory
    {
        /// <remarks>NoAlloc. The optional trace string is caller-owned.</remarks>
        public static RuntimeCommand SetEnabled(
            RuntimeFrame frame,
            GameplayTriggerInstanceId instanceId,
            bool enabled,
            int sourceId = 0,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayTriggerRuntimeCommandIds.SetEnabled,
                targetId: instanceId.Index,
                payload0: instanceId.Generation,
                payload1: enabled ? 1 : 0,
                payload2: 0,
                traceId: traceId);
        }

        /// <remarks>NoAlloc. The optional trace string is caller-owned.</remarks>
        public static RuntimeCommand Reset(
            RuntimeFrame frame,
            GameplayTriggerInstanceId instanceId,
            int sourceId = 0,
            RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayTriggerRuntimeCommandIds.Reset,
                targetId: instanceId.Index,
                payload0: instanceId.Generation,
                payload1: 0,
                payload2: 0,
                traceId: traceId);
        }
    }

    public enum GameplayTriggerLifecycleCommandKind
    {
        None = 0,
        SetEnabled = 1,
        Reset = 2
    }

    public enum GameplayTriggerRuntimeCommandKind
    {
        None = 0,
        SetEnabled = 1,
        Reset = 2,
        SubmitSignalBatch = 3,
        SubmitOccupancyBatch = 4,
        SubmitScalarBatch = 5
    }

    /// <summary>Last lifecycle command outcome exposed without allocating a diagnostics snapshot.</summary>
    public readonly struct GameplayTriggerCommandExecutionResult
    {
        public GameplayTriggerCommandExecutionResult(
            RuntimeFrame frame,
            long commandSequence,
            GameplayTriggerLifecycleCommandKind commandKind,
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerResult result,
            GameplayTriggerSourceBindingId sourceBindingId = default,
            long batchId = 0L,
            RuntimeCommandProvenanceKind provenance = RuntimeCommandProvenanceKind.Unspecified)
            : this(
                frame,
                commandSequence,
                (GameplayTriggerRuntimeCommandKind)commandKind,
                instanceId,
                result,
                sourceBindingId,
                batchId,
                provenance)
        {
        }

        public GameplayTriggerCommandExecutionResult(
            RuntimeFrame frame,
            long commandSequence,
            GameplayTriggerRuntimeCommandKind runtimeCommandKind,
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerResult result,
            GameplayTriggerSourceBindingId sourceBindingId = default,
            long batchId = 0L,
            RuntimeCommandProvenanceKind provenance = RuntimeCommandProvenanceKind.Unspecified)
        {
            Frame = frame;
            CommandSequence = commandSequence;
            RuntimeCommandKind = runtimeCommandKind;
            InstanceId = instanceId;
            Result = result;
            SourceBindingId = sourceBindingId;
            BatchId = batchId;
            Provenance = provenance;
        }

        public RuntimeFrame Frame { get; }
        public long CommandSequence { get; }
        public GameplayTriggerRuntimeCommandKind RuntimeCommandKind { get; }
        public GameplayTriggerLifecycleCommandKind CommandKind =>
            RuntimeCommandKind == GameplayTriggerRuntimeCommandKind.SetEnabled
                ? GameplayTriggerLifecycleCommandKind.SetEnabled
                : RuntimeCommandKind == GameplayTriggerRuntimeCommandKind.Reset
                    ? GameplayTriggerLifecycleCommandKind.Reset
                    : GameplayTriggerLifecycleCommandKind.None;
        public GameplayTriggerInstanceId InstanceId { get; }
        public GameplayTriggerResult Result { get; }
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        public long BatchId { get; }
        public RuntimeCommandProvenanceKind Provenance { get; }
    }
}
