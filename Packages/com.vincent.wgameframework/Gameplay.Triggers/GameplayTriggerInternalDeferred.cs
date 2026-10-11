using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    /// <summary>One committed Trigger-owned action awaiting its due frame.</summary>
    public readonly struct GameplayTriggerDeferredRecord
    {
        internal GameplayTriggerDeferredRecord(
            RuntimeFrame originalDueFrame,
            long sequence,
            GameplayTriggerInternalDeferredOperationKind operationKind,
            long activationId,
            int actionIndex,
            GameplayTriggerActionKindId actionKindId,
            GameplayTriggerBindingId originBindingId,
            GameplayTriggerRouteId originRouteId,
            GameplayTriggerInstanceId targetInstanceId,
            GameplayTriggerSourceBindingId targetSourceBindingId,
            bool enabled,
            int signalId,
            GameplayEntityId subject,
            GameplayEntityId instigator,
            long payload0,
            long payload1,
            long payload2)
        {
            OriginalDueFrame = originalDueFrame;
            Sequence = sequence;
            OperationKind = operationKind;
            ActivationId = activationId;
            ActionIndex = actionIndex;
            ActionKindId = actionKindId;
            OriginBindingId = originBindingId;
            OriginRouteId = originRouteId;
            TargetInstanceId = targetInstanceId;
            TargetSourceBindingId = targetSourceBindingId;
            Enabled = enabled;
            SignalId = signalId;
            Subject = subject;
            Instigator = instigator;
            Payload0 = payload0;
            Payload1 = payload1;
            Payload2 = payload2;
        }

        public RuntimeFrame OriginalDueFrame { get; }
        public long Sequence { get; }
        public GameplayTriggerInternalDeferredOperationKind OperationKind { get; }
        public long ActivationId { get; }
        public int ActionIndex { get; }
        public GameplayTriggerActionKindId ActionKindId { get; }
        public GameplayTriggerBindingId OriginBindingId { get; }
        public GameplayTriggerRouteId OriginRouteId { get; }
        public GameplayTriggerInstanceId TargetInstanceId { get; }
        public GameplayTriggerSourceBindingId TargetSourceBindingId { get; }
        public bool Enabled { get; }
        public int SignalId { get; }
        public GameplayEntityId Subject { get; }
        public GameplayEntityId Instigator { get; }
        public long Payload0 { get; }
        public long Payload1 { get; }
        public long Payload2 { get; }
    }

    /// <summary>Numeric result for the most recently considered deferred record.</summary>
    public readonly struct GameplayTriggerDeferredExecutionResult
    {
        internal GameplayTriggerDeferredExecutionResult(
            RuntimeFrame frame,
            long sequence,
            GameplayTriggerInstanceId targetInstanceId,
            GameplayTriggerSourceBindingId targetSourceBindingId,
            long activationId,
            int actionIndex,
            GameplayTriggerInternalDeferredOperationKind operationKind,
            GameplayTriggerResult result)
        {
            Frame = frame;
            Sequence = sequence;
            TargetInstanceId = targetInstanceId;
            TargetSourceBindingId = targetSourceBindingId;
            ActivationId = activationId;
            ActionIndex = actionIndex;
            OperationKind = operationKind;
            Result = result;
        }

        public RuntimeFrame Frame { get; }
        public long Sequence { get; }
        public GameplayTriggerInstanceId TargetInstanceId { get; }
        public GameplayTriggerSourceBindingId TargetSourceBindingId { get; }
        public long ActivationId { get; }
        public int ActionIndex { get; }
        public GameplayTriggerInternalDeferredOperationKind OperationKind { get; }
        public GameplayTriggerResult Result { get; }
    }

    public sealed partial class GameplayTriggerRuntime
    {
        private GameplayTriggerDeferredRecord[] _deferredRecords;
        private DeferredReservation[] _deferredReservations;
        private bool[] _deferredRemoveScratch;
        private bool[] _deferredSignalStaged;
        private bool[] _deferredSignalSourceBlocked;
        private int _deferredRecordCount;
        private int _deferredReservationCount;
        private long _nextDeferredSequence = 1L;
        private bool _hasPendingDeferredSignalFinalization;

        /// <summary>Gets the committed deferred-record count.</summary>
        /// <remarks>NoAlloc. Records remain ordered by original due frame and sequence.</remarks>
        public int DeferredRecordCount => _deferredRecordCount;

        /// <summary>Gets the sequence that will be assigned to the next committed record.</summary>
        /// <remarks>NoAlloc.</remarks>
        public long NextDeferredSequence => _nextDeferredSequence;

        /// <summary>Gets the most recent deferred execution or retention result.</summary>
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerDeferredExecutionResult LastDeferredExecutionResult { get; private set; }

        /// <summary>Gets one committed deferred record in canonical queue order.</summary>
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerDeferredRecord GetDeferredRecord(int index) => _deferredRecords[index];

        internal bool HasPendingDeferredSignalFinalization => _hasPendingDeferredSignalFinalization;
        internal int DeferredReservationCount => _deferredReservationCount;

        private void InitializeDeferredAuthority()
        {
            if (_catalog.InternalDeferredActionCount > 0 && _options.MaxDeferredRecords == 0)
            {
                throw new ArgumentException(
                    "A catalog with internal deferred actions requires MaxDeferredRecords greater than zero.",
                    nameof(_options));
            }

            _deferredRecords = new GameplayTriggerDeferredRecord[_options.MaxDeferredRecords];
            _deferredReservations = new DeferredReservation[_options.MaxDeferredRecords];
            _deferredRemoveScratch = new bool[_options.MaxDeferredRecords];
            _deferredSignalStaged = new bool[_options.MaxDeferredRecords];
            _deferredSignalSourceBlocked = new bool[_instances.Length];
        }

        private bool TryPrepareInternalDeferred(
            GameplayTriggerActionSinkEntry entry,
            in GameplayTriggerActionPreparation preparation,
            out int token,
            out GameplayTriggerErrorCode errorCode)
        {
            token = 0;
            if (preparation.Frame.Value == long.MaxValue)
            {
                errorCode = GameplayTriggerErrorCode.DeferredFrameOverflow;
                return false;
            }
            if (_deferredRecordCount + _deferredReservationCount >= _deferredRecords.Length)
            {
                errorCode = GameplayTriggerErrorCode.DeferredQueueCapacityExceeded;
                return false;
            }
            if (_nextDeferredSequence <= 0L ||
                _nextDeferredSequence >= long.MaxValue - _deferredReservationCount)
            {
                errorCode = GameplayTriggerErrorCode.DeferredSequenceExhausted;
                return false;
            }
            if (preparation.Target.DomainId != GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId ||
                preparation.Target.Generation != 0 ||
                preparation.Target.Value <= 0L ||
                preparation.Target.Value > int.MaxValue)
            {
                errorCode = GameplayTriggerErrorCode.DeferredInvalidTarget;
                return false;
            }

            var targetSourceBindingId = new GameplayTriggerSourceBindingId((int)preparation.Target.Value);
            int instanceIndex = FindInstance(targetSourceBindingId);
            if (instanceIndex < 0)
            {
                errorCode = GameplayTriggerErrorCode.DeferredInvalidTarget;
                return false;
            }
            if (entry.InternalOperationKind == GameplayTriggerInternalDeferredOperationKind.Signal &&
                _catalog.GetSource(_instances[instanceIndex].SourceIndex).SourceKind !=
                    GameplayTriggerSourceKind.Signal)
            {
                errorCode = GameplayTriggerErrorCode.DeferredSourceKindMismatch;
                return false;
            }

            for (int i = 0; i < _deferredReservations.Length; i++)
            {
                if (_deferredReservations[i].Active) continue;
                _deferredReservations[i] = new DeferredReservation(
                    true,
                    entry,
                    preparation,
                    _instances[instanceIndex].InstanceId,
                    targetSourceBindingId);
                _deferredReservationCount++;
                token = i + 1;
                errorCode = GameplayTriggerErrorCode.None;
                return true;
            }

            errorCode = GameplayTriggerErrorCode.DeferredQueueCapacityExceeded;
            return false;
        }

        private void AbortInternalDeferred(int token)
        {
            int index = token - 1;
            if ((uint)index >= (uint)_deferredReservations.Length ||
                !_deferredReservations[index].Active)
                return;
            _deferredReservations[index] = default;
            _deferredReservationCount--;
        }

        private void CommitInternalDeferred(
            int token,
            long activationId,
            in GameplayTriggerActionIntent intent)
        {
            int index = token - 1;
            if ((uint)index >= (uint)_deferredReservations.Length ||
                !_deferredReservations[index].Active)
                throw new InvalidOperationException("Deferred reservation token is not active.");

            DeferredReservation reservation = _deferredReservations[index];
            GameplayTriggerActionPreparation preparation = reservation.Preparation;
            GameplayTriggerOccurrence occurrence = preparation.Occurrence;
            GameplayTriggerActionInstruction instruction = preparation.Instruction;
            _deferredRecords[_deferredRecordCount++] = new GameplayTriggerDeferredRecord(
                new RuntimeFrame(preparation.Frame.Value + 1L),
                _nextDeferredSequence++,
                reservation.Entry.InternalOperationKind,
                activationId,
                preparation.ActionIndex,
                instruction.ActionKindId,
                preparation.BindingId,
                preparation.RouteId,
                reservation.TargetInstanceId,
                reservation.TargetSourceBindingId,
                reservation.Entry.InternalOperationKind ==
                    GameplayTriggerInternalDeferredOperationKind.SetEnabled &&
                    instruction.Payload0 != 0L,
                reservation.Entry.SignalId,
                occurrence.Subject,
                occurrence.Instigator,
                instruction.Payload0,
                instruction.Payload1,
                instruction.Payload2);
            _deferredReservations[index] = default;
            _deferredReservationCount--;
        }

        internal GameplayTriggerResult StageDueDeferred(RuntimeFrame frame)
        {
            if (_hasPendingDeferredSignalFinalization)
            {
                return new GameplayTriggerResult(
                    false,
                    GameplayTriggerErrorCode.DeferredSignalCollision,
                    (int)GameplayTriggerCheckpointPendingDetailCode.PendingDeferredSignalFinalization);
            }

            Array.Clear(_deferredRemoveScratch, 0, _deferredRecordCount);
            Array.Clear(_deferredSignalStaged, 0, _deferredRecordCount);
            Array.Clear(_deferredSignalSourceBlocked, 0, _deferredSignalSourceBlocked.Length);
            GameplayTriggerResult firstFailure = GameplayTriggerResult.Succeeded;
            bool stagedAnySignal = false;
            for (int i = 0; i < _deferredRecordCount; i++)
            {
                GameplayTriggerDeferredRecord record = _deferredRecords[i];
                if (record.OriginalDueFrame > frame) continue;
                GameplayTriggerErrorCode validation = ValidateDeferredTarget(record, out int instanceIndex);
                if (validation != GameplayTriggerErrorCode.None)
                {
                    _deferredRemoveScratch[i] = true;
                    var failure = new GameplayTriggerResult(false, validation);
                    RecordDeferredResult(frame, record, failure);
                    if (firstFailure.Success) firstFailure = failure;
                    continue;
                }

                GameplayTriggerResult result;
                switch (record.OperationKind)
                {
                    case GameplayTriggerInternalDeferredOperationKind.SetEnabled:
                        if (!record.Enabled)
                            MarkStagedSignalsForInstance(
                                record.TargetInstanceId,
                                compactImmediately: false);
                        TrySetEnabled(record.TargetInstanceId, record.Enabled, out result);
                        _deferredRemoveScratch[i] = true;
                        break;
                    case GameplayTriggerInternalDeferredOperationKind.Reset:
                        MarkStagedSignalsForInstance(
                            record.TargetInstanceId,
                            compactImmediately: false);
                        TryReset(record.TargetInstanceId, out result);
                        _deferredRemoveScratch[i] = true;
                        break;
                    case GameplayTriggerInternalDeferredOperationKind.Signal:
                        _deferredSignalStaged[i] = true;
                        stagedAnySignal = true;
                        result = GameplayTriggerResult.Succeeded;
                        break;
                    default:
                        result = new GameplayTriggerResult(
                            false,
                            GameplayTriggerErrorCode.DeferredInvalidTarget);
                        _deferredRemoveScratch[i] = true;
                        break;
                }
                RecordDeferredResult(frame, record, result);
                if (!result.Success && firstFailure.Success) firstFailure = result;
            }

            CompactDeferredRecords();
            if (stagedAnySignal)
            {
                stagedAnySignal = false;
                for (int i = 0; i < _deferredRecordCount; i++)
                {
                    if (!_deferredSignalStaged[i]) continue;
                    stagedAnySignal = true;
                    break;
                }
            }
            _hasPendingDeferredSignalFinalization = stagedAnySignal;
            return firstFailure;
        }

        internal void ClearStagedSignalsForInstance(GameplayTriggerInstanceId instanceId)
        {
            if (!_hasPendingDeferredSignalFinalization) return;
            MarkStagedSignalsForInstance(instanceId, compactImmediately: true);
        }

        private void MarkStagedSignalsForInstance(
            GameplayTriggerInstanceId instanceId,
            bool compactImmediately)
        {
            bool removed = false;
            for (int i = 0; i < _deferredRecordCount; i++)
            {
                if (!_deferredSignalStaged[i] ||
                    _deferredRecords[i].TargetInstanceId != instanceId)
                    continue;
                _deferredRemoveScratch[i] = true;
                removed = true;
            }
            if (removed && compactImmediately) CompactDeferredRecords();
        }

        internal void BlockStagedSignalsForSource(GameplayTriggerSourceBindingId source)
        {
            int instanceIndex = FindInstance(source);
            if (instanceIndex >= 0) _deferredSignalSourceBlocked[instanceIndex] = true;
        }

        internal bool TryFinalizeDeferredSignalsForSource(
            RuntimeFrame frame,
            GameplayTriggerSourceBindingId source,
            Span<GameplayTriggerSignal> scratch,
            int externalCount,
            GameplayTriggerSignalOrdering ordering,
            bool hasExternalBatch,
            out int consumedCount,
            out GameplayTriggerResult result)
        {
            consumedCount = 0;
            int instanceIndex = FindInstance(source);
            if (instanceIndex < 0)
            {
                result = new GameplayTriggerResult(
                    false,
                    GameplayTriggerErrorCode.UnknownSourceBinding,
                    source.Value);
                return false;
            }

            if (hasExternalBatch && ordering == GameplayTriggerSignalOrdering.Ordered)
            {
                BlockStagedSignalsForSource(source);
                bool submitted = TrySubmitSignals(
                    frame,
                    source,
                    ordering,
                    scratch.Slice(0, externalCount),
                    out result);
                RecordDeferredFailureForSource(
                    frame,
                    source,
                    GameplayTriggerErrorCode.DeferredSignalCollision);
                return submitted;
            }

            bool hasDeferredSignals = HasStagedSignalsForSource(source);
            int available = Math.Min(
                scratch.Length - externalCount,
                _signalInputs.Length - _signalInputCount - externalCount);
            if (available < 0) available = 0;
            for (int i = 0; i < _deferredRecordCount && consumedCount < available; i++)
            {
                if (!_deferredSignalStaged[i] ||
                    _deferredRecords[i].TargetSourceBindingId != source)
                    continue;
                GameplayTriggerDeferredRecord record = _deferredRecords[i];
                scratch[externalCount + consumedCount] = new GameplayTriggerSignal(
                    record.SignalId,
                    record.Subject,
                    record.Instigator,
                    record.Payload0,
                    record.Payload1,
                    record.Payload2,
                    record.Sequence);
                consumedCount++;
            }

            if (externalCount + consumedCount == 0 && !hasExternalBatch)
            {
                result = new GameplayTriggerResult(
                    false,
                    hasDeferredSignals
                        ? GameplayTriggerErrorCode.DeferredInputCapacityExceeded
                        : GameplayTriggerErrorCode.DeferredSignalCollision);
                RecordDeferredFailureForSource(frame, source, result.ErrorCode);
                return false;
            }

            if (!TrySubmitSignals(
                    frame,
                    source,
                    ordering,
                    scratch.Slice(0, externalCount + consumedCount),
                    out result))
            {
                consumedCount = 0;
                GameplayTriggerErrorCode deferredError = result.ErrorCode ==
                    GameplayTriggerErrorCode.InputCapacityExceeded
                    ? GameplayTriggerErrorCode.DeferredInputCapacityExceeded
                    : GameplayTriggerErrorCode.DeferredSignalCollision;
                RecordDeferredFailureForSource(frame, source, deferredError);
                if (!hasExternalBatch)
                    result = new GameplayTriggerResult(false, deferredError);
                return false;
            }

            if (consumedCount > 0)
                ConsumeStagedSignals(source, consumedCount);
            if (HasStagedSignalsForSource(source))
            {
                RecordDeferredFailureForSource(
                    frame,
                    source,
                    hasExternalBatch && consumedCount == 0
                        ? GameplayTriggerErrorCode.DeferredSignalCollision
                        : GameplayTriggerErrorCode.DeferredInputCapacityExceeded);
            }
            return true;
        }

        internal bool HasStagedSignalsForSource(GameplayTriggerSourceBindingId source)
        {
            int instanceIndex = FindInstance(source);
            if (instanceIndex >= 0 && _deferredSignalSourceBlocked[instanceIndex]) return false;
            for (int i = 0; i < _deferredRecordCount; i++)
            {
                if (_deferredSignalStaged[i] &&
                    _deferredRecords[i].TargetSourceBindingId == source)
                    return true;
            }
            return false;
        }

        internal bool TryGetSignalOrdering(
            GameplayTriggerSourceBindingId source,
            out GameplayTriggerSignalOrdering ordering)
        {
            int instanceIndex = FindInstance(source);
            if (instanceIndex < 0 ||
                _catalog.GetSource(_instances[instanceIndex].SourceIndex).SourceKind !=
                    GameplayTriggerSourceKind.Signal)
            {
                ordering = default;
                return false;
            }
            ordering = _catalog.GetSource(_instances[instanceIndex].SourceIndex).SignalOrdering;
            return true;
        }

        internal bool WasStagedSourceSeenEarlier(int recordIndex)
        {
            GameplayTriggerSourceBindingId source = _deferredRecords[recordIndex].TargetSourceBindingId;
            for (int i = 0; i < recordIndex; i++)
            {
                if (_deferredSignalStaged[i] &&
                    _deferredRecords[i].TargetSourceBindingId == source)
                    return true;
            }
            return false;
        }

        internal bool TryGetStagedSourceAt(
            int recordIndex,
            out GameplayTriggerSourceBindingId source,
            out GameplayTriggerSignalOrdering ordering)
        {
            source = default;
            ordering = default;
            if ((uint)recordIndex >= (uint)_deferredRecordCount ||
                !_deferredSignalStaged[recordIndex])
                return false;
            source = _deferredRecords[recordIndex].TargetSourceBindingId;
            int instanceIndex = FindInstance(source);
            if (instanceIndex < 0 || _deferredSignalSourceBlocked[instanceIndex]) return false;
            ordering = _catalog.GetSource(_instances[instanceIndex].SourceIndex).SignalOrdering;
            return true;
        }

        internal void CompleteDeferredSignalFinalization()
        {
            if (!_hasPendingDeferredSignalFinalization) return;
            for (int i = 0; i < _deferredRecordCount; i++)
                _deferredSignalStaged[i] = false;
            Array.Clear(_deferredSignalSourceBlocked, 0, _deferredSignalSourceBlocked.Length);
            _hasPendingDeferredSignalFinalization = false;
        }

        private GameplayTriggerErrorCode ValidateDeferredTarget(
            in GameplayTriggerDeferredRecord record,
            out int instanceIndex)
        {
            instanceIndex = FindInstance(record.TargetSourceBindingId);
            if (instanceIndex < 0 ||
                _instances[instanceIndex].InstanceId != record.TargetInstanceId)
                return GameplayTriggerErrorCode.DeferredStaleTarget;
            if (record.OperationKind == GameplayTriggerInternalDeferredOperationKind.Signal &&
                _catalog.GetSource(_instances[instanceIndex].SourceIndex).SourceKind !=
                    GameplayTriggerSourceKind.Signal)
                return GameplayTriggerErrorCode.DeferredSourceKindMismatch;
            return GameplayTriggerErrorCode.None;
        }

        private void ConsumeStagedSignals(GameplayTriggerSourceBindingId source, int count)
        {
            for (int i = 0; i < _deferredRecordCount && count > 0; i++)
            {
                if (!_deferredSignalStaged[i] ||
                    _deferredRecords[i].TargetSourceBindingId != source)
                    continue;
                _deferredRemoveScratch[i] = true;
                count--;
            }
            CompactDeferredRecords();
        }

        private void CompactDeferredRecords()
        {
            int write = 0;
            for (int read = 0; read < _deferredRecordCount; read++)
            {
                if (_deferredRemoveScratch[read]) continue;
                if (write != read)
                {
                    _deferredRecords[write] = _deferredRecords[read];
                    _deferredSignalStaged[write] = _deferredSignalStaged[read];
                }
                _deferredRemoveScratch[write] = false;
                write++;
            }
            for (int i = write; i < _deferredRecordCount; i++)
            {
                _deferredRecords[i] = default;
                _deferredSignalStaged[i] = false;
                _deferredRemoveScratch[i] = false;
            }
            _deferredRecordCount = write;
        }

        private void RecordDeferredFailureForSource(
            RuntimeFrame frame,
            GameplayTriggerSourceBindingId source,
            GameplayTriggerErrorCode errorCode)
        {
            for (int i = 0; i < _deferredRecordCount; i++)
            {
                if (!_deferredSignalStaged[i] ||
                    _deferredRecords[i].TargetSourceBindingId != source)
                    continue;
                RecordDeferredResult(
                    frame,
                    _deferredRecords[i],
                    new GameplayTriggerResult(
                        false,
                        errorCode));
                return;
            }
        }

        private void RecordDeferredResult(
            RuntimeFrame frame,
            in GameplayTriggerDeferredRecord record,
            GameplayTriggerResult result)
        {
            LastDeferredExecutionResult = new GameplayTriggerDeferredExecutionResult(
                frame,
                record.Sequence,
                record.TargetInstanceId,
                record.TargetSourceBindingId,
                record.ActivationId,
                record.ActionIndex,
                record.OperationKind,
                result);
            PublishDeferredResult(frame, in record, result);
        }

        private readonly struct DeferredReservation
        {
            internal DeferredReservation(
                bool active,
                GameplayTriggerActionSinkEntry entry,
                GameplayTriggerActionPreparation preparation,
                GameplayTriggerInstanceId targetInstanceId,
                GameplayTriggerSourceBindingId targetSourceBindingId)
            {
                Active = active;
                Entry = entry;
                Preparation = preparation;
                TargetInstanceId = targetInstanceId;
                TargetSourceBindingId = targetSourceBindingId;
            }

            internal bool Active { get; }
            internal GameplayTriggerActionSinkEntry Entry { get; }
            internal GameplayTriggerActionPreparation Preparation { get; }
            internal GameplayTriggerInstanceId TargetInstanceId { get; }
            internal GameplayTriggerSourceBindingId TargetSourceBindingId { get; }
        }
    }

    /// <summary>Applies due Trigger-owned lifecycle work and stages due Signal work.</summary>
    public sealed class GameplayTriggerDeferredSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.triggers.deferred";
        public const int DefaultPriority = 800;

        private readonly GameplayTriggerRuntime _runtime;

        /// <summary>Creates a deferred authority system for exactly one runtime.</summary>
        /// <remarks>LowFreqAlloc construction; Tick is NoAlloc.</remarks>
        public GameplayTriggerDeferredSystem(
            GameplayTriggerRuntime runtime,
            string systemId = DefaultSystemId,
            int priority = DefaultPriority)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            SystemId = systemId ?? string.Empty;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase => GameplaySystemPhase.PreCommand;
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;
        public GameplayTriggerResult LastResult { get; private set; }

        /// <remarks>NoAlloc. Internal exact-composition identity.</remarks>
        internal GameplayTriggerRuntime RuntimeIdentity => _runtime;

        public void SetEnabled(bool enabled) => IsEnabled = enabled;

        /// <remarks>NoAlloc after initialization. Does not inspect or drain the command buffer.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            LastResult = _runtime.StageDueDeferred(context.Frame);
        }
    }
}
