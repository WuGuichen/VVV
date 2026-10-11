using System;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionSourceKind
    {
        None = 0,
        Command = 1,
        GameplayAbility = 2,
        LocalInput = 3,
        RuntimeAiPlanner = 4,
        RuntimeAI = RuntimeAiPlanner,
        Replay = 5,
        Scripted = 6,
        CohesionBreak = 10,
        GuardBreak = 11,
        ArmorBreak = 12,
        Hit = 13,
        Death = 14,
        Reaction = 15,
        Landing = 16,
        PlayerIntervention = 20,
        Debug = 30,
    }

    public readonly struct CharacterCancelRule : IEquatable<CharacterCancelRule>
    {
        public CharacterCancelRule(
            int startFrame,
            int endFrame,
            CharacterActionRequestType requestTypeMask = CharacterActionRequestType.Any,
            CharacterCancelInputMask cancelInputMask = CharacterCancelInputMask.Any)
        {
            if (startFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Cancel start frame cannot be negative.");
            }

            if (endFrame < startFrame)
            {
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Cancel end frame cannot be before start frame.");
            }

            if ((requestTypeMask & ~CharacterActionRequestType.All) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestTypeMask), "Request type mask contains undefined bits.");
            }

            if ((cancelInputMask & ~CharacterCancelInputMask.All) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cancelInputMask), "Cancel input mask contains undefined bits.");
            }

            StartFrame = startFrame;
            EndFrame = endFrame;
            RequestTypeMask = requestTypeMask;
            CancelInputMask = cancelInputMask;
        }

        [Obsolete("Use constructor with CharacterCancelInputMask.")]
        public CharacterCancelRule(
            int startFrame,
            int endFrame,
            CharacterActionSlotType actionSlotMask)
            : this(startFrame, endFrame, CharacterActionRequestType.Any, (CharacterCancelInputMask)actionSlotMask)
        {
        }

        [Obsolete("Use constructor with CharacterCancelInputMask.")]
        public CharacterCancelRule(
            int startFrame,
            int endFrame,
            CharacterActionRequestType requestTypeMask,
            CharacterActionSlotType actionSlotMask)
            : this(startFrame, endFrame, requestTypeMask, (CharacterCancelInputMask)actionSlotMask)
        {
        }

        public int StartFrame { get; }

        public int EndFrame { get; }

        public CharacterActionRequestType RequestTypeMask { get; }

        public CharacterCancelInputMask CancelInputMask { get; }

        [Obsolete("Use CancelInputMask to avoid confusion with tactical action slots.")]
        public CharacterActionSlotType ActionSlotMask => (CharacterActionSlotType)CancelInputMask;

        /// <remarks>NoAlloc.</remarks>
        public bool Matches(int localFrame, CharacterActionCategory requestCategory, int actionSlotId)
        {
            return Matches(localFrame, ToRequestType(requestCategory), actionSlotId);
        }

        /// <summary>Matches an already-classified request, including requests that do not start another action.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool Matches(int localFrame, CharacterActionRequestType requestType, int actionSlotId)
        {
            return localFrame >= StartFrame
                && localFrame <= EndFrame
                && MatchesRequestType(requestType)
                && MatchesCancelInput(actionSlotId);
        }

        private bool MatchesRequestType(CharacterActionRequestType requestType)
        {
            if (RequestTypeMask == CharacterActionRequestType.Any)
                return true;

            return requestType != CharacterActionRequestType.Any
                && (RequestTypeMask & requestType) != 0;
        }

        private bool MatchesCancelInput(int cancelInputId)
        {
            return CancelInputMask == CharacterCancelInputMask.Any
                || (cancelInputId > 0 && (CancelInputMask & (CharacterCancelInputMask)cancelInputId) != 0);
        }

        private static CharacterActionRequestType ToRequestType(CharacterActionCategory category)
        {
            switch (category)
            {
                case CharacterActionCategory.Idle: return CharacterActionRequestType.Idle;
                case CharacterActionCategory.Movement: return CharacterActionRequestType.Movement;
                case CharacterActionCategory.BasicAttack: return CharacterActionRequestType.BasicAttack;
                case CharacterActionCategory.Skill: return CharacterActionRequestType.Skill;
                case CharacterActionCategory.Guard: return CharacterActionRequestType.Guard;
                case CharacterActionCategory.Dodge: return CharacterActionRequestType.Dodge;
                case CharacterActionCategory.Jump: return CharacterActionRequestType.Jump;
                case CharacterActionCategory.Interaction: return CharacterActionRequestType.Interaction;
                case CharacterActionCategory.Reaction: return CharacterActionRequestType.Reaction;
                case CharacterActionCategory.PassiveOverlay: return CharacterActionRequestType.PassiveOverlay;
                default: return CharacterActionRequestType.Any;
            }
        }

        public bool Equals(CharacterCancelRule other)
        {
            return StartFrame == other.StartFrame
                && EndFrame == other.EndFrame
                && RequestTypeMask == other.RequestTypeMask
                && CancelInputMask == other.CancelInputMask;
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
                hash = (hash * 397) ^ (int)RequestTypeMask;
                hash = (hash * 397) ^ (int)CancelInputMask;
                return hash;
            }
        }
    }

    public readonly struct CharacterInterruptRule : IEquatable<CharacterInterruptRule>
    {
        public CharacterInterruptRule(
            CharacterActionSourceKind sourceKind,
            int minimumPriority = 0,
            CharacterActionId targetActionId = default,
            bool allow = true)
        {
            SourceKind = sourceKind;
            MinimumPriority = minimumPriority;
            TargetActionId = targetActionId;
            Allow = allow;
        }

        public CharacterActionSourceKind SourceKind { get; }

        public int MinimumPriority { get; }

        public CharacterActionId TargetActionId { get; }

        public bool Allow { get; }

        public bool Matches(CharacterActionSourceKind sourceKind, int priority, CharacterActionId targetActionId)
        {
            return CharacterActionSourceKindUtility.Matches(SourceKind, sourceKind)
                && priority >= MinimumPriority
                && (!TargetActionId.IsValid || TargetActionId == targetActionId);
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
                hash = (hash * 397) ^ TargetActionId.GetHashCode();
                hash = (hash * 397) ^ Allow.GetHashCode();
                return hash;
            }
        }
    }

    internal static class CharacterActionSourceKindUtility
    {
        public static bool Matches(CharacterActionSourceKind ruleSourceKind, CharacterActionSourceKind requestSourceKind)
        {
            return ruleSourceKind == CharacterActionSourceKind.None
                || ruleSourceKind == requestSourceKind
                || (ruleSourceKind == CharacterActionSourceKind.Command && IsCommandLike(requestSourceKind));
        }

        private static bool IsCommandLike(CharacterActionSourceKind sourceKind)
        {
            return sourceKind == CharacterActionSourceKind.LocalInput
                || sourceKind == CharacterActionSourceKind.RuntimeAiPlanner
                || sourceKind == CharacterActionSourceKind.Replay
                || sourceKind == CharacterActionSourceKind.Scripted
                || sourceKind == CharacterActionSourceKind.Debug
                || sourceKind == CharacterActionSourceKind.PlayerIntervention;
        }
    }
}
