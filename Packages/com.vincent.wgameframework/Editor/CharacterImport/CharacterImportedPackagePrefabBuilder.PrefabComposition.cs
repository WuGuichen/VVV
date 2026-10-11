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
        private static Transform CreateBodyModel(ResourcePreviewInfo body, Transform parent)
        {
            Transform wrapper = CreateModelWrapper("BodyModel", body, parent);
            return wrapper;
        }

        private static void ApplyBodyModelNodeVisibility(
            Transform bodyModel,
            ResourcePreviewInfo body,
            bool hideEmbeddedWeapons)
        {
            if (bodyModel == null || body == null)
                return;

            var configuredPaths = new List<string>();
            var configuredPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddModelNodePaths(configuredPaths, configuredPathSet, body.HiddenRendererNodePaths);
            if (hideEmbeddedWeapons)
                AddModelNodePaths(configuredPaths, configuredPathSet, body.EmbeddedWeaponNodePaths);
            if (configuredPaths.Count == 0)
                return;

            Transform[] transforms = bodyModel.GetComponentsInChildren<Transform>(includeInactive: true);
            var candidateTransforms = new List<Transform>(transforms.Length);
            var candidatePaths = new List<string>(transforms.Length);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                Transform candidate = transforms[transformIndex];
                if (candidate == null || candidate == bodyModel)
                    continue;

                candidateTransforms.Add(candidate);
                candidatePaths.Add(NormalizeModelNodePath(
                    AnimationUtility.CalculateTransformPath(candidate, bodyModel)));
            }

            var matchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int pathIndex = 0; pathIndex < configuredPaths.Count; pathIndex++)
            {
                string configuredPath = configuredPaths[pathIndex];
                int directMatchCount = 0;
                for (int candidateIndex = 0; candidateIndex < candidateTransforms.Count; candidateIndex++)
                {
                    if (!ModelNodePathMatches(candidatePaths[candidateIndex], configuredPath))
                        continue;

                    candidateTransforms[candidateIndex].gameObject.SetActive(false);
                    directMatchCount++;
                    matchedPaths.Add(configuredPath);
                }

                if (directMatchCount > 0)
                    continue;

                string leafName = GetModelNodeLeafName(configuredPath);
                int leafMatchIndex = -1;
                int leafMatchCount = 0;
                for (int candidateIndex = 0; candidateIndex < candidateTransforms.Count; candidateIndex++)
                {
                    if (!string.Equals(candidateTransforms[candidateIndex].name, leafName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    leafMatchIndex = candidateIndex;
                    leafMatchCount++;
                }

                if (leafMatchCount == 1)
                {
                    candidateTransforms[leafMatchIndex].gameObject.SetActive(false);
                    matchedPaths.Add(configuredPath);
                }
            }

            if (matchedPaths.Count == configuredPaths.Count)
                return;

            var missingPaths = new List<string>();
            for (int pathIndex = 0; pathIndex < configuredPaths.Count; pathIndex++)
            {
                string configuredPath = configuredPaths[pathIndex];
                if (!matchedPaths.Contains(configuredPath))
                    missingPaths.Add(configuredPath);
            }

            Debug.LogWarning("MxFramework Character Preview: model visibility metadata referenced missing nodes. resource="
                + body.Key + " nodes=" + string.Join(",", missingPaths));
        }

        private static void AddModelNodePaths(
            List<string> target,
            HashSet<string> uniquePaths,
            string[] configuredPaths)
        {
            if (target == null || uniquePaths == null || configuredPaths == null)
                return;

            for (int pathIndex = 0; pathIndex < configuredPaths.Length; pathIndex++)
            {
                string path = NormalizeModelNodePath(configuredPaths[pathIndex]);
                if (!string.IsNullOrWhiteSpace(path) && uniquePaths.Add(path))
                    target.Add(path);
            }
        }

        private static string NormalizeModelNodePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim().Trim('/');
        }

        private static bool ModelNodePathMatches(string candidatePath, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(configuredPath))
                return false;
            return string.Equals(candidatePath, configuredPath, StringComparison.OrdinalIgnoreCase)
                || candidatePath.EndsWith("/" + configuredPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetModelNodeLeafName(string path)
        {
            string normalized = NormalizeModelNodePath(path);
            int separatorIndex = normalized.LastIndexOf('/');
            return separatorIndex >= 0 ? normalized.Substring(separatorIndex + 1) : normalized;
        }

        private static Animator EnsureBodyAnimator(
            Transform bodyModel,
            AnimationClip[] animationClips,
            string importedPackageRoot,
            string modelAssetPath,
            string rigType)
        {
            if (bodyModel == null)
                return null;

            Animator animator = bodyModel.GetComponentInChildren<Animator>(includeInactive: true);
            if (animator == null)
                animator = bodyModel.gameObject.AddComponent<Animator>();

            if (string.Equals(rigType, "Generic", StringComparison.Ordinal))
            {
                Avatar avatar = LoadGenericAvatarAtPath(modelAssetPath, configureModelImporter: true);
                if (avatar == null)
                {
                    throw new InvalidOperationException(
                        "MxFramework Character Preview: Generic character model did not provide a valid non-Human Avatar. modelAssetPath="
                        + NormalizeProjectPath(modelAssetPath));
                }
                animator.avatar = avatar;
            }
            else if (RequiresHumanoidAvatar(animationClips) && animator.avatar == null)
            {
                Avatar avatar = FindHumanoidAvatar(modelAssetPath, importedPackageRoot);
                if (avatar != null)
                    animator.avatar = avatar;
            }

            ConfigurePreviewAnimator(animator);
            EditorUtility.SetDirty(animator);
            return animator;
        }

        private static CharacterBodyMaterialBindingStatus ApplyBodyMaterialBinding(
            CharacterImportedPackage package,
            Dictionary<string, ResourcePreviewInfo> resources,
            string importedPackageRoot,
            Transform bodyModel)
        {
            if (HasBakedSurface(importedPackageRoot))
                return ApplyBakedBodyMaterialBinding(importedPackageRoot, package.PackageId, bodyModel);
            CharacterBodyMaterialBindingStatus paletteStatus = TryApplyPaletteBodyMaterialBinding(
                package,
                resources,
                importedPackageRoot,
                bodyModel,
                out bool paletteHandled);
            if (paletteHandled)
            {
                if (paletteStatus.ShouldWarn)
                    Debug.LogWarning("MxFramework Character Preview: palette material binding failed. " + paletteStatus.Message);
                return paletteStatus;
            }

            CharacterBodyMaterialBindingStatus descriptorStatus = ApplyPackageMaterialDescriptorBindings(
                package,
                importedPackageRoot,
                bodyModel);
            if (!string.Equals(descriptorStatus.Status, "noPackageMaterialDescriptors", StringComparison.Ordinal))
                return descriptorStatus;

            CharacterBodyMaterialBindingStatus status = ResolveBodyMaterialBindingInputs(
                package,
                resources,
                importedPackageRoot,
                bodyModel,
                out ResourcePreviewInfo albedoResource,
                out Texture2D albedoTexture,
                out CharacterMaterialTemplate template,
                out Renderer[] renderers);
            if (!status.CanBind)
            {
                if (status.ShouldWarn)
                    Debug.LogWarning("MxFramework Character Preview: body material binding skipped. " + status.Message);
                return status;
            }

            string materialPath = GetGeneratedBodyMaterialPath(importedPackageRoot, package.PackageId);
            Material material = CreateOrUpdateGeneratedBodyMaterial(materialPath, template, albedoTexture, out string materialIssue);
            if (material == null)
            {
                status = CharacterBodyMaterialBindingStatus.Create(
                    "bodyMaterialMissing",
                    template.TemplateId,
                    materialPath,
                    albedoResource.Key,
                    0,
                    "Body material could not be created or updated. " + materialIssue);
                Debug.LogWarning("MxFramework Character Preview: body material binding skipped. " + status.Message);
                return status;
            }

            int assignedRendererCount = AssignGeneratedBodyMaterial(renderers, material);
            return CharacterBodyMaterialBindingStatus.Create(
                "bodyMaterialBound",
                template.TemplateId,
                materialPath,
                albedoResource.Key,
                assignedRendererCount,
                "Bound " + CharacterBodyMaterialRole + " using material template " + template.TemplateId + ".");
        }

        private static CharacterBodyMaterialBindingStatus ApplyPackageMaterialDescriptorBindings(
            CharacterImportedPackage package,
            string importedPackageRoot,
            Transform bodyModel)
        {
            List<PackageMaterialDescriptorBinding> bindings = ReadPackageMaterialDescriptorBindings(
                package,
                importedPackageRoot,
                out string descriptorIssue);
            if (bindings.Count == 0)
            {
                return string.IsNullOrWhiteSpace(descriptorIssue)
                    ? CharacterBodyMaterialBindingStatus.Create(
                        "noPackageMaterialDescriptors",
                        DefaultCharacterBodyMaterialTemplateId,
                        string.Empty,
                        string.Empty,
                        0,
                        string.Empty)
                    : CharacterBodyMaterialBindingStatus.Create(
                        "packageMaterialDescriptorInvalid",
                        DefaultCharacterBodyMaterialTemplateId,
                        string.Empty,
                        string.Empty,
                        0,
                        descriptorIssue);
            }

            CharacterMaterialTemplate template = CharacterMaterialTemplate.WGameCharacterTraditionalLit();
            if (!TryResolveTemplateShader(template, out string shaderIssue))
            {
                return CharacterBodyMaterialBindingStatus.Create(
                    "characterShaderMissing",
                    template.TemplateId,
                    bindings[0].GeneratedMaterialPath,
                    bindings[0].AlbedoTextureResourceKey,
                    0,
                    shaderIssue);
            }

            Renderer[] renderers = GetBodyMaterialRenderers(bodyModel);
            if (renderers.Length == 0)
            {
                return CharacterBodyMaterialBindingStatus.Create(
                    "bodyRendererMissing",
                    template.TemplateId,
                    bindings[0].GeneratedMaterialPath,
                    bindings[0].AlbedoTextureResourceKey,
                    0,
                    "BodyModel does not contain a MeshRenderer or SkinnedMeshRenderer.");
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                PackageMaterialDescriptorBinding binding = bindings[i];
                binding.GeneratedMaterial = CreateOrUpdateGeneratedPackageMaterial(
                    binding,
                    template,
                    out string materialIssue);
                if (binding.GeneratedMaterial == null)
                {
                    return CharacterBodyMaterialBindingStatus.Create(
                        "bodyMaterialMissing",
                        template.TemplateId,
                        binding.GeneratedMaterialPath,
                        binding.AlbedoTextureResourceKey,
                        0,
                        "Package material could not be generated for " + binding.SourceMaterialName + ". " + materialIssue);
                }
            }

            int assignedRendererCount = AssignPackageMaterialDescriptors(
                renderers,
                bindings,
                out int assignedSlotCount,
                out int materialSlotCount,
                out string[] unmatchedMaterialNames);
            PackageMaterialDescriptorBinding primary = FindPrimaryPackageMaterialBinding(bindings);
            if (assignedSlotCount == 0 || assignedSlotCount != materialSlotCount)
            {
                string unmatched = unmatchedMaterialNames.Length == 0
                    ? string.Empty
                    : " Unmatched source materials: " + string.Join(", ", unmatchedMaterialNames) + ".";
                return CharacterBodyMaterialBindingStatus.Create(
                    "bodyMaterialNotAssigned",
                    template.TemplateId,
                    primary.GeneratedMaterialPath,
                    primary.AlbedoTextureResourceKey,
                    assignedRendererCount,
                    "Assigned " + assignedSlotCount + " of " + materialSlotCount + " body material slots from package descriptors." + unmatched);
            }

            return CharacterBodyMaterialBindingStatus.Create(
                "bodyMaterialBound",
                template.TemplateId,
                primary.GeneratedMaterialPath,
                primary.AlbedoTextureResourceKey,
                assignedRendererCount,
                "Generated " + bindings.Count + " package materials and assigned all " + assignedSlotCount + " body material slots.");
        }

        private static List<PackageMaterialDescriptorBinding> ReadPackageMaterialDescriptorBindings(
            CharacterImportedPackage package,
            string importedPackageRoot,
            out string issue)
        {
            issue = string.Empty;
            var result = new List<PackageMaterialDescriptorBinding>();
            if (package?.UnityResourceCatalog == null)
                return result;

            for (int i = 0; i < package.UnityResourceCatalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = package.UnityResourceCatalog.Entries[i];
                if (entry == null || !string.Equals(entry.TypeId, ResourceTypeIds.Material, StringComparison.Ordinal))
                    continue;

                string descriptorPath = ResolveUnityAssetPath(entry, GetUnityAssetGuid(entry), requireGameObject: false);
                if (string.IsNullOrWhiteSpace(descriptorPath)
                    || !descriptorPath.EndsWith(".material.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                JObject descriptor;
                try
                {
                    descriptor = JObject.Parse(File.ReadAllText(descriptorPath));
                }
                catch (Exception ex)
                {
                    issue = "Material descriptor could not be read: " + descriptorPath + ". " + ex.Message;
                    result.Clear();
                    return result;
                }

                string sourceMaterialName = GetPackageMaterialDescriptorName(descriptor, descriptorPath);
                if (string.IsNullOrWhiteSpace(sourceMaterialName))
                {
                    issue = "Material descriptor has no stable source material name: " + descriptorPath + ".";
                    result.Clear();
                    return result;
                }

                JObject albedoTextureBinding = FindPackageMaterialAlbedoTextureBinding(descriptor);
                string albedoTextureResourceKey = ReadString(albedoTextureBinding, "resourceKey");
                Texture2D albedoTexture = LoadPackageMaterialTexture(package, albedoTextureResourceKey, out string texturePath);
                if (albedoTexture == null)
                {
                    issue = "Material descriptor " + sourceMaterialName
                        + " does not resolve a loadable albedo texture. resourceKey=" + albedoTextureResourceKey
                        + " asset=" + texturePath + ".";
                    result.Clear();
                    return result;
                }

                JObject normalTextureBinding = FindPackageMaterialTextureBinding(
                    descriptor,
                    "_WgNormalTex",
                    "_NormalMap",
                    "_BumpMap");
                string normalTextureResourceKey = ReadString(normalTextureBinding, "resourceKey");
                Texture2D normalTexture = LoadOptionalPackageMaterialTexture(
                    package,
                    normalTextureResourceKey,
                    sourceMaterialName,
                    "normal",
                    out string normalIssue);
                if (!string.IsNullOrWhiteSpace(normalIssue))
                {
                    issue = normalIssue;
                    result.Clear();
                    return result;
                }

                JObject maskTextureBinding = FindPackageMaterialTextureBinding(
                    descriptor,
                    "_WgMaskTex",
                    "_MaskMap",
                    "_MetallicGlossMap");
                string maskTextureResourceKey = ReadString(maskTextureBinding, "resourceKey");
                Texture2D maskTexture = LoadOptionalPackageMaterialTexture(
                    package,
                    maskTextureResourceKey,
                    sourceMaterialName,
                    "mask",
                    out string maskIssue);
                if (!string.IsNullOrWhiteSpace(maskIssue))
                {
                    issue = maskIssue;
                    result.Clear();
                    return result;
                }

                Vector2 textureScale = ParseVector2(albedoTextureBinding?["scale"] as JObject, Vector2.one);
                Vector2 textureOffset = ParseVector2(albedoTextureBinding?["offset"] as JObject, Vector2.zero);
                string declaredSurfaceMode = ReadString(descriptor, "surfaceMode");
                string surfaceMode = NormalizePackageMaterialSurfaceMode(declaredSurfaceMode);
                if (!string.IsNullOrWhiteSpace(declaredSurfaceMode) && string.IsNullOrWhiteSpace(surfaceMode))
                {
                    issue = "Material descriptor " + sourceMaterialName + " declares unsupported surfaceMode '"
                        + declaredSurfaceMode + "'. Expected Opaque or AlphaClip.";
                    result.Clear();
                    return result;
                }
                if (string.IsNullOrWhiteSpace(surfaceMode))
                    surfaceMode = OpaqueMaterialSurfaceMode;
                float alphaCutoff = ReadFloat(descriptor, "alphaCutoff", 0.5f);
                if (string.Equals(surfaceMode, AlphaClipMaterialSurfaceMode, StringComparison.Ordinal)
                    && (float.IsNaN(alphaCutoff) || float.IsInfinity(alphaCutoff) || alphaCutoff < 0f || alphaCutoff > 1f))
                {
                    issue = "Material descriptor " + sourceMaterialName
                        + " declares AlphaClip with alphaCutoff outside 0..1.";
                    result.Clear();
                    return result;
                }
                result.Add(new PackageMaterialDescriptorBinding
                {
                    ResourceKey = entry.Id,
                    DescriptorPath = descriptorPath,
                    SourceMaterialName = sourceMaterialName,
                    GeneratedMaterialPath = GetGeneratedPackageMaterialPath(importedPackageRoot, package.PackageId, descriptorPath),
                    AlbedoTextureResourceKey = albedoTextureResourceKey,
                    AlbedoTexture = albedoTexture,
                    NormalTextureResourceKey = normalTextureResourceKey,
                    NormalTexture = normalTexture,
                    MaskTextureResourceKey = maskTextureResourceKey,
                    MaskTexture = maskTexture,
                    AlbedoTint = ReadPackageMaterialColor(descriptor, "_Color", Color.white),
                    AlbedoTextureScale = textureScale,
                    AlbedoTextureOffset = textureOffset,
                    SurfaceMode = surfaceMode,
                    AlphaCutoff = alphaCutoff,
                    RenderQueue = ReadInt(descriptor, "renderQueue", -1),
                    EnableInstancing = ReadBool(descriptor, "enableInstancing"),
                    DoubleSidedGi = ReadBool(descriptor, "doubleSidedGi")
                });
            }

            return result;
        }

        private static string GetPackageMaterialDescriptorName(JObject descriptor, string descriptorPath)
        {
            string sourceAssetPath = ReadString(descriptor, "sourceAssetPath");
            if (!string.IsNullOrWhiteSpace(sourceAssetPath))
                return Path.GetFileNameWithoutExtension(sourceAssetPath);

            return GetPackageMaterialDescriptorId(descriptorPath);
        }

        private static string GetPackageMaterialDescriptorId(string descriptorPath)
        {
            string fileName = Path.GetFileName(descriptorPath) ?? string.Empty;
            const string suffix = ".material.json";
            return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - suffix.Length)
                : Path.GetFileNameWithoutExtension(fileName);
        }

        private static JObject FindPackageMaterialAlbedoTextureBinding(JObject descriptor)
        {
            JArray textures = descriptor?["textures"] as JArray;
            JObject best = null;
            int bestPriority = int.MaxValue;
            if (textures == null)
                return null;

            for (int i = 0; i < textures.Count; i++)
            {
                JObject candidate = textures[i] as JObject;
                string resourceKey = ReadString(candidate, "resourceKey");
                if (string.IsNullOrWhiteSpace(resourceKey))
                    continue;

                string property = ReadString(candidate, "property");
                int priority;
                if (string.Equals(property, "_MainTex", StringComparison.OrdinalIgnoreCase))
                    priority = 0;
                else if (string.Equals(property, "_BaseMap", StringComparison.OrdinalIgnoreCase))
                    priority = 1;
                else if (string.Equals(property, "_BaseColorMap", StringComparison.OrdinalIgnoreCase))
                    priority = 2;
                else if (resourceKey.IndexOf("mask", StringComparison.OrdinalIgnoreCase) >= 0
                    || resourceKey.IndexOf("matcap", StringComparison.OrdinalIgnoreCase) >= 0)
                    priority = int.MaxValue;
                else
                    priority = 10;

                if (priority < bestPriority)
                {
                    best = candidate;
                    bestPriority = priority;
                }
            }

            return best;
        }

        private static JObject FindPackageMaterialTextureBinding(JObject descriptor, params string[] propertyNames)
        {
            JArray textures = descriptor?["textures"] as JArray;
            for (int propertyIndex = 0; propertyNames != null && propertyIndex < propertyNames.Length; propertyIndex++)
            {
                for (int textureIndex = 0; textures != null && textureIndex < textures.Count; textureIndex++)
                {
                    JObject candidate = textures[textureIndex] as JObject;
                    if (string.Equals(ReadString(candidate, "property"), propertyNames[propertyIndex], StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(ReadString(candidate, "resourceKey")))
                        return candidate;
                }
            }
            return null;
        }

        private static Texture2D LoadOptionalPackageMaterialTexture(
            CharacterImportedPackage package,
            string resourceKey,
            string sourceMaterialName,
            string semantic,
            out string issue)
        {
            issue = string.Empty;
            if (string.IsNullOrWhiteSpace(resourceKey))
                return null;
            Texture2D texture = LoadPackageMaterialTexture(package, resourceKey, out string assetPath);
            if (texture == null)
                issue = "Material descriptor " + sourceMaterialName + " does not resolve a loadable " + semantic
                    + " texture. resourceKey=" + resourceKey + " asset=" + assetPath + ".";
            return texture;
        }

        private static Texture2D LoadPackageMaterialTexture(
            CharacterImportedPackage package,
            string resourceKey,
            out string assetPath)
        {
            assetPath = string.Empty;
            if (package?.UnityResourceCatalog == null || string.IsNullOrWhiteSpace(resourceKey))
                return null;

            for (int i = 0; i < package.UnityResourceCatalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = package.UnityResourceCatalog.Entries[i];
                if (entry == null || !string.Equals(entry.Id, resourceKey, StringComparison.Ordinal))
                    continue;

                assetPath = ResolveUnityAssetPath(entry, GetUnityAssetGuid(entry), requireGameObject: false);
                return string.IsNullOrWhiteSpace(assetPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }

            return null;
        }

        private static Color ReadPackageMaterialColor(JObject descriptor, string propertyName, Color fallback)
        {
            JArray colors = descriptor?["colors"] as JArray;
            if (colors == null)
                return fallback;

            for (int i = colors.Count - 1; i >= 0; i--)
            {
                JObject color = colors[i] as JObject;
                if (!string.Equals(ReadString(color, "name"), propertyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                JObject value = color?["value"] as JObject;
                if (value == null)
                    return fallback;
                return new Color(
                    ReadFloat(value, "r", fallback.r),
                    ReadFloat(value, "g", fallback.g),
                    ReadFloat(value, "b", fallback.b),
                    ReadFloat(value, "a", fallback.a));
            }

            return fallback;
        }

        private static Vector2 ParseVector2(JObject obj, Vector2 fallback)
        {
            return obj == null
                ? fallback
                : new Vector2(ReadFloat(obj, "x", fallback.x), ReadFloat(obj, "y", fallback.y));
        }

        private static string NormalizePackageMaterialSurfaceMode(string value)
        {
            if (string.Equals(value, OpaqueMaterialSurfaceMode, StringComparison.OrdinalIgnoreCase))
                return OpaqueMaterialSurfaceMode;
            if (string.Equals(value, AlphaClipMaterialSurfaceMode, StringComparison.OrdinalIgnoreCase))
                return AlphaClipMaterialSurfaceMode;
            return string.Empty;
        }

        private static Material CreateOrUpdateGeneratedPackageMaterial(
            PackageMaterialDescriptorBinding binding,
            CharacterMaterialTemplate template,
            out string issue)
        {
            Material material = CreateOrUpdateGeneratedBodyMaterial(
                binding.GeneratedMaterialPath,
                template,
                binding.AlbedoTexture,
                out issue);
            if (material == null)
                return null;

            if (material.HasProperty(template.AlbedoTintPropertyName))
                material.SetColor(template.AlbedoTintPropertyName, binding.AlbedoTint);
            if (material.HasProperty(template.AlbedoTexturePropertyName))
            {
                material.SetTextureScale(template.AlbedoTexturePropertyName, binding.AlbedoTextureScale);
                material.SetTextureOffset(template.AlbedoTexturePropertyName, binding.AlbedoTextureOffset);
            }
            if (binding.NormalTexture != null)
            {
                if (!material.HasProperty(template.NormalTexturePropertyName))
                {
                    issue = "Material template " + template.TemplateId + " is missing " + template.NormalTexturePropertyName + ".";
                    return null;
                }
            }
            if (material.HasProperty(template.NormalTexturePropertyName))
            {
                material.SetTexture(template.NormalTexturePropertyName, binding.NormalTexture);
                if (binding.NormalTexture != null)
                    material.EnableKeyword("_WG_NORMALMAP_ON");
                else
                    material.DisableKeyword("_WG_NORMALMAP_ON");
            }
            if (binding.MaskTexture != null)
            {
                if (!material.HasProperty(template.MaskTexturePropertyName))
                {
                    issue = "Material template " + template.TemplateId + " is missing " + template.MaskTexturePropertyName + ".";
                    return null;
                }
            }
            if (material.HasProperty(template.MaskTexturePropertyName))
            {
                material.SetTexture(template.MaskTexturePropertyName, binding.MaskTexture);
                if (binding.MaskTexture != null)
                    material.EnableKeyword("_WG_MATERIAL_MASK_ON");
                else
                    material.DisableKeyword("_WG_MATERIAL_MASK_ON");
            }

            if (!ApplyPackageMaterialSurfaceMode(material, binding, out issue))
                return null;

            material.enableInstancing = binding.EnableInstancing;
            material.doubleSidedGI = binding.DoubleSidedGi;
            material.renderQueue = binding.RenderQueue >= 0
                ? binding.RenderQueue
                : string.Equals(binding.SurfaceMode, AlphaClipMaterialSurfaceMode, StringComparison.Ordinal)
                    ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest
                    : (int)UnityEngine.Rendering.RenderQueue.Geometry;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static bool ApplyPackageMaterialSurfaceMode(
            Material material,
            PackageMaterialDescriptorBinding binding,
            out string issue)
        {
            issue = string.Empty;
            if (!material.HasProperty("_AlphaClip") || !material.HasProperty("_Cutoff"))
            {
                issue = "Material shader " + material.shader?.name
                    + " does not expose the AlphaClip variant contract (_AlphaClip and _Cutoff).";
                return false;
            }

            bool alphaClip = string.Equals(binding.SurfaceMode, AlphaClipMaterialSurfaceMode, StringComparison.Ordinal);
            material.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            material.SetFloat("_Cutoff", binding.AlphaCutoff);
            material.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
            if (alphaClip)
                material.EnableKeyword(AlphaClipKeyword);
            else
                material.DisableKeyword(AlphaClipKeyword);
            return true;
        }

        private static int AssignPackageMaterialDescriptors(
            Renderer[] renderers,
            List<PackageMaterialDescriptorBinding> bindings,
            out int assignedSlotCount,
            out int materialSlotCount,
            out string[] unmatchedMaterialNames)
        {
            assignedSlotCount = 0;
            materialSlotCount = 0;
            var unmatched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bySourceName = new Dictionary<string, PackageMaterialDescriptorBinding>(StringComparer.Ordinal);
            for (int i = 0; i < bindings.Count; i++)
            {
                string key = NormalizePackageMaterialName(bindings[i].SourceMaterialName);
                if (!string.IsNullOrWhiteSpace(key))
                    bySourceName[key] = bindings[i];
            }

            int assignedRendererCount = 0;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                bool rendererChanged = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material source = materials[materialIndex];
                    materialSlotCount++;
                    string sourceName = source != null ? source.name : string.Empty;
                    string key = NormalizePackageMaterialName(sourceName);
                    if (!bySourceName.TryGetValue(key, out PackageMaterialDescriptorBinding binding))
                        binding = FindPackageMaterialBindingForRenderer(renderer, bindings);
                    if (binding == null || binding.GeneratedMaterial == null)
                    {
                        unmatched.Add(string.IsNullOrWhiteSpace(sourceName) ? "<null>" : sourceName);
                        continue;
                    }

                    materials[materialIndex] = binding.GeneratedMaterial;
                    assignedSlotCount++;
                    rendererChanged = true;
                }

                if (!rendererChanged)
                    continue;
                renderer.sharedMaterials = materials;
                assignedRendererCount++;
                EditorUtility.SetDirty(renderer);
            }

            unmatchedMaterialNames = new string[unmatched.Count];
            unmatched.CopyTo(unmatchedMaterialNames);
            Array.Sort(unmatchedMaterialNames, StringComparer.OrdinalIgnoreCase);
            return assignedRendererCount;
        }

        private static PackageMaterialDescriptorBinding FindPackageMaterialBindingForRenderer(
            Renderer renderer,
            List<PackageMaterialDescriptorBinding> bindings)
        {
            string rendererName = NormalizePackageMaterialName(renderer != null ? renderer.name : string.Empty);
            PackageMaterialDescriptorBinding body = null;
            for (int i = 0; i < bindings.Count; i++)
            {
                PackageMaterialDescriptorBinding candidate = bindings[i];
                string candidateName = NormalizePackageMaterialName(candidate.SourceMaterialName);
                if (string.Equals(candidateName, "body", StringComparison.Ordinal))
                    body = candidate;
                if (!string.IsNullOrWhiteSpace(candidateName)
                    && !string.IsNullOrWhiteSpace(rendererName)
                    && rendererName.StartsWith(candidateName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return body;
        }

        private static PackageMaterialDescriptorBinding FindPrimaryPackageMaterialBinding(
            List<PackageMaterialDescriptorBinding> bindings)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                if (string.Equals(NormalizePackageMaterialName(bindings[i].SourceMaterialName), "body", StringComparison.Ordinal))
                    return bindings[i];
            }

            return bindings[0];
        }

        private static string NormalizePackageMaterialName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim();
            const string instanceSuffix = " (Instance)";
            if (normalized.EndsWith(instanceSuffix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - instanceSuffix.Length);

            int suffixStart = normalized.LastIndexOfAny(new[] { '_', '-', '.', ' ' });
            if (suffixStart >= 0 && suffixStart + 1 < normalized.Length)
            {
                bool numericSuffix = true;
                for (int i = suffixStart + 1; i < normalized.Length; i++)
                {
                    if (!char.IsDigit(normalized[i]))
                    {
                        numericSuffix = false;
                        break;
                    }
                }

                if (numericSuffix)
                    normalized = normalized.Substring(0, suffixStart);
            }

            var builder = new StringBuilder(normalized.Length);
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (char.IsLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }

        private static string GetGeneratedPackageMaterialPath(
            string importedPackageRoot,
            string packageId,
            string descriptorPath)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            string packageName = string.IsNullOrWhiteSpace(packageId)
                ? "character"
                : SanitizeGeneratedAssetPathSegment(packageId);
            return root + "/materials/" + packageName + "_"
                + SanitizeGeneratedAssetPathSegment(GetPackageMaterialDescriptorId(descriptorPath)) + ".mat";
        }

        private static string SanitizeGeneratedAssetPathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            char[] characters = value.Trim().ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                char c = characters[i];
                if (c < 32 || c == '<' || c == '>' || c == ':' || c == '"' || c == '/'
                    || c == '\\' || c == '|' || c == '?' || c == '*' || c == '.')
                {
                    characters[i] = '_';
                }
            }
            return new string(characters);
        }

        private static CharacterBodyMaterialBindingStatus CollectPackageMaterialDescriptorBindingStatus(
            CharacterImportedPackage package,
            string importedPackageRoot,
            Transform bodyModel)
        {
            List<PackageMaterialDescriptorBinding> bindings = ReadPackageMaterialDescriptorBindings(
                package,
                importedPackageRoot,
                out string descriptorIssue);
            if (bindings.Count == 0)
            {
                return CharacterBodyMaterialBindingStatus.Create(
                    string.IsNullOrWhiteSpace(descriptorIssue)
                        ? "noPackageMaterialDescriptors"
                        : "packageMaterialDescriptorInvalid",
                    DefaultCharacterBodyMaterialTemplateId,
                    string.Empty,
                    string.Empty,
                    0,
                    descriptorIssue);
            }

            PackageMaterialDescriptorBinding primary = FindPrimaryPackageMaterialBinding(bindings);
            var generatedMaterials = new HashSet<Material>();
            for (int i = 0; i < bindings.Count; i++)
            {
                PackageMaterialDescriptorBinding binding = bindings[i];
                binding.GeneratedMaterial = AssetDatabase.LoadAssetAtPath<Material>(binding.GeneratedMaterialPath);
                if (binding.GeneratedMaterial == null)
                {
                    return CharacterBodyMaterialBindingStatus.Create(
                        "bodyMaterialMissing",
                        DefaultCharacterBodyMaterialTemplateId,
                        binding.GeneratedMaterialPath,
                        binding.AlbedoTextureResourceKey,
                        0,
                        "Generated package material asset is missing for " + binding.SourceMaterialName + ".");
                }

                generatedMaterials.Add(binding.GeneratedMaterial);
            }

            Renderer[] renderers = GetBodyMaterialRenderers(bodyModel);
            if (renderers.Length == 0)
            {
                return CharacterBodyMaterialBindingStatus.Create(
                    "bodyRendererMissing",
                    DefaultCharacterBodyMaterialTemplateId,
                    primary.GeneratedMaterialPath,
                    primary.AlbedoTextureResourceKey,
                    0,
                    "BodyModel does not contain a MeshRenderer or SkinnedMeshRenderer.");
            }

            int materialSlotCount = 0;
            int assignedSlotCount = 0;
            int assignedRendererCount = 0;
            var unmatched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                bool rendererAssigned = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    materialSlotCount++;
                    if (material != null && generatedMaterials.Contains(material))
                    {
                        assignedSlotCount++;
                        rendererAssigned = true;
                    }
                    else
                    {
                        unmatched.Add(material == null ? "<null>" : material.name);
                    }
                }

                if (rendererAssigned)
                    assignedRendererCount++;
            }

            if (assignedSlotCount == 0 || assignedSlotCount != materialSlotCount)
            {
                string[] unmatchedNames = new string[unmatched.Count];
                unmatched.CopyTo(unmatchedNames);
                Array.Sort(unmatchedNames, StringComparer.OrdinalIgnoreCase);
                return CharacterBodyMaterialBindingStatus.Create(
                    "bodyMaterialNotAssigned",
                    DefaultCharacterBodyMaterialTemplateId,
                    primary.GeneratedMaterialPath,
                    primary.AlbedoTextureResourceKey,
                    assignedRendererCount,
                    "Generated package materials are assigned to " + assignedSlotCount + " of " + materialSlotCount
                        + " body material slots. Unmatched: " + string.Join(", ", unmatchedNames) + ".");
            }

            return CharacterBodyMaterialBindingStatus.Create(
                "bodyMaterialBound",
                DefaultCharacterBodyMaterialTemplateId,
                primary.GeneratedMaterialPath,
                primary.AlbedoTextureResourceKey,
                assignedRendererCount,
                "Generated package materials are assigned to every body material slot.");
        }

        private static CharacterGeneratedMaterialBuildResult[] CollectGeneratedMaterialBuildResults(
            string importedPackageRoot,
            CharacterImportedPackage package,
            GameObject prefab)
        {
            List<PackageMaterialDescriptorBinding> bindings = ReadPackageMaterialDescriptorBindings(
                package,
                importedPackageRoot,
                out _);
            if (bindings.Count == 0)
                return Array.Empty<CharacterGeneratedMaterialBuildResult>();
            Transform bodyModel = prefab != null ? FindChildByName(prefab.transform, "BodyModel") : null;
            Renderer[] renderers = GetBodyMaterialRenderers(bodyModel);
            var results = new CharacterGeneratedMaterialBuildResult[bindings.Count];
            for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                PackageMaterialDescriptorBinding binding = bindings[bindingIndex];
                Material material = AssetDatabase.LoadAssetAtPath<Material>(binding.GeneratedMaterialPath);
                int assignedSlots = 0;
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Material[] materials = renderers[rendererIndex]?.sharedMaterials ?? Array.Empty<Material>();
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                        if (materials[materialIndex] == material)
                            assignedSlots++;
                }
                results[bindingIndex] = new CharacterGeneratedMaterialBuildResult
                {
                    SourceMaterialName = binding.SourceMaterialName,
                    MaterialPath = binding.GeneratedMaterialPath,
                    AlbedoTextureResourceKey = binding.AlbedoTextureResourceKey,
                    NormalTextureResourceKey = binding.NormalTextureResourceKey,
                    MaskTextureResourceKey = binding.MaskTextureResourceKey,
                    SurfaceMode = material != null && material.IsKeywordEnabled(AlphaClipKeyword)
                        ? AlphaClipMaterialSurfaceMode
                        : OpaqueMaterialSurfaceMode,
                    AlphaCutoff = material != null && material.HasProperty("_Cutoff")
                        ? material.GetFloat("_Cutoff")
                        : binding.AlphaCutoff,
                    AssignedSlotCount = assignedSlots,
                    Status = material == null ? "Missing" : assignedSlots > 0 ? "Assigned" : "Unassigned"
                };
            }
            Array.Sort(results, (left, right) => string.CompareOrdinal(left?.SourceMaterialName, right?.SourceMaterialName));
            return results;
        }

        private static CharacterBodyMaterialBindingStatus ResolveBodyMaterialBindingInputs(
            CharacterImportedPackage package,
            Dictionary<string, ResourcePreviewInfo> resources,
            string importedPackageRoot,
            Transform bodyModel,
            out ResourcePreviewInfo albedoResource,
            out Texture2D albedoTexture,
            out CharacterMaterialTemplate template,
            out Renderer[] renderers)
        {
            albedoResource = FindBodyAlbedoTextureResource(package, resources);
            albedoTexture = null;
            template = CharacterMaterialTemplate.WGameCharacterTraditionalLit();
            renderers = Array.Empty<Renderer>();

            if (albedoResource == null)
                return CharacterBodyMaterialBindingStatus.Create("noBodyAlbedo", template.TemplateId, string.Empty, string.Empty, 0, "No body albedo texture resource is configured.");

            if (!TryResolveTemplateShader(template, out string shaderIssue))
                return CharacterBodyMaterialBindingStatus.Create(
                    "characterShaderMissing",
                    template.TemplateId,
                    GetGeneratedBodyMaterialPath(importedPackageRoot, package?.PackageId),
                    albedoResource.Key,
                    0,
                    shaderIssue);

            albedoTexture = LoadBodyAlbedoTexture(albedoResource);
            if (albedoTexture == null)
                return CharacterBodyMaterialBindingStatus.Create(
                    "bodyTextureMissing",
                    template.TemplateId,
                    GetGeneratedBodyMaterialPath(importedPackageRoot, package?.PackageId),
                    albedoResource.Key,
                    0,
                    "Body albedo texture could not be loaded. " + FormatResourceDiagnostic(albedoResource));

            renderers = GetBodyMaterialRenderers(bodyModel);
            if (renderers.Length == 0)
                return CharacterBodyMaterialBindingStatus.Create(
                    "bodyRendererMissing",
                    template.TemplateId,
                    GetGeneratedBodyMaterialPath(importedPackageRoot, package?.PackageId),
                    albedoResource.Key,
                    0,
                    "BodyModel does not contain a MeshRenderer or SkinnedMeshRenderer.");

            return CharacterBodyMaterialBindingStatus.Create(
                "ready",
                template.TemplateId,
                GetGeneratedBodyMaterialPath(importedPackageRoot, package?.PackageId),
                albedoResource.Key,
                renderers.Length,
                string.Empty);
        }

        private static ResourcePreviewInfo FindBodyAlbedoTextureResource(
            CharacterImportedPackage package,
            Dictionary<string, ResourcePreviewInfo> resources)
        {
            if (resources == null)
                return null;

            string exactKey = package != null && !string.IsNullOrWhiteSpace(package.PackageId)
                ? "char." + package.PackageId + CharacterBodyAlbedoResourceSuffix
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(exactKey)
                && resources.TryGetValue(exactKey, out ResourcePreviewInfo exact)
                && IsTextureResource(exact))
            {
                return exact;
            }

            foreach (ResourcePreviewInfo resource in resources.Values)
            {
                if (resource == null || !IsTextureResource(resource))
                    continue;
                if (resource.Key.EndsWith(CharacterBodyAlbedoResourceSuffix, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(resource.Key, "texture.body_albedo", StringComparison.OrdinalIgnoreCase))
                {
                    return resource;
                }
            }

            return null;
        }

        private static bool IsTextureResource(ResourcePreviewInfo resource)
        {
            return resource != null
                && (string.Equals(resource.Usage, "texture", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFileNameWithoutExtension(resource.AssetPath), "body_albedo", StringComparison.OrdinalIgnoreCase));
        }

        private static Texture2D LoadBodyAlbedoTexture(ResourcePreviewInfo resource)
        {
            return resource == null || string.IsNullOrWhiteSpace(resource.AssetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(resource.AssetPath);
        }

        private static bool TryResolveTemplateShader(CharacterMaterialTemplate template, out string issue)
        {
            if (!template.IsValid)
            {
                issue = "Material template is invalid.";
                return false;
            }

            if (Shader.Find(template.ShaderName) != null)
            {
                issue = string.Empty;
                return true;
            }

            issue = "Shader was not found for material template " + template.TemplateId + ": " + template.ShaderName;
            return false;
        }

        private static Material CreateOrUpdateGeneratedBodyMaterial(
            string materialPath,
            CharacterMaterialTemplate template,
            Texture2D albedoTexture,
            out string issue)
        {
            issue = string.Empty;
            if (string.IsNullOrWhiteSpace(materialPath))
            {
                issue = "Generated body material path is empty.";
                return null;
            }

            Shader shader = Shader.Find(template.ShaderName);
            if (shader == null)
            {
                issue = "Shader was not found: " + template.ShaderName;
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

            var surface = new CharacterGeneratedMaterialSurface(albedoTexture, Color.white);
            if (!template.Apply(material, surface, out issue))
                return null;

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static Renderer[] GetBodyMaterialRenderers(Transform bodyModel)
        {
            if (bodyModel == null)
                return Array.Empty<Renderer>();

            Renderer[] allRenderers = bodyModel.GetComponentsInChildren<Renderer>(includeInactive: true);
            var result = new List<Renderer>(allRenderers.Length);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer renderer = allRenderers[i];
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    result.Add(renderer);
            }

            return result.ToArray();
        }

        private static int AssignGeneratedBodyMaterial(Renderer[] renderers, Material material)
        {
            if (renderers == null || material == null)
                return 0;

            int assignedCount = 0;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    renderer.sharedMaterial = material;
                    assignedCount++;
                    EditorUtility.SetDirty(renderer);
                    continue;
                }

                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;
                renderer.sharedMaterials = materials;
                assignedCount++;
                EditorUtility.SetDirty(renderer);
            }

            return assignedCount;
        }

        private static string GetGeneratedBodyMaterialPath(string importedPackageRoot, string packageId)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            string name = string.IsNullOrWhiteSpace(packageId) ? "character" : SanitizeName(packageId);
            return root + "/materials/" + name + "_Body.mat";
        }

        private static ResourcePreviewInfo ResolveRuntimeModelResource(
            ResourcePreviewInfo body,
            string importedPackageRoot,
            AnimationClip[] animationClips,
            string rigType)
        {
            if (body == null
                || !string.Equals(rigType, "Humanoid", StringComparison.Ordinal)
                || !RequiresHumanoidAvatar(animationClips))
                return body;

            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(body.AvatarSourceAssetPath))
                AddHumanoidAvatarCandidate(candidates, body.AvatarSourceAssetPath);
            List<string> preferredCandidates = FindPreferredHumanoidAvatarAssetPaths(body.AssetPath, importedPackageRoot);
            for (int candidateIndex = 0; candidateIndex < preferredCandidates.Count; candidateIndex++)
                AddHumanoidAvatarCandidate(candidates, preferredCandidates[candidateIndex]);
            for (int i = 0; i < candidates.Count; i++)
            {
                string candidatePath = candidates[i];
                Avatar avatar = LoadHumanoidAvatarAtPath(candidatePath, configureModelImporter: true);
                if (avatar == null || !CanLoadGameObject(candidatePath))
                    continue;

                if (string.Equals(candidatePath, body.AssetPath, StringComparison.OrdinalIgnoreCase))
                    return body;

                Debug.Log("MxFramework Character Preview: using companion Humanoid model for runtime animation. packageAsset="
                    + body.AssetPath + " runtimeAsset=" + candidatePath);
                return body.WithResolvedAsset(
                    candidatePath,
                    AssetDatabase.AssetPathToGUID(candidatePath),
                    "Imported",
                    true,
                    "Using companion Humanoid FBX for runtime animation preview.");
            }

            return body;
        }

        private static Dictionary<string, GameObject> BuildWeaponPrefabs(
            CharacterImportedPackage package,
            Dictionary<string, ResourcePreviewInfo> resources,
            string importedPackageRoot)
        {
            var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            if (package == null || package.Geometry == null || package.Geometry.WeaponAttachments.Length == 0)
                return result;

            string prefabFolder = importedPackageRoot + "/prefabs";
            EnsureFolder(importedPackageRoot, "prefabs");
            EnsureFolder(prefabFolder, "weapons");
            string weaponFolder = prefabFolder + "/weapons";

            for (int i = 0; i < package.Geometry.WeaponAttachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = package.Geometry.WeaponAttachments[i];
                string resourceKey = GetWeaponVisualResourceKey(attachment);
                if (string.IsNullOrWhiteSpace(resourceKey))
                    continue;
                if (HasRuntimeWeaponResource(attachment))
                {
                    DeleteGeneratedWeaponPrefabIfExists(importedPackageRoot, package, attachment);
                    continue;
                }

                ResourcePreviewInfo resource = FindResource(resources, usage: "weaponModel", resourceKey: resourceKey);
                if (resource == null || !resource.IsImportReady)
                {
                    Debug.LogWarning("MxFramework Character Preview: skipping generated weapon prefab and runtime default mount. weapon="
                        + attachment.WeaponId + " slot=" + attachment.EquipSlot + " " + FormatResourceDiagnostic(resource));
                    continue;
                }

                string prefabName = package.PackageId + "_" + SanitizeName(attachment.EquipmentSlotLabel) + "_" + SanitizeName(attachment.WeaponLabel);
                string prefabPath = weaponFolder + "/" + prefabName + ".prefab";
                    var root = new GameObject(prefabName);
                    try
                    {
                        root.SetActive(false);
                        CreateModelWrapper("Model", resource, root.transform);
                        AddWeaponTraceLocators(package, attachment, root.transform);
                        root.SetActive(true);
                        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        MirrorPrefabToRuntimeResources(importedPackageRoot, package.PackageId, prefabPath);
                        if (prefab != null)
                        result[GetAttachmentKey(attachment)] = prefab;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            return result;
        }

        private static void AddRuntimePrefabDescriptor(GameObject rootObject, CharacterImportedPackage package)
        {
            var descriptor = rootObject.AddComponent<CharacterRuntimePrefabDescriptor>();
            descriptor.Configure(
                package?.PackageId,
                package == null ? string.Empty : CharacterRuntimePrefabResourceCatalog.GetCharacterPrefabResourceId(package.PackageId),
                "default");
            EditorUtility.SetDirty(descriptor);
        }

        private static CharacterRuntimeLocomotionBlendController.DirectClipBlendPoint[] LoadDirectClipBlendPoints(
            string importedPackageRoot)
        {
            string animationSetPath = NormalizeProjectPath(importedPackageRoot) + "/config/animation_set_definition.json";
            if (!File.Exists(animationSetPath))
                return Array.Empty<CharacterRuntimeLocomotionBlendController.DirectClipBlendPoint>();

            JObject root = JObject.Parse(File.ReadAllText(animationSetPath));
            JArray sets = root["sets"] as JArray;
            for (int setIndex = 0; sets != null && setIndex < sets.Count; setIndex++)
            {
                JObject set = sets[setIndex] as JObject;
                JArray groups = set?["groups"] as JArray;
                for (int groupIndex = 0; groups != null && groupIndex < groups.Count; groupIndex++)
                {
                    JObject group = groups[groupIndex] as JObject;
                    Dictionary<string, JObject> clipsById = ReadAnimationClipsById(group);
                    JArray blends = group?["blend2D"] as JArray;
                    for (int blendIndex = 0; blends != null && blendIndex < blends.Count; blendIndex++)
                    {
                        JObject blend = blends[blendIndex] as JObject;
                        if (blend == null)
                            continue;
                        string blendId = ReadString(blend, "blendId");
                        if (!string.IsNullOrWhiteSpace(blendId)
                            && !string.Equals(blendId, "blend.move2d", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        JArray points = blend["points"] as JArray;
                        var result = new List<CharacterRuntimeLocomotionBlendController.DirectClipBlendPoint>();
                        for (int pointIndex = 0; points != null && pointIndex < points.Count; pointIndex++)
                        {
                            JObject point = points[pointIndex] as JObject;
                            if (point == null)
                                continue;

                            string clipId = ReadString(point, "clipId");
                            if (string.IsNullOrWhiteSpace(clipId) || !clipsById.TryGetValue(clipId, out JObject clip))
                                continue;

                            string sourceClipName = ReadString(clip, "sourceClipName");
                            if (string.IsNullOrWhiteSpace(sourceClipName))
                                continue;

                            result.Add(new CharacterRuntimeLocomotionBlendController.DirectClipBlendPoint(
                                sourceClipName,
                                ReadFloat(point, "x", 0f),
                                ReadFloat(point, "y", 0f),
                                ReadFloat(clip, "speed", 1f),
                                ReadBool(clip, "loop", true)));
                        }

                        if (result.Count > 0)
                            return result.ToArray();
                    }
                }
            }

            return Array.Empty<CharacterRuntimeLocomotionBlendController.DirectClipBlendPoint>();
        }

        private static Dictionary<string, JObject> ReadAnimationClipsById(JObject group)
        {
            var result = new Dictionary<string, JObject>(StringComparer.Ordinal);
            JArray clips = group?["clips"] as JArray;
            for (int i = 0; clips != null && i < clips.Count; i++)
            {
                JObject clip = clips[i] as JObject;
                string clipId = ReadString(clip, "clipId");
                if (!string.IsNullOrWhiteSpace(clipId))
                    result[clipId] = clip;
            }

            return result;
        }

        private static void AssignHumanoidAvatarIfNeeded(
            Animator animator,
            AnimationClip[] animationClips,
            string importedPackageRoot,
            string modelAssetPath)
        {
            if (animator == null)
                return;
            if (!RequiresHumanoidAvatar(animationClips))
                return;
            Avatar avatar = FindHumanoidAvatar(modelAssetPath, importedPackageRoot);
            if (avatar == null)
            {
                string message = "MxFramework Character Preview: Humanoid animation clips are configured, but the current body model did not provide a valid Humanoid Avatar. "
                    + "Prefab build aborted to avoid binding an unrelated model Avatar. modelAssetPath=" + NormalizeProjectPath(modelAssetPath);
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }

            if (animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
            {
                string currentAvatarPath = NormalizeProjectPath(AssetDatabase.GetAssetPath(animator.avatar));
                string expectedAvatarPath = NormalizeProjectPath(AssetDatabase.GetAssetPath(avatar));
                if (string.Equals(currentAvatarPath, expectedAvatarPath, StringComparison.OrdinalIgnoreCase))
                    return;

                Debug.LogWarning("MxFramework Character Preview: replacing Humanoid Avatar from a different model. current="
                    + currentAvatarPath + " expected=" + expectedAvatarPath);
            }

            animator.avatar = avatar;
            ConfigurePreviewAnimator(animator);
            EditorUtility.SetDirty(animator);
        }

        private static void ConfigurePreviewAnimator(Animator animator)
        {
            if (animator == null)
                return;

            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
        }

        private static bool RequiresHumanoidAvatar(AnimationClip[] animationClips)
        {
            for (int i = 0; animationClips != null && i < animationClips.Length; i++)
            {
                AnimationClip clip = animationClips[i];
                if (clip != null && clip.humanMotion)
                    return true;
            }

            return false;
        }

        private static Avatar FindHumanoidAvatar(string modelAssetPath, string importedPackageRoot)
        {
            List<string> preferredAvatarPaths = FindPreferredHumanoidAvatarAssetPaths(modelAssetPath, importedPackageRoot);
            for (int i = 0; i < preferredAvatarPaths.Count; i++)
            {
                Avatar avatar = LoadHumanoidAvatarAtPath(preferredAvatarPaths[i], configureModelImporter: true);
                if (avatar != null)
                    return avatar;
            }

            return null;
        }

        private static List<string> FindPreferredHumanoidAvatarAssetPaths(string modelAssetPath, string importedPackageRoot)
        {
            var candidates = new List<string>();
            modelAssetPath = NormalizeProjectPath(modelAssetPath);
            AddHumanoidAvatarCandidate(candidates, modelAssetPath);

            string modelDirectory = Path.GetDirectoryName(modelAssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(modelDirectory))
            {
                string baseName = Path.GetFileNameWithoutExtension(modelAssetPath);
                AddHumanoidAvatarCandidate(candidates, modelDirectory + "/" + baseName + ".fbx");
                const string lightSuffix = "_light";
                if (!string.IsNullOrWhiteSpace(baseName)
                    && baseName.EndsWith(lightSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    AddHumanoidAvatarCandidate(
                        candidates,
                        modelDirectory + "/" + baseName.Substring(0, baseName.Length - lightSuffix.Length) + ".fbx");
                }

                AddFbxAvatarCandidatesInDirectory(candidates, modelDirectory);
            }

            string packageModelDirectory = NormalizeProjectPath(importedPackageRoot).TrimEnd('/', '\\') + "/resources/models";
            AddFbxAvatarCandidatesInDirectory(candidates, packageModelDirectory);
            return candidates;
        }

        private static void AddFbxAvatarCandidatesInDirectory(List<string> candidates, string directory)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(directory))
                return;

            string fullDirectory = Path.GetFullPath(directory);
            if (!Directory.Exists(fullDirectory))
                return;

            string[] files = Directory.GetFiles(fullDirectory, "*.fbx", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
                AddHumanoidAvatarCandidate(candidates, NormalizeProjectPath(files[i]));
        }

        private static void AddHumanoidAvatarCandidate(List<string> candidates, string assetPath)
        {
            if (candidates == null)
                return;

            assetPath = NormalizeProjectPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath)
                || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                || candidates.Contains(assetPath))
            {
                return;
            }

            candidates.Add(assetPath);
        }

        private static Avatar LoadHumanoidAvatarAtPath(string assetPath, bool configureModelImporter)
        {
            assetPath = NormalizeProjectPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            if (configureModelImporter)
                EnsureHumanoidModelImporter(assetPath);

            Avatar avatar = FindValidHumanoidAvatar(assetPath);
            if (avatar != null)
                return avatar;

            // A freshly replaced FBX can expose its new Transform hierarchy one import later
            // than the persisted HumanDescription. Force one synchronous hierarchy refresh
            // before declaring the Avatar invalid, so newly inserted WGame torso bones are
            // incorporated without a manual Inspector reimport.
            if (configureModelImporter)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                EnsureHumanoidModelImporter(assetPath);
                avatar = FindValidHumanoidAvatar(assetPath);
                if (avatar != null)
                    return avatar;
            }

            return null;
        }

        private static Avatar FindValidHumanoidAvatar(string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                if (assets[assetIndex] is Avatar avatar && avatar.isValid && avatar.isHuman)
                    return avatar;
            return null;
        }

        private static Avatar LoadGenericAvatarAtPath(string assetPath, bool configureModelImporter)
        {
            assetPath = NormalizeProjectPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;
            if (configureModelImporter)
                EnsureGenericModelImporter(assetPath);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                if (assets[assetIndex] is Avatar avatar && avatar.isValid && !avatar.isHuman)
                    return avatar;
            return null;
        }

        private static void EnsureGenericModelImporter(string assetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
                return;
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                changed = true;
            }
            if (changed)
                importer.SaveAndReimport();
        }

        private static void EnsureHumanoidModelImporter(string assetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
                return;

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null)
                    return;
            }

            if (RefreshHumanoidSkeletonFromImportedModel(importer, assetPath))
                importer.SaveAndReimport();
        }

        private static bool RefreshHumanoidSkeletonFromImportedModel(
            ModelImporter importer,
            string assetPath)
        {
            if (importer == null || string.IsNullOrWhiteSpace(assetPath))
                return false;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
                return false;

            HumanDescription description = importer.humanDescription;
            SkeletonBone[] source = description.skeleton;
            if (source == null || source.Length == 0)
                return false;

            Transform[] transforms = model.GetComponentsInChildren<Transform>(includeInactive: true);
            var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
            var hierarchyOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null || string.IsNullOrWhiteSpace(transform.name))
                    continue;
                if (!byName.TryAdd(transform.name, transform))
                    duplicates.Add(transform.name);
                else
                    hierarchyOrder[transform.name] = i;
            }

            var updated = new List<SkeletonBone>(source.Length + 2);
            var skeletonNames = new HashSet<string>(StringComparer.Ordinal);
            var consumedTransforms = new HashSet<Transform>();
            bool changed = false;
            for (int i = 0; i < source.Length; i++)
            {
                SkeletonBone previous = source[i];
                string lookupName = previous.name ?? string.Empty;
                if (!byName.TryGetValue(lookupName, out Transform transform)
                    && lookupName.EndsWith("(Clone)", StringComparison.Ordinal))
                {
                    byName.TryGetValue(
                        lookupName.Substring(0, lookupName.Length - "(Clone)".Length),
                        out transform);
                }

                if (transform == null)
                {
                    Debug.LogWarning(
                        "MxFramework Character Import: dropping stale humanoid SkeletonBone '"
                        + lookupName + "' because the imported hierarchy no longer contains it. asset=" + assetPath);
                    changed = true;
                    continue;
                }
                if (duplicates.Contains(transform.name))
                {
                    Debug.LogWarning(
                        "MxFramework Character Import: humanoid skeleton refresh could not uniquely resolve '"
                        + lookupName + "' in " + assetPath + "; preserving the existing HumanDescription.");
                    return false;
                }

                var updatedBone = new SkeletonBone
                {
                    name = previous.name,
                    position = transform.localPosition,
                    rotation = transform.localRotation,
                    scale = transform.localScale
                };
                updated.Add(updatedBone);
                consumedTransforms.Add(transform);
                skeletonNames.Add(updatedBone.name ?? string.Empty);
                changed |= (updatedBone.position - previous.position).sqrMagnitude > 1e-12f
                    || Quaternion.Angle(updatedBone.rotation, previous.rotation) > 0.0001f
                    || (updatedBone.scale - previous.scale).sqrMagnitude > 1e-12f;
            }

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null
                    || consumedTransforms.Contains(transform)
                    || duplicates.Contains(transform.name)
                    || skeletonNames.Contains(transform.name))
                {
                    continue;
                }

                updated.Add(new SkeletonBone
                {
                    name = transform.name,
                    position = transform.localPosition,
                    rotation = transform.localRotation,
                    scale = transform.localScale
                });
                consumedTransforms.Add(transform);
                skeletonNames.Add(transform.name);
                changed = true;
            }

            var human = new List<HumanBone>(description.human ?? Array.Empty<HumanBone>());
            for (int i = human.Count - 1; i >= 0; i--)
            {
                HumanBone mapping = human[i];
                if (byName.ContainsKey(mapping.boneName) && !duplicates.Contains(mapping.boneName))
                    continue;
                Debug.LogWarning(
                    "MxFramework Character Import: removing stale Humanoid mapping "
                    + mapping.humanName + " -> " + mapping.boneName + " because the imported bone no longer exists. asset=" + assetPath);
                human.RemoveAt(i);
                changed = true;
            }

            bool hasWGameTorso = byName.ContainsKey("spine_01.x")
                && byName.ContainsKey("spine_02.x")
                && byName.ContainsKey("upper_chest.x")
                && byName.ContainsKey("neck.x")
                && byName.ContainsKey("head.x");
            if (hasWGameTorso)
            {
                changed |= AddMissingSkeletonBone(updated, skeletonNames, byName["upper_chest.x"]);
                changed |= EnsureHumanBone(human, "Chest", "spine_02.x");
                changed |= EnsureHumanBone(human, "UpperChest", "upper_chest.x");
            }
            description.human = human.ToArray();

            changed |= SortSkeletonBonesByImportedHierarchy(updated, hierarchyOrder);

            if (!changed)
                return false;

            description.skeleton = updated.ToArray();
            importer.humanDescription = description;
            return true;
        }

        private static bool AddMissingSkeletonBone(
            List<SkeletonBone> skeleton,
            HashSet<string> skeletonNames,
            Transform transform)
        {
            if (transform == null || skeletonNames.Contains(transform.name))
                return false;
            skeleton.Add(new SkeletonBone
            {
                name = transform.name,
                position = transform.localPosition,
                rotation = transform.localRotation,
                scale = transform.localScale
            });
            skeletonNames.Add(transform.name);
            return true;
        }

        private static bool SortSkeletonBonesByImportedHierarchy(
            List<SkeletonBone> skeleton,
            IReadOnlyDictionary<string, int> hierarchyOrder)
        {
            if (skeleton == null || skeleton.Count < 2 || hierarchyOrder == null)
                return false;

            var previousOrder = new string[skeleton.Count];
            for (int i = 0; i < skeleton.Count; i++)
                previousOrder[i] = skeleton[i].name ?? string.Empty;

            skeleton.Sort((left, right) =>
                ResolveSkeletonHierarchyOrder(left.name, hierarchyOrder)
                    .CompareTo(ResolveSkeletonHierarchyOrder(right.name, hierarchyOrder)));

            for (int i = 0; i < skeleton.Count; i++)
                if (!string.Equals(previousOrder[i], skeleton[i].name, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static int ResolveSkeletonHierarchyOrder(
            string boneName,
            IReadOnlyDictionary<string, int> hierarchyOrder)
        {
            boneName = boneName ?? string.Empty;
            if (hierarchyOrder.TryGetValue(boneName, out int order))
                return order;
            const string cloneSuffix = "(Clone)";
            if (boneName.EndsWith(cloneSuffix, StringComparison.Ordinal)
                && hierarchyOrder.TryGetValue(
                    boneName.Substring(0, boneName.Length - cloneSuffix.Length),
                    out order))
                return order;
            return int.MaxValue;
        }

        private static bool EnsureHumanBone(List<HumanBone> human, string humanName, string boneName)
        {
            for (int i = 0; i < human.Count; i++)
            {
                HumanBone current = human[i];
                if (!string.Equals(current.humanName, humanName, StringComparison.Ordinal))
                    continue;
                if (string.Equals(current.boneName, boneName, StringComparison.Ordinal))
                    return false;
                current.boneName = boneName;
                current.limit.useDefaultValues = true;
                human[i] = current;
                return true;
            }

            human.Add(new HumanBone
            {
                humanName = humanName,
                boneName = boneName,
                limit = new HumanLimit { useDefaultValues = true }
            });
            return true;
        }

        private static AnimationClip[] LoadAnimationClips(CharacterImportedPackage package, string importedPackageRoot)
        {
            var clips = new List<AnimationClip>();
            if (package != null && package.UnityResourceCatalog != null)
            {
                for (int i = 0; i < package.UnityResourceCatalog.Entries.Count; i++)
                {
                    ResourceCatalogEntry entry = package.UnityResourceCatalog.Entries[i];
                    if (!IsAnimationResource(entry))
                        continue;
                    if (!IsUnityCatalogEntryImported(entry))
                        continue;

                    string assetPath = ResolveUnityAssetPath(entry, GetUnityAssetGuid(entry), requireGameObject: false);
                    if (string.IsNullOrWhiteSpace(assetPath))
                    {
                        Debug.LogWarning("MxFramework Character Preview: animation catalog entry is imported but the Unity asset could not be resolved. resource="
                            + entry.Id + " status=" + GetImportStatus(entry) + " address=" + entry.Address + FormatAssetGuid(GetUnityAssetGuid(entry)));
                        continue;
                    }

                    AddAnimationClipsAtPath(assetPath, clips);
                }
            }

            AddAnimationClipsFromRegistry(importedPackageRoot, clips);
            return clips.ToArray();
        }

        private static void AddAnimationClipsFromRegistry(string importedPackageRoot, List<AnimationClip> clips)
        {
            string registryPath = NormalizeProjectPath(importedPackageRoot) + "/config/animation_clip_registry.json";
            if (!File.Exists(registryPath))
                return;

            JObject root = JObject.Parse(File.ReadAllText(registryPath));
            JArray registryClips = root["clips"] as JArray;
            for (int i = 0; registryClips != null && i < registryClips.Count; i++)
            {
                JObject clip = registryClips[i] as JObject;
                if (clip == null)
                    continue;

                string resourceKey = ReadString(clip, "runtimeResourceKey");
                string sourceClipName = ReadString(clip, "sourceClipName");
                string assetPath = ReadString(clip["sourceSelection"] as JObject, "unityAssetPath");
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = FindProjectRuntimeCatalogAssetPath(resourceKey);

                AnimationClip animationClip = LoadAnimationClipAsset(assetPath, sourceClipName);
                if (animationClip == null || clips.Contains(animationClip))
                    continue;

                clips.Add(animationClip);
            }
        }

        private static bool IsAnimationResource(ResourceCatalogEntry entry)
        {
            if (entry == null)
                return false;

            return string.Equals(entry.TypeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal)
                || string.Equals(entry.TypeId, "AnimationClipGroup", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.TypeId, "animation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetProviderData(entry, "usage"), "animationClipGroup", StringComparison.OrdinalIgnoreCase)
                || HasLabel(entry, "character.animation")
                || HasLabel(entry, "character.usage.animationclipgroup");
        }

        private static void AddAnimationClipsAtPath(string assetPath, List<AnimationClip> clips)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clips.Contains(clip))
                    clips.Add(clip);
            }
        }

        private static void CreateWeapons(
            CharacterImportedPackage package,
            Dictionary<string, ResourcePreviewInfo> resources,
            Transform weaponsRoot,
            Dictionary<int, Transform> sockets)
        {
            for (int i = 0; i < package.Geometry.WeaponAttachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = package.Geometry.WeaponAttachments[i];
                string resourceKey = GetWeaponVisualResourceKey(attachment);
                if (string.IsNullOrWhiteSpace(resourceKey))
                    continue;

                Transform parent = sockets.TryGetValue(attachment.AttachSocketId, out Transform socket)
                    ? socket
                    : weaponsRoot;

                var slotRoot = new GameObject(attachment.EquipSlot + "_Slot").transform;
                slotRoot.SetParent(parent, false);
                ApplyPose(slotRoot, attachment.SlotPose);
                if (parent != weaponsRoot)
                    slotRoot.SetParent(weaponsRoot, true);

                var weaponRoot = new GameObject(attachment.EquipSlot + "_" + attachment.WeaponId).transform;
                weaponRoot.SetParent(slotRoot, false);
                ApplyPose(weaponRoot, attachment.WeaponModelPose);

                ResourcePreviewInfo resource = FindResource(resources, usage: "weaponModel", resourceKey: resourceKey);
                CreateModelWrapper("Model_" + SanitizeName(attachment.WeaponLabel), resource, weaponRoot);
                AddWeaponTraceLocators(package, attachment, weaponRoot);
            }
        }

        private static void AddWeaponTraceLocators(
            CharacterImportedPackage package,
            CharacterWeaponAttachmentRuntimeBinding attachment,
            Transform weaponRoot)
        {
            if (package == null
                || package.Geometry == null
                || attachment == null
                || weaponRoot == null)
            {
                return;
            }

            CharacterWeaponTraceRuntimeBinding[] traces = package.Geometry.WeaponTraces;
            for (int i = 0; traces != null && i < traces.Length; i++)
            {
                CharacterWeaponTraceRuntimeBinding trace = traces[i];
                if (!TraceBelongsToAttachment(trace, attachment))
                    continue;

                AddWeaponTraceLocator(
                    weaponRoot,
                    trace.StartLocatorPath,
                    attachment,
                    trace.StartPose);
                AddWeaponTraceLocator(
                    weaponRoot,
                    trace.EndLocatorPath,
                    attachment,
                    trace.EndPose);
            }
        }

        private static bool TraceBelongsToAttachment(
            CharacterWeaponTraceRuntimeBinding trace,
            CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            if (trace == null || attachment == null)
                return false;
            if ((attachment.TraceId > 0)
                && trace.TraceId != attachment.TraceId)
            {
                return false;
            }
            if ((trace.WeaponId > 0)
                && trace.WeaponId != attachment.WeaponId)
            {
                return false;
            }
            return (trace.EquipSlot <= 0)
                || trace.EquipSlot == attachment.EquipSlot;
        }

        private static void AddWeaponTraceLocator(
            Transform weaponRoot,
            string locatorPath,
            CharacterWeaponAttachmentRuntimeBinding attachment,
            CharacterRuntimePose tracePose)
        {
            Transform locator = EnsureNestedTransform(weaponRoot, locatorPath);
            if (locator == null)
                return;

            ResolveWeaponLocalTracePose(
                attachment,
                tracePose,
                out Vector3 localPosition,
                out Quaternion localRotation,
                out Vector3 localScale);
            locator.localPosition = localPosition;
            locator.localRotation = localRotation;
            locator.localScale = localScale;
        }

        private static Transform EnsureNestedTransform(Transform root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
                return null;

            string normalized = NormalizeProjectPath(path).Trim('/');
            if (string.IsNullOrEmpty(normalized))
                return null;

            Transform current = root;
            string[] segments = normalized.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (string.IsNullOrWhiteSpace(segment))
                    continue;

                Transform child = current.Find(segment);
                if (child == null)
                {
                    child = new GameObject(segment).transform;
                    child.SetParent(current, false);
                }

                current = child;
            }

            return current == root ? null : current;
        }

        private static void ResolveWeaponLocalTracePose(
            CharacterWeaponAttachmentRuntimeBinding attachment,
            CharacterRuntimePose tracePose,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            localPosition = ToVector3(tracePose.Position, Vector3.zero);
            localRotation = ToQuaternion(tracePose.Rotation);
            localScale = ToVector3(tracePose.Scale, Vector3.one);
            if (attachment == null || !IsAttachmentSocketPose(attachment, tracePose))
                return;

            Vector3 gripPosition = ToVector3(attachment.LocalGripPose.Position, Vector3.zero);
            Quaternion gripRotation = ToQuaternion(attachment.LocalGripPose.Rotation);
            Vector3 gripScale = ToVector3(attachment.LocalGripPose.Scale, Vector3.one);
            Matrix4x4 gripMatrix = Matrix4x4.TRS(gripPosition, gripRotation, gripScale);
            Matrix4x4 inverseGrip = gripMatrix.inverse;
            localPosition = inverseGrip.MultiplyPoint3x4(localPosition);
            localRotation = Quaternion.Inverse(gripRotation) * localRotation;
            localScale = DivideScale(localScale, gripScale);
        }

        private static bool IsAttachmentSocketPose(
            CharacterWeaponAttachmentRuntimeBinding attachment,
            CharacterRuntimePose tracePose)
        {
            if (!string.Equals(tracePose.ParentKind, "Socket", StringComparison.Ordinal)
                && !string.Equals(tracePose.ParentKind, "Bone", StringComparison.Ordinal))
            {
                return false;
            }

            string parent = tracePose.ParentPath ?? string.Empty;
            return string.Equals(parent, attachment.AttachSocketLabel, StringComparison.Ordinal)
                || string.Equals(parent, attachment.EquipmentSlotLabel, StringComparison.Ordinal);
        }

        private static Vector3 DivideScale(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Math.Abs(divisor.x) > 0.00001f ? value.x / divisor.x : value.x,
                Math.Abs(divisor.y) > 0.00001f ? value.y / divisor.y : value.y,
                Math.Abs(divisor.z) > 0.00001f ? value.z / divisor.z : value.z);
        }

        private static string GetAttachmentKey(CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return (attachment?.EquipmentSlotLabel ?? string.Empty) + "::" + (attachment?.WeaponLabel ?? string.Empty);
        }

        private static string GetCharacterPrefabResourceId(CharacterImportedPackage package)
        {
            return CharacterRuntimePrefabResourceCatalog.GetCharacterPrefabResourceId(package?.PackageId);
        }

        private static string GetWeaponPrefabResourceId(CharacterImportedPackage package, CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return CharacterRuntimePrefabResourceCatalog.GetWeaponPrefabResourceId(
                package?.PackageId,
                attachment?.EquipmentSlotLabel,
                attachment?.WeaponLabel);
        }

        private static string GetDefaultWeaponResourceId(CharacterImportedPackage package, CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourceId(package, attachment);
        }

        private static string GetWeaponVisualResourceKey(CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return CharacterRuntimePrefabResourceCatalog.GetWeaponVisualResourceKey(attachment);
        }

        private static string GetDefaultWeaponResourcePackageId(CharacterImportedPackage package, CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourcePackageId(package, attachment);
        }

        private static string GetDefaultWeaponResourceVariant(CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourceVariant(attachment);
        }

        private static bool HasRuntimeWeaponResource(CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return CharacterRuntimePrefabResourceCatalog.HasRuntimeWeaponResource(attachment);
        }

        private static void DeleteGeneratedWeaponPrefabIfExists(
            string importedPackageRoot,
            CharacterImportedPackage package,
            CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            if (package == null || attachment == null)
                return;

            string weaponPrefabPath = NormalizeProjectPath(importedPackageRoot) + "/prefabs/weapons/"
                + package.PackageId + "_" + SanitizeName(attachment.EquipmentSlotLabel) + "_" + SanitizeName(attachment.WeaponLabel) + ".prefab";
            DeleteAssetIfExists(weaponPrefabPath);

            string runtimeWeaponPrefabPath = GetRuntimeResourcesAssetPath(importedPackageRoot, package.PackageId, weaponPrefabPath);
            DeleteAssetIfExists(runtimeWeaponPrefabPath);
        }

        private static void DeleteAssetIfExists(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            assetPath = NormalizeProjectPath(assetPath);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
        }

        private static GameObject MirrorPrefabToRuntimeResources(string importedPackageRoot, string packageId, string sourcePrefabPath)
        {
            if (string.IsNullOrWhiteSpace(importedPackageRoot)
                || string.IsNullOrWhiteSpace(packageId)
                || string.IsNullOrWhiteSpace(sourcePrefabPath))
            {
                return null;
            }

            string targetPath = GetRuntimeResourcesAssetPath(importedPackageRoot, packageId, sourcePrefabPath);
            EnsureFolderPath(Path.GetDirectoryName(targetPath)?.Replace('\\', '/'));
            if (File.Exists(targetPath))
            {
                // Update the generated payload in place; its existing .meta owns the runtime asset identity.
                // Unity may still memory-map the previous import on Windows.
                AssetDatabase.ReleaseCachedFileHandles();
                File.Copy(sourcePrefabPath, targetPath, overwrite: true);
                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
            }
            else if (!AssetDatabase.CopyAsset(sourcePrefabPath, targetPath))
            {
                Debug.LogWarning("MxFramework Character Preview: failed to mirror prefab into Resources for runtime loading. source="
                    + sourcePrefabPath + " target=" + targetPath);
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
        }

        private static void MirrorAnimationResourcesToRuntimeResources(string importedPackageRoot, string packageId)
        {
            string registryPath = NormalizeProjectPath(importedPackageRoot) + "/config/animation_clip_registry.json";
            if (!File.Exists(registryPath))
                return;

            var mirroredAssetPaths = new HashSet<string>(StringComparer.Ordinal);
            JObject root = JObject.Parse(File.ReadAllText(registryPath));
            JArray clips = root["clips"] as JArray;
            for (int i = 0; clips != null && i < clips.Count; i++)
            {
                JObject clip = clips[i] as JObject;
                if (clip == null)
                    continue;

                string resourceKey = ReadString(clip, "runtimeResourceKey");
                string assetPath = ReadString(clip["sourceSelection"] as JObject, "unityAssetPath");
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = FindProjectRuntimeCatalogAssetPath(resourceKey);
                assetPath = NormalizeProjectPath(assetPath);
                if (string.IsNullOrWhiteSpace(assetPath) || !mirroredAssetPaths.Add(assetPath))
                    continue;

                string targetPath = GetRuntimeResourcesAssetPath(importedPackageRoot, packageId, assetPath);
                EnsureFolderPath(Path.GetDirectoryName(targetPath)?.Replace('\\', '/'));
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath) != null)
                    AssetDatabase.DeleteAsset(targetPath);
                if (!AssetDatabase.CopyAsset(assetPath, targetPath))
                {
                    Debug.LogWarning("MxFramework Character Preview: failed to mirror animation asset into Resources for runtime loading. source="
                        + assetPath + " target=" + targetPath);
                }
            }
        }

        private static string GetRuntimeResourcesAssetPath(string importedPackageRoot, string packageId, string sourcePrefabPath)
        {
            string relative = GetPackageRelativePath(importedPackageRoot, sourcePrefabPath);
            return importedPackageRoot + "/" + RuntimeResourcesRootName + "/" + UnityResourcesFolderName + "/"
                + RuntimeResourcesAddressRoot + "/" + packageId + "/" + relative;
        }

        private static string GetRuntimeResourcesAddress(string importedPackageRoot, string packageId, string sourcePrefabPath)
        {
            string relative = GetPackageRelativePath(importedPackageRoot, sourcePrefabPath);
            string path = RuntimeResourcesAddressRoot + "/" + packageId + "/" + relative;
            return path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(0, path.Length - ".prefab".Length)
                : path;
        }

        private static string GetPackageRelativePath(string importedPackageRoot, string assetPath)
        {
            string root = NormalizeProjectPath(importedPackageRoot).TrimEnd('/');
            string normalized = NormalizeProjectPath(assetPath);
            string prefix = root + "/";
            return normalized.StartsWith(prefix, StringComparison.Ordinal)
                ? normalized.Substring(prefix.Length)
                : Path.GetFileName(normalized);
        }

        private static GameObject CreateRuntimeResourceBootstrap(
            string importedPackageRoot,
            CharacterImportedPackage package,
            string characterPrefabPath)
        {
            var loader = new GameObject("CharacterRuntimeResourceBootstrap");
            var bootstrap = loader.AddComponent<CharacterRuntimeResourceBootstrap>();
            ConfigureRuntimeResourceBootstrap(bootstrap, importedPackageRoot, package, characterPrefabPath);
            return loader;
        }

        private static void ConfigureRuntimeResourceBootstrap(
            CharacterRuntimeResourceBootstrap bootstrap,
            string importedPackageRoot,
            CharacterImportedPackage package,
            string characterPrefabPath)
        {
            if (bootstrap == null || package == null)
                return;

            string root = NormalizeProjectPath(importedPackageRoot);
            Component debugPanel = EnsureRuntimeAnimationDebugPanel(bootstrap.gameObject);
            Component debugOverlay = debugPanel != null ? EnsureDebugUiOverlayController(bootstrap.gameObject) : null;
            ConfigureRuntimeAnimationDebugPanel(debugPanel, debugOverlay);

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("_catalogId").stringValue = "character.runtime." + package.PackageId;
            serialized.FindProperty("_importedPackageRoot").stringValue = root;
            serialized.FindProperty("_packageId").stringValue = package.PackageId;
            serialized.FindProperty("_characterResourceId").stringValue = GetCharacterPrefabResourceId(package);
            serialized.FindProperty("_characterResourceVariant").stringValue = "default";
            serialized.FindProperty("_loadOnStart").boolValue = true;

            SerializedProperty resources = serialized.FindProperty("_resources");
            List<RuntimeResourceBootstrapEntry> runtimeResources = BuildRuntimeResourceBootstrapEntries(root, package, characterPrefabPath);
            resources.arraySize = runtimeResources.Count;
            for (int i = 0; i < runtimeResources.Count; i++)
            {
                RuntimeResourceBootstrapEntry entry = runtimeResources[i];
                SetSerializedResource(
                    resources.GetArrayElementAtIndex(i),
                    entry.Id,
                    entry.TypeId,
                    entry.ProviderId,
                    entry.Variant,
                    entry.PackageId,
                    entry.Address,
                    entry.Asset);
            }

            string animationSetPath = root + "/config/animation_set_definition.json";
            string animationClipRegistryPath = root + "/config/animation_clip_registry.json";
            string animationResourcePlanPath = root + "/config/animation_resource_plan.json";
            TextAsset animationSet = AssetDatabase.LoadAssetAtPath<TextAsset>(animationSetPath);
            TextAsset animationClipRegistry = AssetDatabase.LoadAssetAtPath<TextAsset>(animationClipRegistryPath);
            serialized.FindProperty("_animationSetDefinitionJson").objectReferenceValue = animationSet;
            serialized.FindProperty("_animationClipRegistryJson").objectReferenceValue = animationClipRegistry;
            serialized.FindProperty("_animationResourcePlanJsonPath").stringValue = File.Exists(animationResourcePlanPath)
                ? animationResourcePlanPath
                : string.Empty;
            serialized.FindProperty("_animationSetDefinitionJsonPath").stringValue = animationSetPath;
            serialized.FindProperty("_animationClipRegistryPath").stringValue = animationClipRegistryPath;
            serialized.FindProperty("_animationSetDefinitionContentHash").stringValue = ComputeFileContentHash(animationSetPath);
            serialized.FindProperty("_animationClipRegistryContentHash").stringValue = ComputeFileContentHash(animationClipRegistryPath);
            serialized.FindProperty("_animationResourcePlanContentHash").stringValue = ComputeFileContentHash(animationResourcePlanPath);
            serialized.FindProperty("_importReportPath").stringValue = package.ImportReport == null ? string.Empty : package.ImportReport.ReportPath;
            serialized.FindProperty("_sourcePackageHash").stringValue = package.ImportReport == null ? string.Empty : package.ImportReport.SourcePackageHash;
            serialized.FindProperty("_generatedConfigHash").stringValue = package.ImportReport == null ? string.Empty : package.ImportReport.GeneratedConfigHash;
            serialized.FindProperty("_geometryBindingHash").stringValue = package.ImportReport == null ? string.Empty : package.ImportReport.GeometryBindingHash;
            serialized.FindProperty("_resourceMappingHash").stringValue = package.ImportReport == null ? string.Empty : package.ImportReport.ResourceMappingHash;
            serialized.FindProperty("_animationSetId").stringValue = ReadFirstAnimationSetId(animationSetPath);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<RuntimeResourceBootstrapEntry> BuildRuntimeResourceBootstrapEntries(
            string importedPackageRoot,
            CharacterImportedPackage package,
            string characterPrefabPath)
        {
            var runtimeResources = new List<RuntimeResourceBootstrapEntry>
            {
                new RuntimeResourceBootstrapEntry(
                    GetCharacterPrefabResourceId(package),
                    ResourceTypeIds.GameObject,
                    ResourcesProvider.Id,
                    "default",
                    package.PackageId,
                    GetRuntimeResourcesAddress(importedPackageRoot, package.PackageId, characterPrefabPath))
            };
            var runtimeResourceIds = new HashSet<string>(StringComparer.Ordinal)
            {
                GetCharacterPrefabResourceId(package)
            };

            for (int i = 0; i < package.Geometry.WeaponAttachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = package.Geometry.WeaponAttachments[i];
                if (string.IsNullOrWhiteSpace(attachment.PreviewResourceKey))
                    continue;
                if (HasRuntimeWeaponResource(attachment))
                {
                    AddMappedRuntimeGameObject(
                        attachment.PreviewResourceKey,
                        runtimeResources,
                        runtimeResourceIds);
                    continue;
                }

                string weaponPrefabPath = importedPackageRoot + "/prefabs/weapons/"
                    + package.PackageId + "_" + SanitizeName(attachment.EquipmentSlotLabel) + "_" + SanitizeName(attachment.WeaponLabel) + ".prefab";
                if (!File.Exists(weaponPrefabPath))
                    continue;

                string weaponResourceId = GetWeaponPrefabResourceId(package, attachment);
                if (!runtimeResourceIds.Add(weaponResourceId))
                    continue;

                runtimeResources.Add(new RuntimeResourceBootstrapEntry(
                    weaponResourceId,
                    ResourceTypeIds.GameObject,
                    ResourcesProvider.Id,
                    "default",
                    package.PackageId,
                    GetRuntimeResourcesAddress(importedPackageRoot, package.PackageId, weaponPrefabPath)));
            }

            AddAnimationRuntimeResources(importedPackageRoot, package, runtimeResources, runtimeResourceIds);
            return runtimeResources;
        }

        private static void AddMappedRuntimeGameObject(
            string resourceKey,
            List<RuntimeResourceBootstrapEntry> runtimeResources,
            HashSet<string> runtimeResourceIds)
        {
            if (string.IsNullOrWhiteSpace(resourceKey)
                || runtimeResourceIds.Contains(resourceKey)
                || !File.Exists(ResourceMappingProfilePath))
            {
                return;
            }

            JObject profile = JObject.Parse(File.ReadAllText(ResourceMappingProfilePath));
            JArray entries = profile["entries"] as JArray;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                JObject key = entry?["resourceKey"] as JObject;
                if (!string.Equals(ReadString(key, "id"), resourceKey, StringComparison.Ordinal)
                    || !string.Equals(ReadString(key, "typeId"), ResourceTypeIds.GameObject, StringComparison.Ordinal))
                {
                    continue;
                }

                string assetPath = ReadString(entry["sourceLocator"] as JObject, "cachedPath");
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null)
                {
                    Debug.LogWarning(
                        "MxFramework Character Runtime: mapped GameObject could not be registered for runtime playback. resource="
                        + resourceKey + " assetPath=" + assetPath);
                    return;
                }

                if (!runtimeResourceIds.Add(resourceKey))
                    return;
                runtimeResources.Add(new RuntimeResourceBootstrapEntry(
                    resourceKey,
                    ResourceTypeIds.GameObject,
                    "memory",
                    ReadString(key, "variant"),
                    ReadString(key, "packageId"),
                    resourceKey,
                    asset));
                return;
            }
        }

        private static void ConfigureRuntimeAnimationDebugPanel(
            Component debugPanel,
            Component debugOverlay)
        {
            if (debugPanel == null)
                return;

            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(RuntimeDebugPanelSettingsPath);
            if (panelSettings == null)
                return;

            var serialized = new SerializedObject(debugPanel);
            serialized.FindProperty("_panelSettings").objectReferenceValue = panelSettings;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (debugOverlay == null)
                return;

            var overlaySerialized = new SerializedObject(debugOverlay);
            SerializedProperty panelSettingsProperty = overlaySerialized.FindProperty("_panelSettings");
            if (panelSettingsProperty != null)
                panelSettingsProperty.objectReferenceValue = panelSettings;
            overlaySerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureLocomotionCalibrationRunner(
            CharacterLocomotionCalibrationRunner runner,
            CharacterRuntimeResourceBootstrap bootstrap)
        {
            if (runner == null)
                return;

            var serialized = new SerializedObject(runner);
            serialized.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            serialized.FindProperty("_loadOnStart").boolValue = true;
            serialized.FindProperty("_keepInputMotionEnabled").boolValue = true;
            SerializedProperty panelSettingsProperty = serialized.FindProperty("_panelSettings");
            if (panelSettingsProperty != null)
                panelSettingsProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<PanelSettings>(RuntimeDebugPanelSettingsPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CharacterLocomotionCalibrationRunner EnsureLocomotionCalibrationRunner(
            GameObject owner,
            CharacterRuntimeResourceBootstrap bootstrap)
        {
            if (owner == null)
                return null;

            CharacterLocomotionCalibrationRunner runner = owner.GetComponent<CharacterLocomotionCalibrationRunner>();
            if (runner == null)
                runner = owner.AddComponent<CharacterLocomotionCalibrationRunner>();

            ConfigureLocomotionCalibrationRunner(runner, bootstrap);
            EditorUtility.SetDirty(runner);
            return runner;
        }

        private static Component EnsureDebugUiOverlayController(GameObject owner)
        {
            if (owner == null)
                return null;

            Type overlayType = Type.GetType("MxFramework.DebugUI.Toolkit.DebugUiOverlayController, MxFramework.DebugUI.Toolkit");
            if (overlayType == null)
                return null;

            Component existing = owner.GetComponent(overlayType);
            return existing != null ? existing : owner.AddComponent(overlayType);
        }

        private static Component EnsureRuntimeAnimationDebugPanel(GameObject owner)
        {
            if (owner == null)
                return null;

            Type panelType = Type.GetType(
                "MxFramework.CharacterRuntimeSpawn.DebugUI.Unity.CharacterRuntimeAnimationDebugPanel, MxFramework.Character.RuntimeSpawn.DebugUI.Unity");
            if (panelType == null)
                return null;

            Component existing = owner.GetComponent(panelType);
            return existing != null ? existing : owner.AddComponent(panelType);
        }

        private static void AddAnimationRuntimeResources(
            string importedPackageRoot,
            CharacterImportedPackage package,
            List<RuntimeResourceBootstrapEntry> runtimeResources,
            HashSet<string> runtimeResourceIds)
        {
            string registryPath = importedPackageRoot + "/config/animation_clip_registry.json";
            if (!File.Exists(registryPath))
                return;

            JObject root = JObject.Parse(File.ReadAllText(registryPath));
            JArray clips = root["clips"] as JArray;
            for (int i = 0; clips != null && i < clips.Count; i++)
            {
                JObject clip = clips[i] as JObject;
                if (clip == null)
                    continue;

                string resourceKey = ReadString(clip, "runtimeResourceKey");
                if (string.IsNullOrWhiteSpace(resourceKey) || !runtimeResourceIds.Add(resourceKey))
                    continue;

                string sourceClipName = ReadString(clip, "sourceClipName");
                string assetPath = ReadString(clip["sourceSelection"] as JObject, "unityAssetPath");
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = FindProjectRuntimeCatalogAssetPath(resourceKey);

                AnimationClip animationClip = LoadAnimationClipAsset(assetPath, sourceClipName);
                if (animationClip == null)
                {
                    Debug.LogWarning("MxFramework Character Runtime: animation clip could not be registered for runtime playback. resource="
                        + resourceKey + " sourceClip=" + sourceClipName + " assetPath=" + assetPath);
                    continue;
                }

                runtimeResources.Add(new RuntimeResourceBootstrapEntry(
                    resourceKey,
                    ResourceTypeIds.AnimationClip,
                    "memory",
                    string.Empty,
                    ResolveRuntimeResourcePackageId(
                        package != null ? package.RuntimeResourceCatalog : null,
                        resourceKey,
                        ResourceTypeIds.AnimationClip,
                        string.Empty,
                        package != null ? package.PackageId : string.Empty),
                    resourceKey,
                    animationClip));
            }
        }

        private static string ResolveRuntimeResourcePackageId(
            ResourceCatalog catalog,
            string resourceId,
            string typeId,
            string variant,
            string fallbackPackageId)
        {
            for (int i = 0; catalog != null && i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null
                    || !string.Equals(entry.Id, resourceId, StringComparison.Ordinal)
                    || !string.Equals(entry.TypeId, typeId, StringComparison.Ordinal)
                    || !string.Equals(entry.Variant, variant, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.PackageId))
                    return entry.PackageId;
                if (!string.IsNullOrWhiteSpace(catalog.PackageId))
                    return catalog.PackageId;
                break;
            }

            return fallbackPackageId ?? string.Empty;
        }

        private static AnimationClip LoadAnimationClipAsset(string assetPath, string sourceClipName)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            AnimationClip direct = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (direct != null && (string.IsNullOrWhiteSpace(sourceClipName) || string.Equals(direct.name, sourceClipName, StringComparison.Ordinal)))
                return direct;
            if (direct != null && !assetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) && !assetPath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                return direct;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            AnimationClip first = null;
            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is AnimationClip clip))
                    continue;
                if (first == null)
                    first = clip;
                if (!string.IsNullOrWhiteSpace(sourceClipName) && string.Equals(clip.name, sourceClipName, StringComparison.Ordinal))
                    return clip;
            }

            return first;
        }

        private static string FindProjectRuntimeCatalogAssetPath(string resourceKey)
        {
            string root = "Assets/Config/MxFramework/ResourceCatalogs";
            if (string.IsNullOrWhiteSpace(resourceKey) || !Directory.Exists(root))
                return string.Empty;

            foreach (string path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
            {
                JObject catalog = JObject.Parse(File.ReadAllText(path));
                JArray entries = catalog["entries"] as JArray;
                for (int i = 0; entries != null && i < entries.Count; i++)
                {
                    JObject entry = entries[i] as JObject;
                    if (!string.Equals(ReadString(entry, "id"), resourceKey, StringComparison.Ordinal))
                        continue;

                    string assetPath = ReadString(entry["providerData"] as JObject, "assetPath");
                    if (!string.IsNullOrWhiteSpace(assetPath))
                        return assetPath;
                }
            }

            return string.Empty;
        }

        private static string ReadFirstAnimationSetId(string animationSetPath)
        {
            if (string.IsNullOrWhiteSpace(animationSetPath) || !File.Exists(animationSetPath))
                return string.Empty;

            JObject root = JObject.Parse(File.ReadAllText(animationSetPath));
            JArray sets = root["sets"] as JArray;
            JObject first = sets != null && sets.Count > 0 ? sets[0] as JObject : null;
            return ReadString(first, "setId");
        }

        private static string ComputeFileContentHash(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return string.Empty;

            using (var sha = SHA256.Create())
            {
                byte[] bytes = File.ReadAllBytes(path);
                byte[] hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static void EnsureFolderPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
                return;

            string[] segments = path.Split('/');
            if (segments.Length == 0 || segments[0] != "Assets")
                return;

            string current = "Assets";
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static void SetSerializedResource(
            SerializedProperty property,
            string id,
            string typeId,
            string providerId,
            string variant,
            string packageId,
            string address,
            UnityEngine.Object asset)
        {
            property.FindPropertyRelative("_id").stringValue = id;
            property.FindPropertyRelative("_typeId").stringValue = typeId;
            property.FindPropertyRelative("_providerId").stringValue = providerId;
            property.FindPropertyRelative("_variant").stringValue = variant;
            property.FindPropertyRelative("_packageId").stringValue = packageId;
            property.FindPropertyRelative("_address").stringValue = address;
            property.FindPropertyRelative("_asset").objectReferenceValue = asset;
        }

        private static Dictionary<int, Transform> CreateSockets(
            CharacterImportedPackage package,
            Transform socketsRoot,
            Transform bodyModel,
            List<TransformBoneBindingInfo> boneBindings,
            ISet<int> resolvedSocketIds)
        {
            var sockets = new Dictionary<int, Transform>();
            for (int i = 0; i < package.Geometry.Sockets.Length; i++)
            {
                CharacterSocketRuntimeBinding socket = package.Geometry.Sockets[i];
                var marker = new GameObject("Socket_" + socket.SocketLabel).transform;
                marker.SetParent(socketsRoot, false);
                ApplyPose(marker, socket.LocalPose);
                AddMarker(marker.gameObject, new Color(0.08f, 0.55f, 0.55f, 0.55f), 0.05f);

                bool isModelRootBinding = string.Equals(
                    socket.LocalPose.ParentKind,
                    "ModelRoot",
                    StringComparison.OrdinalIgnoreCase);
                Transform bindingParent = isModelRootBinding
                    ? bodyModel
                    : FindBodyChild(bodyModel, socket.BonePath, socket.LocalPose.ParentPath);
                if (bindingParent == null
                    && !isModelRootBinding
                    && !string.IsNullOrWhiteSpace(socket.LocatorPath))
                {
                    bindingParent = FindBodyChild(bodyModel, socket.LocatorPath, socket.LocatorPath);
                }

                if (bindingParent != null)
                {
                    ApplyBoneRelativePose(marker, bindingParent, socket.LocalPose);
                    boneBindings.Add(TransformBoneBindingInfo.FromWorld(
                        "socket:" + socket.SocketId,
                        socket.BonePath,
                        marker,
                        bindingParent));
                    resolvedSocketIds?.Add(socket.SocketId);
                }
                else
                {
                    Debug.LogWarning("MxFramework Character Preview: socket binding parent was not found; socket will not follow animation. socket="
                        + socket.SocketId
                        + " parentKind=" + socket.LocalPose.ParentKind
                        + " bonePath=" + socket.BonePath
                        + " locatorPath=" + socket.LocatorPath);
                }

                sockets[socket.SocketId] = marker;
            }

            return sockets;
        }

        private static void AddAbstractBoneRuntimeMap(
            GameObject rootObject,
            CharacterImportedPackage package,
            IReadOnlyDictionary<int, Transform> sockets,
            ISet<int> resolvedSocketIds)
        {
            var bindings = new List<CharacterAbstractBoneRuntimeBinding>();
            CharacterSocketRuntimeBinding[] source = package?.Geometry?.Sockets
                ?? Array.Empty<CharacterSocketRuntimeBinding>();
            for (int i = 0; i < source.Length; i++)
            {
                CharacterSocketRuntimeBinding socket = source[i];
                if (socket == null || string.IsNullOrWhiteSpace(socket.AbstractBoneId))
                    continue;
                if (resolvedSocketIds != null
                    && resolvedSocketIds.Contains(socket.SocketId)
                    && sockets != null
                    && sockets.TryGetValue(socket.SocketId, out Transform target)
                    && target != null)
                    bindings.Add(new CharacterAbstractBoneRuntimeBinding(socket.AbstractBoneId, socket.SocketId, target));
            }

            var map = rootObject.AddComponent<CharacterAbstractBoneRuntimeMap>();
            map.Configure(bindings.ToArray());
            EditorUtility.SetDirty(map);
        }

        private static void CollectAbstractBoneStatus(
            GameObject prefab,
            GameObject runtimePrefab,
            CharacterImportedPackage package,
            CharacterImportedPackagePrefabBuildResult result)
        {
            var issues = new List<string>();
            CharacterSocketRuntimeBinding[] sockets = package?.Geometry?.Sockets
                ?? Array.Empty<CharacterSocketRuntimeBinding>();
            int declared = 0;
            for (int i = 0; i < sockets.Length; i++)
                if (sockets[i] != null && !string.IsNullOrWhiteSpace(sockets[i].AbstractBoneId))
                    declared++;

            CharacterAbstractBoneRuntimeMap[] maps = prefab != null
                ? prefab.GetComponents<CharacterAbstractBoneRuntimeMap>()
                : Array.Empty<CharacterAbstractBoneRuntimeMap>();
            CharacterAbstractBoneRuntimeMap map = maps.Length == 1 ? maps[0] : null;
            if (map == null)
                issues.Add("Abstract bone map is missing or duplicated on generated prefab.");

            CharacterAbstractBoneRuntimeMap[] runtimeMaps = runtimePrefab != null
                ? runtimePrefab.GetComponents<CharacterAbstractBoneRuntimeMap>()
                : Array.Empty<CharacterAbstractBoneRuntimeMap>();
            CharacterAbstractBoneRuntimeMap runtimeMap = runtimeMaps.Length == 1 ? runtimeMaps[0] : null;
            if (runtimeMap == null)
                issues.Add("Abstract bone map is missing or duplicated on generated runtime prefab.");

            int resolved = 0;
            for (int i = 0; i < sockets.Length; i++)
            {
                CharacterSocketRuntimeBinding socket = sockets[i];
                if (socket == null || string.IsNullOrWhiteSpace(socket.AbstractBoneId))
                    continue;
                if (map != null && map.TryResolve(socket.AbstractBoneId, out Transform target) && target != null)
                    resolved++;
                else
                    issues.Add("Abstract bone socket was not resolved. package=" + package?.PackageId
                        + " socket=" + socket.SocketId + " abstractBoneId=" + socket.AbstractBoneId
                        + " bonePath=" + socket.BonePath + " locatorPath=" + socket.LocatorPath);
                if (runtimeMap == null
                    || !runtimeMap.TryResolve(socket.AbstractBoneId, out Transform runtimeTarget)
                    || runtimeTarget == null)
                    issues.Add("Runtime prefab abstract bone socket was not resolved. package=" + package?.PackageId
                        + " socket=" + socket.SocketId + " abstractBoneId=" + socket.AbstractBoneId
                        + " bonePath=" + socket.BonePath + " locatorPath=" + socket.LocatorPath);
            }
            string[] requiredIds = result.AbstractBoneRequiredIds ?? Array.Empty<string>();
            for (int i = 0; i < requiredIds.Length; i++)
            {
                string requiredId = requiredIds[i];
                if (map == null || !map.TryResolve(requiredId, out Transform requiredTarget) || requiredTarget == null)
                    issues.Add("Required abstract bone was not resolved. package=" + package?.PackageId + " abstractBoneId=" + requiredId);
                if (runtimeMap == null || !runtimeMap.TryResolve(requiredId, out Transform runtimeRequiredTarget) || runtimeRequiredTarget == null)
                    issues.Add("Runtime prefab required abstract bone was not resolved. package=" + package?.PackageId + " abstractBoneId=" + requiredId);
            }

            result.AbstractBoneDeclaredCount = declared;
            result.AbstractBoneResolvedCount = resolved;
            result.AbstractBoneIssueCount = issues.Count;
            result.AbstractBoneIssues = issues.ToArray();
            result.AbstractBoneStatus = issues.Count == 0 ? "Ready" : "Failed";
        }

        private static void CreateBodyPartContactPointMarkers(
            CharacterImportedPackage package,
            Transform socketsRoot,
            IReadOnlyDictionary<int, Transform> sockets)
        {
            CharacterBodyPartContactPointConfig[] contactPoints =
                package?.Configs?.BodyPartContactPoints ?? Array.Empty<CharacterBodyPartContactPointConfig>();
            for (int i = 0; i < contactPoints.Length; i++)
            {
                CharacterBodyPartContactPointConfig contactPoint = contactPoints[i];
                Transform parent = socketsRoot;
                if (contactPoint.MotionMode == CharacterBodyPartContactMotionMode.BakedSocket
                    && contactPoint.MotionSocketId > 0)
                {
                    if (sockets != null && sockets.TryGetValue(contactPoint.MotionSocketId, out Transform socket))
                    {
                        parent = socket;
                    }
                    else
                    {
                        Debug.LogWarning("MxFramework Character Preview: contact point socket was not found; marker uses raw local offset. contactPoint="
                            + contactPoint.StableId + " socket=" + contactPoint.MotionSocketId);
                    }
                }

                Transform marker = new GameObject(CreateContactPointMarkerName(contactPoint)).transform;
                marker.SetParent(parent, false);
                marker.localPosition = new Vector3(
                    contactPoint.LocalOffsetXMillimeters / 1000f,
                    contactPoint.LocalOffsetYMillimeters / 1000f,
                    contactPoint.LocalOffsetZMillimeters / 1000f);
                marker.localRotation = Quaternion.identity;
                marker.localScale = Vector3.one;
            }
        }

        private static string CreateContactPointMarkerName(CharacterBodyPartContactPointConfig contactPoint)
        {
            string source = contactPoint == null ? string.Empty : contactPoint.StableId;
            if (string.IsNullOrWhiteSpace(source) && contactPoint != null && contactPoint.ContactPointId > 0)
                source = "id." + contactPoint.ContactPointId;
            if (string.IsNullOrWhiteSpace(source))
                source = "unnamed";

            var builder = new StringBuilder(source.Length + 13);
            builder.Append("ContactPoint_");
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' ? c : '_');
            }

            return builder.ToString();
        }

        private static void CreateColliders(
            CharacterImportedPackage package,
            Transform collidersRoot,
            Transform bodyModel,
            List<TransformBoneBindingInfo> boneBindings)
        {
            for (int i = 0; i < package.Geometry.BodyColliders.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding binding = package.Geometry.BodyColliders[i];
                var node = new GameObject("Collider_" + binding.ColliderId);
                node.transform.SetParent(collidersRoot, false);
                ApplyPose(node.transform, binding.LocalPose);

                Transform bone = FindBodyChild(bodyModel, binding.LocalPose.ParentPath, binding.PartLabel);
                if (bone != null)
                {
                    ApplyBoneRelativePose(node.transform, bone, binding.LocalPose);
                    boneBindings.Add(TransformBoneBindingInfo.FromWorld(
                        "collider:" + binding.ColliderId,
                        binding.LocalPose.ParentPath,
                        node.transform,
                        bone));
                }
                else
                {
                    Debug.LogWarning("MxFramework Character Preview: collider bone was not found; collider will not follow animation. collider="
                        + binding.ColliderId + " parentPath=" + binding.LocalPose.ParentPath + " part=" + binding.PartId);
                }
            }
        }

        private static Transform CreateModelWrapper(string name, ResourcePreviewInfo resource, Transform parent)
        {
            var wrapper = new GameObject(name).transform;
            wrapper.SetParent(parent, false);
            if (resource != null && !resource.WrapperPoseBakedIntoAsset)
                ApplyPose(wrapper, resource.WrapperPose);

            GameObject instance = InstantiateAsset(resource);
            if (instance == null)
            {
                Debug.LogWarning("MxFramework Character Preview: using placeholder model for " + name + ". " + FormatResourceDiagnostic(resource));
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                instance.name = "MissingModelPlaceholder_" + SanitizeName(resource != null ? resource.Key : name);
                instance.transform.localScale = new Vector3(0.35f, 1f, 0.35f);
                UnityEngine.Object.DestroyImmediate(instance.GetComponent<Collider>());
                AssignMaterial(instance, new Color(0.82f, 0.78f, 0.16f, 0.55f));
            }

            Vector3 importedLocalScale = instance.transform.localScale;
            if (resource == null || !resource.WrapperPoseBakedIntoAsset)
                ApplyUnityImportedModelScaleCompensation(wrapper, resource, importedLocalScale);
            instance.transform.SetParent(wrapper, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = IsUsableScale(importedLocalScale) ? importedLocalScale : Vector3.one;
            return wrapper;
        }

        private static void ApplyUnityImportedModelScaleCompensation(
            Transform wrapper,
            ResourcePreviewInfo resource,
            Vector3 importedLocalScale)
        {
            if (wrapper == null || resource == null)
                return;

            Vector3 wrapperScale = wrapper.localScale;
            if (!HasSubUnitWrapperScale(wrapperScale) || !HasLargeImportedRootScale(importedLocalScale))
                return;

            Vector3 compensated = new Vector3(
                wrapperScale.x * importedLocalScale.x,
                wrapperScale.y * importedLocalScale.y,
                wrapperScale.z * importedLocalScale.z);
            if (IsUsableScale(compensated))
                wrapper.localScale = compensated;
        }

        private static bool HasSubUnitWrapperScale(Vector3 scale)
        {
            return IsFinite(scale.x)
                && IsFinite(scale.y)
                && IsFinite(scale.z)
                && Mathf.Abs(scale.x) > 0.000001f
                && Mathf.Abs(scale.y) > 0.000001f
                && Mathf.Abs(scale.z) > 0.000001f
                && Mathf.Abs(scale.x) <= 0.1f
                && Mathf.Abs(scale.y) <= 0.1f
                && Mathf.Abs(scale.z) <= 0.1f;
        }

        private static bool HasLargeImportedRootScale(Vector3 scale)
        {
            return IsFinite(scale.x)
                && IsFinite(scale.y)
                && IsFinite(scale.z)
                && Mathf.Abs(scale.x) >= 10f
                && Mathf.Abs(scale.y) >= 10f
                && Mathf.Abs(scale.z) >= 10f;
        }

        private static bool IsUsableScale(Vector3 scale)
        {
            return IsFinite(scale.x)
                && IsFinite(scale.y)
                && IsFinite(scale.z)
                && Mathf.Abs(scale.x) > 0.000001f
                && Mathf.Abs(scale.y) > 0.000001f
                && Mathf.Abs(scale.z) > 0.000001f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void CreateResourceDiagnostics(Dictionary<string, ResourcePreviewInfo> resources, Transform parent)
        {
            if (resources == null || parent == null)
                return;

            foreach (ResourcePreviewInfo resource in resources.Values)
            {
                if (resource == null || resource.IsImportReady || string.IsNullOrWhiteSpace(resource.Diagnostic))
                    continue;

                var item = new GameObject("Resource_" + SanitizeName(resource.Key)).transform;
                item.SetParent(parent, false);
                item.gameObject.SetActive(false);

                var status = new GameObject("status_" + SanitizeName(string.IsNullOrWhiteSpace(resource.ImportStatus) ? "unknown" : resource.ImportStatus)).transform;
                status.SetParent(item, false);

                var path = new GameObject("asset_" + SanitizeName(string.IsNullOrWhiteSpace(resource.AssetPath) ? "unresolved" : resource.AssetPath)).transform;
                path.SetParent(item, false);
            }
        }

        private static GameObject InstantiateAsset(ResourcePreviewInfo resource)
        {
            if (resource == null || string.IsNullOrWhiteSpace(resource.AssetPath))
                return null;

            if (!resource.IsImportReady)
            {
                Debug.LogWarning("MxFramework Character Preview: model asset is not ready for prefab instantiation. "
                    + FormatResourceDiagnostic(resource));
                return null;
            }

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(resource.AssetPath);
            if (asset == null)
            {
                Debug.LogWarning("MxFramework Character Preview: model asset could not be loaded. "
                    + FormatResourceDiagnostic(resource));
                return null;
            }

            bool preserveNestedPrefab = ShouldPreserveNestedPrefabInstance(resource.AssetPath);
            if (preserveNestedPrefab)
            {
                GameObject prefabInstance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                if (prefabInstance != null)
                    return prefabInstance;
            }

            GameObject instance = UnityEngine.Object.Instantiate(asset);
            if (instance == null)
                return null;

            instance.name = asset.name;
            UnpackPrefabInstanceIfNeeded(instance);
            return instance;
        }

        private static bool ShouldPreserveNestedPrefabInstance(string assetPath)
        {
            return string.Equals(Path.GetExtension(assetPath), ".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static void UnpackPrefabInstanceIfNeeded(GameObject instance)
        {
            if (instance == null || !PrefabUtility.IsPartOfPrefabInstance(instance))
                return;

            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(instance);
            PrefabUtility.UnpackPrefabInstance(
                root != null ? root : instance,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
        }

        private static Transform FindBodyChild(Transform root, string path, string fallbackKey = "")
        {
            if (root == null)
                return null;

            string normalized = (path ?? string.Empty).StartsWith("bone.", StringComparison.Ordinal)
                ? path.Substring("bone.".Length)
                : path ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                Transform direct = root.Find(normalized);
                if (direct != null)
                    return direct;
            }

            string last = normalized.Contains("/") ? normalized.Substring(normalized.LastIndexOf("/", StringComparison.Ordinal) + 1) : normalized;
            foreach (string candidate in GetBoneNameCandidates(last, fallbackKey))
            {
                Transform match = FindChildByName(root, candidate);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChildByName(root.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static IEnumerable<string> GetBoneNameCandidates(string name, string fallbackKey)
        {
            if (!string.IsNullOrWhiteSpace(name))
                yield return name;

            string key = (name + " " + fallbackKey).ToLowerInvariant();
            if (key.Contains("righthand") || key.Contains("right_hand") || key.Contains("hand.r"))
            {
                yield return "hand.R";
                yield return "RightHand";
                yield return "right_hand";
            }
            if (key.Contains("lefthand") || key.Contains("left_hand") || key.Contains("hand.l"))
            {
                yield return "hand.L";
                yield return "LeftHand";
                yield return "left_hand";
            }
            if (key.Contains("upper_arm.r") || key.Contains("upper_arm_r") || key.Contains("rightarm"))
            {
                yield return "upper_arm.R";
                yield return "RightArm";
                yield return "right_arm";
                yield return "arm_stretch.r";
            }
            if (key.Contains("upper_arm.l") || key.Contains("upper_arm_l") || key.Contains("leftarm"))
            {
                yield return "upper_arm.L";
                yield return "LeftArm";
                yield return "left_arm";
                yield return "arm_stretch.l";
            }
            if (key.Contains("forearm.r") || key.Contains("forearm_r") || key.Contains("rightforearm"))
            {
                yield return "forearm.R";
                yield return "RightForeArm";
                yield return "RightForearm";
                yield return "right_forearm";
                yield return "forearm_stretch.r";
            }
            if (key.Contains("forearm.l") || key.Contains("forearm_l") || key.Contains("leftforearm"))
            {
                yield return "forearm.L";
                yield return "LeftForeArm";
                yield return "LeftForearm";
                yield return "left_forearm";
                yield return "forearm_stretch.l";
            }
            if (key.Contains("thigh.r") || key.Contains("thigh_r") || key.Contains("rightupleg"))
            {
                yield return "thigh.R";
                yield return "RightUpLeg";
                yield return "right_up_leg";
                yield return "thigh_stretch.r";
            }
            if (key.Contains("thigh.l") || key.Contains("thigh_l") || key.Contains("leftupleg"))
            {
                yield return "thigh.L";
                yield return "LeftUpLeg";
                yield return "left_up_leg";
                yield return "thigh_stretch.l";
            }
            if (key.Contains("shin.r") || key.Contains("shin_r") || key.Contains("rightleg"))
            {
                yield return "shin.R";
                yield return "RightLeg";
                yield return "right_leg";
                yield return "lower_leg.r";
                yield return "leg_stretch.r";
            }
            if (key.Contains("shin.l") || key.Contains("shin_l") || key.Contains("leftleg"))
            {
                yield return "shin.L";
                yield return "LeftLeg";
                yield return "left_leg";
                yield return "lower_leg.l";
                yield return "leg_stretch.l";
            }
            if (key.Contains("foot.r") || key.Contains("foot_r") || key.Contains("rightfoot"))
            {
                yield return "foot.R";
                yield return "RightFoot";
                yield return "right_foot";
            }
            if (key.Contains("foot.l") || key.Contains("foot_l") || key.Contains("leftfoot"))
            {
                yield return "foot.L";
                yield return "LeftFoot";
                yield return "left_foot";
            }
            if (key.Contains("toe.r") || key.Contains("toe_r") || key.Contains("righttoe"))
            {
                yield return "toe.R";
                yield return "RightToe";
                yield return "RightToes";
                yield return "right_toe";
            }
            if (key.Contains("toe.l") || key.Contains("toe_l") || key.Contains("lefttoe"))
            {
                yield return "toe.L";
                yield return "LeftToe";
                yield return "LeftToes";
                yield return "left_toe";
            }
            if (key.Contains("head"))
            {
                yield return "spine.006_end";
                yield return "spine.006";
                yield return "Head";
                yield return "head.x";
            }
            if (key.Contains("chest"))
            {
                yield return "spine.004";
                yield return "spine.003";
                yield return "spine.002";
                yield return "spine_03.x";
                yield return "spine_02.x";
            }
            if (key.Contains("spine") || key.Contains("torso"))
            {
                yield return "spine.003";
                yield return "spine.002";
                yield return "spine";
                yield return "spine_02.x";
                yield return "spine_01.x";
            }
        }

        private static void ApplyBoneRelativePose(Transform target, Transform bone, CharacterRuntimePose pose)
        {
            target.position = bone.position + (bone.rotation * ToVector3(pose.Position, Vector3.zero));
            target.rotation = bone.rotation * ToQuaternion(pose.Rotation);
            target.localScale = ToVector3(pose.Scale, Vector3.one);
        }

        private static Dictionary<string, ResourcePreviewInfo> ReadResourcePreviewInfos(CharacterImportedPackage package, string importedPackageRoot)
        {
            Dictionary<string, CharacterRuntimePose> wrapperPoses = ReadResourceMappingWrapperPoses(importedPackageRoot);
            var result = new Dictionary<string, ResourcePreviewInfo>(StringComparer.Ordinal);
            AddImportedCatalogPreviewInfos(package?.UnityResourceCatalog, wrapperPoses, result);
            AddGlobalProfilePreviewInfos(result);
            return result;
        }

        private static Dictionary<string, CharacterRuntimePose> ReadResourceMappingWrapperPoses(string importedPackageRoot)
        {
            var result = new Dictionary<string, CharacterRuntimePose>(StringComparer.Ordinal);
            string mappingPath = importedPackageRoot + "/config/resource_catalog_mapping.json";
            if (!File.Exists(mappingPath))
                return result;

            JObject root = JObject.Parse(File.ReadAllText(mappingPath));
            JArray entries = root["entries"] as JArray;
            if (entries == null)
                return result;

            for (int i = 0; i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                if (entry == null)
                    continue;

                string key = ReadString(entry, "packageResourceKey");
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                result[key] = ParsePose(entry["modelWrapperPose"] as JObject);
            }

            return result;
        }
    }
}
