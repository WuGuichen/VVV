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
        private void BuildActionTab(VisualElement root)
        {
            root.Add(CreateSectionTitle("Utility Actions / 输出映射"));
            TextField filter = CreateDelayedTextField("按 state/action id 筛选", _actionFilter);
            filter.RegisterValueChangedCallback(evt =>
            {
                _actionFilter = evt.newValue ?? string.Empty;
                RefreshView();
            });
            root.Add(filter);

            VisualElement buttons = CreateButtonRow();
            buttons.Add(CreateMiniButton("+ 新增动作", AddActionForSelectedState));
            Button copy = CreateMiniButton("复制", () =>
            {
                CharacterTestRuntimeAiPlannerEditorModel.ActionDraft action =
                    _model.DuplicateAction(_selectedActionIndex);
                if (action != null)
                    _selectedActionIndex = _model.Actions.IndexOf(action);
                MarkDirty("已复制动作");
            });
            Button delete = CreateMiniButton("删除", () =>
            {
                if (_model.DeleteAction(_selectedActionIndex))
                    _selectedActionIndex = Math.Min(_selectedActionIndex, _model.Actions.Count - 1);
                MarkDirty("已删除动作");
            });
            Button moveUp = CreateMiniButton("上移", () =>
            {
                if (_model.MoveAction(_selectedActionIndex, -1))
                    _selectedActionIndex--;
                MarkDirty("已调整动作顺序");
            });
            Button moveDown = CreateMiniButton("下移", () =>
            {
                if (_model.MoveAction(_selectedActionIndex, 1))
                    _selectedActionIndex++;
                MarkDirty("已调整动作顺序");
            });
            bool hasSelectedAction =
                _selectedActionIndex >= 0 && _selectedActionIndex < _model.Actions.Count;
            copy.SetEnabled(hasSelectedAction);
            delete.SetEnabled(hasSelectedAction);
            moveUp.SetEnabled(hasSelectedAction && _selectedActionIndex > 0);
            moveDown.SetEnabled(hasSelectedAction && _selectedActionIndex < _model.Actions.Count - 1);
            buttons.Add(copy);
            buttons.Add(delete);
            buttons.Add(moveUp);
            buttons.Add(moveDown);
            root.Add(buttons);

            if (_model.Actions.Count == 0)
            {
                root.Add(CreateEmptyListAction(
                    "暂无 Utility Action。",
                    "新增动作",
                    AddActionForSelectedState));
                return;
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            CharacterTestRuntimeAiPlannerValidationReport report = _model.Validate();
            for (int i = 0; i < _model.Actions.Count; i++)
            {
                if (PassesActionFilter(_model.Actions[i]))
                    scroll.Add(CreateActionRow(i, report));
            }

            root.Add(scroll);
        }

        private void AddActionForSelectedState()
        {
            CharacterTestRuntimeAiPlannerEditorModel.ActionDraft action =
                _model.AddAction(_selectedStateId);
            _selectedActionIndex = _model.Actions.IndexOf(action);
            MarkDirty("已新增动作");
        }

        private VisualElement CreateActionRow(
            int index,
            CharacterTestRuntimeAiPlannerValidationReport report)
        {
            CharacterTestRuntimeAiPlannerEditorModel.ActionDraft action = _model.Actions[index];
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics =
                FindActionDiagnostics(report, action);
            string name = string.IsNullOrWhiteSpace(action.DisplayName)
                ? action.Kind.ToString()
                : action.DisplayName;
            var button = new Button(() =>
            {
                _activeTab = ActionTabIndex;
                _selectedActionIndex = index;
                _selectedStateId = action.StateId;
                RefreshView();
            })
            {
                text = DiagnosticPrefix(diagnostics) + name + "  [" + action.Kind + "]  state=" + action.StateId
                    + "  id=" + action.ActionId
                    + "  score=" + action.Score
                    + "  cost=" + action.Cost
            };
            ApplyListRowButtonStyle(button);
            if (index == _selectedActionIndex)
                button.style.backgroundColor = new Color(0.22f, 0.36f, 0.54f);
            return button;
        }

        private void BuildActionInspector(VisualElement root)
        {
            if (_selectedActionIndex < 0 || _selectedActionIndex >= _model.Actions.Count)
            {
                root.Add(CreateNotice("请选择一个动作。"));
                return;
            }

            CharacterTestRuntimeAiPlannerEditorModel.ActionDraft action =
                _model.Actions[_selectedActionIndex];
            root.Add(CreateSectionTitle("动作 Inspector"));
            CharacterTestRuntimeAiPlannerValidationReport report = _model.Validate();
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics =
                FindActionDiagnostics(report, action);
            AddDiagnosticsSection(root, diagnostics);
            AddActionQuickFixes(root, action, diagnostics);

            TextField displayName = CreateDelayedTextField("显示名", action.DisplayName);
            displayName.RegisterValueChangedCallback(evt =>
            {
                action.DisplayName = evt.newValue ?? string.Empty;
                MarkDirty("已修改动作显示名");
            });
            root.Add(displayName);

            root.Add(CreateStatePopup("所属状态", action.StateId, false, value =>
            {
                action.StateId = value;
                _selectedStateId = value;
                MarkDirty("已修改动作所属状态");
            }));

            EnumField kind = new EnumField("输出类型", action.Kind);
            kind.RegisterValueChangedCallback(evt =>
            {
                action.Kind = (CharacterTestRuntimeAiPlannerActionKind)(object)evt.newValue;
                MarkDirty("已修改动作输出类型");
            });
            root.Add(kind);
            root.Add(CreateReadOnlyLine("输出说明", DescribeActionKind(action.Kind)));

            FloatField score = new FloatField("Utility 分数") { value = action.Score, isDelayed = true };
            score.RegisterValueChangedCallback(evt =>
            {
                action.Score = evt.newValue;
                MarkDirty("已修改动作分数");
            });
            root.Add(score);

            FloatField cost = new FloatField("Cost") { value = action.Cost, isDelayed = true };
            cost.RegisterValueChangedCallback(evt =>
            {
                action.Cost = evt.newValue;
                MarkDirty("已修改动作 Cost");
            });
            root.Add(cost);

            if (action.Kind == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot)
            {
                IntegerField slot = new IntegerField("技能槽 1-9") { value = action.AbilitySlot, isDelayed = true };
                slot.RegisterValueChangedCallback(evt =>
                {
                    action.AbilitySlot = evt.newValue;
                    MarkDirty("已修改技能槽");
                });
                root.Add(slot);
            }

            AddActionMovementOverride(root, action);

            var advanced = new Foldout { text = "高级", value = false };
            IntegerField actionId = new IntegerField("Action Id") { value = action.ActionId, isDelayed = true };
            actionId.RegisterValueChangedCallback(evt =>
            {
                action.ActionId = evt.newValue;
                MarkDirty("已修改 Action Id");
            });
            advanced.Add(actionId);

            IntegerField abilityId = new IntegerField("Ability Id") { value = action.AbilityId, isDelayed = true };
            abilityId.RegisterValueChangedCallback(evt =>
            {
                action.AbilityId = evt.newValue;
                MarkDirty("已修改 Ability Id");
            });
            advanced.Add(abilityId);
            root.Add(advanced);

            root.Add(CreateSpacer(8));
            root.Add(CreateSectionTitle("动作条件"));
            for (int i = 0; i < action.Conditions.Count; i++)
            {
                int conditionIndex = i;
                var conditionPanel = new VisualElement();
                conditionPanel.style.marginBottom = 6;
                conditionPanel.style.paddingBottom = 6;
                conditionPanel.style.borderBottomWidth = 1;
                conditionPanel.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);
                conditionPanel.Add(new Label("条件 " + (i + 1)));
                conditionPanel.Add(CreateSmartConditionEditor(
                    action.Conditions[i],
                    () => MarkDirty("已修改动作条件")));
                conditionPanel.Add(CreateMiniButton("删除条件", () =>
                {
                    action.Conditions.RemoveAt(conditionIndex);
                    MarkDirty("已删除动作条件");
                }));
                root.Add(conditionPanel);
            }

            root.Add(CreateMiniButton("+ 新增条件", () =>
            {
                action.Conditions.Add(new CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft());
                MarkDirty("已新增动作条件");
            }));
        }

        private void AddActionMovementOverride(
            VisualElement root,
            CharacterTestRuntimeAiPlannerEditorModel.ActionDraft action)
        {
            var movement = new Foldout
            {
                text = "移动风格覆盖",
                value = action.HasMovementOverride
            };

            Toggle enabled = new Toggle("覆盖 Profile 默认移动")
            {
                value = action.HasMovementOverride
            };
            enabled.RegisterValueChangedCallback(evt =>
            {
                action.HasMovementOverride = evt.newValue;
                MarkDirty("已修改动作移动风格覆盖");
                RefreshView();
            });
            movement.Add(enabled);

            if (action.HasMovementOverride)
            {
                AddActionMovementFloatField(
                    movement,
                    "速度倍率",
                    action.MovementSpeedScale,
                    value => action.MovementSpeedScale = value);
                AddActionMovementFloatField(
                    movement,
                    "转向响应",
                    action.MovementSteeringResponsiveness,
                    value => action.MovementSteeringResponsiveness = value);
                AddActionMovementFloatField(
                    movement,
                    "接近侧向混合",
                    action.MovementApproachLateralBlend,
                    value => action.MovementApproachLateralBlend = value);
                AddActionMovementFloatField(
                    movement,
                    "偏好距离最小",
                    action.MovementPreferredDistanceMin,
                    value => action.MovementPreferredDistanceMin = value);
                AddActionMovementFloatField(
                    movement,
                    "偏好距离最大",
                    action.MovementPreferredDistanceMax,
                    value => action.MovementPreferredDistanceMax = value);
                AddActionMovementFloatField(
                    movement,
                    "到达减速距离",
                    action.MovementArrivalSlowdownDistance,
                    value => action.MovementArrivalSlowdownDistance = value);
                AddActionMovementIntegerField(
                    movement,
                    "侧移换边最小帧",
                    action.MovementStrafeSwitchMinFrames,
                    value => action.MovementStrafeSwitchMinFrames = value);
                AddActionMovementIntegerField(
                    movement,
                    "侧移换边最大帧",
                    action.MovementStrafeSwitchMaxFrames,
                    value => action.MovementStrafeSwitchMaxFrames = value);
                AddActionMovementIntegerField(
                    movement,
                    "方向保持帧",
                    action.MovementDirectionHoldFrames,
                    value => action.MovementDirectionHoldFrames = value);
                AddActionMovementIntegerField(
                    movement,
                    "反应延迟帧",
                    action.MovementReactionDelayFrames,
                    value => action.MovementReactionDelayFrames = value);
            }
            else
            {
                movement.Add(CreateReadOnlyLine("当前来源", "继承 Profile 默认移动风格"));
            }

            root.Add(movement);
        }

        private void AddActionMovementFloatField(
            VisualElement root,
            string label,
            float value,
            Action<float> setter)
        {
            FloatField field = new FloatField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkDirty("已修改动作移动风格");
            });
            root.Add(field);
        }

        private void AddActionMovementIntegerField(
            VisualElement root,
            string label,
            int value,
            Action<int> setter)
        {
            IntegerField field = new IntegerField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkDirty("已修改动作移动风格");
            });
            root.Add(field);
        }

    }
}
