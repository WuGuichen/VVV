using System.Linq;
using MxFramework.Demo.Story;
using MxFramework.Story;
using NUnit.Framework;

namespace MxFramework.Tests.Demo.Story
{
    public sealed class StoryRuntimeVerticalSliceDemoTests
    {
        [Test]
        public void Demo_CommandPath_TriggerChoiceAppliesGameplayEffect()
        {
            using (var demo = new StoryRuntimeVerticalSliceDemo())
            {
                Assert.IsTrue(demo.RaiseTriggerAndTick());
                StoryRuntimeVerticalSliceSnapshot waiting = demo.CreateSnapshot();
                Assert.IsTrue(waiting.IsWaitingForPresentation);
                Assert.AreEqual(0, waiting.SignalValue);

                Assert.IsTrue(demo.CompletePresentationAndTick());
                StoryRuntimeVerticalSliceSnapshot choice = demo.CreateSnapshot();
                Assert.IsTrue(choice.HasChoice);
                Assert.AreEqual("Stabilize signal", choice.ChoiceText);

                Assert.IsTrue(demo.SelectFirstChoiceAndTick());
                StoryRuntimeVerticalSliceSnapshot completed = demo.CreateSnapshot();
                Assert.AreEqual(StoryGraphRuntimeStatus.Completed, completed.GraphStatus);
                Assert.AreEqual(StoryRuntimeVerticalSliceDemo.SignalDelta, completed.SignalValue);
                Assert.AreEqual(1, completed.GameplayCommandCount);
                AssertContains(completed, "Gameplay AddComponentAttribute");
            }
        }

        [Test]
        public void Demo_SaveRestore_ResumesChoiceStateAndCanContinue()
        {
            using (var demo = new StoryRuntimeVerticalSliceDemo())
            {
                demo.RaiseTriggerAndTick();
                demo.CompletePresentationAndTick();
                StoryRuntimeVerticalSliceSnapshot saved = demo.CreateSnapshot();
                Assert.IsTrue(saved.HasChoice);
                Assert.IsTrue(demo.Save());

                demo.SelectFirstChoiceAndTick();
                Assert.AreEqual(StoryRuntimeVerticalSliceDemo.SignalDelta, demo.CreateSnapshot().SignalValue);

                Assert.IsTrue(demo.Restore());
                StoryRuntimeVerticalSliceSnapshot restored = demo.CreateSnapshot();
                Assert.AreEqual(saved.Hash, restored.Hash);
                Assert.IsTrue(restored.HasChoice);
                Assert.AreEqual(0, restored.SignalValue);

                Assert.IsTrue(demo.SelectFirstChoiceAndTick());
                Assert.AreEqual(StoryRuntimeVerticalSliceDemo.SignalDelta, demo.CreateSnapshot().SignalValue);
            }
        }

        [Test]
        public void Demo_ReplaySmoke_ReproducesStoryAndGameplayHash()
        {
            using (var demo = new StoryRuntimeVerticalSliceDemo())
            {
                demo.RaiseTriggerAndTick();
                demo.CompletePresentationAndTick();
                demo.SelectFirstChoiceAndTick();

                Assert.IsTrue(demo.RunReplaySmoke());
                StringAssert.Contains("Replay ok", demo.CreateSnapshot().ReplayStatus);
            }
        }

        [Test]
        public void Demo_RuntimeAiPlannerProjection_ExposesWhitelistedStoryFacts()
        {
            using (var demo = new StoryRuntimeVerticalSliceDemo())
            {
                demo.RaiseTriggerAndTick();
                demo.CompletePresentationAndTick();
                demo.SelectFirstChoiceAndTick();

                StoryRuntimeVerticalSliceSnapshot snapshot = demo.CreateSnapshot();
                StringAssert.Contains("story.signal.seen=True", snapshot.AiFacts);
                StringAssert.Contains("story.choice.selected=True", snapshot.AiFacts);
                StringAssert.Contains("story.signal.level=5", snapshot.AiFacts);
            }
        }

        [Test]
        public void Demo_RepeatedChoiceRuns_DoNotDependOnBoundedRecentEvents()
        {
            for (int i = 0; i < 40; i++)
            {
                using (var demo = new StoryRuntimeVerticalSliceDemo())
                {
                    Assert.IsTrue(demo.RaiseTriggerAndTick(), "trigger iteration " + i);
                    Assert.IsTrue(demo.CompletePresentationAndTick(), "continue iteration " + i);
                    Assert.IsTrue(demo.SelectFirstChoiceAndTick(), "choice iteration " + i);

                    StoryRuntimeVerticalSliceSnapshot snapshot = demo.CreateSnapshot();
                    Assert.AreEqual(
                        StoryRuntimeVerticalSliceDemo.SignalDelta,
                        snapshot.SignalValue,
                        "signal iteration " + i);
                    Assert.AreEqual(0, demo.StoryModule.Events.PendingCount, "pending Story events iteration " + i);
                }
            }
        }

        private static void AssertContains(StoryRuntimeVerticalSliceSnapshot snapshot, string text)
        {
            Assert.IsTrue(
                snapshot.EventLog.Any(entry => entry.Contains(text)),
                "Expected event log to contain '" + text + "'. Actual: " + string.Join(" | ", snapshot.EventLog));
        }
    }
}
