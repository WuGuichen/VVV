using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Combat.Core;
using MxFramework.Combat.Levels;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestSlopeFormalSceneTests
    {
        private const string ScenePath = "Assets/Scenes/CharacterTestScene.unity";
        private const string DocumentPath =
            "Assets/Config/MxFramework/Levels/CharacterTest/character_test_scene.world_objects.json";
        private const string ExpectedAuthoringHash =
            "1e2bd8d6e99ad7bb6610a882c0ad084869be213a53bd4d21b6546833d9f9a10e";
        private const ulong ExpectedLevelHash = 12242125253111897828UL;
        private const long ExpectedGraphHash = 8908302957704831515L;

        private static readonly string[] SlopePaths =
        {
            "TestCourse_30_MotionGeometry[0]/CharacterTest_Physics_SmoothRamps[4]/CharacterTest_Physics_SmoothRamp_15deg[0]/SlopeSurface[0]",
            "TestCourse_30_MotionGeometry[0]/CharacterTest_Physics_SmoothRamps[4]/CharacterTest_Physics_SmoothRamp_30deg[1]/SlopeSurface[0]",
            "TestCourse_30_MotionGeometry[0]/CharacterTest_Physics_SmoothRamps[4]/CharacterTest_Physics_SmoothRamp_50deg_Blocker[2]/SlopeSurface[0]"
        };

        [Test]
        public void FormalScene_SlopeViewsHaveFrozenHierarchyGeometryMaterialAndNoCollider()
        {
            using var scope = new OfficialSceneScope();
            GameManager manager = scope.Manager;
            float[] angles = { 15f, 30f, 50f };
            float[] y = { 0.534976f, 1.283494f, 2.137436f };
            for (int i = 0; i < SlopePaths.Length; i++)
            {
                Assert.IsTrue(CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                    manager.WorldObjectSceneRoot, SlopePaths[i], out Transform slope), SlopePaths[i]);
                Assert.AreEqual("SlopeSurface", slope.name);
                Assert.AreEqual(0, slope.GetSiblingIndex());
                AssertVector(new Vector3(0f, y[i], 0f), slope.localPosition, 0.000001f);
                AssertVector(new Vector3(2.5f, 0.5f, 6f), slope.localScale, 0.000001f);
                Assert.Less(Quaternion.Angle(Quaternion.Euler(angles[i], 0f, 0f), slope.localRotation), 0.0001f);
                Assert.IsNull(slope.GetComponent<Collider>());
                Assert.IsNotNull(slope.GetComponent<MeshFilter>());
                MeshRenderer renderer = slope.GetComponent<MeshRenderer>();
                Assert.IsNotNull(renderer);
                Assert.IsNotNull(renderer.sharedMaterial);
                Assert.That(
                    AssetDatabase.GetAssetPath(renderer.sharedMaterial),
                    Does.EndWith("CharacterTestPhysicsTerracedRamp.mat"));
            }
        }

        [Test]
        public void FormalAuthoring_RowsCacheAndSecondBaselineShareCanonicalHashWithoutGeometryFields()
        {
            using var scope = new OfficialSceneScope();
            GameManager manager = scope.Manager;
            string json = File.ReadAllText(DocumentPath);
            MxLevelAuthoringDocument envelope = MxLevelAuthoringJson.LoadFromJson(json);
            Assert.AreEqual(ExpectedAuthoringHash, MxLevelAuthoringJson.ComputeContentHash(envelope));
            Assert.AreEqual(ExpectedAuthoringHash, manager.WorldObjectSceneCache.AuthoringContentHash);
            Assert.AreEqual(39, manager.WorldObjectSceneCache.StaticColliderInstanceCount);
            Assert.IsFalse(json.Contains("\"axisX\""));
            Assert.IsFalse(json.Contains("\"halfExtents\""));
            Assert.IsTrue(CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                envelope, out LevelWorldObjectAuthoringDocument document, out LevelWorldObjectAuthoringValidationReport report),
                JoinIssues(report));

            for (int i = 0; i < SlopePaths.Length; i++)
            {
                LevelWorldObjectInstanceDefinition row = FindRow(document, SlopePaths[i]);
                Assert.IsNotNull(row, SlopePaths[i]);
                Assert.AreEqual(10080 + i, row.BodyId);
                Assert.AreEqual(1, row.ColliderId);
                Assert.AreEqual(1012, row.DefinitionId);
                Assert.AreEqual(LevelWorldObjectScaleSource.SceneTransform, row.ScaleSource);
                Assert.AreEqual("character-test.slope." + (i == 0 ? "15deg" : i == 1 ? "30deg" : "50deg"), row.InstanceStableId);
                Assert.AreEqual("TestCourse_30_MotionGeometry", row.Group);
            }

            CharacterTestCombatLevelSceneAdapterResult first =
                CharacterTestCombatLevelSceneAdapter.BuildLevel(
                    manager.WorldObjectSceneRoot, manager.WorldObjectSceneCache);
            CharacterTestCombatLevelSceneAdapterResult second =
                CharacterTestCombatLevelSceneAdapter.BuildLevel(
                    manager.WorldObjectSceneRoot, manager.WorldObjectSceneCache);
            Assert.IsFalse(first.HasErrors);
            Assert.AreEqual(first.ContentHash, second.ContentHash);
            Assert.AreEqual(ExpectedLevelHash, first.ContentHash.Value);
        }

        [Test]
        public void FormalRuntime_CompilesThreeExactObbsWithSnapMaskMaterialAndStableHash()
        {
            using var scope = new OfficialSceneScope();
            GameManager manager = scope.Manager;
            CharacterTestCombatLevelSceneAdapterResult level =
                CharacterTestCombatLevelSceneAdapter.BuildLevel(
                    manager.WorldObjectSceneRoot, manager.WorldObjectSceneCache);
            Assert.IsFalse(level.HasErrors);
            Assert.AreEqual(34, level.AabbColliderCount);
            Assert.AreEqual(3, level.ObbColliderCount);
            Assert.AreEqual(ExpectedLevelHash, level.ContentHash.Value);

            long[][] centers =
            {
                new[] { -22500000L, 534976L, -7000000L },
                new[] { -22500000L, 1283494L, 15000000L },
                new[] { -23200001L, 2137436L, 18500000L }
            };
            long[] axisYx = { 258819L, 500000L, 766045L };
            long[] axisYy = { 965926L, 866025L, 642788L };
            long[] axisZx = { 965926L, 866025L, 642788L };
            long[] axisZy = { -258819L, -500000L, -766044L };
            for (int i = 0; i < 3; i++)
            {
                CombatLevelObbColliderDefinition obb = FindObb(level.Definition, 10080 + i);
                Assert.AreEqual(1, obb.ColliderId);
                Assert.AreEqual(1, obb.Layer);
                Assert.AreEqual(R(centers[i][0], centers[i][1], centers[i][2]), obb.Position);
                Assert.AreEqual(R(1250000L, 250000L, 3000000L), obb.HalfExtents);
                Assert.AreEqual(R(0L, 0L, -1000000L), obb.AxisX);
                Assert.AreEqual(R(axisYx[i], axisYy[i], 0), obb.AxisY);
                Assert.AreEqual(R(axisZx[i], axisZy[i], 0), obb.AxisZ);
                Assert.AreEqual(CombatSurfaceMaterialKind.Rough, obb.SurfaceMaterial.Kind);
                Assert.AreEqual(60000000L, obb.SurfaceMaterial.FrictionRaw);
            }

            CharacterTestCharacterPhysicsRuntime runtime = CharacterTestCombatPhysicsSceneBridge.BuildRuntime(
                manager.WorldObjectSceneRoot, manager.WorldObjectSceneCache, manager.PhysicsSettings);
            Assert.IsNotNull(runtime);
            Assert.AreEqual(100000L, runtime.GroundSnapDistance.RawValue);
            Assert.IsTrue(runtime.StaticObstacleLayerMask.ContainsLayer(1));
            Assert.AreEqual(3, CopyObbs(runtime.World).Count);
        }

        [Test]
        public void FormalRuntime_PreservesTerracedSurfaceAndSphereCapsuleFixtures()
        {
            using var scope = new OfficialSceneScope();
            GameManager manager = scope.Manager;
            CharacterTestCharacterPhysicsRuntime runtime = CharacterTestCombatPhysicsSceneBridge.BuildRuntime(
                manager.WorldObjectSceneRoot, manager.WorldObjectSceneCache, manager.PhysicsSettings);
            Assert.IsNotNull(runtime);
            for (int body = 10060; body <= 10071; body++)
                Assert.IsTrue(runtime.World.TryGetAabbCollider(new CombatBodyId(body), new CombatColliderId(1), out _), "terraced " + body);
            for (int body = 61710; body <= 61713; body++)
                Assert.IsTrue(runtime.World.TryGetAabbCollider(new CombatBodyId(body), new CombatColliderId(1), out _), "surface " + body);
            Assert.IsTrue(runtime.World.TryGetSphereCollider(new CombatBodyId(13010), new CombatColliderId(1), out _));
            Assert.IsTrue(runtime.World.TryGetCapsuleCollider(new CombatBodyId(13011), new CombatColliderId(1), out _));
        }

        [Test]
        public void FormalNavigation_HasFrozenGraphAndTwoStableRouteSequences()
        {
            using var scope = new OfficialSceneScope();
            GameManager manager = scope.Manager;
            Assert.IsTrue(CharacterTestNavigationSceneInputCompiler.TryCompile(
                manager.NavigationSettings,
                manager.WorldObjectSceneCache,
                out CharacterTestNavigationSessionBuildInput input,
                out CharacterTestNavigationSceneInputResult inputResult), inputResult.Status.ToString());
            CharacterTestCharacterPhysicsRuntime runtime = CharacterTestCombatPhysicsSceneBridge.BuildRuntime(
                manager.WorldObjectSceneRoot, manager.WorldObjectSceneCache, manager.PhysicsSettings);
            CharacterTestNavigationSession session = CharacterTestNavigationSessionBuilder.Build(
                input, runtime, inputResult);
            Assert.IsTrue(session.IsAvailable, session.BuildResult.Status.ToString());
            Assert.AreEqual(1, session.Graph.Revision);
            Assert.AreEqual(ExpectedGraphHash, session.Graph.ContentHash);
            Assert.AreEqual(1159, session.Graph.NodeCount);
            Assert.AreEqual(8406, session.Graph.EdgeCount);

            AssertRoute(session, R(0, 0, 0), R(-250000, 0, 4000000),
                new[] { 644, 683, 722 }, new[] { 5146, 5458 });
            AssertRoute(session, R(0, 0, 0), R(-8736000, 0, 4647000),
                new[] { 644, 643, 642, 680, 718 }, new[] { 5147, 5139, 5134, 5438 });
        }

        private static void AssertRoute(
            CharacterTestNavigationSession session,
            FixVector3 start,
            FixVector3 target,
            int[] expectedNodes,
            int[] expectedEdges)
        {
            var query = new CharacterTestPathQuery(
                session.Graph.Revision,
                session.Graph.ContentHash,
                start,
                target,
                450,
                1800,
                session.Input.MaxEndpointSnapDistanceMillimeters,
                session.Input.MaxNodeExpansions,
                session.Input.MaxPathNodeCount);
            Assert.IsTrue(session.Pathfinder.TryFindPath(
                query, session.PathBuffer, out CharacterTestPathfindingSample sample));
            Assert.IsTrue(sample.Success, sample.Status.ToString());
            Assert.AreEqual(expectedNodes.Length, session.PathBuffer.NodeCount);
            Assert.AreEqual(expectedEdges.Length, session.PathBuffer.EdgeCount);
            for (int i = 0; i < expectedNodes.Length; i++)
                Assert.AreEqual(expectedNodes[i], session.PathBuffer.GetNodeId(i), "node " + i);
            for (int i = 0; i < expectedEdges.Length; i++)
                Assert.AreEqual(expectedEdges[i], session.PathBuffer.GetEdgeId(i), "edge " + i);
        }

        private static CombatLevelObbColliderDefinition FindObb(CombatLevelDefinition definition, int bodyId)
        {
            for (int i = 0; i < definition.ObbColliders.Count; i++)
                if (definition.ObbColliders[i].BodyId == bodyId)
                    return definition.ObbColliders[i];
            Assert.Fail("Missing OBB body " + bodyId);
            return null;
        }

        private static LevelWorldObjectInstanceDefinition FindRow(
            LevelWorldObjectAuthoringDocument document,
            string path)
        {
            for (int i = 0; i < document.SingleStaticColliders.Count; i++)
                if (document.SingleStaticColliders[i].TransformRef.Path == path)
                    return document.SingleStaticColliders[i];
            return null;
        }

        private static List<CombatPhysicsObbCollider> CopyObbs(CombatPhysicsWorld world)
        {
            var values = new List<CombatPhysicsObbCollider>();
            world.CopyObbCollidersTo(values);
            return values;
        }

        private static CombatLevelFixedVector3 R(long x, long y, long z) =>
            new CombatLevelFixedVector3(x, y, z);

        private static FixVector3 R(int x, int y, int z) =>
            new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));

        private static void AssertVector(Vector3 expected, Vector3 actual, float tolerance)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance);
            Assert.AreEqual(expected.y, actual.y, tolerance);
            Assert.AreEqual(expected.z, actual.z, tolerance);
        }

        private static string JoinIssues(LevelWorldObjectAuthoringValidationReport report)
        {
            if (report == null || report.Issues.Count == 0)
                return string.Empty;
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < report.Issues.Count; i++)
                text.Append(report.Issues[i].Code).Append('@').Append(report.Issues[i].Path).Append(';');
            return text.ToString();
        }

        private static GameManager FindGameManager(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameManager manager = roots[i].GetComponentInChildren<GameManager>(true);
                if (manager != null)
                    return manager;
            }
            return null;
        }

        private sealed class OfficialSceneScope : IDisposable
        {
            private readonly Scene _previousActive;
            private readonly bool _opened;
            private readonly bool _initialDirty;

            public OfficialSceneScope()
            {
                _previousActive = SceneManager.GetActiveScene();
                Scene = SceneManager.GetSceneByPath(ScenePath);
                _opened = !Scene.IsValid() || !Scene.isLoaded;
                if (_opened)
                    Scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                Assert.IsTrue(Scene.IsValid() && Scene.isLoaded);
                _initialDirty = Scene.isDirty;
                Assert.IsFalse(_initialDirty, "Formal scene must be saved and clean before read-only acceptance.");
                Manager = FindGameManager(Scene);
                Assert.IsNotNull(Manager);
            }

            public Scene Scene { get; private set; }
            public GameManager Manager { get; }

            public void Dispose()
            {
                Assert.AreEqual(_initialDirty, Scene.isDirty, "Formal scene dirty state changed by read-only test.");
                if (_previousActive.IsValid() && _previousActive.isLoaded)
                    SceneManager.SetActiveScene(_previousActive);
                if (_opened && Scene.IsValid() && Scene.isLoaded)
                    EditorSceneManager.CloseScene(Scene, true);
            }
        }
    }
}
