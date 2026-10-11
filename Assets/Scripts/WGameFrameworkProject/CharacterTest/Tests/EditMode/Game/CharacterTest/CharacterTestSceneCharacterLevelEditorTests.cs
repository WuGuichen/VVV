using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    public sealed partial class CharacterTestSceneCharacterAuthoringTests
    {
        [Test]
        public void LevelEditorWindow_CanSwitchToSceneCharactersModule()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MxLevelEditorWindow window = MxLevelEditorWindow.OpenWindow();
            try
            {
                window.CreateGUI();

                PopupField<string> modulePopup =
                    window.rootVisualElement.Q<PopupField<string>>("mx-level-editor-module-popup");
                Assert.IsNotNull(modulePopup);
                modulePopup.value = "场景角色";

                Assert.AreEqual("场景角色", modulePopup.value);
                Assert.IsNotNull(window.rootVisualElement.Q<ListView>("mx-level-editor-row-view"));
                Assert.IsNotNull(
                    window.rootVisualElement.Q<ToolbarMenu>("character-test-character-add-menu"));
                Assert.IsNull(
                    window.rootVisualElement.Q<Label>("mx-level-editor-range-title"));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void LevelEditorWindow_SceneContextTargetSwitchesToRegisteredCharacterModule()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);
            MxLevelEditorWindow window = MxLevelEditorWindow.OpenWindow();
            try
            {
                window.CreateGUI();
                PopupField<string> modulePopup =
                    window.rootVisualElement.Q<PopupField<string>>(
                        "mx-level-editor-module-popup");
                Assert.IsNotNull(modulePopup);
                modulePopup.value = "世界对象";

                CharacterTestSceneCharacterLevelEditorModule characterModule = null;
                IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
                for (int i = 0; i < modules.Count; i++)
                {
                    characterModule =
                        modules[i] as CharacterTestSceneCharacterLevelEditorModule;
                    if (characterModule != null)
                        break;
                }

                Assert.IsNotNull(characterModule);
                var context = new MxLevelEditorContext(null, characterModule, () => { });
                IReadOnlyList<MxLevelEditorListRow> rows =
                    characterModule.BuildRows(context, "document");
                Assert.Greater(rows.Count, 0);
                Assert.IsTrue(characterModule.TryResolveRowObject(
                    context,
                    rows[0],
                    out Object characterObject));

                MethodInfo activateMethod = typeof(MxLevelEditorWindow).GetMethod(
                    "TryActivateSceneContextTarget",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(activateMethod);
                var arguments = new object[] { characterObject, null };

                Assert.IsTrue((bool)activateMethod.Invoke(window, arguments));
                Assert.IsNotNull(arguments[1] as MxLevelEditorListRow);
                Assert.AreEqual(
                    "场景角色",
                    window.rootVisualElement.Q<PopupField<string>>(
                        "mx-level-editor-module-popup").value);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void SceneCharacterSceneContextMenuExposesQuickPlacementCommands()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);
            var module = new CharacterTestSceneCharacterLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(null, module, () => { });
            IReadOnlyList<MxLevelEditorListRow> rows =
                module.BuildRows(context, "document");
            Assert.Greater(rows.Count, 0);
            var menu = new GenericMenu();

            ((IMxLevelEditorSceneContextMenuModule)module).BuildSceneContextMenu(
                new MxLevelEditorSceneViewContext(
                    context,
                    "document",
                    rows[0],
                    rows),
                menu);

            Assert.GreaterOrEqual(menu.GetItemCount(), 6);
        }

        [Test]
        public void SceneCharacter_SelectionResolvesModelChildToRegisteredRoot()
        {
            string scenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var module = new CharacterTestSceneCharacterLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(null, module, () => { });
            IReadOnlyList<MxLevelEditorListRow> rows =
                module.BuildRows(context, "document");
            Assert.Greater(rows.Count, 0);
            Assert.IsTrue(module.TryResolveRowObject(
                context,
                rows[0],
                out Object characterObject));
            var character = characterObject as GameObject;
            Assert.IsNotNull(character);

            var modelChild = new GameObject("LevelEditorContextMenuModelChild")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            modelChild.transform.SetParent(character.transform, false);
            try
            {
                Assert.IsTrue(module.TryResolveSelection(
                    context,
                    modelChild,
                    out MxLevelEditorSelectionTarget target));
                Assert.IsNotNull(target);
                Assert.AreEqual(rows[0].Key, target.RowKey);
            }
            finally
            {
                Object.DestroyImmediate(modelChild);
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
        }

        [Test]
        public void SceneCharacter_RegisteredTransformChangeMarksSceneUnsaved()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);
            var module = new CharacterTestSceneCharacterLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(
                null,
                module,
                () => { });
            IReadOnlyList<MxLevelEditorListRow> rows = module.BuildRows(context, "document");
            Assert.Greater(rows.Count, 0);
            Assert.IsTrue(module.TryResolveRowObject(context, rows[0], out Object rowObject));
            var gameObject = rowObject as GameObject;
            Assert.IsNotNull(gameObject);

            Assert.IsTrue(module.NotifySceneTransformChanged(gameObject.transform));
            Assert.IsTrue(module.IsDirty);
            Assert.That(module.Status, Does.Contain(gameObject.name));
        }

        [Test]
        public void SceneCharacterSceneHint_EmptyDocumentShowsGuidance()
        {
            var module = new CharacterTestSceneCharacterLevelEditorModule();
            var context = CreateSceneViewContext(
                module,
                null,
                Array.Empty<MxLevelEditorListRow>());

            CharacterTestSceneCharacterLevelEditorModule.SceneHintViewModel hint =
                module.BuildSceneHintForTests(
                    context,
                    Array.Empty<CharacterTestSceneCharacterLevelEditorModule.SceneOverlayItem>());

            Assert.IsTrue(hint.IsEmpty);
            Assert.That(hint.Title, Does.Contain("CharacterTest 场景角色"));
            Assert.That(hint.Summary, Does.Contain("暂无 CharacterTest 场景角色"));
            Assert.That(hint.Summary, Does.Contain("创建默认角色"));
            Assert.That(hint.Operation, Does.Contain("左键放置"));
        }

        [Test]
        public void SceneCharacterSceneHint_WithSelectedAiShowsProfileSource()
        {
            var module = new CharacterTestSceneCharacterLevelEditorModule();
            MxLevelEditorListRow player =
                CreateEditorRow("Player", CharacterTestCharacterControlOwner.LocalPlayer, 1, true);
            MxLevelEditorListRow enemy =
                CreateEditorRow("AI Enemy", CharacterTestCharacterControlOwner.AiControlled, 2, true);
            var rows = new[] { player, enemy };
            var items = new[]
            {
                CreateOverlayItem(player, Vector3.zero, false),
                CreateOverlayItem(enemy, Vector3.right, true)
            };
            var context = CreateSceneViewContext(module, enemy, rows);

            CharacterTestSceneCharacterLevelEditorModule.SceneHintViewModel hint =
                module.BuildSceneHintForTests(context, items);

            Assert.IsFalse(hint.IsEmpty);
            Assert.That(hint.Summary, Does.Contain("可见角色 2"));
            Assert.That(hint.Summary, Does.Contain("Runtime AI Planner 控制 1"));
            Assert.That(hint.Selected, Does.Contain("AI Enemy"));
            Assert.That(hint.Ai, Does.Contain("Runtime AI Planner"));
            Assert.That(hint.Ai, Does.Contain("Runtime"));
        }

        [Test]
        public void SceneCharacterOverlay_DisabledLabelIsExplicit()
        {
            MxLevelEditorListRow disabled =
                CreateEditorRow("Disabled Enemy", CharacterTestCharacterControlOwner.AiControlled, 2, false);
            CharacterTestSceneCharacterLevelEditorModule.SceneOverlayItem item =
                CreateOverlayItem(disabled, Vector3.zero, false);

            Assert.IsFalse(item.Enabled);
            Assert.That(
                CharacterTestSceneCharacterLevelEditorModule.FormatSceneOverlayLabelForTests(item),
                Does.Contain("禁用"));
        }

        [Test]
        public void SceneCharacterEmptyState_ShowsPrimaryActions()
        {
            var module = new CharacterTestSceneCharacterLevelEditorModule();
            module.SetDocumentForTests(CreateEmptyCharacterDocument());
            var toolbar = new VisualElement();

            module.BuildModuleToolbar(null, toolbar);

            Assert.IsNotNull(toolbar.Q<VisualElement>(
                CharacterTestSceneCharacterLevelEditorModule.EmptyStateRootName));
            Assert.IsNotNull(toolbar.Q<Button>(
                CharacterTestSceneCharacterLevelEditorModule.EmptyStateDefaultButtonName));
            Assert.IsNotNull(toolbar.Q<Button>(
                CharacterTestSceneCharacterLevelEditorModule.EmptyStatePlaceButtonName));
        }

        [Test]
        public void SceneCharacterPlacementPanel_ReflectsPlacementState()
        {
            var module = new CharacterTestSceneCharacterLevelEditorModule();
            module.SetDocumentForTests(CharacterTestSceneCharacterAuthoringAdapter.CreateDefaultDocument());
            module.ConfigurePlacementForTests(CharacterTestCharacterControlOwner.AiControlled, false);
            var root = new VisualElement();
            var fields = new VisualElement();
            var options = new VisualElement();
            var actions = new VisualElement();
            root.Add(fields);
            root.Add(options);
            root.Add(actions);
            var panel = new MxLevelEditorPlacementPanel(fields, options, actions);
            bool closed = false;
            var context = new MxLevelEditorPlacementContext(
                new MxLevelEditorContext(null, module, () => { }),
                "document",
                null,
                Array.Empty<MxLevelEditorListRow>(),
                () => closed = true);

            module.BuildPlacementPanel(context, panel);

            Button start = root.Q<Button>(
                CharacterTestSceneCharacterLevelEditorModule.PlacementStartButtonName);
            PopupField<string> owner = root.Q<PopupField<string>>(
                CharacterTestSceneCharacterLevelEditorModule.PlacementOwnerFieldName);

            Assert.IsNotNull(start);
            Assert.IsNotNull(owner);
            Assert.IsNotNull(root.Q<ToolbarToggle>("character-test-placement-grid-snap"));
            Assert.IsFalse(module.IsScenePlacementActive);
            Assert.AreEqual("Runtime AI Planner", owner.value);
            Assert.IsTrue(start.enabledSelf);
            Assert.IsFalse(closed);
            context.ClosePlacementSurface();
            Assert.IsTrue(closed);

            module.ConfigurePlacementForTests(CharacterTestCharacterControlOwner.AiControlled, true);
            root = new VisualElement();
            fields = new VisualElement();
            options = new VisualElement();
            actions = new VisualElement();
            root.Add(fields);
            root.Add(options);
            root.Add(actions);
            panel = new MxLevelEditorPlacementPanel(fields, options, actions);
            module.BuildPlacementPanel(context, panel);
            start = root.Q<Button>(
                CharacterTestSceneCharacterLevelEditorModule.PlacementStartButtonName);
            Assert.IsNotNull(start);
            Assert.IsFalse(start.enabledSelf);

            module.CancelScenePlacement(null);

            Assert.IsFalse(module.IsScenePlacementActive);
            Assert.That(module.Status, Does.Contain("取消"));

            root = new VisualElement();
            fields = new VisualElement();
            options = new VisualElement();
            actions = new VisualElement();
            root.Add(fields);
            root.Add(options);
            root.Add(actions);
            panel = new MxLevelEditorPlacementPanel(fields, options, actions);
            module.BuildPlacementPanel(context, panel);
            start = root.Q<Button>(
                CharacterTestSceneCharacterLevelEditorModule.PlacementStartButtonName);
            Assert.IsNotNull(start);
            Assert.IsTrue(start.enabledSelf);
        }

        [UnityTest]
        public IEnumerator SceneCharacterDuplicate_CreatesUniqueObjectAndSupportsUndoRedo()
        {
            EditorSceneManager.OpenScene(
                CharacterTestSceneCharacterAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);
            var module = new CharacterTestSceneCharacterLevelEditorModule();
            module.OnEnable();
            var context = new MxLevelEditorContext(null, module, () => { });
            IReadOnlyList<MxLevelEditorListRow> rows = module.BuildRows(context, "document");
            Assert.IsNotEmpty(rows);
            MxLevelEditorListRow sourceRow = rows[0];
            Assert.IsTrue(module.TryResolveRowObject(context, sourceRow, out Object sourceObject));
            var source = sourceObject as GameObject;
            Assert.IsNotNull(source);

            int countBefore = rows.Count;
            Assert.IsTrue(
                module.DuplicateRowForTests(
                    sourceRow,
                    source.transform.position + new Vector3(2f, 0f, 2f),
                    out GameObject duplicate));
            Assert.IsNotNull(duplicate);
            Assert.AreNotSame(source, duplicate);
            Assert.AreNotSame(source.transform, duplicate.transform);
            Assert.AreEqual(source.transform.rotation, duplicate.transform.rotation);
            Assert.AreEqual(source.transform.localScale, duplicate.transform.localScale);
            Assert.AreEqual(countBefore + 1, module.BuildRows(context, "document").Count);
            yield return null;

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(countBefore, module.BuildRows(context, "document").Count);
            Assert.IsTrue(duplicate == null);

            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(countBefore + 1, module.BuildRows(context, "document").Count);
            Assert.IsTrue(duplicate != null);

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(countBefore, module.BuildRows(context, "document").Count);
        }

        [Test]
        public void SceneCharacterControlOwnerLabels_AreLocalizedForEditor()
        {
            Assert.AreEqual(
                "无控制",
                CharacterTestSceneCharacterLevelEditorModule.GetControlOwnerLabelForTests(
                    CharacterTestCharacterControlOwner.None));
            Assert.AreEqual(
                "本地玩家",
                CharacterTestSceneCharacterLevelEditorModule.GetControlOwnerLabelForTests(
                    CharacterTestCharacterControlOwner.LocalPlayer));
            Assert.AreEqual(
                "Runtime AI Planner",
                CharacterTestSceneCharacterLevelEditorModule.GetControlOwnerLabelForTests(
                    CharacterTestCharacterControlOwner.AiControlled));
        }

        [Test]
        public void SceneCharacterInspector_AiControlledShowsRuntimeAiPlannerProfileSelector()
        {
            var module = new CharacterTestSceneCharacterLevelEditorModule();
            CharacterTestSceneCharacterAuthoringDocument document =
                CharacterTestSceneCharacterAuthoringAdapter.CreateDefaultDocument();
            document.Characters[1].RuntimeAiPlannerProfilePath =
                "MxFramework/CharacterTest/runtime_ai_planner/skill_caster.json";
            module.SetDocumentForTests(document);
            var inspector = new VisualElement();
            MxLevelEditorListRow row =
                CreateEditorRow("AI Enemy", CharacterTestCharacterControlOwner.AiControlled, 2, true, index: 1);

            module.BuildInspector(
                new MxLevelEditorContext(null, module, () => { }),
                row,
                inspector);

            PopupField<string> popup = inspector.Q<PopupField<string>>(
                CharacterTestSceneCharacterLevelEditorModule.RuntimeAiPlannerProfileFieldName);
            Assert.IsNotNull(popup);
            Assert.That(popup.value, Does.Contain("skill_caster.json"));
            Assert.IsNotNull(
                inspector.Q<Button>("character-test-character-properties-tab"));
            Assert.IsNotNull(
                inspector.Q<Button>("character-test-character-runtime-tab"));
            Assert.IsNotNull(
                inspector.Q<VisualElement>("character-test-character-quick-actions"));
        }

        [Test]
        public void SceneCharacterPlacementPointerCapture_ConsumesOnlyPlacementLeftDrag()
        {
            Assert.IsTrue(CharacterTestSceneCharacterLevelEditorModule
                .ShouldCaptureScenePlacementPointerEventForTests(EventType.MouseDown, 0, false));
            Assert.IsTrue(CharacterTestSceneCharacterLevelEditorModule
                .ShouldCaptureScenePlacementPointerEventForTests(EventType.MouseUp, 0, false));
            Assert.IsTrue(CharacterTestSceneCharacterLevelEditorModule
                .ShouldCaptureScenePlacementPointerEventForTests(EventType.MouseDrag, 0, false));
            Assert.IsFalse(CharacterTestSceneCharacterLevelEditorModule
                .ShouldCaptureScenePlacementPointerEventForTests(EventType.MouseMove, 0, false));
            Assert.IsFalse(CharacterTestSceneCharacterLevelEditorModule
                .ShouldCaptureScenePlacementPointerEventForTests(EventType.MouseDown, 0, true));
            Assert.IsFalse(CharacterTestSceneCharacterLevelEditorModule
                .ShouldCaptureScenePlacementPointerEventForTests(EventType.MouseDown, 1, false));
        }

    }
}
