using System;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Physics
{
    /// <summary>
    /// Caller-owned per-frame physical contact event buffer.
    /// </summary>
    /// <remarks>NoAlloc after construction; overflowing Add increments DroppedCount and returns false.</remarks>
    public sealed class CombatPhysicalContactEventBuffer
    {
        private readonly CombatPhysicalContactEvent[] _events;

        public CombatPhysicalContactEventBuffer(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Contact event buffer capacity cannot be negative.");
            }

            _events = capacity == 0
                ? Array.Empty<CombatPhysicalContactEvent>()
                : new CombatPhysicalContactEvent[capacity];
        }

        public int Capacity => _events.Length;

        public int Count { get; private set; }

        public int DroppedCount { get; private set; }

        public CombatPhysicalContactEvent this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _events[index];
            }
        }

        public void Clear()
        {
            Count = 0;
            DroppedCount = 0;
        }

        public bool Add(in CombatPhysicalContactEvent contactEvent)
        {
            if (Count >= _events.Length)
            {
                DroppedCount++;
                return false;
            }

            _events[Count] = contactEvent;
            Count++;
            return true;
        }

        public CombatHash ContributeHash(CombatHash hash)
        {
            hash = hash.Add(Count).Add(DroppedCount);
            for (int i = 0; i < Count; i++)
            {
                hash = _events[i].ContributeHash(hash);
            }

            return hash;
        }

        public CombatHash ComputeHash()
        {
            return ContributeHash(CombatHash.Empty);
        }
    }
}
