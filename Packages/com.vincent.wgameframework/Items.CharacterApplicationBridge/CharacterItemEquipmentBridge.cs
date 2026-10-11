using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.Config;

namespace MxFramework.Items.CharacterApplicationBridge
{
    public sealed class CharacterItemEquipmentProfileConfig : IConfigData
    {
        public const string TableName = "CharacterItemEquipmentProfileConfig";

        public CharacterItemEquipmentProfileConfig(
            ItemDefinitionId itemDefinitionId,
            WeaponConfigId weaponConfigId,
            string stableId,
            WeaponCategory category,
            string[] tags,
            int[] occupiesSlots,
            AbilityLoadoutId grantedAbilityLoadoutId,
            string weaponPresentationProfileId,
            string weaponCombatProfileId,
            string weaponResourceProfileId,
            CharacterResourceKeyEntry[] resourceKeys,
            CharacterTraceBindingEntry[] traceBindings,
            CharacterModifierGrantEntry[] baseModifiers)
        {
            if (!itemDefinitionId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(itemDefinitionId));
            if (!weaponConfigId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(weaponConfigId));

            ItemDefinitionId = itemDefinitionId;
            WeaponConfigId = weaponConfigId;
            StableId = stableId ?? string.Empty;
            Category = category;
            Tags = tags ?? Array.Empty<string>();
            OccupiesSlots = occupiesSlots ?? Array.Empty<int>();
            GrantedAbilityLoadoutId = grantedAbilityLoadoutId;
            WeaponPresentationProfileId = weaponPresentationProfileId ?? string.Empty;
            WeaponCombatProfileId = weaponCombatProfileId ?? string.Empty;
            WeaponResourceProfileId = weaponResourceProfileId ?? string.Empty;
            ResourceKeys = resourceKeys ?? Array.Empty<CharacterResourceKeyEntry>();
            TraceBindings = traceBindings ?? Array.Empty<CharacterTraceBindingEntry>();
            BaseModifiers = baseModifiers ?? Array.Empty<CharacterModifierGrantEntry>();
        }

        public int Id => ItemDefinitionId.Value;
        public ItemDefinitionId ItemDefinitionId { get; }
        public WeaponConfigId WeaponConfigId { get; }
        public string StableId { get; }
        public WeaponCategory Category { get; }
        public string[] Tags { get; }
        public int[] OccupiesSlots { get; }
        public AbilityLoadoutId GrantedAbilityLoadoutId { get; }
        public string WeaponPresentationProfileId { get; }
        public string WeaponCombatProfileId { get; }
        public string WeaponResourceProfileId { get; }
        public CharacterResourceKeyEntry[] ResourceKeys { get; }
        public CharacterTraceBindingEntry[] TraceBindings { get; }
        public CharacterModifierGrantEntry[] BaseModifiers { get; }

        public WeaponConfig ToWeaponConfig()
        {
            return new WeaponConfig(
                WeaponConfigId,
                StableId,
                Category,
                Tags,
                OccupiesSlots,
                GrantedAbilityLoadoutId,
                WeaponPresentationProfileId,
                WeaponCombatProfileId,
                WeaponResourceProfileId,
                ResourceKeys,
                TraceBindings,
                BaseModifiers);
        }

        public static ConfigSchema<CharacterItemEquipmentProfileConfig> CreateSchema()
        {
            var schema = new ConfigSchema<CharacterItemEquipmentProfileConfig>(
                TableName,
                displayName: "Character Item Equipment Profile",
                description: "将通用 ItemDefinition 投影为 Character.Application 兼容 WeaponConfig 的配置。");
            schema.AddField(new ConfigField(nameof(ItemDefinitionId), ConfigFieldType.ConfigReference, displayName: "物品定义", required: true, valueType: typeof(ItemDefinitionId), referenceRule: new ConfigReferenceRule(nameof(ItemDefinitionId), ItemDefinition.TableName, ConfigStructureKind.Table)));
            schema.AddField(new ConfigField(nameof(WeaponConfigId), ConfigFieldType.ConfigReference, displayName: "兼容武器编号", required: true, valueType: typeof(WeaponConfigId), referenceRule: new ConfigReferenceRule(nameof(WeaponConfigId), WeaponConfig.TableName, ConfigStructureKind.Table)));
            schema.AddField(new ConfigField(nameof(StableId), ConfigFieldType.String, displayName: "稳定 ID", required: true));
            schema.AddField(new ConfigField(nameof(Category), ConfigFieldType.Enum, displayName: "武器类别", required: true, enumId: "character.WeaponCategory"));
            schema.AddField(new ConfigField(nameof(Tags), ConfigFieldType.Custom, displayName: "武器标签", valueType: typeof(string[])));
            schema.AddField(new ConfigField(nameof(OccupiesSlots), ConfigFieldType.Custom, displayName: "占用槽位", valueType: typeof(int[])));
            schema.AddField(new ConfigField(nameof(GrantedAbilityLoadoutId), ConfigFieldType.ConfigReference, displayName: "武器授予能力", valueType: typeof(AbilityLoadoutId), referenceRule: new ConfigReferenceRule(nameof(GrantedAbilityLoadoutId), AbilityLoadoutConfig.TableName, ConfigStructureKind.Table, required: false)));
            schema.AddField(new ConfigField(nameof(ResourceKeys), ConfigFieldType.Custom, displayName: "资源引用", valueType: typeof(CharacterResourceKeyEntry[])));
            schema.AddField(new ConfigField(nameof(TraceBindings), ConfigFieldType.Custom, displayName: "轨迹绑定", valueType: typeof(CharacterTraceBindingEntry[])));
            schema.AddField(new ConfigField(nameof(BaseModifiers), ConfigFieldType.Custom, displayName: "基础 modifier", valueType: typeof(CharacterModifierGrantEntry[])));
            return schema;
        }
    }

