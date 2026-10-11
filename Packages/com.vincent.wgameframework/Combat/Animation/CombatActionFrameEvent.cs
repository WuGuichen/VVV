using System;

namespace MxFramework.Combat.Animation
{
    public readonly struct CombatActionFrameEvent : IComparable<CombatActionFrameEvent>, IEquatable<CombatActionFrameEvent>
    {
        public CombatActionFrameEvent(int frame, int eventId, int sourceOrder = 0, int intPayload = 0)
        {
            if (frame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame event cannot be negative.");
            }

            if (eventId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(eventId), "Event id cannot be negative.");
            }

            if (sourceOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOrder), "Source order cannot be negative.");
            }

            Frame = frame;
            EventId = eventId;
            SourceOrder = sourceOrder;
            IntPayload = intPayload;
        }

        public int Frame { get; }

        public int EventId { get; }

        public int SourceOrder { get; }

        public int IntPayload { get; }

        public int CompareTo(CombatActionFrameEvent other)
        {
            int compare = Frame.CompareTo(other.Frame);
            if (compare != 0)
            {
                return compare;
            }

            compare = SourceOrder.CompareTo(other.SourceOrder);
            if (compare != 0)
            {
                return compare;
            }

            return EventId.CompareTo(other.EventId);
        }

        public bool Equals(CombatActionFrameEvent other)
        {
            return Frame == other.Frame
                && EventId == other.EventId
                && SourceOrder == other.SourceOrder
                && IntPayload == other.IntPayload;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatActionFrameEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame;
                hash = (hash * 397) ^ EventId;
                hash = (hash * 397) ^ SourceOrder;
                hash = (hash * 397) ^ IntPayload;
                return hash;
            }
        }
    }
}
