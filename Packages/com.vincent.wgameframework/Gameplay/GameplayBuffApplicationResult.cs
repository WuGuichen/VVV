using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>Optional opaque numeric correlation carried by a Gameplay Buff request.</summary>
    public readonly struct GameplayBuffRequestCorrelation : IEquatable<GameplayBuffRequestCorrelation>
    {
        /// <remarks>NoAlloc on the valid path.</remarks>
        public GameplayBuffRequestCorrelation(long requestId, int operationIndex, int operationKindId)
        {
            if (requestId <= 0L)
                throw new ArgumentOutOfRangeException(nameof(requestId));
            if (operationIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(operationIndex));
            if (operationKindId <= 0)
                throw new ArgumentOutOfRangeException(nameof(operationKindId));

            RequestId = requestId;
            OperationIndex = operationIndex;
            OperationKindId = operationKindId;
        }

        public long RequestId { get; }
        public int OperationIndex { get; }
        public int OperationKindId { get; }
        public bool IsValid => RequestId > 0L && OperationIndex >= 0 && OperationKindId > 0;

        public bool Equals(GameplayBuffRequestCorrelation other)
        {
            return RequestId == other.RequestId &&
                   OperationIndex == other.OperationIndex &&
                   OperationKindId == other.OperationKindId;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayBuffRequestCorrelation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RequestId.GetHashCode();
                hash = (hash * 397) ^ OperationIndex;
                hash = (hash * 397) ^ OperationKindId;
                return hash;
            }
        }

        public static bool operator ==(GameplayBuffRequestCorrelation left, GameplayBuffRequestCorrelation right) => left.Equals(right);
        public static bool operator !=(GameplayBuffRequestCorrelation left, GameplayBuffRequestCorrelation right) => !left.Equals(right);
    }

    /// <summary>Opaque generation-safe handle for one bounded Gameplay Buff request reservation.</summary>
    public readonly struct GameplayBuffRequestReservationToken : IEquatable<GameplayBuffRequestReservationToken>
    {
        internal GameplayBuffRequestReservationToken(int slotIndex, long generation)
        {
            SlotIndex = slotIndex;
            Generation = generation;
        }

        internal int SlotIndex { get; }
        internal long Generation { get; }
        public bool IsValid => SlotIndex >= 0 && Generation > 0L;

        public bool Equals(GameplayBuffRequestReservationToken other)
        {
            return SlotIndex == other.SlotIndex && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayBuffRequestReservationToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (SlotIndex * 397) ^ Generation.GetHashCode();
            }
        }

        public static bool operator ==(GameplayBuffRequestReservationToken left, GameplayBuffRequestReservationToken right) => left.Equals(right);
        public static bool operator !=(GameplayBuffRequestReservationToken left, GameplayBuffRequestReservationToken right) => !left.Equals(right);
    }

    public enum GameplayBuffApplicationResultStatus
    {
        None = 0,
        Succeeded = 1,
        Rejected = 2
    }

    public enum GameplayBuffApplicationResultReasonCode
    {
        None = 0,
        Applied = 1,
        Refreshed = 2,
        Removed = 3,
        RemoveNoOp = 4,
        MissingComponentWorld = 1001,
        MissingComponentEntity = 1002,
        MissingBuffDefinition = 1003,
        DurationOverflow = 1004
    }

    /// <summary>Flat numeric terminal result for one correlated Gameplay Buff request.</summary>
    public readonly struct GameplayBuffApplicationResult
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayBuffApplicationResult(
            RuntimeFrame frame,
            GameplayBuffApplicationRequestKind kind,
            GameplayEntityId targetEntityId,
            int buffId,
            int sourceId,
            GameplayBuffRequestCorrelation correlation,
            GameplayBuffApplicationResultStatus status,
            GameplayBuffApplicationResultReasonCode reasonCode,
            int oldStackCount = 0,
            int newStackCount = 0,
            long oldEndFrame = 0L,
            long newEndFrame = 0L)
        {
            Frame = frame;
            Kind = kind;
            TargetEntityId = targetEntityId;
            BuffId = buffId;
            SourceId = sourceId;
            Correlation = correlation;
            Status = status;
            ReasonCode = reasonCode;
            OldStackCount = oldStackCount;
            NewStackCount = newStackCount;
            OldEndFrame = oldEndFrame;
            NewEndFrame = newEndFrame;
        }

        public RuntimeFrame Frame { get; }
        public GameplayBuffApplicationRequestKind Kind { get; }
        public GameplayEntityId TargetEntityId { get; }
        public int BuffId { get; }
        public int SourceId { get; }
        public GameplayBuffRequestCorrelation Correlation { get; }
        public GameplayBuffApplicationResultStatus Status { get; }
        public GameplayBuffApplicationResultReasonCode ReasonCode { get; }
        public int OldStackCount { get; }
        public int NewStackCount { get; }
        public long OldEndFrame { get; }
        public long NewEndFrame { get; }
    }

    /// <summary>Optional no-throw/no-fail target-owned terminal result sink.</summary>
    public interface IGameplayBuffApplicationResultSink
    {
        /// <remarks>NoAlloc. Implementations must not throw or retry.</remarks>
        void Publish(in GameplayBuffApplicationResult result);
    }
}
