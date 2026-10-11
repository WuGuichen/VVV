using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MxFramework.Audio;
using MxFramework.Camera;
using MxFramework.Camera.Unity;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Config;
using MxFramework.Core.Math;
using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Rendering;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Runtime.Unity;
using MxFramework.Settings;
using MxFramework.Settings.Input;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.UI;
using MxFramework.UI.ConfigBridge;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestIntegrationContractTests
    {

[Test]
        public void CharacterTestInteractionHistory_RecordsSortedEventsAndStableHash()
        {
            var history = new CharacterTestInteractionHistory();
            RuntimeFrame frame = new RuntimeFrame(12);
            CharacterTestInteractionEvent later = CreateTestInteraction(
                frame,
                1201902,
                sourceOrder: 20);
            CharacterTestInteractionEvent earlier = CreateTestInteraction(
                frame,
                1201901,
                sourceOrder: 10);

            history.BeginFrame(frame);
            Assert.IsTrue(history.Record(later));
            Assert.IsTrue(history.Record(earlier));
            Assert.IsFalse(history.Record(earlier));

            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(2, history.LastFrameCount);
            Assert.AreEqual(1201901, history.LastFrameEvents[0].StableId);
            Assert.AreEqual(1201902, history.LastFrameEvents[1].StableId);

            var reversed = new CharacterTestInteractionHistory();
            reversed.BeginFrame(frame);
            Assert.IsTrue(reversed.Record(earlier));
            Assert.IsTrue(reversed.Record(later));
            Assert.AreEqual(history.ComputeStableHash(), reversed.ComputeStableHash());
        }

[Test]
        public void CharacterTestInteractionHistory_RecordDoesNotAllocateAfterWarmup()
        {
            var history = new CharacterTestInteractionHistory(maxHistoryFrames: 256);
            var interactions = new CharacterTestInteractionEvent[64];
            for (int i = 0; i < interactions.Length; i++)
            {
                interactions[i] = CreateTestInteraction(
                    new RuntimeFrame(1000 + i),
                    1201903,
                    sourceOrder: i);
            }

            for (int i = 0; i < 8; i++)
            {
                history.BeginFrame(interactions[i].Frame);
                history.Record(interactions[i]);
            }

            history.Clear();
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int recorded = 0;
            for (int i = 0; i < interactions.Length; i++)
            {
                history.BeginFrame(interactions[i].Frame);
                if (history.Record(interactions[i]))
                    recorded++;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(interactions.Length, recorded);
            Assert.AreEqual(0L, allocatedBytes);
        }

[Test]
        public void CharacterTestInteractionFieldProfiles_ProvideStablePhysicalFieldDefaults()
        {
            CharacterTestInteractionPhysicalField explosion =
                CharacterTestInteractionFieldProfiles.FireballExplosionRadialImpulse(6);
            Assert.AreEqual(CharacterTestInteractionFieldShape.Sphere, explosion.Shape);
            Assert.AreEqual(CharacterTestInteractionForceMode.RadialImpulse, explosion.ForceMode);
            Assert.IsTrue(explosion.Targets(CharacterTestInteractionTargetFilter.DynamicCharacters));
            Assert.IsTrue(explosion.Targets(CharacterTestInteractionTargetFilter.DynamicMovableObjects));
            Assert.IsTrue(explosion.Targets(CharacterTestInteractionTargetFilter.ExcludeSource));
            Assert.AreEqual(1500, explosion.ImpulseSpeedMillimetersPerSecond);
            Assert.AreEqual(
                CharacterTestInteractionFieldProfiles.FireballExplosionMaxSpeedMillimetersPerSecond,
                CharacterTestInteractionFieldProfiles.ResolveFireballExplosionImpulseSpeed(100));
            Assert.AreEqual(
                CharacterTestInteractionFieldProfiles.FireballExplosionMaxSpeedMillimetersPerSecond,
                explosion.MaxSpeedMillimetersPerSecond);

            CharacterTestInteractionPhysicalField meteor =
                CharacterTestInteractionFieldProfiles.MeteorBarrageRadialImpulse(12);
            Assert.AreEqual(CharacterTestInteractionFieldShape.Sphere, meteor.Shape);
            Assert.AreEqual(CharacterTestInteractionForceMode.RadialImpulse, meteor.ForceMode);
            Assert.IsTrue(meteor.Targets(CharacterTestInteractionTargetFilter.DynamicCharacters));
            Assert.IsTrue(meteor.Targets(CharacterTestInteractionTargetFilter.DynamicMovableObjects));
            Assert.IsTrue(meteor.Targets(CharacterTestInteractionTargetFilter.ExcludeSource));
            Assert.AreEqual(3000, meteor.ImpulseSpeedMillimetersPerSecond);
            Assert.AreEqual(
                CharacterTestInteractionFieldProfiles.FireballExplosionMaxSpeedMillimetersPerSecond,
                meteor.MaxSpeedMillimetersPerSecond);

            CharacterTestInteractionPhysicalField tornado =
                CharacterTestInteractionFieldProfiles.TornadoAttractionCapsule();
            Assert.AreEqual(CharacterTestInteractionFieldShape.Capsule, tornado.Shape);
            Assert.AreEqual(CharacterTestInteractionForceMode.AttractionAcceleration, tornado.ForceMode);
            Assert.IsTrue(tornado.Targets(CharacterTestInteractionTargetFilter.DynamicCharacters));
            Assert.IsTrue(tornado.Targets(CharacterTestInteractionTargetFilter.DynamicMovableObjects));
            Assert.IsTrue(tornado.Targets(CharacterTestInteractionTargetFilter.RequireHostile));
            Assert.IsTrue(tornado.Targets(CharacterTestInteractionTargetFilter.ExcludeSource));
            Assert.AreEqual(
                CharacterTestInteractionFieldProfiles.TornadoAttractionAccelerationMillimetersPerSecondSquared,
                tornado.AccelerationMillimetersPerSecondSquared);
            Assert.AreEqual(
                CharacterTestInteractionFieldProfiles.TornadoAttractionMinimumDistanceScaleMilli,
                tornado.LinearFalloffMinimumScaleMilli);
            Assert.AreEqual(
                CharacterTestInteractionFieldProfiles.TornadoAttractionTerminalSpeedMillimetersPerSecond,
                tornado.MaxSpeedMillimetersPerSecond);
            Assert.AreEqual(
                CharacterTestInteractionFieldProfiles.TornadoAttractionCapsuleBottomOffset,
                tornado.CapsuleBottomOffset);
            Assert.AreEqual(
                CharacterTestInteractionFieldProfiles.TornadoAttractionCapsuleTopOffset,
                tornado.CapsuleTopOffset);
        }

[Test]
        public void CharacterTestInteractionEvent_TornadoAttractionConstructionDoesNotAllocateAfterWarmup()
        {
            FixVector3 position = new FixVector3(Fix64.FromInt(1), Fix64.Zero, Fix64.FromInt(2));
            FixVector3 direction = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
            for (int i = 0; i < 8; i++)
            {
                _ = CreateTornadoAttractionInteraction(new RuntimeFrame(i), position, direction);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int intensitySum = 0;
            for (int i = 0; i < 64; i++)
            {
                CharacterTestInteractionEvent interaction =
                    CreateTornadoAttractionInteraction(new RuntimeFrame(100 + i), position, direction);
                intensitySum += interaction.Intensity;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(
                CharacterTestInteractionFieldProfiles.TornadoAttractionAccelerationMillimetersPerSecondSquared * 64,
                intensitySum);
            Assert.AreEqual(0L, allocatedBytes);
        }

[Test]
        public void CharacterTestInteractionPerformanceCounterSource_EmptySnapshotExportsZeroCounters()
        {
            FrameworkPerformanceCounterSnapshot snapshot =
                CharacterTestInteractionPerformanceCounterSource.Capture(
                    0,
                    0,
                    CharacterTestInteractionConsumerDispatchSummary.Empty,
                    CharacterTestInteractionPhysicsSummary.Empty,
                    CharacterTestInteractionVfxSummary.Empty,
                    CharacterTestInteractionAudioSummary.Empty,
                    CharacterTestInteractionRenderingSummary.Empty);

            Assert.IsTrue(snapshot.Enabled);
            Assert.AreEqual(CharacterTestInteractionPerformanceCounterSource.SourceName, snapshot.SourceName);
            Assert.AreEqual(13, snapshot.Count);
            CollectionAssert.AreEqual(
                snapshot.Samples.Select(sample => sample.CounterId).OrderBy(id => id, StringComparer.Ordinal),
                snapshot.Samples.Select(sample => sample.CounterId));
            Assert.IsTrue(snapshot.Samples.All(sample => sample.Cost == FrameworkPerformanceCounterCost.NoAlloc));
            Assert.AreEqual(
                0,
                FindInteractionCounter(
                    snapshot,
                    CharacterTestInteractionPerformanceCounterSource.LastFrameEventsCounterId).Value);
            Assert.AreEqual(
                0,
                FindInteractionCounter(
                    snapshot,
                    CharacterTestInteractionPerformanceCounterSource.PhysicsQueriesCounterId).Value);
        }

[Test]
        public void CharacterTestInteractionPerformanceCounterSource_MapsInteractionSummaries()
        {
            RuntimeFrame frame = new RuntimeFrame(21);
            var dispatcher = new CharacterTestInteractionConsumerDispatchSummary(
                frame,
                registeredConsumerCount: 4,
                executedConsumerCount: 4,
                skippedConsumerCount: 0,
                failedConsumerCount: 0,
                lastConsumerId: CharacterTestInteractionRenderingSystem.ConsumerIdValue);
            var physics = new CharacterTestInteractionPhysicsSummary(
                frame,
                processedFieldCount: 5,
                appliedForceCount: 6,
                characterTargetCount: 7,
                movableTargetCount: 8,
                skippedTargetCount: 9,
                lastTargetEntityId: 2,
                lastMovableNumericId: 501,
                lastSpeedMillimetersPerSecond: 1000,
                lastAccelerationMillimetersPerSecondSquared: 36000,
                lastStableId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0062,
                lastForceMode: CharacterTestInteractionForceMode.AttractionAcceleration,
                lastFieldShape: CharacterTestInteractionFieldShape.Capsule,
                queryCount: 10,
                queryResultCount: 11,
                duplicateBodySkipCount: 12);
            var vfx = new CharacterTestInteractionVfxSummary(
                frame,
                processedInteractionCount: 13,
                vfxSpawnedCount: 14,
                skippedCount: 0,
                lastStableId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0062,
                lastForceMode: CharacterTestInteractionForceMode.RadialImpulse,
                lastFieldShape: CharacterTestInteractionFieldShape.Sphere,
                lastVfxDefinitionId: 1);
            var audio = new CharacterTestInteractionAudioSummary(
                frame,
                processedInteractionCount: 15,
                audioPlayedCount: 16,
                skippedCount: 0,
                lastStableId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0062,
                lastForceMode: CharacterTestInteractionForceMode.RadialImpulse,
                lastFieldShape: CharacterTestInteractionFieldShape.Sphere,
                lastAudioCueKey: CharacterTestAudioCueCatalog.FireballExplosionCueId);
            var rendering = new CharacterTestInteractionRenderingSummary(
                frame,
                processedFieldCount: 17,
                publishedFieldImpulseCount: 18,
                skippedCount: 0,
                lastStableId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0062,
                lastChannelId: CharacterTestInteractionRenderChannels.AttractionCapsule,
                lastForceMode: CharacterTestInteractionForceMode.AttractionAcceleration,
                lastFieldShape: CharacterTestInteractionFieldShape.Capsule,
                lastSubjectSourceId: 2,
                lastRadius: 4.5f,
                lastIntensity: 36f);

            FrameworkPerformanceCounterSnapshot snapshot =
                CharacterTestInteractionPerformanceCounterSource.Capture(
                    lastFrameEventCount: 2,
                    historyEventCount: 3,
                    dispatcher: dispatcher,
                    physics: physics,
                    vfx: vfx,
                    audio: audio,
                    rendering: rendering);

            Assert.AreEqual(2, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.LastFrameEventsCounterId).Value);
            Assert.AreEqual(3, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.HistoryEventsCounterId).Value);
            Assert.AreEqual(4, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.DispatcherExecutedCounterId).Value);
            Assert.AreEqual(5, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.PhysicsFieldsCounterId).Value);
            Assert.AreEqual(10, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.PhysicsQueriesCounterId).Value);
            Assert.AreEqual(11, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.PhysicsQueryResultsCounterId).Value);
            Assert.AreEqual(12, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.PhysicsDuplicateBodySkipsCounterId).Value);
            Assert.AreEqual(6, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.PhysicsAppliedForcesCounterId).Value);
            Assert.AreEqual(7, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.PhysicsCharactersCounterId).Value);
            Assert.AreEqual(8, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.PhysicsMovablesCounterId).Value);
            Assert.AreEqual(14, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.VfxSpawnedCounterId).Value);
            Assert.AreEqual(16, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.AudioPlayedCounterId).Value);
            Assert.AreEqual(18, FindInteractionCounter(snapshot, CharacterTestInteractionPerformanceCounterSource.RenderingPublishedCounterId).Value);
        }

