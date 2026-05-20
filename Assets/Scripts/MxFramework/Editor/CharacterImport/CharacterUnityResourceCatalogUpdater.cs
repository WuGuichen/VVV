using System;
using System.Collections.Generic;
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
            changed |= SetString(catalog, "format", "mx.characterUnityResourceCatalog.v1");
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

            changed |= RefreshOrphanedUnityAssets(root, catalog, entries);

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

            string status = "Failed";
            string guid = string.Empty;
            string mainObjectType = string.Empty;
            string importerKind = GuessImporterKind(ReadString(providerData, "sourceFormat"), assetPath);
            var diagnostics = new JArray();

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                AddDiagnostic(
                    diagnostics,
                    "Error",
                    "CHARPKG_UNITY_ASSET_PATH_MISSING",
                    "Unity resource catalog entry has no Unity asset path.",
                    ReadString(providerData, "sourceRelativePath"),
                    "unityAssetPath");
            }
            else if (!File.Exists(assetPath))
            {
                status = "UnityMissing";
                AddDiagnostic(
                    diagnostics,
                    "Error",
                    "CHARPKG_UNITY_ASSET_MISSING",
                    "Unity asset path does not exist.",
                    assetPath,
                    "unityAssetPath");
            }
            else
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                guid = AssetDatabase.AssetPathToGUID(assetPath);
                Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                mainObjectType = type != null ? type.Name : string.Empty;

                if (string.IsNullOrWhiteSpace(guid) || type == null)
                {
                    status = "Failed";
                    AddDiagnostic(
                        diagnostics,
                        "Error",
                        "CHARPKG_UNITY_IMPORT_FAILED",
                        "Unity AssetDatabase did not produce a main asset for this path.",
                        assetPath,
                        "unityMainObjectType");
                }
                else if (!CanInstantiate(entry, assetPath))
                {
                    status = "Placeholder";
                    AddDiagnostic(
                        diagnostics,
                        "Warning",
                        "CHARPKG_UNITY_ASSET_NOT_INSTANTIABLE",
                        "Unity imported the asset, but it cannot be instantiated as the expected GameObject.",
                        assetPath,
                        "unityAssetPath");
                }
                else
                {
                    status = "Imported";
                }
            }

            bool changed = false;
            changed |= SetString(entry, "packageResourceKey", FirstNonEmpty(ReadString(entry, "packageResourceKey"), ReadString(providerData, "packageResourceKey"), entry.Value<string>("id") ?? string.Empty));
            changed |= SetString(entry, "stableId", FirstNonEmpty(ReadString(entry, "stableId"), ReadString(providerData, "stableId")));
            changed |= SetString(entry, "usage", FirstNonEmpty(ReadString(entry, "usage"), ReadString(providerData, "usage")));
            changed |= SetString(entry, "sourceRelativePath", FirstNonEmpty(ReadString(entry, "sourceRelativePath"), ReadString(providerData, "sourceRelativePath")));
            changed |= SetString(entry, "sourceFormat", FirstNonEmpty(ReadString(entry, "sourceFormat"), ReadString(providerData, "sourceFormat")));
            changed |= SetString(entry, "declaredContentHash", FirstNonEmpty(ReadString(entry, "declaredContentHash"), ReadString(providerData, "declaredContentHash"), ReadString(entry, "hash")));
            changed |= SetString(entry, "contentHash", FirstNonEmpty(ReadString(entry, "contentHash"), ReadString(providerData, "contentHash"), ReadString(entry, "hash")));
            changed |= SetString(entry, "importHash", FirstNonEmpty(ReadString(entry, "importHash"), ReadString(providerData, "importHash")));
            changed |= SetString(entry, "dependencyHash", FirstNonEmpty(ReadString(entry, "dependencyHash"), ReadString(providerData, "dependencyHash")));
            changed |= SetString(entry, "unityAssetGuid", guid);
            changed |= SetString(entry, "unityAssetPath", assetPath);
            changed |= SetString(entry, "unityMainObjectType", mainObjectType);
            changed |= SetString(entry, "importerKind", importerKind);
            changed |= SetString(entry, "importStatus", status);
            changed |= SetArray(entry, "diagnostics", diagnostics);

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
            changed |= SetString(providerData, "diagnosticCount", diagnostics.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            changed |= SetString(providerData, "diagnosticCodes", JoinDiagnosticCodes(diagnostics));
            return changed;
        }

        private static bool RefreshOrphanedUnityAssets(string root, JObject catalog, JArray entries)
        {
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    JObject entry = entries[i] as JObject;
                    JObject providerData = entry?["providerData"] as JObject;
                    AddPath(referenced, ReadString(entry, "unityAssetPath"));
                    AddPath(referenced, ReadString(entry, "address"));
                    AddPath(referenced, ReadString(providerData, "unityAssetPath"));
                    AddPath(referenced, ReadString(providerData, "assetPath"));
                }
            }

            var orphans = new List<string>();
            CollectOrphans(root + "/resources", referenced, orphans);
            CollectOrphans(root + "/imported_assets", referenced, orphans);
            orphans.Sort(StringComparer.OrdinalIgnoreCase);

            var orphanArray = new JArray();
            for (int i = 0; i < orphans.Count; i++)
            {
                orphanArray.Add(new JObject
                {
                    ["unityAssetPath"] = orphans[i],
                    ["importStatus"] = "OrphanedUnityAsset",
                    ["message"] = "Unity asset exists under the generated package root but is no longer referenced by the source resource catalog."
                });
            }

            return SetArray(catalog, "orphanedUnityAssets", orphanArray);
        }

        private static void CollectOrphans(string directory, HashSet<string> referenced, List<string> orphans)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!IsUnityImportTrackedExtension(Path.GetExtension(path)))
                    continue;
                if (!referenced.Contains(path))
                    orphans.Add(path);
            }
        }

        private static bool IsUnityImportTrackedExtension(string extension)
        {
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".glb":
                case ".gltf":
                case ".fbx":
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".mat":
                case ".prefab":
                    return true;
                default:
                    return false;
            }
        }

        private static void AddPath(HashSet<string> paths, string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
                paths.Add(path.Replace('\\', '/'));
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

        private static bool SetString(JObject obj, string key, string value)
        {
            value ??= string.Empty;
            if (string.Equals(ReadString(obj, key), value, StringComparison.Ordinal))
                return false;

            obj[key] = value;
            return true;
        }

        private static bool SetArray(JObject obj, string key, JArray value)
        {
            value ??= new JArray();
            if (JToken.DeepEquals(obj[key], value))
                return false;

            obj[key] = value;
            return true;
        }

        private static void AddDiagnostic(JArray diagnostics, string severity, string code, string message, string sourcePath, string field)
        {
            diagnostics.Add(new JObject
            {
                ["severity"] = severity ?? string.Empty,
                ["code"] = code ?? string.Empty,
                ["message"] = message ?? string.Empty,
                ["sourcePath"] = sourcePath ?? string.Empty,
                ["field"] = field ?? string.Empty
            });
        }

        private static string JoinDiagnosticCodes(JArray diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return string.Empty;

            var codes = new List<string>();
            for (int i = 0; i < diagnostics.Count; i++)
            {
                JObject item = diagnostics[i] as JObject;
                string code = ReadString(item, "code");
                if (!string.IsNullOrWhiteSpace(code))
                    codes.Add(code);
            }

            codes.Sort(StringComparer.Ordinal);
            return string.Join(",", codes);
        }
    }
}
