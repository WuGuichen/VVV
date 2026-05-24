namespace MxFramework.CharacterAction
{
    public static class CharacterActionDiagnostics
    {
        public const string MissingActionSet = CharacterActionDiagnosticCodes.MissingActionSet;
        public const string MissingActionBinding = CharacterActionDiagnosticCodes.MissingActionBinding;
        public const string MissingAbilityBinding = CharacterActionDiagnosticCodes.MissingAbilityBinding;
        public const string MissingActionConfig = CharacterActionDiagnosticCodes.MissingActionConfig;
        public const string MissingReactionProfile = CharacterActionDiagnosticCodes.MissingReactionProfile;
        public const string ActionDurationMissing = CharacterActionDiagnosticCodes.ActionDurationMissing;
        public const string ActionDurationResolvedFromConfig = CharacterActionDiagnosticCodes.ActionDurationResolvedFromConfig;
        public const string ActionDurationResolvedFromCombat = CharacterActionDiagnosticCodes.ActionDurationResolvedFromCombat;
        public const string ActionDurationFallbackUsed = CharacterActionDiagnosticCodes.ActionDurationFallbackUsed;
        public const string ActionQueued = CharacterActionDiagnosticCodes.ActionQueued;
        public const string InsufficientResource = CharacterActionDiagnosticCodes.InsufficientResource;
        public const string ResourceMissing = CharacterActionDiagnosticCodes.ResourceMissing;
        public const string ResourceCostWithoutResourceId = CharacterActionDiagnosticCodes.ResourceCostWithoutResourceId;
        public const string ReactionContextMissingSource = CharacterActionDiagnosticCodes.ReactionContextMissingSource;
        public const string ReactionContextIncomplete = CharacterActionDiagnosticCodes.ReactionContextIncomplete;
        public const string ReactionRuleRequiresHitContext = CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext;
        public const string ReactionRuleNoTarget = CharacterActionDiagnosticCodes.ReactionRuleNoTarget;
        public const string ReactionRuleMatched = CharacterActionDiagnosticCodes.ReactionRuleMatched;
        public const string ReactionRuleSkipped = CharacterActionDiagnosticCodes.ReactionRuleSkipped;
        public const string ReactionFallbackUsed = CharacterActionDiagnosticCodes.ReactionFallbackUsed;
        public const string PhaseCombatAnchorMissing = CharacterActionDiagnosticCodes.PhaseCombatAnchorMissing;
        public const string PhaseCombatRangeMismatch = CharacterActionDiagnosticCodes.PhaseCombatRangeMismatch;
        public const string PhaseOverlap = CharacterActionDiagnosticCodes.PhaseOverlap;
        public const string PhaseGap = CharacterActionDiagnosticCodes.PhaseGap;
        public const string PhaseRangeOutsideDuration = CharacterActionDiagnosticCodes.PhaseRangeOutsideDuration;
        public const string CombatTraceOutsideActivePhase = CharacterActionDiagnosticCodes.CombatTraceOutsideActivePhase;
        public const string TrackEventOutsideDuration = CharacterActionDiagnosticCodes.TrackEventOutsideDuration;
        public const string InvalidCancelWindow = CharacterActionDiagnosticCodes.InvalidCancelWindow;
        public const string CharacterCombatCancelConflict = CharacterActionDiagnosticCodes.CharacterCombatCancelConflict;
        public const string CancelTargetMissing = CharacterActionDiagnosticCodes.CancelTargetMissing;
        public const string InterruptTargetMissing = CharacterActionDiagnosticCodes.InterruptTargetMissing;
        public const string CharacterCancelRejected = CharacterActionDiagnosticCodes.CharacterCancelRejected;
        public const string CombatCancelRejected = CharacterActionDiagnosticCodes.CombatCancelRejected;
        public const string CombatActionMissing = CharacterActionDiagnosticCodes.CombatActionMissing;
        public const string AnimationActionMissing = CharacterActionDiagnosticCodes.AnimationActionMissing;
        public const string PresentationResourceMissing = CharacterActionDiagnosticCodes.PresentationResourceMissing;
        public const string AudioCueMissing = CharacterActionDiagnosticCodes.AudioCueMissing;
    }
}
