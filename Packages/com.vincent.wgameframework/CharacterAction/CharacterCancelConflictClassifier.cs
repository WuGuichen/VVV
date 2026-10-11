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
        private CharacterCancelConflictResult(bool allowed, CharacterCancelRejectionAuthority rejectedBy, CharacterActionDiagnosticCode code)
        {
            Allowed = allowed;
            RejectedBy = rejectedBy;
            CodeId = code;
        }

        public bool Allowed { get; }

        public CharacterCancelRejectionAuthority RejectedBy { get; }

        public CharacterActionDiagnosticCode CodeId { get; }
        public string Code => CharacterActionDiagnosticCodeText.Get(CodeId);

        public static CharacterCancelConflictResult Accepted()
        {
            return new CharacterCancelConflictResult(true, CharacterCancelRejectionAuthority.None, CharacterActionDiagnosticCode.None);
        }

        public static CharacterCancelConflictResult Rejected(CharacterCancelRejectionAuthority rejectedBy, CharacterActionDiagnosticCode code)
        {
            return new CharacterCancelConflictResult(false, rejectedBy, code);
        }

        public bool Equals(CharacterCancelConflictResult other)
        {
            return Allowed == other.Allowed
                && RejectedBy == other.RejectedBy
                && CodeId == other.CodeId;
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
                hash = (hash * 397) ^ (int)CodeId;
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
            CharacterActionId targetActionId,
            CharacterActionCategory requestCategory,
            int actionSlotId = 0)
        {
            if (localFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            }

            if (actionSlotId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionSlotId), "Action slot id cannot be negative.");
            }

            bool characterAllowed = IsCharacterCancelAllowed(characterRules, localFrame, requestCategory, actionSlotId);
            if (!characterAllowed)
            {
                return CharacterCancelConflictResult.Rejected(
                    CharacterCancelRejectionAuthority.Character,
                    CharacterActionDiagnosticCode.CharacterCancelRejected);
            }

            bool combatAllowed = authority != CharacterActionTimelineAuthority.CombatAnchored
                || IsCombatCancelAllowed(combatTimeline, localFrame, targetActionId.Value);
            if (!combatAllowed)
            {
                return CharacterCancelConflictResult.Rejected(
                    CharacterCancelRejectionAuthority.Combat,
                    CharacterActionDiagnosticCode.CombatCancelRejected);
            }

            return CharacterCancelConflictResult.Accepted();
        }

        /// <summary>Classifies a request that exits the active action without a target action.</summary>
        /// <remarks>NoAlloc. Combat-anchored timelines reject because they require a concrete target action.</remarks>
        public static CharacterCancelConflictResult ClassifyUntargeted(
            CharacterActionTimelineAuthority authority,
            IReadOnlyList<CharacterCancelRule> characterRules,
            int localFrame,
            CharacterActionRequestType requestType,
            int actionSlotId = 0)
        {
            if (localFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            if ((requestType & ~CharacterActionRequestType.All) != 0
                || requestType == CharacterActionRequestType.Any)
            {
                throw new ArgumentOutOfRangeException(nameof(requestType), "Request type must identify one or more defined request types.");
            }
            if (actionSlotId < 0)
                throw new ArgumentOutOfRangeException(nameof(actionSlotId), "Action slot id cannot be negative.");

            if (!IsCharacterCancelAllowed(characterRules, localFrame, requestType, actionSlotId))
            {
                return CharacterCancelConflictResult.Rejected(
                    CharacterCancelRejectionAuthority.Character,
                    CharacterActionDiagnosticCode.CharacterCancelRejected);
            }

            if (authority == CharacterActionTimelineAuthority.CombatAnchored)
            {
                return CharacterCancelConflictResult.Rejected(
                    CharacterCancelRejectionAuthority.Combat,
                    CharacterActionDiagnosticCode.CombatCancelRejected);
            }

            return CharacterCancelConflictResult.Accepted();
        }

        private static bool IsCharacterCancelAllowed(
            IReadOnlyList<CharacterCancelRule> characterRules,
            int localFrame,
            CharacterActionCategory requestCategory,
            int actionSlotId)
        {
            if (characterRules == null || characterRules.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < characterRules.Count; i++)
            {
                CharacterCancelRule rule = characterRules[i];
                if (rule.Matches(localFrame, requestCategory, actionSlotId))
                    return true;
            }

            return false;
        }

        private static bool IsCharacterCancelAllowed(
            IReadOnlyList<CharacterCancelRule> characterRules,
            int localFrame,
            CharacterActionRequestType requestType,
            int actionSlotId)
        {
            if (characterRules == null || characterRules.Count == 0)
                return false;

            for (int i = 0; i < characterRules.Count; i++)
            {
                CharacterCancelRule rule = characterRules[i];
                if (rule.Matches(localFrame, requestType, actionSlotId))
                    return true;
            }

            return false;
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
