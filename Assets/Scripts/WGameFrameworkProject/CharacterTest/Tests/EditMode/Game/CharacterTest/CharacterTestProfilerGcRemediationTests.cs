using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.CharacterApplication;
using MxFramework.CharacterControl;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Input;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.UI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestProfilerGcRemediationTests
    {
        private const string CharacterTestScenePath = "Assets/Scenes/CharacterTestScene.unity";
        private const string PlayModeWarmRunReportPath = "Temp/CharacterTestPerformancePlayModeWarmRunReport.txt";
        private const string PlayModeProfilerRecorderReportPath =
            "Temp/CharacterTestPerformancePlayModeProfilerRecorderReport.txt";
        private const string PlayModeWarmRunScenarioName = "character-test.playmode-scene.warm-run";
        private const string PlayModeProfilerRecorderScenarioName =
            "character-test.playmode-scene.profiler-recorder";
        private const int PlayModeWarmupFrameCount = 60;
        private const int PlayModeCaptureFrameCount = 120;

        private static readonly string[] PlayModeProfilerMarkerNames =
        {
            "CharacterTest.GameManager.Simulation",
            "CharacterTest.GameManager.SlicePresentation",
            "CharacterTest.GameManager.UiRefresh",
            "CharacterTest.GameManager.UiTick",
            "CharacterTest.GameSlice.HostTick",
            "CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime",
            "CharacterTest.CharacterRuntime.Motion",
            "CharacterTest.CharacterRuntime.CombatTrace",
            "CharacterTest.CombatTrace.ApplyDamage",
            "CharacterTest.CombatDamageApplier.ScatterPressureRequests",
            "CharacterTest.CharacterRuntime.PhysicalAudio",
            "CharacterTest.CharacterRuntime.ViewSync",
            "CharacterTest.CharacterRuntime.Action",
            "CharacterTest.CharacterRuntime.CollisionProfile",
            "CharacterTest.CharacterRuntime.RootMotion",
            "CharacterTest.CharacterRuntime.AnimationSync",
            "CharacterTest.UI.CombatHudRefresh",
            "CharacterTest.UI.FloatingTextRefresh",
            "CharacterTest.UI.StoryDialogRefresh"
        };

        [Test]
        public void ResourceModeResolver_UsesStreamingAssetsOfflineModeInPlayer()
        {
            Assert.AreEqual(
                GlobalResourceRuntimeMode.Editor,
                CharacterTestRuntimeSessionBuilder.ResolveResourceMode(
                    GlobalResourceRuntimeMode.Editor,
                    isEditor: true));
            Assert.AreEqual(
                GlobalResourceRuntimeMode.Offline,
                CharacterTestRuntimeSessionBuilder.ResolveResourceMode(
                    GlobalResourceRuntimeMode.Editor,
                    isEditor: false));
            Assert.AreEqual(
                GlobalResourceRuntimeMode.Offline,
                CharacterTestRuntimeSessionBuilder.ResolveResourceMode(
                    GlobalResourceRuntimeMode.Offline,
                    isEditor: false));
            Assert.AreEqual(
                GlobalResourceRuntimeMode.Online,
                CharacterTestRuntimeSessionBuilder.ResolveResourceMode(
                    GlobalResourceRuntimeMode.Online,
                    isEditor: false));
        }

        [Test]
        public void CombatDamageApplier_MultipleTraceIdsDoNotAllocateAfterWarmup()
        {
            const int ResultCount = 64;
            const int TraceIdBase = 8100;
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity attacker = SpawnProfilerGcCharacter(world, teamId: 1, x: 0f, z: 0f);
            var results = new List<HitResolveResult>(ResultCount);
            var payloads = new DamageProfileResolveResult[ResultCount];
            for (int i = 0; i < ResultCount; i++)
            {
                CharacterTestCharacterEntity target =
                    SpawnProfilerGcCharacter(world, teamId: 2, x: i + 1f, z: 1f);
                HitResolveResult result = CreateProfilerDamageResult(attacker, target, TraceIdBase + i);
                results.Add(result);
                payloads[i] = CreateProfilerDamagePayload(result);
            }

            var applier = new CharacterTestCombatDamageApplier();
            var drainedEvents = new List<GameplayRuntimeEvent>(ResultCount);
            var frame = new RuntimeFrame(33);
            DamageProfileResolveResult ResolvePayload(HitResolveResult result)
            {
                return payloads[result.TraceId - TraceIdBase];
            }
            Func<HitResolveResult, DamageProfileResolveResult> resolvePayload = ResolvePayload;

            void ExecutePass()
            {
                applier.ClearFrameOutputs();
                drainedEvents.Clear();
                applier.Apply(world, null, frame, results, resolvePayload);
                world.GameplayComponentWorld.DrainEvents(frame, drainedEvents);
            }

            for (int i = 0; i < 8; i++)
                ExecutePass();

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 16; i++)
                ExecutePass();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(ResultCount, applier.DamageTelemetryRows.Count);
            Assert.AreEqual(ResultCount, applier.DamageResolvedEvents.Count);
            Assert.AreEqual(ResultCount, applier.HitPresentationEvents.Count);
        }

        [Test]
        public void CharacterMotion_PhysicsMotorCacheHandlesMultiplePosturesWithoutSteadyStateAllocation()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            SpawnProfilerGcCharacter(world, teamId: 1, x: -4f, z: 0f);
            CharacterTestCharacterEntity low =
                SpawnProfilerGcCharacter(world, teamId: 1, x: 0f, z: 0f);
            CharacterTestCharacterEntity reviveLow =
                SpawnProfilerGcCharacter(world, teamId: 1, x: 4f, z: 0f);
            SetProfilerColliderPosture(
                low,
                CharacterTestColliderPostureComponent.LowProfileId,
                heightMillimeters: 900,
                radiusMillimeters: 260,
                frame: 0L);
            SetProfilerColliderPosture(
                reviveLow,
                CharacterTestColliderPostureComponent.ReviveLowProfileId,
                heightMillimeters: 700,
                radiusMillimeters: 240,
                frame: 0L);

            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetPhysicsRuntime(CreateProfilerGcPhysicsRuntime());

            for (int i = 0; i < 12; i++)
                motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, new RuntimeFrame(i));

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 24; i++)
                motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, new RuntimeFrame(100 + i));
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(3, world.Entities.Count);
        }

        [Test]
        public void CharacterMotion_PhysicsMotorCacheHandlesRootMotionMaskSwitchWithoutSteadyStateAllocation()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            SpawnProfilerGcCharacter(world, teamId: 1, x: -4f, z: 0f);
            CharacterTestCharacterEntity low =
                SpawnProfilerGcCharacter(world, teamId: 1, x: 0f, z: 0f);
            CharacterTestCharacterEntity reviveLow =
                SpawnProfilerGcCharacter(world, teamId: 1, x: 4f, z: 0f);
            SetProfilerColliderPosture(
                low,
                CharacterTestColliderPostureComponent.LowProfileId,
                heightMillimeters: 900,
                radiusMillimeters: 260,
                frame: 0L);
            SetProfilerColliderPosture(
                reviveLow,
                CharacterTestColliderPostureComponent.ReviveLowProfileId,
                heightMillimeters: 700,
                radiusMillimeters: 240,
                frame: 0L);

            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetPhysicsRuntime(CreateProfilerGcPhysicsRuntime());

            for (int i = 0; i < 12; i++)
            {
                EnqueueProfilerRootMotion(world, CharacterMotionObstaclePolicy.StaticOnly);
                motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, new RuntimeFrame(i * 2L));
                EnqueueProfilerRootMotion(world, CharacterMotionObstaclePolicy.Default);
                motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, new RuntimeFrame(i * 2L + 1L));
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 12; i++)
            {
                EnqueueProfilerRootMotion(world, CharacterMotionObstaclePolicy.StaticOnly);
                motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, new RuntimeFrame(100 + i * 2L));
                EnqueueProfilerRootMotion(world, CharacterMotionObstaclePolicy.Default);
                motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, new RuntimeFrame(100 + i * 2L + 1L));
            }
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(3, world.Entities.Count);
        }

        [Test]
        public void CharacterMotion_PhysicsMotorCacheHandlesPostureSwitchAfterWarmupWithoutSteadyStateAllocation()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity standing =
                SpawnProfilerGcCharacter(world, teamId: 1, x: -4f, z: 0f);
            CharacterTestCharacterEntity low =
                SpawnProfilerGcCharacter(world, teamId: 1, x: 0f, z: 0f);
            CharacterTestCharacterEntity corpse =
                SpawnProfilerGcCharacter(world, teamId: 1, x: 4f, z: 0f);

            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetPhysicsRuntime(CreateProfilerGcPhysicsRuntime());

            for (int i = 0; i < 6; i++)
                motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, new RuntimeFrame(i));

            SetProfilerColliderPosture(
                standing,
                CharacterTestColliderPostureComponent.StandingProfileId,
                heightMillimeters: 1860,
                radiusMillimeters: 340,
                frame: 10L);
            SetProfilerColliderPosture(
                low,
                CharacterTestColliderPostureComponent.LowProfileId,
                heightMillimeters: 900,
                radiusMillimeters: 260,
                frame: 10L);
            SetProfilerColliderPosture(
                corpse,
                CharacterTestColliderPostureComponent.CorpseProfileId,
                heightMillimeters: 520,
                radiusMillimeters: 260,
                frame: 10L,
                blocksCharacters: true,
                canBePushed: true);

            for (int i = 0; i < 12; i++)
                motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, new RuntimeFrame(10 + i));

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 24; i++)
                motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, new RuntimeFrame(100 + i));
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(3, world.Entities.Count);
        }

        [Test]
        public void CharacterActionRuntime_ActionSlotStartAndFinishDoNotAllocateAfterWarmup()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = SpawnProfilerGcCharacter(world, teamId: 1, x: 0f, z: 0f);
            CharacterTestCharacterActionRuntime runtime =
                CharacterTestCharacterActionConfigSource.Load(
                    CharacterTestCharacterActionConfigSourceOptions.CreateDefault());
            runtime.BindRuntimeWorld(world);

            long frame = 0L;
            for (int i = 0; i < 6; i++)
                Assert.IsTrue(RunProfilerActionSlotCycle(runtime, entity, ref frame));

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool completed = true;
            for (int i = 0; i < 8; i++)
                completed &= RunProfilerActionSlotCycle(runtime, entity, ref frame);
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(completed);
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(MxFramework.CharacterAction.CharacterActionId.None, entity.Action.ActiveActionId);
        }

        [Test]
        public void CharacterRuntimeView_AuthorityPoseChannelCacheDoesNotAllocateAfterWarmup()
        {
            GameObject root = new GameObject("AuthorityPoseRoot");
            GameObject spine = new GameObject("spine");
            spine.transform.SetParent(root.transform, worldPositionStays: false);
            GameObject rightHand = new GameObject("right_hand");
            rightHand.transform.SetParent(root.transform, worldPositionStays: false);
            var view = new CharacterTestRuntimeCharacterView(
                new CharacterTestCharacterEntityId(1),
                root,
                null,
                null,
                null);
            CharacterAuthorityPoseStateComponent authorityPose =
                CreateProfilerAuthorityPoseState();

            try
            {
                for (int i = 0; i < 8; i++)
                {
                    authorityPose.Tick(new RuntimeFrame(i));
                    view.SyncAuthorityPose(authorityPose);
                    view.TickPresentation(1f / 60f, new RuntimeFrame(i));
                }

                Vector3 warmSpinePosition = spine.transform.position;
                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 32; i++)
                {
                    authorityPose.Tick(new RuntimeFrame(100 + i));
                    view.SyncAuthorityPose(authorityPose);
                    view.TickPresentation(1f / 60f, new RuntimeFrame(100 + i));
                }

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0L, allocatedBytes);
                Assert.AreNotEqual(Vector3.zero, warmSpinePosition);
                Assert.AreEqual(warmSpinePosition, spine.transform.position);
            }
            finally
            {
                view.Release();
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CharacterRuntimeView_BodyPartContactSocketCacheDoesNotAllocateAfterWarmup()
        {
            GameObject root = new GameObject("BodyPartContactRoot");
            GameObject foot = new GameObject("foot_l");
            foot.transform.SetParent(root.transform, worldPositionStays: false);
            GameObject marker = new GameObject("ContactPoint_foot.l");
            marker.transform.SetParent(foot.transform, worldPositionStays: false);
            marker.transform.localPosition = new Vector3(0.15f, 0.02f, 0.03f);
            CharacterBoneRuntimeSync boneSync = root.AddComponent<CharacterBoneRuntimeSync>();
            var binding = new CharacterBoneRuntimeSyncBinding();
            binding.Configure(
                "socket:foot_l",
                "foot_l",
                foot.transform,
                foot.transform,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one);
            boneSync.Configure(new[] { binding }, useExternalRuntimeTick: true);
            var view = new CharacterTestRuntimeCharacterView(
                new CharacterTestCharacterEntityId(1),
                root,
                null,
                null,
                null,
                boneSync);
            CharacterBodyPartContactPointConfig config =
                CreateProfilerBodyPartContactPointConfig();

            try
            {
                for (int i = 0; i < 8; i++)
                {
                    Assert.IsTrue(view.TryResolveBodyPartContactPointWorldPosition(
                        config,
                        out Vector3 warmPosition));
                    Assert.AreEqual(marker.transform.position, warmPosition);
                }

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                bool resolved = true;
                for (int i = 0; i < 128; i++)
                {
                    resolved &= view.TryResolveBodyPartContactPointWorldPosition(
                        config,
                        out Vector3 position);
                    resolved &= position == marker.transform.position;
                }

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.IsTrue(resolved);
                Assert.AreEqual(0L, allocatedBytes);
            }
            finally
            {
                view.Release();
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PerformanceSmokeReportMenu_WritesStableReportIntoTemp()
        {
            string reportPath = Path.Combine("Temp", "CharacterTestPerformanceSmokeReportTests.txt");
            string fullPath = CharacterTestPerformanceSmokeReportMenu.ResolveReportPath(reportPath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            try
            {
                string writtenPath = CharacterTestPerformanceSmokeReportMenu.WriteSmokeReport(
                    reportPath,
                    "character-test.editor-menu.contract",
                    warmupFrameCount: 1,
                    captureFrameCount: 2,
                    deltaTimeSeconds: 1d / 60d);

                Assert.AreEqual(fullPath, writtenPath);
                Assert.IsTrue(File.Exists(fullPath));

                string report = File.ReadAllText(fullPath);
                Assert.That(report, Does.StartWith("scenario: character-test.editor-menu.contract"));
                Assert.That(report, Does.Contain("startFrame: 1"));
                Assert.That(report, Does.Contain("captureStartFrame: 2"));
                Assert.That(report, Does.Contain("frame: 4"));
                Assert.That(report, Does.Contain("warmupFrames: 1"));
                Assert.That(report, Does.Contain("captureFrames: 2"));
                Assert.That(report, Does.Contain("snapshots: 2"));
                Assert.That(report, Does.Contain("source: CharacterTestInteraction"));
                Assert.That(report, Does.Contain("source: CharacterTestRuntime"));
                Assert.That(report, Does.Contain(
                    CharacterTestRuntimePerformanceCounterSource.EntityCountCounterId));
                Assert.That(report, Does.Contain(
                    CharacterTestInteractionPerformanceCounterSource.PhysicsQueriesCounterId));
            }
            finally
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
        }

        [Test]
        public void PerformanceWarmRunTimingReportMenu_WritesTimingAndAllocationStatsIntoTemp()
        {
            string reportPath = Path.Combine("Temp", "CharacterTestPerformanceWarmRunTimingReportTests.txt");
            string fullPath = CharacterTestPerformanceWarmRunTimingReportMenu.ResolveReportPath(reportPath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            try
            {
                string writtenPath = CharacterTestPerformanceWarmRunTimingReportMenu.WriteWarmRunTimingReport(
                    reportPath,
                    "character-test.editor-menu.timing-contract",
                    warmupFrameCount: 1,
                    captureFrameCount: 3,
                    deltaTimeSeconds: 1d / 60d);

                Assert.AreEqual(fullPath, writtenPath);
                Assert.IsTrue(File.Exists(fullPath));

                string report = File.ReadAllText(fullPath);
                Assert.That(report, Does.StartWith("scenario: character-test.editor-menu.timing-contract"));
                Assert.That(report, Does.Contain("profileKind: clean-editor-warm-run"));
                Assert.That(report, Does.Contain("warmupFrames: 1"));
                Assert.That(report, Does.Contain("captureFrames: 3"));
                Assert.That(report, Does.Contain("timingSamples: 3"));
                Assert.That(report, Does.Contain("elapsedMs.median: "));
                Assert.That(report, Does.Contain("elapsedMs.p95: "));
                Assert.That(report, Does.Contain("allocatedBytes.total: "));
                Assert.That(report, Does.Contain("allocatedBytes.p95: "));
                Assert.That(report, Does.Contain("source: CharacterTestInteraction"));
                Assert.That(report, Does.Contain("source: CharacterTestRuntime"));
            }
            finally
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
        }

        [Test]
        public void PerformanceProfilerRawCsvExportMenu_WritesCsvHeadersAndSummaryIntoTemp()
        {
            string frameTimePath = Path.Combine("Temp", "ProfileAnalyzer", "CharacterTestRawFrameTimeExportTests.csv");
            string markerTimingPath = Path.Combine("Temp", "ProfileAnalyzer", "CharacterTestRawMarkerTimingExportTests.csv");
            string summaryPath = Path.Combine("Temp", "ProfileAnalyzer", "CharacterTestRawProfilerExportTests.txt");
            string fullFrameTimePath = Path.GetFullPath(frameTimePath);
            string fullMarkerTimingPath = Path.GetFullPath(markerTimingPath);
            string fullSummaryPath = Path.GetFullPath(summaryPath);
            if (File.Exists(fullFrameTimePath))
                File.Delete(fullFrameTimePath);
            if (File.Exists(fullMarkerTimingPath))
                File.Delete(fullMarkerTimingPath);
            if (File.Exists(fullSummaryPath))
                File.Delete(fullSummaryPath);

            try
            {
                string writtenSummaryPath = CharacterTestPerformanceProfilerRawCsvExportMenu.ExportCurrentProfilerRawCsv(
                    frameTimePath,
                    markerTimingPath,
                    summaryPath,
                    CharacterTestPerformanceProfilerRawCsvExportMenu.DefaultMarkerThreadFilter,
                    maxFrameCount: 1);

                Assert.AreEqual(fullSummaryPath, writtenSummaryPath);
                Assert.IsTrue(File.Exists(fullFrameTimePath));
                Assert.IsTrue(File.Exists(fullMarkerTimingPath));
                Assert.IsTrue(File.Exists(fullSummaryPath));

                string frameTimeCsv = File.ReadAllText(fullFrameTimePath);
                string markerTimingCsv = File.ReadAllText(fullMarkerTimingPath);
                string summary = File.ReadAllText(fullSummaryPath);
                Assert.That(frameTimeCsv, Does.StartWith("Frame Offset; Frame Index; Frame Time (ms)"));
                Assert.That(markerTimingCsv, Does.StartWith("Name;Median Time;Min Time;Max Time"));
                Assert.That(summary, Does.StartWith("scenario: character-test.editor-profiler-raw-csv-export"));
                Assert.That(summary, Does.Contain("markerThreadFilter: Main Thread"));
                Assert.That(summary, Does.Contain("frameTimeCsv: " + fullFrameTimePath));
                Assert.That(summary, Does.Contain("markerTimingCsv: " + fullMarkerTimingPath));
            }
            finally
            {
                if (File.Exists(fullFrameTimePath))
                    File.Delete(fullFrameTimePath);
                if (File.Exists(fullMarkerTimingPath))
                    File.Delete(fullMarkerTimingPath);
                if (File.Exists(fullSummaryPath))
                    File.Delete(fullSummaryPath);
            }
        }

        [Test]
        public void PerformanceProfilerRawCsvExportMenu_ResolvesMarkerCenteredWindow()
        {
            Assert.IsTrue(
                CharacterTestPerformanceProfilerRawCsvExportMenu.TryResolveMarkerCenteredRawFrameWindow(
                    firstRawFrameIndex: 10,
                    lastRawFrameIndex: 30,
                    markerRawFrameIndex: 20,
                    frameRadius: 4,
                    out int middleFirst,
                    out int middleLast));
            Assert.AreEqual(16, middleFirst);
            Assert.AreEqual(24, middleLast);

            Assert.IsTrue(
                CharacterTestPerformanceProfilerRawCsvExportMenu.TryResolveMarkerCenteredRawFrameWindow(
                    firstRawFrameIndex: 10,
                    lastRawFrameIndex: 30,
                    markerRawFrameIndex: 11,
                    frameRadius: 4,
                    out int clampedFirst,
                    out int clampedLast));
            Assert.AreEqual(10, clampedFirst);
            Assert.AreEqual(15, clampedLast);

            Assert.IsFalse(
                CharacterTestPerformanceProfilerRawCsvExportMenu.TryResolveMarkerCenteredRawFrameWindow(
                    firstRawFrameIndex: 10,
                    lastRawFrameIndex: 30,
                    markerRawFrameIndex: 31,
                    frameRadius: 4,
                    out _,
                    out _));
        }

        [Test]
        public void PerformanceProfilerRawCsvExportMenu_WritesMarkerWindowSummaryWhenMarkerIsMissing()
        {
            string frameTimePath = Path.Combine("Temp", "ProfileAnalyzer", "CharacterTestRawMarkerWindowFrameTimeExportTests.csv");
            string markerTimingPath = Path.Combine("Temp", "ProfileAnalyzer", "CharacterTestRawMarkerWindowTimingExportTests.csv");
            string summaryPath = Path.Combine("Temp", "ProfileAnalyzer", "CharacterTestRawMarkerWindowExportTests.txt");
            string fullFrameTimePath = Path.GetFullPath(frameTimePath);
            string fullMarkerTimingPath = Path.GetFullPath(markerTimingPath);
            string fullSummaryPath = Path.GetFullPath(summaryPath);
            if (File.Exists(fullFrameTimePath))
                File.Delete(fullFrameTimePath);
            if (File.Exists(fullMarkerTimingPath))
                File.Delete(fullMarkerTimingPath);
            if (File.Exists(fullSummaryPath))
                File.Delete(fullSummaryPath);

            try
            {
                string writtenSummaryPath =
                    CharacterTestPerformanceProfilerRawCsvExportMenu.ExportCurrentProfilerRawCsvAroundMarker(
                        frameTimePath,
                        markerTimingPath,
                        summaryPath,
                        CharacterTestPerformanceProfilerRawCsvExportMenu.DefaultMarkerThreadFilter,
                        "CharacterTest.Tests.MarkerThatShouldNotExist",
                        frameRadius: 1,
                        maxSearchFrameCount: 1);

                Assert.AreEqual(fullSummaryPath, writtenSummaryPath);
                Assert.IsTrue(File.Exists(fullFrameTimePath));
                Assert.IsTrue(File.Exists(fullMarkerTimingPath));
                Assert.IsTrue(File.Exists(fullSummaryPath));

                string frameTimeCsv = File.ReadAllText(fullFrameTimePath);
                string markerTimingCsv = File.ReadAllText(fullMarkerTimingPath);
                string summary = File.ReadAllText(fullSummaryPath);
                Assert.That(frameTimeCsv, Does.StartWith("Frame Offset; Frame Index; Frame Time (ms)"));
                Assert.That(markerTimingCsv, Does.StartWith("Name;Median Time;Min Time;Max Time"));
                Assert.That(summary, Does.Contain("markerFocusName: CharacterTest.Tests.MarkerThatShouldNotExist"));
                Assert.That(summary, Does.Contain("markerFocusFound: false"));
                Assert.That(summary, Does.Contain("markerSearchMaxFrameCount: 1"));
            }
            finally
            {
                if (File.Exists(fullFrameTimePath))
                    File.Delete(fullFrameTimePath);
                if (File.Exists(fullMarkerTimingPath))
                    File.Delete(fullMarkerTimingPath);
                if (File.Exists(fullSummaryPath))
                    File.Delete(fullSummaryPath);
            }
        }

        [Test]
        public void PerformancePlayerReportRunner_CommandLineParserResolvesReportDirectory()
        {
            string relativePath = Path.Combine("Temp", "CharacterTestPerformancePlayerParserTests");
            string expectedPath = Path.GetFullPath(relativePath);

            Assert.IsTrue(
                CharacterTestPerformancePlayerReportRunner.TryResolveReportDirectory(
                    new[] { "CharacterTest", CharacterTestPerformancePlayerReportRunner.ReportDirectoryArgument, relativePath },
                    out string pairedPath));
            Assert.AreEqual(expectedPath, pairedPath);

            Assert.IsTrue(
                CharacterTestPerformancePlayerReportRunner.TryResolveReportDirectory(
                    new[] { "CharacterTest", CharacterTestPerformancePlayerReportRunner.ReportDirectoryArgument + "=" + relativePath },
                    out string assignedPath));
            Assert.AreEqual(expectedPath, assignedPath);

            Assert.IsFalse(
                CharacterTestPerformancePlayerReportRunner.TryResolveReportDirectory(
                    new[] { "CharacterTest", CharacterTestPerformancePlayerReportRunner.ReportDirectoryArgument },
                    out string missingPath));
            Assert.IsNull(missingPath);
        }

        [Test]
        public void PerformancePlayerBuildReportMenu_ResolvesDesktopTargetPathsAndExecutables()
        {
            string root = Path.GetFullPath(Path.Combine(
                "Temp",
                "CharacterTestPlayerBuildPathTests",
                Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            try
            {
                string windowsPath = CharacterTestPerformancePlayerBuildReportMenu.ResolvePlayerLocationPath(
                    root,
                    BuildTarget.StandaloneWindows64);
                Assert.That(windowsPath, Does.EndWith("CharacterTestPerformance.exe"));
                File.WriteAllText(windowsPath, string.Empty);
                Assert.AreEqual(
                    windowsPath,
                    CharacterTestPerformancePlayerBuildReportMenu.ResolvePlayerExecutable(
                        windowsPath,
                        BuildTarget.StandaloneWindows64));

                string linuxPath = CharacterTestPerformancePlayerBuildReportMenu.ResolvePlayerLocationPath(
                    root,
                    BuildTarget.StandaloneLinux64);
                Assert.That(linuxPath, Does.EndWith("CharacterTestPerformance"));
                File.WriteAllText(linuxPath, string.Empty);
                Assert.AreEqual(
                    linuxPath,
                    CharacterTestPerformancePlayerBuildReportMenu.ResolvePlayerExecutable(
                        linuxPath,
                        BuildTarget.StandaloneLinux64));

                string macPath = CharacterTestPerformancePlayerBuildReportMenu.ResolvePlayerLocationPath(
                    root,
                    BuildTarget.StandaloneOSX);
                Assert.That(macPath, Does.EndWith("CharacterTestPerformance.app"));
                string macExecutableDirectory = Path.Combine(macPath, "Contents", "MacOS");
                Directory.CreateDirectory(macExecutableDirectory);
                string macExecutablePath = Path.Combine(
                    macExecutableDirectory,
                    "CharacterTestPerformance");
                File.WriteAllText(macExecutablePath, string.Empty);
                Assert.AreEqual(
                    macExecutablePath,
                    CharacterTestPerformancePlayerBuildReportMenu.ResolvePlayerExecutable(
                        macPath,
                        BuildTarget.StandaloneOSX));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void PackageTestCommand_CommandLineParserResolvesOptions()
        {
            string outputRoot = Path.Combine("Temp", "CharacterTestPackageParserTests");
            CharacterTestPackageTestOptions paired =
                CharacterTestPackageTestCommand.ResolveOptionsFromCommandLine(
                    new[]
                    {
                        "Unity",
                        CharacterTestPackageTestCommand.OutputRootArgument,
                        outputRoot,
                        CharacterTestPackageTestCommand.SkipResourceBuildArgument,
                        CharacterTestPackageTestCommand.NoRunArgument
                    });
            Assert.AreEqual(outputRoot, paired.OutputRoot);
            Assert.IsTrue(paired.SkipResourceBuild);
            Assert.IsTrue(paired.NoRun);

            CharacterTestPackageTestOptions assigned =
                CharacterTestPackageTestCommand.ResolveOptionsFromCommandLine(
                    new[]
                    {
                        "Unity",
                        CharacterTestPackageTestCommand.OutputRootArgument + "=" + outputRoot
                    });
            Assert.AreEqual(outputRoot, assigned.OutputRoot);
            Assert.IsFalse(assigned.SkipResourceBuild);
            Assert.IsFalse(assigned.NoRun);

            CharacterTestPackageTestOptions defaults =
                CharacterTestPackageTestCommand.ResolveOptionsFromCommandLine(new[] { "Unity" });
            Assert.AreEqual(CharacterTestPackageTestCommand.DefaultOutputRoot, defaults.OutputRoot);
        }

        [Test]
        public void PackageTestCommand_RejectsDifferentActiveBuildTarget()
        {
            Assert.DoesNotThrow(() => CharacterTestPerformancePlayerBuildReportMenu.ValidateActiveBuildTarget(
                BuildTarget.StandaloneWindows64, BuildTarget.StandaloneWindows64));
            StringAssert.Contains("Expected StandaloneWindows64",
                Assert.Throws<InvalidOperationException>(() =>
                    CharacterTestPerformancePlayerBuildReportMenu.ValidateActiveBuildTarget(
                        BuildTarget.StandaloneWindows64, BuildTarget.Android)).Message);
            Assert.Throws<InvalidOperationException>(() =>
                CharacterTestPerformancePlayerBuildReportMenu.ValidateActiveBuildTarget(
                    BuildTarget.NoTarget, BuildTarget.StandaloneWindows64));
        }

        [Test]
        public void PackageTestCommand_SelectsSceneCharactersAndRejectsStaleOrMissingPackages()
        {
            CharacterTestPlayerCharacterPackageSelection selected =
                CharacterTestPlayerCharacterPackageSelection.Resolve();
            CollectionAssert.AreEqual(new[] { "000001", "000002", "000003", "000009" },
                selected.Packages.Select(package => package.PackageId).ToArray());
            StringAssert.Contains("player.local.01|668367837|000009", selected.SourceEvidence);

            string fixtureRoot = Path.Combine("Temp", "CharacterTestPackageSelection", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixtureRoot);
            try
            {
                string levelPath = Path.Combine(fixtureRoot, "level.json");
                JObject level = JObject.Parse(File.ReadAllText(
                    CharacterTestSceneCharacterAuthoringAdapter.DefaultDocumentPath));
                JArray modules = (JArray)level["modules"];
                JArray characters = null;
                foreach (JToken module in modules)
                    if ((string)module["moduleId"] == CharacterTestSceneCharacterAuthoringAdapter.CharactersModuleId)
                        characters = (JArray)module["payload"]["characters"];
                Assert.NotNull(characters);
                foreach (JToken character in characters)
                    if ((string)character["stableId"] == "player.local.01") character["characterId"] = 534146882;
                File.WriteAllText(levelPath, level.ToString());
                InvalidOperationException stale = Assert.Throws<InvalidOperationException>(() =>
                    CharacterTestPlayerCharacterPackageSelection.ResolveFromFiles(levelPath,
                        CharacterTestPlayerCharacterPackageSelection.IdentityCatalogPath,
                        CharacterTestPlayerCharacterPackageSelection.GeneratedRoot,
                        validatePlayable: false, includeDefault: false));
                StringAssert.Contains("package 000009", stale.Message);
                StringAssert.Contains("668367837", stale.Message);

                foreach (JToken character in characters)
                {
                    character["enabled"] = (string)character["stableId"] == "enemy.ai.02";
                    if ((string)character["stableId"] == "player.local.01") character["characterId"] = 668367837;
                }
                File.WriteAllText(levelPath, level.ToString());
                InvalidOperationException missing = Assert.Throws<InvalidOperationException>(() =>
                    CharacterTestPlayerCharacterPackageSelection.ResolveFromFiles(levelPath,
                        CharacterTestPlayerCharacterPackageSelection.IdentityCatalogPath,
                        Path.Combine(fixtureRoot, "MissingGenerated"),
                        validatePlayable: false, includeDefault: false));
                StringAssert.Contains("package 000002 is missing", missing.Message);
            }
            finally
            {
                if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, true);
            }
        }

        [Test]
        public void PackageTestCommand_StagesEverySceneSelectedCharacterPackage()
        {
            CharacterTestPlayerCharacterPackageSelection selected =
                CharacterTestPlayerCharacterPackageSelection.Resolve();
            string stagingRoot = "Assets/__CharacterPackageSelectionStage_" + Guid.NewGuid().ToString("N");
            try
            {
                foreach (CharacterTestPlayerCharacterPackageSelection.Package package in selected.Packages)
                {
                    string staged = CharacterTestPackageTestCommand.StageGeneratedPlayerContent(
                        package.SourceDirectory, stagingRoot);
                    Assert.AreEqual(
                        CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureCharacter(
                            package.SourceDirectory),
                        CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureCharacter(staged),
                        package.PackageId);
                }
            }
            finally { AssetDatabase.DeleteAsset(stagingRoot); }
        }

        [Test]
        public void PackageTestCommand_EvaluatePlayerRunReportsRequiresFinishedReports()
        {
            string root = Path.GetFullPath(Path.Combine(
                "Temp",
                "CharacterTestPackageReportTests",
                Guid.NewGuid().ToString("N")));
            try
            {
                CharacterTestPackageTestPlayerRunVerdict missing =
                    CharacterTestPackageTestCommand.EvaluatePlayerRunReports(root);
                Assert.IsFalse(missing.Passed);
                Assert.AreEqual("Missing", missing.Status);

                Directory.CreateDirectory(root);
                string runReportPath = Path.Combine(
                    root,
                    CharacterTestPerformancePlayerReportRunner.RunReportFileName);
                File.WriteAllText(runReportPath, "status: failed\nissue: expected\n");

                CharacterTestPackageTestPlayerRunVerdict failedStatus =
                    CharacterTestPackageTestCommand.EvaluatePlayerRunReports(root);
                Assert.IsFalse(failedStatus.Passed);
                Assert.AreEqual("failed", failedStatus.Status);

                string timingReportPath = Path.Combine(
                    root,
                    CharacterTestPerformancePlayerReportRunner.TimingReportFileName);
                string profilerRecorderReportPath = Path.Combine(
                    root,
                    CharacterTestPerformancePlayerReportRunner.ProfilerRecorderReportFileName);
                string animationReportPath = Path.Combine(
                    root,
                    CharacterTestPerformancePlayerReportRunner.AnimationReportFileName);
                File.WriteAllText(
                    runReportPath,
                    "status: finished\n"
                    + "scriptingBackend: IL2CPP\ndevelopmentBuild: false\n"
                    + "timingReport: " + timingReportPath + "\n"
                    + "profilerRecorderReport: " + profilerRecorderReportPath + "\n"
                    + "animationReport: " + animationReportPath + "\n");

                CharacterTestPackageTestPlayerRunVerdict missingTiming =
                    CharacterTestPackageTestCommand.EvaluatePlayerRunReports(root);
                Assert.IsFalse(missingTiming.Passed);
                Assert.AreEqual("finished", missingTiming.Status);

                File.WriteAllText(timingReportPath, "timing");
                CharacterTestPackageTestPlayerRunVerdict missingProfiler =
                    CharacterTestPackageTestCommand.EvaluatePlayerRunReports(root);
                Assert.IsFalse(missingProfiler.Passed);
                Assert.AreEqual("finished", missingProfiler.Status);

                File.WriteAllText(profilerRecorderReportPath, "profiler");
                CharacterTestPackageTestPlayerRunVerdict missingAnimation =
                    CharacterTestPackageTestCommand.EvaluatePlayerRunReports(root);
                Assert.IsFalse(missingAnimation.Passed);
                Assert.AreEqual("finished", missingAnimation.Status);

                File.WriteAllText(animationReportPath, "status: failed\nissue: expected animation\n");
                CharacterTestPackageTestPlayerRunVerdict failedAnimation =
                    CharacterTestPackageTestCommand.EvaluatePlayerRunReports(root);
                Assert.IsFalse(failedAnimation.Passed);
                Assert.AreEqual("failed", failedAnimation.Status);

                File.WriteAllText(
                    animationReportPath,
                    "scenario: test\n"
                    + "status: ready\n"
                    + "activeViewCount: 1\n"
                    + "enabledRendererCount: 1\n"
                    + "locomotionControllerCount: 1\n"
                    + "animationBackendReadyCount: 1\n");
                File.AppendAllText(animationReportPath,
                    "views:\n- entityId: 1\n  enabledRendererCount: 1\n");
                var binaryEvidence = new StringBuilder();
                int catalogIndex = 0;
                foreach (CharacterTestRuntimeCatalogArtifact artifact in CharacterTestRuntimeCatalogArtifactManifest.Artifacts)
                {
                    if (!artifact.RequiresPlayerBinary)
                        continue;
                    binaryEvidence.Append("runtimeCatalogLoad.").Append(catalogIndex).Append(".kind: ")
                        .AppendLine(artifact.CatalogKind);
                    binaryEvidence.Append("runtimeCatalogLoad.").Append(catalogIndex).AppendLine(".format: Binary");
                    catalogIndex++;
                }
                binaryEvidence.Append("runtimeCatalogLoad.count: ").AppendLine(catalogIndex.ToString());
                binaryEvidence.Append("runtimeCatalogLoad.expectedCount: ").AppendLine(catalogIndex.ToString());
                binaryEvidence.Append("runtimeCatalogLoad.binaryCount: ").AppendLine(catalogIndex.ToString());
                binaryEvidence.AppendLine("runtimeCatalogLoad.jsonCount: 0");
                binaryEvidence.AppendLine("runtimeCatalogLoad.embeddedCount: 0");
                binaryEvidence.AppendLine("runtimeCatalogLoad.missingExpectedKinds: none");
                binaryEvidence.AppendLine("runtimeCatalogLoad.nonBinaryExpectedKinds: none");
                binaryEvidence.AppendLine("runtimeCatalogLoad.allExpectedBinary: true");
                File.AppendAllText(runReportPath, binaryEvidence.ToString());
                Assert.IsFalse(CharacterTestPackageTestCommand.EvaluatePlayerRunReports(root).Passed,
                    "Idle-only legacy reports must not satisfy gameplay acceptance.");
                File.AppendAllText(runReportPath, CharacterTestPlayerGameplayPackagingTests.GoodGameplayReport);
                CharacterTestPackageTestPlayerRunVerdict passed =
                    CharacterTestPackageTestCommand.EvaluatePlayerRunReports(root);
                Assert.IsTrue(passed.Passed);
                Assert.AreEqual("finished", passed.Status);
                Assert.AreEqual(runReportPath, passed.PlayerRunReportPath);
                Assert.AreEqual(timingReportPath, passed.TimingReportPath);
                Assert.AreEqual(profilerRecorderReportPath, passed.ProfilerRecorderReportPath);
                Assert.AreEqual(animationReportPath, passed.AnimationReportPath);

                string validRunEvidence = File.ReadAllText(runReportPath);
                File.WriteAllText(runReportPath, validRunEvidence.Replace("scriptingBackend: IL2CPP", "scriptingBackend: Mono"));
                Assert.IsFalse(CharacterTestPackageTestCommand.EvaluatePlayerRunReports(root).Passed);
                File.WriteAllText(runReportPath, validRunEvidence);

                string editorReportPath = Path.Combine(root, "editor-run.txt");
                File.WriteAllText(editorReportPath,
                    "status: finished\nprocessExited: true\nprocessExitCode: 0\nbuildConfiguration: Release\nscriptingBackend: IL2CPP\n");
                Assert.IsTrue(CharacterTestPackageTestCommand.EvaluatePlayerRunReports(
                    root, editorReportPath).Passed);
                string validEditorEvidence = File.ReadAllText(editorReportPath);
                File.WriteAllText(editorReportPath, validEditorEvidence.Replace("buildConfiguration: Release", "buildConfiguration: Development"));
                Assert.IsFalse(CharacterTestPackageTestCommand.EvaluatePlayerRunReports(root, editorReportPath).Passed);
                File.WriteAllText(editorReportPath, validEditorEvidence);
                foreach (string abnormal in new[]
                {
                    "status: finished\nprocessExited: true\nprocessExitCode: 7\n",
                    "status: timeout\nprocessExited: true\nprocessExitCode: -1\n",
                    "status: finished-player-still-running-killed\nprocessExited: false\nprocessExitCode: -1\n"
                })
                {
                    File.WriteAllText(editorReportPath, abnormal);
                    Assert.IsFalse(CharacterTestPackageTestCommand.EvaluatePlayerRunReports(
                        root, editorReportPath).Passed, abnormal);
                }
                File.AppendAllText(runReportPath, "status: finished\n");
                File.WriteAllText(editorReportPath,
                    "status: finished\nprocessExited: true\nprocessExitCode: 0\nbuildConfiguration: Release\nscriptingBackend: IL2CPP\n");
                Assert.Throws<FormatException>(() =>
                    CharacterTestPackageTestCommand.EvaluatePlayerRunReports(root, editorReportPath));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void PackageTestCommand_StagesCodexFlowTestGeneratedConfigForPlayer()
        {
            string stagingRoot = "Assets/__CharacterPackageStage_" + Guid.NewGuid().ToString("N");
            try
            {
                Assert.AreEqual("Assets/StreamingAssets/MxFramework/Generated/CharacterPackages/000001",
                    CharacterTestPackageTestCommand.ResolveGeneratedPackageMirror(
                        CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot,
                        "Assets/StreamingAssets/MxFramework/Generated"));
                string stagedPath = CharacterTestPackageTestCommand.StageGeneratedPlayerContent(
                    CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot, stagingRoot);
                string stagedPackageDirectory = Path.GetFullPath(stagedPath);
                string stagedConfigDirectory = Path.Combine(stagedPackageDirectory, "config");
                string stagedCacheDirectory = Path.Combine(stagedPackageDirectory, "package_cache");
                foreach (string relative in new[] { CharacterWeaponTraceBakeStorage.WeaponPath, CharacterWeaponTraceBakeStorage.ContactPath })
                {
                    string source = Path.Combine(CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot, relative);
                    if (File.Exists(source))
                        CollectionAssert.AreEqual(File.ReadAllBytes(source), File.ReadAllBytes(Path.Combine(stagedPackageDirectory, relative)));
                }

                Assert.IsTrue(Directory.Exists(stagedPackageDirectory), stagedPackageDirectory);
                Assert.IsTrue(Directory.Exists(stagedConfigDirectory), stagedConfigDirectory);
                Assert.IsTrue(
                    File.Exists(Path.Combine(stagedConfigDirectory, "animation_set_definition.json")),
                    stagedConfigDirectory);
                Assert.IsTrue(
                    File.Exists(Path.Combine(stagedConfigDirectory, "animation_clip_registry.json")),
                    stagedConfigDirectory);
                Assert.IsTrue(
                    File.Exists(Path.Combine(stagedConfigDirectory, "character_config_patch.json")),
                    stagedConfigDirectory);
                Assert.IsTrue(
                    File.Exists(Path.Combine(stagedConfigDirectory, "geometry_binding.json")),
                    stagedConfigDirectory);
                Assert.IsTrue(
                    File.Exists(Path.Combine(stagedCacheDirectory, "import_report.json")),
                    stagedCacheDirectory);
                Assert.IsFalse(
                    File.Exists(Path.Combine(stagedCacheDirectory, "compile_result.json")),
                    "deprecated full compile snapshot must not be staged: " + stagedCacheDirectory);
                Assert.IsFalse(
                    File.Exists(Path.Combine(stagedCacheDirectory, "animation_compile_result.json")),
                    "deprecated animation compile snapshot must not be staged: " + stagedCacheDirectory);
                Assert.IsFalse(
                    File.Exists(Path.Combine(stagedCacheDirectory, "compile_summary.json")),
                    "bounded compile summary must not be staged: " + stagedCacheDirectory);

                CharacterImportedPackage package =
                    CharacterImportedPackageJson.LoadFromDirectory(stagedPackageDirectory);
                Assert.NotNull(package);
                Assert.IsTrue(
                    PackageContainsGeometryTrace(package, CharacterTestAuthoredContentIds.D403_Id554678986),
                    stagedPackageDirectory);
                Assert.IsTrue(
                    PackageContainsWeaponTraceBinding(package, "trace.iron_sword.blade", CharacterTestAuthoredContentIds.D405_Id1),
                    stagedPackageDirectory);
            }
            finally
            {
                AssetDatabase.DeleteAsset(stagingRoot);
            }
        }

        [Test]
        public void PackageTestCommand_StagingTwoPackagesPreservesEachPackage()
        {
            string root = "Assets/__CharacterPackageStage_" + Guid.NewGuid().ToString("N");
            try
            {
                string firstSource = CreatePackageStageFixture(root, "000001");
                string secondSource = CreatePackageStageFixture(root, "000008");
                string first = CharacterTestPackageTestCommand.StageGeneratedPlayerContent(firstSource, root + "/output");
                string firstReport = File.ReadAllText(first + "/package_cache/import_report.json");
                string firstMeta = File.ReadAllText(first + "/package_cache/import_report.json.meta");
                string second = CharacterTestPackageTestCommand.StageGeneratedPlayerContent(secondSource, root + "/output");
                Assert.AreEqual(root + "/output/CharacterPackages/000001", first);
                Assert.AreEqual(root + "/output/CharacterPackages/000008", second);
                Assert.AreEqual("000008", CharacterImportedPackageJson.LoadImportReportFromFile(second + "/package_cache/import_report.json").PackageId);
                CharacterTestPackageTestCommand.StageGeneratedPlayerContent(secondSource, root + "/output");
                Assert.AreEqual(firstReport, File.ReadAllText(first + "/package_cache/import_report.json"));
                Assert.AreEqual(firstMeta, File.ReadAllText(first + "/package_cache/import_report.json.meta"));
                string sourceIdentity = CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureCharacter(secondSource);
                string stagedIdentity = CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureCharacter(second);
                Assert.AreEqual(sourceIdentity, stagedIdentity);
                File.AppendAllText(secondSource + "/config/animation_set_definition.json", " ");
                Assert.AreNotEqual(sourceIdentity,
                    CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureCharacter(secondSource));
                Assert.AreNotEqual(
                    CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureCharacter(secondSource),
                    CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureCharacter(second));
                File.WriteAllText(secondSource + "/config/new_stageable.json", "{}");
                StringAssert.Contains("config/new_stageable.json",
                    CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureCharacter(secondSource));
                File.AppendAllText(second + "/config/animation_set_definition.json", " changed");
                Assert.AreNotEqual(stagedIdentity,
                    CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureCharacter(second));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void PackageTestCommand_StagesRequiredPayloadsAndOnlyExcludesExactDeprecatedPaths()
        {
            string root = "Assets/__CharacterPackageStage_" + Guid.NewGuid().ToString("N");
            try
            {
                string source = CreatePackageStageFixture(root, "000008");
                string[] excluded = {
                    "package_cache/compile_result.json", "package_cache/animation_compile_result.json", "package_cache/compile_summary.json",
                    "generated/character_application_config_patch.json", "generated/character_geometry_binding.json", "generated/character_resource_mapping.json"
                };
                foreach (string relative in excluded)
                    WritePackageStageFixtureFile(source, relative, Encoding.UTF8.GetBytes("{}"));
                string[] retained = {
                    "config/compile_result.json", "nested/package_cache/compile_result.json",
                    "generated/weapon_trace_bake_catalog.json", "generated/character_hurtbox_bake_catalog.json",
                    "generated/weapon_trace_bake_catalog.bytes", "generated/contact_motion_bake_catalog.bytes", "generated/character_hurtbox_bake_catalog.bytes"
                };
                foreach (string relative in retained)
                    WritePackageStageFixtureFile(source, relative, Encoding.UTF8.GetBytes(relative.EndsWith(".json", StringComparison.Ordinal) ? "{}" : "fixture payload"));

                string[] socketPair = { "generated/character_socket_bake_catalog.json", "generated/character_socket_bake_catalog.bytes" };
                string target = CharacterTestPackageTestCommand.StageGeneratedPlayerContent(source, root + "/output");
                foreach (string relative in excluded)
                {
                    Assert.IsTrue(File.Exists(Path.Combine(source, relative)), relative);
                    Assert.IsFalse(File.Exists(Path.Combine(target, relative)), relative);
                }
                foreach (string relative in retained)
                    CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(source, relative)), File.ReadAllBytes(Path.Combine(target, relative)), relative);
                foreach (string relative in socketPair)
                    CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(source, relative)), File.ReadAllBytes(Path.Combine(target, relative)), relative);
                CollectionAssert.AreEqual(File.ReadAllBytes(source + "/package_cache/import_report.json"), File.ReadAllBytes(target + "/package_cache/import_report.json"));
                Assert.IsFalse(CharacterTestPackageTestCommand.IsExcludedPlayerGeneratedPackageFile("package_cache/compile_result.json.bak"));
                Assert.IsTrue(CharacterTestPackageTestCommand.IsExcludedPlayerGeneratedPackageFile("package_cache\\compile_result.json"));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void PackageTestCommand_RejectsInvalidIdentityBeforeReplacingExistingStage()
        {
            string root = "Assets/__CharacterPackageStage_" + Guid.NewGuid().ToString("N");
            try
            {
                string source = CreatePackageStageFixture(root, "000008");
                string target = CharacterTestPackageTestCommand.StageGeneratedPlayerContent(source, root + "/output");
                string before = File.ReadAllText(target + "/package_cache/import_report.json");
                File.WriteAllText(source + "/package_cache/import_report.json", before.Replace("000008", "000001"));
                Assert.Throws<InvalidOperationException>(() => CharacterTestPackageTestCommand.StageGeneratedPlayerContent(source, root + "/output"));
                Assert.AreEqual(before, File.ReadAllText(target + "/package_cache/import_report.json"));
                File.WriteAllText(source + "/package_cache/import_report.json", before.Replace("000008", "../escape"));
                Assert.Throws<InvalidOperationException>(() => CharacterTestPackageTestCommand.StageGeneratedPlayerContent(source, root + "/output"));
                Assert.AreEqual(before, File.ReadAllText(target + "/package_cache/import_report.json"));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void PackageTestCommand_RejectsSourceOverlapAndOutsideAssetsStage()
        {
            string root = "Assets/__CharacterPackageStage_" + Guid.NewGuid().ToString("N");
            try
            {
                string source = CreatePackageStageFixture(root, "000008");
                string before = File.ReadAllText(source + "/package_cache/import_report.json");
                Assert.Throws<InvalidOperationException>(() => CharacterTestPackageTestCommand.StageGeneratedPlayerContent(source, source));
                Assert.Throws<ArgumentException>(() => CharacterTestPackageTestCommand.StageGeneratedPlayerContent(source, "Temp/CharacterPackageStage"));
                Assert.AreEqual(before, File.ReadAllText(source + "/package_cache/import_report.json"));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        private static void WriteSocketBakePair(string source, string packageId)
        {
            var emptySocketCatalog = new MxFramework.CharacterRuntimeSpawn.CharacterSocketBakeCatalog();
            byte[] socketPayload = MxFramework.CharacterRuntimeSpawn.CharacterSocketBakeCatalogBinary.Serialize(packageId, emptySocketCatalog);
            WritePackageStageFixtureFile(
                source,
                "generated/character_socket_bake_catalog.json",
                Encoding.UTF8.GetBytes(MxFramework.CharacterRuntimeSpawn.CharacterSocketBakeCatalogBinary.SerializeIndex(
                    packageId,
                    emptySocketCatalog,
                    socketPayload)));
            WritePackageStageFixtureFile(source, "generated/character_socket_bake_catalog.bytes", socketPayload);
        }

        private static string CreatePackageStageFixture(string root, string packageId)
        {
            string source = root + "/source/" + packageId;
            foreach (string name in new[] { "animation_set_definition", "animation_clip_registry", "character_config_patch", "geometry_binding", "resource_catalog_mapping", "unity_resource_catalog" })
                WritePackageStageFixtureFile(source, "config/" + name + ".json", Encoding.UTF8.GetBytes("{}"));
            WritePackageStageFixtureFile(source, "package_cache/import_report.json", Encoding.UTF8.GetBytes(
                "{\"format\":\"mx.characterUnityImportReport.v1\",\"packageId\":\"" + packageId + "\",\"status\":\"Ready\"}"));
            // Staging verifies the socket bake v3 pair, so every fixture carries a valid empty pair.
            WriteSocketBakePair(source, packageId);
            return source;
        }

        [Test]
        public void PackageTestCommand_RuntimePrefabMirrorPreservesGuidWhenContentChanges()
        {
            string root = "Assets/__CharacterPackageStage_" + Guid.NewGuid().ToString("N");
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                Directory.CreateDirectory(root + "/prefabs");
                AssetDatabase.ImportAsset(root, ImportAssetOptions.ImportRecursive);
                var sourceObject = new GameObject("RuntimeMirrorFixture");
                SceneManager.MoveGameObjectToScene(sourceObject, preview);
                string sourcePath = root + "/prefabs/000008_character_preview.prefab";
                PrefabUtility.SaveAsPrefabAsset(sourceObject, sourcePath);
                var mirror = typeof(MxFramework.Editor.CharacterImport.CharacterImportedPackagePrefabBuilder).GetMethod(
                    "MirrorPrefabToRuntimeResources", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(mirror);
                var first = (GameObject)mirror.Invoke(null, new object[] { root, "000008", sourcePath });
                string targetPath = AssetDatabase.GetAssetPath(first);
                string originalGuid = AssetDatabase.AssetPathToGUID(targetPath);
                byte[] originalMeta = File.ReadAllBytes(targetPath + ".meta");
                Assert.AreNotEqual(AssetDatabase.AssetPathToGUID(sourcePath), originalGuid);

                var child = new GameObject("UpdatedContent");
                SceneManager.MoveGameObjectToScene(child, preview);
                child.transform.SetParent(sourceObject.transform, false);
                PrefabUtility.SaveAsPrefabAsset(sourceObject, sourcePath);
                var updated = (GameObject)mirror.Invoke(null, new object[] { root, "000008", sourcePath });
                Assert.NotNull(updated.transform.Find("UpdatedContent"));
                Assert.AreEqual(originalGuid, AssetDatabase.AssetPathToGUID(targetPath));
                CollectionAssert.AreEqual(originalMeta, File.ReadAllBytes(targetPath + ".meta"));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.DeleteAsset(root);
            }
        }

        private static void WritePackageStageFixtureFile(string root, string relativePath, byte[] content)
        {
            string path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, content);
        }

        [Test]
        public void UnityCatalogSnapshot_RefreshesWhenAssetOrMetaChanges()
        {
            string root = "Assets/__UnityCatalogSnapshot_" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(root + "/config");
                string asset = root + "/sample.txt";
                string catalog = root + "/config/unity_resource_catalog.json";
                File.WriteAllText(asset, "sample");
                File.WriteAllText(catalog, Newtonsoft.Json.JsonConvert.SerializeObject(new {
                    format = "mx.characterUnityResourceCatalog.v1", packageId = "snapshot_fixture",
                    entries = new[] { new { id = "sample", unityAssetPath = asset, sourceFormat = "txt",
                        importStatus = "PendingUnityImport", providerData = new { assetPath = asset, sourceFormat = "txt" } } }
                }));
                AssetDatabase.ImportAsset(root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
                Type updater = typeof(MxFramework.Editor.CharacterImport.CharacterImportedPackagePrefabBuilder).Assembly.GetType(
                    "MxFramework.Editor.CharacterImport.CharacterUnityResourceCatalogUpdater");
                var refresh = updater.GetMethod("RefreshCatalog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                Action run = () => refresh.Invoke(null, new object[] { root });
                Func<Newtonsoft.Json.Linq.JToken> read = () => Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(catalog))["entries"][0];
                run();
                string firstAssetHash = (string)read()["providerData"]["unitySnapshotAssetHash"];
                string firstMetaHash = (string)read()["providerData"]["unitySnapshotMetaHash"];
                Assert.IsNotEmpty((string)read()["unityAssetGuid"]);
                StringAssert.StartsWith("sha256:", firstAssetHash);
                StringAssert.StartsWith("sha256:", firstMetaHash);
                run();
                Assert.AreEqual(firstMetaHash, (string)read()["providerData"]["unitySnapshotMetaHash"]);

                AssetImporter importer = AssetImporter.GetAtPath(asset);
                importer.userData = "changed snapshot fixture metadata";
                importer.SaveAndReimport();
                run();
                Assert.AreNotEqual(firstMetaHash, (string)read()["providerData"]["unitySnapshotMetaHash"]);
                Assert.AreEqual(firstAssetHash, (string)read()["providerData"]["unitySnapshotAssetHash"]);

                File.WriteAllText(asset, "changed sample");
                AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
                run();
                Assert.AreNotEqual(firstAssetHash, (string)read()["providerData"]["unitySnapshotAssetHash"]);
                AssetDatabase.DeleteAsset(asset);
                run();
                Assert.AreEqual("UnityMissing", (string)read()["importStatus"]);
                Assert.IsEmpty((string)read()["unityAssetGuid"]);
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void AnimationResources_TryCreateLocomotionDefinitionUsesStreamingAssetsMirror()
        {
            string sourcePath = Path.GetFullPath(
                "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/config/animation_set_definition.json");
            Assert.IsTrue(File.Exists(sourcePath), sourcePath);

            string testPackageSlug = "path_resolution_test";
            string mirrorPackageDirectory = Path.Combine(
                Application.streamingAssetsPath,
                "MxFramework",
                "Generated",
                "CharacterPackages",
                testPackageSlug);
            string mirrorConfigDirectory = Path.Combine(
                mirrorPackageDirectory,
                "config");
            string mirrorSetPath = Path.Combine(mirrorConfigDirectory, "animation_set_definition.json");
            string directSetPath =
                "Assets/MxFrameworkGenerated/CharacterPackages/"
                + testPackageSlug
                + "/config/animation_set_definition.json";
            string directClipRegistryPath =
                "Assets/MxFrameworkGenerated/CharacterPackages/"
                + testPackageSlug
                + "/config/animation_clip_registry.json";

            if (Directory.Exists(mirrorPackageDirectory))
                Directory.Delete(mirrorPackageDirectory, recursive: true);

            try
            {
                Directory.CreateDirectory(mirrorConfigDirectory);
                File.Copy(sourcePath, mirrorSetPath, overwrite: true);

                var binding = new CharacterRuntimeAnimationBinding(
                    directSetPath,
                    directClipRegistryPath,
                    new CharacterRuntimeAnimationProfileBinding(
                        "profile.test",
                        CharacterRuntimeAnimationProfileResolver.LocomotionSlotId,
                        "set.base",
                        "group.locomotion",
                        "blend.move2d",
                        CharacterAnimationBlendKind.Blend2D,
                        CharacterTestCharacterSpawnResources.SampleAnimationPackageId));

                bool result = CharacterRuntimeAnimationResources.TryCreateLocomotionDefinition(
                    binding,
                    NullRuntimeLogger.Instance,
                    out CharacterRuntimeLocomotionDefinition locomotionDefinition);

                Assert.IsTrue(result);
                Assert.NotNull(locomotionDefinition.AnimationSet);
                Assert.NotNull(locomotionDefinition.Blend2D);
                Assert.AreEqual("set.base", locomotionDefinition.AnimationSet.SetId);
                Assert.AreEqual("blend.move2d", locomotionDefinition.BlendId);
            }
            finally
            {
                if (Directory.Exists(mirrorPackageDirectory))
                    Directory.Delete(mirrorPackageDirectory, recursive: true);
            }
        }

        private static bool PackageContainsGeometryTrace(CharacterImportedPackage package, int traceId)
        {
            if (package?.Geometry == null)
                return false;

            for (int i = 0; i < package.Geometry.WeaponTraces.Length; i++)
            {
                var trace = package.Geometry.WeaponTraces[i];
                if (trace != null && trace.TraceId == traceId)
                    return true;
            }

            return false;
        }

        private static bool PackageContainsWeaponTraceBinding(
            CharacterImportedPackage package,
            string traceId,
            int slotId)
        {
            if (package?.Configs == null)
                return false;

            for (int weaponIndex = 0; weaponIndex < package.Configs.Weapons.Length; weaponIndex++)
            {
                var weapon = package.Configs.Weapons[weaponIndex];
                if (weapon == null)
                    continue;

                for (int bindingIndex = 0; bindingIndex < weapon.TraceBindings.Length; bindingIndex++)
                {
                    var binding = weapon.TraceBindings[bindingIndex];
                    if (string.Equals(binding.TraceProfileId, traceId, StringComparison.Ordinal)
                        && binding.SlotId == slotId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static CharacterTestCharacterEntity SpawnProfilerGcCharacter(
            CharacterTestCharacterRuntimeWorld world,
            int teamId,
            float x,
            float z)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                out CharacterTestCharacterDefinition definition));
            return world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId,
                    x,
                    y: 0f,
                    z,
                CharacterTestCharacterControlOwner.None),
                definition);
        }

        private static bool RunProfilerActionSlotCycle(
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterEntity entity,
            ref long frame)
        {
            if (!runtime.TryStartActionSlot(
                    entity.Id,
                    CharacterTestActionSlotIds.AttackPrimary,
                    CharacterTestCharacterActionConfigSource.LightAttackActionId,
                    new RuntimeFrame(frame)))
            {
                return false;
            }

            for (int i = 0; i < 160; i++)
            {
                frame++;
                runtime.TickEntity(entity.Id, new RuntimeFrame(frame));
                if (string.IsNullOrEmpty((entity.Action.ActiveActionId).ToString()))
                {
                    frame++;
                    return true;
                }
            }

            return string.IsNullOrEmpty((entity.Action.ActiveActionId).ToString());
        }

        private static CharacterAuthorityPoseStateComponent CreateProfilerAuthorityPoseState()
        {
            var state = new CharacterAuthorityPoseStateComponent(new CharacterAuthorityPoseRigDefinition(new[]
            {
                new CharacterAuthoritySecondaryBinding(
                    CharacterTestAuthoredContentIds.D400_Id165,
                    CharacterTestAuthoredContentIds.D400_Id22,
                    CharacterTestAuthoredContentIds.D400_Id6,
                    CharacterTestAuthoredContentIds.D400_Id12,
                    Fix64.FromRatio(22, 100),
                    Fix64.FromRatio(58, 100),
                    Fix64.FromRatio(4, 100),
                    Fix64.One),
                new CharacterAuthoritySecondaryBinding(
                    CharacterTestAuthoredContentIds.D400_Id166,
                    CharacterTestAuthoredContentIds.D400_Id22,
                    CharacterTestAuthoredContentIds.D400_Id36,
                    CharacterTestAuthoredContentIds.D400_Id47,
                    Fix64.FromRatio(22, 100),
                    Fix64.FromRatio(58, 100),
                    Fix64.FromRatio(4, 100),
                    Fix64.One),
            }));
            Assert.IsTrue(state.ApplySecondaryImpulse(
                new CharacterAuthorityPoseImpulseRequest(
                    CharacterAuthorityPoseRegionIds.Broadcast,
                    new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                    Fix64.FromInt(4),
                    durationFrames: 128,
                    correlationId: new MxFramework.Runtime.RuntimeTraceIdentity(94048),
                    matchSource: CharacterAuthorityPoseMatchSource.PreviewCommand,
                    fallbackIssue: CharacterAuthorityPoseIssue.None),
                RuntimeFrame.Zero,
                out string issue),
                issue);
            return state;
        }

        private static CharacterBodyPartContactPointConfig CreateProfilerBodyPartContactPointConfig()
        {
            return new CharacterBodyPartContactPointConfig(
                new CharacterBodyPartContactPointConfigId(9101),
                "foot.l",
                "humanoid",
                CharacterTestAuthoredContentIds.D400_Id51,
                contactPointId: 9101,
                bodyPartId: 701,
                localOffsetXMillimeters: 0,
                localOffsetYMillimeters: 0,
                localOffsetZMillimeters: 0,
                radiusMillimeters: 80,
                layerMaskProfileId: "default",
                activationMode: CharacterBodyPartContactActivationMode.Always,
                motionMode: CharacterBodyPartContactMotionMode.BakedSocket,
                motionSocketId: CharacterTestAuthoredContentIds.D400_Id50,
                usage: "PresentationAudio",
                authority: "PresentationOnly",
                sourceOrder: 0);
        }

        private static HitResolveResult CreateProfilerDamageResult(
            CharacterTestCharacterEntity attacker,
            CharacterTestCharacterEntity target,
            int traceId)
        {
            return new HitResolveResult(
                ToCombatEntityId(attacker),
                ToCombatEntityId(target),
                actionId: 1001,
                actionInstanceId: traceId,
                traceId: traceId,
                frame: new CombatFrame(3),
                HitResolveKind.Damage,
                damage: 1,
                staggerFrames: 0,
                knockback: FixVector3.Zero);
        }

        private static DamageProfileResolveResult CreateProfilerDamagePayload(HitResolveResult result)
        {
            var usage = new DamageProfileUsage(
                DamageProfileSourceKind.WeaponTrace,
                actionId: CharacterTestCharacterActionConfigSource.LightAttackActionId,
                actionInstanceId: result.ActionInstanceId,
                stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                equipmentSlotId: CharacterTestAuthoredContentIds.D400_Id12,
                weaponId: CharacterTestAuthoredContentIds.D400_Id36,
                sourceEntityId: result.AttackerId.Value,
                targetEntityId: result.TargetId.Value,
                hitZoneId: CharacterTestAuthoredContentIds.D400_Id55,
                bodyPartId: CharacterTestAuthoredContentIds.D400_Id10,
                frame: result.Frame.Value,
                localFrame: 3);
            return new DamageProfileResolveResult(
                success: true,
                reason: string.Empty,
                damageProfileId: CharacterTestAuthoredContentIds.D401_Id1,
                damageTypeId: CharacterTestAuthoredContentIds.D409_Id2,
                formulaId: DamageFormulaIds.Flat,
                incomingDamage: 1,
                staggerFrames: 0,
                impactForce: 0,
                knockback: FixVector3.Zero,
                impactPower: 0,
                guardPressure: 0,
                presentationProfileId: CharacterTestAuthoredContentIds.D407_Id1,
                usage: usage);
        }

        private static CombatEntityId ToCombatEntityId(CharacterTestCharacterEntity entity)
        {
            return new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);
        }

        private static CharacterTestCharacterPhysicsRuntime CreateProfilerGcPhysicsRuntime()
        {
            var physicsWorld = new CombatPhysicsWorld();
            RegisterProfilerStaticAabb(
                physicsWorld,
                entity: 100,
                body: 100,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)));
            return new CharacterTestCharacterPhysicsRuntime(
                physicsWorld,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Zero,
                staticColliderCount: 1,
                "profiler-gc-motion");
        }

        private static void RegisterProfilerStaticAabb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            FixVector3 position,
            FixVector3 localMin,
            FixVector3 localMax)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, position));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(collider),
                layer,
                localMin,
                localMax));
        }

        private static void SetProfilerColliderPosture(
            CharacterTestCharacterEntity entity,
            int profileId,
            int heightMillimeters,
            int radiusMillimeters,
            long frame,
            bool blocksCharacters = true,
            bool canBePushed = true)
        {
            entity.SetColliderPosture(entity.ColliderPosture.WithResolved(
                profileId,
                heightMillimeters,
                radiusMillimeters,
                centerYMillimeters: heightMillimeters / 2,
                blocksCharacters: blocksCharacters,
                canBePushed: canBePushed,
                source: CharacterTestColliderPostureSource.ActionTimeline,
                priority: 100,
                frame: frame));
        }

        private static void EnqueueProfilerRootMotion(
            CharacterTestCharacterRuntimeWorld world,
            CharacterMotionObstaclePolicy obstaclePolicy)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                entities[i].EnqueueMotionRequest(CharacterTestCharacterMotionRequest.RootMotionDeltaRaw(
                    deltaXMillimeters: 1,
                    deltaYMillimeters: 0,
                    deltaZMillimeters: 0,
                    space: CharacterTestCharacterMotionRequestSpace.World,
                    sourceActionId: CharacterTestCharacterActionConfigSource.LightAttackActionId,
                    stableEventId: CharacterTestAuthoredContentIds.D401_Id161,
                    obstaclePolicy: obstaclePolicy));
            }
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModeWarmRunReport_WritesSceneTimingAndCountersIntoTemp()
        {
            string fullPath = Path.GetFullPath(PlayModeWarmRunReportPath);
            string profilerRecorderFullPath = Path.GetFullPath(PlayModeProfilerRecorderReportPath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            if (File.Exists(profilerRecorderFullPath))
                File.Delete(profilerRecorderFullPath);

            EditorSceneManager.OpenScene(CharacterTestScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            bool managerFound = false;
            bool startAccepted = false;
            bool sliceFound = false;
            bool reportWritten = false;
            bool animationReady = false;
            string animationIssue = string.Empty;
            string issue = string.Empty;

            GameManager manager = null;
            for (int i = 0; i < 120; i++)
            {
                manager = Object.FindAnyObjectByType<GameManager>();
                if (manager?.RuntimeContext?.Slice != null)
                    break;

                yield return null;
            }

            managerFound = manager != null;
            GameSlice slice = manager?.RuntimeContext?.Slice;
            sliceFound = slice != null;

            if (manager != null && slice != null)
            {
                CharacterTestMainMenuCommandResult start = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                startAccepted = start.Success;
                issue = start.Message;
                for (int i = 0; i < 30 && manager.RuntimeContext.Paused; i++)
                    yield return null;

                long startFrame = slice.CurrentFrame.Value;
                for (int i = 0; i < PlayModeWarmupFrameCount; i++)
                    yield return null;

                long captureStartFrame = slice.CurrentFrame.Value;
                var elapsedTicks = new long[PlayModeCaptureFrameCount];
                var allocatedBytes = new long[PlayModeCaptureFrameCount];
                long stopwatchFrequency = Stopwatch.Frequency;
                using (var profilerCapture = PlayModeProfilerRecorderCapture.Start(
                    PlayModeProfilerMarkerNames,
                    PlayModeCaptureFrameCount))
                {
                    for (int i = 0; i < PlayModeCaptureFrameCount; i++)
                    {
                        _ = GC.GetAllocatedBytesForCurrentThread();
                        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
                        long tickBefore = Stopwatch.GetTimestamp();
                        yield return null;
                        long tickAfter = Stopwatch.GetTimestamp();
                        long allocationAfter = GC.GetAllocatedBytesForCurrentThread();
                        elapsedTicks[i] = Math.Max(0L, tickAfter - tickBefore);
                        allocatedBytes[i] = Math.Max(0L, allocationAfter - allocationBefore);
                        profilerCapture.Sample();
                    }

                    long endFrame = slice.CurrentFrame.Value;
                    try
                    {
                        string reportText = CharacterTestPerformanceCounterReport.CreateTimingText(
                            PlayModeWarmRunScenarioName,
                            "playmode-scene-warm-run",
                            startFrame,
                            captureStartFrame,
                            endFrame,
                            PlayModeWarmupFrameCount,
                            PlayModeCaptureFrameCount,
                            Time.fixedDeltaTime,
                            stopwatchFrequency,
                            elapsedTicks,
                            allocatedBytes,
                            slice.Services.CreateInteractionPerformanceCounterSnapshot(),
                            slice.Services.CreateRuntimePerformanceCounterSnapshot(),
                            manager.RuntimeContext.UiRuntime?.CreatePerformanceCounterSnapshot());

                        string directory = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);
                        File.WriteAllText(fullPath, reportText);

                        string profilerDirectory = Path.GetDirectoryName(profilerRecorderFullPath);
                        if (!string.IsNullOrEmpty(profilerDirectory))
                            Directory.CreateDirectory(profilerDirectory);
                        File.WriteAllText(
                            profilerRecorderFullPath,
                            profilerCapture.CreateReportText(
                                PlayModeProfilerRecorderScenarioName,
                                startFrame,
                                captureStartFrame,
                                endFrame,
                                PlayModeWarmupFrameCount,
                                PlayModeCaptureFrameCount));
                        reportWritten = File.Exists(fullPath)
                            && File.Exists(profilerRecorderFullPath);
                        CharacterTestAnimationViewDiagnostics animationDiagnostics =
                            slice.Services.CharacterRuntime.ViewBridge.CreateAnimationDiagnostics();
                        animationReady = animationDiagnostics.Ready;
                        animationIssue = animationDiagnostics.FirstIssue;
                    }
                    catch (Exception ex)
                    {
                        issue = ex.ToString();
                    }
                }
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(sliceFound, "CharacterTestScene did not create a GameSlice in Play Mode.");
            Assert.IsTrue(startAccepted, issue);
            Assert.IsTrue(reportWritten, issue);
            Assert.IsTrue(animationReady, animationIssue);

            string report = File.ReadAllText(fullPath);
            Assert.That(report, Does.StartWith("scenario: " + PlayModeWarmRunScenarioName));
            Assert.That(report, Does.Contain("profileKind: playmode-scene-warm-run"));
            Assert.That(report, Does.Contain("warmupFrames: " + PlayModeWarmupFrameCount));
            Assert.That(report, Does.Contain("captureFrames: " + PlayModeCaptureFrameCount));
            Assert.That(report, Does.Contain("timingSamples: " + PlayModeCaptureFrameCount));
            Assert.That(report, Does.Contain("elapsedMs.median: "));
            Assert.That(report, Does.Contain("elapsedMs.p95: "));
            Assert.That(report, Does.Contain("allocatedBytes.total: "));
            Assert.That(report, Does.Contain("source: CharacterTestInteraction"));
            Assert.That(report, Does.Contain("source: CharacterTestRuntime"));
            Assert.That(report, Does.Contain("source: CharacterTestUi"));

            string profilerReport = File.ReadAllText(profilerRecorderFullPath);
            Assert.That(profilerReport, Does.StartWith("scenario: " + PlayModeProfilerRecorderScenarioName));
            Assert.That(profilerReport, Does.Contain("profileKind: playmode-profiler-recorder"));
            Assert.That(profilerReport, Does.Contain("markerCount: " + PlayModeProfilerMarkerNames.Length));
            Assert.That(profilerReport, Does.Contain("marker: CharacterTest.GameManager.Simulation"));
            Assert.That(profilerReport, Does.Contain("marker: CharacterTest.GameSlice.HostTick"));
            Assert.That(profilerReport, Does.Contain("elapsedMs.p95: "));
        }

        [Test]
        public void CombatHudNetworkStatusProvider_DoesNotAllocateWhenNetworkInactive()
        {
            var ownerObject = new GameObject("CombatHudNetworkStatusProviderTest");
            CharacterTestSceneProfile profile = ScriptableObject.CreateInstance<CharacterTestSceneProfile>();
            try
            {
                var owner = ownerObject.AddComponent<TestOwner>();
                var context = new CharacterTestRuntimeContext(owner, profile);
                var sessionBuilder = new CharacterTestRuntimeSessionBuilder(owner, context, profile);
                var coordinator = new CharacterTestNetworkCoordinator(context, profile, sessionBuilder);

                CharacterTestNetworkSessionStatus status = null;
                for (int i = 0; i < 4; i++)
                    status = coordinator.CaptureCombatHudNetworkSessionStatus();
                Assert.IsNull(status);

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();

                for (int i = 0; i < 64; i++)
                    status = coordinator.CaptureCombatHudNetworkSessionStatus();

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.IsNull(status);
                Assert.AreEqual(0L, allocatedBytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void StorySnapshot_IsCachedUntilStoryEventInvalidatesIt()
        {
            using (var harness = new CharacterTestSliceTestHarness())
            {
                StoryDirectorSnapshot first = harness.Modules.StorySnapshot;
                StoryDirectorSnapshot second = harness.Modules.StorySnapshot;

                Assert.NotNull(first);
                Assert.AreSame(first, second);
                Assert.AreEqual(1, harness.Modules.StorySnapshotCreateCountForTests);

                StoryLoadGraphResult load = harness.Modules.StoryDirector.TryLoadGraph(
                    harness.Modules.StoryContent.Graph);
                Assert.IsTrue(load.Success, load.Message);

                StoryDirectorSnapshot afterEvent = harness.Modules.StorySnapshot;

                Assert.NotNull(afterEvent);
                Assert.AreNotSame(first, afterEvent);
                Assert.AreEqual(2, harness.Modules.StorySnapshotCreateCountForTests);
                Assert.AreSame(afterEvent, harness.Modules.StorySnapshot);
            }
        }

        [Test]
        public void ProceduralEffectPreviewSnapshotRefreshStopsAfterBudget()
        {
            using (var harness = new CharacterTestSliceTestHarness())
            {
                CharacterTestCharacterEntity player = harness.SpawnLocalPlayer();

                CharacterTestProceduralEffectPreviewResult result =
                    harness.Presentation.TryApplyLockedTargetProceduralEffectPreview(
                        CharacterTestProceduralEffectPreviewId.LightSecondary,
                        "test");

                Assert.IsTrue(result.Accepted, result.Message);
                Assert.AreEqual(player.Id.Value, result.TargetEntityId);

                CharacterAuthorityPoseSnapshot initial =
                    harness.Presentation.LastProceduralEffectPreviewResult.Snapshot;
                harness.Tick();
                CharacterAuthorityPoseSnapshot refreshed =
                    harness.Presentation.LastProceduralEffectPreviewResult.Snapshot;

                Assert.NotNull(initial);
                Assert.NotNull(refreshed);
                Assert.AreNotSame(initial, refreshed);

                harness.Tick(32);
                CharacterAuthorityPoseSnapshot afterBudget =
                    harness.Presentation.LastProceduralEffectPreviewResult.Snapshot;
                harness.Tick(4);

                Assert.AreSame(
                    afterBudget,
                    harness.Presentation.LastProceduralEffectPreviewResult.Snapshot);
            }
        }

        [Test]
        public void CombatHudBuffBuildInto_WithReusableBuffersMatchesPublicBuild()
        {
            using (var harness = new CharacterTestSliceTestHarness())
            {
                CharacterTestCharacterEntity player = harness.SpawnLocalPlayer();
                GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore =
                    harness.Slice.CharacterRuntime.World.GameplayComponentWorld
                        .GetOrCreateStore<GameplayComponentBuffSetComponent>();
                long frame = harness.CurrentFrame.Value;
                buffStore.Set(
                    player.GameplayEntityId,
                    new GameplayComponentBuffSetComponent(new GameplayComponentBuffEntry(
                        CharacterTestGuardAuraIds.BuffId,
                        stackCount: 1,
                        startFrame: frame,
                        endFrame: frame + CharacterTestGuardAuraIds.GuardAuraBuffDurationFrames)));

                CharacterTestFairyGuiCombatHudViewModel expected =
                    CharacterTestFairyGuiCombatHudViewModelBuilder.Build(
                        harness.Slice,
                        MxUiNullTextProvider.Instance);
                var actual = new CharacterTestFairyGuiCombatHudViewModel();
                var buffers = new CharacterTestFairyGuiCombatHudBuildBuffers();

                CharacterTestFairyGuiCombatHudViewModelBuilder.BuildInto(
                    actual,
                    harness.Slice,
                    MxUiNullTextProvider.Instance,
                    networkStatus: null,
                    hotbarSlotStates: null,
                    buffers);

                Assert.AreEqual(expected.PlayerBuffs.Count, actual.PlayerBuffs.Count);
                Assert.AreEqual(expected.PlayerBuffs[0].BuffId, actual.PlayerBuffs[0].BuffId);
                Assert.AreEqual(expected.PlayerBuffs[0].DisplayName, actual.PlayerBuffs[0].DisplayName);
                Assert.AreEqual(expected.PlayerBuffs[0].ShortName, actual.PlayerBuffs[0].ShortName);
                Assert.AreEqual(expected.PlayerBuffs[0].TimeText, actual.PlayerBuffs[0].TimeText);
                Assert.AreEqual(expected.PlayerBuffs[0].StackText, actual.PlayerBuffs[0].StackText);
                Assert.AreEqual(expected.PlayerBuffs[0].TooltipText, actual.PlayerBuffs[0].TooltipText);
            }
        }

        [Test]
        public void CombatHudBuffBuildInto_WithReusableBuffersDoesNotAllocateAfterWarmup()
        {
            using (var harness = new CharacterTestSliceTestHarness())
            {
                CharacterTestCharacterEntity player = harness.SpawnLocalPlayer();
                GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore =
                    harness.Slice.CharacterRuntime.World.GameplayComponentWorld
                        .GetOrCreateStore<GameplayComponentBuffSetComponent>();
                long frame = harness.CurrentFrame.Value;
                buffStore.Set(
                    player.GameplayEntityId,
                    new GameplayComponentBuffSetComponent(new GameplayComponentBuffEntry(
                        CharacterTestGuardAuraIds.BuffId,
                        stackCount: 1,
                        startFrame: frame,
                        endFrame: frame + CharacterTestGuardAuraIds.GuardAuraBuffDurationFrames)));
                var viewModel = new CharacterTestFairyGuiCombatHudViewModel();
                var buffers = new CharacterTestFairyGuiCombatHudBuildBuffers();

                for (int i = 0; i < 8; i++)
                {
                    CharacterTestFairyGuiCombatHudViewModelBuilder.BuildInto(
                        viewModel,
                        harness.Slice,
                        MxUiNullTextProvider.Instance,
                        networkStatus: null,
                        hotbarSlotStates: null,
                        buffers);
                }

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();

                for (int i = 0; i < 32; i++)
                {
                    CharacterTestFairyGuiCombatHudViewModelBuilder.BuildInto(
                        viewModel,
                        harness.Slice,
                        MxUiNullTextProvider.Instance,
                        networkStatus: null,
                        hotbarSlotStates: null,
                        buffers);
                }

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0L, allocatedBytes);
            }
        }

        [Test]
        public void CombatHudBuildInto_WithStableActionTextDoesNotAllocateAfterWarmup()
        {
            using (var harness = new CharacterTestSliceTestHarness())
            {
                CharacterTestCharacterEntity player = harness.SpawnLocalPlayer();
                player.SetAction(new CharacterTestCharacterActionComponent(
                    CharacterTestCharacterActionConfigSource.LightAttackActionId,
                    MxFramework.Game.CharacterTest.CharacterTestActionStateId.Active,
                    MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Startup,
                    1,
                    default,
                    string.Empty,
                    string.Empty,
                    0f,
                    0f,
                    0f));
                var viewModel = new CharacterTestFairyGuiCombatHudViewModel();
                var buffers = new CharacterTestFairyGuiCombatHudBuildBuffers();

                for (int i = 0; i < 8; i++)
                {
                    CharacterTestFairyGuiCombatHudViewModelBuilder.BuildInto(
                        viewModel,
                        harness.Slice,
                        MxUiNullTextProvider.Instance,
                        networkStatus: null,
                        hotbarSlotStates: null,
                        buffers);
                }

                Assert.AreEqual("Action: character_test.fireball Startup", viewModel.PlayerActionText);

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();

                for (int i = 0; i < 32; i++)
                {
                    CharacterTestFairyGuiCombatHudViewModelBuilder.BuildInto(
                        viewModel,
                        harness.Slice,
                        MxUiNullTextProvider.Instance,
                        networkStatus: null,
                        hotbarSlotStates: null,
                        buffers);
                }

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0L, allocatedBytes);
            }
        }

        [Test]
        public void MainMenuBuildInto_ReusesViewModelAndStableSignature()
        {
            var status = new CharacterTestMainMenuStatus
            {
                IsPaused = true,
                IsMenuVisible = true,
                MenuPausesRuntime = true,
                HasEnteredGameplay = true,
                IsNetworkSessionActive = false,
                RuntimeFrame = 17,
                Message = "Ready."
            };
            var viewModel = new CharacterTestFairyGuiMainMenuViewModel();
            CharacterTestUiTextProviderSignature textSignature =
                CharacterTestUiTextProviderSignature.Create(MxUiNullTextProvider.Instance);

            CharacterTestFairyGuiMainMenuViewModel built =
                CharacterTestFairyGuiMainMenuViewModelBuilder.BuildInto(
                    viewModel,
                    status,
                    controllerAvailable: true,
                    canOpenSettings: true,
                    canOpenAbilities: true,
                    canOpenBehaviorDeck: true,
                    MxUiNullTextProvider.Instance);
            CharacterTestFairyGuiMainMenuViewModelSignature first =
                CharacterTestFairyGuiMainMenuViewModelSignature.Create(built, textSignature);
            CharacterTestFairyGuiMainMenuViewModelSignature second =
                CharacterTestFairyGuiMainMenuViewModelSignature.Create(
                    status,
                    canStart: true,
                    canNetwork: true,
                    canAbilities: true,
                    canBehaviorDeck: true,
                    canSettings: true,
                    canRestart: true,
                    canClose: true,
                    textSignature);

            Assert.AreSame(viewModel, built);
            Assert.AreSame(status, built.Status);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void RuntimeTick_WithStableMoveInputDoesNotAllocateAfterWarmup()
        {
            using (var harness = new CharacterTestSliceTestHarness(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                InputProvider = new StableInputProvider(CreateInputSnapshot(Vector2.right))
            }))
            {
                CharacterTestCharacterEntity player = harness.SpawnLocalPlayer();
                harness.Tick(24, 1d / 60d);

                Assert.AreEqual(Vector2.right, player.Input.Move);

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();

                harness.Tick(36, 1d / 60d);

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0L, allocatedBytes);
                Assert.AreEqual(Vector2.right, player.Input.Move);
            }
        }

        [Test]
        public void TraceRecorder_StructuredRuntimeEventsDoNotAllocateWhileRecordingAfterWarmup()
        {
            CharacterTestTraceRecorder recorder = CharacterTestTraceRecorder.CreateInMemory(capacity: 64);
            var motionSource = new CharacterMotionRequestSource(CharacterTestCharacterActionConfigSource.LightAttackActionId, CharacterTestAuthoredContentIds.D401_Id161);
            var controlTrace = new CharacterTestMotionControlScaleTraceEvent(500, 1000, motionSource);
            var rootMotionTrace = new CharacterTestRootMotionDeltaTraceEvent(12, 0, -4, motionSource);
            var rootMotionActiveTrace =
                new CharacterTestRootMotionActiveTraceEvent(new MxFramework.CharacterAction.CharacterActionId(301), "katana.slash", 2, 8);
            var collisionTrace = new CharacterTestCollisionProfileBlockedTraceEvent(CharacterTestColliderPostureComponent.StandingProfileId, CharacterTestColliderPostureComponent.CrouchProfileId, 2);
            var actionInputTrace = new CharacterTestActionInputTraceEvent(
                CharacterTestActionInputTraceKind.SkillSlot,
                new MxFramework.CharacterAction.CharacterActionId(301),
                CharacterTestActionStateId.Running,
                slotIndex: 1);
            var actionOutcomeTrace = new CharacterTestActionOutcomeTraceEvent(
                CharacterTestActionOutcomeTraceKind.Accepted,
                new MxFramework.CharacterAction.CharacterActionId(301),
                "katana.light");

            for (int i = 0; i < 8; i++)
            {
                recorder.LocomotionControlScale(new RuntimeFrame(i), 1, controlTrace);
                recorder.LocomotionRootMotionDelta(new RuntimeFrame(i), 1, rootMotionTrace);
                recorder.LocomotionRootMotionActive(new RuntimeFrame(i), 1, rootMotionActiveTrace);
                recorder.LocomotionCollisionProfileBlocked(new RuntimeFrame(i), 1, collisionTrace);
                recorder.AttackInput(new RuntimeFrame(i), 1, actionInputTrace);
                recorder.AttackAction(new RuntimeFrame(i), 1, actionOutcomeTrace);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 8; i++)
            {
                recorder.LocomotionControlScale(new RuntimeFrame(i), 1, controlTrace);
                recorder.LocomotionRootMotionDelta(new RuntimeFrame(i), 1, rootMotionTrace);
                recorder.LocomotionRootMotionActive(new RuntimeFrame(i), 1, rootMotionActiveTrace);
                recorder.LocomotionCollisionProfileBlocked(new RuntimeFrame(i), 1, collisionTrace);
                recorder.AttackInput(new RuntimeFrame(i), 1, actionInputTrace);
                recorder.AttackAction(new RuntimeFrame(i), 1, actionOutcomeTrace);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        private static InputSnapshot CreateInputSnapshot(Vector2 move)
        {
            return new InputSnapshot(
                move,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                0f,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackPrimaryHeld: false,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: false,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false);
        }

        private sealed class StableInputProvider : IInputProvider
        {
            private static readonly IDisposable Scope = new ScopeHandle();

            public StableInputProvider(InputSnapshot snapshot)
            {
                Snapshot = snapshot;
                Commands = new InputCommandQueue();
            }

            public InputSnapshot Snapshot { get; }
            public InputCommandQueue Commands { get; }
            public InputContext CurrentContext => InputContext.Gameplay;

            public bool ContainsContext(InputContext context)
            {
                return context == InputContext.Gameplay;
            }

            public bool IsContextEnabled(InputContext context)
            {
                return context == InputContext.Gameplay;
            }

            public void SetContext(InputContext context)
            {
            }

            public IDisposable PushContext(
                InputContext context,
                InputContextPolicy policy = InputContextPolicy.Exclusive)
            {
                return Scope;
            }

            private sealed class ScopeHandle : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }

        private sealed class TestOwner : MonoBehaviour
        {
        }

        private sealed class PlayModeProfilerRecorderCapture : IDisposable
        {
            private readonly string[] _markerNames;
            private readonly ProfilerRecorder[] _recorders;
            private readonly long[][] _samples;
            private readonly bool[] _valid;
            private int _sampleCount;

            private PlayModeProfilerRecorderCapture(string[] markerNames, int sampleCapacity)
            {
                _markerNames = markerNames;
                _recorders = new ProfilerRecorder[markerNames.Length];
                _samples = new long[markerNames.Length][];
                _valid = new bool[markerNames.Length];
                for (int i = 0; i < markerNames.Length; i++)
                {
                    _recorders[i] = ProfilerRecorder.StartNew(
                        ProfilerCategory.Scripts,
                        markerNames[i],
                        capacity: 1);
                    _valid[i] = _recorders[i].Valid;
                    _samples[i] = new long[Math.Max(1, sampleCapacity)];
                }
            }

            /// <remarks>AllocByDesign: test-only explicit profiler evidence capture.</remarks>
            public static PlayModeProfilerRecorderCapture Start(string[] markerNames, int sampleCapacity)
            {
                return new PlayModeProfilerRecorderCapture(
                    markerNames ?? Array.Empty<string>(),
                    Math.Max(1, sampleCapacity));
            }

            public void Sample()
            {
                if (_sampleCount >= _samples[0].Length)
                    return;

                for (int i = 0; i < _recorders.Length; i++)
                {
                    long value = _valid[i] ? _recorders[i].LastValue : 0L;
                    _samples[i][_sampleCount] = Math.Max(0L, value);
                }

                _sampleCount++;
            }

            /// <remarks>AllocByDesign: creates an explicit profiler evidence report for tests/manual review.</remarks>
            public string CreateReportText(
                string scenarioName,
                long startFrame,
                long captureStartFrame,
                long frame,
                int warmupFrameCount,
                int captureFrameCount)
            {
                var builder = new StringBuilder(4096);
                builder.Append("scenario: ").AppendLine(scenarioName);
                builder.AppendLine("profileKind: playmode-profiler-recorder");
                builder.Append("startFrame: ").AppendLine(Math.Max(0L, startFrame).ToString(CultureInfo.InvariantCulture));
                builder.Append("captureStartFrame: ").AppendLine(Math.Max(0L, captureStartFrame).ToString(CultureInfo.InvariantCulture));
                builder.Append("frame: ").AppendLine(Math.Max(0L, frame).ToString(CultureInfo.InvariantCulture));
                builder.Append("warmupFrames: ").AppendLine(Math.Max(0, warmupFrameCount).ToString(CultureInfo.InvariantCulture));
                builder.Append("captureFrames: ").AppendLine(Math.Max(1, captureFrameCount).ToString(CultureInfo.InvariantCulture));
                builder.Append("sampledFrames: ").AppendLine(_sampleCount.ToString(CultureInfo.InvariantCulture));
                builder.Append("markerCount: ").AppendLine(_markerNames.Length.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("markers:");

                for (int i = 0; i < _markerNames.Length; i++)
                    AppendMarkerStats(builder, _markerNames[i], _valid[i], _samples[i], _sampleCount);

                return builder.ToString();
            }

            public void Dispose()
            {
                for (int i = 0; i < _recorders.Length; i++)
                    _recorders[i].Dispose();
            }

            private static void AppendMarkerStats(
                StringBuilder builder,
                string markerName,
                bool available,
                long[] samples,
                int sampleCount)
            {
                long[] sorted = CopyAndSort(samples, sampleCount);
                builder.Append("- marker: ").AppendLine(markerName);
                builder.Append("  available: ").AppendLine(available ? "true" : "false");
                builder.Append("  samples: ").AppendLine(Math.Max(0, sampleCount).ToString(CultureInfo.InvariantCulture));
                builder.Append("  elapsedMs.min: ").AppendLine(FormatNanoseconds(sorted.Length > 0 ? sorted[0] : 0L));
                builder.Append("  elapsedMs.median: ").AppendLine(FormatNanoseconds(Percentile(sorted, 0.5d)));
                builder.Append("  elapsedMs.mean: ").AppendLine(CalculateMeanMilliseconds(sorted).ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append("  elapsedMs.p75: ").AppendLine(FormatNanoseconds(Percentile(sorted, 0.75d)));
                builder.Append("  elapsedMs.p90: ").AppendLine(FormatNanoseconds(Percentile(sorted, 0.9d)));
                builder.Append("  elapsedMs.p95: ").AppendLine(FormatNanoseconds(Percentile(sorted, 0.95d)));
                builder.Append("  elapsedMs.max: ").AppendLine(FormatNanoseconds(sorted.Length > 0 ? sorted[sorted.Length - 1] : 0L));
            }

            private static long[] CopyAndSort(long[] samples, int sampleCount)
            {
                int safeCount = Math.Max(0, Math.Min(sampleCount, samples != null ? samples.Length : 0));
                var copy = new long[safeCount];
                for (int i = 0; i < safeCount; i++)
                    copy[i] = Math.Max(0L, samples[i]);

                Array.Sort(copy);
                return copy;
            }

            private static long Percentile(long[] sorted, double percentile)
            {
                if (sorted == null || sorted.Length == 0)
                    return 0L;

                double clamped = Math.Max(0d, Math.Min(1d, percentile));
                int index = (int)Math.Round((sorted.Length - 1) * clamped, MidpointRounding.AwayFromZero);
                return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
            }

            private static double CalculateMeanMilliseconds(long[] sorted)
            {
                if (sorted == null || sorted.Length == 0)
                    return 0d;

                double total = 0d;
                for (int i = 0; i < sorted.Length; i++)
                    total += sorted[i];

                return total / sorted.Length / 1000000d;
            }

            private static string FormatNanoseconds(long nanoseconds)
            {
                double milliseconds = Math.Max(0L, nanoseconds) / 1000000d;
                return milliseconds.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }
    }
}
