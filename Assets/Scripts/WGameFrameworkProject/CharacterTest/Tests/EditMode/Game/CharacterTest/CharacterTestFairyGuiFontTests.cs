using MxFramework.Game.CharacterTest;
using System.Collections;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiFontTests
    {
        public static IEnumerable LocaleCases()
        {
            var catalog = JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "Resources/Fonts/font-locales.json")));
            foreach (var row in catalog["locales"])
                yield return new TestCaseData((string)row["locale"], (string)row["steam"], (string)row["region"], (string)row["title"] + " " + (string)row["sample"]);
        }

        [TestCaseSource(nameof(LocaleCases))]
        public void RegionalFacesAndFallbacksRenderEachSupportedLocale(string locale, string steam, string region, string sample)
        {
            string previous = CharacterTestFairyGuiFonts.CurrentLocale;
            try
            {
                CharacterTestFairyGuiFonts.SetLocale(locale);
                Assert.That(CharacterTestFairyGuiFonts.RegionForLocale(steam), Is.EqualTo(region));
                Assert.That(CharacterTestFairyGuiFonts.Control, Is.EqualTo(CharacterTestFairyGuiFonts.Body));
                foreach (string role in new[] { CharacterTestFairyGuiFonts.Body, CharacterTestFairyGuiFonts.Heading })
                {
                    var face = (Fgui.DynamicFont)Fgui.FontManager.GetFont(role);
                    string expected = role == CharacterTestFairyGuiFonts.Heading ? "MxUiSerif" + region : "MxUiSans" + region;
                    Assert.That(face.nativeFont, Is.SameAs(UnityEngine.Resources.Load<Font>("Fonts/" + expected)));
                    face.nativeFont.RequestCharactersInTexture(sample, 24, FontStyle.Normal);
                    foreach (char c in sample)
                    {
                        if (char.IsWhiteSpace(c)) continue;
                        Assert.That(face.nativeFont.GetCharacterInfo(c, out var glyph, 24, FontStyle.Normal), Is.True, locale + "/" + role + " lacks U+" + ((int)c).ToString("X4"));
                    }
                }
            }
            finally { CharacterTestFairyGuiFonts.SetLocale(previous); }
        }

        [Test]
        public void LocaleChangeRetainsLiveFontHandlesAndInvalidatesText()
        {
            string previous = CharacterTestFairyGuiFonts.CurrentLocale;
            var runtime = CharacterTestLocalizationRuntime.Create(null);
            runtime.LocaleChanged += CharacterTestFairyGuiFonts.SetLocale;
            try
            {
                CharacterTestFairyGuiFonts.Initialize();
                var face = (Fgui.DynamicFont)Fgui.FontManager.GetFont(CharacterTestFairyGuiFonts.Body);
                int version = face.version;
                runtime.SetWorkingLocale(null, "ja-JP");
                Assert.That(Fgui.FontManager.GetFont(CharacterTestFairyGuiFonts.Body), Is.SameAs(face));
                Assert.That(face.version, Is.GreaterThan(version), "Already-open text must rebuild when the font changes.");
                Assert.That(face.nativeFont, Is.SameAs(UnityEngine.Resources.Load<Font>("Fonts/MxUiSansJP")));
                version = face.version;
                runtime.SetWorkingLocale(null, "ja-JP");
                Assert.That(face.version, Is.EqualTo(version), "Unchanged locale must not rebuild text.");
                runtime.SetWorkingLocale(null, "zh-CN");
                Assert.That(face.nativeFont, Is.SameAs(UnityEngine.Resources.Load<Font>("Fonts/MxUiSansSC")));
            }
            finally { runtime.LocaleChanged -= CharacterTestFairyGuiFonts.SetLocale; CharacterTestFairyGuiFonts.SetLocale(previous); }
        }

        [Test]
        public void RuntimeFontSetStaysWithinTwentyMiB()
        {
            var paths = System.IO.Directory.GetFiles(System.IO.Path.Combine(Application.dataPath, "Resources/Fonts"), "*.otf");
            Assert.That(paths.Length, Is.EqualTo(10), "Remove retired full fonts from Resources, not just from references.");
            long bytes = 0;
            foreach (var path in paths) bytes += new System.IO.FileInfo(path).Length;
            Assert.That(bytes, Is.LessThanOrEqualTo(20L * 1024 * 1024));
        }

        [TestCase(CharacterTestFairyGuiFonts.Body, "MxUiSansSC")]
        [TestCase(CharacterTestFairyGuiFonts.Heading, "MxUiSerifSC")]
        public void BundledFaceResolvesAndContainsCardText(string alias, string resource)
        {
            string previous = Fgui.UIConfig.defaultFont;
            try
            {
                CharacterTestFairyGuiFonts.Initialize();
                var font = Fgui.FontManager.GetFont(alias) as Fgui.DynamicFont;
                Assert.That(font, Is.Not.Null);
                Assert.That(font.nativeFont, Is.SameAs(UnityEngine.Resources.Load<Font>("Fonts/" + resource)));
                foreach (char c in "守距出手聚势预览费用0123456789—…")
                    Assert.That(font.nativeFont.HasCharacter(c), Is.True, alias + " lacks " + c);
                CharacterTestFairyGuiFonts.Initialize();
                Assert.That(Fgui.FontManager.GetFont(alias), Is.SameAs(font), "Repeated setup must reuse the registered face.");
            }
            finally { Fgui.UIConfig.defaultFont = previous; }
        }
    }
}
