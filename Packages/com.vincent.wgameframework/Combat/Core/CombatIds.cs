using System;

namespace MxFramework.Combat.Core
{
    public readonly struct CombatEntityId : IComparable<CombatEntityId>, IEquatable<CombatEntityId>
    {
        public static readonly CombatEntityId None = new CombatEntityId(0);

        public CombatEntityId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Combat entity id cannot be negative.");
            }

            Value = value;
        }

        public int Value { get; }

        public bool IsNone => Value == 0;

        public int CompareTo(CombatEntityId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(CombatEntityId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatEntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public readonly struct CombatBodyId : IComparable<CombatBodyId>, IEquatable<CombatBodyId>
    {
        public static readonly CombatBodyId None = new CombatBodyId(0);

        public CombatBodyId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Combat body id cannot be negative.");
            }

            Value = value;
        }

        public int Value { get; }

        public bool IsNone => Value == 0;

        public int CompareTo(CombatBodyId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(CombatBodyId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatBodyId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public readonly struct CombatColliderId : IComparable<CombatColliderId>, IEquatable<CombatColliderId>
    {
        public static readonly CombatColliderId None = new CombatColliderId(0);

        public CombatColliderId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Combat collider id cannot be negative.");
            }

            Value = value;
        }

        public int Value { get; }

        public bool IsNone => Value == 0;

        public int CompareTo(CombatColliderId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(CombatColliderId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatColliderId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