[Test]
        public void CharacterTestInteractionPerformanceCounterSource_ExportsDebugSourceSections()
        {
            FrameworkPerformanceCounterSnapshot snapshot =
                CharacterTestInteractionPerformanceCounterSource.Capture(
                    1,
                    2,
                    CharacterTestInteractionConsumerDispatchSummary.Empty,
                    CharacterTestInteractionPhysicsSummary.Empty,
                    CharacterTestInteractionVfxSummary.Empty,
                    CharacterTestInteractionAudioSummary.Empty,
                    CharacterTestInteractionRenderingSummary.Empty);
            var source = new FrameworkPerformanceCounterDebugSource(
                () => snapshot,
                "CharacterTestInteractionPerformance");

            FrameworkDebugSnapshot debug = source.CreateSnapshot();

            Assert.AreEqual("CharacterTestInteractionPerformance", debug.SourceName);
            Assert.That(debug.Sections[0].Body, Does.Contain("source: CharacterTestInteraction"));
            Assert.That(
                debug.Sections[1].Body,
                Does.Contain(CharacterTestInteractionPerformanceCounterSource.PhysicsQueriesCounterId));
        }

[Test]
        public void CharacterTestRuntimePerformanceCounterSource_MapsRuntimeSummaries()
        {
            var spawn = new CharacterTestCharacterSpawnPerformanceSummary(
                pendingRequestCount: 1,
                lastProcessedRequestCount: 2,
                lastSpawnedCount: 3,
                lastFailedCount: 4,
                totalSpawnedCount: 5,
                totalFailedCount: 6);
            var view = new CharacterTestCharacterViewSyncPerformanceSummary(
                activeViewCount: 7,
                lastSyncRequestCount: 8,
                lastSyncAppliedCount: 9,
                lastSyncMissCount: 10,
                lastTransformApplyCount: 11,
                totalSyncRequestCount: 12,
                totalSyncAppliedCount: 13,
                totalSyncMissCount: 14,
                totalTransformApplyCount: 15);
            var physics = new CombatPhysicsWorldStats(
                revision: 16,
                bodyCount: 17,
                colliderCount: 18);

            FrameworkPerformanceCounterSnapshot snapshot =
                CharacterTestRuntimePerformanceCounterSource.Capture(
                    entityCount: 19,
                    movableObjectCount: 20,
                    spawn: spawn,
                    view: view,
                    physicsStats: physics,
                    staticColliderCount: 21,
                    syncedCharacterColliderCount: 22,
                    syncedMovableColliderCount: 23);

            Assert.IsTrue(snapshot.Enabled);
            Assert.AreEqual(CharacterTestRuntimePerformanceCounterSource.SourceName, snapshot.SourceName);
            Assert.AreEqual(22, snapshot.Count);
            CollectionAssert.AreEqual(
                snapshot.Samples
                    .OrderBy(sample => sample.Category, StringComparer.Ordinal)
                    .ThenBy(sample => sample.CounterId, StringComparer.Ordinal)
                    .Select(sample => sample.CounterId),
                snapshot.Samples.Select(sample => sample.CounterId));
            Assert.IsTrue(snapshot.Samples.All(sample => sample.Cost == FrameworkPerformanceCounterCost.NoAlloc));
            Assert.AreEqual(19, FindRuntimeCounter(snapshot, CharacterTestRuntimePerformanceCounterSource.EntityCountCounterId).Value);
            Assert.AreEqual(20, FindRuntimeCounter(snapshot, CharacterTestRuntimePerformanceCounterSource.MovableObjectCountCounterId).Value);
            Assert.AreEqual(3, FindRuntimeCounter(snapshot, CharacterTestRuntimePerformanceCounterSource.SpawnLastSpawnedCounterId).Value);
            Assert.AreEqual(4, FindRuntimeCounter(snapshot, CharacterTestRuntimePerformanceCounterSource.SpawnLastFailedCounterId).Value);
            Assert.AreEqual(11, FindRuntimeCounter(snapshot, CharacterTestRuntimePerformanceCounterSource.ViewLastTransformAppliesCounterId).Value);
            Assert.AreEqual(18, FindRuntimeCounter(snapshot, CharacterTestRuntimePerformanceCounterSource.PhysicsWorldCollidersCounterId).Value);
            Assert.AreEqual(22, FindRuntimeCounter(snapshot, CharacterTestRuntimePerformanceCounterSource.PhysicsSyncedCharacterCollidersCounterId).Value);
            Assert.AreEqual(23, FindRuntimeCounter(snapshot, CharacterTestRuntimePerformanceCounterSource.PhysicsSyncedMovableCollidersCounterId).Value);
        }

