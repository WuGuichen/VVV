using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class CharacterTestActionBrowserCatalogTests
    {
        private string _root;
        private const string Source = "CharacterActionSource/Libraries/Common/character_actions.json";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "MxActionBrowserTests", Guid.NewGuid().ToString("N"));
            Write(Source, "{schemaVersion:'0.3.0',actions:[{actionId:1,displayName:'Same',category:'BasicAttack',durationFrames:20,tags:['sword']},{actionId:2,displayName:'Same',category:'Dodge',durationFrames:30,tags:[]}]}");
            Write(CharacterTestActionBrowserCatalog.ClassificationPath, "{schema:'mx.character-action.classification.v1',weaponTypes:[{id:'sword',label:'剑'},{id:'blade',label:'刀'}],entries:[{actionId:1,weaponTypes:['sword','blade'],evidenceTag:'sword'}]}");
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [Test]
        public void AuthorSourcesExcludeGeneratedCopiesAndDiscoverNewPackages()
        {
            Write("Assets/MxFrameworkGenerated/CharacterActions/Common/character_actions.json", File.ReadAllText(Path.Combine(_root, Source)));
            var before = CharacterTestActionBrowserCatalog.Build(_root);
            Assert.That(before.Actions.Length, Is.EqualTo(2));
            Write("Authoring/CharacterPackages/character-000003/config/character_actions.json", "{schemaVersion:'0.3.0',actions:[{actionId:3,displayName:'Role',category:'Skill',durationFrames:40}]}");
            var after = CharacterTestActionBrowserCatalog.Build(_root);
            Assert.That(after.Actions.Length, Is.EqualTo(3));
            Assert.That(after.Issues, Is.Empty);
            Assert.That(after.Actions.Single(a => a.ActionId == 3).LibraryId, Is.EqualTo("000003"));
        }

        [Test]
        public void FiltersIntersectAndKeepSameNameEntriesDistinct()
        {
            var data = CharacterTestActionBrowserCatalog.Build(_root);
            Assert.That(CharacterTestActionBrowserCatalog.Filter(data, "same", "", "").Length, Is.EqualTo(2));
            Assert.That(CharacterTestActionBrowserCatalog.Filter(data, "", "sword", "BasicAttack").Single().ActionId, Is.EqualTo(1));
            Assert.That(CharacterTestActionBrowserCatalog.Filter(data, "", "blade", "BasicAttack").Single().ActionId, Is.EqualTo(1));
            Assert.That(CharacterTestActionBrowserCatalog.Filter(data, "", "sword", "Dodge"), Is.Empty);
            Assert.That(CharacterTestActionBrowserCatalog.Filter(data, "2", "unclassified", "Dodge").Single().ActionId, Is.EqualTo(2));
            Assert.That(CharacterTestActionBrowserCatalog.Filter(data, "no-such-action", "", ""), Is.Empty);
        }

        [Test]
        public void DuplicateIdsAreExplicitErrors()
        {
            Write("Authoring/CharacterPackages/character-000003/config/character_actions.json", File.ReadAllText(Path.Combine(_root, Source)));
            var data = CharacterTestActionBrowserCatalog.Build(_root);
            Assert.That(data.Issues.Any(i => i.Contains("重复动作 ID")), Is.True);
            Assert.That(data.Actions.All(a => a.Issues.Length > 0), Is.True);
        }

        [TestCase("{actionId:99,weaponTypes:['sword'],evidenceTag:'sword'}", "动作不存在")]
        [TestCase("{actionId:1,weaponTypes:['missing'],evidenceTag:'sword'}", "未知或重复")]
        [TestCase("{actionId:1,weaponTypes:['sword'],evidenceTag:'missing'}", "依据的作者标签已失效")]
        public void InvalidClassificationReferencesProduceDiagnostics(string entry, string diagnostic)
        {
            var json = JObject.Parse(File.ReadAllText(Path.Combine(_root, CharacterTestActionBrowserCatalog.ClassificationPath)));
            json["entries"] = new JArray(JObject.Parse(entry));
            Write(CharacterTestActionBrowserCatalog.ClassificationPath, json.ToString());
            Assert.That(CharacterTestActionBrowserCatalog.Build(_root).Issues.Any(i => i.Contains(diagnostic)), Is.True);
        }

        [Test]
        public void ClassificationDoesNotChangeExecutionContent()
        {
            var original = File.ReadAllBytes(Path.Combine(_root, Source));
            var before = CharacterTestActionBrowserCatalog.Build(_root);
            var classification = JObject.Parse(File.ReadAllText(Path.Combine(_root, CharacterTestActionBrowserCatalog.ClassificationPath)));
            classification["entries"] = new JArray();
            Write(CharacterTestActionBrowserCatalog.ClassificationPath, classification.ToString());
            var after = CharacterTestActionBrowserCatalog.Build(_root);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(Path.Combine(_root, Source)));
            CollectionAssert.AreEqual(before.Actions.Select(a => a.ContentHash), after.Actions.Select(a => a.ContentHash));
            Assert.That(before.Fingerprint, Is.Not.EqualTo(after.Fingerprint));
            Assert.That(after.Actions.All(a => a.WeaponTypes.Length == 0), Is.True);
        }

        [TestCase(301)]
        [TestCase(950003)]
        public void NumericBindingDoesNotRequireOwnedCatalog(int id)
        {
            Assert.That(CharacterTestRuntimeAiPlannerDevSceneController.ResolveAuthoringActionId(
                CharacterTestRuntimeAiPlannerDevSceneController.AuthoringActionKey(id)), Is.EqualTo(id));
            Assert.That(CharacterTestRuntimeAiPlannerDevSceneController.ResolveAuthoringActionId("action:-1"), Is.Zero);
        }

        [Test]
        public void ManualClassificationSupportsMultipleWeaponsAndClearingWithoutChangingActions()
        {
            byte[] original = File.ReadAllBytes(Path.Combine(_root, Source));
            var before = CharacterTestActionBrowserCatalog.Build(_root);
            Assert.That(CharacterTestActionClassificationEditor.TrySaveAt(_root, 2, new[] { "sword", "blade" },
                before.Fingerprint, out bool changed, out string issue), Is.True, issue);
            Assert.That(changed, Is.True);
            var after = CharacterTestActionBrowserCatalog.Build(_root);
            Assert.That(after.Issues, Is.Empty);
            CollectionAssert.AreEquivalent(new[] { "sword", "blade" }, after.Actions.Single(a => a.ActionId == 2).WeaponTypes);
            CollectionAssert.AreEqual(before.Actions.Single(a => a.ActionId == 1).WeaponTypes, after.Actions.Single(a => a.ActionId == 1).WeaponTypes);
            Assert.That(CharacterTestActionClassificationEditor.TrySaveAt(_root, 2, Array.Empty<string>(),
                after.Fingerprint, out changed, out issue), Is.True, issue);
            Assert.That(CharacterTestActionBrowserCatalog.Build(_root).Actions.Single(a => a.ActionId == 2).WeaponTypes, Is.Empty);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(Path.Combine(_root, Source)));
        }

        [Test]
        public void StaleOrInvalidClassificationCannotOverwriteFile()
        {
            var before = CharacterTestActionBrowserCatalog.Build(_root);
            string path = Path.Combine(_root, CharacterTestActionBrowserCatalog.ClassificationPath);
            File.AppendAllText(path, "\n");
            byte[] latest = File.ReadAllBytes(path);
            Assert.That(CharacterTestActionClassificationEditor.TrySaveAt(_root, 2, new[] { "sword" },
                before.Fingerprint, out _, out _), Is.False);
            var current = CharacterTestActionBrowserCatalog.Build(_root);
            Assert.That(CharacterTestActionClassificationEditor.TrySaveAt(_root, 2, new[] { "unknown" },
                current.Fingerprint, out _, out _), Is.False);
            CollectionAssert.AreEqual(latest, File.ReadAllBytes(path));
        }

        [Test]
        public void SavingUnchangedClassificationPreservesOriginalEvidenceAndBytes()
        {
            string path = Path.Combine(_root, CharacterTestActionBrowserCatalog.ClassificationPath);
            byte[] original = File.ReadAllBytes(path);
            var data = CharacterTestActionBrowserCatalog.Build(_root);
            Assert.That(CharacterTestActionClassificationEditor.TrySaveAt(_root, 1, new[] { "blade", "sword" },
                data.Fingerprint, out bool changed, out string issue), Is.True, issue);
            Assert.That(changed, Is.False);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        }

        private void Write(string relative, string text)
        {
            string path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }
    }
}
