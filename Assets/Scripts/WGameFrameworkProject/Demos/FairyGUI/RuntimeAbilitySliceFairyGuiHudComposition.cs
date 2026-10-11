using System;
using MxFramework.Demo.Story;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Demo.FairyGui
{
    public static class RuntimeAbilitySliceFairyGuiHudComposition
    {
        public static MxUiViewContract CreateContract()
        {
            return RuntimeAbilitySliceFairyGuiHudManifest.CreateViewContract();
        }

        public static MxFairyGuiPackageDescriptor CreatePackageDescriptor()
        {
            return RuntimeAbilitySliceFairyGuiHudManifest.CreatePackageDescriptor();
        }

        public static MxFairyGuiRuntimeCatalog CreateCatalog(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider = null)
        {
            var catalog = new MxFairyGuiRuntimeCatalog();
            Register(catalog, commandSink, textProvider);
            return catalog;
        }

        public static void Register(
            MxFairyGuiRuntimeCatalog catalog,
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider = null)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            catalog.Register(new MxFairyGuiRuntimeViewRegistration<RuntimeAbilitySliceHudViewModel>(
                CreateContract(),
                CreatePackageDescriptor(),
                new RuntimeAbilitySliceFairyGuiHudBinder(commandSink, textProvider)));
        }

        public static MxFairyGuiNavigator CreateNavigator(
            IResourceManager resourceManager,
            IMxUiCommandSink commandSink,
            IMxFairyGuiHost host = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiViewTransitionController transitionController = null,
            IMxUiTextProvider textProvider = null)
        {
            if (resourceManager == null)
                throw new ArgumentNullException(nameof(resourceManager));

            return MxFairyGuiRuntimeShellComposition.CreateNavigator(
                resourceManager,
                CreateCatalog(commandSink, textProvider),
                host,
                layerHost,
                inputBridge,
                transitionController);
        }

        public static RuntimeAbilitySliceFairyGuiHudShell CreateShell(
            IResourceManager resourceManager,
            IRuntimeAbilitySliceHudCommandTarget commandTarget,
            IMxFairyGuiHost host = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiViewTransitionController transitionController = null,
            IMxUiTextProvider textProvider = null)
        {
            var commandSink = new RuntimeAbilitySliceUiCommandSink(commandTarget);
            MxFairyGuiNavigator navigator = CreateNavigator(resourceManager, commandSink, host, layerHost, inputBridge, transitionController, textProvider);
            return new RuntimeAbilitySliceFairyGuiHudShell(new RuntimeAbilitySliceFairyGuiHudController(navigator), commandSink);
        }
    }

    public sealed class MxFairyGuiProductRuntimeShell
    {
        public MxFairyGuiProductRuntimeShell(
            MxFairyGuiNavigator navigator,
            RuntimeAbilitySliceFairyGuiHudShell runtimeHud,
            StoryRuntimeFairyGuiDialogShell storyDialog)
        {
            Navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            RuntimeHud = runtimeHud ?? throw new ArgumentNullException(nameof(runtimeHud));
            StoryDialog = storyDialog ?? throw new ArgumentNullException(nameof(storyDialog));
        }

        public MxFairyGuiNavigator Navigator { get; }
        public RuntimeAbilitySliceFairyGuiHudShell RuntimeHud { get; }
        public StoryRuntimeFairyGuiDialogShell StoryDialog { get; }

        public bool CloseAll()
        {
            bool closed = false;
            closed |= StoryDialog.Close();
            closed |= RuntimeHud.Close();
            return closed;
        }
    }

    public static class MxFairyGuiProductRuntimeComposition
    {
        public static MxFairyGuiRuntimeCatalog CreateCatalog(
            IMxUiCommandSink runtimeHudCommandSink,
            IMxUiCommandSink storyDialogCommandSink,
            IMxUiTextProvider textProvider = null)
        {
            var catalog = new MxFairyGuiRuntimeCatalog();
            Register(catalog, runtimeHudCommandSink, storyDialogCommandSink, textProvider);
            return catalog;
        }

        public static void Register(
            MxFairyGuiRuntimeCatalog catalog,
            IMxUiCommandSink runtimeHudCommandSink,
            IMxUiCommandSink storyDialogCommandSink,
            IMxUiTextProvider textProvider = null)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            RuntimeAbilitySliceFairyGuiHudComposition.Register(catalog, runtimeHudCommandSink, textProvider);
            StoryRuntimeFairyGuiDialogComposition.Register(catalog, storyDialogCommandSink, textProvider);
        }

        public static MxFairyGuiNavigator CreateNavigator(
            IResourceManager resourceManager,
            IMxUiCommandSink runtimeHudCommandSink,
            IMxUiCommandSink storyDialogCommandSink,
            IMxFairyGuiHost host = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiViewTransitionController transitionController = null,
            IMxUiTextProvider textProvider = null)
        {
            if (resourceManager == null)
                throw new ArgumentNullException(nameof(resourceManager));

            return MxFairyGuiRuntimeShellComposition.CreateNavigator(
                resourceManager,
                CreateCatalog(runtimeHudCommandSink, storyDialogCommandSink, textProvider),
                host,
                layerHost,
                inputBridge,
                transitionController);
        }

        public static MxFairyGuiProductRuntimeShell CreateShell(
            IResourceManager resourceManager,
            IRuntimeAbilitySliceHudCommandTarget runtimeHudCommandTarget,
            IStoryRuntimeVerticalSliceUiCommandTarget storyDialogCommandTarget,
            IMxFairyGuiHost host = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiViewTransitionController transitionController = null,
            IMxUiTextProvider textProvider = null)
        {
            var runtimeHudCommandSink = new RuntimeAbilitySliceUiCommandSink(runtimeHudCommandTarget);
            var storyDialogCommandSink = new StoryRuntimeVerticalSliceUiCommandSink(storyDialogCommandTarget);
            MxFairyGuiNavigator navigator = CreateNavigator(
                resourceManager,
                runtimeHudCommandSink,
                storyDialogCommandSink,
                host,
                layerHost,
                inputBridge,
                transitionController,
                textProvider);

            return new MxFairyGuiProductRuntimeShell(
                navigator,
                new RuntimeAbilitySliceFairyGuiHudShell(
                    new RuntimeAbilitySliceFairyGuiHudController(navigator),
                    runtimeHudCommandSink),
                new StoryRuntimeFairyGuiDialogShell(
                    new StoryRuntimeFairyGuiDialogController(navigator),
                    storyDialogCommandSink));
        }
    }
}
