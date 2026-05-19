using System;
using System.Collections.Generic;

namespace MxFramework.CharacterApplication
{
    public enum AbilityGrantLayer
    {
        CharacterBase = 0,
        Weapon = 1,
        EquipmentState = 2,
        RuntimeTemporary = 3,
        RuntimeDisable = 4
    }

    public readonly struct AbilityGrantHandle
    {
        public AbilityGrantHandle(
            CharacterAbilityId abilityId,
            AbilityLoadoutId loadoutId,
            AbilityGrantLayer layer,
            string sourceStableId)
        {
            AbilityId = abilityId;
            LoadoutId = loadoutId;
            Layer = layer;
            SourceStableId = sourceStableId ?? string.Empty;
        }

        public CharacterAbilityId AbilityId { get; }
        public AbilityLoadoutId LoadoutId { get; }
        public AbilityGrantLayer Layer { get; }
        public string SourceStableId { get; }
    }

    public readonly struct AbilityGrantResolveRequest
    {
        public AbilityGrantResolveRequest(
            CharacterConfig character,
            EquippedWeaponSlot[] equippedWeapons,
            EquipmentStateConfig activeEquipmentState,
            AbilityLoadoutConfig[] abilityLoadouts,
            AbilityLoadoutConfig runtimeTemporaryGrantLoadout = null,
            CharacterAbilityId[] runtimeDisabledAbilityIds = null,
            CharacterAbilityId[] knownAbilityIds = null)
        {
            Character = character;
            EquippedWeapons = equippedWeapons ?? Array.Empty<EquippedWeaponSlot>();
            ActiveEquipmentState = activeEquipmentState;
            AbilityLoadouts = abilityLoadouts ?? Array.Empty<AbilityLoadoutConfig>();
            RuntimeTemporaryGrantLoadout = runtimeTemporaryGrantLoadout;
            RuntimeDisabledAbilityIds = runtimeDisabledAbilityIds ?? Array.Empty<CharacterAbilityId>();
            KnownAbilityIds = knownAbilityIds ?? Array.Empty<CharacterAbilityId>();
        }

        public CharacterConfig Character { get; }
        public EquippedWeaponSlot[] EquippedWeapons { get; }
        public EquipmentStateConfig ActiveEquipmentState { get; }
        public AbilityLoadoutConfig[] AbilityLoadouts { get; }
        public AbilityLoadoutConfig RuntimeTemporaryGrantLoadout { get; }
        public CharacterAbilityId[] RuntimeDisabledAbilityIds { get; }
        public CharacterAbilityId[] KnownAbilityIds { get; }
    }

    public readonly struct AbilityGrantResolveResult
    {
        public AbilityGrantResolveResult(
            CharacterAbilityId[] effectiveAbilityIds,
            AbilitySlotBinding[] effectiveSlotBindings,
            AbilityGrantHandle[] grantHandles,
            AbilityLoadoutId[] appliedLoadoutIds,
            CharacterDiagnostic[] diagnostics)
        {
            EffectiveAbilityIds = effectiveAbilityIds ?? Array.Empty<CharacterAbilityId>();
            EffectiveSlotBindings = effectiveSlotBindings ?? Array.Empty<AbilitySlotBinding>();
            GrantHandles = grantHandles ?? Array.Empty<AbilityGrantHandle>();
            AppliedLoadoutIds = appliedLoadoutIds ?? Array.Empty<AbilityLoadoutId>();
            Diagnostics = diagnostics ?? Array.Empty<CharacterDiagnostic>();
        }

        public CharacterAbilityId[] EffectiveAbilityIds { get; }
        public AbilitySlotBinding[] EffectiveSlotBindings { get; }
        public AbilityGrantHandle[] GrantHandles { get; }
        public AbilityLoadoutId[] AppliedLoadoutIds { get; }
        public CharacterDiagnostic[] Diagnostics { get; }
    }

