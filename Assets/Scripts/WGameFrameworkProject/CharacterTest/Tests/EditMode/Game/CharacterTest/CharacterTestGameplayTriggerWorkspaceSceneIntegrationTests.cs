using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class
        CharacterTestGameplayTriggerWorkspaceSceneIntegrationTests
    {
        private const string ScenePath =
            CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath;

        [Test]
        public void RegionInspector_UsesOnlyCompactTriggerEntry()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            CharacterTestWorldObjectLevelEditorModule module =
                CreateModule();
            MxLevelEditorWindow window =
                AcquireWindow(
                    out bool ownsWindow,
                    out string previousWorkspaceId);
            try
            {
                var context = new MxLevelEditorContext(
                    window,
                    module,
                    () => { });
                MxLevelEditorListRow row =
                    GetFirstRegionRow(module, context);
                var inspector = new VisualElement();

                module.BuildInspector(
                    context,
                    row,
                    inspector);

                VisualElement card = inspector.Q(
                    "character-test-trigger-card");
                Assert.IsNotNull(card);
                Assert.AreEqual(
                    4,
                    card.childCount,
                    "Region Inspector must keep only title, count, open and add.");
                Assert.AreEqual(
                    "关联触发器",
                    inspector.Q<Label>(
                        "character-test-trigger-link-title")?.text);
                Assert.IsNotNull(
                    inspector.Q<Label>(
                        "character-test-trigger-count"));
                Button open = inspector.Q<Button>(
                    "character-test-trigger-open-workspace");
                Assert.IsNotNull(open);
                Assert.IsNotNull(
                    inspector.Q<ToolbarMenu>(
                        "character-test-trigger-add"));
                Assert.IsNull(
                    inspector.Q(
                        "character-test-trigger-strategy"));
                Assert.IsNull(
                    inspector.Q(
                        "character-test-trigger-buff-id"));
                Assert.IsNull(
                    inspector.Q(
                        "character-test-trigger-advanced"));

                PressButton(open);

                Assert.AreEqual(
                    CharacterTestGameplayTriggerWorkspace
                        .WorkspaceIdValue,
                    window.CurrentWorkspaceId);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(
                        module.TriggerWorkspaceRegionFilterForTests));
                Assert.IsFalse(module.IsDirty);
            }
            finally
            {
                ReleaseWindow(
                    window,
                    ownsWindow,
                    previousWorkspaceId);
            }
        }

        [UnityTest]
        public IEnumerator
            RegionSceneContextMenu_OpensAndCreatesThroughRealCallbacks()
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            CharacterTestWorldObjectLevelEditorModule module =
                CreateModule();
            MxLevelEditorWindow window =
                AcquireWindow(
                    out bool ownsWindow,
                    out string previousWorkspaceId);
            try
            {
                var context = new MxLevelEditorContext(
                    window,
                    module,
                    () => { });
                MxLevelEditorListRow row =
                    GetFirstRegionRow(module, context);
                int before =
                    module.TriggerWorkspaceVisibleEntryCountForTests(
                        string.Empty,
                        string.Empty);
                var menu = new GenericMenu();

                ((IMxLevelEditorSceneContextMenuModule)module)
                    .BuildSceneContextMenu(
                        new MxLevelEditorSceneViewContext(
                            context,
                            "regions",
                            row,
                            new[] { row }),
                        menu);
                IReadOnlyList<string> labels =
                    GetMenuLabels(menu);

                Assert.IsTrue(
                    ContainsPrefix(
                        labels,
                        "编辑关联触发器")
                    || ContainsPrefix(
                        labels,
                        "打开玩法触发器"));
                CollectionAssert.Contains(
                    labels,
                    "新建关联触发器/进入时施加 Buff");
                CollectionAssert.Contains(
                    labels,
                    "新建关联触发器/区域内维持 Buff");
                CollectionAssert.Contains(
                    labels,
                    "新建关联触发器/进入时触发 Story");

                InvokeMenuItemByPrefix(
                    menu,
                    ContainsPrefix(labels, "编辑关联触发器")
                        ? "编辑关联触发器"
                        : "打开玩法触发器");
                Assert.AreEqual(
                    CharacterTestGameplayTriggerWorkspace
                        .WorkspaceIdValue,
                    window.CurrentWorkspaceId);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(
                        module.TriggerWorkspaceRegionFilterForTests));
                Assert.IsFalse(module.IsDirty);

                InvokeMenuItem(
                    menu,
                    "新建关联触发器/进入时施加 Buff");
                Assert.AreEqual(
                    before + 1,
                    module.TriggerWorkspaceVisibleEntryCountForTests(
                        string.Empty,
                        string.Empty));
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(
                        module.TriggerWorkspaceSelectedEntryIdForTests));
                Assert.IsTrue(module.IsDirty);

                Undo.PerformUndo();
                yield return null;

                Assert.AreEqual(
                    before,
                    module.TriggerWorkspaceVisibleEntryCountForTests(
                        string.Empty,
                        string.Empty));
                Assert.IsFalse(module.IsDirty);
            }
            finally
            {
                if (module.IsDirty)
                    module.Reload();
                ReleaseWindow(
                    window,
                    ownsWindow,
                    previousWorkspaceId);
            }
        }

        private static CharacterTestWorldObjectLevelEditorModule
            CreateModule()
        {
            var module =
                new CharacterTestWorldObjectLevelEditorModule();
            module.OnEnable();
            Assert.IsFalse(module.IsDirty);
            return module;
        }

        private static MxLevelEditorListRow GetFirstRegionRow(
            CharacterTestWorldObjectLevelEditorModule module,
            MxLevelEditorContext context)
        {
            IReadOnlyList<MxLevelEditorListRow> rows =
                module.BuildRows(context, "regions");
            Assert.IsNotNull(rows);
            Assert.IsNotEmpty(rows);
            return rows[0];
        }

        private static MxLevelEditorWindow AcquireWindow(
            out bool ownsWindow,
            out string previousWorkspaceId)
        {
            MxLevelEditorWindow[] open =
                UnityEngine.Resources
                    .FindObjectsOfTypeAll<MxLevelEditorWindow>();
            MxLevelEditorWindow window =
                open.Length > 0
                    ? open[0]
                    : MxLevelEditorWindow.OpenWindow();
            ownsWindow = open.Length == 0;
            previousWorkspaceId =
                window.CurrentWorkspaceId;
            window.CreateGUI();
            return window;
        }

        private static void ReleaseWindow(
            MxLevelEditorWindow window,
            bool ownsWindow,
            string previousWorkspaceId)
        {
            if (window == null)
                return;
            if (ownsWindow)
            {
                window.Close();
                return;
            }
            if (!string.IsNullOrWhiteSpace(previousWorkspaceId))
                window.TrySelectWorkspace(previousWorkspaceId);
        }

        private static IReadOnlyList<string> GetMenuLabels(
            GenericMenu menu)
        {
            IList items = GetMenuItems(menu);
            var labels = new List<string>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                object item = items[i];
                FieldInfo contentField = item.GetType().GetField(
                    "content",
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);
                var content =
                    contentField?.GetValue(item) as GUIContent;
                if (content != null
                    && !string.IsNullOrWhiteSpace(content.text))
                {
                    labels.Add(content.text);
                }
            }
            return labels;
        }

        private static void InvokeMenuItem(
            GenericMenu menu,
            string label)
        {
            InvokeMenuItemInternal(
                menu,
                value => string.Equals(
                    value,
                    label,
                    StringComparison.Ordinal));
        }

        private static void InvokeMenuItemByPrefix(
            GenericMenu menu,
            string prefix)
        {
            InvokeMenuItemInternal(
                menu,
                value => value.StartsWith(
                    prefix,
                    StringComparison.Ordinal));
        }

        private static void InvokeMenuItemInternal(
            GenericMenu menu,
            Func<string, bool> predicate)
        {
            IList items = GetMenuItems(menu);
            for (int i = 0; i < items.Count; i++)
            {
                object item = items[i];
                Type itemType = item.GetType();
                var content =
                    itemType.GetField(
                            "content",
                            BindingFlags.Instance
                            | BindingFlags.Public
                            | BindingFlags.NonPublic)
                        ?.GetValue(item) as GUIContent;
                if (content == null
                    || !predicate(content.text ?? string.Empty))
                {
                    continue;
                }

                var callback =
                    itemType.GetField(
                            "func",
                            BindingFlags.Instance
                            | BindingFlags.Public
                            | BindingFlags.NonPublic)
                        ?.GetValue(item) as Delegate;
                Assert.IsNotNull(callback);
                callback.DynamicInvoke();
                return;
            }

            Assert.Fail("GenericMenu item was not found.");
        }

        private static IList GetMenuItems(GenericMenu menu)
        {
            Assert.IsNotNull(menu);
            FieldInfo field = typeof(GenericMenu).GetField(
                "m_MenuItems",
                BindingFlags.Instance
                | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var items = field.GetValue(menu) as IList;
            Assert.IsNotNull(items);
            return items;
        }

        private static bool ContainsPrefix(
            IReadOnlyList<string> labels,
            string prefix)
        {
            for (int i = 0; labels != null && i < labels.Count; i++)
            {
                if (labels[i].StartsWith(
                        prefix,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void PressButton(Button button)
        {
            Assert.IsNotNull(button);
            object clickable = typeof(Button)
                .GetProperty(
                    "clickable",
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
                ?.GetValue(button);
            Assert.IsNotNull(clickable);
            MethodInfo[] methods = clickable.GetType().GetMethods(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic);
            MethodInfo invoke = null;
            for (int i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(
                        methods[i].Name,
                        "Invoke",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters =
                    methods[i].GetParameters();
                if (parameters.Length == 0
                    || (parameters.Length == 1
                        && typeof(EventBase).IsAssignableFrom(
                            parameters[0].ParameterType)))
                {
                    invoke = methods[i];
                    break;
                }
            }
            Assert.IsNotNull(invoke);
            if (invoke.GetParameters().Length == 0)
            {
                invoke.Invoke(
                    clickable,
                    Array.Empty<object>());
                return;
            }

            using (ClickEvent click = ClickEvent.GetPooled())
                invoke.Invoke(clickable, new object[] { click });
        }
    }
}
