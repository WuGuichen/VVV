using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabTimelineProjector
    {
        private CharacterTestRuntimeAiLabTimelineTrackProjection BuildDecisionTrack(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex eventIndex,
            int selectedAgentEntityId,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            CharacterTestRuntimeAiLabTimelineViewport viewport)
        {
            if (HasDecisionTrace(timeline, selectedAgentEntityId))
            {
                return BuildTraceDecisionTrack(
                    timeline,
                    eventIndex,
                    selectedAgentEntityId,
                    domain,
                    viewport);
            }

            List<CharacterTestRuntimeAiLabTimelineItem> items = _decisionItems;
            items.Clear();
            CharacterTestRuntimeAiPlannerAgentSnapshot previous = default;
            bool hasPrevious = false;
            int spanStart = 0;
            int previousRecordFrame = 0;
            int sourceOrder = 0;
            for (int i = 0; i < timeline.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.Records[i];
                if (!TryFindAgent(record.FrameSnapshot, selectedAgentEntityId, out CharacterTestRuntimeAiPlannerAgentSnapshot agent))
                {
                    if (hasPrevious)
                    {
                        AddDecisionSpan(items, previous, spanStart, previousRecordFrame, false, sourceOrder++, timeline);
                        hasPrevious = false;
                    }
                    continue;
                }
                if (!hasPrevious)
                {
                    previous = agent;
                    spanStart = record.Frame;
                    previousRecordFrame = record.Frame;
                    hasPrevious = true;
                    continue;
                }

                if (record.Frame != previousRecordFrame + 1)
                {
                    AddDecisionSpan(items, previous, spanStart, previousRecordFrame, false, sourceOrder++, timeline);
                    previous = agent;
                    spanStart = record.Frame;
                    previousRecordFrame = record.Frame;
                    continue;
                }
                if (DecisionEquals(previous, agent))
                {
                    previousRecordFrame = record.Frame;
                    continue;
                }
                AddDecisionSpan(items, previous, spanStart, record.Frame - 1, false, sourceOrder++, timeline);
                previous = agent;
                spanStart = record.Frame;
                previousRecordFrame = record.Frame;
            }

            if (hasPrevious)
                AddDecisionSpan(items, previous, spanStart, previousRecordFrame, previousRecordFrame == domain.LatestFrame, sourceOrder, timeline);

            AppendDecisionEventMarkers(
                items,
                timeline,
                eventIndex,
                selectedAgentEntityId,
                domain,
                viewport);
            TrimOutsideViewport(items, viewport);
            SortItems(items);
            items = AggregateMarkers(items, viewport);
            return new CharacterTestRuntimeAiLabTimelineTrackProjection(
                CharacterTestRuntimeAiLabTimelineTrackKind.Decision,
                "决策",
                items.ToArray(),
                selectedAgentEntityId > 0,
                selectedAgentEntityId > 0 ? string.Empty : "当前帧无智能体");
        }

        private void AppendDecisionEventMarkers(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex eventIndex,
            int selectedAgentEntityId,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            CharacterTestRuntimeAiLabTimelineViewport viewport)
        {
            if (eventIndex == null)
                return;

            List<CanonicalTimelineSample> samples = _decisionSamples;
            samples.Clear();
            for (int frame = domain.RetainedStartFrame; frame <= domain.LatestFrame; frame++)
            {
                int count = eventIndex.GetCount(frame);
                for (int eventIndexInFrame = 0; eventIndexInFrame < count; eventIndexInFrame++)
                {
                    CharacterTestRuntimeAiLabTimelineEventSample sample =
                        eventIndex.GetSample(frame, eventIndexInFrame);
                    if (!IsDecisionEvent(sample.Kind)
                        || frame < viewport.StartFrame
                        || frame > viewport.EndFrame
                        || (selectedAgentEntityId > 0
                            && sample.EntityId > 0
                            && sample.EntityId != selectedAgentEntityId))
                    {
                        continue;
                    }
                    samples.Add(new CanonicalTimelineSample(sample, 0));
                }
            }

            CanonicalizeSamples(samples);
            for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
            {
                CanonicalTimelineSample canonical = samples[sampleIndex];
                CharacterTestRuntimeAiLabTimelineEventSample sample = canonical.Sample;
                items.Add(CreateEventItem(
                    CharacterTestRuntimeAiLabTimelineTrackKind.Decision,
                    CharacterTestRuntimeAiLabTimelineItemKind.Marker,
                    sample,
                    sample.Frame,
                    false,
                    sample.Label,
                    canonical.OccurrenceRank,
                    timeline));
            }
        }

        private CharacterTestRuntimeAiLabTimelineTrackProjection BuildEventTrack(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineTrackKind trackKind,
            string label,
            CharacterTestRuntimeAiLabTimelineEventIndex index,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            int selectedAgentEntityId)
        {
            List<CharacterTestRuntimeAiLabTimelineItem> items = _eventItems;
            List<CanonicalTimelineSample> samples = _eventSamples;
            items.Clear();
            samples.Clear();
            for (int frame = domain.RetainedStartFrame; frame <= domain.LatestFrame; frame++)
            {
                int count = index?.GetCount(frame) ?? 0;
                for (int i = 0; i < count; i++)
                {
                    CharacterTestRuntimeAiLabTimelineEventSample sample = index.GetSample(frame, i);
                    if (trackKind == CharacterTestRuntimeAiLabTimelineTrackKind.Action)
                    {
                        if (!IsActionEvent(sample.Kind)
                            || (sample.Kind == CharacterTestRuntimeAiLabTimelineEventKind.Command
                                && selectedAgentEntityId > 0)
                            || (selectedAgentEntityId > 0 && sample.EntityId > 0
                                && sample.EntityId != selectedAgentEntityId))
                        {
                            continue;
                        }
                    }
                    else if (!IsCombatEvent(sample.Kind))
                    {
                        continue;
                    }
                    samples.Add(new CanonicalTimelineSample(sample, 0));
                }
            }

            CanonicalizeSamples(samples);
            List<OpenActionSample> openActions = _openActions;
            openActions.Clear();
            List<CanonicalTimelineSample> continuousCommands = _continuousCommandSamples;
            continuousCommands.Clear();
            for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
            {
                CanonicalTimelineSample canonical = samples[sampleIndex];
                CharacterTestRuntimeAiLabTimelineEventSample sample = canonical.Sample;
                if (trackKind == CharacterTestRuntimeAiLabTimelineTrackKind.Action)
                {
                    if (IsContinuousCommand(sample))
                    {
                        continuousCommands.Add(canonical);
                        continue;
                    }
                    ActionInstanceKey actionKey = new ActionInstanceKey(sample.EntityId, sample.InstanceId);
                        if (sample.Kind == CharacterTestRuntimeAiLabTimelineEventKind.ActionStarted
                            && sample.InstanceId != 0)
                        {
                            openActions.Add(new OpenActionSample(actionKey, canonical));
                            continue;
                        }
                        if (sample.Kind == CharacterTestRuntimeAiLabTimelineEventKind.ActionEnded
                            && sample.InstanceId != 0)
                        {
                            int pendingStartIndex = FindOpenAction(openActions, actionKey);
                            if (pendingStartIndex < 0)
                                goto EmitMarker;
                            CanonicalTimelineSample started = openActions[pendingStartIndex].Sample;
                            items.Add(CreateEventItem(
                                trackKind,
                                CharacterTestRuntimeAiLabTimelineItemKind.Span,
                                started.Sample,
                                sample.Frame,
                                false,
                                started.Sample.Label,
                                started.OccurrenceRank,
                                timeline));
                            openActions.RemoveAt(pendingStartIndex);
                            continue;
                        }
                }

EmitMarker:
                if (sample.Frame >= viewport.StartFrame && sample.Frame <= viewport.EndFrame)
                    items.Add(CreateEventItem(
                        trackKind,
                        CharacterTestRuntimeAiLabTimelineItemKind.Marker,
                        sample,
                        sample.Frame,
                        false,
                        sample.Label,
                        canonical.OccurrenceRank,
                        timeline));
            }

            if (trackKind == CharacterTestRuntimeAiLabTimelineTrackKind.Action)
            {
                AddContinuousCommandItems(items, continuousCommands, domain, timeline);
                List<CanonicalTimelineSample> open = _openActionSamples;
                open.Clear();
                for (int startIndex = 0; startIndex < openActions.Count; startIndex++)
                    open.Add(openActions[startIndex].Sample);
                open.Sort(CompareCanonicalSamples);
                for (int openIndex = 0; openIndex < open.Count; openIndex++)
                {
                    CanonicalTimelineSample started = open[openIndex];
                    items.Add(CreateEventItem(
                        trackKind,
                        CharacterTestRuntimeAiLabTimelineItemKind.Span,
                        started.Sample,
                        domain.LatestFrame,
                        true,
                        started.Sample.Label,
                        started.OccurrenceRank,
                        timeline));
                }
            }

            TrimOutsideViewport(items, viewport);
            SortItems(items);
            items = AggregateMarkers(items, viewport);
            return new CharacterTestRuntimeAiLabTimelineTrackProjection(
                trackKind,
                label,
                items.ToArray(),
                index != null && index.FrameLimit > 0,
                index != null && index.FrameLimit > 0 ? string.Empty : "未采集事件");
        }

        private void AddContinuousCommandItems(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            List<CanonicalTimelineSample> samples,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            CharacterTestRuntimeAiPlannerDevTimeline timeline)
        {
            int index = 0;
            while (index < samples.Count)
            {
                CanonicalTimelineSample first = samples[index];
                int end = index;
                int endFrame = first.Sample.Frame;
                while (end + 1 < samples.Count)
                {
                    CanonicalTimelineSample next = samples[end + 1];
                    if (next.Sample.Frame != endFrame + 1
                        || !ContinuousCommandEquals(first.Sample, next.Sample))
                    {
                        break;
                    }
                    end++;
                    endFrame = next.Sample.Frame;
                }

                bool isSpan = endFrame > first.Sample.Frame;
                items.Add(CreateEventItem(
                    CharacterTestRuntimeAiLabTimelineTrackKind.Action,
                    isSpan
                        ? CharacterTestRuntimeAiLabTimelineItemKind.Span
                        : CharacterTestRuntimeAiLabTimelineItemKind.Marker,
                    first.Sample,
                    endFrame,
                    isSpan && endFrame == domain.LatestFrame,
                    first.Sample.Label,
                    first.OccurrenceRank,
                    timeline));
                index = end + 1;
            }
        }

        private static bool IsContinuousCommand(
            in CharacterTestRuntimeAiLabTimelineEventSample sample)
        {
            return sample.Kind == CharacterTestRuntimeAiLabTimelineEventKind.Command
                && sample.TraceNumber == CharacterTestRuntimeCommandIds.SetControlIntent;
        }

        private static bool ContinuousCommandEquals(
            in CharacterTestRuntimeAiLabTimelineEventSample left,
            in CharacterTestRuntimeAiLabTimelineEventSample right)
        {
            return left.EntityId == right.EntityId
                && left.RelatedEntityId == right.RelatedEntityId
                && left.TraceNumber == right.TraceNumber
                && string.Equals(left.Category, right.Category, StringComparison.Ordinal)
                && string.Equals(left.Label, right.Label, StringComparison.Ordinal)
                && left.SourceTraceId == right.SourceTraceId;
        }

        private CharacterTestRuntimeAiLabTimelineTrackProjection BuildWarningTrack(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex index,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            int selectedAgentEntityId)
        {
            List<CharacterTestRuntimeAiLabTimelineItem> items = _warningItems;
            List<CanonicalTimelineSample> samples = _warningSamples;
            items.Clear();
            samples.Clear();
            for (int frame = domain.RetainedStartFrame; frame <= domain.LatestFrame; frame++)
            {
                int count = index?.GetCount(frame) ?? 0;
                for (int i = 0; i < count; i++)
                {
                    CharacterTestRuntimeAiLabTimelineEventSample sample = index.GetSample(frame, i);
                    if (sample.Kind != CharacterTestRuntimeAiLabTimelineEventKind.Warning
                        || frame < viewport.StartFrame || frame > viewport.EndFrame)
                        continue;
                    samples.Add(new CanonicalTimelineSample(sample, 0));
                }
            }
            CanonicalizeSamples(samples);
            for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
            {
                CanonicalTimelineSample canonical = samples[sampleIndex];
                CharacterTestRuntimeAiLabTimelineEventSample sample = canonical.Sample;
                    items.Add(CreateEventItem(
                        CharacterTestRuntimeAiLabTimelineTrackKind.Warnings,
                        CharacterTestRuntimeAiLabTimelineItemKind.Marker,
                        sample,
                        sample.Frame,
                        false,
                        string.IsNullOrEmpty(sample.Label) ? sample.Category : sample.Label,
                        canonical.OccurrenceRank,
                        timeline));
            }

            AddDecisionTraceWarnings(
                items,
                timeline,
                selectedAgentEntityId,
                domain);
            SortItems(items);
            items = AggregateMarkers(items, viewport);
            return new CharacterTestRuntimeAiLabTimelineTrackProjection(
                CharacterTestRuntimeAiLabTimelineTrackKind.Warnings,
                "警告",
                items.ToArray());
        }

        private static bool TryFindAgent(
            CharacterTestRuntimeAiPlannerDevFrameSnapshot frame,
            int entityId,
            out CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents = frame?.Agents;
            if (agents != null)
            {
                for (int i = 0; i < agents.Count; i++)
                {
                    if (entityId > 0 && agents[i].EntityId != entityId)
                        continue;
                    agent = agents[i];
                    return true;
                }
            }
            agent = default;
            return false;
        }

        private static int FindOpenAction(
            List<OpenActionSample> openActions,
            ActionInstanceKey key)
        {
            for (int i = 0; i < openActions.Count; i++)
            {
                if (openActions[i].Key.Equals(key))
                    return i;
            }
            return -1;
        }

        private static bool DecisionEquals(
            in CharacterTestRuntimeAiPlannerAgentSnapshot left,
            in CharacterTestRuntimeAiPlannerAgentSnapshot right)
        {
            return left.SelectedActionId == right.SelectedActionId
                && left.SelectedActionKind == right.SelectedActionKind
                && string.Equals(left.ActiveStatePath, right.ActiveStatePath, StringComparison.Ordinal)
                && left.TargetEntityId == right.TargetEntityId
                && left.RuntimeAiPlannerProfileSlot == right.RuntimeAiPlannerProfileSlot
                && left.RuntimeAiBehaviorLoadoutId == right.RuntimeAiBehaviorLoadoutId
                && left.RuntimeAiBehaviorVariantId == right.RuntimeAiBehaviorVariantId;
        }

        private static void AddDecisionSpan(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            in CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            int startFrame,
            int endFrame,
            bool isOpen,
            int sourceOrder,
            CharacterTestRuntimeAiPlannerDevTimeline timeline)
        {
            string label = CharacterTestRuntimeAiPlannerDevPresentationText.BuildActionLabel(agent);
            CharacterTestRuntimeAiLabExecutionIdentity.TryParseRuntimeEntryId(
                agent.SelectedActionName,
                out string runtimeEntryId);
            if (isOpen)
                label = "▶ " + label;
            if (agent.SelectedActionKind == CharacterTestRuntimeAiPlannerActionKind.Dodge
                && agent.TargetAttackImpactFrames >= 0)
            {
                label += "（预计 " + agent.TargetAttackImpactFrames.ToString(CultureInfo.InvariantCulture) + " 帧）";
            }
            string detail = "状态 " + agent.ActiveStatePath
                + " | 目标 " + agent.TargetEntityId
                + " | 分数 " + agent.SelectedUtilityScore.ToString("0.##", CultureInfo.InvariantCulture)
                + " / 代价 " + agent.SelectedActionCost.ToString("0.##", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(runtimeEntryId))
                detail += " | 行为条目 " + runtimeEntryId;
            if (agent.TargetAttackActionId.IsValid)
            {
                detail += " | 攻击 " + agent.TargetAttackActionId
                    + " | 预计命中 " + agent.TargetAttackImpactFrames.ToString(CultureInfo.InvariantCulture) + " 帧"
                    + " | 会命中 " + (agent.TargetAttackWillHitSelf ? "是" : "否")
                    + " | 已响应 " + (agent.TargetAttackInstanceResponded ? "是" : "否")
                    + " | 冷却 " + (agent.SelfDodgeCooldownReady ? "就绪" : "未就绪");
            }
            int sessionGeneration = timeline?.SessionIdentity.Generation ?? 0;
            long recordGeneration = 0L;
            long authorityHash = 0L;
            if (timeline != null
                && timeline.TryGetFrame(startFrame, out CharacterTestRuntimeAiPlannerDevTimelineRecord record))
            {
                recordGeneration = record.RecordGeneration;
                authorityHash = record.StateHash;
            }
            items.Add(new CharacterTestRuntimeAiLabTimelineItem(
                "decision:" + startFrame + ":" + agent.EntityId + ":" + sourceOrder,
                CharacterTestRuntimeAiLabTimelineTrackKind.Decision,
                CharacterTestRuntimeAiLabTimelineItemKind.Span,
                CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame,
                endFrame,
                isOpen,
                agent.EntityId,
                agent.TargetEntityId,
                agent.ActiveStatePath,
                CharacterTestRuntimeAiLabTimelineSeverity.Info,
                label,
                detail,
                1,
                string.Empty,
                sourceOrder,
                behaviorLoadoutId: agent.RuntimeAiBehaviorLoadoutId,
                behaviorEntryId: runtimeEntryId,
                sessionGeneration: sessionGeneration,
                recordGeneration: recordGeneration,
                authorityHash: authorityHash));
        }

        private CharacterTestRuntimeAiLabTimelineItem CreateEventItem(
            CharacterTestRuntimeAiLabTimelineTrackKind trackKind,
            CharacterTestRuntimeAiLabTimelineItemKind itemKind,
            in CharacterTestRuntimeAiLabTimelineEventSample sample,
            int endFrame,
            bool isOpen,
            string label,
            int occurrenceRank,
            CharacterTestRuntimeAiPlannerDevTimeline timeline)
        {
            string key = BuildEventStableKey(trackKind, itemKind, sample, occurrenceRank);
            string tooltip = "第 " + sample.Frame + " 帧"
                + " · 智能体 " + sample.EntityId
                + (sample.RelatedEntityId != 0 ? " → 目标 " + sample.RelatedEntityId : string.Empty)
                + " · " + label;
            if (sample.Value != 0)
                tooltip += " · 数值 " + sample.Value;
            int sessionGeneration = timeline?.SessionIdentity.Generation ?? 0;
            long recordGeneration = 0L;
            long authorityHash = 0L;
            if (timeline != null
                && timeline.TryGetFrame(sample.Frame, out CharacterTestRuntimeAiPlannerDevTimelineRecord record))
            {
                recordGeneration = record.RecordGeneration;
                authorityHash = record.StateHash;
            }
            return new CharacterTestRuntimeAiLabTimelineItem(
                key,
                trackKind,
                itemKind,
                CharacterTestRuntimeAiLabTimelineItemProvenance.TypedEvent,
                sample.Frame,
                endFrame,
                isOpen,
                sample.EntityId,
                sample.RelatedEntityId,
                sample.Category,
                sample.Severity,
                label,
                tooltip,
                sample.Kind == CharacterTestRuntimeAiLabTimelineEventKind.Warning
                    && string.Equals(sample.Category, "TimelineOverflow", StringComparison.Ordinal)
                    ? Math.Max(1, sample.Value)
                    : 1,
                sample.TraceNumber != 0
                    ? sample.TraceNumber.ToString(CultureInfo.InvariantCulture)
                    : sample.SourceTraceId.ToString(),
                occurrenceRank,
                sample.Value,
                behaviorEntryId: sample.BehaviorEntryId,
                behaviorStepId: sample.BehaviorStepId,
                eventKind: sample.Kind,
                occurrenceRank: occurrenceRank,
                profileSlot: sample.ProfileSlot,
                cardInstanceStartFrame: sample.CardInstanceStartFrame,
                sessionGeneration: sessionGeneration,
                recordGeneration: recordGeneration,
                authorityHash: authorityHash,
                sourceTraceIdentity: sample.SourceTraceId,
                behaviorEntryNumericId: sample.BehaviorEntryNumericId,
                behaviorStepIndex: sample.BehaviorStepIndex,
                behaviorEventIndex: sample.BehaviorEventIndex,
                behaviorTransitionIndex: sample.BehaviorTransitionIndex,
                behaviorTargetTimelineIndex: sample.BehaviorTargetTimelineIndex);
        }

        private string BuildEventStableKey(
            CharacterTestRuntimeAiLabTimelineTrackKind trackKind,
            CharacterTestRuntimeAiLabTimelineItemKind itemKind,
            in CharacterTestRuntimeAiLabTimelineEventSample sample,
            int occurrenceRank)
        {
            // The projector is a throttled, low-frequency presentation boundary. Length-prefix
            // string fields so user-authored ':' characters cannot alias another canonical tuple.
            StringBuilder key = _stableKeyBuilder;
            key.Length = 0;
            key.Append("event|")
                .Append((int)trackKind).Append('|')
                .Append((int)itemKind).Append('|')
                .Append((int)sample.Kind).Append('|')
                .Append(sample.Frame).Append('|')
                .Append(sample.EntityId).Append('|')
                .Append(sample.RelatedEntityId).Append('|');
            AppendLengthPrefixed(key, sample.Category);
            key.Append((int)sample.Severity).Append('|');
            AppendLengthPrefixed(key, sample.SourceTraceId.ToString());
            key.Append(sample.TraceNumber).Append('|')
                .Append(sample.InstanceId).Append('|');
            AppendLengthPrefixed(key, sample.BehaviorEntryId);
            AppendLengthPrefixed(key, sample.BehaviorStepId);
            key.Append(sample.Value).Append('|');
            AppendLengthPrefixed(key, sample.Label);
            key.Append(Math.Max(0, occurrenceRank));
            return key.ToString();
        }

        private static void AppendLengthPrefixed(StringBuilder builder, string value)
        {
            string safe = value ?? string.Empty;
            builder.Append(safe.Length).Append('#').Append(safe).Append('|');
        }

    }
}
