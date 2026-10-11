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
        public void Open_RegisteredFactoryReturningNullReturnsCreateFailed()
        {
            var registry = new InMemoryMxUiViewRegistry();
            var id = new MxUiViewId("broken.factory");
            registry.Register(id, () => null);
            var navigator = new InMemoryMxUiNavigator(registry);

            MxUiOpenResult result = navigator.Open(id, args: string.Empty);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, result.ErrorCode);
            StringAssert.Contains("factory returned null", result.Message);
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
        public void Open_BindsArgsWhenViewImplementsTypedContract()
        {
            var registry = new InMemoryMxUiViewRegistry();
            var id = new MxUiViewId("popup.confirm");
            var view = new BindingView(id);
            registry.Register(id, () => view);
            var navigator = new InMemoryMxUiNavigator(registry);

            MxUiOpenResult result = navigator.Open(id, new OpenArgs("delete"));

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(1, view.BindCount);
            Assert.AreEqual("delete", view.LastArgs.Action);
        }

        [Test]
        public void Open_RebindsArgsForAlreadyOpenTypedView()
        {
            var registry = new InMemoryMxUiViewRegistry();
            var id = new MxUiViewId("popup.confirm");
            var view = new BindingView(id);
            registry.Register(id, () => view);
            var navigator = new InMemoryMxUiNavigator(registry);

            navigator.Open(id, new OpenArgs("first"));
            MxUiOpenResult result = navigator.Open(id, new OpenArgs("second"));

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(2, view.BindCount);
            Assert.AreEqual("second", view.LastArgs.Action);
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
        public void Opened_NullViewReturnsCreateFailed()
        {
            MxUiOpenResult result = MxUiOpenResult.Opened(null);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, result.ErrorCode);
            Assert.IsNull(result.View);
        }

        [Test]
        public void Complete_DefaultResultNormalizesFailureErrorCode()
        {
            var operation = new MxUiOpenOperation();

            bool completed = operation.Complete(default);

            Assert.IsTrue(completed);
            Assert.AreEqual(MxUiOpenOperationStatus.Failed, operation.Status);
            Assert.IsFalse(operation.Result.Success);
            Assert.AreEqual(MxUiOpenErrorCode.ViewCreateFailed, operation.Result.ErrorCode);
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
        public void Contract_RejectsMissingPackageComponentAndInvalidCommandFlags()
        {
            var contract = new MxUiViewContract(new MxUiViewDescriptor(new MxUiViewId("broken.view"), "", "", MxUiLayer.Panel));
            contract.Commands = new[]
            {
                new MxUiCommandDescriptor
                {
                    CommandId = "settings.preview",
                    IsReadOnly = true,
                    RequiresConfirmation = true
                }
            };

            MxUiContractValidationResult validation = contract.Validate();

            Assert.IsFalse(validation.Success);
            Assert.AreEqual(3, validation.Errors.Count);
            StringAssert.Contains("package key", validation.Errors[0]);
            StringAssert.Contains("component name", validation.Errors[1]);
            StringAssert.Contains("coherent flags", validation.Errors[2]);
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

        [Test]
        public void LocalizationRequest_UsesProviderNeutralKeyLocaleAndRevision()
        {
            var provider = new RecordingTextProvider(new MxUiLocaleId("en-US"));
            provider.Register(new MxUiTextKey("ui.runtimehud.title"), "Runtime HUD");

            bool found = provider.TryGetText(
                new MxUiLocalizedTextRequest(new MxUiTextKey("ui.runtimehud.title"), "Fallback"),
                out string text);

            Assert.IsTrue(found);
            Assert.AreEqual("Runtime HUD", text);
            Assert.AreEqual(new MxUiLocaleId("en-US"), provider.CurrentLocale);
            Assert.AreEqual(1L, provider.Revision);
        }

        [Test]
        public void LocalizationIds_NormalizeNullAndCompareOrdinal()
        {
            Assert.IsFalse(default(MxUiTextKey).IsValid);
            Assert.IsFalse(new MxUiTextKey(" ").IsValid);
            Assert.IsTrue(new MxUiTextKey("ui.title").IsValid);
            Assert.AreEqual(new MxUiTextKey("ui.title"), new MxUiTextKey("ui.title"));
            Assert.AreNotEqual(new MxUiTextKey("ui.title"), new MxUiTextKey("UI.TITLE"));

            Assert.IsFalse(default(MxUiLocaleId).IsValid);
            Assert.IsTrue(new MxUiLocaleId("zh-CN").IsValid);
            Assert.AreEqual(new MxUiLocaleId("zh-CN"), new MxUiLocaleId("zh-CN"));
            Assert.AreNotEqual(new MxUiLocaleId("zh-CN"), new MxUiLocaleId("zh-cn"));
        }

        [Test]
        public void NullTextProvider_UsesFallbackWithoutGlobalLookup()
        {
            bool found = MxUiNullTextProvider.Instance.TryGetText(
                new MxUiLocalizedTextRequest(new MxUiTextKey("missing.key"), "Fallback Text"),
                out string text);

            Assert.IsTrue(found);
            Assert.AreEqual("Fallback Text", text);
            Assert.AreEqual(0L, MxUiNullTextProvider.Instance.Revision);
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

        private readonly struct OpenArgs
        {
            public OpenArgs(string action)
            {
                Action = action;
            }

            public string Action { get; }
        }

        private sealed class BindingView : IMxUiView<OpenArgs>
        {
            public BindingView(MxUiViewId id)
            {
                Id = id;
                Lifecycle = new MxUiLifecycle();
            }

            public MxUiViewId Id { get; }
            public MxUiLifecycle Lifecycle { get; }
            public int BindCount { get; private set; }
            public OpenArgs LastArgs { get; private set; }

            public void Bind(OpenArgs model)
            {
                BindCount++;
                LastArgs = model;
            }

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

        private sealed class RecordingTextProvider : IMxUiTextProvider
        {
            private readonly System.Collections.Generic.Dictionary<MxUiTextKey, string> _texts =
                new System.Collections.Generic.Dictionary<MxUiTextKey, string>();

            public RecordingTextProvider(MxUiLocaleId locale)
            {
                CurrentLocale = locale;
            }

            public MxUiLocaleId CurrentLocale { get; }
            public long Revision { get; private set; }

            public void Register(MxUiTextKey key, string text)
            {
                _texts[key] = text ?? string.Empty;
                Revision++;
            }

            public bool TryGetText(MxUiLocalizedTextRequest request, out string text)
            {
                if (_texts.TryGetValue(request.Key, out text))
                    return true;

                text = request.FallbackText ?? string.Empty;
                return !string.IsNullOrEmpty(text);
            }
        }
    }
}
