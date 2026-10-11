using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Resources;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    public readonly struct CharacterRuntimeAnimationBinding
    {
        public CharacterRuntimeAnimationBinding(
            string animationSetDefinitionPath,
            string animationClipRegistryPath,
            CharacterRuntimeAnimationProfileBinding profileBinding)
        {
            AnimationSetDefinitionPath = animationSetDefinitionPath ?? string.Empty;
            AnimationClipRegistryPath = animationClipRegistryPath ?? string.Empty;
            ProfileBinding = profileBinding;
        }

        public string AnimationSetDefinitionPath { get; }
        public string AnimationClipRegistryPath { get; }
        public CharacterRuntimeAnimationProfileBinding ProfileBinding { get; }
        public string ResourcePackageId => ProfileBinding.ResourcePackageId;
        public string AnimationProfileId => ProfileBinding.ProfileId;
        public string AnimationSetId => ProfileBinding.SetId;
        public string LocomotionGroupId => ProfileBinding.GroupId;
        public string LocomotionBlendId => ProfileBinding.BlendId;
        public CharacterAnimationBlendKind BlendKind => ProfileBinding.BlendKind;
        public bool IsValid => !string.IsNullOrWhiteSpace(AnimationSetDefinitionPath)
            && !string.IsNullOrWhiteSpace(AnimationClipRegistryPath)
            && ProfileBinding.IsValid;
    }

    public readonly struct CharacterRuntimeLocomotionDefinition
    {
        public CharacterRuntimeLocomotionDefinition(
            MxAnimationSetDefinition animationSet,
            CharacterAnimationBlendKind blendKind,
            MxAnimationBlend1DDefinition blend1D,
            MxAnimationBlend2DDefinition blend2D)
        {
            AnimationSet = animationSet;
            BlendKind = blendKind;
            Blend1D = blend1D;
            Blend2D = blend2D;
        }

        public MxAnimationSetDefinition AnimationSet { get; }
        public CharacterAnimationBlendKind BlendKind { get; }
        public MxAnimationBlend1DDefinition Blend1D { get; }
        public MxAnimationBlend2DDefinition Blend2D { get; }
        public string BlendId => BlendKind == CharacterAnimationBlendKind.Blend1D
            ? Blend1D?.BlendId ?? string.Empty
            : Blend2D?.BlendId ?? string.Empty;
        public bool IsValid => AnimationSet != null
            && ((BlendKind == CharacterAnimationBlendKind.Blend1D && Blend1D != null)
                || (BlendKind == CharacterAnimationBlendKind.Blend2D && Blend2D != null));
    }

    public readonly struct CharacterRuntimeAnimationFallbackActionBinding
    {
        public CharacterRuntimeAnimationFallbackActionBinding(
            int bindingId,
            string actionKey,
            string sourceActionKey,
            float fadeDurationSeconds = 0.08f,
            string bindingLabel = "")
        {
            BindingId = bindingId;
            BindingLabel = bindingLabel ?? string.Empty;
            ActionKey = actionKey ?? string.Empty;
            SourceActionKey = sourceActionKey ?? string.Empty;
            FadeDurationSeconds = fadeDurationSeconds < 0f ? 0f : fadeDurationSeconds;
        }

        public int BindingId { get; }
        public string BindingLabel { get; }
        public string ActionKey { get; }
        public string SourceActionKey { get; }
        public float FadeDurationSeconds { get; }
        public bool IsValid => BindingId > 0 && !string.IsNullOrWhiteSpace(ActionKey)
            && !string.IsNullOrWhiteSpace(SourceActionKey);
    }

    public static class CharacterRuntimeAnimationResources
    {
        private const int MaxCachedAnimationSetDefinitionFiles = 32;
        private const int MaxCachedAnimationClipRegistryFiles = 32;
        private const string AnimationSetPathProviderDataKey = "animationSetPath";
        private const string ClipRegistryPathProviderDataKey = "clipRegistryPath";
        private const string ClipIdProviderDataKey = "clipId";
        private const string SourceClipNameProviderDataKey = "sourceClipName";
        private const string UnityAssetPathProviderDataKey = "unityAssetPath";
        private const string GeneratedAssetsPrefix = "Assets/MxFrameworkGenerated";
        private const string GeneratedStreamingAssetsRoot = "MxFramework/Generated";
        private static readonly string[] DefaultCatalogLabels =
        {
            "CharacterAnimation",
            "CharacterLocomotion"
        };
        private static readonly Dictionary<AnimationSetDefinitionCacheKey, IReadOnlyList<MxAnimationSetDefinition>>
            AnimationSetDefinitionCache =
                new Dictionary<AnimationSetDefinitionCacheKey, IReadOnlyList<MxAnimationSetDefinition>>();
        private static readonly Dictionary<AnimationClipRegistryCacheKey, IReadOnlyList<AnimationClipRegistryEntryData>>
            AnimationClipRegistryCache =
                new Dictionary<AnimationClipRegistryCacheKey, IReadOnlyList<AnimationClipRegistryEntryData>>();

        public static bool TryCreateBinding(
            CharacterImportedPackage package,
            out CharacterRuntimeAnimationBinding binding,
            out CharacterRuntimeSpawnResult spawnResult,
            string packageRootOverride = "")
        {
            binding = default;
            spawnResult = CharacterRuntimeSpawnResolver.Resolve(package, default);
            if (spawnResult == null || !spawnResult.IsSuccess || !spawnResult.Binding.AnimationProfileBinding.IsValid)
                return false;

            string root = string.IsNullOrWhiteSpace(packageRootOverride)
                ? package.RootPath
                : packageRootOverride;
            root = (root ?? string.Empty).TrimEnd('/', '\\').Replace('\\', '/');
            binding = new CharacterRuntimeAnimationBinding(
                root + "/config/animation_set_definition.json",
                root + "/config/animation_clip_registry.json",
                spawnResult.Binding.AnimationProfileBinding);
            return binding.IsValid;
        }

        public static IEnumerable<ResourceCatalogEntry> CreateCatalogEntries(
            CharacterRuntimeAnimationBinding binding,
            string providerId,
            bool useRuntimeResourceKeyAsAddress = false,
            IEnumerable<string> labels = null)
        {
            string clipRegistryPath = ResolveExistingRuntimeGeneratedFilePath(binding.AnimationClipRegistryPath);
            if (!binding.IsValid
                || string.IsNullOrWhiteSpace(providerId)
                || string.IsNullOrEmpty(clipRegistryPath))
            {
                yield break;
            }

            if (!TryLoadClipRegistryEntries(
                    clipRegistryPath,
                    out IReadOnlyList<AnimationClipRegistryEntryData> clips))
            {
                yield break;
            }

            IReadOnlyDictionary<string, string> importedAnimationAssetPaths =
                LoadImportedAnimationAssetPaths(clipRegistryPath);
            IEnumerable<string> safeLabels = labels ?? DefaultCatalogLabels;
            for (int i = 0; clips != null && i < clips.Count; i++)
            {
                AnimationClipRegistryEntryData clip = clips[i];
                string assetPath = ResolveRuntimeAnimationAssetPath(clip, importedAnimationAssetPaths);
                if (string.IsNullOrWhiteSpace(assetPath))
                    continue;

                yield return new ResourceCatalogEntry(
                    clip.ResourceKey,
                    ResourceTypeIds.AnimationClip,
                    providerId,
                    useRuntimeResourceKeyAsAddress ? clip.ResourceKey : assetPath,
                    variant: string.Empty,
                    packageId: binding.ResourcePackageId,
                    labels: safeLabels,
                    hash: clip.Hash,
                    providerData: new Dictionary<string, string>
                    {
                        { AnimationSetPathProviderDataKey, binding.AnimationSetDefinitionPath },
                        { ClipRegistryPathProviderDataKey, binding.AnimationClipRegistryPath },
                        { ClipIdProviderDataKey, clip.ClipId },
                        { SourceClipNameProviderDataKey, clip.SourceClipName },
                        { UnityAssetPathProviderDataKey, assetPath }
                    });
            }
        }

        public static bool TryConfigureLocomotion(
            CharacterRuntimeLocomotionBlendController locomotion,
            IResourceManager resourceManager,
            CharacterRuntimeAnimationBinding binding,
            string actorId,
            IRuntimeLogger logger,
            CharacterRuntimeAnimationFallbackActionBinding[] fallbackActionBindings = null,
            bool logResult = true,
            TextAsset animationSetDefinitionOverride = null)
        {
            logger = logger ?? NullRuntimeLogger.Instance;
            if (locomotion == null)
                return false;

            if (!TryCreateLocomotionDefinition(
                    binding,
                    logger,
                    out CharacterRuntimeLocomotionDefinition locomotionDefinition,
                    fallbackActionBindings,
                    animationSetDefinitionOverride))
            {
                return false;
            }

            bool configured = locomotion.TryConfigureResourceAnimationBackend(
                resourceManager,
                locomotionDefinition,
                actorId);

            if (logResult || !configured)
            {
                logger.Info(
                    "CharacterSpawn",
                    "Locomotion resource animation backend " + (configured ? "ready" : "missing")
                    + ". character=" + actorId
                    + " animationSet=" + locomotionDefinition.AnimationSet.SetId
                    + " blendId=" + locomotionDefinition.BlendId
                    + " blendKind=" + locomotionDefinition.BlendKind
                    + " hasBackend=" + locomotion.HasAnimationBackend);
            }
            return configured;
        }

        public static bool TryCreateLocomotionDefinition(
            CharacterRuntimeAnimationBinding binding,
            IRuntimeLogger logger,
            out CharacterRuntimeLocomotionDefinition locomotionDefinition,
            CharacterRuntimeAnimationFallbackActionBinding[] fallbackActionBindings = null,
            TextAsset animationSetDefinitionOverride = null)
        {
            logger = logger ?? NullRuntimeLogger.Instance;
            locomotionDefinition = default;

            if (!binding.IsValid)
            {
                logger.Warning("CharacterSpawn", "Animation runtime binding is invalid.");
                return false;
            }

            IReadOnlyList<MxAnimationSetDefinition> definitions;
            string animationSetDefinitionPath = binding.AnimationSetDefinitionPath;
            if (animationSetDefinitionOverride != null)
            {
                try
                {
                    definitions = MxAnimationCompiledArtifactJson.LoadSetDefinitions(
                        animationSetDefinitionOverride.text,
                        binding.ResourcePackageId);
                    animationSetDefinitionPath = animationSetDefinitionOverride.name;
                }
                catch (Exception ex)
                {
                    logger.Warning("CharacterSpawn", "Animation set definition override parse failed. error=" + ex.Message);
                    return false;
                }
            }
            else
            {
                animationSetDefinitionPath = ResolveExistingRuntimeGeneratedFilePath(binding.AnimationSetDefinitionPath);
                if (string.IsNullOrEmpty(animationSetDefinitionPath))
                {
                    logger.Warning(
                        "CharacterSpawn",
                        "Animation set definition was not found. path="
                        + binding.AnimationSetDefinitionPath
                        + " resolvedPath="
                        + ResolveRuntimeGeneratedMirrorPath(binding.AnimationSetDefinitionPath));
                    return false;
                }

                if (!TryLoadSetDefinitions(
                        binding,
                        animationSetDefinitionPath,
                        logger,
                        out definitions))
                {
                    return false;
                }
            }

            MxAnimationSetDefinition definition = FindDefinition(definitions, binding.AnimationSetId);
            if (definition == null)
            {
                logger.Warning("CharacterSpawn", "Animation set definition was not found. path="
                    + animationSetDefinitionPath + " setId=" + binding.AnimationSetId);
                return false;
            }

            definition = ApplyFallbackActionBindings(definition, fallbackActionBindings);
            if (!TryFindBlend(
                    definition,
                    binding.LocomotionBlendId,
                    binding.BlendKind,
                    out MxAnimationBlend1DDefinition blend1D,
                    out MxAnimationBlend2DDefinition blend2D))
            {
                logger.Warning("CharacterSpawn", "Locomotion blend definition was not found. path="
                    + animationSetDefinitionPath + " blendId=" + binding.LocomotionBlendId
                    + " blendKind=" + binding.BlendKind);
                return false;
            }

            locomotionDefinition = new CharacterRuntimeLocomotionDefinition(
                definition,
                binding.BlendKind,
                blend1D,
                blend2D);
            return true;
        }

        private static bool TryLoadSetDefinitions(
            CharacterRuntimeAnimationBinding binding,
            string animationSetDefinitionPath,
            IRuntimeLogger logger,
            out IReadOnlyList<MxAnimationSetDefinition> definitions)
        {
            definitions = null;
            if (!TryCreateAnimationSetDefinitionCacheKey(
                    animationSetDefinitionPath,
                    binding.ResourcePackageId,
                    logger,
                    out AnimationSetDefinitionCacheKey key))
            {
                return false;
            }

            if (AnimationSetDefinitionCache.TryGetValue(key, out definitions))
                return true;

            try
            {
                definitions = MxAnimationCompiledArtifactJson.LoadSetDefinitions(
                    File.ReadAllText(animationSetDefinitionPath),
                    binding.ResourcePackageId);
            }
            catch (Exception ex)
            {
                logger.Warning("CharacterSpawn", "Animation set definition parse failed. path="
                    + animationSetDefinitionPath + " error=" + ex.Message);
                definitions = null;
                return false;
            }

            if (AnimationSetDefinitionCache.Count >= MaxCachedAnimationSetDefinitionFiles)
                AnimationSetDefinitionCache.Clear();
            AnimationSetDefinitionCache[key] = definitions;
            return true;
        }

        private static string ResolveExistingRuntimeGeneratedFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            if (File.Exists(path))
                return path;

            string mirrorPath = ResolveRuntimeGeneratedMirrorPath(path);
            return !string.IsNullOrEmpty(mirrorPath) && File.Exists(mirrorPath)
                ? mirrorPath
                : string.Empty;
        }

        private static string ResolveRuntimeGeneratedMirrorPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalizedPath = path.Replace('\\', '/');
            if (!normalizedPath.StartsWith(GeneratedAssetsPrefix + "/", StringComparison.Ordinal))
                return string.Empty;

            string relativePath = normalizedPath.Substring(GeneratedAssetsPrefix.Length + 1);
            string combinedRelativePath = Path.Combine(
                GeneratedStreamingAssetsRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            return Path.Combine(Application.streamingAssetsPath, combinedRelativePath);
        }

        private static bool TryLoadClipRegistryEntries(
            string path,
            out IReadOnlyList<AnimationClipRegistryEntryData> entries)
        {
            entries = null;
            if (!TryCreateAnimationClipRegistryCacheKey(path, out AnimationClipRegistryCacheKey key))
                return false;

            if (AnimationClipRegistryCache.TryGetValue(key, out entries))
                return true;

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(path));
            }
            catch
            {
                return false;
            }

            var parsed = new List<AnimationClipRegistryEntryData>();
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            JArray clips = root["clips"] as JArray;
            for (int i = 0; clips != null && i < clips.Count; i++)
            {
                JObject clip = clips[i] as JObject;
                string resourceKey = ReadString(clip, "runtimeResourceKey");
                if (string.IsNullOrWhiteSpace(resourceKey))
                    continue;
                if (!emitted.Add(resourceKey))
                    continue;

                parsed.Add(new AnimationClipRegistryEntryData(
                    resourceKey,
                    ReadString(clip?["sourceSelection"] as JObject, "unityAssetPath"),
                    ReadString(clip, "hash"),
                    ReadString(clip, "clipId"),
                    ReadString(clip, "sourceClipName")));
            }

            if (AnimationClipRegistryCache.Count >= MaxCachedAnimationClipRegistryFiles)
                AnimationClipRegistryCache.Clear();
            AnimationClipRegistryCache[key] = parsed;
            entries = parsed;
            return true;
        }

        private static IReadOnlyDictionary<string, string> LoadImportedAnimationAssetPaths(
            string clipRegistryPath)
        {
            var assetPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            string configDirectory = Path.GetDirectoryName(clipRegistryPath);
            if (string.IsNullOrWhiteSpace(configDirectory))
                return assetPaths;

            string catalogPath = Path.Combine(configDirectory, "unity_resource_catalog.json");
            JObject root;
            try
            {
                if (!File.Exists(catalogPath))
                    return assetPaths;
                root = JObject.Parse(File.ReadAllText(catalogPath));
            }
            catch
            {
                return assetPaths;
            }

            JArray entries = root["entries"] as JArray;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                JObject providerData = entry?["providerData"] as JObject;
                string typeId = ReadString(entry, "type");
                if (string.IsNullOrWhiteSpace(typeId))
                    typeId = ReadString(entry, "typeId");
                if (!string.Equals(typeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal))
                    continue;

                string resourceKey = ReadString(entry, "id");
                if (string.IsNullOrWhiteSpace(resourceKey))
                    resourceKey = ReadString(entry, "packageResourceKey");
                if (string.IsNullOrWhiteSpace(resourceKey))
                    resourceKey = ReadString(providerData, "packageResourceKey");
                if (string.IsNullOrWhiteSpace(resourceKey) || assetPaths.ContainsKey(resourceKey))
                    continue;

                string assetPath = ReadString(providerData, "assetPath");
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = ReadString(providerData, "unityAssetPath");
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = ReadString(entry, "unityAssetPath");
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = ReadString(entry, "address");
                if (!IsStandaloneAnimationClipPath(assetPath))
                    continue;

                assetPaths.Add(resourceKey, assetPath.Replace('\\', '/'));
            }

            return assetPaths;
        }

        private static string ResolveRuntimeAnimationAssetPath(
            AnimationClipRegistryEntryData clip,
            IReadOnlyDictionary<string, string> importedAnimationAssetPaths)
        {
            if (importedAnimationAssetPaths != null
                && importedAnimationAssetPaths.TryGetValue(clip.ResourceKey, out string importedAssetPath)
                && IsStandaloneAnimationClipPath(importedAssetPath))
            {
                return importedAssetPath.Replace('\\', '/');
            }

            return IsStandaloneAnimationClipPath(clip.LegacyUnityAssetPath)
                ? clip.LegacyUnityAssetPath.Replace('\\', '/')
                : string.Empty;
        }

        private static bool IsStandaloneAnimationClipPath(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath)
                && string.Equals(Path.GetExtension(assetPath), ".anim", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryCreateAnimationSetDefinitionCacheKey(
            string path,
            string packageId,
            IRuntimeLogger logger,
            out AnimationSetDefinitionCacheKey key)
        {
            key = default;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists)
                {
                    logger.Warning("CharacterSpawn", "Animation set definition was not found. path=" + path);
                    return false;
                }

                key = new AnimationSetDefinitionCacheKey(
                    path,
                    packageId,
                    file.Length,
                    file.LastWriteTimeUtc.Ticks);
                return true;
            }
            catch (Exception ex)
            {
                logger.Warning("CharacterSpawn", "Animation set definition stat failed. path="
                    + path + " error=" + ex.Message);
                return false;
            }
        }

        private static bool TryCreateAnimationClipRegistryCacheKey(
            string path,
            out AnimationClipRegistryCacheKey key)
        {
            key = default;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists)
                    return false;

                key = new AnimationClipRegistryCacheKey(
                    path,
                    file.Length,
                    file.LastWriteTimeUtc.Ticks);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetCatalogEntryProviderData(
            ResourceCatalogEntry entry,
            string key,
            out string value)
        {
            value = string.Empty;
            if (entry == null
                || entry.ProviderData == null
                || string.IsNullOrWhiteSpace(key)
                || !entry.ProviderData.TryGetValue(key, out value))
            {
                value = string.Empty;
                return false;
            }

            return !string.IsNullOrWhiteSpace(value);
        }

        public static bool TryGetCatalogEntryUnityAssetPath(ResourceCatalogEntry entry, out string assetPath)
        {
            return TryGetCatalogEntryProviderData(entry, UnityAssetPathProviderDataKey, out assetPath);
        }

        public static bool TryGetCatalogEntrySourceClipName(ResourceCatalogEntry entry, out string sourceClipName)
        {
            return TryGetCatalogEntryProviderData(entry, SourceClipNameProviderDataKey, out sourceClipName);
        }

        public static bool TryGetCatalogEntryClipId(ResourceCatalogEntry entry, out string clipId)
        {
            return TryGetCatalogEntryProviderData(entry, ClipIdProviderDataKey, out clipId);
        }

        private static MxAnimationSetDefinition FindDefinition(
            IReadOnlyList<MxAnimationSetDefinition> definitions,
            string setId)
        {
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                MxAnimationSetDefinition candidate = definitions[i];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.SetId, setId, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool TryFindBlend(
            MxAnimationSetDefinition definition,
            string blendId,
            CharacterAnimationBlendKind blendKind,
            out MxAnimationBlend1DDefinition blend1D,
            out MxAnimationBlend2DDefinition blend2D)
        {
            blend1D = null;
            blend2D = null;
            if (definition == null || string.IsNullOrWhiteSpace(blendId))
                return false;

            int blend1DCount = 0;
            for (int i = 0; i < definition.Blend1DDefinitions.Count; i++)
            {
                MxAnimationBlend1DDefinition candidate = definition.Blend1DDefinitions[i];
                if (candidate != null && string.Equals(candidate.BlendId, blendId, StringComparison.Ordinal))
                {
                    blend1D = candidate;
                    blend1DCount++;
                }
            }
            int blend2DCount = 0;
            for (int i = 0; i < definition.Blend2DDefinitions.Count; i++)
            {
                MxAnimationBlend2DDefinition candidate = definition.Blend2DDefinitions[i];
                if (candidate != null && string.Equals(candidate.BlendId, blendId, StringComparison.Ordinal))
                {
                    blend2D = candidate;
                    blend2DCount++;
                }
            }

            return blendKind == CharacterAnimationBlendKind.Blend1D
                ? blend1DCount == 1 && blend2DCount == 0
                : blendKind == CharacterAnimationBlendKind.Blend2D
                    && blend2DCount == 1 && blend1DCount == 0;
        }

        private static MxAnimationSetDefinition ApplyFallbackActionBindings(
            MxAnimationSetDefinition definition,
            CharacterRuntimeAnimationFallbackActionBinding[] fallbackActionBindings)
        {
            if (definition == null || fallbackActionBindings == null || fallbackActionBindings.Length == 0)
                return definition;

            var actions = new List<MxAnimationActionBinding>(definition.Actions);
            bool changed = false;
            for (int i = 0; i < fallbackActionBindings.Length; i++)
            {
                CharacterRuntimeAnimationFallbackActionBinding fallback = fallbackActionBindings[i];
                if (!fallback.IsValid)
                    continue;
                if (ContainsAction(actions, fallback.ActionKey))
                    continue;
                if (!TryFindAction(actions, fallback.SourceActionKey, out MxAnimationActionBinding source))
                    continue;

                actions.Add(new MxAnimationActionBinding(
                    fallback.BindingId,
                    fallback.ActionKey,
                    source.Clip,
                    source.Layer,
                    source.PlaybackSpeed,
                    source.Loop,
                    source.AlignmentPolicy,
                    source.PresentationEvents,
                    fallback.FadeDurationSeconds, fallback.BindingLabel));
                changed = true;
            }

            if (!changed)
                return definition;

            return new MxAnimationSetDefinition(
                definition.SetId,
                definition.Version,
                definition.DefaultClip,
                definition.FallbackClip,
                actions,
                definition.Events,
                string.Empty,
                definition.Layers,
                definition.Warmup,
                definition.Blend1DDefinitions,
                definition.Blend2DDefinitions,
                definition.CompatibilityExpectation,
                definition.LocomotionClipCalibrations, definition.PreparedClipBindings);
        }

        private static bool ContainsAction(List<MxAnimationActionBinding> actions, string actionKey)
        {
            return TryFindAction(actions, actionKey, out _);
        }

        private static bool TryFindAction(
            List<MxAnimationActionBinding> actions,
            string actionKey,
            out MxAnimationActionBinding binding)
        {
            for (int i = 0; actions != null && i < actions.Count; i++)
            {
                MxAnimationActionBinding candidate = actions[i];
                if (candidate == null)
                    continue;
                if (string.Equals(candidate.ActionKey, actionKey, StringComparison.Ordinal))
                {
                    binding = candidate;
                    return true;
                }
            }

            binding = null;
            return false;
        }

        private readonly struct AnimationSetDefinitionCacheKey : IEquatable<AnimationSetDefinitionCacheKey>
        {
            public AnimationSetDefinitionCacheKey(
                string path,
                string packageId,
                long fileLength,
                long lastWriteUtcTicks)
            {
                Path = path ?? string.Empty;
                PackageId = packageId ?? string.Empty;
                FileLength = fileLength;
                LastWriteUtcTicks = lastWriteUtcTicks;
            }

            public string Path { get; }
            public string PackageId { get; }
            public long FileLength { get; }
            public long LastWriteUtcTicks { get; }

            public bool Equals(AnimationSetDefinitionCacheKey other)
            {
                return FileLength == other.FileLength
                    && LastWriteUtcTicks == other.LastWriteUtcTicks
                    && string.Equals(Path, other.Path, StringComparison.Ordinal)
                    && string.Equals(PackageId, other.PackageId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is AnimationSetDefinitionCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Path != null ? StringComparer.Ordinal.GetHashCode(Path) : 0;
                    hash = (hash * 397) ^ (PackageId != null ? StringComparer.Ordinal.GetHashCode(PackageId) : 0);
                    hash = (hash * 397) ^ FileLength.GetHashCode();
                    hash = (hash * 397) ^ LastWriteUtcTicks.GetHashCode();
                    return hash;
                }
            }
        }

        private readonly struct AnimationClipRegistryCacheKey : IEquatable<AnimationClipRegistryCacheKey>
        {
            public AnimationClipRegistryCacheKey(
                string path,
                long fileLength,
                long lastWriteUtcTicks)
            {
                Path = path ?? string.Empty;
                FileLength = fileLength;
                LastWriteUtcTicks = lastWriteUtcTicks;
            }

            public string Path { get; }
            public long FileLength { get; }
            public long LastWriteUtcTicks { get; }

            public bool Equals(AnimationClipRegistryCacheKey other)
            {
                return FileLength == other.FileLength
                    && LastWriteUtcTicks == other.LastWriteUtcTicks
                    && string.Equals(Path, other.Path, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is AnimationClipRegistryCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Path != null ? StringComparer.Ordinal.GetHashCode(Path) : 0;
                    hash = (hash * 397) ^ FileLength.GetHashCode();
                    hash = (hash * 397) ^ LastWriteUtcTicks.GetHashCode();
                    return hash;
                }
            }
        }

        private readonly struct AnimationClipRegistryEntryData
        {
            public AnimationClipRegistryEntryData(
                string resourceKey,
                string legacyUnityAssetPath,
                string hash,
                string clipId,
                string sourceClipName)
            {
                ResourceKey = resourceKey ?? string.Empty;
                LegacyUnityAssetPath = legacyUnityAssetPath ?? string.Empty;
                Hash = hash ?? string.Empty;
                ClipId = clipId ?? string.Empty;
                SourceClipName = sourceClipName ?? string.Empty;
            }

            public string ResourceKey { get; }
            public string LegacyUnityAssetPath { get; }
            public string Hash { get; }
            public string ClipId { get; }
            public string SourceClipName { get; }
        }

        private static string ReadString(JObject obj, string key, string fallback = "")
        {
            if (obj == null || string.IsNullOrWhiteSpace(key))
                return fallback ?? string.Empty;

            JToken token = obj[key];
            string value = token == null ? string.Empty : token.ToString();
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }
    }
}
