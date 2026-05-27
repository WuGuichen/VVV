using System.Collections.Generic;
using System.IO;
using MxFramework.Demo;
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
        public void Bind_CompatibilityConstructor_UsesSuppliedViewId()
        {
            Fgui.GComponent root = CreateHudRoot();
            var sink = new RecordingCommandSink();
            var customViewId = new MxUiViewId("ui.runtimehud.compat");
            using (var handle = new MxFairyGuiComponentHandle(
                RuntimeAbilitySliceFairyGuiHudIds.PackageId,
                RuntimeAbilitySliceFairyGuiHudIds.ComponentName,
                root))
            {
                var binder = new RuntimeAbilitySliceFairyGuiHudBinder(sink, customViewId);
                binder.Bind(handle, CreateViewModel());

                Button(root, RuntimeAbilitySliceFairyGuiHudIds.Strike).onClick.Call();

                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(customViewId, sink.Last.SourceViewId);
            }
        }

        [Test]
        public void Bind_ConfiguresFocusNavigationForFrameworkInputBridge()
        {
            Fgui.GComponent root = CreateHudRoot();
            var sink = new RecordingCommandSink();
            using (var handle = new MxFairyGuiComponentHandle(
                RuntimeAbilitySliceFairyGuiHudIds.PackageId,
                RuntimeAbilitySliceFairyGuiHudIds.ComponentName,
                root))
            {
                RuntimeAbilitySliceHudViewModel viewModel = CreateViewModel();
                viewModel.Commands = new[]
                {
                    new RuntimeAbilitySliceHudCommandDescriptor(RuntimeAbilitySliceHudCommandIds.Strike, "Strike", true),
                    new RuntimeAbilitySliceHudCommandDescriptor(RuntimeAbilitySliceHudCommandIds.Reset, "Reset", true)
                };

                var binder = new RuntimeAbilitySliceFairyGuiHudBinder(sink);
                binder.Bind(handle, viewModel);

                Assert.IsTrue(Button(root, RuntimeAbilitySliceFairyGuiHudIds.Strike).focusable);
                Assert.IsTrue(Button(root, RuntimeAbilitySliceFairyGuiHudIds.Reset).focusable);
                Assert.IsTrue(Button(root, RuntimeAbilitySliceFairyGuiHudIds.Strike).focused);

                Assert.IsTrue(MxFairyGuiFocusNavigation.MoveNext(root));
                Assert.IsTrue(Button(root, RuntimeAbilitySliceFairyGuiHudIds.Reset).focused);
                Assert.IsTrue(MxFairyGuiFocusNavigation.MovePrevious(root));
                Assert.IsTrue(Button(root, RuntimeAbilitySliceFairyGuiHudIds.Strike).focused);

                Assert.IsTrue(MxFairyGuiFocusNavigation.Submit(root));
                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(RuntimeAbilitySliceHudCommandIds.Strike, sink.Last.CommandId);
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
            MxFairyGuiNavigator compatibilityNavigator = RuntimeAbilitySliceFairyGuiHudComposition.CreateNavigator(
                manager,
                sink,
                null);
            Assert.IsNotNull(compatibilityNavigator);
            RuntimeAbilitySliceFairyGuiHudShell compatibilityShell = RuntimeAbilitySliceFairyGuiHudComposition.CreateShell(
                manager,
                new RecordingHudCommandTarget(),
                null);
            Assert.IsNotNull(compatibilityShell);

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
        public void Shell_Refresh_RebindsRuntimeHudWithInjectedLocalizationProvider()
        {
            RemoveRuntimeHudPackageIfLoaded();

            ResourceManager manager = CreatePublishedRuntimeHudManager();
            var target = new RecordingHudCommandTarget();
            Dictionary<string, string> localization = CreateLocalization(
                ("en-US", "ui.runtimehud.title", "Ability HUD"),
                ("en-US", "ui.runtimehud.mode", "Config Driven"),
                ("zh-CN", "ui.runtimehud.title", "能力 HUD"),
                ("zh-CN", "ui.runtimehud.mode", "配置驱动"));
            var textProvider = new MxDelegateUiTextProvider(
                (MxUiTextKey key, MxUiLocaleId locale, out string text) => localization.TryGetValue(locale.Value + "|" + key.Value, out text),
                new MxUiLocaleId("en-US"));
            RuntimeAbilitySliceFairyGuiHudShell shell = RuntimeAbilitySliceFairyGuiHudComposition.CreateShell(
                manager,
                target,
                textProvider: textProvider);

            try
            {
                MxUiOpenResult firstResult = shell.Open(CreateViewModel());

                Assert.IsTrue(firstResult.Success, firstResult.Message);
                var view = firstResult.View as MxFairyGuiView<RuntimeAbilitySliceHudViewModel>;
                Assert.IsNotNull(view);
                var handle = view.Component as MxFairyGuiComponentHandle;
                Assert.IsNotNull(handle);
                Fgui.GComponent root = handle.Component;
                Assert.AreEqual("Ability HUD", Text(root, RuntimeAbilitySliceFairyGuiHudIds.Title));
                Assert.AreEqual("Config Driven", Text(root, RuntimeAbilitySliceFairyGuiHudIds.Mode));

                textProvider.SetLocale(new MxUiLocaleId("zh-CN"));
                MxUiOpenResult refreshResult = shell.Refresh(CreateViewModel());

                Assert.IsTrue(refreshResult.Success, refreshResult.Message);
                Assert.AreSame(firstResult.View, refreshResult.View);
                Assert.AreEqual("能力 HUD", Text(root, RuntimeAbilitySliceFairyGuiHudIds.Title));
                Assert.AreEqual("配置驱动", Text(root, RuntimeAbilitySliceFairyGuiHudIds.Mode));
                Assert.AreEqual(new MxUiLocaleId("zh-CN"), textProvider.CurrentLocale);
                Assert.Greater(textProvider.Revision, 1L);
            }
            finally
            {
                if (shell.IsOpen)
                    shell.Close();

                RemoveRuntimeHudPackageIfLoaded();
            }
        }

        [Test]
        public void ProductRuntimeShell_OpenRefreshCommandAndClose_CoversRuntimeHudAndStoryDialog()
        {
            RemoveRuntimeHudPackageIfLoaded();
            RemoveStoryDialogPackageIfLoaded();

            ResourceManager manager = CreatePublishedProductRuntimeManager();
            var hudTarget = new RecordingHudCommandTarget();
            var storyTarget = new RecordingStoryCommandTarget(new RuntimeFrame(12));
            MxFairyGuiProductRuntimeShell shell =
                MxFairyGuiProductRuntimeComposition.CreateShell(manager, hudTarget, storyTarget);
            MxFairyGuiRuntimeCatalog catalog = MxFairyGuiProductRuntimeComposition.CreateCatalog(
                new RecordingCommandSink(),
                new RecordingCommandSink());

            try
            {
                MxFairyGuiRuntimeCatalogDiagnostics diagnostics = catalog.CreateDiagnostics(manager);
                Assert.IsTrue(diagnostics.Success);
                Assert.AreEqual(2, diagnostics.ViewCount);
                Assert.AreEqual(2, diagnostics.PackageCount);
                Assert.AreEqual(2, diagnostics.ResourceKeyCount);

                ResourcePreloadPlan preloadPlan = catalog.CreatePreloadPlan("fairygui.product.shell.smoke");
                Assert.AreEqual("fairygui.product.shell.smoke", preloadPlan.GroupId);
                Assert.AreEqual(2, preloadPlan.ExplicitKeys.Count);

                MxUiOpenResult hudOpen = shell.RuntimeHud.Open(CreateViewModel());
                Assert.IsTrue(hudOpen.Success, hudOpen.Message);
                Assert.IsTrue(shell.RuntimeHud.IsOpen);

                var hudView = hudOpen.View as MxFairyGuiView<RuntimeAbilitySliceHudViewModel>;
                Assert.IsNotNull(hudView);
                var hudHandle = hudView.Component as MxFairyGuiComponentHandle;
                Assert.IsNotNull(hudHandle);
                Fgui.GComponent hudRoot = hudHandle.Component;
                Assert.AreEqual("Ability HUD", Text(hudRoot, RuntimeAbilitySliceFairyGuiHudIds.Title));

                RuntimeAbilitySliceHudViewModel refreshedHud = CreateViewModel();
                refreshedHud.Title = "Ability HUD Product Smoke";
                MxUiOpenResult hudRefresh = shell.RuntimeHud.Refresh(refreshedHud);
                Assert.IsTrue(hudRefresh.Success, hudRefresh.Message);
                Assert.AreSame(hudOpen.View, hudRefresh.View);
                Assert.AreEqual("Ability HUD Product Smoke", Text(hudRoot, RuntimeAbilitySliceFairyGuiHudIds.Title));

                Button(hudRoot, RuntimeAbilitySliceFairyGuiHudIds.Strike).onClick.Call();
                Assert.AreEqual(RuntimeAbilitySliceHudCommandIds.Strike, shell.RuntimeHud.LastCommand.CommandId);
                Assert.AreEqual(RuntimeAbilitySliceHudManualCommand.Strike, hudTarget.LastCommand);
                Assert.AreEqual(1, shell.RuntimeHud.AcceptedCommandCount);

                MxUiOpenResult storyOpen = shell.StoryDialog.Open(CreateStoryWaitingViewModel());
                Assert.IsTrue(storyOpen.Success, storyOpen.Message);
                Assert.IsTrue(shell.StoryDialog.IsOpen);

                var storyView = storyOpen.View as MxFairyGuiView<StoryRuntimeVerticalSliceFairyGuiViewModel>;
                Assert.IsNotNull(storyView);
                var storyHandle = storyView.Component as MxFairyGuiComponentHandle;
                Assert.IsNotNull(storyHandle);
                Fgui.GComponent storyRoot = storyHandle.Component;
                Assert.AreEqual("Presentation waiting", DialogText(storyRoot, StoryRuntimeFairyGuiDialogIds.Phase));

                Button(storyRoot, StoryRuntimeFairyGuiDialogIds.Continue).onClick.Call();
                Assert.AreEqual(StoryRuntimeCommandIds.CompletePresentation, storyTarget.Last.CommandId);
                Assert.AreEqual(1, shell.StoryDialog.AcceptedCommandCount);

                MxUiOpenResult storyRefresh = shell.StoryDialog.Refresh(CreateStoryChoiceViewModel());
                Assert.IsTrue(storyRefresh.Success, storyRefresh.Message);
                Assert.AreSame(storyOpen.View, storyRefresh.View);
                Assert.AreEqual("Choice available", DialogText(storyRoot, StoryRuntimeFairyGuiDialogIds.Phase));

                Button(storyRoot, StoryRuntimeFairyGuiDialogIds.Choice).onClick.Call();
                Assert.AreEqual(StoryRuntimeCommandIds.SelectChoice, storyTarget.Last.CommandId);
                Assert.AreEqual(2, shell.StoryDialog.AcceptedCommandCount);

                Assert.AreEqual(2, manager.CreateDebugSnapshot().LoadedCount);
                Assert.IsTrue(shell.CloseAll());
                Assert.IsFalse(shell.RuntimeHud.IsOpen);
                Assert.IsFalse(shell.StoryDialog.IsOpen);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
                Assert.IsNull(Fgui.UIPackage.GetByName(RuntimeAbilitySliceFairyGuiHudIds.PackageId));
                Assert.IsNull(Fgui.UIPackage.GetByName(StoryRuntimeFairyGuiDialogIds.PackageId));
            }
            finally
            {
                shell.CloseAll();
                RemoveRuntimeHudPackageIfLoaded();
                RemoveStoryDialogPackageIfLoaded();
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

        [Test]
        public void FocusNavigation_MovePreviousWithoutCurrentFocus_StartsFromLastEntry()
        {
            Fgui.GComponent root = CreateHudRoot();
            MxFairyGuiFocusNavigation.Configure(
                root,
                new MxFairyGuiFocusNavigationMetadata(
                    RuntimeAbilitySliceFairyGuiHudIds.ViewId,
                    RuntimeAbilitySliceFairyGuiHudIds.Strike,
                    new[] { RuntimeAbilitySliceFairyGuiHudIds.Strike, RuntimeAbilitySliceFairyGuiHudIds.Reset }));

            Assert.IsFalse(Button(root, RuntimeAbilitySliceFairyGuiHudIds.Strike).focused);
            Assert.IsFalse(Button(root, RuntimeAbilitySliceFairyGuiHudIds.Reset).focused);

            Assert.IsTrue(MxFairyGuiFocusNavigation.MovePrevious(root));
            Assert.IsTrue(Button(root, RuntimeAbilitySliceFairyGuiHudIds.Reset).focused);
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

        private static StoryRuntimeVerticalSliceFairyGuiViewModel CreateStoryWaitingViewModel()
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

        private static StoryRuntimeVerticalSliceFairyGuiViewModel CreateStoryChoiceViewModel()
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

        private static string DialogText(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asTextField.text;
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

        private static ResourceManager CreatePublishedProductRuntimeManager()
        {
            byte[] runtimeHudPackageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxRuntimeHud/MxRuntimeHud_fui.bytes"));
            byte[] storyDialogPackageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxStoryDialog/MxStoryDialog_fui.bytes"));

            return CreateManager(
                new MemoryResourceProvider()
                    .Register("fgui/MxRuntimeHud_fui.bytes", runtimeHudPackageBytes)
                    .Register("fgui/MxStoryDialog_fui.bytes", storyDialogPackageBytes),
                Entry(RuntimeAbilitySliceFairyGuiHudIds.PackageBytesKey, "fgui/MxRuntimeHud_fui.bytes"),
                Entry(StoryRuntimeFairyGuiDialogIds.PackageBytesKey, "fgui/MxStoryDialog_fui.bytes"));
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

        private static Dictionary<string, string> CreateLocalization(params (string Locale, string Key, string Text)[] entries)
        {
            var table = new Dictionary<string, string>();
            for (int i = 0; i < entries.Length; i++)
                table[entries[i].Locale + "|" + entries[i].Key] = entries[i].Text;

            return table;
        }

        private static void RemoveRuntimeHudPackageIfLoaded()
        {
            if (Fgui.UIPackage.GetByName(RuntimeAbilitySliceFairyGuiHudIds.PackageId) != null)
                Fgui.UIPackage.RemovePackage(RuntimeAbilitySliceFairyGuiHudIds.PackageId);
        }

        private static void RemoveStoryDialogPackageIfLoaded()
        {
            if (Fgui.UIPackage.GetByName(StoryRuntimeFairyGuiDialogIds.PackageId) != null)
                Fgui.UIPackage.RemovePackage(StoryRuntimeFairyGuiDialogIds.PackageId);
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
