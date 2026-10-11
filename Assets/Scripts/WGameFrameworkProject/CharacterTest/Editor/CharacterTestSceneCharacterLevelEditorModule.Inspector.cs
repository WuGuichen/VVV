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
        private void DrawSummaryInspector(VisualElement inspector)
        {
            inspector.Add(CreateTitle("开始编辑"));
            var actions = new VisualElement();
            actions.AddToClassList("mx-level-editor__inspector-actions");
            actions.Add(CreateButton("创建默认角色", () =>
            {
                CreateDefaultTemplate();
                _activeContext?.RequestRefresh?.Invoke();
            }));
            actions.Add(CreateButton("进入放置模式", () =>
            {
                BeginScenePlacement(_activeContext);
                _activeContext?.RequestRefresh?.Invoke();
            }));
            actions.Add(CreateButton("选择场景根节点", () =>
            {
                SelectRootSource();
                _activeContext?.RequestRefresh?.Invoke();
            }));
            inspector.Add(actions);
            inspector.Add(new Label("模块: " + CharacterTestSceneCharacterAuthoringAdapter.CharactersModuleId));
            inspector.Add(new Label("文档: " + DocumentPath));
            inspector.Add(new Label("角色数: " + Count(_document?.Characters)));
            inspector.Add(new Label("错误: " + (_report?.ErrorCount ?? 0) + "  警告: " + (_report?.WarningCount ?? 0)));
            CharacterTestSceneCharacterCacheSyncStatus syncStatus = BuildCacheSyncStatus();
            inspector.Add(new HelpBox(syncStatus.Message, ToHelpBoxMessageType(syncStatus)));
            if (IsDocumentEmpty())
            {
                inspector.Add(new HelpBox(
                    "待配置：当前没有本地玩家，也没有 Runtime AI Planner 控制的角色。",
                    HelpBoxMessageType.Info));
                inspector.Add(new HelpBox(
                    "下一步：创建默认角色，或点击“开始放置角色”。完成后点击保存。",
                    HelpBoxMessageType.Info));
            }

            inspector.Add(new HelpBox(
                "角色行为来自自己的 Runtime AI Planner JSON；未指定时继承当前场景 CharacterTestSceneProfile 的 JSON。控制来源设为 Runtime AI Planner 后，重建 CharacterTest 会话生效。",
                HelpBoxMessageType.Info));
        }

        private void DrawCharacterInspector(
            VisualElement inspector,
            CharacterTestSceneCharacterDefinition character)
        {
            inspector.Add(CreateTitle(
                string.IsNullOrWhiteSpace(character.DisplayName)
                    ? "场景角色"
                    : character.DisplayName));
            inspector.Add(CreateCharacterInspectorTabs());
            if (_showRuntimeInspector)
            {
                DrawCharacterRuntimeInspector(inspector, character);
                return;
            }

            inspector.Add(CreateToggle("启用", character.Enabled, value => character.Enabled = value));
            inspector.Add(CreateTextField("显示名", character.DisplayName, value => character.DisplayName = value));
            inspector.Add(CreateIntegerField("队伍", character.TeamId, value => character.TeamId = value));
            inspector.Add(CreateControlOwnerPopup(
                "控制来源",
                character.ControlOwner,
                value => character.ControlOwner = value,
                "character-test-character-inspector-control-owner",
                true));
            inspector.Add(CreateRuntimeAiPlannerProfileSelector(character));
            DrawLevelBehaviorBinding(inspector, character);
            inspector.Add(CreateCharacterPopup(character));
            inspector.Add(CreateTextField("场景对象路径", character.TransformRef?.Path ?? string.Empty, value =>
            {
                character.TransformRef ??= new LevelTransformReference();
                character.TransformRef.Path = value;
                character.TransformPath = value;
            }));
            inspector.Add(CreateTextField("分组", character.Group, value => character.Group = value));
            inspector.Add(CreateTextField("备注", character.Notes, value => character.Notes = value));
            inspector.Add(new HelpBox(
                character.ControlOwner == CharacterTestCharacterControlOwner.AiControlled
                    ? "该角色会使用上方选择的 Runtime AI Planner 配置；空值表示继承场景默认。"
                    : "只有 Runtime AI Planner 控制的角色会由其命令模块驱动。",
                HelpBoxMessageType.Info));

            Foldout advanced = new Foldout { text = "高级", value = false };
            advanced.Add(CreateTextField("稳定 ID", character.StableId, value => character.StableId = value));
            advanced.Add(CreateIntegerField("角色 ID", character.CharacterId, value => character.CharacterId = value));
            inspector.Add(advanced);

            inspector.Add(CreateCharacterQuickPlacementActions(_selectedRow, character));
        }

        private VisualElement CreateCharacterInspectorTabs()
        {
            var tabs = new VisualElement { name = "character-test-character-inspector-tabs" };
            tabs.AddToClassList("mx-level-editor__inspector-tabs");

            var properties = new Button(() =>
            {
                _showRuntimeInspector = false;
                _activeContext?.RequestRefresh?.Invoke();
            })
            {
                name = "character-test-character-properties-tab",
                text = "属性"
            };
            properties.EnableInClassList(
                "mx-level-editor__inspector-tab--selected",
                !_showRuntimeInspector);
            tabs.Add(properties);

            var runtime = new Button(() =>
            {
                _showRuntimeInspector = true;
                _activeContext?.RequestRefresh?.Invoke();
            })
            {
                name = "character-test-character-runtime-tab",
                text = "运行时检查"
            };
            runtime.EnableInClassList(
                "mx-level-editor__inspector-tab--selected",
                _showRuntimeInspector);
            tabs.Add(runtime);
            return tabs;
        }

        private void DrawCharacterRuntimeInspector(
            VisualElement inspector,
            CharacterTestSceneCharacterDefinition character)
        {
            CharacterTestSceneCharacterCacheSyncStatus syncStatus = BuildCacheSyncStatus();
            inspector.Add(new HelpBox(syncStatus.Message, ToHelpBoxMessageType(syncStatus)));
            inspector.Add(new Label("启用状态：" + (character.Enabled ? "有效" : "已禁用")));
            inspector.Add(new Label("控制来源：" + GetControlOwnerLabel(character.ControlOwner)));
            inspector.Add(new Label("队伍：" + character.TeamId));
            inspector.Add(new Label(
                "场景对象："
                + (character.TransformRef?.Path ?? character.TransformPath ?? "未绑定")));
            string planner = string.IsNullOrWhiteSpace(character.RuntimeAiPlannerProfilePath)
                ? "继承场景默认"
                : character.RuntimeAiPlannerProfilePath;
            inspector.Add(new Label("Runtime AI Planner：" + planner));

            var actions = new VisualElement();
            actions.AddToClassList("mx-level-editor__inspector-actions");
            actions.Add(CreateButton("选择", () =>
            {
                if (_selectedRow != null
                    && TryResolveRowTransform(_selectedRow, out Transform transform))
                {
                    Selection.activeGameObject = transform.gameObject;
                }
            }));
            actions.Add(CreateButton("聚焦", () =>
            {
                if (_selectedRow != null
                    && TryResolveRowTransform(_selectedRow, out Transform transform))
                {
                    Selection.activeGameObject = transform.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }));
            inspector.Add(actions);

            var advanced = new Foldout
            {
                text = "文档元数据",
                value = false
            };
            advanced.Add(new Label("稳定 ID：" + character.StableId));
            advanced.Add(new Label("角色 ID：" + character.CharacterId));
            advanced.Add(new Label("分组：" + character.Group));
            inspector.Add(advanced);
        }

        private PopupField<string> CreateCharacterPopup(CharacterTestSceneCharacterDefinition character)
        {
            return CreateCharacterDefinitionPopup(
                "角色定义",
                character.CharacterId,
                selectedId =>
                {
                    character.CharacterId = selectedId;
                    MarkDirty();
                });
        }

        private VisualElement CreateRuntimeAiPlannerProfileSelector(
            CharacterTestSceneCharacterDefinition character)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;

            string selectedProjectPath = string.IsNullOrWhiteSpace(character.RuntimeAiPlannerProfilePath)
                ? string.Empty
                : CharacterTestRuntimeAiPlannerProfilePicker.NormalizeProjectAssetPath(
                    character.RuntimeAiPlannerProfilePath);
            var picker = new CharacterTestRuntimeAiPlannerProfilePicker(
                "Runtime AI Planner 行为",
                selectedProjectPath,
                includeInherit: true,
                inheritLabel: BuildSceneDefaultRuntimeAiPlannerInheritLabel(),
                onSelected: entry =>
                {
                    if (entry.IsInherit)
                    {
                        character.RuntimeAiPlannerProfilePath = string.Empty;
                        MarkDirty();
                        return;
                    }

                    if (!entry.IsValid)
                    {
                        _status = "Runtime AI Planner 配置校验失败，不能绑定到角色。";
                        _activeContext?.RequestRefresh?.Invoke();
                        return;
                    }

                    character.RuntimeAiPlannerProfilePath = entry.RuntimePath;
                    MarkDirty();
                },
                popupName: RuntimeAiPlannerProfileFieldName)
            {
                name = RuntimeAiPlannerProfileFieldName,
                tooltip = "空值表示继承场景 CharacterTestSceneProfile 的 Runtime AI Planner JSON。"
            };
            picker.SetEnabled(character.ControlOwner == CharacterTestCharacterControlOwner.AiControlled);
            container.Add(picker);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.Add(CreateButton("打开编辑", () =>
            {
                string projectPath = string.IsNullOrWhiteSpace(character.RuntimeAiPlannerProfilePath)
                    ? GetSceneDefaultRuntimeAiPlannerProjectPath()
                    : CharacterTestRuntimeAiPlannerProfilePicker.NormalizeProjectAssetPath(
                        character.RuntimeAiPlannerProfilePath);
                if (string.IsNullOrWhiteSpace(projectPath))
                {
                    _status = "当前角色和场景都没有 Runtime AI Planner 配置。";
                    _activeContext?.RequestRefresh?.Invoke();
                    return;
                }

                CharacterTestRuntimeAiPlannerWindow.OpenProfile(projectPath);
            }));
            actions.Add(CreateButton("继承场景默认", () =>
            {
                character.RuntimeAiPlannerProfilePath = string.Empty;
                MarkDirty();
                _activeContext?.RequestRefresh?.Invoke();
            }));
            container.Add(actions);

            if (character.ControlOwner != CharacterTestCharacterControlOwner.AiControlled)
            {
                container.Add(new HelpBox(
                    "只有 Runtime AI Planner 控制的角色会使用行为配置。",
                    HelpBoxMessageType.Info));
            }

            var advanced = new Foldout
            {
                text = "高级 JSON 路径",
                value = false
            };
            string currentPath = string.IsNullOrWhiteSpace(character.RuntimeAiPlannerProfilePath)
                ? "继承场景默认"
                : character.RuntimeAiPlannerProfilePath;
            var pathField = new TextField("当前路径")
            {
                value = currentPath,
                isReadOnly = true
            };
            advanced.Add(pathField);

            var advancedActions = new VisualElement();
            advancedActions.style.flexDirection = FlexDirection.Row;
            advancedActions.style.flexWrap = Wrap.Wrap;
            Button choose = CreateButton("选择 JSON", () =>
            {
                string selected = EditorUtility.OpenFilePanel(
                    "选择 Runtime AI Planner JSON",
                    Application.streamingAssetsPath,
                    "json");
                if (string.IsNullOrWhiteSpace(selected))
                    return;

                if (!TryConvertProfileFileToStreamingAssetsPath(selected, out string runtimePath))
                {
                    _status = "Runtime AI Planner JSON 必须位于 Assets/StreamingAssets 下。";
                    return;
                }

                character.RuntimeAiPlannerProfilePath = runtimePath;
                MarkDirty();
                _activeContext?.RequestRefresh?.Invoke();
            });
            choose.name = RuntimeAiPlannerProfileChooseButtonName;
            choose.SetEnabled(character.ControlOwner == CharacterTestCharacterControlOwner.AiControlled);
            advancedActions.Add(choose);
            advanced.Add(advancedActions);
            container.Add(advanced);
            return container;
        }

        private static string BuildSceneDefaultRuntimeAiPlannerInheritLabel()
        {
            string projectPath = GetSceneDefaultRuntimeAiPlannerProjectPath();
            if (string.IsNullOrWhiteSpace(projectPath))
                return "继承场景默认（未设置）";

            return CharacterTestRuntimeAiPlannerProfilePicker.BuildInheritLabel(
                CharacterTestRuntimeAiPlannerProfilePicker.ToStreamingAssetsRelativePath(projectPath));
        }

        private static string GetSceneDefaultRuntimeAiPlannerProjectPath()
        {
            CharacterTestSceneProfile sceneProfile =
                AssetDatabase.LoadAssetAtPath<CharacterTestSceneProfile>(
                    CharacterTestSceneProfile.DefaultAssetPath);
            if (sceneProfile == null || string.IsNullOrWhiteSpace(sceneProfile.RuntimeAiPlannerProfilePath))
                return string.Empty;

            return CharacterTestRuntimeAiPlannerProfilePicker.NormalizeProjectAssetPath(
                sceneProfile.RuntimeAiPlannerProfilePath);
        }

        private static string NormalizeRuntimeAiPlannerProfilePathForEditor(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return CharacterTestRuntimeAiPlannerProfileJson.NormalizeStreamingAssetsRelativePath(value);
        }

        private static bool TryConvertProfileFileToStreamingAssetsPath(
            string fullPath,
            out string runtimePath)
        {
            runtimePath = string.Empty;
            if (string.IsNullOrWhiteSpace(fullPath))
                return false;

            string normalizedFullPath = fullPath.Replace('\\', '/');
            string streamingAssetsPath = Application.streamingAssetsPath.Replace('\\', '/');
            if (!normalizedFullPath.StartsWith(streamingAssetsPath + "/", StringComparison.OrdinalIgnoreCase))
                return false;

            runtimePath = CharacterTestRuntimeAiPlannerProfileJson.NormalizeStreamingAssetsRelativePath(
                normalizedFullPath.Substring(streamingAssetsPath.Length + 1));
            return true;
        }

        private PopupField<string> CreateControlOwnerPopup(
            string label,
            CharacterTestCharacterControlOwner value,
            Action<CharacterTestCharacterControlOwner> setter,
            string name,
            bool markDirty)
        {
            var labels = new List<string>(ControlOwnerLabels);
            int index = IndexOfControlOwner(value);
            if (index < 0)
                index = 0;
            var popup = new PopupField<string>(label, labels, index)
            {
                name = name
            };
            popup.tooltip = "保存值仍为 CharacterTestCharacterControlOwner 枚举；这里只本地化显示文案。";
            popup.RegisterValueChangedCallback(_ =>
            {
                int selected = Mathf.Clamp(labels.IndexOf(popup.value), 0, ControlOwnerValues.Length - 1);
                setter(ControlOwnerValues[selected]);
                if (markDirty)
                    MarkDirty();
            });
            return popup;
        }

        private static int IndexOfControlOwner(CharacterTestCharacterControlOwner owner)
        {
            for (int i = 0; i < ControlOwnerValues.Length; i++)
            {
                if (ControlOwnerValues[i] == owner)
                    return i;
            }

            return -1;
        }

        private static string GetControlOwnerLabel(CharacterTestCharacterControlOwner owner)
        {
            int index = IndexOfControlOwner(owner);
            return index >= 0 && index < ControlOwnerLabels.Length
                ? ControlOwnerLabels[index]
                : owner.ToString();
        }

    }
}
