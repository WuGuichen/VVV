using System;
using System.Globalization;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private const int GraphEditorDomainDetector = 1;
        private const int GraphEditorDomainCondition = 2;
        private const int GraphEditorDomainTransition = 3;

        private void BindDetectorGraphInput(
            DetectorSlot slot,
            string name,
            CharacterTestRuntimeAiLabDetectorFieldKind field)
        {
            Fgui.GTextInput input = slot.Visual.GetChild(name)?.asTextInput;
            if (input == null)
                return;
            input.onTouchBegin.Set(context => context.StopPropagation());
            input.onFocusIn.Set(() => BeginDetectorGraphField(slot, input, field));
            input.onSubmit.Set(() => { CommitDetectorGraphField(slot, input, field); });
            input.onFocusOut.Set(() => { CommitDetectorGraphField(slot, input, field); });
        }

        private void BindConditionGraphInput(
            DetectorSlot slot,
            string name,
            CharacterTestRuntimeAiLabConditionFieldKind field)
        {
            Fgui.GTextInput input = slot.Visual.GetChild(name)?.asTextInput;
            if (input == null)
                return;
            input.onTouchBegin.Set(context => context.StopPropagation());
            input.onFocusIn.Set(() => BeginConditionGraphField(slot, input, field));
            input.onSubmit.Set(() => { CommitConditionGraphField(slot, input, field); });
            input.onFocusOut.Set(() => { CommitConditionGraphField(slot, input, field); });
        }

        private void BindTransitionGraphInput(
            TransitionSlot slot,
            string name,
            CharacterTestRuntimeAiLabTransitionFieldKind field)
        {
            Fgui.GTextInput input = slot.Visual.GetChild(name)?.asTextInput;
            if (input == null)
                return;
            input.onTouchBegin.Set(context => context.StopPropagation());
            input.onFocusIn.Set(() => BeginTransitionGraphField(slot, input, field));
            input.onSubmit.Set(() => { CommitTransitionGraphField(slot, input, field); });
            input.onFocusOut.Set(() => { CommitTransitionGraphField(slot, input, field); });
        }

        private void BeginDetectorGraphField(
            DetectorSlot slot,
            Fgui.GTextInput input,
            CharacterTestRuntimeAiLabDetectorFieldKind field)
        {
            CancelGraphFieldEditVisual(deactivating: false);
            if (_commands?.BeginDetectorFieldEdit(slot.StableId, field, out long epoch) != true)
                return;
            CaptureGraphFieldVisual(
                GraphEditorDomainDetector,
                (int)field,
                slot.StableId,
                string.Empty,
                epoch,
                input,
                slot.Visual);
        }

        private void BeginConditionGraphField(
            DetectorSlot slot,
            Fgui.GTextInput input,
            CharacterTestRuntimeAiLabConditionFieldKind field)
        {
            if (!TryGetSelectedCondition(slot, out CharacterTestRuntimeAiLabLogicConditionViewModel condition))
                return;
            CancelGraphFieldEditVisual(deactivating: false);
            if (_commands?.BeginConditionFieldEdit(
                    slot.StableId,
                    condition.ConditionId,
                    field,
                    out long epoch) != true)
                return;
            CaptureGraphFieldVisual(
                GraphEditorDomainCondition,
                (int)field,
                slot.StableId,
                condition.ConditionId,
                epoch,
                input,
                slot.Visual);
        }

        private void BeginTransitionGraphField(
            TransitionSlot slot,
            Fgui.GTextInput input,
            CharacterTestRuntimeAiLabTransitionFieldKind field)
        {
            CancelGraphFieldEditVisual(deactivating: false);
            if (_commands?.BeginTransitionFieldEdit(slot.StableId, field, out long epoch) != true)
                return;
            CaptureGraphFieldVisual(
                GraphEditorDomainTransition,
                (int)field,
                slot.StableId,
                string.Empty,
                epoch,
                input,
                slot.Visual);
        }

        private void CaptureGraphFieldVisual(
            int domain,
            int field,
            string stableId,
            string conditionId,
            long epoch,
            Fgui.GTextInput input,
            Fgui.GComponent root)
        {
            _graphFieldEditDomain = domain;
            _graphFieldEditKind = field;
            _graphFieldEditStableId = stableId ?? string.Empty;
            _graphFieldEditConditionId = conditionId ?? string.Empty;
            _graphFieldEditEpoch = epoch;
            _graphFieldEditInput = input;
            SetGraphFieldError(root, string.Empty);
        }

        private bool CommitDetectorGraphField(
            DetectorSlot slot,
            Fgui.GTextInput input,
            CharacterTestRuntimeAiLabDetectorFieldKind field)
        {
            if (!GraphFieldMatches(GraphEditorDomainDetector, (int)field, slot.StableId, string.Empty))
                return true;
            if (!TryParseDetectorField(field, input?.text, out CharacterTestRuntimeAiLabDetectorFieldValue value, out string error))
                return RejectGraphFieldInput(slot.Visual, input, error);
            if (_commands?.CommitDetectorFieldEdit(_graphFieldEditEpoch, slot.StableId, field, value) != true)
                return RejectGraphFieldInput(slot.Visual, input, _commands?.LastMessage ?? "Detector 字段提交失败。");
            ClearGraphFieldEditVisualState();
            return true;
        }

        private bool CommitConditionGraphField(
            DetectorSlot slot,
            Fgui.GTextInput input,
            CharacterTestRuntimeAiLabConditionFieldKind field)
        {
            if (!GraphFieldMatches(
                    GraphEditorDomainCondition,
                    (int)field,
                    slot.StableId,
                    _graphFieldEditConditionId))
                return true;
            if (!TryParseConditionField(
                    slot.Visual,
                    field,
                    input?.text,
                    out CharacterTestRuntimeAiLabConditionFieldValue value,
                    out string error))
                return RejectGraphFieldInput(slot.Visual, input, error);
            if (_commands?.CommitConditionFieldEdit(
                    _graphFieldEditEpoch,
                    slot.StableId,
                    _graphFieldEditConditionId,
                    field,
                    value) != true)
                return RejectGraphFieldInput(slot.Visual, input, _commands?.LastMessage ?? "Condition 字段提交失败。");
            ClearGraphFieldEditVisualState();
            return true;
        }

        private bool CommitTransitionGraphField(
            TransitionSlot slot,
            Fgui.GTextInput input,
            CharacterTestRuntimeAiLabTransitionFieldKind field)
        {
            if (!GraphFieldMatches(GraphEditorDomainTransition, (int)field, slot.StableId, string.Empty))
                return true;
            if (!TryParseTransitionField(field, input?.text, out CharacterTestRuntimeAiLabTransitionFieldValue value, out string error))
                return RejectGraphFieldInput(slot.Visual, input, error);
            if (_commands?.CommitTransitionFieldEdit(_graphFieldEditEpoch, slot.StableId, field, value) != true)
                return RejectGraphFieldInput(slot.Visual, input, _commands?.LastMessage ?? "Transition 字段提交失败。");
            ClearGraphFieldEditVisualState();
            return true;
        }

        private bool TryCommitActiveGraphFieldBeforeNavigation()
        {
            if (_graphFieldEditEpoch == 0L)
                return true;
            if (_graphFieldEditDomain == GraphEditorDomainDetector)
            {
                DetectorSlot slot = FindDetectorSlot(_graphFieldEditStableId);
                return slot == null || CommitDetectorGraphField(
                    slot,
                    _graphFieldEditInput,
                    (CharacterTestRuntimeAiLabDetectorFieldKind)_graphFieldEditKind);
            }
            if (_graphFieldEditDomain == GraphEditorDomainCondition)
            {
                DetectorSlot slot = FindDetectorSlot(_graphFieldEditStableId);
                return slot == null || CommitConditionGraphField(
                    slot,
                    _graphFieldEditInput,
                    (CharacterTestRuntimeAiLabConditionFieldKind)_graphFieldEditKind);
            }
            if (_graphFieldEditDomain == GraphEditorDomainTransition)
            {
                TransitionSlot slot = FindTransitionSlot(_graphFieldEditStableId);
                return slot == null || CommitTransitionGraphField(
                    slot,
                    _graphFieldEditInput,
                    (CharacterTestRuntimeAiLabTransitionFieldKind)_graphFieldEditKind);
            }
            return true;
        }

        private bool GraphFieldMatches(int domain, int field, string stableId, string conditionId)
        {
            return _graphFieldEditEpoch != 0L
                && _graphFieldEditDomain == domain
                && _graphFieldEditKind == field
                && string.Equals(_graphFieldEditStableId, stableId, StringComparison.Ordinal)
                && string.Equals(_graphFieldEditConditionId, conditionId ?? string.Empty, StringComparison.Ordinal);
        }

        private bool TryParseDetectorField(
            CharacterTestRuntimeAiLabDetectorFieldKind field,
            string text,
            out CharacterTestRuntimeAiLabDetectorFieldValue value,
            out string error)
        {
            value = default;
            error = string.Empty;
            if (field == CharacterTestRuntimeAiLabDetectorFieldKind.Title
                || field == CharacterTestRuntimeAiLabDetectorFieldKind.OwningStage)
            {
                value = new CharacterTestRuntimeAiLabDetectorFieldValue(stringValue: text ?? string.Empty);
                return true;
            }
            if (field == CharacterTestRuntimeAiLabDetectorFieldKind.Priority
                || field == CharacterTestRuntimeAiLabDetectorFieldKind.ActivationStartFrame
                || field == CharacterTestRuntimeAiLabDetectorFieldKind.ActivationDurationFrames)
            {
                if (!int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                {
                    error = "请输入有效整数。";
                    return false;
                }
                value = new CharacterTestRuntimeAiLabDetectorFieldValue(intValue: integer);
                return true;
            }
            error = "当前 Detector 字段不支持文本提交。";
            return false;
        }

        private static bool TryParseConditionField(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabConditionFieldKind field,
            string text,
            out CharacterTestRuntimeAiLabConditionFieldValue value,
            out string error)
        {
            value = default;
            error = string.Empty;
            if (field == CharacterTestRuntimeAiLabConditionFieldKind.Value)
            {
                if (!float.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                    || float.IsNaN(number)
                    || float.IsInfinity(number))
                {
                    error = "请输入有效数字。";
                    return false;
                }
                value = new CharacterTestRuntimeAiLabConditionFieldValue(floatValue: number);
                return true;
            }
            if (field == CharacterTestRuntimeAiLabConditionFieldKind.Range)
            {
                string minimumText = root?.GetChild("inputConditionValue")?.asTextInput?.text ?? string.Empty;
                string maximumText = root?.GetChild("inputConditionMax")?.asTextInput?.text ?? string.Empty;
                if (!float.TryParse(minimumText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float minimum)
                    || !float.TryParse(maximumText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float maximum)
                    || float.IsNaN(minimum)
                    || float.IsInfinity(minimum)
                    || float.IsNaN(maximum)
                    || float.IsInfinity(maximum))
                {
                    error = "请输入有效范围。";
                    return false;
                }
                value = new CharacterTestRuntimeAiLabConditionFieldValue(
                    floatValue: minimum,
                    secondFloatValue: maximum);
                return true;
            }
            if (field == CharacterTestRuntimeAiLabConditionFieldKind.AbilityId)
            {
                if (!int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int abilityId))
                {
                    error = "请输入有效 Ability ID。";
                    return false;
                }
                value = new CharacterTestRuntimeAiLabConditionFieldValue(intValue: abilityId);
                return true;
            }
            if (field == CharacterTestRuntimeAiLabConditionFieldKind.CharacterActionId)
            {
                value = new CharacterTestRuntimeAiLabConditionFieldValue(stringValue: text ?? string.Empty);
                return true;
            }
            error = "当前 Condition 字段不支持文本提交。";
            return false;
        }

        private static bool TryParseTransitionField(
            CharacterTestRuntimeAiLabTransitionFieldKind field,
            string text,
            out CharacterTestRuntimeAiLabTransitionFieldValue value,
            out string error)
        {
            value = default;
            error = string.Empty;
            if (field == CharacterTestRuntimeAiLabTransitionFieldKind.Label
                || field == CharacterTestRuntimeAiLabTransitionFieldKind.SourceStage
                || field == CharacterTestRuntimeAiLabTransitionFieldKind.TargetStage
                || field == CharacterTestRuntimeAiLabTransitionFieldKind.EndReason)
            {
                value = new CharacterTestRuntimeAiLabTransitionFieldValue(stringValue: text ?? string.Empty);
                return true;
            }
            if (field == CharacterTestRuntimeAiLabTransitionFieldKind.Priority)
            {
                if (!int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int priority))
                {
                    error = "请输入有效优先级。";
                    return false;
                }
                value = new CharacterTestRuntimeAiLabTransitionFieldValue(intValue: priority);
                return true;
            }
            error = "当前 Transition 字段不支持文本提交。";
            return false;
        }

        private bool RejectGraphFieldInput(Fgui.GComponent root, Fgui.GTextInput input, string error)
        {
            SetGraphFieldError(root, error);
            input?.RequestFocus();
            return false;
        }

        private static void SetGraphFieldError(Fgui.GComponent root, string message)
        {
            string normalized = message ?? string.Empty;
            SetText(root, "fieldError", normalized);
            SetObjectVisible(root, "fieldError", normalized.Length > 0);
        }

        private void CancelGraphFieldEditVisual(bool deactivating)
        {
            if (_graphFieldEditEpoch != 0L || (_commands?.HasActiveGraphFieldEdit ?? false))
                _commands?.CancelGraphFieldEdit(deactivating);
            ClearGraphFieldEditVisualState();
        }

        private void ClearGraphFieldEditVisualState()
        {
            _graphFieldEditEpoch = 0L;
            _graphFieldEditDomain = 0;
            _graphFieldEditKind = 0;
            _graphFieldEditStableId = string.Empty;
            _graphFieldEditConditionId = string.Empty;
            _graphFieldEditInput = null;
        }

        private DetectorSlot FindDetectorSlot(string stableId)
        {
            for (int i = 0; i < _detectorPool.Count; i++)
            {
                DetectorSlot slot = _detectorPool[i];
                if (string.Equals(slot.StableId, stableId, StringComparison.Ordinal))
                    return slot;
            }
            return null;
        }

        private TransitionSlot FindTransitionSlot(string stableId)
        {
            for (int i = 0; i < _transitionPool.Count; i++)
            {
                TransitionSlot slot = _transitionPool[i];
                if (string.Equals(slot.StableId, stableId, StringComparison.Ordinal))
                    return slot;
            }
            return null;
        }

        private static void SetInputText(Fgui.GComponent root, string name, string value)
        {
            Fgui.GTextInput input = root?.GetChild(name)?.asTextInput;
            if (input != null)
                input.text = value ?? string.Empty;
        }

        private static void SetInputEnabled(Fgui.GComponent root, string name, bool enabled)
        {
            Fgui.GTextInput input = root?.GetChild(name)?.asTextInput;
            if (input == null)
                return;
            input.enabled = enabled;
            input.grayed = !enabled;
        }

        private static void SetButtonTitle(Fgui.GComponent root, string name, string value)
        {
            Fgui.GButton button = root?.GetChild(name)?.asButton;
            string next = value ?? string.Empty;
            if (button != null && !string.Equals(button.title, next, StringComparison.Ordinal))
                button.title = next;
        }
    }
}
