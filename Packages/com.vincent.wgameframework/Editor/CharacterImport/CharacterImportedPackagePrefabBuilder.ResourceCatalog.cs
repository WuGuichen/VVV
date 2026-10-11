using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterImport
{
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        private static void AddImportedCatalogPreviewInfos(
            ResourceCatalog catalog,
            Dictionary<string, CharacterRuntimePose> wrapperPoses,
            Dictionary<string, ResourcePreviewInfo> result)
        {
            if (catalog == null)
                return;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                bool isGameObject = string.Equals(entry.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal);
                bool isTexture = string.Equals(entry.TypeId, ResourceTypeIds.Texture2D, StringComparison.Ordinal);
                if (!isGameObject && !isTexture)
                    continue;

                string assetGuid = GetUnityAssetGuid(entry);
                string assetPath = ResolveUnityAssetPath(entry, assetGuid, requireGameObject: isGameObject);
                string importStatus = GetImportStatus(entry);
                bool canLoadAsset = isGameObject ? CanLoadGameObject(assetPath) : CanLoadTexture2D(assetPath);
                bool isImportReady = canLoadAsset || IsUnityCatalogEntryImported(entry);
                if (canLoadAsset && !string.Equals(importStatus, "Imported", StringComparison.OrdinalIgnoreCase))
                    importStatus = "Imported";

                string key = GetProviderData(entry, "packageResourceKey");
                if (string.IsNullOrWhiteSpace(key))
                    key = entry.Id;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                CharacterRuntimePose wrapperPose = wrapperPoses != null && wrapperPoses.TryGetValue(key, out CharacterRuntimePose pose)
                    ? pose
                    : ParseProviderDataPose(entry, "modelWrapperPose") ?? CreateDefaultPose();
                string usage = GetProviderData(entry, "usage");
                if (string.IsNullOrWhiteSpace(usage))
                    usage = InferUsage(entry);

                result[key] = new ResourcePreviewInfo(
                    key,
                    usage,
                    assetPath,
                    assetGuid,
                    importStatus,
                    isImportReady,
                    GetProviderData(entry, "avatarSourceAssetPath"),
                    GetProviderData(entry, "avatarSourceGuid"),
                    FirstNonEmpty(GetProviderData(entry, "avatarSourceHumanoidStatus"), GetProviderData(entry, "avatarSourceStatus")),
                    wrapperPose,
                    false,
                    ParseModelNodePaths(GetProviderData(entry, "hiddenRendererNodePaths")),
                    ParseModelNodePaths(
                        GetProviderData(entry, "embeddedWeaponNodePaths"),
                        GetProviderData(entry, "embeddedWeaponNodePath")),
                    CreateResourceDiagnostic(entry, assetPath, assetGuid, importStatus, isImportReady));
            }
        }

        private static void AddGlobalProfilePreviewInfos(Dictionary<string, ResourcePreviewInfo> result)
        {
            if (result == null || !File.Exists(ResourceMappingProfilePath))
                return;

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(ResourceMappingProfilePath));
            }
            catch
            {
                return;
            }

            JArray entries = root["entries"] as JArray;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                JObject resourceKey = entry?["resourceKey"] as JObject;
                JObject source = entry?["sourceLocator"] as JObject;
                JObject providerData = entry?["providerData"] as JObject;
                if (entry == null || resourceKey == null)
                    continue;

                string key = ReadString(resourceKey, "id");
                if (string.IsNullOrWhiteSpace(key) || result.ContainsKey(key))
                    continue;

                string type = FirstNonEmpty(ReadString(resourceKey, "typeId"), ReadString(resourceKey, "type"));
                if (!string.Equals(type, ResourceTypeIds.GameObject, StringComparison.OrdinalIgnoreCase))
                    continue;

                string assetGuid = FirstNonEmpty(ReadString(source, "unityGuid"), ReadString(providerData, "unityAssetGuid"));
                string assetPath = ResolveGlobalProfileAssetPath(source, providerData, assetGuid);
                bool canLoadGameObject = CanLoadGameObject(assetPath);
                string importStatus = canLoadGameObject ? "Imported" : "NeedsUnityImport";
                string usage = FirstNonEmpty(ReadString(providerData, "usage"), InferGlobalProfileUsage(entry));
                CharacterRuntimePose wrapperPose = ParseProviderDataPose(providerData, "modelWrapperPose") ?? CreateDefaultPose();
                bool wrapperPoseBakedIntoAsset = IsGlobalProfileWrapperPrefab(providerData, assetPath);

                result[key] = new ResourcePreviewInfo(
                    key,
                    usage,
                    assetPath,
                    assetGuid,
                    importStatus,
                    canLoadGameObject,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    wrapperPose,
                    wrapperPoseBakedIntoAsset,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    CreateGlobalProfileResourceDiagnostic(key, assetPath, assetGuid, importStatus, canLoadGameObject));
            }
        }

        private static bool IsGlobalProfileWrapperPrefab(JObject providerData, string assetPath)
        {
            if (providerData == null)
                return false;
            if (!string.Equals(ReadString(providerData, "usesUnityWrapperPrefab"), "true", StringComparison.OrdinalIgnoreCase))
                return false;
            return !string.IsNullOrWhiteSpace(assetPath) && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveGlobalProfileAssetPath(JObject source, JObject providerData, string assetGuid)
        {
            if (!string.IsNullOrWhiteSpace(assetGuid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (CanUseUnityAssetPath(guidPath, requireGameObject: true))
                    return guidPath;
            }

            string assetPath = FirstNonEmpty(
                ReadString(source, "cachedPath"),
                ReadString(providerData, "generatedAssetPath"),
                ReadString(providerData, "unityAssetPath"));
            return CanUseUnityAssetPath(assetPath, requireGameObject: true) ? assetPath : string.Empty;
        }

        private static CharacterRuntimePose? ParseProviderDataPose(JObject providerData, string key)
        {
            string value = ReadString(providerData, key);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            try
            {
                return ParsePose(JObject.Parse(value));
            }
            catch
            {
                return null;
            }
        }

        private static string InferGlobalProfileUsage(JObject entry)
        {
            JArray labels = entry?["labels"] as JArray;
            for (int i = 0; labels != null && i < labels.Count; i++)
            {
                string label = labels[i]?.ToString() ?? string.Empty;
                const string usagePrefix = "usage.";
                if (label.StartsWith(usagePrefix, StringComparison.OrdinalIgnoreCase))
                    return label.Substring(usagePrefix.Length);
            }

            return string.Empty;
        }

        private static string CreateGlobalProfileResourceDiagnostic(
            string key,
            string assetPath,
            string assetGuid,
            string importStatus,
            bool isImportReady)
        {
            if (isImportReady)
                return "Imported public resource: " + key;

            if (string.IsNullOrWhiteSpace(assetPath))
                return "Public resource is registered but no loadable Unity GameObject asset was found. key=" + key + " guid=" + assetGuid;

            return "Public resource is not ready for prefab preview. key=" + key + " asset=" + assetPath + " status=" + importStatus;
        }

        private static string ResolveUnityAssetPath(ResourceCatalogEntry entry, string assetGuid, bool requireGameObject)
        {
            if (!string.IsNullOrWhiteSpace(assetGuid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (CanUseUnityAssetPath(guidPath, requireGameObject))
                    return guidPath;
            }

            string assetPath = GetProviderData(entry, "unityAssetPath");
            if (CanUseUnityAssetPath(assetPath, requireGameObject))
                return assetPath;

            assetPath = GetProviderData(entry, "unityRuntimeAssetPath");
            if (CanUseUnityAssetPath(assetPath, requireGameObject))
                return assetPath;

            assetPath = GetProviderData(entry, "generatedAssetPath");
            if (CanUseUnityAssetPath(assetPath, requireGameObject))
                return assetPath;

            assetPath = GetProviderData(entry, "assetPath");
            if (CanUseUnityAssetPath(assetPath, requireGameObject))
                return assetPath;

            if (CanUseUnityAssetPath(entry.Address, requireGameObject))
                return entry.Address;

            return string.Empty;
        }

        private static bool CanUseUnityAssetPath(string assetPath, bool requireGameObject)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            return requireGameObject
                ? CanLoadGameObject(assetPath)
                : AssetDatabase.GetMainAssetTypeAtPath(assetPath) != null;
        }

        private static bool CanLoadGameObject(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath)
                && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
        }

        private static bool CanLoadTexture2D(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath)
                && AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) != null;
        }

        private static bool IsUnityCatalogEntryImported(ResourceCatalogEntry entry)
        {
            return string.Equals(GetImportStatus(entry), "Imported", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetImportStatus(ResourceCatalogEntry entry)
        {
            string status = GetProviderData(entry, "importStatus");
            return !string.IsNullOrWhiteSpace(status) ? status : string.Empty;
        }

        private static string GetUnityAssetGuid(ResourceCatalogEntry entry)
        {
            string assetGuid = GetProviderData(entry, "unityAssetGuid");
            if (string.IsNullOrWhiteSpace(assetGuid))
                assetGuid = GetProviderData(entry, "assetGuid");
            if (string.IsNullOrWhiteSpace(assetGuid))
                assetGuid = GetProviderData(entry, "assetGUID");
            if (string.IsNullOrWhiteSpace(assetGuid))
                assetGuid = GetProviderData(entry, "guid");
            return assetGuid;
        }

        private static string GetProviderData(ResourceCatalogEntry entry, string key)
        {
            return entry != null
                && entry.ProviderData != null
                && entry.ProviderData.TryGetValue(key, out string value)
                ? value
                : string.Empty;
        }

        private static string[] ParseModelNodePaths(params string[] configuredValues)
        {
            if (configuredValues == null || configuredValues.Length == 0)
                return Array.Empty<string>();

            var paths = new List<string>();
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            char[] separators = { ';', ',', '\r', '\n' };
            for (int valueIndex = 0; valueIndex < configuredValues.Length; valueIndex++)
            {
                string configuredValue = configuredValues[valueIndex];
                if (string.IsNullOrWhiteSpace(configuredValue))
                    continue;

                string[] candidates = configuredValue.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    string path = candidates[candidateIndex]
                        .Replace('\\', '/')
                        .Trim()
                        .Trim('/');
                    if (!string.IsNullOrWhiteSpace(path) && uniquePaths.Add(path))
                        paths.Add(path);
                }
            }

            return paths.ToArray();
        }

        private static CharacterRuntimePose? ParseProviderDataPose(ResourceCatalogEntry entry, string key)
        {
            string value = GetProviderData(entry, key);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            try
            {
                return ParsePose(JObject.Parse(value));
            }
            catch
            {
                return null;
            }
        }

        private static bool HasLabel(ResourceCatalogEntry entry, string value)
        {
            if (entry == null || entry.Labels == null || string.IsNullOrWhiteSpace(value))
                return false;

            for (int i = 0; i < entry.Labels.Count; i++)
            {
                if (string.Equals(entry.Labels[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string InferUsage(ResourceCatalogEntry entry)
        {
            if (entry == null)
                return string.Empty;

            for (int i = 0; i < entry.Labels.Count; i++)
            {
                string label = entry.Labels[i] ?? string.Empty;
                const string usagePrefix = "character.usage.";
                if (label.StartsWith(usagePrefix, StringComparison.OrdinalIgnoreCase))
                    return label.Substring(usagePrefix.Length);
                if (label.Equals("character.model", StringComparison.OrdinalIgnoreCase))
                    return "characterModel";
                if (label.Equals("weapon.model", StringComparison.OrdinalIgnoreCase))
                    return "weaponModel";
            }

            return string.Empty;
        }

        private static ResourcePreviewInfo FindResource(Dictionary<string, ResourcePreviewInfo> resources, string usage, string resourceKey)
        {
            if (!string.IsNullOrWhiteSpace(resourceKey) && resources.TryGetValue(resourceKey, out ResourcePreviewInfo exact))
                return exact;
            if (!string.IsNullOrWhiteSpace(resourceKey))
                return null;

            foreach (ResourcePreviewInfo resource in resources.Values)
            {
                if (string.Equals(resource.Usage, usage, StringComparison.OrdinalIgnoreCase))
                    return resource;
            }

            return null;
        }

        private static bool ValidatePreviewResources(Dictionary<string, ResourcePreviewInfo> resources)
        {
            bool isValid = true;
            foreach (ResourcePreviewInfo resource in resources.Values)
            {
                if (resource.IsImportReady)
                    continue;
                if (!string.Equals(resource.Usage, "characterModel", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(resource.Usage, "weaponModel", StringComparison.OrdinalIgnoreCase))
                    continue;

                Debug.LogWarning("MxFramework Character Preview: imported model is not ready; prefab generation will use placeholders or skip default weapon mounting. "
                    + FormatResourceDiagnostic(resource));
            }

            return isValid;
        }

        private static CharacterWeaponAttachmentRuntimeBinding[] GetRuntimeMountedWeaponAttachments(
            CharacterWeaponAttachmentRuntimeBinding[] attachments,
            Dictionary<string, GameObject> weaponPrefabs)
        {
            if (attachments == null || attachments.Length == 0)
                return Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();

            var result = new List<CharacterWeaponAttachmentRuntimeBinding>();
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (ShouldMountWeaponAttachment(attachment, weaponPrefabs))
                    result.Add(attachment);
            }

            return result.ToArray();
        }

        private static bool ShouldMountWeaponAttachment(
            CharacterWeaponAttachmentRuntimeBinding attachment,
            Dictionary<string, GameObject> weaponPrefabs)
        {
            return attachment != null
                && (HasRuntimeWeaponResource(attachment)
                    || !string.IsNullOrWhiteSpace(attachment.PreviewResourceKey)
                    && weaponPrefabs != null
                    && weaponPrefabs.ContainsKey(GetAttachmentKey(attachment)));
        }

        private static CharacterRuntimePose CreateDefaultPose()
        {
            return new CharacterRuntimePose(string.Empty, string.Empty, default, default, new CharacterRuntimeVector3(1f, 1f, 1f));
        }

        private static string CreateResourceDiagnostic(
            ResourceCatalogEntry entry,
            string assetPath,
            string assetGuid,
            string importStatus,
            bool isImportReady)
        {
            if (entry == null)
                return "resource=<missing catalog entry>";
            if (isImportReady)
                return string.Empty;

            string candidatePath = !string.IsNullOrWhiteSpace(assetPath)
                ? assetPath
                : FirstNonEmpty(
                    GetProviderData(entry, "unityAssetPath"),
                    GetProviderData(entry, "assetPath"),
                    entry.Address);
            string status = string.IsNullOrWhiteSpace(importStatus) ? "MissingImportStatus" : importStatus;
            return "resource=" + entry.Id
                + " status=" + status
                + " assetPath=" + (string.IsNullOrWhiteSpace(candidatePath) ? "<unresolved>" : candidatePath)
                + FormatAssetGuid(assetGuid);
        }

        private static string FormatResourceDiagnostic(ResourcePreviewInfo resource)
        {
            if (resource == null)
                return "resource=<missing catalog entry>; source=unity_resource_catalog";
            if (!string.IsNullOrWhiteSpace(resource.Diagnostic))
                return resource.Diagnostic;

            return "resource=" + resource.Key
                + " status=" + (string.IsNullOrWhiteSpace(resource.ImportStatus) ? "Imported" : resource.ImportStatus)
                + " assetPath=" + (string.IsNullOrWhiteSpace(resource.AssetPath) ? "<unresolved>" : resource.AssetPath)
                + FormatAssetGuid(resource.AssetGuid);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }
    }
}
