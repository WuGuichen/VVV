using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring.Cli;

internal interface IResourceMappingFirstPartyProducerAdapter
{
    string ProducerId { get; }
    ResourceMappingProducerSnapshotResult BuildSnapshot(string repositoryRoot, ResourceMappingProfileV2 priorProfile = null);
}

internal sealed class ResourceMappingProducerSnapshotResult
{
    public bool Success { get; set; }
    public ResourceMappingProducerSnapshot Snapshot { get; set; }
    public List<ResourceMappingDiagnostic> Diagnostics { get; } = new();
}

/// <summary>Enumerates a domain-owned repository surface and derives a complete snapshot from path conventions and Unity metadata.</summary>
internal abstract class ResourceMappingRepositoryProducerAdapter : IResourceMappingFirstPartyProducerAdapter
{
    public abstract string ProducerId { get; }
    protected abstract IEnumerable<string> EnumerateAssetPaths(string repositoryRoot);
    protected abstract ResourceMappingDerivedAsset Describe(string assetPath);
    protected abstract IReadOnlyList<string> RequiredDirectories { get; }
    protected virtual IReadOnlyList<string> RequiredFiles => Array.Empty<string>();

    protected virtual IEnumerable<ResourceMappingDerivedAsset> EnumerateAssets(string repositoryRoot)
    {
        foreach (string path in EnumerateAssetPaths(repositoryRoot))
        {
            ResourceMappingDerivedAsset asset = Describe(path);
            if (asset != null)
                yield return asset;
        }
    }

    /// <remarks>AllocByDesign. Low-frequency authoring synchronization.</remarks>
    public ResourceMappingProducerSnapshotResult BuildSnapshot(string repositoryRoot, ResourceMappingProfileV2 priorProfile = null)
    {
        string root = Path.GetFullPath(repositoryRoot ?? string.Empty);
        var result = new ResourceMappingProducerSnapshotResult
        {
            Snapshot = new ResourceMappingProducerSnapshot { ProducerId = ProducerId }
        };
        for (int i = 0; i < RequiredDirectories.Count; i++)
        {
            string required = RequiredDirectories[i];
            if (!Directory.Exists(Path.Combine(root, required.Replace('/', Path.DirectorySeparatorChar))))
                result.Diagnostics.Add(Error("ProducerSourceRootMissing", "Required producer source root is missing: " + required + ".", required));
        }
        for (int i = 0; i < RequiredFiles.Count; i++)
        {
            string required = RequiredFiles[i];
            if (!File.Exists(Path.Combine(root, required.Replace('/', Path.DirectorySeparatorChar))))
                result.Diagnostics.Add(Error("ProducerSourceMissing", "Required producer source is missing: " + required + ".", required));
        }
        if (result.Diagnostics.Count > 0)
            return result;
        Dictionary<string, ResourceMappingEntry> priorByGuid = ReadPriorEntriesByGuid(priorProfile, result);
        if (result.Diagnostics.Count > 0)
            return result;
        foreach (ResourceMappingDerivedAsset asset in EnumerateAssets(root)
                     .OrderBy(value => value.AssetPath, StringComparer.Ordinal)
                     .ThenBy(value => value.ResourceId, StringComparer.Ordinal))
        {
            string path = asset.AssetPath;
            string guid = ReadUnityGuid(root, path);
            if (!string.IsNullOrEmpty(guid) && priorByGuid.TryGetValue(guid, out ResourceMappingEntry prior) && prior.ResourceKey != null)
            {
                result.Snapshot.Entries.Add(CreateEntryFromPrior(prior, asset, path, guid));
                continue;
            }
            if (string.IsNullOrEmpty(guid))
            {
                ResourceMappingEntry pending = CreatePendingUnityImportEntry(asset);
                if (pending != null)
                    result.Snapshot.Entries.Add(pending);
                else
                    result.Diagnostics.Add(Error("ProducerSourceGuidMissing", "Repository source has no valid Unity GUID: " + path + ".", path));
                continue;
            }
            result.Snapshot.Entries.Add(CreateEntry(asset, guid));
        }
        result.Success = result.Diagnostics.Count == 0;
        return result;
    }

    protected virtual ResourceMappingEntry CreatePendingUnityImportEntry(ResourceMappingDerivedAsset asset) => null;

