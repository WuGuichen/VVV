using MxFramework.ApplicationStartup;
using MxFramework.ApplicationStartup.PlatformBridge;
using MxFramework.ApplicationStartup.Unity;
using MxFramework.ApplicationStartup.Unity.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MxFramework.Tests.ApplicationStartup.Unity.Validation
{
    public sealed class ApplicationUnityStartupValidationSurfaceTests
    {
        private const string UxmlPath = "Assets/UI/MxFramework/ApplicationBoot/ApplicationBootValidation.uxml";
        private const string UssPath = "Assets/UI/MxFramework/ApplicationBoot/ApplicationBootValidation.uss";

        [Test]
        public void ValidationAssets_ContainEveryStableNamedElement()
        {
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            Assert.That(tree, Is.Not.Null);
            Assert.That(styleSheet, Is.Not.Null);

            TemplateContainer instance = tree.Instantiate();
            Assert.That(instance.Q<VisualElement>(ApplicationUnityStartupValidationElementNames.Root), Is.Not.Null);
            Assert.That(instance.Q<Label>(ApplicationUnityStartupValidationElementNames.State), Is.Not.Null);
            Assert.That(instance.Q<Label>(ApplicationUnityStartupValidationElementNames.Attempt), Is.Not.Null);
            Assert.That(instance.Q<Label>(ApplicationUnityStartupValidationElementNames.Plan), Is.Not.Null);
            Assert.That(instance.Q<Label>(ApplicationUnityStartupValidationElementNames.Run), Is.Not.Null);
            Assert.That(instance.Q<Label>(ApplicationUnityStartupValidationElementNames.Phase), Is.Not.Null);
            Assert.That(instance.Q<Label>(ApplicationUnityStartupValidationElementNames.Step), Is.Not.Null);
            Assert.That(instance.Q<Label>(ApplicationUnityStartupValidationElementNames.Progress), Is.Not.Null);
            Assert.That(instance.Q<Label>(ApplicationUnityStartupValidationElementNames.Error), Is.Not.Null);
            Assert.That(instance.Q<Label>(ApplicationUnityStartupValidationElementNames.Live), Is.Not.Null);
            Assert.That(instance.Q<Label>(ApplicationUnityStartupValidationElementNames.Lifecycle), Is.Not.Null);
            Assert.That(instance.Q<Label>(ApplicationUnityStartupValidationElementNames.Retirement), Is.Not.Null);
            Assert.That(instance.Q<Button>(ApplicationUnityStartupValidationElementNames.Start), Is.Not.Null);
            Assert.That(instance.Q<Button>(ApplicationUnityStartupValidationElementNames.Retry), Is.Not.Null);
            Assert.That(instance.Q<Button>(ApplicationUnityStartupValidationElementNames.Cancel), Is.Not.Null);
        }

        [Test]
        public void RequestQueue_IsBoundedFifoWithMonotonicIdsAndNoOverwrite()
        {
            var queue = new ApplicationUnityStartupRequestQueue(2);

            Assert.That(queue.TryEnqueue(ApplicationUnityStartupRequestKind.Unknown), Is.False);
            Assert.That(queue.TryEnqueue((ApplicationUnityStartupRequestKind)99), Is.False);
            Assert.That(queue.TryEnqueue(ApplicationUnityStartupRequestKind.Start), Is.True);
            Assert.That(queue.TryEnqueue(ApplicationUnityStartupRequestKind.Cancel), Is.True);
            Assert.That(queue.TryEnqueue(ApplicationUnityStartupRequestKind.Retry), Is.False);
            Assert.That(queue.DroppedRequestCount, Is.EqualTo(1));

            Assert.That(queue.TryDequeue(out ApplicationUnityStartupSurfaceRequest first), Is.True);
            Assert.That(first.RequestId, Is.EqualTo(1));
            Assert.That(first.Kind, Is.EqualTo(ApplicationUnityStartupRequestKind.Start));
            Assert.That(queue.TryDequeue(out ApplicationUnityStartupSurfaceRequest second), Is.True);
            Assert.That(second.RequestId, Is.EqualTo(2));
            Assert.That(second.Kind, Is.EqualTo(ApplicationUnityStartupRequestKind.Cancel));
            Assert.That(queue.TryDequeue(out ApplicationUnityStartupSurfaceRequest empty), Is.False);
            Assert.That(empty.RequestId, Is.EqualTo(0));

            Assert.That(queue.TryEnqueue(ApplicationUnityStartupRequestKind.Retry), Is.True);
            Assert.That(queue.TryDequeue(out ApplicationUnityStartupSurfaceRequest third), Is.True);
            Assert.That(third.RequestId, Is.EqualTo(3));
        }

        [Test]
        public void RequestQueue_RequestIdExhaustionRejectsWithoutThrowing()
        {
            var queue = new ApplicationUnityStartupRequestQueue(1, long.MaxValue);

            Assert.That(queue.TryEnqueue(ApplicationUnityStartupRequestKind.Start), Is.False);
            Assert.That(queue.DroppedRequestCount, Is.EqualTo(1));
            Assert.That(queue.TryDequeue(out _), Is.False);
        }

        [Test]
        public void Binder_RequiresEveryStableNamedElement()
        {
            GameObject owner = new GameObject("ApplicationValidationSurfaceTest");
            owner.SetActive(false);
            try
            {
                var surface = owner.AddComponent<ApplicationUnityStartupValidationSurface>();
                LogAssert.Expect(LogType.Error, "Application startup Validation UXML is missing one or more required named elements.");
                Assert.That(surface.TryBindForTests(new VisualElement(), 2), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Apply_ProjectsTypedLoadingErrorAndReadyStates()
        {
            GameObject owner = new GameObject("ApplicationValidationSurfaceTest");
            owner.SetActive(false);
            try
            {
                VisualElement documentRoot = CreateRequiredTree();
                var surface = owner.AddComponent<ApplicationUnityStartupValidationSurface>();
                Assert.That(surface.TryBindForTests(documentRoot, 2), Is.True);

                ApplicationUnityStartupSurfaceModel loading = CreateModel(
                    ApplicationUnityBootRootState.Running,
                    ApplicationStartupState.Running,
                    ApplicationStartupErrorCode.None,
                    runtimeErrorCode: string.Empty,
                    hasRuntimeSession: false);
                surface.Apply(in loading);
                Assert.That(documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.State).text, Is.EqualTo("Loading"));
                Assert.That(documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Cancel).enabledSelf, Is.True);
                Assert.That(documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Retry).enabledSelf, Is.False);

                ApplicationUnityStartupSurfaceModel failed = CreateModel(
                    ApplicationUnityBootRootState.Ready,
                    ApplicationStartupState.Failed,
                    ApplicationStartupErrorCode.StepFailed,
                    runtimeErrorCode: "runtime.failed",
                    hasRuntimeSession: false);
                surface.Apply(in failed);
                Assert.That(documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.State).text, Is.EqualTo("Error"));
                StringAssert.Contains("StepFailed", documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Error).text);
                StringAssert.Contains("runtime.failed", documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Error).text);
                StringAssert.Contains("Root None", documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Error).text);
                Assert.That(documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Retry).enabledSelf, Is.True);

                ApplicationUnityStartupSurfaceModel cancelled = CreateModel(
                    ApplicationUnityBootRootState.Ready,
                    ApplicationStartupState.Cancelled,
                    ApplicationStartupErrorCode.Cancelled,
                    runtimeErrorCode: string.Empty,
                    hasRuntimeSession: false);
                surface.Apply(in cancelled);
                Assert.That(documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Retry).enabledSelf, Is.True);

                ApplicationUnityStartupSurfaceModel timedOut = CreateModel(
                    ApplicationUnityBootRootState.Ready,
                    ApplicationStartupState.TimedOut,
                    ApplicationStartupErrorCode.Timeout,
                    runtimeErrorCode: string.Empty,
                    hasRuntimeSession: false);
                surface.Apply(in timedOut);
                Assert.That(documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Retry).enabledSelf, Is.True);

                ApplicationUnityStartupSurfaceModel rollbackFailed = CreateModel(
                    ApplicationUnityBootRootState.Ready,
                    ApplicationStartupState.RollbackFailed,
                    ApplicationStartupErrorCode.RollbackFailed,
                    runtimeErrorCode: string.Empty,
                    hasRuntimeSession: false);
                surface.Apply(in rollbackFailed);
                Assert.That(documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Retry).enabledSelf, Is.False);

                ApplicationUnityStartupSurfaceModel recoverableRootFailure = CreateModel(
                    ApplicationUnityBootRootState.Failed,
                    ApplicationStartupState.Created,
                    ApplicationStartupErrorCode.None,
                    runtimeErrorCode: string.Empty,
                    hasRuntimeSession: false,
                    rootError: ApplicationUnityBootErrorCode.RunFactoryUnavailable);
                surface.Apply(in recoverableRootFailure);
                Assert.That(documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Retry).enabledSelf, Is.True);

                ApplicationUnityStartupSurfaceModel fatalFactoryFailure = CreateModel(
                    ApplicationUnityBootRootState.Failed,
                    ApplicationStartupState.Created,
                    ApplicationStartupErrorCode.None,
                    runtimeErrorCode: string.Empty,
                    hasRuntimeSession: false,
                    rootError: ApplicationUnityBootErrorCode.RunFactoryRejected,
                    creationStableErrorCode: "factory.rejected");
                surface.Apply(in fatalFactoryFailure);
                Assert.That(documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Retry).enabledSelf, Is.False);
                StringAssert.Contains("factory.rejected", documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Error).text);

                ApplicationUnityStartupSurfaceModel changedFactoryDetail = CreateModel(
                    ApplicationUnityBootRootState.Failed,
                    ApplicationStartupState.Created,
                    ApplicationStartupErrorCode.None,
                    runtimeErrorCode: string.Empty,
                    hasRuntimeSession: false,
                    rootError: ApplicationUnityBootErrorCode.RunFactoryRejected,
                    creationStableErrorCode: "factory.changed");
                surface.Apply(in changedFactoryDetail);
                StringAssert.Contains("factory.changed", documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.Error).text);

                ApplicationUnityStartupSurfaceModel fatalRootFailure = CreateModel(
                    ApplicationUnityBootRootState.Failed,
                    ApplicationStartupState.Created,
                    ApplicationStartupErrorCode.None,
                    runtimeErrorCode: string.Empty,
                    hasRuntimeSession: false,
                    rootError: ApplicationUnityBootErrorCode.InvalidComposition);
                surface.Apply(in fatalRootFailure);
                Assert.That(documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Retry).enabledSelf, Is.False);

                ApplicationUnityStartupSurfaceModel ready = CreateModel(
                    ApplicationUnityBootRootState.Ready,
                    ApplicationStartupState.Succeeded,
                    ApplicationStartupErrorCode.None,
                    runtimeErrorCode: string.Empty,
                    hasRuntimeSession: true);
                surface.Apply(in ready);
                Label state = documentRoot.Q<Label>(ApplicationUnityStartupValidationElementNames.State);
                Assert.That(state.text, Is.EqualTo("Ready"));
                string exactTextReference = state.text;
                surface.Apply(in ready);
                Assert.That(state.text, Is.SameAs(exactTextReference), "An unchanged model must not rewrite label values.");
                Assert.That(documentRoot.Q<Button>(ApplicationUnityStartupValidationElementNames.Cancel).enabledSelf, Is.False);

                surface.UnbindForTests();
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static ApplicationUnityStartupSurfaceModel CreateModel(
            ApplicationUnityBootRootState rootState,
            ApplicationStartupState startupState,
            ApplicationStartupErrorCode applicationErrorCode,
            string runtimeErrorCode,
            bool hasRuntimeSession,
            ApplicationUnityBootErrorCode rootError = ApplicationUnityBootErrorCode.None,
            string creationStableErrorCode = null)
        {
            return new ApplicationUnityStartupSurfaceModel(
                rootState,
                rootError != ApplicationUnityBootErrorCode.None
                    ? rootError
                    : rootState == ApplicationUnityBootRootState.Failed
                        ? ApplicationUnityBootErrorCode.InvalidRun
                        : ApplicationUnityBootErrorCode.None,
                3,
                new ApplicationStartupPlanId("validation.plan"),
                "run-3",
                startupState,
                startupState == ApplicationStartupState.RollingBack ? ApplicationStartupPhase.Rollback : ApplicationStartupPhase.Forward,
                new ApplicationStartupStepId("validation.step"),
                0.5f,
                0.25f,
                false,
                applicationErrorCode,
                runtimeErrorCode,
                7,
                hasRuntimeSession,
                hasRuntimeSession ? ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded : ApplicationLiveFrameTickStatus.Boot,
                ApplicationPlatformLifecyclePumpStatus.Idle,
                ApplicationLifecycleCheckpointOperationStatus.Unknown,
                1,
                0,
                string.Empty,
                string.Empty,
                creationStableErrorCode);
        }

        private static VisualElement CreateRequiredTree()
        {
            var documentRoot = new VisualElement();
            var root = new VisualElement { name = ApplicationUnityStartupValidationElementNames.Root };
            documentRoot.Add(root);
            AddLabel(root, ApplicationUnityStartupValidationElementNames.State);
            AddLabel(root, ApplicationUnityStartupValidationElementNames.Attempt);
            AddLabel(root, ApplicationUnityStartupValidationElementNames.Plan);
            AddLabel(root, ApplicationUnityStartupValidationElementNames.Run);
            AddLabel(root, ApplicationUnityStartupValidationElementNames.Phase);
            AddLabel(root, ApplicationUnityStartupValidationElementNames.Step);
            AddLabel(root, ApplicationUnityStartupValidationElementNames.Progress);
            AddLabel(root, ApplicationUnityStartupValidationElementNames.Error);
            AddLabel(root, ApplicationUnityStartupValidationElementNames.Live);
            AddLabel(root, ApplicationUnityStartupValidationElementNames.Lifecycle);
            AddLabel(root, ApplicationUnityStartupValidationElementNames.Retirement);
            root.Add(new Button { name = ApplicationUnityStartupValidationElementNames.Start });
            root.Add(new Button { name = ApplicationUnityStartupValidationElementNames.Retry });
            root.Add(new Button { name = ApplicationUnityStartupValidationElementNames.Cancel });
            return documentRoot;
        }

        private static void AddLabel(VisualElement root, string name) => root.Add(new Label { name = name });
    }
}
