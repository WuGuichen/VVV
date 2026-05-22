using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.Resources;
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
        [SerializeField] private string _blend2DId = "blend.move2d";
        [SerializeField] private string _blendXParameter = "moveX";
        [SerializeField] private string _blendYParameter = "moveY";
        [SerializeField] private int _blendParameterScale = 1000;
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
        private UnityPlayablesAnimationBackend _animationBackend;
        private bool _ownsAnimationBackend;
        private MxAnimationBlend2DDefinition _blend2DDefinition;
        private MxAnimationBlendReachabilityReport _blendReachabilityReport;
        private MxAnimationBackendResult _lastAnimationResult;
        private int _lastQuantizedBlendX;
        private int _lastQuantizedBlendY;
        private int _blendDomainMinX = -1000;
        private int _blendDomainMaxX = 1000;
        private int _blendDomainMinY = -1000;
        private int _blendDomainMaxY = 1000;

        public Vector2 Blend => _blend;
        public float Speed01 => _speed01;
        public bool HasAnimationClips => _animationClips != null && _animationClips.Length > 0;
        public bool HasAnimationBackend => _animationBackend != null;
        public bool UsingFallback => _usingFallback;
        public string ActiveBlend2DId => _blend2DId;
        public MxAnimationBackendResult LastAnimationResult => _lastAnimationResult;
        public int LastQuantizedBlendX => _lastQuantizedBlendX;
        public int LastQuantizedBlendY => _lastQuantizedBlendY;
        public MxAnimationBlend2DControllerDomain ActiveBlend2DControllerDomain =>
            new MxAnimationBlend2DControllerDomain(_blendDomainMinX, _blendDomainMaxX, _blendDomainMinY, _blendDomainMaxY);
        public MxAnimationBlendReachabilityReport ActiveBlendReachabilityReport =>
            _blendReachabilityReport ?? CreateReachabilityReport();

        private void Awake()
        {
            ResolveReferences();
            CaptureBasePose();
        }

        private void OnDestroy()
        {
            ReleaseAnimationBackend();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (_motionController == null)
                return;

            _blend = _motionController.LastLocalMove;
            _speed01 = Mathf.Max(0f, _motionController.LastSpeed01);
            ApplyAnimatorParameters();
            ApplyAnimationBackend();
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

        public void ConfigureAnimationBackend(
            UnityPlayablesAnimationBackend backend,
            MxAnimationBlend2DDefinition blendDefinition,
            bool ownsBackend = true)
        {
            ReleaseAnimationBackend();
            _animationBackend = backend;
            _ownsAnimationBackend = ownsBackend;
            _lastAnimationResult = default;
            _lastQuantizedBlendX = 0;
            _lastQuantizedBlendY = 0;
            _blend2DDefinition = blendDefinition;
            _blendReachabilityReport = null;

            if (blendDefinition == null)
            {
                ResetBlendDomain();
                return;
            }

            _blend2DId = blendDefinition.BlendId;
            _blendXParameter = blendDefinition.ParameterXId;
            _blendYParameter = blendDefinition.ParameterYId;
            _blendParameterScale = Mathf.Max(1, Math.Max(blendDefinition.ParameterXScale, blendDefinition.ParameterYScale));
            RebuildBlendDomain(blendDefinition);
            _blendReachabilityReport = CreateReachabilityReport();
        }

        public MxAnimationDiagnosticSnapshot CreateAnimationSnapshot()
        {
            return _animationBackend != null ? _animationBackend.CreateSnapshot() : null;
        }

        public bool SetClipPlaybackSpeedOverride(ResourceKey clipKey, float playbackSpeed)
        {
            return _animationBackend != null && _animationBackend.SetClipPlaybackSpeedOverride(clipKey, playbackSpeed);
        }

        public bool TryGetClipPlaybackSpeedOverride(ResourceKey clipKey, out float playbackSpeed)
        {
            if (_animationBackend != null)
                return _animationBackend.TryGetClipPlaybackSpeedOverride(clipKey, out playbackSpeed);

            playbackSpeed = 0f;
            return false;
        }

        public MxAnimationLocomotionBlendProbeSnapshot CreateLocomotionBlendProbeSnapshot()
        {
            if (_blend2DDefinition == null)
                return null;

            MxAnimationBlendReachabilityReport reachability = ActiveBlendReachabilityReport;
            IReadOnlyList<MxAnimationBlend2DWeight> weights = TryGetBackendBlend2DWeights(out bool fromBackend);
            if (weights == null || weights.Count == 0)
            {
                MxAnimationBlend2DWeights evaluated = MxAnimationBlend2DCalculator.Evaluate(
                    _blend2DDefinition,
                    new MxAnimationQuantizedParameter(_blendXParameter, _lastQuantizedBlendX, _blendParameterScale),
                    new MxAnimationQuantizedParameter(_blendYParameter, _lastQuantizedBlendY, _blendParameterScale));
                weights = evaluated.Weights;
                fromBackend = false;
            }

            return new MxAnimationLocomotionBlendProbeSnapshot(
                _blend2DDefinition.BlendId,
                ActiveBlend2DControllerDomain,
                _lastQuantizedBlendX,
                _lastQuantizedBlendY,
                reachability,
                weights,
                fromBackend);
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
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return;

            SetAnimatorFloat(_blendXParameter, _blend.x);
            SetAnimatorFloat(_blendYParameter, _blend.y);
            SetAnimatorFloat(_speedParameter, _speed01);
        }

        private void ApplyAnimationBackend()
        {
            if (_animationBackend == null || string.IsNullOrWhiteSpace(_blend2DId))
                return;

            int scale = Math.Max(1, _blendParameterScale);
            _lastQuantizedBlendX = QuantizeBlendAxis(_blend.x, scale, _blendDomainMinX, _blendDomainMaxX);
            _lastQuantizedBlendY = QuantizeBlendAxis(_blend.y, scale, _blendDomainMinY, _blendDomainMaxY);
            _lastAnimationResult = _animationBackend.SetBlend2D(new MxAnimationBlend2DRequest
            {
                BlendId = _blend2DId,
                ParameterX = new MxAnimationQuantizedParameter(_blendXParameter, _lastQuantizedBlendX, scale),
                ParameterY = new MxAnimationQuantizedParameter(_blendYParameter, _lastQuantizedBlendY, scale),
                CorrelationId = "character-runtime-locomotion:" + Time.frameCount.ToString()
            });
            _animationBackend.Tick(Time.deltaTime);
        }

        private void ApplyFallbackPose()
        {
            _usingFallback = _enableFallbackPose && _animationBackend == null && (_animator == null || !HasAnimationClips);
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

        private void ReleaseAnimationBackend()
        {
            if (_animationBackend != null && _ownsAnimationBackend)
                _animationBackend.Release();

            _animationBackend = null;
            _ownsAnimationBackend = false;
            _blend2DDefinition = null;
            _blendReachabilityReport = null;
        }

        private MxAnimationBlendReachabilityReport CreateReachabilityReport()
        {
            if (_blend2DDefinition == null)
                return null;

            return MxAnimationBlendReachabilityAnalyzer.Analyze(_blend2DDefinition, ActiveBlend2DControllerDomain);
        }

        private void ResetBlendDomain()
        {
            int scale = Mathf.Max(1, _blendParameterScale);
            _blendDomainMinX = -scale;
            _blendDomainMaxX = scale;
            _blendDomainMinY = -scale;
            _blendDomainMaxY = scale;
        }

        private void RebuildBlendDomain(MxAnimationBlend2DDefinition blendDefinition)
        {
            ResetBlendDomain();
            if (blendDefinition == null || blendDefinition.Points.Count == 0)
                return;

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int i = 0; i < blendDefinition.Points.Count; i++)
            {
                MxAnimationBlend2DPoint point = blendDefinition.Points[i];
                if (point == null)
                    continue;

                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            if (minX <= maxX)
            {
                _blendDomainMinX = minX;
                _blendDomainMaxX = maxX;
            }

            if (minY <= maxY)
            {
                _blendDomainMinY = minY;
                _blendDomainMaxY = maxY;
            }
        }

        private static int QuantizeBlendAxis(float value, int scale, int min, int max)
        {
            if (min > max)
            {
                int swap = min;
                min = max;
                max = swap;
            }

            return Mathf.Clamp(Mathf.RoundToInt(value * Mathf.Max(1, scale)), min, max);
        }

        private IReadOnlyList<MxAnimationBlend2DWeight> TryGetBackendBlend2DWeights(out bool fromBackend)
        {
            fromBackend = false;
            MxAnimationDiagnosticSnapshot snapshot = CreateAnimationSnapshot();
            if (snapshot == null)
                return Array.Empty<MxAnimationBlend2DWeight>();

            for (int i = 0; i < snapshot.LayerStates.Count; i++)
            {
                MxAnimationLayerDiagnostic layer = snapshot.LayerStates[i];
                if (layer.BlendKind == MxAnimationBlendKind.Blend2D
                    && string.Equals(layer.Blend2DId, _blend2DDefinition.BlendId, StringComparison.Ordinal))
                {
                    fromBackend = true;
                    return layer.Blend2DWeights;
                }
            }

            return Array.Empty<MxAnimationBlend2DWeight>();
        }
    }
}