[Test]
        public void CharacterTestUiPerformanceCounterSource_MapsUiRefreshSummaries()
        {
            var summary = new CharacterTestUiPerformanceSummary(
                combatHudRefreshAttemptCount: 1,
                combatHudSuccessfulRefreshCount: 2,
                combatHudSkippedRefreshCount: 3,
                floatingTextRefreshAttemptCount: 4,
                floatingTextSkippedRefreshCount: 5,
                floatingTextActiveCount: 6,
                floatingTextQueuedCount: 7,
                floatingTextSpawnedCount: 8,
                floatingTextDroppedCount: 9,
                mainMenuRefreshAttemptCount: 10,
                mainMenuSuccessfulRefreshCount: 11,
                mainMenuSkippedRefreshCount: 12,
                networkPanelRefreshAttemptCount: 13,
                networkPanelSuccessfulRefreshCount: 14,
                networkPanelSkippedRefreshCount: 15,
                settingsPanelRefreshAttemptCount: 16,
                settingsPanelSuccessfulRefreshCount: 17,
                settingsPanelSkippedRefreshCount: 18,
                abilityPanelRefreshAttemptCount: 19,
                abilityPanelSuccessfulRefreshCount: 20,
                abilityPanelSkippedRefreshCount: 21,
                storyDialogSuccessfulRefreshCount: 22,
                storyDialogSkippedRefreshCount: 23);

            FrameworkPerformanceCounterSnapshot snapshot =
                CharacterTestUiPerformanceCounterSource.Capture(summary);

            Assert.IsTrue(snapshot.Enabled);
            Assert.AreEqual(CharacterTestUiPerformanceCounterSource.SourceName, snapshot.SourceName);
            Assert.AreEqual(23, snapshot.Count);
            Assert.IsTrue(snapshot.Samples.All(sample => sample.Cost == FrameworkPerformanceCounterCost.NoAlloc));
            Assert.AreEqual(1, FindUiCounter(snapshot, CharacterTestUiPerformanceCounterSource.CombatHudRefreshAttemptsCounterId).Value);
            Assert.AreEqual(5, FindUiCounter(snapshot, CharacterTestUiPerformanceCounterSource.FloatingTextRefreshSkippedCounterId).Value);
            Assert.AreEqual(6, FindUiCounter(snapshot, CharacterTestUiPerformanceCounterSource.FloatingTextActiveCounterId).Value);
            Assert.AreEqual(9, FindUiCounter(snapshot, CharacterTestUiPerformanceCounterSource.FloatingTextDroppedCounterId).Value);
            Assert.AreEqual(13, FindUiCounter(snapshot, CharacterTestUiPerformanceCounterSource.NetworkPanelRefreshAttemptsCounterId).Value);
            Assert.AreEqual(20, FindUiCounter(snapshot, CharacterTestUiPerformanceCounterSource.AbilityPanelRefreshSuccessCounterId).Value);
            Assert.AreEqual(23, FindUiCounter(snapshot, CharacterTestUiPerformanceCounterSource.StoryDialogRefreshSkippedCounterId).Value);
        }

[Test]
        public void CharacterTestPerformanceCounterReport_ExportsStableSmokeReport()
        {
            FrameworkPerformanceCounterSnapshot ui =
                CharacterTestUiPerformanceCounterSource.Capture(new CharacterTestUiPerformanceSummary(
                    combatHudRefreshAttemptCount: 1,
                    combatHudSuccessfulRefreshCount: 2,
                    combatHudSkippedRefreshCount: 3,
                    floatingTextRefreshAttemptCount: 4,
                    floatingTextSkippedRefreshCount: 5,
                    floatingTextActiveCount: 6,
                    floatingTextQueuedCount: 7,
                    floatingTextSpawnedCount: 8,
                    floatingTextDroppedCount: 9,
                    mainMenuRefreshAttemptCount: 10,
                    mainMenuSuccessfulRefreshCount: 11,
                    mainMenuSkippedRefreshCount: 12,
                    networkPanelRefreshAttemptCount: 13,
                    networkPanelSuccessfulRefreshCount: 14,
                    networkPanelSkippedRefreshCount: 15,
                    settingsPanelRefreshAttemptCount: 16,
                    settingsPanelSuccessfulRefreshCount: 17,
                    settingsPanelSkippedRefreshCount: 18,
                    abilityPanelRefreshAttemptCount: 19,
                    abilityPanelSuccessfulRefreshCount: 20,
                    abilityPanelSkippedRefreshCount: 21,
                    storyDialogSuccessfulRefreshCount: 22,
                    storyDialogSkippedRefreshCount: 23));
            FrameworkPerformanceCounterSnapshot runtime =
                CharacterTestRuntimePerformanceCounterSource.Capture(
                    entityCount: 2,
                    movableObjectCount: 3,
                    spawn: new CharacterTestCharacterSpawnPerformanceSummary(
                        pendingRequestCount: 4,
                        lastProcessedRequestCount: 5,
                        lastSpawnedCount: 6,
                        lastFailedCount: 7,
                        totalSpawnedCount: 8,
                        totalFailedCount: 9),
                    view: new CharacterTestCharacterViewSyncPerformanceSummary(
                        activeViewCount: 10,
                        lastSyncRequestCount: 11,
                        lastSyncAppliedCount: 12,
                        lastSyncMissCount: 13,
                        lastTransformApplyCount: 14,
                        totalSyncRequestCount: 15,
                        totalSyncAppliedCount: 16,
                        totalSyncMissCount: 17,
                        totalTransformApplyCount: 18),
                    physicsStats: new CombatPhysicsWorldStats(
                        revision: 19,
                        bodyCount: 20,
                        colliderCount: 21),
                    staticColliderCount: 22,
                    syncedCharacterColliderCount: 23,
                    syncedMovableColliderCount: 24);

            string report = CharacterTestPerformanceCounterReport.CreateText(
                string.Empty,
                -1,
                ui,
                null,
                runtime);

            Assert.That(report, Does.StartWith(
                "scenario: " + CharacterTestPerformanceCounterReport.DefaultScenarioName));
            Assert.That(report, Does.Contain("frame: 0"));
            Assert.That(report, Does.Contain("snapshots: 2"));
            Assert.Less(
                report.IndexOf("source: CharacterTestRuntime", StringComparison.Ordinal),
                report.IndexOf("source: CharacterTestUi", StringComparison.Ordinal));
            Assert.That(report, Does.Contain(
                "CharacterTestSpawn/"
                + CharacterTestRuntimePerformanceCounterSource.SpawnLastSpawnedCounterId
                + "=6 entities cost=NoAlloc"));
            Assert.That(report, Does.Contain(
                "CharacterTestView/"
                + CharacterTestRuntimePerformanceCounterSource.ViewLastTransformAppliesCounterId
                + "=14 applies cost=NoAlloc"));
            Assert.That(report, Does.Contain(
                "CharacterTestUi.FloatingText/"
                + CharacterTestUiPerformanceCounterSource.FloatingTextRefreshSkippedCounterId
                + "=5 skips cost=NoAlloc"));
        }

