using System;
using MxFramework.Gameplay;

namespace MxFramework.CharacterAction
{
    public enum CharacterReactionRuleTrigger
    {
        Any = 0,
        PostureBreak = 1,
        GuardBreak = 2,
        ArmorBreak = 3,
        PressureBandChanged = 4,
        Death = 5,
        Lifecycle = 6
    }

    public sealed class CharacterReactionRule
    {
        public CharacterReactionRule(
            string actionId,
            CharacterReactionRuleTrigger trigger,
            bool requiresBodyPart = false,
            bool requiresHitDirection = false,
            bool requiresDamageType = false,
            bool requiresReactionGroup = false,
            PressureBand? currentPressureBand = null,
            bool? isDeath = null,
            bool? isAirborne = null,
            CharacterActionPhaseKind? currentPhase = null,
            bool? currentActionCommitted = null,
            bool? currentActionInterruptible = null)
        {
            ActionId = actionId ?? string.Empty;
            Trigger = trigger;
            RequiresBodyPart = requiresBodyPart;
            RequiresHitDirection = requiresHitDirection;
            RequiresDamageType = requiresDamageType;
            RequiresReactionGroup = requiresReactionGroup;
            CurrentPressureBand = currentPressureBand;
            IsDeath = isDeath;
            IsAirborne = isAirborne;
            CurrentPhase = currentPhase;
            CurrentActionCommitted = currentActionCommitted;
            CurrentActionInterruptible = currentActionInterruptible;
        }

        public string ActionId { get; }
        public CharacterReactionRuleTrigger Trigger { get; }
        public bool RequiresBodyPart { get; }
        public bool RequiresHitDirection { get; }
        public bool RequiresDamageType { get; }
        public bool RequiresReactionGroup { get; }
        public PressureBand? CurrentPressureBand { get; }
        public bool? IsDeath { get; }
        public bool? IsAirborne { get; }
        public CharacterActionPhaseKind? CurrentPhase { get; }
        public bool? CurrentActionCommitted { get; }
        public bool? CurrentActionInterruptible { get; }
        public bool RequiresHitContext => RequiresBodyPart || RequiresHitDirection || RequiresDamageType || RequiresReactionGroup;
    }

    public sealed class CharacterReactionProfile
    {
        public CharacterReactionProfile(string stableId, CharacterReactionRule[] rules, string defaultActionId = "")
        {
            StableId = stableId ?? string.Empty;
            Rules = rules ?? Array.Empty<CharacterReactionRule>();
            DefaultActionId = defaultActionId ?? string.Empty;
        }

        public string StableId { get; }
        public CharacterReactionRule[] Rules { get; }
        public string DefaultActionId { get; }
    }

    public readonly struct CharacterReactionSelectionResult
    {
        public CharacterReactionSelectionResult(
            bool accepted,
            string selectedActionId,
            string rejectCode,
            CharacterActionDiagnostic[] diagnostics)
        {
            Accepted = accepted;
            SelectedActionId = selectedActionId ?? string.Empty;
            RejectCode = rejectCode ?? string.Empty;
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
        }

        public bool Accepted { get; }
        public string SelectedActionId { get; }
        public string RejectCode { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }
    }

    public static class CharacterReactionSelector
    {
        public static CharacterReactionSelectionResult Select(
            CharacterReactionProfile profile,
            CharacterReactionContext context)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            CharacterReactionRuleTrigger trigger = ToTrigger(context.SourceKind);
            for (int i = 0; i < profile.Rules.Length; i++)
            {
                CharacterReactionRule rule = profile.Rules[i];
                if (rule == null || !Matches(rule.Trigger, trigger))
                {
                    continue;
                }

                if (rule.RequiresHitContext && !context.HasFullHitContext)
                {
                    return HitContextRequired(rule, context);
                }

                if (!MatchesPressureOnlyDimensions(rule, context))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(rule.ActionId))
                {
                    return new CharacterReactionSelectionResult(
                        accepted: false,
                        selectedActionId: string.Empty,
                        rejectCode: CharacterActionDiagnosticCodes.ReactionRuleNoTarget,
                        diagnostics: new[]
                        {
                            CharacterActionDiagnostic.Error(
                                CharacterActionDiagnosticCodes.ReactionRuleNoTarget,
                                "Reaction rule matched but did not provide an action id.")
                        });
                }

                return new CharacterReactionSelectionResult(
                    accepted: true,
                    selectedActionId: rule.ActionId,
                    rejectCode: string.Empty,
                    diagnostics: Array.Empty<CharacterActionDiagnostic>());
            }

