using System;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestCharacterDistanceM4JointCandidateTests
    {
        [Test]
        public void ProductionDefaults_AreTwiceFrozenRepresentativePeaks()
        {
            CharacterTestRuntimeAiJointGenerationBudget budget =
                new CharacterTestRuntimeAiJointGenerationBudget().Normalize();

            Assert.GreaterOrEqual(budget.ActorScanLimit, 32 * 2);
            Assert.GreaterOrEqual(budget.ActorTopKLimit, 16 * 2);
            Assert.GreaterOrEqual(budget.ReadyActionLimit, 4 * 2);
            Assert.GreaterOrEqual(budget.ActionZoneBroadphaseLimit, 256 * 2);
            Assert.GreaterOrEqual(budget.JointCandidateLimit, 256 * 2);
            Assert.GreaterOrEqual(budget.LeafPreviewTraceLimit, 256 * 2);
            Assert.GreaterOrEqual(budget.CandidatePosePreviewLimit, 256 * 2);
            Assert.GreaterOrEqual(budget.PathQueryLimit, 256 * 2);
            Assert.GreaterOrEqual(
                CharacterTestRuntimeAiJointSelectionCache.DefaultTracePlanCapacity,
                64 * 2);
            Assert.GreaterOrEqual(
                CharacterTestRuntimeAiJointSelectionCache.DefaultResultCapacity,
                256 * 2);
        }

        [TestCase(7)]
        [TestCase(8)]
        public void EvaluationBudget_DefaultPlusOne_ReportsTypedStableTruncationNoAlloc(
            int phaseValue)
        {
            var phase = (CharacterTestRuntimeAiJointTruncationPhase)phaseValue;
            int count = 0;
            int limit = phase == CharacterTestRuntimeAiJointTruncationPhase.CandidatePose
                ? CharacterTestRuntimeAiJointGenerationBudget.DefaultCandidatePosePreviewLimit
                : CharacterTestRuntimeAiJointGenerationBudget.DefaultPathQueryLimit;
            CharacterTestRuntimeAiJointEvaluationBudgetDiagnostics diagnostics = default;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < limit + 1; i++)
            {
                CharacterTestRuntimeAiJointEvaluationBudgetCounter.TryConsume(
                    ref count, limit, phase, 7, out diagnostics);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(limit, count);
            Assert.AreEqual(phase, diagnostics.TruncationPhase);
            Assert.AreEqual(limit + 1, diagnostics.TruncationObservedCount);
            Assert.AreEqual(limit, diagnostics.TruncationAcceptedCount);
            Assert.AreEqual(limit, diagnostics.TruncationLimit);
        }

        [TestCase(65, 64)]
        [TestCase(257, 256)]
        public void Generate_ActorPressure_DefaultAndHardPlusOne_TruncatesStablyNoAlloc(
            int actorCount,
            int requestedLimit)
        {
            var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
            var actors = new CharacterTestRuntimeAiJointActorInput[actorCount];
            for (int i = 0; i < actors.Length; i++)
                actors[i] = Actor(i + 2, 2, CharacterTestCharacterLifeState.Alive, i + 1);
            CharacterTestRuntimeAiJointActionInput[] actions =
            {
                Action(1, "attack.basic", true)
            };
            var zones = new CharacterTestRuntimeAiJointZoneInput[actorCount];
            for (int i = 0; i < zones.Length; i++)
                zones[i] = Zone(i + 2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04, true, true, Box(0, 10));
            var budget = new CharacterTestRuntimeAiJointGenerationBudget(
                actorScanLimit: requestedLimit,
                actorTopKLimit: CharacterTestRuntimeAiJointGenerationBudget.HardActorTopKLimit,
                actionZoneBroadphaseLimit:
                    CharacterTestRuntimeAiJointGenerationBudget.HardActionZoneBroadphaseLimit,
                jointCandidateLimit:
                    CharacterTestRuntimeAiJointGenerationBudget.HardJointCandidateLimit);

            CharacterTestRuntimeAiJointGenerationDiagnostics first = generator.Generate(
                1, 1, actors, actorCount, actions, 1, zones, actorCount, budget);
            Assert.AreEqual(CharacterTestRuntimeAiJointTruncationPhase.ActorScan,
                first.TruncationPhase);
            Assert.AreEqual(actorCount, first.TruncationObservedCount);
            Assert.AreEqual(requestedLimit, first.TruncationLimit);

            generator.Generate(1, 1, actors, actorCount, actions, 1, zones, actorCount, budget);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            CharacterTestRuntimeAiJointGenerationDiagnostics current = default;
            for (int i = 0; i < 128; i++)
            {
                current = generator.Generate(
                    1, 1, actors, actorCount, actions, 1, zones, actorCount, budget);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(first.TruncationPhase, current.TruncationPhase);
            Assert.AreEqual(first.TruncationObservedCount, current.TruncationObservedCount);
            Assert.AreEqual(first.TruncationLimit, current.TruncationLimit);
        }

        [Test]
        public void Generate_FiltersInvalidActorsAndUnreadyActions_AndKeepsCompatibilityExplicit()
        {
            var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
            CharacterTestRuntimeAiJointActorInput[] actors =
            {
                Actor(1, 1, CharacterTestCharacterLifeState.Alive, 1),
                Actor(2, 1, CharacterTestCharacterLifeState.Alive, 1),
                Actor(3, 2, CharacterTestCharacterLifeState.Dead, 1),
                Actor(4, 2, CharacterTestCharacterLifeState.Alive, 1, snapshotCurrent: false),
                Actor(6, 2, CharacterTestCharacterLifeState.Alive, 1, maskCompatible: false),
                Actor(7, 2, CharacterTestCharacterLifeState.Alive, 1, withinSoftSearchRadius: false),
                Actor(5, 2, CharacterTestCharacterLifeState.Alive, 1)
            };
            CharacterTestRuntimeAiJointActionInput[] actions =
            {
                Action(10, "attack.basic", ready: true),
                Action(11, "attack.unready", ready: false),
                Action(
                    12,
                    "ability.compat",
                    ready: true,
                    CharacterTestRuntimeAiJointCandidateSource.CompatibilityLegacyRange)
            };
            CharacterTestRuntimeAiJointZoneInput[] zones =
            {
                Zone(5, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04, hittable: true, approachable: true, Box(0, 10))
            };

            CharacterTestRuntimeAiJointGenerationDiagnostics diagnostics = generator.Generate(
                1,
                1,
                actors,
                actors.Length,
                actions,
                actions.Length,
                zones,
                zones.Length,
                new CharacterTestRuntimeAiJointGenerationBudget());

            Assert.AreEqual(1, diagnostics.ValidActorCount);
            Assert.AreEqual(2, diagnostics.ReadyActionCount);
            Assert.AreEqual(2, diagnostics.CandidateCount);
            Assert.IsFalse(diagnostics.CandidateSetTruncated);
            CharacterTestRuntimeAiJointCandidateSeed first = generator.GetCandidate(0);
            CharacterTestRuntimeAiJointCandidateSeed second = generator.GetCandidate(1);
            CharacterTestRuntimeAiJointCandidateSeed evaluator = first.IsCompatibility ? second : first;
            CharacterTestRuntimeAiJointCandidateSeed compatibility = first.IsCompatibility ? first : second;
            Assert.AreEqual(
                CharacterActionLegacyFixtureIds.From("attack.basic"),
                evaluator.Action.ResolvedActionId);
            Assert.AreEqual(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04, evaluator.ZoneId);
            Assert.IsFalse(evaluator.IsCompatibility);
            Assert.AreEqual(
                CharacterActionLegacyFixtureIds.From("ability.compat"),
                compatibility.Action.ResolvedActionId);
            Assert.AreEqual(0, compatibility.ZoneId);
            Assert.IsTrue(compatibility.IsCompatibility);
            Assert.IsFalse(compatibility.ZoneApproachable);
            Assert.IsFalse(compatibility.ExplicitEnvelopeIntersection);
        }

        [Test]
        public void Generate_ExplicitIntersectionBeatsNearRoot_AndOrderPerturbationIsStable()
        {
            var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
            CharacterTestRuntimeAiJointActorInput near = Actor(10, 2, CharacterTestCharacterLifeState.Alive, 10);
            CharacterTestRuntimeAiJointActorInput far = Actor(20, 2, CharacterTestCharacterLifeState.Alive, 1000);
            CharacterTestRuntimeAiJointActorInput[] actors = { near, far };
            CharacterTestRuntimeAiJointActionInput a =
                Action(2, "action.z", ready: true, bounds: Box(100, 200));
            CharacterTestRuntimeAiJointActionInput b =
                Action(1, "action.a", ready: true, bounds: Box(100, 200));
            CharacterTestRuntimeAiJointActionInput[] actions = { a, b };
            CharacterTestRuntimeAiJointZoneInput zoneNear =
                Zone(10, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry08, hittable: true, approachable: true, Box(0, 10));
            CharacterTestRuntimeAiJointZoneInput zoneFar =
                Zone(20, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry06, hittable: true, approachable: true, Box(120, 130));
            CharacterTestRuntimeAiJointZoneInput[] zones = { zoneNear, zoneFar };

            generator.Generate(
                1,
                1,
                actors,
                actors.Length,
                actions,
                actions.Length,
                zones,
                zones.Length,
                new CharacterTestRuntimeAiJointGenerationBudget(actorTopKLimit: 1));
            Assert.AreEqual(2, generator.CandidateCount);
            string first = Identity(generator.GetCandidate(0));
            string second = Identity(generator.GetCandidate(1));
            Assert.AreEqual(
                "20/" + CharacterActionLegacyFixtureIds.From("action.a") + "/zone.far",
                first);
            Assert.AreEqual(
                "20/" + CharacterActionLegacyFixtureIds.From("action.z") + "/zone.far",
                second);

            Array.Reverse(actors);
            Array.Reverse(actions);
            Array.Reverse(zones);
            generator.Generate(
                1,
                1,
                actors,
                actors.Length,
                actions,
                actions.Length,
                zones,
                zones.Length,
                new CharacterTestRuntimeAiJointGenerationBudget(actorTopKLimit: 1));
            Assert.AreEqual(first, Identity(generator.GetCandidate(0)));
            Assert.AreEqual(second, Identity(generator.GetCandidate(1)));
        }

        [Test]
        public void Generate_BudgetsReportFirstTypedCanonicalTruncation()
        {
            var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
            CharacterTestRuntimeAiJointActorInput[] actors =
            {
                Actor(30, 2, CharacterTestCharacterLifeState.Alive, 30),
                Actor(10, 2, CharacterTestCharacterLifeState.Alive, 10),
                Actor(20, 2, CharacterTestCharacterLifeState.Alive, 20)
            };
            CharacterTestRuntimeAiJointActionInput[] actions =
            {
                Action(1, "attack.basic", ready: true)
            };
            CharacterTestRuntimeAiJointZoneInput[] zones =
            {
                Zone(10, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04, true, true, Box(0, 10)),
                Zone(20, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04, true, true, Box(0, 10)),
                Zone(30, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04, true, true, Box(0, 10))
            };

            CharacterTestRuntimeAiJointGenerationDiagnostics diagnostics = generator.Generate(
                1,
                1,
                actors,
                actors.Length,
                actions,
                actions.Length,
                zones,
                zones.Length,
                new CharacterTestRuntimeAiJointGenerationBudget(
                    actorScanLimit: 2,
                    actorTopKLimit: 2,
                    readyActionLimit: 1,
                    actionZoneBroadphaseLimit: 1,
                    jointCandidateLimit: 1));

            Assert.IsTrue(diagnostics.CandidateSetTruncated);
            Assert.AreEqual(
                CharacterTestRuntimeAiJointTruncationPhase.ActorScan,
                diagnostics.TruncationPhase);
            Assert.AreEqual(3, diagnostics.TruncationObservedCount);
            Assert.AreEqual(2, diagnostics.TruncationAcceptedCount);
            Assert.AreEqual(2, diagnostics.TruncationLimit);
            Assert.AreEqual(1, generator.CandidateCount);
            Assert.AreEqual(10, generator.GetCandidate(0).TargetEntityId);
        }

        [Test]
        public void Generate_CandidateOverflowFailsClosedAndNextRunClearsOldEntries()
        {
            var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
            CharacterTestRuntimeAiJointActorInput[] actors =
            {
                Actor(2, 2, CharacterTestCharacterLifeState.Alive, 1)
            };
            CharacterTestRuntimeAiJointActionInput[] actions =
            {
                Action(1, "attack.a", true),
                Action(2, "attack.b", true)
            };
            CharacterTestRuntimeAiJointZoneInput[] zones =
            {
                Zone(2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04, true, true, Box(0, 10))
            };
            CharacterTestRuntimeAiJointGenerationDiagnostics diagnostics = generator.Generate(
                1,
                1,
                actors,
                1,
                actions,
                2,
                zones,
                1,
                new CharacterTestRuntimeAiJointGenerationBudget(jointCandidateLimit: 1));
            Assert.AreEqual(CharacterTestRuntimeAiJointTruncationPhase.JointCandidate, diagnostics.TruncationPhase);
            Assert.AreEqual(1, generator.CandidateCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => generator.GetCandidate(1));

            generator.Generate(
                1,
                1,
                Array.Empty<CharacterTestRuntimeAiJointActorInput>(),
                0,
                actions,
                2,
                zones,
                1,
                new CharacterTestRuntimeAiJointGenerationBudget());
            Assert.AreEqual(0, generator.CandidateCount);
        }

        [Test]
        public void Generate_WarmedPathAllocatesZeroBytes()
        {
            var generator = new CharacterTestRuntimeAiJointCandidateGenerator();
            CharacterTestRuntimeAiJointActorInput[] actors =
            {
                Actor(2, 2, CharacterTestCharacterLifeState.Alive, 1)
            };
            CharacterTestRuntimeAiJointActionInput[] actions =
            {
                Action(1, "attack.basic", true)
            };
            CharacterTestRuntimeAiJointZoneInput[] zones =
            {
                Zone(2, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Geometry04, true, true, Box(0, 10))
            };
            var budget = new CharacterTestRuntimeAiJointGenerationBudget();
            generator.Generate(1, 1, actors, 1, actions, 1, zones, 1, budget);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
                generator.Generate(1, 1, actors, 1, actions, 1, zones, 1, budget);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
        }

        private static CharacterTestRuntimeAiJointActorInput Actor(
            int id,
            int team,
            CharacterTestCharacterLifeState lifeState,
            long distance,
            bool snapshotCurrent = true,
            bool maskCompatible = true,
            bool withinSoftSearchRadius = true)
        {
            return new CharacterTestRuntimeAiJointActorInput(
                id,
                team,
                lifeState,
                snapshotCurrent,
                distance,
                maskCompatible,
                withinSoftSearchRadius);
        }

        private static CharacterTestRuntimeAiJointActionInput Action(
            int plannerActionId,
            string stableId,
            bool ready,
            CharacterTestRuntimeAiJointCandidateSource source =
                CharacterTestRuntimeAiJointCandidateSource.EvaluatorBacked,
            CharacterTestRuntimeAiJointAabb? bounds = null)
        {
            return new CharacterTestRuntimeAiJointActionInput(
                plannerActionId,
                CharacterActionLegacyFixtureIds.From(stableId),
                source,
                ready,
                resolvable: true,
                actionUtilityMilli: 1000,
                bounds ?? Box(0, 10));
        }

        private static CharacterTestRuntimeAiJointZoneInput Zone(
            int targetEntityId,
            int zoneId,
            bool hittable,
            bool approachable,
            CharacterTestRuntimeAiJointAabb bounds)
        {
            return new CharacterTestRuntimeAiJointZoneInput(
                targetEntityId,
                zoneId,
                hittable,
                approachable,
                authoredPriority: 1,
                bounds);
        }

        private static CharacterTestRuntimeAiJointAabb Box(int min, int max)
        {
            return new CharacterTestRuntimeAiJointAabb(min, min, min, max, max, max);
        }

        private static string Identity(CharacterTestRuntimeAiJointCandidateSeed candidate)
        {
            return candidate.TargetEntityId
                + "/" + candidate.Action.ResolvedActionId
                + "/" + candidate.ZoneId;
        }
    }
}
