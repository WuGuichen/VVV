using System;
using System.Reflection;
using MxFramework.UI;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceTests
    {

        [Test]
        public void RuntimeActualDurationChangesCardStatusAndWorkspaceRevision()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(
                    CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture());
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.That(compile.Success, Is.True);
            var runtime = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                EntityId = 17,
                CurrentStageIndex = 0,
                StageEnteredFrame = 10,
                PendingTransitionIndex = -1,
                DetectorArmed = new bool[compile.Program.Detectors.Count],
                PrimaryActionState = (int)CharacterTestRuntimeAiLabLogicActionState.Running,
                PrimaryActionStageIndex = 0
            };
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Pursuit Dash Strike",
                compile,
                payload,
                0,
                runtimeState: runtime,
                runtimeFrame: 40);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(viewModel);
            long firstRevision = viewModel.BehaviorCardWorkspace.WorkspaceRevision;
            Assert.That(viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActualDurationFrames,
                Is.EqualTo(30));
            StringAssert.Contains("30f", viewModel.BehaviorCardWorkspace.Timeline.Stages[0].RuntimeStatusText);

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Pursuit Dash Strike",
                compile,
                payload,
                0,
                runtimeState: runtime,
                runtimeFrame: 41);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(viewModel);

            Assert.That(viewModel.BehaviorCardWorkspace.WorkspaceRevision, Is.Not.EqualTo(firstRevision));
            StringAssert.Contains("31f", viewModel.BehaviorCardWorkspace.Timeline.Stages[0].RuntimeStatusText);
        }

        [Test]
        public void SameFrameDetectorHitsShowOneCommittedWinnerAndPendingRestoresWithoutOnsetEvent()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(
                    CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture());
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.That(compile.Success, Is.True);
            Assert.That(compile.Program.Detectors.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(compile.Program.Transitions.Count, Is.GreaterThanOrEqualTo(2));
            const string BehaviorEntryId = "entry.action-chain";
            string firstDetectorTransitionId = string.Empty;
            string secondDetectorTransitionId = string.Empty;
            for (int transitionIndex = 0;
                 transitionIndex < compile.Program.Transitions.Count;
                 transitionIndex++)
            {
                CharacterTestRuntimeAiLabCompiledLogicTransition transition =
                    compile.Program.Transitions[transitionIndex];
                if (transition.DetectorIndex == 0 && firstDetectorTransitionId.Length == 0)
                    firstDetectorTransitionId = transition.TransitionId;
                if (transition.DetectorIndex == 1 && secondDetectorTransitionId.Length == 0)
                    secondDetectorTransitionId = transition.TransitionId;
            }
            Assert.That(firstDetectorTransitionId, Is.Not.Empty);
            Assert.That(secondDetectorTransitionId, Is.Not.Empty);
            var runtime = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                EntityId = 17,
                CurrentStageIndex = 0,
                StageEnteredFrame = 10,
                PendingTransitionIndex = -1,
                DetectorArmed = new bool[compile.Program.Detectors.Count]
            };
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer(4);
            events.Add(new CharacterTestRuntimeAiLabLogicEvent(
                40,
                CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered,
                compile.Program.Stages[0].StageId,
                compile.Program.Detectors[0].DetectorId,
                firstDetectorTransitionId,
                runtime.EntityId,
                BehaviorEntryId,
                compile.Program.SemanticHash));
            events.Add(new CharacterTestRuntimeAiLabLogicEvent(
                40,
                CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered,
                compile.Program.Stages[0].StageId,
                compile.Program.Detectors[1].DetectorId,
                secondDetectorTransitionId,
                runtime.EntityId,
                BehaviorEntryId,
                compile.Program.SemanticHash));
            events.Add(new CharacterTestRuntimeAiLabLogicEvent(
                40,
                CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted,
                compile.Program.Stages[0].StageId,
                string.Empty,
                compile.Program.Transitions[0].TransitionId,
                runtime.EntityId,
                BehaviorEntryId,
                compile.Program.SemanticHash));
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Pursuit Dash Strike",
                compile,
                payload,
                0,
                runtimeState: runtime,
                latestEvents: events,
                runtimeFrame: 40,
                behaviorEntryId: BehaviorEntryId);

            int detectorHitCount = 0;
            int committedCount = 0;
            for (int i = 0; i < viewModel.BehaviorCardWorkspace.Timeline.TransitionCount; i++)
            {
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition =
                    viewModel.BehaviorCardWorkspace.Timeline.Transitions[i];
                if (transition.DetectorHit)
                    detectorHitCount++;
                if (transition.Committed)
                    committedCount++;
            }
            Assert.That(detectorHitCount, Is.EqualTo(2));
            Assert.That(committedCount, Is.EqualTo(1));
            Assert.That(
                viewModel.BehaviorCardWorkspace.Timeline.Transitions[0].RuntimeFrame,
                Is.EqualTo(40));

            events.Clear();
            runtime.PendingTransitionIndex = 1;
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Pursuit Dash Strike",
                compile,
                payload,
                0,
                runtimeState: runtime,
                latestEvents: events,
                runtimeFrame: 41,
                behaviorEntryId: BehaviorEntryId);

            Assert.That(
                viewModel.BehaviorCardWorkspace.Timeline.Transitions[1].Pending,
                Is.True);
            Assert.That(
                viewModel.BehaviorCardWorkspace.Timeline.Transitions[0].Committed,
                Is.False);
        }

        [Test]
        public void WorkspaceRevisionIncludesTransitionRuntimeFrame()
        {
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            timeline.Transitions = new[]
            {
                new CharacterTestRuntimeAiLabLogicTransitionViewModel
                {
                    TransitionId = "transition.runtime",
                    RuntimeFrame = 10
                }
            };
            timeline.TransitionCount = 1;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(viewModel);
            long first = viewModel.BehaviorCardWorkspace.WorkspaceRevision;

            timeline.Transitions[0].RuntimeFrame = 11;
            viewModel.BehaviorCardWorkspace.WorkspaceRevision = 0L;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(viewModel);

            Assert.That(viewModel.BehaviorCardWorkspace.WorkspaceRevision, Is.Not.EqualTo(first));
        }

        [Test]
        public void WorkspaceRevisionIncludesStagePresentationRevision()
        {
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(viewModel, 10L);
            long first = viewModel.BehaviorCardWorkspace.WorkspaceRevision;

            viewModel.BehaviorCardWorkspace.WorkspaceRevision = 0L;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(viewModel, 11L);

            Assert.That(viewModel.BehaviorCardWorkspace.WorkspaceRevision, Is.Not.EqualTo(first));
        }

        [Test]
        public void TransitionRoutesUseTheFrozenStableSortKey()
        {
            var forwardLow = Route("transition.forward.low", 0, 2, 10);
            var forwardHighB = Route("transition.forward.high.b", 0, 2, 20);
            var forwardHighA = Route("transition.forward.high.a", 0, 2, 20);
            var back = Route("transition.back", 2, 0, 500);
            var self = Route("transition.self", 1, 1, 500);
            var end = Route("transition.end", 0, -1, 500, endsCard: true);
            var card = Route("transition.card", -1, 1, 500);
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] routes =
            {
                card,
                self,
                forwardLow,
                end,
                forwardHighB,
                back,
                forwardHighA
            };

            Array.Sort(
                routes,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .CompareTransitionRouteOrder);

            Assert.That(routes[0], Is.SameAs(forwardHighA));
            Assert.That(routes[1], Is.SameAs(forwardHighB));
            Assert.That(routes[2], Is.SameAs(forwardLow));
            Assert.That(routes[3], Is.SameAs(back));
            Assert.That(routes[4], Is.SameAs(self));
            Assert.That(routes[5], Is.SameAs(end));
            Assert.That(routes[6], Is.SameAs(card));
        }

        [Test]
        public void BackEdgeBezierStaysOutsideNonEndpointCardObstacle()
        {
            const float sourceX = 944f;
            const float targetX = -6f;
            const float endpointY = 150f;
            const float routeY = 520f;
            Rect obstacle = new Rect(338f, 68f, 224f, 256f);
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .TransitionRouteIntersectsObstacle(
                        sourceX,
                        endpointY,
                        targetX,
                        endpointY,
                        routeY,
                        obstacle),
                Is.False);
        }

        [Test]
        public void CardDetectorRouteEscapesSidewaysBeforeCrossingStageMatrix()
        {
            const float sourceX = 780f;
            const float sourceY = 113f;
            const float targetX = 1223.2f;
            const float targetY = 474f;
            const float routeY = 900f;
            Rect intermediateStage = new Rect(389.6f, 444f, 554.4f, 338f);

            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .TransitionRouteIntersectsObstacle(
                        sourceX,
                        sourceY,
                        targetX,
                        targetY,
                        routeY,
                        intermediateStage),
                Is.True,
                "The direct vertical descent reproduces the card-level detector failure.");
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .TransitionRouteIntersectsObstacle(
                        sourceX,
                        sourceY,
                        targetX,
                        targetY,
                        routeY,
                        escapeX: 1864f,
                        obstacle: intermediateStage),
                Is.False,
                "A side gutter must clear the stage matrix before the route descends.");
        }

        [Test]
        public void SharedCardDetectorPortCorridorAllowsOutwardEscapeOnly()
        {
            const float sourceX = 100f;
            const float sourceY = 40f;
            const float targetX = 300f;
            const float targetY = 40f;
            const float routeY = 200f;
            Rect siblingCard = new Rect(0f, 0f, 100f, 80f);
            Rect siblingBody =
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .TrimSharedDetectorPortCorridor(
                        siblingCard,
                        sourceX,
                        2f);

            Assert.That(siblingBody.xMax, Is.EqualTo(98f));
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .TransitionRouteIntersectsObstacle(
                        sourceX,
                        sourceY,
                        targetX,
                        targetY,
                        routeY,
                        escapeX: 200f,
                        obstacle: siblingBody),
                Is.False,
                "The aligned two-pixel output strip must allow an outward escape.");
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .TransitionRouteIntersectsObstacle(
                        sourceX,
                        sourceY,
                        targetX,
                        targetY,
                        routeY,
                        escapeX: -100f,
                        obstacle: siblingBody),
                Is.True,
                "An opposite escape must still collide after entering the sibling card body.");
        }

        [Test]
        public void TransitionRouteObstacleSamplerRejectsExpandedTransitionChip()
        {
            const float sourceX = 120f;
            const float targetX = 980f;
            const float endpointY = 180f;
            const float routeY = 540f;
            Vector2 blockingPoint =
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .EvaluateTransitionRoutePoint(
                        sourceX,
                        endpointY,
                        targetX,
                        endpointY,
                        routeY,
                        0.75f);
            Rect blockingChip = new Rect(
                blockingPoint.x - 72f,
                blockingPoint.y - 10f,
                144f,
                20f);
            Rect outerChip = new Rect(478f, 220f, 144f, 72f);

            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .TransitionRouteIntersectsObstacle(
                        sourceX,
                        endpointY,
                        targetX,
                        endpointY,
                        routeY,
                        blockingChip),
                Is.True);
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .TransitionRouteIntersectsObstacle(
                        sourceX,
                        endpointY,
                        targetX,
                        endpointY,
                        720f,
                        outerChip),
                Is.False);
        }

        [Test]
        public void TransitionRoutesUseSmoothCubicDirectionalCurvesAndBoundedLaneBudget()
        {
            Vector2 start = CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                .EvaluateTransitionRoutePoint(120f, 180f, 980f, 220f, 540f, 1100f, 0f);
            Vector2 firstCurve = CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                .EvaluateTransitionRoutePoint(120f, 180f, 980f, 220f, 540f, 1100f, 0.25f);
            Vector2 secondCurve = CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                .EvaluateTransitionRoutePoint(120f, 180f, 980f, 220f, 540f, 1100f, 0.75f);
            Vector2 end = CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                .EvaluateTransitionRoutePoint(120f, 180f, 980f, 220f, 540f, 1100f, 1f);

            Assert.That(start, Is.EqualTo(new Vector2(120f, 180f)));
            Assert.That(end, Is.EqualTo(new Vector2(980f, 220f)));
            Assert.That(firstCurve.x, Is.InRange(120.01f, 1099.99f));
            Assert.That(firstCurve.y, Is.InRange(180.01f, 539.99f));
            Assert.That(secondCurve.x, Is.InRange(980.01f, 1099.99f));
            Assert.That(secondCurve.y, Is.InRange(220.01f, 539.99f));
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveTransitionRouteLaneBudget(7, 11),
                Is.EqualTo(19));
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveTransitionRouteLaneBudget(0, 0),
                Is.EqualTo(1));
        }

        [Test]
        public void TransitionRouteBaselineScrollsWithItsContent()
        {
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveScrolledRouteBaseY(920f, 480f),
                Is.EqualTo(440f));
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveScrolledRouteBaseY(920f, -120f),
                Is.EqualTo(920f));
        }

        [Test]
        public void SelfLoopUsesLocalGutterWhileOtherRoutesUseOuterGutter()
        {
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveTransitionRouteBaseYForClass(
                        CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                            .TransitionRouteClass.SelfLoop,
                        outerRouteBaseY: 900f,
                        sourceStageBottom: 340f,
                        sourceDetectorBottom: 372f),
                Is.EqualTo(384f));
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveTransitionRouteBaseYForClass(
                        CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                            .TransitionRouteClass.BackEdge,
                        outerRouteBaseY: 900f,
                        sourceStageBottom: 340f,
                        sourceDetectorBottom: 372f),
                Is.EqualTo(900f));
        }

        [Test]
        public void DetectorVerticalReorderOnlyUsesItsVisiblePriorityCohort()
        {
            var stageTop = DetectorRank(0, 100, 0, cardScope: false);
            var cardTop = DetectorRank(-1, 90, 1, cardScope: true);
            var otherStage = DetectorRank(1, 80, 2, cardScope: false);
            var source = DetectorRank(0, 70, 3, cardScope: false);
            var cardBottom = DetectorRank(-1, 60, 4, cardScope: true);
            var stageBottom = DetectorRank(0, 10, 5, cardScope: false);
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors =
            {
                stageTop,
                cardTop,
                otherStage,
                source,
                cardBottom,
                stageBottom
            };

            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveDetectorPriorityForCohortRankMove(
                        detectors,
                        detectors.Length,
                        source,
                        moveUp: true,
                        steps: 1,
                        fallbackPriority: source.Priority),
                Is.EqualTo(101));
            Assert.That(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                    .ResolveDetectorPriorityForCohortRankMove(
                        detectors,
                        detectors.Length,
                        source,
                        moveUp: false,
                        steps: 1,
                        fallbackPriority: source.Priority),
                Is.EqualTo(9));
        }

        private static CharacterTestRuntimeAiLabLogicTransitionViewModel Route(
            string id,
            int source,
            int target,
            int priority,
            bool endsCard = false)
        {
            return new CharacterTestRuntimeAiLabLogicTransitionViewModel
            {
                TransitionId = id,
                SourceStageIndex = source,
                TargetStageIndex = target,
                Priority = priority,
                EndsCard = endsCard
            };
        }

        private static CharacterTestRuntimeAiLabLogicDetectorViewModel DetectorRank(
            int stageIndex,
            int priority,
            int rank,
            bool cardScope)
        {
            return new CharacterTestRuntimeAiLabLogicDetectorViewModel
            {
                StageIndex = stageIndex,
                Priority = priority,
                PriorityRank = rank,
                CardScope = cardScope
            };
        }
    }
}
