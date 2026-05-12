using System;

namespace MxFramework.Combat.Animation
{
    public readonly struct CombatActionWindow : IEquatable<CombatActionWindow>
    {
        public CombatActionWindow(CombatActionWindowKind kind, CombatFrameRange range, int targetActionId = 0, int priority = 0)
        {
            if (targetActionId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetActionId), "Target action id cannot be negative.");
            }

            Kind = kind;
            Range = range;
            TargetActionId = targetActionId;
            Priority = priority;
        }

        public CombatActionWindowKind Kind { get; }

        public CombatFrameRange Range { get; }

        public int TargetActionId { get; }

        public int Priority { get; }

        public bool Contains(int localFrame)
        {
            return Range.Contains(localFrame);
        }

        public bool Equals(CombatActionWindow other)
        {
            return Kind == other.Kind
                && Range.Equals(other.Range)
                && TargetActionId == other.TargetActionId
                && Priority == other.Priority;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatActionWindow other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ Range.GetHashCode();
                hash = (hash * 397) ^ TargetActionId;
                hash = (hash * 397) ^ Priority;
                return hash;
            }
        }
    }
}
