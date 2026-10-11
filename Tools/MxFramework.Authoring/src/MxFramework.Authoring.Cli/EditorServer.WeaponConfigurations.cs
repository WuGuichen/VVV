using System.Globalization;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    internal static CharacterCombinedWeaponConfigurationsSnapshot ReadCharacterWeaponConfigurations(
        string rootPath,
        string packageRelative,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterAuthoringCompileResult compile = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            Options = new CharacterAuthoringCompileOptions { ValidateResourceFiles = true }
        });
        var snapshot = new CharacterCombinedWeaponConfigurationsSnapshot
        {
            PackageRelative = packageRelative,
            PackageId = package.Manifest?.PackageId ?? string.Empty,
            DefaultCombinedWeaponConfigurationId =
                package.ApplicationConfig?.DefaultCombinedWeaponConfigurationId ?? string.Empty,
            Status = compile.GateReport?.ExportBlocked == true ? "Blocked" : "Ready"
        };

        Dictionary<string, SingleWeaponConfiguration> singles = IndexSingleWeaponConfigurations(
            package.GlobalWeaponLibrary?.SingleWeaponConfigurations);
        List<PatchEntry> entries = compile.GeneratedConfigPatch?.Patch?.Entries ?? new List<PatchEntry>();
        Dictionary<int, PatchEntry> states = IndexWeaponConfigurationEntries(entries,
            CharacterApplicationCompilerTableNames.EquipmentStateConfig);
        Dictionary<int, PatchEntry> actions = IndexWeaponConfigurationEntries(entries,
            CharacterApplicationCompilerTableNames.CombatActionSetConfig);
        Dictionary<int, PatchEntry> abilities = IndexWeaponConfigurationEntries(entries,
            CharacterApplicationCompilerTableNames.AbilityLoadoutConfig);
        Dictionary<string, CharacterWeaponSlotProfile> characterSlots = IndexCharacterWeaponSlots(
            package.Geometry?.WeaponSlots);
        var ordered = new List<CombinedWeaponConfigurationAuthoringSummary>(
            package.ApplicationConfig?.CombinedWeaponConfigurations ??
            new List<CombinedWeaponConfigurationAuthoringSummary>());
        ordered.Sort((left, right) => string.CompareOrdinal(
            left?.CombinedWeaponConfigurationId,
            right?.CombinedWeaponConfigurationId));

        for (int i = 0; i < ordered.Count; i++)
        {
            CombinedWeaponConfigurationAuthoringSummary source = ordered[i];
            if (source == null)
                continue;
            PatchEntry loadout = FindWeaponConfigurationLoadout(
                entries,
                snapshot.PackageId,
                source.CombinedWeaponConfigurationId);
            int stateId = ParseAbilityChainInt(loadout?.Fields.GetScalar("EquipmentStateId"));
            states.TryGetValue(stateId, out PatchEntry state);
            int actionId = ParseAbilityChainInt(state?.Fields.GetScalar("CombatActionSetId"));
            int abilityId = ParseAbilityChainInt(state?.Fields.GetScalar("GrantedAbilityLoadoutId"));
            actions.TryGetValue(actionId, out PatchEntry action);
            abilities.TryGetValue(abilityId, out PatchEntry ability);
            var item = new CharacterCombinedWeaponConfigurationSnapshot
            {
                CombinedWeaponConfigurationId = source.CombinedWeaponConfigurationId,
                DisplayName = source.DisplayName,
                IsDefault = string.Equals(
                    source.CombinedWeaponConfigurationId,
                    snapshot.DefaultCombinedWeaponConfigurationId,
                    StringComparison.Ordinal),
                LoadoutId = ParseAbilityChainInt(loadout?.Id),
                LoadoutStableId = loadout?.Fields.GetScalar("StableId") ?? string.Empty,
                EquipmentStateId = stateId,
                EquipmentStateStableId = state?.Fields.GetScalar("StableId") ?? string.Empty,
                AnimationConfigurationId = source.AnimationConfigurationId,
                AbilityConfigurationId = source.AbilityConfigurationId,
                ReactionConfigurationId = source.ReactionConfigurationId,
                AnimationConfiguration = GlobalWeaponConfigurationResolver.FindAnimation(
                    package.GlobalWeaponLibrary,
                    source.AnimationConfigurationId),
                AbilityConfiguration = GlobalWeaponConfigurationResolver.FindAbility(
                    package.GlobalWeaponLibrary,
                    source.AbilityConfigurationId),
                ReactionConfiguration = GlobalWeaponConfigurationResolver.FindReaction(
                    package.GlobalWeaponLibrary,
                    source.ReactionConfigurationId),
                ActionConfiguration = source.ActionConfiguration,
                GeneratedAnimationProfileId = state?.Fields.GetScalar("AnimationProfileId") ?? string.Empty,
                GeneratedActionSetId = action?.Fields.GetScalar("StableId") ?? string.Empty,
                GeneratedAbilityLoadoutId = ability?.Fields.GetScalar("StableId") ?? string.Empty,
                GenerationStatus = compile.GateReport?.ExportBlocked == true ? "Blocked" : "Generated"
            };
            item.AnimationReferences = FindGlobalConfigurationReferences(
                rootPath,
                "animation",
                source.AnimationConfigurationId,
                jsonOptions);
            item.AbilityReferences = FindGlobalConfigurationReferences(
                rootPath,
                "ability",
                source.AbilityConfigurationId,
                jsonOptions);
            item.ReactionReferences = FindGlobalConfigurationReferences(
                rootPath,
                "reaction",
                source.ReactionConfigurationId,
                jsonOptions);
            item.Slots.Add(BuildWeaponConfigurationSlot(
                snapshot.PackageId,
                CharacterWeaponConfigurationValidator.MainHandSlotId,
                source,
                singles,
                characterSlots));
            item.Slots.Add(BuildWeaponConfigurationSlot(
                snapshot.PackageId,
                CharacterWeaponConfigurationValidator.OffHandSlotId,
                source,
                singles,
                characterSlots));
            AddWeaponConfigurationDiagnostics(item, compile.GateReport, source.CombinedWeaponConfigurationId);
            snapshot.CombinedWeaponConfigurations.Add(item);
        }

        if (snapshot.CombinedWeaponConfigurations.Count == 0)
            snapshot.Diagnostics.Add(new CharacterWeaponConfigurationDiagnostic
            {
                Code = CharacterAuthoringValidationCodes.CombinedWeaponConfigurationMissing,
                Message = "No editable combined weapon configuration exists."
            });
        return snapshot;
    }

    private static CharacterCombinedWeaponConfigurationSlotSnapshot BuildWeaponConfigurationSlot(
        string packageId,
        string slotId,
        CombinedWeaponConfigurationAuthoringSummary configuration,
        Dictionary<string, SingleWeaponConfiguration> singles,
        Dictionary<string, CharacterWeaponSlotProfile> characterSlots)
    {
        CombinedWeaponConfigurationSlotAuthoringSummary source = configuration.Slots?.Find(item =>
            item != null && string.Equals(item.SlotId, slotId, StringComparison.Ordinal));
        if (source == null)
            return new CharacterCombinedWeaponConfigurationSlotSnapshot
            {
                SlotId = slotId,
                DisplayName = "未配置"
            };
        singles.TryGetValue(source.SingleWeaponConfigurationId ?? string.Empty,
            out SingleWeaponConfiguration single);
        characterSlots.TryGetValue(slotId, out CharacterWeaponSlotProfile characterSlot);
        bool isUnarmed = GlobalWeaponLibrarySchema.IsUnarmed(source.SingleWeaponConfigurationId);
        return new CharacterCombinedWeaponConfigurationSlotSnapshot
        {
            SlotId = slotId,
            SingleWeaponConfigurationId = source.SingleWeaponConfigurationId,
            SingleWeaponDisplayName = single?.DisplayName ?? source.SingleWeaponConfigurationId,
            AttachmentId = isUnarmed ? string.Empty : "weapon.attachment." +
                CharacterPackageResourceKeyGenerator.NormalizeSegment(
                    GlobalWeaponConfigurationResolver.GetCharacterScopedCombinationIdentity(
                        packageId,
                        configuration.CombinedWeaponConfigurationId)) + "." +
                CharacterPackageResourceKeyGenerator.NormalizeSegment(slotId) + "." +
                CharacterPackageResourceKeyGenerator.NormalizeSegment(source.SingleWeaponConfigurationId),
            AttachSocketId = characterSlot?.AttachSocketId ?? string.Empty,
            WeaponResourceKey = FirstNonEmpty(
                single?.WeaponRef?.RuntimeResourceKey,
                single?.WeaponRef?.PackageResourceKey,
                single?.WeaponRef?.ProviderResourceKey),
            DisplayName = single?.DisplayName ?? source.SingleWeaponConfigurationId,
            IsUnarmed = isUnarmed
        };
    }

    private static Dictionary<string, CharacterWeaponSlotProfile> IndexCharacterWeaponSlots(
        List<CharacterWeaponSlotProfile> slots)
    {
        var result = new Dictionary<string, CharacterWeaponSlotProfile>(StringComparer.Ordinal);
        for (int i = 0; slots != null && i < slots.Count; i++)
        {
            CharacterWeaponSlotProfile slot = slots[i];
            if (slot != null && !string.IsNullOrWhiteSpace(slot.SlotId) && !result.ContainsKey(slot.SlotId))
                result.Add(slot.SlotId, slot);
        }
        return result;
    }

    private static void AddWeaponConfigurationDiagnostics(
        CharacterCombinedWeaponConfigurationSnapshot item,
        CharacterCompilerGateReport gate,
        string combinedWeaponConfigurationId)
    {
        List<CharacterAuthoringValidationIssue> issues = gate?.Issues;
        for (int i = 0; issues != null && i < issues.Count; i++)
        {
            CharacterAuthoringValidationIssue issue = issues[i];
            if (issue == null || (!string.IsNullOrWhiteSpace(combinedWeaponConfigurationId) &&
                !(issue.SourceObjectPath ?? string.Empty).Contains(combinedWeaponConfigurationId, StringComparison.Ordinal)))
                continue;
            item.Diagnostics.Add(new CharacterWeaponConfigurationDiagnostic
            {
                Code = issue.Code,
                Message = issue.Message
            });
        }
    }

    private static Dictionary<int, PatchEntry> IndexWeaponConfigurationEntries(
        List<PatchEntry> entries,
        string source)
    {
        var result = new Dictionary<int, PatchEntry>();
        for (int i = 0; entries != null && i < entries.Count; i++)
        {
            PatchEntry entry = entries[i];
            if (entry != null && string.Equals(entry.Source, source, StringComparison.Ordinal))
                result[ParseAbilityChainInt(entry.Id)] = entry;
        }
        return result;
    }

    private static PatchEntry FindWeaponConfigurationLoadout(
        List<PatchEntry> entries,
        string packageId,
        string combinedWeaponConfigurationId)
    {
        string stableId = "equip_loadout." +
            CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId) + "." +
            CharacterPackageResourceKeyGenerator.NormalizeSegment(
                GlobalWeaponConfigurationResolver.GetCharacterScopedCombinationIdentity(
                    packageId,
                    combinedWeaponConfigurationId));
        return entries.Find(entry => entry != null &&
            string.Equals(entry.Source, CharacterApplicationCompilerTableNames.EquipmentLoadoutConfig, StringComparison.Ordinal) &&
            string.Equals(entry.Fields.GetScalar("StableId"), stableId, StringComparison.Ordinal));
    }

    private static Dictionary<string, SingleWeaponConfiguration> IndexSingleWeaponConfigurations(
        List<SingleWeaponConfiguration> items)
    {
        var result = new Dictionary<string, SingleWeaponConfiguration>(StringComparer.Ordinal);
        for (int i = 0; items != null && i < items.Count; i++)
            if (items[i] != null && !string.IsNullOrWhiteSpace(items[i].SingleWeaponConfigurationId))
                result[items[i].SingleWeaponConfigurationId] = items[i];
        return result;
    }

    private static Dictionary<string, WeaponAttachmentProfile> IndexWeaponAttachments(
        List<WeaponAttachmentProfile> items)
    {
        var result = new Dictionary<string, WeaponAttachmentProfile>(StringComparer.Ordinal);
        for (int i = 0; items != null && i < items.Count; i++)
            if (items[i] != null && !string.IsNullOrWhiteSpace(items[i].AttachmentId))
                result[items[i].AttachmentId] = items[i];
        return result;
    }
}

