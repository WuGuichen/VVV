using System;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Demo.FairyGui
{
    public static class UiFoundationDemoComposition
    {
        public static MxFairyGuiRuntimeCatalog CreateCatalog(IMxUiCommandSink commandSink)
        {
            var catalog = new MxFairyGuiRuntimeCatalog();
            MxUiViewContract contract = UiFoundationDemoFairyGuiManifest.CreateViewContract();
            contract.Descriptor.Modal = true;
            contract.Descriptor.CloseOnSceneChange = true;
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<UiFoundationDemoViewModel>(
                contract,
                UiFoundationDemoFairyGuiManifest.CreatePackageDescriptor(),
                new UiFoundationDemoFairyGuiBinder(commandSink)));
            return catalog;
        }

        public static MxFairyGuiNavigator CreateNavigator(
            IResourceManager resourceManager,
            IMxUiCommandSink commandSink,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null)
        {
            if (resourceManager == null)
                throw new ArgumentNullException(nameof(resourceManager));

            return MxFairyGuiRuntimeShellComposition.CreateNavigator(
                resourceManager,
                CreateCatalog(commandSink),
                layerHost: layerHost,
                inputBridge: inputBridge);
        }
    }
}
