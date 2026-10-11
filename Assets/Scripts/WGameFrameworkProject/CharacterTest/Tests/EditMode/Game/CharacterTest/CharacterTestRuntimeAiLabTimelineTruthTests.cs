using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FairyGUI;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.Tests
{
    [TestFixture]
    public sealed class CharacterTestRuntimeAiLabTimelineTruthTests
    {
        [Test]
        public void Projection_AllowsRetentionWithoutChangingSessionIdentity()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(2);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            Capture(timeline, index, 10);
            Capture(timeline, index, 11);
            Capture(timeline, index, 12);

            CharacterTestRuntimeAiLabTimelineProjection projection = Project(timeline, index, 4);

            Assert.AreEqual(10, projection.Domain.SessionStartFrame);
            Assert.AreEqual(11, projection.Domain.RetainedStartFrame);
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineValidity.Valid, projection.Validity);
            AssertNoItemOutside(projection, 11, 12);
        }

        [Test]
        public void Projection_RejectsOldEventIndexWhenSameStartFrameBeginsNewGeneration()
        {
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            timeline.BeginSession(Identity(2, 10, 4));
            index.Reset(Identity(1, 10, 4));
            timeline.AddRecord(10, 0f, 0d, Snapshot(), null, null, 10L);
            index.CaptureFrame(10, null, null, null, null);

            CharacterTestRuntimeAiLabTimelineProjection projection = Project(timeline, index, 4);

            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineValidity.SessionMismatch, projection.Validity);
            AssertEventTracksUnavailableAndEmpty(projection);
        }

        [Test]
        public void Projection_RejectsCapturedEmptyFutureSlot()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            Capture(timeline, index, 10);
            index.CaptureFrame(11, null, null, null, null);

            CharacterTestRuntimeAiLabTimelineProjection projection = Project(timeline, index, 4);

            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineValidity.EventAfterLatest, projection.Validity);
            Assert.AreEqual(11, projection.DiagnosticFrame);
            AssertEventTracksUnavailableAndEmpty(projection);
            AssertNoItemOutside(projection, 10, 10);
        }

        [Test]
        public void EventIndex_RepeatedEmptyCaptureCountsOnceAndTruncateClearsCapturedSlot()
        {
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(Identity(1, 10, 4));

            index.CaptureFrame(10, null, null, null, null);
            index.CaptureFrame(10, null, null, null, null);
            index.CaptureFrame(11, null, null, null, null);

            Assert.AreEqual(2, index.CapturedFrameCount);
            Assert.IsTrue(index.IsFrameCaptured(11));
            index.TruncateAfter(10);
            Assert.AreEqual(1, index.CapturedFrameCount);
            Assert.IsFalse(index.IsFrameCaptured(11));
            Assert.AreEqual(0, index.GetCount(11));
        }

        [Test]
        public void EventIndex_ControlCommandsBelongToTargetAgentAndFormContinuousActionSpan()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            timeline.AddRecord(10, 0f, 0d, Snapshot(Agent()), null, null, 10L);
            timeline.AddRecord(11, 0f, 0d, Snapshot(Agent()), null, null, 11L);
            var first = new RuntimeCommand(
                new RuntimeFrame(10),
                sourceId: CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                commandId: CharacterTestRuntimeCommandIds.SetControlIntent,
                targetId: 1,
                traceId: new RuntimeTraceIdentity(964711));
            var second = new RuntimeCommand(
                new RuntimeFrame(11),
                sourceId: CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                commandId: CharacterTestRuntimeCommandIds.SetControlIntent,
                targetId: 1,
                traceId: new RuntimeTraceIdentity(964711));

            index.CaptureFrame(10, new[] { first }, null, null, null);
            index.CaptureFrame(11, new[] { second }, null, null, null);

            CharacterTestRuntimeAiLabTimelineEventSample sample = index.GetSample(10, 0);
            Assert.AreEqual(1, sample.EntityId);
            Assert.AreEqual(CharacterTestRuntimeCommandSources.RuntimeAiPlanner, sample.RelatedEntityId);
            Assert.AreEqual(CharacterTestRuntimeCommandIds.SetControlIntent, sample.TraceNumber);
            Assert.IsNotEmpty(sample.Label);

            CharacterTestRuntimeAiLabTimelineProjection projection =
                Project(timeline, index, 4, entityId: 1);
            CharacterTestRuntimeAiLabTimelineItem[] actions = LeafItems(projection.Tracks[2]);
            Assert.AreEqual(1, actions.Length);
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineItemKind.Span, actions[0].ItemKind);
            Assert.AreEqual(10, actions[0].StartFrame);
            Assert.AreEqual(11, actions[0].EndFrame);
        }

        [Test]
        public void Projection_DecisionTraceBuildsFiveSemanticTracksAndCoalescesHeldFrames()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            CaptureDecisionTrace(timeline, index, identity, 10, committed: true);
            CaptureDecisionTrace(timeline, index, identity, 11, committed: false);

            CharacterTestRuntimeAiLabTimelineProjection projection =
                Project(timeline, index, 4, entityId: 1);

            CollectionAssert.AreEqual(
                new[] { "感知", "决策", "动作", "结果", "警告" },
                projection.Tracks.Select(track => track.Label).ToArray());
            Assert.AreEqual(1, LeafItems(projection.Tracks[0]).Length);
            CharacterTestRuntimeAiLabTimelineItem[] decisions =
                LeafItems(projection.Tracks[1]);
            Assert.AreEqual(1, decisions.Length);
            Assert.AreEqual(10, decisions[0].StartFrame);
            Assert.AreEqual(11, decisions[0].EndFrame);
            StringAssert.Contains("3.0m", decisions[0].Label);
            Assert.AreEqual(1, LeafItems(projection.Tracks[2]).Length);
            Assert.AreEqual(1, LeafItems(projection.Tracks[3]).Length);
            Assert.AreEqual(0, LeafItems(projection.Tracks[4]).Length);
        }

        [Test]
        public void Projection_EnqueuedCommandWithoutRuntimeObservationIsNotReportedAsCompleted()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            CaptureDecisionTrace(
                timeline,
                index,
                identity,
                10,
                committed: true,
                runtimeEffectKind:
                    CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved);

            CharacterTestRuntimeAiLabTimelineProjection projection =
                Project(timeline, index, 4, entityId: 1);
            CharacterTestRuntimeAiLabTimelineItem result =
                LeafItems(projection.Tracks[3]).Single();

            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineSeverity.Warning,
                result.Severity);
            Assert.AreEqual("已入队 · 尚未执行", result.Label);
            Assert.AreEqual(
                "控制意图：已入队 · 动作状态：尚未观测到开始、完成或失败",
                result.Tooltip);
        }

        [Test]
        public void Projection_OverflowHidesAllPartialEventTracks()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            timeline.AddRecord(10, 0f, 0d, Snapshot(), null, null, 10L);
            var events = new List<CharacterTestRuntimeEvent>(100);
            for (int i = 0; i < 100; i++)
            {
                events.Add(new CharacterTestRuntimeEvent(
                    new RuntimeFrame(10),
                    CharacterTestRuntimeEventType.CommandAccepted,
                    i + 1,
                    i + 1,
                    0));
            }
            index.CaptureFrame(10, null, events, null, null);

            CharacterTestRuntimeAiLabTimelineProjection projection = Project(timeline, index, 4);

            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineValidity.EventSlotOverflow, projection.Validity);
            Assert.AreEqual(10, projection.DiagnosticFrame);
            Assert.AreEqual(37, projection.DiagnosticValue);
            AssertEventTracksUnavailableAndEmpty(projection);
        }

        [Test]
        public void Projection_CanonicalKeysIgnoreInputOrderAndRankExactDuplicates()
        {
            CharacterTestRuntimeEvent first = Event(10, 2, 1, "beta");
            CharacterTestRuntimeEvent second = Event(10, 1, 2, "alpha");
            CharacterTestRuntimeAiLabTimelineProjection forward = ProjectEvents(new[] { first, second, second });
            CharacterTestRuntimeAiLabTimelineProjection reverse = ProjectEvents(new[] { second, first, second });

            string[] forwardKeys = LeafKeys(forward.Tracks[2]);
            string[] reverseKeys = LeafKeys(reverse.Tracks[2]);
            CollectionAssert.AreEqual(forwardKeys, reverseKeys);
            Assert.AreEqual(3, forwardKeys.Length);
            Assert.AreNotEqual(forwardKeys[0], forwardKeys[1]);
            int[] ranks = LeafItems(forward.Tracks[2]).Select(item => item.SourceOrder).ToArray();
            CollectionAssert.Contains(ranks, 0);
            CollectionAssert.Contains(ranks, 1);
        }

        [Test]
        public void Projection_StableKeysDistinguishTextCategoryAndNumericTrace()
        {
            CharacterTestRuntimeAiLabTimelineProjection projection = ProjectSamples(new[]
            {
                Sample(category: "alpha:2", severity: CharacterTestRuntimeAiLabTimelineSeverity.Warning, sourceTraceId: new RuntimeTraceIdentity(1201301)),
                Sample(category: "alpha", severity: CharacterTestRuntimeAiLabTimelineSeverity.Warning, sourceTraceId: new RuntimeTraceIdentity(1201302))
            });

            CharacterTestRuntimeAiLabTimelineItem[] leaves = LeafItems(projection.Tracks[2]);
            Assert.AreEqual(2, leaves.Length);
            Assert.AreNotEqual(leaves[0].StableKey, leaves[1].StableKey);
        }

        [Test]
        public void Projection_DuplicateActionLifecycleSamplesProduceDistinctSpans()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            Capture(timeline, index, 10);
            Capture(timeline, index, 11);
            SetSamples(index, 10, new[]
            {
                ActionSample(10, CharacterTestRuntimeAiLabTimelineEventKind.ActionStarted, sourceOrder: 0),
                ActionSample(10, CharacterTestRuntimeAiLabTimelineEventKind.ActionStarted, sourceOrder: 1)
            });
            SetSamples(index, 11, new[]
            {
                ActionSample(11, CharacterTestRuntimeAiLabTimelineEventKind.ActionEnded, sourceOrder: 0),
                ActionSample(11, CharacterTestRuntimeAiLabTimelineEventKind.ActionEnded, sourceOrder: 1)
            });

            CharacterTestRuntimeAiLabTimelineItem[] leaves = LeafItems(Project(timeline, index, 4).Tracks[2]);

            Assert.AreEqual(2, leaves.Length);
            Assert.IsTrue(leaves.All(item => item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.Span));
            Assert.AreNotEqual(leaves[0].StableKey, leaves[1].StableKey);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, leaves.Select(item => item.SourceOrder).ToArray());
        }

        [Test]
        public void Projection_BehaviorLogicEventsBelongToDecisionLaneWithRecordIdentity()
        {
            CharacterTestRuntimeAiLabTimelineProjection projection = ProjectSamples(new[]
            {
                new CharacterTestRuntimeAiLabTimelineEventSample(
                    10,
                    CharacterTestRuntimeAiLabTimelineEventKind.BehaviorStageEntered,
                    7,
                    0,
                    "BehaviorLogic.StageEntered",
                    CharacterTestRuntimeAiLabTimelineSeverity.Success,
                    "stage.pursue",
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0029),
                    0,
                    behaviorEntryId: "card.pursuit",
                    behaviorStepId: "stage.pursue")
            });

            CharacterTestRuntimeAiLabTimelineItem[] decision = LeafItems(projection.Tracks[1]);
            CharacterTestRuntimeAiLabTimelineItem[] action = LeafItems(projection.Tracks[2]);
            Assert.AreEqual(1, decision.Length);
            Assert.AreEqual(0, action.Length);
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineEventKind.BehaviorStageEntered, decision[0].EventKind);
            Assert.AreEqual(1, decision[0].SessionGeneration);
            Assert.Greater(decision[0].RecordGeneration, 0L);
            Assert.AreEqual(10L, decision[0].AuthorityHash);
            Assert.AreEqual("card.pursuit", decision[0].BehaviorEntryId);
            Assert.AreEqual("stage.pursue", decision[0].BehaviorStepId);
        }

        [Test]
        public void Projection_RevisionLaneUsesOnlyAlignedRuntimeFrameRecords()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(3, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            Capture(timeline, index, 10);
            Capture(timeline, index, 11);
            CharacterTestRuntimeAiPlannerDevTimelineRecord first = timeline.Records[0];
            CharacterTestRuntimeAiPlannerDevTimelineRecord second = timeline.Records[1];
            var revision = new CharacterTestRuntimeAiLabRevisionProjectionInput(
                identity,
                99L,
                new[]
                {
                    new CharacterTestRuntimeAiLabRevisionFrame(10, first.RecordGeneration, first.StateHash, 0L, 0, 0),
                    new CharacterTestRuntimeAiLabRevisionFrame(11, second.RecordGeneration, second.StateHash, 0x42L, 0, 1)
                },
                new[] { new CharacterTestRuntimeAiLabRevisionChange(11, 2, 4, 0x42L, "draft.pursuit") },
                CharacterTestRuntimeAiLabTimelineValidity.Valid,
                0,
                string.Empty);

            CharacterTestRuntimeAiLabTimelineProjection projection = Project(timeline, index, 4, 0, revision);
            CharacterTestRuntimeAiLabTimelineItem[] leaves = LeafItems(projection.Tracks[0]);

            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineValidity.Valid, projection.Validity);
            Assert.IsTrue(projection.Tracks[0].Available);
            Assert.AreEqual(3, leaves.Length);
            Assert.AreEqual(1, leaves.Count(item => item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.Marker));
            Assert.IsTrue(leaves.All(item => item.Provenance == CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord));
            Assert.IsFalse(leaves.Any(item => item.StartFrame == 10 && item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.Marker));
        }

        [Test]
        public void Projection_RevisionLaneFailsClosedWhenRecordIdentityDoesNotMatch()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(2, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            Capture(timeline, index, 10);
            CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.Records[0];
            var revision = new CharacterTestRuntimeAiLabRevisionProjectionInput(
                identity,
                7L,
                new[]
                {
                    new CharacterTestRuntimeAiLabRevisionFrame(
                        10,
                        record.RecordGeneration,
                        record.StateHash + 1L,
                        0L,
                        0,
                        0)
                },
                Array.Empty<CharacterTestRuntimeAiLabRevisionChange>(),
                CharacterTestRuntimeAiLabTimelineValidity.Valid,
                0,
                string.Empty);

            CharacterTestRuntimeAiLabTimelineProjection projection = Project(timeline, index, 4, 0, revision);

            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineValidity.RevisionHistoryMismatch, projection.Validity);
            Assert.IsFalse(projection.Tracks[0].Available);
            Assert.AreEqual(0, projection.Tracks[0].Items.Count);
        }

        [Test]
        public void Projection_SameFrameEventsBecomeExactFrameStackWithTypedLeafMembers()
        {
            CharacterTestRuntimeAiLabTimelineProjection projection = ProjectEvents(new[]
            {
                Event(10, 1, 2, "one"),
                Event(10, 2, 1, "two")
            });

            CharacterTestRuntimeAiLabTimelineItem stack = projection.Tracks[2].Items[0];
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack, stack.ItemKind);
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate, stack.Provenance);
            Assert.AreEqual(2, stack.Members.Count);
            for (int i = 0; i < stack.Members.Count; i++)
            {
                Assert.AreEqual(CharacterTestRuntimeAiLabTimelineItemProvenance.TypedEvent, stack.Members[i].Provenance);
                Assert.AreEqual(CharacterTestRuntimeAiLabTimelineItemKind.Marker, stack.Members[i].ItemKind);
            }
        }

        [Test]
        public void Projection_NearbyFramesAtLowZoomBecomeDensityAggregateNotExactStack()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 1000);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            timeline.AddRecord(10, 0f, 0d, Snapshot(), null, null, 10L);
            index.CaptureFrame(10, null, new[] { Event(10, 1, 2, "one") }, null, null);
            timeline.AddRecord(11, 0f, 0d, Snapshot(), null, null, 11L);
            index.CaptureFrame(11, null, new[] { Event(11, 2, 1, "two") }, null, null);

            CharacterTestRuntimeAiLabTimelineProjection projection =
                new CharacterTestRuntimeAiLabTimelineProjector().Build(
                    timeline,
                    index,
                    1000,
                    0,
                    new CharacterTestRuntimeAiLabTimelineViewport(10, 1009, -1, 100f, true));

            CharacterTestRuntimeAiLabTimelineItem density = projection.Tracks[2].Items[0];
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate, density.ItemKind);
            Assert.AreEqual(10, density.StartFrame);
            Assert.AreEqual(11, density.EndFrame);
            Assert.AreEqual(2, density.Members.Count);
            Assert.IsTrue(density.Members.All(item => item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.Marker));
            Assert.IsFalse(CharacterTestRuntimeAiPlannerDevSceneController.CanRebindTimelineSelection(density, density));
        }

        [Test]
        public void ExactFrameStackSelectionRejectsReplacementRecordGeneration()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            timeline.AddRecord(10, 0f, 0d, Snapshot(), null, null, 10L);
            index.CaptureFrame(10, null, new[] { Event(10, 1, 2, "one"), Event(10, 2, 1, "two") }, null, null);
            CharacterTestRuntimeAiLabTimelineItem previous = Project(timeline, index, 4).Tracks[2].Items[0];

            Begin(timeline, index, identity);
            timeline.AddRecord(10, 0f, 0d, Snapshot(), null, null, 10L);
            index.CaptureFrame(10, null, new[] { Event(10, 1, 2, "one"), Event(10, 2, 1, "two") }, null, null);
            CharacterTestRuntimeAiLabTimelineItem replacement = Project(timeline, index, 4).Tracks[2].Items[0];

            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack, previous.ItemKind);
            Assert.AreNotEqual(previous.RecordGeneration, replacement.RecordGeneration);
            Assert.IsFalse(CharacterTestRuntimeAiPlannerDevSceneController.CanRebindTimelineSelection(previous, replacement));
        }

        [Test]
        public void RuntimeFramePageContextPreservesViewportAndExactLocatorAcrossWorkspaceCopies()
        {
            CharacterTestRuntimeAiLabTimelineProjection projection = ProjectEvents(new[]
            {
                Event(10, 1, 2, "one"),
                Event(10, 2, 1, "two")
            });
            CharacterTestRuntimeAiLabTimelineItem leaf = projection.Tracks[2].Items[0].Members[0];
            var viewport = new CharacterTestRuntimeAiLabTimelineViewport(
                10,
                70,
                2,
                1290f,
                false,
                CharacterTestRuntimeAiLabTimelineViewportMode.Manual);
            var runtimeFrame = new CharacterTestRuntimeAiLabRuntimeFramePageContext(
                viewport,
                default,
                leaf.CreateLocator(),
                true);

            CharacterTestRuntimeAiLabPageContext copied = CharacterTestRuntimeAiLabPageContext.CreateDefault()
                .WithRuntimeFrame(runtimeFrame)
                .WithWorkspace(CharacterTestRuntimeAiLabWorkspace.Facts)
                .WithWorkspace(CharacterTestRuntimeAiLabWorkspace.RuntimeFrame);

            Assert.AreEqual(10, copied.RuntimeFrameContext.Viewport.StartFrame);
            Assert.AreEqual(70, copied.RuntimeFrameContext.Viewport.EndFrame);
            Assert.IsTrue(copied.RuntimeFrameContext.DetailsVisible);
            Assert.AreEqual(leaf.StableKey, copied.RuntimeFrameContext.SelectedLocator.StableKey);
            Assert.AreEqual(leaf.RecordGeneration, copied.RuntimeFrameContext.SelectedLocator.RecordGeneration);
        }

        [Test]
        public void AppliedRevisionBoundaryRejectsRestartTruncateAndHashMismatch()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(3, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            timeline.BeginSession(identity);
            timeline.AddRecord(10, 0f, 0d, Snapshot(), null, null, 10L);
            CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.Records[0];
            var applied = new CharacterTestRuntimeAiLabAppliedRevision(10, 0, null, identity.Generation);

            Assert.IsTrue(applied.TryBindRuntimeFrame(identity, record, 0x42L));
            Assert.IsTrue(applied.MatchesRuntimeFrame(identity, record, 0x42L));
            Assert.IsFalse(applied.MatchesRuntimeFrame(Identity(4, 10, 4), record, 0x42L));
            Assert.IsFalse(applied.MatchesRuntimeFrame(identity, record, 0x43L));

            timeline.BeginSession(identity);
            timeline.AddRecord(10, 0f, 0d, Snapshot(), null, null, 10L);
            CharacterTestRuntimeAiPlannerDevTimelineRecord replacement = timeline.Records[0];
            Assert.AreNotEqual(record.RecordGeneration, replacement.RecordGeneration);
            Assert.IsFalse(applied.MatchesRuntimeFrame(identity, replacement, 0x42L));
        }

        [Test]
        public void RevisionChangeRangeRebuildIgnoresStaleReusableBufferTail()
        {
            var frames = new[]
            {
                new CharacterTestRuntimeAiLabRevisionFrame(10, 1L, 10L, 20L, 0, 0),
                new CharacterTestRuntimeAiLabRevisionFrame(11, 2L, 11L, 21L, 0, 0)
            };
            var changes = new[]
            {
                new CharacterTestRuntimeAiLabRevisionChange(10, 0, 1, 10L, "current"),
                new CharacterTestRuntimeAiLabRevisionChange(11, 1, 2, 11L, "stale-tail"),
                new CharacterTestRuntimeAiLabRevisionChange(11, 2, 3, 12L, "stale-tail-2")
            };

            CharacterTestRuntimeAiPlannerDevSceneController.RebuildRevisionChangeRanges(
                frames,
                frames.Length,
                changes,
                retainedChangeCount: 1);

            Assert.AreEqual(1, frames[0].ChangeCount);
            Assert.AreEqual(0, frames[1].ChangeCount);
        }

        [Test]
        public void Projection_CommittedFrameZeroDoesNotFallBackToMutableTimelineSelection()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 0, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            Capture(timeline, index, 0);
            Capture(timeline, index, 1);
            Assert.IsTrue(timeline.SelectFrame(1));

            CharacterTestRuntimeAiLabTimelineProjection projection =
                new CharacterTestRuntimeAiLabTimelineProjector().Build(
                    timeline,
                    index,
                    4,
                    0,
                    new CharacterTestRuntimeAiLabTimelineViewport(0, 3, -1, 1200f, true),
                    revisionInput: null,
                    committedSelectedFrame: 0,
                    hasCommittedSelection: true);

            Assert.AreEqual(0, projection.Domain.SelectedFrame);
        }

        [Test]
        public void Projection_GlobalLimitFlattensAggregatesToAllTypedLeaves()
        {
            const int frameCount = 300;
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, frameCount);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(frameCount);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            for (int offset = 0; offset < frameCount; offset++)
            {
                int frame = 10 + offset;
                Capture(timeline, index, frame);
                SetSamples(index, frame, new[]
                {
                    new CharacterTestRuntimeAiLabTimelineEventSample(
                        frame,
                        CharacterTestRuntimeAiLabTimelineEventKind.Command,
                        1,
                        2,
                        "Command",
                        CharacterTestRuntimeAiLabTimelineSeverity.Info,
                        "event-" + offset,
                        default,
                        0)
                });
            }

            CharacterTestRuntimeAiLabTimelineProjection projection =
                new CharacterTestRuntimeAiLabTimelineProjector().Build(
                    timeline,
                    index,
                    frameCount,
                    0,
                    new CharacterTestRuntimeAiLabTimelineViewport(10, 309, -1, 100000f, true));

            CharacterTestRuntimeAiLabTimelineTrackProjection actionTrack = projection.Tracks[2];
            Assert.Less(actionTrack.Items.Count, frameCount);
            string[] firstAggregationKeys = actionTrack.Items.Select(item => item.StableKey).ToArray();
            int leafCount = 0;
            for (int i = 0; i < actionTrack.Items.Count; i++)
            {
                CharacterTestRuntimeAiLabTimelineItem item = actionTrack.Items[i];
                Assert.AreEqual(CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate, item.Provenance);
                for (int memberIndex = 0; memberIndex < item.Members.Count; memberIndex++)
                {
                    Assert.AreNotEqual(
                        CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate,
                        item.Members[memberIndex].Provenance);
                    leafCount++;
                }
            }
            Assert.AreEqual(frameCount, leafCount);

            CharacterTestRuntimeAiLabTimelineProjection otherZoom =
                new CharacterTestRuntimeAiLabTimelineProjector().Build(
                    timeline,
                    index,
                    frameCount,
                    0,
                    new CharacterTestRuntimeAiLabTimelineViewport(10, 309, 2, 100000f, true));
            string[] otherAggregationKeys = otherZoom.Tracks[2].Items
                .Select(item => item.StableKey)
                .ToArray();
            Assert.AreEqual(firstAggregationKeys.Length, otherAggregationKeys.Length);
            CollectionAssert.AreNotEqual(firstAggregationKeys, otherAggregationKeys);
        }

        [Test]
        public void Projection_DecisionDoesNotBridgeMissingRuntimeFrame()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            CharacterTestRuntimeAiPlannerSnapshot snapshot = Snapshot(Agent());
            timeline.AddRecord(10, 0f, 0d, snapshot, null, null, 10L);
            index.CaptureFrame(10, null, null, null, null);
            timeline.AddRecord(12, 0f, 0d, snapshot, null, null, 12L);
            index.CaptureFrame(12, null, null, null, null);

            CharacterTestRuntimeAiLabTimelineProjection projection = Project(timeline, index, 4, 1);

            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineValidity.MissingRuntimeFrameRange, projection.Validity);
            Assert.AreEqual(2, projection.Tracks[1].Items.Count);
            Assert.AreEqual(10, projection.Tracks[1].Items[0].StartFrame);
            Assert.AreEqual(10, projection.Tracks[1].Items[0].EndFrame);
            Assert.AreEqual(12, projection.Tracks[1].Items[1].StartFrame);
            Assert.AreEqual(12, projection.Tracks[1].Items[1].EndFrame);
        }

        [Test]
        public void Projection_CorruptSlotReturnsTypedFailure()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            timeline.AddRecord(10, 0f, 0d, Snapshot(), null, null, 10L);
            index.CaptureFrame(10, null, new[] { Event(10, 1, 2, "event") }, null, null);
            FieldInfo samplesField = typeof(CharacterTestRuntimeAiLabTimelineEventIndex).GetField(
                "_samples",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var samples = (CharacterTestRuntimeAiLabTimelineEventSample[])samplesField.GetValue(index);
            CharacterTestRuntimeAiLabTimelineEventSample original = samples[0];
            samples[0] = new CharacterTestRuntimeAiLabTimelineEventSample(
                11,
                original.Kind,
                original.EntityId,
                original.RelatedEntityId,
                original.Category,
                original.Severity,
                original.Label,
                original.SourceTraceId,
                original.SourceOrder);

            CharacterTestRuntimeAiLabTimelineProjection projection = Project(timeline, index, 4);

            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineValidity.CorruptEventSlot, projection.Validity);
            AssertEventTracksUnavailableAndEmpty(projection);
        }

        [Test]
        public void Projection_UnknownEventKindReturnsCorruptEventSlot()
        {
            CharacterTestRuntimeAiLabTimelineProjection projection = ProjectSamples(new[]
            {
                new CharacterTestRuntimeAiLabTimelineEventSample(
                    10,
                    (CharacterTestRuntimeAiLabTimelineEventKind)999,
                    1,
                    2,
                    "InvalidKind",
                    CharacterTestRuntimeAiLabTimelineSeverity.Info,
                    "invalid",
                    default,
                    0)
            });

            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineValidity.CorruptEventSlot, projection.Validity);
            AssertEventTracksUnavailableAndEmpty(projection);
        }

        [Test]
        public void Projection_UnknownSeverityReturnsCorruptEventSlot()
        {
            CharacterTestRuntimeAiLabTimelineProjection projection = ProjectSamples(new[]
            {
                new CharacterTestRuntimeAiLabTimelineEventSample(
                    10,
                    CharacterTestRuntimeAiLabTimelineEventKind.Command,
                    1,
                    2,
                    "InvalidSeverity",
                    (CharacterTestRuntimeAiLabTimelineSeverity)999,
                    "invalid",
                    default,
                    0)
            });

            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineValidity.CorruptEventSlot, projection.Validity);
            AssertEventTracksUnavailableAndEmpty(projection);
        }

        [Test]
        public void BinderPoolResetClearsAllDynamicStateAndPreservesStableListeners()
        {
            var parent = new GComponent();
            var visual = new GComponent { name = "marker", visible = true, tooltips = "old" };
            var count = new GTextField { name = "markerCount", text = "7" };
            var label = new GTextField { name = "spanLabel", text = "old label" };
            var tick = new GTextField { name = "tickLabel", text = "900" };
            var selection = new GGraph { name = "selectionOutline", visible = true };
            visual.AddChild(count);
            visual.AddChild(label);
            visual.AddChild(tick);
            visual.AddChild(selection);
            visual.onClick.Add(() => { });
            visual.onTouchBegin.Add(context => { });
            visual.data = new object();
            visual.alpha = 0.25f;
            visual.rotation = 45f;
            visual.SetScale(2f, 3f);
            visual.grayed = true;
            parent.AddChild(visual);

            CharacterTestFairyGuiRuntimeAiLabTimelineVisualPool.ResetVisual(visual);

            Assert.IsFalse(visual.visible);
            Assert.AreEqual(string.Empty, visual.tooltips);
            Assert.AreEqual(string.Empty, count.text);
            Assert.AreEqual(string.Empty, label.text);
            Assert.AreEqual(string.Empty, tick.text);
            Assert.IsFalse(selection.visible);
            Assert.IsFalse(visual.onClick.isEmpty);
            Assert.IsFalse(visual.onTouchBegin.isEmpty);
            Assert.IsNull(visual.data);
            Assert.AreEqual(1f, visual.alpha);
            Assert.AreEqual(0f, visual.rotation);
            Assert.AreEqual(1f, visual.scaleX);
            Assert.AreEqual(1f, visual.scaleY);
            Assert.IsFalse(visual.grayed);
            Assert.IsNull(visual.parent);

            visual.visible = true;
            visual.tooltips = "second use";
            visual.data = new object();
            parent.AddChild(visual);
            CharacterTestFairyGuiRuntimeAiLabTimelineVisualPool.ResetVisual(visual);
            Assert.IsFalse(visual.visible);
            Assert.AreEqual(string.Empty, visual.tooltips);
            Assert.IsNull(visual.data);
            Assert.IsNull(visual.parent);
        }

        private static CharacterTestRuntimeAiLabTimelineProjection ProjectEvents(CharacterTestRuntimeEvent[] events)
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            timeline.AddRecord(10, 0f, 0d, Snapshot(), null, null, 10L);
            index.CaptureFrame(10, null, events, null, null);
            return Project(timeline, index, 4);
        }

        private static string[] LeafKeys(CharacterTestRuntimeAiLabTimelineTrackProjection track)
        {
            return LeafItems(track).Select(item => item.StableKey).ToArray();
        }

        private static CharacterTestRuntimeAiLabTimelineItem[] LeafItems(
            CharacterTestRuntimeAiLabTimelineTrackProjection track)
        {
            var result = new List<CharacterTestRuntimeAiLabTimelineItem>();
            for (int itemIndex = 0; itemIndex < track.Items.Count; itemIndex++)
            {
                CharacterTestRuntimeAiLabTimelineItem item = track.Items[itemIndex];
                if (item.Provenance != CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate)
                {
                    result.Add(item);
                    continue;
                }
                for (int memberIndex = 0; memberIndex < item.Members.Count; memberIndex++)
                    result.Add(item.Members[memberIndex]);
            }
            return result.ToArray();
        }

        private static CharacterTestRuntimeAiLabTimelineProjection ProjectSamples(
            CharacterTestRuntimeAiLabTimelineEventSample[] samples)
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = Identity(1, 10, 4);
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            Begin(timeline, index, identity);
            Capture(timeline, index, 10);
            SetSamples(index, 10, samples);
            return Project(timeline, index, 4);
        }

        private static void SetSamples(
            CharacterTestRuntimeAiLabTimelineEventIndex index,
            int frame,
            CharacterTestRuntimeAiLabTimelineEventSample[] samples)
        {
            FieldInfo samplesField = typeof(CharacterTestRuntimeAiLabTimelineEventIndex).GetField(
                "_samples",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo countsField = typeof(CharacterTestRuntimeAiLabTimelineEventIndex).GetField(
                "_counts",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var storage = (CharacterTestRuntimeAiLabTimelineEventSample[])samplesField.GetValue(index);
            var counts = (int[])countsField.GetValue(index);
            int offset = frame - index.SessionStartFrame;
            Array.Copy(samples, 0, storage, offset * CharacterTestRuntimeAiLabTimelineEventIndex.MaxEventsPerFrame, samples.Length);
            counts[offset] = samples.Length;
        }

        private static CharacterTestRuntimeAiLabTimelineEventSample Sample(
            string category,
            CharacterTestRuntimeAiLabTimelineSeverity severity,
            RuntimeTraceIdentity sourceTraceId)
        {
            return new CharacterTestRuntimeAiLabTimelineEventSample(
                10,
                CharacterTestRuntimeAiLabTimelineEventKind.Command,
                1,
                2,
                category,
                severity,
                "label",
                sourceTraceId,
                0);
        }

        private static CharacterTestRuntimeAiLabTimelineEventSample ActionSample(
            int frame,
            CharacterTestRuntimeAiLabTimelineEventKind kind,
            int sourceOrder)
        {
            return new CharacterTestRuntimeAiLabTimelineEventSample(
                frame,
                kind,
                1,
                0,
                kind == CharacterTestRuntimeAiLabTimelineEventKind.ActionStarted ? "ActionStarted" : "ActionFinished",
                CharacterTestRuntimeAiLabTimelineSeverity.Success,
                "action.test",
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0030),
                sourceOrder,
                instanceId: 77L);
        }

        private static void Begin(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex index,
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity)
        {
            timeline.BeginSession(identity);
            index.Reset(identity);
        }

        private static void Capture(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex index,
            int frame)
        {
            timeline.AddRecord(frame, 0f, 0d, Snapshot(), null, null, frame);
            index.CaptureFrame(frame, null, null, null, null);
        }

        private static void CaptureDecisionTrace(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex index,
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity,
            int frame,
            bool committed,
            CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind runtimeEffectKind =
                CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.Running)
        {
            CharacterTestRuntimeAiLabDecisionTraceWriter writer =
                timeline.BeginDecisionTraceCapture(
                    identity.Generation,
                    frame,
                    out CharacterTestRuntimeAiLabDecisionTraceHandle handle);
            CharacterTestRuntimeAiLabDecisionTraceAgentWriter agent =
                writer.BeginAgent(1);
            agent.AddCandidate(new CharacterTestRuntimeAiLabCandidateEvaluationTrace
            {
                StableId = "behavior-card.retreat",
                Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration,
                State = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected
            });
            agent.SetCommit(new CharacterTestRuntimeAiLabCommitTrace
            {
                PlannerLogicKind = committed
                    ? CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed
                    : CharacterTestRuntimeAiLabPlannerLogicCommitKind.Held,
                CommandEnqueueKind =
                    CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued,
                RuntimeEffectKind = runtimeEffectKind,
                EvidenceFrame = frame,
                PlannerEvidenceFrame = frame,
                CommandEvidenceFrame = frame,
                RuntimeEffectEvidenceFrame = frame,
                CardStableId = "behavior-card.retreat",
                StageStableId = "action",
                MainActionStableId = "legacy:Retreat",
                CommandKind =
                    CharacterTestRuntimeAiLabDecisionTraceCommandKind.SetControlIntent,
                RuntimeEffectActionStableId = "legacy:Retreat"
            });
            writer.Seal();
            timeline.AddRecord(
                frame,
                0f,
                0d,
                Snapshot(Agent()),
                null,
                null,
                frame,
                decisionTraceHandle: handle);
            index.CaptureFrame(frame, null, null, null, null);
        }

        private static CharacterTestRuntimeAiLabTimelineProjection Project(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineEventIndex index,
            int frameLimit,
            int entityId = 0,
            CharacterTestRuntimeAiLabRevisionProjectionInput revisionInput = null)
        {
            return new CharacterTestRuntimeAiLabTimelineProjector().Build(
                timeline,
                index,
                frameLimit,
                entityId,
                new CharacterTestRuntimeAiLabTimelineViewport(
                    timeline.SessionIdentity.SessionStartFrame,
                    timeline.SessionIdentity.SessionStartFrame + frameLimit - 1,
                    -1,
                    1200f,
                    true),
                revisionInput);
        }

        private static CharacterTestRuntimeAiLabTimelineSessionIdentity Identity(int generation, int start, int limit)
        {
            return new CharacterTestRuntimeAiLabTimelineSessionIdentity(generation, start, limit);
        }

        private static CharacterTestRuntimeEvent Event(int frame, int source, int target, string message)
        {
            return new CharacterTestRuntimeEvent(
                new RuntimeFrame(frame),
                CharacterTestRuntimeEventType.CommandAccepted,
                1,
                source,
                target,
                message: message);
        }

        private static CharacterTestRuntimeAiPlannerSnapshot Snapshot(
            params CharacterTestRuntimeAiPlannerAgentSnapshot[] agents)
        {
            return new CharacterTestRuntimeAiPlannerSnapshot(true, "test", agents, string.Empty);
        }

        private static CharacterTestRuntimeAiPlannerAgentSnapshot Agent()
        {
            return new CharacterTestRuntimeAiPlannerAgentSnapshot(
                1,
                2,
                "Pursue",
                true,
                false,
                3f,
                true,
                1f,
                0f,
                10,
                "Approach target",
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                "Move",
                string.Empty,
                string.Empty,
                0,
                0);
        }

        private static void AssertEventTracksUnavailableAndEmpty(CharacterTestRuntimeAiLabTimelineProjection projection)
        {
            for (int track = 2; track <= 4; track++)
            {
                Assert.IsFalse(projection.Tracks[track].Available);
                Assert.AreEqual(0, projection.Tracks[track].Items.Count);
            }
        }

        private static void AssertNoItemOutside(
            CharacterTestRuntimeAiLabTimelineProjection projection,
            int retainedStart,
            int latest)
        {
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                for (int itemIndex = 0; itemIndex < projection.Tracks[trackIndex].Items.Count; itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem item = projection.Tracks[trackIndex].Items[itemIndex];
                    Assert.GreaterOrEqual(item.StartFrame, retainedStart);
                    Assert.LessOrEqual(item.EndFrame, latest);
                }
            }
        }
    }
}
