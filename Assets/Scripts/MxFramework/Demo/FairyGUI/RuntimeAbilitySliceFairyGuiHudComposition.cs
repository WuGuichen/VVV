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

        public static MxFairyGuiRuntimeCatalog CreateCatalog(IMxUiCommandSink commandSink)
        {
            var catalog = new MxFairyGuiRuntimeCatalog();
            Register(catalog, commandSink);
            return catalog;
        }

        public static void Register(MxFairyGuiRuntimeCatalog catalog, IMxUiCommandSink commandSink)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            catalog.Register(new MxFairyGuiRuntimeViewRegistration<RuntimeAbilitySliceHudViewModel>(
                CreateContract(),
                CreatePackageDescriptor(),
                new RuntimeAbilitySliceFairyGuiHudBinder(commandSink)));
        }

        public static MxFairyGuiNavigator CreateNavigator(
            IResourceManager resourceManager,
            IMxUiCommandSink commandSink,
            IMxFairyGuiHost host = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null)
        {
            if (resourceManager == null)
                throw new ArgumentNullException(nameof(resourceManager));

            return MxFairyGuiRuntimeShellComposition.CreateNavigator(
                resourceManager,
                CreateCatalog(commandSink),
                host,
                layerHost,
                inputBridge);
        }

        public static RuntimeAbilitySliceFairyGuiHudShell CreateShell(
            IResourceManager resourceManager,
            IRuntimeAbilitySliceHudCommandTarget commandTarget,
            IMxFairyGuiHost host = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null)
        {
            var commandSink = new RuntimeAbilitySliceUiCommandSink(commandTarget);
            MxFairyGuiNavigator navigator = CreateNavigator(resourceManager, commandSink, host, layerHost, inputBridge);
            return new RuntimeAbilitySliceFairyGuiHudShell(new RuntimeAbilitySliceFairyGuiHudController(navigator), commandSink);
        }
    }
}
