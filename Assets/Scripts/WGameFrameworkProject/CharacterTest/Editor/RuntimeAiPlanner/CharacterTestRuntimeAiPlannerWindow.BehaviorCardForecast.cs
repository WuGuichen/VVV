using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private void BuildBehaviorCardTimelinePreview(VisualElement root)
        {
            var panel = new VisualElement();
            panel.name = BehaviorCardTimelinePreviewName;
            panel.style.paddingTop = 8;
            panel.style.paddingBottom = 8;
            root.Add(panel);

            panel.Add(CreateSectionTitle("高级诊断 / 选中步骤只读摘要"));
            var foldout = new Foldout
            {
                text = "展开 raw / action-chain 摘要",
                value = false
            };
            panel.Add(foldout);
            foldout.Add(CreateWrappedLabel(
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.FormatTimelinePreview(
                    GetSelectedBehaviorCardStep())));
        }

        private void BuildBehaviorCardForecast(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            var panel = CreateBehaviorCardSurface(
                new Color(0.07f, 0.09f, 0.11f),
                new Color(0.17f, 0.22f, 0.28f));
            panel.name = BehaviorCardForecastName;
            root.Add(panel);

            panel.Add(CreateSectionTitle("情境预估"));
            panel.Add(CreateWrappedLabel("只预估行为卡会沿哪条步骤路径请求动作，不预估伤害、胜负、真实路径或最终命中。"));

            var table = new VisualElement();
            table.name = BehaviorCardForecastTableName;
            table.style.marginTop = 6;
            panel.Add(table);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 8;
            header.style.paddingTop = 5;
            header.style.paddingBottom = 5;
            header.style.backgroundColor = new Color(0.12f, 0.15f, 0.18f);
            table.Add(header);
            header.Add(BuildBehaviorCardForecastCell("情境", 110, true));
            Label pathHeader = BuildBehaviorCardForecastCell("路径", 420, true);
            pathHeader.style.flexGrow = 1;
            header.Add(pathHeader);
            header.Add(BuildBehaviorCardForecastCell("提示", 180, true));

            List<CharacterTestRuntimeAiPlannerBehaviorCardForecastRow> rows =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.BuildForecast(draft);
            for (int i = 0; i < rows.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardForecastRow row = rows[i];
                table.Add(BuildBehaviorCardForecastRow(row));
            }
        }

        private static VisualElement BuildBehaviorCardForecastRow(
            CharacterTestRuntimeAiPlannerBehaviorCardForecastRow row)
        {
            var line = new VisualElement();
            line.name = BehaviorCardPathForecastRowName;
            line.style.flexDirection = FlexDirection.Row;
            line.style.marginBottom = 1;
            line.style.paddingLeft = 6;
            line.style.paddingRight = 6;
            line.style.paddingTop = 5;
            line.style.paddingBottom = 5;
            line.style.backgroundColor = new Color(0.10f, 0.12f, 0.14f);

            line.Add(BuildBehaviorCardForecastCell(row.Scenario, 110, true));

            Label path = BuildBehaviorCardForecastCell(FormatBehaviorCardForecastPath(row), 420, false);
            path.style.flexGrow = 1;
            line.Add(path);

            line.Add(BuildBehaviorCardForecastCell(FormatBehaviorCardForecastHint(row), 180, false));
            return line;
        }

        private static Label BuildBehaviorCardForecastCell(string text, float width, bool bold)
        {
            Label cell = CreateWrappedLabel(text);
            cell.style.width = width;
            cell.style.minWidth = width;
            cell.style.marginRight = 8;
            if (bold)
                cell.style.unityFontStyleAndWeight = FontStyle.Bold;
            return cell;
        }

        private static string FormatBehaviorCardForecastPath(
            CharacterTestRuntimeAiPlannerBehaviorCardForecastRow row)
        {
            if (string.IsNullOrWhiteSpace(row.Notice))
                return row.ExpectedStep + " -> " + row.Output;

            return row.Notice;
        }

        private static string FormatBehaviorCardForecastHint(
            CharacterTestRuntimeAiPlannerBehaviorCardForecastRow row)
        {
            if (row.Output.IndexOf("UseAbility", StringComparison.OrdinalIgnoreCase) >= 0)
                return "进入追击动作";
            if (row.Output.IndexOf("UseAction", StringComparison.OrdinalIgnoreCase) >= 0)
                return "请求 CharacterAction";
            if (row.Output.IndexOf("End", StringComparison.OrdinalIgnoreCase) >= 0
                || row.Output.IndexOf("行为组", StringComparison.OrdinalIgnoreCase) >= 0)
                return "回流重新决策";
            return row.Output;
        }
    }
}
