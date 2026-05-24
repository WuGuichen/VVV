using System;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Story.Runtime
{
    public sealed class StoryRuntimeSaveStateProvider :
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer
    {
        public const string ModuleId = StoryRuntimeModule.DefaultModuleId;
        public const string CustomStateTypeId = "mxframework.story.runtime.state";
        public const int SchemaVersion = 1;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include
        };

        private readonly StoryDirector _director;
        private readonly Func<long> _frameProvider;

        public StoryRuntimeSaveStateProvider(StoryDirector director)
            : this(director, null)
        {
        }

        public StoryRuntimeSaveStateProvider(StoryDirector director, Func<long> frameProvider)
        {
            _director = director ?? throw new ArgumentNullException(nameof(director));
            _frameProvider = frameProvider;
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            string payload = JsonConvert.SerializeObject(_director.CaptureSaveState(), JsonSettings);
            var moduleState = new RuntimeModuleSaveState(
                ModuleId,
                SchemaVersion,
                new RuntimeCustomState(CustomStateTypeId, SchemaVersion, payload));
            var saveState = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                "story-runtime-s1",
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
            RuntimeSaveStateResult<StoryDirectorSaveState> load = LoadDirectorState(saveState);
            if (!load.Success)
            {
                return RuntimeSaveStateResult<bool>.Failed(load.Error);
            }

            StoryLoadGraphResult restore;
            try
            {
                restore = _director.RestoreSaveState(load.Value);
            }
            catch (Exception exception)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates[" + ModuleId + "].customState.payloadJson",
                    "Story director restore threw: " + exception.Message,
                    -1,
                    StoryDirectorSaveState.CurrentSchemaVersion,
                    exception));
            }

            if (!restore.Success)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates[" + ModuleId + "].customState.payloadJson",
                    "Story director rejected restored state: " + restore.Code + ". " + restore.Message));
            }

            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private static RuntimeSaveStateResult<StoryDirectorSaveState> LoadDirectorState(RuntimeSaveState saveState)
        {
            if (saveState == null)
            {
                return Failed("$", "Story save state cannot be null.");
            }

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
            {
                return Failed("$.moduleStates[" + ModuleId + "]", "Story runtime module state is missing.");
            }

            if (module.SchemaVersion != SchemaVersion)
            {
                return Unsupported("$.moduleStates[" + ModuleId + "].schemaVersion", module.SchemaVersion);
            }

            if (module.CustomState == null)
            {
                return Failed("$.moduleStates[" + ModuleId + "].customState", "Story runtime custom state is missing.");
            }

            if (!string.Equals(module.CustomState.TypeId, CustomStateTypeId, StringComparison.Ordinal))
            {
                return RuntimeSaveStateResult<StoryDirectorSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$.moduleStates[" + ModuleId + "].customState.typeId",
                    "Story runtime custom state type id does not match."));
            }

            if (module.CustomState.SchemaVersion != SchemaVersion)
            {
                return Unsupported("$.moduleStates[" + ModuleId + "].customState.schemaVersion", module.CustomState.SchemaVersion);
            }

            try
            {
                StoryDirectorSaveState state = JsonConvert.DeserializeObject<StoryDirectorSaveState>(
                    module.CustomState.PayloadJson,
                    JsonSettings);
                if (state == null)
                {
                    return Failed("$.moduleStates[" + ModuleId + "].customState.payloadJson", "Story runtime payload parsed to null.");
                }

                if (state.SchemaVersion != StoryDirectorSaveState.CurrentSchemaVersion)
                {
                    return Unsupported("$.moduleStates[" + ModuleId + "].customState.payloadJson.schemaVersion", state.SchemaVersion);
                }

                return RuntimeSaveStateResult<StoryDirectorSaveState>.Succeeded(state);
            }
            catch (Exception exception)
            {
                return RuntimeSaveStateResult<StoryDirectorSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates[" + ModuleId + "].customState.payloadJson",
                    "Story runtime payload json could not be parsed: " + exception.Message,
                    -1,
                    StoryDirectorSaveState.CurrentSchemaVersion,
                    exception));
            }
        }

        private static RuntimeSaveStateResult<StoryDirectorSaveState> Failed(string path, string message)
        {
            return RuntimeSaveStateResult<StoryDirectorSaveState>.Failed(new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.InvalidDocument,
                path,
                message));
        }

        private static RuntimeSaveStateResult<StoryDirectorSaveState> Unsupported(string path, int sourceSchemaVersion)
        {
            return RuntimeSaveStateResult<StoryDirectorSaveState>.Failed(new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.UnsupportedVersion,
                path,
                "Story runtime save state schema version is unsupported.",
                sourceSchemaVersion,
                SchemaVersion));
        }
    }
}
