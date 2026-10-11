using System;
using System.Collections.Generic;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private Fgui.GComponent _formalConditionPicker;
        private Fgui.GObject _formalConditionPickerAnchor;
        private bool _formalConditionPickerUsesPopupService;
        private CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind _conditionPickerSubject;
        private readonly List<CharacterTestRuntimeAiLabBehaviorConditionDescriptor> _conditionPickerOptions =
            new List<CharacterTestRuntimeAiLabBehaviorConditionDescriptor>();
        private int _conditionPickerReplaceLeafIndex = -1;
        private bool _conditionPickerRestrictsSubject;
        private CharacterTestRuntimeAiLabCardConditionFact _conditionPickerSelectedFact;

        private bool TryShowFormalConditionPicker(
            Fgui.GObject anchor,
            int replaceLeafIndex = -1,
            bool restrictToCurrentSubject = false)
        {
            if (anchor == null || !EnsureFormalConditionPicker())
                return false;
            bool open = _popupService?.IsOpen(_formalConditionPicker) == true
                || _formalConditionPicker.parent != null;
            if (open)
            {
                bool sameAnchor = ReferenceEquals(_formalConditionPickerAnchor, anchor);
                CloseFormalConditionPicker();
                if (sameAnchor)
                    return true;
            }
            _formalConditionPickerAnchor = anchor;
            _conditionPickerReplaceLeafIndex = replaceLeafIndex;
            _conditionPickerRestrictsSubject = restrictToCurrentSubject && replaceLeafIndex >= 0;
            _conditionPickerSelectedFact = default;
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind initialSubject =
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget;
            if (replaceLeafIndex >= 0
                && TryGetCondition(replaceLeafIndex, out BehaviorRecipeTriggerConditionViewModel currentRow)
                && CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(
                    currentRow.Fact,
                    out CharacterTestRuntimeAiLabBehaviorConditionDescriptor currentDescriptor))
            {
                initialSubject = currentDescriptor.Subject;
                _conditionPickerSelectedFact = currentDescriptor.Fact;
            }
            Fgui.GTextInput query = _formalConditionPicker.GetChild("query")?.asTextInput;
            if (query != null)
            {
                query.text = string.Empty;
                query.onChanged.Set(RefreshFormalConditionPicker);
            }
            BindConditionSubjectButton("btnTarget",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget);
            BindConditionSubjectButton("btnSelf",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self);
            BindConditionSubjectButton("btnEnvironment",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment);
            Fgui.GButton eventResult = _formalConditionPicker.GetChild("btnEventResult")?.asButton;
            if (eventResult != null)
            {
                if (_conditionPickerRestrictsSubject)
                {
                    eventResult.visible = true;
                    eventResult.title = "更换判断对象…";
                    eventResult.onClick.Set(ExpandConditionPickerSubjects);
                }
                else
                {
                    eventResult.visible = _model?.AllowsEventSignal == true && replaceLeafIndex < 0;
                    eventResult.title = "其他事件结果";
                    eventResult.onClick.Set(() =>
                    {
                        CloseFormalConditionPicker();
                        Submit(
                            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddEventSignal,
                            stringValue: _selectedConditionNodeId);
                    });
                }
            }
            Fgui.GButton close = _formalConditionPicker.GetChild("btnClose")?.asButton;
            if (close != null)
                close.onClick.Set(CloseFormalConditionPicker);
            SelectConditionPickerSubject(initialSubject);
            RefreshConditionPickerMode();

            if (_popupService != null)
            {
                _formalConditionPickerUsesPopupService = _popupService.Show(
                    _viewId,
                    _formalConditionPicker,
                    anchor,
                    new MxFairyGuiPopupOptions(
                        MxFairyGuiPopupPlacement.Auto,
                        safeMargin: 12f,
                        anchorGap: 4f));
            }
            if (!_formalConditionPickerUsesPopupService)
                Fgui.GRoot.inst.TogglePopup(_formalConditionPicker, anchor, Fgui.PopupDirection.Auto, false);
            return true;
        }

        private bool EnsureFormalConditionPicker()
        {
            if (_formalConditionPicker != null
                && _formalConditionPicker.displayObject != null
                && !_formalConditionPicker.displayObject.isDisposed)
                return true;
            _formalConditionPicker = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "RuntimeAiLabBehaviorConditionPicker")?.asCom;
            if (_formalConditionPicker == null)
                return false;
            string[] required =
            {
                "query", "btnTarget", "btnSelf", "btnEnvironment", "btnEventResult",
                "propertyList", "emptyText", "btnClose"
            };
            for (int i = 0; i < required.Length; i++)
            {
                if (_formalConditionPicker.GetChild(required[i]) != null)
                    continue;
                _formalConditionPicker.Dispose();
                _formalConditionPicker = null;
                return false;
            }
            _formalConditionPicker.name = "behaviorConditionPicker";
            _formalConditionPicker.opaque = true;
            CharacterTestFairyGuiPopupInputBoundary.Install(
                _formalConditionPicker,
                CloseFormalConditionPicker);
            return true;
        }

        private void BindConditionSubjectButton(
            string controlName,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject)
        {
            Fgui.GButton button = _formalConditionPicker?.GetChild(controlName)?.asButton;
            if (button == null)
                return;
            button.onClick.Set(() => SelectConditionPickerSubject(subject));
        }

        private void SelectConditionPickerSubject(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject)
        {
            _conditionPickerSubject = subject;
            SetConditionSubjectButton("btnTarget",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget);
            SetConditionSubjectButton("btnSelf",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self);
            SetConditionSubjectButton("btnEnvironment",
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment);
            RefreshFormalConditionPicker();
        }

        private void SetConditionSubjectButton(
            string controlName,
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject)
        {
            Fgui.GButton button = _formalConditionPicker?.GetChild(controlName)?.asButton;
            if (button == null)
                return;
            string title = CharacterTestRuntimeAiLabBehaviorConditionCatalog.SubjectText(subject);
            button.title = (_conditionPickerSubject == subject ? "✓ " : string.Empty) + title;
            button.enabled = !_conditionPickerRestrictsSubject;
        }

        private void ExpandConditionPickerSubjects()
        {
            _conditionPickerRestrictsSubject = false;
            RefreshConditionPickerMode();
            SelectConditionPickerSubject(_conditionPickerSubject);
        }

        private void RefreshConditionPickerMode()
        {
            if (_formalConditionPicker == null)
                return;
            Fgui.GTextField title = _formalConditionPicker.GetChild("title")?.asTextField;
            Fgui.GTextField subjectLabel = _formalConditionPicker.GetChild("subjectLabel")?.asTextField;
            if (title != null)
            {
                title.text = _conditionPickerReplaceLeafIndex < 0
                    ? "添加条件"
                    : _conditionPickerRestrictsSubject
                        ? "更改条件属性"
                        : "更换判断对象";
            }
            if (subjectLabel != null)
            {
                subjectLabel.text = _conditionPickerRestrictsSubject
                    ? "当前判断对象"
                    : "判断对象";
            }

            string[] controlNames = { "btnTarget", "btnSelf", "btnEnvironment" };
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind[] subjects =
            {
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget,
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self,
                CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment
            };
            int visibleIndex = 0;
            for (int i = 0; i < controlNames.Length; i++)
            {
                Fgui.GButton button = _formalConditionPicker.GetChild(controlNames[i])?.asButton;
                if (button == null)
                    continue;
                bool subjectAvailable = HasAuthorableConditionForSubject(subjects[i]);
                bool visible = subjectAvailable
                    && (!_conditionPickerRestrictsSubject || subjects[i] == _conditionPickerSubject);
                button.visible = visible;
                if (visible)
                {
                    button.SetXY(18f, 92f + visibleIndex * 46f);
                    visibleIndex++;
                }
            }
            Fgui.GButton eventResult = _formalConditionPicker.GetChild("btnEventResult")?.asButton;
            if (eventResult != null)
            {
                if (_conditionPickerRestrictsSubject)
                {
                    eventResult.visible = true;
                    eventResult.title = "更换判断对象…";
                    eventResult.SetXY(18f, 92f + visibleIndex * 46f);
                    eventResult.onClick.Set(ExpandConditionPickerSubjects);
                }
                else
                {
                    eventResult.visible = _model?.AllowsEventSignal == true
                        && _conditionPickerReplaceLeafIndex < 0;
                    eventResult.title = "其他事件结果";
                    eventResult.SetXY(18f, 92f + visibleIndex * 46f);
                    eventResult.onClick.Set(() =>
                    {
                        CloseFormalConditionPicker();
                        Submit(
                            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.AddEventSignal,
                            stringValue: _selectedConditionNodeId);
                    });
                }
            }
        }

        private void RefreshFormalConditionPicker()
        {
            if (_formalConditionPicker == null)
                return;
            string query = _formalConditionPicker.GetChild("query")?.asTextInput?.text?.Trim()
                ?? string.Empty;
            _conditionPickerOptions.Clear();
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorConditionDescriptor> descriptors =
                CharacterTestRuntimeAiLabBehaviorConditionCatalog.All;
            for (int i = 0; i < descriptors.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor = descriptors[i];
                if (descriptor.Subject != _conditionPickerSubject)
                    continue;
                string search = descriptor.DisplayName + " "
                    + CharacterTestRuntimeAiLabBehaviorConditionCatalog.CategoryText(descriptor.Category)
                    + " " + descriptor.Fact;
                if (query.Length > 0 && search.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                _conditionPickerOptions.Add(descriptor);
            }
            Fgui.GList list = _formalConditionPicker.GetChild("propertyList")?.asList;
            if (list != null)
            {
                list.itemRenderer = (index, itemObject) =>
                {
                    Fgui.GComponent row = itemObject?.asCom;
                    if (row == null || index < 0 || index >= _conditionPickerOptions.Count)
                        return;
                    CharacterTestRuntimeAiLabBehaviorConditionDescriptor descriptor =
                        _conditionPickerOptions[index];
                    Fgui.GTextField title = row.GetChild("title")?.asTextField;
                    Fgui.GTextField category = row.GetChild("category")?.asTextField;
                    if (title != null)
                        title.text = descriptor.Fact == _conditionPickerSelectedFact
                            ? "✓  " + descriptor.DisplayName
                            : descriptor.DisplayName;
                    if (category != null)
                        category.text = CharacterTestRuntimeAiLabBehaviorConditionCatalog.CategoryText(
                            descriptor.Category);
                    row.onClick.Set(() =>
                    {
                        Fgui.GObject anchor = _formalConditionPickerAnchor;
                        int replaceLeafIndex = _conditionPickerReplaceLeafIndex;
                        bool selectingCurrent = replaceLeafIndex >= 0
                            && descriptor.Fact == _conditionPickerSelectedFact;
                        if (selectingCurrent)
                            return;
                        _conditionPickerSelectedFact = descriptor.Fact;
                        RefreshFormalConditionPicker();
                        BeginAddSubjectCondition(anchor, descriptor, replaceLeafIndex);
                    });
                };
                list.numItems = _conditionPickerOptions.Count;
            }
            Fgui.GTextField empty = _formalConditionPicker.GetChild("emptyText")?.asTextField;
            if (empty != null)
                empty.visible = _conditionPickerOptions.Count == 0;
        }

        internal static bool HasAuthorableConditionForSubject(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabBehaviorConditionDescriptor> descriptors =
                CharacterTestRuntimeAiLabBehaviorConditionCatalog.All;
            for (int i = 0; i < descriptors.Count; i++)
            {
                if (descriptors[i].Subject == subject)
                    return true;
            }
            return false;
        }

        private void CloseFormalConditionPicker()
        {
            if (_formalConditionPicker == null)
                return;
            if (_formalConditionPickerUsesPopupService && _popupService != null)
                _popupService.Hide(_formalConditionPicker, MxFairyGuiPopupCloseReason.Toggle);
            else if (_formalConditionPicker.parent != null)
                Fgui.GRoot.inst.HidePopup(_formalConditionPicker);
            _formalConditionPickerUsesPopupService = false;
            _formalConditionPickerAnchor = null;
            _conditionPickerReplaceLeafIndex = -1;
            _conditionPickerRestrictsSubject = false;
            _conditionPickerSelectedFact = default;
        }
    }
}
