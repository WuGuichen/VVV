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
        public void LevelEditorModule_BuildsStructuredRowsWithoutOpeningWindow()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();

            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });
            var tree = module.BuildTree(context);
            var rows = module.BuildRows(context, "movables");

            Assert.IsNotNull(tree);
            Assert.IsNotNull(rows);
            Assert.GreaterOrEqual(tree.Count, 5);
            Assert.AreEqual(8, rows.Count);
            Assert.AreEqual("可移动物体", rows[0].Kind);
            Assert.IsFalse(string.IsNullOrWhiteSpace(rows[0].Key));
            Assert.IsFalse(string.IsNullOrWhiteSpace(rows[0].Definition));
            Assert.IsFalse(string.IsNullOrWhiteSpace(rows[0].Transform));
            Assert.IsFalse(string.IsNullOrWhiteSpace(rows[0].SearchText));
        }

        [Test]
        public void LevelEditorModule_StructuredRowKeysStayStableAcrossRebuilds()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });

            IReadOnlyList<MxLevelEditorListRow> rows = module.BuildRows(context, "movables");
            MxLevelEditorListRow sphere = FindRowByDefinition(rows, "球体");
            Assert.IsNotNull(sphere);
            Assert.IsTrue(sphere.Key.StartsWith("movable:", System.StringComparison.Ordinal));
            Assert.That(sphere.SearchText.ToLowerInvariant(), Does.Contain("sphere"));

            module.Validate();
            module.BuildRows(context, "static.single");
            IReadOnlyList<MxLevelEditorListRow> rebuiltRows = module.BuildRows(context, "movables");
            MxLevelEditorListRow rebuiltSphere = FindRowByDefinition(rebuiltRows, "球体");

            Assert.IsNotNull(rebuiltSphere);
            Assert.AreEqual(sphere.Key, rebuiltSphere.Key);
        }

        [Test]
        public void LevelEditorWindow_SearchFiltersRowsAndClearRestoresScope()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MxLevelEditorWindow window = MxLevelEditorWindow.OpenWindow();
            try
            {
                window.CreateGUI();
                SelectWorldObjectsModule(window);

                TextField search = window.rootVisualElement.Q<TextField>("mx-level-editor-search-field");
                ListView rowView = window.rootVisualElement.Q<ListView>("mx-level-editor-row-view");
                Label rangeCount = window.rootVisualElement.Q<Label>("mx-level-editor-range-count");
                Assert.IsNotNull(search);
                Assert.IsNotNull(rowView);
                Assert.IsNotNull(rangeCount);

                int totalRows = CountListViewItems(rowView);
                Assert.Greater(totalRows, 1);

                search.value = "capsule movable";
                Assert.AreEqual(1, CountListViewItems(rowView));
                rangeCount = window.rootVisualElement.Q<Label>("mx-level-editor-range-count");
                Assert.That(rangeCount.text, Does.Contain("1/"));

                search.value = string.Empty;
                Assert.AreEqual(totalRows, CountListViewItems(rowView));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void LevelEditorModule_SelectionSyncResolvesRowsAndBatchChildren()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });

            IReadOnlyList<MxLevelEditorListRow> movableRows = module.BuildRows(context, "movables");
            MxLevelEditorListRow capsule = FindRowByDefinition(movableRows, "胶囊体");
            Assert.IsNotNull(capsule);
            Assert.IsTrue(module.TryResolveRowObject(context, capsule, out Object capsuleObject));
            Assert.IsNotNull(capsuleObject);
            Assert.IsTrue(module.TryResolveSelection(context, capsuleObject, out MxLevelEditorSelectionTarget capsuleTarget));
            Assert.AreEqual("movables", capsuleTarget.TreeNodeId);
            Assert.AreEqual(capsule.Key, capsuleTarget.RowKey);

            IReadOnlyList<MxLevelEditorListRow> batchRows = module.BuildRows(context, "static.batches");
            Assert.Greater(batchRows.Count, 0);
            Assert.IsTrue(module.TryResolveRowObject(context, batchRows[0], out Object batchContainerObject));
            var batchContainer = batchContainerObject as GameObject;
            Assert.IsNotNull(batchContainer);
            Assert.Greater(batchContainer.transform.childCount, 0);

            GameObject directChild = batchContainer.transform.GetChild(0).gameObject;
            Assert.IsTrue(module.TryResolveSelection(context, directChild, out MxLevelEditorSelectionTarget batchTarget));
            Assert.AreEqual("static.batches", batchTarget.TreeNodeId);
            Assert.AreEqual(batchRows[0].Key, batchTarget.RowKey);
        }

        [Test]
        public void LevelEditorModule_SceneOverlayBuildsSelectedAndScopeItems()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });

            IReadOnlyList<MxLevelEditorListRow> rows = module.BuildRows(context, "document");
            MxLevelEditorListRow capsule = FindRowByDefinition(rows, "胶囊体");
            Assert.IsNotNull(capsule);

            IReadOnlyList<CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem> items =
                module.BuildSceneOverlayItemsForTests(
                    new MxLevelEditorSceneViewContext(
                        context,
                        "document",
                        capsule,
                        rows));

            Assert.Greater(items.Count, rows.Count);
            AssertSceneOverlayContainsKind(items, "可移动物体");
            AssertSceneOverlayContainsKind(items, "静态碰撞体");
            AssertSceneOverlayContainsKind(items, "静态碰撞体批次");
            AssertSceneOverlayContainsKind(items, "关卡点");
            AssertSceneOverlayContainsKind(items, "区域");

            CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem selected =
                FindSceneOverlayItem(items, capsule.Key);
            Assert.IsNotNull(selected);
            Assert.IsTrue(selected.IsSelected);
            Assert.AreEqual(CharacterTestMovableNaturalObjectShapeKind.Capsule, selected.ShapeKind);
            Assert.IsNotNull(selected.Transform);
            Assert.IsFalse(string.IsNullOrWhiteSpace(selected.Label));
        }

        [Test]
        public void LevelEditorModule_SceneOverlayExpandsBatchDirectChildrenOnly()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });

            IReadOnlyList<MxLevelEditorListRow> batchRows = module.BuildRows(context, "static.batches");
            Assert.Greater(batchRows.Count, 0);
            Assert.IsTrue(module.TryResolveRowObject(context, batchRows[0], out Object batchContainerObject));
            var batchContainer = batchContainerObject as GameObject;
            Assert.IsNotNull(batchContainer);

            IReadOnlyList<CharacterTestWorldObjectLevelEditorModule.SceneOverlayItem> items =
                module.BuildSceneOverlayItemsForTests(
                    new MxLevelEditorSceneViewContext(
                        context,
                        "static.batches",
                        batchRows[0],
                        new[] { batchRows[0] }));

            Assert.AreEqual(batchContainer.transform.childCount, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                Assert.AreEqual(batchRows[0].Key, items[i].RowKey);
                Assert.AreEqual("静态碰撞体批次", items[i].Kind);
                Assert.IsTrue(items[i].IsSelected);
                Assert.AreEqual(batchContainer.transform, items[i].Transform.parent);
            }
        }

        [Test]
        public void SceneCoverage_ReportsOnlyTopLevelUnregisteredSubtrees()
        {
            var root = new GameObject("LevelRoot");
            var structuralGroup = new GameObject("StructuralGroup");
            structuralGroup.transform.SetParent(root.transform, worldPositionStays: false);
            var managed = new GameObject("Managed");
            managed.transform.SetParent(structuralGroup.transform, worldPositionStays: false);
            var managedVisual = new GameObject("ManagedVisual");
            managedVisual.transform.SetParent(managed.transform, worldPositionStays: false);
            var unregistered = new GameObject("Unregistered");
            unregistered.transform.SetParent(root.transform, worldPositionStays: false);
            var unregisteredVisual = new GameObject("UnregisteredVisual");
            unregisteredVisual.transform.SetParent(unregistered.transform, worldPositionStays: false);
            var output = new List<Transform>();

            try
            {
                MxLevelEditorSceneCoverage.CollectUnregisteredRoots(
                    root.transform,
                    new[] { managed.transform },
                    output);

                Assert.AreEqual(1, output.Count);
                Assert.AreEqual(unregistered.transform, output[0]);
                Assert.IsFalse(output.Contains(structuralGroup.transform));
                Assert.IsFalse(output.Contains(managedVisual.transform));
                Assert.IsFalse(output.Contains(unregisteredVisual.transform));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LevelEditorModule_RegisteredTransformChangeMarksSceneUnsaved()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });
            MxLevelEditorListRow row = module.BuildRows(context, "movables")[0];
            Assert.IsTrue(module.TryResolveRowObject(context, row, out Object rowObject));
            var gameObject = rowObject as GameObject;
            Assert.IsNotNull(gameObject);

            Assert.IsTrue(module.NotifySceneTransformChanged(gameObject.transform));
            Assert.IsTrue(module.IsDirty);
            Assert.That(module.Status, Does.Contain(gameObject.name));
        }

        [Test]
        public void LevelEditorModule_NewRootObjectAppearsInUnregisteredAction()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var worldModule = new CharacterTestWorldObjectLevelEditorModule();
            var characterModule = new CharacterTestSceneCharacterLevelEditorModule();
            worldModule.OnEnable();
            characterModule.OnEnable();
            var managed = new List<Transform>();
            worldModule.CollectManagedSceneTransforms(managed);
            characterModule.CollectManagedSceneTransforms(managed);
            worldModule.RefreshUnregisteredSceneObjectsForTests(managed);
            int initialCount = worldModule.UnregisteredSceneObjectCountForTests;
            GameManager gameManager = FindGameManagerForTests();
            var probe = new GameObject("NewUnregisteredRootObject");
            probe.transform.SetParent(gameManager.WorldObjectSceneRoot, worldPositionStays: false);

            try
            {
                worldModule.RefreshUnregisteredSceneObjectsForTests(managed);
                Assert.AreEqual(
                    initialCount + 1,
                    worldModule.UnregisteredSceneObjectCountForTests);

                var toolbar = new VisualElement();
                worldModule.BuildModuleToolbar(null, toolbar);
                Button action = toolbar.Q<Button>("character-test-unregistered-scene-objects");
                Assert.IsNotNull(action);
                Assert.That(action.text, Does.Contain((initialCount + 1).ToString()));
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }
    }
}
