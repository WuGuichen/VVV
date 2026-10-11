using System;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterEquipmentComponent
    {
        public CharacterTestCharacterEquipmentComponent(
            bool useDefaultLoadout,
            string equipmentStateStableId = "",
            uint version = 0)
        {
            UseDefaultLoadout = useDefaultLoadout;
            EquipmentStateStableId = equipmentStateStableId ?? string.Empty;
            Version = version;
        }

        public bool UseDefaultLoadout { get; }
        public string EquipmentStateStableId { get; }
        public uint Version { get; }

        public EquipmentVersionSnapshot ToSnapshot() => new EquipmentVersionSnapshot(EquipmentStateStableId, Version);
    }

    /// <summary>
    /// Deterministic snapshot of character equipment state and version.
    /// Used by tactical card preparation to invalidate stale preparations when weapon/equipment changes.
    /// </summary>
    public readonly struct EquipmentVersionSnapshot : IEquatable<EquipmentVersionSnapshot>
    {
        public string EquipmentStateStableId { get; }
        public uint Version { get; }

        public EquipmentVersionSnapshot(string equipmentStateStableId, uint version)
        {
            EquipmentStateStableId = equipmentStateStableId ?? string.Empty;
            Version = version;
        }

        public bool Equals(EquipmentVersionSnapshot other) =>
            Version == other.Version && string.Equals(EquipmentStateStableId, other.EquipmentStateStableId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is EquipmentVersionSnapshot other && Equals(other);

        public override int GetHashCode() =>
            ((EquipmentStateStableId != null ? StringComparer.Ordinal.GetHashCode(EquipmentStateStableId) : 0) * 397) ^ (int)Version;

        public static bool operator ==(EquipmentVersionSnapshot left, EquipmentVersionSnapshot right) => left.Equals(right);
        public static bool operator !=(EquipmentVersionSnapshot left, EquipmentVersionSnapshot right) => !left.Equals(right);

        public override string ToString() => $"EquipmentSnapshot({EquipmentStateStableId}, v{Version})";
    }
}
