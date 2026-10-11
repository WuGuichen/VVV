using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private void MarkBehaviorCardDraftDirty()
        {
            _behaviorCardStatusText = "Editor draft modified, not saved";
            RefreshView();
        }

        private static PopupField<string> CreateBranchField(
            string label,
            List<string> choices,
            string current,
            Action<string> onChanged)
        {
            string value = string.IsNullOrWhiteSpace(current) ? "End" : current;
            if (!choices.Contains(value))
                value = "End";
            PopupField<string> field = new PopupField<string>(label, choices, value);
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue ?? "End"));
            return field;
        }

        private static VisualElement CreateSummaryPill(string label, string value)
        {
            var pill = new VisualElement();
            pill.style.marginRight = 6;
            pill.style.marginBottom = 6;
            pill.style.paddingLeft = 8;
            pill.style.paddingRight = 8;
            pill.style.paddingTop = 4;
            pill.style.paddingBottom = 4;
            pill.style.backgroundColor = new Color(0.16f, 0.18f, 0.20f);
            pill.Add(CreateWrappedLabel(label + ": " + value));
            return pill;
        }

        private void AddBehaviorCardStep()
        {
            EnsureBehaviorCardDrafts();
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = GetSelectedBehaviorCardDraft();
            if (draft == null
                || draft.Steps.Count >= CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.MaxStepCount)
            {
                return;
            }

            string id = "step_"
                + (draft.Steps.Count + 1).ToString(CultureInfo.InvariantCulture);
            draft.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                id,
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait)
            {
                Title = "新步骤",
                ConditionSummary = "条件待配置",
                WaitFrames = 1,
                Note = "Editor-only 草稿步骤。"
            });
            _selectedBehaviorCardStepIndex = draft.Steps.Count - 1;
            _behaviorCardStatusText = "草稿已修改，未保存";
            RefreshView();
        }

        private void DeleteSelectedBehaviorCardStep()
        {
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = GetSelectedBehaviorCardDraft();
            if (draft == null
                || draft.Steps.Count <= CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.MinStepCount
                || _selectedBehaviorCardStepIndex < 0
                || _selectedBehaviorCardStepIndex >= draft.Steps.Count)
            {
                return;
            }

            draft.Steps.RemoveAt(_selectedBehaviorCardStepIndex);
            if (_selectedBehaviorCardStepIndex >= draft.Steps.Count)
                _selectedBehaviorCardStepIndex = draft.Steps.Count - 1;
            _behaviorCardStatusText = "草稿已修改，未保存";
            RefreshView();
        }

        private void MoveSelectedBehaviorCardStep(int delta)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = GetSelectedBehaviorCardDraft();
            if (draft == null || delta == 0)
                return;

            int newIndex = _selectedBehaviorCardStepIndex + delta;
            if (_selectedBehaviorCardStepIndex < 0
                || _selectedBehaviorCardStepIndex >= draft.Steps.Count
                || newIndex < 0
                || newIndex >= draft.Steps.Count)
            {
                return;
            }

            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step =
                draft.Steps[_selectedBehaviorCardStepIndex];
            draft.Steps.RemoveAt(_selectedBehaviorCardStepIndex);
            draft.Steps.Insert(newIndex, step);
            _selectedBehaviorCardStepIndex = newIndex;
            _behaviorCardStatusText = "草稿已修改，未保存";
            RefreshView();
        }

        private void SaveSelectedBehaviorCardDraft()
        {
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = GetSelectedBehaviorCardDraft();
            if (draft == null)
                return;

            draft.SavedRevision++;
            _behaviorCardStatusText = "Editor-only 草稿已保存到当前窗口内存 rev "
                + draft.SavedRevision.ToString(CultureInfo.InvariantCulture);
            RefreshView();
        }
    }
}
