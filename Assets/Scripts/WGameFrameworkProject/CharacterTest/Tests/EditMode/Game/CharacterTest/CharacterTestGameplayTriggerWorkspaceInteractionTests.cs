using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Config.Levels;
using MxFramework.Editor;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class
        CharacterTestGameplayTriggerWorkspaceInteractionTests
    {
        private const string RegionStableId =
            "region.p3.3b.interaction";

        [UnityTest]
        public IEnumerator DelayedName_EnterFocusOutAndEscapeFollowContract()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);
            CharacterTestWorldObjectLevelEditorModule module =
                CreateModule(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                    out CharacterTestLevelGameplayTriggerEntry entry);
            string baseline = module.CaptureDocumentSnapshotForTests();
            string originalName =
                module.GetTriggerDisplayNameForTests(entry.StableId);
            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            TriggerWorkspaceInteractionTestWindow window =
                ScriptableObject.CreateInstance<
                    TriggerWorkspaceInteractionTestWindow>();
            try
            {
                window.rootVisualElement.Add(workspace);
                window.position = new Rect(100f, 100f, 760f, 620f);
                window.Show();
                yield return null;

                TextField name = FindNameField(workspace);
                Assert.IsTrue(name.isDelayed);
                SetDelayedTextDraft(name, "Enter 提交");
                SendKey(name, KeyCode.Return, '\n');
                yield return null;
                yield return null;
                Assert.AreEqual(
                    "Enter 提交",
                    module.GetTriggerDisplayNameForTests(
                        entry.StableId));
                Assert.IsTrue(module.IsDirty);

                Undo.PerformUndo();
                yield return null;
                Assert.AreEqual(
                    baseline,
                    module.CaptureDocumentSnapshotForTests());
                Assert.AreEqual(
                    originalName,
                    module.GetTriggerDisplayNameForTests(
                        entry.StableId));
                Assert.IsFalse(module.IsDirty);

                name = FindNameField(workspace);
                FocusTextInput(name);
                SetDelayedTextDraft(name, "失焦提交");
                workspace.Q<Button>(
                        "character-test-trigger-workspace-locate")
                    .Focus();
                yield return null;
                yield return null;
                Assert.AreEqual(
                    "失焦提交",
                    module.GetTriggerDisplayNameForTests(
                        entry.StableId));
                Assert.IsTrue(module.IsDirty);

                Undo.PerformUndo();
                yield return null;
                Assert.AreEqual(
                    baseline,
                    module.CaptureDocumentSnapshotForTests());
                Assert.IsFalse(module.IsDirty);

                name = FindNameField(workspace);
                FocusTextInput(name);
                yield return null;
                Assert.IsTrue(
                    IsFocusWithin(workspace, name),
                    "The name input must own focus before Esc.");
                SetDelayedTextDraft(name, "应被 Esc 放弃");
                SendKey(name, KeyCode.Escape);
                Assert.AreEqual(
                    originalName,
                    module.GetTriggerDisplayNameForTests(
                        entry.StableId),
                    "Esc must not commit the draft during dispatch.");
                Assert.AreEqual(
                    originalName,
                    name.value,
                    "Esc must restore the visible value during dispatch.");
                yield return null;
                Assert.AreEqual(
                    originalName,
                    module.GetTriggerDisplayNameForTests(
                        entry.StableId));
                Assert.AreEqual(originalName, name.value);
                Assert.AreEqual(
                    baseline,
                    module.CaptureDocumentSnapshotForTests());
                Assert.IsFalse(module.IsDirty);
            }
            finally
            {
                window.Close();
            }
        }

        [UnityTest]
        public IEnumerator DropdownRefresh_PreservesScrollAndTextFocus()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);
            CharacterTestWorldObjectLevelEditorModule module =
                CreateModule(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    CharacterTestPoisonIds.PoisonedBuffId,
                    CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                    out _);
            string baseline = module.CaptureDocumentSnapshotForTests();
            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            TriggerWorkspaceInteractionTestWindow window =
                ScriptableObject.CreateInstance<
                    TriggerWorkspaceInteractionTestWindow>();
            try
            {
                window.rootVisualElement.Add(workspace);
                window.position = new Rect(100f, 100f, 420f, 520f);
                window.Show();
                yield return null;

                Foldout advanced = workspace.Q<Foldout>(
                    "character-test-trigger-options");
                Assert.IsNotNull(advanced);
                advanced.value = true;
                yield return null;

                ScrollView scroll = workspace.Q<ScrollView>(
                    "character-test-trigger-workspace-editor");
                float requestedScrollY = Mathf.Min(
                    120f,
                    scroll.verticalScroller.highValue);
                Assert.Greater(requestedScrollY, 0f);
                scroll.scrollOffset =
                    new Vector2(0f, requestedScrollY);
                yield return null;
                float expectedScrollY = scroll.scrollOffset.y;

                TextField name = FindNameField(workspace);
                FocusTextInput(name);
                yield return null;
                Assert.IsTrue(IsFocusWithin(workspace, name));

                PopupField<string> cooldownMode =
                    workspace.Q<PopupField<string>>(
                        "character-test-trigger-cooldown-mode");
                Assert.IsNotNull(cooldownMode);
                cooldownMode.value = "自定义";
                yield return null;
                yield return null;

                scroll = workspace.Q<ScrollView>(
                    "character-test-trigger-workspace-editor");
                Assert.AreEqual(
                    expectedScrollY,
                    scroll.scrollOffset.y,
                    0.5f);
                name = FindNameField(workspace);
                Assert.IsTrue(
                    IsFocusWithin(workspace, name),
                    "Refreshing after a dropdown commit must restore the "
                    + "previous text focus.");

                Undo.PerformUndo();
                yield return null;
                Assert.AreEqual(
                    baseline,
                    module.CaptureDocumentSnapshotForTests());
                Assert.IsFalse(module.IsDirty);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void UnknownReferencesAndReadOnlyBrowsing_PreserveAuthoring()
        {
            EditorSceneManager.OpenScene(
                CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                OpenSceneMode.Single);

            CharacterTestWorldObjectLevelEditorModule module =
                CreateModule(
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    payload0: 987654L,
                    payload1: 876543L,
                    out _);
            string unknownBuffBaseline =
                module.CaptureDocumentSnapshotForTests();
            VisualElement workspace =
                module.CreateGameplayTriggerWorkspace(null);
            AssertUnknownReference(
                workspace,
                "character-test-trigger-buff-picker",
                "未登记 Buff 987654");
            AssertUnknownReference(
                workspace,
                "character-test-trigger-buff-attribution",
                "未登记来源机制 876543");
            Assert.AreEqual(
                unknownBuffBaseline,
                module.CaptureDocumentSnapshotForTests());
            Assert.IsFalse(module.IsDirty);

            module = CreateModule(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .StoryRaiseTriggerEnterOnceTemplateId,
                payload0: 999999L,
                payload1: 0L,
                out _);
            string unknownStoryBaseline =
                module.CaptureDocumentSnapshotForTests();
            workspace = module.CreateGameplayTriggerWorkspace(null);
            AssertUnknownReference(
                workspace,
                "character-test-trigger-story-reference",
                "未登记剧情触发 999999");
            Assert.AreEqual(
                unknownStoryBaseline,
                module.CaptureDocumentSnapshotForTests());
            Assert.IsFalse(module.IsDirty);

            module = CreateModule(
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyBuffEnterOnceTemplateId,
                CharacterTestPoisonIds.PoisonedBuffId,
                CharacterTestPoisonIds.PoisonBottlePoolSourceId,
                out CharacterTestLevelGameplayTriggerEntry first);
            CharacterTestLevelGameplayTriggerEntry second =
                module.AddTriggerTemplateForTests(
                    RegionStableId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId);
            Assert.IsNotNull(second);
            module.SetTriggerDocumentForTests(
                ExtractTriggerDocument(
                    module.CaptureDocumentSnapshotForTests()),
                markDirty: false);
            string browsingBaseline =
                module.CaptureDocumentSnapshotForTests();
            int undoGroup = Undo.GetCurrentGroup();
            workspace = module.CreateGameplayTriggerWorkspace(null);

            Foldout advanced = workspace.Q<Foldout>(
                "character-test-trigger-options");
            advanced.value = true;
            advanced.value = false;
            module.SetTriggerWorkspaceRuntimeInspectorForTests(true);
            module.SetTriggerWorkspaceRuntimeInspectorForTests(false);
            VisualElement secondRow = workspace.Q(
                "character-test-trigger-workspace-item-"
                + second.StableId);
            Assert.IsNotNull(secondRow);
            using (ClickEvent click = ClickEvent.GetPooled())
                secondRow.SendEvent(click);

            Assert.AreEqual(
                second.StableId,
                module.TriggerWorkspaceSelectedEntryIdForTests);
            Assert.AreNotEqual(
                first.StableId,
                module.TriggerWorkspaceSelectedEntryIdForTests);
            Assert.AreEqual(
                browsingBaseline,
                module.CaptureDocumentSnapshotForTests());
            Assert.AreEqual(undoGroup, Undo.GetCurrentGroup());
            Assert.IsFalse(module.IsDirty);
        }

        private static CharacterTestWorldObjectLevelEditorModule
            CreateModule(
                string templateId,
                long payload0,
                long payload1,
                out CharacterTestLevelGameplayTriggerEntry entry)
        {
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            module.SetDocumentForTests(CreateWorldDocument());
            var trigger =
                new CharacterTestLevelGameplayTriggerAuthoringDocument();
            entry = trigger.AddTemplate(
                templateId,
                RegionStableId,
                payload0,
                payload1);
            module.SetTriggerDocumentForTests(
                trigger,
                markDirty: false);
            return module;
        }

        private static LevelWorldObjectAuthoringDocument
            CreateWorldDocument()
        {
            return new LevelWorldObjectAuthoringDocument
            {
                LevelId =
                    CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath =
                    CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterTestSceneRoot",
                Regions = new List<LevelWorldObjectRegionDefinition>
                {
                    new LevelWorldObjectRegionDefinition
                    {
                        Enabled = true,
                        DefinitionId =
                            CharacterTestWorldObjectDefinitionIds
                                .RegionStartArea,
                        StableId = RegionStableId,
                        RegionKind =
                            LevelWorldObjectRegionKind.TriggerRegion,
                        ScaleSource =
                            LevelWorldObjectScaleSource.SceneTransform,
                        TransformRef =
                            LevelTransformReference.FromPath(
                                "TestCourse_00_Start[2]/"
                                + "LevelRegion_StartArea[6]")
                    }
                }
            };
        }

        private static CharacterTestLevelGameplayTriggerAuthoringDocument
            ExtractTriggerDocument(string snapshot)
        {
            MxLevelAuthoringDocument envelope =
                MxLevelAuthoringJson.LoadFromJson(snapshot);
            Assert.IsTrue(
                CharacterTestLevelGameplayTriggerAuthoringAdapter.TryExtract(
                    envelope,
                    out CharacterTestLevelGameplayTriggerAuthoringDocument
                        trigger,
                    out bool modulePresent,
                    out LevelWorldObjectAuthoringValidationReport report),
                JoinIssues(report));
            Assert.IsTrue(modulePresent);
            return trigger;
        }

        private static TextField FindNameField(
            VisualElement workspace)
        {
            TextField field = workspace.Q<TextField>(
                "character-test-trigger-workspace-name");
            Assert.IsNotNull(field);
            return field;
        }

        private static void FocusTextInput(TextField field)
        {
            VisualElement input = field.Q(
                TextField.textInputUssName);
            Assert.IsNotNull(input);
            input.Focus();
        }

        private static void SetDelayedTextDraft(
            TextField field,
            string draft)
        {
            VisualElement input = field.Q(
                TextField.textInputUssName);
            Assert.IsNotNull(input);
            PropertyInfo textProperty = input.GetType().GetProperty(
                "text",
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic);
            Assert.IsNotNull(textProperty);
            textProperty.SetValue(input, draft);
        }

        private static void SendKey(
            TextField field,
            KeyCode keyCode,
            char character = '\0')
        {
            VisualElement input = field.Q(
                TextField.textInputUssName);
            Assert.IsNotNull(input);
            if (input.panel is IRuntimePanel runtimePanel)
            {
                MethodInfo sendKeyboardEvent =
                    typeof(IRuntimePanel).GetMethod(
                        "SendKeyboardEvent",
                        BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic);
                Assert.IsNotNull(sendKeyboardEvent);
                sendKeyboardEvent.Invoke(
                    runtimePanel,
                    new object[]
                    {
                        true,
                        character,
                        keyCode,
                        EventModifiers.None,
                        input
                    });
                return;
            }

            var source = new Event
            {
                type = EventType.KeyDown,
                keyCode = keyCode,
                character = character
            };
            using (KeyDownEvent key = KeyDownEvent.GetPooled(source))
                input.SendEvent(key);
        }

        private static bool IsFocusWithin(
            VisualElement root,
            VisualElement expectedRoot)
        {
            VisualElement focused =
                root?.panel?.focusController?.focusedElement
                    as VisualElement;
            while (focused != null)
            {
                if (ReferenceEquals(focused, expectedRoot))
                    return true;
                focused = focused.parent;
            }

            return false;
        }

        private static void AssertUnknownReference(
            VisualElement workspace,
            string rootName,
            string expectedValue)
        {
            VisualElement root = workspace.Q(rootName);
            Assert.IsNotNull(root);
            DropdownField picker = root.Q<DropdownField>(
                MxAuthoringReferenceField.ValueElementName);
            Assert.IsNotNull(picker);
            Assert.AreEqual(expectedValue, picker.value);
            Assert.IsNotNull(
                root.Q<HelpBox>(
                    MxAuthoringReferenceField
                        .DiagnosticElementName));
        }

        private static string JoinIssues(
            LevelWorldObjectAuthoringValidationReport report)
        {
            if (report?.Issues == null
                || report.Issues.Count == 0)
            {
                return string.Empty;
            }

            var rows = new string[report.Issues.Count];
            for (int i = 0; i < report.Issues.Count; i++)
            {
                LevelWorldObjectAuthoringIssue issue = report.Issues[i];
                rows[i] = issue == null
                    ? "[null]"
                    : issue.Code + ": " + issue.Message;
            }
            return string.Join(Environment.NewLine, rows);
        }

        private sealed class TriggerWorkspaceInteractionTestWindow
            : EditorWindow
        {
        }
    }
}
