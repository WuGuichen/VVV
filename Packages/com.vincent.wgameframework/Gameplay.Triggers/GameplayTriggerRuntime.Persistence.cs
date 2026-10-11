using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    public sealed partial class GameplayTriggerRuntime
    {
        /// <remarks>NoAlloc. Internal defensive checkpoint classifier input.</remarks>
        internal int PendingInputBatchCount => _batchCount;

        /// <remarks>NoAlloc. Internal defensive checkpoint classifier input.</remarks>
        internal int PendingOccupancyItemCount => _occupancyInputCount;

        /// <remarks>NoAlloc. Internal defensive checkpoint classifier input.</remarks>
        internal int PendingScalarItemCount => _scalarInputCount;

        /// <remarks>NoAlloc. Internal defensive checkpoint classifier input.</remarks>
        internal int PendingSignalItemCount => _signalInputCount;

        /// <remarks>NoAlloc. Pure numeric classifier used by capture and quiescence checks.</remarks>
        internal static GameplayTriggerCheckpointPendingDetailCode ClassifyCheckpointPendingWork(
            int pendingInputBatches,
            int pendingOccupancyItems,
            int pendingScalarItems,
            int pendingSignalItems)
        {
            return ClassifyCheckpointPendingWork(
                pendingInputBatches,
                pendingOccupancyItems,
                pendingScalarItems,
                pendingSignalItems,
                0,
                false,
                false);
        }

        /// <remarks>NoAlloc. Pure numeric classifier including internal deferred transient state.</remarks>
        internal static GameplayTriggerCheckpointPendingDetailCode ClassifyCheckpointPendingWork(
            int pendingInputBatches,
            int pendingOccupancyItems,
            int pendingScalarItems,
            int pendingSignalItems,
            int pendingDeferredReservations,
            bool pendingDeferredSignalFinalization)
        {
            return ClassifyCheckpointPendingWork(
                pendingInputBatches,
                pendingOccupancyItems,
                pendingScalarItems,
                pendingSignalItems,
                pendingDeferredReservations,
                pendingDeferredSignalFinalization,
                false);
        }

        /// <remarks>NoAlloc. Pure numeric classifier including reliable transfer state.</remarks>
        internal static GameplayTriggerCheckpointPendingDetailCode ClassifyCheckpointPendingWork(
            int pendingInputBatches,
            int pendingOccupancyItems,
            int pendingScalarItems,
            int pendingSignalItems,
            int pendingDeferredReservations,
            bool pendingDeferredSignalFinalization,
            bool pendingReliableOutboxTransfer)
        {
            if (pendingReliableOutboxTransfer)
                return GameplayTriggerCheckpointPendingDetailCode.PendingReliableOutboxTransfer;
            if (pendingInputBatches > 0)
                return GameplayTriggerCheckpointPendingDetailCode.PendingInputBatches;
            if (pendingOccupancyItems > 0)
                return GameplayTriggerCheckpointPendingDetailCode.PendingOccupancyItems;
            if (pendingScalarItems > 0)
                return GameplayTriggerCheckpointPendingDetailCode.PendingScalarItems;
            if (pendingSignalItems > 0)
                return GameplayTriggerCheckpointPendingDetailCode.PendingSignalItems;
            if (pendingDeferredReservations > 0)
                return GameplayTriggerCheckpointPendingDetailCode.PendingDeferredReservations;
            if (pendingDeferredSignalFinalization)
                return GameplayTriggerCheckpointPendingDetailCode.PendingDeferredSignalFinalization;
            return GameplayTriggerCheckpointPendingDetailCode.None;
        }

        /// <remarks>NoAlloc. Reads current pending counts without clearing or draining them.</remarks>
        internal GameplayTriggerCheckpointPendingDetailCode ClassifyCheckpointPendingWork()
        {
            int pendingReservations =
                _deferredReservationCount + _reliableOutboxReservationCount;
            return ClassifyCheckpointPendingWork(
                pendingReservations > 0 ? 0 : _batchCount,
                pendingReservations > 0 ? 0 : _occupancyInputCount,
                pendingReservations > 0 ? 0 : _scalarInputCount,
                pendingReservations > 0 ? 0 : _signalInputCount,
                pendingReservations,
                _hasPendingDeferredSignalFinalization,
                _reliableTransferInFlight);
        }

        /// <summary>Builds a complete replacement store without mutating the live runtime.</summary>
        /// <remarks>AllocByDesign. The caller must fully validate every row before calling.</remarks>
        internal GameplayTriggerRuntimeRestorePlan CreateRestorePlan(
            long nextActivationId,
            long nextDeferredSequence,
            long nextReliableOutboxSequence,
            bool hasEvaluated,
            RuntimeFrame lastEvaluatedFrame,
            GameplayTriggerInstanceAuthorityState[] instanceStates,
            GameplayTriggerRelationAuthorityState[] relationStates,
            GameplayTriggerRouteAuthorityState[] routeStates,
            GameplayTriggerDeferredRecord[] deferredRecords,
            GameplayTriggerReliableOutboxRecord[] reliableOutboxRecords)
        {
            if (instanceStates == null) throw new ArgumentNullException(nameof(instanceStates));
            if (relationStates == null) throw new ArgumentNullException(nameof(relationStates));
            if (routeStates == null) throw new ArgumentNullException(nameof(routeStates));
            if (deferredRecords == null) throw new ArgumentNullException(nameof(deferredRecords));
            if (reliableOutboxRecords == null) throw new ArgumentNullException(nameof(reliableOutboxRecords));

            var instances = new InstanceState[_instances.Length];
            for (int i = 0; i < instances.Length; i++)
            {
                InstanceState current = _instances[i];
                GameplayTriggerInstanceAuthorityState restored = instanceStates[i];
                current.Enabled = restored.Enabled;
                current.ActiveCount = restored.ActiveCount;
                current.AggregatePresenceEpoch = restored.AggregatePresenceEpoch;
                instances[i] = current;
            }

            var relations = new RelationState[_relations.Length];
            for (int i = 0; i < relationStates.Length; i++)
            {
                GameplayTriggerRelationAuthorityState restored = relationStates[i];
                var relation = new RelationState(restored.InstanceId.Index - 1, restored.Subject)
                {
                    Active = restored.Active,
                    Primed = restored.Primed,
                    Scalar = restored.Scalar,
                    PresenceEpoch = restored.PresenceEpoch,
                    PresenceStartFrame = restored.PresenceStartFrame,
                    SeenFrame = -1L
                };
                relations[i] = relation;
            }

            var routes = new RouteState[_routeStates.Length];
            for (int i = 0; i < routeStates.Length; i++)
            {
                GameplayTriggerRouteAuthorityState restored = routeStates[i];
                var route = new RouteState(
                    restored.InstanceId.Index - 1,
                    restored.RouteId,
                    restored.Subject,
                    restored.Instigator)
                {
                    ActivationCount = restored.ActivationCount,
                    HasActivated = restored.HasActivated,
                    LastActivationFrame = restored.LastActivationFrame,
                    LastActivatedPresenceEpoch = restored.LastActivatedPresenceEpoch,
                    HasStaySchedule = restored.HasStaySchedule,
                    NextStayFrame = restored.NextStayFrame
                };
                routes[i] = route;
            }

            var restoredDeferredRecords = new GameplayTriggerDeferredRecord[_deferredRecords.Length];
            Array.Copy(
                deferredRecords,
                restoredDeferredRecords,
                deferredRecords.Length);
            var restoredReliableOutboxRecords =
                new GameplayTriggerReliableOutboxRecord[_reliableOutboxRecords.Length];
            Array.Copy(
                reliableOutboxRecords,
                restoredReliableOutboxRecords,
                reliableOutboxRecords.Length);

            return new GameplayTriggerRuntimeRestorePlan(
                instances,
                relations,
                routes,
                new InputBatch[_batches.Length],
                new GameplayEntityId[_occupancyInputs.Length],
                new GameplayTriggerScalarSample[_scalarInputs.Length],
                new GameplayTriggerSignal[_signalInputs.Length],
                new GameplayTriggerOccurrence[_occurrences.Length],
                new Candidate[_candidates.Length],
                new GameplayTriggerActivation[_activations.Length],
                new GameplayTriggerActionIntent[_intents.Length],
                new PreparedAction[_preparedActions.Length],
                new bool[_conditionStack.Length],
                new GameplayTriggerDiagnostic[_diagnostics.Length],
                restoredDeferredRecords,
                new DeferredReservation[_deferredReservations.Length],
                new bool[_deferredRemoveScratch.Length],
                new bool[_deferredSignalStaged.Length],
                new bool[_deferredSignalSourceBlocked.Length],
                restoredReliableOutboxRecords,
                new ReliableOutboxReservation[_reliableOutboxReservations.Length],
                new bool[_reliableOutboxRemoveScratch.Length],
                relationStates.Length,
                routeStates.Length,
                deferredRecords.Length,
                reliableOutboxRecords.Length,
                nextActivationId,
                nextDeferredSequence,
                nextReliableOutboxSequence,
                hasEvaluated,
                lastEvaluatedFrame);
        }

        /// <summary>Commits one fully built restore plan by replacing live store references and markers.</summary>
        /// <remarks>
        /// NoAlloc. Call only during exclusive single-threaded bootstrap/restore with no Tick or concurrent readers.
        /// This method performs no validation, external calls, or allocation.
        /// </remarks>
        internal void CommitRestorePlan(GameplayTriggerRuntimeRestorePlan plan)
        {
            _instances = (InstanceState[])plan.Instances;
            _relations = (RelationState[])plan.Relations;
            _routeStates = (RouteState[])plan.RouteStates;
            _batches = (InputBatch[])plan.Batches;
            _occupancyInputs = (GameplayEntityId[])plan.OccupancyInputs;
            _scalarInputs = (GameplayTriggerScalarSample[])plan.ScalarInputs;
            _signalInputs = (GameplayTriggerSignal[])plan.SignalInputs;
            _occurrences = (GameplayTriggerOccurrence[])plan.Occurrences;
            _candidates = (Candidate[])plan.Candidates;
            _activations = (GameplayTriggerActivation[])plan.Activations;
            _intents = (GameplayTriggerActionIntent[])plan.Intents;
            _preparedActions = (PreparedAction[])plan.PreparedActions;
            _conditionStack = (bool[])plan.ConditionStack;
            _diagnostics = (GameplayTriggerDiagnostic[])plan.Diagnostics;
            _deferredRecords = (GameplayTriggerDeferredRecord[])plan.DeferredRecords;
            _deferredReservations = (DeferredReservation[])plan.DeferredReservations;
            _deferredRemoveScratch = (bool[])plan.DeferredRemoveScratch;
            _deferredSignalStaged = (bool[])plan.DeferredSignalStaged;
            _deferredSignalSourceBlocked = (bool[])plan.DeferredSignalSourceBlocked;
            _reliableOutboxRecords =
                (GameplayTriggerReliableOutboxRecord[])plan.ReliableOutboxRecords;
            _reliableOutboxReservations =
                (ReliableOutboxReservation[])plan.ReliableOutboxReservations;
            _reliableOutboxRemoveScratch = (bool[])plan.ReliableOutboxRemoveScratch;

            _relationCount = plan.RelationCount;
            _routeStateCount = plan.RouteStateCount;
            _batchCount = 0;
            _occupancyInputCount = 0;
            _scalarInputCount = 0;
            _signalInputCount = 0;
            _occurrenceCount = 0;
            _candidateCount = 0;
            _candidateCapacityReservation = 0;
            _activationCount = 0;
            _intentCount = 0;
            _diagnosticCount = 0;
            _deferredRecordCount = plan.DeferredRecordCount;
            _reliableOutboxRecordCount = plan.ReliableOutboxRecordCount;
            _deferredReservationCount = 0;
            _reliableOutboxReservationCount = 0;
            _nextActivationId = plan.NextActivationId;
            _nextDeferredSequence = plan.NextDeferredSequence;
            _nextReliableOutboxSequence = plan.NextReliableOutboxSequence;
            _hasPendingDeferredSignalFinalization = false;
            _reliableTransferInFlight = false;
            LastDeferredExecutionResult = default;
            LastReliableOutboxExecutionResult = default;
            _hasEvaluated = plan.HasEvaluated;
            _lastEvaluatedFrame = plan.LastEvaluatedFrame;
        }

        internal sealed class GameplayTriggerRuntimeRestorePlan
        {
            internal GameplayTriggerRuntimeRestorePlan(
                object instances,
                object relations,
                object routeStates,
                object batches,
                object occupancyInputs,
                object scalarInputs,
                object signalInputs,
                object occurrences,
                object candidates,
                object activations,
                object intents,
                object preparedActions,
                object conditionStack,
                object diagnostics,
                object deferredRecords,
                object deferredReservations,
                object deferredRemoveScratch,
                object deferredSignalStaged,
                object deferredSignalSourceBlocked,
                object reliableOutboxRecords,
                object reliableOutboxReservations,
                object reliableOutboxRemoveScratch,
                int relationCount,
                int routeStateCount,
                int deferredRecordCount,
                int reliableOutboxRecordCount,
                long nextActivationId,
                long nextDeferredSequence,
                long nextReliableOutboxSequence,
                bool hasEvaluated,
                RuntimeFrame lastEvaluatedFrame)
            {
                Instances = instances;
                Relations = relations;
                RouteStates = routeStates;
                Batches = batches;
                OccupancyInputs = occupancyInputs;
                ScalarInputs = scalarInputs;
                SignalInputs = signalInputs;
                Occurrences = occurrences;
                Candidates = candidates;
                Activations = activations;
                Intents = intents;
                PreparedActions = preparedActions;
                ConditionStack = conditionStack;
                Diagnostics = diagnostics;
                DeferredRecords = deferredRecords;
                DeferredReservations = deferredReservations;
                DeferredRemoveScratch = deferredRemoveScratch;
                DeferredSignalStaged = deferredSignalStaged;
                DeferredSignalSourceBlocked = deferredSignalSourceBlocked;
                ReliableOutboxRecords = reliableOutboxRecords;
                ReliableOutboxReservations = reliableOutboxReservations;
                ReliableOutboxRemoveScratch = reliableOutboxRemoveScratch;
                RelationCount = relationCount;
                RouteStateCount = routeStateCount;
                DeferredRecordCount = deferredRecordCount;
                ReliableOutboxRecordCount = reliableOutboxRecordCount;
                NextActivationId = nextActivationId;
                NextDeferredSequence = nextDeferredSequence;
                NextReliableOutboxSequence = nextReliableOutboxSequence;
                HasEvaluated = hasEvaluated;
                LastEvaluatedFrame = lastEvaluatedFrame;
            }

            internal object Instances { get; }
            internal object Relations { get; }
            internal object RouteStates { get; }
            internal object Batches { get; }
            internal object OccupancyInputs { get; }
            internal object ScalarInputs { get; }
            internal object SignalInputs { get; }
            internal object Occurrences { get; }
            internal object Candidates { get; }
            internal object Activations { get; }
            internal object Intents { get; }
            internal object PreparedActions { get; }
            internal object ConditionStack { get; }
            internal object Diagnostics { get; }
            internal object DeferredRecords { get; }
            internal object DeferredReservations { get; }
            internal object DeferredRemoveScratch { get; }
            internal object DeferredSignalStaged { get; }
            internal object DeferredSignalSourceBlocked { get; }
            internal object ReliableOutboxRecords { get; }
            internal object ReliableOutboxReservations { get; }
            internal object ReliableOutboxRemoveScratch { get; }
            internal int RelationCount { get; }
            internal int RouteStateCount { get; }
            internal int DeferredRecordCount { get; }
            internal int ReliableOutboxRecordCount { get; }
            internal long NextActivationId { get; }
            internal long NextDeferredSequence { get; }
            internal long NextReliableOutboxSequence { get; }
            internal bool HasEvaluated { get; }
            internal RuntimeFrame LastEvaluatedFrame { get; }
        }
    }
}
