using System;
using System.Collections.Generic;

namespace MxFramework.CharacterApplication
{
    /// <summary>A concrete weapon kind, independent of the broad equipment slot category.</summary>
    public sealed class WeaponTypeDefinition
    {
        public string TypeId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string EquipmentCategory { get; set; } = string.Empty;
        public string IconResourceKey { get; set; } = string.Empty;
    }

    /// <summary>Validated immutable lookup of the one authored weapon-type projection.</summary>
    public sealed class WeaponTypeCatalog
    {
        private readonly Dictionary<string, WeaponTypeDefinition> _types = new Dictionary<string, WeaponTypeDefinition>(StringComparer.Ordinal);
        public WeaponTypeCatalog(IReadOnlyList<WeaponTypeDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0) throw new ArgumentException("WeaponTypeCatalogMissing");
            for (int i = 0; i < definitions.Count; i++)
            {
                var d = definitions[i];
                if (d == null || string.IsNullOrWhiteSpace(d.TypeId) || string.IsNullOrWhiteSpace(d.DisplayName)
                    || string.IsNullOrWhiteSpace(d.EquipmentCategory) || HasWhitespace(d.TypeId))
                    throw new ArgumentException("WeaponTypeDefinitionInvalid");
                if (!Enum.TryParse(d.EquipmentCategory, out WeaponCategory category)
                    || !Enum.IsDefined(typeof(WeaponCategory), category) || category.ToString() != d.EquipmentCategory
                    || category == WeaponCategory.Unarmed)
                    throw new ArgumentException("WeaponTypeEquipmentCategoryInvalid:" + d.TypeId);
                if (_types.ContainsKey(d.TypeId)) throw new ArgumentException("WeaponTypeIdDuplicate:" + d.TypeId);
                _types.Add(d.TypeId, new WeaponTypeDefinition { TypeId = d.TypeId, DisplayName = d.DisplayName,
                    EquipmentCategory = d.EquipmentCategory, IconResourceKey = d.IconResourceKey ?? string.Empty });
            }
        }
        private static bool HasWhitespace(string value)
        { foreach (char c in value) if (char.IsWhiteSpace(c)) return true; return false; }
        public bool Contains(string typeId) => typeId != null && _types.ContainsKey(typeId);
        public bool ValidateWeapon(string typeId, string equipmentCategory, out string issue)
        {
            if (string.IsNullOrEmpty(typeId)) { issue = "WeaponTypeMissing"; return false; }
            if (!_types.TryGetValue(typeId, out var type)) { issue = "WeaponTypeUnknown:" + typeId; return false; }
            if (!string.Equals(type.EquipmentCategory, equipmentCategory, StringComparison.Ordinal))
            { issue = "WeaponTypeCategoryMismatch:" + typeId; return false; }
            issue = string.Empty; return true;
        }
        public string DisplayName(string typeId) => _types.TryGetValue(typeId ?? string.Empty, out var d) ? d.DisplayName : string.Empty;
        public string IconResourceKey(string typeId) => _types.TryGetValue(typeId ?? string.Empty, out var d) ? d.IconResourceKey : string.Empty;
        public bool ValidateRequirement(string typeId, int slotId, out string issue)
        {
            if (!Contains(typeId)) { issue = "RequiredWeaponTypeUnknown:" + typeId; return false; }
            if (slotId <= 0) { issue = "RequiredWeaponSlotInvalid"; return false; }
            issue = string.Empty; return true;
        }
        public bool Matches(string requiredTypeId, int requiredSlot, string equippedTypeId, string equipmentCategory, out string issue)
        {
            if (!ValidateRequirement(requiredTypeId, requiredSlot, out issue)) return false;
            if (!ValidateWeapon(equippedTypeId, equipmentCategory, out issue)) return false;
            if (!string.Equals(requiredTypeId, equippedTypeId, StringComparison.Ordinal))
            { issue = "WeaponTypeMismatch:required=" + requiredTypeId + ":equipped=" + equippedTypeId; return false; }
            issue = string.Empty; return true;
        }
    }
}
