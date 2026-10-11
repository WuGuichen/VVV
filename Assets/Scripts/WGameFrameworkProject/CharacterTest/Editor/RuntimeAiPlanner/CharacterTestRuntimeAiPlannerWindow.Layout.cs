using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Diagnostics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement { name = "runtime-ai-planner-toolbar" };
            toolbar.style.flexDirection = FlexDirection.Column;
            toolbar.style.flexShrink = 0;
            toolbar.style.marginBottom = 6;

            var profileGroup = new VisualElement { name = "runtime-ai-planner-toolbar-profile-row" };
            profileGroup.style.flexDirection = FlexDirection.Row;
            profileGroup.style.flexGrow = 1;
            profileGroup.style.flexShrink = 1;
            profileGroup.style.minWidth = 0;
            profileGroup.style.alignItems = Align.Center;

            _profilePicker = new CharacterTestRuntimeAiPlannerProfilePicker(
                "行为 Profile",
                _profileProjectPath,
                includeInherit: false,
                inheritLabel: string.Empty,
                onSelected: entry =>
                {
                    SetProfilePath(entry.ProjectAssetPath);
                    TryAutoLoadSelectedProfile();
                    RefreshView();
                });
            profileGroup.Add(_profilePicker);
            toolbar.Add(profileGroup);

            var advanced = new Foldout
            {
                name = "runtime-ai-planner-toolbar-advanced-json",
                text = "高级 JSON 路径",
                value = false
            };
            advanced.style.marginTop = 3;
            advanced.style.marginBottom = 3;
            var advancedRow = new VisualElement();
            advancedRow.style.flexDirection = FlexDirection.Row;
            advancedRow.style.alignItems = Align.Center;
            advancedRow.style.minWidth = 0;
            _profilePathField = new TextField("JSON 路径")
            {
                value = _profileProjectPath,
                isReadOnly = true
            };
            _profilePathField.style.minWidth = 0;
            _profilePathField.style.flexGrow = 1;
            _profilePathField.style.flexShrink = 1;
            advancedRow.Add(_profilePathField);
            advancedRow.Add(CreateToolbarButton("选择 JSON", ChooseProfileJson));
            advanced.Add(advancedRow);
            toolbar.Add(advanced);

            var commandGroup = new VisualElement { name = "runtime-ai-planner-toolbar-command-row" };
            commandGroup.style.flexDirection = FlexDirection.Row;
            commandGroup.style.flexWrap = Wrap.Wrap;
            commandGroup.style.justifyContent = Justify.FlexEnd;
            commandGroup.style.flexShrink = 0;
            commandGroup.style.marginTop = 3;
            commandGroup.Add(CreateToolbarButton("打开", OpenSelectedProfile));
            commandGroup.Add(CreateTemplateToolbarMenu());
            commandGroup.Add(CreateToolbarButton("复制为", CopySelectedProfileAsNew));
            commandGroup.Add(CreateToolbarButton("加载场景默认", LoadSceneProfile));
            commandGroup.Add(CreateToolbarButton("设为场景默认", BindToSceneProfile));
            commandGroup.Add(CreateToolbarButton("保存", SaveProfile));
            commandGroup.Add(CreateToolbarButton("校验", ValidateProfile));
            commandGroup.Add(CreateToolbarButton("复制报告", CopyReport));
            toolbar.Add(commandGroup);
            return toolbar;
        }

        private VisualElement CreateTabBar()
        {
            var row = new Toolbar();
            row.style.marginBottom = 8;
            _tabToggles = new ToolbarToggle[_tabNames.Length];
            for (int i = 0; i < _tabNames.Length; i++)
            {
                int tabIndex = i;
                var toggle = new ToolbarToggle { text = _tabNames[i], value = i == _activeTab };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (!evt.newValue)
                        return;

                    _activeTab = tabIndex;
                    _selectedTransitionIndex = -1;
                    SyncTabToggles();
                    RefreshView();
                });
                _tabToggles[i] = toggle;
                row.Add(toggle);
            }

            return row;
        }

        private VisualElement CreateMainArea()
        {
            var area = new VisualElement();
            area.style.flexDirection = FlexDirection.Row;
            area.style.flexGrow = 1;
            area.style.minHeight = 0;

            _leftPanel = CreatePanel(250);
            _centerPanel = CreatePanel(0);
            _centerPanel.style.flexGrow = 1;
            _centerPanel.style.flexShrink = 1;
            _centerPanel.style.minWidth = 0;
            _centerPanel.style.overflow = Overflow.Hidden;
            _rightPanel = CreatePanel(320);

            area.Add(_leftPanel);
            area.Add(_centerPanel);
            area.Add(_rightPanel);
            return area;
        }

        private static VisualElement CreatePanel(float width)
        {
            var panel = new VisualElement();
            panel.style.paddingLeft = 8;
            panel.style.paddingRight = 8;
            panel.style.paddingTop = 8;
            panel.style.paddingBottom = 8;
            panel.style.marginRight = 6;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderRightColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f);
            panel.style.minHeight = 0;
            if (width > 0f)
            {
                panel.style.width = width;
                panel.style.minWidth = width;
            }

            return panel;
        }

        private static ToolbarButton CreateToolbarButton(string text, Action action)
        {
            var button = new ToolbarButton(action) { text = text };
            button.style.marginLeft = 4;
            button.style.marginBottom = 2;
            return button;
        }

        private ToolbarMenu CreateTemplateToolbarMenu()
        {
            var menu = new ToolbarMenu
            {
                name = TemplateMenuName,
                text = "模板"
            };
            menu.style.marginLeft = 4;
            menu.style.marginBottom = 2;

            IReadOnlyList<CharacterTestRuntimeAiPlannerTemplateDescriptor> templates =
                CharacterTestRuntimeAiPlannerTemplateLibrary.Templates;
            for (int i = 0; i < templates.Count; i++)
            {
                CharacterTestRuntimeAiPlannerTemplateDescriptor template = templates[i];
                menu.menu.AppendAction(
                    template.DisplayName,
                    _ => CreateProfileTemplate(template.TemplateId));
            }

            return menu;
        }

        private void RefreshView()
        {
            if (_leftPanel == null || _centerPanel == null || _rightPanel == null)
                return;

            SyncTabToggles();
            if (_profilePathField != null)
                _profilePathField.SetValueWithoutNotify(_profileProjectPath);
            _profilePicker?.SetSelectedProjectPath(_profileProjectPath);

            _leftPanel.Clear();
            _centerPanel.Clear();
            _rightPanel.Clear();
            ApplyMainPanelLayout();

            if (_activeTab == PlayMonitorTabIndex)
            {
                CapturePlayMonitorSnapshots();
                BuildPlayMonitorLeftPanel(_leftPanel);
                BuildPlayMonitor(_centerPanel);
                BuildPlayMonitorInspector(_rightPanel);
                UpdateStatusLabel();
                return;
            }

            ClearPlayMonitorSnapshots();
            if (_activeTab == BehaviorCardEditorTabIndex)
            {
                BuildBehaviorCardSidePanel(_leftPanel);
                BuildBehaviorCardEditorTab(_centerPanel);
                UpdateStatusLabel();
                return;
            }

            if (_activeTab == BehaviorGroupPreviewTabIndex)
                BuildBehaviorGroupPreviewLeftPanel(_leftPanel);
            else if (_activeTab == BehaviorCompositionTabIndex)
                BuildBehaviorCompositionLeftPanel(_leftPanel);
            else
                BuildLeftPanel(_leftPanel);
            if (!_profileLoaded)
            {
                BuildEmptyProfileView(_centerPanel);
                BuildEmptyProfileInspector(_rightPanel);
                UpdateStatusLabel();
                return;
            }

            EnsureSelection();
            switch (_activeTab)
            {
                case StateTabIndex:
                    BuildStateTab(_centerPanel);
                    BuildStateInspector(_rightPanel);
                    break;
                case ActionTabIndex:
                    BuildActionTab(_centerPanel);
                    BuildActionInspector(_rightPanel);
                    break;
                case PerceptionTabIndex:
                    BuildPerceptionTab(_centerPanel);
                    BuildPerceptionInspector(_rightPanel);
                    break;
                case BehaviorGroupPreviewTabIndex:
                    BuildBehaviorGroupPreviewTab(_centerPanel);
                    BuildBehaviorGroupPreviewInspector(_rightPanel);
                    break;
                case BehaviorCompositionTabIndex:
                    BuildBehaviorCompositionTab(_centerPanel);
                    BuildBehaviorCompositionInspector(_rightPanel);
                    break;
                case BehaviorCardEditorTabIndex:
                    BuildBehaviorCardEditorTab(_centerPanel);
                    BuildBehaviorCardStepInspector(_rightPanel);
                    break;
                case ValidationTabIndex:
                    BuildValidationTab(_centerPanel);
                    BuildValidationInspector(_rightPanel);
                    break;
            }

            UpdateStatusLabel();
        }

        private void ApplyMainPanelLayout()
        {
            if (_activeTab == PlayMonitorTabIndex)
            {
                SetPanelWidth(_leftPanel, 180f, 160f);
                SetPanelWidth(_rightPanel, 460f, 380f);
                return;
            }

            if (_activeTab == BehaviorGroupPreviewTabIndex)
            {
                SetPanelWidth(_leftPanel, 230f, 210f);
                SetPanelWidth(_rightPanel, 360f, 320f);
                return;
            }

            if (_activeTab == BehaviorCompositionTabIndex)
            {
                SetPanelWidth(_leftPanel, 200f, 180f);
                SetPanelWidth(_rightPanel, 340f, 300f);
                return;
            }

            if (_activeTab == BehaviorCardEditorTabIndex)
            {
                SetPanelWidth(_leftPanel, 320f, 280f);
                CollapsePanel(_rightPanel);
                return;
            }

            SetPanelWidth(_leftPanel, 250f, 250f);
            SetPanelWidth(_rightPanel, 320f, 320f);
        }

        private static void SetPanelWidth(VisualElement panel, float width, float minWidth)
        {
            if (panel == null)
                return;

            panel.style.display = DisplayStyle.Flex;
            panel.style.width = width;
            panel.style.minWidth = minWidth;
        }

        private static void CollapsePanel(VisualElement panel)
        {
            if (panel == null)
                return;

            panel.style.display = DisplayStyle.None;
            panel.style.width = 0f;
            panel.style.minWidth = 0f;
        }

        private void SyncTabToggles()
        {
            if (_tabToggles == null)
                return;

            for (int i = 0; i < _tabToggles.Length; i++)
                _tabToggles[i].SetValueWithoutNotify(i == _activeTab);
        }

        private void SetProfilePath(string projectPath)
        {
            _profileProjectPath = NormalizeProjectAssetPath(projectPath);
            _profileLoaded = false;
            _dirty = false;
            _selectedTransitionIndex = -1;
            _selectedActionIndex = -1;
            _selectedAgentIndex = -1;
            _profileLoadFailure = string.Empty;
            _behaviorCompositionProfile = null;
            _lastBehaviorCompositionReport = string.Empty;
            _statusText = "已选择路径，尚未加载";
        }

        private bool TryAutoLoadSelectedProfile()
        {
            if (_profileLoaded)
                return true;

            string fullPath = ResolveProjectFullPath(_profileProjectPath);
            if (!File.Exists(fullPath))
            {
                _profileLoadFailure = "Profile JSON 文件不存在: " + _profileProjectPath;
                _lastReport = _profileLoadFailure;
                _statusText = "未加载";
                return false;
            }

            return LoadProfileFromProjectPath(_profileProjectPath, showNotification: false);
        }

        private bool LoadProfileFromProjectPath(string projectPath)
        {
            return LoadProfileFromProjectPath(projectPath, showNotification: true);
        }

        private bool LoadProfileFromProjectPath(string projectPath, bool showNotification)
        {
            string normalizedPath = NormalizeProjectAssetPath(projectPath);
            string fullPath = ResolveProjectFullPath(normalizedPath);
            if (!File.Exists(fullPath))
            {
                _profileProjectPath = normalizedPath;
                _profileLoaded = false;
                _statusText = "Profile JSON 文件不存在: " + normalizedPath;
                _lastReport = _statusText;
                _profileLoadFailure = _statusText;
                if (showNotification)
                    ShowNotification(new GUIContent("Profile JSON 文件不存在"));
                RefreshView();
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(fullPath);
            }
            catch (IOException exception)
            {
                _profileProjectPath = normalizedPath;
                _profileLoaded = false;
                _statusText = "Profile JSON 读取失败";
                _lastReport = "Profile JSON read failed: " + exception.Message;
                _profileLoadFailure = _lastReport;
                if (showNotification)
                    ShowNotification(new GUIContent("Profile JSON 读取失败"));
                RefreshView();
                return false;
            }

            if (!CharacterTestRuntimeAiPlannerProfileJson.TryLoad(
                    json,
                    out CharacterTestRuntimeAiPlannerProfile profile,
                    out CharacterTestRuntimeAiPlannerValidationReport report))
            {
                _profileProjectPath = normalizedPath;
                _profileLoaded = false;
                _lastReport = report.ToText();
                _statusText = "Profile JSON 校验失败";
                _profileLoadFailure = _lastReport;
                if (showNotification)
                    ShowNotification(new GUIContent("Profile JSON 校验失败"));
                RefreshView();
                return false;
            }

            _profileProjectPath = normalizedPath;
            _model.Load(profile);
            _behaviorCompositionProfile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile(profile);
            _lastBehaviorCompositionReport = string.Empty;
            _selectedStateId = string.IsNullOrWhiteSpace(_model.InitialStateId)
                ? _model.GetFirstStateId()
                : _model.InitialStateId;
            EnsureSelection();
            _selectedTransitionIndex = -1;
            _selectedActionIndex = -1;
            _selectedAgentIndex = -1;
            _profileLoaded = true;
            _dirty = false;
            _lastReport = report.ToText();
            _profileLoadFailure = string.Empty;
            _statusText = "已加载";
            RefreshView();
            return true;
        }

        private void MarkDirty(string status)
        {
            _dirty = true;
            _statusText = string.IsNullOrWhiteSpace(status) ? "有未保存修改" : status;
            RefreshView();
        }

        private void EnsureSelection()
        {
            if (_model.States.Count == 0)
            {
                _selectedStateId = string.Empty;
                _selectedTransitionIndex = -1;
            }
            else if (!_model.HasState(_selectedStateId))
            {
                _selectedStateId = _model.GetFirstStateId();
            }

            if (_selectedTransitionIndex >= _model.Transitions.Count)
                _selectedTransitionIndex = -1;
            if (_selectedActionIndex >= _model.Actions.Count)
                _selectedActionIndex = _model.Actions.Count - 1;
            if (_selectedActionIndex < 0 && _model.Actions.Count > 0)
                _selectedActionIndex = 0;
        }

        private void BuildEmptyProfileView(VisualElement root)
        {
            root.Add(CreateSectionTitle("Profile JSON 未加载"));
            string message = string.IsNullOrWhiteSpace(_profileLoadFailure)
                ? "请选择或创建 Runtime AI Planner Profile JSON。"
                : _profileLoadFailure;
            root.Add(CreateNotice(message));

            var actions = CreateButtonRow();
            actions.Add(CreatePrimaryButton("创建行为模板", ShowTemplateMenu));
            actions.Add(CreateMiniButton("选择 JSON", ChooseProfileJson));
            actions.Add(CreateMiniButton("加载当前场景 Profile", LoadSceneProfile));
            root.Add(actions);

            root.Add(CreateSpacer(8));
            root.Add(CreateReadOnlyLine("当前路径", _profileProjectPath));
            root.Add(CreateReadOnlyLine("编辑方式", "Edit Mode 保存 JSON，重建 CharacterTest 会话后生效"));
        }

        private void BuildEmptyProfileInspector(VisualElement root)
        {
            root.Add(CreateSectionTitle("Profile Inspector"));
            root.Add(CreateReadOnlyLine("状态", string.IsNullOrWhiteSpace(_statusText) ? "未加载" : _statusText));
            root.Add(CreateReadOnlyLine("路径", _profileProjectPath));
            string fullPath = ResolveProjectFullPath(_profileProjectPath);
            root.Add(CreateReadOnlyLine("本地文件", fullPath));

            if (!string.IsNullOrWhiteSpace(_lastReport))
            {
                root.Add(CreateSpacer(8));
                root.Add(CreateSectionTitle("诊断"));
                root.Add(CreateCompactReportField(_lastReport));
            }

            root.Add(CreateSpacer(8));
            root.Add(CreateNotice("创建默认模板会写入默认 StreamingAssets JSON；保存后不会热改 Play Mode runtime。"));
        }

        private void BuildLeftPanel(VisualElement root)
        {
            root.Add(CreateSectionTitle("Profile 摘要"));
            if (!_profileLoaded)
            {
                root.Add(CreateWrappedLabel("未加载 Profile JSON"));
                root.Add(CreateWrappedLabel(_profileProjectPath));
                if (!string.IsNullOrWhiteSpace(_profileLoadFailure))
                    root.Add(CreateWrappedLabel(_profileLoadFailure));
                return;
            }

            root.Add(CreateWrappedLabel(_model.ProfileId));
            root.Add(CreateWrappedLabel(_profileProjectPath));
            root.Add(new Label("状态 " + _model.States.Count
                + " / 切换 " + _model.Transitions.Count
                + " / 动作 " + _model.Actions.Count));
            root.Add(new Label(_dirty ? "有未保存修改" : "已同步到 JSON"));

            root.Add(CreateSpacer(8));
            root.Add(CreateSectionTitle("状态树"));
            VisualElement buttons = CreateButtonRow();
            Button addRoot = CreateMiniButton("+ 根状态", () =>
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft state = _model.AddRootState();
                _selectedStateId = state.StateId;
                _selectedTransitionIndex = -1;
                _activeTab = StateTabIndex;
                MarkDirty("已新增 root 状态");
            });
            Button addChild = CreateMiniButton("+ 子状态", () =>
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft state =
                    _model.AddChildState(_selectedStateId);
                _selectedStateId = state.StateId;
                _selectedTransitionIndex = -1;
                _activeTab = StateTabIndex;
                MarkDirty("已新增子状态");
            });
            Button copy = CreateMiniButton("复制", () =>
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft state =
                    _model.DuplicateState(_selectedStateId);
                if (state != null)
                    _selectedStateId = state.StateId;
                _selectedTransitionIndex = -1;
                _activeTab = StateTabIndex;
                MarkDirty("已复制状态");
            });
            Button delete = CreateMiniButton("删除", () =>
            {
                string deleted = _selectedStateId;
                if (_model.DeleteState(deleted))
                    _selectedStateId = _model.GetFirstStateId();
                _selectedTransitionIndex = -1;
                _activeTab = StateTabIndex;
                MarkDirty("已删除状态及相关切换，保存前请校验引用");
            });
            addChild.SetEnabled(_model.HasState(_selectedStateId));
            copy.SetEnabled(_model.HasState(_selectedStateId));
            delete.SetEnabled(_model.HasState(_selectedStateId));
            buttons.Add(addRoot);
            buttons.Add(addChild);
            buttons.Add(copy);
            buttons.Add(delete);
            root.Add(buttons);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            CharacterTestRuntimeAiPlannerValidationReport report =
                _profileLoaded ? _model.Validate() : null;
            for (int i = 0; i < _model.States.Count; i++)
                scroll.Add(CreateStateRow(_model.States[i], report));

            root.Add(scroll);
        }

        private VisualElement CreateStateRow(
            CharacterTestRuntimeAiPlannerEditorModel.StateDraft state,
            CharacterTestRuntimeAiPlannerValidationReport report)
        {
            int depth = _model.GetStateDepth(state.StateId);
            bool selected = string.Equals(state.StateId, _selectedStateId, StringComparison.Ordinal);
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics =
                FindStateDiagnostics(report, state);
            string name = string.IsNullOrWhiteSpace(state.DisplayName) ? state.StateId : state.DisplayName;
            var button = new Button(() =>
            {
                _activeTab = StateTabIndex;
                _selectedStateId = state.StateId;
                _selectedTransitionIndex = -1;
                RefreshView();
            })
            {
                text = DiagnosticPrefix(diagnostics) + name + " (" + state.StateId + ")"
            };
            button.style.marginLeft = depth * 14;
            ApplyListRowButtonStyle(button, fillWidth: false);
            button.style.marginBottom = 2;
            if (selected)
                button.style.backgroundColor = new Color(0.22f, 0.36f, 0.54f);
            return button;
        }

    }
}
