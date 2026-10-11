using MxFramework.Config;
using MxFramework.UI;
using MxFramework.UI.ConfigBridge;
using NUnit.Framework;

namespace MxFramework.Tests.UI
{
    public sealed class MxConfigUiTextProviderTests
    {
        [Test]
        public void TryGetText_MapsUiKeyAndLocaleToConfigProvider()
        {
            var localization = new MemoryLocalizationProvider();
            localization.Register(new LocalizedTextKey("ui.title"), LocaleId.ZhCN, "标题");
            var provider = new MxConfigUiTextProvider(localization, new MxUiLocaleId("zh-CN"));

            bool found = provider.TryGetText(
                new MxUiLocalizedTextRequest(new MxUiTextKey("ui.title"), "Fallback"),
                out string text);

            Assert.IsTrue(found);
            Assert.AreEqual("标题", text);
            Assert.AreEqual(new MxUiLocaleId("zh-CN"), provider.CurrentLocale);
        }

        [Test]
        public void TryGetText_WhenMissing_UsesFallback()
        {
            var provider = new MxConfigUiTextProvider(new MemoryLocalizationProvider(), new MxUiLocaleId("en-US"));

            bool found = provider.TryGetText(
                new MxUiLocalizedTextRequest(new MxUiTextKey("missing"), "Fallback"),
                out string text);

            Assert.IsTrue(found);
            Assert.AreEqual("Fallback", text);
        }

        [Test]
        public void SetLocale_IncrementsRevisionOnlyWhenLocaleChanges()
        {
            var provider = new MxConfigUiTextProvider(new MemoryLocalizationProvider(), new MxUiLocaleId("zh-CN"));

            provider.SetLocale(new MxUiLocaleId("zh-CN"));
            Assert.AreEqual(0L, provider.Revision);

            provider.SetLocale(new MxUiLocaleId("en-US"));
            Assert.AreEqual(1L, provider.Revision);
            Assert.AreEqual(new MxUiLocaleId("en-US"), provider.CurrentLocale);
        }
    }
}
