using System.Reflection;
using MxFramework.Combat.Levels;
using MxFramework.Combat.Physics;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestCombatLevelEditorPreviewTests
    {
        [Test]
        public void PreviewModel_BuildsHashColliderSurfaceAndSpawnSummary()
        {
            GameObject root = CreateBridgeRoot("CharacterTest Editor Preview Root", out CharacterTestCombatPhysicsSceneBridge bridge);
            GameObject markerObject = new GameObject("Bounce Pad Floor");
            try
            {
                root.transform.position = new Vector3(3f, 4f, 5f);
                markerObject.transform.SetParent(root.transform, worldPositionStays: false);
                markerObject.transform.position = new Vector3(1f, 2f, 3f);
                markerObject.transform.localScale = new Vector3(4f, 2f, 6f);
                CharacterTestCombatPhysicsAabbMarker marker =
                    markerObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(marker, "_combatLayer", 6);
                SetPrivateField(marker, "_bodyId", 321);
                SetPrivateField(marker, "_colliderId", 7);
                SetPrivateField(marker, "_surfaceMaterialKind", CombatSurfaceMaterialKind.BouncePad);
                SetPrivateField(marker, "_surfaceFriction", 17f);
                SetPrivateField(marker, "_surfaceElasticity", 0f);
                SetPrivateField(marker, "_surfaceFixedBounceHeight", 4f);

                CharacterTestCombatLevelPreviewModel model =
                    CharacterTestCombatLevelPreviewModel.Build(bridge);

                Assert.AreEqual("character-test.level.charactertest_editor_preview_root", model.LevelStableId);
                Assert.IsFalse(string.IsNullOrEmpty(model.ContentHash));
                Assert.AreEqual(1, model.ColliderCount);
                Assert.AreEqual(1, model.SpawnPointCount);
                Assert.AreEqual(new Vector3(3f, 4f, 5f), model.DefaultSpawnPosition);
                Assert.AreEqual(0, model.ErrorCount);
                Assert.AreEqual(0, model.WarningCount);

                CharacterTestCombatLevelPreviewCollider collider = model.Colliders[0];
                Assert.AreEqual("body.321.collider.7", collider.StableId);
                Assert.AreEqual(321, collider.EntityId);
                Assert.AreEqual(321, collider.BodyId);
                Assert.AreEqual(7, collider.ColliderId);
                Assert.AreEqual(6, collider.Layer);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), collider.Position);
                Assert.AreEqual(new Vector3(-2f, -1f, -3f), collider.LocalMin);
                Assert.AreEqual(new Vector3(2f, 1f, 3f), collider.LocalMax);
                Assert.AreEqual(new Vector3(-1f, 1f, 0f), collider.WorldMin);
                Assert.AreEqual(new Vector3(3f, 3f, 6f), collider.WorldMax);
                Assert.AreEqual(CombatSurfaceMaterialKind.BouncePad, collider.SurfaceMaterialKind);
                Assert.AreEqual("bounce_pad", collider.SurfaceMaterialStableId);
                Assert.AreEqual(17f, collider.SurfaceFriction, 0.0001f);
                Assert.AreEqual(0f, collider.SurfaceElasticity, 0.0001f);
                Assert.AreEqual(4f, collider.SurfaceFixedBounceHeight, 0.0001f);
                StringAssert.Contains("\"levelStableId\"", model.CreateJsonPreview());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PreviewModel_ReportsTypedInvalidTransformAndMarkerIssues()
        {
            GameObject root = CreateBridgeRoot("CharacterTest Editor Diagnostics Root", out CharacterTestCombatPhysicsSceneBridge bridge);
            GameObject firstObject = new GameObject("Invalid Floor A");
            GameObject secondObject = new GameObject("Invalid Floor B");
            try
            {
                firstObject.transform.SetParent(root.transform, worldPositionStays: false);
                secondObject.transform.SetParent(root.transform, worldPositionStays: false);
                firstObject.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                firstObject.transform.localScale = new Vector3(0f, 2f, 2f);

                CharacterTestCombatPhysicsAabbMarker first =
                    firstObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                CharacterTestCombatPhysicsAabbMarker second =
                    secondObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(first, "_combatLayer", 99);
                SetPrivateField(first, "_bodyId", 10);
                SetPrivateField(first, "_colliderId", 1);
                SetPrivateField(second, "_bodyId", 10);
                SetPrivateField(second, "_colliderId", 1);

                CharacterTestCombatLevelPreviewModel model =
                    CharacterTestCombatLevelPreviewModel.Build(bridge);

                Assert.IsTrue(model.HasErrors);
                AssertHasIssue(model, CharacterTestCombatLevelSceneAdapter.InvalidTransformScaleCode);
                AssertHasIssue(model, CharacterTestCombatLevelPreviewDiagnosticCodes.InvalidMarkerLayer);
                AssertHasIssue(model, CharacterTestCombatLevelPreviewDiagnosticCodes.InvalidMarkerExtents);
                Assert.AreEqual(1, model.ColliderCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PreviewModel_ExplicitCompiledResultBuildsRealObbCornersMaterialAndHash()
        {
            GameObject root = CreateBridgeRoot("CharacterTest Explicit OBB Preview", out _);
            GameObject markerObject = new GameObject("Rotated Preview Box");
            try
            {
                markerObject.transform.SetParent(root.transform, false);
                markerObject.transform.position = new Vector3(1f, 2f, 3f);
                markerObject.transform.localScale = new Vector3(4f, 2f, 6f);
                markerObject.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
                CharacterTestCombatPhysicsAabbMarker marker = markerObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(marker, "_bodyId", 777);
                SetPrivateField(marker, "_colliderId", 9);
                SetPrivateField(marker, "_surfaceMaterialKind", CombatSurfaceMaterialKind.Rough);
                CharacterTestCombatLevelSceneAdapterResult compiled =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, new[] { marker });

                CharacterTestCombatLevelPreviewModel model = CharacterTestCombatLevelPreviewModel.Build(compiled);
                CharacterTestCombatLevelPreviewCollider collider = model.Colliders[0];
                var corners = new Vector3[8];
                collider.CopyWorldCornersTo(corners);

                Assert.AreEqual(CombatPhysicsShapeKind.Obb, collider.ShapeKind);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), collider.Center);
                Assert.AreEqual(new Vector3(2f, 1f, 3f), collider.HalfExtents);
                Assert.AreEqual(CombatSurfaceMaterialKind.Rough, collider.SurfaceMaterialKind);
                Assert.IsFalse(string.IsNullOrWhiteSpace(model.ContentHash));
                Assert.AreEqual(8, new System.Collections.Generic.HashSet<Vector3>(corners).Count);
                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 relative = corners[i] - collider.Center;
                    Assert.AreEqual(2f, Mathf.Abs(Vector3.Dot(relative, collider.AxisX)), 0.0001f);
                    Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(relative, collider.AxisY)), 0.0001f);
                    Assert.AreEqual(3f, Mathf.Abs(Vector3.Dot(relative, collider.AxisZ)), 0.0001f);
                }
                StringAssert.Contains("\"obbColliders\"", model.CreateJsonPreview());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ScenePreview_UsesFormalGameManagerRootAndCacheIgnoringLegacyDecoy()
        {
            var managerObject = new GameObject("GameManager Host");
            GameManager manager = managerObject.AddComponent<GameManager>();
            var formalRootObject = new GameObject("Formal World Root");
            var bridgeObject = new GameObject("Preview Bridge");
            var formalBox = new GameObject("Formal Rotated Box");
            var decoyObject = new GameObject("Legacy Decoy");
            try
            {
                formalRootObject.transform.SetParent(managerObject.transform, false);
                bridgeObject.transform.SetParent(managerObject.transform, false);
                formalBox.transform.SetParent(formalRootObject.transform, false);
                formalBox.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
                CharacterTestCombatPhysicsSceneBridge bridge = bridgeObject.AddComponent<CharacterTestCombatPhysicsSceneBridge>();
                SetPrivateField(manager, "_worldObjectSceneRoot", formalRootObject.transform);
                manager.WorldObjectSceneCache.Configure(
                    new CharacterTestWorldObjectSceneSource.MovableInstance[0],
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            formalBox.transform,
                            800,
                            5,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    });

                CharacterTestWorldObjectSceneSource decoy = decoyObject.AddComponent<CharacterTestWorldObjectSceneSource>();
                decoy.Configure(
                    new CharacterTestWorldObjectSceneSource.MovableInstance[0],
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            decoyObject.transform,
                            999,
                            1,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    });

                CharacterTestCombatLevelPreviewModel expected = CharacterTestCombatLevelPreviewModel.Build(
                    formalRootObject.transform,
                    manager.WorldObjectSceneCache);
                CharacterTestCombatLevelPreviewModel actual =
                    CharacterTestCombatLevelScenePreview.BuildModelForBridgeForTests(bridge);
                Selection.activeGameObject = formalBox;
                System.Collections.Generic.HashSet<string> selected =
                    CharacterTestCombatLevelScenePreview.CollectSelectedStableIdsForTests(bridge);

                Assert.AreEqual(expected.ContentHash, actual.ContentHash);
                Assert.AreEqual(expected.Definition.LevelStableId, actual.Definition.LevelStableId);
                Assert.AreEqual(expected.ColliderCount, actual.ColliderCount);
                Assert.AreEqual(800, actual.Colliders[0].BodyId);
                Assert.AreNotEqual(999, actual.Colliders[0].BodyId);
                Assert.IsTrue(selected.Contains("body.800.collider.5"));
            }
            finally
            {
                Selection.activeObject = null;
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(decoyObject);
            }
        }

        [Test]
        public void PreviewModel_SortsScenePreviewCollidersByStableId()
        {
            GameObject root = CreateBridgeRoot("CharacterTest Editor Sorted Root", out CharacterTestCombatPhysicsSceneBridge bridge);
            GameObject laterObject = new GameObject("Later Stable Id");
            GameObject earlierObject = new GameObject("Earlier Stable Id");
            try
            {
                laterObject.transform.SetParent(root.transform, worldPositionStays: false);
                earlierObject.transform.SetParent(root.transform, worldPositionStays: false);
                CharacterTestCombatPhysicsAabbMarker later =
                    laterObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                CharacterTestCombatPhysicsAabbMarker earlier =
                    earlierObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(later, "_bodyId", 20);
                SetPrivateField(later, "_colliderId", 1);
                SetPrivateField(earlier, "_bodyId", 10);
                SetPrivateField(earlier, "_colliderId", 1);

                CharacterTestCombatLevelPreviewModel model =
                    CharacterTestCombatLevelPreviewModel.Build(bridge);

                Assert.AreEqual(2, model.ColliderCount);
                Assert.AreEqual("body.10.collider.1", model.Colliders[0].StableId);
                Assert.AreEqual("body.20.collider.1", model.Colliders[1].StableId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PreviewModel_ReportsMissingAabbMarker()
        {
            GameObject root = CreateBridgeRoot("CharacterTest Editor Empty Root", out CharacterTestCombatPhysicsSceneBridge bridge);
            try
            {
                CharacterTestCombatLevelPreviewModel model =
                    CharacterTestCombatLevelPreviewModel.Build(bridge);

                Assert.AreEqual(0, model.ColliderCount);
                Assert.AreEqual(1, model.WarningCount);
                AssertHasIssue(model, CharacterTestCombatLevelPreviewDiagnosticCodes.MissingAabbMarker);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ScenePreview_LabelModeDefaultsToOffAndCanBeChanged()
        {
            CharacterTestCombatLevelScenePreview.ResetLabelModePreferenceForTests();
            try
            {
                Assert.AreEqual(
                    CharacterTestCombatLevelSceneLabelMode.Off,
                    CharacterTestCombatLevelScenePreview.LabelMode);

                CharacterTestCombatLevelScenePreview.LabelMode =
                    CharacterTestCombatLevelSceneLabelMode.Issues;

                Assert.AreEqual(
                    CharacterTestCombatLevelSceneLabelMode.Issues,
                    CharacterTestCombatLevelScenePreview.LabelMode);

                CharacterTestCombatLevelScenePreview.LabelMode =
                    CharacterTestCombatLevelSceneLabelMode.Selected;

                Assert.AreEqual(
                    CharacterTestCombatLevelSceneLabelMode.Selected,
                    CharacterTestCombatLevelScenePreview.LabelMode);
            }
            finally
            {
                CharacterTestCombatLevelScenePreview.ResetLabelModePreferenceForTests();
            }
        }

        [Test]
        public void ScenePreview_ShortLabelOmitsFullStableId()
        {
            var collider = new CharacterTestCombatLevelPreviewCollider(
                "body.61710.collider.1",
                entityId: 61710,
                bodyId: 61710,
                colliderId: 1,
                layer: 1,
                position: Vector3.zero,
                localMin: -Vector3.one,
                localMax: Vector3.one,
                worldMin: -Vector3.one,
                worldMax: Vector3.one,
                surfaceMaterialKind: CombatSurfaceMaterialKind.HardSlick,
                surfaceMaterialStableId: "hard_slick",
                surfaceFriction: 1f,
                surfaceElasticity: 0f,
                surfaceFixedBounceHeight: 0f);

            string label = CharacterTestCombatLevelScenePreview.CreateShortLabel(collider);

            Assert.AreEqual("B61710/C1 hard_slick", label);
            Assert.IsFalse(label.Contains("body.61710.collider.1"));
            Assert.IsFalse(label.Contains("\n"));
        }

        [Test]
        public void PreviewModel_KeepsFullColliderDataForInspector()
        {
            GameObject root = CreateBridgeRoot("CharacterTest Editor Full Data Root", out CharacterTestCombatPhysicsSceneBridge bridge);
            GameObject markerObject = new GameObject("Full Data Floor");
            try
            {
                markerObject.transform.SetParent(root.transform, worldPositionStays: false);
                CharacterTestCombatPhysicsAabbMarker marker =
                    markerObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(marker, "_bodyId", 61710);
                SetPrivateField(marker, "_colliderId", 1);
                SetPrivateField(marker, "_surfaceMaterialKind", CombatSurfaceMaterialKind.HardSlick);

                CharacterTestCombatLevelPreviewModel model =
                    CharacterTestCombatLevelPreviewModel.Build(bridge);

                Assert.AreEqual(1, model.ColliderCount);
                Assert.AreEqual("body.61710.collider.1", model.Colliders[0].StableId);
                Assert.AreEqual(1, model.Colliders[0].Layer);
                Assert.AreEqual(CombatSurfaceMaterialKind.HardSlick, model.Colliders[0].SurfaceMaterialKind);
                Assert.AreEqual("hard_slick", model.Colliders[0].SurfaceMaterialStableId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateBridgeRoot(
            string name,
            out CharacterTestCombatPhysicsSceneBridge bridge)
        {
            var root = new GameObject(name);
            bridge = root.AddComponent<CharacterTestCombatPhysicsSceneBridge>();
            return root;
        }

        private static void AssertHasIssue(
            CharacterTestCombatLevelPreviewModel model,
            string code)
        {
            for (int i = 0; i < model.Issues.Count; i++)
            {
                if (model.Issues[i].Code == code)
                {
                    return;
                }
            }

            Assert.Fail("Expected preview diagnostic code: " + code);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "Missing private field: " + fieldName);
            field.SetValue(target, value);
        }
    }
}
