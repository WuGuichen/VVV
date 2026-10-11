using MxFramework.Runtime;
using System;

namespace MxFramework.Gameplay
{
    public enum GameplayBuffApplicationRequestKind
    {
        Apply = 0,
        Remove = 1
    }

    public readonly struct GameplayBuffApplicationRequest : IEquatable<GameplayBuffApplicationRequest>
    {
        public GameplayBuffApplicationRequest(
            GameplayBuffApplicationRequestKind kind,
            GameplayEntityId targetEntityId,
            int buffId,
            int sourceId,
            RuntimeTraceIdentity traceId,
            int commandId)
            : this(kind, targetEntityId, buffId, sourceId, traceId, commandId, default)
        {
        }

        /// <remarks>NoAlloc on the valid path.</remarks>
        public GameplayBuffApplicationRequest(
            GameplayBuffApplicationRequestKind kind,
            GameplayEntityId targetEntityId,
            int buffId,
            int sourceId,
            RuntimeTraceIdentity traceId,
            int commandId,
            GameplayBuffRequestCorrelation correlation)
        {
            if (kind != GameplayBuffApplicationRequestKind.Apply &&
                kind != GameplayBuffApplicationRequestKind.Remove)
                throw new ArgumentOutOfRangeException(nameof(kind), "Gameplay buff request kind is not supported.");
            if (!targetEntityId.IsValid)
                throw new ArgumentException("Gameplay buff request target entity id must be valid.", nameof(targetEntityId));
            if (buffId <= 0)
                throw new ArgumentOutOfRangeException(nameof(buffId), "Gameplay buff request buff id must be greater than zero.");
            if (sourceId < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceId), "Gameplay buff request source id cannot be negative.");

            Kind = kind;
            TargetEntityId = targetEntityId;
            BuffId = buffId;
            SourceId = sourceId;
            TraceId = traceId;
            CommandId = commandId;
            Correlation = correlation;
        }

        public GameplayBuffApplicationRequestKind Kind { get; }
        public GameplayEntityId TargetEntityId { get; }
        public int BuffId { get; }
        public int SourceId { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public int CommandId { get; }
        public GameplayBuffRequestCorrelation Correlation { get; }

        public bool Equals(GameplayBuffApplicationRequest other)
        {
            return Kind == other.Kind
                && TargetEntityId.Equals(other.TargetEntityId)
                && BuffId == other.BuffId
                && SourceId == other.SourceId
                && TraceId.Equals(other.TraceId)
                && CommandId == other.CommandId
                && Correlation.Equals(other.Correlation);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayBuffApplicationRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ TargetEntityId.GetHashCode();
                hash = (hash * 397) ^ BuffId;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TraceId);
                hash = (hash * 397) ^ CommandId;
                if (Correlation.IsValid)
                    hash = (hash * 397) ^ Correlation.GetHashCode();
                return hash;
            }
        }
    }
}
