using System;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private Fgui.GComponent _targetStatePicker;
        private Fgui.GObject _targetStatePickerAnchor;
        private bool _targetStatePickerUsesPopupService;
        private int _targetStatePickerLeafIndex = -1;
        private uint _targetStateDraftPhases;
        private uint _targetStateDraftRequired;
        private uint _targetStateDraftForbidden;

        private bool TryShowTargetStatePicker(
            Fgui.GObject anchor,
            BehaviorRecipeTriggerConditionViewModel row)
        {
            if (anchor == null || row == null || !EnsureTargetStatePicker())
                return false;
            CloseTargetStatePicker();
            _targetStatePickerAnchor = anchor;
            _targetStatePickerLeafIndex = row.LeafIndex;
            _targetStateDraftPhases = row.AllowedTargetPhases;
            _targetStateDraftRequired = row.RequiredTargetFlags;
            _targetStateDraftForbidden = row.ForbiddenTargetFlags;
            BindTargetStatePickerHandlers();
            RefreshTargetStatePicker();
            if (_popupService != null)
            {
                _targetStatePickerUsesPopupService = _popupService.Show(
                    _viewId,
                    _targetStatePicker,
                    anchor,
                    new MxFairyGuiPopupOptions(
                        MxFairyGuiPopupPlacement.Auto,
                        safeMargin: 12f,
                        anchorGap: 4f));
            }
            if (!_targetStatePickerUsesPopupService)
                Fgui.GRoot.inst.TogglePopup(_targetStatePicker, anchor, Fgui.PopupDirection.Auto, false);
            return true;
        }

        private bool EnsureTargetStatePicker()
        {
            if (_targetStatePicker != null
                && _targetStatePicker.displayObject != null
                && !_targetStatePicker.displayObject.isDisposed)
                return true;
            _targetStatePicker = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "RuntimeAiLabTargetStatePicker")?.asCom;
            if (_targetStatePicker == null)
                return false;
            string[] required =
            {
                "summary", "validation", "phaseNoTarget", "phaseAlive", "phaseDying",
                "phaseDead", "phaseReviving", "btnApply", "btnCancel", "btnClose"
            };
            for (int i = 0; i < required.Length; i++)
            {
                if (_targetStatePicker.GetChild(required[i]) != null)
                    continue;
                _targetStatePicker.Dispose();
                _targetStatePicker = null;
                return false;
            }
            string[] flags = { "perceivable", "losBlocked", "inRange", "movePath", "threat", "responded" };
            string[] states = { "Ignore", "Required", "Forbidden" };
            for (int i = 0; i < flags.Length; i++)
                for (int j = 0; j < states.Length; j++)
                    if (_targetStatePicker.GetChild("flag" + flags[i] + states[j]) == null)
                    {
                        _targetStatePicker.Dispose();
                        _targetStatePicker = null;
                        return false;
                    }
            _targetStatePicker.name = "targetStatePicker";
            _targetStatePicker.opaque = true;
            CharacterTestFairyGuiPopupInputBoundary.Install(
                _targetStatePicker,
                CloseTargetStatePicker);
            return true;
        }

        private void BindTargetStatePickerHandlers()
        {
            BindTargetPhase("phaseNoTarget", CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget);
            BindTargetPhase("phaseAlive", CharacterTestRuntimeAiPlannerTargetStateMask.Alive);
            BindTargetPhase("phaseDying", CharacterTestRuntimeAiPlannerTargetStateMask.Dying);
            BindTargetPhase("phaseDead", CharacterTestRuntimeAiPlannerTargetStateMask.Dead);
            BindTargetPhase("phaseReviving", CharacterTestRuntimeAiPlannerTargetStateMask.Reviving);
            BindTargetFlag("perceivable", CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable);
            BindTargetFlag("losBlocked", CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked);
            BindTargetFlag("inRange", CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange);
            BindTargetFlag("movePath", CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable);
            BindTargetFlag("threat", CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf);
            BindTargetFlag("responded", CharacterTestRuntimeAiPlannerTargetStateMask.AttackInstanceResponded);
            _targetStatePicker.GetChild("btnApply").asButton.onClick.Set(ApplyTargetStatePicker);
            _targetStatePicker.GetChild("btnCancel").asButton.onClick.Set(CloseTargetStatePicker);
            _targetStatePicker.GetChild("btnClose").asButton.onClick.Set(CloseTargetStatePicker);
        }

        private void BindTargetPhase(string name, CharacterTestRuntimeAiPlannerTargetStateMask phase)
        {
            Fgui.GButton button = _targetStatePicker.GetChild(name)?.asButton;
            if (button == null)
                return;
            button.onClick.Set(() =>
            {
                _targetStateDraftPhases ^= (uint)phase;
                RefreshTargetStatePicker();
            });
        }

        private void BindTargetFlag(string prefix, CharacterTestRuntimeAiPlannerTargetStateMask flag)
        {
            BindTargetFlagState(prefix, "Ignore", flag, 0);
            BindTargetFlagState(prefix, "Required", flag, 1);
            BindTargetFlagState(prefix, "Forbidden", flag, 2);
        }

        private void BindTargetFlagState(
            string prefix,
            string suffix,
            CharacterTestRuntimeAiPlannerTargetStateMask flag,
            int state)
        {
            Fgui.GButton button = _targetStatePicker.GetChild("flag" + prefix + suffix)?.asButton;
            if (button == null)
                return;
            uint mask = (uint)flag;
            button.onClick.Set(() =>
            {
                _targetStateDraftRequired &= ~mask;
                _targetStateDraftForbidden &= ~mask;
                if (state == 1)
                    _targetStateDraftRequired |= mask;
                else if (state == 2)
                    _targetStateDraftForbidden |= mask;
                RefreshTargetStatePicker();
            });
        }

        private void RefreshTargetStatePicker()
        {
            if (_targetStatePicker == null)
                return;
            SetTargetPickerButton("phaseNoTarget", "无目标", (uint)CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget, _targetStateDraftPhases);
            SetTargetPickerButton("phaseAlive", "存活", (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive, _targetStateDraftPhases);
            SetTargetPickerButton("phaseDying", "濒死", (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Dying, _targetStateDraftPhases);
            SetTargetPickerButton("phaseDead", "死亡", (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Dead, _targetStateDraftPhases);
            SetTargetPickerButton("phaseReviving", "复活中", (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Reviving, _targetStateDraftPhases);
            RefreshTargetFlag("perceivable", CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable);
            RefreshTargetFlag("losBlocked", CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked);
            RefreshTargetFlag("inRange", CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange);
            RefreshTargetFlag("movePath", CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable);
            RefreshTargetFlag("threat", CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf);
            RefreshTargetFlag("responded", CharacterTestRuntimeAiPlannerTargetStateMask.AttackInstanceResponded);
            Fgui.GTextField summary = _targetStatePicker.GetChild("summary")?.asTextField;
            if (summary != null)
                summary.text = BehaviorRecipeTriggerEventInspectorProjection.FormatTargetStateValue(
                    new CharacterTestRuntimeAiLabCardCondition
                    {
                        Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetState,
                        AllowedTargetPhases = _targetStateDraftPhases,
                        RequiredTargetFlags = _targetStateDraftRequired,
                        ForbiddenTargetFlags = _targetStateDraftForbidden
                    });
            bool valid = _targetStateDraftPhases != 0u
                && (_targetStateDraftRequired & _targetStateDraftForbidden) == 0u;
            Fgui.GTextField validation = _targetStatePicker.GetChild("validation")?.asTextField;
            if (validation != null)
                validation.text = valid ? string.Empty : "至少选择一个阶段，且同一状态不能同时必须与排除。";
            Fgui.GButton apply = _targetStatePicker.GetChild("btnApply")?.asButton;
            if (apply != null)
                apply.enabled = valid;
        }

        private void RefreshTargetFlag(string prefix, CharacterTestRuntimeAiPlannerTargetStateMask flag)
        {
            uint mask = (uint)flag;
            int state = (_targetStateDraftRequired & mask) != 0u
                ? 1
                : (_targetStateDraftForbidden & mask) != 0u ? 2 : 0;
            SetTargetPickerState("flag" + prefix + "Ignore", "不限", state == 0);
            SetTargetPickerState("flag" + prefix + "Required", "必须", state == 1);
            SetTargetPickerState("flag" + prefix + "Forbidden", "排除", state == 2);
        }

        private void SetTargetPickerButton(string name, string title, uint mask, uint value) =>
            SetTargetPickerState(name, title, (value & mask) != 0u);

        private void SetTargetPickerState(string name, string title, bool selected)
        {
            Fgui.GButton button = _targetStatePicker.GetChild(name)?.asButton;
            if (button != null)
                button.title = (selected ? "✓ " : string.Empty) + title;
        }

        private void ApplyTargetStatePicker()
        {
            if (_targetStatePickerLeafIndex < 0 || _targetStateDraftPhases == 0u)
                return;
            Submit(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionTargetStateMasks,
                conditionIndex: _targetStatePickerLeafIndex,
                allowedTargetPhases: _targetStateDraftPhases,
                requiredTargetFlags: _targetStateDraftRequired,
                forbiddenTargetFlags: _targetStateDraftForbidden);
            CloseTargetStatePicker();
        }

        private void CloseTargetStatePicker()
        {
            if (_targetStatePicker == null)
                return;
            if (_targetStatePickerUsesPopupService && _popupService != null)
                _popupService.Hide(_targetStatePicker, MxFairyGuiPopupCloseReason.Toggle);
            else if (_targetStatePicker.parent != null)
                Fgui.GRoot.inst.HidePopup(_targetStatePicker);
            _targetStatePickerUsesPopupService = false;
            _targetStatePickerAnchor = null;
            _targetStatePickerLeafIndex = -1;
        }
    }
}
