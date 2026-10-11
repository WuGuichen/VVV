using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>One recognized EventBlock, independent of its author-facing name.</summary>
    /// <remarks>LowFreqAlloc. Derived description only; never changes execution or source identity.</remarks>
    internal sealed class CharacterTestTacticalDescriptionSegment
    {
        internal CharacterTestTacticalDescriptionSegment(string blockId, CharacterTestTacticalApproachActionHint action)
        { BlockId = blockId; Action = action; }
        internal CharacterTestTacticalDescriptionSegment(string blockId, CharacterTestTacticalRetreatHint retreat)
        { BlockId = blockId; Retreat = retreat; }
        internal CharacterTestTacticalDescriptionSegment(string blockId, CharacterTestTacticalOrbitHint orbit)
        { BlockId = blockId; Orbit = orbit; }
        internal CharacterTestTacticalDescriptionSegment(string blockId, CharacterTestTacticalKeepDistanceHint keepDistance)
        { BlockId = blockId; KeepDistance = keepDistance; }
        internal CharacterTestTacticalDescriptionSegment(string blockId, CharacterTestTacticalDistanceArrivalHint arrival)
        { BlockId = blockId; Arrival = arrival; }
        internal CharacterTestTacticalDescriptionSegment(string blockId, CharacterTestTacticalFacingStepHint facingStep)
        { BlockId = blockId; FacingStep = facingStep; }
        internal CharacterTestTacticalDescriptionSegment(string blockId, CharacterTestTacticalWaitOpportunityHint waitOpportunity)
        { BlockId = blockId; WaitOpportunity = waitOpportunity; }
        internal CharacterTestTacticalDescriptionSegment(string blockId, CharacterTestTacticalFacingTargetHint facingTarget)
        { BlockId = blockId; FacingTarget = facingTarget; }
        internal CharacterTestTacticalDescriptionSegment(string blockId, string nodeId, string text, string details = null)
        { BlockId = blockId; TextNodeId = nodeId; LiteralText = text; LiteralDetails = details; }
        private string TextNodeId { get; }
        private string LiteralText { get; }
        private string LiteralDetails { get; }
        internal string BlockId { get; }
        internal CharacterTestTacticalApproachActionHint Action { get; }
        internal CharacterTestTacticalRetreatHint Retreat { get; }
        internal CharacterTestTacticalOrbitHint Orbit { get; }
        internal CharacterTestTacticalKeepDistanceHint KeepDistance { get; }
        internal CharacterTestTacticalDistanceArrivalHint Arrival { get; }
        internal CharacterTestTacticalFacingStepHint FacingStep { get; }
        internal CharacterTestTacticalWaitOpportunityHint WaitOpportunity { get; }
        internal CharacterTestTacticalFacingTargetHint FacingTarget { get; }
        internal string NodeId => Action?.NodeId ?? Retreat?.NodeId ?? Orbit?.NodeId ?? KeepDistance?.NodeId ?? Arrival?.NodeId ?? FacingStep?.NodeId ?? FacingTarget?.NodeId ?? WaitOpportunity?.NodeId ?? TextNodeId;
        internal string Text => Action?.Text ?? Retreat?.Text ?? Orbit?.Text ?? KeepDistance?.Text ?? Arrival?.Text ?? FacingStep?.Text ?? FacingTarget?.Text ?? WaitOpportunity?.Text ?? LiteralText;
        internal string DetailsText => Action?.DetailsText ?? Retreat?.DetailsText ?? Orbit?.DetailsText ?? KeepDistance?.DetailsText
            ?? Arrival?.DetailsText ?? FacingStep?.DetailsText ?? FacingTarget?.DetailsText ?? WaitOpportunity?.DetailsText ?? LiteralDetails;
        internal string TextTemplate => Action?.TextTemplate ?? Retreat?.TextTemplate ?? Orbit?.TextTemplate ?? KeepDistance?.TextTemplate ?? Arrival?.TextTemplate ?? FacingStep?.Text ?? FacingTarget?.Text ?? WaitOpportunity?.Text ?? LiteralText;
        internal string MovementLabel => Action?.MovementLabel ?? Retreat?.MovementLabel ?? Orbit?.MovementLabel ?? KeepDistance?.MovementLabel ?? Arrival?.MovementLabel ?? string.Empty;
        internal CharacterTestTacticalBasicMovementHint Movement => Action != null ? Action.Movement : Retreat?.Movement ?? Orbit?.Movement ?? KeepDistance?.Movement ?? Arrival?.Movement;
    }

    /// <summary>Concurrent retreat movement and facing over the same complete block interval.</summary>
    internal sealed class CharacterTestTacticalRetreatHint
    {
        internal const string TemplateId = "retreat-face.v1";
        internal const string Title = "常规移动面向目标后撤";
        internal string NodeId { get; private set; }
        internal string DefinitionId { get; private set; }
        internal string BlockId { get; private set; }
        internal string TextTemplate { get; private set; }
        internal string DetailsText { get; private set; }
        internal string MovementLabel { get; private set; }
        internal string RetreatLabel { get; private set; }
        internal string DurationText { get; private set; }
        internal int DurationFrames { get; private set; }
        internal CharacterTestTacticalBasicMovementHint Movement { get; private set; }
        internal string Text => TextTemplate.Replace("{movement}", MovementLabel).Replace("{retreat}", RetreatLabel);

        internal static bool TryParse(CharacterTestTacticalSharedDefinition definition, string instance,
            CharacterTestTacticalEventBlock block, CharacterTestTacticalAuthoringEvent[] events, int duration,
            double frameSeconds, IMxUiTextProvider textProvider, out CharacterTestTacticalRetreatHint hint)
        {
            hint = null;
            if (events.Length != 2 || duration <= 0 || frameSeconds <= 0 || double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds)) return false;
            var move = events.FirstOrDefault(e => e.Operation?.Kind == "Movement");
            var turn = events.FirstOrDefault(e => e.Operation?.Kind == "TurnToTarget");
            if (move == null || turn == null || turn.Operation.Target != "currentCombatTarget") return false;
            var movement = move.Operation.Movement;
            if (movement == null || movement.Strategy != CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard
                || movement.Target != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                || movement.DistanceSource != "Manual" || !string.IsNullOrEmpty(movement.ActionSlotId)) return false;
            foreach (var ev in events)
                if (ev.TimingKind != "Duration" || ev.ConditionRoot != null || ev.MaxExecutionsPerPass != 1 || ev.CooldownFrames != 0
                    || ev.Start?.Kind != "Absolute" || ev.Start.Frame != 0 || ev.Start.Space != "Block"
                    || ev.End?.Kind != "Anchor" || ev.End.TargetKind != "Builtin" || ev.End.SourceEventId != block.BlockId
                    || ev.End.Anchor != "SimulationEnd" || ev.End.OffsetFrames != 0) return false;
            string L(string key, string fallback) => CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description." + key, fallback);
            hint = new CharacterTestTacticalRetreatHint {
                NodeId = instance, DefinitionId = definition.DefinitionId, BlockId = block.BlockId,
                TextTemplate = L("retreat_face", "常规后撤"),
                DetailsText = L("retreat_face_details", "面向目标，以常规移动持续远离目标；本段持续 {seconds} 秒。")
                    .Replace("{seconds}", (duration * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)),
                MovementLabel = L("basic_movement", CharacterTestTacticalBasicMovementHint.Title),
                RetreatLabel = L("retreat", "后撤"), DurationFrames = duration,
                DurationText = L("duration_seconds", "{seconds} 秒").Replace("{seconds}", (duration * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)),
                Movement = new CharacterTestTacticalBasicMovementHint(instance, move.EventId, turn.EventId, move.Operation.Speed, turn.Operation.Speed)
            };
            return true;
        }
    }

    internal static class CharacterTestTacticalBlockDescription
    {
        internal static string ActionLimitsText(CharacterTestTacticalEventBlock block, double frameSeconds, IMxUiTextProvider provider)
        {
            if (block.PreparationTimeoutFrames <= 0 || block.CompletionTimeoutFrames <= 0) return string.Empty;
            return " " + CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description.action_limits_details",
                "准备上限 {preparation} 秒；执行上限 {completion} 秒。")
                .Replace("{preparation}", (block.PreparationTimeoutFrames * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture))
                .Replace("{completion}", (block.CompletionTimeoutFrames * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture));
        }

        internal static string DistanceText(double minimum, double maximum, IMxUiTextProvider textProvider)
        {
            string min = minimum.ToString("0.###", CultureInfo.InvariantCulture);
            string max = maximum.ToString("0.###", CultureInfo.InvariantCulture);
            return min == max
                ? CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.distance_meters", "{distance} 米")
                    .Replace("{distance}", min)
                : CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.distance_range_meters", "{min}～{max} 米")
                    .Replace("{min}", min).Replace("{max}", max);
        }

        internal static string Continuation(string text) => string.IsNullOrEmpty(text)
            ? text : char.ToLowerInvariant(text[0]) + text.Substring(1);

        /// <remarks>LowFreqAlloc. Derive ordinary guard text from its authoring source and selected action.</remarks>
        internal static bool TryOrdinaryGuardText(CharacterTestTacticalSharedDefinition definition, string instance,
            CharacterTestTacticalEventBlock block, CharacterTestTacticalAuthoringEvent[] events,
            double frameSeconds, CharacterTestTacticalCardInputs inputs, IMxUiTextProvider provider, out string text, bool includeDetails = false)
        {
            text = null;
            if (events.Length != 1 || block.Completion != "Action" || block.MaximumGuardFrames <= 0
                || block.CompletionEventId != events[0].EventId || events[0].Operation?.Kind != "CharacterAction"
                || events[0].ConditionRoot != null || events[0].MaxExecutionsPerPass != 1 || events[0].CooldownFrames != 0
                || frameSeconds <= 0 || double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds)) return false;
            var slot = definition.Source.Slots.FirstOrDefault(s => s.SlotId == events[0].Operation.SlotId);
            if (slot?.Resolver != "ordinaryGuard") return false;
            string L(string key, string fallback) => CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description." + key, fallback);
            string seconds = (block.MaximumGuardFrames * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture);
            text = L("ordinary_guard_details", "普通防御，维持上限 {seconds} 秒；正面左右各90°可格挡，格挡仍承受散势。")
                .Replace("{seconds}", seconds);
            if (inputs != null)
            {
                try
                {
                    var action = inputs.ResolveSlot(inputs.HasExplicitBindings ? instance + "/" + slot.SlotId : slot.Resolver);
                    if (!CharacterTestTacticalCardInputs.TryValidateOrdinaryGuardAction(action, out _)) return false;
                    var windows = action.CombatTrack.Events.Where(e => e.Kind == MxFramework.CharacterAction.CharacterActionTrackEventKind.SetHitTargetState);
                    int start = windows.Min(e => e.Frame), end = windows.Max(e => e.EndFrame) + 1;
                    text += L("ordinary_guard_action_span", "动作预计 {duration} 秒，格挡窗口 {start}–{end} 秒。")
                        .Replace("{duration}", (action.DurationFrames.Value * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture))
                        .Replace("{start}", (start * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture))
                        .Replace("{end}", (end * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture));
                }
                catch (InvalidOperationException) { return false; }
            }
            text = includeDetails ? text + ActionLimitsText(block, frameSeconds, provider) : L("ordinary_guard", "防御");
            return true;
        }

        /// <remarks>LowFreqAlloc. Recognize a complete, contiguous sequence; partial or overlapping matches keep the generic description.</remarks>
        internal static bool TryParse(CharacterTestTacticalSharedDefinition definition, CharacterTestTacticalDslNode node,
            CharacterTestTacticalAuthoringEvent[] reachable, CharacterTestTacticalCardInputs inputs,
            IReadOnlyDictionary<string, string> names, CharacterTestTacticalAuthoringCompilation prepared,
            IMxUiTextProvider textProvider, double frameSeconds, out CharacterTestTacticalDescriptionSegment[] result)
        {
            result = Array.Empty<CharacterTestTacticalDescriptionSegment>();
            var source = definition.Source;
            if (source.Timelines.Count != 1 || source.ActivationConditionRoot != null || source.Timelines[0].EntryConditionRoot != null) return false;
            var timeline = source.Timelines[0];
            if (timeline.EventBlocks == null || timeline.EventBlocks.Count == 0 || reachable.Length != timeline.Events.Count) return false;
            var ids = timeline.EventBlocks.SelectMany(b => b.EventIds).ToArray();
            if (ids.Length != reachable.Length || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length
                || !new HashSet<string>(ids, StringComparer.Ordinal).SetEquals(reachable.Select(e => e.EventId))) return false;
            var timing = prepared == null ? new CharacterTestTacticalTimingEvaluation(source, null, preview: true, lengths: node.BlockDurations) : null;
            if (timing != null) { timing.Evaluate(); if (timing.Issues.Count > 0) return false; }
            var ordered = new List<(CharacterTestTacticalEventBlock block, int start, int end)>();
            foreach (var block in timeline.EventBlocks)
            {
                int start, end;
                bool valid = prepared != null ? prepared.TryBlockFrames(block.BlockId, out start, out end)
                    : timing.TryFrames(block.BlockId, out start, out end);
                if (!valid || start < 0 || end <= start) return false;
                ordered.Add((block, start, end));
            }
            int previousEnd = 0;
            var segments = new List<CharacterTestTacticalDescriptionSegment>();
            foreach (var item in ordered.OrderBy(b => b.start).ThenBy(b => b.block.BlockId, StringComparer.Ordinal))
            {
                if (item.start != previousEnd) return false;
                previousEnd = item.end;
                var events = reachable.Where(e => item.block.EventIds.Contains(e.EventId)).ToArray();
                if ((item.block.Completion == "Delay" || item.block.Completion == "Distance")
                    && CharacterTestTacticalAuthoringCompiler.ValidateShared(source).Count == 0)
                {
                    string text = item.block.Completion == "Delay"
                        ? CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.wait_two_seconds", "等待2秒")
                        : CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.wait_target_distance", "等待目标进入指定距离");
                    string details = item.block.Completion == "Delay"
                        ? CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.wait_two_seconds_details", "等待2秒后，本段完成并继续后续行为。")
                        : CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.wait_target_distance_details", "不主动接近目标，等待目标进入指定距离后完成本段；最多等待 {seconds} 秒，超时结束本段。");
                    details = details.Replace("{seconds}", ((item.end - item.start) * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture));
                    segments.Add(new CharacterTestTacticalDescriptionSegment(item.block.BlockId, node.NodeId, text, details));
                }
                else if (TryOrdinaryGuardText(definition, node.NodeId, item.block, events, frameSeconds, inputs, textProvider, out var guardText))
                {
                    TryOrdinaryGuardText(definition, node.NodeId, item.block, events, frameSeconds, inputs, textProvider, out var guardDetails, includeDetails: true);
                    segments.Add(new CharacterTestTacticalDescriptionSegment(item.block.BlockId, node.NodeId, guardText, guardDetails));
                }
                else if (CharacterTestTacticalFacingTargetHint.TryParse(node.NodeId, item.block, events, textProvider, frameSeconds, out var facingTarget))
                    segments.Add(new CharacterTestTacticalDescriptionSegment(item.block.BlockId, facingTarget));
                else if (CharacterTestTacticalWaitOpportunityHint.TryParse(node.NodeId, item.block, events,
                    item.end - item.start, frameSeconds, textProvider, out var waitOpportunity))
                    segments.Add(new CharacterTestTacticalDescriptionSegment(item.block.BlockId, waitOpportunity));
                else if (CharacterTestTacticalFacingStepHint.TryParse(definition, node.NodeId, item.block, events,
                    frameSeconds, inputs, textProvider, out var facingStep))
                    segments.Add(new CharacterTestTacticalDescriptionSegment(item.block.BlockId, facingStep));
                else if (CharacterTestTacticalRetreatHint.TryParse(definition, node.NodeId, item.block, events,
                    item.end - item.start, frameSeconds, textProvider, out var retreat))
                    segments.Add(new CharacterTestTacticalDescriptionSegment(item.block.BlockId, retreat));
                else if (CharacterTestTacticalOrbitHint.TryParse(definition, node.NodeId, item.block, events,
                    item.end - item.start, frameSeconds, inputs, textProvider, out var orbit))
                    segments.Add(new CharacterTestTacticalDescriptionSegment(item.block.BlockId, orbit));
                else if (CharacterTestTacticalKeepDistanceHint.TryParse(definition, node.NodeId, item.block, events,
                    item.end - item.start, frameSeconds, inputs, textProvider, out var keepDistance))
                    segments.Add(new CharacterTestTacticalDescriptionSegment(item.block.BlockId, keepDistance));
                else if (CharacterTestTacticalDistanceArrivalHint.TryParse(definition, node.NodeId, item.block, events,
                    item.end - item.start, frameSeconds, inputs, textProvider, out var arrival))
                    segments.Add(new CharacterTestTacticalDescriptionSegment(item.block.BlockId, arrival));
                else if (CharacterTestTacticalApproachActionHint.TryParseActionBlock(definition, node.NodeId, item.block, events,
                    item.end - item.start, frameSeconds, inputs, names, textProvider, out var actionOnly))
                    segments.Add(new CharacterTestTacticalDescriptionSegment(item.block.BlockId, actionOnly));
                else if (CharacterTestTacticalApproachActionHint.TryParse(definition, node.NodeId, events, inputs, names,
                    prepared, out var action, textProvider))
                    segments.Add(new CharacterTestTacticalDescriptionSegment(item.block.BlockId, action));
                else return false;
            }
            result = segments.ToArray();
            return result.Length > 0;
        }
    }
}
