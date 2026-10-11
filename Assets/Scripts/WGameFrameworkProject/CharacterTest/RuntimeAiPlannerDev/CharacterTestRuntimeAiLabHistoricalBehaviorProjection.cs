using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus
    {
        NotHistorical = 0,
        Resolved = 1,
        ResolvedWithIncompleteEventEvidence = 2,
        Unavailable = 3
    }

    internal enum CharacterTestRuntimeAiLabHistoricalEventEvidenceState
    {
        Complete = 0,
        SelectedFrameOverflow = 1,
        PreviousScanOverflow = 2,
        BeforeRetainedStart = 3,
        Unavailable = 4
    }

    /// <summary>Reusable read-only projection for one selected historical RuntimeFrame.</summary>
    /// <remarks>LowFreqAlloc: constructed once by the Runtime AI Lab controller and reused across selections.</remarks>
    internal sealed class CharacterTestRuntimeAiLabHistoricalBehaviorProjection
    {
        internal CharacterTestRuntimeAiLabHistoricalBehaviorProjection()
        {
            ExactFrameEvents = new CharacterTestRuntimeAiLabLogicEventBuffer(
                CharacterTestRuntimeAiLabTimelineEventIndex.MaxEventsPerFrame);
        }

        internal CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus Status { get; set; }
        internal CharacterTestRuntimeAiLabHistoricalEventEvidenceState EventEvidenceState { get; set; }
        internal string DiagnosticCode { get; set; } = string.Empty;
        internal string Diagnostic { get; set; } = string.Empty;
        internal int SessionGeneration { get; set; }
        internal int SelectedFrame { get; set; }
        internal int LatestFrame { get; set; }
        internal int RetainedStartFrame { get; set; }
        internal int EntityId { get; set; }
        internal int ProfileSlot { get; set; }
        internal string BehaviorEntryId { get; set; } = string.Empty;
        internal long ProgramSemanticHash { get; set; }
        internal int CardInstanceStartFrame { get; set; } = -1;
        internal int EventOverflowCount { get; set; }
        internal CharacterTestRuntimeAiLabCompiledLogicProgram FrozenProgram { get; set; }
        internal CharacterTestRuntimeAiLabLogicAgentStateSnapshot RuntimeState { get; set; }
        internal CharacterTestRuntimeAiLabLogicEventBuffer ExactFrameEvents { get; }
        internal int ExactCommittedTransitionIndex { get; set; } = -1;
        internal int PreviousCommittedTransitionIndex { get; set; } = -1;
        internal int PreviousCommittedRuntimeFrame { get; set; } = -1;
        internal bool IsHistorical => Status != CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.NotHistorical;
        internal bool HasSnapshotState => Status == CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.Resolved
            || Status == CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.ResolvedWithIncompleteEventEvidence;
        internal bool EventEvidenceComplete => EventEvidenceState == CharacterTestRuntimeAiLabHistoricalEventEvidenceState.Complete;

        internal void Reset()
        {
            Status = CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.NotHistorical;
            EventEvidenceState = CharacterTestRuntimeAiLabHistoricalEventEvidenceState.Unavailable;
            DiagnosticCode = string.Empty;
            Diagnostic = string.Empty;
            SessionGeneration = 0;
            SelectedFrame = 0;
            LatestFrame = 0;
            RetainedStartFrame = 0;
            EntityId = 0;
            ProfileSlot = 0;
            BehaviorEntryId = string.Empty;
            ProgramSemanticHash = 0L;
            CardInstanceStartFrame = -1;
            EventOverflowCount = 0;
            FrozenProgram = null;
            RuntimeState = null;
            ExactFrameEvents.Clear();
            ExactCommittedTransitionIndex = -1;
            PreviousCommittedTransitionIndex = -1;
            PreviousCommittedRuntimeFrame = -1;
        }
    }

    internal static class CharacterTestRuntimeAiLabHistoricalBehaviorProjector
    {
        /// <remarks>NoAlloc after the reusable result and its event buffer have been initialized.</remarks>
        internal static CharacterTestRuntimeAiLabHistoricalBehaviorProjection ResolveInto(
            CharacterTestRuntimeAiLabHistoricalBehaviorProjection result,
            CharacterTestRuntimeAiLabTimelineEventIndex eventIndex,
            int sessionGeneration,
            int retainedStartFrame,
            int selectedFrame,
            int latestFrame,
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime)
        {
            result = result ?? new CharacterTestRuntimeAiLabHistoricalBehaviorProjection();
            result.Reset();
            if (selectedFrame >= latestFrame)
                return result;

            result.Status = CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.Unavailable;
            result.SessionGeneration = sessionGeneration;
            result.SelectedFrame = selectedFrame;
            result.LatestFrame = latestFrame;
            result.RetainedStartFrame = retainedStartFrame;
            if (eventIndex == null
                || !eventIndex.SessionIdentity.IsValid
                || eventIndex.SessionIdentity.Generation != sessionGeneration
                || selectedFrame < retainedStartFrame
                || selectedFrame < eventIndex.SessionStartFrame
                || !eventIndex.IsFrameCaptured(selectedFrame))
            {
                return Fail(result, "HISTORY_FRAME_DOMAIN_INVALID", "历史帧不属于当前 retained timeline session。");
            }
            if (runtime == null
                || runtime.Status != CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved
                || runtime.CommittedCard == null
                || runtime.CommittedProgram == null
                || runtime.LogicAgent == null)
            {
                return Fail(result, "HISTORY_LOGIC_STATE_MISSING", runtime?.Diagnostic ?? "历史行为卡状态不可用。");
            }

            CharacterTestRuntimeAiLabLogicAgentStateSnapshot state = runtime.LogicAgent;
            CharacterTestRuntimeAiLabCompiledLogicProgram program = runtime.CommittedProgram;
            if (state.DetectorArmed == null || state.DetectorArmed.Length != program.Detectors.Count)
                return Fail(result, "HISTORY_DETECTOR_STATE_INVALID", "历史 Detector armed 状态与冻结 program 不一致。");
            if (state.CardStartedFrame < 0 || state.CardStartedFrame > selectedFrame)
                return Fail(result, "HISTORY_CARD_INSTANCE_INVALID", "历史行为卡实例起始帧无效。");

            result.EntityId = runtime.SelectedEntityId;
            result.ProfileSlot = runtime.ProfileSlot;
            result.BehaviorEntryId = runtime.CommittedCard.RuntimeEntryId;
            result.ProgramSemanticHash = program.SemanticHash;
            result.CardInstanceStartFrame = state.CardStartedFrame;
            result.FrozenProgram = program;
            result.RuntimeState = state;
            result.Status = CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.Resolved;

            int selectedOverflow = eventIndex.GetOverflowCount(selectedFrame);
            if (selectedOverflow > 0)
            {
                result.Status = CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.ResolvedWithIncompleteEventEvidence;
                result.EventEvidenceState = CharacterTestRuntimeAiLabHistoricalEventEvidenceState.SelectedFrameOverflow;
                result.EventOverflowCount = selectedOverflow;
                result.DiagnosticCode = "HISTORY_EVENT_EVIDENCE_OVERFLOW";
                result.Diagnostic = "本帧 typed event 不完整；已保留 snapshot 状态并隐藏命中与实际路径。";
                return result;
            }

            result.EventEvidenceState = CharacterTestRuntimeAiLabHistoricalEventEvidenceState.Complete;
            int sampleCount = eventIndex.GetCount(selectedFrame);
            for (int i = 0; i < sampleCount; i++)
            {
                CharacterTestRuntimeAiLabTimelineEventSample sample = eventIndex.GetSample(selectedFrame, i);
                if (!CharacterTestRuntimeAiPlannerDevSceneController.TryConvertLogicTimelineSample(
                        sample,
                        out CharacterTestRuntimeAiLabLogicEvent evt)
                    || !MatchesInstance(result, evt))
                {
                    continue;
                }
                if (!ValidateEvent(program, evt, out int transitionIndex))
                    return Fail(result, "HISTORY_EVENT_IDENTITY_INVALID", "历史 typed event 引用了冻结 program 中不存在的节点。");
                result.ExactFrameEvents.Add(evt);
                if (evt.Kind != CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted)
                    continue;
                if (result.ExactCommittedTransitionIndex >= 0)
                    return Fail(result, "HISTORY_MULTIPLE_COMMITTED_TRANSITIONS", "同一逻辑帧出现了多个已提交跳转。");
                result.ExactCommittedTransitionIndex = transitionIndex;
            }

            if (result.ExactCommittedTransitionIndex < 0)
                FindPreviousCommitted(result, eventIndex, program);
            if (result.Status == CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.Unavailable)
                return result;
            result.Status = result.EventEvidenceState == CharacterTestRuntimeAiLabHistoricalEventEvidenceState.Complete
                ? CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.Resolved
                : CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.ResolvedWithIncompleteEventEvidence;
            return result;
        }

        private static void FindPreviousCommitted(
            CharacterTestRuntimeAiLabHistoricalBehaviorProjection result,
            CharacterTestRuntimeAiLabTimelineEventIndex eventIndex,
            CharacterTestRuntimeAiLabCompiledLogicProgram program)
        {
            int lower = Math.Max(result.RetainedStartFrame, result.CardInstanceStartFrame);
            int upper = result.SelectedFrame - 1;
            for (int frame = upper; frame >= lower; frame--)
            {
                int overflow = eventIndex.GetOverflowCount(frame);
                if (overflow > 0)
                {
                    result.EventEvidenceState = CharacterTestRuntimeAiLabHistoricalEventEvidenceState.PreviousScanOverflow;
                    result.EventOverflowCount = overflow;
                    result.DiagnosticCode = "HISTORY_PREVIOUS_PATH_OVERFLOW";
                    result.Diagnostic = "Frame " + frame + " 的事件证据不完整；最近实际路径不可证明。";
                    return;
                }
                int count = eventIndex.GetCount(frame);
                for (int i = 0; i < count; i++)
                {
                    CharacterTestRuntimeAiLabTimelineEventSample sample = eventIndex.GetSample(frame, i);
                    if (!CharacterTestRuntimeAiPlannerDevSceneController.TryConvertLogicTimelineSample(
                            sample,
                            out CharacterTestRuntimeAiLabLogicEvent evt)
                        || evt.Kind != CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted
                        || !MatchesInstance(result, evt))
                    {
                        continue;
                    }
                    if (!ValidateEvent(program, evt, out int transitionIndex))
                    {
                        Fail(result, "HISTORY_EVENT_IDENTITY_INVALID", "历史提交路径引用了冻结 program 中不存在的 Transition。");
                        return;
                    }
                    result.PreviousCommittedTransitionIndex = transitionIndex;
                    result.PreviousCommittedRuntimeFrame = frame;
                    return;
                }
            }
            if (result.CardInstanceStartFrame < result.RetainedStartFrame)
            {
                result.EventEvidenceState = CharacterTestRuntimeAiLabHistoricalEventEvidenceState.BeforeRetainedStart;
                result.DiagnosticCode = "HISTORY_PREVIOUS_PATH_BEFORE_RETAINED_START";
                result.Diagnostic = "更早的实际路径已超出 retained frame 范围。";
            }
        }

        private static bool MatchesInstance(
            CharacterTestRuntimeAiLabHistoricalBehaviorProjection result,
            in CharacterTestRuntimeAiLabLogicEvent evt)
        {
            return evt.EntityId == result.EntityId
                && evt.ProfileSlot == result.ProfileSlot
                && evt.CardInstanceStartFrame == result.CardInstanceStartFrame
                && evt.ProgramSemanticHash == result.ProgramSemanticHash
                && string.Equals(evt.BehaviorEntryId, result.BehaviorEntryId, StringComparison.Ordinal);
        }

        private static bool ValidateEvent(
            CharacterTestRuntimeAiLabCompiledLogicProgram program,
            in CharacterTestRuntimeAiLabLogicEvent evt,
            out int transitionIndex)
        {
            transitionIndex = -1;
            if (!string.IsNullOrEmpty(evt.StageId) && FindStage(program, evt.StageId) < 0)
                return false;
            if (!string.IsNullOrEmpty(evt.DetectorId) && FindDetector(program, evt.DetectorId) < 0)
                return false;
            if (!string.IsNullOrEmpty(evt.TransitionId))
            {
                transitionIndex = FindTransition(program, evt.TransitionId);
                if (transitionIndex < 0)
                    return false;
            }
            return true;
        }

        private static int FindStage(CharacterTestRuntimeAiLabCompiledLogicProgram program, string stableId)
        {
            for (int i = 0; i < program.Stages.Count; i++)
                if (string.Equals(program.Stages[i].StageId, stableId, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static int FindDetector(CharacterTestRuntimeAiLabCompiledLogicProgram program, string stableId)
        {
            for (int i = 0; i < program.Detectors.Count; i++)
                if (string.Equals(program.Detectors[i].DetectorId, stableId, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static int FindTransition(CharacterTestRuntimeAiLabCompiledLogicProgram program, string stableId)
        {
            for (int i = 0; i < program.Transitions.Count; i++)
                if (string.Equals(program.Transitions[i].TransitionId, stableId, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static CharacterTestRuntimeAiLabHistoricalBehaviorProjection Fail(
            CharacterTestRuntimeAiLabHistoricalBehaviorProjection result,
            string code,
            string diagnostic)
        {
            result.Status = CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.Unavailable;
            result.EventEvidenceState = CharacterTestRuntimeAiLabHistoricalEventEvidenceState.Unavailable;
            result.DiagnosticCode = code ?? string.Empty;
            result.Diagnostic = diagnostic ?? string.Empty;
            result.FrozenProgram = null;
            result.RuntimeState = null;
            result.ExactFrameEvents.Clear();
            result.ExactCommittedTransitionIndex = -1;
            result.PreviousCommittedTransitionIndex = -1;
            result.PreviousCommittedRuntimeFrame = -1;
            return result;
        }
    }
}