    public static class AbilityGrantResolver
    {
        public static AbilityGrantResolveResult Resolve(AbilityGrantResolveRequest request)
        {
            var diagnostics = new CharacterDiagnosticBuilder();
            var effectiveAbilities = new List<CharacterAbilityId>();
            var grantHandles = new List<AbilityGrantHandle>();
            var appliedLoadouts = new List<AbilityLoadoutId>();
            var slotBindingsBySlot = new Dictionary<string, AbilitySlotBinding>(StringComparer.Ordinal);
            var slotBindingOrder = new List<string>();
            var inputIntentToSlot = new Dictionary<string, string>(StringComparer.Ordinal);

            if (request.Character != null)
                ApplyLoadoutById(request.Character.BaseAbilityLoadoutId, AbilityGrantLayer.CharacterBase, request.Character.StableId, request, effectiveAbilities, grantHandles, appliedLoadouts, slotBindingsBySlot, slotBindingOrder, inputIntentToSlot, diagnostics);

            for (int i = 0; i < request.EquippedWeapons.Length; i++)
            {
                WeaponConfig weapon = request.EquippedWeapons[i].Weapon;
                if (weapon == null)
                    continue;

                ApplyLoadoutById(weapon.GrantedAbilityLoadoutId, AbilityGrantLayer.Weapon, weapon.StableId, request, effectiveAbilities, grantHandles, appliedLoadouts, slotBindingsBySlot, slotBindingOrder, inputIntentToSlot, diagnostics);
            }

            if (request.ActiveEquipmentState != null)
            {
                ApplyLoadoutById(
                    request.ActiveEquipmentState.GrantedAbilityLoadoutId,
                    AbilityGrantLayer.EquipmentState,
                    request.ActiveEquipmentState.StableId,
                    request,
                    effectiveAbilities,
                    grantHandles,
                    appliedLoadouts,
                    slotBindingsBySlot,
                    slotBindingOrder,
                    inputIntentToSlot,
                    diagnostics);
            }

            if (request.RuntimeTemporaryGrantLoadout != null)
            {
                ApplyLoadout(
                    request.RuntimeTemporaryGrantLoadout,
                    AbilityGrantLayer.RuntimeTemporary,
                    request.RuntimeTemporaryGrantLoadout.StableId,
                    request,
                    effectiveAbilities,
                    grantHandles,
                    appliedLoadouts,
                    slotBindingsBySlot,
                    slotBindingOrder,
                    inputIntentToSlot,
                    diagnostics);
            }

            for (int i = 0; i < request.RuntimeDisabledAbilityIds.Length; i++)
            {
                CharacterAbilityId abilityId = request.RuntimeDisabledAbilityIds[i];
                RemoveAbility(abilityId, effectiveAbilities, grantHandles, slotBindingsBySlot, slotBindingOrder, inputIntentToSlot);
            }

            return new AbilityGrantResolveResult(
                effectiveAbilities.ToArray(),
                CreateSlotBindingArray(slotBindingsBySlot, slotBindingOrder),
                grantHandles.ToArray(),
                appliedLoadouts.ToArray(),
                diagnostics.ToArray());
        }

        private static void ApplyLoadoutById(
            AbilityLoadoutId loadoutId,
            AbilityGrantLayer layer,
            string sourceStableId,
            AbilityGrantResolveRequest request,
            List<CharacterAbilityId> effectiveAbilities,
            List<AbilityGrantHandle> grantHandles,
            List<AbilityLoadoutId> appliedLoadouts,
            Dictionary<string, AbilitySlotBinding> slotBindingsBySlot,
            List<string> slotBindingOrder,
            Dictionary<string, string> inputIntentToSlot,
            CharacterDiagnosticBuilder diagnostics)
        {
            if (!loadoutId.IsValid)
                return;

            AbilityLoadoutConfig loadout = FindLoadout(request.AbilityLoadouts, loadoutId);
            if (loadout == null)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingAbilityLoadout,
                    AbilityLoadoutConfig.TableName,
                    loadoutId.Value,
                    sourceStableId,
                    nameof(AbilityLoadoutId),
                    "Ability loadout config is missing: " + loadoutId.Value + ".");
                return;
            }

