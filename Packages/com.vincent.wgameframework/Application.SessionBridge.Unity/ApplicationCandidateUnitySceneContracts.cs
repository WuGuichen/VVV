using System;

namespace MxFramework.ApplicationStartup.SessionBridge.Unity
{
    public enum ApplicationCandidateUnitySceneIsolationPolicy
    {
        Invalid = 0,
        SerializedInactiveRootsWithExactMarker = 1
    }

    /// <summary>Immutable authoring-approved description of one candidate Unity scene artifact.</summary>
    /// <remarks>Construction is LowFreqAlloc; getters are NoAlloc.</remarks>
    public sealed class ApplicationCandidateUnitySceneManifest
    {
        public ApplicationCandidateUnitySceneManifest(
            string sceneKey,
            string exactScenePath,
            string artifactId,
            string markerId,
            string expectedContentHash,
            long revision,
            int expectedRootCount,
            ApplicationCandidateUnitySceneIsolationPolicy isolationPolicy)
        {
            if (string.IsNullOrWhiteSpace(sceneKey)) throw new ArgumentException("Scene key is required.", nameof(sceneKey));
            if (string.IsNullOrWhiteSpace(exactScenePath)) throw new ArgumentException("Exact scene path is required.", nameof(exactScenePath));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentException("Artifact id is required.", nameof(artifactId));
            if (string.IsNullOrWhiteSpace(markerId)) throw new ArgumentException("Marker id is required.", nameof(markerId));
            if (string.IsNullOrWhiteSpace(expectedContentHash))
                throw new ArgumentException("Expected content hash is required.", nameof(expectedContentHash));
            if (!IsCanonicalSceneAssetPath(exactScenePath))
                throw new ArgumentException("Scene path must be a canonical Assets/.../.unity path.", nameof(exactScenePath));
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (expectedRootCount <= 0) throw new ArgumentOutOfRangeException(nameof(expectedRootCount));
            if (isolationPolicy != ApplicationCandidateUnitySceneIsolationPolicy.SerializedInactiveRootsWithExactMarker)
                throw new ArgumentOutOfRangeException(nameof(isolationPolicy));

            SceneKey = sceneKey;
            ExactScenePath = exactScenePath;
            ArtifactId = artifactId;
            MarkerId = markerId;
            ExpectedContentHash = expectedContentHash;
            Revision = revision;
            ExpectedRootCount = expectedRootCount;
            IsolationPolicy = isolationPolicy;
        }

        public string SceneKey { get; }
        public string ExactScenePath { get; }
        public string ArtifactId { get; }
        public string MarkerId { get; }
        public string ExpectedContentHash { get; }
        public long Revision { get; }
        public int ExpectedRootCount { get; }
        public ApplicationCandidateUnitySceneIsolationPolicy IsolationPolicy { get; }

