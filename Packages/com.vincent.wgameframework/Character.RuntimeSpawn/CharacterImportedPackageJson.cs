using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.Config;
using MxFramework.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterRuntimeSpawn
{
    public static partial class CharacterImportedPackageJson
    {
        private static int ReadNumericIdentity(JObject source, string key)
        {
            JToken token = source?[key];
            if (token == null || token.Type == JTokenType.Null) return 0;
            if (token.Type != JTokenType.Integer) throw new CharacterImportedPackageJsonException("Expected numeric content identity: " + key);
            long value = token.Value<long>();
            if (value < 0 || value > int.MaxValue) throw new CharacterImportedPackageJsonException("Content identity outside Int32: " + key);
            return (int)value;
        }
        private static int[] ReadNumericIdentities(JArray values)
        {
            if (values == null) return Array.Empty<int>();
            var result = new int[values.Count];
            for (int i = 0; i < result.Length; i++)
            {
                if (values[i].Type != JTokenType.Integer || values[i].Value<long>() <= 0 || values[i].Value<long>() > int.MaxValue)
                    throw new CharacterImportedPackageJsonException("Content identity array requires positive Int32 values.");
                result[i] = values[i].Value<int>();
            }
            return result;
        }
        private static bool IsStableBodyPartDamageId(int value) => value > 0;
        private static bool IsStableAnchorId(int value) => value > 0;
        private static bool IsRuntimeStableId(int value) => value > 0;

        public static CharacterImportedPackage LoadFromDirectory(string importedPackageRoot)
        {
            if (string.IsNullOrWhiteSpace(importedPackageRoot))
                throw new CharacterImportedPackageJsonException("Imported package root path is missing.");
            if (!Directory.Exists(importedPackageRoot))
                throw new CharacterImportedPackageJsonException("Imported package root was not found: " + importedPackageRoot + ".");

            string configRoot = Path.Combine(importedPackageRoot, "config");
            string cacheRoot = Path.Combine(importedPackageRoot, "package_cache");
            CharacterImportedConfigSet configs = LoadConfigSetFromFile(Path.Combine(configRoot, "character_config_patch.json"));
            if (configs.RuntimeCharacterId <= 0)
                throw new CharacterImportedPackageJsonException("Imported character package has no persisted runtimeCharacterId. Regenerate it with the current authoring producer.");
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

            string tacticalPath = Path.Combine(importedPackageRoot, CharacterTacticalDefaults.RelativePath);
            CharacterTacticalDefaults tactical = File.Exists(tacticalPath)
                ? JsonConvert.DeserializeObject<CharacterTacticalDefaults>(File.ReadAllText(tacticalPath)) : null;
            if (File.Exists(tacticalPath) && (tactical == null || !tactical.TryValidate(out _)))
                throw new CharacterImportedPackageJsonException("Invalid character tactical defaults: " + tacticalPath);
            return new CharacterImportedPackage(importedPackageRoot, packageId, configs, geometry, mapping, catalog, report, runtimeCatalog, runtimePlan, audioCueManifest, compiledAnimationArtifacts, tactical);
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
            var bodyPartContactPoints = new List<CharacterBodyPartContactPointConfig>();
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
                    case CharacterBodyPartContactPointConfig.TableName:
                        bodyPartContactPoints.Add(ParseBodyPartContactPoint(fields));
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
                bodyPartContactPoints.ToArray(),
                equipmentSchemas.ToArray(),
                loadouts.ToArray(),
                states.ToArray(),
                weapons.ToArray(),
                abilityLoadouts.ToArray(),
                actionSets.ToArray(),
                presentations.ToArray(),
                spawnProfiles.ToArray(), ReadNumericIdentity(root, "runtimeCharacterId"));
        }

        public static CharacterImportedGeometryBinding LoadGeometryBinding(string json)
        {
            JObject root = ParseRoot(json, "geometry binding");
            string format = ReadString(root, "format");
            string schemaVersion = ReadString(root, "schemaVersion");
            if (format != "mx.characterGeometryBinding.v8" || schemaVersion != "8.0")
                throw new CharacterImportedPackageJsonException("Unsupported character geometry identity schema. Expected v8/8.0.");
            int features = ReadInt(root, "features");
            if ((features & ~62) != 0)
                throw new CharacterImportedPackageJsonException("Unknown character geometry feature flags.");
            bool isV2 = true;
            bool isV3 = false;
            bool isV4 = (features & 4) != 0;
            bool isV5 = (features & 16) != 0;
            bool isV6 = (features & 32) != 0;
            bool isV7 = (features & 8) != 0;

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

            CharacterHitZoneRuntimeBinding[] hitZoneBindings =
                ParseArray(root["hitZoneBindings"] as JArray, ParseHitZoneBinding);
            CharacterDamageZoneRuntimeBinding[] damageZones = isV2 || isV3 || isV4 || isV5 || isV6 || isV7
                ? ParseArray(root["damageZones"] as JArray, ParseDamageZoneBinding)
                : Array.Empty<CharacterDamageZoneRuntimeBinding>();
            ValidateDamageZoneBindings(damageZones, hitZoneBindings);
            bool hasAuthoredEngagementAnchors = (features & 2) != 0;
            int engagementAnchorRevision = hasAuthoredEngagementAnchors
                ? ReadInt(root, "engagementAnchorRevision")
                : 0;
            string engagementAnchorContentHash = hasAuthoredEngagementAnchors
                ? ReadString(root, "engagementAnchorContentHash")
                : string.Empty;
            CharacterEngagementAnchorRuntimeBinding[] engagementAnchors = hasAuthoredEngagementAnchors
                ? ParseArray(root["engagementAnchors"] as JArray, ParseEngagementAnchorBinding)
                : Array.Empty<CharacterEngagementAnchorRuntimeBinding>();
            if (hasAuthoredEngagementAnchors)
            {
                ValidateEngagementAnchorBindings(
                    engagementAnchorRevision,
                    engagementAnchorContentHash,
                    engagementAnchors,
                    damageZones);
            }
            int bodyPresenceRevision = 0;
            string bodyPresenceContentHash = string.Empty;
            bool hasAuthoredBodyPresence = (features & 4) != 0;
            CharacterBodyPresenceRuntimeBinding[] bodyPresenceVolumes = hasAuthoredBodyPresence
                ? ParseBodyPresenceBindings(
                    root,
                    out bodyPresenceRevision,
                    out bodyPresenceContentHash,
                    verifyContentHash: !isV7)
                : Array.Empty<CharacterBodyPresenceRuntimeBinding>();
            CharacterBodyColliderRuntimeBinding[] bodyColliders =
                ParseArray(root["bodyColliders"] as JArray, ParseBodyCollider);
            CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding[] animatedHurtboxSources = isV7
                ? ParseBodyPresenceV2AnimatedSources(root, bodyColliders, bodyPresenceRevision,
                    bodyPresenceContentHash, bodyPresenceVolumes)
                : Array.Empty<CharacterBodyPresenceAnimatedHurtboxSourceRuntimeBinding>();
            CharacterBodyPartDamageRuntimeBinding[] bodyPartDamageDefinitions =
                Array.Empty<CharacterBodyPartDamageRuntimeBinding>();
            CharacterBodyPartDamageLookupRuntimeBinding[] bodyPartDamagePartLookup =
                Array.Empty<CharacterBodyPartDamageLookupRuntimeBinding>();
            string bodyPartDamageFormat = string.Empty;
            string bodyPartDamageSchemaVersion = string.Empty;
            int bodyPartDamageRevision = 0;
            string bodyPartDamageContentHash = string.Empty;
            if (isV5)
            {
                ParseBodyPartDamageBindings(
                    root,
                    damageZones,
                    out bodyPartDamageDefinitions,
                    out bodyPartDamagePartLookup,
                    out bodyPartDamageFormat,
                    out bodyPartDamageSchemaVersion,
                    out bodyPartDamageRevision,
                    out bodyPartDamageContentHash);
            }
            CharacterAuthorityPoseRigRuntimeBinding authorityPoseRig = isV6
                ? ParseAuthorityPoseRig(root, bodyPartDamageDefinitions, bodyPartDamagePartLookup)
                : CharacterAuthorityPoseRigRuntimeBinding.Missing;
            return new CharacterImportedGeometryBinding(
                format,
                schemaVersion,
                ReadString(root, "packageId"),
                bodyProfile,
                bodyColliders,
                hitZoneBindings,
                damageZones,
                ParseArray(root["sockets"] as JArray, ParseSocket),
                ParseArray(root["weaponAttachments"] as JArray, ParseWeaponAttachment),
                ParseArray(root["weaponTraces"] as JArray, ParseWeaponTrace),
                engagementAnchors,
                engagementAnchorRevision,
                engagementAnchorContentHash,
                hasAuthoredEngagementAnchors
                    ? CharacterEngagementAnchorMode.AuthoredAnchors
                    : CharacterEngagementAnchorMode.CompatibilityRootApproach,
                bodyPresenceVolumes,
                bodyPresenceRevision,
                bodyPresenceContentHash,
                hasAuthoredBodyPresence
                    ? CharacterBodyPresenceMode.Authored
                    : CharacterBodyPresenceMode.CompatibilityHardRoot,
                bodyPartDamageDefinitions,
                bodyPartDamagePartLookup,
                bodyPartDamageFormat,
                bodyPartDamageSchemaVersion,
                bodyPartDamageRevision,
                bodyPartDamageContentHash,
                (isV5 || isV6 || isV7) ? CharacterBodyPartDamageMode.Authored : CharacterBodyPartDamageMode.Missing,
                authorityPoseRig,
                animatedHurtboxSources);
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
                    MirrorUnityCatalogField(providerData, entry, "sourceHash");
                    MirrorUnityCatalogField(providerData, entry, "artifactHash");
                    MirrorUnityCatalogField(providerData, entry, "artifactStatus");
                    MirrorUnityCatalogField(providerData, entry, "artifactValidationStatus");
                    MirrorUnityCatalogField(providerData, entry, "artifactTransforms");
                    MirrorUnityCatalogField(providerData, entry, "gltfAnimationMethod");
                    MirrorUnityCatalogField(providerData, entry, "avatarSourceStatus");
                    MirrorUnityCatalogField(providerData, entry, "avatarSourceRelativePath");
                    MirrorUnityCatalogField(providerData, entry, "avatarSourceAssetPath");
                    MirrorUnityCatalogField(providerData, entry, "avatarSourceContentHash");
                    MirrorUnityCatalogField(providerData, entry, "avatarSourceGuid");
                    MirrorUnityCatalogField(providerData, entry, "avatarSourceHumanoidStatus");
                    MirrorUnityCatalogField(providerData, entry, "avatarSourceMessage");
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
            string format = ReadString(root, "format");
            if (!string.Equals(format, "mx.characterAudioCueManifest.v1", StringComparison.Ordinal)
                && !string.Equals(format, "mx.characterAudioCueManifest.v2", StringComparison.Ordinal))
                throw new CharacterImportedPackageJsonException("Unsupported audio cue manifest format.");

            return new CharacterAudioCueManifest(
                ReadString(root, "packageId"),
                ReadString(root, "characterStableId"),
                ReadStringArray(root["banks"] as JArray),
                ParseArray(root["cues"] as JArray, ParseAudioCueManifestEntry),
                ReadString(root, "globalCatalogHash"));
        }

        public static CharacterAnimationSetDefinition LoadAnimationSetDefinition(string json)
        {
            JObject root = ParseRoot(json, "animation set definition");
            if (!string.Equals(ReadString(root, "format"), "mx.animationSetDefinition.v2", StringComparison.Ordinal))
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

        private const string AnimationResourcePlanFormatV1 = "mx.animationResourcePlan.v1";
        private const string AnimationResourcePlanFormatV2 = "mx.animationResourcePlan.v2";
        private const string AnimationResourcePlanSchemaVersionV2 = "2.0";

        private static readonly string[] AnimationResourcePlanV2Fields =
        {
            "format",
            "schemaVersion",
            "packageId",
            "stableId",
            "planHash"
        };

        public static CharacterAnimationResourcePlan LoadAnimationResourcePlan(string json)
        {
            JObject root = ParseRoot(json, "animation resource plan");
            string format = ReadString(root, "format");
            if (string.Equals(format, AnimationResourcePlanFormatV1, StringComparison.Ordinal))
                return LoadAnimationResourcePlanV1(root);
            if (string.Equals(format, AnimationResourcePlanFormatV2, StringComparison.Ordinal))
                return LoadAnimationResourcePlanV2(root);

            throw new CharacterImportedPackageJsonException("Unsupported animation resource plan format.");
        }

        /// <summary>完整编译计划（含嵌套 catalog / resource plan / audio manifest / diagnostics）的宽松读取。</summary>
        private static CharacterAnimationResourcePlan LoadAnimationResourcePlanV1(JObject root)
        {
            return new CharacterAnimationResourcePlan(
                ReadString(root, "format"),
                ReadString(root, "schemaVersion"),
                ReadString(root, "packageId"),
                ReadString(root, "stableId"),
                ReadString(root, "planHash"));
        }

        /// <summary>
        /// 正式角色包运行投影：恰好五个字段，严格校验。
        /// </summary>
        /// <remarks>
        /// 严格是有意的：任何额外顶层字段（包括被裁掉的四个完整对象）都拒绝，
        /// 未来新增字段必须升级 wire version，不做加法式扩展。
        /// </remarks>
        private static CharacterAnimationResourcePlan LoadAnimationResourcePlanV2(JObject root)
        {
            foreach (JProperty property in root.Properties())
            {
                if (Array.IndexOf(AnimationResourcePlanV2Fields, property.Name) >= 0)
                    continue;

                throw new CharacterImportedPackageJsonException(
                    "Animation resource plan v2 must contain exactly the five runtime fields; unexpected field '" + property.Name + "'.");
            }

            string schemaVersion = ReadRequiredStringField(root, "schemaVersion", "animation resource plan v2");
            if (!string.Equals(schemaVersion, AnimationResourcePlanSchemaVersionV2, StringComparison.Ordinal))
            {
                throw new CharacterImportedPackageJsonException(
                    "Unsupported animation resource plan v2 schema version: " + schemaVersion + ".");
            }

            string packageId = ReadRequiredStringField(root, "packageId", "animation resource plan v2");
            string stableId = ReadRequiredStringField(root, "stableId", "animation resource plan v2");
            string planHash = ReadRequiredStringField(root, "planHash", "animation resource plan v2");
            if (!IsLowercaseSha256Hash(planHash))
            {
                throw new CharacterImportedPackageJsonException(
                    "Animation resource plan v2 field 'planHash' must be 'sha256:' followed by 64 lowercase hex characters.");
            }

            return new CharacterAnimationResourcePlan(
                AnimationResourcePlanFormatV2,
                schemaVersion,
                packageId,
                stableId,
                planHash);
        }

        private static string ReadRequiredStringField(JObject root, string name, string description)
        {
            JToken token = root[name];
            if (token == null || token.Type != JTokenType.String)
                throw new CharacterImportedPackageJsonException(description + " field '" + name + "' must be a string.");

            string value = token.Value<string>() ?? string.Empty;
            if (value.Length == 0)
                throw new CharacterImportedPackageJsonException(description + " field '" + name + "' must not be empty.");

            return value;
        }

        private static bool IsLowercaseSha256Hash(string value)
        {
            const string prefix = "sha256:";
            if (value == null || value.Length != prefix.Length + 64 || !value.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            for (int i = prefix.Length; i < value.Length; i++)
            {
                char character = value[i];
                bool isLowercaseHex = (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f');
                if (!isLowercaseHex)
                    return false;
            }

            return true;
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
            RequireAnimationResourcePlanIdentity(resourcePlan, setDefinition);

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
            where T : class        {
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

        /// <summary>
        /// v2 运行投影的跨产物身份校验：其 PackageId/StableId 必须与同包 AnimationSetDefinition 一致。
        /// </summary>
        /// <remarks>
        /// 只作用于 v2，保留旧 v1 的兼容边界。比较的是两个动画产物（例如
        /// <c>animation.000008</c> / <c>charpkg.000008.animation</c>），
        /// 不是拿动画包身份与角色包六位 ID 直接比较。不一致即拒绝，不自动替换身份。
        /// </remarks>
        private static void RequireAnimationResourcePlanIdentity(
            CharacterAnimationResourcePlan resourcePlan,
            CharacterAnimationSetDefinition setDefinition)
        {
            if (resourcePlan == null
                || setDefinition == null
                || !string.Equals(resourcePlan.Format, AnimationResourcePlanFormatV2, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.Equals(resourcePlan.PackageId, setDefinition.PackageId, StringComparison.Ordinal)
                || !string.Equals(resourcePlan.StableId, setDefinition.StableId, StringComparison.Ordinal))
            {
                throw new CharacterImportedPackageJsonException(
                    "Animation resource plan v2 identity does not match the package animation set definition: plan '"
                    + resourcePlan.PackageId + "' / '" + resourcePlan.StableId
                    + "' vs set '" + setDefinition.PackageId + "' / '" + setDefinition.StableId + "'.");
            }
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
                FirstNonEmpty(ReadString(fields, "cueId"), ReadString(fields, "audioCueId")),
                ReadString(fields, "stableId"),
                ReadString(fields, "resourceKey"),
                ReadString(fields, "eventPath"),
                ReadString(fields, "bank"),
                ReadEnum(fields, "fallbackPolicy", CharacterResourceFailurePolicy.MuteMissingCue),
                ReadStringDictionary(fields["providerData"] as JObject),
                ReadInt(fields, "runtimeCueId"),
                ReadInt(fields, "runtimeEventId"),
                ReadString(fields, "eventDefinitionId"));
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
            CharacterAnimationBlendReference[] blend1D = ParseAnimationBlendReferences(
                fields["blend1D"] as JArray,
                CharacterAnimationBlendKind.Blend1D);
            CharacterAnimationBlendReference[] blend2D = ParseAnimationBlendReferences(
                fields["blend2D"] as JArray,
                CharacterAnimationBlendKind.Blend2D);
            var blends = new CharacterAnimationBlendReference[blend1D.Length + blend2D.Length];
            Array.Copy(blend1D, 0, blends, 0, blend1D.Length);
            Array.Copy(blend2D, 0, blends, blend1D.Length, blend2D.Length);
            return new CharacterAnimationGroup(
                ReadString(fields, "groupId"),
                ReadString(fields, "displayName"),
                ReadString(fields, "usage"),
                ParseArray(fields["clips"] as JArray, ParseAnimationClipReference),
                blends);
        }

        private static CharacterAnimationBlendReference[] ParseAnimationBlendReferences(
            JArray items,
            CharacterAnimationBlendKind blendKind)
        {
            if (items == null || items.Count == 0)
                return Array.Empty<CharacterAnimationBlendReference>();

            var results = new List<CharacterAnimationBlendReference>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                JObject blend = items[i] as JObject;
                if (blend == null)
                    continue;

                JArray points = blend["points"] as JArray;
                var clipIds = new List<string>(points != null ? points.Count : 0);
                for (int pointIndex = 0; points != null && pointIndex < points.Count; pointIndex++)
                {
                    string clipId = ReadString(points[pointIndex] as JObject, "clipId");
                    if (!string.IsNullOrWhiteSpace(clipId))
                        clipIds.Add(clipId);
                }

                results.Add(new CharacterAnimationBlendReference(
                    ReadString(blend, "blendId"),
                    blendKind,
                    ReadString(blend, "defaultClipId"),
                    clipIds.ToArray()));
            }

            return results.ToArray();
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
                ReadNumericIdentity(fields, "bindingId"),
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
                ReadString(fields, "defaultGroupId"),
                ParseArray(fields["slots"] as JArray, ParseAnimationProfileSlot));
        }

        private static CharacterAnimationProfileSlot ParseAnimationProfileSlot(JObject fields)
        {
            return new CharacterAnimationProfileSlot(
                ReadString(fields, "slotId"),
                ReadString(fields, "displayName"),
                ReadString(fields, "purpose"),
                ReadString(fields, "setId"),
                ReadString(fields, "groupId"),
                ReadString(fields, "defaultClipId"),
                ReadString(fields, "defaultBlendId"),
                ReadString(fields, "preloadPolicy"),
                ReadBool(fields, "required"));
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
                    ReadFloat(item, "MaxValue"))),
                fields["BaseCohesion"] == null ? 100 : ReadInt(fields, "BaseCohesion"),
                fields["ScatterRecoveryRate"] == null ? 2 : ReadInt(fields, "ScatterRecoveryRate"),
                fields["ScatterRecoveryDelayFrames"] == null ? 360 : ReadInt(fields, "ScatterRecoveryDelayFrames"),
                fields["CohesionBreakRecoveryDelayFrames"] == null ? 270 : ReadInt(fields, "CohesionBreakRecoveryDelayFrames"));
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
                    ReadNumericIdentity(item, "SocketId"),
                    ReadNumericIdentity(item, "ParentPartId"),
                    ReadString(item, "LocatorId"))),
                ParseArray(fields["HitZoneBindings"] as JArray, item => new CharacterHitZoneBindingEntry(
                    ReadNumericIdentity(item, "HitZoneId"),
                    ReadNumericIdentity(item, "PartId"),
                    ReadInt(item, "Priority"),
                    ReadBool(item, "IsWeakPoint"),
                    ReadFloat(item, "DamageMultiplierOverride"),
                    ReadFloat(item, "ImpactPowerScaleOverride"))));
        }

        private static CharacterBodyPartConfig ParseBodyPart(JObject fields)
        {
            return new CharacterBodyPartConfig(
                new CharacterBodyPartConfigId(ReadInt(fields, "BodyPartConfigId")),
                ReadString(fields, "StableId"),
                ReadString(fields, "PartSetId"),
                ReadNumericIdentity(fields, "PartId"),
                ReadNumericIdentity(fields, "ParentPartId"),
                ReadEnum(fields, "PartKind", CharacterBodyPartKind.Custom),
                ReadString(fields, "LocatorId"),
                ReadNumericIdentity(fields, "HitZoneId"),
                ReadNumericIdentity(fields, "ReactionGroupId"),
                ReadFloat(fields, "DamageMultiplier"),
                ReadFloat(fields, "ImpulseScale"),
                ReadFloat(fields, "StaggerScale"),
                ReadFloat(fields, "ImpactPowerScale"),
                ReadBool(fields, "IsCritical"));
        }

        private static CharacterBodyPartContactPointConfig ParseBodyPartContactPoint(JObject fields)
        {
            int id = ReadInt(fields, "ContactPointConfigId");
            if (id == 0)
                id = ReadInt(fields, "Id");
            return new CharacterBodyPartContactPointConfig(
                new CharacterBodyPartContactPointConfigId(id),
                ReadString(fields, "StableId"),
                ReadString(fields, "PartSetId"),
                ReadNumericIdentity(fields, "PartId"),
                ReadInt(fields, "ContactPointId"),
                ReadInt(fields, "BodyPartId"),
                ReadInt(fields, "LocalOffsetXMillimeters"),
                ReadInt(fields, "LocalOffsetYMillimeters"),
                ReadInt(fields, "LocalOffsetZMillimeters"),
                ReadInt(fields, "RadiusMillimeters"),
                ReadString(fields, "LayerMaskProfileId"),
                ReadEnum(fields, "ActivationMode", CharacterBodyPartContactActivationMode.Disabled),
                ReadEnum(fields, "MotionMode", CharacterBodyPartContactMotionMode.Static),
                ReadNumericIdentity(fields, "MotionSocketId"),
                ReadString(fields, "Usage"),
                ReadString(fields, "Authority"),
                ReadInt(fields, "SourceOrder"));
        }

        private static EquipmentSchemaConfig ParseEquipmentSchema(JObject fields)
        {
            return new EquipmentSchemaConfig(
                new EquipmentSchemaId(ReadInt(fields, "EquipmentSchemaId")),
                ReadString(fields, "StableId"),
                ParseArray(fields["Slots"] as JArray, item => new EquipmentSlotEntry(
                    ReadNumericIdentity(item, "SlotId"),
                    ReadEnum(item, "Kind", EquipmentSlotKind.Custom),
                    ReadString(item, "DisplayName"),
                    ReadStringArray(item["AllowedWeaponCategories"] as JArray),
                    ReadStringArray(item["RequiredTags"] as JArray))),
                ParseArray(fields["ExclusiveGroups"] as JArray, item => new EquipmentExclusiveGroupEntry(
                    ReadString(item, "GroupId"),
                    ReadNumericIdentities(item["SlotIds"] as JArray),
                    ReadInt(item, "MaxFilledCount"))),
                ParseIdArray(fields["AllowedStateIds"] as JArray, value => new EquipmentStateId(value)));
        }

        private static EquipmentLoadoutConfig ParseLoadout(JObject fields)
        {
            return new EquipmentLoadoutConfig(
                new EquipmentLoadoutId(ReadInt(fields, "LoadoutId")),
                ReadString(fields, "StableId"),
                new EquipmentSchemaId(ReadInt(fields, "EquipmentSchemaId")),
                new EquipmentStateId(ReadInt(fields, "EquipmentStateId")),
                ParseArray(fields["Slots"] as JArray, item => new EquipmentLoadoutSlotEntry(
                    ReadNumericIdentity(item, "SlotId"),
                    new WeaponConfigId(ReadInt(item, "WeaponId")),
                    ReadString(item, "WeaponInstanceStableId"),
                    ReadString(item, "AttachmentId"))));
        }

        private static EquipmentStateConfig ParseEquipmentState(JObject fields)
        {
            return new EquipmentStateConfig(
                new EquipmentStateId(ReadInt(fields, "StateId")),
                ReadString(fields, "StableId"),
                new EquipmentSchemaId(ReadInt(fields, "EquipmentSchemaId")),
                ReadInt(fields, "Priority"),
                ParseArray(fields["MatchRules"] as JArray, item => new EquipmentMatchRule(
                    ReadNumericIdentities(item["RequiredFilledSlots"] as JArray),
                    ReadNumericIdentities(item["RequiredEmptySlots"] as JArray),
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
                ReadNumericIdentities(fields["OccupiesSlots"] as JArray),
                new AbilityLoadoutId(ReadInt(fields, "GrantedAbilityLoadoutId")),
                ReadString(fields, "WeaponPresentationProfileId"),
                ReadString(fields, "WeaponCombatProfileId"),
                ReadString(fields, "WeaponResourceProfileId"),
                ParseArray(fields["ResourceKeys"] as JArray, ParseResourceKeyEntry),
                ParseArray(fields["TraceBindings"] as JArray, item => new CharacterTraceBindingEntry(
                    ReadString(item, "TraceProfileId"),
                    ReadNumericIdentity(item, "SlotId"),
                    ReadNumericIdentity(item, "SocketId"),
                    ReadFloat(item, "Length"),
                    ReadFloat(item, "Radius"),
                    ReadFloat(item, "ProximalExtension"),
                    ReadFloat(item, "ProximalRadius"))),
                ParseArray(fields["BaseModifiers"] as JArray, item => new CharacterModifierGrantEntry(
                    ReadString(item, "ModifierStableId"),
                    ReadFloat(item, "Value"),
                    ReadString(item, "Source"))),
                ReadString(fields, "BladeTrailResourceKey"),
                fields["BladeTrailResidueMilliseconds"] != null ? ReadInt(fields, "BladeTrailResidueMilliseconds") : 100,
                ReadInt(fields, "BladeTrailStyleId"),
                ReadString(fields, "WeaponTypeId"));
        }

        private static AbilityLoadoutConfig ParseAbilityLoadout(JObject fields)
        {
            return new AbilityLoadoutConfig(
                new AbilityLoadoutId(ReadInt(fields, "LoadoutId")),
                ReadString(fields, "StableId"),
                ParseIdArray(fields["AbilityIds"] as JArray, value => new CharacterAbilityId(value)),
                ParseIdArray(fields["RemoveAbilityIds"] as JArray, value => new CharacterAbilityId(value)),
                ParseArray(fields["SlotBindings"] as JArray, item => new AbilitySlotBinding(
                    ReadNumericIdentity(item, "SlotId"),
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
                ReadStringArray(fields["PresentationTags"] as JArray),
                ParseGameplayCameraProfile(fields["GameplayCameraProfile"] as JObject),
                ParsePresentationImages(fields["PresentationImages"] as JObject));
        }

        private static CharacterPresentationImages ParsePresentationImages(JObject fields)
        {
            if (fields == null) return null;
            var images = fields.ToObject<CharacterPresentationImages>();
            if (images.SchemaVersion != "1.0")
                throw new CharacterImportedPackageJsonException("Unsupported presentation image schema.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; images.Variants != null && i < images.Variants.Count; i++)
                if (images.Variants[i] == null || string.IsNullOrWhiteSpace(images.Variants[i].VariantId) || !ids.Add(images.Variants[i].VariantId))
                    throw new CharacterImportedPackageJsonException("Presentation image variants require unique stable identities.");
            return images;
        }

        private static CharacterGameplayCameraProfile ParseGameplayCameraProfile(JObject fields)
        {
            if (fields == null)
                return null;

            JObject normal = fields["Normal"] as JObject;
            JObject locked = fields["Locked"] as JObject;
            JObject obstruction = fields["Obstruction"] as JObject;
            return new CharacterGameplayCameraProfile(
                ReadString(fields, "ProfileId"),
                ReadString(fields, "FocusAbstractBoneId"),
                ParseGameplayCameraVector(fields["FocusOffset"] as JObject),
                ParseGameplayCameraVector(fields["FramingBoundsCenter"] as JObject),
                ParseGameplayCameraVector(fields["FramingBoundsExtents"] as JObject),
                new CharacterGameplayCameraNormalSettings(
                    ReadFloat(normal, "Distance"),
                    ReadFloat(normal, "MinDistance"),
                    ReadFloat(normal, "MaxDistance"),
                    ReadFloat(normal, "FieldOfView"),
                    ReadFloat(normal, "Pitch"),
                    ReadFloat(normal, "YawOffset"),
                    ReadFloat(normal, "TargetPadding")),
                new CharacterGameplayCameraLockedSettings(
                    ReadFloat(locked, "MinDistance"),
                    ReadFloat(locked, "MaxDistance"),
                    ReadFloat(locked, "TargetScreenY"),
                    ReadFloat(locked, "TargetPadding"),
                    ReadFloat(locked, "FramingScale", 1f)),
                new CharacterGameplayCameraObstructionSettings(
                    ReadFloat(obstruction, "Radius"),
                    ReadFloat(obstruction, "Padding"),
                    ReadFloat(obstruction, "MinDistance"),
                    ReadFloat(obstruction, "FocusClearance")));
        }

        private static CharacterGameplayCameraVector3 ParseGameplayCameraVector(JObject fields)
        {
            return new CharacterGameplayCameraVector3(
                ReadFloat(fields, "X"),
                ReadFloat(fields, "Y"),
                ReadFloat(fields, "Z"));
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
                ReadNumericIdentity(fields, "colliderId"),
                ReadNumericIdentity(fields, "partId"),
                ReadNumericIdentity(fields, "hitZoneId"),
                ReadString(fields, "shape"),
                ReadPose(fields["localPose"] as JObject),
                ReadVector3(fields["size"] as JObject),
                ReadFloat(fields, "radius"),
                ReadFloat(fields, "height"),
                ReadInt(fields, "priority"),
                ReadBool(fields, "isWeakPoint"),
                ReadString(fields, "physicsLayer"),
                ReadFloat(fields, "damageMultiplierOverride"),
                ReadFloat(fields, "impactPowerScaleOverride"),
                ReadString(fields, "materialStableId"),
                ReadString(fields, "partLabel"),
                ReadString(fields, "hitZoneLabel"),
                ReadString(fields, "colliderLabel"));
        }

        private static CharacterHitZoneRuntimeBinding ParseHitZoneBinding(JObject fields)
        {
            return new CharacterHitZoneRuntimeBinding(
                ReadNumericIdentity(fields, "hitZoneId"),
                ReadNumericIdentity(fields, "partId"),
                ReadInt(fields, "priority"),
                ReadBool(fields, "isWeakPoint"),
                ReadFloat(fields, "damageMultiplierOverride"),
                ReadFloat(fields, "impactPowerScaleOverride"));
        }

        private static CharacterDamageZoneRuntimeBinding ParseDamageZoneBinding(JObject fields)
        {
            return new CharacterDamageZoneRuntimeBinding(
                ReadNumericIdentity(fields, "zoneId"),
                ReadString(fields, "displayName"),
                ReadNumericIdentities(fields?["hitZoneIds"] as JArray),
                ReadBool(fields, "hittable"),
                ReadBool(fields, "aimable"),
                ReadBool(fields, "approachable"),
                ReadInt(fields, "authoredPriority"));
        }

        private static CharacterEngagementAnchorRuntimeBinding ParseEngagementAnchorBinding(
            JObject fields)
        {
            string referenceFrame = ReadString(fields, "referenceFrame");
            if (!string.Equals(referenceFrame, "ModelRoot", StringComparison.Ordinal))
            {
                throw new CharacterImportedPackageJsonException(
                    "CHARANCH101_INVALID_BINDING: EngagementAnchor referenceFrame must be ModelRoot.");
            }
            JObject position = fields?["localPositionMillimeters"] as JObject;
            if (position == null)
            {
                throw new CharacterImportedPackageJsonException(
                    "CHARANCH101_INVALID_BINDING: EngagementAnchor localPositionMillimeters is required.");
            }
            return new CharacterEngagementAnchorRuntimeBinding(
                ReadNumericIdentity(fields, "anchorId"),
                ReadNumericIdentity(fields, "zoneId"),
                CharacterEngagementAnchorReferenceFrame.ModelRoot,
                new CharacterRuntimeIntVector3(
                    ReadInt(position, "x"),
                    ReadInt(position, "y"),
                    ReadInt(position, "z")),
                ReadInt(fields, "yawMilliDegrees"),
                ReadInt(fields, "capacity"),
                ReadInt(fields, "authoredPriority"),
                ReadBool(fields, "enabled"),
                ReadNumericIdentity(fields, "phaseId"),
                ReadString(fields, "anchorLabel"), ReadString(fields, "zoneLabel"), ReadString(fields, "phaseLabel"));
        }

        private static void ParseBodyPartDamageBindings(
            JObject root,
            CharacterDamageZoneRuntimeBinding[] damageZones,
            out CharacterBodyPartDamageRuntimeBinding[] definitions,
            out CharacterBodyPartDamageLookupRuntimeBinding[] partLookup,
            out string format,
            out string schemaVersion,
            out int revision,
            out string contentHash)
        {
            if (!string.Equals(ReadBodyPartDamageString(root, "bodyPartDamageMode"), "Authored", StringComparison.Ordinal))
                throw BodyPartDamageInvalid("bodyPartDamageMode must be Authored.");

            format = ReadBodyPartDamageString(root, "bodyPartDamageFormat");
            schemaVersion = ReadBodyPartDamageString(root, "bodyPartDamageSchemaVersion");
            revision = ReadBodyPartDamageInt(root, "bodyPartDamageRevision");
            contentHash = ReadBodyPartDamageString(root, "bodyPartDamageContentHash");
            if (!string.Equals(format, "mx.characterBodyPartDamage.v3", StringComparison.Ordinal)
                || !string.Equals(schemaVersion, "3.0", StringComparison.Ordinal)
                || revision < 1
                || !IsSha256(contentHash))
                throw BodyPartDamageInvalid("source identity is invalid.");

            JArray definitionSource = ReadBodyPartDamageArray(root, "bodyPartDamageDefinitions");
            JArray lookupSource = ReadBodyPartDamageArray(root, "bodyPartDamagePartLookup");
            if (definitionSource.Count < 1 || definitionSource.Count > 8)
                throw BodyPartDamageInvalid("definition capacity must be in [1,8].");

            var knownZones = new HashSet<int>();
            for (int i = 0; i < damageZones.Length; i++)
            {
                int zoneId = damageZones[i]?.ZoneId ?? 0;
                if (zoneId > 0)
                    knownZones.Add(zoneId);
            }

            definitions = new CharacterBodyPartDamageRuntimeBinding[definitionSource.Count];
            var expectedLookup = new Dictionary<int, int>();
            var presentationEventIds = new HashSet<string>(StringComparer.Ordinal);
            var knownPartStates = new HashSet<int>();
            bool? usesSeveranceExtension = null;
            for (int i = 0; i < definitionSource.Count; i++)
            {
                if (!(definitionSource[i] is JObject item))
                    throw BodyPartDamageInvalid("every definition must be an object.");
                int definitionIndex = ReadBodyPartDamageInt(item, "definitionIndex");
                int partStateId = ReadNumericIdentity(item, "partStateId");
                int damageZoneId = ReadNumericIdentity(item, "damageZoneId");
                int woundCapacity = ReadBodyPartDamageInt(item, "woundCapacity");
                int openDamageScaleMilli = ReadBodyPartDamageInt(item, "openDamageScaleMilli");
                string focusConsumeTag = ReadBodyPartDamageString(item, "focusConsumeTag");
                int maxIntegrity = ReadBodyPartDamageInt(item, "maxIntegrity");
                int integrityDamageScaleMilli = ReadBodyPartDamageInt(item, "integrityDamageScaleMilli");
                string breakReactionId = ReadBodyPartDamageString(item, "breakReactionId");
                int priorityDeltaOpen = ReadBodyPartDamageInt(item, "authoredPriorityDeltaOpen");
                int priorityDeltaBroken = ReadBodyPartDamageInt(item, "authoredPriorityDeltaBroken");
                bool hasSeveranceExtension = HasBodyPartDamageSeveranceExtension(item);
                if (usesSeveranceExtension.HasValue
                    && usesSeveranceExtension.Value != hasSeveranceExtension)
                {
                    throw BodyPartDamageInvalid("severance extension fields must be present on every definition or none.");
                }
                usesSeveranceExtension = hasSeveranceExtension;
                ReadBodyPartDamageSeveranceExtension(
                    item,
                    hasSeveranceExtension,
                    out bool severable,
                    out string severRequiredTag,
                    out string presentationEventId);
                JArray memberSource = ReadBodyPartDamageArray(item, "memberPartIds");
                if (definitionIndex != i
                    || !IsStableBodyPartDamageId(partStateId)
                    || !knownPartStates.Add(partStateId)
                    || memberSource.Count == 0
                    || (damageZoneId > 0 && !knownZones.Contains(damageZoneId))
                    || woundCapacity < 0 || woundCapacity > 1000000
                    || openDamageScaleMilli < 0 || openDamageScaleMilli > 4000
                    || (woundCapacity > 0 && openDamageScaleMilli == 0)
                    || maxIntegrity < 0 || maxIntegrity > 1000000
                    || integrityDamageScaleMilli < 0 || integrityDamageScaleMilli > 4000
                    || (!string.IsNullOrEmpty(focusConsumeTag)
                        && (woundCapacity == 0 || !IsExplicitBodyPartFocusConsumeTag(focusConsumeTag)))
                    || (!string.IsNullOrEmpty(breakReactionId)
                        && (maxIntegrity == 0 || !IsStableBodyPartDamageId(breakReactionId)))
                    || (!severable
                        && (!string.IsNullOrEmpty(severRequiredTag)
                            || !string.IsNullOrEmpty(presentationEventId)))
                    || (severable
                        && (maxIntegrity == 0
                            || !IsExplicitBodyPartFocusConsumeTag(severRequiredTag)
                            || !IsStableBodyPartDamageId(presentationEventId)
                            || !presentationEventId.StartsWith("event.", StringComparison.Ordinal)
                            || !presentationEventIds.Add(presentationEventId)))
                    || priorityDeltaOpen < -1000 || priorityDeltaOpen > 1000
                    || priorityDeltaBroken < -1000 || priorityDeltaBroken > 1000)
                    throw BodyPartDamageInvalid("definition fields are invalid or unordered.");

                var memberPartIds = new int[memberSource.Count];
                int previousPartId = 0;
                for (int memberIndex = 0; memberIndex < memberSource.Count; memberIndex++)
                {
                    JToken memberToken = memberSource[memberIndex];
                    if (memberToken == null || memberToken.Type != JTokenType.Integer)
                        throw BodyPartDamageInvalid("memberPartIds must contain numeric identities.");
                    int partId = memberToken.Value<int>();
                    if (!IsStableBodyPartDamageId(partId)
                        || !expectedLookup.TryAdd(partId, i))
                        throw BodyPartDamageInvalid("memberPartIds must be unique and ordinally sorted.");
                    memberPartIds[memberIndex] = partId;
                    previousPartId = partId;
                }
                definitions[i] = new CharacterBodyPartDamageRuntimeBinding(
                    definitionIndex,
                    partStateId,
                    memberPartIds,
                    damageZoneId,
                    woundCapacity,
                    openDamageScaleMilli,
                    focusConsumeTag,
                    maxIntegrity,
                    integrityDamageScaleMilli,
                    breakReactionId,
                    priorityDeltaOpen,
                    priorityDeltaBroken,
                    severable,
                    severRequiredTag,
                    presentationEventId, ReadString(item, "partStateLabel"),
                    ReadString(item, "damageZoneLabel"), ReadStringArray(item["memberPartLabels"] as JArray));
            }

            if (lookupSource.Count != expectedLookup.Count)
                throw BodyPartDamageInvalid("part lookup count does not match member ownership.");
            partLookup = new CharacterBodyPartDamageLookupRuntimeBinding[lookupSource.Count];
            int previousLookupPartId = 0;
            for (int i = 0; i < lookupSource.Count; i++)
            {
                if (!(lookupSource[i] is JObject item))
                    throw BodyPartDamageInvalid("every part lookup must be an object.");
                int partId = ReadNumericIdentity(item, "partId");
                int definitionIndex = ReadBodyPartDamageInt(item, "definitionIndex");
                if (!expectedLookup.TryGetValue(partId, out int expectedDefinitionIndex)
                    || definitionIndex != expectedDefinitionIndex
                    || (i > 0 && (previousLookupPartId).CompareTo(partId) >= 0))
                    throw BodyPartDamageInvalid("part lookup is invalid or unordered.");
                partLookup[i] = new CharacterBodyPartDamageLookupRuntimeBinding(partId, definitionIndex);
                previousLookupPartId = partId;
            }

            if (!string.Equals(
                    contentHash,
                    ComputeBodyPartDamageContentHash(
                        "mx.characterBodyPartDamage.v" + ReadInt(root, "bodyPartDamageContentEncodingVersion"),
                        ReadInt(root, "bodyPartDamageContentEncodingVersion") + ".0",
                        revision,
                        definitions,
                        usesSeveranceExtension == true),
                    StringComparison.Ordinal))
            {
                throw new CharacterImportedPackageJsonException(
                    "CHARBPD102_CONTENT_HASH_MISMATCH: Body-part damage content hash does not match the canonical binding.");
            }
        }

        private static string ComputeBodyPartDamageContentHash(
            string format,
            string schemaVersion,
            int revision,
            CharacterBodyPartDamageRuntimeBinding[] definitions,
            bool includeSeveranceExtension)
        {
            var builder = new StringBuilder();
            builder.Append("format|").Append(format).Append('\n')
                .Append("schemaVersion|").Append(schemaVersion).Append('\n')
                .Append("revision|").Append(revision).Append('\n');
            for (int i = 0; i < definitions.Length; i++)
            {
                CharacterBodyPartDamageRuntimeBinding definition = definitions[i];
                builder.Append("partState|").Append(definition.PartStateLabel).Append('|')
                    .Append(definition.DamageZoneLabel).Append('|')
                    .Append(definition.WoundCapacity).Append('|')
                    .Append(definition.OpenDamageScaleMilli).Append('|')
                    .Append(definition.FocusConsumeTag).Append('|')
                    .Append(definition.MaxIntegrity).Append('|')
                    .Append(definition.IntegrityDamageScaleMilli).Append('|')
                    .Append(definition.BreakReactionId).Append('|')
                    .Append(definition.AuthoredPriorityDeltaOpen).Append('|')
                    .Append(definition.AuthoredPriorityDeltaBroken);
                if (includeSeveranceExtension)
                {
                    builder.Append('|').Append(definition.Severable).Append('|')
                        .Append(definition.SeverRequiredTag).Append('|')
                        .Append(definition.PresentationEventId);
                }
                builder.Append('\n');
                for (int memberIndex = 0; memberIndex < definition.MemberPartLabels.Length; memberIndex++)
                    builder.Append("member|").Append(definition.MemberPartLabels[memberIndex]).Append('\n');
            }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                var result = new StringBuilder(hash.Length * 2 + 7).Append("sha256:");
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static string ReadBodyPartDamageString(JObject obj, string name)
        {
            JToken token = obj?[name];
            if (token == null || token.Type != JTokenType.String)
                throw BodyPartDamageInvalid(name + " must be a string.");
            return token.Value<string>() ?? string.Empty;
        }

        private static bool HasBodyPartDamageSeveranceExtension(JObject obj)
        {
            int present = 0;
            if (obj?["severable"] != null) present++;
            if (obj?["severRequiredTag"] != null) present++;
            if (obj?["presentationEventId"] != null) present++;
            if (present != 0 && present != 3)
                throw BodyPartDamageInvalid("severance extension must contain severable, severRequiredTag, and presentationEventId together.");
            return present == 3;
        }

        private static void ReadBodyPartDamageSeveranceExtension(
            JObject obj,
            bool present,
            out bool severable,
            out string severRequiredTag,
            out string presentationEventId)
        {
            if (!present)
            {
                severable = false;
                severRequiredTag = string.Empty;
                presentationEventId = string.Empty;
                return;
            }

            JToken severableToken = obj?["severable"];
            if (severableToken == null || severableToken.Type != JTokenType.Boolean)
                throw BodyPartDamageInvalid("severable must be a boolean.");
            severable = severableToken.Value<bool>();
            severRequiredTag = ReadBodyPartDamageString(obj, "severRequiredTag");
            presentationEventId = ReadBodyPartDamageString(obj, "presentationEventId");
        }

        private static int ReadBodyPartDamageInt(JObject obj, string name)
        {
            JToken token = obj?[name];
            if (token == null || token.Type != JTokenType.Integer)
                throw BodyPartDamageInvalid(name + " must be an integer.");
            long value;
            try
            {
                value = token.Value<long>();
            }
            catch (Exception exception) when (exception is InvalidCastException || exception is OverflowException)
            {
                throw BodyPartDamageInvalid(name + " is outside the Int32 range.");
            }
            if (value < int.MinValue || value > int.MaxValue)
                throw BodyPartDamageInvalid(name + " is outside the Int32 range.");
            return (int)value;
        }

        private static JArray ReadBodyPartDamageArray(JObject obj, string name)
        {
            if (!(obj?[name] is JArray value))
                throw BodyPartDamageInvalid(name + " must be an array.");
            return value;
        }

        private static bool IsStableBodyPartDamageId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLower(c) && !char.IsDigit(c) && c != '.' && c != '_' && c != '-')
                    return false;
            }
            return true;
        }

        private static bool IsExplicitBodyPartFocusConsumeTag(string value)
        {
            return IsStableBodyPartDamageId(value)
                && (value.StartsWith("action.", StringComparison.Ordinal)
                    || value.StartsWith("ability.", StringComparison.Ordinal)
                    || value.StartsWith("interaction.", StringComparison.Ordinal));
        }

        private static CharacterImportedPackageJsonException BodyPartDamageInvalid(string detail)
        {
            return new CharacterImportedPackageJsonException(
                "CHARBPD101_INVALID_BINDING: " + detail);
        }

        private static CharacterBodyPresenceRuntimeBinding[] ParseBodyPresenceBindings(
            JObject root,
            out int revision,
            out string contentHash,
            bool verifyContentHash = true)
        {
            string mode = ReadRequiredString(root, "bodyPresenceMode");
            if (!string.Equals(mode, "Authored", StringComparison.Ordinal))
                throw BodyPresenceInvalid("bodyPresenceMode must be Authored.");

            revision = ReadRequiredInt(root, "bodyPresenceRevision");
            contentHash = ReadRequiredString(root, "bodyPresenceContentHash");
            JArray source = ReadRequiredArray(root, "bodyPresenceVolumes");
            if (revision < 1 || !IsSha256(contentHash)
                || source.Count > 4)
                throw BodyPresenceInvalid("revision, content hash, or volume capacity is invalid.");

            var volumes = new CharacterBodyPresenceRuntimeBinding[source.Count];
            string previousVolumeId = string.Empty;
            for (int i = 0; i < source.Count; i++)
            {
                if (!(source[i] is JObject item))
                    throw BodyPresenceInvalid("every volume must be an object.");
                CharacterBodyPresenceRuntimeBinding volume = ParseBodyPresenceVolume(item);
                if (i > 0 && string.CompareOrdinal(previousVolumeId, volume.VolumeId) >= 0)
                    throw BodyPresenceInvalid("volume identities must be unique and ordinally sorted.");
                previousVolumeId = volume.VolumeId;
                volumes[i] = volume;
            }

            string computedHash = ComputeBodyPresenceContentHash(revision, volumes);
            if (verifyContentHash && !string.Equals(contentHash, computedHash, StringComparison.Ordinal))
            {
                throw new CharacterImportedPackageJsonException(
                    "CHARPRES102_CONTENT_HASH_MISMATCH: Body Presence content hash does not match the canonical binding.");
            }
            return volumes;
        }

        private static CharacterBodyPresenceRuntimeBinding ParseBodyPresenceVolume(JObject fields)
        {
            string volumeId = ReadRequiredString(fields, "volumeId");
            if (!IsStableBodyPresenceId(volumeId))
                throw BodyPresenceInvalid("volumeId is invalid.");

            if (!string.Equals(
                    ReadRequiredString(fields, "referenceFrame"),
                    "ModelRoot",
                    StringComparison.Ordinal))
                throw BodyPresenceInvalid("referenceFrame must be ModelRoot.");

            CharacterBodyPresenceShape shape = ReadRequiredString(fields, "shape") switch
            {
                "Sphere" => CharacterBodyPresenceShape.Sphere,
                "Capsule" => CharacterBodyPresenceShape.Capsule,
                "Box" => CharacterBodyPresenceShape.Box,
                _ => throw BodyPresenceInvalid("shape must be Sphere, Capsule, or Box.")
            };

            JObject poseObject = ReadRequiredObject(fields, "localPose");
            CharacterRuntimeVector3 position = ReadRequiredPresenceVector(
                ReadRequiredObject(poseObject, "position"));
            CharacterRuntimeQuaternion rotation = ReadRequiredPresenceQuaternion(
                ReadRequiredObject(poseObject, "rotation"));
            CharacterRuntimeVector3 scale = ReadRequiredPresenceVector(
                ReadRequiredObject(poseObject, "scale"));
            float radius = ReadRequiredFiniteFloat(fields, "radius");
            float height = ReadRequiredFiniteFloat(fields, "height");
            CharacterRuntimeVector3 size = ReadRequiredPresenceVector(
                ReadRequiredObject(fields, "size"));
            int skinMillimeters = ReadRequiredInt(fields, "skinMillimeters");
            int pressureScaleMilli = ReadRequiredInt(fields, "pressureScaleMilli");
            bool enabled = ReadRequiredBool(fields, "enabled");

            double normSquared = (double)rotation.X * rotation.X
                + (double)rotation.Y * rotation.Y
                + (double)rotation.Z * rotation.Z
                + (double)rotation.W * rotation.W;
            bool validPose = Math.Abs(position.X) <= 1000f
                && Math.Abs(position.Y) <= 1000f
                && Math.Abs(position.Z) <= 1000f
                && Math.Abs(normSquared - 1d) <= 0.001d
                && Math.Abs(scale.X - 1f) <= 0.0001f
                && Math.Abs(scale.Y - 1f) <= 0.0001f
                && Math.Abs(scale.Z - 1f) <= 0.0001f;
            bool boundedDimensions = radius >= 0f && radius <= 1000f
                && height >= 0f && height <= 1000f
                && size.X >= 0f && size.X <= 1000f
                && size.Y >= 0f && size.Y <= 1000f
                && size.Z >= 0f && size.Z <= 1000f;
            bool validDimensions = shape switch
            {
                CharacterBodyPresenceShape.Sphere => radius > 0f,
                CharacterBodyPresenceShape.Capsule => radius > 0f && height >= 2f * radius,
                CharacterBodyPresenceShape.Box => size.X > 0f && size.Y > 0f && size.Z > 0f,
                _ => false
            };
            if (!validPose || !boundedDimensions || !validDimensions
                || skinMillimeters < 0 || skinMillimeters > 10000
                || pressureScaleMilli < 0 || pressureScaleMilli > 100000)
                throw BodyPresenceInvalid("pose, dimensions, skin, or pressure scale is invalid.");

            return new CharacterBodyPresenceRuntimeBinding(
                volumeId,
                CharacterBodyPresenceReferenceFrame.ModelRoot,
                shape,
                new CharacterRuntimePose("ModelRoot", string.Empty, position, rotation, scale),
                radius,
                height,
                size,
                skinMillimeters,
                pressureScaleMilli,
                enabled);
        }

        private static CharacterRuntimeVector3 ReadRequiredPresenceVector(JObject value)
        {
            return new CharacterRuntimeVector3(
                ReadRequiredFiniteFloat(value, "x"),
                ReadRequiredFiniteFloat(value, "y"),
                ReadRequiredFiniteFloat(value, "z"));
        }

        private static CharacterRuntimeQuaternion ReadRequiredPresenceQuaternion(JObject value)
        {
            return new CharacterRuntimeQuaternion(
                ReadRequiredFiniteFloat(value, "x"),
                ReadRequiredFiniteFloat(value, "y"),
                ReadRequiredFiniteFloat(value, "z"),
                ReadRequiredFiniteFloat(value, "w"));
        }

        private static string ComputeBodyPresenceContentHash(
            int revision,
            CharacterBodyPresenceRuntimeBinding[] volumes)
        {
            var builder = new StringBuilder();
            builder.Append("{\"format\":\"mx.characterBodyPresence.v1\",\"schemaVersion\":\"1.0\",\"revision\":")
                .Append(revision).Append(",\"volumes\":[");
            for (int i = 0; i < volumes.Length; i++)
            {
                CharacterBodyPresenceRuntimeBinding volume = volumes[i];
                if (i > 0)
                    builder.Append(',');
                builder.Append("{\"volumeId\":\"").Append(volume.VolumeId)
                    .Append("\",\"referenceFrame\":\"").Append(volume.ReferenceFrame)
                    .Append("\",\"shape\":\"").Append(volume.Shape)
                    .Append("\",\"localPose\":{\"position\":");
                AppendCanonicalPresenceVector(builder, volume.LocalPose.Position);
                builder.Append(",\"rotation\":");
                AppendCanonicalPresenceQuaternion(builder, volume.LocalPose.Rotation);
                builder.Append(",\"scale\":");
                AppendCanonicalPresenceVector(builder, volume.LocalPose.Scale);
                builder.Append("},\"radius\":");
                AppendCanonicalPresenceFloat(builder, volume.Radius);
                builder.Append(",\"height\":");
                AppendCanonicalPresenceFloat(builder, volume.Height);
                builder.Append(",\"size\":");
                AppendCanonicalPresenceVector(builder, volume.Size);
                builder.Append(",\"skinMillimeters\":").Append(volume.SkinMillimeters)
                    .Append(",\"pressureScaleMilli\":").Append(volume.PressureScaleMilli)
                    .Append(",\"enabled\":").Append(volume.Enabled ? "true" : "false")
                    .Append('}');
            }
            builder.Append("]}");

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                var result = new StringBuilder(hash.Length * 2 + 7);
                result.Append("sha256:");
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static void AppendCanonicalPresenceVector(
            StringBuilder builder,
            CharacterRuntimeVector3 value)
        {
            builder.Append("{\"x\":");
            AppendCanonicalPresenceFloat(builder, value.X);
            builder.Append(",\"y\":");
            AppendCanonicalPresenceFloat(builder, value.Y);
            builder.Append(",\"z\":");
            AppendCanonicalPresenceFloat(builder, value.Z);
            builder.Append('}');
        }

        private static void AppendCanonicalPresenceQuaternion(
            StringBuilder builder,
            CharacterRuntimeQuaternion value)
        {
            builder.Append("{\"x\":");
            AppendCanonicalPresenceFloat(builder, value.X);
            builder.Append(",\"y\":");
            AppendCanonicalPresenceFloat(builder, value.Y);
            builder.Append(",\"z\":");
            AppendCanonicalPresenceFloat(builder, value.Z);
            builder.Append(",\"w\":");
            AppendCanonicalPresenceFloat(builder, value.W);
            builder.Append('}');
        }

        private static void AppendCanonicalPresenceFloat(StringBuilder builder, float value)
        {
            builder.Append((value == 0f ? 0f : value).ToString("R", CultureInfo.InvariantCulture));
        }

        private static string ReadRequiredString(JObject obj, string name)
        {
            JToken token = obj?[name];
            if (token == null || token.Type != JTokenType.String)
                throw BodyPresenceInvalid(name + " must be a string.");
            return token.Value<string>() ?? string.Empty;
        }

        private static int ReadRequiredInt(JObject obj, string name)
        {
            JToken token = obj?[name];
            if (token == null || token.Type != JTokenType.Integer)
                throw BodyPresenceInvalid(name + " must be an integer.");
            long value;
            try
            {
                value = token.Value<long>();
            }
            catch (Exception exception) when (exception is InvalidCastException || exception is OverflowException)
            {
                throw BodyPresenceInvalid(name + " is outside the Int32 range.");
            }
            if (value < int.MinValue || value > int.MaxValue)
                throw BodyPresenceInvalid(name + " is outside the Int32 range.");
            return (int)value;
        }

        private static float ReadRequiredFiniteFloat(JObject obj, string name)
        {
            JToken token = obj?[name];
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
                throw BodyPresenceInvalid(name + " must be a number.");
            double value;
            try
            {
                value = token.Value<double>();
            }
            catch (Exception exception) when (exception is InvalidCastException || exception is OverflowException)
            {
                throw BodyPresenceInvalid(name + " must be finite and within the Single range.");
            }
            if (double.IsNaN(value) || double.IsInfinity(value)
                || value < -float.MaxValue || value > float.MaxValue)
                throw BodyPresenceInvalid(name + " must be finite and within the Single range.");
            float result = (float)value;
            if (float.IsNaN(result) || float.IsInfinity(result))
                throw BodyPresenceInvalid(name + " must be finite and within the Single range.");
            return result;
        }

        private static bool ReadRequiredBool(JObject obj, string name)
        {
            JToken token = obj?[name];
            if (token == null || token.Type != JTokenType.Boolean)
                throw BodyPresenceInvalid(name + " must be a boolean.");
            return token.Value<bool>();
        }

        private static JObject ReadRequiredObject(JObject obj, string name)
        {
            if (!(obj?[name] is JObject value))
                throw BodyPresenceInvalid(name + " must be an object.");
            return value;
        }

        private static JArray ReadRequiredArray(JObject obj, string name)
        {
            if (!(obj?[name] is JArray value))
                throw BodyPresenceInvalid(name + " must be an array.");
            return value;
        }

        private static bool IsStableBodyPresenceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLower(c) && !char.IsDigit(c) && c != '.' && c != '_' && c != '-')
                    return false;
            }
            return true;
        }

        private static CharacterImportedPackageJsonException BodyPresenceInvalid(string detail)
        {
            return new CharacterImportedPackageJsonException(
                "CHARPRES101_INVALID_BINDING: " + detail);
        }

        private static void ValidateDamageZoneBindings(
            CharacterDamageZoneRuntimeBinding[] damageZones,
            CharacterHitZoneRuntimeBinding[] hitZoneBindings)
        {
            var knownHitZones = new HashSet<int>();
            for (int i = 0; i < hitZoneBindings.Length; i++)
            {
                int hitZoneId = hitZoneBindings[i]?.HitZoneId ?? 0;
                if ((hitZoneId > 0))
                    knownHitZones.Add(hitZoneId);
            }

            var zoneIds = new HashSet<int>();
            var ownedHitZones = new HashSet<int>();
            int previousZoneId = 0;
            for (int i = 0; i < damageZones.Length; i++)
            {
                CharacterDamageZoneRuntimeBinding zone = damageZones[i];
                if (zone == null || (zone.ZoneId <= 0)
                    || !zoneIds.Add(zone.ZoneId)
                    || (i > 0 && (previousZoneId).CompareTo(zone.ZoneId) >= 0)
                    || zone.HitZoneIds.Length == 0
                    || zone.AuthoredPriority < -100000 || zone.AuthoredPriority > 100000)
                {
                    throw new CharacterImportedPackageJsonException("CHARDZ101_INVALID_BINDING: invalid or unordered DamageZone identity.");
                }
                previousZoneId = zone.ZoneId;
                int previousHitZoneId = 0;
                for (int j = 0; j < zone.HitZoneIds.Length; j++)
                {
                    int hitZoneId = zone.HitZoneIds[j];
                    if (!knownHitZones.Contains(hitZoneId)
                        || !ownedHitZones.Add(hitZoneId)
                        || (j > 0 && (previousHitZoneId).CompareTo(hitZoneId) >= 0))
                    {
                        throw new CharacterImportedPackageJsonException("CHARDZ101_INVALID_BINDING: invalid, duplicate, or unordered HitZone membership.");
                    }
                    previousHitZoneId = hitZoneId;
                }
            }
        }

        private static void ValidateEngagementAnchorBindings(
            int revision,
            string contentHash,
            CharacterEngagementAnchorRuntimeBinding[] anchors,
            CharacterDamageZoneRuntimeBinding[] damageZones)
        {
            if (revision <= 0 || !IsSha256(contentHash))
            {
                throw new CharacterImportedPackageJsonException(
                    "CHARANCH101_INVALID_BINDING: EngagementAnchor revision or content hash is invalid.");
            }

            var zones = new Dictionary<int, CharacterDamageZoneRuntimeBinding>();
            for (int i = 0; i < damageZones.Length; i++)
            {
                CharacterDamageZoneRuntimeBinding zone = damageZones[i];
                if (zone != null && (zone.ZoneId > 0))
                    zones[zone.ZoneId] = zone;
            }

            int previousAnchorId = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                CharacterEngagementAnchorRuntimeBinding anchor = anchors[i];
                CharacterRuntimeIntVector3 position = anchor?.LocalPositionMillimeters ?? default;
                if (anchor == null
                    || !IsStableAnchorId(anchor.AnchorId)
                    || (i > 0 && (previousAnchorId).CompareTo(anchor.AnchorId) >= 0)
                    || !zones.TryGetValue(anchor.ZoneId, out CharacterDamageZoneRuntimeBinding zone)
                    || !zone.Approachable
                    || anchor.ReferenceFrame != CharacterEngagementAnchorReferenceFrame.ModelRoot
                    || position.X < -10000000 || position.X > 10000000
                    || position.Y < -10000000 || position.Y > 10000000
                    || position.Z < -10000000 || position.Z > 10000000
                    || anchor.YawMilliDegrees < 0 || anchor.YawMilliDegrees >= 360000
                    || anchor.Capacity < 1 || anchor.Capacity > 64
                    || anchor.AuthoredPriority < -100000 || anchor.AuthoredPriority > 100000
                    || ((anchor.PhaseId > 0) && !IsStableAnchorId(anchor.PhaseId)))
                {
                    throw new CharacterImportedPackageJsonException(
                        "CHARANCH101_INVALID_BINDING: invalid, unordered, or unbound EngagementAnchor.");
                }
                previousAnchorId = anchor.AnchorId;
            }

            string computedHash = ComputeEngagementAnchorContentHash(revision, anchors);
            if (!string.Equals(contentHash, computedHash, StringComparison.Ordinal))
            {
                throw new CharacterImportedPackageJsonException(
                    "CHARANCH102_CONTENT_HASH_MISMATCH: EngagementAnchor content hash does not match the canonical binding.");
            }
        }

        private static string ComputeEngagementAnchorContentHash(
            int revision,
            CharacterEngagementAnchorRuntimeBinding[] anchors)
        {
            var builder = new StringBuilder();
            builder.Append("revision|").Append(revision).Append('\n');
            for (int i = 0; i < anchors.Length; i++)
            {
                CharacterEngagementAnchorRuntimeBinding anchor = anchors[i];
                CharacterRuntimeIntVector3 position = anchor.LocalPositionMillimeters;
                builder.Append("engagementAnchor|").Append(anchor.AnchorId).Append('|')
                    .Append(anchor.ZoneId).Append('|').Append(anchor.ReferenceFrame).Append('|')
                    .Append(position.X).Append('|').Append(position.Y).Append('|')
                    .Append(position.Z).Append('|').Append(anchor.YawMilliDegrees).Append('|')
                    .Append(anchor.Capacity).Append('|').Append(anchor.AuthoredPriority).Append('|')
                    .Append(anchor.Enabled).Append('|').Append(anchor.PhaseId)
                    .Append('\n');
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                var result = new StringBuilder(hash.Length * 2 + 7);
                result.Append("sha256:");
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 71
                || !value.StartsWith("sha256:", StringComparison.Ordinal))
                return false;
            for (int i = 7; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsDigit(c) && (c < 'a' || c > 'f'))
                    return false;
            }
            return true;
        }

        private static bool IsStableAnchorId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLower(c) && !char.IsDigit(c) && c != '.' && c != '_' && c != '-')
                    return false;
            }
            return true;
        }

        private static CharacterSocketRuntimeBinding ParseSocket(JObject fields)
        {
            return new CharacterSocketRuntimeBinding(
                ReadNumericIdentity(fields, "socketId"),
                ReadString(fields, "abstractBoneId"),
                ReadString(fields, "parentPartId"),
                ReadString(fields, "bonePath"),
                ReadString(fields, "locatorPath"),
                ReadPose(fields["localPose"] as JObject),
                ReadString(fields, "usage"),
                socketLabel: ReadString(fields, "socketLabel"));
        }

        private static CharacterWeaponAttachmentRuntimeBinding ParseWeaponAttachment(JObject fields)
        {
            JObject weaponRef = fields?["weaponRef"] as JObject;
            string sourceProviderId = ReadString(weaponRef, "sourceProviderId");
            string resourcePackageId = ReadString(weaponRef, "packageId");
            if (string.IsNullOrWhiteSpace(resourcePackageId)
                && string.Equals(sourceProviderId, "resourceMapping", StringComparison.Ordinal))
            {
                resourcePackageId = "common";
            }

            return new CharacterWeaponAttachmentRuntimeBinding(
                ReadNumericIdentity(fields, "weaponId"),
                ReadNumericIdentity(fields, "equipSlot"),
                ReadNumericIdentity(fields, "attachSocketId"),
                ReadPose(fields["localGripPose"] as JObject),
                ReadPose(fields["slotPose"] as JObject),
                ReadPose(fields["weaponModelPose"] as JObject),
                ReadString(fields, "previewResourceKey"),
                ReadNumericIdentity(fields, "traceId"),
                ReadString(weaponRef, "runtimeResourceKey"),
                resourcePackageId,
                sourceProviderId,
                ReadNumericIdentity(fields, "traceStartSocketId"),
                ReadNumericIdentity(fields, "traceEndSocketId"),
                ReadFloat(fields, "traceRadius"),
                ReadString(fields, "traceSampleRule"),
                ReadString(fields, "attachmentId"),
                attachSocketLabel: ReadString(fields, "attachSocketLabel"),
                weaponLabel: ReadString(fields, "weaponLabel"), equipmentSlotLabel: ReadString(fields, "equipmentSlotLabel"), traceLabel: ReadString(fields, "traceLabel"));
        }

        private static CharacterWeaponTraceRuntimeBinding ParseWeaponTrace(JObject fields)
        {
            return new CharacterWeaponTraceRuntimeBinding(
                ReadNumericIdentity(fields, "traceId"),
                ReadNumericIdentity(fields, "weaponId"),
                ReadNumericIdentity(fields, "equipSlot"),
                ReadString(fields, "startLocatorPath"),
                ReadString(fields, "endLocatorPath"),
                ReadFloat(fields, "radius"),
                ReadString(fields, "sampleRule"),
                ReadInt(fields, "fixedSampleCount"),
                ReadStringArray(fields["actionKeys"] as JArray),
                ReadPose(fields["startPose"] as JObject),
                ReadPose(fields["endPose"] as JObject),
                ReadFloat(fields, "proximalExtension"),
                ReadFloat(fields, "proximalRadius"),
                socketId: ReadNumericIdentity(fields, "socketId"),
                socketLabel: ReadString(fields, "socketLabel"),
                weaponLabel: ReadString(fields, "weaponLabel"), equipmentSlotLabel: ReadString(fields, "equipmentSlotLabel"), traceLabel: ReadString(fields, "traceLabel"));
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
