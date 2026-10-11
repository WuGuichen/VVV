using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.NoEngineTests.Determinism
{
    public sealed class FrameDeterminismTests
    {
        private const int Frames = 64;
        private const double FixedDelta = 1d / 60d;

        [Test]
        public void SameInitialStateAndFrameInputs_MatchEveryFrame()
        {
            var first = new Fixture();
            var second = new Fixture();
            for (int frame = 0; frame < Frames; frame++)
                FrameTrace.Equal(frame, first.Tick(), second.Tick(), "preloaded canonical commands");
        }

        [TestCase(0)]
        [TestCase(23)]
        public void SnapshotRestore_ContinuesRandomCommandSequenceAndFutureAllocation(int boundary)
        {
            var baseline = new Fixture();
            for (int i = 0; i < boundary; i++) baseline.Tick();
            var restored = new Fixture();
            restored.Clock.Reset(baseline.Clock.CurrentFrame);
            restored.Random.RestoreState(baseline.Random.CaptureState());
            restored.Commands.RestoreSnapshot(baseline.Commands.CaptureSnapshot());
            restored.Entities.RestoreSnapshot(baseline.Entities.CreateSnapshot());
            // This positive contract has no destroyed slots. Hole/generation history has a separate red regression.
            for (int frame = boundary; frame < Frames; frame++)
                FrameTrace.Equal(frame, baseline.Tick(), restored.Tick(), "snapshot boundary=" + boundary);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void RenderPacing_ChangesAdmittedTicksPerUpdateButNotLogicAtSameFrame(int schedule)
        {
            var baseline = new Fixture();
            var paced = new Fixture();
            var expected = new List<RuntimeSnapshotValue[]>();
            for (int i = 0; i < Frames; i++) expected.Add(baseline.Tick());
            var driver = new CharacterTestFixedTickDriver { CanStepPredicate = () => paced.Clock.CurrentFrame.Value < Frames };
            double[] deltas = schedule == 0 ? new[] { 1d / 240d, 1d / 30d, 0d, 1d / 120d } : new[] { 1d, 0d, 1d / 120d };
            int updates = 0;
            while (paced.Clock.CurrentFrame.Value < Frames && updates < 2048)
            {
                driver.Step(deltas[updates++ % deltas.Length], FixedDelta, admittedDelta =>
                {
                    Assert.AreEqual(FixedDelta, admittedDelta);
                    long frame = paced.Clock.CurrentFrame.Value;
                    FrameTrace.Equal(frame, expected[(int)frame], paced.Tick(), "schedule=" + schedule);
                });
            }
            Assert.AreEqual(Frames, paced.Clock.CurrentFrame.Value, "Pacing failed to admit the complete trace.");
            if (schedule == 1) Assert.Greater(driver.DiscardedWallSeconds, 0d);
        }

        [Test]
        public void FirstDivergence_DiagnosticContainsFrameFieldAndInputs()
        {
            AssertionException failure = Assert.Throws<AssertionException>(() => FrameTrace.Equal(17,
                new[] { FrameTrace.Value("random.drawCount", 5) },
                new[] { FrameTrace.Value("random.drawCount", 6) }, "command=1"));
            StringAssert.Contains("frame=17 field=random.drawCount expected=5 actual=6", failure.Message);
            StringAssert.Contains("inputs=command=1", failure.Message);
        }

        // This fixture drives actual core owners, not a replica of CharacterTest gameplay.
        private sealed class Fixture
        {
            internal readonly RuntimeClock Clock = new RuntimeClock();
            internal readonly DeterministicRandom Random = new DeterministicRandom(771u);
            internal readonly RuntimeCommandBuffer Commands = new RuntimeCommandBuffer();
            internal readonly GameplayEntityLifecycle Entities = new GameplayEntityLifecycle();
            internal Fixture()
            {
                for (int frame = 0; frame < Frames; frame++)
                {
                    Assert.IsTrue(Commands.Enqueue(new RuntimeCommand(new RuntimeFrame(frame), 1, 10, 1,
                        payload0: frame, traceId: new RuntimeTraceIdentity(91090001))).Success);
                    if (frame % 7 == 0)
                        Assert.IsTrue(Commands.Enqueue(new RuntimeCommand(new RuntimeFrame(frame), 2, 20, 1,
                            traceId: new RuntimeTraceIdentity(91090002))).Success);
                }
            }
            internal RuntimeSnapshotValue[] Tick()
            {
                RuntimeFrame frame = Clock.CurrentFrame;
                IReadOnlyList<RuntimeCommand> drained = Commands.DrainForFrame(frame);
                var values = new List<RuntimeSnapshotValue>();
                for (int i = 0; i < drained.Count; i++)
                {
                    values.Add(FrameTrace.Value("command[" + i + "].id", drained[i].CommandId));
                    values.Add(FrameTrace.Value("command[" + i + "].sequence", drained[i].Sequence));
                    values.Add(FrameTrace.Value("command[" + i + "].payload0", drained[i].Envelope.Data0));
                    Random.NextInt(-1000, 1000);
                    if (drained[i].CommandId == 2) Entities.Create();
                }
                values.Add(FrameTrace.Value("command.count", drained.Count));
                RuntimeCommandBufferSnapshot queue = Commands.CaptureSnapshot();
                values.Add(FrameTrace.Value("command.pending", queue.PendingCount));
                values.Add(FrameTrace.Value("command.nextSequence", queue.NextSequence));
                RuntimeRandomState random = Random.CaptureState();
                values.Add(FrameTrace.Value("random.algorithm", random.AlgorithmId));
                values.Add(FrameTrace.Value("random.seed", random.Seed));
                values.Add(FrameTrace.Value("random.state", random.State));
                values.Add(FrameTrace.Value("random.drawCount", random.DrawCount));
                GameplayEntityId[] entities = Entities.CreateSnapshot();
                for (int i = 0; i < entities.Length; i++)
                {
                    values.Add(FrameTrace.Value("entity[" + i + "].index", entities[i].Index));
                    values.Add(FrameTrace.Value("entity[" + i + "].generation", entities[i].Generation));
                }
                values.Add(FrameTrace.Value("frame", Clock.Step().Value));
                return values.ToArray();
            }
        }
    }

    public sealed class AllocatorDeterminismRegressionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SnapshotRestore_MustPreserveFutureEntityIndexAndGeneration(bool useSaveProvider)
        {
            var source = new GameplayComponentWorld();
            GameplayEntityId first = source.Registry.CreateEntity();
            GameplayEntityId second = source.Registry.CreateEntity();
            source.Registry.CreateEntity();
            Assert.IsTrue(source.Registry.DestroyEntity(second));
            Assert.IsTrue(source.Registry.DestroyEntity(first));
            var restored = new GameplayComponentWorld();
            if (useSaveProvider)
            {
                RuntimeSaveStateResult<RuntimeSaveState> saved = new GameplayComponentWorldSaveStateProvider(source).CaptureSaveState();
                Assert.IsTrue(saved.Success);
                RuntimeSaveStateResult<bool> load = new GameplayComponentWorldSaveStateProvider(restored).RestoreSaveState(saved.Value);
                Assert.IsTrue(load.Success, load.Error.ToString());
            }
            else restored.Registry.Lifecycle.RestoreAllocationState(source.Registry.Lifecycle.CaptureAllocationState());
            // A live-entity-only hash would agree here; continuation is the missing contract.
            CollectionAssert.AreEqual(source.CreateEntitySnapshot(), restored.CreateEntitySnapshot());
            GameplayEntityId expected = source.Registry.CreateEntity();
            GameplayEntityId actual = restored.Registry.CreateEntity();
            FrameTrace.Equal(1, new[] { FrameTrace.Value("allocator.next.index", expected.Index), FrameTrace.Value("allocator.next.generation", expected.Generation) },
                new[] { FrameTrace.Value("allocator.next.index", actual.Index), FrameTrace.Value("allocator.next.generation", actual.Generation) }, "destroy=[2,1]; create; saveProvider=" + useSaveProvider);
        }
    }
}
