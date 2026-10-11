using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    public enum GameplayTriggerRuntimeEventKind
    {
        None = 0,
        Occurrence = 1,
        ActivationAccepted = 2,
        ActionDispatched = 3,
        RuntimeDiagnostic = 4,
        CommandResult = 5,
        InternalDeferredResult = 6,
        ReliableTransferResult = 7,
        ActionResult = 8
    }

    public enum GameplayTriggerRuntimeEventStatus
    {
        None = 0,
        Observed = 1,
        Accepted = 2,
        Dispatched = 3,
        Succeeded = 4,
        Rejected = 5,
        Retained = 6
    }

    public enum GameplayTriggerRuntimeEventDropReason
    {
        None = 0,
        SourceCapacityExceeded = 1,
        SourceSequenceExhausted = 2,
        ObserverCapacityExceeded = 3,
        ObserverDisabled = 4,
        InvalidActionResult = 5
    }

    /// <summary>Fixed numeric presentation payload emitted by Gameplay Trigger producers.</summary>
    public readonly struct GameplayTriggerRuntimeEvent
    {
        internal GameplayTriggerRuntimeEvent(in GameplayTriggerRuntimeEventData data)
        {
            Frame = data.Frame;
            SourceSequence = data.SourceSequence;
            Kind = data.Kind;
            Status = data.Status;
            DefinitionId = data.DefinitionId;
            BindingId = data.BindingId;
            SourceBindingId = data.SourceBindingId;
            InstanceId = data.InstanceId;
            RouteId = data.RouteId;
            ActivationId = data.ActivationId;
            ActionIndex = data.ActionIndex;
            ActionKindId = data.ActionKindId;
            Target = data.Target;
            Owner = data.Owner;
            Subject = data.Subject;
            Instigator = data.Instigator;
            OccurrenceKind = data.OccurrenceKind;
            SignalId = data.SignalId;
            PreviousScalarRaw = data.PreviousScalarRaw;
            CurrentScalarRaw = data.CurrentScalarRaw;
            OccurrencePayload0 = data.OccurrencePayload0;
            OccurrencePayload1 = data.OccurrencePayload1;
            OccurrencePayload2 = data.OccurrencePayload2;
            OrderingValue = data.OrderingValue;
            PresenceEpoch = data.PresenceEpoch;
            PresenceStartFrame = data.PresenceStartFrame;
            ActionPayload0 = data.ActionPayload0;
            ActionPayload1 = data.ActionPayload1;
            ActionPayload2 = data.ActionPayload2;
            OriginFrame = data.OriginFrame;
            DueFrame = data.DueFrame;
            ProducerSequence = data.ProducerSequence;
            TargetInstanceId = data.TargetInstanceId;
            TargetSourceBindingId = data.TargetSourceBindingId;
            InternalDeferredOperationKind = data.InternalDeferredOperationKind;
            ReliableTransferStatus = data.ReliableTransferStatus;
            RuntimeCommandKind = data.RuntimeCommandKind;
            CommandSourceId = data.CommandSourceId;
            CommandId = data.CommandId;
            CommandTargetId = data.CommandTargetId;
            CommandPayload0 = data.CommandPayload0;
            CommandPayload1 = data.CommandPayload1;
            CommandPayload2 = data.CommandPayload2;
            Provenance = data.Provenance;
            BatchId = data.BatchId;
            ReasonCode = data.ReasonCode;
            Detail0 = data.Detail0;
            Detail1 = data.Detail1;
            ResultPayload0 = data.ResultPayload0;
            ResultPayload1 = data.ResultPayload1;
            ResultPayload2 = data.ResultPayload2;
        }

        public RuntimeFrame Frame { get; }
        public long SourceSequence { get; }
        public GameplayTriggerRuntimeEventKind Kind { get; }
        public GameplayTriggerRuntimeEventStatus Status { get; }
        public GameplayTriggerDefinitionId DefinitionId { get; }
        public GameplayTriggerBindingId BindingId { get; }
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        public GameplayTriggerInstanceId InstanceId { get; }
        public GameplayTriggerRouteId RouteId { get; }
        public long ActivationId { get; }
        public int ActionIndex { get; }
        public GameplayTriggerActionKindId ActionKindId { get; }
        public GameplayTriggerTargetRef Target { get; }
        public GameplayEntityId Owner { get; }
        public GameplayEntityId Subject { get; }
        public GameplayEntityId Instigator { get; }
        public GameplayTriggerOccurrenceKind OccurrenceKind { get; }
        public int SignalId { get; }
        public long PreviousScalarRaw { get; }
        public long CurrentScalarRaw { get; }
        public long OccurrencePayload0 { get; }
        public long OccurrencePayload1 { get; }
        public long OccurrencePayload2 { get; }
        public long OrderingValue { get; }
        public long PresenceEpoch { get; }
        public long PresenceStartFrame { get; }
        public long ActionPayload0 { get; }
        public long ActionPayload1 { get; }
        public long ActionPayload2 { get; }
        public RuntimeFrame OriginFrame { get; }
        public RuntimeFrame DueFrame { get; }
        public long ProducerSequence { get; }
        public GameplayTriggerInstanceId TargetInstanceId { get; }
        public GameplayTriggerSourceBindingId TargetSourceBindingId { get; }
        public GameplayTriggerInternalDeferredOperationKind InternalDeferredOperationKind { get; }
        public GameplayTriggerReliableTransferStatus ReliableTransferStatus { get; }
        public GameplayTriggerRuntimeCommandKind RuntimeCommandKind { get; }
        public int CommandSourceId { get; }
        public int CommandId { get; }
        public int CommandTargetId { get; }
        public int CommandPayload0 { get; }
        public int CommandPayload1 { get; }
        public int CommandPayload2 { get; }
        public RuntimeCommandProvenanceKind Provenance { get; }
        public long BatchId { get; }
        public int ReasonCode { get; }
        public int Detail0 { get; }
        public int Detail1 { get; }
        public long ResultPayload0 { get; }
        public long ResultPayload1 { get; }
        public long ResultPayload2 { get; }

        internal GameplayTriggerRuntimeEvent WithSourceSequence(long sourceSequence)
        {
            GameplayTriggerRuntimeEventData data = GameplayTriggerRuntimeEventData.FromEvent(in this);
            data.SourceSequence = sourceSequence;
            return new GameplayTriggerRuntimeEvent(in data);
        }
    }

    internal struct GameplayTriggerRuntimeEventData
    {
        internal RuntimeFrame Frame;
        internal long SourceSequence;
        internal GameplayTriggerRuntimeEventKind Kind;
        internal GameplayTriggerRuntimeEventStatus Status;
        internal GameplayTriggerDefinitionId DefinitionId;
        internal GameplayTriggerBindingId BindingId;
        internal GameplayTriggerSourceBindingId SourceBindingId;
        internal GameplayTriggerInstanceId InstanceId;
        internal GameplayTriggerRouteId RouteId;
        internal long ActivationId;
        internal int ActionIndex;
        internal GameplayTriggerActionKindId ActionKindId;
        internal GameplayTriggerTargetRef Target;
        internal GameplayEntityId Owner;
        internal GameplayEntityId Subject;
        internal GameplayEntityId Instigator;
        internal GameplayTriggerOccurrenceKind OccurrenceKind;
        internal int SignalId;
        internal long PreviousScalarRaw;
        internal long CurrentScalarRaw;
        internal long OccurrencePayload0;
        internal long OccurrencePayload1;
        internal long OccurrencePayload2;
        internal long OrderingValue;
        internal long PresenceEpoch;
        internal long PresenceStartFrame;
        internal long ActionPayload0;
        internal long ActionPayload1;
        internal long ActionPayload2;
        internal RuntimeFrame OriginFrame;
        internal RuntimeFrame DueFrame;
        internal long ProducerSequence;
        internal GameplayTriggerInstanceId TargetInstanceId;
        internal GameplayTriggerSourceBindingId TargetSourceBindingId;
        internal GameplayTriggerInternalDeferredOperationKind InternalDeferredOperationKind;
        internal GameplayTriggerReliableTransferStatus ReliableTransferStatus;
        internal GameplayTriggerRuntimeCommandKind RuntimeCommandKind;
        internal int CommandSourceId;
        internal int CommandId;
        internal int CommandTargetId;
        internal int CommandPayload0;
        internal int CommandPayload1;
        internal int CommandPayload2;
        internal RuntimeCommandProvenanceKind Provenance;
        internal long BatchId;
        internal int ReasonCode;
        internal int Detail0;
        internal int Detail1;
        internal long ResultPayload0;
        internal long ResultPayload1;
        internal long ResultPayload2;

        internal static GameplayTriggerRuntimeEventData Create(
            RuntimeFrame frame,
            GameplayTriggerRuntimeEventKind kind,
            GameplayTriggerRuntimeEventStatus status)
        {
            return new GameplayTriggerRuntimeEventData
            {
                Frame = frame,
                Kind = kind,
                Status = status,
                ActionIndex = -1
            };
        }

        internal static GameplayTriggerRuntimeEventData FromEvent(
            in GameplayTriggerRuntimeEvent evt)
        {
            return new GameplayTriggerRuntimeEventData
            {
                Frame = evt.Frame,
                SourceSequence = evt.SourceSequence,
                Kind = evt.Kind,
                Status = evt.Status,
                DefinitionId = evt.DefinitionId,
                BindingId = evt.BindingId,
                SourceBindingId = evt.SourceBindingId,
                InstanceId = evt.InstanceId,
                RouteId = evt.RouteId,
                ActivationId = evt.ActivationId,
                ActionIndex = evt.ActionIndex,
                ActionKindId = evt.ActionKindId,
                Target = evt.Target,
                Owner = evt.Owner,
                Subject = evt.Subject,
                Instigator = evt.Instigator,
                OccurrenceKind = evt.OccurrenceKind,
                SignalId = evt.SignalId,
                PreviousScalarRaw = evt.PreviousScalarRaw,
                CurrentScalarRaw = evt.CurrentScalarRaw,
                OccurrencePayload0 = evt.OccurrencePayload0,
                OccurrencePayload1 = evt.OccurrencePayload1,
                OccurrencePayload2 = evt.OccurrencePayload2,
                OrderingValue = evt.OrderingValue,
                PresenceEpoch = evt.PresenceEpoch,
                PresenceStartFrame = evt.PresenceStartFrame,
                ActionPayload0 = evt.ActionPayload0,
                ActionPayload1 = evt.ActionPayload1,
                ActionPayload2 = evt.ActionPayload2,
                OriginFrame = evt.OriginFrame,
                DueFrame = evt.DueFrame,
                ProducerSequence = evt.ProducerSequence,
                TargetInstanceId = evt.TargetInstanceId,
                TargetSourceBindingId = evt.TargetSourceBindingId,
                InternalDeferredOperationKind = evt.InternalDeferredOperationKind,
                ReliableTransferStatus = evt.ReliableTransferStatus,
                RuntimeCommandKind = evt.RuntimeCommandKind,
                CommandSourceId = evt.CommandSourceId,
                CommandId = evt.CommandId,
                CommandTargetId = evt.CommandTargetId,
                CommandPayload0 = evt.CommandPayload0,
                CommandPayload1 = evt.CommandPayload1,
                CommandPayload2 = evt.CommandPayload2,
                Provenance = evt.Provenance,
                BatchId = evt.BatchId,
                ReasonCode = evt.ReasonCode,
                Detail0 = evt.Detail0,
                Detail1 = evt.Detail1,
                ResultPayload0 = evt.ResultPayload0,
                ResultPayload1 = evt.ResultPayload1,
                ResultPayload2 = evt.ResultPayload2
            };
        }
    }

    /// <summary>Fixed-capacity source with one producer owner and one destructive Router owner.</summary>
    public sealed class GameplayTriggerRuntimeEventSource
    {
        private readonly GameplayTriggerRuntimeEvent[] _events;
        private GameplayTriggerRuntime _producerOwner;
        private bool _drainOwnerBound;
        private int _count;
        private long _nextSequence;
        private bool _sequenceExhausted;
        private long _droppedCount;

        /// <remarks>AllocByDesign. Capacity zero is valid.</remarks>
        public GameplayTriggerRuntimeEventSource(int capacity)
            : this(capacity, 1L, false, 0L)
        {
        }

        internal GameplayTriggerRuntimeEventSource(
            int capacity,
            long nextSequence,
            bool sequenceExhausted,
            long droppedCount)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (nextSequence <= 0L) throw new ArgumentOutOfRangeException(nameof(nextSequence));
            if (sequenceExhausted && nextSequence != long.MaxValue)
                throw new ArgumentException("An exhausted source must keep the long.MaxValue sentinel.", nameof(nextSequence));
            if (droppedCount < 0L) throw new ArgumentOutOfRangeException(nameof(droppedCount));
            _events = new GameplayTriggerRuntimeEvent[capacity];
            _nextSequence = nextSequence;
            _sequenceExhausted = sequenceExhausted;
            _droppedCount = droppedCount;
        }

        public int Capacity => _events.Length;
        public int PendingCount => _count;
        public long DroppedCount => _droppedCount;
        public GameplayTriggerRuntimeEventDropReason LastDropReason { get; private set; }
        public int HighWaterMark { get; private set; }
        public long NextSequence => _nextSequence;
        public bool IsSequenceExhausted => _sequenceExhausted;

        /// <summary>Publishes one future target-owned numeric action result.</summary>
        /// <remarks>NoAlloc after initialization. Invalid values fail closed without throwing.</remarks>
        public bool TryPublishActionResult(
            RuntimeFrame frame,
            long activationId,
            int actionIndex,
            GameplayTriggerActionKindId actionKindId,
            GameplayTriggerTargetRef target,
            GameplayTriggerRuntimeEventStatus status,
            int reasonCode = 0,
            long resultPayload0 = 0L,
            long resultPayload1 = 0L,
            long resultPayload2 = 0L)
        {
            if (_producerOwner == null ||
                activationId <= 0L ||
                actionIndex < 0 ||
                !actionKindId.IsValid ||
                !target.IsValid ||
                (status != GameplayTriggerRuntimeEventStatus.Succeeded &&
                 status != GameplayTriggerRuntimeEventStatus.Rejected))
            {
                RecordDrop(GameplayTriggerRuntimeEventDropReason.InvalidActionResult);
                return false;
            }

            GameplayTriggerRuntimeEventData data = GameplayTriggerRuntimeEventData.Create(
                frame,
                GameplayTriggerRuntimeEventKind.ActionResult,
                status);
            data.OriginFrame = frame;
            data.ActivationId = activationId;
            data.ActionIndex = actionIndex;
            data.ActionKindId = actionKindId;
            data.Target = target;
            data.ReasonCode = reasonCode;
            data.ResultPayload0 = resultPayload0;
            data.ResultPayload1 = resultPayload1;
            data.ResultPayload2 = resultPayload2;
            var evt = new GameplayTriggerRuntimeEvent(in data);
            return TryPublishInternal(in evt);
        }

        internal bool IsProducerOwnerBound => _producerOwner != null;
        internal bool IsDrainOwnerBound => _drainOwnerBound;

        internal void ValidateProducerUnbound()
        {
            if (_producerOwner != null)
                throw new InvalidOperationException(
                    "A Gameplay Trigger event source can have only one runtime producer owner.");
        }

        internal void ValidateProducerBound()
        {
            if (_producerOwner == null)
                throw new InvalidOperationException(
                    "A Gameplay Trigger event source must be bound to its runtime producer before it can be routed.");
        }

        internal void BindProducer(GameplayTriggerRuntime runtime)
        {
            _producerOwner = runtime;
        }

        internal void ValidateDrainUnbound()
        {
            if (_drainOwnerBound)
                throw new InvalidOperationException(
                    "A Gameplay Trigger event source can have only one Router drain owner.");
        }

        internal void BindDrainOwner()
        {
            _drainOwnerBound = true;
        }

        internal bool TryPublishInternal(in GameplayTriggerRuntimeEvent evt)
        {
            if (_sequenceExhausted)
            {
                RecordDrop(GameplayTriggerRuntimeEventDropReason.SourceSequenceExhausted);
                return false;
            }
            if (_count >= _events.Length)
            {
                RecordDrop(GameplayTriggerRuntimeEventDropReason.SourceCapacityExceeded);
                return false;
            }

            long sequence = _nextSequence;
            GameplayTriggerRuntimeEvent accepted = evt.WithSourceSequence(sequence);
            int insert = _count;
            while (insert > 0 && Compare(in accepted, in _events[insert - 1]) < 0)
            {
                _events[insert] = _events[insert - 1];
                insert--;
            }
            _events[insert] = accepted;
            _count++;
            if (_count > HighWaterMark) HighWaterMark = _count;
            if (sequence == long.MaxValue)
            {
                _sequenceExhausted = true;
                _nextSequence = long.MaxValue;
            }
            else
            {
                _nextSequence = sequence + 1L;
            }
            return true;
        }

        internal GameplayTriggerRuntimeEvent GetPending(int index) => _events[index];

        internal int GetEligibleCount(RuntimeFrame frame)
        {
            int eligible = 0;
            while (eligible < _count && _events[eligible].Frame <= frame) eligible++;
            return eligible;
        }

        internal void ConsumePrefix(int count)
        {
            int remaining = _count - count;
            for (int i = 0; i < remaining; i++) _events[i] = _events[count + i];
            Array.Clear(_events, remaining, count);
            _count = remaining;
        }

        internal void ResetPresentationState()
        {
            Array.Clear(_events, 0, _count);
            _count = 0;
            _nextSequence = 1L;
            _sequenceExhausted = false;
            _droppedCount = 0L;
            LastDropReason = GameplayTriggerRuntimeEventDropReason.None;
            HighWaterMark = 0;
        }

        private void RecordDrop(GameplayTriggerRuntimeEventDropReason reason)
        {
            if (_droppedCount < long.MaxValue) _droppedCount++;
            LastDropReason = reason;
        }

        private static int Compare(
            in GameplayTriggerRuntimeEvent left,
            in GameplayTriggerRuntimeEvent right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            return frame != 0 ? frame : left.SourceSequence.CompareTo(right.SourceSequence);
        }
    }

    /// <summary>One observer-owned fixed-capacity presentation buffer.</summary>
    public sealed class GameplayTriggerRuntimeEventObserverBuffer
    {
        private readonly GameplayTriggerRuntimeEvent[] _events;
        private int _head;
        private int _count;
        private long _droppedCount;
        private bool _routerBound;

        /// <remarks>AllocByDesign. Capacity zero is valid.</remarks>
        public GameplayTriggerRuntimeEventObserverBuffer(int capacity)
            : this(capacity, 0L)
        {
        }

        internal GameplayTriggerRuntimeEventObserverBuffer(int capacity, long droppedCount)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (droppedCount < 0L) throw new ArgumentOutOfRangeException(nameof(droppedCount));
            _events = new GameplayTriggerRuntimeEvent[capacity];
            _droppedCount = droppedCount;
        }

        public int Capacity => _events.Length;
        public int Count => _count;
        public long DroppedCount => _droppedCount;
        public GameplayTriggerRuntimeEventDropReason LastDropReason { get; private set; }
        public int HighWaterMark { get; private set; }
        public bool IsEnabled { get; private set; } = true;

        /// <remarks>NoAlloc.</remarks>
        public void SetEnabled(bool enabled) => IsEnabled = enabled;

        /// <remarks>NoAlloc.</remarks>
        public bool TryDequeue(out GameplayTriggerRuntimeEvent evt)
        {
            if (_count == 0)
            {
                evt = default;
                return false;
            }
            evt = _events[_head];
            _events[_head] = default;
            _head++;
            if (_head == _events.Length) _head = 0;
            _count--;
            return true;
        }

        /// <summary>Clears pending records while preserving diagnostics and enabled state.</summary>
        /// <remarks>NoAlloc.</remarks>
        public void Clear()
        {
            Array.Clear(_events, 0, _events.Length);
            _head = 0;
            _count = 0;
        }

        internal bool IsRouterBound => _routerBound;

        internal void ValidateRouterUnbound()
        {
            if (_routerBound)
                throw new InvalidOperationException(
                    "A Gameplay Trigger observer buffer can be bound to only one Router.");
        }

        internal void BindRouter()
        {
            _routerBound = true;
        }

        internal void EnqueueFromRouter(in GameplayTriggerRuntimeEvent evt)
        {
            if (!IsEnabled)
            {
                RecordDrop(GameplayTriggerRuntimeEventDropReason.ObserverDisabled);
                return;
            }
            if (_count >= _events.Length)
            {
                RecordDrop(GameplayTriggerRuntimeEventDropReason.ObserverCapacityExceeded);
                return;
            }

            int tail = _head + _count;
            if (tail >= _events.Length) tail -= _events.Length;
            _events[tail] = evt;
            _count++;
            if (_count > HighWaterMark) HighWaterMark = _count;
        }

        internal void ResetPresentationState()
        {
            Clear();
            _droppedCount = 0L;
            LastDropReason = GameplayTriggerRuntimeEventDropReason.None;
            HighWaterMark = 0;
        }

        private void RecordDrop(GameplayTriggerRuntimeEventDropReason reason)
        {
            if (_droppedCount < long.MaxValue) _droppedCount++;
            LastDropReason = reason;
        }
    }

    public readonly struct GameplayTriggerRuntimeEventObserverRegistration
    {
        public GameplayTriggerRuntimeEventObserverRegistration(
            string observerId,
            GameplayTriggerRuntimeEventObserverBuffer buffer)
        {
            ObserverId = observerId;
            Buffer = buffer;
        }

        public string ObserverId { get; }
        public GameplayTriggerRuntimeEventObserverBuffer Buffer { get; }
    }

    public sealed partial class GameplayTriggerRuntime
    {
        private void PublishCanonicalOccurrences()
        {
            if (_eventSource == null) return;
            for (int i = 0; i < _occurrenceCount; i++)
            {
                GameplayTriggerOccurrence occurrence = _occurrences[i];
                GameplayTriggerBinding binding = GetBindingForInstance(occurrence.InstanceId);
                GameplayTriggerRuntimeEventData data = GameplayTriggerRuntimeEventData.Create(
                    occurrence.Frame,
                    GameplayTriggerRuntimeEventKind.Occurrence,
                    GameplayTriggerRuntimeEventStatus.Observed);
                PopulateOrigin(ref data, in binding, occurrence.InstanceId);
                PopulateOccurrence(ref data, in occurrence);
                data.OriginFrame = occurrence.Frame;
                PublishInternal(ref data);
            }
        }

        private void PublishAcceptedActivation(
            in GameplayTriggerActivation activation,
            in GameplayTriggerBinding binding)
        {
            if (_eventSource == null) return;
            GameplayTriggerOccurrence occurrence = activation.Occurrence;
            GameplayTriggerRuntimeEventData data = GameplayTriggerRuntimeEventData.Create(
                occurrence.Frame,
                GameplayTriggerRuntimeEventKind.ActivationAccepted,
                GameplayTriggerRuntimeEventStatus.Accepted);
            PopulateOrigin(ref data, in binding, occurrence.InstanceId);
            PopulateOccurrence(ref data, in occurrence);
            data.ActivationId = activation.ActivationId;
            data.RouteId = activation.RouteId;
            data.OriginFrame = occurrence.Frame;
            PublishInternal(ref data);
        }

        private void PublishActionDispatched(
            in GameplayTriggerActionIntent intent,
            GameplayTriggerDefinitionId definitionId,
            GameplayTriggerSourceBindingId sourceBindingId)
        {
            if (_eventSource == null) return;
            GameplayTriggerOccurrence occurrence = intent.Occurrence;
            GameplayTriggerRuntimeEventData data = GameplayTriggerRuntimeEventData.Create(
                intent.Frame,
                GameplayTriggerRuntimeEventKind.ActionDispatched,
                GameplayTriggerRuntimeEventStatus.Dispatched);
            data.DefinitionId = definitionId;
            data.BindingId = intent.BindingId;
            data.SourceBindingId = sourceBindingId;
            data.InstanceId = occurrence.InstanceId;
            data.RouteId = intent.RouteId;
            data.ActivationId = intent.ActivationId;
            data.ActionIndex = intent.ActionIndex;
            data.ActionKindId = intent.Instruction.ActionKindId;
            data.Target = intent.Target;
            data.Owner = intent.Owner;
            PopulateOccurrence(ref data, in occurrence);
            data.ActionPayload0 = intent.Instruction.Payload0;
            data.ActionPayload1 = intent.Instruction.Payload1;
            data.ActionPayload2 = intent.Instruction.Payload2;
            data.OriginFrame = occurrence.Frame;
            PublishInternal(ref data);
        }

        private void PublishRuntimeDiagnostic(
            RuntimeFrame frame,
            GameplayTriggerErrorCode code,
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerRouteId routeId,
            int detail)
        {
            if (_eventSource == null) return;
            GameplayTriggerRuntimeEventData data = GameplayTriggerRuntimeEventData.Create(
                frame,
                GameplayTriggerRuntimeEventKind.RuntimeDiagnostic,
                GameplayTriggerRuntimeEventStatus.Rejected);
            data.SourceBindingId = sourceBindingId;
            data.InstanceId = instanceId;
            if (TryGetBindingForInstance(instanceId, out GameplayTriggerBinding binding) &&
                (!sourceBindingId.IsValid || sourceBindingId == binding.SourceBindingId))
            {
                PopulateOrigin(ref data, in binding, instanceId);
            }
            data.RouteId = routeId;
            data.OriginFrame = frame;
            data.ReasonCode = (int)code;
            data.Detail0 = detail;
            PublishInternal(ref data);
        }

        internal void PublishCommandResult(
            in GameplayTriggerCommandExecutionResult executionResult,
            in RuntimeCommand command)
        {
            if (_eventSource == null) return;
            GameplayTriggerResult result = executionResult.Result;
            GameplayTriggerRuntimeEventData data = GameplayTriggerRuntimeEventData.Create(
                executionResult.Frame,
                GameplayTriggerRuntimeEventKind.CommandResult,
                result.Success
                    ? GameplayTriggerRuntimeEventStatus.Succeeded
                    : GameplayTriggerRuntimeEventStatus.Rejected);
            data.InstanceId = executionResult.InstanceId;
            data.SourceBindingId = executionResult.SourceBindingId;
            GameplayTriggerInstanceId resolvedInstanceId = default;
            if (TryGetBindingForInstance(
                    executionResult.InstanceId,
                    out GameplayTriggerBinding binding) ||
                TryGetBindingForSource(
                    executionResult.SourceBindingId,
                    out binding,
                    out resolvedInstanceId))
            {
                PopulateOrigin(
                    ref data,
                    in binding,
                    executionResult.InstanceId.IsValid
                        ? executionResult.InstanceId
                        : resolvedInstanceId);
            }
            data.OriginFrame = command.Frame;
            data.ProducerSequence = command.Sequence;
            data.RuntimeCommandKind = executionResult.RuntimeCommandKind;
            data.CommandSourceId = command.SourceId;
            data.CommandId = command.CommandId;
            data.CommandTargetId = command.TargetId;
            data.CommandPayload0 = command.Payload0;
            data.CommandPayload1 = command.Payload1;
            data.CommandPayload2 = command.Payload2;
            data.Provenance = executionResult.Provenance;
            data.BatchId = executionResult.BatchId;
            data.ReasonCode = (int)result.ErrorCode;
            data.Detail0 = result.Detail0;
            data.Detail1 = result.Detail1;
            PublishInternal(ref data);
        }

        private void PublishDeferredResult(
            RuntimeFrame frame,
            in GameplayTriggerDeferredRecord record,
            GameplayTriggerResult result)
        {
            if (_eventSource == null) return;
            GameplayTriggerRuntimeEventData data = GameplayTriggerRuntimeEventData.Create(
                frame,
                GameplayTriggerRuntimeEventKind.InternalDeferredResult,
                result.Success
                    ? GameplayTriggerRuntimeEventStatus.Succeeded
                    : GameplayTriggerRuntimeEventStatus.Rejected);
            GetOriginByBindingId(
                record.OriginBindingId,
                out GameplayTriggerBinding binding,
                out GameplayTriggerInstanceId instanceId);
            PopulateOrigin(ref data, in binding, instanceId);
            data.RouteId = record.OriginRouteId;
            data.ActivationId = record.ActivationId;
            data.ActionIndex = record.ActionIndex;
            data.ActionKindId = record.ActionKindId;
            data.Target = GameplayTriggerTargetRef.FromTriggerSourceBinding(
                record.TargetSourceBindingId);
            data.TargetInstanceId = record.TargetInstanceId;
            data.TargetSourceBindingId = record.TargetSourceBindingId;
            data.InternalDeferredOperationKind = record.OperationKind;
            data.Subject = record.Subject;
            data.Instigator = record.Instigator;
            data.SignalId = record.SignalId;
            data.ActionPayload0 = record.Payload0;
            data.ActionPayload1 = record.Payload1;
            data.ActionPayload2 = record.Payload2;
            data.DueFrame = record.OriginalDueFrame;
            data.ProducerSequence = record.Sequence;
            data.ReasonCode = (int)result.ErrorCode;
            data.Detail0 = result.Detail0;
            data.Detail1 = result.Detail1;
            PublishInternal(ref data);
        }

        private void PublishReliableResult(
            RuntimeFrame frame,
            in GameplayTriggerReliableOutboxRecord record,
            GameplayTriggerReliableTransferResult result)
        {
            if (_eventSource == null) return;
            GameplayTriggerBinding binding = GetBindingForInstance(record.OriginInstanceId);
            GameplayTriggerRuntimeEventData data = GameplayTriggerRuntimeEventData.Create(
                frame,
                GameplayTriggerRuntimeEventKind.ReliableTransferResult,
                result.Accepted
                    ? GameplayTriggerRuntimeEventStatus.Succeeded
                    : GameplayTriggerRuntimeEventStatus.Retained);
            PopulateOrigin(ref data, in binding, record.OriginInstanceId);
            data.RouteId = record.OriginRouteId;
            data.ActivationId = record.ActivationId;
            data.ActionIndex = record.ActionIndex;
            data.ActionKindId = record.ActionKindId;
            data.Target = record.ResolvedTarget;
            data.Owner = record.Owner;
            data.Subject = record.Subject;
            data.Instigator = record.Instigator;
            data.OccurrenceKind = record.OccurrenceKind;
            data.SignalId = record.OccurrenceSignalId;
            data.PreviousScalarRaw = record.PreviousScalarRaw;
            data.CurrentScalarRaw = record.CurrentScalarRaw;
            data.OccurrencePayload0 = record.OccurrencePayload0;
            data.OccurrencePayload1 = record.OccurrencePayload1;
            data.OccurrencePayload2 = record.OccurrencePayload2;
            data.OrderingValue = record.OccurrenceOrderingValue;
            data.PresenceEpoch = record.OccurrencePresenceEpoch;
            data.PresenceStartFrame = record.OccurrencePresenceStartFrame;
            data.ActionPayload0 = record.ActionPayload0;
            data.ActionPayload1 = record.ActionPayload1;
            data.ActionPayload2 = record.ActionPayload2;
            data.OriginFrame = record.OriginalActivationFrame;
            data.DueFrame = record.OriginalDueFrame;
            data.ProducerSequence = record.Sequence;
            data.ReliableTransferStatus = result.Status;
            data.Detail0 = result.DetailCode;
            PublishInternal(ref data);
        }

        private GameplayTriggerBinding GetBindingForInstance(GameplayTriggerInstanceId instanceId)
        {
            int instanceIndex = instanceId.Index - 1;
            return _catalog.GetBinding(_instances[instanceIndex].BindingIndex);
        }

        private bool TryGetBindingForInstance(
            GameplayTriggerInstanceId instanceId,
            out GameplayTriggerBinding binding)
        {
            int instanceIndex = instanceId.Index - 1;
            if (instanceId.IsValid &&
                (uint)instanceIndex < (uint)_instances.Length &&
                _instances[instanceIndex].InstanceId == instanceId)
            {
                binding = _catalog.GetBinding(_instances[instanceIndex].BindingIndex);
                return true;
            }
            binding = default;
            return false;
        }

        private bool TryGetBindingForSource(
            GameplayTriggerSourceBindingId sourceBindingId,
            out GameplayTriggerBinding binding,
            out GameplayTriggerInstanceId instanceId)
        {
            if (sourceBindingId.IsValid)
            {
                for (int i = 0; i < _instances.Length; i++)
                {
                    GameplayTriggerBinding candidate =
                        _catalog.GetBinding(_instances[i].BindingIndex);
                    if (candidate.SourceBindingId != sourceBindingId) continue;
                    binding = candidate;
                    instanceId = _instances[i].InstanceId;
                    return true;
                }
            }
            binding = default;
            instanceId = default;
            return false;
        }

        private void GetOriginByBindingId(
            GameplayTriggerBindingId bindingId,
            out GameplayTriggerBinding binding,
            out GameplayTriggerInstanceId instanceId)
        {
            for (int i = 0; i < _instances.Length; i++)
            {
                GameplayTriggerBinding candidate = _catalog.GetBinding(_instances[i].BindingIndex);
                if (candidate.BindingId != bindingId) continue;
                binding = candidate;
                instanceId = _instances[i].InstanceId;
                return;
            }
            binding = default;
            instanceId = default;
        }

        private static void PopulateOrigin(
            ref GameplayTriggerRuntimeEventData data,
            in GameplayTriggerBinding binding,
            GameplayTriggerInstanceId instanceId)
        {
            data.DefinitionId = binding.DefinitionId;
            data.BindingId = binding.BindingId;
            data.SourceBindingId = binding.SourceBindingId;
            data.InstanceId = instanceId;
            data.Owner = binding.Owner;
        }

        private static void PopulateOccurrence(
            ref GameplayTriggerRuntimeEventData data,
            in GameplayTriggerOccurrence occurrence)
        {
            data.Subject = occurrence.Subject;
            data.Instigator = occurrence.Instigator;
            data.OccurrenceKind = occurrence.Kind;
            data.SignalId = occurrence.SignalId;
            data.PreviousScalarRaw = occurrence.PreviousScalar.RawValue;
            data.CurrentScalarRaw = occurrence.CurrentScalar.RawValue;
            data.OccurrencePayload0 = occurrence.Payload0;
            data.OccurrencePayload1 = occurrence.Payload1;
            data.OccurrencePayload2 = occurrence.Payload2;
            data.OrderingValue = occurrence.OrderingValue;
            data.PresenceEpoch = occurrence.PresenceEpoch;
            data.PresenceStartFrame = occurrence.PresenceStartFrame;
        }

        private void PublishInternal(ref GameplayTriggerRuntimeEventData data)
        {
            GameplayTriggerRuntimeEvent evt = new GameplayTriggerRuntimeEvent(in data);
            _eventSource.TryPublishInternal(in evt);
        }
    }
}
