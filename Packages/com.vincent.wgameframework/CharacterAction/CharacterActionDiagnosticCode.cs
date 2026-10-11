using System;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionDiagnosticCode
    {
        None = 0,
        MissingActionSet = 1,
        ActionConfigNotReady = 2,
        MissingActionBinding = 3,
        CommandSequenceInvalid = 4,
        CommandIntentConflict = 5,
        CommandSequenceExhausted = 6,
        MissingAbilityBinding = 7,
        MissingActionConfig = 8,
        MissingReactionProfile = 9,
        MissingLandingProfile = 10,
        ActionDurationMissing = 11,
        ActionDurationResolvedFromConfig = 12,
        ActionDurationResolvedFromCombat = 13,
        ActionDurationFallbackUsed = 14,
        ActionQueued = 15,
        ActionLowerPriorityRejected = 16,
        ActionSlotEmpty = 17,
        ActionSlotBlocked = 18,
        AbilityRequiredTagMissing = 19,
        AbilityForbiddenTagMatched = 20,
        InsufficientResource = 21,
        ResourceMissing = 22,
        ResourceCostWithoutResourceId = 23,
        ReactionContextMissingSource = 24,
        ReactionContextIncomplete = 25,
        ReactionRuleRequiresHitContext = 26,
        ReactionRuleNoTarget = 27,
        ReactionRuleMatched = 28,
        ReactionRuleSkipped = 29,
        ReactionFallbackUsed = 30,
        PhaseCombatAnchorMissing = 31,
        PhaseCombatRangeMismatch = 32,
        PhaseOverlap = 33,
        PhaseGap = 34,
        PhaseRangeOutsideDuration = 35,
        CombatTraceOutsideActivePhase = 36,
        LegacyTraceProfileId = 37,
        TrackEventOutsideDuration = 38,
        InvalidCancelWindow = 39,
        CharacterCombatCancelConflict = 40,
        CancelTargetMissing = 41,
        InterruptTargetMissing = 42,
        CharacterCancelRejected = 43,
        CombatCancelRejected = 44,
        CombatActionMissing = 45,
        AnimationActionMissing = 46,
        PresentationResourceMissing = 47,
        AudioCueMissing = 48,
        AdapterSinkMissing = 49,
        AdapterPayloadMissing = 50,
        AdapterSinkFailure = 51,
        WorkstationEditingUnsupported = 52,
        WorkstationUnsupportedField = 53,
        ConfigDuplicateId = 54,
        ConfigDuplicateStableId = 55,
        EquipmentTagMissing = 56,
        CharacterEquipmentStateMissing = 57,
        JsonInvalidDocument = 58,
        JsonMissingRootArray = 59,
        JsonDuplicateId = 60,
        JsonDuplicateStableId = 61,
        JsonInvalidEnum = 62,
        JsonInvalidFrame = 63,
        JsonInvalidConfig = 64,
        InvalidTrackLifecycle = 65,
        InvalidPartStateDependency = 66,
        PartStateDependencyEventUnavailable = 67,
        PartStateDependencyUnknown = 68,
        PartStateDependencyBindingInvalid = 69,
        PartStateDependencyDisallowed = 70,
        PartStateDependencySevered = 71,
        ResolverMissingActionSet = 1001,
        ResolverMissingActionBinding = 1002,
        ResolverMissingAbilityBinding = 1003,
        ResolverMissingActionConfig = 1004,
        ResolverStateDisabled = 1010,
        ResolverDead = 1011,
        ResolverActionCommitted = 1012,
        ResolverCooldownActive = 1020,
        ResolverInsufficientResource = 1021,
        ResolverPressureReactionLocked = 1030,
        ResolverReactionContextIncomplete = 1031,
        ResolverInvalidTarget = 1040,
        ResolverEquipmentStateMismatch = 1041,
        ResolverLowerPriorityRejected = 1050,
        ResolverCombatActionMissing = 1060,
        ResolverAnimationActionMissing = 1061,
        ResolverResourceMissing = 1062,
        ResolverPhaseAnchorInvalid = 1070,
        ResolverCancelConflict = 1080,
        ResolverActionSlotEmpty = 1090,
        ResolverActionSlotBlocked = 1091,
    }

    public static class CharacterActionDiagnosticCodeText
    {
        public static bool IsDefined(CharacterActionDiagnosticCode value)
        {
            switch (value)
            {
                case CharacterActionDiagnosticCode.None:
                case CharacterActionDiagnosticCode.MissingActionSet:
                case CharacterActionDiagnosticCode.ActionConfigNotReady:
                case CharacterActionDiagnosticCode.MissingActionBinding:
                case CharacterActionDiagnosticCode.CommandSequenceInvalid:
                case CharacterActionDiagnosticCode.CommandIntentConflict:
                case CharacterActionDiagnosticCode.CommandSequenceExhausted:
                case CharacterActionDiagnosticCode.MissingAbilityBinding:
                case CharacterActionDiagnosticCode.MissingActionConfig:
                case CharacterActionDiagnosticCode.MissingReactionProfile:
                case CharacterActionDiagnosticCode.MissingLandingProfile:
                case CharacterActionDiagnosticCode.ActionDurationMissing:
                case CharacterActionDiagnosticCode.ActionDurationResolvedFromConfig:
                case CharacterActionDiagnosticCode.ActionDurationResolvedFromCombat:
                case CharacterActionDiagnosticCode.ActionDurationFallbackUsed:
                case CharacterActionDiagnosticCode.ActionQueued:
                case CharacterActionDiagnosticCode.ActionLowerPriorityRejected:
                case CharacterActionDiagnosticCode.ActionSlotEmpty:
                case CharacterActionDiagnosticCode.ActionSlotBlocked:
                case CharacterActionDiagnosticCode.AbilityRequiredTagMissing:
                case CharacterActionDiagnosticCode.AbilityForbiddenTagMatched:
                case CharacterActionDiagnosticCode.InsufficientResource:
                case CharacterActionDiagnosticCode.ResourceMissing:
                case CharacterActionDiagnosticCode.ResourceCostWithoutResourceId:
                case CharacterActionDiagnosticCode.ReactionContextMissingSource:
                case CharacterActionDiagnosticCode.ReactionContextIncomplete:
                case CharacterActionDiagnosticCode.ReactionRuleRequiresHitContext:
                case CharacterActionDiagnosticCode.ReactionRuleNoTarget:
                case CharacterActionDiagnosticCode.ReactionRuleMatched:
                case CharacterActionDiagnosticCode.ReactionRuleSkipped:
                case CharacterActionDiagnosticCode.ReactionFallbackUsed:
                case CharacterActionDiagnosticCode.PhaseCombatAnchorMissing:
                case CharacterActionDiagnosticCode.PhaseCombatRangeMismatch:
                case CharacterActionDiagnosticCode.PhaseOverlap:
                case CharacterActionDiagnosticCode.PhaseGap:
                case CharacterActionDiagnosticCode.PhaseRangeOutsideDuration:
                case CharacterActionDiagnosticCode.CombatTraceOutsideActivePhase:
                case CharacterActionDiagnosticCode.LegacyTraceProfileId:
                case CharacterActionDiagnosticCode.TrackEventOutsideDuration:
                case CharacterActionDiagnosticCode.InvalidCancelWindow:
                case CharacterActionDiagnosticCode.CharacterCombatCancelConflict:
                case CharacterActionDiagnosticCode.CancelTargetMissing:
                case CharacterActionDiagnosticCode.InterruptTargetMissing:
                case CharacterActionDiagnosticCode.CharacterCancelRejected:
                case CharacterActionDiagnosticCode.CombatCancelRejected:
                case CharacterActionDiagnosticCode.CombatActionMissing:
                case CharacterActionDiagnosticCode.AnimationActionMissing:
                case CharacterActionDiagnosticCode.PresentationResourceMissing:
                case CharacterActionDiagnosticCode.AudioCueMissing:
                case CharacterActionDiagnosticCode.AdapterSinkMissing:
                case CharacterActionDiagnosticCode.AdapterPayloadMissing:
                case CharacterActionDiagnosticCode.AdapterSinkFailure:
                case CharacterActionDiagnosticCode.WorkstationEditingUnsupported:
                case CharacterActionDiagnosticCode.WorkstationUnsupportedField:
                case CharacterActionDiagnosticCode.ConfigDuplicateId:
                case CharacterActionDiagnosticCode.ConfigDuplicateStableId:
                case CharacterActionDiagnosticCode.EquipmentTagMissing:
                case CharacterActionDiagnosticCode.CharacterEquipmentStateMissing:
                case CharacterActionDiagnosticCode.JsonInvalidDocument:
                case CharacterActionDiagnosticCode.JsonMissingRootArray:
                case CharacterActionDiagnosticCode.JsonDuplicateId:
                case CharacterActionDiagnosticCode.JsonDuplicateStableId:
                case CharacterActionDiagnosticCode.JsonInvalidEnum:
                case CharacterActionDiagnosticCode.JsonInvalidFrame:
                case CharacterActionDiagnosticCode.JsonInvalidConfig:
                case CharacterActionDiagnosticCode.InvalidTrackLifecycle:
                case CharacterActionDiagnosticCode.InvalidPartStateDependency:
                case CharacterActionDiagnosticCode.PartStateDependencyEventUnavailable:
                case CharacterActionDiagnosticCode.PartStateDependencyUnknown:
                case CharacterActionDiagnosticCode.PartStateDependencyBindingInvalid:
                case CharacterActionDiagnosticCode.PartStateDependencyDisallowed:
                case CharacterActionDiagnosticCode.PartStateDependencySevered:
                case CharacterActionDiagnosticCode.ResolverMissingActionSet:
                case CharacterActionDiagnosticCode.ResolverMissingActionBinding:
                case CharacterActionDiagnosticCode.ResolverMissingAbilityBinding:
                case CharacterActionDiagnosticCode.ResolverMissingActionConfig:
                case CharacterActionDiagnosticCode.ResolverStateDisabled:
                case CharacterActionDiagnosticCode.ResolverDead:
                case CharacterActionDiagnosticCode.ResolverActionCommitted:
                case CharacterActionDiagnosticCode.ResolverCooldownActive:
                case CharacterActionDiagnosticCode.ResolverInsufficientResource:
                case CharacterActionDiagnosticCode.ResolverPressureReactionLocked:
                case CharacterActionDiagnosticCode.ResolverReactionContextIncomplete:
                case CharacterActionDiagnosticCode.ResolverInvalidTarget:
                case CharacterActionDiagnosticCode.ResolverEquipmentStateMismatch:
                case CharacterActionDiagnosticCode.ResolverLowerPriorityRejected:
                case CharacterActionDiagnosticCode.ResolverCombatActionMissing:
                case CharacterActionDiagnosticCode.ResolverAnimationActionMissing:
                case CharacterActionDiagnosticCode.ResolverResourceMissing:
                case CharacterActionDiagnosticCode.ResolverPhaseAnchorInvalid:
                case CharacterActionDiagnosticCode.ResolverCancelConflict:
                case CharacterActionDiagnosticCode.ResolverActionSlotEmpty:
                case CharacterActionDiagnosticCode.ResolverActionSlotBlocked:
                    return true;
                default: return false;
            }
        }

        public static CharacterActionDiagnosticCode FromRejectReason(CharacterActionRejectReason reason)
        {
            switch (reason)
            {
                case CharacterActionRejectReason.None: return CharacterActionDiagnosticCode.None;
                case CharacterActionRejectReason.MissingActionSet: return CharacterActionDiagnosticCode.ResolverMissingActionSet;
                case CharacterActionRejectReason.MissingActionBinding: return CharacterActionDiagnosticCode.ResolverMissingActionBinding;
                case CharacterActionRejectReason.MissingAbilityBinding: return CharacterActionDiagnosticCode.ResolverMissingAbilityBinding;
                case CharacterActionRejectReason.MissingActionConfig: return CharacterActionDiagnosticCode.ResolverMissingActionConfig;
                case CharacterActionRejectReason.StateDisabled: return CharacterActionDiagnosticCode.ResolverStateDisabled;
                case CharacterActionRejectReason.Dead: return CharacterActionDiagnosticCode.ResolverDead;
                case CharacterActionRejectReason.ActionCommitted: return CharacterActionDiagnosticCode.ResolverActionCommitted;
                case CharacterActionRejectReason.CooldownActive: return CharacterActionDiagnosticCode.ResolverCooldownActive;
                case CharacterActionRejectReason.InsufficientResource: return CharacterActionDiagnosticCode.ResolverInsufficientResource;
                case CharacterActionRejectReason.PressureReactionLocked: return CharacterActionDiagnosticCode.ResolverPressureReactionLocked;
                case CharacterActionRejectReason.ReactionContextIncomplete: return CharacterActionDiagnosticCode.ResolverReactionContextIncomplete;
                case CharacterActionRejectReason.InvalidTarget: return CharacterActionDiagnosticCode.ResolverInvalidTarget;
                case CharacterActionRejectReason.EquipmentStateMismatch: return CharacterActionDiagnosticCode.ResolverEquipmentStateMismatch;
                case CharacterActionRejectReason.LowerPriorityRejected: return CharacterActionDiagnosticCode.ResolverLowerPriorityRejected;
                case CharacterActionRejectReason.CombatActionMissing: return CharacterActionDiagnosticCode.ResolverCombatActionMissing;
                case CharacterActionRejectReason.AnimationActionMissing: return CharacterActionDiagnosticCode.ResolverAnimationActionMissing;
                case CharacterActionRejectReason.ResourceMissing: return CharacterActionDiagnosticCode.ResolverResourceMissing;
                case CharacterActionRejectReason.PhaseAnchorInvalid: return CharacterActionDiagnosticCode.ResolverPhaseAnchorInvalid;
                case CharacterActionRejectReason.CancelConflict: return CharacterActionDiagnosticCode.ResolverCancelConflict;
                case CharacterActionRejectReason.ActionSlotEmpty: return CharacterActionDiagnosticCode.ResolverActionSlotEmpty;
                case CharacterActionRejectReason.ActionSlotBlocked: return CharacterActionDiagnosticCode.ResolverActionSlotBlocked;
                default: throw new System.ArgumentOutOfRangeException(nameof(reason));
            }
        }

        public static bool TryParse(string value, out CharacterActionDiagnosticCode code)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.None), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.None;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.MissingActionSet), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.MissingActionSet;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ActionConfigNotReady), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ActionConfigNotReady;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.MissingActionBinding), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.MissingActionBinding;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.CommandSequenceInvalid), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.CommandSequenceInvalid;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.CommandIntentConflict), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.CommandIntentConflict;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.CommandSequenceExhausted), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.CommandSequenceExhausted;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.MissingAbilityBinding), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.MissingAbilityBinding;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.MissingActionConfig), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.MissingActionConfig;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.MissingReactionProfile), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.MissingReactionProfile;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.MissingLandingProfile), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.MissingLandingProfile;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ActionDurationMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ActionDurationMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ActionDurationResolvedFromConfig), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ActionDurationResolvedFromConfig;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ActionDurationResolvedFromCombat), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ActionDurationResolvedFromCombat;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ActionDurationFallbackUsed), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ActionDurationFallbackUsed;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ActionQueued), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ActionQueued;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ActionLowerPriorityRejected), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ActionLowerPriorityRejected;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ActionSlotEmpty), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ActionSlotEmpty;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ActionSlotBlocked), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ActionSlotBlocked;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.AbilityRequiredTagMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.AbilityRequiredTagMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.AbilityForbiddenTagMatched), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.AbilityForbiddenTagMatched;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.InsufficientResource), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.InsufficientResource;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResourceMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResourceMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResourceCostWithoutResourceId), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResourceCostWithoutResourceId;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ReactionContextMissingSource), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ReactionContextMissingSource;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ReactionContextIncomplete), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ReactionContextIncomplete;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ReactionRuleRequiresHitContext), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ReactionRuleRequiresHitContext;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ReactionRuleNoTarget), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ReactionRuleNoTarget;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ReactionRuleMatched), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ReactionRuleMatched;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ReactionRuleSkipped), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ReactionRuleSkipped;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ReactionFallbackUsed), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ReactionFallbackUsed;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.PhaseCombatAnchorMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.PhaseCombatAnchorMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.PhaseCombatRangeMismatch), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.PhaseCombatRangeMismatch;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.PhaseOverlap), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.PhaseOverlap;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.PhaseGap), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.PhaseGap;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.PhaseRangeOutsideDuration), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.PhaseRangeOutsideDuration;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.CombatTraceOutsideActivePhase), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.CombatTraceOutsideActivePhase;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.LegacyTraceProfileId), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.LegacyTraceProfileId;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.TrackEventOutsideDuration), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.TrackEventOutsideDuration;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.InvalidCancelWindow), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.InvalidCancelWindow;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.CharacterCombatCancelConflict), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.CharacterCombatCancelConflict;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.CancelTargetMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.CancelTargetMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.InterruptTargetMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.InterruptTargetMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.CharacterCancelRejected), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.CharacterCancelRejected;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.CombatCancelRejected), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.CombatCancelRejected;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.CombatActionMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.CombatActionMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.AnimationActionMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.AnimationActionMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.PresentationResourceMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.PresentationResourceMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.AudioCueMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.AudioCueMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.AdapterSinkMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.AdapterSinkMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.AdapterPayloadMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.AdapterPayloadMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.AdapterSinkFailure), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.AdapterSinkFailure;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.WorkstationEditingUnsupported), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.WorkstationEditingUnsupported;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.WorkstationUnsupportedField), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.WorkstationUnsupportedField;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ConfigDuplicateId), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ConfigDuplicateId;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ConfigDuplicateStableId), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ConfigDuplicateStableId;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.EquipmentTagMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.EquipmentTagMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.CharacterEquipmentStateMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.CharacterEquipmentStateMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.JsonInvalidDocument), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.JsonInvalidDocument;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.JsonMissingRootArray), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.JsonMissingRootArray;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.JsonDuplicateId), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.JsonDuplicateId;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.JsonDuplicateStableId), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.JsonDuplicateStableId;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.JsonInvalidEnum), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.JsonInvalidEnum;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.JsonInvalidFrame), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.JsonInvalidFrame;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.JsonInvalidConfig), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.JsonInvalidConfig;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.InvalidTrackLifecycle), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.InvalidTrackLifecycle;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.InvalidPartStateDependency), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.InvalidPartStateDependency;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.PartStateDependencyEventUnavailable), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.PartStateDependencyEventUnavailable;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.PartStateDependencyUnknown), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.PartStateDependencyUnknown;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.PartStateDependencyBindingInvalid), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.PartStateDependencyBindingInvalid;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.PartStateDependencyDisallowed), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.PartStateDependencyDisallowed;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.PartStateDependencySevered), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.PartStateDependencySevered;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverMissingActionSet), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverMissingActionSet;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverMissingActionBinding), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverMissingActionBinding;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverMissingAbilityBinding), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverMissingAbilityBinding;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverMissingActionConfig), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverMissingActionConfig;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverStateDisabled), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverStateDisabled;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverDead), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverDead;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverActionCommitted), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverActionCommitted;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverCooldownActive), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverCooldownActive;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverInsufficientResource), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverInsufficientResource;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverPressureReactionLocked), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverPressureReactionLocked;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverReactionContextIncomplete), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverReactionContextIncomplete;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverInvalidTarget), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverInvalidTarget;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverEquipmentStateMismatch), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverEquipmentStateMismatch;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverLowerPriorityRejected), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverLowerPriorityRejected;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverCombatActionMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverCombatActionMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverAnimationActionMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverAnimationActionMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverResourceMissing), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverResourceMissing;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverPhaseAnchorInvalid), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverPhaseAnchorInvalid;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverCancelConflict), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverCancelConflict;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverActionSlotEmpty), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverActionSlotEmpty;
                    return true;
                }
                if (string.Equals(value, Get(CharacterActionDiagnosticCode.ResolverActionSlotBlocked), StringComparison.Ordinal))
                {
                    code = CharacterActionDiagnosticCode.ResolverActionSlotBlocked;
                    return true;
                }
            }

            code = CharacterActionDiagnosticCode.None;
            return false;
        }
        public static string Get(CharacterActionDiagnosticCode code)
        {
            switch (code)
            {
                case CharacterActionDiagnosticCode.None: return string.Empty;
                case CharacterActionDiagnosticCode.MissingActionSet: return CharacterActionDiagnosticCodes.MissingActionSet;
                case CharacterActionDiagnosticCode.ActionConfigNotReady: return CharacterActionDiagnosticCodes.ActionConfigNotReady;
                case CharacterActionDiagnosticCode.MissingActionBinding: return CharacterActionDiagnosticCodes.MissingActionBinding;
                case CharacterActionDiagnosticCode.CommandSequenceInvalid: return CharacterActionDiagnosticCodes.CommandSequenceInvalid;
                case CharacterActionDiagnosticCode.CommandIntentConflict: return CharacterActionDiagnosticCodes.CommandIntentConflict;
                case CharacterActionDiagnosticCode.CommandSequenceExhausted: return CharacterActionDiagnosticCodes.CommandSequenceExhausted;
                case CharacterActionDiagnosticCode.MissingAbilityBinding: return CharacterActionDiagnosticCodes.MissingAbilityBinding;
                case CharacterActionDiagnosticCode.MissingActionConfig: return CharacterActionDiagnosticCodes.MissingActionConfig;
                case CharacterActionDiagnosticCode.MissingReactionProfile: return CharacterActionDiagnosticCodes.MissingReactionProfile;
                case CharacterActionDiagnosticCode.MissingLandingProfile: return CharacterActionDiagnosticCodes.MissingLandingProfile;
                case CharacterActionDiagnosticCode.ActionDurationMissing: return CharacterActionDiagnosticCodes.ActionDurationMissing;
                case CharacterActionDiagnosticCode.ActionDurationResolvedFromConfig: return CharacterActionDiagnosticCodes.ActionDurationResolvedFromConfig;
                case CharacterActionDiagnosticCode.ActionDurationResolvedFromCombat: return CharacterActionDiagnosticCodes.ActionDurationResolvedFromCombat;
                case CharacterActionDiagnosticCode.ActionDurationFallbackUsed: return CharacterActionDiagnosticCodes.ActionDurationFallbackUsed;
                case CharacterActionDiagnosticCode.ActionQueued: return CharacterActionDiagnosticCodes.ActionQueued;
                case CharacterActionDiagnosticCode.ActionLowerPriorityRejected: return CharacterActionDiagnosticCodes.ActionLowerPriorityRejected;
                case CharacterActionDiagnosticCode.ActionSlotEmpty: return CharacterActionDiagnosticCodes.ActionSlotEmpty;
                case CharacterActionDiagnosticCode.ActionSlotBlocked: return CharacterActionDiagnosticCodes.ActionSlotBlocked;
                case CharacterActionDiagnosticCode.AbilityRequiredTagMissing: return CharacterActionDiagnosticCodes.AbilityRequiredTagMissing;
                case CharacterActionDiagnosticCode.AbilityForbiddenTagMatched: return CharacterActionDiagnosticCodes.AbilityForbiddenTagMatched;
                case CharacterActionDiagnosticCode.InsufficientResource: return CharacterActionDiagnosticCodes.InsufficientResource;
                case CharacterActionDiagnosticCode.ResourceMissing: return CharacterActionDiagnosticCodes.ResourceMissing;
                case CharacterActionDiagnosticCode.ResourceCostWithoutResourceId: return CharacterActionDiagnosticCodes.ResourceCostWithoutResourceId;
                case CharacterActionDiagnosticCode.ReactionContextMissingSource: return CharacterActionDiagnosticCodes.ReactionContextMissingSource;
                case CharacterActionDiagnosticCode.ReactionContextIncomplete: return CharacterActionDiagnosticCodes.ReactionContextIncomplete;
                case CharacterActionDiagnosticCode.ReactionRuleRequiresHitContext: return CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext;
                case CharacterActionDiagnosticCode.ReactionRuleNoTarget: return CharacterActionDiagnosticCodes.ReactionRuleNoTarget;
                case CharacterActionDiagnosticCode.ReactionRuleMatched: return CharacterActionDiagnosticCodes.ReactionRuleMatched;
                case CharacterActionDiagnosticCode.ReactionRuleSkipped: return CharacterActionDiagnosticCodes.ReactionRuleSkipped;
                case CharacterActionDiagnosticCode.ReactionFallbackUsed: return CharacterActionDiagnosticCodes.ReactionFallbackUsed;
                case CharacterActionDiagnosticCode.PhaseCombatAnchorMissing: return CharacterActionDiagnosticCodes.PhaseCombatAnchorMissing;
                case CharacterActionDiagnosticCode.PhaseCombatRangeMismatch: return CharacterActionDiagnosticCodes.PhaseCombatRangeMismatch;
                case CharacterActionDiagnosticCode.PhaseOverlap: return CharacterActionDiagnosticCodes.PhaseOverlap;
                case CharacterActionDiagnosticCode.PhaseGap: return CharacterActionDiagnosticCodes.PhaseGap;
                case CharacterActionDiagnosticCode.PhaseRangeOutsideDuration: return CharacterActionDiagnosticCodes.PhaseRangeOutsideDuration;
                case CharacterActionDiagnosticCode.CombatTraceOutsideActivePhase: return CharacterActionDiagnosticCodes.CombatTraceOutsideActivePhase;
                case CharacterActionDiagnosticCode.LegacyTraceProfileId: return CharacterActionDiagnosticCodes.LegacyTraceProfileId;
                case CharacterActionDiagnosticCode.TrackEventOutsideDuration: return CharacterActionDiagnosticCodes.TrackEventOutsideDuration;
                case CharacterActionDiagnosticCode.InvalidCancelWindow: return CharacterActionDiagnosticCodes.InvalidCancelWindow;
                case CharacterActionDiagnosticCode.CharacterCombatCancelConflict: return CharacterActionDiagnosticCodes.CharacterCombatCancelConflict;
                case CharacterActionDiagnosticCode.CancelTargetMissing: return CharacterActionDiagnosticCodes.CancelTargetMissing;
                case CharacterActionDiagnosticCode.InterruptTargetMissing: return CharacterActionDiagnosticCodes.InterruptTargetMissing;
                case CharacterActionDiagnosticCode.CharacterCancelRejected: return CharacterActionDiagnosticCodes.CharacterCancelRejected;
                case CharacterActionDiagnosticCode.CombatCancelRejected: return CharacterActionDiagnosticCodes.CombatCancelRejected;
                case CharacterActionDiagnosticCode.CombatActionMissing: return CharacterActionDiagnosticCodes.CombatActionMissing;
                case CharacterActionDiagnosticCode.AnimationActionMissing: return CharacterActionDiagnosticCodes.AnimationActionMissing;
                case CharacterActionDiagnosticCode.PresentationResourceMissing: return CharacterActionDiagnosticCodes.PresentationResourceMissing;
                case CharacterActionDiagnosticCode.AudioCueMissing: return CharacterActionDiagnosticCodes.AudioCueMissing;
                case CharacterActionDiagnosticCode.AdapterSinkMissing: return CharacterActionDiagnosticCodes.AdapterSinkMissing;
                case CharacterActionDiagnosticCode.AdapterPayloadMissing: return CharacterActionDiagnosticCodes.AdapterPayloadMissing;
                case CharacterActionDiagnosticCode.AdapterSinkFailure: return CharacterActionDiagnosticCodes.AdapterSinkFailure;
                case CharacterActionDiagnosticCode.WorkstationEditingUnsupported: return CharacterActionDiagnosticCodes.WorkstationEditingUnsupported;
                case CharacterActionDiagnosticCode.WorkstationUnsupportedField: return CharacterActionDiagnosticCodes.WorkstationUnsupportedField;
                case CharacterActionDiagnosticCode.ConfigDuplicateId: return CharacterActionDiagnosticCodes.ConfigDuplicateId;
                case CharacterActionDiagnosticCode.ConfigDuplicateStableId: return CharacterActionDiagnosticCodes.ConfigDuplicateStableId;
                case CharacterActionDiagnosticCode.EquipmentTagMissing: return CharacterActionDiagnosticCodes.EquipmentTagMissing;
                case CharacterActionDiagnosticCode.CharacterEquipmentStateMissing: return CharacterActionDiagnosticCodes.CharacterEquipmentStateMissing;
                case CharacterActionDiagnosticCode.JsonInvalidDocument: return CharacterActionDiagnosticCodes.JsonInvalidDocument;
                case CharacterActionDiagnosticCode.JsonMissingRootArray: return CharacterActionDiagnosticCodes.JsonMissingRootArray;
                case CharacterActionDiagnosticCode.JsonDuplicateId: return CharacterActionDiagnosticCodes.JsonDuplicateId;
                case CharacterActionDiagnosticCode.JsonDuplicateStableId: return CharacterActionDiagnosticCodes.JsonDuplicateStableId;
                case CharacterActionDiagnosticCode.JsonInvalidEnum: return CharacterActionDiagnosticCodes.JsonInvalidEnum;
                case CharacterActionDiagnosticCode.JsonInvalidFrame: return CharacterActionDiagnosticCodes.JsonInvalidFrame;
                case CharacterActionDiagnosticCode.JsonInvalidConfig: return CharacterActionDiagnosticCodes.JsonInvalidConfig;
                case CharacterActionDiagnosticCode.InvalidTrackLifecycle: return CharacterActionDiagnosticCodes.InvalidTrackLifecycle;
                case CharacterActionDiagnosticCode.InvalidPartStateDependency: return CharacterActionDiagnosticCodes.InvalidPartStateDependency;
                case CharacterActionDiagnosticCode.PartStateDependencyEventUnavailable: return CharacterActionDiagnosticCodes.PartStateDependencyEventUnavailable;
                case CharacterActionDiagnosticCode.PartStateDependencyUnknown: return CharacterActionDiagnosticCodes.PartStateDependencyUnknown;
                case CharacterActionDiagnosticCode.PartStateDependencyBindingInvalid: return CharacterActionDiagnosticCodes.PartStateDependencyBindingInvalid;
                case CharacterActionDiagnosticCode.PartStateDependencyDisallowed: return CharacterActionDiagnosticCodes.PartStateDependencyDisallowed;
                case CharacterActionDiagnosticCode.PartStateDependencySevered: return CharacterActionDiagnosticCodes.PartStateDependencySevered;
                case CharacterActionDiagnosticCode.ResolverMissingActionSet: return "MissingActionSet";
                case CharacterActionDiagnosticCode.ResolverMissingActionBinding: return "MissingActionBinding";
                case CharacterActionDiagnosticCode.ResolverMissingAbilityBinding: return "MissingAbilityBinding";
                case CharacterActionDiagnosticCode.ResolverMissingActionConfig: return "MissingActionConfig";
                case CharacterActionDiagnosticCode.ResolverStateDisabled: return "StateDisabled";
                case CharacterActionDiagnosticCode.ResolverDead: return "Dead";
                case CharacterActionDiagnosticCode.ResolverActionCommitted: return "ActionCommitted";
                case CharacterActionDiagnosticCode.ResolverCooldownActive: return "CooldownActive";
                case CharacterActionDiagnosticCode.ResolverInsufficientResource: return "InsufficientResource";
                case CharacterActionDiagnosticCode.ResolverPressureReactionLocked: return "PressureReactionLocked";
                case CharacterActionDiagnosticCode.ResolverReactionContextIncomplete: return "ReactionContextIncomplete";
                case CharacterActionDiagnosticCode.ResolverInvalidTarget: return "InvalidTarget";
                case CharacterActionDiagnosticCode.ResolverEquipmentStateMismatch: return "EquipmentStateMismatch";
                case CharacterActionDiagnosticCode.ResolverLowerPriorityRejected: return "LowerPriorityRejected";
                case CharacterActionDiagnosticCode.ResolverCombatActionMissing: return "CombatActionMissing";
                case CharacterActionDiagnosticCode.ResolverAnimationActionMissing: return "AnimationActionMissing";
                case CharacterActionDiagnosticCode.ResolverResourceMissing: return "ResourceMissing";
                case CharacterActionDiagnosticCode.ResolverPhaseAnchorInvalid: return "PhaseAnchorInvalid";
                case CharacterActionDiagnosticCode.ResolverCancelConflict: return "CancelConflict";
                case CharacterActionDiagnosticCode.ResolverActionSlotEmpty: return "ActionSlotEmpty";
                case CharacterActionDiagnosticCode.ResolverActionSlotBlocked: return "ActionSlotBlocked";
                default: throw new System.ArgumentOutOfRangeException(nameof(code));
            }
        }
    }
}
