using System.Collections;
using MxFramework.ApplicationStartup.SessionBridge;
using MxFramework.ApplicationStartup.SessionBridge.Unity;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.Application
{
    public sealed class ApplicationCandidateUnityScenePlayModeTests
    {
        private ApplicationCandidateSceneGraph _activeGraph;
        private ApplicationCandidateSceneGraph _secondaryGraph;

        [UnityTearDown]
        public IEnumerator TearDownExactFixtureScenes()
        {
            yield return CleanupGraph(_secondaryGraph);
            _secondaryGraph = null;
            yield return CleanupGraph(_activeGraph);
            _activeGraph = null;

            yield return UnloadExactPath(ApplicationCandidateUnitySceneFixture.ScenePath);
            yield return UnloadExactPath(ApplicationCandidateUnitySceneFixture.DecoyScenePath);
            yield return UnloadExactPath(ApplicationCandidateUnitySceneFixture.UnapprovedActiveRootScenePath);
        }

        [UnityTest]
        public IEnumerator WaitingUnload_StartsAfterOtherCandidateLoadReleasesMutationToken()
        {
            var firstProvider = new RecordingCandidateUnitySceneArtifactProvider(
                releaseInvariant: () =>
                    !SceneManager.GetSceneByPath(ApplicationCandidateUnitySceneFixture.ScenePath).isLoaded);
            _activeGraph = CreateGraph(
                ApplicationCandidateUnitySceneFixture.LoadApprovedManifest(), firstProvider);
            BeginLoad(_activeGraph);
            yield return Pump(_activeGraph.Controller);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, _activeGraph.Controller.LastResult.Status);
            Scene firstScene = SceneManager.GetSceneByPath(ApplicationCandidateUnitySceneFixture.ScenePath);
            Assert.IsTrue(firstScene.IsValid() && firstScene.isLoaded);
            ulong firstHandle = firstScene.handle.GetRawData();

            ApplicationCandidateUnitySceneFixture.ManifestDocument decoyDocument =
                ApplicationCandidateUnitySceneFixture.LoadDocument(
                    ApplicationCandidateUnitySceneFixture.DecoyManifestPath);
            var secondProvider = new RecordingCandidateUnitySceneArtifactProvider(
                ApplicationCandidateUnitySceneFixture.DecoyManifestPath,
                releaseInvariant: () =>
                    !SceneManager.GetSceneByPath(ApplicationCandidateUnitySceneFixture.DecoyScenePath).isLoaded);
            _secondaryGraph = CreateGraph(
                ApplicationCandidateUnitySceneFixture.ToRuntimeManifest(decoyDocument), secondProvider);
            BeginLoad(_secondaryGraph);
            Assert.IsTrue(_secondaryGraph.Controller.IsBusy);

            Assert.IsTrue(_activeGraph.IsolationGate.MakeInert().Success);
            SceneFlowResult unloadAccepted =
                _activeGraph.Controller.RequestUnload(ApplicationCandidateUnitySceneFixture.SceneKey);
            Assert.AreEqual(SceneFlowResultStatus.Accepted, unloadAccepted.Status);
            Assert.IsTrue(_activeGraph.Controller.IsBusy);
            Assert.IsTrue(IsSceneLoaded(firstHandle));

            for (int i = 0; i < 300 &&
                (_secondaryGraph.Controller.IsBusy || _activeGraph.Controller.IsBusy); i++)
            {
                _secondaryGraph.Controller.Tick();
                _activeGraph.Controller.Tick();
                yield return null;
            }
            _secondaryGraph.Controller.Tick();
            _activeGraph.Controller.Tick();

            Assert.IsFalse(_secondaryGraph.Controller.IsBusy);
            Assert.IsFalse(_activeGraph.Controller.IsBusy);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded,
                _secondaryGraph.Controller.LastResult.Status,
                _secondaryGraph.Controller.LastResult.Error.Message);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded,
                _activeGraph.Controller.LastResult.Status,
                _activeGraph.Controller.LastResult.Error.Message);
            Assert.IsFalse(IsSceneLoaded(firstHandle));
            Assert.IsFalse(_activeGraph.IsolationGate.HasOwnedScene);
            Assert.AreEqual(1, firstProvider.ReleaseCount);

            Scene secondScene = SceneManager.GetSceneByPath(ApplicationCandidateUnitySceneFixture.DecoyScenePath);
            Assert.IsTrue(secondScene.IsValid() && secondScene.isLoaded);
            ulong secondHandle = secondScene.handle.GetRawData();
            Assert.IsTrue(_secondaryGraph.IsolationGate.MakeInert().Success);
            Assert.AreEqual(SceneFlowResultStatus.Accepted,
                _secondaryGraph.Controller.RequestUnload(ApplicationCandidateUnitySceneFixture.SceneKey).Status);
            yield return Pump(_secondaryGraph.Controller);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded,
                _secondaryGraph.Controller.LastResult.Status,
                _secondaryGraph.Controller.LastResult.Error.Message);
            Assert.IsFalse(IsSceneLoaded(secondHandle));
            Assert.IsFalse(_secondaryGraph.IsolationGate.HasOwnedScene);
            Assert.AreEqual(1, secondProvider.ReleaseCount);
            _secondaryGraph = null;
            _activeGraph = null;
        }

        [UnityTest]
        public IEnumerator AdditivePrepareAndRollback_PreservesHostAndSameNameDecoy()
        {
            Scene host = SceneManager.GetActiveScene();
            ulong hostHandle = host.handle.GetRawData();
            yield return LoadAdditive(ApplicationCandidateUnitySceneFixture.DecoyScenePath);
            Scene decoy = SceneManager.GetSceneByPath(ApplicationCandidateUnitySceneFixture.DecoyScenePath);
            Assert.IsTrue(decoy.IsValid() && decoy.isLoaded);
            ulong decoyHandle = decoy.handle.GetRawData();

            ApplicationCandidateUnitySceneFixtureProbe.ResetCounters();
            var provider = new RecordingCandidateUnitySceneArtifactProvider(
                releaseInvariant: () =>
                    !SceneManager.GetSceneByPath(ApplicationCandidateUnitySceneFixture.ScenePath).isLoaded);
            _activeGraph = CreateGraph(
                ApplicationCandidateUnitySceneFixture.LoadApprovedManifest(), provider);
            BeginLoad(_activeGraph);
            yield return Pump(_activeGraph.Controller);

            Assert.AreEqual(SceneFlowResultStatus.Succeeded, _activeGraph.Controller.LastResult.Status,
                _activeGraph.Controller.LastResult.Error.Message);
            Scene candidate = SceneManager.GetSceneByPath(ApplicationCandidateUnitySceneFixture.ScenePath);
            Assert.IsTrue(candidate.IsValid() && candidate.isLoaded);
            ulong candidateHandle = candidate.handle.GetRawData();
            Assert.AreNotEqual(hostHandle, candidateHandle);
            Assert.AreNotEqual(decoyHandle, candidateHandle);
            Assert.IsTrue(IsSceneLoaded(hostHandle));
            Assert.IsTrue(IsSceneLoaded(decoyHandle));
            GameObject[] roots = candidate.GetRootGameObjects();
            Assert.AreEqual(ApplicationCandidateUnitySceneFixture.ExpectedRootCount, roots.Length);
            for (int i = 0; i < roots.Length; i++) Assert.IsFalse(roots[i].activeSelf);
            Assert.AreEqual(0, ApplicationCandidateUnitySceneFixtureProbe.AwakeCount);
            Assert.AreEqual(0, ApplicationCandidateUnitySceneFixtureProbe.OnEnableCount);

            // P0 regression: a completed load must release the process mutation token. The second graph
            // reaches its real unapproved-manifest preflight instead of failing early as MutationBusy.
            ApplicationCandidateUnitySceneFixture.ManifestDocument unapproved =
                ApplicationCandidateUnitySceneFixture.LoadDocument(
                    ApplicationCandidateUnitySceneFixture.UnapprovedManifestPath);
            var secondProvider = new RecordingCandidateUnitySceneArtifactProvider(
                ApplicationCandidateUnitySceneFixture.UnapprovedManifestPath);
            ApplicationCandidateSceneGraph second = CreateGraph(
                ApplicationCandidateUnitySceneFixture.ToRuntimeManifest(unapproved), secondProvider);
            BeginLoad(second);
            second.Controller.Tick();
            Assert.AreEqual(1, secondProvider.PreflightCount);
            Assert.AreEqual(SceneFlowResultStatus.Failed, second.Controller.LastResult.Status);
            Assert.IsTrue(IsSceneLoaded(candidateHandle));

            Assert.IsTrue(_activeGraph.IsolationGate.MakeInert().Success);
            Assert.AreEqual(SceneFlowResultStatus.Accepted,
                _activeGraph.Controller.RequestUnload(ApplicationCandidateUnitySceneFixture.SceneKey).Status);
            yield return Pump(_activeGraph.Controller);

            Assert.AreEqual(SceneFlowResultStatus.Succeeded, _activeGraph.Controller.LastResult.Status,
                _activeGraph.Controller.LastResult.Error.Message);
            Assert.IsFalse(IsSceneLoaded(candidateHandle));
            Assert.IsTrue(IsSceneLoaded(hostHandle));
            Assert.IsTrue(IsSceneLoaded(decoyHandle));
            CollectionAssert.AreEqual(new[] { "preflight", "acquire", "release" }, provider.Events);
            Assert.AreEqual(1, provider.ReleaseCount);
            _activeGraph = null;
        }

        [UnityTest]
        public IEnumerator CompletedLoadThenExactRollback_ReleasesBeforeFreshRetry()
        {
            var firstProvider = new RecordingCandidateUnitySceneArtifactProvider(
                releaseInvariant: () =>
                    !SceneManager.GetSceneByPath(ApplicationCandidateUnitySceneFixture.ScenePath).isLoaded);
            _activeGraph = CreateGraph(
                ApplicationCandidateUnitySceneFixture.LoadApprovedManifest(), firstProvider);
            BeginLoad(_activeGraph);
            yield return Pump(_activeGraph.Controller);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, _activeGraph.Controller.LastResult.Status);
            Assert.AreEqual(SceneFlowResultStatus.Accepted,
                _activeGraph.Controller.RequestUnload(ApplicationCandidateUnitySceneFixture.SceneKey).Status);
            yield return Pump(_activeGraph.Controller);
            Assert.AreEqual(1, firstProvider.ReleaseCount);
            ApplicationCandidateSceneGraph first = _activeGraph;
            _activeGraph = null;

            var retryProvider = new RecordingCandidateUnitySceneArtifactProvider(
                releaseInvariant: () =>
                    !SceneManager.GetSceneByPath(ApplicationCandidateUnitySceneFixture.ScenePath).isLoaded);
            _activeGraph = CreateGraph(
                ApplicationCandidateUnitySceneFixture.LoadApprovedManifest(), retryProvider);
            Assert.AreNotSame(first.Controller, _activeGraph.Controller);
            Assert.AreNotSame(first.IsolationGate.SceneOwnershipIdentity,
                _activeGraph.IsolationGate.SceneOwnershipIdentity);
            BeginLoad(_activeGraph);
            yield return Pump(_activeGraph.Controller);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, _activeGraph.Controller.LastResult.Status,
                _activeGraph.Controller.LastResult.Error.Message);
            Assert.AreEqual(SceneFlowResultStatus.Accepted,
                _activeGraph.Controller.RequestUnload(ApplicationCandidateUnitySceneFixture.SceneKey).Status);
            yield return Pump(_activeGraph.Controller);
            Assert.AreEqual(1, retryProvider.ReleaseCount);
            _activeGraph = null;
        }

        [UnityTest]
        public IEnumerator ArtifactReleaseFailure_RetainsPermanentDebtAndBlocksDifferentCandidatePreflight()
        {
            var debtProvider = new RecordingCandidateUnitySceneArtifactProvider(
                releaseInvariant: () =>
                    !SceneManager.GetSceneByPath(ApplicationCandidateUnitySceneFixture.ScenePath).isLoaded,
                throwOnRelease: true);
            _activeGraph = CreateGraph(
                ApplicationCandidateUnitySceneFixture.LoadApprovedManifest(), debtProvider);
            var debtOwner = (CandidateUnitySceneAdapter)_activeGraph.IsolationGate;

            try
            {
                BeginLoad(_activeGraph);
                yield return Pump(_activeGraph.Controller);
                Assert.AreEqual(SceneFlowResultStatus.Succeeded, _activeGraph.Controller.LastResult.Status);
                Scene candidate = SceneManager.GetSceneByPath(ApplicationCandidateUnitySceneFixture.ScenePath);
                Assert.IsTrue(candidate.IsValid() && candidate.isLoaded);
                ulong candidateHandle = candidate.handle.GetRawData();

                Assert.IsTrue(_activeGraph.IsolationGate.MakeInert().Success);
                Assert.AreEqual(SceneFlowResultStatus.Accepted,
                    _activeGraph.Controller.RequestUnload(ApplicationCandidateUnitySceneFixture.SceneKey).Status);
                yield return Pump(_activeGraph.Controller);

                Assert.AreEqual(SceneFlowResultStatus.Failed, _activeGraph.Controller.LastResult.Status);
                Assert.AreSame(debtProvider.ReleaseException,
                    _activeGraph.Controller.LastResult.Error.Exception);
                Assert.IsFalse(IsSceneLoaded(candidateHandle),
                    "The exact physical candidate Scene must be unloaded before artifact release is attempted.");
                Assert.AreEqual(1, debtProvider.ReleaseCount);
                Assert.IsTrue(_activeGraph.IsolationGate.HasOwnedScene,
                    "Artifact release debt must remain represented as owned-scene cleanup debt.");

                Assert.AreEqual(SceneFlowResultStatus.Accepted,
                    _activeGraph.Controller.RequestUnload(ApplicationCandidateUnitySceneFixture.SceneKey).Status);
                yield return Pump(_activeGraph.Controller);
                Assert.AreEqual(SceneFlowResultStatus.Failed, _activeGraph.Controller.LastResult.Status);
                Assert.AreSame(debtProvider.ReleaseException,
                    _activeGraph.Controller.LastResult.Error.Exception,
                    "Repeated unload must report the exact retained release sentinel.");
                Assert.AreEqual(1, debtProvider.ReleaseCount,
                    "A repeated unload must report the retained release error without retrying artifact release.");
                Assert.IsTrue(_activeGraph.IsolationGate.HasOwnedScene);

                ApplicationCandidateUnitySceneFixture.ManifestDocument decoyDocument =
                    ApplicationCandidateUnitySceneFixture.LoadDocument(
                        ApplicationCandidateUnitySceneFixture.DecoyManifestPath);
                var blockedProvider = new RecordingCandidateUnitySceneArtifactProvider(
                    ApplicationCandidateUnitySceneFixture.DecoyManifestPath);
                _secondaryGraph = CreateGraph(
                    ApplicationCandidateUnitySceneFixture.ToRuntimeManifest(decoyDocument), blockedProvider);
                BeginLoad(_secondaryGraph);
                _secondaryGraph.Controller.Tick();

                Assert.AreEqual(SceneFlowResultStatus.Failed, _secondaryGraph.Controller.LastResult.Status);
                Assert.AreEqual(0, blockedProvider.PreflightCount,
                    "The retained permanent mutation token must block a different path before preflight.");
                Assert.AreEqual(0, blockedProvider.AcquireCount);
                Assert.IsFalse(SceneManager.GetSceneByPath(
                    ApplicationCandidateUnitySceneFixture.DecoyScenePath).isLoaded);
            }
            finally
            {
                debtOwner.ReleasePermanentMutationTokenForTests();
                _secondaryGraph = null;
                _activeGraph = null;
            }
        }

        private static ApplicationCandidateSceneGraph CreateGraph(
            ApplicationCandidateUnitySceneManifest manifest,
            RecordingCandidateUnitySceneArtifactProvider provider) =>
            new ApplicationCandidateUnitySceneGraphFactory().Create(
                manifest, provider, ApplicationControllerPumpStrategy.Standalone);

        private static void BeginLoad(ApplicationCandidateSceneGraph graph)
        {
            ApplicationCandidateSceneIsolationResult isolated = graph.IsolationGate.TryEnterIsolation(
                new ApplicationCandidateSceneRequest(ApplicationCandidateUnitySceneFixture.SceneKey));
            Assert.IsTrue(isolated.Success, isolated.Message);
            Assert.AreEqual(SceneFlowResultStatus.Accepted, graph.Controller.RequestLoad(new SceneFlowRequest(
                ApplicationCandidateUnitySceneFixture.SceneKey, SceneFlowLoadMode.Additive, false)).Status);
        }

        private static IEnumerator Pump(SceneFlowController controller)
        {
            yield return Drain(controller);
            controller.Tick();
            Assert.IsFalse(controller.IsBusy, "Scene operation did not reach terminal state.");
        }

        private static IEnumerator Drain(SceneFlowController controller)
        {
            for (int i = 0; i < 300 && controller.IsBusy; i++)
            {
                controller.Tick();
                yield return null;
            }
        }

        private static IEnumerator LoadAdditive(string path)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            Assert.IsNotNull(operation, "Failed to start exact decoy fixture load.");
            while (!operation.isDone) yield return null;
        }

        private static IEnumerator CleanupGraph(ApplicationCandidateSceneGraph graph)
        {
            if (graph == null) yield break;
            yield return Drain(graph.Controller);
            if (!graph.IsolationGate.HasOwnedScene) yield break;
            graph.IsolationGate.MakeInert();
            if (!graph.Controller.IsBusy)
                graph.Controller.RequestUnload(ApplicationCandidateUnitySceneFixture.SceneKey);
            yield return Drain(graph.Controller);
        }

        private static IEnumerator UnloadExactPath(string path)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                Scene exact = default;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (string.Equals(scene.path, path, System.StringComparison.Ordinal))
                    {
                        exact = scene;
                        break;
                    }
                }
                if (!exact.IsValid() || !exact.isLoaded) yield break;
                AsyncOperation operation = SceneManager.UnloadSceneAsync(exact);
                if (operation == null) yield break;
                while (!operation.isDone) yield return null;
            }
        }

        private static bool IsSceneLoaded(ulong handle)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.handle.GetRawData() == handle) return scene.isLoaded;
            }
            return false;
        }
    }
}
