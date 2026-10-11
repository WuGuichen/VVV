using System.Linq;
using MxFramework.Config;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public sealed class LocalizationTsvLoaderTests
    {
        [Test]
        public void LoadFromText_RegistersLocalesAndTexts()
        {
            LocalizationTsvLoadResult result = LocalizationTsvLoader.LoadFromText(
                "key\tzh-CN\ten-US\n" +
                "ui.title\t标题\tTitle\n");

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(2, result.Locales.Count);
            Assert.AreEqual(1, result.Keys.Count);
            Assert.IsTrue(result.Provider.TryGetText(
                new LocalizedTextKey("ui.title"),
                LocaleId.ZhCN,
                out string zh));
            Assert.AreEqual("标题", zh);
            Assert.IsTrue(result.Provider.TryGetText(
                new LocalizedTextKey("ui.title"),
                LocaleId.EnUS,
                out string en));
            Assert.AreEqual("Title", en);
        }

        [Test]
        public void LoadFromText_WhenHeaderMissing_ReportsError()
        {
            LocalizationTsvLoadResult result = LocalizationTsvLoader.LoadFromText("id\tzh-CN\nui.title\t标题");

            Assert.IsTrue(result.HasErrors);
            Assert.AreEqual(LocalizationTsvIssueCode.InvalidHeader, result.Issues[0].Code);
        }

        [Test]
        public void LoadFromText_WhenKeyMissing_ReportsErrorAndSkipsRow()
        {
            LocalizationTsvLoadResult result = LocalizationTsvLoader.LoadFromText("key\tzh-CN\n\t标题");

            Assert.IsTrue(result.HasErrors);
            Assert.AreEqual(LocalizationTsvIssueCode.MissingKey, result.Issues[0].Code);
            Assert.AreEqual(0, result.Keys.Count);
        }

        [Test]
        public void LoadFromText_WhenTextMissing_ReportsWarning()
        {
            LocalizationTsvLoadResult result = LocalizationTsvLoader.LoadFromText("key\tzh-CN\ten-US\nui.title\t标题\t");

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(LocalizationTsvIssueCode.MissingText, result.Issues[0].Code);
            Assert.AreEqual(LocalizationTsvIssueSeverity.Warning, result.Issues[0].Severity);
        }

        [Test]
        public void LoadFromText_WhenDuplicateKeyLocale_LaterValueWinsAndWarns()
        {
            LocalizationTsvLoadResult result = LocalizationTsvLoader.LoadFromText(
                "key\tzh-CN\n" +
                "ui.title\t旧\n" +
                "ui.title\t新\n");

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(1, result.Issues.Count(i => i.Code == LocalizationTsvIssueCode.DuplicateText));
            Assert.IsTrue(result.Provider.TryGetText(new LocalizedTextKey("ui.title"), LocaleId.ZhCN, out string text));
            Assert.AreEqual("新", text);
        }

        [Test]
        public void LoadFromText_WhenLocaleHeaderInvalidInMiddle_DoesNotShiftLaterLocaleText()
        {
            LocalizationTsvLoadResult result = LocalizationTsvLoader.LoadFromText(
                "key\tzh-CN\t\ten-US\n" +
                "ui.title\t标题\tinvalid-column\tTitle\n");

            Assert.IsTrue(result.HasErrors);
            Assert.AreEqual(1, result.Issues.Count(i => i.Code == LocalizationTsvIssueCode.InvalidHeader));
            Assert.IsTrue(result.Provider.TryGetText(new LocalizedTextKey("ui.title"), LocaleId.ZhCN, out string zh));
            Assert.AreEqual("标题", zh);
            Assert.IsTrue(result.Provider.TryGetText(new LocalizedTextKey("ui.title"), LocaleId.EnUS, out string en));
            Assert.AreEqual("Title", en);
        }
    }
}
