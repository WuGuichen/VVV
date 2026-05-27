using System;
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
}
