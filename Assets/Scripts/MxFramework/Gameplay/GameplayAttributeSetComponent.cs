using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayAttributeSetComponent : IGameplayComponent, IEquatable<GameplayAttributeSetComponent>
    {
        private readonly GameplayAttributeValue[] _values;

        public GameplayAttributeSetComponent(params GameplayAttributeValue[] values)
        {
            _values = CopySorted(values);
        }

        public int Count => _values == null ? 0 : _values.Length;

        public bool TryGet(int attributeId, out GameplayAttributeValue value)
        {
            int index = FindIndex(attributeId);
            if (index >= 0)
            {
                value = _values[index];
                return true;
            }

            value = default;
            return false;
        }

        public int GetCurrentValueOrDefault(int attributeId)
        {
            return TryGet(attributeId, out GameplayAttributeValue value) ? value.CurrentValue : 0;
        }

        public GameplayAttributeSetComponent SetBaseValue(int attributeId, int baseValue)
        {
            ValidateAttributeId(attributeId);
            GameplayAttributeValue existing;
            int currentValue = TryGet(attributeId, out existing) ? existing.CurrentValue : baseValue;
            return Upsert(new GameplayAttributeValue(attributeId, baseValue, currentValue));
        }

        public GameplayAttributeSetComponent SetCurrentValue(int attributeId, int currentValue)
        {
            ValidateAttributeId(attributeId);
            GameplayAttributeValue existing;
            int baseValue = TryGet(attributeId, out existing) ? existing.BaseValue : currentValue;
            return Upsert(new GameplayAttributeValue(attributeId, baseValue, currentValue));
        }

        public GameplayAttributeSetComponent AddCurrentValue(int attributeId, int delta)
        {
            ValidateAttributeId(attributeId);
            GameplayAttributeValue existing;
            if (!TryGet(attributeId, out existing))
                return Upsert(new GameplayAttributeValue(attributeId, 0, delta));

            return Upsert(new GameplayAttributeValue(
                attributeId,
                existing.BaseValue,
                checked(existing.CurrentValue + delta)));
        }

        public GameplayAttributeValue[] ToArray()
        {
            if (_values == null || _values.Length == 0)
                return Array.Empty<GameplayAttributeValue>();

            var copy = new GameplayAttributeValue[_values.Length];
            Array.Copy(_values, copy, _values.Length);
            return copy;
        }

        public bool Equals(GameplayAttributeSetComponent other)
        {
            int count = Count;
            if (count != other.Count)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (!_values[i].Equals(other._values[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayAttributeSetComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Count;
                for (int i = 0; i < Count; i++)
                    hash = (hash * 397) ^ _values[i].GetHashCode();
                return hash;
            }
        }

        private GameplayAttributeSetComponent Upsert(GameplayAttributeValue value)
        {
            int count = Count;
            if (count == 0)
                return new GameplayAttributeSetComponent(value);

            var copy = new GameplayAttributeValue[count + 1];
            int write = 0;
            bool inserted = false;
            for (int i = 0; i < count; i++)
            {
                GameplayAttributeValue current = _values[i];
                if (current.AttributeId == value.AttributeId)
                {
                    copy[write++] = value;
                    inserted = true;
                    continue;
                }

                if (!inserted && value.AttributeId < current.AttributeId)
                {
                    copy[write++] = value;
                    inserted = true;
                }

                copy[write++] = current;
            }

            if (!inserted)
                copy[write++] = value;

            if (write != copy.Length)
                Array.Resize(ref copy, write);

            return new GameplayAttributeSetComponent(copy);
        }

        private int FindIndex(int attributeId)
        {
            if (attributeId <= 0 || _values == null)
                return -1;

            int left = 0;
            int right = _values.Length - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                int id = _values[mid].AttributeId;
                if (id == attributeId)
                    return mid;
                if (id < attributeId)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            return -1;
        }

        private static GameplayAttributeValue[] CopySorted(GameplayAttributeValue[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<GameplayAttributeValue>();

            var copy = new GameplayAttributeValue[values.Length];
            Array.Copy(values, copy, values.Length);
            Array.Sort(copy, CompareValues);
            for (int i = 0; i < copy.Length; i++)
            {
                ValidateAttributeId(copy[i].AttributeId);
                if (i > 0 && copy[i - 1].AttributeId == copy[i].AttributeId)
                    throw new ArgumentException("Gameplay attribute set cannot contain duplicate attribute ids.", nameof(values));
            }

            return copy;
        }

        private static int CompareValues(GameplayAttributeValue left, GameplayAttributeValue right)
        {
            return left.AttributeId.CompareTo(right.AttributeId);
        }

        private static void ValidateAttributeId(int attributeId)
        {
            if (attributeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(attributeId), "Gameplay attribute id must be greater than zero.");
        }
    }
}
