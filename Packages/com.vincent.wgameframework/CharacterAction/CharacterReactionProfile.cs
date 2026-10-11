using System;
using System.Collections.Generic;
using MxFramework.Gameplay;

namespace MxFramework.CharacterAction
{
    public enum CharacterReactionRuleTrigger
    {
        Any = 0,
        CohesionBreak = 1,
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
            CharacterActionId actionId,
            CharacterReactionRuleTrigger trigger,
            bool requiresBodyPart = false,
            bool requiresHitZone = false,
            bool requiresHitDirection = false,
            bool requiresDamageType = false,
            bool requiresReactionGroup = false,
            PressureBand? currentScatterPressureBand = null,
            bool? isDeath = null,
            bool? isAirborne = null,
            CharacterActionPhaseKind? currentPhase = null,
            bool? currentActionCommitted = null,
            bool? currentActionInterruptible = null,
            int priority = 0,
            int bodyPartId = 0,
            int hitZoneId = 0,
            int damageTypeId = 0,
            CharacterHitDirection? hitDirection = null,
            int? minImpactForce = null,
            int? maxImpactForce = null,
            int reactionGroupId = 0,
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

            ActionId = actionId;
            Trigger = trigger;
            BodyPartId = bodyPartId;
            HitZoneId = hitZoneId;
            DamageTypeId = damageTypeId;
            HitDirection = hitDirection;
            MinImpactForce = minImpactForce;
            MaxImpactForce = maxImpactForce;
            ReactionGroupId = reactionGroupId;
            RequiresBodyPart = requiresBodyPart || (BodyPartId > 0);
            RequiresHitZone = requiresHitZone || (HitZoneId > 0);
            RequiresHitDirection = requiresHitDirection || HitDirection.HasValue;
            RequiresDamageType = requiresDamageType || (DamageTypeId > 0);
            RequiresReactionGroup = requiresReactionGroup || !(ReactionGroupId <= 0);
            RequiresImpactForce = requiresImpactForce || MinImpactForce.HasValue || MaxImpactForce.HasValue;
            CurrentScatterPressureBand = currentScatterPressureBand;
            IsDeath = isDeath;
            IsAirborne = isAirborne;
            CurrentPhase = currentPhase;
            CurrentActionCommitted = currentActionCommitted;
            CurrentActionInterruptible = currentActionInterruptible;
            Priority = priority;
        }

        public CharacterActionId ActionId { get; }
        public CharacterReactionRuleTrigger Trigger { get; }
        public bool RequiresBodyPart { get; }
        public bool RequiresHitZone { get; }
        public bool RequiresHitDirection { get; }
        public bool RequiresDamageType { get; }
        public bool RequiresReactionGroup { get; }
        public bool RequiresImpactForce { get; }
        public int BodyPartId { get; }
        public int HitZoneId { get; }
        public int DamageTypeId { get; }
        public CharacterHitDirection? HitDirection { get; }
        public int? MinImpactForce { get; }
        public int? MaxImpactForce { get; }
        public int ReactionGroupId { get; }
        public PressureBand? CurrentScatterPressureBand { get; }
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
        public CharacterReactionProfile(
            string stableId,
            CharacterReactionRule[] rules,
            CharacterActionId defaultActionId = default)
        {
            StableId = stableId ?? string.Empty;
            Rules = rules ?? Array.Empty<CharacterReactionRule>();
            DefaultActionId = defaultActionId;
        }

