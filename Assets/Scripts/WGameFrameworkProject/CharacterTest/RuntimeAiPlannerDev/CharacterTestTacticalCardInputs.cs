using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Consumer selection over complete measurement evidence; never a replacement measurement.</summary>
    internal sealed class CharacterTestTacticalActionRange
    {
        internal const string SelectionPolicy = "first-closed-hit-run-v1";
        internal CharacterTestTacticalActionRange(CharacterActionSpatialSummary evidence, int preferredMillimeters)
        { Evidence = evidence; PreferredMillimeters = preferredMillimeters; }
        internal CharacterActionSpatialSummary Evidence { get; }
        internal int PreferredMillimeters { get; }
        internal double? MinDistance => Evidence.Intervals[0].FirstHitMillimeters / 1000d;
        internal double? MaxDistance => Evidence.Intervals[0].LastHitMillimeters / 1000d;
        internal double? OptimalDistance => PreferredMillimeters / 1000d;
        internal CharacterActionTacticalSector Sector => Evidence.Sector;
        internal bool IgnoresFurtherIntervals => Evidence.Intervals.Count > 1;
    }

    /// <summary>Validated, low-frequency inputs for a single primary-attack tactical card.</summary>
    /// <remarks>LowFreqAlloc. Preparation reads the current loaded providers and derived store; never call from Tick.</remarks>
    internal sealed partial class CharacterTestTacticalCardInputs
    {
        private readonly JToken _preparedActionContent;
        private readonly JToken _preparedStepBackContent;
        private JToken _preparedStepForwardContent;
        internal CharacterActionConfig StepForwardAction { get; private set; }
        internal string StepForwardContentHash { get; private set; }
        private void SetStepForward(CharacterActionConfig action, string hash)
        { StepForwardAction = action; StepForwardContentHash = hash; _preparedStepForwardContent = action == null ? null : SnapshotAction(action); }

        private CharacterTestTacticalCardInputs(CharacterActionConfig action, CharacterActionTacticalMetrics metrics,
            string fingerprint, string actionSetId, int sourceCharacterId, bool testActionOverride = false,
            CharacterActionConfig stepBack = null, string stepBackHash = null,
            CharacterActionSpatialSummary spatialSummary = null, int summarySourceCharacterId = 0, string summaryIssue = null,
            CharacterTestTacticalInputRequirements requirements = CharacterTestTacticalInputRequirements.All,
            CharacterActionProjectileReferenceCatalog combat = null)
        {
            Requirements = requirements;
            Action = action;
            Metrics = metrics;
            _dependencyFingerprint = fingerprint;
            ActionSetId = actionSetId;
            SourceCharacterId = sourceCharacterId;
            TestActionOverride = testActionOverride;
            StartupEndFrame = metrics?.StartupEndFrame ?? 0;
            DurationFrames = metrics?.TotalDurationFrames.Value ?? 0;
            _preparedActionContent = action == null ? null : SnapshotAction(action);
            StepBackAction = stepBack; StepBackContentHash = stepBackHash;
            SpatialSummary = spatialSummary ?? metrics?.SpatialSummary;
            if (SpatialSummary != null && TrySelectRange(SpatialSummary, out var selected, out _)) SelectedRange = selected;
            SummarySourceCharacterId = summarySourceCharacterId > 0 ? summarySourceCharacterId : sourceCharacterId;
            SummaryIssue = summaryIssue ?? string.Empty;
            _preparedStepBackContent = stepBack == null ? null : SnapshotAction(stepBack);
        }

        internal CharacterTestTacticalInputRequirements Requirements { get; }
        internal static CharacterTestTacticalCardInputs Unbound(int character)
            => new CharacterTestTacticalCardInputs(null, null, "tactical-unbound-v1:" + character, string.Empty, character, requirements: CharacterTestTacticalInputRequirements.None);
        internal CharacterActionConfig Action { get; }
        internal CharacterActionConfig StepBackAction { get; }
        internal string StepBackContentHash { get; }
        internal CharacterActionConfig[] CompilationActions
        {
            get
            {
                var list = new List<CharacterActionConfig>();
                if (_slotInputs != null)
                {
                    foreach (var v in _slotInputs.Values)
                        if (v?.CompilationActions != null)
                            list.AddRange(v.CompilationActions);
                }
                if (Action != null) list.Add(Action);
                if (StepBackAction != null) list.Add(StepBackAction);
                if (StepForwardAction != null) list.Add(StepForwardAction);
                return list.GroupBy(a => a.ActionId.Value).Select(g => g.First()).ToArray();
            }
        }
        internal CharacterActionConfig ResolveSlot(string resolver) => _slotInputs != null && _slotInputs.TryGetValue(resolver, out var slot) ? slot.Action
            : resolver == "lightAttackOpener" && Action != null ? Action
            : resolver == "stepBack" && StepBackAction != null ? StepBackAction
            : resolver == "stepForward" && StepForwardAction != null ? StepForwardAction
            : resolver == "ordinaryGuard" && Action?.Category == CharacterActionCategory.Guard ? Action
            : throw new InvalidOperationException("ActionSlot: 当前装配没有可用的 " + resolver + " 动作。");
        internal CharacterActionTacticalMetrics Metrics { get; }
        internal CharacterActionSpatialSummary SpatialSummary { get; }
        internal CharacterTestTacticalActionRange SelectedRange { get; }
        internal int SummarySourceCharacterId { get; }
        internal bool UsesDefaultSummary => SummarySourceCharacterId != SourceCharacterId;
        internal string SummaryIssue { get; }
        private readonly string _dependencyFingerprint;
        private string _simulationDependencyFingerprint;
        internal string DependencyFingerprint => _simulationDependencyFingerprint ?? _dependencyFingerprint;
        internal int SimulationTickRate { get; private set; } = CharacterTestDeterministicUnits.DefaultTickRate;
        internal CharacterTestTacticalCardInputs WithSimulationTickRate(int rate)
        {
            if (rate <= 0 || rate > (int.MaxValue - 2) / 2) throw new ArgumentOutOfRangeException(nameof(rate));
            var copy = (CharacterTestTacticalCardInputs)MemberwiseClone(); copy.SimulationTickRate = rate;
            copy._simulationDependencyFingerprint = rate == CharacterTestDeterministicUnits.DefaultTickRate ? null
                : _dependencyFingerprint + "|simulation-tick-rate:" + rate.ToString(CultureInfo.InvariantCulture);
            return copy;
        }
        internal int StartupEndFrame { get; }
        internal int DurationFrames { get; }
        internal string ActionSetId { get; }
        internal int SourceCharacterId { get; }
        internal bool TestActionOverride { get; }
        internal string SlotSource => TestActionOverride ? "AttackPrimary/test-selection" : "AttackPrimary/LightAttack/sequence-opener";

        /// <summary>Rejects mutable registry content that no longer matches the prepared timing and measurement.</summary>
        /// <remarks>LowFreqAlloc. Call immediately before compiling; never call from Tick or a live condition.</remarks>
        internal bool TryValidatePreparedAction(out string issue)
        {
            issue = string.Empty;
            if (StepForwardAction != null && !JToken.DeepEquals(_preparedStepForwardContent, SnapshotAction(StepForwardAction)))
            { issue = "Action:PreparedStepForwardChanged"; return false; }
            if (_slotInputs != null)
            {
                foreach (var input in _slotInputs.Values)
                    if (!input.TryValidatePreparedAction(out issue)) return false;
                return true;
            }
            try
            {
                // DeepEquals compares structured values, including every nested track/phase array,
                // without making identity depend on object-property enumeration order.
                if (Action != null && !JToken.DeepEquals(_preparedActionContent, SnapshotAction(Action)))
                    return Fail("Action:PreparedContentChanged", out issue);
                if (StepBackAction != null && !JToken.DeepEquals(_preparedStepBackContent, SnapshotAction(StepBackAction)))
                    return Fail("Action:PreparedStepBackChanged", out issue);
                return true;
            }
            catch (JsonException)
            {
                return Fail("Action:PreparedContentUnavailable", out issue);
            }
        }

        private static JToken SnapshotAction(CharacterActionConfig action)
            => JToken.FromObject(action, JsonSerializer.Create(new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
                ReferenceLoopHandling = ReferenceLoopHandling.Error
            }));

        internal static bool TryPrepare(CharacterTestCharacterActionRuntime runtime, CharacterTestCharacterEntity source,
            CharacterTestCharacterPhysicsRuntime physics, ICharacterTestBakedWeaponTraceSource bakes,
            ICharacterTestWeaponTraceBindingResolver bindings, CharacterActionTacticalMetricsStore store,
            CharacterActionSpatialMeasurement measurement, out CharacterTestTacticalCardInputs inputs, out string issue,
            int testActionId = 0, CharacterTestTacticalInputRequirements requirements = CharacterTestTacticalInputRequirements.All,
            CharacterActionProjectileReferenceCatalog combat = null)
        {
            inputs = null;
            issue = string.Empty;
            if ((requirements & CharacterTestTacticalInputRequirements.SpatialSummary) != 0) requirements |= CharacterTestTacticalInputRequirements.PrimaryAction;
            if ((requirements & CharacterTestTacticalInputRequirements.PrimaryAction) == 0) testActionId = 0;
            if (requirements == CharacterTestTacticalInputRequirements.None && source != null) { inputs = Unbound(source.Identity.CharacterId); return true; }
            if (runtime == null || source == null || physics == null || store == null || measurement == null)
                return Fail("CurrentInputsMissing", out issue);
            // The basic-behaviour mapping is provenance only; it does not grant tactical actions.
            runtime.TryGetEntityActionSet(source.Id, out CharacterActionSetConfig actionSet);
            CharacterActionConfig action = null;
            bool primary = (requirements & CharacterTestTacticalInputRequirements.PrimaryAction) != 0;
            bool spatial = (requirements & CharacterTestTacticalInputRequirements.SpatialSummary) != 0;
            CharacterActionTacticalMetricsLoadResult result = default;
            if (primary || spatial)
            {
                if (!TryResolveSelectedAction(actionSet, runtime.Registry.Actions, testActionId, out action, out issue) || !TryValidateAction(action, out issue)) return false;
                if (spatial && action.Category == CharacterActionCategory.Guard)
                    return Fail("Action:GuardHasNoAttackRange", out issue);
                if (spatial && action.Category != CharacterActionCategory.Dodge)
                {
                    result = store.QueryForTacticalCard(new CharacterActionTacticalInput(action, source, physics, bakes, bindings, combat), measurement);
                    if (result.Status != CharacterActionTacticalMetricsLoadStatus.Valid || result.Metrics == null)
                        return Fail("ActionSummary:" + result.Status + ":" + result.Issue, out issue);
                    if (!TryValidateSummary(result.SpatialSummary, out issue)) return false;
                }
                else
                {
                    if (!CharacterActionContentHashUtility.TryCompute(action, source, physics, bakes, bindings, out string actionHash, out issue)) return false;
                    result = new CharacterActionTacticalMetricsLoadResult(CharacterActionTacticalMetricsLoadStatus.Valid, string.Empty, new CharacterActionTacticalMetrics(action, actionHash));
                }
            }
            CharacterActionConfig stepBack = null;
            if ((requirements & CharacterTestTacticalInputRequirements.StepBack) != 0) TryResolveStepBack(runtime.Registry.Actions, out stepBack);
            string stepBackHash = null;
            if (stepBack != null && !CharacterActionContentHashUtility.TryCompute(stepBack, source, physics, bakes, bindings, out stepBackHash, out _))
                stepBack = null;
            CharacterActionConfig stepForward = null;
            string stepForwardHash = null;
            if ((requirements & CharacterTestTacticalInputRequirements.StepForward) != 0)
            {
                TryResolveStepForward(runtime.Registry.Actions, out stepForward);
                if (stepForward != null && !CharacterActionContentHashUtility.TryCompute(stepForward, source, physics, bakes, bindings, out stepForwardHash, out _))
                    stepForward = null;
            }
            // Only the resolved functional binding matters. ActionSet/equipment labels, sequence tails,
            // unrelated command slots and priorities that leave this opener unchanged are not dependencies.
            var content = new JObject
            {
                ["schema"] = "tactical-primary-opener-input-v1",
                ["slot"] = "AttackPrimary", ["intent"] = "LightAttack", ["selection"] = testActionId > 0 ? "test-selection" : "sequence-opener",
                ["actionId"] = action?.ActionId.Value ?? 0,
                ["actionContentHash"] = result.Metrics?.ActionContentHash,
                ["borrowedSummarySourceCharacterId"] = result.UsesDefaultSummary ? result.SummarySourceCharacterId : 0,
                ["stepBackActionId"] = stepBack?.ActionId.Value ?? 0,
                ["stepBackContentHash"] = stepBack == null ? null : stepBackHash,
                ["summary"] = result.SpatialSummary == null ? null : JObject.Parse(CharacterActionTacticalSummaryJson.Write(result.SpatialSummary))
            };
            if (requirements != CharacterTestTacticalInputRequirements.All) content["requirements"] = (int)requirements;
            if (spatial) content["rangeSelectionPolicy"] = CharacterTestTacticalActionRange.SelectionPolicy;
            if (stepForward != null) { content["stepForwardActionId"] = stepForward.ActionId.Value; content["stepForwardContentHash"] = stepForwardHash; }
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(content.ToString(Formatting.None)));
                inputs = new CharacterTestTacticalCardInputs(action, result.Metrics,
                    BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant(), actionSet?.StableId ?? string.Empty, source.Identity.CharacterId, testActionId > 0, stepBack, stepBackHash,
                    result.SpatialSummary, result.SummarySourceCharacterId, result.UsesDefaultSummary ? result.Issue : null, requirements);
                inputs.SetStepForward(stepForward, stepForwardHash);
            }
            return true;
        }

        internal const int CommonStepBackActionId = 950003;
        internal const int CommonStepForwardActionId = CharacterTestCombatEvadeRuntime.ForwardStepActionId;
        internal static bool TryResolveStepForward(CharacterActionConfig[] actions, out CharacterActionConfig action)
        {
            action = actions?.FirstOrDefault(a => a != null && a.ActionId.Value == CommonStepForwardActionId
                && a.Category == CharacterActionCategory.Dodge && a.DurationFrames.HasValue && a.DurationFrames.Value > 0);
            return action != null;
        }

        internal static bool TryResolveStepBack(CharacterActionConfig[] actions, out CharacterActionConfig action)
        {
            action = null;
            if (actions == null) return false;
            // The tactical stepBack slot is equipped with the common action directly.
            // Input intent bindings are unrelated to this authored action event.
            foreach (var candidate in actions)
                if (candidate != null && candidate.ActionId.Value == CommonStepBackActionId && candidate.Category == CharacterActionCategory.Dodge
                    && candidate.DurationFrames.HasValue && candidate.DurationFrames.Value > 0 && candidate.DurationFrames.Value < int.MaxValue)
                { action = candidate; return true; }
            return false;
        }

        internal static bool IsActionEquipped(CharacterActionSetConfig set, int actionId)
        {
            if (set == null || actionId <= 0) return false;
            if (set.DefaultActionId.IsValid && set.DefaultActionId.Value == actionId) return true;
            if (set.CommandBindings != null)
            {
                for (int i = 0; i < set.CommandBindings.Length; i++)
                    if (set.CommandBindings[i] != null && set.CommandBindings[i].ActionId.Value == actionId)
                        return true;
            }
            if (set.CommandSequences != null)
            {
                for (int i = 0; i < set.CommandSequences.Length; i++)
                {
                    var seq = set.CommandSequences[i];
                    if (seq?.ActionIds != null)
                    {
                        for (int j = 0; j < seq.ActionIds.Length; j++)
                            if (seq.ActionIds[j].Value == actionId)
                                return true;
                    }
                }
            }
            if (set.AbilityBindings != null)
            {
                for (int i = 0; i < set.AbilityBindings.Length; i++)
                    if (set.AbilityBindings[i] != null && set.AbilityBindings[i].ActionId.Value == actionId)
                        return true;
            }
            if (set.ReactionBindings != null)
            {
                for (int i = 0; i < set.ReactionBindings.Length; i++)
                    if (set.ReactionBindings[i] != null && set.ReactionBindings[i].DefaultActionId.Value == actionId)
                        return true;
            }
            return false;
        }

        /// <summary>
        /// Determines whether an action has standalone opener eligibility:
        /// CanLaunchAsOpener = IsExplicitOpener || HasStandaloneBinding || (!IsDerivative)
        /// - IsExplicitOpener: set.DefaultActionId or index 0 of ANY CommandSequence.
        /// - HasStandaloneBinding: present in CommandBindings, AbilityBindings, or ReactionBindings.
        /// - IsDerivative: present at index > 0 of ANY CommandSequence.
        /// An action with mixed identity (e.g. sequence A derivative and sequence B opener) has IsExplicitOpener == true,
        /// so it is eligible regardless of sequence array ordering.
        /// Pure derivatives (IsDerivative == true, but !IsExplicitOpener and !HasStandaloneBinding) are strictly rejected.
        /// Note: This performs static structural eligibility; dynamic launch validity is arbitrated by the action runtime state machine.
        /// </summary>
        internal static bool IsActionEligibleForTacticalOpener(CharacterActionSetConfig set, int actionId)
        {
            if (set == null || actionId <= 0) return false;

            // 1. Default action is always an explicit opener
            if (set.DefaultActionId.IsValid && set.DefaultActionId.Value == actionId) return true;

            // 2. Bound as single command, ability or reaction (HasStandaloneBinding)
            if (set.CommandBindings != null)
            {
                for (int i = 0; i < set.CommandBindings.Length; i++)
                    if (set.CommandBindings[i] != null && set.CommandBindings[i].ActionId.Value == actionId)
                        return true;
            }
            if (set.AbilityBindings != null)
            {
                for (int i = 0; i < set.AbilityBindings.Length; i++)
                    if (set.AbilityBindings[i] != null && set.AbilityBindings[i].ActionId.Value == actionId)
                        return true;
            }
            if (set.ReactionBindings != null)
            {
                for (int i = 0; i < set.ReactionBindings.Length; i++)
                    if (set.ReactionBindings[i] != null && set.ReactionBindings[i].DefaultActionId.Value == actionId)
                        return true;
            }

            // 3. Command Sequences complete-set evaluation:
            // An action is an explicit opener if it is at index 0 of ANY sequence.
            // It is a derivative if it is at index > 0 in ANY sequence.
            // Explicit opener privilege takes precedence over derivative appearance.
            if (set.CommandSequences != null)
            {
                bool isSequenceOpener = false;
                bool isSequenceDerivative = false;
                for (int i = 0; i < set.CommandSequences.Length; i++)
                {
                    var seq = set.CommandSequences[i];
                    if (seq?.ActionIds == null || seq.ActionIds.Length == 0) continue;
                    if (seq.ActionIds[0].Value == actionId) isSequenceOpener = true;
                    for (int j = 1; j < seq.ActionIds.Length; j++)
                    {
                        if (seq.ActionIds[j].Value == actionId) isSequenceDerivative = true;
                    }
                }
                if (isSequenceOpener) return true;
                if (isSequenceDerivative) return false;
            }

            return true;
        }

        internal static bool TryResolveSelectedAction(CharacterActionSetConfig set, CharacterActionConfig[] actions,
            int testActionId, out CharacterActionConfig action, out string issue)
        {
            action = null; issue = string.Empty;
            if (testActionId <= 0) return Fail("Action:ExplicitSelectionRequired", out issue);
            // Tactical ownership is checked by CharacterTacticalPlayerService. Authoring previews
            // intentionally use temporary explicit IDs; neither path infers ownership from ActionSet.
            foreach (var candidate in actions ?? Array.Empty<CharacterActionConfig>())
                if (candidate?.ActionId.Value == testActionId) { action = candidate; return TryValidateAction(action, out issue); }
            return Fail("ActionSlot:SelectedTestActionUnavailable", out issue);
        }

        /// <summary>
        /// Resolves a generalized TacticalActionSlot into a concrete physical CharacterActionConfig
        /// based on the character's current CharacterActionSetConfig loadout.
        /// </summary>
        internal static bool TryResolveActionSlot(CharacterActionSetConfig set, CharacterActionConfig[] actions,
            TacticalActionSlot slot, out CharacterActionConfig action, out string issue)
        {
            action = null;
            issue = string.Empty;
            if (slot == TacticalActionSlot.None)
                return Fail("Card:EmptyActionSlot", out issue);
            if (set == null)
                return Fail("ActionSlot:MissingCurrentActionSet", out issue);

            int targetActionId = 0;
            switch (slot)
            {
                case TacticalActionSlot.PrimaryAttack:
                    // 1. Primary sequence opener (e.g. LightAttack intent 59 or AttackPrimary 1)
                    if (set.CommandSequences != null)
                    {
                        for (int i = 0; i < set.CommandSequences.Length; i++)
                        {
                            var seq = set.CommandSequences[i];
                            if (seq != null && seq.ActionIds != null && seq.ActionIds.Length > 0 &&
                                (seq.IntentId == CharacterTestCharacterActionConfigSource.LightAttackIntentNumericId
                                 || seq.IntentId == CharacterTestActionSlotIds.AttackPrimary))
                            {
                                targetActionId = seq.ActionIds[0].Value;
                                break;
                            }
                        }
                    }
                    // 2. Command bindings
                    if (targetActionId <= 0 && set.CommandBindings != null)
                    {
                        for (int i = 0; i < set.CommandBindings.Length; i++)
                        {
                            var b = set.CommandBindings[i];
                            if (b != null && b.ActionId.IsValid &&
                                (b.IntentId == CharacterTestCharacterActionConfigSource.LightAttackIntentNumericId
                                 || b.IntentId == CharacterTestActionSlotIds.AttackPrimary))
                            {
                                targetActionId = b.ActionId.Value;
                                break;
                            }
                        }
                    }
                    // 3. Fallback to DefaultActionId or first sequence opener
                    if (targetActionId <= 0 && set.DefaultActionId.IsValid)
                        targetActionId = set.DefaultActionId.Value;
                    if (targetActionId <= 0 && set.CommandSequences != null && set.CommandSequences.Length > 0)
                    {
                        for (int i = 0; i < set.CommandSequences.Length; i++)
                        {
                            var seq = set.CommandSequences[i];
                            if (seq != null && seq.ActionIds != null && seq.ActionIds.Length > 0 && seq.ActionIds[0].IsValid)
                            {
                                targetActionId = seq.ActionIds[0].Value;
                                break;
                            }
                        }
                    }
                    break;

                case TacticalActionSlot.SecondaryAttack:
                    if (set.CommandSequences != null)
                    {
                        for (int i = 0; i < set.CommandSequences.Length; i++)
                        {
                            var seq = set.CommandSequences[i];
                            if (seq != null && seq.ActionIds != null && seq.ActionIds.Length > 0 &&
                                seq.IntentId == CharacterTestActionSlotIds.AttackSecondary)
                            {
                                targetActionId = seq.ActionIds[0].Value;
                                break;
                            }
                        }
                    }
                    if (targetActionId <= 0 && set.CommandBindings != null)
                    {
                        for (int i = 0; i < set.CommandBindings.Length; i++)
                        {
                            var b = set.CommandBindings[i];
                            if (b != null && b.ActionId.IsValid && b.IntentId == CharacterTestActionSlotIds.AttackSecondary)
                            {
                                targetActionId = b.ActionId.Value;
                                break;
                            }
                        }
                    }
                    break;

                case TacticalActionSlot.Mobility:
                    if (set.CommandBindings != null)
                    {
                        for (int i = 0; i < set.CommandBindings.Length; i++)
                        {
                            var b = set.CommandBindings[i];
                            if (b != null && b.ActionId.IsValid &&
                                (b.IntentId == CharacterTestCharacterActionConfigSource.DodgeIntentId
                                 || b.IntentId == CharacterTestActionSlotIds.Dodge))
                            {
                                targetActionId = b.ActionId.Value;
                                break;
                            }
                        }
                    }
                    if (targetActionId <= 0 && actions != null)
                    {
                        for (int i = 0; i < actions.Length; i++)
                        {
                            var cand = actions[i];
                            if (cand != null && cand.Category == CharacterActionCategory.Dodge && IsActionEquipped(set, cand.ActionId.Value))
                            {
                                targetActionId = cand.ActionId.Value;
                                break;
                            }
                        }
                    }
                    break;

                case TacticalActionSlot.Skill:
                    if (set.AbilityBindings != null && set.AbilityBindings.Length > 0)
                    {
                        for (int i = 0; i < set.AbilityBindings.Length; i++)
                        {
                            var ab = set.AbilityBindings[i];
                            if (ab != null && ab.ActionId.IsValid)
                            {
                                targetActionId = ab.ActionId.Value;
                                break;
                            }
                        }
                    }
                    if (targetActionId <= 0 && actions != null)
                    {
                        for (int i = 0; i < actions.Length; i++)
                        {
                            var cand = actions[i];
                            if (cand != null && cand.Category == CharacterActionCategory.Skill && IsActionEquipped(set, cand.ActionId.Value))
                            {
                                targetActionId = cand.ActionId.Value;
                                break;
                            }
                        }
                    }
                    break;

                case TacticalActionSlot.Guard:
                    if (set.CommandBindings != null)
                    {
                        for (int i = 0; i < set.CommandBindings.Length; i++)
                        {
                            var b = set.CommandBindings[i];
                            if (b != null && b.ActionId.IsValid &&
                                (b.IntentId == CharacterTestCharacterActionConfigSource.GuardAuraIntentId
                                 || b.IntentId == CharacterTestActionSlotIds.Guard))
                            {
                                targetActionId = b.ActionId.Value;
                                break;
                            }
                        }
                    }
                    break;

                default:
                    return Fail("Card:UnsupportedActionSlot", out issue);
            }

            if (targetActionId <= 0)
                return Fail("Card:EmptyActionSlot", out issue);

            if (!IsActionEquipped(set, targetActionId))
                return Fail("Action:NotEquippedOnCharacter", out issue);

            if (!IsActionEligibleForTacticalOpener(set, targetActionId))
                return Fail("Action:ComboDerivativeNotAllowedAsOpener", out issue);

            foreach (var candidate in actions ?? Array.Empty<CharacterActionConfig>())
            {
                if (candidate?.ActionId.Value == targetActionId)
                {
                    action = candidate;
                    return TryValidateAction(action, out issue);
                }
            }

            return Fail("ActionSlot:SelectedTestActionUnavailable", out issue);
        }

        /// <summary>Uses the formal resolver with no active-action state; never resumes a live combo.</summary>
        internal static bool TryResolveOpener(CharacterActionSetConfig actionSet, CharacterActionConfig[] actions,
            out CharacterActionConfig action, out string issue)
        {
            action = null;
            issue = string.Empty;
            if (actionSet == null || actions == null) return Fail("ActionSlot:MissingCurrentActionSet", out issue);
            bool hasSequence = false;
            for (int i = 0; i < actionSet.CommandSequences.Length; i++)
                if (actionSet.CommandSequences[i]?.IntentId == CharacterTestCharacterActionConfigSource.LightAttackIntentNumericId)
                    hasSequence = true;
            if (!hasSequence) return Fail("ActionSlot:MissingLightAttackSequence", out issue);
            var request = new CharacterActionIntentRequest(new GameplayEntityId(1, 1), CharacterTestCharacterActionConfigSource.LightAttackIntentNumericId, null,
                0, default, CharacterActionSourceKind.LocalInput, 0, default,
                new MxFramework.Runtime.RuntimeTraceIdentity(2008, 1), CharacterTestActionSlotIds.AttackPrimary);
            var resolved = new CharacterActionResolver().ResolveCommand(
                new CharacterActionResolverContext(actionSet, actions, state: default), request);
            if (!resolved.IsSuccess || resolved.Plan == null)
                return Fail("ActionSlot:OpenerRejected:" + resolved.RejectReason, out issue);
            for (int i = 0; i < actions.Length; i++)
                if (actions[i] != null && actions[i].ActionId == resolved.Plan.ActionId)
                { action = actions[i]; return true; }
            return Fail("ActionSlot:ResolvedActionMissing", out issue);
        }

        internal static bool TryValidateAction(CharacterActionConfig action, out string issue)
        {
            issue = string.Empty;
            if (action == null)
                return Fail("Action:ActionNull", out issue);
            if (action.Category != CharacterActionCategory.BasicAttack
                && action.Category != CharacterActionCategory.Skill
                && action.Category != CharacterActionCategory.Dodge
                && action.Category != CharacterActionCategory.Guard)
                return Fail("Action:UnsupportedCategory", out issue);
            if (!action.DurationFrames.HasValue || action.DurationFrames.Value <= 0
                || action.DurationFrames.Value >= int.MaxValue)
                return Fail("Action:FiniteDurationRequired", out issue);
            int duration = action.DurationFrames.Value;
            CharacterActionPhase? startup = null;
            CharacterActionPhase? active = null;
            CharacterActionPhase? recovery = null;
            for (int i = 0; i < action.Phases.Length; i++)
            {
                var phase = action.Phases[i];
                if (phase.StartFrame < 0 || phase.EndFrame < phase.StartFrame || phase.EndFrame >= duration)
                    return Fail("Action:PhaseOutsideDuration", out issue);
                if (phase.Kind == CharacterActionPhaseKind.Startup)
                {
                    if (startup.HasValue) return Fail("Action:SingleStartupRequired", out issue);
                    startup = phase;
                }
                else if (phase.Kind == CharacterActionPhaseKind.Active)
                {
                    if (active.HasValue) return Fail("Action:SingleActiveRequired", out issue);
                    active = phase;
                }
                else if (phase.Kind == CharacterActionPhaseKind.Recovery)
                {
                    if (recovery.HasValue) return Fail("Action:SingleRecoveryRequired", out issue);
                    recovery = phase;
                }
                else return Fail("Action:UnsupportedPhaseKind", out issue);
            }
            if (!startup.HasValue || !active.HasValue || startup.Value.StartFrame != 0
                || active.Value.StartFrame != startup.Value.EndFrame + 1)
                return Fail("Action:ContiguousStartupActiveRequired", out issue);
            if (recovery.HasValue
                ? recovery.Value.StartFrame != active.Value.EndFrame + 1 || recovery.Value.EndFrame != duration - 1
                : active.Value.EndFrame != duration - 1)
                return Fail("Action:ContiguousCompletionRequired", out issue);
            bool hasDirectHit = false;
            if (action.CombatTrack != null && action.CombatTrack.Events != null)
            {
                for (int i = 0; i < action.CombatTrack.Events.Length; i++)
                {
                    var evt = action.CombatTrack.Events[i];
                    if (evt.Kind != CharacterActionTrackEventKind.HitTraceWindow) continue;
                    if (evt.Frame < active.Value.StartFrame || evt.EndFrame > active.Value.EndFrame || evt.EndFrame < evt.Frame)
                        return Fail("Action:DirectHitOutsideActive", out issue);
                    hasDirectHit = true;
                }
            }
            if (action.Category == CharacterActionCategory.BasicAttack && !hasDirectHit)
                return Fail("Action:DirectHitRequired", out issue);
            return true;
        }

        // This is the ordinary-guard slot contract, not a policy for every Guard action.
        // Other authored Guard actions may legitimately own different defensive states.
        internal static bool TryValidateOrdinaryGuardAction(CharacterActionConfig action, out string issue)
        {
            if (!TryValidateAction(action, out issue)) return false;
            if (action.Category != CharacterActionCategory.Guard)
                return Fail("Action:OrdinaryGuardCategoryRequired", out issue);
            var active = action.Phases.First(p => p.Kind == CharacterActionPhaseKind.Active);
            bool hasGuardWindow = false;
            if (action.CombatTrack?.Events != null)
                foreach (var evt in action.CombatTrack.Events)
                {
                    if (evt.Kind == CharacterActionTrackEventKind.HitTraceWindow)
                        return Fail("Action:OrdinaryGuardMustNotAttack", out issue);
                    if (evt.Kind != CharacterActionTrackEventKind.SetHitTargetState) continue;
                    if (evt.TargetStateFlags != HitTargetStateFlags.Blocking)
                        return Fail("Action:OrdinaryGuardBlockingOnly", out issue);
                    // Combat consumers run after lifecycle Exit; EndFrame is the first ineffective hit frame.
                    if (evt.Frame < active.StartFrame || (long)evt.EndFrame > (long)active.EndFrame + 1L || evt.EndFrame <= evt.Frame)
                        return Fail("Action:GuardWindowOutsideActive", out issue);
                    hasGuardWindow = true;
                }
            return hasGuardWindow || Fail("Action:GuardBlockingWindowRequired", out issue);
        }

        internal static bool TryValidateSummary(CharacterActionSpatialSummary summary, out string issue)
            => TrySelectRange(summary, out _, out issue);

        /// <remarks>LowFreqAlloc. Select only at preparation; the original status and all intervals remain intact.</remarks>
        internal static bool TrySelectRange(CharacterActionSpatialSummary summary, out CharacterTestTacticalActionRange range, out string issue)
        {
            range = null;
            issue = string.Empty;
            if (summary != null && summary.ProberVersion != CharacterActionRangeProber.Version)
                return Fail("ActionSummary:HurtboxSurfaceMeasurementRequired", out issue);
            if (summary == null || (summary.ProberStatus != CharacterActionProberStatus.Valid
                && summary.ProberStatus != CharacterActionProberStatus.Discontinuous)
                || summary.Intervals.Count == 0 || summary.IsLowerBoundOpen || summary.IsUpperBoundOpen
                || !summary.VerifiedOptimalMillimeters.HasValue)
                return Fail("ActionSummary:ClosedVerifiedFirstRangeRequired", out issue);
            int optimal = summary.VerifiedOptimalMillimeters.Value;
            bool verifiedInRange = false;
            foreach (var interval in summary.Intervals)
                if (optimal >= interval.FirstHitMillimeters && optimal <= interval.LastHitMillimeters) verifiedInRange = true;
            if (!verifiedInRange)
                return Fail("ActionSummary:OptimalOutsideRange", out issue);
            // The first card faces the target centre; offset sectors need an explicit future aiming policy.
            if (summary.Sector.CenterMilliDegrees != 0 || summary.Sector.ToleranceMilliDegrees <= 0)
                return Fail("ActionSummary:CentredFacingSectorRequired", out issue);
            var first = summary.Intervals[0];
            if (optimal < first.FirstHitMillimeters || optimal > first.LastHitMillimeters)
            {
                // v5 preserves every missed grid point as a break. A grid point inside its first
                // reported hit run was actually evaluated, unlike an arbitrary midpoint in a gap.
                int step = summary.Measurement.StepMillimeters;
                if ((first.FirstHitMillimeters - summary.Measurement.SearchMinMillimeters) % step != 0
                    || (first.LastHitMillimeters - first.FirstHitMillimeters) % step != 0)
                    return Fail("ActionSummary:FirstRangeSampleGridRequired", out issue);
                optimal = checked(first.FirstHitMillimeters + (int)(((long)first.LastHitMillimeters - first.FirstHitMillimeters) / (2L * step)) * step);
            }
            range = new CharacterTestTacticalActionRange(summary, optimal);
            return true;
        }

        private static bool Fail(string value, out string issue) { issue = value; return false; }
    }
}
