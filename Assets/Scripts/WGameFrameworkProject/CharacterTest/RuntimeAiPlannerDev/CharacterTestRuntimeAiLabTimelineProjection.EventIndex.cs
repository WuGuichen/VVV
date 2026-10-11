using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabTimelineEventSample
    {
        public CharacterTestRuntimeAiLabTimelineEventSample(
            int frame,
            CharacterTestRuntimeAiLabTimelineEventKind kind,
            int entityId,
            int relatedEntityId,
            string category,
            CharacterTestRuntimeAiLabTimelineSeverity severity,
            string label,
            RuntimeTraceIdentity sourceTraceId,
            int sourceOrder,
            long instanceId = 0,
            int value = 0,
            int traceNumber = 0,
            string behaviorEntryId = "",
            string behaviorStepId = "",
            int profileSlot = 0,
            int cardInstanceStartFrame = -1,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind?
                behaviorTriggerSourceKind = null,
            CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind?
                behaviorOutcomeKind = null,
            string behaviorTargetTimelineId = "",
            int behaviorLocalFrame = 0,
            long behaviorPrimaryActionInstanceId = 0L,
            int behaviorReasonCode = 0,
            int numericLabel = -1, string sourceTraceLabel = "", int behaviorEntryNumericId = 0, int behaviorStepIndex = -1, int behaviorEventIndex = -1, int behaviorTransitionIndex = -1, int behaviorTargetTimelineIndex = -1, int categoryCode = 0, int categoryValue = 0, int labelCode = 0, RuntimeHashDigest labelDigest = default, RuntimeHashDigest categoryDigest = default, int traceCategoryId = 0, int traceStageId = 0)
        {
            TraceCategoryId = traceCategoryId; TraceStageId = traceStageId;
            Frame = frame;
            Kind = kind;
            EntityId = entityId;
            RelatedEntityId = relatedEntityId;
            Category = category ?? string.Empty;
            Severity = severity;
            TextLabel = label ?? string.Empty;
            HasNumericLabel = numericLabel >= 0;
            NumericLabel = numericLabel;
            CategoryCode = categoryCode; CategoryValue = categoryValue; LabelCode = labelCode; LabelDigest = labelDigest; CategoryDigest = categoryDigest;
            SourceTraceId = sourceTraceId;
            SourceTraceLabel = sourceTraceLabel ?? string.Empty;
            BehaviorEntryNumericId = behaviorEntryNumericId; BehaviorStepIndex = behaviorStepIndex;
            BehaviorEventIndex = behaviorEventIndex; BehaviorTransitionIndex = behaviorTransitionIndex;
            BehaviorTargetTimelineIndex = behaviorTargetTimelineIndex;
            SourceOrder = sourceOrder;
            InstanceId = instanceId;
            Value = value;
            TraceNumber = traceNumber;
            BehaviorEntryId = behaviorEntryId ?? string.Empty;
            BehaviorStepId = behaviorStepId ?? string.Empty;
            ProfileSlot = Math.Max(0, profileSlot);
            CardInstanceStartFrame = cardInstanceStartFrame;
            BehaviorTriggerSourceKind = behaviorTriggerSourceKind;
            BehaviorOutcomeKind = behaviorOutcomeKind;
            BehaviorTargetTimelineId = behaviorTargetTimelineId ?? string.Empty;
            BehaviorLocalFrame = Math.Max(0, behaviorLocalFrame);
            BehaviorPrimaryActionInstanceId = Math.Max(0L, behaviorPrimaryActionInstanceId);
            BehaviorReasonCode = behaviorReasonCode;
        }

        public int Frame { get; }
        public CharacterTestRuntimeAiLabTimelineEventKind Kind { get; }
        public int EntityId { get; }
        public int RelatedEntityId { get; }
        public string Category { get; }
        public CharacterTestRuntimeAiLabTimelineSeverity Severity { get; }
        internal string TextLabel { get; }
        internal bool HasNumericLabel { get; }
        internal int NumericLabel { get; }
        // Formatting belongs to the UI projection. Capture and content hashing use the numeric field directly.
        public string Label => HasNumericLabel ? new CharacterActionId(NumericLabel).ToString() : TextLabel;
        public int TraceCategoryId { get; }
        public int TraceStageId { get; }
        public int CategoryCode { get; }
        public int CategoryValue { get; }
        public int LabelCode { get; }
        public RuntimeHashDigest LabelDigest { get; }
        public RuntimeHashDigest CategoryDigest { get; }
        public RuntimeTraceIdentity SourceTraceId { get; }
        public string SourceTraceLabel { get; }
        public int BehaviorEntryNumericId { get; }
        public int BehaviorStepIndex { get; }
        public int BehaviorEventIndex { get; }
        public int BehaviorTransitionIndex { get; }
        public int BehaviorTargetTimelineIndex { get; }
        public int SourceOrder { get; }
        public long InstanceId { get; }
        public int Value { get; }
        public int TraceNumber { get; }
        public string BehaviorEntryId { get; }
        public string BehaviorStepId { get; }
        public int ProfileSlot { get; }
        public int CardInstanceStartFrame { get; }
        public CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind?
            BehaviorTriggerSourceKind { get; }
        public CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind? BehaviorOutcomeKind { get; }
        public string BehaviorTargetTimelineId { get; }
        public int BehaviorLocalFrame { get; }
        public long BehaviorPrimaryActionInstanceId { get; }
        public int BehaviorReasonCode { get; }
        internal void Contribute(RuntimeHashAccumulator hash, int key)
        {
            hash.BeginObject(key, 20044, 2);
            hash.AddInt(1, Frame); hash.AddInt(2, (int)Kind); hash.AddInt(3, EntityId); hash.AddInt(4, RelatedEntityId);
            hash.AddInt(5, (int)Severity); hash.AddInt(6, SourceOrder); hash.AddLong(7, InstanceId); hash.AddInt(8, Value);
            hash.AddInt(9, TraceNumber); hash.AddInt(10, CategoryCode); hash.AddInt(11, CategoryValue); hash.AddDigest(12, CategoryDigest);
            hash.AddBool(13, HasNumericLabel); hash.AddInt(14, NumericLabel); hash.AddInt(15, LabelCode); hash.AddDigest(16, LabelDigest);
            SourceTraceId.Contribute(hash, 17); hash.AddInt(18, BehaviorEntryNumericId); hash.AddInt(19, BehaviorStepIndex);
            hash.AddInt(20, ProfileSlot); hash.AddInt(21, CardInstanceStartFrame);
            hash.AddBool(22, BehaviorTriggerSourceKind.HasValue); hash.AddInt(23, (int)(BehaviorTriggerSourceKind ?? 0));
            hash.AddBool(24, BehaviorOutcomeKind.HasValue); hash.AddInt(25, (int)(BehaviorOutcomeKind ?? 0));
            hash.AddInt(26, BehaviorTargetTimelineIndex); hash.AddInt(27, BehaviorLocalFrame); hash.AddLong(28, BehaviorPrimaryActionInstanceId);
            hash.AddInt(29, BehaviorReasonCode); hash.AddInt(30, BehaviorEventIndex); hash.AddInt(31, BehaviorTransitionIndex);
            hash.AddInt(32, TraceCategoryId); hash.AddInt(33, TraceStageId);
            hash.EndObject();
        }
    }

    internal sealed class CharacterTestRuntimeAiLabTimelineEventIndex
    {
        public const int MaxEventsPerFrame = 64;
        private const int PayloadEventsPerFrame = MaxEventsPerFrame - 1;

        private CharacterTestRuntimeAiLabTimelineEventSample[] _samples =
            Array.Empty<CharacterTestRuntimeAiLabTimelineEventSample>();
        private int[] _counts = Array.Empty<int>();
        private bool[] _captured = Array.Empty<bool>();
        private int[] _overflowCounts = Array.Empty<int>();
        private CharacterTestRuntimeAiLabTimelineSessionIdentity _sessionIdentity;
        private int _sessionStartFrame;
        private int _frameLimit;
        private int _capturedFrameCount;
        private long _contentRevision;

        public CharacterTestRuntimeAiLabTimelineSessionIdentity SessionIdentity => _sessionIdentity;
        public int SessionStartFrame => _sessionStartFrame;
        public int FrameLimit => _frameLimit;
        public int CapturedFrameCount => _capturedFrameCount;
        public long ContentRevision => _contentRevision;

        public void Reset(int sessionStartFrame, int frameLimit)
        {
            Reset(new CharacterTestRuntimeAiLabTimelineSessionIdentity(1, sessionStartFrame, frameLimit));
        }

        public void Reset(CharacterTestRuntimeAiLabTimelineSessionIdentity sessionIdentity)
        {
            if (!sessionIdentity.IsValid)
                throw new ArgumentException("Timeline session identity is invalid.", nameof(sessionIdentity));
            // Mono materializes the fixed category strings on the first invocation. Do this in setup.
            LogicEventCategory(CharacterTestRuntimeAiLabLogicEventKind.StageEntered);
            _sessionIdentity = sessionIdentity;
            _sessionStartFrame = sessionIdentity.SessionStartFrame;
            _frameLimit = sessionIdentity.FrameLimit;
            _samples = new CharacterTestRuntimeAiLabTimelineEventSample[_frameLimit * MaxEventsPerFrame];
            _counts = new int[_frameLimit];
            _captured = new bool[_frameLimit];
            _overflowCounts = new int[_frameLimit];
            _capturedFrameCount = 0;
            _contentRevision++;
        }



        public void Clear()
        {
            Array.Clear(_samples, 0, _samples.Length);
            Array.Clear(_counts, 0, _counts.Length);
            Array.Clear(_captured, 0, _captured.Length);
            Array.Clear(_overflowCounts, 0, _overflowCounts.Length);
            _sessionIdentity = default;
            _sessionStartFrame = 0;
            _frameLimit = 0;
            _capturedFrameCount = 0;
            _contentRevision++;
        }

        internal RecoveryState CaptureRecoveryState()
        {
            return new RecoveryState(
                (CharacterTestRuntimeAiLabTimelineEventSample[])_samples.Clone(),
                (int[])_counts.Clone(),
                (bool[])_captured.Clone(),
                (int[])_overflowCounts.Clone(),
                _sessionIdentity,
                _sessionStartFrame,
                _frameLimit,
                _capturedFrameCount,
                _contentRevision,
                ComputeRecoveryHash());
        }

        internal void RestoreRecoveryState(RecoveryState state)
        {
            if (state == null)
            {
                Clear();
                return;
            }
            _samples = (CharacterTestRuntimeAiLabTimelineEventSample[])state.Samples.Clone();
            _counts = (int[])state.Counts.Clone();
            _captured = (bool[])state.Captured.Clone();
            _overflowCounts = (int[])state.OverflowCounts.Clone();
            _sessionIdentity = state.SessionIdentity;
            _sessionStartFrame = state.SessionStartFrame;
            _frameLimit = state.FrameLimit;
            _capturedFrameCount = state.CapturedFrameCount;
            _contentRevision = state.ContentRevision;
        }

        private readonly RuntimeHashAccumulator _recoveryHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerLabFrameState, RuntimeHashSchemaIds.Version1);
        private readonly RuntimeHashAccumulator _frameContentHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerLabFrameState, RuntimeHashSchemaIds.Version1);

        internal long ComputeRecoveryHash()
        {
            RuntimeHashAccumulator hash = _recoveryHash; hash.Reset();
            hash.AddInt(1, _sessionIdentity.Generation); hash.AddInt(2, _sessionIdentity.SessionStartFrame); hash.AddInt(3, _sessionIdentity.FrameLimit);
            hash.AddInt(4, _sessionStartFrame); hash.AddInt(5, _frameLimit); hash.AddInt(6, _capturedFrameCount); hash.AddLong(7, _contentRevision);
            hash.BeginArray(8, _counts.Length);
            for (int i = 0; i < _counts.Length; i++)
            {
                hash.BeginElement(i); hash.BeginObject(1, 20047, 1);
                hash.AddInt(1, _counts[i]); hash.AddBool(2, _captured[i]); hash.AddInt(3, _overflowCounts[i]);
                int slotStart = i * MaxEventsPerFrame; int count = Math.Min(_counts[i], MaxEventsPerFrame);
                hash.BeginArray(4, count);
                for (int j = 0; j < count; j++) { hash.BeginElement(j); _samples[slotStart + j].Contribute(hash, 1); hash.EndElement(); }
                hash.EndArray(); hash.EndObject(); hash.EndElement();
            }
            hash.EndArray(); return hash.ToHash();
        }

        internal long ComputeFrameContentHash(int frame)
        {
            int frameIndex = frame - _sessionStartFrame;
            if (frameIndex < 0 || frameIndex >= _counts.Length || !_captured[frameIndex]) return 0L;
            RuntimeHashAccumulator hash = _frameContentHash; hash.Reset();
            hash.AddInt(1, _sessionIdentity.Generation); hash.AddInt(2, frame);
            hash.AddInt(3, _counts[frameIndex]); hash.AddInt(4, _overflowCounts[frameIndex]);
            int slotStart = frameIndex * MaxEventsPerFrame; int count = Math.Min(_counts[frameIndex], MaxEventsPerFrame);
            hash.BeginArray(5, count);
            for (int i = 0; i < count; i++) { hash.BeginElement(i); _samples[slotStart + i].Contribute(hash, 1); hash.EndElement(); }
            hash.EndArray(); return hash.ToHash();
        }

        internal sealed class RecoveryState
        {
            internal RecoveryState(
                CharacterTestRuntimeAiLabTimelineEventSample[] samples,
                int[] counts,
                bool[] captured,
                int[] overflowCounts,
                CharacterTestRuntimeAiLabTimelineSessionIdentity sessionIdentity,
                int sessionStartFrame,
                int frameLimit,
                int capturedFrameCount,
                long contentRevision,
                long contentHash)
            {
                Samples = samples;
                Counts = counts;
                Captured = captured;
                OverflowCounts = overflowCounts;
                SessionIdentity = sessionIdentity;
                SessionStartFrame = sessionStartFrame;
                FrameLimit = frameLimit;
                CapturedFrameCount = capturedFrameCount;
                ContentRevision = contentRevision;
                ContentHash = contentHash;
            }

            internal CharacterTestRuntimeAiLabTimelineEventSample[] Samples { get; }
            internal int[] Counts { get; }
            internal bool[] Captured { get; }
            internal int[] OverflowCounts { get; }
            internal CharacterTestRuntimeAiLabTimelineSessionIdentity SessionIdentity { get; }
            internal int SessionStartFrame { get; }
            internal int FrameLimit { get; }
            internal int CapturedFrameCount { get; }
            internal long ContentRevision { get; }
            internal long ContentHash { get; }
        }

        public void CaptureFrame(
            int frame,
            IReadOnlyList<RuntimeCommand> commands,
            IReadOnlyList<CharacterTestRuntimeEvent> runtimeEvents,
            CharacterTestTraceRecorder traceRecorder,
            CharacterTestCharacterActionTimelineEventBuffer actionEvents,
            CharacterTestRuntimeAiLabLogicEventBuffer logicEvents = null,
            int logicEventFrame = int.MinValue)
        {
            CaptureFrame(
                frame,
                new CharacterTestRuntimeAiLabFrozenArray<RuntimeCommand>(commands),
                runtimeEvents,
                traceRecorder,
                actionEvents,
                logicEvents,
                logicEventFrame);
        }

        public void CaptureFrame(
            int frame,
            CharacterTestRuntimeAiLabFrozenArray<RuntimeCommand> commands,
            IReadOnlyList<CharacterTestRuntimeEvent> runtimeEvents,
            CharacterTestTraceRecorder traceRecorder,
            CharacterTestCharacterActionTimelineEventBuffer actionEvents,
            CharacterTestRuntimeAiLabLogicEventBuffer logicEvents = null,
            int logicEventFrame = int.MinValue)
        {
            int frameOffset = frame - _sessionStartFrame;
            if (frameOffset < 0 || frameOffset >= _counts.Length)
                return;

            int slotStart = frameOffset * MaxEventsPerFrame;
            Array.Clear(_samples, slotStart, MaxEventsPerFrame);
            _counts[frameOffset] = 0;
            _overflowCounts[frameOffset] = 0;
            if (!_captured[frameOffset])
            {
                _captured[frameOffset] = true;
                _capturedFrameCount++;
            }
            int count = 0;
            int overflow = 0;
            int sourceOrder = 0;
            int expectedLogicEventFrame =
                logicEventFrame == int.MinValue ? frame : logicEventFrame;

            if (logicEvents != null)
            {
                overflow += logicEvents.OverflowCount;
                for (int i = 0; i < logicEvents.Count; i++)
                {
                    CharacterTestRuntimeAiLabLogicEvent evt = logicEvents[i];
                    if (evt.Frame != expectedLogicEventFrame)
                        continue;
                    AddOrOverflow(
                        slotStart,
                        ref count,
                        ref overflow,
                        new CharacterTestRuntimeAiLabTimelineEventSample(
                            frame,
                            ToTimelineEventKind(evt.Kind),
                            evt.EntityId,
                            0,
                            LogicEventCategory(evt.Kind),
                            CharacterTestRuntimeAiLabTimelineSeverity.Info,
                            BuildLogicEventLabel(evt),
                            evt.SourceIdentity,
                            sourceOrder++,
                            instanceId: evt.ProgramSemanticHash,
                            behaviorEntryId: evt.BehaviorEntryId,
                            behaviorStepId: evt.StageId,
                            profileSlot: evt.ProfileSlot,
                            cardInstanceStartFrame: evt.CardInstanceStartFrame,
                            behaviorTriggerSourceKind: evt.TriggerSourceKind,
                            behaviorOutcomeKind: evt.OutcomeKind,
                            behaviorTargetTimelineId: evt.TargetTimelineId,
                            behaviorLocalFrame: evt.LocalFrame,
                            behaviorPrimaryActionInstanceId: evt.PrimaryActionInstanceId,
                            behaviorReasonCode: evt.ReasonCode, sourceTraceLabel: evt.TransitionId, behaviorEntryNumericId: evt.BehaviorEntryNumericId, behaviorStepIndex: evt.StageIndex, behaviorEventIndex: evt.DetectorIndex, behaviorTransitionIndex: evt.TransitionIndex, behaviorTargetTimelineIndex: evt.TargetTimelineIndex, categoryCode: 1, categoryValue: (int)evt.Kind));
                }
            }

            if (actionEvents != null)
            {
                for (int i = 0; i < actionEvents.Count; i++)
                {
                    CharacterTestCharacterActionTimelineEvent evt = actionEvents[i];
                    if (evt.Frame != frame)
                        continue;

                    CharacterTestRuntimeAiLabTimelineEventKind kind;
                    CharacterTestRuntimeAiLabTimelineSeverity severity;
                    switch (evt.Kind)
                    {
                        case CharacterActionRunnerEventKind.ActionStarted:
                            kind = CharacterTestRuntimeAiLabTimelineEventKind.ActionStarted;
                            severity = CharacterTestRuntimeAiLabTimelineSeverity.Success;
                            break;
                        case CharacterActionRunnerEventKind.ActionFinished:
                        case CharacterActionRunnerEventKind.ActionCancelled:
                        case CharacterActionRunnerEventKind.ActionInterrupted:
                            kind = CharacterTestRuntimeAiLabTimelineEventKind.ActionEnded;
                            severity = evt.Kind == CharacterActionRunnerEventKind.ActionFinished
                                ? CharacterTestRuntimeAiLabTimelineSeverity.Success
                                : CharacterTestRuntimeAiLabTimelineSeverity.Warning;
                            break;
                        case CharacterActionRunnerEventKind.CancelRejected:
                        case CharacterActionRunnerEventKind.InterruptRejected:
                            kind = CharacterTestRuntimeAiLabTimelineEventKind.Warning;
                            severity = CharacterTestRuntimeAiLabTimelineSeverity.Warning;
                            break;
                        default:
                            kind = CharacterTestRuntimeAiLabTimelineEventKind.ActionEvent;
                            severity = CharacterTestRuntimeAiLabTimelineSeverity.Info;
                            break;
                    }

                    AddOrOverflow(
                        slotStart,
                        ref count,
                        ref overflow,
                        new CharacterTestRuntimeAiLabTimelineEventSample(
                            frame,
                            kind,
                            evt.EntityId,
                            0,
                            ActionEventCategory(evt.Kind),
                            severity,
                            string.Empty,
                            default,
                            sourceOrder++,
                            evt.InstanceId, numericLabel: evt.ActionId.Value, categoryCode: 2, categoryValue: (int)evt.Kind));
                }
            }

            if (runtimeEvents != null)
            {
                for (int i = 0; i < runtimeEvents.Count; i++)
                {
                    CharacterTestRuntimeEvent evt = runtimeEvents[i];
                    if (evt.Frame.Value != frame)
                        continue;
                    bool rejected = IsRejectedRuntimeEvent(evt.Type);
                    AddOrOverflow(
                        slotStart,
                        ref count,
                        ref overflow,
                        new CharacterTestRuntimeAiLabTimelineEventSample(
                            frame,
                            rejected
                                ? CharacterTestRuntimeAiLabTimelineEventKind.Warning
                                : CharacterTestRuntimeAiLabTimelineEventKind.Command,
                            evt.EntityId != 0 ? evt.EntityId : evt.SourceId,
                            evt.TargetId,
                            RuntimeEventCategory(evt.Type),
                            rejected
                                ? CharacterTestRuntimeAiLabTimelineSeverity.Warning
                                : CharacterTestRuntimeAiLabTimelineSeverity.Info,
                            evt.Message,
                            default,
                            sourceOrder++, categoryCode: 3, categoryValue: (int)evt.Type));
                }
            }

            IReadOnlyList<CharacterTestTraceEvent> traces = traceRecorder?.RecentEvents;
            if (traces != null)
            {
                for (int i = 0; i < traces.Count; i++)
                {
                    CharacterTestTraceEvent trace = traces[i];
                    if (trace.Frame != frame)
                        continue;

                    CharacterTestRuntimeAiLabTimelineEventKind kind =
                        CharacterTestRuntimeAiLabTimelineEventKind.ActionEvent;
                    CharacterTestRuntimeAiLabTimelineSeverity severity = trace.Warning
                        ? CharacterTestRuntimeAiLabTimelineSeverity.Warning
                        : CharacterTestRuntimeAiLabTimelineSeverity.Info;
                    int relatedEntityId = 0;
                    int value = 0;
                    int traceNumber = 0;
                    if (trace.Kind == CharacterTestTraceEventKind.CombatHit)
                    {
                        CharacterTestCombatHitTraceEvent hit = trace.CombatHitTrace;
                        kind = hit.Damage > 0
                            ? CharacterTestRuntimeAiLabTimelineEventKind.Damage
                            : CharacterTestRuntimeAiLabTimelineEventKind.CombatHit;
                        relatedEntityId = hit.TargetEntityId;
                        value = hit.Damage;
                        traceNumber = hit.TraceId;
                        severity = CharacterTestRuntimeAiLabTimelineSeverity.Success;
                    }
                    else if (trace.Warning)
                    {
                        kind = CharacterTestRuntimeAiLabTimelineEventKind.Warning;
                    }

                    AddOrOverflow(
                        slotStart,
                        ref count,
                        ref overflow,
                        new CharacterTestRuntimeAiLabTimelineEventSample(
                            frame,
                            kind,
                            trace.EntityId,
                            relatedEntityId,
                            trace.Category,
                            severity,
                            trace.Kind == CharacterTestTraceEventKind.Text
                                ? trace.Message
                                : TraceEventLabel(trace.Kind),
                            default,
                            sourceOrder++,
                            value: value,
                            traceNumber: traceNumber, categoryCode: 4, categoryValue: (int)trace.Kind));
                }
            }

            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                int entityId = command.TargetId != 0
                    ? command.TargetId
                    : command.SourceId;
                int relatedEntityId = command.TargetId != 0
                    ? command.SourceId
                    : 0;
                AddOrOverflow(
                    slotStart,
                    ref count,
                    ref overflow,
                    new CharacterTestRuntimeAiLabTimelineEventSample(
                        frame,
                        CharacterTestRuntimeAiLabTimelineEventKind.Command,
                        entityId,
                        relatedEntityId,
                        RuntimeCommandCategory(command.CommandId),
                        CharacterTestRuntimeAiLabTimelineSeverity.Info,
                        RuntimeCommandLabel(command),
                        command.TraceId,
                        sourceOrder++,
                        traceNumber: command.CommandId, categoryCode: 5, labelCode: RuntimeCommandLabelCode(command)));
            }

            if (overflow > 0)
            {
                _overflowCounts[frameOffset] = overflow;
                _samples[slotStart + count] = new CharacterTestRuntimeAiLabTimelineEventSample(
                    frame,
                    CharacterTestRuntimeAiLabTimelineEventKind.Warning,
                    0,
                    0,
                    "TimelineOverflow",
                    CharacterTestRuntimeAiLabTimelineSeverity.Warning,
                    "本帧有事件被聚合",
                    default,
                    sourceOrder,
                    value: overflow, categoryCode: 6);
                count++;
            }

            _counts[frameOffset] = count;
            if (logicEvents != null)
            {
                for (int i = 0; i < logicEvents.Count; i++)
                {
                    if (logicEvents[i].Frame != expectedLogicEventFrame)
                        AppendLogicEvent(logicEvents[i]);
                }
            }
            _contentRevision++;
        }

        private void AppendLogicEvent(CharacterTestRuntimeAiLabLogicEvent evt)
        {
            int frameOffset = evt.Frame - _sessionStartFrame;
            if (frameOffset < 0 || frameOffset >= _counts.Length)
                return;
            int slotStart = frameOffset * MaxEventsPerFrame;
            int count = _counts[frameOffset];
            CharacterTestRuntimeAiLabTimelineEventKind kind = ToTimelineEventKind(evt.Kind);
            string label = BuildLogicEventLabel(evt);
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabTimelineEventSample existing = _samples[slotStart + i];
                if (existing.Kind == kind
                    && existing.EntityId == evt.EntityId
                    && string.Equals(existing.Label, label, StringComparison.Ordinal)
                    && existing.SourceTraceId.Equals(evt.SourceIdentity)
                    && existing.BehaviorStepIndex == evt.StageIndex
                    && existing.ProfileSlot == evt.ProfileSlot
                    && existing.CardInstanceStartFrame == evt.CardInstanceStartFrame
                    && existing.BehaviorTriggerSourceKind == evt.TriggerSourceKind
                    && existing.BehaviorOutcomeKind == evt.OutcomeKind
                    && string.Equals(
                        existing.BehaviorTargetTimelineId,
                        evt.TargetTimelineId,
                        StringComparison.Ordinal)
                    && existing.BehaviorLocalFrame == evt.LocalFrame
                    && existing.BehaviorPrimaryActionInstanceId == evt.PrimaryActionInstanceId
                    && (existing.BehaviorReasonCode == evt.ReasonCode))
                {
                    return;
                }
            }
            if (count >= MaxEventsPerFrame)
            {
                _overflowCounts[frameOffset]++;
                return;
            }
            _samples[slotStart + count] = new CharacterTestRuntimeAiLabTimelineEventSample(
                evt.Frame,
                kind,
                evt.EntityId,
                0,
                LogicEventCategory(evt.Kind),
                CharacterTestRuntimeAiLabTimelineSeverity.Info,
                label,
                evt.SourceIdentity,
                count,
                instanceId: evt.ProgramSemanticHash,
                behaviorEntryId: evt.BehaviorEntryId,
                behaviorStepId: evt.StageId,
                profileSlot: evt.ProfileSlot,
                cardInstanceStartFrame: evt.CardInstanceStartFrame,
                behaviorTriggerSourceKind: evt.TriggerSourceKind,
                behaviorOutcomeKind: evt.OutcomeKind,
                behaviorTargetTimelineId: evt.TargetTimelineId,
                behaviorLocalFrame: evt.LocalFrame,
                behaviorPrimaryActionInstanceId: evt.PrimaryActionInstanceId,
                behaviorReasonCode: evt.ReasonCode, sourceTraceLabel: evt.TransitionId, behaviorEntryNumericId: evt.BehaviorEntryNumericId, behaviorStepIndex: evt.StageIndex, behaviorEventIndex: evt.DetectorIndex, behaviorTransitionIndex: evt.TransitionIndex, behaviorTargetTimelineIndex: evt.TargetTimelineIndex, categoryCode: 1, categoryValue: (int)evt.Kind);
            _counts[frameOffset] = count + 1;
        }

        public int GetCount(int frame)
        {
            int offset = frame - _sessionStartFrame;
            return offset >= 0 && offset < _counts.Length ? _counts[offset] : 0;
        }

        public bool IsFrameCaptured(int frame)
        {
            int offset = frame - _sessionStartFrame;
            return offset >= 0 && offset < _captured.Length && _captured[offset];
        }

        public int GetOverflowCount(int frame)
        {
            int offset = frame - _sessionStartFrame;
            return offset >= 0 && offset < _overflowCounts.Length ? _overflowCounts[offset] : 0;
        }

        public CharacterTestRuntimeAiLabTimelineEventSample GetSample(int frame, int index)
        {
            int offset = frame - _sessionStartFrame;
            int count = offset >= 0 && offset < _counts.Length ? _counts[offset] : 0;
            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _samples[offset * MaxEventsPerFrame + index];
        }

        public void TruncateAfter(int frame)
        {
            int firstOffset = Math.Max(0, frame - _sessionStartFrame + 1);
            bool changed = false;
            for (int offset = firstOffset; offset < _counts.Length; offset++)
            {
                if (!_captured[offset])
                    continue;
                Array.Clear(_samples, offset * MaxEventsPerFrame, MaxEventsPerFrame);
                _counts[offset] = 0;
                _overflowCounts[offset] = 0;
                _captured[offset] = false;
                _capturedFrameCount--;
                changed = true;
            }

            if (changed)
                _contentRevision++;
        }

        private static string ActionEventCategory(CharacterActionRunnerEventKind kind)
        {
            switch (kind)
            {
                case CharacterActionRunnerEventKind.ActionStarted: return "ActionStarted";
                case CharacterActionRunnerEventKind.PhaseChanged: return "PhaseChanged";
                case CharacterActionRunnerEventKind.TrackEventFired: return "TrackEventFired";
                case CharacterActionRunnerEventKind.ActionFinished: return "ActionFinished";
                case CharacterActionRunnerEventKind.ActionCancelled: return "ActionCancelled";
                case CharacterActionRunnerEventKind.ActionInterrupted: return "ActionInterrupted";
                case CharacterActionRunnerEventKind.CancelRejected: return "CancelRejected";
                case CharacterActionRunnerEventKind.InterruptRejected: return "InterruptRejected";
                default: return "ActionEvent";
            }
        }

        private static string RuntimeCommandCategory(int commandId)
        {
            switch (commandId)
            {
                case CharacterTestRuntimeCommandIds.SpawnCharacter: return "RuntimeCommand.901000";
                case CharacterTestRuntimeCommandIds.SetCameraMode: return "RuntimeCommand.901010";
                case CharacterTestRuntimeCommandIds.ToggleTargetLock: return "RuntimeCommand.901011";
                case CharacterTestRuntimeCommandIds.SetTargetLock: return "RuntimeCommand.901012";
                case CharacterTestRuntimeCommandIds.SetControlIntent: return "RuntimeCommand.901020";
                case CharacterTestRuntimeCommandIds.CastAbilitySlot: return "RuntimeCommand.901021";
                case CharacterTestRuntimeCommandIds.SetRuntimeAiBehaviorSelection: return "RuntimeCommand.901022";
                case CharacterTestRuntimeCommandIds.RequestCharacterAction: return "RuntimeCommand.901023";
                case CharacterTestRuntimeCommandIds.BehaviorRecipeOrchestration: return "RuntimeCommand.901024";
                case CharacterTestRuntimeCommandIds.ApplyAuthorityPosePreview: return "RuntimeCommand.901030";
                case CharacterTestRuntimeCommandIds.SurfaceProbe: return "RuntimeCommand.901040";
                case CharacterTestRuntimeCommandIds.GenesisMovableObjectProbe: return "RuntimeCommand.901041";
                case CharacterTestRuntimeCommandIds.GenesisPotencyMechanicsProbe: return "RuntimeCommand.901042";
                case CharacterTestRuntimeCommandIds.MovableNaturalObjectReset: return "RuntimeCommand.901043";
                case CharacterTestRuntimeCommandIds.MovableNaturalObjectPotencyInfluence: return "RuntimeCommand.901044";
                case CharacterTestRuntimeCommandIds.GenesisLivingBeingProbe: return "RuntimeCommand.901045";
                case CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesProbe: return "RuntimeCommand.901046";
                case CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesRuntimeState: return "RuntimeCommand.901047";
                case CharacterTestRuntimeCommandIds.ReviveCharacter: return "RuntimeCommand.901050";
                case CharacterTestRuntimeCommandIds.DestroyCharacter: return "RuntimeCommand.901060";
                default: return "RuntimeCommand." + commandId.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static int RuntimeCommandLabelCode(in RuntimeCommand command)
        {
            if (command.CommandId == CharacterTestRuntimeCommandIds.SetControlIntent)
            {
                CharacterTestControlIntent intent = CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
                if (intent.AttackPrimaryPressed) return 1;
                if (intent.DodgePressed) return 2;
                if (intent.SkillSlotPressedMask != 0) return 3;
                return intent.MoveXMilli != 0 || intent.MoveYMilli != 0 || intent.WorldMoveXMilli != 0 || intent.WorldMoveYMilli != 0 ? 4 : 5;
            }
            if (command.CommandId == CharacterTestRuntimeCommandIds.RequestCharacterAction) return command.Payload0 != 0 ? 6 : 7;
            return 0; // The command number already distinguishes the other fixed labels.
        }

        private static string RuntimeCommandLabel(in RuntimeCommand command)
        {
            switch (command.CommandId)
            {
                case CharacterTestRuntimeCommandIds.SetControlIntent:
                    CharacterTestControlIntent intent =
                        CharacterTestRuntimeCommandFactory.DecodeControlIntent(command);
                    if (intent.AttackPrimaryPressed)
                        return "基础攻击";
                    if (intent.DodgePressed)
                        return "闪避";
                    if (intent.SkillSlotPressedMask != 0)
                        return "使用技能";
                    if (intent.MoveXMilli != 0
                        || intent.MoveYMilli != 0
                        || intent.WorldMoveXMilli != 0
                        || intent.WorldMoveYMilli != 0)
                    {
                        return "移动";
                    }
                    return "停止";
                case CharacterTestRuntimeCommandIds.CastAbilitySlot:
                    return "施放技能";
                case CharacterTestRuntimeCommandIds.RequestCharacterAction:
                    return command.Payload0 != 0 ? "执行动作" : "取消动作";
                case CharacterTestRuntimeCommandIds.SpawnCharacter:
                    return "生成角色";
                case CharacterTestRuntimeCommandIds.ReviveCharacter:
                    return "复活";
                case CharacterTestRuntimeCommandIds.DestroyCharacter:
                    return "移除角色";
                case CharacterTestRuntimeCommandIds.SetCameraMode: return "命令 901010";
                case CharacterTestRuntimeCommandIds.ToggleTargetLock: return "命令 901011";
                case CharacterTestRuntimeCommandIds.SetTargetLock: return "命令 901012";
                case CharacterTestRuntimeCommandIds.SetRuntimeAiBehaviorSelection: return "命令 901022";
                case CharacterTestRuntimeCommandIds.BehaviorRecipeOrchestration: return "命令 901024";
                case CharacterTestRuntimeCommandIds.ApplyAuthorityPosePreview: return "命令 901030";
                case CharacterTestRuntimeCommandIds.SurfaceProbe: return "命令 901040";
                case CharacterTestRuntimeCommandIds.GenesisMovableObjectProbe: return "命令 901041";
                case CharacterTestRuntimeCommandIds.GenesisPotencyMechanicsProbe: return "命令 901042";
                case CharacterTestRuntimeCommandIds.MovableNaturalObjectReset: return "命令 901043";
                case CharacterTestRuntimeCommandIds.MovableNaturalObjectPotencyInfluence: return "命令 901044";
                case CharacterTestRuntimeCommandIds.GenesisLivingBeingProbe: return "命令 901045";
                case CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesProbe: return "命令 901046";
                case CharacterTestRuntimeCommandIds.GenesisLivingNaturalAttributesRuntimeState: return "命令 901047";
                default: return "命令 " + command.CommandId.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static CharacterTestRuntimeAiLabTimelineEventKind ToTimelineEventKind(
            CharacterTestRuntimeAiLabLogicEventKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabLogicEventKind.StageEntered:
                    return CharacterTestRuntimeAiLabTimelineEventKind.BehaviorStageEntered;
                case CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered:
                    return CharacterTestRuntimeAiLabTimelineEventKind.BehaviorDetectorTriggered;
                case CharacterTestRuntimeAiLabLogicEventKind.TransitionPending:
                case CharacterTestRuntimeAiLabLogicEventKind.ActionInterruptRequested:
                    return CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionPending;
                case CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted:
                    return CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionCommitted;
                case CharacterTestRuntimeAiLabLogicEventKind.CardEnded:
                    return CharacterTestRuntimeAiLabTimelineEventKind.BehaviorCardEnded;
                default:
                    return CharacterTestRuntimeAiLabTimelineEventKind.Warning;
            }
        }

        private static string LogicEventCategory(CharacterTestRuntimeAiLabLogicEventKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabLogicEventKind.StageEntered: return "BehaviorLogic.StageEntered";
                case CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered: return "BehaviorLogic.DetectorTriggered";
                case CharacterTestRuntimeAiLabLogicEventKind.TransitionPending: return "BehaviorLogic.TransitionPending";
                case CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted: return "BehaviorLogic.TransitionCommitted";
                case CharacterTestRuntimeAiLabLogicEventKind.ActionInterruptRequested: return "BehaviorLogic.ActionInterruptRequested";
                case CharacterTestRuntimeAiLabLogicEventKind.CardEnded: return "BehaviorLogic.CardEnded";
                default: return "BehaviorLogic.Error";
            }
        }

        private static string BuildLogicEventLabel(in CharacterTestRuntimeAiLabLogicEvent evt)
        {
            if (evt.Kind == CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered
                && !string.IsNullOrEmpty(evt.DetectorId))
            {
                return evt.DetectorId;
            }
            if (!string.IsNullOrEmpty(evt.TransitionId))
                return evt.TransitionId;
            if (!string.IsNullOrEmpty(evt.DetectorId))
                return evt.DetectorId;
            return evt.StageId;
        }

        private static bool IsRejectedRuntimeEvent(CharacterTestRuntimeEventType type)
        {
            switch (type)
            {
                case CharacterTestRuntimeEventType.CommandRejected:
                case CharacterTestRuntimeEventType.CharacterSpawnRejected:
                case CharacterTestRuntimeEventType.TargetLockRejected:
                case CharacterTestRuntimeEventType.ControlIntentRejected:
                case CharacterTestRuntimeEventType.CharacterReviveRejected:
                case CharacterTestRuntimeEventType.CharacterDestroyRejected:
                case CharacterTestRuntimeEventType.RuntimeAiBehaviorSelectionRejected:
                case CharacterTestRuntimeEventType.SurfaceProbeRejected:
                case CharacterTestRuntimeEventType.GenesisMovableObjectProbeRejected:
                case CharacterTestRuntimeEventType.GenesisPotencyMechanicsProbeRejected:
                case CharacterTestRuntimeEventType.GenesisLivingBeingProbeRejected:
                case CharacterTestRuntimeEventType.GenesisLivingNaturalAttributesProbeRejected:
                case CharacterTestRuntimeEventType.GenesisLivingNaturalAttributesRuntimeStateRejected:
                case CharacterTestRuntimeEventType.MovableNaturalObjectResetRejected:
                case CharacterTestRuntimeEventType.MovableNaturalObjectPotencyInfluenceRejected:
                    return true;
                default:
                    return false;
            }
        }

        private static string RuntimeEventCategory(CharacterTestRuntimeEventType type)
        {
            switch (type)
            {
                case CharacterTestRuntimeEventType.CommandAccepted: return "CommandAccepted";
                case CharacterTestRuntimeEventType.CommandRejected: return "CommandRejected";
                case CharacterTestRuntimeEventType.ControlIntentApplied: return "ControlIntentApplied";
                case CharacterTestRuntimeEventType.ControlIntentRejected: return "ControlIntentRejected";
                case CharacterTestRuntimeEventType.TargetLockChanged: return "TargetLockChanged";
                case CharacterTestRuntimeEventType.TargetLockRejected: return "TargetLockRejected";
                case CharacterTestRuntimeEventType.RuntimeAiBehaviorSelectionApplied: return "BehaviorSelectionApplied";
                case CharacterTestRuntimeEventType.RuntimeAiBehaviorSelectionRejected: return "BehaviorSelectionRejected";
                default: return "RuntimeEvent";
            }
        }

        private static string TraceEventLabel(CharacterTestTraceEventKind kind)
        {
            switch (kind)
            {
                case CharacterTestTraceEventKind.CombatHit: return "命中";
                case CharacterTestTraceEventKind.CombatHitQuery: return "命中查询";
                case CharacterTestTraceEventKind.ActionInput: return "动作输入";
                case CharacterTestTraceEventKind.ActionOutcome: return "动作结果";
                case CharacterTestTraceEventKind.CollisionProfileBlocked: return "碰撞阻挡";
                case CharacterTestTraceEventKind.CohesionBreak: return "内聚破坏";
                default: return "运行时事件";
            }
        }

        private void AddOrOverflow(
            int slotStart,
            ref int count,
            ref int overflow,
            in CharacterTestRuntimeAiLabTimelineEventSample sample)
        {
            if (count >= PayloadEventsPerFrame)
            {
                overflow++;
                return;
            }
            _samples[slotStart + count] = sample;
            count++;
        }
    }
}