        public string StableId { get; }
        public CharacterReactionRule[] Rules { get; }
        public CharacterActionId DefaultActionId { get; }
    }

    public readonly struct CharacterReactionSelectionResult
    {
        public CharacterReactionSelectionResult(
            bool accepted,
            CharacterActionId selectedActionId,
            CharacterActionDiagnosticCode rejectCode,
            CharacterActionDiagnostic[] diagnostics)
        {
            Accepted = accepted;
            SelectedActionId = selectedActionId;
            RejectCodeId = rejectCode;
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
        }

        public bool Accepted { get; }
        public CharacterActionId SelectedActionId { get; }
        public CharacterActionDiagnosticCode RejectCodeId { get; }
        public string RejectCode => CharacterActionDiagnosticCodeText.Get(RejectCodeId);
        public CharacterActionDiagnostic[] Diagnostics { get; }
    }

    /// <summary>Noalloc reaction selection result for runtime warm paths.</summary>
    /// <remarks>
    /// NoAlloc. Detailed skipped-rule diagnostics are intentionally omitted; use
    /// <see cref="CharacterReactionSelector.Select"/> for alloc-by-design diagnostic reporting.
    /// </remarks>
    public readonly struct CharacterReactionSelectionFastResult
    {
        public CharacterReactionSelectionFastResult(
            bool accepted,
            CharacterActionId selectedActionId,
            CharacterActionDiagnosticCode rejectCode,
            bool fallbackUsed)
        {
            Accepted = accepted;
            SelectedActionId = selectedActionId;
            RejectCodeId = rejectCode;
            FallbackUsed = fallbackUsed;
        }

        public bool Accepted { get; }
        public CharacterActionId SelectedActionId { get; }
        public CharacterActionDiagnosticCode RejectCodeId { get; }
        public string RejectCode => CharacterActionDiagnosticCodeText.Get(RejectCodeId);
        public bool FallbackUsed { get; }
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
            CharacterReactionRule missingHitContextRule = null;
            for (int i = 0; i < profile.Rules.Length; i++)
            {
                CharacterReactionRule rule = profile.Rules[i];
                if (rule == null || !Matches(rule.Trigger, trigger))
                {
                    if (rule != null)
                    {
                        diagnostics.Add(CharacterActionDiagnostic.Info(
                            CharacterActionDiagnosticCode.ReactionRuleSkipped,
                            "Skipped reaction rule '" + rule.ActionId + "' because trigger '" + rule.Trigger + "' does not match context trigger '" + trigger + "'."));
                    }

                    continue;
                }

                if (!MatchesPressureOnlyDimensions(rule, context))
                {
                    diagnostics.Add(CharacterActionDiagnostic.Info(
                        CharacterActionDiagnosticCode.ReactionRuleSkipped,
                        "Skipped reaction rule '" + rule.ActionId + "' because PressureOnly dimensions did not match."));
                    continue;
                }

                CharacterActionDiagnostic hitDiagnostic;
                bool hitDimensionMatched = MatchesFullHitDimensions(rule, context, out hitDiagnostic);
                if (!hitDimensionMatched)
                {
                    if (hitDiagnostic.Severity == CharacterActionDiagnosticSeverity.Error)
                    {
                        if (context.SourceKind == CharacterReactionContextSourceKind.Hit && !context.HasFullHitContext)
                        {
                            if (missingHitContextRule == null)
                                missingHitContextRule = rule;

                            diagnostics.Add(CharacterActionDiagnostic.Info(
                                CharacterActionDiagnosticCode.ReactionRuleSkipped,
                                "Skipped reaction rule '" + rule.ActionId + "' because a required hit field is missing from " + context.Completeness + " context."));
                            continue;
                        }

                        return new CharacterReactionSelectionResult(
                            accepted: false,
                            selectedActionId: CharacterActionId.None,
                            rejectCode: hitDiagnostic.CodeId,
                            diagnostics: new[] { hitDiagnostic });
                    }

                    diagnostics.Add(hitDiagnostic);
                    continue;
                }

                if (!rule.ActionId.IsValid)
                {
                    return new CharacterReactionSelectionResult(
                        accepted: false,
                        selectedActionId: CharacterActionId.None,
                        rejectCode: CharacterActionDiagnosticCode.ReactionRuleNoTarget,
                        diagnostics: new[]
                        {
                            CharacterActionDiagnostic.Error(
                                CharacterActionDiagnosticCode.ReactionRuleNoTarget,
                                "Reaction rule matched but did not provide an action id.")
                        });
                }

                candidates.Add(new CharacterReactionRuleCandidate(
                    rule,
                    i,
                    GetTriggerSpecificity(rule.Trigger, trigger),
                    GetHitSpecificity(rule),
                    GetPressureOnlySpecificity(rule),
                    false));
            }

            if (candidates.Count > 0)
            {
                candidates.Sort(CompareCandidates);
                CharacterReactionRuleCandidate selected = candidates[0];
                if (selected.RequiresMissingHitContext)
                    return HitContextRequired(selected.Rule, context);

                for (int i = 1; i < candidates.Count; i++)
                {
                    diagnostics.Add(CharacterActionDiagnostic.Info(
                        CharacterActionDiagnosticCode.ReactionRuleSkipped,
                        "Skipped reaction rule '" + candidates[i].Rule.ActionId + "' because rule '" + selected.Rule.ActionId + "' ranked higher."));
                }

                diagnostics.Add(CharacterActionDiagnostic.Info(
                    CharacterActionDiagnosticCode.ReactionRuleMatched,
                    "Matched reaction rule '" + selected.Rule.ActionId + "'."));

                return new CharacterReactionSelectionResult(
                    accepted: true,
                    selectedActionId: selected.Rule.ActionId,
                    rejectCode: CharacterActionDiagnosticCode.None,
                    diagnostics: diagnostics.ToArray());
            }

            if (context.SourceKind == CharacterReactionContextSourceKind.Hit && !context.HasFullHitContext)
            {
                if (missingHitContextRule != null)
                    return HitContextRequired(missingHitContextRule, context);

                return HitContextIncomplete(context);
            }

            if (profile.DefaultActionId.IsValid)
            {
                diagnostics.Add(CharacterActionDiagnostic.Info(
                    CharacterActionDiagnosticCode.ReactionFallbackUsed,
                    "Reaction profile used fallback action '" + profile.DefaultActionId + "'."));

                return new CharacterReactionSelectionResult(
                    accepted: true,
                    selectedActionId: profile.DefaultActionId,
                    rejectCode: CharacterActionDiagnosticCode.None,
                    diagnostics: diagnostics.ToArray());
            }

            diagnostics.Add(CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCode.ReactionRuleNoTarget,
                "Reaction profile has no matching rule and no default action id."));

            return new CharacterReactionSelectionResult(
                accepted: false,
                selectedActionId: CharacterActionId.None,
                rejectCode: CharacterActionDiagnosticCode.ReactionRuleNoTarget,
                diagnostics: diagnostics.ToArray());
        }

        /// <summary>Selects a reaction action without allocating skipped-rule diagnostics.</summary>
        /// <remarks>NoAlloc after the supplied profile/context are initialized.</remarks>
        public static CharacterReactionSelectionFastResult SelectNoAlloc(
            CharacterReactionProfile profile,
            CharacterReactionContext context)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            CharacterReactionRuleTrigger trigger = ToTrigger(context.SourceKind);
            CharacterReactionRule missingHitContextRule = null;
            CharacterReactionRule selected = null;
            int selectedOrder = 0;
            int selectedTriggerSpecificity = 0;
            int selectedHitSpecificity = 0;
            int selectedPressureSpecificity = 0;

            for (int i = 0; i < profile.Rules.Length; i++)
            {
                CharacterReactionRule rule = profile.Rules[i];
                if (rule == null || !Matches(rule.Trigger, trigger))
                    continue;

                if (!MatchesPressureOnlyDimensions(rule, context))
                    continue;

                CharacterActionDiagnosticCode rejectCode;
                bool missingRequiredHitContext;
                if (!MatchesFullHitDimensionsNoAlloc(rule, context, out rejectCode, out missingRequiredHitContext))
                {
                    if (missingRequiredHitContext)
                    {
                        if (context.SourceKind == CharacterReactionContextSourceKind.Hit && !context.HasFullHitContext)
                        {
                            if (missingHitContextRule == null)
                                missingHitContextRule = rule;
                            continue;
                        }

                        return new CharacterReactionSelectionFastResult(
                            accepted: false,
                            selectedActionId: CharacterActionId.None,
                            rejectCode: rejectCode,
                            fallbackUsed: false);
                    }

                    continue;
                }

                if (!rule.ActionId.IsValid)
                {
                    return new CharacterReactionSelectionFastResult(
                        accepted: false,
                        selectedActionId: CharacterActionId.None,
                        rejectCode: CharacterActionDiagnosticCode.ReactionRuleNoTarget,
                        fallbackUsed: false);
                }

                int triggerSpecificity = GetTriggerSpecificity(rule.Trigger, trigger);
                int hitSpecificity = GetHitSpecificity(rule);
                int pressureSpecificity = GetPressureOnlySpecificity(rule);
                if (!IsCandidateBetter(
                        rule,
                        i,
                        triggerSpecificity,
                        hitSpecificity,
                        pressureSpecificity,
                        selected,
                        selectedOrder,
                        selectedTriggerSpecificity,
                        selectedHitSpecificity,
                        selectedPressureSpecificity))
                {
                    continue;
                }

                selected = rule;
                selectedOrder = i;
                selectedTriggerSpecificity = triggerSpecificity;
                selectedHitSpecificity = hitSpecificity;
                selectedPressureSpecificity = pressureSpecificity;
            }

            if (selected != null)
            {
                return new CharacterReactionSelectionFastResult(
                    accepted: true,
                    selectedActionId: selected.ActionId,
                    rejectCode: CharacterActionDiagnosticCode.None,
                    fallbackUsed: false);
            }

            if (context.SourceKind == CharacterReactionContextSourceKind.Hit && !context.HasFullHitContext)
            {
                return new CharacterReactionSelectionFastResult(
                    accepted: false,
                    selectedActionId: CharacterActionId.None,
                    rejectCode: missingHitContextRule != null
                        ? CharacterActionDiagnosticCode.ReactionRuleRequiresHitContext
                        : CharacterActionDiagnosticCode.ReactionContextIncomplete,
                    fallbackUsed: false);
            }

            if (profile.DefaultActionId.IsValid)
            {
                return new CharacterReactionSelectionFastResult(
                    accepted: true,
                    selectedActionId: profile.DefaultActionId,
                    rejectCode: CharacterActionDiagnosticCode.None,
                    fallbackUsed: true);
            }

            return new CharacterReactionSelectionFastResult(
                accepted: false,
                selectedActionId: CharacterActionId.None,
                rejectCode: CharacterActionDiagnosticCode.ReactionRuleNoTarget,
                fallbackUsed: false);
        }

        private static CharacterReactionSelectionResult HitContextRequired(
            CharacterReactionRule rule,
            CharacterReactionContext context)
        {
            return new CharacterReactionSelectionResult(
                accepted: false,
                selectedActionId: CharacterActionId.None,
                rejectCode: CharacterActionDiagnosticCode.ReactionRuleRequiresHitContext,
                diagnostics: new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.ReactionRuleRequiresHitContext,
                        "Reaction rule requires full hit context but only " + context.Completeness + " context is available."),
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.ReactionContextIncomplete,
                        "Rule action '" + rule.ActionId + "' cannot be evaluated without body part, hit zone, damage type, hit direction, impact force, or reaction group.")
                });
        }

        private static CharacterReactionSelectionResult HitContextIncomplete(CharacterReactionContext context)
        {
            return new CharacterReactionSelectionResult(
                accepted: false,
                selectedActionId: CharacterActionId.None,
                rejectCode: CharacterActionDiagnosticCode.ReactionContextIncomplete,
                diagnostics: new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.ReactionContextIncomplete,
                        "Hit reaction context is incomplete and cannot be used for fallback selection. Completeness=" + context.Completeness + ".")
                });
        }

        private static bool Matches(CharacterReactionRuleTrigger ruleTrigger, CharacterReactionRuleTrigger contextTrigger)
        {
            return ruleTrigger == CharacterReactionRuleTrigger.Any || ruleTrigger == contextTrigger;
        }

        private static bool MatchesPressureOnlyDimensions(CharacterReactionRule rule, CharacterReactionContext context)
        {
            return (!rule.CurrentScatterPressureBand.HasValue || rule.CurrentScatterPressureBand.Value == context.CurrentScatterPressureBand)
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

            if (rule.RequiresBodyPart && (context.BodyPartId <= 0))
                return MissingRequiredHitField(rule, nameof(context.BodyPartId), out diagnostic);
            if (rule.RequiresHitZone && (context.HitZoneId <= 0))
                return MissingRequiredHitField(rule, nameof(context.HitZoneId), out diagnostic);
            if (rule.RequiresDamageType && (context.DamageTypeId <= 0))
                return MissingRequiredHitField(rule, nameof(context.DamageTypeId), out diagnostic);
            if (rule.RequiresHitDirection && context.HitDirection == CharacterHitDirection.Unknown)
                return MissingRequiredHitField(rule, nameof(context.HitDirection), out diagnostic);
            if (rule.RequiresReactionGroup && (context.ReactionGroupId <= 0))
                return MissingRequiredHitField(rule, nameof(context.ReactionGroupId), out diagnostic);

            if ((rule.BodyPartId > 0) && rule.BodyPartId != context.BodyPartId)
                return HitDimensionSkipped(rule, "body part", out diagnostic);
            if ((rule.HitZoneId > 0) && rule.HitZoneId != context.HitZoneId)
                return HitDimensionSkipped(rule, "hit zone", out diagnostic);
            if ((rule.DamageTypeId > 0) && rule.DamageTypeId != context.DamageTypeId)
                return HitDimensionSkipped(rule, "damage type", out diagnostic);
            if (rule.HitDirection.HasValue && rule.HitDirection.Value != context.HitDirection)
                return HitDimensionSkipped(rule, "hit direction", out diagnostic);
            if (!(rule.ReactionGroupId <= 0) && !(rule.ReactionGroupId == context.ReactionGroupId))
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
                CharacterActionDiagnosticCode.ReactionContextIncomplete,
                "Reaction rule '" + rule.ActionId + "' requires missing hit field '" + fieldName + "'.");
            return false;
        }

        private static bool HitDimensionSkipped(
            CharacterReactionRule rule,
            string dimensionName,
            out CharacterActionDiagnostic diagnostic)
        {
            diagnostic = CharacterActionDiagnostic.Info(
                CharacterActionDiagnosticCode.ReactionRuleSkipped,
                "Skipped reaction rule '" + rule.ActionId + "' because full hit " + dimensionName + " did not match.");
            return false;
        }

        private static bool MatchesFullHitDimensionsNoAlloc(
            CharacterReactionRule rule,
            CharacterReactionContext context,
            out CharacterActionDiagnosticCode rejectCode,
            out bool missingRequiredHitContext)
        {
            rejectCode = CharacterActionDiagnosticCode.None;
            missingRequiredHitContext = false;
            if (!rule.RequiresHitContext)
                return true;

            if (rule.RequiresBodyPart && (context.BodyPartId <= 0))
                return MissingRequiredHitFieldNoAlloc(out rejectCode, out missingRequiredHitContext);
            if (rule.RequiresHitZone && (context.HitZoneId <= 0))
                return MissingRequiredHitFieldNoAlloc(out rejectCode, out missingRequiredHitContext);
            if (rule.RequiresDamageType && (context.DamageTypeId <= 0))
                return MissingRequiredHitFieldNoAlloc(out rejectCode, out missingRequiredHitContext);
            if (rule.RequiresHitDirection && context.HitDirection == CharacterHitDirection.Unknown)
                return MissingRequiredHitFieldNoAlloc(out rejectCode, out missingRequiredHitContext);
            if (rule.RequiresReactionGroup && (context.ReactionGroupId <= 0))
                return MissingRequiredHitFieldNoAlloc(out rejectCode, out missingRequiredHitContext);

            if ((rule.BodyPartId > 0) && rule.BodyPartId != context.BodyPartId)
                return false;
            if ((rule.HitZoneId > 0) && rule.HitZoneId != context.HitZoneId)
                return false;
            if ((rule.DamageTypeId > 0) && rule.DamageTypeId != context.DamageTypeId)
                return false;
            if (rule.HitDirection.HasValue && rule.HitDirection.Value != context.HitDirection)
                return false;
            if (!(rule.ReactionGroupId <= 0) && !(rule.ReactionGroupId == context.ReactionGroupId))
                return false;
            if (rule.MinImpactForce.HasValue && context.ImpactForce < rule.MinImpactForce.Value)
                return false;
            if (rule.MaxImpactForce.HasValue && context.ImpactForce > rule.MaxImpactForce.Value)
                return false;

            return true;
        }

        private static bool MissingRequiredHitFieldNoAlloc(
            out CharacterActionDiagnosticCode rejectCode,
            out bool missingRequiredHitContext)
        {
            rejectCode = CharacterActionDiagnosticCode.ReactionContextIncomplete;
            missingRequiredHitContext = true;
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
            if (rule.CurrentScatterPressureBand.HasValue)
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
            return left.Rule.ActionId.CompareTo(right.Rule.ActionId);
        }

        private static bool IsCandidateBetter(
            CharacterReactionRule candidate,
            int candidateOrder,
            int candidateTriggerSpecificity,
            int candidateHitSpecificity,
            int candidatePressureSpecificity,
            CharacterReactionRule selected,
            int selectedOrder,
            int selectedTriggerSpecificity,
            int selectedHitSpecificity,
            int selectedPressureSpecificity)
        {
            if (selected == null)
                return true;

            if (candidateTriggerSpecificity != selectedTriggerSpecificity)
                return candidateTriggerSpecificity > selectedTriggerSpecificity;
            if (candidateHitSpecificity != selectedHitSpecificity)
                return candidateHitSpecificity > selectedHitSpecificity;
            if (candidatePressureSpecificity != selectedPressureSpecificity)
                return candidatePressureSpecificity > selectedPressureSpecificity;
            if (candidate.Priority != selected.Priority)
                return candidate.Priority > selected.Priority;
            if (candidateOrder != selectedOrder)
                return candidateOrder < selectedOrder;
            return candidate.ActionId.CompareTo(selected.ActionId) < 0;
        }

        private static CharacterReactionRuleTrigger ToTrigger(CharacterReactionContextSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case CharacterReactionContextSourceKind.CohesionBreak:
                    return CharacterReactionRuleTrigger.CohesionBreak;
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
                int pressureOnlySpecificity,
                bool requiresMissingHitContext)
            {
                Rule = rule;
                RuleOrder = ruleOrder;
                TriggerSpecificity = triggerSpecificity;
                HitSpecificity = hitSpecificity;
                PressureOnlySpecificity = pressureOnlySpecificity;
                RequiresMissingHitContext = requiresMissingHitContext;
            }

            public CharacterReactionRule Rule { get; }
            public int RuleOrder { get; }
            public int TriggerSpecificity { get; }
            public int HitSpecificity { get; }
            public int PressureOnlySpecificity { get; }
            public bool RequiresMissingHitContext { get; }
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
                            CharacterActionDiagnosticCode.ReactionRuleRequiresHitContext,
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
                        CharacterActionDiagnosticCode.ReactionRuleRequiresHitContext,
                        "Reaction rule requires hit context that is not available in " + context.Completeness + " context.")
                };
            }

            if (rule.RequiresBodyPart && (context.BodyPartId <= 0))
                return MissingHitField(rule, nameof(context.BodyPartId));
            if (rule.RequiresHitZone && (context.HitZoneId <= 0))
                return MissingHitField(rule, nameof(context.HitZoneId));
            if (rule.RequiresDamageType && (context.DamageTypeId <= 0))
                return MissingHitField(rule, nameof(context.DamageTypeId));
            if (rule.RequiresHitDirection && context.HitDirection == CharacterHitDirection.Unknown)
                return MissingHitField(rule, nameof(context.HitDirection));
            if (rule.RequiresReactionGroup && (context.ReactionGroupId <= 0))
                return MissingHitField(rule, nameof(context.ReactionGroupId));

            return Array.Empty<CharacterActionDiagnostic>();
        }

        private static CharacterActionDiagnostic[] MissingHitField(CharacterReactionRule rule, string fieldName)
        {
            return new[]
            {
                CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.ReactionContextIncomplete,
                    "Reaction rule '" + rule.ActionId + "' requires missing hit field '" + fieldName + "'.")
            };
        }
    }
}