    protected ResourceMappingEntry CreatePendingGeneratedEntry(ResourceMappingDerivedAsset asset)
    {
        string recordId = ProducerId + ":" + asset.ResourceId;
        string fingerprint = ResourceMappingCanonicalJson.ComputeFingerprint(ResourceMappingCanonicalJson.Serialize(
            AppendProviderData(
                new[] { ProducerId, recordId, asset.AssetPath, asset.ResourceId, "pending-unity-import" },
                asset.ProviderData)));
        return new ResourceMappingEntry
        {
            ProducerReceipt = new ResourceMappingProducerReceipt { ProducerId = ProducerId, ProducerContractVersion = "2", SourceRecordFingerprint = fingerprint },
            AuthoringRecordId = recordId,
            SourceLocator = new ResourceMappingSourceLocator
            {
                Kind = ResourceMappingSourceLocatorKind.GeneratedUnityAsset,
                CachedPath = asset.AssetPath,
                GeneratedAssetId = asset.ResourceId,
                DomainSourceId = ProducerId + "/" + asset.ResourceId
            },
            ResourceKey = new ResourceMappingResourceKey
            {
                Id = asset.ResourceId,
                TypeId = asset.TypeId,
                Variant = string.Empty,
                PackageId = asset.PackageId
            },
            DeliveryIntent = asset.DeliveryIntent,
            OverrideIntent = ResourceMappingOverrideIntent.Forbid,
            BundleIntent = new ResourceMappingBundleIntent { RuleId = asset.BundleRuleId },
            Labels = new List<string>(asset.Labels),
            PreloadGroups = new List<string>(asset.PreloadGroups),
            ProviderData = new Dictionary<string, string>(asset.ProviderData, StringComparer.Ordinal)
        };
    }

    protected static IEnumerable<string> Files(string root, string relativeRoot, params string[] extensions)
    {
        string directory = Path.Combine(root, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory))
            yield break;
        foreach (string fullPath in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(fullPath);
            if (extensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase)))
                yield return Relative(root, fullPath);
        }
    }

    protected static string Relative(string root, string fullPath) =>
        Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');

    protected static string Snake(string value)
    {
        var output = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if ((char.IsUpper(current) && i > 0 && value[i - 1] != '_') || current == '-' || current == ' ')
                output.Append('_');
            if (current != '-' && current != ' ')
                output.Append(char.ToLowerInvariant(current));
        }
        return output.ToString();
    }

    private ResourceMappingEntry CreateEntry(ResourceMappingDerivedAsset asset, string guid)
    {
        string recordId = ProducerId + ":" + asset.ResourceId;
        string fingerprint = ResourceMappingCanonicalJson.ComputeFingerprint(ResourceMappingCanonicalJson.Serialize(
            AppendProviderData(
                new[] { ProducerId, recordId, asset.ResourceId, asset.TypeId, asset.PackageId, guid, asset.AssetPath, asset.BundleRuleId },
                asset.ProviderData)));
        bool excluded = asset.DeliveryIntent == ResourceMappingDeliveryIntent.Excluded;
        return new ResourceMappingEntry
        {
            ProducerReceipt = new ResourceMappingProducerReceipt { ProducerId = ProducerId, ProducerContractVersion = "2", SourceRecordFingerprint = fingerprint },
            AuthoringRecordId = recordId,
            ResourceKey = excluded ? null : new ResourceMappingResourceKey { Id = asset.ResourceId, TypeId = asset.TypeId, Variant = string.Empty, PackageId = asset.PackageId },
            SourceLocator = excluded ? null : new ResourceMappingSourceLocator { Kind = ResourceMappingSourceLocatorKind.UnityAsset, UnityGuid = guid, CachedPath = asset.AssetPath, DomainSourceId = ProducerId + "/" + asset.ResourceId },
            DeliveryIntent = asset.DeliveryIntent,
            OverrideIntent = excluded ? null : ResourceMappingOverrideIntent.Forbid,
            BundleIntent = excluded ? null : new ResourceMappingBundleIntent { RuleId = asset.BundleRuleId },
            Labels = new List<string>(asset.Labels),
            PreloadGroups = new List<string>(asset.PreloadGroups),
            ProviderData = excluded
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(asset.ProviderData, StringComparer.Ordinal)
        };
    }

    private ResourceMappingEntry CreateEntryFromPrior(
        ResourceMappingEntry prior,
        ResourceMappingDerivedAsset asset,
        string assetPath,
        string guid)
    {
        bool excluded = asset.DeliveryIntent == ResourceMappingDeliveryIntent.Excluded;
        string fingerprint = ResourceMappingCanonicalJson.ComputeFingerprint(ResourceMappingCanonicalJson.Serialize(
            AppendProviderData(new[]
            {
                ProducerId, prior.AuthoringRecordId, prior.ResourceKey.ToIdentityString(), guid, assetPath,
                asset.BundleRuleId, string.Join("|", asset.Labels), string.Join("|", asset.PreloadGroups)
            }, asset.ProviderData)));
        return new ResourceMappingEntry
        {
            ProducerReceipt = new ResourceMappingProducerReceipt { ProducerId = ProducerId, ProducerContractVersion = "2", SourceRecordFingerprint = fingerprint },
            AuthoringRecordId = prior.AuthoringRecordId,
            ResourceKey = excluded ? null : new ResourceMappingResourceKey { Id = prior.ResourceKey.Id, TypeId = prior.ResourceKey.TypeId, Variant = prior.ResourceKey.Variant, PackageId = prior.ResourceKey.PackageId },
            SourceLocator = excluded ? null : new ResourceMappingSourceLocator { Kind = ResourceMappingSourceLocatorKind.UnityAsset, UnityGuid = guid, CachedPath = assetPath, DomainSourceId = prior.SourceLocator.DomainSourceId },
            DeliveryIntent = asset.DeliveryIntent,
            OverrideIntent = excluded ? null : ResourceMappingOverrideIntent.Forbid,
            BundleIntent = excluded ? null : new ResourceMappingBundleIntent { RuleId = asset.BundleRuleId },
            Labels = MergeStrings(asset.Labels, prior.Labels),
            PreloadGroups = new List<string>(asset.PreloadGroups),
            Dependencies = excluded
                ? new List<ResourceMappingResourceKey>()
                : new List<ResourceMappingResourceKey>(prior.Dependencies.ConvertAll(key => new ResourceMappingResourceKey { Id = key.Id, TypeId = key.TypeId, Variant = key.Variant, PackageId = key.PackageId })),
            ProviderData = excluded
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : MergeProviderData(prior.ProviderData, asset.ProviderData)
        };
    }

    private static Dictionary<string, string> MergeProviderData(
        Dictionary<string, string> prior,
        Dictionary<string, string> current)
    {
        var result = prior != null
            ? new Dictionary<string, string>(prior, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        if (current == null)
            return result;

        foreach (KeyValuePair<string, string> pair in current)
            result[pair.Key] = pair.Value ?? string.Empty;
        return result;
    }

    private static string SerializeProviderData(Dictionary<string, string> providerData)
    {
        if (providerData == null || providerData.Count == 0)
            return string.Empty;

        return ResourceMappingCanonicalJson.Serialize(providerData
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new[] { pair.Key, pair.Value ?? string.Empty })
            .ToArray());
    }

    private static string[] AppendProviderData(
        string[] values,
        Dictionary<string, string> providerData)
    {
        string serialized = SerializeProviderData(providerData);
        if (string.IsNullOrEmpty(serialized))
            return values;

        var result = new string[values.Length + 2];
        Array.Copy(values, result, values.Length);
        result[values.Length] = "providerData";
        result[values.Length + 1] = serialized;
        return result;
    }

    private static List<string> MergeStrings(IEnumerable<string> current, IEnumerable<string> prior)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (current != null)
            foreach (string value in current)
                if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
                    result.Add(value);
        if (prior != null)
            foreach (string value in prior)
                if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
                    result.Add(value);
        return result;
    }

    private static string ReadUnityGuid(string root, string assetPath)
    {
        string metaPath = Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar)) + ".meta";
        if (!File.Exists(metaPath)) return string.Empty;
        foreach (string line in File.ReadLines(metaPath))
        {
            const string prefix = "guid: ";
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                string guid = line.Substring(prefix.Length).Trim();
                return guid.Length == 32 ? guid : string.Empty;
            }
        }
        return string.Empty;
    }

    private Dictionary<string, ResourceMappingEntry> ReadPriorEntriesByGuid(ResourceMappingProfileV2 priorProfile, ResourceMappingProducerSnapshotResult result)
    {
        var entries = new Dictionary<string, ResourceMappingEntry>(StringComparer.Ordinal);
        if (priorProfile == null) return entries;
        for (int i = 0; i < priorProfile.Entries.Count; i++)
        {
            ResourceMappingEntry entry = priorProfile.Entries[i];
            if (entry?.ProducerReceipt?.ProducerId != ProducerId || entry.SourceLocator == null || string.IsNullOrWhiteSpace(entry.SourceLocator.UnityGuid))
                continue;
            if (!entries.TryAdd(entry.SourceLocator.UnityGuid, entry))
                result.Diagnostics.Add(Error("ProducerSourceGuidAmbiguous", "One producer owns multiple identities for the same Unity GUID.", entry.SourceLocator.UnityGuid));
        }
        return entries;
    }

    private static ResourceMappingDiagnostic Error(string code, string message, string path) =>
        new() { Severity = ResourceMappingDiagnosticSeverity.Error, Code = code, Message = message, Path = path };
}

