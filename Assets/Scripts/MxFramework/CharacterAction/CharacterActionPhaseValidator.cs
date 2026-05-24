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
            CombatActionTimeline combatTimeline,
            int? durationFrames = null)
        {
            if (phases == null)
            {
                throw new ArgumentNullException(nameof(phases));
            }

            var issues = new List<CharacterActionValidationIssue>();
            if (authority != CharacterActionTimelineAuthority.CombatAnchored)
            {
                ValidateTimelineShape(phases, durationFrames, issues, suppressedPhaseIndexes: null);
                return issues.ToArray();
            }

            var combatAuthorityIssuePhaseIndexes = new List<int>();
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
                    combatAuthorityIssuePhaseIndexes.Add(i);
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
                    combatAuthorityIssuePhaseIndexes.Add(i);
                }
            }

            ValidateTimelineShape(phases, durationFrames, issues, combatAuthorityIssuePhaseIndexes);
            return issues.ToArray();
        }

        private static void ValidateTimelineShape(
            IReadOnlyList<CharacterActionPhase> phases,
            int? durationFrames,
            List<CharacterActionValidationIssue> issues,
            IReadOnlyList<int> suppressedPhaseIndexes)
        {
            if (phases.Count == 0)
            {
                if (durationFrames.HasValue && durationFrames.Value > 0)
                {
                    issues.Add(new CharacterActionValidationIssue(
                        CharacterActionDiagnosticCodes.PhaseGap,
                        -1,
                        CharacterActionPhaseKind.None,
                        "Character action phases must cover the full action duration."));
                }

                return;
            }

            var ordered = new List<IndexedPhase>(phases.Count);
            for (int i = 0; i < phases.Count; i++)
            {
                CharacterActionPhase phase = phases[i];
                ordered.Add(new IndexedPhase(i, phase));
                if (durationFrames.HasValue && phase.EndFrame >= durationFrames.Value)
                {
                    issues.Add(new CharacterActionValidationIssue(
                        CharacterActionDiagnosticCodes.PhaseRangeOutsideDuration,
                        i,
                        phase.Kind,
                        "Character action phase range must be within action duration."));
                }
            }

            ordered.Sort(CompareIndexedPhases);
            int expectedStart = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                IndexedPhase current = ordered[i];
                if (current.Phase.StartFrame < expectedStart)
                {
                    AddShapeIssue(
                        issues,
                        suppressedPhaseIndexes,
                        CharacterActionDiagnosticCodes.PhaseOverlap,
                        current.Index,
                        current.Phase.Kind,
                        "Character action phases must not overlap.");
                }
                else if (current.Phase.StartFrame > expectedStart)
                {
                    AddShapeIssue(
                        issues,
                        suppressedPhaseIndexes,
                        CharacterActionDiagnosticCodes.PhaseGap,
                        current.Index,
                        current.Phase.Kind,
                        "Character action phases must not leave frame gaps.");
                }

                if (current.Phase.EndFrame + 1 > expectedStart)
                    expectedStart = current.Phase.EndFrame + 1;
            }

            if (durationFrames.HasValue && expectedStart < durationFrames.Value)
            {
                IndexedPhase last = ordered[ordered.Count - 1];
                AddShapeIssue(
                    issues,
                    suppressedPhaseIndexes,
                    CharacterActionDiagnosticCodes.PhaseGap,
                    last.Index,
                    last.Phase.Kind,
                    "Character action phases must cover the full action duration.");
            }
        }

        private static void AddShapeIssue(
            List<CharacterActionValidationIssue> issues,
            IReadOnlyList<int> suppressedPhaseIndexes,
            string code,
            int phaseIndex,
            CharacterActionPhaseKind phaseKind,
            string message)
        {
            if (suppressedPhaseIndexes != null)
            {
                for (int i = 0; i < suppressedPhaseIndexes.Count; i++)
                {
                    if (suppressedPhaseIndexes[i] == phaseIndex)
                        return;
                }
            }

            issues.Add(new CharacterActionValidationIssue(code, phaseIndex, phaseKind, message));
        }

        private static int CompareIndexedPhases(IndexedPhase left, IndexedPhase right)
        {
            int compare = left.Phase.StartFrame.CompareTo(right.Phase.StartFrame);
            if (compare != 0)
                return compare;
            compare = left.Phase.EndFrame.CompareTo(right.Phase.EndFrame);
            if (compare != 0)
                return compare;
            return left.Index.CompareTo(right.Index);
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

        private readonly struct IndexedPhase
        {
            public IndexedPhase(int index, CharacterActionPhase phase)
            {
                Index = index;
                Phase = phase;
            }

            public int Index { get; }
            public CharacterActionPhase Phase { get; }
        }
    }
}
