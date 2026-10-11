using System;
using System.Globalization;
using System.IO;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Gameplay.Triggers
{
    /// <summary>Identifies Gameplay Trigger checkpoint work that prevents a quiescent capture.</summary>
    public enum GameplayTriggerCheckpointPendingDetailCode
    {
        /// <summary>No pending Gameplay Trigger work.</summary>
        None = 0,
        /// <summary>At least one submitted input batch is pending evaluation.</summary>
        PendingInputBatches = 1,
        /// <summary>At least one occupancy item is pending evaluation.</summary>
        PendingOccupancyItems = 2,
        /// <summary>At least one scalar item is pending evaluation.</summary>
        PendingScalarItems = 3,
        /// <summary>At least one signal item is pending evaluation.</summary>
        PendingSignalItems = 4,
        /// <summary>At least one internal deferred reservation is unfinished.</summary>
        PendingDeferredReservations = 5,
        /// <summary>Due deferred Signal work awaits Command-phase finalization.</summary>
        PendingDeferredSignalFinalization = 6,
        /// <summary>A reliable target callback is currently in flight.</summary>
        PendingReliableOutboxTransfer = 7
    }

    /// <summary>Identifies stable Gameplay Trigger save-state validation failures.</summary>
    public enum GameplayTriggerSaveStateErrorDetailCode
    {
        /// <summary>No Gameplay Trigger-specific detail.</summary>
        None = 0,
        /// <summary>The Gameplay Trigger module state is missing.</summary>
        MissingModuleState = 1,
        /// <summary>The Gameplay Trigger module state is duplicated.</summary>
        DuplicateModuleState = 2,
        /// <summary>The module schema version does not match.</summary>
        ModuleSchemaMismatch = 3,
        /// <summary>The module custom state is missing.</summary>
        MissingCustomState = 4,
        /// <summary>The custom-state type id does not match.</summary>
        CustomStateTypeMismatch = 5,
        /// <summary>The custom-state schema version does not match.</summary>
        CustomStateSchemaMismatch = 6,
        /// <summary>The payload is not one strict JSON object matching the declared contract.</summary>
        PayloadJsonInvalid = 7,
        /// <summary>The payload schema version does not match.</summary>
        PayloadSchemaMismatch = 8,
        /// <summary>The root save-state document is missing.</summary>
        SaveStateMissing = 9,
        /// <summary>The definition catalog hash does not match.</summary>
        DefinitionCatalogHashMismatch = 101,
        /// <summary>The binding catalog hash does not match.</summary>
        BindingCatalogHashMismatch = 102,
        /// <summary>The composed Gameplay Trigger catalog hash does not match.</summary>
        GameplayTriggerCatalogHashMismatch = 103,
        /// <summary>The maximum input batch option does not match.</summary>
        MaxInputBatchesMismatch = 111,
        /// <summary>The maximum input item option does not match.</summary>
        MaxInputItemsMismatch = 112,
        /// <summary>The maximum relation option does not match.</summary>
        MaxRelationsMismatch = 113,
        /// <summary>The maximum occurrence option does not match.</summary>
        MaxOccurrencesMismatch = 114,
        /// <summary>The maximum candidate option does not match.</summary>
        MaxCandidatesMismatch = 115,
        /// <summary>The maximum route-state option does not match.</summary>
        MaxRouteStatesMismatch = 116,
        /// <summary>The maximum activation option does not match.</summary>
        MaxActivationsMismatch = 117,
        /// <summary>The maximum intent option does not match.</summary>
        MaxIntentsMismatch = 118,
        /// <summary>The maximum prepared-action option does not match.</summary>
        MaxPreparedActionsMismatch = 119,
        /// <summary>The maximum condition-stack option does not match.</summary>
        MaxConditionStackDepthMismatch = 120,
        /// <summary>The maximum deferred-record option does not match.</summary>
        MaxDeferredRecordsMismatch = 121,
        /// <summary>The maximum reliable-outbox option does not match.</summary>
        MaxReliableOutboxRecordsMismatch = 122,
        /// <summary>The next activation id is outside the resumable range.</summary>
        NextActivationIdInvalid = 201,
        /// <summary>The root frame or evaluation marker is invalid.</summary>
        EvaluationStateInvalid = 202,
        /// <summary>The next deferred sequence is outside the resumable range.</summary>
        NextDeferredSequenceInvalid = 203,
        /// <summary>The next reliable-outbox sequence is outside the resumable range.</summary>
        NextReliableOutboxSequenceInvalid = 204,
        /// <summary>The saved instance set does not match the static catalog instance set.</summary>
        InstanceCountMismatch = 210,
        /// <summary>A saved instance identity is duplicated.</summary>
        DuplicateInstance = 211,
        /// <summary>A saved instance identity or catalog identity does not match.</summary>
        InstanceIdentityMismatch = 212,
        /// <summary>A saved instance authority value is invalid.</summary>
        InstanceStateInvalid = 213,
        /// <summary>The saved relation count exceeds configured capacity.</summary>
        RelationCapacityExceeded = 220,
        /// <summary>A saved relation key is duplicated.</summary>
        DuplicateRelation = 221,
        /// <summary>A saved relation references an unknown or incompatible authority object.</summary>
        RelationReferenceInvalid = 222,
        /// <summary>A saved relation authority value is invalid.</summary>
        RelationStateInvalid = 223,
        /// <summary>The saved route-state count exceeds configured capacity.</summary>
        RouteStateCapacityExceeded = 230,
        /// <summary>A saved route-state key is duplicated.</summary>
        DuplicateRouteState = 231,
        /// <summary>A saved route-state references an unknown authority object.</summary>
        RouteReferenceInvalid = 232,
        /// <summary>A saved route-state key does not match its configured state scope.</summary>
        RouteScopeShapeInvalid = 233,
        /// <summary>A saved route-state authority value is invalid.</summary>
        RouteStateInvalid = 234,
        /// <summary>An instance active count does not match its active relations.</summary>
        ActiveCountMismatch = 240,
        /// <summary>The saved deferred-record count exceeds configured capacity.</summary>
        DeferredCapacityExceeded = 250,
        /// <summary>A saved deferred sequence is duplicated.</summary>
        DuplicateDeferredSequence = 251,
        /// <summary>The deferred queue is not in canonical due-frame/sequence order.</summary>
        DeferredOrderInvalid = 252,
        /// <summary>A deferred operation or operation payload is invalid.</summary>
        DeferredRecordInvalid = 253,
        /// <summary>A deferred target does not match the static Trigger instance set.</summary>
        DeferredTargetInvalid = 254,
        /// <summary>A deferred origin does not match a catalog action route.</summary>
        DeferredOriginInvalid = 255,
        /// <summary>The saved reliable-outbox count exceeds configured capacity.</summary>
        ReliableOutboxCapacityExceeded = 260,
        /// <summary>A reliable-outbox sequence is duplicated.</summary>
        DuplicateReliableOutboxSequence = 261,
        /// <summary>A reliable-outbox activation/action origin is duplicated.</summary>
        DuplicateReliableOutboxOrigin = 262,
        /// <summary>The reliable outbox is not in canonical due-frame/sequence order.</summary>
        ReliableOutboxOrderInvalid = 263,
        /// <summary>A reliable-outbox record has an invalid structural shape.</summary>
        ReliableOutboxRecordInvalid = 264,
        /// <summary>A reliable-outbox target is invalid or cannot be re-resolved.</summary>
        ReliableOutboxTargetInvalid = 265,
        /// <summary>A reliable-outbox origin does not match a reliable catalog action.</summary>
        ReliableOutboxOriginInvalid = 266,
        /// <summary>A referenced gameplay entity is not valid in the restored world.</summary>
        UnknownEntity = 301,
        /// <summary>The restored-entity validator threw an exception.</summary>
        EntityValidatorException = 302
    }

    /// <summary>Validates Gameplay Trigger entity references against the already restored gameplay world.</summary>
    public interface IGameplayTriggerRestoreEntityValidator
    {
        /// <summary>Returns whether one non-default entity id is valid in the restored world.</summary>
        /// <remarks>NoAlloc. Implementations must not mutate runtime state.</remarks>
        bool IsRestoredEntityValid(GameplayEntityId entityId);
    }

    /// <summary>Captures, restores, and checks quiescence for one Gameplay Trigger runtime.</summary>
    public sealed class GameplayTriggerRuntimeSaveStateParticipant :
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer,
        IRuntimeCheckpointQuiescenceChecker
    {
        /// <summary>Stable registry participant identity.</summary>
        public const string ParticipantId = "mxframework.gameplay.triggers";

        /// <summary>Stable runtime module identity.</summary>
        public const string ModuleId = "mxframework.gameplay.triggers";

        /// <summary>Stable custom-state type identity.</summary>
        public const string TypeId = "mxframework.gameplay.triggers.state";

        /// <summary>Current Gameplay Trigger module and payload schema.</summary>
        public const int CurrentPayloadSchemaVersion = 3;

        /// <summary>Recommended registry order after the gameplay world participant.</summary>
        public const int DefaultSaveStateOrder = 200;

        /// <summary>Recommended quiescence-registry order after the gameplay world participant.</summary>
        public const int DefaultQuiescenceOrder = 200;

        private const string ModulePath = "$.moduleStates[mxframework.gameplay.triggers]";
        private const string PayloadPath = ModulePath + ".customState.payloadJson";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Error,
            Culture = CultureInfo.InvariantCulture
        };

        private readonly GameplayTriggerRuntime _runtime;
        private readonly IGameplayTriggerRestoreEntityValidator _entityValidator;

        /// <summary>Creates a save-state participant for one runtime and explicit restored-world validator.</summary>
        /// <remarks>LowFreqAlloc. Construction allocates the participant object.</remarks>
        public GameplayTriggerRuntimeSaveStateParticipant(
            GameplayTriggerRuntime runtime,
            IGameplayTriggerRestoreEntityValidator entityValidator)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _entityValidator = entityValidator ?? throw new ArgumentNullException(nameof(entityValidator));
        }

        /// <summary>Checks whether the runtime has no submitted trigger input awaiting evaluation.</summary>
        /// <remarks>NoAlloc. The context frame is intentionally not treated as trigger frame authority.</remarks>
        public RuntimeCheckpointQuiescenceResult CheckQuiescence(in RuntimeCheckpointContext context)
        {
            GameplayTriggerCheckpointPendingDetailCode detail = _runtime.ClassifyCheckpointPendingWork();
            return detail == GameplayTriggerCheckpointPendingDetailCode.None
                ? RuntimeCheckpointQuiescenceResult.Ready()
                : RuntimeCheckpointQuiescenceResult.Pending((int)detail);
        }

        /// <summary>Captures one canonical Gameplay Trigger authority payload.</summary>
        /// <remarks>
        /// AllocByDesign. Capture allocates DTO arrays, JSON, and the save-state document. Direct capture rejects
        /// pending work but is not RuntimeHost/frame authority; use the checkpoint barrier for authoritative capture.
        /// </remarks>
        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            GameplayTriggerCheckpointPendingDetailCode pending = _runtime.ClassifyCheckpointPendingWork();
            if (pending != GameplayTriggerCheckpointPendingDetailCode.None)
            {
                return Failed<RuntimeSaveState>(
                    RuntimeSaveStateErrorCode.PendingWork,
                    "$",
                    "Gameplay Trigger runtime has submitted input awaiting evaluation.",
                    (int)pending);
            }

            if (_runtime.NextActivationId < 1L || _runtime.NextActivationId >= long.MaxValue)
            {
                return Failed<RuntimeSaveState>(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    PayloadPath + ".nextActivationId",
                    "Gameplay Trigger next activation id is outside the resumable range.",
                    (int)GameplayTriggerSaveStateErrorDetailCode.NextActivationIdInvalid);
            }
            if (_runtime.NextDeferredSequence < 1L)
            {
                return Failed<RuntimeSaveState>(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    PayloadPath + ".nextDeferredSequence",
                    "Gameplay Trigger next deferred sequence is outside the resumable range.",
                    (int)GameplayTriggerSaveStateErrorDetailCode.NextDeferredSequenceInvalid);
            }
            if (_runtime.NextReliableOutboxSequence < 1L)
            {
                return Failed<RuntimeSaveState>(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    PayloadPath + ".nextReliableOutboxSequence",
                    "Gameplay Trigger next reliable-outbox sequence is outside the resumable range.",
                    (int)GameplayTriggerSaveStateErrorDetailCode.NextReliableOutboxSequenceInvalid);
            }

            long frame = _runtime.AuthorityHasEvaluated ? _runtime.AuthorityLastEvaluatedFrame.Value : 0L;
            if (frame < 0L || frame >= long.MaxValue)
            {
                return Failed<RuntimeSaveState>(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.frame",
                    "Gameplay Trigger capture frame is outside the resumable range.",
                    (int)GameplayTriggerSaveStateErrorDetailCode.EvaluationStateInvalid);
            }

            TriggerPayload payload = CapturePayload();
            string payloadJson = JsonConvert.SerializeObject(payload, JsonSettings);
            var moduleState = new RuntimeModuleSaveState(
                ModuleId,
                CurrentPayloadSchemaVersion,
                new RuntimeCustomState(3314, TypeId, CurrentPayloadSchemaVersion, payloadJson));
            var saveState = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                string.Empty,
                string.Empty,
                string.Empty,
                frame,
                null,
                null,
                new[] { moduleState },
                null);
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(saveState);
        }

        /// <summary>Restores one fully validated Gameplay Trigger authority payload.</summary>
        /// <remarks>
        /// AllocByDesign. Restore is an exclusive bootstrap operation: do not call Tick or expose concurrent readers.
        /// Atomicity covers this participant's single-threaded validate-plan-commit boundary only; it is not lock-free,
        /// thread-safe, or a rollback coordinator across other participants.
        /// </remarks>
        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            if (saveState == null)
            {
                return Failed<bool>(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$",
                    "Save state is null.",
                    (int)GameplayTriggerSaveStateErrorDetailCode.SaveStateMissing);
            }
            if (saveState.Frame < 0L || saveState.Frame >= long.MaxValue)
            {
                return Failed<bool>(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.frame",
                    "Root save-state frame is outside the resumable range.",
                    (int)GameplayTriggerSaveStateErrorDetailCode.EvaluationStateInvalid);
            }

            RuntimeSaveStateResult<RuntimeModuleSaveState> moduleResult = FindModule(saveState);
            if (!moduleResult.Success) return RuntimeSaveStateResult<bool>.Failed(moduleResult.Error);
            RuntimeModuleSaveState module = moduleResult.Value;
            RuntimeSaveStateResult<TriggerPayload> payloadResult = LoadPayload(module);
            if (!payloadResult.Success) return RuntimeSaveStateResult<bool>.Failed(payloadResult.Error);

            RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan> planResult =
                ValidateAndCreatePlan(payloadResult.Value, saveState.Frame);
            if (!planResult.Success) return RuntimeSaveStateResult<bool>.Failed(planResult.Error);

            _runtime.CommitRestorePlan(planResult.Value);
            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private TriggerPayload CapturePayload()
        {
            GameplayTriggerCatalog catalog = _runtime.AuthorityCatalog;
            GameplayTriggerRuntimeOptions options = _runtime.AuthorityOptions;
            var payload = new TriggerPayload
            {
                SchemaVersion = CurrentPayloadSchemaVersion,
                DefinitionCatalogHash = catalog.DefinitionCatalogHash,
                BindingCatalogHash = catalog.BindingCatalogHash,
                GameplayTriggerCatalogHash = catalog.GameplayTriggerCatalogHash,
                Options = OptionsPayload.From(options),
                NextActivationId = _runtime.NextActivationId,
                NextDeferredSequence = _runtime.NextDeferredSequence,
                NextReliableOutboxSequence = _runtime.NextReliableOutboxSequence,
                HasEvaluated = _runtime.AuthorityHasEvaluated,
                LastEvaluatedFrame = _runtime.AuthorityLastEvaluatedFrame.Value,
                Instances = new InstancePayload[_runtime.InstanceCount],
                Relations = new RelationPayload[_runtime.RelationCount],
                RouteStates = new RoutePayload[_runtime.RouteStateCount],
                DeferredRecords = new DeferredPayload[_runtime.DeferredRecordCount],
                ReliableOutboxRecords =
                    new ReliableOutboxPayload[_runtime.ReliableOutboxRecordCount]
            };

            for (int i = 0; i < payload.Instances.Length; i++)
                payload.Instances[i] = InstancePayload.From(_runtime.GetInstanceAuthorityState(i));
            for (int i = 0; i < payload.Relations.Length; i++)
                payload.Relations[i] = RelationPayload.From(_runtime.GetRelationAuthorityState(i));
            for (int i = 0; i < payload.RouteStates.Length; i++)
                payload.RouteStates[i] = RoutePayload.From(_runtime.GetRouteAuthorityState(i));
            for (int i = 0; i < payload.DeferredRecords.Length; i++)
                payload.DeferredRecords[i] = DeferredPayload.From(_runtime.GetDeferredRecord(i));
            for (int i = 0; i < payload.ReliableOutboxRecords.Length; i++)
                payload.ReliableOutboxRecords[i] =
                    ReliableOutboxPayload.From(_runtime.GetReliableOutboxRecord(i));

            Array.Sort(payload.Instances, CompareInstances);
            Array.Sort(payload.Relations, CompareRelations);
            Array.Sort(payload.RouteStates, CompareRoutes);
            return payload;
        }

        private static RuntimeSaveStateResult<RuntimeModuleSaveState> FindModule(RuntimeSaveState saveState)
        {
            RuntimeModuleSaveState found = null;
            int count = 0;
            for (int i = 0; i < saveState.ModuleStates.Count; i++)
            {
                RuntimeModuleSaveState candidate = saveState.ModuleStates[i];
                if (candidate == null || !string.Equals(candidate.ModuleId, ModuleId, StringComparison.Ordinal)) continue;
                found = candidate;
                count++;
            }
            if (count == 0)
                return Failed<RuntimeModuleSaveState>(RuntimeSaveStateErrorCode.CustomStateMismatch, ModulePath, "Gameplay Trigger module state is missing.", (int)GameplayTriggerSaveStateErrorDetailCode.MissingModuleState);
            if (count > 1)
                return Failed<RuntimeModuleSaveState>(RuntimeSaveStateErrorCode.CustomStateMismatch, ModulePath, "Gameplay Trigger module state is duplicated.", (int)GameplayTriggerSaveStateErrorDetailCode.DuplicateModuleState);
            if (found.SchemaVersion != 1 &&
                found.SchemaVersion != 2 &&
                found.SchemaVersion != CurrentPayloadSchemaVersion)
                return Failed<RuntimeModuleSaveState>(RuntimeSaveStateErrorCode.CustomStateMismatch, ModulePath + ".schemaVersion", "Gameplay Trigger module schema does not match.", (int)GameplayTriggerSaveStateErrorDetailCode.ModuleSchemaMismatch, found.SchemaVersion);
            if (found.CustomState == null)
                return Failed<RuntimeModuleSaveState>(RuntimeSaveStateErrorCode.CustomStateMismatch, ModulePath + ".customState", "Gameplay Trigger custom state is missing.", (int)GameplayTriggerSaveStateErrorDetailCode.MissingCustomState);
            if (!string.Equals(found.CustomState.TypeId, TypeId, StringComparison.Ordinal))
                return Failed<RuntimeModuleSaveState>(RuntimeSaveStateErrorCode.CustomStateMismatch, ModulePath + ".customState.typeId", "Gameplay Trigger custom-state type does not match.", (int)GameplayTriggerSaveStateErrorDetailCode.CustomStateTypeMismatch);
            if (found.CustomState.SchemaVersion != found.SchemaVersion)
                return Failed<RuntimeModuleSaveState>(RuntimeSaveStateErrorCode.CustomStateMismatch, ModulePath + ".customState.schemaVersion", "Gameplay Trigger custom-state schema does not match.", (int)GameplayTriggerSaveStateErrorDetailCode.CustomStateSchemaMismatch, found.CustomState.SchemaVersion);
            return RuntimeSaveStateResult<RuntimeModuleSaveState>.Succeeded(found);
        }

        private static RuntimeSaveStateResult<TriggerPayload> LoadPayload(RuntimeModuleSaveState module)
        {
            try
            {
                using (var stringReader = new StringReader(module.CustomState.PayloadJson))
                using (var reader = new JsonTextReader(stringReader) { DateParseHandling = DateParseHandling.None })
                {
                    if (!reader.Read() || reader.TokenType == JsonToken.Comment || reader.TokenType != JsonToken.StartObject)
                        throw new JsonReaderException("Payload must contain exactly one root object.");

                    int depth = 1;
                    while (depth > 0 && reader.Read())
                    {
                        if (reader.TokenType == JsonToken.Comment)
                            throw new JsonReaderException("JSON comments are not accepted.");
                        if (reader.TokenType == JsonToken.StartObject || reader.TokenType == JsonToken.StartArray) depth++;
                        if (reader.TokenType == JsonToken.EndObject || reader.TokenType == JsonToken.EndArray) depth--;
                    }
                    if (depth != 0) throw new JsonReaderException("JSON root object is incomplete.");
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.Comment)
                            throw new JsonReaderException("JSON comments are not accepted.");
                        throw new JsonReaderException("Trailing JSON tokens are not accepted.");
                    }
                }

                JToken token;
                using (var stringReader = new StringReader(module.CustomState.PayloadJson))
                using (var reader = new JsonTextReader(stringReader) { DateParseHandling = DateParseHandling.None })
                {
                    token = JToken.Load(reader, new JsonLoadSettings
                    {
                        CommentHandling = CommentHandling.Ignore,
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        LineInfoHandling = LineInfoHandling.Load
                    });
                }
                JsonSerializer serializer = JsonSerializer.Create(JsonSettings);
                TriggerPayload payload = token.ToObject<TriggerPayload>(serializer);
                if (payload == null) throw new JsonSerializationException("Payload parsed to null.");
                if (payload.SchemaVersion != module.SchemaVersion)
                    return Failed<TriggerPayload>(RuntimeSaveStateErrorCode.CustomStateMismatch, PayloadPath + ".schemaVersion", "Gameplay Trigger payload schema does not match.", (int)GameplayTriggerSaveStateErrorDetailCode.PayloadSchemaMismatch, payload.SchemaVersion);
                if (payload.SchemaVersion == 1)
                {
                    var root = (JObject)token;
                    var options = (JObject)root["options"];
                    if (options.Property("maxDeferredRecords") != null ||
                        root.Property("nextDeferredSequence") != null ||
                        root.Property("deferredRecords") != null ||
                        options.Property("maxReliableOutboxRecords") != null ||
                        root.Property("nextReliableOutboxSequence") != null ||
                        root.Property("reliableOutboxRecords") != null)
                    {
                        return Failed<TriggerPayload>(RuntimeSaveStateErrorCode.CustomStateMismatch, PayloadPath, "Gameplay Trigger v1 payload contains v2 authority fields.", (int)GameplayTriggerSaveStateErrorDetailCode.PayloadSchemaMismatch, payload.SchemaVersion);
                    }
                }
                else if (payload.SchemaVersion == 2)
                {
                    var root = (JObject)token;
                    var options = (JObject)root["options"];
                    if (!payload.Options.MaxDeferredRecords.HasValue ||
                        !payload.NextDeferredSequence.HasValue ||
                        payload.DeferredRecords == null)
                        return Failed<TriggerPayload>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath, "Gameplay Trigger v2 payload is missing deferred authority fields.", (int)GameplayTriggerSaveStateErrorDetailCode.PayloadJsonInvalid, payload.SchemaVersion);
                    if (options.Property("maxReliableOutboxRecords") != null ||
                        root.Property("nextReliableOutboxSequence") != null ||
                        root.Property("reliableOutboxRecords") != null)
                        return Failed<TriggerPayload>(RuntimeSaveStateErrorCode.CustomStateMismatch, PayloadPath, "Gameplay Trigger v2 payload contains v3 authority fields.", (int)GameplayTriggerSaveStateErrorDetailCode.PayloadSchemaMismatch, payload.SchemaVersion);
                }
                else if (!payload.Options.MaxDeferredRecords.HasValue ||
                         !payload.Options.MaxReliableOutboxRecords.HasValue ||
                         !payload.NextDeferredSequence.HasValue ||
                         !payload.NextReliableOutboxSequence.HasValue ||
                         payload.DeferredRecords == null ||
                         payload.ReliableOutboxRecords == null)
                {
                    return Failed<TriggerPayload>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath, "Gameplay Trigger v3 payload is missing reliable authority fields.", (int)GameplayTriggerSaveStateErrorDetailCode.PayloadJsonInvalid, payload.SchemaVersion);
                }
                return RuntimeSaveStateResult<TriggerPayload>.Succeeded(payload);
            }
            catch (Exception exception) when (
                exception is JsonReaderException ||
                exception is JsonSerializationException ||
                exception is OverflowException ||
                exception is FormatException)
            {
                return Failed<TriggerPayload>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath, "Gameplay Trigger payload JSON is invalid.", (int)GameplayTriggerSaveStateErrorDetailCode.PayloadJsonInvalid, -1, exception);
            }
        }

        private RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan> ValidateAndCreatePlan(
            TriggerPayload payload,
            long rootFrame)
        {
            RuntimeSaveStateResult<bool> header = ValidateHeader(payload, rootFrame);
            if (!header.Success) return RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan>.Failed(header.Error);

            RuntimeSaveStateResult<GameplayTriggerInstanceAuthorityState[]> instancesResult = ValidateInstances(
                payload.Instances,
                payload.HasEvaluated);
            if (!instancesResult.Success) return RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan>.Failed(instancesResult.Error);
            GameplayTriggerInstanceAuthorityState[] instances = instancesResult.Value;

            RuntimeSaveStateResult<GameplayTriggerRelationAuthorityState[]> relationsResult = ValidateRelations(payload.Relations, payload.HasEvaluated, payload.LastEvaluatedFrame, instances);
            if (!relationsResult.Success) return RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan>.Failed(relationsResult.Error);
            GameplayTriggerRelationAuthorityState[] relations = relationsResult.Value;

            RuntimeSaveStateResult<GameplayTriggerRouteAuthorityState[]> routesResult = ValidateRoutes(payload.RouteStates, payload, instances, relations);
            if (!routesResult.Success) return RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan>.Failed(routesResult.Error);
            GameplayTriggerRouteAuthorityState[] routes = routesResult.Value;

            RuntimeSaveStateResult<GameplayTriggerDeferredRecord[]> deferredResult =
                ValidateDeferredRecords(
                    payload.SchemaVersion == 1
                        ? Array.Empty<DeferredPayload>()
                        : payload.DeferredRecords,
                    payload.SchemaVersion == 1
                        ? 1L
                        : payload.NextDeferredSequence.Value,
                    payload.NextActivationId,
                    payload.HasEvaluated,
                    payload.LastEvaluatedFrame);
            if (!deferredResult.Success)
                return RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan>.Failed(
                    deferredResult.Error);
            GameplayTriggerDeferredRecord[] deferredRecords = deferredResult.Value;

            RuntimeSaveStateResult<GameplayTriggerReliableOutboxRecord[]> reliableResult =
                ValidateReliableOutboxRecords(
                    payload.SchemaVersion < 3
                        ? Array.Empty<ReliableOutboxPayload>()
                        : payload.ReliableOutboxRecords,
                    payload.SchemaVersion < 3
                        ? 1L
                        : payload.NextReliableOutboxSequence.Value,
                    payload.NextActivationId,
                    payload.HasEvaluated,
                    payload.LastEvaluatedFrame);
            if (!reliableResult.Success)
                return RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan>.Failed(
                    reliableResult.Error);
            GameplayTriggerReliableOutboxRecord[] reliableOutboxRecords = reliableResult.Value;

            RuntimeSaveStateResult<bool> counts = ValidateActiveCounts(instances, relations);
            if (!counts.Success) return RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan>.Failed(counts.Error);
            RuntimeSaveStateResult<bool> entities = ValidateEntities(instances, relations, routes);
            if (!entities.Success) return RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan>.Failed(entities.Error);
            RuntimeSaveStateResult<bool> deferredEntities = ValidateDeferredEntities(deferredRecords);
            if (!deferredEntities.Success) return RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan>.Failed(deferredEntities.Error);
            RuntimeSaveStateResult<bool> reliableEntities =
                ValidateReliableOutboxEntities(reliableOutboxRecords);
            if (!reliableEntities.Success)
                return RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan>.Failed(
                    reliableEntities.Error);

            GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan plan = _runtime.CreateRestorePlan(
                payload.NextActivationId,
                payload.SchemaVersion == 1 ? 1L : payload.NextDeferredSequence.Value,
                payload.SchemaVersion < 3 ? 1L : payload.NextReliableOutboxSequence.Value,
                payload.HasEvaluated,
                new RuntimeFrame(payload.LastEvaluatedFrame),
                instances,
                relations,
                routes,
                deferredRecords,
                reliableOutboxRecords);
            return RuntimeSaveStateResult<GameplayTriggerRuntime.GameplayTriggerRuntimeRestorePlan>.Succeeded(plan);
        }

        private RuntimeSaveStateResult<bool> ValidateHeader(TriggerPayload payload, long rootFrame)
        {
            GameplayTriggerCatalog catalog = _runtime.AuthorityCatalog;
            GameplayTriggerRuntimeOptions options = _runtime.AuthorityOptions;
            bool legacyV1 = payload.SchemaVersion == 1;
            bool hasReliableAuthority = payload.SchemaVersion >= 3;
            if (legacyV1 && !catalog.HasLegacyV1CatalogIdentity)
                return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.DefinitionCatalogHashMismatch, ".definitionCatalogHash");
            long expectedDefinitionHash = legacyV1
                ? catalog.LegacyV1DefinitionCatalogHash
                : catalog.DefinitionCatalogHash;
            long expectedBindingHash = legacyV1
                ? catalog.LegacyV1BindingCatalogHash
                : catalog.BindingCatalogHash;
            long expectedCombinedHash = legacyV1
                ? catalog.LegacyV1GameplayTriggerCatalogHash
                : catalog.GameplayTriggerCatalogHash;
            if (payload.DefinitionCatalogHash != expectedDefinitionHash) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.DefinitionCatalogHashMismatch, ".definitionCatalogHash");
            if (payload.BindingCatalogHash != expectedBindingHash) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.BindingCatalogHashMismatch, ".bindingCatalogHash");
            if (payload.GameplayTriggerCatalogHash != expectedCombinedHash) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.GameplayTriggerCatalogHashMismatch, ".gameplayTriggerCatalogHash");
            if (payload.Options.MaxInputBatches != options.MaxInputBatches) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxInputBatchesMismatch, ".options.maxInputBatches");
            if (payload.Options.MaxInputItems != options.MaxInputItems) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxInputItemsMismatch, ".options.maxInputItems");
            if (payload.Options.MaxRelations != options.MaxRelations) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxRelationsMismatch, ".options.maxRelations");
            if (payload.Options.MaxOccurrences != options.MaxOccurrences) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxOccurrencesMismatch, ".options.maxOccurrences");
            if (payload.Options.MaxCandidates != options.MaxCandidates) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxCandidatesMismatch, ".options.maxCandidates");
            if (payload.Options.MaxRouteStates != options.MaxRouteStates) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxRouteStatesMismatch, ".options.maxRouteStates");
            if (payload.Options.MaxActivations != options.MaxActivations) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxActivationsMismatch, ".options.maxActivations");
            if (payload.Options.MaxIntents != options.MaxIntents) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxIntentsMismatch, ".options.maxIntents");
            if (payload.Options.MaxPreparedActions != options.MaxPreparedActions) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxPreparedActionsMismatch, ".options.maxPreparedActions");
            if (payload.Options.MaxConditionStackDepth != options.MaxConditionStackDepth) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxConditionStackDepthMismatch, ".options.maxConditionStackDepth");
            if (!legacyV1 && payload.Options.MaxDeferredRecords.Value != options.MaxDeferredRecords) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxDeferredRecordsMismatch, ".options.maxDeferredRecords");
            if (hasReliableAuthority && payload.Options.MaxReliableOutboxRecords.Value != options.MaxReliableOutboxRecords) return MissingConfig(GameplayTriggerSaveStateErrorDetailCode.MaxReliableOutboxRecordsMismatch, ".options.maxReliableOutboxRecords");
            if (payload.NextActivationId < 1L || payload.NextActivationId >= long.MaxValue)
                return Invalid(GameplayTriggerSaveStateErrorDetailCode.NextActivationIdInvalid, ".nextActivationId");
            long nextDeferredSequence = legacyV1 ? 1L : payload.NextDeferredSequence.Value;
            if (nextDeferredSequence < 1L)
                return Invalid(GameplayTriggerSaveStateErrorDetailCode.NextDeferredSequenceInvalid, ".nextDeferredSequence");
            long nextReliableOutboxSequence = hasReliableAuthority
                ? payload.NextReliableOutboxSequence.Value
                : 1L;
            if (nextReliableOutboxSequence < 1L)
                return Invalid(GameplayTriggerSaveStateErrorDetailCode.NextReliableOutboxSequenceInvalid, ".nextReliableOutboxSequence");
            if (payload.LastEvaluatedFrame < 0L || payload.LastEvaluatedFrame >= long.MaxValue || payload.LastEvaluatedFrame > rootFrame)
                return Invalid(GameplayTriggerSaveStateErrorDetailCode.EvaluationStateInvalid, ".lastEvaluatedFrame");
            if (!payload.HasEvaluated && (payload.LastEvaluatedFrame != 0L || payload.NextActivationId != 1L || nextDeferredSequence != 1L || nextReliableOutboxSequence != 1L || payload.Relations.Length != 0 || payload.RouteStates.Length != 0 || (!legacyV1 && payload.DeferredRecords.Length != 0) || (hasReliableAuthority && payload.ReliableOutboxRecords.Length != 0)))
                return Invalid(GameplayTriggerSaveStateErrorDetailCode.EvaluationStateInvalid, ".hasEvaluated");
            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private RuntimeSaveStateResult<GameplayTriggerInstanceAuthorityState[]> ValidateInstances(
            InstancePayload[] saved,
            bool hasEvaluated)
        {
            if (saved.Length != _runtime.InstanceCount)
                return Failed<GameplayTriggerInstanceAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".instances", "Saved instance set does not match the catalog.", (int)GameplayTriggerSaveStateErrorDetailCode.InstanceCountMismatch);
            var result = new GameplayTriggerInstanceAuthorityState[saved.Length];
            var seen = new bool[saved.Length];
            for (int i = 0; i < saved.Length; i++)
            {
                InstancePayload row = saved[i];
                int slot = row.InstanceIndex - 1;
                if (slot < 0 || slot >= result.Length)
                    return Failed<GameplayTriggerInstanceAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".instances[" + i + "]", "Saved instance id is outside the static catalog set.", (int)GameplayTriggerSaveStateErrorDetailCode.InstanceIdentityMismatch);
                if (seen[slot])
                    return Failed<GameplayTriggerInstanceAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".instances[" + i + "]", "Saved instance id is duplicated.", (int)GameplayTriggerSaveStateErrorDetailCode.DuplicateInstance);
                seen[slot] = true;
                GameplayTriggerInstanceAuthorityState expected = _runtime.GetInstanceAuthorityState(slot);
                if (row.InstanceGeneration != expected.InstanceId.Generation || row.BindingId != expected.BindingId.Value || row.SourceBindingId != expected.SourceBindingId.Value || row.DefinitionId != expected.DefinitionId.Value || row.DefinitionVersion != expected.DefinitionVersion || row.SourceDefinitionId != expected.SourceDefinitionId.Value || row.OwnerIndex != expected.Owner.Index || row.OwnerGeneration != expected.Owner.Generation)
                    return Failed<GameplayTriggerInstanceAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".instances[" + i + "]", "Saved instance catalog identity does not match.", (int)GameplayTriggerSaveStateErrorDetailCode.InstanceIdentityMismatch);
                if (row.ActiveCount < 0 ||
                    row.ActiveCount > _runtime.RelationCapacity ||
                    row.AggregatePresenceEpoch < 0L ||
                    (row.ActiveCount > 0 && row.AggregatePresenceEpoch == 0L) ||
                    (!hasEvaluated && (row.ActiveCount != 0 || row.AggregatePresenceEpoch != 0L)))
                    return Failed<GameplayTriggerInstanceAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".instances[" + i + "]", "Saved instance authority values are invalid.", (int)GameplayTriggerSaveStateErrorDetailCode.InstanceStateInvalid);
                result[slot] = new GameplayTriggerInstanceAuthorityState(expected.InstanceId, expected.BindingId, expected.SourceBindingId, expected.DefinitionId, expected.DefinitionVersion, expected.SourceDefinitionId, expected.Owner, row.Enabled, row.ActiveCount, row.AggregatePresenceEpoch);
            }
            return RuntimeSaveStateResult<GameplayTriggerInstanceAuthorityState[]>.Succeeded(result);
        }

        private RuntimeSaveStateResult<GameplayTriggerRelationAuthorityState[]> ValidateRelations(RelationPayload[] saved, bool hasEvaluated, long lastFrame, GameplayTriggerInstanceAuthorityState[] instances)
        {
            if (saved.Length > _runtime.RelationCapacity)
                return Failed<GameplayTriggerRelationAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".relations", "Saved relations exceed configured capacity.", (int)GameplayTriggerSaveStateErrorDetailCode.RelationCapacityExceeded);
            var sorted = new RelationPayload[saved.Length];
            Array.Copy(saved, sorted, saved.Length);
            Array.Sort(sorted, CompareRelations);
            var result = new GameplayTriggerRelationAuthorityState[sorted.Length];
            for (int i = 0; i < sorted.Length; i++)
            {
                RelationPayload row = sorted[i];
                if (i > 0 && CompareRelations(sorted[i - 1], row) == 0)
                    return Failed<GameplayTriggerRelationAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".relations[" + i + "]", "Saved relation key is duplicated.", (int)GameplayTriggerSaveStateErrorDetailCode.DuplicateRelation);
                int slot = row.InstanceIndex - 1;
                if (slot < 0 || slot >= instances.Length || row.InstanceGeneration != instances[slot].InstanceId.Generation)
                    return Failed<GameplayTriggerRelationAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".relations[" + i + "]", "Saved relation references an unknown instance.", (int)GameplayTriggerSaveStateErrorDetailCode.RelationReferenceInvalid);
                GameplayTriggerSourceDefinition source = GetSource(slot);
                if (!TryEntity(
                    row.SubjectIndex,
                    row.SubjectGeneration,
                    source.SourceKind == GameplayTriggerSourceKind.Occupancy,
                    out GameplayEntityId subject))
                    return Failed<GameplayTriggerRelationAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".relations[" + i + "].subjectIndex", "Saved relation subject is invalid.", (int)GameplayTriggerSaveStateErrorDetailCode.RelationReferenceInvalid);
                bool shapeValid = source.SourceKind != GameplayTriggerSourceKind.Signal;
                if (source.SourceKind == GameplayTriggerSourceKind.Occupancy) shapeValid &= !row.Primed && row.ScalarRawValue == 0L;
                if (source.SourceKind == GameplayTriggerSourceKind.Threshold) shapeValid &= row.Primed || !row.Active;
                if (!shapeValid)
                    return Failed<GameplayTriggerRelationAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".relations[" + i + "]", "Saved relation is incompatible with its source kind.", (int)GameplayTriggerSaveStateErrorDetailCode.RelationReferenceInvalid);
                if (row.PresenceEpoch < 0L || row.PresenceStartFrame < 0L || row.PresenceStartFrame >= long.MaxValue || (row.PresenceEpoch == 0L && (row.PresenceStartFrame != 0L || row.Active)) || (row.PresenceEpoch > 0L && (!hasEvaluated || row.PresenceStartFrame > lastFrame)) || (row.Active && row.PresenceEpoch == 0L))
                    return Failed<GameplayTriggerRelationAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".relations[" + i + "]", "Saved relation authority values are invalid.", (int)GameplayTriggerSaveStateErrorDetailCode.RelationStateInvalid);
                result[i] = new GameplayTriggerRelationAuthorityState(instances[slot].InstanceId, subject, row.Active, row.Primed, Fix64.FromRaw(row.ScalarRawValue), row.PresenceEpoch, row.PresenceStartFrame);
            }
            return RuntimeSaveStateResult<GameplayTriggerRelationAuthorityState[]>.Succeeded(result);
        }

        private RuntimeSaveStateResult<GameplayTriggerRouteAuthorityState[]> ValidateRoutes(RoutePayload[] saved, TriggerPayload payload, GameplayTriggerInstanceAuthorityState[] instances, GameplayTriggerRelationAuthorityState[] relations)
        {
            if (saved.Length > _runtime.RouteStateCapacity)
                return Failed<GameplayTriggerRouteAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".routeStates", "Saved route states exceed configured capacity.", (int)GameplayTriggerSaveStateErrorDetailCode.RouteStateCapacityExceeded);
            var sorted = new RoutePayload[saved.Length];
            Array.Copy(saved, sorted, saved.Length);
            Array.Sort(sorted, CompareRoutes);
            var result = new GameplayTriggerRouteAuthorityState[sorted.Length];
            long activationSum = 0L;
            for (int i = 0; i < sorted.Length; i++)
            {
                RoutePayload row = sorted[i];
                if (i > 0 && CompareRoutes(sorted[i - 1], row) == 0)
                    return Failed<GameplayTriggerRouteAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".routeStates[" + i + "]", "Saved route-state key is duplicated.", (int)GameplayTriggerSaveStateErrorDetailCode.DuplicateRouteState);
                int slot = row.InstanceIndex - 1;
                if (slot < 0 || slot >= instances.Length || row.InstanceGeneration != instances[slot].InstanceId.Generation || !TryFindRoute(slot, row.RouteId, out GameplayTriggerRouteDefinition route))
                    return Failed<GameplayTriggerRouteAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".routeStates[" + i + "]", "Saved route state references an unknown instance or route.", (int)GameplayTriggerSaveStateErrorDetailCode.RouteReferenceInvalid);
                if (!TryEntity(row.SubjectIndex, row.SubjectGeneration, false, out GameplayEntityId subject) || !TryEntity(row.InstigatorIndex, row.InstigatorGeneration, false, out GameplayEntityId instigator) || !HasScopeShape(route.ActivationPolicy.StateScope, subject, instigator))
                    return Failed<GameplayTriggerRouteAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".routeStates[" + i + "]", "Saved route-state key does not match its configured scope.", (int)GameplayTriggerSaveStateErrorDetailCode.RouteScopeShapeInvalid);
                GameplayTriggerActivationPolicy policy = route.ActivationPolicy;
                bool valid = row.ActivationCount >= 1 && row.HasActivated && payload.HasEvaluated && row.LastActivationFrame >= 0L && row.LastActivationFrame <= payload.LastEvaluatedFrame && row.LastActivationFrame < long.MaxValue && row.LastActivatedPresenceEpoch >= 0L;
                if (policy.MaxActivations > 0 && row.ActivationCount > policy.MaxActivations) valid = false;
                if (policy.PresenceLimit == GameplayTriggerPresenceLimit.Ignore && row.LastActivatedPresenceEpoch != 0L) valid = false;
                if (policy.PresenceLimit == GameplayTriggerPresenceLimit.OncePerPresence &&
                    (GetSource(slot).SourceKind == GameplayTriggerSourceKind.Signal ||
                     !ValidatePresenceEpoch(slot, policy.StateScope, subject, row.LastActivatedPresenceEpoch, instances, relations))) valid = false;
                if (!row.HasStaySchedule && row.NextStayFrame != 0L) valid = false;
                if (row.HasStaySchedule && ((route.OccurrenceMask & GameplayTriggerOccurrenceKind.Stayed) == 0 || row.NextStayFrame < 0L)) valid = false;
                try { activationSum = checked(activationSum + row.ActivationCount); } catch (OverflowException) { valid = false; }
                if (!valid)
                    return Failed<GameplayTriggerRouteAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".routeStates[" + i + "]", "Saved route-state authority values are invalid.", (int)GameplayTriggerSaveStateErrorDetailCode.RouteStateInvalid);
                result[i] = new GameplayTriggerRouteAuthorityState(instances[slot].InstanceId, new GameplayTriggerRouteId(row.RouteId), subject, instigator, row.ActivationCount, row.HasActivated, row.LastActivationFrame, row.LastActivatedPresenceEpoch, row.HasStaySchedule, row.NextStayFrame);
            }
            if (activationSum > payload.NextActivationId - 1L)
                return Failed<GameplayTriggerRouteAuthorityState[]>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + ".nextActivationId", "Next activation id cannot precede committed route activations.", (int)GameplayTriggerSaveStateErrorDetailCode.NextActivationIdInvalid);
            return RuntimeSaveStateResult<GameplayTriggerRouteAuthorityState[]>.Succeeded(result);
        }

        private RuntimeSaveStateResult<bool> ValidateActiveCounts(GameplayTriggerInstanceAuthorityState[] instances, GameplayTriggerRelationAuthorityState[] relations)
        {
            var activeCounts = new int[instances.Length];
            for (int i = 0; i < relations.Length; i++)
            {
                GameplayTriggerRelationAuthorityState relation = relations[i];
                int slot = relation.InstanceId.Index - 1;
                if (relation.Active) activeCounts[slot]++;
                if (!instances[slot].Enabled && (relation.Active || relation.Primed))
                    return Invalid(GameplayTriggerSaveStateErrorDetailCode.InstanceStateInvalid, ".instances[" + slot + "].enabled");
            }
            for (int i = 0; i < instances.Length; i++)
            {
                if (instances[i].ActiveCount != activeCounts[i] || (!instances[i].Enabled && instances[i].ActiveCount != 0))
                    return Invalid(GameplayTriggerSaveStateErrorDetailCode.ActiveCountMismatch, ".instances[" + i + "].activeCount");
            }
            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private RuntimeSaveStateResult<GameplayTriggerDeferredRecord[]> ValidateDeferredRecords(
            DeferredPayload[] saved,
            long nextDeferredSequence,
            long nextActivationId,
            bool hasEvaluated,
            long lastEvaluatedFrame)
        {
            if (saved.Length > _runtime.AuthorityOptions.MaxDeferredRecords)
            {
                return Failed<GameplayTriggerDeferredRecord[]>(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    PayloadPath + ".deferredRecords",
                    "Saved deferred record count exceeds configured capacity.",
                    (int)GameplayTriggerSaveStateErrorDetailCode.DeferredCapacityExceeded);
            }

            var records = new GameplayTriggerDeferredRecord[saved.Length];
            long maximumDueFrame = lastEvaluatedFrame == long.MaxValue
                ? long.MaxValue
                : lastEvaluatedFrame + 1L;
            long previousDueFrame = -1L;
            long previousSequence = -1L;
            for (int i = 0; i < saved.Length; i++)
            {
                DeferredPayload row = saved[i];
                if (row.OriginalDueFrame < 0L ||
                    !hasEvaluated ||
                    row.OriginalDueFrame > maximumDueFrame ||
                    row.Sequence <= 0L ||
                    row.Sequence >= nextDeferredSequence)
                {
                    return InvalidDeferred(
                        GameplayTriggerSaveStateErrorDetailCode.DeferredRecordInvalid,
                        i);
                }
                for (int prior = 0; prior < i; prior++)
                {
                    if (saved[prior].Sequence != row.Sequence) continue;
                    return InvalidDeferred(
                        GameplayTriggerSaveStateErrorDetailCode.DuplicateDeferredSequence,
                        i);
                }
                if (row.OriginalDueFrame < previousDueFrame ||
                    (row.OriginalDueFrame == previousDueFrame &&
                        row.Sequence <= previousSequence))
                {
                    return InvalidDeferred(
                        GameplayTriggerSaveStateErrorDetailCode.DeferredOrderInvalid,
                        i);
                }
                previousDueFrame = row.OriginalDueFrame;
                previousSequence = row.Sequence;

                if (row.ActivationId <= 0L ||
                    row.ActivationId >= nextActivationId ||
                    row.ActionIndex < 0 ||
                    row.ActionKindId <= 0 ||
                    row.OriginBindingId <= 0 ||
                    row.OriginRouteId <= 0)
                {
                    return InvalidDeferred(
                        GameplayTriggerSaveStateErrorDetailCode.DeferredOriginInvalid,
                        i);
                }
                if (row.TargetInstanceIndex <= 0 ||
                    row.TargetInstanceGeneration <= 0 ||
                    row.TargetSourceBindingId <= 0)
                {
                    return InvalidDeferred(
                        GameplayTriggerSaveStateErrorDetailCode.DeferredTargetInvalid,
                        i);
                }

                var actionKindId = new GameplayTriggerActionKindId(row.ActionKindId);
                int sinkIndex = _runtime.AuthorityCatalog.FindSink(actionKindId);
                if (sinkIndex < 0)
                {
                    return InvalidDeferred(
                        GameplayTriggerSaveStateErrorDetailCode.DeferredRecordInvalid,
                        i);
                }
                GameplayTriggerActionSinkEntry descriptor =
                    _runtime.AuthorityCatalog.GetSinkEntry(sinkIndex);
                if (descriptor.DeliveryContract != GameplayTriggerDeliveryContract.InternalDeferred ||
                    (int)descriptor.InternalOperationKind != row.OperationKind)
                {
                    return InvalidDeferred(
                        GameplayTriggerSaveStateErrorDetailCode.DeferredRecordInvalid,
                        i);
                }

                int targetSlot = row.TargetInstanceIndex - 1;
                if (targetSlot < 0 || targetSlot >= _runtime.InstanceCount)
                {
                    return InvalidDeferred(
                        GameplayTriggerSaveStateErrorDetailCode.DeferredTargetInvalid,
                        i);
                }
                GameplayTriggerInstanceAuthorityState target =
                    _runtime.GetInstanceAuthorityState(targetSlot);
                if (target.InstanceId.Generation != row.TargetInstanceGeneration ||
                    target.SourceBindingId.Value != row.TargetSourceBindingId)
                {
                    return InvalidDeferred(
                        GameplayTriggerSaveStateErrorDetailCode.DeferredTargetInvalid,
                        i);
                }

                if (!TryFindDeferredOrigin(
                        row,
                        out GameplayTriggerActionInstruction instruction))
                {
                    return InvalidDeferred(
                        GameplayTriggerSaveStateErrorDetailCode.DeferredOriginInvalid,
                        i);
                }

                bool validShape =
                    TryEntity(row.SubjectIndex, row.SubjectGeneration, false, out _) &&
                    TryEntity(row.InstigatorIndex, row.InstigatorGeneration, false, out _) &&
                    row.Payload0 == instruction.Payload0 &&
                    row.Payload1 == instruction.Payload1 &&
                    row.Payload2 == instruction.Payload2;
                switch (descriptor.InternalOperationKind)
                {
                    case GameplayTriggerInternalDeferredOperationKind.SetEnabled:
                        validShape = validShape && row.SignalId == 0 &&
                            row.Enabled == (instruction.Payload0 != 0L);
                        break;
                    case GameplayTriggerInternalDeferredOperationKind.Reset:
                        validShape = validShape && !row.Enabled && row.SignalId == 0;
                        break;
                    case GameplayTriggerInternalDeferredOperationKind.Signal:
                        validShape = validShape && !row.Enabled &&
                            row.SignalId == descriptor.SignalId &&
                            GetSource(targetSlot).SourceKind == GameplayTriggerSourceKind.Signal;
                        break;
                    default:
                        validShape = false;
                        break;
                }
                if (!validShape)
                {
                    return InvalidDeferred(
                        GameplayTriggerSaveStateErrorDetailCode.DeferredRecordInvalid,
                        i);
                }

                TryEntity(row.SubjectIndex, row.SubjectGeneration, false, out GameplayEntityId subject);
                TryEntity(row.InstigatorIndex, row.InstigatorGeneration, false, out GameplayEntityId instigator);
                records[i] = new GameplayTriggerDeferredRecord(
                    new RuntimeFrame(row.OriginalDueFrame),
                    row.Sequence,
                    descriptor.InternalOperationKind,
                    row.ActivationId,
                    row.ActionIndex,
                    actionKindId,
                    new GameplayTriggerBindingId(row.OriginBindingId),
                    new GameplayTriggerRouteId(row.OriginRouteId),
                    target.InstanceId,
                    target.SourceBindingId,
                    row.Enabled,
                    row.SignalId,
                    subject,
                    instigator,
                    row.Payload0,
                    row.Payload1,
                    row.Payload2);
            }
            return RuntimeSaveStateResult<GameplayTriggerDeferredRecord[]>.Succeeded(records);
        }

        private bool TryFindDeferredOrigin(
            DeferredPayload row,
            out GameplayTriggerActionInstruction instruction)
        {
            for (int instanceIndex = 0; instanceIndex < _runtime.InstanceCount; instanceIndex++)
            {
                GameplayTriggerInstanceAuthorityState state =
                    _runtime.GetInstanceAuthorityState(instanceIndex);
                if (state.BindingId.Value != row.OriginBindingId) continue;
                GameplayTriggerDefinition definition = _runtime.AuthorityCatalog.GetDefinition(
                    _runtime.AuthorityCatalog.FindDefinition(state.DefinitionId));
                for (int routeIndex = 0; routeIndex < definition.RouteCount; routeIndex++)
                {
                    GameplayTriggerRouteDefinition route = definition.GetRoute(routeIndex);
                    if (route.RouteId.Value != row.OriginRouteId ||
                        row.ActionIndex >= route.ActionCount)
                        continue;
                    instruction = route.GetAction(row.ActionIndex);
                    if (instruction.ActionKindId.Value != row.ActionKindId ||
                        instruction.TargetSelector != GameplayTriggerTargetSelector.ExplicitBindingTarget)
                        return false;
                    GameplayTriggerBinding binding = _runtime.AuthorityCatalog.GetBinding(instanceIndex);
                    return binding.TryGetExplicitTarget(
                            instruction.TargetSlotId,
                            out GameplayTriggerTargetRef target) &&
                        target.DomainId == GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId &&
                        target.Generation == 0 &&
                        target.Value == row.TargetSourceBindingId;
                }
                break;
            }
            instruction = default;
            return false;
        }

        private RuntimeSaveStateResult<GameplayTriggerReliableOutboxRecord[]>
            ValidateReliableOutboxRecords(
                ReliableOutboxPayload[] saved,
                long nextSequence,
                long nextActivationId,
                bool hasEvaluated,
                long lastEvaluatedFrame)
        {
            if (saved.Length > _runtime.AuthorityOptions.MaxReliableOutboxRecords)
                return InvalidReliable(
                    GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxCapacityExceeded,
                    -1);

            var records = new GameplayTriggerReliableOutboxRecord[saved.Length];
            long maximumDueFrame = lastEvaluatedFrame == long.MaxValue
                ? long.MaxValue
                : lastEvaluatedFrame + 1L;
            long previousDueFrame = -1L;
            long previousSequence = -1L;
            for (int i = 0; i < saved.Length; i++)
            {
                ReliableOutboxPayload row = saved[i];
                if (!hasEvaluated ||
                    row.OriginalActivationFrame < 0L ||
                    row.OriginalActivationFrame > lastEvaluatedFrame ||
                    row.OriginalActivationFrame == long.MaxValue ||
                    row.OriginalDueFrame != row.OriginalActivationFrame + 1L ||
                    row.OriginalDueFrame > maximumDueFrame ||
                    row.Sequence <= 0L ||
                    row.Sequence >= nextSequence)
                    return InvalidReliable(
                        GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxRecordInvalid,
                        i);

                for (int prior = 0; prior < i; prior++)
                {
                    if (saved[prior].Sequence == row.Sequence)
                        return InvalidReliable(
                            GameplayTriggerSaveStateErrorDetailCode.DuplicateReliableOutboxSequence,
                            i);
                    if (saved[prior].ActivationId == row.ActivationId &&
                        saved[prior].ActionIndex == row.ActionIndex)
                        return InvalidReliable(
                            GameplayTriggerSaveStateErrorDetailCode.DuplicateReliableOutboxOrigin,
                            i);
                }

                if (row.OriginalDueFrame < previousDueFrame ||
                    (row.OriginalDueFrame == previousDueFrame &&
                     row.Sequence <= previousSequence))
                    return InvalidReliable(
                        GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxOrderInvalid,
                        i);
                previousDueFrame = row.OriginalDueFrame;
                previousSequence = row.Sequence;

                if (row.ActivationId <= 0L ||
                    row.ActivationId >= nextActivationId ||
                    row.ActionIndex < 0 ||
                    row.ActionKindId <= 0 ||
                    row.OriginBindingId <= 0 ||
                    row.OriginRouteId <= 0 ||
                    row.OriginInstanceIndex <= 0 ||
                    row.OriginInstanceGeneration <= 0)
                    return InvalidReliable(
                        GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxOriginInvalid,
                        i);

                int instanceSlot = row.OriginInstanceIndex - 1;
                if (instanceSlot < 0 || instanceSlot >= _runtime.InstanceCount)
                    return InvalidReliable(
                        GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxOriginInvalid,
                        i);
                GameplayTriggerInstanceAuthorityState origin =
                    _runtime.GetInstanceAuthorityState(instanceSlot);
                if (origin.InstanceId.Generation != row.OriginInstanceGeneration ||
                    origin.BindingId.Value != row.OriginBindingId ||
                    !TryFindRoute(instanceSlot, row.OriginRouteId, out GameplayTriggerRouteDefinition route) ||
                    row.ActionIndex >= route.ActionCount)
                    return InvalidReliable(
                        GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxOriginInvalid,
                        i);

                GameplayTriggerActionInstruction instruction =
                    route.GetAction(row.ActionIndex);
                var actionKindId = new GameplayTriggerActionKindId(row.ActionKindId);
                int sinkIndex = _runtime.AuthorityCatalog.FindSink(actionKindId);
                if (instruction.ActionKindId != actionKindId ||
                    sinkIndex < 0 ||
                    _runtime.AuthorityCatalog.GetSinkEntry(sinkIndex).DeliveryContract !=
                        GameplayTriggerDeliveryContract.ReliableDeferred ||
                    (route.OccurrenceMask & (GameplayTriggerOccurrenceKind)row.OccurrenceKind) == 0)
                    return InvalidReliable(
                        GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxOriginInvalid,
                        i);

                if (!TryEntity(row.OwnerIndex, row.OwnerGeneration, false, out GameplayEntityId owner) ||
                    !TryEntity(row.SubjectIndex, row.SubjectGeneration, false, out GameplayEntityId subject) ||
                    !TryEntity(row.InstigatorIndex, row.InstigatorGeneration, false, out GameplayEntityId instigator) ||
                    owner != origin.Owner ||
                    row.ActionPayload0 != instruction.Payload0 ||
                    row.ActionPayload1 != instruction.Payload1 ||
                    row.ActionPayload2 != instruction.Payload2 ||
                    !IsReliableOccurrenceShapeValid(
                        GetSource(instanceSlot),
                        row,
                        subject,
                        instigator))
                    return InvalidReliable(
                        GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxRecordInvalid,
                        i);

                if (row.TargetDomainId <= 0 ||
                    row.TargetValue <= 0L ||
                    row.TargetGeneration < 0)
                    return InvalidReliable(
                        GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxTargetInvalid,
                        i);
                var target = new GameplayTriggerTargetRef(
                    row.TargetDomainId,
                    row.TargetValue,
                    row.TargetGeneration);
                GameplayTriggerBinding binding =
                    _runtime.AuthorityCatalog.GetBinding(instanceSlot);
                if (!TryResolveReliableTarget(
                        binding,
                        instruction,
                        subject,
                        instigator,
                        out GameplayTriggerTargetRef resolvedTarget) ||
                    target != resolvedTarget)
                    return InvalidReliable(
                        GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxTargetInvalid,
                        i);

                records[i] = new GameplayTriggerReliableOutboxRecord(
                    new RuntimeFrame(row.OriginalActivationFrame),
                    new RuntimeFrame(row.OriginalDueFrame),
                    row.Sequence,
                    row.ActivationId,
                    row.ActionIndex,
                    actionKindId,
                    origin.BindingId,
                    new GameplayTriggerRouteId(row.OriginRouteId),
                    origin.InstanceId,
                    (GameplayTriggerOccurrenceKind)row.OccurrenceKind,
                    row.OccurrenceSignalId,
                    row.PreviousScalarRaw,
                    row.CurrentScalarRaw,
                    row.OccurrencePayload0,
                    row.OccurrencePayload1,
                    row.OccurrencePayload2,
                    row.OccurrenceOrderingValue,
                    row.OccurrencePresenceEpoch,
                    row.OccurrencePresenceStartFrame,
                    target,
                    owner,
                    subject,
                    instigator,
                    row.ActionPayload0,
                    row.ActionPayload1,
                    row.ActionPayload2);
            }
            return RuntimeSaveStateResult<GameplayTriggerReliableOutboxRecord[]>.Succeeded(records);
        }

        private static bool IsReliableOccurrenceShapeValid(
            GameplayTriggerSourceDefinition source,
            ReliableOutboxPayload row,
            GameplayEntityId subject,
            GameplayEntityId instigator)
        {
            GameplayTriggerOccurrenceKind kind =
                (GameplayTriggerOccurrenceKind)row.OccurrenceKind;
            bool singleKind = kind == GameplayTriggerOccurrenceKind.Entered ||
                kind == GameplayTriggerOccurrenceKind.Stayed ||
                kind == GameplayTriggerOccurrenceKind.Exited ||
                kind == GameplayTriggerOccurrenceKind.Pulsed;
            if (!singleKind || row.OccurrenceOrderingValue < 0L) return false;

            if (source.SourceKind == GameplayTriggerSourceKind.Signal)
            {
                return kind == GameplayTriggerOccurrenceKind.Pulsed &&
                    row.OccurrenceSignalId > 0 &&
                    row.PreviousScalarRaw == 0L &&
                    row.CurrentScalarRaw == 0L &&
                    row.OccurrencePresenceEpoch == 0L &&
                    row.OccurrencePresenceStartFrame == 0L;
            }

            bool common = kind != GameplayTriggerOccurrenceKind.Pulsed &&
                row.OccurrenceSignalId == 0 &&
                !instigator.IsValid &&
                row.OccurrencePayload0 == 0L &&
                row.OccurrencePayload1 == 0L &&
                row.OccurrencePayload2 == 0L &&
                row.OccurrencePresenceEpoch > 0L &&
                row.OccurrencePresenceStartFrame >= 0L &&
                row.OccurrencePresenceStartFrame <= row.OriginalActivationFrame;
            if (!common) return false;
            if (source.SourceKind == GameplayTriggerSourceKind.Occupancy)
                return subject.IsValid &&
                    row.PreviousScalarRaw == 0L &&
                    row.CurrentScalarRaw == 0L;
            return source.SourceKind == GameplayTriggerSourceKind.Threshold;
        }

        private static bool TryResolveReliableTarget(
            GameplayTriggerBinding binding,
            GameplayTriggerActionInstruction instruction,
            GameplayEntityId subject,
            GameplayEntityId instigator,
            out GameplayTriggerTargetRef target)
        {
            switch (instruction.TargetSelector)
            {
                case GameplayTriggerTargetSelector.Owner:
                    target = GameplayTriggerTargetRef.FromGameplayEntity(binding.Owner);
                    return target.IsValid;
                case GameplayTriggerTargetSelector.Subject:
                    target = GameplayTriggerTargetRef.FromGameplayEntity(subject);
                    return target.IsValid;
                case GameplayTriggerTargetSelector.Instigator:
                    target = GameplayTriggerTargetRef.FromGameplayEntity(instigator);
                    return target.IsValid;
                case GameplayTriggerTargetSelector.ExplicitBindingTarget:
                    return binding.TryGetExplicitTarget(instruction.TargetSlotId, out target);
                default:
                    target = default;
                    return false;
            }
        }

        private RuntimeSaveStateResult<bool> ValidateReliableOutboxEntities(
            GameplayTriggerReliableOutboxRecord[] records)
        {
            for (int i = 0; i < records.Length; i++)
            {
                GameplayTriggerReliableOutboxRecord record = records[i];
                if (record.Owner.IsValid &&
                    !ValidateEntity(record.Owner, ".reliableOutboxRecords[" + i + "].ownerIndex", out RuntimeSaveStateResult<bool> ownerFailure))
                    return ownerFailure;
                if (record.Subject.IsValid &&
                    !ValidateEntity(record.Subject, ".reliableOutboxRecords[" + i + "].subjectIndex", out RuntimeSaveStateResult<bool> subjectFailure))
                    return subjectFailure;
                if (record.Instigator.IsValid &&
                    !ValidateEntity(record.Instigator, ".reliableOutboxRecords[" + i + "].instigatorIndex", out RuntimeSaveStateResult<bool> instigatorFailure))
                    return instigatorFailure;
                if (record.ResolvedTarget.DomainId == GameplayTriggerTargetRef.GameplayEntityDomainId)
                {
                    if (record.ResolvedTarget.Value > int.MaxValue ||
                        record.ResolvedTarget.Generation <= 0)
                        return Invalid(
                            GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxTargetInvalid,
                            ".reliableOutboxRecords[" + i + "].targetValue");
                    var entity = new GameplayEntityId(
                        (int)record.ResolvedTarget.Value,
                        record.ResolvedTarget.Generation);
                    if (!ValidateEntity(entity, ".reliableOutboxRecords[" + i + "].targetValue", out RuntimeSaveStateResult<bool> targetFailure))
                        return targetFailure;
                }
            }
            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private static RuntimeSaveStateResult<GameplayTriggerReliableOutboxRecord[]>
            InvalidReliable(
                GameplayTriggerSaveStateErrorDetailCode detail,
                int index) =>
            Failed<GameplayTriggerReliableOutboxRecord[]>(
                RuntimeSaveStateErrorCode.InvalidDocument,
                index < 0
                    ? PayloadPath + ".reliableOutboxRecords"
                    : PayloadPath + ".reliableOutboxRecords[" + index + "]",
                "Gameplay Trigger reliable-outbox authority state is invalid.",
                (int)detail);

        private RuntimeSaveStateResult<bool> ValidateDeferredEntities(
            GameplayTriggerDeferredRecord[] records)
        {
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].Subject.IsValid &&
                    !ValidateEntity(records[i].Subject, ".deferredRecords[" + i + "].subjectIndex", out RuntimeSaveStateResult<bool> subjectFailure))
                    return subjectFailure;
                if (records[i].Instigator.IsValid &&
                    !ValidateEntity(records[i].Instigator, ".deferredRecords[" + i + "].instigatorIndex", out RuntimeSaveStateResult<bool> instigatorFailure))
                    return instigatorFailure;
            }
            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private static RuntimeSaveStateResult<GameplayTriggerDeferredRecord[]> InvalidDeferred(
            GameplayTriggerSaveStateErrorDetailCode detail,
            int index) =>
            Failed<GameplayTriggerDeferredRecord[]>(
                RuntimeSaveStateErrorCode.InvalidDocument,
                PayloadPath + ".deferredRecords[" + index + "]",
                "Gameplay Trigger deferred authority state is invalid.",
                (int)detail);

        private RuntimeSaveStateResult<bool> ValidateEntities(GameplayTriggerInstanceAuthorityState[] instances, GameplayTriggerRelationAuthorityState[] relations, GameplayTriggerRouteAuthorityState[] routes)
        {
            for (int i = 0; i < instances.Length; i++)
                if (instances[i].Owner.IsValid && !ValidateEntity(instances[i].Owner, ".instances[" + i + "].ownerIndex", out RuntimeSaveStateResult<bool> failure)) return failure;
            for (int i = 0; i < relations.Length; i++)
                if (relations[i].Subject.IsValid &&
                    !ValidateEntity(relations[i].Subject, ".relations[" + i + "].subjectIndex", out RuntimeSaveStateResult<bool> failure)) return failure;
            for (int i = 0; i < routes.Length; i++)
            {
                if (routes[i].Subject.IsValid && !ValidateEntity(routes[i].Subject, ".routeStates[" + i + "].subjectIndex", out RuntimeSaveStateResult<bool> subjectFailure)) return subjectFailure;
                if (routes[i].Instigator.IsValid && !ValidateEntity(routes[i].Instigator, ".routeStates[" + i + "].instigatorIndex", out RuntimeSaveStateResult<bool> instigatorFailure)) return instigatorFailure;
            }
            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private bool ValidateEntity(GameplayEntityId entity, string path, out RuntimeSaveStateResult<bool> failure)
        {
            try
            {
                if (_entityValidator.IsRestoredEntityValid(entity))
                {
                    failure = default;
                    return true;
                }
                failure = Failed<bool>(RuntimeSaveStateErrorCode.UnknownEntity, PayloadPath + path, "Gameplay Trigger payload references an unknown restored entity.", (int)GameplayTriggerSaveStateErrorDetailCode.UnknownEntity);
                return false;
            }
            catch (Exception exception)
            {
                failure = Failed<bool>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + path, "Gameplay Trigger restored-entity validator threw an exception.", (int)GameplayTriggerSaveStateErrorDetailCode.EntityValidatorException, -1, exception);
                return false;
            }
        }

        private GameplayTriggerSourceDefinition GetSource(int instanceSlot)
        {
            GameplayTriggerDefinition definition = _runtime.AuthorityCatalog.GetDefinition(_runtime.AuthorityCatalog.FindDefinition(_runtime.GetInstanceAuthorityState(instanceSlot).DefinitionId));
            return _runtime.AuthorityCatalog.GetSource(_runtime.AuthorityCatalog.FindSource(definition.SourceDefinitionId));
        }

        private bool TryFindRoute(int instanceSlot, int routeId, out GameplayTriggerRouteDefinition route)
        {
            GameplayTriggerDefinition definition = _runtime.AuthorityCatalog.GetDefinition(_runtime.AuthorityCatalog.FindDefinition(_runtime.GetInstanceAuthorityState(instanceSlot).DefinitionId));
            for (int i = 0; i < definition.RouteCount; i++)
            {
                GameplayTriggerRouteDefinition candidate = definition.GetRoute(i);
                if (candidate.RouteId.Value != routeId) continue;
                route = candidate;
                return true;
            }
            route = null;
            return false;
        }

        private static bool ValidatePresenceEpoch(int slot, GameplayTriggerStateScope scope, GameplayEntityId subject, long epoch, GameplayTriggerInstanceAuthorityState[] instances, GameplayTriggerRelationAuthorityState[] relations)
        {
            if (epoch <= 0L) return false;
            if (scope == GameplayTriggerStateScope.PerInstance) return instances[slot].AggregatePresenceEpoch > 0L && epoch <= instances[slot].AggregatePresenceEpoch;
            if (scope != GameplayTriggerStateScope.PerSubject) return false;
            for (int i = 0; i < relations.Length; i++)
                if (relations[i].InstanceId == instances[slot].InstanceId && relations[i].Subject == subject)
                    return relations[i].PresenceEpoch > 0L && epoch <= relations[i].PresenceEpoch;
            return false;
        }

        private static bool HasScopeShape(GameplayTriggerStateScope scope, GameplayEntityId subject, GameplayEntityId instigator)
        {
            switch (scope)
            {
                case GameplayTriggerStateScope.PerInstance: return !subject.IsValid && !instigator.IsValid;
                case GameplayTriggerStateScope.PerSubject: return subject.IsValid && !instigator.IsValid;
                case GameplayTriggerStateScope.PerInstigator: return !subject.IsValid && instigator.IsValid;
                case GameplayTriggerStateScope.PerPair: return subject.IsValid && instigator.IsValid;
                default: return false;
            }
        }

        private static bool TryEntity(int index, int generation, bool required, out GameplayEntityId entity)
        {
            entity = default;
            if (index < 0 || generation < 0 || ((index == 0) != (generation == 0))) return false;
            if (required && index == 0) return false;
            entity = new GameplayEntityId(index, generation);
            return true;
        }

        private static int CompareInstances(InstancePayload left, InstancePayload right)
        {
            int value = left.InstanceIndex.CompareTo(right.InstanceIndex);
            return value != 0 ? value : left.InstanceGeneration.CompareTo(right.InstanceGeneration);
        }

        private static int CompareRelations(RelationPayload left, RelationPayload right)
        {
            int value = left.InstanceIndex.CompareTo(right.InstanceIndex);
            if (value != 0) return value;
            value = left.InstanceGeneration.CompareTo(right.InstanceGeneration);
            if (value != 0) return value;
            value = left.SubjectIndex.CompareTo(right.SubjectIndex);
            return value != 0 ? value : left.SubjectGeneration.CompareTo(right.SubjectGeneration);
        }

        private static int CompareRoutes(RoutePayload left, RoutePayload right)
        {
            int value = left.InstanceIndex.CompareTo(right.InstanceIndex);
            if (value != 0) return value;
            value = left.InstanceGeneration.CompareTo(right.InstanceGeneration);
            if (value != 0) return value;
            value = left.RouteId.CompareTo(right.RouteId);
            if (value != 0) return value;
            value = left.SubjectIndex.CompareTo(right.SubjectIndex);
            if (value != 0) return value;
            value = left.SubjectGeneration.CompareTo(right.SubjectGeneration);
            if (value != 0) return value;
            value = left.InstigatorIndex.CompareTo(right.InstigatorIndex);
            return value != 0 ? value : left.InstigatorGeneration.CompareTo(right.InstigatorGeneration);
        }

        private static RuntimeSaveStateResult<bool> MissingConfig(GameplayTriggerSaveStateErrorDetailCode detail, string path) =>
            Failed<bool>(RuntimeSaveStateErrorCode.MissingConfig, PayloadPath + path, "Gameplay Trigger runtime configuration does not match the payload.", (int)detail);

        private static RuntimeSaveStateResult<bool> Invalid(GameplayTriggerSaveStateErrorDetailCode detail, string path) =>
            Failed<bool>(RuntimeSaveStateErrorCode.InvalidDocument, PayloadPath + path, "Gameplay Trigger payload authority state is invalid.", (int)detail);

        private static RuntimeSaveStateResult<T> Failed<T>(RuntimeSaveStateErrorCode code, string path, string message, int detail, int sourceSchema = -1, Exception exception = null) =>
            RuntimeSaveStateResult<T>.Failed(new RuntimeSaveStateError(code, path, message, sourceSchema, CurrentPayloadSchemaVersion, exception, detail));

        private sealed class TriggerPayload
        {
            [JsonProperty(Order = 1, Required = Required.Always)] public int SchemaVersion { get; set; }
            [JsonProperty(Order = 2, Required = Required.Always)] public long DefinitionCatalogHash { get; set; }
            [JsonProperty(Order = 3, Required = Required.Always)] public long BindingCatalogHash { get; set; }
            [JsonProperty(Order = 4, Required = Required.Always)] public long GameplayTriggerCatalogHash { get; set; }
            [JsonProperty(Order = 5, Required = Required.Always)] public OptionsPayload Options { get; set; }
            [JsonProperty(Order = 6, Required = Required.Always)] public long NextActivationId { get; set; }
            [JsonProperty(Order = 7, Required = Required.Default)] public long? NextDeferredSequence { get; set; }
            [JsonProperty(Order = 8, Required = Required.Always)] public bool HasEvaluated { get; set; }
            [JsonProperty(Order = 9, Required = Required.Always)] public long LastEvaluatedFrame { get; set; }
            [JsonProperty(Order = 10, Required = Required.Always)] public InstancePayload[] Instances { get; set; }
            [JsonProperty(Order = 11, Required = Required.Always)] public RelationPayload[] Relations { get; set; }
            [JsonProperty(Order = 12, Required = Required.Always)] public RoutePayload[] RouteStates { get; set; }
            [JsonProperty(Order = 13, Required = Required.Default)] public DeferredPayload[] DeferredRecords { get; set; }
            [JsonProperty(Order = 14, Required = Required.Default)] public long? NextReliableOutboxSequence { get; set; }
            [JsonProperty(Order = 15, Required = Required.Default)] public ReliableOutboxPayload[] ReliableOutboxRecords { get; set; }
        }

        private sealed class OptionsPayload
        {
            [JsonProperty(Order = 1, Required = Required.Always)] public int MaxInputBatches { get; set; }
            [JsonProperty(Order = 2, Required = Required.Always)] public int MaxInputItems { get; set; }
            [JsonProperty(Order = 3, Required = Required.Always)] public int MaxRelations { get; set; }
            [JsonProperty(Order = 4, Required = Required.Always)] public int MaxOccurrences { get; set; }
            [JsonProperty(Order = 5, Required = Required.Always)] public int MaxCandidates { get; set; }
            [JsonProperty(Order = 6, Required = Required.Always)] public int MaxRouteStates { get; set; }
            [JsonProperty(Order = 7, Required = Required.Always)] public int MaxActivations { get; set; }
            [JsonProperty(Order = 8, Required = Required.Always)] public int MaxIntents { get; set; }
            [JsonProperty(Order = 9, Required = Required.Always)] public int MaxPreparedActions { get; set; }
            [JsonProperty(Order = 10, Required = Required.Always)] public int MaxConditionStackDepth { get; set; }
            [JsonProperty(Order = 11, Required = Required.Default)] public int? MaxDeferredRecords { get; set; }
            [JsonProperty(Order = 12, Required = Required.Default)] public int? MaxReliableOutboxRecords { get; set; }

            public static OptionsPayload From(GameplayTriggerRuntimeOptions value) => new OptionsPayload
            {
                MaxInputBatches = value.MaxInputBatches,
                MaxInputItems = value.MaxInputItems,
                MaxRelations = value.MaxRelations,
                MaxOccurrences = value.MaxOccurrences,
                MaxCandidates = value.MaxCandidates,
                MaxRouteStates = value.MaxRouteStates,
                MaxActivations = value.MaxActivations,
                MaxIntents = value.MaxIntents,
                MaxPreparedActions = value.MaxPreparedActions,
                MaxConditionStackDepth = value.MaxConditionStackDepth,
                MaxDeferredRecords = value.MaxDeferredRecords,
                MaxReliableOutboxRecords = value.MaxReliableOutboxRecords
            };
        }

        private sealed class ReliableOutboxPayload
        {
            [JsonProperty(Order = 1, Required = Required.Always)] public long OriginalActivationFrame { get; set; }
            [JsonProperty(Order = 2, Required = Required.Always)] public long OriginalDueFrame { get; set; }
            [JsonProperty(Order = 3, Required = Required.Always)] public long Sequence { get; set; }
            [JsonProperty(Order = 4, Required = Required.Always)] public long ActivationId { get; set; }
            [JsonProperty(Order = 5, Required = Required.Always)] public int ActionIndex { get; set; }
            [JsonProperty(Order = 6, Required = Required.Always)] public int ActionKindId { get; set; }
            [JsonProperty(Order = 7, Required = Required.Always)] public int OriginBindingId { get; set; }
            [JsonProperty(Order = 8, Required = Required.Always)] public int OriginRouteId { get; set; }
            [JsonProperty(Order = 9, Required = Required.Always)] public int OriginInstanceIndex { get; set; }
            [JsonProperty(Order = 10, Required = Required.Always)] public int OriginInstanceGeneration { get; set; }
            [JsonProperty(Order = 11, Required = Required.Always)] public int OccurrenceKind { get; set; }
            [JsonProperty(Order = 12, Required = Required.Always)] public int OccurrenceSignalId { get; set; }
            [JsonProperty(Order = 13, Required = Required.Always)] public long PreviousScalarRaw { get; set; }
            [JsonProperty(Order = 14, Required = Required.Always)] public long CurrentScalarRaw { get; set; }
            [JsonProperty(Order = 15, Required = Required.Always)] public long OccurrencePayload0 { get; set; }
            [JsonProperty(Order = 16, Required = Required.Always)] public long OccurrencePayload1 { get; set; }
            [JsonProperty(Order = 17, Required = Required.Always)] public long OccurrencePayload2 { get; set; }
            [JsonProperty(Order = 18, Required = Required.Always)] public long OccurrenceOrderingValue { get; set; }
            [JsonProperty(Order = 19, Required = Required.Always)] public long OccurrencePresenceEpoch { get; set; }
            [JsonProperty(Order = 20, Required = Required.Always)] public long OccurrencePresenceStartFrame { get; set; }
            [JsonProperty(Order = 21, Required = Required.Always)] public int TargetDomainId { get; set; }
            [JsonProperty(Order = 22, Required = Required.Always)] public long TargetValue { get; set; }
            [JsonProperty(Order = 23, Required = Required.Always)] public int TargetGeneration { get; set; }
            [JsonProperty(Order = 24, Required = Required.Always)] public int OwnerIndex { get; set; }
            [JsonProperty(Order = 25, Required = Required.Always)] public int OwnerGeneration { get; set; }
            [JsonProperty(Order = 26, Required = Required.Always)] public int SubjectIndex { get; set; }
            [JsonProperty(Order = 27, Required = Required.Always)] public int SubjectGeneration { get; set; }
            [JsonProperty(Order = 28, Required = Required.Always)] public int InstigatorIndex { get; set; }
            [JsonProperty(Order = 29, Required = Required.Always)] public int InstigatorGeneration { get; set; }
            [JsonProperty(Order = 30, Required = Required.Always)] public long ActionPayload0 { get; set; }
            [JsonProperty(Order = 31, Required = Required.Always)] public long ActionPayload1 { get; set; }
            [JsonProperty(Order = 32, Required = Required.Always)] public long ActionPayload2 { get; set; }

            public static ReliableOutboxPayload From(GameplayTriggerReliableOutboxRecord value) =>
                new ReliableOutboxPayload
                {
                    OriginalActivationFrame = value.OriginalActivationFrame.Value,
                    OriginalDueFrame = value.OriginalDueFrame.Value,
                    Sequence = value.Sequence,
                    ActivationId = value.ActivationId,
                    ActionIndex = value.ActionIndex,
                    ActionKindId = value.ActionKindId.Value,
                    OriginBindingId = value.OriginBindingId.Value,
                    OriginRouteId = value.OriginRouteId.Value,
                    OriginInstanceIndex = value.OriginInstanceId.Index,
                    OriginInstanceGeneration = value.OriginInstanceId.Generation,
                    OccurrenceKind = (int)value.OccurrenceKind,
                    OccurrenceSignalId = value.OccurrenceSignalId,
                    PreviousScalarRaw = value.PreviousScalarRaw,
                    CurrentScalarRaw = value.CurrentScalarRaw,
                    OccurrencePayload0 = value.OccurrencePayload0,
                    OccurrencePayload1 = value.OccurrencePayload1,
                    OccurrencePayload2 = value.OccurrencePayload2,
                    OccurrenceOrderingValue = value.OccurrenceOrderingValue,
                    OccurrencePresenceEpoch = value.OccurrencePresenceEpoch,
                    OccurrencePresenceStartFrame = value.OccurrencePresenceStartFrame,
                    TargetDomainId = value.ResolvedTarget.DomainId,
                    TargetValue = value.ResolvedTarget.Value,
                    TargetGeneration = value.ResolvedTarget.Generation,
                    OwnerIndex = value.Owner.Index,
                    OwnerGeneration = value.Owner.Generation,
                    SubjectIndex = value.Subject.Index,
                    SubjectGeneration = value.Subject.Generation,
                    InstigatorIndex = value.Instigator.Index,
                    InstigatorGeneration = value.Instigator.Generation,
                    ActionPayload0 = value.ActionPayload0,
                    ActionPayload1 = value.ActionPayload1,
                    ActionPayload2 = value.ActionPayload2
                };
        }

        private sealed class DeferredPayload
        {
            [JsonProperty(Order = 1, Required = Required.Always)] public long OriginalDueFrame { get; set; }
            [JsonProperty(Order = 2, Required = Required.Always)] public long Sequence { get; set; }
            [JsonProperty(Order = 3, Required = Required.Always)] public int OperationKind { get; set; }
            [JsonProperty(Order = 4, Required = Required.Always)] public long ActivationId { get; set; }
            [JsonProperty(Order = 5, Required = Required.Always)] public int ActionIndex { get; set; }
            [JsonProperty(Order = 6, Required = Required.Always)] public int ActionKindId { get; set; }
            [JsonProperty(Order = 7, Required = Required.Always)] public int OriginBindingId { get; set; }
            [JsonProperty(Order = 8, Required = Required.Always)] public int OriginRouteId { get; set; }
            [JsonProperty(Order = 9, Required = Required.Always)] public int TargetInstanceIndex { get; set; }
            [JsonProperty(Order = 10, Required = Required.Always)] public int TargetInstanceGeneration { get; set; }
            [JsonProperty(Order = 11, Required = Required.Always)] public int TargetSourceBindingId { get; set; }
            [JsonProperty(Order = 12, Required = Required.Always)] public bool Enabled { get; set; }
            [JsonProperty(Order = 13, Required = Required.Always)] public int SignalId { get; set; }
            [JsonProperty(Order = 14, Required = Required.Always)] public int SubjectIndex { get; set; }
            [JsonProperty(Order = 15, Required = Required.Always)] public int SubjectGeneration { get; set; }
            [JsonProperty(Order = 16, Required = Required.Always)] public int InstigatorIndex { get; set; }
            [JsonProperty(Order = 17, Required = Required.Always)] public int InstigatorGeneration { get; set; }
            [JsonProperty(Order = 18, Required = Required.Always)] public long Payload0 { get; set; }
            [JsonProperty(Order = 19, Required = Required.Always)] public long Payload1 { get; set; }
            [JsonProperty(Order = 20, Required = Required.Always)] public long Payload2 { get; set; }

            public static DeferredPayload From(GameplayTriggerDeferredRecord value) => new DeferredPayload
            {
                OriginalDueFrame = value.OriginalDueFrame.Value,
                Sequence = value.Sequence,
                OperationKind = (int)value.OperationKind,
                ActivationId = value.ActivationId,
                ActionIndex = value.ActionIndex,
                ActionKindId = value.ActionKindId.Value,
                OriginBindingId = value.OriginBindingId.Value,
                OriginRouteId = value.OriginRouteId.Value,
                TargetInstanceIndex = value.TargetInstanceId.Index,
                TargetInstanceGeneration = value.TargetInstanceId.Generation,
                TargetSourceBindingId = value.TargetSourceBindingId.Value,
                Enabled = value.Enabled,
                SignalId = value.SignalId,
                SubjectIndex = value.Subject.Index,
                SubjectGeneration = value.Subject.Generation,
                InstigatorIndex = value.Instigator.Index,
                InstigatorGeneration = value.Instigator.Generation,
                Payload0 = value.Payload0,
                Payload1 = value.Payload1,
                Payload2 = value.Payload2
            };
        }

        private sealed class InstancePayload
        {
            [JsonProperty(Order = 1, Required = Required.Always)] public int InstanceIndex { get; set; }
            [JsonProperty(Order = 2, Required = Required.Always)] public int InstanceGeneration { get; set; }
            [JsonProperty(Order = 3, Required = Required.Always)] public int BindingId { get; set; }
            [JsonProperty(Order = 4, Required = Required.Always)] public int SourceBindingId { get; set; }
            [JsonProperty(Order = 5, Required = Required.Always)] public int DefinitionId { get; set; }
            [JsonProperty(Order = 6, Required = Required.Always)] public int DefinitionVersion { get; set; }
            [JsonProperty(Order = 7, Required = Required.Always)] public int SourceDefinitionId { get; set; }
            [JsonProperty(Order = 8, Required = Required.Always)] public int OwnerIndex { get; set; }
            [JsonProperty(Order = 9, Required = Required.Always)] public int OwnerGeneration { get; set; }
            [JsonProperty(Order = 10, Required = Required.Always)] public bool Enabled { get; set; }
            [JsonProperty(Order = 11, Required = Required.Always)] public int ActiveCount { get; set; }
            [JsonProperty(Order = 12, Required = Required.Always)] public long AggregatePresenceEpoch { get; set; }

            public static InstancePayload From(GameplayTriggerInstanceAuthorityState value) => new InstancePayload
            {
                InstanceIndex = value.InstanceId.Index, InstanceGeneration = value.InstanceId.Generation,
                BindingId = value.BindingId.Value, SourceBindingId = value.SourceBindingId.Value,
                DefinitionId = value.DefinitionId.Value, DefinitionVersion = value.DefinitionVersion,
                SourceDefinitionId = value.SourceDefinitionId.Value, OwnerIndex = value.Owner.Index,
                OwnerGeneration = value.Owner.Generation, Enabled = value.Enabled, ActiveCount = value.ActiveCount,
                AggregatePresenceEpoch = value.AggregatePresenceEpoch
            };
        }

        private sealed class RelationPayload
        {
            [JsonProperty(Order = 1, Required = Required.Always)] public int InstanceIndex { get; set; }
            [JsonProperty(Order = 2, Required = Required.Always)] public int InstanceGeneration { get; set; }
            [JsonProperty(Order = 3, Required = Required.Always)] public int SubjectIndex { get; set; }
            [JsonProperty(Order = 4, Required = Required.Always)] public int SubjectGeneration { get; set; }
            [JsonProperty(Order = 5, Required = Required.Always)] public bool Active { get; set; }
            [JsonProperty(Order = 6, Required = Required.Always)] public bool Primed { get; set; }
            [JsonProperty(Order = 7, Required = Required.Always)] public long ScalarRawValue { get; set; }
            [JsonProperty(Order = 8, Required = Required.Always)] public long PresenceEpoch { get; set; }
            [JsonProperty(Order = 9, Required = Required.Always)] public long PresenceStartFrame { get; set; }

            public static RelationPayload From(GameplayTriggerRelationAuthorityState value) => new RelationPayload
            {
                InstanceIndex = value.InstanceId.Index, InstanceGeneration = value.InstanceId.Generation,
                SubjectIndex = value.Subject.Index, SubjectGeneration = value.Subject.Generation,
                Active = value.Active, Primed = value.Primed, ScalarRawValue = value.Scalar.RawValue,
                PresenceEpoch = value.PresenceEpoch, PresenceStartFrame = value.PresenceStartFrame
            };
        }

        private sealed class RoutePayload
        {
            [JsonProperty(Order = 1, Required = Required.Always)] public int InstanceIndex { get; set; }
            [JsonProperty(Order = 2, Required = Required.Always)] public int InstanceGeneration { get; set; }
            [JsonProperty(Order = 3, Required = Required.Always)] public int RouteId { get; set; }
            [JsonProperty(Order = 4, Required = Required.Always)] public int SubjectIndex { get; set; }
            [JsonProperty(Order = 5, Required = Required.Always)] public int SubjectGeneration { get; set; }
            [JsonProperty(Order = 6, Required = Required.Always)] public int InstigatorIndex { get; set; }
            [JsonProperty(Order = 7, Required = Required.Always)] public int InstigatorGeneration { get; set; }
            [JsonProperty(Order = 8, Required = Required.Always)] public int ActivationCount { get; set; }
            [JsonProperty(Order = 9, Required = Required.Always)] public bool HasActivated { get; set; }
            [JsonProperty(Order = 10, Required = Required.Always)] public long LastActivationFrame { get; set; }
            [JsonProperty(Order = 11, Required = Required.Always)] public long LastActivatedPresenceEpoch { get; set; }
            [JsonProperty(Order = 12, Required = Required.Always)] public bool HasStaySchedule { get; set; }
            [JsonProperty(Order = 13, Required = Required.Always)] public long NextStayFrame { get; set; }

            public static RoutePayload From(GameplayTriggerRouteAuthorityState value) => new RoutePayload
            {
                InstanceIndex = value.InstanceId.Index, InstanceGeneration = value.InstanceId.Generation,
                RouteId = value.RouteId.Value, SubjectIndex = value.Subject.Index,
                SubjectGeneration = value.Subject.Generation, InstigatorIndex = value.Instigator.Index,
                InstigatorGeneration = value.Instigator.Generation, ActivationCount = value.ActivationCount,
                HasActivated = value.HasActivated, LastActivationFrame = value.LastActivationFrame,
                LastActivatedPresenceEpoch = value.LastActivatedPresenceEpoch, HasStaySchedule = value.HasStaySchedule,
                NextStayFrame = value.NextStayFrame
            };
        }
    }
}
