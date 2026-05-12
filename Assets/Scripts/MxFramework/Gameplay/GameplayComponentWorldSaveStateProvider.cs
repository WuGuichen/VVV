using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentWorldSaveStateProvider :
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer
    {
        public const string ModuleId = "mxframework.gameplay.component-world";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include
        };

        private readonly GameplayComponentWorld _world;
        private readonly Func<long> _frameProvider;

        public GameplayComponentWorldSaveStateProvider(GameplayComponentWorld world)
            : this(world, null)
        {
        }

        public GameplayComponentWorldSaveStateProvider(GameplayComponentWorld world, Func<long> frameProvider)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _frameProvider = frameProvider;
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            GameplayComponentWorldSaveState componentState = CaptureComponentWorld();
            string payloadJson = JsonConvert.SerializeObject(componentState, JsonSettings);
            var moduleState = new RuntimeModuleSaveState(
                ModuleId,
                GameplayComponentWorldSaveState.CurrentSchemaVersion,
                new RuntimeCustomState(
                    ModuleId,
                    GameplayComponentWorldSaveState.CurrentSchemaVersion,
                    payloadJson));

            var saveState = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                string.Empty,
                string.Empty,
                string.Empty,
                _frameProvider != null ? _frameProvider() : 0L,
                null,
                null,
                new[] { moduleState },
                null);

            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(saveState);
        }

        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            RuntimeSaveStateResult<GameplayComponentWorldSaveState> loadResult = LoadComponentWorldState(saveState);
            if (!loadResult.Success)
                return RuntimeSaveStateResult<bool>.Failed(loadResult.Error);

            GameplayComponentWorldSaveState componentState = loadResult.Value;
            RuntimeSaveStateResult<RestorePlan> planResult = CreateRestorePlan(componentState);
            if (!planResult.Success)
                return RuntimeSaveStateResult<bool>.Failed(planResult.Error);

            RestorePlan plan = planResult.Value;
            _world.Clear();
            _world.Registry.RestoreEntities(plan.Entities);
            for (int i = 0; i < plan.Actions.Count; i++)
                plan.Actions[i](_world.Registry);

            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private GameplayComponentWorldSaveState CaptureComponentWorld()
        {
            GameplayEntityId[] entityIds = _world.CreateEntitySnapshot();
            var entities = new GameplayComponentEntitySaveState[entityIds.Length];
            for (int i = 0; i < entityIds.Length; i++)
                entities[i] = new GameplayComponentEntitySaveState(entityIds[i].Index, entityIds[i].Generation);

            GameplayComponentSaveStateAdapter[] adapters = _world.Schemas.CreateSaveStateAdapters();
            var stores = new List<GameplayComponentStoreSaveState>();
            for (int i = 0; i < adapters.Length; i++)
            {
                GameplayComponentSaveStateAdapter adapter = adapters[i];
                if (!adapter.HasStore(_world.Registry))
                    continue;

                GameplayComponentEntrySaveState[] entries = adapter.CaptureEntries(_world.Registry);
                stores.Add(new GameplayComponentStoreSaveState(
                    adapter.Schema.StableId,
                    adapter.Schema.Version,
                    entries));
            }

            return new GameplayComponentWorldSaveState(
                GameplayComponentWorldSaveState.CurrentSchemaVersion,
                entities,
                stores);
        }

        private static RuntimeSaveStateResult<GameplayComponentWorldSaveState> LoadComponentWorldState(RuntimeSaveState saveState)
        {
            if (saveState == null)
                return FailedState("$", "Save state is null.");

            RuntimeModuleSaveState module = null;
            for (int i = 0; i < saveState.ModuleStates.Count; i++)
            {
                RuntimeModuleSaveState candidate = saveState.ModuleStates[i];
                if (candidate != null && string.Equals(candidate.ModuleId, ModuleId, StringComparison.Ordinal))
                {
                    module = candidate;
                    break;
                }
            }

            if (module == null)
                return FailedState("$.moduleStates[" + ModuleId + "]", "Component world module state is missing.");
            if (module.SchemaVersion != GameplayComponentWorldSaveState.CurrentSchemaVersion)
                return UnsupportedState("$.moduleStates[" + ModuleId + "].schemaVersion", module.SchemaVersion);
            if (module.CustomState == null)
                return FailedState("$.moduleStates[" + ModuleId + "].customState", "Component world custom state is missing.");
            if (!string.Equals(module.CustomState.TypeId, ModuleId, StringComparison.Ordinal))
                return FailedState("$.moduleStates[" + ModuleId + "].customState.typeId", "Component world custom state type id does not match module id.");
            if (module.CustomState.SchemaVersion != GameplayComponentWorldSaveState.CurrentSchemaVersion)
                return UnsupportedState("$.moduleStates[" + ModuleId + "].customState.schemaVersion", module.CustomState.SchemaVersion);

            try
            {
                GameplayComponentWorldSaveState state = JsonConvert.DeserializeObject<GameplayComponentWorldSaveState>(
                    module.CustomState.PayloadJson,
                    JsonSettings);
                if (state == null)
                    return FailedState("$.moduleStates[" + ModuleId + "].customState.payloadJson", "Component world payload parsed to null.");
                if (state.SchemaVersion != GameplayComponentWorldSaveState.CurrentSchemaVersion)
                    return UnsupportedState("$.moduleStates[" + ModuleId + "].customState.payloadJson.schemaVersion", state.SchemaVersion);

                return RuntimeSaveStateResult<GameplayComponentWorldSaveState>.Succeeded(state);
            }
            catch (Exception exception)
            {
                return RuntimeSaveStateResult<GameplayComponentWorldSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates[" + ModuleId + "].customState.payloadJson",
                    "Component world payload json could not be parsed: " + exception.Message,
                    -1,
                    GameplayComponentWorldSaveState.CurrentSchemaVersion,
                    exception));
            }
        }

        private RuntimeSaveStateResult<RestorePlan> CreateRestorePlan(GameplayComponentWorldSaveState state)
        {
            RuntimeSaveStateResult<GameplayEntityId[]> entitiesResult = ValidateEntities(state.Entities);
            if (!entitiesResult.Success)
                return RuntimeSaveStateResult<RestorePlan>.Failed(entitiesResult.Error);

            GameplayEntityId[] entities = entitiesResult.Value;
            var alive = new HashSet<GameplayEntityId>(entities);
            var seenStores = new HashSet<string>(StringComparer.Ordinal);
            var actions = new List<RestoreComponentAction>();

            for (int storeIndex = 0; storeIndex < state.ComponentStores.Count; storeIndex++)
            {
                GameplayComponentStoreSaveState store = state.ComponentStores[storeIndex];
                string storePath = "$.moduleStates[" + ModuleId + "].componentStores[" + storeIndex + "]";
                RuntimeSaveStateResult<bool> storeResult = ValidateStore(store, storePath, seenStores, out GameplayComponentSaveStateAdapter adapter);
                if (!storeResult.Success)
                    return RuntimeSaveStateResult<RestorePlan>.Failed(storeResult.Error);

                var seenEntries = new HashSet<GameplayEntityId>();
                for (int entryIndex = 0; entryIndex < store.Entries.Count; entryIndex++)
                {
                    GameplayComponentEntrySaveState entry = store.Entries[entryIndex];
                    string entryPath = storePath + ".entries[" + entryIndex + "]";
                    RuntimeSaveStateResult<GameplayEntityId> entityResult = ValidateEntryEntity(entry, entryPath, alive, seenEntries);
                    if (!entityResult.Success)
                        return RuntimeSaveStateResult<RestorePlan>.Failed(entityResult.Error);

                    RuntimeSaveStateResult<RestoreComponentAction> actionResult = adapter.CreateRestoreAction(
                        entityResult.Value,
                        entry.Payload,
                        entryPath + ".payload");
                    if (!actionResult.Success)
                        return RuntimeSaveStateResult<RestorePlan>.Failed(actionResult.Error);

                    actions.Add(actionResult.Value);
                }
            }

            return RuntimeSaveStateResult<RestorePlan>.Succeeded(new RestorePlan(entities, actions));
        }

        private static RuntimeSaveStateResult<GameplayEntityId[]> ValidateEntities(
            IReadOnlyList<GameplayComponentEntitySaveState> savedEntities)
        {
            if (savedEntities == null || savedEntities.Count == 0)
                return RuntimeSaveStateResult<GameplayEntityId[]>.Succeeded(Array.Empty<GameplayEntityId>());

            var entities = new GameplayEntityId[savedEntities.Count];
            var seen = new HashSet<GameplayEntityId>();
            var seenIndices = new HashSet<int>();
            for (int i = 0; i < savedEntities.Count; i++)
            {
                GameplayComponentEntitySaveState saved = savedEntities[i];
                string path = "$.moduleStates[" + ModuleId + "].entities[" + i + "]";
                if (saved == null)
                    return FailedEntities(path, "Saved component entity entry is null.");

                GameplayEntityId entityId;
                try
                {
                    entityId = new GameplayEntityId(saved.Index, saved.Generation);
                }
                catch (Exception exception)
                {
                    return RuntimeSaveStateResult<GameplayEntityId[]>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.UnknownEntity,
                        path,
                        "Saved component entity id is invalid: " + exception.Message,
                        -1,
                        GameplayComponentWorldSaveState.CurrentSchemaVersion,
                        exception));
                }

                if (!entityId.IsValid)
                    return FailedEntities(path, "Saved component entity id must be valid.");
                if (!seen.Add(entityId))
                    return FailedEntities(path, "Saved component entity id is duplicated.");
                if (!seenIndices.Add(entityId.Index))
                    return FailedEntities(path, "Saved component entity index is duplicated.");

                entities[i] = entityId;
            }

            return RuntimeSaveStateResult<GameplayEntityId[]>.Succeeded(entities);
        }

        private RuntimeSaveStateResult<bool> ValidateStore(
            GameplayComponentStoreSaveState store,
            string path,
            HashSet<string> seenStores,
            out GameplayComponentSaveStateAdapter adapter)
        {
            adapter = default;
            if (store == null)
                return FailedBool(path, "Component store save entry is null.");
            if (string.IsNullOrEmpty(store.SchemaId))
                return FailedBool(path + ".schemaId", "Component store schema id is missing.");
            if (!seenStores.Add(store.SchemaId))
                return FailedBool(path + ".schemaId", "Component store schema id is duplicated.");
            if (!_world.Schemas.TryGetByStableId(store.SchemaId, out GameplayComponentSchema schema))
                return FailedBool(path + ".schemaId", "Component schema is not registered: " + store.SchemaId);
            if (schema.Version != store.SchemaVersion)
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    path + ".schemaVersion",
                    "Component schema version is not supported.",
                    store.SchemaVersion,
                    schema.Version));
            if (!_world.Schemas.TryGetSaveStateAdapterByStableId(store.SchemaId, out adapter))
                return FailedBool(path + ".schemaId", "Component schema does not have a SaveState adapter: " + store.SchemaId);

            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private static RuntimeSaveStateResult<GameplayEntityId> ValidateEntryEntity(
            GameplayComponentEntrySaveState entry,
            string path,
            HashSet<GameplayEntityId> alive,
            HashSet<GameplayEntityId> seenEntries)
        {
            if (entry == null)
                return FailedEntity(path, "Component entry is null.");

            GameplayEntityId entityId;
            try
            {
                entityId = new GameplayEntityId(entry.EntityIndex, entry.EntityGeneration);
            }
            catch (Exception exception)
            {
                return RuntimeSaveStateResult<GameplayEntityId>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnknownEntity,
                    path,
                    "Component entry entity id is invalid: " + exception.Message,
                    -1,
                    GameplayComponentWorldSaveState.CurrentSchemaVersion,
                    exception));
            }

            if (!entityId.IsValid)
                return FailedEntity(path, "Component entry entity id must be valid.");
            if (!alive.Contains(entityId))
                return FailedEntity(path, "Component entry references an entity that is not alive in this save state.");
            if (!seenEntries.Add(entityId))
                return FailedEntity(path, "Component entry entity id is duplicated within this store.");
            if (entry.Payload == null)
                return FailedEntity(path + ".payload", "Component entry payload is missing.");

            return RuntimeSaveStateResult<GameplayEntityId>.Succeeded(entityId);
        }

        private static RuntimeSaveStateResult<GameplayComponentWorldSaveState> FailedState(string path, string message)
        {
            return RuntimeSaveStateResult<GameplayComponentWorldSaveState>.Failed(new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.InvalidDocument,
                path,
                message));
        }

        private static RuntimeSaveStateResult<GameplayComponentWorldSaveState> UnsupportedState(string path, int version)
        {
            return RuntimeSaveStateResult<GameplayComponentWorldSaveState>.Failed(new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.UnsupportedVersion,
                path,
                "Component world save state schema version is not supported.",
                version,
                GameplayComponentWorldSaveState.CurrentSchemaVersion));
        }

        private static RuntimeSaveStateResult<GameplayEntityId[]> FailedEntities(string path, string message)
        {
            return RuntimeSaveStateResult<GameplayEntityId[]>.Failed(new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.UnknownEntity,
                path,
                message));
        }

        private static RuntimeSaveStateResult<bool> FailedBool(string path, string message)
        {
            return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.InvalidDocument,
                path,
                message));
        }

        private static RuntimeSaveStateResult<GameplayEntityId> FailedEntity(string path, string message)
        {
            return RuntimeSaveStateResult<GameplayEntityId>.Failed(new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.UnknownEntity,
                path,
                message));
        }

        private sealed class RestorePlan
        {
            public RestorePlan(GameplayEntityId[] entities, IReadOnlyList<RestoreComponentAction> actions)
            {
                Entities = entities ?? Array.Empty<GameplayEntityId>();
                Actions = actions ?? Array.Empty<RestoreComponentAction>();
            }

            public GameplayEntityId[] Entities { get; }
            public IReadOnlyList<RestoreComponentAction> Actions { get; }
        }
    }
}
