using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public enum RuntimeSaveStateCoordinatorPhase
    {
        Migration = 0,
        Capture = 1,
        Restore = 2
    }

    public sealed class RuntimeSaveStateParticipantError
    {
        public RuntimeSaveStateParticipantError(
            string participantId,
            RuntimeSaveStateCoordinatorPhase phase,
            RuntimeSaveStateError error)
        {
            ParticipantId = participantId ?? string.Empty;
            Phase = phase;
            Error = error ?? RuntimeSaveStateError.None;
        }

        public string ParticipantId { get; }
        public RuntimeSaveStateCoordinatorPhase Phase { get; }
        public RuntimeSaveStateError Error { get; }

        public override string ToString()
        {
            string participant = string.IsNullOrEmpty(ParticipantId) ? "<coordinator>" : ParticipantId;
            return Phase + " Participant='" + participant + "' " + Error;
        }
    }

    public sealed class RuntimeSaveStateCoordinatorResult<T>
    {
        private RuntimeSaveStateCoordinatorResult(
            bool success,
            T value,
            IReadOnlyList<RuntimeSaveStateParticipantError> errors)
        {
            Success = success;
            Value = value;
            Errors = CopyErrors(errors);
        }

        public bool Success { get; }
        public T Value { get; }
        public IReadOnlyList<RuntimeSaveStateParticipantError> Errors { get; }

        public RuntimeSaveStateParticipantError FirstError => Errors.Count > 0 ? Errors[0] : null;

        public static RuntimeSaveStateCoordinatorResult<T> Succeeded(T value)
        {
            return new RuntimeSaveStateCoordinatorResult<T>(true, value, null);
        }

        public static RuntimeSaveStateCoordinatorResult<T> Failed(
            T value,
            IReadOnlyList<RuntimeSaveStateParticipantError> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                throw new ArgumentException("Coordinator failure requires at least one error.", nameof(errors));
            }

            return new RuntimeSaveStateCoordinatorResult<T>(false, value, errors);
        }

        private static ReadOnlyCollection<RuntimeSaveStateParticipantError> CopyErrors(
            IReadOnlyList<RuntimeSaveStateParticipantError> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                return new ReadOnlyCollection<RuntimeSaveStateParticipantError>(new List<RuntimeSaveStateParticipantError>());
            }

            var copy = new List<RuntimeSaveStateParticipantError>(errors.Count);
            for (int i = 0; i < errors.Count; i++)
            {
                copy.Add(errors[i]);
            }

            return new ReadOnlyCollection<RuntimeSaveStateParticipantError>(copy);
        }
    }

    public sealed class RuntimeSaveStateCoordinator
    {
        private readonly RuntimeSaveStateRegistry _registry;
        private readonly RuntimeSaveStateMigrationPipeline _migrationPipeline;
        private readonly int _targetSchemaVersion;

        public RuntimeSaveStateCoordinator(RuntimeSaveStateRegistry registry)
            : this(registry, null, RuntimeSaveState.CurrentSchemaVersion)
        {
        }

        public RuntimeSaveStateCoordinator(
            RuntimeSaveStateRegistry registry,
            RuntimeSaveStateMigrationPipeline migrationPipeline,
            int targetSchemaVersion = RuntimeSaveState.CurrentSchemaVersion)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (targetSchemaVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetSchemaVersion), "Save state schema version cannot be negative.");
            }

            _registry = registry;
            _migrationPipeline = migrationPipeline ?? new RuntimeSaveStateMigrationPipeline();
            _targetSchemaVersion = targetSchemaVersion;
        }

        public RuntimeSaveStateCoordinatorResult<RuntimeSaveState> CaptureSaveState()
        {
            IReadOnlyList<RuntimeSaveStateParticipant> participants = _registry.GetParticipantsInStableOrder();
            var captured = new List<RuntimeSaveState>();
            var errors = new List<RuntimeSaveStateParticipantError>();

            for (int i = 0; i < participants.Count; i++)
            {
                RuntimeSaveStateParticipant participant = participants[i];
                if (participant.Provider == null)
                {
                    continue;
                }

                RuntimeSaveStateResult<RuntimeSaveState> result;
                try
                {
                    result = participant.Provider.CaptureSaveState();
                }
                catch (Exception ex)
                {
                    errors.Add(CreateExceptionError(
                        participant.ParticipantId,
                        RuntimeSaveStateCoordinatorPhase.Capture,
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$",
                        "Save state provider threw an exception: " + ex.Message,
                        ex));
                    continue;
                }

                if (!result.Success)
                {
                    errors.Add(new RuntimeSaveStateParticipantError(
                        participant.ParticipantId,
                        RuntimeSaveStateCoordinatorPhase.Capture,
                        result.Error));
                    continue;
                }

                if (result.Value == null)
                {
                    errors.Add(CreateError(
                        participant.ParticipantId,
                        RuntimeSaveStateCoordinatorPhase.Capture,
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$",
                        "Save state provider returned a null document."));
                    continue;
                }

                captured.Add(result.Value);
            }

            RuntimeSaveState merged = MergeCapturedStates(captured);
            if (errors.Count > 0)
            {
                return RuntimeSaveStateCoordinatorResult<RuntimeSaveState>.Failed(merged, errors);
            }

            return RuntimeSaveStateCoordinatorResult<RuntimeSaveState>.Succeeded(merged);
        }

        public RuntimeSaveStateCoordinatorResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            var errors = new List<RuntimeSaveStateParticipantError>();
            RuntimeSaveStateResult<RuntimeSaveState> migrationResult = _migrationPipeline.Migrate(saveState, _targetSchemaVersion);
            if (!migrationResult.Success)
            {
                errors.Add(new RuntimeSaveStateParticipantError(
                    string.Empty,
                    RuntimeSaveStateCoordinatorPhase.Migration,
                    migrationResult.Error));
                return RuntimeSaveStateCoordinatorResult<bool>.Failed(false, errors);
            }

            RuntimeSaveState migratedSaveState = migrationResult.Value;
            IReadOnlyList<RuntimeSaveStateParticipant> participants = _registry.GetParticipantsInStableOrder();
            for (int i = 0; i < participants.Count; i++)
            {
                RuntimeSaveStateParticipant participant = participants[i];
                if (participant.Restorer == null)
                {
                    continue;
                }

                RuntimeSaveStateResult<bool> result;
                try
                {
                    result = participant.Restorer.RestoreSaveState(migratedSaveState);
                }
                catch (Exception ex)
                {
                    errors.Add(CreateExceptionError(
                        participant.ParticipantId,
                        RuntimeSaveStateCoordinatorPhase.Restore,
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$",
                        "Save state restorer threw an exception: " + ex.Message,
                        ex));
                    continue;
                }

                if (!result.Success)
                {
                    errors.Add(new RuntimeSaveStateParticipantError(
                        participant.ParticipantId,
                        RuntimeSaveStateCoordinatorPhase.Restore,
                        result.Error));
                    continue;
                }

                if (!result.Value)
                {
                    errors.Add(CreateError(
                        participant.ParticipantId,
                        RuntimeSaveStateCoordinatorPhase.Restore,
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$",
                        "Save state restorer returned false."));
                }
            }

            if (errors.Count > 0)
            {
                return RuntimeSaveStateCoordinatorResult<bool>.Failed(false, errors);
            }

            return RuntimeSaveStateCoordinatorResult<bool>.Succeeded(true);
        }

        private static RuntimeSaveState MergeCapturedStates(IReadOnlyList<RuntimeSaveState> captured)
        {
            if (captured == null || captured.Count == 0)
            {
                return new RuntimeSaveState(
                    RuntimeSaveState.CurrentSchemaVersion,
                    DateTime.UtcNow,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0L,
                    null,
                    null,
                    null,
                    null);
            }

            RuntimeSaveState header = captured[0];
            var entities = new List<RuntimeEntitySaveState>();
            var globalCounters = new List<RuntimeCounterSaveState>();
            var moduleStates = new List<RuntimeModuleSaveState>();
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < captured.Count; i++)
            {
                RuntimeSaveState state = captured[i];
                Append(entities, state.Entities);
                Append(globalCounters, state.GlobalCounters);
                Append(moduleStates, state.ModuleStates);
                MergeMetadata(metadata, state.Metadata);
            }

            return new RuntimeSaveState(
                header.SchemaVersion,
                header.CreatedAtUtc,
                header.FrameworkVersion,
                header.ConfigVersion,
                header.ResourceCatalogVersion,
                header.Frame,
                entities,
                globalCounters,
                moduleStates,
                metadata);
        }

        private static void Append<T>(List<T> target, IReadOnlyList<T> source)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        private static void MergeMetadata(Dictionary<string, string> target, IReadOnlyDictionary<string, string> source)
        {
            if (source == null || source.Count == 0)
            {
                return;
            }

            var keys = new List<string>(source.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                target[key ?? string.Empty] = source[key] ?? string.Empty;
            }
        }

        private static RuntimeSaveStateParticipantError CreateError(
            string participantId,
            RuntimeSaveStateCoordinatorPhase phase,
            RuntimeSaveStateErrorCode code,
            string path,
            string message)
        {
            return new RuntimeSaveStateParticipantError(
                participantId,
                phase,
                new RuntimeSaveStateError(code, path, message));
        }

        private static RuntimeSaveStateParticipantError CreateExceptionError(
            string participantId,
            RuntimeSaveStateCoordinatorPhase phase,
            RuntimeSaveStateErrorCode code,
            string path,
            string message,
            Exception exception)
        {
            return new RuntimeSaveStateParticipantError(
                participantId,
                phase,
                new RuntimeSaveStateError(code, path, message, -1, -1, exception));
        }
    }
}