            ApplyLoadout(loadout, layer, sourceStableId, request, effectiveAbilities, grantHandles, appliedLoadouts, slotBindingsBySlot, slotBindingOrder, inputIntentToSlot, diagnostics);
        }

        private static void ApplyLoadout(
            AbilityLoadoutConfig loadout,
            AbilityGrantLayer layer,
            string sourceStableId,
            AbilityGrantResolveRequest request,
            List<CharacterAbilityId> effectiveAbilities,
            List<AbilityGrantHandle> grantHandles,
            List<AbilityLoadoutId> appliedLoadouts,
            Dictionary<string, AbilitySlotBinding> slotBindingsBySlot,
            List<string> slotBindingOrder,
            Dictionary<string, string> inputIntentToSlot,
            CharacterDiagnosticBuilder diagnostics)
        {
            if (loadout == null)
                return;

            appliedLoadouts.Add(loadout.LoadoutId);

            for (int i = 0; i < loadout.RemoveAbilityIds.Length; i++)
            {
                RemoveAbility(loadout.RemoveAbilityIds[i], effectiveAbilities, grantHandles, slotBindingsBySlot, slotBindingOrder, inputIntentToSlot);
            }

            for (int i = 0; i < loadout.AbilityIds.Length; i++)
            {
                CharacterAbilityId abilityId = loadout.AbilityIds[i];
                if (!IsKnownAbility(abilityId, request.KnownAbilityIds))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.MissingAbility,
                        AbilityLoadoutConfig.TableName,
                        loadout.Id,
                        loadout.StableId,
                        "AbilityIds[" + i + "]",
                        "Ability id is invalid or not present in the known ability set.");
                    continue;
                }

                if (CharacterResolverUtility.ContainsAbilityId(effectiveAbilities.ToArray(), abilityId))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Warning,
                        CharacterDiagnosticCode.DuplicateAbilityGrant,
                        AbilityLoadoutConfig.TableName,
                        loadout.Id,
                        loadout.StableId,
                        "AbilityIds[" + i + "]",
                        "Ability is granted more than once and will be kept once: " + abilityId.Value + ".");
                    continue;
                }

                effectiveAbilities.Add(abilityId);
                grantHandles.Add(new AbilityGrantHandle(abilityId, loadout.LoadoutId, layer, sourceStableId));
            }

            for (int i = 0; i < loadout.SlotBindings.Length; i++)
            {
                AbilitySlotBinding binding = loadout.SlotBindings[i];
                if (!binding.AbilityId.IsValid)
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.MissingAbility,
                        AbilityLoadoutConfig.TableName,
                        loadout.Id,
                        loadout.StableId,
                        "SlotBindings[" + i + "].AbilityId",
                        "Ability slot binding references an invalid ability id.");
                    continue;
                }

                if (!CharacterResolverUtility.ContainsAbilityId(effectiveAbilities.ToArray(), binding.AbilityId))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.MissingAbility,
                        AbilityLoadoutConfig.TableName,
                        loadout.Id,
                        loadout.StableId,
                        "SlotBindings[" + i + "].AbilityId",
                        "Ability slot binding references an ability that is not effective.");
                    continue;
                }

                if (string.IsNullOrEmpty(binding.SlotId))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.AbilitySlotConflict,
                        AbilityLoadoutConfig.TableName,
                        loadout.Id,
                        loadout.StableId,
                        "SlotBindings[" + i + "].SlotId",
                        "Ability slot binding has an empty slot id.");
                    continue;
                }

                if (slotBindingsBySlot.ContainsKey(binding.SlotId))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.AbilitySlotConflict,
                        AbilityLoadoutConfig.TableName,
                        loadout.Id,
                        loadout.StableId,
                        "SlotBindings[" + i + "].SlotId",
                        "Multiple ability bindings target the same slot. The first binding is kept.");
                    continue;
                }

                if (!string.IsNullOrEmpty(binding.InputIntentId))
                {
                    string existingSlot;
                    if (inputIntentToSlot.TryGetValue(binding.InputIntentId, out existingSlot))
                    {
                        diagnostics.Add(
                            CharacterDiagnosticSeverity.Error,
                            CharacterDiagnosticCode.AbilityInputIntentConflict,
                            AbilityLoadoutConfig.TableName,
                            loadout.Id,
                            loadout.StableId,
                            "SlotBindings[" + i + "].InputIntentId",
                            "Input intent is already bound by slot: " + existingSlot + ".");
                        continue;
                    }

                    inputIntentToSlot.Add(binding.InputIntentId, binding.SlotId);
                }

                slotBindingsBySlot.Add(binding.SlotId, binding);
                slotBindingOrder.Add(binding.SlotId);
            }
        }

        private static void RemoveAbility(
            CharacterAbilityId abilityId,
            List<CharacterAbilityId> effectiveAbilities,
            List<AbilityGrantHandle> grantHandles,
            Dictionary<string, AbilitySlotBinding> slotBindingsBySlot,
            List<string> slotBindingOrder,
            Dictionary<string, string> inputIntentToSlot)
        {
            if (!abilityId.IsValid)
                return;

            for (int i = effectiveAbilities.Count - 1; i >= 0; i--)
            {
                if (effectiveAbilities[i].Equals(abilityId))
                    effectiveAbilities.RemoveAt(i);
            }

            for (int i = grantHandles.Count - 1; i >= 0; i--)
            {
                if (grantHandles[i].AbilityId.Equals(abilityId))
                    grantHandles.RemoveAt(i);
            }

            for (int i = slotBindingOrder.Count - 1; i >= 0; i--)
            {
                string slotId = slotBindingOrder[i];
                AbilitySlotBinding binding;
                if (!slotBindingsBySlot.TryGetValue(slotId, out binding))
                    continue;

                if (!binding.AbilityId.Equals(abilityId))
                    continue;

                slotBindingsBySlot.Remove(slotId);
                slotBindingOrder.RemoveAt(i);
                if (!string.IsNullOrEmpty(binding.InputIntentId))
                    inputIntentToSlot.Remove(binding.InputIntentId);
            }
        }

        private static AbilitySlotBinding[] CreateSlotBindingArray(
            Dictionary<string, AbilitySlotBinding> slotBindingsBySlot,
            List<string> slotBindingOrder)
        {
            if (slotBindingOrder.Count == 0)
                return Array.Empty<AbilitySlotBinding>();

            var bindings = new List<AbilitySlotBinding>();
            for (int i = 0; i < slotBindingOrder.Count; i++)
            {
                AbilitySlotBinding binding;
                if (slotBindingsBySlot.TryGetValue(slotBindingOrder[i], out binding))
                    bindings.Add(binding);
            }

            return bindings.ToArray();
        }

        private static bool IsKnownAbility(CharacterAbilityId abilityId, CharacterAbilityId[] knownAbilityIds)
        {
            if (!abilityId.IsValid)
                return false;

            if (knownAbilityIds == null || knownAbilityIds.Length == 0)
                return true;

            return CharacterResolverUtility.ContainsAbilityId(knownAbilityIds, abilityId);
        }

        private static AbilityLoadoutConfig FindLoadout(AbilityLoadoutConfig[] loadouts, AbilityLoadoutId loadoutId)
        {
            if (loadouts == null || !loadoutId.IsValid)
                return null;

            for (int i = 0; i < loadouts.Length; i++)
            {
                AbilityLoadoutConfig loadout = loadouts[i];
                if (loadout != null && loadout.LoadoutId.Equals(loadoutId))
                    return loadout;
            }

            return null;
        }
    }
}