internal sealed class CharacterCombinedWeaponConfigurationsSnapshot
{
    public string SchemaVersion { get; set; } = "4.0";
    public string PackageRelative { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public string DefaultCombinedWeaponConfigurationId { get; set; } = string.Empty;
    public List<CharacterCombinedWeaponConfigurationSnapshot> CombinedWeaponConfigurations { get; set; } =
        new List<CharacterCombinedWeaponConfigurationSnapshot>();
    public List<CharacterWeaponConfigurationDiagnostic> Diagnostics { get; set; } =
        new List<CharacterWeaponConfigurationDiagnostic>();
}

internal sealed class CharacterCombinedWeaponConfigurationSnapshot
{
    public string CombinedWeaponConfigurationId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int LoadoutId { get; set; }
    public string LoadoutStableId { get; set; } = string.Empty;
    public int EquipmentStateId { get; set; }
    public string EquipmentStateStableId { get; set; } = string.Empty;
    public string AnimationConfigurationId { get; set; } = string.Empty;
    public string AbilityConfigurationId { get; set; } = string.Empty;
    public string ReactionConfigurationId { get; set; } = string.Empty;
    public GlobalAnimationConfiguration AnimationConfiguration { get; set; }
    public GlobalAbilityConfiguration AbilityConfiguration { get; set; }
    public GlobalReactionConfiguration ReactionConfiguration { get; set; }
    public CombinedWeaponActionConfiguration ActionConfiguration { get; set; } =
        new CombinedWeaponActionConfiguration();
    public List<EditorServer.GlobalWeaponLibraryReference> AnimationReferences { get; set; } =
        new List<EditorServer.GlobalWeaponLibraryReference>();
    public List<EditorServer.GlobalWeaponLibraryReference> AbilityReferences { get; set; } =
        new List<EditorServer.GlobalWeaponLibraryReference>();
    public List<EditorServer.GlobalWeaponLibraryReference> ReactionReferences { get; set; } =
        new List<EditorServer.GlobalWeaponLibraryReference>();
    public string GeneratedAnimationProfileId { get; set; } = string.Empty;
    public string GeneratedActionSetId { get; set; } = string.Empty;
    public string GeneratedAbilityLoadoutId { get; set; } = string.Empty;
    public string GenerationStatus { get; set; } = "Unknown";
    public List<CharacterCombinedWeaponConfigurationSlotSnapshot> Slots { get; set; } =
        new List<CharacterCombinedWeaponConfigurationSlotSnapshot>();
    public List<CharacterWeaponConfigurationDiagnostic> Diagnostics { get; set; } =
        new List<CharacterWeaponConfigurationDiagnostic>();
}

internal sealed class CharacterCombinedWeaponConfigurationSlotSnapshot
{
    public string SlotId { get; set; } = string.Empty;
    public bool IsUnarmed { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string SingleWeaponConfigurationId { get; set; } = string.Empty;
    public string SingleWeaponDisplayName { get; set; } = string.Empty;
    public string AttachmentId { get; set; } = string.Empty;
    public string AttachSocketId { get; set; } = string.Empty;
    public string WeaponResourceKey { get; set; } = string.Empty;
}

internal sealed class CharacterWeaponConfigurationDiagnostic
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
