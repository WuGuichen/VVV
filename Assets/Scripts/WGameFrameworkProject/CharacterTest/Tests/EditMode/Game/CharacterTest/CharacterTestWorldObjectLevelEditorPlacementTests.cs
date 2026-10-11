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
        public void LevelEditorModule_PlacementPreviewUsesRoleDefinitionShape()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();

            module.ConfigurePlacementForTests(
                CharacterTestWorldObjectLevelEditorModule.PlacementRole.Movable,
                CharacterTestWorldObjectDefinitionIds.MovableCapsule,
                active: true);
            CharacterTestWorldObjectLevelEditorModule.PlacementPreviewItem capsule =
                module.BuildPlacementPreviewForTests(new Vector3(2f, 0f, 3f));

            Assert.IsNotNull(capsule);
            Assert.AreEqual(CharacterTestWorldObjectLevelEditorModule.PlacementRole.Movable, capsule.Role);
            Assert.AreEqual(CharacterTestWorldObjectDefinitionIds.MovableCapsule, capsule.DefinitionId);
            Assert.AreEqual(CharacterTestMovableNaturalObjectShapeKind.Capsule, capsule.ShapeKind);
            Assert.AreEqual(new Vector3(2f, 0f, 3f), capsule.Position);
            Assert.Greater(capsule.HalfExtents.x, 0f);

            module.ConfigurePlacementForTests(
                CharacterTestWorldObjectLevelEditorModule.PlacementRole.Point,
                CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn,
                active: true);
            CharacterTestWorldObjectLevelEditorModule.PlacementPreviewItem point =
                module.BuildPlacementPreviewForTests(Vector3.one);

            Assert.IsNotNull(point);
            Assert.AreEqual(CharacterTestWorldObjectLevelEditorModule.PlacementRole.Point, point.Role);
            Assert.AreEqual(CharacterTestMovableNaturalObjectShapeKind.Aabb, point.ShapeKind);
        }

        [Test]
        public void LevelEditorModule_PlacementCreatesSceneObjectsAndAuthoringRows()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameManager gameManager = FindGameManagerForTests();
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });
            int movableRowsBefore = module.BuildRows(context, "movables").Count;
            int staticRowsBefore = module.BuildRows(context, "static.single").Count;
            int pointRowsBefore = module.BuildRows(context, "points").Count;

            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult movable = null;
            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult stat = null;
            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult point = null;
            try
            {
                movable = module.PlaceObjectForTests(
                    CharacterTestWorldObjectLevelEditorModule.PlacementRole.Movable,
                    CharacterTestWorldObjectDefinitionIds.MovableSphere,
                    new Vector3(21f, 0f, 22f));
                stat = module.PlaceObjectForTests(
                    CharacterTestWorldObjectLevelEditorModule.PlacementRole.Static,
                    CharacterTestWorldObjectDefinitionIds.StaticCapsuleProbe,
                    new Vector3(23f, 0f, 24f));
                point = module.PlaceObjectForTests(
                    CharacterTestWorldObjectLevelEditorModule.PlacementRole.Point,
                    CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn,
                    new Vector3(25f, 0f, 26f));

                AssertPlacementCreated(movable, gameManager.WorldObjectSceneRoot, PrimitiveType.Sphere);
                AssertPlacementCreated(stat, gameManager.WorldObjectSceneRoot, PrimitiveType.Capsule);
                AssertPlacementCreated(point, gameManager.WorldObjectSceneRoot, null);
                Assert.AreEqual(point.GameObject, Selection.activeGameObject);
                Assert.GreaterOrEqual(movable.FirstAssignedId, 121);
                Assert.GreaterOrEqual(stat.FirstAssignedId, 10000);

                IReadOnlyList<MxLevelEditorListRow> movableRows = module.BuildRows(context, "movables");
                IReadOnlyList<MxLevelEditorListRow> staticRows = module.BuildRows(context, "static.single");
                IReadOnlyList<MxLevelEditorListRow> pointRows = module.BuildRows(context, "points");
                Assert.AreEqual(movableRowsBefore + 1, movableRows.Count);
                Assert.AreEqual(staticRowsBefore + 1, staticRows.Count);
                Assert.AreEqual(pointRowsBefore + 1, pointRows.Count);
                Assert.IsNotNull(FindRowByKey(movableRows, movable.RowKey));
                Assert.IsNotNull(FindRowByKey(staticRows, stat.RowKey));
                Assert.IsNotNull(FindRowByKey(pointRows, point.RowKey));
            }
            finally
            {
                Selection.activeObject = null;
                DestroyPlacementObject(movable);
                DestroyPlacementObject(stat);
                DestroyPlacementObject(point);
            }
        }

        [Test]
        public void LevelEditorPlacementGrid_SnapsOnlyWorldXAndZ()
        {
            Vector3 previous = EditorSnapSettings.move;
            try
            {
                EditorSnapSettings.move = new Vector3(0.5f, 0.25f, 2f);

                Vector3 snapped = CharacterTestLevelEditorPlacementUtility.ApplyHorizontalGrid(
                    new Vector3(0.74f, 1.37f, 2.9f),
                    enabled: true);

                Assert.AreEqual(0.5f, snapped.x, 0.0001f);
                Assert.AreEqual(1.37f, snapped.y, 0.0001f);
                Assert.AreEqual(2f, snapped.z, 0.0001f);
            }
            finally
            {
                EditorSnapSettings.move = previous;
            }
        }

        [UnityTest]
        public IEnumerator LevelEditorModule_DuplicateAndRepositionShareOneStepUndoRedo()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(null, module, () => { });
            IReadOnlyList<MxLevelEditorListRow> rows = module.BuildRows(context, "movables");
            Assert.IsNotEmpty(rows);
            MxLevelEditorListRow sourceRow = rows[0];
            Assert.IsTrue(module.TryResolveRowObject(context, sourceRow, out Object sourceObject));
            var source = sourceObject as GameObject;
            Assert.IsNotNull(source);

            int countBefore = rows.Count;
            Vector3 duplicatePosition = source.transform.position + new Vector3(3f, 1f, 4f);
            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult duplicate =
                module.DuplicateRowForTests(sourceRow, duplicatePosition);

            Assert.IsTrue(duplicate.Success, duplicate.Status);
            Assert.IsNotNull(duplicate.GameObject);
            Assert.AreNotSame(source, duplicate.GameObject);
            Assert.AreNotSame(source.transform, duplicate.GameObject.transform);
            Assert.AreEqual(source.transform.rotation, duplicate.GameObject.transform.rotation);
            Assert.AreEqual(source.transform.localScale, duplicate.GameObject.transform.localScale);
            Assert.AreNotEqual(sourceRow.Key, duplicate.RowKey);
            Assert.AreEqual(
                countBefore + 1,
                module.BuildRows(context, "movables").Count,
                "Duplicate should add one authoring row.");
            yield return null;

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                countBefore,
                module.BuildRows(context, "movables").Count,
                "Undo should remove the duplicated authoring row.");
            Assert.IsTrue(duplicate.GameObject == null);

            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(
                countBefore + 1,
                module.BuildRows(context, "movables").Count,
                "Redo should restore the duplicated authoring row.");
            Assert.IsTrue(duplicate.GameObject != null);

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(
                countBefore,
                module.BuildRows(context, "movables").Count,
                "Second undo should remove the redone duplicated authoring row.");

            Vector3 originalPosition = source.transform.position;
            Vector3 repositioned = originalPosition + new Vector3(5f, 2f, 6f);
            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult moved =
                module.RepositionRowForTests(sourceRow, repositioned);
            Assert.IsTrue(moved.Success, moved.Status);
            Assert.AreEqual(repositioned, source.transform.position);

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(originalPosition, source.transform.position);
            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(repositioned, source.transform.position);
            Undo.PerformUndo();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LevelEditorModule_DeleteRemovesAllDirectRegistrationsAndSupportsUndo()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameManager gameManager = FindGameManagerForTests();
            var sourceModule = new CharacterTestWorldObjectLevelEditorModule();
            sourceModule.OnEnable();
            var context = new MxLevelEditorContext(null, sourceModule, () => { });
            IReadOnlyList<MxLevelEditorListRow> sourceRows =
                sourceModule.BuildRows(context, "movables");
            Assert.IsNotEmpty(sourceRows);
            Assert.IsTrue(
                sourceModule.TryResolveRowObject(
                    context,
                    sourceRows[0],
                    out Object sourceObject));
            var source = sourceObject as GameObject;
            Assert.IsNotNull(source);

            LevelTransformReference transformRef =
                CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(
                    gameManager.WorldObjectSceneRoot,
                    source.transform);
            var document = new LevelWorldObjectAuthoringDocument
            {
                LevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                RootPath = gameManager.WorldObjectSceneRoot.name,
                Points = new List<LevelWorldObjectPointDefinition>
                {
                    new LevelWorldObjectPointDefinition
                    {
                        Enabled = true,
                        DefinitionId = CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn,
                        StableId = "delete.reference.a",
                        TransformRef = transformRef.Normalize(),
                        TransformPath = transformRef.Path
                    },
                    new LevelWorldObjectPointDefinition
                    {
                        Enabled = true,
                        DefinitionId = CharacterTestWorldObjectDefinitionIds.PointAiSpawn,
                        StableId = "delete.reference.b",
                        TransformRef = transformRef.Normalize(),
                        TransformPath = transformRef.Path
                    }
                }
            };
            sourceModule.SetDocumentForTests(document);
            IReadOnlyList<MxLevelEditorListRow> rows =
                sourceModule.BuildRows(context, "points");
            Assert.AreEqual(2, rows.Count);

            int removed = sourceModule.DeleteRowForTests(rows[0], out GameObject deleted);
            Assert.AreEqual(2, removed);
            Assert.IsTrue(deleted == null);
            Assert.AreEqual(0, sourceModule.BuildRows(context, "points").Count);
            yield return null;

            Undo.PerformUndo();
            yield return null;
            Assert.IsTrue(deleted != null);
            Assert.AreEqual(2, sourceModule.BuildRows(context, "points").Count);
        }

        [Test]
        public void LevelEditorModule_PlacementRejectsRoleDefinitionMismatch()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });
            int movableRowsBefore = module.BuildRows(context, "movables").Count;

            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult result =
                module.PlaceObjectForTests(
                    CharacterTestWorldObjectLevelEditorModule.PlacementRole.Movable,
                    CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                    Vector3.zero);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.GameObject);
            Assert.That(result.Status, Does.Contain("不支持"));
            Assert.AreEqual(movableRowsBefore, module.BuildRows(context, "movables").Count);
        }
    }
}
