using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabTimelineProjector
    {
        private CharacterTestRuntimeAiLabTimelineTrackProjection BuildPerceptionTrack(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            int selectedAgentEntityId,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            CharacterTestRuntimeAiLabTimelineViewport viewport)
        {
            List<CharacterTestRuntimeAiLabTimelineItem> items = _perceptionItems;
            items.Clear();
            PerceptionTimelineState previous = default;
            CharacterTestRuntimeAiPlannerAgentSnapshot firstAgent = default;
            CharacterTestRuntimeAiPlannerAgentSnapshot lastAgent = default;
            bool hasPrevious = false;
            bool foundAgent = false;
            int spanStart = 0;
            int previousFrame = 0;
            int sourceOrder = 0;
            for (int i = 0; i < timeline.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.Records[i];
                if (!TryFindAgent(
                        record.FrameSnapshot,
                        selectedAgentEntityId,
                        out CharacterTestRuntimeAiPlannerAgentSnapshot agent))
                {
                    if (hasPrevious)
                    {
                        AddPerceptionSpan(
                            items,
                            previous,
                            firstAgent,
                            lastAgent,
                            spanStart,
                            previousFrame,
                            false,
                            sourceOrder++,
                            timeline);
                        hasPrevious = false;
                    }
                    continue;
                }

                foundAgent = true;
                PerceptionTimelineState current = new PerceptionTimelineState(agent);
                if (!hasPrevious)
                {
                    previous = current;
                    firstAgent = agent;
                    lastAgent = agent;
                    spanStart = record.Frame;
                    previousFrame = record.Frame;
                    hasPrevious = true;
                    continue;
                }

                if (record.Frame != previousFrame + 1 || !previous.Equals(current))
                {
                    AddPerceptionSpan(
                        items,
                        previous,
                        firstAgent,
                        lastAgent,
                        spanStart,
                        previousFrame,
                        false,
                        sourceOrder++,
                        timeline);
                    previous = current;
                    firstAgent = agent;
                    spanStart = record.Frame;
                }

                lastAgent = agent;
                previousFrame = record.Frame;
            }

            if (hasPrevious)
            {
                AddPerceptionSpan(
                    items,
                    previous,
                    firstAgent,
                    lastAgent,
                    spanStart,
                    previousFrame,
                    previousFrame == domain.LatestFrame,
                    sourceOrder,
                    timeline);
            }

            TrimOutsideViewport(items, viewport);
            SortItems(items);
            items = AggregateMarkers(items, viewport);
            return new CharacterTestRuntimeAiLabTimelineTrackProjection(
                CharacterTestRuntimeAiLabTimelineTrackKind.Perception,
                "感知",
                items.ToArray(),
                foundAgent,
                foundAgent ? string.Empty : "当前历史中没有所选智能体的感知快照");
        }

        private CharacterTestRuntimeAiLabTimelineTrackProjection BuildTraceDecisionTrack(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex eventIndex,
            int selectedAgentEntityId,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            CharacterTestRuntimeAiLabTimelineViewport viewport)
        {
            List<CharacterTestRuntimeAiLabTimelineItem> items = _decisionItems;
            items.Clear();
            TraceDecisionTimelineState previous = default;
            CharacterTestRuntimeAiPlannerAgentSnapshot firstAgent = default;
            bool hasPrevious = false;
            int spanStart = 0;
            int previousFrame = 0;
            int sourceOrder = 0;
            for (int i = 0; i < timeline.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.Records[i];
                if (!TryFindAgent(
                        record.FrameSnapshot,
                        selectedAgentEntityId,
                        out CharacterTestRuntimeAiPlannerAgentSnapshot agent)
                    || !timeline.TryGetDecisionTrace(
                        record,
                        selectedAgentEntityId,
                        out CharacterTestRuntimeAiLabDecisionTraceAgentView trace))
                {
                    if (hasPrevious)
                    {
                        AddTraceDecisionSpan(
                            items,
                            previous,
                            firstAgent,
                            spanStart,
                            previousFrame,
                            false,
                            sourceOrder++,
                            timeline);
                        hasPrevious = false;
                    }
                    continue;
                }

                TraceDecisionTimelineState current =
                    new TraceDecisionTimelineState(trace, agent);
                if (!hasPrevious)
                {
                    previous = current;
                    firstAgent = agent;
                    spanStart = record.Frame;
                    previousFrame = record.Frame;
                    hasPrevious = true;
                    continue;
                }

                if (record.Frame != previousFrame + 1 || !previous.Equals(current))
                {
                    AddTraceDecisionSpan(
                        items,
                        previous,
                        firstAgent,
                        spanStart,
                        previousFrame,
                        false,
                        sourceOrder++,
                        timeline);
                    previous = current;
                    firstAgent = agent;
                    spanStart = record.Frame;
                }
                previousFrame = record.Frame;
            }

            if (hasPrevious)
            {
                AddTraceDecisionSpan(
                    items,
                    previous,
                    firstAgent,
                    spanStart,
                    previousFrame,
                    previousFrame == domain.LatestFrame,
                    sourceOrder,
                    timeline);
            }

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

        private CharacterTestRuntimeAiLabTimelineTrackProjection BuildActionTrack(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex eventIndex,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            int selectedAgentEntityId)
        {
            CharacterTestRuntimeAiLabTimelineTrackProjection typedEvents = BuildEventTrack(
                timeline,
                CharacterTestRuntimeAiLabTimelineTrackKind.Action,
                "动作",
                eventIndex,
                domain,
                viewport,
                selectedAgentEntityId);
            List<CharacterTestRuntimeAiLabTimelineItem> items = _actionStateItems;
            items.Clear();
            if (HasDecisionTrace(timeline, selectedAgentEntityId))
            {
                AddTraceActionSpans(
                    items,
                    timeline,
                    selectedAgentEntityId,
                    domain);
                for (int i = 0; i < typedEvents.Items.Count; i++)
                    items.Add(typedEvents.Items[i]);
                TrimOutsideViewport(items, viewport);
                SortItems(items);
                items = AggregateMarkers(items, viewport);
                return new CharacterTestRuntimeAiLabTimelineTrackProjection(
                    CharacterTestRuntimeAiLabTimelineTrackKind.Action,
                    "动作",
                    items.ToArray(),
                    true,
                    string.Empty);
            }

            CharacterTestRuntimeAiPlannerAgentSnapshot previous = default;
            bool hasPrevious = false;
            bool foundAgent = false;
            int spanStart = 0;
            int previousFrame = 0;
            int sourceOrder = 0;
            for (int i = 0; i < timeline.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.Records[i];
                if (!TryFindAgent(
                        record.FrameSnapshot,
                        selectedAgentEntityId,
                        out CharacterTestRuntimeAiPlannerAgentSnapshot agent))
                {
                    if (hasPrevious)
                    {
                        AddActionStateSpan(
                            items,
                            previous,
                            spanStart,
                            previousFrame,
                            false,
                            sourceOrder++,
                            timeline);
                        hasPrevious = false;
                    }
                    continue;
                }

                foundAgent = true;
                if (!hasPrevious)
                {
                    previous = agent;
                    spanStart = record.Frame;
                    previousFrame = record.Frame;
                    hasPrevious = true;
                    continue;
                }

                if (record.Frame != previousFrame + 1 || !ActionStateEquals(previous, agent))
                {
                    AddActionStateSpan(
                        items,
                        previous,
                        spanStart,
                        previousFrame,
                        false,
                        sourceOrder++,
                        timeline);
                    previous = agent;
                    spanStart = record.Frame;
                }
                previousFrame = record.Frame;
            }

            if (hasPrevious)
            {
                AddActionStateSpan(
                    items,
                    previous,
                    spanStart,
                    previousFrame,
                    previousFrame == domain.LatestFrame,
                    sourceOrder++,
                    timeline);
            }

            for (int i = 0; i < typedEvents.Items.Count; i++)
                items.Add(typedEvents.Items[i]);
            TrimOutsideViewport(items, viewport);
            SortItems(items);
            items = AggregateMarkers(items, viewport);
            return new CharacterTestRuntimeAiLabTimelineTrackProjection(
                CharacterTestRuntimeAiLabTimelineTrackKind.Action,
                "动作",
                items.ToArray(),
                foundAgent || typedEvents.Available,
                foundAgent || typedEvents.Available
                    ? string.Empty
                    : typedEvents.UnavailableMessage);
        }

        private CharacterTestRuntimeAiLabTimelineTrackProjection BuildResultTrack(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex eventIndex,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            int selectedAgentEntityId)
        {
            CharacterTestRuntimeAiLabTimelineTrackProjection combatEvents = BuildEventTrack(
                timeline,
                CharacterTestRuntimeAiLabTimelineTrackKind.Result,
                "结果",
                eventIndex,
                domain,
                viewport,
                selectedAgentEntityId);
            List<CharacterTestRuntimeAiLabTimelineItem> items = _resultItems;
            items.Clear();
            RuntimeEffectTimelineState previous = default;
            bool hasPrevious = false;
            int spanStart = 0;
            int previousFrame = 0;
            int sourceOrder = 0;
            for (int i = 0; i < timeline.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.Records[i];
                if (!timeline.TryGetDecisionTrace(
                        record,
                        selectedAgentEntityId,
                        out CharacterTestRuntimeAiLabDecisionTraceAgentView trace))
                {
                    if (hasPrevious)
                    {
                        AddRuntimeEffectSpan(
                            items,
                            previous,
                            selectedAgentEntityId,
                            spanStart,
                            previousFrame,
                            false,
                            sourceOrder++,
                            timeline);
                        hasPrevious = false;
                    }
                    continue;
                }

                RuntimeEffectTimelineState current = new RuntimeEffectTimelineState(trace.Commit);
                if (!current.HasEvidence)
                {
                    if (hasPrevious)
                    {
                        AddRuntimeEffectSpan(
                            items,
                            previous,
                            selectedAgentEntityId,
                            spanStart,
                            previousFrame,
                            false,
                            sourceOrder++,
                            timeline);
                        hasPrevious = false;
                    }
                    continue;
                }

                if (!hasPrevious)
                {
                    previous = current;
                    spanStart = record.Frame;
                    previousFrame = record.Frame;
                    hasPrevious = true;
                    continue;
                }

                if (record.Frame != previousFrame + 1 || !previous.Equals(current))
                {
                    AddRuntimeEffectSpan(
                        items,
                        previous,
                        selectedAgentEntityId,
                        spanStart,
                        previousFrame,
                        false,
                        sourceOrder++,
                        timeline);
                    previous = current;
                    spanStart = record.Frame;
                }
                previousFrame = record.Frame;
            }

            if (hasPrevious)
            {
                AddRuntimeEffectSpan(
                    items,
                    previous,
                    selectedAgentEntityId,
                    spanStart,
                    previousFrame,
                    previousFrame == domain.LatestFrame,
                    sourceOrder++,
                    timeline);
            }

            for (int i = 0; i < combatEvents.Items.Count; i++)
                items.Add(combatEvents.Items[i]);
            TrimOutsideViewport(items, viewport);
            SortItems(items);
            items = AggregateMarkers(items, viewport);
            return new CharacterTestRuntimeAiLabTimelineTrackProjection(
                CharacterTestRuntimeAiLabTimelineTrackKind.Result,
                "结果",
                items.ToArray(),
                combatEvents.Available || selectedAgentEntityId > 0,
                combatEvents.Available || selectedAgentEntityId > 0
                    ? string.Empty
                    : combatEvents.UnavailableMessage);
        }

        private static void AddTraceActionSpans(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            int selectedAgentEntityId,
            CharacterTestRuntimeAiLabTimelineDomain domain)
        {
            TraceActionTimelineState previous = default;
            CharacterTestRuntimeAiPlannerAgentSnapshot firstAgent = default;
            bool hasPrevious = false;
            int spanStart = 0;
            int previousFrame = 0;
            int sourceOrder = 0;
            for (int i = 0; i < timeline.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.Records[i];
                if (!TryFindAgent(
                        record.FrameSnapshot,
                        selectedAgentEntityId,
                        out CharacterTestRuntimeAiPlannerAgentSnapshot agent)
                    || !timeline.TryGetDecisionTrace(
                        record,
                        selectedAgentEntityId,
                        out CharacterTestRuntimeAiLabDecisionTraceAgentView trace))
                {
                    if (hasPrevious)
                    {
                        AddTraceActionSpan(
                            items,
                            previous,
                            firstAgent,
                            spanStart,
                            previousFrame,
                            false,
                            sourceOrder++,
                            timeline);
                        hasPrevious = false;
                    }
                    continue;
                }

                TraceActionTimelineState current = new TraceActionTimelineState(trace.Commit, agent);
                if (!current.HasAction)
                {
                    if (hasPrevious)
                    {
                        AddTraceActionSpan(
                            items,
                            previous,
                            firstAgent,
                            spanStart,
                            previousFrame,
                            false,
                            sourceOrder++,
                            timeline);
                        hasPrevious = false;
                    }
                    continue;
                }
                if (!hasPrevious)
                {
                    previous = current;
                    firstAgent = agent;
                    spanStart = record.Frame;
                    previousFrame = record.Frame;
                    hasPrevious = true;
                    continue;
                }
                if (record.Frame != previousFrame + 1 || !previous.Equals(current))
                {
                    AddTraceActionSpan(
                        items,
                        previous,
                        firstAgent,
                        spanStart,
                        previousFrame,
                        false,
                        sourceOrder++,
                        timeline);
                    previous = current;
                    firstAgent = agent;
                    spanStart = record.Frame;
                }
                previousFrame = record.Frame;
            }
            if (hasPrevious)
            {
                AddTraceActionSpan(
                    items,
                    previous,
                    firstAgent,
                    spanStart,
                    previousFrame,
                    previousFrame == domain.LatestFrame,
                    sourceOrder,
                    timeline);
            }
        }

        private static void AddDecisionTraceWarnings(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            int selectedAgentEntityId,
            CharacterTestRuntimeAiLabTimelineDomain domain)
        {
            if (timeline == null || selectedAgentEntityId <= 0)
                return;

            TraceWarningTimelineState previous = default;
            bool hasPrevious = false;
            int spanStart = 0;
            int previousFrame = 0;
            int sourceOrder = items.Count;
            for (int i = 0; i < timeline.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.Records[i];
                TraceWarningTimelineState current = default;
                bool hasWarning = timeline.TryGetDecisionTrace(
                        record,
                        selectedAgentEntityId,
                        out CharacterTestRuntimeAiLabDecisionTraceAgentView trace)
                    && TraceWarningTimelineState.TryCreate(trace, out current);
                if (!hasWarning)
                {
                    if (hasPrevious)
                    {
                        AddTraceWarningSpan(
                            items,
                            previous,
                            selectedAgentEntityId,
                            spanStart,
                            previousFrame,
                            false,
                            sourceOrder++,
                            timeline);
                        hasPrevious = false;
                    }
                    continue;
                }

                if (!hasPrevious)
                {
                    previous = current;
                    spanStart = record.Frame;
                    previousFrame = record.Frame;
                    hasPrevious = true;
                    continue;
                }

                if (record.Frame != previousFrame + 1 || !previous.Equals(current))
                {
                    AddTraceWarningSpan(
                        items,
                        previous,
                        selectedAgentEntityId,
                        spanStart,
                        previousFrame,
                        false,
                        sourceOrder++,
                        timeline);
                    previous = current;
                    spanStart = record.Frame;
                }
                previousFrame = record.Frame;
            }

            if (hasPrevious)
            {
                AddTraceWarningSpan(
                    items,
                    previous,
                    selectedAgentEntityId,
                    spanStart,
                    previousFrame,
                    previousFrame == domain.LatestFrame,
                    sourceOrder,
                    timeline);
            }
        }

        private static void AddTraceWarningSpan(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            in TraceWarningTimelineState state,
            int selectedAgentEntityId,
            int startFrame,
            int endFrame,
            bool isOpen,
            int sourceOrder,
            CharacterTestRuntimeAiPlannerDevTimeline timeline)
        {
            ResolveRecordIdentity(
                timeline,
                startFrame,
                out int sessionGeneration,
                out long recordGeneration,
                out long authorityHash);
            items.Add(new CharacterTestRuntimeAiLabTimelineItem(
                "decision-warning:" + sessionGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + recordGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + startFrame.ToString(CultureInfo.InvariantCulture)
                    + ":" + sourceOrder.ToString(CultureInfo.InvariantCulture),
                CharacterTestRuntimeAiLabTimelineTrackKind.Warnings,
                endFrame > startFrame
                    ? CharacterTestRuntimeAiLabTimelineItemKind.Span
                    : CharacterTestRuntimeAiLabTimelineItemKind.Marker,
                CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame,
                endFrame,
                isOpen,
                selectedAgentEntityId,
                0,
                state.Category,
                state.Severity,
                state.Label,
                state.Detail,
                1,
                state.SourceTraceId,
                sourceOrder,
                sessionGeneration: sessionGeneration,
                recordGeneration: recordGeneration,
                authorityHash: authorityHash,
                reasonCode: state.SourceTraceId));
        }

        private static bool HasDecisionTrace(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            int selectedAgentEntityId)
        {
            if (timeline == null || selectedAgentEntityId <= 0)
                return false;
            for (int i = 0; i < timeline.Count; i++)
            {
                if (timeline.TryGetDecisionTrace(
                        timeline.Records[i],
                        selectedAgentEntityId,
                        out CharacterTestRuntimeAiLabDecisionTraceAgentView trace)
                    && trace.IsValid)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddPerceptionSpan(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            in PerceptionTimelineState state,
            in CharacterTestRuntimeAiPlannerAgentSnapshot firstAgent,
            in CharacterTestRuntimeAiPlannerAgentSnapshot lastAgent,
            int startFrame,
            int endFrame,
            bool isOpen,
            int sourceOrder,
            CharacterTestRuntimeAiPlannerDevTimeline timeline)
        {
            string label = state.TargetExists
                ? state.TargetDead ? "死亡目标" : state.TargetVisible ? "最近敌人" : "最近目标"
                : "无目标";
            string detail = state.TargetExists
                ? "目标 " + state.TargetEntityId.ToString(CultureInfo.InvariantCulture)
                    + " | 距离 "
                    + firstAgent.TargetDistance.ToString("0.0", CultureInfo.InvariantCulture)
                    + " → "
                    + lastAgent.TargetDistance.ToString("0.0", CultureInfo.InvariantCulture)
                    + "m | "
                    + (state.TargetVisible ? "可见" : "不可见")
                    + " | "
                    + (state.TargetDead ? "已死亡" : "未死亡")
                    + " | "
                    + (state.LineOfSightBlocked ? "视线受阻" : "视线畅通")
                    + " | "
                    + (state.InAttackRange ? "攻击范围内" : "攻击范围外")
                : "本区段没有有效目标";
            ResolveRecordIdentity(
                timeline,
                startFrame,
                out int sessionGeneration,
                out long recordGeneration,
                out long authorityHash);
            items.Add(new CharacterTestRuntimeAiLabTimelineItem(
                "perception:" + sessionGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + recordGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + startFrame.ToString(CultureInfo.InvariantCulture)
                    + ":" + state.TargetEntityId.ToString(CultureInfo.InvariantCulture)
                    + ":" + sourceOrder.ToString(CultureInfo.InvariantCulture),
                CharacterTestRuntimeAiLabTimelineTrackKind.Perception,
                CharacterTestRuntimeAiLabTimelineItemKind.Span,
                CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame,
                endFrame,
                isOpen,
                firstAgent.EntityId,
                state.TargetEntityId,
                "Perception.Target",
                CharacterTestRuntimeAiLabTimelineSeverity.Info,
                label,
                detail,
                1,
                "planner.fact.target-exists",
                sourceOrder,
                sessionGeneration: sessionGeneration,
                recordGeneration: recordGeneration,
                authorityHash: authorityHash,
                hasPerceptionSnapshot: true,
                perceptionStartDistance: firstAgent.TargetDistance,
                perceptionEndDistance: lastAgent.TargetDistance,
                perceptionTargetExists: state.TargetExists,
                perceptionTargetVisible: state.TargetVisible,
                perceptionLineOfSightBlocked: state.LineOfSightBlocked,
                perceptionInAttackRange: state.InAttackRange,
                perceptionTargetDead: state.TargetDead));
        }

        private static void AddTraceDecisionSpan(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            in TraceDecisionTimelineState state,
            in CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            int startFrame,
            int endFrame,
            bool isOpen,
            int sourceOrder,
            CharacterTestRuntimeAiPlannerDevTimeline timeline)
        {
            string label = ResolveDecisionLabel(state, agent);
            string detail = "决策结果：" + PlannerDecisionStatusLabel(
                state.PlannerLogicKind,
                state.CommandEnqueueKind);
            ResolveRecordIdentity(
                timeline,
                startFrame,
                out int sessionGeneration,
                out long recordGeneration,
                out long authorityHash);
            items.Add(new CharacterTestRuntimeAiLabTimelineItem(
                "decision-trace:" + sessionGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + recordGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + startFrame.ToString(CultureInfo.InvariantCulture)
                    + ":" + sourceOrder.ToString(CultureInfo.InvariantCulture),
                CharacterTestRuntimeAiLabTimelineTrackKind.Decision,
                CharacterTestRuntimeAiLabTimelineItemKind.Span,
                CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame,
                endFrame,
                isOpen,
                agent.EntityId,
                agent.TargetEntityId,
                "DecisionTrace",
                state.IsFailure
                    ? CharacterTestRuntimeAiLabTimelineSeverity.Warning
                    : CharacterTestRuntimeAiLabTimelineSeverity.Info,
                label,
                detail,
                1,
                state.SelectedCandidateStableId,
                sourceOrder,
                behaviorLoadoutId: agent.RuntimeAiBehaviorLoadoutId,
                behaviorEntryId: state.CardStableId,
                behaviorStepId: state.StageStableId,
                sessionGeneration: sessionGeneration,
                recordGeneration: recordGeneration,
                authorityHash: authorityHash,
                plannerLogicKind: state.PlannerLogicKind,
                commandEnqueueKind: state.CommandEnqueueKind,
                fallbackActionKind: state.FallbackActionKind,
                reasonCode: state.ReasonCode));
        }

        private static void AddActionStateSpan(
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
            string detail = "状态 " + agent.ActiveStatePath
                + " | 目标 " + agent.TargetEntityId.ToString(CultureInfo.InvariantCulture)
                + " | 分数 " + agent.SelectedUtilityScore.ToString("0.##", CultureInfo.InvariantCulture)
                + " / 代价 " + agent.SelectedActionCost.ToString("0.##", CultureInfo.InvariantCulture)
                + (!string.IsNullOrEmpty(agent.LastCommand)
                    ? " | 命令 " + agent.LastCommand
                    : string.Empty);
            ResolveRecordIdentity(
                timeline,
                startFrame,
                out int sessionGeneration,
                out long recordGeneration,
                out long authorityHash);
            items.Add(new CharacterTestRuntimeAiLabTimelineItem(
                "action-state:" + sessionGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + recordGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + startFrame.ToString(CultureInfo.InvariantCulture)
                    + ":" + sourceOrder.ToString(CultureInfo.InvariantCulture),
                CharacterTestRuntimeAiLabTimelineTrackKind.Action,
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

        private static void AddTraceActionSpan(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            in TraceActionTimelineState state,
            in CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            int startFrame,
            int endFrame,
            bool isOpen,
            int sourceOrder,
            CharacterTestRuntimeAiPlannerDevTimeline timeline)
        {
            string label = !string.IsNullOrEmpty(state.MainActionStableId)
                ? FriendlyStableToken(state.MainActionStableId)
                : CharacterTestRuntimeAiPlannerDevPresentationText.BuildActionLabel(agent);
            ResolveRecordIdentity(
                timeline,
                startFrame,
                out int sessionGeneration,
                out long recordGeneration,
                out long authorityHash);
            items.Add(new CharacterTestRuntimeAiLabTimelineItem(
                "trace-action:" + sessionGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + recordGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + startFrame.ToString(CultureInfo.InvariantCulture)
                    + ":" + sourceOrder.ToString(CultureInfo.InvariantCulture),
                CharacterTestRuntimeAiLabTimelineTrackKind.Action,
                CharacterTestRuntimeAiLabTimelineItemKind.Span,
                CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame,
                endFrame,
                isOpen,
                agent.EntityId,
                agent.TargetEntityId,
                "DecisionTrace.Command",
                state.CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected
                    ? CharacterTestRuntimeAiLabTimelineSeverity.Error
                    : CharacterTestRuntimeAiLabTimelineSeverity.Info,
                label,
                "主动作 " + NonEmptyTimelineValue(state.MainActionStableId, "无")
                    + " | 命令 " + state.CommandKind
                    + " | 入队 " + state.CommandEnqueueKind,
                1,
                state.MainActionStableId,
                sourceOrder,
                behaviorLoadoutId: agent.RuntimeAiBehaviorLoadoutId,
                behaviorEntryId: state.CardStableId,
                behaviorStepId: state.MainActionStableId,
                sessionGeneration: sessionGeneration,
                recordGeneration: recordGeneration,
                authorityHash: authorityHash,
                commandEnqueueKind: state.CommandEnqueueKind,
                commandKind: state.CommandKind,
                fallbackActionKind: state.FallbackActionKind,
                reasonCode: state.ReasonCode));
        }

        private static void AddRuntimeEffectSpan(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            in RuntimeEffectTimelineState state,
            int selectedAgentEntityId,
            int startFrame,
            int endFrame,
            bool isOpen,
            int sourceOrder,
            CharacterTestRuntimeAiPlannerDevTimeline timeline)
        {
            string label = RuntimeEffectLabel(state.Kind, state.CommandEnqueueKind);
            ResolveRecordIdentity(
                timeline,
                startFrame,
                out int sessionGeneration,
                out long recordGeneration,
                out long authorityHash);
            items.Add(new CharacterTestRuntimeAiLabTimelineItem(
                "runtime-effect:" + sessionGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + recordGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + startFrame.ToString(CultureInfo.InvariantCulture)
                    + ":" + sourceOrder.ToString(CultureInfo.InvariantCulture),
                CharacterTestRuntimeAiLabTimelineTrackKind.Result,
                CharacterTestRuntimeAiLabTimelineItemKind.Span,
                CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                startFrame,
                endFrame,
                isOpen,
                selectedAgentEntityId,
                0,
                "RuntimeEffect",
                ResolveRuntimeEffectSeverity(state.Kind, state.CommandEnqueueKind),
                label,
                BuildRuntimeEffectSummary(state),
                1,
                state.BehaviorActionStableId,
                sourceOrder,
                behaviorEntryId: state.CardStableId,
                behaviorStepId: NonEmptyTimelineValue(state.StageStableId, state.BehaviorActionStableId),
                sessionGeneration: sessionGeneration,
                recordGeneration: recordGeneration,
                authorityHash: authorityHash,
                commandEnqueueKind: state.CommandEnqueueKind,
                commandKind: state.CommandKind,
                runtimeEffectKind: state.Kind,
                reasonCode: state.ReasonCode));
        }

        private static bool ActionStateEquals(
            in CharacterTestRuntimeAiPlannerAgentSnapshot left,
            in CharacterTestRuntimeAiPlannerAgentSnapshot right)
        {
            return left.SelectedActionId == right.SelectedActionId
                && left.SelectedActionKind == right.SelectedActionKind
                && string.Equals(left.SelectedActionName, right.SelectedActionName, StringComparison.Ordinal)
                && string.Equals(left.LastCommand, right.LastCommand, StringComparison.Ordinal);
        }

        private static string ResolveDecisionLabel(
            in TraceDecisionTimelineState state,
            in CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!string.IsNullOrEmpty(state.DecisionBasisLabel))
                return state.DecisionBasisLabel;
            if (!string.IsNullOrEmpty(state.StageStableId))
            {
                string stage = FriendlyStableToken(state.StageStableId);
                if (string.Equals(stage, "action", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(state.MainActionStableId))
                {
                    return "执行 " + FriendlyStableToken(state.MainActionStableId);
                }
                return stage;
            }
            if (!string.IsNullOrEmpty(state.MainActionStableId))
                return FriendlyStableToken(state.MainActionStableId);
            if (string.Equals(
                    state.SelectedCandidateStableId,
                    "planner.fallback.no-executable-action",
                    StringComparison.Ordinal))
            {
                return "无可执行行为";
            }
            if (!string.IsNullOrEmpty(state.CardStableId))
                return "已选择行为卡";
            return CharacterTestRuntimeAiPlannerDevPresentationText.BuildActionLabel(agent);
        }

        private static string ResolveDecisionBasisLabel(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            in CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            for (int i = 0; i < trace.Facts.Count; i++)
            {
                CharacterTestRuntimeAiLabFactObservationTrace fact = trace.Facts[i];
                if (fact.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                    || fact.Value.Kind != CharacterTestRuntimeAiLabDecisionTraceValueKind.Float)
                {
                    continue;
                }

                return "距离 "
                    + fact.Value.Number.ToString("0.0", CultureInfo.InvariantCulture)
                    + "m";
            }

            return agent.TargetExists
                ? "距离 "
                    + agent.TargetDistance.ToString("0.0", CultureInfo.InvariantCulture)
                    + "m"
                : string.Empty;
        }

        private static string FriendlyStableToken(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return string.Empty;
            string lower = stableId.ToLowerInvariant();
            if (lower.Contains("observe") || lower.Contains("perception"))
                return "观察";
            if (lower.Contains("wait") || lower.Contains("hold"))
                return "等待";
            if (lower.Contains("approach") || lower.Contains("pursuit") || lower.Contains("engage"))
                return "接近";
            if (lower.Contains("retreat"))
                return "撤退";
            if (lower.Contains("strafe"))
                return "侧移";
            if (lower.Contains("attack") || lower.Contains("damage"))
                return "攻击";
            if (lower.Contains("dodge") || lower.Contains("evade"))
                return "闪避";
            if (lower.Contains("move"))
                return "移动";
            if (IsOpaqueTimelineIdentifier(stableId))
                return "未识别动作";
            int separator = Math.Max(
                stableId.LastIndexOf('.'),
                Math.Max(stableId.LastIndexOf('/'), stableId.LastIndexOf(':')));
            string token = separator >= 0 && separator + 1 < stableId.Length
                ? stableId.Substring(separator + 1)
                : stableId;
            if (IsOpaqueTimelineIdentifier(token))
                return "未识别动作";
            return token.Length <= 18 ? token : "未识别动作";
        }

        private static bool IsOpaqueTimelineIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (Guid.TryParse(value, out _))
                return true;
            int hexadecimalCount = 0;
            int separatorCount = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if ((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F'))
                {
                    hexadecimalCount++;
                }
                else if (character == '-' || character == '_')
                {
                    separatorCount++;
                }
            }
            return value.Length >= 16
                && hexadecimalCount >= 12
                && hexadecimalCount + separatorCount >= value.Length - 2;
        }

        private static CharacterTestRuntimeAiLabTimelineSeverity ResolveRuntimeEffectSeverity(
            CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind kind,
            CharacterTestRuntimeAiLabCommandEnqueueKind commandEnqueueKind)
        {
            if (commandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected
                || kind == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Failed)
            {
                return CharacterTestRuntimeAiLabTimelineSeverity.Error;
            }

            switch (kind)
            {
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Completed:
                    return CharacterTestRuntimeAiLabTimelineSeverity.Success;
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Cancelled:
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved:
                    return CharacterTestRuntimeAiLabTimelineSeverity.Warning;
                default:
                    return CharacterTestRuntimeAiLabTimelineSeverity.Info;
            }
        }

        private static string RuntimeEffectLabel(
            CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind kind,
            CharacterTestRuntimeAiLabCommandEnqueueKind commandEnqueueKind)
        {
            if (commandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected)
                return "命令被拒绝";

            switch (kind)
            {
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Started:
                    return "动作已开始";
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running:
                    return "动作执行中";
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Completed:
                    return "动作已完成";
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Cancelled:
                    return "动作已取消";
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Failed:
                    return "动作执行失败";
                default:
                    return commandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued
                        ? "已入队 · 尚未执行"
                        : "尚未提交命令";
            }
        }

        private static string BuildRuntimeEffectSummary(in RuntimeEffectTimelineState state)
        {
            string summary = RuntimeCommandLabel(state.CommandKind)
                + "：" + CommandEnqueueLabel(state.CommandEnqueueKind)
                + " · 动作状态：" + RuntimeEffectStateLabel(state.Kind);
            if (!string.IsNullOrEmpty(state.ReasonCode))
                summary += " · " + HumanizeTimelineReasonCode(state.ReasonCode);
            return summary;
        }

        private static string PlannerDecisionStatusLabel(
            CharacterTestRuntimeAiLabPlannerLogicCommitKind plannerLogicKind,
            CharacterTestRuntimeAiLabCommandEnqueueKind commandEnqueueKind)
        {
            if (commandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected
                || plannerLogicKind == CharacterTestRuntimeAiLabPlannerLogicCommitKind.Rejected)
            {
                return "未执行";
            }
            switch (plannerLogicKind)
            {
                case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed:
                    return commandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued
                        ? "已入队 · 尚未执行"
                        : "已提交";
                case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Held:
                    return "保持当前行为";
                case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Pending:
                    return "等待切换条件";
                case CharacterTestRuntimeAiLabPlannerLogicCommitKind.Fallback:
                    return "已使用兜底行为";
                default:
                    return "尚未产生执行决策";
            }
        }

        private static string HumanizeTimelineReasonCode(string reasonCode)
        {
            if (string.IsNullOrWhiteSpace(reasonCode))
                return "未提供具体原因";
            string lower = reasonCode.ToLowerInvariant();
            if (lower.Contains("no-executable") || lower.Contains("no_executable"))
                return "没有满足条件的可执行行为";
            if (lower.Contains("target") && (lower.Contains("missing") || lower.Contains("not")))
                return "当前没有有效目标";
            if (lower.Contains("range"))
                return "目标不在有效范围内";
            if (lower.Contains("blocked") || lower.Contains("path"))
                return "移动路径或视线受阻";
            if (lower.Contains("transition"))
                return "尚未满足切换条件";
            if (lower.Contains("queue"))
                return "命令队列暂时无法接收新命令";
            if (lower.Contains("reject") || lower.Contains("invalid"))
                return "运行时拒绝了本次执行";
            for (int i = 0; i < reasonCode.Length; i++)
            {
                if (reasonCode[i] >= '\u4e00' && reasonCode[i] <= '\u9fff')
                    return reasonCode;
            }
            return "具体原因暂不可读";
        }

        private static string RuntimeCommandLabel(
            CharacterTestRuntimeAiLabDecisionTraceCommandKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabDecisionTraceCommandKind.SetControlIntent:
                    return "控制意图";
                case CharacterTestRuntimeAiLabDecisionTraceCommandKind.BehaviorCardIntent:
                    return "行为卡意图";
                default:
                    return "运行命令";
            }
        }

        private static string CommandEnqueueLabel(
            CharacterTestRuntimeAiLabCommandEnqueueKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued:
                    return "已入队";
                case CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected:
                    return "被拒绝";
                default:
                    return "未提交";
            }
        }

        private static string RuntimeEffectStateLabel(
            CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Started:
                    return "已开始";
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running:
                    return "执行中";
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Completed:
                    return "已完成";
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Cancelled:
                    return "已取消";
                case CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Failed:
                    return "失败";
                default:
                    return "尚未观测到开始、完成或失败";
            }
        }

        private static string NonEmptyTimelineValue(string value, string fallback) =>
            string.IsNullOrEmpty(value) ? fallback : value;

        private static void ResolveRecordIdentity(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            int startFrame,
            out int sessionGeneration,
            out long recordGeneration,
            out long authorityHash)
        {
            sessionGeneration = timeline?.SessionIdentity.Generation ?? 0;
            recordGeneration = 0L;
            authorityHash = 0L;
            if (timeline != null
                && timeline.TryGetFrame(
                    startFrame,
                    out CharacterTestRuntimeAiPlannerDevTimelineRecord record))
            {
                recordGeneration = record.RecordGeneration;
                authorityHash = record.StateHash;
            }
        }

        private static string FindSelectedCandidateStableId(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace)
        {
            string fallback = string.Empty;
            for (int i = 0; i < trace.Candidates.Count; i++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = trace.Candidates[i];
                if (candidate.State != CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected)
                    continue;
                if (candidate.Layer == CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration)
                    return candidate.StableId ?? string.Empty;
                if (fallback.Length == 0)
                    fallback = candidate.StableId ?? string.Empty;
            }
            return fallback;
        }

        private readonly struct PerceptionTimelineState : IEquatable<PerceptionTimelineState>
        {
            public PerceptionTimelineState(
                in CharacterTestRuntimeAiPlannerAgentSnapshot agent)
            {
                TargetEntityId = agent.TargetEntityId;
                TargetExists = agent.TargetExists;
                TargetVisible = agent.TargetVisible;
                TargetDead = agent.TargetDead;
                LineOfSightBlocked = agent.TargetLineOfSightBlocked;
                InAttackRange = agent.TargetInAttackRange;
            }

            public int TargetEntityId { get; }
            public bool TargetExists { get; }
            public bool TargetVisible { get; }
            public bool TargetDead { get; }
            public bool LineOfSightBlocked { get; }
            public bool InAttackRange { get; }

            public bool Equals(PerceptionTimelineState other)
            {
                return TargetEntityId == other.TargetEntityId
                    && TargetExists == other.TargetExists
                    && TargetVisible == other.TargetVisible
                    && TargetDead == other.TargetDead
                    && LineOfSightBlocked == other.LineOfSightBlocked
                    && InAttackRange == other.InAttackRange;
            }
        }

        private readonly struct TraceDecisionTimelineState :
            IEquatable<TraceDecisionTimelineState>
        {
            public TraceDecisionTimelineState(
                in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
                in CharacterTestRuntimeAiPlannerAgentSnapshot agent)
            {
                CharacterTestRuntimeAiLabCommitTrace commit = trace.Commit;
                CardStableId = commit.CardStableId ?? string.Empty;
                StageStableId = commit.StageStableId ?? string.Empty;
                MainActionStableId = commit.MainActionStableId ?? string.Empty;
                SelectedCandidateStableId = FindSelectedCandidateStableId(trace);
                DecisionBasisLabel = ResolveDecisionBasisLabel(trace, agent);
                PlannerLogicKind = commit.PlannerLogicKind;
                CommandEnqueueKind = commit.CommandEnqueueKind;
                FallbackActionKind = agent.SelectedActionKind;
                ReasonCode = NonEmptyTimelineValue(
                    commit.PlannerReasonCode.ToString(CultureInfo.InvariantCulture),
                    NonEmptyTimelineValue(commit.CommandReasonCode.ToString(CultureInfo.InvariantCulture), commit.ReasonCode.ToString(CultureInfo.InvariantCulture)));
            }

            public string CardStableId { get; }
            public string StageStableId { get; }
            public string MainActionStableId { get; }
            public string SelectedCandidateStableId { get; }
            public string DecisionBasisLabel { get; }
            public CharacterTestRuntimeAiLabPlannerLogicCommitKind PlannerLogicKind { get; }
            public CharacterTestRuntimeAiLabCommandEnqueueKind CommandEnqueueKind { get; }
            public CharacterTestRuntimeAiPlannerActionKind FallbackActionKind { get; }
            public string ReasonCode { get; }
            public bool IsFailure =>
                PlannerLogicKind == CharacterTestRuntimeAiLabPlannerLogicCommitKind.Rejected
                || CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected
                || string.Equals(
                    SelectedCandidateStableId,
                    "planner.fallback.no-executable-action",
                    StringComparison.Ordinal);

            public bool Equals(TraceDecisionTimelineState other)
            {
                return string.Equals(CardStableId, other.CardStableId, StringComparison.Ordinal)
                    && string.Equals(StageStableId, other.StageStableId, StringComparison.Ordinal)
                    && string.Equals(MainActionStableId, other.MainActionStableId, StringComparison.Ordinal)
                    && string.Equals(
                        SelectedCandidateStableId,
                        other.SelectedCandidateStableId,
                        StringComparison.Ordinal)
                    && PlannerLogicKind == other.PlannerLogicKind
                    && CommandEnqueueKind == other.CommandEnqueueKind
                    && FallbackActionKind == other.FallbackActionKind
                    && string.Equals(ReasonCode, other.ReasonCode, StringComparison.Ordinal)
                    && IsFailure == other.IsFailure;
            }
        }

        private readonly struct RuntimeEffectTimelineState :
            IEquatable<RuntimeEffectTimelineState>
        {
            public RuntimeEffectTimelineState(
                in CharacterTestRuntimeAiLabCommitTrace commit)
            {
                Kind = commit.RuntimeEffectKind;
                CardStableId = commit.CardStableId ?? string.Empty;
                StageStableId = commit.StageStableId ?? string.Empty;
                BehaviorActionStableId = string.IsNullOrEmpty(commit.RuntimeEffectActionStableId)
                    ? commit.MainActionStableId ?? string.Empty
                    : commit.RuntimeEffectActionStableId;
                ReasonCode = commit.RuntimeEffectReasonCode.ToString(CultureInfo.InvariantCulture);
                CommandEnqueueKind = commit.CommandEnqueueKind;
                CommandKind = commit.CommandKind;
            }

            public CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind Kind { get; }
            public string CardStableId { get; }
            public string StageStableId { get; }
            public string BehaviorActionStableId { get; }
            public string ReasonCode { get; }
            public CharacterTestRuntimeAiLabCommandEnqueueKind CommandEnqueueKind { get; }
            public CharacterTestRuntimeAiLabDecisionTraceCommandKind CommandKind { get; }
            public bool HasEvidence =>
                Kind != CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved
                || CommandEnqueueKind != CharacterTestRuntimeAiLabCommandEnqueueKind.NotRequested;

            public bool Equals(RuntimeEffectTimelineState other)
            {
                return Kind == other.Kind
                    && string.Equals(CardStableId, other.CardStableId, StringComparison.Ordinal)
                    && string.Equals(StageStableId, other.StageStableId, StringComparison.Ordinal)
                    && string.Equals(BehaviorActionStableId, other.BehaviorActionStableId, StringComparison.Ordinal)
                    && string.Equals(ReasonCode, other.ReasonCode, StringComparison.Ordinal)
                    && CommandEnqueueKind == other.CommandEnqueueKind
                    && CommandKind == other.CommandKind;
            }
        }

        private readonly struct TraceActionTimelineState :
            IEquatable<TraceActionTimelineState>
        {
            public TraceActionTimelineState(
                in CharacterTestRuntimeAiLabCommitTrace commit,
                in CharacterTestRuntimeAiPlannerAgentSnapshot agent)
            {
                CardStableId = commit.CardStableId ?? string.Empty;
                MainActionStableId = commit.MainActionStableId ?? string.Empty;
                CommandKind = commit.CommandKind;
                CommandActionId = commit.CommandActionId;
                CommandEnqueueKind = commit.CommandEnqueueKind;
                FallbackActionId = agent.SelectedActionId;
                FallbackActionKind = agent.SelectedActionKind;
                FallbackActionName = agent.SelectedActionName ?? string.Empty;
                ReasonCode = NonEmptyTimelineValue(commit.CommandReasonCode.ToString(CultureInfo.InvariantCulture), commit.ReasonCode.ToString(CultureInfo.InvariantCulture));
            }

            public string CardStableId { get; }
            public string MainActionStableId { get; }
            public CharacterTestRuntimeAiLabDecisionTraceCommandKind CommandKind { get; }
            public int CommandActionId { get; }
            public CharacterTestRuntimeAiLabCommandEnqueueKind CommandEnqueueKind { get; }
            public int FallbackActionId { get; }
            public CharacterTestRuntimeAiPlannerActionKind FallbackActionKind { get; }
            public string FallbackActionName { get; }
            public string ReasonCode { get; }
            public bool HasAction =>
                !string.IsNullOrEmpty(MainActionStableId)
                || CommandKind != CharacterTestRuntimeAiLabDecisionTraceCommandKind.None
                || FallbackActionId != 0
                || !string.IsNullOrEmpty(FallbackActionName);

            public bool Equals(TraceActionTimelineState other)
            {
                return string.Equals(CardStableId, other.CardStableId, StringComparison.Ordinal)
                    && string.Equals(MainActionStableId, other.MainActionStableId, StringComparison.Ordinal)
                    && CommandKind == other.CommandKind
                    && CommandActionId == other.CommandActionId
                    && CommandEnqueueKind == other.CommandEnqueueKind
                    && FallbackActionId == other.FallbackActionId
                    && FallbackActionKind == other.FallbackActionKind
                    && string.Equals(ReasonCode, other.ReasonCode, StringComparison.Ordinal)
                    && string.Equals(FallbackActionName, other.FallbackActionName, StringComparison.Ordinal);
            }
        }

        private readonly struct TraceWarningTimelineState :
            IEquatable<TraceWarningTimelineState>
        {
            private TraceWarningTimelineState(
                string category,
                string label,
                string detail,
                string sourceTraceId,
                CharacterTestRuntimeAiLabTimelineSeverity severity)
            {
                Category = category ?? string.Empty;
                Label = label ?? string.Empty;
                Detail = detail ?? string.Empty;
                SourceTraceId = sourceTraceId ?? string.Empty;
                Severity = severity;
            }

            public string Category { get; }
            public string Label { get; }
            public string Detail { get; }
            public string SourceTraceId { get; }
            public CharacterTestRuntimeAiLabTimelineSeverity Severity { get; }

            public static bool TryCreate(
                in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
                out TraceWarningTimelineState state)
            {
                if (trace.Overflowed)
                {
                    CharacterTestRuntimeAiLabDecisionTraceOverflow overflow = trace.Overflow;
                    state = new TraceWarningTimelineState(
                        "DecisionTraceOverflow",
                        "追踪数据溢出",
                        "本帧诊断事件过多，仅保留了 "
                            + overflow.Limit.ToString(CultureInfo.InvariantCulture)
                            + " 条",
                        overflow.Region.ToString(),
                        CharacterTestRuntimeAiLabTimelineSeverity.Error);
                    return true;
                }

                CharacterTestRuntimeAiLabCommitTrace commit = trace.Commit;
                if (commit.CommandEnqueueKind == CharacterTestRuntimeAiLabCommandEnqueueKind.Rejected)
                {
                    string reason = NonEmptyTimelineValue(
                        commit.CommandReasonCode.ToString(CultureInfo.InvariantCulture),
                        commit.ReasonCode.ToString(CultureInfo.InvariantCulture));
                    state = new TraceWarningTimelineState(
                        "CommandRejected",
                        "命令被拒绝",
                        HumanizeTimelineReasonCode(reason),
                        reason,
                        CharacterTestRuntimeAiLabTimelineSeverity.Error);
                    return true;
                }
                if (commit.RuntimeEffectKind == CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Failed)
                {
                    string reason = NonEmptyTimelineValue(
                        commit.RuntimeEffectReasonCode.ToString(CultureInfo.InvariantCulture),
                        commit.ReasonCode.ToString(CultureInfo.InvariantCulture));
                    state = new TraceWarningTimelineState(
                        "RuntimeEffectFailed",
                        "执行失败",
                        HumanizeTimelineReasonCode(reason),
                        reason,
                        CharacterTestRuntimeAiLabTimelineSeverity.Error);
                    return true;
                }
                if (commit.PlannerLogicKind == CharacterTestRuntimeAiLabPlannerLogicCommitKind.Rejected)
                {
                    string reason = NonEmptyTimelineValue(
                        commit.PlannerReasonCode.ToString(CultureInfo.InvariantCulture),
                        commit.ReasonCode.ToString(CultureInfo.InvariantCulture));
                    state = new TraceWarningTimelineState(
                        "PlannerRejected",
                        "决策被拒绝",
                        HumanizeTimelineReasonCode(reason),
                        reason,
                        CharacterTestRuntimeAiLabTimelineSeverity.Error);
                    return true;
                }

                for (int i = 0; i < trace.Candidates.Count; i++)
                {
                    CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = trace.Candidates[i];
                    if (candidate.State
                            != CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected
                        || !string.Equals(
                            candidate.StableId,
                            "planner.fallback.no-executable-action",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    state = new TraceWarningTimelineState(
                        "NoExecutableAction",
                        "无可执行行为",
                        "当前事实没有满足任何主动作条件",
                        candidate.StableId,
                        CharacterTestRuntimeAiLabTimelineSeverity.Warning);
                    return true;
                }

                state = default;
                return false;
            }

            public bool Equals(TraceWarningTimelineState other)
            {
                return string.Equals(Category, other.Category, StringComparison.Ordinal)
                    && string.Equals(Label, other.Label, StringComparison.Ordinal)
                    && string.Equals(Detail, other.Detail, StringComparison.Ordinal)
                    && string.Equals(SourceTraceId, other.SourceTraceId, StringComparison.Ordinal)
                    && Severity == other.Severity;
            }
        }
    }
}
