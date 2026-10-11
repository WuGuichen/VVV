using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestSlopeFormalSceneTests
    {
        private const long ExpectedDefaultBaselineGraphHash = -205402090243034927L;
        private const int ExpectedDefaultBaselineNodeCount = 1173;
        private const int ExpectedDefaultBaselineEdgeCount = 8544;
        private const long ExpectedProductionBaselineGraphHash = 3631157898817211323L;
        private const int ExpectedProductionBaselineNodeCount = 1145;
        private const int ExpectedProductionBaselineEdgeCount = 8334;
        private const long ExpectedProductionFormalGraphHash = 4091462993826317581L;
        private const int ExpectedProductionFormalNodeCount = 1125;
        private const int ExpectedProductionFormalEdgeCount = 8146;
        private const int DefaultClearanceRadiusMillimeters = 450;
        private const int DefaultClearanceHeightMillimeters = 1800;
        private const int ProductionClearanceRadiusMillimeters = 1300;
        private const int ProductionClearanceHeightMillimeters = 4200;
        private const int MovableSlopeGuardMillimeters = 2000;

        private static readonly int[] FormalSlopeBodyIds = { 10080, 10081, 10082 };

        private static readonly string[] FormalSlopeStableIds =
        {
            "character-test.slope.15deg",
            "character-test.slope.30deg",
            "character-test.slope.50deg"
        };

        private static readonly int[] FormalMovableNumericIds =
        {
            101, 102, 103, 104, 105, 110, 120, 121
        };

        private static readonly CharacterTestMovableNaturalObjectShapeKind[] FormalMovableShapes =
        {
            CharacterTestMovableNaturalObjectShapeKind.Aabb,
            CharacterTestMovableNaturalObjectShapeKind.Aabb,
            CharacterTestMovableNaturalObjectShapeKind.Aabb,
            CharacterTestMovableNaturalObjectShapeKind.Aabb,
            CharacterTestMovableNaturalObjectShapeKind.Aabb,
            CharacterTestMovableNaturalObjectShapeKind.Sphere,
            CharacterTestMovableNaturalObjectShapeKind.Capsule,
            CharacterTestMovableNaturalObjectShapeKind.Aabb
        };

        [Test]
        public void FormalNavigation_DefaultClearanceFreezesSlopeDeltaAndTwoLegacyRoutes()
        {
            using var scope = new OfficialSceneScope();
            GameManager manager = scope.Manager;
            CompileFormalNavigationInput(
                manager,
                out CharacterTestNavigationSessionBuildInput input,
                out CharacterTestNavigationSceneInputResult inputResult);
            CharacterTestCharacterPhysicsRuntime formal = BuildFormalRuntime(manager, input, inputResult);
            CharacterTestCharacterPhysicsRuntime baseline = CloneStaticRuntime(
                formal,
                excludeFormalSlopes: true,
                reverseRegistration: false,
                "default-clearance-baseline");

            CharacterTestNavigationSession baselineSession = CharacterTestNavigationSessionBuilder.Build(
                input,
                baseline,
                inputResult);
            CharacterTestNavigationSession formalSession = CharacterTestNavigationSessionBuilder.Build(
                input,
                formal,
                inputResult);

            AssertNavigationBuildSucceeded(baselineSession, "default baseline");
            AssertNavigationBuildSucceeded(formalSession, "default formal");
            Assert.AreEqual(ExpectedDefaultBaselineGraphHash, baselineSession.Graph.ContentHash);
            Assert.AreEqual(ExpectedDefaultBaselineNodeCount, baselineSession.Graph.NodeCount);
            Assert.AreEqual(ExpectedDefaultBaselineEdgeCount, baselineSession.Graph.EdgeCount);
            Assert.AreEqual(ExpectedGraphHash, formalSession.Graph.ContentHash);
            Assert.AreEqual(1159, formalSession.Graph.NodeCount);
            Assert.AreEqual(8406, formalSession.Graph.EdgeCount);
            Assert.AreEqual(-14, formalSession.Graph.NodeCount - baselineSession.Graph.NodeCount);
            Assert.AreEqual(-138, formalSession.Graph.EdgeCount - baselineSession.Graph.EdgeCount);

            RouteSnapshot baselinePlayerToAi = CaptureRoute(
                baselineSession,
                R(0, 0, 0),
                R(-250000, 0, 4000000),
                DefaultClearanceRadiusMillimeters,
                DefaultClearanceHeightMillimeters);
            RouteSnapshot formalPlayerToAi = CaptureRoute(
                formalSession,
                R(0, 0, 0),
                R(-250000, 0, 4000000),
                DefaultClearanceRadiusMillimeters,
                DefaultClearanceHeightMillimeters);
            AssertSuccessfulRoute(
                baselinePlayerToAi,
                new[] { 644, 683, 722 },
                new[] { 5146, 5458 },
                "default baseline Player->AI");
            AssertRouteSnapshotsEqual(
                baselinePlayerToAi,
                formalPlayerToAi,
                "default Player->AI");

            RouteSnapshot baselinePlayerToAiOne = CaptureRoute(
                baselineSession,
                R(0, 0, 0),
                R(-8736000, 0, 4647000),
                DefaultClearanceRadiusMillimeters,
                DefaultClearanceHeightMillimeters);
            RouteSnapshot formalPlayerToAiOne = CaptureRoute(
                formalSession,
                R(0, 0, 0),
                R(-8736000, 0, 4647000),
                DefaultClearanceRadiusMillimeters,
                DefaultClearanceHeightMillimeters);
            AssertSuccessfulRoute(
                baselinePlayerToAiOne,
                new[] { 644, 643, 642, 680, 718 },
                new[] { 5147, 5139, 5134, 5438 },
                "default baseline Player->AI_1");
            AssertRouteSnapshotsEqual(
                baselinePlayerToAiOne,
                formalPlayerToAiOne,
                "default Player->AI_1");
        }

        [Test]
        public void FormalNavigation_ProductionMaximumClearanceFreezesGraphAndCausalRouteBoundary()
        {
            using var scope = new OfficialSceneScope();
            GameManager manager = scope.Manager;
            CompileFormalNavigationInput(
                manager,
                out CharacterTestNavigationSessionBuildInput input,
                out CharacterTestNavigationSceneInputResult inputResult);
            CharacterTestCharacterPhysicsRuntime formal = BuildFormalRuntime(manager, input, inputResult);
            CharacterTestCharacterPhysicsRuntime baseline = CloneStaticRuntime(
                formal,
                excludeFormalSlopes: true,
                reverseRegistration: false,
                "production-clearance-baseline");
            CharacterTestCharacterPhysicsRuntime compositionRuntime = CloneStaticRuntime(
                formal,
                excludeFormalSlopes: false,
                reverseRegistration: false,
                "production-clearance-composition");

            FixVector3 maximumAgentHalfExtents;
            CharacterTestNavigationSession composedSession;
            using (var slice = new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                PhysicsRuntime = compositionRuntime,
                NavigationBuildInput = input,
                NavigationInputResult = inputResult,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                EnableCharacterSimulation = false,
                SuppressMissingStartupSpawnWarning = true
            }))
            {
                maximumAgentHalfExtents =
                    slice.CharacterRuntime.ResolveMaximumNavigationAgentHalfExtents();
                composedSession = slice.Services.Modules.NavigationSession;
                AssertNavigationBuildSucceeded(composedSession, "production composition");
            }

            Assert.AreEqual(
                new FixVector3(
                    Fix64.FromRaw(1300000),
                    Fix64.FromRaw(2100000),
                    Fix64.FromRaw(1300000)),
                maximumAgentHalfExtents);

            CharacterTestNavigationSession baselineSession = CharacterTestNavigationSessionBuilder.Build(
                input,
                baseline,
                inputResult,
                maximumAgentHalfExtents);
            CharacterTestNavigationSession formalSession = CharacterTestNavigationSessionBuilder.Build(
                input,
                formal,
                inputResult,
                maximumAgentHalfExtents);
            AssertNavigationBuildSucceeded(baselineSession, "production baseline");
            AssertNavigationBuildSucceeded(formalSession, "production formal");
            AssertGraphsEqual(composedSession.Graph, formalSession.Graph, "production composition");

            RouteSnapshot baselinePlayerToAi = CaptureRoute(
                baselineSession,
                R(0, 0, 0),
                R(-250000, 0, 4000000),
                ProductionClearanceRadiusMillimeters,
                ProductionClearanceHeightMillimeters);
            RouteSnapshot formalPlayerToAi = CaptureRoute(
                formalSession,
                R(0, 0, 0),
                R(-250000, 0, 4000000),
                ProductionClearanceRadiusMillimeters,
                ProductionClearanceHeightMillimeters);
            RouteSnapshot baselinePlayerToAiOne = CaptureRoute(
                baselineSession,
                R(0, 0, 0),
                R(-8736000, 0, 4647000),
                ProductionClearanceRadiusMillimeters,
                ProductionClearanceHeightMillimeters);
            RouteSnapshot formalPlayerToAiOne = CaptureRoute(
                formalSession,
                R(0, 0, 0),
                R(-8736000, 0, 4647000),
                ProductionClearanceRadiusMillimeters,
                ProductionClearanceHeightMillimeters);

            AssertNoSlopeCausalRouteRegression(
                baselinePlayerToAi,
                formalPlayerToAi,
                "production Player->AI");
            AssertNoSlopeCausalRouteRegression(
                baselinePlayerToAiOne,
                formalPlayerToAiOne,
                "production Player->AI_1");
            Assert.AreEqual(ExpectedProductionBaselineGraphHash, baselineSession.Graph.ContentHash);
            Assert.AreEqual(ExpectedProductionBaselineNodeCount, baselineSession.Graph.NodeCount);
            Assert.AreEqual(ExpectedProductionBaselineEdgeCount, baselineSession.Graph.EdgeCount);
            Assert.AreEqual(ExpectedProductionFormalGraphHash, formalSession.Graph.ContentHash);
            Assert.AreEqual(ExpectedProductionFormalNodeCount, formalSession.Graph.NodeCount);
            Assert.AreEqual(ExpectedProductionFormalEdgeCount, formalSession.Graph.EdgeCount);
            Assert.AreEqual(-20, formalSession.Graph.NodeCount - baselineSession.Graph.NodeCount);
            Assert.AreEqual(-188, formalSession.Graph.EdgeCount - baselineSession.Graph.EdgeCount);
            Assert.AreEqual(
                ProductionClearanceRadiusMillimeters,
                formalSession.Graph.GetNode(0).ClearanceRadiusMillimeters);
            Assert.AreEqual(
                ProductionClearanceHeightMillimeters,
                formalSession.Graph.GetNode(0).ClearanceHeightMillimeters);

            AssertSuccessfulRoute(
                baselinePlayerToAi,
                new[] { 644, 683, 722 },
                new[] { 5146, 5458 },
                "production baseline Player->AI");
            AssertRouteSnapshotsEqual(
                baselinePlayerToAi,
                formalPlayerToAi,
                "production Player->AI");
            AssertSuccessfulRoute(
                baselinePlayerToAiOne,
                new[] { 644, 643, 642, 680, 718 },
                new[] { 5147, 5139, 5134, 5438 },
                "production baseline Player->AI_1");
            AssertRouteSnapshotsEqual(
                baselinePlayerToAiOne,
                formalPlayerToAiOne,
                "production Player->AI_1");
        }

        [Test]
        public void FormalNavigation_ReversedStaticBodiesAndFourShapeRegistrationsAreStableAtBothClearances()
        {
            using var scope = new OfficialSceneScope();
            GameManager manager = scope.Manager;
            CompileFormalNavigationInput(
                manager,
                out CharacterTestNavigationSessionBuildInput input,
                out CharacterTestNavigationSceneInputResult inputResult);
            CharacterTestCharacterPhysicsRuntime source = BuildFormalRuntime(manager, input, inputResult);
            AssertFormalRuntimeContainsFourStaticShapes(source);
            CharacterTestCharacterPhysicsRuntime ordered = CloneStaticRuntime(
                source,
                excludeFormalSlopes: false,
                reverseRegistration: false,
                "ordered-static-registration");
            CharacterTestCharacterPhysicsRuntime reversed = CloneStaticRuntime(
                source,
                excludeFormalSlopes: false,
                reverseRegistration: true,
                "reversed-static-registration");

            AssertRegistrationOrderStable(
                input,
                inputResult,
                ordered,
                reversed,
                supportedAgentHalfExtents: null,
                DefaultClearanceRadiusMillimeters,
                DefaultClearanceHeightMillimeters,
                "default clearance");
            AssertRegistrationOrderStable(
                input,
                inputResult,
                ordered,
                reversed,
                new FixVector3(
                    Fix64.FromRaw(1300000),
                    Fix64.FromRaw(2100000),
                    Fix64.FromRaw(1300000)),
                ProductionClearanceRadiusMillimeters,
                ProductionClearanceHeightMillimeters,
                "production clearance");
        }

        [Test]
        public void FormalMovables_RemainDynamicTypedAndGuardedFromStaticObbSlopes()
        {
            using var scope = new OfficialSceneScope();
            GameManager manager = scope.Manager;
            CharacterTestMovableNaturalObjectSpawnSpec[] specs =
                manager.WorldObjectSceneCache.CollectMovableSpawnSpecs(out string[] diagnostics);
            Assert.AreEqual(0, diagnostics.Length, string.Join("\n", diagnostics));
            Assert.AreEqual(8, specs.Length);
            Assert.AreEqual(8, manager.WorldObjectSceneCache.MovableInstanceCount);

            CharacterTestCharacterPhysicsRuntime runtime =
                CharacterTestCombatPhysicsSceneBridge.BuildRuntime(
                    manager.WorldObjectSceneRoot,
                    manager.WorldObjectSceneCache,
                    manager.PhysicsSettings);
            Assert.IsNotNull(runtime);
            var world = new CharacterTestCharacterRuntimeWorld();
            world.SetMovableNaturalObjectSpawnSpecs(specs);
            Assert.AreEqual(8, world.MovableNaturalObjects.Count);
            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                world.MovableNaturalObjects,
                runtime);
            Assert.AreEqual(8, runtime.SyncedMovableObjectColliderCount);
            Assert.AreEqual(47, runtime.World.TotalColliderCount);

            for (int i = 0; i < specs.Length; i++)
            {
                CharacterTestMovableNaturalObjectSpawnSpec spec = specs[i];
                Assert.AreEqual(FormalMovableNumericIds[i], spec.NumericId, "movable numeric " + i);
                Assert.AreEqual(FormalMovableShapes[i], spec.ShapeKind, "movable shape " + spec.StableId);
                Assert.IsFalse(IsFormalSlopeStableId(spec.StableId), spec.StableId);
                Assert.IsFalse(IsFormalSlopeBody(spec.NumericId), spec.StableId);
                AssertMovableDynamicCollider(runtime, spec);
                AssertMovableSlopeGuard(runtime, spec);
            }

            for (int i = 0; i < FormalSlopeBodyIds.Length; i++)
            {
                Assert.IsTrue(runtime.World.TryGetObbCollider(
                    new CombatBodyId(FormalSlopeBodyIds[i]),
                    new CombatColliderId(1),
                    out _), "formal slope OBB " + FormalSlopeBodyIds[i]);
            }

            var scratch = new CharacterTestMovableNaturalObjectContactScratch();
            scratch.BuildStaticObstacles(runtime);
            Assert.AreEqual(34, scratch.StaticObstacleCount);
            for (int i = 0; i < scratch.StaticObstacles.Count; i++)
            {
                CharacterTestMovableNaturalObjectStaticContactObstacle obstacle =
                    scratch.StaticObstacles[i];
                Assert.IsTrue(
                    runtime.StaticObstacleLayerMask.ContainsLayer(obstacle.Collider.Layer));
                Assert.IsFalse(IsFormalSlopeBody(obstacle.Collider.BodyId.Value));
                Assert.Less(
                    obstacle.Collider.BodyId.Value,
                    CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectBodyIdStart,
                    "Movable static-obstacle cache must exclude dynamic movable bodies.");
                Assert.AreNotEqual(
                    CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer,
                    obstacle.Collider.Layer,
                    "Movable static-obstacle cache must stay static AABB-only.");
            }
        }

        private static void CompileFormalNavigationInput(
            GameManager manager,
            out CharacterTestNavigationSessionBuildInput input,
            out CharacterTestNavigationSceneInputResult inputResult)
        {
            Assert.IsTrue(CharacterTestNavigationSceneInputCompiler.TryCompile(
                manager.NavigationSettings,
                manager.WorldObjectSceneCache,
                out input,
                out inputResult), inputResult.Status.ToString());
            Assert.AreEqual(CharacterTestNavigationSceneInputStatus.Success, inputResult.Status);
        }

        private static CharacterTestCharacterPhysicsRuntime BuildFormalRuntime(
            GameManager manager,
            CharacterTestNavigationSessionBuildInput input,
            CharacterTestNavigationSceneInputResult inputResult)
        {
            CharacterTestCharacterPhysicsRuntime runtime =
                CharacterTestCombatPhysicsSceneBridge.BuildRuntime(
                    manager.WorldObjectSceneRoot,
                    manager.WorldObjectSceneCache,
                    manager.PhysicsSettings);
            Assert.IsNotNull(runtime);
            runtime.SetNavigationBuildInput(input, inputResult);
            return runtime;
        }

        private static CharacterTestCharacterPhysicsRuntime CloneStaticRuntime(
            CharacterTestCharacterPhysicsRuntime source,
            bool excludeFormalSlopes,
            bool reverseRegistration,
            string sourceSuffix)
        {
            var sourceBodies = new List<CombatPhysicsBody>();
            var sourceAabbs = new List<CombatPhysicsAabbCollider>();
            var sourceSpheres = new List<CombatPhysicsSphereCollider>();
            var sourceCapsules = new List<CombatPhysicsCapsuleCollider>();
            var sourceObbs = new List<CombatPhysicsObbCollider>();
            source.World.CopyBodiesTo(sourceBodies);
            source.World.CopyAabbCollidersTo(sourceAabbs);
            source.World.CopySphereCollidersTo(sourceSpheres);
            source.World.CopyCapsuleCollidersTo(sourceCapsules);
            source.World.CopyObbCollidersTo(sourceObbs);
            if (reverseRegistration)
            {
                sourceBodies.Reverse();
                sourceAabbs.Reverse();
                sourceSpheres.Reverse();
                sourceCapsules.Reverse();
                sourceObbs.Reverse();
            }

            var world = new CombatPhysicsWorld();
            for (int i = 0; i < sourceBodies.Count; i++)
            {
                CombatPhysicsBody body = sourceBodies[i];
                if (excludeFormalSlopes && IsFormalSlopeBody(body.BodyId.Value))
                {
                    continue;
                }
                world.UpsertBody(body);
            }
            for (int i = 0; i < sourceAabbs.Count; i++)
            {
                CombatPhysicsAabbCollider collider = sourceAabbs[i];
                if (!excludeFormalSlopes || !IsFormalSlopeBody(collider.BodyId.Value))
                    world.UpsertAabbCollider(collider);
            }
            for (int i = 0; i < sourceSpheres.Count; i++)
            {
                CombatPhysicsSphereCollider collider = sourceSpheres[i];
                if (!excludeFormalSlopes || !IsFormalSlopeBody(collider.BodyId.Value))
                    world.UpsertSphereCollider(collider);
            }
            for (int i = 0; i < sourceCapsules.Count; i++)
            {
                CombatPhysicsCapsuleCollider collider = sourceCapsules[i];
                if (!excludeFormalSlopes || !IsFormalSlopeBody(collider.BodyId.Value))
                    world.UpsertCapsuleCollider(collider);
            }
            for (int i = 0; i < sourceObbs.Count; i++)
            {
                CombatPhysicsObbCollider collider = sourceObbs[i];
                if (!excludeFormalSlopes || !IsFormalSlopeBody(collider.BodyId.Value))
                    world.UpsertObbCollider(collider);
            }

            var clone = new CharacterTestCharacterPhysicsRuntime(
                world,
                source.ObstacleLayerMask,
                source.CharacterHalfExtents,
                source.SkinWidth,
                source.MaxStepHeight,
                world.TotalColliderCount,
                source.SourceDescription + ";" + sourceSuffix,
                source.GroundSnapDistance);
            clone.SetNavigationBuildInput(source.NavigationBuildInput, source.NavigationInputResult);
            return clone;
        }

        private static void AssertNavigationBuildSucceeded(
            CharacterTestNavigationSession session,
            string context)
        {
            Assert.IsNotNull(session, context);
            Assert.IsTrue(session.IsAvailable, context + " " + session.BuildResult.Status);
            Assert.AreEqual(CharacterTestNavigationSessionBuildStatus.Success, session.BuildResult.Status);
            Assert.AreEqual(CharacterTestNavigationSceneInputStatus.Success, session.SceneInputResult.Status);
        }

        private static RouteSnapshot CaptureRoute(
            CharacterTestNavigationSession session,
            FixVector3 start,
            FixVector3 target,
            int clearanceRadiusMillimeters,
            int clearanceHeightMillimeters)
        {
            var query = new CharacterTestPathQuery(
                session.Graph.Revision,
                session.Graph.ContentHash,
                start,
                target,
                clearanceRadiusMillimeters,
                clearanceHeightMillimeters,
                session.Input.MaxEndpointSnapDistanceMillimeters,
                session.Input.MaxNodeExpansions,
                session.Input.MaxPathNodeCount);
            session.Pathfinder.TryFindPath(query, session.PathBuffer, out CharacterTestPathfindingSample sample);
            var nodes = new int[session.PathBuffer.NodeCount];
            var edges = new int[session.PathBuffer.EdgeCount];
            for (int i = 0; i < nodes.Length; i++)
                nodes[i] = session.PathBuffer.GetNodeId(i);
            for (int i = 0; i < edges.Length; i++)
                edges[i] = session.PathBuffer.GetEdgeId(i);
            return new RouteSnapshot(sample, nodes, edges);
        }

        private static void AssertSuccessfulRoute(
            RouteSnapshot actual,
            int[] expectedNodes,
            int[] expectedEdges,
            string context)
        {
            Assert.AreEqual(CharacterTestPathStatus.Success, actual.Sample.Status, context);
            CollectionAssert.AreEqual(expectedNodes, actual.NodeIds, context + " nodes");
            CollectionAssert.AreEqual(expectedEdges, actual.EdgeIds, context + " edges");
        }

        private static void AssertRouteSnapshotsEqual(
            RouteSnapshot expected,
            RouteSnapshot actual,
            string context)
        {
            Assert.AreEqual(expected.Sample.Status, actual.Sample.Status, context + " status");
            Assert.AreEqual(expected.Sample.SourceNodeId, actual.Sample.SourceNodeId, context + " source");
            Assert.AreEqual(expected.Sample.TargetNodeId, actual.Sample.TargetNodeId, context + " target");
            Assert.AreEqual(expected.Sample.SourceSnapDistanceMillimeters,
                actual.Sample.SourceSnapDistanceMillimeters, context + " source snap");
            Assert.AreEqual(expected.Sample.TargetSnapDistanceMillimeters,
                actual.Sample.TargetSnapDistanceMillimeters, context + " target snap");
            Assert.AreEqual(expected.Sample.TotalCostMillimeters,
                actual.Sample.TotalCostMillimeters, context + " cost");
            CollectionAssert.AreEqual(expected.NodeIds, actual.NodeIds, context + " nodes");
            CollectionAssert.AreEqual(expected.EdgeIds, actual.EdgeIds, context + " edges");
        }

        private static void AssertNoSlopeCausalRouteRegression(
            RouteSnapshot baseline,
            RouteSnapshot formal,
            string context)
        {
            if (baseline.Sample.Success && !formal.Sample.Success)
            {
                Assert.Fail(
                    "M4-T5 requires T2 layout rollback: baseline route succeeds but formal slope route fails. "
                    + context
                    + " baseline=" + baseline.Sample.Status
                    + " formal=" + formal.Sample.Status);
            }
        }

        private static void AssertRegistrationOrderStable(
            CharacterTestNavigationSessionBuildInput input,
            CharacterTestNavigationSceneInputResult inputResult,
            CharacterTestCharacterPhysicsRuntime ordered,
            CharacterTestCharacterPhysicsRuntime reversed,
            FixVector3? supportedAgentHalfExtents,
            int clearanceRadiusMillimeters,
            int clearanceHeightMillimeters,
            string context)
        {
            CharacterTestNavigationSession orderedSession = CharacterTestNavigationSessionBuilder.Build(
                input, ordered, inputResult, supportedAgentHalfExtents);
            CharacterTestNavigationSession reversedSession = CharacterTestNavigationSessionBuilder.Build(
                input, reversed, inputResult, supportedAgentHalfExtents);
            AssertNavigationBuildSucceeded(orderedSession, context + " ordered");
            AssertNavigationBuildSucceeded(reversedSession, context + " reversed");
            AssertGraphsEqual(orderedSession.Graph, reversedSession.Graph, context);
            AssertRouteSnapshotsEqual(
                CaptureRoute(
                    orderedSession,
                    R(0, 0, 0),
                    R(-250000, 0, 4000000),
                    clearanceRadiusMillimeters,
                    clearanceHeightMillimeters),
                CaptureRoute(
                    reversedSession,
                    R(0, 0, 0),
                    R(-250000, 0, 4000000),
                    clearanceRadiusMillimeters,
                    clearanceHeightMillimeters),
                context + " Player->AI");
            AssertRouteSnapshotsEqual(
                CaptureRoute(
                    orderedSession,
                    R(0, 0, 0),
                    R(-8736000, 0, 4647000),
                    clearanceRadiusMillimeters,
                    clearanceHeightMillimeters),
                CaptureRoute(
                    reversedSession,
                    R(0, 0, 0),
                    R(-8736000, 0, 4647000),
                    clearanceRadiusMillimeters,
                    clearanceHeightMillimeters),
                context + " Player->AI_1");
        }

        private static void AssertGraphsEqual(
            CharacterTestNavigationGraph expected,
            CharacterTestNavigationGraph actual,
            string context)
        {
            Assert.AreEqual(expected.Revision, actual.Revision, context + " revision");
            Assert.AreEqual(expected.ContentHash, actual.ContentHash, context + " hash");
            Assert.AreEqual(expected.NodeCount, actual.NodeCount, context + " nodes");
            Assert.AreEqual(expected.EdgeCount, actual.EdgeCount, context + " edges");
            for (int i = 0; i < expected.NodeCount; i++)
            {
                CharacterTestNavigationGraphNode left = expected.GetNode(i);
                CharacterTestNavigationGraphNode right = actual.GetNode(i);
                Assert.AreEqual(left.NodeId, right.NodeId, context + " node id " + i);
                Assert.AreEqual(left.Position, right.Position, context + " node position " + i);
                Assert.AreEqual(left.ClearanceRadiusMillimeters,
                    right.ClearanceRadiusMillimeters, context + " node radius " + i);
                Assert.AreEqual(left.ClearanceHeightMillimeters,
                    right.ClearanceHeightMillimeters, context + " node height " + i);
                Assert.AreEqual(left.TraversalPenaltyMillimeters,
                    right.TraversalPenaltyMillimeters, context + " node penalty " + i);
                Assert.AreEqual(left.AreaTagMask, right.AreaTagMask, context + " node area " + i);
            }
            for (int i = 0; i < expected.EdgeCount; i++)
            {
                CharacterTestNavigationGraphEdge left = expected.GetEdge(i);
                CharacterTestNavigationGraphEdge right = actual.GetEdge(i);
                Assert.AreEqual(left.EdgeId, right.EdgeId, context + " edge id " + i);
                Assert.AreEqual(left.FromNodeId, right.FromNodeId, context + " edge from " + i);
                Assert.AreEqual(left.ToNodeId, right.ToNodeId, context + " edge to " + i);
                Assert.AreEqual(left.LengthMillimeters, right.LengthMillimeters, context + " edge length " + i);
                Assert.AreEqual(left.ClearanceRadiusMillimeters,
                    right.ClearanceRadiusMillimeters, context + " edge radius " + i);
                Assert.AreEqual(left.ClearanceHeightMillimeters,
                    right.ClearanceHeightMillimeters, context + " edge height " + i);
                Assert.AreEqual(left.TraversalPenaltyMillimeters,
                    right.TraversalPenaltyMillimeters, context + " edge penalty " + i);
                Assert.AreEqual(left.BlockedLayerMask,
                    right.BlockedLayerMask, context + " edge blocked layers " + i);
                Assert.AreEqual(left.AreaTagMask, right.AreaTagMask, context + " edge area " + i);
            }
        }

        private static void AssertFormalRuntimeContainsFourStaticShapes(
            CharacterTestCharacterPhysicsRuntime runtime)
        {
            var aabbs = new List<CombatPhysicsAabbCollider>();
            var spheres = new List<CombatPhysicsSphereCollider>();
            var capsules = new List<CombatPhysicsCapsuleCollider>();
            var obbs = new List<CombatPhysicsObbCollider>();
            runtime.World.CopyAabbCollidersTo(aabbs);
            runtime.World.CopySphereCollidersTo(spheres);
            runtime.World.CopyCapsuleCollidersTo(capsules);
            runtime.World.CopyObbCollidersTo(obbs);
            Assert.Greater(aabbs.Count, 0);
            Assert.Greater(spheres.Count, 0);
            Assert.Greater(capsules.Count, 0);
            Assert.AreEqual(3, obbs.Count);
        }

        private static void AssertMovableDynamicCollider(
            CharacterTestCharacterPhysicsRuntime runtime,
            CharacterTestMovableNaturalObjectSpawnSpec spec)
        {
            CombatBodyId bodyId = runtime.GetMovableObjectBodyId(spec.NumericId);
            CombatEntityId entityId = runtime.GetMovableObjectEntityId(spec.NumericId);
            var colliderId = new CombatColliderId(
                CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectColliderId);
            Assert.AreEqual(
                CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectBodyIdStart + spec.NumericId,
                bodyId.Value);
            Assert.IsTrue(runtime.World.TryGetBody(bodyId, out CombatPhysicsBody body), spec.StableId);
            Assert.AreEqual(entityId, body.EntityId);
            Assert.AreEqual(
                new FixVector3(
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(spec.XMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(spec.YMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(spec.ZMillimeters)),
                body.Position);

            bool hasAabb = runtime.World.TryGetAabbCollider(bodyId, colliderId, out CombatPhysicsAabbCollider aabb);
            bool hasSphere = runtime.World.TryGetSphereCollider(bodyId, colliderId, out CombatPhysicsSphereCollider sphere);
            bool hasCapsule = runtime.World.TryGetCapsuleCollider(bodyId, colliderId, out CombatPhysicsCapsuleCollider capsule);
            bool hasObb = runtime.World.TryGetObbCollider(bodyId, colliderId, out _);
            Assert.AreEqual(spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb, hasAabb);
            Assert.AreEqual(spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Sphere, hasSphere);
            Assert.AreEqual(spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Capsule, hasCapsule);
            Assert.IsFalse(hasObb);
            if (hasAabb)
                Assert.AreEqual(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer, aabb.Layer);
            if (hasSphere)
                Assert.AreEqual(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer, sphere.Layer);
            if (hasCapsule)
                Assert.AreEqual(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer, capsule.Layer);
        }

        private static void AssertMovableSlopeGuard(
            CharacterTestCharacterPhysicsRuntime runtime,
            CharacterTestMovableNaturalObjectSpawnSpec spec)
        {
            Fix64 centerX = CharacterTestDeterministicUnits.MillimetersToFix64Meters(spec.XMillimeters);
            Fix64 centerZ = CharacterTestDeterministicUnits.MillimetersToFix64Meters(spec.ZMillimeters);
            int radiusMillimeters = Math.Max(spec.HalfExtentXMillimeters, spec.HalfExtentZMillimeters);
            Fix64 halfX = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb
                    ? spec.HalfExtentXMillimeters
                    : radiusMillimeters);
            Fix64 halfZ = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                spec.ShapeKind == CharacterTestMovableNaturalObjectShapeKind.Aabb
                    ? spec.HalfExtentZMillimeters
                    : radiusMillimeters);
            Fix64 movableMinX = centerX - halfX;
            Fix64 movableMaxX = centerX + halfX;
            Fix64 movableMinZ = centerZ - halfZ;
            Fix64 movableMaxZ = centerZ + halfZ;
            Fix64 guard = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                MovableSlopeGuardMillimeters);

            for (int i = 0; i < FormalSlopeBodyIds.Length; i++)
            {
                var bodyId = new CombatBodyId(FormalSlopeBodyIds[i]);
                Assert.IsTrue(runtime.World.TryGetBody(bodyId, out CombatPhysicsBody body));
                Assert.IsTrue(runtime.World.TryGetObbCollider(
                    bodyId,
                    new CombatColliderId(1),
                    out CombatPhysicsObbCollider slope));
                slope.GetWorldAabb(body.Position, out FixVector3 slopeMin, out FixVector3 slopeMax);
                Fix64 xGap = Fix64.Max(slopeMin.X - movableMaxX, movableMinX - slopeMax.X);
                Fix64 zGap = Fix64.Max(slopeMin.Z - movableMaxZ, movableMinZ - slopeMax.Z);
                Fix64 conservativeAxisGap = Fix64.Max(xGap, zGap);
                Assert.GreaterOrEqual(
                    conservativeAxisGap.RawValue,
                    guard.RawValue,
                    spec.StableId + " vs slope " + bodyId.Value
                        + " conservative axis gap raw=" + conservativeAxisGap.RawValue);
            }
        }

        private static bool IsFormalSlopeBody(int bodyId)
        {
            for (int i = 0; i < FormalSlopeBodyIds.Length; i++)
                if (FormalSlopeBodyIds[i] == bodyId)
                    return true;
            return false;
        }

        private static bool IsFormalSlopeStableId(string stableId)
        {
            for (int i = 0; i < FormalSlopeStableIds.Length; i++)
                if (string.Equals(FormalSlopeStableIds[i], stableId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private readonly struct RouteSnapshot
        {
            public RouteSnapshot(
                CharacterTestPathfindingSample sample,
                int[] nodeIds,
                int[] edgeIds)
            {
                Sample = sample;
                NodeIds = nodeIds ?? Array.Empty<int>();
                EdgeIds = edgeIds ?? Array.Empty<int>();
            }

            public CharacterTestPathfindingSample Sample { get; }
            public int[] NodeIds { get; }
            public int[] EdgeIds { get; }
        }
    }
}
