using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    /// <summary>Contributes canonical Gameplay Trigger authority state to the numeric Runtime hash.</summary>
    /// <remarks>
    /// Construction is LowFreqAlloc. Contribute is NoAlloc after initialization and must only
    /// be called at a quiescent boundary after a complete Evaluate/Tick. All field numbers and
    /// array element field numbers are frozen; the field write order matches the legacy
    /// string-key implementation exactly.
    /// </remarks>
    public sealed class GameplayTriggerRuntimeHashContributor : IRuntimeHashContributor
    {
        /// <summary>Canonical default contributor identifier string.</summary>
        public const string DefaultContributorId = "mxframework.gameplay.triggers";

        /// <summary>Current Gameplay Trigger authority hash field schema.</summary>
        /// <remarks>NoAlloc compile-time constant. Additive public contract.</remarks>
        public const int CurrentStateSchemaVersion = 3;

        private readonly GameplayTriggerRuntime _runtime;
        private readonly int[] _relationOrder;
        private readonly int[] _routeStateOrder;
        /// <summary>Creates a hash contributor for one Gameplay Trigger Runtime authority store.</summary>
        /// <remarks>LowFreqAlloc additive public contract. Allocates canonical-order scratch from fixed runtime capacities.</remarks>
        public GameplayTriggerRuntimeHashContributor(GameplayTriggerRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _relationOrder = new int[runtime.RelationCapacity];
            _routeStateOrder = new int[runtime.RouteStateCapacity];
        }

        public int ContributorId => RuntimeHashContributorIds.GameplayTriggers;
        public int HashSchemaId => RuntimeHashSchemaIds.GameplayTriggers;
        public int HashSchemaVersion => 1;

        /// <summary>Writes the canonical Gameplay Trigger authority projection into the caller-owned accumulator.</summary>
        /// <remarks>NoAlloc after initialization. Additive public contract; call only at a complete Evaluate/Tick quiescent boundary.</remarks>
        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null) throw new ArgumentNullException(nameof(accumulator));

            GameplayTriggerCatalog catalog = _runtime.AuthorityCatalog;
            GameplayTriggerRuntimeOptions options = _runtime.AuthorityOptions;
            accumulator.AddInt(HashFields.StateSchema, CurrentStateSchemaVersion);
            accumulator.AddLong(HashFields.CatalogDefinitionHash, catalog.DefinitionCatalogHash);
            accumulator.AddLong(HashFields.CatalogBindingHash, catalog.BindingCatalogHash);
            accumulator.AddLong(HashFields.CatalogCombinedHash, catalog.GameplayTriggerCatalogHash);

            accumulator.AddInt(HashFields.MaxInputBatches, options.MaxInputBatches);
            accumulator.AddInt(HashFields.MaxInputItems, options.MaxInputItems);
            accumulator.AddInt(HashFields.MaxRelations, options.MaxRelations);
            accumulator.AddInt(HashFields.MaxOccurrences, options.MaxOccurrences);
            accumulator.AddInt(HashFields.MaxCandidates, options.MaxCandidates);
            accumulator.AddInt(HashFields.MaxRouteStates, options.MaxRouteStates);
            accumulator.AddInt(HashFields.MaxActivations, options.MaxActivations);
            accumulator.AddInt(HashFields.MaxIntents, options.MaxIntents);
            accumulator.AddInt(HashFields.MaxPreparedActions, options.MaxPreparedActions);
            accumulator.AddInt(HashFields.MaxConditionStackDepth, options.MaxConditionStackDepth);
            accumulator.AddInt(HashFields.MaxDeferredRecords, options.MaxDeferredRecords);
            accumulator.AddInt(HashFields.MaxReliableOutboxRecords, options.MaxReliableOutboxRecords);

            accumulator.AddLong(HashFields.NextActivationId, _runtime.NextActivationId);
            accumulator.AddLong(HashFields.NextDeferredSequence, _runtime.NextDeferredSequence);
            accumulator.AddLong(HashFields.NextReliableOutboxSequence, _runtime.NextReliableOutboxSequence);
            accumulator.AddInt(HashFields.HasEvaluated, _runtime.AuthorityHasEvaluated ? 1 : 0);
            accumulator.AddLong(HashFields.LastEvaluatedFrame, _runtime.AuthorityLastEvaluatedFrame.Value);

            // Instance ids are permanently assigned as index + 1 in backing-array order and
            // Phase 19.2E has no dynamic instance allocator, so this order is already canonical.
            accumulator.BeginArray(HashFields.Instances, _runtime.InstanceCount);
            for (int instanceIndex = 0; instanceIndex < _runtime.InstanceCount; instanceIndex++)
            {
                GameplayTriggerInstanceAuthorityState state =
                    _runtime.GetInstanceAuthorityState(instanceIndex);
                accumulator.BeginElement(instanceIndex);
                ContributeInstanceAuthorityState(accumulator, in state);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            int relationCount = _runtime.RelationCount;
            BuildRelationOrder(relationCount);
            accumulator.BeginArray(HashFields.Relations, relationCount);
            for (int orderedIndex = 0; orderedIndex < relationCount; orderedIndex++)
            {
                GameplayTriggerRelationAuthorityState state =
                    _runtime.GetRelationAuthorityState(_relationOrder[orderedIndex]);
                accumulator.BeginElement(orderedIndex);
                ContributeRelationAuthorityState(accumulator, in state);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            int routeStateCount = _runtime.RouteStateCount;
            BuildRouteStateOrder(routeStateCount);
            accumulator.BeginArray(HashFields.Routes, routeStateCount);
            for (int orderedIndex = 0; orderedIndex < routeStateCount; orderedIndex++)
            {
                GameplayTriggerRouteAuthorityState state =
                    _runtime.GetRouteAuthorityState(_routeStateOrder[orderedIndex]);
                accumulator.BeginElement(orderedIndex);
                ContributeRouteAuthorityState(accumulator, in state);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            int deferredRecordCount = _runtime.DeferredRecordCount;
            accumulator.BeginArray(HashFields.Deferred, deferredRecordCount);
            for (int recordIndex = 0; recordIndex < deferredRecordCount; recordIndex++)
            {
                GameplayTriggerDeferredRecord record = _runtime.GetDeferredRecord(recordIndex);
                accumulator.BeginElement(recordIndex);
                ContributeDeferredRecord(accumulator, in record);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            int reliableOutboxRecordCount = _runtime.ReliableOutboxRecordCount;
            accumulator.BeginArray(HashFields.ReliableOutbox, reliableOutboxRecordCount);
            for (int recordIndex = 0; recordIndex < reliableOutboxRecordCount; recordIndex++)
            {
                GameplayTriggerReliableOutboxRecord record =
                    _runtime.GetReliableOutboxRecord(recordIndex);
                accumulator.BeginElement(recordIndex);
                ContributeReliableOutboxRecord(accumulator, in record);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }

        /// <remarks>NoAlloc. Internal field-schema seam used by the contributor and focused tests.</remarks>
        internal static void ContributeReliableOutboxRecord(
            RuntimeHashAccumulator accumulator,
            in GameplayTriggerReliableOutboxRecord record)
        {
            accumulator.AddLong(ReliableOutboxHashFields.OriginalActivationFrame, record.OriginalActivationFrame.Value);
            accumulator.AddLong(ReliableOutboxHashFields.OriginalDueFrame, record.OriginalDueFrame.Value);
            accumulator.AddLong(ReliableOutboxHashFields.Sequence, record.Sequence);
            accumulator.AddLong(ReliableOutboxHashFields.ActivationId, record.ActivationId);
            accumulator.AddInt(ReliableOutboxHashFields.ActionIndex, record.ActionIndex);
            accumulator.AddInt(ReliableOutboxHashFields.ActionKindId, record.ActionKindId.Value);
            accumulator.AddInt(ReliableOutboxHashFields.OriginBindingId, record.OriginBindingId.Value);
            accumulator.AddInt(ReliableOutboxHashFields.OriginRouteId, record.OriginRouteId.Value);
            accumulator.AddInt(ReliableOutboxHashFields.OriginInstanceIndex, record.OriginInstanceId.Index);
            accumulator.AddInt(ReliableOutboxHashFields.OriginInstanceGeneration, record.OriginInstanceId.Generation);
            accumulator.AddInt(ReliableOutboxHashFields.OccurrenceKind, (int)record.OccurrenceKind);
            accumulator.AddInt(ReliableOutboxHashFields.OccurrenceSignalId, record.OccurrenceSignalId);
            accumulator.AddLong(ReliableOutboxHashFields.OccurrencePreviousScalarRaw, record.PreviousScalarRaw);
            accumulator.AddLong(ReliableOutboxHashFields.OccurrenceCurrentScalarRaw, record.CurrentScalarRaw);
            accumulator.AddLong(ReliableOutboxHashFields.OccurrencePayload0, record.OccurrencePayload0);
            accumulator.AddLong(ReliableOutboxHashFields.OccurrencePayload1, record.OccurrencePayload1);
            accumulator.AddLong(ReliableOutboxHashFields.OccurrencePayload2, record.OccurrencePayload2);
            accumulator.AddLong(ReliableOutboxHashFields.OccurrenceOrderingValue, record.OccurrenceOrderingValue);
            accumulator.AddLong(ReliableOutboxHashFields.OccurrencePresenceEpoch, record.OccurrencePresenceEpoch);
            accumulator.AddLong(ReliableOutboxHashFields.OccurrencePresenceStartFrame, record.OccurrencePresenceStartFrame);
            accumulator.AddInt(ReliableOutboxHashFields.TargetDomainId, record.ResolvedTarget.DomainId);
            accumulator.AddLong(ReliableOutboxHashFields.TargetValue, record.ResolvedTarget.Value);
            accumulator.AddInt(ReliableOutboxHashFields.TargetGeneration, record.ResolvedTarget.Generation);
            accumulator.AddInt(ReliableOutboxHashFields.OwnerIndex, record.Owner.Index);
            accumulator.AddInt(ReliableOutboxHashFields.OwnerGeneration, record.Owner.Generation);
            accumulator.AddInt(ReliableOutboxHashFields.SubjectIndex, record.Subject.Index);
            accumulator.AddInt(ReliableOutboxHashFields.SubjectGeneration, record.Subject.Generation);
            accumulator.AddInt(ReliableOutboxHashFields.InstigatorIndex, record.Instigator.Index);
            accumulator.AddInt(ReliableOutboxHashFields.InstigatorGeneration, record.Instigator.Generation);
            accumulator.AddLong(ReliableOutboxHashFields.ActionPayload0, record.ActionPayload0);
            accumulator.AddLong(ReliableOutboxHashFields.ActionPayload1, record.ActionPayload1);
            accumulator.AddLong(ReliableOutboxHashFields.ActionPayload2, record.ActionPayload2);
        }

        /// <remarks>NoAlloc. Internal field-schema seam used by the contributor and focused tests.</remarks>
        internal static void ContributeDeferredRecord(
            RuntimeHashAccumulator accumulator,
            in GameplayTriggerDeferredRecord record)
        {
            accumulator.AddLong(DeferredHashFields.OriginalDueFrame, record.OriginalDueFrame.Value);
            accumulator.AddLong(DeferredHashFields.Sequence, record.Sequence);
            accumulator.AddInt(DeferredHashFields.Operation, (int)record.OperationKind);
            accumulator.AddLong(DeferredHashFields.ActivationId, record.ActivationId);
            accumulator.AddInt(DeferredHashFields.ActionIndex, record.ActionIndex);
            accumulator.AddInt(DeferredHashFields.ActionKindId, record.ActionKindId.Value);
            accumulator.AddInt(DeferredHashFields.OriginBindingId, record.OriginBindingId.Value);
            accumulator.AddInt(DeferredHashFields.OriginRouteId, record.OriginRouteId.Value);
            accumulator.AddInt(DeferredHashFields.TargetInstanceIndex, record.TargetInstanceId.Index);
            accumulator.AddInt(DeferredHashFields.TargetInstanceGeneration, record.TargetInstanceId.Generation);
            accumulator.AddInt(DeferredHashFields.TargetSourceBindingId, record.TargetSourceBindingId.Value);
            accumulator.AddInt(DeferredHashFields.Enabled, record.Enabled ? 1 : 0);
            accumulator.AddInt(DeferredHashFields.SignalId, record.SignalId);
            accumulator.AddInt(DeferredHashFields.SubjectIndex, record.Subject.Index);
            accumulator.AddInt(DeferredHashFields.SubjectGeneration, record.Subject.Generation);
            accumulator.AddInt(DeferredHashFields.InstigatorIndex, record.Instigator.Index);
            accumulator.AddInt(DeferredHashFields.InstigatorGeneration, record.Instigator.Generation);
            accumulator.AddLong(DeferredHashFields.Payload0, record.Payload0);
            accumulator.AddLong(DeferredHashFields.Payload1, record.Payload1);
            accumulator.AddLong(DeferredHashFields.Payload2, record.Payload2);
        }

        /// <remarks>NoAlloc. Internal field-schema seam used by the contributor and focused tests.</remarks>
        internal static void ContributeInstanceAuthorityState(
            RuntimeHashAccumulator accumulator,
            in GameplayTriggerInstanceAuthorityState state)
        {
            accumulator.AddInt(InstanceHashFields.Index, state.InstanceId.Index);
            accumulator.AddInt(InstanceHashFields.Generation, state.InstanceId.Generation);
            accumulator.AddInt(InstanceHashFields.BindingId, state.BindingId.Value);
            accumulator.AddInt(InstanceHashFields.SourceBindingId, state.SourceBindingId.Value);
            accumulator.AddInt(InstanceHashFields.DefinitionId, state.DefinitionId.Value);
            accumulator.AddInt(InstanceHashFields.DefinitionVersion, state.DefinitionVersion);
            accumulator.AddInt(InstanceHashFields.SourceDefinitionId, state.SourceDefinitionId.Value);
            accumulator.AddInt(InstanceHashFields.OwnerIndex, state.Owner.Index);
            accumulator.AddInt(InstanceHashFields.OwnerGeneration, state.Owner.Generation);
            accumulator.AddInt(InstanceHashFields.Enabled, state.Enabled ? 1 : 0);
            accumulator.AddInt(InstanceHashFields.ActiveCount, state.ActiveCount);
            accumulator.AddLong(InstanceHashFields.AggregatePresenceEpoch, state.AggregatePresenceEpoch);
        }

        /// <remarks>NoAlloc. Internal field-schema seam used by the contributor and focused tests.</remarks>
        internal static void ContributeRelationAuthorityState(
            RuntimeHashAccumulator accumulator,
            in GameplayTriggerRelationAuthorityState state)
        {
            accumulator.AddInt(RelationHashFields.InstanceIndex, state.InstanceId.Index);
            accumulator.AddInt(RelationHashFields.InstanceGeneration, state.InstanceId.Generation);
            accumulator.AddInt(RelationHashFields.SubjectIndex, state.Subject.Index);
            accumulator.AddInt(RelationHashFields.SubjectGeneration, state.Subject.Generation);
            accumulator.AddInt(RelationHashFields.Active, state.Active ? 1 : 0);
            accumulator.AddInt(RelationHashFields.Primed, state.Primed ? 1 : 0);
            accumulator.AddLong(RelationHashFields.ScalarRaw, state.Scalar.RawValue);
            accumulator.AddLong(RelationHashFields.PresenceEpoch, state.PresenceEpoch);
            accumulator.AddLong(RelationHashFields.PresenceStartFrame, state.PresenceStartFrame);
        }

        /// <remarks>NoAlloc. Internal field-schema seam used by the contributor and focused tests.</remarks>
        internal static void ContributeRouteAuthorityState(
            RuntimeHashAccumulator accumulator,
            in GameplayTriggerRouteAuthorityState state)
        {
            accumulator.AddInt(RouteHashFields.InstanceIndex, state.InstanceId.Index);
            accumulator.AddInt(RouteHashFields.InstanceGeneration, state.InstanceId.Generation);
            accumulator.AddInt(RouteHashFields.RouteId, state.RouteId.Value);
            accumulator.AddInt(RouteHashFields.SubjectIndex, state.Subject.Index);
            accumulator.AddInt(RouteHashFields.SubjectGeneration, state.Subject.Generation);
            accumulator.AddInt(RouteHashFields.InstigatorIndex, state.Instigator.Index);
            accumulator.AddInt(RouteHashFields.InstigatorGeneration, state.Instigator.Generation);
            accumulator.AddInt(RouteHashFields.ActivationCount, state.ActivationCount);
            accumulator.AddInt(RouteHashFields.HasActivated, state.HasActivated ? 1 : 0);
            accumulator.AddLong(RouteHashFields.LastActivationFrame, state.LastActivationFrame);
            accumulator.AddLong(RouteHashFields.LastActivatedPresenceEpoch, state.LastActivatedPresenceEpoch);
            accumulator.AddInt(RouteHashFields.HasStaySchedule, state.HasStaySchedule ? 1 : 0);
            accumulator.AddLong(RouteHashFields.NextStayFrame, state.NextStayFrame);
        }

        [ThreadStatic] private static RuntimeHashAccumulator t_staticAccumulator;

        private static RuntimeHashAccumulator GetStaticAccumulator()
        {
            if (t_staticAccumulator == null)
                t_staticAccumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            else
                t_staticAccumulator.Reset();
            return t_staticAccumulator;
        }

        private void BuildRelationOrder(int count)
        {
            for (int i = 0; i < count; i++) _relationOrder[i] = i;
            HeapSortRelations(count);
        }

        private void BuildRouteStateOrder(int count)
        {
            for (int i = 0; i < count; i++) _routeStateOrder[i] = i;
            HeapSortRouteStates(count);
        }

        private void HeapSortRelations(int count)
        {
            for (int root = (count - 2) / 2; root >= 0 && count > 1; root--)
                SiftDownRelations(root, count);
            for (int end = count - 1; end > 0; end--)
            {
                Swap(_relationOrder, 0, end);
                SiftDownRelations(0, end);
            }
        }

        private void SiftDownRelations(int root, int count)
        {
            while (true)
            {
                int child = (root * 2) + 1;
                if (child >= count) return;
                int right = child + 1;
                if (right < count && CompareRelations(_relationOrder[child], _relationOrder[right]) < 0)
                    child = right;
                if (CompareRelations(_relationOrder[root], _relationOrder[child]) >= 0) return;
                Swap(_relationOrder, root, child);
                root = child;
            }
        }

        private int CompareRelations(int leftIndex, int rightIndex)
        {
            GameplayTriggerRelationAuthorityState left = _runtime.GetRelationAuthorityState(leftIndex);
            GameplayTriggerRelationAuthorityState right = _runtime.GetRelationAuthorityState(rightIndex);
            int result = left.InstanceId.CompareTo(right.InstanceId);
            return result != 0 ? result : left.Subject.CompareTo(right.Subject);
        }

        private void HeapSortRouteStates(int count)
        {
            for (int root = (count - 2) / 2; root >= 0 && count > 1; root--)
                SiftDownRouteStates(root, count);
            for (int end = count - 1; end > 0; end--)
            {
                Swap(_routeStateOrder, 0, end);
                SiftDownRouteStates(0, end);
            }
        }

        private void SiftDownRouteStates(int root, int count)
        {
            while (true)
            {
                int child = (root * 2) + 1;
                if (child >= count) return;
                int right = child + 1;
                if (right < count && CompareRouteStates(_routeStateOrder[child], _routeStateOrder[right]) < 0)
                    child = right;
                if (CompareRouteStates(_routeStateOrder[root], _routeStateOrder[child]) >= 0) return;
                Swap(_routeStateOrder, root, child);
                root = child;
            }
        }

        private int CompareRouteStates(int leftIndex, int rightIndex)
        {
            GameplayTriggerRouteAuthorityState left = _runtime.GetRouteAuthorityState(leftIndex);
            GameplayTriggerRouteAuthorityState right = _runtime.GetRouteAuthorityState(rightIndex);
            int result = left.InstanceId.CompareTo(right.InstanceId);
            if (result != 0) return result;
            result = left.RouteId.CompareTo(right.RouteId);
            if (result != 0) return result;
            result = left.Subject.CompareTo(right.Subject);
            return result != 0 ? result : left.Instigator.CompareTo(right.Instigator);
        }

        private static void Swap(int[] values, int left, int right)
        {
            int value = values[left];
            values[left] = values[right];
            values[right] = value;
        }

        private static class HashFields
        {
            public const int StateSchema = 1;
            public const int CatalogDefinitionHash = 2;
            public const int CatalogBindingHash = 3;
            public const int CatalogCombinedHash = 4;
            public const int MaxInputBatches = 5;
            public const int MaxInputItems = 6;
            public const int MaxRelations = 7;
            public const int MaxOccurrences = 8;
            public const int MaxCandidates = 9;
            public const int MaxRouteStates = 10;
            public const int MaxActivations = 11;
            public const int MaxIntents = 12;
            public const int MaxPreparedActions = 13;
            public const int MaxConditionStackDepth = 14;
            public const int MaxDeferredRecords = 15;
            public const int MaxReliableOutboxRecords = 16;
            public const int NextActivationId = 17;
            public const int NextDeferredSequence = 18;
            public const int NextReliableOutboxSequence = 19;
            public const int HasEvaluated = 20;
            public const int LastEvaluatedFrame = 21;
            public const int Instances = 22;
            public const int Relations = 23;
            public const int Routes = 24;
            public const int Deferred = 25;
            public const int ReliableOutbox = 26;
        }

        private static class InstanceHashFields
        {
            public const int Index = 1;
            public const int Generation = 2;
            public const int BindingId = 3;
            public const int SourceBindingId = 4;
            public const int DefinitionId = 5;
            public const int DefinitionVersion = 6;
            public const int SourceDefinitionId = 7;
            public const int OwnerIndex = 8;
            public const int OwnerGeneration = 9;
            public const int Enabled = 10;
            public const int ActiveCount = 11;
            public const int AggregatePresenceEpoch = 12;
        }

        private static class RelationHashFields
        {
            public const int InstanceIndex = 1;
            public const int InstanceGeneration = 2;
            public const int SubjectIndex = 3;
            public const int SubjectGeneration = 4;
            public const int Active = 5;
            public const int Primed = 6;
            public const int ScalarRaw = 7;
            public const int PresenceEpoch = 8;
            public const int PresenceStartFrame = 9;
        }

        private static class RouteHashFields
        {
            public const int InstanceIndex = 1;
            public const int InstanceGeneration = 2;
            public const int RouteId = 3;
            public const int SubjectIndex = 4;
            public const int SubjectGeneration = 5;
            public const int InstigatorIndex = 6;
            public const int InstigatorGeneration = 7;
            public const int ActivationCount = 8;
            public const int HasActivated = 9;
            public const int LastActivationFrame = 10;
            public const int LastActivatedPresenceEpoch = 11;
            public const int HasStaySchedule = 12;
            public const int NextStayFrame = 13;
        }

        private static class DeferredHashFields
        {
            public const int OriginalDueFrame = 1;
            public const int Sequence = 2;
            public const int Operation = 3;
            public const int ActivationId = 4;
            public const int ActionIndex = 5;
            public const int ActionKindId = 6;
            public const int OriginBindingId = 7;
            public const int OriginRouteId = 8;
            public const int TargetInstanceIndex = 9;
            public const int TargetInstanceGeneration = 10;
            public const int TargetSourceBindingId = 11;
            public const int Enabled = 12;
            public const int SignalId = 13;
            public const int SubjectIndex = 14;
            public const int SubjectGeneration = 15;
            public const int InstigatorIndex = 16;
            public const int InstigatorGeneration = 17;
            public const int Payload0 = 18;
            public const int Payload1 = 19;
            public const int Payload2 = 20;
        }

        private static class ReliableOutboxHashFields
        {
            public const int OriginalActivationFrame = 1;
            public const int OriginalDueFrame = 2;
            public const int Sequence = 3;
            public const int ActivationId = 4;
            public const int ActionIndex = 5;
            public const int ActionKindId = 6;
            public const int OriginBindingId = 7;
            public const int OriginRouteId = 8;
            public const int OriginInstanceIndex = 9;
            public const int OriginInstanceGeneration = 10;
            public const int OccurrenceKind = 11;
            public const int OccurrenceSignalId = 12;
            public const int OccurrencePreviousScalarRaw = 13;
            public const int OccurrenceCurrentScalarRaw = 14;
            public const int OccurrencePayload0 = 15;
            public const int OccurrencePayload1 = 16;
            public const int OccurrencePayload2 = 17;
            public const int OccurrenceOrderingValue = 18;
            public const int OccurrencePresenceEpoch = 19;
            public const int OccurrencePresenceStartFrame = 20;
            public const int TargetDomainId = 21;
            public const int TargetValue = 22;
            public const int TargetGeneration = 23;
            public const int OwnerIndex = 24;
            public const int OwnerGeneration = 25;
            public const int SubjectIndex = 26;
            public const int SubjectGeneration = 27;
            public const int InstigatorIndex = 28;
            public const int InstigatorGeneration = 29;
            public const int ActionPayload0 = 30;
            public const int ActionPayload1 = 31;
            public const int ActionPayload2 = 32;
        }
    }
}
