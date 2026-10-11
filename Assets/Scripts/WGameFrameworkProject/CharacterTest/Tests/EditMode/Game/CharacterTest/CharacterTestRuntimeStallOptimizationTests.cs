using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;
using Unity.Profiling;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeStallOptimizationTests
    {
        [TestCase(0)] [TestCase(9)] [TestCase(10)] [TestCase(999)] [TestCase(int.MaxValue)]
        public void ContentHashSegmentedKeysKeepExistingEncoding(int index)
        {
            var old = new RuntimeContentHashAccumulator(); var current = new RuntimeContentHashAccumulator();
            string text = "实体😀." + index.ToString(CultureInfo.InvariantCulture) + ".value";
            var key = new RuntimeHashKey("实体😀.") + index + ".value";
            old.AddInt(text, -42); current.AddInt(key, -42);
            old.AddLong(text, long.MinValue); current.AddLong(key, long.MinValue);
            old.AddDoubleQuantized(text, -0.125, 1000); current.AddDoubleQuantized(key, -0.125, 1000);
            old.AddStringStable(text, null); current.AddStringStable(key, null);
            old.AddStringStable(text, ""); current.AddStringStable(key, "");
            old.AddStringStable(text, "a\0\ud800😀"); current.AddStringStable(key, "a\0\ud800😀");
            Assert.AreEqual(old.ToHash(), current.ToHash());
        }

        [Test]
        public void ContentHashMatchesFrozenGoldenIncludingUtf16AndNulls()
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddInt("a", -3); hash.AddLong("b", long.MaxValue); hash.AddDoubleQuantized("p", -0.25, 100);
            hash.AddStringStable("文本😀", null); hash.AddStringStable("empty", "");
            hash.AddStringStable(new RuntimeHashKey("arr.") + int.MaxValue + ".末", "x\0y\ud800");
            Assert.AreEqual(1194455866938868474L, hash.ToHash());
        }

        [TestCase(0, false)] [TestCase(1, false)] [TestCase(1, true)]
        public void LiveAbilityHashMatchesRecoverySnapshotWithoutCapturingIt(int caster, bool exclude)
        {
            var registry = new AbilityDefinitionRegistry(); registry.TryRegister(Ability(1, null, null), out _);
            var module = new AbilityRuntimeModule(registry);
            module.RestoreRuntimeState(new AbilityRuntimeState(AbilityRuntimeState.CurrentSchemaVersion, module.ModuleId, registry.ComputeStableHash(),
                new[] { new AbilityRuntimeCooldownEntry(1, 1, 50), new AbilityRuntimeCooldownEntry(2, 1, 60) }));
            var participant = new CharacterTestNetworkSnapshotParticipant(null, null, abilityRuntimeModule: module);
            var expected = new RuntimeHashAccumulator(29500, 1); var snapshot = CharacterTestNetworkAbilityRuntimeSnapshot.Capture(module);
            if (exclude) CharacterTestNetworkAbilityRuntimeSnapshot.AddStateHashExcludingCaster(expected, snapshot, caster);
            else CharacterTestNetworkAbilityRuntimeSnapshot.AddStateHash(expected, snapshot, caster);
            var actual = new RuntimeHashAccumulator(29500, 1);
            typeof(CharacterTestNetworkSnapshotParticipant).GetMethod("AddLiveAbilityHash", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(participant, new object[] { actual, caster, exclude });
            Assert.AreEqual(expected.ToHash(), actual.ToHash());
            long before = participant.ComputeStateHash(RuntimeFrame.Zero);
            module.RestoreRuntimeState(new AbilityRuntimeState(AbilityRuntimeState.CurrentSchemaVersion, module.ModuleId, registry.ComputeStableHash(), Array.Empty<AbilityRuntimeCooldownEntry>()));
            Assert.AreNotEqual(before, participant.ComputeStateHash(RuntimeFrame.Zero), "Same frame number must not hide state mutations.");
        }

        [Test]
        public void InteractiveSlowUpdatesDoNotRepayEightStepsOrSkipLogicalFrames()
        {
            var driver = new CharacterTestFixedTickDriver(); var frames = new List<int>(); int frame = 0;
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(1, driver.StepInteractive(0.5, 1d / 60d, dt => { Assert.AreEqual(1d / 60d, dt); frames.Add(++frame); }, budgetMilliseconds: 0));
            Assert.AreEqual(10, frame); CollectionAssert.AreEqual(new[] { 1,2,3,4,5,6,7,8,9,10 }, frames);
            Assert.Greater(driver.DiscardedWallSeconds, 4.8);
            Assert.Less(driver.AccumulatorSeconds, 1d / 60d);
            Assert.AreEqual(0, driver.StepInteractive(0, 1d / 60d, _ => Assert.Fail("No wall-time debt should remain.")));
        }

        [Test]
        public void InteractivePacingPreservesFractionAndLegacyModeStillCatchesUp()
        {
            var driver = new CharacterTestFixedTickDriver();
            Assert.AreEqual(1, driver.StepInteractive(0.055, 0.02, _ => { }, budgetMilliseconds: 0));
            Assert.AreEqual(0.015, driver.AccumulatorSeconds, 0.0000001);
            Assert.AreEqual(1, driver.StepInteractive(0.005, 0.02, _ => { }, budgetMilliseconds: 0));
            driver.Reset(); Assert.AreEqual(0, driver.DiscardedWallSeconds);
            Assert.AreEqual(3, driver.Step(0.06, 0.02, _ => { }));
        }

        [Test]
        public void ActualLoopbackWrapperUsesInteractivePolicyAndReplayIsExcluded()
        {
            using var runner = new CharacterTestNetworkLoopbackRunner(NullRuntimeLogger.Instance, createServerViews: false);
            Assert.IsTrue(GameManager.UsesInteractiveLabPacing(true, false, runner));
            Assert.IsFalse(GameManager.UsesInteractiveLabPacing(true, true, runner));
            Assert.IsFalse(GameManager.UsesInteractiveLabPacing(false, false, runner));
            Assert.IsFalse(GameManager.UsesInteractiveLabPacing(true, false, new OtherNetworkRunner()));
        }
        private sealed class OtherNetworkRunner : ICharacterTestNetworkRunner
        {
            public GameSlice Slice => null;
            public void Tick(double deltaTime) { }
            public CharacterTestNetworkLoopbackDiagnostics CaptureDiagnostics() => default;
            public void Dispose() { }
        }

        [Test]
        public void LabCaptureReusesCurrentPlannerSnapshotWithoutChangingEitherFrameHeader()
        {
            using var runner = new CharacterTestNetworkLoopbackRunner(NullRuntimeLogger.Instance, createServerViews: false);
            for (int i = 0; i < 20; i++) runner.Tick(1d / 60d);
            var slice = runner.ServerSlice; var participant = slice.Services.Network.CreateSnapshotParticipant();
            var module = slice.RuntimeAiPlannerCommands; var snapshot = module.CreateStateSnapshot();
            var cursor = slice.CurrentFrame;
            long expected = participant.ComputeStateHash(cursor);
            Assert.AreEqual(expected, participant.ComputeCapturedStateHash(cursor, module, snapshot));
            Assert.AreNotEqual(expected, participant.ComputeStateHash(new RuntimeFrame(cursor.Value - 1)), "Completed-frame and next-cursor headers remain distinct.");
            runner.Tick(1d / 60d);
            var next = module.CreateStateSnapshot();
            Assert.AreEqual(participant.ComputeStateHash(slice.CurrentFrame), participant.ComputeCapturedStateHash(slice.CurrentFrame, module, next));
        }

        [Test]
        public void DefinitionsAreOwnedAndStableHashInvalidatesOnRegistration()
        {
            var tags = new[] { 900500 }; var costs = new[] { new AbilityCostConfig(1, 5) };
            var first = Ability(1, costs, tags); var registry = new AbilityDefinitionRegistry();
            Assert.IsTrue(registry.TryRegister(first, out _)); long hash = registry.ComputeStableHash();
            tags[0] = 900501; costs[0] = new AbilityCostConfig(1, 99);
            Assert.AreEqual(900500, first.Damage.Tags[0]); Assert.AreEqual(5, first.Costs[0].Amount);
            Assert.AreEqual(hash, registry.ComputeStableHash());
            Assert.Throws<NotSupportedException>(() => ((IList<int>)first.Damage.Tags)[0] = 900502);
            Assert.Throws<NotSupportedException>(() => ((IList<AbilityCostConfig>)first.Costs)[0] = costs[0]);
            Assert.IsTrue(registry.TryRegister(Ability(2, null, null), out _)); Assert.AreNotEqual(hash, registry.ComputeStableHash());
            var expected = new RuntimeContentHashAccumulator(); var ordered = registry.CreateSnapshot(); expected.AddInt("ability.definition.count", ordered.Count);
            for (int i = 0; i < ordered.Count; i++) ordered[i].Contribute(expected, "ability.definition." + i);
            Assert.AreEqual(expected.ToHash(), registry.ComputeStableHash());
        }

        [Test]
        public void DirectPoseHashEqualsOwnedSnapshotBeforeAfterTickAndRestore()
        {
            var rig = new CharacterAuthorityPoseRigDefinition(new[] {
                new CharacterAuthoritySecondaryBinding(1201201, 1201202, 1201202, CharacterTestAuthoredContentIds.D400_Id12, Fix64.FromRatio(22,100), Fix64.FromRatio(58,100), Fix64.FromRatio(4,100), Fix64.FromRatio(9,1000)) });
            var pose = new CharacterAuthorityPoseStateComponent(rig);
            pose.ApplySecondaryImpulse(new CharacterAuthorityPoseImpulseRequest(1201202, new FixVector3(Fix64.One, Fix64.One, Fix64.One), Fix64.FromInt(4), 5, new MxFramework.Runtime.RuntimeTraceIdentity(94004), CharacterAuthorityPoseMatchSource.PreviewCommand, CharacterAuthorityPoseIssue.None), RuntimeFrame.Zero, out _);
            for (int frame = 0; frame < 8; frame++)
            {
                pose.Tick(new RuntimeFrame(frame)); var snapshot = pose.CreateSnapshot();
                var expected = new RuntimeHashAccumulator(29500, 1); SnapshotPoseHash(expected, 1, snapshot, pose.Definition);
                var actual = new RuntimeHashAccumulator(29500, 1); pose.AddStateHash(actual, 1); Assert.AreEqual(expected.ToHash(), actual.ToHash());
                pose.RestoreSnapshot(snapshot); var restored = new RuntimeHashAccumulator(29500, 1); pose.AddStateHash(restored, 1); Assert.AreEqual(actual.ToHash(), restored.ToHash());
            }
        }

        [Test]
        public void WarmDefinitionAndDirectPoseContributionsDoNotAllocate()
        {
            var registry = new AbilityDefinitionRegistry(); registry.TryRegister(Ability(1, null, null), out _); registry.ComputeStableHash();
            var pose = new CharacterAuthorityPoseStateComponent(CharacterAuthorityPoseRigDefinition.Empty); var hash = new RuntimeHashAccumulator(29500, 1);
            for (int i = 0; i < 20; i++) { pose.AddStateHash(hash, 1); registry.ComputeStableHash(); }
            using (var positive = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            { GC.KeepAlive(new byte[8192]); positive.Stop(); Assert.Greater(positive.Count, 0); }
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            for (int i = 0; i < 100; i++) { pose.AddStateHash(hash, 1); registry.ComputeStableHash(); }
            recorder.Stop(); Assert.AreEqual(0, recorder.Count);
        }

        public static object RunControlledHashBenchmark()
        {
            var definitions = CharacterTestAbilityCatalogSource.CreateRegistry();
            var pose = new CharacterAuthorityPoseStateComponent(new CharacterAuthorityPoseRigDefinition(new[] {
                new CharacterAuthoritySecondaryBinding(1201201, 1201202, 1201202, CharacterTestAuthoredContentIds.D400_Id12, Fix64.FromRatio(22,100), Fix64.FromRatio(58,100), Fix64.FromRatio(4,100), Fix64.FromRatio(9,1000)) }));
            Func<long> oldDefinitions = () => {
                var hash = new RuntimeContentHashAccumulator(); var values = definitions.CreateSnapshot(); hash.AddInt("ability.definition.count", values.Count);
                for (int i = 0; i < values.Count; i++) values[i].Contribute(hash, "ability.definition." + i);
                return hash.ToHash(); };
            Func<long> oldPose = () => { var hash = new RuntimeHashAccumulator(29500, 1); SnapshotPoseHash(hash, 1, pose.CreateSnapshot(), pose.Definition); return hash.ToHash(); };
            Func<long> newPose = () => { var hash = new RuntimeHashAccumulator(29500, 1); pose.AddStateHash(hash, 1); return hash.ToHash(); };
            Assert.AreEqual(oldDefinitions(), definitions.ComputeStableHash()); Assert.AreEqual(oldPose(), newPose());
            var rows = new List<object>();
            void Measure(string name, Func<long> operation)
            {
                for (int i = 0; i < 3; i++) operation();
                using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 65536, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                var timer = System.Diagnostics.Stopwatch.StartNew(); long last = 0;
                for (int i = 0; i < 16; i++) last = operation();
                timer.Stop(); recorder.Stop(); int count = recorder.Count; long allocationEvents = 0;
                for (int i = 0; i < Math.Min(count, recorder.Capacity); i++) allocationEvents += recorder.GetSample(i).Count;
                rows.Add(new { name, iterations = 16, millisecondsPerCall = timer.Elapsed.TotalMilliseconds / 16,
                    allocationEventsPerCall = allocationEvents / 16d, recorderUnit = recorder.UnitType.ToString(),
                    allocationCaptureComplete = !recorder.WrappedAround && count <= recorder.Capacity,
                    hash = last.ToString(CultureInfo.InvariantCulture) });
            }
            Measure("definition-full-recompute", oldDefinitions); Measure("definition-owned-cache", definitions.ComputeStableHash);
            Measure("pose-snapshot-restore-v3", oldPose); Measure("pose-direct-v3", newPose);
            var result = new { mode = "EditMode controlled hash paths; not a complete gameplay frame", unity = UnityEngine.Application.unityVersion, rows };
            string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Library/RuntimeStallEvidence"));
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "controlled-hash-benchmark.json"), Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return result;
        }

        private static void SnapshotPoseHash(RuntimeHashAccumulator accumulator, int key,
            CharacterAuthorityPoseSnapshot snapshot, CharacterAuthorityPoseRigDefinition definition)
        {
            // Exercise an independent owner restored from the captured snapshot. Protocol v3 is
            // compared on both paths; the separate content golden above retains the legacy encoding.
            var restoredOwner = new CharacterAuthorityPoseStateComponent(definition);
            restoredOwner.RestoreSnapshot(snapshot);
            restoredOwner.AddStateHash(accumulator, key);
        }

        private static AbilityDefinitionConfig Ability(int id, AbilityCostConfig[] costs, int[] tags)
            => new AbilityDefinitionConfig(id, "ability." + id, "Ability", AbilityInputBindingConfig.Empty, AbilityCooldownConfig.None,
                costs, 0, AbilityActionBindingConfig.Empty, new AbilityDamageConfig(1201203, 10, 1, tags, 1201204, damageTypeLabel: "damage", analysisCategoryLabel: "test"), 0, 0);
    }
}
