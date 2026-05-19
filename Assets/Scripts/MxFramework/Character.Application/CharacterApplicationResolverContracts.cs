using System;

namespace MxFramework.CharacterApplication
{
    public enum CharacterDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum CharacterDiagnosticCode
    {
        None = 0,
        MissingCharacterConfig = 1,
        MissingBodyProfile = 2,
        MissingAttributeProfile = 3,
        MissingEquipmentSchema = 4,
        MissingDefaultLoadout = 5,
        MissingPresentationProfile = 6,
        InvalidBodyPartSet = 7,
        InvalidEquipmentLoadout = 8,
        NoEquipmentStateMatch = 9,
        EquipmentStateTie = 10,
        InvalidSlot = 11,
        MissingRequiredSlot = 12,
        CategoryNotAllowed = 13,
        OccupiedSlotConflict = 14,
        MissingWeaponConfig = 15,
        MissingEquipmentStateConfig = 16,
        MissingAbilityLoadout = 17,
        MissingAbility = 18,
        DuplicateAbilityGrant = 19,
        AbilitySlotConflict = 20,
        AbilityInputIntentConflict = 21,
        MissingCombatActionSet = 22,
        MissingCombatAction = 23,
        MissingAnimationAction = 24,
        DuplicateCombatActionKey = 25,
        UnknownHitZone = 26,
        UnmappedHitZone = 27,
        AmbiguousHitZone = 28,
        MissingBodyPart = 29,
        BodyPartParentMissing = 30,
        MissingResourceKey = 31,
        MissingSpawnProfile = 32,
        InvalidSpawnRequest = 33,
        InvalidSaveStateBinding = 34,
        SaveStateBindingMismatch = 35,
        SaveStateActiveStateMismatch = 36
    }

