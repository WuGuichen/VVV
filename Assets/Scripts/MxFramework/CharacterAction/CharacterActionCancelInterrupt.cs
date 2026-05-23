using System;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionSourceKind
    {
        None = 0,
        Command = 1,
        GameplayAbility = 2,
        PostureBreak = 10,
        GuardBreak = 11,
        ArmorBreak = 12,
        Hit = 13,
        Death = 14,
        PlayerIntervention = 20,
        Debug = 30,
    }

    public readonly struct CharacterCancelRule : IEquatable<CharacterCancelRule>
    {
        public CharacterCancelRule(
            int startFrame,
            int endFrame,
            int targetActionId = 0,
            CharacterActionSourceKind sourceKind = CharacterActionSourceKind.Command,
            bool allow = true)
        {
            if (startFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Cancel start frame cannot be negative.");
            }

            if (endFrame < startFrame)
            {
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Cancel end frame cannot be before start frame.");
            }

            if (targetActionId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetActionId), "Target action id cannot be negative.");
            }

            StartFrame = startFrame;
            EndFrame = endFrame;
            TargetActionId = targetActionId;
            SourceKind = sourceKind;
            Allow = allow;
        }

        public int StartFrame { get; }

        public int EndFrame { get; }

        public int TargetActionId { get; }

        public CharacterActionSourceKind SourceKind { get; }

        public bool Allow { get; }

        public bool Matches(int localFrame, int targetActionId, CharacterActionSourceKind sourceKind)
        {
            return localFrame >= StartFrame
                && localFrame <= EndFrame
                && (TargetActionId == 0 || TargetActionId == targetActionId)
                && (SourceKind == CharacterActionSourceKind.None || SourceKind == sourceKind);
        }

        public bool Equals(CharacterCancelRule other)
        {
            return StartFrame == other.StartFrame
                && EndFrame == other.EndFrame
                && TargetActionId == other.TargetActionId
                && SourceKind == other.SourceKind
                && Allow == other.Allow;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterCancelRule other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StartFrame;
                hash = (hash * 397) ^ EndFrame;
                hash = (hash * 397) ^ TargetActionId;
                hash = (hash * 397) ^ (int)SourceKind;
                hash = (hash * 397) ^ Allow.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct CharacterInterruptRule : IEquatable<CharacterInterruptRule>
    {
        public CharacterInterruptRule(
            CharacterActionSourceKind sourceKind,
            int minimumPriority = 0,
            int targetActionId = 0,
            bool allow = true)
        {
            if (targetActionId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetActionId), "Target action id cannot be negative.");
            }

            SourceKind = sourceKind;
            MinimumPriority = minimumPriority;
            TargetActionId = targetActionId;
            Allow = allow;
        }

        public CharacterActionSourceKind SourceKind { get; }

        public int MinimumPriority { get; }

        public int TargetActionId { get; }

        public bool Allow { get; }

        public bool Matches(CharacterActionSourceKind sourceKind, int priority, int targetActionId)
        {
            return (SourceKind == CharacterActionSourceKind.None || SourceKind == sourceKind)
                && priority >= MinimumPriority
                && (TargetActionId == 0 || TargetActionId == targetActionId);
        }

        public bool Equals(CharacterInterruptRule other)
        {
            return SourceKind == other.SourceKind
                && MinimumPriority == other.MinimumPriority
                && TargetActionId == other.TargetActionId
                && Allow == other.Allow;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterInterruptRule other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)SourceKind;
                hash = (hash * 397) ^ MinimumPriority;
                hash = (hash * 397) ^ TargetActionId;
                hash = (hash * 397) ^ Allow.GetHashCode();
                return hash;
            }
        }
    }
}
