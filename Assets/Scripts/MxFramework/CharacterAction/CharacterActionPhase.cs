using System;
using MxFramework.Combat.Animation;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionPhaseKind
    {
        None = 0,
        Startup = 1,
        Active = 2,
        Recovery = 3,
        Loop = 4,
        Airborne = 5,
        Landing = 6,
        Channel = 7,
        Hold = 8,
        Exit = 9,
        Custom = 100,
    }

    public enum CharacterActionTimelineAuthority
    {
        CharacterAuthored = 0,
        CombatAnchored = 1,
    }

    public readonly struct CharacterActionPhase : IEquatable<CharacterActionPhase>
    {
        public CharacterActionPhase(
            CharacterActionPhaseKind kind,
            int startFrame,
            int endFrame,
            CombatActionPhase combatPhaseAnchor = CombatActionPhase.None,
            bool requiresCombatPhaseMatch = true)
        {
            if (startFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Phase start frame cannot be negative.");
            }

            if (endFrame < startFrame)
            {
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Phase end frame cannot be before start frame.");
            }

            Kind = kind;
            StartFrame = startFrame;
            EndFrame = endFrame;
            CombatPhaseAnchor = combatPhaseAnchor;
            RequiresCombatPhaseMatch = requiresCombatPhaseMatch;
        }

        public CharacterActionPhaseKind Kind { get; }

        public int StartFrame { get; }

        public int EndFrame { get; }

        public CombatActionPhase CombatPhaseAnchor { get; }

        public bool RequiresCombatPhaseMatch { get; }

        public bool Contains(int localFrame)
        {
            return localFrame >= StartFrame && localFrame <= EndFrame;
        }

        public bool Equals(CharacterActionPhase other)
        {
            return Kind == other.Kind
                && StartFrame == other.StartFrame
                && EndFrame == other.EndFrame
                && CombatPhaseAnchor == other.CombatPhaseAnchor
                && RequiresCombatPhaseMatch == other.RequiresCombatPhaseMatch;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionPhase other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ StartFrame;
                hash = (hash * 397) ^ EndFrame;
                hash = (hash * 397) ^ (int)CombatPhaseAnchor;
                hash = (hash * 397) ^ RequiresCombatPhaseMatch.GetHashCode();
                return hash;
            }
        }

        internal static bool TryGetCombatRange(
            CombatActionTimeline timeline,
            CombatActionPhase anchor,
            out CombatFrameRange range)
        {
            if (timeline == null)
            {
                range = CombatFrameRange.Empty;
                return false;
            }

            switch (anchor)
            {
                case CombatActionPhase.Startup:
                    range = timeline.Startup;
                    return true;
                case CombatActionPhase.Active:
                    range = timeline.Active;
                    return true;
                case CombatActionPhase.Recovery:
                    range = timeline.Recovery;
                    return true;
                default:
                    range = CombatFrameRange.Empty;
                    return false;
            }
        }
    }
}
