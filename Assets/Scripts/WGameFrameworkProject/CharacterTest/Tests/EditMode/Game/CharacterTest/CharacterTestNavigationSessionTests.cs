using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestNavigationSessionTests
    {
        [Test]
        public void SceneInput_CompilesExplicitFormalRegionAndQuantizesBounds()
        {
            GameObject ignored = CreateRegionObject("Ignored", new Vector3(20f, 0f, 20f), new Vector3(4f, 1f, 4f));
            GameObject bounds = CreateRegionObject("Bounds", new Vector3(1.25f, 0f, -0.25f), new Vector3(5f, 1f, 3f));
            try
            {
                var cache = new CharacterTestWorldObjectSceneCache();
                cache.Configure(
                    Array.Empty<CharacterTestWorldObjectSceneSource.MovableInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.StaticColliderInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.LevelPointInstance>(),
                    new[]
                    {
                        Region("ignored", ignored.transform),
                        Region("navigation.bounds", bounds.transform)
                    });
                CharacterTestNavigationSceneSettings settings =
                    CharacterTestNavigationSceneSettings.CreateForTests(
                        revision: 7,
                        boundsRegionStableId: "navigation.bounds",
                        cellSizeMeters: 2f,
                        maxNodeCount: 64,
                        maxEdgeCount: 512,
                        maxPathNodeCount: 32);

                Assert.IsTrue(CharacterTestNavigationSceneInputCompiler.TryCompile(
                    settings,
                    cache,
                    out CharacterTestNavigationSessionBuildInput input,
                    out CharacterTestNavigationSceneInputResult result));

                Assert.IsTrue(result.Success);
                Assert.AreEqual(7, input.Revision);
                Assert.AreEqual(-2000, input.MinimumXMillimeters);
                Assert.AreEqual(4000, input.MaximumXMillimeters);
                Assert.AreEqual(-2000, input.MinimumZMillimeters);
                Assert.AreEqual(2000, input.MaximumZMillimeters);
                Assert.AreEqual(2000, input.CellSizeMillimeters);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ignored);
                UnityEngine.Object.DestroyImmediate(bounds);
            }
        }

        [Test]
        public void SceneInput_UnionIsIndependentOfRegionOrder()
        {
            GameObject first = CreateRegionObject("First", new Vector3(-2f, 0f, 0f), new Vector3(4f, 1f, 4f));
            GameObject second = CreateRegionObject("Second", new Vector3(3f, 0f, 1f), new Vector3(2f, 1f, 6f));
            try
            {
                CharacterTestNavigationSceneSettings settings =
                    CharacterTestNavigationSceneSettings.CreateForTests(
                        boundsRegionStableId: "*",
                        maxNodeCount: 128,
                        maxEdgeCount: 1024,
                        maxPathNodeCount: 32);
                var forward = new CharacterTestWorldObjectSceneCache();
                forward.Configure(
                    Array.Empty<CharacterTestWorldObjectSceneSource.MovableInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.StaticColliderInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.LevelPointInstance>(),
                    new[] { Region("first", first.transform), Region("second", second.transform) });
                var reverse = new CharacterTestWorldObjectSceneCache();
                reverse.Configure(
                    Array.Empty<CharacterTestWorldObjectSceneSource.MovableInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.StaticColliderInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.LevelPointInstance>(),
                    new[] { Region("second", second.transform), Region("first", first.transform) });

                Assert.IsTrue(CharacterTestNavigationSceneInputCompiler.TryCompile(
                    settings, forward, out CharacterTestNavigationSessionBuildInput left, out _));
                Assert.IsTrue(CharacterTestNavigationSceneInputCompiler.TryCompile(
                    settings, reverse, out CharacterTestNavigationSessionBuildInput right, out _));
                Assert.AreEqual(left, right);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [TestCase(true, (int)CharacterTestNavigationSceneInputStatus.Disabled)]
        [TestCase(false, (int)CharacterTestNavigationSceneInputStatus.MissingSceneCache)]
        public void SceneInput_ReportsUnavailableInputs(bool disabled, int expectedStatus)
        {
            CharacterTestNavigationSceneSettings settings =
                CharacterTestNavigationSceneSettings.CreateForTests(enabled: !disabled);
            CharacterTestWorldObjectSceneCache cache = disabled
                ? new CharacterTestWorldObjectSceneCache()
                : null;

            Assert.IsFalse(CharacterTestNavigationSceneInputCompiler.TryCompile(
                settings, cache, out _, out CharacterTestNavigationSceneInputResult result));
            Assert.AreEqual((CharacterTestNavigationSceneInputStatus)expectedStatus, result.Status);
        }

        [Test]
        public void SceneInput_RejectsMissingAndAmbiguousExplicitBounds()
        {
            GameObject first = CreateRegionObject("First", Vector3.zero, new Vector3(4f, 1f, 4f));
            GameObject second = CreateRegionObject("Second", Vector3.zero, new Vector3(4f, 1f, 4f));
            try
            {
                CharacterTestNavigationSceneSettings settings =
                    CharacterTestNavigationSceneSettings.CreateForTests(
                        boundsRegionStableId: "navigation.bounds");
                var cache = new CharacterTestWorldObjectSceneCache();
                cache.Configure(
                    Array.Empty<CharacterTestWorldObjectSceneSource.MovableInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.StaticColliderInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.LevelPointInstance>(),
                    new[] { Region("other", first.transform) });
                Assert.IsFalse(CharacterTestNavigationSceneInputCompiler.TryCompile(
                    settings, cache, out _, out CharacterTestNavigationSceneInputResult missing));
                Assert.AreEqual(CharacterTestNavigationSceneInputStatus.BoundsRegionNotFound, missing.Status);

                cache.Configure(
                    Array.Empty<CharacterTestWorldObjectSceneSource.MovableInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.StaticColliderInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.LevelPointInstance>(),
                    new[]
                    {
                        Region("navigation.bounds", first.transform),
                        Region("navigation.bounds", second.transform)
                    });
                Assert.IsFalse(CharacterTestNavigationSceneInputCompiler.TryCompile(
                    settings, cache, out _, out CharacterTestNavigationSceneInputResult ambiguous));
                Assert.AreEqual(CharacterTestNavigationSceneInputStatus.AmbiguousBoundsRegion, ambiguous.Status);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void SceneInput_RejectsOutOfRangeQuantization()
        {
            GameObject bounds = CreateRegionObject(
                "Bounds",
                new Vector3(3_000_000f, 0f, 0f),
                new Vector3(4f, 1f, 4f));
            try
            {
                var cache = new CharacterTestWorldObjectSceneCache();
                cache.Configure(
                    Array.Empty<CharacterTestWorldObjectSceneSource.MovableInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.StaticColliderInstance>(),
                    Array.Empty<CharacterTestWorldObjectSceneSource.LevelPointInstance>(),
                    new[] { Region("navigation.bounds", bounds.transform) });

                Assert.IsFalse(CharacterTestNavigationSceneInputCompiler.TryCompile(
                    CharacterTestNavigationSceneSettings.CreateForTests(
                        boundsRegionStableId: "navigation.bounds"),
                    cache,
                    out _,
                    out CharacterTestNavigationSceneInputResult result));
                Assert.AreEqual(CharacterTestNavigationSceneInputStatus.InvalidBounds, result.Status);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bounds);
            }
        }

        [Test]
        public void SessionBuild_GeneratesStableGraphAndFiltersStaticOccupancy()
        {
            CharacterTestNavigationSessionBuildInput input = Input();
            CharacterTestCharacterPhysicsRuntime empty = CreatePhysicsRuntime(addCenterWall: false);
            CharacterTestCharacterPhysicsRuntime obstructed = CreatePhysicsRuntime(addCenterWall: true);

            CharacterTestNavigationSession first = CharacterTestNavigationSessionBuilder.Build(input, empty);
            CharacterTestNavigationSession second = CharacterTestNavigationSessionBuilder.Build(input, empty);
            CharacterTestNavigationSession blocked = CharacterTestNavigationSessionBuilder.Build(input, obstructed);

            Assert.IsTrue(first.IsAvailable);
            Assert.IsTrue(second.IsAvailable);
            Assert.IsTrue(blocked.IsAvailable);
            Assert.AreEqual(9, first.Graph.NodeCount);
            Assert.AreEqual(first.Graph.ContentHash, second.Graph.ContentHash);
            Assert.AreEqual(first.Graph.EdgeCount, second.Graph.EdgeCount);
            Assert.Less(blocked.Graph.NodeCount, first.Graph.NodeCount);
            Assert.AreNotEqual(first.Graph.ContentHash, blocked.Graph.ContentHash);
        }

        [Test]
        public void SessionBuild_ReportsTypedCapacityAndUnavailableFailures()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysicsRuntime(addCenterWall: false);
            var sceneInputResult = new CharacterTestNavigationSceneInputResult(
                CharacterTestNavigationSceneInputStatus.BoundsRegionNotFound,
                sourceIndex: -1,
                stableId: "navigation.bounds");
            CharacterTestNavigationSession unavailable = CharacterTestNavigationSessionBuilder.Build(
                null,
                physics,
                sceneInputResult);
            Assert.AreEqual(
                CharacterTestNavigationSessionBuildStatus.InputUnavailable,
                unavailable.BuildResult.Status);
            Assert.AreEqual(
                CharacterTestNavigationSceneInputStatus.BoundsRegionNotFound,
                unavailable.SceneInputResult.Status);
            Assert.AreEqual("navigation.bounds", unavailable.SceneInputResult.StableId);

            CharacterTestNavigationSession missingPhysics =
                CharacterTestNavigationSessionBuilder.Build(Input(), null);
            Assert.AreEqual(
                CharacterTestNavigationSessionBuildStatus.MissingPhysicsRuntime,
                missingPhysics.BuildResult.Status);

            CharacterTestNavigationSessionBuildInput capped = new CharacterTestNavigationSessionBuildInput(
                revision: 1,
                minimumXMillimeters: -2000,
                maximumXMillimeters: 2000,
                minimumZMillimeters: -2000,
                maximumZMillimeters: 2000,
                planeYMillimeters: 0,
                cellSizeMillimeters: 1000,
                connectDiagonals: true,
                maxNodeCount: 8,
                maxEdgeCount: 64,
                maxEndpointSnapDistanceMillimeters: 4000,
                maxNodeExpansions: 8,
                maxPathNodeCount: 8);
            CharacterTestNavigationSession tooLarge =
                CharacterTestNavigationSessionBuilder.Build(capped, physics);
            Assert.AreEqual(
                CharacterTestNavigationSessionBuildStatus.NodeCapacityExceeded,
                tooLarge.BuildResult.Status);
        }

        [Test]
        public void Composition_CreatesOneNavigationOwnerPerSliceAndRebuildReplacesIt()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysicsRuntime(addCenterWall: false);
            var options = new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                PhysicsRuntime = physics,
                NavigationBuildInput = Input(),
                CreateCharacterViews = false,
                EnableCharacterSimulation = false,
                StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>(),
                SuppressMissingStartupSpawnWarning = true
            };

            CharacterTestNavigationSession firstOwner;
            long firstHash;
            using (var first = new GameSlice(options))
            {
                firstOwner = first.Services.Modules.NavigationSession;
                firstHash = firstOwner.Graph.ContentHash;
                var service = first.Services.Modules.NavigationService
                    as CharacterTestCombatPhysicsNavigationService;
                Assert.IsNotNull(service);
                Assert.AreSame(firstOwner, service.NavigationSession);
                Assert.AreSame(firstOwner.Graph, firstOwner.Pathfinder.Graph);
            }

            using (var rebuilt = new GameSlice(options))
            {
                CharacterTestNavigationSession nextOwner = rebuilt.Services.Modules.NavigationSession;
                Assert.AreNotSame(firstOwner, nextOwner);
                Assert.AreEqual(firstHash, nextOwner.Graph.ContentHash);
                Assert.AreNotSame(firstOwner.Pathfinder, nextOwner.Pathfinder);
                Assert.AreNotSame(firstOwner.PathBuffer, nextOwner.PathBuffer);
            }
        }

        [Test]
        public void Composition_ConsumesNavigationInputCarriedByPhysicsRuntime()
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysicsRuntime(addCenterWall: false);
            physics.SetNavigationBuildInput(Input());
            using var slice = new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                PhysicsRuntime = physics,
                CreateCharacterViews = false,
                EnableCharacterSimulation = false,
                StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>(),
                SuppressMissingStartupSpawnWarning = true
            });

            Assert.IsTrue(slice.Services.Modules.NavigationSession.IsAvailable);
            Assert.AreEqual(
                CharacterTestNavigationSceneInputStatus.Success,
                slice.Services.Modules.NavigationSession.SceneInputResult.Status);
            Assert.AreEqual(
                physics.NavigationBuildInput.Value.Revision,
                slice.Services.Modules.NavigationSession.Graph.Revision);
        }

        [Test]
        public void SessionBuild_UsesLargestPlayableCharacterClearance()
        {
            var physics = new CharacterTestCharacterPhysicsRuntime(
                new CombatPhysicsWorld(),
                CombatPhysicsLayerMask.FromLayer(1),
                Position(0.45f, 0.9f, 0.45f),
                Fix64.FromRatio(1, 100),
                Fix64.FromRatio(1, 4),
                staticColliderCount: 1,
                sourceDescription: "test.navigation.playable-clearance");
            var tallDefinition = new CharacterTestCharacterDefinition(
                characterId: 910001,
                displayName: "Tall navigation fixture",
                prefabKey: default,
                defaultTeamId: 1,
                animationBinding: default,
                physicsProfile: new CharacterTestCharacterPhysicsComponent(
                    capsuleRadiusMeters: 0.6f,
                    capsuleHeightMeters: 2.4f,
                    capsuleCenterY: 1.2f,
                    massKg: 80f));
            var definitions = new CharacterTestCharacterDefinitionRegistry(
                new[] { tallDefinition });
            var runtime = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                definitions,
                physicsRuntime: physics,
                createViews: false);
            try
            {
                FixVector3 supported = runtime.ResolveMaximumNavigationAgentHalfExtents();
                CharacterTestNavigationSession session = CharacterTestNavigationSessionBuilder.Build(
                    Input(),
                    physics,
                    supportedAgentHalfExtents: supported);

                Assert.IsTrue(session.IsAvailable, session.BuildResult.Status.ToString());
                Assert.AreEqual(Position(0.6f, 1.2f, 0.6f), supported);
                CharacterTestNavigationGraphNode node = session.Graph.GetNode(0);
                Assert.AreEqual(600, node.ClearanceRadiusMillimeters);
                Assert.AreEqual(2400, node.ClearanceHeightMillimeters);
                Assert.IsTrue(session.Graph.TryFindNearestNode(
                    Position(0f, 0f, 0f),
                    requiredClearanceRadiusMillimeters: 600,
                    requiredClearanceHeightMillimeters: 2400,
                    out _,
                    out _));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [TestCase("Assets/Scenes/CharacterTestScene.unity", "genesis.scene.bounds")]
        [TestCase("Assets/Scenes/CharacterTestRuntimeAiLabScene.unity", "*")]
        public void FormalSceneFixture_CompilesAndBuildsInjectedNavigationSession(
            string scenePath,
            string expectedBoundsStableId)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                GameManager gameManager = FindGameManager(scene);
                Assert.IsNotNull(gameManager, scenePath);
                Assert.AreEqual(
                    expectedBoundsStableId,
                    gameManager.NavigationSettings.BoundsRegionStableId);
                Assert.IsTrue(CharacterTestNavigationSceneInputCompiler.TryCompile(
                    gameManager.NavigationSettings,
                    gameManager.WorldObjectSceneCache,
                    out CharacterTestNavigationSessionBuildInput input,
                    out CharacterTestNavigationSceneInputResult inputResult),
                    inputResult.Status.ToString());

                CharacterTestCharacterPhysicsRuntime physics =
                    CreatePhysicsRuntime(addCenterWall: false);
                CharacterTestNavigationSession first = CharacterTestNavigationSessionBuilder.Build(
                    input,
                    physics,
                    inputResult);
                CharacterTestNavigationSession second = CharacterTestNavigationSessionBuilder.Build(
                    input,
                    physics,
                    inputResult);

                Assert.IsTrue(first.IsAvailable, first.BuildResult.Status.ToString());
                Assert.AreEqual(CharacterTestNavigationSceneInputStatus.Success, first.SceneInputResult.Status);
                Assert.Greater(first.Graph.NodeCount, 0);
                Assert.AreEqual(first.Graph.ContentHash, second.Graph.ContentHash);
                Assert.AreEqual(first.Graph.Revision, second.Graph.Revision);
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        [Test]
        public void SessionRuntime_PathQueriesAreNoAllocAfterInitialization()
        {
            CharacterTestNavigationSession session = CharacterTestNavigationSessionBuilder.Build(
                Input(),
                CreatePhysicsRuntime(addCenterWall: false));
            var query = new CharacterTestPathQuery(
                session.Graph.Revision,
                session.Graph.ContentHash,
                Position(-2, 0, -2),
                Position(2, 0, 2),
                clearanceRadiusMillimeters: 500,
                clearanceHeightMillimeters: 2000,
                maxEndpointSnapDistanceMillimeters: session.Input.MaxEndpointSnapDistanceMillimeters,
                maxNodeExpansions: session.Input.MaxNodeExpansions,
                maxPathNodeCount: session.Input.MaxPathNodeCount);
            Assert.IsTrue(session.Pathfinder.TryFindPath(
                query, session.PathBuffer, out CharacterTestPathfindingSample warm));
            Assert.IsTrue(warm.Success);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int successCount = 0;
            for (int i = 0; i < 128; i++)
            {
                if (session.Pathfinder.TryFindPath(query, session.PathBuffer, out _))
                    successCount++;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(128, successCount);
            Assert.AreEqual(0L, allocated);
        }

        private static CharacterTestNavigationSessionBuildInput Input()
        {
            return new CharacterTestNavigationSessionBuildInput(
                revision: 3,
                minimumXMillimeters: -2000,
                maximumXMillimeters: 2000,
                minimumZMillimeters: -2000,
                maximumZMillimeters: 2000,
                planeYMillimeters: 0,
                cellSizeMillimeters: 2000,
                connectDiagonals: true,
                maxNodeCount: 64,
                maxEdgeCount: 512,
                maxEndpointSnapDistanceMillimeters: 4000,
                maxNodeExpansions: 64,
                maxPathNodeCount: 16);
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysicsRuntime(bool addCenterWall)
        {
            var world = new CombatPhysicsWorld();
            if (addCenterWall)
            {
                var bodyId = new CombatBodyId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 1);
                world.UpsertBody(new CombatPhysicsBody(
                    new CombatEntityId(CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart + 1),
                    bodyId,
                    FixVector3.Zero));
                world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                    bodyId,
                    new CombatColliderId(1),
                    layer: 1,
                    localMin: Position(-0.25f, 0f, -0.25f),
                    localMax: Position(0.25f, 2f, 0.25f),
                    surfaceMaterial: CombatSurfaceMaterial.Default));
            }

            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.FromRatio(1, 4),
                staticColliderCount: 1,
                sourceDescription: addCenterWall ? "test.navigation.wall" : "test.navigation.empty");
        }

        private static CharacterTestWorldObjectSceneSource.LevelRegionInstance Region(
            string stableId,
            Transform transform)
        {
            return new CharacterTestWorldObjectSceneSource.LevelRegionInstance(
                true,
                definitionId: 3002,
                stableId,
                transform,
                CharacterTestWorldObjectInstanceScaleSource.SceneTransform);
        }

        private static GameManager FindGameManager(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameManager gameManager = roots[i].GetComponentInChildren<GameManager>(true);
                if (gameManager != null)
                    return gameManager;
            }

            return null;
        }

        private static GameObject CreateRegionObject(
            string name,
            Vector3 position,
            Vector3 scale)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            return gameObject;
        }

        private static FixVector3 Position(float x, float y, float z)
        {
            return new FixVector3(ToFix64(x), ToFix64(y), ToFix64(z));
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }
    }
}
