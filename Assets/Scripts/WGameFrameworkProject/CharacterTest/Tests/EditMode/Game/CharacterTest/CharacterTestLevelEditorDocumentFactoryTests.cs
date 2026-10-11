using System;
using System.Linq;
using MxFramework.Config.Levels;
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
