using System;
using System.Buffers.Binary;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    public enum GameplayTriggerAuthorityBinaryErrorCode
    {
        None = 0,
        DestinationTooSmall = 1,
        InvalidHeader = 2,
        UnsupportedSchema = 3,
        IdentityMismatch = 4,
        CapacityExceeded = 5,
        InvalidRecord = 6,
        UnknownEntity = 7,
        PendingWork = 8,
        HashMismatch = 9
    }

    public readonly struct GameplayTriggerAuthorityBinaryResult
    {
        private GameplayTriggerAuthorityBinaryResult(bool success, GameplayTriggerAuthorityBinaryErrorCode errorCode, int detail)
        {
            Success = success;
            ErrorCode = errorCode;
            Detail = detail;
        }

        public bool Success { get; }
        public GameplayTriggerAuthorityBinaryErrorCode ErrorCode { get; }
        public int Detail { get; }
        public static GameplayTriggerAuthorityBinaryResult Succeeded() =>
            new GameplayTriggerAuthorityBinaryResult(true, GameplayTriggerAuthorityBinaryErrorCode.None, 0);
        public static GameplayTriggerAuthorityBinaryResult Failed(GameplayTriggerAuthorityBinaryErrorCode code, int detail = 0) =>
            new GameplayTriggerAuthorityBinaryResult(false, code, detail);
    }

    public interface IGameplayTriggerAuthorityEntityValidator
    {
        bool Contains(GameplayEntityId entityId);
    }

    /// <summary>Canonical bounded binary capture and candidate restore for Gameplay Trigger schema-3 authority.</summary>
    public sealed class GameplayTriggerAuthorityBinaryState
    {
        public const uint Magic = 0x4D585431U;
        public const ushort BinarySchema = 1;
        public const ushort SemanticAuthoritySchema = 3;
        public const int HeaderBytes = 144;
        public const int InstanceRecordBytes = 52;
        public const int RelationRecordBytes = 44;
        public const int RouteRecordBytes = 64;
        public const int DeferredRecordBytes = 104;
        public const int ReliableRecordBytes = 192;

        private readonly GameplayTriggerRuntime _runtime;
        private readonly GameplayTriggerInstanceAuthorityState[] _instances;
        private readonly GameplayTriggerRelationAuthorityState[] _relations;
        private readonly GameplayTriggerRouteAuthorityState[] _routes;
        private readonly GameplayTriggerDeferredRecord[] _deferred;
        private readonly GameplayTriggerReliableOutboxRecord[] _reliable;
        private readonly int[] _relationOrder;
        private readonly int[] _routeOrder;
        private GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan _preparedPlan;
        private IGameplayTriggerAuthorityEntityValidator _entityValidator;

        public GameplayTriggerAuthorityBinaryState(
            GameplayTriggerRuntime runtime,
            IGameplayTriggerAuthorityEntityValidator entityValidator = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _entityValidator = entityValidator;
            GameplayTriggerRuntimeOptions options = runtime.AuthorityOptions;
            _instances = new GameplayTriggerInstanceAuthorityState[runtime.InstanceCount];
            _relations = new GameplayTriggerRelationAuthorityState[options.MaxRelations];
            _routes = new GameplayTriggerRouteAuthorityState[options.MaxRouteStates];
            _deferred = new GameplayTriggerDeferredRecord[options.MaxDeferredRecords];
            _reliable = new GameplayTriggerReliableOutboxRecord[options.MaxReliableOutboxRecords];
            _relationOrder = new int[options.MaxRelations];
            _routeOrder = new int[options.MaxRouteStates];
        }

        public int MaximumEncodedBytes => HeaderBytes
            + (_instances.Length * InstanceRecordBytes)
            + (_relations.Length * RelationRecordBytes)
            + (_routes.Length * RouteRecordBytes)
            + (_deferred.Length * DeferredRecordBytes)
            + (_reliable.Length * ReliableRecordBytes);

        public void SetEntityValidator(IGameplayTriggerAuthorityEntityValidator validator)
        {
            _entityValidator = validator;
        }

        /// <remarks>NoAlloc. Writes the quiescent authority state to caller-owned storage.</remarks>
        public GameplayTriggerAuthorityBinaryResult TryCapture(
            Span<byte> destination,
            out int bytesWritten,
            out long binaryHash)
        {
            bytesWritten = 0;
            binaryHash = 0L;
            if (_runtime.ClassifyCheckpointPendingWork() != GameplayTriggerCheckpointPendingDetailCode.None)
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.PendingWork);
            int required = HeaderBytes
                + (_runtime.InstanceCount * InstanceRecordBytes)
                + (_runtime.RelationCount * RelationRecordBytes)
                + (_runtime.RouteStateCount * RouteRecordBytes)
                + (_runtime.DeferredRecordCount * DeferredRecordBytes)
                + (_runtime.ReliableOutboxRecordCount * ReliableRecordBytes);
            if (destination.Length < required)
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.DestinationTooSmall);

            destination.Slice(0, required).Clear();
            WriteHeader(destination, required);
            int offset = HeaderBytes;
            for (int i = 0; i < _runtime.InstanceCount; i++)
            {
                WriteInstance(destination.Slice(offset, InstanceRecordBytes), _runtime.GetInstanceAuthorityState(i));
                offset += InstanceRecordBytes;
            }

            BuildRelationOrder();
            for (int i = 0; i < _runtime.RelationCount; i++)
            {
                WriteRelation(destination.Slice(offset, RelationRecordBytes), _runtime.GetRelationAuthorityState(_relationOrder[i]));
                offset += RelationRecordBytes;
            }

            BuildRouteOrder();
            for (int i = 0; i < _runtime.RouteStateCount; i++)
            {
                WriteRoute(destination.Slice(offset, RouteRecordBytes), _runtime.GetRouteAuthorityState(_routeOrder[i]));
                offset += RouteRecordBytes;
            }

            for (int i = 0; i < _runtime.DeferredRecordCount; i++)
            {
                WriteDeferred(destination.Slice(offset, DeferredRecordBytes), _runtime.GetDeferredRecord(i));
                offset += DeferredRecordBytes;
            }

            for (int i = 0; i < _runtime.ReliableOutboxRecordCount; i++)
            {
                WriteReliable(destination.Slice(offset, ReliableRecordBytes), _runtime.GetReliableOutboxRecord(i));
                offset += ReliableRecordBytes;
            }

            bytesWritten = required;
            binaryHash = HashBytes(destination.Slice(0, required));
            return GameplayTriggerAuthorityBinaryResult.Succeeded();
        }

        /// <remarks>
        /// LowFreqAlloc. Fully validates into a replacement plan before live publication; failures leave live state unchanged.
        /// </remarks>
        public GameplayTriggerAuthorityBinaryResult TryPrepare(ReadOnlySpan<byte> state, long expectedBinaryHash)
        {
            _preparedPlan = null;
            if (state.Length < HeaderBytes)
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.InvalidHeader);
            if (expectedBinaryHash != HashBytes(state))
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.HashMismatch);
            if (ReadU32(state, 0) != Magic || ReadU16(state, 6) != SemanticAuthoritySchema || ReadI32(state, 8) != HeaderBytes || ReadI32(state, 12) != 0)
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.InvalidHeader);
            if (ReadU16(state, 4) != BinarySchema)
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.UnsupportedSchema);
            GameplayTriggerCatalog catalog = _runtime.AuthorityCatalog;
            GameplayTriggerRuntimeOptions options = _runtime.AuthorityOptions;
            if (ReadI64(state, 16) != catalog.DefinitionCatalogHash
                || ReadI64(state, 24) != catalog.BindingCatalogHash
                || ReadI64(state, 32) != catalog.GameplayTriggerCatalogHash
                || !OptionsMatch(state.Slice(40, 48), options))
            {
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.IdentityMismatch);
            }

            long nextActivation = ReadI64(state, 88);
            long nextDeferred = ReadI64(state, 96);
            long nextReliable = ReadI64(state, 104);
            byte hasEvaluatedValue = state[112];
            for (int i = 113; i < 120; i++)
            {
                if (state[i] != 0)
                    return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.InvalidHeader);
            }
            long lastFrame = ReadI64(state, 120);
            int instanceCount = ReadU16(state, 128);
            int relationCount = ReadU16(state, 130);
            int routeCount = ReadU16(state, 132);
            int deferredCount = ReadU16(state, 134);
            int reliableCount = ReadU16(state, 136);
            if (ReadU16(state, 138) != 0 || ReadI32(state, 140) != 0)
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.InvalidHeader);
            if (instanceCount != _instances.Length
                || relationCount > _relations.Length
                || routeCount > _routes.Length
                || deferredCount > _deferred.Length
                || reliableCount > _reliable.Length)
            {
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.CapacityExceeded);
            }
            long expectedBytes = HeaderBytes
                + ((long)instanceCount * InstanceRecordBytes)
                + ((long)relationCount * RelationRecordBytes)
                + ((long)routeCount * RouteRecordBytes)
                + ((long)deferredCount * DeferredRecordBytes)
                + ((long)reliableCount * ReliableRecordBytes);
            if (state.Length != expectedBytes
                || nextActivation < 1L || nextActivation == long.MaxValue
                || nextDeferred < 1L || nextDeferred == long.MaxValue
                || nextReliable < 1L || nextReliable == long.MaxValue
                || hasEvaluatedValue > 1 || lastFrame < 0L || lastFrame == long.MaxValue)
            {
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.InvalidHeader);
            }

            int offset = HeaderBytes;
            for (int i = 0; i < instanceCount; i++)
            {
                GameplayTriggerAuthorityBinaryResult result = TryReadInstance(state.Slice(offset, InstanceRecordBytes), i, out _instances[i]);
                if (!result.Success) return result;
                offset += InstanceRecordBytes;
            }
            for (int i = 0; i < relationCount; i++)
            {
                GameplayTriggerAuthorityBinaryResult result = TryReadRelation(state.Slice(offset, RelationRecordBytes), out _relations[i]);
                if (!result.Success) return result;
                offset += RelationRecordBytes;
            }
            for (int i = 0; i < routeCount; i++)
            {
                GameplayTriggerAuthorityBinaryResult result = TryReadRoute(state.Slice(offset, RouteRecordBytes), out _routes[i]);
                if (!result.Success) return result;
                offset += RouteRecordBytes;
            }
            for (int i = 0; i < deferredCount; i++)
            {
                GameplayTriggerAuthorityBinaryResult result = TryReadDeferred(state.Slice(offset, DeferredRecordBytes), out _deferred[i]);
                if (!result.Success) return result;
                offset += DeferredRecordBytes;
            }
            for (int i = 0; i < reliableCount; i++)
            {
                GameplayTriggerAuthorityBinaryResult result = TryReadReliable(state.Slice(offset, ReliableRecordBytes), out _reliable[i]);
                if (!result.Success) return result;
                offset += ReliableRecordBytes;
            }

            var instanceCopy = new GameplayTriggerInstanceAuthorityState[instanceCount];
            var relationCopy = new GameplayTriggerRelationAuthorityState[relationCount];
            var routeCopy = new GameplayTriggerRouteAuthorityState[routeCount];
            var deferredCopy = new GameplayTriggerDeferredRecord[deferredCount];
            var reliableCopy = new GameplayTriggerReliableOutboxRecord[reliableCount];
            Array.Copy(_instances, instanceCopy, instanceCount);
            Array.Copy(_relations, relationCopy, relationCount);
            Array.Copy(_routes, routeCopy, routeCount);
            Array.Copy(_deferred, deferredCopy, deferredCount);
            Array.Copy(_reliable, reliableCopy, reliableCount);
            _preparedPlan = _runtime.CreateRestorePlan(
                nextActivation,
                nextDeferred,
                nextReliable,
                hasEvaluatedValue != 0,
                new RuntimeFrame(lastFrame),
                instanceCopy,
                relationCopy,
                routeCopy,
                deferredCopy,
                reliableCopy);
            return GameplayTriggerAuthorityBinaryResult.Succeeded();
        }

        /// <remarks>NoAlloc no-fail publication of a fully prepared candidate.</remarks>
        public void CommitPrepared()
        {
            if (_preparedPlan == null) throw new InvalidOperationException("Trigger authority candidate is not prepared.");
            GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan plan = _preparedPlan;
            _preparedPlan = null;
            _runtime.CommitRestorePlan(plan);
        }

        public void DiscardPrepared() { _preparedPlan = null; }

        private void WriteHeader(Span<byte> state, int totalBytes)
        {
            GameplayTriggerCatalog catalog = _runtime.AuthorityCatalog;
            GameplayTriggerRuntimeOptions options = _runtime.AuthorityOptions;
            WriteU32(state, 0, Magic);
            WriteU16(state, 4, BinarySchema);
            WriteU16(state, 6, SemanticAuthoritySchema);
            WriteI32(state, 8, HeaderBytes);
            WriteI64(state, 16, catalog.DefinitionCatalogHash);
            WriteI64(state, 24, catalog.BindingCatalogHash);
            WriteI64(state, 32, catalog.GameplayTriggerCatalogHash);
            WriteOptions(state.Slice(40, 48), options);
            WriteI64(state, 88, _runtime.NextActivationId);
            WriteI64(state, 96, _runtime.NextDeferredSequence);
            WriteI64(state, 104, _runtime.NextReliableOutboxSequence);
            state[112] = _runtime.AuthorityHasEvaluated ? (byte)1 : (byte)0;
            WriteI64(state, 120, _runtime.AuthorityLastEvaluatedFrame.Value);
            WriteU16(state, 128, checked((ushort)_runtime.InstanceCount));
            WriteU16(state, 130, checked((ushort)_runtime.RelationCount));
            WriteU16(state, 132, checked((ushort)_runtime.RouteStateCount));
            WriteU16(state, 134, checked((ushort)_runtime.DeferredRecordCount));
            WriteU16(state, 136, checked((ushort)_runtime.ReliableOutboxRecordCount));
        }

        private static void WriteOptions(Span<byte> state, GameplayTriggerRuntimeOptions value)
        {
            WriteI32(state, 0, value.MaxInputBatches); WriteI32(state, 4, value.MaxInputItems);
            WriteI32(state, 8, value.MaxRelations); WriteI32(state, 12, value.MaxOccurrences);
            WriteI32(state, 16, value.MaxCandidates); WriteI32(state, 20, value.MaxRouteStates);
            WriteI32(state, 24, value.MaxActivations); WriteI32(state, 28, value.MaxIntents);
            WriteI32(state, 32, value.MaxPreparedActions); WriteI32(state, 36, value.MaxConditionStackDepth);
            WriteI32(state, 40, value.MaxDeferredRecords); WriteI32(state, 44, value.MaxReliableOutboxRecords);
        }

        private static bool OptionsMatch(ReadOnlySpan<byte> state, GameplayTriggerRuntimeOptions value)
        {
            return ReadI32(state, 0) == value.MaxInputBatches && ReadI32(state, 4) == value.MaxInputItems
                && ReadI32(state, 8) == value.MaxRelations && ReadI32(state, 12) == value.MaxOccurrences
                && ReadI32(state, 16) == value.MaxCandidates && ReadI32(state, 20) == value.MaxRouteStates
                && ReadI32(state, 24) == value.MaxActivations && ReadI32(state, 28) == value.MaxIntents
                && ReadI32(state, 32) == value.MaxPreparedActions && ReadI32(state, 36) == value.MaxConditionStackDepth
                && ReadI32(state, 40) == value.MaxDeferredRecords && ReadI32(state, 44) == value.MaxReliableOutboxRecords;
        }

        private static void WriteInstance(Span<byte> state, GameplayTriggerInstanceAuthorityState value)
        {
            WriteEntity(state, 0, value.InstanceId.Index, value.InstanceId.Generation);
            WriteI32(state, 8, value.BindingId.Value); WriteI32(state, 12, value.SourceBindingId.Value);
            WriteI32(state, 16, value.DefinitionId.Value); WriteI32(state, 20, value.DefinitionVersion);
            WriteI32(state, 24, value.SourceDefinitionId.Value); WriteEntity(state, 28, value.Owner.Index, value.Owner.Generation);
            state[36] = value.Enabled ? (byte)1 : (byte)0;
            WriteI32(state, 40, value.ActiveCount); WriteI64(state, 44, value.AggregatePresenceEpoch);
        }

        private GameplayTriggerAuthorityBinaryResult TryReadInstance(ReadOnlySpan<byte> state, int index, out GameplayTriggerInstanceAuthorityState value)
        {
            value = default;
            GameplayTriggerInstanceAuthorityState expected = _runtime.GetInstanceAuthorityState(index);
            if (state[36] > 1 || state[37] != 0 || state[38] != 0 || state[39] != 0
                || ReadI32(state, 0) != expected.InstanceId.Index || ReadI32(state, 4) != expected.InstanceId.Generation
                || ReadI32(state, 8) != expected.BindingId.Value || ReadI32(state, 12) != expected.SourceBindingId.Value
                || ReadI32(state, 16) != expected.DefinitionId.Value || ReadI32(state, 20) != expected.DefinitionVersion
                || ReadI32(state, 24) != expected.SourceDefinitionId.Value
                || ReadI32(state, 28) != expected.Owner.Index || ReadI32(state, 32) != expected.Owner.Generation
                || !Contains(expected.Owner)
                || ReadI32(state, 40) < 0 || ReadI64(state, 44) < 0L)
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.InvalidRecord, index);
            value = new GameplayTriggerInstanceAuthorityState(
                expected.InstanceId, expected.BindingId, expected.SourceBindingId, expected.DefinitionId,
                expected.DefinitionVersion, expected.SourceDefinitionId, expected.Owner, state[36] != 0,
                ReadI32(state, 40), ReadI64(state, 44));
            return GameplayTriggerAuthorityBinaryResult.Succeeded();
        }

        private static void WriteRelation(Span<byte> state, GameplayTriggerRelationAuthorityState value)
        {
            WriteEntity(state, 0, value.InstanceId.Index, value.InstanceId.Generation);
            WriteEntity(state, 8, value.Subject.Index, value.Subject.Generation);
            state[16] = value.Active ? (byte)1 : (byte)0; state[17] = value.Primed ? (byte)1 : (byte)0;
            WriteI64(state, 20, value.Scalar.RawValue); WriteI64(state, 28, value.PresenceEpoch); WriteI64(state, 36, value.PresenceStartFrame);
        }

        private GameplayTriggerAuthorityBinaryResult TryReadRelation(ReadOnlySpan<byte> state, out GameplayTriggerRelationAuthorityState value)
        {
            value = default;
            if (!TryReadEntity(state, 0, out GameplayEntityId instanceEntity)
                || instanceEntity.Index > _runtime.InstanceCount || state[16] > 1 || state[17] > 1 || state[18] != 0 || state[19] != 0
                || !TryReadEntity(state, 8, out GameplayEntityId subject) || !Contains(subject)
                || ReadI64(state, 28) < 0L || ReadI64(state, 36) < 0L)
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.InvalidRecord);
            value = new GameplayTriggerRelationAuthorityState(
                new GameplayTriggerInstanceId(instanceEntity.Index, instanceEntity.Generation), subject,
                state[16] != 0, state[17] != 0, Fix64.FromRaw(ReadI64(state, 20)), ReadI64(state, 28), ReadI64(state, 36));
            return GameplayTriggerAuthorityBinaryResult.Succeeded();
        }

        private static void WriteRoute(Span<byte> state, GameplayTriggerRouteAuthorityState value)
        {
            WriteEntity(state, 0, value.InstanceId.Index, value.InstanceId.Generation); WriteI32(state, 8, value.RouteId.Value);
            WriteEntity(state, 16, value.Subject.Index, value.Subject.Generation); WriteEntity(state, 24, value.Instigator.Index, value.Instigator.Generation);
            WriteI32(state, 32, value.ActivationCount); state[36] = value.HasActivated ? (byte)1 : (byte)0; state[37] = value.HasStaySchedule ? (byte)1 : (byte)0;
            WriteI64(state, 40, value.LastActivationFrame); WriteI64(state, 48, value.LastActivatedPresenceEpoch); WriteI64(state, 56, value.NextStayFrame);
        }

        private GameplayTriggerAuthorityBinaryResult TryReadRoute(ReadOnlySpan<byte> state, out GameplayTriggerRouteAuthorityState value)
        {
            value = default;
            if (!TryReadEntity(state, 0, out GameplayEntityId instanceEntity) || instanceEntity.Index > _runtime.InstanceCount
                || ReadI32(state, 8) <= 0 || ReadI32(state, 12) != 0
                || !TryReadOptionalEntity(state, 16, out GameplayEntityId subject) || !TryReadOptionalEntity(state, 24, out GameplayEntityId instigator)
                || !Contains(subject) || !Contains(instigator) || ReadI32(state, 32) < 0 || state[36] > 1 || state[37] > 1
                || state[38] != 0 || state[39] != 0 || ReadI64(state, 40) < 0L || ReadI64(state, 48) < 0L || ReadI64(state, 56) < 0L)
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.InvalidRecord);
            value = new GameplayTriggerRouteAuthorityState(
                new GameplayTriggerInstanceId(instanceEntity.Index, instanceEntity.Generation), new GameplayTriggerRouteId(ReadI32(state, 8)),
                subject, instigator, ReadI32(state, 32), state[36] != 0, ReadI64(state, 40), ReadI64(state, 48), state[37] != 0, ReadI64(state, 56));
            return GameplayTriggerAuthorityBinaryResult.Succeeded();
        }

        private static void WriteDeferred(Span<byte> s, GameplayTriggerDeferredRecord r)
        {
            WriteI64(s, 0, r.OriginalDueFrame.Value); WriteI64(s, 8, r.Sequence); WriteI32(s, 16, (int)r.OperationKind);
            WriteI64(s, 20, r.ActivationId); WriteI32(s, 28, r.ActionIndex); WriteI32(s, 32, r.ActionKindId.Value);
            WriteI32(s, 36, r.OriginBindingId.Value); WriteI32(s, 40, r.OriginRouteId.Value);
            WriteEntity(s, 44, r.TargetInstanceId.Index, r.TargetInstanceId.Generation); WriteI32(s, 52, r.TargetSourceBindingId.Value);
            WriteI32(s, 56, r.Enabled ? 1 : 0); WriteI32(s, 60, r.SignalId); WriteEntity(s, 64, r.Subject.Index, r.Subject.Generation);
            WriteEntity(s, 72, r.Instigator.Index, r.Instigator.Generation); WriteI64(s, 80, r.Payload0); WriteI64(s, 88, r.Payload1); WriteI64(s, 96, r.Payload2);
        }

        private GameplayTriggerAuthorityBinaryResult TryReadDeferred(ReadOnlySpan<byte> s, out GameplayTriggerDeferredRecord r)
        {
            r = default;
            int op = ReadI32(s, 16); int enabled = ReadI32(s, 56);
            if (ReadI64(s, 0) < 0 || ReadI64(s, 8) < 1 || op < 1 || op > 3 || ReadI64(s, 20) < 1 || ReadI32(s, 28) < 0
                || ReadI32(s, 32) <= 0 || ReadI32(s, 36) <= 0 || ReadI32(s, 40) <= 0
                || !TryReadEntity(s, 44, out GameplayEntityId target) || target.Index > _runtime.InstanceCount || ReadI32(s, 52) <= 0
                || (enabled != 0 && enabled != 1) || !TryReadOptionalEntity(s, 64, out GameplayEntityId subject)
                || !TryReadOptionalEntity(s, 72, out GameplayEntityId instigator) || !Contains(subject) || !Contains(instigator))
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.InvalidRecord);
            r = new GameplayTriggerDeferredRecord(new RuntimeFrame(ReadI64(s, 0)), ReadI64(s, 8), (GameplayTriggerInternalDeferredOperationKind)op,
                ReadI64(s, 20), ReadI32(s, 28), new GameplayTriggerActionKindId(ReadI32(s, 32)), new GameplayTriggerBindingId(ReadI32(s, 36)),
                new GameplayTriggerRouteId(ReadI32(s, 40)), new GameplayTriggerInstanceId(target.Index, target.Generation),
                new GameplayTriggerSourceBindingId(ReadI32(s, 52)), enabled != 0, ReadI32(s, 60), subject, instigator,
                ReadI64(s, 80), ReadI64(s, 88), ReadI64(s, 96));
            return GameplayTriggerAuthorityBinaryResult.Succeeded();
        }

        private static void WriteReliable(Span<byte> s, GameplayTriggerReliableOutboxRecord r)
        {
            WriteI64(s, 0, r.OriginalActivationFrame.Value); WriteI64(s, 8, r.OriginalDueFrame.Value); WriteI64(s, 16, r.Sequence); WriteI64(s, 24, r.ActivationId);
            WriteI32(s, 32, r.ActionIndex); WriteI32(s, 36, r.ActionKindId.Value); WriteI32(s, 40, r.OriginBindingId.Value); WriteI32(s, 44, r.OriginRouteId.Value);
            WriteEntity(s, 48, r.OriginInstanceId.Index, r.OriginInstanceId.Generation); WriteI32(s, 56, (int)r.OccurrenceKind); WriteI32(s, 60, r.OccurrenceSignalId);
            WriteI64(s, 64, r.PreviousScalarRaw); WriteI64(s, 72, r.CurrentScalarRaw); WriteI64(s, 80, r.OccurrencePayload0); WriteI64(s, 88, r.OccurrencePayload1);
            WriteI64(s, 96, r.OccurrencePayload2); WriteI64(s, 104, r.OccurrenceOrderingValue); WriteI64(s, 112, r.OccurrencePresenceEpoch); WriteI64(s, 120, r.OccurrencePresenceStartFrame);
            WriteI32(s, 128, r.ResolvedTarget.DomainId); WriteI64(s, 132, r.ResolvedTarget.Value); WriteI32(s, 140, r.ResolvedTarget.Generation);
            WriteEntity(s, 144, r.Owner.Index, r.Owner.Generation); WriteEntity(s, 152, r.Subject.Index, r.Subject.Generation); WriteEntity(s, 160, r.Instigator.Index, r.Instigator.Generation);
            WriteI64(s, 168, r.ActionPayload0); WriteI64(s, 176, r.ActionPayload1); WriteI64(s, 184, r.ActionPayload2);
        }

        private GameplayTriggerAuthorityBinaryResult TryReadReliable(ReadOnlySpan<byte> s, out GameplayTriggerReliableOutboxRecord r)
        {
            r = default;
            int occurrence = ReadI32(s, 56); int domain = ReadI32(s, 128); long targetValue = ReadI64(s, 132); int targetGeneration = ReadI32(s, 140);
            if (ReadI64(s, 0) < 0 || ReadI64(s, 8) < 0 || ReadI64(s, 16) < 1 || ReadI64(s, 24) < 1 || ReadI32(s, 32) < 0
                || ReadI32(s, 36) <= 0 || ReadI32(s, 40) <= 0 || ReadI32(s, 44) <= 0 || !TryReadEntity(s, 48, out GameplayEntityId origin)
                || origin.Index > _runtime.InstanceCount || occurrence <= 0 || (occurrence & ~15) != 0 || domain <= 0 || targetValue <= 0 || targetGeneration < 0
                || !TryReadEntity(s, 144, out GameplayEntityId owner) || !TryReadOptionalEntity(s, 152, out GameplayEntityId subject)
                || !TryReadOptionalEntity(s, 160, out GameplayEntityId instigator) || !Contains(owner) || !Contains(subject) || !Contains(instigator))
                return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.InvalidRecord);
            if (domain == GameplayTriggerTargetRef.GameplayEntityDomainId)
            {
                if (targetValue > int.MaxValue || !Contains(new GameplayEntityId((int)targetValue, targetGeneration)))
                    return GameplayTriggerAuthorityBinaryResult.Failed(GameplayTriggerAuthorityBinaryErrorCode.UnknownEntity);
            }
            r = new GameplayTriggerReliableOutboxRecord(new RuntimeFrame(ReadI64(s, 0)), new RuntimeFrame(ReadI64(s, 8)), ReadI64(s, 16), ReadI64(s, 24),
                ReadI32(s, 32), new GameplayTriggerActionKindId(ReadI32(s, 36)), new GameplayTriggerBindingId(ReadI32(s, 40)), new GameplayTriggerRouteId(ReadI32(s, 44)),
                new GameplayTriggerInstanceId(origin.Index, origin.Generation), (GameplayTriggerOccurrenceKind)occurrence, ReadI32(s, 60), ReadI64(s, 64), ReadI64(s, 72),
                ReadI64(s, 80), ReadI64(s, 88), ReadI64(s, 96), ReadI64(s, 104), ReadI64(s, 112), ReadI64(s, 120),
                new GameplayTriggerTargetRef(domain, targetValue, targetGeneration), owner, subject, instigator, ReadI64(s, 168), ReadI64(s, 176), ReadI64(s, 184));
            return GameplayTriggerAuthorityBinaryResult.Succeeded();
        }

        private bool Contains(GameplayEntityId entity) => entity == default || _entityValidator == null || _entityValidator.Contains(entity);

        private void BuildRelationOrder()
        {
            int count = _runtime.RelationCount;
            for (int i = 0; i < count; i++) _relationOrder[i] = i;
            for (int i = 1; i < count; i++)
            {
                int value = _relationOrder[i]; int j = i - 1;
                while (j >= 0 && CompareRelations(_runtime.GetRelationAuthorityState(_relationOrder[j]), _runtime.GetRelationAuthorityState(value)) > 0)
                { _relationOrder[j + 1] = _relationOrder[j]; j--; }
                _relationOrder[j + 1] = value;
            }
        }

        private void BuildRouteOrder()
        {
            int count = _runtime.RouteStateCount;
            for (int i = 0; i < count; i++) _routeOrder[i] = i;
            for (int i = 1; i < count; i++)
            {
                int value = _routeOrder[i]; int j = i - 1;
                while (j >= 0 && CompareRoutes(_runtime.GetRouteAuthorityState(_routeOrder[j]), _runtime.GetRouteAuthorityState(value)) > 0)
                { _routeOrder[j + 1] = _routeOrder[j]; j--; }
                _routeOrder[j + 1] = value;
            }
        }

        private static int CompareRelations(GameplayTriggerRelationAuthorityState a, GameplayTriggerRelationAuthorityState b)
        { int c = a.InstanceId.Index.CompareTo(b.InstanceId.Index); if (c != 0) return c; c = a.Subject.Index.CompareTo(b.Subject.Index); return c != 0 ? c : a.Subject.Generation.CompareTo(b.Subject.Generation); }
        private static int CompareRoutes(GameplayTriggerRouteAuthorityState a, GameplayTriggerRouteAuthorityState b)
        { int c = a.InstanceId.Index.CompareTo(b.InstanceId.Index); if (c != 0) return c; c = a.RouteId.Value.CompareTo(b.RouteId.Value); if (c != 0) return c; c = a.Subject.Index.CompareTo(b.Subject.Index); if (c != 0) return c; c = a.Subject.Generation.CompareTo(b.Subject.Generation); if (c != 0) return c; c = a.Instigator.Index.CompareTo(b.Instigator.Index); return c != 0 ? c : a.Instigator.Generation.CompareTo(b.Instigator.Generation); }

        private static long HashBytes(ReadOnlySpan<byte> bytes)
        { unchecked { ulong h = 14695981039346656037UL; for (int i = 0; i < bytes.Length; i++) { h ^= bytes[i]; h *= 1099511628211UL; } return (long)h; } }
        private static void WriteEntity(Span<byte> s, int o, int index, int generation) { WriteI32(s, o, index); WriteI32(s, o + 4, generation); }
        private static bool TryReadEntity(ReadOnlySpan<byte> s, int o, out GameplayEntityId e) { int i = ReadI32(s, o); int g = ReadI32(s, o + 4); if (i <= 0 || g <= 0) { e = default; return false; } e = new GameplayEntityId(i, g); return true; }
        private static bool TryReadOptionalEntity(ReadOnlySpan<byte> s, int o, out GameplayEntityId e) { int i = ReadI32(s, o); int g = ReadI32(s, o + 4); if (i == 0 && g == 0) { e = default; return true; } if (i <= 0 || g <= 0) { e = default; return false; } e = new GameplayEntityId(i, g); return true; }
        private static void WriteU16(Span<byte> s, int o, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(o, 2), v);
        private static void WriteU32(Span<byte> s, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(s.Slice(o, 4), v);
        private static void WriteI32(Span<byte> s, int o, int v) => BinaryPrimitives.WriteInt32LittleEndian(s.Slice(o, 4), v);
        private static void WriteI64(Span<byte> s, int o, long v) => BinaryPrimitives.WriteInt64LittleEndian(s.Slice(o, 8), v);
        private static ushort ReadU16(ReadOnlySpan<byte> s, int o) => BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(o, 2));
        private static uint ReadU32(ReadOnlySpan<byte> s, int o) => BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(o, 4));
        private static int ReadI32(ReadOnlySpan<byte> s, int o) => BinaryPrimitives.ReadInt32LittleEndian(s.Slice(o, 4));
        private static long ReadI64(ReadOnlySpan<byte> s, int o) => BinaryPrimitives.ReadInt64LittleEndian(s.Slice(o, 8));
    }
}
