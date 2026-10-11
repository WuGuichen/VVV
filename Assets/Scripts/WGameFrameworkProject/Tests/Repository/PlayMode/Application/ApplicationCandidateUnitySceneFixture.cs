using System;
using System.IO;
using System.Collections.Generic;
using MxFramework.ApplicationStartup.SessionBridge.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.PlayMode.Application
{
    public static class ApplicationCandidateUnitySceneFixture
    {
        public const string Schema = "mx.application.candidate-unity-scene-fixture.v1";
        public const string SceneKey = "mx.tests.candidate-session.unity";
        public const string ScenePath = "Assets/Scenes/Tests/ApplicationCandidateUnitySceneFixture.unity";
        public const string DecoyScenePath = "Assets/Scenes/Tests/Decoy/ApplicationCandidateUnitySceneFixture.unity";
        public const string DecoyManifestPath = "Assets/Scenes/Tests/Decoy/ApplicationCandidateUnitySceneFixture.manifest.json";
        public const string UnapprovedActiveRootScenePath = "Assets/Scenes/Tests/ApplicationCandidateUnitySceneActiveRootFixture.unity";
        public const string ManifestPath = "Assets/Scenes/Tests/ApplicationCandidateUnitySceneFixture.manifest.json";
        public const string UnapprovedManifestPath = "Assets/Scenes/Tests/ApplicationCandidateUnitySceneActiveRootFixture.manifest.json";
        public const string ArtifactId = "mx.tests.artifact.candidate-unity-scene";
        public const string DecoyArtifactId = "mx.tests.artifact.candidate-unity-scene-decoy";
        public const string MarkerId = "mx.tests.marker.candidate-unity-scene";
        public const long Revision = 1;
        public const int ExpectedRootCount = 2;

        [Serializable]
        public sealed class ManifestDocument
        {
            public string schema;
            public string sceneKey;
            public string exactScenePath;
            public string artifactId;
            public string markerId;
            public long revision;
            public int expectedRootCount;
            public string dependencyHash;
            public bool approved;
        }

        public static ManifestDocument LoadDocument(string path)
        {
            string absolutePath = Path.GetFullPath(path);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException("Candidate Unity fixture manifest is missing.", absolutePath);
            ManifestDocument document = JsonUtility.FromJson<ManifestDocument>(File.ReadAllText(absolutePath));
            if (document == null) throw new InvalidDataException("Candidate Unity fixture manifest is invalid: " + path);
            return document;
        }

        public static ApplicationCandidateUnitySceneManifest LoadApprovedManifest()
        {
            ManifestDocument document = LoadDocument(ManifestPath);
            if (!document.approved) throw new InvalidDataException("Candidate Unity fixture was not approved.");
            return ToRuntimeManifest(document);
        }

        public static ApplicationCandidateUnitySceneManifest ToRuntimeManifest(ManifestDocument document) =>
            new ApplicationCandidateUnitySceneManifest(
                document.sceneKey,
                document.exactScenePath,
                document.artifactId,
                document.markerId,
                document.dependencyHash,
                document.revision,
                document.expectedRootCount,
                ApplicationCandidateUnitySceneIsolationPolicy.SerializedInactiveRootsWithExactMarker);

        public static bool Matches(ManifestDocument left, ManifestDocument right) =>
            left != null && right != null &&
            string.Equals(left.schema, right.schema, StringComparison.Ordinal) &&
            string.Equals(left.sceneKey, right.sceneKey, StringComparison.Ordinal) &&
            string.Equals(left.exactScenePath, right.exactScenePath, StringComparison.Ordinal) &&
            string.Equals(left.artifactId, right.artifactId, StringComparison.Ordinal) &&
            string.Equals(left.markerId, right.markerId, StringComparison.Ordinal) &&
            left.revision == right.revision &&
            left.expectedRootCount == right.expectedRootCount &&
            string.Equals(left.dependencyHash, right.dependencyHash, StringComparison.Ordinal) &&
            left.approved == right.approved;
    }

    /// <summary>Test-only provider that treats the generated manifest document as frozen approval authority.</summary>
    public sealed class RecordingCandidateUnitySceneArtifactProvider :
        IApplicationCandidateUnitySceneArtifactProvider
    {
        private readonly string _manifestPath;
        private readonly bool _leaseArtifactMismatch;
        private readonly Func<bool> _releaseInvariant;
        private ApplicationCandidateUnitySceneFixture.ManifestDocument _frozenDocument;
        private ApplicationCandidateUnitySceneApprovalProof _issuedProof;

        public RecordingCandidateUnitySceneArtifactProvider(
            string manifestPath = ApplicationCandidateUnitySceneFixture.ManifestPath,
            bool leaseArtifactMismatch = false,
            Func<bool> releaseInvariant = null,
            bool throwOnRelease = false)
        {
            _manifestPath = manifestPath;
            _leaseArtifactMismatch = leaseArtifactMismatch;
            _releaseInvariant = releaseInvariant;
            ReleaseException = throwOnRelease
                ? new InvalidOperationException("Injected artifact release failure.")
                : null;
        }

        public List<string> Events { get; } = new List<string>();
        public int PreflightCount { get; private set; }
        public int AcquireCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public Exception ReleaseException { get; }

        public ApplicationCandidateUnityScenePreflightResult Preflight(
            ApplicationCandidateUnitySceneManifest manifest)
        {
            Events.Add("preflight");
            PreflightCount++;
            ApplicationCandidateUnitySceneFixture.ManifestDocument document =
                ApplicationCandidateUnitySceneFixture.LoadDocument(_manifestPath);
            if (!document.approved || !MatchesManifest(document, manifest))
                return ApplicationCandidateUnityScenePreflightResult.Rejected(
                    "MX_TEST_MANIFEST_REJECTED",
                    "The frozen fixture manifest did not approve the exact candidate request.");

            _frozenDocument = document;
            _issuedProof = new ApplicationCandidateUnitySceneApprovalProof(
                "mx.tests.approval." + document.dependencyHash,
                document.artifactId,
                document.exactScenePath,
                document.dependencyHash,
                document.revision,
                ApplicationCandidateUnitySceneIsolationPolicy.SerializedInactiveRootsWithExactMarker);
            return ApplicationCandidateUnityScenePreflightResult.Approved(_issuedProof);
        }

        public ApplicationCandidateUnitySceneArtifactAcquireResult Acquire(
            ApplicationCandidateUnitySceneManifest manifest,
            ApplicationCandidateUnitySceneApprovalProof approvalProof)
        {
            Events.Add("acquire");
            AcquireCount++;
            ApplicationCandidateUnitySceneFixture.ManifestDocument observed =
                ApplicationCandidateUnitySceneFixture.LoadDocument(_manifestPath);
            if (_frozenDocument == null ||
                !ApplicationCandidateUnitySceneFixture.Matches(_frozenDocument, observed) ||
                !MatchesManifest(observed, manifest) ||
                !ReferenceEquals(approvalProof, _issuedProof) ||
                !MatchesProof(observed, approvalProof))
                return ApplicationCandidateUnitySceneArtifactAcquireResult.Failed(
                    "MX_TEST_APPROVAL_CHANGED",
                    "The fixture approval changed between preflight and artifact acquisition.");

            return ApplicationCandidateUnitySceneArtifactAcquireResult.Succeeded(
                new Lease(
                    _leaseArtifactMismatch ? "wrong-artifact" : observed.artifactId,
                    observed.exactScenePath,
                    observed.revision,
                    approvalProof));
        }

        public void Release(IApplicationCandidateUnitySceneArtifactLease lease)
        {
            if (_releaseInvariant != null)
                Assert.IsTrue(_releaseInvariant(),
                    "Artifact release ran before the exact candidate Scene was no longer loaded.");
            Events.Add("release");
            ReleaseCount++;
            if (ReleaseException != null) throw ReleaseException;
        }

        private static bool MatchesManifest(
            ApplicationCandidateUnitySceneFixture.ManifestDocument document,
            ApplicationCandidateUnitySceneManifest manifest) =>
            document != null && manifest != null &&
            string.Equals(document.schema, ApplicationCandidateUnitySceneFixture.Schema, StringComparison.Ordinal) &&
            string.Equals(document.sceneKey, manifest.SceneKey, StringComparison.Ordinal) &&
            string.Equals(document.exactScenePath, manifest.ExactScenePath, StringComparison.Ordinal) &&
            string.Equals(document.artifactId, manifest.ArtifactId, StringComparison.Ordinal) &&
            string.Equals(document.markerId, manifest.MarkerId, StringComparison.Ordinal) &&
            string.Equals(document.dependencyHash, manifest.ExpectedContentHash, StringComparison.Ordinal) &&
            document.revision == manifest.Revision &&
            document.expectedRootCount == manifest.ExpectedRootCount &&
            manifest.IsolationPolicy ==
                ApplicationCandidateUnitySceneIsolationPolicy.SerializedInactiveRootsWithExactMarker;

        private static bool MatchesProof(
            ApplicationCandidateUnitySceneFixture.ManifestDocument document,
            ApplicationCandidateUnitySceneApprovalProof proof) =>
            proof != null &&
            string.Equals(document.artifactId, proof.ArtifactId, StringComparison.Ordinal) &&
            string.Equals(document.exactScenePath, proof.ExactScenePath, StringComparison.Ordinal) &&
            string.Equals(document.dependencyHash, proof.ContentHash, StringComparison.Ordinal) &&
            document.revision == proof.Revision &&
            proof.IsolationPolicy ==
                ApplicationCandidateUnitySceneIsolationPolicy.SerializedInactiveRootsWithExactMarker;

        private sealed class Lease : IApplicationCandidateUnitySceneArtifactLease
        {
            public Lease(
                string artifactId,
                string exactScenePath,
                long revision,
                ApplicationCandidateUnitySceneApprovalProof approvalProof)
            {
                ArtifactId = artifactId;
                ExactScenePath = exactScenePath;
                Revision = revision;
                ApprovalProof = approvalProof;
            }

            public string ArtifactId { get; }
            public string ExactScenePath { get; }
            public long Revision { get; }
            public ApplicationCandidateUnitySceneApprovalProof ApprovalProof { get; }
        }
    }
}
