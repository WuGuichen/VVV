using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayAttributeSetComponent : IGameplayComponent, IEquatable<GameplayAttributeSetComponent>
    {
        private const int InlineCapacity = 4;

        private readonly GameplayAttributeValue[] _values;
        private readonly GameplayAttributeValue _value0;
        private readonly GameplayAttributeValue _value1;
        private readonly GameplayAttributeValue _value2;
        private readonly GameplayAttributeValue _value3;
        private readonly int _count;

        public GameplayAttributeSetComponent(params GameplayAttributeValue[] values)
            : this(CopySorted(values), takeOwnership: true)
        {
        }

        private GameplayAttributeSetComponent(GameplayAttributeValue[] sortedValues, bool takeOwnership)
        {
            GameplayAttributeValue[] values = takeOwnership
                ? sortedValues ?? Array.Empty<GameplayAttributeValue>()
                : CopySorted(sortedValues);
            _count = values.Length;
            if (_count > InlineCapacity)
            {
                _values = values;
                _value0 = default;
                _value1 = default;
                _value2 = default;
                _value3 = default;
                return;
            }

            _values = null;
            _value0 = _count > 0 ? values[0] : default;
            _value1 = _count > 1 ? values[1] : default;
            _value2 = _count > 2 ? values[2] : default;
            _value3 = _count > 3 ? values[3] : default;
        }

        private GameplayAttributeSetComponent(
            int count,
            GameplayAttributeValue value0,
            GameplayAttributeValue value1,
            GameplayAttributeValue value2,
            GameplayAttributeValue value3)
        {
            _count = count;
            _values = null;
            _value0 = value0;
            _value1 = value1;
            _value2 = value2;
            _value3 = value3;
        }

        public int Count => _count;

        public bool TryGet(int attributeId, out GameplayAttributeValue value)
        {
            int index = FindIndex(attributeId);
            if (index >= 0)
            {
                value = GetValueAt(index);
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
            int index = FindIndex(attributeId);
            int currentValue = index >= 0 ? GetValueAt(index).CurrentValue : baseValue;
            return Upsert(index, new GameplayAttributeValue(attributeId, baseValue, currentValue));
        }

        public GameplayAttributeSetComponent SetCurrentValue(int attributeId, int currentValue)
        {
            ValidateAttributeId(attributeId);
            int index = FindIndex(attributeId);
            int baseValue = index >= 0 ? GetValueAt(index).BaseValue : currentValue;
            return Upsert(index, new GameplayAttributeValue(attributeId, baseValue, currentValue));
        }

        public GameplayAttributeSetComponent AddCurrentValue(int attributeId, int delta)
        {
            ValidateAttributeId(attributeId);
            int index = FindIndex(attributeId);
            if (index < 0)
                return Upsert(index, new GameplayAttributeValue(attributeId, 0, delta));

            GameplayAttributeValue existing = GetValueAt(index);
            return Upsert(index, new GameplayAttributeValue(
                attributeId,
                existing.BaseValue,
                checked(existing.CurrentValue + delta)));
        }

        internal GameplayAttributeSetComponent WithValue(GameplayAttributeValue value)
        {
            ValidateAttributeId(value.AttributeId);
            return Upsert(FindIndex(value.AttributeId), value);
        }

        public GameplayAttributeValue[] ToArray()
        {
            if (_count == 0)
                return Array.Empty<GameplayAttributeValue>();

            var copy = new GameplayAttributeValue[_count];
            if (_values != null)
            {
                Array.Copy(_values, copy, _count);
                return copy;
            }

            for (int i = 0; i < _count; i++)
                copy[i] = GetValueAt(i);
            return copy;
        }

        public bool Equals(GameplayAttributeSetComponent other)
        {
            int count = Count;
            if (count != other.Count)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (!GetValueAt(i).Equals(other.GetValueAt(i)))
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
                    hash = (hash * 397) ^ GetValueAt(i).GetHashCode();
                return hash;
            }
        }

        private GameplayAttributeSetComponent Upsert(int existingIndex, GameplayAttributeValue value)
        {
            int count = Count;
            if (count == 0)
            {
                return CreateInline(
                    1,
                    value,
                    default,
                    default,
                    default);
            }

            if (existingIndex >= 0)
            {
                if (GetValueAt(existingIndex).Equals(value))
                    return this;

                if (_values == null)
                    return ReplaceInline(existingIndex, value);

                var replacement = new GameplayAttributeValue[count];
                Array.Copy(_values, replacement, count);
                replacement[existingIndex] = value;
                return new GameplayAttributeSetComponent(replacement, takeOwnership: true);
            }

            if (_values == null && count < InlineCapacity)
                return InsertInline(value);

            return InsertArray(value);
        }

        private GameplayAttributeSetComponent ReplaceInline(
            int index,
            GameplayAttributeValue value)
        {
            GameplayAttributeValue value0 = _value0;
            GameplayAttributeValue value1 = _value1;
            GameplayAttributeValue value2 = _value2;
            GameplayAttributeValue value3 = _value3;
            AssignInline(
                ref value0,
                ref value1,
                ref value2,
                ref value3,
                index,
                value);
            return CreateInline(_count, value0, value1, value2, value3);
        }

        private GameplayAttributeSetComponent InsertInline(GameplayAttributeValue value)
        {
            int insertIndex = FindInsertIndex(value.AttributeId);
            GameplayAttributeValue value0 = default;
            GameplayAttributeValue value1 = default;
            GameplayAttributeValue value2 = default;
            GameplayAttributeValue value3 = default;
            int read = 0;
            int newCount = _count + 1;
            for (int write = 0; write < newCount; write++)
            {
                GameplayAttributeValue next;
                if (write == insertIndex)
                {
                    next = value;
                }
                else
                {
                    next = GetValueAt(read);
                    read++;
                }

                AssignInline(
                    ref value0,
                    ref value1,
                    ref value2,
                    ref value3,
                    write,
                    next);
            }

            return CreateInline(newCount, value0, value1, value2, value3);
        }

        private GameplayAttributeSetComponent InsertArray(GameplayAttributeValue value)
        {
            int count = Count;
            var copy = new GameplayAttributeValue[count + 1];
            int write = 0;
            bool inserted = false;
            for (int i = 0; i < count; i++)
            {
                GameplayAttributeValue current = GetValueAt(i);
                if (!inserted && value.AttributeId < current.AttributeId)
                {
                    copy[write++] = value;
                    inserted = true;
                }

                copy[write++] = current;
            }

            if (!inserted)
                copy[write++] = value;

            return new GameplayAttributeSetComponent(copy, takeOwnership: true);
        }

        private int FindIndex(int attributeId)
        {
            if (attributeId <= 0 || _count == 0)
                return -1;

            int left = 0;
            int right = _count - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                int id = GetValueAt(mid).AttributeId;
                if (id == attributeId)
                    return mid;
                if (id < attributeId)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            return -1;
        }

        private int FindInsertIndex(int attributeId)
        {
            int index = 0;
            while (index < _count && GetValueAt(index).AttributeId < attributeId)
                index++;
            return index;
        }

        private GameplayAttributeValue GetValueAt(int index)
        {
            if (_values != null)
                return _values[index];

            switch (index)
            {
                case 0:
                    return _value0;
                case 1:
                    return _value1;
                case 2:
                    return _value2;
                case 3:
                    return _value3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static GameplayAttributeSetComponent CreateInline(
            int count,
            GameplayAttributeValue value0,
            GameplayAttributeValue value1,
            GameplayAttributeValue value2,
            GameplayAttributeValue value3)
        {
            return new GameplayAttributeSetComponent(
                count,
                value0,
                value1,
                value2,
                value3);
        }

        private static void AssignInline(
            ref GameplayAttributeValue value0,
            ref GameplayAttributeValue value1,
            ref GameplayAttributeValue value2,
            ref GameplayAttributeValue value3,
            int index,
            GameplayAttributeValue value)
        {
            switch (index)
            {
                case 0:
                    value0 = value;
                    break;
                case 1:
                    value1 = value;
                    break;
                case 2:
                    value2 = value;
                    break;
                case 3:
                    value3 = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
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
