using System;
using MxFramework.AI;
using MxFramework.Story;
using MxFramework.Story.RuntimeAiPlannerBridge;
using NUnit.Framework;

namespace MxFramework.Tests.StoryRuntimeAiPlannerBridge
{
    public sealed class StoryRuntimeAiWorldStateProjectorTests
    {
        private static readonly StoryFactKey BoolFact = new StoryFactKey(10, 1);
        private static readonly StoryFactKey IntFact = new StoryFactKey(10, 2);
        private static readonly StoryFactKey SkippedFact = new StoryFactKey(10, 99);
        private static readonly AiFactKey BoolAiFact = new AiFactKey("story.bool");
        private static readonly AiFactKey IntAiFact = new AiFactKey("story.int");

        [Test]
        public void Project_Blackboard_WritesOnlyWhitelistedFacts()
        {
            var blackboard = new StoryBlackboard();
            blackboard.Set(BoolFact, StoryValue.FromBool(true));
            blackboard.Set(IntFact, StoryValue.FromInt32(7));
            blackboard.Set(SkippedFact, StoryValue.FromInt32(99));
            var worldState = new AiWorldState();
            var diagnostics = new StoryRuntimeAiProjectionDiagnostics();

            StoryRuntimeAiProjectionResult result = StoryRuntimeAiWorldStateProjector.Project(
                blackboard,
                worldState,
                Profile(),
                diagnostics);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.ProjectedCount);
            Assert.AreEqual(1, result.SkippedFactCount);
            Assert.IsTrue(worldState.TryGetValue(BoolAiFact, out bool boolValue));
            Assert.IsTrue(boolValue);
            Assert.IsTrue(worldState.TryGetValue(IntAiFact, out int intValue));
            Assert.AreEqual(7, intValue);
            Assert.IsFalse(worldState.Contains(new AiFactKey("story.unlisted")));
            Assert.AreEqual(1, diagnostics.SkippedFactCount);
        }

        [Test]
        public void Project_MissingFact_RemovesStaleAiFactAndReportsDiagnostic()
        {
            var blackboard = new StoryBlackboard();
            blackboard.Set(BoolFact, StoryValue.FromBool(true));
            var worldState = new AiWorldState();
            worldState.SetValue(IntAiFact, 42);
            var diagnostics = new StoryRuntimeAiProjectionDiagnostics();

            StoryRuntimeAiProjectionResult result = StoryRuntimeAiWorldStateProjector.Project(
                blackboard,
                worldState,
                Profile(),
                diagnostics);

            Assert.AreEqual(1, result.ProjectedCount);
            Assert.AreEqual(1, result.MissingFactCount);
            Assert.IsFalse(worldState.Contains(IntAiFact));
            Assert.AreEqual(1, diagnostics.MissingFactCount);
            Assert.AreEqual(StoryRuntimeAiProjectionDiagnosticCode.MissingStoryFact, diagnostics.Recent[0].Code);
        }

        [Test]
        public void Project_UnsupportedValueKind_RemovesStaleAiFactAndReportsDiagnostic()
        {
            var blackboard = new StoryBlackboard();
            blackboard.Set(BoolFact, StoryValue.FromBool(true));
            blackboard.Set(IntFact, StoryValue.FromStringRef(9001));
            var worldState = new AiWorldState();
            worldState.SetValue(IntAiFact, 42);
            var diagnostics = new StoryRuntimeAiProjectionDiagnostics();

            StoryRuntimeAiProjectionResult result = StoryRuntimeAiWorldStateProjector.Project(
                blackboard,
                worldState,
                Profile(),
                diagnostics);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.UnsupportedFactCount);
            Assert.IsFalse(worldState.Contains(IntAiFact));
            Assert.AreEqual(1, diagnostics.UnsupportedFactCount);
            Assert.AreEqual(StoryRuntimeAiProjectionDiagnosticCode.UnsupportedStoryValueKind, diagnostics.Recent[0].Code);
        }

        [Test]
        public void Project_Snapshot_DoesNotMutateStoryFacts()
        {
            var blackboard = new StoryBlackboard();
            blackboard.Set(BoolFact, StoryValue.FromBool(false));
            StoryDirectorSnapshot snapshot = new StoryDirectorSnapshot(
                StoryDirector.SchemaVersion,
                1,
                Array.Empty<StoryGraphRuntimeSnapshot>(),
                Array.Empty<StoryBeatInstanceSnapshot>(),
                blackboard.CreateOrderedSnapshot());
            var worldState = new AiWorldState();

            StoryRuntimeAiWorldStateProjector.Project(snapshot, worldState, new StoryRuntimeAiProjectionProfile(
                new[] { new StoryRuntimeAiFactMapping(BoolFact, BoolAiFact) }));

            Assert.IsTrue(blackboard.TryGet(BoolFact, out StoryValue value));
            Assert.AreEqual(StoryValue.FromBool(false), value);
            Assert.IsTrue(worldState.TryGetValue(BoolAiFact, out bool projected));
            Assert.IsFalse(projected);
        }

        [Test]
        public void Profile_DuplicateStoryFactKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => new StoryRuntimeAiProjectionProfile(new[]
            {
                new StoryRuntimeAiFactMapping(BoolFact, BoolAiFact),
                new StoryRuntimeAiFactMapping(BoolFact, new AiFactKey("story.bool.copy"))
            }));
        }

        private static StoryRuntimeAiProjectionProfile Profile()
        {
            return new StoryRuntimeAiProjectionProfile(new[]
            {
                new StoryRuntimeAiFactMapping(BoolFact, BoolAiFact),
                new StoryRuntimeAiFactMapping(IntFact, IntAiFact)
            });
        }
    }
}
