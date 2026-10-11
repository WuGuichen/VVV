using System;
using MxFramework.Tests.Game.CharacterTest;
using MxFramework.CharacterAction;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceTests
    {
        private const int ApproachCardNumericId = 1001;
        private const int OtherCardNumericId = 1002;
        private const int ComboCardNumericId = 1003;
        private const string ApproachCardRuntimeEntryId = "1001";
        private const string ComboCardRuntimeEntryId = "1003";

        [Test]
        public void RecipeInspector_CharacterActionCatalogBuildsStableSearchableOptions()
        {
            var action = new CharacterActionConfig(
                new CharacterActionId(7),
                "基础攻击",
                CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored,
                new[] { "melee", "combo" },
                10,
                42,
                Array.Empty<CharacterActionRequirement>(),
                Array.Empty<CharacterActionPhase>(),
                Array.Empty<CharacterCancelRule>(),
                Array.Empty<CharacterInterruptRule>());

            BehaviorRecipeCharacterActionOptionViewModel[] options =
                BehaviorRecipeTriggerEventInspectorProjection.BuildCharacterActionOptions(
                    new[] { action, action });

            Assert.AreEqual(1, options.Length);
            Assert.AreEqual(new CharacterActionId(7), options[0].ActionId);
            Assert.AreEqual("基础攻击", options[0].DisplayName);
            Assert.AreEqual(42, options[0].DurationFrames);
            Assert.IsTrue(options[0].Valid);
            StringAssert.Contains("7", options[0].SearchText);
            StringAssert.Contains("combo", options[0].SearchText);
        }

        [Test]
        public void RecipeInspector_CharacterActionWithoutAuthoritativeDurationIsNotSelectable()
        {
            var action = new CharacterActionConfig(
                new CharacterActionId(8),
                "缺少时长动作",
                CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored,
                Array.Empty<string>(),
                0,
                null,
                Array.Empty<CharacterActionRequirement>(),
                Array.Empty<CharacterActionPhase>(),
                Array.Empty<CharacterCancelRule>(),
                Array.Empty<CharacterInterruptRule>());

            BehaviorRecipeCharacterActionOptionViewModel[] options =
                BehaviorRecipeTriggerEventInspectorProjection.BuildCharacterActionOptions(
                    new[] { action });

            Assert.AreEqual(1, options.Length);
            Assert.IsFalse(options[0].Valid);
        }

        [Test]
        public void RecipeTimeline_CharacterActionSummaryUsesDisplayNameAndFallsBackToStableId()
        {
            var operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
            {
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                CharacterActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From("action.basic")
            };
            var options = new[]
            {
                new BehaviorRecipeCharacterActionOptionViewModel
                {
                    ActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From("action.basic"),
                    DisplayName = "基础攻击",
                    Valid = true
                }
            };

            Assert.AreEqual(
                "播放 基础攻击",
                BehaviorRecipeTimelineProjection.BuildV6OperationSummary(operation, null, options));
            Assert.AreEqual(
                "播放 #" + MxFramework.Tests.CharacterActionLegacyFixtureIds.From("action.basic").Value,
                BehaviorRecipeTimelineProjection.BuildV6OperationSummary(operation, null));
        }

        [Test]
        public void RecipeTimeline_ProjectsStagesIntoCompactPrimaryAndBranchTracks()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            Assert.AreEqual(2, projection.TimelineCount);
            BehaviorRecipeTimelineViewModel move = projection.GetTimeline(0);
            Assert.AreEqual("stage.move", move.StageId);
            Assert.AreEqual(3, move.TrackCount);
            Assert.AreEqual(BehaviorRecipeEventKind.PrimaryAction, move.GetTrack(0).Event.Kind);
            Assert.AreEqual("入口 · 01 移动至目标", move.GetTrack(0).DisplayName);
            StringAssert.Contains("移动至目标", move.GetTrack(0).Event.Title);
            Assert.AreEqual(0, move.GetTrack(0).Event.StartFrame);
            Assert.AreEqual(BehaviorRecipeEventPrimitive.OpenSpan, move.GetTrack(0).Event.Primitive);
            Assert.AreEqual("detector.arrived", move.GetTrack(1).Event.DetectorId);
            Assert.AreEqual("detector.path", move.GetTrack(2).Event.DetectorId);
            Assert.IsTrue(move.GetTrack(1).Event.CanMove);
            Assert.IsTrue(move.GetTrack(1).Event.CanResize,
                "Open branch spans expose a right-edge handle; resizing converts them to fixed spans.");
            Assert.IsTrue(move.GetTrack(2).Event.CanResize);
            Assert.AreEqual(1, projection.GetTimeline(1).TrackCount);
        }

        [Test]
        public void RecipeTimeline_EventGesturesStayInsideTheirHorizontalTrackBounds()
        {
            Assert.AreEqual(
                0f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ClampRecipeHorizontalEventX(-80f, 600f, 200f));
            Assert.AreEqual(
                400f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ClampRecipeHorizontalEventX(520f, 600f, 200f));
            Assert.AreEqual(
                96f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ClampRecipeEventWidth(20f, 600f, 120f, 96f));
            Assert.AreEqual(
                480f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ClampRecipeEventWidth(700f, 600f, 120f, 96f));
        }

        [Test]
        public void RecipeTimeline_EventResizeUsesLocalPixelsAndSnapsToWholeFrames()
        {
            const float trackWidth = 1200f;
            const int viewportStart = 0;
            const int viewportEnd = 240;
            int deltaFrames = CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                .ResolveRecipeGestureDeltaFrames(
                    25f,
                    trackWidth,
                    viewportStart,
                    viewportEnd,
                    snapFrames: 1);

            Assert.AreEqual(5, deltaFrames,
                "At five local pixels per frame, 25 local pixels must resize by exactly five frames.");
            Assert.AreEqual(
                -5,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveRecipeGestureDeltaFrames(
                        -25f,
                        trackWidth,
                        viewportStart,
                        viewportEnd,
                        snapFrames: 1));
            Assert.AreEqual(
                0,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveRecipeGestureDeltaFrames(
                        2.49f,
                        trackWidth,
                        viewportStart,
                        viewportEnd,
                        snapFrames: 1));
            Assert.AreEqual(
                1,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveRecipeGestureDeltaFrames(
                        2.51f,
                        trackWidth,
                        viewportStart,
                        viewportEnd,
                        snapFrames: 1));

            var viewport = new BehaviorTimelineViewport(
                viewportStart,
                viewportEnd,
                1f,
                0f,
                false,
                1);
            var transform = new BehaviorTimelineTransform(viewport, 0f, trackWidth);
            float previewDelta = transform.FrameToX(35) - transform.FrameToX(30);
            Assert.AreEqual(25f, previewDelta, 0.001f,
                "The snapped preview edge must move one local pixel for every local pointer pixel.");
            Assert.AreEqual(
                35,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveRecipeGestureFrame(175f, transform),
                "The committed edge frame must be read directly from the same local pointer X used by the preview.");
            Assert.AreEqual(
                36,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveRecipeGestureFrame(177.51f, transform),
                "Half-frame crossings must snap deterministically to the next whole frame.");
            Assert.AreEqual(
                293f,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveRecipeResizeHandleX(150f, 150f, trackWidth, 14f),
                0.001f,
                "The resize hit target must remain centered on the true frame edge.");
        }

        [Test]
        public void RecipeTimeline_OnlyAddsEntryTrackWhenStageHasRealEntryConditions()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.Stages[0].EntryConditionCount = 2;
            source.Stages[0].EntryConditionSummaryText = "2 条入口条件";
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            BehaviorRecipeTimelineViewModel move = projection.GetTimeline(0);
            Assert.AreEqual(4, move.TrackCount);
            Assert.AreEqual(BehaviorRecipeEventKind.StageEntry, move.GetTrack(0).Event.Kind);
            Assert.AreEqual("入口条件", move.GetTrack(0).DisplayName);
            Assert.AreEqual("移动至目标 · 2 条入口条件", move.GetTrack(0).Event.Title);
            Assert.AreEqual(BehaviorRecipeEventKind.PrimaryAction, move.GetTrack(1).Event.Kind);
        }

        [Test]
        public void RecipeTimeline_BranchAggregatesResultsAndChoosesStaticHighestPriority()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.Transitions = new[]
            {
                new CharacterTestRuntimeAiLabLogicTransitionViewModel
                {
                    Index = 0,
                    DetectorIndex = 0,
                    Priority = 10,
                    TransitionId = "transition.low",
                    TargetText = "结束行为卡"
                },
                new CharacterTestRuntimeAiLabLogicTransitionViewModel
                {
                    Index = 1,
                    DetectorIndex = 0,
                    Priority = 50,
                    TransitionId = "transition.high",
                    TargetText = "进入攻击"
                },
                new CharacterTestRuntimeAiLabLogicTransitionViewModel
                {
                    Index = 2,
                    DetectorIndex = 1,
                    Priority = 20,
                    TransitionId = "transition.replan",
                    TargetText = "重新决策"
                }
            };
            source.TransitionCount = 3;
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            BehaviorRecipeEventViewModel branch = projection.GetTimeline(0).GetTrack(1).Event;
            Assert.AreEqual(2, branch.TransitionIndices.Count);
            Assert.AreEqual(1, branch.EffectiveTransitionIndex);
            Assert.AreEqual(1, branch.AdditionalResultCount);
            StringAssert.Contains("进入攻击", branch.Title);
            StringAssert.Contains("+1", branch.Title);
        }

        [Test]
        public void RecipeTimeline_CardScopeDetectorProducesSharedPinnedMirrorPerStage()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.Detectors = new[]
            {
                new CharacterTestRuntimeAiLabLogicDetectorViewModel
                {
                    Index = 0,
                    DetectorId = "detector.shared",
                    Title = "目标丢失",
                    CardScope = true,
                    Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Card,
                    StartFrame = 45,
                    DurationFrames = 60,
                    WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                    Priority = 100
                }
            };
            source.DetectorCount = 1;
            source.Transitions = new[]
            {
                new CharacterTestRuntimeAiLabLogicTransitionViewModel
                {
                    Index = 0,
                    DetectorIndex = 0,
                    TransitionId = "transition.shared",
                    TargetText = "重新决策"
                }
            };
            source.TransitionCount = 1;
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            for (int i = 0; i < projection.TimelineCount; i++)
            {
                BehaviorRecipeEventViewModel mirror = projection.GetTimeline(i).GetTrack(1).Event;
                Assert.AreEqual(BehaviorRecipeEventKind.LegacySharedBranch, mirror.Kind);
                Assert.AreEqual(0, mirror.StartFrame);
                Assert.IsTrue(mirror.Shared);
                Assert.IsFalse(mirror.CanMove);
                Assert.IsFalse(mirror.CanResize);
                StringAssert.Contains("整卡计时 +45f", mirror.Detail);
                Assert.AreEqual("detector.shared", mirror.DetectorId);
            }
        }

        [Test]
        public void RecipeTimeline_UsesStageLocalPlayheadOnlyOnCurrentTimeline()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RuntimeFrame = 140;
            source.StageEnteredFrame = 120;
            source.Stages[0].Current = true;
            source.Stages[0].ActionRunning = true;
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            Assert.AreEqual(20, projection.LocalPlayheadFrame);
            Assert.AreEqual(0, projection.CurrentTimelineIndex);
            Assert.IsTrue(projection.GetTimeline(0).GetTrack(0).Event.Running);
            Assert.IsFalse(projection.GetTimeline(1).GetTrack(0).Event.Running);
        }

        [Test]
        public void RecipeTimeline_WaitResizesRealWaitFieldButActionReferenceLengthIsReadOnly()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.Stages[0].Kind = CharacterTestRuntimeAiLabCardStepKind.Wait;
            source.Stages[0].WaitFrames = 75;
            source.Stages[1].Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction;
            source.Stages[1].ActualDurationFrames = 42;
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            BehaviorRecipeEventViewModel wait = projection.GetTimeline(0).GetTrack(0).Event;
            BehaviorRecipeEventViewModel action = projection.GetTimeline(1).GetTrack(0).Event;
            Assert.AreEqual(75, wait.DurationFrames);
            Assert.IsTrue(wait.CanResize);
            Assert.AreEqual(42, action.DurationFrames);
            Assert.IsFalse(action.CanResize);
        }

        [Test]
        public void RecipeTimeline_BranchRowsAreSortedByPriorityThenStableId()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.Detectors[0].Priority = 10;
            source.Detectors[1].Priority = 100;
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            BehaviorRecipeTimelineViewModel move = projection.GetTimeline(0);
            Assert.AreEqual("detector.path", move.GetTrack(1).Event.DetectorId);
            Assert.AreEqual("detector.arrived", move.GetTrack(2).Event.DetectorId);
        }

        [Test]
        public void RecipeTimeline_AddBranchAtFrameCreatesDetectorAndResultAsOneUndoTransaction()
        {
            var host = new GameObject("Behavior Recipe Add Branch Transaction Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    PrepareRecipeController(host);
                const int entryIndex = 2;
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                    ProjectDraft(controller, entryIndex);
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    () => workspace = ProjectDraft(controller, entryIndex),
                    () => entryIndex,
                    workspaceView: () => workspace);

                CharacterTestRuntimeAiLabBehaviorCardLogicPayload before =
                    CurrentDraftPayload(controller, entryIndex);
                CharacterTestRuntimeAiLabLogicStage sourceStage = before.Stages[0];
                string expectedTarget = FindNextStageId(before, sourceStage);
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline beforeTimeline =
                    FindRecipeTimeline(before, sourceStage.StageId);
                string[] existingEventIds = RecipeEventIds(beforeTimeline);

                Assert.That(handler.TryHandle(
                    Command(
                        CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStageBranchAtFrame,
                        sourceStage.StageId,
                        37),
                    out string addMessage), Is.True, addMessage);

                CharacterTestRuntimeAiLabBehaviorCardLogicPayload added =
                    CurrentDraftPayload(controller, entryIndex);
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline addedTimeline =
                    FindRecipeTimeline(added, sourceStage.StageId);
                Assert.AreEqual(
                    beforeTimeline.TriggerEvents.Count + 1,
                    addedTimeline.TriggerEvents.Count,
                    "Creating one Branch Event must add exactly one timeline row.");
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent addedEvent =
                    FindNewRecipeEvent(addedTimeline, existingEventIds);
                Assert.NotNull(addedEvent);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame,
                    addedEvent.SourceKind);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame,
                    addedEvent.LocalFrameAnchor);
                Assert.AreEqual(37, addedEvent.LocalFrame);
                Assert.AreEqual(37, addedEvent.ActivationStartFrame);
                Assert.AreEqual(sourceStage.StageId, addedEvent.TimelineId);
                Assert.AreEqual(
                    string.Equals(expectedTarget, CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                        StringComparison.Ordinal)
                        ? CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
                        : CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline,
                    addedEvent.OutcomeKind);
                Assert.AreEqual(
                    string.Equals(expectedTarget, CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                        StringComparison.Ordinal)
                        ? string.Empty
                        : expectedTarget,
                    addedEvent.TargetTimelineId);

                handler.UndoStageEdit();

                CharacterTestRuntimeAiLabBehaviorCardLogicPayload undone =
                    CurrentDraftPayload(controller, entryIndex);
                CollectionAssert.AreEquivalent(
                    existingEventIds,
                    RecipeEventIds(FindRecipeTimeline(undone, sourceStage.StageId)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NativeTimeline_AddEventDoesNotInventAnOperationOrPredicate()
        {

            using var f = new NativeEditingFixture();
            int count = f.Payload().Timelines[0].Events.Count;
            int revision = f.Controller.DraftWorkspace.CurrentRevision;
            string added = f.AddEvent(12);
            var evt = f.Payload().Timelines[0].Events.Find(e => e.EventId == added);
            Assert.NotNull(evt);
            Assert.AreEqual("approach", evt.TimelineId);
            Assert.AreEqual(12, evt.StartFrame);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured, evt.Operation.Kind);
            Assert.IsNull(evt.ConditionRoot, "A newly authored native event must not invent a predicate.");
            Assert.AreEqual(count + 1, f.Payload().Timelines[0].Events.Count);
            Assert.AreEqual(revision + 1, f.Controller.DraftWorkspace.CurrentRevision);
            f.Undo();
            Assert.AreEqual(count, f.Payload().Timelines[0].Events.Count);
        }

        [Test]
        public void NativeTimeline_TargetAndPriorityEditsPersistInEventAuthority()
        {

            using var f = new NativeEditingFixture();
            Assert.IsTrue(f.Controller.TryAddLogicStage(NativeEditingFixture.Entry, 0, out string issue), issue);
            string target = f.Payload().Timelines[1].TimelineId;
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationKind,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline));
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TargetTimelineId,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(stringValue: target));
            Assert.AreEqual(target, f.Event().Operation.TargetTimelineId);
            int priority = f.Event().Priority;
            f.Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Priority,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: priority + 20));
            Assert.AreEqual(priority + 20, f.Event().Priority);
            Assert.AreEqual(target, f.Event().Operation.TargetTimelineId);
        }

        [Test]
        public void RecipeTimeline_V6ProjectsOnlyUnifiedEventsAndKeepsEntryConditionsOnTimeline()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestBehaviorRecipeReferenceCompilation.Compile(payload);
            Assert.IsTrue(compile.Success);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RecipePayload = payload;
            source.CompiledRecipeV6 = compile.Program.RecipeV6;
            source.Presentation.SelectedRecipeEventStableKey =
                BehaviorRecipeTimelineProjection.BuildV6EventStableKey("approach", "event.exit");
            source.Presentation.SelectEvent("approach", "event.exit");
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            Assert.AreEqual(1, projection.TimelineCount);
            BehaviorRecipeTimelineViewModel timeline = projection.GetTimeline(0);
            Assert.IsTrue(timeline.Entry);
            Assert.AreEqual(2, timeline.TrackCount, "Timeline entry conditions must not create an Event row.");
            for (int i = 0; i < timeline.TrackCount; i++)
            {
                Assert.AreEqual(BehaviorRecipeTrackKind.EventLane, timeline.GetTrack(i).Kind);
                Assert.AreEqual(timeline.GetTrack(i).Event.GroupDisplayName, timeline.GetTrack(i).DisplayName);
                Assert.AreEqual(BehaviorRecipeEventKind.Event, timeline.GetTrack(i).Event.Kind);
            }
            BehaviorRecipeEventViewModel move = string.Equals(
                    timeline.GetTrack(0).Event.EventId, "event.move", StringComparison.Ordinal)
                ? timeline.GetTrack(0).Event
                : timeline.GetTrack(1).Event;
            BehaviorRecipeEventViewModel exit = ReferenceEquals(move, timeline.GetTrack(0).Event)
                ? timeline.GetTrack(1).Event
                : timeline.GetTrack(0).Event;
            Assert.AreEqual("接近目标", move.Title);
            Assert.AreEqual(BehaviorRecipeEventPrimitive.Span, move.Primitive);
            Assert.IsTrue(move.CanMove);
            Assert.IsTrue(move.CanResize);
            StringAssert.Contains("TargetExists = 否", exit.Title);
            Assert.IsTrue(exit.Selected);
        }

        [Test]
        public void RecipeTimeline_V8UsesExplicitTimingKindForMarkerAndSpan()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent exit = payload.Timelines[0].Events[1];
            exit.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
            exit.DurationFrames = 1;
            exit.MaxExecutionsPerPass = 1;
            exit.CooldownFrames = 0;
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestBehaviorRecipeReferenceCompilation.Compile(payload);
            Assert.IsTrue(compile.Success);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RecipePayload = payload;
            source.CompiledRecipeV6 = compile.Program.RecipeV6;
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            BehaviorRecipeTimelineViewModel timeline = projection.GetTimeline(0);
            BehaviorRecipeEventViewModel instant = FindProjectedEvent(timeline, "event.exit");
            BehaviorRecipeEventViewModel duration = FindProjectedEvent(timeline, "event.move");
            Assert.AreEqual(BehaviorRecipeEventPrimitive.Marker, instant.Primitive);
            Assert.IsFalse(instant.CanResize);
            Assert.AreEqual(BehaviorRecipeEventPrimitive.Span, duration.Primitive);
            Assert.IsTrue(duration.CanResize);
        }

        [Test]
        public void RecipeTimeline_V8CompactsNonOverlappingEventsIntoSharedLanes()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline sourceTimeline = payload.Timelines[0];
            sourceTimeline.Events[0].DurationFrames = 20;
            sourceTimeline.Events[1].DurationFrames = 10;
            sourceTimeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.followup",
                TimelineId = sourceTimeline.TimelineId,
                Title = "后续事件",
                StartFrame = 20,
                DurationFrames = 10,
                TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                MaxExecutionsPerPass = 1,
                Priority = 50,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner
                }
            });
            CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.SynchronizeForWrite(payload);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestBehaviorRecipeReferenceCompilation.Compile(payload);
            Assert.IsTrue(compile.Success);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RecipePayload = payload;
            source.CompiledRecipeV6 = compile.Program.RecipeV6;
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            BehaviorRecipeTimelineViewModel timeline = projection.GetTimeline(0);
            Assert.AreEqual(3, timeline.TrackCount, "Every Event must remain independently editable.");
            Assert.AreEqual(2, timeline.LaneCount, "Only overlapping Event ranges should require another lane.");
            BehaviorRecipeTrackViewModel move = FindProjectedTrack(timeline, "event.move");
            BehaviorRecipeTrackViewModel exit = FindProjectedTrack(timeline, "event.exit");
            BehaviorRecipeTrackViewModel followup = FindProjectedTrack(timeline, "event.followup");
            Assert.AreEqual(exit.VerticalOrder, followup.VerticalOrder,
                "The first available lane must be reused after its prior Event ends.");
            Assert.AreNotEqual(move.VerticalOrder, exit.VerticalOrder,
                "Events whose half-open ranges overlap must remain on separate lanes.");
        }

        [Test]
        public void RecipeTimeline_CharacterActionEventUsesAuthoritativeDurationAndCannotResize()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent actionEvent = payload.Timelines[0].Events[0];
            actionEvent.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;
            actionEvent.DurationFrames = 3;
            actionEvent.ConditionRoot = null;
            actionEvent.MaxExecutionsPerPass = 1;
            actionEvent.CooldownFrames = 0;
            actionEvent.Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
            {
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                CharacterActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From("action.basic")
            };
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RecipePayload = payload;
            source.CharacterActionOptions = new[]
            {
                new BehaviorRecipeCharacterActionOptionViewModel
                {
                    ActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From("action.basic"),
                    DisplayName = "基础攻击",
                    DurationFrames = 42,
                    Valid = true
                }
            };
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            BehaviorRecipeEventViewModel projected =
                FindProjectedEvent(projection.GetTimeline(0), actionEvent.EventId);
            Assert.IsTrue(projected.CharacterActionEvent);
            Assert.AreEqual(42, projected.DurationFrames);
            Assert.IsFalse(projected.CanResize);
            StringAssert.Contains("基础攻击", projected.Title);
            StringAssert.Contains("42f", projected.Title);
        }

        [Test]
        public void RecipeTimeline_V6CursorUsesAuthoritativeLocalFrameAndCompiledEventIndex()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestBehaviorRecipeReferenceCompilation.Compile(payload);
            Assert.IsTrue(compile.Success);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RecipePayload = payload;
            source.CompiledRecipeV6 = compile.Program.RecipeV6;
            source.RuntimeFrame = 900;
            source.StageEnteredFrame = 899;
            int moveEventIndex = -1;
            for (int i = 0; i < compile.Program.RecipeV6.Events.Count; i++)
            {
                if (string.Equals(
                        compile.Program.RecipeV6.Events[i].EventId,
                        "event.move",
                        StringComparison.Ordinal))
                {
                    moveEventIndex = compile.Program.RecipeV6.Events[i].Index;
                    break;
                }
            }
            Assert.GreaterOrEqual(moveEventIndex, 0);
            source.RuntimeState = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                BehaviorCardStableId = ApproachCardNumericId,
                ProgramSemanticHash = compile.Program.SemanticHash,
                RuntimeState = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Running,
                CurrentTimelineIndex = 0,
                TimelineEnteredFrame = 880,
                LocalFrame = 20,
                ActorOwnerEventIndex = moveEventIndex
            };
            source.RuntimeCursor.Populate(
                compile.Program.RecipeV6,
                source.RuntimeState,
                compile.Program.SemanticHash,
                ApproachCardRuntimeEntryId,
                source.RuntimeFrame,
                historical: false);
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            Assert.AreEqual(20, projection.LocalPlayheadFrame,
                "v6 cursor must not be derived from the legacy StageEnteredFrame.");
            Assert.AreEqual(0, projection.CurrentTimelineIndex);
            BehaviorRecipeTimelineViewModel timeline = projection.GetTimeline(0);
            BehaviorRecipeEventViewModel move = null;
            for (int i = 0; i < timeline.TrackCount; i++)
            {
                if (string.Equals(timeline.GetTrack(i).Event.EventId, "event.move", StringComparison.Ordinal))
                    move = timeline.GetTrack(i).Event;
            }
            Assert.NotNull(move);
            Assert.AreEqual(moveEventIndex, move.CompiledEventIndex);
            Assert.IsTrue(move.Running);
            Assert.AreEqual(20, move.ProgressFrames);
        }

        [Test]
        public void RecipeTimeline_V6CursorHidesWhenRuntimeAuthorityDoesNotMatch()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestBehaviorRecipeReferenceCompilation.Compile(payload);
            Assert.IsTrue(compile.Success);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RecipePayload = payload;
            source.CompiledRecipeV6 = compile.Program.RecipeV6;
            source.RuntimeState = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                BehaviorCardStableId = OtherCardNumericId,
                ProgramSemanticHash = compile.Program.SemanticHash + 1,
                RuntimeState = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Running,
                CurrentTimelineIndex = 0,
                LocalFrame = 42
            };
            source.RuntimeCursor.Populate(
                compile.Program.RecipeV6,
                source.RuntimeState,
                compile.Program.SemanticHash,
                ApproachCardRuntimeEntryId,
                42,
                historical: false);
            var projection = new BehaviorRecipeTimelineProjection();

            projection.Rebuild(source);

            Assert.IsFalse(source.RuntimeCursor.Available);
            Assert.IsFalse(source.RuntimeCursor.AuthorityMatched);
            Assert.AreEqual(-1, projection.LocalPlayheadFrame);
            Assert.AreEqual(-1, projection.CurrentTimelineIndex);
        }

        [Test]
        public void RecipeTimeline_V8InspectorDoesNotProjectStaleRuntimeAsWaitingCondition()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult runningCompile =
                CharacterTestBehaviorRecipeReferenceCompilation.Compile(payload);
            Assert.IsTrue(runningCompile.Success);

            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RecipePayload = payload;
            source.Presentation.SelectEvent("approach", "event.exit");
            source.RuntimeState = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                BehaviorCardStableId = ApproachCardNumericId,
                ProgramSemanticHash = runningCompile.Program.SemanticHash,
                RuntimeState = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Running,
                CurrentTimelineIndex = 0,
                LocalFrame = 10,
                EventResults = new int[runningCompile.Program.RecipeV6.Events.Count]
            };

            payload.Timelines[0].Events[1].Priority++;
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult editedCompile =
                CharacterTestBehaviorRecipeReferenceCompilation.Compile(payload);
            Assert.IsTrue(editedCompile.Success);
            Assert.AreNotEqual(
                runningCompile.Program.SemanticHash,
                editedCompile.Program.SemanticHash);
            source.CompiledRecipeV6 = editedCompile.Program.RecipeV6;
            source.RuntimeCursor.Populate(
                editedCompile.Program.RecipeV6,
                source.RuntimeState,
                editedCompile.Program.SemanticHash,
                ApproachCardRuntimeEntryId,
                10,
                historical: false);

            BehaviorRecipeTriggerEventInspectorProjection.Rebuild(
                source,
                payload,
                source.RuntimeState);

            Assert.IsFalse(source.RuntimeCursor.AuthorityMatched);
            Assert.AreEqual("未运行", source.TriggerEventInspector.RuntimeStateText);
        }

        [Test]
        public void RecipeTimeline_V8InspectorOnlyShowsWaitingConditionInsideActiveEventRange()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            payload.Timelines[0].Events[1].StartFrame = 12;
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestBehaviorRecipeReferenceCompilation.Compile(payload);
            Assert.IsTrue(compile.Success);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RecipePayload = payload;
            source.CompiledRecipeV6 = compile.Program.RecipeV6;
            source.Presentation.SelectEvent("approach", "event.exit");
            source.RuntimeState = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                BehaviorCardStableId = ApproachCardNumericId,
                ProgramSemanticHash = compile.Program.SemanticHash,
                RuntimeState = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Running,
                CurrentTimelineIndex = 0,
                LocalFrame = 10,
                EventResults = new int[compile.Program.RecipeV6.Events.Count]
            };
            source.RuntimeCursor.Populate(
                compile.Program.RecipeV6,
                source.RuntimeState,
                compile.Program.SemanticHash,
                ApproachCardRuntimeEntryId,
                10,
                historical: false);

            BehaviorRecipeTriggerEventInspectorProjection.Rebuild(
                source,
                payload,
                source.RuntimeState);

            Assert.AreEqual("等待开始", source.TriggerEventInspector.RuntimeStateText);

            source.RuntimeState.LocalFrame = 12;
            source.RuntimeCursor.Populate(
                compile.Program.RecipeV6,
                source.RuntimeState,
                compile.Program.SemanticHash,
                ApproachCardRuntimeEntryId,
                12,
                historical: false);
            BehaviorRecipeTriggerEventInspectorProjection.Rebuild(
                source,
                payload,
                source.RuntimeState);

            Assert.AreEqual("等待条件", source.TriggerEventInspector.RuntimeStateText);
        }

        [Test]
        public void RecipeTimeline_V6RuntimeOverlayMovesCursorToJumpTargetWithoutRebuild()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            var second = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            {
                TimelineId = "attack",
                Order = 1,
                Title = "攻击"
            };
            second.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.attack",
                TimelineId = second.TimelineId,
                StartFrame = 0,
                DurationFrames = 30,
                MaxExecutionsPerPass = 1,
                Priority = 100,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                }
            });
            payload.Timelines.Add(second);
            payload.Timelines[0].Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.jump",
                TimelineId = "approach",
                StartFrame = 60,
                DurationFrames = 1,
                MaxExecutionsPerPass = 1,
                Priority = 300,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline,
                    TargetTimelineId = "attack"
                }
            });
            CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.SynchronizeForWrite(payload);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestBehaviorRecipeReferenceCompilation.Compile(payload);
            Assert.IsTrue(compile.Success, JoinDiagnostics(compile));
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RecipePayload = payload;
            source.CompiledRecipeV6 = compile.Program.RecipeV6;
            source.RuntimeState = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                BehaviorCardStableId = ComboCardNumericId,
                ProgramSemanticHash = compile.Program.SemanticHash,
                RuntimeState = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Running,
                CurrentTimelineIndex = 0,
                LocalFrame = 18,
                ActorOwnerEventIndex = compile.Program.RecipeV6.Timelines[0].Events[0].Index
            };
            source.RuntimeCursor.Populate(
                compile.Program.RecipeV6,
                source.RuntimeState,
                compile.Program.SemanticHash,
                ComboCardRuntimeEntryId,
                18,
                historical: false);
            var projection = new BehaviorRecipeTimelineProjection();
            projection.Rebuild(source);
            Assert.IsTrue(projection.GetTimeline(0).Current);

            source.RuntimeState.CurrentTimelineIndex = 1;
            source.RuntimeState.LocalFrame = 0;
            source.RuntimeState.TimelineEnteredFrame = 19;
            source.RuntimeState.ActorOwnerEventIndex =
                compile.Program.RecipeV6.Timelines[1].Events[0].Index;
            source.RuntimeCursor.Populate(
                compile.Program.RecipeV6,
                source.RuntimeState,
                compile.Program.SemanticHash,
                ComboCardRuntimeEntryId,
                19,
                historical: false);
            projection.UpdateRuntimeOverlay(source);

            Assert.AreEqual(1, projection.CurrentTimelineIndex);
            Assert.AreEqual(0, projection.LocalPlayheadFrame);
            Assert.IsFalse(projection.GetTimeline(0).Current);
            Assert.IsTrue(projection.GetTimeline(1).Current);
            Assert.IsTrue(projection.GetTimeline(1).GetTrack(0).Event.Running);
        }

        [Test]
        public void RecipeTimeline_RuntimeCursorFollowAdvancesAtEightyPercentAndRecentersWithHeadroom()
        {
            Assert.AreEqual(
                0,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveRecipeAutoFollowStart(0, 100, 79));
            Assert.AreEqual(
                15,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveRecipeAutoFollowStart(0, 100, 80));
            Assert.AreEqual(
                0,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveRecipeAutoFollowStart(50, 150, 5));
        }

        [Test]
        public void RecipeTimeline_V6InspectorReadsUnifiedEventAndMutatesItsTimeAndRepeatFields()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RecipePayload = payload;
            source.CanEdit = true;
            source.Presentation.SelectedRecipeEventStableKey =
                BehaviorRecipeTimelineProjection.BuildV6EventStableKey("approach", "event.exit");
            source.Presentation.SelectEvent("approach", "event.exit");

            BehaviorRecipeTriggerEventInspectorProjection.Rebuild(source, payload, null);

            Assert.NotNull(source.TriggerEventInspector);
            Assert.IsTrue(source.TriggerEventInspector.UnifiedV6);
            Assert.AreEqual(1, source.TriggerEventInspector.Conditions.Length);
            Assert.AreEqual("目标状态 符合 阶段：无目标", source.TriggerEventInspector.Conditions[0].EffectiveSummary);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner,
                source.TriggerEventInspector.OperationKind);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActivationStartFrame,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: 12),
                out bool moved,
                out string moveDiagnostic), moveDiagnostic);
            Assert.IsTrue(moved);
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.MaxExecutionsPerPass,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: 0),
                out bool repeated,
                out string repeatDiagnostic), repeatDiagnostic);
            Assert.IsTrue(repeated);
            Assert.AreEqual(12, payload.Timelines[0].Events[1].StartFrame);
            Assert.AreEqual(0, payload.Timelines[0].Events[1].MaxExecutionsPerPass);
        }

        [Test]
        public void RecipeTimeline_V8TimingMutationClearsIncompatibleOperationAtomically()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.Timelines[0].Events[1];
            evt.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;
            evt.DurationFrames = 18;
            evt.MaxExecutionsPerPass = 3;
            evt.CooldownFrames = 4;
            evt.Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
            {
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion
            };

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TimingKind,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant),
                out bool changed,
                out string diagnostic), diagnostic);

            Assert.IsTrue(changed);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant, evt.TimingKind);
            Assert.AreEqual(1, evt.DurationFrames);
            Assert.AreEqual(1, evt.MaxExecutionsPerPass);
            Assert.AreEqual(0, evt.CooldownFrames);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured, evt.Operation.Kind);
        }

        [Test]
        public void RecipeTimeline_GenericOperationMutationCannotCreateCharacterActionEvent()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();

            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationKind,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction),
                out bool changed,
                out string diagnostic));

            Assert.IsFalse(changed);
            StringAssert.Contains("添加角色动作事件", diagnostic);
            Assert.AreNotEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                payload.Timelines[0].Events[1].Operation.Kind);
        }

        [Test]
        public void RecipeTimeline_GenericOperationMutationCannotCreateAbilityEvent()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();

            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationKind,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability),
                out bool changed,
                out string diagnostic));

            Assert.IsFalse(changed);
            StringAssert.Contains("不支持 Ability 事件", diagnostic);
            Assert.AreNotEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability,
                payload.Timelines[0].Events[1].Operation.Kind);
        }

        [Test]
        public void RecipeTimeline_GenericOperationMutationCannotCreateHoldEvent()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();

            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationKind,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Hold),
                out bool changed,
                out string diagnostic));

            Assert.IsFalse(changed);
            StringAssert.Contains("不支持 Hold 事件", diagnostic);
            Assert.AreNotEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Hold,
                payload.Timelines[0].Events[1].Operation.Kind);
        }

        [Test]
        public void RecipeTimeline_LegacyConditionalCharacterActionCanBeExplicitlyRepaired()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.Timelines[0].Events[1];
            evt.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
            evt.MaxExecutionsPerPass = 0;
            evt.CooldownFrames = 12;
            evt.DurationReviewRequired = true;
            evt.Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
            {
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                CharacterActionId = CharacterTestCharacterActionConfigSource.LightAttackActionId
            };
            Assert.NotNull(evt.ConditionRoot);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConvertCharacterActionEvent,
                -1,
                default,
                out bool changed,
                out string diagnostic), diagnostic);

            Assert.IsTrue(changed);
            Assert.IsNull(evt.ConditionRoot);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration, evt.TimingKind);
            Assert.AreEqual(1, evt.MaxExecutionsPerPass);
            Assert.AreEqual(0, evt.CooldownFrames);
            Assert.IsFalse(evt.DurationReviewRequired);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                evt.Operation.Kind);
        }

        [Test]
        public void RecipeTimeline_V6ConditionOwnersReuseTreeInspectorWithoutCreatingEvents()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            payload.ActivationConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "activation.all",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
            };
            payload.ActivationConditionRoot.Children.Add(
                new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = "activation.target",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    Predicate = new CharacterTestRuntimeAiLabCardCondition
                    {
                        ConditionId = "activation.target",
                        Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                        Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                        BoolValue = true
                    }
                });
            payload.ActivationConditionRoot.Children.Add(
                new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = "activation.distance",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    Predicate = new CharacterTestRuntimeAiLabCardCondition
                    {
                        ConditionId = "activation.distance",
                        Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                        Operator = CharacterTestRuntimeAiLabCardConditionOperator.Between,
                        Value = 0.5f,
                        MaxValue = 1.5f
                    }
                });
            CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.SynchronizeForWrite(payload);
            int originalEventCount = payload.Timelines[0].Events.Count;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source = BuildRecipeSource();
            source.RecipePayload = payload;
            source.CanEdit = true;
            source.CanEditCardDisplayName = true;
            source.CanEditResponsibilities = true;
            source.EntryIndex = 4;
            source.ResponsibilityCompatibilityMask =
                (1 << (int)CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting)
                | (1 << (int)CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage);
            source.ResponsibilityKinds = new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
            };
            source.CardTitleText = "木桩";

            source.Presentation.SelectRecipe();
            source.Presentation.SelectedRecipeEventStableKey = "condition-owner:activation";
            BehaviorRecipeTriggerEventInspectorProjection.Rebuild(source, payload, null);
            Assert.NotNull(source.TriggerEventInspector);
            Assert.IsTrue(source.TriggerEventInspector.ConditionOwnerOnly);
            Assert.IsFalse(source.TriggerEventInspector.AllowsEventSignal);
            Assert.AreEqual("木桩", source.TriggerEventInspector.DisplayTitle);
            Assert.IsTrue(source.TriggerEventInspector.CanEditDisplayTitle);
            Assert.IsTrue(source.TriggerEventInspector.CanEditResponsibilities);
            Assert.AreEqual(4, source.TriggerEventInspector.EntryIndex);
            Assert.AreEqual(source.ResponsibilityCompatibilityMask,
                source.TriggerEventInspector.ResponsibilityCompatibilityMask);
            CollectionAssert.AreEqual(source.ResponsibilityKinds,
                source.TriggerEventInspector.ResponsibilityKinds);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId,
                source.TriggerEventInspector.EventId);
            source.CanEdit = false;
            source.CanEditCardDisplayName = true;
            BehaviorRecipeTriggerEventInspectorProjection.Rebuild(source, payload, null);
            Assert.IsFalse(source.TriggerEventInspector.CanEdit);
            Assert.IsTrue(source.TriggerEventInspector.CanEditDisplayTitle);
            Assert.IsTrue(CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder.CanCommitInput(
                source.TriggerEventInspector,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.Title),
                "Behavior-card naming must commit even when behavior-logic editing is unavailable.");
            Assert.AreEqual(-1, source.TriggerEventInspector.Conditions[0].LeafIndex,
                "The visible root group is not an editable condition leaf.");
            Assert.IsTrue(BehaviorRecipeTriggerEventInspectorProjection.TryResolveConditionLeaf(
                source.TriggerEventInspector.Conditions,
                0,
                out BehaviorRecipeTriggerConditionViewModel activationLeaf));
            Assert.AreEqual("activation.target", activationLeaf.NodeId);
            Assert.IsTrue(activationLeaf.BoolValue,
                "The boolean dropdown must resolve the leaf value rather than the root group's default value.");

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionBoolValue,
                0,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(boolValue: false),
                out bool activationChanged,
                out string activationDiagnostic), activationDiagnostic);
            Assert.IsTrue(activationChanged);
            Assert.IsFalse(payload.ActivationConditionRoot.Children[0].Predicate.BoolValue);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionRange,
                1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    floatValue: 0.8f,
                    floatMaxValue: 2.4f),
                out bool rangeChanged,
                out string rangeDiagnostic), rangeDiagnostic);
            Assert.IsTrue(rangeChanged);
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode distanceScope =
                payload.ActivationConditionRoot.Children[1];
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                distanceScope.Kind);
            Assert.AreEqual(0.8f, distanceScope.Children[0].Predicate.Value);
            Assert.AreEqual(2.4f, distanceScope.Children[0].Predicate.MaxValue);

            source.Presentation.SelectTimeline("approach");
            source.Presentation.SelectedRecipeEventStableKey = "condition-owner:timeline:approach";
            BehaviorRecipeTriggerEventInspectorProjection.Rebuild(source, payload, null);
            Assert.IsTrue(source.TriggerEventInspector.ConditionOwnerOnly);
            Assert.AreEqual(BehaviorRecipeInspectorMode.Timeline,
                source.TriggerEventInspector.InspectorMode);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TimelineEntryConditionOwnerId,
                source.TriggerEventInspector.EventId);
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TimelineEntryConditionOwnerId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAnyGroup,
                -1,
                default,
                out bool entryChanged,
                out string entryDiagnostic), entryDiagnostic);
            Assert.IsTrue(entryChanged);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any,
                payload.Timelines[0].EntryConditionRoot.Kind);
            Assert.AreEqual(originalEventCount, payload.Timelines[0].Events.Count,
                "Editing property condition trees must not create timeline Events.");

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TimelineEntryConditionOwnerId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TimelineTitle,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    stringValue: "持续靠近（已编辑）"),
                out bool titleChanged,
                out string titleDiagnostic), titleDiagnostic);
            Assert.IsTrue(titleChanged);
            Assert.AreEqual("持续靠近（已编辑）", payload.Timelines[0].Title);

            payload.EntryTimelineId = string.Empty;
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TimelineEntryConditionOwnerId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.SetEntryTimeline,
                -1,
                default,
                out bool entryTimelineChanged,
                out string entryTimelineDiagnostic), entryTimelineDiagnostic);
            Assert.IsTrue(entryTimelineChanged);
            Assert.AreEqual("approach", payload.EntryTimelineId);
        }

        [Test]
        public void RecipeInspectorRefreshesWhenOnlyBehaviorCardDraftNameChanges()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestBehaviorRecipeReferenceCompilation.Compile(payload);
            Assert.IsTrue(compile.Success);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            viewModel.BehaviorCardWorkspace.Timeline.Presentation.SelectRecipe();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "旧名称",
                compile,
                payload,
                0);
            Assert.AreEqual("旧名称", viewModel.BehaviorCardWorkspace.Timeline.CardTitleText);
            Assert.AreEqual("旧名称", viewModel.BehaviorCardWorkspace.Timeline.TriggerEventInspector.DisplayTitle);

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "新名称",
                compile,
                payload,
                0);
            Assert.AreEqual("新名称", viewModel.BehaviorCardWorkspace.Timeline.CardTitleText);
            Assert.AreEqual("新名称", viewModel.BehaviorCardWorkspace.Timeline.TriggerEventInspector.DisplayTitle);
        }

        [Test]
        public void RecipeTimeline_V6ConditionMutationAddsRelativeGroupsAndReordersSiblings()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.Timelines[0].Events[1];

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAnyGroup,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    stringValue: "condition.target"),
                out bool groupAdded,
                out string groupDiagnostic), groupDiagnostic);
            Assert.IsTrue(groupAdded);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All,
                evt.ConditionRoot.Kind);
            Assert.AreEqual("condition.target", evt.ConditionRoot.Children[0].NodeId);
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode anyGroup = evt.ConditionRoot.Children[1];
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any, anyGroup.Kind);
            Assert.AreEqual(2, anyGroup.Children.Count);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddCondition,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    stringValue: anyGroup.NodeId),
                out bool childAdded,
                out string childDiagnostic), childDiagnostic);
            Assert.IsTrue(childAdded);
            Assert.AreEqual(3, anyGroup.Children.Count,
                "Adding relative to a selected All/Any group must create a child, not wrap the tree.");

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.MoveConditionNode,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    intValue: -1,
                    stringValue: anyGroup.NodeId),
                out bool moved,
                out string moveDiagnostic), moveDiagnostic);
            Assert.IsTrue(moved);
            Assert.AreEqual(anyGroup.NodeId, evt.ConditionRoot.Children[0].NodeId);
            Assert.AreEqual("condition.target", evt.ConditionRoot.Children[1].NodeId);
        }

        [Test]
        public void RecipeTimeline_V8ConditionGroupKindIsEditedOnTheGroupItself()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = BuildV6TimelinePayload();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.Timelines[0].Events[1];

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddCondition,
                -1,
                default,
                out bool conditionAdded,
                out string addDiagnostic), addDiagnostic);
            Assert.IsTrue(conditionAdded);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All,
                evt.ConditionRoot.Kind);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.SetConditionGroupKind,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any,
                    stringValue: evt.ConditionRoot.NodeId),
                out bool groupChanged,
                out string groupDiagnostic), groupDiagnostic);
            Assert.IsTrue(groupChanged);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any,
                evt.ConditionRoot.Kind);

            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                "approach",
                "event.exit",
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.SetConditionGroupKind,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not,
                    stringValue: evt.ConditionRoot.NodeId),
                out _,
                out string notDiagnostic));
            StringAssert.Contains("只能包含一个", notDiagnostic);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload BuildV6TimelinePayload()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                BehaviorRecipeVersion = 8,
                EntryTimelineId = "approach"
            };
            var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            {
                TimelineId = "approach",
                Order = 0,
                Title = "持续靠近",
                EntryConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = "entry.target",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    Predicate = new CharacterTestRuntimeAiLabCardCondition
                    {
                        ConditionId = "entry.target",
                        Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                        Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                        BoolValue = true
                    }
                }
            };
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.move",
                TimelineId = timeline.TimelineId,
                Title = "接近目标",
                StartFrame = 0,
                DurationFrames = 30,
                MaxExecutionsPerPass = 1,
                Priority = 100,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget,
                    MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                    MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget,
                    MoveMaxDistance = 1.5f
                }
            });
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.exit",
                TimelineId = timeline.TimelineId,
                Title = "目标消失后退出",
                StartFrame = 0,
                DurationFrames = 30,
                MaxExecutionsPerPass = 1,
                Priority = 200,
                ConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = "condition.target",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    Predicate = new CharacterTestRuntimeAiLabCardCondition
                    {
                        ConditionId = "condition.target",
                        Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                        Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                        BoolValue = false
                    }
                },
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner
                }
            });
            payload.Timelines.Add(timeline);
            CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.SynchronizeForWrite(payload);
            return payload;
        }

        private static CharacterTestRuntimeAiPlannerDevSceneController PrepareRecipeController(
            GameObject host)
        {
            CharacterTestRuntimeAiPlannerDevSceneController controller =
                host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
            controller.PrepareInitialSetupState();
            Assert.That(
                controller.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                Is.True,
                presetMessage);
            if (controller.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer)
                controller.ToggleAuthoringMode();
            return controller;
        }

        private static string FindNextStageId(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabLogicStage source)
        {
            CharacterTestRuntimeAiLabLogicStage next = null;
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage candidate = payload.Stages[i];
                if (candidate == null
                    || candidate.Order <= source.Order
                    || (next != null && candidate.Order >= next.Order))
                    continue;
                next = candidate;
            }
            return next?.StageId ?? CharacterTestRuntimeAiLabLogicTransition.EndTarget;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeTimeline FindRecipeTimeline(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string timelineId)
        {
            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[i];
                if (string.Equals(timeline?.TimelineId, timelineId, StringComparison.Ordinal))
                    return timeline;
            }
            Assert.Fail("Recipe Timeline was not found: " + timelineId);
            return null;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent FindRecipeEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            string eventId)
        {
            for (int i = 0; timeline != null && i < timeline.TriggerEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent recipeEvent = timeline.TriggerEvents[i];
                if (string.Equals(recipeEvent?.EventId, eventId, StringComparison.Ordinal))
                    return recipeEvent;
            }
            Assert.Fail("Recipe Event was not found: " + eventId);
            return null;
        }

        private static BehaviorRecipeEventViewModel FindProjectedEvent(
            BehaviorRecipeTimelineViewModel timeline,
            string eventId)
        {
            for (int i = 0; timeline != null && i < timeline.TrackCount; i++)
            {
                BehaviorRecipeEventViewModel candidate = timeline.GetTrack(i).Event;
                if (candidate != null && string.Equals(candidate.EventId, eventId, StringComparison.Ordinal))
                    return candidate;
            }
            return null;
        }

        private static BehaviorRecipeTrackViewModel FindProjectedTrack(
            BehaviorRecipeTimelineViewModel timeline,
            string eventId)
        {
            for (int i = 0; timeline != null && i < timeline.TrackCount; i++)
            {
                BehaviorRecipeTrackViewModel candidate = timeline.GetTrack(i);
                if (candidate?.Event != null
                    && string.Equals(candidate.Event.EventId, eventId, StringComparison.Ordinal))
                    return candidate;
            }
            Assert.Fail("Projected Event was not found: " + eventId);
            return null;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent FindNewRecipeEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            string[] existingEventIds)
        {
            for (int i = 0; timeline != null && i < timeline.TriggerEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent candidate = timeline.TriggerEvents[i];
                bool existed = false;
                for (int existingIndex = 0; existingIndex < existingEventIds.Length; existingIndex++)
                {
                    if (!string.Equals(
                            existingEventIds[existingIndex],
                            candidate?.EventId,
                            StringComparison.Ordinal))
                        continue;
                    existed = true;
                    break;
                }
                if (!existed)
                    return candidate;
            }
            return null;
        }

        private static string[] RecipeEventIds(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline)
        {
            if (timeline == null)
                return Array.Empty<string>();
            var result = new string[timeline.TriggerEvents.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = timeline.TriggerEvents[i]?.EventId ?? string.Empty;
            return result;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel BuildRecipeSource()
        {
            return new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
            {
                Available = true,
                StageCount = 2,
                DetectorCount = 2,
                TransitionCount = 2,
                HorizontalEndFrame = 240,
                Stages = new[]
                {
                    new CharacterTestRuntimeAiLabLogicStageViewModel
                    {
                        Index = 0,
                        Order = 0,
                        StageId = "stage.move",
                        Title = "移动至目标",
                        Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget,
                        DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent,
                        DurationFrames = 120,
                        Entry = true
                    },
                    new CharacterTestRuntimeAiLabLogicStageViewModel
                    {
                        Index = 1,
                        Order = 1,
                        StageId = "stage.attack",
                        Title = "基础攻击",
                        Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                        DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed,
                        DurationFrames = 60
                    }
                },
                Detectors = new[]
                {
                    new CharacterTestRuntimeAiLabLogicDetectorViewModel
                    {
                        Index = 0,
                        StageIndex = 0,
                        DetectorId = "detector.arrived",
                        Title = "到达目标",
                        Priority = 100,
                        StartFrame = 0,
                        DurationFrames = 120,
                        WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd
                    },
                    new CharacterTestRuntimeAiLabLogicDetectorViewModel
                    {
                        Index = 1,
                        StageIndex = 0,
                        DetectorId = "detector.path",
                        Title = "路径不可用",
                        Priority = 50,
                        StartFrame = 0,
                        DurationFrames = 120,
                        WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite
                    }
                },
                Transitions = new[]
                {
                    new CharacterTestRuntimeAiLabLogicTransitionViewModel
                    {
                        Index = 0,
                        DetectorIndex = 0,
                        TransitionId = "transition.next",
                        TargetText = "进入基础攻击"
                    },
                    new CharacterTestRuntimeAiLabLogicTransitionViewModel
                    {
                        Index = 1,
                        DetectorIndex = 1,
                        TransitionId = "transition.replan",
                        TargetText = "重新决策"
                    }
                }
            };
        }
    }
}
