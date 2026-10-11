using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.CharacterApplication;
using MxFramework.Editor.Animation;
using MxFramework.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    public enum CharacterActionReferenceDiscoverySourceKind
    {
        ManualFallback,
        MxAnimationClipRegistry,
        CombatActionSetConfig,
        CharacterPresentationProfileConfig,
        CharacterApplicationConfig,
        ResourceCatalog,
        UnavailableProvider,
    }

    public enum CharacterActionReferenceDiscoveryCategory
    {
        AnimationActionKey,
        CombatActionId,
        PresentationResourceKey,
        AudioCueId,
        CharacterStableId,
        EquipmentStateStableId,
        EquipmentSlotId,
        EquipmentTag,
    }

    public enum CharacterActionReferenceDiscoverySeverity
    {
        Info,
        Warning,
        Error,
    }

    public sealed class CharacterActionReferenceDiscoveryEntry
    {
        public CharacterActionReferenceDiscoveryEntry(
            CharacterActionReferenceDiscoveryCategory category,
            string value,
            CharacterActionReferenceDiscoverySourceKind sourceKind,
            string sourceName)
        {
            Category = category;
            Value = value ?? string.Empty;
            SourceKind = sourceKind;
            SourceName = sourceName ?? string.Empty;
        }

        public CharacterActionReferenceDiscoveryCategory Category { get; }
        public string Value { get; }
        public CharacterActionReferenceDiscoverySourceKind SourceKind { get; }
        public string SourceName { get; }
    }

    public sealed class CharacterActionReferenceDiscoveryDiagnostic
    {
        public CharacterActionReferenceDiscoveryDiagnostic(
            CharacterActionReferenceDiscoverySeverity severity,
            string code,
            string sourceName,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            SourceName = sourceName ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterActionReferenceDiscoverySeverity Severity { get; }
        public string Code { get; }
        public string SourceName { get; }
        public string Message { get; }
    }

    public sealed class CharacterActionReferenceDiscoveryResult
    {
        public CharacterActionReferenceDiscoveryResult(
            CharacterActionAuthoringReferenceContext referenceContext,
            CharacterActionReferenceDiscoveryEntry[] entries,
            CharacterActionReferenceDiscoveryDiagnostic[] diagnostics)
        {
            ReferenceContext = referenceContext ?? CharacterActionAuthoringReferenceContext.CreateDefault();
            Entries = entries ?? Array.Empty<CharacterActionReferenceDiscoveryEntry>();
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionReferenceDiscoveryDiagnostic>();
        }

        public CharacterActionAuthoringReferenceContext ReferenceContext { get; }
        public CharacterActionReferenceDiscoveryEntry[] Entries { get; }
        public CharacterActionReferenceDiscoveryDiagnostic[] Diagnostics { get; }

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Diagnostics.Length; i++)
                {
                    if (Diagnostics[i].Severity == CharacterActionReferenceDiscoverySeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public string SummaryText
        {
            get
            {
                var builder = new StringBuilder();
                builder.Append("Discovered references: ").Append(Entries.Length).AppendLine();
                AppendCount(builder, CharacterActionReferenceDiscoveryCategory.AnimationActionKey);
                AppendCount(builder, CharacterActionReferenceDiscoveryCategory.CombatActionId);
                AppendCount(builder, CharacterActionReferenceDiscoveryCategory.PresentationResourceKey);
                AppendCount(builder, CharacterActionReferenceDiscoveryCategory.AudioCueId);
                AppendCount(builder, CharacterActionReferenceDiscoveryCategory.CharacterStableId);
                AppendCount(builder, CharacterActionReferenceDiscoveryCategory.EquipmentStateStableId);
                AppendCount(builder, CharacterActionReferenceDiscoveryCategory.EquipmentSlotId);
                AppendCount(builder, CharacterActionReferenceDiscoveryCategory.EquipmentTag);

                builder.AppendLine();
                builder.AppendLine("Source diagnostics:");
                if (Diagnostics.Length == 0)
                {
                    builder.AppendLine("- No unavailable providers or discovery diagnostics.");
                }
                else
                {
                    for (int i = 0; i < Diagnostics.Length; i++)
                    {
                        CharacterActionReferenceDiscoveryDiagnostic diagnostic = Diagnostics[i];
                        builder.Append("- ")
                            .Append(diagnostic.Severity)
                            .Append(' ')
                            .Append(diagnostic.Code)
                            .Append(" [")
                            .Append(diagnostic.SourceName)
                            .Append("] ")
                            .Append(diagnostic.Message)
                            .AppendLine();
                    }
                }

                builder.AppendLine();
                builder.AppendLine("Sources:");
                var sourceCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < Entries.Length; i++)
                {
                    string source = Entries[i].SourceName;
                    if (string.IsNullOrWhiteSpace(source))
                        source = Entries[i].SourceKind.ToString();
                    sourceCounts[source] = sourceCounts.TryGetValue(source, out int count) ? count + 1 : 1;
                }

                if (sourceCounts.Count == 0)
                {
                    builder.AppendLine("- none");
                }
                else
                {
                    foreach (KeyValuePair<string, int> pair in sourceCounts)
                        builder.Append("- ").Append(pair.Key).Append(": ").Append(pair.Value).AppendLine();
                }

                return builder.ToString();
            }
        }

        private void AppendCount(StringBuilder builder, CharacterActionReferenceDiscoveryCategory category)
        {
            int count = 0;
            for (int i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].Category == category)
                    count++;
            }

            builder.Append("- ").Append(category).Append(": ").Append(count).AppendLine();
        }
    }

    public sealed class CharacterActionReferenceDiscoveryOptions
    {
        public bool IncludeAssetDatabase { get; set; } = true;
        public string[] SearchFolders { get; set; } = CreateDefaultSearchFolders();
        public MxAnimationClipRegistryAsset[] AnimationRegistryAssets { get; set; } = Array.Empty<MxAnimationClipRegistryAsset>();
        public CombatActionSetConfig[] CombatActionSets { get; set; } = Array.Empty<CombatActionSetConfig>();
        public CharacterPresentationProfileConfig[] PresentationProfiles { get; set; } = Array.Empty<CharacterPresentationProfileConfig>();
        public CharacterConfig[] Characters { get; set; } = Array.Empty<CharacterConfig>();
        public EquipmentSchemaConfig[] EquipmentSchemas { get; set; } = Array.Empty<EquipmentSchemaConfig>();
        public EquipmentStateConfig[] EquipmentStates { get; set; } = Array.Empty<EquipmentStateConfig>();
        public WeaponConfig[] Weapons { get; set; } = Array.Empty<WeaponConfig>();
        public ResourceCatalog[] ResourceCatalogs { get; set; } = Array.Empty<ResourceCatalog>();

        public static string[] CreateDefaultSearchFolders()
        {
            var folders = new List<string>();
            if (MxFrameworkPackageAssetLocator.TryResolveAssetRoot(out string packageRoot))
                folders.Add(packageRoot);
            folders.AddRange(new[]
            {
                "Assets/MxFrameworkGenerated",
                "Assets/Config",
                "Assets/StreamingAssets",
            });
            return folders.ToArray();
        }
    }

    public static class CharacterActionReferenceDiscoveryService
    {
        public const string AnimationRegistryUnavailableCode = "AnimationRegistryUnavailable";
        public const string CharacterApplicationConfigUnavailableCode = "CharacterApplicationConfigUnavailable";
        public const string CharacterApplicationConfigParseFailedCode = "CharacterApplicationConfigParseFailed";
        public const string ResourceCatalogUnavailableCode = "ResourceCatalogUnavailable";
        public const string ResourceCatalogParseFailedCode = "ResourceCatalogParseFailed";

        public static CharacterActionReferenceDiscoveryResult Discover(
            CharacterActionAuthoringModel model,
            CharacterActionReferenceDiscoveryOptions options = null)
        {
            options = options ?? new CharacterActionReferenceDiscoveryOptions();
            var builder = new DiscoveryBuilder(model);

            DiscoverAnimationRegistries(builder, options);
            DiscoverCharacterApplicationConfigs(builder, options);
            DiscoverResourceCatalogs(builder, options);

            return builder.Build();
        }

        private static void DiscoverAnimationRegistries(
            DiscoveryBuilder builder,
            CharacterActionReferenceDiscoveryOptions options)
        {
            List<MxAnimationClipRegistryAsset> assets = CollectAnimationRegistryAssets(options);
            if (assets.Count == 0)
            {
                builder.AddDiagnostic(
                    CharacterActionReferenceDiscoverySeverity.Warning,
                    AnimationRegistryUnavailableCode,
                    "MxAnimation",
                    "No MxAnimationClipRegistryAsset providers were available in the configured search folders.");
                return;
            }

            for (int i = 0; i < assets.Count; i++)
            {
                MxAnimationClipRegistryAsset asset = assets[i];
                if (asset == null)
                    continue;

                string sourceName = AssetSourceName(asset, "MxAnimation registry");
                MxAnimationClipRegistryBindingEntry[] bindings = asset.Bindings;
                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    builder.AddReference(
                        CharacterActionReferenceDiscoveryCategory.AnimationActionKey,
                        bindings[bindingIndex].ResolveActionKey(),
                        CharacterActionReferenceDiscoverySourceKind.MxAnimationClipRegistry,
                        sourceName);
                }
            }
        }

        private static void DiscoverCharacterApplicationConfigs(
            DiscoveryBuilder builder,
            CharacterActionReferenceDiscoveryOptions options)
        {
            int discovered = 0;
            discovered += DiscoverCombatActionSets(builder, options.CombatActionSets);
            discovered += DiscoverPresentationProfiles(builder, options.PresentationProfiles);
            discovered += DiscoverCharacters(builder, options.Characters);
            discovered += DiscoverEquipmentSchemas(builder, options.EquipmentSchemas);
            discovered += DiscoverEquipmentStates(builder, options.EquipmentStates);
            discovered += DiscoverWeapons(builder, options.Weapons);
            discovered += DiscoverCharacterApplicationPatchJson(builder, options);

            if (discovered == 0)
            {
                builder.AddDiagnostic(
                    CharacterActionReferenceDiscoverySeverity.Warning,
                    CharacterApplicationConfigUnavailableCode,
                    "Character.Application",
                    "No Combat/Character.Application config provider data was available; manual reference fallback remains active.");
            }
        }

        private static void DiscoverResourceCatalogs(
            DiscoveryBuilder builder,
            CharacterActionReferenceDiscoveryOptions options)
        {
            List<DiscoveredResourceCatalog> catalogs = CollectResourceCatalogs(builder, options);
            if (catalogs.Count == 0)
            {
                builder.AddDiagnostic(
                    CharacterActionReferenceDiscoverySeverity.Warning,
                    ResourceCatalogUnavailableCode,
                    "Resources",
                    "No ResourceCatalog providers were available in the configured search folders.");
                return;
            }

            for (int i = 0; i < catalogs.Count; i++)
            {
                ResourceCatalog catalog = catalogs[i].Catalog;
                if (catalog == null)
                    continue;

                string sourceName = catalogs[i].SourceName;
                for (int entryIndex = 0; entryIndex < catalog.Entries.Count; entryIndex++)
                {
                    ResourceCatalogEntry entry = catalog.Entries[entryIndex];
                    if (entry == null)
                        continue;

                    ResourceKey key = entry.CreateKey(catalog.PackageId);
                    string reference = string.IsNullOrWhiteSpace(key.Id) ? key.ToString() : key.Id;
                    if (IsAudioResource(entry.TypeId))
                    {
                        builder.AddReference(
                            CharacterActionReferenceDiscoveryCategory.AudioCueId,
                            reference,
                            CharacterActionReferenceDiscoverySourceKind.ResourceCatalog,
                            sourceName);
                    }
                    else if (IsPresentationResource(entry.TypeId))
                    {
                        builder.AddReference(
                            CharacterActionReferenceDiscoveryCategory.PresentationResourceKey,
                            reference,
                            CharacterActionReferenceDiscoverySourceKind.ResourceCatalog,
                            sourceName);
                    }
                }
            }
        }

        private static int DiscoverCombatActionSets(DiscoveryBuilder builder, CombatActionSetConfig[] actionSets)
        {
            actionSets = actionSets ?? Array.Empty<CombatActionSetConfig>();
            int discovered = 0;
            for (int i = 0; i < actionSets.Length; i++)
            {
                CombatActionSetConfig actionSet = actionSets[i];
                if (actionSet == null)
                    continue;

                discovered++;
                string sourceName = string.IsNullOrWhiteSpace(actionSet.StableId)
                    ? "CombatActionSetConfig " + actionSet.Id
                    : "CombatActionSetConfig " + actionSet.StableId;
                CombatActionEntry[] actions = actionSet.Actions;
                for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
                {
                    CombatActionEntry action = actions[actionIndex];
                    builder.AddReference(
                        CharacterActionReferenceDiscoveryCategory.CombatActionId,
                        action.CombatActionId.ToString(),
                        CharacterActionReferenceDiscoverySourceKind.CombatActionSetConfig,
                        sourceName);
                    builder.AddReference(
                        CharacterActionReferenceDiscoveryCategory.AnimationActionKey,
                        action.AnimationActionKey,
                        CharacterActionReferenceDiscoverySourceKind.CombatActionSetConfig,
                        sourceName);
                }
            }

            return discovered;
        }

        private static int DiscoverPresentationProfiles(DiscoveryBuilder builder, CharacterPresentationProfileConfig[] profiles)
        {
            profiles = profiles ?? Array.Empty<CharacterPresentationProfileConfig>();
            int discovered = 0;
            for (int i = 0; i < profiles.Length; i++)
            {
                CharacterPresentationProfileConfig profile = profiles[i];
                if (profile == null)
                    continue;

                discovered++;
                string sourceName = string.IsNullOrWhiteSpace(profile.StableId)
                    ? "CharacterPresentationProfileConfig " + profile.Id
                    : "CharacterPresentationProfileConfig " + profile.StableId;
                CharacterResourceKeyEntry[] resources = profile.ResourceKeys;
                for (int resourceIndex = 0; resourceIndex < resources.Length; resourceIndex++)
                    AddCharacterResource(builder, resources[resourceIndex], sourceName);
            }

            return discovered;
        }

        private static int DiscoverCharacters(DiscoveryBuilder builder, CharacterConfig[] characters)
        {
            characters = characters ?? Array.Empty<CharacterConfig>();
            int discovered = 0;
            for (int i = 0; i < characters.Length; i++)
            {
                CharacterConfig character = characters[i];
                if (character == null)
                    continue;

                discovered++;
                builder.AddReference(
                    CharacterActionReferenceDiscoveryCategory.CharacterStableId,
                    character.StableId,
                    CharacterActionReferenceDiscoverySourceKind.CharacterApplicationConfig,
                    "CharacterConfig " + character.Id);
            }

            return discovered;
        }

        private static int DiscoverEquipmentSchemas(DiscoveryBuilder builder, EquipmentSchemaConfig[] schemas)
        {
            schemas = schemas ?? Array.Empty<EquipmentSchemaConfig>();
            int discovered = 0;
            for (int i = 0; i < schemas.Length; i++)
            {
                EquipmentSchemaConfig schema = schemas[i];
                if (schema == null)
                    continue;

                discovered++;
                string sourceName = string.IsNullOrWhiteSpace(schema.StableId)
                    ? "EquipmentSchemaConfig " + schema.Id
                    : "EquipmentSchemaConfig " + schema.StableId;
                EquipmentSlotEntry[] slots = schema.Slots;
                for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                {
                    builder.AddReference(
                        CharacterActionReferenceDiscoveryCategory.EquipmentSlotId,
                        slots[slotIndex].SlotId,
                        CharacterActionReferenceDiscoverySourceKind.CharacterApplicationConfig,
                        sourceName);
                    AddMany(builder, CharacterActionReferenceDiscoveryCategory.EquipmentTag, slots[slotIndex].RequiredTags, sourceName);
                }
            }

            return discovered;
        }

        private static int DiscoverEquipmentStates(DiscoveryBuilder builder, EquipmentStateConfig[] states)
        {
            states = states ?? Array.Empty<EquipmentStateConfig>();
            int discovered = 0;
            for (int i = 0; i < states.Length; i++)
            {
                EquipmentStateConfig state = states[i];
                if (state == null)
                    continue;

                discovered++;
                string sourceName = string.IsNullOrWhiteSpace(state.StableId)
                    ? "EquipmentStateConfig " + state.Id
                    : "EquipmentStateConfig " + state.StableId;
                builder.AddReference(
                    CharacterActionReferenceDiscoveryCategory.EquipmentStateStableId,
                    state.StableId,
                    CharacterActionReferenceDiscoverySourceKind.CharacterApplicationConfig,
                    sourceName);
                AddMany(builder, CharacterActionReferenceDiscoveryCategory.EquipmentTag, state.Tags, sourceName);
            }

            return discovered;
        }

        private static int DiscoverWeapons(DiscoveryBuilder builder, WeaponConfig[] weapons)
        {
            weapons = weapons ?? Array.Empty<WeaponConfig>();
            int discovered = 0;
            for (int i = 0; i < weapons.Length; i++)
            {
                WeaponConfig weapon = weapons[i];
                if (weapon == null)
                    continue;

                discovered++;
                string sourceName = string.IsNullOrWhiteSpace(weapon.StableId)
                    ? "WeaponConfig " + weapon.Id
                    : "WeaponConfig " + weapon.StableId;
                AddMany(builder, CharacterActionReferenceDiscoveryCategory.EquipmentTag, weapon.Tags, sourceName);
                CharacterResourceKeyEntry[] resources = weapon.ResourceKeys;
                for (int resourceIndex = 0; resourceIndex < resources.Length; resourceIndex++)
                    AddCharacterResource(builder, resources[resourceIndex], sourceName);
            }

            return discovered;
        }

        private static int DiscoverCharacterApplicationPatchJson(
            DiscoveryBuilder builder,
            CharacterActionReferenceDiscoveryOptions options)
        {
            if (!options.IncludeAssetDatabase)
                return 0;

            string[] folders = ExistingSearchFolders(options.SearchFolders);
            if (folders.Length == 0)
                return 0;

            int discovered = 0;
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", folders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!LooksLikeCharacterApplicationPatchPath(path))
                    continue;

                TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                discovered += DiscoverCharacterApplicationPatchJson(builder, textAsset != null ? textAsset.text : string.Empty, path);
            }

            return discovered;
        }

        private static int DiscoverCharacterApplicationPatchJson(
            DiscoveryBuilder builder,
            string json,
            string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(json)
                || json.IndexOf("\"entries\"", StringComparison.OrdinalIgnoreCase) < 0
                || json.IndexOf("\"fields\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return 0;
            }

            try
            {
                JObject root = JObject.Parse(json);
                JArray entries = root["patch"]?["entries"] as JArray ?? root["entries"] as JArray;
                if (entries == null)
                    return 0;

                int discovered = 0;
                for (int i = 0; i < entries.Count; i++)
                {
                    JObject entry = entries[i] as JObject;
                    JObject fields = entry?["fields"] as JObject;
                    string tableName = entry?.Value<string>("source") ?? entry?.Value<string>("tableName") ?? string.Empty;
                    if (fields == null || string.IsNullOrWhiteSpace(tableName))
                        continue;

                    string sourceName = sourcePath + ":" + tableName + "/" + (entry.Value<string>("id") ?? string.Empty);
                    discovered += DiscoverCharacterApplicationPatchEntry(builder, tableName, fields, sourceName);
                }

                return discovered;
            }
            catch (Exception ex)
            {
                builder.AddDiagnostic(
                    CharacterActionReferenceDiscoverySeverity.Warning,
                    CharacterApplicationConfigParseFailedCode,
                    sourcePath,
                    ex.Message);
                return 0;
            }
        }

        private static int DiscoverCharacterApplicationPatchEntry(
            DiscoveryBuilder builder,
            string tableName,
            JObject fields,
            string sourceName)
        {
            if (string.Equals(tableName, CharacterConfig.TableName, StringComparison.Ordinal))
            {
                builder.AddReference(
                    CharacterActionReferenceDiscoveryCategory.CharacterStableId,
                    fields.Value<string>("StableId"),
                    CharacterActionReferenceDiscoverySourceKind.CharacterApplicationConfig,
                    sourceName);
                return 1;
            }

            if (string.Equals(tableName, EquipmentStateConfig.TableName, StringComparison.Ordinal))
            {
                builder.AddReference(
                    CharacterActionReferenceDiscoveryCategory.EquipmentStateStableId,
                    fields.Value<string>("StableId"),
                    CharacterActionReferenceDiscoverySourceKind.CharacterApplicationConfig,
                    sourceName);
                AddJsonStringArray(builder, CharacterActionReferenceDiscoveryCategory.EquipmentTag, fields["Tags"], sourceName);
                return 1;
            }

            if (string.Equals(tableName, EquipmentSchemaConfig.TableName, StringComparison.Ordinal))
            {
                JArray slots = fields["Slots"] as JArray;
                if (slots != null)
                {
                    for (int i = 0; i < slots.Count; i++)
                    {
                        builder.AddReference(
                            CharacterActionReferenceDiscoveryCategory.EquipmentSlotId,
                            slots[i]?.Value<string>("SlotId"),
                            CharacterActionReferenceDiscoverySourceKind.CharacterApplicationConfig,
                            sourceName);
                        AddJsonStringArray(builder, CharacterActionReferenceDiscoveryCategory.EquipmentTag, slots[i]?["RequiredTags"], sourceName);
                    }
                }

                return 1;
            }

            if (string.Equals(tableName, WeaponConfig.TableName, StringComparison.Ordinal))
            {
                AddJsonStringArray(builder, CharacterActionReferenceDiscoveryCategory.EquipmentTag, fields["Tags"], sourceName);
                AddJsonResourceKeys(builder, fields["ResourceKeys"] as JArray, sourceName);
                return 1;
            }

            if (string.Equals(tableName, CombatActionSetConfig.TableName, StringComparison.Ordinal))
            {
                JArray actions = fields["Actions"] as JArray;
                if (actions != null)
                {
                    for (int i = 0; i < actions.Count; i++)
                    {
                        JObject action = actions[i] as JObject;
                        if (action == null)
                            continue;

                        builder.AddReference(
                            CharacterActionReferenceDiscoveryCategory.CombatActionId,
                            action.Value<string>("CombatActionId"),
                            CharacterActionReferenceDiscoverySourceKind.CombatActionSetConfig,
                            sourceName);
                        builder.AddReference(
                            CharacterActionReferenceDiscoveryCategory.AnimationActionKey,
                            action.Value<string>("AnimationActionKey"),
                            CharacterActionReferenceDiscoverySourceKind.CombatActionSetConfig,
                            sourceName);
                    }
                }

                return 1;
            }

            if (string.Equals(tableName, CharacterPresentationProfileConfig.TableName, StringComparison.Ordinal))
            {
                AddJsonResourceKeys(builder, fields["ResourceKeys"] as JArray, sourceName);
                return 1;
            }

            return 0;
        }

        private static void AddJsonResourceKeys(DiscoveryBuilder builder, JArray resources, string sourceName)
        {
            if (resources == null)
                return;

            for (int i = 0; i < resources.Count; i++)
            {
                JObject resource = resources[i] as JObject;
                if (resource == null)
                    continue;

                string id = resource.Value<string>("Id");
                string typeId = resource.Value<string>("TypeId");
                string usageKind = resource.Value<string>("UsageKind");
                CharacterActionReferenceDiscoveryCategory category =
                    string.Equals(usageKind, CharacterResourceUsageKind.Sfx.ToString(), StringComparison.Ordinal)
                    || IsAudioResource(typeId)
                        ? CharacterActionReferenceDiscoveryCategory.AudioCueId
                        : CharacterActionReferenceDiscoveryCategory.PresentationResourceKey;
                builder.AddReference(
                    category,
                    id,
                    CharacterActionReferenceDiscoverySourceKind.CharacterPresentationProfileConfig,
                    sourceName);
            }
        }

        private static void AddJsonStringArray(
            DiscoveryBuilder builder,
            CharacterActionReferenceDiscoveryCategory category,
            JToken token,
            string sourceName)
        {
            JArray values = token as JArray;
            if (values == null)
                return;

            for (int i = 0; i < values.Count; i++)
            {
                builder.AddReference(
                    category,
                    values[i]?.ToString(),
                    CharacterActionReferenceDiscoverySourceKind.CharacterApplicationConfig,
                    sourceName);
            }
        }

        private static void AddCharacterResource(
            DiscoveryBuilder builder,
            CharacterResourceKeyEntry resource,
            string sourceName)
        {
            if (resource.UsageKind == CharacterResourceUsageKind.Sfx || IsAudioResource(resource.TypeId))
            {
                builder.AddReference(
                    CharacterActionReferenceDiscoveryCategory.AudioCueId,
                    resource.Id,
                    CharacterActionReferenceDiscoverySourceKind.CharacterPresentationProfileConfig,
                    sourceName);
                return;
            }

            builder.AddReference(
                CharacterActionReferenceDiscoveryCategory.PresentationResourceKey,
                resource.Id,
                CharacterActionReferenceDiscoverySourceKind.CharacterPresentationProfileConfig,
                sourceName);
        }

        private static void AddMany(
            DiscoveryBuilder builder,
            CharacterActionReferenceDiscoveryCategory category,
            string[] values,
            string sourceName)
        {
            values = values ?? Array.Empty<string>();
            for (int i = 0; i < values.Length; i++)
            {
                builder.AddReference(
                    category,
                    values[i],
                    CharacterActionReferenceDiscoverySourceKind.CharacterApplicationConfig,
                    sourceName);
            }
        }

        private static List<MxAnimationClipRegistryAsset> CollectAnimationRegistryAssets(CharacterActionReferenceDiscoveryOptions options)
        {
            var assets = new List<MxAnimationClipRegistryAsset>();
            AddUniqueAssets(assets, options.AnimationRegistryAssets);

            if (!options.IncludeAssetDatabase)
                return assets;

            string[] folders = ExistingSearchFolders(options.SearchFolders);
            if (folders.Length == 0)
                return assets;

            string[] guids = AssetDatabase.FindAssets("t:MxAnimationClipRegistryAsset", folders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                MxAnimationClipRegistryAsset asset = AssetDatabase.LoadAssetAtPath<MxAnimationClipRegistryAsset>(path);
                if (asset != null && !assets.Contains(asset))
                    assets.Add(asset);
            }

            return assets;
        }

        private static List<DiscoveredResourceCatalog> CollectResourceCatalogs(
            DiscoveryBuilder builder,
            CharacterActionReferenceDiscoveryOptions options)
        {
            var catalogs = new List<DiscoveredResourceCatalog>();
            ResourceCatalog[] providedCatalogs = options.ResourceCatalogs ?? Array.Empty<ResourceCatalog>();
            for (int i = 0; i < providedCatalogs.Length; i++)
            {
                ResourceCatalog catalog = providedCatalogs[i];
                if (catalog != null)
                    catalogs.Add(new DiscoveredResourceCatalog(catalog, "ResourceCatalog " + catalog.CatalogId));
            }

            if (!options.IncludeAssetDatabase)
                return catalogs;

            string[] folders = ExistingSearchFolders(options.SearchFolders);
            if (folders.Length == 0)
                return catalogs;

            string[] guids = AssetDatabase.FindAssets("t:TextAsset", folders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!LooksLikeResourceCatalogPath(path))
                    continue;

                TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                ResourceCatalog catalog = TryParseResourceCatalog(textAsset != null ? textAsset.text : string.Empty, path, builder);
                if (catalog != null)
                    catalogs.Add(new DiscoveredResourceCatalog(catalog, path));
            }

            return catalogs;
        }

        private static ResourceCatalog TryParseResourceCatalog(string json, string sourcePath, DiscoveryBuilder builder)
        {
            if (string.IsNullOrWhiteSpace(json)
                || json.IndexOf("\"entries\"", StringComparison.OrdinalIgnoreCase) < 0
                || json.IndexOf("\"catalogId\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return null;
            }

            try
            {
                ResourceCatalogJsonDto dto = JsonConvert.DeserializeObject<ResourceCatalogJsonDto>(json);
                if (dto?.entries == null)
                    return null;

                var entries = new List<ResourceCatalogEntry>(dto.entries.Length);
                for (int i = 0; i < dto.entries.Length; i++)
                {
                    ResourceCatalogEntryJsonDto entry = dto.entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                        continue;

                    entries.Add(new ResourceCatalogEntry(
                        entry.id,
                        entry.ResolveTypeId(),
                        entry.provider,
                        entry.address,
                        entry.variant,
                        entry.packageId,
                        ConvertDependencies(entry.dependencies),
                        entry.labels,
                        entry.hash,
                        entry.size,
                        entry.allowOverride,
                        entry.providerData));
                }

                return new ResourceCatalog(dto.catalogId, dto.packageId, entries);
            }
            catch (Exception ex)
            {
                builder.AddDiagnostic(
                    CharacterActionReferenceDiscoverySeverity.Warning,
                    ResourceCatalogParseFailedCode,
                    sourcePath,
                    ex.Message);
                return null;
            }
        }

        private static ResourceKey[] ConvertDependencies(ResourceKeyJsonDto[] dependencies)
        {
            dependencies = dependencies ?? Array.Empty<ResourceKeyJsonDto>();
            var keys = new List<ResourceKey>(dependencies.Length);
            for (int i = 0; i < dependencies.Length; i++)
            {
                ResourceKeyJsonDto dependency = dependencies[i];
                if (dependency == null)
                    continue;

                keys.Add(new ResourceKey(
                    dependency.id,
                    dependency.ResolveTypeId(),
                    dependency.variant,
                    dependency.packageId));
            }

            return keys.ToArray();
        }

        private static bool LooksLikeResourceCatalogPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return false;

            string fileName = Path.GetFileName(path);
            return fileName.IndexOf("catalog", StringComparison.OrdinalIgnoreCase) >= 0
                || fileName.IndexOf("resources", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeCharacterApplicationPatchPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return false;

            string fileName = Path.GetFileName(path);
            return fileName.IndexOf("character_config_patch", StringComparison.OrdinalIgnoreCase) >= 0
                || fileName.IndexOf("character_application_config_patch", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAudioResource(string typeId)
        {
            return string.Equals(typeId, ResourceTypeIds.AudioClip, StringComparison.Ordinal);
        }

        private static bool IsPresentationResource(string typeId)
        {
            return string.Equals(typeId, ResourceTypeIds.GameObject, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.Object, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.Sprite, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.Texture2D, StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.Material, StringComparison.Ordinal);
        }

        private static void AddUniqueAssets<TAsset>(List<TAsset> assets, TAsset[] candidates)
            where TAsset : UnityEngine.Object
        {
            candidates = candidates ?? Array.Empty<TAsset>();
            for (int i = 0; i < candidates.Length; i++)
            {
                TAsset candidate = candidates[i];
                if (candidate != null && !assets.Contains(candidate))
                    assets.Add(candidate);
            }
        }

        private static string AssetSourceName(UnityEngine.Object asset, string fallback)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrWhiteSpace(path))
                return path;

            return string.IsNullOrWhiteSpace(asset.name) ? fallback : fallback + " " + asset.name;
        }

        private static string[] ExistingSearchFolders(string[] folders)
        {
            folders = folders ?? Array.Empty<string>();
            var existing = new List<string>();
            for (int i = 0; i < folders.Length; i++)
            {
                string folder = folders[i];
                if (!string.IsNullOrWhiteSpace(folder) && AssetDatabase.IsValidFolder(folder))
                    existing.Add(folder);
            }

            return existing.ToArray();
        }

        private readonly struct DiscoveredResourceCatalog
        {
            public DiscoveredResourceCatalog(ResourceCatalog catalog, string sourceName)
            {
                Catalog = catalog;
                SourceName = sourceName ?? string.Empty;
            }

            public ResourceCatalog Catalog { get; }
            public string SourceName { get; }
        }

        private sealed class DiscoveryBuilder
        {
            private readonly CharacterActionAuthoringReferenceContext _referenceContext;
            private readonly List<CharacterActionReferenceDiscoveryEntry> _entries = new List<CharacterActionReferenceDiscoveryEntry>();
            private readonly List<CharacterActionReferenceDiscoveryDiagnostic> _diagnostics = new List<CharacterActionReferenceDiscoveryDiagnostic>();
            private readonly HashSet<string> _entryKeys = new HashSet<string>(StringComparer.Ordinal);

            public DiscoveryBuilder(CharacterActionAuthoringModel model)
            {
                _referenceContext = CharacterActionAuthoringReferenceContext.CreateDefault();
                if (model != null)
                {
                    _referenceContext.MergeFromModel(model);
                    _referenceContext.MergeFromReferenceContext(model.ReferenceContext);
                }

                AddSnapshotEntries(CharacterActionReferenceDiscoverySourceKind.ManualFallback, "Manual fallback");
            }

            public void AddReference(CharacterActionReferenceDiscoveryCategory category, int id,
                CharacterActionReferenceDiscoverySourceKind sourceKind, string sourceName)
            {
                if (id <= 0) return;
                switch (category)
                {
                    case CharacterActionReferenceDiscoveryCategory.CombatActionId: _referenceContext.AddCombatActionId(id); break;
                    case CharacterActionReferenceDiscoveryCategory.AudioCueId: _referenceContext.AddAudioCueId(id); break;
                    case CharacterActionReferenceDiscoveryCategory.EquipmentSlotId: _referenceContext.AddEquipmentSlotId(id); break;
                    default: throw new ArgumentOutOfRangeException(nameof(category));
                }
                string value = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string key = category + "|" + value + "|" + sourceKind + "|" + sourceName;
                if (_entryKeys.Add(key)) _entries.Add(new CharacterActionReferenceDiscoveryEntry(category, value, sourceKind, sourceName));
            }

            public void AddReference(
                CharacterActionReferenceDiscoveryCategory category,
                string value,
                CharacterActionReferenceDiscoverySourceKind sourceKind,
                string sourceName)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;

                value = value.Trim();
                AddToContext(category, value);
                string key = category + "|" + value + "|" + sourceKind + "|" + sourceName;
                if (!_entryKeys.Add(key))
                    return;

                _entries.Add(new CharacterActionReferenceDiscoveryEntry(category, value, sourceKind, sourceName));
            }

            public void AddDiagnostic(
                CharacterActionReferenceDiscoverySeverity severity,
                string code,
                string sourceName,
                string message)
            {
                _diagnostics.Add(new CharacterActionReferenceDiscoveryDiagnostic(severity, code, sourceName, message));
            }

            public CharacterActionReferenceDiscoveryResult Build()
            {
                _referenceContext.Normalize();
                return new CharacterActionReferenceDiscoveryResult(
                    _referenceContext,
                    _entries.ToArray(),
                    _diagnostics.ToArray());
            }

            private void AddSnapshotEntries(CharacterActionReferenceDiscoverySourceKind sourceKind, string sourceName)
            {
                AddSnapshotEntries(CharacterActionReferenceDiscoveryCategory.AnimationActionKey, _referenceContext.AnimationActionKeys, sourceKind, sourceName);
                AddSnapshotEntries(CharacterActionReferenceDiscoveryCategory.CombatActionId, _referenceContext.CombatActionIds, sourceKind, sourceName);
                AddSnapshotEntries(CharacterActionReferenceDiscoveryCategory.PresentationResourceKey, _referenceContext.PresentationResourceKeys, sourceKind, sourceName);
                AddSnapshotEntries(CharacterActionReferenceDiscoveryCategory.AudioCueId, _referenceContext.AudioCueIds, sourceKind, sourceName);
                AddSnapshotEntries(CharacterActionReferenceDiscoveryCategory.CharacterStableId, _referenceContext.CharacterStableIds, sourceKind, sourceName);
                AddSnapshotEntries(CharacterActionReferenceDiscoveryCategory.EquipmentStateStableId, _referenceContext.EquipmentStateStableIds, sourceKind, sourceName);
                AddSnapshotEntries(CharacterActionReferenceDiscoveryCategory.EquipmentSlotId, _referenceContext.EquipmentSlotIds, sourceKind, sourceName);
                AddSnapshotEntries(CharacterActionReferenceDiscoveryCategory.EquipmentTag, _referenceContext.EquipmentTags, sourceKind, sourceName);
            }

            private void AddSnapshotEntries(CharacterActionReferenceDiscoveryCategory category,
                List<int> values, CharacterActionReferenceDiscoverySourceKind sourceKind, string sourceName)
            {
                if (values == null) return;
                foreach (int value in values) AddReference(category, value, sourceKind, sourceName);
            }

            private void AddSnapshotEntries(
                CharacterActionReferenceDiscoveryCategory category,
                List<string> values,
                CharacterActionReferenceDiscoverySourceKind sourceKind,
                string sourceName)
            {
                values = values ?? new List<string>();
                for (int i = 0; i < values.Count; i++)
                    AddReference(category, values[i], sourceKind, sourceName);
            }

            private void AddToContext(CharacterActionReferenceDiscoveryCategory category, string value)
            {
                switch (category)
                {
                    case CharacterActionReferenceDiscoveryCategory.AnimationActionKey:
                        _referenceContext.AddAnimationActionKey(value);
                        break;
                    case CharacterActionReferenceDiscoveryCategory.CombatActionId:
                        _referenceContext.AddCombatActionId(int.Parse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    case CharacterActionReferenceDiscoveryCategory.PresentationResourceKey:
                        _referenceContext.AddPresentationResourceKey(value);
                        break;
                    case CharacterActionReferenceDiscoveryCategory.AudioCueId:
                        _referenceContext.AddAudioCueId(CharacterContentIdentityAuthoringCatalog.GetExisting(401, value));
                        break;
                    case CharacterActionReferenceDiscoveryCategory.CharacterStableId:
                        _referenceContext.AddCharacterStableId(value);
                        break;
                    case CharacterActionReferenceDiscoveryCategory.EquipmentStateStableId:
                        _referenceContext.AddEquipmentStateStableId(value);
                        break;
                    case CharacterActionReferenceDiscoveryCategory.EquipmentSlotId:
                        _referenceContext.AddEquipmentSlotId(CharacterContentIdentityAuthoringCatalog.GetExisting(405, value));
                        break;
                    case CharacterActionReferenceDiscoveryCategory.EquipmentTag:
                        _referenceContext.AddEquipmentTag(value);
                        break;
                }
            }
        }

        private sealed class ResourceCatalogJsonDto
        {
            public string catalogId;
            public string packageId;
            public ResourceCatalogEntryJsonDto[] entries;
        }

        private sealed class ResourceCatalogEntryJsonDto
        {
            public string id;
            public string type;
            public string typeId;
            public string variant;
            public string packageId;
            public string provider;
            public string address;
            public string[] labels;
            public ResourceKeyJsonDto[] dependencies;
            public string hash;
            public long size;
            public bool allowOverride;
            public Dictionary<string, string> providerData;

            public string ResolveTypeId()
            {
                return string.IsNullOrWhiteSpace(typeId) ? type : typeId;
            }
        }

        private sealed class ResourceKeyJsonDto
        {
            public string id;
            public string type;
            public string typeId;
            public string variant;
            public string packageId;

            public string ResolveTypeId()
            {
                return string.IsNullOrWhiteSpace(typeId) ? type : typeId;
            }
        }
    }
}