            if (!string.IsNullOrEmpty(profile.DefaultActionId))
            {
                return new CharacterReactionSelectionResult(
                    accepted: true,
                    selectedActionId: profile.DefaultActionId,
                    rejectCode: string.Empty,
                    diagnostics: Array.Empty<CharacterActionDiagnostic>());
            }

            return new CharacterReactionSelectionResult(
                accepted: false,
                selectedActionId: string.Empty,
                rejectCode: CharacterActionDiagnosticCodes.ReactionRuleNoTarget,
                diagnostics: new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.ReactionRuleNoTarget,
                        "Reaction profile has no matching rule and no default action id.")
                });
        }

        private static CharacterReactionSelectionResult HitContextRequired(
            CharacterReactionRule rule,
            CharacterReactionContext context)
        {
            return new CharacterReactionSelectionResult(
                accepted: false,
                selectedActionId: string.Empty,
                rejectCode: CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext,
                diagnostics: new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext,
                        "Reaction rule requires full hit context but only " + context.Completeness + " context is available."),
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.ReactionContextIncomplete,
                        "Rule action '" + rule.ActionId + "' cannot be evaluated without body part, damage type, hit direction, or reaction group.")
                });
        }

        private static bool Matches(CharacterReactionRuleTrigger ruleTrigger, CharacterReactionRuleTrigger contextTrigger)
        {
            return ruleTrigger == CharacterReactionRuleTrigger.Any || ruleTrigger == contextTrigger;
        }

        private static bool MatchesPressureOnlyDimensions(CharacterReactionRule rule, CharacterReactionContext context)
        {
            return (!rule.CurrentPressureBand.HasValue || rule.CurrentPressureBand.Value == context.CurrentPressureBand)
                && (!rule.IsDeath.HasValue || rule.IsDeath.Value == context.IsDeath)
                && (!rule.IsAirborne.HasValue || rule.IsAirborne.Value == context.IsAirborne)
                && (!rule.CurrentPhase.HasValue || rule.CurrentPhase.Value == context.CurrentCharacterPhase)
                && (!rule.CurrentActionCommitted.HasValue || rule.CurrentActionCommitted.Value == context.CurrentActionCommitted)
                && (!rule.CurrentActionInterruptible.HasValue || rule.CurrentActionInterruptible.Value == context.CurrentActionInterruptible);
        }

        private static CharacterReactionRuleTrigger ToTrigger(CharacterReactionContextSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case CharacterReactionContextSourceKind.PostureBreak:
                    return CharacterReactionRuleTrigger.PostureBreak;
                case CharacterReactionContextSourceKind.GuardBreak:
                    return CharacterReactionRuleTrigger.GuardBreak;
                case CharacterReactionContextSourceKind.ArmorBreak:
                    return CharacterReactionRuleTrigger.ArmorBreak;
                case CharacterReactionContextSourceKind.PressureBandChanged:
                    return CharacterReactionRuleTrigger.PressureBandChanged;
                case CharacterReactionContextSourceKind.Death:
                    return CharacterReactionRuleTrigger.Death;
                case CharacterReactionContextSourceKind.Lifecycle:
                    return CharacterReactionRuleTrigger.Lifecycle;
                default:
                    return CharacterReactionRuleTrigger.Any;
            }
        }
    }

    public static class CharacterReactionRuleValidator
    {
        public static CharacterActionDiagnostic[] ValidatePressureOnlyProfile(CharacterReactionProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            for (int i = 0; i < profile.Rules.Length; i++)
            {
                CharacterReactionRule rule = profile.Rules[i];
                if (rule == null)
                {
                    continue;
                }

                if (rule.RequiresHitContext)
                {
                    return new[]
                    {
                        CharacterActionDiagnostic.Error(
                            CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext,
                            "PressureOnly reaction profile cannot use body part, damage type, hit direction, or reaction group dimensions.")
                    };
                }
            }

            return Array.Empty<CharacterActionDiagnostic>();
        }

        public static CharacterActionDiagnostic[] ValidateAgainstContext(
            CharacterReactionRule rule,
            CharacterReactionContext context)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            if (rule.RequiresHitContext && !context.HasFullHitContext)
            {
                return new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext,
                        "Reaction rule requires hit context that is not available in " + context.Completeness + " context.")
                };
            }

            return Array.Empty<CharacterActionDiagnostic>();
        }
    }
}
