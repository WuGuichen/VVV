using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MxFramework.Authoring
{
    public readonly struct AuthoringResourceKeyParts
    {
        public AuthoringResourceKeyParts(string packageId, string type, string id, string variant)
        {
            PackageId = packageId ?? string.Empty;
            Type = type ?? string.Empty;
            Id = id ?? string.Empty;
            Variant = variant ?? string.Empty;
        }

        public string PackageId { get; }
        public string Type { get; }
        public string Id { get; }
        public string Variant { get; }
    }

    public static class AuthoringResourceNaming
    {
        public const string UnityProjectAssetStableIdPrefix = "unity.project.";
        public const string UnityAssetDatabaseStableIdPrefix = "unity.";
        public const string UnityOrphanStableIdPrefix = "unity.orphan.";
        public const string CharacterPackageStableIdPrefix = "resource.characterPackage.";
        public const string RuntimeCatalogStableIdPrefix = "runtime.";
        public const string GlobalProfileStableIdPrefix = "global.profile.";
        public const string FmodEventStableIdPrefix = "fmod.event.";
        public const string AudioEventDefinitionPrefix = "audio.event.";
        public const string AudioCuePrefix = "audio.cue.";

        public static string BuildProviderResourceId(string providerId, string stableId, string providerResourceKey)
        {
            return (providerId ?? string.Empty) + ":" + FirstNonEmpty(stableId, providerResourceKey);
        }

        public static string BuildNormalizedProviderResourceId(string providerId, string stableId, string providerResourceKey)
        {
            return (providerId ?? string.Empty) + ":" + NormalizeIdSegment(FirstNonEmpty(stableId, providerResourceKey), "resource");
        }

        public static string BuildUnityProjectAssetStableId(string unityAssetPath)
        {
            return UnityProjectAssetStableIdPrefix + NormalizeStableSegment(unityAssetPath);
        }

        public static string BuildUnityAssetDatabaseStableId(params string[] identityParts)
        {
            return UnityAssetDatabaseStableIdPrefix + NormalizeStableSegment(FirstNonEmpty(identityParts));
        }

        public static string BuildUnityAssetDatabaseSubAssetStableId(string parentIdentity, string subAssetIdentity)
        {
            return UnityAssetDatabaseStableIdPrefix + NormalizeStableSegment(FirstNonEmpty(parentIdentity, "asset") + "." + FirstNonEmpty(subAssetIdentity, "subasset"));
        }

        public static string BuildUnityOrphanStableId(string unityAssetPath)
        {
            return UnityOrphanStableIdPrefix + NormalizeStableSegment(unityAssetPath);
        }

        public static string BuildCharacterPackageScopeId(string packageId)
        {
            return string.IsNullOrWhiteSpace(packageId)
                ? "characterPackage"
                : "characterPackage:" + NormalizeIdSegment(packageId);
        }

        public static string BuildCharacterPackageFallbackStableId(string packageId, string localId)
        {
            return CharacterPackageStableIdPrefix + NormalizeIdSegment(packageId) + "." + NormalizeIdSegment(localId);
        }

        public static string BuildCharacterResourceKey(string packageId, string typeId, string localId, string variant = "")
        {
            string key = "char." + NormalizeIdSegment(packageId) + "." + NormalizeCharacterResourceTypeSegment(typeId) + "." + NormalizeIdSegment(localId);
            string variantSegment = string.IsNullOrWhiteSpace(variant) ? string.Empty : NormalizeIdSegment(variant);
            return !string.IsNullOrWhiteSpace(variantSegment) && variantSegment != "default"
                ? key + "." + variantSegment
                : key;
        }

        public static string BuildRuntimeCatalogStableId(string packageStableId, string runtimeResourceKey, string address)
        {
            return RuntimeCatalogStableIdPrefix + NormalizeStableSegment(FirstNonEmpty(packageStableId, runtimeResourceKey, address));
        }

        public static string BuildFmodEventStableId(string fmodEventPath)
        {
            return FmodEventStableIdPrefix + NormalizeStableSegment(NormalizeFmodEventPath(fmodEventPath, "event"));
        }

        public static string BuildAudioEventDefinitionId(string fmodEventPath)
        {
            return AudioEventDefinitionPrefix + NormalizeStableSegment(NormalizeFmodEventPath(fmodEventPath, "event"));
        }

        public static string BuildAudioCueId(string fmodEventPath)
        {
            return AudioCuePrefix + NormalizeStableSegment(NormalizeFmodEventPath(fmodEventPath, "event"));
        }

        public static string FormatProfileResourceKey(AuthoringResourceKeyParts key)
        {
            return (key.PackageId ?? string.Empty) + ":" +
                   (key.Type ?? string.Empty) + ":" +
                   (key.Id ?? string.Empty) + ":" +
                   (key.Variant ?? string.Empty);
        }

        public static void ApplyCanonicalIdentity(AuthoringResourceCollection collection)
        {
            if (collection == null || collection.Items == null)
                return;

            for (int i = 0; i < collection.Items.Count; i++)
                ApplyCanonicalIdentity(collection.Items[i]);
        }

        public static void ApplyCanonicalIdentity(AuthoringResourceItem item)
        {
            if (item == null)
                return;

            string canonicalIdentity = ChooseCanonicalIdentity(item);
            string canonicalName = ChooseCanonicalDisplayName(item, canonicalIdentity);
            item.CanonicalIdentity = canonicalIdentity;
            item.CanonicalName = canonicalName;
            item.TechnicalIdentity = ChooseTechnicalIdentity(item);
            AddMetadataIfMissing(item.Metadata, "canonicalIdentity", item.CanonicalIdentity);
            AddMetadataIfMissing(item.Metadata, "canonicalName", item.CanonicalName);
            AddMetadataIfMissing(item.Metadata, "technicalIdentity", item.TechnicalIdentity);
        }

        public static string ChooseCanonicalIdentity(AuthoringResourceItem item)
        {
            if (item == null)
                return string.Empty;

            foreach (string value in EnumerateIdentityCandidates(item, preferUserFacing: true))
            {
                if (!IsRawUnityGeneratedId(value) && !LooksLikeAssetPath(value))
                    return value;
            }

            return FirstNonEmpty(
                GetMetadata(item, "runtimeResourceKey"),
                GetMetadata(item, "packageResourceKey"),
                item.StableId,
                item.ResourceId,
                item.DisplayName);
        }

        public static string ChooseCanonicalDisplayName(AuthoringResourceItem item, string canonicalIdentity)
        {
            if (item == null)
                return string.Empty;

            string simplified = SimplifyResourceName(FirstNonEmpty(canonicalIdentity, item.DisplayName), item);
            return FirstNonEmpty(simplified, item.DisplayName, item.StableId, item.ResourceId);
        }

        public static string ChooseTechnicalIdentity(AuthoringResourceItem item)
        {
            if (item == null)
                return string.Empty;

            return FirstNonEmpty(
                GetMetadata(item, "resourceKey"),
                item.StableId,
                item.ResourceId,
                GetMetadata(item, "unityAssetGuid"),
                GetMetadata(item, "unityAssetPath"));
        }

        public static bool IsRawUnityGeneratedId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.StartsWith(UnityProjectAssetStableIdPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool LooksLikeAssetPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string text = value.Replace('\\', '/');
            if (text.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || text.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (text.IndexOf('/') >= 0)
                return HasKnownResourceExtension(text);

            return HasKnownResourceExtension(text) && text.Split('.').Length <= 2;
        }

        public static string SimplifyResourceName(string value, AuthoringResourceItem item = null)
        {
            string text = value != null ? value.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (LooksLikeAssetPath(text))
                return StripKnownResourceExtension(Path.GetFileName(text.Replace('\\', '/')));

            if (IsRawUnityGeneratedId(text))
            {
                string path = FirstNonEmpty(GetMetadata(item, "unityAssetPath"), GetMetadata(item, "sourcePath"));
                if (!string.IsNullOrWhiteSpace(path))
                    return StripKnownResourceExtension(Path.GetFileName(path.Replace('\\', '/')));
            }

            if (item != null && string.Equals(item.Kind, CharacterPackageResourceTypeIds.Animation, StringComparison.Ordinal) && text.IndexOf(".animation.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int dot = text.LastIndexOf('.');
                if (dot >= 0 && dot + 1 < text.Length)
                    return StripKnownResourceExtension(text.Substring(dot + 1));
            }

            return text;
        }

        public static string NormalizeIdSegment(string value)
        {
            return NormalizeIdSegment(value, "unknown");
        }

        public static string NormalizeIdSegment(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback;

            var builder = new StringBuilder(value.Length);
            bool lastWasDot = false;
            string lower = value.Trim().ToLowerInvariant();
            for (int i = 0; i < lower.Length; i++)
            {
                char c = lower[i];
                bool separator = c == '.' || c == '/' || c == '\\' || c == ':' || char.IsWhiteSpace(c);
                if (separator)
                {
                    if (!lastWasDot && builder.Length > 0)
                    {
                        builder.Append('.');
                        lastWasDot = true;
                    }
                    continue;
                }

                bool valid = (c >= 'a' && c <= 'z') ||
                             (c >= '0' && c <= '9') ||
                             c == '_' ||
                             c == '-';
                if (valid)
                {
                    builder.Append(c);
                    lastWasDot = false;
                }
            }

            string result = builder.ToString().Trim('.');
            return string.IsNullOrWhiteSpace(result)
                ? string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback
                : result;
        }

        public static string NormalizeStableSegment(string value)
        {
            return NormalizeIdSegment(value, "resource");
        }

        public static string GetFileDisplayName(string path, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                string fileName = Path.GetFileNameWithoutExtension(path.Replace('\\', '/'));
                if (!string.IsNullOrWhiteSpace(fileName))
                    return fileName;
            }

            return fallback ?? string.Empty;
        }

        public static string FirstNonEmpty(params string[] values)
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

        public static string NormalizeFmodEventPath(string path, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path))
                return fallback ?? string.Empty;

            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("event:/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("event:/".Length);
            else if (normalized.StartsWith("snapshot:/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("snapshot:/".Length);
            return normalized;
        }

        public static string GetFmodDisplayName(string path, string fallback)
        {
            string normalized = NormalizeFmodEventPath(path, fallback);
            int slash = normalized.LastIndexOf('/');
            if (slash >= 0 && slash + 1 < normalized.Length)
                return normalized.Substring(slash + 1);

            return string.IsNullOrWhiteSpace(normalized) ? fallback ?? string.Empty : normalized;
        }

        private static IEnumerable<string> EnumerateIdentityCandidates(AuthoringResourceItem item, bool preferUserFacing)
        {
            if (item == null)
                yield break;

            if (preferUserFacing)
            {
                string[] metadataKeys = { "runtimeResourceKey", "packageResourceKey", "audioCueId", "audioEventDefinitionId" };
                for (int i = 0; i < metadataKeys.Length; i++)
                {
                    string value = GetMetadata(item, metadataKeys[i]);
                    if (!string.IsNullOrWhiteSpace(value))
                        yield return value;
                }
            }

            if (item.ProviderBindings != null)
            {
                for (int i = 0; i < item.ProviderBindings.Count; i++)
                {
                    AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                    if (binding == null)
                        continue;

                    string[] bindingValues =
                    {
                        binding.RuntimeResourceKey,
                        binding.PackageResourceKey,
                        binding.ProviderResourceKey,
                        binding.FmodEventPath,
                        binding.UnityAssetPath
                    };
                    for (int valueIndex = 0; valueIndex < bindingValues.Length; valueIndex++)
                    {
                        if (!string.IsNullOrWhiteSpace(bindingValues[valueIndex]))
                            yield return bindingValues[valueIndex];
                    }
                }
            }

            string[] itemValues = { item.DisplayName, item.StableId, item.ResourceId };
            for (int i = 0; i < itemValues.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(itemValues[i]))
                    yield return itemValues[i];
            }
        }

        private static string NormalizeCharacterResourceTypeSegment(string typeId)
        {
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase))
                return "anim";

            return NormalizeIdSegment(typeId);
        }

        private static string GetMetadata(AuthoringResourceItem item, string key)
        {
            if (item == null || item.Metadata == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            return item.Metadata.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
        }

        private static void AddMetadataIfMissing(Dictionary<string, string> metadata, string key, string value)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value) || metadata.ContainsKey(key))
                return;

            metadata[key] = value;
        }

        private static bool HasKnownResourceExtension(string value)
        {
            string extension = Path.GetExtension(value ?? string.Empty);
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            switch (extension.ToLowerInvariant())
            {
                case ".anim":
                case ".asset":
                case ".controller":
                case ".overridecontroller":
                case ".prefab":
                case ".mat":
                case ".json":
                case ".glb":
                case ".gltf":
                case ".fbx":
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".wav":
                case ".ogg":
                case ".mp3":
                    return true;
                default:
                    return false;
            }
        }

        private static string StripKnownResourceExtension(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string extension = Path.GetExtension(value);
            return HasKnownResourceExtension(value) ? value.Substring(0, value.Length - extension.Length) : value;
        }
    }
}
