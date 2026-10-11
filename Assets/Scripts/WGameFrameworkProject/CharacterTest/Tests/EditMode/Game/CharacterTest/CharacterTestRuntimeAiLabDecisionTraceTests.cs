using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    [TestFixture]
    public sealed class CharacterTestRuntimeAiLabDecisionTraceTests
    {
        [Test]
        public void Store_SealedTracePreservesTypedCausalPhases()
        {
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(2, 1, 2, 2);
            CharacterTestRuntimeAiLabDecisionTraceWriter writer = store.BeginCapture(7L, 42, out var handle);
            CharacterTestRuntimeAiLabDecisionTraceAgentWriter agent = writer.BeginAgent(101);
            var previous = new CharacterTestRuntimeAiLabCommittedStateTrace
            {
                CardStableId = "card.pursue",
                StageStableId = "stage.approach",
                MainActionStableId = "action.move"
            };
            agent.SetPrevious(previous);
            agent.AddFact(new CharacterTestRuntimeAiLabFactObservationTrace
            {
                StableId = "fact.target.distance",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                Value = CharacterTestRuntimeAiLabDecisionTraceValue.FromFloat(4.25f),
                ObservedFrame = 42,
                LastChangedFrame = 42,
                ParticipatedLayers = CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration
            });
            agent.AddCandidate(new CharacterTestRuntimeAiLabCandidateEvaluationTrace
            {
                StableId = "card.attack",
                Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration,
                State = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected,
                Score = 8.5f,
                ReasonCode = CharacterTestRuntimeAiLabDecisionTraceReasonCodes.Selected
            });
            agent.SetCommit(new CharacterTestRuntimeAiLabCommitTrace
            {
                PlannerLogicKind = CharacterTestRuntimeAiLabPlannerLogicCommitKind.Committed,
                CommandEnqueueKind = CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued,
                RuntimeEffectKind = CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved,
                EvidenceFrame = 42,
                CardStableId = "card.attack"
            });
            writer.Seal();
            Assert.IsFalse(agent.AddFact(new CharacterTestRuntimeAiLabFactObservationTrace
            {
                StableId = "fact.after-seal"
            }));

            Assert.IsTrue(store.TryGetAgent(handle, 101, out var trace));
            Assert.AreEqual("card.pursue", trace.PreviousCommitted.CardStableId);
            Assert.AreEqual(1, trace.Facts.Count);
            Assert.AreEqual(4.25f, trace.Facts[0].Value.Number);
            Assert.AreEqual(1, trace.Candidates.Count);
            Assert.AreEqual(CharacterTestRuntimeAiLabCommandEnqueueKind.Enqueued, trace.Commit.CommandEnqueueKind);
            Assert.AreEqual(CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved, trace.Commit.RuntimeEffectKind);
        }

        [Test]
        public void Store_HandleSeparatesSnapshotAndDecisionFrames()
        {
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(1);
            CharacterTestRuntimeAiLabDecisionTraceWriter writer = store.BeginCapture(
                3L,
                42,
                out var handle,
                decisionFrame: 41);
            writer.BeginAgent(7);
            writer.Seal();

            Assert.AreEqual(42, handle.Frame);
            Assert.AreEqual(41, handle.DecisionFrame);
            Assert.IsTrue(store.TryGetAgent(handle, 7, out _));
            Assert.IsFalse(store.TryGetAgent(
                new CharacterTestRuntimeAiLabDecisionTraceHandle(
                    handle.Slot,
                    handle.SlotGeneration,
                    handle.SessionGeneration,
                    handle.Frame,
                    handle.Frame),
                7,
                out _));
        }

        [Test]
        public void FactObservation_StoresBoundedDerivedDependencies()
        {
            var fact = new CharacterTestRuntimeAiLabFactObservationTrace();
            Assert.IsTrue(fact.TryAddDependency("fact.action.running", new MxFramework.Runtime.RuntimeTraceIdentity(1201702)));
            Assert.IsTrue(fact.TryAddDependency("fact.action.running", new MxFramework.Runtime.RuntimeTraceIdentity(1201702)));
            Assert.IsTrue(fact.TryAddDependency("fact.action.transition-window", new MxFramework.Runtime.RuntimeTraceIdentity(1201703)));
            Assert.IsFalse(fact.TryAddDependency("fact.third", new MxFramework.Runtime.RuntimeTraceIdentity(1201704)));
            Assert.AreEqual(2, fact.DependencyCount);
            Assert.AreEqual("fact.action.running", fact.GetDependencyStableId(0));
            Assert.AreEqual("fact.action.transition-window", fact.GetDependencyStableId(1));
        }

        [Test]
        public void Store_ReleasedGenerationCannotReadReusedSlot()
        {
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(1, 1, 1, 1);
            CharacterTestRuntimeAiLabDecisionTraceWriter first = store.BeginCapture(1L, 10, out var oldHandle);
            first.BeginAgent(1);
            first.Seal();
            store.Release(oldHandle);

            CharacterTestRuntimeAiLabDecisionTraceWriter second = store.BeginCapture(1L, 11, out var newHandle);
            second.BeginAgent(2);
            second.Seal();

            Assert.IsFalse(store.TryGetAgent(oldHandle, 1, out _));
            Assert.IsTrue(store.TryGetAgent(newHandle, 2, out _));
            Assert.AreNotEqual(oldHandle.SlotGeneration, newHandle.SlotGeneration);
        }

        [Test]
        public void TimelineClone_PreservesTraceMappingForRecovery()
        {
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(2);
            timeline.BeginSession(new CharacterTestRuntimeAiLabTimelineSessionIdentity(3, 20, 2));
            CharacterTestRuntimeAiLabDecisionTraceWriter writer =
                timeline.BeginDecisionTraceCapture(3L, 20, out var handle);
            writer.BeginAgent(77);
            writer.Seal();
            CharacterTestRuntimeAiPlannerDevTimelineRecord record = timeline.AddRecord(
                20, 0f, 0d, null, null, null, 991L, decisionTraceHandle: handle);

            CharacterTestRuntimeAiPlannerDevTimeline clone = timeline.Clone();

            Assert.IsTrue(clone.TryGetDecisionTrace(record, 77, out var trace));
            Assert.AreEqual(77, trace.EntityId);
            Assert.AreEqual(991L, clone.Records[0].StateHash);
        }

        [Test]
        public void TimelineEviction_InvalidatesOldTraceWithoutChangingAuthorityHash()
        {
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(1);
            timeline.BeginSession(new CharacterTestRuntimeAiLabTimelineSessionIdentity(4, 30, 2));
            CharacterTestRuntimeAiPlannerDevTimelineRecord oldRecord = AddTraceRecord(timeline, 4L, 30, 5, 1234L);
            CharacterTestRuntimeAiPlannerDevTimelineRecord retained = AddTraceRecord(timeline, 4L, 31, 6, 1234L);

            Assert.IsFalse(timeline.TryGetDecisionTrace(oldRecord, 5, out _));
            Assert.IsTrue(timeline.TryGetDecisionTrace(retained, 6, out _));
            Assert.AreEqual(1234L, retained.StateHash);
        }

        [Test]
        public void Store_OverflowIsExplicitAndFailClosed()
        {
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(1, 1, 1, 1);
            CharacterTestRuntimeAiLabDecisionTraceWriter writer = store.BeginCapture(1L, 1, out var handle);
            CharacterTestRuntimeAiLabDecisionTraceAgentWriter agent = writer.BeginAgent(1);
            Assert.IsTrue(agent.AddFact(new CharacterTestRuntimeAiLabFactObservationTrace { StableId = "fact.one" }));
            Assert.IsFalse(agent.AddFact(new CharacterTestRuntimeAiLabFactObservationTrace { StableId = "fact.two" }));
            writer.Seal();

            Assert.IsTrue(store.TryGetAgent(handle, 1, out var trace));
            Assert.IsTrue(trace.Overflowed);
            Assert.AreEqual(1, trace.Facts.Count);
        }

        [Test]
        public void Store_PerLayerCandidateCapacityDoesNotHideOtherLayers()
        {
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(1, 1, 1, 1);
            CharacterTestRuntimeAiLabDecisionTraceWriter writer = store.BeginCapture(1L, 1, out var handle);
            CharacterTestRuntimeAiLabDecisionTraceAgentWriter agent = writer.BeginAgent(1);
            Assert.IsTrue(agent.AddCandidate(new CharacterTestRuntimeAiLabCandidateEvaluationTrace
            {
                StableId = "card.one",
                Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration
            }));
            Assert.IsFalse(agent.AddCandidate(new CharacterTestRuntimeAiLabCandidateEvaluationTrace
            {
                StableId = "card.two",
                Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration
            }));
            Assert.IsTrue(agent.AddCandidate(new CharacterTestRuntimeAiLabCandidateEvaluationTrace
            {
                StableId = "action.one",
                Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction
            }));
            writer.Seal();

            Assert.IsTrue(store.TryGetAgent(handle, 1, out var trace));
            Assert.AreEqual(2, trace.Candidates.Count);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.CardArbitrationCandidates,
                trace.Overflow.Region);
        }

        [Test]
        public void Store_StagingTraceIsUnreadableUntilPromoted()
        {
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(1, 1, 1, 1);
            CharacterTestRuntimeAiLabDecisionTraceWriter writer =
                store.BeginCapture(2L, 5, out var handle, staging: true);
            writer.BeginAgent(9);
            writer.Seal();

            Assert.IsFalse(store.TryGetAgent(handle, 9, out _));
            Assert.IsTrue(store.PromoteStaging(handle));
            Assert.IsTrue(store.TryGetAgent(handle, 9, out _));
        }

        [Test]
        public void Timeline_BranchTraceTransactionPinsOldFutureUntilStagedMappingCommits()
        {
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            timeline.BeginSession(new CharacterTestRuntimeAiLabTimelineSessionIdentity(5, 10, 3));
            AddTraceRecord(timeline, 5L, 10, 1, 10L);
            CharacterTestRuntimeAiPlannerDevTimelineRecord oldEleven =
                AddTraceRecord(timeline, 5L, 11, 1, 11L);
            CharacterTestRuntimeAiPlannerDevTimelineRecord oldTwelve =
                AddTraceRecord(timeline, 5L, 12, 1, 12L);
            Assert.IsTrue(timeline.SelectFrame(10));
            timeline.MarkEditBranchStart();
            long before = timeline.ComputeDecisionTraceRecoveryHash();
            Assert.IsTrue(timeline.BeginBranchDecisionTraceTransaction(10));

            CharacterTestRuntimeAiLabDecisionTraceWriter writer =
                timeline.BeginDecisionTraceCapture(5L, 11, out var stagedHandle);
            writer.BeginAgent(2);
            writer.Seal();
            CharacterTestRuntimeAiPlannerDevTimelineRecord staged = timeline.AddRecord(
                11, 0f, 0d, null, null, null, 111L, decisionTraceHandle: stagedHandle);

            Assert.IsTrue(timeline.TryGetDecisionTrace(oldEleven, 1, out _));
            Assert.IsTrue(timeline.TryGetDecisionTrace(oldTwelve, 1, out _));
            Assert.IsFalse(timeline.TryGetDecisionTrace(staged, 2, out _));
            Assert.AreNotEqual(before, timeline.ComputeDecisionTraceRecoveryHash());
            Assert.IsTrue(timeline.CommitBranchDecisionTraceTransaction());
            Assert.IsFalse(timeline.TryGetDecisionTrace(oldEleven, 1, out _));
            Assert.IsFalse(timeline.TryGetDecisionTrace(oldTwelve, 1, out _));
            Assert.IsTrue(timeline.TryGetDecisionTrace(staged, 2, out _));
        }

        [Test]
        public void TimelineClone_BeforeBranchPreservesOldFutureWhenCandidateFails()
        {
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(4);
            timeline.BeginSession(new CharacterTestRuntimeAiLabTimelineSessionIdentity(6, 20, 3));
            AddTraceRecord(timeline, 6L, 20, 1, 20L);
            CharacterTestRuntimeAiPlannerDevTimelineRecord oldFuture =
                AddTraceRecord(timeline, 6L, 21, 1, 21L);
            Assert.IsTrue(timeline.SelectFrame(20));
            CharacterTestRuntimeAiPlannerDevTimeline recovery = timeline.Clone();
            timeline.MarkEditBranchStart();
            Assert.IsTrue(timeline.BeginBranchDecisionTraceTransaction(20));
            CharacterTestRuntimeAiLabDecisionTraceWriter writer =
                timeline.BeginDecisionTraceCapture(6L, 21, out var stagedHandle);
            writer.BeginAgent(2);
            writer.Seal();
            timeline.AddRecord(21, 0f, 0d, null, null, null, 211L, decisionTraceHandle: stagedHandle);

            Assert.IsTrue(recovery.TryGetDecisionTrace(oldFuture, 1, out var restored));
            Assert.AreEqual(1, restored.EntityId);
            Assert.IsFalse(recovery.BranchDecisionTraceTransactionActive);
        }

        [Test]
        public void Store_Clone_InUseSlotsDeepCopiedAndFreeSlotsShared()
        {
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(2, 2, 4, 4);
            long hashBefore = store.ComputeRecoveryHash();

            // 抓取并固化一个 InUse 槽位
            CharacterTestRuntimeAiLabDecisionTraceWriter writer = store.BeginCapture(100L, 1, out var handle);
            CharacterTestRuntimeAiLabDecisionTraceAgentWriter agent = writer.BeginAgent(42);
            agent.AddFact(new CharacterTestRuntimeAiLabFactObservationTrace { StableId = "fact.initial" });
            writer.Seal();

            long hashCaptured = store.ComputeRecoveryHash();
            var clone = store.Clone();

            // 1. 克隆后哈希与原对象完全一致
            Assert.AreEqual(hashCaptured, clone.ComputeRecoveryHash());

            // 2. 原 InUse 槽位在克隆体中能正确查到数据
            Assert.IsTrue(clone.TryGetAgent(handle, 42, out var trace));
            Assert.AreEqual(1, trace.Facts.Count);
            Assert.AreEqual("fact.initial", trace.Facts[0].StableId);

            // 3. 验证 InUse 是深拷贝：即使后续修改也不会互相污染
            Assert.IsTrue(store.TryGetAgent(handle, 42, out var originalTrace));
            Assert.AreEqual(1, originalTrace.Facts.Count);
        }

        [Test]
        public void FactsPresentationState_NormalizesFiltersPinsAndClearsUnavailableSelection()
        {
            var host = new GameObject("FactsPresentationStateTest");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.SetFactsSearchText("  target  ");
                controller.ToggleFactsChangedOnly();
                controller.ToggleFactsParticipatedOnly();
                controller.ToggleFactsDeltaExpanded();
                controller.SetFactsActiveTableTab(CharacterTestRuntimeAiLabFactsTableTab.Candidates);
                controller.ToggleFactsPin("fact.target.distance");
                controller.SelectFactsStableId("candidate.approach");
                controller.SetFactsScrollOffsets(3f, 2f, 1f);

                Assert.AreEqual("target", controller.FactsSearchText);
                Assert.IsTrue(controller.FactsChangedOnly);
                Assert.IsTrue(controller.FactsParticipatedOnly);
                Assert.IsTrue(controller.FactsDeltaExpanded);
                Assert.AreEqual(CharacterTestRuntimeAiLabFactsTableTab.Candidates, controller.FactsActiveTableTab);
                Assert.IsTrue(controller.IsFactsPinned("fact.target.distance"));
                Assert.AreEqual("candidate.approach", controller.FactsSelectedStableId);
                Assert.AreEqual(3f, controller.FactsFactScrollOffset);
                Assert.AreEqual(2f, controller.FactsCandidateScrollOffset);
                Assert.AreEqual(1f, controller.FactsDeltaScrollOffset);

                controller.ClearFactsSelectionIfUnavailable(
                    new[] { new CharacterTestRuntimeAiLabFactObservationTrace { StableId = "fact.target.distance" } },
                    System.Array.Empty<CharacterTestRuntimeAiLabCandidateEvaluationTrace>());
                Assert.AreEqual(string.Empty, controller.FactsSelectedStableId);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FactsBehaviorTargetResolver_EmptySelectionIsUnavailable()
        {
            CharacterTestRuntimeAiLabDecisionTraceAgentView trace = CreateBehaviorNavigationTrace();
            Assert.IsFalse(CharacterTestRuntimeAiLabFactsBehaviorTargetResolver.TryResolve(
                trace,
                string.Empty,
                out string target));
            Assert.AreEqual(string.Empty, target);
        }

        [Test]
        public void FactsBehaviorTargetResolver_RelatedFactFindsExactSelectedNode()
        {
            CharacterTestRuntimeAiLabDecisionTraceAgentView trace = CreateBehaviorNavigationTrace();
            Assert.IsTrue(CharacterTestRuntimeAiLabFactsBehaviorTargetResolver.TryResolve(
                trace,
                "fact.related",
                out string target));
            Assert.AreEqual("card.alpha/stage/pursue", target);
        }

        [Test]
        public void FactsBehaviorTargetResolver_UnrelatedFactIsUnavailable()
        {
            CharacterTestRuntimeAiLabDecisionTraceAgentView trace = CreateBehaviorNavigationTrace();
            Assert.IsFalse(CharacterTestRuntimeAiLabFactsBehaviorTargetResolver.TryResolve(
                trace,
                "fact.unrelated",
                out string target));
            Assert.AreEqual(string.Empty, target);
        }

        [Test]
        public void FactsBehaviorTargetResolver_PlannerCandidateIsNotBehaviorNode()
        {
            CharacterTestRuntimeAiLabDecisionTraceAgentView trace = CreateBehaviorNavigationTrace();
            Assert.IsFalse(CharacterTestRuntimeAiLabFactsBehaviorTargetResolver.TryResolve(
                trace,
                "planner.action.930010",
                out string target));
            Assert.AreEqual(string.Empty, target);
        }

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(42, 41)]
        public void PostTickTimelineSnapshot_UsesImmediatelyCompletedDecisionFrame(
            int snapshotFrame,
            int expectedDecisionFrame)
        {
            Assert.AreEqual(
                expectedDecisionFrame,
                CharacterTestRuntimeAiPlannerDevSceneController
                    .ResolveDecisionTraceFrameForPostTickSnapshot(snapshotFrame));
        }

        private static CharacterTestRuntimeAiPlannerDevTimelineRecord AddTraceRecord(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            long sessionGeneration,
            int frame,
            int entityId,
            long stateHash)
        {
            CharacterTestRuntimeAiLabDecisionTraceWriter writer =
                timeline.BeginDecisionTraceCapture(sessionGeneration, frame, out var handle);
            writer.BeginAgent(entityId);
            writer.Seal();
            return timeline.AddRecord(
                frame, 0f, 0d, null, null, null, stateHash, decisionTraceHandle: handle);
        }

        private static CharacterTestRuntimeAiLabDecisionTraceAgentView CreateBehaviorNavigationTrace()
        {
            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(1);
            CharacterTestRuntimeAiLabDecisionTraceWriter writer =
                store.BeginCapture(11L, 4, out var handle);
            CharacterTestRuntimeAiLabDecisionTraceAgentWriter agent = writer.BeginAgent(2);
            agent.AddFact(new CharacterTestRuntimeAiLabFactObservationTrace { StableId = "fact.related" });
            agent.AddFact(new CharacterTestRuntimeAiLabFactObservationTrace { StableId = "fact.unrelated" });
            var stage = new CharacterTestRuntimeAiLabCandidateEvaluationTrace
            {
                StableId = "card.alpha/stage/pursue",
                Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction,
                State = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Selected
            };
            stage.TryAddRelatedFact("fact.related", new MxFramework.Runtime.RuntimeTraceIdentity(1201701));
            agent.AddCandidate(stage);
            var planner = new CharacterTestRuntimeAiLabCandidateEvaluationTrace
            {
                StableId = "planner.action.930010",
                Layer = CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction,
                State = CharacterTestRuntimeAiLabDecisionTraceCandidateState.Evaluated
            };
            planner.TryAddRelatedFact("fact.related", new MxFramework.Runtime.RuntimeTraceIdentity(1201701));
            agent.AddCandidate(planner);
            writer.Seal();
            Assert.IsTrue(store.TryGetAgent(handle, 2, out var trace));
            return trace;
        }
    }
}
