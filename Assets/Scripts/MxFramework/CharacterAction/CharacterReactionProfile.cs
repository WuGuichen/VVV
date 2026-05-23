using System;
using System.Collections.Generic;
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
            bool requiresHitZone = false,
            bool requiresHitDirection = false,
            bool requiresDamageType = false,
            bool requiresReactionGroup = false,
            PressureBand? currentPressureBand = null,
            bool? isDeath = null,
            bool? isAirborne = null,
            CharacterActionPhaseKind? currentPhase = null,
            bool? currentActionCommitted = null,
            bool? currentActionInterruptible = null,
            int priority = 0)
        {
            ActionId = actionId ?? string.Empty;
            Trigger = trigger;
            RequiresBodyPart = requiresBodyPart;
            RequiresHitZone = requiresHitZone;
            RequiresHitDirection = requiresHitDirection;
            RequiresDamageType = requiresDamageType;
            RequiresReactionGroup = requiresReactionGroup;
            CurrentPressureBand = currentPressureBand;
            IsDeath = isDeath;
            IsAirborne = isAirborne;
            CurrentPhase = currentPhase;
            CurrentActionCommitted = currentActionCommitted;
            CurrentActionInterruptible = currentActionInterruptible;
            Priority = priority;
        }

        public string ActionId { get; }
        public CharacterReactionRuleTrigger Trigger { get; }
        public bool RequiresBodyPart { get; }
        public bool RequiresHitZone { get; }
        public bool RequiresHitDirection { get; }
        public bool RequiresDamageType { get; }
        public bool RequiresReactionGroup { get; }
        public PressureBand? CurrentPressureBand { get; }
        public bool? IsDeath { get; }
        public bool? IsAirborne { get; }
        public CharacterActionPhaseKind? CurrentPhase { get; }
        public bool? CurrentActionCommitted { get; }
        public bool? CurrentActionInterruptible { get; }
        public int Priority { get; }
        public bool RequiresHitContext => RequiresBodyPart || RequiresHitZone || RequiresHitDirection || RequiresDamageType || RequiresReactionGroup;
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
            var diagnostics = new List<CharacterActionDiagnostic>();
            var candidates = new List<CharacterReactionRuleCandidate>();
            for (int i = 0; i < profile.Rules.Length; i++)
            {
                CharacterReactionRule rule = profile.Rules[i];
                if (rule == null || !Matches(rule.Trigger, trigger))
                {
                    if (rule != null)
                    {
                        diagnostics.Add(CharacterActionDiagnostic.Info(
                            CharacterActionDiagnosticCodes.ReactionRuleSkipped,
                            "Skipped reaction rule '" + rule.ActionId + "' because trigger '" + rule.Trigger + "' does not match context trigger '" + trigger + "'."));
                    }

                    continue;
                }

                if (rule.RequiresHitContext && !context.HasFullHitContext)
                {
                    return HitContextRequired(rule, context);
                }

                if (!MatchesPressureOnlyDimensions(rule, context))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Info(
                        CharacterActionDiagnosticCodes.ReactionRuleSkipped,
                        "Skipped reaction rule '" + rule.ActionId + "' because PressureOnly dimensions did not match."));
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

                candidates.Add(new CharacterReactionRuleCandidate(
                    rule,
                    i,
                    GetTriggerSpecificity(rule.Trigger, trigger),
                    GetPressureOnlySpecificity(rule)));
            }

            if (candidates.Count > 0)
            {
                candidates.Sort(CompareCandidates);
                CharacterReactionRuleCandidate selected = candidates[0];
                for (int i = 1; i < candidates.Count; i++)
                {
                    diagnostics.Add(CharacterActionDiagnostic.Info(
                        CharacterActionDiagnosticCodes.ReactionRuleSkipped,
                        "Skipped reaction rule '" + candidates[i].Rule.ActionId + "' because rule '" + selected.Rule.ActionId + "' ranked higher."));
                }

                diagnostics.Add(CharacterActionDiagnostic.Info(
                    CharacterActionDiagnosticCodes.ReactionRuleMatched,
                    "Matched reaction rule '" + selected.Rule.ActionId + "'."));

                return new CharacterReactionSelectionResult(
                    accepted: true,
                    selectedActionId: selected.Rule.ActionId,
                    rejectCode: string.Empty,
                    diagnostics: diagnostics.ToArray());
            }

            if (!string.IsNullOrEmpty(profile.DefaultActionId))
            {
                diagnostics.Add(CharacterActionDiagnostic.Info(
                    CharacterActionDiagnosticCodes.ReactionFallbackUsed,
                    "Reaction profile used fallback action '" + profile.DefaultActionId + "'."));

                return new CharacterReactionSelectionResult(
                    accepted: true,
                    selectedActionId: profile.DefaultActionId,
                    rejectCode: string.Empty,
                    diagnostics: diagnostics.ToArray());
            }

            diagnostics.Add(CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCodes.ReactionRuleNoTarget,
                "Reaction profile has no matching rule and no default action id."));

            return new CharacterReactionSelectionResult(
                accepted: false,
                selectedActionId: string.Empty,
                rejectCode: CharacterActionDiagnosticCodes.ReactionRuleNoTarget,
                diagnostics: diagnostics.ToArray());
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
                        "Rule action '" + rule.ActionId + "' cannot be evaluated without body part, hit zone, damage type, hit direction, or reaction group.")
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

        private static int GetTriggerSpecificity(CharacterReactionRuleTrigger ruleTrigger, CharacterReactionRuleTrigger contextTrigger)
        {
            if (ruleTrigger == CharacterReactionRuleTrigger.Any)
                return 0;
            return ruleTrigger == contextTrigger ? 1 : -1;
        }

        private static int GetPressureOnlySpecificity(CharacterReactionRule rule)
        {
            int score = 0;
            if (rule.CurrentPressureBand.HasValue)
                score++;
            if (rule.IsDeath.HasValue)
                score++;
            if (rule.IsAirborne.HasValue)
                score++;
            if (rule.CurrentPhase.HasValue)
                score++;
            if (rule.CurrentActionCommitted.HasValue)
                score++;
            if (rule.CurrentActionInterruptible.HasValue)
                score++;
            return score;
        }

        private static int CompareCandidates(CharacterReactionRuleCandidate left, CharacterReactionRuleCandidate right)
        {
            int compare = right.TriggerSpecificity.CompareTo(left.TriggerSpecificity);
            if (compare != 0)
                return compare;
            compare = right.PressureOnlySpecificity.CompareTo(left.PressureOnlySpecificity);
            if (compare != 0)
                return compare;
            compare = right.Rule.Priority.CompareTo(left.Rule.Priority);
            if (compare != 0)
                return compare;
            compare = left.RuleOrder.CompareTo(right.RuleOrder);
            if (compare != 0)
                return compare;
            return string.CompareOrdinal(left.Rule.ActionId, right.Rule.ActionId);
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

        private readonly struct CharacterReactionRuleCandidate
        {
            public CharacterReactionRuleCandidate(
                CharacterReactionRule rule,
                int ruleOrder,
                int triggerSpecificity,
                int pressureOnlySpecificity)
            {
                Rule = rule;
                RuleOrder = ruleOrder;
                TriggerSpecificity = triggerSpecificity;
                PressureOnlySpecificity = pressureOnlySpecificity;
            }

            public CharacterReactionRule Rule { get; }
            public int RuleOrder { get; }
            public int TriggerSpecificity { get; }
            public int PressureOnlySpecificity { get; }
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
                            "PressureOnly reaction profile cannot use body part, hit zone, damage type, hit direction, or reaction group dimensions.")
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
