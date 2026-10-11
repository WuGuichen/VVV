using System;
using System.Globalization;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private void ConfigureDetectorEditorHandlers(DetectorSlot slot)
        {
            BindDetectorGraphInput(slot, "inputTitle", CharacterTestRuntimeAiLabDetectorFieldKind.Title);
            BindDetectorGraphInput(slot, "inputOwner", CharacterTestRuntimeAiLabDetectorFieldKind.OwningStage);
            BindDetectorGraphInput(slot, "inputPriority", CharacterTestRuntimeAiLabDetectorFieldKind.Priority);
            BindDetectorGraphInput(slot, "inputStart", CharacterTestRuntimeAiLabDetectorFieldKind.ActivationStartFrame);
            BindDetectorGraphInput(slot, "inputDuration", CharacterTestRuntimeAiLabDetectorFieldKind.ActivationDurationFrames);
            BindConditionGraphInput(slot, "inputConditionValue", CharacterTestRuntimeAiLabConditionFieldKind.Value);
            BindConditionGraphInput(slot, "inputConditionMax", CharacterTestRuntimeAiLabConditionFieldKind.Range);
            BindConditionGraphInput(slot, "inputConditionAbility", CharacterTestRuntimeAiLabConditionFieldKind.AbilityId);
            BindConditionGraphInput(slot, "inputConditionAction", CharacterTestRuntimeAiLabConditionFieldKind.CharacterActionId);
            BindSlotButton(slot.Visual, "btnScope", () => RunGraphEditorAction(() => ToggleDetectorScope(slot)));
            BindSlotButton(slot.Visual, "btnTrigger", () => RunGraphEditorAction(() => CycleDetectorTrigger(slot)));
            BindSlotButton(slot.Visual, "btnWindowMode", () => RunGraphEditorAction(() => CycleDetectorWindowMode(slot)));
            BindSlotButton(slot.Visual, "btnFact", () => RunGraphEditorAction(() => CycleConditionFact(slot)));
            BindSlotButton(slot.Visual, "btnConditionPrevious", () => RunGraphEditorAction(() => MoveConditionSelection(slot, -1)));
            BindSlotButton(slot.Visual, "btnConditionNext", () => RunGraphEditorAction(() => MoveConditionSelection(slot, 1)));
            BindSlotButton(slot.Visual, "btnConditionFact", () => RunGraphEditorAction(() => CycleConditionFact(slot)));
            BindSlotButton(slot.Visual, "btnConditionOperator", () => RunGraphEditorAction(() => CycleConditionOperator(slot)));
            BindSlotButton(slot.Visual, "btnConditionBool", () => RunGraphEditorAction(() => ToggleConditionBool(slot)));
            BindSlotButton(slot.Visual, "btnAddCondition", () => RunGraphEditorAction(() => AddCondition(slot)));
            BindSlotButton(slot.Visual, "btnDeleteCondition", () => RunGraphEditorAction(() => DeleteCondition(slot)));
        }

        private void BindDetectorEditorFields(
            DetectorSlot slot,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            Fgui.GComponent band = slot.Visual;
            bool selected = detector.Selected;
            SetDetectorEditorVisibility(band, selected);
            if (!selected)
                return;

            if (!string.Equals(_conditionDetectorId, detector.DetectorId, StringComparison.Ordinal))
            {
                _conditionDetectorId = detector.DetectorId ?? string.Empty;
                _conditionIndex = 0;
            }
            int conditionCount = Math.Min(detector.ConditionCount, detector.Conditions?.Length ?? 0);
            if (conditionCount == 0)
                _conditionIndex = 0;
            else
                _conditionIndex = Math.Max(0, Math.Min(_conditionIndex, conditionCount - 1));

            bool editingThisDetector = _graphFieldEditEpoch != 0L
                && string.Equals(_graphFieldEditStableId, detector.DetectorId, StringComparison.Ordinal);
            if (!editingThisDetector)
            {
                bool signalReason = detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed
                    || detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionRejected;
                SetInputText(
                    band,
                    "inputTitle",
                    signalReason ? detector.ActionSignalReasonCode : detector.Title);
                SetInputText(band, "inputOwner", detector.OwningStageId);
                SetInputText(band, "inputPriority", detector.Priority.ToString(CultureInfo.InvariantCulture));
                SetInputText(band, "inputStart", detector.StartFrame.ToString(CultureInfo.InvariantCulture));
                SetInputText(band, "inputDuration", detector.DurationFrames.ToString(CultureInfo.InvariantCulture));
                SetGraphFieldError(band, string.Empty);
            }
            SetButtonTitle(band, "btnScope", FormatDetectorScope(detector.Scope));
            SetButtonTitle(band, "btnTrigger", FormatDetectorTrigger(detector));
            SetButtonTitle(band, "btnWindowMode", FormatDetectorWindow(detector.WindowEndMode));
            SetInputEnabled(band, "inputTitle", _canEdit);
            SetInputEnabled(band, "inputOwner", _canEdit && detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage);
            SetInputEnabled(band, "inputPriority", _canEdit);
            SetInputEnabled(
                band,
                "inputStart",
                _canEdit
                    && (detector.Trigger != CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame
                        || detector.LocalFrameAnchor ==
                            CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame));
            SetInputEnabled(
                band,
                "inputDuration",
                _canEdit && detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed);

            bool conditionEditor = detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge;
            BindSelectedCondition(band, detector, conditionCount, editingThisDetector && _graphFieldEditDomain == GraphEditorDomainCondition);
            SetObjectVisible(band, "conditionStatus", conditionEditor);
            SetObjectVisible(band, "btnAddCondition", conditionEditor);
            SetButtonTitle(band, "btnAddCondition", "添加条件");
            SetButtonEnabled(band, "btnAddCondition", conditionEditor && _canEdit);
            SetObjectVisible(band, "btnDeleteCondition", conditionEditor && conditionCount > 0);
            SetButtonTitle(band, "btnDeleteCondition", "删除条件");
            SetButtonEnabled(band, "btnDeleteCondition", conditionEditor && conditionCount > 0 && _canEdit);
            SetObjectVisible(band, "inputOwner", detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage);
            SetObjectVisible(
                band,
                "inputDuration",
                detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed);
        }

        private void BindSelectedCondition(
            Fgui.GComponent band,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            int conditionCount,
            bool editingCondition)
        {
            bool visible = detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge
                && conditionCount > 0;
            SetObjectVisible(band, "btnConditionPrevious", visible);
            SetObjectVisible(band, "btnConditionNext", visible);
            SetObjectVisible(band, "btnConditionFact", visible);
            SetObjectVisible(band, "btnConditionOperator", visible);
            SetButtonEnabled(band, "btnConditionPrevious", visible && _conditionIndex > 0);
            SetButtonEnabled(band, "btnConditionNext", visible && _conditionIndex + 1 < conditionCount);
            SetButtonEnabled(band, "btnConditionFact", visible && _canEdit);
            SetButtonEnabled(band, "btnConditionOperator", visible && _canEdit);
            SetText(
                band,
                "conditionStatus",
                detector.Trigger != CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge
                    ? string.Empty
                    : conditionCount == 0
                        ? "暂无条件"
                        : "条件 " + (_conditionIndex + 1) + "/" + conditionCount);
            if (!visible)
            {
                SetConditionValueVisibility(band, false, false, false, false, false);
                return;
            }

            CharacterTestRuntimeAiLabLogicConditionViewModel condition = detector.Conditions[_conditionIndex];
            bool numeric = IsNumericConditionFact(condition.Fact);
            bool range = numeric && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between;
            bool ability = condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable;
            bool action = condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction;
            bool boolean = !numeric && !ability && !action;
            SetButtonTitle(band, "btnConditionFact", FormatConditionFact(condition.Fact));
            SetButtonTitle(band, "btnConditionOperator", FormatConditionOperator(condition.Operator));
            SetButtonTitle(band, "btnConditionBool", condition.BoolValue ? "是" : "否");
            if (!editingCondition)
            {
                SetInputText(band, "inputConditionValue", condition.Value.ToString("0.###", CultureInfo.InvariantCulture));
                SetInputText(band, "inputConditionMax", condition.MaxValue.ToString("0.###", CultureInfo.InvariantCulture));
                SetInputText(band, "inputConditionAbility", condition.AbilityId.ToString(CultureInfo.InvariantCulture));
                SetInputText(
                    band,
                    "inputConditionAction",
                    condition.CharacterActionId.IsValid ? condition.CharacterActionId.ToString() : string.Empty);
            }
            SetConditionValueVisibility(band, boolean, numeric, range, ability, action);
            SetButtonEnabled(band, "btnConditionBool", boolean && _canEdit);
            SetInputEnabled(band, "inputConditionValue", numeric && _canEdit);
            SetInputEnabled(band, "inputConditionMax", range && _canEdit);
            SetInputEnabled(band, "inputConditionAbility", ability && _canEdit);
            SetInputEnabled(band, "inputConditionAction", action && _canEdit);
        }

        private static void SetDetectorEditorVisibility(Fgui.GComponent band, bool visible)
        {
            SetObjectVisible(band, "inputTitle", visible);
            SetObjectVisible(band, "btnScope", visible);
            SetObjectVisible(band, "inputOwner", visible);
            SetObjectVisible(band, "inputPriority", visible);
            SetObjectVisible(band, "inputStart", visible);
            SetObjectVisible(band, "inputDuration", visible);
            if (visible)
                return;
            SetObjectVisible(band, "conditionStatus", false);
            SetObjectVisible(band, "btnConditionPrevious", false);
            SetObjectVisible(band, "btnConditionNext", false);
            SetObjectVisible(band, "btnConditionFact", false);
            SetObjectVisible(band, "btnConditionOperator", false);
            SetConditionValueVisibility(band, false, false, false, false, false);
            SetObjectVisible(band, "btnAddCondition", false);
            SetObjectVisible(band, "btnDeleteCondition", false);
            SetObjectVisible(band, "fieldError", false);
        }

        private static void SetConditionValueVisibility(
            Fgui.GComponent band,
            bool boolValue,
            bool value,
            bool maximum,
            bool ability,
            bool action)
        {
            SetObjectVisible(band, "btnConditionBool", boolValue);
            SetObjectVisible(band, "inputConditionValue", value);
            SetObjectVisible(band, "inputConditionMax", maximum);
            SetObjectVisible(band, "inputConditionAbility", ability);
            SetObjectVisible(band, "inputConditionAction", action);
        }

        private void RunGraphEditorAction(Action action)
        {
            if (!TryCommitActiveGraphFieldBeforeNavigation())
                return;
            action?.Invoke();
        }

        internal bool TryRepairSelectedLogicElement()
        {
            if (!_behaviorLogicMode
                || !_canEdit
                || _selectedKind != CharacterTestRuntimeAiLabLogicElementKind.Detector
                || string.IsNullOrEmpty(_selectedStableId))
                return false;

            int count = Math.Min(_activeDetectorCount, _detectorPool.Count);
            for (int i = 0; i < count; i++)
            {
                DetectorSlot slot = _detectorPool[i];
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = slot?.ViewModel;
                if (detector == null
                    || !string.Equals(slot.StableId, _selectedStableId, StringComparison.Ordinal)
                    || detector.Trigger != CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge
                    || detector.ConditionCount != 0)
                    continue;

                RunGraphEditorAction(() => AddCondition(slot));
                return true;
            }
            return false;
        }

        private void ToggleDetectorScope(DetectorSlot slot)
        {
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = slot.ViewModel;
            if (detector == null)
                return;
            CharacterTestRuntimeAiLabLogicDetectorScope scope = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card
                ? CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                : CharacterTestRuntimeAiLabLogicDetectorScope.Card;
            string owner = scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                ? ResolveDefaultOwningStageId(detector)
                : string.Empty;
            ApplyDetectorTypedField(
                slot,
                CharacterTestRuntimeAiLabDetectorFieldKind.Scope,
                new CharacterTestRuntimeAiLabDetectorFieldValue(stringValue: owner, scope: scope));
        }

        private void CycleDetectorTrigger(DetectorSlot slot)
        {
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = slot.ViewModel;
            if (detector == null)
                return;
            CharacterTestRuntimeAiLabLogicStageViewModel owner = ResolveDetectorOwnerStage(detector);
            bool finitePrimaryAction = CanBindPrimaryActionEnd(owner);
            bool ownsActionInstance = owner != null
                && (owner.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    || owner.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility);
            CharacterTestRuntimeAiLabLogicDetectorTrigger trigger;
            switch (detector.Trigger)
            {
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge:
                    trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame;
                    break;
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame:
                    if (detector.LocalFrameAnchor ==
                            CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame
                        && finitePrimaryAction)
                    {
                        // ActionCompleted is an adapter command. Native v5 mutation converts it to
                        // LocalFrame.PrimaryActionEnd and never persists a new unfiltered signal.
                        trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted;
                    }
                    else
                    {
                        trigger = ownsActionInstance
                            ? CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed
                            : CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge;
                    }
                    break;
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted:
                    trigger = ownsActionInstance
                        ? CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed
                        : CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge;
                    break;
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                    trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionRejected;
                    break;
                default:
                    trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge;
                    break;
            }
            ApplyDetectorTypedField(
                slot,
                CharacterTestRuntimeAiLabDetectorFieldKind.Trigger,
                new CharacterTestRuntimeAiLabDetectorFieldValue(trigger: trigger));
        }

        private CharacterTestRuntimeAiLabLogicStageViewModel ResolveDetectorOwnerStage(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            for (int i = 0; detector != null && i < _activeStageCount; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = _boundStages[i];
                if (stage != null
                    && string.Equals(stage.StageId, detector.OwningStageId, StringComparison.Ordinal))
                {
                    return stage;
                }
            }
            return null;
        }

        private static bool CanBindPrimaryActionEnd(
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (stage == null)
                return false;
            if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait)
                return stage.WaitFrames > 0;
            return (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility)
                && Math.Max(stage.ActualDurationFrames, stage.DurationFrames) > 0;
        }

        private void CycleDetectorWindowMode(DetectorSlot slot)
        {
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = slot.ViewModel;
            if (detector == null)
                return;
            CharacterTestRuntimeAiLabLogicDetectorWindowEndMode mode;
            if (detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)
            {
                mode = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    ? CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd
                    : CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite;
            }
            else if (detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd)
                mode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite;
            else
                mode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed;
            ApplyDetectorTypedField(
                slot,
                CharacterTestRuntimeAiLabDetectorFieldKind.WindowEndMode,
                new CharacterTestRuntimeAiLabDetectorFieldValue(windowEndMode: mode));
        }

        private void ApplyDetectorTypedField(
            DetectorSlot slot,
            CharacterTestRuntimeAiLabDetectorFieldKind field,
            in CharacterTestRuntimeAiLabDetectorFieldValue value)
        {
            if (_commands?.BeginDetectorFieldEdit(slot.StableId, field, out long epoch) != true)
            {
                SetGraphFieldError(slot.Visual, _commands?.LastMessage);
                return;
            }
            if (_commands.CommitDetectorFieldEdit(epoch, slot.StableId, field, value))
                SetGraphFieldError(slot.Visual, string.Empty);
            else
                SetGraphFieldError(slot.Visual, _commands.LastMessage);
        }

        private void MoveConditionSelection(DetectorSlot slot, int direction)
        {
            int count = Math.Min(slot.ViewModel?.ConditionCount ?? 0, slot.ViewModel?.Conditions?.Length ?? 0);
            if (count <= 0)
                return;
            _conditionIndex = Math.Max(0, Math.Min(_conditionIndex + direction, count - 1));
            BindDetectorEditorFields(slot, slot.ViewModel);
        }

        private void CycleConditionFact(DetectorSlot slot)
        {
            if (!TryGetSelectedCondition(slot, out CharacterTestRuntimeAiLabLogicConditionViewModel condition))
                return;
            var fact = (CharacterTestRuntimeAiLabCardConditionFact)(((int)condition.Fact + 1) % 11);
            ApplyConditionField(
                slot,
                condition,
                CharacterTestRuntimeAiLabConditionFieldKind.Fact,
                new CharacterTestRuntimeAiLabConditionFieldValue(fact: fact));
        }

        private void CycleConditionOperator(DetectorSlot slot)
        {
            if (!TryGetSelectedCondition(slot, out CharacterTestRuntimeAiLabLogicConditionViewModel condition))
                return;
            int count = IsNumericConditionFact(condition.Fact) ? 5 : 2;
            var conditionOperator = (CharacterTestRuntimeAiLabCardConditionOperator)(((int)condition.Operator + 1) % count);
            ApplyConditionField(
                slot,
                condition,
                CharacterTestRuntimeAiLabConditionFieldKind.Operator,
                new CharacterTestRuntimeAiLabConditionFieldValue(conditionOperator: conditionOperator));
        }

        private void ToggleConditionBool(DetectorSlot slot)
        {
            if (!TryGetSelectedCondition(slot, out CharacterTestRuntimeAiLabLogicConditionViewModel condition))
                return;
            ApplyConditionField(
                slot,
                condition,
                CharacterTestRuntimeAiLabConditionFieldKind.BoolValue,
                new CharacterTestRuntimeAiLabConditionFieldValue(boolValue: !condition.BoolValue));
        }

        private void ApplyConditionField(
            DetectorSlot slot,
            CharacterTestRuntimeAiLabLogicConditionViewModel condition,
            CharacterTestRuntimeAiLabConditionFieldKind field,
            in CharacterTestRuntimeAiLabConditionFieldValue value)
        {
            if (_commands?.ApplyConditionField(slot.StableId, condition.ConditionId, field, value) == true)
                SetGraphFieldError(slot.Visual, string.Empty);
            else
                SetGraphFieldError(slot.Visual, _commands?.LastMessage);
        }

        private void AddCondition(DetectorSlot slot)
        {
            int count = Math.Min(slot.ViewModel?.ConditionCount ?? 0, slot.ViewModel?.Conditions?.Length ?? 0);
            _conditionIndex = count;
            if (_commands?.AddDetectorCondition(slot.StableId) == true)
                SetGraphFieldError(slot.Visual, string.Empty);
            else
            {
                _conditionIndex = Math.Max(0, count - 1);
                SetGraphFieldError(slot.Visual, _commands?.LastMessage);
            }
        }

        private void DeleteCondition(DetectorSlot slot)
        {
            if (!TryGetSelectedCondition(slot, out CharacterTestRuntimeAiLabLogicConditionViewModel condition))
                return;
            _conditionIndex = Math.Max(0, _conditionIndex - 1);
            if (_commands?.DeleteDetectorCondition(slot.StableId, condition.ConditionId) == true)
                SetGraphFieldError(slot.Visual, string.Empty);
            else
                SetGraphFieldError(slot.Visual, _commands?.LastMessage);
        }

        private bool TryGetSelectedCondition(
            DetectorSlot slot,
            out CharacterTestRuntimeAiLabLogicConditionViewModel condition)
        {
            condition = null;
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = slot?.ViewModel;
            int count = Math.Min(detector?.ConditionCount ?? 0, detector?.Conditions?.Length ?? 0);
            if (count <= 0)
                return false;
            _conditionIndex = Math.Max(0, Math.Min(_conditionIndex, count - 1));
            condition = detector.Conditions[_conditionIndex];
            return condition != null;
        }

        private string ResolveDefaultOwningStageId(CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (!string.IsNullOrEmpty(detector?.OwningStageId))
                return detector.OwningStageId;
            for (int i = 0; i < _activeStageCount && i < _stagePool.Count; i++)
            {
                if (!string.IsNullOrEmpty(_stagePool[i].StableId))
                    return _stagePool[i].StableId;
            }
            return string.Empty;
        }

        private static bool IsNumericConditionFact(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            return fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                || fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance
                || fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames;
        }

        private static string FormatDetectorScope(CharacterTestRuntimeAiLabLogicDetectorScope scope) =>
            scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card ? "全局监听" : "阶段监听";

        private static string FormatDetectorTrigger(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            switch (detector.Trigger)
            {
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted:
                    return "兼容：无原因完成";
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                    return "动作实例失败";
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame:
                    return detector.LocalFrameAnchor ==
                            CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd
                        ? "主行为结束帧"
                        : "局部帧";
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionRejected:
                    return "动作实例被拒绝";
                default:
                    return "具体条件";
            }
        }

        private static string FormatDetectorWindow(CharacterTestRuntimeAiLabLogicDetectorWindowEndMode mode)
        {
            switch (mode)
            {
                case CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd:
                    return "阶段结束";
                case CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent:
                    return "事件发生";
                case CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite:
                    return "持续监听";
                default:
                    return "固定时长";
            }
        }

        private static string FormatConditionFact(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                    return "目标存在";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                    return "目标可见";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                    return "目标死亡";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistance:
                    return "锁定受击框净空";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistanceAvailable:
                    return "受击框距离可用";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance:
                    return "目标根距离";
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning:
                    return "自身动作执行中";
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible:
                    return "自身动作可打断";
                case CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction:
                    return "可切换到动作";
                case CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable:
                    return "能力可用";
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                    return "移动路径可用";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                    return "敌方攻击将命中";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames:
                    return "敌方攻击命中帧";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    return "本次攻击已响应";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                    return "目标在攻击范围内";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                    return "目标视线受阻";
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                    return "附近存在危险";
                default:
                    return "闪避冷却就绪";
            }
        }

        private static string FormatConditionOperator(CharacterTestRuntimeAiLabCardConditionOperator conditionOperator)
        {
            switch (conditionOperator)
            {
                case CharacterTestRuntimeAiLabCardConditionOperator.Equals:
                    return "等于";
                case CharacterTestRuntimeAiLabCardConditionOperator.NotEquals:
                    return "不等于";
                case CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual:
                    return "小于等于";
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual:
                    return "大于等于";
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan:
                    return "大于";
                default:
                    return "介于";
            }
        }
    }
}
