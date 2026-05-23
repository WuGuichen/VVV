using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;

namespace MxFramework.CharacterAction
{
    public readonly struct CharacterActionValidationIssue : IEquatable<CharacterActionValidationIssue>
    {
        public CharacterActionValidationIssue(string code, int phaseIndex, CharacterActionPhaseKind phaseKind, string message)
        {
            Code = code ?? string.Empty;
            PhaseIndex = phaseIndex;
            PhaseKind = phaseKind;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public int PhaseIndex { get; }

        public CharacterActionPhaseKind PhaseKind { get; }

        public string Message { get; }

        public bool Equals(CharacterActionValidationIssue other)
        {
            return string.Equals(Code, other.Code, StringComparison.Ordinal)
                && PhaseIndex == other.PhaseIndex
                && PhaseKind == other.PhaseKind
                && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionValidationIssue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Code != null ? Code.GetHashCode() : 0;
                hash = (hash * 397) ^ PhaseIndex;
                hash = (hash * 397) ^ (int)PhaseKind;
                hash = (hash * 397) ^ (Message != null ? Message.GetHashCode() : 0);
                return hash;
            }
        }
    }

    public static class CharacterActionPhaseValidator
    {
        public static CharacterActionValidationIssue[] Validate(
            CharacterActionTimelineAuthority authority,
            IReadOnlyList<CharacterActionPhase> phases,
            CombatActionTimeline combatTimeline)
        {
            if (phases == null)
            {
                throw new ArgumentNullException(nameof(phases));
            }

            var issues = new List<CharacterActionValidationIssue>();
            if (authority != CharacterActionTimelineAuthority.CombatAnchored)
            {
                return issues.ToArray();
            }

            for (int i = 0; i < phases.Count; i++)
            {
                CharacterActionPhase phase = phases[i];
                if (!RequiresCombatAnchor(phase.Kind))
                    continue;

                if (combatTimeline == null || phase.CombatPhaseAnchor == CombatActionPhase.None)
                {
                    issues.Add(new CharacterActionValidationIssue(
                        CharacterActionDiagnosticCodes.PhaseCombatAnchorMissing,
                        i,
                        phase.Kind,
                        "CombatAnchored phases must declare a Startup, Active, or Recovery combat anchor."));
                    continue;
                }

                if (!CharacterActionPhase.TryGetCombatRange(combatTimeline, phase.CombatPhaseAnchor, out CombatFrameRange combatRange)
                    || !MatchesCombatRange(phase, combatRange))
                {
                    issues.Add(new CharacterActionValidationIssue(
                        CharacterActionDiagnosticCodes.PhaseCombatRangeMismatch,
                        i,
                        phase.Kind,
                        "CombatAnchored phase range must match its anchored CombatActionTimeline range."));
                }
            }

            return issues.ToArray();
        }

        private static bool RequiresCombatAnchor(CharacterActionPhaseKind kind)
        {
            return kind != CharacterActionPhaseKind.None;
        }

        private static bool MatchesCombatRange(CharacterActionPhase phase, CombatFrameRange combatRange)
        {
            if (phase.Kind == CharacterActionPhaseKind.Startup
                || phase.Kind == CharacterActionPhaseKind.Active
                || phase.Kind == CharacterActionPhaseKind.Recovery)
            {
                return phase.StartFrame == combatRange.StartFrame
                    && phase.EndFrame == combatRange.EndFrame
                    && CorePhaseMatchesAnchor(phase.Kind, phase.CombatPhaseAnchor);
            }

            return !phase.RequiresCombatPhaseMatch
                || (phase.StartFrame >= combatRange.StartFrame && phase.EndFrame <= combatRange.EndFrame);
        }

        private static bool CorePhaseMatchesAnchor(CharacterActionPhaseKind kind, CombatActionPhase anchor)
        {
            return (kind == CharacterActionPhaseKind.Startup && anchor == CombatActionPhase.Startup)
                || (kind == CharacterActionPhaseKind.Active && anchor == CombatActionPhase.Active)
                || (kind == CharacterActionPhaseKind.Recovery && anchor == CombatActionPhase.Recovery);
        }
    }
}
