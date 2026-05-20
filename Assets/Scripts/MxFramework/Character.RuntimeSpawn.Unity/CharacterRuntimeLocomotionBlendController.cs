using System;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterRuntimeInputMotionController))]
    [AddComponentMenu("MxFramework/Character/Runtime Locomotion Blend Controller")]
    public sealed class CharacterRuntimeLocomotionBlendController : MonoBehaviour
    {
        [SerializeField] private CharacterRuntimeInputMotionController _motionController;
        [SerializeField] private Transform _modelRoot;
        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationClip[] _animationClips = Array.Empty<AnimationClip>();
        [SerializeField] private string _blendXParameter = "LocomotionX";
        [SerializeField] private string _blendYParameter = "LocomotionY";
        [SerializeField] private string _speedParameter = "LocomotionSpeed";
        [SerializeField] private bool _enableFallbackPose = true;
        [SerializeField] private float _fallbackBobAmplitude = 0.035f;
        [SerializeField] private float _fallbackBobRate = 7f;
        [SerializeField] private float _fallbackLeanDegrees = 4f;

        private Vector3 _modelRootBaseLocalPosition;
        private Quaternion _modelRootBaseLocalRotation;
        private bool _hasBasePose;
        private float _fallbackPhase;
        private Vector2 _blend;
        private float _speed01;
        private bool _usingFallback;

        public Vector2 Blend => _blend;
        public float Speed01 => _speed01;
        public bool HasAnimationClips => _animationClips != null && _animationClips.Length > 0;
        public bool UsingFallback => _usingFallback;

        private void Awake()
        {
            ResolveReferences();
            CaptureBasePose();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (_motionController == null)
                return;

            _blend = Vector2.ClampMagnitude(_motionController.LastLocalMove, 1f);
            _speed01 = Mathf.Clamp01(_motionController.LastSpeed01);
            ApplyAnimatorParameters();
            ApplyFallbackPose();
        }

        public void Configure(
            Transform modelRoot,
            Animator animator,
            AnimationClip[] animationClips)
        {
            _modelRoot = modelRoot;
            _animator = animator;
            _animationClips = animationClips ?? Array.Empty<AnimationClip>();
            CaptureBasePose();
        }

        private void ResolveReferences()
        {
            if (_motionController == null)
                _motionController = GetComponent<CharacterRuntimeInputMotionController>();
            if (_modelRoot == null)
                _modelRoot = transform.Find("ModelRoot");
            if (_animator == null && _modelRoot != null)
                _animator = _modelRoot.GetComponentInChildren<Animator>(includeInactive: true);
        }

        private void CaptureBasePose()
        {
            if (_modelRoot == null)
                return;

            _modelRootBaseLocalPosition = _modelRoot.localPosition;
            _modelRootBaseLocalRotation = _modelRoot.localRotation;
            _hasBasePose = true;
        }

        private void ApplyAnimatorParameters()
        {
            if (_animator == null)
                return;

            SetAnimatorFloat(_blendXParameter, _blend.x);
            SetAnimatorFloat(_blendYParameter, _blend.y);
            SetAnimatorFloat(_speedParameter, _speed01);
        }

        private void ApplyFallbackPose()
        {
            _usingFallback = _enableFallbackPose && !HasAnimationClips;
            if (!_usingFallback || _modelRoot == null || !_hasBasePose)
                return;

            if (_speed01 <= 0.001f)
            {
                _fallbackPhase = 0f;
                _modelRoot.localPosition = Vector3.Lerp(_modelRoot.localPosition, _modelRootBaseLocalPosition, 0.35f);
                _modelRoot.localRotation = Quaternion.Slerp(_modelRoot.localRotation, _modelRootBaseLocalRotation, 0.35f);
                return;
            }

            _fallbackPhase += Time.deltaTime * Mathf.Max(0.1f, _fallbackBobRate) * Mathf.Lerp(0.65f, 1.35f, _speed01);
            float bob = Mathf.Sin(_fallbackPhase) * _fallbackBobAmplitude * _speed01;
            Quaternion lean = Quaternion.Euler(_blend.y * _fallbackLeanDegrees, 0f, -_blend.x * _fallbackLeanDegrees);
            _modelRoot.localPosition = _modelRootBaseLocalPosition + new Vector3(0f, bob, 0f);
            _modelRoot.localRotation = _modelRootBaseLocalRotation * lean;
        }

        private void SetAnimatorFloat(string parameterName, float value)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                return;

            for (int i = 0; i < _animator.parameterCount; i++)
            {
                AnimatorControllerParameter parameter = _animator.parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Float
                    && string.Equals(parameter.name, parameterName, StringComparison.Ordinal))
                {
                    _animator.SetFloat(parameterName, value);
                    return;
                }
            }
        }
    }
}
