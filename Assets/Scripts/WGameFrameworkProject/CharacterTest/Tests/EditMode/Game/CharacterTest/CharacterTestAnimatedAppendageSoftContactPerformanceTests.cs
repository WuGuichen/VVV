using System;
using System.Diagnostics;
using System.IO;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using Unity.Profiling;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestAnimatedAppendageSoftContactPerformanceTests
    {
        [Test]
        public void WarmedEightDragonOwnersByThreeSources_Warm300Measure1800_ReportsTimingAndZeroBytes()
        {
            CharacterTestCharacterEntity[] roster = new CharacterTestCharacterEntity[9];
            for (int i = 0; i < roster.Length; i++)
                roster[i] = AnimatedAppendageSoftContactFixture.CreateEntity(i + 1, 0f);

            var previous = new CharacterTestAnimatedAppendageLeafWitness[24];
            var current = new CharacterTestAnimatedAppendageLeafWitness[24];
            for (int owner = 0; owner < 8; owner++)
            {
                for (int leaf = 0; leaf < 3; leaf++)
                {
                    int index = owner * 3 + leaf;
                    CombatPhysicsShape shape = CombatPhysicsShape.Sphere(
                        new FixVector3(Fix64.Zero, AnimatedAppendageSoftContactFixture.Meters(0.9f), Fix64.Zero),
                        Fix64.One);
                    previous[index] = AnimatedAppendageSoftContactFixture.Witness(
                        owner + 1,
                        "hurt.performance." + leaf,
                        1000 + leaf,
                        "part" + leaf,
                        shape,
                        1);
                    current[index] = AnimatedAppendageSoftContactFixture.WithFrame(previous[index], 2);
                }
            }

            CharacterTestCharacterPhysicsRuntime physics = AnimatedAppendageSoftContactFixture.CreatePhysics();
            var resolver = new CharacterTestCharacterContactResolver(null);
            for (int i = 0; i < 300; i++)
                RunFrame(resolver, roster, physics, previous, current, out _, out _);
            Assert.AreEqual(192, resolver.CaptureAnimatedAppendageSoftContactDiagnostics().ConstraintCount);

            var resolveTicks = new long[1800];
            var applyTicks = new long[1800];
            var wholeTicks = new long[1800];
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool allSucceeded = true;
            for (int i = 0; i < 1800; i++)
            {
                RunFrame(
                    resolver,
                    roster,
                    physics,
                    previous,
                    current,
                    out resolveTicks[i],
                    out applyTicks[i]);
                wholeTicks[i] = resolveTicks[i] + applyTicks[i];
                CharacterTestAnimatedAppendageSoftContactDiagnostics diagnostics =
                    resolver.CaptureAnimatedAppendageSoftContactDiagnostics();
                allSucceeded &= diagnostics.Status
                    == CharacterTestAnimatedAppendageSoftContactBuildStatus.Success;
                allSucceeded &= diagnostics.ConstraintCount == 192;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(allSucceeded);
            Assert.AreEqual(0L, allocated);

            WriteTiming("query_build_pressure", resolveTicks);
            WriteTiming("apply_contact_state", applyTicks);
            WriteTiming("whole_contact_chain", wholeTicks);
            TestContext.Out.WriteLine(
                "lcc_m5_t3_perf owners=8 player=1 sources_per_owner=3"
                + " warm=300 measure=1800 constraints=192 allocated_bytes=" + allocated);
        }

        [Test]
        public void FormalV7EightDragonRuntimeModule_Warm300Measure1800_WholeTickAndStagesAreZeroBytes()
        {
            CharacterTestCharacterDefinition dragon = FindDefinition("000003");
            CharacterTestCharacterDefinition player = FindDefinition("000001");
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(
                Path.GetFullPath(dragon.GeneratedPackageRoot));
            Assert.NotNull(package?.Geometry);
            Assert.AreEqual("mx.characterGeometryBinding.v8", package.Geometry.Format);
            Assert.AreEqual(3, package.Geometry.AnimatedHurtboxSources.Length);

            var physics = new CharacterTestCharacterPhysicsRuntime(
                CharacterTestAuthorityPoseTerrainTests.CreateGroundWorld(),
                CombatPhysicsLayerMask.FromLayer(0),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                staticColliderCount: 1,
                sourceDescription: "lcc-m5-t3.formal-runtime-performance");
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physics,
                traceRecorder: null,
                createViews: false);
            using (var host = new RuntimeHost())
            using (var motionRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts,
                "CharacterTest.CharacterRuntime.Motion",
                capacity: 1))
            using (var authorityRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts,
                "CharacterTest.CharacterRuntime.AuthorityPose",
                capacity: 1))
            {
                host.RegisterModule(module);
                host.Initialize();
                host.Start();
                for (int i = 0; i < 8; i++)
                {
                    Assert.IsTrue(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                        dragon.CharacterId,
                        teamId: 1,
                        x: 0f,
                        y: 0f,
                        z: 0f,
                        controlOwner: CharacterTestCharacterControlOwner.None)));
                }
                Assert.IsTrue(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    player.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.None)));

                int frame = 0;
                Tick(host, ref frame);
                Assert.AreEqual(9, module.Entities.Count);
                for (int i = 0; i < 300; i++)
                    Tick(host, ref frame);

                var wholeTicks = new long[1800];
                var motionNanoseconds = new long[1800];
                var authorityNanoseconds = new long[1800];
                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1800; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    Tick(host, ref frame);
                    wholeTicks[i] = Stopwatch.GetTimestamp() - start;
                    motionNanoseconds[i] = motionRecorder.Valid
                        ? Math.Max(0L, motionRecorder.LastValue)
                        : 0L;
                    authorityNanoseconds[i] = authorityRecorder.Valid
                        ? Math.Max(0L, authorityRecorder.LastValue)
                        : 0L;
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.AreEqual(0L, allocated);
                WriteTiming("formal_runtime_module_whole_tick", wholeTicks);
                WriteNanosecondTiming(
                    "motion_including_sync1",
                    motionNanoseconds,
                    motionRecorder.Valid);
                WriteNanosecondTiming(
                    "authority_pose_sync2_m5",
                    authorityNanoseconds,
                    authorityRecorder.Valid);
                TestContext.Out.WriteLine(
                    "lcc_m5_t3_formal_runtime owners=8 player=1 sources_per_owner=3"
                    + " warm=300 measure=1800 allocated_bytes=" + allocated
                    + " final_frame=" + frame);
            }
        }

        private static void RunFrame(
            CharacterTestCharacterContactResolver resolver,
            CharacterTestCharacterEntity[] roster,
            CharacterTestCharacterPhysicsRuntime physics,
            CharacterTestAnimatedAppendageLeafWitness[] previous,
            CharacterTestAnimatedAppendageLeafWitness[] current,
            out long resolveTicks,
            out long applyTicks)
        {
            resolver.Clear();
            long start = Stopwatch.GetTimestamp();
            resolver.ResolveAnimatedAppendageSoftContacts(
                roster,
                physics,
                previous,
                previous.Length,
                previousReady: true,
                current,
                current.Length,
                currentReady: true,
                runSpeedMillimetersPerSecond: 6000,
                accelerationMillimetersPerSecondSquared: 18000,
                tickRate: 60);
            long resolved = Stopwatch.GetTimestamp();
            resolver.ApplyCharacterContactState(roster);
            long applied = Stopwatch.GetTimestamp();
            resolveTicks = resolved - start;
            applyTicks = applied - resolved;
        }

        private static void WriteTiming(string stage, long[] ticks)
        {
            Array.Sort(ticks);
            TestContext.Out.WriteLine(
                "lcc_m5_t3_timing stage=" + stage
                + " p50_us=" + ToMicroseconds(ticks[899]).ToString("F3")
                + " p95_us=" + ToMicroseconds(ticks[1709]).ToString("F3")
                + " max_us=" + ToMicroseconds(ticks[1799]).ToString("F3"));
        }

        private static double ToMicroseconds(long ticks)
        {
            return ticks * 1000000d / Stopwatch.Frequency;
        }

        private static void WriteNanosecondTiming(string stage, long[] nanoseconds, bool markerValid)
        {
            Array.Sort(nanoseconds);
            TestContext.Out.WriteLine(
                "lcc_m5_t3_profiler stage=" + stage
                + " valid=" + markerValid
                + " p50_us=" + (nanoseconds[899] / 1000d).ToString("F3")
                + " p95_us=" + (nanoseconds[1709] / 1000d).ToString("F3")
                + " max_us=" + (nanoseconds[1799] / 1000d).ToString("F3"));
        }

        private static void Tick(RuntimeHost host, ref int frame)
        {
            host.Tick(frame, 1d / 60d, frame / 60d);
            frame++;
        }

        private static CharacterTestCharacterDefinition FindDefinition(string packageId)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            for (int i = 0; i < registry.Definitions.Count; i++)
            {
                CharacterTestCharacterDefinition definition = registry.Definitions[i];
                if (definition.GeneratedPackageRoot.Replace('\\', '/').EndsWith(
                        "/" + packageId,
                        StringComparison.OrdinalIgnoreCase))
                    return definition;
            }
            Assert.Fail("Generated package was not discovered: " + packageId);
            return default;
        }
    }
}
