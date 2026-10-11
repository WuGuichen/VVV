using System;

namespace MxFramework.Runtime
{
    public readonly struct RuntimeCommand : IEquatable<RuntimeCommand>
    {
        public RuntimeCommand(
            RuntimeFrame frame,
            int sourceId,
            int commandId,
            int targetId,
            int payload0 = 0,
            int payload1 = 0,
            int payload2 = 0,
            RuntimeTraceIdentity traceId = default,
            long sequence = 0L,
            RuntimeCommandEnvelope envelope = default)
        {
            if (sequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence), "Runtime command sequence cannot be negative.");
            }

            Frame = frame;
            SourceId = sourceId;
            CommandId = commandId;
            TargetId = targetId;
            Payload0 = payload0;
            Payload1 = payload1;
            Payload2 = payload2;
            TraceId = traceId;
            Sequence = sequence;
            Envelope = envelope;
        }

        public RuntimeFrame Frame { get; }
        public int SourceId { get; }
        public int CommandId { get; }
        public int TargetId { get; }
        public int Payload0 { get; }
        public int Payload1 { get; }
        public int Payload2 { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public long Sequence { get; }
        /// <summary>Gets the optional fixed typed payload copied with this command.</summary>
        public RuntimeCommandEnvelope Envelope { get; }

        public RuntimeCommand WithSequence(long sequence)
        {
            return new RuntimeCommand(
                Frame,
                SourceId,
                CommandId,
                TargetId,
                Payload0,
                Payload1,
                Payload2,
                TraceId,
                sequence,
                Envelope);
        }

        public bool Equals(RuntimeCommand other)
        {
            return Frame == other.Frame
                && SourceId == other.SourceId
                && CommandId == other.CommandId
                && TargetId == other.TargetId
                && Payload0 == other.Payload0
                && Payload1 == other.Payload1
                && Payload2 == other.Payload2
                && TraceId == other.TraceId
                && Sequence == other.Sequence
                && Envelope == other.Envelope;
        }

        public override bool Equals(object obj)
        {
            return obj is RuntimeCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ CommandId;
                hash = (hash * 397) ^ TargetId;
                hash = (hash * 397) ^ Payload0;
                hash = (hash * 397) ^ Payload1;
                hash = (hash * 397) ^ Payload2;
                hash = (hash * 397) ^ TraceId.GetHashCode();
                hash = (hash * 397) ^ Sequence.GetHashCode();
                hash = (hash * 397) ^ Envelope.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return "Frame=" + Frame
                + " SourceId=" + SourceId
                + " CommandId=" + CommandId
                + " TargetId=" + TargetId
                + " Sequence=" + Sequence
                + " EnvelopeTypeId=" + Envelope.TypeId;
        }
    }

    public enum RuntimeCommandErrorCode
    {
        None = 0,
        LateCommand = 1,
        InvalidCommandId = 2,
        UnregisteredCommandId = 3,
        InvalidPayload = 4,
        UnregisteredSourceId = 5,
        UnauthorizedSource = 6,
        ProvenanceMismatch = 7,
        UnauthorizedCommand = 8,
        PendingCapacityExceeded = 9,
        SequenceExhausted = 10,
        AtomicBatchRequiresBoundedBuffer = 11
    }

    public readonly struct RuntimeCommandError
    {
        public RuntimeCommandError(
            RuntimeCommandErrorCode code,
            RuntimeCommand command,
            RuntimeFrame currentFrame,
            string message)
        {
            Code = code;
            Command = command;
            CurrentFrame = currentFrame;
            Message = message ?? string.Empty;
        }

        public RuntimeCommandErrorCode Code { get; }
        public RuntimeCommand Command { get; }
        public RuntimeFrame CurrentFrame { get; }
        public RuntimeFrame CommandFrame => Command.Frame;
        public string Message { get; }
        public bool IsNone => Code == RuntimeCommandErrorCode.None;

        public static RuntimeCommandError None => new RuntimeCommandError(RuntimeCommandErrorCode.None, default, RuntimeFrame.Zero, string.Empty);

        public override string ToString()
        {
            return Code + " CommandFrame=" + CommandFrame + " CurrentFrame=" + CurrentFrame + " " + Message;
        }
    }

    public readonly struct RuntimeCommandValidationResult
    {
        private RuntimeCommandValidationResult(bool success, RuntimeCommand command, RuntimeCommandError error)
        {
            Success = success;
            Command = command;
            Error = error;
        }

        public bool Success { get; }
        public RuntimeCommand Command { get; }
        public RuntimeCommandError Error { get; }

        public static RuntimeCommandValidationResult Accepted(RuntimeCommand command)
        {
            return new RuntimeCommandValidationResult(true, command, RuntimeCommandError.None);
        }

        public static RuntimeCommandValidationResult Failed(RuntimeCommandError error)
        {
            return new RuntimeCommandValidationResult(false, error.Command, error);
        }
    }

    /// <summary>Atomic admission result for a caller-owned command span.</summary>
    public readonly struct RuntimeCommandBatchSubmissionResult
    {
        internal RuntimeCommandBatchSubmissionResult(
            bool success,
            int submittedCount,
            int failedCommandIndex,
            RuntimeCommandError error)
        {
            Success = success;
            SubmittedCount = submittedCount;
            FailedCommandIndex = failedCommandIndex;
            Error = error;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool Success { get; }
        /// <summary>Gets the full submitted count on success, or zero on failure.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int SubmittedCount { get; }
        /// <summary>Gets the first failing caller index, or -1 when no input command failed.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int FailedCommandIndex { get; }
        /// <remarks>NoAlloc.</remarks>
        public RuntimeCommandError Error { get; }

        internal static RuntimeCommandBatchSubmissionResult Accepted(int submittedCount) =>
            new RuntimeCommandBatchSubmissionResult(
                true,
                submittedCount,
                failedCommandIndex: -1,
                RuntimeCommandError.None);

        internal static RuntimeCommandBatchSubmissionResult Failed(
            int failedCommandIndex,
            RuntimeCommandError error) =>
            new RuntimeCommandBatchSubmissionResult(
                false,
                submittedCount: 0,
                failedCommandIndex,
                error);
    }

    /// <summary>
    /// Pure command validation contract. Implementations must not mutate externally observable state and
    /// must not depend on the command already being committed; atomic batch preflight can validate commands
    /// that are subsequently not admitted.
    /// </summary>
    public interface IRuntimeCommandValidator
    {
        RuntimeCommandValidationResult Validate(RuntimeCommand command);
    }
}
