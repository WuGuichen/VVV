using System.Collections.Generic;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestWorldObjectLevelAuthoringTests
    {
        [Test]
        public void P2_2_ContentProjection_FindsAndResolvesFormalWorldObjectPrefab()
        {
            var projection = new CharacterTestWorldObjectContentProjection();

            IReadOnlyList<ResourceCatalogEntry> entries =
                projection.FindPrefabEntries("world_object", "sample.level_world_object");
            Assert.AreEqual(1, entries.Count);

            ResourceKey key = entries[0].CreateKey("mxframework.samples");
            Assert.IsTrue(projection.TryResolvePrefab(key, out GameObject prefab, out string diagnostic));
            Assert.IsNotNull(prefab);
            Assert.IsTrue(string.IsNullOrEmpty(diagnostic));
            Assert.AreNotEqual(PrefabAssetType.NotAPrefab, PrefabUtility.GetPrefabAssetType(prefab));
        }

        [Test]
        public void P2_2_ContentProjection_MissingPrefabDoesNotFallBackToPrimitive()
        {
            var projection = new CharacterTestWorldObjectContentProjection();
            var missing = new ResourceKey(
                "art.level.world_object.missing",
                ResourceTypeIds.GameObject,
                string.Empty,
                "mxframework.samples");

            Assert.IsFalse(projection.TryResolvePrefab(missing, out GameObject prefab, out string diagnostic));
            Assert.IsNull(prefab);
            StringAssert.Contains("不存在", diagnostic);
        }

        [Test]
        public void P2_2_DefinitionWorkspace_UsesVirtualizedStructuredDefinitionRows()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(null, module, () => { });

            IReadOnlyList<MxLevelEditorTreeNode> tree = module.BuildTree(context);
            IReadOnlyList<MxLevelEditorListRow> rows = module.BuildRows(context, "definitions");

            Assert.That(tree, Has.Some.Matches<MxLevelEditorTreeNode>(
                node => node.Id == "definitions"));
            Assert.AreEqual(22, rows.Count);
            Assert.That(rows[0].Key, Does.StartWith("definition:"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(rows[0].SearchText));
        }

        [Test]
        public void P2_2_Placement_CreatesConnectedPrefabInstanceAndRegistersRow()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameManager gameManager = FindGameManagerForTests();
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(null, module, () => { });
            int rowsBefore = module.BuildRows(context, "movables").Count;
            CharacterTestWorldObjectLevelEditorModule.PlacementPreviewItem preview =
                module.BuildPlacementPreviewForTests(new Vector3(31f, 1f, 32f));
            Assert.IsNotNull(preview.PreviewObject);
            Assert.AreNotEqual(
                PrefabAssetType.NotAPrefab,
                PrefabUtility.GetPrefabAssetType(preview.PreviewObject));
            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult result = null;
            try
            {
                result = module.PlaceObjectForTests(
                    CharacterTestWorldObjectLevelEditorModule.PlacementRole.Movable,
                    CharacterTestWorldObjectDefinitionIds.MovableSlickLight,
                    new Vector3(31f, 1f, 32f));

                Assert.IsTrue(result.Success, result.Status);
                Assert.IsNotNull(result.GameObject);
                Assert.AreEqual(gameManager.WorldObjectSceneRoot, result.GameObject.transform.parent);
                Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(result.GameObject));
                Assert.AreEqual(rowsBefore + 1, module.BuildRows(context, "movables").Count);
            }
            finally
            {
                Selection.activeObject = null;
                DestroyPlacementObject(result);
            }
        }
    }
}
