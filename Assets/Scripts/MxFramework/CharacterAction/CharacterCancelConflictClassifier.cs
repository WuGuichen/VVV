using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;

namespace MxFramework.CharacterAction
{
    public enum CharacterCancelRejectionAuthority
    {
        None = 0,
        Character = 1,
        Combat = 2,
    }

    public readonly struct CharacterCancelConflictResult : IEquatable<CharacterCancelConflictResult>
    {
        private CharacterCancelConflictResult(bool allowed, CharacterCancelRejectionAuthority rejectedBy, string code)
        {
            Allowed = allowed;
            RejectedBy = rejectedBy;
            Code = code ?? string.Empty;
        }

        public bool Allowed { get; }

        public CharacterCancelRejectionAuthority RejectedBy { get; }

        public string Code { get; }

        public static CharacterCancelConflictResult Accepted()
        {
            return new CharacterCancelConflictResult(true, CharacterCancelRejectionAuthority.None, string.Empty);
        }

        public static CharacterCancelConflictResult Rejected(CharacterCancelRejectionAuthority rejectedBy, string code)
        {
            return new CharacterCancelConflictResult(false, rejectedBy, code);
        }

        public bool Equals(CharacterCancelConflictResult other)
        {
            return Allowed == other.Allowed
                && RejectedBy == other.RejectedBy
                && string.Equals(Code, other.Code, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterCancelConflictResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Allowed.GetHashCode();
                hash = (hash * 397) ^ (int)RejectedBy;
                hash = (hash * 397) ^ (Code != null ? Code.GetHashCode() : 0);
                return hash;
            }
        }
    }

    public static class CharacterCancelConflictClassifier
    {
        public static CharacterCancelConflictResult Classify(
            CharacterActionTimelineAuthority authority,
            IReadOnlyList<CharacterCancelRule> characterRules,
            CombatActionTimeline combatTimeline,
            int localFrame,
            int targetActionId,
            CharacterActionSourceKind sourceKind)
        {
            if (localFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            }

            if (targetActionId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetActionId), "Target action id cannot be negative.");
            }

            bool characterAllowed = IsCharacterCancelAllowed(characterRules, localFrame, targetActionId, sourceKind);
            if (!characterAllowed)
            {
                return CharacterCancelConflictResult.Rejected(
                    CharacterCancelRejectionAuthority.Character,
                    CharacterActionDiagnosticCodes.CharacterCancelRejected);
            }

            bool combatAllowed = authority != CharacterActionTimelineAuthority.CombatAnchored
                || IsCombatCancelAllowed(combatTimeline, localFrame, targetActionId);
            if (!combatAllowed)
            {
                return CharacterCancelConflictResult.Rejected(
                    CharacterCancelRejectionAuthority.Combat,
                    CharacterActionDiagnosticCodes.CombatCancelRejected);
            }

            return CharacterCancelConflictResult.Accepted();
        }

        private static bool IsCharacterCancelAllowed(
            IReadOnlyList<CharacterCancelRule> characterRules,
            int localFrame,
            int targetActionId,
            CharacterActionSourceKind sourceKind)
        {
            if (characterRules == null || characterRules.Count == 0)
            {
                return false;
            }

            bool allowed = false;
            for (int i = 0; i < characterRules.Count; i++)
            {
                CharacterCancelRule rule = characterRules[i];
                if (!rule.Matches(localFrame, targetActionId, sourceKind))
                {
                    continue;
                }

                if (!rule.Allow)
                {
                    return false;
                }

                allowed = true;
            }

            return allowed;
        }

        private static bool IsCombatCancelAllowed(CombatActionTimeline combatTimeline, int localFrame, int targetActionId)
        {
            if (combatTimeline == null)
            {
                return false;
            }

            for (int i = 0; i < combatTimeline.WindowCount; i++)
            {
                CombatActionWindow window = combatTimeline.GetWindow(i);
                if (window.Kind == CombatActionWindowKind.Cancel
                    && window.Contains(localFrame)
                    && (window.TargetActionId == 0 || window.TargetActionId == targetActionId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
