using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestCooldownAdmission
    {
        Available = 0,
        Cooldown = 1,
        CapacityFull = 2,
    }

    /// <summary>Fixed-capacity cooldown lookup that only reuses free or expired storage.</summary>
    /// <remarks>
    /// NoAlloc after construction. An active entry is never evicted: a full active window rejects a new key until
    /// at least one slot expires, preserving cooldown correctness under bursts larger than the configured capacity.
    /// </remarks>
    internal sealed class CharacterTestFixedCooldownCache<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        private readonly Dictionary<TKey, int> _slotByKey;
        private readonly TKey[] _keys;
        private readonly long[] _expiresAt;
        private readonly bool[] _occupied;

        public CharacterTestFixedCooldownCache(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _slotByKey = new Dictionary<TKey, int>(capacity);
            _keys = capacity == 0 ? Array.Empty<TKey>() : new TKey[capacity];
            _expiresAt = capacity == 0 ? Array.Empty<long>() : new long[capacity];
            _occupied = capacity == 0 ? Array.Empty<bool>() : new bool[capacity];
        }

        public int Count => _slotByKey.Count;

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetValue(TKey key, out long frame)
        {
            if (_slotByKey.TryGetValue(key, out int slot))
            {
                frame = _expiresAt[slot];
                return true;
            }

            frame = 0L;
            return false;
        }

        /// <remarks>NoAlloc.</remarks>
        public CharacterTestCooldownAdmission Evaluate(TKey key, long frame, int cooldownFrames)
        {
            if (_slotByKey.TryGetValue(key, out int existingSlot))
            {
                return frame < _expiresAt[existingSlot]
                    ? CharacterTestCooldownAdmission.Cooldown
                    : CharacterTestCooldownAdmission.Available;
            }

            return FindAvailableOrExpiredSlot(frame, cooldownFrames) >= 0
                ? CharacterTestCooldownAdmission.Available
                : CharacterTestCooldownAdmission.CapacityFull;
        }

        /// <remarks>NoAlloc after construction. Call only after <see cref="Evaluate"/> returns Available.</remarks>
        public bool Record(TKey key, long frame, int cooldownFrames)
        {
            if (_keys.Length == 0)
                return false;

            if (_slotByKey.TryGetValue(key, out int existingSlot))
            {
                _expiresAt[existingSlot] = SaturatingAdd(frame, cooldownFrames);
                return true;
            }

            int targetSlot = FindAvailableOrExpiredSlot(frame, cooldownFrames);
            if (targetSlot < 0)
                return false;
            if (_occupied[targetSlot])
                _slotByKey.Remove(_keys[targetSlot]);

            _keys[targetSlot] = key;
            _expiresAt[targetSlot] = SaturatingAdd(frame, cooldownFrames);
            _occupied[targetSlot] = true;
            _slotByKey.Add(key, targetSlot);
            return true;
        }

        public void Clear()
        {
            _slotByKey.Clear();
            Array.Clear(_occupied, 0, _occupied.Length);
        }

        private int FindAvailableOrExpiredSlot(long frame, int cooldownFrames)
        {
            int oldestSlot = -1;
            long oldestExpiry = long.MaxValue;
            for (int i = 0; i < _occupied.Length; i++)
            {
                if (!_occupied[i])
                    return i;
                if (frame >= _expiresAt[i] && _expiresAt[i] < oldestExpiry)
                {
                    oldestExpiry = _expiresAt[i];
                    oldestSlot = i;
                }
            }

            return oldestSlot;
        }

        private static long SaturatingAdd(long frame, int cooldownFrames)
        {
            long duration = cooldownFrames > 0 ? cooldownFrames : 0L;
            return frame > long.MaxValue - duration ? long.MaxValue : frame + duration;
        }
    }

}