        private static bool IsCanonicalSceneAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".unity", StringComparison.Ordinal) || path.IndexOf('\\') >= 0 ||
                path.IndexOf("//", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("/./", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("/../", StringComparison.Ordinal) >= 0) return false;
            return path.Length > "Assets/.unity".Length;
        }
    }

    /// <summary>Immutable proof issued by the configured artifact provider before acquisition.</summary>
    /// <remarks>Construction is LowFreqAlloc; getters are NoAlloc.</remarks>
    public sealed class ApplicationCandidateUnitySceneApprovalProof
    {
        public ApplicationCandidateUnitySceneApprovalProof(
            string approvalId,
            string artifactId,
            string exactScenePath,
            string contentHash,
            long revision,
            ApplicationCandidateUnitySceneIsolationPolicy isolationPolicy)
        {
            if (string.IsNullOrWhiteSpace(approvalId)) throw new ArgumentException("Approval id is required.", nameof(approvalId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentException("Artifact id is required.", nameof(artifactId));
            if (string.IsNullOrWhiteSpace(exactScenePath)) throw new ArgumentException("Exact scene path is required.", nameof(exactScenePath));
            if (string.IsNullOrWhiteSpace(contentHash)) throw new ArgumentException("Content hash is required.", nameof(contentHash));
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (isolationPolicy != ApplicationCandidateUnitySceneIsolationPolicy.SerializedInactiveRootsWithExactMarker)
                throw new ArgumentOutOfRangeException(nameof(isolationPolicy));
            ApprovalId = approvalId;
            ArtifactId = artifactId;
            ExactScenePath = exactScenePath;
            ContentHash = contentHash;
            Revision = revision;
            IsolationPolicy = isolationPolicy;
        }

        public string ApprovalId { get; }
        public string ArtifactId { get; }
        public string ExactScenePath { get; }
        public string ContentHash { get; }
        public long Revision { get; }
        public ApplicationCandidateUnitySceneIsolationPolicy IsolationPolicy { get; }
    }

    public readonly struct ApplicationCandidateUnityScenePreflightResult
    {
        private ApplicationCandidateUnityScenePreflightResult(
            ApplicationCandidateUnitySceneApprovalProof proof,
            string errorCode,
            string message,
            Exception exception)
        {
            Proof = proof;
            ErrorCode = errorCode ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public bool Success => Proof != null && string.IsNullOrEmpty(ErrorCode);
        public ApplicationCandidateUnitySceneApprovalProof Proof { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public static ApplicationCandidateUnityScenePreflightResult Approved(
            ApplicationCandidateUnitySceneApprovalProof proof)
        {
            if (proof == null) throw new ArgumentNullException(nameof(proof));
            return new ApplicationCandidateUnityScenePreflightResult(proof, string.Empty, string.Empty, null);
        }

        public static ApplicationCandidateUnityScenePreflightResult Rejected(
            string errorCode,
            string message,
            Exception exception = null)
        {
            if (string.IsNullOrWhiteSpace(errorCode)) throw new ArgumentException("Error code is required.", nameof(errorCode));
            return new ApplicationCandidateUnityScenePreflightResult(null, errorCode, message, exception);
        }
    }

    /// <summary>Read-only proof that the exact scene artifact is resident for loading.</summary>
    /// <remarks>Getters must be NoAlloc. The lease exposes no release or Unity object authority.</remarks>
    public interface IApplicationCandidateUnitySceneArtifactLease
    {
        string ArtifactId { get; }
        string ExactScenePath { get; }
        long Revision { get; }
        ApplicationCandidateUnitySceneApprovalProof ApprovalProof { get; }
    }

    public readonly struct ApplicationCandidateUnitySceneArtifactAcquireResult
    {
        private ApplicationCandidateUnitySceneArtifactAcquireResult(
            IApplicationCandidateUnitySceneArtifactLease lease,
            string errorCode,
            string message,
            Exception exception)
        {
            Lease = lease;
            ErrorCode = errorCode ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public bool Success => Lease != null && string.IsNullOrEmpty(ErrorCode);
        public IApplicationCandidateUnitySceneArtifactLease Lease { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public static ApplicationCandidateUnitySceneArtifactAcquireResult Succeeded(
            IApplicationCandidateUnitySceneArtifactLease lease)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));
            return new ApplicationCandidateUnitySceneArtifactAcquireResult(lease, string.Empty, string.Empty, null);
        }

        public static ApplicationCandidateUnitySceneArtifactAcquireResult Failed(
            string errorCode,
            string message,
            Exception exception = null)
        {
            if (string.IsNullOrWhiteSpace(errorCode)) throw new ArgumentException("Error code is required.", nameof(errorCode));
            return new ApplicationCandidateUnitySceneArtifactAcquireResult(null, errorCode, message, exception);
        }
    }

    /// <summary>Low-frequency artifact residency boundary used before Unity scene mutation.</summary>
    public interface IApplicationCandidateUnitySceneArtifactProvider
    {
        ApplicationCandidateUnityScenePreflightResult Preflight(
            ApplicationCandidateUnitySceneManifest manifest);

        ApplicationCandidateUnitySceneArtifactAcquireResult Acquire(
            ApplicationCandidateUnitySceneManifest manifest,
            ApplicationCandidateUnitySceneApprovalProof approvalProof);

        /// <summary>Releases a lease previously returned by this provider.</summary>
        /// <remarks>LowFreqAlloc on failure paths. Called only after unload reaches a true terminal state.</remarks>
        void Release(IApplicationCandidateUnitySceneArtifactLease lease);
    }
}