internal sealed class CharacterRepositoryProducerAdapter : ResourceMappingRepositoryProducerAdapter
{
    public override string ProducerId => "character.global-profile";
    protected override IReadOnlyList<string> RequiredDirectories => new[] { "Assets/Art/MxFramework/Samples/Characters/Skeleton", "Assets/Art/MxFramework/Samples/Weapons/Katana" };
    protected override IEnumerable<string> EnumerateAssetPaths(string root)
    {
        foreach (string path in Files(root, "Assets/Art/MxFramework/Samples", ".anim", ".mask", ".fbx", ".prefab"))
            yield return path;
        foreach (string path in Files(root, "Assets/Art/MxFramework/Characters", ".anim"))
            yield return path;
    }

    protected override ResourceMappingDerivedAsset Describe(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        if (path.StartsWith("Assets/Art/MxFramework/Characters/", StringComparison.Ordinal)
            && path.Contains("/AnimationClips/", StringComparison.Ordinal))
        {
            string[] parts = path.Split('/');
            if (parts.Length < 7)
                return null;

            string characterId = Snake(parts[4]);
            return new ResourceMappingDerivedAsset(
                path,
                "art.character." + characterId + ".animation." + stem.ToLowerInvariant(),
                "AnimationClip",
                characterId,
                "character.samples",
                new[] { "character." + characterId, "domain.animation", "package." + characterId },
                new[] { "AnimationWarmup" });
        }
        if (path.Contains("/AnimationClips/", StringComparison.Ordinal)) return Asset(path, "art.character.skeleton.animation." + stem.ToLowerInvariant(), "AnimationClip", "character.samples", "domain.animation", "AnimationWarmup");
        if (path.EndsWith("/SkeletonUpperBody.mask", StringComparison.Ordinal)) return Asset(path, "art.character.skeleton.mask.upper_body", "AvatarMask", "character.samples", "domain.animation", "AnimationWarmup");
        if (path.EndsWith("/Skeleton.fbx", StringComparison.Ordinal)) return Asset(path, "art.character.skeleton.model", "GameObject", "character.samples", "domain.model", "SpawnCritical");
        if (path.EndsWith("/Katana_Generic01.prefab", StringComparison.Ordinal)) return Asset(path, "art.weapon.katana.generic_01", "GameObject", "character.samples", "domain.model", "EquipmentInitial");
        return null;
    }
    private static ResourceMappingDerivedAsset Asset(string path, string id, string type, string bundle, string label, string preload) => new(path, id, type, "mxframework.samples", bundle, new[] { label }, new[] { preload });
}