    public sealed class CharacterItemEquipmentProfileRegistry
    {
        private readonly Dictionary<int, CharacterItemEquipmentProfileConfig> _byItemDefinitionId =
            new Dictionary<int, CharacterItemEquipmentProfileConfig>();

        public int Count => _byItemDefinitionId.Count;

        public bool Register(CharacterItemEquipmentProfileConfig profile)
        {
            if (profile == null || _byItemDefinitionId.ContainsKey(profile.ItemDefinitionId.Value))
                return false;
            _byItemDefinitionId.Add(profile.ItemDefinitionId.Value, profile);
            return true;
        }

        public bool TryGet(ItemDefinitionId itemDefinitionId, out CharacterItemEquipmentProfileConfig profile)
        {
            return _byItemDefinitionId.TryGetValue(itemDefinitionId.Value, out profile);
        }
    }

    public enum CharacterItemEquipmentProjectionStatus
    {
        Success = 0,
        InvalidEquipmentContainer = 1,
        InvalidEquipmentSchema = 2,
        ProjectionFailed = 3
    }

    public sealed class CharacterItemEquipmentProjectionResult
    {
        public CharacterItemEquipmentProjectionResult(
            CharacterItemEquipmentProjectionStatus status,
            EquipmentLoadoutConfig loadout,
            WeaponConfig[] weapons,
            CharacterDiagnostic[] diagnostics)
        {
            Status = status;
            Loadout = loadout;
            Weapons = weapons ?? Array.Empty<WeaponConfig>();
            Diagnostics = diagnostics ?? Array.Empty<CharacterDiagnostic>();
        }

        public CharacterItemEquipmentProjectionStatus Status { get; }
        public bool IsSuccess => Status == CharacterItemEquipmentProjectionStatus.Success;
        public EquipmentLoadoutConfig Loadout { get; }
        public WeaponConfig[] Weapons { get; }
        public CharacterDiagnostic[] Diagnostics { get; }
    }

