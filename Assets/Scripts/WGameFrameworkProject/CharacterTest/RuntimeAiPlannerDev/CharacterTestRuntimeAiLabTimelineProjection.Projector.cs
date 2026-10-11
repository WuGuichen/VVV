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
        private const int MaxVisibleItems = 256;
        // Marker glyphs use a zoom-dependent 10-18 px footprint. Keeping projected
        // marker centers at least 22 px apart prevents separate aggregates from
        // colliding even in the detailed presentation mode.
        private const float ClusterDistancePixels = 22f;
        private readonly List<CharacterTestRuntimeAiLabTimelineItem> _revisionItems = new List<CharacterTestRuntimeAiLabTimelineItem>(64);
        private readonly List<CharacterTestRuntimeAiLabTimelineItem> _perceptionItems = new List<CharacterTestRuntimeAiLabTimelineItem>(64);
        private readonly List<CharacterTestRuntimeAiLabTimelineItem> _decisionItems = new List<CharacterTestRuntimeAiLabTimelineItem>(128);
        private readonly List<CharacterTestRuntimeAiLabTimelineItem> _actionStateItems = new List<CharacterTestRuntimeAiLabTimelineItem>(128);
        private readonly List<CharacterTestRuntimeAiLabTimelineItem> _resultItems = new List<CharacterTestRuntimeAiLabTimelineItem>(128);
        private readonly List<CharacterTestRuntimeAiLabTimelineItem> _eventItems = new List<CharacterTestRuntimeAiLabTimelineItem>(256);
        private readonly List<CharacterTestRuntimeAiLabTimelineItem> _warningItems = new List<CharacterTestRuntimeAiLabTimelineItem>(64);
        private readonly List<CharacterTestRuntimeAiLabTimelineItem> _stackScratch = new List<CharacterTestRuntimeAiLabTimelineItem>(256);
        private readonly List<CharacterTestRuntimeAiLabTimelineItem> _aggregateScratch = new List<CharacterTestRuntimeAiLabTimelineItem>(256);
        private readonly List<CharacterTestRuntimeAiLabTimelineItem> _limitScratch = new List<CharacterTestRuntimeAiLabTimelineItem>(256);
        private readonly List<CanonicalTimelineSample> _decisionSamples = new List<CanonicalTimelineSample>(64);
        private readonly List<CanonicalTimelineSample> _eventSamples = new List<CanonicalTimelineSample>(256);
        private readonly List<CanonicalTimelineSample> _warningSamples = new List<CanonicalTimelineSample>(64);
        private readonly List<CanonicalTimelineSample> _openActionSamples = new List<CanonicalTimelineSample>(64);
        private readonly List<CanonicalTimelineSample> _continuousCommandSamples =
            new List<CanonicalTimelineSample>(256);
        private readonly List<OpenActionSample> _openActions = new List<OpenActionSample>(64);
        private readonly StringBuilder _stableKeyBuilder = new StringBuilder(192);

        public CharacterTestRuntimeAiLabTimelineProjection Build(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex eventIndex,
            int frameLimit,
            int selectedAgentEntityId,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            CharacterTestRuntimeAiLabRevisionProjectionInput revisionInput = null,
            int committedSelectedFrame = 0,
            bool hasCommittedSelection = false)
        {
            CharacterTestRuntimeAiLabTimelineDomain domain = BuildDomain(
                timeline,
                frameLimit,
                committedSelectedFrame,
                hasCommittedSelection);
            long contentRevision = ((timeline?.ContentRevision ?? 0L) << 32)
                ^ (eventIndex?.ContentRevision ?? 0L);
            if (revisionInput != null)
                contentRevision ^= revisionInput.ContentRevision;
            if (!domain.HasFrames)
            {
                return new CharacterTestRuntimeAiLabTimelineProjection(
                    contentRevision,
                    domain,
                    viewport,
                    CreateEmptyTracks(),
                    "尚未捕获运行时帧",
                    CharacterTestRuntimeAiLabTimelineValidity.NoFrames);
            }

            CharacterTestRuntimeAiLabTimelineValidity validity = ValidateProjectionInputs(
                timeline,
                eventIndex,
                domain,
                out int diagnosticFrame,
                out int diagnosticValue,
                out string diagnosticMessage);
            bool eventTracksAvailable = validity == CharacterTestRuntimeAiLabTimelineValidity.Valid;
            var tracks = new CharacterTestRuntimeAiLabTimelineTrackProjection[5];
            CharacterTestRuntimeAiLabTimelineTrackProjection revisionTrack = BuildRevisionTrack(
                timeline,
                revisionInput,
                domain,
                viewport,
                out CharacterTestRuntimeAiLabTimelineValidity revisionValidity,
                out int revisionDiagnosticFrame,
                out string revisionDiagnosticMessage);
            tracks[0] = selectedAgentEntityId > 0
                ? BuildPerceptionTrack(
                    timeline,
                    selectedAgentEntityId,
                    domain,
                    viewport)
                : revisionTrack;
            tracks[1] = BuildDecisionTrack(
                timeline,
                eventTracksAvailable ? eventIndex : null,
                selectedAgentEntityId,
                domain,
                viewport);
            tracks[2] = eventTracksAvailable
                ? BuildActionTrack(
                    timeline,
                    eventIndex,
                    domain,
                    viewport,
                    selectedAgentEntityId)
                : UnavailableTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Action, "动作", diagnosticMessage);
            tracks[3] = eventTracksAvailable
                ? BuildResultTrack(
                    timeline,
                    eventIndex,
                    domain,
                    viewport,
                    selectedAgentEntityId)
                : UnavailableTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Result, "结果", diagnosticMessage);
            tracks[4] = eventTracksAvailable
                ? BuildWarningTrack(timeline, eventIndex, domain, viewport, selectedAgentEntityId)
                : UnavailableTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Warnings, "警告", diagnosticMessage);
            EnforceGlobalItemLimit(tracks, viewport);
            CharacterTestRuntimeAiLabTimelineValidity projectionValidity = validity;
            int projectionDiagnosticFrame = diagnosticFrame;
            int projectionDiagnosticValue = diagnosticValue;
            string projectionMessage = diagnosticMessage;
            if (revisionInput != null
                && projectionValidity == CharacterTestRuntimeAiLabTimelineValidity.Valid
                && revisionValidity != CharacterTestRuntimeAiLabTimelineValidity.Valid)
            {
                projectionValidity = revisionValidity;
                projectionDiagnosticFrame = revisionDiagnosticFrame;
                projectionDiagnosticValue = 0;
                projectionMessage = revisionDiagnosticMessage;
            }
            return new CharacterTestRuntimeAiLabTimelineProjection(
                contentRevision,
                domain,
                viewport,
                tracks,
                projectionMessage,
                projectionValidity,
                projectionDiagnosticFrame,
                projectionDiagnosticValue);
        }

        private CharacterTestRuntimeAiLabTimelineTrackProjection BuildRevisionTrack(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabRevisionProjectionInput input,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            out CharacterTestRuntimeAiLabTimelineValidity validity,
            out int diagnosticFrame,
            out string diagnosticMessage)
        {
            validity = CharacterTestRuntimeAiLabTimelineValidity.Valid;
            diagnosticFrame = 0;
            diagnosticMessage = string.Empty;
            if (input == null)
            {
                validity = CharacterTestRuntimeAiLabTimelineValidity.RevisionHistoryMismatch;
                diagnosticMessage = "尚未提供 RuntimeFrame 对齐的 revision history。";
                return UnavailableTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Revision, "版本", diagnosticMessage);
            }
            if (!timeline.SessionIdentity.Equals(input.SessionIdentity))
            {
                validity = CharacterTestRuntimeAiLabTimelineValidity.SessionMismatch;
                diagnosticMessage = "Revision history 与 RuntimeFrame 不属于同一会话。";
                return UnavailableTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Revision, "版本", diagnosticMessage);
            }
            if (input.Validity != CharacterTestRuntimeAiLabTimelineValidity.Valid)
            {
                validity = input.Validity;
                diagnosticFrame = input.DiagnosticFrame;
                diagnosticMessage = input.DiagnosticMessage;
                return UnavailableTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Revision, "版本", diagnosticMessage);
            }
            if (input.FrameCount != timeline.Count)
            {
                validity = CharacterTestRuntimeAiLabTimelineValidity.RevisionRecordMissing;
                diagnosticMessage = "Revision history 与 retained RuntimeFrame 数量不一致。";
                return UnavailableTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Revision, "版本", diagnosticMessage);
            }

            List<CharacterTestRuntimeAiLabTimelineItem> items = _revisionItems;
            items.Clear();
            int spanStartIndex = 0;
            int sourceOrder = 0;
            for (int frameIndex = 0; frameIndex < input.FrameCount; frameIndex++)
            {
                CharacterTestRuntimeAiLabRevisionFrame revisionFrame = input.FrameAt(frameIndex);
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.Records[frameIndex];
                if (revisionFrame.Frame != record.Frame
                    || revisionFrame.RecordGeneration != record.RecordGeneration
                    || revisionFrame.AuthorityHash != record.StateHash)
                {
                    validity = CharacterTestRuntimeAiLabTimelineValidity.RevisionHistoryMismatch;
                    diagnosticFrame = record.Frame;
                    diagnosticMessage = "Revision history 与 retained RuntimeFrame identity 不一致。";
                    return UnavailableTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Revision, "版本", diagnosticMessage);
                }

                if (frameIndex > 0)
                {
                    CharacterTestRuntimeAiLabRevisionFrame previous = input.FrameAt(frameIndex - 1);
                    if (revisionFrame.Frame != previous.Frame + 1
                        || revisionFrame.AppliedRevisionHash != previous.AppliedRevisionHash)
                    {
                        AddRevisionSpan(items, input, spanStartIndex, frameIndex - 1, sourceOrder++, timeline.SessionIdentity.Generation);
                        spanStartIndex = frameIndex;
                    }
                }

                for (int changeIndex = 0; changeIndex < revisionFrame.ChangeCount; changeIndex++)
                {
                    CharacterTestRuntimeAiLabRevisionChange change =
                        input.ChangeAt(revisionFrame.ChangeStart + changeIndex);
                    string label = "行为配置已更新";
                    string tooltip = "该帧开始使用新的行为配置。";
                    items.Add(new CharacterTestRuntimeAiLabTimelineItem(
                        "revision-marker:" + timeline.SessionIdentity.Generation.ToString(CultureInfo.InvariantCulture)
                            + ":" + record.RecordGeneration.ToString(CultureInfo.InvariantCulture)
                            + ":" + record.Frame.ToString(CultureInfo.InvariantCulture)
                            + ":" + change.ProfileSlot.ToString(CultureInfo.InvariantCulture)
                            + ":" + change.Revision.ToString(CultureInfo.InvariantCulture)
                            + ":" + changeIndex.ToString(CultureInfo.InvariantCulture),
                        CharacterTestRuntimeAiLabTimelineTrackKind.Revision,
                        CharacterTestRuntimeAiLabTimelineItemKind.Marker,
                        CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                        record.Frame,
                        record.Frame,
                        false,
                        0,
                        0,
                        "AppliedRevision",
                        CharacterTestRuntimeAiLabTimelineSeverity.Success,
                        label,
                        tooltip,
                        1,
                        string.Empty,
                        sourceOrder++,
                        behaviorEntryId: change.GroupDraftId,
                        occurrenceRank: changeIndex,
                        profileSlot: change.ProfileSlot,
                        sessionGeneration: timeline.SessionIdentity.Generation,
                        recordGeneration: record.RecordGeneration,
                        authorityHash: record.StateHash));
                }
            }

            if (input.FrameCount > 0)
                AddRevisionSpan(items, input, spanStartIndex, input.FrameCount - 1, sourceOrder, timeline.SessionIdentity.Generation);
            TrimOutsideViewport(items, viewport);
            SortItems(items);
            items = AggregateMarkers(items, viewport);
            return new CharacterTestRuntimeAiLabTimelineTrackProjection(
                CharacterTestRuntimeAiLabTimelineTrackKind.Revision,
                "版本",
                items.ToArray());
        }

        private static void AddRevisionSpan(
            List<CharacterTestRuntimeAiLabTimelineItem> items,
            CharacterTestRuntimeAiLabRevisionProjectionInput input,
            int startIndex,
            int endIndex,
            int sourceOrder,
            int sessionGeneration)
        {
            CharacterTestRuntimeAiLabRevisionFrame first = input.FrameAt(startIndex);
            CharacterTestRuntimeAiLabRevisionFrame last = input.FrameAt(endIndex);
            string hash = first.AppliedRevisionHash.ToString("X16", CultureInfo.InvariantCulture);
            items.Add(new CharacterTestRuntimeAiLabTimelineItem(
                "revision-span:" + sessionGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + first.RecordGeneration.ToString(CultureInfo.InvariantCulture)
                    + ":" + first.Frame.ToString(CultureInfo.InvariantCulture)
                    + ":" + last.Frame.ToString(CultureInfo.InvariantCulture)
                    + ":" + hash,
                CharacterTestRuntimeAiLabTimelineTrackKind.Revision,
                CharacterTestRuntimeAiLabTimelineItemKind.Span,
                CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord,
                first.Frame,
                last.Frame,
                false,
                0,
                0,
                "AppliedRevisionState",
                CharacterTestRuntimeAiLabTimelineSeverity.Info,
                first.AppliedRevisionHash == 0L ? "基础配置" : "已应用配置",
                "当前观测区间使用同一套行为配置。",
                1,
                string.Empty,
                sourceOrder,
                sessionGeneration: sessionGeneration,
                recordGeneration: first.RecordGeneration,
                authorityHash: first.AuthorityHash));
        }

        private static CharacterTestRuntimeAiLabTimelineDomain BuildDomain(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            int frameLimit,
            int committedSelectedFrame,
            bool hasCommittedSelection)
        {
            if (timeline == null || timeline.Count <= 0)
                return new CharacterTestRuntimeAiLabTimelineDomain(0, 0, 0, 0, frameLimit, 0);
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = timeline.SessionIdentity;
            int sessionStart = identity.IsValid
                ? identity.SessionStartFrame
                : timeline.Records[0].Frame;
            return new CharacterTestRuntimeAiLabTimelineDomain(
                sessionStart,
                timeline.Records[0].Frame,
                timeline.LatestFrame,
                hasCommittedSelection ? committedSelectedFrame : timeline.SelectedFrame,
                identity.IsValid ? identity.FrameLimit : frameLimit,
                timeline.Count);
        }

        private static CharacterTestRuntimeAiLabTimelineValidity ValidateProjectionInputs(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex eventIndex,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            out int diagnosticFrame,
            out int diagnosticValue,
            out string diagnosticMessage)
        {
            diagnosticFrame = 0;
            diagnosticValue = 0;
            if (timeline == null || eventIndex == null
                || !timeline.SessionIdentity.IsValid
                || !timeline.SessionIdentity.Equals(eventIndex.SessionIdentity))
            {
                diagnosticMessage = "RuntimeFrame 与 typed event 不属于同一会话。";
                return CharacterTestRuntimeAiLabTimelineValidity.SessionMismatch;
            }

            for (int frame = domain.RetainedStartFrame; frame <= domain.LatestFrame; frame++)
            {
                if (!eventIndex.IsFrameCaptured(frame))
                    continue;
                int overflow = eventIndex.GetOverflowCount(frame);
                if (overflow > 0)
                {
                    diagnosticFrame = frame;
                    diagnosticValue = overflow;
                    diagnosticMessage = "Frame " + frame.ToString(CultureInfo.InvariantCulture)
                        + " 的 typed event 已溢出，丢失 " + overflow.ToString(CultureInfo.InvariantCulture) + " 项。";
                    return CharacterTestRuntimeAiLabTimelineValidity.EventSlotOverflow;
                }
                int count = eventIndex.GetCount(frame);
                for (int index = 0; index < count; index++)
                {
                    CharacterTestRuntimeAiLabTimelineEventSample sample = eventIndex.GetSample(frame, index);
                    if (sample.Frame == frame
                        && IsKnownEventKind(sample.Kind)
                        && IsKnownSeverity(sample.Severity))
                        continue;
                    diagnosticFrame = frame;
                    diagnosticMessage = "Frame " + frame.ToString(CultureInfo.InvariantCulture)
                        + " 的 typed event slot 已损坏。";
                    return CharacterTestRuntimeAiLabTimelineValidity.CorruptEventSlot;
                }
            }

            int sessionEnd = timeline.SessionIdentity.SessionStartFrame + timeline.SessionIdentity.FrameLimit - 1;
            for (int frame = domain.LatestFrame + 1; frame <= sessionEnd; frame++)
            {
                if (!eventIndex.IsFrameCaptured(frame))
                    continue;
                diagnosticFrame = frame;
                diagnosticMessage = "检测到 latest 之后的已采集 typed event slot。";
                return CharacterTestRuntimeAiLabTimelineValidity.EventAfterLatest;
            }

            int previousFrame = -1;
            for (int recordIndex = 0; recordIndex < timeline.Count; recordIndex++)
            {
                int frame = timeline.Records[recordIndex].Frame;
                if (!eventIndex.IsFrameCaptured(frame))
                {
                    diagnosticFrame = frame;
                    diagnosticMessage = "RuntimeFrame 缺少对应的 typed event capture slot。";
                    return CharacterTestRuntimeAiLabTimelineValidity.UncapturedRetainedFrame;
                }
                if (previousFrame >= 0 && frame != previousFrame + 1)
                {
                    diagnosticFrame = previousFrame + 1;
                    diagnosticValue = frame - previousFrame - 1;
                    diagnosticMessage = "RuntimeFrame 历史存在缺口 "
                        + diagnosticFrame.ToString(CultureInfo.InvariantCulture) + ".."
                        + (frame - 1).ToString(CultureInfo.InvariantCulture) + "。";
                    return CharacterTestRuntimeAiLabTimelineValidity.MissingRuntimeFrameRange;
                }
                previousFrame = frame;
            }

            diagnosticMessage = string.Empty;
            return CharacterTestRuntimeAiLabTimelineValidity.Valid;
        }

        private static bool IsKnownEventKind(CharacterTestRuntimeAiLabTimelineEventKind kind)
        {
            return kind >= CharacterTestRuntimeAiLabTimelineEventKind.Command
                && kind <= CharacterTestRuntimeAiLabTimelineEventKind.BehaviorCardEnded;
        }

        private static bool IsKnownSeverity(CharacterTestRuntimeAiLabTimelineSeverity severity)
        {
            return severity >= CharacterTestRuntimeAiLabTimelineSeverity.Info
                && severity <= CharacterTestRuntimeAiLabTimelineSeverity.Error;
        }

        private static CharacterTestRuntimeAiLabTimelineTrackProjection[] CreateEmptyTracks()
        {
            return new[]
            {
                EmptyTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Perception, "感知"),
                EmptyTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Decision, "决策"),
                EmptyTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Action, "动作"),
                EmptyTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Result, "结果"),
                EmptyTrack(CharacterTestRuntimeAiLabTimelineTrackKind.Warnings, "警告")
            };
        }

        private static CharacterTestRuntimeAiLabTimelineTrackProjection EmptyTrack(
            CharacterTestRuntimeAiLabTimelineTrackKind kind,
            string label)
        {
            return new CharacterTestRuntimeAiLabTimelineTrackProjection(
                kind,
                label,
                Array.Empty<CharacterTestRuntimeAiLabTimelineItem>());
        }

        private static CharacterTestRuntimeAiLabTimelineTrackProjection UnavailableTrack(
            CharacterTestRuntimeAiLabTimelineTrackKind kind,
            string label,
            string message)
        {
            return new CharacterTestRuntimeAiLabTimelineTrackProjection(
                kind,
                label,
                Array.Empty<CharacterTestRuntimeAiLabTimelineItem>(),
                false,
                message);
        }

    }
}
