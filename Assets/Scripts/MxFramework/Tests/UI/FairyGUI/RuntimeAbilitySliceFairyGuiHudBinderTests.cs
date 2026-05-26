using System.IO;
using MxFramework.Demo;
using MxFramework.Demo.FairyGui;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using NUnit.Framework;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.UI.FairyGui
{
    public sealed class RuntimeAbilitySliceFairyGuiHudBinderTests
    {
        [Test]
        public void Bind_WithConstructedComponent_WritesMinimalHudText()
        {
            Fgui.GComponent root = CreateHudRoot();
            using (var handle = new MxFairyGuiComponentHandle(
                RuntimeAbilitySliceFairyGuiHudIds.PackageId,
                RuntimeAbilitySliceFairyGuiHudIds.ComponentName,
                root))
            {
                var binder = new RuntimeAbilitySliceFairyGuiHudBinder(new RecordingCommandSink());

                binder.Bind(handle, CreateViewModel());

                Assert.AreEqual("Ability HUD", Text(root, RuntimeAbilitySliceFairyGuiHudIds.Title));
                Assert.AreEqual("Config Driven", Text(root, RuntimeAbilitySliceFairyGuiHudIds.Mode));
                Assert.AreEqual("Player", Text(root, RuntimeAbilitySliceFairyGuiHudIds.PlayerName));
                StringAssert.Contains("HP 70/100", Text(root, RuntimeAbilitySliceFairyGuiHudIds.PlayerHp));
                Assert.AreEqual("Enemy", Text(root, RuntimeAbilitySliceFairyGuiHudIds.EnemyName));
                StringAssert.Contains("HP 12/50", Text(root, RuntimeAbilitySliceFairyGuiHudIds.EnemyHp));
                Assert.AreEqual("Recent action: player strike", Text(root, RuntimeAbilitySliceFairyGuiHudIds.RecentAction));
                Assert.AreEqual("Strike", Button(root, RuntimeAbilitySliceFairyGuiHudIds.Strike).text);
                Assert.IsTrue(Button(root, RuntimeAbilitySliceFairyGuiHudIds.Strike).enabled);
                Assert.AreEqual("Reset", Button(root, RuntimeAbilitySliceFairyGuiHudIds.Reset).text);
                Assert.IsFalse(Button(root, RuntimeAbilitySliceFairyGuiHudIds.Reset).enabled);
            }
        }

        [Test]
        public void Bind_ButtonClicks_EnqueueUiCommands()
        {
            Fgui.GComponent root = CreateHudRoot();
            var sink = new RecordingCommandSink();
            using (var handle = new MxFairyGuiComponentHandle(
                RuntimeAbilitySliceFairyGuiHudIds.PackageId,
                RuntimeAbilitySliceFairyGuiHudIds.ComponentName,
                root))
            {
                var binder = new RuntimeAbilitySliceFairyGuiHudBinder(sink);
                binder.Bind(handle, CreateViewModel());

                Button(root, RuntimeAbilitySliceFairyGuiHudIds.Strike).onClick.Call();
                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(RuntimeAbilitySliceFairyGuiHudIds.ViewId, sink.Last.SourceViewId);
                Assert.AreEqual(RuntimeAbilitySliceHudCommandIds.Strike, sink.Last.CommandId);

                Button(root, RuntimeAbilitySliceFairyGuiHudIds.Reset).onClick.Call();
                Assert.AreEqual(2, sink.Count);
                Assert.AreEqual(RuntimeAbilitySliceHudCommandIds.Reset, sink.Last.CommandId);
            }
        }

        [Test]
        public void Open_WithPublishedRuntimeHudPackage_LoadsBindsAndReleases()
        {
            RemoveRuntimeHudPackageIfLoaded();

            byte[] packageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxRuntimeHud/MxRuntimeHud_fui.bytes"));
            Assert.Greater(packageBytes.Length, 4);
            Assert.AreEqual((byte)'F', packageBytes[0]);
            Assert.AreEqual((byte)'G', packageBytes[1]);
            Assert.AreEqual((byte)'U', packageBytes[2]);
            Assert.AreEqual((byte)'I', packageBytes[3]);

            ResourceManager manager = CreateManager(
                new MemoryResourceProvider().Register("fgui/MxRuntimeHud_fui.bytes", packageBytes),
                Entry(RuntimeAbilitySliceFairyGuiHudIds.PackageBytesKey, "fgui/MxRuntimeHud_fui.bytes"));
            var sink = new RecordingCommandSink();
            MxFairyGuiNavigator navigator = RuntimeAbilitySliceFairyGuiHudComposition.CreateNavigator(manager, sink);

            try
            {
                MxUiOpenResult result = navigator.Open(RuntimeAbilitySliceFairyGuiHudIds.ViewId, CreateViewModel());

                Assert.IsTrue(result.Success, result.Message);
                Assert.IsTrue(navigator.IsOpen(RuntimeAbilitySliceFairyGuiHudIds.ViewId));
                Assert.AreEqual(MxUiLifecycleState.Visible, result.View.Lifecycle.State);

                Assert.IsTrue(navigator.Close(RuntimeAbilitySliceFairyGuiHudIds.ViewId));
                Assert.IsFalse(navigator.IsOpen(RuntimeAbilitySliceFairyGuiHudIds.ViewId));
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
                Assert.IsNull(Fgui.UIPackage.GetByName(RuntimeAbilitySliceFairyGuiHudIds.PackageId));
            }
            finally
            {
                if (navigator.IsOpen(RuntimeAbilitySliceFairyGuiHudIds.ViewId))
                    navigator.Close(RuntimeAbilitySliceFairyGuiHudIds.ViewId);

                RemoveRuntimeHudPackageIfLoaded();
            }
        }

        [Test]
        public void Shell_OpenRefreshCommandAndClose_UsesProductizedNavigatorPath()
        {
            RemoveRuntimeHudPackageIfLoaded();

            ResourceManager manager = CreatePublishedRuntimeHudManager();
            var target = new RecordingHudCommandTarget();
            RuntimeAbilitySliceFairyGuiHudShell shell = RuntimeAbilitySliceFairyGuiHudComposition.CreateShell(manager, target);

            try
            {
                RuntimeAbilitySliceHudViewModel first = CreateViewModel();
                MxUiOpenResult firstResult = shell.Open(first);

                Assert.IsTrue(firstResult.Success, firstResult.Message);
                Assert.IsTrue(shell.IsOpen);

                var view = firstResult.View as MxFairyGuiView<RuntimeAbilitySliceHudViewModel>;
                Assert.IsNotNull(view);
                var handle = view.Component as MxFairyGuiComponentHandle;
                Assert.IsNotNull(handle);
                Fgui.GComponent root = handle.Component;
                Assert.AreEqual("Ability HUD", Text(root, RuntimeAbilitySliceFairyGuiHudIds.Title));

                RuntimeAbilitySliceHudViewModel refreshed = CreateViewModel();
                refreshed.Title = "Ability HUD Refreshed";
                MxUiOpenResult refreshResult = shell.Refresh(refreshed);

                Assert.IsTrue(refreshResult.Success, refreshResult.Message);
                Assert.AreSame(firstResult.View, refreshResult.View);
                Assert.AreEqual("Ability HUD Refreshed", Text(root, RuntimeAbilitySliceFairyGuiHudIds.Title));

                Button(root, RuntimeAbilitySliceFairyGuiHudIds.Strike).onClick.Call();
                Assert.AreEqual(RuntimeAbilitySliceHudCommandIds.Strike, shell.LastCommand.CommandId);
                Assert.AreEqual(RuntimeAbilitySliceHudManualCommand.Strike, target.LastCommand);
                Assert.AreEqual(1, shell.AcceptedCommandCount);
                Assert.IsTrue(shell.LastCommandResult.Success);
                Assert.IsFalse(target.AutoSequenceEnabled);

                Button(root, RuntimeAbilitySliceFairyGuiHudIds.Reset).onClick.Call();
                Assert.AreEqual(RuntimeAbilitySliceHudCommandIds.Reset, shell.LastCommand.CommandId);
                Assert.AreEqual(RuntimeAbilitySliceHudManualCommand.Reset, target.LastCommand);
                Assert.AreEqual(2, shell.AcceptedCommandCount);
                Assert.IsTrue(shell.LastCommandResult.Success);

                Assert.IsTrue(shell.Close());
                Assert.IsFalse(shell.IsOpen);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
                Assert.IsNull(Fgui.UIPackage.GetByName(RuntimeAbilitySliceFairyGuiHudIds.PackageId));
            }
            finally
            {
                if (shell.IsOpen)
                    shell.Close();

                RemoveRuntimeHudPackageIfLoaded();
            }
        }

        [Test]
        public void GeneratedManifest_ValidatesPublishedRuntimeHudSourceAndProjectsContract()
        {
            MxFairyGuiManifest manifest = RuntimeAbilitySliceFairyGuiHudManifest.Create();
            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.ValidateSources(
                manifest,
                Directory.GetCurrentDirectory(),
                CreateManagerCatalog(RuntimeAbilitySliceFairyGuiHudIds.PackageBytesKey, "fgui/MxRuntimeHud_fui.bytes"));

            Assert.IsTrue(result.Success, FormatDiagnostics(result));

            MxUiViewContract contract = RuntimeAbilitySliceFairyGuiHudManifest.CreateViewContract();
            Assert.AreEqual(RuntimeAbilitySliceFairyGuiHudIds.ViewId, contract.Descriptor.Id);
            Assert.AreEqual(RuntimeAbilitySliceFairyGuiHudIds.PackageId, contract.Descriptor.PackageKey);
            Assert.AreEqual(RuntimeAbilitySliceFairyGuiHudIds.ComponentName, contract.Descriptor.ComponentName);
            Assert.AreEqual(2, contract.Commands.Count);
        }

        private static RuntimeAbilitySliceHudViewModel CreateViewModel()
        {
            var viewModel = new RuntimeAbilitySliceHudViewModel
            {
                Title = "Ability HUD",
                ModeName = "Config Driven",
                Commands = new[]
                {
                    new RuntimeAbilitySliceHudCommandDescriptor(RuntimeAbilitySliceHudCommandIds.Strike, "Strike", true),
                    new RuntimeAbilitySliceHudCommandDescriptor(RuntimeAbilitySliceHudCommandIds.Reset, "Reset", false)
                }
            };
            viewModel.Player.DisplayName = "Player";
            viewModel.Player.Hp = 70;
            viewModel.Player.MaxHp = 100;
            viewModel.Player.Attack = 16;
            viewModel.Player.Defense = 4;
            viewModel.Player.BuffSummary = "none";
            viewModel.Enemy.DisplayName = "Enemy";
            viewModel.Enemy.Hp = 12;
            viewModel.Enemy.MaxHp = 50;
            viewModel.Enemy.Attack = 9;
            viewModel.Enemy.Defense = 2;
            viewModel.Enemy.BuffSummary = "burning";
            viewModel.Feedback.RecentActionText = "Recent action: player strike";
            return viewModel;
        }

        private static Fgui.GComponent CreateHudRoot()
        {
            var root = new Fgui.GComponent();
            AddText(root, RuntimeAbilitySliceFairyGuiHudIds.Title);
            AddText(root, RuntimeAbilitySliceFairyGuiHudIds.Mode);
            AddText(root, RuntimeAbilitySliceFairyGuiHudIds.PlayerName);
            AddText(root, RuntimeAbilitySliceFairyGuiHudIds.PlayerHp);
            AddText(root, RuntimeAbilitySliceFairyGuiHudIds.EnemyName);
            AddText(root, RuntimeAbilitySliceFairyGuiHudIds.EnemyHp);
            AddText(root, RuntimeAbilitySliceFairyGuiHudIds.RecentAction);
            AddButton(root, RuntimeAbilitySliceFairyGuiHudIds.Strike);
            AddButton(root, RuntimeAbilitySliceFairyGuiHudIds.Reset);
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
            manager.AddCatalog(CreateCatalog(entries));
            return manager;
        }

        private static ResourceManager CreatePublishedRuntimeHudManager()
        {
            byte[] packageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxRuntimeHud/MxRuntimeHud_fui.bytes"));

            return CreateManager(
                new MemoryResourceProvider().Register("fgui/MxRuntimeHud_fui.bytes", packageBytes),
                Entry(RuntimeAbilitySliceFairyGuiHudIds.PackageBytesKey, "fgui/MxRuntimeHud_fui.bytes"));
        }

        private static ResourceCatalog CreateManagerCatalog(ResourceKey key, string address)
        {
            return CreateCatalog(Entry(key, address));
        }

        private static ResourceCatalog CreateCatalog(params ResourceCatalogEntry[] entries)
        {
            return new ResourceCatalog("ui.runtimehud.test.catalog", string.Empty, entries);
        }

        private static ResourceCatalogEntry Entry(ResourceKey key, string address)
        {
            return new ResourceCatalogEntry(key.Id, key.TypeId, "memory", address, key.Variant, key.PackageId);
        }

        private static void RemoveRuntimeHudPackageIfLoaded()
        {
            if (Fgui.UIPackage.GetByName(RuntimeAbilitySliceFairyGuiHudIds.PackageId) != null)
                Fgui.UIPackage.RemovePackage(RuntimeAbilitySliceFairyGuiHudIds.PackageId);
        }

        private static string FormatDiagnostics(MxFairyGuiManifestValidationResult result)
        {
            var lines = new System.Text.StringBuilder();
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                MxFairyGuiManifestDiagnostic diagnostic = result.Diagnostics[i];
                lines.Append(diagnostic.Code)
                    .Append(": ")
                    .Append(diagnostic.SourcePath)
                    .Append(" ")
                    .Append(diagnostic.Field)
                    .Append(" ")
                    .AppendLine(diagnostic.Message);
            }

            return lines.ToString();
        }

        private sealed class RecordingCommandSink : IMxUiCommandSink
        {
            public MxUiCommand Last { get; private set; }
            public int Count { get; private set; }

            public void Enqueue(MxUiCommand command)
            {
                Last = command;
                Count++;
            }
        }

        private sealed class RecordingHudCommandTarget : IRuntimeAbilitySliceHudCommandTarget
        {
            public bool IsInitialized { get; set; } = true;
            public bool AutoSequenceEnabled { get; private set; } = true;
            public RuntimeAbilitySliceHudManualCommand LastCommand { get; private set; }
            public int Count { get; private set; }

            public void SetAutoSequenceEnabled(bool enabled)
            {
                AutoSequenceEnabled = enabled;
            }

            public RuntimeCommandValidationResult EnqueueHudCommand(RuntimeAbilitySliceHudManualCommand command)
            {
                LastCommand = command;
                Count++;
                return RuntimeCommandValidationResult.Accepted(new RuntimeCommand(
                    RuntimeFrame.Zero,
                    sourceId: 1,
                    commandId: (int)command,
                    targetId: 0,
                    traceId: "fairygui-hud-shell-test"));
            }
        }
    }
}
