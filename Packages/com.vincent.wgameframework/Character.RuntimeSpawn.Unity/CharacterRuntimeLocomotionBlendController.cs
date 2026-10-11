using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Resources;
using UnityEngine;
using UnityEngine.Serialization;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    /// <summary>
    /// AllocByDesign presentation checkpoint for locomotion, action playback, blend state, and the optional backend graph.
    /// </summary>
    public sealed class CharacterRuntimeAnimationPlaybackState
    {
        public CharacterRuntimeAnimationPlaybackState(
            MxAnimationPlaybackState backendState,
            float fallbackPhase,
            Vector2 blend,
            float speed01,
            bool useExternalMotion,
            Vector2 externalBlend,
            float externalSpeed01,
            bool usingFallback,
            string lastActionKey,
            int lastQuantizedBlendX,
            int lastQuantizedBlendY,
            float playbackSpeedCompensation,
            bool hasAppliedLocomotionBlend,
            string lastAppliedBlendId,
            int lastAppliedBlendParameterScale,
            int lastAppliedQuantizedBlendX,
            int lastAppliedQuantizedBlendY,
            float lastAppliedPlaybackSpeedCompensation,
            float runtimeAnimationClock,
            float suppressLocomotionBlendUntilTime)
        {
            BackendState = backendState;
            FallbackPhase = fallbackPhase;
            Blend = blend;
            Speed01 = speed01;
            UseExternalMotion = useExternalMotion;
            ExternalBlend = externalBlend;
            ExternalSpeed01 = externalSpeed01;
            UsingFallback = usingFallback;
            LastActionKey = lastActionKey ?? string.Empty;
            LastQuantizedBlendX = lastQuantizedBlendX;
            LastQuantizedBlendY = lastQuantizedBlendY;
            PlaybackSpeedCompensation = playbackSpeedCompensation;
            HasAppliedLocomotionBlend = hasAppliedLocomotionBlend;
            LastAppliedBlendId = lastAppliedBlendId ?? string.Empty;
            LastAppliedBlendParameterScale = lastAppliedBlendParameterScale;
            LastAppliedQuantizedBlendX = lastAppliedQuantizedBlendX;
            LastAppliedQuantizedBlendY = lastAppliedQuantizedBlendY;
            LastAppliedPlaybackSpeedCompensation = lastAppliedPlaybackSpeedCompensation;
            RuntimeAnimationClock = runtimeAnimationClock;
            SuppressLocomotionBlendUntilTime = suppressLocomotionBlendUntilTime;
        }

        public MxAnimationPlaybackState BackendState { get; }
        public float FallbackPhase { get; }
        public Vector2 Blend { get; }
        public float Speed01 { get; }
        public bool UseExternalMotion { get; }
        public Vector2 ExternalBlend { get; }
        public float ExternalSpeed01 { get; }
        public bool UsingFallback { get; }
        public string LastActionKey { get; }
        public int LastQuantizedBlendX { get; }
        public int LastQuantizedBlendY { get; }
        public float PlaybackSpeedCompensation { get; }
        public bool HasAppliedLocomotionBlend { get; }
        public string LastAppliedBlendId { get; }
        public int LastAppliedBlendParameterScale { get; }
        public int LastAppliedQuantizedBlendX { get; }
        public int LastAppliedQuantizedBlendY { get; }
        public float LastAppliedPlaybackSpeedCompensation { get; }
        public float RuntimeAnimationClock { get; }
        public float SuppressLocomotionBlendUntilTime { get; }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Runtime Locomotion Blend Controller")]
    public sealed class CharacterRuntimeLocomotionBlendController : MonoBehaviour
    {
        private const float ExternalIdleTargetThreshold = 0.0001f;
        private const float ExternalIdleSnapThreshold = 0.05f;
        private const string LocomotionBlendCorrelationId = "character-runtime-locomotion";

        [SerializeField] private CharacterRuntimeInputMotionController _motionController;
        [SerializeField] private Transform _modelRoot;
        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationClip[] _animationClips = Array.Empty<AnimationClip>();
        [FormerlySerializedAs("_blend2DId")]
        [SerializeField] private string _blendId = "blend.move2d";
        [SerializeField] private string _blendXParameter = "moveX";
        [SerializeField] private string _blendYParameter = "moveY";
        [SerializeField] private int _blendParameterScale = 1000;
        [SerializeField] private string _speedParameter = "LocomotionSpeed";
        [SerializeField] private bool _enableFallbackPose = true;
        [SerializeField] private float _fallbackBobAmplitude = 0.035f;
        [SerializeField] private float _fallbackBobRate = 7f;
        [SerializeField] private float _fallbackLeanDegrees = 4f;
        [SerializeField] private bool _enablePlaybackSpeedCompensation = true;
        [SerializeField] private float _minPlaybackSpeedCompensation = 0.25f;
        [SerializeField] private float _maxPlaybackSpeedCompensation = 2.5f;
        [SerializeField] private float _externalBlendResponse = 18f;
        [SerializeField] private float _externalSpeedResponse = 18f;
        [SerializeField] private float _externalBlendStopResponse = 90f;
        [SerializeField] private float _externalSpeedStopResponse = 90f;
        [SerializeField] private bool _useExternalRuntimeTick;
        [SerializeField] private DirectClipBlendPoint[] _directClipBlendPoints = Array.Empty<DirectClipBlendPoint>();

        private Vector3 _modelRootBaseLocalPosition;
        private Quaternion _modelRootBaseLocalRotation;
        private bool _hasBasePose;
        private float _fallbackPhase;
        private Vector2 _blend;
        private float _speed01;
        private bool _useExternalMotion;
        private Vector2 _externalBlend;
        private float _externalSpeed01;
        private bool _usingFallback;
        private UnityPlayablesAnimationBackend _animationBackend;
        private bool _ownsAnimationBackend;
        private MxAnimationSetDefinition _animationSetDefinition;
        private CharacterAnimationBlendKind _blendKind = CharacterAnimationBlendKind.Blend2D;
        private MxAnimationBlend1DDefinition _blend1DDefinition;
        private MxAnimationBlend2DDefinition _blend2DDefinition;
        private MxAnimationBlend2DDefinition _sourceBlend2DDefinition;
        private MxAnimationBlendReachabilityReport _blendReachabilityReport;
        private MxAnimationBackendResult _lastAnimationResult;
        private MxAnimationBackendResult _lastActionAnimationResult;
        private string _lastActionKey = string.Empty;
        private int _lastQuantizedBlendX;
        private int _lastQuantizedBlendY;
        private int _blendDomainMinX = -1000;
        private int _blendDomainMaxX = 1000;
        private int _blendDomainMinY = -1000;
        private int _blendDomainMaxY = 1000;
        private readonly List<BlendPointPlaybackCalibration> _blendPointPlaybackCalibrations =
            new List<BlendPointPlaybackCalibration>();
        private int _lastPlaybackCompensationX = int.MinValue;
        private int _lastPlaybackCompensationY = int.MinValue;
        private float _lastPlaybackCompensationActualSpeed = -1f;
        private float _playbackSpeedCompensation = 1f;
        private bool _hasAppliedLocomotionBlend;
        private string _lastAppliedBlendId = string.Empty;
        private int _lastAppliedBlendParameterScale;
        private int _lastAppliedQuantizedBlendX;
        private int _lastAppliedQuantizedBlendY;
        private float _lastAppliedPlaybackSpeedCompensation = 1f;
        private float _lastBlendSampleSpeed;
        private float _lastBlendNodeSpeed;
        private float _runtimeAnimationClock;
        private float _suppressLocomotionBlendUntilTime;
        private bool _loggedAnimationBackendFailure;
        private readonly Dictionary<ResourceKey, Vector2Int> _blendPointCoordinateOverrides =
            new Dictionary<ResourceKey, Vector2Int>();
        private readonly MxAnimationBlend2DEvaluationBuffer _playbackSpeedEvaluationBuffer =
            new MxAnimationBlend2DEvaluationBuffer();
        private readonly MxAnimationBlend2DEvaluationBuffer _transitionEvaluationBuffer =
            new MxAnimationBlend2DEvaluationBuffer();
        private Animator _cachedAnimatorParameterSource;
        private RuntimeAnimatorController _cachedAnimatorController;
        private string _cachedBlendXParameter = string.Empty;
        private string _cachedBlendYParameter = string.Empty;
        private string _cachedSpeedParameter = string.Empty;
        private bool _hasBlendXAnimatorParameter;
        private bool _hasBlendYAnimatorParameter;
        private bool _hasSpeedAnimatorParameter;
        private int _blendXAnimatorParameterHash;
        private int _blendYAnimatorParameterHash;
        private int _speedAnimatorParameterHash;
        private bool _hasAppliedBlendXAnimatorParameter;
        private bool _hasAppliedBlendYAnimatorParameter;
        private bool _hasAppliedSpeedAnimatorParameter;
        private float _lastAppliedBlendXAnimatorParameter;
        private float _lastAppliedBlendYAnimatorParameter;
        private float _lastAppliedSpeedAnimatorParameter;
        private int _animatorParameterApplyCount;
        private int _animatorParameterSkippedCount;
        private bool _motionControllerLookupAttempted;
        private bool _modelRootLookupAttempted;
        private bool _animatorLookupAttempted;
        private Transform _lastAnimatorLookupRoot;
        private bool _deferPoseEvaluation;

        public Vector2 Blend => _blend;
        public float Speed01 => _speed01;
        public bool HasAnimationClips => _animationClips != null && _animationClips.Length > 0;
        public bool HasAnimationBackend => _animationBackend != null;
        public bool CanDeferPoseEvaluation => !_usingFallback && _animationBackend is IMxAnimationPoseEvaluationBackend budget
            && budget.CanDeferPoseEvaluation;
        public bool UsingFallback => _usingFallback;
        public string ActiveBlendId => _blendId;
        public CharacterAnimationBlendKind ActiveBlendKind => _blendKind;
        public MxAnimationBackendResult LastAnimationResult => _lastAnimationResult;
        public MxAnimationBackendResult LastActionAnimationResult => _lastActionAnimationResult;
        public string LastActionKey => _lastActionKey;
        public int LastQuantizedBlendX => _lastQuantizedBlendX;
        public int LastQuantizedBlendY => _lastQuantizedBlendY;
        public float PlaybackSpeedCompensation => _playbackSpeedCompensation;
        public float LastBlendSampleSpeed => _lastBlendSampleSpeed;
        public float LastBlendNodeSpeed => _lastBlendNodeSpeed;
        public bool UseExternalRuntimeTick
        {
            get => _useExternalRuntimeTick;
            set => _useExternalRuntimeTick = value;
        }

        public bool IsLocomotionBlendSuppressed => _runtimeAnimationClock < _suppressLocomotionBlendUntilTime;
        public MxAnimationBlend2DControllerDomain ActiveBlend2DControllerDomain =>
            new MxAnimationBlend2DControllerDomain(_blendDomainMinX, _blendDomainMaxX, _blendDomainMinY, _blendDomainMaxY);
        public MxAnimationBlendReachabilityReport ActiveBlendReachabilityReport =>
            _blendReachabilityReport ?? CreateReachabilityReport();

        private void Awake()
        {
            ResolveReferences();
            CaptureBasePose();
        }

        private void OnValidate()
        {
            InvalidateReferenceLookup();
        }

        private void OnTransformChildrenChanged()
        {
            InvalidateReferenceLookup();
        }

        private void OnDestroy()
        {
            ReleaseAnimationBackend();
        }

        private void LateUpdate()
        {
            if (_useExternalRuntimeTick)
                return;

            TickRuntimeAnimation(Time.deltaTime, Time.frameCount);
        }

        /// <summary>NoAlloc: use the caller's clip time on the next animation tick, preserving crossfade state.</summary>
        public bool TrySynchronizeActionPlaybackTime(double timeSeconds)
        {
            return _animationBackend is IMxAnimationTimeSynchronizedBackend synchronized
                && synchronized.TrySynchronizeCurrentClipTime(
                    MxAnimationLayerId.Base, _lastActionAnimationResult.ClipKey, timeSeconds);
        }

        /// <summary>NoAlloc. Temporary action pose for bounded presentation sampling; caller restores transform state.</summary>
        public bool TryEvaluateActionPoseAtTime(double timeSeconds)
        {
            return _animationBackend is IMxAnimationPoseSamplingBackend sampler
                && sampler.TryEvaluateClipPose(MxAnimationLayerId.Base, _lastActionAnimationResult.ClipKey, timeSeconds);
        }

        public void TickRuntimeAnimation(float deltaTime, int frameIndex)
        {
            ResolveReferences();

            if (_useExternalMotion)
            {
                float targetSpeed01 = Mathf.Max(0f, _externalSpeed01);
                bool targetIdle = targetSpeed01 <= ExternalIdleTargetThreshold &&
                    _externalBlend.sqrMagnitude <= ExternalIdleTargetThreshold;
                float blendResponse = targetIdle ? _externalBlendStopResponse : _externalBlendResponse;
                float speedResponse = targetIdle ? _externalSpeedStopResponse : _externalSpeedResponse;
                float blendFactor = CalculateResponseFactor(blendResponse, deltaTime);
                float speedFactor = CalculateResponseFactor(speedResponse, deltaTime);
                _blend = Vector2.Lerp(_blend, _externalBlend, blendFactor);
                _speed01 = Mathf.Lerp(_speed01, targetSpeed01, speedFactor);

                if (targetIdle)
                {
                    if (_blend.sqrMagnitude <= ExternalIdleSnapThreshold * ExternalIdleSnapThreshold)
                        _blend = Vector2.zero;
                    if (_speed01 <= ExternalIdleSnapThreshold)
                        _speed01 = 0f;
                }
            }
            else
            {
                if (_motionController != null)
                {
                    _blend = _motionController.LastLocalMove;
                    _speed01 = Mathf.Max(0f, _motionController.LastSpeed01);
                }
                else
                {
                    _blend = Vector2.zero;
                    _speed01 = 0f;
                }
            }

            ApplyAnimatorParameters();
            ApplyAnimationBackend(deltaTime);
            if (!_deferPoseEvaluation && !Application.isPlaying && _animator != null)
                _animator.Update(0f);
            if (!_deferPoseEvaluation) ApplyFallbackPose(deltaTime);
            _runtimeAnimationClock += Mathf.Max(0f, deltaTime);
        }

        /// <summary>NoAlloc: advance presentation clocks and transitions, retaining the last published pose.</summary>
        /// <remarks>Call EvaluateCurrentAnimationPose when due. Returns false without ticking if unsupported.</remarks>
        public bool TryAdvanceRuntimeAnimationState(float deltaTime, int frameIndex)
        {
            if (!CanDeferPoseEvaluation) return false;
            _deferPoseEvaluation = true;
            try { TickRuntimeAnimation(deltaTime, frameIndex); }
            finally { _deferPoseEvaluation = false; }
            return true;
        }

        public void Configure(
            Transform modelRoot,
            Animator animator,
            AnimationClip[] animationClips)
        {
            _modelRoot = modelRoot;
            _animator = animator;
            _animationClips = animationClips ?? Array.Empty<AnimationClip>();
            InvalidateReferenceLookup();
            InvalidateAnimatorParameterCache();
            CaptureBasePose();
        }

        public void SetExternalMotion(Vector2 blend, float speed01)
        {
            _useExternalMotion = true;
            _externalBlend = Vector2.ClampMagnitude(blend, 1f);
            _externalSpeed01 = Mathf.Max(0f, speed01);
        }

        public void ClearExternalMotion()
        {
            _useExternalMotion = false;
            _externalBlend = Vector2.zero;
            _externalSpeed01 = 0f;
        }

        public void ConfigureDirectClipBlendPoints(DirectClipBlendPoint[] points)
        {
            _directClipBlendPoints = points ?? Array.Empty<DirectClipBlendPoint>();
            InvalidateAppliedLocomotionBlend();
        }

        public bool TryConfigureDirectClipAnimationBackend(string actorId = "")
        {
            if (_animationBackend != null)
                return true;

            ResolveReferences();
            if (_animator == null || _animationClips == null || _animationClips.Length == 0)
                return false;

            AnimationClip idle = FindClipByTokens(_animationClips, "idle") ?? FindFirstClip(_animationClips);
            if (idle == null)
                return false;

            var provider = new MemoryResourceProvider();
            var entries = new List<ResourceCatalogEntry>();
            var points = new List<MxAnimationBlend2DPoint>();
            int nextClipIndex = 0;

            ResourceKey idleKey = TryAddConfiguredDirectClipPoints(
                points,
                provider,
                entries,
                ref nextClipIndex);
            if (!idleKey.IsValid)
            {
                idleKey = RegisterDirectClip(provider, entries, idle, ref nextClipIndex);
                points.Add(new MxAnimationBlend2DPoint(0, 0, idleKey));

                AddDirectionalDirectClip(points, provider, entries, _animationClips, "forward", 0, 450, 0, 650, ref nextClipIndex);
                AddDirectionalDirectClip(points, provider, entries, _animationClips, "back", 0, -350, 0, -400, ref nextClipIndex);
                AddDirectionalDirectClip(points, provider, entries, _animationClips, "left", -350, 0, -650, 0, ref nextClipIndex);
                AddDirectionalDirectClip(points, provider, entries, _animationClips, "right", 350, 0, 650, 0, ref nextClipIndex);
            }

            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog(
                "character.runtime.direct-clips",
                string.Empty,
                entries));

            var blend = new MxAnimationBlend2DDefinition(
                _blendId,
                _blendXParameter,
                _blendYParameter,
                MxAnimationLayerId.Base,
                points,
                Mathf.Max(1, _blendParameterScale),
                Mathf.Max(1, _blendParameterScale));
            var definition = new MxAnimationSetDefinition(
                "character.runtime.direct-clips",
                1,
                idleKey,
                idleKey,
                blend2DDefinitions: new[] { blend });
            var backend = new UnityPlayablesAnimationBackend(
                _animator,
                manager,
                definition,
                string.IsNullOrWhiteSpace(actorId) ? gameObject.name : actorId);

            ConfigureAnimationBackend(backend, blend, definition, ownsBackend: true);
            return true;
        }

        public bool TryConfigureResourceAnimationBackend(
            IResourceManager resourceManager,
            CharacterRuntimeLocomotionDefinition locomotionDefinition,
            string actorId = "")
        {
            ResolveReferences();
            if (_animator == null || resourceManager == null || !locomotionDefinition.IsValid)
                return false;

            var backend = new UnityPlayablesAnimationBackend(
                _animator,
                resourceManager,
                locomotionDefinition.AnimationSet,
                string.IsNullOrWhiteSpace(actorId) ? gameObject.name : actorId);
            ConfigureAnimationBackend(backend, locomotionDefinition, ownsBackend: true);
            return true;
        }

        public bool TryCrossFadeAction(
            int bindingId,
            float fadeDurationSeconds,
            string correlationId = "")
        {
            return TryCrossFadeAction(
                bindingId,
                fadeDurationSeconds,
                targetStartOffsetSeconds: 0f,
                playbackSpeed: 1f,
                correlationId: correlationId);
        }

        public bool TryCrossFadeAction(
            int bindingId,
            float fadeDurationSeconds,
            float targetStartOffsetSeconds,
            float playbackSpeed = 1f,
            string correlationId = "",
            bool allowFallback = true)
        {
            if (bindingId <= 0 || _animationBackend == null)
                return false;

            if (_animationSetDefinition != null && _animationSetDefinition.TryFindPreparedClipBinding(bindingId, out MxAnimationActionBinding direct))
                return TryCrossFadeClip(direct.Clip, fadeDurationSeconds, targetStartOffsetSeconds, playbackSpeed, correlationId, allowFallback);

            if (_animationSetDefinition == null
                || !_animationSetDefinition.TryFindBinding(bindingId, out MxAnimationActionBinding binding))
                return false;

            _lastActionAnimationResult = _animationBackend.CrossFade(new MxAnimationCrossFadeRequest
            {
                BindingId = bindingId,
                AllowFallback = allowFallback,
                LayerId = MxAnimationLayerId.Base,
                FadeDurationSeconds = Mathf.Max(0f, fadeDurationSeconds),
                TargetStartOffsetSeconds = Mathf.Max(0f, targetStartOffsetSeconds),
                PlaybackSpeed = playbackSpeed <= 0.0001f ? 1f : playbackSpeed,
                CorrelationId = string.IsNullOrWhiteSpace(correlationId)
                    ? "character-runtime-action"
                    : correlationId
            });

            if (_lastActionAnimationResult.Success)
            {
                _lastActionKey = binding.ActionKey;
                InvalidateAppliedLocomotionBlend();
            }

            return _lastActionAnimationResult.Success;
        }

        public bool HasActionBinding(int bindingId)
        {
            return bindingId > 0
                && _animationSetDefinition != null
                && _animationSetDefinition.TryFindBinding(bindingId, out _);
        }

        /// <summary>LowFreqAlloc: resolves the effective one-shot duration without retaining a new resource handle.</summary>
        public bool TryGetActionPlaybackDuration(int bindingId, out float durationSeconds)
        {
            durationSeconds = 0f;
            if (!HasActionBinding(bindingId)
                || !_animationSetDefinition.TryFindBinding(
                    bindingId,
                    out MxAnimationActionBinding binding)
                || _animationBackend == null
                || !_animationBackend.TryGetClipDuration(binding.Clip, out float clipDuration))
            {
                return false;
            }

            float playbackSpeed = binding.PlaybackSpeed;
            if (playbackSpeed <= 0.0001f || float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed))
                playbackSpeed = 1f;
            durationSeconds = clipDuration / playbackSpeed;
            return durationSeconds > 0f && !float.IsNaN(durationSeconds) && !float.IsInfinity(durationSeconds);
        }

        /// <summary>NoAlloc after controller initialization; evaluates the configured 2D blend at the supplied input.</summary>
        public MxAnimationSetDefinition AnimationSet => _animationSetDefinition;

        public MxAnimationBlend2DDefinition LocomotionBlend2DDefinition => _sourceBlend2DDefinition;

        public bool TryResolveDominantLocomotionClip(Vector2 blend, out ResourceKey clipKey) =>
            TryResolveDominantLocomotionPoint(blend, out clipKey, out _, out _);

        public bool TryResolveDominantLocomotionPoint(Vector2 blend, out ResourceKey clipKey, out int pointX, out int pointY)
        {
            pointX = 0;
            pointY = 0;
            clipKey = default;
            if (_blendKind != CharacterAnimationBlendKind.Blend2D
                || _blend2DDefinition == null
                || _blend2DDefinition.Points.Count == 0)
            {
                return false;
            }

            Vector2 clamped = Vector2.ClampMagnitude(blend, 1f);
            int scale = Math.Max(1, _blendParameterScale);
            int x = QuantizeBlendAxis(clamped.x, scale, _blendDomainMinX, _blendDomainMaxX);
            int y = QuantizeBlendAxis(clamped.y, scale, _blendDomainMinY, _blendDomainMaxY);
            MxAnimationBlend2DCalculator.EvaluatePrepared(
                _blend2DDefinition, x, y,
                _transitionEvaluationBuffer);

            float greatestWeight = 0f;
            IReadOnlyList<MxAnimationBlend2DWeight> weights = _transitionEvaluationBuffer.Weights;
            for (int i = 0; i < weights.Count; i++)
            {
                MxAnimationBlend2DWeight weight = weights[i];
                if (!weight.ClipKey.IsValid
                    || weight.Weight <= greatestWeight
                    || (weight.X == 0 && weight.Y == 0))
                {
                    continue;
                }

                greatestWeight = weight.Weight;
                clipKey = weight.ClipKey;
                pointX = weight.X;
                pointY = weight.Y;
            }

            return clipKey.IsValid;
        }

        public bool TryCrossFadeAction(
            int bindingId,
            float fadeDurationSeconds,
            float targetStartOffsetSeconds,
            float playbackSpeed,
            float suppressLocomotionBlendSeconds,
            string correlationId = "",
            bool allowFallback = true)
        {
            bool accepted = TryCrossFadeAction(
                bindingId,
                fadeDurationSeconds,
                targetStartOffsetSeconds,
                playbackSpeed,
                correlationId,
                allowFallback);
            if (accepted)
                SuppressLocomotionBlendForSeconds(suppressLocomotionBlendSeconds);
            return accepted;
        }

        public bool TryCrossFadeClip(ResourceKey clipKey, float fadeDurationSeconds, string correlationId = "")
        {
            return TryCrossFadeClip(
                clipKey,
                fadeDurationSeconds,
                targetStartOffsetSeconds: 0f,
                playbackSpeed: 1f,
                correlationId: correlationId);
        }

        public bool TryCrossFadeClip(
            ResourceKey clipKey,
            float fadeDurationSeconds,
            float targetStartOffsetSeconds,
            float playbackSpeed = 1f,
            string correlationId = "",
            bool allowFallback = true)
        {
            if (!clipKey.IsValid || _animationBackend == null)
                return false;

            _lastActionAnimationResult = _animationBackend.CrossFade(new MxAnimationCrossFadeRequest
            {
                ClipKey = clipKey,
                AllowFallback = allowFallback,
                LayerId = MxAnimationLayerId.Base,
                FadeDurationSeconds = Mathf.Max(0f, fadeDurationSeconds),
                TargetStartOffsetSeconds = Mathf.Max(0f, targetStartOffsetSeconds),
                PlaybackSpeed = playbackSpeed <= 0.0001f ? 1f : playbackSpeed,
                CorrelationId = string.IsNullOrWhiteSpace(correlationId)
                    ? "character-runtime-clip:" + clipKey.Id
                    : correlationId
            });

            if (_lastActionAnimationResult.Success)
            {
                _lastActionKey = clipKey.Id;
                InvalidateAppliedLocomotionBlend();
            }

            return _lastActionAnimationResult.Success;
        }

        /// <summary>Preparation only: resolves a source clip address against the configured package before playback.</summary>
        public bool TryPrepareClipKeyForPlayback(string clipId, out ResourceKey clipKey)
        {
            clipKey = ResolveConfiguredClipKey(clipId);
            return clipKey.IsValid;
        }

        private ResourceKey ResolveConfiguredClipKey(string clipId)
        {
            if (string.IsNullOrWhiteSpace(clipId) || _animationSetDefinition == null)
                return new ResourceKey(clipId, ResourceTypeIds.AnimationClip);

            if (MatchesClipId(_animationSetDefinition.DefaultClip, clipId))
                return _animationSetDefinition.DefaultClip;
            if (MatchesClipId(_animationSetDefinition.FallbackClip, clipId))
                return _animationSetDefinition.FallbackClip;

            for (int i = 0; i < _animationSetDefinition.Actions.Count; i++)
            {
                MxAnimationActionBinding action = _animationSetDefinition.Actions[i];
                if (action != null && MatchesClipId(action.Clip, clipId))
                    return action.Clip;
            }

            for (int i = 0; i < _animationSetDefinition.Blend2DDefinitions.Count; i++)
            {
                MxAnimationBlend2DDefinition blend = _animationSetDefinition.Blend2DDefinitions[i];
                IReadOnlyList<MxAnimationBlend2DPoint> points = blend?.Points;
                for (int pointIndex = 0; points != null && pointIndex < points.Count; pointIndex++)
                {
                    MxAnimationBlend2DPoint point = points[pointIndex];
                    if (point != null && MatchesClipId(point.ClipKey, clipId))
                        return point.ClipKey;
                }
            }

            return new ResourceKey(clipId, ResourceTypeIds.AnimationClip);
        }

        private static bool MatchesClipId(ResourceKey key, string clipId)
        {
            return key.IsValid && string.Equals(key.Id, clipId, StringComparison.Ordinal);
        }

        public bool TryCrossFadeClip(
            ResourceKey clipKey,
            float fadeDurationSeconds,
            float targetStartOffsetSeconds,
            float playbackSpeed,
            float suppressLocomotionBlendSeconds,
            string correlationId = "")
        {
            bool accepted = TryCrossFadeClip(
                clipKey,
                fadeDurationSeconds,
                targetStartOffsetSeconds,
                playbackSpeed,
                correlationId);
            if (accepted)
                SuppressLocomotionBlendForSeconds(suppressLocomotionBlendSeconds);
            return accepted;
        }

        public void SuppressLocomotionBlendForSeconds(float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
                return;

            _suppressLocomotionBlendUntilTime = Mathf.Max(
                _suppressLocomotionBlendUntilTime,
                _runtimeAnimationClock + seconds);
            InvalidateAppliedLocomotionBlend();
        }

        public void ResumeLocomotionBlend()
        {
            _suppressLocomotionBlendUntilTime = 0f;
            _lastActionKey = string.Empty;
            InvalidateAppliedLocomotionBlend();
        }

        public void ConfigureAnimationBackend(
            UnityPlayablesAnimationBackend backend,
            MxAnimationBlend2DDefinition blendDefinition,
            MxAnimationSetDefinition animationSetDefinition = null,
            bool ownsBackend = true)
        {
            ConfigureAnimationBackend(
                backend,
                new CharacterRuntimeLocomotionDefinition(
                    animationSetDefinition,
                    CharacterAnimationBlendKind.Blend2D,
                    null,
                    blendDefinition),
                ownsBackend);
        }

        public void ConfigureAnimationBackend(
            UnityPlayablesAnimationBackend backend,
            CharacterRuntimeLocomotionDefinition locomotionDefinition,
            bool ownsBackend = true)
        {
            ReleaseAnimationBackend();
            _animationBackend = backend;
            _ownsAnimationBackend = ownsBackend;
            _animationSetDefinition = locomotionDefinition.AnimationSet;
            _blendKind = locomotionDefinition.BlendKind;
            _loggedAnimationBackendFailure = false;
            InvalidateReferenceLookup();
            _lastAnimationResult = default;
            _lastQuantizedBlendX = 0;
            _lastQuantizedBlendY = 0;
            _lastPlaybackCompensationX = int.MinValue;
            _lastPlaybackCompensationY = int.MinValue;
            _lastPlaybackCompensationActualSpeed = -1f;
            _playbackSpeedCompensation = 1f;
            InvalidateAppliedLocomotionBlend();
            _lastBlendSampleSpeed = 0f;
            _lastBlendNodeSpeed = 0f;
            _blend1DDefinition = locomotionDefinition.Blend1D;
            _sourceBlend2DDefinition = locomotionDefinition.Blend2D;
            _blend2DDefinition = locomotionDefinition.Blend2D;
            _blendReachabilityReport = null;
            _suppressLocomotionBlendUntilTime = 0f;
            _blendPointPlaybackCalibrations.Clear();
            _blendPointCoordinateOverrides.Clear();

            if (!locomotionDefinition.IsValid)
            {
                ResetBlendDomain();
                return;
            }

            _blendId = locomotionDefinition.BlendId;
            if (_blendKind == CharacterAnimationBlendKind.Blend1D)
            {
                _blendParameterScale = Math.Max(1, _blend1DDefinition.ParameterScale);
            }
            else
            {
                _blendXParameter = _blend2DDefinition.ParameterXId;
                _blendYParameter = _blend2DDefinition.ParameterYId;
                _blendParameterScale = Mathf.Max(
                    1,
                    Math.Max(_blend2DDefinition.ParameterXScale, _blend2DDefinition.ParameterYScale));
            }
            InvalidateAnimatorParameterCache();
            if (_animationBackend != null && _blend2DDefinition != null)
                RebuildBlendDomain(_blend2DDefinition);
            else
                ResetBlendDomain();
            if (_blend2DDefinition != null)
            {
                RebuildBlendPointPlaybackCalibrations(_blend2DDefinition);
                _blendReachabilityReport = CreateReachabilityReport();
            }
        }

        public MxAnimationDiagnosticSnapshot CreateAnimationSnapshot()
        {
            return _animationBackend != null ? _animationBackend.CreateSnapshot() : null;
        }

        /// <summary>AllocByDesign: captures controller and backend playback state for rollback or preview.</summary>
        public CharacterRuntimeAnimationPlaybackState CapturePlaybackState()
        {
            MxAnimationPlaybackState backendState =
                (_animationBackend as IMxAnimationRewindableBackend)?.CapturePlaybackState();
            return new CharacterRuntimeAnimationPlaybackState(
                backendState,
                _fallbackPhase,
                _blend,
                _speed01,
                _useExternalMotion,
                _externalBlend,
                _externalSpeed01,
                _usingFallback,
                _lastActionKey,
                _lastQuantizedBlendX,
                _lastQuantizedBlendY,
                _playbackSpeedCompensation,
                _hasAppliedLocomotionBlend,
                _lastAppliedBlendId,
                _lastAppliedBlendParameterScale,
                _lastAppliedQuantizedBlendX,
                _lastAppliedQuantizedBlendY,
                _lastAppliedPlaybackSpeedCompensation,
                _runtimeAnimationClock,
                _suppressLocomotionBlendUntilTime);
        }

        /// <summary>LowFreqAlloc: restores animation playback without advancing presentation time.</summary>
        public bool RestorePlaybackState(
            CharacterRuntimeAnimationPlaybackState state,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (state == null)
            {
                diagnostic = "Animation playback state is missing.";
                return false;
            }

            ResolveReferences();
            _fallbackPhase = state.FallbackPhase;
            _blend = state.Blend;
            _speed01 = state.Speed01;
            _useExternalMotion = state.UseExternalMotion;
            _externalBlend = state.ExternalBlend;
            _externalSpeed01 = state.ExternalSpeed01;
            _usingFallback = state.UsingFallback;
            _lastActionKey = state.LastActionKey;
            _lastQuantizedBlendX = state.LastQuantizedBlendX;
            _lastQuantizedBlendY = state.LastQuantizedBlendY;
            _playbackSpeedCompensation = state.PlaybackSpeedCompensation;
            _hasAppliedLocomotionBlend = state.HasAppliedLocomotionBlend;
            _lastAppliedBlendId = state.LastAppliedBlendId;
            _lastAppliedBlendParameterScale = state.LastAppliedBlendParameterScale;
            _lastAppliedQuantizedBlendX = state.LastAppliedQuantizedBlendX;
            _lastAppliedQuantizedBlendY = state.LastAppliedQuantizedBlendY;
            _lastAppliedPlaybackSpeedCompensation = state.LastAppliedPlaybackSpeedCompensation;
            _runtimeAnimationClock = state.RuntimeAnimationClock;
            _suppressLocomotionBlendUntilTime = state.SuppressLocomotionBlendUntilTime;
            _hasAppliedBlendXAnimatorParameter = false;
            _hasAppliedBlendYAnimatorParameter = false;
            _hasAppliedSpeedAnimatorParameter = false;
            ApplyAnimatorParameters();

            if (state.BackendState != null)
            {
                if (!(_animationBackend is IMxAnimationRewindableBackend rewindable))
                {
                    diagnostic = "Animation backend does not support playback restore.";
                    return false;
                }

                MxAnimationBackendResult result = rewindable.RestorePlaybackState(state.BackendState);
                if (!result.Success)
                {
                    diagnostic = result.Message;
                    return false;
                }
            }

            ApplyFallbackPose(0f);
            return true;
        }

        /// <summary>NoAlloc: evaluates the restored graph without advancing time.</summary>
        public void EvaluateCurrentAnimationPose()
        {
            if (_animationBackend is IMxAnimationRewindableBackend rewindable)
                rewindable.EvaluateCurrentPose();
            ApplyFallbackPose(0f);
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

        public bool SetBlendPointCoordinateOverride(ResourceKey clipKey, float x, float y)
        {
            if (!clipKey.IsValid || _sourceBlend2DDefinition == null || _animationBackend == null)
                return false;

            int scale = Math.Max(1, _blendParameterScale);
            int quantizedX = Mathf.RoundToInt(x * scale);
            int quantizedY = Mathf.RoundToInt(y * scale);
            if (!_animationBackend.SetBlend2DPointCoordinateOverride(_blendId, clipKey, quantizedX, quantizedY))
                return false;

            _blendPointCoordinateOverrides[clipKey] = new Vector2Int(quantizedX, quantizedY);
            RebuildRuntimeBlendDefinition();
            InvalidateAppliedLocomotionBlend();
            return true;
        }

        public bool TryGetBlendPointCoordinate(ResourceKey clipKey, out Vector2 coordinate)
        {
            coordinate = default;
            if (!clipKey.IsValid || _blend2DDefinition == null)
                return false;

            for (int i = 0; i < _blend2DDefinition.Points.Count; i++)
            {
                MxAnimationBlend2DPoint point = _blend2DDefinition.Points[i];
                if (point != null && point.ClipKey == clipKey)
                {
                    float scale = Math.Max(1, _blendParameterScale);
                    coordinate = new Vector2(point.X / scale, point.Y / scale);
                    return true;
                }
            }

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

        private static void AddDirectionalDirectClip(
            List<MxAnimationBlend2DPoint> points,
            MemoryResourceProvider provider,
            List<ResourceCatalogEntry> entries,
            AnimationClip[] clips,
            string directionToken,
            int walkX,
            int walkY,
            int runX,
            int runY,
            ref int nextClipIndex)
        {
            AnimationClip walk = FindClipByTokens(clips, "walk", directionToken);
            AnimationClip run = FindClipByTokens(clips, "run", directionToken);
            AnimationClip fallback = FindClipByTokens(clips, directionToken);

            if (walk != null)
            {
                ResourceKey walkKey = RegisterDirectClip(provider, entries, walk, ref nextClipIndex);
                points.Add(new MxAnimationBlend2DPoint(walkX, walkY, walkKey));
            }

            AnimationClip far = run ?? fallback ?? walk;
            if (far == null || ReferenceEquals(far, walk))
                return;

            ResourceKey farKey = RegisterDirectClip(provider, entries, far, ref nextClipIndex);
            points.Add(new MxAnimationBlend2DPoint(runX, runY, farKey));
        }

        private ResourceKey TryAddConfiguredDirectClipPoints(
            List<MxAnimationBlend2DPoint> points,
            MemoryResourceProvider provider,
            List<ResourceCatalogEntry> entries,
            ref int nextClipIndex)
        {
            if (_directClipBlendPoints == null || _directClipBlendPoints.Length == 0)
                return default;

            ResourceKey firstKey = default;
            ResourceKey idleKey = default;
            int scale = Mathf.Max(1, _blendParameterScale);
            for (int i = 0; i < _directClipBlendPoints.Length; i++)
            {
                DirectClipBlendPoint point = _directClipBlendPoints[i];
                if (point == null || string.IsNullOrWhiteSpace(point.SourceClipName))
                    continue;

                AnimationClip clip = FindClipByExactName(_animationClips, point.SourceClipName);
                if (clip == null)
                    continue;

                ResourceKey key = RegisterDirectClip(provider, entries, clip, ref nextClipIndex);
                if (!firstKey.IsValid)
                    firstKey = key;
                int x = Mathf.RoundToInt(point.X * scale);
                int y = Mathf.RoundToInt(point.Y * scale);
                if (x == 0 && y == 0)
                    idleKey = key;
                points.Add(new MxAnimationBlend2DPoint(
                    x,
                    y,
                    key,
                    point.PlaybackSpeed,
                    point.Loop));
            }

            return idleKey.IsValid ? idleKey : firstKey;
        }

        private static ResourceKey RegisterDirectClip(
            MemoryResourceProvider provider,
            List<ResourceCatalogEntry> entries,
            AnimationClip clip,
            ref int nextClipIndex)
        {
            string id = "character.runtime.direct_clip." + nextClipIndex.ToString();
            string address = "direct/" + id;
            nextClipIndex++;
            provider.Register(address, clip);
            entries.Add(new ResourceCatalogEntry(
                id,
                ResourceTypeIds.AnimationClip,
                provider.ProviderId,
                address));
            return new ResourceKey(id, ResourceTypeIds.AnimationClip);
        }

        private static AnimationClip FindClipByTokens(AnimationClip[] clips, params string[] tokens)
        {
            for (int i = 0; clips != null && i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                    continue;

                string name = clip.name ?? string.Empty;
                bool matches = true;
                for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                {
                    string token = tokens[tokenIndex];
                    if (string.IsNullOrWhiteSpace(token))
                        continue;

                    if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    return clip;
            }

            return null;
        }

        private static AnimationClip FindClipByExactName(AnimationClip[] clips, string clipName)
        {
            for (int i = 0; clips != null && i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null && string.Equals(clip.name, clipName, StringComparison.Ordinal))
                    return clip;
            }

            return null;
        }

        private static AnimationClip FindFirstClip(AnimationClip[] clips)
        {
            for (int i = 0; clips != null && i < clips.Length; i++)
            {
                if (clips[i] != null)
                    return clips[i];
            }

            return null;
        }

        private static float CalculateResponseFactor(float response, float deltaTime)
        {
            if (response <= 0f)
                return 1f;
            if (deltaTime <= 0f)
                return 0f;

            return 1f - Mathf.Exp(-response * deltaTime);
        }

        private void ResolveReferences()
        {
            if (_motionController == null && !_motionControllerLookupAttempted)
            {
                _motionControllerLookupAttempted = true;
                TryGetComponent(out _motionController);
            }

            if (_modelRoot == null && !_modelRootLookupAttempted)
            {
                _modelRootLookupAttempted = true;
                _modelRoot = transform.Find("ModelRoot");
            }

            if (_animator == null
                && _modelRoot != null
                && (!_animatorLookupAttempted || !ReferenceEquals(_lastAnimatorLookupRoot, _modelRoot)))
            {
                _animatorLookupAttempted = true;
                _lastAnimatorLookupRoot = _modelRoot;
                Animator resolvedAnimator = _modelRoot.GetComponentInChildren<Animator>(includeInactive: true);
                if (!ReferenceEquals(_animator, resolvedAnimator))
                {
                    _animator = resolvedAnimator;
                    InvalidateAnimatorParameterCache();
                }
            }
        }

        private void InvalidateReferenceLookup()
        {
            _motionControllerLookupAttempted = _motionController != null;
            _modelRootLookupAttempted = _modelRoot != null;
            _animatorLookupAttempted = _animator != null;
            _lastAnimatorLookupRoot = _modelRoot;
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
            {
                InvalidateAnimatorParameterValues();
                return;
            }

            EnsureAnimatorParameterCache();
            if (_hasBlendXAnimatorParameter)
                ApplyAnimatorFloatParameter(
                    _blendXAnimatorParameterHash,
                    _blend.x,
                    ref _hasAppliedBlendXAnimatorParameter,
                    ref _lastAppliedBlendXAnimatorParameter);
            if (_hasBlendYAnimatorParameter)
                ApplyAnimatorFloatParameter(
                    _blendYAnimatorParameterHash,
                    _blend.y,
                    ref _hasAppliedBlendYAnimatorParameter,
                    ref _lastAppliedBlendYAnimatorParameter);
            if (_hasSpeedAnimatorParameter)
                ApplyAnimatorFloatParameter(
                    _speedAnimatorParameterHash,
                    _speed01,
                    ref _hasAppliedSpeedAnimatorParameter,
                    ref _lastAppliedSpeedAnimatorParameter);
        }

        private void ApplyAnimatorFloatParameter(
            int parameterHash,
            float value,
            ref bool hasApplied,
            ref float lastAppliedValue)
        {
            if (hasApplied && lastAppliedValue == value)
            {
                _animatorParameterSkippedCount++;
                return;
            }

            _animator.SetFloat(parameterHash, value);
            hasApplied = true;
            lastAppliedValue = value;
            _animatorParameterApplyCount++;
        }

        private void ApplyAnimationBackend(float deltaTime)
        {
            if (_animationBackend == null)
                return;

            if (string.IsNullOrWhiteSpace(_blendId))
            {
                TickAnimationBackend(deltaTime);
                return;
            }

            if (IsLocomotionBlendSuppressed)
            {
                TickAnimationBackend(deltaTime);
                return;
            }

            int scale = Math.Max(1, _blendParameterScale);
            if (_blendKind == CharacterAnimationBlendKind.Blend1D)
            {
                IReadOnlyList<MxAnimationBlend1DPoint> points = _blend1DDefinition?.Points;
                if (points == null || points.Count == 0)
                {
                    TickAnimationBackend(deltaTime);
                    return;
                }

                int minimum = points[0].Threshold;
                int maximum = points[points.Count - 1].Threshold;
                _lastQuantizedBlendX = Mathf.RoundToInt(Mathf.Lerp(minimum, maximum, Mathf.Clamp01(_speed01)));
                _lastQuantizedBlendY = 0;
                _playbackSpeedCompensation = 1f;
            }
            else
            {
                int rawQuantizedBlendX = QuantizeBlendAxisUnclamped(_blend.x, scale);
                int rawQuantizedBlendY = QuantizeBlendAxisUnclamped(_blend.y, scale);
                _lastQuantizedBlendX = QuantizeBlendAxis(_blend.x, scale, _blendDomainMinX, _blendDomainMaxX);
                _lastQuantizedBlendY = QuantizeBlendAxis(_blend.y, scale, _blendDomainMinY, _blendDomainMaxY);
                _playbackSpeedCompensation = CalculatePlaybackSpeedCompensation(
                    rawQuantizedBlendX,
                    rawQuantizedBlendY,
                    _lastQuantizedBlendX,
                    _lastQuantizedBlendY,
                    scale);
            }
            if (ShouldApplyLocomotionBlend(scale))
            {
                _lastAnimationResult = _blendKind == CharacterAnimationBlendKind.Blend1D
                    ? _animationBackend.SetBlend1D(new MxAnimationBlend1DRequest
                    {
                        BlendId = _blendId,
                        Parameter = new MxAnimationQuantizedParameter(
                            _blend1DDefinition.ParameterId,
                            _lastQuantizedBlendX,
                            scale),
                        CorrelationId = LocomotionBlendCorrelationId
                    })
                    : _animationBackend.SetBlend2D(new MxAnimationBlend2DRequest
                    {
                        BlendId = _blendId,
                        ParameterX = new MxAnimationQuantizedParameter(_blendXParameter, _lastQuantizedBlendX, scale),
                        ParameterY = new MxAnimationQuantizedParameter(_blendYParameter, _lastQuantizedBlendY, scale),
                        PlaybackSpeedMultiplier = _playbackSpeedCompensation,
                        CorrelationId = LocomotionBlendCorrelationId
                    });
                if (_lastAnimationResult.Success)
                {
                    RememberAppliedLocomotionBlend(scale);
                }
                else if (!_loggedAnimationBackendFailure)
                {
                    _loggedAnimationBackendFailure = true;
                    Debug.LogWarning(
                        "MxFramework Character: locomotion animation request failed. character="
                        + gameObject.name
                        + " blendId=" + _blendId
                        + " blendKind=" + _blendKind
                        + " code=" + _lastAnimationResult.Code
                        + " clip=" + _lastAnimationResult.ClipKey
                        + " message=" + _lastAnimationResult.Message
                        + FormatResourceError(_lastAnimationResult.ResourceError),
                        this);
                }
            }

            TickAnimationBackend(deltaTime);
        }

        private void TickAnimationBackend(float deltaTime)
        {
            float safeDelta = Mathf.Max(0f, deltaTime);
            if (_deferPoseEvaluation && _animationBackend is IMxAnimationPoseEvaluationBackend budget)
                budget.AdvancePresentationState(safeDelta);
            else _animationBackend.Tick(safeDelta);
        }

        private bool ShouldApplyLocomotionBlend(int scale)
        {
            return !_hasAppliedLocomotionBlend
                || _lastAppliedBlendParameterScale != scale
                || _lastAppliedQuantizedBlendX != _lastQuantizedBlendX
                || _lastAppliedQuantizedBlendY != _lastQuantizedBlendY
                || Mathf.Abs(_lastAppliedPlaybackSpeedCompensation - _playbackSpeedCompensation) > 0.0001f
                || !string.Equals(_lastAppliedBlendId, _blendId, StringComparison.Ordinal);
        }

        private void RememberAppliedLocomotionBlend(int scale)
        {
            _hasAppliedLocomotionBlend = true;
            _lastAppliedBlendId = _blendId;
            _lastAppliedBlendParameterScale = scale;
            _lastAppliedQuantizedBlendX = _lastQuantizedBlendX;
            _lastAppliedQuantizedBlendY = _lastQuantizedBlendY;
            _lastAppliedPlaybackSpeedCompensation = _playbackSpeedCompensation;
        }

        private void ApplyFallbackPose(float deltaTime)
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

            _fallbackPhase += Mathf.Max(0f, deltaTime) * Mathf.Max(0.1f, _fallbackBobRate) * Mathf.Lerp(0.65f, 1.35f, _speed01);
            float bob = Mathf.Sin(_fallbackPhase) * _fallbackBobAmplitude * _speed01;
            Quaternion lean = Quaternion.Euler(_blend.y * _fallbackLeanDegrees, 0f, -_blend.x * _fallbackLeanDegrees);
            _modelRoot.localPosition = _modelRootBaseLocalPosition + new Vector3(0f, bob, 0f);
            _modelRoot.localRotation = _modelRootBaseLocalRotation * lean;
        }

        private void EnsureAnimatorParameterCache()
        {
            string blendXParameter = _blendXParameter ?? string.Empty;
            string blendYParameter = _blendYParameter ?? string.Empty;
            string speedParameter = _speedParameter ?? string.Empty;
            RuntimeAnimatorController animatorController =
                _animator != null ? _animator.runtimeAnimatorController : null;

            if (ReferenceEquals(_cachedAnimatorParameterSource, _animator)
                && ReferenceEquals(_cachedAnimatorController, animatorController)
                && string.Equals(_cachedBlendXParameter, blendXParameter, StringComparison.Ordinal)
                && string.Equals(_cachedBlendYParameter, blendYParameter, StringComparison.Ordinal)
                && string.Equals(_cachedSpeedParameter, speedParameter, StringComparison.Ordinal))
            {
                return;
            }

            _cachedAnimatorParameterSource = _animator;
            _cachedAnimatorController = animatorController;
            _cachedBlendXParameter = blendXParameter;
            _cachedBlendYParameter = blendYParameter;
            _cachedSpeedParameter = speedParameter;
            _hasBlendXAnimatorParameter = false;
            _hasBlendYAnimatorParameter = false;
            _hasSpeedAnimatorParameter = false;
            _blendXAnimatorParameterHash = 0;
            _blendYAnimatorParameterHash = 0;
            _speedAnimatorParameterHash = 0;
            InvalidateAnimatorParameterValues();

            if (_animator == null || animatorController == null)
                return;

            AnimatorControllerParameter[] parameters = _animator.parameters;
            CacheAnimatorFloatParameter(
                parameters,
                blendXParameter,
                out _blendXAnimatorParameterHash,
                out _hasBlendXAnimatorParameter);
            CacheAnimatorFloatParameter(
                parameters,
                blendYParameter,
                out _blendYAnimatorParameterHash,
                out _hasBlendYAnimatorParameter);
            CacheAnimatorFloatParameter(
                parameters,
                speedParameter,
                out _speedAnimatorParameterHash,
                out _hasSpeedAnimatorParameter);
        }

        private static void CacheAnimatorFloatParameter(
            AnimatorControllerParameter[] parameters,
            string parameterName,
            out int parameterHash,
            out bool hasParameter)
        {
            parameterHash = 0;
            hasParameter = false;
            if (string.IsNullOrWhiteSpace(parameterName) || parameters == null)
                return;

            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Float
                    && string.Equals(parameter.name, parameterName, StringComparison.Ordinal))
                {
                    parameterHash = Animator.StringToHash(parameterName);
                    hasParameter = true;
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
            _animationSetDefinition = null;
            _blendKind = CharacterAnimationBlendKind.None;
            _blend1DDefinition = null;
            _blend2DDefinition = null;
            _sourceBlend2DDefinition = null;
            _blendReachabilityReport = null;
            _blendPointCoordinateOverrides.Clear();
            InvalidateAppliedLocomotionBlend();
        }

        private void InvalidateAnimatorParameterCache()
        {
            _cachedAnimatorParameterSource = null;
            _cachedAnimatorController = null;
            _cachedBlendXParameter = string.Empty;
            _cachedBlendYParameter = string.Empty;
            _cachedSpeedParameter = string.Empty;
            _hasBlendXAnimatorParameter = false;
            _hasBlendYAnimatorParameter = false;
            _hasSpeedAnimatorParameter = false;
            _blendXAnimatorParameterHash = 0;
            _blendYAnimatorParameterHash = 0;
            _speedAnimatorParameterHash = 0;
            InvalidateAnimatorParameterValues();
        }

        private void InvalidateAppliedLocomotionBlend()
        {
            _hasAppliedLocomotionBlend = false;
            _lastAppliedBlendId = string.Empty;
            _lastAppliedBlendParameterScale = 0;
            _lastAppliedQuantizedBlendX = 0;
            _lastAppliedQuantizedBlendY = 0;
            _lastAppliedPlaybackSpeedCompensation = 1f;
        }

        private void InvalidateAnimatorParameterValues()
        {
            _hasAppliedBlendXAnimatorParameter = false;
            _hasAppliedBlendYAnimatorParameter = false;
            _hasAppliedSpeedAnimatorParameter = false;
            _lastAppliedBlendXAnimatorParameter = 0f;
            _lastAppliedBlendYAnimatorParameter = 0f;
            _lastAppliedSpeedAnimatorParameter = 0f;
        }

        private static string FormatResourceError(ResourceError error)
        {
            if (error.IsNone)
                return string.Empty;

            return " resourceError=" + error;
        }

        private void RebuildRuntimeBlendDefinition()
        {
            if (_sourceBlend2DDefinition == null)
                return;

            var points = new List<MxAnimationBlend2DPoint>(_sourceBlend2DDefinition.Points.Count);
            for (int i = 0; i < _sourceBlend2DDefinition.Points.Count; i++)
            {
                MxAnimationBlend2DPoint point = _sourceBlend2DDefinition.Points[i];
                if (point == null)
                    continue;

                if (_blendPointCoordinateOverrides.TryGetValue(point.ClipKey, out Vector2Int coordinate))
                {
                    points.Add(new MxAnimationBlend2DPoint(
                        coordinate.x,
                        coordinate.y,
                        point.ClipKey,
                        point.PlaybackSpeed,
                        point.Loop));
                    continue;
                }

                points.Add(point);
            }

            _blend2DDefinition = new MxAnimationBlend2DDefinition(
                _sourceBlend2DDefinition.BlendId,
                _sourceBlend2DDefinition.ParameterXId,
                _sourceBlend2DDefinition.ParameterYId,
                _sourceBlend2DDefinition.LayerId,
                points,
                _sourceBlend2DDefinition.ParameterXScale,
                _sourceBlend2DDefinition.ParameterYScale,
                _sourceBlend2DDefinition.FadeDurationSeconds);
            RebuildBlendDomain(_blend2DDefinition);
            RebuildBlendPointPlaybackCalibrations(_blend2DDefinition);
            _blendReachabilityReport = CreateReachabilityReport();
            _lastPlaybackCompensationX = int.MinValue;
            _lastPlaybackCompensationY = int.MinValue;
            _lastPlaybackCompensationActualSpeed = -1f;
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

        private void RebuildBlendPointPlaybackCalibrations(MxAnimationBlend2DDefinition blendDefinition)
        {
            _blendPointPlaybackCalibrations.Clear();
            if (blendDefinition == null)
                return;

            for (int i = 0; i < blendDefinition.Points.Count; i++)
            {
                MxAnimationBlend2DPoint point = blendDefinition.Points[i];
                if (point == null || !point.ClipKey.IsValid)
                    continue;

                float x = point.X;
                float y = point.Y;
                float magnitude = Mathf.Sqrt((x * x) + (y * y));
                if (magnitude <= 0.0001f)
                    continue;

                float inverseMagnitude = 1f / magnitude;
                _blendPointPlaybackCalibrations.Add(new BlendPointPlaybackCalibration(
                    point.ClipKey,
                    x * inverseMagnitude,
                    y * inverseMagnitude,
                    inverseMagnitude));
            }
        }

        private float CalculatePlaybackSpeedCompensation(
            int rawQuantizedX,
            int rawQuantizedY,
            int clampedQuantizedX,
            int clampedQuantizedY,
            int scale)
        {
            if (!_enablePlaybackSpeedCompensation || _blend2DDefinition == null)
            {
                _lastBlendSampleSpeed = 0f;
                _lastBlendNodeSpeed = 0f;
                return 1f;
            }

            float sampleSpeed = Mathf.Sqrt((rawQuantizedX * rawQuantizedX) + (rawQuantizedY * rawQuantizedY)) / Mathf.Max(1, scale);
            if (sampleSpeed <= 0.0001f)
            {
                _lastBlendSampleSpeed = 0f;
                _lastBlendNodeSpeed = 0f;
                return 1f;
            }

            if (clampedQuantizedX == _lastPlaybackCompensationX
                && clampedQuantizedY == _lastPlaybackCompensationY
                && Mathf.Abs(sampleSpeed - _lastPlaybackCompensationActualSpeed) <= 0.0001f)
            {
                return _playbackSpeedCompensation;
            }

            _lastPlaybackCompensationX = clampedQuantizedX;
            _lastPlaybackCompensationY = clampedQuantizedY;
            _lastPlaybackCompensationActualSpeed = sampleSpeed;
            _lastBlendSampleSpeed = sampleSpeed;

            MxAnimationBlend2DCalculator.Evaluate(
                _blend2DDefinition,
                new MxAnimationQuantizedParameter(_blendXParameter, clampedQuantizedX, scale),
                new MxAnimationQuantizedParameter(_blendYParameter, clampedQuantizedY, scale),
                _playbackSpeedEvaluationBuffer);
            float nodeSpeed = CalculateBlendedMovingNodeSpeed(_playbackSpeedEvaluationBuffer.Weights, scale);
            if (nodeSpeed <= 0.0001f)
            {
                _lastBlendNodeSpeed = 0f;
                return 1f;
            }

            _lastBlendNodeSpeed = nodeSpeed;
            float multiplier = sampleSpeed * (1f / nodeSpeed);
            return Mathf.Clamp(
                multiplier,
                Mathf.Max(1f, Mathf.Clamp(_minPlaybackSpeedCompensation, 0.01f, 1f)),
                Mathf.Max(1f, _maxPlaybackSpeedCompensation));
        }

        private static float CalculateBlendedMovingNodeSpeed(IReadOnlyList<MxAnimationBlend2DWeight> weights, int scale)
        {
            if (weights == null || weights.Count == 0)
                return 0f;

            float weightedMagnitude = 0f;
            float movingWeight = 0f;
            float quantizedScale = Mathf.Max(1, scale);
            for (int weightIndex = 0; weightIndex < weights.Count; weightIndex++)
            {
                MxAnimationBlend2DWeight weight = weights[weightIndex];
                if (weight.Weight <= 0f)
                    continue;

                float magnitude = Mathf.Sqrt((weight.X * weight.X) + (weight.Y * weight.Y)) / quantizedScale;
                if (magnitude <= 0.0001f)
                    continue;

                weightedMagnitude += magnitude * weight.Weight;
                movingWeight += weight.Weight;
            }

            return movingWeight <= 0.0001f ? 0f : weightedMagnitude * (1f / movingWeight);
        }

        private static int QuantizeBlendAxisUnclamped(float value, int scale)
        {
            return Mathf.RoundToInt(value * Mathf.Max(1, scale));
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

        private readonly struct BlendPointPlaybackCalibration
        {
            public BlendPointPlaybackCalibration(
                ResourceKey clipKey,
                float directionX,
                float directionY,
                float inverseMagnitude)
            {
                ClipKey = clipKey;
                DirectionX = directionX;
                DirectionY = directionY;
                InverseMagnitude = inverseMagnitude;
            }

            public ResourceKey ClipKey { get; }
            public float DirectionX { get; }
            public float DirectionY { get; }
            public float InverseMagnitude { get; }
            public bool IsValid => ClipKey.IsValid;
        }

        [Serializable]
        public sealed class DirectClipBlendPoint
        {
            [SerializeField] private string _sourceClipName = string.Empty;
            [SerializeField] private float _x;
            [SerializeField] private float _y;
            [SerializeField] private float _playbackSpeed = 1f;
            [SerializeField] private bool _loop = true;

            public DirectClipBlendPoint(
                string sourceClipName,
                float x,
                float y,
                float playbackSpeed = 1f,
                bool loop = true)
            {
                _sourceClipName = sourceClipName ?? string.Empty;
                _x = x;
                _y = y;
                _playbackSpeed = Math.Abs(playbackSpeed) < 0.0001f ? 1f : playbackSpeed;
                _loop = loop;
            }

            public string SourceClipName => _sourceClipName;
            public float X => _x;
            public float Y => _y;
            public float PlaybackSpeed => _playbackSpeed;
            public bool Loop => _loop;
        }
    }
}
