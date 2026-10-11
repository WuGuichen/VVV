using System;
using MxFramework.Config.Levels;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelEditorWorkflowPureTests
    {
        private static CharacterTestSceneCharacterAuthoringDocument Fixture()
            => new CharacterTestSceneCharacterAuthoringDocument { Characters = new List<CharacterTestSceneCharacterDefinition> {
                new CharacterTestSceneCharacterDefinition { StableId = "actor.alpha", TeamId = 1, Group = "Original" },
                new CharacterTestSceneCharacterDefinition { StableId = "actor.beta", TeamId = 2, Group = "Original" },
                new CharacterTestSceneCharacterDefinition { StableId = "actor.alpha.long", TeamId = 3, Group = "Other" } } };
        [Test] public void BatchUpdatesOnlySelectedActors()
        {
            var document = Fixture();
            CharacterTestLevelBatchEdits.Apply(document, new[] { "actor.alpha", "actor.beta" }, 9, true, "Joined");
            Assert.AreEqual(9, document.Characters[0].TeamId); Assert.AreEqual(9, document.Characters[1].TeamId);
            Assert.AreEqual("Joined", document.Characters[0].Group); Assert.AreEqual("Joined", document.Characters[1].Group);
            Assert.AreEqual(3, document.Characters[2].TeamId); Assert.AreEqual("Other", document.Characters[2].Group);
        }
        [Test] public void BatchMissingActorRejectsBeforeChangingAnyActor()
        {
            var document = Fixture();
            Assert.Throws<InvalidOperationException>(() => CharacterTestLevelBatchEdits.Apply(document, new[] { "actor.alpha", "missing" }, 9, true, "Joined"));
            Assert.AreEqual(1, document.Characters[0].TeamId); Assert.AreEqual("Original", document.Characters[0].Group);
        }
        [Test] public void BatchDuplicateActorIdentityRejectsWithoutPartialMutation()
        {
            var document = Fixture(); document.Characters[2].StableId = "actor.beta";
            Assert.Throws<InvalidOperationException>(() => CharacterTestLevelBatchEdits.Apply(document, new[] { "actor.alpha", "actor.beta" }, 9, true, "Joined"));
            Assert.AreEqual(1, document.Characters[0].TeamId); Assert.AreEqual(2, document.Characters[1].TeamId);
        }
        [Test] public void GroupOnlyBatchRetainsTeamAndAllowsEmptyGroup()
        {
            var document = Fixture(); CharacterTestLevelBatchEdits.Apply(document, new[] { "actor.alpha", "actor.alpha" }, null, true, "");
            Assert.AreEqual(1, document.Characters[0].TeamId); Assert.AreEqual("", document.Characters[0].Group);
        }
        [Test] public void CharacterDiagnosticIndexLocatesExactActor()
        {
            Assert.AreEqual("actor.beta", CharacterTestLevelDiagnosticLocation.ActorId("modules[0].payload.characters[1].behaviorBinding.actions[0]", Fixture()));
            Assert.AreEqual(-1, CharacterTestLevelDiagnosticLocation.ActorIndex("characters[-1]"));
        }
        [Test] public void PublishedDiagnosticLocatesActorUsingExactBoundaries()
        {
            Assert.AreEqual("actor.alpha.long", CharacterTestLevelDiagnosticLocation.ActorId("level.a/actor.alpha.long/card.pin/slot", Fixture()));
            Assert.IsNull(CharacterTestLevelDiagnosticLocation.ActorId("level.a/actor.alpha.extra/card.pin/slot", Fixture()));
        }
        [Test] public void UnlocatableDiagnosticsRemainExplicit()
        {
            Assert.IsNull(CharacterTestLevelDiagnosticLocation.ActorId("characters[999].teamId", Fixture()));
            Assert.IsNull(CharacterTestLevelDiagnosticLocation.ActorId("world/static-floor", Fixture()));
        }
        [Test] public void JoinedSaveContextProvidesDeepCopiesOnlyForTheSelectedDocument()
        {
            var document = new MxLevelAuthoringDocument { LevelId = "a", ScenePath = "Assets/A.unity" };
            using var scope = new CharacterTestJoinedLevelSaveContext("a.json", document);
            Assert.IsFalse(CharacterTestJoinedLevelSaveContext.TryGet("b.json", out _));
            Assert.IsTrue(CharacterTestJoinedLevelSaveContext.TryGet("a.json", out var first)); first.LevelId = "changed";
            Assert.IsTrue(CharacterTestJoinedLevelSaveContext.TryGet("a.json", out var second)); Assert.AreEqual("a", second.LevelId);
        }
        [Test] public void NestedJoinedSaveContextRestoresOuterDocumentAndClearsOnDispose()
        {
            using (var outer = new CharacterTestJoinedLevelSaveContext("a.json", new MxLevelAuthoringDocument { LevelId = "a" }))
            {
                using (var inner = new CharacterTestJoinedLevelSaveContext("b.json", new MxLevelAuthoringDocument { LevelId = "b" }))
                    Assert.IsFalse(CharacterTestJoinedLevelSaveContext.TryGet("a.json", out _));
                Assert.IsTrue(CharacterTestJoinedLevelSaveContext.TryGet("a.json", out var restored)); Assert.AreEqual("a", restored.LevelId);
            }
            Assert.IsFalse(CharacterTestJoinedLevelSaveContext.TryGet("a.json", out _));
        }
    }
}
