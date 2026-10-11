using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.ApplicationStartup;
using MxFramework.ApplicationStartup.PlatformBridge;
using MxFramework.ApplicationStartup.Unity;
using MxFramework.Platform.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MxFramework.Tests.PlayMode.Application
{
    [PrebuildSetup(typeof(ApplicationUnityBootValidationPlayModeBuildSettings))]
    [PostBuildCleanup(typeof(ApplicationUnityBootValidationPlayModeBuildSettings))]
    public sealed class ApplicationUnityBootValidationPlayModeTests
    {
        public const string ScenePath =
            "Assets/Scenes/Tests/Application/ApplicationUnityBootValidation.unity";

        private ApplicationUnityBootRoot _root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            bool shutdownReachedTerminal = true;
            try
            {
                if (_root != null)
                {
                    if (_root.IsInitialized)
                    {
                        _root.BeginControlledShutdown();
                        for (int i = 0; i < 240 &&
                            _root.Driver.RootState != ApplicationUnityBootRootState.ShutdownComplete &&
                            _root.Driver.RootState != ApplicationUnityBootRootState.ShutdownDebt; i++)
                            yield return null;
                        shutdownReachedTerminal =
                            _root.Driver.RootState == ApplicationUnityBootRootState.ShutdownComplete ||
                            _root.Driver.RootState == ApplicationUnityBootRootState.ShutdownDebt;
                    }

                    UnityEngine.Object.Destroy(_root.gameObject);
                    yield return null;
                    _root = null;
                }

                yield return UnloadValidationScenes();
            }
            finally
            {
                ApplicationUnityBootValidationCompositionFixture.ReleaseRetainedTestResources();
                ApplicationUnityBootRoot.ResetProcessGateForTests();
            }
            Assert.That(shutdownReachedTerminal, Is.True,
                "Controlled shutdown did not reach a terminal state before test isolation cleanup.");
        }

        [UnityTest]
        public IEnumerator ColdBootSentinel_PublishesFirstRevisionAndVisibleValidationSurface()
        {
            ApplicationUnityBootValidationProbe.Reset(ApplicationUnityBootValidationScenario.Success);
            yield return LoadBootRoot();
            yield return WaitForStartupState(_root, ApplicationStartupState.Succeeded, 600);

            Assert.That(ApplicationUnityBootValidationProbe.Publisher.Current.Revision, Is.EqualTo(1));
            Assert.That(ApplicationUnityBootValidationProbe.Publisher.Current.HasRuntimeSession, Is.True);
            Assert.That(ApplicationUnityBootValidationProbe.CommitCount, Is.EqualTo(1));
            for (int i = 0; i < 120 &&
                ApplicationUnityBootValidationProbe.RetriedRunFirstTickFrame < 0; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(ApplicationUnityBootValidationProbe.RetriedRunFirstTickFrame, Is.EqualTo(42));
            Assert.That(ApplicationUnityBootValidationProbe.Reporter.TryClaimDrain(out _), Is.False);
            ApplicationUnityBootValidationProbe.Reporter.ReportSuspend(false);
            yield return null;
            Assert.That(ApplicationUnityBootValidationProbe.Reporter.ReportSuspend(true), Is.True);
            for (int i = 0; i < 180 &&
                _root.Driver.SurfaceModel.LifecycleCheckpointStatus !=
                    ApplicationLifecycleCheckpointOperationStatus.Succeeded; i++)
                yield return null;
            Assert.That(_root.Driver.SurfaceModel.LifecycleCheckpointStatus,
                Is.EqualTo(ApplicationLifecycleCheckpointOperationStatus.Succeeded));

            UIDocument document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);
            Assert.That(document.rootVisualElement.panel, Is.Not.Null);
            Label title = document.rootVisualElement.Q<Label>("application-boot-title");
            Label state = document.rootVisualElement.Q<Label>("application-boot-state");
            Button retry = document.rootVisualElement.Q<Button>("application-boot-retry");
            AssertVisibleText(title);
            AssertVisibleText(state);
            AssertVisibleText(retry);
            Assert.That(state.text, Is.EqualTo("Ready"));
        }

        [UnityTest]
        public IEnumerator RestoreBoundaryFailure_RollsBackAndFreshRetryPublishesFromBoot()
        {
            ApplicationUnityBootValidationProbe.Reset(
                ApplicationUnityBootValidationScenario.RestoreBoundaryFailureThenSuccess);
            yield return LoadBootRoot();
            yield return WaitForStartupState(_root, ApplicationStartupState.Failed, 600);

            Assert.That(ApplicationUnityBootValidationProbe.Publisher.Current.Revision, Is.EqualTo(0));
            Assert.That(ApplicationUnityBootValidationProbe.Publisher.Current.HasRuntimeSession, Is.False);
            Assert.That(ApplicationUnityBootValidationProbe.RestoreBoundaryInjectionCount, Is.EqualTo(1));
            Assert.That(_root.Driver.ActiveCoordinator.ForwardError.Message,
                Is.EqualTo("Injected failure at the restore/publication boundary."));
            int failedRollback = ApplicationUnityBootValidationProbe.AppFlowRollbackCount;
            int failedRelease = ApplicationUnityBootValidationProbe.ResourceReleaseCount;
            Assert.That(failedRollback, Is.GreaterThan(0));
            Assert.That(failedRelease, Is.GreaterThan(0));

            UIDocument document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            Button retry = document.rootVisualElement.Q<Button>("application-boot-retry");
            Click(retry);
            yield return WaitForStartupState(_root, ApplicationStartupState.Succeeded, 600);

            Assert.That(_root.Driver.Attempt, Is.EqualTo(2));
            Assert.That(ApplicationUnityBootValidationProbe.Publisher.Current.Revision, Is.EqualTo(1));
            Assert.That(ApplicationUnityBootValidationProbe.CommitCount, Is.EqualTo(1));
            Assert.That(ApplicationUnityBootValidationProbe.FirstGraph,
                Is.Not.SameAs(ApplicationUnityBootValidationProbe.SecondGraph));
        }

        [UnityTest]
        public IEnumerator FailureCancelRollbackThenFreshRetry_ProvesLiveReplacementChain()
        {
            var bootFence = new ApplicationPublicationSaveFence();
            var bootPublisher = new LiveApplicationStatePublisher(2, bootFence);
            var bootReporter = new UnityApplicationLifecycleReporter(true, false, 4);
            Assert.That(ApplicationPlatformLifecycleBridge.TryCreate(
                bootReporter, bootPublisher, bootFence, DeferredCheckpointPolicy.Instance, 4,
                out ApplicationPlatformLifecycleBridge bootBridge),
                Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded));
            Assert.That(bootReporter.TryClaimDrain(out _), Is.False);
            Assert.That(bootReporter.ReportSuspend(true), Is.True);
            Assert.That(bootBridge.Pump(true),
                Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.DeferredAtBootOrMissingCapability));
            Assert.That(bootBridge.LastCheckpointStatus,
                Is.EqualTo(ApplicationLifecycleCheckpointOperationStatus.Unknown));

            ApplicationUnityBootValidationProbe.Reset(
                ApplicationUnityBootValidationScenario.FailThenCancelThenSuccess,
                bootstrapExistingLive: true);
            yield return LoadBootRoot();
            yield return WaitForStartupState(_root, ApplicationStartupState.Failed, 360);

            Assert.That(_root.Driver.SurfaceModel.RuntimeErrorCode,
                Is.EqualTo("PLAYMODE_POST_SCENE_FAILURE"));
            int failureRollback = ApplicationUnityBootValidationProbe.AppFlowRollbackCount;
            int failureRelease = ApplicationUnityBootValidationProbe.ResourceReleaseCount;
            Assert.That(failureRollback, Is.GreaterThan(0));
            Assert.That(failureRelease, Is.GreaterThan(0));

            UIDocument document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            Label state = document.rootVisualElement.Q<Label>("application-boot-state");
            Button retry = document.rootVisualElement.Q<Button>("application-boot-retry");
            Button cancel = document.rootVisualElement.Q<Button>("application-boot-cancel");
            Assert.That(state.text, Is.EqualTo("Error"));
            Assert.That(retry.enabledSelf, Is.True);

            Click(retry);
            yield return WaitForStep(_root, "validation-post-scene", 600);
            Assert.That(_root.Driver.SurfaceModel.CanCancel, Is.True);
            Assert.That(state.text, Is.EqualTo("Loading"));
            Assert.That(cancel.enabledSelf, Is.True);

            Click(cancel);
            yield return WaitForStartupState(_root, ApplicationStartupState.Cancelled, 360);
            int cancelRollback = ApplicationUnityBootValidationProbe.AppFlowRollbackCount;
            int cancelRelease = ApplicationUnityBootValidationProbe.ResourceReleaseCount;
            Assert.That(cancelRollback, Is.GreaterThan(failureRollback));
            Assert.That(cancelRelease, Is.GreaterThan(failureRelease));
            Assert.That(state.text, Is.EqualTo("Error"));
            Assert.That(retry.enabledSelf, Is.True);

            Click(retry);
            yield return WaitForStartupState(_root, ApplicationStartupState.Succeeded, 600);

            Assert.That(_root.Driver.Attempt, Is.EqualTo(3));
            Assert.That(ApplicationUnityBootValidationProbe.RunCreateCount, Is.EqualTo(3));
            Assert.That(ApplicationUnityBootValidationProbe.GraphBuildCount, Is.EqualTo(3));
            Assert.That(ApplicationUnityBootValidationProbe.CommitCount, Is.EqualTo(1));
            Assert.That(ApplicationUnityBootValidationProbe.FirstGraph,
                Is.Not.SameAs(ApplicationUnityBootValidationProbe.SecondGraph));
            Assert.That(ApplicationUnityBootValidationProbe.SecondGraph,
                Is.Not.SameAs(ApplicationUnityBootValidationProbe.ThirdGraph));
            Assert.That(ApplicationUnityBootValidationProbe.Publisher.Current.HasRuntimeSession, Is.True);
            Assert.That(ApplicationUnityBootValidationProbe.Publisher.Current.NextRuntimeFrame, Is.EqualTo(42));
            Assert.That(state.text, Is.EqualTo("Ready"));

            for (int i = 0; i < 120 &&
                (ApplicationUnityBootValidationProbe.ResourceReleaseCount <= cancelRelease ||
                 ApplicationUnityBootValidationProbe.Publisher.RetiredCount != 0); i++)
                yield return null;
            Assert.That(ApplicationUnityBootValidationProbe.ResourceReleaseCount,
                Is.GreaterThan(cancelRelease), "Old live retirement must add its own release evidence.");
            Assert.That(ApplicationUnityBootValidationProbe.Publisher.RetiredCount, Is.EqualTo(0));

            for (int i = 0; i < 120 &&
                ApplicationUnityBootValidationProbe.RetriedRunFirstTickFrame < 0; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(ApplicationUnityBootValidationProbe.RetriedRunFirstTickFrame, Is.EqualTo(42));
            Assert.That(_root.Driver.LastLiveFrame.Status,
                Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded));
            Assert.That(_root.Driver.LastLiveFrame.Frame, Is.GreaterThanOrEqualTo(42));

            Assert.That(ApplicationUnityBootValidationProbe.Reporter.TryClaimDrain(out _), Is.False);
            Assert.That(ApplicationUnityBootValidationProbe.Reporter.ReportSuspend(true), Is.True);
            Assert.That(_root.Driver.SurfaceModel.LiveRevision, Is.EqualTo(2));
        }

        private IEnumerator LoadBootRoot()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, "Validation setup did not register the generated Boot scene.");
            while (!load.isDone) yield return null;

            for (int i = 0; i < 30 && _root == null; i++)
            {
                _root = UnityEngine.Object.FindAnyObjectByType<ApplicationUnityBootRoot>();
                if (_root == null) yield return null;
            }
            Assert.That(_root, Is.Not.Null);
            Assert.That(_root.IsInitialized, Is.True, _root.InitializationStableErrorCode);
            Assert.That(_root.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            Assert.That(ApplicationUnityBootValidationProbe.CompositionCreateCount, Is.EqualTo(1));
        }

        private static void AssertVisibleText(TextElement element)
        {
            Assert.That(element, Is.Not.Null);
            Assert.That(element.text, Is.Not.Empty);
            Assert.That(element.resolvedStyle.color.a, Is.GreaterThan(0f));
        }

        private static IEnumerator UnloadValidationScenes()
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded ||
                    (!string.Equals(scene.path, ScenePath, StringComparison.Ordinal) &&
                     !string.Equals(scene.path, ApplicationCandidateUnitySceneFixture.ScenePath, StringComparison.Ordinal) &&
                     !string.Equals(scene.path, ApplicationCandidateUnitySceneFixture.DecoyScenePath, StringComparison.Ordinal)))
                    continue;
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                    while (!unload.isDone) yield return null;
            }
        }

        private sealed class DeferredCheckpointPolicy : IApplicationLifecycleCheckpointPolicy
        {
            public static readonly DeferredCheckpointPolicy Instance = new DeferredCheckpointPolicy();
            public ApplicationLifecycleCheckpointBeginResult TryBegin(
                LiveApplicationState exactState,
                in ApplicationLifecycleCheckpointEvidence evidence) =>
                ApplicationLifecycleCheckpointBeginResult.Deferred;
        }

        private static IEnumerator WaitForStartupState(
            ApplicationUnityBootRoot root,
            ApplicationStartupState expected,
            int frameBudget)
        {
            for (int i = 0; i < frameBudget && root.Driver.SurfaceModel.StartupState != expected; i++)
                yield return null;
            Assert.That(root.Driver.SurfaceModel.StartupState, Is.EqualTo(expected),
                "attempt=" + root.Driver.Attempt +
                " runs=" + ApplicationUnityBootValidationProbe.RunCreateCount +
                " graphs=" + ApplicationUnityBootValidationProbe.GraphBuildCount +
                " step=" + root.Driver.SurfaceModel.StepId.Value +
                " appError=" + root.Driver.SurfaceModel.ApplicationErrorCode +
                " runtimeError=" + root.Driver.SurfaceModel.RuntimeErrorCode);
        }

        private static IEnumerator WaitForStep(
            ApplicationUnityBootRoot root,
            string expectedStepId,
            int frameBudget)
        {
            for (int i = 0; i < frameBudget && root.Driver.SurfaceModel.StepId.Value != expectedStepId; i++)
                yield return null;
            Assert.That(root.Driver.SurfaceModel.StepId.Value, Is.EqualTo(expectedStepId));
            Assert.That(root.Driver.SurfaceModel.StartupState, Is.EqualTo(ApplicationStartupState.Running));
        }

        private static void Click(Button button)
        {
            MethodInfo invoke = typeof(Clickable).GetMethod(
                "Invoke",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(EventBase) },
                null);
            Assert.That(invoke, Is.Not.Null);
            using (ClickEvent click = ClickEvent.GetPooled())
            {
                click.target = button;
                invoke.Invoke(button.clickable, new object[] { click });
            }
        }
    }
}
