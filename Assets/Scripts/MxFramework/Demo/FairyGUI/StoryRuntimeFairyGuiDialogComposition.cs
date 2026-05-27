using System;
using MxFramework.Demo.Story;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Demo.FairyGui
{
    public static class StoryRuntimeFairyGuiDialogComposition
    {
        public static MxUiViewContract CreateContract()
        {
            return StoryRuntimeFairyGuiDialogManifest.CreateViewContract();
        }

        public static MxFairyGuiPackageDescriptor CreatePackageDescriptor()
        {
            return StoryRuntimeFairyGuiDialogManifest.CreatePackageDescriptor();
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

            catalog.Register(new MxFairyGuiRuntimeViewRegistration<StoryRuntimeVerticalSliceFairyGuiViewModel>(
                CreateContract(),
                CreatePackageDescriptor(),
                new StoryRuntimeFairyGuiDialogBinder(commandSink, textProvider)));
        }

        public static MxFairyGuiNavigator CreateNavigator(
            IResourceManager resourceManager,
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider = null,
            IMxFairyGuiHost host = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null)
        {
            if (resourceManager == null)
                throw new ArgumentNullException(nameof(resourceManager));

            return MxFairyGuiRuntimeShellComposition.CreateNavigator(
                resourceManager,
                CreateCatalog(commandSink, textProvider),
                host,
                layerHost,
                inputBridge);
        }

        public static StoryRuntimeFairyGuiDialogShell CreateShell(
            IResourceManager resourceManager,
            IStoryRuntimeVerticalSliceUiCommandTarget commandTarget,
            IMxUiTextProvider textProvider = null,
            IMxFairyGuiHost host = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null)
        {
            var commandSink = new StoryRuntimeVerticalSliceUiCommandSink(commandTarget);
            MxFairyGuiNavigator navigator = CreateNavigator(resourceManager, commandSink, textProvider, host, layerHost, inputBridge);
            return new StoryRuntimeFairyGuiDialogShell(new StoryRuntimeFairyGuiDialogController(navigator), commandSink);
        }
    }
}
