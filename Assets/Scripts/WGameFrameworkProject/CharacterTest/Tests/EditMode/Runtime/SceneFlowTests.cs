using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class SceneFlowTests
    {
        [Test]
        public void RequestLoad_WhenLoadSucceedsUpdatesActiveSceneKey()
        {
            var driver = new FakeSceneFlowDriver();
            var operation = new FakeSceneFlowOperation("menu", 0.25f);
            driver.EnqueueLoad(operation);
            var controller = new SceneFlowController(driver);

            SceneFlowResult requestResult = controller.RequestLoad(new SceneFlowRequest("menu"));

            Assert.IsTrue(requestResult.Success);
            Assert.AreEqual(SceneFlowResultStatus.Accepted, requestResult.Status);
            Assert.AreEqual(SceneFlowOperationType.Load, requestResult.OperationType);
            SceneFlowSnapshot loading = controller.CaptureSnapshot();
            Assert.IsTrue(loading.IsBusy);
            Assert.AreEqual(SceneFlowOperationType.Load, loading.CurrentOperationType);
            Assert.AreEqual("menu", loading.CurrentSceneKey);
            Assert.AreEqual(0.25f, loading.Progress);

            operation.CompleteSuccess();
            controller.Tick();

            SceneFlowSnapshot snapshot = controller.CaptureSnapshot();
            Assert.IsFalse(snapshot.IsBusy);
            Assert.AreEqual("menu", snapshot.ActiveSceneKey);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, snapshot.LastResult.Status);
            Assert.AreEqual(SceneFlowOperationType.Load, snapshot.LastResult.OperationType);
            Assert.AreEqual("menu", snapshot.LastResult.SceneKey);
        }

        [TestCase(SceneFlowLoadMode.Single)]
        [TestCase(SceneFlowLoadMode.Additive)]
        public void RequestLoad_WhenLoadFailsKeepsPreviousActiveScene(SceneFlowLoadMode loadMode)
        {
            var driver = new FakeSceneFlowDriver();
            var bootLoad = new FakeSceneFlowOperation("boot");
            var gameplayLoad = new FakeSceneFlowOperation("gameplay");
            driver.EnqueueLoad(bootLoad);
            driver.EnqueueLoad(gameplayLoad);
            var controller = new SceneFlowController(driver);
            controller.RequestLoad(new SceneFlowRequest("boot"));
            bootLoad.CompleteSuccess();
            controller.Tick();

            controller.RequestLoad(new SceneFlowRequest("gameplay", loadMode, unloadPreviousScene: true));
            gameplayLoad.CompleteFailure(new SceneFlowError(
                SceneFlowErrorCode.OperationFailed,
                "gameplay",
                "load failed"));
            controller.Tick();

            SceneFlowSnapshot snapshot = controller.CaptureSnapshot();
            Assert.IsFalse(snapshot.IsBusy);
            Assert.AreEqual("boot", snapshot.ActiveSceneKey);
            Assert.AreEqual(SceneFlowResultStatus.Failed, snapshot.LastResult.Status);
            Assert.AreEqual(SceneFlowOperationType.Load, snapshot.LastResult.OperationType);
            Assert.AreEqual(SceneFlowErrorCode.OperationFailed, snapshot.LastResult.Error.Code);
            Assert.AreEqual("gameplay", snapshot.LastResult.SceneKey);
            CollectionAssert.AreEquivalent(new[] { "boot" }, driver.LoadedSceneKeys);
            Assert.IsEmpty(driver.UnloadSceneKeys);
        }

        [Test]
        public void RequestLoad_WhenBusyRejectsNewRequest()
        {
            var driver = new FakeSceneFlowDriver();
            var operation = new FakeSceneFlowOperation("menu");
            driver.EnqueueLoad(operation);
            var controller = new SceneFlowController(driver);
            controller.RequestLoad(new SceneFlowRequest("menu"));

            SceneFlowResult busy = controller.RequestLoad(new SceneFlowRequest("gameplay"));

            Assert.IsFalse(busy.Success);
            Assert.AreEqual(SceneFlowResultStatus.Failed, busy.Status);
            Assert.AreEqual(SceneFlowErrorCode.Busy, busy.Error.Code);
            Assert.AreEqual(1, driver.LoadRequests.Count);

            operation.CompleteSuccess();
            controller.Tick();

            Assert.AreEqual("menu", controller.ActiveSceneKey);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, controller.LastResult.Status);
        }

        [Test]
        public void RequestLoad_AdditiveWithUnloadPreviousSceneUnloadsPreviousAfterLoadSucceeds()
        {
            var driver = new FakeSceneFlowDriver();
            var bootLoad = new FakeSceneFlowOperation("boot");
            var gameplayLoad = new FakeSceneFlowOperation("gameplay");
            var bootUnload = new FakeSceneFlowOperation("boot");
            driver.EnqueueLoad(bootLoad);
            driver.EnqueueLoad(gameplayLoad);
            driver.EnqueueUnload(bootUnload);
            var controller = new SceneFlowController(driver);
            controller.RequestLoad(new SceneFlowRequest("boot"));
            bootLoad.CompleteSuccess();
            controller.Tick();

            controller.RequestLoad(new SceneFlowRequest(
                "gameplay",
                SceneFlowLoadMode.Additive,
                unloadPreviousScene: true));
            gameplayLoad.CompleteSuccess();
            controller.Tick();

            SceneFlowSnapshot unloading = controller.CaptureSnapshot();
            Assert.AreEqual("gameplay", unloading.ActiveSceneKey);
            Assert.IsTrue(unloading.IsBusy);
            Assert.AreEqual(SceneFlowOperationType.Unload, unloading.CurrentOperationType);
            CollectionAssert.AreEqual(new[] { "boot" }, driver.UnloadSceneKeys);
            CollectionAssert.AreEquivalent(new[] { "boot", "gameplay" }, driver.LoadedSceneKeys);

            bootUnload.CompleteSuccess();
            controller.Tick();

            SceneFlowSnapshot snapshot = controller.CaptureSnapshot();
            Assert.IsFalse(snapshot.IsBusy);
            Assert.AreEqual("gameplay", snapshot.ActiveSceneKey);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, snapshot.LastResult.Status);
            Assert.AreEqual(SceneFlowOperationType.Unload, snapshot.LastResult.OperationType);
            Assert.AreEqual("boot", snapshot.LastResult.SceneKey);
            controller.Tick();
            CollectionAssert.AreEqual(new[] { "boot" }, driver.UnloadSceneKeys);
            CollectionAssert.AreEquivalent(new[] { "gameplay" }, driver.LoadedSceneKeys);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RequestLoad_SingleDoesNotUnloadPreviousSceneAgain(bool unloadPreviousScene)
        {
            var driver = new FakeSceneFlowDriver("boot");
            var gameplayLoad = new FakeSceneFlowOperation("gameplay");
            driver.EnqueueLoad(gameplayLoad);
            var controller = new SceneFlowController(driver, "boot");

            controller.RequestLoad(new SceneFlowRequest(
                "gameplay", SceneFlowLoadMode.Single, unloadPreviousScene));
            gameplayLoad.CompleteSuccess();
            controller.Tick();
            controller.Tick();

            Assert.IsFalse(controller.IsBusy);
            Assert.AreEqual("gameplay", controller.ActiveSceneKey);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, controller.LastResult.Status);
            Assert.AreEqual(SceneFlowOperationType.Load, controller.LastResult.OperationType);
            Assert.AreEqual("gameplay", controller.LastResult.SceneKey);
            Assert.IsEmpty(driver.UnloadSceneKeys);
            CollectionAssert.AreEquivalent(new[] { "gameplay" }, driver.LoadedSceneKeys);
        }

        [Test]
        public void RequestLoad_AdditiveWithoutUnloadPreviousSceneKeepsBothScenes()
        {
            var driver = new FakeSceneFlowDriver("boot");
            var gameplayLoad = new FakeSceneFlowOperation("gameplay");
            driver.EnqueueLoad(gameplayLoad);
            var controller = new SceneFlowController(driver, "boot");

            controller.RequestLoad(new SceneFlowRequest(
                "gameplay", SceneFlowLoadMode.Additive, unloadPreviousScene: false));
            gameplayLoad.CompleteSuccess();
            controller.Tick();

            Assert.IsFalse(controller.IsBusy);
            Assert.AreEqual("gameplay", controller.ActiveSceneKey);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, controller.LastResult.Status);
            Assert.AreEqual(SceneFlowOperationType.Load, controller.LastResult.OperationType);
            Assert.IsEmpty(driver.UnloadSceneKeys);
            CollectionAssert.AreEquivalent(new[] { "boot", "gameplay" }, driver.LoadedSceneKeys);
        }

        [Test]
        public void RequestLoad_AdditiveUnloadFailureRemainsDiagnosable()
        {
            var driver = new FakeSceneFlowDriver("boot");
            var gameplayLoad = new FakeSceneFlowOperation("gameplay");
            var bootUnload = new FakeSceneFlowOperation("boot");
            driver.EnqueueLoad(gameplayLoad);
            driver.EnqueueUnload(bootUnload);
            var controller = new SceneFlowController(driver, "boot");

            controller.RequestLoad(new SceneFlowRequest(
                "gameplay", SceneFlowLoadMode.Additive, unloadPreviousScene: true));
            gameplayLoad.CompleteSuccess();
            controller.Tick();
            bootUnload.CompleteFailure(new SceneFlowError(
                SceneFlowErrorCode.DriverFailure, "boot", "unload failed"));
            controller.Tick();

            Assert.IsFalse(controller.IsBusy);
            Assert.AreEqual("gameplay", controller.ActiveSceneKey);
            Assert.AreEqual(SceneFlowResultStatus.Failed, controller.LastResult.Status);
            Assert.AreEqual(SceneFlowOperationType.Unload, controller.LastResult.OperationType);
            Assert.AreEqual("boot", controller.LastResult.SceneKey);
            Assert.AreEqual(SceneFlowErrorCode.DriverFailure, controller.LastResult.Error.Code);
            Assert.AreEqual("unload failed", controller.LastResult.Error.Message);
            CollectionAssert.AreEqual(new[] { "boot" }, driver.UnloadSceneKeys);
            CollectionAssert.AreEquivalent(new[] { "boot", "gameplay" }, driver.LoadedSceneKeys);
        }

        [Test]
        public void RequestUnload_StartsExplicitUnloadOperation()
        {
            var driver = new FakeSceneFlowDriver("menu");
            var unload = new FakeSceneFlowOperation("menu", 0.5f);
            driver.EnqueueUnload(unload);
            var controller = new SceneFlowController(driver, "menu");

            SceneFlowResult result = controller.RequestUnload("menu");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(SceneFlowResultStatus.Accepted, result.Status);
            Assert.AreEqual(SceneFlowOperationType.Unload, result.OperationType);
            Assert.AreEqual("menu", controller.CaptureSnapshot().CurrentSceneKey);

            unload.CompleteSuccess();
            controller.Tick();

            Assert.AreEqual(SceneFlowResultStatus.Succeeded, controller.LastResult.Status);
            Assert.AreEqual(SceneFlowOperationType.Unload, controller.LastResult.OperationType);
            Assert.AreEqual(string.Empty, controller.ActiveSceneKey);
            CollectionAssert.AreEqual(new[] { "menu" }, driver.UnloadSceneKeys);
        }

        [Test]
        public void RuntimeModule_TickAdvancesSceneFlowController()
        {
            var driver = new FakeSceneFlowDriver();
            var operation = new FakeSceneFlowOperation("menu");
            driver.EnqueueLoad(operation);
            var controller = new SceneFlowController(driver);
            controller.RequestLoad(new SceneFlowRequest("menu"));
            operation.CompleteSuccess();
            var host = new RuntimeHost();
            host.RegisterModule(new SceneFlowRuntimeModule(controller));
            host.Initialize();
            host.Start();

            host.Tick(1, 0.016d, 0.016d);

            Assert.AreEqual("menu", controller.ActiveSceneKey);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, controller.LastResult.Status);
        }

        private sealed class FakeSceneFlowDriver : ISceneFlowDriver
        {
            private readonly Queue<FakeSceneFlowOperation> _loadOperations = new Queue<FakeSceneFlowOperation>();
            private readonly Queue<FakeSceneFlowOperation> _unloadOperations = new Queue<FakeSceneFlowOperation>();

            public List<SceneFlowRequest> LoadRequests { get; } = new List<SceneFlowRequest>();
            public List<string> UnloadSceneKeys { get; } = new List<string>();
            public HashSet<string> LoadedSceneKeys { get; }

            public FakeSceneFlowDriver(params string[] loadedSceneKeys)
            {
                LoadedSceneKeys = new HashSet<string>(loadedSceneKeys);
            }

            public void EnqueueLoad(FakeSceneFlowOperation operation)
            {
                _loadOperations.Enqueue(operation);
            }

            public void EnqueueUnload(FakeSceneFlowOperation operation)
            {
                _unloadOperations.Enqueue(operation);
            }

            public ISceneFlowOperation LoadScene(SceneFlowRequest request)
            {
                LoadRequests.Add(request);
                FakeSceneFlowOperation operation = _loadOperations.Count > 0
                    ? _loadOperations.Dequeue()
                    : new FakeSceneFlowOperation(request.SceneKey);
                operation.OnSuccess = () =>
                {
                    if (request.LoadMode == SceneFlowLoadMode.Single)
                        LoadedSceneKeys.Clear();
                    LoadedSceneKeys.Add(request.SceneKey);
                };
                return operation;
            }

            public ISceneFlowOperation UnloadScene(string sceneKey)
            {
                UnloadSceneKeys.Add(sceneKey);
                if (!LoadedSceneKeys.Contains(sceneKey))
                {
                    var failed = new FakeSceneFlowOperation(sceneKey);
                    failed.CompleteFailure(new SceneFlowError(
                        SceneFlowErrorCode.DriverFailure, sceneKey, "Scene is not loaded."));
                    return failed;
                }

                FakeSceneFlowOperation operation = _unloadOperations.Count > 0
                    ? _unloadOperations.Dequeue()
                    : new FakeSceneFlowOperation(sceneKey);
                operation.OnSuccess = () => LoadedSceneKeys.Remove(sceneKey);
                return operation;
            }
        }

        private sealed class FakeSceneFlowOperation : ISceneFlowOperation
        {
            private SceneFlowError _error;

            public FakeSceneFlowOperation(string sceneKey, float progress = 0f)
            {
                SceneKey = sceneKey;
                Progress = progress;
                _error = SceneFlowError.None;
            }

            public string SceneKey { get; }
            public bool IsDone { get; private set; }
            public float Progress { get; private set; }
            public bool Success { get; private set; }
            public SceneFlowError Error => _error;
            public System.Action OnSuccess { get; set; }

            public void CompleteSuccess()
            {
                OnSuccess?.Invoke();
                IsDone = true;
                Success = true;
                Progress = 1f;
                _error = SceneFlowError.None;
            }

            public void CompleteFailure(SceneFlowError error)
            {
                IsDone = true;
                Success = false;
                Progress = 1f;
                _error = error;
            }
        }
    }
}
