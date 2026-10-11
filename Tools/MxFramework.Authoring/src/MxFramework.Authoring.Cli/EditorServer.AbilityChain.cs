using System.Globalization;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string AbilityChainStatusReady = "Ready";
    private const string AbilityChainStatusBlocked = "Blocked";
    private const string AbilityChainStageMissing = "Missing";
    private const string AbilityChainStageMissingTarget = "MissingTarget";
    private const string AbilityChainStageDeferred = "Deferred";

    internal static CharacterAbilityChainSnapshot ReadCharacterAbilityChain(
        string rootPath,
        string packageRelative,
        JsonSerializerOptions jsonOptions)
    {
        string resolvedPackageRelative = string.IsNullOrWhiteSpace(packageRelative)
            ? DefaultCharacterPackageRelativePath
            : packageRelative;
        CharacterAuthoringCompileResult compile = CompileCharacterPackage(
            rootPath,
            resolvedPackageRelative,
            checkFiles: true,
            checkHashes: false,
            jsonOptions);
        List<PatchEntry> entries = compile?.GeneratedConfigPatch?.Patch?.Entries ?? new List<PatchEntry>();
        PatchEntry character = FindAbilityChainEntry(entries, CharacterApplicationCompilerTableNames.CharacterConfig);
        var snapshot = new CharacterAbilityChainSnapshot
        {
            PackageRelative = resolvedPackageRelative,
            PackageId = compile?.PackageId ?? string.Empty,
            CharacterStableId = character?.Fields.GetScalar("StableId") ?? compile?.PackageStableId ?? string.Empty,
            GeneratedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            RuntimeBindingMode = "ExplicitCharacterGameplayAbilityBindingCatalog"
        };

        if (character == null)
        {
            snapshot.Status = AbilityChainStatusBlocked;
            snapshot.Diagnostics.Add(new CharacterAbilityChainDiagnostic
            {
                Code = "ABILITY_CHAIN_CHARACTER_CONFIG_MISSING",
                Message = "Compiled CharacterConfig is missing; ability scenarios cannot be resolved."
            });
            return snapshot;
        }

        AbilityActionBindingReadiness actionBindings = ReadAbilityActionBindings(rootPath, snapshot.PackageId);
        snapshot.ActionBindingSourcePaths.AddRange(actionBindings.SourcePaths);

        Dictionary<int, PatchEntry> abilityLoadouts = IndexAbilityChainEntries(
            entries,
            CharacterApplicationCompilerTableNames.AbilityLoadoutConfig);
        Dictionary<int, PatchEntry> weapons = IndexAbilityChainEntries(
            entries,
            CharacterApplicationCompilerTableNames.WeaponConfig);
        Dictionary<int, PatchEntry> combatActionSets = IndexAbilityChainEntries(
            entries,
            CharacterApplicationCompilerTableNames.CombatActionSetConfig);
        List<PatchEntry> equipmentStates = FindAbilityChainEntries(
            entries,
            CharacterApplicationCompilerTableNames.EquipmentStateConfig);
        List<PatchEntry> equipmentLoadouts = FindAbilityChainEntries(
            entries,
            CharacterApplicationCompilerTableNames.EquipmentLoadoutConfig);

        int defaultLoadoutId = ParseAbilityChainInt(character.Fields.GetScalar("DefaultLoadoutId"));
        int baseAbilityLoadoutId = ParseAbilityChainInt(character.Fields.GetScalar("BaseAbilityLoadoutId"));
        equipmentLoadouts.Sort(CompareAbilityChainPatchEntries);
        for (int i = 0; i < equipmentLoadouts.Count; i++)
        {
            PatchEntry equipmentLoadout = equipmentLoadouts[i];
            CharacterAbilityScenarioSnapshot scenario = BuildAbilityScenario(
                equipmentLoadout,
                snapshot.CharacterStableId,
                defaultLoadoutId,
                baseAbilityLoadoutId,
                abilityLoadouts,
                weapons,
                equipmentStates,
                combatActionSets,
                actionBindings);
            snapshot.Scenarios.Add(scenario);
            if (scenario.IsDefault)
                snapshot.DefaultScenarioId = scenario.ScenarioId;
        }

        if (snapshot.Scenarios.Count == 0)
        {
            snapshot.Status = AbilityChainStatusBlocked;
            snapshot.Diagnostics.Add(new CharacterAbilityChainDiagnostic
            {
                Code = "ABILITY_CHAIN_SCENARIO_MISSING",
                Message = "Compiled EquipmentLoadoutConfig entries are missing."
            });
            return snapshot;
        }

        if (string.IsNullOrWhiteSpace(snapshot.DefaultScenarioId))
        {
            snapshot.DefaultScenarioId = snapshot.Scenarios[0].ScenarioId;
            snapshot.Diagnostics.Add(new CharacterAbilityChainDiagnostic
            {
                Code = "ABILITY_CHAIN_DEFAULT_SCENARIO_MISSING",
                Message = "CharacterConfig.DefaultLoadoutId did not match a compiled equipment loadout; the first scenario is used for preview."
            });
        }

        var potentialAbilityIds = new HashSet<int>();
        for (int i = 0; i < snapshot.Scenarios.Count; i++)
        {
            List<CharacterAbilityChainAbility> abilities = snapshot.Scenarios[i].Abilities;
            for (int j = 0; j < abilities.Count; j++)
                potentialAbilityIds.Add(abilities[j].AbilityId);
        }

        snapshot.PotentialAbilityCount = potentialAbilityIds.Count;
        snapshot.Status = AbilityChainStatusReady;
        return snapshot;
    }

    private static CharacterAbilityScenarioSnapshot BuildAbilityScenario(
        PatchEntry equipmentLoadout,
        string characterStableId,
        int defaultLoadoutId,
        int baseAbilityLoadoutId,
        Dictionary<int, PatchEntry> abilityLoadouts,
        Dictionary<int, PatchEntry> weapons,
        List<PatchEntry> equipmentStates,
        Dictionary<int, PatchEntry> combatActionSets,
        AbilityActionBindingReadiness actionBindings)
    {
        int loadoutId = ParseAbilityChainInt(equipmentLoadout?.Id);
        string loadoutStableId = equipmentLoadout?.Fields.GetScalar("StableId") ?? string.Empty;
        PatchEntry equipmentState = FindAbilityChainEquipmentState(equipmentStates, equipmentLoadout);
        int combatActionSetId = ParseAbilityChainInt(equipmentState?.Fields.GetScalar("CombatActionSetId"));
        combatActionSets.TryGetValue(combatActionSetId, out PatchEntry combatActionSet);
        string actionSetStableId = combatActionSet?.Fields.GetScalar("StableId") ?? string.Empty;
        string configurationId = GetAbilityChainConfigurationId(loadoutStableId);
        var scenario = new CharacterAbilityScenarioSnapshot
        {
            ScenarioId = configurationId,
            ConfigurationId = configurationId,
            LoadoutId = loadoutId,
            StableId = loadoutStableId,
            DisplayName = FormatAbilityChainScenarioName(loadoutStableId),
            IsDefault = loadoutId == defaultLoadoutId,
            EquipmentStateStableId = equipmentState?.Fields.GetScalar("StableId") ?? string.Empty,
            ActionSetStableId = actionSetStableId,
            SourceCounts = CreateAbilityChainSourceCounts()
        };
        var effective = new Dictionary<int, ScenarioAbilityAccumulator>();
        var slotOwners = new Dictionary<string, int>(StringComparer.Ordinal);
        var inputOwners = new Dictionary<string, int>(StringComparer.Ordinal);

        ApplyAbilityChainLoadout(
            baseAbilityLoadoutId,
            "base",
            "CharacterBase",
            characterStableId,
            abilityLoadouts,
            effective,
            slotOwners,
            inputOwners,
            scenario.Diagnostics);

        List<FieldValue> equipmentSlots = GetAbilityChainList(equipmentLoadout, "Slots");
        for (int i = 0; i < equipmentSlots.Count; i++)
        {
            FieldValue slot = equipmentSlots[i];
            int weaponId = GetAbilityChainMapInt(slot, "WeaponId");
            if (!weapons.TryGetValue(weaponId, out PatchEntry weapon))
            {
                scenario.Diagnostics.Add(new CharacterAbilityChainDiagnostic
                {
                    Code = "ABILITY_CHAIN_WEAPON_MISSING",
                    Message = "Equipment slot references missing WeaponConfig " + weaponId.ToString(CultureInfo.InvariantCulture) + "."
                });
                continue;
            }

            ApplyAbilityChainLoadout(
                ParseAbilityChainInt(weapon.Fields.GetScalar("GrantedAbilityLoadoutId")),
                "weapon",
                "Weapon",
                weapon.Fields.GetScalar("StableId"),
                abilityLoadouts,
                effective,
                slotOwners,
                inputOwners,
                scenario.Diagnostics);
        }

        if (equipmentState != null)
        {
            ApplyAbilityChainLoadout(
                ParseAbilityChainInt(equipmentState.Fields.GetScalar("GrantedAbilityLoadoutId")),
                "equipmentState",
                "EquipmentState",
                equipmentState.Fields.GetScalar("StableId"),
                abilityLoadouts,
                effective,
                slotOwners,
                inputOwners,
                scenario.Diagnostics);
        }
        else
        {
            scenario.Diagnostics.Add(new CharacterAbilityChainDiagnostic
            {
                Code = "ABILITY_CHAIN_EQUIPMENT_STATE_MISSING",
                Message = "No compiled EquipmentStateConfig matches " + loadoutStableId + "."
            });
        }

        var abilityIds = new List<int>(effective.Keys);
        abilityIds.Sort();
        for (int i = 0; i < abilityIds.Count; i++)
        {
            ScenarioAbilityAccumulator accumulator = effective[abilityIds[i]];
            CharacterAbilityChainAbility ability = BuildAbilityChainAbility(
                accumulator,
                actionSetStableId,
                actionBindings);
            scenario.Abilities.Add(ability);
            string sourceKind = accumulator.GrantSources.Count > 0 ? accumulator.GrantSources[0].SourceKind : "base";
            if (!scenario.SourceCounts.ContainsKey(sourceKind))
                scenario.SourceCounts[sourceKind] = 0;
            scenario.SourceCounts[sourceKind]++;
        }

        scenario.EffectiveAbilityCount = scenario.Abilities.Count;
        return scenario;
    }

    private static void ApplyAbilityChainLoadout(
        int loadoutId,
        string sourceKind,
        string layer,
        string sourceStableId,
        Dictionary<int, PatchEntry> abilityLoadouts,
        Dictionary<int, ScenarioAbilityAccumulator> effective,
        Dictionary<string, int> slotOwners,
        Dictionary<string, int> inputOwners,
        List<CharacterAbilityChainDiagnostic> diagnostics)
    {
        if (loadoutId <= 0)
            return;
        if (!abilityLoadouts.TryGetValue(loadoutId, out PatchEntry loadout))
        {
            diagnostics.Add(new CharacterAbilityChainDiagnostic
            {
                Code = "ABILITY_CHAIN_LOADOUT_MISSING",
                Message = "AbilityLoadoutConfig " + loadoutId.ToString(CultureInfo.InvariantCulture) + " is missing."
            });
            return;
        }

        List<int> removals = GetAbilityChainIntList(loadout, "RemoveAbilityIds");
        for (int i = 0; i < removals.Count; i++)
            RemoveAbilityChainAbility(removals[i], effective, slotOwners, inputOwners);

        var grant = new CharacterAbilityGrantSource
        {
            Layer = layer,
            SourceKind = sourceKind,
            SourceStableId = sourceStableId ?? string.Empty,
            LoadoutId = loadoutId,
            LoadoutStableId = loadout.Fields.GetScalar("StableId")
        };
        List<int> grants = GetAbilityChainIntList(loadout, "AbilityIds");
        for (int i = 0; i < grants.Count; i++)
        {
            int abilityId = grants[i];
            if (!effective.TryGetValue(abilityId, out ScenarioAbilityAccumulator accumulator))
            {
                accumulator = new ScenarioAbilityAccumulator(abilityId);
                effective.Add(abilityId, accumulator);
                accumulator.GrantSources.Add(grant.Clone());
            }
            else
            {
                diagnostics.Add(new CharacterAbilityChainDiagnostic
                {
                    Code = "ABILITY_CHAIN_DUPLICATE_GRANT",
                    Message = "Ability " + abilityId.ToString(CultureInfo.InvariantCulture) + " is granted more than once; the first grant remains authoritative."
                });
            }
        }

        List<FieldValue> bindings = GetAbilityChainList(loadout, "SlotBindings");
        for (int i = 0; i < bindings.Count; i++)
        {
            FieldValue binding = bindings[i];
            int abilityId = GetAbilityChainMapInt(binding, "AbilityId");
            string slotId = GetAbilityChainMapScalar(binding, "SlotId");
            string inputIntentId = GetAbilityChainMapScalar(binding, "InputIntentId");
            if (!effective.TryGetValue(abilityId, out ScenarioAbilityAccumulator accumulator))
            {
                diagnostics.Add(new CharacterAbilityChainDiagnostic
                {
                    Code = "ABILITY_CHAIN_SLOT_ABILITY_NOT_EFFECTIVE",
                    Message = "Slot " + slotId + " references ability " + abilityId.ToString(CultureInfo.InvariantCulture) + " which is not effective."
                });
                continue;
            }
            if (string.IsNullOrWhiteSpace(slotId) || slotOwners.ContainsKey(slotId))
            {
                diagnostics.Add(new CharacterAbilityChainDiagnostic
                {
                    Code = "ABILITY_CHAIN_SLOT_CONFLICT",
                    Message = "Ability slot is empty or already occupied: " + slotId + "."
                });
                continue;
            }
            if (!string.IsNullOrWhiteSpace(inputIntentId) && inputOwners.ContainsKey(inputIntentId))
            {
                diagnostics.Add(new CharacterAbilityChainDiagnostic
                {
                    Code = "ABILITY_CHAIN_INPUT_CONFLICT",
                    Message = "Input intent is already occupied: " + inputIntentId + "."
                });
                continue;
            }

            accumulator.SlotBindings.Add(new CharacterAbilitySlotSnapshot
            {
                SlotId = slotId,
                InputIntentId = inputIntentId
            });
            slotOwners.Add(slotId, abilityId);
            if (!string.IsNullOrWhiteSpace(inputIntentId))
                inputOwners.Add(inputIntentId, abilityId);
        }
    }

    private static CharacterAbilityChainAbility BuildAbilityChainAbility(
        ScenarioAbilityAccumulator accumulator,
        string actionSetStableId,
        AbilityActionBindingReadiness actionBindings)
    {
        var ability = new CharacterAbilityChainAbility
        {
            AbilityId = accumulator.AbilityId,
            GrantedStatus = AbilityChainStatusReady,
            SlotStatus = accumulator.SlotBindings.Count > 0 ? AbilityChainStatusReady : "Unbound",
            SlotMessage = accumulator.SlotBindings.Count > 0
                ? "AbilityLoadoutConfig supplies slot and input intent bindings."
                : "No slot/input binding is configured; this may be a passive or event-driven ability.",
            RuntimeStatus = AbilityChainStageDeferred,
            RuntimeMessage = "CharacterGameplayAbilityBindingCatalog requires an explicit runtime mapping."
        };
        ability.GrantSources.AddRange(accumulator.GrantSources);
        ability.SlotBindings.AddRange(accumulator.SlotBindings);

        for (int i = 0; i < actionBindings.Bindings.Count; i++)
        {
            AbilityActionBindingRecord record = actionBindings.Bindings[i];
            bool matchesActionSet = string.Equals(record.ActionSetStableId, actionSetStableId, StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(actionSetStableId) && record.ActionSetStableId != null && (
                    record.ActionSetStableId.EndsWith("." + actionSetStableId, StringComparison.Ordinal)
                    || actionSetStableId.EndsWith("." + record.ActionSetStableId, StringComparison.Ordinal)));
            if (record.AbilityId != accumulator.AbilityId || !matchesActionSet)
                continue;
            ability.ActionBindings.Add(new CharacterAbilityActionBindingSnapshot
            {
                ActionSetStableId = record.ActionSetStableId,
                ActionId = record.ActionId,
                ActionExists = record.ActionExists,
                SourcePath = record.SourcePath,
                SourceObjectPath = record.SourceObjectPath
            });
        }

        if (ability.ActionBindings.Count == 0)
            ability.ActionStatus = AbilityChainStageMissing;
        else if (ability.ActionBindings.Exists(binding => !binding.ActionExists))
            ability.ActionStatus = AbilityChainStageMissingTarget;
        else
            ability.ActionStatus = AbilityChainStatusReady;

        ability.OverallStatus = AbilityChainStageDeferred;
        if (ability.ActionStatus == AbilityChainStageMissing)
        {
            ability.NextAction = "BindCharacterAction";
        }
        else if (ability.ActionStatus == AbilityChainStageMissingTarget)
        {
            ability.NextAction = "RepairCharacterAction";
        }
        else
        {
            ability.OverallStatus = AbilityChainStageDeferred;
            ability.NextAction = "RegisterGameplayBinding";
        }

        return ability;
    }

    private static PatchEntry FindAbilityChainEquipmentState(List<PatchEntry> states, PatchEntry loadout)
    {
        int exactStateId = ParseAbilityChainInt(loadout?.Fields.GetScalar("EquipmentStateId"));
        for (int i = 0; i < states.Count; i++)
        {
            PatchEntry state = states[i];
            if (state != null && ParseAbilityChainInt(state.Id) == exactStateId)
                return state;
        }
        return null;
    }

    private static string GetAbilityChainConfigurationId(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return string.Empty;
        int index = stableId.LastIndexOf('.');
        return index >= 0 && index + 1 < stableId.Length ? stableId[(index + 1)..] : stableId;
    }

    private static void RemoveAbilityChainAbility(
        int abilityId,
        Dictionary<int, ScenarioAbilityAccumulator> effective,
        Dictionary<string, int> slotOwners,
        Dictionary<string, int> inputOwners)
    {
        if (!effective.TryGetValue(abilityId, out ScenarioAbilityAccumulator accumulator))
            return;
        for (int i = 0; i < accumulator.SlotBindings.Count; i++)
        {
            CharacterAbilitySlotSnapshot binding = accumulator.SlotBindings[i];
            slotOwners.Remove(binding.SlotId);
            if (!string.IsNullOrWhiteSpace(binding.InputIntentId))
                inputOwners.Remove(binding.InputIntentId);
        }
        effective.Remove(abilityId);
    }

    private static Dictionary<int, PatchEntry> IndexAbilityChainEntries(List<PatchEntry> entries, string source)
    {
        var result = new Dictionary<int, PatchEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            PatchEntry entry = entries[i];
            if (entry == null || !string.Equals(entry.Source, source, StringComparison.Ordinal))
                continue;
            int id = ParseAbilityChainInt(entry.Id);
            if (id > 0 && !result.ContainsKey(id))
                result.Add(id, entry);
        }
        return result;
    }

    private static PatchEntry FindAbilityChainEntry(List<PatchEntry> entries, string source)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            PatchEntry entry = entries[i];
            if (entry != null && string.Equals(entry.Source, source, StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    private static List<PatchEntry> FindAbilityChainEntries(List<PatchEntry> entries, string source)
    {
        var result = new List<PatchEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            PatchEntry entry = entries[i];
            if (entry != null && string.Equals(entry.Source, source, StringComparison.Ordinal))
                result.Add(entry);
        }
        return result;
    }

    private static List<FieldValue> GetAbilityChainList(PatchEntry entry, string field)
    {
        if (entry?.Fields != null
            && entry.Fields.TryGetValue(field, out FieldValue value)
            && value?.Kind == FieldValueKind.List
            && value.List != null)
        {
            return value.List;
        }
        return new List<FieldValue>();
    }

    private static List<int> GetAbilityChainIntList(PatchEntry entry, string field)
    {
        List<FieldValue> values = GetAbilityChainList(entry, field);
        var result = new List<int>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            int value = ParseAbilityChainInt(values[i]?.Scalar);
            if (value > 0)
                result.Add(value);
        }
        return result;
    }

    private static string GetAbilityChainMapScalar(FieldValue value, string field)
    {
        if (value?.Kind != FieldValueKind.Map || value.Map == null)
            return string.Empty;
        return value.Map.GetScalar(field);
    }

    private static int GetAbilityChainMapInt(FieldValue value, string field)
    {
        return ParseAbilityChainInt(GetAbilityChainMapScalar(value, field));
    }

    private static bool AbilityChainListContains(PatchEntry entry, string field, string expected)
    {
        List<FieldValue> values = GetAbilityChainList(entry, field);
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i]?.Scalar, expected, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static int ParseAbilityChainInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
    }

    private static int CompareAbilityChainPatchEntries(PatchEntry left, PatchEntry right)
    {
        return ParseAbilityChainInt(left?.Id).CompareTo(ParseAbilityChainInt(right?.Id));
    }

    private static Dictionary<string, int> CreateAbilityChainSourceCounts()
    {
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["base"] = 0,
            ["weapon"] = 0,
            ["equipmentState"] = 0
        };
    }

    private static string FormatAbilityChainScenarioName(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return "Unnamed loadout";
        int index = stableId.LastIndexOf('.');
        string name = index >= 0 && index + 1 < stableId.Length ? stableId[(index + 1)..] : stableId;
        return name.Replace('_', ' ');
    }

    private sealed class ScenarioAbilityAccumulator
    {
        public ScenarioAbilityAccumulator(int abilityId)
        {
            AbilityId = abilityId;
        }

        public int AbilityId { get; }
        public List<CharacterAbilityGrantSource> GrantSources { get; } = new List<CharacterAbilityGrantSource>();
        public List<CharacterAbilitySlotSnapshot> SlotBindings { get; } = new List<CharacterAbilitySlotSnapshot>();
    }

}

