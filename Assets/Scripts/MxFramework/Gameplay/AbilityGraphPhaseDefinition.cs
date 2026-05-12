using System;

namespace MxFramework.Gameplay
{
    public readonly struct AbilityGraphPhaseId : IEquatable<AbilityGraphPhaseId>, IComparable<AbilityGraphPhaseId>
    {
        public static readonly AbilityGraphPhaseId Empty = new AbilityGraphPhaseId(string.Empty);

        private readonly string _value;

        public AbilityGraphPhaseId(string value)
        {
            _value = value ?? string.Empty;
        }

        public string Value => _value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public int CompareTo(AbilityGraphPhaseId other)
        {
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public bool Equals(AbilityGraphPhaseId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityGraphPhaseId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261;
                string value = Value;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash ^ value[i]) * 16777619;

                return hash;
            }
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(AbilityGraphPhaseId left, AbilityGraphPhaseId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AbilityGraphPhaseId left, AbilityGraphPhaseId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct AbilityGraphPhaseDefinition
    {
        public AbilityGraphPhaseDefinition(string phaseId, int durationFrames, string nextPhaseId = null)
            : this(new AbilityGraphPhaseId(phaseId), durationFrames, new AbilityGraphPhaseId(nextPhaseId), nextPhaseId != null)
        {
        }

        public AbilityGraphPhaseDefinition(
            AbilityGraphPhaseId phaseId,
            int durationFrames,
            AbilityGraphPhaseId nextPhaseId,
            bool hasNextPhase)
        {
            PhaseId = phaseId;
            DurationFrames = durationFrames;
            HasNextPhase = hasNextPhase;
            NextPhaseId = hasNextPhase ? nextPhaseId : AbilityGraphPhaseId.Empty;
        }

        public AbilityGraphPhaseId PhaseId { get; }
        public int DurationFrames { get; }
        public bool HasNextPhase { get; }
        public AbilityGraphPhaseId NextPhaseId { get; }
        public bool IsTerminal => !HasNextPhase;
    }

    public readonly struct AbilityGraphPhaseTransition
    {
        public AbilityGraphPhaseTransition(
            AbilityGraphPhaseId fromPhaseId,
            AbilityGraphPhaseId toPhaseId,
            long totalElapsedFrames,
            int consumedFrames)
        {
            FromPhaseId = fromPhaseId;
            ToPhaseId = toPhaseId;
            TotalElapsedFrames = totalElapsedFrames;
            ConsumedFrames = consumedFrames;
        }

        public AbilityGraphPhaseId FromPhaseId { get; }
        public AbilityGraphPhaseId ToPhaseId { get; }
        public long TotalElapsedFrames { get; }
        public int ConsumedFrames { get; }
    }
}
