using System;

namespace MxFramework.Gameplay.Triggers
{
    /// <summary>Stable trigger definition identity. Zero is invalid.</summary>
    public readonly struct GameplayTriggerDefinitionId : IComparable<GameplayTriggerDefinitionId>, IEquatable<GameplayTriggerDefinitionId>
    {
        public GameplayTriggerDefinitionId(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int Value { get; }
        public bool IsValid => Value > 0;
        public int CompareTo(GameplayTriggerDefinitionId other) => Value.CompareTo(other.Value);
        public bool Equals(GameplayTriggerDefinitionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GameplayTriggerDefinitionId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? Value.ToString() : "Invalid";
        public static bool operator ==(GameplayTriggerDefinitionId left, GameplayTriggerDefinitionId right) => left.Equals(right);
        public static bool operator !=(GameplayTriggerDefinitionId left, GameplayTriggerDefinitionId right) => !left.Equals(right);
    }

    /// <summary>Stable source definition identity. Zero is invalid.</summary>
    public readonly struct GameplayTriggerSourceDefinitionId : IComparable<GameplayTriggerSourceDefinitionId>, IEquatable<GameplayTriggerSourceDefinitionId>
    {
        public GameplayTriggerSourceDefinitionId(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int Value { get; }
        public bool IsValid => Value > 0;
        public int CompareTo(GameplayTriggerSourceDefinitionId other) => Value.CompareTo(other.Value);
        public bool Equals(GameplayTriggerSourceDefinitionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GameplayTriggerSourceDefinitionId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? Value.ToString() : "Invalid";
        public static bool operator ==(GameplayTriggerSourceDefinitionId left, GameplayTriggerSourceDefinitionId right) => left.Equals(right);
        public static bool operator !=(GameplayTriggerSourceDefinitionId left, GameplayTriggerSourceDefinitionId right) => !left.Equals(right);
    }

    /// <summary>Stable route identity within a definition. Zero is invalid.</summary>
    public readonly struct GameplayTriggerRouteId : IComparable<GameplayTriggerRouteId>, IEquatable<GameplayTriggerRouteId>
    {
        public GameplayTriggerRouteId(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int Value { get; }
        public bool IsValid => Value > 0;
        public int CompareTo(GameplayTriggerRouteId other) => Value.CompareTo(other.Value);
        public bool Equals(GameplayTriggerRouteId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GameplayTriggerRouteId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? Value.ToString() : "Invalid";
        public static bool operator ==(GameplayTriggerRouteId left, GameplayTriggerRouteId right) => left.Equals(right);
        public static bool operator !=(GameplayTriggerRouteId left, GameplayTriggerRouteId right) => !left.Equals(right);
    }

    /// <summary>Stable authoring binding identity. Zero is invalid.</summary>
    public readonly struct GameplayTriggerBindingId : IComparable<GameplayTriggerBindingId>, IEquatable<GameplayTriggerBindingId>
    {
        public GameplayTriggerBindingId(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int Value { get; }
        public bool IsValid => Value > 0;
        public int CompareTo(GameplayTriggerBindingId other) => Value.CompareTo(other.Value);
        public bool Equals(GameplayTriggerBindingId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GameplayTriggerBindingId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? Value.ToString() : "Invalid";
        public static bool operator ==(GameplayTriggerBindingId left, GameplayTriggerBindingId right) => left.Equals(right);
        public static bool operator !=(GameplayTriggerBindingId left, GameplayTriggerBindingId right) => !left.Equals(right);
    }

    /// <summary>Stable authority-provider binding identity. Zero is invalid.</summary>
    public readonly struct GameplayTriggerSourceBindingId : IComparable<GameplayTriggerSourceBindingId>, IEquatable<GameplayTriggerSourceBindingId>
    {
        public GameplayTriggerSourceBindingId(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int Value { get; }
        public bool IsValid => Value > 0;
        public int CompareTo(GameplayTriggerSourceBindingId other) => Value.CompareTo(other.Value);
        public bool Equals(GameplayTriggerSourceBindingId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GameplayTriggerSourceBindingId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? Value.ToString() : "Invalid";
        public static bool operator ==(GameplayTriggerSourceBindingId left, GameplayTriggerSourceBindingId right) => left.Equals(right);
        public static bool operator !=(GameplayTriggerSourceBindingId left, GameplayTriggerSourceBindingId right) => !left.Equals(right);
    }

    /// <summary>Stable action kind identity. Zero is invalid.</summary>
    public readonly struct GameplayTriggerActionKindId : IComparable<GameplayTriggerActionKindId>, IEquatable<GameplayTriggerActionKindId>
    {
        public GameplayTriggerActionKindId(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int Value { get; }
        public bool IsValid => Value > 0;
        public int CompareTo(GameplayTriggerActionKindId other) => Value.CompareTo(other.Value);
        public bool Equals(GameplayTriggerActionKindId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GameplayTriggerActionKindId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? Value.ToString() : "Invalid";
        public static bool operator ==(GameplayTriggerActionKindId left, GameplayTriggerActionKindId right) => left.Equals(right);
        public static bool operator !=(GameplayTriggerActionKindId left, GameplayTriggerActionKindId right) => !left.Equals(right);
    }

    /// <summary>Generation-safe runtime trigger instance identity.</summary>
    public readonly struct GameplayTriggerInstanceId : IComparable<GameplayTriggerInstanceId>, IEquatable<GameplayTriggerInstanceId>
    {
        public GameplayTriggerInstanceId(int index, int generation)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if ((index == 0) != (generation == 0)) throw new ArgumentException("Instance id must be default or have positive index and generation.");
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index > 0 && Generation > 0;
        public int CompareTo(GameplayTriggerInstanceId other) { int result = Index.CompareTo(other.Index); return result != 0 ? result : Generation.CompareTo(other.Generation); }
        public bool Equals(GameplayTriggerInstanceId other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is GameplayTriggerInstanceId other && Equals(other);
        public override int GetHashCode() { unchecked { return (Index * 397) ^ Generation; } }
        public override string ToString() => IsValid ? $"{Index}:{Generation}" : "Invalid";
        public static bool operator ==(GameplayTriggerInstanceId left, GameplayTriggerInstanceId right) => left.Equals(right);
        public static bool operator !=(GameplayTriggerInstanceId left, GameplayTriggerInstanceId right) => !left.Equals(right);
    }
}
