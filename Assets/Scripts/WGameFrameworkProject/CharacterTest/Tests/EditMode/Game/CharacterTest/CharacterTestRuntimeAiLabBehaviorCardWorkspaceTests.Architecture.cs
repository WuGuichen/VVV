using NUnit.Framework;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceTests
    {
        [Test]
        public void BehaviorRuntimeTimeSnapshot_SeparatesRuntimeOrigins()
        {
            var snapshot = new BehaviorRuntimeTimeSnapshot(
                runtimeFrame: 140,
                cardStartedRuntimeFrame: 100,
                stageEnteredRuntimeFrame: 120,
                actionEnteredRuntimeFrame: 135);

            Assert.AreEqual(40, snapshot.GetCardElapsedFrame());
            Assert.AreEqual(20, snapshot.GetStageElapsedFrame());
            Assert.AreEqual(5, snapshot.GetActionElapsedFrame());
        }

        [Test]
        public void BehaviorRuntimeTimeSnapshot_InvalidOrFutureOriginReturnsUnavailable()
        {
            var snapshot = new BehaviorRuntimeTimeSnapshot(10, -1, 11, 10);

            Assert.AreEqual(-1, snapshot.GetCardElapsedFrame());
            Assert.AreEqual(-1, snapshot.GetStageElapsedFrame());
            Assert.AreEqual(0, snapshot.GetActionElapsedFrame());
        }

        [Test]
        public void BehaviorPlanTimeRange_DoesNotTurnDurationIntoRuntimeAuthority()
        {
            var range = new BehaviorPlanTimeRange(30, 60, 120);

            Assert.AreEqual(30, range.PlannedStartRelativeFrame);
            Assert.AreEqual(60, range.PlannedDurationFrames);
            Assert.AreEqual(90, range.PlannedEndRelativeFrame);
            Assert.AreEqual(120, range.PresentationEndRelativeFrame);
        }

        [Test]
        public void BehaviorTimelineTransform_MapsAndSnapsRelativeFrames()
        {
            var viewport = new BehaviorTimelineViewport(60, 300, 1f, 0f, false, 15);
            var transform = new BehaviorTimelineTransform(viewport, 20f, 480f);

            Assert.AreEqual(20f, transform.FrameToX(60), 0.001f);
            Assert.AreEqual(260f, transform.FrameToX(180), 0.001f);
            Assert.AreEqual(300, transform.XToFrame(500f));
            Assert.AreEqual(75, transform.SnapRelativeFrame(68));
        }

        [Test]
        public void BehaviorPlanViewportCommands_PreserveExistingFitAndZoomRules()
        {
            var current = new BehaviorTimelineViewport(60, 300, 1f, 1200f, true, 15);

            BehaviorTimelineViewport fit = BehaviorPlanViewportCommands.CalculateNext(
                current,
                480,
                0,
                0,
                0,
                true);
            BehaviorTimelineViewport zoomIn = BehaviorPlanViewportCommands.CalculateNext(
                current,
                480,
                0,
                1,
                0,
                false);

            Assert.AreEqual(0, fit.StartRelativeFrame);
            Assert.AreEqual(480, fit.EndRelativeFrame);
            Assert.AreEqual(0f, fit.VerticalScrollOffset);
            Assert.IsTrue(fit.FollowCurrent);
            Assert.AreEqual(192, zoomIn.SpanFrames);
            Assert.AreEqual(15, zoomIn.SnapFrames);
            Assert.IsFalse(zoomIn.FollowCurrent,
                "Manual viewport changes must suspend runtime cursor following.");
        }

        [Test]
        public void TimelineCompatibilityPropertiesHaveSingleSubmodelAuthority()
        {
            var timeline = new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
            {
                RuntimeFrame = 50,
                CardStartedFrame = 20,
                HorizontalStartFrame = 30,
                HorizontalEndFrame = 270,
                DurationFrames = 360
            };

            Assert.AreEqual(50, timeline.RuntimeOverlay.RuntimeFrame);
            Assert.AreEqual(30, timeline.RuntimeOverlay.Time.GetCardElapsedFrame());
            Assert.AreEqual(30, timeline.Presentation.Viewport.StartRelativeFrame);
            Assert.AreEqual(270, timeline.Presentation.Viewport.EndRelativeFrame);
            Assert.AreEqual(360, timeline.Structure.DurationFrames);
        }

        [Test]
        public void BehaviorPlanNavigationCommands_EmitTypedSelectionAndRuntimeLocator()
        {
            var sink = new BehaviorPlanRecordingCommandSink();
            var commands = new BehaviorPlanNavigationCommands(sink, new MxUiViewId("behavior.plan"));

            commands.SelectDetector("detector.exit");

            Assert.AreEqual(1, sink.Count);
            Assert.AreEqual(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectDetector,
                sink.Last.CommandId);
            var selection = (CharacterTestRuntimeAiLabBehaviorCardElementCommand)sink.Last.Payload;
            Assert.AreEqual("detector.exit", selection.StableId);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicElementKind.Detector, selection.Kind);

            commands.LocateRuntimeFrame("transition.commit", 144);

            Assert.AreEqual(2, sink.Count);
            Assert.AreEqual(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                    .BehaviorTransitionLocateRuntimeFrame,
                sink.Last.CommandId);
            var locator = (CharacterTestRuntimeAiLabBehaviorTransitionNavigationCommand)sink.Last.Payload;
            Assert.AreEqual("transition.commit", locator.TransitionId);
            Assert.AreEqual(144, locator.Frame);
        }

        [Test]
        public void TimelineRendererState_LiveAndReplayHighWaterPoolsAreIsolated()
        {
            var live = new CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder();
            var replay = new CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder();

            live.VisualPool.Markers.Add(null);
            live.VisualPool.Preheated = true;
            live.LastSelectedStableKey = "live:item";

            Assert.AreEqual(1, live.VisualPool.Markers.Count);
            Assert.IsTrue(live.VisualPool.Preheated);
            Assert.AreEqual("live:item", live.LastSelectedStableKey);
            Assert.AreEqual(0, replay.VisualPool.Markers.Count);
            Assert.IsFalse(replay.VisualPool.Preheated);
            Assert.AreEqual(string.Empty, replay.LastSelectedStableKey);

            live.ResetForRoot();

            Assert.AreEqual(0, live.VisualPool.Markers.Count);
            Assert.IsFalse(live.VisualPool.Preheated);
            Assert.AreEqual(string.Empty, live.LastSelectedStableKey);
        }

        [Test]
        public void TimelineDetailState_ResetForRootClearsSelectionAndPaging()
        {
            var details = new CharacterTestFairyGuiRuntimeAiLabTimelineDetailBinder(
                null,
                null,
                null);

            details.ResetForRoot();

            Assert.AreEqual(0, details.ClusterMemberOffset);
            Assert.IsNull(details.Details);
            Assert.IsNull(details.Item);
            Assert.IsNull(details.Workspace);
        }

        [Test]
        public void TimelineDetailFormatter_NullItemProducesSafeEmptyText()
        {
            Assert.DoesNotThrow(() => CharacterTestRuntimeAiLabTimelineDetailFormatter.BuildMeta(null));
            Assert.AreEqual(string.Empty, CharacterTestRuntimeAiLabTimelineDetailFormatter.BuildSummary(null));
            Assert.AreEqual(
                string.Empty,
                CharacterTestRuntimeAiLabTimelineDetailFormatter.BuildEvidence(null, true));
        }

        private sealed class BehaviorPlanRecordingCommandSink : IMxUiCommandSink
        {
            internal int Count { get; private set; }
            internal MxUiCommand Last { get; private set; }

            public void Enqueue(MxUiCommand command)
            {
                Count++;
                Last = command;
            }
        }
    }
}
