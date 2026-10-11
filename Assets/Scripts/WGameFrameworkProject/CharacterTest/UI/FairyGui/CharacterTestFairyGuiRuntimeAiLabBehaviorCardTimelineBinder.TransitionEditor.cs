using System;
using System.Globalization;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private const string TransitionResultMenuComponentName =
            "RuntimeAiLabStageTypedOptionMenu";
        private const float TransitionResultMenuWidth = 276f;
        private const float TransitionResultMenuRowHeight = 42f;
        private const int TransitionTargetPageSize = 4;

        private void ConfigureTransitionEditorHandlers(TransitionSlot slot)
        {
            BindTransitionGraphInput(slot, "inputLabel", CharacterTestRuntimeAiLabTransitionFieldKind.Label);
            BindTransitionGraphInput(slot, "inputPriority", CharacterTestRuntimeAiLabTransitionFieldKind.Priority);
            BindTransitionGraphInput(slot, "inputSource", CharacterTestRuntimeAiLabTransitionFieldKind.SourceStage);
            BindTransitionGraphInput(slot, "inputTarget", CharacterTestRuntimeAiLabTransitionFieldKind.TargetStage);
            BindTransitionGraphInput(slot, "inputEndReason", CharacterTestRuntimeAiLabTransitionFieldKind.EndReason);
            BindSlotButton(slot.Visual, "btnPolicy", () => RunGraphEditorAction(() => CycleTransitionPolicy(slot)));
            BindSlotButton(slot.Visual, "btnTargetPrevious", () => RunGraphEditorAction(() => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleTransitionTarget,
                slot.StableId,
                -1)));
            BindSlotButton(slot.Visual, "btnTargetNext", () => RunGraphEditorAction(() => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleTransitionTarget,
                slot.StableId,
                1)));
            BindSlotButton(slot.Visual, "btnDelete", () => RunGraphEditorAction(() => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.RemoveSelectedElement,
                slot.StableId,
                0,
                CharacterTestRuntimeAiLabLogicElementKind.Transition)));
        }

        private void BindTransitionEditorFields(
            TransitionSlot slot,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            Fgui.GComponent chip = slot.Visual;
            bool selected = transition.Selected;
            SetTransitionEditorVisibility(chip, selected, transition.EndsCard);
            if (!selected)
                return;
            bool editingThisTransition = _graphFieldEditEpoch != 0L
                && _graphFieldEditDomain == GraphEditorDomainTransition
                && string.Equals(_graphFieldEditStableId, transition.TransitionId, StringComparison.Ordinal);
            if (!editingThisTransition)
            {
                SetInputText(chip, "inputLabel", transition.Label);
                SetInputText(chip, "inputPriority", transition.Priority.ToString(CultureInfo.InvariantCulture));
                SetInputText(chip, "inputSource", transition.SourceStageId);
                SetInputText(chip, "inputTarget", transition.TargetStageId);
                SetInputText(chip, "inputEndReason", transition.EndReason);
                SetGraphFieldError(chip, string.Empty);
            }
            SetButtonTitle(chip, "btnPolicy", transition.Policy.ToString());
            SetButtonTitle(chip, "btnTargetPrevious", "← 上一目标");
            SetButtonTitle(chip, "btnTargetNext", "下一目标 →");
            SetButtonTitle(chip, "btnDelete", "删除跳转");
            SetInputEnabled(chip, "inputLabel", _canEdit);
            SetInputEnabled(chip, "inputPriority", _canEdit);
            SetInputEnabled(chip, "inputSource", _canEdit && transition.SourceEditable);
            SetInputEnabled(chip, "inputTarget", _canEdit);
            SetInputEnabled(chip, "inputEndReason", _canEdit && transition.EndsCard);
        }

        private static void SetTransitionEditorVisibility(
            Fgui.GComponent chip,
            bool selected,
            bool endsCard)
        {
            SetObjectVisible(chip, "inputLabel", selected);
            SetObjectVisible(chip, "inputPriority", selected);
            SetObjectVisible(chip, "inputSource", selected);
            SetObjectVisible(chip, "inputTarget", selected);
            SetObjectVisible(chip, "inputEndReason", selected && endsCard);
            if (!selected)
                SetObjectVisible(chip, "fieldError", false);
        }

        private void CycleTransitionPolicy(TransitionSlot slot)
        {
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition = slot.ViewModel;
            if (transition == null)
                return;
            var policy = (CharacterTestRuntimeAiLabLogicTransitionPolicy)(((int)transition.Policy + 1) % 3);
            if (_commands?.BeginTransitionFieldEdit(
                    slot.StableId,
                    CharacterTestRuntimeAiLabTransitionFieldKind.Policy,
                    out long epoch) != true)
            {
                SetGraphFieldError(slot.Visual, _commands?.LastMessage);
                return;
            }
            var value = new CharacterTestRuntimeAiLabTransitionFieldValue(policy: policy);
            if (_commands.CommitTransitionFieldEdit(
                    epoch,
                    slot.StableId,
                    CharacterTestRuntimeAiLabTransitionFieldKind.Policy,
                    value))
                SetGraphFieldError(slot.Visual, string.Empty);
            else
                SetGraphFieldError(slot.Visual, _commands.LastMessage);
        }

        private void EnsureTransitionResultMenu()
        {
            if (_transitionResultMenu != null || _root == null)
                return;
            _transitionResultMenu = CreateRequiredComponent(TransitionResultMenuComponentName);
            _transitionResultMenu.name = "runtimeTransitionResultMenu";
            _transitionResultMenu.visible = false;
            _transitionResultMenu.touchable = true;
            _transitionResultMenu.sortingOrder = 300;
            _transitionResultMenu.SetSize(TransitionResultMenuWidth, 222f);
            _transitionResultMenu.onTouchBegin.Set(context => context.StopPropagation());
            _root.AddChild(_transitionResultMenu);

            for (int i = 0; i < 7; i++)
            {
                Fgui.GButton option = _transitionResultMenu.GetChild("option" + i)?.asButton;
                if (option == null)
                    continue;
                option.visible = false;
                option.SetSize(TransitionResultMenuWidth - 12f, 38f);
                option.titleFontSize = 17;
                int optionIndex = i;
                BindSlotButton(
                    _transitionResultMenu,
                    "option" + i,
                    () => ApplyTransitionResultOption(optionIndex));
            }
        }

        private void ToggleTransitionResultMenu(DetectorSlot slot)
        {
            if (!_behaviorLogicMode
                || !_canEdit
                || slot?.ViewModel == null)
            {
                return;
            }
            EnsureTransitionResultMenu();
            if (_transitionResultMenu == null)
                return;
            if (_transitionResultMenu.visible
                && string.Equals(
                    _transitionResultDetectorId,
                    slot.StableId,
                    StringComparison.Ordinal))
            {
                HideTransitionResultMenu();
                return;
            }

            _transitionResultDetectorId = slot.StableId ?? string.Empty;
            _transitionResultTargetPickerMode = false;
            _transitionResultTargetPage = 0;
            _transitionResultMenu.visible = true;
            ConfigureTransitionResultMenu(slot);
            PositionTransitionResultMenu(slot);
            if (_selectedKind != CharacterTestRuntimeAiLabLogicElementKind.Detector
                || !string.Equals(_selectedStableId, slot.StableId, StringComparison.Ordinal))
            {
                Enqueue(
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectDetector,
                    slot.StableId,
                    0,
                    CharacterTestRuntimeAiLabLogicElementKind.Detector);
            }
        }

        private void RefreshTransitionResultMenuAnchor()
        {
            if (_transitionResultMenu?.visible != true
                || string.IsNullOrEmpty(_transitionResultDetectorId))
            {
                return;
            }
            DetectorSlot slot = FindDetectorSlot(_transitionResultDetectorId);
            if (slot?.Visual?.visible != true
                || slot.ViewModel == null)
            {
                HideTransitionResultMenu();
                return;
            }
            ConfigureTransitionResultMenu(slot);
            PositionTransitionResultMenu(slot);
        }

        private void PositionTransitionResultMenu(DetectorSlot slot)
        {
            Fgui.GObject anchor = slot?.Visual?.GetChild("btnAddTransition");
            if (_transitionResultMenu == null
                || anchor == null
                || !anchor.visible
                || _root == null)
            {
                return;
            }
            Vector2 belowGlobal = anchor.LocalToGlobal(new Vector2(0f, anchor.height + 6f));
            Vector2 below = _root.GlobalToLocal(belowGlobal);
            float x = Mathf.Clamp(
                below.x + anchor.width - TransitionResultMenuWidth,
                8f,
                Math.Max(8f, _root.width - TransitionResultMenuWidth - 8f));
            float y = below.y;
            float menuHeight = _transitionResultMenu.height;
            if (y + menuHeight > _root.height - 8f)
            {
                Vector2 aboveGlobal = anchor.LocalToGlobal(Vector2.zero);
                Vector2 above = _root.GlobalToLocal(aboveGlobal);
                y = above.y - menuHeight - 6f;
            }
            _transitionResultMenu.SetXY(x, Mathf.Max(8f, y));
        }

        private bool HasNextStageForDetector(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            int targetIndex = detector?.StageIndex >= 0
                ? detector.StageIndex + 1
                : ResolveEntryStageIndex();
            return targetIndex >= 0 && targetIndex < _activeStageCount;
        }

        private int ResolveEntryStageIndex()
        {
            for (int i = 0; i < _activeStageCount && i < _boundStages.Length; i++)
            {
                if (_boundStages[i]?.Entry == true)
                    return i;
            }
            return _activeStageCount > 0 ? 0 : -1;
        }

        private void ConfigureTransitionResultMenu(DetectorSlot slot)
        {
            if (_transitionResultMenu == null || slot?.ViewModel == null)
                return;
            for (int i = 0; i < _transitionResultTargetOption.Length; i++)
                _transitionResultTargetOption[i] = -1;
            if (_transitionResultTargetPickerMode)
            {
                ConfigureTransitionTargetPicker();
                return;
            }

            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = slot.ViewModel;
            bool hasAutomaticTarget = HasNextStageForDetector(detector);
            string automaticTitle = detector.CardScope
                ? "进入入口阶段"
                : "进入下一阶段";
            ConfigureTransitionResultOption(
                0,
                0,
                automaticTitle,
                true,
                hasAutomaticTarget,
                hasAutomaticTarget
                    ? "检测命中后进入默认目标阶段。"
                    : "当前没有可用的默认目标；请先添加阶段。");
            ConfigureTransitionResultOption(
                1,
                1,
                "停留当前阶段",
                true,
                detector.StageIndex >= 0 && detector.StageIndex < _activeStageCount,
                "检测命中后重新进入当前阶段，形成自循环。");
            ConfigureTransitionResultOption(
                2,
                2,
                "选择其他阶段…",
                true,
                _activeStageCount > 0,
                "明确选择任意目标阶段，可前进也可回退。");
            ConfigureTransitionResultOption(
                3,
                3,
                "结束行为卡",
                true,
                true,
                "结束当前行为卡并返回行为组仲裁。");
            ConfigureTransitionResultOption(
                4,
                4,
                "重新决策",
                true,
                true,
                "结束当前行为卡并立即请求重新决策。");
            HideTransitionResultOptionsFrom(5);
            ResizeTransitionResultMenu(5);
        }

        private void ConfigureTransitionTargetPicker()
        {
            int pageCount = Math.Max(
                1,
                (_activeStageCount + TransitionTargetPageSize - 1)
                    / TransitionTargetPageSize);
            _transitionResultTargetPage = Mathf.Clamp(
                _transitionResultTargetPage,
                0,
                pageCount - 1);
            int firstStage = _transitionResultTargetPage * TransitionTargetPageSize;
            int optionIndex = 0;
            int row = 0;
            for (; optionIndex < TransitionTargetPageSize; optionIndex++)
            {
                int stageIndex = firstStage + optionIndex;
                if (stageIndex >= _activeStageCount)
                    break;
                CharacterTestRuntimeAiLabLogicStageViewModel stage =
                    stageIndex < _boundStages.Length ? _boundStages[stageIndex] : null;
                _transitionResultTargetOption[optionIndex] = stageIndex;
                ConfigureTransitionResultOption(
                    optionIndex,
                    row++,
                    (stage?.Entry == true ? "入口 · " : string.Empty)
                        + (stage?.Title ?? "阶段 " + (stageIndex + 1)),
                    true,
                    stage != null,
                    "将检测结果连接到这个阶段。");
            }
            for (int i = optionIndex; i < TransitionTargetPageSize; i++)
                ConfigureTransitionResultOption(i, 0, string.Empty, false, false, string.Empty);

            bool hasPreviousPage = _transitionResultTargetPage > 0;
            _transitionResultTargetOption[4] = -2;
            ConfigureTransitionResultOption(
                4,
                row,
                "← 上一页",
                hasPreviousPage,
                hasPreviousPage,
                string.Empty);
            if (hasPreviousPage)
                row++;

            bool hasNextPage = _transitionResultTargetPage + 1 < pageCount;
            _transitionResultTargetOption[5] = -3;
            ConfigureTransitionResultOption(
                5,
                row,
                "下一页 →",
                hasNextPage,
                hasNextPage,
                string.Empty);
            if (hasNextPage)
                row++;

            _transitionResultTargetOption[6] = -4;
            ConfigureTransitionResultOption(
                6,
                row++,
                "← 返回结果类型",
                true,
                true,
                string.Empty);
            ResizeTransitionResultMenu(row);
        }

        private void ConfigureTransitionResultOption(
            int optionIndex,
            int row,
            string title,
            bool visible,
            bool enabled,
            string tooltip)
        {
            Fgui.GButton option = _transitionResultMenu?.GetChild("option" + optionIndex)?.asButton;
            if (option == null)
                return;
            option.visible = visible;
            option.enabled = enabled;
            option.title = title ?? string.Empty;
            BindTooltip(option, tooltip);
            if (visible)
                option.SetXY(6f, 6f + row * TransitionResultMenuRowHeight);
        }

        private void HideTransitionResultOptionsFrom(int firstOption)
        {
            for (int i = firstOption; i < 7; i++)
                ConfigureTransitionResultOption(i, 0, string.Empty, false, false, string.Empty);
        }

        private void ResizeTransitionResultMenu(int rowCount)
        {
            float height = 12f + Math.Max(1, rowCount) * TransitionResultMenuRowHeight;
            _transitionResultMenu?.SetSize(TransitionResultMenuWidth, height);
            _transitionResultMenu?.GetChild("background")?.SetSize(
                TransitionResultMenuWidth,
                height);
        }

        private void ApplyTransitionResultOption(int optionIndex)
        {
            if (_transitionResultMenu?.visible != true
                || string.IsNullOrEmpty(_transitionResultDetectorId))
            {
                return;
            }
            DetectorSlot slot = FindDetectorSlot(_transitionResultDetectorId);
            if (_transitionResultTargetPickerMode)
            {
                int target = optionIndex >= 0 && optionIndex < _transitionResultTargetOption.Length
                    ? _transitionResultTargetOption[optionIndex]
                    : -1;
                if (target == -2)
                {
                    _transitionResultTargetPage--;
                    ConfigureTransitionResultMenu(slot);
                    PositionTransitionResultMenu(slot);
                    return;
                }
                if (target == -3)
                {
                    _transitionResultTargetPage++;
                    ConfigureTransitionResultMenu(slot);
                    PositionTransitionResultMenu(slot);
                    return;
                }
                if (target == -4)
                {
                    _transitionResultTargetPickerMode = false;
                    ConfigureTransitionResultMenu(slot);
                    PositionTransitionResultMenu(slot);
                    return;
                }
                if (target < 0 || target >= _activeStageCount)
                    return;
                string targetDetectorId = _transitionResultDetectorId;
                HideTransitionResultMenu();
                Enqueue(
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddTransitionToStage,
                    targetDetectorId,
                    target,
                    CharacterTestRuntimeAiLabLogicElementKind.Detector);
                return;
            }

            if (optionIndex == 2)
            {
                _transitionResultTargetPickerMode = true;
                _transitionResultTargetPage = 0;
                ConfigureTransitionResultMenu(slot);
                PositionTransitionResultMenu(slot);
                return;
            }
            if (optionIndex == 1)
            {
                int currentStage = slot?.ViewModel?.StageIndex ?? -1;
                if (currentStage < 0 || currentStage >= _activeStageCount)
                    return;
                string currentStageDetectorId = _transitionResultDetectorId;
                HideTransitionResultMenu();
                Enqueue(
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddTransitionToStage,
                    currentStageDetectorId,
                    currentStage,
                    CharacterTestRuntimeAiLabLogicElementKind.Detector);
                return;
            }

            var preset = optionIndex switch
            {
                0 => CharacterTestRuntimeAiLabTransitionResultPreset.NextStage,
                3 => CharacterTestRuntimeAiLabTransitionResultPreset.EndBehaviorCard,
                4 => CharacterTestRuntimeAiLabTransitionResultPreset.Replan,
                _ => CharacterTestRuntimeAiLabTransitionResultPreset.Automatic
            };
            if (preset == CharacterTestRuntimeAiLabTransitionResultPreset.NextStage
                && !HasNextStageForDetector(slot?.ViewModel))
            {
                return;
            }
            string presetDetectorId = _transitionResultDetectorId;
            HideTransitionResultMenu();
            Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddTransition,
                presetDetectorId,
                (int)preset,
                CharacterTestRuntimeAiLabLogicElementKind.Detector);
        }

        private void HideTransitionResultMenu()
        {
            if (_transitionResultMenu != null)
                _transitionResultMenu.visible = false;
            _transitionResultDetectorId = string.Empty;
            _transitionResultTargetPickerMode = false;
            _transitionResultTargetPage = 0;
        }
    }
}
