using System;
using System.IO;
using System.Linq;
using MxFramework.Config.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelEditorDocumentFactoryTests
    {
        private static MxLevelAuthoringDocument Fixture()
        {
            var document = new MxLevelAuthoringDocument { LevelId = "level.a", ScenePath = "Assets/Scenes/fixture.unity" };
            document.Modules.Add(new MxLevelAuthoringModuleDocument { ModuleId = "characterTest.characters", SchemaVersion = 2,
                Payload = JObject.Parse(@"{'characters':[{'stableId':'level.a.player','behaviorBinding':{'groupId':'shared.group','groupRevision':3,'groupContentHash':77,'actions':[{'entryId':'shared.entry','dependencyFingerprint':'old'}]}},{'stableId':'custom.enemy'}]}") });
            document.Modules.Add(new MxLevelAuthoringModuleDocument { ModuleId = "characterTest.tacticalStage", SchemaVersion = 1,
                Payload = JObject.Parse(@"{'playerSpawnStableId':'level.a.player','opponentSpawnStableId':'custom.enemy','initialMomentum':50}") });
            document.Modules.Add(new MxLevelAuthoringModuleDocument { ModuleId = "characterTest.guidance", SchemaVersion = 1,
                Payload = JObject.Parse(@"{'enabled':true,'steps':[{'stableId':'level.a.step','actor':'custom.enemy'}],'inputFingerprint':'old'}") });
            document.Modules.Add(new MxLevelAuthoringModuleDocument { ModuleId = "characterTest.genesisValidation", SchemaVersion = 1,
                Payload = JObject.Parse(@"{'stations':[{'stableId':'level.a.station'}]}") });
            document.Modules.Add(new MxLevelAuthoringModuleDocument { ModuleId = "characterTest.gameplayTriggers", SchemaVersion = 1,
                Payload = JObject.Parse(@"{'triggers':[{'stableId':'level.a.trigger','actor':'level.a.player'}]}") });
            return document;
        }

        [Test]
        public void CopyRekeysOwnedIdsAndKeepsAllExactActorReferences()
        {
            var copy = CharacterTestLevelEditorDocumentFactory.Clone(Fixture(), "level.b", false);
            copy.TryGetModule("characterTest.characters", out var characters);
            var actors = (JArray)characters.Payload["characters"];
            copy.TryGetModule("characterTest.tacticalStage", out var stage);
            Assert.AreEqual("level.b", copy.LevelId);
            Assert.AreEqual("level.b.player", (string)actors[0]["stableId"]);
            Assert.AreEqual((string)actors[0]["stableId"], (string)stage.Payload["playerSpawnStableId"]);
            Assert.That((string)actors[1]["stableId"], Does.StartWith("level.b.instance."));
            Assert.AreEqual((string)actors[1]["stableId"], (string)stage.Payload["opponentSpawnStableId"]);
            copy.TryGetModule("characterTest.guidance", out var guidance);
            Assert.AreEqual((string)actors[1]["stableId"], (string)guidance.Payload["steps"][0]["actor"]);
        }

        [Test]
        public void CopyKeepsPublishedGroupVersionAndCardEntryIdentity()
        {
            var copy = CharacterTestLevelEditorDocumentFactory.Clone(Fixture(), "level.b", false);
            copy.TryGetModule("characterTest.characters", out var characters);
            var binding = characters.Payload["characters"][0]["behaviorBinding"];
            Assert.AreEqual("shared.group", (string)binding["groupId"]);
            Assert.AreEqual(3, (int)binding["groupRevision"]);
            Assert.AreEqual(77L, (long)binding["groupContentHash"]);
            Assert.AreEqual("shared.entry", (string)binding["actions"][0]["entryId"]);
            Assert.AreEqual("", (string)binding["actions"][0]["dependencyFingerprint"]);
        }

        [Test]
        public void CopyDoesNotMutateSourceAndNestedObjectsAreIndependent()
        {
            var source = Fixture(); string before = MxLevelAuthoringJson.SaveToJson(source);
            var copy = CharacterTestLevelEditorDocumentFactory.Clone(source, "level.b", false);
            copy.Modules[0].Payload["characters"][0]["stableId"] = "edited";
            Assert.AreEqual(before, MxLevelAuthoringJson.SaveToJson(source));
        }

        [TestCase("groupId")]
        [TestCase("cardId")]
        [TestCase("entryId")]
        [TestCase("actionId")]
        [TestCase("resourceKey")]
        public void CopyPreservesPublishedReferencesThatCollideWithActorIdentity(string field)
        {
            var source = Fixture();
            source.TryGetModule("characterTest.characters", out var characters);
            var actor = (JObject)characters.Payload["characters"][0];
            actor["stableId"] = "shared.pin";
            actor["displayName"] = "shared.pin";
            actor["behaviorBinding"][field] = "shared.pin";
            actor["behaviorBinding"]["publishedResource"] = JObject.Parse("{'stableId':'shared.pin'}");
            source.TryGetModule("characterTest.tacticalStage", out var stage);
            stage.Payload["playerSpawnStableId"] = "shared.pin";
            string before = MxLevelAuthoringJson.SaveToJson(source);
            var copy = CharacterTestLevelEditorDocumentFactory.Clone(source, "level.b", false);
            copy.TryGetModule("characterTest.characters", out var copiedCharacters);
            var copiedActor = (JObject)copiedCharacters.Payload["characters"][0];
            copy.TryGetModule("characterTest.tacticalStage", out var copiedStage);
            Assert.AreNotEqual("shared.pin", (string)copiedActor["stableId"]);
            Assert.AreEqual((string)copiedActor["stableId"], (string)copiedStage.Payload["playerSpawnStableId"]);
            Assert.AreEqual("shared.pin", (string)copiedActor["behaviorBinding"][field]);
            Assert.AreEqual("shared.pin", (string)copiedActor["behaviorBinding"]["publishedResource"]["stableId"]);
            Assert.AreEqual("shared.pin", (string)copiedActor["displayName"]);
            Assert.AreEqual(before, MxLevelAuthoringJson.SaveToJson(source));
        }

        [Test]
        public void CopyRekeysOwnedWorldAndGenesisIdentitiesAndTheirTypedReferences()
        {
            var source = Fixture();
            source.Modules.Add(new MxLevelAuthoringModuleDocument { ModuleId = "characterTest.worldObjects", SchemaVersion = 1,
                Payload = JObject.Parse(@"{'levelId':'level.a','points':[{'stableId':'level.a.point'}],
                    'regions':[{'stableId':'level.a.region'}],
                    'movableInstances':[{'instanceStableId':'level.a.movable','definitionStableId':'level.a.player'}]}") });
            source.TryGetModule("characterTest.genesisValidation", out var genesis);
            genesis.Payload["groups"] = JArray.Parse("[{'stableId':'level.a.local-group'}]");
            genesis.Payload["stations"][0]["groupId"] = "level.a.local-group";
            genesis.Payload["stations"][0]["bindings"] = JObject.Parse(@"{
                'subjectCharacter':{'stableId':'level.a.player'},
                'targetMovableObject':{'instanceStableId':'level.a.movable'},
                'startPoint':{'stableId':'level.a.point'},'resetPoint':{'stableId':'level.a.point'},
                'observationRegion':{'stableId':'level.a.region'}}");
            source.TryGetModule("characterTest.gameplayTriggers", out var triggers);
            triggers.Payload["regionBindings"] = JArray.Parse("[{'regionStableId':'level.a.region'}]");
            var copy = CharacterTestLevelEditorDocumentFactory.Clone(source, "level.b", false);
            copy.TryGetModule("characterTest.worldObjects", out var world);
            copy.TryGetModule("characterTest.genesisValidation", out var copiedGenesis);
            copy.TryGetModule("characterTest.gameplayTriggers", out var copiedTriggers);
            Assert.AreEqual("level.b", (string)world.Payload["levelId"]);
            Assert.AreEqual("level.b.movable", (string)world.Payload["movableInstances"][0]["instanceStableId"]);
            Assert.AreEqual("level.a.player", (string)world.Payload["movableInstances"][0]["definitionStableId"]);
            var station = copiedGenesis.Payload["stations"][0];
            Assert.AreEqual("level.b.local-group", (string)station["groupId"]);
            Assert.AreEqual("level.b.local-group", (string)copiedGenesis.Payload["groups"][0]["stableId"]);
            Assert.AreEqual("level.b.player", (string)station["bindings"]["subjectCharacter"]["stableId"]);
            Assert.AreEqual("level.b.movable", (string)station["bindings"]["targetMovableObject"]["instanceStableId"]);
            Assert.AreEqual("level.b.point", (string)station["bindings"]["startPoint"]["stableId"]);
            Assert.AreEqual("level.b.point", (string)station["bindings"]["resetPoint"]["stableId"]);
            Assert.AreEqual("level.b.region", (string)station["bindings"]["observationRegion"]["stableId"]);
            Assert.AreEqual("level.b.region", (string)copiedTriggers.Payload["regionBindings"][0]["regionStableId"]);
        }

        [Test]
        public void FormalGroupCollisionCopySaveReloadResolvesExactPublishedGroup()
        {
            string project = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            string template = Path.Combine(project, "Assets/Config/MxFramework/Levels/FormalExamples/formal.sword.courtyard.level.json");
            string templateBefore = File.ReadAllText(template);
            var source = MxLevelAuthoringJson.LoadFromJson(templateBefore);
            source.TryGetModule("characterTest.characters", out var characters);
            var actor = (JObject)characters.Payload["characters"][0];
            string groupId = (string)actor["behaviorBinding"]["groupId"];
            actor["stableId"] = groupId;
            string sourceBefore = MxLevelAuthoringJson.SaveToJson(source);
            var copy = CharacterTestLevelEditorDocumentFactory.Clone(source, "level.p02.collision", false);
            string path = Path.Combine(Path.GetTempPath(), "mx-level-copy-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, MxLevelAuthoringJson.SaveToJson(copy));
                var reloaded = MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(path));
                reloaded.TryGetModule("characterTest.characters", out var reloadedCharacters);
                var definition = reloadedCharacters.Payload["characters"][0].ToObject<CharacterTestSceneCharacterDefinition>();
                Assert.AreNotEqual(groupId, definition.StableId);
                Assert.AreEqual(groupId, definition.BehaviorBinding.GroupId);
                var catalog = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
                Assert.IsTrue(catalog.Success, catalog.Diagnostic);
                var group = CharacterTestLevelBehaviorCompiler.Resolve(reloaded.LevelId, definition, catalog);
                Assert.AreEqual(groupId, group.StableGroupId);
                Assert.AreEqual(definition.BehaviorBinding.GroupContentHash, group.ContentHash);
                Assert.AreEqual(sourceBefore, MxLevelAuthoringJson.SaveToJson(source));
                Assert.AreEqual(templateBefore, File.ReadAllText(template));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void EmptyLevelClearsActorDependentRulesAndKeepsExistingNumericRules()
        {
            var copy = CharacterTestLevelEditorDocumentFactory.Clone(Fixture(), "level.b", true);
            copy.TryGetModule("characterTest.characters", out var characters);
            copy.TryGetModule("characterTest.guidance", out var guidance);
            copy.TryGetModule("characterTest.genesisValidation", out var genesis);
            copy.TryGetModule("characterTest.gameplayTriggers", out var triggers);
            copy.TryGetModule("characterTest.tacticalStage", out var stage);
            Assert.AreEqual(0, ((JArray)characters.Payload["characters"]).Count);
            Assert.AreEqual(0, ((JArray)guidance.Payload["steps"]).Count);
            Assert.IsFalse((bool)guidance.Payload["enabled"]);
            Assert.AreEqual(0, ((JArray)genesis.Payload["stations"]).Count);
            Assert.AreEqual(0, ((JArray)triggers.Payload["triggers"]).Count);
            Assert.AreEqual("", (string)stage.Payload["playerSpawnStableId"]);
            Assert.AreEqual("", (string)stage.Payload["opponentSpawnStableId"]);
            Assert.AreEqual(50, (int)stage.Payload["initialMomentum"]);
        }

        [Test]
        public void SameLevelIdentityAndUnsafeNewIdentityAreRejected()
        {
            Assert.Throws<ArgumentException>(() => CharacterTestLevelEditorDocumentFactory.Clone(Fixture(), "level.a", false));
            Assert.Throws<ArgumentException>(() => CharacterTestLevelEditorDocumentFactory.Clone(Fixture(), "../bad", false));
            Assert.Throws<ArgumentException>(() => CharacterTestLevelEditorDocumentFactory.Clone(Fixture(), "Upper", false));
        }

        [Test]
        public void CopyIsDeterministicForSameSourceAndNewIdentity()
        {
            string first = MxLevelAuthoringJson.SaveToJson(CharacterTestLevelEditorDocumentFactory.Clone(Fixture(), "level.b", false));
            string second = MxLevelAuthoringJson.SaveToJson(CharacterTestLevelEditorDocumentFactory.Clone(Fixture(), "level.b", false));
            Assert.AreEqual(first, second);
        }
    }
}
