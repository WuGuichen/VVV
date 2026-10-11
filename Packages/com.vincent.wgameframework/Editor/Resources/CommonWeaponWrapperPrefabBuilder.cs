using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal static class CommonWeaponWrapperPrefabBuilder
    {
        private const string UsesUnityWrapperPrefabKey = "usesUnityWrapperPrefab";
        private const string UnityRuntimeAssetPathKey = "unityRuntimeAssetPath";
        private const string RawGeneratedAssetPathKey = "rawGeneratedAssetPath";
        private const string SourceGeneratedAssetPathKey = "sourceGeneratedAssetPath";
        private const string GeneratedAssetPathKey = "generatedAssetPath";
        private const string UnityAssetGuidKey = "unityAssetGuid";
        private const string ModelWrapperPoseKey = "modelWrapperPose";
        private const string CommonModelsBundleRuleId = "common.models";
        private const string WeaponMaterialTemplateId = "wgame.prop_stylized_lit";
        private const string PaletteWeaponMaterialTemplateId = "wgame.prop_palette_stylized_lit";
        private const string SharedPaletteMode = "sharedWGameAtlas";
        private const string PaletteModeKey = "paletteMode";
        private const string SharedPaletteAssetPath = WGameCharacterPaletteAssetGenerator.TexturePath;
        private const string WeaponMaterialShaderName = "WGame/PropStylizedLit";
        private const string WeaponAlbedoBindTarget = "weapon.albedo";
        private const string WeaponMaterialBoundStatus = "weaponMaterialBound";
        private const string NoWeaponAlbedoStatus = "noWeaponAlbedo";
        private const string WeaponMaterialStatusKey = "weaponMaterialBindingStatus";
        private const string WeaponMaterialTemplateKey = "weaponMaterialTemplateId";
        private const string WeaponMaterialPathKey = "weaponMaterialPath";
        private const string WeaponMaterialTextureResourceKey = "weaponMaterialTextureResourceKey";
        private const string WeaponMaterialSourceKey = "weaponMaterialSource";
        private const string WeaponMaterialRendererCountKey = "weaponMaterialRendererCount";
        private const string BaseMapPropertyName = "_BaseMap";
        private const string ColorPropertyName = "_Color";
        private const string WgBaseColorPropertyName = "_WgBaseColor";
        private const string WgAlbedoStrengthPropertyName = "_WgAlbedoStrength";
        private const string WgPaletteEnabledPropertyName = "_WgPaletteEnabled";
        private const string WgPaletteTexturePropertyName = "_WgColorPaletteTex";
        private const string WgPaletteParamsPropertyName = "_WgColorPaletteParams";
        private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        [MenuItem("MxFramework/Weapons/Synchronize Public Weapon Prefabs", priority = 120)]
        public static void SynchronizeAllFromMenu()
        {
            var report = SynchronizeFromGlobalProfile(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            Debug.Log($"[MxFramework] Public weapon wrapper prefabs synchronized. Built: {report.Built}, Total: {report.Total}, Materials: {report.MaterialBindings}, Errors: {report.Errors.Count}.");
            if (report.Errors.Count > 0)
            {
                foreach (var err in report.Errors) Debug.LogError(err);
            }
        }

        public static CommonWeaponWrapperPrefabBuildReport SynchronizeFromGlobalProfile(string profilePath)
        {
            var report = new CommonWeaponWrapperPrefabBuildReport();
            if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
                return report;

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(profilePath));
            }
            catch (Exception ex)
            {
                report.Errors.Add("Failed to read global resource build profile: " + ex.Message);
                return report;
            }

            JArray entries = root["entries"] as JArray;
            if (entries == null)
                return report;

            string cacheInputHash = ComputeCacheInputHash(root, entries);
            if (TryReuseCachedWrappers(profilePath, cacheInputHash, report))
                return report;

            bool changed = false;
            var wrapperPaths = new JArray();
            for (int i = 0; i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                if (!TryReadWrapperEntry(entry, out string rawAssetPath, out string wrapperPrefabPath, out string weaponId))
                    continue;

                report.Total++;
                wrapperPaths.Add(wrapperPrefabPath);
                if (!TryBuildWrapperPrefab(entry, entries, weaponId, rawAssetPath, wrapperPrefabPath, report, out WeaponMaterialBindingStatus materialStatus))
                    continue;

                JToken before = entry.DeepClone();
                UpdateProfileEntry(entry, rawAssetPath, wrapperPrefabPath, materialStatus);
                changed |= !JToken.DeepEquals(before, entry);
                report.Built++;
                if (materialStatus.RendererCount > 0)
                    report.MaterialBindings++;
            }

            if (changed)
                File.WriteAllText(profilePath, JsonConvert.SerializeObject(root, Formatting.Indented));

            if (report.Errors.Count == 0)
                WriteWrapperCache(profilePath, ComputeCacheInputHash(root, entries), wrapperPaths, report);

            return report;
        }

        private static string ComputeCacheInputHash(JObject root, JArray entries)
        {
            var value = new StringBuilder("CommonWeaponWrapperPrefabBuilder.v2|")
                .Append(root?.ToString(Formatting.None) ?? string.Empty);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                if (!TryReadWrapperEntry(entries[i] as JObject, out string rawAssetPath, out _, out string weaponId))
                    continue;
                value.Append('|').Append(weaponId).Append(':').Append(rawAssetPath).Append(':')
                    .Append(AssetDatabase.GetAssetDependencyHash(rawAssetPath));
            }
            return Hash128.Compute(value.ToString()).ToString();
        }

        private static bool TryReuseCachedWrappers(
            string profilePath,
            string inputHash,
            CommonWeaponWrapperPrefabBuildReport report)
        {
            string cachePath = GetWrapperCachePath(profilePath);
            if (!File.Exists(cachePath))
                return false;
            try
            {
                JObject cache = JObject.Parse(File.ReadAllText(cachePath));
                if (!string.Equals((string)cache["inputHash"], inputHash, StringComparison.Ordinal))
                    return false;
                JArray outputs = cache["outputs"] as JArray;
                for (int i = 0; outputs != null && i < outputs.Count; i++)
                {
                    JObject output = outputs[i] as JObject;
                    string path = (string)output?["path"] ?? string.Empty;
                    var file = new FileInfo(path);
                    if (!file.Exists
                        || file.Length != ((long?)output?["length"] ?? -1L)
                        || file.LastWriteTimeUtc.Ticks != ((long?)output?["lastWriteUtcTicks"] ?? 0L)
                        || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    {
                        return false;
                    }
                }
                report.Total = (int?)cache["total"] ?? 0;
                report.MaterialBindings = (int?)cache["materialBindings"] ?? 0;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WriteWrapperCache(
            string profilePath,
            string inputHash,
            JArray wrapperPaths,
            CommonWeaponWrapperPrefabBuildReport report)
        {
            var outputs = new JArray();
            for (int i = 0; wrapperPaths != null && i < wrapperPaths.Count; i++)
            {
                string path = (string)wrapperPaths[i] ?? string.Empty;
                var file = new FileInfo(path);
                outputs.Add(new JObject
                {
                    ["path"] = path,
                    ["length"] = file.Exists ? file.Length : -1L,
                    ["lastWriteUtcTicks"] = file.Exists ? file.LastWriteTimeUtc.Ticks : 0L
                });
            }
            var cache = new JObject
            {
                ["format"] = "mx.commonWeaponWrapperEditorCache.v1",
                ["inputHash"] = inputHash,
                ["total"] = report.Total,
                ["materialBindings"] = report.MaterialBindings,
                ["outputs"] = outputs
            };
            string cachePath = GetWrapperCachePath(profilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
            File.WriteAllText(cachePath, cache.ToString(Formatting.Indented) + "\n");
        }

        private static string GetWrapperCachePath(string profilePath)
        {
            string key = Hash128.Compute(Path.GetFullPath(profilePath ?? string.Empty)).ToString();
            return Path.GetFullPath(Path.Combine(
                "Library",
                "MxFrameworkAuthoringCache",
                "CommonWeapons",
                key + ".json"));
        }

        private static bool TryReadWrapperEntry(
            JObject entry,
            out string rawAssetPath,
            out string wrapperPrefabPath,
            out string weaponId)
        {
            rawAssetPath = string.Empty;
            wrapperPrefabPath = string.Empty;
            weaponId = string.Empty;
            if (entry == null)
                return false;

            JObject providerData = entry["providerData"] as JObject;
            JObject source = entry["source"] as JObject;
            JObject sourceLocator = entry["sourceLocator"] as JObject;
            string resourceKey = ReadResourceKeyId(entry);
            string locatorPath = NormalizeAssetPath(ReadString(sourceLocator, "cachedPath"));
            if (!IsWeaponWrapperEntry(providerData, resourceKey, locatorPath))
                return false;

            weaponId = FirstNonEmpty(
                ReadString(providerData, "commonResourceId"),
                TryExtractWeaponIdFromResourceKey(resourceKey));
            rawAssetPath = NormalizeAssetPath(FirstNonEmpty(
                ReadString(providerData, RawGeneratedAssetPathKey),
                ReadString(providerData, SourceGeneratedAssetPathKey),
                locatorPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? string.Empty : locatorPath));
            wrapperPrefabPath = NormalizeAssetPath(FirstNonEmpty(
                ReadString(providerData, UnityRuntimeAssetPathKey),
                ReadString(source, "unityAssetPath"),
                ReadString(providerData, GeneratedAssetPathKey),
                locatorPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? locatorPath : string.Empty,
                BuildDefaultWrapperPrefabPath(rawAssetPath, weaponId)));

            return !string.IsNullOrWhiteSpace(weaponId) &&
                IsProjectAssetPath(rawAssetPath) &&
                IsProjectAssetPath(wrapperPrefabPath) &&
                wrapperPrefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWeaponWrapperEntry(JObject providerData, string resourceKey, string locatorPath)
        {
            if (string.Equals(ReadString(providerData, UsesUnityWrapperPrefabKey), "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(ReadString(providerData, "commonResourceKind"), "weapon", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadString(providerData, "usage"), "weaponModel", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Resource Mapping Profile v2 producer entries no longer carry the legacy wrapper metadata.
            // A raw common weapon model is the pending-wrapper state; an existing wrapper prefab is already ready.
            return !string.IsNullOrWhiteSpace(TryExtractWeaponIdFromResourceKey(resourceKey)) &&
                locatorPath.StartsWith("Assets/MxFrameworkGenerated/Common/Weapons/", StringComparison.OrdinalIgnoreCase) &&
                !locatorPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildDefaultWrapperPrefabPath(string rawAssetPath, string weaponId)
        {
            string safeWeaponId = NormalizeAssetFileName(weaponId);
            string modelFolder = NormalizeAssetPath(Path.GetDirectoryName(rawAssetPath)?.Replace('\\', '/'));
            string weaponRoot = NormalizeAssetPath(Path.GetDirectoryName(modelFolder)?.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(weaponRoot) ||
                !weaponRoot.StartsWith("Assets/MxFrameworkGenerated/Common/Weapons/", StringComparison.OrdinalIgnoreCase))
            {
                weaponRoot = "Assets/MxFrameworkGenerated/Common/Weapons/" + safeWeaponId;
            }

            return weaponRoot + "/prefabs/" + safeWeaponId + "_weapon.prefab";
        }

        private static string TryReadPaletteModeFromWeaponSources(string weaponId)
        {
            if (string.IsNullOrWhiteSpace(weaponId))
                return string.Empty;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcesPath = Path.GetFullPath(Path.Combine(projectRoot, "ArtSource", "Common", "Weapons", weaponId, "weapon_sources.json"));
            if (!File.Exists(sourcesPath))
                return string.Empty;

            try
            {
                JObject json = JObject.Parse(File.ReadAllText(sourcesPath));
                return ReadString(json, PaletteModeKey);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryBuildWrapperPrefab(
            JObject entry,
            JArray entries,
            string weaponId,
            string rawAssetPath,
            string wrapperPrefabPath,
            CommonWeaponWrapperPrefabBuildReport report,
            out WeaponMaterialBindingStatus materialStatus)
        {
            materialStatus = WeaponMaterialBindingStatus.Create(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0);

            AssetDatabase.ImportAsset(rawAssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            GameObject rawAsset = AssetDatabase.LoadAssetAtPath<GameObject>(rawAssetPath);
            if (rawAsset == null)
            {
                report.Errors.Add("Cannot load public weapon raw model: " + rawAssetPath);
                return false;
            }

            EnsureFolderPath(Path.GetDirectoryName(wrapperPrefabPath)?.Replace('\\', '/'));
            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(wrapperPrefabPath));
            try
            {
                ApplyWrapperPose(root.transform, entry["providerData"] as JObject);
                GameObject child = PrefabUtility.InstantiatePrefab(rawAsset) as GameObject;
                if (child == null)
                    child = UnityEngine.Object.Instantiate(rawAsset);
                if (child == null)
                {
                    report.Errors.Add("Cannot instantiate public weapon raw model: " + rawAssetPath);
                    return false;
                }

                Vector3 importedLocalScale = child.transform.localScale;
                child.name = rawAsset.name;
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale = importedLocalScale;

                string paletteMode = ReadString(entry["providerData"] as JObject, PaletteModeKey);
                if (string.IsNullOrWhiteSpace(paletteMode))
                {
                    paletteMode = TryReadPaletteModeFromWeaponSources(weaponId);
                    if (!string.IsNullOrWhiteSpace(paletteMode) && entry["providerData"] is JObject providerData)
                    {
                        providerData[PaletteModeKey] = paletteMode;
                    }
                }
                if (!TryApplyGeneratedWeaponMaterial(root, child, entries, weaponId, rawAssetPath, wrapperPrefabPath, paletteMode, report, out materialStatus))
                    return false;

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, wrapperPrefabPath);
                if (prefab == null)
                {
                    report.Errors.Add("Failed to save public weapon wrapper prefab: " + wrapperPrefabPath);
                    return false;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.ImportAsset(wrapperPrefabPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            return true;
        }

        private static bool TryApplyGeneratedWeaponMaterial(
            GameObject root,
            GameObject child,
            JArray entries,
            string weaponId,
            string rawAssetPath,
            string wrapperPrefabPath,
            string paletteMode,
            CommonWeaponWrapperPrefabBuildReport report,
            out WeaponMaterialBindingStatus status)
        {
            status = WeaponMaterialBindingStatus.Create(string.Empty, string.Empty, string.Empty, string.Empty, 0);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                report.Errors.Add("Public weapon wrapper prefab has no renderers: " + wrapperPrefabPath);
                return false;
            }

            WeaponImportedMaterialFallback importedFallback = CollectImportedMaterialFallback(child);
            if (!importedFallback.HasMaterialColor &&
                TryExtractGlbBaseColor(rawAssetPath, out Color glbBaseColor))
            {
                importedFallback.Tint = glbBaseColor;
                importedFallback.HasMaterialColor = true;
                importedFallback.ColorSource = "importedGlbBaseColor";
            }

            if (importedFallback.Texture == null &&
                TryExtractEmbeddedFbxAlbedoTexture(rawAssetPath, wrapperPrefabPath, weaponId, out Texture2D embeddedTexture))
            {
                importedFallback.Texture = embeddedTexture;
                importedFallback.TextureSource = "embeddedFbxAlbedo";
            }

            WeaponMaterialInput input = BuildWeaponMaterialInput(entries, weaponId, importedFallback);
            if (string.Equals(paletteMode, SharedPaletteMode, StringComparison.Ordinal))
                input = WeaponMaterialInput.Palette();
            string materialPath = BuildGeneratedWeaponMaterialPath(wrapperPrefabPath, weaponId);
            Material material = CreateOrUpdateGeneratedWeaponMaterial(materialPath, input, out string issue);
            if (material == null)
            {
                report.Errors.Add(issue);
                return false;
            }

            int rendererCount = AssignGeneratedWeaponMaterial(renderers, material);
            status = WeaponMaterialBindingStatus.Create(
                input.Status,
                materialPath,
                input.TextureResourceKey,
                input.Source,
                rendererCount);
            return true;
        }

        private static WeaponMaterialInput BuildWeaponMaterialInput(
            JArray entries,
            string weaponId,
            WeaponImportedMaterialFallback importedFallback)
        {
            if (TryLoadExplicitWeaponAlbedoTexture(entries, weaponId, out Texture2D explicitTexture, out string textureResourceKey))
            {
                return new WeaponMaterialInput(
                    WeaponMaterialBoundStatus,
                    explicitTexture,
                    importedFallback.Tint,
                    textureResourceKey,
                    "explicitWeaponAlbedo");
            }

            if (importedFallback.Texture != null)
            {
                return new WeaponMaterialInput(
                    WeaponMaterialBoundStatus,
                    importedFallback.Texture,
                    importedFallback.Tint,
                    string.Empty,
                    FirstNonEmpty(importedFallback.TextureSource, "importedModelTexture"));
            }

            return new WeaponMaterialInput(
                NoWeaponAlbedoStatus,
                null,
                importedFallback.Tint,
                string.Empty,
                FirstNonEmpty(importedFallback.ColorSource, importedFallback.HasMaterialColor ? "importedMaterialColor" : "defaultPropMaterial"));
        }

        private static bool TryLoadExplicitWeaponAlbedoTexture(
            JArray entries,
            string weaponId,
            out Texture2D texture,
            out string textureResourceKey)
        {
            texture = null;
            textureResourceKey = string.Empty;
            if (entries == null || string.IsNullOrWhiteSpace(weaponId))
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                JObject providerData = entry?["providerData"] as JObject;
                string resourceKey = ReadResourceKeyId(entry);
                string bindTarget = ReadString(providerData, "bindTarget");
                bool legacyTextureEntry =
                    string.Equals(ReadString(providerData, "commonResourceKind"), "weapon", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ReadString(providerData, "commonResourceId"), weaponId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ReadString(providerData, "usage"), "texture", StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(bindTarget, WeaponAlbedoBindTarget, StringComparison.OrdinalIgnoreCase) ||
                        resourceKey.EndsWith(".texture.albedo", StringComparison.OrdinalIgnoreCase));
                bool profileV2TextureEntry = string.Equals(
                    resourceKey,
                    "weapon." + weaponId + ".texture.albedo",
                    StringComparison.OrdinalIgnoreCase);
                if (!legacyTextureEntry && !profileV2TextureEntry)
                {
                    continue;
                }

                string assetPath = NormalizeAssetPath(FirstNonEmpty(
                    ReadString(entry["source"] as JObject, "unityAssetPath"),
                    ReadString(entry["sourceLocator"] as JObject, "cachedPath"),
                    ReadString(providerData, UnityRuntimeAssetPathKey),
                    ReadString(providerData, RawGeneratedAssetPathKey),
                    ReadString(providerData, SourceGeneratedAssetPathKey)));
                if (!IsProjectAssetPath(assetPath))
                    continue;

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Texture2D candidate = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (candidate == null)
                    continue;

                texture = candidate;
                textureResourceKey = resourceKey;
                return true;
            }

            return false;
        }

        private static WeaponImportedMaterialFallback CollectImportedMaterialFallback(GameObject child)
        {
            var fallback = new WeaponImportedMaterialFallback();
            if (child == null)
                return fallback;

            float r = 0f;
            float g = 0f;
            float b = 0f;
            float a = 0f;
            int colorCount = 0;
            Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                if (materials == null)
                    continue;

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                        continue;

                    fallback.HasMaterial = true;
                    if (fallback.Texture == null && TryGetMaterialTexture(material, out Texture texture))
                    {
                        fallback.Texture = texture;
                        fallback.TextureSource = "importedModelTexture";
                    }

                    if (TryGetMaterialColor(material, out Color color))
                    {
                        r += color.r;
                        g += color.g;
                        b += color.b;
                        a += color.a;
                        colorCount++;
                    }
                }
            }

            if (colorCount > 0)
            {
                fallback.Tint = new Color(r / colorCount, g / colorCount, b / colorCount, a / colorCount);
                fallback.HasMaterialColor = true;
            }

            return fallback;
        }

        private static bool TryExtractEmbeddedFbxAlbedoTexture(
            string rawAssetPath,
            string wrapperPrefabPath,
            string weaponId,
            out Texture2D texture)
        {
            texture = null;
            if (string.IsNullOrWhiteSpace(rawAssetPath) ||
                !rawAssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                !IsProjectAssetPath(rawAssetPath))
            {
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string rawFullPath = Path.GetFullPath(Path.Combine(projectRoot, rawAssetPath));
            if (!File.Exists(rawFullPath) ||
                !TryReadFileBytes(rawFullPath, out byte[] fbxBytes) ||
                !TryExtractFirstPng(fbxBytes, out byte[] pngBytes))
            {
                return false;
            }

            string textureAssetPath = BuildGeneratedEmbeddedAlbedoTexturePath(wrapperPrefabPath, weaponId);
            EnsureFolderPath(Path.GetDirectoryName(textureAssetPath)?.Replace('\\', '/'));
            string textureFullPath = Path.GetFullPath(Path.Combine(projectRoot, textureAssetPath));
            try
            {
                if (!File.Exists(textureFullPath) || !FileBytesEqual(textureFullPath, pngBytes))
                    File.WriteAllBytes(textureFullPath, pngBytes);
            }
            catch (Exception)
            {
                return false;
            }

            AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            return texture != null;
        }

        private static bool TryReadFileBytes(string path, out byte[] bytes)
        {
            bytes = null;
            try
            {
                bytes = File.ReadAllBytes(path);
                return bytes != null && bytes.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryExtractFirstPng(byte[] source, out byte[] pngBytes)
        {
            pngBytes = null;
            if (source == null || source.Length < PngSignature.Length + 12)
                return false;

            int signatureOffset = IndexOfBytes(source, PngSignature, 0);
            if (signatureOffset < 0)
                return false;

            int cursor = signatureOffset + PngSignature.Length;
            while (cursor + 12 <= source.Length)
            {
                int chunkLength = ReadPngChunkLength(source, cursor);
                if (chunkLength < 0)
                    return false;

                int chunkTotalLength = 12 + chunkLength;
                if (chunkTotalLength < 12 || cursor + chunkTotalLength > source.Length)
                    return false;

                bool isEndChunk =
                    source[cursor + 4] == (byte)'I' &&
                    source[cursor + 5] == (byte)'E' &&
                    source[cursor + 6] == (byte)'N' &&
                    source[cursor + 7] == (byte)'D';
                cursor += chunkTotalLength;
                if (isEndChunk)
                {
                    int pngLength = cursor - signatureOffset;
                    pngBytes = new byte[pngLength];
                    Buffer.BlockCopy(source, signatureOffset, pngBytes, 0, pngLength);
                    return true;
                }
            }

            return false;
        }

        private static int ReadPngChunkLength(byte[] source, int offset)
        {
            if (source == null || offset < 0 || offset + 4 > source.Length)
                return -1;

            int result =
                (source[offset] << 24) |
                (source[offset + 1] << 16) |
                (source[offset + 2] << 8) |
                source[offset + 3];
            return result >= 0 ? result : -1;
        }

        private static int IndexOfBytes(byte[] source, byte[] pattern, int startIndex)
        {
            if (source == null || pattern == null || pattern.Length == 0)
                return -1;

            int max = source.Length - pattern.Length;
            for (int i = Math.Max(0, startIndex); i <= max; i++)
            {
                bool matches = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] == pattern[j])
                        continue;

                    matches = false;
                    break;
                }

                if (matches)
                    return i;
            }

            return -1;
        }

        private static bool FileBytesEqual(string path, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(path) || bytes == null || !File.Exists(path))
                return false;

            try
            {
                byte[] existing = File.ReadAllBytes(path);
                if (existing.Length != bytes.Length)
                    return false;

                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] != bytes[i])
                        return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryExtractGlbBaseColor(string rawAssetPath, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(rawAssetPath) ||
                !rawAssetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ||
                !IsProjectAssetPath(rawAssetPath))
            {
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string rawFullPath = Path.GetFullPath(Path.Combine(projectRoot, rawAssetPath));
            if (!File.Exists(rawFullPath) || !TryReadFileBytes(rawFullPath, out byte[] glbBytes))
                return false;

            return TryExtractGlbBaseColor(glbBytes, out color);
        }

        private static bool TryExtractGlbBaseColor(byte[] glbBytes, out Color color)
        {
            color = Color.white;
            if (!TryReadGlbJson(glbBytes, out JObject json))
                return false;

            JArray materials = json["materials"] as JArray;
            if (materials == null || materials.Count == 0)
                return false;

            var materialWeights = new Dictionary<int, float>();
            CollectGlbPrimitiveMaterialWeights(json, materialWeights);
            if (materialWeights.Count == 0)
            {
                for (int i = 0; i < materials.Count; i++)
                    materialWeights[i] = 1f;
            }

            float r = 0f;
            float g = 0f;
            float b = 0f;
            float a = 0f;
            float totalWeight = 0f;
            foreach (KeyValuePair<int, float> pair in materialWeights)
            {
                if (pair.Key < 0 || pair.Key >= materials.Count || pair.Value <= 0f)
                    continue;

                if (!TryReadGlbMaterialBaseColor(materials[pair.Key] as JObject, out Color materialColor))
                    continue;

                r += materialColor.r * pair.Value;
                g += materialColor.g * pair.Value;
                b += materialColor.b * pair.Value;
                a += materialColor.a * pair.Value;
                totalWeight += pair.Value;
            }

            if (totalWeight <= 0f)
                return false;

            color = new Color(r / totalWeight, g / totalWeight, b / totalWeight, a / totalWeight);
            return true;
        }

        private static bool TryReadGlbJson(byte[] glbBytes, out JObject json)
        {
            json = null;
            if (glbBytes == null || glbBytes.Length < 20)
                return false;
            if (glbBytes[0] != (byte)'g' || glbBytes[1] != (byte)'l' || glbBytes[2] != (byte)'T' || glbBytes[3] != (byte)'F')
                return false;

            int jsonLength = ReadLittleEndianInt32(glbBytes, 12);
            if (jsonLength <= 0 || 20 + jsonLength > glbBytes.Length)
                return false;

            int jsonType = ReadLittleEndianInt32(glbBytes, 16);
            if (jsonType != 0x4E4F534A)
                return false;

            try
            {
                string text = Encoding.UTF8.GetString(glbBytes, 20, jsonLength).TrimEnd('\0', ' ', '\r', '\n', '\t');
                json = JObject.Parse(text);
                return true;
            }
            catch (JsonException)
            {
                json = null;
                return false;
            }
        }

        private static void CollectGlbPrimitiveMaterialWeights(JObject json, Dictionary<int, float> materialWeights)
        {
            JArray meshes = json?["meshes"] as JArray;
            JArray accessors = json?["accessors"] as JArray;
            if (meshes == null || materialWeights == null)
                return;

            for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
            {
                JArray primitives = meshes[meshIndex]?["primitives"] as JArray;
                if (primitives == null)
                    continue;

                for (int primitiveIndex = 0; primitiveIndex < primitives.Count; primitiveIndex++)
                {
                    JObject primitive = primitives[primitiveIndex] as JObject;
                    int materialIndex = primitive?["material"]?.Value<int>() ?? -1;
                    if (materialIndex < 0)
                        continue;

                    float weight = 1f;
                    int indicesAccessor = primitive?["indices"]?.Value<int>() ?? -1;
                    if (accessors != null && indicesAccessor >= 0 && indicesAccessor < accessors.Count)
                        weight = Math.Max(1f, accessors[indicesAccessor]?["count"]?.Value<float>() ?? 1f);

                    materialWeights.TryGetValue(materialIndex, out float existingWeight);
                    materialWeights[materialIndex] = existingWeight + weight;
                }
            }
        }

        private static bool TryReadGlbMaterialBaseColor(JObject material, out Color color)
        {
            color = Color.white;
            JArray factor = material?["pbrMetallicRoughness"]?["baseColorFactor"] as JArray;
            if (factor == null || factor.Count < 3)
                return false;

            color = new Color(
                ReadColorFactor(factor, 0, 1f),
                ReadColorFactor(factor, 1, 1f),
                ReadColorFactor(factor, 2, 1f),
                ReadColorFactor(factor, 3, 1f));
            return true;
        }

        private static float ReadColorFactor(JArray values, int index, float fallback)
        {
            if (values == null || index < 0 || index >= values.Count)
                return fallback;

            float value = values[index].Value<float>();
            return Mathf.Clamp01(value);
        }

        private static int ReadLittleEndianInt32(byte[] source, int offset)
        {
            if (source == null || offset < 0 || offset + 4 > source.Length)
                return -1;

            return source[offset] |
                (source[offset + 1] << 8) |
                (source[offset + 2] << 16) |
                (source[offset + 3] << 24);
        }

        private static bool TryGetMaterialTexture(Material material, out Texture texture)
        {
            texture = null;
            if (material == null)
                return false;

            if (TryGetMaterialTexture(material, BaseMapPropertyName, out texture) ||
                TryGetMaterialTexture(material, "_MainTex", out texture) ||
                TryGetMaterialTexture(material, "_BaseColorMap", out texture) ||
                TryGetMaterialTexture(material, "_BaseColorTexture", out texture))
            {
                return true;
            }

            texture = material.mainTexture;
            return texture != null;
        }

        private static bool TryGetMaterialTexture(Material material, string propertyName, out Texture texture)
        {
            texture = null;
            if (material == null || string.IsNullOrWhiteSpace(propertyName) || !material.HasProperty(propertyName))
                return false;

            texture = material.GetTexture(propertyName);
            return texture != null;
        }

        private static bool TryGetMaterialColor(Material material, out Color color)
        {
            color = Color.white;
            if (material == null)
                return false;

            if (TryGetMaterialColor(material, "_BaseColor", out color) ||
                TryGetMaterialColor(material, ColorPropertyName, out color) ||
                TryGetMaterialColor(material, WgBaseColorPropertyName, out color))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetMaterialColor(Material material, string propertyName, out Color color)
        {
            color = Color.white;
            if (material == null || string.IsNullOrWhiteSpace(propertyName) || !material.HasProperty(propertyName))
                return false;

            color = material.GetColor(propertyName);
            return true;
        }

        private static Material CreateOrUpdateGeneratedWeaponMaterial(
            string materialPath,
            WeaponMaterialInput input,
            out string issue)
        {
            issue = string.Empty;
            Shader shader = Shader.Find(WeaponMaterialShaderName);
            if (shader == null)
            {
                issue = "Weapon material shader was not found: " + WeaponMaterialShaderName;
                return null;
            }

            EnsureFolderPath(Path.GetDirectoryName(materialPath)?.Replace('\\', '/'));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(materialPath)
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty(BaseMapPropertyName))
                material.SetTexture(BaseMapPropertyName, input.AlbedoTexture);
            if (material.HasProperty(ColorPropertyName))
                material.SetColor(ColorPropertyName, input.Tint);
            if (material.HasProperty(WgBaseColorPropertyName))
                material.SetColor(WgBaseColorPropertyName, input.Tint);
            if (material.HasProperty(WgAlbedoStrengthPropertyName))
                material.SetFloat(WgAlbedoStrengthPropertyName, input.AlbedoTexture != null ? 1f : 0f);

            if (input.UsePalette)
            {
                WGameSurfacePaletteCatalogDocument catalog = WGameSurfacePaletteCatalog.LoadAndValidate(out string catalogIssue);
                WGameColorPaletteAtlasReceipt receipt = WGameSurfacePaletteCatalog.ReadReceipt(
                    WGameCharacterPaletteAssetGenerator.ReceiptPath,
                    out string receiptIssue);
                if (catalog == null || receipt == null
                    || !string.Equals(catalog.CatalogHash, receipt.CatalogHash, StringComparison.Ordinal)
                    || !File.Exists(Path.GetFullPath(SharedPaletteAssetPath))
                    || !string.Equals(
                        receipt.AtlasHash,
                        WGameSurfacePaletteCatalog.ComputeSha256(File.ReadAllBytes(Path.GetFullPath(SharedPaletteAssetPath))),
                        StringComparison.Ordinal))
                {
                    issue = !string.IsNullOrWhiteSpace(catalogIssue)
                        ? catalogIssue
                        : !string.IsNullOrWhiteSpace(receiptIssue)
                            ? receiptIssue
                            : "Shared Surface Palette catalog, atlas, and receipt have drifted.";
                    return null;
                }
                Texture2D paletteAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(SharedPaletteAssetPath);
                if (paletteAtlas == null || paletteAtlas.width != 256 || paletteAtlas.height != 256)
                {
                    issue = "Shared WGame RGB Color Palette atlas was not found at 256x256: " + SharedPaletteAssetPath;
                    return null;
                }

                material.EnableKeyword("_WG_PALETTE_ON");
                material.SetFloat(WgPaletteEnabledPropertyName, 1f);
                material.SetTexture(WgPaletteTexturePropertyName, paletteAtlas);
                material.SetVector(WgPaletteParamsPropertyName, new Vector4(256f, 256f, 1f / 256f, 1f / 256f));
            }
            else
            {
                material.DisableKeyword("_WG_PALETTE_ON");
                if (material.HasProperty(WgPaletteEnabledPropertyName))
                    material.SetFloat(WgPaletteEnabledPropertyName, 0f);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            return material;
        }

        private static int AssignGeneratedWeaponMaterial(Renderer[] renderers, Material material)
        {
            if (renderers == null || material == null)
                return 0;

            int assigned = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    renderer.sharedMaterial = material;
                    assigned++;
                    continue;
                }

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    materials[materialIndex] = material;
                renderer.sharedMaterials = materials;
                assigned++;
            }

            return assigned;
        }

        private static string BuildGeneratedWeaponMaterialPath(string wrapperPrefabPath, string weaponId)
        {
            string prefabFolder = NormalizeAssetPath(Path.GetDirectoryName(wrapperPrefabPath)?.Replace('\\', '/'));
            string weaponRoot = NormalizeAssetPath(Path.GetDirectoryName(prefabFolder)?.Replace('\\', '/'));
            string safeWeaponId = NormalizeAssetFileName(FirstNonEmpty(weaponId, "weapon"));
            return weaponRoot + "/materials/" + safeWeaponId + "_Weapon.mat";
        }

        private static string BuildGeneratedEmbeddedAlbedoTexturePath(string wrapperPrefabPath, string weaponId)
        {
            string prefabFolder = NormalizeAssetPath(Path.GetDirectoryName(wrapperPrefabPath)?.Replace('\\', '/'));
            string weaponRoot = NormalizeAssetPath(Path.GetDirectoryName(prefabFolder)?.Replace('\\', '/'));
            string safeWeaponId = NormalizeAssetFileName(FirstNonEmpty(weaponId, "weapon"));
            return weaponRoot + "/textures/" + safeWeaponId + "_EmbeddedAlbedo.png";
        }

        private static void ApplyWrapperPose(Transform transform, JObject providerData)
        {
            if (transform == null)
                return;

            JObject pose = null;
            string poseText = ReadString(providerData, ModelWrapperPoseKey);
            if (!string.IsNullOrWhiteSpace(poseText))
            {
                try
                {
                    pose = JObject.Parse(poseText);
                }
                catch
                {
                    pose = null;
                }
            }

            transform.localPosition = ReadVector3(pose?["position"] as JObject, Vector3.zero);
            transform.localRotation = ReadQuaternion(pose?["rotation"] as JObject, Quaternion.identity);
            transform.localScale = ReadVector3(pose?["scale"] as JObject, Vector3.one);
        }

        private static void UpdateProfileEntry(
            JObject entry,
            string rawAssetPath,
            string wrapperPrefabPath,
            WeaponMaterialBindingStatus materialStatus)
        {
            JObject source = entry["source"] as JObject;
            JObject providerData = entry["providerData"] as JObject;
            if (providerData == null)
            {
                providerData = new JObject();
                entry["providerData"] = providerData;
            }

            string guid = AssetDatabase.AssetPathToGUID(wrapperPrefabPath);
            if (source != null)
            {
                source["unityAssetPath"] = wrapperPrefabPath;
                source["unityGuid"] = guid;
                entry["bundleRule"] = CommonModelsBundleRuleId;
                entry["deliveryMode"] = "internal";
                entry["bundleGroupHint"] = CommonModelsBundleRuleId;
                entry["runtimeLoadable"] = !string.IsNullOrWhiteSpace(guid);
                entry["editorOnly"] = string.IsNullOrWhiteSpace(guid);
            }

            JObject sourceLocator = entry["sourceLocator"] as JObject;
            if (sourceLocator != null || source == null)
            {
                if (sourceLocator == null)
                {
                    sourceLocator = new JObject();
                    entry["sourceLocator"] = sourceLocator;
                }

                sourceLocator["kind"] = "unityAsset";
                sourceLocator["cachedPath"] = wrapperPrefabPath;
                sourceLocator["unityGuid"] = guid;
                JObject bundleIntent = entry["bundleIntent"] as JObject;
                if (bundleIntent == null)
                {
                    bundleIntent = new JObject();
                    entry["bundleIntent"] = bundleIntent;
                }

                bundleIntent["ruleId"] = CommonModelsBundleRuleId;
                entry["deliveryIntent"] = "internal";
            }

            providerData[RawGeneratedAssetPathKey] = rawAssetPath;
            providerData[GeneratedAssetPathKey] = wrapperPrefabPath;
            providerData[UnityRuntimeAssetPathKey] = wrapperPrefabPath;
            providerData[UnityAssetGuidKey] = guid;
            providerData[UsesUnityWrapperPrefabKey] = "true";
            providerData["unityRuntimeAssetKind"] = "weaponWrapperPrefab";
            providerData[WeaponMaterialStatusKey] = materialStatus.Status;
            providerData[WeaponMaterialTemplateKey] = materialStatus.TemplateId;
            providerData[WeaponMaterialPathKey] = materialStatus.MaterialPath;
            providerData[WeaponMaterialTextureResourceKey] = materialStatus.TextureResourceKey;
            providerData[WeaponMaterialSourceKey] = materialStatus.Source;
            providerData[WeaponMaterialRendererCountKey] = materialStatus.RendererCount.ToString(CultureInfo.InvariantCulture);
        }

        private static Vector3 ReadVector3(JObject value, Vector3 fallback)
        {
            if (value == null)
                return fallback;
            return new Vector3(
                ReadFloat(value, "x", fallback.x),
                ReadFloat(value, "y", fallback.y),
                ReadFloat(value, "z", fallback.z));
        }

        private static Quaternion ReadQuaternion(JObject value, Quaternion fallback)
        {
            if (value == null)
                return fallback;
            return new Quaternion(
                ReadFloat(value, "x", fallback.x),
                ReadFloat(value, "y", fallback.y),
                ReadFloat(value, "z", fallback.z),
                ReadFloat(value, "w", fallback.w));
        }

        private static float ReadFloat(JObject value, string key, float fallback)
        {
            JToken token = value?[key];
            if (token == null)
                return fallback;
            return token.Type == JTokenType.Float || token.Type == JTokenType.Integer
                ? token.Value<float>()
                : fallback;
        }

        private static string ReadString(JObject obj, string key)
        {
            JToken token = obj?[key];
            return token != null ? token.Value<string>() ?? string.Empty : string.Empty;
        }

        private static string ReadResourceKeyId(JObject entry)
        {
            return ReadString(entry?["resourceKey"] as JObject, "id");
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

        private static string TryExtractWeaponIdFromResourceKey(string resourceKey)
        {
            string normalized = resourceKey ?? string.Empty;
            const string prefix = "weapon.";
            const string suffix = ".model";
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                normalized.Length <= prefix.Length + suffix.Length)
            {
                return string.Empty;
            }

            return normalized.Substring(prefix.Length, normalized.Length - prefix.Length - suffix.Length);
        }

        private static string NormalizeAssetPath(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string NormalizeAssetFileName(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return "weapon";

            char[] chars = normalized.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if ((c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' ||
                    c == '-')
                {
                    continue;
                }

                chars[i] = '_';
            }

            return new string(chars);
        }

        private static bool IsProjectAssetPath(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.StartsWith("Assets/", StringComparison.Ordinal) &&
                !value.Contains("/../", StringComparison.Ordinal) &&
                !value.EndsWith("/..", StringComparison.Ordinal);
        }

        private static void EnsureFolderPath(string path)
        {
            string normalized = NormalizeAssetPath(path);
            if (string.IsNullOrWhiteSpace(normalized) || !normalized.StartsWith("Assets/", StringComparison.Ordinal))
                return;

            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private readonly struct WeaponMaterialInput
        {
            public WeaponMaterialInput(
                string status,
                Texture albedoTexture,
                Color tint,
                string textureResourceKey,
                string source)
            {
                Status = status;
                AlbedoTexture = albedoTexture;
                Tint = tint;
                TextureResourceKey = textureResourceKey ?? string.Empty;
                Source = source ?? string.Empty;
                UsePalette = false;
            }

            public string Status { get; }
            public Texture AlbedoTexture { get; }
            public Color Tint { get; }
            public string TextureResourceKey { get; }
            public string Source { get; }
            public bool UsePalette { get; }

            private WeaponMaterialInput(bool usePalette)
            {
                Status = WeaponMaterialBoundStatus;
                AlbedoTexture = null;
                Tint = Color.white;
                TextureResourceKey = "wgame.character.palette.shared";
                Source = SharedPaletteMode;
                UsePalette = usePalette;
            }

            public static WeaponMaterialInput Palette() => new WeaponMaterialInput(usePalette: true);
        }

        private sealed class WeaponImportedMaterialFallback
        {
            public Texture Texture { get; set; }
            public string TextureSource { get; set; } = string.Empty;
            public Color Tint { get; set; } = Color.white;
            public bool HasMaterial { get; set; }
            public bool HasMaterialColor { get; set; }
            public string ColorSource { get; set; } = string.Empty;
        }

        private readonly struct WeaponMaterialBindingStatus
        {
            private WeaponMaterialBindingStatus(
                string status,
                string materialPath,
                string textureResourceKey,
                string source,
                int rendererCount)
            {
                Status = status ?? string.Empty;
                TemplateId = string.Equals(source, SharedPaletteMode, StringComparison.Ordinal)
                    ? PaletteWeaponMaterialTemplateId
                    : WeaponMaterialTemplateId;
                MaterialPath = materialPath ?? string.Empty;
                TextureResourceKey = textureResourceKey ?? string.Empty;
                Source = source ?? string.Empty;
                RendererCount = rendererCount;
            }

            public string Status { get; }
            public string TemplateId { get; }
            public string MaterialPath { get; }
            public string TextureResourceKey { get; }
            public string Source { get; }
            public int RendererCount { get; }

            public static WeaponMaterialBindingStatus Create(
                string status,
                string materialPath,
                string textureResourceKey,
                string source,
                int rendererCount)
            {
                return new WeaponMaterialBindingStatus(status, materialPath, textureResourceKey, source, rendererCount);
            }
        }
    }

    internal sealed class CommonWeaponWrapperPrefabBuildReport
    {
        public int Total { get; set; }
        public int Built { get; set; }
        public int MaterialBindings { get; set; }
        public List<string> Errors { get; } = new List<string>();
    }
}