internal sealed class AudioRepositoryProducerAdapter : ResourceMappingRepositoryProducerAdapter
{
    public override string ProducerId => "audio.global-profile";
    protected override IReadOnlyList<string> RequiredDirectories => new[] { "Assets/Audio/MxFramework/Samples/MagicEffects" };
    protected override IEnumerable<string> EnumerateAssetPaths(string root) => Files(root, "Assets/Audio/MxFramework/Samples/MagicEffects", ".wav", ".ogg");
    protected override ResourceMappingDerivedAsset Describe(string path) => new(path, "audio.magic_effect." + Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), "AudioClip", "mxframework.samples", "audio.samples", new[] { "domain.audio" }, Array.Empty<string>());
}

internal sealed class FairyGuiRepositoryProducerAdapter : ResourceMappingRepositoryProducerAdapter
{
    public override string ProducerId => "fairygui.global-profile";
    protected override IReadOnlyList<string> RequiredDirectories => new[] { "Assets/Bundles/FGUI" };
    protected override IReadOnlyList<string> RequiredFiles => new[]
    {
        "Assets/Bundles/FGUI/MxCharacterTestAbilityUi/MxCharacterTestAbilityUi_fui.bytes",
        "Assets/Bundles/FGUI/MxCharacterTestAbilityUi/MxCharacterTestAbilityUi_atlas0.png",
        "Assets/Bundles/FGUI/MxCharacterTestBehaviorDeckUi/MxCharacterTestBehaviorDeckUi_fui.bytes",
        "Assets/Bundles/FGUI/MxCharacterTestBehaviorDeckUi/MxCharacterTestBehaviorDeckUi_atlas0.png",
        "Assets/Bundles/FGUI/MxCharacterTestTacticalDeckUi/MxCharacterTestTacticalDeckUi_fui.bytes",
        "Assets/Bundles/FGUI/MxCharacterTestTacticalDeckUi/MxCharacterTestTacticalDeckUi_atlas0.png",
        "Assets/Bundles/FGUI/MxCharacterTestCombatHud/MxCharacterTestCombatHud_fui.bytes",
        "Assets/Bundles/FGUI/MxCharacterTestCombatHud/MxCharacterTestCombatHud_atlas0.png",
        "Assets/Bundles/FGUI/MxCharacterTestHud/MxCharacterTestHud_fui.bytes",
        "Assets/Bundles/FGUI/MxCharacterTestHud/MxCharacterTestHud_atlas0.png",
        "Assets/Bundles/FGUI/MxCharacterTestNetworkUi/MxCharacterTestNetworkUi_fui.bytes",
        "Assets/Bundles/FGUI/MxCharacterTestNetworkUi/MxCharacterTestNetworkUi_atlas0.png",
        "Assets/Bundles/FGUI/MxPlayerGuidanceUi/MxPlayerGuidanceUi_fui.bytes",
        "Assets/Bundles/FGUI/MxPlayerGuidanceUi/MxPlayerGuidanceUi_atlas0.png",
        "Assets/Bundles/FGUI/MxCharacterTestRuntimeAiLabUi/MxCharacterTestRuntimeAiLabUi_fui.bytes",
        "Assets/Bundles/FGUI/MxCharacterTestRuntimeAiLabUi/MxCharacterTestRuntimeAiLabUi_atlas0.png",
        "Assets/Bundles/FGUI/MxCharacterTestRuntimeAiLabRecoveryUi/MxCharacterTestRuntimeAiLabRecoveryUi_fui.bytes",
        "Assets/Bundles/FGUI/MxCharacterTestRuntimeAiLabRecoveryUi/MxCharacterTestRuntimeAiLabRecoveryUi_atlas0.png",
        "Assets/Bundles/FGUI/MxCharacterTestSettingsUi/MxCharacterTestSettingsUi_fui.bytes",
        "Assets/Bundles/FGUI/MxCharacterTestSettingsUi/MxCharacterTestSettingsUi_atlas0.png",
        "Assets/Bundles/FGUI/MxCharacterTestUi/MxCharacterTestUi_fui.bytes",
        "Assets/Bundles/FGUI/MxCharacterTestUi/MxCharacterTestUi_atlas0.png",
        "Assets/Bundles/FGUI/MxStoryDialog/MxStoryDialog_fui.bytes"
    };
    protected override IEnumerable<string> EnumerateAssetPaths(string root) => Files(root, "Assets/Bundles/FGUI", ".png", ".bytes");
    protected override ResourceMappingDerivedAsset Describe(string path)
    {
        string package = path.Split('/')[3];
        string packageId;
        if (package == "MxCharacterTestUi") packageId = "charactertest.player_ui";
        else if (package == "MxPlayerGuidanceUi") packageId = "player_guidance";
        else if (package.StartsWith("MxCharacterTest", StringComparison.Ordinal)) packageId = "charactertest." + Snake(package.Substring("MxCharacterTest".Length));
        else if (package == "MxStoryDialog") packageId = "storydialog";
        else return null;
        string extension = Path.GetExtension(path);
        string suffix = extension.Equals(".bytes", StringComparison.OrdinalIgnoreCase) ? "fui" : "atlas0";
        string stem = Path.GetFileNameWithoutExtension(path);
        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase) && stem.StartsWith(package + "_atlas", StringComparison.Ordinal))
            suffix = stem.Substring(package.Length + 1);
        string id = "ui.fairygui." + packageId + "." + suffix;
        return new ResourceMappingDerivedAsset(path, id, suffix == "fui" ? "MxFairyGuiPackageBytes" : "Texture2D", package, "ui.fairygui." + packageId, new[] { "bundle.ui.fairygui." + packageId, "domain.ui", "package." + package }, new[] { "UiDeferred" });
    }
}

