using System;
using MxFramework.UI;
using NUnit.Framework;

namespace MxFramework.Tests.UI
{
    public sealed class MxUiCoreTests
    {
        [Test]
        public void Registry_RejectsDuplicateIds()
        {
            var registry = new InMemoryMxUiViewRegistry();
            var id = new MxUiViewId("hud.main");
            registry.Register(id, () => new FakeView(id));

            Assert.Throws<ArgumentException>(() => registry.Register(id, () => new FakeView(id)));
        }

        [Test]
        public void Open_MissingViewReturnsStructuredFailure()
        {
            var navigator = new InMemoryMxUiNavigator(new InMemoryMxUiViewRegistry());

            MxUiOpenResult result = navigator.Open(new MxUiViewId("missing.view"), args: string.Empty);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewNotFound, result.ErrorCode);
            StringAssert.Contains("missing.view", result.Message);
        }

        [Test]
        public void Open_RegisteredViewShowsSynchronously()
        {
            var registry = new InMemoryMxUiViewRegistry();
            var id = new MxUiViewId("hud.main");
            registry.Register(id, () => new FakeView(id));
            var navigator = new InMemoryMxUiNavigator(registry);

            MxUiOpenResult result = navigator.Open(id, args: 0);

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsNotNull(result.View);
            Assert.AreEqual(MxUiLifecycleState.Visible, result.View.Lifecycle.State);
            Assert.IsTrue(navigator.IsOpen(id));
        }

        [Test]
        public void OpenAsync_CompletesImmediatelyForInMemoryNavigator()
        {
            var registry = new InMemoryMxUiViewRegistry();
            var id = new MxUiViewId("popup.inventory");
            registry.Register(id, () => new FakeView(id));
            var navigator = new InMemoryMxUiNavigator(registry);
            MxUiOpenResult callbackResult = default;

            MxUiOpenOperation operation = navigator.OpenAsync(id, args: string.Empty);
            operation.Completed += result => callbackResult = result;

            Assert.AreEqual(MxUiOpenOperationStatus.Succeeded, operation.Status);
            Assert.IsTrue(operation.Result.Success, operation.Result.Message);
            Assert.IsTrue(callbackResult.Success, callbackResult.Message);
        }

        [Test]
        public void OpenAsync_MissingViewCompletesWithFailure()
        {
            var navigator = new InMemoryMxUiNavigator(new InMemoryMxUiViewRegistry());

            MxUiOpenOperation operation = navigator.OpenAsync(new MxUiViewId("missing.async"), args: string.Empty);

            Assert.AreEqual(MxUiOpenOperationStatus.Failed, operation.Status);
            Assert.IsFalse(operation.Result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewNotFound, operation.Result.ErrorCode);
        }

        [Test]
        public void Lifecycle_TransitionsAreIdempotentAndTerminal()
        {
            var lifecycle = new MxUiLifecycle();

            Assert.IsTrue(lifecycle.Show());
            Assert.IsFalse(lifecycle.Show());
            Assert.AreEqual(MxUiLifecycleState.Visible, lifecycle.State);
            Assert.IsTrue(lifecycle.Hide());
            Assert.IsFalse(lifecycle.Hide());
            Assert.AreEqual(MxUiLifecycleState.Hidden, lifecycle.State);
            Assert.IsTrue(lifecycle.Dispose());
            Assert.IsFalse(lifecycle.Dispose());
            Assert.IsFalse(lifecycle.Show());
            Assert.IsFalse(lifecycle.Hide());
            Assert.AreEqual(MxUiLifecycleState.Disposed, lifecycle.State);
        }

        [Test]
        public void Contract_ValidatesDescriptorAndCommandIds()
        {
            var missingId = new MxUiViewContract(new MxUiViewDescriptor(default, "pkg", "Comp", MxUiLayer.Panel));
            missingId.Commands = new[]
            {
                new MxUiCommandDescriptor { CommandId = "open" },
                new MxUiCommandDescriptor { CommandId = "open" },
                new MxUiCommandDescriptor()
            };

            MxUiContractValidationResult validation = missingId.Validate();

            Assert.IsFalse(validation.Success);
            Assert.AreEqual(3, validation.Errors.Count);
            StringAssert.Contains("View id", validation.Errors[0]);
            StringAssert.Contains("Duplicate command id", validation.Errors[1]);
            StringAssert.Contains("non-empty id", validation.Errors[2]);
        }

        [Test]
        public void ContractRegistry_RejectsInvalidContractsAndDuplicateViewIds()
        {
            var registry = new MxUiViewContractRegistry();
            var id = new MxUiViewId("settings.panel");
            registry.Register(new MxUiViewContract(new MxUiViewDescriptor(id, "settings", "SettingsPanel", MxUiLayer.Panel)));

            Assert.Throws<ArgumentException>(() => registry.Register(
                new MxUiViewContract(new MxUiViewDescriptor(id, "settings", "SettingsPanel", MxUiLayer.Panel))));
            Assert.Throws<ArgumentException>(() => registry.Register(
                new MxUiViewContract(new MxUiViewDescriptor(default, "broken", "Broken", MxUiLayer.Panel))));
        }

        [Test]
        public void CommandSink_CarriesCommandDescriptorContractShape()
        {
            var sink = new RecordingCommandSink();
            var id = new MxUiViewId("hud.main");
            var descriptor = new MxUiCommandDescriptor
            {
                CommandId = "inventory.open",
                PayloadType = "InventoryOpenArgs",
                RiskLevel = "Low",
                Owner = "RuntimeHud",
                IsReadOnly = true
            };

            sink.Enqueue(new MxUiCommand(id, descriptor.CommandId, payload: null));

            Assert.IsTrue(descriptor.IsValid());
            Assert.AreEqual(id, sink.LastCommand.SourceViewId);
            Assert.AreEqual("inventory.open", sink.LastCommand.CommandId);
        }

        private sealed class FakeView : IMxUiView
        {
            public FakeView(MxUiViewId id)
            {
                Id = id;
                Lifecycle = new MxUiLifecycle();
            }

            public MxUiViewId Id { get; }
            public MxUiLifecycle Lifecycle { get; }

            public void Show()
            {
                Lifecycle.Show();
            }

            public void Hide()
            {
                Lifecycle.Hide();
            }

            public void Dispose()
            {
                Lifecycle.Dispose();
            }
        }

        private sealed class RecordingCommandSink : IMxUiCommandSink
        {
            public MxUiCommand LastCommand { get; private set; }

            public void Enqueue(MxUiCommand command)
            {
                LastCommand = command;
            }
        }
    }
}
