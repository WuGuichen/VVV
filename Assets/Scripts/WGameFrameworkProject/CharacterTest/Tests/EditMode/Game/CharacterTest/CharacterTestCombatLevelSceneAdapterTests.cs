using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Combat.Core;
using MxFramework.Combat.Levels;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestCombatLevelSceneAdapterTests
    {
        [Test]
        public void BuildLevel_PreservesAabbMarkerGeometryIdsLayerAndSurfaceMaterial()
        {
            var root = new GameObject("CharacterTest Level Adapter Root");
            var markerObject = new GameObject("Elastic Floor");
            try
            {
                root.transform.position = new Vector3(7f, 8f, 9f);
                markerObject.transform.SetParent(root.transform, worldPositionStays: false);
                markerObject.transform.position = new Vector3(1f, 2f, 3f);
                markerObject.transform.localScale = new Vector3(4f, 2f, 6f);
                CharacterTestCombatPhysicsAabbMarker marker =
                    markerObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(marker, "_combatLayer", 6);
                SetPrivateField(marker, "_bodyId", 321);
                SetPrivateField(marker, "_colliderId", 7);
                SetPrivateField(marker, "_surfaceMaterialKind", CombatSurfaceMaterialKind.Elastic);
                SetPrivateField(marker, "_surfaceFriction", 19f);
                SetPrivateField(marker, "_surfaceElasticity", 0.75f);

                CharacterTestCombatLevelSceneAdapterResult result =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, new[] { marker });

                Assert.IsFalse(result.HasErrors);
                Assert.AreEqual(1, result.StaticColliderCount);
                Assert.AreEqual(1, result.Definition.SpawnPoints.Count);
                Assert.AreEqual("spawn.default", result.Definition.SpawnPoints[0].StableId);
                Assert.AreEqual(V(7, 8, 9), result.Definition.SpawnPoints[0].Position);
                CombatLevelAabbColliderDefinition collider = result.Definition.AabbColliders[0];
                Assert.AreEqual("body.321.collider.7", collider.StableId);
                Assert.AreEqual(321, collider.EntityId);
                Assert.AreEqual(321, collider.BodyId);
                Assert.AreEqual(7, collider.ColliderId);
                Assert.AreEqual(6, collider.Layer);
                Assert.AreEqual(V(1, 2, 3), collider.Position);
                Assert.AreEqual(V(-2, -1, -3), collider.LocalMin);
                Assert.AreEqual(V(2, 1, 3), collider.LocalMax);
                CombatSurfaceMaterial material = collider.SurfaceMaterial.ToSurfaceMaterial();
                Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, material.Kind);
                Assert.AreEqual(Fix64.FromInt(19), material.Friction);
                Assert.AreEqual(Fix64.FromRatio(75, 100), material.Elasticity);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildLevel_HashIsStableWhenMarkerCollectionOrderChanges()
        {
            var root = new GameObject("CharacterTest Stable Level Root");
            var firstObject = new GameObject("Floor A");
            var secondObject = new GameObject("Floor B");
            try
            {
                firstObject.transform.SetParent(root.transform, worldPositionStays: false);
                secondObject.transform.SetParent(root.transform, worldPositionStays: false);
                firstObject.transform.position = new Vector3(1f, 0f, 0f);
                secondObject.transform.position = new Vector3(2f, 0f, 0f);
                CharacterTestCombatPhysicsAabbMarker first =
                    firstObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                CharacterTestCombatPhysicsAabbMarker second =
                    secondObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();

                CharacterTestCombatLevelSceneAdapterResult natural =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, new[] { first, second });
                CharacterTestCombatLevelSceneAdapterResult reversed =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, new[] { second, first });

                Assert.IsFalse(natural.HasErrors);
                Assert.IsFalse(reversed.HasErrors);
                Assert.AreEqual(natural.ContentHash, reversed.ContentHash);
                Assert.AreEqual(
                    natural.Definition.AabbColliders[0].StableId,
                    reversed.Definition.AabbColliders[0].StableId);
                Assert.AreEqual(
                    natural.Definition.AabbColliders[0].BodyId,
                    reversed.Definition.AabbColliders[0].BodyId);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildLevel_ExplicitIdsKeepHashStableWhenHierarchyChanges()
        {
            var firstRoot = new GameObject("CharacterTest Explicit Id Level");
            var secondRoot = new GameObject("CharacterTest Explicit Id Level");
            var firstGroup = new GameObject("Group A");
            var secondGroup = new GameObject("Group B");
            var firstMarkerObject = new GameObject("Floor");
            var secondMarkerObject = new GameObject("Floor");
            try
            {
                firstGroup.transform.SetParent(firstRoot.transform, worldPositionStays: false);
                secondGroup.transform.SetParent(secondRoot.transform, worldPositionStays: false);
                firstMarkerObject.transform.SetParent(firstGroup.transform, worldPositionStays: false);
                secondMarkerObject.transform.SetParent(secondGroup.transform, worldPositionStays: false);
                firstMarkerObject.transform.position = new Vector3(1f, 2f, 3f);
                secondMarkerObject.transform.position = new Vector3(1f, 2f, 3f);
                firstMarkerObject.transform.localScale = new Vector3(4f, 2f, 6f);
                secondMarkerObject.transform.localScale = new Vector3(4f, 2f, 6f);

                CharacterTestCombatPhysicsAabbMarker first =
                    firstMarkerObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                CharacterTestCombatPhysicsAabbMarker second =
                    secondMarkerObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(first, "_bodyId", 321);
                SetPrivateField(first, "_colliderId", 7);
                SetPrivateField(second, "_bodyId", 321);
                SetPrivateField(second, "_colliderId", 7);

                CharacterTestCombatLevelSceneAdapterResult firstResult =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(firstRoot.transform, new[] { first });
                CharacterTestCombatLevelSceneAdapterResult secondResult =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(secondRoot.transform, new[] { second });

                Assert.IsFalse(firstResult.HasErrors);
                Assert.IsFalse(secondResult.HasErrors);
                Assert.AreEqual("body.321.collider.7", firstResult.Definition.AabbColliders[0].StableId);
                Assert.AreEqual(firstResult.ContentHash, secondResult.ContentHash);
            }
            finally
            {
                Object.DestroyImmediate(firstRoot);
                Object.DestroyImmediate(secondRoot);
            }
        }

        [TestCase(15f)]
        [TestCase(30f)]
        [TestCase(50f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void BuildLevel_RotatedBoxCompilesObbWithQuantizedMatrixBasis(float angle)
        {
            var root = new GameObject("CharacterTest Rotated Marker Root");
            var markerObject = new GameObject("Rotated Floor");
            try
            {
                markerObject.transform.SetParent(root.transform, worldPositionStays: false);
                markerObject.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
                markerObject.transform.localScale = new Vector3(4f, 2f, 6f);
                CharacterTestCombatPhysicsAabbMarker marker =
                    markerObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(marker, "_bodyId", 456);
                SetPrivateField(marker, "_colliderId", 7);
                SetPrivateField(marker, "_combatLayer", 6);
                SetPrivateField(marker, "_surfaceMaterialKind", CombatSurfaceMaterialKind.Rough);
                SetPrivateField(marker, "_surfaceFriction", 17f);

                CharacterTestCombatLevelSceneAdapterResult result =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, new[] { marker });

                Assert.IsFalse(result.HasErrors);
                Assert.AreEqual(0, result.AabbColliderCount);
                Assert.AreEqual(1, result.ObbColliderCount);
                Assert.AreEqual(1, result.StaticColliderCount);
                Assert.IsFalse(HasIssue(result.AdapterReport, CharacterTestCombatLevelSceneAdapter.UnsupportedRotationCode));
                CombatLevelObbColliderDefinition obb = result.Definition.ObbColliders[0];
                Matrix4x4 matrix = markerObject.transform.localToWorldMatrix;
                Assert.AreEqual(ToLevelAxis(matrix.GetColumn(0)), obb.AxisX);
                Assert.AreEqual(ToLevelAxis(matrix.GetColumn(1)), obb.AxisY);
                Assert.AreEqual(ToLevelAxis(matrix.GetColumn(2)), obb.AxisZ);
                Assert.AreEqual(V(2, 1, 3), obb.HalfExtents);
                Assert.AreEqual("body.456.collider.7", obb.StableId);
                Assert.AreEqual(456, obb.BodyId);
                Assert.AreEqual(7, obb.ColliderId);
                Assert.AreEqual(6, obb.Layer);
                Assert.AreEqual(CombatSurfaceMaterialKind.Rough, obb.SurfaceMaterial.Kind);
                Assert.AreEqual(Fix64.FromInt(17), obb.SurfaceMaterial.ToSurfaceMaterial().Friction);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildLevel_RotationChangesCompiledHashAndIdentityRemainsAabb()
        {
            var root = new GameObject("CharacterTest Hash Rotation Root");
            var markerObject = new GameObject("Floor");
            try
            {
                markerObject.transform.SetParent(root.transform, false);
                markerObject.transform.localScale = new Vector3(4f, 2f, 6f);
                CharacterTestCombatPhysicsAabbMarker marker = markerObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(marker, "_bodyId", 500);
                SetPrivateField(marker, "_colliderId", 1);

                CharacterTestCombatLevelSceneAdapterResult identity =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, new[] { marker });
                markerObject.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
                CharacterTestCombatLevelSceneAdapterResult rotated =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, new[] { marker });

                Assert.AreEqual(1, identity.AabbColliderCount);
                Assert.AreEqual(0, identity.ObbColliderCount);
                Assert.AreEqual(0, rotated.AabbColliderCount);
                Assert.AreEqual(1, rotated.ObbColliderCount);
                Assert.AreNotEqual(identity.ContentHash, rotated.ContentHash);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildLevel_DefinitionScaleIgnoresTransformScaleButKeepsWorldBasis()
        {
            var root = new GameObject("CharacterTest Definition Scale Root");
            var source = root.AddComponent<CharacterTestWorldObjectSceneSource>();
            var box = new GameObject("Definition Box");
            try
            {
                box.transform.SetParent(root.transform, false);
                box.transform.localScale = new Vector3(7f, 8f, 9f);
                box.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
                source.Configure(
                    new CharacterTestWorldObjectSceneSource.MovableInstance[0],
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            box.transform,
                            600,
                            2,
                            CharacterTestWorldObjectInstanceScaleSource.Definition)
                    });

                CharacterTestCombatLevelSceneAdapterResult result =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, source.CollectStaticColliderSpecs(out _));
                CharacterTestWorldObjectSceneSource.BuiltInCatalog.TryGetDefinition(
                    CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                    out CharacterTestWorldObjectDefinition definition);

                Assert.IsFalse(result.HasErrors);
                CombatLevelObbColliderDefinition obb = result.Definition.ObbColliders[0];
                Assert.AreEqual(
                    R(
                        definition.Shape.HalfExtentXMillimeters * 1000L,
                        definition.Shape.HalfExtentYMillimeters * 1000L,
                        definition.Shape.HalfExtentZMillimeters * 1000L),
                    obb.HalfExtents);
                Assert.AreEqual(ToLevelAxis(box.transform.localToWorldMatrix.GetColumn(0)), obb.AxisX);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(0f, 1f, 1f, CharacterTestCombatLevelSceneAdapter.InvalidTransformScaleCode)]
        [TestCase(-1f, 1f, 1f, CharacterTestCombatLevelSceneAdapter.InvalidTransformScaleCode)]
        [TestCase(-1f, -1f, 1f, CharacterTestCombatLevelSceneAdapter.InvalidTransformScaleCode)]
        [TestCase(-1f, -1f, -1f, CharacterTestCombatLevelSceneAdapter.InvalidTransformScaleCode)]
        [TestCase(0.000001f, 1f, 1f, CharacterTestCombatLevelSceneAdapter.InvalidTransformScaleCode)]
        public void BuildLevel_InvalidAncestorScaleFailsClosed(
            float x,
            float y,
            float z,
            string expectedCode)
        {
            var ancestor = new GameObject("Ancestor");
            var root = new GameObject("Root");
            var markerObject = new GameObject("Box");
            try
            {
                root.transform.SetParent(ancestor.transform, false);
                markerObject.transform.SetParent(root.transform, false);
                ancestor.transform.localScale = new Vector3(x, y, z);
                CharacterTestCombatPhysicsAabbMarker marker = markerObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(marker, "_bodyId", 700);
                SetPrivateField(marker, "_colliderId", 3);

                CharacterTestCombatLevelSceneAdapterResult result =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, new[] { marker });

                Assert.IsTrue(result.HasErrors, "Expected typed transform error: " + expectedCode);
                Assert.AreEqual(0, result.StaticColliderCount);
                AssertIssue(result.AdapterReport, expectedCode, "$.staticColliderSpecs['body.700.collider.3'].transform");
            }
            finally
            {
                Object.DestroyImmediate(ancestor);
            }
        }

        [Test]
        public void TransformBasisCore_NonFiniteInputsReturnFixedTypedCode()
        {
            string expected = CharacterTestCombatLevelSceneAdapter.InvalidTransformNonFiniteCode;
            Assert.AreEqual(
                expected,
                CharacterTestCombatLevelSceneAdapter.ValidateTransformBasisForTests(
                    new Vector3(float.NaN, 0f, 0f),
                    Vector3.right,
                    Vector3.up,
                    Vector3.forward));
            Assert.AreEqual(
                expected,
                CharacterTestCombatLevelSceneAdapter.ValidateTransformBasisForTests(
                    Vector3.zero,
                    new Vector3(float.PositiveInfinity, 0f, 0f),
                    Vector3.up,
                    Vector3.forward));
            Assert.AreEqual(
                expected,
                CharacterTestCombatLevelSceneAdapter.ValidateTransformBasisForTests(
                    Vector3.zero,
                    new Vector3(float.MaxValue, float.MaxValue, 0f),
                    Vector3.up,
                    Vector3.forward));
        }

        [Test]
        public void BuildLevel_RealAncestorShearFailsClosedWithTypedError()
        {
            AssertInvalidHierarchy(
                ancestor =>
                {
                    ancestor.localScale = new Vector3(2f, 1f, 1f);
                    ancestor.GetChild(0).localRotation = Quaternion.Euler(0f, 30f, 0f);
                },
                CharacterTestCombatLevelSceneAdapter.InvalidTransformShearCode);
        }

        [Test]
        public void QuantizationCore_RejectsExtremeFiniteComponentsWithoutSaturating()
        {
            Assert.IsFalse(CharacterTestCombatLevelSceneAdapter.TryQuantizeVectorForTests(
                new Vector3(1e13f, 0f, 0f), out _));
            Assert.IsFalse(CharacterTestCombatLevelSceneAdapter.TryQuantizeVectorForTests(
                new Vector3(float.MaxValue, 0f, 0f), out _));
            Assert.IsTrue(CharacterTestCombatLevelSceneAdapter.TryQuantizeVectorForTests(
                new Vector3(1.25f, -2.5f, 3.75f), out FixVector3 valid));
            Assert.AreEqual(
                new FixVector3(Fix64.FromRaw(1250000), Fix64.FromRaw(-2500000), Fix64.FromRaw(3750000)),
                valid);
        }

        [Test]
        public void BuildLevel_ExtremeFinitePositionFailsQuantizationWithoutShape()
        {
            var root = new GameObject("CharacterTest Quantization Root");
            var box = new GameObject("Extreme Position Box");
            try
            {
                box.transform.SetParent(root.transform, false);
                box.transform.position = new Vector3(1e13f, 0f, 0f);
                CharacterTestCombatPhysicsAabbMarker marker = box.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(marker, "_bodyId", 720);
                SetPrivateField(marker, "_colliderId", 8);

                CharacterTestCombatLevelSceneAdapterResult result =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, new[] { marker });

                Assert.IsTrue(result.HasErrors);
                Assert.AreEqual(0, result.StaticColliderCount);
                AssertIssue(
                    result.AdapterReport,
                    CharacterTestCombatLevelSceneAdapter.InvalidTransformNonFiniteCode,
                    "$.staticColliderSpecs['body.720.collider.8'].transform");
                StringAssert.Contains("reason=quantization-range", result.AdapterReport.Issues[0].Message);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void QuantizedBasisCore_RejectsFloatOrthogonalFixtureBeyondRawTolerance()
        {
            Vector3 axisX = new Vector3(1.000016f, 0.001f, 0f);
            Vector3 axisY = new Vector3(-0.001f, 1.000016f, 0f);
            Vector3 axisZ = Vector3.forward;
            Assert.LessOrEqual(Mathf.Abs(Vector3.Dot(axisX, axisY)), 0.00001f);

            Assert.AreEqual(
                CharacterTestCombatLevelSceneAdapter.InvalidQuantizedObbAxesCode,
                CharacterTestCombatLevelSceneAdapter.ValidateQuantizedBasisForTests(axisX, axisY, axisZ));
        }

        [Test]
        public void BuildLevel_RotatedSceneCachePreservesScaleSourceAxesAndExtents()
        {
            var root = new GameObject("CharacterTest Rotated Cache Root");
            var box = new GameObject("Cached Box");
            var cache = new CharacterTestWorldObjectSceneCache();
            try
            {
                box.transform.SetParent(root.transform, false);
                box.transform.localScale = new Vector3(4f, 2f, 6f);
                box.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
                cache.Configure(
                    new CharacterTestWorldObjectSceneSource.MovableInstance[0],
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            box.transform,
                            800,
                            5,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    });

                CharacterTestWorldObjectStaticColliderSpec[] specs = cache.CollectStaticColliderSpecs(out _);
                CharacterTestCombatLevelSceneAdapterResult result =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, cache);

                Assert.AreEqual(1, specs.Length);
                Assert.AreEqual(CharacterTestWorldObjectInstanceScaleSource.SceneTransform, specs[0].ScaleSource);
                Assert.IsFalse(result.HasErrors);
                Assert.AreEqual(1, result.ObbColliderCount);
                CombatLevelObbColliderDefinition obb = result.Definition.ObbColliders[0];
                Assert.AreEqual(V(2, 1, 3), obb.HalfExtents);
                Assert.AreEqual(ToLevelAxis(box.transform.localToWorldMatrix.GetColumn(0)), obb.AxisX);
                Assert.AreEqual(ToLevelAxis(box.transform.localToWorldMatrix.GetColumn(1)), obb.AxisY);
                Assert.AreEqual(ToLevelAxis(box.transform.localToWorldMatrix.GetColumn(2)), obb.AxisZ);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildRuntime_ObbOnlyLevelIncludesObbLayerInObstacleMask()
        {
            var root = new GameObject("CharacterTest OBB Mask Root");
            CharacterTestCombatPhysicsSceneBridge bridge = root.AddComponent<CharacterTestCombatPhysicsSceneBridge>();
            var box = new GameObject("OBB Only");
            try
            {
                box.transform.SetParent(root.transform, false);
                box.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
                CharacterTestCombatPhysicsAabbMarker marker = box.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(marker, "_bodyId", 900);
                SetPrivateField(marker, "_colliderId", 6);
                SetPrivateField(marker, "_combatLayer", 12);

                CharacterTestCharacterPhysicsRuntime runtime = bridge.BuildRuntime();

                Assert.NotNull(runtime);
                Assert.AreEqual(1, runtime.StaticColliderCount);
                Assert.IsTrue(runtime.StaticObstacleLayerMask.ContainsLayer(12));
                Assert.IsTrue(runtime.World.TryGetObbCollider(
                    new CombatBodyId(900),
                    new CombatColliderId(6),
                    out CombatPhysicsObbCollider obb));
                Assert.AreEqual(12, obb.Layer);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildRuntime_AppendsSphereAndCapsuleMarkersToPhysicsWorld()
        {
            var root = new GameObject("CharacterTest Shape Marker Root");
            CharacterTestCombatPhysicsSceneBridge bridge =
                root.AddComponent<CharacterTestCombatPhysicsSceneBridge>();
            var floorObject = new GameObject("Aabb Floor");
            var sphereObject = new GameObject("Sphere Target");
            var capsuleObject = new GameObject("Capsule Target");
            try
            {
                floorObject.transform.SetParent(root.transform, worldPositionStays: false);
                sphereObject.transform.SetParent(root.transform, worldPositionStays: false);
                capsuleObject.transform.SetParent(root.transform, worldPositionStays: false);
                floorObject.transform.position = new Vector3(0f, -1f, 0f);
                floorObject.transform.localScale = new Vector3(4f, 1f, 4f);
                sphereObject.transform.position = new Vector3(2f, 0f, 0f);
                sphereObject.transform.localScale = new Vector3(2f, 2f, 2f);
                capsuleObject.transform.position = new Vector3(4f, 0f, 0f);
                capsuleObject.transform.localScale = new Vector3(1f, 2f, 1f);

                CharacterTestCombatPhysicsAabbMarker floor =
                    floorObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                CharacterTestCombatPhysicsSphereMarker sphere =
                    sphereObject.AddComponent<CharacterTestCombatPhysicsSphereMarker>();
                CharacterTestCombatPhysicsCapsuleMarker capsule =
                    capsuleObject.AddComponent<CharacterTestCombatPhysicsCapsuleMarker>();
                SetPrivateField(floor, "_bodyId", 12000);
                SetPrivateField(floor, "_colliderId", 1);
                SetPrivateField(sphere, "_combatLayer", 5);
                SetPrivateField(sphere, "_bodyId", 12000);
                SetPrivateField(sphere, "_colliderId", 3);
                SetPrivateField(sphere, "_surfaceMaterialKind", CombatSurfaceMaterialKind.Elastic);
                SetPrivateField(sphere, "_surfaceFriction", 21f);
                SetPrivateField(capsule, "_combatLayer", 6);
                SetPrivateField(capsule, "_bodyId", 12002);
                SetPrivateField(capsule, "_colliderId", 4);
                SetPrivateField(capsule, "_surfaceMaterialKind", CombatSurfaceMaterialKind.Rough);
                SetPrivateField(capsule, "_surfaceFriction", 33f);

                CharacterTestCharacterPhysicsRuntime runtime = bridge.BuildRuntime();

                Assert.NotNull(runtime);
                Assert.AreEqual(3, runtime.StaticColliderCount);
                StringAssert.Contains("sceneShapeColliders=2", runtime.SourceDescription);
                Assert.IsTrue(runtime.World.TryGetBody(new CombatBodyId(12000), out CombatPhysicsBody sharedBody));
                Assert.AreEqual(new FixVector3(Fix64.Zero, -Fix64.One, Fix64.Zero), sharedBody.Position);
                Assert.IsTrue(runtime.World.TryGetSphereCollider(
                    new CombatBodyId(12000),
                    new CombatColliderId(3),
                    out CombatPhysicsSphereCollider sphereCollider));
                Assert.AreEqual(5, sphereCollider.Layer);
                Assert.AreEqual(new FixVector3(Fix64.FromInt(2), Fix64.One, Fix64.Zero), sphereCollider.LocalCenter);
                Assert.AreEqual(Fix64.One, sphereCollider.Radius);
                Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, sphereCollider.SurfaceMaterialKind);
                Assert.AreEqual(Fix64.FromInt(21), sphereCollider.SurfaceFriction);
                Assert.IsTrue(runtime.World.TryGetCapsuleCollider(
                    new CombatBodyId(12002),
                    new CombatColliderId(4),
                    out CombatPhysicsCapsuleCollider capsuleCollider));
                Assert.AreEqual(6, capsuleCollider.Layer);
                Assert.AreEqual(Fix64.Half, capsuleCollider.Radius);
                Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.FromRatio(-3, 2), Fix64.Zero), capsuleCollider.LocalPointA);
                Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.FromRatio(3, 2), Fix64.Zero), capsuleCollider.LocalPointB);
                Assert.AreEqual(CombatSurfaceMaterialKind.Rough, capsuleCollider.SurfaceMaterialKind);

                var hits = new List<CombatQueryResult>();
                CombatPhysicsLayerMask mask = CombatPhysicsLayerMask.FromLayer(5).AddLayer(6);
                int hitCount = runtime.World.Query(CombatPhysicsQuery.From(new CombatAabbQuery(
                    Header(CombatQueryKind.Aabb, mask),
                    new FixVector3(Fix64.One, Fix64.FromInt(-2), -Fix64.One),
                    new FixVector3(Fix64.FromInt(5), Fix64.FromInt(2), Fix64.One))), hits);

                Assert.AreEqual(2, hitCount);
                AssertHasTargetShape(hits, 12000, 3, CombatPhysicsShapeKind.Sphere);
                AssertHasTargetShape(hits, 12002, 4, CombatPhysicsShapeKind.Capsule);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildRuntime_UsesWorldObjectSceneSourceBeforePerObjectMarkers()
        {
            var root = new GameObject("CharacterTest Source Level Root");
            CharacterTestCombatPhysicsSceneBridge bridge =
                root.AddComponent<CharacterTestCombatPhysicsSceneBridge>();
            CharacterTestWorldObjectSceneSource source =
                root.AddComponent<CharacterTestWorldObjectSceneSource>();
            var floorObject = new GameObject("Aabb Floor");
            var sphereObject = new GameObject("Sphere Target");
            var capsuleObject = new GameObject("Capsule Target");
            try
            {
                floorObject.transform.SetParent(root.transform, worldPositionStays: false);
                sphereObject.transform.SetParent(root.transform, worldPositionStays: false);
                capsuleObject.transform.SetParent(root.transform, worldPositionStays: false);
                floorObject.transform.position = new Vector3(0f, -1f, 0f);
                floorObject.transform.localScale = new Vector3(4f, 1f, 4f);
                sphereObject.transform.position = new Vector3(2f, 0f, 0f);
                sphereObject.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                capsuleObject.transform.position = new Vector3(4f, 0f, 0f);
                capsuleObject.transform.localScale = new Vector3(1f, 2f, 1f);

                source.Configure(
                    new CharacterTestWorldObjectSceneSource.MovableInstance[0],
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            floorObject.transform,
                            12000,
                            1,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform),
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticSphereProbe,
                            sphereObject.transform,
                            12000,
                            3,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform),
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticCapsuleProbe,
                            capsuleObject.transform,
                            12002,
                            4,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    });

                CharacterTestCharacterPhysicsRuntime runtime = bridge.BuildRuntime();

                Assert.NotNull(runtime);
                Assert.AreEqual(3, runtime.StaticColliderCount);
                StringAssert.Contains("sceneShapeColliders=2", runtime.SourceDescription);
                Assert.IsTrue(runtime.World.TryGetBody(new CombatBodyId(12000), out CombatPhysicsBody sharedBody));
                Assert.AreEqual(new FixVector3(Fix64.Zero, -Fix64.One, Fix64.Zero), sharedBody.Position);
                Assert.IsTrue(runtime.World.TryGetSphereCollider(
                    new CombatBodyId(12000),
                    new CombatColliderId(3),
                    out CombatPhysicsSphereCollider sphereCollider));
                Assert.AreEqual(1, sphereCollider.Layer);
                Assert.AreEqual(new FixVector3(Fix64.FromInt(2), Fix64.One, Fix64.Zero), sphereCollider.LocalCenter);
                Assert.AreEqual(Fix64.FromRatio(3, 4), sphereCollider.Radius);
                Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, sphereCollider.SurfaceMaterialKind);
                Assert.AreEqual(Fix64.FromInt(8), sphereCollider.SurfaceFriction);
                Assert.IsTrue(runtime.World.TryGetCapsuleCollider(
                    new CombatBodyId(12002),
                    new CombatColliderId(4),
                    out CombatPhysicsCapsuleCollider capsuleCollider));
                Assert.AreEqual(1, capsuleCollider.Layer);
                Assert.AreEqual(Fix64.Half, capsuleCollider.Radius);
                Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.FromRatio(-3, 2), Fix64.Zero), capsuleCollider.LocalPointA);
                Assert.AreEqual(new FixVector3(Fix64.Zero, Fix64.FromRatio(3, 2), Fix64.Zero), capsuleCollider.LocalPointB);
                Assert.AreEqual(CombatSurfaceMaterialKind.Rough, capsuleCollider.SurfaceMaterialKind);
                Assert.AreEqual(Fix64.FromInt(20), capsuleCollider.SurfaceFriction);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildLevel_UsesWorldObjectLevelPointsWhenSourceCacheIsPresent()
        {
            var root = new GameObject("CharacterTest Point Source Root");
            CharacterTestCombatPhysicsSceneBridge bridge =
                root.AddComponent<CharacterTestCombatPhysicsSceneBridge>();
            CharacterTestWorldObjectSceneSource source =
                root.AddComponent<CharacterTestWorldObjectSceneSource>();
            GameObject floorObject = new GameObject("Aabb Floor");
            GameObject playerPoint = new GameObject("LevelPoint_PlayerSpawn");
            GameObject aiPoint = new GameObject("LevelPoint_AiSpawn");
            try
            {
                floorObject.transform.SetParent(root.transform, worldPositionStays: false);
                playerPoint.transform.SetParent(root.transform, worldPositionStays: false);
                aiPoint.transform.SetParent(root.transform, worldPositionStays: false);
                floorObject.transform.position = new Vector3(0f, -1f, 0f);
                floorObject.transform.localScale = new Vector3(4f, 1f, 4f);
                playerPoint.transform.position = new Vector3(2f, 1f, 3f);
                aiPoint.transform.position = new Vector3(4f, 1f, 5f);

                source.Configure(
                    new CharacterTestWorldObjectSceneSource.MovableInstance[0],
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            floorObject.transform,
                            12000,
                            1,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    },
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.LevelPointInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn,
                            "spawn.player",
                            playerPoint.transform),
                        new CharacterTestWorldObjectSceneSource.LevelPointInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.PointAiSpawn,
                            "spawn.ai",
                            aiPoint.transform)
                    },
                    new CharacterTestWorldObjectSceneSource.LevelRegionInstance[0]);

                CharacterTestCombatLevelSceneAdapterResult result =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(bridge);

                Assert.IsFalse(result.HasErrors);
                Assert.AreEqual(2, result.Definition.SpawnPoints.Count);
                AssertHasPoint(result.Definition.SpawnPoints, "spawn.player", V(2, 1, 3));
                AssertHasPoint(result.Definition.SpawnPoints, "spawn.ai", V(4, 1, 5));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildLevel_UsesGameManagerSceneCacheLevelPoints()
        {
            var root = new GameObject("CharacterTest GameManager Cache Root");
            var cache = new CharacterTestWorldObjectSceneCache();
            GameObject floorObject = new GameObject("Aabb Floor");
            GameObject playerPoint = new GameObject("LevelPoint_PlayerSpawn");
            GameObject aiPoint = new GameObject("LevelPoint_AiSpawn");
            try
            {
                floorObject.transform.SetParent(root.transform, worldPositionStays: false);
                playerPoint.transform.SetParent(root.transform, worldPositionStays: false);
                aiPoint.transform.SetParent(root.transform, worldPositionStays: false);
                floorObject.transform.position = new Vector3(0f, -1f, 0f);
                floorObject.transform.localScale = new Vector3(4f, 1f, 4f);
                playerPoint.transform.position = new Vector3(2f, 1f, 3f);
                aiPoint.transform.position = new Vector3(4f, 1f, 5f);

                cache.Configure(
                    new CharacterTestWorldObjectSceneSource.MovableInstance[0],
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            floorObject.transform,
                            12000,
                            1,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    },
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.LevelPointInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn,
                            "spawn.player",
                            playerPoint.transform),
                        new CharacterTestWorldObjectSceneSource.LevelPointInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.PointAiSpawn,
                            "spawn.ai",
                            aiPoint.transform)
                    },
                    new CharacterTestWorldObjectSceneSource.LevelRegionInstance[0]);

                CharacterTestCombatLevelSceneAdapterResult result =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, cache);

                Assert.IsFalse(result.HasErrors);
                Assert.AreEqual(2, result.Definition.SpawnPoints.Count);
                AssertHasPoint(result.Definition.SpawnPoints, "spawn.player", V(2, 1, 3));
                AssertHasPoint(result.Definition.SpawnPoints, "spawn.ai", V(4, 1, 5));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static CombatLevelFixedVector3 V(int x, int y, int z)
        {
            return CombatLevelFixedVector3.FromFixVector3(new FixVector3(
                Fix64.FromInt(x),
                Fix64.FromInt(y),
                Fix64.FromInt(z)));
        }

        private static CombatLevelFixedVector3 R(long x, long y, long z) =>
            new CombatLevelFixedVector3(x, y, z);

        private static CombatLevelFixedVector3 ToLevelAxis(Vector4 column)
        {
            Vector3 axis = new Vector3(column.x, column.y, column.z).normalized;
            return CombatLevelFixedVector3.FromFixVector3(
                CharacterTestCombatPhysicsSceneBridge.ToFixVector3(axis));
        }

        private static void AssertInvalidHierarchy(Action<Transform> configure, string expectedCode)
        {
            var ancestor = new GameObject("Ancestor");
            var root = new GameObject("Root");
            var markerObject = new GameObject("Box");
            try
            {
                root.transform.SetParent(ancestor.transform, false);
                markerObject.transform.SetParent(root.transform, false);
                configure(ancestor.transform);
                CharacterTestCombatPhysicsAabbMarker marker = markerObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(marker, "_bodyId", 710);
                SetPrivateField(marker, "_colliderId", 4);

                CharacterTestCombatLevelSceneAdapterResult result =
                    CharacterTestCombatLevelSceneAdapter.BuildLevel(root.transform, new[] { marker });

                Assert.IsTrue(result.HasErrors, "Expected typed transform error: " + expectedCode);
                Assert.AreEqual(0, result.StaticColliderCount);
                AssertIssue(result.AdapterReport, expectedCode, "$.staticColliderSpecs['body.710.collider.4'].transform");
            }
            finally
            {
                Object.DestroyImmediate(ancestor);
            }
        }

        private static void AssertIssue(
            CombatLevelValidationReport report,
            string code,
            string path)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code != code)
                    continue;
                Assert.AreEqual(path, report.Issues[i].Path);
                Assert.AreEqual(CombatLevelValidationSeverity.Error, report.Issues[i].Severity);
                StringAssert.Contains("bodyId=", report.Issues[i].Message);
                StringAssert.Contains("colliderId=", report.Issues[i].Message);
                StringAssert.Contains("reason=", report.Issues[i].Message);
                return;
            }
            Assert.Fail("Expected issue: " + code);
        }

        private static bool HasIssue(CombatLevelValidationReport report, string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return true;
            }
            return false;
        }

        private static void AssertHasIssue(CombatLevelValidationReport report, string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                {
                    return;
                }
            }

            Assert.Fail("Expected validation issue code: " + code);
        }

        private static void AssertHasPoint(
            IReadOnlyList<CombatLevelPointDefinition> points,
            string stableId,
            CombatLevelFixedVector3 position)
        {
            for (int i = 0; i < points.Count; i++)
            {
                CombatLevelPointDefinition point = points[i];
                if (point.StableId == stableId)
                {
                    Assert.AreEqual(position, point.Position);
                    return;
                }
            }

            Assert.Fail("Expected level point: " + stableId);
        }

        private static void AssertHasTargetShape(
            IReadOnlyList<CombatQueryResult> hits,
            int bodyId,
            int colliderId,
            CombatPhysicsShapeKind targetShapeKind)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                CombatQueryResult hit = hits[i];
                if (hit.TargetBodyId.Value == bodyId && hit.TargetColliderId.Value == colliderId)
                {
                    Assert.AreEqual(targetShapeKind, hit.TargetShapeKind);
                    return;
                }
            }

            Assert.Fail(
                "Expected hit for body "
                + bodyId
                + " collider "
                + colliderId
                + " shape "
                + targetShapeKind);
        }

        private static CombatQueryHeader Header(
            CombatQueryKind kind,
            CombatPhysicsLayerMask layerMask)
        {
            return new CombatQueryHeader(
                queryId: 1,
                kind,
                new CombatEntityId(1),
                traceId: 5,
                actionId: 6,
                sourceOrder: 7,
                layerMask);
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