[Test]
        public void CharacterTestPerformanceSmokeScenario_RunsFixedFramesAndExportsCounters()
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty,
                createCharacterViews: false))
            {
                CharacterTestPerformanceSmokeScenarioResult result = slice.RunPerformanceSmokeScenario(
                    "character-test.clean-smoke.contract",
                    warmupFrameCount: 2,
                    captureFrameCount: 3,
                    deltaTimeSeconds: 1d / 60d,
                    uiSnapshotProvider: () => CharacterTestUiPerformanceCounterSource.Capture(
                        new CharacterTestUiPerformanceSummary(
                            combatHudRefreshAttemptCount: 0,
                            combatHudSuccessfulRefreshCount: 0,
                            combatHudSkippedRefreshCount: 0,
                            floatingTextRefreshAttemptCount: 4,
                            floatingTextSkippedRefreshCount: 5,
                            floatingTextActiveCount: 0,
                            floatingTextQueuedCount: 0,
                            floatingTextSpawnedCount: 0,
                            floatingTextDroppedCount: 0,
                            mainMenuRefreshAttemptCount: 0,
                            mainMenuSuccessfulRefreshCount: 0,
                            mainMenuSkippedRefreshCount: 0,
                            networkPanelRefreshAttemptCount: 0,
                            networkPanelSuccessfulRefreshCount: 0,
                            networkPanelSkippedRefreshCount: 0,
                            settingsPanelRefreshAttemptCount: 0,
                            settingsPanelSuccessfulRefreshCount: 0,
                            settingsPanelSkippedRefreshCount: 0,
                            abilityPanelRefreshAttemptCount: 0,
                            abilityPanelSuccessfulRefreshCount: 0,
                            abilityPanelSkippedRefreshCount: 0,
                            storyDialogSuccessfulRefreshCount: 0,
                            storyDialogSkippedRefreshCount: 0)));

                Assert.AreEqual("character-test.clean-smoke.contract", result.ScenarioName);
                Assert.AreEqual(result.StartFrame + 2L, result.CaptureStartFrame);
                Assert.AreEqual(result.StartFrame + 5L, result.Frame);
                Assert.AreEqual(2, result.WarmupFrameCount);
                Assert.AreEqual(3, result.CaptureFrameCount);
                Assert.AreEqual(result.Frame, slice.CurrentFrame.Value);
                Assert.That(result.ReportText, Does.StartWith("scenario: character-test.clean-smoke.contract"));
                Assert.That(result.ReportText, Does.Contain("startFrame: " + result.StartFrame));
                Assert.That(result.ReportText, Does.Contain("captureStartFrame: " + result.CaptureStartFrame));
                Assert.That(result.ReportText, Does.Contain("frame: " + result.Frame));
                Assert.That(result.ReportText, Does.Contain("warmupFrames: 2"));
                Assert.That(result.ReportText, Does.Contain("captureFrames: 3"));
                Assert.That(result.ReportText, Does.Contain("snapshots: 3"));
                Assert.That(result.ReportText, Does.Contain("source: CharacterTestInteraction"));
                Assert.That(result.ReportText, Does.Contain("source: CharacterTestRuntime"));
                Assert.That(result.ReportText, Does.Contain("source: CharacterTestUi"));
                Assert.That(result.ReportText, Does.Contain(
                    CharacterTestRuntimePerformanceCounterSource.EntityCountCounterId));
                Assert.That(result.ReportText, Does.Contain(
                    CharacterTestInteractionPerformanceCounterSource.PhysicsQueriesCounterId));
                Assert.That(result.ReportText, Does.Contain(
                    CharacterTestUiPerformanceCounterSource.FloatingTextRefreshSkippedCounterId
                    + "=5 skips cost=NoAlloc"));
            }
        }

[Test]
        public void CharacterTestInteractionConsumerDispatcher_ExecutesConsumersInStableOrder()
        {
            var calls = new List<string>();
            RuntimeFrame frame = new RuntimeFrame(12);
            CharacterTestInteractionEvent interaction = CreateTestInteraction(
                frame,
                1201904,
                sourceOrder: 1);
            var dispatcher = new CharacterTestInteractionConsumerDispatcher(
                new RecordingInteractionConsumer("interaction.z", 200, calls),
                new RecordingInteractionConsumer("interaction.b", 100, calls),
                new RecordingInteractionConsumer("interaction.a", 100, calls));

            dispatcher.Consume(new CharacterTestInteractionConsumerContext(
                null,
                null,
                new[] { interaction },
                frame,
                60));

            CollectionAssert.AreEqual(
                new[] { "interaction.a", "interaction.b", "interaction.z" },
                calls);
            Assert.AreEqual(3, dispatcher.ConsumerCount);
            Assert.AreEqual(frame.Value, dispatcher.LastSummary.Frame.Value);
            Assert.AreEqual(3, dispatcher.LastSummary.RegisteredConsumerCount);
            Assert.AreEqual(3, dispatcher.LastSummary.ExecutedConsumerCount);
            Assert.AreEqual(0, dispatcher.LastSummary.SkippedConsumerCount);
            Assert.AreEqual(0, dispatcher.LastSummary.FailedConsumerCount);
            Assert.AreEqual("interaction.z", dispatcher.LastSummary.LastConsumerId);
        }

[Test]
        public void CharacterTestInteractionConsumerDispatcher_RejectsNullAndDuplicateConsumers()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CharacterTestInteractionConsumerDispatcher((ICharacterTestInteractionConsumer[])null));
            Assert.Throws<ArgumentException>(() =>
                new CharacterTestInteractionConsumerDispatcher(new ICharacterTestInteractionConsumer[] { null }));

            var calls = new List<string>();
            Assert.Throws<InvalidOperationException>(() =>
                new CharacterTestInteractionConsumerDispatcher(
                    new RecordingInteractionConsumer("interaction.same", 10, calls),
                    new RecordingInteractionConsumer("interaction.same", 20, calls)));
        }

