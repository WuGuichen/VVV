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
        private static CharacterRuntimePose ParsePose(JObject obj)
        {
            if (obj == null)
                return new CharacterRuntimePose(string.Empty, string.Empty, default, default, new CharacterRuntimeVector3(1f, 1f, 1f));

            return new CharacterRuntimePose(
                ReadString(obj, "parentKind"),
                ReadString(obj, "parentPath"),
                ParseVector3(obj["position"] as JObject, Vector3.zero),
                ParseQuaternion(obj["rotation"] as JObject),
                ParseVector3(obj["scale"] as JObject, Vector3.one));
        }

        private static CharacterRuntimeVector3 ParseVector3(JObject obj, Vector3 fallback)
        {
            if (obj == null)
                return new CharacterRuntimeVector3(fallback.x, fallback.y, fallback.z);
            return new CharacterRuntimeVector3(ReadFloat(obj, "x", fallback.x), ReadFloat(obj, "y", fallback.y), ReadFloat(obj, "z", fallback.z));
        }

        private static CharacterRuntimeQuaternion ParseQuaternion(JObject obj)
        {
            if (obj == null)
                return new CharacterRuntimeQuaternion(0f, 0f, 0f, 1f);
            return new CharacterRuntimeQuaternion(ReadFloat(obj, "x", 0f), ReadFloat(obj, "y", 0f), ReadFloat(obj, "z", 0f), ReadFloat(obj, "w", 1f));
        }

        private static void ApplyPose(Transform transform, CharacterRuntimePose pose)
        {
            transform.localPosition = ToVector3(pose.Position, Vector3.zero);
            transform.localRotation = ToQuaternion(pose.Rotation);
            transform.localScale = ToVector3(pose.Scale, Vector3.one);
        }

        private static Vector3 ToVector3(CharacterRuntimeVector3 value, Vector3 fallback)
        {
            Vector3 vector = new Vector3(value.X, value.Y, value.Z);
            return vector == Vector3.zero && fallback == Vector3.one ? fallback : vector;
        }

        private static Quaternion ToQuaternion(CharacterRuntimeQuaternion value)
        {
            Quaternion q = new Quaternion(value.X, value.Y, value.Z, value.W);
            return q == default ? Quaternion.identity : q;
        }

        private static void AddMarker(GameObject node, Color color, float size)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Marker";
            sphere.transform.SetParent(node.transform, false);
            sphere.transform.localScale = Vector3.one * size;
            UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
            AssignMaterial(sphere, color);
        }

        private static void AssignMaterial(GameObject gameObject, Color color)
        {
            Renderer renderer = gameObject.GetComponentInChildren<Renderer>();
            if (renderer == null)
                return;

            Material material = GetOrCreatePreviewMaterial(color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            renderer.sharedMaterial = material;
        }

        private static Material GetOrCreatePreviewMaterial(Color color)
        {
            string materialName = "CharacterPreview_" + ColorUtility.ToHtmlStringRGBA(color);
            string materialPath = string.IsNullOrWhiteSpace(_previewMaterialFolder)
                ? string.Empty
                : _previewMaterialFolder + "/" + materialName + ".mat";
            Material material = !string.IsNullOrWhiteSpace(materialPath)
                ? AssetDatabase.LoadAssetAtPath<Material>(materialPath)
                : null;
            if (material != null)
                return material;

            material = new Material(FindPreviewShader())
            {
                name = materialName
            };

            if (!string.IsNullOrWhiteSpace(materialPath))
                AssetDatabase.CreateAsset(material, materialPath);

            return material;
        }

        private static Shader FindPreviewShader()
        {
            string[] names =
            {
                "WGame/CharacterPaletteLegacy",
                "Universal Render Pipeline/Unlit",
                "Unlit/Color"
            };

            for (int i = 0; i < names.Length; i++)
            {
                Shader shader = Shader.Find(names[i]);
                if (shader != null)
                    return shader;
            }

            throw new InvalidOperationException(
                "Character preview requires WGame/CharacterPaletteLegacy or a supported unlit fallback shader.");
        }

        private static void CreatePreviewCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1.35f, -4.2f);
            camera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            camera.fieldOfView = 45f;
        }

        private static void CreatePreviewLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static void CreatePreviewFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "PreviewFloor";
            floor.transform.position = new Vector3(0f, -0.06f, 0f);
            floor.transform.localScale = new Vector3(4f, 0.08f, 4f);
            AssignMaterial(floor, new Color(0.25f, 0.28f, 0.3f));
        }

        private static void EnsureFolder(string parent, string folder)
        {
            string path = parent + "/" + folder;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folder);
        }

        private static string ToProjectRelativePath(string path)
        {
            string full = Path.GetFullPath(path).Replace('\\', '/');
            string project = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
            return full.StartsWith(project, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(project.Length)
                : path.Replace('\\', '/');
        }

        private static string NormalizeProjectPath(string path)
        {
            return ToProjectRelativePath(path).TrimEnd('/');
        }

        private static string FormatIssues(CharacterRuntimeSpawnResult result)
        {
            if (result == null || result.Issues.Length == 0)
                return "unknown error";

            var parts = new List<string>();
            for (int i = 0; i < result.Issues.Length; i++)
                parts.Add(result.Issues[i].Code + ": " + result.Issues[i].Message);
            return string.Join("; ", parts);
        }

        private static string FormatAssetGuid(string assetGuid)
        {
            return string.IsNullOrWhiteSpace(assetGuid) ? string.Empty : " guid=" + assetGuid;
        }

        private static string ReadString(JObject obj, string key)
        {
            return obj == null ? string.Empty : (obj[key]?.Value<string>() ?? string.Empty);
        }

        private static bool ReadBool(JObject obj, string key, bool fallback = false)
        {
            return obj == null ? fallback : (obj[key]?.Value<bool>() ?? fallback);
        }

        private static float ReadFloat(JObject obj, string key, float fallback)
        {
            return obj == null ? fallback : (obj[key]?.Value<float?>() ?? fallback);
        }

        private static int ReadInt(JObject obj, string key, int fallback)
        {
            return obj == null ? fallback : (obj[key]?.Value<int?>() ?? fallback);
        }

        private static int? ReadNullableInt(JObject obj, string key, int? fallback)
        {
            return obj == null || obj[key] == null ? fallback : obj[key]?.Value<int?>();
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace('.', '_').Replace('/', '_');
        }

        private static string SanitizeResourceIdSegment(string value)
        {
            return SanitizeName(value).ToLowerInvariant();
        }

        private sealed class ImportedActionSetBinding
        {
            public string StableId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string EquipmentStateStableId { get; set; } = string.Empty;
        }

        private struct ImportedReactionSlot
        {
            public ImportedReactionSlot(
                string slotId,
                string displayName,
                string trigger,
                string pressureBand,
                int reactionGroupId,
                int? minImpactForce,
                int? maxImpactForce,
                int actionId,
                int durationFrames,
                int priority,
                string resourceKey)
            {
                SlotId = slotId ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                Trigger = trigger ?? string.Empty;
                PressureBand = pressureBand ?? string.Empty;
                ReactionGroupId = reactionGroupId;
                MinImpactForce = minImpactForce;
                MaxImpactForce = maxImpactForce;
                ActionId = actionId;
                DurationFrames = durationFrames;
                Priority = priority;
                ResourceKey = resourceKey ?? string.Empty;
            }

            public string SlotId;
            public string DisplayName;
            public string Trigger;
            public string PressureBand;
            public int ReactionGroupId;
            public int? MinImpactForce;
            public int? MaxImpactForce;
            public int ActionId;
            public int DurationFrames;
            public int Priority;
            public string ResourceKey;
        }

        private readonly struct CharacterContactMotionSocketSample
        {
            public CharacterContactMotionSocketSample(
                int socketId,
                string socketPath,
                Transform transform,
                Vector3 localOffset,
                CharacterContactMotionCurveSampler curveSampler,
                CharacterContactMotionSocketSampleSourceKind sourceKind)
            {
                SocketId = socketId;
                SocketPath = socketPath ?? string.Empty;
                Transform = transform;
                LocalOffset = localOffset;
                CurveSampler = curveSampler;
                SourceKind = sourceKind;
            }

            public int SocketId { get; }
            public string SocketPath { get; }
            public Transform Transform { get; }
            public Vector3 LocalOffset { get; }
            public CharacterContactMotionCurveSampler CurveSampler { get; }
            public CharacterContactMotionSocketSampleSourceKind SourceKind { get; }

            public bool TryGetLocalPosition(Transform root, float time, out Vector3 local)
            {
                local = Vector3.zero;
                if (Transform == null || root == null)
                    return false;

                Vector3 world = SourceKind == CharacterContactMotionSocketSampleSourceKind.AnimatedBone
                    || SourceKind == CharacterContactMotionSocketSampleSourceKind.HumanoidCurve
                    ? Transform.TransformPoint(LocalOffset)
                    : Transform.position;
                local = root.InverseTransformPoint(world);
                if (CurveSampler != null)
                    local += CurveSampler.EvaluateDelta(time);
                return true;
            }

            public bool TryGetLocalBasis(
                Transform root,
                out Vector3 right,
                out Vector3 up,
                out Vector3 forward)
            {
                right = Vector3.zero;
                up = Vector3.zero;
                forward = Vector3.zero;
                if (Transform == null || root == null)
                    return false;

                right = root.InverseTransformDirection(Transform.right).normalized;
                up = root.InverseTransformDirection(Transform.up).normalized;
                forward = root.InverseTransformDirection(Transform.forward).normalized;
                return right.sqrMagnitude > 0f && up.sqrMagnitude > 0f && forward.sqrMagnitude > 0f;
            }
        }

        private sealed class CharacterContactMotionCurveSampler
        {
            private readonly AnimationCurve _x;
            private readonly AnimationCurve _y;
            private readonly AnimationCurve _z;
            private readonly Vector3 _origin;

            public CharacterContactMotionCurveSampler(AnimationCurve x, AnimationCurve y, AnimationCurve z)
            {
                _x = x;
                _y = y;
                _z = z;
                _origin = new Vector3(
                    Evaluate(_x, 0f),
                    Evaluate(_y, 0f),
                    Evaluate(_z, 0f));
            }

            public Vector3 EvaluateDelta(float time)
            {
                return new Vector3(
                    Evaluate(_x, time),
                    Evaluate(_y, time),
                    Evaluate(_z, time)) - _origin;
            }

            private static float Evaluate(AnimationCurve curve, float time)
            {
                return curve != null ? curve.Evaluate(time) : 0f;
            }
        }

        private enum CharacterContactMotionSocketSampleSourceKind
        {
            AnimatedBone = 0,
            HumanoidCurve = 1,
            MarkerFallback = 2,
            LocatorFallback = 3
        }

        private readonly struct TransformBoneBindingInfo
        {
            private TransformBoneBindingInfo(
                string bindingId,
                string bonePath,
                Transform target,
                Transform bone,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale)
            {
                BindingId = bindingId ?? string.Empty;
                BonePath = bonePath ?? string.Empty;
                Target = target;
                Bone = bone;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale == Vector3.zero ? Vector3.one : localScale;
            }

            public string BindingId { get; }
            public string BonePath { get; }
            public Transform Target { get; }
            public Transform Bone { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }

            public static TransformBoneBindingInfo FromPose(
                string bindingId,
                string bonePath,
                Transform target,
                Transform bone,
                CharacterRuntimePose pose)
            {
                return new TransformBoneBindingInfo(
                    bindingId,
                    bonePath,
                    target,
                    bone,
                    ToVector3(pose.Position, Vector3.zero),
                    ToQuaternion(pose.Rotation),
                    ToVector3(pose.Scale, Vector3.one));
            }

            public static TransformBoneBindingInfo FromWorld(string bindingId, string bonePath, Transform target, Transform bone)
            {
                return new TransformBoneBindingInfo(
                    bindingId,
                    bonePath,
                    target,
                    bone,
                    Quaternion.Inverse(bone.rotation) * (target.position - bone.position),
                    Quaternion.Inverse(bone.rotation) * target.rotation,
                    target.localScale);
            }
        }

        private readonly struct RuntimeResourceBootstrapEntry
        {
            public RuntimeResourceBootstrapEntry(
                string id,
                string typeId,
                string providerId,
                string variant,
                string packageId,
                string address,
                UnityEngine.Object asset = null)
            {
                Id = id ?? string.Empty;
                TypeId = typeId ?? string.Empty;
                ProviderId = providerId ?? string.Empty;
                Variant = variant ?? string.Empty;
                PackageId = packageId ?? string.Empty;
                Address = address ?? string.Empty;
                Asset = asset;
            }

            public string Id { get; }
            public string TypeId { get; }
            public string ProviderId { get; }
            public string Variant { get; }
            public string PackageId { get; }
            public string Address { get; }
            public UnityEngine.Object Asset { get; }
        }

        private readonly struct CharacterBodyMaterialBindingStatus
        {
            private CharacterBodyMaterialBindingStatus(
                string status,
                string templateId,
                string materialPath,
                string textureResourceKey,
                int rendererCount,
                string message)
            {
                Status = status ?? string.Empty;
                TemplateId = templateId ?? string.Empty;
                MaterialPath = materialPath ?? string.Empty;
                TextureResourceKey = textureResourceKey ?? string.Empty;
                RendererCount = rendererCount;
                Message = message ?? string.Empty;
            }

            public string Status { get; }
            public string TemplateId { get; }
            public string MaterialPath { get; }
            public string TextureResourceKey { get; }
            public int RendererCount { get; }
            public string Message { get; }
            public bool CanBind => string.Equals(Status, "ready", StringComparison.Ordinal);
            public bool IsBound => string.Equals(Status, "bodyMaterialBound", StringComparison.Ordinal);
            public bool ShouldWarn => !string.Equals(Status, "noBodyAlbedo", StringComparison.Ordinal)
                && !CanBind
                && !IsBound;

            public static CharacterBodyMaterialBindingStatus Create(
                string status,
                string templateId,
                string materialPath,
                string textureResourceKey,
                int rendererCount,
                string message)
            {
                return new CharacterBodyMaterialBindingStatus(status, templateId, materialPath, textureResourceKey, rendererCount, message);
            }
        }

        private sealed class PackageMaterialDescriptorBinding
        {
            public string ResourceKey { get; set; } = string.Empty;
            public string DescriptorPath { get; set; } = string.Empty;
            public string SourceMaterialName { get; set; } = string.Empty;
            public string GeneratedMaterialPath { get; set; } = string.Empty;
            public string AlbedoTextureResourceKey { get; set; } = string.Empty;
            public Texture2D AlbedoTexture { get; set; }
            public string NormalTextureResourceKey { get; set; } = string.Empty;
            public Texture2D NormalTexture { get; set; }
            public string MaskTextureResourceKey { get; set; } = string.Empty;
            public Texture2D MaskTexture { get; set; }
            public Material GeneratedMaterial { get; set; }
            public Color AlbedoTint { get; set; } = Color.white;
            public Vector2 AlbedoTextureScale { get; set; } = Vector2.one;
            public Vector2 AlbedoTextureOffset { get; set; } = Vector2.zero;
            public string SurfaceMode { get; set; } = OpaqueMaterialSurfaceMode;
            public float AlphaCutoff { get; set; } = 0.5f;
            public int RenderQueue { get; set; } = -1;
            public bool EnableInstancing { get; set; }
            public bool DoubleSidedGi { get; set; }
        }

        private readonly struct CharacterGeneratedMaterialSurface
        {
            public CharacterGeneratedMaterialSurface(Texture2D albedoTexture, Color albedoTint)
            {
                AlbedoTexture = albedoTexture;
                AlbedoTint = albedoTint;
            }

            public Texture2D AlbedoTexture { get; }
            public Color AlbedoTint { get; }
        }

        private readonly struct CharacterMaterialTemplate
        {
            private CharacterMaterialTemplate(
                string templateId,
                string shaderName,
                string albedoTexturePropertyName,
                string albedoTintPropertyName,
                string normalTexturePropertyName,
                string maskTexturePropertyName)
            {
                TemplateId = templateId ?? string.Empty;
                ShaderName = shaderName ?? string.Empty;
                AlbedoTexturePropertyName = albedoTexturePropertyName ?? string.Empty;
                AlbedoTintPropertyName = albedoTintPropertyName ?? string.Empty;
                NormalTexturePropertyName = normalTexturePropertyName ?? string.Empty;
                MaskTexturePropertyName = maskTexturePropertyName ?? string.Empty;
            }

            public string TemplateId { get; }
            public string ShaderName { get; }
            public string AlbedoTexturePropertyName { get; }
            public string AlbedoTintPropertyName { get; }
            public string NormalTexturePropertyName { get; }
            public string MaskTexturePropertyName { get; }
            public bool IsValid => !string.IsNullOrWhiteSpace(TemplateId)
                && !string.IsNullOrWhiteSpace(ShaderName)
                && !string.IsNullOrWhiteSpace(AlbedoTexturePropertyName)
                && !string.IsNullOrWhiteSpace(AlbedoTintPropertyName);

            public static CharacterMaterialTemplate WGameCharacterTraditionalLit()
            {
                return new CharacterMaterialTemplate(
                    DefaultCharacterBodyMaterialTemplateId,
                    "WGame/CharacterTraditionalLit",
                    "_BaseMap",
                    "_Color",
                    "_WgNormalTex",
                    "_WgMaskTex");
            }

            public bool Apply(Material material, CharacterGeneratedMaterialSurface surface, out string issue)
            {
                issue = string.Empty;
                if (material == null)
                {
                    issue = "Material is null.";
                    return false;
                }

                if (!RequireProperty(material, AlbedoTexturePropertyName, "surface.albedoTexture", out issue)
                    || !RequireProperty(material, AlbedoTintPropertyName, "surface.albedoTint", out issue))
                {
                    return false;
                }

                material.SetTexture(AlbedoTexturePropertyName, surface.AlbedoTexture);
                material.SetColor(AlbedoTintPropertyName, surface.AlbedoTint);
                return true;
            }

            private bool RequireProperty(Material material, string propertyName, string semanticName, out string issue)
            {
                if (material.HasProperty(propertyName))
                {
                    issue = string.Empty;
                    return true;
                }

                issue = "Material template " + TemplateId + " cannot map " + semanticName
                    + " because shader " + ShaderName + " is missing property " + propertyName + ".";
                return false;
            }
        }

        private sealed class ResourcePreviewInfo
        {
            public ResourcePreviewInfo(
                string key,
                string usage,
                string assetPath,
                string assetGuid,
                string importStatus,
                bool isImportReady,
                string avatarSourceAssetPath,
                string avatarSourceGuid,
                string avatarSourceStatus,
                CharacterRuntimePose wrapperPose,
                bool wrapperPoseBakedIntoAsset,
                string[] hiddenRendererNodePaths,
                string[] embeddedWeaponNodePaths,
                string diagnostic)
            {
                Key = key ?? string.Empty;
                Usage = usage ?? string.Empty;
                AssetPath = assetPath ?? string.Empty;
                AssetGuid = assetGuid ?? string.Empty;
                ImportStatus = importStatus ?? string.Empty;
                IsImportReady = isImportReady;
                AvatarSourceAssetPath = avatarSourceAssetPath ?? string.Empty;
                AvatarSourceGuid = avatarSourceGuid ?? string.Empty;
                AvatarSourceStatus = avatarSourceStatus ?? string.Empty;
                WrapperPose = wrapperPose;
                WrapperPoseBakedIntoAsset = wrapperPoseBakedIntoAsset;
                HiddenRendererNodePaths = hiddenRendererNodePaths ?? Array.Empty<string>();
                EmbeddedWeaponNodePaths = embeddedWeaponNodePaths ?? Array.Empty<string>();
                Diagnostic = diagnostic ?? string.Empty;
            }

            public string Key { get; }
            public string Usage { get; }
            public string AssetPath { get; }
            public string AssetGuid { get; }
            public string ImportStatus { get; }
            public bool IsImportReady { get; }
            public string AvatarSourceAssetPath { get; }
            public string AvatarSourceGuid { get; }
            public string AvatarSourceStatus { get; }
            public CharacterRuntimePose WrapperPose { get; }
            public bool WrapperPoseBakedIntoAsset { get; }
            public string[] HiddenRendererNodePaths { get; }
            public string[] EmbeddedWeaponNodePaths { get; }
            public string Diagnostic { get; }

            public ResourcePreviewInfo WithResolvedAsset(
                string assetPath,
                string assetGuid,
                string importStatus,
                bool isImportReady,
                string diagnostic)
            {
                return new ResourcePreviewInfo(
                    Key,
                    Usage,
                    assetPath,
                    assetGuid,
                    importStatus,
                    isImportReady,
                    AvatarSourceAssetPath,
                    AvatarSourceGuid,
                    AvatarSourceStatus,
                    WrapperPose,
                    WrapperPoseBakedIntoAsset,
                    HiddenRendererNodePaths,
                    EmbeddedWeaponNodePaths,
                    diagnostic);
            }
        }
    }
}
