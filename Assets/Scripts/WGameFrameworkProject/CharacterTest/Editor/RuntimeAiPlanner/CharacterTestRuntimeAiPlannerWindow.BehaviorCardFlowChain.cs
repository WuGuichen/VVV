using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private void BuildBehaviorCardFlowChain(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview)
        {
            var panel = CreateBehaviorCardSurface(
                new Color(0.07f, 0.09f, 0.11f),
                new Color(0.17f, 0.22f, 0.28f));
            panel.name = BehaviorCardFlowChainName;
            panel.style.marginBottom = 8;
            root.Add(panel);

            var flowchartTitleRow = new VisualElement();
            flowchartTitleRow.style.flexDirection = FlexDirection.Row;
            flowchartTitleRow.style.alignItems = Align.Center;
            flowchartTitleRow.style.marginBottom = 6;
            panel.Add(flowchartTitleRow);
            Label flowchartTitle = CreateSectionTitle("主执行链");
            flowchartTitle.style.flexGrow = 1;
            flowchartTitleRow.Add(flowchartTitle);
            flowchartTitleRow.Add(CreateSummaryPill("分支流程图", "动作 / 判断 / End"));
            flowchartTitleRow.Add(CreateSummaryPill("自动适配", "无横向滚动"));
            panel.Add(CreateWrappedLabel("动作节点执行动作，判断节点分出条件路径；连线标签只显示关键条件，详细规则在侧栏 Inspector 编辑。"));

            BuildBehaviorCardFlowRuler(panel, preview);

            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult flowchart =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.BuildFlowchartLayoutResult(
                    draft,
                    preview,
                    GetBehaviorCardGraphAvailableWidth());
            panel.Add(BuildBehaviorCardFlowchartCanvas(draft, preview, flowchart));

            var actions = CreateButtonRow();
            panel.Add(actions);
            Button addStep = CreatePrimaryButton("娣诲姞姝ラ", AddBehaviorCardStep);
            addStep.name = BehaviorCardAddStepButtonName;
            addStep.SetEnabled(draft.Steps.Count < CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.MaxStepCount);
            actions.Add(addStep);
        }
    }
}