    /// <summary>Projects item-backed equipment containers to Character.Application legacy equipment configs.</summary>
    /// <remarks>Allocation: LowFreqAlloc projection bridge; intended for loadout rebuilds, not per-frame simulation.</remarks>
    public static class CharacterItemEquipmentProjector
    {
        public static CharacterItemEquipmentProjectionResult Project(
            InventoryContainer equipmentContainer,
            EquipmentSchemaConfig equipmentSchema,
            EquipmentLoadoutId loadoutId,
            string loadoutStableId,
            ItemDefinitionRegistry itemDefinitions,
            CharacterItemEquipmentProfileRegistry equipmentProfiles)
        {
            var diagnostics = new List<CharacterDiagnostic>();
            if (equipmentContainer == null)
            {
                diagnostics.Add(Diagnostic(CharacterDiagnosticCode.InvalidEquipmentLoadout, "equipmentContainer", "Equipment container is required."));
                return new CharacterItemEquipmentProjectionResult(CharacterItemEquipmentProjectionStatus.InvalidEquipmentContainer, null, null, diagnostics.ToArray());
            }

            if (equipmentSchema == null || equipmentSchema.Slots == null)
            {
                diagnostics.Add(Diagnostic(CharacterDiagnosticCode.MissingEquipmentSchema, "equipmentSchema", "Equipment schema is required."));
                return new CharacterItemEquipmentProjectionResult(CharacterItemEquipmentProjectionStatus.InvalidEquipmentSchema, null, null, diagnostics.ToArray());
            }

            if (equipmentContainer.SlotCount != equipmentSchema.Slots.Length)
            {
                diagnostics.Add(Diagnostic(CharacterDiagnosticCode.InvalidSlot, "equipmentContainer.SlotCount", "Equipment container slot count must match EquipmentSchema slots."));
                return new CharacterItemEquipmentProjectionResult(CharacterItemEquipmentProjectionStatus.ProjectionFailed, null, null, diagnostics.ToArray());
            }

            var loadoutSlots = new List<EquipmentLoadoutSlotEntry>();
            var weapons = new List<WeaponConfig>();
            var emittedWeaponIds = new HashSet<int>();
            for (int slotIndex = 0; slotIndex < equipmentContainer.SlotCount; slotIndex++)
            {
                ItemStack stack = equipmentContainer.GetStack(slotIndex);
                if (stack.IsEmpty)
                    continue;

                EquipmentSlotEntry schemaSlot = equipmentSchema.Slots[slotIndex];
                if (stack.Count != 1)
                {
                    diagnostics.Add(Diagnostic(CharacterDiagnosticCode.InvalidEquipmentLoadout, "Slots[" + slotIndex + "]", "Equipped item stack count must be exactly 1."));
                    continue;
                }

                if (itemDefinitions == null || !itemDefinitions.TryGet(stack.DefinitionId, out ItemDefinition definition))
                {
                    diagnostics.Add(Diagnostic(CharacterDiagnosticCode.MissingWeaponConfig, "Slots[" + slotIndex + "]", "Equipped item definition is missing."));
                    continue;
                }

                if (definition.Kind != ItemKind.Equipment)
                {
                    diagnostics.Add(Diagnostic(CharacterDiagnosticCode.InvalidEquipmentLoadout, definition.StableId, "Equipped item definition kind must be Equipment."));
                    continue;
                }

                if (equipmentProfiles == null || !equipmentProfiles.TryGet(stack.DefinitionId, out CharacterItemEquipmentProfileConfig profile))
                {
                    diagnostics.Add(Diagnostic(CharacterDiagnosticCode.MissingWeaponConfig, definition.StableId, "Character item equipment profile is missing."));
                    continue;
                }

                if (!IsSlotCompatible(schemaSlot, profile))
                {
                    diagnostics.Add(Diagnostic(CharacterDiagnosticCode.CategoryNotAllowed, "Slots[" + slotIndex + "].SlotId", "Item equipment profile is not compatible with the equipment slot."));
                    continue;
                }

                if (emittedWeaponIds.Add(profile.WeaponConfigId.Value))
                    weapons.Add(profile.ToWeaponConfig());

                string instanceStableId = stack.InstanceId.IsValid
                    ? definition.StableId + ".instance." + stack.InstanceId.Value
                    : definition.StableId;
                loadoutSlots.Add(new EquipmentLoadoutSlotEntry(schemaSlot.SlotId, profile.WeaponConfigId, instanceStableId));
            }

            if (diagnostics.Count > 0)
                return new CharacterItemEquipmentProjectionResult(CharacterItemEquipmentProjectionStatus.ProjectionFailed, null, weapons.ToArray(), diagnostics.ToArray());

            var loadout = new EquipmentLoadoutConfig(
                loadoutId,
                loadoutStableId ?? string.Empty,
                equipmentSchema.EquipmentSchemaId,
                loadoutSlots.ToArray());
            return new CharacterItemEquipmentProjectionResult(CharacterItemEquipmentProjectionStatus.Success, loadout, weapons.ToArray(), diagnostics.ToArray());
        }

        private static bool IsSlotCompatible(EquipmentSlotEntry slot, CharacterItemEquipmentProfileConfig profile)
        {
            if (slot.AllowedWeaponCategories != null && slot.AllowedWeaponCategories.Length > 0)
            {
                string category = profile.Category.ToString();
                if (!Contains(slot.AllowedWeaponCategories, category))
                    return false;
            }

            if (slot.RequiredTags != null)
            {
                for (int i = 0; i < slot.RequiredTags.Length; i++)
                {
                    if (!Contains(profile.Tags, slot.RequiredTags[i]))
                        return false;
                }
            }

            return true;
        }

        private static bool Contains(string[] values, string value)
        {
            if (values == null)
                return false;
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static CharacterDiagnostic Diagnostic(CharacterDiagnosticCode code, string field, string message)
        {
            return new CharacterDiagnostic(
                CharacterDiagnosticSeverity.Error,
                code,
                CharacterItemEquipmentProfileConfig.TableName,
                0,
                string.Empty,
                field,
                message);
        }
    }
}