internal sealed class StartScreenRepositoryProducerAdapter : ResourceMappingRepositoryProducerAdapter
{
    public override string ProducerId => "ui.start-screen.global-profile";
    protected override IReadOnlyList<string> RequiredDirectories => new[] { "Assets/UI/MxFramework/Samples/StartScreen/Textures" };
    protected override IEnumerable<string> EnumerateAssetPaths(string root) => Files(root, "Assets/UI/MxFramework/Samples/StartScreen/Textures", ".png");
    protected override ResourceMappingDerivedAsset Describe(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        int separator = stem.IndexOf('_');
        string id = "ui.start_screen." + (separator < 0 ? stem : stem.Substring(0, separator) + "." + stem.Substring(separator + 1));
        if (id == "ui.start_screen.button.normal") return new ResourceMappingDerivedAsset(path, id, string.Empty, string.Empty, string.Empty, new[] { "domain.texture" }, Array.Empty<string>(), ResourceMappingDeliveryIntent.Excluded);
        return new ResourceMappingDerivedAsset(path, id, "Texture2D", "mxframework.samples", "ui.start_screen", new[] { "domain.texture" }, new[] { "UiDeferred" });
    }
}

internal sealed class VfxRepositoryProducerAdapter : ResourceMappingRepositoryProducerAdapter
{
    private const string PublishedBindingsPath = "Assets/Config/MxFramework/Vfx/vfx_published_bindings.json";

    public override string ProducerId => "vfx.global-profile";
    protected override IReadOnlyList<string> RequiredDirectories => new[] { "Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs" };
    protected override IReadOnlyList<string> RequiredFiles => new[] { PublishedBindingsPath };
    protected override IEnumerable<string> EnumerateAssetPaths(string root) => Files(root, "Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs", ".prefab");
    protected override ResourceMappingDerivedAsset Describe(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        string name = stem.StartsWith("Aura_", StringComparison.Ordinal) ? stem.Substring(5) : stem;
        return new ResourceMappingDerivedAsset(path, "vfx.status_aura." + Snake(name), "GameObject", "mxframework.samples", "vfx.samples", new[] { "domain.vfx" }, Array.Empty<string>());
    }

