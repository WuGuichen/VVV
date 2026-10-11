using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    public static class CharacterProceduralBodyRegionIds
    {
        public const int BroadcastAll = 0;
        public const int Default = CharacterAuthorityPoseRegionIds.Default;

        public static int NormalizeBindingRegion(int regionId)
        {
            return (regionId <= 0)
                ? Default
                : regionId;
        }

        public static bool IsBroadcastAll(int regionId)
        {
            return (regionId <= 0);
        }
    }

    public enum CharacterViewOnlyRagdollMode
    {
        Disabled = 0,
        KinematicFollow = 1,
        DynamicPresentation = 2,
        Recovering = 3
    }

    public readonly struct CharacterProceduralBoneImpulseRequest
    {
        public CharacterProceduralBoneImpulseRequest(
            Vector3 sourcePosition,
            Vector3 direction,
            float strength,
            float durationSeconds,
            int bodyRegionId = 0,
            string correlationId = "",
            string bodyRegionMatchSource = "",
            string bodyRegionFallbackIssue = "")
        {
            SourcePosition = sourcePosition;
            Direction = SafeNormalize(direction, Vector3.forward);
            Strength = Mathf.Max(0f, strength);
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            BodyRegionId = bodyRegionId;
            CorrelationId = correlationId ?? string.Empty;
            BodyRegionMatchSource = bodyRegionMatchSource ?? string.Empty;
            BodyRegionFallbackIssue = bodyRegionFallbackIssue ?? string.Empty;
        }

        public Vector3 SourcePosition { get; }
        public Vector3 Direction { get; }
        public float Strength { get; }
        public float DurationSeconds { get; }
        public int BodyRegionId { get; }
        public string CorrelationId { get; }
        public string BodyRegionMatchSource { get; }
        public string BodyRegionFallbackIssue { get; }

        public bool HasEffect => Strength > 0f && DurationSeconds > 0f;

        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 0.000001f && IsFinite(value)
                ? value.normalized
                : fallback;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct CharacterProceduralBoneRigSnapshot
    {
        public CharacterProceduralBoneRigSnapshot(
            int activeChainCount,
            int activeIkTargetCount,
            int activeImpulseCount,
            CharacterViewOnlyRagdollMode ragdollState,
            string lastIssue,
            int knownRegionCount = 0,
            int lastResolvedRegionId = 0,
            string lastRegionMatchSource = "",
            string lastRegionFallbackIssue = "")
        {
            ActiveChainCount = Math.Max(0, activeChainCount);
            ActiveIkTargetCount = Math.Max(0, activeIkTargetCount);
            ActiveImpulseCount = Math.Max(0, activeImpulseCount);
            RagdollState = ragdollState;
            LastIssue = lastIssue ?? string.Empty;
            KnownRegionCount = Math.Max(0, knownRegionCount);
            LastResolvedRegionId = lastResolvedRegionId;
            LastRegionMatchSource = lastRegionMatchSource ?? string.Empty;
            LastRegionFallbackIssue = lastRegionFallbackIssue ?? string.Empty;
        }

        public int ActiveChainCount { get; }
        public int ActiveIkTargetCount { get; }
        public int ActiveImpulseCount { get; }
        public CharacterViewOnlyRagdollMode RagdollState { get; }
        public string LastIssue { get; }
        public int KnownRegionCount { get; }
        public int LastResolvedRegionId { get; }
        public string LastRegionMatchSource { get; }
        public string LastRegionFallbackIssue { get; }
        public bool HasIssue => !string.IsNullOrEmpty(LastIssue);

        public static CharacterProceduralBoneRigSnapshot Empty(string issue = "")
        {
            return new CharacterProceduralBoneRigSnapshot(
                0,
                0,
                0,
                CharacterViewOnlyRagdollMode.Disabled,
                issue);
        }
    }

    /// <summary>Immutable low-frequency snapshot of one secondary bone chain simulation.</summary>
    public readonly struct CharacterSecondaryBoneChainPlaybackState
    {
        public CharacterSecondaryBoneChainPlaybackState(
            Vector3 offsetDegrees,
            Vector3 angularVelocityDegrees,
            float remainingImpulseSeconds)
        {
            OffsetDegrees = offsetDegrees;
            AngularVelocityDegrees = angularVelocityDegrees;
            RemainingImpulseSeconds = Mathf.Max(0f, remainingImpulseSeconds);
        }

        public Vector3 OffsetDegrees { get; }
        public Vector3 AngularVelocityDegrees { get; }
        public float RemainingImpulseSeconds { get; }
    }

    /// <summary>Immutable low-frequency snapshot of one view-only ragdoll body.</summary>
    public readonly struct CharacterViewOnlyRagdollBodyPlaybackState
    {
        public CharacterViewOnlyRagdollBodyPlaybackState(
            Vector3 position,
            Quaternion rotation,
            Vector3 linearVelocity,
            Vector3 angularVelocity,
            bool isKinematic)
        {
            Position = position;
            Rotation = rotation;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
            IsKinematic = isKinematic;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 LinearVelocity { get; }
        public Vector3 AngularVelocity { get; }
        public bool IsKinematic { get; }
    }

    /// <summary>AllocByDesign checkpoint for view-only ragdoll mode, timers, and rigidbody poses.</summary>
    public sealed class CharacterViewOnlyRagdollPlaybackState
    {
        private readonly CharacterViewOnlyRagdollBodyPlaybackState[] _bodies;

        public CharacterViewOnlyRagdollPlaybackState(
            CharacterViewOnlyRagdollMode mode,
            float dynamicSecondsRemaining,
            CharacterViewOnlyRagdollBodyPlaybackState[] bodies)
        {
            Mode = mode;
            DynamicSecondsRemaining = Mathf.Max(0f, dynamicSecondsRemaining);
            _bodies = bodies != null
                ? (CharacterViewOnlyRagdollBodyPlaybackState[])bodies.Clone()
                : Array.Empty<CharacterViewOnlyRagdollBodyPlaybackState>();
        }

        public CharacterViewOnlyRagdollMode Mode { get; }
        public float DynamicSecondsRemaining { get; }
        public IReadOnlyList<CharacterViewOnlyRagdollBodyPlaybackState> Bodies => _bodies;
    }

    /// <summary>AllocByDesign checkpoint for IK, secondary bones, and view-only ragdoll presentation.</summary>
    public sealed class CharacterProceduralBonePlaybackState
    {
        private readonly CharacterSecondaryBoneChainPlaybackState[] _secondaryChains;

        public CharacterProceduralBonePlaybackState(
            float ikSolveSecondsRemaining,
            CharacterSecondaryBoneChainPlaybackState[] secondaryChains,
            CharacterViewOnlyRagdollPlaybackState ragdoll)
        {
            IkSolveSecondsRemaining = Mathf.Max(0f, ikSolveSecondsRemaining);
            _secondaryChains = secondaryChains != null
                ? (CharacterSecondaryBoneChainPlaybackState[])secondaryChains.Clone()
                : Array.Empty<CharacterSecondaryBoneChainPlaybackState>();
            Ragdoll = ragdoll;
        }

        public float IkSolveSecondsRemaining { get; }
        public IReadOnlyList<CharacterSecondaryBoneChainPlaybackState> SecondaryChains => _secondaryChains;
        public CharacterViewOnlyRagdollPlaybackState Ragdoll { get; }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Procedural Bone Rig")]
    public sealed class CharacterProceduralBoneRig : MonoBehaviour
    {
        [SerializeField] private bool _useExternalRuntimeTick = true;
        [SerializeField] private CharacterSecondaryBoneChain[] _secondaryChains =
            Array.Empty<CharacterSecondaryBoneChain>();
        [SerializeField] private CharacterTwoBoneIkTarget[] _ikTargets =
            Array.Empty<CharacterTwoBoneIkTarget>();
        [SerializeField] private CharacterViewOnlyRagdollRig _ragdollRig;

        private bool _childrenResolved;
        private bool _hasActivePresentationWork;
        private float _ikSolveSecondsRemaining;
        private Transform[] _authorityPoseControlledBones = Array.Empty<Transform>();
        private int _authorityPoseControlledBoneCount;
        private string _lastIssue = string.Empty;
        private int _lastResolvedRegionId = 0;
        private string _lastRegionMatchSource = string.Empty;
        private string _lastRegionFallbackIssue = string.Empty;

        public bool UseExternalRuntimeTick
        {
            get => _useExternalRuntimeTick;
            set => _useExternalRuntimeTick = value;
        }

        public IReadOnlyList<CharacterSecondaryBoneChain> SecondaryChains => _secondaryChains;
        public IReadOnlyList<CharacterTwoBoneIkTarget> IkTargets => _ikTargets;
        public CharacterViewOnlyRagdollRig RagdollRig => _ragdollRig;
        public bool HasActivePresentationWork => _hasActivePresentationWork;

        /// <summary>
        /// Marks the authored bones whose final presentation pose is supplied by the
        /// deterministic authority-pose frame. Matching float secondary/IK writers are skipped.
        /// </summary>
        /// <remarks>LowFreqAlloc: the caller retains ownership of the stable input array.</remarks>
        public void ConfigureAuthorityPoseControlledBones(Transform[] bones, int count)
        {
            _authorityPoseControlledBones = bones ?? Array.Empty<Transform>();
            _authorityPoseControlledBoneCount = Mathf.Clamp(
                count,
                0,
                _authorityPoseControlledBones.Length);
            if (_childrenResolved)
                RefreshActivePresentationWork();
        }

        private void LateUpdate()
        {
            if (_useExternalRuntimeTick)
                return;

            Tick(Time.deltaTime);
        }

        public void RefreshChildren()
        {
            _secondaryChains = GetComponentsInChildren<CharacterSecondaryBoneChain>(true);
            _ikTargets = GetComponentsInChildren<CharacterTwoBoneIkTarget>(true);
            _ragdollRig = GetComponentInChildren<CharacterViewOnlyRagdollRig>(true);
            _childrenResolved = true;
            _lastIssue = string.Empty;
            RefreshActivePresentationWork();
        }

        public void ApplyImpulse(CharacterProceduralBoneImpulseRequest request)
        {
            EnsureChildren();
            if (!request.HasEffect)
                return;

            int matchedChains = ApplySecondaryImpulseToChains(request);

            _ragdollRig?.ApplyImpulse(request);
            RecordRegionDiagnostics(request, matchedChains);
            RefreshActivePresentationWork();
        }

        public void ApplySecondaryBoneImpulse(CharacterProceduralBoneImpulseRequest request)
        {
            EnsureChildren();
            if (!request.HasEffect)
                return;

            int matchedChains = ApplySecondaryImpulseToChains(request);

            RecordRegionDiagnostics(request, matchedChains);
            RefreshActivePresentationWork();
        }

        public bool TryResolveNearestBodyRegion(Vector3 worldPosition, out int regionId)
        {
            EnsureChildren();
            regionId = 0;
            if (!IsFinite(worldPosition))
                return false;

            float bestDistanceSquared = float.MaxValue;
            int bestRegion = 0;
            for (int i = 0; i < _secondaryChains.Length; i++)
            {
                CharacterSecondaryBoneChain chain = _secondaryChains[i];
                if (chain == null || !chain.isActiveAndEnabled)
                    continue;

                int candidateRegion = chain.NormalizedBodyRegionId;
                if (candidateRegion == CharacterProceduralBodyRegionIds.Default)
                {
                    continue;
                }

                Transform anchor = chain.ChainRoot != null ? chain.ChainRoot : chain.transform;
                if (anchor == null)
                    continue;

                float distanceSquared = (anchor.position - worldPosition).sqrMagnitude;
                if (!IsFinite(distanceSquared) || distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                bestRegion = candidateRegion;
            }

            if ((bestRegion <= 0))
                return false;

            regionId = bestRegion;
            return true;
        }

        public void RequestIkSolve(float durationSeconds = 0f)
        {
            EnsureChildren();
            _ikSolveSecondsRemaining = Mathf.Max(
                _ikSolveSecondsRemaining,
                Mathf.Max(0.001f, durationSeconds));
            RefreshActivePresentationWork();
        }

        public CharacterProceduralBoneRigSnapshot CreateSnapshot()
        {
            EnsureChildren();

            int activeChainCount = 0;
            int activeIkTargetCount = 0;
            int activeImpulseCount = 0;
            int knownRegionCount = CountKnownRegions();
            string issue = _lastIssue;

            for (int i = 0; i < _secondaryChains.Length; i++)
            {
                CharacterSecondaryBoneChain chain = _secondaryChains[i];
                if (chain == null
                    || !chain.isActiveAndEnabled
                    || IsAuthorityPoseControlled(chain))
                    continue;

                activeChainCount++;
                activeImpulseCount += chain.ActiveImpulseCount;
                if (string.IsNullOrEmpty(issue))
                    issue = chain.LastIssue;
            }

            for (int i = 0; i < _ikTargets.Length; i++)
            {
                CharacterTwoBoneIkTarget ikTarget = _ikTargets[i];
                if (ikTarget == null
                    || !ikTarget.isActiveAndEnabled
                    || IsAuthorityPoseControlled(ikTarget))
                    continue;

                activeIkTargetCount++;
                if (string.IsNullOrEmpty(issue))
                    issue = ikTarget.LastIssue;
            }

            CharacterViewOnlyRagdollMode ragdollState = CharacterViewOnlyRagdollMode.Disabled;
            if (_ragdollRig != null)
            {
                ragdollState = _ragdollRig.Mode;
                activeImpulseCount += _ragdollRig.ActiveImpulseCount;
                if (string.IsNullOrEmpty(issue))
                    issue = _ragdollRig.LastIssue;
            }

            return new CharacterProceduralBoneRigSnapshot(
                activeChainCount,
                activeIkTargetCount,
                activeImpulseCount,
                ragdollState,
                issue,
                knownRegionCount,
                _lastResolvedRegionId,
                _lastRegionMatchSource,
                _lastRegionFallbackIssue);
        }

        /// <summary>AllocByDesign: captures procedural presentation state for rollback or preview.</summary>
        public CharacterProceduralBonePlaybackState CapturePlaybackState()
        {
            EnsureChildren();
            var chains = new CharacterSecondaryBoneChainPlaybackState[_secondaryChains.Length];
            for (int i = 0; i < chains.Length; i++)
            {
                CharacterSecondaryBoneChain chain = _secondaryChains[i];
                chains[i] = chain != null ? chain.CapturePlaybackState() : default;
            }

            return new CharacterProceduralBonePlaybackState(
                _ikSolveSecondsRemaining,
                chains,
                _ragdollRig != null ? _ragdollRig.CapturePlaybackState() : null);
        }

        /// <summary>LowFreqAlloc: restores procedural presentation without advancing its timers.</summary>
        public bool RestorePlaybackState(
            CharacterProceduralBonePlaybackState state,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (state == null)
            {
                diagnostic = "Procedural bone playback state is missing.";
                return false;
            }

            EnsureChildren();
            if (state.SecondaryChains.Count != _secondaryChains.Length)
            {
                diagnostic = "Procedural bone chain count does not match.";
                return false;
            }

            for (int i = 0; i < _secondaryChains.Length; i++)
            {
                CharacterSecondaryBoneChain chain = _secondaryChains[i];
                if (chain != null)
                {
                    chain.RestorePlaybackState(
                        state.SecondaryChains[i],
                        applyPose: !IsAuthorityPoseControlled(chain));
                }
            }

            _ikSolveSecondsRemaining = state.IkSolveSecondsRemaining;
            bool solveIk = _ikSolveSecondsRemaining > 0f || HasContinuousIkTarget();
            if (solveIk)
            {
                for (int i = 0; i < _ikTargets.Length; i++)
                {
                    CharacterTwoBoneIkTarget target = _ikTargets[i];
                    if (target != null
                        && target.isActiveAndEnabled
                        && !IsAuthorityPoseControlled(target))
                        target.Tick();
                }
            }

            if (_ragdollRig != null && state.Ragdoll != null
                && !_ragdollRig.RestorePlaybackState(state.Ragdoll, out diagnostic))
            {
                return false;
            }

            RefreshActivePresentationWork();
            return true;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(float deltaTime)
        {
            EnsureChildren();
            if (!_hasActivePresentationWork)
                return;

            float safeDeltaTime = Mathf.Max(0f, deltaTime);

            for (int i = 0; i < _secondaryChains.Length; i++)
            {
                CharacterSecondaryBoneChain chain = _secondaryChains[i];
                if (chain != null
                    && !IsAuthorityPoseControlled(chain)
                    && chain.HasActivePresentationWork)
                    chain.Tick(safeDeltaTime);
            }

            bool solveIk = _ikSolveSecondsRemaining > 0f || HasContinuousIkTarget();
            if (_ikSolveSecondsRemaining > 0f)
                _ikSolveSecondsRemaining = Mathf.Max(0f, _ikSolveSecondsRemaining - safeDeltaTime);

            if (solveIk)
            {
                for (int i = 0; i < _ikTargets.Length; i++)
                {
                    CharacterTwoBoneIkTarget ikTarget = _ikTargets[i];
                    if (ikTarget != null
                        && ikTarget.isActiveAndEnabled
                        && !IsAuthorityPoseControlled(ikTarget))
                        ikTarget.Tick();
                }
            }

            _ragdollRig?.Tick(safeDeltaTime);
            RefreshActivePresentationWork();
        }

        private void EnsureChildren()
        {
            if (!_childrenResolved)
                RefreshChildren();
        }

        private int ApplySecondaryImpulseToChains(CharacterProceduralBoneImpulseRequest request)
        {
            int matchedChains = 0;
            for (int i = 0; i < _secondaryChains.Length; i++)
            {
                CharacterSecondaryBoneChain chain = _secondaryChains[i];
                if (chain != null
                    && !IsAuthorityPoseControlled(chain)
                    && chain.ApplyImpulse(request))
                    matchedChains++;
            }

            return matchedChains;
        }

        private void RecordRegionDiagnostics(
            CharacterProceduralBoneImpulseRequest request,
            int matchedChains)
        {
            _lastResolvedRegionId = CharacterProceduralBodyRegionIds.IsBroadcastAll(request.BodyRegionId)
                ? CharacterProceduralBodyRegionIds.BroadcastAll
                : CharacterProceduralBodyRegionIds.NormalizeBindingRegion(request.BodyRegionId);
            _lastRegionMatchSource = string.IsNullOrWhiteSpace(request.BodyRegionMatchSource)
                ? (CharacterProceduralBodyRegionIds.IsBroadcastAll(request.BodyRegionId) ? "broadcast" : "request")
                : request.BodyRegionMatchSource;
            _lastRegionFallbackIssue = request.BodyRegionFallbackIssue;
            if (matchedChains == 0
                && !CharacterProceduralBodyRegionIds.IsBroadcastAll(request.BodyRegionId)
                && string.IsNullOrEmpty(_lastRegionFallbackIssue))
            {
                _lastRegionFallbackIssue = "no secondary chain matched region "
                    + CharacterProceduralBodyRegionIds.NormalizeBindingRegion(request.BodyRegionId);
            }
        }

        private int CountKnownRegions()
        {
            var regions = new HashSet<int>();
            for (int i = 0; i < _secondaryChains.Length; i++)
            {
                CharacterSecondaryBoneChain chain = _secondaryChains[i];
                if (chain == null || !chain.isActiveAndEnabled)
                    continue;

                regions.Add(chain.NormalizedBodyRegionId);
            }

            return regions.Count;
        }

        private void RefreshActivePresentationWork()
        {
            _hasActivePresentationWork = _ikSolveSecondsRemaining > 0f || HasContinuousIkTarget();
            if (_hasActivePresentationWork)
                return;

            for (int i = 0; i < _secondaryChains.Length; i++)
            {
                CharacterSecondaryBoneChain chain = _secondaryChains[i];
                if (chain != null
                    && !IsAuthorityPoseControlled(chain)
                    && chain.HasActivePresentationWork)
                {
                    _hasActivePresentationWork = true;
                    return;
                }
            }

            _hasActivePresentationWork = _ragdollRig != null
                && _ragdollRig.HasActivePresentationWork;
        }

        private bool HasContinuousIkTarget()
        {
            for (int i = 0; i < _ikTargets.Length; i++)
            {
                CharacterTwoBoneIkTarget ikTarget = _ikTargets[i];
                if (ikTarget != null
                    && ikTarget.isActiveAndEnabled
                    && !IsAuthorityPoseControlled(ikTarget)
                    && ikTarget.SolveContinuously)
                    return true;
            }

            return false;
        }

        private bool IsAuthorityPoseControlled(CharacterSecondaryBoneChain chain)
        {
            return chain != null
                && chain.AffectsAny(
                    _authorityPoseControlledBones,
                    _authorityPoseControlledBoneCount);
        }

        private bool IsAuthorityPoseControlled(CharacterTwoBoneIkTarget target)
        {
            return target != null
                && target.AffectsAny(
                    _authorityPoseControlledBones,
                    _authorityPoseControlledBoneCount);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [AddComponentMenu("MxFramework/Character/Secondary Bone Chain")]
    public sealed class CharacterSecondaryBoneChain : MonoBehaviour
    {
        [SerializeField] private Transform _chainRoot;
        [SerializeField] private Transform[] _affectedBones = Array.Empty<Transform>();
        [SerializeField] private int _bodyRegionId = 0;
        [SerializeField] private int _maxAutoDepth = 8;
        [SerializeField] private float _stiffness = 42f;
        [SerializeField] private float _damping = 11f;
        [SerializeField] private float _maxAngleDegrees = 24f;
        [SerializeField] private float _impulseScale = 18f;

        private Quaternion[] _bindLocalRotations = Array.Empty<Quaternion>();
        private Vector3 _offsetDegrees;
        private Vector3 _angularVelocityDegrees;
        private float _remainingImpulseSeconds;
        private string _lastIssue = string.Empty;

        public Transform ChainRoot => _chainRoot != null ? _chainRoot : transform;
        public int BodyRegionId => _bodyRegionId;
        public int NormalizedBodyRegionId =>
            CharacterProceduralBodyRegionIds.NormalizeBindingRegion(_bodyRegionId);
        public int ActiveImpulseCount => _remainingImpulseSeconds > 0f ? 1 : 0;
        public string LastIssue => _lastIssue;
        public bool HasActivePresentationWork =>
            _remainingImpulseSeconds > 0f
            || _offsetDegrees.sqrMagnitude > 0.0001f
            || _angularVelocityDegrees.sqrMagnitude > 0.0001f;

        private void Awake()
        {
            CaptureBindPose();
        }

        public void ConfigureRuntimeChain(
            Transform chainRoot,
            int bodyRegionId,
            params Transform[] affectedBones)
        {
            _chainRoot = chainRoot != null ? chainRoot : transform;
            _bodyRegionId = bodyRegionId;
            _affectedBones = affectedBones ?? Array.Empty<Transform>();
            CaptureBindPose();
        }

        public void CaptureBindPose()
        {
            EnsureAffectedBones();
            if (_affectedBones.Length == 0)
            {
                _bindLocalRotations = Array.Empty<Quaternion>();
                return;
            }

            _bindLocalRotations = new Quaternion[_affectedBones.Length];
            for (int i = 0; i < _affectedBones.Length; i++)
                _bindLocalRotations[i] = _affectedBones[i] != null
                    ? _affectedBones[i].localRotation
                    : Quaternion.identity;
        }

        public void ConfigureRuntimeTuning(
            float stiffness,
            float damping,
            float maxAngleDegrees,
            float impulseScale,
            int maxAutoDepth = -1)
        {
            _stiffness = Mathf.Max(0f, stiffness);
            _damping = Mathf.Max(0f, damping);
            _maxAngleDegrees = Mathf.Max(0f, maxAngleDegrees);
            _impulseScale = Mathf.Max(0f, impulseScale);
            if (maxAutoDepth >= 0)
                _maxAutoDepth = maxAutoDepth;
        }

        public bool ApplyImpulse(CharacterProceduralBoneImpulseRequest request)
        {
            if (!request.HasEffect || !MatchesRegion(request.BodyRegionId))
                return false;

            EnsureBindPose();
            Vector3 localDirection = transform.InverseTransformDirection(request.Direction);
            if (!IsFinite(localDirection))
            {
                _lastIssue = "secondary impulse direction is not finite";
                return false;
            }

            Vector3 swing = new Vector3(-localDirection.z, localDirection.x, localDirection.y * 0.25f);
            if (swing.sqrMagnitude <= 0.000001f)
                swing = Vector3.right;

            _angularVelocityDegrees += swing.normalized * request.Strength * Mathf.Max(0f, _impulseScale);
            _remainingImpulseSeconds = Mathf.Max(_remainingImpulseSeconds, request.DurationSeconds);
            return true;
        }

        public CharacterSecondaryBoneChainPlaybackState CapturePlaybackState()
        {
            return new CharacterSecondaryBoneChainPlaybackState(
                _offsetDegrees,
                _angularVelocityDegrees,
                _remainingImpulseSeconds);
        }

        public void RestorePlaybackState(CharacterSecondaryBoneChainPlaybackState state)
        {
            RestorePlaybackState(state, applyPose: true);
        }

        internal void RestorePlaybackState(
            CharacterSecondaryBoneChainPlaybackState state,
            bool applyPose)
        {
            _offsetDegrees = state.OffsetDegrees;
            _angularVelocityDegrees = state.AngularVelocityDegrees;
            _remainingImpulseSeconds = state.RemainingImpulseSeconds;
            if (applyPose)
                Tick(0f);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(float deltaTime)
        {
            EnsureBindPose();
            if (_affectedBones.Length == 0)
                return;

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            if (_remainingImpulseSeconds > 0f)
                _remainingImpulseSeconds = Mathf.Max(0f, _remainingImpulseSeconds - safeDeltaTime);

            if (safeDeltaTime > 0f)
            {
                _angularVelocityDegrees += -_offsetDegrees * Mathf.Max(0f, _stiffness) * safeDeltaTime;
                _angularVelocityDegrees *= Mathf.Exp(-Mathf.Max(0f, _damping) * safeDeltaTime);
                _offsetDegrees += _angularVelocityDegrees * safeDeltaTime;
            }

            if (!IsFinite(_offsetDegrees) || !IsFinite(_angularVelocityDegrees))
            {
                ResetMotion("secondary chain produced non-finite motion");
                return;
            }

            if (_remainingImpulseSeconds <= 0f
                && _offsetDegrees.sqrMagnitude <= 0.0001f
                && _angularVelocityDegrees.sqrMagnitude <= 0.0001f)
            {
                _offsetDegrees = Vector3.zero;
                _angularVelocityDegrees = Vector3.zero;
            }

            float maxAngle = Mathf.Max(0f, _maxAngleDegrees);
            if (_offsetDegrees.magnitude > maxAngle && maxAngle > 0f)
                _offsetDegrees = _offsetDegrees.normalized * maxAngle;

            Quaternion offset = Quaternion.Euler(_offsetDegrees);
            for (int i = 0; i < _affectedBones.Length; i++)
            {
                Transform bone = _affectedBones[i];
                if (bone == null)
                    continue;

                Quaternion bindRotation = i < _bindLocalRotations.Length
                    ? _bindLocalRotations[i]
                    : bone.localRotation;
                float falloff = 1f - (float)i / Mathf.Max(1, _affectedBones.Length);
                bone.localRotation = bindRotation * Quaternion.Slerp(Quaternion.identity, offset, falloff);
            }
        }

        private void ResetMotion(string issue)
        {
            _offsetDegrees = Vector3.zero;
            _angularVelocityDegrees = Vector3.zero;
            _remainingImpulseSeconds = 0f;
            _lastIssue = issue;
        }

        private bool MatchesRegion(int requestedRegion)
        {
            if (CharacterProceduralBodyRegionIds.IsBroadcastAll(requestedRegion))
                return true;

            return NormalizedBodyRegionId == CharacterProceduralBodyRegionIds.NormalizeBindingRegion(requestedRegion);
        }

        private void EnsureBindPose()
        {
            EnsureAffectedBones();
            if (_bindLocalRotations.Length != _affectedBones.Length)
                CaptureBindPose();
        }

        private void EnsureAffectedBones()
        {
            if (_chainRoot == null)
                _chainRoot = transform;

            if (_affectedBones != null && _affectedBones.Length > 0)
                return;

            int capacity = Mathf.Max(1, _maxAutoDepth + 1);
            Transform[] discovered = new Transform[capacity];
            int count = CollectChain(_chainRoot, discovered, 0, 0, Mathf.Max(0, _maxAutoDepth));
            if (count == 0)
            {
                _affectedBones = Array.Empty<Transform>();
                return;
            }

            _affectedBones = new Transform[count];
            Array.Copy(discovered, _affectedBones, count);
        }

        internal bool AffectsAny(Transform[] bones, int count)
        {
            if (bones == null || count <= 0)
                return false;

            EnsureAffectedBones();
            int safeCount = Mathf.Min(count, bones.Length);
            for (int i = 0; i < _affectedBones.Length; i++)
            {
                Transform affected = _affectedBones[i];
                if (affected == null)
                    continue;

                for (int j = 0; j < safeCount; j++)
                {
                    if (bones[j] == affected)
                        return true;
                }
            }

            return false;
        }

        private static int CollectChain(
            Transform node,
            Transform[] buffer,
            int count,
            int depth,
            int maxDepth)
        {
            if (node == null || count >= buffer.Length || depth > maxDepth)
                return count;

            buffer[count++] = node;
            if (node.childCount == 0 || depth == maxDepth)
                return count;

            return CollectChain(node.GetChild(0), buffer, count, depth + 1, maxDepth);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [AddComponentMenu("MxFramework/Character/Two Bone IK Target")]
    public sealed class CharacterTwoBoneIkTarget : MonoBehaviour
    {
        [SerializeField] private Transform _root;
        [SerializeField] private Transform _mid;
        [SerializeField] private Transform _tip;
        [SerializeField] private Transform _target;
        [SerializeField] private Transform _pole;
        [SerializeField] private bool _solveContinuously;
        [SerializeField] private float _maxExtensionRatio = 0.995f;
        [SerializeField] private float _minBendDistance = 0.001f;

        private bool _lastTargetClamped;
        private string _lastIssue = string.Empty;

        public bool LastTargetClamped => _lastTargetClamped;
        public string LastIssue => _lastIssue;
        public bool SolveContinuously
        {
            get => _solveContinuously;
            set => _solveContinuously = value;
        }

        internal bool AffectsAny(Transform[] bones, int count)
        {
            if (bones == null || count <= 0)
                return false;

            int safeCount = Mathf.Min(count, bones.Length);
            for (int i = 0; i < safeCount; i++)
            {
                Transform bone = bones[i];
                if (bone != null && (bone == _root || bone == _mid || bone == _tip))
                    return true;
            }

            return false;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick()
        {
            _lastTargetClamped = false;
            if (_root == null || _mid == null || _tip == null || _target == null)
            {
                _lastIssue = "two-bone IK target is missing a required transform";
                return;
            }

            Vector3 rootPosition = _root.position;
            Vector3 midPosition = _mid.position;
            Vector3 tipPosition = _tip.position;
            float upperLength = Vector3.Distance(rootPosition, midPosition);
            float lowerLength = Vector3.Distance(midPosition, tipPosition);
            float maxReach = upperLength + lowerLength;
            if (upperLength <= 0.000001f || lowerLength <= 0.000001f || maxReach <= 0.000001f)
            {
                _lastIssue = "two-bone IK chain has invalid bone length";
                return;
            }

            Vector3 targetVector = _target.position - rootPosition;
            float targetDistance = targetVector.magnitude;
            if (!IsFinite(targetVector) || targetDistance <= 0.000001f)
            {
                _lastIssue = "two-bone IK target vector is invalid";
                return;
            }

            float clampedDistance = Mathf.Clamp(
                targetDistance,
                Mathf.Max(_minBendDistance, Mathf.Abs(upperLength - lowerLength) + _minBendDistance),
                maxReach * Mathf.Clamp(_maxExtensionRatio, 0.001f, 1f));
            _lastTargetClamped = !Mathf.Approximately(clampedDistance, targetDistance);

            Vector3 targetDirection = targetVector / targetDistance;
            Vector3 planeNormal = ResolvePlaneNormal(rootPosition, midPosition, targetDirection);
            Vector3 bendDirection = Vector3.Cross(planeNormal, targetDirection);
            if (bendDirection.sqrMagnitude <= 0.000001f)
                bendDirection = Vector3.up;
            bendDirection.Normalize();

            float x = (upperLength * upperLength + clampedDistance * clampedDistance - lowerLength * lowerLength)
                / (2f * clampedDistance);
            float ySquared = Mathf.Max(0f, upperLength * upperLength - x * x);
            float y = Mathf.Sqrt(ySquared);

            Vector3 solvedMid = rootPosition + targetDirection * x + bendDirection * y;
            Vector3 solvedTip = rootPosition + targetDirection * clampedDistance;
            if (!IsFinite(solvedMid) || !IsFinite(solvedTip))
            {
                _lastIssue = "two-bone IK solver produced non-finite output";
                return;
            }

            ApplyLookRotation(_root, solvedMid - rootPosition, planeNormal);
            _mid.position = solvedMid;
            ApplyLookRotation(_mid, solvedTip - solvedMid, planeNormal);
            _tip.position = solvedTip;
            _lastIssue = string.Empty;
        }

        private Vector3 ResolvePlaneNormal(
            Vector3 rootPosition,
            Vector3 midPosition,
            Vector3 targetDirection)
        {
            if (_pole != null)
            {
                Vector3 poleVector = _pole.position - rootPosition;
                Vector3 poleNormal = Vector3.Cross(targetDirection, poleVector);
                if (poleNormal.sqrMagnitude > 0.000001f && IsFinite(poleNormal))
                    return poleNormal.normalized;
            }

            Vector3 currentNormal = Vector3.Cross(midPosition - rootPosition, targetDirection);
            if (currentNormal.sqrMagnitude > 0.000001f && IsFinite(currentNormal))
                return currentNormal.normalized;

            Vector3 fallbackNormal = Vector3.Cross(Vector3.up, targetDirection);
            if (fallbackNormal.sqrMagnitude > 0.000001f)
                return fallbackNormal.normalized;

            return Vector3.right;
        }

        private static void ApplyLookRotation(Transform bone, Vector3 forward, Vector3 up)
        {
            if (bone == null || forward.sqrMagnitude <= 0.000001f)
                return;

            if (up.sqrMagnitude <= 0.000001f)
                up = Vector3.up;

            bone.rotation = Quaternion.LookRotation(forward.normalized, up.normalized);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/View Only Ragdoll Rig")]
    public sealed class CharacterViewOnlyRagdollRig : MonoBehaviour
    {
        [SerializeField] private Rigidbody[] _bodies = Array.Empty<Rigidbody>();
        [SerializeField] private Collider[] _colliders = Array.Empty<Collider>();
        [SerializeField] private CharacterViewOnlyRagdollMode _mode = CharacterViewOnlyRagdollMode.KinematicFollow;
        [SerializeField] private float _defaultDynamicDurationSeconds = 0.45f;
        [SerializeField] private bool _restoreKinematicOnDisable = true;
        [SerializeField] private bool _disableCollidersOutsideDynamicPresentation = true;

        private float _dynamicSecondsRemaining;
        private bool _bodiesResolved;
        private string _lastIssue = string.Empty;

        public CharacterViewOnlyRagdollMode Mode => _mode;
        public int ActiveImpulseCount => _mode == CharacterViewOnlyRagdollMode.DynamicPresentation ? 1 : 0;
        public string LastIssue => _lastIssue;
        public bool HasActivePresentationWork =>
            _mode == CharacterViewOnlyRagdollMode.DynamicPresentation
            || _mode == CharacterViewOnlyRagdollMode.Recovering;

        private void OnEnable()
        {
            RefreshBodies();
            ApplyMode();
        }

        private void OnDisable()
        {
            if (!_restoreKinematicOnDisable)
                return;

            SetMode(CharacterViewOnlyRagdollMode.KinematicFollow);
        }

        public void RefreshBodies()
        {
            _bodies = GetComponentsInChildren<Rigidbody>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _bodiesResolved = true;
            _lastIssue = string.Empty;
            ApplyMode();
        }

        public void SetMode(CharacterViewOnlyRagdollMode mode)
        {
            EnsureBodies();
            _mode = NormalizeMode(mode);
            if (_mode != CharacterViewOnlyRagdollMode.DynamicPresentation)
                _dynamicSecondsRemaining = 0f;

            ApplyMode();
        }

        public void TriggerDynamic(float durationSeconds)
        {
            EnsureBodies();
            if (_bodies.Length == 0)
            {
                _lastIssue = "view-only ragdoll has no rigidbodies";
                return;
            }

            _dynamicSecondsRemaining = Mathf.Max(0f, durationSeconds);
            if (_dynamicSecondsRemaining <= 0f)
                _dynamicSecondsRemaining = Mathf.Max(0.01f, _defaultDynamicDurationSeconds);

            _mode = CharacterViewOnlyRagdollMode.DynamicPresentation;
            ApplyMode();
        }

        public void ApplyImpulse(CharacterProceduralBoneImpulseRequest request)
        {
            if (!request.HasEffect)
                return;

            TriggerDynamic(request.DurationSeconds);
            if (_mode != CharacterViewOnlyRagdollMode.DynamicPresentation)
                return;

            Vector3 velocityChange = request.Direction * request.Strength;
            for (int i = 0; i < _bodies.Length; i++)
            {
                Rigidbody body = _bodies[i];
                if (body == null)
                    continue;

                body.AddForce(velocityChange, ForceMode.VelocityChange);
            }
        }

        /// <summary>AllocByDesign: captures all view-only rigidbody poses and velocities.</summary>
        public CharacterViewOnlyRagdollPlaybackState CapturePlaybackState()
        {
            EnsureBodies();
            var bodies = new CharacterViewOnlyRagdollBodyPlaybackState[_bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = _bodies[i];
                bodies[i] = body == null
                    ? default
                    : new CharacterViewOnlyRagdollBodyPlaybackState(
                        body.position,
                        body.rotation,
                        body.linearVelocity,
                        body.angularVelocity,
                        body.isKinematic);
            }

            return new CharacterViewOnlyRagdollPlaybackState(
                _mode,
                _dynamicSecondsRemaining,
                bodies);
        }

        /// <summary>LowFreqAlloc: restores view-only rigidbody poses without simulating time.</summary>
        public bool RestorePlaybackState(
            CharacterViewOnlyRagdollPlaybackState state,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (state == null)
            {
                diagnostic = "View-only ragdoll playback state is missing.";
                return false;
            }

            EnsureBodies();
            if (state.Bodies.Count != _bodies.Length)
            {
                diagnostic = "View-only ragdoll body count does not match.";
                return false;
            }

            _mode = NormalizeMode(state.Mode);
            _dynamicSecondsRemaining = state.DynamicSecondsRemaining;
            ApplyMode();
            for (int i = 0; i < _bodies.Length; i++)
            {
                Rigidbody body = _bodies[i];
                if (body == null)
                    continue;
                CharacterViewOnlyRagdollBodyPlaybackState bodyState = state.Bodies[i];
                body.position = bodyState.Position;
                body.rotation = bodyState.Rotation;
                body.linearVelocity = bodyState.LinearVelocity;
                body.angularVelocity = bodyState.AngularVelocity;
                body.isKinematic = bodyState.IsKinematic;
            }

            Physics.SyncTransforms();
            return true;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(float deltaTime)
        {
            EnsureBodies();
            if (_mode != CharacterViewOnlyRagdollMode.DynamicPresentation)
                return;

            _dynamicSecondsRemaining = Mathf.Max(0f, _dynamicSecondsRemaining - Mathf.Max(0f, deltaTime));
            if (_dynamicSecondsRemaining > 0f)
                return;

            _mode = CharacterViewOnlyRagdollMode.Recovering;
            ApplyMode();
            _mode = CharacterViewOnlyRagdollMode.KinematicFollow;
            ApplyMode();
        }

        private void ApplyMode()
        {
            bool dynamic = _mode == CharacterViewOnlyRagdollMode.DynamicPresentation;
            for (int i = 0; i < _bodies.Length; i++)
            {
                Rigidbody body = _bodies[i];
                if (body == null)
                    continue;

                body.isKinematic = !dynamic;
                body.detectCollisions = dynamic;
            }

            if (!_disableCollidersOutsideDynamicPresentation)
                return;

            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider collider = _colliders[i];
                if (collider != null)
                    collider.enabled = dynamic;
            }
        }

        private void EnsureBodies()
        {
            if (!_bodiesResolved)
                RefreshBodies();
        }

        private static CharacterViewOnlyRagdollMode NormalizeMode(CharacterViewOnlyRagdollMode mode)
        {
            switch (mode)
            {
                case CharacterViewOnlyRagdollMode.Disabled:
                case CharacterViewOnlyRagdollMode.KinematicFollow:
                case CharacterViewOnlyRagdollMode.DynamicPresentation:
                case CharacterViewOnlyRagdollMode.Recovering:
                    return mode;
                default:
                    return CharacterViewOnlyRagdollMode.KinematicFollow;
            }
        }
    }
}
