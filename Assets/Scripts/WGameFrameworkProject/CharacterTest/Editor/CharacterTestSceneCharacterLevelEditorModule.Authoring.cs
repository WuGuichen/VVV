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
        private void RebuildRows()
        {
            _rows.Clear();
            if (_document?.Characters == null)
                return;

            for (int i = 0; i < _document.Characters.Count; i++)
            {
                CharacterTestSceneCharacterDefinition character = _document.Characters[i];
                if (character == null)
                    continue;

                string title = string.IsNullOrWhiteSpace(character.DisplayName)
                    ? character.StableId
                    : character.DisplayName;
                string transformPath = character.TransformRef?.Path ?? character.TransformPath ?? string.Empty;
                string status = ResolveCharacterStatus(character);
                _rows.Add(new Row(
                    i,
                    "场景角色",
                    character.Group,
                    ResolveCharacterDefinitionLabel(character.CharacterId),
                    character.StableId,
                    transformPath,
                    status,
                    title,
                    character.ControlOwner,
                    character.TeamId,
                    character.Enabled));
            }
        }

        private VisualElement CreateCharacterActionRow(MxLevelEditorWindow window)
        {
            var row = new VisualElement { name = "character-test-character-action-row" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.NoWrap;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.FlexEnd;

            var addMenu = new ToolbarMenu
            {
                name = "character-test-character-add-menu",
                text = "+ 添加角色"
            };
            addMenu.AddToClassList("mx-level-editor__add-menu");
            addMenu.menu.AppendAction("创建默认角色", _ =>
            {
                CreateDefaultTemplate();
                window?.RepaintRequested();
            });
            addMenu.menu.AppendAction("添加选中为本地玩家", _ =>
            {
                AddSelectedTransform(CharacterTestCharacterControlOwner.LocalPlayer);
                window?.RepaintRequested();
            });
            addMenu.menu.AppendAction("添加选中为 Runtime AI Planner", _ =>
            {
                AddSelectedTransform(CharacterTestCharacterControlOwner.AiControlled);
                window?.RepaintRequested();
            });
            addMenu.menu.AppendSeparator();
            addMenu.menu.AppendAction("打开 Runtime AI Planner", _ =>
            {
                EditorApplication.ExecuteMenuItem(CharacterTestRuntimeAiPlannerWindow.MenuPath);
            });
            row.Add(addMenu);
            return row;
        }

        private static string ResolveCharacterStatus(CharacterTestSceneCharacterDefinition character)
        {
            if (character == null)
                return string.Empty;

            if (!character.Enabled)
                return "disabled";

            if (character.ControlOwner != CharacterTestCharacterControlOwner.AiControlled
                || string.IsNullOrWhiteSpace(character.RuntimeAiPlannerProfilePath))
            {
                return string.Empty;
            }

            string path = NormalizeRuntimeAiPlannerProfilePathForEditor(character.RuntimeAiPlannerProfilePath);
            string fileName = Path.GetFileNameWithoutExtension(path);
            return string.IsNullOrWhiteSpace(fileName)
                ? "Runtime AI Planner 自定义配置"
                : "Runtime AI Planner " + fileName;
        }

        private VisualElement CreateEmptyStatePanel(MxLevelEditorWindow window)
        {
            var panel = new VisualElement { name = EmptyStateRootName };
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.marginLeft = 0;
            panel.style.marginRight = 0;
            panel.style.marginBottom = 8;
            panel.style.paddingLeft = 14;
            panel.style.paddingRight = 14;
            panel.style.paddingTop = 12;
            panel.style.paddingBottom = 12;
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderTopColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            panel.style.borderRightColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            panel.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            panel.style.borderLeftColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            panel.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.92f);

            VisualElement title = CreateTitle("暂无 CharacterTest 角色");
            title.style.marginBottom = 4;
            panel.Add(title);
            panel.Add(new Label("先创建或放置测试角色。保存后重建 CharacterTest 会话生效。"));
            panel.Add(new Label("Runtime AI Planner 控制的角色可单独指定行为配置；未指定时继承当前场景级配置。"));

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 10;
            panel.Add(actions);

            Button defaultButton = CreateButton("创建默认角色", () =>
            {
                CreateDefaultTemplate();
                window?.RepaintRequested();
            });
            defaultButton.name = EmptyStateDefaultButtonName;
            defaultButton.style.minWidth = 126;
            actions.Add(defaultButton);

            Button placeButton = CreateButton(_placementActive ? "放置中..." : "开始放置角色", () =>
            {
                BeginScenePlacement(_activeContext);
                window?.RepaintRequested();
            });
            placeButton.name = EmptyStatePlaceButtonName;
            placeButton.SetEnabled(!_placementActive);
            placeButton.style.minWidth = 126;
            actions.Add(placeButton);

            Button plannerButton = CreateButton("Runtime AI Planner", () =>
            {
                EditorApplication.ExecuteMenuItem(CharacterTestRuntimeAiPlannerWindow.MenuPath);
            });
            plannerButton.style.minWidth = 150;
            actions.Add(plannerButton);

            return panel;
        }

        private void BeginScenePlacement(MxLevelEditorContext context)
        {
            ResetCharacterPlacementOperation();
            _placementActive = true;
            _status = "角色放置已开启：在 SceneView 左键确认，Esc 取消。";
            context?.RequestRefresh?.Invoke();
            SceneView.RepaintAll();
        }

        private void CreateDefaultTemplate()
        {
            EnsureLoaded();
            string levelId = _document.LevelId;
            string scenePath = _document.ScenePath;
            string prefix = string.IsNullOrEmpty(CharacterTestLevelEditorDocumentSession.CurrentPath) ? string.Empty : levelId + ".";
            GameManager gameManager = FindGameManager();
            Transform root = gameManager == null ? null : gameManager.WorldObjectSceneRoot;
            if (root != null)
            {
                Transform player = EnsureMarker(root, "Spawns", "Player", new Vector3(0f, 1f, 0f));
                Transform enemy = EnsureMarker(root, "Spawns", "Enemy_AI", new Vector3(0f, 1f, 4f));
                _document = new CharacterTestSceneCharacterAuthoringDocument
                {
                    LevelId = levelId,
                    ScenePath = scenePath,
                    RootPath = root.name,
                    Characters = new List<CharacterTestSceneCharacterDefinition>
                    {
                        CreateCharacterDefinition(
                            root,
                            player,
                            prefix + "player.local",
                            "本地玩家",
                            1,
                            CharacterTestCharacterControlOwner.LocalPlayer),
                        CreateCharacterDefinition(
                            root,
                            enemy,
                            prefix + "enemy.ai.01",
                            "Runtime AI Planner 敌人",
                            2,
                            CharacterTestCharacterControlOwner.AiControlled)
                    }
                }.Normalize();
            }
            else
            {
                _document = CharacterTestSceneCharacterAuthoringAdapter.CreateDefaultDocument();
                _document.LevelId = levelId; _document.ScenePath = scenePath;
                foreach (var actor in _document.Characters) actor.StableId = prefix + actor.StableId;
            }

            _status = "已创建默认 CharacterTest 角色模板。";
            MarkDirty();
        }

        private void AddSelectedTransform(CharacterTestCharacterControlOwner owner)
        {
            Transform transform = ResolveSelectionTransform(Selection.activeObject);
            if (transform == null)
            {
                _status = "请先选择场景 GameObject 或 Transform。";
                return;
            }

            AddTransform(transform, owner, GuessTeamId(owner));
        }

        private void AddTransform(
            Transform transform,
            CharacterTestCharacterControlOwner owner,
            int teamId)
        {
            EnsureLoaded();
            GameManager gameManager = FindGameManager();
            Transform root = gameManager == null ? transform.root : gameManager.WorldObjectSceneRoot;
            if (root == null || transform == null)
            {
                _status = "没有有效的 Transform 可供添加。";
                return;
            }

            if (_document.Characters == null)
                _document.Characters = new List<CharacterTestSceneCharacterDefinition>();

            string stableId = CreateStableId(owner, _document.Characters.Count + 1);
            while (ContainsStableId(stableId))
                stableId = CreateStableId(owner, _document.Characters.Count + UnityEngine.Random.Range(2, 10000));

            _document.Characters.Add(CreateCharacterDefinition(
                root,
                transform,
                stableId,
                transform.name,
                teamId,
                owner));
            _document = _document.Normalize();
            _status = "已添加 CharacterTest 场景角色：" + stableId + "。";
            MarkDirty();
        }

        private GameObject CreateCharacterMarker(
            CharacterTestCharacterControlOwner owner,
            Vector3 position)
        {
            GameManager gameManager = FindGameManager();
            if (gameManager == null)
            {
                _status = "当前场景中未找到 CharacterTest GameManager。";
                return null;
            }

            Transform parent = EnsureChild(gameManager.WorldObjectSceneRoot, "Characters");
            var created = new GameObject(CreateUniqueChildName(parent, owner));
            Undo.RegisterCreatedObjectUndo(created, "放置 CharacterTest 场景角色");
            created.transform.SetParent(parent, worldPositionStays: true);
            created.transform.position = position;
            EditorSceneManager.MarkSceneDirty(created.scene);
            return created;
        }

    }
}
