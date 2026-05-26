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

            var contracts = new MxUiViewContractRegistry();
            contracts.Register(CreateContract());

            var packages = new MxFairyGuiPackageCatalog();
            packages.Register(CreatePackageDescriptor());

            var bindings = new MxFairyGuiViewBindingRegistry();
            bindings.Register(
                StoryRuntimeFairyGuiDialogIds.ViewId,
                new StoryRuntimeFairyGuiDialogBinder(commandSink, textProvider));

            return new MxFairyGuiNavigator(
                contracts,
                packages,
                new MxFairyGuiResourceBridge(resourceManager),
                host ?? new MxFairyGuiHost(),
                bindings,
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
