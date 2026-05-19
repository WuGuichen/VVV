using System;
using System.Collections.Generic;

namespace MxFramework.CharacterApplication
{
    public enum EquipmentStateResolveStatus
    {
        Success = 0,
        NoMatchingState = 1,
        MultipleMatchingStates = 2,
        InvalidSlot = 3,
        MissingRequiredSlot = 4,
        CategoryNotAllowed = 5,
        OccupiedSlotConflict = 6,
        MissingWeaponConfig = 7,
        MissingEquipmentStateConfig = 8,
        InvalidLoadoutSchema = 9
    }

    public readonly struct EquippedWeaponSlot
    {
        public EquippedWeaponSlot(string slotId, string mountedSlotId, WeaponConfig weapon, string weaponInstanceStableId)
        {
            SlotId = slotId ?? string.Empty;
            MountedSlotId = mountedSlotId ?? string.Empty;
            Weapon = weapon;
            WeaponInstanceStableId = weaponInstanceStableId ?? string.Empty;
        }

        public string SlotId { get; }
        public string MountedSlotId { get; }
        public WeaponConfig Weapon { get; }
        public string WeaponInstanceStableId { get; }
    }

    public readonly struct EquipmentStateResolveResult
    {
        public EquipmentStateResolveResult(
            EquipmentStateResolveStatus status,
            EquipmentStateId activeStateId,
            EquipmentStateConfig activeState,
            EquipmentStateId[] matchedStateIds,
            EquippedWeaponSlot[] equippedWeapons,
            CharacterDiagnostic[] diagnostics)
        {
            Status = status;
            ActiveStateId = activeStateId;
            ActiveState = activeState;
            MatchedStateIds = matchedStateIds ?? Array.Empty<EquipmentStateId>();
            EquippedWeapons = equippedWeapons ?? Array.Empty<EquippedWeaponSlot>();
            Diagnostics = diagnostics ?? Array.Empty<CharacterDiagnostic>();
        }

        public EquipmentStateResolveStatus Status { get; }
        public bool IsSuccess => Status == EquipmentStateResolveStatus.Success;
        public EquipmentStateId ActiveStateId { get; }
        public EquipmentStateConfig ActiveState { get; }
        public EquipmentStateId[] MatchedStateIds { get; }
        public EquippedWeaponSlot[] EquippedWeapons { get; }
        public CharacterDiagnostic[] Diagnostics { get; }
    }

    public static class EquipmentStateResolver
    {
        public static EquipmentStateResolveResult Resolve(
            EquipmentSchemaConfig schema,
            EquipmentLoadoutConfig loadout,
            IReadOnlyList<WeaponConfig> weaponConfigs,
            IReadOnlyList<EquipmentStateConfig> stateConfigs)
        {
            var diagnostics = new CharacterDiagnosticBuilder();
            if (schema == null)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingEquipmentSchema,
                    EquipmentSchemaConfig.TableName,
                    0,
                    string.Empty,
                    nameof(schema),
                    "Equipment schema is required.");
                return CreateFailure(EquipmentStateResolveStatus.InvalidLoadoutSchema, diagnostics);
            }

