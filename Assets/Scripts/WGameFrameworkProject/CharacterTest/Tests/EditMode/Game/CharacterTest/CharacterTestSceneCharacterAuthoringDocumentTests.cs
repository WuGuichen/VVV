using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestSceneCharacterAuthoringTests
    {
        [Test]
        public void SceneCharacterJson_RoundTripsThroughLevelEnvelope()
        {
            GameObject root = CreateRoot();
            GameObject player = CreateChild(root.transform, "Player");
            GameObject enemy = CreateChild(root.transform, "Enemy");

            CharacterTestSceneCharacterAuthoringDocument document = CreateTwoCharacterDocument(
                root.transform,
                player.transform,
                enemy.transform);
            MxLevelAuthoringDocument envelope =
                CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(
                    document,
                    root.transform);

            string json = MxLevelAuthoringJson.SaveToJson(envelope);
            MxLevelAuthoringDocument loaded = MxLevelAuthoringJson.LoadFromJson(json);

            Assert.IsTrue(
                CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(
                    loaded,
                    out CharacterTestSceneCharacterAuthoringDocument roundTripped,
                    out LevelWorldObjectAuthoringValidationReport extractReport),
                JoinIssues(extractReport));
            Assert.AreEqual(2, roundTripped.Characters.Count);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerProfileJson.NormalizeStreamingAssetsRelativePath(
                    "MxFramework/CharacterTest/runtime_ai_planner/skill_caster.json"),
                FindCharacter(
                    roundTripped.Characters,
                    CharacterTestCharacterControlOwner.AiControlled).RuntimeAiPlannerProfilePath);

            LevelWorldObjectAuthoringValidationReport validation =
                CharacterTestSceneCharacterAuthoringAdapter.ValidateDocumentForCache(
                    roundTripped,
                    null,
                    root.transform,
                    new CharacterTestCharacterDefinitionRegistry());
            Assert.IsFalse(validation.HasErrors, JoinIssues(validation));
        }

        [Test]
        public void SceneCharacterValidator_ReportsDuplicateMissingAndInvalidFields()
        {
            var document = new CharacterTestSceneCharacterAuthoringDocument
            {
                LevelId = CharacterTestSceneCharacterAuthoringAdapter.DefaultLevelId,
                ScenePath = CharacterTestSceneCharacterAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterAuthoringTestRoot",
                Characters = new List<CharacterTestSceneCharacterDefinition>
                {
                    new CharacterTestSceneCharacterDefinition
                    {
                        Enabled = true,
                        StableId = "duplicate",
                        CharacterId = -1,
                        TeamId = 0,
                        ControlOwner = (CharacterTestCharacterControlOwner)999,
                        TransformRef = new LevelTransformReference()
                    },
                    new CharacterTestSceneCharacterDefinition
                    {
                        Enabled = true,
                        StableId = "duplicate",
                        CharacterId = CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                        TeamId = 1,
                        ControlOwner = CharacterTestCharacterControlOwner.LocalPlayer,
                        TransformRef = LevelTransformReference.FromPath("Missing")
                    }
                }
            };

            GameObject root = CreateRoot();
            LevelWorldObjectAuthoringValidationReport report =
                CharacterTestSceneCharacterAuthoringAdapter.ValidateDocumentForCache(
                    document,
                    null,
                    root.transform,
                    new CharacterTestCharacterDefinitionRegistry());

            Assert.IsTrue(report.HasErrors, JoinIssues(report));
            AssertIssue(report, CharacterTestSceneCharacterAuthoringAdapter.DuplicateStableId);
            AssertIssue(report, CharacterTestSceneCharacterAuthoringAdapter.MissingTransformPath);
            AssertIssue(report, CharacterTestSceneCharacterAuthoringAdapter.InvalidCharacterId);
            AssertIssue(report, CharacterTestSceneCharacterAuthoringAdapter.InvalidTeamId);
            AssertIssue(report, CharacterTestSceneCharacterAuthoringAdapter.InvalidControlOwner);
            AssertIssue(report, CharacterTestSceneCharacterAuthoringAdapter.TransformNotFound);
        }

        [Test]
        public void SceneCharacterValidator_AllowsInactiveRuntimeAiProfileWithoutAiControlOwner()
        {
            GameObject root = CreateRoot();
            GameObject player = CreateChild(root.transform, "Player");
            GameObject enemy = CreateChild(root.transform, "Enemy");
            CharacterTestSceneCharacterAuthoringDocument document =
                CreateTwoCharacterDocument(root.transform, player.transform, enemy.transform);
            CharacterTestSceneCharacterDefinition authoredEnemy =
                FindCharacter(document.Characters, CharacterTestCharacterControlOwner.AiControlled);
            authoredEnemy.ControlOwner = CharacterTestCharacterControlOwner.None;

            LevelWorldObjectAuthoringValidationReport report =
                CharacterTestSceneCharacterAuthoringAdapter.ValidateDocumentForCache(
                    document,
                    null,
                    root.transform,
                    new CharacterTestCharacterDefinitionRegistry());

            Assert.IsFalse(report.HasErrors, JoinIssues(report));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerProfileJson.NormalizeStreamingAssetsRelativePath(
                    "MxFramework/CharacterTest/runtime_ai_planner/skill_caster.json"),
                authoredEnemy.RuntimeAiPlannerProfilePath);
        }

        [Test]
        public void SceneCharacterAndWorldObjectModules_UpsertWithoutDroppingEachOther()
        {
            CharacterTestSceneCharacterAuthoringDocument characters =
                CharacterTestSceneCharacterAuthoringAdapter.CreateDefaultDocument();
            MxLevelAuthoringDocument envelope =
                CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(characters);

            var worldObjects = new LevelWorldObjectAuthoringDocument
            {
                LevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterAuthoringTestRoot",
                Points = new List<LevelWorldObjectPointDefinition>
                {
                    new LevelWorldObjectPointDefinition
                    {
                        Enabled = true,
                        DefinitionId = CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn,
                        StableId = "point.player",
                        TransformRef = LevelTransformReference.FromPath("Player")
                    }
                }
            };

            envelope = CharacterTestWorldObjectAuthoringAdapter.UpsertWorldObjectsModule(
                envelope,
                worldObjects);

            Assert.IsTrue(CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(
                envelope,
                out _,
                out LevelWorldObjectAuthoringValidationReport characterReport), JoinIssues(characterReport));
            Assert.IsTrue(CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                envelope,
                out _,
                out LevelWorldObjectAuthoringValidationReport worldReport), JoinIssues(worldReport));

            characters.Characters[0].DisplayName = "Changed";
            envelope = CharacterTestSceneCharacterAuthoringAdapter.UpsertCharactersModule(
                envelope,
                characters);

            Assert.IsTrue(CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                envelope,
                out LevelWorldObjectAuthoringDocument preservedWorldObjects,
                out worldReport), JoinIssues(worldReport));
            Assert.AreEqual(1, preservedWorldObjects.Points.Count);
        }

        [Test]
        public void LevelEditorEnvelopeMerge_UsesLatestSiblingModuleInBothSaveOrders()
        {
            CharacterTestSceneCharacterAuthoringDocument latestCharacters =
                CharacterTestSceneCharacterAuthoringAdapter.CreateDefaultDocument();
            latestCharacters.Characters[0].DisplayName = "Latest Character";
            MxLevelAuthoringDocument latest =
                CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(
                    latestCharacters);

            var currentWorldObjects = new LevelWorldObjectAuthoringDocument
            {
                LevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterAuthoringTestRoot",
                Points = new List<LevelWorldObjectPointDefinition>
                {
                    new LevelWorldObjectPointDefinition
                    {
                        Enabled = true,
                        DefinitionId = CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn,
                        StableId = "point.current",
                        Group = "Current World",
                        TransformRef = LevelTransformReference.FromPath("Player")
                    }
                }
            };

            MxLevelAuthoringDocument merged =
                CharacterTestLevelEditorEnvelopeMerge.MergeWorldObjects(
                    latest,
                    currentWorldObjects,
                    null);
            Assert.IsTrue(
                CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(
                    merged,
                    out CharacterTestSceneCharacterAuthoringDocument preservedCharacters,
                    out LevelWorldObjectAuthoringValidationReport characterReport),
                JoinIssues(characterReport));
            Assert.AreEqual("Latest Character", preservedCharacters.Characters[0].DisplayName);

            CharacterTestSceneCharacterAuthoringDocument currentCharacters =
                preservedCharacters.Normalize();
            currentCharacters.Characters[0].DisplayName = "Current Character";
            merged = CharacterTestLevelEditorEnvelopeMerge.MergeCharacters(
                merged,
                currentCharacters,
                null);

            Assert.IsTrue(
                CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                    merged,
                    out LevelWorldObjectAuthoringDocument preservedWorldObjects,
                    out LevelWorldObjectAuthoringValidationReport worldReport),
                JoinIssues(worldReport));
            Assert.AreEqual("Current World", preservedWorldObjects.Points[0].Group);
        }

        [Test]
        public void SceneCharacterCache_ProducesAiControlledSpawnSpecs()
        {
            GameObject root = CreateRoot();
            GameObject player = CreateChild(root.transform, "Player");
            GameObject enemy = CreateChild(root.transform, "Enemy");
            var cache = new CharacterTestSceneCharacterCache();
            cache.Configure(new[]
            {
                new CharacterTestSceneCharacterCache.CharacterInstance(
                    true,
                    "player.local",
                    "Player",
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    1,
                    CharacterTestCharacterControlOwner.LocalPlayer,
                    player.transform,
                    "Players",
                    string.Empty),
                new CharacterTestSceneCharacterCache.CharacterInstance(
                    true,
                    "enemy.ai",
                    "AI Enemy",
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    2,
                    CharacterTestCharacterControlOwner.AiControlled,
                    enemy.transform,
                    "Enemies",
                    string.Empty,
                    "MxFramework/CharacterTest/runtime_ai_planner/ranged_kiter.json")
            });

            CharacterTestCharacterSpawnSpec[] specs =
                cache.CollectCharacterSpawnSpecs(out string[] diagnostics);

            Assert.AreEqual(0, diagnostics.Length, string.Join("\n", diagnostics));
            Assert.AreEqual(2, specs.Length);
            AssertSpecOwner(specs, CharacterTestCharacterControlOwner.LocalPlayer, 1);
            AssertSpecOwner(specs, CharacterTestCharacterControlOwner.AiControlled, 2);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerProfileJson.NormalizeStreamingAssetsRelativePath(
                    "MxFramework/CharacterTest/runtime_ai_planner/ranged_kiter.json"),
                FindSpec(specs, CharacterTestCharacterControlOwner.AiControlled).RuntimeAiPlannerProfilePath);
        }

        [Test]
        public void SceneCharacterCacheSyncStatus_ReportsJsonRowsNotAppliedToEmptyCache()
        {
            GameObject root = CreateRoot();
            GameObject player = CreateChild(root.transform, "Player");
            GameObject enemy = CreateChild(root.transform, "Enemy");
            CharacterTestSceneCharacterAuthoringDocument document =
                CreateTwoCharacterDocument(root.transform, player.transform, enemy.transform);
            MxLevelAuthoringDocument envelope =
                CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(
                    document,
                    root.transform);

            CharacterTestSceneCharacterCacheSyncStatus status =
                CharacterTestSceneCharacterCacheSyncStatus.Evaluate(
                    document,
                    envelope,
                    new CharacterTestSceneCharacterCache(),
                    "Assets/Config/test.json");

            Assert.AreEqual(CharacterTestSceneCharacterCacheSyncKind.NotApplied, status.Kind);
            Assert.IsTrue(status.NeedsApply);
            Assert.That(status.Message, Does.Contain("场景缓存为空"));
        }

        [Test]
        public void SceneCharacterCacheSyncStatus_ReportsAppliedAfterEnvelopeAppliedToCache()
        {
            GameObject root = CreateRoot();
            GameObject player = CreateChild(root.transform, "Player");
            GameObject enemy = CreateChild(root.transform, "Enemy");
            CharacterTestSceneCharacterAuthoringDocument document =
                CreateTwoCharacterDocument(root.transform, player.transform, enemy.transform);
            MxLevelAuthoringDocument envelope =
                CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(
                    document,
                    root.transform);
            var cache = new CharacterTestSceneCharacterCache();

            bool applied = CharacterTestSceneCharacterAuthoringAdapter.ApplyEnvelopeToCache(
                envelope,
                cache,
                root.transform,
                "Assets/Config/test.json",
                out LevelWorldObjectAuthoringValidationReport report);

            Assert.IsTrue(applied, JoinIssues(report));
            CharacterTestSceneCharacterCacheSyncStatus status =
                CharacterTestSceneCharacterCacheSyncStatus.Evaluate(
                    document,
                    envelope,
                    cache,
                    "Assets/Config/test.json");

            Assert.AreEqual(CharacterTestSceneCharacterCacheSyncKind.Applied, status.Kind);
            Assert.IsTrue(status.IsApplied);
        }

        [Test]
        public void SceneCharacterCacheSyncStatus_ReportsOutOfSyncWhenJsonChangesAfterApply()
        {
            GameObject root = CreateRoot();
            GameObject player = CreateChild(root.transform, "Player");
            GameObject enemy = CreateChild(root.transform, "Enemy");
            CharacterTestSceneCharacterAuthoringDocument document =
                CreateTwoCharacterDocument(root.transform, player.transform, enemy.transform);
            MxLevelAuthoringDocument appliedEnvelope =
                CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(
                    document,
                    root.transform);
            var cache = new CharacterTestSceneCharacterCache();
            Assert.IsTrue(CharacterTestSceneCharacterAuthoringAdapter.ApplyEnvelopeToCache(
                appliedEnvelope,
                cache,
                root.transform,
                "Assets/Config/test.json",
                out LevelWorldObjectAuthoringValidationReport report), JoinIssues(report));

            document.Characters[1].TeamId = 3;
            MxLevelAuthoringDocument editedEnvelope =
                CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(
                    document,
                    root.transform);
            CharacterTestSceneCharacterCacheSyncStatus status =
                CharacterTestSceneCharacterCacheSyncStatus.Evaluate(
                    document,
                    editedEnvelope,
                    cache,
                    "Assets/Config/test.json");

            Assert.AreEqual(CharacterTestSceneCharacterCacheSyncKind.OutOfSync, status.Kind);
            Assert.IsTrue(status.NeedsApply);
        }

        [Test]
        public void RuntimeSessionBuilder_DoesNotUseWorldObjectPointsWhenCharacterCacheIsEmpty()
        {
            var host = new GameObject("CharacterAuthoringTestGameManager");
            host.SetActive(false);
            var profile = ScriptableObject.CreateInstance<CharacterTestSceneProfile>();
            try
            {
                GameManager manager = host.AddComponent<GameManager>();
                GameObject playerPoint = CreateChild(host.transform, "PlayerPoint");
                GameObject aiPoint = CreateChild(host.transform, "AiPoint");
                manager.WorldObjectSceneCache.Configure(
                    Array.Empty<CharacterTestWorldObjectSceneSource.MovableInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.StaticColliderInstance>(),
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.LevelPointInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn,
                            "point.player",
                            playerPoint.transform),
                        new CharacterTestWorldObjectSceneSource.LevelPointInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.PointAiSpawn,
                            "point.ai",
                            aiPoint.transform)
                    },
                    Array.Empty<CharacterTestWorldObjectSceneSource.LevelRegionInstance>());
                var context = new CharacterTestRuntimeContext(manager, profile);
                var builder = new CharacterTestRuntimeSessionBuilder(manager, context, profile);

                CharacterTestCharacterSpawnSpec[] specs = InvokeCollectLevelCharacterSpawnSpecs(builder);

                Assert.AreEqual(0, specs.Length);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeSessionBuilder_AssignsRuntimeAiPlannerProfileSlotFromCharacterPath()
        {
            var host = new GameObject("CharacterAuthoringTestGameManager");
            host.SetActive(false);
            var profile = ScriptableObject.CreateInstance<CharacterTestSceneProfile>();
            try
            {
                GameManager manager = host.AddComponent<GameManager>();
                var context = new CharacterTestRuntimeContext(manager, profile);
                var builder = new CharacterTestRuntimeSessionBuilder(manager, context, profile);
                var specs = new[]
                {
                    new CharacterTestCharacterSpawnSpec(
                        CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                        teamId: 1,
                        x: 0f,
                        y: 1f,
                        z: 0f,
                        CharacterTestCharacterControlOwner.LocalPlayer),
                    new CharacterTestCharacterSpawnSpec(
                        CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                        teamId: 2,
                        x: 2f,
                        y: 1f,
                        z: 0f,
                        CharacterTestCharacterControlOwner.AiControlled,
                        "MxFramework/CharacterTest/runtime_ai_planner/skill_caster.json")
                };

                CharacterTestRuntimeAiPlannerProfileBinding[] bindings =
                    InvokeBuildRuntimeAiPlannerProfileBindings(
                        builder,
                        specs,
                        out CharacterTestCharacterSpawnSpec[] assignedSpecs);

                Assert.AreEqual(1, bindings.Length);
                Assert.AreEqual(1, bindings[0].Slot);
                Assert.AreEqual(0, assignedSpecs[0].RuntimeAiPlannerProfileSlot);
                Assert.AreEqual(1, assignedSpecs[1].RuntimeAiPlannerProfileSlot);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(host);
            }
        }
    }
}
