using System;
using System.Globalization;

namespace MxFramework.CharacterApplication
{
    internal static class CharacterApplicationIdFormat
    {
        public static bool IsValid(int value)
        {
            return value > 0;
        }

        public static string Format(int value, string format, IFormatProvider formatProvider)
        {
            return IsValid(value)
                ? value.ToString(format, formatProvider ?? CultureInfo.InvariantCulture)
                : string.Empty;
        }
    }

    public readonly struct CharacterConfigId : IEquatable<CharacterConfigId>, IFormattable
    {
        public CharacterConfigId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(CharacterConfigId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is CharacterConfigId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct CharacterAttributeProfileId : IEquatable<CharacterAttributeProfileId>, IFormattable
    {
        public CharacterAttributeProfileId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(CharacterAttributeProfileId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is CharacterAttributeProfileId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct CharacterBodyProfileId : IEquatable<CharacterBodyProfileId>, IFormattable
    {
        public CharacterBodyProfileId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(CharacterBodyProfileId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is CharacterBodyProfileId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct CharacterBodyPartConfigId : IEquatable<CharacterBodyPartConfigId>, IFormattable
    {
        public CharacterBodyPartConfigId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(CharacterBodyPartConfigId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is CharacterBodyPartConfigId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct EquipmentSchemaId : IEquatable<EquipmentSchemaId>, IFormattable
    {
        public EquipmentSchemaId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(EquipmentSchemaId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is EquipmentSchemaId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct EquipmentLoadoutId : IEquatable<EquipmentLoadoutId>, IFormattable
    {
        public EquipmentLoadoutId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(EquipmentLoadoutId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is EquipmentLoadoutId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct EquipmentStateId : IEquatable<EquipmentStateId>, IFormattable
    {
        public EquipmentStateId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(EquipmentStateId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is EquipmentStateId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct WeaponConfigId : IEquatable<WeaponConfigId>, IFormattable
    {
        public WeaponConfigId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(WeaponConfigId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is WeaponConfigId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct AbilityLoadoutId : IEquatable<AbilityLoadoutId>, IFormattable
    {
        public AbilityLoadoutId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(AbilityLoadoutId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is AbilityLoadoutId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct CombatActionSetId : IEquatable<CombatActionSetId>, IFormattable
    {
        public CombatActionSetId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(CombatActionSetId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is CombatActionSetId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct CharacterPresentationProfileId : IEquatable<CharacterPresentationProfileId>, IFormattable
    {
        public CharacterPresentationProfileId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(CharacterPresentationProfileId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is CharacterPresentationProfileId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct SpawnProfileId : IEquatable<SpawnProfileId>, IFormattable
    {
        public SpawnProfileId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(SpawnProfileId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is SpawnProfileId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct CharacterAttributeId : IEquatable<CharacterAttributeId>, IFormattable
    {
        public CharacterAttributeId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(CharacterAttributeId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is CharacterAttributeId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct CharacterAbilityId : IEquatable<CharacterAbilityId>, IFormattable
    {
        public CharacterAbilityId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(CharacterAbilityId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is CharacterAbilityId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }

    public readonly struct CharacterCombatActionId : IEquatable<CharacterCombatActionId>, IFormattable
    {
        public CharacterCombatActionId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => CharacterApplicationIdFormat.IsValid(Value);
        public bool Equals(CharacterCombatActionId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is CharacterCombatActionId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public string ToString(string format, IFormatProvider formatProvider) { return CharacterApplicationIdFormat.Format(Value, format, formatProvider); }
        public override string ToString() { return ToString(null, CultureInfo.InvariantCulture); }
    }
}
