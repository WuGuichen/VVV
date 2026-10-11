using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public enum RuntimeSaveStateRegistryErrorCode
    {
        None = 0,
        DuplicateParticipantId = 1,
        RegistrySealed = 2,
        MissingRootHeaderAuthority = 3,
        RootHeaderAuthorityIdMismatch = 4,
        RootHeaderAuthorityOrderMismatch = 5,
        RootHeaderAuthorityContractMismatch = 6,
        CompetingRootHeaderProvider = 7,
        NegativeProviderOrder = 8,
        RegistryChangedDuringComposition = 9,
        CompositionInProgress = 10
    }

    public sealed class RuntimeSaveStateParticipant
    {
        public RuntimeSaveStateParticipant(
            string participantId,
            int order,
            IRuntimeSaveStateProvider provider,
            IRuntimeSaveStateRestorer restorer)
        {
            if (string.IsNullOrWhiteSpace(participantId))
            {
                throw new ArgumentException("Save state participant id cannot be empty.", nameof(participantId));
            }

            if (provider == null && restorer == null)
            {
                throw new ArgumentException("Save state participant must provide capture, restore, or both.", nameof(provider));
            }

            ParticipantId = participantId;
            Order = order;
            Provider = provider;
            Restorer = restorer;
        }

        public string ParticipantId { get; }
        public int Order { get; }
        public IRuntimeSaveStateProvider Provider { get; }
        public IRuntimeSaveStateRestorer Restorer { get; }
    }

    public sealed class RuntimeSaveStateRegistryError
    {
        public RuntimeSaveStateRegistryError(string participantId, string message)
            : this(RuntimeSaveStateRegistryErrorCode.None, participantId, message)
        {
        }

        /// <summary>Creates a structured registry error.</summary>
        /// <remarks>LowFreqAlloc.</remarks>
        public RuntimeSaveStateRegistryError(
            RuntimeSaveStateRegistryErrorCode code,
            string participantId,
            string message)
        {
            Code = code;
            ParticipantId = participantId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public RuntimeSaveStateRegistryErrorCode Code { get; }
        public string ParticipantId { get; }
        public string Message { get; }

        public override string ToString()
        {
            if (Code == RuntimeSaveStateRegistryErrorCode.None)
            {
                return "Participant='" + ParticipantId + "' " + Message;
            }

            return Code + " Participant='" + ParticipantId + "' " + Message;
        }
    }

    public readonly struct RuntimeSaveStateRegistryResult
    {
        private RuntimeSaveStateRegistryResult(bool success, RuntimeSaveStateRegistryError error)
        {
            Success = success;
            Error = error;
        }

        public bool Success { get; }
        public RuntimeSaveStateRegistryError Error { get; }

        public static RuntimeSaveStateRegistryResult Succeeded()
        {
            return new RuntimeSaveStateRegistryResult(true, null);
        }

        public static RuntimeSaveStateRegistryResult Failed(RuntimeSaveStateRegistryError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            return new RuntimeSaveStateRegistryResult(false, error);
        }
    }

    /// <summary>Immutable, authoritative participant catalog produced by registry sealing.</summary>
    /// <remarks>
    /// AllocByDesign. The registry creates this snapshot once during composition. Participant objects are
    /// immutable and the sorted collection cannot be mutated by callers.
    /// </remarks>
    public sealed class RuntimeSaveStateRegistrySnapshot
    {
        internal RuntimeSaveStateRegistrySnapshot(
            IReadOnlyList<RuntimeSaveStateParticipant> participants,
            RuntimeSaveStateParticipant rootHeaderParticipant)
        {
            Participants = participants ?? throw new ArgumentNullException(nameof(participants));
            RootHeaderParticipant = rootHeaderParticipant ?? throw new ArgumentNullException(nameof(rootHeaderParticipant));
            RootHeaderAuthority = (IRuntimeSaveStateRootHeaderAuthority)rootHeaderParticipant.Provider;
        }

        public IReadOnlyList<RuntimeSaveStateParticipant> Participants { get; }
        public RuntimeSaveStateParticipant RootHeaderParticipant { get; }
        public IRuntimeSaveStateRootHeaderAuthority RootHeaderAuthority { get; }
    }

    internal sealed class RuntimeSaveStateRegistryPreparedSnapshot
    {
        internal RuntimeSaveStateRegistryPreparedSnapshot(
            IReadOnlyList<RuntimeSaveStateParticipant> participants,
            RuntimeSaveStateParticipant rootHeaderParticipant,
            int revision)
        {
            Participants = participants;
            RootHeaderParticipant = rootHeaderParticipant;
            Revision = revision;
        }

        internal IReadOnlyList<RuntimeSaveStateParticipant> Participants { get; }
        internal RuntimeSaveStateParticipant RootHeaderParticipant { get; }
        internal int Revision { get; }
    }

    public sealed class RuntimeSaveStateRegistry
    {
        private readonly Dictionary<string, RuntimeSaveStateParticipant> _participantsById;
        private readonly List<RuntimeSaveStateParticipant> _participants;
        private RuntimeSaveStateRegistrySnapshot _authoritativeSnapshot;
        private RuntimeSaveStateRegistryPreparedSnapshot _activePreparation;
        private int _revision;

        public RuntimeSaveStateRegistry()
        {
            _participantsById = new Dictionary<string, RuntimeSaveStateParticipant>(StringComparer.Ordinal);
            _participants = new List<RuntimeSaveStateParticipant>();
        }

        public int Count => _participants.Count;
        public bool IsSealed => _authoritativeSnapshot != null;
        public RuntimeSaveStateRegistrySnapshot AuthoritativeSnapshot => _authoritativeSnapshot;

        public RuntimeSaveStateRegistryResult Register(RuntimeSaveStateParticipant participant)
        {
            if (participant == null)
            {
                throw new ArgumentNullException(nameof(participant));
            }

            if (IsSealed)
            {
                return Failed(
                    RuntimeSaveStateRegistryErrorCode.RegistrySealed,
                    participant.ParticipantId,
                    "The authoritative save-state registry is sealed.");
            }

            if (_participantsById.ContainsKey(participant.ParticipantId))
            {
                return Failed(
                    RuntimeSaveStateRegistryErrorCode.DuplicateParticipantId,
                    participant.ParticipantId,
                    "A save state participant with the same id is already registered.");
            }

            _participantsById.Add(participant.ParticipantId, participant);
            _participants.Add(participant);
            unchecked
            {
                _revision++;
            }
            return RuntimeSaveStateRegistryResult.Succeeded();
        }

        public RuntimeSaveStateRegistryResult Register(
            string participantId,
            int order,
            IRuntimeSaveStateProvider provider,
            IRuntimeSaveStateRestorer restorer)
        {
            return Register(new RuntimeSaveStateParticipant(participantId, order, provider, restorer));
        }

        public IReadOnlyList<RuntimeSaveStateParticipant> GetParticipantsInStableOrder()
        {
            if (_authoritativeSnapshot != null)
            {
                return _authoritativeSnapshot.Participants;
            }

            return CreateStableSnapshot();
        }

        /// <summary>Validates authoritative root-header ownership without mutating the registry.</summary>
        /// <remarks>AllocByDesign. Intended for composition-time diagnostics only.</remarks>
        public RuntimeSaveStateRegistryResult PreflightAuthoritative()
        {
            IReadOnlyList<RuntimeSaveStateParticipant> ordered = CreateStableSnapshot();
            return ValidateAuthoritative(ordered, out _);
        }

        /// <summary>Validates and freezes the authoritative participant catalog.</summary>
        /// <remarks>
        /// AllocByDesign on the first successful call. Later successful calls reuse the immutable snapshot.
        /// Registration after a successful seal returns a structured <see cref="RuntimeSaveStateRegistryErrorCode.RegistrySealed"/> error.
        /// </remarks>
        public RuntimeSaveStateRegistryResult SealAuthoritative()
        {
            RuntimeSaveStateRegistryResult prepare = PrepareAuthoritative(out RuntimeSaveStateRegistryPreparedSnapshot prepared);
            if (!prepare.Success)
            {
                return prepare;
            }

            try
            {
                return CommitAuthoritative(prepared, out _);
            }
            finally
            {
                AbortAuthoritative(prepared);
            }
        }

        internal RuntimeSaveStateRegistryResult PrepareAuthoritative(
            out RuntimeSaveStateRegistryPreparedSnapshot prepared)
        {
            prepared = null;
            if (_activePreparation != null)
            {
                return Failed(
                    RuntimeSaveStateRegistryErrorCode.CompositionInProgress,
                    RuntimeSaveStateRootHeaderProvider.ParticipantId,
                    "An authoritative save-state composition is already in progress.");
            }

            if (_authoritativeSnapshot != null)
            {
                prepared = new RuntimeSaveStateRegistryPreparedSnapshot(
                    _authoritativeSnapshot.Participants,
                    _authoritativeSnapshot.RootHeaderParticipant,
                    _revision);
                _activePreparation = prepared;
                return RuntimeSaveStateRegistryResult.Succeeded();
            }

            IReadOnlyList<RuntimeSaveStateParticipant> ordered = CreateStableSnapshot();
            RuntimeSaveStateRegistryResult validation = ValidateAuthoritative(
                ordered,
                out RuntimeSaveStateParticipant authority);
            if (!validation.Success)
            {
                return validation;
            }

            prepared = new RuntimeSaveStateRegistryPreparedSnapshot(ordered, authority, _revision);
            _activePreparation = prepared;
            return RuntimeSaveStateRegistryResult.Succeeded();
        }

        internal RuntimeSaveStateRegistryResult CommitAuthoritative(
            RuntimeSaveStateRegistryPreparedSnapshot prepared,
            out RuntimeSaveStateRegistrySnapshot snapshot)
        {
            snapshot = null;
            if (prepared == null)
            {
                throw new ArgumentNullException(nameof(prepared));
            }

            if (!ReferenceEquals(prepared, _activePreparation))
            {
                return Failed(
                    RuntimeSaveStateRegistryErrorCode.CompositionInProgress,
                    RuntimeSaveStateRootHeaderProvider.ParticipantId,
                    "The authoritative composition token is not active.");
            }

            try
            {
                if (_authoritativeSnapshot != null)
                {
                    if (prepared.Revision == _revision &&
                        ReferenceEquals(prepared.Participants, _authoritativeSnapshot.Participants))
                    {
                        snapshot = _authoritativeSnapshot;
                        return RuntimeSaveStateRegistryResult.Succeeded();
                    }

                    return Failed(
                        RuntimeSaveStateRegistryErrorCode.RegistryChangedDuringComposition,
                        RuntimeSaveStateRootHeaderProvider.ParticipantId,
                        "The authoritative registry changed before composition commit.");
                }

                if (prepared.Revision != _revision)
                {
                    return Failed(
                        RuntimeSaveStateRegistryErrorCode.RegistryChangedDuringComposition,
                        RuntimeSaveStateRootHeaderProvider.ParticipantId,
                        "The save-state participant catalog changed during authoritative composition.");
                }

                snapshot = new RuntimeSaveStateRegistrySnapshot(
                    prepared.Participants,
                    prepared.RootHeaderParticipant);
                _authoritativeSnapshot = snapshot;
                return RuntimeSaveStateRegistryResult.Succeeded();
            }
            finally
            {
                _activePreparation = null;
            }
        }

        internal void AbortAuthoritative(RuntimeSaveStateRegistryPreparedSnapshot prepared)
        {
            if (ReferenceEquals(prepared, _activePreparation))
            {
                _activePreparation = null;
            }
        }

        private ReadOnlyCollection<RuntimeSaveStateParticipant> CreateStableSnapshot()
        {
            var ordered = new List<RuntimeSaveStateParticipant>(_participants);
            ordered.Sort(CompareParticipants);
            return new ReadOnlyCollection<RuntimeSaveStateParticipant>(ordered);
        }

        private static RuntimeSaveStateRegistryResult ValidateAuthoritative(
            IReadOnlyList<RuntimeSaveStateParticipant> ordered,
            out RuntimeSaveStateParticipant authority)
        {
            authority = null;
            for (int i = 0; i < ordered.Count; i++)
            {
                RuntimeSaveStateParticipant participant = ordered[i];
                IRuntimeSaveStateProvider provider = participant.Provider;
                if (provider == null)
                {
                    continue;
                }

                if (participant.Order < RuntimeSaveStateRootHeaderProvider.ParticipantOrder)
                {
                    return Failed(
                        RuntimeSaveStateRegistryErrorCode.NegativeProviderOrder,
                        participant.ParticipantId,
                        "Capture providers cannot use a negative save-state order.");
                }

                bool reservedId = string.Equals(
                    participant.ParticipantId,
                    RuntimeSaveStateRootHeaderProvider.ParticipantId,
                    StringComparison.Ordinal);
                var rootHeaderAuthority = provider as IRuntimeSaveStateRootHeaderAuthority;

                if (rootHeaderAuthority != null && !reservedId)
                {
                    return Failed(
                        RuntimeSaveStateRegistryErrorCode.RootHeaderAuthorityIdMismatch,
                        participant.ParticipantId,
                        "A root-header authority must use the reserved participant id.");
                }

                if (reservedId)
                {
                    if (rootHeaderAuthority == null)
                    {
                        return Failed(
                            RuntimeSaveStateRegistryErrorCode.RootHeaderAuthorityContractMismatch,
                            participant.ParticipantId,
                            "The reserved root-header participant must implement the authority contract.");
                    }

                    if (participant.Order != RuntimeSaveStateRootHeaderProvider.ParticipantOrder)
                    {
                        return Failed(
                            RuntimeSaveStateRegistryErrorCode.RootHeaderAuthorityOrderMismatch,
                            participant.ParticipantId,
                            "The root-header authority must use order zero.");
                    }

                    authority = participant;
                    continue;
                }

                if (participant.Order == RuntimeSaveStateRootHeaderProvider.ParticipantOrder)
                {
                    return Failed(
                        RuntimeSaveStateRegistryErrorCode.CompetingRootHeaderProvider,
                        participant.ParticipantId,
                        "Only the reserved root-header authority may capture at order zero.");
                }
            }

            if (authority == null)
            {
                RuntimeSaveStateParticipant reservedParticipant;
                if (TryFindParticipant(ordered, RuntimeSaveStateRootHeaderProvider.ParticipantId, out reservedParticipant))
                {
                    return Failed(
                        RuntimeSaveStateRegistryErrorCode.RootHeaderAuthorityContractMismatch,
                        reservedParticipant.ParticipantId,
                        "The reserved root-header participant must provide the authority capture contract.");
                }

                return Failed(
                    RuntimeSaveStateRegistryErrorCode.MissingRootHeaderAuthority,
                    RuntimeSaveStateRootHeaderProvider.ParticipantId,
                    "The authoritative save-state registry requires one root-header authority.");
            }

            return RuntimeSaveStateRegistryResult.Succeeded();
        }

        private static bool TryFindParticipant(
            IReadOnlyList<RuntimeSaveStateParticipant> participants,
            string participantId,
            out RuntimeSaveStateParticipant participant)
        {
            for (int i = 0; i < participants.Count; i++)
            {
                RuntimeSaveStateParticipant candidate = participants[i];
                if (string.Equals(candidate.ParticipantId, participantId, StringComparison.Ordinal))
                {
                    participant = candidate;
                    return true;
                }
            }

            participant = null;
            return false;
        }

        private static RuntimeSaveStateRegistryResult Failed(
            RuntimeSaveStateRegistryErrorCode code,
            string participantId,
            string message)
        {
            return RuntimeSaveStateRegistryResult.Failed(new RuntimeSaveStateRegistryError(code, participantId, message));
        }

        private static int CompareParticipants(RuntimeSaveStateParticipant left, RuntimeSaveStateParticipant right)
        {
            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
            {
                return order;
            }

            return string.Compare(left.ParticipantId, right.ParticipantId, StringComparison.Ordinal);
        }
    }
}
