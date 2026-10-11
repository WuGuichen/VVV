using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private static readonly string[] BehaviorCardCapabilityFilters =
        {
            "All",
            "Pursuit",
            "Damage",
            "Control",
            "Kite",
            "Defense"
        };
        private void BuildBehaviorCardSidePanel(VisualElement root)
        {
            EnsureBehaviorCardDrafts();

            if (GetSelectedBehaviorCardStep() == null)
            {
                BuildBehaviorCardLeftPanel(root);
                return;
            }

            BuildBehaviorCardStepInspector(root);
        }

        private void BuildBehaviorCardLeftPanel(VisualElement root)
        {
            EnsureBehaviorCardDrafts();

            root.Add(CreateSectionTitle("行为卡库"));
            root.Add(CreateNotice(
                "06 只提供 Editor-only 草稿 UI 基础。这里选择的是行为卡模板草稿，不会保存 JSON，也不会写入 gameplay authority。"));

            TextField search = CreateDelayedTextField("搜索", _behaviorCardSearchText);
            search.name = BehaviorCardSearchFieldName;
            search.RegisterValueChangedCallback(evt =>
            {
                _behaviorCardSearchText = evt.newValue ?? string.Empty;
                RefreshView();
            });
            root.Add(search);

            var filter = new PopupField<string>(
                "能力",
                new List<string>(BehaviorCardCapabilityFilters),
                string.IsNullOrWhiteSpace(_behaviorCardCapabilityFilter)
                    ? "All"
                    : _behaviorCardCapabilityFilter);
            filter.name = BehaviorCardCapabilityFilterName;
            filter.RegisterValueChangedCallback(evt =>
            {
                _behaviorCardCapabilityFilter = evt.newValue ?? "All";
                RefreshView();
            });
            root.Add(filter);

            var list = new ScrollView();
            list.name = BehaviorCardLibraryName;
            list.style.flexGrow = 1;
            list.style.marginTop = 6;
            root.Add(list);

            List<CharacterTestRuntimeAiPlannerBehaviorCardDraft> matches =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.FilterDrafts(
                _behaviorCardDrafts,
                _behaviorCardSearchText,
                _behaviorCardCapabilityFilter);
            if (matches.Count == 0)
            {
                list.Add(CreateWrappedLabel("没有匹配的行为卡草稿。"));
                return;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = matches[i];
                int cardIndex = FindBehaviorCardDraftIndex(draft);
                list.Add(BuildBehaviorCardLibraryRow(draft, cardIndex));
            }
        }

        private VisualElement BuildBehaviorCardLibraryRow(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            int cardIndex)
        {
            var row = CreateBehaviorCardSurface(
                cardIndex == _selectedBehaviorCardIndex
                    ? new Color(0.12f, 0.24f, 0.40f)
                    : new Color(0.08f, 0.11f, 0.13f),
                cardIndex == _selectedBehaviorCardIndex
                    ? new Color(0.24f, 0.48f, 0.78f)
                    : new Color(0.17f, 0.22f, 0.28f));
            row.style.marginBottom = 6;
            row.style.minHeight = 68;
            row.RegisterCallback<MouseDownEvent>(_ =>
            {
                _selectedBehaviorCardIndex = cardIndex;
                _selectedBehaviorCardStepIndex = -1;
                _selectedBehaviorCardBranchId = string.Empty;
                RefreshView();
            });

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            row.Add(header);

            Label marker = CreateWrappedLabel(">");
            marker.style.width = 18;
            marker.style.unityFontStyleAndWeight = FontStyle.Bold;
            marker.style.color = new Color(0.70f, 0.84f, 1.0f);
            header.Add(marker);

            Label title = CreateWrappedLabel(draft.DisplayName);
            title.style.flexGrow = 1;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 13;
            header.Add(title);

            Label category = CreateWrappedLabel(draft.CapabilityText);
            category.style.color = new Color(0.68f, 0.76f, 0.84f);
            category.style.marginLeft = 18;
            row.Add(category);

            Label output = CreateWrappedLabel(draft.OutputSummary);
            output.style.color = new Color(0.60f, 0.66f, 0.72f);
            output.style.marginLeft = 18;
            row.Add(output);
            return row;
        }

        private int FindBehaviorCardDraftIndex(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            if (_behaviorCardDrafts == null || draft == null)
                return 0;

            for (int i = 0; i < _behaviorCardDrafts.Count; i++)
            {
                if (ReferenceEquals(_behaviorCardDrafts[i], draft))
                    return i;
            }

            return 0;
        }

    }
}
