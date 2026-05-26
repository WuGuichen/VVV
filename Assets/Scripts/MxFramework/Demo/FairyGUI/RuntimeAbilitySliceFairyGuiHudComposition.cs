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

        public static MxFairyGuiNavigator CreateNavigator(
            IResourceManager resourceManager,
            IMxUiCommandSink commandSink,
            IMxFairyGuiHost host = null)
        {
            if (resourceManager == null)
                throw new ArgumentNullException(nameof(resourceManager));

            var contracts = new MxUiViewContractRegistry();
            contracts.Register(CreateContract());

            var packages = new MxFairyGuiPackageCatalog();
            packages.Register(CreatePackageDescriptor());

            var bindings = new MxFairyGuiViewBindingRegistry();
            bindings.Register(RuntimeAbilitySliceFairyGuiHudIds.ViewId, new RuntimeAbilitySliceFairyGuiHudBinder(commandSink));

            return new MxFairyGuiNavigator(
                contracts,
                packages,
                new MxFairyGuiResourceBridge(resourceManager),
                host ?? new MxFairyGuiHost(),
                bindings);
        }
    }
}
