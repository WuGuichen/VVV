using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.Combat.Physics;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestWorldObjectLevelAuthoringTests
    {
        [Test]
        public void LevelEditorModule_ReopeningAfterSceneSwitchLoadsCurrentDocument()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule();
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                module.OnEnable();
                Assert.AreEqual(
                    "character_test_scene",
                    MxLevelAuthoringJson.LoadFromJson(module.EnvelopeJsonForTests).LevelId);

                EditorSceneManager.OpenScene("Assets/Scenes/TacticalCardDemoScene.unity", OpenSceneMode.Single);
                module.OnEnable();
                Assert.AreEqual(
                    "tactical_card_demo",
                    MxLevelAuthoringJson.LoadFromJson(module.EnvelopeJsonForTests).LevelId);

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                module.OnEnable();
                Assert.AreEqual(
                    "character_test_scene",
                    MxLevelAuthoringJson.LoadFromJson(module.EnvelopeJsonForTests).LevelId);
                Assert.AreEqual(0, module.ValidationReportForTests.ErrorCount);
            }
            finally
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        [Test]
        public void LevelEditorModule_SelectionValidationSummarizesRegisteredMovable()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });

            MxLevelEditorListRow capsule = FindRowByDefinition(module.BuildRows(context, "movables"), "胶囊体");
            Assert.IsNotNull(capsule);
            Assert.IsTrue(module.TryResolveRowObject(context, capsule, out Object capsuleObject));

            CharacterTestWorldObjectLevelEditorModule.SelectionValidationSummary summary =
                module.BuildSelectionValidationSummaryForTests(capsule, capsuleObject);

            Assert.IsTrue(summary.IsRegistered);
            Assert.IsFalse(summary.IsAddableSelection);
            Assert.AreEqual("运行时就绪", summary.Status);
            Assert.AreEqual("可移动物体", summary.Role);
            Assert.That(summary.Definition, Does.Contain("胶囊体"));
            Assert.That(summary.RuntimeCache, Does.Contain("可移动物体出生配置"));
            Assert.That(summary.Shape, Does.Contain("胶囊体"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary.Material));
            Assert.That(summary.Potency, Does.Contain("D/P/S="));

            Selection.activeObject = capsuleObject;
            var inspector = new VisualElement();
            module.BuildInspector(context, capsule, inspector);
            Foldout advanced = inspector.Q<Foldout>("character-test-advanced-ids");
            Assert.IsNotNull(advanced);
            Assert.IsFalse(advanced.value);
            Assert.IsNotNull(
                inspector.Q<VisualElement>("character-test-world-object-quick-actions"));

            module.SetRuntimeInspectorForTests(showRuntimeInspector: true);
            module.BuildInspector(context, capsule, inspector);
            Assert.IsNotNull(inspector.Q("character-test-selection-validation"));
            Assert.IsNotNull(inspector.Q("character-test-runtime-participation"));
            Selection.activeObject = null;
        }

        [Test]
        public void LevelEditorModule_SelectionValidationSummarizesBatchChildRuntimePreview()
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
            Assert.Greater(batchContainer.transform.childCount, 0);

            GameObject directChild = batchContainer.transform.GetChild(0).gameObject;
            CharacterTestWorldObjectLevelEditorModule.SelectionValidationSummary summary =
                module.BuildSelectionValidationSummaryForTests(batchRows[0], directChild);

            Assert.IsTrue(summary.IsRegistered);
            Assert.IsTrue(summary.IsBatchChild);
            Assert.AreEqual("静态碰撞体批次", summary.Role);
            Assert.That(summary.RuntimeCache, Does.Contain("静态碰撞体缓存条目"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary.BodyIdPreview));
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary.ColliderIdPreview));
            Assert.That(summary.Status, Is.EqualTo("运行时就绪").Or.EqualTo("警告"));
        }

        [Test]
        public void LevelEditorModule_SelectionValidationShowsActionsForUnregisteredSelection()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameManager gameManager = FindGameManagerForTests();
            var probe = new GameObject("UnregisteredValidationProbe");
            probe.transform.SetParent(gameManager.WorldObjectSceneRoot, worldPositionStays: false);
            Selection.activeObject = probe;

            try
            {
                var module = new CharacterTestWorldObjectLevelEditorModule();
                module.OnEnable();
                var context = new MxLevelEditorContext(
                    null,
                    module,
                    () => { });
                var inspector = new VisualElement();
                module.BuildInspector(context, null, inspector);

                CharacterTestWorldObjectLevelEditorModule.SelectionValidationSummary summary =
                    module.BuildSelectionValidationSummaryForTests(null, probe);
                Assert.IsFalse(summary.IsRegistered);
                Assert.IsTrue(summary.IsAddableSelection);
                Assert.AreEqual("未登记", summary.Status);
                Assert.That(summary.RuntimeCache, Does.Contain("不会进入 CharacterTest 运行时缓存"));

                Assert.IsNotNull(inspector.Q("character-test-selection-validation"));
                Assert.IsNotNull(inspector.Q("character-test-selection-validation-actions"));
                Assert.IsNotNull(inspector.Q<Button>("character-test-validation-add-movable"));
                Assert.IsNotNull(inspector.Q<Button>("character-test-validation-add-static"));
                Assert.IsNotNull(inspector.Q<Button>("character-test-validation-create-batch"));
                Assert.IsNotNull(inspector.Q<Button>("character-test-validation-add-point"));
                Assert.IsNotNull(inspector.Q<Button>("character-test-validation-add-region"));
            }
            finally
            {
                Selection.activeObject = null;
                Object.DestroyImmediate(probe);
            }
        }

        [UnityTest]
        public IEnumerator LevelEditorWindow_ExternalSelectionClearsSearchAndRevealsRow()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });
            MxLevelEditorListRow capsule = FindRowByDefinition(module.BuildRows(context, "movables"), "胶囊体");
            Assert.IsNotNull(capsule);
            Assert.IsTrue(module.TryResolveRowObject(context, capsule, out Object capsuleObject));

            MxLevelEditorWindow window = MxLevelEditorWindow.OpenWindow();
            try
            {
                Selection.activeObject = null;
                window.CreateGUI();
                SelectWorldObjectsModule(window);

                TextField search = window.rootVisualElement.Q<TextField>("mx-level-editor-search-field");
                ListView rowView = window.rootVisualElement.Q<ListView>("mx-level-editor-row-view");
                Label status = window.rootVisualElement.Q<Label>("mx-level-editor-search-status");
                Assert.IsNotNull(search);
                Assert.IsNotNull(rowView);
                Assert.IsNotNull(status);

                search.value = "slick";
                Assert.Less(CountListViewItems(rowView), 7);

                Selection.activeObject = capsuleObject;
                window.RevealCurrentUnitySelection();
                yield return null;

                Assert.AreEqual(string.Empty, search.value);
                Assert.AreEqual("已清除搜索，以显示当前选中对象。", status.text);
                var selectedRow = rowView.selectedItem as MxLevelEditorListRow;
                Assert.IsNotNull(selectedRow);
                Assert.AreEqual(capsule.Key, selectedRow.Key);
            }
            finally
            {
                window.Close();
                Selection.activeObject = null;
            }
        }

        [Test]
        public void LevelEditorWindow_CreateGuiBuildsFrameworkShell()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MxLevelEditorWindow window = MxLevelEditorWindow.OpenWindow();
            try
            {
                window.CreateGUI();
                SelectWorldObjectsModule(window);

                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-header"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-compact-header"));
                Assert.AreEqual(
                    "世界对象",
                    window.rootVisualElement.Q<PopupField<string>>(
                        "mx-level-editor-module-popup").value);
                Assert.IsNull(
                    window.rootVisualElement.Q<Label>("mx-level-editor-range-title"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-common-toolbar"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-content"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-structured-content"));
                Assert.IsNotNull(window.rootVisualElement.Q<TwoPaneSplitView>("mx-level-editor-splitter-root"));
                Assert.IsNotNull(
                    window.rootVisualElement.Q<TwoPaneSplitView>("mx-level-editor-main-inspector-splitter"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-tree-pane"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-module-toolbar"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-row-panel"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-range-summary"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-search-bar"));
                TextField searchField =
                    window.rootVisualElement.Q<TextField>("mx-level-editor-search-field");
                Assert.IsNotNull(searchField);
                Assert.AreEqual("搜索", searchField.label);
                Assert.GreaterOrEqual(searchField.style.minWidth.value.value, 220f);
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("mx-level-editor-search-clear"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-inspector"));
                Assert.IsNotNull(window.rootVisualElement.Q("character-test-empty-inspector-actions"));
                Assert.IsNotNull(window.rootVisualElement.Q("character-test-selection-validation"));
                Assert.IsNotNull(
                    window.rootVisualElement.Q<ToolbarMenu>("mx-level-editor-tools-menu"));
                Assert.That(
                    window.rootVisualElement.Q<ToolbarMenu>(
                        "mx-level-editor-tools-menu").tooltip,
                    Does.Contain("B：切换 Scene / 关卡编辑器"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("mx-level-editor-reload"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("mx-level-editor-validate"));
                Button save =
                    window.rootVisualElement.Q<Button>("mx-level-editor-save");
                Assert.IsNotNull(save);
                Assert.AreEqual("保存", save.text);
                Assert.That(save.tooltip, Does.Contain("同步更新 Runtime"));
                Assert.IsNull(
                    window.rootVisualElement.Q<Button>("mx-level-editor-apply"));
                Assert.IsNotNull(
                    window.rootVisualElement.Q<ToolbarMenu>("character-test-world-object-add-menu"));
                Assert.AreEqual(
                    "添加当前选中",
                    window.rootVisualElement.Q<ToolbarMenu>(
                        "character-test-world-object-add-menu").text);
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("mx-level-editor-placement-mode"));
                Assert.IsNotNull(
                    window.rootVisualElement.Q<Button>("mx-level-editor-navigation-type-tab"));
                Assert.IsNotNull(
                    window.rootVisualElement.Q<Button>("mx-level-editor-navigation-group-tab"));
                Assert.IsNotNull(
                    window.rootVisualElement.Q<Button>("mx-level-editor-select-root"));
                ListView groupView =
                    window.rootVisualElement.Q<ListView>("mx-level-editor-group-tree-view");
                Assert.IsNotNull(groupView);
                Assert.AreEqual(DisplayStyle.None, groupView.style.display.value);

                Assert.IsNull(window.rootVisualElement.Q("mx-level-editor-placement-toolbar"));
                window.SetPlacementPopoverOpenForTests(true);
                Assert.IsTrue(window.PlacementPopoverOpenForTests);
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-placement-popover"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-placement-fields"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-placement-options"));
                Assert.IsNotNull(window.rootVisualElement.Q("mx-level-editor-placement-actions"));
                Assert.IsNotNull(window.rootVisualElement.Q<Button>("character-test-placement-toggle"));
                Assert.IsNotNull(window.rootVisualElement.Q<PopupField<string>>("character-test-placement-role"));
                Assert.IsNotNull(window.rootVisualElement.Q<PopupField<string>>("character-test-placement-definition"));
                Assert.IsNotNull(
                    window.rootVisualElement.Q<ToolbarToggle>("character-test-placement-grid-snap"));
                Toggle sceneEdit =
                    window.rootVisualElement.Q<Toggle>("mx-level-editor-scene-edit-toggle");
                Assert.IsNotNull(sceneEdit);
                Assert.AreEqual(window.SceneMarkersEnabledForTests, sceneEdit.value);
                Assert.That(sceneEdit.tooltip, Does.Contain("不影响放置预览"));
                Assert.IsTrue(window.SceneViewCallbackRegisteredForTests);

                Foldout diagnosticsFoldout =
                    window.rootVisualElement.Q<Foldout>("mx-level-editor-diagnostics-foldout");
                Assert.IsNotNull(diagnosticsFoldout);

                var integerFields = new List<IntegerField>();
                CollectVisualElements(window.rootVisualElement, integerFields);
                Assert.IsFalse(
                    integerFields.Exists(field => field.label == "Batch Body Start"),
                    "Batch Body Start should not be part of the normal Level Editor toolbar.");
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void LevelEditorWindow_CreateGuiEnablesMultiAtlasForEditorFallbackFonts()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MxLevelEditorWindow window = MxLevelEditorWindow.OpenWindow();
            try
            {
                window.CreateGUI();

                TextSettings[] allSettings =
                    UnityEngine.Resources.FindObjectsOfTypeAll<TextSettings>();
                TextSettings editorSettings = null;
                for (int settingsIndex = 0; settingsIndex < allSettings.Length; settingsIndex++)
                {
                    TextSettings candidate = allSettings[settingsIndex];
                    if (candidate != null
                        && string.Equals(
                            candidate.GetType().FullName,
                            "UnityEditor.EditorTextSettings",
                            StringComparison.Ordinal))
                    {
                        editorSettings = candidate;
                        break;
                    }
                }

                Assert.IsNotNull(editorSettings);
                int atlasBackedFallbackCount = 0;
                List<FontAsset> fallbackFonts = editorSettings.fallbackFontAssets;
                for (int fontIndex = 0; fontIndex < fallbackFonts.Count; fontIndex++)
                {
                    FontAsset fallbackFont = fallbackFonts[fontIndex];
                    if (fallbackFont == null
                        || fallbackFont.atlasWidth <= 0
                        || fallbackFont.atlasHeight <= 0)
                    {
                        continue;
                    }

                    atlasBackedFallbackCount++;
                    Assert.IsTrue(
                        fallbackFont.isMultiAtlasTexturesEnabled,
                        fallbackFont.name);
                }

                Assert.Greater(atlasBackedFallbackCount, 0);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void LevelEditorWindow_ToggleFocusShortcutUsesSingleBKey()
        {
            const string shortcutId = "MxFramework/Level Editor/Toggle Focus";
            CollectionAssert.Contains(
                ShortcutManager.instance.GetAvailableShortcutIds(),
                shortcutId);

            MethodInfo shortcutMethod = typeof(MxLevelEditorWindow).GetMethod(
                "ToggleFocusShortcut",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(shortcutMethod);

            bool foundShortcut = false;
            bool foundDefaultB = false;
            IList<CustomAttributeData> attributes = shortcutMethod.GetCustomAttributesData();
            for (int i = 0; i < attributes.Count; i++)
            {
                CustomAttributeData attribute = attributes[i];
                if (attribute.AttributeType != typeof(ShortcutAttribute))
                    continue;

                foundShortcut = true;
                IList<CustomAttributeTypedArgument> arguments = attribute.ConstructorArguments;
                for (int j = 0; j < arguments.Count; j++)
                {
                    CustomAttributeTypedArgument argument = arguments[j];
                    if (argument.ArgumentType == typeof(KeyCode)
                        && Convert.ToInt32(argument.Value) == (int)KeyCode.B)
                    {
                        foundDefaultB = true;
                    }
                }
            }

            Assert.IsTrue(foundShortcut);
            Assert.IsTrue(foundDefaultB);
        }

        [Test]
        public void WorldObjectSceneContextMenuExposesQuickPlacementCommands()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(null, module, () => { });
            MxLevelEditorListRow row = FindRowByDefinition(
                module.BuildRows(context, "movables"),
                "胶囊体");
            var menu = new GenericMenu();

            ((IMxLevelEditorSceneContextMenuModule)module).BuildSceneContextMenu(
                new MxLevelEditorSceneViewContext(
                    context,
                    "movables",
                    row,
                    new[] { row }),
                menu);

            Assert.GreaterOrEqual(menu.GetItemCount(), 6);
        }

        [UnityTest]
        public IEnumerator LevelEditorWindow_ToggleFocusReturnsToSceneAndPreservesWindowState()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MethodInfo shortcutMethod = typeof(MxLevelEditorWindow).GetMethod(
                "ToggleFocusShortcut",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(shortcutMethod);

            SceneView sceneView = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            sceneView.Show();
            sceneView.Focus();
            yield return null;

            shortcutMethod.Invoke(null, null);
            yield return null;

            var window = EditorWindow.focusedWindow as MxLevelEditorWindow;
            Assert.IsNotNull(window);
            window.CreateGUI();
            SelectWorldObjectsModule(window);
            TextField search =
                window.rootVisualElement.Q<TextField>("mx-level-editor-search-field");
            Assert.IsNotNull(search);
            search.value = "胶囊";

            shortcutMethod.Invoke(null, null);
            yield return null;
            Assert.AreSame(sceneView, EditorWindow.focusedWindow);

            shortcutMethod.Invoke(null, null);
            yield return null;
            Assert.AreSame(window, EditorWindow.focusedWindow);
            Assert.AreEqual(
                "胶囊",
                window.rootVisualElement.Q<TextField>(
                    "mx-level-editor-search-field").value);

            window.Close();
            sceneView.Focus();
        }

        [Test]
        public void LevelEditorWindow_TracksDirtyModuleForNativeClosePrompt()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
            for (int i = 0; i < modules.Count; i++)
                modules[i].Reload();

            MxLevelEditorWindow window = MxLevelEditorWindow.OpenWindow();
            try
            {
                window.CreateGUI();
                var managed = new List<Transform>();
                IMxLevelEditorSceneChangeAwareModule changedModule = null;
                for (int i = 0; i < modules.Count; i++)
                {
                    if (modules[i] is not IMxLevelEditorSceneChangeAwareModule aware)
                        continue;

                    int countBefore = managed.Count;
                    aware.CollectManagedSceneTransforms(managed);
                    if (changedModule == null && managed.Count > countBefore)
                        changedModule = aware;
                }

                Assert.IsNotNull(changedModule);
                Assert.IsFalse(window.hasUnsavedChanges);
                Assert.IsTrue(changedModule.NotifySceneTransformChanged(managed[0]));

                window.RepaintRequested();

                Assert.IsTrue(window.hasUnsavedChanges);
                Assert.That(window.saveChangesMessage, Does.Contain("重新载入最近保存的场景与配置"));
                Assert.AreEqual(
                    "● 有未保存更改",
                    window.rootVisualElement.Q<Label>("mx-level-editor-document-state").text);
            }
            finally
            {
                for (int i = 0; i < modules.Count; i++)
                    modules[i].Reload();
                window.RepaintRequested();
                window.Close();
            }
        }

        [Test]
        public void LevelEditorWindow_DestroyFallbackRemovesUnsavedDuplicatedWorldObject()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
            for (int i = 0; i < modules.Count; i++)
                modules[i].Reload();

            CharacterTestWorldObjectLevelEditorModule worldModule = null;
            for (int i = 0; i < modules.Count; i++)
            {
                worldModule = modules[i] as CharacterTestWorldObjectLevelEditorModule;
                if (worldModule != null)
                    break;
            }

            Assert.IsNotNull(worldModule);
            var context = new MxLevelEditorContext(null, worldModule, () => { });
            IReadOnlyList<MxLevelEditorListRow> rows = worldModule.BuildRows(context, "movables");
            int initialRowCount = rows.Count;
            Assert.Greater(initialRowCount, 0);
            Assert.IsTrue(worldModule.TryResolveRowObject(context, rows[0], out Object sourceObject));
            var source = sourceObject as GameObject;
            Assert.IsNotNull(source);

            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult duplicate =
                worldModule.DuplicateRowForTests(
                    rows[0],
                    source.transform.position + Vector3.right * 3f);
            Assert.IsTrue(duplicate.Success, duplicate.Status);
            Assert.IsNotNull(duplicate.GameObject);
            string duplicateName = duplicate.GameObject.name;
            Assert.IsTrue(EditorSceneManager.GetActiveScene().isDirty);

            MxLevelEditorWindow window = MxLevelEditorWindow.OpenWindow();
            try
            {
                window.CreateGUI();
                window.RepaintRequested();
                Assert.IsTrue(window.hasUnsavedChanges);

                Object.DestroyImmediate(window);

                Assert.IsNull(GameObject.Find(duplicateName));
                Assert.IsFalse(EditorSceneManager.GetActiveScene().isDirty);
                Assert.AreEqual(
                    initialRowCount,
                    worldModule.BuildRows(context, "movables").Count);
            }
            finally
            {
                if (window != null)
                    window.Close();
            }
        }

        [Test]
        public void LevelEditorWindow_SceneMarkerPreferenceDoesNotDisablePlacementInteraction()
        {
            MxLevelEditorWindow.CaptureSceneEditPreferenceForTests(
                out bool hadSceneEditPreference,
                out bool previousSceneEditPreference);
            try
            {
                MxLevelEditorWindow.SetSceneEditPreferenceForTests(false);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                MxLevelEditorWindow window = MxLevelEditorWindow.OpenWindow();
                try
                {
                    window.CreateGUI();
                    window.SetPlacementPopoverOpenForTests(true);

                    Toggle sceneEdit =
                        window.rootVisualElement.Q<Toggle>("mx-level-editor-scene-edit-toggle");
                    Assert.IsNotNull(sceneEdit);
                    Assert.IsFalse(window.SceneMarkersEnabledForTests);
                    Assert.IsFalse(sceneEdit.value);
                    Assert.IsTrue(window.SceneViewCallbackRegisteredForTests);
                    Assert.IsTrue(window.ScenePlacementInteractionEnabledForTests);

                    sceneEdit.value = true;
                    Assert.IsTrue(window.SceneMarkersEnabledForTests);
                    Assert.IsTrue(MxLevelEditorWindow.ReadSceneEditPreferenceForTests());
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                MxLevelEditorWindow.RestoreSceneEditPreferenceForTests(
                    hadSceneEditPreference,
                    previousSceneEditPreference);
            }
        }

        [Test]
        public void LevelEditorWindow_LayoutPreferencesClampAndRestore()
        {
            MxLevelEditorWindow.CaptureLayoutPreferencesForTests(
                out bool hadLeftPaneWidth,
                out float previousLeftPaneWidth,
                out bool hadInspectorPaneWidth,
                out float previousInspectorPaneWidth);
            try
            {
                MxLevelEditorWindow.ClearLayoutPreferencesForTests();
                Assert.AreEqual(
                    300f,
                    MxLevelEditorWindow.ReadLeftPaneWidthPreferenceForTests(1365f),
                    0.01f);
                Assert.AreEqual(
                    440f,
                    MxLevelEditorWindow.ReadInspectorPaneWidthPreferenceForTests(1365f, 300f),
                    0.01f);

                MxLevelEditorWindow.SetLayoutPreferencesForTests(1f, 1f);
                Assert.AreEqual(
                    220f,
                    MxLevelEditorWindow.ReadLeftPaneWidthPreferenceForTests(1365f),
                    0.01f);
                Assert.AreEqual(
                    360f,
                    MxLevelEditorWindow.ReadInspectorPaneWidthPreferenceForTests(1365f, 220f),
                    0.01f);

                MxLevelEditorWindow.SetLayoutPreferencesForTests(360f, 480f);
                Assert.AreEqual(
                    360f,
                    MxLevelEditorWindow.ReadLeftPaneWidthPreferenceForTests(1365f),
                    0.01f);
                Assert.AreEqual(
                    480f,
                    MxLevelEditorWindow.ReadInspectorPaneWidthPreferenceForTests(1365f, 360f),
                    0.01f);
            }
            finally
            {
                MxLevelEditorWindow.RestoreLayoutPreferencesForTests(
                    hadLeftPaneWidth,
                    previousLeftPaneWidth,
                    hadInspectorPaneWidth,
                    previousInspectorPaneWidth);
            }
        }

        [Test]
        public void LevelEditorWindow_PlacementPopoverLayoutStaysInsideNarrowPanel()
        {
            Vector2 layout =
                MxLevelEditorWindow.ResolvePlacementPopoverLayoutForTests(300f, 292f);

            Assert.Greater(layout.x, 0f);
            Assert.GreaterOrEqual(layout.y, 8f);
            Assert.LessOrEqual(layout.y + layout.x, 292f);
        }

        [Test]
        public void LevelEditorModule_RangeInspectorShowsSelectedTreeSummaryWithoutRow()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });

            IReadOnlyList<MxLevelEditorListRow> rows = module.BuildRows(context, "movables");
            Assert.AreEqual(8, rows.Count);

            var inspector = new VisualElement();
            module.BuildInspector(context, null, inspector);

            Assert.IsNotNull(inspector.Q("character-test-range-summary"));
            Assert.IsNotNull(inspector.Q<Foldout>("character-test-document-metadata"));
            Assert.IsNotNull(inspector.Q<HelpBox>());
        }

        [Test]
        public void LevelEditorModule_SelectedMovableInspectorUsesDefinitionPopupAndAdvancedIds()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });
            IReadOnlyList<MxLevelEditorListRow> rows = module.BuildRows(context, "movables");
            Assert.Greater(rows.Count, 0);

            var inspector = new VisualElement();
            module.BuildInspector(context, rows[0], inspector);

            PopupField<string> popup =
                inspector.Q<PopupField<string>>("character-test-inspector-definition-popup");
            Assert.IsNotNull(popup);
            AssertContainsChoice(popup, "101 轻型滑动物");
            AssertDoesNotContainChoice(popup, "1001 默认边界");

            Foldout advanced = inspector.Q<Foldout>("character-test-advanced-ids");
            Assert.IsNotNull(advanced);
            Assert.IsFalse(advanced.value);
            Assert.IsNotNull(inspector.Q<Button>("character-test-inspector-properties-tab"));
            Assert.IsNotNull(inspector.Q<Button>("character-test-inspector-runtime-tab"));
            Assert.IsNotNull(inspector.Q<IntegerField>("character-test-advanced-definition-id"));
            AssertNoTopLevelIntegerField(inspector, "定义 ID");
            Assert.IsNotNull(inspector.Q("character-test-definition-summary"));
            Assert.IsNotNull(inspector.Q("character-test-inspector-section-基础属性"));
        }

        [Test]
        public void LevelEditorModule_SelectedBatchInspectorFiltersStaticDefinitions()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });
            IReadOnlyList<MxLevelEditorListRow> rows = module.BuildRows(context, "static.batches");
            Assert.Greater(rows.Count, 0);

            var inspector = new VisualElement();
            module.BuildInspector(context, rows[0], inspector);

            PopupField<string> popup =
                inspector.Q<PopupField<string>>("character-test-inspector-definition-popup");
            Assert.IsNotNull(popup);
            AssertContainsChoice(popup, "1001 默认边界");
            AssertDoesNotContainChoice(popup, "101 轻型滑动物");

            Foldout advanced = inspector.Q<Foldout>("character-test-advanced-ids");
            Assert.IsNotNull(advanced);
            Assert.IsFalse(advanced.value);
            Assert.IsNotNull(inspector.Q<IntegerField>("character-test-advanced-body-id-start"));
            AssertNoTopLevelIntegerField(inspector, "起始刚体 ID");
        }

    }
}
