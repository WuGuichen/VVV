using System;
using MxFramework.Demo.Story;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Demo.FairyGui
{
    public delegate bool MxUiTextResolver(MxUiTextKey key, MxUiLocaleId locale, out string text);

    public sealed class MxDelegateUiTextProvider : IMxUiTextProvider
    {
        private readonly MxUiTextResolver _resolver;
        private MxUiLocaleId _locale;
        private long _revision;

        public MxDelegateUiTextProvider(
            MxUiTextResolver resolver,
            MxUiLocaleId locale,
            long revision = 1L)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _locale = locale;
            _revision = revision;
        }

        public MxUiLocaleId CurrentLocale => _locale;
        public long Revision => _revision;

        public void SetLocale(MxUiLocaleId locale)
        {
            if (_locale.Equals(locale))
                return;

            _locale = locale;
            _revision++;
        }

        public void Refresh()
        {
            _revision++;
        }

        public bool TryGetText(MxUiLocalizedTextRequest request, out string text)
        {
            text = string.Empty;
            return request.IsValid && _resolver(request.Key, _locale, out text);
        }
    }

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

        public static StoryRuntimeFairyGuiDialogShell CreateShell(
            IResourceManager resourceManager,
            IStoryRuntimeVerticalSliceUiCommandTarget commandTarget,
            IMxFairyGuiHost host = null,
            IMxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiViewTransitionController transitionController = null,
            IMxUiTextProvider textProvider = null)
        {
            var commandSink = new StoryRuntimeVerticalSliceUiCommandSink(commandTarget);
            MxFairyGuiNavigator navigator = CreateNavigator(resourceManager, commandSink, host, layerHost, inputBridge, transitionController, textProvider);
            return new StoryRuntimeFairyGuiDialogShell(new StoryRuntimeFairyGuiDialogController(navigator), commandSink);
        }
    }
}
