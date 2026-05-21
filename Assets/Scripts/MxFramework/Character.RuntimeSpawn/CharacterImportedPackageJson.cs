using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.Config;
using MxFramework.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterRuntimeSpawn
{
    public static class CharacterImportedPackageJson
    {
        public static CharacterImportedPackage LoadFromDirectory(string importedPackageRoot)
        {
            if (string.IsNullOrWhiteSpace(importedPackageRoot))
                throw new CharacterImportedPackageJsonException("Imported package root path is missing.");
            if (!Directory.Exists(importedPackageRoot))
                throw new CharacterImportedPackageJsonException("Imported package root was not found: " + importedPackageRoot + ".");

            string configRoot = Path.Combine(importedPackageRoot, "config");
            string cacheRoot = Path.Combine(importedPackageRoot, "package_cache");
            CharacterImportedConfigSet configs = LoadConfigSetFromFile(Path.Combine(configRoot, "character_config_patch.json"));
            CharacterImportedGeometryBinding geometry = LoadGeometryBindingFromFile(Path.Combine(configRoot, "geometry_binding.json"));
            CharacterImportedResourceMapping mapping = LoadResourceMappingFromFile(Path.Combine(configRoot, "resource_catalog_mapping.json"));
            ResourceCatalog catalog = LoadResourceCatalogFromFile(Path.Combine(configRoot, "unity_resource_catalog.json"));
            ResourceCatalog runtimeCatalog = LoadOptionalResourceCatalogFromFile(Path.Combine(configRoot, "runtime_resource_catalog.json"));
            CharacterResourcePlan runtimePlan = LoadOptionalCharacterResourcePlanFromFile(Path.Combine(configRoot, "character_resource_plan.json"));
            CharacterAudioCueManifest audioCueManifest = LoadOptionalAudioCueManifestFromFile(Path.Combine(configRoot, "audio_cue_manifest.json"));
            CharacterCompiledAnimationArtifacts compiledAnimationArtifacts = LoadCompiledAnimationArtifacts(configRoot, runtimeCatalog);
            CharacterUnityImportRuntimeReport report = LoadImportReportFromFile(Path.Combine(cacheRoot, "import_report.json"));

            string packageId = !string.IsNullOrEmpty(report.PackageId)
                ? report.PackageId
                : !string.IsNullOrEmpty(geometry.PackageId) ? geometry.PackageId : mapping.PackageId;

            return new CharacterImportedPackage(importedPackageRoot, packageId, configs, geometry, mapping, catalog, report, runtimeCatalog, runtimePlan, audioCueManifest, compiledAnimationArtifacts);
        }

        public static CharacterImportedConfigSet LoadConfigSetFromFile(string path)
        {
            return LoadConfigSet(ReadRequiredFile(path, "character config patch"));
        }

        public static CharacterImportedGeometryBinding LoadGeometryBindingFromFile(string path)
        {
            return LoadGeometryBinding(ReadRequiredFile(path, "geometry binding"));
        }

        public static CharacterImportedResourceMapping LoadResourceMappingFromFile(string path)
        {
            return LoadResourceMapping(ReadRequiredFile(path, "resource mapping"));
        }

        public static ResourceCatalog LoadResourceCatalogFromFile(string path)
        {
            return LoadResourceCatalog(ReadRequiredFile(path, "Unity resource catalog"));
        }

        public static CharacterResourcePlan LoadCharacterResourcePlanFromFile(string path)
        {
            return LoadCharacterResourcePlan(ReadRequiredFile(path, "character resource plan"));
        }

        public static CharacterAudioCueManifest LoadAudioCueManifestFromFile(string path)
        {
            return LoadAudioCueManifest(ReadRequiredFile(path, "audio cue manifest"));
        }

        public static CharacterAnimationSetDefinition LoadAnimationSetDefinitionFromFile(string path)
        {
            return LoadAnimationSetDefinition(ReadRequiredFile(path, "animation set definition"));
        }

        public static CharacterAnimationClipRegistry LoadAnimationClipRegistryFromFile(string path)
        {
            return LoadAnimationClipRegistry(ReadRequiredFile(path, "animation clip registry"));
        }

        public static CharacterAnimationResourcePlan LoadAnimationResourcePlanFromFile(string path)
        {
            return LoadAnimationResourcePlan(ReadRequiredFile(path, "animation resource plan"));
        }

        public static CharacterUnityImportRuntimeReport LoadImportReportFromFile(string path)
        {
            return LoadImportReport(ReadRequiredFile(path, "import report"));
        }

        public static CharacterImportedConfigSet LoadConfigSet(string json)
        {
            JObject root = ParseRoot(json, "character config patch");
            if (!string.Equals(ReadString(root, "format"), "mx.characterApplicationConfigPatchBundle.v1", StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException("Unsupported character config patch format.");

            JArray entries = root["patch"]?["entries"] as JArray;
            if (entries == null)
                throw new CharacterImportedPackageJsonException("Character config patch is missing patch.entries.");

            var characters = new List<CharacterConfig>();
            var attributes = new List<CharacterAttributeProfileConfig>();
            var bodyProfiles = new List<CharacterBodyProfileConfig>();
            var bodyParts = new List<CharacterBodyPartConfig>();
            var equipmentSchemas = new List<EquipmentSchemaConfig>();
            var loadouts = new List<EquipmentLoadoutConfig>();
            var states = new List<EquipmentStateConfig>();
            var weapons = new List<WeaponConfig>();
            var abilityLoadouts = new List<AbilityLoadoutConfig>();
            var actionSets = new List<CombatActionSetConfig>();
            var presentations = new List<CharacterPresentationProfileConfig>();
            var spawnProfiles = new List<SpawnProfileConfig>();

            for (int i = 0; i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                JObject fields = entry?["fields"] as JObject;
                if (entry == null || fields == null)
                    continue;

                string source = ReadString(entry, "source");
                switch (source)
                {
                    case CharacterConfig.TableName:
                        characters.Add(ParseCharacter(fields));
                        break;
                    case CharacterAttributeProfileConfig.TableName:
                        attributes.Add(ParseAttributeProfile(fields));
                        break;
                    case CharacterBodyProfileConfig.TableName:
                        bodyProfiles.Add(ParseBodyProfile(fields));
                        break;
                    case CharacterBodyPartConfig.TableName:
                        bodyParts.Add(ParseBodyPart(fields));
                        break;
                    case EquipmentSchemaConfig.TableName:
                        equipmentSchemas.Add(ParseEquipmentSchema(fields));
                        break;
                    case EquipmentLoadoutConfig.TableName:
                        loadouts.Add(ParseLoadout(fields));
                        break;
                    case EquipmentStateConfig.TableName:
                        states.Add(ParseEquipmentState(fields));
                        break;
                    case WeaponConfig.TableName:
                        weapons.Add(ParseWeapon(fields));
                        break;
                    case AbilityLoadoutConfig.TableName:
                        abilityLoadouts.Add(ParseAbilityLoadout(fields));
                        break;
                    case CombatActionSetConfig.TableName:
                        actionSets.Add(ParseCombatActionSet(fields));
                        break;
                    case CharacterPresentationProfileConfig.TableName:
                        presentations.Add(ParsePresentation(fields));
                        break;
                    case SpawnProfileConfig.TableName:
                        spawnProfiles.Add(ParseSpawnProfile(fields));
                        break;
                }
            }

            return new CharacterImportedConfigSet(
                characters.ToArray(),
                attributes.ToArray(),
                bodyProfiles.ToArray(),
                bodyParts.ToArray(),
                equipmentSchemas.ToArray(),
                loadouts.ToArray(),
                states.ToArray(),
                weapons.ToArray(),
                abilityLoadouts.ToArray(),
                actionSets.ToArray(),
                presentations.ToArray(),
                spawnProfiles.ToArray());
        }

        public static CharacterImportedGeometryBinding LoadGeometryBinding(string json)
        {
            JObject root = ParseRoot(json, "geometry binding");
            if (!string.Equals(ReadString(root, "format"), "mx.characterGeometryBinding.v1", StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException("Unsupported character geometry binding format.");

            JObject body = root["bodyProfile"] as JObject;
            JObject capsule = body?["defaultCapsule"] as JObject;
            var bodyProfile = body == null
                ? null
                : new CharacterBodyGeometryRuntimeProfile(
                    ReadString(body, "profileId"),
                    ReadString(body, "bodyKind"),
                    ReadFloat(body, "bodyScale", 1f),
                    ReadFloat(body, "heightMeters"),
                    ReadFloat(body, "radiusMeters"),
                    ReadFloat(body, "massKg"),
                    ReadFloat(capsule, "height"),
                    ReadFloat(capsule, "radius"),
                    ReadVector3(capsule?["center"] as JObject));

            return new CharacterImportedGeometryBinding(
                ReadString(root, "packageId"),
                bodyProfile,
                ParseArray(root["bodyColliders"] as JArray, ParseBodyCollider),
                ParseArray(root["hitZoneBindings"] as JArray, ParseHitZoneBinding),
                ParseArray(root["sockets"] as JArray, ParseSocket),
                ParseArray(root["weaponAttachments"] as JArray, ParseWeaponAttachment),
                ParseArray(root["weaponTraces"] as JArray, ParseWeaponTrace));
        }

        public static CharacterImportedResourceMapping LoadResourceMapping(string json)
        {
            JObject root = ParseRoot(json, "resource mapping");
            if (!string.Equals(ReadString(root, "format"), "mx.characterResourceMapping.v1", StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException("Unsupported character resource mapping format.");

            return new CharacterImportedResourceMapping(
                ReadString(root, "packageId"),
                ParseArray(root["entries"] as JArray, ParseResourceMappingEntry));
        }

        public static ResourceCatalog LoadResourceCatalog(string json)
        {
            JObject root = ParseRoot(json, "Unity resource catalog");
            if (ReadInt(root, "schemaVersion", 1) != 1)
                throw new CharacterImportedPackageJsonException("Unsupported Unity resource catalog schema version.");

            string catalogId = ReadString(root, "catalogId");
            string packageId = ReadString(root, "packageId");
            var entries = new List<ResourceCatalogEntry>();
            JArray array = root["entries"] as JArray;
            if (array != null)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    JObject entry = array[i] as JObject;
                    if (entry == null)
                        continue;

                    Dictionary<string, string> providerData = ReadStringDictionary(entry["providerData"] as JObject);
                    MirrorUnityCatalogField(providerData, entry, "packageResourceKey");
                    MirrorUnityCatalogField(providerData, entry, "stableId");
                    MirrorUnityCatalogField(providerData, entry, "usage");
                    MirrorUnityCatalogField(providerData, entry, "sourceRelativePath");
                    MirrorUnityCatalogField(providerData, entry, "sourceFormat");
                    MirrorUnityCatalogField(providerData, entry, "declaredContentHash");
                    MirrorUnityCatalogField(providerData, entry, "contentHash");
                    MirrorUnityCatalogField(providerData, entry, "importHash");
                    MirrorUnityCatalogField(providerData, entry, "dependencyHash");
                    MirrorUnityCatalogField(providerData, entry, "unityAssetPath");
                    MirrorUnityCatalogField(providerData, entry, "unityAssetGuid");
                    MirrorUnityCatalogField(providerData, entry, "unityMainObjectType");
                    MirrorUnityCatalogField(providerData, entry, "importerKind");
                    MirrorUnityCatalogField(providerData, entry, "importStatus");
                    MirrorUnityCatalogDiagnostics(providerData, entry["diagnostics"] as JArray);

                    entries.Add(new ResourceCatalogEntry(
                        ReadString(entry, "id"),
                        ReadString(entry, "type"),
                        ReadString(entry, "provider"),
                        ReadString(entry, "address"),
                        ReadString(entry, "variant"),
                        string.IsNullOrEmpty(ReadString(entry, "packageId")) ? packageId : ReadString(entry, "packageId"),
                        ParseResourceKeyArray(entry["dependencies"] as JArray),
                        ReadStringArray(entry["labels"] as JArray),
                        ReadString(entry, "hash"),
                        ReadLong(entry, "size"),
                        ReadBool(entry, "allowOverride"),
                        providerData));
                }
            }

            return new ResourceCatalog(catalogId, packageId, entries);
        }

        public static CharacterResourcePlan LoadCharacterResourcePlan(string json)
        {
            JObject root = ParseRoot(json, "character resource plan");
            if (!string.Equals(ReadString(root, "format"), "mx.characterResourcePlan.v1", StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException("Unsupported character resource plan format.");

            var groups = new List<CharacterResourcePlanGroup>
            {
                ParseResourcePlanGroup(root, "spawnCritical", CharacterResourcePlanGroupKind.SpawnCritical, CharacterResourceFailurePolicy.FailSpawn),
                ParseResourcePlanGroup(root, "presentationCritical", CharacterResourcePlanGroupKind.PresentationCritical, CharacterResourceFailurePolicy.UseFallbackVisual),
                ParseResourcePlanGroup(root, "equipmentInitial", CharacterResourcePlanGroupKind.EquipmentInitial, CharacterResourceFailurePolicy.UseFallbackEquipment),
                ParseResourcePlanGroup(root, "animationWarmup", CharacterResourcePlanGroupKind.AnimationWarmup, CharacterResourceFailurePolicy.UseFallbackPose),
                ParseResourcePlanGroup(root, "vfxWarmup", CharacterResourcePlanGroupKind.VfxWarmup, CharacterResourceFailurePolicy.SkipEffect),
                ParseResourcePlanGroup(root, "uiDeferred", CharacterResourcePlanGroupKind.UiDeferred, CharacterResourceFailurePolicy.ShowPlaceholder)
            };

            return new CharacterResourcePlan(
                ReadString(root, "characterStableId"),
                ReadString(root, "planHash"),
                groups,
                ParseAudioResourcePlan(root["audio"] as JObject));
        }

        public static CharacterAudioCueManifest LoadAudioCueManifest(string json)
        {
            JObject root = ParseRoot(json, "audio cue manifest");
            if (!string.Equals(ReadString(root, "format"), "mx.characterAudioCueManifest.v1", StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException("Unsupported audio cue manifest format.");

            return new CharacterAudioCueManifest(
                ReadString(root, "packageId"),
                ReadString(root, "characterStableId"),
                ReadStringArray(root["banks"] as JArray),
                ParseArray(root["cues"] as JArray, ParseAudioCueManifestEntry));
        }

        public static CharacterAnimationSetDefinition LoadAnimationSetDefinition(string json)
        {
            JObject root = ParseRoot(json, "animation set definition");
            if (!string.Equals(ReadString(root, "format"), "mx.animationSetDefinition.v1", StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException("Unsupported animation set definition format.");

            return new CharacterAnimationSetDefinition(
                ReadString(root, "format"),
                ReadString(root, "schemaVersion"),
                ReadString(root, "packageId"),
                ReadString(root, "stableId"),
                ReadString(root, "displayName"),
                ReadString(root, "skeletonProfileId"),
                ReadString(root, "avatarProfileId"),
                ParseArray(root["sets"] as JArray, ParseAnimationSet),
                ParseArray(root["profiles"] as JArray, ParseAnimationProfile));
        }

        public static CharacterAnimationClipRegistry LoadAnimationClipRegistry(string json)
        {
            JObject root = ParseRoot(json, "animation clip registry");
            if (!string.Equals(ReadString(root, "format"), "mx.animationClipRegistry.v1", StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException("Unsupported animation clip registry format.");

            return new CharacterAnimationClipRegistry(
                ReadString(root, "format"),
                ReadString(root, "schemaVersion"),
                ReadString(root, "packageId"),
                ParseArray(root["clips"] as JArray, ParseAnimationClipRegistryEntry));
        }

        public static CharacterAnimationResourcePlan LoadAnimationResourcePlan(string json)
        {
            JObject root = ParseRoot(json, "animation resource plan");
            if (!string.Equals(ReadString(root, "format"), "mx.animationResourcePlan.v1", StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException("Unsupported animation resource plan format.");

            return new CharacterAnimationResourcePlan(
                ReadString(root, "format"),
                ReadString(root, "schemaVersion"),
                ReadString(root, "packageId"),
                ReadString(root, "stableId"),
                ReadString(root, "planHash"));
        }

        private static ResourceCatalog LoadOptionalResourceCatalogFromFile(string path)
        {
            return File.Exists(path) ? LoadResourceCatalog(File.ReadAllText(path)) : null;
        }

        private static CharacterResourcePlan LoadOptionalCharacterResourcePlanFromFile(string path)
        {
            return File.Exists(path) ? LoadCharacterResourcePlan(File.ReadAllText(path)) : null;
        }

        private static CharacterAudioCueManifest LoadOptionalAudioCueManifestFromFile(string path)
        {
            return File.Exists(path) ? LoadAudioCueManifest(File.ReadAllText(path)) : CharacterAudioCueManifest.Empty;
        }

        private static CharacterCompiledAnimationArtifacts LoadCompiledAnimationArtifacts(string configRoot, ResourceCatalog runtimeCatalog)
        {
            var diagnostics = new List<CharacterRuntimeSpawnIssue>();
            string setPath = Path.Combine(configRoot, "animation_set_definition.json");
            string clipRegistryPath = Path.Combine(configRoot, "animation_clip_registry.json");
            string resourcePlanPath = Path.Combine(configRoot, "animation_resource_plan.json");
            CharacterAnimationSetDefinition setDefinition = LoadOptionalAnimationArtifact(
                setPath,
                "config/animation_set_definition.json",
                "animation set definition",
                LoadAnimationSetDefinition,
                diagnostics);
            CharacterAnimationClipRegistry clipRegistry = LoadOptionalAnimationArtifact(
                clipRegistryPath,
                "config/animation_clip_registry.json",
                "animation clip registry",
                LoadAnimationClipRegistry,
                diagnostics);
            CharacterAnimationResourcePlan resourcePlan = LoadOptionalAnimationArtifact(
                resourcePlanPath,
                "config/animation_resource_plan.json",
                "animation resource plan",
                LoadAnimationResourcePlan,
                diagnostics);

            string runtimeResourcePackageId = runtimeCatalog != null && !string.IsNullOrWhiteSpace(runtimeCatalog.PackageId)
                ? runtimeCatalog.PackageId
                : setDefinition != null ? setDefinition.PackageId : string.Empty;
            IMxAnimationMappingProvider runtimeMappingProvider = File.Exists(setPath)
                ? MxAnimationCompiledArtifactJson.LoadMappingProvider(File.ReadAllText(setPath), runtimeResourcePackageId)
                : new MxAnimationStaticMappingProvider(null);
            MxAnimationClipRegistry runtimeClipRegistry = File.Exists(clipRegistryPath)
                ? MxAnimationCompiledArtifactJson.LoadClipRegistry(File.ReadAllText(clipRegistryPath), runtimeCatalog, runtimeResourcePackageId)
                : new MxAnimationClipRegistry(0, runtimeCatalog != null ? runtimeCatalog.CatalogId : string.Empty, string.Empty, null);

            return new CharacterCompiledAnimationArtifacts(setDefinition, clipRegistry, resourcePlan, diagnostics.ToArray(), runtimeMappingProvider, runtimeClipRegistry);
        }

        private static T LoadOptionalAnimationArtifact<T>(
            string path,
            string sourcePath,
            string description,
            Func<string, T> parse,
            List<CharacterRuntimeSpawnIssue> diagnostics)
            where T : class
        {
            if (File.Exists(path))
                return parse(File.ReadAllText(path));

            diagnostics.Add(new CharacterRuntimeSpawnIssue(
                CharacterRuntimeSpawnIssueSeverity.Error,
                CharacterRuntimeSpawnIssueCodes.MissingCompiledAnimationArtifact,
                sourcePath,
                string.Empty,
                "Compiled " + description + " artifact is missing."));
            return null;
        }

        private static CharacterResourcePlanGroup ParseResourcePlanGroup(
            JObject root,
            string propertyName,
            CharacterResourcePlanGroupKind kind,
            CharacterResourceFailurePolicy fallbackFailurePolicy)
        {
            JObject group = root[propertyName] as JObject;
            if (group == null)
                return CharacterResourcePlanGroup.Empty(kind);

            return new CharacterResourcePlanGroup(
                kind,
                kind.ToString(),
                ParseArray(group["resources"] as JArray, ParseResourcePlanResourceKey),
                ReadBool(group, "required"),
                ReadEnum(group, "failurePolicy", fallbackFailurePolicy));
        }

        private static ResourceKey ParseResourcePlanResourceKey(JObject fields)
        {
            return new ResourceKey(
                FirstNonEmpty(ReadString(fields, "resourceKey"), ReadString(fields, "id")),
                FirstNonEmpty(ReadString(fields, "typeId"), ReadString(fields, "type")),
                ReadString(fields, "variant"),
                ReadString(fields, "packageId"));
        }

        private static CharacterAudioResourcePlan ParseAudioResourcePlan(JObject fields)
        {
            if (fields == null)
                return CharacterAudioResourcePlan.Empty;

            string[] cueKeys = ReadStringArray(fields["requiredCues"] as JArray);
            var cueIds = new List<int>();
            for (int i = 0; i < cueKeys.Length; i++)
            {
                if (int.TryParse(cueKeys[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cueId))
                    cueIds.Add(cueId);
            }

            return new CharacterAudioResourcePlan(
                ReadStringArray(fields["requiredBanks"] as JArray),
                cueIds,
                ReadStringArray(fields["requiredEventDefinitionIds"] as JArray),
                ReadEnum(fields, "failurePolicy", CharacterResourceFailurePolicy.MuteMissingCue),
                cueKeys);
        }

        private static CharacterAudioCueManifestEntry ParseAudioCueManifestEntry(JObject fields)
        {
            return new CharacterAudioCueManifestEntry(
                ReadString(fields, "cueId"),
                ReadString(fields, "stableId"),
                ReadString(fields, "resourceKey"),
                ReadString(fields, "eventPath"),
                ReadString(fields, "bank"),
                ReadEnum(fields, "fallbackPolicy", CharacterResourceFailurePolicy.MuteMissingCue),
                ReadStringDictionary(fields["providerData"] as JObject));
        }

        private static CharacterAnimationSet ParseAnimationSet(JObject fields)
        {
            return new CharacterAnimationSet(
                ReadString(fields, "setId"),
                ReadString(fields, "displayName"),
                ReadString(fields, "version"),
                ReadString(fields, "defaultClipId"),
                ReadString(fields, "fallbackClipId"),
                ParseArray(fields["groups"] as JArray, ParseAnimationGroup),
                ParseArray(fields["actionBindings"] as JArray, ParseAnimationActionBinding));
        }

        private static CharacterAnimationGroup ParseAnimationGroup(JObject fields)
        {
            return new CharacterAnimationGroup(
                ReadString(fields, "groupId"),
                ReadString(fields, "displayName"),
                ReadString(fields, "usage"),
                ParseArray(fields["clips"] as JArray, ParseAnimationClipReference));
        }

        private static CharacterAnimationClipReference ParseAnimationClipReference(JObject fields)
        {
            return new CharacterAnimationClipReference(
                ReadString(fields, "clipId"),
                ReadString(fields, "displayName"),
                ReadString(fields, "runtimeResourceKey"),
                ReadString(fields, "sourceClipName"),
                ReadString(fields, "sourceSubClipId"),
                ReadBool(fields, "loop"),
                ReadFloat(fields, "speed", 1f),
                ReadString(fields, "rootMotionPolicy"));
        }

        private static CharacterAnimationActionBinding ParseAnimationActionBinding(JObject fields)
        {
            return new CharacterAnimationActionBinding(
                ReadString(fields, "bindingId"),
                ReadString(fields, "actionId"),
                ReadString(fields, "groupId"),
                ReadString(fields, "clipId"),
                ReadString(fields, "blendId"),
                ReadString(fields, "timelineId"),
                ReadBool(fields, "required"));
        }

        private static CharacterAnimationProfile ParseAnimationProfile(JObject fields)
        {
            return new CharacterAnimationProfile(
                ReadString(fields, "profileId"),
                ReadString(fields, "displayName"),
                ReadString(fields, "defaultSetId"),
                ReadString(fields, "defaultGroupId"));
        }

        private static CharacterAnimationClipRegistryEntry ParseAnimationClipRegistryEntry(JObject fields)
        {
            return new CharacterAnimationClipRegistryEntry(
                ReadString(fields, "setId"),
                ReadString(fields, "groupId"),
                ReadString(fields, "clipId"),
                ReadString(fields, "displayName"),
                ReadString(fields, "sourceClipName"),
                ReadString(fields, "sourceSubClipId"),
                ReadString(fields, "runtimeResourceKey"));
        }

        private static void MirrorUnityCatalogField(Dictionary<string, string> providerData, JObject entry, string name)
        {
            if (providerData == null || entry == null || string.IsNullOrWhiteSpace(name) || providerData.ContainsKey(name))
                return;

            string value = ReadString(entry, name);
            if (!string.IsNullOrWhiteSpace(value))
                providerData[name] = value;
        }

        private static void MirrorUnityCatalogDiagnostics(Dictionary<string, string> providerData, JArray diagnostics)
        {
            if (providerData == null || diagnostics == null || diagnostics.Count == 0)
                return;

            providerData["diagnosticCount"] = diagnostics.Count.ToString(CultureInfo.InvariantCulture);
            var codes = new List<string>();
            for (int i = 0; i < diagnostics.Count; i++)
            {
                JObject item = diagnostics[i] as JObject;
                string code = ReadString(item, "code");
                if (!string.IsNullOrWhiteSpace(code))
                    codes.Add(code);
            }

            codes.Sort(StringComparer.Ordinal);
            providerData["diagnosticCodes"] = string.Join(",", codes);
        }

        public static CharacterUnityImportRuntimeReport LoadImportReport(string json)
        {
            JObject root = ParseRoot(json, "import report");
            if (!string.Equals(ReadString(root, "format"), "mx.characterUnityImportReport.v1", StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException("Unsupported character Unity import report format.");

            return new CharacterUnityImportRuntimeReport(
                ReadString(root, "packageId"),
                ReadString(root, "packageStableId"),
                ReadString(root, "status"),
                ReadBool(root, "canWriteToUnityProject"),
                ReadBool(root, "canSpawnAfterImport"),
                ReadString(root, "targetRootPath"),
                ReadString(root, "reportPath"),
                ReadString(root, "sourcePackageHash"),
                ReadString(root, "generatedConfigHash"),
                ReadString(root, "geometryBindingHash"),
                ReadString(root, "resourceMappingHash"),
                ReadString(root, "writePlanHash"),
                ReadInt(root, "conflictCount"),
                ReadInt(root, "errorCount"));
        }

        private static CharacterConfig ParseCharacter(JObject fields)
        {
            return new CharacterConfig(
                new CharacterConfigId(ReadInt(fields, "CharacterId")),
                ReadString(fields, "StableId"),
                new LocalizedTextKey(ReadString(fields, "NameText")),
                new LocalizedTextKey(ReadString(fields, "DescriptionText")),
                new CharacterAttributeProfileId(ReadInt(fields, "AttributeProfileId")),
                new CharacterBodyProfileId(ReadInt(fields, "BodyProfileId")),
                new EquipmentSchemaId(ReadInt(fields, "EquipmentSchemaId")),
                new EquipmentLoadoutId(ReadInt(fields, "DefaultLoadoutId")),
                new AbilityLoadoutId(ReadInt(fields, "BaseAbilityLoadoutId")),
                new CharacterPresentationProfileId(ReadInt(fields, "PresentationProfileId")),
                ReadEnum(fields, "DefaultControllerKind", CharacterControllerKind.None),
                ReadString(fields, "DefaultControllerProfileId"),
                ReadStringArray(fields["DefaultSpawnTags"] as JArray));
        }

        private static CharacterAttributeProfileConfig ParseAttributeProfile(JObject fields)
        {
            return new CharacterAttributeProfileConfig(
                new CharacterAttributeProfileId(ReadInt(fields, "AttributeProfileId")),
                ReadString(fields, "StableId"),
                ParseArray(fields["Attributes"] as JArray, item => new CharacterAttributeEntry(
                    new CharacterAttributeId(ReadInt(item, "AttributeId")),
                    ReadString(item, "StableId"),
                    ReadEnum(item, "Group", CharacterAttributeGroup.Custom),
                    ReadFloat(item, "BaseValue"),
                    ReadFloat(item, "InitialValue"),
                    ReadFloat(item, "MinValue"),
                    ReadFloat(item, "MaxValue"))));
        }

        private static CharacterBodyProfileConfig ParseBodyProfile(JObject fields)
        {
            return new CharacterBodyProfileConfig(
                new CharacterBodyProfileId(ReadInt(fields, "BodyProfileId")),
                ReadString(fields, "StableId"),
                ReadEnum(fields, "BodyKind", CharacterBodyKind.Custom),
                ReadString(fields, "PartSetId"),
                ReadString(fields, "DefaultMotionProfileId"),
                ReadString(fields, "DefaultPhysicsProfileId"),
                ParseArray(fields["Sockets"] as JArray, item => new CharacterSocketEntry(
                    ReadString(item, "SocketId"),
                    ReadString(item, "ParentPartId"),
                    ReadString(item, "LocatorId"))),
                ParseArray(fields["HitZoneBindings"] as JArray, item => new CharacterHitZoneBindingEntry(
                    ReadString(item, "HitZoneId"),
                    ReadString(item, "PartId"),
                    ReadInt(item, "Priority"),
                    ReadBool(item, "IsWeakPoint"),
                    ReadFloat(item, "DamageMultiplierOverride"),
                    ReadFloat(item, "PostureDamageScaleOverride"))));
        }

        private static CharacterBodyPartConfig ParseBodyPart(JObject fields)
        {
            return new CharacterBodyPartConfig(
                new CharacterBodyPartConfigId(ReadInt(fields, "BodyPartConfigId")),
                ReadString(fields, "StableId"),
                ReadString(fields, "PartSetId"),
                ReadString(fields, "PartId"),
                ReadString(fields, "ParentPartId"),
                ReadEnum(fields, "PartKind", CharacterBodyPartKind.Custom),
                ReadString(fields, "LocatorId"),
                ReadString(fields, "HitZoneId"),
                ReadString(fields, "ReactionGroupId"),
                ReadFloat(fields, "DamageMultiplier"),
                ReadFloat(fields, "ImpulseScale"),
                ReadFloat(fields, "StaggerScale"),
                ReadFloat(fields, "PostureDamageScale"),
                ReadBool(fields, "IsCritical"));
        }

        private static EquipmentSchemaConfig ParseEquipmentSchema(JObject fields)
        {
            return new EquipmentSchemaConfig(
                new EquipmentSchemaId(ReadInt(fields, "EquipmentSchemaId")),
                ReadString(fields, "StableId"),
                ParseArray(fields["Slots"] as JArray, item => new EquipmentSlotEntry(
                    ReadString(item, "SlotId"),
                    ReadEnum(item, "Kind", EquipmentSlotKind.Custom),
                    ReadString(item, "DisplayName"),
                    ReadStringArray(item["AllowedWeaponCategories"] as JArray),
                    ReadStringArray(item["RequiredTags"] as JArray))),
                ParseArray(fields["ExclusiveGroups"] as JArray, item => new EquipmentExclusiveGroupEntry(
                    ReadString(item, "GroupId"),
                    ReadStringArray(item["SlotIds"] as JArray),
                    ReadInt(item, "MaxFilledCount"))),
                ParseIdArray(fields["AllowedStateIds"] as JArray, value => new EquipmentStateId(value)));
        }

        private static EquipmentLoadoutConfig ParseLoadout(JObject fields)
        {
            return new EquipmentLoadoutConfig(
                new EquipmentLoadoutId(ReadInt(fields, "LoadoutId")),
                ReadString(fields, "StableId"),
                new EquipmentSchemaId(ReadInt(fields, "EquipmentSchemaId")),
                ParseArray(fields["Slots"] as JArray, item => new EquipmentLoadoutSlotEntry(
                    ReadString(item, "SlotId"),
                    new WeaponConfigId(ReadInt(item, "WeaponId")),
                    ReadString(item, "WeaponInstanceStableId"))));
        }

        private static EquipmentStateConfig ParseEquipmentState(JObject fields)
        {
            return new EquipmentStateConfig(
                new EquipmentStateId(ReadInt(fields, "StateId")),
                ReadString(fields, "StableId"),
                new EquipmentSchemaId(ReadInt(fields, "EquipmentSchemaId")),
                ReadInt(fields, "Priority"),
                ParseArray(fields["MatchRules"] as JArray, item => new EquipmentMatchRule(
                    ReadStringArray(item["RequiredFilledSlots"] as JArray),
                    ReadStringArray(item["RequiredEmptySlots"] as JArray),
                    ReadStringArray(item["RequiredWeaponCategoriesBySlot"] as JArray),
                    ReadStringArray(item["RequiredWeaponTagsBySlot"] as JArray),
                    ReadStringArray(item["ForbiddenWeaponTagsBySlot"] as JArray))),
                new AbilityLoadoutId(ReadInt(fields, "GrantedAbilityLoadoutId")),
                new CombatActionSetId(ReadInt(fields, "CombatActionSetId")),
                ReadString(fields, "AnimationProfileId"),
                ReadStringArray(fields["Tags"] as JArray));
        }

        private static WeaponConfig ParseWeapon(JObject fields)
        {
            return new WeaponConfig(
                new WeaponConfigId(ReadInt(fields, "WeaponId")),
                ReadString(fields, "StableId"),
                ReadEnum(fields, "Category", WeaponCategory.Custom),
                ReadStringArray(fields["Tags"] as JArray),
                ReadStringArray(fields["OccupiesSlots"] as JArray),
                new AbilityLoadoutId(ReadInt(fields, "GrantedAbilityLoadoutId")),
                ReadString(fields, "WeaponPresentationProfileId"),
                ReadString(fields, "WeaponCombatProfileId"),
                ReadString(fields, "WeaponResourceProfileId"),
                ParseArray(fields["ResourceKeys"] as JArray, ParseResourceKeyEntry),
                ParseArray(fields["TraceBindings"] as JArray, item => new CharacterTraceBindingEntry(
                    ReadString(item, "TraceProfileId"),
                    ReadString(item, "SlotId"),
                    ReadString(item, "SocketId"),
                    ReadFloat(item, "Length"),
                    ReadFloat(item, "Radius"))),
                ParseArray(fields["BaseModifiers"] as JArray, item => new CharacterModifierGrantEntry(
                    ReadString(item, "ModifierStableId"),
                    ReadFloat(item, "Value"),
                    ReadString(item, "Source"))));
        }

        private static AbilityLoadoutConfig ParseAbilityLoadout(JObject fields)
        {
            return new AbilityLoadoutConfig(
                new AbilityLoadoutId(ReadInt(fields, "LoadoutId")),
                ReadString(fields, "StableId"),
                ParseIdArray(fields["AbilityIds"] as JArray, value => new CharacterAbilityId(value)),
                ParseIdArray(fields["RemoveAbilityIds"] as JArray, value => new CharacterAbilityId(value)),
                ParseArray(fields["SlotBindings"] as JArray, item => new AbilitySlotBinding(
                    ReadString(item, "SlotId"),
                    new CharacterAbilityId(ReadInt(item, "AbilityId")),
                    ReadString(item, "InputIntentId"))));
        }

        private static CombatActionSetConfig ParseCombatActionSet(JObject fields)
        {
            return new CombatActionSetConfig(
                new CombatActionSetId(ReadInt(fields, "ActionSetId")),
                ReadString(fields, "StableId"),
                ParseArray(fields["Actions"] as JArray, item => new CombatActionEntry(
                    ReadString(item, "ActionKey"),
                    new CharacterCombatActionId(ReadInt(item, "CombatActionId")),
                    ReadString(item, "TraceProfileIdOverride"),
                    ReadString(item, "AnimationActionKey"))));
        }

        private static CharacterPresentationProfileConfig ParsePresentation(JObject fields)
        {
            return new CharacterPresentationProfileConfig(
                new CharacterPresentationProfileId(ReadInt(fields, "PresentationProfileId")),
                ReadString(fields, "StableId"),
                ReadString(fields, "DefaultAnimationProfileId"),
                ParseArray(fields["ResourceKeys"] as JArray, ParseResourceKeyEntry),
                ReadStringArray(fields["PresentationTags"] as JArray));
        }

        private static SpawnProfileConfig ParseSpawnProfile(JObject fields)
        {
            return new SpawnProfileConfig(
                new SpawnProfileId(ReadInt(fields, "SpawnProfileId")),
                ReadString(fields, "StableId"),
                new CharacterConfigId(ReadInt(fields, "CharacterId")),
                ReadString(fields, "TeamId"),
                ReadEnum(fields, "ControllerKind", CharacterControllerKind.None),
                new EquipmentLoadoutId(ReadInt(fields, "EquipmentLoadoutId")),
                ParsePoseEntry(fields["SpawnPose"] as JObject),
                ReadString(fields, "RuntimeAiPlannerProfileId"),
                ReadString(fields, "DebugName"));
        }

        private static CharacterResourceKeyEntry ParseResourceKeyEntry(JObject fields)
        {
            return new CharacterResourceKeyEntry(
                ReadString(fields, "Id"),
                ReadString(fields, "TypeId"),
                ReadEnum(fields, "UsageKind", CharacterResourceUsageKind.Custom),
                ReadString(fields, "Variant"),
                ReadString(fields, "PackageId"),
                ReadString(fields, "PreloadGroupId"));
        }

        private static CharacterPoseEntry ParsePoseEntry(JObject fields)
        {
            if (fields == null)
                return default;

            return new CharacterPoseEntry(
                ReadString(fields, "AnchorId"),
                ReadFloat(fields, "X"),
                ReadFloat(fields, "Y"),
                ReadFloat(fields, "Z"),
                ReadFloat(fields, "YawDegrees"));
        }

        private static CharacterBodyColliderRuntimeBinding ParseBodyCollider(JObject fields)
        {
            return new CharacterBodyColliderRuntimeBinding(
                ReadString(fields, "colliderId"),
                ReadString(fields, "partId"),
                ReadString(fields, "hitZoneId"),
                ReadString(fields, "shape"),
                ReadPose(fields["localPose"] as JObject),
                ReadVector3(fields["size"] as JObject),
                ReadFloat(fields, "radius"),
                ReadFloat(fields, "height"),
                ReadInt(fields, "priority"),
                ReadBool(fields, "isWeakPoint"),
                ReadString(fields, "physicsLayer"));
        }

        private static CharacterHitZoneRuntimeBinding ParseHitZoneBinding(JObject fields)
        {
            return new CharacterHitZoneRuntimeBinding(
                ReadString(fields, "hitZoneId"),
                ReadString(fields, "partId"),
                ReadInt(fields, "priority"),
                ReadBool(fields, "isWeakPoint"));
        }

        private static CharacterSocketRuntimeBinding ParseSocket(JObject fields)
        {
            return new CharacterSocketRuntimeBinding(
                ReadString(fields, "socketId"),
                ReadString(fields, "parentPartId"),
                ReadString(fields, "bonePath"),
                ReadString(fields, "locatorPath"),
                ReadPose(fields["localPose"] as JObject),
                ReadString(fields, "usage"));
        }

        private static CharacterWeaponAttachmentRuntimeBinding ParseWeaponAttachment(JObject fields)
        {
            return new CharacterWeaponAttachmentRuntimeBinding(
                ReadString(fields, "weaponId"),
                ReadString(fields, "equipSlot"),
                ReadString(fields, "attachSocketId"),
                ReadPose(fields["localGripPose"] as JObject),
                ReadString(fields, "previewResourceKey"),
                ReadString(fields, "traceId"));
        }

        private static CharacterWeaponTraceRuntimeBinding ParseWeaponTrace(JObject fields)
        {
            return new CharacterWeaponTraceRuntimeBinding(
                ReadString(fields, "traceId"),
                ReadString(fields, "weaponId"),
                ReadString(fields, "equipSlot"),
                ReadString(fields, "startLocatorPath"),
                ReadString(fields, "endLocatorPath"),
                ReadFloat(fields, "radius"),
                ReadString(fields, "sampleRule"),
                ReadInt(fields, "fixedSampleCount"),
                ReadStringArray(fields["actionKeys"] as JArray));
        }

        private static CharacterImportedResourceMappingEntry ParseResourceMappingEntry(JObject fields)
        {
            return new CharacterImportedResourceMappingEntry(
                ReadString(fields, "packageResourceKey"),
                ReadString(fields, "projectResourceKey"),
                ReadString(fields, "stableId"),
                ReadString(fields, "typeId"),
                ReadString(fields, "usage"),
                ReadString(fields, "importTargetPath"),
                ReadString(fields, "importHash"),
                ReadString(fields, "dependencyHash"));
        }

        private static CharacterRuntimePose ReadPose(JObject fields)
        {
            if (fields == null)
                return default;

            return new CharacterRuntimePose(
                ReadString(fields, "parentKind"),
                ReadString(fields, "parentPath"),
                ReadVector3(fields["position"] as JObject),
                ReadQuaternion(fields["rotation"] as JObject),
                ReadVector3(fields["scale"] as JObject, 1f));
        }

        private static CharacterRuntimeVector3 ReadVector3(JObject fields, float defaultComponent = 0f)
        {
            if (fields == null)
                return new CharacterRuntimeVector3(defaultComponent, defaultComponent, defaultComponent);

            return new CharacterRuntimeVector3(
                ReadFloat(fields, "x", defaultComponent),
                ReadFloat(fields, "y", defaultComponent),
                ReadFloat(fields, "z", defaultComponent));
        }

        private static CharacterRuntimeQuaternion ReadQuaternion(JObject fields)
        {
            if (fields == null)
                return new CharacterRuntimeQuaternion(0f, 0f, 0f, 1f);

            return new CharacterRuntimeQuaternion(
                ReadFloat(fields, "x"),
                ReadFloat(fields, "y"),
                ReadFloat(fields, "z"),
                ReadFloat(fields, "w", 1f));
        }

        private static string ReadRequiredFile(string path, string description)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new CharacterImportedPackageJsonException(description + " path is missing.");
            if (!File.Exists(path))
                throw new CharacterImportedPackageJsonException(description + " file was not found: " + path + ".");

            return File.ReadAllText(path);
        }

        private static JObject ParseRoot(string json, string description)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new CharacterImportedPackageJsonException(description + " json is empty.");

            try
            {
                return JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new CharacterImportedPackageJsonException(description + " json could not be parsed: " + ex.Message + ".", ex);
            }
        }

        private static T[] ParseArray<T>(JArray array, Func<JObject, T> parse)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<T>();

            var values = new List<T>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                if (item != null)
                    values.Add(parse(item));
            }

            return values.ToArray();
        }

        private static TId[] ParseIdArray<TId>(JArray array, Func<int, TId> create)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<TId>();

            var values = new TId[array.Count];
            for (int i = 0; i < array.Count; i++)
                values[i] = create(ReadInt(array[i]));

            return values;
        }

        private static ResourceKey[] ParseResourceKeyArray(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<ResourceKey>();

            var keys = new List<ResourceKey>(array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                if (item == null)
                    continue;

                keys.Add(new ResourceKey(
                    ReadString(item, "id"),
                    ReadString(item, "type"),
                    ReadString(item, "variant"),
                    ReadString(item, "packageId")));
            }

            return keys.ToArray();
        }

        private static string[] ReadStringArray(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<string>();

            var values = new string[array.Count];
            for (int i = 0; i < array.Count; i++)
                values[i] = array[i]?.ToString() ?? string.Empty;
            return values;
        }

        private static Dictionary<string, string> ReadStringDictionary(JObject obj)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (obj == null)
                return values;

            foreach (JProperty property in obj.Properties())
                values[property.Name] = property.Value?.ToString() ?? string.Empty;
            return values;
        }

        private static TEnum ReadEnum<TEnum>(JObject obj, string name, TEnum fallback) where TEnum : struct
        {
            string value = ReadString(obj, name);
            if (Enum.TryParse(value, true, out TEnum parsed))
                return parsed;

            return fallback;
        }

        private static string ReadString(JObject obj, string name)
        {
            return obj == null ? string.Empty : obj[name]?.ToString() ?? string.Empty;
        }

        private static int ReadInt(JObject obj, string name, int fallback = 0)
        {
            return obj == null ? fallback : ReadInt(obj[name], fallback);
        }

        private static int ReadInt(JToken token, int fallback = 0)
        {
            if (token == null)
                return fallback;
            if (token.Type == JTokenType.Integer)
                return token.Value<int>();

            return int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }

        private static long ReadLong(JObject obj, string name, long fallback = 0L)
        {
            JToken token = obj == null ? null : obj[name];
            if (token == null)
                return fallback;
            if (token.Type == JTokenType.Integer)
                return token.Value<long>();

            return long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                ? value
                : fallback;
        }

        private static float ReadFloat(JObject obj, string name, float fallback = 0f)
        {
            JToken token = obj == null ? null : obj[name];
            if (token == null)
                return fallback;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                return token.Value<float>();

            return float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value
                : fallback;
        }

        private static bool ReadBool(JObject obj, string name, bool fallback = false)
        {
            JToken token = obj == null ? null : obj[name];
            if (token == null)
                return fallback;
            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();

            return bool.TryParse(token.ToString(), out bool value) ? value : fallback;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }
    }

    public sealed class CharacterImportedPackageJsonException : Exception
    {
        public CharacterImportedPackageJsonException(string message)
            : base(message)
        {
        }

        public CharacterImportedPackageJsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
