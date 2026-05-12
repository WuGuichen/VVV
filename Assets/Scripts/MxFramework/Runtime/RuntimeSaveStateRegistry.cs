using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
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
        {
            ParticipantId = participantId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string ParticipantId { get; }
        public string Message { get; }

        public override string ToString()
        {
            return "Participant='" + ParticipantId + "' " + Message;
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

    public sealed class RuntimeSaveStateRegistry
    {
        private readonly Dictionary<string, RuntimeSaveStateParticipant> _participantsById;
        private readonly List<RuntimeSaveStateParticipant> _participants;

        public RuntimeSaveStateRegistry()
        {
            _participantsById = new Dictionary<string, RuntimeSaveStateParticipant>(StringComparer.Ordinal);
            _participants = new List<RuntimeSaveStateParticipant>();
        }

        public int Count => _participants.Count;

        public RuntimeSaveStateRegistryResult Register(RuntimeSaveStateParticipant participant)
        {
            if (participant == null)
            {
                throw new ArgumentNullException(nameof(participant));
            }

            if (_participantsById.ContainsKey(participant.ParticipantId))
            {
                return RuntimeSaveStateRegistryResult.Failed(new RuntimeSaveStateRegistryError(
                    participant.ParticipantId,
                    "A save state participant with the same id is already registered."));
            }

            _participantsById.Add(participant.ParticipantId, participant);
            _participants.Add(participant);
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
            var ordered = new List<RuntimeSaveStateParticipant>(_participants);
            ordered.Sort(CompareParticipants);
            return new ReadOnlyCollection<RuntimeSaveStateParticipant>(ordered);
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
