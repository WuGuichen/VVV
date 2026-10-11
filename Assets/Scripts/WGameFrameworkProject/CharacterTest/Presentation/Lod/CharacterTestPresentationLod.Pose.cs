using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestPresentationLod
    {
        private readonly Dictionary<Renderer, SubjectBinding> _poseSubjectsByRenderer = new Dictionary<Renderer, SubjectBinding>();
        private readonly List<Material> _poseMaterialScratch = new List<Material>(4);
        private Func<Renderer, bool> _shadowPoseDemand;
        internal long PoseObservations { get; private set; }
        internal long PoseEvaluations { get; private set; }
        internal long PoseHolds { get; private set; }

        /// <summary>NoAlloc after camera observation warmup. Reuses the same registered camera snapshots and cache.</summary>
        internal bool ShouldEvaluatePose(PresentationLodHandle handle, int entityId, bool consumerNeedsPose)
        {
            if (!_coordinator.IsRegistered(handle) || !_subjects.TryGetValue(entityId, out SubjectBinding subject)) return true;
            if (!subject.PoseRegistered)
            { subject.PoseRegistered = true; _coordinator.InvalidateObservation(handle); }
            bool protect = consumerNeedsPose || subject.OwnerImportant || subject.PadImportant
                || subject.FeedbackUntil > Now || PotentialPoseCaster(subject.Renderers) || subject.GeometryDirty;
            long cameras = CameraCaptures * 397 + _coordinator.Counters.Cameras;
            if (protect)
            {
                subject.Pose.Invalidate(); subject.Pose.Evaluate(Now); PoseEvaluations++; return true;
            }
            if (subject.Pose.NeedsObservation(Now, cameras))
            {
                bool known = true, anyCamera = false;
                float height = 0;
                foreach (var pair in _cameras)
                {
                    if (!pair.Value.Captured) continue;
                    anyCamera = true;
                    if (!_coordinator.TryObserve(handle, pair.Key, out var value)) { known = false; continue; }
                    known &= value.Known;
                    height = Mathf.Max(height, value.ScreenHeight);
                    protect |= value.PoseShadowNeedsPose;
                }
                subject.Pose.Observe(Now, cameras, known && anyCamera, height, protect);
                PoseObservations++;
            }
            bool evaluate = subject.Pose.Evaluate(Now);
            if (evaluate) PoseEvaluations++; else PoseHolds++;
            return evaluate;
        }

        internal void BindShadowPoseDemand(Func<Renderer, bool> demand)
        {
            _shadowPoseDemand = demand;
            foreach (SubjectBinding binding in _subjects.Values) binding.Pose.Invalidate();
        }

        internal MxDirectionalShadowPosePublication ReadPosePublication(Renderer renderer)
        {
            if (renderer == null || !_poseSubjectsByRenderer.TryGetValue(renderer, out var binding)) return default;
            var view = binding.View;
            return new MxDirectionalShadowPosePublication(view.PublishedPoseVersion, view.PublishedPoseFrame,
                view.HasReliablePoseDeformationVersion);
        }

        internal bool CollectShadowReceiverCameras(List<UnityEngine.Camera> cameras)
        {
            UnityEngine.Camera main = CharacterTestMainCameraCache.Resolve(ref _main);
            if (Eligible(main) && main.isActiveAndEnabled && !cameras.Contains(main)) cameras.Add(main);
            foreach (CameraBinding binding in _cameras.Values)
                if (binding.Explicit && Eligible(binding.Camera) && binding.Camera.isActiveAndEnabled
                    && !cameras.Contains(binding.Camera)) cameras.Add(binding.Camera);
            return true;
        }

        private void BindPoseRenderers(SubjectBinding binding)
        {
            foreach (Renderer renderer in binding.Renderers)
                if (renderer != null) _poseSubjectsByRenderer[renderer] = binding;
        }
        private void UnbindPoseRenderers(SubjectBinding binding)
        {
            foreach (Renderer renderer in binding.Renderers)
                if (!ReferenceEquals(renderer, null) && _poseSubjectsByRenderer.TryGetValue(renderer, out var owner)
                    && ReferenceEquals(owner, binding)) _poseSubjectsByRenderer.Remove(renderer);
        }

        private bool PotentialPoseCaster(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0) return true;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) return true;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.shadowCastingMode == ShadowCastingMode.Off) continue;
                renderer.GetSharedMaterials(_poseMaterialScratch);
                bool casts = false;
                for (int sub = 0; sub < _poseMaterialScratch.Count; sub++)
                    casts |= MxDirectionalShadowCoverage.CanCast(_poseMaterialScratch[sub]);
                if (!casts) continue;
                if (_shadowPoseDemand == null) return true;
                try { if (_shadowPoseDemand(renderer)) return true; }
                catch { return true; }
            }
            return false;
        }
    }
}
