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
            string traceId = "",
            long sequence = 0L)
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
            TraceId = traceId ?? string.Empty;
            Sequence = sequence;
        }

        public RuntimeFrame Frame { get; }
        public int SourceId { get; }
        public int CommandId { get; }
        public int TargetId { get; }
        public int Payload0 { get; }
        public int Payload1 { get; }
        public int Payload2 { get; }
        public string TraceId { get; }
        public long Sequence { get; }

        public RuntimeCommand WithSequence(long sequence)
        {
            return new RuntimeCommand(Frame, SourceId, CommandId, TargetId, Payload0, Payload1, Payload2, TraceId, sequence);
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
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal)
                && Sequence == other.Sequence;
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
                hash = (hash * 397) ^ (TraceId == null ? 0 : StringComparer.Ordinal.GetHashCode(TraceId));
                hash = (hash * 397) ^ Sequence.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return "Frame=" + Frame
                + " SourceId=" + SourceId
                + " CommandId=" + CommandId
                + " TargetId=" + TargetId
                + " Sequence=" + Sequence;
        }
    }

    public enum RuntimeCommandErrorCode
    {
        None = 0,
        LateCommand = 1,
        InvalidCommandId = 2,
        UnregisteredCommandId = 3,
        InvalidPayload = 4
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

    public interface IRuntimeCommandValidator
    {
        RuntimeCommandValidationResult Validate(RuntimeCommand command);
    }
}
