using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private void ShowConditionSubjectPicker(Fgui.GObject anchor)
        {
            if (_model?.CanEdit != true || anchor == null)
                return;
            if (TryShowFormalConditionPicker(anchor))
                return;
            var subjects = new System.Collections.Generic.List<
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind>
            {
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self
            };
            var labels = new System.Collections.Generic.List<string>
            {
                "目标", "自身"
            };
            if (_model.AllowsEventSignal)
                labels.Add("其他事件结果");
            ShowConditionDropdown(anchor, labels.ToArray(), -1, index =>
            {
                if (index >= subjects.Count)
                {
                    Submit(
                        CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddEventSignal,
                        stringValue: _selectedConditionNodeId);
                    return;
                }
                ShowConditionPropertyPicker(anchor, subjects[index]);
            });
        }

        private void ShowConditionPropertyPicker(
            Fgui.GObject anchor,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject)
        {
            var options = new System.Collections.Generic.List<
                CharacterTestRuntimeAiLabBehaviorConditionDescriptor>();
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorConditionDescriptor> all =
                CharacterTestRuntimeAiLabBehaviorConditionCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor = all[i];
                if (descriptor.Subject == subject)
                    options.Add(descriptor);
            }
            var labels = new string[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                labels[i] = CharacterTestRuntimeAiLabBehaviorConditionCatalog.CategoryText(options[i].Category)
                    + " / " + options[i].DisplayName;
            }
            ShowConditionDropdown(anchor, labels, -1, index =>
                BeginAddSubjectCondition(anchor, options[index]));
        }

        private void BeginAddSubjectCondition(
            Fgui.GObject anchor,
            CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor,
            int replaceLeafIndex = -1)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField mutationField =
                replaceLeafIndex >= 0
                    ? CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ReplaceSubjectCondition
                    : CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddSubjectCondition;
            if (descriptor.ValueKind == CharacterTestRuntimeAiLabBehaviorConditionValueKind.CharacterAction)
            {
                ShowCharacterActionPicker(anchor, default, actionId => Submit(
                    mutationField,
                    conditionIndex: replaceLeafIndex,
                    intValue: (int)descriptor.Fact,
                    stringValue: _selectedConditionNodeId,
                    parameterStringValue: actionId.Value.ToString(CultureInfo.InvariantCulture)));
                return;
            }
            if (descriptor.ValueKind == CharacterTestRuntimeAiLabBehaviorConditionValueKind.Ability)
            {
                ShowAbilityPicker(anchor, 0, abilityId => Submit(
                    mutationField,
                    conditionIndex: replaceLeafIndex,
                    intValue: (int)descriptor.Fact,
                    stringValue: _selectedConditionNodeId,
                    parameterIntValue: abilityId));
                return;
            }
            Submit(
                mutationField,
                conditionIndex: replaceLeafIndex,
                intValue: (int)descriptor.Fact,
                stringValue: _selectedConditionNodeId);
        }

        private void CycleConditionFact(int index)
        {
            if (_model?.CanEdit != true || index < 0 || index >= _model.Conditions.Length)
                return;
            int count = Enum.GetValues(typeof(CharacterTestRuntimeAiLabCardConditionFact)).Length;
            int next = ((int)_model.Conditions[index].Fact + 1) % count;
            Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionFact,
                index, intValue: next);
        }

        private void CycleConditionOperator(int index)
        {
            if (_model?.CanEdit != true || index < 0 || index >= _model.Conditions.Length)
                return;
            BehaviorRecipeTriggerConditionViewModel row = _model.Conditions[index];
            int next = row.ValueKind switch
            {
                BehaviorRecipeTriggerConditionValueKind.Boolean => row.EffectiveOperator == 0 ? 1 : 0,
                BehaviorRecipeTriggerConditionValueKind.Range => row.EffectiveOperator == 6 ? 7 : 6,
                _ => row.EffectiveOperator switch { 2 => 3, 3 => 4, 4 => 5, _ => 2 }
            };
            Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionOperator,
                index, intValue: next);
        }

        private void ShowFactDropdown(int conditionIndex, Fgui.GObject anchor)
        {
            if (!TryGetCondition(conditionIndex, out BehaviorRecipeTriggerConditionViewModel row))
                return;
            if (!CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                    row.Fact,
                    out CharacterTestRuntimeAiLabBehaviorConditionDescriptor current))
                return;
            var values = new List<CharacterTestRuntimeAiLabCardConditionFact>();
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorConditionDescriptor> descriptors =
                CharacterTestRuntimeAiLabBehaviorConditionCatalog.All;
            for (int i = 0; i < descriptors.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor = descriptors[i];
                if (descriptor.Subject != current.Subject
                    || descriptor.IsSubjectAvailability != current.IsSubjectAvailability)
                    continue;
                values.Add(descriptor.Fact);
            }
            var labels = new string[values.Count];
            int selected = 0;
            for (int i = 0; i < values.Count; i++)
            {
                CharacterTestRuntimeAiLabCardConditionFact fact = values[i];
                CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(fact, out var descriptor);
                labels[i] = CharacterTestRuntimeAiLabBehaviorConditionCatalog.CategoryText(descriptor.Category)
                    + " / " + descriptor.DisplayName;
                if (fact == row.Fact)
                    selected = i;
            }
            ShowConditionDropdown(anchor, labels, selected, index =>
            {
                CharacterTestRuntimeAiLabCardConditionFact fact = values[index];
                if (fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction)
                {
                    ShowCharacterActionPicker(
                        anchor,
                        row.CharacterActionId,
                        actionId => Submit(
                            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionCharacterActionId,
                            conditionIndex,
                            stringValue: actionId.Value.ToString(CultureInfo.InvariantCulture)));
                    return;
                }
                if (fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable)
                {
                    ShowAbilityPicker(anchor, row.AbilityId, abilityId => Submit(
                        CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ReplaceSubjectCondition,
                        conditionIndex: conditionIndex,
                        intValue: (int)fact,
                        parameterIntValue: abilityId));
                    return;
                }
                Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionFact,
                    conditionIndex,
                    intValue: (int)fact);
            });
        }

        private void ShowAbilityPicker(Fgui.GObject anchor, int selectedAbilityId, Action<int> select)
        {
            IReadOnlyList<MxFramework.Gameplay.AbilityDefinitionConfig> abilities =
                CharacterTestAbilityCatalogSource.CreateRegistry().CreateSnapshot();
            var labels = new string[abilities.Count];
            int selected = -1;
            for (int i = 0; i < abilities.Count; i++)
            {
                labels[i] = abilities[i].DisplayName + " · "
                    + abilities[i].AbilityId.ToString(CultureInfo.InvariantCulture);
                if (abilities[i].AbilityId == selectedAbilityId)
                    selected = i;
            }
            ShowConditionDropdown(anchor, labels, selected, index =>
                select?.Invoke(abilities[index].AbilityId));
        }

        private void ShowOperatorDropdown(int conditionIndex, Fgui.GObject anchor)
        {
            if (!TryGetCondition(conditionIndex, out BehaviorRecipeTriggerConditionViewModel row))
                return;
            int[] values = ResolveOperatorChoices(row.ValueKind);
            var labels = new string[values.Length];
            int selected = 0;
            for (int i = 0; i < values.Length; i++)
            {
                labels[i] = ResolveOperatorDropdownText(values[i]);
                if (values[i] == row.EffectiveOperator)
                    selected = i;
            }
            ShowConditionDropdown(anchor, labels, selected, index =>
                Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionOperator,
                    conditionIndex,
                    intValue: values[index]));
        }

        private void ShowBooleanDropdown(int conditionIndex, Fgui.GObject anchor)
        {
            if (!TryGetCondition(conditionIndex, out BehaviorRecipeTriggerConditionViewModel row))
                return;
            ShowConditionDropdown(anchor, new[] { "是", "否" }, row.BoolValue ? 0 : 1, index =>
                Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionBoolValue,
                    conditionIndex,
                    boolValue: index == 0));
        }

        private void ShowSignalDropdown(Fgui.GObject anchor)
        {
            if (_model?.CanEdit != true || !_model.CanUseActionSignal)
                return;
            CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal[] values =
            {
                CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Completed,
                CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Failed,
                CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Rejected
            };
            var labels = new string[values.Length];
            int selected = 0;
            for (int i = 0; i < values.Length; i++)
            {
                labels[i] = ResolveSignalText(values[i]);
                if (values[i] == _model.ActionSignal)
                    selected = i;
            }
            ShowConditionDropdown(anchor, labels, selected, index => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActionSignal,
                intValue: (int)values[index]));
        }

        private void ShowSignalReasonDropdown(Fgui.GObject anchor)
        {
            if (_model?.CanEdit != true || !_model.CanUseActionSignal)
                return;
            var labels = new string[SignalReasons.Length];
            int selected = 0;
            for (int i = 0; i < SignalReasons.Length; i++)
            {
                labels[i] = ResolveReasonText(SignalReasons[i]);
                if (string.Equals(SignalReasons[i], _model.ActionSignalReasonCode, StringComparison.Ordinal))
                    selected = i;
            }
            ShowConditionDropdown(anchor, labels, selected, index => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActionSignalReason,
                stringValue: SignalReasons[index]));
        }

        private void ShowOutcomeDropdown(Fgui.GObject anchor)
        {
            if (_model?.CanEdit != true)
                return;
            if (_model.UnifiedV6)
            {
                ShowOperationDropdown(anchor);
                return;
            }
            string[] labels = { "显式退出行为卡", "进入其他时间轴" };
            int selected = _model.OutcomeKind
                == CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline ? 1 : 0;
            ShowConditionDropdown(anchor, labels, selected, index =>
            {
                if (index == 0)
                {
                    Submit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OutcomeKind,
                        intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner);
                }
                else
                {
                    SelectDefaultTimelineOutcome();
                }
            });
        }

        private void ShowOperationDropdown(Fgui.GObject anchor)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind[] allValues =
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured,
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget,
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition,
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion,
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline,
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner
            };
            var compatible = new System.Collections.Generic.List<CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind>(
                allValues.Length);
            for (int i = 0; i < allValues.Length; i++)
            {
                if (CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.IsOperationCompatible(
                    _model.TimingKind,
                    allValues[i]))
                {
                    compatible.Add(allValues[i]);
                }
            }
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind[] values = compatible.ToArray();
            var labels = new string[values.Length];
            int selected = 0;
            for (int i = 0; i < values.Length; i++)
            {
                labels[i] = ResolveOperationText(values[i]);
                if (values[i] == _model.OperationKind)
                    selected = i;
            }
            ShowConditionDropdown(anchor, labels, selected, index =>
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind operation = values[index];
                Submit(
                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationKind,
                    intValue: (int)operation);
            });
        }

        private void ShowConditionAddDropdown(Fgui.GObject anchor)
        {
            if (_model?.AllowsEventSignal == false)
            {
                string[] factOnlyLabels =
                {
                    "添加“全部满足”组", "添加“任一满足”组", "添加“非”组"
                };
                ShowConditionDropdown(anchor, factOnlyLabels, -1, index => Submit(
                    index switch
                    {
                        1 => CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAnyGroup,
                        2 => CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddNotGroup,
                        _ => CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAllGroup
                    },
                    stringValue: _selectedConditionNodeId));
                return;
            }
            string[] labels =
            {
                "引用其他事件结果",
                "添加“全部满足”组",
                "添加“任一满足”组",
                "添加“非”组"
            };
            ShowConditionDropdown(anchor, labels, -1, index => Submit(
                index switch
                {
                    1 => CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAllGroup,
                    2 => CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddAnyGroup,
                    3 => CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddNotGroup,
                    _ => CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddEventSignal
                },
                stringValue: _selectedConditionNodeId));
        }

        private void ShowConditionGroupKindDropdown(
            BehaviorRecipeTriggerConditionViewModel row,
            Fgui.GObject anchor)
        {
            if (_model?.CanEdit != true || row == null || anchor == null)
                return;
            bool canNegate = row.ChildCount == 1;
            string[] labels = canNegate
                ? new[] { "全部满足", "任一满足", "非" }
                : new[] { "全部满足", "任一满足" };
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind[] kinds = canNegate
                ? new[]
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All,
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any,
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not
                }
                : new[]
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All,
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any
                };
            int selected = Array.IndexOf(kinds, row.NodeKind);
            ShowConditionDropdown(anchor, labels, selected, index => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.SetConditionGroupKind,
                intValue: (int)kinds[index],
                stringValue: row.NodeId));
        }

        private void ShowEventSignalSourceDropdown(int conditionIndex, string selectedEventId, Fgui.GObject anchor)
        {
            BehaviorRecipeTriggerEventSignalOptionViewModel[] options =
                _model?.SignalEventOptions ?? Array.Empty<BehaviorRecipeTriggerEventSignalOptionViewModel>();
            if (!_model.CanEdit || options.Length == 0)
                return;
            var labels = new string[options.Length];
            int selected = -1;
            for (int i = 0; i < options.Length; i++)
            {
                labels[i] = options[i].DisplayText;
                if (string.Equals(options[i].EventId, selectedEventId, StringComparison.Ordinal))
                    selected = i;
            }
            ShowConditionDropdown(anchor, labels, selected, index => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionSignalSourceEventId,
                conditionIndex: conditionIndex,
                stringValue: options[index].EventId));
        }

        private void ShowEventSignalKindDropdown(
            int conditionIndex,
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal selectedSignal,
            Fgui.GObject anchor)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeEventSignal[] values =
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed,
                CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Failed,
                CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Rejected,
                CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Cancelled,
                CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.CommitFailed
            };
            string[] labels = { "已完成", "执行失败", "执行被拒绝", "被取消", "本次提交失败（未出招）" };
            int selected = Array.IndexOf(values, selectedSignal);
            ShowConditionDropdown(anchor, labels, selected, index => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionSignalKind,
                conditionIndex: conditionIndex,
                intValue: (int)values[index]));
        }


        private void ShowTargetTimelineDropdown(Fgui.GObject anchor)
        {
            if (_model?.CanEdit != true || _model.TimelineOptions.Length == 0)
                return;
            var labels = new string[_model.TimelineOptions.Length];
            int selected = 0;
            for (int i = 0; i < _model.TimelineOptions.Length; i++)
            {
                labels[i] = _model.TimelineOptions[i].DisplayText;
                if (string.Equals(
                    _model.TimelineOptions[i].TimelineId,
                    _model.TargetTimelineId,
                    StringComparison.Ordinal))
                {
                    selected = i;
                }
            }
            ShowConditionDropdown(anchor, labels, selected, index => Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.TargetTimelineId,
                stringValue: _model.TimelineOptions[index].TimelineId));
        }

        private bool TryGetCondition(
            int conditionIndex,
            out BehaviorRecipeTriggerConditionViewModel row)
        {
            row = null;
            if (_model?.CanEdit != true || conditionIndex < 0)
            {
                return false;
            }
            if (_model.UnifiedV6)
            {
                // Unified condition trees expose group rows and leaf rows in the same visible
                // list, while mutations address only the flattened leaf index. Looking up the
                // visible array directly made leaf 0 resolve to the root "All" group, so boolean
                // dropdowns displayed the wrong selected value and disabled the value the author
                // was trying to choose. Resolve the leaf identity explicitly for every dropdown.
                return BehaviorRecipeTriggerEventInspectorProjection.TryResolveConditionLeaf(
                    _model.Conditions,
                    conditionIndex,
                    out row);
            }
            if (conditionIndex >= _model.Conditions.Length)
                return false;
            row = _model.Conditions[conditionIndex];
            return row != null;
        }

        private void ShowConditionDropdown(
            Fgui.GObject anchor,
            string[] labels,
            int selectedIndex,
            Action<int> select)
        {
            if (anchor == null || labels == null || labels.Length == 0)
                return;
            if (_model?.TacticalEditor?.ConditionsOpen == true && _tacticalConditionLease == null) CaptureTacticalConditionLease();
            EnsureConditionDropdown();
            bool dropdownOpen = _popupService?.IsOpen(_conditionDropdown) == true
                || _conditionDropdown.parent != null;
            if (dropdownOpen)
            {
                bool sameAnchor = ReferenceEquals(_conditionDropdownAnchor, anchor);
                CloseConditionDropdown();
                if (sameAnchor)
                    return;
            }
            int columns = labels.Length > 8 ? 2 : 1;
            int rows = Mathf.CeilToInt(labels.Length / (float)columns);
            float columnWidth = Mathf.Max(anchor.width, columns > 1 ? 206f : 126f);
            float width = 16f + columns * columnWidth + (columns - 1) * 6f;
            float height = 16f + rows * 38f;
            _conditionDropdown.SetSize(width, height);
            _conditionDropdownBackground.SetXY(0f, 0f);
            _conditionDropdownBackground.SetSize(width, height);
            _conditionDropdownBackground.DrawRect(
                width,
                height,
                1,
                RuntimeSrgb(70, 101, 116),
                RuntimeSrgb(7, 20, 29, 252));
            for (int i = 0; i < _conditionDropdownButtons.Length; i++)
            {
                Fgui.GButton button = _conditionDropdownButtons[i];
                bool visible = i < labels.Length;
                button.visible = visible;
                if (!visible)
                    continue;
                int itemIndex = i;
                int column = i / rows;
                int row = i % rows;
                button.SetXY(8f + column * (columnWidth + 6f), 8f + row * 38f);
                button.SetSize(columnWidth, 34f);
                button.text = (i == selectedIndex ? "✓  " : "    ") + labels[i];
                button.enabled = i != selectedIndex;
                button.grayed = false;
                button.onClick.Set(context =>
                {
                    context.StopPropagation();
                    CloseConditionDropdown();
                    select(itemIndex);
                });
            }
            if (_popupService != null)
            {
                _conditionDropdownAnchor = anchor;
                _conditionDropdownUsesPopupService = _popupService.Show(
                    _viewId,
                    _conditionDropdown,
                    anchor,
                    new MxFairyGuiPopupOptions(
                        MxFairyGuiPopupPlacement.Auto,
                        safeMargin: 12f,
                        anchorGap: 4f));
                if (!_conditionDropdownUsesPopupService)
                {
                    Fgui.GRoot.inst.TogglePopup(
                        _conditionDropdown,
                        anchor,
                        Fgui.PopupDirection.Auto,
                        false);
                }
            }
            else
            {
                _conditionDropdownAnchor = anchor;
                _conditionDropdownUsesPopupService = false;
                Fgui.GRoot.inst.TogglePopup(
                    _conditionDropdown,
                    anchor,
                    Fgui.PopupDirection.Auto,
                    false);
            }
        }

        private void EnsureConditionDropdown()
        {
            if (_conditionDropdown != null)
                return;
            _conditionDropdown = new Fgui.GComponent
            {
                name = "behaviorConditionDropdown",
                opaque = true
            };
            _conditionDropdownBackground = new Fgui.GGraph
            {
                name = "background",
                touchable = false
            };
            _conditionDropdown.AddChild(_conditionDropdownBackground);
            for (int i = 0; i < _conditionDropdownButtons.Length; i++)
            {
                Fgui.GButton button = Fgui.UIPackage.CreateObject(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "CompactNeutralButton")?.asButton;
                button ??= new Fgui.GButton();
                button.name = "conditionChoice" + i;
                button.mode = Fgui.ButtonMode.Common;
                SetButtonFontSize(button, 15);
                _conditionDropdownButtons[i] = button;
                _conditionDropdown.AddChild(button);
            }
        }

        private void CloseConditionDropdown()
        {
            if (_conditionDropdown == null)
                return;
            _conditionDropdownAnchor = null;
            if (_conditionDropdownUsesPopupService && _popupService != null)
                _popupService.Hide(_conditionDropdown, MxFairyGuiPopupCloseReason.Toggle);
            else if (_conditionDropdown.parent != null)
                Fgui.GRoot.inst.HidePopup(_conditionDropdown);
            _conditionDropdownUsesPopupService = false;
        }

        private static int[] ResolveOperatorChoices(BehaviorRecipeTriggerConditionValueKind kind) => kind switch
        {
            BehaviorRecipeTriggerConditionValueKind.Range => new[] { 6, 7 },
            BehaviorRecipeTriggerConditionValueKind.Number => new[] { 0, 1, 2, 3, 4, 5 },
            _ => new[] { 0, 1 }
        };

        private static string ResolveOperatorDropdownText(int value) => value switch
        {
            0 => "等于  =",
            1 => "不等于  ≠",
            2 => "小于等于  ≤",
            3 => "大于  >",
            4 => "大于等于  ≥",
            5 => "小于  <",
            6 => "在区间内",
            7 => "不在区间内",
            _ => "等于  ="
        };

        private static string ResolveFactDropdownText(
            CharacterTestRuntimeAiLabCardConditionFact fact) =>
            CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                fact,
                out CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor)
                    ? descriptor.DisplayName
                    : fact.ToString();

        private static Color RuntimeSrgb(byte red, byte green, byte blue, byte alpha = 255) =>
            new Color(
                Mathf.GammaToLinearSpace(red / 255f),
                Mathf.GammaToLinearSpace(green / 255f),
                Mathf.GammaToLinearSpace(blue / 255f),
                alpha / 255f);

    }
}
