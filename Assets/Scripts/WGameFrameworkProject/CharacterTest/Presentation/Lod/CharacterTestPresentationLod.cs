using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using MxFramework.Rendering;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    internal enum PresentationLodCameraPurpose { GameplayBase, UiOverlay, IndependentPreview, Reflection }
    /// <summary>Unity observation adapter for one GameSlice's registered views. No scene-wide discovery.</summary>
    internal sealed partial class CharacterTestPresentationLod : IPresentationLodObserver
    {
        private sealed class SubjectBinding
        {
            internal CharacterTestRuntimeCharacterView View;
            internal PresentationLodHandle Handle;
            internal Renderer[] Renderers;
            internal Vector3 Anchor;
            internal Matrix4x4 Transform;
            internal bool GeometryDirty, ShadowProtected;
            internal bool OwnerImportant, PadImportant;
            internal bool PoseRegistered;
            internal readonly PresentationCadenceLodPolicy Pose = new PresentationCadenceLodPolicy();
            internal readonly PresentationCadenceLodPolicy Vfx = new PresentationCadenceLodPolicy();
            internal long VfxTraversal = -1, VfxObservedPoseVersion = -1;
            internal Matrix4x4 VfxObservedTransform;
            internal bool VfxDecision;
            internal CharacterTestPresentationLodLifetime Lifetime;
            internal readonly PresentationGeometryLodConsumer Geometry = new PresentationGeometryLodConsumer();
            internal double FeedbackUntil;
        }
        private sealed class CameraBinding
        {
            internal UnityCamera Camera;
            internal bool Explicit;
            internal long Revision;
            internal Matrix4x4 View, Projection;
            internal Rect Pixels;
            internal int Layers;
            internal RenderTexture Output;
            internal int Display;
            internal Vector3 Position;
            internal readonly Plane[] Planes = new Plane[6];
            internal bool Captured;
        }
        private readonly Dictionary<int, SubjectBinding> _subjects = new Dictionary<int, SubjectBinding>();
        private readonly Dictionary<int, CameraBinding> _cameras = new Dictionary<int, CameraBinding>();
        private readonly Dictionary<UnityCamera, int> _cameraIds = new Dictionary<UnityCamera, int>();
        private int _nextCameraId;
        private readonly List<int> _deadCameras = new List<int>(2);
        private readonly PresentationLodCoordinator _coordinator;
        private UnityCamera _main;
        private int _lateFrame = -1;
        private int _evaluationFrame = -1, _frameEvaluations;
        private const int MaximumFrameEvaluations = 32;
        internal CharacterTestPresentationLod() { _coordinator = new PresentationLodCoordinator(this); }
        internal PresentationLodCounters Counters => _coordinator.Counters;
        internal long CameraCaptures { get; private set; }
        internal long ProjectionComputations { get; private set; }
        internal long BoundsComputations { get; private set; }
        internal long GeometryRefreshes { get; private set; }
        internal double Now => Time.unscaledTimeAsDouble;

        /// <remarks>LowFreqAlloc at view creation. Consumers are cached once; no frame GetComponents.</remarks>
        internal PresentationLodHandle Register(CharacterTestRuntimeCharacterView view)
        {
            if (_subjects.TryGetValue(view.EntityId.Value, out SubjectBinding existing)) return existing.Handle;
            var binding = new SubjectBinding { View = view, Anchor = view.Instance.transform.position,
                Transform = view.Instance.transform.localToWorldMatrix,
                Renderers = view.Instance.GetComponentsInChildren<Renderer>(true) };
            binding.Handle = _coordinator.Register(view.EntityId.Value, view.HasPresentationCloth,
                view.SetClothLodPaused, Now);
            _subjects.Add(view.EntityId.Value, binding);
            BindPoseRenderers(binding);
            binding.Geometry.Bind(view.Instance);
            binding.ShadowProtected = PotentialCaster(binding.Renderers);
            _coordinator.SetPoseProtection(binding.Handle, binding.ShadowProtected, Now);
            binding.Lifetime = view.Instance.AddComponent<CharacterTestPresentationLodLifetime>();
            binding.Lifetime.Bind(this, binding.Handle, view.EntityId.Value);
            return binding.Handle;
        }

        internal void Unregister(PresentationLodHandle handle, int entityId, bool destroyed = false)
        {
            if (!_coordinator.Unregister(handle)) return;
            if (_subjects.TryGetValue(entityId, out SubjectBinding binding))
            {
                binding.Lifetime?.Detach(); binding.Geometry.Clear(); UnbindPoseRenderers(binding); _subjects.Remove(entityId);
                if (destroyed) binding.View.ReleaseDestroyedPresentationLod();
            }
        }

        /// <summary>Explicit API for a second gameplay base camera. Previews and overlay cameras are rejected.</summary>
        internal bool RegisterGameplayCamera(UnityCamera camera, PresentationLodCameraPurpose purpose)
        {
            if (purpose != PresentationLodCameraPurpose.GameplayBase || !Eligible(camera)) return false;
            int id = GetOrAddCamera(camera);
            CameraBinding binding = _cameras[id];
            binding.Explicit = true; Capture(id, binding); return true;
        }
        internal void UnregisterGameplayCamera(UnityCamera camera)
        {
            if (camera == null) return;
            if (!_cameraIds.TryGetValue(camera, out int id)) return;
            if (!_cameras.TryGetValue(id, out CameraBinding binding)) return;
            binding.Explicit = false;
            if (camera != _main)
            {
                _coordinator.RemoveCamera(id); _cameras.Remove(id); _cameraIds.Remove(camera);
                foreach (SubjectBinding subject in _subjects.Values) subject.Geometry.RemoveCamera(id);
            }
        }

        internal bool RegisterReflectionCamera(UnityCamera camera)
        {
            if (!MxSceneRenderingAuxiliaryCamera.TryGet(camera, out var auxiliary) ||
                auxiliary.Kind != MxCameraRenderKind.Reflection || !Eligible(camera)) return false;
            int id = GetOrAddCamera(camera);
            CameraBinding binding = _cameras[id]; binding.Explicit = true; Capture(id, binding);
            return true;
        }

        /// <summary>Called once before the existing presentation traversal, after the gameplay camera tick.</summary>
        internal void BeginPresentation()
        {
            _vfxTraversal++;
            CaptureCameras();
            UpdateGeometry();
            TickBounded();
        }

        /// <summary>Final UI phase remains active at timeScale=0. Each session captures its cameras once per LateUpdate.</summary>
        internal void BeginLatePresentation()
        {
            if (_lateFrame == Time.frameCount) return;
            _lateFrame = Time.frameCount;
            CaptureCameras();
            UpdateGeometry();
            TickBounded();
        }

        private void TickBounded()
        {
            int frame = Time.frameCount;
            if (_evaluationFrame != frame) { _evaluationFrame = frame; _frameEvaluations = 0; }
            _frameEvaluations += _coordinator.Tick(Now, MaximumFrameEvaluations - _frameEvaluations);
        }

        internal bool TryProject(PresentationLodHandle handle, UnityCamera camera, out Vector3 screen)
        {
            screen = default;
            BeginLatePresentation();
            if (camera == null || !_cameraIds.TryGetValue(camera, out int cameraId)
                || !_coordinator.TryObserve(handle, cameraId, out var observed)
                || !observed.Projected) return false;
            screen = new Vector3(observed.ScreenX, observed.ScreenY, observed.Depth); return true;
        }

        internal void SetAnchor(PresentationLodHandle handle, int entityId, Vector3 anchor)
        {
            if (!_coordinator.IsRegistered(handle) || !_subjects.TryGetValue(entityId, out SubjectBinding binding)) return;
            if (binding.Anchor.Equals(anchor)) return;
            binding.Anchor = anchor; _coordinator.Invalidate(handle, Now);
        }
        internal void RefreshTransform(PresentationLodHandle handle, int entityId)
        {
            if (!_coordinator.IsRegistered(handle) || !_subjects.TryGetValue(entityId, out SubjectBinding binding)
                || binding.View.Instance == null) return;
            Matrix4x4 current = binding.View.Instance.transform.localToWorldMatrix;
            if (binding.Transform.Equals(current)) return;
            binding.Transform = current; _coordinator.Invalidate(handle, Now);
        }
        /// <summary>Existing pose traversal invalidates dynamic bounds only for potentially reducible cloth.</summary>
        internal void RefreshPose(PresentationLodHandle handle, int entityId)
        {
            if (!_coordinator.IsRegistered(handle) || !_subjects.TryGetValue(entityId, out SubjectBinding binding)
                || (!binding.PoseRegistered && (!binding.View.HasPresentationCloth || binding.ShadowProtected))) return;
            _coordinator.InvalidateObservation(handle);
        }
        internal void InvalidateGeometry(PresentationLodHandle handle, int entityId)
        {
            if (!_coordinator.IsRegistered(handle) || !_subjects.TryGetValue(entityId, out SubjectBinding binding)) return;
            binding.GeometryDirty = true; _coordinator.Invalidate(handle, Now);
            binding.Pose.Invalidate();
            binding.Geometry.Bind(binding.View.Instance);
            // A replaced/nested renderer is unknown until the next demanded observation refreshes its binding.
            _coordinator.SetPoseProtection(handle, true, Now);
        }
        internal void Discontinuity(PresentationLodHandle handle)
        {
            _coordinator.Invalidate(handle, Now);
            foreach (SubjectBinding subject in _subjects.Values)
                if (subject.Handle.Slot == handle.Slot && subject.Handle.Generation == handle.Generation)
                { subject.Pose.Invalidate(); subject.Vfx.Invalidate(); subject.VfxTraversal = -1; break; }
        }
        internal void SetImportance(PresentationLodHandle handle, int entityId, bool locked, double feedbackUntil)
        {
            if (!_coordinator.IsRegistered(handle) || !_subjects.TryGetValue(entityId, out SubjectBinding binding)) return;
            binding.PadImportant = locked;
            binding.FeedbackUntil = Math.Max(binding.FeedbackUntil, feedbackUntil);
            if (locked || binding.OwnerImportant || binding.FeedbackUntil > Now)
            { binding.Geometry.Begin(true); binding.Geometry.Apply(); binding.Pose.Invalidate(); binding.Vfx.Invalidate(); }
            _coordinator.SetImportance(handle, locked || binding.OwnerImportant, feedbackUntil, Now);
        }
        internal void SetOwnerImportance(PresentationLodHandle handle, int entityId, bool important)
        {
            if (!_coordinator.IsRegistered(handle) || !_subjects.TryGetValue(entityId, out SubjectBinding binding)) return;
            binding.OwnerImportant = important;
            if (important) { binding.Geometry.Begin(true); binding.Geometry.Apply(); binding.Pose.Invalidate(); binding.Vfx.Invalidate(); }
            _coordinator.SetImportance(handle, important || binding.PadImportant, 0, Now);
        }

        internal void Clear()
        {
            foreach (SubjectBinding binding in _subjects.Values) { binding.Lifetime?.Detach(); binding.Geometry.Clear(); }
            _subjects.Clear(); _cameras.Clear(); _cameraIds.Clear(); _coordinator.Clear(); _main = null; _lateFrame = -1;
            _poseSubjectsByRenderer.Clear(); _shadowPoseDemand = null;
            _evaluationFrame = -1; _frameEvaluations = 0;
        }

        public PresentationLodObservation Observe(int subjectId, int cameraId)
        {
            if (!_subjects.TryGetValue(subjectId, out SubjectBinding subject) || subject.View.Instance == null
                || !_cameras.TryGetValue(cameraId, out CameraBinding camera) || !camera.Captured) return default;
            if (subject.GeometryDirty)
            {
                UnbindPoseRenderers(subject);
                subject.Renderers = subject.View.Instance.GetComponentsInChildren<Renderer>(true);
                BindPoseRenderers(subject);
                subject.GeometryDirty = false; GeometryRefreshes++;
                subject.ShadowProtected = PotentialCaster(subject.Renderers);
                _coordinator.SetPoseProtection(subject.Handle, subject.ShadowProtected, Now);
            }
            bool projected = false;
            float x = 0, y = 0, depth = 0;
            if (subject.View.HasPresentationHeadPad)
            {
                Vector3 local = camera.View.MultiplyPoint(subject.Anchor);
                Vector4 clip = camera.Projection * new Vector4(local.x, local.y, local.z, 1);
                projected = Finite(clip.x) && Finite(clip.y) && Finite(clip.w) && Mathf.Abs(clip.w) > 0.000001f;
                x = projected ? camera.Pixels.x + (clip.x / clip.w * 0.5f + 0.5f) * camera.Pixels.width : 0;
                y = projected ? camera.Pixels.y + (clip.y / clip.w * 0.5f + 0.5f) * camera.Pixels.height : 0;
                depth = -local.z; ProjectionComputations++;
            }
            bool known = false, visible = false, shadow = false, invalidBounds = false;
            Bounds modelBounds = default;
            float screenHeight = float.PositiveInfinity;
            if (!subject.PoseRegistered && (!subject.View.HasPresentationCloth || subject.ShadowProtected))
            {
                known = projected;
                visible = projected && depth > 0 && camera.Pixels.Contains(new Vector2(x, y));
                shadow = subject.ShadowProtected;
            }
            else
            {
                BoundsComputations++;
                for (int i = 0; i < subject.Renderers.Length; i++)
                {
                    Renderer renderer = subject.Renderers[i];
                    if (renderer == null) { subject.GeometryDirty = true; invalidBounds = true; continue; }
                    // Phase 1 conservatively protects every skinned potential caster, including a later cast-mode change.
                    shadow |= renderer is SkinnedMeshRenderer || renderer.shadowCastingMode != ShadowCastingMode.Off;
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    Bounds bounds = renderer.bounds;
                    bool valid = Finite(bounds.center.x) && Finite(bounds.center.y) && Finite(bounds.center.z)
                        && Finite(bounds.extents.x) && Finite(bounds.extents.y) && Finite(bounds.extents.z)
                        && bounds.extents.sqrMagnitude > 0;
                    if (!valid) { shadow = true; invalidBounds = true; continue; }
                    if (!known) modelBounds = bounds; else modelBounds.Encapsulate(bounds);
                    known = true;
                    if ((camera.Layers & (1 << renderer.gameObject.layer)) != 0)
                        visible |= GeometryUtility.TestPlanesAABB(camera.Planes, bounds);
                }
                known &= !invalidBounds;
                if (known) screenHeight = visible ? PresentationGeometryLodPolicy.ScreenHeight(camera.View,
                    camera.Projection, modelBounds.center, modelBounds.extents.magnitude, camera.Camera.orthographic) : 0f;
            }
            return new PresentationLodObservation(known, visible, shadow, projected, x, y, depth,
                (subject.Anchor - camera.Position).sqrMagnitude, screenHeight, PotentialPoseCaster(subject.Renderers));
        }

        private void CaptureCameras()
        {
            UnityCamera current = CharacterTestMainCameraCache.Resolve(ref _main);
            int mainId = Eligible(current) ? GetOrAddCamera(current) : 0;
            _deadCameras.Clear();
            foreach (var pair in _cameras)
                if (pair.Value.Camera == null || (!pair.Value.Explicit && pair.Key != mainId)) _deadCameras.Add(pair.Key);
            for (int i = 0; i < _deadCameras.Count; i++)
            {
                int id = _deadCameras[i]; UnityCamera removed = _cameras[id].Camera;
                _coordinator.RemoveCamera(id); _cameras.Remove(id);
                foreach (SubjectBinding subject in _subjects.Values) subject.Geometry.RemoveCamera(id);
                if (!ReferenceEquals(removed, null)) _cameraIds.Remove(removed);
            }
            foreach (var pair in _cameras) Capture(pair.Key, pair.Value);
        }
        private int GetOrAddCamera(UnityCamera camera)
        {
            if (_cameraIds.TryGetValue(camera, out int id)) return id;
            id = ++_nextCameraId;
            _cameraIds.Add(camera, id); _cameras.Add(id, new CameraBinding { Camera = camera });
            return id;
        }
        private void UpdateGeometry()
        {
            foreach (SubjectBinding subject in _subjects.Values)
            {
                if (subject.Geometry.Count == 0) continue;
                subject.Geometry.Begin(subject.OwnerImportant || subject.PadImportant || subject.FeedbackUntil > Now);
                foreach (var pair in _cameras)
                    if (pair.Value.Captured) subject.Geometry.Observe(pair.Key, pair.Value.View, pair.Value.Projection,
                        pair.Value.Camera.orthographic);
                subject.Geometry.Apply();
            }
        }
        private void Capture(int id, CameraBinding binding)
        {
            UnityCamera camera = binding.Camera;
            if (!Eligible(camera) || !camera.isActiveAndEnabled)
            { binding.Captured = false; _coordinator.RemoveCamera(id); return; }
            Matrix4x4 view = camera.worldToCameraMatrix, projection = camera.projectionMatrix;
            Rect pixels = camera.pixelRect; int layers = camera.cullingMask;
            RenderTexture output = camera.targetTexture; int display = camera.targetDisplay;
            if (binding.Captured && binding.View.Equals(view) && binding.Projection.Equals(projection)
                && binding.Pixels.Equals(pixels) && binding.Layers == layers
                && ReferenceEquals(binding.Output, output) && binding.Display == display) return;
            binding.View = view; binding.Projection = projection; binding.Pixels = pixels;
            binding.Output = output; binding.Display = display;
            binding.Position = camera.transform.position; binding.Layers = layers;
            binding.Revision++; binding.Captured = true; CameraCaptures++;
            GeometryUtility.CalculateFrustumPlanes(projection * view, binding.Planes);
            _coordinator.SetCamera(id, binding.Revision);
        }
        private static bool Eligible(UnityCamera camera) => camera != null && camera.cameraType == CameraType.Game
            && !camera.TryGetComponent<CharacterTestRuntimeAiLabPreviewCameraMarker>(out _)
            && !camera.TryGetComponent<MxSceneLightingPreviewCamera>(out _)
            && camera != global::FairyGUI.StageCamera.main;
        private static bool PotentialCaster(Renderer[] renderers)
        {
            if (renderers.Length == 0) return true;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] == null || renderers[i] is SkinnedMeshRenderer
                    || renderers[i].shadowCastingMode != ShadowCastingMode.Off) return true;
            return false;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
