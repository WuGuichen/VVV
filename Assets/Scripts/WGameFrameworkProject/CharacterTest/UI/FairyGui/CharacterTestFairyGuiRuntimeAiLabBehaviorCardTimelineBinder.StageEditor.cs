using System;
using System.Globalization;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private const string StageEditorComponentName = "RuntimeAiLabStageEditorPopover";

        private void BindStageEditorPopover(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            int expandedIndex = -1;
            for (int i = 0; i < stageCount; i++)
            {
                if (stages[i]?.Expanded == true)
                {
                    expandedIndex = i;
                    break;
                }
            }
            if (expandedIndex < 0)
            {
                if (_stageEditorPopover != null)
                    _stageEditorPopover.visible = false;
                HideStageTypedOptions();
                CancelStageFieldEditVisual(deactivating: false);
                return;
            }

            EnsureStageEditorPopover();
            CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[expandedIndex];
            if (!string.Equals(_stageEditorStableId, stage.StageId, StringComparison.Ordinal))
                HideStageTypedOptions();
            _stageEditorStableId = stage.StageId ?? string.Empty;
            const float width = 1000f;
            const float height = 300f;
            float preferredX = _stageX[expandedIndex] - CanvasLeft + _stageWidth[expandedIndex] + 12f;
            float visibleWidth = Mathf.Max(320f, _root?.width ?? CanvasRight - CanvasLeft);
            if (preferredX + width > visibleWidth)
                preferredX = _stageX[expandedIndex] - CanvasLeft - width - 12f;
            float x = Mathf.Clamp(preferredX, 8f, Mathf.Max(8f, visibleWidth - width - 8f));
            const float y = 8f;
            _stageEditorPopover.SetXY(x, y);
            _stageEditorPopover.SetSize(width, height);
            _stageEditorPopover.visible = true;
            _stageEditorPopover.sortingOrder = 50;
            ApplyCompactStageEditorLayout();
            BindStageEditorValues(stage);
            ApplyStageEditorReadability();
        }

        private void ApplyCompactStageEditorLayout()
        {
            SetObjectGeometry(_stageEditorPopover.GetChild("header"), 0f, 0f, 1000f, 56f);
            SetObjectGeometry(_stageEditorPopover.GetChild("editorTitle"), 18f, 6f, 780f, 26f);
            SetObjectGeometry(_stageEditorPopover.GetChild("editorSubtitle"), 18f, 32f, 780f, 18f);
            SetObjectGeometry(_stageEditorPopover.GetChild("btnClose"), 948f, 10f, 36f, 30f);

            SetObjectGeometry(_stageEditorPopover.GetChild("titleLabel"), 18f, 66f, 96f, 18f);
            SetObjectGeometry(_stageEditorPopover.GetChild("inputTitle"), 18f, 86f, 430f, 34f);
            SetObjectGeometry(_stageEditorPopover.GetChild("startLabel"), 468f, 66f, 90f, 18f);
            SetObjectGeometry(_stageEditorPopover.GetChild("inputStart"), 468f, 86f, 104f, 34f);
            SetObjectGeometry(_stageEditorPopover.GetChild("durationLabel"), 588f, 66f, 90f, 18f);
            SetObjectGeometry(_stageEditorPopover.GetChild("inputDuration"), 588f, 86f, 104f, 34f);
            SetObjectGeometry(_stageEditorPopover.GetChild("btnDurationMode"), 708f, 86f, 274f, 34f);

            SetObjectGeometry(_stageEditorPopover.GetChild("kindLabel"), 18f, 134f, 76f, 34f);
            SetObjectGeometry(_stageEditorPopover.GetChild("btnKind"), 96f, 134f, 352f, 34f);
            string[] dynamicLabels =
            {
                "abilityLabel", "actionLabel", "moveLabel", "waitLabel", "legacyLabel"
            };
            for (int i = 0; i < dynamicLabels.Length; i++)
                SetObjectGeometry(_stageEditorPopover.GetChild(dynamicLabels[i]), 468f, 134f, 116f, 34f);
            SetObjectGeometry(_stageEditorPopover.GetChild("inputAbilityId"), 588f, 134f, 394f, 34f);
            SetObjectGeometry(_stageEditorPopover.GetChild("inputActionId"), 588f, 134f, 394f, 34f);
            SetObjectGeometry(_stageEditorPopover.GetChild("inputWaitFrames"), 588f, 134f, 394f, 34f);
            SetObjectGeometry(_stageEditorPopover.GetChild("btnLegacyAction"), 588f, 134f, 394f, 34f);
            SetObjectGeometry(_stageEditorPopover.GetChild("inputMoveMin"), 588f, 134f, 168f, 34f);
            SetObjectGeometry(_stageEditorPopover.GetChild("moveSeparator"), 760f, 134f, 42f, 34f);
            SetObjectGeometry(_stageEditorPopover.GetChild("inputMoveMax"), 806f, 134f, 176f, 34f);

            SetObjectGeometry(_stageEditorPopover.GetChild("noteLabel"), 18f, 180f, 80f, 18f);
            SetObjectGeometry(_stageEditorPopover.GetChild("inputNote"), 96f, 178f, 602f, 58f);
            SetObjectGeometry(_stageEditorPopover.GetChild("fieldError"), 712f, 178f, 270f, 58f);
            SetObjectGeometry(_stageEditorPopover.GetChild("btnAddDetector"), 18f, 250f, 180f, 36f);
            SetObjectGeometry(_stageEditorPopover.GetChild("btnDuplicate"), 210f, 250f, 180f, 36f);
            SetObjectGeometry(_stageEditorPopover.GetChild("btnDelete"), 402f, 250f, 196f, 36f);
        }

        private static void SetObjectGeometry(
            Fgui.GObject target,
            float x,
            float y,
            float width,
            float height)
        {
            if (target == null)
                return;
            target.SetXY(x, y);
            target.SetSize(width, height);
        }

        private void EnsureStageEditorPopover()
        {
            if (_stageEditorPopover != null)
                return;
            _stageEditorPopover = CreateRequiredComponent(StageEditorComponentName);
            _stageEditorPopover.name = "runtimeStageEditorPopover";
            _contentLayer.AddChild(_stageEditorPopover);
            _stageTypedOptionMenu = _stageEditorPopover.GetChild("typedOptionMenu")?.asCom;
            ConfigureStageEditorHandlers();
        }

        private void ConfigureStageEditorHandlers()
        {
            if (_stageEditorHandlersConfigured || _stageEditorPopover == null)
                return;
            _stageEditorHandlersConfigured = true;
            _stageEditorPopover.onTouchBegin.Set(context => context.StopPropagation());
            BindStageFieldInput("inputTitle", CharacterTestRuntimeAiLabStageFieldKind.Title);
            BindStageFieldInput("inputStart", CharacterTestRuntimeAiLabStageFieldKind.PlannedStartFrame);
            BindStageFieldInput("inputDuration", CharacterTestRuntimeAiLabStageFieldKind.PlannedDurationFrames);
            BindStageFieldInput("inputAbilityId", CharacterTestRuntimeAiLabStageFieldKind.AbilityId);
            BindStageFieldInput("inputActionId", CharacterTestRuntimeAiLabStageFieldKind.CharacterActionId);
            BindStageFieldInput("inputMoveMin", CharacterTestRuntimeAiLabStageFieldKind.MoveDistanceBand);
            BindStageFieldInput("inputMoveMax", CharacterTestRuntimeAiLabStageFieldKind.MoveDistanceBand);
            BindStageFieldInput("inputWaitFrames", CharacterTestRuntimeAiLabStageFieldKind.WaitFrames);
            BindStageFieldInput("inputNote", CharacterTestRuntimeAiLabStageFieldKind.Note);
            BindSlotButton(_stageEditorPopover, "btnKind", () => RunStageEditorAction(() =>
                ShowStageTypedOptions(CharacterTestRuntimeAiLabStageFieldKind.Kind)));
            BindSlotButton(_stageEditorPopover, "btnDurationMode", () => RunStageEditorAction(() =>
                ShowStageTypedOptions(CharacterTestRuntimeAiLabStageFieldKind.DurationMode)));
            BindSlotButton(_stageEditorPopover, "btnLegacyAction", () => RunStageEditorAction(() =>
                ShowStageTypedOptions(CharacterTestRuntimeAiLabStageFieldKind.MovementStrategy)));
            BindSlotButton(_stageEditorPopover, "btnAddDetector", () => RunStageEditorAction(() =>
                Enqueue(CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStageDetector, _stageEditorStableId)));
            BindSlotButton(_stageEditorPopover, "btnDuplicate", () => RunStageEditorAction(() =>
                _commands?.DuplicateSelectedStages()));
            BindSlotButton(_stageEditorPopover, "btnDelete", () => RunStageEditorAction(() =>
                _commands?.DeleteSelectedStages()));
            BindSlotButton(_stageEditorPopover, "btnClose", () => RunStageEditorAction(() =>
                _commands?.ToggleStageExpanded(_stageEditorStableId)));
            if (_stageTypedOptionMenu != null)
            {
                _stageTypedOptionMenu.onTouchBegin.Set(context => context.StopPropagation());
                for (int i = 0; i < 7; i++)
                {
                    int optionIndex = i;
                    BindSlotButton(_stageTypedOptionMenu, "option" + i, () =>
                        ApplyStageTypedOption(optionIndex));
                }
            }
        }

        private void ShowStageTypedOptions(CharacterTestRuntimeAiLabStageFieldKind field)
        {
            if (_stageTypedOptionMenu == null || !_canEdit)
                return;
            if (_stageTypedOptionMenu.visible && _stageTypedOptionField == field)
            {
                HideStageTypedOptions();
                return;
            }

            _stageTypedOptionField = field;
            int count = field == CharacterTestRuntimeAiLabStageFieldKind.Kind
                ? 5
                : field == CharacterTestRuntimeAiLabStageFieldKind.DurationMode
                    ? 3
                    : field == CharacterTestRuntimeAiLabStageFieldKind.MovementStrategy ? 4 : 7;
            const float y = 126f;
            float x = field == CharacterTestRuntimeAiLabStageFieldKind.DurationMode ? 652f : 96f;
            _stageTypedOptionMenu.SetXY(x, y);
            _stageTypedOptionMenu.SetSize(330f, 12f + count * 32f);
            _stageTypedOptionMenu.sortingOrder = 200;
            _stageTypedOptionMenu.visible = true;
            for (int i = 0; i < 7; i++)
            {
                Fgui.GButton option = _stageTypedOptionMenu.GetChild("option" + i)?.asButton;
                if (option == null)
                    continue;
                option.visible = i < count;
                if (i < count)
                    option.title = ResolveStageTypedOptionTitle(field, i);
            }
        }

        private void ApplyStageTypedOption(int optionIndex)
        {
            if (_stageTypedOptionMenu == null || !_stageTypedOptionMenu.visible)
                return;
            CharacterTestRuntimeAiLabStageFieldKind field = _stageTypedOptionField;
            HideStageTypedOptions();
            switch (field)
            {
                case CharacterTestRuntimeAiLabStageFieldKind.Kind:
                    _commands?.SetSelectedStageKind(
                        _stageEditorStableId,
                        ResolveStageKindOption(optionIndex));
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.DurationMode:
                    _commands?.SetSelectedStageDurationMode(
                        _stageEditorStableId,
                        (CharacterTestRuntimeAiLabLogicDurationMode)optionIndex);
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.LegacyActionKind:
                    _commands?.SetSelectedLegacyActionKind(
                        _stageEditorStableId,
                        (CharacterTestRuntimeAiPlannerActionKind)optionIndex);
                    break;
                case CharacterTestRuntimeAiLabStageFieldKind.MovementStrategy:
                    _commands?.SetSelectedMovementStrategy(
                        _stageEditorStableId,
                        (CharacterTestRuntimeAiLabMovementStrategy)optionIndex);
                    break;
            }
        }

        private static string ResolveStageTypedOptionTitle(
            CharacterTestRuntimeAiLabStageFieldKind field,
            int optionIndex)
        {
            if (field == CharacterTestRuntimeAiLabStageFieldKind.Kind)
            {
                return optionIndex switch
                {
                    0 => "Use Ability",
                    1 => "Character Action",
                    2 => "Move To Target",
                    3 => "Wait",
                    4 => "Logic",
                    _ => string.Empty
                };
            }
            if (field == CharacterTestRuntimeAiLabStageFieldKind.DurationMode)
            {
                return optionIndex switch
                {
                    0 => "Fixed",
                    1 => "Until Event",
                    2 => "Infinite",
                    _ => string.Empty
                };
            }
            if (field == CharacterTestRuntimeAiLabStageFieldKind.MovementStrategy)
            {
                return optionIndex switch
                {
                    0 => "接近目标",
                    1 => "保持距离",
                    2 => "环境避险",
                    3 => "受阻横移",
                    _ => string.Empty
                };
            }
            return optionIndex switch
            {
                0 => "Wait",
                1 => "Move To Target",
                2 => "Strafe",
                3 => "Retreat",
                4 => "Primary Attack",
                5 => "Cast Ability Slot",
                6 => "Dodge",
                _ => string.Empty
            };
        }

        private static CharacterTestRuntimeAiLabCardStepKind ResolveStageKindOption(int optionIndex)
        {
            return optionIndex switch
            {
                0 => CharacterTestRuntimeAiLabCardStepKind.UseAbility,
                1 => CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                2 => CharacterTestRuntimeAiLabCardStepKind.MoveToTarget,
                3 => CharacterTestRuntimeAiLabCardStepKind.Wait,
                4 => CharacterTestRuntimeAiLabCardStepKind.Logic,
                _ => CharacterTestRuntimeAiLabCardStepKind.Logic
            };
        }

        private void HideStageTypedOptions()
        {
            if (_stageTypedOptionMenu != null)
                _stageTypedOptionMenu.visible = false;
            _stageTypedOptionField = default;
        }

        private void BindStageFieldInput(string name, CharacterTestRuntimeAiLabStageFieldKind field)
        {
            Fgui.GTextInput input = _stageEditorPopover.GetChild(name)?.asTextInput;
            if (input == null)
                return;
            input.onTouchBegin.Set(context => context.StopPropagation());
            input.onFocusIn.Set(() => BeginStageFieldInput(field));
            input.onSubmit.Set(() => { CommitStageFieldInput(input, field); });
            input.onFocusOut.Set(() => { CommitStageFieldInput(input, field); });
            if (field == CharacterTestRuntimeAiLabStageFieldKind.Note)
            {
                input.onKeyDown.Set(context =>
                {
                    if (context?.inputEvent != null
                        && context.inputEvent.ctrl
                        && (context.inputEvent.keyCode == KeyCode.Return
                            || context.inputEvent.keyCode == KeyCode.KeypadEnter))
                    {
                        CommitStageFieldInput(input, field);
                        context.StopPropagation();
                    }
                });
            }
        }

        private void BeginStageFieldInput(CharacterTestRuntimeAiLabStageFieldKind field)
        {
            CancelStageFieldEditVisual(deactivating: false);
            if (_commands?.BeginStageFieldEdit(_stageEditorStableId, field, out _stageFieldEditEpoch) != true)
                return;
            _stageFieldEditStableId = _stageEditorStableId;
            _stageFieldEditKind = field;
            SetText(_stageEditorPopover, "fieldError", string.Empty);
        }

        private void RunStageEditorAction(Action action)
        {
            if (!TryCommitActiveStageFieldBeforeNavigation())
                return;
            action?.Invoke();
        }

        private bool TryCommitActiveStageFieldBeforeNavigation()
        {
            if (_stageFieldEditEpoch == 0L)
                return true;
            string inputName = _stageFieldEditKind switch
            {
                CharacterTestRuntimeAiLabStageFieldKind.Title => "inputTitle",
                CharacterTestRuntimeAiLabStageFieldKind.PlannedStartFrame => "inputStart",
                CharacterTestRuntimeAiLabStageFieldKind.PlannedDurationFrames => "inputDuration",
                CharacterTestRuntimeAiLabStageFieldKind.AbilityId => "inputAbilityId",
                CharacterTestRuntimeAiLabStageFieldKind.CharacterActionId => "inputActionId",
                CharacterTestRuntimeAiLabStageFieldKind.MoveDistanceBand => "inputMoveMin",
                CharacterTestRuntimeAiLabStageFieldKind.WaitFrames => "inputWaitFrames",
                CharacterTestRuntimeAiLabStageFieldKind.Note => "inputNote",
                _ => string.Empty
            };
            Fgui.GTextInput input = inputName.Length > 0
                ? _stageEditorPopover?.GetChild(inputName)?.asTextInput
                : null;
            if (!CommitStageFieldInput(input, _stageFieldEditKind))
            {
                SetText(_stageEditorPopover, "fieldError", "请先修正并提交当前字段。");
                return false;
            }
            return true;
        }

        internal bool TryCommitPendingInspectorEdit() =>
            TryCommitActiveStageFieldBeforeNavigation();

        private bool CommitStageFieldInput(
            Fgui.GTextInput input,
            CharacterTestRuntimeAiLabStageFieldKind field)
        {
            if (_stageFieldEditEpoch == 0L
                || field != _stageFieldEditKind
                || !string.Equals(_stageFieldEditStableId, _stageEditorStableId, StringComparison.Ordinal))
            {
                return true;
            }
            if (!TryParseStageFieldValue(field, input?.text, out CharacterTestRuntimeAiLabStageFieldValue value, out string error))
            {
                SetText(_stageEditorPopover, "fieldError", error);
                input?.RequestFocus();
                return false;
            }
            long epoch = _stageFieldEditEpoch;
            if (_commands?.CommitStageFieldEdit(epoch, _stageEditorStableId, field, value) == true)
            {
                ClearStageFieldEditVisualState();
                return true;
            }
            SetText(_stageEditorPopover, "fieldError", _commands?.LastMessage ?? "字段提交失败。");
            input?.RequestFocus();
            return false;
        }

        private bool TryParseStageFieldValue(
            CharacterTestRuntimeAiLabStageFieldKind field,
            string text,
            out CharacterTestRuntimeAiLabStageFieldValue value,
            out string error)
        {
            value = default;
            error = string.Empty;
            string normalized = text?.Trim() ?? string.Empty;
            switch (field)
            {
                case CharacterTestRuntimeAiLabStageFieldKind.Title:
                case CharacterTestRuntimeAiLabStageFieldKind.CharacterActionId:
                case CharacterTestRuntimeAiLabStageFieldKind.Note:
                    value = new CharacterTestRuntimeAiLabStageFieldValue(stringValue: text ?? string.Empty);
                    return true;
                case CharacterTestRuntimeAiLabStageFieldKind.PlannedStartFrame:
                case CharacterTestRuntimeAiLabStageFieldKind.PlannedDurationFrames:
                case CharacterTestRuntimeAiLabStageFieldKind.AbilityId:
                case CharacterTestRuntimeAiLabStageFieldKind.WaitFrames:
                    if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                    {
                        error = "请输入有效整数。";
                        return false;
                    }
                    value = new CharacterTestRuntimeAiLabStageFieldValue(intValue: integer);
                    return true;
                case CharacterTestRuntimeAiLabStageFieldKind.MoveDistanceBand:
                    return TryParseMoveDistanceBand(out value, out error);
                default:
                    error = "当前字段不支持文本提交。";
                    return false;
            }
        }

        private bool TryParseMoveDistanceBand(
            out CharacterTestRuntimeAiLabStageFieldValue value,
            out string error)
        {
            value = default;
            error = string.Empty;
            string minimumText = _stageEditorPopover.GetChild("inputMoveMin")?.asTextInput?.text ?? string.Empty;
            string maximumText = _stageEditorPopover.GetChild("inputMoveMax")?.asTextInput?.text ?? string.Empty;
            if (!float.TryParse(minimumText, NumberStyles.Float, CultureInfo.InvariantCulture, out float minimum)
                || !float.TryParse(maximumText, NumberStyles.Float, CultureInfo.InvariantCulture, out float maximum))
            {
                error = "移动距离必须是有效数字。";
                return false;
            }
            value = new CharacterTestRuntimeAiLabStageFieldValue(
                floatValue: minimum,
                secondFloatValue: maximum);
            return true;
        }

        private void BindStageEditorValues(CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            SetText(_stageEditorPopover, "editorTitle", "编辑 " + (stage.Title ?? stage.StageId));
            SetText(_stageEditorPopover, "editorSubtitle", stage.StageId + " · " + stage.Kind);
            SetButtonTitle("btnKind", stage.Kind.ToString());
            SetButtonTitle("btnDurationMode", stage.DurationMode.ToString());
            SetButtonTitle("btnLegacyAction", MovementStrategyLabel(stage.MovementStrategy));
            if (_stageFieldEditEpoch == 0L)
            {
                SetInputText("inputTitle", stage.Title);
                SetInputText("inputStart", stage.StartFrame.ToString(CultureInfo.InvariantCulture));
                SetInputText("inputDuration", stage.DurationFrames.ToString(CultureInfo.InvariantCulture));
                SetInputText("inputAbilityId", stage.AbilityId.ToString(CultureInfo.InvariantCulture));
                SetInputText(
                    "inputActionId",
                    stage.CharacterActionId.IsValid ? stage.CharacterActionId.ToString() : string.Empty);
                SetInputText("inputMoveMin", stage.MoveMinDistance.ToString("0.###", CultureInfo.InvariantCulture));
                SetInputText("inputMoveMax", stage.MoveMaxDistance.ToString("0.###", CultureInfo.InvariantCulture));
                SetInputText("inputWaitFrames", stage.WaitFrames.ToString(CultureInfo.InvariantCulture));
                SetInputText("inputNote", stage.Note);
            }
            bool fixedDuration = stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed;
            bool ability = stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility;
            bool move = stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
            SetInputEnabled("inputTitle", _canEdit);
            SetInputEnabled("inputStart", _canEdit);
            SetInputEnabled("inputDuration", fixedDuration && _canEdit);
            SetInputEnabled("inputAbilityId", ability && _canEdit);
            SetInputEnabled("inputActionId", stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction && _canEdit);
            SetInputEnabled("inputMoveMin", move && _canEdit);
            SetInputEnabled("inputMoveMax", move && _canEdit);
            SetInputEnabled("inputWaitFrames", stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait && _canEdit);
            SetInputEnabled("inputNote", _canEdit);
            SetStageEditorRowVisible("abilityLabel", "inputAbilityId", ability);
            SetStageEditorRowVisible("actionLabel", "inputActionId",
                stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction);
            SetObjectVisible(_stageEditorPopover, "moveLabel", move);
            SetObjectVisible(_stageEditorPopover, "inputMoveMin", move);
            SetObjectVisible(_stageEditorPopover, "moveSeparator", move);
            SetObjectVisible(_stageEditorPopover, "inputMoveMax", move);
            SetStageEditorRowVisible("waitLabel", "inputWaitFrames",
                stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait);
            SetText(_stageEditorPopover, "legacyLabel", "移动方式");
            SetStageEditorRowVisible("legacyLabel", "btnLegacyAction", move);
            SetButtonEnabled(_stageEditorPopover, "btnKind", _canEdit);
            SetButtonEnabled(_stageEditorPopover, "btnDurationMode", _canEdit);
            SetButtonEnabled(_stageEditorPopover, "btnLegacyAction", move && _canEdit);
            SetButtonEnabled(_stageEditorPopover, "btnAddDetector", _canEdit);
            SetButtonEnabled(_stageEditorPopover, "btnDuplicate", _canEdit);
            SetButtonEnabled(_stageEditorPopover, "btnDelete", _canEdit);
        }

        private static string MovementStrategyLabel(CharacterTestRuntimeAiLabMovementStrategy strategy)
        {
            switch (strategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance:
                    return "保持距离";
                case CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard:
                    return "环境避险";
                case CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked:
                    return "受阻横移";
                default:
                    return "接近目标";
            }
        }

        private void SetInputText(string name, string value)
        {
            Fgui.GTextInput input = _stageEditorPopover.GetChild(name)?.asTextInput;
            if (input != null)
                input.text = value ?? string.Empty;
        }

        private void SetButtonTitle(string name, string value)
        {
            Fgui.GButton button = _stageEditorPopover.GetChild(name)?.asButton;
            if (button != null)
                button.title = value ?? string.Empty;
        }

        private void ApplyStageEditorReadability()
        {
            SetStageEditorTextSize("editorTitle", 22);
            SetStageEditorTextSize("editorSubtitle", 16);
            SetStageEditorTextSize("titleLabel", 17);
            SetStageEditorTextSize("inputTitle", 18);
            SetStageEditorTextSize("startLabel", 17);
            SetStageEditorTextSize("inputStart", 18);
            SetStageEditorTextSize("durationLabel", 17);
            SetStageEditorTextSize("inputDuration", 18);
            SetStageEditorTextSize("kindLabel", 17);
            SetStageEditorTextSize("abilityLabel", 17);
            SetStageEditorTextSize("inputAbilityId", 18);
            SetStageEditorTextSize("actionLabel", 17);
            SetStageEditorTextSize("inputActionId", 18);
            SetStageEditorTextSize("moveLabel", 17);
            SetStageEditorTextSize("inputMoveMin", 18);
            SetStageEditorTextSize("moveSeparator", 18);
            SetStageEditorTextSize("inputMoveMax", 18);
            SetStageEditorTextSize("waitLabel", 17);
            SetStageEditorTextSize("inputWaitFrames", 18);
            SetStageEditorTextSize("legacyLabel", 17);
            SetStageEditorTextSize("noteLabel", 17);
            SetStageEditorTextSize("inputNote", 17);
            SetStageEditorTextSize("fieldError", 16);
            SetStageEditorButtonSize("btnClose", 18);
            SetStageEditorButtonSize("btnKind", 18);
            SetStageEditorButtonSize("btnDurationMode", 18);
            SetStageEditorButtonSize("btnLegacyAction", 18);
            SetStageEditorButtonSize("btnAddDetector", 17);
            SetStageEditorButtonSize("btnDuplicate", 17);
            SetStageEditorButtonSize("btnDelete", 17);
        }

        private void SetStageEditorTextSize(string name, int size)
        {
            SetTimelineTextFontSize(_stageEditorPopover?.GetChild(name)?.asTextField, size);
        }

        private void SetStageEditorButtonSize(string name, int size)
        {
            Fgui.GButton button = _stageEditorPopover?.GetChild(name)?.asButton;
            if (button != null && button.titleFontSize != size)
                button.titleFontSize = size;
        }

        private void SetStageEditorRowVisible(string labelName, string controlName, bool visible)
        {
            SetObjectVisible(_stageEditorPopover, labelName, visible);
            SetObjectVisible(_stageEditorPopover, controlName, visible);
        }

        private void SetInputEnabled(string name, bool enabled)
        {
            Fgui.GTextInput input = _stageEditorPopover.GetChild(name)?.asTextInput;
            if (input == null)
                return;
            input.enabled = enabled;
            input.grayed = !enabled;
        }

        private void CancelStageFieldEditVisual(bool deactivating)
        {
            if (_stageFieldEditEpoch != 0L || (_commands?.HasActiveStageFieldEdit ?? false))
                _commands?.CancelStageFieldEdit(deactivating);
            ClearStageFieldEditVisualState();
        }

        private void ClearStageFieldEditVisualState()
        {
            _stageFieldEditEpoch = 0L;
            _stageFieldEditKind = default;
            _stageFieldEditStableId = string.Empty;
        }
    }
}