[Test]
        public void CharacterTestInteractionConsumerDispatcher_EmptyInteractionsStillCallsConsumers()
        {
            var calls = new List<string>();
            var consumer = new RecordingInteractionConsumer("interaction.empty", 10, calls);
            var dispatcher = new CharacterTestInteractionConsumerDispatcher(consumer);
            RuntimeFrame frame = new RuntimeFrame(14);

            dispatcher.Consume(new CharacterTestInteractionConsumerContext(
                null,
                null,
                Array.Empty<CharacterTestInteractionEvent>(),
                frame,
                tickRate: 0));

            CollectionAssert.AreEqual(new[] { "interaction.empty" }, calls);
            Assert.AreEqual(0, consumer.LastInteractionCount);
            Assert.AreEqual(CharacterTestDeterministicUnits.DefaultTickRate, consumer.LastTickRate);
            Assert.AreEqual(1, dispatcher.LastSummary.ExecutedConsumerCount);
            Assert.AreEqual("interaction.empty", dispatcher.LastSummary.LastConsumerId);
        }

[Test]
        public void CharacterTestInteractionConsumerDispatcher_ExecutesBuiltInConsumersInSubsystemOrder()
        {
            var dispatcher = new CharacterTestInteractionConsumerDispatcher(
                new CharacterTestInteractionRenderingSystem(),
                new CharacterTestInteractionAudioSystem(null),
                new CharacterTestInteractionPhysicsSystem(),
                new CharacterTestInteractionVfxSystem(null));
            RuntimeFrame frame = new RuntimeFrame(18);

            dispatcher.Consume(new CharacterTestInteractionConsumerContext(
                null,
                null,
                Array.Empty<CharacterTestInteractionEvent>(),
                frame,
                CharacterTestDeterministicUnits.DefaultTickRate));

            Assert.AreEqual(4, dispatcher.ConsumerCount);
            Assert.AreEqual(4, dispatcher.LastSummary.ExecutedConsumerCount);
            Assert.AreEqual(0, dispatcher.LastSummary.FailedConsumerCount);
            Assert.AreEqual(
                CharacterTestInteractionRenderingSystem.ConsumerIdValue,
                dispatcher.LastSummary.LastConsumerId);
        }

[Test]
        public void CharacterTestInteractionPhysicsSystem_ExplosionImpulseTargetsPushableCharactersWithStableFalloff()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            firstTarget.SetTransform(new CharacterTestCharacterTransformComponent(1f, 0f, 0f, 0f));
            secondTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 1f, 0f));
            firstTarget.SetPhysics(firstTarget.Physics.WithRuntimeModifiers(
                massScale: 1f,
                radiusScale: 1f,
                pushScale: 0.5f));
            secondTarget.SetPhysics(secondTarget.Physics.WithRuntimeModifiers(
                massScale: 1f,
                radiusScale: 1f,
                pushScale: 1f,
                canBePushed: false));
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            var system = new CharacterTestInteractionPhysicsSystem();
            var interaction = new CharacterTestInteractionEvent(
                new RuntimeFrame(5),
                CharacterTestInteractionKind.PhysicalField,
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0069,
                sourceOrder: 10,
                sourceEntityId: attacker.Id.Value,
                targetEntityId: 0,
                anchorPosition: FixVector3.Zero,
                direction: new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                radius: Fix64.FromInt(2),
                intensity: 8,
                traceId: 77,
                sourceDomain: CharacterTestInteractionSourceDomains.CombatProjectileAreaPulse,
                CharacterTestInteractionFieldProfiles.FireballExplosionRadialImpulse(8));

            var dispatcher = new CharacterTestInteractionConsumerDispatcher(system);
            dispatcher.Consume(new CharacterTestInteractionConsumerContext(
                world,
                physicsRuntime,
                new[] { interaction },
                new RuntimeFrame(5),
                CharacterTestDeterministicUnits.DefaultTickRate));

            Assert.AreEqual(0, attacker.PendingMotionRequestCount, "Explosion physics must exclude the source entity.");
            Assert.AreEqual(1, firstTarget.PendingMotionRequestCount);
            Assert.AreEqual(0, secondTarget.PendingMotionRequestCount, "Targets with CanBePushed=false must be skipped.");
            CharacterTestCharacterMotionRequest request = firstTarget.MotionRequests.Single();
            Assert.AreEqual(CharacterTestCharacterMotionRequestKind.Impulse, request.Kind);
            Assert.AreEqual(CharacterTestInteractionPhysicsSystem.ExplosionImpulseSourceActionId, request.SourceActionId);
            Assert.AreEqual("test.explosion", request.StableEventId);
            Assert.AreEqual(500, request.XRaw);
            Assert.AreEqual(0, request.YRaw);
            Assert.AreEqual(0, request.ZRaw);
            Assert.AreEqual(1, system.LastSummary.ProcessedEventCount);
            Assert.AreEqual(1, system.LastSummary.AppliedImpulseCount);
            Assert.AreEqual(1, system.LastSummary.CharacterTargetCount);
            Assert.AreEqual(0, system.LastSummary.MovableTargetCount);
            Assert.AreEqual(1, system.LastSummary.SkippedTargetCount);
            Assert.AreEqual(firstTarget.Id.Value, system.LastSummary.LastTargetEntityId);
            Assert.AreEqual(500, system.LastSummary.LastSpeedMillimetersPerSecond);
            Assert.AreEqual("test.explosion", system.LastSummary.LastStableId);
            Assert.AreEqual(1, dispatcher.LastSummary.ExecutedConsumerCount);
            Assert.AreEqual(CharacterTestInteractionPhysicsSystem.ConsumerIdValue, dispatcher.LastSummary.LastConsumerId);
        }

[Test]
        public void CharacterTestInteractionPhysicsSystem_RadialFieldPushesCharactersAndMovableObjects()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            firstTarget.SetTransform(new CharacterTestCharacterTransformComponent(1f, 0f, 0f, 0f));
            secondTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 1f, 0f));
            secondTarget.SetPhysics(secondTarget.Physics.WithRuntimeModifiers(
                massScale: 1f,
                radiusScale: 1f,
                pushScale: 1f,
                canBePushed: false));
            world.SetMovableNaturalObjectSpawnSpecs(new[]
            {
                CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                    "movable.field.target",
                    501,
                    "movable-natural-object",
                    massKg: 80f,
                    CombatSurfaceMaterial.Default,
                    new Vector3(0.25f, 0.25f, 0.25f),
                    new Vector3(-1f, 0.25f, 0f))
            });
            CharacterTestMovableNaturalObjectState movable = world.MovableNaturalObjects.Single();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            var system = new CharacterTestInteractionPhysicsSystem();
            var interaction = new CharacterTestInteractionEvent(
                new RuntimeFrame(5),
                CharacterTestInteractionKind.PhysicalField,
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0076,
                sourceOrder: 10,
                sourceEntityId: attacker.Id.Value,
                targetEntityId: 0,
                anchorPosition: FixVector3.Zero,
                direction: new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                radius: Fix64.FromInt(2),
                intensity: 8,
                traceId: 77,
                sourceDomain: CharacterTestInteractionSourceDomains.CombatProjectileAreaPulse,
                CharacterTestInteractionFieldProfiles.FireballExplosionRadialImpulse(8));

            system.Tick(world, physicsRuntime, new[] { interaction }, new RuntimeFrame(5));

            Assert.AreEqual(1, firstTarget.PendingMotionRequestCount);
            Assert.AreEqual(0, secondTarget.PendingMotionRequestCount);
            Assert.AreEqual(0, movable.VelocityXMillimetersPerSecond);
            Assert.AreEqual(0, movable.VelocityZMillimetersPerSecond);
            Assert.AreEqual(1, system.LastSummary.CharacterTargetCount);
            Assert.AreEqual(1, system.LastSummary.MovableTargetCount);
            Assert.AreEqual(2, system.LastSummary.AppliedForceCount);
            Assert.AreEqual(501, system.LastSummary.LastMovableNumericId);

            CharacterTestMovableNaturalObjectSystem.BeginFrame(world.MovableNaturalObjects);
            CharacterTestMovableNaturalObjectSystem.Tick(
                world,
                physicsRuntime,
                CharacterTestDeterministicUnits.DefaultTickRate,
                new RuntimeFrame(6));

            Assert.Less(movable.VelocityXMillimetersPerSecond, 0);
            Assert.AreEqual(0, movable.VelocityZMillimetersPerSecond);
            Assert.Less(movable.XMillimeters, -1000);
            StringAssert.Contains("interaction:test.radial.field", movable.ContactPair);
            Assert.Greater(movable.ContactPressure, 0);
            Assert.Greater(movable.ContactCount, 0);
        }