            if (loadout == null)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingDefaultLoadout,
                    EquipmentLoadoutConfig.TableName,
                    0,
                    string.Empty,
                    nameof(loadout),
                    "Equipment loadout is required.");
                return CreateFailure(EquipmentStateResolveStatus.InvalidLoadoutSchema, diagnostics);
            }

            if (!loadout.EquipmentSchemaId.Equals(schema.EquipmentSchemaId))
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.InvalidEquipmentLoadout,
                    EquipmentLoadoutConfig.TableName,
                    loadout.Id,
                    loadout.StableId,
                    nameof(EquipmentLoadoutConfig.EquipmentSchemaId),
                    "Loadout schema id does not match the resolver schema.");
                return CreateFailure(EquipmentStateResolveStatus.InvalidLoadoutSchema, diagnostics);
            }

            Dictionary<string, EquipmentSlotEntry> schemaSlots = BuildSchemaSlotMap(schema);
            var filledSlots = new Dictionary<string, EquippedWeaponSlot>(StringComparer.Ordinal);
            var uniqueWeapons = new List<EquippedWeaponSlot>();
            EquipmentStateResolveStatus validationStatus = ValidateLoadout(schema, loadout, weaponConfigs, schemaSlots, filledSlots, uniqueWeapons, diagnostics);
            if (validationStatus != EquipmentStateResolveStatus.Success)
                return new EquipmentStateResolveResult(validationStatus, default, null, Array.Empty<EquipmentStateId>(), uniqueWeapons.ToArray(), diagnostics.ToArray());

            var candidateStates = new List<EquipmentStateConfig>();
            for (int i = 0; i < schema.AllowedStateIds.Length; i++)
            {
                EquipmentStateConfig state = FindState(stateConfigs, schema.AllowedStateIds[i]);
                if (state == null)
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.MissingEquipmentStateConfig,
                        EquipmentSchemaConfig.TableName,
                        schema.Id,
                        schema.StableId,
                        nameof(EquipmentSchemaConfig.AllowedStateIds),
                        "Allowed equipment state config is missing: " + schema.AllowedStateIds[i].Value + ".");
                    continue;
                }

                if (!state.EquipmentSchemaId.Equals(schema.EquipmentSchemaId))
                    continue;

                candidateStates.Add(state);
            }

            if (diagnostics.Count > 0 && candidateStates.Count == 0)
                return new EquipmentStateResolveResult(EquipmentStateResolveStatus.MissingEquipmentStateConfig, default, null, Array.Empty<EquipmentStateId>(), uniqueWeapons.ToArray(), diagnostics.ToArray());

            EquipmentStateConfig best = null;
            int bestPriority = int.MinValue;
            var matched = new List<EquipmentStateId>();
            for (int i = 0; i < candidateStates.Count; i++)
            {
                EquipmentStateConfig state = candidateStates[i];
                if (!MatchesState(state, filledSlots))
                    continue;

                matched.Add(state.StateId);
                if (best == null || state.Priority > bestPriority)
                {
                    best = state;
                    bestPriority = state.Priority;
                }
            }

            if (best == null)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.NoEquipmentStateMatch,
                    EquipmentSchemaConfig.TableName,
                    schema.Id,
                    schema.StableId,
                    nameof(EquipmentSchemaConfig.AllowedStateIds),
                    "No allowed equipment state matched the current loadout.");
                return new EquipmentStateResolveResult(EquipmentStateResolveStatus.NoMatchingState, default, null, matched.ToArray(), uniqueWeapons.ToArray(), diagnostics.ToArray());
            }

            var topMatches = new List<EquipmentStateId>();
            for (int i = 0; i < candidateStates.Count; i++)
            {
                EquipmentStateConfig state = candidateStates[i];
                if (state.Priority == bestPriority && MatchesState(state, filledSlots))
                    topMatches.Add(state.StateId);
            }

            if (topMatches.Count > 1)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.EquipmentStateTie,
                    EquipmentSchemaConfig.TableName,
                    schema.Id,
                    schema.StableId,
                    nameof(EquipmentSchemaConfig.AllowedStateIds),
                    "Multiple equipment states matched with the same top priority.");
                return new EquipmentStateResolveResult(EquipmentStateResolveStatus.MultipleMatchingStates, default, null, topMatches.ToArray(), uniqueWeapons.ToArray(), diagnostics.ToArray());
            }

            return new EquipmentStateResolveResult(
                EquipmentStateResolveStatus.Success,
                best.StateId,
                best,
                matched.ToArray(),
                uniqueWeapons.ToArray(),
                diagnostics.ToArray());
        }

        private static EquipmentStateResolveResult CreateFailure(EquipmentStateResolveStatus status, CharacterDiagnosticBuilder diagnostics)
        {
            return new EquipmentStateResolveResult(status, default, null, Array.Empty<EquipmentStateId>(), Array.Empty<EquippedWeaponSlot>(), diagnostics.ToArray());
        }

        private static Dictionary<string, EquipmentSlotEntry> BuildSchemaSlotMap(EquipmentSchemaConfig schema)
        {
            var slots = new Dictionary<string, EquipmentSlotEntry>(StringComparer.Ordinal);
            if (schema == null || schema.Slots == null)
                return slots;

            for (int i = 0; i < schema.Slots.Length; i++)
            {
                EquipmentSlotEntry slot = schema.Slots[i];
                if (!string.IsNullOrEmpty(slot.SlotId) && !slots.ContainsKey(slot.SlotId))
                    slots.Add(slot.SlotId, slot);
            }

            return slots;
        }

        private static EquipmentStateResolveStatus ValidateLoadout(
            EquipmentSchemaConfig schema,
            EquipmentLoadoutConfig loadout,
            IReadOnlyList<WeaponConfig> weaponConfigs,
            Dictionary<string, EquipmentSlotEntry> schemaSlots,
            Dictionary<string, EquippedWeaponSlot> filledSlots,
            List<EquippedWeaponSlot> uniqueWeapons,
            CharacterDiagnosticBuilder diagnostics)
        {
            EquipmentStateResolveStatus status = EquipmentStateResolveStatus.Success;

            for (int i = 0; i < loadout.Slots.Length; i++)
            {
                EquipmentLoadoutSlotEntry loadoutSlot = loadout.Slots[i];
                if (string.IsNullOrEmpty(loadoutSlot.SlotId) || !schemaSlots.ContainsKey(loadoutSlot.SlotId))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.InvalidSlot,
                        EquipmentLoadoutConfig.TableName,
                        loadout.Id,
                        loadout.StableId,
                        "Slots[" + i + "].SlotId",
                        "Loadout slot is not declared by the equipment schema.");
                    status = MaxStatus(status, EquipmentStateResolveStatus.InvalidSlot);
                    continue;
                }

                if (!loadoutSlot.WeaponId.IsValid)
                    continue;

                WeaponConfig weapon = FindWeapon(weaponConfigs, loadoutSlot.WeaponId);
                if (weapon == null)
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.MissingWeaponConfig,
                        EquipmentLoadoutConfig.TableName,
                        loadout.Id,
                        loadout.StableId,
                        "Slots[" + i + "].WeaponId",
                        "Weapon config is missing: " + loadoutSlot.WeaponId.Value + ".");
                    status = MaxStatus(status, EquipmentStateResolveStatus.MissingWeaponConfig);
                    continue;
                }

                EquipmentSlotEntry mountSlot = schemaSlots[loadoutSlot.SlotId];
                if (!IsWeaponAllowedInSlot(mountSlot, weapon))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.CategoryNotAllowed,
                        EquipmentLoadoutConfig.TableName,
                        loadout.Id,
                        loadout.StableId,
                        "Slots[" + i + "].WeaponId",
                        "Weapon category or tags are not allowed by the equipment slot.");
                    status = MaxStatus(status, EquipmentStateResolveStatus.CategoryNotAllowed);
                }

                var equipped = new EquippedWeaponSlot(loadoutSlot.SlotId, loadoutSlot.SlotId, weapon, loadoutSlot.WeaponInstanceStableId);
                uniqueWeapons.Add(equipped);

                string[] occupiedSlots = weapon.OccupiesSlots == null || weapon.OccupiesSlots.Length == 0
                    ? new[] { loadoutSlot.SlotId }
                    : weapon.OccupiesSlots;

                for (int j = 0; j < occupiedSlots.Length; j++)
                {
                    string occupiedSlot = occupiedSlots[j];
                    if (string.IsNullOrEmpty(occupiedSlot) || !schemaSlots.ContainsKey(occupiedSlot))
                    {
                        diagnostics.Add(
                            CharacterDiagnosticSeverity.Error,
                            CharacterDiagnosticCode.InvalidSlot,
                            WeaponConfig.TableName,
                            weapon.Id,
                            weapon.StableId,
                            nameof(WeaponConfig.OccupiesSlots),
                            "Weapon occupies a slot that is not declared by the equipment schema.");
                        status = MaxStatus(status, EquipmentStateResolveStatus.InvalidSlot);
                        continue;
                    }

                    var occupied = new EquippedWeaponSlot(occupiedSlot, loadoutSlot.SlotId, weapon, loadoutSlot.WeaponInstanceStableId);
                    if (filledSlots.ContainsKey(occupiedSlot))
                    {
                        diagnostics.Add(
                            CharacterDiagnosticSeverity.Error,
                            CharacterDiagnosticCode.OccupiedSlotConflict,
                            EquipmentLoadoutConfig.TableName,
                            loadout.Id,
                            loadout.StableId,
                            "Slots[" + i + "].WeaponId",
                            "Multiple equipped weapons occupy the same slot: " + occupiedSlot + ".");
                        status = MaxStatus(status, EquipmentStateResolveStatus.OccupiedSlotConflict);
                    }
                    else
                    {
                        filledSlots.Add(occupiedSlot, occupied);
                    }
                }
            }

            status = ValidateExclusiveGroups(schema, loadout, filledSlots, diagnostics, status);
            return status;
        }

        private static EquipmentStateResolveStatus ValidateExclusiveGroups(
            EquipmentSchemaConfig schema,
            EquipmentLoadoutConfig loadout,
            Dictionary<string, EquippedWeaponSlot> filledSlots,
            CharacterDiagnosticBuilder diagnostics,
            EquipmentStateResolveStatus status)
        {
            if (schema.ExclusiveGroups == null)
                return status;

            for (int i = 0; i < schema.ExclusiveGroups.Length; i++)
            {
                EquipmentExclusiveGroupEntry group = schema.ExclusiveGroups[i];
                if (group.MaxFilledCount < 0)
                    continue;

                int filledCount = 0;
                for (int j = 0; j < group.SlotIds.Length; j++)
                {
                    if (filledSlots.ContainsKey(group.SlotIds[j]))
                        filledCount++;
                }

                if (filledCount > group.MaxFilledCount)
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.OccupiedSlotConflict,
                        EquipmentLoadoutConfig.TableName,
                        loadout.Id,
                        loadout.StableId,
                        nameof(EquipmentSchemaConfig.ExclusiveGroups),
                        "Equipment exclusive group is over-filled: " + group.GroupId + ".");
                    status = MaxStatus(status, EquipmentStateResolveStatus.OccupiedSlotConflict);
                }
            }

            return status;
        }

        private static EquipmentStateResolveStatus MaxStatus(EquipmentStateResolveStatus current, EquipmentStateResolveStatus next)
        {
            if (current == EquipmentStateResolveStatus.Success)
                return next;

            if (next == EquipmentStateResolveStatus.MissingWeaponConfig)
                return next;

            return current;
        }

        private static bool IsWeaponAllowedInSlot(EquipmentSlotEntry slot, WeaponConfig weapon)
        {
            if (slot.AllowedWeaponCategories != null && slot.AllowedWeaponCategories.Length > 0)
            {
                string category = weapon.Category.ToString();
                if (!CharacterResolverUtility.ContainsString(slot.AllowedWeaponCategories, category))
                    return false;
            }

            if (slot.RequiredTags != null)
            {
                for (int i = 0; i < slot.RequiredTags.Length; i++)
                {
                    if (!CharacterResolverUtility.ContainsString(weapon.Tags, slot.RequiredTags[i]))
                        return false;
                }
            }

            return true;
        }

        private static bool MatchesState(EquipmentStateConfig state, Dictionary<string, EquippedWeaponSlot> filledSlots)
        {
            if (state.MatchRules == null || state.MatchRules.Length == 0)
                return true;

            for (int i = 0; i < state.MatchRules.Length; i++)
            {
                if (!MatchesRule(state.MatchRules[i], filledSlots))
                    return false;
            }

            return true;
        }

        private static bool MatchesRule(EquipmentMatchRule rule, Dictionary<string, EquippedWeaponSlot> filledSlots)
        {
            if (!AllRequiredSlotsFilled(rule.RequiredFilledSlots, filledSlots))
                return false;

            if (!AllRequiredSlotsEmpty(rule.RequiredEmptySlots, filledSlots))
                return false;

            if (!AllRequiredCategoriesMatch(rule.RequiredWeaponCategoriesBySlot, filledSlots))
                return false;

            if (!AllRequiredTagsMatch(rule.RequiredWeaponTagsBySlot, filledSlots))
                return false;

            if (!AllForbiddenTagsAbsent(rule.ForbiddenWeaponTagsBySlot, filledSlots))
                return false;

            return true;
        }

        private static bool AllRequiredSlotsFilled(string[] requiredSlots, Dictionary<string, EquippedWeaponSlot> filledSlots)
        {
            if (requiredSlots == null)
                return true;

            for (int i = 0; i < requiredSlots.Length; i++)
            {
                if (!filledSlots.ContainsKey(requiredSlots[i]))
                    return false;
            }

            return true;
        }

        private static bool AllRequiredSlotsEmpty(string[] requiredSlots, Dictionary<string, EquippedWeaponSlot> filledSlots)
        {
            if (requiredSlots == null)
                return true;

            for (int i = 0; i < requiredSlots.Length; i++)
            {
                if (filledSlots.ContainsKey(requiredSlots[i]))
                    return false;
            }

            return true;
        }

        private static bool AllRequiredCategoriesMatch(string[] rules, Dictionary<string, EquippedWeaponSlot> filledSlots)
        {
            if (rules == null)
                return true;

            for (int i = 0; i < rules.Length; i++)
            {
                string slotId;
                string category;
                if (!CharacterResolverUtility.TrySplitSlotToken(rules[i], out slotId, out category))
                    return false;

                EquippedWeaponSlot equipped;
                if (!filledSlots.TryGetValue(slotId, out equipped) || equipped.Weapon == null)
                    return false;

                if (!CharacterResolverUtility.EqualsOrdinal(equipped.Weapon.Category.ToString(), category))
                    return false;
            }

            return true;
        }

        private static bool AllRequiredTagsMatch(string[] rules, Dictionary<string, EquippedWeaponSlot> filledSlots)
        {
            if (rules == null)
                return true;

            for (int i = 0; i < rules.Length; i++)
            {
                string slotId;
                string tag;
                if (!CharacterResolverUtility.TrySplitSlotToken(rules[i], out slotId, out tag))
                    return false;

                EquippedWeaponSlot equipped;
                if (!filledSlots.TryGetValue(slotId, out equipped) || equipped.Weapon == null)
                    return false;

                if (!CharacterResolverUtility.ContainsString(equipped.Weapon.Tags, tag))
                    return false;
            }

            return true;
        }

        private static bool AllForbiddenTagsAbsent(string[] rules, Dictionary<string, EquippedWeaponSlot> filledSlots)
        {
            if (rules == null)
                return true;

            for (int i = 0; i < rules.Length; i++)
            {
                string slotId;
                string tag;
                if (!CharacterResolverUtility.TrySplitSlotToken(rules[i], out slotId, out tag))
                    return false;

                EquippedWeaponSlot equipped;
                if (!filledSlots.TryGetValue(slotId, out equipped) || equipped.Weapon == null)
                    continue;

                if (CharacterResolverUtility.ContainsString(equipped.Weapon.Tags, tag))
                    return false;
            }

            return true;
        }

        private static WeaponConfig FindWeapon(IReadOnlyList<WeaponConfig> weaponConfigs, WeaponConfigId weaponId)
        {
            if (weaponConfigs == null || !weaponId.IsValid)
                return null;

            for (int i = 0; i < weaponConfigs.Count; i++)
            {
                WeaponConfig weapon = weaponConfigs[i];
                if (weapon != null && weapon.WeaponId.Equals(weaponId))
                    return weapon;
            }

            return null;
        }

        private static EquipmentStateConfig FindState(IReadOnlyList<EquipmentStateConfig> stateConfigs, EquipmentStateId stateId)
        {
            if (stateConfigs == null || !stateId.IsValid)
                return null;

            for (int i = 0; i < stateConfigs.Count; i++)
            {
                EquipmentStateConfig state = stateConfigs[i];
                if (state != null && state.StateId.Equals(stateId))
                    return state;
            }

            return null;
        }
    }
}