    protected override IEnumerable<ResourceMappingDerivedAsset> EnumerateAssets(string repositoryRoot)
    {
        foreach (ResourceMappingDerivedAsset asset in base.EnumerateAssets(repositoryRoot))
            yield return asset;

        const string bladeTrailPath = "Assets/VFX/MxFramework/Samples/BladeTrail/BladeTrail.prefab";
        if (File.Exists(Path.Combine(repositoryRoot, bladeTrailPath.Replace('/', Path.DirectorySeparatorChar))))
            yield return new ResourceMappingDerivedAsset(bladeTrailPath, "vfx.weapon.blade_trail", "GameObject",
                "mxframework.samples", "vfx.samples", new[] { "domain.vfx", "vfx.sample" }, Array.Empty<string>());

        string manifestPath = Path.Combine(
            repositoryRoot,
            PublishedBindingsPath.Replace('/', Path.DirectorySeparatorChar));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!document.RootElement.TryGetProperty("bindings", out JsonElement bindings)
            || bindings.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement binding in bindings.EnumerateArray())
        {
            if (!IsProductionReady(binding)
                || !binding.TryGetProperty("resourceKey", out JsonElement resourceKey))
            {
                continue;
            }

            string id = ReadString(resourceKey, "id");
            string typeId = ReadString(resourceKey, "typeId");
            string packageId = ReadString(resourceKey, "packageId");
            string assetPath = ReadString(binding, "assetPath");
            if (string.IsNullOrWhiteSpace(id)
                || string.IsNullOrWhiteSpace(typeId)
                || string.IsNullOrWhiteSpace(packageId)
                || string.IsNullOrWhiteSpace(assetPath))
            {
                continue;
            }

            yield return new ResourceMappingDerivedAsset(
                assetPath,
                id,
                typeId,
                packageId,
                "vfx.samples",
                new[] { "domain.vfx", "vfx.production" },
                Array.Empty<string>());
        }
    }

    private static bool IsProductionReady(JsonElement binding) =>
        string.Equals(ReadString(binding, "visualMaturity"), "Production", StringComparison.Ordinal)
        && string.Equals(ReadString(binding, "reviewStatus"), "Ready", StringComparison.Ordinal);

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
}

internal sealed class DemoRepositoryProducerAdapter : ResourceMappingRepositoryProducerAdapter
{
    public override string ProducerId => "demo.samples.global-profile";
    protected override IReadOnlyList<string> RequiredDirectories => new[] { "Assets/MxFrameworkGenerated/Common" };
    protected override IReadOnlyList<string> RequiredFiles => new[] { "Assets/Scenes/CharacterTestScene.unity" };
    protected override IEnumerable<string> EnumerateAssetPaths(string root)
    {
        foreach (string path in Files(root, "Assets/MxFrameworkGenerated/Common", ".fbx", ".png", ".prefab"))
        {
            string[] parts = path.Split('/');
            if (path.Contains("/Weapons/", StringComparison.Ordinal) && path.Contains("/model/", StringComparison.Ordinal) &&
                Path.GetFileName(path) == parts[4] + ".fbx")
            {
                string wrapper = Path.Combine(root, "Assets", "MxFrameworkGenerated", "Common", "Weapons", parts[4], "prefabs", parts[4] + "_weapon.prefab");
                if (File.Exists(wrapper))
                    continue;
            }
            yield return path;
        }
        string scene = Path.Combine(root, "Assets/Scenes/CharacterTestScene.unity");
        if (File.Exists(scene)) yield return Relative(root, scene);
    }

    protected override IEnumerable<ResourceMappingDerivedAsset> EnumerateAssets(string repositoryRoot)
    {
        foreach (ResourceMappingDerivedAsset asset in base.EnumerateAssets(repositoryRoot))
        {
            if (asset != null && asset.ResourceId.StartsWith("weapon.", StringComparison.Ordinal) &&
                asset.ResourceId.EndsWith(".model", StringComparison.Ordinal))
            {
                string weaponId = asset.ResourceId.Substring(
                    "weapon.".Length,
                    asset.ResourceId.Length - "weapon.".Length - ".model".Length);
                AddCommonWeaponMetadata(repositoryRoot, asset, weaponId);
            }

            yield return asset;
        }
    }

    private static void AddCommonWeaponMetadata(
        string repositoryRoot,
        ResourceMappingDerivedAsset asset,
        string weaponId)
    {
        string weaponRoot = "Assets/MxFrameworkGenerated/Common/Weapons/" + weaponId;
        string wrapperPath = weaponRoot + "/prefabs/" + weaponId + "_weapon.prefab";
        string wrapperFullPath = Path.Combine(repositoryRoot, wrapperPath.Replace('/', Path.DirectorySeparatorChar));
        string manifestPath = Path.Combine(
            repositoryRoot,
            "ArtSource",
            "Common",
            "Weapons",
            weaponId,
            "weapon_sources.json");
        string rawGeneratedPath = FindRawGeneratedWeaponPath(repositoryRoot, weaponRoot, weaponId);

        asset.ProviderData["commonResourceKind"] = "weapon";
        asset.ProviderData["commonResourceId"] = weaponId;
        asset.ProviderData["usage"] = CharacterPackageResourceUsageIds.WeaponModel;
        asset.ProviderData["rawGeneratedAssetPath"] = rawGeneratedPath;
        asset.ProviderData["sourceGeneratedAssetPath"] = rawGeneratedPath;
        asset.ProviderData["generatedAssetPath"] = wrapperPath;
        asset.ProviderData["unityRuntimeAssetPath"] = wrapperPath;
        asset.ProviderData["usesUnityWrapperPrefab"] = File.Exists(wrapperFullPath) ? "true" : "false";

        string modelWrapperPose = ReadModelWrapperPose(manifestPath, asset.ResourceId);
        if (!string.IsNullOrWhiteSpace(modelWrapperPose))
            asset.ProviderData["modelWrapperPose"] = modelWrapperPose;
        string paletteMode = ReadManifestString(manifestPath, "paletteMode");
        if (!string.IsNullOrWhiteSpace(paletteMode))
            asset.ProviderData["paletteMode"] = paletteMode;
    }

