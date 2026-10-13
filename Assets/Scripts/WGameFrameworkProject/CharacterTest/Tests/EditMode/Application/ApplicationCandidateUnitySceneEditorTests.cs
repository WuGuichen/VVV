using System;
using System.Threading.Tasks;
using MxFramework.ApplicationStartup.SessionBridge;
using MxFramework.ApplicationStartup.SessionBridge.Unity;
using MxFramework.ApplicationStartup.SessionBridge.Unity.Editor;
using MxFramework.Runtime;
using MxFramework.Tests.PlayMode.Application;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.ApplicationStartup
{
    public sealed class ApplicationCandidateUnitySceneEditorTests
    {
        [Test]
        public void GeneratedFixture_ValidatorAcceptsExactApprovedUnapprovedAndDecoyEvidence() =>
            ApplicationCandidateUnitySceneFixtureValidator.ValidateOrThrow();

        [Test]
        public void Manifest_InvalidAuthorityFieldsFailClosed()
        {
            Assert.Throws<ArgumentException>(() => Manifest(sceneKey: ""));
            Assert.Throws<ArgumentException>(() => Manifest(scenePath: ""));
            Assert.Throws<ArgumentException>(() => Manifest(artifactId: ""));
            Assert.Throws<ArgumentException>(() => Manifest(markerId: ""));
            Assert.Throws<ArgumentException>(() => Manifest(contentHash: ""));
            Assert.Throws<ArgumentException>(() => Manifest(scenePath: "Assets\\Scenes\\candidate.unity"));
            Assert.Throws<ArgumentException>(() => Manifest(scenePath: "Assets/Scenes/../candidate.unity"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Manifest(revision: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Manifest(rootCount: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Manifest(
                policy: ApplicationCandidateUnitySceneIsolationPolicy.Invalid));
        }

        [Test]
        public void Factory_CreateIsFreshAndWorkerThreadIsRejected()
        {
            var factory = new ApplicationCandidateUnitySceneGraphFactory();
            var provider = new RecordingCandidateUnitySceneArtifactProvider();
            ApplicationCandidateSceneGraph first = factory.Create(
                Manifest(), provider, ApplicationControllerPumpStrategy.Standalone);
            ApplicationCandidateSceneGraph second = factory.Create(
                Manifest(), provider, ApplicationControllerPumpStrategy.Standalone);
            Assert.AreNotSame(first.Controller, second.Controller);
            Assert.AreNotSame(first.IsolationGate, second.IsolationGate);
            Assert.AreNotSame(first.IsolationGate.SceneOwnershipIdentity,
                second.IsolationGate.SceneOwnershipIdentity);

            AggregateException aggregate = Assert.Throws<AggregateException>(() =>
                Task.Run(() => factory.Create(
                    Manifest(), provider, ApplicationControllerPumpStrategy.Standalone)).Wait());
            Assert.IsInstanceOf<InvalidOperationException>(aggregate.InnerException);
        }

        [Test]
        public void ArtifactLeaseMismatch_ReleasesBeforeTerminalFailureAndNeverLoadsScene()
        {
            var provider = new RecordingCandidateUnitySceneArtifactProvider(leaseArtifactMismatch: true);
            ApplicationCandidateSceneGraph graph = new ApplicationCandidateUnitySceneGraphFactory().Create(
                Manifest(), provider, ApplicationControllerPumpStrategy.Standalone);
            BeginAndTick(graph);

            Assert.AreEqual(SceneFlowResultStatus.Failed, graph.Controller.LastResult.Status);
            CollectionAssert.AreEqual(new[] { "preflight", "acquire", "release" }, provider.Events);
            Assert.AreEqual(1, provider.ReleaseCount);
            Assert.IsFalse(graph.IsolationGate.HasOwnedScene);
        }

        [Test]
        public void ManifestContentMismatch_RejectsDuringRealPreflightBeforeAcquire()
        {
            var provider = new RecordingCandidateUnitySceneArtifactProvider();
            ApplicationCandidateSceneGraph graph = new ApplicationCandidateUnitySceneGraphFactory().Create(
                Manifest(contentHash: "wrong-content-hash"),
                provider,
                ApplicationControllerPumpStrategy.Standalone);
            BeginAndTick(graph);

            Assert.AreEqual(SceneFlowResultStatus.Failed, graph.Controller.LastResult.Status);
            CollectionAssert.AreEqual(new[] { "preflight" }, provider.Events);
            Assert.AreEqual(0, provider.AcquireCount);
            Assert.IsFalse(graph.IsolationGate.HasOwnedScene);
        }

        [Test]
        public void UnapprovedActiveRootEvidence_IsNaturallyRejectedBeforeAcquireOrSceneLoad()
        {
            ApplicationCandidateUnitySceneFixture.ManifestDocument document =
                ApplicationCandidateUnitySceneFixture.LoadDocument(
                    ApplicationCandidateUnitySceneFixture.UnapprovedManifestPath);
            Assert.IsFalse(document.approved);
            Assert.IsFalse(SceneManager.GetSceneByPath(document.exactScenePath).isLoaded);
            var provider = new RecordingCandidateUnitySceneArtifactProvider(
                ApplicationCandidateUnitySceneFixture.UnapprovedManifestPath);
            ApplicationCandidateSceneGraph graph = new ApplicationCandidateUnitySceneGraphFactory().Create(
                ApplicationCandidateUnitySceneFixture.ToRuntimeManifest(document),
                provider,
                ApplicationControllerPumpStrategy.Standalone);
            BeginAndTick(graph);

            Assert.AreEqual(SceneFlowResultStatus.Failed, graph.Controller.LastResult.Status);
            CollectionAssert.AreEqual(new[] { "preflight" }, provider.Events);
            Assert.AreEqual(0, provider.AcquireCount);
            Assert.IsFalse(graph.IsolationGate.HasOwnedScene);
            Assert.IsFalse(SceneManager.GetSceneByPath(document.exactScenePath).isLoaded);
        }

        [Test]
        public void ArtifactLeaseGetterFault_ReleasesLeaseAndUnlocksNextPreflight()
        {
            var sentinel = new InvalidOperationException("Injected lease approval getter fault.");
            var faulting = new FaultingLeaseProvider(sentinel);
            ApplicationCandidateSceneGraph graph = new ApplicationCandidateUnitySceneGraphFactory().Create(
                Manifest(), faulting, ApplicationControllerPumpStrategy.Standalone);
            BeginAndTick(graph);

            Assert.AreEqual(SceneFlowResultStatus.Failed, graph.Controller.LastResult.Status);
            Assert.AreSame(sentinel, graph.Controller.LastResult.Error.Exception);
            Assert.AreEqual(1, faulting.ReleaseCount);
            Assert.IsFalse(graph.IsolationGate.HasOwnedScene);

            var next = new RecordingCandidateUnitySceneArtifactProvider();
            ApplicationCandidateSceneGraph nextGraph = new ApplicationCandidateUnitySceneGraphFactory().Create(
                Manifest(contentHash: "next-preflight-mismatch"),
                next,
                ApplicationControllerPumpStrategy.Standalone);
            BeginAndTick(nextGraph);
            Assert.AreEqual(1, next.PreflightCount,
                "A lease getter fault must not retain the process mutation token after exact cleanup.");
        }

        private static void BeginAndTick(ApplicationCandidateSceneGraph graph)
        {
            Assert.IsTrue(graph.IsolationGate.TryEnterIsolation(
                new ApplicationCandidateSceneRequest(ApplicationCandidateUnitySceneFixture.SceneKey)).Success);
            Assert.AreEqual(SceneFlowResultStatus.Accepted, graph.Controller.RequestLoad(new SceneFlowRequest(
                ApplicationCandidateUnitySceneFixture.SceneKey, SceneFlowLoadMode.Additive, false)).Status);
            graph.Controller.Tick();
        }

        private static ApplicationCandidateUnitySceneManifest Manifest(
            string sceneKey = ApplicationCandidateUnitySceneFixture.SceneKey,
            string scenePath = ApplicationCandidateUnitySceneFixture.ScenePath,
            string artifactId = ApplicationCandidateUnitySceneFixture.ArtifactId,
            string markerId = ApplicationCandidateUnitySceneFixture.MarkerId,
            string contentHash = null,
            long revision = ApplicationCandidateUnitySceneFixture.Revision,
            int rootCount = ApplicationCandidateUnitySceneFixture.ExpectedRootCount,
            ApplicationCandidateUnitySceneIsolationPolicy policy =
                ApplicationCandidateUnitySceneIsolationPolicy.SerializedInactiveRootsWithExactMarker)
        {
            if (contentHash == null)
                contentHash = ApplicationCandidateUnitySceneFixture.LoadDocument(
                    ApplicationCandidateUnitySceneFixture.ManifestPath).dependencyHash;
            return new ApplicationCandidateUnitySceneManifest(
                sceneKey, scenePath, artifactId, markerId, contentHash, revision, rootCount, policy);
        }

        private sealed class FaultingLeaseProvider : IApplicationCandidateUnitySceneArtifactProvider
        {
            private readonly Exception _sentinel;
            private ApplicationCandidateUnitySceneApprovalProof _proof;

            public FaultingLeaseProvider(Exception sentinel) =>
                _sentinel = sentinel ?? throw new ArgumentNullException(nameof(sentinel));

            public int ReleaseCount { get; private set; }

            public ApplicationCandidateUnityScenePreflightResult Preflight(
                ApplicationCandidateUnitySceneManifest manifest)
            {
                _proof = new ApplicationCandidateUnitySceneApprovalProof(
                    "mx.tests.approval.faulting-lease",
                    manifest.ArtifactId,
                    manifest.ExactScenePath,
                    manifest.ExpectedContentHash,
                    manifest.Revision,
                    manifest.IsolationPolicy);
                return ApplicationCandidateUnityScenePreflightResult.Approved(_proof);
            }

            public ApplicationCandidateUnitySceneArtifactAcquireResult Acquire(
                ApplicationCandidateUnitySceneManifest manifest,
                ApplicationCandidateUnitySceneApprovalProof approvalProof) =>
                ApplicationCandidateUnitySceneArtifactAcquireResult.Succeeded(
                    new FaultingLease(manifest, approvalProof, _sentinel));

            public void Release(IApplicationCandidateUnitySceneArtifactLease lease) => ReleaseCount++;

            private sealed class FaultingLease : IApplicationCandidateUnitySceneArtifactLease
            {
                private readonly ApplicationCandidateUnitySceneApprovalProof _proof;
                private readonly Exception _sentinel;

                public FaultingLease(
                    ApplicationCandidateUnitySceneManifest manifest,
                    ApplicationCandidateUnitySceneApprovalProof proof,
                    Exception sentinel)
                {
                    ArtifactId = manifest.ArtifactId;
                    ExactScenePath = manifest.ExactScenePath;
                    Revision = manifest.Revision;
                    _proof = proof;
                    _sentinel = sentinel;
                }

                public string ArtifactId { get; }
                public string ExactScenePath { get; }
                public long Revision { get; }
                public ApplicationCandidateUnitySceneApprovalProof ApprovalProof
                {
                    get
                    {
                        GC.KeepAlive(_proof);
                        throw _sentinel;
                    }
                }
            }
        }
    }
}
