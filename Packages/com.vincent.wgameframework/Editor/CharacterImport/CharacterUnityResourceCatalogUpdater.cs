using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using MxFramework.Editor.Animation;
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

            string sourceJson = File.ReadAllText(catalogPath);
            string sourceHash = Hash128.Compute(sourceJson).ToString();
            if (TryRestoreCachedCatalog(root, catalogPath, sourceJson, sourceHash))
                return;

            JObject catalog;
            try
            {
                catalog = JObject.Parse(sourceJson);
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
                var avatarSources = new Dictionary<string, AvatarSourceRefreshResult>(StringComparer.OrdinalIgnoreCase);
                PreImportCharacterAvatarSources(entries);
                for (int i = 0; i < entries.Count; i++)
                {
                    JObject entry = entries[i] as JObject;
                    if (entry == null)
                        continue;

                    changed |= RefreshEntry(entry, avatarSources);
                }
            }

            changed |= RefreshOrphanedUnityAssets(root, catalog, entries);

            string enrichedJson = catalog.ToString(Formatting.Indented) + "\n";
            if (changed && !string.Equals(sourceJson, enrichedJson, StringComparison.Ordinal))
            {
                File.WriteAllText(catalogPath, enrichedJson);
                AssetDatabase.ImportAsset(catalogPath);
            }
            WriteCatalogCache(root, sourceHash, enrichedJson);
        }

        private static bool TryRestoreCachedCatalog(
            string importedRoot,
            string catalogPath,
            string sourceJson,
            string sourceHash)
        {
            string cachePath = GetCatalogCachePath(importedRoot);
            if (!File.Exists(cachePath))
                return false;
            try
            {
                JObject cache = JObject.Parse(File.ReadAllText(cachePath));
                string enrichedJson = (string)cache["enrichedCatalog"] ?? string.Empty;
                if (!CatalogSnapshotMatchesAssets(enrichedJson))
                    return false;
                if (string.Equals(sourceJson, enrichedJson, StringComparison.Ordinal))
                    return true;
                if (!string.Equals((string)cache["sourceHash"], sourceHash, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(enrichedJson))
                {
                    return false;
                }
                File.WriteAllText(catalogPath, enrichedJson);
                AssetDatabase.ImportAsset(catalogPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("MxFramework Character Import: Unity resource catalog cache could not be read: " + exception.Message);
                return false;
            }
        }

        private static void WriteCatalogCache(string importedRoot, string sourceHash, string enrichedJson)
        {
            string cachePath = GetCatalogCachePath(importedRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
            var cache = new JObject
            {
                ["format"] = "mx.characterUnityResourceCatalogEditorCache.v1",
                ["sourceHash"] = sourceHash ?? string.Empty,
                ["enrichedCatalog"] = enrichedJson ?? string.Empty
            };
            string json = cache.ToString(Formatting.Indented) + "\n";
            if (!File.Exists(cachePath) || !string.Equals(File.ReadAllText(cachePath), json, StringComparison.Ordinal))
                File.WriteAllText(cachePath, json);
        }

        private static string GetCatalogCachePath(string importedRoot)
        {
            string key = Hash128.Compute(Path.GetFullPath(importedRoot ?? string.Empty)).ToString();
            return Path.GetFullPath(Path.Combine(
                "Library",
                "MxFrameworkAuthoringCache",
                "CharacterImport",
                key,
                "unity_resource_catalog.json"));
        }

        private static bool CatalogSnapshotMatchesAssets(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            JArray entries = JObject.Parse(json)["entries"] as JArray;
            if (entries == null || entries.Count == 0) return false;
            foreach (JObject entry in entries)
            {
                string path = ReadString(entry, "unityAssetPath");
                JObject data = entry["providerData"] as JObject;
                if (string.IsNullOrEmpty(ReadString(data, "unitySnapshotAssetHash")) ||
                    !string.Equals(ReadString(data, "unitySnapshotAssetHash"), ComputeSnapshotFileHash(path), StringComparison.Ordinal) ||
                    !string.Equals(ReadString(data, "unitySnapshotMetaHash"), ComputeSnapshotFileHash(path + ".meta"), StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static string ComputeSnapshotFileHash(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return string.Empty;
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return "sha256:" + BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static bool RefreshEntry(
            JObject entry,
            Dictionary<string, AvatarSourceRefreshResult> avatarSources)
        {
            JObject providerData = entry["providerData"] as JObject;
            bool providerDataCreated = false;
            if (providerData == null)
            {
                providerData = new JObject();
                entry["providerData"] = providerData;
                providerDataCreated = true;
            }

            string assetPath = ReadString(providerData, "assetPath");
            if (string.IsNullOrWhiteSpace(assetPath))
                assetPath = ReadString(providerData, "unityAssetPath");
            if (string.IsNullOrWhiteSpace(assetPath))
                assetPath = ReadString(entry, "unityAssetPath");
            if (string.IsNullOrWhiteSpace(assetPath))
                assetPath = ReadString(entry, "address");

            string declaredSourceFormat = FirstNonEmpty(
                ReadString(providerData, "authoringSourceFormat"),
                ReadString(providerData, "sourceFormat"),
                ReadString(entry, "sourceFormat"));
            bool extractsAnimationClip = IsAnimationClipGroup(entry, providerData)
                && string.Equals(declaredSourceFormat, "fbx", StringComparison.OrdinalIgnoreCase);
            string sourceFbxAssetPath = ReadString(providerData, "sourceFbxAssetPath");
            if (extractsAnimationClip && string.IsNullOrWhiteSpace(sourceFbxAssetPath)
                && string.Equals(Path.GetExtension(assetPath), ".fbx", StringComparison.OrdinalIgnoreCase))
            {
                sourceFbxAssetPath = assetPath;
                assetPath = ResolveExtractedAnimationClipPath(entry);
            }

            string status = "Failed";
            string guid = string.Empty;
            string mainObjectType = string.Empty;
            string importerKind = GuessImporterKind(
                extractsAnimationClip ? "anim" : ReadString(providerData, "sourceFormat"),
                assetPath);
            var diagnostics = new JArray();
            string gltfAnimationMethod = string.Empty;
            string rigType = ResolveRigType(entry, providerData);
            AvatarSourceRefreshResult avatarSource = RefreshAvatarSourceCached(
                entry,
                providerData,
                rigType,
                diagnostics,
                avatarSources);

            if (extractsAnimationClip)
            {
                string requestedClipName = ReadString(providerData, "animationClipName");
                if (string.IsNullOrWhiteSpace(sourceFbxAssetPath))
                {
                    AddDiagnostic(
                        diagnostics,
                        "Error",
                        "CHARPKG_ANIMATION_SOURCE_FBX_PATH_MISSING",
                        "AnimationClip extraction requires a source FBX asset path.",
                        ReadString(providerData, "sourceRelativePath"),
                        "sourceFbxAssetPath");
                }
                else if (!File.Exists(sourceFbxAssetPath))
                {
                    status = "UnityMissing";
                    AddDiagnostic(
                        diagnostics,
                        "Error",
                        "CHARPKG_ANIMATION_SOURCE_FBX_MISSING",
                        "AnimationClip source FBX does not exist.",
                        sourceFbxAssetPath,
                        "sourceFbxAssetPath");
                }
                else if (string.IsNullOrWhiteSpace(assetPath)
                    || !assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                {
                    AddDiagnostic(
                        diagnostics,
                        "Error",
                        "CHARPKG_ANIMATION_RUNTIME_ASSET_NOT_EXTRACTED",
                        "Animation runtime resources must target a standalone .anim asset; FBX is source-only.",
                        assetPath,
                        "unityAssetPath");
                }
                else
                {
                    RefreshPrimaryImporter(entry, providerData, sourceFbxAssetPath, "unity-fbx", rigType, avatarSource, diagnostics, out gltfAnimationMethod);
                    string extractionError;
                    if (!MxAnimationClipFbxExtractor.ExtractNamedClipToAsset(
                        sourceFbxAssetPath,
                        requestedClipName,
                        assetPath,
                        out extractionError))
                    {
                        AddDiagnostic(
                            diagnostics,
                            "Error",
                            "CHARPKG_ANIMATION_CLIP_EXTRACTION_FAILED",
                            extractionError,
                            sourceFbxAssetPath,
                            "animationClipName");
                    }
                    else
                    {
                        guid = AssetDatabase.AssetPathToGUID(assetPath);
                        Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                        mainObjectType = type != null ? type.Name : string.Empty;
                        importerKind = "unity-anim";
                        if (string.IsNullOrWhiteSpace(guid) || type != typeof(AnimationClip))
                        {
                            AddDiagnostic(
                                diagnostics,
                                "Error",
                                "CHARPKG_ANIMATION_CLIP_EXTRACTION_INVALID",
                                "Extracted .anim is not a standalone Unity AnimationClip.",
                                assetPath,
                                "unityMainObjectType");
                        }
                        else if (!HasErrorDiagnostic(diagnostics))
                        {
                            status = "Extracted";
                        }
                    }
                }
            }
            else if (string.IsNullOrWhiteSpace(assetPath))
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
                if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) == null)
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                importerKind = RefreshPrimaryImporter(entry, providerData, assetPath, importerKind, rigType, avatarSource, diagnostics, out gltfAnimationMethod);
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

            bool changed = providerDataCreated;
            changed |= SetString(entry, "packageResourceKey", FirstNonEmpty(ReadString(entry, "packageResourceKey"), ReadString(providerData, "packageResourceKey"), entry.Value<string>("id") ?? string.Empty));
            changed |= SetString(entry, "stableId", FirstNonEmpty(ReadString(entry, "stableId"), ReadString(providerData, "stableId")));
            changed |= SetString(entry, "usage", FirstNonEmpty(ReadString(entry, "usage"), ReadString(providerData, "usage")));
            changed |= SetString(entry, "sourceRelativePath", FirstNonEmpty(ReadString(entry, "sourceRelativePath"), ReadString(providerData, "sourceRelativePath")));
            changed |= SetString(entry, "sourceFormat", extractsAnimationClip ? "anim" : FirstNonEmpty(ReadString(entry, "sourceFormat"), ReadString(providerData, "sourceFormat")));
            changed |= SetString(entry, "declaredContentHash", FirstNonEmpty(ReadString(entry, "declaredContentHash"), ReadString(providerData, "declaredContentHash"), ReadString(entry, "hash")));
            changed |= SetString(entry, "contentHash", FirstNonEmpty(ReadString(entry, "contentHash"), ReadString(providerData, "contentHash"), ReadString(entry, "hash")));
            changed |= SetString(entry, "importHash", FirstNonEmpty(ReadString(entry, "importHash"), ReadString(providerData, "importHash")));
            changed |= SetString(entry, "dependencyHash", FirstNonEmpty(ReadString(entry, "dependencyHash"), ReadString(providerData, "dependencyHash")));
            changed |= SetString(entry, "unityAssetGuid", guid);
            changed |= SetString(entry, "unityAssetPath", assetPath);
            changed |= SetString(entry, "address", assetPath);
            changed |= SetString(entry, "unityMainObjectType", mainObjectType);
            changed |= SetString(entry, "importerKind", importerKind);
            changed |= SetString(entry, "importStatus", status);
            changed |= SetString(entry, "gltfAnimationMethod", gltfAnimationMethod);
            changed |= SetString(entry, "rigType", rigType);
            changed |= SetString(entry, "avatarSourceStatus", avatarSource.Status);
            changed |= SetString(entry, "avatarSourceRelativePath", avatarSource.RelativePath);
            changed |= SetString(entry, "avatarSourceAssetPath", avatarSource.AssetPath);
            changed |= SetString(entry, "avatarSourceGuid", avatarSource.Guid);
            changed |= SetString(entry, "avatarSourceHumanoidStatus", avatarSource.HumanoidStatus);
            changed |= SetString(entry, "avatarSourceRigStatus", avatarSource.RigStatus);
            changed |= SetString(entry, "avatarSourceMessage", avatarSource.Message);
            changed |= SetArray(entry, "diagnostics", diagnostics);

            changed |= SetString(providerData, "unityAssetGuid", guid);
            changed |= SetString(providerData, "unitySnapshotAssetHash", ComputeSnapshotFileHash(assetPath));
            changed |= SetString(providerData, "unitySnapshotMetaHash", ComputeSnapshotFileHash(assetPath + ".meta"));
            changed |= SetString(providerData, "assetPath", assetPath);
            changed |= SetString(providerData, "unityAssetPath", assetPath);
            changed |= SetString(providerData, "sourceFormat", extractsAnimationClip ? "anim" : ReadString(providerData, "sourceFormat"));
            changed |= SetString(providerData, "authoringSourceFormat", declaredSourceFormat);
            changed |= SetString(providerData, "sourceFbxAssetPath", sourceFbxAssetPath);
            changed |= SetString(providerData, "unityMainObjectType", mainObjectType);
            changed |= SetString(providerData, "importerKind", importerKind);
            changed |= SetString(providerData, "importStatus", status);
            changed |= SetString(providerData, "gltfAnimationMethod", gltfAnimationMethod);
            changed |= SetString(providerData, "rigType", rigType);
            changed |= SetString(providerData, "avatarSourceStatus", avatarSource.Status);
            changed |= SetString(providerData, "avatarSourceRelativePath", avatarSource.RelativePath);
            changed |= SetString(providerData, "avatarSourceAssetPath", avatarSource.AssetPath);
            changed |= SetString(providerData, "avatarSourceGuid", avatarSource.Guid);
            changed |= SetString(providerData, "avatarSourceHumanoidStatus", avatarSource.HumanoidStatus);
            changed |= SetString(providerData, "avatarSourceRigStatus", avatarSource.RigStatus);
            changed |= SetString(providerData, "avatarSourceMessage", avatarSource.Message);
            changed |= SetString(providerData, "diagnosticCount", diagnostics.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            changed |= SetString(providerData, "diagnosticCodes", JoinDiagnosticCodes(diagnostics));
            return changed;
        }

        private static string RefreshPrimaryImporter(
            JObject entry,
            JObject providerData,
            string assetPath,
            string importerKind,
            string rigType,
            AvatarSourceRefreshResult avatarSource,
            JArray diagnostics,
            out string gltfAnimationMethod)
        {
            gltfAnimationMethod = string.Empty;
            string extension = Path.GetExtension(assetPath ?? string.Empty).ToLowerInvariant();
            if (extension == ".glb" || extension == ".gltf")
            {
                gltfAnimationMethod = NormalizeGltfImporter(assetPath, diagnostics);
                return "unity-gltf";
            }

            if (extension == ".fbx" && IsCharacterModel(entry, providerData))
            {
                HumanoidAvatarStatus status = NormalizeRigFbxImporter(
                    assetPath,
                    rigType,
                    ModelImporterAvatarSetup.CreateFromThisModel,
                    null,
                    diagnostics,
                    "unityAssetPath");
                if (!status.Valid)
                {
                    AddDiagnostic(
                        diagnostics,
                        "Error",
                        "CHARPKG_RIG_AVATAR_INVALID",
                        "Character model FBX did not produce a valid " + rigType + " Avatar.",
                        assetPath,
                        "unityAssetPath");
                }

                return "unity-fbx";
            }

            if (extension == ".fbx" && IsAnimationClipGroup(entry, providerData)
                && string.Equals(rigType, "Generic", StringComparison.Ordinal))
            {
                NormalizeGenericAnimationFbxImporter(assetPath, avatarSource, providerData, diagnostics);
                return "unity-fbx";
            }

            if (IsTextureAsset(assetPath))
            {
                NormalizeTextureImporter(assetPath, ReadString(providerData, "textureSemantic"), ReadString(providerData, "bindTarget"), diagnostics);
                return "unity-texture";
            }

            return importerKind;
        }

        private static AvatarSourceRefreshResult RefreshAvatarSource(
            JObject entry,
            JObject providerData,
            string rigType,
            JArray diagnostics)
        {
            string relativePath = FirstNonEmpty(ReadString(entry, "avatarSourceRelativePath"), ReadString(providerData, "avatarSourceRelativePath"));
            string assetPath = FirstNonEmpty(ReadString(entry, "avatarSourceAssetPath"), ReadString(providerData, "avatarSourceAssetPath"));
            string sourceStatus = FirstNonEmpty(ReadString(entry, "avatarSourceStatus"), ReadString(providerData, "avatarSourceStatus"));
            string sourceMessage = FirstNonEmpty(ReadString(entry, "avatarSourceMessage"), ReadString(providerData, "avatarSourceMessage"));
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return new AvatarSourceRefreshResult
                {
                    Status = sourceStatus,
                    RelativePath = relativePath,
                    Message = sourceMessage
                };
            }

            if (!File.Exists(assetPath))
            {
                AddDiagnostic(
                    diagnostics,
                    "Error",
                    "CHARPKG_AVATAR_SOURCE_MISSING",
                    rigType + " Avatar source FBX is declared but missing from the generated Unity package.",
                    assetPath,
                    "avatarSourceAssetPath");
                return new AvatarSourceRefreshResult
                {
                    Status = "Missing",
                    RelativePath = relativePath,
                    AssetPath = assetPath,
                    HumanoidStatus = "Missing",
                    Message = "Avatar source FBX is missing from generated Unity assets."
                };
            }

            HumanoidAvatarStatus humanoid = NormalizeRigFbxImporter(
                assetPath,
                rigType,
                ModelImporterAvatarSetup.CreateFromThisModel,
                null,
                diagnostics,
                "avatarSourceAssetPath");
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            string status = humanoid.Valid ? "Valid" : "Invalid";
            string message = humanoid.Valid
                ? "Avatar source FBX produced a valid " + rigType + " Avatar."
                : "Avatar source FBX did not produce a valid " + rigType + " Avatar.";
            if (!humanoid.Valid)
            {
                AddDiagnostic(
                    diagnostics,
                    "Error",
                    "CHARPKG_AVATAR_SOURCE_INVALID",
                    message,
                    assetPath,
                    "avatarSourceAssetPath");
            }

            return new AvatarSourceRefreshResult
            {
                Status = status,
                RelativePath = relativePath,
                AssetPath = assetPath,
                Guid = guid,
                HumanoidStatus = string.Equals(rigType, "Humanoid", StringComparison.Ordinal) ? humanoid.Status : string.Empty,
                RigStatus = humanoid.Status,
                Message = message
            };
        }

        private static AvatarSourceRefreshResult RefreshAvatarSourceCached(
            JObject entry,
            JObject providerData,
            string rigType,
            JArray diagnostics,
            Dictionary<string, AvatarSourceRefreshResult> cache)
        {
            string assetPath = FirstNonEmpty(
                ReadString(entry, "avatarSourceAssetPath"),
                ReadString(providerData, "avatarSourceAssetPath"));
            string key = (rigType ?? string.Empty) + "|" + (assetPath ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(assetPath)
                && cache != null
                && cache.TryGetValue(key, out AvatarSourceRefreshResult cached))
            {
                return cached;
            }

            AvatarSourceRefreshResult result = RefreshAvatarSource(entry, providerData, rigType, diagnostics);
            if (!string.IsNullOrWhiteSpace(assetPath)
                && result != null
                && string.Equals(result.Status, "Valid", StringComparison.Ordinal)
                && cache != null)
            {
                cache[key] = result;
            }
            return result;
        }

        private static string NormalizeGltfImporter(string assetPath, JArray diagnostics)
        {
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                AddDiagnostic(
                    diagnostics,
                    "Warning",
                    "CHARPKG_GLTF_IMPORTER_MISSING",
                    "Unity did not expose a glTF importer for this asset.",
                    assetPath,
                    "importerKind");
                return string.Empty;
            }

            SerializedObject serialized = new SerializedObject(importer);
            SerializedProperty animationMethod = serialized.FindProperty("importSettings.animationMethod");
            if (animationMethod == null)
            {
                AddDiagnostic(
                    diagnostics,
                    "Warning",
                    "CHARPKG_GLTF_ANIMATION_METHOD_MISSING",
                    "glTF importer settings did not expose importSettings.animationMethod; Mecanim normalization could not be verified.",
                    assetPath,
                    "gltfAnimationMethod");
                return importer.GetType().Name;
            }

            const int mecanim = 2;
            bool changed = animationMethod.intValue != mecanim;
            if (changed)
            {
                animationMethod.intValue = mecanim;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }

            return animationMethod.intValue == mecanim ? "Mecanim" : animationMethod.intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static HumanoidAvatarStatus NormalizeRigFbxImporter(
            string assetPath,
            string rigType,
            ModelImporterAvatarSetup avatarSetup,
            Avatar sourceAvatar,
            JArray diagnostics,
            string field)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                AddDiagnostic(
                    diagnostics,
                    "Error",
                    "CHARPKG_AVATAR_SOURCE_NOT_FBX",
                    rigType + " Avatar source must be imported by Unity ModelImporter.",
                    assetPath,
                    field);
                return new HumanoidAvatarStatus { Status = "NotModelImporter" };
            }

            ModelImporterAnimationType animationType;
            if (string.Equals(rigType, "Humanoid", StringComparison.Ordinal))
                animationType = ModelImporterAnimationType.Human;
            else if (string.Equals(rigType, "Generic", StringComparison.Ordinal))
                animationType = ModelImporterAnimationType.Generic;
            else
            {
                AddDiagnostic(
                    diagnostics,
                    "Error",
                    "CHARPKG_RIG_TYPE_UNSPECIFIED",
                    "Skeletal FBX import requires rigType Humanoid or Generic.",
                    assetPath,
                    "rigType");
                return new HumanoidAvatarStatus { Status = "RigTypeUnspecified" };
            }

            bool changed = false;
            if (importer.animationType != animationType)
            {
                importer.animationType = animationType;
                changed = true;
            }

            if (importer.avatarSetup != avatarSetup)
            {
                importer.avatarSetup = avatarSetup;
                changed = true;
            }

            if (avatarSetup == ModelImporterAvatarSetup.CopyFromOther && importer.sourceAvatar != sourceAvatar)
            {
                importer.sourceAvatar = sourceAvatar;
                changed = true;
            }

            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();

            if (avatarSetup == ModelImporterAvatarSetup.CopyFromOther)
            {
                bool validSource = sourceAvatar != null
                    && sourceAvatar.isValid
                    && (animationType == ModelImporterAnimationType.Human ? sourceAvatar.isHuman : !sourceAvatar.isHuman);
                return new HumanoidAvatarStatus
                {
                    Status = validSource
                        ? animationType == ModelImporterAnimationType.Human ? "ValidHuman" : "ValidGeneric"
                        : "InvalidSourceAvatar",
                    Valid = validSource
                };
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Avatar avatar)
                {
                    bool valid = avatar.isValid
                        && (animationType == ModelImporterAnimationType.Human ? avatar.isHuman : !avatar.isHuman);
                    return new HumanoidAvatarStatus
                    {
                        Status = valid
                            ? animationType == ModelImporterAnimationType.Human ? "ValidHuman" : "ValidGeneric"
                            : avatar.isValid ? (avatar.isHuman ? "UnexpectedHuman" : "UnexpectedGeneric") : "Invalid",
                        Valid = valid
                    };
                }
            }

            return new HumanoidAvatarStatus { Status = "MissingAvatar" };
        }

        private static void NormalizeGenericAnimationFbxImporter(
            string assetPath,
            AvatarSourceRefreshResult avatarSource,
            JObject providerData,
            JArray diagnostics)
        {
            Avatar avatar = LoadAvatar(avatarSource?.AssetPath);
            if (avatar == null || !avatar.isValid || avatar.isHuman)
            {
                AddDiagnostic(
                    diagnostics,
                    "Error",
                    "CHARPKG_GENERIC_ANIMATION_AVATAR_INVALID",
                    "Generic animation FBX requires a valid non-Human Avatar from the package character model.",
                    avatarSource?.AssetPath ?? string.Empty,
                    "avatarSourceAssetPath");
                return;
            }

            HumanoidAvatarStatus status = NormalizeRigFbxImporter(
                assetPath,
                "Generic",
                ModelImporterAvatarSetup.CopyFromOther,
                avatar,
                diagnostics,
                "unityAssetPath");
            if (!status.Valid)
                return;

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
                return;
            string takeName = ReadString(providerData, "animationTakeName");
            string clipName = ReadString(providerData, "animationClipName");
            bool loopTime = ReadBool(providerData, "animationLoopTime");
            bool loopBlend = ReadBool(providerData, "animationLoopBlend");
            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            ModelImporterClipAnimation selected = null;
            for (int i = 0; defaults != null && i < defaults.Length; i++)
            {
                if (selected == null)
                    selected = defaults[i];
                if (string.Equals(defaults[i]?.takeName, takeName, StringComparison.Ordinal))
                {
                    selected = defaults[i];
                    break;
                }
            }
            if (selected == null)
            {
                AddDiagnostic(
                    diagnostics,
                    "Error",
                    "CHARPKG_ANIMATION_TAKE_MISSING",
                    "Animation FBX did not expose the requested Take '" + takeName + "'.",
                    assetPath,
                    "animationTakeName");
                return;
            }

            if (!string.IsNullOrWhiteSpace(clipName))
                selected.name = clipName;
            selected.loopTime = loopTime;
            selected.loopPose = loopBlend;
            string rootMotionPolicy = ReadString(providerData, "rootMotionPolicy");
            if (string.Equals(rootMotionPolicy, "Ignore", StringComparison.OrdinalIgnoreCase))
            {
                selected.lockRootRotation = true;
                selected.lockRootHeightY = true;
                selected.lockRootPositionXZ = true;
                selected.keepOriginalOrientation = true;
                selected.keepOriginalPositionY = true;
                selected.keepOriginalPositionXZ = true;
            }
            else if (!string.IsNullOrWhiteSpace(rootMotionPolicy))
            {
                AddDiagnostic(
                    diagnostics,
                    "Error",
                    "CHARPKG_ROOT_MOTION_POLICY_UNSUPPORTED",
                    "Generic FBX import currently supports rootMotionPolicy Ignore only.",
                    assetPath,
                    "rootMotionPolicy");
                return;
            }
            ModelImporterClipAnimation[] current = importer.clipAnimations;
            if (current != null && current.Length == 1 && AnimationClipImportSettingsMatch(current[0], selected))
                return;
            importer.clipAnimations = new[] { selected };
            importer.SaveAndReimport();
        }

        internal static void NormalizeTextureImporter(string assetPath, string semantic, string bindTarget, JArray diagnostics)
        {
            bool faceDetail = string.Equals(bindTarget, "faceDetail", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(semantic) && !faceDetail)
                return;
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                AddDiagnostic(diagnostics, "Error", "CHARPKG_TEXTURE_IMPORTER_MISSING", "Texture semantic requires Unity TextureImporter.", assetPath, "textureSemantic");
                return;
            }

            TextureImporterType textureType;
            bool srgb;
            if (faceDetail || string.Equals(semantic, "Color", StringComparison.OrdinalIgnoreCase))
            {
                textureType = TextureImporterType.Default;
                srgb = true;
            }
            else if (string.Equals(semantic, "Normal", StringComparison.OrdinalIgnoreCase))
            {
                textureType = TextureImporterType.NormalMap;
                srgb = false;
            }
            else if (string.Equals(semantic, "Mask", StringComparison.OrdinalIgnoreCase))
            {
                textureType = TextureImporterType.Default;
                srgb = false;
            }
            else
            {
                AddDiagnostic(diagnostics, "Error", "CHARPKG_TEXTURE_SEMANTIC_INVALID", "textureSemantic must be Color, Normal, or Mask.", assetPath, "textureSemantic");
                return;
            }

            if (!faceDetail && importer.textureType == textureType && importer.sRGBTexture == srgb)
                return;
            importer.textureType = textureType;
            importer.sRGBTexture = srgb;
            if (faceDetail)
            {
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.isReadable = false;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.crunchedCompression = false;
                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                android.name = "Android";
                android.overridden = true;
                android.maxTextureSize = 512;
                android.format = TextureImporterFormat.ASTC_6x6;
                android.compressionQuality = 100;
                importer.SetPlatformTextureSettings(android);
            }
            importer.SaveAndReimport();
            if (faceDetail)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null || texture.width != 512 || texture.height != 512 || !importer.DoesSourceTextureHaveAlpha())
                    AddDiagnostic(diagnostics, "Error", "CHARPKG_FACE_DETAIL_TEXTURE_INVALID", "FaceDetail texture must be 512x512 RGBA.", assetPath, "bindTarget");
            }
        }

        internal static string[] NormalizeFaceDetailTextureImporterForTests(string assetPath)
        {
            var diagnostics = new JArray();
            NormalizeTextureImporter(assetPath, string.Empty, "faceDetail", diagnostics);
            var messages = new string[diagnostics.Count];
            for (int index = 0; index < diagnostics.Count; index++)
                messages[index] = diagnostics[index]?.ToString() ?? string.Empty;
            return messages;
        }

        private static bool AnimationClipImportSettingsMatch(
            ModelImporterClipAnimation current,
            ModelImporterClipAnimation desired)
        {
            return current != null
                && desired != null
                && string.Equals(current.name, desired.name, StringComparison.Ordinal)
                && string.Equals(current.takeName, desired.takeName, StringComparison.Ordinal)
                && current.loopTime == desired.loopTime
                && current.loopPose == desired.loopPose
                && current.lockRootRotation == desired.lockRootRotation
                && current.lockRootHeightY == desired.lockRootHeightY
                && current.lockRootPositionXZ == desired.lockRootPositionXZ
                && current.keepOriginalOrientation == desired.keepOriginalOrientation
                && current.keepOriginalPositionY == desired.keepOriginalPositionY
                && current.keepOriginalPositionXZ == desired.keepOriginalPositionXZ;
        }

        private static Avatar LoadAvatar(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
                if (assets[i] is Avatar avatar)
                    return avatar;
            return null;
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
                    AddPath(referenced, ReadString(entry, "avatarSourceAssetPath"));
                    AddPath(referenced, ReadString(providerData, "unityAssetPath"));
                    AddPath(referenced, ReadString(providerData, "assetPath"));
                    AddPath(referenced, ReadString(providerData, "avatarSourceAssetPath"));
                    AddPath(referenced, ReadString(providerData, "sourceFbxAssetPath"));
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
                case ".anim":
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

        private static void PreImportCharacterAvatarSources(JArray entries)
        {
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                JObject providerData = entry?["providerData"] as JObject;
                if (!IsCharacterModel(entry, providerData))
                    continue;
                string assetPath = FirstNonEmpty(
                    ReadString(providerData, "assetPath"),
                    ReadString(providerData, "unityAssetPath"),
                    ReadString(entry, "unityAssetPath"),
                    ReadString(entry, "address"));
                if (!string.Equals(Path.GetExtension(assetPath), ".fbx", StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(assetPath))
                    continue;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                NormalizeRigFbxImporter(
                    assetPath,
                    ResolveRigType(entry, providerData),
                    ModelImporterAvatarSetup.CreateFromThisModel,
                    null,
                    new JArray(),
                    "unityAssetPath");
            }
        }

        private static string ResolveRigType(JObject entry, JObject providerData)
        {
            string value = FirstNonEmpty(ReadString(entry, "rigType"), ReadString(providerData, "rigType"));
            if (string.Equals(value, "Humanoid", StringComparison.OrdinalIgnoreCase))
                return "Humanoid";
            if (string.Equals(value, "Generic", StringComparison.OrdinalIgnoreCase))
                return "Generic";
            if (string.IsNullOrWhiteSpace(value)
                && (!string.IsNullOrWhiteSpace(ReadString(entry, "avatarSourceHumanoidStatus"))
                    || !string.IsNullOrWhiteSpace(ReadString(providerData, "avatarSourceHumanoidStatus"))))
                return "Humanoid";
            return string.Empty;
        }

        private static bool IsCharacterModel(JObject entry, JObject providerData)
        {
            return string.Equals(
                FirstNonEmpty(ReadString(entry, "usage"), ReadString(providerData, "usage")),
                "characterModel",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAnimationClipGroup(JObject entry, JObject providerData)
        {
            return string.Equals(
                FirstNonEmpty(ReadString(entry, "usage"), ReadString(providerData, "usage")),
                "animationClipGroup",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTextureAsset(string assetPath)
        {
            string extension = Path.GetExtension(assetPath ?? string.Empty).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tga";
        }

        private static string ResolveExtractedAnimationClipPath(JObject entry)
        {
            string packageId = NormalizeAssetFileSegment(ReadString(entry, "packageId"));
            string resourceId = NormalizeAssetFileSegment(ReadString(entry, "id"));
            return "Assets/MxFrameworkGenerated/CharacterPackages/"
                + packageId
                + "/runtime_assets/animation_clips/"
                + resourceId
                + ".anim";
        }

        private static string NormalizeAssetFileSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var characters = value.Trim().ToLowerInvariant().ToCharArray();
            bool lastWasDot = false;
            var result = new System.Text.StringBuilder(characters.Length);
            for (int i = 0; i < characters.Length; i++)
            {
                char c = characters[i];
                bool separator = c == '.' || c == '/' || c == '\\' || c == ':' || char.IsWhiteSpace(c);
                if (separator)
                {
                    if (!lastWasDot && result.Length > 0)
                    {
                        result.Append('.');
                        lastWasDot = true;
                    }
                    continue;
                }
                bool valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!valid)
                    continue;
                result.Append(c);
                lastWasDot = false;
            }

            string normalized = result.ToString().Trim('.');
            return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
        }

        private static bool HasErrorDiagnostic(JArray diagnostics)
        {
            for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
            {
                JObject diagnostic = diagnostics[i] as JObject;
                if (string.Equals(ReadString(diagnostic, "severity"), "Error", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
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

        private static bool ReadBool(JObject obj, string key)
        {
            if (obj == null || obj[key] == null)
                return false;
            if (obj[key].Type == JTokenType.Boolean)
                return obj[key].Value<bool>();
            return bool.TryParse(obj[key].Value<string>(), out bool value) && value;
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

        private sealed class AvatarSourceRefreshResult
        {
            public string Status { get; set; } = string.Empty;
            public string RelativePath { get; set; } = string.Empty;
            public string AssetPath { get; set; } = string.Empty;
            public string Guid { get; set; } = string.Empty;
            public string HumanoidStatus { get; set; } = string.Empty;
            public string RigStatus { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        private sealed class HumanoidAvatarStatus
        {
            public bool Valid { get; set; }
            public string Status { get; set; } = string.Empty;
        }
    }
}