    private static string FindRawGeneratedWeaponPath(
        string repositoryRoot,
        string weaponRoot,
        string weaponId)
    {
        string[] extensions = { ".fbx", ".glb", ".gltf" };
        for (int i = 0; i < extensions.Length; i++)
        {
            string candidate = weaponRoot + "/model/" + weaponId + extensions[i];
            string fullPath = Path.Combine(repositoryRoot, candidate.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                return candidate;
        }

        return weaponRoot + "/model/" + weaponId + ".fbx";
    }

    private static string ReadModelWrapperPose(string manifestPath, string resourceKey)
    {
        if (!File.Exists(manifestPath))
            return string.Empty;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!document.RootElement.TryGetProperty("items", out JsonElement items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("target", out JsonElement target) ||
                    !string.Equals(ReadJsonString(target, "resourceKey"), resourceKey, StringComparison.Ordinal) ||
                    !target.TryGetProperty("importHints", out JsonElement importHints) ||
                    !importHints.TryGetProperty("modelWrapperPose", out JsonElement pose) ||
                    pose.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                return pose.GetRawText();
            }
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static string ReadManifestString(string manifestPath, string propertyName)
    {
        if (!File.Exists(manifestPath) || string.IsNullOrWhiteSpace(propertyName))
            return string.Empty;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return ReadJsonString(document.RootElement, propertyName);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string ReadJsonString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    protected override ResourceMappingDerivedAsset Describe(string path)
    {
        if (path.EndsWith("CharacterTestScene.unity", StringComparison.Ordinal)) return new(path, "scene.character_test", "Scene", "mxframework.samples", "scene.character_test", new[] { "bundle.scene.character_test", "domain.scene", "package.mxframework.samples" }, Array.Empty<string>());
        string[] parts = path.Split('/');
        if (path.Contains("/Characters/", StringComparison.Ordinal) && Path.GetFileName(path) == parts[4] + ".fbx") return new(path, "character." + parts[4] + ".model", "GameObject", "common", "common.models", new[] { "bundle.common.models", "domain.character", "package.common", "public.common", "usage.charactermodel" }, new[] { "SpawnCritical" });
        if (path.Contains("/Textures/", StringComparison.Ordinal) && Path.GetFileName(path) == "body_albedo.png") return new(path, "texture." + parts[4], "Texture2D", "common", "common.textures", new[] { "bundle.common.textures", "domain.texture", "package.common", "public.common", "usage.texture" }, Array.Empty<string>());
        if (path.Contains("/Weapons/", StringComparison.Ordinal) && Path.GetFileName(path) == parts[4] + "_weapon.prefab") return new(path, "weapon." + parts[4] + ".model", "GameObject", "common", "common.models", new[] { "bundle.common.models", "domain.weapon", "package.common", "public.common", "usage.weaponmodel" }, new[] { "EquipmentInitial" });
        if (path.Contains("/Weapons/", StringComparison.Ordinal) && path.Contains("/model/", StringComparison.Ordinal) && Path.GetFileName(path) == parts[4] + ".fbx") return new(path, "weapon." + parts[4] + ".model", "GameObject", "common", "common.models", new[] { "bundle.common.models", "domain.weapon", "package.common", "public.common", "usage.weaponmodel" }, new[] { "EquipmentInitial" });
        if (path.Contains("/Weapons/", StringComparison.Ordinal) && Path.GetFileName(path) == "weapon_albedo.png") return new(path, "weapon." + parts[4] + ".texture.albedo", "Texture2D", "common", "common.textures", new[] { "bundle.common.textures", "domain.texture", "package.common", "public.common", "usage.texture" }, Array.Empty<string>());
        return null;
    }

    protected override ResourceMappingEntry CreatePendingUnityImportEntry(ResourceMappingDerivedAsset asset)
    {
        return asset.AssetPath.EndsWith(".unity", StringComparison.Ordinal)
            ? null
            : CreatePendingGeneratedEntry(asset);
    }
}

internal sealed class ResourceMappingProfilePolicyProducerAdapter : IResourceMappingFirstPartyProducerAdapter
{
    public string ProducerId => "resource-manager.profile-policy";
    public ResourceMappingProducerSnapshotResult BuildSnapshot(string repositoryRoot, ResourceMappingProfileV2 priorProfile = null)
    {
        var snapshot = new ResourceMappingProducerSnapshot { ProducerId = ProducerId };
        AddRule(snapshot, "audio.samples", "global.audio.samples.assetbundle", false);
        AddRule(snapshot, "character.package.assets", "global.character.package.assets", true);
        AddRule(snapshot, "character.samples", "global.character.samples.assetbundle", false);
        AddRule(snapshot, "common.audio", "common_audio", true); AddRule(snapshot, "common.config", "common_config", true);
        AddRule(snapshot, "common.materials", "common_materials", true); AddRule(snapshot, "common.models", "common_models", false);
        AddRule(snapshot, "common.textures", "common_textures", false); AddRule(snapshot, "common.vfx", "common_vfx", true);
        AddRule(snapshot, "scene.character_test", "global.scene.character_test.assetbundle", false);
        string[] ui =
        {
            "ability_ui",
            "behavior_deck_ui",
            "combat_hud",
            "hud",
            "network_ui",
            "player_ui",
            "runtime_ai_lab_recovery_ui",
            "runtime_ai_lab_ui",
            "settings_ui",
            "tactical_deck_ui"
        };
        for (int i = 0; i < ui.Length; i++) AddRule(snapshot, "ui.fairygui.charactertest." + ui[i], "global.ui.fairygui.charactertest." + ui[i] + ".assetbundle", false);
        AddRule(snapshot, "ui.fairygui.player_guidance", "global.ui.fairygui.player_guidance.assetbundle", false);
        AddRule(snapshot, "ui.fairygui.storydialog", "global.ui.fairygui.storydialog.assetbundle", false);
        AddRule(snapshot, "ui.start_screen", "global.ui.start_screen.assetbundle", false); AddRule(snapshot, "vfx.samples", "global.vfx.samples.assetbundle", false);
        AddPreload(snapshot, "AnimationWarmup", "bundle.animation.warmup", false); AddPreload(snapshot, "EquipmentInitial", "bundle.equipment.initial", true);
        AddPreload(snapshot, "SpawnCritical", "bundle.spawn.critical", true); AddPreload(snapshot, "UiDeferred", "bundle.ui.deferred", false); AddPreload(snapshot, "boot.base", "preload.boot.base", true);
        return new ResourceMappingProducerSnapshotResult { Success = true, Snapshot = snapshot };
    }
    private void AddRule(ResourceMappingProducerSnapshot snapshot, string id, string bundleName, bool allowEmpty)
    {
        string recordId = "bundle:" + id;
        snapshot.BundleRules.Add(new ResourceMappingBundleRule { ProducerReceipt = Receipt(recordId), AuthoringRecordId = recordId, Id = id, BundleName = bundleName, Compression = "lz4", BuildTarget = "ActiveBuildTarget", IncludeDependencies = true, AllowEmpty = allowEmpty });
    }
    private void AddPreload(ResourceMappingProducerSnapshot snapshot, string id, string label, bool failFast)
    {
        string recordId = "preload:" + id;
        snapshot.PreloadGroups.Add(new ResourceMappingPreloadGroup { ProducerReceipt = Receipt(recordId), AuthoringRecordId = recordId, Id = id, ExplicitKeys = new List<ResourceMappingResourceKey>(), Labels = new List<string> { label }, FailFast = failFast, MaxConcurrentLoads = 4, AllowEmpty = false });
    }
    private ResourceMappingProducerReceipt Receipt(string id) => new() { ProducerId = ProducerId, ProducerContractVersion = "2", SourceRecordFingerprint = ResourceMappingCanonicalJson.ComputeFingerprint(ProducerId + "|" + id) };
}

internal sealed class ResourceMappingDerivedAsset
{
    public ResourceMappingDerivedAsset(string assetPath, string resourceId, string typeId, string packageId, string bundleRuleId, string[] labels, string[] preloadGroups, ResourceMappingDeliveryIntent deliveryIntent = ResourceMappingDeliveryIntent.Internal)
    { AssetPath = assetPath; ResourceId = resourceId; TypeId = typeId; PackageId = packageId; BundleRuleId = bundleRuleId; Labels = labels; PreloadGroups = preloadGroups; DeliveryIntent = deliveryIntent; }
    public string AssetPath { get; } public string ResourceId { get; } public string TypeId { get; } public string PackageId { get; } public string BundleRuleId { get; } public string[] Labels { get; } public string[] PreloadGroups { get; } public ResourceMappingDeliveryIntent DeliveryIntent { get; } public Dictionary<string, string> ProviderData { get; } = new(StringComparer.Ordinal);
}

internal static class ResourceMappingFirstPartyProducerAdapters
{
    public static IReadOnlyList<IResourceMappingFirstPartyProducerAdapter> CreateAll() => new IResourceMappingFirstPartyProducerAdapter[]
    {
        new ResourceMappingProfilePolicyProducerAdapter(), new CharacterRepositoryProducerAdapter(), new AudioRepositoryProducerAdapter(),
        new FairyGuiRepositoryProducerAdapter(), new StartScreenRepositoryProducerAdapter(), new VfxRepositoryProducerAdapter(), new DemoRepositoryProducerAdapter()
    };
}
