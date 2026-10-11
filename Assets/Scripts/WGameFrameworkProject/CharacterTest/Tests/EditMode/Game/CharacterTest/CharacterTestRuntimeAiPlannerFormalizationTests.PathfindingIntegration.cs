using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerFormalizationTests
    {
        [TestCase(CharacterTestRuntimeAiPlannerActionKind.MoveToTarget)]
        [TestCase(CharacterTestRuntimeAiPlannerActionKind.Strafe)]
        [TestCase(CharacterTestRuntimeAiPlannerActionKind.Retreat)]
        public void PathfindingPlanner_MovementActionConsumesDirectDetourAndTypedFailure(
            CharacterTestRuntimeAiPlannerActionKind actionKind)
        {
            using (PlannerNavigationFixture direct = CreatePlannerNavigationFixture(
                       actionKind,
                       PlannerNavigationScenario.Direct))
            {
                CharacterTestRuntimeAiPlannerDecision decision = direct.Evaluate(frame: 10);
                CharacterTestNavigationSample sample = direct.GetCachedSample();
                Assert.AreEqual(CharacterTestNavigationResolutionStatus.Direct, sample.ResolutionStatus);
                AssertMovementRelativeToNavigation(actionKind, decision.Move, sample.Result.Move);
                Assert.IsTrue(decision.MovePathAvailable);
                Assert.IsTrue(decision.ShouldSubmitControlIntent);
            }

            using (PlannerNavigationFixture detour = CreatePlannerNavigationFixture(
                       actionKind,
                       PlannerNavigationScenario.Detour))
            {
                CharacterTestRuntimeAiPlannerDecision decision = detour.Evaluate(frame: 11);
                CharacterTestNavigationSample sample = detour.GetCachedSample();
                Assert.AreEqual(CharacterTestNavigationResolutionStatus.PathWaypoint, sample.ResolutionStatus);
                Assert.AreEqual(CharacterTestPathStatus.Success, sample.PathStatus);
                Assert.IsTrue(sample.HasNextWaypoint);
                AssertMovementRelativeToNavigation(actionKind, decision.Move, sample.Result.Move);
                Assert.IsTrue(decision.MovePathAvailable);
                Assert.IsFalse(decision.MoveDirectPathClear);
                Assert.AreEqual(8f, decision.TargetDistance, 0.001f);
                Assert.IsFalse(decision.TargetInAttackRange);
                Assert.IsTrue(decision.ShouldSubmitControlIntent);
            }

            using (PlannerNavigationFixture failure = CreatePlannerNavigationFixture(
                       actionKind,
                       PlannerNavigationScenario.Unreachable))
            {
                CharacterTestRuntimeAiPlannerDecision decision = failure.Evaluate(frame: 12);
                CharacterTestNavigationSample sample = failure.GetCachedSample();
                Assert.AreEqual(CharacterTestPathStatus.Unreachable, sample.PathStatus);
                Assert.AreEqual(CharacterTestNavigationBlockedReason.Unreachable, sample.BlockedReason);
                Assert.AreEqual(CharacterTestNavigationFallbackReason.Stop, sample.FallbackReason);
                Assert.AreEqual(Vector2.zero, decision.Move);
                Assert.IsFalse(decision.MovePathAvailable);
                Assert.IsFalse(decision.MoveDirectPathClear);
                Assert.IsTrue(decision.ShouldSubmitControlIntent);
            }
        }

        [Test]
        public void PathfindingPlanner_FactsAndDiagnosticsRefreshWithoutStaleNavigation()
        {
            using PlannerNavigationFixture fixture = CreatePlannerNavigationFixture(
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                PlannerNavigationScenario.Detour);

            CharacterTestRuntimeAiPlannerDecision detourDecision = fixture.Evaluate(frame: 20);
            CharacterTestNavigationSample detourSample = fixture.GetCachedSample();
            Assert.AreEqual(CharacterTestNavigationResolutionStatus.PathWaypoint, detourSample.ResolutionStatus);
            Assert.IsTrue(detourDecision.MovePathAvailable);
            Assert.IsTrue(fixture.Evaluator.TryGetFactState(fixture.Source.Id.Value, out var detourFacts));
            Assert.IsTrue(detourFacts.MovePathAvailable);
            Assert.IsFalse(detourFacts.DirectPathClear);

            Assert.IsTrue(fixture.Physics.World.RemoveBody(new CombatBodyId(PlannerObstacleBodyId)));
            CharacterTestRuntimeAiPlannerDecision directDecision = fixture.Evaluate(frame: 21);
            CharacterTestNavigationSample directSample = fixture.GetCachedSample();
            Assert.AreEqual(CharacterTestNavigationResolutionStatus.Direct, directSample.ResolutionStatus);
            Assert.IsTrue(directDecision.MovePathAvailable);
            Assert.IsTrue(directDecision.MoveDirectPathClear);
            Assert.AreEqual(-1, directDecision.MoveCandidateIndex);
            Assert.IsTrue(fixture.Evaluator.TryGetFactState(fixture.Source.Id.Value, out var directFacts));
            Assert.IsTrue(directFacts.DirectPathClear);
            Assert.AreEqual(-1, directFacts.NavigationCandidateIndex);

            Assert.IsTrue(fixture.Module.World.Remove(fixture.Target.Id));
            CharacterTestRuntimeAiPlannerDecision noTargetDecision = fixture.Evaluate(frame: 22);
            Assert.IsFalse(noTargetDecision.TargetExists);
            Assert.IsFalse(noTargetDecision.MovePathAvailable);
            Assert.IsTrue(fixture.Evaluator.TryGetFactState(fixture.Source.Id.Value, out var noTargetFacts));
            Assert.IsFalse(noTargetFacts.TargetExists);
            Assert.IsFalse(noTargetFacts.MovePathAvailable);
            Assert.IsFalse(noTargetFacts.DirectPathClear);
            Assert.AreEqual(-1, noTargetFacts.NavigationCandidateIndex);
            CharacterTestRuntimeAiPlannerAgentSnapshot agent =
                fixture.Evaluator.CreateSnapshot(string.Empty).Agents[0];
            Assert.IsFalse(agent.NavigationDiagnostics.HasSample);
        }

        [Test]
        public void PathfindingPlanner_BudgetExceededRemainsDistinctFromUnreachable()
        {
            using PlannerNavigationFixture fixture = CreatePlannerNavigationFixture(
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                PlannerNavigationScenario.BudgetExceeded);

            CharacterTestRuntimeAiPlannerDecision decision = fixture.Evaluate(frame: 23);
            CharacterTestNavigationSample sample = fixture.GetCachedSample();

            Assert.AreEqual(CharacterTestPathStatus.BudgetExceeded, sample.PathStatus);
            Assert.AreEqual(CharacterTestNavigationBlockedReason.BudgetExceeded, sample.BlockedReason);
            Assert.AreNotEqual(CharacterTestPathStatus.Unreachable, sample.PathStatus);
            Assert.IsFalse(decision.MovePathAvailable);
        }

        [Test]
        public void PathfindingPlanner_DynamicSegmentObstacleFallsBackAndRecovers()
        {
            using PlannerNavigationFixture fixture = CreatePlannerNavigationFixture(
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                PlannerNavigationScenario.Detour);

            CharacterTestRuntimeAiPlannerDecision clearDecision = fixture.Evaluate(frame: 24);
            CharacterTestNavigationSample clear = fixture.GetCachedSample();
            Assert.AreEqual(CharacterTestNavigationResolutionStatus.PathWaypoint, clear.ResolutionStatus);
            Assert.IsTrue(clearDecision.MovePathAvailable);

            float waypointX = PlannerNavigationToFloat(clear.NextWaypoint.X);
            float waypointZ = PlannerNavigationToFloat(clear.NextWaypoint.Z);
            AddPlannerNavigationAabb(
                fixture.Physics.World,
                PlannerDynamicObstacleBodyId,
                centerX: (fixture.Source.Transform.X + waypointX) * 0.5f,
                centerZ: (fixture.Source.Transform.Z + waypointZ) * 0.5f,
                halfX: 0.35f,
                halfZ: 0.35f,
                layer: CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer);

            CharacterTestRuntimeAiPlannerDecision blockedDecision = fixture.Evaluate(frame: 24);
            CharacterTestNavigationSample blocked = fixture.GetCachedSample();
            Assert.AreEqual(CharacterTestNavigationResolutionStatus.LocalCandidate, blocked.ResolutionStatus);
            Assert.AreEqual(CharacterTestNavigationBlockedReason.SegmentBlocked, blocked.BlockedReason);
            Assert.AreEqual(CharacterTestNavigationFallbackReason.LocalCandidate, blocked.FallbackReason);
            Assert.AreEqual(new CombatEntityId(PlannerDynamicObstacleBodyId), blocked.SegmentObstacleEntityId);
            Assert.IsTrue(blockedDecision.MovePathAvailable);

            Assert.IsTrue(fixture.Physics.World.RemoveBody(new CombatBodyId(PlannerDynamicObstacleBodyId)));
            CharacterTestRuntimeAiPlannerDecision recoveredDecision = fixture.Evaluate(frame: 25);
            CharacterTestNavigationSample recovered = fixture.GetCachedSample();
            Assert.AreEqual(CharacterTestNavigationResolutionStatus.PathWaypoint, recovered.ResolutionStatus);
            Assert.AreEqual(CharacterTestNavigationBlockedReason.None, recovered.BlockedReason);
            Assert.IsTrue(recoveredDecision.MovePathAvailable);
        }

        [Test]
        public void PathfindingPlanner_RollbackDropsDerivedSampleAndRecomputesSameWitness()
        {
            using PlannerNavigationFixture fixture = CreatePlannerNavigationFixture(
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                PlannerNavigationScenario.Detour);

            fixture.Evaluate(frame: 26);
            CharacterTestNavigationSample before = fixture.GetCachedSample();
            CharacterTestRuntimeAiPlannerSlotStateSnapshot authority =
                fixture.Evaluator.CreateStateSnapshot(slot: 0);

            Assert.IsTrue(
                fixture.Evaluator.TryApplyStateSnapshot(authority, out string failure),
                failure);
            CharacterTestRuntimeAiPlannerAgentSnapshot restored =
                fixture.Evaluator.CreateSnapshot("restored-before-resense").Agents[0];
            Assert.IsFalse(restored.NavigationDiagnostics.HasSample);

            fixture.Evaluate(frame: 27);
            CharacterTestNavigationSample recomputed = fixture.GetCachedSample();
            Assert.AreEqual(before.ResolutionStatus, recomputed.ResolutionStatus);
            Assert.AreEqual(before.PathStatus, recomputed.PathStatus);
            Assert.AreEqual(before.GraphRevision, recomputed.GraphRevision);
            Assert.AreEqual(before.GraphContentHash, recomputed.GraphContentHash);
            Assert.AreEqual(before.NextWaypoint, recomputed.NextWaypoint);
            Assert.AreEqual(before.TotalCostMillimeters, recomputed.TotalCostMillimeters);
        }

        [Test]
        public void PathfindingPlanner_OverlayReadsCachedIdentityWithoutQueries()
        {
            using PlannerNavigationFixture fixture = CreatePlannerNavigationFixture(
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                PlannerNavigationScenario.Detour);
            fixture.Evaluate(frame: 30);
            CharacterTestNavigationSample cached = fixture.GetCachedSample();
            int combatQueries = fixture.Cache.NavigationQueryCount;
            int pathQueries = fixture.Cache.PathfindingQueryCount;

            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                fixture.Evaluator.CreateSnapshot("pathfinding-overlay");
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = snapshot.Agents[0];
            string runtimeAiLabText =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.BuildNavigationPathText(agent);
            string factsPrimary =
                CharacterTestRuntimeAiPlannerNavigationDiagnosticText.BuildFactsCardPrimary(agent);
            string factsDetail =
                CharacterTestRuntimeAiPlannerNavigationDiagnosticText.BuildFactsCardDetail(agent);
            CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel playMonitor =
                CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel.Create("pathfinding", agent);

            Assert.IsTrue(agent.NavigationDiagnostics.HasSample);
            CharacterTestNavigationSample snapshotSample = agent.NavigationDiagnostics.Sample;
            Assert.AreEqual(cached.Frame, snapshotSample.Frame);
            Assert.AreEqual(cached.GraphRevision, snapshotSample.GraphRevision);
            Assert.AreEqual(cached.GraphContentHash, snapshotSample.GraphContentHash);
            Assert.AreEqual(cached.PathStatus, snapshotSample.PathStatus);
            Assert.AreEqual(cached.NextWaypoint, snapshotSample.NextWaypoint);
            StringAssert.Contains(cached.GraphContentHash.ToString("X16"), runtimeAiLabText);
            StringAssert.Contains("PathWaypoint", runtimeAiLabText);
            StringAssert.Contains("expanded=", runtimeAiLabText);
            StringAssert.Contains("segment=", runtimeAiLabText);
            StringAssert.Contains(cached.GraphContentHash.ToString("X16"), factsPrimary);
            StringAssert.Contains("PathWaypoint", factsPrimary);
            StringAssert.Contains("WP ", factsDetail);
            StringAssert.Contains(" · C ", factsDetail);
            StringAssert.Contains(" · X ", factsDetail);
            StringAssert.Contains(" · Q ", factsDetail);
            StringAssert.Contains(" · S ", factsDetail);
            StringAssert.Contains(" · R ", factsDetail);
            StringAssert.Contains(" · F ", factsDetail);
            StringAssert.Contains(cached.GraphContentHash.ToString("X16"), playMonitor.PathText);
            StringAssert.Contains("图身份", playMonitor.MovementDetailText);
            StringAssert.Contains("重规划/阻断/fallback", playMonitor.MovementDetailText);
            Assert.AreEqual(combatQueries, fixture.Cache.NavigationQueryCount);
            Assert.AreEqual(pathQueries, fixture.Cache.PathfindingQueryCount);
        }

        [Test]
        public void PathfindingPlanner_WarmedPlannerTickWithPathSampleDoesNotAllocate()
        {
            using PlannerNavigationFixture fixture = CreatePlannerNavigationFixture(
                CharacterTestRuntimeAiPlannerActionKind.MoveToTarget,
                PlannerNavigationScenario.Detour);
            for (int frame = 40; frame < 80; frame++)
                fixture.Evaluate(frame);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool evaluated = true;
            for (int frame = 80; frame < 112; frame++)
                evaluated &= fixture.TryEvaluate(frame, out _);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(evaluated);
            Assert.AreEqual(1, fixture.Cache.PathfindingQueryCount);
            Assert.AreEqual(0L, allocated);
        }

        private const int PlannerObstacleBodyId = 790001;
        private const int PlannerDynamicObstacleBodyId = 790002;

        private enum PlannerNavigationScenario
        {
            Direct,
            Detour,
            Unreachable,
            BudgetExceeded
        }

        private static PlannerNavigationFixture CreatePlannerNavigationFixture(
            CharacterTestRuntimeAiPlannerActionKind actionKind,
            PlannerNavigationScenario scenario)
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePlannerNavigationPhysics();
            if (scenario == PlannerNavigationScenario.Detour
                || scenario == PlannerNavigationScenario.BudgetExceeded)
            {
                AddPlannerNavigationAabb(
                    physics.World,
                    PlannerObstacleBodyId,
                    centerX: 0f,
                    centerZ: 0f,
                    halfX: 0.6f,
                    halfZ: 0.6f);
            }
            else if (scenario == PlannerNavigationScenario.Unreachable)
            {
                AddPlannerNavigationAabb(
                    physics.World,
                    PlannerObstacleBodyId,
                    centerX: 0f,
                    centerZ: 0f,
                    halfX: 0.6f,
                    halfZ: 5.5f);
            }

            int maxNodeExpansions = scenario == PlannerNavigationScenario.BudgetExceeded ? 1 : 128;
            CharacterTestNavigationSession session = CharacterTestNavigationSessionBuilder.Build(
                new CharacterTestNavigationSessionBuildInput(
                    revision: 301,
                    minimumXMillimeters: -6000,
                    maximumXMillimeters: 6000,
                    minimumZMillimeters: -4000,
                    maximumZMillimeters: 4000,
                    planeYMillimeters: 0,
                    cellSizeMillimeters: 2000,
                    connectDiagonals: true,
                    maxNodeCount: 128,
                    maxEdgeCount: 1024,
                    maxEndpointSnapDistanceMillimeters: 4000,
                    maxNodeExpansions,
                    maxPathNodeCount: 64),
                physics,
                new CharacterTestNavigationSceneInputResult(
                    CharacterTestNavigationSceneInputStatus.Success));
            Assert.IsTrue(session.IsAvailable, session.BuildResult.Status.ToString());

            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physics,
                createViews: false);
            CharacterTestCharacterDefinition definition = GetPlannerNavigationDefinition();
            CharacterTestCharacterEntity source = module.World.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: -4f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            CharacterTestCharacterEntity target = module.World.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 4f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            source.SetMotionState(0f, Vector2.zero, isGrounded: true);
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);

            CharacterTestRuntimeAiPlannerProfile profile = CreateSingleMovementActionProfile(
                actionKind,
                new CharacterTestRuntimeAiPlannerMovementSettings(
                    speedScale: 1f,
                    steeringResponsiveness: 1f,
                    approachLateralBlend: 0f,
                    arrivalSlowdownDistance: 0f,
                    directionHoldFrames: 0,
                    reactionDelayFrames: 0));
            CharacterTestRuntimeAiPlannerCompiledProfile.TryCompile(
                profile,
                out CharacterTestRuntimeAiPlannerCompiledProfile compiled,
                out CharacterTestRuntimeAiPlannerValidationReport report);
            Assert.NotNull(compiled, report.ToText());

            float[] candidateAngles = scenario == PlannerNavigationScenario.Unreachable
                || scenario == PlannerNavigationScenario.BudgetExceeded
                    ? new[] { 0f }
                    : CharacterTestAiBehaviorProfileSource.CopyDefaultNavigationCandidateAngles();
            var navigationProfile = new CharacterTestAiBehaviorProfile(
                attackRange: 0.5f,
                navigationProbeMinLength: 0.5f,
                navigationProbeMaxLength: 12f,
                navigationCandidateAngles: candidateAngles);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
            var navigationService = new CharacterTestCombatPhysicsNavigationService(
                physics,
                navigationProfile,
                cache,
                session);
            var evaluator = new CharacterTestRuntimeAiPlannerEvaluator(
                compiled,
                navigationService,
                new FakeRuntimeAiPlannerPerceptionSource(),
                cache);
            return new PlannerNavigationFixture(
                module,
                physics,
                source,
                target,
                evaluator);
        }

        private static void AssertMovementRelativeToNavigation(
            CharacterTestRuntimeAiPlannerActionKind actionKind,
            Vector2 movement,
            Vector2 navigationMove)
        {
            Assert.Greater(movement.sqrMagnitude, 0.01f);
            Vector2 expectedForward = navigationMove.normalized;
            float dot = Vector2.Dot(movement.normalized, expectedForward);
            switch (actionKind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                    Assert.Greater(dot, 0.99f);
                    break;
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    Assert.Less(Mathf.Abs(dot), 0.01f);
                    break;
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    Assert.Less(dot, -0.99f);
                    break;
                default:
                    Assert.Fail("Unexpected movement action: " + actionKind);
                    break;
            }
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePlannerNavigationPhysics()
        {
            return new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.FromRatio(1, 4),
                staticColliderCount: 1,
                sourceDescription: "test.runtime-ai-planner.pathfinding");
        }

        private static CharacterTestCharacterDefinition GetPlannerNavigationDefinition()
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                out CharacterTestCharacterDefinition definition));
            return definition;
        }

        private static void AddPlannerNavigationAabb(
            CombatPhysicsWorld world,
            int id,
            float centerX,
            float centerZ,
            float halfX,
            float halfZ,
            int layer = 1)
        {
            var bodyId = new CombatBodyId(id);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(id),
                bodyId,
                PlannerNavigationPosition(centerX, 0f, centerZ)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                layer,
                PlannerNavigationPosition(-halfX, 0f, -halfZ),
                PlannerNavigationPosition(halfX, 2f, halfZ),
                CombatSurfaceMaterial.Default));
        }

        private static FixVector3 PlannerNavigationPosition(float x, float y, float z)
        {
            return new FixVector3(
                PlannerNavigationFix64(x),
                PlannerNavigationFix64(y),
                PlannerNavigationFix64(z));
        }

        private static Fix64 PlannerNavigationFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static float PlannerNavigationToFloat(Fix64 value)
        {
            return (float)(value.RawValue / (double)Fix64.Scale);
        }

        private sealed class PlannerNavigationFixture : IDisposable
        {
            public PlannerNavigationFixture(
                CharacterTestCharacterRuntimeModule module,
                CharacterTestCharacterPhysicsRuntime physics,
                CharacterTestCharacterEntity source,
                CharacterTestCharacterEntity target,
                CharacterTestRuntimeAiPlannerEvaluator evaluator)
            {
                Module = module;
                Physics = physics;
                Source = source;
                Target = target;
                Evaluator = evaluator;
            }

            public CharacterTestCharacterRuntimeModule Module { get; }
            public CharacterTestCharacterPhysicsRuntime Physics { get; }
            public CharacterTestCharacterEntity Source { get; }
            public CharacterTestCharacterEntity Target { get; }
            public CharacterTestRuntimeAiPlannerEvaluator Evaluator { get; }
            public CharacterTestPerceptionFrameCache Cache => Module.PerceptionFrameCache;

            public CharacterTestRuntimeAiPlannerDecision Evaluate(int frame)
            {
                Assert.IsTrue(TryEvaluate(frame, out CharacterTestRuntimeAiPlannerDecision decision));
                return decision;
            }

            public bool TryEvaluate(
                int frame,
                out CharacterTestRuntimeAiPlannerDecision decision)
            {
                Cache.BeginFrame(new RuntimeFrame(frame), Module, Physics);
                return Evaluator.TryEvaluate(
                    Source,
                    Module.Entities,
                    new RuntimeFrame(frame),
                    out decision);
            }

            public CharacterTestNavigationSample GetCachedSample()
            {
                Assert.IsTrue(Cache.TryGetCachedNavigation(
                    Source.Id.Value,
                    Target.Id.Value,
                    out CharacterTestNavigationSample sample));
                return sample;
            }

            public void Dispose()
            {
                Module.Dispose();
            }
        }
    }
}
