using System;

namespace MxFramework.Audio
{
    public enum AudioHandleState
    {
        Invalid = 0,
        Playing = 1,
        Stopping = 2,
        Stopped = 3,
        Released = 4
    }

    public readonly struct AudioHandle : IEquatable<AudioHandle>
    {
        public AudioHandle(int id, int eventId, int emitterId, AudioHandleState state)
        {
            Id = id;
            EventId = eventId;
            EmitterId = emitterId;
            State = state;
        }

        public int Id { get; }
        public int EventId { get; }
        public int EmitterId { get; }
        public AudioHandleState State { get; }
        public bool IsValid => Id > 0 && EventId > 0;

        public static AudioHandle Invalid => new AudioHandle(0, 0, 0, AudioHandleState.Invalid);

        public bool Equals(AudioHandle other)
        {
            return Id == other.Id && EventId == other.EventId && EmitterId == other.EmitterId;
        }

        public override bool Equals(object obj)
        {
            return obj is AudioHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id;
                hash = (hash * 397) ^ EventId;
                hash = (hash * 397) ^ EmitterId;
                return hash;
            }
        }

        public override string ToString()
        {
            return "AudioHandle Id=" + Id + " EventId=" + EventId + " EmitterId=" + EmitterId + " State=" + State;
        }
    }
}
