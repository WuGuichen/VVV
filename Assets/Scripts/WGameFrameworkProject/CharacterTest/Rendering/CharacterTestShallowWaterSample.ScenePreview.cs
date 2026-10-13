using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestShallowWaterSession
    {
        // Editor supplies only live, visible Scene cameras. No UnityEditor dependency crosses this boundary.
        private readonly Dictionary<UnityCamera, ScenePreviewView> _scenePreviews = new Dictionary<UnityCamera, ScenePreviewView>();
        internal int ScenePreviewCount => _scenePreviews.Count;
        internal int SceneSurfaceDraws { get; private set; }
        internal int SceneReflectionRenders { get; private set; }

        internal sealed class ScenePreviewView
        {
            internal UnityCamera Source, Reflection;
            internal UniversalAdditionalCameraData Data;
            internal MxSceneRenderingAuxiliaryCamera Auxiliary;
            internal RTHandle Target, ColorView;
            internal Matrix4x4 ViewProjection;
            internal readonly Plane[] Frustum = new Plane[6];
            internal readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
            internal readonly RippleFieldView RippleField = new RippleFieldView();
            internal bool Wanted, Valid;
            internal WaterReflectionSchedule Schedule;
            internal int Budget;
            internal string Issue = string.Empty;
            internal RenderTexture Texture => Target?.rt;
            internal bool Ready => Valid && Texture != null && Texture.IsCreated();
        }

        internal bool TryGetScenePreview(UnityCamera camera, out ScenePreviewView view)
        {
            view = null;
            return !_disposed && _pipeline != null && camera != null && camera.cameraType == CameraType.SceneView
                && _scenePreviews.TryGetValue(camera, out view);
        }

        internal bool PrepareScenePreview(UnityCamera camera, out ScenePreviewView view)
        {
            view = null;
            if (_disposed || _pipeline == null || !IsActive || camera == null
                || camera.cameraType != CameraType.SceneView || Settings == null) return false;
            if (!_scenePreviews.TryGetValue(camera, out view))
            {
                view = new ScenePreviewView { Source = camera };
                _scenePreviews.Add(camera, view);
            }
            PrepareScenePreview(view);
            return true;
        }

        private void PrepareScenePreview(ScenePreviewView view)
        {
            if (!RipplesEnabled || Settings.EffectiveRippleLimit == 0 || _ripples.Count == 0
                || !SurfaceVisible(view.Source, view.Frustum)) view.RippleField.Release();
            if (view.Source == null)
            {
                view.Wanted = false;
                _lod?.UnregisterGameplayCamera(view.Reflection); ReleaseSceneTarget(view); return;
            }
            GeometryUtility.CalculateFrustumPlanes(view.Source, view.Frustum);
            view.Wanted = SurfaceEnabled && ReflectionEnabled
                && view.Source.transform.position.y > Position.y + .006f
                && GeometryUtility.TestPlanesAABB(view.Frustum, SurfaceBounds(Position, Rotation, Settings.Size));
            if (!view.Wanted)
            {
                if (view.Reflection != null) view.Reflection.enabled = false;
                _lod?.UnregisterGameplayCamera(view.Reflection);
                ReleaseSceneTarget(view);
                view.Issue = string.Empty;
                return;
            }
            if (view.Reflection == null)
            {
                var go = new GameObject("Shallow Water Scene Reflection") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, Owner.gameObject.scene);
                view.Reflection = go.AddComponent<UnityCamera>();
                view.Reflection.enabled = false;
                view.Data = go.AddComponent<UniversalAdditionalCameraData>();
                view.Auxiliary = go.AddComponent<MxSceneRenderingAuxiliaryCamera>();
                view.Auxiliary.Kind = MxCameraRenderKind.Reflection;
                view.Auxiliary.InvertCulling = true;
            }
            // World lighting still belongs to this scene's configured Game camera. Screen resources never do.
            view.Auxiliary.SourceCamera = _source;
            if (!EnsureSceneTarget(view)) return;
            var camera = view.Reflection;
            int budget = ReflectionBudget;
            if (view.Budget != budget) { view.Budget = budget; view.Valid = false; }
            bool render = !Application.isPlaying || view.Schedule.IsDue(view.Source, Time.realtimeSinceStartupAsDouble,
                view.Ready, Settings.EffectiveReflectionRate);
            camera.enabled = render && Application.isPlaying;
            if (!render) return;
            camera.CopyFrom(view.Source);
            camera.cameraType = CameraType.Game;
            camera.enabled = Application.isPlaying;
            camera.targetTexture = view.Texture;
            camera.depth = _source.depth - 1;
            camera.cullingMask = view.Source.cullingMask & Settings.ReflectionLayers.value & ~(1 << 5);
            camera.farClipPlane = Mathf.Min(view.Source.farClipPlane, Settings.EffectiveReflectionDistance);
            camera.useOcclusionCulling = false; camera.allowMSAA = false;
            view.Data.renderType = CameraRenderType.Base;
            view.Data.renderPostProcessing = false; view.Data.renderShadows = Settings.UseReflectionShadows && _sourceData.renderShadows;
            view.Data.requiresColorOption = CameraOverrideOption.Off;
            view.Data.requiresDepthOption = CameraOverrideOption.On;
            view.Data.volumeLayerMask = 0; view.Data.antialiasing = AntialiasingMode.None;
            Matrix4x4 mirror = ReflectionMatrix(Position.y);
            camera.transform.SetPositionAndRotation(mirror.MultiplyPoint(view.Source.transform.position),
                Quaternion.LookRotation(mirror.MultiplyVector(view.Source.transform.forward), mirror.MultiplyVector(view.Source.transform.up)));
            camera.worldToCameraMatrix = view.Source.worldToCameraMatrix * mirror;
            // CopyFrom preserves perspective/orthographic lens settings; rebuild with the bounded far clip.
            camera.ResetProjectionMatrix();
            Vector3 point = camera.worldToCameraMatrix.MultiplyPoint(Position + Vector3.up * .006f);
            Vector3 normal = camera.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
            camera.projectionMatrix = camera.CalculateObliqueMatrix(new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal)));
            var projection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
            if (!view.ViewProjection.Equals(projection)) view.Valid = false;
            view.ViewProjection = projection;
            view.Issue = string.Empty;
        }

        private bool EnsureSceneTarget(ScenePreviewView view)
        {
            float scale = Settings.EffectiveReflectionScale;
            int width = Mathf.Max(64, Mathf.RoundToInt(view.Source.pixelWidth * scale));
            int height = Mathf.Max(64, Mathf.RoundToInt(view.Source.pixelHeight * scale));
            float limit = Mathf.Min(1, Settings.EffectiveReflectionEdge / (float)Mathf.Max(width, height));
            width = Mathf.Max(64, Mathf.RoundToInt(width * limit)); height = Mathf.Max(64, Mathf.RoundToInt(height * limit));
            var cached = view.Texture;
            if (cached != null && view.ColorView != null && cached.IsCreated() && SupportedDepth(cached.depthStencilFormat)
                && cached.width == width && cached.height == height) return true;
            ReleaseSceneTarget(view);
            GraphicsFormat depth = SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil);
            if (!SupportedDepth(depth)) depth = GraphicsFormat.D24_UNorm_S8_UInt;
            if (!SupportedDepth(depth)) depth = GraphicsFormat.D32_SFloat_S8_UInt;
            if (!SupportedDepth(depth))
            { view.Issue = "No supported Scene reflection depth-stencil format."; return false; }
            var descriptor = new RenderTextureDescriptor(width, height)
            { graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat, depthStencilFormat = depth, msaaSamples = 1, sRGB = false };
            var rt = new RenderTexture(descriptor)
            { name = "WGame.ShallowWater.SceneReflection", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!rt.Create() || !SupportedDepth(rt.depthStencilFormat))
            { DestroyOwned(rt); view.Issue = "Could not create Scene reflection output with depth-stencil."; return false; }
            view.Target = RTHandles.Alloc(rt);
            view.ColorView = RTHandles.Alloc(new RenderTargetIdentifier(rt), "WGame.ShallowWater.SceneReflection.Color");
            return true;
        }

        internal void RenderScenePreviewReflection(UnityCamera camera)
        {
            if (Application.isPlaying || _renderRequestInProgress || _renderContextDepth != 0) return;
            if (!PrepareScenePreview(camera, out var view) || !view.Wanted || view.Texture == null) return;
            _renderRequestInProgress = true;
            try
            {
                RenderPipeline.SubmitRenderRequest(view.Reflection,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = view.Texture });
            }
            finally { _renderRequestInProgress = false; _renderContextDepth = 0; }
        }

        private void RefreshScenePreviews()
        {
            foreach (var view in _scenePreviews.Values) PrepareScenePreview(view);
        }

        private void BindScenePreviewLod(CharacterTestPresentationLod lod, bool remove)
        {
            foreach (var view in _scenePreviews.Values)
                if (remove || !view.Wanted) lod?.UnregisterGameplayCamera(view.Reflection);
                else lod?.RegisterReflectionCamera(view.Reflection);
        }

        private void EndSceneReflection(UnityCamera camera)
        {
            foreach (var view in _scenePreviews.Values)
            {
                if (camera != view.Reflection) continue;
                SceneReflectionRenders++;
                view.Valid = view.Texture != null && view.Texture.IsCreated() && camera.targetTexture == view.Texture;
                view.Schedule.Record(view.Source, Time.realtimeSinceStartupAsDouble);
                return;
            }
        }

        private void ReleaseSceneTarget(ScenePreviewView view)
        {
            view.Valid = false;
            if (view.Reflection != null) { view.Reflection.enabled = false; view.Reflection.targetTexture = null; }
            view.ColorView?.Release(); view.ColorView = null;
            var rt = view.Texture;
            view.Target?.Release(); view.Target = null;
            DestroyOwned(rt);
        }

        internal void ReleaseScenePreview(UnityCamera camera)
        {
            // Unity-null camera keys must still be removed from the dictionary.
            if (ReferenceEquals(camera, null) || !_scenePreviews.TryGetValue(camera, out var view)) return;
            ReleaseScenePreview(view); _scenePreviews.Remove(camera);
        }

        private void ReleaseScenePreview(ScenePreviewView view)
        {
            view.RippleField.Release();
            _lod?.UnregisterGameplayCamera(view.Reflection);
            ReleaseSceneTarget(view);
            if (view.Auxiliary != null) view.Auxiliary.SourceCamera = null;
            if (view.Reflection != null) DestroyOwned(view.Reflection.gameObject);
            view.Reflection = null; view.Data = null; view.Auxiliary = null;
        }

        internal void ReleaseScenePreviews()
        {
            foreach (var view in _scenePreviews.Values) ReleaseScenePreview(view);
            _scenePreviews.Clear();
        }
    }
}