[Test]
        public void CharacterTestInteractionVfxSystem_FireballExplosionPublishesVfx()
        {
            var interaction = CreateTestInteraction(
                new RuntimeFrame(12),
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                sourceOrder: 44);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            var vfxSystem = new CharacterTestInteractionVfxSystem(vfx);

            var dispatcher = new CharacterTestInteractionConsumerDispatcher(vfxSystem);
            dispatcher.Consume(new CharacterTestInteractionConsumerContext(
                null,
                null,
                new[] { interaction },
                new RuntimeFrame(12),
                CharacterTestDeterministicUnits.DefaultTickRate));

            Assert.AreEqual(1, vfxSystem.LastSummary.ProcessedInteractionCount);
            Assert.AreEqual(1, vfxSystem.LastSummary.VfxSpawnedCount);
            Assert.AreEqual(0, vfxSystem.LastSummary.SkippedCount);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                vfxSystem.LastSummary.LastStableId);
            Assert.AreEqual(CharacterTestInteractionForceMode.RadialImpulse, vfxSystem.LastSummary.LastForceMode);
            Assert.AreEqual(CharacterTestInteractionFieldShape.Sphere, vfxSystem.LastSummary.LastFieldShape);
            Assert.AreEqual(
                CharacterTestVfxIds.FireballExplosionImpactDefinitionId,
                vfxSystem.LastSummary.LastVfxDefinitionId);
            Assert.AreEqual(1, dispatcher.LastSummary.ExecutedConsumerCount);
            Assert.AreEqual(
                CharacterTestInteractionVfxSystem.ConsumerIdValue,
                dispatcher.LastSummary.LastConsumerId);

            var pending = new List<VfxSpawnRequest>();
            Assert.AreEqual(1, vfx.CopyPendingSpawnRequests(pending));
            Assert.AreEqual(CharacterTestInteractionPresentationRules.VfxSourceDomain, pending[0].SourceDomain);
            Assert.AreEqual(new VfxDefinitionId(CharacterTestVfxIds.FireballExplosionImpactDefinitionId), pending[0].DefinitionId);
            Assert.AreEqual(12, pending[0].SourceFrame);
            Assert.AreEqual(44, pending[0].SourceOrder);
            Assert.AreEqual(1, pending[0].SourceId);
            Assert.AreEqual(
                CharacterTestVfxIds.FireballExplosionImpactLifetimeFrames,
                pending[0].LifetimeFramesOverride);
            Assert.AreEqual(VfxAnchorKind.World, pending[0].Anchor.Kind);
            AssertVfxVector(1f, 0f, 2f, pending[0].Anchor.Pose.Position);
            AssertVfxVector(0f, 0f, 1f, pending[0].Anchor.Pose.Forward);
        }

[Test]
        public void CharacterTestInteractionAudioSystem_FireballExplosionPlaysCue()
        {
            var interaction = CreateTestInteraction(
                new RuntimeFrame(12),
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                sourceOrder: 44);
            var audioService = new RecordingAudioService();
            var audioPlayback = new CharacterTestAudioCuePlaybackSystem(
                audioService,
                CharacterTestAudioCueCatalog.CreateDefault(),
                NullRuntimeLogger.Instance);
            var audioSystem = new CharacterTestInteractionAudioSystem(audioPlayback);

            var dispatcher = new CharacterTestInteractionConsumerDispatcher(audioSystem);
            dispatcher.Consume(new CharacterTestInteractionConsumerContext(
                null,
                null,
                new[] { interaction },
                new RuntimeFrame(12),
                CharacterTestDeterministicUnits.DefaultTickRate));

            Assert.AreEqual(1, audioSystem.LastSummary.ProcessedInteractionCount);
            Assert.AreEqual(1, audioSystem.LastSummary.AudioPlayedCount);
            Assert.AreEqual(0, audioSystem.LastSummary.SkippedCount);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                audioSystem.LastSummary.LastStableId);
            Assert.AreEqual(CharacterTestInteractionForceMode.RadialImpulse, audioSystem.LastSummary.LastForceMode);
            Assert.AreEqual(CharacterTestInteractionFieldShape.Sphere, audioSystem.LastSummary.LastFieldShape);
            Assert.AreEqual(
                CharacterTestAudioCueCatalog.FireballExplosionCueKey,
                audioSystem.LastSummary.LastAudioCueKey);
            Assert.AreEqual(1, dispatcher.LastSummary.ExecutedConsumerCount);
            Assert.AreEqual(
                CharacterTestInteractionAudioSystem.ConsumerIdValue,
                dispatcher.LastSummary.LastConsumerId);
            Assert.AreEqual(1, audioService.PlayOneShotCount);
            Assert.AreEqual(CharacterTestAudioCueCatalog.FireballExplosionEventId, audioService.LastPlayRequest.EventId);
            Assert.AreEqual(12L, audioService.LastPlayRequest.Frame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                audioService.LastPlayRequest.TraceId);
            Assert.AreEqual(1, audioService.LastPlayRequest.EmitterId);
            Assert.AreEqual(1f, audioService.LastPlayRequest.Transform.X, 0.0001f);
            Assert.AreEqual(0f, audioService.LastPlayRequest.Transform.Y, 0.0001f);
            Assert.AreEqual(2f, audioService.LastPlayRequest.Transform.Z, 0.0001f);
            Assert.AreEqual(0f, audioService.LastPlayRequest.Transform.ForwardX, 0.0001f);
            Assert.AreEqual(0f, audioService.LastPlayRequest.Transform.ForwardY, 0.0001f);
            Assert.AreEqual(1f, audioService.LastPlayRequest.Transform.ForwardZ, 0.0001f);
        }

