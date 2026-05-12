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

        [Test]
        public void RequestLoad_WhenLoadFailsKeepsPreviousActiveScene()
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

            controller.RequestLoad(new SceneFlowRequest("gameplay"));
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
        public void RequestLoad_WithUnloadPreviousSceneUnloadsPreviousAfterLoadSucceeds()
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
                SceneFlowLoadMode.Single,
                unloadPreviousScene: true));
            gameplayLoad.CompleteSuccess();
            controller.Tick();

            SceneFlowSnapshot unloading = controller.CaptureSnapshot();
            Assert.AreEqual("gameplay", unloading.ActiveSceneKey);
            Assert.IsTrue(unloading.IsBusy);
            Assert.AreEqual(SceneFlowOperationType.Unload, unloading.CurrentOperationType);
            CollectionAssert.AreEqual(new[] { "boot" }, driver.UnloadSceneKeys);

            bootUnload.CompleteSuccess();
            controller.Tick();

            SceneFlowSnapshot snapshot = controller.CaptureSnapshot();
            Assert.IsFalse(snapshot.IsBusy);
            Assert.AreEqual("gameplay", snapshot.ActiveSceneKey);
            Assert.AreEqual(SceneFlowResultStatus.Succeeded, snapshot.LastResult.Status);
            Assert.AreEqual(SceneFlowOperationType.Unload, snapshot.LastResult.OperationType);
            Assert.AreEqual("boot", snapshot.LastResult.SceneKey);
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
                return _loadOperations.Count > 0
                    ? _loadOperations.Dequeue()
                    : new FakeSceneFlowOperation(request.SceneKey);
            }

            public ISceneFlowOperation UnloadScene(string sceneKey)
            {
                UnloadSceneKeys.Add(sceneKey);
                return _unloadOperations.Count > 0
                    ? _unloadOperations.Dequeue()
                    : new FakeSceneFlowOperation(sceneKey);
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

            public void CompleteSuccess()
            {
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