    public readonly struct CharacterDiagnostic
    {
        public CharacterDiagnostic(
            CharacterDiagnosticSeverity severity,
            CharacterDiagnosticCode code,
            string sourceTable,
            int sourceId,
            string sourceStableId,
            string field,
            string message)
        {
            Severity = severity;
            Code = code;
            SourceTable = sourceTable ?? string.Empty;
            SourceId = sourceId;
            SourceStableId = sourceStableId ?? string.Empty;
            Field = field ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterDiagnosticSeverity Severity { get; }
        public CharacterDiagnosticCode Code { get; }
        public string StableCode => CharacterDiagnosticCodes.ToStableCode(Code);
        public string SourceTable { get; }
        public int SourceId { get; }
        public string SourceStableId { get; }
        public string Field { get; }
        public string Message { get; }
        public bool IsError => Severity == CharacterDiagnosticSeverity.Error;
    }

    public static class CharacterDiagnosticCodes
    {
        public static string ToStableCode(CharacterDiagnosticCode code)
        {
            switch (code)
            {
                case CharacterDiagnosticCode.MissingCharacterConfig:
                    return "CHAR_MISSING_CHARACTER_CONFIG";
                case CharacterDiagnosticCode.MissingBodyProfile:
                    return "CHAR_MISSING_BODY_PROFILE";
                case CharacterDiagnosticCode.MissingAttributeProfile:
                    return "CHAR_MISSING_ATTRIBUTE_PROFILE";
                case CharacterDiagnosticCode.MissingEquipmentSchema:
                    return "CHAR_MISSING_EQUIPMENT_SCHEMA";
                case CharacterDiagnosticCode.MissingDefaultLoadout:
                    return "CHAR_MISSING_DEFAULT_LOADOUT";
                case CharacterDiagnosticCode.MissingPresentationProfile:
                    return "CHAR_MISSING_PRESENTATION_PROFILE";
                case CharacterDiagnosticCode.InvalidBodyPartSet:
                    return "CHAR_INVALID_BODY_PART_SET";
                case CharacterDiagnosticCode.InvalidEquipmentLoadout:
                    return "CHAR_INVALID_LOADOUT_SCHEMA";
                case CharacterDiagnosticCode.NoEquipmentStateMatch:
                    return "CHAR_EQUIPMENT_STATE_NO_MATCH";
                case CharacterDiagnosticCode.EquipmentStateTie:
                    return "CHAR_EQUIPMENT_STATE_TIE";
                case CharacterDiagnosticCode.InvalidSlot:
                    return "CHAR_INVALID_SLOT";
                case CharacterDiagnosticCode.MissingRequiredSlot:
                    return "CHAR_MISSING_REQUIRED_SLOT";
                case CharacterDiagnosticCode.CategoryNotAllowed:
                    return "CHAR_CATEGORY_NOT_ALLOWED";
                case CharacterDiagnosticCode.OccupiedSlotConflict:
                    return "CHAR_OCCUPIED_SLOT_CONFLICT";
                case CharacterDiagnosticCode.MissingWeaponConfig:
                    return "CHAR_MISSING_WEAPON";
                case CharacterDiagnosticCode.MissingEquipmentStateConfig:
                    return "CHAR_MISSING_EQUIPMENT_STATE";
                case CharacterDiagnosticCode.MissingAbilityLoadout:
                    return "CHAR_MISSING_ABILITY_LOADOUT";
                case CharacterDiagnosticCode.MissingAbility:
                    return "CHAR_MISSING_ABILITY";
                case CharacterDiagnosticCode.DuplicateAbilityGrant:
                    return "CHAR_DUPLICATE_ABILITY_GRANT";
                case CharacterDiagnosticCode.AbilitySlotConflict:
                    return "CHAR_ABILITY_SLOT_CONFLICT";
                case CharacterDiagnosticCode.AbilityInputIntentConflict:
                    return "CHAR_ABILITY_INPUT_INTENT_CONFLICT";
                case CharacterDiagnosticCode.MissingCombatActionSet:
                    return "CHAR_MISSING_COMBAT_ACTION_SET";
                case CharacterDiagnosticCode.MissingCombatAction:
                    return "CHAR_MISSING_COMBAT_ACTION";
                case CharacterDiagnosticCode.MissingAnimationAction:
                    return "CHAR_MISSING_ANIMATION_ACTION";
                case CharacterDiagnosticCode.DuplicateCombatActionKey:
                    return "CHAR_DUPLICATE_COMBAT_ACTION_KEY";
                case CharacterDiagnosticCode.UnknownHitZone:
                    return "CHAR_UNKNOWN_HIT_ZONE";
                case CharacterDiagnosticCode.UnmappedHitZone:
                    return "CHAR_UNMAPPED_HIT_ZONE";
                case CharacterDiagnosticCode.AmbiguousHitZone:
                    return "CHAR_AMBIGUOUS_HIT_ZONE";
                case CharacterDiagnosticCode.MissingBodyPart:
                    return "CHAR_MISSING_BODY_PART";
                case CharacterDiagnosticCode.BodyPartParentMissing:
                    return "CHAR_BODY_PART_PARENT_MISSING";
                case CharacterDiagnosticCode.MissingResourceKey:
                    return "CHAR_MISSING_RESOURCE_KEY";
                case CharacterDiagnosticCode.MissingSpawnProfile:
                    return "CHAR_MISSING_SPAWN_PROFILE";
                case CharacterDiagnosticCode.InvalidSpawnRequest:
                    return "CHAR_INVALID_SPAWN_REQUEST";
                case CharacterDiagnosticCode.InvalidSaveStateBinding:
                    return "CHAR_INVALID_SAVE_STATE_BINDING";
                case CharacterDiagnosticCode.SaveStateBindingMismatch:
                    return "CHAR_SAVE_STATE_BINDING_MISMATCH";
                case CharacterDiagnosticCode.SaveStateActiveStateMismatch:
                    return "CHAR_SAVE_STATE_ACTIVE_STATE_MISMATCH";
                default:
                    return "CHAR_NONE";
            }
        }
    }

    public sealed class CharacterValidationReport
    {
        public CharacterValidationReport(CharacterDiagnostic[] issues)
        {
            Issues = issues ?? Array.Empty<CharacterDiagnostic>();
        }

        public static CharacterValidationReport Empty { get; } = new CharacterValidationReport(Array.Empty<CharacterDiagnostic>());

        public CharacterDiagnostic[] Issues { get; }
        public CharacterDiagnostic[] Diagnostics => Issues;

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Issues.Length; i++)
                {
                    if (Issues[i].Severity == CharacterDiagnosticSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public bool HasWarnings
        {
            get
            {
                for (int i = 0; i < Issues.Length; i++)
                {
                    if (Issues[i].Severity == CharacterDiagnosticSeverity.Warning)
                        return true;
                }

                return false;
            }
        }
    }

    public sealed class CharacterResourceDependencyReport
    {
        public CharacterResourceDependencyReport(
            CharacterResourceKeyEntry[] requiredResources,
            string[] preloadGroupIds,
            CharacterDiagnostic[] diagnostics)
        {
            RequiredResources = requiredResources ?? Array.Empty<CharacterResourceKeyEntry>();
            PreloadGroupIds = preloadGroupIds ?? Array.Empty<string>();
            Diagnostics = diagnostics ?? Array.Empty<CharacterDiagnostic>();
        }

        public static CharacterResourceDependencyReport Empty { get; } = new CharacterResourceDependencyReport(
            Array.Empty<CharacterResourceKeyEntry>(),
            Array.Empty<string>(),
            Array.Empty<CharacterDiagnostic>());

        public CharacterResourceKeyEntry[] RequiredResources { get; }
        public string[] PreloadGroupIds { get; }
        public CharacterDiagnostic[] Diagnostics { get; }
    }

    public readonly struct CharacterDebugContext
    {
        public CharacterDebugContext(
            string characterStableId,
            string loadoutStableId,
            string activeEquipmentStateStableId,
            string combatActionSetStableId,
            string animationProfileId,
            string diagnosticSummary)
        {
            CharacterStableId = characterStableId ?? string.Empty;
            LoadoutStableId = loadoutStableId ?? string.Empty;
            ActiveEquipmentStateStableId = activeEquipmentStateStableId ?? string.Empty;
            CombatActionSetStableId = combatActionSetStableId ?? string.Empty;
            AnimationProfileId = animationProfileId ?? string.Empty;
            DiagnosticSummary = diagnosticSummary ?? string.Empty;
        }

        public string CharacterStableId { get; }
        public string LoadoutStableId { get; }
        public string ActiveEquipmentStateStableId { get; }
        public string CombatActionSetStableId { get; }
        public string AnimationProfileId { get; }
        public string DiagnosticSummary { get; }
    }

    public readonly struct CharacterResolvedProfile
    {
        public CharacterResolvedProfile(
            CharacterConfigId characterId,
            CharacterBodyProfileId bodyProfileId,
            CharacterAttributeProfileId attributeProfileId,
            EquipmentSchemaId equipmentSchemaId,
            EquipmentLoadoutId loadoutId,
            EquipmentStateId activeEquipmentStateId,
            AbilityLoadoutId[] effectiveAbilityLoadoutIds,
            CharacterAbilityId[] effectiveAbilityIds,
            AbilitySlotBinding[] effectiveSlotBindings,
            AbilityGrantHandle[] abilityGrantHandles,
            CombatActionSetId combatActionSetId,
            string animationProfileId,
            CharacterResourceKeyEntry[] requiredResources,
            CharacterValidationReport validationReport,
            CharacterResourceDependencyReport resourceDependencyReport,
            CharacterDebugContext debugContext)
        {
            CharacterId = characterId;
            BodyProfileId = bodyProfileId;
            AttributeProfileId = attributeProfileId;
            EquipmentSchemaId = equipmentSchemaId;
            LoadoutId = loadoutId;
            ActiveEquipmentStateId = activeEquipmentStateId;
            EffectiveAbilityLoadoutIds = effectiveAbilityLoadoutIds ?? Array.Empty<AbilityLoadoutId>();
            EffectiveAbilityIds = effectiveAbilityIds ?? Array.Empty<CharacterAbilityId>();
            EffectiveSlotBindings = effectiveSlotBindings ?? Array.Empty<AbilitySlotBinding>();
            AbilityGrantHandles = abilityGrantHandles ?? Array.Empty<AbilityGrantHandle>();
            CombatActionSetId = combatActionSetId;
            AnimationProfileId = animationProfileId ?? string.Empty;
            RequiredResources = requiredResources ?? Array.Empty<CharacterResourceKeyEntry>();
            ValidationReport = validationReport ?? CharacterValidationReport.Empty;
            ResourceDependencyReport = resourceDependencyReport ?? CharacterResourceDependencyReport.Empty;
            DebugContext = debugContext;
        }

        public CharacterConfigId CharacterId { get; }
        public CharacterBodyProfileId BodyProfileId { get; }
        public CharacterAttributeProfileId AttributeProfileId { get; }
        public EquipmentSchemaId EquipmentSchemaId { get; }
        public EquipmentLoadoutId LoadoutId { get; }
        public EquipmentStateId ActiveEquipmentStateId { get; }
        public AbilityLoadoutId[] EffectiveAbilityLoadoutIds { get; }
        public CharacterAbilityId[] EffectiveAbilityIds { get; }
        public AbilitySlotBinding[] EffectiveSlotBindings { get; }
        public AbilityGrantHandle[] AbilityGrantHandles { get; }
        public CombatActionSetId CombatActionSetId { get; }
        public string AnimationProfileId { get; }
        public CharacterResourceKeyEntry[] RequiredResources { get; }
        public CharacterValidationReport ValidationReport { get; }
        public CharacterValidationReport Diagnostics => ValidationReport;
        public CharacterResourceDependencyReport ResourceDependencyReport { get; }
        public CharacterDebugContext DebugContext { get; }
    }
}
