using System;
using System.Collections.Generic;

namespace MxFramework.Rendering
{
    public readonly struct MxRenderSubjectId : IEquatable<MxRenderSubjectId>
    {
        public MxRenderSubjectId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;

        public bool Equals(MxRenderSubjectId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is MxRenderSubjectId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return IsValid ? Value.ToString() : "Invalid";
        }

        public static bool operator ==(MxRenderSubjectId left, MxRenderSubjectId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxRenderSubjectId left, MxRenderSubjectId right)
        {
            return !left.Equals(right);
        }
    }

    public enum MxRenderSubjectRole
    {
        None = 0,
        Primary = 1,
        LocalControlled = 2,
        Focus = 3,
        Tracked = 4
    }

    public interface IRenderSubjectMap<TSourceId>
    {
        bool TryResolve(TSourceId sourceId, out MxRenderSubjectId subject);
        MxRenderSubjectId GetOrCreate(TSourceId sourceId, MxRenderSubjectRole role);
        bool Release(TSourceId sourceId);
    }

    public sealed class MxRenderSubjectRegistry
    {
        private const int SlotBits = 20;
        private const int SlotMask = (1 << SlotBits) - 1;
        private const int InitialGeneration = 1;
        public const int MaxSubjectSlots = SlotMask;

        private readonly int _maxSubjectSlots;
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly Queue<int> _freeSlots = new Queue<int>();

        internal event Action<MxRenderSubjectId> SubjectReleased;

        public int ActiveCount { get; private set; }

        public MxRenderSubjectRegistry(int maxSubjectSlots = MaxSubjectSlots)
        {
            if (maxSubjectSlots <= 0 || maxSubjectSlots > MaxSubjectSlots)
                throw new ArgumentOutOfRangeException(nameof(maxSubjectSlots), maxSubjectSlots, "Subject slot capacity must be between 1 and MxRenderSubjectRegistry.MaxSubjectSlots.");

            _maxSubjectSlots = maxSubjectSlots;
        }

        public MxRenderSubjectId Register(MxRenderSubjectRole role)
        {
            int slotIndex;
            Slot slot;
            if (_freeSlots.Count > 0)
            {
                slotIndex = _freeSlots.Dequeue();
                slot = _slots[slotIndex];
            }
            else
            {
                if (_slots.Count >= _maxSubjectSlots)
                    throw new InvalidOperationException("MxRenderSubjectRegistry subject slot capacity exceeded.");

                slotIndex = _slots.Count;
                slot = new Slot { Generation = InitialGeneration };
                _slots.Add(slot);
            }

            slot.Active = true;
            slot.Reusable = false;
            slot.Role = role;
            slot.ReferenceCount = 0;
            _slots[slotIndex] = slot;
            ActiveCount++;
            return Encode(slotIndex, slot.Generation);
        }

        public bool TryResolve(MxRenderSubjectId subject, out MxRenderSubjectRegistration registration)
        {
            if (!TryDecode(subject, out int slotIndex, out int generation))
            {
                registration = default;
                return false;
            }

            Slot slot = _slots[slotIndex];
            if (!slot.Active || slot.Generation != generation)
            {
                registration = default;
                return false;
            }

            registration = new MxRenderSubjectRegistration(subject, slot.Role);
            return true;
        }

        public bool Release(MxRenderSubjectId subject)
        {
            if (!TryDecode(subject, out int slotIndex, out int generation))
                return false;

            Slot slot = _slots[slotIndex];
            if (!slot.Active || slot.Generation != generation)
                return false;

            slot.Active = false;
            slot.Role = MxRenderSubjectRole.None;
            ActiveCount--;
            _slots[slotIndex] = slot;

            SubjectReleased?.Invoke(subject);

            slot = _slots[slotIndex];
            slot.Generation = NextGeneration(slot.Generation);
            _slots[slotIndex] = slot;
            TryMarkReusable(slotIndex);
            return true;
        }

        public void Clear()
        {
            var released = new List<MxRenderSubjectId>();
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                if (!slot.Active)
                    continue;

                released.Add(Encode(i, slot.Generation));
                slot.Active = false;
                slot.Role = MxRenderSubjectRole.None;
                _slots[i] = slot;
            }

            ActiveCount = 0;
            for (int i = 0; i < released.Count; i++)
                SubjectReleased?.Invoke(released[i]);

            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                if (slot.Active)
                    continue;

                slot.Generation = NextGeneration(slot.Generation);
                _slots[i] = slot;
            }

