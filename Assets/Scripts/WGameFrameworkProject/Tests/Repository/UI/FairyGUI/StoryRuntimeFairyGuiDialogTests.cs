using System.Collections.Generic;
using System.IO;
using MxFramework.Demo.FairyGui;
using MxFramework.Demo.Story;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Story.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using NUnit.Framework;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.UI.FairyGui
{
    public sealed class StoryRuntimeFairyGuiDialogTests
    {
        [Test]
        public void Build_FromStorySnapshot_ExposesContinueAndChoiceStates()
        {
            using (var demo = new StoryRuntimeVerticalSliceDemo())
            {
                StoryRuntimeVerticalSliceFairyGuiViewModel ready =
                    StoryRuntimeVerticalSliceFairyGuiViewModelBuilder.Build(demo.CreateSnapshot());
                Assert.AreEqual("Ready", ready.Phase);
                Assert.IsFalse(Find(ready, StoryRuntimeVerticalSliceUiCommandIds.CompletePresentation).Enabled);
                Assert.IsFalse(Find(ready, StoryRuntimeVerticalSliceUiCommandIds.SelectChoice).Enabled);

                Assert.IsTrue(demo.RaiseTriggerAndTick());
                StoryRuntimeVerticalSliceFairyGuiViewModel waiting =
                    StoryRuntimeVerticalSliceFairyGuiViewModelBuilder.Build(demo.CreateSnapshot());
                Assert.AreEqual("Presentation waiting", waiting.Phase);
                Assert.IsTrue(Find(waiting, StoryRuntimeVerticalSliceUiCommandIds.CompletePresentation).Enabled);
                Assert.IsFalse(Find(waiting, StoryRuntimeVerticalSliceUiCommandIds.SelectChoice).Enabled);

                Assert.IsTrue(demo.CompletePresentationAndTick());
                StoryRuntimeVerticalSliceFairyGuiViewModel choice =
                    StoryRuntimeVerticalSliceFairyGuiViewModelBuilder.Build(demo.CreateSnapshot());
                Assert.AreEqual("Choice available", choice.Phase);
                Assert.IsFalse(Find(choice, StoryRuntimeVerticalSliceUiCommandIds.CompletePresentation).Enabled);
                Assert.IsTrue(Find(choice, StoryRuntimeVerticalSliceUiCommandIds.SelectChoice).Enabled);
                Assert.AreEqual(1, choice.Choices.Count);
                Assert.AreEqual(StoryRuntimeVerticalSliceDemo.StabilizeChoiceId, choice.Choices[0].ChoiceId);
            }
        }

        [Test]
        public void Build_FromStorySnapshot_ExposesStableChoiceListAndLegacyPrimaryChoice()
        {
            StoryRuntimeVerticalSliceFairyGuiViewModel viewModel =
                StoryRuntimeVerticalSliceFairyGuiViewModelBuilder.Build(CreateMultiChoiceSnapshot(), 100);

            Assert.AreEqual("Choice available", viewModel.Phase);
            Assert.AreEqual("Trace signal", viewModel.ChoiceText);
            Assert.AreEqual(3, viewModel.Choices.Count);
            Assert.AreEqual("Trace signal", viewModel.Choices[0].Text);
            Assert.IsTrue(viewModel.Choices[0].Enabled);
            Assert.AreEqual("Ignore signal", viewModel.Choices[1].Text);
            Assert.IsFalse(viewModel.Choices[1].Enabled);
            Assert.AreEqual("Force bridge", viewModel.Choices[2].Text);

            var payload = (StoryRuntimeVerticalSliceSelectChoicePayload)viewModel.Choices[2].Command.Payload;
            Assert.AreEqual(100, payload.GraphId);
            Assert.AreEqual(11, payload.BeatInstanceId);
            Assert.AreEqual(55, payload.ChoiceId);
            Assert.AreEqual(44, ((StoryRuntimeVerticalSliceSelectChoicePayload)Find(
                viewModel,
                StoryRuntimeVerticalSliceUiCommandIds.SelectChoice).Payload).ChoiceId);
        }

        [Test]
        public void Bind_WithConstructedComponent_WritesTextAndButtonState()
        {
            Fgui.GComponent root = CreateDialogRoot();
            using (var handle = new MxFairyGuiComponentHandle(
                StoryRuntimeFairyGuiDialogIds.PackageId,
                StoryRuntimeFairyGuiDialogIds.ComponentName,
                root))
            {
                var binder = new StoryRuntimeFairyGuiDialogBinder(new RecordingUiCommandSink());

                binder.Bind(handle, CreateChoiceViewModel());

                Assert.AreEqual("Story", Text(root, StoryRuntimeFairyGuiDialogIds.Title));
                Assert.AreEqual("Choice available", Text(root, StoryRuntimeFairyGuiDialogIds.Phase));
                Assert.AreEqual("The signal is unstable.", Text(root, StoryRuntimeFairyGuiDialogIds.DialogueText));
                Assert.AreEqual("Stabilize signal", Text(root, StoryRuntimeFairyGuiDialogIds.ChoiceText));
                Assert.AreEqual("Signal 1 / commands 2", Text(root, StoryRuntimeFairyGuiDialogIds.SignalText));
                Assert.AreEqual("Continue", Button(root, StoryRuntimeFairyGuiDialogIds.Continue).text);
                Assert.IsFalse(Button(root, StoryRuntimeFairyGuiDialogIds.Continue).enabled);
                Assert.AreEqual("Stabilize signal", Button(root, StoryRuntimeFairyGuiDialogIds.Choice).text);
                Assert.IsTrue(Button(root, StoryRuntimeFairyGuiDialogIds.Choice).enabled);
            }
        }

        [Test]
        public void Bind_WithMultipleChoices_CreatesChoiceListAndSkipsDisabledChoice()
        {
            Fgui.GComponent root = CreateDialogRoot();
            var sink = new RecordingUiCommandSink();
            using (var handle = new MxFairyGuiComponentHandle(
                StoryRuntimeFairyGuiDialogIds.PackageId,
                StoryRuntimeFairyGuiDialogIds.ComponentName,
                root))
            {
                var binder = new StoryRuntimeFairyGuiDialogBinder(sink);

                binder.Bind(handle, CreateMultiChoiceViewModel());

                Assert.AreEqual("Trace signal", Button(root, StoryRuntimeFairyGuiDialogIds.Choice).text);
                Assert.IsTrue(Button(root, StoryRuntimeFairyGuiDialogIds.Choice).enabled);
                Assert.AreEqual("Ignore signal", Button(root, StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + 1).text);
                Assert.IsFalse(Button(root, StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + 1).enabled);
                Assert.AreEqual("Force bridge", Button(root, StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + 2).text);
                Assert.IsTrue(Button(root, StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + 2).enabled);
                Assert.IsFalse(Object(root, StoryRuntimeFairyGuiDialogIds.ChoiceText).visible);
                Assert.LessOrEqual(
                    Button(root, StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + 2).y +
                    Button(root, StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + 2).height,
                    Object(root, StoryRuntimeFairyGuiDialogIds.SignalText).y);
                Assert.GreaterOrEqual(Object(root, StoryRuntimeFairyGuiDialogIds.Continue).y, 374f);

                Button(root, StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + 1).onClick.Call();
                Assert.AreEqual(0, sink.Count);

                Button(root, StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + 2).onClick.Call();
                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(StoryRuntimeVerticalSliceUiCommandIds.SelectChoice, sink.Last.CommandId);
                var payload = (StoryRuntimeVerticalSliceSelectChoicePayload)sink.Last.Payload;
                Assert.AreEqual(100, payload.GraphId);
                Assert.AreEqual(11, payload.BeatInstanceId);
                Assert.AreEqual(55, payload.ChoiceId);
            }
        }

        [Test]
        public void Bind_WithManyChoices_KeepsChoiceListInsideBoundedStoryArea()
        {
            Fgui.GComponent root = CreateDialogRoot();
            using (var handle = new MxFairyGuiComponentHandle(
                StoryRuntimeFairyGuiDialogIds.PackageId,
                StoryRuntimeFairyGuiDialogIds.ComponentName,
                root))
            {
                var binder = new StoryRuntimeFairyGuiDialogBinder(new RecordingUiCommandSink());

                binder.Bind(handle, CreateManyChoiceViewModel(6));

                Fgui.GButton last = Button(root, StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + 5);
                Assert.IsNotNull(last);
                Assert.LessOrEqual(last.y + last.height, Object(root, StoryRuntimeFairyGuiDialogIds.SignalText).y);
                Assert.Greater(last.height, 0f);
                Assert.Less(last.height, 22f);
            }
        }

        [Test]
        public void Bind_ButtonClicks_EnqueueStoryUiCommandsWithPayloads()
        {
            Fgui.GComponent root = CreateDialogRoot();
            var sink = new RecordingUiCommandSink();
            using (var handle = new MxFairyGuiComponentHandle(
                StoryRuntimeFairyGuiDialogIds.PackageId,
                StoryRuntimeFairyGuiDialogIds.ComponentName,
                root))
            {
                var binder = new StoryRuntimeFairyGuiDialogBinder(sink);
                binder.Bind(handle, CreateWaitingViewModel());

                Button(root, StoryRuntimeFairyGuiDialogIds.Continue).onClick.Call();
                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(StoryRuntimeFairyGuiDialogIds.ViewId, sink.Last.SourceViewId);
                Assert.AreEqual(StoryRuntimeVerticalSliceUiCommandIds.CompletePresentation, sink.Last.CommandId);
                Assert.IsInstanceOf<StoryRuntimeVerticalSliceCompletePresentationPayload>(sink.Last.Payload);

                binder.Bind(handle, CreateChoiceViewModel());
                Button(root, StoryRuntimeFairyGuiDialogIds.Choice).onClick.Call();
                Assert.AreEqual(2, sink.Count);
                Assert.AreEqual(StoryRuntimeVerticalSliceUiCommandIds.SelectChoice, sink.Last.CommandId);
                Assert.IsInstanceOf<StoryRuntimeVerticalSliceSelectChoicePayload>(sink.Last.Payload);
            }
        }

        [Test]
        public void Bind_ConfiguresDefaultFocusAndSkipsDisabledStoryActions()
        {
            Fgui.GComponent root = CreateDialogRoot();
            using (var handle = new MxFairyGuiComponentHandle(
                StoryRuntimeFairyGuiDialogIds.PackageId,
                StoryRuntimeFairyGuiDialogIds.ComponentName,
                root))
            {
                var binder = new StoryRuntimeFairyGuiDialogBinder(new RecordingUiCommandSink());
                binder.Bind(handle, CreateChoiceViewModel());

                Assert.IsTrue(Button(root, StoryRuntimeFairyGuiDialogIds.Continue).focusable);
                Assert.IsTrue(Button(root, StoryRuntimeFairyGuiDialogIds.Choice).focusable);
                Assert.IsFalse(Button(root, StoryRuntimeFairyGuiDialogIds.Continue).enabled);
                Assert.IsTrue(Button(root, StoryRuntimeFairyGuiDialogIds.Choice).focused);
                Assert.IsTrue(MxFairyGuiFocusNavigation.MoveNext(root));
                Assert.IsTrue(Button(root, StoryRuntimeFairyGuiDialogIds.Choice).focused);
            }
        }

        [Test]
        public void CommandSink_MapsUiCommandsToStoryRuntimeCommands()
        {
            var target = new RecordingStoryCommandTarget(new RuntimeFrame(7));
            var sink = new StoryRuntimeVerticalSliceUiCommandSink(target);

            sink.Enqueue(new MxUiCommand(
                StoryRuntimeFairyGuiDialogIds.ViewId,
                StoryRuntimeVerticalSliceUiCommandIds.CompletePresentation,
                new StoryRuntimeVerticalSliceCompletePresentationPayload(100, 12, 34, "continue.trace")));

            Assert.IsTrue(sink.LastResult.Success);
            Assert.AreEqual(StoryRuntimeCommandIds.CompletePresentation, target.Last.CommandId);
            Assert.AreEqual(StoryRuntimeCommandSources.PresentationAdapter, target.Last.SourceId);
            Assert.AreEqual(100, target.Last.TargetId);
            Assert.AreEqual(12, target.Last.Payload0);
            Assert.AreEqual(34, target.Last.Payload1);

            sink.Enqueue(new MxUiCommand(
                StoryRuntimeFairyGuiDialogIds.ViewId,
                StoryRuntimeVerticalSliceUiCommandIds.SelectChoice,
                new StoryRuntimeVerticalSliceSelectChoicePayload(100, 12, 56, "choice.trace")));

            Assert.IsTrue(sink.LastResult.Success);
            Assert.AreEqual(StoryRuntimeCommandIds.SelectChoice, target.Last.CommandId);
            Assert.AreEqual(StoryRuntimeCommandSources.UiAdapter, target.Last.SourceId);
            Assert.AreEqual(100, target.Last.TargetId);
            Assert.AreEqual(12, target.Last.Payload0);
            Assert.AreEqual(56, target.Last.Payload1);
        }

        [Test]
        public void Open_WithPublishedStoryDialogPackage_LoadsBindsAndReleases()
        {
            RemoveStoryDialogPackageIfLoaded();

            byte[] packageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxStoryDialog/MxStoryDialog_fui.bytes"));
            Assert.Greater(packageBytes.Length, 4);
            Assert.AreEqual((byte)'F', packageBytes[0]);
            Assert.AreEqual((byte)'G', packageBytes[1]);
            Assert.AreEqual((byte)'U', packageBytes[2]);
            Assert.AreEqual((byte)'I', packageBytes[3]);

            ResourceManager manager = CreateManager(
                new MemoryResourceProvider().Register("fgui/MxStoryDialog_fui.bytes", packageBytes),
                Entry(StoryRuntimeFairyGuiDialogIds.PackageBytesKey, "fgui/MxStoryDialog_fui.bytes"));
            var target = new RecordingStoryCommandTarget(new RuntimeFrame(4));
            StoryRuntimeFairyGuiDialogShell shell = StoryRuntimeFairyGuiDialogComposition.CreateShell(manager, target);

            try
            {
                MxUiOpenResult result = shell.Open(CreateChoiceViewModel());

                Assert.IsTrue(result.Success, result.Message);
                Assert.IsTrue(shell.IsOpen);

                var view = result.View as MxFairyGuiView<StoryRuntimeVerticalSliceFairyGuiViewModel>;
                Assert.IsNotNull(view);
                var handle = view.Component as MxFairyGuiComponentHandle;
                Assert.IsNotNull(handle);
                Fgui.GComponent root = handle.Component;
                Assert.AreEqual("Choice available", Text(root, StoryRuntimeFairyGuiDialogIds.Phase));

                Button(root, StoryRuntimeFairyGuiDialogIds.Choice).onClick.Call();
                Assert.AreEqual(StoryRuntimeCommandIds.SelectChoice, target.Last.CommandId);
                Assert.AreEqual(1, shell.AcceptedCommandCount);

                Assert.IsTrue(shell.Close());
                Assert.IsFalse(shell.IsOpen);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
                Assert.IsNull(Fgui.UIPackage.GetByName(StoryRuntimeFairyGuiDialogIds.PackageId));
            }
            finally
            {
                if (shell.IsOpen)
                    shell.Close();

                RemoveStoryDialogPackageIfLoaded();
            }
        }

        [Test]
        public void Shell_Refresh_RebindsStoryDialogWithInjectedLocalizationProvider()
        {
            RemoveStoryDialogPackageIfLoaded();

            byte[] packageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxStoryDialog/MxStoryDialog_fui.bytes"));
            ResourceManager manager = CreateManager(
                new MemoryResourceProvider().Register("fgui/MxStoryDialog_fui.bytes", packageBytes),
                Entry(StoryRuntimeFairyGuiDialogIds.PackageBytesKey, "fgui/MxStoryDialog_fui.bytes"));
            var target = new RecordingStoryCommandTarget(new RuntimeFrame(4));
            Dictionary<string, string> localization = CreateLocalization(
                ("en-US", "ui.story.dialog.title", "Story"),
                ("en-US", "story.text.11", "The signal is unstable."),
                ("en-US", "story.choice.33", "Stabilize signal"),
                ("zh-CN", "ui.story.dialog.title", "剧情"),
                ("zh-CN", "story.text.11", "信号不稳定。"),
                ("zh-CN", "story.choice.33", "稳定信号"));
            var textProvider = new MxDelegateUiTextProvider(
                (MxUiTextKey key, MxUiLocaleId locale, out string text) => localization.TryGetValue(locale.Value + "|" + key.Value, out text),
                new MxUiLocaleId("en-US"));
            StoryRuntimeFairyGuiDialogShell shell = StoryRuntimeFairyGuiDialogComposition.CreateShell(
                manager,
                target,
                textProvider: textProvider);

            try
            {
                MxUiOpenResult firstResult = shell.Open(CreateChoiceViewModel());

                Assert.IsTrue(firstResult.Success, firstResult.Message);
                var view = firstResult.View as MxFairyGuiView<StoryRuntimeVerticalSliceFairyGuiViewModel>;
                Assert.IsNotNull(view);
                var handle = view.Component as MxFairyGuiComponentHandle;
                Assert.IsNotNull(handle);
                Fgui.GComponent root = handle.Component;
                Assert.AreEqual("Story", Text(root, StoryRuntimeFairyGuiDialogIds.Title));
                Assert.AreEqual("The signal is unstable.", Text(root, StoryRuntimeFairyGuiDialogIds.DialogueText));
                Assert.AreEqual("Stabilize signal", Text(root, StoryRuntimeFairyGuiDialogIds.ChoiceText));

                textProvider.SetLocale(new MxUiLocaleId("zh-CN"));
                MxUiOpenResult refreshResult = shell.Refresh(CreateChoiceViewModel());

                Assert.IsTrue(refreshResult.Success, refreshResult.Message);
                Assert.AreSame(firstResult.View, refreshResult.View);
                Assert.AreEqual("剧情", Text(root, StoryRuntimeFairyGuiDialogIds.Title));
                Assert.AreEqual("信号不稳定。", Text(root, StoryRuntimeFairyGuiDialogIds.DialogueText));
                Assert.AreEqual("稳定信号", Text(root, StoryRuntimeFairyGuiDialogIds.ChoiceText));
                Assert.AreEqual(new MxUiLocaleId("zh-CN"), textProvider.CurrentLocale);
                Assert.Greater(textProvider.Revision, 1L);
            }
            finally
            {
                if (shell.IsOpen)
                    shell.Close();

                RemoveStoryDialogPackageIfLoaded();
            }
        }

        [Test]
        public void GeneratedManifest_ProjectsStoryDialogContract()
        {
            MxUiViewContract contract = StoryRuntimeFairyGuiDialogManifest.CreateViewContract();

            Assert.AreEqual(StoryRuntimeFairyGuiDialogIds.ViewId, contract.Descriptor.Id);
            Assert.AreEqual(StoryRuntimeFairyGuiDialogIds.PackageId, contract.Descriptor.PackageKey);
            Assert.AreEqual(StoryRuntimeFairyGuiDialogIds.ComponentName, contract.Descriptor.ComponentName);
            Assert.AreEqual(MxUiLayer.Modal, contract.Descriptor.Layer);
            Assert.AreEqual(2, contract.Commands.Count);
        }

        [Test]
        public void Bind_WhenLocalizationMisses_UsesRequestFallbackTextBeforeLegacyField()
        {
            Fgui.GComponent root = CreateDialogRoot();
            var provider = new MxDelegateUiTextProvider(
                (MxUiTextKey key, MxUiLocaleId locale, out string text) =>
                {
                    text = string.Empty;
                    return false;
                },
                new MxUiLocaleId("en-US"));
            using (var handle = new MxFairyGuiComponentHandle(
                StoryRuntimeFairyGuiDialogIds.PackageId,
                StoryRuntimeFairyGuiDialogIds.ComponentName,
                root))
            {
                var viewModel = CreateChoiceViewModel();
                viewModel.DialogueText = "legacy dialogue";
                viewModel.DialogueLocalizedText = new MxUiLocalizedTextRequest(
                    new MxUiTextKey("story.text.missing"),
                    "request dialogue fallback");
                var binder = new StoryRuntimeFairyGuiDialogBinder(new RecordingUiCommandSink(), provider);

                binder.Bind(handle, viewModel);

                Assert.AreEqual("request dialogue fallback", Text(root, StoryRuntimeFairyGuiDialogIds.DialogueText));
            }
        }

        private static StoryRuntimeVerticalSliceFairyGuiViewModel CreateWaitingViewModel()
        {
            var snapshot = new StoryRuntimeVerticalSliceSnapshot(
                new RuntimeFrame(1),
                2,
                MxFramework.Story.StoryGraphRuntimeStatus.Active,
                11,
                22,
                0,
                0,
                "The signal is unstable.",
                string.Empty,
                1,
                10,
                "not saved",
                "not replayed",
                "facts",
                "preload",
                2,
                new[] { "triggered" });

            return StoryRuntimeVerticalSliceFairyGuiViewModelBuilder.Build(snapshot, 100);
        }

        private static StoryRuntimeVerticalSliceFairyGuiViewModel CreateChoiceViewModel()
        {
            var snapshot = new StoryRuntimeVerticalSliceSnapshot(
                new RuntimeFrame(2),
                3,
                MxFramework.Story.StoryGraphRuntimeStatus.Active,
                0,
                0,
                11,
                33,
                "The signal is unstable.",
                "Stabilize signal",
                1,
                10,
                "not saved",
                "not replayed",
                "facts",
                "preload",
                2,
                new[] { "presentation complete", "choice available" });

            return StoryRuntimeVerticalSliceFairyGuiViewModelBuilder.Build(snapshot, 100);
        }

        private static StoryRuntimeVerticalSliceFairyGuiViewModel CreateMultiChoiceViewModel()
        {
            return StoryRuntimeVerticalSliceFairyGuiViewModelBuilder.Build(CreateMultiChoiceSnapshot(), 100);
        }

        private static StoryRuntimeVerticalSliceFairyGuiViewModel CreateManyChoiceViewModel(int count)
        {
            var choices = new StoryRuntimeVerticalSliceChoiceSnapshot[count];
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i] = new StoryRuntimeVerticalSliceChoiceSnapshot(
                    100,
                    11,
                    4400 + i,
                    5400 + i,
                    "Choice " + i,
                    enabled: true);
            }

            var snapshot = new StoryRuntimeVerticalSliceSnapshot(
                new RuntimeFrame(2),
                3,
                MxFramework.Story.StoryGraphRuntimeStatus.Active,
                0,
                0,
                11,
                4400,
                "The signal is unstable.",
                "Choice 0",
                1,
                10,
                "not saved",
                "not replayed",
                "facts",
                "preload",
                2,
                choices,
                new[] { "presentation complete", "choices available" });

            return StoryRuntimeVerticalSliceFairyGuiViewModelBuilder.Build(snapshot, 100);
        }

        private static StoryRuntimeVerticalSliceSnapshot CreateMultiChoiceSnapshot()
        {
            return new StoryRuntimeVerticalSliceSnapshot(
                new RuntimeFrame(2),
                3,
                MxFramework.Story.StoryGraphRuntimeStatus.Active,
                0,
                0,
                11,
                44,
                "The signal is unstable.",
                "Trace signal",
                1,
                10,
                "not saved",
                "not replayed",
                "facts",
                "preload",
                2,
                new[]
                {
                    new StoryRuntimeVerticalSliceChoiceSnapshot(100, 11, 44, 4401, "Trace signal", true),
                    new StoryRuntimeVerticalSliceChoiceSnapshot(100, 11, 45, 4402, "Ignore signal", false),
                    new StoryRuntimeVerticalSliceChoiceSnapshot(100, 11, 55, 4403, "Force bridge", true)
                },
                new[] { "presentation complete", "choices available" });
        }

        private static StoryRuntimeVerticalSliceUiCommandDescriptor Find(
            StoryRuntimeVerticalSliceFairyGuiViewModel viewModel,
            string commandId)
        {
            for (int i = 0; i < viewModel.Commands.Count; i++)
            {
                StoryRuntimeVerticalSliceUiCommandDescriptor command = viewModel.Commands[i];
                if (command.CommandId == commandId)
                    return command;
            }

            Assert.Fail("Command not found: " + commandId);
            return default;
        }

        private static Fgui.GComponent CreateDialogRoot()
        {
            var root = new Fgui.GComponent();
            AddText(root, StoryRuntimeFairyGuiDialogIds.Title);
            AddText(root, StoryRuntimeFairyGuiDialogIds.Phase);
            AddText(root, StoryRuntimeFairyGuiDialogIds.DialogueText);
            AddText(root, StoryRuntimeFairyGuiDialogIds.ChoiceText);
            AddText(root, StoryRuntimeFairyGuiDialogIds.SignalText);
            AddText(root, StoryRuntimeFairyGuiDialogIds.EventLog);
            AddButton(root, StoryRuntimeFairyGuiDialogIds.Continue);
            AddButton(root, StoryRuntimeFairyGuiDialogIds.Choice);
            return root;
        }

        private static void AddText(Fgui.GComponent root, string name)
        {
            root.AddChild(new Fgui.GTextField { name = name });
        }

        private static void AddButton(Fgui.GComponent root, string name)
        {
            root.AddChild(new Fgui.GButton { name = name });
        }

        private static string Text(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asTextField.text;
        }

        private static Fgui.GButton Button(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asButton;
        }

        private static Fgui.GObject Object(Fgui.GComponent root, string name)
        {
            return root.GetChild(name);
        }

        private static ResourceManager CreateManager(MemoryResourceProvider provider, params ResourceCatalogEntry[] entries)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog("ui.storydialog.test.catalog", string.Empty, entries));
            return manager;
        }

        private static ResourceCatalogEntry Entry(ResourceKey key, string address)
        {
            return new ResourceCatalogEntry(key.Id, key.TypeId, "memory", address, key.Variant, key.PackageId);
        }

        private static Dictionary<string, string> CreateLocalization(params (string Locale, string Key, string Text)[] entries)
        {
            var table = new Dictionary<string, string>();
            for (int i = 0; i < entries.Length; i++)
                table[entries[i].Locale + "|" + entries[i].Key] = entries[i].Text;

            return table;
        }

        private static void RemoveStoryDialogPackageIfLoaded()
        {
            if (Fgui.UIPackage.GetByName(StoryRuntimeFairyGuiDialogIds.PackageId) != null)
                Fgui.UIPackage.RemovePackage(StoryRuntimeFairyGuiDialogIds.PackageId);
        }

        private sealed class RecordingUiCommandSink : IMxUiCommandSink
        {
            public MxUiCommand Last { get; private set; }
            public int Count { get; private set; }

            public void Enqueue(MxUiCommand command)
            {
                Last = command;
                Count++;
            }
        }

        private sealed class RecordingStoryCommandTarget : IStoryRuntimeVerticalSliceUiCommandTarget
        {
            public RecordingStoryCommandTarget(RuntimeFrame frame)
            {
                CurrentCommandFrame = frame;
            }

            public RuntimeFrame CurrentCommandFrame { get; }
            public RuntimeCommand Last { get; private set; }

            public RuntimeCommandValidationResult EnqueueStoryCommand(RuntimeCommand command)
            {
                Last = command;
                return RuntimeCommandValidationResult.Accepted(command);
            }
        }
    }
}
