using System;
using System.Reflection;
using MxFramework.DebugUI.Input;
using MxFramework.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Tests.Input
{
    public sealed class InputServiceLifecycleTests : InputTestFixture
    {
        private GameObject _owner;
        private LifecycleInputService _service;

        [SetUp]
        public void CreateService()
        {
            _owner = new GameObject("InputServiceLifecycleTest");
            _service = _owner.AddComponent<LifecycleInputService>();
            Assert.IsTrue(_service.isActiveAndEnabled);
            Assert.Greater(_service.EnableCount, 0, "Unity must dispatch OnEnable automatically.");
            AssertMaps(gameplay: true);
        }

        [TearDown]
        public void DestroyService()
        {
            UnityEngine.Object.DestroyImmediate(_owner);
        }

        [TestCase(false, InputContextPolicy.Exclusive)]
        [TestCase(false, InputContextPolicy.Overlay)]
        [TestCase(true, InputContextPolicy.Exclusive)]
        [TestCase(true, InputContextPolicy.Overlay)]
        public void DisabledService_DisposingExistingScopeKeepsMapsDisabled(
            bool deactivateObject, InputContextPolicy policy)
        {
            IDisposable scope = _service.PushContext(InputContext.UI, policy);
            AssertMaps(gameplay: policy == InputContextPolicy.Overlay, ui: true);

            DisableService(deactivateObject);
            scope.Dispose();

            Assert.AreEqual(InputContext.Gameplay, _service.CurrentContext);
            Assert.AreEqual(1, _service.Contexts.Count);
            AssertMaps();
            Assert.AreEqual(InputSnapshot.Empty, _service.Snapshot);
            scope.Dispose();
            AssertMaps();
            Assert.AreEqual(1, _service.Contexts.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisabledService_SetContextUpdatesStackWithoutEnablingMaps(bool deactivateObject)
        {
            DisableService(deactivateObject);
            _service.SetContext(InputContext.UI);

            Assert.AreEqual(InputContext.UI, _service.CurrentContext);
            Assert.IsTrue(_service.IsContextEnabled(InputContext.UI));
            AssertMaps();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisabledService_PublicStackMutationsKeepMapsDisabled(bool deactivateObject)
        {
            DisableService(deactivateObject);
            _service.Contexts.Set(InputContext.UI);
            AssertMaps();
            using (IDisposable scope = _service.Contexts.Push(InputContext.Debug, InputContextPolicy.Overlay))
            {
                Assert.AreEqual(2, _service.Contexts.Count);
                Assert.IsTrue(_service.ContainsContext(InputContext.Debug));
                Assert.IsTrue(_service.IsContextEnabled(InputContext.UI));
                AssertMaps();
                scope.Dispose();
                AssertMaps();
                scope.Dispose();
                AssertMaps();
            }

            Assert.AreEqual(InputContext.UI, _service.CurrentContext);
            AssertMaps();
            _service.Contexts.Clear();
            Assert.AreEqual(0, _service.Contexts.Count);
            AssertMaps();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisabledService_DebugAdapterReleaseKeepsMapsDisabled(bool deactivateObject)
        {
            using (var adapter = new DebugUiInputAdapter(_service))
            {
                adapter.SetEnabled(true);
                AssertMaps(gameplay: true, debug: true);
                DisableService(deactivateObject);

                adapter.SetEnabled(false);

                Assert.IsFalse(adapter.Enabled);
                Assert.IsFalse(_service.ContainsContext(InputContext.Debug));
                Assert.AreEqual(InputContext.Gameplay, _service.CurrentContext);
                AssertMaps();
                adapter.SetEnabled(false);
                Assert.IsFalse(adapter.Enabled);
                Assert.IsFalse(_service.ContainsContext(InputContext.Debug));
                AssertMaps();
            }
            AssertMaps();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReenabledService_ResetsInitialContextAndAppliesActiveScopes(bool deactivateObject)
        {
            long generation = _service.Commands.Generation;
            IDisposable oldScope = _service.PushContext(InputContext.UI);
            DisableService(deactivateObject);
            _service.Contexts.Set(InputContext.UI);
            IDisposable oldOverlay = _service.Contexts.Push(InputContext.Debug, InputContextPolicy.Overlay);
            Assert.AreEqual(2, _service.Contexts.Count);
            _service.Commands.Enqueue(new InputCommand(0, 1, InputIntent.Jump));
            Assert.AreEqual(1, _service.Commands.PendingCount);
            int enableCount = _service.EnableCount;

            if (deactivateObject)
                _owner.SetActive(true);
            else
                _service.enabled = true;

            Assert.IsTrue(_service.isActiveAndEnabled);
            Assert.Greater(_service.EnableCount, enableCount);
            Assert.Greater(_service.Commands.Generation, generation);
            Assert.AreEqual(InputContext.Gameplay, _service.CurrentContext);
            Assert.IsFalse(_service.ContainsContext(InputContext.UI));
            Assert.IsFalse(_service.ContainsContext(InputContext.Debug));
            Assert.AreEqual(1, _service.Contexts.Count);
            Assert.AreEqual(InputSnapshot.Empty, _service.Snapshot);
            Assert.AreEqual(0, _service.Commands.PendingCount);
            AssertMaps(gameplay: true);
            oldScope.Dispose();
            AssertMaps(gameplay: true);
            oldScope.Dispose();
            AssertMaps(gameplay: true);
            oldOverlay.Dispose();
            AssertMaps(gameplay: true);
            oldOverlay.Dispose();
            AssertMaps(gameplay: true);

            using (_service.PushContext(InputContext.Debug, InputContextPolicy.Overlay))
            {
                AssertMaps(gameplay: true, debug: true);
                using (_service.PushContext(InputContext.UI))
                    AssertMaps(ui: true);
                AssertMaps(gameplay: true, debug: true);
            }

            AssertMaps(gameplay: true);
            _service.SetContext(InputContext.UI);
            AssertMaps(ui: true);
        }

        [Test]
        public void ActiveOverlayExclusiveAndRepeatedDispose_PreserveMapSemantics()
        {
            using (IDisposable overlay = _service.PushContext(InputContext.Debug, InputContextPolicy.Overlay))
            {
                AssertMaps(true, false, true);
                using (IDisposable exclusive = _service.PushContext(InputContext.UI))
                {
                    AssertMaps(false, true, false);
                    exclusive.Dispose();
                    AssertMaps(true, false, true);
                    exclusive.Dispose();
                    AssertMaps(true, false, true);
                }
                overlay.Dispose();
                AssertMaps(true, false, false);
            }
        }

        private void DisableService(bool deactivateObject)
        {
            int disableCount = _service.DisableCount;
            if (deactivateObject)
                _owner.SetActive(false);
            else
                _service.enabled = false;

            Assert.IsFalse(_service.isActiveAndEnabled);
            Assert.Greater(_service.DisableCount, disableCount, "Unity must dispatch OnDisable automatically.");
            AssertMaps();
            Assert.AreEqual(InputSnapshot.Empty, _service.Snapshot);
        }

        private void AssertMaps(bool gameplay = false, bool ui = false, bool debug = false)
        {
            Assert.AreEqual(gameplay, _service.Actions.FindActionMap(InputActionNames.GameplayMap).enabled, "Gameplay map");
            Assert.AreEqual(ui, _service.Actions.FindActionMap(InputActionNames.UIMap).enabled, "UI map");
            Assert.AreEqual(debug, _service.Actions.FindActionMap(InputActionNames.DebugMap).enabled, "Debug map");
        }

        // ExecuteAlways lets the native EditMode runner dispatch the real Unity lifecycle callbacks.
        [ExecuteAlways]
        private sealed class LifecycleInputService : InputService
        {
            private InputActionAsset _testActions;
            public int EnableCount { get; private set; }
            public int DisableCount { get; private set; }

            protected override void Awake()
            {
                typeof(InputService).GetField("_bindingOverridesKey", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(this, "mxframework.input.lifecycle-test." + Guid.NewGuid().ToString("N"));
                base.Awake();
            }

            protected override void OnEnable()
            {
                EnableCount++;
                base.OnEnable();
            }

            protected override void OnDisable()
            {
                DisableCount++;
                base.OnDisable();
            }

            protected override InputActionAsset CreateRuntimeActions(out bool ownsActions)
            {
                // The fixture owns cleanup so EditMode never queues a runtime Destroy.
                ownsActions = false;
                return _testActions = DefaultInputActionAssetFactory.Create();
            }

            protected override void OnDestroy()
            {
                base.OnDestroy();
                UnityEngine.Object.DestroyImmediate(_testActions);
            }
        }
    }
}
