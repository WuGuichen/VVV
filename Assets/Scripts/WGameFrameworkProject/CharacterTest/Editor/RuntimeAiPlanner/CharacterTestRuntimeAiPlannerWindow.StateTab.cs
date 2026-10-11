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
        private void BuildStateTab(VisualElement root)
        {
            root.Add(CreateStateTabHeader());
            root.Add(CreateNotice("空来源状态表示全局切换。删除状态时会同步删除指向该状态的切换。"));
            CharacterTestRuntimeAiPlannerValidationReport report = _model.Validate();
            root.Add(new CharacterTestRuntimeAiPlannerFlowPreview(
                _model,
                report,
                _selectedStateId,
                _selectedTransitionIndex,
                SelectFlowState,
                SelectFlowTransition,
                CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview,
                GetFlowPreviewViewportWidth(),
                _flowPreviewZoom,
                SetFlowPreviewZoom,
                FitFlowPreviewZoom));

            VisualElement buttons = CreateButtonRow();
            buttons.Add(CreateMiniButton("+ 新增切换", AddTransitionForSelectedState));
            Button copy = CreateMiniButton("复制", () =>
            {
                CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft duplicated =
                    _model.DuplicateTransition(_selectedTransitionIndex);
                if (duplicated != null)
                    _selectedTransitionIndex = _model.Transitions.IndexOf(duplicated);
                MarkDirty("已复制状态切换");
            });
            Button delete = CreateMiniButton("删除", () =>
            {
                if (_model.DeleteTransition(_selectedTransitionIndex))
                    _selectedTransitionIndex = -1;
                MarkDirty("已删除状态切换");
            });
            Button moveUp = CreateMiniButton("上移", () =>
            {
                if (_model.MoveTransition(_selectedTransitionIndex, -1))
                    _selectedTransitionIndex--;
                MarkDirty("已调整状态切换顺序");
            });
            Button moveDown = CreateMiniButton("下移", () =>
            {
                if (_model.MoveTransition(_selectedTransitionIndex, 1))
                    _selectedTransitionIndex++;
                MarkDirty("已调整状态切换顺序");
            });
            bool hasSelectedTransition =
                _selectedTransitionIndex >= 0 && _selectedTransitionIndex < _model.Transitions.Count;
            copy.SetEnabled(hasSelectedTransition);
            delete.SetEnabled(hasSelectedTransition);
            moveUp.SetEnabled(hasSelectedTransition && _selectedTransitionIndex > 0);
            moveDown.SetEnabled(hasSelectedTransition && _selectedTransitionIndex < _model.Transitions.Count - 1);
            buttons.Add(copy);
            buttons.Add(delete);
            buttons.Add(moveUp);
            buttons.Add(moveDown);
            root.Add(buttons);

            if (_model.Transitions.Count == 0)
            {
                root.Add(CreateEmptyListAction(
                    "暂无状态切换。",
                    "新增切换",
                    AddTransitionForSelectedState));
                return;
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            for (int i = 0; i < _model.Transitions.Count; i++)
                scroll.Add(CreateTransitionRow(i, report));
            root.Add(scroll);
        }

        private VisualElement CreateStateTabHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;

            Label title = CreateSectionTitle("状态切换");
            title.style.flexGrow = 1;
            title.style.marginBottom = 0;
            header.Add(title);

            Button expand = CreatePrimaryButton("放大预览", OpenFlowPreviewWindow);
            expand.name = CharacterTestRuntimeAiPlannerFlowPreview.ExpandButtonName;
            expand.tooltip = "打开独立只读窗口查看完整行为流程。";
            expand.style.flexShrink = 0;
            header.Add(expand);
            return header;
        }

        private void SelectFlowState(string stateId)
        {
            if (!_model.HasState(stateId))
                return;

            _activeTab = StateTabIndex;
            _selectedStateId = stateId;
            _selectedTransitionIndex = -1;
            RefreshView();
        }

        private void SelectFlowTransition(int transitionIndex)
        {
            if (transitionIndex < 0 || transitionIndex >= _model.Transitions.Count)
                return;

            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition =
                _model.Transitions[transitionIndex];
            _activeTab = StateTabIndex;
            _selectedTransitionIndex = transitionIndex;
            if (!string.IsNullOrWhiteSpace(transition.SourceStateId)
                && _model.HasState(transition.SourceStateId))
            {
                _selectedStateId = transition.SourceStateId;
            }
            else if (_model.HasState(transition.TargetStateId))
            {
                _selectedStateId = transition.TargetStateId;
            }

            RefreshView();
        }

        private void SetFlowPreviewZoom(float zoom)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom))
                zoom = 1f;

            _flowPreviewZoom = Mathf.Clamp(
                zoom,
                CharacterTestRuntimeAiPlannerFlowPreview.MinDetailZoom,
                CharacterTestRuntimeAiPlannerFlowPreview.MaxZoom);
            RefreshView();
        }

        private void FitFlowPreviewZoom()
        {
            _flowPreviewZoom = CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom;
            RefreshView();
        }

        private float GetFlowPreviewViewportWidth()
        {
            if (_centerPanel != null && _centerPanel.resolvedStyle.width > 240f)
                return _centerPanel.resolvedStyle.width - 18f;

            return 640f;
        }

        private void OpenFlowPreviewWindow()
        {
            CharacterTestRuntimeAiPlannerFlowPreviewWindow.Open(
                _model,
                _selectedStateId,
                _selectedTransitionIndex,
                SelectFlowState,
                SelectFlowTransition);
        }

        private void AddTransitionForSelectedState()
        {
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition =
                _model.AddTransition(_selectedStateId);
            _selectedTransitionIndex = _model.Transitions.IndexOf(transition);
            MarkDirty("已新增状态切换");
        }

        private VisualElement CreateTransitionRow(
            int index,
            CharacterTestRuntimeAiPlannerValidationReport report)
        {
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition = _model.Transitions[index];
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics =
                FindTransitionDiagnostics(report, _model.Transitions, transition);
            string source = string.IsNullOrWhiteSpace(transition.SourceStateId)
                ? "全局"
                : transition.SourceStateId;
            string text = source + " -> " + transition.TargetStateId
                + "  p=" + transition.Priority
                + "  " + CharacterTestRuntimeAiPlannerEditorFactCatalog.FormatCondition(transition.Condition);
            if (!string.IsNullOrWhiteSpace(transition.DisplayName))
                text = transition.DisplayName + "  |  " + text;

            var button = new Button(() =>
            {
                _selectedTransitionIndex = index;
                _selectedStateId = string.IsNullOrWhiteSpace(transition.SourceStateId)
                    ? transition.TargetStateId
                    : transition.SourceStateId;
                RefreshView();
            })
            {
                text = DiagnosticPrefix(diagnostics) + text
            };
            ApplyListRowButtonStyle(button);
            if (index == _selectedTransitionIndex)
                button.style.backgroundColor = new Color(0.22f, 0.36f, 0.54f);
            return button;
        }

        private void BuildStateInspector(VisualElement root)
        {
            if (_selectedTransitionIndex >= 0 && _selectedTransitionIndex < _model.Transitions.Count)
            {
                BuildTransitionInspector(root, _model.Transitions[_selectedTransitionIndex]);
                return;
            }

            CharacterTestRuntimeAiPlannerEditorModel.StateDraft state =
                _model.FindState(_selectedStateId);
            if (state == null)
            {
                root.Add(CreateNotice("请选择一个状态。"));
                return;
            }

            root.Add(CreateSectionTitle("状态 Inspector"));
            CharacterTestRuntimeAiPlannerValidationReport report = _model.Validate();
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics =
                FindStateDiagnostics(report, state);
            AddDiagnosticsSection(root, diagnostics);
            AddStateQuickFixes(root, state, diagnostics);

            TextField displayName = CreateDelayedTextField("显示名", state.DisplayName);
            displayName.RegisterValueChangedCallback(evt =>
            {
                state.DisplayName = evt.newValue ?? string.Empty;
                MarkDirty("已修改状态显示名");
            });
            root.Add(displayName);

            root.Add(CreateStatePopup("父状态", state.ParentStateId, true, value =>
            {
                if (string.Equals(value, state.StateId, StringComparison.Ordinal))
                {
                    ShowNotification(new GUIContent("父状态不能指向自身"));
                    return;
                }

                state.ParentStateId = value;
                MarkDirty("已修改父状态");
            }));

            Button initialButton = new Button(() =>
            {
                _model.InitialStateId = state.StateId;
                MarkDirty("已设置初始状态");
            })
            {
                text = string.Equals(_model.InitialStateId, state.StateId, StringComparison.Ordinal)
                    ? "当前为初始状态"
                    : "设为初始状态"
            };
            root.Add(initialButton);

            var advanced = new Foldout { text = "高级", value = false };
            TextField idField = CreateDelayedTextField("状态 Id", state.StateId);
            idField.RegisterValueChangedCallback(evt =>
            {
                string oldId = state.StateId;
                if (_model.RenameState(oldId, evt.newValue))
                {
                    _selectedStateId = state.StateId;
                    MarkDirty("已重命名状态并同步引用");
                }
                else
                {
                    ShowNotification(new GUIContent("状态 Id 无效或重复"));
                    RefreshView();
                }
            });
            advanced.Add(idField);
            root.Add(advanced);
        }

        private void BuildTransitionInspector(
            VisualElement root,
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition)
        {
            root.Add(CreateSectionTitle("切换 Inspector"));
            CharacterTestRuntimeAiPlannerValidationReport report = _model.Validate();
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics =
                FindTransitionDiagnostics(report, _model.Transitions, transition);
            AddDiagnosticsSection(root, diagnostics);
            AddTransitionQuickFixes(root, transition, diagnostics);

            TextField displayName = CreateDelayedTextField("显示名", transition.DisplayName);
            displayName.RegisterValueChangedCallback(evt =>
            {
                transition.DisplayName = evt.newValue ?? string.Empty;
                MarkDirty("已修改切换显示名");
            });
            root.Add(displayName);

            root.Add(CreateStatePopup("来源状态", transition.SourceStateId, true, value =>
            {
                transition.SourceStateId = value;
                MarkDirty("已修改切换来源");
            }));
            root.Add(CreateStatePopup("目标状态", transition.TargetStateId, false, value =>
            {
                transition.TargetStateId = value;
                MarkDirty("已修改切换目标");
            }));

            IntegerField priority = new IntegerField("优先级") { value = transition.Priority, isDelayed = true };
            priority.RegisterValueChangedCallback(evt =>
            {
                transition.Priority = evt.newValue;
                MarkDirty("已修改切换优先级");
            });
            root.Add(priority);
            root.Add(CreateSmartConditionEditor(transition.Condition, () => MarkDirty("已修改切换条件")));
        }

    }
}
