using MxFramework.Config.Levels;
using NUnit.Framework;
using System.Collections.Generic;

namespace MxFramework.Tests.Config
{
    public sealed class LevelWorldObjectAuthoringTests
    {
        [Test]
        public void WorldObjectAuthoringJson_RoundTripNormalizesOrderAndHash()
        {
            var document = new LevelWorldObjectAuthoringDocument
            {
                LevelId = "character_test_scene",
                ScenePath = "Assets\\Scenes\\CharacterTestScene.unity",
                RootPath = "CharacterTestSceneRoot"
            };
            document.SingleStaticColliders.Add(new LevelWorldObjectInstanceDefinition
            {
                Enabled = true,
                Role = LevelWorldObjectRole.StaticCollider,
                DefinitionId = 1001,
                TransformRef = LevelTransformReference.FromPath("TestCourse_30_MotionGeometry[3]/Wall[0]"),
                BodyId = 61710,
                ColliderId = 1,
                Group = "TestCourse_30_MotionGeometry"
            });
            document.MovableInstances.Add(new LevelWorldObjectInstanceDefinition
            {
                Enabled = true,
                Role = LevelWorldObjectRole.Movable,
                DefinitionId = 101,
                TransformRef = LevelTransformReference.FromPath("TestCourse_20_MovableNaturalObjects[2]/MovableNaturalObject_slick_light_visual[0]"),
                NumericInstanceId = 1,
                Group = "TestCourse_20_MovableNaturalObjects"
            });
            document.StaticColliderBatches.Add(new LevelWorldObjectStaticColliderBatchDefinition
            {
                Enabled = true,
                DefinitionId = 1001,
                ContainerTransformRef = LevelTransformReference.FromPath("TestCourse_30_MotionGeometry[3]/Stairs[0]"),
                BodyIdStart = 10020,
                ColliderId = 1,
                Group = "TestCourse_30_MotionGeometry"
            });
            document.Points.Add(new LevelWorldObjectPointDefinition
            {
                Enabled = true,
                DefinitionId = 2001,
                StableId = "spawn.player",
                PointKind = LevelWorldObjectPointKind.PlayerSpawn,
                TransformRef = LevelTransformReference.FromPath("TestCourse_00_Start[0]/LevelPoint_PlayerSpawn[0]"),
                Group = "TestCourse_00_Start"
            });

            string json = LevelWorldObjectAuthoringJson.SaveToJson(document);
            LevelWorldObjectAuthoringDocument loaded =
                LevelWorldObjectAuthoringJson.LoadFromJson(json);
            string firstHash = LevelWorldObjectAuthoringJson.ComputeContentHash(document);
            string secondHash = LevelWorldObjectAuthoringJson.ComputeContentHash(loaded);

            Assert.AreEqual(LevelWorldObjectAuthoringDocument.CurrentFormat, loaded.Format);
            Assert.AreEqual(LevelWorldObjectAuthoringDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreEqual("Assets/Scenes/CharacterTestScene.unity", loaded.ScenePath);
            Assert.AreEqual(1, loaded.MovableInstances.Count);
            Assert.AreEqual(1, loaded.SingleStaticColliders.Count);
            Assert.AreEqual(1, loaded.StaticColliderBatches.Count);
            Assert.AreEqual(1, loaded.Points.Count);
            Assert.AreEqual(LevelWorldObjectRole.Movable, loaded.Instances[0].Role);
            Assert.AreEqual(LevelWorldObjectRole.StaticCollider, loaded.Instances[1].Role);
            Assert.AreEqual(firstHash, secondHash);
        }

        [Test]
        public void WorldObjectAuthoringJson_V1PayloadMigratesToV2Collections()
        {
            string json =
                "{"
                + "\"format\":\"mx.level.worldObjects.v1\","
                + "\"schemaVersion\":1,"
                + "\"levelId\":\"character_test_scene\","
                + "\"scenePath\":\"Assets/Scenes/CharacterTestScene.unity\","
                + "\"rootPath\":\"CharacterTestSceneRoot\","
                + "\"instances\":["
                + "{\"enabled\":true,\"role\":\"Movable\",\"definitionId\":101,\"transformPath\":\"Group[0]/Movable[0]\",\"numericInstanceId\":7},"
                + "{\"enabled\":true,\"role\":\"StaticCollider\",\"definitionId\":1001,\"transformPath\":\"Group[0]/Wall[1]\",\"bodyId\":99,\"colliderId\":1}"
                + "]}";

            LevelWorldObjectAuthoringDocument loaded =
                LevelWorldObjectAuthoringJson.LoadFromJson(json);

            Assert.AreEqual(LevelWorldObjectAuthoringDocument.CurrentFormat, loaded.Format);
            Assert.AreEqual(LevelWorldObjectAuthoringDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreEqual(1, loaded.MovableInstances.Count);
            Assert.AreEqual(1, loaded.SingleStaticColliders.Count);
            Assert.AreEqual("Group[0]/Movable[0]", loaded.MovableInstances[0].TransformRef.Path);
            Assert.AreEqual("Group[0]/Wall[1]", loaded.SingleStaticColliders[0].TransformRef.Path);
        }

        [Test]
        public void LevelAuthoringJson_V3RoundTripPreservesModulePayloadAndHash()
        {
            var payload = new LevelWorldObjectAuthoringDocument
            {
                LevelId = "character_test_scene",
                ScenePath = "Assets/Scenes/CharacterTestScene.unity",
                RootPath = "CharacterTestSceneRoot"
            };
            payload.MovableInstances.Add(new LevelWorldObjectInstanceDefinition
            {
                Role = LevelWorldObjectRole.Movable,
                DefinitionId = 101,
                TransformRef = LevelTransformReference.FromPath("Group[0]/Movable[0]"),
                NumericInstanceId = 101
            });

            var envelope = new MxLevelAuthoringDocument
            {
                LevelId = payload.LevelId,
                ScenePath = payload.ScenePath,
                RootTransformRef = LevelTransformReference.FromPath(payload.RootPath)
            };
            envelope.UpsertModulePayload(
                MxLevelAuthoringDocument.CharacterTestWorldObjectsModuleId,
                1,
                payload,
                "CharacterTest World Objects");

            string json = MxLevelAuthoringJson.SaveToJson(envelope);
            MxLevelAuthoringDocument loaded = MxLevelAuthoringJson.LoadFromJson(json);
            string firstHash = MxLevelAuthoringJson.ComputeContentHash(envelope);
            string secondHash = MxLevelAuthoringJson.ComputeContentHash(loaded);

            Assert.AreEqual(MxLevelAuthoringDocument.CurrentFormat, loaded.Format);
            Assert.AreEqual(MxLevelAuthoringDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreEqual(1, loaded.Modules.Count);
            Assert.AreEqual(
                MxLevelAuthoringDocument.CharacterTestWorldObjectsModuleId,
                loaded.Modules[0].ModuleId);
            Assert.IsTrue(loaded.TryReadModulePayload(
                MxLevelAuthoringDocument.CharacterTestWorldObjectsModuleId,
                out LevelWorldObjectAuthoringDocument loadedPayload));
            loadedPayload = loadedPayload.Normalize();
            Assert.AreEqual(1, loadedPayload.MovableInstances.Count);
            Assert.AreEqual("Group[0]/Movable[0]", loadedPayload.MovableInstances[0].TransformRef.Path);
            Assert.AreEqual(firstHash, secondHash);
        }

        [Test]
        public void LevelAuthoringJson_V2WorldObjectsPayloadMigratesToV3Envelope()
        {
            var payload = new LevelWorldObjectAuthoringDocument
            {
                LevelId = "character_test_scene",
                ScenePath = "Assets/Scenes/CharacterTestScene.unity",
                RootPath = "CharacterTestSceneRoot"
            };
            payload.SingleStaticColliders.Add(new LevelWorldObjectInstanceDefinition
            {
                Role = LevelWorldObjectRole.StaticCollider,
                DefinitionId = 1001,
                TransformRef = LevelTransformReference.FromPath("Group[0]/Wall[0]"),
                BodyId = 100,
                ColliderId = 1
            });

            string legacyJson = LevelWorldObjectAuthoringJson.SaveToJson(payload);
            MxLevelAuthoringDocument envelope = MxLevelAuthoringJson.LoadFromJson(legacyJson);

            Assert.AreEqual(MxLevelAuthoringDocument.CurrentFormat, envelope.Format);
            Assert.AreEqual("CharacterTestSceneRoot", envelope.RootTransformRef.Path);
            Assert.AreEqual(1, envelope.Modules.Count);
            Assert.AreEqual(
                MxLevelAuthoringDocument.CharacterTestWorldObjectsModuleId,
                envelope.Modules[0].ModuleId);
            Assert.IsTrue(envelope.TryReadModulePayload(
                MxLevelAuthoringDocument.CharacterTestWorldObjectsModuleId,
                out LevelWorldObjectAuthoringDocument loadedPayload));
            Assert.AreEqual(1, loadedPayload.Normalize().SingleStaticColliders.Count);
        }

        [Test]
        public void LevelAuthoringValidator_ReportsDuplicateModuleAndMissingRoot()
        {
            var envelope = new MxLevelAuthoringDocument
            {
                LevelId = "level",
                ScenePath = "Assets/Scenes/Test.unity"
            };
            envelope.Modules.Add(new MxLevelAuthoringModuleDocument
            {
                ModuleId = "module.a"
            });
            envelope.Modules.Add(new MxLevelAuthoringModuleDocument
            {
                ModuleId = "module.a"
            });

            MxLevelAuthoringValidationReport report =
                MxLevelAuthoringValidator.Validate(envelope);

            Assert.IsTrue(report.HasErrors);
            AssertHasIssue(report, MxLevelAuthoringValidator.MissingRootTransformRef);
            AssertHasIssue(report, MxLevelAuthoringValidator.DuplicateModuleId);
        }

        [Test]
        public void WorldObjectAuthoringResolver_ExpandsStaticColliderBatchesDeterministically()
        {
            var document = new LevelWorldObjectAuthoringDocument
            {
                LevelId = "character_test_scene",
                ScenePath = "Assets/Scenes/CharacterTestScene.unity",
                RootPath = "CharacterTestSceneRoot"
            };
            document.SingleStaticColliders.Add(new LevelWorldObjectInstanceDefinition
            {
                Enabled = true,
                Role = LevelWorldObjectRole.StaticCollider,
                DefinitionId = 1002,
                TransformRef = LevelTransformReference.FromPath("Single[0]"),
                BodyId = 900,
                ColliderId = 1,
                Group = "SingleGroup"
            });
            document.StaticColliderBatches.Add(new LevelWorldObjectStaticColliderBatchDefinition
            {
                Enabled = true,
                DefinitionId = 1001,
                ContainerTransformRef = LevelTransformReference.FromPath("Batch[0]"),
                BodyIdStart = 1000,
                ColliderId = 2,
                Group = "BatchGroup"
            });

            List<LevelWorldObjectInstanceDefinition> expanded =
                LevelWorldObjectAuthoringResolver.ExpandStaticColliderBatches(
                    document,
                    _ => new[]
                    {
                        LevelTransformReference.FromPath("Batch[0]/A[0]"),
                        LevelTransformReference.FromPath("Batch[0]/B[1]")
                    });

            Assert.AreEqual(3, expanded.Count);
            Assert.AreEqual("Batch[0]/A[0]", expanded[0].TransformRef.Path);
            Assert.AreEqual(1000, expanded[0].BodyId);
            Assert.AreEqual(2, expanded[0].ColliderId);
            Assert.AreEqual("Batch[0]/B[1]", expanded[1].TransformRef.Path);
            Assert.AreEqual(1001, expanded[1].BodyId);
            Assert.AreEqual("Single[0]", expanded[2].TransformRef.Path);
        }

        [Test]
        public void WorldObjectAuthoringValidator_ReportsDuplicateIdsAndPaths()
        {
            var document = new LevelWorldObjectAuthoringDocument
            {
                LevelId = "character_test_scene",
                ScenePath = "Assets/Scenes/CharacterTestScene.unity",
                RootPath = "CharacterTestSceneRoot"
            };
            document.MovableInstances.Add(new LevelWorldObjectInstanceDefinition
            {
                Role = LevelWorldObjectRole.Movable,
                DefinitionId = 101,
                TransformRef = LevelTransformReference.FromPath("Group[0]/Object[0]"),
                NumericInstanceId = 7
            });
            document.MovableInstances.Add(new LevelWorldObjectInstanceDefinition
            {
                Role = LevelWorldObjectRole.Movable,
                DefinitionId = 101,
                TransformRef = LevelTransformReference.FromPath("Group[0]/Object[0]"),
                NumericInstanceId = 7
            });
            document.SingleStaticColliders.Add(new LevelWorldObjectInstanceDefinition
            {
                Role = LevelWorldObjectRole.StaticCollider,
                DefinitionId = 1001,
                TransformRef = LevelTransformReference.FromPath("Group[0]/Wall[1]"),
                BodyId = 99,
                ColliderId = 1
            });
            document.SingleStaticColliders.Add(new LevelWorldObjectInstanceDefinition
            {
                Role = LevelWorldObjectRole.StaticCollider,
                DefinitionId = 1001,
                TransformRef = LevelTransformReference.FromPath("Group[0]/Wall[2]"),
                BodyId = 99,
                ColliderId = 1
            });
            document.StaticColliderBatches.Add(new LevelWorldObjectStaticColliderBatchDefinition
            {
                DefinitionId = 1001,
                ContainerTransformRef = LevelTransformReference.FromPath("Group[0]/Batch[3]"),
                BodyIdStart = 1000,
                ColliderId = 1
            });
            document.StaticColliderBatches.Add(new LevelWorldObjectStaticColliderBatchDefinition
            {
                DefinitionId = 1001,
                ContainerTransformRef = LevelTransformReference.FromPath("Group[0]/Batch[3]"),
                BodyIdStart = 1000,
                ColliderId = 1
            });
            document.Points.Add(new LevelWorldObjectPointDefinition
            {
                DefinitionId = 2001,
                StableId = "spawn.player",
                TransformRef = LevelTransformReference.FromPath("Group[0]/PointA[4]")
            });
            document.Points.Add(new LevelWorldObjectPointDefinition
            {
                DefinitionId = 2001,
                StableId = "spawn.player",
                TransformRef = LevelTransformReference.FromPath("Group[0]/PointB[5]")
            });

            LevelWorldObjectAuthoringValidationReport report =
                LevelWorldObjectAuthoringValidator.Validate(document);

            Assert.IsTrue(report.HasErrors);
            AssertHasIssue(report, LevelWorldObjectAuthoringValidator.DuplicateTransformPath);
            AssertHasIssue(report, LevelWorldObjectAuthoringValidator.DuplicateMovableNumericId);
            AssertHasIssue(report, LevelWorldObjectAuthoringValidator.DuplicateStaticBodyColliderId);
            AssertHasIssue(report, LevelWorldObjectAuthoringValidator.DuplicatePointStableId);
        }

        private static void AssertHasIssue(
            LevelWorldObjectAuthoringValidationReport report,
            string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return;
            }

            Assert.Fail("Expected authoring issue code: " + code);
        }

        private static void AssertHasIssue(
            MxLevelAuthoringValidationReport report,
            string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return;
            }

            Assert.Fail("Expected authoring issue code: " + code);
        }
    }
}
