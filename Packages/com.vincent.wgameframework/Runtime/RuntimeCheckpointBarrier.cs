using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    /// <summary>Identifies framework-owned checkpoint failure details.</summary>
    public enum RuntimeCheckpointErrorDetailCode
    {
        /// <summary>No framework-owned checkpoint detail.</summary>
        None = 0,
        /// <summary>The RuntimeHost has not started any Tick attempt.</summary>
        HostHasNoTickAttempt = 1,
        /// <summary>The RuntimeHost is currently inside a Tick attempt.</summary>
        HostTickInProgress = 2,
        /// <summary>The latest RuntimeHost Tick attempt failed.</summary>
        HostLastTickAttemptFailed = 3,
        /// <summary>A quiescence checker threw an exception.</summary>
        QuiescenceCheckException = 4,
        /// <summary>A checker returned a default or otherwise invalid pending result.</summary>
        InvalidQuiescenceResult = 5
    }

    /// <summary>Provides the completed RuntimeHost frame to a checkpoint quiescence checker.</summary>
    public readonly struct RuntimeCheckpointContext
    {
        /// <summary>Creates a context for a completed RuntimeHost frame.</summary>
        /// <remarks>NoAlloc for a valid frame. Invalid arguments throw.</remarks>
        public RuntimeCheckpointContext(long completedFrame)
        {
            if (completedFrame < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(completedFrame), "Completed frame cannot be negative.");
            }

            CompletedFrame = completedFrame;
        }

        /// <summary>Gets the fully completed RuntimeHost frame being captured.</summary>
        /// <remarks>NoAlloc.</remarks>
        public long CompletedFrame { get; }
    }

    /// <summary>Reports whether a checkpoint participant is ready without creating diagnostic text.</summary>
    public readonly struct RuntimeCheckpointQuiescenceResult
    {
        private RuntimeCheckpointQuiescenceResult(bool isReady, int detailCode)
        {
            IsReady = isReady;
            DetailCode = detailCode;
        }

        /// <summary>Gets whether the participant is ready for capture.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool IsReady { get; }

        /// <summary>Gets the participant-owned numeric pending detail, or zero when ready.</summary>
        /// <remarks>NoAlloc. Positive pending details are scoped by ParticipantId.</remarks>
        public int DetailCode { get; }

        /// <summary>Creates a ready result.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static RuntimeCheckpointQuiescenceResult Ready()
        {
            return new RuntimeCheckpointQuiescenceResult(true, 0);
        }

        /// <summary>Creates a pending result with a participant-owned positive numeric detail.</summary>
        /// <remarks>NoAlloc for a valid detail code. Invalid arguments throw.</remarks>
        public static RuntimeCheckpointQuiescenceResult Pending(int detailCode)
        {
            if (detailCode <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(detailCode), "Pending detail code must be positive.");
            }

            return new RuntimeCheckpointQuiescenceResult(false, detailCode);
        }
    }

    /// <summary>Checks whether participant-owned state is safe to capture at an end-of-Tick checkpoint.</summary>
    public interface IRuntimeCheckpointQuiescenceChecker
    {
        /// <summary>Checks readiness without draining, clearing, or mutating participant state.</summary>
        /// <remarks>NoAlloc after initialization. Implementations must be pure read-only checks.</remarks>
        RuntimeCheckpointQuiescenceResult CheckQuiescence(in RuntimeCheckpointContext context);
    }

    /// <summary>Registers one immutable checkpoint quiescence identity and checker.</summary>
    public sealed class RuntimeCheckpointQuiescenceParticipant
    {
        /// <summary>Creates an immutable checkpoint quiescence registration entry.</summary>
        /// <remarks>
        /// LowFreqAlloc. Construction allocates the participant object; valid inputs add no other allocations.
        /// Invalid arguments throw.
        /// </remarks>
        public RuntimeCheckpointQuiescenceParticipant(
            string participantId,
            int order,
            IRuntimeCheckpointQuiescenceChecker checker)
        {
            if (string.IsNullOrWhiteSpace(participantId))
            {
                throw new ArgumentException("Checkpoint quiescence participant id cannot be empty.", nameof(participantId));
            }

            ParticipantId = participantId;
            Order = order;
            Checker = checker ?? throw new ArgumentNullException(nameof(checker));
        }

        /// <summary>Gets the stable participant identity.</summary>
        /// <remarks>NoAlloc.</remarks>
        public string ParticipantId { get; }

        /// <summary>Gets the primary stable ordering key.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int Order { get; }

        /// <summary>Gets the pure read-only checker.</summary>
        /// <remarks>NoAlloc.</remarks>
        public IRuntimeCheckpointQuiescenceChecker Checker { get; }
    }

    /// <summary>Describes a checkpoint quiescence registry failure.</summary>
    public sealed class RuntimeCheckpointQuiescenceRegistryError
    {
        /// <summary>Creates a low-frequency registry diagnostic.</summary>
        /// <remarks>
        /// LowFreqAlloc. Construction allocates the registry error object; valid string references add no other
        /// allocations. ToString is AllocByDesign.
        /// </remarks>
        public RuntimeCheckpointQuiescenceRegistryError(string participantId, string message)
        {
            ParticipantId = participantId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>Gets the rejected participant identity.</summary>
        /// <remarks>NoAlloc.</remarks>
        public string ParticipantId { get; }

        /// <summary>Gets the low-frequency diagnostic message.</summary>
        /// <remarks>NoAlloc.</remarks>
        public string Message { get; }

        /// <summary>Formats the registry diagnostic.</summary>
        /// <remarks>AllocByDesign.</remarks>
        public override string ToString()
        {
            return "Participant='" + ParticipantId + "' " + Message;
        }
    }

    /// <summary>Reports checkpoint quiescence registry registration success or failure.</summary>
    public readonly struct RuntimeCheckpointQuiescenceRegistryResult
    {
        private RuntimeCheckpointQuiescenceRegistryResult(
            bool success,
            RuntimeCheckpointQuiescenceRegistryError error)
        {
            Success = success;
            Error = error;
        }

        /// <summary>Gets whether registration succeeded.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool Success { get; }

        /// <summary>Gets the structured failure, or null on success.</summary>
        /// <remarks>NoAlloc.</remarks>
        public RuntimeCheckpointQuiescenceRegistryError Error { get; }

        /// <summary>Creates a successful registration result.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static RuntimeCheckpointQuiescenceRegistryResult Succeeded()
        {
            return new RuntimeCheckpointQuiescenceRegistryResult(true, null);
        }

        /// <summary>Creates a failed registration result.</summary>
        /// <remarks>NoAlloc for a valid error reference. Invalid arguments throw.</remarks>
        public static RuntimeCheckpointQuiescenceRegistryResult Failed(
            RuntimeCheckpointQuiescenceRegistryError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            return new RuntimeCheckpointQuiescenceRegistryResult(false, error);
        }
    }

    /// <summary>Stores checkpoint quiescence participants in deterministic Order/ParticipantId order.</summary>
    public sealed class RuntimeCheckpointQuiescenceRegistry
    {
        private readonly Dictionary<string, RuntimeCheckpointQuiescenceParticipant> _participantsById;
        private readonly List<RuntimeCheckpointQuiescenceParticipant> _participants;

        /// <summary>Creates an empty checkpoint quiescence registry.</summary>
        /// <remarks>AllocByDesign. Internal collections are created once during composition.</remarks>
        public RuntimeCheckpointQuiescenceRegistry()
        {
            _participantsById = new Dictionary<string, RuntimeCheckpointQuiescenceParticipant>(StringComparer.Ordinal);
            _participants = new List<RuntimeCheckpointQuiescenceParticipant>();
        }

        /// <summary>Gets the registered participant count.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int Count => _participants.Count;

        /// <summary>Registers a checkpoint quiescence participant.</summary>
        /// <remarks>
        /// LowFreqAlloc. Stable ordering is established during registration so checkpoint checks do not sort.
        /// </remarks>
        public RuntimeCheckpointQuiescenceRegistryResult Register(
            RuntimeCheckpointQuiescenceParticipant participant)
        {
            if (participant == null)
            {
                throw new ArgumentNullException(nameof(participant));
            }

            if (_participantsById.ContainsKey(participant.ParticipantId))
            {
                return RuntimeCheckpointQuiescenceRegistryResult.Failed(
                    new RuntimeCheckpointQuiescenceRegistryError(
                        participant.ParticipantId,
                        "A checkpoint quiescence participant with the same id is already registered."));
            }

            int insertIndex = _participants.Count;
            while (insertIndex > 0 && CompareParticipants(participant, _participants[insertIndex - 1]) < 0)
            {
                insertIndex--;
            }

            _participantsById.Add(participant.ParticipantId, participant);
            _participants.Insert(insertIndex, participant);
            return RuntimeCheckpointQuiescenceRegistryResult.Succeeded();
        }

        /// <summary>Creates and registers a checkpoint quiescence participant.</summary>
        /// <remarks>LowFreqAlloc.</remarks>
        public RuntimeCheckpointQuiescenceRegistryResult Register(
            string participantId,
            int order,
            IRuntimeCheckpointQuiescenceChecker checker)
        {
            return Register(new RuntimeCheckpointQuiescenceParticipant(participantId, order, checker));
        }

        internal RuntimeCheckpointQuiescenceParticipant GetParticipantInStableOrder(int index)
        {
            return _participants[index];
        }

        private static int CompareParticipants(
            RuntimeCheckpointQuiescenceParticipant left,
            RuntimeCheckpointQuiescenceParticipant right)
        {
            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
            {
                return order;
            }

            return string.Compare(left.ParticipantId, right.ParticipantId, StringComparison.Ordinal);
        }
    }

    /// <summary>Captures a normalized authority checkpoint only after a successful, quiescent RuntimeHost Tick.</summary>
    public sealed class RuntimeCheckpointBarrier
    {
        private const string RuntimeHostParticipantId = "mxframework.runtime.host";
        private const string HostErrorPath = "$.checkpoint.host";
        private const string QuiescenceErrorPath = "$.checkpoint.quiescence";

        private readonly RuntimeHost _host;
        private readonly RuntimeSaveStateCoordinator _coordinator;
        private readonly RuntimeCheckpointQuiescenceRegistry _quiescenceRegistry;

        /// <summary>Gets the coordinator whose composition is captured by this barrier.</summary>
        /// <remarks>NoAlloc.</remarks>
        public RuntimeSaveStateCoordinator Coordinator => _coordinator;

        /// <summary>Gets the coordinator's frozen restore profile, or null for a legacy coordinator.</summary>
        /// <remarks>NoAlloc.</remarks>
        public RuntimeSaveStateRestoreProfile RestoreProfile => _coordinator.RestoreProfile;

        /// <summary>Gets the exact RuntimeHost captured by this barrier.</summary>
        /// <remarks>NoAlloc. Internal composition validation only.</remarks>
        internal RuntimeHost RuntimeHost => _host;

        /// <summary>Binds one RuntimeHost, save-state coordinator, and quiescence registry.</summary>
        /// <remarks>
        /// LowFreqAlloc. Construction allocates the barrier object; valid references add no other allocations.
        /// Invalid arguments throw.
        /// </remarks>
        public RuntimeCheckpointBarrier(
            RuntimeHost host,
            RuntimeSaveStateCoordinator coordinator,
            RuntimeCheckpointQuiescenceRegistry quiescenceRegistry)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _quiescenceRegistry = quiescenceRegistry ?? throw new ArgumentNullException(nameof(quiescenceRegistry));
        }

        /// <summary>Captures a checkpoint for the latest fully successful RuntimeHost Tick.</summary>
        /// <remarks>
        /// AllocByDesign. Host and quiescence failures prevent every save-state provider call. The successful
        /// result is a readonly copy whose root Frame is normalized to the completed RuntimeHost frame.
        /// </remarks>
        public RuntimeSaveStateCoordinatorResult<RuntimeSaveState> CaptureCheckpoint()
        {
            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> hostFailure = ValidateHost();
            if (hostFailure != null)
            {
                return hostFailure;
            }

            var context = new RuntimeCheckpointContext(_host.LastTickAttemptFrame);
            var errors = new List<RuntimeSaveStateParticipantError>();
            for (int i = 0; i < _quiescenceRegistry.Count; i++)
            {
                RuntimeCheckpointQuiescenceParticipant participant =
                    _quiescenceRegistry.GetParticipantInStableOrder(i);

                RuntimeCheckpointQuiescenceResult result;
                try
                {
                    result = participant.Checker.CheckQuiescence(in context);
                }
                catch (Exception exception)
                {
                    errors.Add(CreateCheckpointError(
                        participant.ParticipantId,
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "Checkpoint quiescence checker threw an exception: " + exception.Message,
                        (int)RuntimeCheckpointErrorDetailCode.QuiescenceCheckException,
                        exception));
                    continue;
                }

                if (!result.IsReady)
                {
                    if (result.DetailCode <= 0)
                    {
                        errors.Add(CreateCheckpointError(
                            participant.ParticipantId,
                            RuntimeSaveStateErrorCode.InvalidDocument,
                            "Checkpoint quiescence checker returned an invalid pending result.",
                            (int)RuntimeCheckpointErrorDetailCode.InvalidQuiescenceResult,
                            null));
                        continue;
                    }

                    errors.Add(CreateCheckpointError(
                        participant.ParticipantId,
                        RuntimeSaveStateErrorCode.PendingWork,
                        "Checkpoint participant has pending work.",
                        result.DetailCode,
                        null));
                }
            }

            if (errors.Count > 0)
            {
                return RuntimeSaveStateCoordinatorResult<RuntimeSaveState>.Failed(null, errors);
            }

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> captured = _coordinator.CaptureSaveState();
            if (!captured.Success)
            {
                return RuntimeSaveStateCoordinatorResult<RuntimeSaveState>.Failed(null, captured.Errors);
            }

            RuntimeSaveState state = captured.Value;
            var normalized = new RuntimeSaveState(
                state.SchemaVersion,
                state.CreatedAtUtc,
                state.FrameworkVersion,
                state.ConfigVersion,
                state.ResourceCatalogVersion,
                context.CompletedFrame,
                state.Entities,
                state.GlobalCounters,
                state.ModuleStates,
                state.Metadata);
            return RuntimeSaveStateCoordinatorResult<RuntimeSaveState>.Succeeded(normalized);
        }

        private RuntimeSaveStateCoordinatorResult<RuntimeSaveState> ValidateHost()
        {
            if (!_host.HasTickAttempt)
            {
                return CreateHostFailure(
                    RuntimeCheckpointErrorDetailCode.HostHasNoTickAttempt,
                    "RuntimeHost has not attempted a Tick.");
            }

            if (_host.IsTickInProgress)
            {
                return CreateHostFailure(
                    RuntimeCheckpointErrorDetailCode.HostTickInProgress,
                    "RuntimeHost Tick is still in progress.");
            }

            if (!_host.LastTickAttemptSucceeded)
            {
                return CreateHostFailure(
                    RuntimeCheckpointErrorDetailCode.HostLastTickAttemptFailed,
                    "The latest RuntimeHost Tick attempt did not complete successfully.");
            }

            return null;
        }

        private static RuntimeSaveStateCoordinatorResult<RuntimeSaveState> CreateHostFailure(
            RuntimeCheckpointErrorDetailCode detailCode,
            string message)
        {
            var errors = new[]
            {
                new RuntimeSaveStateParticipantError(
                    RuntimeHostParticipantId,
                    RuntimeSaveStateCoordinatorPhase.Checkpoint,
                    new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.PendingWork,
                        HostErrorPath,
                        message,
                        -1,
                        -1,
                        null,
                        (int)detailCode))
            };
            return RuntimeSaveStateCoordinatorResult<RuntimeSaveState>.Failed(null, errors);
        }

        private static RuntimeSaveStateParticipantError CreateCheckpointError(
            string participantId,
            RuntimeSaveStateErrorCode code,
            string message,
            int detailCode,
            Exception exception)
        {
            return new RuntimeSaveStateParticipantError(
                participantId,
                RuntimeSaveStateCoordinatorPhase.Checkpoint,
                new RuntimeSaveStateError(
                    code,
                    QuiescenceErrorPath,
                    message,
                    -1,
                    -1,
                    exception,
                    detailCode));
        }
    }
}
