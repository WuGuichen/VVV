using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public enum RuntimeSaveStateCoordinatorPhase
    {
        Migration = 0,
        Capture = 1,
        Restore = 2,
        Checkpoint = 3,
        Composition = 4
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

    /// <summary>Reports authoritative coordinator composition without exposing a partial coordinator.</summary>
    /// <remarks>LowFreqAlloc. Created once at a Runtime composition boundary.</remarks>
    public sealed class RuntimeSaveStateCoordinatorFactoryResult
    {
        private RuntimeSaveStateCoordinatorFactoryResult(
            bool success,
            RuntimeSaveStateCoordinator coordinator,
            RuntimeSaveStateParticipantError error)
        {
            Success = success;
            Coordinator = coordinator;
            Error = error;
        }

        public bool Success { get; }
        public RuntimeSaveStateCoordinator Coordinator { get; }
        public RuntimeSaveStateParticipantError Error { get; }

        internal static RuntimeSaveStateCoordinatorFactoryResult Succeeded(RuntimeSaveStateCoordinator coordinator)
        {
            return new RuntimeSaveStateCoordinatorFactoryResult(true, coordinator, null);
        }

        internal static RuntimeSaveStateCoordinatorFactoryResult Failed(RuntimeSaveStateParticipantError error)
        {
            return new RuntimeSaveStateCoordinatorFactoryResult(false, null, error);
        }
    }

    public sealed class RuntimeSaveStateCoordinator
    {
        private readonly RuntimeSaveStateRegistry _registry;
        private readonly RuntimeSaveStateMigrationPipeline _migrationPipeline;
        private readonly int _targetSchemaVersion;
        private readonly RuntimeSaveStateRegistrySnapshot _authoritativeRegistry;
        private readonly RuntimeSaveStateRootHeaderDescriptor _authoritativeDescriptor;
        private readonly RuntimeSaveStateRestoreProfile _restoreProfile;

        /// <summary>Gets whether this coordinator owns a frozen authoritative composition.</summary>
        public bool IsAuthoritative => _authoritativeRegistry != null;

        /// <summary>Gets the frozen restore profile, or <see langword="null"/> for a legacy coordinator.</summary>
        public RuntimeSaveStateRestoreProfile RestoreProfile => _restoreProfile;

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

        private RuntimeSaveStateCoordinator(
            RuntimeSaveStateRegistry registry,
            RuntimeSaveStateRegistrySnapshot authoritativeRegistry,
            RuntimeSaveStateRestoreProfile restoreProfile)
        {
            _registry = registry;
            _authoritativeRegistry = authoritativeRegistry;
            _restoreProfile = restoreProfile;
            _authoritativeDescriptor = restoreProfile.Descriptor;
            _targetSchemaVersion = restoreProfile.Descriptor.SchemaVersion;
        }

        /// <summary>Validates and freezes an authoritative Runtime SaveState composition.</summary>
        /// <remarks>
        /// AllocByDesign. Seals the registry and copies the participant catalog, migration catalog, target
        /// schema, and all four root descriptor fields. No save-state provider is invoked during composition.
        /// </remarks>
        public static RuntimeSaveStateCoordinatorFactoryResult CreateAuthoritative(
            RuntimeSaveStateRegistry registry,
            RuntimeSaveStateMigrationPipeline migrationPipeline = null)
        {
            return CreateAuthoritativeCore(registry, migrationPipeline, null);
        }

        /// <summary>Validates a registry against an existing frozen restore profile and seals the composition.</summary>
        /// <remarks>AllocByDesign. The profile identity is retained for candidate prepare/restore plan matching.</remarks>
        public static RuntimeSaveStateCoordinatorFactoryResult CreateAuthoritativeWithProfile(
            RuntimeSaveStateRegistry registry,
            RuntimeSaveStateRestoreProfile restoreProfile)
        {
            if (restoreProfile == null)
            {
                throw new ArgumentNullException(nameof(restoreProfile));
            }

            return CreateAuthoritativeCore(registry, null, restoreProfile);
        }

        private static RuntimeSaveStateCoordinatorFactoryResult CreateAuthoritativeCore(
            RuntimeSaveStateRegistry registry,
            RuntimeSaveStateMigrationPipeline migrationPipeline,
            RuntimeSaveStateRestoreProfile restoreProfile)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            RuntimeSaveStateRegistryResult prepare = registry.PrepareAuthoritative(
                out RuntimeSaveStateRegistryPreparedSnapshot prepared);
            if (!prepare.Success)
            {
                return FactoryFailure(
                    prepare.Error != null ? prepare.Error.ParticipantId : string.Empty,
                    prepare.Error != null ? prepare.Error.ToString() : "Authoritative registry preparation failed.",
                    null);
            }

            bool profileClaimed = false;
            bool profileClaimCommitted = false;
            RuntimeSaveStateRestoreProfile claimedProfile = null;
            try
            {
                RuntimeSaveStateRootHeaderDescriptor descriptor;
                try
                {
                    var authority = (IRuntimeSaveStateRootHeaderAuthority)prepared.RootHeaderParticipant.Provider;
                    RuntimeSaveStateRootHeaderDescriptor source = authority.Descriptor;
                    if (source == null)
                    {
                        return FactoryFailure(
                            prepared.RootHeaderParticipant.ParticipantId,
                            "The root-header authority returned a null descriptor.",
                            null);
                    }

                    descriptor = new RuntimeSaveStateRootHeaderDescriptor(
                        source.SchemaVersion,
                        source.FrameworkVersion,
                        source.ConfigVersion,
                        source.ResourceCatalogVersion);
                }
                catch (Exception ex)
                {
                    return FactoryFailure(
                        prepared.RootHeaderParticipant.ParticipantId,
                        "The root-header authority descriptor could not be frozen: " + ex.Message,
                        ex);
                }

                RuntimeSaveStateRestoreProfile frozenProfile = restoreProfile;
                if (frozenProfile != null && !frozenProfile.Descriptor.Equals(descriptor))
                {
                    return FactoryFailure(
                        prepared.RootHeaderParticipant.ParticipantId,
                        "The root-header authority descriptor does not match the supplied restore profile.",
                        null);
                }

                if (frozenProfile == null)
                {
                    try
                    {
                        frozenProfile = RuntimeSaveStateRestoreProfile.Create(descriptor, migrationPipeline);
                    }
                    catch (Exception ex)
                    {
                        return FactoryFailure(
                            prepared.RootHeaderParticipant.ParticipantId,
                            "The save-state restore profile could not be frozen: " + ex.Message,
                            ex);
                    }
                }

                if (!frozenProfile.TryClaimComposition())
                {
                    return FactoryFailure(
                        prepared.RootHeaderParticipant.ParticipantId,
                        "The supplied restore profile is already bound to an authoritative coordinator.",
                        null);
                }

                profileClaimed = true;
                claimedProfile = frozenProfile;

                RuntimeSaveStateRegistryResult commit = registry.CommitAuthoritative(
                    prepared,
                    out RuntimeSaveStateRegistrySnapshot snapshot);
                if (!commit.Success)
                {
                    return FactoryFailure(
                        commit.Error != null ? commit.Error.ParticipantId : string.Empty,
                        commit.Error != null ? commit.Error.ToString() : "Authoritative registry commit failed.",
                        null);
                }

                var coordinator = new RuntimeSaveStateCoordinator(
                    registry,
                    snapshot,
                    frozenProfile);
                profileClaimCommitted = true;
                return RuntimeSaveStateCoordinatorFactoryResult.Succeeded(coordinator);
            }
            finally
            {
                if (profileClaimed && !profileClaimCommitted)
                {
                    claimedProfile.ReleaseCompositionClaim();
                }

                registry.AbortAuthoritative(prepared);
            }
        }

        public RuntimeSaveStateCoordinatorResult<RuntimeSaveState> CaptureSaveState()
        {
            if (_authoritativeRegistry != null)
            {
                return CaptureAuthoritativeSaveState();
            }

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
            if (IsAuthoritative)
            {
                RuntimeSaveStateResult<RuntimeSaveStateRestorePlan> preparation = _restoreProfile.Prepare(saveState);
                if (!preparation.Success)
                {
                    if (preparation.Error.Code == RuntimeSaveStateErrorCode.RootHeaderMismatch)
                    {
                        return RestoreFailure(
                            preparation.Error,
                            RuntimeSaveStateCoordinatorPhase.Restore,
                            RuntimeSaveStateRootHeaderProvider.ParticipantId);
                    }

                    return RestoreFailure(preparation.Error, RuntimeSaveStateCoordinatorPhase.Migration);
                }

                return RestorePrepared(preparation.Value);
            }

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

            return RestoreState(migrationResult.Value, _registry.GetParticipantsInStableOrder());
        }

        /// <summary>Restores a plan prepared by this coordinator's exact authoritative profile.</summary>
        /// <remarks>LowFreqAlloc. Rejected plans invoke no participant restorer.</remarks>
        public RuntimeSaveStateCoordinatorResult<bool> RestorePrepared(RuntimeSaveStateRestorePlan plan)
        {
            if (!IsAuthoritative)
            {
                return RestoreFailure(
                    new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.RestoreProfileMismatch,
                        "$",
                        "Legacy coordinators cannot restore authoritative prepared plans."),
                    RuntimeSaveStateCoordinatorPhase.Restore);
            }

            if (!_restoreProfile.Owns(plan))
            {
                return RestoreFailure(
                    new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.RestoreProfileMismatch,
                        "$",
                        "The restore plan belongs to a different authoritative profile."),
                    RuntimeSaveStateCoordinatorPhase.Restore);
            }

            return RestoreState(plan.State, _authoritativeRegistry.Participants);
        }

        private static RuntimeSaveStateCoordinatorResult<bool> RestoreState(
            RuntimeSaveState migratedSaveState,
            IReadOnlyList<RuntimeSaveStateParticipant> participants)
        {
            var errors = new List<RuntimeSaveStateParticipantError>();
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

        private static RuntimeSaveStateCoordinatorResult<bool> RestoreFailure(
            RuntimeSaveStateError error,
            RuntimeSaveStateCoordinatorPhase phase,
            string participantId = "")
        {
            return RuntimeSaveStateCoordinatorResult<bool>.Failed(
                false,
                new[]
                {
                    new RuntimeSaveStateParticipantError(
                        participantId,
                        phase,
                        error)
                });
        }

        private RuntimeSaveStateCoordinatorResult<RuntimeSaveState> CaptureAuthoritativeSaveState()
        {
            RuntimeSaveStateParticipant authority = _authoritativeRegistry.RootHeaderParticipant;
            RuntimeSaveStateResult<RuntimeSaveState> authorityResult;
            try
            {
                authorityResult = authority.Provider.CaptureSaveState();
            }
            catch (Exception ex)
            {
                return CaptureFailure(CreateExceptionError(
                    authority.ParticipantId,
                    RuntimeSaveStateCoordinatorPhase.Capture,
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Root-header authority threw an exception: " + ex.Message,
                    ex));
            }

            if (!authorityResult.Success)
            {
                RuntimeSaveStateError authorityError = authorityResult.Error;
                if (authorityError == null || authorityError.IsNone)
                {
                    authorityError = new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$",
                        "Root-header authority returned an invalid failure result.");
                }

                return CaptureFailure(new RuntimeSaveStateParticipantError(
                    authority.ParticipantId,
                    RuntimeSaveStateCoordinatorPhase.Capture,
                    authorityError));
            }

            RuntimeSaveState header = authorityResult.Value;
            if (header == null)
            {
                return CaptureFailure(CreateError(
                    authority.ParticipantId,
                    RuntimeSaveStateCoordinatorPhase.Capture,
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Root-header authority returned a null document."));
            }

            if (!_authoritativeDescriptor.Matches(header))
            {
                return CaptureFailure(CreateRootHeaderMismatchError(
                    RuntimeSaveStateCoordinatorPhase.Capture,
                    header,
                    "Root-header authority output does not match the frozen composition descriptor."));
            }

            if (header.Frame != 0L)
            {
                return CaptureFailure(CreateError(
                    authority.ParticipantId,
                    RuntimeSaveStateCoordinatorPhase.Capture,
                    RuntimeSaveStateErrorCode.RootHeaderPolluted,
                    "$.frame",
                    "Root-header authority must leave checkpoint Frame at zero."));
            }

            if (HasBodyOrMetadata(header))
            {
                return CaptureFailure(CreateError(
                    authority.ParticipantId,
                    RuntimeSaveStateCoordinatorPhase.Capture,
                    RuntimeSaveStateErrorCode.RootHeaderPolluted,
                    "$",
                    "Root-header authority must return a header-only document."));
            }

            var capturedBodies = new List<RuntimeSaveState>();
            var errors = new List<RuntimeSaveStateParticipantError>();
            IReadOnlyList<RuntimeSaveStateParticipant> participants = _authoritativeRegistry.Participants;
            for (int i = 0; i < participants.Count; i++)
            {
                RuntimeSaveStateParticipant participant = participants[i];
                if (ReferenceEquals(participant, authority) || participant.Provider == null)
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

                capturedBodies.Add(result.Value);
            }

            if (errors.Count > 0)
            {
                return RuntimeSaveStateCoordinatorResult<RuntimeSaveState>.Failed(null, errors);
            }

            return RuntimeSaveStateCoordinatorResult<RuntimeSaveState>.Succeeded(
                MergeAuthoritativeState(header, capturedBodies));
        }

        private static RuntimeSaveState MergeAuthoritativeState(
            RuntimeSaveState header,
            IReadOnlyList<RuntimeSaveState> capturedBodies)
        {
            var entities = new List<RuntimeEntitySaveState>();
            var globalCounters = new List<RuntimeCounterSaveState>();
            var moduleStates = new List<RuntimeModuleSaveState>();
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < capturedBodies.Count; i++)
            {
                RuntimeSaveState body = capturedBodies[i];
                Append(entities, body.Entities);
                Append(globalCounters, body.GlobalCounters);
                Append(moduleStates, body.ModuleStates);
                MergeMetadata(metadata, body.Metadata);
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

        private static bool HasBodyOrMetadata(RuntimeSaveState saveState)
        {
            return saveState.Entities.Count != 0 ||
                   saveState.GlobalCounters.Count != 0 ||
                   saveState.ModuleStates.Count != 0 ||
                   saveState.Metadata.Count != 0;
        }

        private static RuntimeSaveStateCoordinatorResult<RuntimeSaveState> CaptureFailure(
            RuntimeSaveStateParticipantError error)
        {
            return RuntimeSaveStateCoordinatorResult<RuntimeSaveState>.Failed(
                null,
                new[] { error });
        }

        private static RuntimeSaveStateCoordinatorFactoryResult FactoryFailure(
            string participantId,
            string message,
            Exception exception)
        {
            return RuntimeSaveStateCoordinatorFactoryResult.Failed(CreateExceptionError(
                participantId,
                RuntimeSaveStateCoordinatorPhase.Composition,
                RuntimeSaveStateErrorCode.CompositionFailed,
                "$",
                message,
                exception));
        }

        private RuntimeSaveStateParticipantError CreateRootHeaderMismatchError(
            RuntimeSaveStateCoordinatorPhase phase,
            RuntimeSaveState actual,
            string message)
        {
            string path;
            if (actual == null || actual.SchemaVersion != _authoritativeDescriptor.SchemaVersion)
            {
                path = "$.schemaVersion";
            }
            else if (!string.Equals(actual.FrameworkVersion, _authoritativeDescriptor.FrameworkVersion, StringComparison.Ordinal))
            {
                path = "$.frameworkVersion";
            }
            else if (!string.Equals(actual.ConfigVersion, _authoritativeDescriptor.ConfigVersion, StringComparison.Ordinal))
            {
                path = "$.configVersion";
            }
            else
            {
                path = "$.resourceCatalogVersion";
            }

            return CreateError(
                RuntimeSaveStateRootHeaderProvider.ParticipantId,
                phase,
                RuntimeSaveStateErrorCode.RootHeaderMismatch,
                path,
                message);
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
