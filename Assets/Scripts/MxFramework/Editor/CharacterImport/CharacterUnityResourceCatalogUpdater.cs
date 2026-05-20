using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    internal static class CharacterUnityResourceCatalogUpdater
    {
        public static void RefreshCatalog(string importedPackageRoot)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            string catalogPath = root + "/config/unity_resource_catalog.json";
            if (!File.Exists(catalogPath))
                return;

            JObject catalog;
            try
            {
                catalog = JObject.Parse(File.ReadAllText(catalogPath));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("MxFramework Character Import: failed to read Unity resource catalog: " + ex.Message);
                return;
            }

            bool changed = false;
            JArray entries = catalog["entries"] as JArray;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    JObject entry = entries[i] as JObject;
                    if (entry == null)
                        continue;

                    changed |= RefreshEntry(entry);
                }
            }

            if (!changed)
                return;

            File.WriteAllText(catalogPath, catalog.ToString(Formatting.Indented) + "\n");
            AssetDatabase.ImportAsset(catalogPath, ImportAssetOptions.ForceUpdate);
        }

        private static bool RefreshEntry(JObject entry)
        {
            JObject providerData = entry["providerData"] as JObject;
            string assetPath = ReadString(providerData, "assetPath");
            if (string.IsNullOrWhiteSpace(assetPath))
                assetPath = ReadString(providerData, "unityAssetPath");
            if (string.IsNullOrWhiteSpace(assetPath))
                assetPath = ReadString(entry, "unityAssetPath");
            if (string.IsNullOrWhiteSpace(assetPath))
                assetPath = ReadString(entry, "address");

            string status = "UnityMissing";
            string guid = string.Empty;
            string mainObjectType = string.Empty;
            string importerKind = GuessImporterKind(ReadString(providerData, "sourceFormat"), assetPath);

            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                guid = AssetDatabase.AssetPathToGUID(assetPath);
                Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                mainObjectType = type != null ? type.Name : string.Empty;

                if (!string.IsNullOrWhiteSpace(guid) && type != null)
                    status = CanInstantiate(entry, assetPath) ? "Imported" : "Copied";
            }

            bool changed = false;
            changed |= SetString(entry, "unityAssetGuid", guid);
            changed |= SetString(entry, "unityAssetPath", assetPath);
            changed |= SetString(entry, "unityMainObjectType", mainObjectType);
            changed |= SetString(entry, "importerKind", importerKind);
            changed |= SetString(entry, "importStatus", status);

            if (providerData == null)
            {
                providerData = new JObject();
                entry["providerData"] = providerData;
                changed = true;
            }

            changed |= SetString(providerData, "unityAssetGuid", guid);
            changed |= SetString(providerData, "unityAssetPath", assetPath);
            changed |= SetString(providerData, "unityMainObjectType", mainObjectType);
            changed |= SetString(providerData, "importerKind", importerKind);
            changed |= SetString(providerData, "importStatus", status);
            return changed;
        }

        private static bool CanInstantiate(JObject entry, string assetPath)
        {
            string type = ReadString(entry, "type");
            if (!string.Equals(type, "GameObject", StringComparison.OrdinalIgnoreCase))
                return true;

            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
        }

        private static string GuessImporterKind(string sourceFormat, string assetPath)
        {
            string extension = Path.GetExtension(assetPath ?? string.Empty).TrimStart('.').ToLowerInvariant();
            string format = string.IsNullOrWhiteSpace(sourceFormat) ? extension : sourceFormat.ToLowerInvariant();
            if (format == "glb" || format == "gltf")
                return "unity-gltf";
            if (format == "fbx")
                return "unity-fbx";
            if (format == "png" || format == "jpg" || format == "jpeg" || format == "tga")
                return "unity-texture";
            return string.IsNullOrWhiteSpace(format) ? "unity-asset" : "unity-" + format;
        }

        private static string NormalizeProjectPath(string path)
        {
            string full = Path.GetFullPath(path ?? string.Empty).Replace('\\', '/');
            string project = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
            string result = full.StartsWith(project, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(project.Length)
                : (path ?? string.Empty).Replace('\\', '/');
            return result.TrimEnd('/');
        }

        private static string ReadString(JObject obj, string key)
        {
            return obj == null ? string.Empty : (obj[key]?.Value<string>() ?? string.Empty);
        }

        private static bool SetString(JObject obj, string key, string value)
        {
            value ??= string.Empty;
            if (string.Equals(ReadString(obj, key), value, StringComparison.Ordinal))
                return false;

            obj[key] = value;
            return true;
        }
    }
}
