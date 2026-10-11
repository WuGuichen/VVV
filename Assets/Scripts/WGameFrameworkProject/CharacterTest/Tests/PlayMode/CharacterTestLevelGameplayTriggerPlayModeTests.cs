using System.Collections;
using System.Collections.Generic;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.CombatBridge;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class CharacterTestLevelGameplayTriggerPlayModeTests
    {
        [UnityTest]
        public IEnumerator ColliderFreeRegionMaintainsAndRemovesBuffThroughTypedCombatSensorRuntime()
        {
            GameObject root =
                new GameObject("CharacterTestTriggerPlayModeRoot");
            var region = new GameObject("TriggerRegion");
            region.transform.SetParent(root.transform, false);
            region.transform.position = new Vector3(0f, 1f, 0f);
            region.transform.localScale = new Vector3(8f, 6f, 8f);

            GameSlice slice = null;
            try
            {
                CharacterTestGameplayTriggerSceneCache cache =
                    CreateCache(
                        root,
                        region,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    CharacterTestGuardAuraIds.BuffId,
                    CharacterTestGuardAuraIds.AuraId);
                slice = new GameSlice(
                    new CharacterTestSliceOptions
                    {
                        Logger = NullRuntimeLogger.Instance,
                        PhysicsRuntime = CreatePhysicsRuntime(),
                        CreateCharacterViews = false,
                        EnableLocalInputCommands = false,
                        SuppressMissingStartupSpawnWarning = true,
                        StartupCharacterSpawnSpecs =
                            new CharacterTestCharacterSpawnSpec[0],
                        GameplayTriggerSceneCache = cache
                    });
                RuntimeCommandValidationResult spawn =
                    slice.Services.EnqueueRuntimeCommand(
                        CharacterTestRuntimeCommandFactory.SpawnCharacter(
                            slice.CurrentFrame,
                            CharacterTestRuntimeCommandSources.System,
                            new CharacterTestCharacterSpawnSpec(
                                CharacterTestCharacterDefinitionRegistry
                                    .DefaultPlayableCharacterId,
                                teamId: 1,
                                x: 0f,
                                y: 1f,
                                z: 0f),
                            traceId: new RuntimeTraceIdentity(9004, CharacterTestRuntimeCommandSources.System, slice.CurrentFrame.Value, 1)));
                Assert.That(
                    spawn.Success,
                    Is.True,
                    spawn.Error.Message);

                for (int i = 0; i < 5; i++)
                {
                    slice.Tick(0d);
                    yield return null;
                }

                Assert.That(
                    root.GetComponentInChildren<Collider>(),
                    Is.Null);
                Assert.That(
                    slice.LevelGameplayTriggerStatus.StartupState,
                    Is.EqualTo(
                        CharacterTestLevelGameplayTriggerStartupState.Ready));
                Assert.That(
                    slice.LevelGameplayTriggerStatus.TryCapture(
                        cache.SensorRows[0].SourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            snapshot),
                    Is.True);
                Assert.That(snapshot.HasSensorResult, Is.True);
                Assert.That(
                    snapshot.SensorResult.Status,
                    Is.EqualTo(
                        GameplayTriggerCombatSensorTickStatus.Submitted));
                Assert.That(
                    snapshot.SensorResult.QueryHitCount,
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(
                    snapshot.SensorResult.SubmittedSubjectCount,
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(snapshot.HasOccurrence, Is.True);
                Assert.That(
                    snapshot.Occurrence.OccurrenceKind,
                    Is.EqualTo(GameplayTriggerOccurrenceKind.Entered)
                        .Or.EqualTo(
                            GameplayTriggerOccurrenceKind.Stayed));
                Assert.That(snapshot.HasActionDispatch, Is.True);
                Assert.That(
                    snapshot.ActionDispatch.ActionPayload0,
                    Is.EqualTo(CharacterTestGuardAuraIds.BuffId));
                Assert.That(
                    snapshot.ActionDispatch.ActionPayload1,
                    Is.EqualTo(
                        CharacterTestGuardAuraIds.AuraId));
                Assert.That(snapshot.HasActionResult, Is.True);
                Assert.That(
                    snapshot.ActionResult.Status,
                    Is.EqualTo(
                        GameplayTriggerRuntimeEventStatus.Succeeded));
                Assert.That(slice.CharacterEntities, Has.Count.EqualTo(1));
                CharacterTestCharacterEntity player =
                    slice.CharacterEntities[0];
                GameplayComponentWorld componentWorld =
                    slice.Services.Modules.CharacterRuntime.World
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
                    Is.True,
                    "player="
                    + player.GameplayEntityId
                    + ", subject="
                    + snapshot.ActionDispatch.Subject
                    + ", target="
                    + snapshot.ActionDispatch.Target
                    + ", buffStoreCount="
                    + store.Count);
                Assert.That(
                    buffSet.TryGet(
                        CharacterTestGuardAuraIds.BuffId,
                        out _),
                    Is.True);

                player.SetTransform(
                    new CharacterTestCharacterTransformComponent(
                        x: 20f,
                        y: player.Transform.Y,
                        z: player.Transform.Z,
                        yawDegrees: player.Transform.YawDegrees));
                slice.Tick(0d);
                yield return null;

                Assert.That(
                    slice.LevelGameplayTriggerStatus.TryCapture(
                        cache.SensorRows[0].SourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            exitedSnapshot),
                    Is.True);
                Assert.That(exitedSnapshot.HasOccurrence, Is.True);
                Assert.That(
                    exitedSnapshot.Occurrence.OccurrenceKind,
                    Is.EqualTo(GameplayTriggerOccurrenceKind.Exited));
                Assert.That(exitedSnapshot.HasActionDispatch, Is.True);
                Assert.That(
                    exitedSnapshot.ActionDispatch.ActionPayload0,
                    Is.EqualTo(CharacterTestGuardAuraIds.BuffId));
                Assert.That(exitedSnapshot.HasActionResult, Is.True);
                Assert.That(
                    exitedSnapshot.ActionResult.Status,
                    Is.EqualTo(
                        GameplayTriggerRuntimeEventStatus.Succeeded));
                bool hasBuffSet = store.TryGet(
                    player.GameplayEntityId,
                    out buffSet);
                Assert.That(
                    hasBuffSet
                    && buffSet.TryGet(
                        CharacterTestGuardAuraIds.BuffId,
                        out _),
                    Is.False);
            }
            finally
            {
                slice?.Dispose();
                Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator
            ColliderFreeRegionRaisesFormalStoryThroughTypedCombatSensorRuntime()
        {
            GameObject root =
                new GameObject("CharacterTestStoryTriggerPlayModeRoot");
            var region = new GameObject("TriggerRegion");
            region.transform.SetParent(root.transform, false);
            region.transform.position = new Vector3(0f, 1f, 0f);
            region.transform.localScale = new Vector3(8f, 6f, 8f);

            GameSlice slice = null;
            try
            {
                CharacterTestGameplayTriggerSceneCache cache =
                    CreateCache(
                        root,
                        region,
                        CharacterTestLevelGameplayTriggerAuthoringDocument
                            .StoryRaiseTriggerEnterOnceTemplateId,
                        CharacterTestStoryIds.Triggers.LevelStartRegion,
                        payload1: 0L);
                slice = new GameSlice(
                    new CharacterTestSliceOptions
                    {
                        Logger = NullRuntimeLogger.Instance,
                        PhysicsRuntime = CreatePhysicsRuntime(),
                        CreateCharacterViews = false,
                        EnableLocalInputCommands = false,
                        SuppressMissingStartupSpawnWarning = true,
                        StartupCharacterSpawnSpecs =
                            new CharacterTestCharacterSpawnSpec[0],
                        GameplayTriggerSceneCache = cache,
                        StoryContent =
                            CharacterTestStoryFixture
                                .CreateBootstrapContent()
                    });
                for (int i = 0; i < 4; i++)
                {
                    slice.Tick(0d);
                    yield return null;
                }
                int nextBeatBeforeRegionEntry =
                    slice.Services.StorySnapshot.NextBeatInstanceId;

                RuntimeCommandValidationResult spawn =
                    slice.Services.EnqueueRuntimeCommand(
                        CharacterTestRuntimeCommandFactory.SpawnCharacter(
                            slice.CurrentFrame,
                            CharacterTestRuntimeCommandSources.System,
                            new CharacterTestCharacterSpawnSpec(
                                CharacterTestCharacterDefinitionRegistry
                                    .DefaultPlayableCharacterId,
                                teamId: 1,
                                x: 0f,
                                y: 1f,
                                z: 0f),
                            traceId: new RuntimeTraceIdentity(9004, CharacterTestRuntimeCommandSources.System, slice.CurrentFrame.Value, 2)));
                Assert.That(
                    spawn.Success,
                    Is.True,
                    spawn.Error.Message);

                for (int i = 0; i < 8; i++)
                {
                    slice.Tick(0d);
                    yield return null;
                }

                Assert.That(
                    root.GetComponentInChildren<Collider>(),
                    Is.Null);
                Assert.That(
                    slice.LevelGameplayTriggerStatus.TryCapture(
                        cache.SensorRows[0].SourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            snapshot),
                    Is.True);
                Assert.That(snapshot.HasOccurrence, Is.True);
                Assert.That(snapshot.HasActionDispatch, Is.True);
                Assert.That(
                    snapshot.ActionDispatch.ActionPayload0,
                    Is.EqualTo(
                        CharacterTestStoryIds.Triggers.LevelStartRegion));
                Assert.That(snapshot.HasActionResult, Is.True);
                Assert.That(
                    snapshot.ActionResult.Status,
                    Is.EqualTo(
                        GameplayTriggerRuntimeEventStatus.Succeeded));
                Assert.That(
                    slice.Services.StorySnapshot.NextBeatInstanceId,
                    Is.GreaterThan(nextBeatBeforeRegionEntry),
                    "Formal level-start Story trigger did not create a beat.");
            }
            finally
            {
                slice?.Dispose();
                Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator
            LocalPlayerHpThresholdRaisesStoryAndRearmsAfterRecovery()
        {
            CharacterTestGameplayTriggerSceneCache cache =
                CreatePlayerHpThresholdCache();
            GameSlice slice = null;
            try
            {
                slice = new GameSlice(
                    new CharacterTestSliceOptions
                    {
                        Logger = NullRuntimeLogger.Instance,
                        CreateCharacterViews = false,
                        EnableLocalInputCommands = false,
                        SuppressMissingStartupSpawnWarning = true,
                        StartupCharacterSpawnSpecs =
                            new CharacterTestCharacterSpawnSpec[0],
                        GameplayTriggerSceneCache = cache,
                        StoryContent =
                            CharacterTestStoryFixture
                                .CreateBootstrapContent()
                    });
                RuntimeCommandValidationResult spawn =
                    slice.Services.EnqueueRuntimeCommand(
                        CharacterTestRuntimeCommandFactory.SpawnCharacter(
                            slice.CurrentFrame,
                            CharacterTestRuntimeCommandSources.System,
                            new CharacterTestCharacterSpawnSpec(
                                CharacterTestCharacterDefinitionRegistry
                                    .DefaultPlayableCharacterId,
                                teamId: 1,
                                x: 0f,
                                y: 1f,
                                z: 0f),
                            traceId: new RuntimeTraceIdentity(9004, CharacterTestRuntimeCommandSources.System, slice.CurrentFrame.Value, 3)));
                Assert.That(
                    spawn.Success,
                    Is.True,
                    spawn.Error.Message);
                for (int i = 0; i < 3; i++)
                {
                    slice.Tick(0d);
                    yield return null;
                }

                CharacterTestCharacterEntity player =
                    slice.CharacterEntities[0];
                int sourceBindingId =
                    cache.AttributeThresholdRows[0].SourceBindingId;
                Assert.That(
                    slice.LevelGameplayTriggerStatus.TryCapture(
                        sourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            primed),
                    Is.True);
                Assert.That(
                    primed.AttributeThresholdResult.SampledValue,
                    Is.EqualTo(
                        CharacterTestCharacterAttributeIds.DefaultMaxHp));
                Assert.That(primed.HasOccurrence, Is.False);

                slice.Services.Modules.CharacterRuntime.World
                    .SetCurrentAttribute(
                        player.GameplayEntityId,
                        CharacterTestCharacterAttributeIds.Hp,
                        CharacterTestLevelGameplayTriggerAuthoringDocument
                            .DefaultPlayerHpEnterThreshold);
                for (int i = 0; i < 3; i++)
                {
                    slice.Tick(0d);
                    yield return null;
                }
                Assert.That(
                    slice.LevelGameplayTriggerStatus.TryCapture(
                        sourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            first),
                    Is.True);
                Assert.That(first.HasActionResult, Is.True);
                Assert.That(
                    first.ActionResult.Status,
                    Is.EqualTo(
                        GameplayTriggerRuntimeEventStatus.Succeeded));
                long firstActivationId =
                    first.ActionDispatch.ActivationId;

                slice.Services.Modules.CharacterRuntime.World
                    .SetCurrentAttribute(
                        player.GameplayEntityId,
                        CharacterTestCharacterAttributeIds.Hp,
                        CharacterTestLevelGameplayTriggerAuthoringDocument
                            .DefaultPlayerHpExitThreshold + 1);
                slice.Tick(0d);
                yield return null;
                slice.Services.Modules.CharacterRuntime.World
                    .SetCurrentAttribute(
                        player.GameplayEntityId,
                        CharacterTestCharacterAttributeIds.Hp,
                        CharacterTestLevelGameplayTriggerAuthoringDocument
                            .DefaultPlayerHpEnterThreshold);
                for (int i = 0; i < 3; i++)
                {
                    slice.Tick(0d);
                    yield return null;
                }
                Assert.That(
                    slice.LevelGameplayTriggerStatus.TryCapture(
                        sourceBindingId,
                        out CharacterTestLevelGameplayTriggerRuntimeSnapshot
                            second),
                    Is.True);
                Assert.That(
                    second.ActionDispatch.ActivationId,
                    Is.GreaterThan(firstActivationId));
            }
            finally
            {
                slice?.Dispose();
            }

            yield return null;
        }

        private static CharacterTestGameplayTriggerSceneCache
            CreateCache(
                GameObject root,
                GameObject region,
                string templateId,
                long payload0,
                long payload1)
        {
            const string regionStableId = "region.playmode.test";
            var world = new LevelWorldObjectAuthoringDocument
            {
                LevelId =
                    CharacterTestWorldObjectAuthoringAdapter
                        .DefaultLevelId,
                ScenePath =
                    CharacterTestWorldObjectAuthoringAdapter
                        .DefaultScenePath,
                RootPath = root.name,
                Regions =
                    new List<LevelWorldObjectRegionDefinition>
                    {
                        new LevelWorldObjectRegionDefinition
                        {
                            Enabled = true,
                            DefinitionId =
                                CharacterTestWorldObjectDefinitionIds
                                    .RegionStartArea,
                            StableId = regionStableId,
                            RegionKind =
                                LevelWorldObjectRegionKind
                                    .TriggerRegion,
                            ScaleSource =
                                LevelWorldObjectScaleSource
                                    .SceneTransform,
                            TransformRef =
                                LevelTransformReference.FromPath(
                                    region.name)
                        }
                    }
            };
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            trigger.AddTemplate(
                templateId,
                regionStableId,
                payload0,
                payload1);
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
                report?.Issues.Count > 0
                    ? report.Issues[0].Code
                        + ": "
                        + report.Issues[0].Message
                    : string.Empty);

            var cache =
                new CharacterTestGameplayTriggerSceneCache();
            cache.Configure(data);
            return cache;
        }

        private static CharacterTestGameplayTriggerSceneCache
            CreatePlayerHpThresholdCache()
        {
            var world = new LevelWorldObjectAuthoringDocument
            {
                LevelId =
                    CharacterTestWorldObjectAuthoringAdapter
                        .DefaultLevelId,
                ScenePath =
                    CharacterTestWorldObjectAuthoringAdapter
                        .DefaultScenePath,
                RootPath = "CharacterTestTriggerPlayModeRoot"
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
                    CharacterTestWorldObjectDefinitionCatalog
                        .CreateCharacterTestSceneCatalog(),
                    out CharacterTestGameplayTriggerSceneCacheData data,
                    out LevelWorldObjectAuthoringValidationReport report),
                Is.True,
                report?.Issues.Count > 0
                    ? report.Issues[0].Code
                        + ": "
                        + report.Issues[0].Message
                    : string.Empty);

            var cache =
                new CharacterTestGameplayTriggerSceneCache();
            cache.Configure(data);
            return cache;
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
                sourceDescription: "P2.4 PlayMode smoke");
        }
    }
}
