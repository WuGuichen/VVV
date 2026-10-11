using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private void BuildBehaviorCardPageHeader(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            var header = CreateBehaviorCardSurface(
                new Color(0.06f, 0.10f, 0.13f),
                new Color(0.18f, 0.25f, 0.30f));
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 10;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            root.Add(header);

            Button back = CreateMiniButton("<", () =>
            {
                _activeTab = BehaviorCompositionTabIndex;
                SyncTabToggles();
                RefreshView();
            });
            back.style.width = 38;
            back.style.height = 30;
            header.Add(back);

            Label title = CreateWrappedLabel("行为卡： " + draft.DisplayName);
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            title.style.marginLeft = 10;
            header.Add(title);

            header.Add(CreateSummaryPill("草稿", "Editor-only"));
            header.Add(CreateSummaryPill("状态", _behaviorCardStatusText));
            header.Add(CreateSummaryPill(
                "步骤",
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.CountExecutableSteps(draft)
                    .ToString(CultureInfo.InvariantCulture)
                    + " 步"));
            header.Add(CreateSummaryPill("步骤内时间轴", "预览"));
        }

        private static void BuildBehaviorCardCardSummary(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview)
        {
            var summary = new VisualElement();
            summary.style.flexDirection = FlexDirection.Row;
            summary.style.flexWrap = Wrap.Wrap;
            summary.style.marginBottom = 8;
            root.Add(summary);

            summary.Add(CreateSummaryPill("能力", draft.CapabilityText));
            summary.Add(CreateSummaryPill("输出", draft.OutputSummary));
            summary.Add(CreateSummaryPill("状态", draft.IsPublished ? "已发布" : "草稿"));
            if (preview != null && !string.IsNullOrWhiteSpace(preview.WarningText))
                summary.Add(CreateSummaryPill("提示", preview.WarningText));

            var detail = CreateBehaviorCardSurface(
                new Color(0.08f, 0.10f, 0.12f),
                new Color(0.15f, 0.19f, 0.23f));
            detail.style.marginBottom = 10;
            root.Add(detail);
            detail.Add(CreateWrappedLabel("意图：" + draft.IntentText));
            detail.Add(CreateWrappedLabel("触发：" + draft.TriggerSummary));
            detail.Add(CreateWrappedLabel("风险：" + draft.RiskText));
        }
    }
}
