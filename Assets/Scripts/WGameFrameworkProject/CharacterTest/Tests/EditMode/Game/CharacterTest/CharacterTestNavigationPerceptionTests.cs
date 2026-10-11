using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestNavigationPerceptionTests
    {
        private static readonly float[] CandidateAngles = { 90f, -90f, 45f, -45f };

        [Test]
        public void DirectPath_SkipsAStarAndPublishesGraphWitness()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            CharacterTestNavigationSession session = BuildSession(physics, Input());
            using ActorFixture actors = CreateActors(physics, -4f, 4f);
            actors.Cache.BeginFrame(new RuntimeFrame(10), actors.Module, physics);

            Assert.IsTrue(TryQuery(actors, session, 100, out CharacterTestNavigationSample sample));

            Assert.IsTrue(sample.Result.DirectPathClear);
            Assert.AreEqual(CharacterTestNavigationResolutionStatus.Direct, sample.ResolutionStatus);
            Assert.AreEqual(CharacterTestPathStatus.None, sample.PathStatus);
            Assert.AreEqual(0, sample.PathQueryCount);
            Assert.AreEqual(0, actors.Cache.PathfindingQueryCount);
            Assert.AreEqual(session.Graph.Revision, sample.GraphRevision);
            Assert.AreEqual(session.Graph.ContentHash, sample.GraphContentHash);
            Assert.AreEqual(physics.World.Revision, sample.PhysicsWorldRevision);
        }

        [Test]
        public void StaticObstacle_UsesDeterministicAStarWaypoint()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            AddAabb(physics.World, 101, layer: 1, centerX: 0f, centerZ: 0f, halfX: 0.6f, halfZ: 0.6f);
            CharacterTestNavigationSession session = BuildSession(physics, Input());
            using ActorFixture actors = CreateActors(physics, -4f, 4f);
            actors.Cache.BeginFrame(new RuntimeFrame(11), actors.Module, physics);

            Assert.IsTrue(TryQuery(actors, session, 101, out CharacterTestNavigationSample sample));

            Assert.IsFalse(sample.Result.DirectPathClear);
            Assert.AreEqual(CharacterTestNavigationResolutionStatus.PathWaypoint, sample.ResolutionStatus);
            Assert.AreEqual(CharacterTestPathStatus.Success, sample.PathStatus);
            Assert.AreEqual(1, sample.PathQueryCount);
            Assert.IsTrue(sample.HasNextWaypoint);
            Assert.Greater(sample.RemainingDistanceMillimeters, 0L);
            Assert.Greater(sample.TotalCostMillimeters, 0L);
            Assert.Greater(sample.ExpandedNodeCount, 0);
            Assert.Greater(Math.Abs(sample.Result.Move.y), 0.1f);
            Assert.AreEqual(CharacterTestNavigationReplanReason.DirectPathBlocked, sample.ReplanReason);
            Assert.AreEqual(CharacterTestNavigationBlockedReason.None, sample.BlockedReason);
        }

        [Test]
        public void UnreachableAndBudgetExceeded_RemainDistinctTypedFailures()
        {
            CharacterTestCharacterPhysicsRuntime blockedPhysics = CreatePhysics();
            AddAabb(
                blockedPhysics.World,
                102,
                layer: 1,
                centerX: 0f,
                centerZ: 0f,
                halfX: 0.6f,
                halfZ: 5.5f);
            CharacterTestNavigationSession unreachableSession = BuildSession(blockedPhysics, Input());
            using ActorFixture blockedActors = CreateActors(blockedPhysics, -4f, 4f);
            blockedActors.Cache.BeginFrame(new RuntimeFrame(12), blockedActors.Module, blockedPhysics);
            Assert.IsTrue(TryQuery(
                blockedActors,
                unreachableSession,
                102,
                out CharacterTestNavigationSample unreachable,
                Array.Empty<float>()));
            Assert.AreEqual(CharacterTestPathStatus.Unreachable, unreachable.PathStatus);
            Assert.AreEqual(CharacterTestNavigationBlockedReason.Unreachable, unreachable.BlockedReason);
            Assert.AreEqual(CharacterTestNavigationFallbackReason.Stop, unreachable.FallbackReason);

            CharacterTestCharacterPhysicsRuntime budgetPhysics = CreatePhysics();
            AddAabb(budgetPhysics.World, 103, layer: 1, centerX: 0f, centerZ: 0f, halfX: 0.6f, halfZ: 0.6f);
            CharacterTestNavigationSession budgetSession = BuildSession(
                budgetPhysics,
                Input(maxNodeExpansions: 1));
            using ActorFixture budgetActors = CreateActors(budgetPhysics, -4f, 4f);
            budgetActors.Cache.BeginFrame(new RuntimeFrame(13), budgetActors.Module, budgetPhysics);
            Assert.IsTrue(TryQuery(
                budgetActors,
                budgetSession,
                103,
                out CharacterTestNavigationSample budget,
                Array.Empty<float>()));
            Assert.AreEqual(CharacterTestPathStatus.BudgetExceeded, budget.PathStatus);
            Assert.AreEqual(CharacterTestNavigationBlockedReason.BudgetExceeded, budget.BlockedReason);
            Assert.AreNotEqual(CharacterTestPathStatus.Unreachable, budget.PathStatus);
        }

        [Test]
        public void TargetOutsideGraph_IsTypedAndFallsBackWithoutStaleSuccess()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            AddAabb(physics.World, 104, layer: 1, centerX: -1f, centerZ: 0f, halfX: 0.5f, halfZ: 0.6f);
            CharacterTestNavigationSession session = BuildSession(
                physics,
                Input(maxEndpointSnapDistanceMillimeters: 2000));
            using ActorFixture actors = CreateActors(physics, -4f, 20f);
            actors.Cache.BeginFrame(new RuntimeFrame(14), actors.Module, physics);

            Assert.IsTrue(TryQuery(
                actors,
                session,
                104,
                out CharacterTestNavigationSample sample,
                Array.Empty<float>(),
                probeMaxLength: 30f));

            Assert.AreEqual(CharacterTestPathStatus.TargetOutsideGraph, sample.PathStatus);
            Assert.AreEqual(CharacterTestNavigationBlockedReason.TargetOutsideGraph, sample.BlockedReason);
            Assert.IsFalse(sample.HasNextWaypoint);
            Assert.AreEqual(Vector2.zero, sample.Result.Move);
        }

        [Test]
        public void GraphUnavailable_UsesTypedLocalCandidateFallback()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            AddAabb(physics.World, 105, layer: 1, centerX: 0f, centerZ: 0f, halfX: 0.6f, halfZ: 0.6f);
            using ActorFixture actors = CreateActors(physics, -4f, 4f);
            actors.Cache.BeginFrame(new RuntimeFrame(15), actors.Module, physics);

            Assert.IsTrue(TryQuery(
                actors,
                navigationSession: null,
                navigationProfileKey: 105,
                out CharacterTestNavigationSample sample,
                candidateAngles: new[] { 90f }));

            Assert.AreEqual(CharacterTestPathStatus.GraphUnavailable, sample.PathStatus);
            Assert.AreEqual(CharacterTestNavigationBlockedReason.GraphUnavailable, sample.BlockedReason);
            Assert.AreEqual(CharacterTestNavigationResolutionStatus.LocalCandidate, sample.ResolutionStatus);
            Assert.AreEqual(CharacterTestNavigationFallbackReason.LocalCandidate, sample.FallbackReason);
            Assert.AreEqual(0, sample.PathQueryCount);
        }

        [Test]
        public void CacheKey_UsesGraphBudgetAndPhysicsWitness_AndReadOnlyAccessDoesNotQuery()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            AddAabb(physics.World, 106, layer: 1, centerX: 0f, centerZ: 0f, halfX: 0.6f, halfZ: 0.6f);
            CharacterTestNavigationSession firstSession = BuildSession(physics, Input(revision: 1));
            CharacterTestNavigationSession secondSession = BuildSession(physics, Input(revision: 2));
            CharacterTestNavigationSession budgetSession = BuildSession(
                physics,
                Input(revision: 2, maxNodeExpansions: 1));
            Assert.AreEqual(secondSession.Graph.ContentHash, budgetSession.Graph.ContentHash);
            using ActorFixture actors = CreateActors(physics, -4f, 4f);
            actors.Cache.BeginFrame(new RuntimeFrame(16), actors.Module, physics);

            Assert.IsTrue(TryQuery(actors, firstSession, 106, out CharacterTestNavigationSample first));
            int combatQueries = actors.Cache.NavigationQueryCount;
            int pathQueries = actors.Cache.PathfindingQueryCount;
            Assert.IsTrue(TryQuery(actors, firstSession, 106, out CharacterTestNavigationSample reused));
            Assert.AreEqual(combatQueries, actors.Cache.NavigationQueryCount);
            Assert.AreEqual(pathQueries, actors.Cache.PathfindingQueryCount);
            Assert.AreEqual(first.GraphContentHash, reused.GraphContentHash);

            Assert.IsTrue(TryQuery(actors, firstSession, 107, out CharacterTestNavigationSample profileChanged));
            Assert.Greater(actors.Cache.PathfindingQueryCount, pathQueries);
            Assert.AreEqual(first.PathStatus, profileChanged.PathStatus);
            pathQueries = actors.Cache.PathfindingQueryCount;

            CharacterTestColliderPostureComponent posture = actors.Source.ColliderPosture;
            int physicsRevisionBeforeClearanceChange = physics.World.Revision;
            actors.Source.SetColliderPosture(posture.WithResolved(
                profileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0055,
                posture.HeightMillimeters + 200,
                posture.RadiusMillimeters + 100,
                posture.CenterYMillimeters + 100,
                posture.BlocksCharacters,
                posture.CanBePushed,
                CharacterTestColliderPostureSource.ActionTimeline,
                posture.Priority + 1,
                frame: 16));
            Assert.AreEqual(physicsRevisionBeforeClearanceChange, physics.World.Revision);
            Assert.IsTrue(TryQuery(actors, firstSession, 107, out CharacterTestNavigationSample clearanceChanged));
            Assert.Greater(actors.Cache.PathfindingQueryCount, pathQueries);
            Assert.Greater(clearanceChanged.ClearanceRadiusMillimeters, profileChanged.ClearanceRadiusMillimeters);
            actors.Source.SetColliderPosture(posture);

            Assert.IsTrue(TryQuery(actors, secondSession, 106, out CharacterTestNavigationSample second));
            Assert.Greater(actors.Cache.PathfindingQueryCount, pathQueries);
            Assert.AreEqual(2, second.GraphRevision);
            pathQueries = actors.Cache.PathfindingQueryCount;
            Assert.IsTrue(TryQuery(actors, budgetSession, 106, out CharacterTestNavigationSample budget));
            Assert.Greater(actors.Cache.PathfindingQueryCount, pathQueries);
            Assert.AreEqual(CharacterTestPathStatus.BudgetExceeded, budget.PathStatus);

            int beforeReadOnlyCombat = actors.Cache.NavigationQueryCount;
            int beforeReadOnlyPath = actors.Cache.PathfindingQueryCount;
            Assert.IsTrue(actors.Cache.TryGetCachedNavigation(
                actors.Source.Id.Value,
                actors.Target.Id.Value,
                out CharacterTestNavigationSample cached));
            Assert.IsTrue(actors.Cache.TryGetLatestCachedNavigation(
                actors.Source.Id.Value,
                out CharacterTestNavigationSample latest));
            Assert.AreEqual(budget.PathStatus, cached.PathStatus);
            Assert.AreEqual(cached.GraphContentHash, latest.GraphContentHash);
            Assert.AreEqual(beforeReadOnlyCombat, actors.Cache.NavigationQueryCount);
            Assert.AreEqual(beforeReadOnlyPath, actors.Cache.PathfindingQueryCount);
        }

        [Test]
        public void DynamicSegmentObstacle_RefreshesSameFrameAndRecoversNextFrame()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            AddAabb(physics.World, 107, layer: 1, centerX: 0f, centerZ: 0f, halfX: 0.6f, halfZ: 0.6f);
            CharacterTestNavigationSession session = BuildSession(physics, Input());
            using ActorFixture actors = CreateActors(physics, -4f, 4f);
            actors.Cache.BeginFrame(new RuntimeFrame(17), actors.Module, physics);
            Assert.IsTrue(TryQuery(actors, session, 107, out CharacterTestNavigationSample clearSegment));
            Assert.AreEqual(CharacterTestNavigationResolutionStatus.PathWaypoint, clearSegment.ResolutionStatus);

            float waypointX = ToFloat(clearSegment.NextWaypoint.X);
            float waypointZ = ToFloat(clearSegment.NextWaypoint.Z);
            float midpointX = (actors.Source.Transform.X + waypointX) * 0.5f;
            float midpointZ = (actors.Source.Transform.Z + waypointZ) * 0.5f;
            int beforeWorldRevision = physics.World.Revision;
            AddAabb(
                physics.World,
                900101,
                CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer,
                midpointX,
                midpointZ,
                halfX: 0.35f,
                halfZ: 0.35f);
            Assert.Greater(physics.World.Revision, beforeWorldRevision);
            int beforePathQueries = actors.Cache.PathfindingQueryCount;

            Assert.IsTrue(TryQuery(actors, session, 107, out CharacterTestNavigationSample blockedSegment));
            Assert.Greater(actors.Cache.PathfindingQueryCount, beforePathQueries);
            Assert.AreEqual(CharacterTestNavigationResolutionStatus.LocalCandidate, blockedSegment.ResolutionStatus);
            Assert.AreEqual(CharacterTestNavigationBlockedReason.SegmentBlocked, blockedSegment.BlockedReason);
            Assert.AreEqual(CharacterTestNavigationFallbackReason.LocalCandidate, blockedSegment.FallbackReason);
            Assert.Greater(blockedSegment.SegmentPathHitCount, 0);
            Assert.AreEqual(new CombatEntityId(900101), blockedSegment.SegmentObstacleEntityId);
            Assert.AreEqual(physics.World.Revision, blockedSegment.PhysicsWorldRevision);

            Assert.IsTrue(physics.World.RemoveBody(new CombatBodyId(900101)));
            actors.Cache.BeginFrame(new RuntimeFrame(18), actors.Module, physics);
            Assert.IsTrue(TryQuery(actors, session, 107, out CharacterTestNavigationSample recovered));
            Assert.AreEqual(CharacterTestNavigationResolutionStatus.PathWaypoint, recovered.ResolutionStatus);
            Assert.AreEqual(CharacterTestNavigationBlockedReason.None, recovered.BlockedReason);
        }

        [Test]
        public void BroadphaseInsertionOrder_DoesNotChangeNavigationSample()
        {
            CharacterTestNavigationSample forward = QueryWithStaticObstacleOrder(reverse: false);
            CharacterTestNavigationSample reverse = QueryWithStaticObstacleOrder(reverse: true);

            Assert.AreEqual(forward.PathStatus, reverse.PathStatus);
            Assert.AreEqual(forward.ResolutionStatus, reverse.ResolutionStatus);
            Assert.AreEqual(forward.GraphContentHash, reverse.GraphContentHash);
            Assert.AreEqual(forward.NextWaypoint.X, reverse.NextWaypoint.X);
            Assert.AreEqual(forward.NextWaypoint.Y, reverse.NextWaypoint.Y);
            Assert.AreEqual(forward.NextWaypoint.Z, reverse.NextWaypoint.Z);
            Assert.AreEqual(forward.RemainingDistanceMillimeters, reverse.RemainingDistanceMillimeters);
            Assert.AreEqual(forward.TotalCostMillimeters, reverse.TotalCostMillimeters);
            Assert.AreEqual(forward.ExpandedNodeCount, reverse.ExpandedNodeCount);
            Assert.AreEqual(forward.CombatQueryCount, reverse.CombatQueryCount);
            Assert.AreEqual(forward.DirectPathHitCount, reverse.DirectPathHitCount);
            Assert.AreEqual(forward.NearestObstacleEntityId, reverse.NearestObstacleEntityId);
            Assert.AreEqual(forward.NearestObstacleBodyId, reverse.NearestObstacleBodyId);
        }

        [Test]
        public void WarmedBeginFrameAndForcedPathQueries_DoNotAllocate()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            AddAabb(physics.World, 108, layer: 1, centerX: 0f, centerZ: 0f, halfX: 0.6f, halfZ: 0.6f);
            CharacterTestNavigationSession session = BuildSession(physics, Input());
            using ActorFixture actors = CreateActors(physics, -4f, 4f);

            actors.Cache.BeginFrame(new RuntimeFrame(19), actors.Module, physics);
            for (int i = 0; i < 40; i++)
                Assert.IsTrue(TryQuery(actors, session, 200 + i, out _));

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            actors.Cache.BeginFrame(new RuntimeFrame(20), actors.Module, physics);
            int successCount = 0;
            for (int i = 0; i < 32; i++)
            {
                if (TryQueryWithoutAssert(actors, session, 300 + i, out CharacterTestNavigationSample sample)
                    && sample.PathStatus == CharacterTestPathStatus.Success)
                {
                    successCount++;
                }
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(32, successCount);
            Assert.AreEqual(32, actors.Cache.PathfindingQueryCount);
            Assert.AreEqual(0L, allocated);
        }

        private static bool TryQuery(
            ActorFixture actors,
            CharacterTestNavigationSession navigationSession,
            int navigationProfileKey,
            out CharacterTestNavigationSample sample,
            float[] candidateAngles = null,
            float probeMaxLength = 12f)
        {
            return actors.Cache.TryGetNavigation(
                actors.Source,
                actors.Target,
                attackRange: 2f,
                navigationProfileKey,
                probeMinLength: 0.5f,
                probeMaxLength,
                candidateAngles ?? CandidateAngles,
                navigationSession,
                out sample);
        }

        private static bool TryQueryWithoutAssert(
            ActorFixture actors,
            CharacterTestNavigationSession navigationSession,
            int navigationProfileKey,
            out CharacterTestNavigationSample sample)
        {
            return actors.Cache.TryGetNavigation(
                actors.Source,
                actors.Target,
                attackRange: 2f,
                navigationProfileKey,
                probeMinLength: 0.5f,
                probeMaxLength: 12f,
                CandidateAngles,
                navigationSession,
                out sample);
        }

        private static CharacterTestNavigationSample QueryWithStaticObstacleOrder(bool reverse)
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics();
            if (reverse)
            {
                AddAabb(physics.World, 110, layer: 1, centerX: 0f, centerZ: 0.25f, halfX: 0.6f, halfZ: 0.45f);
                AddAabb(physics.World, 109, layer: 1, centerX: 0f, centerZ: -0.25f, halfX: 0.6f, halfZ: 0.45f);
            }
            else
            {
                AddAabb(physics.World, 109, layer: 1, centerX: 0f, centerZ: -0.25f, halfX: 0.6f, halfZ: 0.45f);
                AddAabb(physics.World, 110, layer: 1, centerX: 0f, centerZ: 0.25f, halfX: 0.6f, halfZ: 0.45f);
            }

            CharacterTestNavigationSession session = BuildSession(physics, Input());
            using ActorFixture actors = CreateActors(physics, -4f, 4f);
            actors.Cache.BeginFrame(new RuntimeFrame(21), actors.Module, physics);
            Assert.IsTrue(TryQuery(actors, session, 109, out CharacterTestNavigationSample sample));
            Assert.AreEqual(CharacterTestPathStatus.Success, sample.PathStatus);
            return sample;
        }

        private static CharacterTestNavigationSession BuildSession(
            CharacterTestCharacterPhysicsRuntime physics,
            CharacterTestNavigationSessionBuildInput input)
        {
            CharacterTestNavigationSession session = CharacterTestNavigationSessionBuilder.Build(
                input,
                physics,
                new CharacterTestNavigationSceneInputResult(
                    CharacterTestNavigationSceneInputStatus.Success));
            Assert.IsTrue(session.IsAvailable, session.BuildResult.Status.ToString());
            return session;
        }

        private static CharacterTestNavigationSessionBuildInput Input(
            int revision = 1,
            int maxEndpointSnapDistanceMillimeters = 4000,
            int maxNodeExpansions = 128)
        {
            return new CharacterTestNavigationSessionBuildInput(
                revision,
                minimumXMillimeters: -6000,
                maximumXMillimeters: 6000,
                minimumZMillimeters: -4000,
                maximumZMillimeters: 4000,
                planeYMillimeters: 0,
                cellSizeMillimeters: 2000,
                connectDiagonals: true,
                maxNodeCount: 128,
                maxEdgeCount: 1024,
                maxEndpointSnapDistanceMillimeters,
                maxNodeExpansions,
                maxPathNodeCount: 64);
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysics()
        {
            return new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.FromRatio(1, 4),
                staticColliderCount: 1,
                sourceDescription: "test.navigation.perception");
        }

        private static void AddAabb(
            CombatPhysicsWorld world,
            int id,
            int layer,
            float centerX,
            float centerZ,
            float halfX,
            float halfZ)
        {
            var bodyId = new CombatBodyId(id);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(id),
                bodyId,
                Position(centerX, 0f, centerZ)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                layer,
                Position(-halfX, 0f, -halfZ),
                Position(halfX, 2f, halfZ),
                CombatSurfaceMaterial.Default));
        }

        private static ActorFixture CreateActors(
            CharacterTestCharacterPhysicsRuntime physics,
            float sourceX,
            float targetX)
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physics,
                createViews: false);
            CharacterTestCharacterDefinition definition = GetDefinition();
            CharacterTestCharacterEntity source = module.World.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    sourceX,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            CharacterTestCharacterEntity target = module.World.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    targetX,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            source.SetMotionState(0f, Vector2.zero, isGrounded: true);
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return new ActorFixture(module, source, target);
        }

        private static CharacterTestCharacterDefinition GetDefinition()
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                out CharacterTestCharacterDefinition definition));
            return definition;
        }

        private static FixVector3 Position(float x, float y, float z)
        {
            return new FixVector3(ToFix64(x), ToFix64(y), ToFix64(z));
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }

        private sealed class ActorFixture : IDisposable
        {
            public ActorFixture(
                CharacterTestCharacterRuntimeModule module,
                CharacterTestCharacterEntity source,
                CharacterTestCharacterEntity target)
            {
                Module = module;
                Source = source;
                Target = target;
            }

            public CharacterTestCharacterRuntimeModule Module { get; }
            public CharacterTestCharacterEntity Source { get; }
            public CharacterTestCharacterEntity Target { get; }
            public CharacterTestPerceptionFrameCache Cache => Module.PerceptionFrameCache;

            public void Dispose()
            {
                Module.Dispose();
            }
        }
    }
}
