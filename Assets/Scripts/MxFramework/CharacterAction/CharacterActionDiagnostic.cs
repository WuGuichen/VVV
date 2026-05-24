using System;

namespace MxFramework.CharacterAction
{
    public static class CharacterActionDiagnosticCodes
    {
        public const string MissingActionSet = "ACT_MISSING_ACTION_SET";
        public const string MissingActionBinding = "ACT_MISSING_ACTION_BINDING";
        public const string MissingAbilityBinding = "ACT_ABILITY_BINDING_MISSING";
        public const string MissingActionConfig = "ACT_MISSING_ACTION_CONFIG";
        public const string MissingReactionProfile = "ACT_MISSING_REACTION_PROFILE";
        public const string ActionDurationMissing = "ACT_ACTION_DURATION_MISSING";
        public const string ActionDurationResolvedFromConfig = "ACT_ACTION_DURATION_RESOLVED_FROM_CONFIG";
        public const string ActionDurationResolvedFromCombat = "ACT_ACTION_DURATION_RESOLVED_FROM_COMBAT";
        public const string ActionDurationFallbackUsed = "ACT_ACTION_DURATION_FALLBACK_USED";
        public const string ActionQueued = "ACT_ACTION_QUEUED";
        public const string ActionLowerPriorityRejected = "ACT_ACTION_LOWER_PRIORITY_REJECTED";
        public const string AbilityRequiredTagMissing = "ACT_ABILITY_REQUIRED_TAG_MISSING";
        public const string AbilityForbiddenTagMatched = "ACT_ABILITY_FORBIDDEN_TAG_MATCHED";
        public const string InsufficientResource = "ACT_INSUFFICIENT_RESOURCE";
        public const string ResourceMissing = "ACT_RESOURCE_MISSING";
        public const string ResourceCostWithoutResourceId = "ACT_RESOURCE_COST_WITHOUT_RESOURCE_ID";
        public const string ReactionContextMissingSource = "ACT_REACTION_CONTEXT_MISSING_SOURCE";
        public const string ReactionContextIncomplete = "ACT_REACTION_CONTEXT_INCOMPLETE";
        public const string ReactionRuleRequiresHitContext = "ACT_REACTION_RULE_REQUIRES_HIT_CONTEXT";
        public const string ReactionRuleNoTarget = "ACT_REACTION_RULE_NO_TARGET";
        public const string ReactionRuleMatched = "ACT_REACTION_RULE_MATCHED";
        public const string ReactionRuleSkipped = "ACT_REACTION_RULE_SKIPPED";
        public const string ReactionFallbackUsed = "ACT_REACTION_FALLBACK_USED";
        public const string PhaseCombatAnchorMissing = "ACT_PHASE_COMBAT_ANCHOR_MISSING";
        public const string PhaseCombatRangeMismatch = "ACT_PHASE_COMBAT_RANGE_MISMATCH";
        public const string PhaseOverlap = "ACT_PHASE_OVERLAP";
        public const string PhaseGap = "ACT_PHASE_GAP";
        public const string PhaseRangeOutsideDuration = "ACT_PHASE_RANGE_OUTSIDE_DURATION";
        public const string CombatTraceOutsideActivePhase = "ACT_COMBAT_TRACE_OUTSIDE_ACTIVE_PHASE";
        public const string TrackEventOutsideDuration = "ACT_TRACK_EVENT_OUTSIDE_DURATION";
        public const string InvalidCancelWindow = "ACT_INVALID_CANCEL_WINDOW";
        public const string CharacterCombatCancelConflict = "ACT_CHARACTER_COMBAT_CANCEL_CONFLICT";
        public const string CancelTargetMissing = "ACT_CANCEL_TARGET_MISSING";
        public const string InterruptTargetMissing = "ACT_INTERRUPT_TARGET_MISSING";
        public const string CharacterCancelRejected = "ACT_CHARACTER_CANCEL_REJECTED";
        public const string CombatCancelRejected = "ACT_COMBAT_CANCEL_REJECTED";
        public const string CombatActionMissing = "ACT_COMBAT_ACTION_MISSING";
        public const string AnimationActionMissing = "ACT_ANIMATION_ACTION_MISSING";
        public const string PresentationResourceMissing = "ACT_PRESENTATION_RESOURCE_MISSING";
        public const string AudioCueMissing = "ACT_AUDIO_CUE_MISSING";
    }

    public enum CharacterActionDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public readonly struct CharacterActionDiagnostic : IEquatable<CharacterActionDiagnostic>
    {
        public CharacterActionDiagnostic(
            string code,
            CharacterActionDiagnosticSeverity severity,
            string message)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Diagnostic code cannot be empty.", nameof(code));

            Code = code;
            Severity = severity;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public CharacterActionDiagnosticSeverity Severity { get; }
        public string Message { get; }

        public bool Equals(CharacterActionDiagnostic other)
        {
            return string.Equals(Code, other.Code, StringComparison.Ordinal)
                && Severity == other.Severity
                && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionDiagnostic other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Code == null ? 0 : Code.GetHashCode();
                hash = (hash * 397) ^ (int)Severity;
                hash = (hash * 397) ^ (Message == null ? 0 : Message.GetHashCode());
                return hash;
            }
        }

        public static CharacterActionDiagnostic Warning(string code, string message)
        {
            return new CharacterActionDiagnostic(code, CharacterActionDiagnosticSeverity.Warning, message);
        }

        public static CharacterActionDiagnostic Error(string code, string message)
        {
            return new CharacterActionDiagnostic(code, CharacterActionDiagnosticSeverity.Error, message);
        }

        public static CharacterActionDiagnostic Info(string code, string message)
        {
            return new CharacterActionDiagnostic(code, CharacterActionDiagnosticSeverity.Info, message);
        }
    }
}