[Test]
        public void CharacterTestInteractionRenderingSystem_PhysicalFieldPublishesRenderFieldImpulse()
        {
            RuntimeFrame frame = new RuntimeFrame(12);
            CharacterTestInteractionEvent interaction = CreateTestInteraction(
                frame,
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                sourceOrder: 44);
            var subjects = new MxRenderSubjectRegistry();
            var publisher = new RenderDataPublisher(subjects);
            var rendering = new CharacterTestInteractionRenderingSystem(
                publisher,
                subjects.CreateMap<int>());
            var dispatcher = new CharacterTestInteractionConsumerDispatcher(rendering);

            dispatcher.Consume(new CharacterTestInteractionConsumerContext(
                null,
                null,
                new[] { interaction },
                frame,
                CharacterTestDeterministicUnits.DefaultTickRate));

            Assert.AreEqual(1, rendering.LastSummary.ProcessedFieldCount);
            Assert.AreEqual(1, rendering.LastSummary.PublishedFieldImpulseCount);
            Assert.AreEqual(0, rendering.LastSummary.SkippedCount);
            Assert.AreEqual(
                CharacterTestInteractionRenderChannels.RadialImpulseSphere,
                rendering.LastSummary.LastChannelId);
            Assert.AreEqual(CharacterTestInteractionForceMode.RadialImpulse, rendering.LastSummary.LastForceMode);
            Assert.AreEqual(CharacterTestInteractionFieldShape.Sphere, rendering.LastSummary.LastFieldShape);
            Assert.AreEqual(1, rendering.LastSummary.LastSubjectSourceId);
            Assert.AreEqual(2.5f, rendering.LastSummary.LastRadius, 0.0001f);
            Assert.AreEqual(2f, rendering.LastSummary.LastIntensity, 0.0001f);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                rendering.LastSummary.LastStableId);
            Assert.AreEqual(1, dispatcher.LastSummary.ExecutedConsumerCount);
            Assert.AreEqual(
                CharacterTestInteractionRenderingSystem.ConsumerIdValue,
                dispatcher.LastSummary.LastConsumerId);

            RenderDataPublisherSnapshot snapshot = publisher.CaptureSnapshot();
            Assert.AreEqual(1, snapshot.CurrentFrameCount(RenderDataEventKind.FieldImpulse));
            RenderDataEvent evt = snapshot.CurrentFrameEvents.Single();
            Assert.AreEqual(RenderDataEventKind.FieldImpulse, evt.Kind);
            Assert.AreEqual(CharacterTestInteractionRenderChannels.RadialImpulseSphere, evt.ChannelId);
            Assert.AreEqual(1f, evt.WorldPosition.x, 0.0001f);
            Assert.AreEqual(0f, evt.WorldPosition.y, 0.0001f);
            Assert.AreEqual(2f, evt.WorldPosition.z, 0.0001f);
            Assert.AreEqual(2.5f, evt.Radius, 0.0001f);
            Assert.AreEqual(2f, evt.Magnitude, 0.0001f);
        }

[Test]
        public void CharacterTestInteractionRenderingSystem_AttractionCapsulePublishesNamedRenderChannel()
        {
            RuntimeFrame frame = new RuntimeFrame(18);
            var interaction = new CharacterTestInteractionEvent(
                frame,
                CharacterTestInteractionKind.PhysicalField,
                CharacterTestCharacterActionConfigSource.TornadoAttractionStableEventId,
                sourceOrder: 7,
                sourceEntityId: 2,
                targetEntityId: 0,
                new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.FromInt(4)),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                CharacterTestInteractionFieldProfiles.TornadoAttractionRadius,
                CharacterTestInteractionFieldProfiles.TornadoAttractionAccelerationMillimetersPerSecondSquared,
                traceId: 99,
                CharacterTestInteractionSourceDomains.CombatProjectileActiveField,
                CharacterTestInteractionFieldProfiles.TornadoAttractionCapsule());
            var subjects = new MxRenderSubjectRegistry();
            var publisher = new RenderDataPublisher(subjects);
            var rendering = new CharacterTestInteractionRenderingSystem(
                publisher,
                subjects.CreateMap<int>());

            rendering.Tick(new[] { interaction }, frame);

            Assert.AreEqual(1, rendering.LastSummary.ProcessedFieldCount);
            Assert.AreEqual(1, rendering.LastSummary.PublishedFieldImpulseCount);
            Assert.AreEqual(
                CharacterTestInteractionRenderChannels.AttractionCapsule,
                rendering.LastSummary.LastChannelId);
            Assert.AreEqual(CharacterTestInteractionForceMode.AttractionAcceleration, rendering.LastSummary.LastForceMode);
            Assert.AreEqual(CharacterTestInteractionFieldShape.Capsule, rendering.LastSummary.LastFieldShape);
            Assert.AreEqual(2, rendering.LastSummary.LastSubjectSourceId);
            Assert.AreEqual(4.5f, rendering.LastSummary.LastRadius, 0.0001f);
            Assert.AreEqual(36f, rendering.LastSummary.LastIntensity, 0.0001f);

            RenderDataPublisherSnapshot snapshot = publisher.CaptureSnapshot();
            RenderDataEvent evt = snapshot.CurrentFrameEvents.Single();
            Assert.AreEqual(CharacterTestInteractionRenderChannels.AttractionCapsule, evt.ChannelId);
            Assert.AreEqual(3f, evt.WorldPosition.x, 0.0001f);
            Assert.AreEqual(4f, evt.WorldPosition.z, 0.0001f);
            Assert.AreEqual(4.5f, evt.Radius, 0.0001f);
            Assert.AreEqual(36f, evt.Magnitude, 0.0001f);
        }

[Test]
        public void CharacterTestInteractionRenderingSystem_WithoutPublisherSkipsButKeepsSummary()
        {
            RuntimeFrame frame = new RuntimeFrame(12);
            CharacterTestInteractionEvent interaction = CreateTestInteraction(
                frame,
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                sourceOrder: 44);
            var rendering = new CharacterTestInteractionRenderingSystem();

            rendering.Tick(new[] { interaction }, frame);

            Assert.AreEqual(1, rendering.LastSummary.ProcessedFieldCount);
            Assert.AreEqual(0, rendering.LastSummary.PublishedFieldImpulseCount);
            Assert.AreEqual(1, rendering.LastSummary.SkippedCount);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                rendering.LastSummary.LastStableId);
        }

