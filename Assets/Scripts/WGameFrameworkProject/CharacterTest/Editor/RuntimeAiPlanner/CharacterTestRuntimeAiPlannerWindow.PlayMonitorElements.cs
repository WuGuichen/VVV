using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private static VisualElement CreatePlayMonitorSourceHeader(
            string targetName,
            CharacterTestRuntimeAiPlannerSnapshot snapshot,
            int agentCount)
        {
            string profile = snapshot != null ? EmptyAsNone(snapshot.ProfileId) : "unavailable";
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Column;
            header.style.marginTop = 4;
            header.style.marginBottom = 6;
            header.style.paddingBottom = 4;

            Label title = CreateSectionTitle(targetName + "  agents=" + agentCount);
            title.style.marginBottom = 1;
            header.Add(title);
            Label details = CreateWrappedLabel("profile=" + profile);
            details.style.color = new Color(0.68f, 0.68f, 0.68f);
            header.Add(details);
            return header;
        }

        private static VisualElement CreateAgentSummaryHeader()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 3;
            row.style.minWidth = 0;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            AddAgentHeaderCell(row, "Agent", 58f);
            AddAgentHeaderCell(row, "State", 120f);
            AddAgentHeaderCell(row, "Target", 64f);
            AddAgentHeaderCell(row, "Action", 150f);
            AddAgentHeaderCell(row, "Dist", 54f);
            AddAgentHeaderCell(row, "LOS", 88f);
            AddAgentHeaderCell(row, "Path", 110f);
            AddAgentHeaderCell(row, "Score/Cost", 84f);
            AddAgentHeaderCell(row, "Command", 110f);
            return row;
        }

        private static Button CreateAgentSummaryRow(
            CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel viewModel,
            Action onClick)
        {
            var row = new Button(onClick);
            row.style.flexDirection = FlexDirection.Column;
            row.style.alignItems = Align.Stretch;
            row.style.marginBottom = 5;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.minWidth = 0;
            row.style.whiteSpace = WhiteSpace.Normal;
            row.style.backgroundColor = GetSummaryRowColor(viewModel.Severity);
            row.style.borderLeftWidth = 3;
            row.style.borderLeftColor = GetSeverityColor(viewModel.Severity);
            row.tooltip = viewModel.DecisionHeadline;

            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.flexWrap = Wrap.Wrap;
            columns.style.alignItems = Align.FlexStart;
            columns.style.minWidth = 0;
            AddAgentSummaryCell(columns, viewModel.AgentText, 58f);
            AddAgentSummaryCell(columns, viewModel.StateText, 120f);
            AddAgentSummaryCell(columns, viewModel.TargetText, 64f);
            AddAgentSummaryCell(columns, viewModel.ActionText, 150f, 1f);
            AddAgentSummaryCell(columns, viewModel.DistanceText, 54f);
            AddAgentSummaryCell(columns, viewModel.VisibilityText, 88f);
            AddAgentSummaryCell(columns, viewModel.PathText, 110f);
            AddAgentSummaryCell(columns, viewModel.ScoreCostText, 84f);
            AddAgentSummaryCell(columns, viewModel.CommandText, 110f);
            row.Add(columns);

            var chips = CreatePlayMonitorChipRow(
                CreatePlayMonitorChip(viewModel.IssueText, GetSeverityColor(viewModel.Severity)),
                CreatePlayMonitorChip(viewModel.BehaviorLoadoutText, new Color(0.25f, 0.29f, 0.34f)),
                CreatePlayMonitorChip(viewModel.DecisionHeadline, new Color(0.25f, 0.29f, 0.34f)));
            chips.style.marginTop = 3;
            row.Add(chips);
            return row;
        }

        private static VisualElement CreatePlayMonitorInspectorHeader(
            CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel viewModel)
        {
            var header = new VisualElement();
            header.style.marginBottom = 8;
            header.style.paddingLeft = 6;
            header.style.paddingRight = 6;
            header.style.paddingTop = 6;
            header.style.paddingBottom = 6;
            header.style.backgroundColor = new Color(0.18f, 0.2f, 0.22f);
            header.style.borderLeftWidth = 3;
            header.style.borderLeftColor = GetSeverityColor(viewModel.Severity);

            Label title = CreateSectionTitle(viewModel.AgentText + "  " + viewModel.ActionText);
            title.style.marginBottom = 2;
            header.Add(title);
            header.Add(CreateWrappedLabel("DebugSource: " + viewModel.SourceName));
            header.Add(CreateWrappedLabel("State: " + viewModel.StateText));
            header.Add(CreateWrappedLabel("Loadout: " + viewModel.BehaviorLoadoutText));
            header.Add(CreatePlayMonitorChipRow(
                CreatePlayMonitorChip(viewModel.TargetText, new Color(0.25f, 0.29f, 0.34f)),
                CreatePlayMonitorChip(viewModel.VisibilityText, new Color(0.25f, 0.29f, 0.34f)),
                CreatePlayMonitorChip(viewModel.PathText, new Color(0.25f, 0.29f, 0.34f))));
            return header;
        }

        private static VisualElement CreatePlayMonitorSection(string title, params VisualElement[] children)
        {
            var section = new VisualElement();
            section.style.marginBottom = 8;
            section.style.paddingLeft = 6;
            section.style.paddingRight = 6;
            section.style.paddingTop = 6;
            section.style.paddingBottom = 6;
            section.style.borderLeftWidth = 1;
            section.style.borderRightWidth = 1;
            section.style.borderTopWidth = 1;
            section.style.borderBottomWidth = 1;
            section.style.borderLeftColor = new Color(0.24f, 0.24f, 0.24f);
            section.style.borderRightColor = new Color(0.24f, 0.24f, 0.24f);
            section.style.borderTopColor = new Color(0.24f, 0.24f, 0.24f);
            section.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);
            section.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            section.style.minWidth = 0;
            section.Add(CreateSectionTitle(title));
            for (int i = 0; i < children.Length; i++)
                section.Add(children[i]);
            return section;
        }

        private static VisualElement CreatePlayMonitorField(string label, string value)
        {
            var field = new VisualElement();
            field.style.flexDirection = FlexDirection.Column;
            field.style.marginBottom = 5;
            field.style.minWidth = 0;

            var name = new Label(label);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = new Color(0.72f, 0.72f, 0.72f);
            name.style.marginBottom = 1;
            field.Add(name);

            Label text = CreateWrappedLabel(value);
            text.style.flexGrow = 1;
            text.style.minWidth = 0;
            field.Add(text);
            return field;
        }

        private static VisualElement CreatePlayMonitorChipRow(params Label[] chips)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.minWidth = 0;
            for (int i = 0; i < chips.Length; i++)
                row.Add(chips[i]);
            return row;
        }

        private static Label CreatePlayMonitorChip(string text, Color color)
        {
            var chip = new Label(text);
            chip.style.whiteSpace = WhiteSpace.Normal;
            chip.style.marginRight = 4;
            chip.style.marginBottom = 4;
            chip.style.paddingLeft = 5;
            chip.style.paddingRight = 5;
            chip.style.paddingTop = 2;
            chip.style.paddingBottom = 2;
            chip.style.backgroundColor = color;
            chip.style.color = Color.white;
            chip.style.minWidth = 0;
            return chip;
        }

        private static void AddAgentHeaderCell(VisualElement row, string text, float width)
        {
            Label label = CreateWrappedLabel(text);
            label.style.width = width;
            label.style.minWidth = width;
            label.style.marginRight = 4;
            label.style.color = new Color(0.66f, 0.66f, 0.66f);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(label);
        }

        private static void AddAgentSummaryCell(
            VisualElement row,
            string text,
            float width,
            float flexGrow = 0f)
        {
            Label label = CreateWrappedLabel(text);
            label.style.width = width;
            label.style.minWidth = width;
            label.style.marginRight = 4;
            label.style.flexGrow = flexGrow;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(label);
        }

        private static Color GetSummaryRowColor(CharacterTestRuntimeAiPlannerPlayMonitorSeverity severity)
        {
            switch (severity)
            {
                case CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Error:
                    return new Color(0.32f, 0.16f, 0.16f);
                case CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Warning:
                    return new Color(0.28f, 0.24f, 0.15f);
                default:
                    return new Color(0.2f, 0.2f, 0.2f);
            }
        }

        private static Color GetSeverityColor(CharacterTestRuntimeAiPlannerPlayMonitorSeverity severity)
        {
            switch (severity)
            {
                case CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Error:
                    return new Color(0.72f, 0.24f, 0.22f);
                case CharacterTestRuntimeAiPlannerPlayMonitorSeverity.Warning:
                    return new Color(0.76f, 0.58f, 0.2f);
                default:
                    return new Color(0.26f, 0.52f, 0.34f);
            }
        }

        private static Color GetVisibilityColor(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!agent.TargetExists)
                return new Color(0.32f, 0.32f, 0.32f);
            if (agent.TargetLineOfSightBlocked)
                return new Color(0.76f, 0.58f, 0.2f);
            return agent.TargetVisible ? new Color(0.26f, 0.52f, 0.34f) : new Color(0.32f, 0.32f, 0.32f);
        }

        private static Color GetPathColor(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!agent.MovePathAvailable)
                return new Color(0.72f, 0.24f, 0.22f);
            return agent.MoveDirectPathClear ? new Color(0.26f, 0.52f, 0.34f) : new Color(0.76f, 0.58f, 0.2f);
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }

    }
}
