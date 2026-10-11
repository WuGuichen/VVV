using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MxFramework.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Read-only discovery; applying its result is a separate GameManager Undo operation.</summary>
    internal static class CharacterTestSceneRenderingAutoScanner
    {
        internal sealed class Result
        {
            internal Transform StaticRoot;
            internal MeshRenderer[] StaticSourceRenderers;
            internal string StaticSelectionNote;
            internal Camera Camera;
            internal Light Sun;
            internal MxRenderingPipelineFeature Feature;
            internal MxSceneLightingData Data;
            internal MxSceneLightingLook Look;
            internal ComputeShader Compute;
            internal Shader CaptureShader, ResolveShader, FogShader, DustShader;
            internal string BaseColorProperty, SurfaceDataProperty, SolidColorProperty;
            internal string EmissionIntensityProperty;
            internal string SecondaryColorProperty, SecondaryBlendProperty;
            internal readonly List<string> Diagnostics = new List<string>();

            internal int ApplyMissing(MxSceneRenderingConfiguration c)
            {
                int filled = 0;
                if (c.StaticRoot == null && StaticRoot != null) { c.StaticRoot = StaticRoot; filled++; }
                if ((c.StaticSourceRenderers == null || c.StaticSourceRenderers.Length == 0) &&
                    StaticSourceRenderers != null && StaticSourceRenderers.Length > 0)
                    { c.StaticSourceRenderers = StaticSourceRenderers; filled++; }
                if (c.TargetCamera == null && Camera != null) { c.TargetCamera = Camera; filled++; }
                if (c.Sun == null && Sun != null) { c.Sun = Sun; filled++; }
                if (c.Feature == null && Feature != null) { c.Feature = Feature; filled++; }
                if (c.Data == null && Data != null) { c.Data = Data; filled++; }
                if (c.Look == null && Look != null) { c.Look = Look; filled++; }
                if (c.Compute == null && Compute != null) { c.Compute = Compute; filled++; }
                if (c.CaptureShader == null && CaptureShader != null) { c.CaptureShader = CaptureShader; filled++; }
                if (c.ResolveShader == null && ResolveShader != null) { c.ResolveShader = ResolveShader; filled++; }
                if (c.FogCompositeShader == null && FogShader != null) { c.FogCompositeShader = FogShader; filled++; }
                if (c.DustShader == null && DustShader != null) { c.DustShader = DustShader; filled++; }
                if (c.SourceLayout == null) { c.SourceLayout = new MxSceneLightingSourceLayout(); filled++; }
                var layout = c.SourceLayout;
                if (string.IsNullOrWhiteSpace(layout.BaseColorProperty) && BaseColorProperty != null)
                    { layout.BaseColorProperty = BaseColorProperty; filled++; }
                if (string.IsNullOrWhiteSpace(layout.SurfaceDataProperty) && SurfaceDataProperty != null)
                    { layout.SurfaceDataProperty = SurfaceDataProperty; filled++; }
                if (string.IsNullOrWhiteSpace(layout.EmissionIntensityProperty) && EmissionIntensityProperty != null)
                    { layout.EmissionIntensityProperty = EmissionIntensityProperty; filled++; }
                if (string.IsNullOrWhiteSpace(layout.SolidColorProperty) && SolidColorProperty != null)
                    { layout.SolidColorProperty = SolidColorProperty; filled++; }
                if (string.IsNullOrWhiteSpace(layout.SecondaryColorProperty) && SecondaryColorProperty != null)
                    { layout.SecondaryColorProperty = SecondaryColorProperty; filled++; }
                if (string.IsNullOrWhiteSpace(layout.SecondaryBlendProperty) && SecondaryBlendProperty != null)
                    { layout.SecondaryBlendProperty = SecondaryBlendProperty; filled++; }
                return filled;
            }

            internal string Message(int filled) => "自动扫描填充 " + filled + " 项；保留已有绑定和 GI／雾开关。" +
                (string.IsNullOrEmpty(StaticSelectionNote) ? string.Empty : "\n" + StaticSelectionNote) +
                (Diagnostics.Count == 0 ? string.Empty : "\n" + string.Join("\n", Diagnostics));
        }

        internal static Result Scan(GameManager owner, MxSceneRenderingConfiguration current)
        {
            if (owner == null || current == null) throw new ArgumentNullException(owner == null ? nameof(owner) : nameof(current));
            Scene scene = owner.gameObject.scene;
            var result = new Result();
            if ((current.StaticRoot == null || current.StaticRoot == owner.WorldObjectSceneRoot) &&
                (current.StaticSourceRenderers == null || current.StaticSourceRenderers.Length == 0) &&
                TrySelectTestCourseSources(owner, scene, out var courseSources))
            {
                result.StaticRoot = owner.WorldObjectSceneRoot;
                result.StaticSourceRenderers = courseSources;
                result.StaticSelectionNote = "已选择测试场地、材质样本和固定台阶／坡道的 " +
                    courseSources.Length + " 个网格作为 GI 静态源；活动物体和网格线已排除。";
            }
            else if (current.StaticRoot == null) result.StaticRoot = FindStaticRoot(owner, scene, result.Diagnostics);
            if (current.TargetCamera == null) result.Camera = FindCamera(scene, result.Diagnostics);
            if (current.Sun == null) result.Sun = FindSun(scene, result.Diagnostics);
            if (current.Feature == null)
                result.Feature = FindFeature(current.TargetCamera != null ? current.TargetCamera : result.Camera, result.Diagnostics);
            if (current.Data == null) result.Data = FindSceneData(scene, result.Diagnostics);
            if (current.Look == null)
                result.Look = UniqueAssets<MxSceneLightingLook>("t:MxSceneLightingLook", "共享 Look", result.Diagnostics);
            if (current.Compute == null)
                result.Compute = UniqueAssets<ComputeShader>("SceneLighting t:ComputeShader", "光照 Compute",
                    result.Diagnostics, x => x.name == "SceneLighting");
            if (current.CaptureShader == null)
                result.CaptureShader = FindShader("Hidden/MxFramework/SceneLightingCapture", result.Diagnostics);
            if (current.ResolveShader == null)
                result.ResolveShader = FindShader("Hidden/MxFramework/SceneLightingResolve", result.Diagnostics);
            if (current.FogCompositeShader == null)
                result.FogShader = FindShader("Hidden/MxFramework/Rendering/VolumetricFogComposite", result.Diagnostics);

            if (current.DustShader == null)
                result.DustShader = FindShader("Hidden/MxFramework/Rendering/SceneDust", result.Diagnostics);

            Transform materialRoot = current.StaticRoot != null ? current.StaticRoot : result.StaticRoot;
            if (materialRoot == null && owner.WorldObjectSceneRoot != owner.transform)
                materialRoot = owner.WorldObjectSceneRoot;
            if (materialRoot != null) FindMaterialMapping(materialRoot, result);
            return result;
        }

        private static Camera FindCamera(Scene scene, List<string> diagnostics)
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
                .Where(x => x.gameObject.scene == scene).ToArray();
            var tagged = cameras.Where(x => x.CompareTag("MainCamera")).ToArray();
            if (tagged.Length == 1) return tagged[0];
            if (tagged.Length > 1) { diagnostics.Add("多个 MainCamera，目标相机需手动选择。"); return null; }
            var active = cameras.Where(x => x.isActiveAndEnabled && x.cameraType == CameraType.Game).ToArray();
            if (active.Length == 1) return active[0];
            diagnostics.Add(active.Length == 0 ? "未找到场景主相机。" : "多个活动 Game 相机，目标相机需手动选择。");
            return null;
        }

        private static Light FindSun(Scene scene, List<string> diagnostics)
        {
            var sun = RenderSettings.sun;
            if (sun != null && sun.gameObject.scene == scene) return sun;
            var directional = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
                .Where(x => x.gameObject.scene == scene && x.isActiveAndEnabled && x.type == LightType.Directional)
                .ToArray();
            if (directional.Length == 1) return directional[0];
            diagnostics.Add(directional.Length == 0 ? "未找到场景太阳。" :
                "多个方向光且 RenderSettings.sun 未指定，太阳需手动选择。");
            return null;
        }

        private static Transform FindStaticRoot(GameManager owner, Scene scene, List<string> diagnostics)
        {
            Transform explicitRoot = owner.WorldObjectSceneRoot;
            if (explicitRoot != null && explicitRoot != owner.transform && IsSafeStaticRoot(explicitRoot))
                return explicitRoot;
            var candidates = scene.GetRootGameObjects().Select(x => x.transform)
                .Where(x => x != owner.transform && x != explicitRoot && IsSafeStaticRoot(x)).ToArray();
            if (candidates.Length == 1) return candidates[0];
            diagnostics.Add(candidates.Length > 1 ? "多个可预处理静态根，需手动选择。" :
                "无法安全确认静态根：世界根含动态物体或缺少兼容材质；GI 构建前需单独指定静态几何根。");
            return null;
        }

        private static bool TrySelectTestCourseSources(GameManager owner, Scene scene,
            out MeshRenderer[] renderers)
        {
            renderers = null;
            Transform root = owner.WorldObjectSceneRoot;
            if (scene.name != "CharacterTestScene" || root == null || root == owner.transform ||
                root.gameObject.scene != scene) return false;
            var names = new[] { "TestCourse_00_Start", "TestCourse_10_SurfaceMaterials", "TestCourse_30_MotionGeometry" };
            var selected = new List<MeshRenderer>();
            foreach (string name in names)
            {
                var groups = Enumerable.Range(0, root.childCount).Select(root.GetChild)
                    .Where(x => x.name == name).ToArray();
                if (groups.Length != 1 || groups[0].GetComponentInChildren<SkinnedMeshRenderer>(false) != null ||
                    groups[0].GetComponentInChildren<Rigidbody>(false) != null ||
                    groups[0].GetComponentInChildren<Animator>(false) != null ||
                    groups[0].GetComponentInChildren<MonoBehaviour>(false) != null) return false;
                var groupRenderers = groups[0].GetComponentsInChildren<MeshRenderer>(false)
                    .Where(x => x.enabled && (name != "TestCourse_00_Start" || x.name == "GridPlane_120m"))
                    .ToArray();
                if (groupRenderers.Length == 0) return false;
                foreach (var renderer in groupRenderers)
                {
                    if (!renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null ||
                        renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material =>
                            material == null ||
                            !(material.HasProperty("_WgStoneColor") ||
                              (material.HasProperty("_WgBakedBaseColor") &&
                               material.GetTexture("_WgBakedBaseColor") != null &&
                               material.HasProperty("_WgSurfaceDataTex") &&
                               material.GetTexture("_WgSurfaceDataTex") != null)))) return false;
                    selected.Add(renderer);
                }
            }
            renderers = selected.ToArray();
            return true;
        }

        private static bool IsSafeStaticRoot(Transform root)
        {
            if (root.GetComponentInChildren<SkinnedMeshRenderer>(false) != null ||
                root.GetComponentsInChildren<Rigidbody>(false).Any(x => !x.isKinematic)) return false;
            bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!renderer.enabled || !renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                    continue;
                if (!renderer.gameObject.isStatic) return false;
                found = true;
                foreach (var material in renderer.sharedMaterials)
                    if (material == null || (!material.HasProperty("_WgBakedBaseColor") &&
                        !material.HasProperty("_WgStoneColor"))) return false;
            }
            return found;
        }

        private static MxRenderingPipelineFeature FindFeature(Camera camera, List<string> diagnostics)
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipeline)
            { diagnostics.Add("当前不是 URP，无法识别渲染入口。"); return null; }
            var serialized = new SerializedObject(pipeline);
            var renderers = serialized.FindProperty("m_RendererDataList");
            var defaultIndex = serialized.FindProperty("m_DefaultRendererIndex");
            int index = defaultIndex != null ? defaultIndex.intValue : 0;
            if (camera != null && camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
            {
                var property = cameraData.GetType().GetProperty("rendererIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property?.GetValue(cameraData) is int cameraIndex && cameraIndex >= 0) index = cameraIndex;
            }
            if (renderers == null || index < 0 || index >= renderers.arraySize)
            { diagnostics.Add("无法读取目标相机使用的 URP Renderer。"); return null; }
            var rendererData = renderers.GetArrayElementAtIndex(index).objectReferenceValue as ScriptableRendererData;
            if (rendererData == null)
            { diagnostics.Add("目标相机的 URP Renderer 资源缺失。"); return null; }
            var features = new SerializedObject(rendererData).FindProperty("m_RendererFeatures");
            var matches = new List<MxRenderingPipelineFeature>();
            if (features != null)
                for (int i = 0; i < features.arraySize; i++)
                    if (features.GetArrayElementAtIndex(i).objectReferenceValue is MxRenderingPipelineFeature feature)
                        matches.Add(feature);
            if (matches.Count == 1)
            {
                if (!matches[0].isActive) diagnostics.Add("URP 渲染入口当前未启用，请检查 Renderer 资源。");
                return matches[0];
            }
            diagnostics.Add(matches.Count == 0 ? "目标相机的 URP Renderer 未安装 MxRenderingPipelineFeature。" :
                "目标相机的 URP Renderer 存在多个渲染入口，需手动选择。");
            return null;
        }

        private static MxSceneLightingData FindSceneData(Scene scene, List<string> diagnostics)
        {
            string marker = "/SceneLighting/" + scene.name + "/";
            var matches = AssetDatabase.FindAssets("t:MxSceneLightingData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Replace('\\', '/').IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(AssetDatabase.LoadAssetAtPath<MxSceneLightingData>)
                .Where(x => x != null).ToArray();
            if (matches.Length == 1) return matches[0];
            diagnostics.Add(matches.Length == 0 ? "当前场景没有光传输缓存；启用 GI 前需构建。" :
                "当前场景匹配到多个光传输缓存，需手动选择。");
            return null;
        }

        private static T UniqueAssets<T>(string query, string label, List<string> diagnostics,
            Func<T, bool> predicate = null) where T : UnityEngine.Object
        {
            var matches = AssetDatabase.FindAssets(query).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>).Where(x => x != null && (predicate == null || predicate(x)))
                .Distinct().ToArray();
            if (matches.Length == 1) return matches[0];
            diagnostics.Add(matches.Length == 0 ? "未找到" + label + "。" : "找到多个" + label + "，需手动选择。");
            return null;
        }

        private static Shader FindShader(string shaderName, List<string> diagnostics)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null && shader.isSupported) return shader;
            diagnostics.Add("缺少可用 Shader：" + shaderName);
            return null;
        }

        private static void FindMaterialMapping(Transform root, Result result)
        {
            var materials = root.GetComponentsInChildren<MeshRenderer>(false)
                .Where(x => x.enabled).SelectMany(x => x.sharedMaterials).Where(x => x != null).Distinct().ToArray();
            if (materials.Any(x => x.HasProperty("_WgBakedBaseColor") && x.HasProperty("_WgSurfaceDataTex")))
            { result.BaseColorProperty = "_WgBakedBaseColor"; result.SurfaceDataProperty = "_WgSurfaceDataTex"; }
            if (materials.Any(x => x.HasProperty("_WgStoneColor"))) result.SolidColorProperty = "_WgStoneColor";
            if (materials.Any(x => x.HasProperty("_WgEmissionIntensity"))) result.EmissionIntensityProperty = "_WgEmissionIntensity";
            if (materials.Any(x => x.HasProperty("_WgWeatheredStoneColor") && x.HasProperty("_WgWeatheringCoverage")))
            { result.SecondaryColorProperty = "_WgWeatheredStoneColor"; result.SecondaryBlendProperty = "_WgWeatheringCoverage"; }
            if (materials.Length > 0 && result.BaseColorProperty == null && result.SolidColorProperty == null)
                result.Diagnostics.Add("未识别当前材质的底色来源，GI 构建前需检查高级材质映射。");
        }
    }
}