[Test]
        public void CharacterActionRuntime_FireballExplosionInteraction_RecordsSingleAreaPulseEventForMultipleTargets()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            AlignTargetsWithDefaultProjectileSocketLine(firstTarget, secondTarget);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            var interactionPhysics = new CharacterTestInteractionPhysicsSystem();
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FireballExplosionIntentId,
                RuntimeFrame.Zero));

            int interactionCount = 0;
            int acceptedDamageCount = 0;
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            for (int i = 0; i < 40; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                interactionPhysics.Tick(world, physicsRuntime, combat.LastFrameInteractionEvents, frame);

                interactionCount += combat.LastFrameInteractionEvents.Count;
                acceptedDamageCount += attacker.LastCombatHitResults.Count(result => result.IsAcceptedDamage);
                if (interactionCount > 0 && acceptedDamageCount >= 2)
                    break;
            }

            Assert.AreEqual(2, acceptedDamageCount, "AreaPulseOnImpact should damage both aligned hostile targets.");
            Assert.AreEqual(1, interactionCount, "One explosion interaction should represent the area pulse.");
            Assert.AreEqual(1, combat.InteractionHistory.Count);
            CharacterTestInteractionEvent interaction = combat.InteractionHistory.Events.Single();
            Assert.AreEqual(CharacterTestInteractionKind.PhysicalField, interaction.Kind);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                interaction.StableId);
            Assert.AreEqual(CharacterTestInteractionSourceDomains.CombatProjectileAreaPulse, interaction.SourceDomain);
            Assert.AreEqual(attacker.Id.Value, interaction.SourceEntityId);
            Assert.AreEqual(0, interaction.TargetEntityId);
            Assert.AreEqual(Fix64.FromRatio(5, 2), interaction.Radius);
            Assert.AreEqual(6, interaction.Intensity);
            Assert.AreEqual(CharacterTestInteractionFieldShape.Sphere, interaction.PhysicalField.Shape);
            Assert.AreEqual(CharacterTestInteractionForceMode.RadialImpulse, interaction.PhysicalField.ForceMode);
            Assert.IsTrue(interaction.PhysicalField.Targets(CharacterTestInteractionTargetFilter.DynamicCharacters));
            Assert.IsTrue(interaction.PhysicalField.Targets(CharacterTestInteractionTargetFilter.DynamicMovableObjects));
            Assert.AreEqual(
                6 * CharacterTestInteractionFieldProfiles.FireballExplosionImpulseSpeedPerImpactPowerMillimetersPerSecond,
                interaction.PhysicalField.ImpulseSpeedMillimetersPerSecond);
            Assert.GreaterOrEqual(interaction.SourceOrder, 0);
            Assert.Greater(interaction.TraceId, 0);
            Assert.That(combat.DamageTelemetryRows, Has.Exactly(2).Matches<DamageTelemetryRow>(row =>
                (row.StableEventId == CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId)
                && row.IncomingDamage == 8
                && row.FinalDamage == 8
                && row.ImpactPower == 6
                && row.ScatterImpact == 6));
            Assert.AreEqual(
                CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - 8,
                world.GetCurrentAttribute(firstTarget, CharacterTestCharacterAttributeIds.Hp));
            Assert.AreEqual(
                CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - 8,
                world.GetCurrentAttribute(secondTarget, CharacterTestCharacterAttributeIds.Hp));
            Assert.AreEqual(1, firstTarget.PendingMotionRequestCount);
            Assert.AreEqual(1, secondTarget.PendingMotionRequestCount);
            Assert.AreEqual(
                CharacterTestInteractionPhysicsSystem.ExplosionImpulseSourceActionId,
                firstTarget.MotionRequests.Single().SourceActionId);
            Assert.AreEqual(
                CharacterTestInteractionPhysicsSystem.ExplosionImpulseSourceActionId,
                secondTarget.MotionRequests.Single().SourceActionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                firstTarget.MotionRequests.Single().StableEventId);
            Assert.AreEqual(1, interactionPhysics.LastSummary.ProcessedEventCount);
            Assert.AreEqual(2, interactionPhysics.LastSummary.AppliedImpulseCount);
            Assert.AreEqual(2, interactionPhysics.LastSummary.CharacterTargetCount);
            Assert.AreEqual(interaction.Frame.Value, combat.InteractionDebugSnapshot.Events.Single().Frame.Value);
            Assert.AreEqual(
                combat.InteractionHistory.ComputeStableHash(),
                combat.InteractionDebugSnapshot.StableHash);
        }

[Test]
        public void CharacterRuntimeModule_FireballExplosionInteractionPhysics_AppliesImpulseNextMotionTickAndHashesDeterministically()
        {
            CharacterTestExplosionRuntimeSliceResult first =
                RunFireballExplosionInteractionPhysicsRuntimeHostSlice();
            CharacterTestExplosionRuntimeSliceResult second =
                RunFireballExplosionInteractionPhysicsRuntimeHostSlice();

            Assert.Greater(first.InteractionFrame, 0);
            Assert.AreEqual(1, first.PendingImpulseCountAtInteractionFrame);
            Assert.AreEqual(
                CharacterTestInteractionPhysicsSystem.ExplosionImpulseSourceActionId,
                first.PendingImpulseSourceActionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId,
                first.PendingImpulseStableEventId);
            Assert.AreEqual(1, first.ProcessedEventCount);
            Assert.AreEqual(1, first.AppliedImpulseCount);
            Assert.GreaterOrEqual(first.PerformanceLastFrameEvents, 1);
            Assert.Greater(first.PerformancePhysicsQueries, 0);
            Assert.Greater(first.PerformancePhysicsQueryResults, 0);
            Assert.Greater(first.PerformancePhysicsAppliedForces, 0);
            Assert.AreEqual(1, first.PerformanceVfxSpawned);
            Assert.AreEqual(1, first.PerformanceAudioPlayed);
            Assert.AreEqual(first.HashBeforeMotionTick, first.HashAfterCounterRead);
            Assert.AreEqual(first.HashAfterMotionTick, second.HashAfterMotionTick);
            Assert.AreEqual(first.TargetVelocityXMillimetersPerSecond, second.TargetVelocityXMillimetersPerSecond);
            Assert.AreEqual(first.TargetVelocityYMillimetersPerSecond, second.TargetVelocityYMillimetersPerSecond);
            Assert.AreNotEqual(0, CharacterTestDeterministicMath.Magnitude(
                first.TargetVelocityXMillimetersPerSecond,
                first.TargetVelocityYMillimetersPerSecond));
            Assert.AreNotEqual(first.HashBeforeMotionTick, first.HashAfterMotionTick);
        }

[Test]
        public void CharacterRuntimeModule_TornadoInteractionPerformanceCounters_RecordFieldWorkload()
        {
            CharacterTestTornadoPerformanceRuntimeSliceResult result =
                RunTornadoInteractionPerformanceRuntimeHostSlice();

            Assert.Greater(result.InteractionFrame, 0);
            Assert.GreaterOrEqual(result.PerformanceLastFrameEvents, 1);
            Assert.Greater(result.PerformancePhysicsQueries, 0);
            Assert.Greater(result.PerformancePhysicsQueryResults, 0);
            Assert.Greater(result.PerformancePhysicsCharacters, 0);
            Assert.Greater(result.PerformancePhysicsMovables, 0);
            Assert.Greater(result.PerformancePhysicsAppliedForces, 0);
            Assert.AreEqual(result.HashBeforeCounterRead, result.HashAfterCounterRead);
        }

[Test]
        public void CharacterTestDebugSourceRegistry_RegistersPerformanceCounters()
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty,
                createCharacterViews: false))
            {
                FrameworkDebugSourceRegistry registry = slice.CreateDebugSourceRegistry();

                Assert.IsTrue(registry.TryGet(
                    "CharacterTestInteractionPerformance",
                    out IFrameworkDebugSource source));
                FrameworkDebugSnapshot debug = source.CreateSnapshot();
                Assert.AreEqual("CharacterTestInteractionPerformance", debug.SourceName);
                Assert.That(debug.Sections[1].Body, Does.Contain(
                    CharacterTestInteractionPerformanceCounterSource.LastFrameEventsCounterId));

                Assert.IsTrue(registry.TryGet(
                    "CharacterTestRuntimePerformance",
                    out IFrameworkDebugSource runtimeSource));
                FrameworkDebugSnapshot runtimeDebug = runtimeSource.CreateSnapshot();
                Assert.AreEqual("CharacterTestRuntimePerformance", runtimeDebug.SourceName);
                Assert.That(runtimeDebug.Sections[1].Body, Does.Contain(
                    CharacterTestRuntimePerformanceCounterSource.EntityCountCounterId));

                var owner = new GameObject("CharacterTest UI Performance Source Test");
                CharacterTestUiRuntime runtime = null;
                try
                {
                    CharacterTestDebugUiHost uiOwner = owner.AddComponent<CharacterTestDebugUiHost>();
                    runtime = new CharacterTestUiRuntime(uiOwner, NullRuntimeLogger.Instance);

                    Assert.IsTrue(runtime.RegisterPerformanceDebugSource(registry));
                    Assert.IsFalse(runtime.RegisterPerformanceDebugSource(registry));
                    Assert.IsTrue(registry.TryGet(
                        "CharacterTestUiPerformance",
                        out IFrameworkDebugSource uiSource));

                    FrameworkDebugSnapshot uiDebug = uiSource.CreateSnapshot();
                    Assert.AreEqual("CharacterTestUiPerformance", uiDebug.SourceName);
                    Assert.That(uiDebug.Sections[1].Body, Does.Contain(
                        CharacterTestUiPerformanceCounterSource.FloatingTextRefreshAttemptsCounterId));
                }
                finally
                {
                    runtime?.Release();
                    UnityEngine.Object.DestroyImmediate(owner);
                }
            }
        }
}
}
