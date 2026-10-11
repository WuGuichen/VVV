using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Levels;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Levels
{
    public sealed class CombatLevelDefinitionTests
    {
        [Test]
        public void Json_RoundTripPreservesLevelDefinition()
        {
            CombatLevelDefinition original = CreateLevel(
                new[]
                {
                    CreateCollider(
                        "floor.elastic",
                        entityId: 10,
                        bodyId: 20,
                        colliderId: 30,
                        layer: 2,
                        material: CombatSurfaceMaterial.Elastic)
                },
                new[]
                {
                    CreatePoint("spawn.default", x: 1, y: 2, z: 3)
                },
                new[]
                {
                    CreatePoint("checkpoint.start", x: 4, y: 5, z: 6)
                });

            string json = CombatLevelJson.SaveToJson(original);
            CombatLevelDefinition restored = CombatLevelJson.LoadFromJson(json);

            StringAssert.Contains("\"kind\": \"Elastic\"", json);
            Assert.AreEqual(original.SchemaVersion, restored.SchemaVersion);
            Assert.AreEqual(original.LevelStableId, restored.LevelStableId);
            Assert.AreEqual(original.DisplayName, restored.DisplayName);
            Assert.AreEqual(original.AabbColliders.Count, restored.AabbColliders.Count);
            Assert.AreEqual(original.AabbColliders[0].StableId, restored.AabbColliders[0].StableId);
            Assert.AreEqual(original.AabbColliders[0].Position, restored.AabbColliders[0].Position);
            Assert.AreEqual(original.AabbColliders[0].SurfaceMaterial, restored.AabbColliders[0].SurfaceMaterial);
            Assert.AreEqual(original.SpawnPoints[0].Position, restored.SpawnPoints[0].Position);
            Assert.AreEqual(original.Checkpoints[0].StableId, restored.Checkpoints[0].StableId);
            Assert.IsFalse(CombatLevelValidator.Validate(restored).HasErrors);
        }

        [Test]
        public void Hash_DoesNotDependOnInputListOrder()
        {
            CombatLevelAabbColliderDefinition floor = CreateCollider(
                "floor.default",
                entityId: 10,
                bodyId: 10,
                colliderId: 1,
                layer: 1,
                material: CombatSurfaceMaterial.Default);
            CombatLevelAabbColliderDefinition wall = CreateCollider(
                "wall.rough",
                entityId: 11,
                bodyId: 11,
                colliderId: 1,
                layer: 1,
                material: CombatSurfaceMaterial.Rough);
            CombatLevelPointDefinition spawnA = CreatePoint("spawn.a", x: 0, y: 1, z: 0);
            CombatLevelPointDefinition spawnB = CreatePoint("spawn.b", x: 4, y: 1, z: 0);
            CombatLevelPointDefinition checkpointA = CreatePoint("checkpoint.a", x: 8, y: 1, z: 0);
            CombatLevelPointDefinition checkpointB = CreatePoint("checkpoint.b", x: 12, y: 1, z: 0);

            CombatLevelDefinition first = CreateLevel(
                new[] { floor, wall },
                new[] { spawnB, spawnA },
                new[] { checkpointB, checkpointA });
            CombatLevelDefinition second = CreateLevel(
                new[] { wall, floor },
                new[] { spawnA, spawnB },
                new[] { checkpointA, checkpointB });

            Assert.AreEqual(
                CombatLevelHash.ComputeContentHash(first),
                CombatLevelHash.ComputeContentHash(second));
        }

        [Test]
        public void Hash_ChangesWhenSurfaceMaterialChanges()
        {
            CombatLevelDefinition elastic = CreateLevel(
                new[]
                {
                    CreateCollider(
                        "floor",
                        entityId: 10,
                        bodyId: 10,
                        colliderId: 1,
                        layer: 1,
                        material: CombatSurfaceMaterial.Elastic)
                },
                new[] { CreatePoint("spawn.default", x: 0, y: 1, z: 0) },
                new CombatLevelPointDefinition[0]);
            CombatLevelDefinition bouncePad = CreateLevel(
                new[]
                {
                    CreateCollider(
                        "floor",
                        entityId: 10,
                        bodyId: 10,
                        colliderId: 1,
                        layer: 1,
                        material: CombatSurfaceMaterial.BouncePad)
                },
                new[] { CreatePoint("spawn.default", x: 0, y: 1, z: 0) },
                new CombatLevelPointDefinition[0]);

            Assert.AreNotEqual(
                CombatLevelHash.ComputeContentHash(elastic),
                CombatLevelHash.ComputeContentHash(bouncePad));
        }

        [Test]
        public void Validator_ReportsSchemaStableIdNumericIdAabbLayerAndSpawnErrors()
        {
            var invalidSurface = new CombatLevelSurfaceMaterialDefinition(
                CombatSurfaceMaterialKind.Elastic,
                frictionRaw: -1,
                elasticityRaw: Fix64.Scale + 1,
                fixedBounceHeightRaw: -1);
            CombatLevelDefinition invalid = new CombatLevelDefinition(
                schemaVersion: CombatLevelDefinition.CurrentSchemaVersion + 1,
                levelStableId: "",
                displayName: "Invalid",
                aabbColliders: new[]
                {
                    CreateCollider(
                        "",
                        entityId: 0,
                        bodyId: 1,
                        colliderId: 1,
                        layer: 99,
                        localMin: V(1, 0, 0),
                        localMax: V(0, 0, 0),
                        surfaceMaterial: invalidSurface),
                    CreateCollider(
                        "duplicate",
                        entityId: 2,
                        bodyId: 1,
                        colliderId: 1,
                        layer: 1,
                        material: CombatSurfaceMaterial.Default),
                    CreateCollider(
                        "duplicate",
                        entityId: 3,
                        bodyId: 3,
                        colliderId: 1,
                        layer: 1,
                        material: CombatSurfaceMaterial.Default)
                },
                spawnPoints: new CombatLevelPointDefinition[0],
                checkpoints: new CombatLevelPointDefinition[0]);

            CombatLevelValidationReport report = CombatLevelValidator.Validate(invalid);

            Assert.IsTrue(report.HasErrors);
            AssertHasIssue(report, CombatLevelValidationCodes.UnsupportedSchemaVersion);
            AssertHasIssue(report, CombatLevelValidationCodes.MissingLevelStableId);
            AssertHasIssue(report, CombatLevelValidationCodes.MissingStableId);
            AssertHasIssue(report, CombatLevelValidationCodes.DuplicateStableId);
            AssertHasIssue(report, CombatLevelValidationCodes.InvalidEntityId);
            AssertHasIssue(report, CombatLevelValidationCodes.DuplicateBodyId);
            AssertHasIssue(report, CombatLevelValidationCodes.DuplicateColliderId);
            AssertHasIssue(report, CombatLevelValidationCodes.InvalidLayer);
            AssertHasIssue(report, CombatLevelValidationCodes.InvalidAabb);
            AssertHasIssue(report, CombatLevelValidationCodes.InvalidSurfaceMaterial);
            AssertHasIssue(report, CombatLevelValidationCodes.MissingSpawnPoint);
        }

        [Test]
        public void ObbDefinition_PreservesRawFieldsAndBuildsPriorityZeroPhysicsValues()
        {
            CombatLevelObbColliderDefinition definition = CreateObb(
                "slope.30",
                entityId: 101,
                bodyId: 201,
                colliderId: 301,
                layer: 4,
                position: V(2, 3, 4),
                localCenter: V(0, 1, 0),
                halfExtents: V(5, 1, 2),
                axisX: R(866025, 0, -500000),
                axisY: R(0, 1000000, 0),
                axisZ: R(500000, 0, 866025),
                surfaceMaterial: CombatLevelSurfaceMaterialDefinition.FromSurfaceMaterial(
                    CombatSurfaceMaterial.Rough));

            CombatPhysicsBody body = definition.ToPhysicsBody();
            CombatPhysicsObbCollider collider = definition.ToObbCollider();

            Assert.AreEqual("slope.30", definition.StableId);
            Assert.AreEqual(V(2, 3, 4), definition.Position);
            Assert.AreEqual(R(866025, 0, -500000), definition.AxisX);
            Assert.AreEqual(new CombatEntityId(101), body.EntityId);
            Assert.AreEqual(new CombatBodyId(201), body.BodyId);
            Assert.AreEqual(V(2, 3, 4).ToFixVector3(), body.Position);
            Assert.AreEqual(new CombatColliderId(301), collider.ColliderId);
            Assert.AreEqual(V(0, 1, 0).ToFixVector3(), collider.LocalCenter);
            Assert.AreEqual(V(5, 1, 2).ToFixVector3(), collider.HalfExtents);
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, collider.SurfaceMaterial.Kind);
            Assert.AreEqual(0, collider.Priority);

            CombatPhysicsAabbCollider aabb = CreateCollider(
                "priority.aabb",
                entityId: 102,
                bodyId: 202,
                colliderId: 302,
                layer: 4,
                material: CombatSurfaceMaterial.Default).ToAabbCollider();
            Assert.AreEqual(0, aabb.Priority);
        }

        [Test]
        public void Definition_ConstructorsCopyBothColliderListsAndCompatibilityConstructorUsesEmptyObbs()
        {
            var aabbs = new List<CombatLevelAabbColliderDefinition>
            {
                CreateCollider("floor", 1, 1, 1, 0, CombatSurfaceMaterial.Default)
            };
            var obbs = new List<CombatLevelObbColliderDefinition>
            {
                CreateValidObb("slope", bodyId: 2, colliderId: 1)
            };
            var spawns = new List<CombatLevelPointDefinition>
            {
                CreatePoint("spawn.default", 0, 1, 0)
            };
            var checkpoints = new List<CombatLevelPointDefinition>();

            var canonical = new CombatLevelDefinition(
                CombatLevelDefinition.CurrentSchemaVersion,
                "level.copy",
                "Copy",
                aabbs,
                obbs,
                spawns,
                checkpoints);
            var compatible = new CombatLevelDefinition(
                CombatLevelDefinition.CurrentSchemaVersion,
                "level.compatible",
                "Compatible",
                aabbs,
                spawns,
                checkpoints);

            aabbs.Clear();
            obbs.Clear();
            spawns.Clear();
            checkpoints.Add(CreatePoint("checkpoint.after", 0, 0, 0));

            Assert.AreEqual(1, canonical.AabbColliders.Count);
            Assert.AreEqual(1, canonical.ObbColliders.Count);
            Assert.AreEqual(1, canonical.SpawnPoints.Count);
            Assert.AreEqual(0, canonical.Checkpoints.Count);
            Assert.AreEqual(0, compatible.ObbColliders.Count);
        }

        [Test]
        public void Validator_UsesFrozenRawAxisBoundaryIssueSets()
        {
            AssertIssueSequence(
                ValidateSingleObb(CreateValidObb(
                    "unit.32",
                    axisX: R(1000016, 0, 0),
                    axisY: R(0, 1000000, 0),
                    axisZ: R(0, 0, 1000000))),
                new string[0],
                new string[0]);

            AssertIssueSequence(
                ValidateSingleObb(CreateValidObb(
                    "unit.33",
                    axisX: R(1000016, 1000, 0),
                    axisY: R(-1000, 1000016, 0),
                    axisZ: R(0, 0, 1000000))),
                new[]
                {
                    CombatLevelValidationCodes.InvalidObbAxis,
                    CombatLevelValidationCodes.InvalidObbAxis
                },
                new[]
                {
                    "$.obbColliders[0].axisX",
                    "$.obbColliders[0].axisY"
                });

            AssertIssueSequence(
                ValidateSingleObb(CreateValidObb(
                    "orthogonality.32",
                    axisX: R(1000000, 0, 0),
                    axisY: R(32, 1000000, 0),
                    axisZ: R(0, 0, 1000000))),
                new string[0],
                new string[0]);

            AssertIssueSequence(
                ValidateSingleObb(CreateValidObb(
                    "orthogonality.33",
                    axisX: R(1000000, 0, 0),
                    axisY: R(33, 1000000, 0),
                    axisZ: R(0, 0, 1000000))),
                new[] { CombatLevelValidationCodes.InvalidObbOrthogonality },
                new[] { "$.obbColliders[0].axisX" });

            AssertIssueSequence(
                ValidateSingleObb(CreateValidObb(
                    "handedness.64",
                    axisX: R(1000000, 0, 0),
                    axisY: R(0, 1000000, 0),
                    axisZ: R(0, 0, 999936))),
                new[] { CombatLevelValidationCodes.InvalidObbAxis },
                new[] { "$.obbColliders[0].axisZ" });

            AssertIssueSequence(
                ValidateSingleObb(CreateValidObb(
                    "handedness.65",
                    axisX: R(1000000, 0, 0),
                    axisY: R(0, 1000000, 0),
                    axisZ: R(0, 0, 999935))),
                new[]
                {
                    CombatLevelValidationCodes.InvalidObbAxis,
                    CombatLevelValidationCodes.InvalidObbHandedness
                },
                new[]
                {
                    "$.obbColliders[0].axisZ",
                    "$.obbColliders[0].axisZ"
                });
        }

        [Test]
        public void Validator_ReportsSafeTypedIssueForAxisRawOverflow()
        {
            CombatLevelValidationReport report = ValidateSingleObb(CreateValidObb(
                "overflow",
                axisX: R(long.MaxValue, 0, 0),
                axisY: R(0, 1000000, 0),
                axisZ: R(0, 0, 1000000)));

            AssertIssueSequence(
                report,
                new[] { CombatLevelValidationCodes.InvalidObbAxis },
                new[] { "$.obbColliders[0].axisX" });
        }

        [Test]
        public void Validator_ReportsTypedIssueWhenHandednessRawArithmeticOverflows()
        {
            CombatLevelValidationReport report = ValidateSingleObb(CreateValidObb(
                "handedness.overflow",
                axisX: R(2000000000L, 0, 0),
                axisY: R(0, 2000000000L, 0),
                axisZ: R(0, 0, 2000000000L)));

            AssertIssueSequence(
                report,
                new[]
                {
                    CombatLevelValidationCodes.InvalidObbAxis,
                    CombatLevelValidationCodes.InvalidObbAxis,
                    CombatLevelValidationCodes.InvalidObbAxis,
                    CombatLevelValidationCodes.InvalidObbAxis
                },
                new[]
                {
                    "$.obbColliders[0].axisX",
                    "$.obbColliders[0].axisY",
                    "$.obbColliders[0].axisZ",
                    "$.obbColliders[0].axisZ"
                });
        }

        [Test]
        public void Validator_ReportsStableNullGeometryIssueOrderAndPaths()
        {
            var obb = new CombatLevelObbColliderDefinition(
                "null.geometry",
                entityId: 1,
                bodyId: 1,
                colliderId: 1,
                layer: 0,
                position: null,
                localCenter: null,
                halfExtents: null,
                axisX: null,
                axisY: null,
                axisZ: null,
                surfaceMaterial: null);

            AssertIssueSequence(
                ValidateSingleObb(obb),
                new[]
                {
                    CombatLevelValidationCodes.InvalidObbPosition,
                    CombatLevelValidationCodes.InvalidObbLocalCenter,
                    CombatLevelValidationCodes.InvalidObbHalfExtents,
                    CombatLevelValidationCodes.InvalidObbAxis,
                    CombatLevelValidationCodes.InvalidObbAxis,
                    CombatLevelValidationCodes.InvalidObbAxis,
                    CombatLevelValidationCodes.InvalidSurfaceMaterial
                },
                new[]
                {
                    "$.obbColliders[0].position",
                    "$.obbColliders[0].localCenter",
                    "$.obbColliders[0].halfExtents",
                    "$.obbColliders[0].axisX",
                    "$.obbColliders[0].axisY",
                    "$.obbColliders[0].axisZ",
                    "$.obbColliders[0].surfaceMaterial"
                });
        }

        [Test]
        public void Validator_ReportsHalfExtentsForNullZeroAndNegativeRawValues()
        {
            CombatLevelFixedVector3[] values =
            {
                null,
                R(0, 1, 1),
                R(-1, 1, 1)
            };
            for (int i = 0; i < values.Length; i++)
            {
                CombatLevelObbColliderDefinition obb = CreateObb(
                    "extent." + i,
                    entityId: 20,
                    bodyId: 20,
                    colliderId: 30,
                    layer: 1,
                    position: V(0, 0, 0),
                    localCenter: V(0, 0, 0),
                    halfExtents: values[i],
                    axisX: R(1000000, 0, 0),
                    axisY: R(0, 1000000, 0),
                    axisZ: R(0, 0, 1000000),
                    surfaceMaterial: CombatLevelSurfaceMaterialDefinition.Default);
                AssertIssueSequence(
                    ValidateSingleObb(obb),
                    new[] { CombatLevelValidationCodes.InvalidObbHalfExtents },
                    new[] { "$.obbColliders[0].halfExtents" });
            }
        }

        [Test]
        public void Validator_ReportsCrossShapeStableBodyAndColliderConflictsInOrder()
        {
            CombatLevelDefinition level = CreateLevel(
                new[]
                {
                    CreateCollider("shared", 10, 20, 30, 1, CombatSurfaceMaterial.Default)
                },
                new[]
                {
                    CreateValidObb("shared", bodyId: 20, colliderId: 30)
                },
                new[] { CreatePoint("spawn.default", 0, 1, 0) },
                new CombatLevelPointDefinition[0]);

            AssertIssueSequence(
                CombatLevelValidator.Validate(level),
                new[]
                {
                    CombatLevelValidationCodes.DuplicateStableId,
                    CombatLevelValidationCodes.DuplicateBodyId,
                    CombatLevelValidationCodes.DuplicateColliderId
                },
                new[]
                {
                    "$.obbColliders[0].stableId",
                    "$.obbColliders[0].bodyId",
                    "$.obbColliders[0].colliderId"
                });
        }

        [Test]
        public void Validator_ReportsObbNumericLayerAndMaterialIssuesInOrder()
        {
            var invalidMaterial = new CombatLevelSurfaceMaterialDefinition(
                CombatSurfaceMaterialKind.Elastic,
                frictionRaw: -1,
                elasticityRaw: Fix64.Scale + 1,
                fixedBounceHeightRaw: -1);
            CombatLevelObbColliderDefinition obb = CreateObb(
                "invalid.numeric",
                entityId: 0,
                bodyId: 0,
                colliderId: 0,
                layer: 32,
                position: V(0, 0, 0),
                localCenter: V(0, 0, 0),
                halfExtents: V(1, 1, 1),
                axisX: R(1000000, 0, 0),
                axisY: R(0, 1000000, 0),
                axisZ: R(0, 0, 1000000),
                surfaceMaterial: invalidMaterial);

            AssertIssueSequence(
                ValidateSingleObb(obb),
                new[]
                {
                    CombatLevelValidationCodes.InvalidEntityId,
                    CombatLevelValidationCodes.InvalidBodyId,
                    CombatLevelValidationCodes.InvalidColliderId,
                    CombatLevelValidationCodes.InvalidLayer,
                    CombatLevelValidationCodes.InvalidSurfaceMaterial
                },
                new[]
                {
                    "$.obbColliders[0].entityId",
                    "$.obbColliders[0].bodyId",
                    "$.obbColliders[0].colliderId",
                    "$.obbColliders[0].layer",
                    "$.obbColliders[0].surfaceMaterial"
                });
        }

        [Test]
        public void Validator_ReportsObbToObbIdentityConflictsInOrder()
        {
            CombatLevelDefinition level = CreateLevel(
                new CombatLevelAabbColliderDefinition[0],
                new[]
                {
                    CreateValidObb("duplicate.obb", bodyId: 20, colliderId: 30),
                    CreateValidObb("duplicate.obb", bodyId: 20, colliderId: 30)
                },
                new[] { CreatePoint("spawn.default", 0, 1, 0) },
                new CombatLevelPointDefinition[0]);

            AssertIssueSequence(
                CombatLevelValidator.Validate(level),
                new[]
                {
                    CombatLevelValidationCodes.DuplicateStableId,
                    CombatLevelValidationCodes.DuplicateBodyId,
                    CombatLevelValidationCodes.DuplicateColliderId
                },
                new[]
                {
                    "$.obbColliders[1].stableId",
                    "$.obbColliders[1].bodyId",
                    "$.obbColliders[1].colliderId"
                });
        }

        [Test]
        public void Json_V2RoundTripPreservesEveryObbRawField()
        {
            CombatLevelObbColliderDefinition obb = CreateObb(
                "obb.roundtrip", 11, 12, 13, 4,
                R(101, 102, 103), R(201, 202, 203), R(301, 302, 303),
                R(866025, 0, -500000), R(0, 1000000, 0), R(500000, 0, 866025),
                CombatLevelSurfaceMaterialDefinition.FromSurfaceMaterial(CombatSurfaceMaterial.Rough));
            CombatLevelDefinition original = CreateLevel(
                new CombatLevelAabbColliderDefinition[0],
                new[] { obb },
                new[] { CreatePoint("spawn.default", 0, 1, 0) },
                new CombatLevelPointDefinition[0]);

            string json = CombatLevelJson.SaveToJson(original);
            CombatLevelObbColliderDefinition restored = CombatLevelJson.LoadFromJson(json).ObbColliders[0];

            Assert.AreEqual(CombatLevelDefinition.CurrentSchemaVersion, JObject.Parse(json)["schemaVersion"].Value<int>());
            Assert.AreEqual(obb.StableId, restored.StableId);
            Assert.AreEqual(obb.EntityId, restored.EntityId);
            Assert.AreEqual(obb.BodyId, restored.BodyId);
            Assert.AreEqual(obb.ColliderId, restored.ColliderId);
            Assert.AreEqual(obb.Layer, restored.Layer);
            Assert.AreEqual(obb.Position, restored.Position);
            Assert.AreEqual(obb.LocalCenter, restored.LocalCenter);
            Assert.AreEqual(obb.HalfExtents, restored.HalfExtents);
            Assert.AreEqual(obb.AxisX, restored.AxisX);
            Assert.AreEqual(obb.AxisY, restored.AxisY);
            Assert.AreEqual(obb.AxisZ, restored.AxisZ);
            Assert.AreEqual(obb.SurfaceMaterial, restored.SurfaceMaterial);
        }

        [Test]
        public void Json_V1MigratesToCanonicalV2EmptyObbAndMatchingHash()
        {
            CombatLevelDefinition canonical = CreateLevel(
                new[] { CreateCollider("floor", 1, 2, 3, 0, CombatSurfaceMaterial.Default) },
                new CombatLevelObbColliderDefinition[0],
                new[] { CreatePoint("spawn.default", 0, 1, 0) },
                new CombatLevelPointDefinition[0]);
            JObject v1 = JObject.Parse(CombatLevelJson.SaveToJson(canonical));
            v1["schemaVersion"] = 1;
            v1.Remove("obbColliders");

            CombatLevelDefinition migrated = CombatLevelJson.LoadFromJson(v1.ToString(Formatting.None));
            string saved = CombatLevelJson.SaveToJson(migrated);

            Assert.AreEqual(2, migrated.SchemaVersion);
            Assert.AreEqual(0, migrated.ObbColliders.Count);
            Assert.AreEqual(CombatLevelHash.ComputeContentHash(canonical), CombatLevelHash.ComputeContentHash(migrated));
            Assert.AreEqual(2, JObject.Parse(saved)["schemaVersion"].Value<int>());
            Assert.IsInstanceOf<JArray>(JObject.Parse(saved)["obbColliders"]);
        }

        [Test]
        public void Json_RejectsRootSchemaUnknownAndV1ObbWithStableExceptions()
        {
            AssertMessage<JsonSerializationException>(() => CombatLevelJson.LoadFromJson("[]"), "Combat level root must be a JSON object.");
            AssertMessage<JsonSerializationException>(() => CombatLevelJson.LoadFromJson("{}"), "Combat level schemaVersion must be an integer.");
            AssertMessage<JsonSerializationException>(() => CombatLevelJson.LoadFromJson("{\"schemaVersion\":\"2\"}"), "Combat level schemaVersion must be an integer.");
            AssertMessage<NotSupportedException>(() => CombatLevelJson.LoadFromJson("{\"schemaVersion\":0}"), "Unsupported Combat level schema version: 0.");
            AssertMessage<NotSupportedException>(() => CombatLevelJson.LoadFromJson("{\"schemaVersion\":3}"), "Unsupported Combat level schema version: 3.");
            AssertMessage<NotSupportedException>(() => CombatLevelJson.LoadFromJson("{\"schemaVersion\":9223372036854775808}"), "Unsupported Combat level schema version: 9223372036854775808.");

            JObject valid = JObject.Parse(CombatLevelJson.SaveToJson(CreateHashLevel(CreateHashObb())));
            valid["unknown"] = 1;
            AssertMessage<JsonSerializationException>(
                () => CombatLevelJson.LoadFromJson(valid.ToString(Formatting.None)),
                "Unknown Combat level property: $.unknown.");

            valid.Remove("unknown");
            valid["schemaVersion"] = 1;
            AssertMessage<JsonSerializationException>(
                () => CombatLevelJson.LoadFromJson(valid.ToString(Formatting.None)),
                "Combat level schema v1 must not contain $.obbColliders.");
        }

        [Test]
        public void Json_RejectsDuplicatePropertiesBeforeSchemaBranchOrDtoConstruction()
        {
            string canonical = JObject.Parse(
                CombatLevelJson.SaveToJson(CreateHashLevel(CreateHashObb())))
                .ToString(Formatting.None);

            string duplicateSchema = canonical.Replace(
                "{\"schemaVersion\":2,",
                "{\"schemaVersion\":1,\"schemaVersion\":2,");
            string duplicateDisplayName = canonical.Replace(
                "\"displayName\":\"Hash Level\",",
                "\"displayName\":\"First\",\"displayName\":\"Hash Level\",");
            string duplicateAxisRaw = canonical.Replace(
                "\"axisX\":{\"xRaw\":1000000,",
                "\"axisX\":{\"xRaw\":999999,\"xRaw\":1000000,");
            string duplicateMaterialRaw = canonical.Replace(
                "\"surfaceMaterial\":{\"kind\":\"Default\",\"frictionRaw\":0,",
                "\"surfaceMaterial\":{\"kind\":\"Default\",\"frictionRaw\":1,\"frictionRaw\":0,");

            AssertDuplicateRejected(duplicateSchema, "schemaVersion");
            AssertDuplicateRejected(duplicateDisplayName, "displayName");
            AssertDuplicateRejected(duplicateAxisRaw, "xRaw");
            AssertDuplicateRejected(duplicateMaterialRaw, "frictionRaw");
        }

        [Test]
        public void Json_RejectsV2ListAndNestedFieldTokenFailuresBeforeConstruction()
        {
            string[] lists = { "aabbColliders", "obbColliders", "spawnPoints", "checkpoints" };
            for (int i = 0; i < lists.Length; i++)
            {
                JObject missing = JObject.Parse(CombatLevelJson.SaveToJson(CreateHashLevel(CreateHashObb())));
                missing.Remove(lists[i]);
                AssertMessage<JsonSerializationException>(
                    () => CombatLevelJson.LoadFromJson(missing.ToString(Formatting.None)),
                    "Combat level $." + lists[i] + " must be a JSON array.");

                JObject invalid = JObject.Parse(CombatLevelJson.SaveToJson(CreateHashLevel(CreateHashObb())));
                invalid[lists[i]] = JValue.CreateNull();
                AssertMessage<JsonSerializationException>(
                    () => CombatLevelJson.LoadFromJson(invalid.ToString(Formatting.None)),
                    "Combat level $." + lists[i] + " must be a JSON array.");
                invalid[lists[i]] = new JObject();
                AssertMessage<JsonSerializationException>(
                    () => CombatLevelJson.LoadFromJson(invalid.ToString(Formatting.None)),
                    "Combat level $." + lists[i] + " must be a JSON array.");
            }

            JObject field = JObject.Parse(CombatLevelJson.SaveToJson(CreateHashLevel(CreateHashObb())));
            JObject axisX = (JObject)field["obbColliders"][0]["axisX"];
            axisX.Remove("xRaw");
            AssertMessage<JsonSerializationException>(
                () => CombatLevelJson.LoadFromJson(field.ToString(Formatting.None)),
                "Path: $.obbColliders[0].axisX.xRaw.");

            field = JObject.Parse(CombatLevelJson.SaveToJson(CreateHashLevel(CreateHashObb())));
            field["obbColliders"][0]["axisX"]["xRaw"] = "1000000";
            AssertMessage<JsonSerializationException>(
                () => CombatLevelJson.LoadFromJson(field.ToString(Formatting.None)),
                "Path: $.obbColliders[0].axisX.xRaw.");

            field = JObject.Parse(CombatLevelJson.SaveToJson(CreateHashLevel(CreateHashObb())));
            field["obbColliders"][0]["mystery"] = 1;
            AssertMessage<JsonSerializationException>(
                () => CombatLevelJson.LoadFromJson(field.ToString(Formatting.None)),
                "Unknown Combat level property: $.obbColliders[0].mystery.");
        }

        [Test]
        public void Json_LoadAndSaveRejectValidatorErrorsWithoutReturningPartialContent()
        {
            JObject invalidJson = JObject.Parse(CombatLevelJson.SaveToJson(CreateHashLevel(CreateHashObb())));
            invalidJson["obbColliders"][0]["stableId"] = "";
            AssertMessage<JsonSerializationException>(
                () => CombatLevelJson.LoadFromJson(invalidJson.ToString(Formatting.None)),
                "Combat level validation failed: MissingStableId at $.obbColliders[0].stableId.");

            CombatLevelDefinition invalidDefinition = new CombatLevelDefinition(
                1,
                "level.invalid",
                "Invalid",
                new CombatLevelAabbColliderDefinition[0],
                new CombatLevelObbColliderDefinition[0],
                new[] { CreatePoint("spawn.default", 0, 1, 0) },
                new CombatLevelPointDefinition[0]);
            AssertMessage<InvalidOperationException>(
                () => CombatLevelJson.SaveToJson(invalidDefinition),
                "Combat level validation failed: UnsupportedSchemaVersion at $.schemaVersion.");
        }

        [Test]
        public void Hash_ObbOrderAndEveryPersistedFieldAffectContentIdentity()
        {
            CombatLevelObbColliderDefinition first = CreateHashObb(stableId: "obb.a", bodyId: 10);
            CombatLevelObbColliderDefinition second = CreateHashObb(stableId: "obb.b", bodyId: 11);
            CombatLevelDefinition ordered = CreateHashLevel(first, second);
            CombatLevelDefinition reversed = CreateHashLevel(second, first);
            Assert.AreEqual(CombatLevelHash.ComputeContentHash(ordered), CombatLevelHash.ComputeContentHash(reversed));

            CombatLevelAabbColliderDefinition aabbA = CreateCollider("aabb.a", 20, 20, 1, 1, CombatSurfaceMaterial.Default);
            CombatLevelAabbColliderDefinition aabbB = CreateCollider("aabb.b", 21, 21, 1, 1, CombatSurfaceMaterial.Rough);
            CombatLevelDefinition mixedOrdered = CreateLevel(
                new[] { aabbA, aabbB },
                new[] { first, second },
                new[] { CreatePoint("spawn.default", 0, 1, 0) },
                new CombatLevelPointDefinition[0]);
            CombatLevelDefinition mixedReversed = CreateLevel(
                new[] { aabbB, aabbA },
                new[] { second, first },
                new[] { CreatePoint("spawn.default", 0, 1, 0) },
                new CombatLevelPointDefinition[0]);
            Assert.AreEqual(
                CombatLevelHash.ComputeContentHash(mixedOrdered),
                CombatLevelHash.ComputeContentHash(mixedReversed));

            CombatHash baseline = CombatLevelHash.ComputeContentHash(CreateHashLevel(CreateHashObb()));
            CombatLevelDefinition[] mutations =
            {
                CreateHashLevel(CreateHashObb(stableId: "obb.changed")),
                CreateHashLevel(CreateHashObb(entityId: 101)),
                CreateHashLevel(CreateHashObb(bodyId: 102)),
                CreateHashLevel(CreateHashObb(colliderId: 103)),
                CreateHashLevel(CreateHashObb(layer: 2)),
                CreateHashLevel(CreateHashObb(positionXRaw: 1)),
                CreateHashLevel(CreateHashObb(localCenterXRaw: 1)),
                CreateHashLevel(CreateHashObb(halfExtentXRaw: 1000001)),
                CreateHashLevel(CreateHashObb(axisXXRaw: 999999)),
                CreateHashLevel(CreateHashObb(axisYYRaw: 999999)),
                CreateHashLevel(CreateHashObb(axisZZRaw: 999999)),
                CreateHashLevel(CreateHashObb(material: CombatLevelSurfaceMaterialDefinition.FromSurfaceMaterial(CombatSurfaceMaterial.Rough)))
            };
            for (int i = 0; i < mutations.Length; i++)
                Assert.AreNotEqual(baseline, CombatLevelHash.ComputeContentHash(mutations[i]), "mutation " + i);
        }

        [Test]
        public void Hash_ShapeDiscriminatorAndDisplayNameAffectIdentity()
        {
            CombatLevelDefinition obb = CreateHashLevel(CreateHashObb());
            CombatLevelDefinition renamed = new CombatLevelDefinition(
                2, obb.LevelStableId, "Renamed", obb.AabbColliders, obb.ObbColliders, obb.SpawnPoints, obb.Checkpoints);
            CombatLevelDefinition aabb = CreateLevel(
                new[] { CreateCollider("obb.default", 100, 10, 1, 1, V(0, 0, 0), V(-1, -1, -1), V(1, 1, 1), CombatSurfaceMaterial.Default) },
                new CombatLevelObbColliderDefinition[0],
                new[] { CreatePoint("spawn.default", 0, 1, 0) },
                new CombatLevelPointDefinition[0]);

            Assert.AreNotEqual(CombatLevelHash.ComputeContentHash(obb), CombatLevelHash.ComputeContentHash(renamed));
            Assert.AreNotEqual(CombatLevelHash.ComputeContentHash(obb), CombatLevelHash.ComputeContentHash(aabb));
            Assert.AreEqual(1, CombatLevelHash.ShapeDiscriminatorAabb);
            Assert.AreEqual(2, CombatLevelHash.ShapeDiscriminatorObb);
        }

        [Test]
        public void Hash_EveryAxisComponentAndMaterialComponentMutatesIdentity()
        {
            CombatLevelObbColliderDefinition baselineObb = CreateHashObb();
            CombatHash baseline = CombatLevelHash.ComputeContentHash(CreateHashLevel(baselineObb));
            long[][][] axes =
            {
                new[] { new[] { 1000000L, 0L, 0L }, new[] { 0L, 1000000L, 0L }, new[] { 0L, 0L, 1000000L } },
                new[] { new[] { 1000000L, 0L, 0L }, new[] { 0L, 1000000L, 0L }, new[] { 0L, 0L, 1000000L } },
                new[] { new[] { 1000000L, 0L, 0L }, new[] { 0L, 1000000L, 0L }, new[] { 0L, 0L, 1000000L } }
            };
            for (int axis = 0; axis < 3; axis++)
            {
                for (int component = 0; component < 3; component++)
                {
                    axes[axis][axis][component] += 1L;
                    CombatLevelObbColliderDefinition mutation = CreateObb(
                        baselineObb.StableId,
                        baselineObb.EntityId,
                        baselineObb.BodyId,
                        baselineObb.ColliderId,
                        baselineObb.Layer,
                        baselineObb.Position,
                        baselineObb.LocalCenter,
                        baselineObb.HalfExtents,
                        R(axes[axis][0][0], axes[axis][0][1], axes[axis][0][2]),
                        R(axes[axis][1][0], axes[axis][1][1], axes[axis][1][2]),
                        R(axes[axis][2][0], axes[axis][2][1], axes[axis][2][2]),
                        baselineObb.SurfaceMaterial);
                    Assert.AreNotEqual(
                        baseline,
                        CombatLevelHash.ComputeContentHash(CreateHashLevel(mutation)),
                        "axis=" + axis + ", component=" + component);
                    axes[axis][axis][component] -= 1L;
                }
            }

            CombatLevelSurfaceMaterialDefinition[] materials =
            {
                new CombatLevelSurfaceMaterialDefinition(CombatSurfaceMaterialKind.Rough, 0, 0, 0),
                new CombatLevelSurfaceMaterialDefinition(CombatSurfaceMaterialKind.Default, 1, 0, 0),
                new CombatLevelSurfaceMaterialDefinition(CombatSurfaceMaterialKind.Default, 0, 1, 0),
                new CombatLevelSurfaceMaterialDefinition(CombatSurfaceMaterialKind.Default, 0, 0, 1)
            };
            for (int i = 0; i < materials.Length; i++)
            {
                CombatLevelObbColliderDefinition mutation = CreateObb(
                    baselineObb.StableId,
                    baselineObb.EntityId,
                    baselineObb.BodyId,
                    baselineObb.ColliderId,
                    baselineObb.Layer,
                    baselineObb.Position,
                    baselineObb.LocalCenter,
                    baselineObb.HalfExtents,
                    baselineObb.AxisX,
                    baselineObb.AxisY,
                    baselineObb.AxisZ,
                    materials[i]);
                Assert.AreNotEqual(
                    baseline,
                    CombatLevelHash.ComputeContentHash(CreateHashLevel(mutation)),
                    "material component=" + i);
            }
        }

        [Test]
        public void WorldBuilder_BuildsAndCopiesObbWithPriorityZeroAndHonorsClearExisting()
        {
            CombatLevelDefinition level = CreateHashLevel(CreateHashObb());
            CombatPhysicsWorld world = CombatLevelWorldBuilder.BuildWorld(level);

            Assert.AreEqual(1, world.BodyCount);
            Assert.AreEqual(1, world.TotalColliderCount);
            Assert.IsTrue(world.TryGetObbCollider(new CombatBodyId(10), new CombatColliderId(1), out CombatPhysicsObbCollider obb));
            Assert.AreEqual(FixVector3.Zero, obb.LocalCenter);
            Assert.AreEqual(new FixVector3(Fix64.One, Fix64.One, Fix64.One), obb.HalfExtents);
            Assert.AreEqual(CombatSurfaceMaterialKind.Default, obb.SurfaceMaterial.Kind);
            Assert.AreEqual(0, obb.Priority);
            var copied = new List<CombatPhysicsObbCollider>();
            Assert.AreEqual(1, world.CopyObbCollidersTo(copied));
            Assert.AreEqual(obb, copied[0]);

            CombatLevelWorldBuilder.PopulateWorld(
                CreateLevel(
                    new[] { CreateCollider("extra", 200, 200, 1, 0, CombatSurfaceMaterial.Default) },
                    new CombatLevelObbColliderDefinition[0],
                    new[] { CreatePoint("spawn.extra", 0, 1, 0) },
                    new CombatLevelPointDefinition[0]),
                world,
                clearExisting: false);
            Assert.AreEqual(2, world.TotalColliderCount);
            CombatLevelWorldBuilder.PopulateWorld(level, world, clearExisting: true);
            Assert.AreEqual(1, world.TotalColliderCount);
            Assert.IsFalse(world.TryGetBody(new CombatBodyId(200), out _));
        }

        [Test]
        public void WorldBuilder_InvalidDefinitionNeverMutatesPrepopulatedWorldForEitherClearMode()
        {
            var world = new CombatPhysicsWorld();
            var existingBodyId = new CombatBodyId(900);
            var existingColliderId = new CombatColliderId(901);
            CombatPhysicsBody existingBody = new CombatPhysicsBody(
                new CombatEntityId(899),
                existingBodyId,
                R(7000000, 8000000, 9000000).ToFixVector3());
            CombatPhysicsAabbCollider existingCollider = new CombatPhysicsAabbCollider(
                existingBodyId,
                existingColliderId,
                3,
                V(-1, -1, -1).ToFixVector3(),
                V(1, 1, 1).ToFixVector3(),
                CombatSurfaceMaterial.Rough);
            world.UpsertBody(existingBody);
            world.UpsertAabbCollider(existingCollider);
            int revision = world.Revision;
            int topologyRevision = world.TopologyRevision;

            CombatLevelDefinition invalid = CreateLevel(
                new[] { CreateCollider("would.add", 300, 300, 1, 1, CombatSurfaceMaterial.Default) },
                new[]
                {
                    CreateObb(
                        "invalid.extent", 301, 301, 1, 1,
                        V(0, 0, 0), V(0, 0, 0), R(0, 1000000, 1000000),
                        R(1000000, 0, 0), R(0, 1000000, 0), R(0, 0, 1000000),
                        CombatLevelSurfaceMaterialDefinition.Default)
                },
                new[] { CreatePoint("spawn.default", 0, 1, 0) },
                new CombatLevelPointDefinition[0]);

            Assert.Throws<InvalidOperationException>(() =>
                CombatLevelWorldBuilder.PopulateWorld(invalid, world, clearExisting: false));
            AssertWorldUnchanged();
            Assert.Throws<InvalidOperationException>(() =>
                CombatLevelWorldBuilder.PopulateWorld(invalid, world, clearExisting: true));
            AssertWorldUnchanged();

            void AssertWorldUnchanged()
            {
                Assert.AreEqual(1, world.BodyCount);
                Assert.AreEqual(1, world.TotalColliderCount);
                Assert.AreEqual(revision, world.Revision);
                Assert.AreEqual(topologyRevision, world.TopologyRevision);
                Assert.IsTrue(world.TryGetBody(existingBodyId, out CombatPhysicsBody body));
                Assert.AreEqual(existingBody, body);
                Assert.IsTrue(world.TryGetAabbCollider(existingBodyId, existingColliderId, out CombatPhysicsAabbCollider collider));
                Assert.AreEqual(existingCollider, collider);
                Assert.IsFalse(world.TryGetBody(new CombatBodyId(300), out _));
                Assert.IsFalse(world.TryGetBody(new CombatBodyId(301), out _));
            }
        }

        [Test]
        public void WorldBuilder_BuildsPhysicsWorldWithSurfaceMaterial()
        {
            CombatLevelDefinition level = CreateLevel(
                new[]
                {
                    CreateCollider(
                        "bounce.floor",
                        entityId: 100,
                        bodyId: 200,
                        colliderId: 300,
                        layer: 4,
                        position: V(2, 3, 4),
                        localMin: V(-5, -1, -2),
                        localMax: V(5, 0, 2),
                        material: CombatSurfaceMaterial.BouncePad)
                },
                new[] { CreatePoint("spawn.default", x: 0, y: 1, z: 0) },
                new CombatLevelPointDefinition[0]);

            CombatPhysicsWorld world = CombatLevelWorldBuilder.BuildWorld(level);

            Assert.AreEqual(1, world.BodyCount);
            Assert.AreEqual(1, world.ColliderCount);
            Assert.IsTrue(world.TryGetBody(new CombatBodyId(200), out CombatPhysicsBody body));
            Assert.AreEqual(new CombatEntityId(100), body.EntityId);
            Assert.AreEqual(V(2, 3, 4).ToFixVector3(), body.Position);
            Assert.IsTrue(world.TryGetAabbCollider(
                new CombatBodyId(200),
                new CombatColliderId(300),
                out CombatPhysicsAabbCollider collider));
            Assert.AreEqual(4, collider.Layer);
            Assert.AreEqual(V(-5, -1, -2).ToFixVector3(), collider.LocalMin);
            Assert.AreEqual(V(5, 0, 2).ToFixVector3(), collider.LocalMax);
            Assert.AreEqual(CombatSurfaceMaterialKind.BouncePad, collider.SurfaceMaterialKind);
            Assert.AreEqual(CombatSurfaceMaterial.BouncePad.Friction, collider.SurfaceFriction);
            Assert.AreEqual(CombatSurfaceMaterial.BouncePad.Elasticity, collider.SurfaceElasticity);
            Assert.AreEqual(CombatSurfaceMaterial.BouncePad.FixedBounceHeight, collider.SurfaceFixedBounceHeight);
        }

        private static CombatLevelDefinition CreateLevel(
            IReadOnlyList<CombatLevelAabbColliderDefinition> colliders,
            IReadOnlyList<CombatLevelPointDefinition> spawnPoints,
            IReadOnlyList<CombatLevelPointDefinition> checkpoints)
        {
            return new CombatLevelDefinition(
                CombatLevelDefinition.CurrentSchemaVersion,
                "level.test",
                "Test Level",
                colliders,
                spawnPoints,
                checkpoints);
        }

        private static CombatLevelDefinition CreateLevel(
            IReadOnlyList<CombatLevelAabbColliderDefinition> aabbColliders,
            IReadOnlyList<CombatLevelObbColliderDefinition> obbColliders,
            IReadOnlyList<CombatLevelPointDefinition> spawnPoints,
            IReadOnlyList<CombatLevelPointDefinition> checkpoints)
        {
            return new CombatLevelDefinition(
                CombatLevelDefinition.CurrentSchemaVersion,
                "level.test",
                "Test Level",
                aabbColliders,
                obbColliders,
                spawnPoints,
                checkpoints);
        }

        private static CombatLevelValidationReport ValidateSingleObb(
            CombatLevelObbColliderDefinition obb)
        {
            return CombatLevelValidator.Validate(CreateLevel(
                new CombatLevelAabbColliderDefinition[0],
                new[] { obb },
                new[] { CreatePoint("spawn.default", 0, 1, 0) },
                new CombatLevelPointDefinition[0]));
        }

        private static CombatLevelDefinition CreateHashLevel(
            params CombatLevelObbColliderDefinition[] obbs)
        {
            return new CombatLevelDefinition(
                CombatLevelDefinition.CurrentSchemaVersion,
                "level.hash",
                "Hash Level",
                new CombatLevelAabbColliderDefinition[0],
                obbs,
                new[] { CreatePoint("spawn.default", 0, 1, 0) },
                new CombatLevelPointDefinition[0]);
        }

        private static CombatLevelObbColliderDefinition CreateHashObb(
            string stableId = "obb.default",
            int entityId = 100,
            int bodyId = 10,
            int colliderId = 1,
            int layer = 1,
            long positionXRaw = 0,
            long localCenterXRaw = 0,
            long halfExtentXRaw = 1000000,
            long axisXXRaw = 1000000,
            long axisYYRaw = 1000000,
            long axisZZRaw = 1000000,
            CombatLevelSurfaceMaterialDefinition material = null)
        {
            return CreateObb(
                stableId,
                entityId,
                bodyId,
                colliderId,
                layer,
                R(positionXRaw, 0, 0),
                R(localCenterXRaw, 0, 0),
                R(halfExtentXRaw, 1000000, 1000000),
                R(axisXXRaw, 0, 0),
                R(0, axisYYRaw, 0),
                R(0, 0, axisZZRaw),
                material ?? CombatLevelSurfaceMaterialDefinition.Default);
        }

        private static CombatLevelObbColliderDefinition CreateValidObb(
            string stableId,
            int bodyId = 20,
            int colliderId = 30,
            CombatLevelFixedVector3 halfExtents = null,
            CombatLevelFixedVector3 axisX = null,
            CombatLevelFixedVector3 axisY = null,
            CombatLevelFixedVector3 axisZ = null)
        {
            return CreateObb(
                stableId,
                entityId: bodyId,
                bodyId: bodyId,
                colliderId: colliderId,
                layer: 1,
                position: V(0, 0, 0),
                localCenter: V(0, 0, 0),
                halfExtents: halfExtents ?? V(1, 1, 1),
                axisX: axisX ?? R(1000000, 0, 0),
                axisY: axisY ?? R(0, 1000000, 0),
                axisZ: axisZ ?? R(0, 0, 1000000),
                surfaceMaterial: CombatLevelSurfaceMaterialDefinition.Default);
        }

        private static CombatLevelObbColliderDefinition CreateObb(
            string stableId,
            int entityId,
            int bodyId,
            int colliderId,
            int layer,
            CombatLevelFixedVector3 position,
            CombatLevelFixedVector3 localCenter,
            CombatLevelFixedVector3 halfExtents,
            CombatLevelFixedVector3 axisX,
            CombatLevelFixedVector3 axisY,
            CombatLevelFixedVector3 axisZ,
            CombatLevelSurfaceMaterialDefinition surfaceMaterial)
        {
            return new CombatLevelObbColliderDefinition(
                stableId,
                entityId,
                bodyId,
                colliderId,
                layer,
                position,
                localCenter,
                halfExtents,
                axisX,
                axisY,
                axisZ,
                surfaceMaterial);
        }

        private static CombatLevelAabbColliderDefinition CreateCollider(
            string stableId,
            int entityId,
            int bodyId,
            int colliderId,
            int layer,
            CombatSurfaceMaterial material)
        {
            return CreateCollider(
                stableId,
                entityId,
                bodyId,
                colliderId,
                layer,
                V(0, 0, 0),
                V(-1, -1, -1),
                V(1, 1, 1),
                CombatLevelSurfaceMaterialDefinition.FromSurfaceMaterial(material));
        }

        private static CombatLevelAabbColliderDefinition CreateCollider(
            string stableId,
            int entityId,
            int bodyId,
            int colliderId,
            int layer,
            CombatLevelFixedVector3 position,
            CombatLevelFixedVector3 localMin,
            CombatLevelFixedVector3 localMax,
            CombatSurfaceMaterial material)
        {
            return CreateCollider(
                stableId,
                entityId,
                bodyId,
                colliderId,
                layer,
                position,
                localMin,
                localMax,
                CombatLevelSurfaceMaterialDefinition.FromSurfaceMaterial(material));
        }

        private static CombatLevelAabbColliderDefinition CreateCollider(
            string stableId,
            int entityId,
            int bodyId,
            int colliderId,
            int layer,
            CombatLevelFixedVector3 localMin,
            CombatLevelFixedVector3 localMax,
            CombatLevelSurfaceMaterialDefinition surfaceMaterial)
        {
            return CreateCollider(
                stableId,
                entityId,
                bodyId,
                colliderId,
                layer,
                V(0, 0, 0),
                localMin,
                localMax,
                surfaceMaterial);
        }

        private static CombatLevelAabbColliderDefinition CreateCollider(
            string stableId,
            int entityId,
            int bodyId,
            int colliderId,
            int layer,
            CombatLevelFixedVector3 position,
            CombatLevelFixedVector3 localMin,
            CombatLevelFixedVector3 localMax,
            CombatLevelSurfaceMaterialDefinition surfaceMaterial)
        {
            return new CombatLevelAabbColliderDefinition(
                stableId,
                entityId,
                bodyId,
                colliderId,
                layer,
                position,
                localMin,
                localMax,
                surfaceMaterial);
        }

        private static CombatLevelPointDefinition CreatePoint(string stableId, int x, int y, int z)
        {
            return new CombatLevelPointDefinition(stableId, V(x, y, z));
        }

        private static CombatLevelFixedVector3 V(int x, int y, int z)
        {
            return CombatLevelFixedVector3.FromFixVector3(new FixVector3(
                Fix64.FromInt(x),
                Fix64.FromInt(y),
                Fix64.FromInt(z)));
        }

        private static CombatLevelFixedVector3 R(long xRaw, long yRaw, long zRaw)
        {
            return new CombatLevelFixedVector3(xRaw, yRaw, zRaw);
        }

        private static void AssertIssueSequence(
            CombatLevelValidationReport report,
            IReadOnlyList<string> expectedCodes,
            IReadOnlyList<string> expectedPaths)
        {
            Assert.AreEqual(expectedCodes.Count, expectedPaths.Count);
            Assert.AreEqual(expectedCodes.Count, report.Issues.Count);
            for (int i = 0; i < expectedCodes.Count; i++)
            {
                Assert.AreEqual(expectedCodes[i], report.Issues[i].Code, "code at issue " + i);
                Assert.AreEqual(expectedPaths[i], report.Issues[i].Path, "path at issue " + i);
                Assert.AreEqual(
                    CombatLevelValidationSeverity.Error,
                    report.Issues[i].Severity,
                    "severity at issue " + i);
            }
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

        private static void AssertMessage<TException>(TestDelegate action, string expectedSubstring)
            where TException : Exception
        {
            TException exception = Assert.Throws<TException>(action);
            StringAssert.Contains(expectedSubstring, exception.Message);
        }

        private static void AssertDuplicateRejected(string json, string propertyName)
        {
            JsonReaderException exception = Assert.Throws<JsonReaderException>(() =>
                CombatLevelJson.LoadFromJson(json));
            StringAssert.Contains(propertyName, exception.Message);
        }
    }
}
