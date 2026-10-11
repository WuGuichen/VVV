using System;
using MxFramework.CharacterAction;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using UnityEngine;
using UnityEngine.Playables;

namespace MxFramework.Editor.CharacterAction.Timeline.Preview
{
    internal sealed partial class CharacterActionPreviewRuntimeSession
    {
        private readonly UnityBladeTrailBudget _previewBladeTrailBudget = new UnityBladeTrailBudget(8);
        private static bool IsBladeTrailRequest(string key) => key == "vfx.weapon.blade_trail"
            || (key != null && key.StartsWith("vfx.weapon.blade_trail:", StringComparison.Ordinal));
        private IUnityBladeTrailPoseSource CreatePreviewBladeTrailSource(CharacterActionVfxRequest request)
        {
            if (_previewInstance == null || _defaultEquipmentBinder == null || !request.Metadata.HasLifecycle
                || !TryResolvePreviewWeaponTraceBinding(request.VfxConfig.Parameter0, request.VfxConfig.Parameter1, out var trace)
                || !_defaultEquipmentBinder.TryGetSpawnedWeapon(trace.WeaponId, trace.EquipmentSlotId, out GameObject weapon)
                || !UnityBladeTrailWeaponLocators.TryResolve(weapon.transform, trace.TraceBinding.StartLocatorPath,
                    trace.TraceBinding.EndLocatorPath, out Transform root, out Transform tip)) return null;
            return new PreviewBladeTrailSource(this, root, tip, request.Metadata.InstanceId);
        }
        private void StopPreviewBladeTrail(CharacterActionVfxRequest request)
        {
            for (int i = 0; i < _activeVfxPreviews.Count; i++)
            {
                PreviewActiveVfx active = _activeVfxPreviews[i];
                if (active.BladeTrail == null || active.SourceInstanceId != request.Metadata.InstanceId
                    || active.StableEventId != request.Metadata.StableEventId) continue;
                active.TrailExiting = true;
                active.BladeTrail.BeginPresentationExit(request.VfxConfig.StopPolicy == CharacterActionVfxStopPolicy.Immediate
                    ? VfxStopMode.Immediate : VfxStopMode.Dissipate);
            }
        }
        private sealed class PreviewBladeTrailSource : IUnityBladeTrailPoseSource, IUnityBladeTrailAnimationEvaluator
        {
            private readonly CharacterActionPreviewRuntimeSession _session;
            private readonly Transform _root;
            private readonly Transform _tip;
            private readonly long _instance;
            private readonly int _equipmentRevision;
            private readonly UnityBladeTrailTransformSampler _sampler;
            private bool _disposed;
            public PreviewBladeTrailSource(CharacterActionPreviewRuntimeSession session, Transform root, Transform tip, long instance)
            {
                _session = session; _root = root; _tip = tip; _instance = instance;
                _equipmentRevision = session._defaultEquipmentBinder.EquipmentRevision;
                _sampler = new UnityBladeTrailTransformSampler(session._previewInstance.transform, root, tip, this);
            }
            public bool TryGetPresentedPose(out UnityBladeTrailPose pose)
            {
                pose = default;
                if (_disposed || _session._previewInstance == null || _root == null || _tip == null
                    || _session._defaultEquipmentBinder == null
                    || _session._defaultEquipmentBinder.EquipmentRevision != _equipmentRevision
                    || _session._gateway?.ActiveInstance?.InstanceId != _instance
                    || !_session.TryResolvePreviewAnimationClipEvent(_session._lastFrame, out AnimationTrackEvent animation)) return false;
                Transform owner = _session._previewInstance.transform;
                pose = new UnityBladeTrailPose(_root.position, _tip.position, owner.position, owner.rotation,
                    ResolvePreviewAnimationSampleTime(animation, _session._lastFrame), _instance);
                return true;
            }
            public bool TrySamplePose(double actionTime, Vector3 position, Quaternion rotation, out Vector3 root, out Vector3 tip) =>
                _sampler.TrySample(actionTime, position, rotation, out root, out tip);
            public bool TryEvaluateBladeTrailPose(double actionTimeSeconds)
            {
                if (_disposed) return false;
                if (_session._locomotion != null && _session._locomotion.TryEvaluateActionPoseAtTime(actionTimeSeconds))
                { _session._boneSync?.ApplyBindings(); return true; }
                if (!_session._fallbackAnimationGraph.IsValid() || !_session._fallbackAnimationPlayable.IsValid()) return false;
                double originalTime = _session._fallbackAnimationPlayable.GetTime();
                bool fireEvents = _session._previewAnimator != null && _session._previewAnimator.fireEvents;
                try
                {
                    if (_session._previewAnimator != null) _session._previewAnimator.fireEvents = false;
                    _session._fallbackAnimationPlayable.SetTime(actionTimeSeconds);
                    _session._fallbackAnimationGraph.Evaluate(0f);
                    _session._boneSync?.ApplyBindings();
                    return true;
                }
                finally
                {
                    _session._fallbackAnimationPlayable.SetTime(originalTime);
                    if (_session._previewAnimator != null) _session._previewAnimator.fireEvents = fireEvents;
                }
            }
            public void Dispose() { _disposed = true; }
        }
    }
}
