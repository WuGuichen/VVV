using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private static readonly Unity.Profiling.ProfilerMarker FrameCaptureMarker =
            new Unity.Profiling.ProfilerMarker("CharacterTest.RuntimeAiLab.CaptureFrame");
        internal const int DefaultRuntimeAiLabFrameLimit = 900;
        private const int MinimumReadableTimelineSpan = 90;
        private const float TimelineProjectionPixelWidth = 1732f;
        private static readonly int[] TimelineZoomSpans = { 120, 300, 600, 900, 1800 };
        private static readonly int[] FrameLimitPresets = { 300, 600, 900, 1800 };

        private readonly CharacterTestRuntimeAiLabTimelineEventIndex _timelineEventIndex =
            new CharacterTestRuntimeAiLabTimelineEventIndex();
        private readonly CharacterTestRuntimeAiLabTimelineProjector _timelineProjector =
            new CharacterTestRuntimeAiLabTimelineProjector();
        private readonly CharacterTestRuntimeAiLabLogicEventBuffer _selectedFrameLogicEvents =
            new CharacterTestRuntimeAiLabLogicEventBuffer(CharacterTestRuntimeAiLabTimelineEventIndex.MaxEventsPerFrame);
        private readonly CharacterTestRuntimeAiLabLogicEventBuffer _capturedBehaviorLogicEvents =
            new CharacterTestRuntimeAiLabLogicEventBuffer(CharacterTestRuntimeAiLabTimelineEventIndex.MaxEventsPerFrame);
        private int _frameLimit = DefaultRuntimeAiLabFrameLimit;
        private bool _timelineSessionStarted;
        private int _timelineSessionGeneration;
        private bool _sessionContinuable = true;
        private int _timelineZoomLevel = -1;
        private int _timelineViewStartOverride = int.MinValue;
        private bool? _timelineExpandedOverride;
        private CharacterTestRuntimeAiLabTimelineProjection _cachedTimelineProjection;
        private long _cachedTimelineRevision = long.MinValue;
        private int _cachedTimelineAgentId;
        private int _cachedTimelineSelectedFrame = int.MinValue;
        private CharacterTestRuntimeAiLabTimelineViewport _cachedTimelineViewport;
        private float _lastTimelineProjectionBuildTime = float.NegativeInfinity;
        private bool _timelineScrubActive;
        private bool _timelineScrubStartedFromHistory;
        private bool _hasTimelineScrubDesiredFrame;
        private int _timelineScrubDesiredFrame;
        private int _futureAdvanceStartFrame;
        private float _lastFutureAdvancePresentationTime = float.NegativeInfinity;
        private CharacterTestRuntimeAiLabTimelineItem _selectedTimelineItem;
        private string _selectedTimelineVisualStableKey = string.Empty;

        internal int FrameLimit => _frameLimit;
        internal bool SessionCompleted => _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Completed;
        internal bool SessionContinuable => _sessionContinuable;
        internal int TimelineZoomLevel => _timelineZoomLevel;
        internal bool TimelineExpanded => _timelineExpandedOverride
            ?? (_context != null && _context.Paused);

        internal CharacterTestRuntimeAiLabLogicEventBuffer GetBehaviorLogicEventsForFrame(int frame)
        {
            _selectedFrameLogicEvents.Clear();
            int count = _timelineEventIndex.GetCount(frame);
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabTimelineEventSample sample = _timelineEventIndex.GetSample(frame, i);
                if (!TryConvertLogicTimelineSample(sample, out CharacterTestRuntimeAiLabLogicEvent logicEvent))
                    continue;
                _selectedFrameLogicEvents.Add(logicEvent);
            }
            return _selectedFrameLogicEvents;
        }

        internal static bool TryConvertLogicTimelineSample(
            in CharacterTestRuntimeAiLabTimelineEventSample sample,
            out CharacterTestRuntimeAiLabLogicEvent logicEvent)
        {
            logicEvent = default;
            if (!TryMapLogicTimelineKind(sample, out CharacterTestRuntimeAiLabLogicEventKind kind))
                return false;
            string detectorId = kind == CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered
                ? sample.Label
                : string.Empty;
            string transitionId = kind == CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered
                || kind == CharacterTestRuntimeAiLabLogicEventKind.TransitionPending
                || kind == CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted
                || kind == CharacterTestRuntimeAiLabLogicEventKind.ActionInterruptRequested
                    ? sample.SourceTraceLabel
                    : string.Empty;
            logicEvent = new CharacterTestRuntimeAiLabLogicEvent(
                sample.Frame,
                kind,
                sample.BehaviorStepId,
                detectorId,
                transitionId,
                sample.EntityId,
                sample.BehaviorEntryId,
                sample.InstanceId,
                sample.ProfileSlot,
                sample.CardInstanceStartFrame,
                sample.BehaviorTargetTimelineId,
                sample.BehaviorLocalFrame,
                sample.BehaviorPrimaryActionInstanceId,
                sample.BehaviorReasonCode,
                sample.BehaviorTriggerSourceKind,
                sample.BehaviorOutcomeKind, sample.BehaviorEntryNumericId, sample.BehaviorStepIndex, sample.BehaviorEventIndex, sample.BehaviorTransitionIndex, sample.BehaviorTargetTimelineIndex);
            return true;
        }

        /// <remarks>NoAlloc. Aggregates current-frame typed logic events in stable profile-slot order.</remarks>
        private CharacterTestRuntimeAiLabLogicEventBuffer CollectLatestBehaviorLogicEvents(
            CharacterTestRuntimeAiPlannerCommandModule module)
        {
            _capturedBehaviorLogicEvents.Clear();
            if (module == null)
                return _capturedBehaviorLogicEvents;
            for (int slot = 0; slot < module.BehaviorLogicEventSlotCount; slot++)
            {
                CharacterTestRuntimeAiLabLogicEventBuffer source =
                    module.GetLatestBehaviorLogicEventsForSlot(slot);
                if (source == null)
                    continue;
                for (int i = 0; i < source.Count; i++)
                    _capturedBehaviorLogicEvents.Add(source[i]);
                _capturedBehaviorLogicEvents.AddOverflow(source.OverflowCount);
            }
            return _capturedBehaviorLogicEvents;
        }

        private static bool TryMapLogicTimelineKind(
            in CharacterTestRuntimeAiLabTimelineEventSample sample,
            out CharacterTestRuntimeAiLabLogicEventKind kind)
        {
            switch (sample.Kind)
            {
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorStageEntered:
                    kind = CharacterTestRuntimeAiLabLogicEventKind.StageEntered;
                    return true;
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorDetectorTriggered:
                    kind = CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered;
                    return true;
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionPending:
                    kind = string.Equals(
                        sample.Category,
                        "BehaviorLogic.ActionInterruptRequested",
                        StringComparison.Ordinal)
                            ? CharacterTestRuntimeAiLabLogicEventKind.ActionInterruptRequested
                            : CharacterTestRuntimeAiLabLogicEventKind.TransitionPending;
                    return true;
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionCommitted:
                    kind = CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted;
                    return true;
                case CharacterTestRuntimeAiLabTimelineEventKind.BehaviorCardEnded:
                    kind = CharacterTestRuntimeAiLabLogicEventKind.CardEnded;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }
        internal bool FutureAdvanceActive => _hasTimelineScrubDesiredFrame
            && _timeline != null
            && (_timelineScrubDesiredFrame > _timeline.LatestFrame
                || (_branchRecomputeArmed && _timelineScrubDesiredFrame > _branchBaseFrame))
            && _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running;
        internal int FutureAdvanceTargetFrame => _hasTimelineScrubDesiredFrame
            ? _timelineScrubDesiredFrame
            : 0;
        internal int FutureAdvanceStartFrame => _futureAdvanceStartFrame;
        internal string SelectedTimelineVisualStableKey => _selectedTimelineVisualStableKey;

        internal bool TryGetDecisionTrace(
            int frame,
            int entityId,
            out CharacterTestRuntimeAiLabDecisionTraceAgentView trace)
        {
            trace = default;
            return frame >= 0
                && entityId > 0
                && _timeline != null
                && _timeline.TryGetFrame(frame, out CharacterTestRuntimeAiPlannerDevTimelineRecord record)
                && _timeline.TryGetDecisionTrace(record, entityId, out trace);
        }

        internal bool TrySetFrameLimit(int frameLimit)
        {
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                _statusMessage = "帧上限只能在配置阶段修改。";
                return false;
            }

            int clamped = Math.Max(1, Math.Min(CharacterTestRuntimeAiPlannerDevTimeline.DefaultCapacity, frameLimit));
            if (_draftWorkspace != null)
            {
                bool changed = _draftWorkspace.FrameLimit != clamped;
                if (!_draftWorkspace.TrySetFrameLimit(clamped))
                {
                    _statusMessage = "当前工作区只读，无法保存帧上限。";
                    return false;
                }
                if (changed)
                    _draftPersistence.MarkDirty();
            }
            _frameLimit = clamped;
            _statusMessage = "本次实验帧上限已设为 "
                + clamped.ToString(CultureInfo.InvariantCulture)
                + "。";
            return true;
        }

        internal bool CycleFrameLimitPreset()
        {
            int next = FrameLimitPresets[0];
            for (int i = 0; i < FrameLimitPresets.Length; i++)
            {
                if (_frameLimit < FrameLimitPresets[i])
                {
                    next = FrameLimitPresets[i];
                    return TrySetFrameLimit(next);
                }
            }
            return TrySetFrameLimit(next);
        }

        internal bool ToggleTimelineExpanded()
        {
            if (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
                return false;
            _timelineExpandedOverride = !TimelineExpanded;
            _statusMessage = TimelineExpanded ? "时间轴已展开。" : "时间轴已收起。";
            return true;
        }

        /// <remarks>NoAlloc. Restores low-frequency timeline presentation state.</remarks>
        internal void RestoreTimelineExpanded(bool expanded)
        {
            _timelineExpandedOverride = expanded;
        }

        internal bool ZoomTimeline(int direction)
        {
            if (_timeline.Count <= 0 || direction == 0)
                return false;

            CharacterTestRuntimeAiLabTimelineViewport current = ResolveTimelineViewport();
            if (!TryResolveTimelineZoomLevel(current.FrameSpan, direction, out int nextZoomLevel))
            {
                _statusMessage = direction > 0
                    ? "时间轴已达到最大放大倍率。"
                    : "时间轴已达到最大缩小倍率。";
                return false;
            }

            _timelineZoomLevel = nextZoomLevel;
            _timelineViewStartOverride = int.MinValue;
            InvalidateTimelineProjection();
            _statusMessage = "时间轴可视跨度：" + TimelineZoomSpans[_timelineZoomLevel] + " 帧。";
            return true;
        }

        internal static bool CanZoomTimelineSpan(int currentSpan, int direction)
        {
            return TryResolveTimelineZoomLevel(currentSpan, direction, out _);
        }

        internal static bool TryResolveTimelineZoomLevel(
            int currentSpan,
            int direction,
            out int zoomLevel)
        {
            zoomLevel = -1;
            if (direction == 0)
                return false;

            int safeSpan = Math.Max(1, currentSpan);
            if (direction > 0)
            {
                for (int i = TimelineZoomSpans.Length - 1; i >= 0; i--)
                {
                    if (TimelineZoomSpans[i] >= safeSpan)
                        continue;
                    zoomLevel = i;
                    return true;
                }
                return false;
            }

            for (int i = 0; i < TimelineZoomSpans.Length; i++)
            {
                if (TimelineZoomSpans[i] <= safeSpan)
                    continue;
                zoomLevel = i;
                return true;
            }
            return false;
        }

        internal bool FitTimeline()
        {
            _timelineZoomLevel = -1;
            _timelineViewStartOverride = int.MinValue;
            InvalidateTimelineProjection();
            _statusMessage = "时间轴已适配完整实验。";
            return true;
        }

        internal bool PanTimeline(int direction)
        {
            if (_timeline.Count <= 0 || _timelineZoomLevel < 0 || direction == 0)
                return false;
            CharacterTestRuntimeAiLabTimelineViewport current = ResolveTimelineViewport();
            int delta = Math.Max(1, current.FrameSpan / 4) * Math.Sign(direction);
            _timelineViewStartOverride = ClampTimelineViewStart(current.StartFrame + delta, current.FrameSpan);
            InvalidateTimelineProjection();
            _statusMessage = "时间轴视窗已移动到帧 " + _timelineViewStartOverride + "。";
            return true;
        }

        internal CharacterTestRuntimeAiLabTimelineProjection BuildTimelineProjection()
        {
            int selectedAgentEntityId = ResolveSelectedAgentEntityId();
            CharacterTestRuntimeAiLabTimelineViewport viewport = ResolveTimelineViewport();
            long revision = ((_timeline?.ContentRevision ?? 0L) << 32)
                ^ _timelineEventIndex.ContentRevision;
            CharacterTestRuntimeAiLabCommittedFrameReceipt committedReceipt = _frameSelection.CommittedReceipt;
            int selectedFrame = committedReceipt.IsValid
                ? committedReceipt.Frame
                : _timeline?.SelectedFrame ?? 0;
            if (_cachedTimelineProjection != null
                && _cachedTimelineRevision == revision
                && _cachedTimelineAgentId == selectedAgentEntityId
                && _cachedTimelineSelectedFrame == selectedFrame
                && ViewportEquals(_cachedTimelineViewport, viewport))
            {
                return _cachedTimelineProjection;
            }

            // Live capture can advance every authority tick. Keep the immutable projection at
            // a UI-friendly 10 Hz while the simulation runs; paused/review selection remains immediate.
            if (_cachedTimelineProjection != null
                && _context != null
                && !_context.Paused
                && Time.unscaledTime - _lastTimelineProjectionBuildTime < 0.1f
                && _cachedTimelineAgentId == selectedAgentEntityId
                && ViewportEquals(_cachedTimelineViewport, viewport))
            {
                return _cachedTimelineProjection;
            }

            CharacterTestRuntimeAiLabRevisionProjectionInput revisionInput = BuildRevisionProjectionInput();
            _cachedTimelineProjection = _timelineProjector.Build(
                _timeline,
                _timelineEventIndex,
                _frameLimit,
                selectedAgentEntityId,
                viewport,
                revisionInput,
                selectedFrame,
                committedReceipt.IsValid);
            if (_selectedTimelineItem != null)
            {
                if (!_cachedTimelineProjection.TryFindItem(
                        _selectedTimelineItem.StableKey,
                        out CharacterTestRuntimeAiLabTimelineItem rebound)
                    || !CanRebindTimelineSelection(_selectedTimelineItem, rebound))
                {
                    ClearTimelineItemSelection();
                }
                else
                {
                    _selectedTimelineItem = rebound;
                }
            }
            _cachedTimelineRevision = revision;
            _cachedTimelineAgentId = selectedAgentEntityId;
            _cachedTimelineSelectedFrame = selectedFrame;
            _cachedTimelineViewport = viewport;
            _lastTimelineProjectionBuildTime = Time.unscaledTime;
            return _cachedTimelineProjection;
        }

        internal CharacterTestRuntimeAiLabRuntimeFramePageContext CaptureRuntimeFramePageContext()
        {
            return new CharacterTestRuntimeAiLabRuntimeFramePageContext(
                ResolveTimelineViewport(),
                _frameSelection.CommittedReceipt,
                _selectedTimelineItem?.CreateLocator() ?? default,
                _selectedTimelineItem != null);
        }

        internal bool RestoreRuntimeFramePageContext(
            CharacterTestRuntimeAiLabRuntimeFramePageContext context,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            CharacterTestRuntimeAiLabTimelineViewport viewport = context.Viewport;
            if (viewport.PixelWidth > 0f && viewport.EndFrame >= viewport.StartFrame)
            {
                _timelineZoomLevel = viewport.ZoomLevel;
                _timelineViewStartOverride = viewport.Mode == CharacterTestRuntimeAiLabTimelineViewportMode.FitRetained
                    ? int.MinValue
                    : viewport.StartFrame;
                InvalidateTimelineProjection();
            }

            CharacterTestRuntimeAiLabCommittedFrameReceipt saved = context.CommittedReceipt;
            CharacterTestRuntimeAiLabCommittedFrameReceipt current = _frameSelection.CommittedReceipt;
            if (saved.IsValid
                && (!current.IsValid
                    || saved.CommitGeneration != current.CommitGeneration
                    || saved.Bundle.SessionIdentity.Generation != current.Bundle.SessionIdentity.Generation
                    || saved.Bundle.RecordGeneration != current.Bundle.RecordGeneration
                    || saved.AuthorityHash != current.AuthorityHash))
            {
                ClearTimelineItemSelection();
                diagnostic = "RUNTIME_FRAME_CONTEXT_STALE";
                return false;
            }

            if (!context.DetailsVisible || !context.SelectedLocator.IsValid)
            {
                ClearTimelineItemSelection();
                return true;
            }
            if (!TryResolveTimelineItem(context.SelectedLocator, out CharacterTestRuntimeAiLabTimelineItem item, out bool member))
            {
                ClearTimelineItemSelection();
                diagnostic = "RUNTIME_FRAME_LOCATOR_STALE";
                return false;
            }
            _selectedTimelineItem = item;
            _selectedTimelineVisualStableKey = member ? string.Empty : item.StableKey;
            return true;
        }

        internal bool SelectTimelineItem(string stableKey)
        {
            if (!TryResolveTimelineItem(stableKey, out CharacterTestRuntimeAiLabTimelineItem item, out bool member))
            {
                _statusMessage = "时间轴事件已更新，请重新选择。";
                return false;
            }

            _selectedTimelineItem = item;
            if (!member)
                _selectedTimelineVisualStableKey = item.StableKey;
            _statusMessage = item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack
                ? "已选择同帧事件堆叠；可展开并定位精确 typed leaf。"
                : item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate
                    ? "已选择跨帧密度聚合；双击仅缩放并居中。"
                : "已选择时间轴事件；双击或使用详情按钮定位。";
            return true;
        }

        internal bool SelectTimelineItem(CharacterTestRuntimeAiLabTimelineItemLocator locator)
        {
            if (!TryResolveTimelineItem(locator, out CharacterTestRuntimeAiLabTimelineItem item, out bool member))
            {
                _statusMessage = "时间轴 locator 已过期；当前 branch 未执行模糊重绑定。";
                return false;
            }
            _selectedTimelineItem = item;
            _selectedTimelineVisualStableKey = member ? string.Empty : item.StableKey;
            return true;
        }

        internal bool TryResolveBehaviorTransitionTimelineLocator(
            string transitionId,
            int frame,
            out CharacterTestRuntimeAiLabTimelineItemLocator locator)
        {
            locator = default;
            if (string.IsNullOrEmpty(transitionId) || frame < 0)
                return false;

            CharacterTestRuntimeAiLabTimelineProjection projection = BuildTimelineProjection();
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> items =
                    projection.Tracks[trackIndex].Items;
                for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem item = items[itemIndex];
                    if (TryMatchBehaviorTransitionTimelineItem(item, transitionId, frame, out locator))
                        return true;
                    for (int memberIndex = 0; memberIndex < item.Members.Count; memberIndex++)
                    {
                        if (TryMatchBehaviorTransitionTimelineItem(
                                item.Members[memberIndex],
                                transitionId,
                                frame,
                                out locator))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool TryMatchBehaviorTransitionTimelineItem(
            CharacterTestRuntimeAiLabTimelineItem item,
            string transitionId,
            int frame,
            out CharacterTestRuntimeAiLabTimelineItemLocator locator)
        {
            locator = default;
            if (item == null
                || item.Provenance == CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate
                || item.EventKind != CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionCommitted
                || item.StartFrame > frame
                || item.EndFrame < frame
                || !string.Equals(item.SourceTraceId.ToString(), transitionId, StringComparison.Ordinal))
            {
                return false;
            }
            locator = item.CreateLocator();
            return locator.IsValid;
        }

        internal bool NavigateTimelineItem(
            CharacterTestRuntimeAiLabTimelineItemLocator locator,
            bool useEndFrame)
        {
            if (!TryResolveTimelineItem(locator, out CharacterTestRuntimeAiLabTimelineItem item, out _))
            {
                _statusMessage = "时间轴 locator 已过期；当前 branch 未执行模糊重绑定。";
                return false;
            }

            if (item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate)
            {
                if (_timelineZoomLevel < 0)
                    _timelineZoomLevel = TimelineZoomSpans.Length - 1;
                else if (_timelineZoomLevel > 0)
                    _timelineZoomLevel--;
                int span = _timelineZoomLevel >= 0
                    ? TimelineZoomSpans[_timelineZoomLevel]
                    : Math.Max(1, BuildTimelineProjection().Domain.FrameLimit);
                _timelineViewStartOverride = ClampTimelineViewStart(
                    item.StartFrame - span / 2,
                    span);
                InvalidateTimelineProjection();
                _statusMessage = "已缩放并居中事件聚合。";
                return true;
            }
            if (item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack)
            {
                _statusMessage = "请从同帧堆叠中选择一个 typed leaf 后再定位。";
                return false;
            }

            return SeekToFrame(new CharacterTestRuntimeAiLabSeekRequest(
                useEndFrame ? item.EndFrame : item.StartFrame,
                CharacterTestRuntimeAiLabSeekOrigin.Marker,
                stayPaused: true,
                item.AgentEntityId,
                item.StableKey));
        }

        private bool TryResolveTimelineItem(
            CharacterTestRuntimeAiLabTimelineItemLocator locator,
            out CharacterTestRuntimeAiLabTimelineItem item,
            out bool member)
        {
            member = false;
            if (!locator.IsValid)
            {
                item = null;
                return false;
            }
            CharacterTestRuntimeAiLabTimelineProjection projection = BuildTimelineProjection();
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> items = projection.Tracks[trackIndex].Items;
                for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem candidate = items[itemIndex];
                    if (locator.Matches(candidate))
                    {
                        item = candidate;
                        return true;
                    }
                    if (candidate.ItemKind != CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack)
                        continue;
                    for (int memberIndex = 0; memberIndex < candidate.Members.Count; memberIndex++)
                    {
                        CharacterTestRuntimeAiLabTimelineItem leaf = candidate.Members[memberIndex];
                        if (!locator.Matches(leaf))
                            continue;
                        item = leaf;
                        member = true;
                        return true;
                    }
                }
            }
            item = null;
            return false;
        }

        internal bool TryGetSelectedTimelineItem(out CharacterTestRuntimeAiLabTimelineItem item)
        {
            item = _selectedTimelineItem;
            return item != null;
        }

        internal bool ClearTimelineItemSelection()
        {
            bool changed = _selectedTimelineItem != null
                || !string.IsNullOrEmpty(_selectedTimelineVisualStableKey);
            _selectedTimelineItem = null;
            _selectedTimelineVisualStableKey = string.Empty;
            return changed;
        }

        private bool TryResolveTimelineItem(
            string stableKey,
            out CharacterTestRuntimeAiLabTimelineItem item,
            out bool member)
        {
            member = false;
            CharacterTestRuntimeAiLabTimelineProjection projection = BuildTimelineProjection();
            if (projection.TryFindItem(stableKey, out item))
                return true;
            IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> members = _selectedTimelineItem?.Members;
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    if (!string.Equals(members[i].StableKey, stableKey, StringComparison.Ordinal))
                        continue;
                    item = members[i];
                    member = true;
                    return true;
                }
            }
            item = null;
            return false;
        }

        internal bool BeginTimelineScrub(int frame)
        {
            string failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.StaleCompletion;
            if (!TryBuildFrameSelectionTarget(
                    frame,
                    CharacterTestRuntimeAiLabSeekOrigin.Scrub,
                    out CharacterTestRuntimeAiLabFrameSelectionTarget target)
                || !_frameSelection.BeginScrub(target, out failureCode))
            {
                _statusMessage = BuildFrameSelectionFailureMessage(failureCode);
                return false;
            }
            ClearTimelineItemSelection();
            _timelineScrubActive = true;
            _timelineScrubStartedFromHistory = !_branchRecomputeArmed
                && _timeline != null
                && _timeline.HasSelection
                && _timeline.SelectedFrame < _timeline.LatestFrame;
            return QueueTimelineScrub(frame);
        }

        internal bool UpdateTimelineScrub(int frame)
        {
            string failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.StaleCompletion;
            if (!TryBuildFrameSelectionTarget(
                    frame,
                    CharacterTestRuntimeAiLabSeekOrigin.Scrub,
                    out CharacterTestRuntimeAiLabFrameSelectionTarget target)
                || !_frameSelection.UpdateScrub(target, out failureCode))
            {
                _statusMessage = BuildFrameSelectionFailureMessage(failureCode);
                return false;
            }
            if (!_timelineScrubActive)
            {
                _timelineScrubActive = true;
                _timelineScrubStartedFromHistory = !_branchRecomputeArmed
                    && _timeline != null
                    && _timeline.HasSelection
                    && _timeline.SelectedFrame < _timeline.LatestFrame;
            }
            return QueueTimelineScrub(frame);
        }

        internal bool EndTimelineScrub(int frame)
        {
            string failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.StaleCompletion;
            if (!TryBuildFrameSelectionTarget(
                    frame,
                    CharacterTestRuntimeAiLabSeekOrigin.Scrub,
                    out CharacterTestRuntimeAiLabFrameSelectionTarget target)
                || !_frameSelection.EndScrub(target, out failureCode))
            {
                _statusMessage = BuildFrameSelectionFailureMessage(failureCode);
                return false;
            }
            _timelineScrubActive = false;
            return QueueTimelineScrub(frame);
        }

        internal void CancelTimelineScrub()
        {
            _frameSelection.CancelScrubPreview();
            _timelineScrubActive = false;
            _timelineScrubStartedFromHistory = false;
            _hasTimelineScrubDesiredFrame = false;
            _timelineScrubDesiredFrame = 0;
            _futureAdvanceStartFrame = 0;
        }

        private bool QueueTimelineScrub(int requestedFrame)
        {
            if (!EnsureTimelineSessionContinuable())
                return false;
            if (_timeline == null || _timeline.Count <= 0)
            {
                _statusMessage = "尚未捕获可拖动的运行时帧。";
                return false;
            }

            if (_loopPlaybackActive)
                PauseLoopReview("循环回看已停止，正在拖动时间轴。");

            SetPaused(true);
            int sessionStartFrame = _timeline.Records[0].Frame;
            int sessionEndFrame = sessionStartFrame + _frameLimit - 1;
            int desiredFrame = Math.Max(sessionStartFrame, Math.Min(sessionEndFrame, requestedFrame));
            if (desiredFrame > _timeline.LatestFrame && _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Completed)
                desiredFrame = _timeline.LatestFrame;
            if (requestedFrame > sessionEndFrame)
                _statusMessage = "目标超过本次实验帧上限，已限制到帧 " + sessionEndFrame + "。";

            if (_branchRecomputeArmed && desiredFrame < _branchBaseFrame)
                CancelBranchRecompute();
            if ((desiredFrame > _timeline.LatestFrame
                    || (_branchRecomputeArmed && desiredFrame > _branchBaseFrame))
                && _futureAdvanceStartFrame <= 0)
                _futureAdvanceStartFrame = _branchRecomputeArmed
                    ? _branchBaseFrame
                    : _timeline.LatestFrame;
            _timelineScrubDesiredFrame = desiredFrame;
            _hasTimelineScrubDesiredFrame = true;
            ProcessTimelineScrubWork();
            return true;
        }

        private bool TryBuildFrameSelectionTarget(
            int requestedFrame,
            CharacterTestRuntimeAiLabSeekOrigin origin,
            out CharacterTestRuntimeAiLabFrameSelectionTarget target)
        {
            target = default;
            if (_timeline == null
                || !_timeline.TryResolveNearestFrame(requestedFrame, out int resolvedFrame, out _)
                || !_timeline.TryGetFrame(resolvedFrame, out CharacterTestRuntimeAiPlannerDevTimelineRecord record))
            {
                return false;
            }
            target = new CharacterTestRuntimeAiLabFrameSelectionTarget(
                resolvedFrame,
                ResolveSelectedAgentEntityId(),
                record.RecordGeneration,
                _timeline.SessionIdentity.Generation,
                origin);
            return true;
        }

        private static string BuildFrameSelectionFailureMessage(string failureCode)
        {
            return failureCode == CharacterTestRuntimeAiLabFrameSelectionFailureCodes.SessionLocked
                ? "当前会话恢复失败，只能重置或退出。"
                : "选帧事务正在处理，请等待当前定位完成。";
        }

        private void ProcessTimelineScrubWork()
        {
            if (_seekInProgress || !_hasTimelineScrubDesiredFrame || _timeline == null || _timeline.Count <= 0)
                return;

            int desiredFrame = _timelineScrubDesiredFrame;
            if (!_branchRecomputeArmed
                && _timelineScrubStartedFromHistory
                && desiredFrame > _timeline.LatestFrame)
            {
                desiredFrame = _timeline.LatestFrame;
                _timelineScrubDesiredFrame = desiredFrame;
                _futureAdvanceStartFrame = 0;
                _statusMessage = "历史回看不会生成未来帧；请先选择“从此重算”，或从 Latest 继续。";
            }
            if (_branchRecomputeArmed && desiredFrame > _branchBaseFrame)
            {
                AdvanceFutureFramesWithinBudget();
                return;
            }
            if (desiredFrame <= _timeline.LatestFrame)
            {
                if (!_timeline.TryResolveNearestFrame(desiredFrame, out int resolvedFrame, out _))
                    return;
                if (!_timeline.HasSelection || _timeline.SelectedFrame != resolvedFrame)
                {
                    SeekToFrame(new CharacterTestRuntimeAiLabSeekRequest(
                        resolvedFrame,
                        CharacterTestRuntimeAiLabSeekOrigin.Scrub,
                        stayPaused: true));
                    return;
                }

                if (!_timelineScrubActive)
                {
                    if (_frameSelection.Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Previewing
                        && !CommitTimelineScrubSelection(resolvedFrame))
                    {
                        return;
                    }
                    _hasTimelineScrubDesiredFrame = false;
                    _futureAdvanceStartFrame = 0;
                    _timelineScrubStartedFromHistory = false;
                    // A scrub may clamp to the already selected frame (for example,
                    // dragging beyond Latest when only one frame is retained). No
                    // restore callback will run in that case, so close the preview
                    // transaction explicitly instead of leaving global UI input gated.
                    _frameSelection.CancelScrubPreview();
                }
                return;
            }

            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running)
                return;
            if (_timeline.HasSelection && _timeline.SelectedFrame < _timeline.LatestFrame)
            {
                _timelineScrubDesiredFrame = _timeline.LatestFrame;
                _futureAdvanceStartFrame = 0;
                _statusMessage = "历史回看不会生成未来帧；请先选择“从此重算”，或从 Latest 继续。";
                SeekToFrame(new CharacterTestRuntimeAiLabSeekRequest(
                    _timeline.LatestFrame,
                    CharacterTestRuntimeAiLabSeekOrigin.Latest,
                    stayPaused: true));
                return;
            }

            AdvanceFutureFramesWithinBudget();
        }

        private void AdvanceFutureFramesWithinBudget()
        {
            const int maxTicksPerUpdate = 16;
            const double budgetMilliseconds = 4d;
            long startTimestamp = Stopwatch.GetTimestamp();
            int processed = 0;
            while (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                && (_branchRecomputeArmed || _timeline.LatestFrame < _timelineScrubDesiredFrame)
                && processed < maxTicksPerUpdate
                && ElapsedTimelineWorkMilliseconds(startTimestamp) < budgetMilliseconds)
            {
                if (!AdvanceOneAuthorityFrame(CharacterTestRuntimeAiLabPresentationPolicy.Deferred))
                {
                    _statusMessage = "未来帧模拟失败，运行时切片不可用。";
                    CancelTimelineScrub();
                    return;
                }
                processed++;
            }

            bool reachedTarget = _timeline.LatestFrame >= _timelineScrubDesiredFrame
                || _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Completed;
            bool refreshPresentation = reachedTarget
                || Time.unscaledTime - _lastFutureAdvancePresentationTime >= 0.1f;
            bool commitGeneratedProgress = refreshPresentation
                && (_timelineScrubActive || reachedTarget);
            if (commitGeneratedProgress
                && !CommitTimelineScrubSelection(_timeline.LatestFrame))
            {
                return;
            }
            if (refreshPresentation)
            {
                _gameManager?.RefreshPresentationForRuntimeAiPlannerDev();
                _lastFutureAdvancePresentationTime = Time.unscaledTime;
            }

            if (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Completed)
            {
                _timelineScrubDesiredFrame = _timeline.LatestFrame;
                _statusMessage = "已达到本次实验帧上限。";
            }
            else if (reachedTarget)
            {
                _statusMessage = "未来帧已模拟到 " + _timeline.LatestFrame + "。";
            }
            else
            {
                _statusMessage = "正在模拟 " + _timeline.LatestFrame + "/" + _timelineScrubDesiredFrame + "。";
            }

            if (reachedTarget && !_timelineScrubActive)
            {
                _hasTimelineScrubDesiredFrame = false;
                _futureAdvanceStartFrame = 0;
                _timelineScrubStartedFromHistory = false;
            }
        }

        private bool CommitTimelineScrubSelection(int frame)
        {
            if (!_timeline.TryGetFrame(frame, out CharacterTestRuntimeAiPlannerDevTimelineRecord record))
                return false;

            var target = new CharacterTestRuntimeAiLabFrameSelectionTarget(
                frame,
                ResolveSelectedAgentEntityId(),
                record.RecordGeneration,
                _timeline.SessionIdentity.Generation,
                CharacterTestRuntimeAiLabSeekOrigin.Scrub);
            string failureCode;
            bool targetAccepted = _frameSelection.ScrubGestureActive
                ? _frameSelection.UpdateScrub(target, out failureCode)
                : _frameSelection.ReplacePendingTarget(target, out failureCode);
            if (!targetAccepted
                || !_frameSelection.TryTakeNextRestore(out CharacterTestRuntimeAiLabFrameSelectionTarget activeTarget))
            {
                _statusMessage = BuildFrameSelectionFailureMessage(failureCode);
                return false;
            }

            int resolvedAgent = ResolveAgentForRecord(record, activeTarget.AgentEntityId);
            CharacterTestRuntimeAiLabCommittedFrameBundle bundle = CreateCommittedFrameBundle(record);
            CharacterTestRuntimeAiLabFrameSelectionCompletion completion = _frameSelection.CompleteRestore(
                _frameSelection.TransactionId,
                activeTarget,
                resolvedAgent,
                bundle,
                out _);
            if (completion == CharacterTestRuntimeAiLabFrameSelectionCompletion.Committed
                || completion == CharacterTestRuntimeAiLabFrameSelectionCompletion.Unchanged)
                return true;

            _statusMessage = completion == CharacterTestRuntimeAiLabFrameSelectionCompletion.Superseded
                ? "目标帧已更新，正在继续模拟。"
                : "未来帧提交失败，请重新拖动时间轴。";
            return false;
        }

        private static double ElapsedTimelineWorkMilliseconds(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
        }

        internal void CaptureAfterAuthorityTick()
        {
            using var captureScope = FrameCaptureMarker.Auto();
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || _seekInProgress
                || _loopPlaybackActive)
            {
                return;
            }

            CaptureLatestFrameIfNeeded(force: false);
            CaptureTestStateIfNeeded(force: false);
            if (_timelineSessionStarted && _timeline.Count >= _frameLimit)
                CompleteSessionAtFrameLimit();
            ObserveTacticalCardTestFrame();
        }

        private void PrepareTimelineForSessionStart()
        {
            using var startupProfile = CharacterTestStartupProfiler.PrepareTimeline.Auto();
            CancelQueuedApply(
                CharacterTestRuntimeAiLabQueuedApplyCancelReason.SessionChanged,
                "Apply F+1 已取消：Runtime AI Lab 会话正在重新建立。");
            _appliedRevisionEvents.Clear();
            _baselineRevision = null;
            _baselineRevisionPeers.Clear();
            _frameBuffer = new CharacterTestRuntimeAiPlannerDevFrameBuffer(_frameLimit);
            _timeline = new CharacterTestRuntimeAiPlannerDevTimeline(_frameLimit);
            _timelineEventIndex.Clear();
            _timelineSessionStarted = false;
            _sessionContinuable = true;
            _timelineZoomLevel = -1;
            _timelineViewStartOverride = int.MinValue;
            _timelineExpandedOverride = null;
            ClearTimelineItemSelection();
            InvalidateTimelineProjection();
        }

        private void BeginVisibleTimelineSession(int sessionStartFrame)
        {
            CancelQueuedApply(
                CharacterTestRuntimeAiLabQueuedApplyCancelReason.SessionChanged,
                "Apply F+1 已取消：RuntimeFrame 会话 generation 已变化。");
            _frameBuffer.Clear();
            _timelineSessionGeneration = _timelineSessionGeneration == int.MaxValue
                ? 1
                : _timelineSessionGeneration + 1;
            var sessionIdentity = new CharacterTestRuntimeAiLabTimelineSessionIdentity(
                _timelineSessionGeneration,
                sessionStartFrame,
                _frameLimit);
            _timeline.BeginSession(sessionIdentity);
            _frameSelection.Reset();
            _timelineEventIndex.Reset(sessionIdentity);
            _timelineSessionStarted = true;
            PrepareFrameCaptureStorage();
            _lastCapturedRuntimeFrame = int.MinValue;
            ClearTimelineItemSelection();
            InvalidateTimelineProjection();
        }

        private void ResetTimelineSessionState()
        {
            CancelQueuedApply(
                CharacterTestRuntimeAiLabQueuedApplyCancelReason.SessionChanged,
                "Apply F+1 已取消：Runtime AI Lab 会话已重置。");
            CancelTimelineScrub();
            _frameSelection.Reset();
            CancelBranchRecompute();
            ResetHistoryPlaybackState();
            _timelineSessionStarted = false;
            _frameCaptureStorage = null;
            _activeFrameCapture = null;
            _nextFrameCaptureStorage = 0;
            _sessionContinuable = true;
            _timelineEventIndex.Clear();
            _timelineZoomLevel = -1;
            _timelineViewStartOverride = int.MinValue;
            _timelineExpandedOverride = null;
            ClearTimelineItemSelection();
            InvalidateTimelineProjection();
        }

        private void CompleteSessionAtFrameLimit()
        {
            if (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Completed)
                return;
            SetPaused(true);
            RestoreUnityTimeScale();
            _phase = CharacterTestRuntimeAiPlannerDevScenePhase.Completed;
            _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Review);
            _statusMessage = "已达到本次实验帧上限，可继续检查历史或重置。";
        }

        private bool EnsureTimelineSessionContinuable()
        {
            if (_sessionContinuable)
                return true;
            _statusMessage = "时间轴恢复失败，本次会话不可继续；请重置或退出实验室。";
            return false;
        }

        private CharacterTestRuntimeAiLabTimelineViewport ResolveTimelineViewport()
        {
            if (_timeline == null || _timeline.Count <= 0)
                return new CharacterTestRuntimeAiLabTimelineViewport(
                    0,
                    0,
                    _timelineZoomLevel,
                    TimelineProjectionPixelWidth,
                    true,
                    CharacterTestRuntimeAiLabTimelineViewportMode.FitRetained);
            int sessionStart = _timeline.SessionIdentity.IsValid
                ? _timeline.SessionIdentity.SessionStartFrame
                : _timeline.Records[0].Frame;
            int sessionEnd = sessionStart + _frameLimit - 1;
            if (_timelineZoomLevel < 0)
            {
                int retainedStart = _timeline.Records[0].Frame;
                int retainedEnd = _timeline.LatestFrame;
                int retainedSpan = Math.Max(1, retainedEnd - retainedStart + 1);
                int fitSpan = Math.Min(_frameLimit, Math.Max(MinimumReadableTimelineSpan, retainedSpan));
                int leadingPadding = Math.Max(0, (fitSpan - retainedSpan) / 2);
                int fitStart = ClampTimelineViewStart(retainedStart - leadingPadding, fitSpan);
                return new CharacterTestRuntimeAiLabTimelineViewport(
                    fitStart,
                    Math.Min(sessionEnd, fitStart + fitSpan - 1),
                    -1,
                    TimelineProjectionPixelWidth,
                    true,
                    CharacterTestRuntimeAiLabTimelineViewportMode.FitRetained);
            }

            int span = TimelineZoomSpans[_timelineZoomLevel];
            int start = _timelineViewStartOverride != int.MinValue
                ? ClampTimelineViewStart(_timelineViewStartOverride, span)
                : ClampTimelineViewStart(_timeline.LatestFrame - (int)(span * 0.8f), span);
            return new CharacterTestRuntimeAiLabTimelineViewport(
                start,
                start + span - 1,
                _timelineZoomLevel,
                TimelineProjectionPixelWidth,
                _timelineViewStartOverride == int.MinValue,
                CharacterTestRuntimeAiLabTimelineViewportMode.Manual);
        }

        private int ClampTimelineViewStart(int requestedStart, int span)
        {
            if (_timeline == null || _timeline.Count <= 0)
                return 0;
            int sessionStart = _timeline.SessionIdentity.IsValid
                ? _timeline.SessionIdentity.SessionStartFrame
                : _timeline.Records[0].Frame;
            int sessionEnd = Math.Max(sessionStart + _frameLimit - 1, _timeline.LatestFrame);
            int maxStart = Math.Max(sessionStart, sessionEnd - Math.Max(1, span) + 1);
            return Math.Max(sessionStart, Math.Min(maxStart, requestedStart));
        }

        private int ResolveSelectedAgentEntityId()
        {
            if (!_frameBuffer.TryGetSelected(out CharacterTestRuntimeAiPlannerDevFrameSnapshot frame))
                return 0;
            return frame.Agents != null && _selectedAgentIndex >= 0 && _selectedAgentIndex < frame.Agents.Count
                ? frame.Agents[_selectedAgentIndex].EntityId
                : 0;
        }

        private void InvalidateTimelineProjection()
        {
            _cachedTimelineProjection = null;
            _cachedTimelineRevision = long.MinValue;
            _cachedTimelineSelectedFrame = int.MinValue;
            _lastTimelineProjectionBuildTime = float.NegativeInfinity;
        }

        internal static bool CanRebindTimelineSelection(
            CharacterTestRuntimeAiLabTimelineItem previous,
            CharacterTestRuntimeAiLabTimelineItem current)
        {
            if (previous == null || current == null)
                return false;
            if (previous.Provenance == CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate
                || current.Provenance == CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate)
            {
                if (previous.Provenance != current.Provenance
                    || previous.ItemKind != current.ItemKind
                    || previous.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate
                    || previous.SessionGeneration != current.SessionGeneration
                    || previous.RecordGeneration != current.RecordGeneration
                    || previous.AuthorityHash != current.AuthorityHash
                    || previous.StartFrame != current.StartFrame
                    || previous.EndFrame != current.EndFrame
                    || previous.Members.Count != current.Members.Count
                    || !string.Equals(previous.StableKey, current.StableKey, System.StringComparison.Ordinal))
                {
                    return false;
                }
                for (int i = 0; i < previous.Members.Count; i++)
                {
                    if (!previous.Members[i].CreateLocator().Matches(current.Members[i]))
                        return false;
                }
                return true;
            }
            if (previous.SessionGeneration <= 0 || previous.RecordGeneration <= 0L)
                return true;
            return previous.SessionGeneration == current.SessionGeneration
                && previous.RecordGeneration == current.RecordGeneration
                && previous.AuthorityHash == current.AuthorityHash
                && previous.StartFrame == current.StartFrame
                && previous.TrackKind == current.TrackKind
                && previous.EventKind == current.EventKind
                && previous.OccurrenceRank == current.OccurrenceRank
                && previous.AgentEntityId == current.AgentEntityId
                && previous.RelatedEntityId == current.RelatedEntityId
                && previous.ProfileSlot == current.ProfileSlot
                && previous.CardInstanceStartFrame == current.CardInstanceStartFrame
                && previous.SourceTraceId.Equals(current.SourceTraceId)
                && string.Equals(previous.BehaviorEntryId, current.BehaviorEntryId, System.StringComparison.Ordinal)
                && string.Equals(previous.BehaviorStepId, current.BehaviorStepId, System.StringComparison.Ordinal);
        }

        private static bool ViewportEquals(
            CharacterTestRuntimeAiLabTimelineViewport left,
            CharacterTestRuntimeAiLabTimelineViewport right)
        {
            return left.StartFrame == right.StartFrame
                && left.EndFrame == right.EndFrame
                && left.ZoomLevel == right.ZoomLevel
                && left.PixelWidth.Equals(right.PixelWidth)
                && left.FollowLatest == right.FollowLatest
                && left.Mode == right.Mode;
        }
    }

    internal enum CharacterTestRuntimeAiLabSeekOrigin
    {
        Scrub = 0,
        Jump = 1,
        Marker = 2,
        Latest = 3,
        Loop = 4,
        Playback = 5
    }

    internal readonly struct CharacterTestRuntimeAiLabSeekRequest
    {
        public CharacterTestRuntimeAiLabSeekRequest(
            int targetFrame,
            CharacterTestRuntimeAiLabSeekOrigin origin,
            bool stayPaused,
            int agentEntityId = 0,
            string itemStableKey = "")
        {
            TargetFrame = targetFrame;
            Origin = origin;
            StayPaused = stayPaused;
            AgentEntityId = agentEntityId;
            ItemStableKey = itemStableKey ?? string.Empty;
        }

        public int TargetFrame { get; }
        public CharacterTestRuntimeAiLabSeekOrigin Origin { get; }
        public bool StayPaused { get; }
        public int AgentEntityId { get; }
        public string ItemStableKey { get; }
    }

    internal readonly struct CharacterTestRuntimeAiLabSeekResult
    {
        public CharacterTestRuntimeAiLabSeekResult(
            bool success,
            int committedFrame,
            int resolvedAgentEntityId,
            string failureCode)
        {
            Success = success;
            CommittedFrame = committedFrame;
            ResolvedAgentEntityId = resolvedAgentEntityId;
            FailureCode = failureCode ?? string.Empty;
        }

        public bool Success { get; }
        public int CommittedFrame { get; }
        public int ResolvedAgentEntityId { get; }
        public string FailureCode { get; }
    }
}
