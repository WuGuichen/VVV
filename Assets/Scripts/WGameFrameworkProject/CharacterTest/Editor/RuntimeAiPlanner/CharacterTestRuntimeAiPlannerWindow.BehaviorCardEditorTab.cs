using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private void EnsureBehaviorCardDrafts()
        {
            if (_behaviorCardDrafts == null || _behaviorCardDrafts.Count == 0)
            {
                _behaviorCardDrafts =
                    CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.CreateBuiltInDrafts();
                _selectedBehaviorCardIndex = 0;
                _selectedBehaviorCardStepIndex = -1;
            }

            if (_selectedBehaviorCardIndex < 0)
                _selectedBehaviorCardIndex = 0;
            if (_selectedBehaviorCardIndex >= _behaviorCardDrafts.Count)
                _selectedBehaviorCardIndex = _behaviorCardDrafts.Count - 1;

            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = GetSelectedBehaviorCardDraft();
            if (draft == null || draft.Steps.Count == 0)
            {
                _selectedBehaviorCardStepIndex = -1;
                _selectedBehaviorCardBranchId = string.Empty;
                return;
            }

            if (_selectedBehaviorCardStepIndex < -1)
                _selectedBehaviorCardStepIndex = -1;
            if (_selectedBehaviorCardStepIndex >= draft.Steps.Count)
                _selectedBehaviorCardStepIndex = draft.Steps.Count - 1;

            if (_selectedBehaviorCardStepIndex < 0)
            {
                _selectedBehaviorCardBranchId = string.Empty;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_selectedBehaviorCardBranchId)
                && !BehaviorCardBranchExists(draft, _selectedBehaviorCardBranchId))
            {
                _selectedBehaviorCardBranchId = string.Empty;
            }
        }

        private CharacterTestRuntimeAiPlannerBehaviorCardDraft GetSelectedBehaviorCardDraft()
        {
            if (_behaviorCardDrafts == null
                || _selectedBehaviorCardIndex < 0
                || _selectedBehaviorCardIndex >= _behaviorCardDrafts.Count)
            {
                return null;
            }

            return _behaviorCardDrafts[_selectedBehaviorCardIndex];
        }

        private CharacterTestRuntimeAiPlannerBehaviorCardStepDraft GetSelectedBehaviorCardStep()
        {
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = GetSelectedBehaviorCardDraft();
            if (draft == null
                || _selectedBehaviorCardStepIndex < 0
                || _selectedBehaviorCardStepIndex >= draft.Steps.Count)
            {
                return null;
            }

            return draft.Steps[_selectedBehaviorCardStepIndex];
        }

        private static bool BehaviorCardBranchExists(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            string branchId)
        {
            if (draft == null || string.IsNullOrWhiteSpace(branchId))
                return false;

            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step == null)
                    continue;

                for (int j = 0; j < step.Branches.Count; j++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch = step.Branches[j];
                    if (branch != null && string.Equals(branch.BranchId, branchId, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private void RefreshBehaviorCardViewPreservingScroll()
        {
            CaptureBehaviorCardScrollOffsets();
            RefreshView();
        }

        private void CaptureBehaviorCardScrollOffsets()
        {
            if (_centerPanel == null)
                return;

            ScrollView editorScroll = _centerPanel.Q<ScrollView>(BehaviorCardEditorScrollName);
            if (editorScroll != null)
                _behaviorCardEditorScrollOffset = editorScroll.scrollOffset;
        }

        private static void RestoreBehaviorCardScrollOffset(ScrollView scroll, Vector2 offset)
        {
            if (scroll == null)
                return;

            scroll.scrollOffset = offset;
            scroll.schedule.Execute(() => scroll.scrollOffset = offset);
        }

        private float GetBehaviorCardGraphAvailableWidth()
        {
            float width = 0f;
            if (_centerPanel != null && _centerPanel.resolvedStyle.width > 1f)
                width = _centerPanel.resolvedStyle.width - 36f;

            if (width <= 1f && position.width > 1f)
                width = position.width - 320f - 96f;

            if (width <= 1f)
                width = 720f;

            return Math.Max(420f, width);
        }

        private void BuildBehaviorCardEditorTab(VisualElement root)
        {
            EnsureBehaviorCardDrafts();
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = GetSelectedBehaviorCardDraft();
            if (draft == null)
            {
                root.Add(CreateNotice("没有可编辑的行为卡草稿。"));
                return;
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = BehaviorCardEditorScrollName;
            scroll.style.flexGrow = 1;
            scroll.style.minWidth = 0;
            scroll.contentContainer.style.minWidth = 0;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(scroll);
            RestoreBehaviorCardScrollOffset(scroll, _behaviorCardEditorScrollOffset);

            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.BuildFlowTimePreview(draft);

            BuildBehaviorCardPageHeader(scroll, draft);
            BuildBehaviorCardCardSummary(scroll, draft, preview);
            BuildBehaviorCardFlowChain(scroll, draft, preview);
            scroll.Add(CreateSpacer(8));
            BuildBehaviorCardForecast(scroll, draft);
            scroll.Add(CreateSpacer(8));
            BuildBehaviorCardTimelinePreview(scroll);
        }
    }
}
