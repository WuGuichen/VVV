using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    internal static class CharacterActionRuntimeAnimationResourceDiscovery
    {
        private const string RuntimeCatalogRoot = "Assets/Config/MxFramework/ResourceCatalogs";
        private const string ResourceMappingProfilePath = "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json";
        private const string ClipIdProviderDataKey = "clipId";
        private const string SourceClipNameProviderDataKey = "sourceClipName";
        private const string UnityAssetPathProviderDataKey = "unityAssetPath";

        public static ResourceCatalogEntry[] DiscoverRuntimeAnimationClipEntries(
            string providerId,
            IEnumerable<string> labels = null)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return Array.Empty<ResourceCatalogEntry>();

            var records = new Dictionary<string, RuntimeAnimationClipRecord>(StringComparer.Ordinal);
            LoadResourceMappingProfile(records);
            LoadRuntimeCatalogs(records);

            var sorted = new List<RuntimeAnimationClipRecord>(records.Values);
            sorted.Sort(CompareRecords);

            var result = new List<ResourceCatalogEntry>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                RuntimeAnimationClipRecord record = sorted[i];
                if (!record.IsRuntimeReadyAnimation)
                    continue;

                string sourceClipName = FirstNonEmpty(record.SourceClipName, Path.GetFileNameWithoutExtension(record.AssetPath));
                string clipId = FirstNonEmpty(record.ClipId, NormalizeClipId(sourceClipName), NormalizeClipId(record.RuntimeResourceKey));
                result.Add(new ResourceCatalogEntry(
                    record.RuntimeResourceKey,
                    ResourceTypeIds.AnimationClip,
                    providerId,
                    record.RuntimeResourceKey,
                    variant: record.Variant,
                    packageId: record.PackageId,
                    labels: labels ?? CreateDefaultLabels(record),
                    hash: record.Hash,
                    providerData: new Dictionary<string, string>
                    {
                        { ClipIdProviderDataKey, clipId },
                        { SourceClipNameProviderDataKey, sourceClipName },
                        { UnityAssetPathProviderDataKey, record.AssetPath },
                        { "resourceManagerAddress", FirstNonEmpty(record.Address, record.RuntimeResourceKey) },
                        { "resourceManagerSource", record.Source }
                    }));
            }

            return result.ToArray();
        }

        private static void LoadResourceMappingProfile(Dictionary<string, RuntimeAnimationClipRecord> records)
        {
            string profilePath = ToFileSystemPath(ResourceMappingProfilePath);
            if (!File.Exists(profilePath))
                return;

            JObject document;
            try
            {
                document = JObject.Parse(File.ReadAllText(profilePath));
            }
            catch
            {
                return;
            }

            JArray entries = document["entries"] as JArray;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                if (entry == null || !string.Equals(ReadString(entry, "deliveryIntent"), "internal", StringComparison.OrdinalIgnoreCase))
                    continue;

                JObject resourceKey = entry["resourceKey"] as JObject;
                JObject source = entry["sourceLocator"] as JObject;
                JObject providerData = entry["providerData"] as JObject;
                string runtimeKey = ReadString(resourceKey, "id");
                string assetPath = NormalizeProjectPath(FirstNonEmpty(
                    ReadString(source, "cachedPath"),
                    ReadString(providerData, "unityAssetPath")));

                if (!IsAnimationClipAsset(assetPath, resourceKey, providerData))
                    continue;

                Put(records, new RuntimeAnimationClipRecord
                {
                    RuntimeResourceKey = runtimeKey,
                    PackageId = ReadString(resourceKey, "packageId"),
                    Variant = ReadString(resourceKey, "variant"),
                    AssetPath = assetPath,
                    Address = runtimeKey,
                    Hash = ReadString(source, "contentHash"),
                    ClipId = FirstNonEmpty(ReadString(providerData, "subClipId"), ReadString(providerData, "clipId")),
                    SourceClipName = FirstNonEmpty(ReadString(providerData, "clipName"), ReadString(providerData, "subClipName")),
                    RuntimeReady = true,
                    Source = "resourceMapping"
                });
            }
        }

        private static void LoadRuntimeCatalogs(Dictionary<string, RuntimeAnimationClipRecord> records)
        {
            string catalogRoot = ToFileSystemPath(RuntimeCatalogRoot);
            if (!Directory.Exists(catalogRoot) || records.Count == 0)
                return;

            string[] files = Directory.GetFiles(catalogRoot, "*.json", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length; i++)
                LoadRuntimeCatalog(files[i], records);
        }

        private static void LoadRuntimeCatalog(string path, Dictionary<string, RuntimeAnimationClipRecord> records)
        {
            JObject document;
            try
            {
                document = JObject.Parse(File.ReadAllText(path));
            }
            catch
            {
                return;
            }

            string catalogPackageId = ReadString(document, "packageId");
            JArray entries = document["entries"] as JArray;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                JObject providerData = entry?["providerData"] as JObject;
                string runtimeKey = ReadString(entry, "id");
                string assetPath = NormalizeProjectPath(FirstNonEmpty(
                    ReadString(providerData, "unityAssetPath"),
                    ReadString(providerData, "assetPath"),
                    ReadString(entry, "unityAssetPath")));

                if (string.IsNullOrWhiteSpace(runtimeKey)
                    || !records.TryGetValue(runtimeKey, out RuntimeAnimationClipRecord existing)
                    || !IsAnimationClipCatalogEntry(entry, assetPath))
                {
                    continue;
                }

                Put(records, new RuntimeAnimationClipRecord
                {
                    RuntimeResourceKey = runtimeKey,
                    PackageId = FirstNonEmpty(ReadString(entry, "packageId"), catalogPackageId, existing.PackageId),
                    Variant = FirstNonEmpty(ReadString(entry, "variant"), existing.Variant),
                    AssetPath = FirstNonEmpty(assetPath, existing.AssetPath),
                    Address = FirstNonEmpty(ReadString(entry, "address"), existing.Address, runtimeKey),
                    Hash = FirstNonEmpty(ReadString(entry, "hash"), existing.Hash),
                    ClipId = FirstNonEmpty(ReadString(providerData, "subClipId"), ReadString(providerData, "clipId"), existing.ClipId),
                    SourceClipName = FirstNonEmpty(
                        ReadString(providerData, "clipName"),
                        ReadString(providerData, "subClipName"),
                        existing.SourceClipName),
                    RuntimeReady = existing.RuntimeReady,
                    Source = existing.Source + "+runtimeCatalog"
                });
            }
        }

        private static void Put(
            Dictionary<string, RuntimeAnimationClipRecord> records,
            RuntimeAnimationClipRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.RuntimeResourceKey) || string.IsNullOrWhiteSpace(record.AssetPath))
                return;

            record.RuntimeResourceKey = record.RuntimeResourceKey.Trim();
            record.AssetPath = NormalizeProjectPath(record.AssetPath);
            records[record.RuntimeResourceKey] = record;
        }

        private static bool IsAnimationClipAsset(
            string assetPath,
            JObject resourceKey,
            JObject providerData)
        {
            if (string.IsNullOrWhiteSpace(assetPath)
                || !assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string assetType = ReadString(providerData, "assetType");
            string usage = ReadString(providerData, "usage");
            string typeId = FirstNonEmpty(ReadString(resourceKey, "typeId"), ReadString(resourceKey, "type"));
            return string.Equals(assetType, ResourceTypeIds.AnimationClip, StringComparison.Ordinal)
                || string.Equals(usage, "animationClip", StringComparison.Ordinal)
                || string.Equals(typeId, "animation", StringComparison.Ordinal)
                || string.Equals(typeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal);
        }

        private static bool IsAnimationClipCatalogEntry(JObject entry, string assetPath)
        {
            if (entry == null
                || string.IsNullOrWhiteSpace(assetPath)
                || !assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string typeId = FirstNonEmpty(ReadString(entry, "typeId"), ReadString(entry, "type"));
            return string.Equals(typeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal);
        }

        private static string[] CreateDefaultLabels(RuntimeAnimationClipRecord record)
        {
            return new[]
            {
                "CharacterActionPreview",
                "CharacterAnimation",
                "ResourceManagerRuntime"
            };
        }

        private static int CompareRecords(RuntimeAnimationClipRecord left, RuntimeAnimationClipRecord right)
        {
            int sourceName = string.Compare(left.SourceClipName, right.SourceClipName, StringComparison.Ordinal);
            return sourceName != 0
                ? sourceName
                : string.Compare(left.RuntimeResourceKey, right.RuntimeResourceKey, StringComparison.Ordinal);
        }

        private static string NormalizeClipId(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            bool previousSeparator = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                    previousSeparator = false;
                }
                else if (!previousSeparator)
                {
                    builder.Append('_');
                    previousSeparator = true;
                }
            }

            return builder.ToString().Trim('_');
        }

        private static bool ReadBool(JObject obj, string key)
        {
            JToken token = obj?[key];
            if (token == null)
                return false;
            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();

            return bool.TryParse(token.ToString(), out bool value) && value;
        }

        private static string ReadString(JObject obj, string key)
        {
            return obj?[key]?.Value<string>() ?? string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }

        private static string NormalizeProjectPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string ToFileSystemPath(string projectOrAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(projectOrAbsolutePath))
                return string.Empty;
            if (Path.IsPathRooted(projectOrAbsolutePath))
                return Path.GetFullPath(projectOrAbsolutePath);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, projectOrAbsolutePath));
        }

        private sealed class RuntimeAnimationClipRecord
        {
            public string RuntimeResourceKey { get; set; } = string.Empty;
            public string PackageId { get; set; } = string.Empty;
            public string Variant { get; set; } = string.Empty;
            public string AssetPath { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string Hash { get; set; } = string.Empty;
            public string ClipId { get; set; } = string.Empty;
            public string SourceClipName { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public bool RuntimeReady { get; set; }
            public bool IsRuntimeReadyAnimation => RuntimeReady
                && !string.IsNullOrWhiteSpace(RuntimeResourceKey)
                && !string.IsNullOrWhiteSpace(AssetPath);
        }
    }
}
