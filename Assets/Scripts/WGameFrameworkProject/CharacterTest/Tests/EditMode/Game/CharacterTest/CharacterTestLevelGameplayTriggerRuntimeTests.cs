using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;
using MxFramework.Core.Math;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.GameplayBridge;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelGameplayTriggerRuntimeTests
    {
        private const string RegionStableId = "region.runtime.test";

        [Test]
        public void SceneCacheCompiler_ProjectsColliderFreeRegionToFixedCombatAabb()
        {
            GameObject root = null;
            try
            {
                CharacterTestGameplayTriggerSceneCacheData data =
                    CreateCacheData(
                        CharacterTestLevelGameplayTriggerAuthoringDocument
                            .GameplayApplyBuffEnterOnceTemplateId,
                        CharacterTestGuardAuraIds.BuffId,
                        out root);

                Assert.That(
                    root.GetComponentInChildren<Collider>(),
                    Is.Null,
                    "Trigger regions must not require Unity Physics colliders.");
                Assert.That(data.TriggerEntryCount, Is.EqualTo(1));
                Assert.That(data.SensorRows, Has.Length.EqualTo(1));
                CharacterTestGameplayTriggerSensorCacheRow row =
                    data.SensorRows[0];
                Assert.That(row.SourceBindingId, Is.GreaterThan(0));
                Assert.That(
                    row.LayerMask,
                    Is.EqualTo(
                        1 << CharacterTestCharacterPhysicsRuntime
                            .DynamicCharacterLayer));
                Assert.That(row.Center, Is.EqualTo(ToFixVector(0f, 1f, 0f)));
                Assert.That(
                    row.HalfExtents,
                    Is.EqualTo(ToFixVector(5f, 3f, 5f)));
                Assert.That(data.TriggerCatalogHash, Is.Not.Zero);
                Assert.That(data.SensorCatalogHash, Is.Not.Zero);
                Assert.That(data.CombinedCatalogHash, Is.Not.Zero);
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeInspector_ShowsCompiledQueryFromSceneCache()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            try
            {
                string fullPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    CharacterTestWorldObjectAuthoringAdapter
                        .DefaultDocumentPath);
                MxLevelAuthoringDocument savedEnvelope =
                    MxLevelAuthoringJson.LoadFromJson(
                        File.ReadAllText(fullPath));
                Assert.That(
                    CharacterTestWorldObjectAuthoringAdapter
                        .TryExtractWorldObjectsDocument(
                            savedEnvelope,
                            out LevelWorldObjectAuthoringDocument world,
                            out LevelWorldObjectAuthoringValidationReport
                                worldReport),
                    Is.True,
                    JoinIssues(worldReport));
                Assert.That(world.Regions, Is.Not.Empty);
                LevelWorldObjectRegionDefinition region =
                    world.Regions[0];
                region.Enabled = true;
                region.RegionKind =
                    LevelWorldObjectRegionKind.TriggerRegion;

                var trigger =
                    new CharacterTestLevelGameplayTriggerAuthoringDocument();
                trigger.AddTemplate(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    region.StableId,
                    CharacterTestGuardAuraIds.BuffId);
                MxLevelAuthoringDocument envelope =
                    CharacterTestWorldObjectAuthoringAdapter
                        .CreateEnvelopeFromDocument(world);
                envelope =
                    CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                        envelope,
                        trigger);
                GameManager gameManager =
                    Object.FindAnyObjectByType<GameManager>();
                Assert.That(gameManager, Is.Not.Null);
                Assert.That(
                    CharacterTestGameplayTriggerSceneCacheCompiler.TryCompile(
                        envelope,
                        world,
                        gameManager.WorldObjectSceneRoot,
                        CharacterTestWorldObjectDefinitionCatalog
                            .CreateCharacterTestSceneCatalog(),
                        out CharacterTestGameplayTriggerSceneCacheData data,
                        out LevelWorldObjectAuthoringValidationReport report),
                    Is.True,
                    JoinIssues(report));
                gameManager.GameplayTriggerSceneCache.Configure(data);

                module.SetDocumentForTests(world);
                module.SetTriggerDocumentForTests(
                    trigger,
                    markDirty: false);
                module.SetRuntimeInspectorForTests(true);
                var context =
                    new MxLevelEditorContext(
                        null,
                        module,
                        () => { });
                IReadOnlyList<MxLevelEditorListRow> rows =
                    module.BuildRows(context, "regions");
                Assert.That(rows, Is.Not.Empty);
                var inspector = new VisualElement();
                module.BuildInspector(
                    context,
                    rows[0],
                    inspector);

                Assert.That(
                    inspector.Q<VisualElement>(
                        "character-test-trigger-runtime-inspector"),
                    Is.Not.Null);
                Label query = inspector.Q<Label>(
                    "character-test-trigger-runtime-query");
                Assert.That(query, Is.Not.Null);
                StringAssert.Contains("Combat AABB", query.text);
                Label startup = inspector.Q<Label>(
                    "character-test-trigger-runtime-startup");
                Assert.That(startup, Is.Not.Null);
                StringAssert.Contains("未进入 Play Mode", startup.text);
                yield return null;
            }
            finally
            {
                EditorSceneManager.OpenScene(
                    CharacterTestWorldObjectAuthoringAdapter
                        .DefaultScenePath,
                    OpenSceneMode.Single);
            }
        }

        [Test]
        public void GameSlice_ValidCacheRunsCombatSensorAndBuffAction()
        {
            CharacterTestGameplayTriggerSceneCache cache =
                CreateCache(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    CharacterTestGuardAuraIds.BuffId);
            using (CharacterTestSliceTestHarness harness =
                   CreateHarness(cache))
            {
                CharacterTestCharacterEntity player =
                    harness.SpawnLocalPlayer(
                        new CharacterTestCharacterSpawnSpec(
                            CharacterTestCharacterDefinitionRegistry
                                .DefaultPlayableCharacterId,
                            teamId: 1,
                            x: 0f,
                            y: 1f,
                            z: 0f));

                CharacterTestLevelGameplayTriggerRuntimeStatus status =
                    harness.Slice.LevelGameplayTriggerStatus;
                Assert.That(
                    status.StartupState,
                    Is.EqualTo(
                        CharacterTestLevelGameplayTriggerStartupState.Ready));
                Assert.That(
                    status.TryCapture(
                        cache.SensorRows[0].SourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            snapshot),
                    Is.True);
                Assert.That(snapshot.HasSensorResult, Is.True);
                Assert.That(
                    snapshot.SensorResult.Status,
                    Is.EqualTo(
                        MxFramework.Gameplay.Triggers.CombatBridge
                            .GameplayTriggerCombatSensorTickStatus.Submitted));
                Assert.That(
                    snapshot.SensorResult.QueryHitCount,
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(snapshot.HasOccurrence, Is.True);
                Assert.That(
                    snapshot.Occurrence.OccurrenceKind,
                    Is.EqualTo(GameplayTriggerOccurrenceKind.Entered)
                        .Or.EqualTo(
                            GameplayTriggerOccurrenceKind.Stayed));
                Assert.That(snapshot.HasActionDispatch, Is.True);
                Assert.That(snapshot.HasActionResult, Is.True);
                Assert.That(
                    snapshot.ActionResult.Status,
                    Is.EqualTo(
                        GameplayTriggerRuntimeEventStatus.Succeeded));

                GameplayComponentWorld componentWorld =
                    harness.Modules.CharacterRuntime.World
                        .GameplayComponentWorld;
                Assert.That(
                    componentWorld.TryGetStore(
                        out GameplayComponentStore<
                            GameplayComponentBuffSetComponent> store),
                    Is.True);
                Assert.That(
                    store.TryGet(
                        player.GameplayEntityId,
                        out GameplayComponentBuffSetComponent buffSet),
                    Is.True);
                Assert.That(
                    buffSet.TryGet(
                        CharacterTestGuardAuraIds.BuffId,
                        out _),
                    Is.True);
            }
        }

        [Test]
        public void GameSlice_PresenceBuffAppliesToEverySubjectInSameEvaluation()
        {
            CharacterTestGameplayTriggerSceneCache cache =
                CreateCache(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    CharacterTestPoisonIds.PoisonedBuffId);
            using (CharacterTestSliceTestHarness harness =
                   CreateHarness(
                       cache,
                       startupCharacterSpawnSpecs:
                           Array.Empty<
                               CharacterTestCharacterSpawnSpec>()))
            {
                RuntimeCommandValidationResult aiSpawn =
                    harness.Enqueue(
                        CharacterTestRuntimeCommandFactory.SpawnCharacter(
                            harness.CurrentFrame,
                            CharacterTestRuntimeCommandSources.System,
                            CharacterTestCharacterSpawnSpec
                                .TemporaryAiOpponent,
                            new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0025)));
                RuntimeCommandValidationResult playerSpawn =
                    harness.Enqueue(
                        CharacterTestRuntimeCommandFactory.SpawnCharacter(
                            harness.CurrentFrame,
                            CharacterTestRuntimeCommandSources.System,
                            CharacterTestCharacterSpawnSpec
                                .TemporaryPlayer,
                            new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0026)));
                Assert.That(aiSpawn.Success, Is.True, aiSpawn.Error.Message);
                Assert.That(
                    playerSpawn.Success,
                    Is.True,
                    playerSpawn.Error.Message);

                harness.Tick();

                Assert.That(harness.CharacterEntities, Has.Count.EqualTo(2));
                Assert.That(harness.LocalPlayer, Is.Not.Null);
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.TryCapture(
                        cache.SensorRows[0].SourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            snapshot),
                    Is.True);
                Assert.That(
                    snapshot.SensorResult.SubmittedSubjectCount,
                    Is.EqualTo(2));

                GameplayComponentWorld componentWorld =
                    harness.Modules.CharacterRuntime.World
                        .GameplayComponentWorld;
                Assert.That(
                    componentWorld.TryGetStore(
                        out GameplayComponentStore<
                            GameplayComponentBuffSetComponent> store),
                    Is.True);
                for (int i = 0;
                     i < harness.CharacterEntities.Count;
                     i++)
                {
                    CharacterTestCharacterEntity entity =
                        harness.CharacterEntities[i];
                    Assert.That(
                        store.TryGet(
                            entity.GameplayEntityId,
                            out GameplayComponentBuffSetComponent buffSet),
                        Is.True,
                        "Missing Buff set for entity "
                        + entity.Id.Value);
                    Assert.That(
                        buffSet.TryGet(
                            CharacterTestPoisonIds.PoisonedBuffId,
                            out _),
                        Is.True,
                        "Poison was not applied to entity "
                        + entity.Id.Value
                        + " ("
                        + entity.Control.Owner
                        + ").");
                }
            }
        }

        [Test]
        public void GameSlice_PresenceBuffPersistsInsideAndRemovesOnExit()
        {
            CharacterTestGameplayTriggerSceneCache cache =
                CreateCache(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    CharacterTestPoisonIds.PoisonedBuffId);
            using (CharacterTestSliceTestHarness harness =
                   CreateHarness(
                       cache,
                       startupCharacterSpawnSpecs:
                           Array.Empty<
                               CharacterTestCharacterSpawnSpec>()))
            {
                CharacterTestCharacterEntity player =
                    harness.SpawnLocalPlayer(
                        new CharacterTestCharacterSpawnSpec(
                            CharacterTestCharacterDefinitionRegistry
                                .DefaultPlayableCharacterId,
                            teamId: 1,
                            x: 0f,
                            y: 1f,
                            z: 0f));
                GameplayComponentWorld componentWorld =
                    harness.Modules.CharacterRuntime.World
                        .GameplayComponentWorld;
                Assert.That(
                    componentWorld.TryGetStore(
                        out GameplayComponentStore<
                            GameplayComponentBuffSetComponent> store),
                    Is.True);
                Assert.That(
                    store.TryGet(
                        player.GameplayEntityId,
                        out GameplayComponentBuffSetComponent enteredSet),
                    Is.True);
                Assert.That(
                    enteredSet.TryGet(
                        CharacterTestPoisonIds.PoisonedBuffId,
                        out _),
                    Is.True);

                harness.Tick();

                Assert.That(
                    store.TryGet(
                        player.GameplayEntityId,
                        out GameplayComponentBuffSetComponent stayedSet),
                    Is.True);
                Assert.That(
                    stayedSet.TryGet(
                        CharacterTestPoisonIds.PoisonedBuffId,
                        out _),
                    Is.True);
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.TryCapture(
                        cache.SensorRows[0].SourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            stayedSnapshot),
                    Is.True);
                Assert.That(
                    stayedSnapshot.Occurrence.OccurrenceKind,
                    Is.EqualTo(GameplayTriggerOccurrenceKind.Stayed));

                player.SetTransform(
                    new CharacterTestCharacterTransformComponent(
                        20f,
                        1f,
                        0f));
                harness.Tick();

                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.TryCapture(
                        cache.SensorRows[0].SourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            exitedSnapshot),
                    Is.True);
                Assert.That(
                    exitedSnapshot.Occurrence.OccurrenceKind,
                    Is.EqualTo(GameplayTriggerOccurrenceKind.Exited));
                bool hasExitedSet =
                    store.TryGet(
                        player.GameplayEntityId,
                        out GameplayComponentBuffSetComponent exitedSet);
                Assert.That(
                    hasExitedSet
                    && exitedSet.TryGet(
                        CharacterTestPoisonIds.PoisonedBuffId,
                        out _),
                    Is.False);
            }
        }

        [Test]
        public void GameSlice_BadHashAndUnknownSensorSourceFailClosed()
        {
            CharacterTestGameplayTriggerSceneCacheData badHashData =
                CreateCacheDataAndDestroyScene(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    CharacterTestGuardAuraIds.BuffId);
            badHashData.TriggerSourceHash = "mismatch";
            var badHashCache = new CharacterTestGameplayTriggerSceneCache();
            badHashCache.Configure(badHashData);
            using (CharacterTestSliceTestHarness harness =
                   CreateHarness(badHashCache))
            {
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.StartupState,
                    Is.EqualTo(
                        CharacterTestLevelGameplayTriggerStartupState
                            .FailedClosed));
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.StartupFailure,
                    Is.EqualTo(
                        CharacterTestLevelGameplayTriggerStartupFailure
                            .CacheSourceHashMismatch));
                Assert.That(
                    harness.Modules.LevelGameplayTriggers,
                    Is.Null);
            }

            CharacterTestGameplayTriggerSceneCacheData unknownSourceData =
                CreateCacheDataAndDestroyScene(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    CharacterTestGuardAuraIds.BuffId);
            CharacterTestGameplayTriggerSensorCacheRow valid =
                unknownSourceData.SensorRows[0];
            FixVector3 min = valid.Min;
            FixVector3 max = valid.Max;
            unknownSourceData.SensorRows = new[]
            {
                new CharacterTestGameplayTriggerSensorCacheRow(
                    sourceBindingId: valid.SourceBindingId + 1000,
                    valid.RegionStableId,
                    valid.ProviderId,
                    valid.QueryId,
                    valid.LayerMask,
                    valid.MaxQueryHits,
                    valid.MaxMappedSubjects,
                    in min,
                    in max)
            };
            var unknownSourceCache =
                new CharacterTestGameplayTriggerSceneCache();
            unknownSourceCache.Configure(unknownSourceData);
            using (CharacterTestSliceTestHarness harness =
                   CreateHarness(unknownSourceCache))
            {
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.StartupState,
                    Is.EqualTo(
                        CharacterTestLevelGameplayTriggerStartupState
                            .FailedClosed));
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.StartupFailure,
                    Is.EqualTo(
                        CharacterTestLevelGameplayTriggerStartupFailure
                            .SensorCatalogRejected));
                Assert.That(
                    harness.Modules.LevelGameplayTriggers,
                    Is.Null);
            }
        }

        [Test]
        public void GameSlice_StoryTemplateUsesReliableTransferAndReturnsTerminalResult()
        {
            const int storyTriggerId =
                CharacterTestStoryIds.Triggers.LevelStartRegion;
            CharacterTestGameplayTriggerSceneCache cache =
                CreateCache(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId,
                    storyTriggerId);
            using (CharacterTestSliceTestHarness harness =
                   CreateHarness(
                       cache,
                       CharacterTestStoryFixture.CreateBootstrapContent()))
            {
                harness.SpawnLocalPlayer(CreatePlayerSpawn());
                harness.Tick(4);

                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.TryCapture(
                        cache.SensorRows[0].SourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            snapshot),
                    Is.True);
                Assert.That(snapshot.HasActionDispatch, Is.True);
                Assert.That(snapshot.HasActionResult, Is.True);
                Assert.That(
                    snapshot.ActionResult.Status,
                    Is.EqualTo(
                        GameplayTriggerRuntimeEventStatus.Succeeded));
                Assert.That(
                    snapshot.ActionResult.ReasonCode,
                    Is.Zero);
            }
        }

        [Test]
        public void PlayerHpThreshold_CacheAndRuntimeUseCompleteDerivedScalar()
        {
            CharacterTestGameplayTriggerSceneCache cache =
                CreatePlayerHpThresholdCache();
            Assert.That(cache.SensorRowCount, Is.Zero);
            Assert.That(cache.AttributeThresholdRowCount, Is.EqualTo(1));
            Assert.That(cache.SensorCatalogHash, Is.Zero);
            Assert.That(cache.AttributeThresholdCatalogHash, Is.Not.Zero);
            Assert.That(cache.CombinedCatalogHash, Is.Not.Zero);

            using (CharacterTestSliceTestHarness harness =
                   CreateHarness(
                       cache,
                       CharacterTestStoryFixture.CreateBootstrapContent()))
            {
                CharacterTestCharacterEntity player =
                    harness.SpawnLocalPlayer(CreatePlayerSpawn());
                harness.Tick(2);
                int sourceBindingId =
                    cache.AttributeThresholdRows[0].SourceBindingId;
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.TryCapture(
                        sourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            initial),
                    Is.True);
                Assert.That(initial.HasAttributeThresholdResult, Is.True);
                Assert.That(
                    initial.AttributeThresholdResult.SampledValue,
                    Is.EqualTo(
                        CharacterTestCharacterAttributeIds.DefaultMaxHp));
                Assert.That(initial.HasOccurrence, Is.False);

                harness.Modules.CharacterRuntime.World.SetCurrentAttribute(
                    player.GameplayEntityId,
                    CharacterTestCharacterAttributeIds.Hp,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .DefaultPlayerHpEnterThreshold);
                harness.Tick(1);
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.TryCapture(
                        sourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            entered),
                    Is.True);
                Assert.That(entered.HasOccurrence, Is.True);
                Assert.That(
                    entered.Occurrence.OccurrenceKind,
                    Is.EqualTo(GameplayTriggerOccurrenceKind.Entered));
                harness.Tick(2);
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.TryCapture(
                        sourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            completed),
                    Is.True);
                Assert.That(completed.HasActionResult, Is.True);
                Assert.That(
                    completed.ActionResult.Status,
                    Is.EqualTo(
                        GameplayTriggerRuntimeEventStatus.Succeeded));
                long firstActivationId =
                    completed.ActionDispatch.ActivationId;

                harness.Modules.CharacterRuntime.World.SetCurrentAttribute(
                    player.GameplayEntityId,
                    CharacterTestCharacterAttributeIds.Hp,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .DefaultPlayerHpExitThreshold + 1);
                harness.Tick(2);
                harness.Modules.CharacterRuntime.World.SetCurrentAttribute(
                    player.GameplayEntityId,
                    CharacterTestCharacterAttributeIds.Hp,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .DefaultPlayerHpEnterThreshold);
                harness.Tick(1);
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.TryCapture(
                        sourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            reentered),
                    Is.True);
                Assert.That(
                    reentered.Occurrence.OccurrenceKind,
                    Is.EqualTo(GameplayTriggerOccurrenceKind.Entered));
                harness.Tick(2);
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.TryCapture(
                        sourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            recompleted),
                    Is.True);
                Assert.That(
                    recompleted.ActionDispatch.ActivationId,
                    Is.GreaterThan(firstActivationId));
            }
        }

        [Test]
        public void PlayerHpThreshold_RestoreResamplesAuthoritativeAttribute()
        {
            CharacterTestGameplayTriggerSceneCache cache =
                CreatePlayerHpThresholdCache();
            using (CharacterTestSliceTestHarness harness =
                   CreateHarness(
                       cache,
                       CharacterTestStoryFixture.CreateBootstrapContent()))
            {
                CharacterTestCharacterEntity player =
                    harness.SpawnLocalPlayer(CreatePlayerSpawn());
                harness.Tick(2);
                CharacterTestLevelGameplayTriggerRuntimeComposition
                    composition =
                        harness.Modules.LevelGameplayTriggers;
                RuntimeSaveStateCoordinatorResult<RuntimeSaveState> capture =
                    CaptureEventually(harness, composition);
                Assert.That(capture.Success, Is.True);

                harness.Modules.CharacterRuntime.World.SetCurrentAttribute(
                    player.GameplayEntityId,
                    CharacterTestCharacterAttributeIds.Hp,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .DefaultPlayerHpEnterThreshold);
                harness.Tick(2);
                RuntimeSaveStateCoordinatorResult<bool> restore =
                    composition.RestoreCheckpoint(capture.Value);
                Assert.That(
                    restore.Success,
                    Is.True,
                    restore.FirstError?.ToString());

                harness.Tick(1);
                int sourceBindingId =
                    cache.AttributeThresholdRows[0].SourceBindingId;
                Assert.That(
                    harness.Slice.LevelGameplayTriggerStatus.TryCapture(
                        sourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            restored),
                    Is.True);
                Assert.That(
                    restored.AttributeThresholdResult.SampledValue,
                    Is.EqualTo(
                        CharacterTestCharacterAttributeIds.DefaultMaxHp));
                Assert.That(
                    restored.HasOccurrence,
                    Is.False,
                    "Restoring the authoritative attribute state must not replay a synthetic low-HP sample.");
            }
        }

        [Test]
        public void ActionBootstrap_IncompleteStoryMappingIsRejectedBeforeRuntime()
        {
            CharacterTestGameplayTriggerSceneCacheData data =
                CreateCacheDataAndDestroyScene(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId,
                    payload0: 1);
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(
                    data.CanonicalTriggerEnvelopeJson);
            Assert.That(
                CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    envelope,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument
                        trigger,
                    out bool present,
                    out LevelWorldObjectAuthoringValidationReport report),
                Is.True,
                JoinIssues(report));
            Assert.That(present, Is.True);
            GameplayTriggerCompileResult compile =
                CharacterTestLevelGameplayTriggerAuthoringAdapter
                    .CompileRuntimeCache(
                        trigger,
                        data.TriggerCatalogHash,
                        out report);
            Assert.That(compile?.Succeeded, Is.True, JoinIssues(report));

            Assert.That(
                CharacterTestLevelGameplayTriggerRuntimeComposition
                    .TryCreateActionBootstrap(
                        compile.CompiledCatalog,
                        new GameplayBuffRequestBuffer(4, 4),
                        includeStory: false,
                        out _,
                        out string message),
                Is.False);
            StringAssert.Contains("no action bootstrap", message);
        }

        [Test]
        public void CheckpointRestoreAndEquivalentReplayPreserveStateAndHash()
        {
            CharacterTestGameplayTriggerSceneCache firstCache =
                CreateCache(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    CharacterTestGuardAuraIds.BuffId);
            long replayHash;
            using (CharacterTestSliceTestHarness first =
                   CreateHarness(firstCache))
            {
                first.SpawnLocalPlayer(
                    CreatePlayerSpawn());
                first.Tick(2);

                CharacterTestLevelGameplayTriggerRuntimeComposition
                    composition =
                        first.Modules.LevelGameplayTriggers;
                RuntimeSaveStateCoordinatorResult<RuntimeSaveState> capture =
                    CaptureEventually(first, composition);
                Assert.That(
                    capture.Success,
                    Is.True,
                    capture.FirstError?.ToString());
                var hashFrame = new RuntimeFrame(900);
                long capturedHash =
                    composition.ComputeResultHash(hashFrame);

                first.Tick(2);
                RuntimeSaveStateCoordinatorResult<bool> restore =
                    composition.RestoreCheckpoint(capture.Value);
                Assert.That(
                    restore.Success,
                    Is.True,
                    restore.FirstError?.ToString());
                Assert.That(
                    composition.ComputeResultHash(hashFrame),
                    Is.EqualTo(capturedHash));

                first.Tick();
                Assert.That(
                    first.Slice.LevelGameplayTriggerStatus.TryCapture(
                        firstCache.SensorRows[0].SourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            afterRestore),
                    Is.True);
                Assert.That(afterRestore.HasOccurrence, Is.True);
                Assert.That(
                    afterRestore.Occurrence.OccurrenceKind,
                    Is.EqualTo(GameplayTriggerOccurrenceKind.Stayed),
                    "Restore must continue occupancy instead of duplicating Entered.");
                replayHash =
                    composition.ComputeResultHash(
                        new RuntimeFrame(901));
            }

            CharacterTestGameplayTriggerSceneCache replayCache =
                CreateCache(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    CharacterTestGuardAuraIds.BuffId);
            using (CharacterTestSliceTestHarness replay =
                   CreateHarness(replayCache))
            {
                replay.SpawnLocalPlayer(
                    CreatePlayerSpawn());
                replay.Tick(2);
                CharacterTestLevelGameplayTriggerRuntimeComposition
                    replayComposition =
                        replay.Modules.LevelGameplayTriggers;
                RuntimeSaveStateCoordinatorResult<RuntimeSaveState>
                    replayCapture =
                        CaptureEventually(replay, replayComposition);
                Assert.That(replayCapture.Success, Is.True);
                replay.Tick(2);
                Assert.That(
                    replayComposition.RestoreCheckpoint(
                        replayCapture.Value).Success,
                    Is.True);
                replay.Tick();
                Assert.That(
                    replayComposition.ComputeResultHash(
                        new RuntimeFrame(901)),
                    Is.EqualTo(replayHash));
            }
        }

        [Test]
        public void GameplayModule_SteadyStateTriggerTickDoesNotAllocate()
        {
            CharacterTestGameplayTriggerSceneCache cache =
                CreateCache(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    CharacterTestGuardAuraIds.BuffId);
            using (CharacterTestSliceTestHarness harness =
                   CreateHarness(cache))
            {
                harness.SpawnLocalPlayer(CreatePlayerSpawn());
                harness.Tick(4);
                CharacterTestLevelGameplayTriggerRuntimeComposition
                    composition =
                        harness.Modules.LevelGameplayTriggers;

                const long startFrame = 1000;
                for (int i = 0; i < 8; i++)
                {
                    composition.GameplayModule.Tick(
                        CreateTickContext(startFrame + i));
                }

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++)
                {
                    composition.GameplayModule.Tick(
                        CreateTickContext(startFrame + 8 + i));
                }
                long allocated =
                    GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(
                    allocated,
                    Is.Zero,
                    "CharacterTest P2.4 Trigger steady-state Tick allocated.");
            }
        }

        private static RuntimeTickContext CreateTickContext(long frame)
        {
            return new RuntimeTickContext(
                frame,
                0d,
                0d,
                RuntimeTickStage.Simulation);
        }

        private static RuntimeSaveStateCoordinatorResult<RuntimeSaveState>
            CaptureEventually(
                CharacterTestSliceTestHarness harness,
                CharacterTestLevelGameplayTriggerRuntimeComposition
                    composition)
        {
            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result =
                composition.CaptureCheckpoint();
            for (int i = 0; i < 4 && !result.Success; i++)
            {
                harness.Tick();
                result = composition.CaptureCheckpoint();
            }
            return result;
        }

        private static CharacterTestSliceTestHarness CreateHarness(
            CharacterTestGameplayTriggerSceneCache cache,
            CharacterTestStoryContent storyContent = null,
            IReadOnlyList<CharacterTestCharacterSpawnSpec>
                startupCharacterSpawnSpecs = null)
        {
            return new CharacterTestSliceTestHarness(
                new CharacterTestSliceOptions
                {
                    Logger = NullRuntimeLogger.Instance,
                    StoryContent = storyContent,
                    PhysicsRuntime = CreatePhysicsRuntime(),
                    CreateCharacterViews = false,
                    EnableLocalInputCommands = false,
                    SuppressMissingStartupSpawnWarning = true,
                    StartupCharacterSpawnSpecs =
                        startupCharacterSpawnSpecs,
                    GameplayTriggerSceneCache = cache
                });
        }

        private static CharacterTestCharacterSpawnSpec CreatePlayerSpawn()
        {
            return new CharacterTestCharacterSpawnSpec(
                CharacterTestCharacterDefinitionRegistry
                    .DefaultPlayableCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0f);
        }

        private static CharacterTestCharacterPhysicsRuntime
            CreatePhysicsRuntime()
        {
            return new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Zero,
                staticColliderCount: 1,
                sourceDescription: "P2.4 Trigger integration test");
        }

        private static CharacterTestGameplayTriggerSceneCache CreateCache(
            string templateId,
            long payload0)
        {
            CharacterTestGameplayTriggerSceneCacheData data =
                CreateCacheDataAndDestroyScene(templateId, payload0);
            var cache = new CharacterTestGameplayTriggerSceneCache();
            cache.Configure(data);
            return cache;
        }

        private static CharacterTestGameplayTriggerSceneCache
            CreatePlayerHpThresholdCache()
        {
            LevelWorldObjectAuthoringDocument world =
                new LevelWorldObjectAuthoringDocument
                {
                    LevelId =
                        CharacterTestWorldObjectAuthoringAdapter
                            .DefaultLevelId,
                    ScenePath =
                        CharacterTestWorldObjectAuthoringAdapter
                            .DefaultScenePath,
                    RootPath = "CharacterTestTriggerRuntimeTestRoot"
                };
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            trigger.AddPlayerHpLowStoryTemplate(
                CharacterTestStoryIds.Triggers.LevelStartRegion);
            MxLevelAuthoringDocument envelope =
                CharacterTestWorldObjectAuthoringAdapter
                    .CreateEnvelopeFromDocument(world);
            envelope =
                CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                    envelope,
                    trigger);
            Assert.That(
                CharacterTestGameplayTriggerSceneCacheCompiler.TryCompile(
                    envelope,
                    world,
                    sceneRoot: null,
                    definitionProvider: null,
                    out CharacterTestGameplayTriggerSceneCacheData data,
                    out LevelWorldObjectAuthoringValidationReport report),
                Is.True,
                JoinIssues(report));
            var cache = new CharacterTestGameplayTriggerSceneCache();
            cache.Configure(data);
            return cache;
        }

        private static CharacterTestGameplayTriggerSceneCacheData
            CreateCacheDataAndDestroyScene(
                string templateId,
                long payload0)
        {
            CharacterTestGameplayTriggerSceneCacheData data =
                CreateCacheData(
                    templateId,
                    payload0,
                    out GameObject root);
            Object.DestroyImmediate(root);
            return data;
        }

        private static CharacterTestGameplayTriggerSceneCacheData
            CreateCacheData(
                string templateId,
                long payload0,
                out GameObject root)
        {
            root = new GameObject("CharacterTestTriggerRuntimeTestRoot");
            var regions = new GameObject("Regions");
            regions.transform.SetParent(root.transform, false);
            var region = new GameObject("TriggerRegion");
            region.transform.SetParent(regions.transform, false);
            region.transform.position = new Vector3(0f, 1f, 0f);
            region.transform.localScale = new Vector3(10f, 6f, 10f);

            LevelWorldObjectAuthoringDocument world =
                CreateWorldDocument();
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            trigger.AddTemplate(
                templateId,
                RegionStableId,
                payload0);
            MxLevelAuthoringDocument envelope =
                CharacterTestWorldObjectAuthoringAdapter
                    .CreateEnvelopeFromDocument(world);
            envelope =
                CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                    envelope,
                    trigger);

            Assert.That(
                CharacterTestGameplayTriggerSceneCacheCompiler.TryCompile(
                    envelope,
                    world,
                    root.transform,
                    CharacterTestWorldObjectDefinitionCatalog
                        .CreateCharacterTestSceneCatalog(),
                    out CharacterTestGameplayTriggerSceneCacheData data,
                    out LevelWorldObjectAuthoringValidationReport report),
                Is.True,
                JoinIssues(report));
            return data;
        }

        private static LevelWorldObjectAuthoringDocument
            CreateWorldDocument()
        {
            return new LevelWorldObjectAuthoringDocument
            {
                LevelId =
                    CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath =
                    CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterTestTriggerRuntimeTestRoot",
                Regions = new List<LevelWorldObjectRegionDefinition>
                {
                    new LevelWorldObjectRegionDefinition
                    {
                        Enabled = true,
                        DefinitionId =
                            CharacterTestWorldObjectDefinitionIds
                                .RegionStartArea,
                        StableId = RegionStableId,
                        RegionKind =
                            LevelWorldObjectRegionKind.TriggerRegion,
                        ScaleSource =
                            LevelWorldObjectScaleSource.SceneTransform,
                        TransformRef =
                            LevelTransformReference.FromPath(
                                "Regions/TriggerRegion")
                    }
                }
            };
        }

        private static FixVector3 ToFixVector(float x, float y, float z)
        {
            return new FixVector3(
                ToFix(x),
                ToFix(y),
                ToFix(z));
        }

        private static Fix64 ToFix(float value)
        {
            return Fix64.FromRaw(
                (long)Math.Round(value * Fix64.Scale));
        }

        private static string JoinIssues(
            LevelWorldObjectAuthoringValidationReport report)
        {
            if (report?.Issues == null || report.Issues.Count == 0)
                return string.Empty;
            var lines = new string[report.Issues.Count];
            for (int i = 0; i < report.Issues.Count; i++)
            {
                lines[i] = report.Issues[i].Code
                    + ": "
                    + report.Issues[i].Message;
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
