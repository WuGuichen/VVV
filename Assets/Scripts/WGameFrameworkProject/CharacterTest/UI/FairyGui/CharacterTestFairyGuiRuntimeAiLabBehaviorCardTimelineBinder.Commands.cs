using System;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private void HandleTimelineKeyDown(Fgui.EventContext context)
        {
            if (context?.inputEvent == null
                || context.initiator is Fgui.GTextInput
                || context.sender is Fgui.GTextInput)
                return;
            bool handled = true;
            bool control = context.inputEvent.ctrl || context.inputEvent.command;
            switch (context.inputEvent.keyCode)
            {
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    _commands?.DeleteSelectedStages();
                    break;
                case KeyCode.D when control:
                    _commands?.DuplicateSelectedStages();
                    break;
                case KeyCode.Z when control && context.inputEvent.shift:
                    _commands?.RedoStageEdit();
                    break;
                case KeyCode.Z when control:
                    _commands?.UndoStageEdit();
                    break;
                case KeyCode.Y when control:
                    _commands?.RedoStageEdit();
                    break;
                case KeyCode.Escape:
                    // The timeline consumes Escape before it reaches the Stage listener.
                    // Cancel the source-preview gesture here so mouse-up cannot commit it.
                    if (_tacticalGesture != null) CancelRecipeGesture();
                    CancelStagePointerGesture(clearSelection: false);
                    CancelStageMarquee();
                    CancelStageFieldEditVisual(deactivating: false);
                    CancelGraphFieldEditVisual(deactivating: false);
                    HideStageTypedOptions();
                    HideTransitionResultMenu();
                    break;
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                    int step = context.inputEvent.shift && (_commands?.StageSnapEnabled ?? true) ? 15 : 1;
                    _commands?.NudgeSelectedStages(
                        context.inputEvent.keyCode == KeyCode.LeftArrow ? -step : step);
                    break;
                default:
                    handled = false;
                    break;
            }
            if (!handled)
                return;
            context.PreventDefault();
            context.StopPropagation();
        }

        private void ConfigureStageButtons(Fgui.GComponent card, CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (_behaviorLogicMode)
            {
                SetObjectVisible(card, "resizeHandle", false);
                SetObjectVisible(card, "selectionCount", false);
                SetObjectVisible(card, "disclosure", false);
                return;
            }
            SetObjectVisible(card, "resizeHandle", stage.Selected);
            Fgui.GObject resizeHandle = card.GetChild("resizeHandle");
            if (resizeHandle != null)
                resizeHandle.enabled = stage.Selected && _canEdit
                    && stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed;
            SetObjectVisible(card, "selectionCount", stage.SelectionCount > 1 && stage.PrimarySelection);
            SetText(card, "selectionCount", stage.SelectionCount > 1
                ? stage.SelectionCount + " selected"
                : string.Empty);
            SetText(card, "disclosure", stage.Expanded ? "▾" : "▸");
        }

        private void ConfigureDetectorButtons(Fgui.GComponent band, CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (_behaviorLogicMode)
            {
                SetObjectVisible(band, "btnFact", false);
                SetObjectVisible(band, "btnWindowMinus", false);
                SetObjectVisible(band, "btnWindowPlus", false);
                bool hasResult = detector.LinkedTransitionCount > 0;
                SetObjectVisible(band, "btnAddTransition", true);
                SetButtonEnabled(band, "btnAddTransition", _canEdit);
                if (band.GetChild("btnAddTransition") != null)
                {
                    bool dense = band.height <= CompactBehaviorLogicDenseRouteHeight + 0.5f;
                    float width = hasResult ? 32f : dense ? 108f : 132f;
                    float height = dense ? 32f : 38f;
                    SetObjectXY(
                        band,
                        "btnAddTransition",
                        Math.Max(0f, band.width - width),
                        Math.Max(0f, (band.height - height) * 0.5f));
                    SetObjectSize(band, "btnAddTransition", width, height);
                    SetButtonTitle(
                        band,
                        "btnAddTransition",
                        hasResult ? "+" : "+ 设置结果");
                    Fgui.GButton resultButton = band.GetChild("btnAddTransition")?.asButton;
                    if (resultButton != null)
                    {
                        resultButton.titleFontSize = hasResult ? 20 : dense ? 15 : 17;
                        BindTooltip(resultButton, hasResult
                            ? "为这个检测继续添加一个结果分支。"
                            : "设置检测命中后的目标阶段、结束行为卡或重新决策。");
                    }
                }
                SetObjectVisible(band, "btnDelete", false);
                SetObjectVisible(band, "btnPriorityUp", false);
                SetObjectVisible(band, "btnPriorityDown", false);
                SetObjectVisible(band, "btnTrigger", false);
                SetObjectVisible(band, "btnWindowMode", false);
                SetObjectVisible(band, "btnDurationMinus", false);
                SetObjectVisible(band, "btnDurationPlus", false);
                SetObjectVisible(band, "btnDuplicate", false);
                return;
            }
            bool selected = detector.Selected;
            SetObjectVisible(band, "btnFact", false);
            SetObjectVisible(band, "btnWindowMinus", false);
            SetObjectVisible(band, "btnWindowPlus", false);
            SetObjectVisible(band, "btnAddTransition", selected);
            SetObjectVisible(band, "btnDelete", selected);
            SetObjectVisible(band, "btnPriorityUp", false);
            SetObjectVisible(band, "btnPriorityDown", false);
            SetObjectVisible(band, "btnTrigger", selected);
            SetObjectVisible(band, "btnWindowMode", selected);
            SetObjectVisible(band, "btnDurationMinus", false);
            SetObjectVisible(band, "btnDurationPlus", false);
            SetObjectVisible(band, "btnDuplicate", selected);
            SetButtonEnabled(band, "btnAddTransition", selected && _canEdit);
            SetButtonEnabled(band, "btnDelete", selected && _canEdit);
            SetButtonEnabled(band, "btnTrigger", selected && _canEdit);
            SetButtonEnabled(band, "btnWindowMode", selected && _canEdit);
            SetButtonEnabled(band, "btnDuplicate", selected && _canEdit);
        }

        private void ConfigureTransitionButtons(
            Fgui.GComponent chip,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (_behaviorLogicMode)
            {
                SetObjectVisible(chip, "btnPolicy", false);
                SetObjectVisible(chip, "btnTargetPrevious", false);
                SetObjectVisible(chip, "btnTargetNext", false);
                SetObjectVisible(chip, "btnDelete", false);
                return;
            }
            SetObjectVisible(chip, "btnPolicy", transition.Selected);
            SetObjectVisible(chip, "btnTargetPrevious", transition.Selected);
            SetObjectVisible(chip, "btnTargetNext", transition.Selected);
            SetObjectVisible(chip, "btnDelete", transition.Selected);
            SetButtonEnabled(chip, "btnPolicy", transition.Selected && _canEdit);
            SetButtonEnabled(chip, "btnTargetPrevious", transition.Selected && _canEdit);
            SetButtonEnabled(chip, "btnTargetNext", transition.Selected && _canEdit);
            SetButtonEnabled(chip, "btnDelete", transition.Selected && _canEdit);
        }

        private void MoveDetector(DetectorSlot slot, int direction) =>
            Enqueue(CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.MoveDetectorWindowStart, slot.StableId, direction);

        private void Enqueue(
            string commandId,
            string stableId = null,
            int direction = 0,
            CharacterTestRuntimeAiLabLogicElementKind kind = CharacterTestRuntimeAiLabLogicElementKind.None)
        {
            if (!string.IsNullOrEmpty(commandId))
                _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    commandId,
                    new CharacterTestRuntimeAiLabBehaviorCardElementCommand(stableId, direction, kind)));
        }

        internal bool TryAddDetectorToSelectedStage()
        {
            if (!_behaviorLogicMode
                || !_canEdit
                || !TryResolveSelectedStageContext(out string stageId))
                return false;
            Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStageDetector,
                stageId,
                kind: CharacterTestRuntimeAiLabLogicElementKind.Stage);
            return true;
        }

        internal bool HasSelectedStageContext =>
            TryResolveSelectedStageContext(out _);

        private bool TryResolveSelectedStageContext(out string stageId)
        {
            stageId = string.Empty;
            if (!string.IsNullOrEmpty(_selectedStableId)
                && _selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Stage)
            {
                stageId = _selectedStableId;
                return true;
            }

            if (!string.IsNullOrEmpty(_selectedStableId)
                && _selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Detector)
            {
                for (int i = 0; i < _boundDetectorCount; i++)
                {
                    CharacterTestRuntimeAiLabLogicDetectorViewModel detector = _boundDetectors[i];
                    if (detector == null
                        || !string.Equals(detector.DetectorId, _selectedStableId, StringComparison.Ordinal)
                        || detector.CardScope)
                    {
                        continue;
                    }
                    int stageIndex = detector.StageIndex;
                    if (stageIndex >= 0 && stageIndex < _boundStages.Length)
                    {
                        stageId = _boundStages[stageIndex]?.StageId ?? string.Empty;
                        return stageId.Length > 0;
                    }
                }
            }

            if (!string.IsNullOrEmpty(_selectedStableId)
                && _selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Transition)
            {
                for (int i = 0; i < _boundTransitionCount; i++)
                {
                    CharacterTestRuntimeAiLabLogicTransitionViewModel transition = _boundTransitions[i];
                    if (transition == null
                        || !string.Equals(transition.TransitionId, _selectedStableId, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    int stageIndex = transition.SourceStageIndex;
                    if (stageIndex >= 0 && stageIndex < _boundStages.Length)
                    {
                        stageId = _boundStages[stageIndex]?.StageId ?? string.Empty;
                        return stageId.Length > 0;
                    }
                }
            }
            for (int i = 0; i < _activeStageCount && i < _boundStages.Length; i++)
            {
                if (_boundStages[i]?.Current != true)
                    continue;
                stageId = _boundStages[i].StageId ?? string.Empty;
                return stageId.Length > 0;
            }
            if (_activeStageCount > 0 && _activeStageCount <= _boundStages.Length)
            {
                stageId = _boundStages[_activeStageCount - 1]?.StageId ?? string.Empty;
                return stageId.Length > 0;
            }
            return false;
        }

        internal bool TryRemoveSelectedLogicElement()
        {
            if (!_behaviorLogicMode
                || !_canEdit
                || _selectedKind == CharacterTestRuntimeAiLabLogicElementKind.None
                || string.IsNullOrEmpty(_selectedStableId))
                return false;
            Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.RemoveSelectedElement,
                _selectedStableId,
                kind: _selectedKind);
            return true;
        }

        internal bool TrySetSelectedStageKind(CharacterTestRuntimeAiLabCardStepKind kind)
            => TrySetStageKind(_selectedStableId, kind);

        internal bool TrySetStageKind(
            string stageId,
            CharacterTestRuntimeAiLabCardStepKind kind)
        {
            return _behaviorLogicMode
                && _canEdit
                && !string.IsNullOrEmpty(stageId)
                && (_commands?.SetSelectedStageKind(stageId, kind) ?? false);
        }

        internal bool TrySetSelectedStageDurationMode(CharacterTestRuntimeAiLabLogicDurationMode mode)
            => TrySetStageDurationMode(_selectedStableId, mode);

        internal bool TrySetStageDurationMode(
            string stageId,
            CharacterTestRuntimeAiLabLogicDurationMode mode)
        {
            return _behaviorLogicMode
                && _canEdit
                && !string.IsNullOrEmpty(stageId)
                && (_commands?.SetSelectedStageDurationMode(stageId, mode) ?? false);
        }

        internal bool TrySetSelectedMovementStrategy(CharacterTestRuntimeAiLabMovementStrategy strategy)
            => TrySetMovementStrategy(_selectedStableId, strategy);

        internal bool TrySetMovementStrategy(
            string stageId,
            CharacterTestRuntimeAiLabMovementStrategy strategy)
        {
            return _behaviorLogicMode
                && _canEdit
                && !string.IsNullOrEmpty(stageId)
                && (_commands?.SetSelectedMovementStrategy(stageId, strategy) ?? false);
        }

        internal bool TrySetSelectedMovementTargetKind(CharacterTestRuntimeAiLabMovementTargetKind targetKind)
            => TrySetMovementTargetKind(_selectedStableId, targetKind);

        internal bool TrySetMovementTargetKind(
            string stageId,
            CharacterTestRuntimeAiLabMovementTargetKind targetKind)
        {
            return _behaviorLogicMode
                && _canEdit
                && !string.IsNullOrEmpty(stageId)
                && (_commands?.SetSelectedMovementTargetKind(stageId, targetKind) ?? false);
        }

        internal bool TrySetMoveDistanceBand(string stageId, float minimum, float maximum)
        {
            return TrySetStageField(
                stageId,
                CharacterTestRuntimeAiLabStageFieldKind.MoveDistanceBand,
                new CharacterTestRuntimeAiLabStageFieldValue(
                    floatValue: minimum,
                    secondFloatValue: maximum));
        }

        internal bool TrySetMoveTargetPosition(string stageId, float x, float z)
        {
            return TrySetStageField(
                stageId,
                CharacterTestRuntimeAiLabStageFieldKind.MoveTargetPosition,
                new CharacterTestRuntimeAiLabStageFieldValue(
                    floatValue: x,
                    secondFloatValue: z));
        }

        internal bool TrySetMoveTargetEntityId(string stageId, int entityId)
        {
            return TrySetStageField(
                stageId,
                CharacterTestRuntimeAiLabStageFieldKind.MoveTargetEntityId,
                new CharacterTestRuntimeAiLabStageFieldValue(intValue: entityId));
        }

        internal bool TrySetSelectedStageDurationFrames(int frames)
            => TrySetStageDurationFrames(_selectedStableId, frames);

        internal bool TrySetStageDurationFrames(string stageId, int frames)
        {
            return TrySetStageField(
                stageId,
                CharacterTestRuntimeAiLabStageFieldKind.PlannedDurationFrames,
                new CharacterTestRuntimeAiLabStageFieldValue(intValue: Math.Max(1, frames)));
        }

        private bool TrySetStageField(
            string stageId,
            CharacterTestRuntimeAiLabStageFieldKind field,
            in CharacterTestRuntimeAiLabStageFieldValue value)
        {
            if (!_behaviorLogicMode
                || !_canEdit
                || string.IsNullOrEmpty(stageId)
                || _commands?.BeginStageFieldEdit(stageId, field, out long epoch) != true)
                return false;
            return _commands.CommitStageFieldEdit(epoch, stageId, field, value);
        }

        internal bool TrySetSelectedDetectorTrigger(CharacterTestRuntimeAiLabLogicDetectorTrigger trigger)
            => TrySetDetectorTrigger(_selectedStableId, trigger);

        internal bool TrySetDetectorTrigger(
            string detectorId,
            CharacterTestRuntimeAiLabLogicDetectorTrigger trigger)
        {
            DetectorSlot slot = FindDetectorSlot(detectorId);
            if (!_behaviorLogicMode || !_canEdit || slot == null)
                return false;
            ApplyDetectorTypedField(
                slot,
                CharacterTestRuntimeAiLabDetectorFieldKind.Trigger,
                new CharacterTestRuntimeAiLabDetectorFieldValue(trigger: trigger));
            return true;
        }

        internal bool TrySetSelectedDetectorScope(CharacterTestRuntimeAiLabLogicDetectorScope scope)
            => TrySetDetectorScope(_selectedStableId, scope);

        internal bool TrySetDetectorScope(
            string detectorId,
            CharacterTestRuntimeAiLabLogicDetectorScope scope)
        {
            DetectorSlot slot = FindDetectorSlot(detectorId);
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = slot?.ViewModel;
            if (!_behaviorLogicMode || !_canEdit || detector == null)
                return false;
            string owner = scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                ? ResolveDefaultOwningStageId(detector)
                : string.Empty;
            ApplyDetectorTypedField(
                slot,
                CharacterTestRuntimeAiLabDetectorFieldKind.Scope,
                new CharacterTestRuntimeAiLabDetectorFieldValue(stringValue: owner, scope: scope));
            return true;
        }

        internal bool TrySetSelectedDetectorWindowMode(
            CharacterTestRuntimeAiLabLogicDetectorWindowEndMode mode)
            => TrySetDetectorWindowMode(_selectedStableId, mode);

        internal bool TrySetDetectorWindowMode(
            string detectorId,
            CharacterTestRuntimeAiLabLogicDetectorWindowEndMode mode)
        {
            DetectorSlot slot = FindDetectorSlot(detectorId);
            if (!_behaviorLogicMode || !_canEdit || slot == null)
                return false;
            ApplyDetectorTypedField(
                slot,
                CharacterTestRuntimeAiLabDetectorFieldKind.WindowEndMode,
                new CharacterTestRuntimeAiLabDetectorFieldValue(windowEndMode: mode));
            return true;
        }

        internal bool TrySetSelectedTransitionPolicy(CharacterTestRuntimeAiLabLogicTransitionPolicy policy)
        {
            return TrySetTransitionField(
                _selectedStableId,
                CharacterTestRuntimeAiLabTransitionFieldKind.Policy,
                new CharacterTestRuntimeAiLabTransitionFieldValue(policy: policy));
        }

        internal bool TrySetSelectedTransitionTarget(string targetStageId)
        {
            return TrySetTransitionField(
                _selectedStableId,
                CharacterTestRuntimeAiLabTransitionFieldKind.TargetStage,
                new CharacterTestRuntimeAiLabTransitionFieldValue(stringValue: targetStageId));
        }

        internal bool TrySetTransitionPolicy(
            string transitionId,
            CharacterTestRuntimeAiLabLogicTransitionPolicy policy)
        {
            return TrySetTransitionField(
                transitionId,
                CharacterTestRuntimeAiLabTransitionFieldKind.Policy,
                new CharacterTestRuntimeAiLabTransitionFieldValue(policy: policy));
        }

        internal bool TrySetTransitionTarget(string transitionId, string targetStageId)
        {
            return TrySetTransitionField(
                transitionId,
                CharacterTestRuntimeAiLabTransitionFieldKind.TargetStage,
                new CharacterTestRuntimeAiLabTransitionFieldValue(stringValue: targetStageId));
        }

        private bool TrySetTransitionField(
            string transitionId,
            CharacterTestRuntimeAiLabTransitionFieldKind field,
            in CharacterTestRuntimeAiLabTransitionFieldValue value)
        {
            if (!_behaviorLogicMode
                || !_canEdit
                || string.IsNullOrEmpty(transitionId)
                || _commands?.BeginTransitionFieldEdit(transitionId, field, out long epoch) != true)
                return false;
            return _commands.CommitTransitionFieldEdit(
                epoch,
                transitionId,
                field,
                value);
        }

        internal bool TryCycleSelectedDetectorTrigger()
        {
            DetectorSlot slot = FindSelectedDetectorSlot();
            if (!_behaviorLogicMode || !_canEdit || slot == null)
                return false;
            CycleDetectorTrigger(slot);
            return true;
        }

        internal bool TryToggleSelectedDetectorScope()
        {
            DetectorSlot slot = FindSelectedDetectorSlot();
            if (!_behaviorLogicMode || !_canEdit || slot == null)
                return false;
            ToggleDetectorScope(slot);
            return true;
        }

        internal bool TryCycleSelectedDetectorWindowMode()
        {
            DetectorSlot slot = FindSelectedDetectorSlot();
            if (!_behaviorLogicMode || !_canEdit || slot == null)
                return false;
            CycleDetectorWindowMode(slot);
            return true;
        }

        internal bool TryCycleSelectedTransitionPolicy()
        {
            if (!_behaviorLogicMode
                || !_canEdit
                || _selectedKind != CharacterTestRuntimeAiLabLogicElementKind.Transition
                || string.IsNullOrEmpty(_selectedStableId))
                return false;
            Enqueue(CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleTransitionPolicy, _selectedStableId);
            return true;
        }

        internal bool TryCycleSelectedTransitionTarget()
        {
            if (!_behaviorLogicMode
                || !_canEdit
                || _selectedKind != CharacterTestRuntimeAiLabLogicElementKind.Transition
                || string.IsNullOrEmpty(_selectedStableId))
                return false;
            Enqueue(CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleTransitionTarget, _selectedStableId, 1);
            return true;
        }

        private DetectorSlot FindSelectedDetectorSlot()
        {
            if (_selectedKind != CharacterTestRuntimeAiLabLogicElementKind.Detector
                || string.IsNullOrEmpty(_selectedStableId))
                return null;
            int count = Math.Min(_activeDetectorCount, _detectorPool.Count);
            for (int i = 0; i < count; i++)
            {
                DetectorSlot slot = _detectorPool[i];
                if (string.Equals(slot?.StableId, _selectedStableId, StringComparison.Ordinal))
                    return slot;
            }
            return null;
        }

        private void ResolveSelectedToken(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            _selectedKind = viewModel.SelectedElementKind;
            int index = viewModel.SelectedElementIndex;
            _selectedStableId = string.Empty;
            if (_selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                && index >= 0 && index < stageCount)
                _selectedStableId = stages[index]?.StageId ?? string.Empty;
            else if (_selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Detector
                && index >= 0 && index < detectorCount)
                _selectedStableId = detectors[index]?.DetectorId ?? string.Empty;
            else if (_selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Transition
                && index >= 0 && index < transitionCount)
                _selectedStableId = transitions[index]?.TransitionId ?? string.Empty;
        }
    }
}
