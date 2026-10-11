using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    public enum GameplayTriggerReliableTransferStatus
    {
        None = 0,
        Accepted = 1,
        TargetMissing = 2,
        TargetDisabled = 3,
        TargetCapacityExceeded = 4,
        TransientRejected = 5,
        TargetModuleUnavailable = 6
    }

    /// <summary>Numeric outcome of one reliable target transfer attempt.</summary>
    public readonly struct GameplayTriggerReliableTransferResult
    {
        public GameplayTriggerReliableTransferResult(
            GameplayTriggerReliableTransferStatus status,
            int detailCode = 0)
        {
            Status = status;
            DetailCode = detailCode;
        }

        public GameplayTriggerReliableTransferStatus Status { get; }
        public int DetailCode { get; }
        public bool Accepted => Status == GameplayTriggerReliableTransferStatus.Accepted;
    }

    /// <summary>One immutable Trigger-owned action awaiting reliable transfer.</summary>
    public readonly struct GameplayTriggerReliableOutboxRecord
    {
        internal GameplayTriggerReliableOutboxRecord(
            RuntimeFrame originalActivationFrame,
            RuntimeFrame originalDueFrame,
            long sequence,
            long activationId,
            int actionIndex,
            GameplayTriggerActionKindId actionKindId,
            GameplayTriggerBindingId originBindingId,
            GameplayTriggerRouteId originRouteId,
            GameplayTriggerInstanceId originInstanceId,
            GameplayTriggerOccurrenceKind occurrenceKind,
            int occurrenceSignalId,
            long previousScalarRaw,
            long currentScalarRaw,
            long occurrencePayload0,
            long occurrencePayload1,
            long occurrencePayload2,
            long occurrenceOrderingValue,
            long occurrencePresenceEpoch,
            long occurrencePresenceStartFrame,
            GameplayTriggerTargetRef resolvedTarget,
            GameplayEntityId owner,
            GameplayEntityId subject,
            GameplayEntityId instigator,
            long actionPayload0,
            long actionPayload1,
            long actionPayload2)
        {
            OriginalActivationFrame = originalActivationFrame;
            OriginalDueFrame = originalDueFrame;
            Sequence = sequence;
            ActivationId = activationId;
            ActionIndex = actionIndex;
            ActionKindId = actionKindId;
            OriginBindingId = originBindingId;
            OriginRouteId = originRouteId;
            OriginInstanceId = originInstanceId;
            OccurrenceKind = occurrenceKind;
            OccurrenceSignalId = occurrenceSignalId;
            PreviousScalarRaw = previousScalarRaw;
            CurrentScalarRaw = currentScalarRaw;
            OccurrencePayload0 = occurrencePayload0;
            OccurrencePayload1 = occurrencePayload1;
            OccurrencePayload2 = occurrencePayload2;
            OccurrenceOrderingValue = occurrenceOrderingValue;
            OccurrencePresenceEpoch = occurrencePresenceEpoch;
            OccurrencePresenceStartFrame = occurrencePresenceStartFrame;
            ResolvedTarget = resolvedTarget;
            Owner = owner;
            Subject = subject;
            Instigator = instigator;
            ActionPayload0 = actionPayload0;
            ActionPayload1 = actionPayload1;
            ActionPayload2 = actionPayload2;
        }

        public RuntimeFrame OriginalActivationFrame { get; }
        public RuntimeFrame OriginalDueFrame { get; }
        public long Sequence { get; }
        public long ActivationId { get; }
        public int ActionIndex { get; }
        public GameplayTriggerActionKindId ActionKindId { get; }
        public GameplayTriggerBindingId OriginBindingId { get; }
        public GameplayTriggerRouteId OriginRouteId { get; }
        public GameplayTriggerInstanceId OriginInstanceId { get; }
        public GameplayTriggerOccurrenceKind OccurrenceKind { get; }
        public int OccurrenceSignalId { get; }
        public long PreviousScalarRaw { get; }
        public long CurrentScalarRaw { get; }
        public long OccurrencePayload0 { get; }
        public long OccurrencePayload1 { get; }
        public long OccurrencePayload2 { get; }
        public long OccurrenceOrderingValue { get; }
        public long OccurrencePresenceEpoch { get; }
        public long OccurrencePresenceStartFrame { get; }
        public GameplayTriggerTargetRef ResolvedTarget { get; }
        public GameplayEntityId Owner { get; }
        public GameplayEntityId Subject { get; }
        public GameplayEntityId Instigator { get; }
        public long ActionPayload0 { get; }
        public long ActionPayload1 { get; }
        public long ActionPayload2 { get; }
    }

    /// <summary>Numeric result for the last reliable outbox record considered.</summary>
    public readonly struct GameplayTriggerReliableOutboxExecutionResult
    {
        internal GameplayTriggerReliableOutboxExecutionResult(
            RuntimeFrame attemptFrame,
            in GameplayTriggerReliableOutboxRecord record,
            GameplayTriggerReliableTransferResult transferResult)
        {
            AttemptFrame = attemptFrame;
            Sequence = record.Sequence;
            ActivationId = record.ActivationId;
            ActionIndex = record.ActionIndex;
            ActionKindId = record.ActionKindId;
            Target = record.ResolvedTarget;
            TransferResult = transferResult;
        }

        public RuntimeFrame AttemptFrame { get; }
        public long Sequence { get; }
        public long ActivationId { get; }
        public int ActionIndex { get; }
        public GameplayTriggerActionKindId ActionKindId { get; }
        public GameplayTriggerTargetRef Target { get; }
        public GameplayTriggerReliableTransferResult TransferResult { get; }
    }

    /// <summary>Target-specific synchronous transfer boundary for one reliable action kind.</summary>
    public interface IGameplayTriggerReliableTargetTransfer
    {
        GameplayTriggerActionKindId ActionKindId { get; }
        string TargetModuleId { get; }
        bool IsEnabled { get; }

        /// <remarks>NoAlloc after initialization.</remarks>
        GameplayTriggerReliableTransferResult TryTransfer(
            RuntimeFrame currentFrame,
            in GameplayTriggerReliableOutboxRecord record);
    }

    public sealed partial class GameplayTriggerRuntime
    {
        private GameplayTriggerReliableOutboxRecord[] _reliableOutboxRecords;
        private ReliableOutboxReservation[] _reliableOutboxReservations;
        private bool[] _reliableOutboxRemoveScratch;
        private int _reliableOutboxRecordCount;
        private int _reliableOutboxReservationCount;
        private long _nextReliableOutboxSequence = 1L;
        private bool _reliableTransferInFlight;
        private bool _reliableOutboxModuleBound;

        /// <remarks>NoAlloc. Records remain in canonical due-frame and sequence order.</remarks>
        public int ReliableOutboxRecordCount => _reliableOutboxRecordCount;

        /// <remarks>NoAlloc.</remarks>
        public long NextReliableOutboxSequence => _nextReliableOutboxSequence;

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerReliableOutboxExecutionResult LastReliableOutboxExecutionResult { get; private set; }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerReliableOutboxRecord GetReliableOutboxRecord(int index) =>
            _reliableOutboxRecords[index];

        internal bool ReliableTransferInFlight => _reliableTransferInFlight;
        internal int ReliableOutboxReservationCount => _reliableOutboxReservationCount;

        internal void BindReliableOutboxModule()
        {
            if (_reliableOutboxModuleBound)
                throw new InvalidOperationException(
                    "A GameplayTriggerRuntime can be bound to only one reliable outbox module.");
            _reliableOutboxModuleBound = true;
        }

        private void InitializeReliableOutboxAuthority()
        {
            if (_catalog.ReliableDeferredActionCount > 0 &&
                _options.MaxReliableOutboxRecords == 0)
            {
                throw new ArgumentException(
                    "A catalog with reliable deferred actions requires MaxReliableOutboxRecords greater than zero.",
                    nameof(_options));
            }

            _reliableOutboxRecords =
                new GameplayTriggerReliableOutboxRecord[_options.MaxReliableOutboxRecords];
            _reliableOutboxReservations =
                new ReliableOutboxReservation[_options.MaxReliableOutboxRecords];
            _reliableOutboxRemoveScratch = new bool[_options.MaxReliableOutboxRecords];
        }

        private bool TryPrepareReliableOutbox(
            GameplayTriggerActionSinkEntry entry,
            in GameplayTriggerActionPreparation preparation,
            out int token,
            out GameplayTriggerErrorCode errorCode)
        {
            token = 0;
            if (entry.Sink is IGameplayTriggerReliableActionPreparationValidator validator &&
                !validator.TryValidateReliableAction(in preparation))
            {
                errorCode = GameplayTriggerErrorCode.ReservationRejected;
                return false;
            }
            if (preparation.Frame.Value == long.MaxValue)
            {
                errorCode = GameplayTriggerErrorCode.ReliableOutboxFrameOverflow;
                return false;
            }
            if (_reliableOutboxRecordCount + _reliableOutboxReservationCount >=
                _reliableOutboxRecords.Length)
            {
                errorCode = GameplayTriggerErrorCode.ReliableOutboxCapacityExceeded;
                return false;
            }
            if (_nextReliableOutboxSequence <= 0L ||
                _nextReliableOutboxSequence == long.MaxValue ||
                _reliableOutboxReservationCount >=
                    long.MaxValue - _nextReliableOutboxSequence)
            {
                errorCode = GameplayTriggerErrorCode.ReliableOutboxSequenceExhausted;
                return false;
            }
            if (!preparation.Target.IsValid)
            {
                errorCode = GameplayTriggerErrorCode.MissingActionTarget;
                return false;
            }

            for (int i = 0; i < _reliableOutboxReservations.Length; i++)
            {
                if (_reliableOutboxReservations[i].Active) continue;
                _reliableOutboxReservations[i] =
                    new ReliableOutboxReservation(true, entry.ActionKindId, preparation);
                _reliableOutboxReservationCount++;
                token = i + 1;
                errorCode = GameplayTriggerErrorCode.None;
                return true;
            }

            errorCode = GameplayTriggerErrorCode.ReliableOutboxCapacityExceeded;
            return false;
        }

        private void AbortReliableOutbox(int token)
        {
            int index = token - 1;
            if ((uint)index >= (uint)_reliableOutboxReservations.Length ||
                !_reliableOutboxReservations[index].Active)
                return;
            _reliableOutboxReservations[index] = default;
            _reliableOutboxReservationCount--;
        }

        private void CommitReliableOutbox(
            int token,
            long activationId,
            in GameplayTriggerActionIntent intent)
        {
            int index = token - 1;
            if ((uint)index >= (uint)_reliableOutboxReservations.Length ||
                !_reliableOutboxReservations[index].Active)
                throw new InvalidOperationException("Reliable outbox reservation token is not active.");

            ReliableOutboxReservation reservation = _reliableOutboxReservations[index];
            GameplayTriggerActionPreparation preparation = reservation.Preparation;
            GameplayTriggerOccurrence occurrence = preparation.Occurrence;
            GameplayTriggerActionInstruction instruction = preparation.Instruction;
            _reliableOutboxRecords[_reliableOutboxRecordCount++] =
                new GameplayTriggerReliableOutboxRecord(
                    preparation.Frame,
                    new RuntimeFrame(preparation.Frame.Value + 1L),
                    _nextReliableOutboxSequence++,
                    activationId,
                    preparation.ActionIndex,
                    reservation.ActionKindId,
                    preparation.BindingId,
                    preparation.RouteId,
                    occurrence.InstanceId,
                    occurrence.Kind,
                    occurrence.SignalId,
                    occurrence.PreviousScalar.RawValue,
                    occurrence.CurrentScalar.RawValue,
                    occurrence.Payload0,
                    occurrence.Payload1,
                    occurrence.Payload2,
                    occurrence.OrderingValue,
                    occurrence.PresenceEpoch,
                    occurrence.PresenceStartFrame,
                    preparation.Target,
                    preparation.Owner,
                    occurrence.Subject,
                    occurrence.Instigator,
                    instruction.Payload0,
                    instruction.Payload1,
                    instruction.Payload2);
            _reliableOutboxReservations[index] = default;
            _reliableOutboxReservationCount--;
        }

        internal GameplayTriggerReliableTransferResult InvokeReliableTransfer(
            RuntimeFrame frame,
            in GameplayTriggerReliableOutboxRecord record,
            IGameplayTriggerReliableTargetTransfer transfer)
        {
            _reliableTransferInFlight = true;
            try
            {
                return transfer.TryTransfer(frame, in record);
            }
            finally
            {
                _reliableTransferInFlight = false;
            }
        }

        internal void RecordReliableExecution(
            RuntimeFrame frame,
            in GameplayTriggerReliableOutboxRecord record,
            GameplayTriggerReliableTransferResult result)
        {
            LastReliableOutboxExecutionResult =
                new GameplayTriggerReliableOutboxExecutionResult(frame, in record, result);
            PublishReliableResult(frame, in record, result);
        }

        internal void BeginReliableOutboxScan()
        {
            Array.Clear(_reliableOutboxRemoveScratch, 0, _reliableOutboxRecordCount);
        }

        internal void MarkReliableOutboxAccepted(int index)
        {
            _reliableOutboxRemoveScratch[index] = true;
        }

        internal void CompleteReliableOutboxScan()
        {
            int write = 0;
            for (int read = 0; read < _reliableOutboxRecordCount; read++)
            {
                if (_reliableOutboxRemoveScratch[read]) continue;
                if (write != read) _reliableOutboxRecords[write] = _reliableOutboxRecords[read];
                write++;
            }
            for (int i = write; i < _reliableOutboxRecordCount; i++)
                _reliableOutboxRecords[i] = default;
            _reliableOutboxRecordCount = write;
        }

        private readonly struct ReliableOutboxReservation
        {
            public ReliableOutboxReservation(
                bool active,
                GameplayTriggerActionKindId actionKindId,
                GameplayTriggerActionPreparation preparation)
            {
                Active = active;
                ActionKindId = actionKindId;
                Preparation = preparation;
            }

            public bool Active { get; }
            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerActionPreparation Preparation { get; }
        }
    }
}
