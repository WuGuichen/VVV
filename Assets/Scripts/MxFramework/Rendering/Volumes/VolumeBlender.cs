using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Rendering
{
    public readonly struct MxVolumeRequestId : IEquatable<MxVolumeRequestId>
    {
        public MxVolumeRequestId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }
        public bool IsValid => Value > 0;

        public bool Equals(MxVolumeRequestId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is MxVolumeRequestId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? Value.ToString() : "Invalid";
        }

        public static bool operator ==(MxVolumeRequestId left, MxVolumeRequestId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxVolumeRequestId left, MxVolumeRequestId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct MxRenderingCameraToken : IEquatable<MxRenderingCameraToken>
    {
        public MxRenderingCameraToken(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }
        public bool IsValid => Value > 0;

        public bool Equals(MxRenderingCameraToken other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is MxRenderingCameraToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? Value.ToString() : "Invalid";
        }

        public static bool operator ==(MxRenderingCameraToken left, MxRenderingCameraToken right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxRenderingCameraToken left, MxRenderingCameraToken right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct MxVolumeProfileReference : IEquatable<MxVolumeProfileReference>
    {
        public MxVolumeProfileReference(string key, VolumeProfile profile = null)
        {
            Key = key ?? string.Empty;
            Profile = profile;
        }

        public string Key { get; }
        public VolumeProfile Profile { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Key) || Profile != null;

        public bool Equals(MxVolumeProfileReference other)
        {
            bool hasKey = !string.IsNullOrWhiteSpace(Key);
            bool otherHasKey = !string.IsNullOrWhiteSpace(other.Key);
            if (hasKey || otherHasKey)
                return hasKey && otherHasKey && string.Equals(Key, other.Key, StringComparison.Ordinal);

            return ReferenceEquals(Profile, other.Profile);
        }

        public override bool Equals(object obj)
        {
            return obj is MxVolumeProfileReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (!string.IsNullOrWhiteSpace(Key))
                return StringComparer.Ordinal.GetHashCode(Key);

            return Profile != null ? Profile.GetHashCode() : 0;
        }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(Key))
                return Key;

            return Profile != null ? Profile.name : "Invalid";
        }

        public static bool operator ==(MxVolumeProfileReference left, MxVolumeProfileReference right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxVolumeProfileReference left, MxVolumeProfileReference right)
        {
            return !left.Equals(right);
        }
    }

    public enum MxVolumeRequestScopeKind
    {
        Global = 0,
        CameraKind = 1,
        ExplicitCamera = 2
    }

    public readonly struct MxVolumeRequestScope : IEquatable<MxVolumeRequestScope>
    {
        private MxVolumeRequestScope(MxVolumeRequestScopeKind kind, MxCameraRenderKind cameraKind, MxRenderingCameraToken cameraToken)
        {
            Kind = kind;
            CameraKind = cameraKind;
            CameraToken = cameraToken;
        }

        public MxVolumeRequestScopeKind Kind { get; }
        public MxCameraRenderKind CameraKind { get; }
        public MxRenderingCameraToken CameraToken { get; }

        public static MxVolumeRequestScope Global()
        {
            return new MxVolumeRequestScope(MxVolumeRequestScopeKind.Global, MxCameraRenderKind.Unknown, default);
        }

        public static MxVolumeRequestScope ForCameraKind(MxCameraRenderKind cameraKind)
        {
            return new MxVolumeRequestScope(MxVolumeRequestScopeKind.CameraKind, cameraKind, default);
        }

        public static MxVolumeRequestScope ForExplicitCamera(MxRenderingCameraToken cameraToken)
        {
            return new MxVolumeRequestScope(MxVolumeRequestScopeKind.ExplicitCamera, MxCameraRenderKind.Unknown, cameraToken);
        }

        public bool IsVisibleTo(in MxVolumeEvaluationContext context)
        {
            switch (Kind)
            {
                case MxVolumeRequestScopeKind.Global:
                    return true;
                case MxVolumeRequestScopeKind.CameraKind:
                    return CameraKind == context.CameraKind;
                case MxVolumeRequestScopeKind.ExplicitCamera:
                    return CameraToken.IsValid && CameraToken == context.CameraToken;
                default:
                    return false;
            }
        }

        public bool Equals(MxVolumeRequestScope other)
        {
            return Kind == other.Kind && CameraKind == other.CameraKind && CameraToken == other.CameraToken;
        }

        public override bool Equals(object obj)
        {
            return obj is MxVolumeRequestScope other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ (int)CameraKind;
                hashCode = (hashCode * 397) ^ CameraToken.GetHashCode();
                return hashCode;
            }
        }
    }

    public readonly struct MxVolumeBlendTiming
    {
        public MxVolumeBlendTiming(float blendInSeconds, float holdSeconds, float blendOutSeconds)
        {
            if (blendInSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(blendInSeconds), blendInSeconds, "Blend-in duration must be non-negative.");
            if (holdSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(holdSeconds), holdSeconds, "Hold duration must be non-negative.");
            if (blendOutSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(blendOutSeconds), blendOutSeconds, "Blend-out duration must be non-negative.");

            BlendInSeconds = blendInSeconds;
            HoldSeconds = holdSeconds;
            BlendOutSeconds = blendOutSeconds;
        }

        public float BlendInSeconds { get; }
        public float HoldSeconds { get; }
        public float BlendOutSeconds { get; }
    }

    public readonly struct MxVolumeRequestDescriptor
    {
        public MxVolumeRequestDescriptor(
            MxVolumeProfileReference profile,
            MxVolumeRequestScope scope,
            int priority,
            MxVolumeBlendTiming timing,
            string debugName = null)
        {
            if (!profile.IsValid)
                throw new ArgumentException("Volume request profile reference must contain a stable key, a VolumeProfile reference, or both.", nameof(profile));

            Profile = profile;
            Scope = scope;
            Priority = priority;
            Timing = timing;
            DebugName = debugName ?? string.Empty;
        }

        public MxVolumeProfileReference Profile { get; }
        public MxVolumeRequestScope Scope { get; }
        public int Priority { get; }
        public MxVolumeBlendTiming Timing { get; }
        public string DebugName { get; }
    }

    public interface IVolumeBlender
    {
        MxVolumeRequestId Request(in MxVolumeRequestDescriptor descriptor);
        bool Release(MxVolumeRequestId requestId);
        bool TryGetRequest(MxVolumeRequestId requestId, out MxVolumeRequestSnapshot request);
        MxVolumeBlendStateSnapshot CaptureBlendState(in MxVolumeEvaluationContext context);
        MxVolumeDiagnosticsSnapshot CaptureDiagnostics();
    }

    public readonly struct MxVolumeEvaluationContext : IEquatable<MxVolumeEvaluationContext>
    {
        public MxVolumeEvaluationContext(MxCameraRenderKind cameraKind, MxRenderingCameraToken cameraToken, float presentationTimeSeconds)
        {
            CameraKind = cameraKind;
            CameraToken = cameraToken;
            PresentationTimeSeconds = Mathf.Max(0f, presentationTimeSeconds);
        }

        public MxCameraRenderKind CameraKind { get; }
        public MxRenderingCameraToken CameraToken { get; }
        public float PresentationTimeSeconds { get; }

        public bool Equals(MxVolumeEvaluationContext other)
        {
            return CameraKind == other.CameraKind
                && CameraToken == other.CameraToken
                && PresentationTimeSeconds.Equals(other.PresentationTimeSeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is MxVolumeEvaluationContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)CameraKind;
                hashCode = (hashCode * 397) ^ CameraToken.GetHashCode();
                hashCode = (hashCode * 397) ^ PresentationTimeSeconds.GetHashCode();
                return hashCode;
            }
        }
    }

    public enum MxVolumeRequestPhase
    {
        BlendIn = 0,
        Hold = 1,
        BlendOut = 2,
        Expired = 3,
        Released = 4
    }

    public enum MxVolumeRequestCleanupReason
    {
        None = 0,
        AutoExpired = 1,
        Released = 2
    }

    public readonly struct MxVolumeRequestSnapshot
    {
        public MxVolumeRequestSnapshot(
            MxVolumeRequestId requestId,
            MxVolumeProfileReference profile,
            MxVolumeRequestScope scope,
            int priority,
            MxVolumeRequestPhase phase,
            float weight,
            ulong creationSequence,
            string debugName,
            MxVolumeRequestCleanupReason cleanupReason = MxVolumeRequestCleanupReason.None)
        {
            RequestId = requestId;
            Profile = profile;
            Scope = scope;
            Priority = priority;
            Phase = phase;
            Weight = Mathf.Clamp01(weight);
            CreationSequence = creationSequence;
            DebugName = debugName ?? string.Empty;
            CleanupReason = cleanupReason;
        }

        public MxVolumeRequestId RequestId { get; }
        public MxVolumeProfileReference Profile { get; }
        public MxVolumeRequestScope Scope { get; }
        public int Priority { get; }
        public MxVolumeRequestPhase Phase { get; }
        public float Weight { get; }
        public ulong CreationSequence { get; }
        public string DebugName { get; }
        public MxVolumeRequestCleanupReason CleanupReason { get; }
    }

    public readonly struct MxVolumeAppliedProfileSnapshot
    {
        public MxVolumeAppliedProfileSnapshot(MxVolumeProfileReference profile, float weight, int priority, MxVolumeRequestId sourceRequestId)
        {
            Profile = profile;
            Weight = Mathf.Clamp01(weight);
            Priority = priority;
            SourceRequestId = sourceRequestId;
        }

        public MxVolumeProfileReference Profile { get; }
        public float Weight { get; }
        public int Priority { get; }
        public MxVolumeRequestId SourceRequestId { get; }
    }

    public readonly struct MxVolumeBlendStateSnapshot
    {
        private readonly List<MxVolumeRequestSnapshot> _activeRequests;
        private readonly List<MxVolumeRequestSnapshot> _suppressedRequests;
        private readonly List<MxVolumeAppliedProfileSnapshot> _appliedProfiles;

        public MxVolumeBlendStateSnapshot(
            MxVolumeEvaluationContext context,
            IReadOnlyList<MxVolumeRequestSnapshot> activeRequests,
            IReadOnlyList<MxVolumeRequestSnapshot> suppressedRequests,
            IReadOnlyList<MxVolumeAppliedProfileSnapshot> appliedProfiles)
        {
            Context = context;
            _activeRequests = activeRequests != null ? new List<MxVolumeRequestSnapshot>(activeRequests) : new List<MxVolumeRequestSnapshot>();
            _suppressedRequests = suppressedRequests != null ? new List<MxVolumeRequestSnapshot>(suppressedRequests) : new List<MxVolumeRequestSnapshot>();
            _appliedProfiles = appliedProfiles != null ? new List<MxVolumeAppliedProfileSnapshot>(appliedProfiles) : new List<MxVolumeAppliedProfileSnapshot>();
        }

        public MxVolumeEvaluationContext Context { get; }
        public IReadOnlyList<MxVolumeRequestSnapshot> ActiveRequests => _activeRequests != null ? (IReadOnlyList<MxVolumeRequestSnapshot>)_activeRequests : Array.Empty<MxVolumeRequestSnapshot>();
        public IReadOnlyList<MxVolumeRequestSnapshot> SuppressedRequests => _suppressedRequests != null ? (IReadOnlyList<MxVolumeRequestSnapshot>)_suppressedRequests : Array.Empty<MxVolumeRequestSnapshot>();
        public IReadOnlyList<MxVolumeAppliedProfileSnapshot> AppliedProfiles => _appliedProfiles != null ? (IReadOnlyList<MxVolumeAppliedProfileSnapshot>)_appliedProfiles : Array.Empty<MxVolumeAppliedProfileSnapshot>();
    }

    public readonly struct MxVolumeDiagnosticsSnapshot
    {
        private readonly List<MxVolumeRequestSnapshot> _activeRequests;
        private readonly List<MxVolumeRequestSnapshot> _expiredRequests;
        private readonly List<MxVolumeBlendStateSnapshot> _recentBlendStates;

        public MxVolumeDiagnosticsSnapshot(
            IReadOnlyList<MxVolumeRequestSnapshot> activeRequests,
            IReadOnlyList<MxVolumeRequestSnapshot> expiredRequests,
            IReadOnlyList<MxVolumeBlendStateSnapshot> recentBlendStates)
        {
            _activeRequests = activeRequests != null ? new List<MxVolumeRequestSnapshot>(activeRequests) : new List<MxVolumeRequestSnapshot>();
            _expiredRequests = expiredRequests != null ? new List<MxVolumeRequestSnapshot>(expiredRequests) : new List<MxVolumeRequestSnapshot>();
            _recentBlendStates = recentBlendStates != null ? new List<MxVolumeBlendStateSnapshot>(recentBlendStates) : new List<MxVolumeBlendStateSnapshot>();
        }

        public IReadOnlyList<MxVolumeRequestSnapshot> ActiveRequests => _activeRequests != null ? (IReadOnlyList<MxVolumeRequestSnapshot>)_activeRequests : Array.Empty<MxVolumeRequestSnapshot>();
        public IReadOnlyList<MxVolumeRequestSnapshot> ExpiredRequests => _expiredRequests != null ? (IReadOnlyList<MxVolumeRequestSnapshot>)_expiredRequests : Array.Empty<MxVolumeRequestSnapshot>();
        public IReadOnlyList<MxVolumeBlendStateSnapshot> RecentBlendStates => _recentBlendStates != null ? (IReadOnlyList<MxVolumeBlendStateSnapshot>)_recentBlendStates : Array.Empty<MxVolumeBlendStateSnapshot>();
    }

    public sealed class VolumeBlender : IVolumeBlender
    {
        private const int MaxExpiredSnapshots = 32;
        private const int MaxRecentBlendStates = 16;

        private readonly Dictionary<MxVolumeRequestId, RequestState> _requests = new Dictionary<MxVolumeRequestId, RequestState>();
        private readonly List<MxVolumeRequestSnapshot> _expiredRequests = new List<MxVolumeRequestSnapshot>();
        private readonly List<MxVolumeBlendStateSnapshot> _recentBlendStates = new List<MxVolumeBlendStateSnapshot>();
        private readonly List<MxVolumeRequestSnapshot> _scratchVisible = new List<MxVolumeRequestSnapshot>();
        private readonly List<MxVolumeRequestSnapshot> _scratchSuppressed = new List<MxVolumeRequestSnapshot>();

        private ulong _nextRequestId = 1;
        private ulong _nextCreationSequence = 1;
        private float _lastPresentationTimeSeconds;

        public MxVolumeRequestId Request(in MxVolumeRequestDescriptor descriptor)
        {
            var requestId = new MxVolumeRequestId(_nextRequestId++);
            var state = new RequestState(
                requestId,
                descriptor,
                _nextCreationSequence++,
                _lastPresentationTimeSeconds);

            _requests.Add(requestId, state);
            return requestId;
        }

        public bool Release(MxVolumeRequestId requestId)
        {
            if (!_requests.TryGetValue(requestId, out RequestState state))
                return false;

            if (state.ReleaseTimeSeconds.HasValue)
                return false;

            float weight = state.Evaluate(_lastPresentationTimeSeconds).Weight;
            state.ReleaseTimeSeconds = _lastPresentationTimeSeconds;
            state.ReleaseStartWeight = weight;
            _requests[requestId] = state;
            return true;
        }

        public bool TryGetRequest(MxVolumeRequestId requestId, out MxVolumeRequestSnapshot request)
        {
            if (_requests.TryGetValue(requestId, out RequestState state))
            {
                request = state.Evaluate(_lastPresentationTimeSeconds);
                return true;
            }

            request = default;
            return false;
        }

        public MxVolumeBlendStateSnapshot CaptureBlendState(in MxVolumeEvaluationContext context)
        {
            _lastPresentationTimeSeconds = context.PresentationTimeSeconds;
            _scratchVisible.Clear();
            _scratchSuppressed.Clear();

            var expiredIds = new List<MxVolumeRequestId>();
            foreach (RequestState state in _requests.Values)
            {
                MxVolumeRequestSnapshot snapshot = state.Evaluate(context.PresentationTimeSeconds);
                if (snapshot.Phase == MxVolumeRequestPhase.Expired)
                {
                    expiredIds.Add(snapshot.RequestId);
                    AddExpired(snapshot);
                    continue;
                }

                if (!snapshot.Scope.IsVisibleTo(context))
                    continue;

                _scratchVisible.Add(snapshot);
            }

            for (int i = 0; i < expiredIds.Count; i++)
                _requests.Remove(expiredIds[i]);

            _scratchVisible.Sort(CompareByWinnerOrder);
            var applied = new List<MxVolumeAppliedProfileSnapshot>();
            int winnerIndex = -1;
            for (int i = 0; i < _scratchVisible.Count; i++)
            {
                if (_scratchVisible[i].Weight <= 0f)
                    continue;

                winnerIndex = i;
                break;
            }

            if (winnerIndex >= 0)
            {
                MxVolumeRequestSnapshot winner = _scratchVisible[winnerIndex];
                applied.Add(new MxVolumeAppliedProfileSnapshot(winner.Profile, winner.Weight, winner.Priority, winner.RequestId));

                for (int i = 0; i < _scratchVisible.Count; i++)
                {
                    if (i == winnerIndex || _scratchVisible[i].Weight <= 0f)
                        continue;

                    _scratchSuppressed.Add(_scratchVisible[i]);
                }
            }

            var stateSnapshot = new MxVolumeBlendStateSnapshot(
                context,
                _scratchVisible,
                _scratchSuppressed,
                applied);
            AddRecentBlendState(stateSnapshot);
            return stateSnapshot;
        }

        public MxVolumeDiagnosticsSnapshot CaptureDiagnostics()
        {
            var active = new List<MxVolumeRequestSnapshot>(_requests.Count);
            foreach (RequestState state in _requests.Values)
            {
                MxVolumeRequestSnapshot snapshot = state.Evaluate(_lastPresentationTimeSeconds);
                if (snapshot.Phase != MxVolumeRequestPhase.Expired)
                    active.Add(snapshot);
            }

            active.Sort(CompareByWinnerOrder);
            return new MxVolumeDiagnosticsSnapshot(active, _expiredRequests, _recentBlendStates);
        }

        public void SetPresentationTime(float presentationTimeSeconds)
        {
            _lastPresentationTimeSeconds = Mathf.Max(0f, presentationTimeSeconds);
        }

        private static int CompareByWinnerOrder(MxVolumeRequestSnapshot left, MxVolumeRequestSnapshot right)
        {
            int priorityCompare = right.Priority.CompareTo(left.Priority);
            if (priorityCompare != 0)
                return priorityCompare;

            int sequenceCompare = left.CreationSequence.CompareTo(right.CreationSequence);
            if (sequenceCompare != 0)
                return sequenceCompare;

            return left.RequestId.Value.CompareTo(right.RequestId.Value);
        }

        private void AddExpired(MxVolumeRequestSnapshot snapshot)
        {
            _expiredRequests.Add(snapshot);
            if (_expiredRequests.Count > MaxExpiredSnapshots)
                _expiredRequests.RemoveAt(0);
        }

        private void AddRecentBlendState(MxVolumeBlendStateSnapshot snapshot)
        {
            _recentBlendStates.Add(snapshot);
            if (_recentBlendStates.Count > MaxRecentBlendStates)
                _recentBlendStates.RemoveAt(0);
        }

        private struct RequestState
        {
            public RequestState(
                MxVolumeRequestId requestId,
                MxVolumeRequestDescriptor descriptor,
                ulong creationSequence,
                float startTimeSeconds)
            {
                RequestId = requestId;
                Descriptor = descriptor;
                CreationSequence = creationSequence;
                StartTimeSeconds = startTimeSeconds;
                ReleaseTimeSeconds = null;
                ReleaseStartWeight = 1f;
            }

            public MxVolumeRequestId RequestId { get; }
            public MxVolumeRequestDescriptor Descriptor { get; }
            public ulong CreationSequence { get; }
            public float StartTimeSeconds { get; }
            public float? ReleaseTimeSeconds { get; set; }
            public float ReleaseStartWeight { get; set; }

            public MxVolumeRequestSnapshot Evaluate(float presentationTimeSeconds)
            {
                float elapsed = Mathf.Max(0f, presentationTimeSeconds - StartTimeSeconds);
                if (ReleaseTimeSeconds.HasValue)
                    return EvaluateRelease(presentationTimeSeconds);

                MxVolumeBlendTiming timing = Descriptor.Timing;
                if (timing.BlendInSeconds > 0f && elapsed < timing.BlendInSeconds)
                {
                    return Snapshot(MxVolumeRequestPhase.BlendIn, elapsed / timing.BlendInSeconds, MxVolumeRequestCleanupReason.None);
                }

                if (timing.HoldSeconds <= 0f)
                    return Snapshot(MxVolumeRequestPhase.Hold, 1f, MxVolumeRequestCleanupReason.None);

                float holdElapsed = elapsed - timing.BlendInSeconds;
                if (holdElapsed < timing.HoldSeconds)
                    return Snapshot(MxVolumeRequestPhase.Hold, 1f, MxVolumeRequestCleanupReason.None);

                float blendOutElapsed = holdElapsed - timing.HoldSeconds;
                if (timing.BlendOutSeconds <= 0f || blendOutElapsed >= timing.BlendOutSeconds)
                    return Snapshot(MxVolumeRequestPhase.Expired, 0f, MxVolumeRequestCleanupReason.AutoExpired);

                float weight = 1f - (blendOutElapsed / timing.BlendOutSeconds);
                return Snapshot(MxVolumeRequestPhase.BlendOut, weight, MxVolumeRequestCleanupReason.None);
            }

            private MxVolumeRequestSnapshot EvaluateRelease(float presentationTimeSeconds)
            {
                float releaseElapsed = Mathf.Max(0f, presentationTimeSeconds - ReleaseTimeSeconds.Value);
                float blendOutSeconds = Descriptor.Timing.BlendOutSeconds;
                if (blendOutSeconds <= 0f || releaseElapsed >= blendOutSeconds)
                    return Snapshot(MxVolumeRequestPhase.Expired, 0f, MxVolumeRequestCleanupReason.Released);

                float weight = ReleaseStartWeight * (1f - releaseElapsed / blendOutSeconds);
                return Snapshot(MxVolumeRequestPhase.Released, weight, MxVolumeRequestCleanupReason.Released);
            }

            private MxVolumeRequestSnapshot Snapshot(MxVolumeRequestPhase phase, float weight, MxVolumeRequestCleanupReason cleanupReason)
            {
                return new MxVolumeRequestSnapshot(
                    RequestId,
                    Descriptor.Profile,
                    Descriptor.Scope,
                    Descriptor.Priority,
                    phase,
                    weight,
                    CreationSequence,
                    Descriptor.DebugName,
                    cleanupReason);
            }
        }
    }
}