            _freeSlots.Clear();
            for (int i = 0; i < _slots.Count; i++)
                TryMarkReusable(i);
        }

        public RenderSubjectMap<TSourceId> CreateMap<TSourceId>()
        {
            return new RenderSubjectMap<TSourceId>(this);
        }

        internal bool AddReference(MxRenderSubjectId subject)
        {
            if (!TryDecode(subject, out int slotIndex, out int generation))
                return false;

            Slot slot = _slots[slotIndex];
            if (!slot.Active || slot.Generation != generation)
                return false;

            slot.ReferenceCount++;
            _slots[slotIndex] = slot;
            return true;
        }

        internal bool ReleaseReference(MxRenderSubjectId subject)
        {
            if (!TryDecode(subject, out int slotIndex, out int generation))
                return false;

            Slot slot = _slots[slotIndex];
            if (slot.Generation != generation || slot.ReferenceCount <= 0)
                return false;

            slot.ReferenceCount--;
            _slots[slotIndex] = slot;
            TryMarkReusable(slotIndex);
            return true;
        }

        private void TryMarkReusable(int slotIndex)
        {
            Slot slot = _slots[slotIndex];
            if (slot.Active || slot.Reusable || slot.ReferenceCount > 0)
                return;

            slot.Reusable = true;
            _slots[slotIndex] = slot;
            _freeSlots.Enqueue(slotIndex);
        }

        private static MxRenderSubjectId Encode(int slotIndex, int generation)
        {
            return new MxRenderSubjectId((generation << SlotBits) | (slotIndex + 1));
        }

        private bool TryDecode(MxRenderSubjectId subject, out int slotIndex, out int generation)
        {
            slotIndex = -1;
            generation = 0;
            if (!subject.IsValid)
                return false;

            int encodedSlot = subject.Value & SlotMask;
            generation = subject.Value >> SlotBits;
            slotIndex = encodedSlot - 1;
            return generation > 0 && slotIndex >= 0 && slotIndex < _slots.Count;
        }

        private static int NextGeneration(int generation)
        {
            if (generation == int.MaxValue >> SlotBits)
                throw new InvalidOperationException("MxRenderSubjectId generation overflow.");

            return generation + 1;
        }

        private struct Slot
        {
            public int Generation;
            public int ReferenceCount;
            public bool Active;
            public bool Reusable;
            public MxRenderSubjectRole Role;
        }
    }

    public readonly struct MxRenderSubjectRegistration
    {
        public MxRenderSubjectRegistration(MxRenderSubjectId subject, MxRenderSubjectRole role)
        {
            Subject = subject;
            Role = role;
        }

        public MxRenderSubjectId Subject { get; }
        public MxRenderSubjectRole Role { get; }
    }

    public sealed class RenderSubjectMap<TSourceId> : IRenderSubjectMap<TSourceId>
    {
        private readonly MxRenderSubjectRegistry _registry;
        private readonly Dictionary<TSourceId, MxRenderSubjectId> _sourceToSubject = new Dictionary<TSourceId, MxRenderSubjectId>();
        private readonly Dictionary<MxRenderSubjectId, TSourceId> _subjectToSource = new Dictionary<MxRenderSubjectId, TSourceId>();

        public RenderSubjectMap(MxRenderSubjectRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _registry.SubjectReleased += OnSubjectReleased;
        }

        public int Count => _sourceToSubject.Count;

        public bool TryResolve(TSourceId sourceId, out MxRenderSubjectId subject)
        {
            if (!_sourceToSubject.TryGetValue(sourceId, out subject))
                return false;

            if (_registry.TryResolve(subject, out var _))
                return true;

            _sourceToSubject.Remove(sourceId);
            _subjectToSource.Remove(subject);
            subject = default;
            return false;
        }

        public MxRenderSubjectId GetOrCreate(TSourceId sourceId, MxRenderSubjectRole role)
        {
            if (TryResolve(sourceId, out MxRenderSubjectId existing))
                return existing;

            MxRenderSubjectId subject = _registry.Register(role);
            _sourceToSubject[sourceId] = subject;
            _subjectToSource[subject] = sourceId;
            return subject;
        }

        public bool Release(TSourceId sourceId)
        {
            if (!_sourceToSubject.TryGetValue(sourceId, out MxRenderSubjectId subject))
                return false;

            _sourceToSubject.Remove(sourceId);
            _subjectToSource.Remove(subject);
            return _registry.Release(subject);
        }

        public void Clear()
        {
            var subjects = new List<MxRenderSubjectId>(_subjectToSource.Keys);
            _sourceToSubject.Clear();
            _subjectToSource.Clear();

            for (int i = 0; i < subjects.Count; i++)
                _registry.Release(subjects[i]);
        }

        private void OnSubjectReleased(MxRenderSubjectId subject)
        {
            if (!_subjectToSource.TryGetValue(subject, out TSourceId sourceId))
                return;

            _subjectToSource.Remove(subject);
            _sourceToSubject.Remove(sourceId);
        }
    }
}