internal sealed class CharacterAbilityChainSnapshot
{
    public string SchemaVersion { get; set; } = "1.0";
    public string PackageRelative { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string CharacterStableId { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public string GeneratedAtUtc { get; set; } = string.Empty;
    public string DefaultScenarioId { get; set; } = string.Empty;
    public int PotentialAbilityCount { get; set; }
    public string RuntimeBindingMode { get; set; } = string.Empty;
    public List<string> ActionBindingSourcePaths { get; set; } = new List<string>();
    public List<CharacterAbilityScenarioSnapshot> Scenarios { get; set; } = new List<CharacterAbilityScenarioSnapshot>();
    public List<CharacterAbilityChainDiagnostic> Diagnostics { get; set; } = new List<CharacterAbilityChainDiagnostic>();
}

internal sealed class CharacterAbilityScenarioSnapshot
{
    public string ScenarioId { get; set; } = string.Empty;
    public string ConfigurationId { get; set; } = string.Empty;
    public int LoadoutId { get; set; }
    public string StableId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string EquipmentStateStableId { get; set; } = string.Empty;
    public string ActionSetStableId { get; set; } = string.Empty;
    public int EffectiveAbilityCount { get; set; }
    public Dictionary<string, int> SourceCounts { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
    public List<CharacterAbilityChainAbility> Abilities { get; set; } = new List<CharacterAbilityChainAbility>();
    public List<CharacterAbilityChainDiagnostic> Diagnostics { get; set; } = new List<CharacterAbilityChainDiagnostic>();
}

internal sealed class CharacterAbilityChainAbility
{
    public int AbilityId { get; set; }
    public string GrantedStatus { get; set; } = "Unknown";
    public string SlotStatus { get; set; } = "Unknown";
    public string SlotMessage { get; set; } = string.Empty;
    public string ActionStatus { get; set; } = "Unknown";
    public string RuntimeStatus { get; set; } = "Unknown";
    public string RuntimeMessage { get; set; } = string.Empty;
    public string OverallStatus { get; set; } = "Unknown";
    public string NextAction { get; set; } = string.Empty;
    public List<CharacterAbilityGrantSource> GrantSources { get; set; } = new List<CharacterAbilityGrantSource>();
    public List<CharacterAbilitySlotSnapshot> SlotBindings { get; set; } = new List<CharacterAbilitySlotSnapshot>();
    public List<CharacterAbilityActionBindingSnapshot> ActionBindings { get; set; } = new List<CharacterAbilityActionBindingSnapshot>();
}

internal sealed class CharacterAbilityGrantSource
{
    public string Layer { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string SourceStableId { get; set; } = string.Empty;
    public int LoadoutId { get; set; }
    public string LoadoutStableId { get; set; } = string.Empty;

    public CharacterAbilityGrantSource Clone()
    {
        return new CharacterAbilityGrantSource
        {
            Layer = Layer,
            SourceKind = SourceKind,
            SourceStableId = SourceStableId,
            LoadoutId = LoadoutId,
            LoadoutStableId = LoadoutStableId
        };
    }
}

internal sealed class CharacterAbilitySlotSnapshot
{
    public string SlotId { get; set; } = string.Empty;
    public string InputIntentId { get; set; } = string.Empty;
}

internal sealed class CharacterAbilityActionBindingSnapshot
{
    public string ActionSetStableId { get; set; } = string.Empty;
    public int ActionId { get; set; }
    public bool ActionExists { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string SourceObjectPath { get; set; } = string.Empty;
}

internal sealed class CharacterAbilityChainDiagnostic
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
