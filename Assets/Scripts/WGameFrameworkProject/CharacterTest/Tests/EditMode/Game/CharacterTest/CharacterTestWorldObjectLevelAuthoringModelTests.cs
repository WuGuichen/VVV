using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestWorldObjectLevelAuthoringTests
    {
        [Test]
        public void WorldObjectJsonDocument_AppliesBackToSceneSourceWithoutChangingRuntimeSpecs()
        {
            GameObject root = new GameObject("CharacterTestSceneRoot");
            CharacterTestWorldObjectSceneSource source =
                root.AddComponent<CharacterTestWorldObjectSceneSource>();
            GameObject movableObject = CreateChild(root.transform, "MovableNaturalObject_slick_light_visual");
            GameObject staticObject = CreateChild(root.transform, "SurfaceProbe_HardSlick");
            movableObject.transform.position = new Vector3(1f, 0f, 2f);
            movableObject.transform.localScale = new Vector3(1.5f, 1f, 1.25f);
            staticObject.transform.position = new Vector3(3f, -1f, 4f);
            staticObject.transform.localScale = new Vector3(4f, 1f, 6f);

            try
            {
                source.Configure(
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.MovableInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.MovableSlickLight,
                            7,
                            "authoring.slick_light",
                            movableObject.transform,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    },
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticHardSlickSurface,
                            staticObject.transform,
                            61710,
                            1,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    });

                CharacterTestMovableNaturalObjectSpawnSpec[] beforeMovables =
                    source.CollectMovableSpawnSpecs(out string[] beforeMovableDiagnostics);
                CharacterTestWorldObjectStaticColliderSpec[] beforeStatic =
                    source.CollectStaticColliderSpecs(out string[] beforeStaticDiagnostics);
                Assert.AreEqual(0, beforeMovableDiagnostics.Length);
                Assert.AreEqual(0, beforeStaticDiagnostics.Length);

                LevelWorldObjectAuthoringDocument document =
                    CharacterTestWorldObjectAuthoringAdapter.CreateDocumentFromSource(source);
                MxLevelAuthoringDocument envelope =
                    CharacterTestWorldObjectAuthoringAdapter.CreateEnvelopeFromDocument(document, root.transform);
                bool applied = CharacterTestWorldObjectAuthoringAdapter.ApplyEnvelopeToSource(
                    envelope,
                    source,
                    CharacterTestWorldObjectSceneSource.BuiltInCatalog,
                    CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath,
                    out LevelWorldObjectAuthoringValidationReport report);

                Assert.IsTrue(applied, JoinIssues(report));
                Assert.AreEqual(
                    MxLevelAuthoringJson.ComputeContentHash(envelope),
                    source.AuthoringContentHash);
                CharacterTestMovableNaturalObjectSpawnSpec[] afterMovables =
                    source.CollectMovableSpawnSpecs(out string[] afterMovableDiagnostics);
                CharacterTestWorldObjectStaticColliderSpec[] afterStatic =
                    source.CollectStaticColliderSpecs(out string[] afterStaticDiagnostics);

                Assert.AreEqual(0, afterMovableDiagnostics.Length);
                Assert.AreEqual(0, afterStaticDiagnostics.Length);
                Assert.AreEqual(beforeMovables.Length, afterMovables.Length);
                Assert.AreEqual(beforeStatic.Length, afterStatic.Length);
                Assert.AreEqual(beforeMovables[0].StableId, afterMovables[0].StableId);
                Assert.AreEqual(beforeMovables[0].NumericId, afterMovables[0].NumericId);
                Assert.AreEqual(beforeMovables[0].ShapeKind, afterMovables[0].ShapeKind);
                Assert.AreEqual(beforeMovables[0].MassGrams, afterMovables[0].MassGrams);
                Assert.AreEqual(beforeMovables[0].Material.Kind, afterMovables[0].Material.Kind);
                Assert.AreEqual(beforeStatic[0].DefinitionId, afterStatic[0].DefinitionId);
                Assert.AreEqual(beforeStatic[0].BodyId, afterStatic[0].BodyId);
                Assert.AreEqual(beforeStatic[0].ColliderId, afterStatic[0].ColliderId);
                Assert.AreEqual(beforeStatic[0].ShapeKind, afterStatic[0].ShapeKind);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CombatLevelCandidate_InvalidTransformMapsTypedIssueWithoutMutatingSourceOrEnvelope()
        {
            GameObject root = new GameObject("CharacterTest Candidate Root");
            CharacterTestWorldObjectSceneSource source = root.AddComponent<CharacterTestWorldObjectSceneSource>();
            GameObject box = CreateChild(root.transform, "Invalid Candidate Box");
            try
            {
                source.Configure(
                    new CharacterTestWorldObjectSceneSource.MovableInstance[0],
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            box.transform,
                            650,
                            2,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    });
                LevelWorldObjectAuthoringDocument document =
                    CharacterTestWorldObjectAuthoringAdapter.CreateDocumentFromSource(source);
                MxLevelAuthoringDocument envelope =
                    CharacterTestWorldObjectAuthoringAdapter.CreateEnvelopeFromDocument(document, root.transform);
                string envelopeBefore = MxLevelAuthoringJson.SaveToJson(envelope);
                int sourceCountBefore = source.StaticColliderInstanceCount;
                box.transform.localScale = new Vector3(-1f, 1f, 1f);

                bool compiled = CharacterTestWorldObjectAuthoringAdapter.TryCompileCombatLevelCandidate(
                    envelope,
                    root.transform,
                    CharacterTestWorldObjectSceneSource.BuiltInCatalog,
                    out _,
                    out LevelWorldObjectAuthoringValidationReport report);

                Assert.IsFalse(compiled);
                Assert.IsTrue(report.HasErrors);
                Assert.AreEqual(sourceCountBefore, source.StaticColliderInstanceCount);
                Assert.AreEqual(envelopeBefore, MxLevelAuthoringJson.SaveToJson(envelope));
                StringAssert.DoesNotContain("axisX", envelopeBefore);
                StringAssert.DoesNotContain("halfExtents", envelopeBefore);
                bool found = false;
                for (int i = 0; i < report.Issues.Count; i++)
                {
                    if (report.Issues[i].Code != CharacterTestCombatLevelSceneAdapter.InvalidTransformScaleCode)
                        continue;
                    found = true;
                    Assert.AreEqual(LevelWorldObjectAuthoringSeverity.Error, report.Issues[i].Severity);
                    StringAssert.Contains("$.staticColliderSpecs['body.650.collider.2'].transform", report.Issues[i].Path);
                    StringAssert.Contains("reason=", report.Issues[i].Message);
                }
                Assert.IsTrue(found);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WorldObjectModule_TryPrepareSaveBlocksInvalidLiveTransformBeforeAnyWrite()
        {
            var managerObject = new GameObject("GameManager Prepare Host");
            GameManager manager = managerObject.AddComponent<GameManager>();
            GameObject formalRoot = CreateChild(managerObject.transform, "Formal Root");
            GameObject box = CreateChild(formalRoot.transform, "Invalid Save Box");
            var module = new CharacterTestWorldObjectLevelEditorModule();
            try
            {
                typeof(GameManager).GetField("_worldObjectSceneRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(manager, formalRoot.transform);
                var document = new LevelWorldObjectAuthoringDocument
                {
                    LevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                    ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                    RootPath = formalRoot.name,
                    SingleStaticColliders = new List<LevelWorldObjectInstanceDefinition>
                    {
                        new LevelWorldObjectInstanceDefinition
                        {
                            Enabled = true,
                            Role = LevelWorldObjectRole.StaticCollider,
                            DefinitionId = CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            TransformRef = CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(formalRoot.transform, box.transform),
                            ScaleSource = LevelWorldObjectScaleSource.SceneTransform,
                            BodyId = 660,
                            ColliderId = 3
                        }
                    }
                };
                module.OnEnable();
                Assert.IsFalse(module.ContentProjectionInitializedForTests);
                Assert.IsTrue(module.DefinitionCompileSucceededForTests);
                module.Reload();
                Assert.IsFalse(module.ContentProjectionInitializedForTests);
                Assert.IsTrue(module.DefinitionCompileSucceededForTests);
                module.SetDocumentForTests(document);
                int cacheCountBefore = manager.WorldObjectSceneCache.StaticColliderInstanceCount;
                string fullPath = Path.GetFullPath(Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    module.DocumentPath));
                byte[] diskBefore = File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
                box.transform.localScale = new Vector3(-1f, 1f, 1f);
                module.NotifySceneTransformChanged(box.transform);
                bool sceneDirtyBefore = box.scene.isDirty;

                bool prepared = ((IMxLevelEditorSaveParticipant)module).TryPrepareSave(out string failureMessage);

                Assert.IsFalse(prepared);
                Assert.IsTrue(module.IsDirty);
                Assert.AreEqual(cacheCountBefore, manager.WorldObjectSceneCache.StaticColliderInstanceCount);
                Assert.AreEqual(sceneDirtyBefore, box.scene.isDirty);
                Assert.IsFalse(string.IsNullOrWhiteSpace(failureMessage));
                bool found = false;
                for (int i = 0; i < module.ValidationReportForTests.Issues.Count; i++)
                {
                    LevelWorldObjectAuthoringIssue issue = module.ValidationReportForTests.Issues[i];
                    if (issue.Code != CharacterTestCombatLevelSceneAdapter.InvalidTransformScaleCode)
                        continue;
                    found = true;
                    Assert.AreEqual(LevelWorldObjectAuthoringSeverity.Error, issue.Severity);
                    Assert.AreEqual("$.staticColliderSpecs['body.660.collider.3'].transform", issue.Path);
                }
                Assert.IsTrue(found);
                if (diskBefore == null)
                    Assert.IsFalse(File.Exists(fullPath));
                else
                    CollectionAssert.AreEqual(diskBefore, File.ReadAllBytes(fullPath));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void LevelEditorModel_AddTransformsCreatesDocumentRowsAndPreventsDuplicates()
        {
            GameObject root = new GameObject("CharacterTestSceneRoot");
            CharacterTestWorldObjectSceneSource source =
                root.AddComponent<CharacterTestWorldObjectSceneSource>();
            GameObject movableObject = CreateChild(root.transform, "MovableNaturalObject_sphere_visual");
            GameObject staticObject = CreateChild(root.transform, "PhysicsProbe_SphereTarget");

            try
            {
                var document = new LevelWorldObjectAuthoringDocument
                {
                    LevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                    ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                    RootPath = root.name
                };

                int firstAdd = CharacterTestWorldObjectLevelEditorModel.AddTransforms(
                    document,
                    source,
                    new[] { movableObject.transform },
                    LevelWorldObjectRole.Movable,
                    CharacterTestWorldObjectDefinitionIds.MovableSphere);
                CharacterTestWorldObjectLevelEditorModel.AddRowsResult duplicateAdd =
                    CharacterTestWorldObjectLevelEditorModel.AddTransformsDetailed(
                    document,
                    source,
                    new[] { movableObject.transform },
                    LevelWorldObjectRole.Movable,
                    CharacterTestWorldObjectDefinitionIds.MovableSphere);
                int staticAdd = CharacterTestWorldObjectLevelEditorModel.AddTransforms(
                    document,
                    source,
                    new[] { staticObject.transform },
                    LevelWorldObjectRole.StaticCollider,
                    CharacterTestWorldObjectDefinitionIds.StaticSphereProbe);

                Assert.AreEqual(1, firstAdd);
                Assert.AreEqual(0, duplicateAdd.Added);
                Assert.AreEqual(1, duplicateAdd.SkippedDuplicate);
                Assert.AreEqual(1, staticAdd);
                Assert.AreEqual(2, document.Instances.Count);
                Assert.AreEqual(1, document.MovableInstances.Count);
                Assert.AreEqual(1, document.SingleStaticColliders.Count);
                Assert.AreEqual(LevelWorldObjectRole.Movable, document.Instances[0].Role);
                Assert.AreEqual(CharacterTestWorldObjectDefinitionIds.MovableSphere, document.Instances[0].DefinitionId);
                Assert.AreEqual(1, document.Instances[0].NumericInstanceId);
                Assert.AreEqual(LevelWorldObjectRole.StaticCollider, document.Instances[1].Role);
                Assert.AreEqual(CharacterTestWorldObjectDefinitionIds.StaticSphereProbe, document.Instances[1].DefinitionId);
                Assert.AreEqual(10000, document.Instances[1].BodyId);
                Assert.AreEqual(1, document.Instances[1].ColliderId);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LevelEditorModel_AddStaticBatchesCreatesContainerRowsAndPreventsDuplicates()
        {
            GameObject root = new GameObject("CharacterTestSceneRoot");
            CharacterTestWorldObjectSceneSource source =
                root.AddComponent<CharacterTestWorldObjectSceneSource>();
            GameObject container = CreateChild(root.transform, "StaticBatch");
            CreateChild(container.transform, "BlockA");
            CreateChild(container.transform, "BlockB");

            try
            {
                var document = new LevelWorldObjectAuthoringDocument
                {
                    LevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                    ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                    RootPath = root.name
                };

                int firstAdd = CharacterTestWorldObjectLevelEditorModel.AddStaticBatches(
                    document,
                    source,
                    new[] { container.transform },
                    CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                    1);
                CharacterTestWorldObjectLevelEditorModel.AddRowsResult duplicateAdd =
                    CharacterTestWorldObjectLevelEditorModel.AddStaticBatchesDetailed(
                    document,
                    source,
                    new[] { container.transform },
                    CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                    1);

                Assert.AreEqual(1, firstAdd);
                Assert.AreEqual(0, duplicateAdd.Added);
                Assert.AreEqual(1, duplicateAdd.SkippedDuplicate);
                Assert.AreEqual(1, document.StaticColliderBatches.Count);
                Assert.AreEqual(10000, document.StaticColliderBatches[0].BodyIdStart);
                Assert.AreEqual("StaticBatch[0]", document.StaticColliderBatches[0].ContainerTransformRef.Path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LevelEditorModel_AddStaticBatchesAllocatesAfterExistingStaticIds()
        {
            GameObject root = new GameObject("CharacterTestSceneRoot");
            CharacterTestWorldObjectSceneSource source =
                root.AddComponent<CharacterTestWorldObjectSceneSource>();
            GameObject single = CreateChild(root.transform, "SingleStatic");
            GameObject firstContainer = CreateChild(root.transform, "FirstBatch");
            CreateChild(firstContainer.transform, "BlockA");
            CreateChild(firstContainer.transform, "BlockB");
            GameObject secondContainer = CreateChild(root.transform, "SecondBatch");
            CreateChild(secondContainer.transform, "BlockC");

            try
            {
                var document = new LevelWorldObjectAuthoringDocument
                {
                    LevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                    ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                    RootPath = root.name
                };

                int staticAdd = CharacterTestWorldObjectLevelEditorModel.AddTransforms(
                    document,
                    source,
                    new[] { single.transform },
                    LevelWorldObjectRole.StaticCollider,
                    CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary);
                int batchAdd = CharacterTestWorldObjectLevelEditorModel.AddStaticBatches(
                    document,
                    source,
                    new[] { firstContainer.transform, secondContainer.transform },
                    CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                    1);

                Assert.AreEqual(1, staticAdd);
                Assert.AreEqual(2, batchAdd);
                Assert.AreEqual(10000, document.SingleStaticColliders[0].BodyId);
                Assert.AreEqual(10001, document.StaticColliderBatches[0].BodyIdStart);
                Assert.AreEqual(10003, document.StaticColliderBatches[1].BodyIdStart);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LevelEditorModel_AddPointsAndRegionsCreatesSemanticRowsAndPreventsDuplicates()
        {
            GameObject root = new GameObject("CharacterTestSceneRoot");
            CharacterTestWorldObjectSceneSource source =
                root.AddComponent<CharacterTestWorldObjectSceneSource>();
            GameObject pointObject = CreateChild(root.transform, "PlayerSpawn");
            GameObject regionObject = CreateChild(root.transform, "StartRegion");

            try
            {
                var document = new LevelWorldObjectAuthoringDocument
                {
                    LevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                    ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                    RootPath = root.name
                };

                CharacterTestWorldObjectLevelEditorModel.AddRowsResult pointAdd =
                    CharacterTestWorldObjectLevelEditorModel.AddPointsDetailed(
                        document,
                        source,
                        new[] { pointObject.transform },
                        CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn);
                CharacterTestWorldObjectLevelEditorModel.AddRowsResult duplicatePoint =
                    CharacterTestWorldObjectLevelEditorModel.AddPointsDetailed(
                        document,
                        source,
                        new[] { pointObject.transform },
                        CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn);
                CharacterTestWorldObjectLevelEditorModel.AddRowsResult regionAdd =
                    CharacterTestWorldObjectLevelEditorModel.AddRegionsDetailed(
                        document,
                        source,
                        new[] { regionObject.transform },
                        CharacterTestWorldObjectDefinitionIds.RegionStartArea);

                Assert.AreEqual(1, pointAdd.Added);
                Assert.AreEqual(0, duplicatePoint.Added);
                Assert.AreEqual(1, duplicatePoint.SkippedDuplicate);
                Assert.AreEqual(1, regionAdd.Added);
                Assert.AreEqual(1, document.Points.Count);
                Assert.AreEqual(1, document.Regions.Count);
                Assert.AreEqual(
                    LevelWorldObjectPointKind.PlayerSpawn,
                    document.Points[0].PointKind);
                Assert.AreEqual(
                    LevelWorldObjectRegionKind.TestRegion,
                    document.Regions[0].RegionKind);
                Assert.IsFalse(string.IsNullOrWhiteSpace(document.Points[0].StableId));
                Assert.IsFalse(string.IsNullOrWhiteSpace(document.Regions[0].StableId));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OfficialCharacterTestScene_WorldObjectAuthoringAndExecutableCacheRespectP1Boundary()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "CharacterTestScene should open for world object authoring tests.");

            string fullPath = ToFullPath(CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath);
            Assert.IsTrue(File.Exists(fullPath), "CharacterTest world object JSON should exist.");
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
            Assert.AreEqual(MxLevelAuthoringDocument.CurrentFormat, envelope.Format);
            Assert.IsTrue(CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                envelope,
                out LevelWorldObjectAuthoringDocument document,
                out LevelWorldObjectAuthoringValidationReport extractReport),
                JoinIssues(extractReport));

            GameManager gameManager = FindGameManagerForTests();
            CharacterTestWorldObjectSceneCache cache = gameManager.WorldObjectSceneCache;
            CharacterTestWorldObjectSceneSource[] sources =
                Object.FindObjectsByType<CharacterTestWorldObjectSceneSource>(UnityEngine.FindObjectsInactive.Exclude);
            Assert.AreEqual(
                0,
                sources.Length,
                "Official CharacterTestScene should use GameManager scene cache instead of a world object scene source.");

            LevelWorldObjectAuthoringValidationReport report =
                CharacterTestWorldObjectAuthoringAdapter.ValidateEnvelopeForCache(
                    envelope,
                    cache,
                    gameManager.WorldObjectSceneRoot,
                    CharacterTestWorldObjectSceneSource.BuiltInCatalog);
            Assert.IsFalse(report.HasErrors, JoinIssues(report));
            Assert.AreEqual(8, document.MovableInstances.Count);
            // M4-T2 formally adds the 15/30/50-degree slope rows as three single statics.
            Assert.AreEqual(16, document.SingleStaticColliders.Count);
            Assert.AreEqual(3, document.StaticColliderBatches.Count);
            Assert.AreEqual(22, document.Points.Count);
            Assert.AreEqual(5, document.Regions.Count);
            // These rows are owned by the world-object module, so its existing executable
            // Scene Cache must stay synchronized. Genesis P1 still does not build a separate
            // Genesis executable cache or claim SceneRunnable authority.
            Assert.AreEqual(8, cache.MovableInstanceCount);
            Assert.AreEqual(39, cache.StaticColliderInstanceCount);
            Assert.AreEqual(22, cache.PointInstanceCount);
            Assert.AreEqual(5, cache.RegionInstanceCount);
            CharacterTestCharacterSpawnSpec[] spawnSpecs =
                cache.CollectCharacterSpawnSpecs(out string[] spawnDiagnostics);
            Assert.AreEqual(0, spawnDiagnostics.Length, string.Join("\n", spawnDiagnostics));
            Assert.AreEqual(6, spawnSpecs.Length);
            Assert.AreEqual(
                MxLevelAuthoringJson.ComputeContentHash(envelope),
                cache.AuthoringContentHash,
                "The world-object owner cache must match its authoring envelope; this does not build the separate Genesis P2 cache.");
            Assert.AreEqual(
                CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath,
                cache.AuthoringJsonPath);
        }

        [Test]
        public void OfficialCharacterTestScene_InitialMonoBehavioursOnlyGameManager()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "CharacterTestScene should open for MonoBehaviour entry tests.");

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            var unexpected = new List<string>();
            int gameManagerCount = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    unexpected.Add("<missing script>");
                    continue;
                }

                if (behaviour is GameManager)
                {
                    gameManagerCount++;
                    continue;
                }

                string typeName = behaviour.GetType().FullName;
                if (string.Equals(
                        typeName,
                        "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData",
                        StringComparison.Ordinal)
                    || string.Equals(
                        typeName,
                        "UnityEngine.Rendering.Volume",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                unexpected.Add(typeName + " on " + behaviour.gameObject.name);
            }

            Assert.AreEqual(1, gameManagerCount, "CharacterTestScene should contain one GameManager MonoBehaviour.");
            Assert.AreEqual(
                0,
                unexpected.Count,
                "Official CharacterTestScene should only ship GameManager and approved URP bootstrap adapters: "
                + string.Join(", ", unexpected));
        }
    }
}
