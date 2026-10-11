using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Creates and destroys generation-based gameplay entity ids.</summary>
    public sealed class GameplayEntityLifecycle
    {
        private readonly List<Slot> _slots;
        private readonly Queue<int> _freeIndices;

        public GameplayEntityLifecycle()
        {
            _slots = new List<Slot>();
            _freeIndices = new Queue<int>();
        }

        public int CountAlive { get; private set; }
        public int CountAllocated => _slots.Count;

        /// <summary>Captures every slot generation and the FIFO reuse order for deterministic continuation.</summary>
        /// <remarks>LowFreqAlloc. The live entity projection alone is insufficient for restoration.</remarks>
        public GameplayEntityLifecycleSnapshot CaptureAllocationState()
        {
            var generations = new int[_slots.Count];
            var alive = new bool[_slots.Count];
            for (int i = 0; i < _slots.Count; i++)
            {
                generations[i] = _slots[i].Generation;
                alive[i] = _slots[i].IsAlive;
            }
            return new GameplayEntityLifecycleSnapshot(generations, alive, _freeIndices.ToArray());
        }

        /// <remarks>LowFreqAlloc. Validates the entire candidate before changing live state.</remarks>
        public void RestoreAllocationState(GameplayEntityLifecycleSnapshot snapshot)
        {
            ValidateAllocationState(snapshot);
            _slots.Clear();
            _freeIndices.Clear();
            CountAlive = 0;
            for (int i = 0; i < snapshot.Generations.Count; i++)
            {
                _slots.Add(new Slot(snapshot.Generations[i], snapshot.Alive[i]));
                if (snapshot.Alive[i]) CountAlive++;
            }
            for (int i = 0; i < snapshot.FreeIndices.Count; i++)
                _freeIndices.Enqueue(snapshot.FreeIndices[i]);
        }

        public static void ValidateAllocationState(GameplayEntityLifecycleSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Generations.Count != snapshot.Alive.Count)
                throw new ArgumentException("Allocation slot counts differ.", nameof(snapshot));
            var seen = new bool[snapshot.Generations.Count];
            int deadCount = 0;
            for (int i = 0; i < seen.Length; i++)
            {
                if (snapshot.Generations[i] <= 0)
                    throw new ArgumentException("Allocation generation must be positive.", nameof(snapshot));
                if (!snapshot.Alive[i]) deadCount++;
            }
            if (snapshot.FreeIndices.Count != deadCount)
                throw new ArgumentException("Allocation free queue must cover every dead slot.", nameof(snapshot));
            for (int i = 0; i < snapshot.FreeIndices.Count; i++)
            {
                int slot = snapshot.FreeIndices[i] - 1;
                if (slot < 0 || slot >= seen.Length || seen[slot] || snapshot.Alive[slot])
                    throw new ArgumentException("Allocation free queue contains an invalid or duplicate slot.", nameof(snapshot));
                seen[slot] = true;
            }
        }

        public GameplayEntityId Create()
        {
            int index;
            Slot slot;
            if (_freeIndices.Count > 0)
            {
                index = _freeIndices.Dequeue();
                slot = _slots[index - 1];
            }
            else
            {
                index = _slots.Count + 1;
                slot = new Slot(1, false);
                _slots.Add(slot);
            }

            slot.IsAlive = true;
            _slots[index - 1] = slot;
            CountAlive++;

            return new GameplayEntityId(index, slot.Generation);
        }

        public bool Destroy(GameplayEntityId entityId)
        {
            if (!TryGetAliveSlotIndex(entityId, out int slotIndex))
                return false;

            Slot slot = _slots[slotIndex];
            if (slot.Generation == int.MaxValue)
                throw new InvalidOperationException("Gameplay entity generation overflow.");

            slot.IsAlive = false;
            slot.Generation++;
            _slots[slotIndex] = slot;
            _freeIndices.Enqueue(slotIndex + 1);
            CountAlive--;

            return true;
        }

        public bool IsAlive(GameplayEntityId entityId)
        {
            return TryGetAliveSlotIndex(entityId, out _);
        }

        public GameplayEntityId[] CreateSnapshot()
        {
            if (CountAlive == 0)
                return Array.Empty<GameplayEntityId>();

            var snapshot = new GameplayEntityId[CountAlive];
            int snapshotIndex = 0;
            for (int slotIndex = 0; slotIndex < _slots.Count; slotIndex++)
            {
                Slot slot = _slots[slotIndex];
                if (!slot.IsAlive)
                    continue;

                snapshot[snapshotIndex++] = new GameplayEntityId(slotIndex + 1, slot.Generation);
            }

            return snapshot;
        }

        /// <summary>Copies the canonical live-entity snapshot into caller-owned storage.</summary>
        /// <remarks>NoAlloc. Entities are written in ascending entity-index order.</remarks>
        public bool TryCopySnapshot(Span<GameplayEntityId> destination, out int written)
        {
            written = 0;
            if (destination.Length < CountAlive)
                return false;

            for (int slotIndex = 0; slotIndex < _slots.Count; slotIndex++)
            {
                Slot slot = _slots[slotIndex];
                if (!slot.IsAlive)
                    continue;

                destination[written++] = new GameplayEntityId(slotIndex + 1, slot.Generation);
            }

            return true;
        }

        public void Clear()
        {
            _freeIndices.Clear();
            CountAlive = 0;

            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                if (slot.Generation == int.MaxValue)
                    throw new InvalidOperationException("Gameplay entity generation overflow.");

                slot.IsAlive = false;
                slot.Generation++;
                _slots[i] = slot;
                _freeIndices.Enqueue(i + 1);
            }
        }

        /// <summary>Imports a live-entity projection. Use RestoreAllocationState for deterministic continuation.</summary>
        public void RestoreSnapshot(IReadOnlyList<GameplayEntityId> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                _slots.Clear(); _freeIndices.Clear(); CountAlive = 0;
                return;
            }

            int maxIndex = 0;
            var seen = new HashSet<GameplayEntityId>();
            var seenIndices = new HashSet<int>();
            for (int i = 0; i < entities.Count; i++)
            {
                GameplayEntityId entityId = entities[i];
                if (!entityId.IsValid)
                    throw new ArgumentException("Gameplay entity restore snapshot contains an invalid entity id.", nameof(entities));
                if (!seen.Add(entityId))
                    throw new ArgumentException("Gameplay entity restore snapshot contains a duplicate entity id.", nameof(entities));
                if (!seenIndices.Add(entityId.Index))
                    throw new ArgumentException("Gameplay entity restore snapshot contains a duplicate entity index.", nameof(entities));
                if (entityId.Index > maxIndex)
                    maxIndex = entityId.Index;
            }

            _slots.Clear();
            _freeIndices.Clear();
            CountAlive = 0;
            for (int i = 0; i < maxIndex; i++)
            {
                _slots.Add(new Slot(1, false));
                _freeIndices.Enqueue(i + 1);
            }

            for (int i = 0; i < entities.Count; i++)
            {
                GameplayEntityId entityId = entities[i];
                int slotIndex = entityId.Index - 1;
                _slots[slotIndex] = new Slot(entityId.Generation, true);
                RemoveFreeIndex(entityId.Index);
                CountAlive++;
            }
        }

        private void RemoveFreeIndex(int index)
        {
            int count = _freeIndices.Count;
            for (int i = 0; i < count; i++)
            {
                int candidate = _freeIndices.Dequeue();
                if (candidate != index)
                    _freeIndices.Enqueue(candidate);
            }
        }

        private bool TryGetAliveSlotIndex(GameplayEntityId entityId, out int slotIndex)
        {
            slotIndex = entityId.Index - 1;
            if (!entityId.IsValid || slotIndex < 0 || slotIndex >= _slots.Count)
                return false;

            Slot slot = _slots[slotIndex];
            return slot.IsAlive && slot.Generation == entityId.Generation;
        }

        private struct Slot
        {
            public Slot(int generation, bool isAlive)
            {
                Generation = generation;
                IsAlive = isAlive;
            }

            public int Generation;
            public bool IsAlive;
        }
    }

    /// <summary>Immutable local/save allocation state; slot index is its position plus one.</summary>
    public sealed class GameplayEntityLifecycleSnapshot
    {
        public GameplayEntityLifecycleSnapshot(IReadOnlyList<int> generations, IReadOnlyList<bool> alive,
            IReadOnlyList<int> freeIndices)
        {
            if (generations == null || alive == null || freeIndices == null)
                throw new ArgumentException("Allocation state collections must be present.");
            Generations = Copy(generations);
            Alive = Copy(alive);
            FreeIndices = Copy(freeIndices);
        }

        public IReadOnlyList<int> Generations { get; }
        public IReadOnlyList<bool> Alive { get; }
        public IReadOnlyList<int> FreeIndices { get; }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values)
        {
            var copy = new T[values.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = values[i];
            return Array.AsReadOnly(copy);
        }
    }
}
