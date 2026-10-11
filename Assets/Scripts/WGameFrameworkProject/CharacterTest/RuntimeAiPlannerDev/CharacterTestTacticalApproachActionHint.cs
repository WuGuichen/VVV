using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Fixed player hint for the verified approach, face, wait and action composition.</summary>
    /// <remarks>LowFreqAlloc. Derived presentation only; does not own source, bindings or execution state.</remarks>
    internal sealed class CharacterTestTacticalApproachActionHint
    {
        internal const string TemplateId = "approach-face-action.v1";
        internal const string Title = "常规移动接敌出招";
        internal const string LeadPrefix = "以";
        internal const string LeadSuffix = "接敌就位，";
        internal const string Lead = LeadPrefix + CharacterTestTacticalBasicMovementHint.Title + LeadSuffix;
        internal const string Verb = "发动";
        internal const string DefaultTextTemplate = "以{movement}接敌就位，发动{action}。";
        internal string TextTemplate { get; private set; }
        internal string DetailsText { get; private set; }
        internal string MovementLabel { get; private set; }
        internal string ActionPrompt { get; private set; }
        internal string NodeId { get; private set; }
        internal string DefinitionId { get; private set; }
        internal string ActionSlotPath { get; private set; }
        internal string ActionName { get; private set; }
        internal bool HasSelectedAction { get; private set; }
        internal bool IsActionOnly { get; private set; }
        internal string DurationText { get; private set; }
        internal CharacterTestTacticalBasicMovementHint Movement { get; private set; }
        internal double? ApproachDistance { get; private set; }
        internal double? MinimumDistance { get; private set; }
        internal double? MaximumDistance { get; private set; }
        internal bool UsesFirstRange { get; private set; }
        internal double? FacingCenter { get; private set; }
        internal double? FacingTolerance { get; private set; }
        internal string Text => TextTemplate.Replace("{movement}", MovementLabel).Replace("{action}", ActionName);
        private IMxUiTextProvider _textProvider;
        internal string RangeText => MinimumDistance.HasValue && MaximumDistance.HasValue
            ? CharacterTestTacticalBlockDescription.DistanceText(MinimumDistance.Value, MaximumDistance.Value, _textProvider)
                + (UsesFirstRange ? CharacterTestUiText.Resolve(_textProvider, "ui.character_test.tactical.description.first_hit_range_only", "（仅第一段，不采用远段）") : "") : "—";
        internal string ApproachText => ApproachDistance.HasValue ? Number(ApproachDistance.Value) + " m" : "—";
        internal string FacingText => FacingTolerance.HasValue
            ? (FacingCenter.GetValueOrDefault() == 0 ? "" : Number(FacingCenter.Value) + "° ") + "±" + Number(FacingTolerance.Value) + "°" : "—";
        private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        internal static bool TryParse(CharacterTestTacticalSharedDefinition definition, string instance,
            CharacterTestTacticalAuthoringEvent[] events, CharacterTestTacticalCardInputs inputs,
            IReadOnlyDictionary<string, string> selectedNames, CharacterTestTacticalAuthoringCompilation prepared,
            out CharacterTestTacticalApproachActionHint hint, IMxUiTextProvider textProvider = null,
            double frameSeconds = 1d / CharacterTestDeterministicUnits.DefaultTickRate)
        {
            hint = null;
            var source = definition.Source;
            if (source.Timelines.Count != 1 || source.ActivationConditionRoot != null
                || source.Timelines[0].EntryConditionRoot != null || events.Length != 4) return false;
            var move = events.FirstOrDefault(e => e.Operation.Kind == "Movement" || e.Operation.Kind == "ApproachOptimalRange");
            var face = events.FirstOrDefault(e => e.Operation.Kind == "TurnToTarget");
            var gate = events.FirstOrDefault(e => e.Operation.Kind == "WaitForCondition");
            var action = events.FirstOrDefault(e => e.Operation.Kind == "CharacterAction");
            if (move == null || face == null || gate == null || action == null || move.ConditionRoot != null || face.ConditionRoot != null
                || face.Operation.Target != "currentCombatTarget" || !AtStart(move.Start) || !AtStart(face.Start) || !AtStart(gate.Start)) return false;
            // Playback ignores legacy action conditions; the explicit wait gate below owns admission.
            if (action.Start == null || !(action.Start.Kind == "Anchor" && action.Start.SourceEventId == gate.EventId
                    && action.Start.Anchor == "End" && action.Start.OffsetFrames == 0 && action.Start.TargetKind == "Event")
                && !(action.Start.Kind == "Absolute" && action.Start.Frame == 1)) return false;
            if (move.End == null || move.End.Kind != "Anchor" || move.End.SourceEventId != action.EventId
                || move.End.TargetKind != "Event" || move.End.OffsetFrames != 0
                || move.End.Anchor != "Start" && move.End.Anchor != "ActionStartupEnd") return false;
            if (move.Operation.Kind == "Movement")
            {
                var movement = move.Operation.Movement;
                if (movement == null || movement.Strategy != CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget
                    || movement.Target != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                    || movement.DistanceSource != "ActionOptimal" || movement.ActionSlotId != action.Operation.SlotId) return false;
            }
            else if (move.Operation.SourceActionEventId != action.EventId) return false;
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            bool Gate(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
            {
                if (node == null) return false;
                if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All)
                    return node.Children != null && node.Children.Count > 0 && node.Children.All(Gate);
                if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope)
                    return node.Subject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
                        && node.Children?.Count == 1 && Gate(node.Children[0]);
                if (node.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate) return false;
                var binding = source.PredicateBindings.FirstOrDefault(b => b.NodeId == node.NodeId);
                if (binding != null) return binding.SourceActionEventId == action.EventId
                    && (binding.Kind == "RootDistanceRange" || binding.Kind == "TargetFacing") && kinds.Add(binding.Kind);
                return Alive(node) && kinds.Add("Alive");
            }
            if (!Gate(gate.ConditionRoot) || !kinds.SetEquals(new[] { "Alive", "RootDistanceRange", "TargetFacing" })) return false;
            if (prepared != null)
            {
                var positions = prepared.Events.ToArray();
                var attackFrame = positions.First(e => e.EventId == action.EventId).StartFrame;
                if (positions.First(e => e.EventId == gate.EventId).StartFrame >= attackFrame
                    || positions.First(e => e.EventId == face.EventId).EndFrame < attackFrame) return false;
            }
            string path = instance == "legacy-use" ? action.Operation.SlotId : instance + "/" + action.Operation.SlotId;
            CharacterTestTacticalCardInputs selected = null;
            try { selected = inputs?.SlotInputs(path); }
            catch (InvalidOperationException) { }
            string name = selected?.Action?.DisplayName;
            bool hasAction = selected?.Action != null;
            if (hasAction && string.IsNullOrWhiteSpace(name)) name = "动作 " + selected.Action.ActionId.Value;
            if (!hasAction && selectedNames != null && selectedNames.TryGetValue(path, out var currentName))
            { name = currentName; hasAction = true; }
            var summary = selected?.SelectedRange;
            hint = new CharacterTestTacticalApproachActionHint {
                _textProvider = textProvider,
                TextTemplate = CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.approach_face_action", DefaultTextTemplate),
                MovementLabel = CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.basic_movement", CharacterTestTacticalBasicMovementHint.Title),
                ActionPrompt = CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.choose_action", "选择动作"),
                NodeId = instance, DefinitionId = definition.DefinitionId, ActionSlotPath = path,
                ActionName = hasAction ? name : CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.equipped_action", "装配动作"), HasSelectedAction = hasAction,
                Movement = new CharacterTestTacticalBasicMovementHint(instance, move.EventId, face.EventId, move.Operation.Speed, face.Operation.Speed),
                ApproachDistance = summary?.OptimalDistance, MinimumDistance = summary?.MinDistance, MaximumDistance = summary?.MaxDistance,
                UsesFirstRange = summary?.IgnoresFurtherIntervals == true,
                FacingCenter = summary == null ? (double?)null : summary.Sector.CenterMilliDegrees / 1000d,
                FacingTolerance = summary == null ? (double?)null : summary.Sector.ToleranceMilliDegrees / 1000d,
                DurationText = selected?.Action == null ? "—" : CharacterTestUiText.Resolve(textProvider,
                    "ui.character_test.tactical.description.expected_duration_seconds", "≈ {seconds} 秒")
                    .Replace("{seconds}", Number(selected.Action.DurationFrames.Value * frameSeconds))
            };
            return true;
        }

        private static bool AtStart(CharacterTestTacticalFrameReference value) => value?.Kind == "Absolute" && value.Frame == 0;

        internal static bool TryParseActionBlock(CharacterTestTacticalSharedDefinition definition, string instance,
            CharacterTestTacticalEventBlock block, CharacterTestTacticalAuthoringEvent[] events, int duration, double frameSeconds,
            CharacterTestTacticalCardInputs inputs, IReadOnlyDictionary<string, string> names, IMxUiTextProvider provider,
            out CharacterTestTacticalApproachActionHint hint)
        {
            hint = null;
            if (events.Length != 2 || block.Completion != "Action" || duration <= 0) return false;
            var action = events.FirstOrDefault(e => e.Operation?.Kind == "CharacterAction");
            var face = events.FirstOrDefault(e => e.Operation?.Kind == "TurnToTarget");
            if (action == null || face == null || block.CompletionEventId != action.EventId || face.ConditionRoot != null
                || face.Operation.Target != "currentCombatTarget" || !AtStart(action.Start) || action.Start.Space != "Block"
                || !AtStart(face.Start) || face.Start.Space != "Block"
                || face.End?.TargetKind != "Builtin" || face.End.SourceEventId != block.BlockId || face.End.Anchor != "SimulationEnd"
                || face.End.OffsetFrames != 0 || events.Any(e => e.MaxExecutionsPerPass != 1 || e.CooldownFrames != 0)) return false;
            string path = instance == "legacy-use" ? action.Operation.SlotId : instance + "/" + action.Operation.SlotId;
            CharacterTestTacticalCardInputs selected = null;
            try { selected = inputs?.SlotInputs(path); } catch (InvalidOperationException) { }
            bool hasAction = selected?.Action != null;
            string name = selected?.Action?.DisplayName;
            if (hasAction && string.IsNullOrWhiteSpace(name)) name = "动作 " + selected.Action.ActionId.Value;
            if (!hasAction && names != null && names.TryGetValue(path, out var configured)) { name = configured; hasAction = true; }
            string L(string key, string fallback) => CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description." + key, fallback);
            var summary = selected?.SelectedRange;
            hint = new CharacterTestTacticalApproachActionHint {
                _textProvider = provider,
                IsActionOnly = true, TextTemplate = L("face_action", "发动 {action}"), MovementLabel = string.Empty,
                DetailsText = L("face_action_details", "面向目标，按时间轴播放 {action}；动作结束后继续。是否进入本段由显式条件行为决定。")
                    .Replace("{action}", hasAction ? name : L("equipped_action", "装配动作"))
                    + CharacterTestTacticalBlockDescription.ActionLimitsText(block, frameSeconds, provider),
                ActionPrompt = L("choose_action", "选择动作"), ActionName = hasAction ? name : L("equipped_action", "装配动作"), HasSelectedAction = hasAction,
                NodeId = instance, DefinitionId = definition.DefinitionId, ActionSlotPath = path,
                MinimumDistance = summary?.MinDistance, MaximumDistance = summary?.MaxDistance,
                UsesFirstRange = summary?.IgnoresFurtherIntervals == true,
                FacingCenter = summary == null ? (double?)null : summary.Sector.CenterMilliDegrees / 1000d,
                FacingTolerance = summary == null ? (double?)null : summary.Sector.ToleranceMilliDegrees / 1000d,
                DurationText = selected?.Action == null ? "—" : L("expected_duration_seconds", "≈ {seconds} 秒")
                    .Replace("{seconds}", (selected.Action.DurationFrames.Value * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)) };
            return true;
        }
        internal static bool Alive(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
        {
            if (node?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope)
                return node.Subject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
                    && node.Children?.Count == 1 && Alive(node.Children[0]);
            var fact = node?.Predicate;
            return node?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate
                && fact?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetState
                && fact.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals
                && fact.AllowedTargetPhases == (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive
                && fact.RequiredTargetFlags == 0 && fact.ForbiddenTargetFlags == 0;
        }
    }
}
