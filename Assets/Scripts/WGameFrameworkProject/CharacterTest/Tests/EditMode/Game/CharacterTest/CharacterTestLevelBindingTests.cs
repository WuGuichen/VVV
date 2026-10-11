using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelBindingTests
    {
        private static CharacterTestRuntimeAiPlannerBehaviorGroupRevision Group(int revision) => new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
            "shared", revision, "group", "", null, 0, new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("shared"),
            Array.Empty<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference>());
        private static CharacterTestRuntimeAiPlannerProjectCatalogState Catalog(params CharacterTestRuntimeAiPlannerBehaviorGroupRevision[] groups)
        {
            var cards = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate("test", 1,
                Array.Empty<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(), new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null,null)).Catalog;
            Assert.That(cards, Is.Not.Null, "Valid frozen card fixture required.");
            return new CharacterTestRuntimeAiPlannerProjectCatalogState(cards, new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog("test", 1, groups), "", "", "", "", "");
        }
        [Test] public void SelectedRevisionDoesNotFollowNewLatest()
        {
            var first = Group(1); var second = Group(2);
            var actor = new CharacterTestSceneCharacterDefinition { StableId = "enemy.left", BehaviorBinding = CharacterTestLevelBehaviorCompiler.Select(first) };
            Assert.That(CharacterTestLevelBehaviorCompiler.Resolve("level.a", actor, Catalog(first,second)).Revision, Is.EqualTo(1));
        }
        [Test] public void MissingGroupReportsLevelAndActor()
        {
            var actor = new CharacterTestSceneCharacterDefinition { StableId = "enemy.left", BehaviorBinding = CharacterTestLevelBehaviorCompiler.Select(Group(1)) };
            var error = Assert.Throws<InvalidOperationException>(() => CharacterTestLevelBehaviorCompiler.Resolve("level.a", actor, Catalog()));
            StringAssert.Contains("level.a/enemy.left/missing-group", error.Message);
        }
        [Test] public void DriftedGroupHashIsRejected()
        {
            var group=Group(1); var actor=new CharacterTestSceneCharacterDefinition { StableId="enemy.left", BehaviorBinding=CharacterTestLevelBehaviorCompiler.Select(group) };
            actor.BehaviorBinding.GroupContentHash++;
            StringAssert.Contains("group-hash", Assert.Throws<InvalidOperationException>(()=>CharacterTestLevelBehaviorCompiler.Resolve("level.a",actor,Catalog(group))).Message);
        }
        [Test] public void UnknownAndDuplicateSlotBindingsAreRejected()
        {
            var group=Group(1); var actor=new CharacterTestSceneCharacterDefinition {StableId="enemy.left",BehaviorBinding=CharacterTestLevelBehaviorCompiler.Select(group)};
            actor.BehaviorBinding.Actions.Add(new CharacterTestLevelActionBinding {EntryId="unknown",SlotPath="attack",ActionId=455});
            Assert.Throws<InvalidOperationException>(()=>CharacterTestLevelBehaviorCompiler.Resolve("level.a",actor,Catalog(group)));
            actor.BehaviorBinding.Actions.Add(actor.BehaviorBinding.Actions[0]);
            Assert.Throws<InvalidOperationException>(()=>CharacterTestLevelBehaviorCompiler.Resolve("level.a",actor,Catalog(group)));
        }
        [Test] public void SameTemplateInstancesKeepIndependentBindingsThroughNormalize()
        {
            var left=new CharacterTestSceneCharacterDefinition {StableId="left",CharacterId=123,BehaviorBinding=CharacterTestLevelBehaviorCompiler.Select(Group(1))};
            left.BehaviorBinding.Actions.Add(new CharacterTestLevelActionBinding {EntryId="entry",SlotPath="attack",ActionId=455});
            var right=left.Normalize(); right.StableId="right";right.BehaviorBinding.Actions[0].ActionId=454;
            var copy=new CharacterTestSceneCharacterAuthoringDocument {Characters=new List<CharacterTestSceneCharacterDefinition>{left,right}}.Normalize();
            Assert.That(copy.Characters[0].BehaviorBinding.Actions[0].ActionId,Is.EqualTo(455));
            Assert.That(copy.Characters[1].BehaviorBinding.Actions[0].ActionId,Is.EqualTo(454));
            right.BehaviorBinding.Actions.Clear(); Assert.That(left.BehaviorBinding.Actions.Count,Is.EqualTo(1));
        }
        [Test] public void CacheAndSpawnSlotRemapKeepInstanceIdentity()
        {
            var binding=CharacterTestLevelBehaviorCompiler.Select(Group(1));
            var row=new CharacterTestSceneCharacterCache.CharacterInstance(true,"enemy.left","left",CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,2,
                CharacterTestCharacterControlOwner.AiControlled,null,"","",hasSpawnPose:true,behaviorBinding:binding);
            Assert.That(row.TryCreateSpawnSpec(new CharacterTestCharacterDefinitionRegistry(),out var spec,out _),Is.True);
            Assert.That(spec.WithRuntimeAiPlannerProfileSlot(8).InstanceStableId,Is.EqualTo("enemy.left"));
            var selection=new CharacterTestRuntimeAiBehaviorSelection(1,2,3,9);
            Assert.That(spec.WithRuntimeAiBehaviorSelection(selection).InstanceStableId,Is.EqualTo("enemy.left"));
            var cloned=row.BehaviorBinding;cloned.GroupRevision=99;Assert.That(row.BehaviorBinding.GroupRevision,Is.EqualTo(1));
        }
        [Test] public void ClearThenSerializeReloadOmitsBindingAndPreservesOldEnvelopeHash()
        {
            var document=new CharacterTestSceneCharacterAuthoringDocument {Characters=new List<CharacterTestSceneCharacterDefinition>{new CharacterTestSceneCharacterDefinition {StableId="enemy.left"}}};
            var before=CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(document);
            string hash=MxLevelAuthoringJson.ComputeContentHash(before);
            document.Characters[0].BehaviorBinding=CharacterTestLevelBehaviorCompiler.Select(Group(1));
            CharacterTestSceneCharacterAuthoringAdapter.UpsertCharactersModule(before,document);
            Assert.That(MxLevelAuthoringJson.ComputeContentHash(before),Is.Not.EqualTo(hash));
            document.Characters[0].BehaviorBinding=null;
            CharacterTestSceneCharacterAuthoringAdapter.UpsertCharactersModule(before,document.Normalize());
            string json=MxLevelAuthoringJson.SaveToJson(before);StringAssert.DoesNotContain("behaviorBinding",json);
            Assert.That(MxLevelAuthoringJson.ComputeContentHash(MxLevelAuthoringJson.LoadFromJson(json)),Is.EqualTo(hash));
        }
        [Test] public void DirectoryRejectsTraversalAndWrongPublishedHash()
        {
            string root=Path.Combine(Path.GetTempPath(),"mx-level-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try {
                Assert.Throws<InvalidDataException>(()=>CharacterTestLevelCatalog.SafePath(root,"../outside.json"));
                var doc=new MxLevelAuthoringDocument {LevelId="a",ScenePath="Assets/Scenes/Test.unity"};
                string json=MxLevelAuthoringJson.SaveToJson(doc);File.WriteAllText(Path.Combine(root,"a.json"),json);
                var catalog=new CharacterTestLevelCatalog {DefaultLevelId="a",Levels=new List<CharacterTestLevelEntry>{new CharacterTestLevelEntry{LevelId="a",Path="a.json",DocumentHash="wrong"}}};
                Assert.Throws<InvalidDataException>(()=>catalog.Load("a",root,true,out _));
            }finally{ Directory.Delete(root,true); }
        }
    }
}
