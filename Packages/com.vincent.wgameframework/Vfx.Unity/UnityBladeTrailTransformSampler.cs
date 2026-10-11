using System;
using UnityEngine;

namespace MxFramework.Vfx.Unity
{
    public interface IUnityBladeTrailAnimationEvaluator
    {
        /// <summary>NoAlloc. Temporarily evaluate matching current clip; retain its clock and transition state.</summary>
        bool TryEvaluateBladeTrailPose(double actionTimeSeconds);
    }

    /// <summary>LowFreqAlloc at construction; fixed transform snapshots restore presentation after each sub-sample.</summary>
    public sealed class UnityBladeTrailTransformSampler
    {
        private readonly Transform _owner;
        private readonly Transform _root;
        private readonly Transform _tip;
        private readonly Transform[] _transforms;
        private readonly Vector3[] _positions;
        private readonly Quaternion[] _rotations;
        private readonly Vector3[] _scales;
        private readonly IUnityBladeTrailAnimationEvaluator _evaluator;
        public UnityBladeTrailTransformSampler(Transform owner, Transform root, Transform tip,
            IUnityBladeTrailAnimationEvaluator evaluator)
        {
            _owner = owner; _root = root; _tip = tip; _evaluator = evaluator;
            _transforms = owner.GetComponentsInChildren<Transform>(includeInactive: true);
            _positions = new Vector3[_transforms.Length]; _rotations = new Quaternion[_transforms.Length];
            _scales = new Vector3[_transforms.Length];
        }
        public bool TrySample(double actionTime, Vector3 ownerPosition, Quaternion ownerRotation,
            out Vector3 root, out Vector3 tip)
        {
            root = default; tip = default;
            if (_owner == null || _root == null || _tip == null || _evaluator == null) return false;
            for (int i = 0; i < _transforms.Length; i++)
            {
                Transform target = _transforms[i]; if (target == null) return false;
                _positions[i] = target.localPosition; _rotations[i] = target.localRotation; _scales[i] = target.localScale;
            }
            try
            {
                if (!_evaluator.TryEvaluateBladeTrailPose(actionTime)) return false;
                _owner.SetPositionAndRotation(ownerPosition, ownerRotation);
                root = _root.position; tip = _tip.position;
                return true;
            }
            finally
            {
                for (int i = 0; i < _transforms.Length; i++)
                {
                    Transform target = _transforms[i]; if (target == null) continue;
                    target.localPosition = _positions[i]; target.localRotation = _rotations[i]; target.localScale = _scales[i];
                }
            }
        }
    }
}
