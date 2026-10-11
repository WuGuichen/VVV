using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestSceneCharacterLevelEditorModule
    {
        private CharacterTestSceneCharacterDefinition CreateCharacterDefinition(
            Transform root,
            Transform transform,
            string stableId,
            string displayName,
            int teamId,
            CharacterTestCharacterControlOwner owner)
        {
            LevelTransformReference transformRef =
                CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
            return new CharacterTestSceneCharacterDefinition
            {
                Enabled = true,
                StableId = stableId,
                DisplayName = displayName,
                TransformRef = transformRef,
                TransformPath = transformRef.Path,
                SpawnPose = CaptureSpawnPose(transform),
                CharacterId = _placementCharacterId,
                TeamId = teamId,
                ControlOwner = owner,
                RuntimeAiPlannerProfilePath = string.Empty,
                Group = owner == CharacterTestCharacterControlOwner.LocalPlayer ? "Players" : "Enemies"
            };
        }

        private static CharacterTestSceneCharacterSpawnPose CaptureSpawnPose(Transform transform)
        {
            return transform == null ? null : new CharacterTestSceneCharacterSpawnPose
            {
                Position = transform.position,
                YawDegrees = transform.eulerAngles.y
            };
        }

        private static Transform EnsureMarker(
            Transform root,
            string groupName,
            string markerName,
            Vector3 position)
        {
            Transform group = EnsureChild(root, groupName);
            Transform marker = group.Find(markerName);
            if (marker != null)
                return marker;

            var created = new GameObject(markerName);
            Undo.RegisterCreatedObjectUndo(created, "创建 CharacterTest 角色标记");
            created.transform.SetParent(group, worldPositionStays: true);
            created.transform.position = position;
            EditorSceneManager.MarkSceneDirty(created.scene);
            return created.transform;
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform existing = parent == null ? null : parent.Find(childName);
            if (existing != null)
                return existing;

            var created = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(created, "创建 CharacterTest 标记分组");
            if (parent != null)
                created.transform.SetParent(parent, worldPositionStays: false);
            return created.transform;
        }

        private bool ContainsStableId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId) || _document?.Characters == null)
                return false;

            for (int i = 0; i < _document.Characters.Count; i++)
            {
                if (string.Equals(_document.Characters[i]?.StableId, stableId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private bool TryResolveRowTransform(Row row, out Transform transform)
        {
            transform = null;
            if (row == null || _document?.Characters == null || row.Index < 0 || row.Index >= _document.Characters.Count)
                return false;

            GameManager gameManager = FindGameManager();
            Transform root = gameManager == null ? null : gameManager.WorldObjectSceneRoot;
            if (root == null)
                return false;

            CharacterTestSceneCharacterDefinition character = _document.Characters[row.Index];
            return CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(root, character.TransformRef, out transform);
        }

        private static Transform ResolveSelectionTransform(Object selectedObject)
        {
            if (selectedObject is GameObject gameObject)
                return gameObject.transform;
            if (selectedObject is Component component)
                return component.transform;
            return null;
        }

        private static GameManager FindGameManager()
        {
            GameManager[] managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Exclude);
            if (managers == null || managers.Length == 0)
                return null;

            string activeScene = EditorSceneManager.GetActiveScene().path;
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null
                    && string.Equals(managers[i].gameObject.scene.path, activeScene, StringComparison.Ordinal))
                {
                    return managers[i];
                }
            }

            return managers[0];
        }

        private static bool TryResolvePlacementPosition(Vector2 mousePosition, out Vector3 position)
        {
            return CharacterTestLevelEditorPlacementUtility.TryResolveSurfacePosition(
                mousePosition,
                out position,
                out _);
        }

        private static string CreateStableId(CharacterTestCharacterControlOwner owner, int index)
        {
            string prefix = owner == CharacterTestCharacterControlOwner.LocalPlayer
                ? "player.local"
                : owner == CharacterTestCharacterControlOwner.AiControlled
                    ? "enemy.ai"
                    : "character";
            return prefix + "." + Mathf.Max(1, index).ToString("00");
        }

        private static int GuessTeamId(CharacterTestCharacterControlOwner owner)
        {
            return owner == CharacterTestCharacterControlOwner.LocalPlayer ? 1 : 2;
        }

        private static string CreateUniqueChildName(Transform parent, CharacterTestCharacterControlOwner owner)
        {
            string baseName = owner == CharacterTestCharacterControlOwner.LocalPlayer
                ? "Character_Player"
                : owner == CharacterTestCharacterControlOwner.AiControlled
                    ? "Character_AI"
                    : "Character";
            string name = baseName;
            int suffix = 1;
            while (parent != null && parent.Find(name) != null)
                name = baseName + "_" + suffix++;
            return name;
        }

        private string ResolveCharacterDefinitionLabel(int characterId)
        {
            return _characterDefinitions.TryGet(
                    characterId,
                    out CharacterTestCharacterDefinition definition)
                ? FormatCharacterDefinitionLabel(definition)
                : characterId + " 缺失";
        }

        private int CountOwner(CharacterTestCharacterControlOwner owner)
        {
            int count = 0;
            if (_document?.Characters == null)
                return 0;

            for (int i = 0; i < _document.Characters.Count; i++)
            {
                if (_document.Characters[i] != null && _document.Characters[i].ControlOwner == owner)
                    count++;
            }

            return count;
        }

        private int CountGroup(string group)
        {
            int count = 0;
            if (_document?.Characters == null)
                return 0;

            for (int i = 0; i < _document.Characters.Count; i++)
            {
                if (string.Equals(_document.Characters[i]?.Group, group, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static int Count<T>(IReadOnlyList<T> values)
        {
            return values == null ? 0 : values.Count;
        }

        private static bool RowMatchesTree(Row row, string treeNodeId)
        {
            if (row == null || string.IsNullOrWhiteSpace(treeNodeId) || treeNodeId == "document")
                return true;
            if (treeNodeId.StartsWith("owner:", StringComparison.Ordinal))
            {
                string owner = treeNodeId.Substring("owner:".Length);
                return string.Equals(row.Owner.ToString(), owner, StringComparison.Ordinal);
            }
            if (treeNodeId.StartsWith("group:", StringComparison.Ordinal))
            {
                string group = treeNodeId.Substring("group:".Length);
                return string.Equals(row.Group, group, StringComparison.Ordinal);
            }

            return true;
        }

        private static string GetTreeNodeId(Row row)
        {
            if (row == null)
                return "document";
            if (!string.IsNullOrWhiteSpace(row.Group))
                return "group:" + row.Group;
            return "owner:" + row.Owner;
        }

        private static VisualElement CreateTitle(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 6;
            return label;
        }

        private VisualElement CreateToggle(string label, bool value, Action<bool> setter)
        {
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkDirty();
            });
            return field;
        }

        private VisualElement CreateTextField(string label, string value, Action<string> setter)
        {
            var field = new TextField(label) { value = value ?? string.Empty };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkDirty();
            });
            return field;
        }

        private VisualElement CreateIntegerField(string label, int value, Action<int> setter)
        {
            var field = new IntegerField(label) { value = value };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkDirty();
            });
            return field;
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 4;
            button.style.marginTop = 6;
            return button;
        }

        private static string ToFullPath(string projectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static void WriteEnvelope(string projectRelativePath, MxLevelAuthoringDocument envelope)
        {
            string fullPath = ToFullPath(projectRelativePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, MxLevelAuthoringJson.SaveToJson(envelope), System.Text.Encoding.UTF8);
        }

        private static string BuildValidationBlockedStatus(string operation)
        {
            return operation + "已被校验错误阻止。";
        }

        private static HelpBoxMessageType ToHelpBoxMessageType(
            CharacterTestSceneCharacterCacheSyncStatus status)
        {
            if (status.Kind == CharacterTestSceneCharacterCacheSyncKind.Applied
                || status.Kind == CharacterTestSceneCharacterCacheSyncKind.EmptyDocument)
            {
                return HelpBoxMessageType.Info;
            }

            if (status.Kind == CharacterTestSceneCharacterCacheSyncKind.MissingSceneTarget)
                return HelpBoxMessageType.Error;

            return HelpBoxMessageType.Warning;
        }

        private static Color ResolveOwnerColor(CharacterTestCharacterControlOwner owner)
        {
            if (owner == CharacterTestCharacterControlOwner.LocalPlayer)
                return new Color(0.25f, 0.75f, 1f, 0.9f);
            if (owner == CharacterTestCharacterControlOwner.AiControlled)
                return new Color(1f, 0.35f, 0.25f, 0.9f);
            return new Color(0.8f, 0.8f, 0.8f, 0.85f);
        }

        private static GUIStyle _sceneLabelStyle;
        private static GUIStyle _sceneSelectedLabelStyle;
        private static GUIStyle _sceneDisabledLabelStyle;
        private static GUIStyle _sceneHintPanelStyle;
        private static GUIStyle _sceneHintTitleStyle;
        private static GUIStyle _sceneHintBodyStyle;

        private static GUIStyle SceneLabelStyle
        {
            get
            {
                if (_sceneLabelStyle != null)
                    return _sceneLabelStyle;

                _sceneLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = Color.white }
                };
                return _sceneLabelStyle;
            }
        }

        private static GUIStyle SceneSelectedLabelStyle
        {
            get
            {
                if (_sceneSelectedLabelStyle != null)
                    return _sceneSelectedLabelStyle;

                _sceneSelectedLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = Color.yellow }
                };
                return _sceneSelectedLabelStyle;
            }
        }

        private static GUIStyle SceneDisabledLabelStyle
        {
            get
            {
                if (_sceneDisabledLabelStyle != null)
                    return _sceneDisabledLabelStyle;

                _sceneDisabledLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = new Color(0.62f, 0.62f, 0.62f, 0.9f) }
                };
                return _sceneDisabledLabelStyle;
            }
        }

        private static GUIStyle SceneHintPanelStyle
        {
            get
            {
                if (_sceneHintPanelStyle != null)
                    return _sceneHintPanelStyle;

                _sceneHintPanelStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 8, 8),
                    normal = { textColor = Color.white }
                };
                return _sceneHintPanelStyle;
            }
        }

        private static GUIStyle SceneHintTitleStyle
        {
            get
            {
                if (_sceneHintTitleStyle != null)
                    return _sceneHintTitleStyle;

                _sceneHintTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = Color.white },
                    wordWrap = false
                };
                return _sceneHintTitleStyle;
            }
        }

        private static GUIStyle SceneHintBodyStyle
        {
            get
            {
                if (_sceneHintBodyStyle != null)
                    return _sceneHintBodyStyle;

                _sceneHintBodyStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(0.86f, 0.86f, 0.86f, 1f) },
                    wordWrap = true
                };
                return _sceneHintBodyStyle;
            }
        }

        private static void DrawSceneOverlayItem(SceneOverlayItem item)
        {
            if (item == null)
                return;

            Handles.color = ResolveOverlayColor(item);
            Handles.DrawWireDisc(item.Position, Vector3.up, OverlayRadius);
            Handles.Label(
                item.Position + Vector3.up * 0.25f,
                FormatSceneOverlayLabel(item),
                ResolveOverlayLabelStyle(item));
        }

        internal static string FormatSceneOverlayLabelForTests(SceneOverlayItem item)
        {
            return FormatSceneOverlayLabel(item);
        }

        private static string FormatSceneOverlayLabel(SceneOverlayItem item)
        {
            if (item == null)
                return string.Empty;

            string suffix = item.Enabled ? string.Empty : " (禁用)";
            return item.Label + " [" + item.Owner + "]" + suffix;
        }

        private static Color ResolveOverlayColor(SceneOverlayItem item)
        {
            if (item == null)
                return Color.white;
            if (!item.Enabled && item.IsSelected)
                return new Color(1f, 0.9f, 0.25f, 0.55f);
            if (!item.Enabled)
                return new Color(0.55f, 0.55f, 0.55f, 0.45f);
            return item.IsSelected ? Color.yellow : ResolveOwnerColor(item.Owner);
        }

        private static GUIStyle ResolveOverlayLabelStyle(SceneOverlayItem item)
        {
            if (item == null)
                return SceneLabelStyle;
            if (!item.Enabled)
                return SceneDisabledLabelStyle;
            return item.IsSelected ? SceneSelectedLabelStyle : SceneLabelStyle;
        }

        private static void DrawSceneHintOverlay(SceneHintViewModel hint, SceneView sceneView)
        {
            if (hint == null
                || sceneView == null
                || Event.current == null
                || Event.current.type != EventType.Repaint)
            {
                return;
            }

            float width = Mathf.Clamp(sceneView.position.width - 24f, 320f, 620f);
            float contentWidth = width - 20f;
            float height = 16f
                + CalculateSceneHintLineHeight(hint.Title, SceneHintTitleStyle, contentWidth)
                + CalculateSceneHintLineHeight(hint.Summary, SceneHintBodyStyle, contentWidth)
                + CalculateSceneHintLineHeight(hint.Selected, SceneHintBodyStyle, contentWidth)
                + CalculateSceneHintLineHeight(hint.Operation, SceneHintBodyStyle, contentWidth)
                + CalculateSceneHintLineHeight(hint.Ai, SceneHintBodyStyle, contentWidth);
            var rect = new Rect(12f, 12f, width, height);
            Handles.BeginGUI();
            try
            {
                GUI.Box(rect, GUIContent.none, SceneHintPanelStyle);
                float y = rect.y + 8f;
                DrawSceneHintLine(rect.x + 10f, ref y, contentWidth, hint.Title, SceneHintTitleStyle);
                DrawSceneHintLine(rect.x + 10f, ref y, contentWidth, hint.Summary, SceneHintBodyStyle);
                DrawSceneHintLine(rect.x + 10f, ref y, contentWidth, hint.Selected, SceneHintBodyStyle);
                DrawSceneHintLine(rect.x + 10f, ref y, contentWidth, hint.Operation, SceneHintBodyStyle);
                DrawSceneHintLine(rect.x + 10f, ref y, contentWidth, hint.Ai, SceneHintBodyStyle);
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        private static float CalculateSceneHintLineHeight(string text, GUIStyle style, float width)
        {
            return Mathf.Ceil(style.CalcHeight(new GUIContent(text ?? string.Empty), width)) + 2f;
        }

        private static void DrawSceneHintLine(
            float x,
            ref float y,
            float width,
            string text,
            GUIStyle style)
        {
            float height = CalculateSceneHintLineHeight(text, style, width);
            GUI.Label(new Rect(x, y, width, height), text ?? string.Empty, style);
            y += height;
        }

        private static string BuildSceneHintSelectedText(
            MxLevelEditorSceneViewContext context,
            IReadOnlyList<SceneOverlayItem> items)
        {
            if (context?.SelectedRow?.Payload is Row row)
                return "选中：" + row.Title + " [" + GetControlOwnerLabel(row.Owner) + "] 队伍 " + row.TeamId;

            int count = items == null ? 0 : items.Count;
            for (int i = 0; i < count; i++)
            {
                SceneOverlayItem item = items[i];
                if (item != null && item.IsSelected)
                    return "选中：" + item.Label + " [" + GetControlOwnerLabel(item.Owner) + "]";
            }

            return "未选中角色。";
        }

    }
}
