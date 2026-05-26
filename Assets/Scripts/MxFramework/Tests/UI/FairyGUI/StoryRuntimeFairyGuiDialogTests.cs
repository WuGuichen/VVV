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
            }
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
        public void GeneratedManifest_ProjectsStoryDialogContract()
        {
            MxUiViewContract contract = StoryRuntimeFairyGuiDialogManifest.CreateViewContract();

            Assert.AreEqual(StoryRuntimeFairyGuiDialogIds.ViewId, contract.Descriptor.Id);
            Assert.AreEqual(StoryRuntimeFairyGuiDialogIds.PackageId, contract.Descriptor.PackageKey);
            Assert.AreEqual(StoryRuntimeFairyGuiDialogIds.ComponentName, contract.Descriptor.ComponentName);
            Assert.AreEqual(MxUiLayer.Modal, contract.Descriptor.Layer);
            Assert.AreEqual(2, contract.Commands.Count);
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
