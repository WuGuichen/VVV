using System;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestCharacterDistanceM4JointSelectionTests
    {
        [Test]
        public void Selector_LeafPreviewTracePressure_UsesTypedStableTruncation()
        {
            var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
            CharacterTestRuntimeAiJointActorInput[] actors =
            {
                new CharacterTestRuntimeAiJointActorInput(
                    2, 2, CharacterTestCharacterLifeState.Alive, true, 1)
            };
            CharacterTestRuntimeAiJointActionInput[] actions =
            {
                JointAction(1, "attack.basic", 1000)
            };
            CharacterTestRuntimeAiJointZoneInput[] zones =
            {
                JointZone(2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry02),
                JointZone(2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry03)
            };
            CharacterTestRuntimeAiJointGenerationDiagnostics generation = generator.Generate(
                1, 1, actors, 1, actions, 1, zones, 2,
                new CharacterTestRuntimeAiJointGenerationBudget());
            var selector = new CharacterTestRuntimeAiJointSelector();
            var source = new FakeEvaluationSource();

            CharacterTestRuntimeAiJointSelectionResult result = selector.Select(
                generator, source, generation,
                out CharacterTestRuntimeAiJointSelectorDiagnostics diagnostics,
                leafPreviewTraceLimit: 1);

            Assert.IsFalse(result.HasSelection);
            Assert.AreEqual(1, result.EvaluatedCount);
            Assert.AreEqual(CharacterTestRuntimeAiJointTruncationPhase.LeafPreviewTrace,
                diagnostics.TruncationPhase);
            Assert.AreEqual(2, diagnostics.TruncationObservedCount);
            Assert.AreEqual(1, diagnostics.TruncationAcceptedCount);
            Assert.AreEqual(1, diagnostics.TruncationLimit);
        }

        [Test]
        public void Selector_LeafPreviewBudget_ProducesSameSelectionForWarmAndColdCaches()
        {
            var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
            CharacterTestRuntimeAiJointActorInput[] actors =
            {
                new CharacterTestRuntimeAiJointActorInput(
                    2, 2, CharacterTestCharacterLifeState.Alive, true, 1)
            };
            CharacterTestRuntimeAiJointActionInput[] actions =
            {
                JointAction(1, "attack.basic", 1000)
            };
            CharacterTestRuntimeAiJointZoneInput[] zones =
            {
                JointZone(2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry02),
                JointZone(2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry03)
            };
            CharacterTestRuntimeAiJointGenerationDiagnostics generation = generator.Generate(
                1, 1, actors, 1, actions, 1, zones, 2,
                new CharacterTestRuntimeAiJointGenerationBudget());
            var source = new FakeEvaluationSource();
            var warmSelector = new CharacterTestRuntimeAiJointSelector();
            warmSelector.BeginFrame();
            warmSelector.Select(
                generator,
                source,
                generation,
                out _,
                leafPreviewTraceLimit: 2);

            warmSelector.BeginFrame();
            CharacterTestRuntimeAiJointSelectionResult warm = warmSelector.Select(
                generator,
                source,
                generation,
                out CharacterTestRuntimeAiJointSelectorDiagnostics warmDiagnostics,
                leafPreviewTraceLimit: 1);
            var coldSelector = new CharacterTestRuntimeAiJointSelector();
            coldSelector.BeginFrame();
            CharacterTestRuntimeAiJointSelectionResult cold = coldSelector.Select(
                generator,
                source,
                generation,
                out CharacterTestRuntimeAiJointSelectorDiagnostics coldDiagnostics,
                leafPreviewTraceLimit: 1);

            Assert.AreEqual(cold.HasSelection, warm.HasSelection);
            Assert.AreEqual(cold.EvaluatedCount, warm.EvaluatedCount);
            Assert.AreEqual(coldDiagnostics.TruncationPhase, warmDiagnostics.TruncationPhase);
            Assert.AreEqual(coldDiagnostics.TruncationObservedCount,
                warmDiagnostics.TruncationObservedCount);
            Assert.AreEqual(coldDiagnostics.TruncationAcceptedCount,
                warmDiagnostics.TruncationAcceptedCount);
            Assert.AreEqual(1, warmDiagnostics.ResultCacheHitCount);
        }

        [Test]
        public void Reducer_UsesFrozenOpportunityThenIntegerUtilityCostAndStableIdentityOrder()
        {
            CharacterTestRuntimeAiJointSelectionCandidate compatibility = Candidate(
                1, "action.compat", 0, 0, 9999, 0, 0, 0,
                CharacterTestRuntimeAiJointEvaluationStatus.CompatibilityLegacyRange,
                current: false, turn: false, approach: false);
            CharacterTestRuntimeAiJointSelectionCandidate approach = Candidate(
                2, "action.basic", global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry13, 10, 90, 20, 1,
                CharacterTestRuntimeAiJointEvaluationStatus.EvaluatorValid,
                current: false, turn: false, approach: true);
            CharacterTestRuntimeAiJointSelectionCandidate turn = Candidate(
                3, "action.basic", global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04, 0, 1, 0, 20, 1,
                CharacterTestRuntimeAiJointEvaluationStatus.EvaluatorValid,
                current: false, turn: true, approach: false);
            CharacterTestRuntimeAiJointSelectionCandidate current = Candidate(
                4, "action.basic", global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04, 0, 1, 0, 20, 1,
                CharacterTestRuntimeAiJointEvaluationStatus.EvaluatorValid,
                current: true, turn: false, approach: false);

            Assert.IsTrue(CharacterTestRuntimeAiJointReducer.IsBetter(approach, compatibility));
            Assert.IsTrue(CharacterTestRuntimeAiJointReducer.IsBetter(turn, approach));
            Assert.IsTrue(CharacterTestRuntimeAiJointReducer.IsBetter(current, turn));

            AssertBetter(Candidate(8, "z", 1201503, 1201513, 20, 50, 10, 1),
                Candidate(1, "a", 1201501, 1201511, 10, 1, 1, 99));
            AssertBetter(Candidate(8, "z", 1201503, 1201513, 20, 40, 10, 1),
                Candidate(1, "a", 1201501, 1201511, 20, 50, 1, 99));
            AssertBetter(Candidate(8, "z", 1201503, 1201513, 20, 40, 9, 1),
                Candidate(1, "a", 1201501, 1201511, 20, 40, 10, 99));
            AssertBetter(Candidate(8, "z", 1201503, 1201513, 20, 40, 9, 2),
                Candidate(1, "a", 1201501, 1201511, 20, 40, 9, 1));
            AssertBetter(Candidate(1, "z", 1201503, 1201513, 20, 40, 9, 2),
                Candidate(2, "a", 1201501, 1201511, 20, 40, 9, 2));
            AssertBetter(Candidate(1, "a", 1201503, 1201513, 20, 40, 9, 2),
                Candidate(1, "b", 1201501, 1201511, 20, 40, 9, 2));
            AssertBetter(Candidate(1, "a", 1201501, 1201513, 20, 40, 9, 2),
                Candidate(1, "a", 1201502, 1201511, 20, 40, 9, 2));
            AssertBetter(Candidate(1, "a", 1201501, 1201511, 20, 40, 9, 2, plannerActionId: 2),
                Candidate(1, "a", 1201501, 1201512, 20, 40, 9, 2, plannerActionId: 1));
            AssertBetter(Candidate(1, "a", 1201501, 1201511, 20, 40, 9, 2, plannerActionId: 1),
                Candidate(1, "a", 1201501, 1201511, 20, 40, 9, 2, plannerActionId: 2));
        }

        [Test]
        public void Reducer_SelectionIsIndependentOfCandidateOrderAndValidNoOpportunityIsNotSelectable()
        {
            CharacterTestRuntimeAiJointSelectionCandidate invalid = Candidate(
                1, "action.invalid", global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry01, 0, 10000, 0, 0, 0,
                CharacterTestRuntimeAiJointEvaluationStatus.EvaluatorValid,
                false, false, false);
            CharacterTestRuntimeAiJointSelectionCandidate low = Candidate(
                20, "action.z", global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry09, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry15, 100, 50, 20, 1);
            CharacterTestRuntimeAiJointSelectionCandidate high = Candidate(
                10, "action.a", global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry02, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry13, 100, 50, 20, 1);
            CharacterTestRuntimeAiJointSelectionCandidate[] candidates = { invalid, low, high };

            CharacterTestRuntimeAiJointSelectionResult first =
                CharacterTestRuntimeAiJointReducer.Select(candidates, candidates.Length);
            Array.Reverse(candidates);
            CharacterTestRuntimeAiJointSelectionResult second =
                CharacterTestRuntimeAiJointReducer.Select(candidates, candidates.Length);

            Assert.IsTrue(first.HasSelection);
            Assert.AreEqual(2, first.SelectableCount);
            Assert.AreEqual(10, first.Selection.TargetEntityId);
            Assert.AreEqual(first.Selection.TargetEntityId, second.Selection.TargetEntityId);
            Assert.AreEqual(first.Selection.ResolvedActionId, second.Selection.ResolvedActionId);
            Assert.AreEqual(first.Selection.ZoneId, second.Selection.ZoneId);
            Assert.AreEqual(first.Selection.AnchorId, second.Selection.AnchorId);
        }

        [Test]
        public void TwoLevelCache_HitEqualsColdValue_AndEveryRevisionModeZoneAnchorWitnessInvalidates()
        {
            var cache = new CharacterTestRuntimeAiJointSelectionCache(8, 16);
            cache.BeginFrame();
            CharacterTestRuntimeAiJointTracePlanCacheKey trace = TraceKey();
            var planValue = new CharacterTestRuntimeAiJointTracePlanCacheValue(default, 7, true);
            Assert.IsFalse(cache.TracePlans.TryGet(trace, out _));
            Assert.IsTrue(cache.TracePlans.TryStore(trace, planValue));
            Assert.IsTrue(cache.TracePlans.TryGet(trace, out CharacterTestRuntimeAiJointTracePlanCacheValue hit));
            Assert.AreEqual(planValue.SampleCount, hit.SampleCount);
            Assert.AreEqual(planValue.Valid, hit.Valid);

            AssertTraceMiss(cache, TraceKey(physics: 2));
            AssertTraceMiss(cache, TraceKey(pose: 2));
            AssertTraceMiss(cache, TraceKey(facing: 2));
            AssertTraceMiss(cache, TraceKey(binding: 2));
            AssertTraceMiss(cache, TraceKey(animation: 2));
            AssertTraceMiss(cache, TraceKey(rootMotion: 2));
            AssertTraceMiss(cache, TraceKey(policy: 2));
            AssertTraceMiss(cache, TraceKey(target: 3));
            AssertTraceMiss(cache, TraceKey(requiredTurnYaw: 2));

            CharacterTestRuntimeAiJointResultCacheKey resultKey = ResultKey(trace);
            CharacterTestRuntimeAiJointEvaluation cold = Evaluation(path: 120, tick: 8, anchor: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry13);
            Assert.IsFalse(cache.Results.TryGet(resultKey, out _));
            Assert.IsTrue(cache.Results.TryStore(resultKey, cold));
            Assert.IsTrue(cache.Results.TryGet(resultKey, out CharacterTestRuntimeAiJointEvaluation warm));
            AssertEvaluationEqual(cold, warm);

            AssertResultMiss(cache, ResultKey(trace, targetHurtbox: 2));
            AssertResultMiss(cache, ResultKey(trace, zoneBinding: 2));
            AssertResultMiss(cache, ResultKey(trace, mode: CharacterTestAttackEvaluationMode.TurnInPlace));
            AssertResultMiss(cache, ResultKey(trace, zone: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry03));
            AssertResultMiss(cache, ResultKey(trace, anchor: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry14));
            AssertResultMiss(cache, ResultKey(trace, candidateX: 2));
            AssertResultMiss(cache, ResultKey(trace, lockRevision: 2));
            AssertResultMiss(cache, ResultKey(trace, reservationRevision: 2));
            AssertResultMiss(cache, ResultKey(trace, reservationCount: 2));
            AssertResultMiss(cache, ResultKey(trace, graphRevision: 2));
            AssertResultMiss(cache, ResultKey(trace, graphHash: 2));
            AssertResultMiss(cache, ResultKey(trace, classificationFrame: 2));
            AssertResultMiss(cache, ResultKey(trace, classificationRevision: 2));
        }

        [Test]
        public void FrameCache_UsesDeterministicCapacityOverflowAndRollbackClear()
        {
            var cache = new CharacterTestRuntimeAiJointSelectionCache(2, 2);
            cache.BeginFrame();
            CharacterTestRuntimeAiJointTracePlanCacheKey a = TraceKey(source: 1);
            CharacterTestRuntimeAiJointTracePlanCacheKey b = TraceKey(source: 2);
            CharacterTestRuntimeAiJointTracePlanCacheKey c = TraceKey(source: 3);
            var value = new CharacterTestRuntimeAiJointTracePlanCacheValue(default, 1, true);
            Assert.IsTrue(cache.TracePlans.TryStore(a, value));
            Assert.IsTrue(cache.TracePlans.TryStore(b, value));
            Assert.IsFalse(cache.TracePlans.TryStore(c, value));
            Assert.AreEqual(1, cache.TracePlans.OverflowCount);

            cache.BeginFrame();
            Assert.IsTrue(cache.TracePlans.TryStore(c, value));
            Assert.IsTrue(cache.TracePlans.TryGet(c, out _));
            Assert.IsFalse(cache.TracePlans.TryGet(a, out _));
            Assert.IsTrue(cache.TracePlans.TryGet(b, out _));

            cache.Clear();
            Assert.IsFalse(cache.TracePlans.TryGet(b, out _));
            Assert.IsFalse(cache.TracePlans.TryGet(c, out _));
        }

        [Test]
        public void ReducerAndCaches_WarmedPathsAllocateZeroBytes()
        {
            CharacterTestRuntimeAiJointSelectionCandidate[] candidates =
            {
                Candidate(2, "action.a", global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry02, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry13, 100, 50, 10, 1),
                Candidate(3, "action.b", global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry03, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry14, 90, 40, 9, 2)
            };
            var cache = new CharacterTestRuntimeAiJointSelectionCache(4, 4);
            CharacterTestRuntimeAiJointTracePlanCacheKey trace = TraceKey();
            CharacterTestRuntimeAiJointResultCacheKey result = ResultKey(trace);
            var plan = new CharacterTestRuntimeAiJointTracePlanCacheValue(default, 3, true);
            CharacterTestRuntimeAiJointEvaluation evaluation = Evaluation(50, 10, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry13);
            cache.BeginFrame();
            cache.TracePlans.TryStore(trace, plan);
            cache.Results.TryStore(result, evaluation);
            CharacterTestRuntimeAiJointReducer.Select(candidates, candidates.Length);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool allHits = true;
            for (int i = 0; i < 128; i++)
            {
                allHits &= cache.TracePlans.TryGet(trace, out _);
                allHits &= cache.Results.TryGet(result, out _);
                CharacterTestRuntimeAiJointReducer.Select(candidates, candidates.Length);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(allHits);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void Selector_EvaluatesFiniteTargetActionZoneSet_AndCacheHitsPreserveWinner()
        {
            var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
            CharacterTestRuntimeAiJointActorInput[] actors =
            {
                new CharacterTestRuntimeAiJointActorInput(
                    2, 2, CharacterTestCharacterLifeState.Alive, true, 100),
                new CharacterTestRuntimeAiJointActorInput(
                    3, 2, CharacterTestCharacterLifeState.Alive, true, 400)
            };
            CharacterTestRuntimeAiJointActionInput[] actions =
            {
                JointAction(1, "weapon.short", 2000),
                JointAction(2, "weapon.spear", 1000)
            };
            CharacterTestRuntimeAiJointZoneInput[] zones =
            {
                JointZone(2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04),
                JointZone(3, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04)
            };
            CharacterTestRuntimeAiJointGenerationDiagnostics generation = generator.Generate(
                1, 1, actors, 2, actions, 2, zones, 2,
                new CharacterTestRuntimeAiJointGenerationBudget());
            Assert.AreEqual(4, generation.CandidateCount, "cold diagnostics candidate count");
            Assert.AreEqual(4, generator.CandidateCount, "cold generator candidate count");
            var source = new FakeEvaluationSource();
            var selector = new CharacterTestRuntimeAiJointSelector(8, 8);
            selector.BeginFrame();

            CharacterTestRuntimeAiJointSelectionResult cold = selector.Select(
                generator,
                source,
                generation,
                out CharacterTestRuntimeAiJointSelectorDiagnostics coldDiagnostics);
            Assert.IsTrue(cold.HasSelection);
            Assert.AreEqual(3, cold.Selection.TargetEntityId);
            Assert.AreEqual(
                CharacterActionLegacyFixtureIds.From("weapon.spear"),
                cold.Selection.ResolvedActionId);
            Assert.AreEqual(4, coldDiagnostics.EvaluationCount, "cold evaluation count");
            Assert.AreEqual(2, coldDiagnostics.TracePlanBuildCount, "cold trace build count");
            Assert.AreEqual(0, coldDiagnostics.ResultCacheHitCount);

            Array.Reverse(actors);
            Array.Reverse(actions);
            Array.Reverse(zones);
            generation = generator.Generate(
                1, 1, actors, 2, actions, 2, zones, 2,
                new CharacterTestRuntimeAiJointGenerationBudget());
            Assert.AreEqual(4, generation.CandidateCount, "warm diagnostics candidate count");
            Assert.AreEqual(4, generator.CandidateCount, "warm generator candidate count");
            CharacterTestRuntimeAiJointSelectionResult warm = selector.Select(
                generator,
                source,
                generation,
                out CharacterTestRuntimeAiJointSelectorDiagnostics warmDiagnostics);
            Assert.AreEqual(4, warm.EvaluatedCount, "warm evaluated candidate count");
            Assert.AreEqual(2, warm.SelectableCount, "warm selectable candidate count");
            Assert.AreEqual(cold.Selection.TargetEntityId, warm.Selection.TargetEntityId);
            Assert.AreEqual(
                cold.Selection.ResolvedActionId,
                warm.Selection.ResolvedActionId);
            Assert.AreEqual(0, warmDiagnostics.EvaluationCount);
            Assert.AreEqual(0, warmDiagnostics.TracePlanBuildCount);
            Assert.AreEqual(4, warmDiagnostics.ResultCacheHitCount, "warm result cache hit count");
        }

        private static void AssertBetter(
            CharacterTestRuntimeAiJointSelectionCandidate better,
            CharacterTestRuntimeAiJointSelectionCandidate worse)
        {
            Assert.IsTrue(CharacterTestRuntimeAiJointReducer.IsBetter(better, worse));
            Assert.IsFalse(CharacterTestRuntimeAiJointReducer.IsBetter(worse, better));
        }

        private static CharacterTestRuntimeAiJointSelectionCandidate Candidate(
            int target,
            string actionId,
            int zone,
            int anchor,
            int utility,
            int path,
            int tick,
            int priority,
            CharacterTestRuntimeAiJointEvaluationStatus status =
                CharacterTestRuntimeAiJointEvaluationStatus.EvaluatorValid,
            bool current = true,
            bool turn = false,
            bool approach = false,
            int plannerActionId = 1)
        {
            var actor = new CharacterTestRuntimeAiJointActorInput(
                target, 2, CharacterTestCharacterLifeState.Alive, true, target);
            var action = new CharacterTestRuntimeAiJointActionInput(
                plannerActionId,
                CharacterActionLegacyFixtureIds.From(actionId),
                status == CharacterTestRuntimeAiJointEvaluationStatus.CompatibilityLegacyRange
                    ? CharacterTestRuntimeAiJointCandidateSource.CompatibilityLegacyRange
                    : CharacterTestRuntimeAiJointCandidateSource.EvaluatorBacked,
                true,
                true,
                utility,
                new CharacterTestRuntimeAiJointAabb(0, 0, 0, 1, 1, 1));
            var seed = new CharacterTestRuntimeAiJointCandidateSeed(
                actor, action, zone, approach, priority, true);
            var evaluation = new CharacterTestRuntimeAiJointEvaluation(
                status, current, turn, approach, path, tick, anchor);
            return new CharacterTestRuntimeAiJointSelectionCandidate(seed, evaluation);
        }

        private static CharacterTestRuntimeAiJointEvaluation Evaluation(
            int path,
            int tick,
            int anchor)
        {
            return new CharacterTestRuntimeAiJointEvaluation(
                CharacterTestRuntimeAiJointEvaluationStatus.EvaluatorValid,
                true,
                false,
                false,
                path,
                tick,
                anchor);
        }

        private static CharacterTestRuntimeAiJointActionInput JointAction(
            int plannerActionId,
            string stableId,
            int utility)
        {
            return new CharacterTestRuntimeAiJointActionInput(
                plannerActionId,
                CharacterActionLegacyFixtureIds.From(stableId),
                CharacterTestRuntimeAiJointCandidateSource.EvaluatorBacked,
                true,
                true,
                utility,
                new CharacterTestRuntimeAiJointAabb(0, 0, 0, 1000, 1000, 1000));
        }

        private static CharacterTestRuntimeAiJointZoneInput JointZone(int target, int zone)
        {
            return new CharacterTestRuntimeAiJointZoneInput(
                target,
                zone,
                true,
                true,
                1,
                new CharacterTestRuntimeAiJointAabb(0, 0, 0, 100, 100, 100));
        }

        private static CharacterTestRuntimeAiJointTracePlanCacheKey TraceKey(
            int source = 1,
            int physics = 1,
            int pose = 1,
            int facing = 1,
            int binding = 1,
            int animation = 1,
            int rootMotion = 1,
            int policy = 1,
            int target = 2,
            int requiredTurnYaw = 1)
        {
            return new CharacterTestRuntimeAiJointTracePlanCacheKey(
                source,
                new MxFramework.CharacterAction.CharacterActionId(101),
                CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                10,
                20,
                30,
                400,
                50,
                physics,
                pose,
                facing,
                binding,
                animation,
                rootMotion,
                policy,
                target,
                requiredTurnYaw);
        }

        private static CharacterTestRuntimeAiJointResultCacheKey ResultKey(
            CharacterTestRuntimeAiJointTracePlanCacheKey trace,
            int targetHurtbox = 1,
            int zoneBinding = 1,
            CharacterTestAttackEvaluationMode mode = CharacterTestAttackEvaluationMode.CurrentPose,
            int zone = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry02,
            int anchor = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry13,
            int candidateX = 1,
            int lockRevision = 1,
            int reservationRevision = 1,
            int reservationCount = 1,
            int graphRevision = 1,
            long graphHash = 1,
            int classificationFrame = 1,
            int classificationRevision = 1)
        {
            return new CharacterTestRuntimeAiJointResultCacheKey(
                trace,
                2,
                targetHurtbox,
                zoneBinding,
                mode,
                zone,
                anchor,
                candidateX,
                2,
                3,
                4,
                lockRevision,
                reservationRevision,
                reservationCount,
                graphRevision,
                graphHash,
                classificationFrame,
                classificationRevision);
        }

        private static void AssertTraceMiss(
            CharacterTestRuntimeAiJointSelectionCache cache,
            CharacterTestRuntimeAiJointTracePlanCacheKey key)
        {
            Assert.IsFalse(cache.TracePlans.TryGet(key, out _));
        }

        private static void AssertResultMiss(
            CharacterTestRuntimeAiJointSelectionCache cache,
            CharacterTestRuntimeAiJointResultCacheKey key)
        {
            Assert.IsFalse(cache.Results.TryGet(key, out _));
        }

        private static void AssertEvaluationEqual(
            CharacterTestRuntimeAiJointEvaluation expected,
            CharacterTestRuntimeAiJointEvaluation actual)
        {
            Assert.AreEqual(expected.Status, actual.Status);
            Assert.AreEqual(expected.CurrentPose, actual.CurrentPose);
            Assert.AreEqual(expected.TurnInPlace, actual.TurnInPlace);
            Assert.AreEqual(expected.ApproachPose, actual.ApproachPose);
            Assert.AreEqual(expected.PathCostMillimeters, actual.PathCostMillimeters);
            Assert.AreEqual(expected.EarliestHitTick, actual.EarliestHitTick);
            Assert.AreEqual(expected.AnchorId, actual.AnchorId);
        }

        private sealed class FakeEvaluationSource : ICharacterTestRuntimeAiJointEvaluationSource
        {
            public bool TryCreateCacheKeys(
                CharacterTestRuntimeAiJointCandidateSeed seed,
                out CharacterTestRuntimeAiJointTracePlanCacheKey tracePlanKey,
                out CharacterTestRuntimeAiJointResultCacheKey resultKey)
            {
                tracePlanKey = new CharacterTestRuntimeAiJointTracePlanCacheKey(
                    1,
                    seed.Action.ResolvedActionId,
                    CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                    0,
                    0,
                    0,
                    0,
                    1,
                    1,
                    1,
                    1,
                    1,
                    1,
                    1,
                    1);
                resultKey = new CharacterTestRuntimeAiJointResultCacheKey(
                    tracePlanKey,
                    seed.TargetEntityId,
                    1,
                    1,
                    CharacterTestAttackEvaluationMode.CurrentPose,
                    seed.ZoneId,
                    0,
                    0,
                    0,
                    0,
                    0,
                    1,
                    1,
                    0,
                    1,
                    1,
                    1,
                    1);
                return true;
            }

            public bool TryBuildTracePlan(
                CharacterTestRuntimeAiJointCandidateSeed seed,
                out CharacterTestRuntimeAiJointTracePlanCacheValue tracePlan)
            {
                tracePlan = new CharacterTestRuntimeAiJointTracePlanCacheValue(default, 1, true);
                return true;
            }

            public bool TryEvaluate(
                CharacterTestRuntimeAiJointCandidateSeed seed,
                CharacterTestRuntimeAiJointTracePlanCacheValue tracePlan,
                out CharacterTestRuntimeAiJointEvaluation evaluation)
            {
                bool spear = seed.Action.ResolvedActionId
                    == CharacterActionLegacyFixtureIds.From("weapon.spear");
                bool current = spear && seed.TargetEntityId == 3;
                bool approach = spear && seed.TargetEntityId == 2;
                evaluation = new CharacterTestRuntimeAiJointEvaluation(
                    CharacterTestRuntimeAiJointEvaluationStatus.EvaluatorValid,
                    current,
                    false,
                    approach,
                    approach ? 100 : int.MaxValue,
                    current ? 5 : -1,
                    approach ? global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry16 : 0);
                return true;
            }
        }
    }
}
