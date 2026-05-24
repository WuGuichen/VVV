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
        Lifecycle = 6,
        Hit = 7
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
            int priority = 0,
            string bodyPartId = "",
            string hitZoneId = "",
            string damageTypeId = "",
            CharacterHitDirection? hitDirection = null,
            int? minImpactForce = null,
            int? maxImpactForce = null,
            string reactionGroupId = "",
            bool requiresImpactForce = false)
        {
            if (hitDirection.HasValue && !Enum.IsDefined(typeof(CharacterHitDirection), hitDirection.Value))
                throw new ArgumentOutOfRangeException(nameof(hitDirection), "Hit direction is not defined.");
            if (minImpactForce.HasValue && minImpactForce.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(minImpactForce), "Minimum impact force cannot be negative.");
            if (maxImpactForce.HasValue && maxImpactForce.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(maxImpactForce), "Maximum impact force cannot be negative.");
            if (minImpactForce.HasValue && maxImpactForce.HasValue && minImpactForce.Value > maxImpactForce.Value)
                throw new ArgumentOutOfRangeException(nameof(minImpactForce), "Minimum impact force cannot exceed maximum impact force.");

            ActionId = actionId ?? string.Empty;
            Trigger = trigger;
            BodyPartId = bodyPartId ?? string.Empty;
            HitZoneId = hitZoneId ?? string.Empty;
            DamageTypeId = damageTypeId ?? string.Empty;
            HitDirection = hitDirection;
            MinImpactForce = minImpactForce;
            MaxImpactForce = maxImpactForce;
            ReactionGroupId = reactionGroupId ?? string.Empty;
            RequiresBodyPart = requiresBodyPart || !string.IsNullOrEmpty(BodyPartId);
            RequiresHitZone = requiresHitZone || !string.IsNullOrEmpty(HitZoneId);
            RequiresHitDirection = requiresHitDirection || HitDirection.HasValue;
            RequiresDamageType = requiresDamageType || !string.IsNullOrEmpty(DamageTypeId);
            RequiresReactionGroup = requiresReactionGroup || !string.IsNullOrEmpty(ReactionGroupId);
            RequiresImpactForce = requiresImpactForce || MinImpactForce.HasValue || MaxImpactForce.HasValue;
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
        public bool RequiresImpactForce { get; }
        public string BodyPartId { get; }
        public string HitZoneId { get; }
        public string DamageTypeId { get; }
        public CharacterHitDirection? HitDirection { get; }
        public int? MinImpactForce { get; }
        public int? MaxImpactForce { get; }
        public string ReactionGroupId { get; }
        public PressureBand? CurrentPressureBand { get; }
        public bool? IsDeath { get; }
        public bool? IsAirborne { get; }
        public CharacterActionPhaseKind? CurrentPhase { get; }
        public bool? CurrentActionCommitted { get; }
        public bool? CurrentActionInterruptible { get; }
        public int Priority { get; }
        public bool RequiresHitContext => RequiresBodyPart
            || RequiresHitZone
            || RequiresHitDirection
            || RequiresDamageType
            || RequiresReactionGroup
            || RequiresImpactForce;
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

            if (context.SourceKind == CharacterReactionContextSourceKind.Hit && !context.HasFullHitContext)
                return HitContextIncomplete(context);

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

                CharacterActionDiagnostic hitDiagnostic;
                bool hitDimensionMatched = MatchesFullHitDimensions(rule, context, out hitDiagnostic);
                if (!hitDimensionMatched)
                {
                    if (hitDiagnostic.Severity == CharacterActionDiagnosticSeverity.Error)
                    {
                        return new CharacterReactionSelectionResult(
                            accepted: false,
                            selectedActionId: string.Empty,
                            rejectCode: hitDiagnostic.Code,
                            diagnostics: new[] { hitDiagnostic });
                    }

                    diagnostics.Add(hitDiagnostic);
                    continue;
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
                    GetHitSpecificity(rule),
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
                        "Rule action '" + rule.ActionId + "' cannot be evaluated without body part, hit zone, damage type, hit direction, impact force, or reaction group.")
                });
        }

        private static CharacterReactionSelectionResult HitContextIncomplete(CharacterReactionContext context)
        {
            return new CharacterReactionSelectionResult(
                accepted: false,
                selectedActionId: string.Empty,
                rejectCode: CharacterActionDiagnosticCodes.ReactionContextIncomplete,
                diagnostics: new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.ReactionContextIncomplete,
                        "Hit reaction context is incomplete and cannot be used for fallback selection. Completeness=" + context.Completeness + ".")
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

        private static bool MatchesFullHitDimensions(
            CharacterReactionRule rule,
            CharacterReactionContext context,
            out CharacterActionDiagnostic diagnostic)
        {
            if (!rule.RequiresHitContext)
            {
                diagnostic = default;
                return true;
            }

            if (rule.RequiresBodyPart && string.IsNullOrEmpty(context.BodyPartId))
                return MissingRequiredHitField(rule, nameof(context.BodyPartId), out diagnostic);
            if (rule.RequiresHitZone && string.IsNullOrEmpty(context.HitZoneId))
                return MissingRequiredHitField(rule, nameof(context.HitZoneId), out diagnostic);
            if (rule.RequiresDamageType && string.IsNullOrEmpty(context.DamageTypeId))
                return MissingRequiredHitField(rule, nameof(context.DamageTypeId), out diagnostic);
            if (rule.RequiresHitDirection && context.HitDirection == CharacterHitDirection.Unknown)
                return MissingRequiredHitField(rule, nameof(context.HitDirection), out diagnostic);
            if (rule.RequiresReactionGroup && string.IsNullOrEmpty(context.ReactionGroupId))
                return MissingRequiredHitField(rule, nameof(context.ReactionGroupId), out diagnostic);

            if (!string.IsNullOrEmpty(rule.BodyPartId) && !string.Equals(rule.BodyPartId, context.BodyPartId, StringComparison.Ordinal))
                return HitDimensionSkipped(rule, "body part", out diagnostic);
            if (!string.IsNullOrEmpty(rule.HitZoneId) && !string.Equals(rule.HitZoneId, context.HitZoneId, StringComparison.Ordinal))
                return HitDimensionSkipped(rule, "hit zone", out diagnostic);
            if (!string.IsNullOrEmpty(rule.DamageTypeId) && !string.Equals(rule.DamageTypeId, context.DamageTypeId, StringComparison.Ordinal))
                return HitDimensionSkipped(rule, "damage type", out diagnostic);
            if (rule.HitDirection.HasValue && rule.HitDirection.Value != context.HitDirection)
                return HitDimensionSkipped(rule, "hit direction", out diagnostic);
            if (!string.IsNullOrEmpty(rule.ReactionGroupId) && !string.Equals(rule.ReactionGroupId, context.ReactionGroupId, StringComparison.Ordinal))
                return HitDimensionSkipped(rule, "reaction group", out diagnostic);
            if (rule.MinImpactForce.HasValue && context.ImpactForce < rule.MinImpactForce.Value)
                return HitDimensionSkipped(rule, "minimum impact force", out diagnostic);
            if (rule.MaxImpactForce.HasValue && context.ImpactForce > rule.MaxImpactForce.Value)
                return HitDimensionSkipped(rule, "maximum impact force", out diagnostic);

            diagnostic = default;
            return true;
        }

        private static bool MissingRequiredHitField(
            CharacterReactionRule rule,
            string fieldName,
            out CharacterActionDiagnostic diagnostic)
        {
            diagnostic = CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCodes.ReactionContextIncomplete,
                "Reaction rule '" + rule.ActionId + "' requires missing hit field '" + fieldName + "'.");
            return false;
        }

        private static bool HitDimensionSkipped(
            CharacterReactionRule rule,
            string dimensionName,
            out CharacterActionDiagnostic diagnostic)
        {
            diagnostic = CharacterActionDiagnostic.Info(
                CharacterActionDiagnosticCodes.ReactionRuleSkipped,
                "Skipped reaction rule '" + rule.ActionId + "' because full hit " + dimensionName + " did not match.");
            return false;
        }

        private static int GetTriggerSpecificity(CharacterReactionRuleTrigger ruleTrigger, CharacterReactionRuleTrigger contextTrigger)
        {
            if (ruleTrigger == CharacterReactionRuleTrigger.Any)
                return 0;
            return ruleTrigger == contextTrigger ? 1 : -1;
        }

        private static int GetHitSpecificity(CharacterReactionRule rule)
        {
            int score = 0;
            if (rule.RequiresBodyPart)
                score++;
            if (rule.RequiresHitZone)
                score++;
            if (rule.RequiresDamageType)
                score++;
            if (rule.RequiresHitDirection)
                score++;
            if (rule.RequiresImpactForce)
                score++;
            if (rule.RequiresReactionGroup)
                score++;
            return score;
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
            compare = right.HitSpecificity.CompareTo(left.HitSpecificity);
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
                case CharacterReactionContextSourceKind.Hit:
                    return CharacterReactionRuleTrigger.Hit;
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
                int hitSpecificity,
                int pressureOnlySpecificity)
            {
                Rule = rule;
                RuleOrder = ruleOrder;
                TriggerSpecificity = triggerSpecificity;
                HitSpecificity = hitSpecificity;
                PressureOnlySpecificity = pressureOnlySpecificity;
            }

            public CharacterReactionRule Rule { get; }
            public int RuleOrder { get; }
            public int TriggerSpecificity { get; }
            public int HitSpecificity { get; }
            public int PressureOnlySpecificity { get; }
        }
    }

    public static class CharacterReactionRuleValidator
    {
        public static CharacterActionDiagnostic[] ValidateProfileForCompleteness(
            CharacterReactionProfile profile,
            CharacterReactionContextCompleteness completeness)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (completeness == CharacterReactionContextCompleteness.Full)
                return Array.Empty<CharacterActionDiagnostic>();

            return ValidatePressureOnlyProfile(profile);
        }

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
                            "PressureOnly reaction profile cannot use body part, hit zone, damage type, hit direction, impact force, or reaction group dimensions.")
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

            if (rule.RequiresBodyPart && string.IsNullOrEmpty(context.BodyPartId))
                return MissingHitField(rule, nameof(context.BodyPartId));
            if (rule.RequiresHitZone && string.IsNullOrEmpty(context.HitZoneId))
                return MissingHitField(rule, nameof(context.HitZoneId));
            if (rule.RequiresDamageType && string.IsNullOrEmpty(context.DamageTypeId))
                return MissingHitField(rule, nameof(context.DamageTypeId));
            if (rule.RequiresHitDirection && context.HitDirection == CharacterHitDirection.Unknown)
                return MissingHitField(rule, nameof(context.HitDirection));
            if (rule.RequiresReactionGroup && string.IsNullOrEmpty(context.ReactionGroupId))
                return MissingHitField(rule, nameof(context.ReactionGroupId));

            return Array.Empty<CharacterActionDiagnostic>();
        }

        private static CharacterActionDiagnostic[] MissingHitField(CharacterReactionRule rule, string fieldName)
        {
            return new[]
            {
                CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.ReactionContextIncomplete,
                    "Reaction rule '" + rule.ActionId + "' requires missing hit field '" + fieldName + "'.")
            };
        }
    }
}
