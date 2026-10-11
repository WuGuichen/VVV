using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private VisualElement BuildBehaviorCardFlowBoard(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardViewModel board)
        {
            var root = new VisualElement();
            root.name = BehaviorCardFlowBoardName;
            root.style.marginTop = 6;
            root.style.marginBottom = 8;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.backgroundColor = new Color(0.045f, 0.055f, 0.065f);
            root.style.borderTopWidth = 1;
            root.style.borderBottomWidth = 1;
            root.style.borderLeftWidth = 1;
            root.style.borderRightWidth = 1;
            root.style.borderTopColor = new Color(0.18f, 0.23f, 0.28f);
            root.style.borderBottomColor = new Color(0.18f, 0.23f, 0.28f);
            root.style.borderLeftColor = new Color(0.18f, 0.23f, 0.28f);
            root.style.borderRightColor = new Color(0.18f, 0.23f, 0.28f);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.marginBottom = 5;
            root.Add(header);
            header.Add(CreateFlowBoardLaneTitle("失败/中断", 150f));
            Label main = CreateFlowBoardLaneTitle("主路径", 0f);
            main.style.flexGrow = 1;
            header.Add(main);
            header.Add(CreateFlowBoardLaneTitle("条件/快捷分支", 190f));

            for (int i = 0; board != null && i < board.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardStep boardStep = board.Steps[i];
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step =
                    FindBehaviorCardStepById(draft, boardStep.StepId);
                CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing =
                    FindBehaviorCardStepTiming(preview, boardStep.StepId);
                root.Add(BuildBehaviorCardFlowBoardRow(step, timing, boardStep, i));
            }

            root.Add(BuildBehaviorCardFlowBoardEnd(board));
            return root;
        }

        private VisualElement BuildBehaviorCardFlowBoardRow(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardStep boardStep,
            int pathIndex)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            row.style.marginBottom = 5;
            row.style.minHeight = 84;

            row.Add(BuildBehaviorCardFlowBoardBranchLane(
                boardStep.LeftBranches,
                BehaviorCardFlowBoardLeftLaneName,
                alignRight: true));

            var mainLane = new VisualElement();
            mainLane.name = BehaviorCardFlowBoardMainLaneName;
            mainLane.style.flexGrow = 1;
            mainLane.style.minWidth = 0;
            mainLane.style.marginLeft = 7;
            mainLane.style.marginRight = 7;
            row.Add(mainLane);
            mainLane.Add(BuildBehaviorCardFlowBoardStepCard(step, timing, boardStep.StepIndex, pathIndex));
            if (!string.IsNullOrWhiteSpace(boardStep.MainBranch.FromStepId))
                mainLane.Add(BuildBehaviorCardFlowBoardConnector(boardStep.MainBranch));

            row.Add(BuildBehaviorCardFlowBoardBranchLane(
                boardStep.RightBranches,
                BehaviorCardFlowBoardRightLaneName,
                alignRight: false));
            return row;
        }

        private VisualElement BuildBehaviorCardFlowBoardStepCard(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing,
            int stepIndex,
            int pathIndex)
        {
            bool isSelected = stepIndex == _selectedBehaviorCardStepIndex;
            var card = CreateBehaviorCardSurface(
                isSelected
                    ? new Color(0.08f, 0.14f, 0.20f)
                    : new Color(0.075f, 0.095f, 0.115f),
                isSelected
                    ? new Color(0.24f, 0.52f, 0.90f)
                    : new Color(0.22f, 0.26f, 0.31f));
            card.name = BehaviorCardStepCardName;
            card.style.marginBottom = 0;
            card.style.paddingLeft = 7;
            card.style.paddingRight = 7;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.RegisterCallback<MouseDownEvent>(_ =>
            {
                _selectedBehaviorCardStepIndex = stepIndex;
                _selectedBehaviorCardBranchId = string.Empty;
                RefreshBehaviorCardViewPreservingScroll();
            });

            if (step == null)
            {
                card.Add(CreateSingleLineLabel("Missing step", 12, new Color(0.86f, 0.90f, 0.94f)));
                return card;
            }

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;
            card.Add(header);

            Label index = CreateSingleLineLabel((pathIndex + 1).ToString(CultureInfo.InvariantCulture), 11, new Color(0.86f, 0.90f, 0.94f));
            index.style.width = 22;
            index.style.height = 20;
            index.style.unityTextAlign = TextAnchor.MiddleCenter;
            index.style.unityFontStyleAndWeight = FontStyle.Bold;
            index.style.backgroundColor = new Color(0.11f, 0.16f, 0.20f);
            index.style.marginRight = 8;
            header.Add(index);

            Label title = CreateSingleLineLabel(step.DisplayTitle, 12, new Color(0.86f, 0.90f, 0.94f));
            title.tooltip = step.DisplayTitle;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            header.Add(title);

            card.Add(BuildBehaviorCardFlowBoardStepMetaRow(step, timing));
            BuildBehaviorCardStepTimeline(card, step, timing, expanded: false);
            return card;
        }

        private static VisualElement BuildBehaviorCardFlowBoardStepMetaRow(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing)
        {
            var row = new VisualElement();
            row.name = BehaviorCardStepBranchPanelName;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            row.style.overflow = Overflow.Hidden;

            row.Add(CreateCompactBadge(
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft.FormatKind(step.Kind),
                new Color(0.10f, 0.24f, 0.38f),
                54f));
            row.Add(CreateCompactBadge(
                FormatBehaviorCardGraphOutput(step),
                new Color(0.10f, 0.13f, 0.16f),
                74f));
            row.Add(CreateCompactBadge(
                FormatBehaviorCardDurationChip(timing),
                new Color(0.16f, 0.18f, 0.21f),
                52f));

            if (!string.IsNullOrWhiteSpace(timing.WarningText))
            {
                Label warning = CreateCompactBadge("提示", new Color(0.35f, 0.24f, 0.10f), 48f);
                warning.style.maxWidth = 30;
                warning.style.width = 30;
                warning.name = BehaviorCardStepWarningChipName;
                warning.tooltip = timing.WarningText;
                row.Add(warning);
            }

            return row;
        }

        private static string FormatBehaviorCardGraphOutput(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step == null)
                return string.Empty;

            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility
                && step.AbilityId == CharacterTestAbilityIds.DashStrikeAbilityId)
            {
                return "DashStrike";
            }

            return FormatBehaviorCardOutput(step);
        }

        private VisualElement BuildBehaviorCardFlowBoardBranchLane(
            List<CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview> branches,
            string laneName,
            bool alignRight)
        {
            var lane = new VisualElement();
            lane.name = laneName;
            lane.style.width = laneName == BehaviorCardFlowBoardLeftLaneName ? 150 : 190;
            lane.style.minWidth = lane.style.width;
            lane.style.justifyContent = Justify.Center;
            lane.style.alignItems = alignRight ? Align.FlexEnd : Align.FlexStart;

            for (int i = 0; branches != null && i < branches.Count; i++)
                lane.Add(BuildBehaviorCardFlowBoardBranchChip(branches[i]));
            return lane;
        }

        private VisualElement BuildBehaviorCardFlowBoardBranchChip(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            Color color = GetBehaviorCardTriggerBackground(branch.TriggerType);
            var chip = new VisualElement();
            chip.name = BehaviorCardFlowBoardBranchChipName;
            chip.userData = branch;
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.marginBottom = 4;
            chip.style.paddingLeft = 6;
            chip.style.paddingRight = 6;
            chip.style.paddingTop = 3;
            chip.style.paddingBottom = 3;
            chip.style.maxWidth = 180;
            chip.style.backgroundColor = color;
            chip.style.borderTopWidth = 1;
            chip.style.borderBottomWidth = 1;
            chip.style.borderLeftWidth = 1;
            chip.style.borderRightWidth = 1;
            chip.style.borderTopColor = GetBehaviorCardTriggerBorder(branch.TriggerType);
            chip.style.borderBottomColor = GetBehaviorCardTriggerBorder(branch.TriggerType);
            chip.style.borderLeftColor = GetBehaviorCardTriggerBorder(branch.TriggerType);
            chip.style.borderRightColor = GetBehaviorCardTriggerBorder(branch.TriggerType);
            chip.tooltip = FormatBehaviorCardBranchLine(branch);
            chip.RegisterCallback<MouseDownEvent>(_ => SelectBehaviorCardFlowBoardBranch(branch));

            Label arrow = CreateSingleLineLabel(GetBehaviorCardBranchArrow(branch.Kind), 10, GetBehaviorCardBranchColor(branch.Kind));
            arrow.style.width = 22;
            chip.Add(arrow);
            Label text = CreateSingleLineLabel(FormatBehaviorCardBranchChip(branch), 10, new Color(0.86f, 0.90f, 0.94f));
            text.style.flexGrow = 1;
            chip.Add(text);
            return chip;
        }

        private static Color GetBehaviorCardTriggerBackground(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType triggerType)
        {
            switch (triggerType)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition:
                    return new Color(0.07f, 0.11f, 0.18f);
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure:
                    return new Color(0.16f, 0.09f, 0.06f);
                default:
                    return new Color(0.07f, 0.14f, 0.09f);
            }
        }

        private static Color GetBehaviorCardTriggerBorder(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType triggerType)
        {
            switch (triggerType)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition:
                    return new Color(0.20f, 0.42f, 0.82f);
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure:
                    return new Color(0.88f, 0.36f, 0.18f);
                default:
                    return new Color(0.24f, 0.64f, 0.32f);
            }
        }

        private static VisualElement BuildBehaviorCardFlowBoardConnector(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            var connector = new VisualElement();
            connector.name = BehaviorCardFlowBoardConnectorName;
            connector.style.flexDirection = FlexDirection.Row;
            connector.style.alignItems = Align.Center;
            connector.style.justifyContent = Justify.Center;
            connector.style.height = 22;
            connector.style.marginTop = 2;
            connector.style.marginBottom = 2;

            Color color = GetBehaviorCardBranchColor(branch.Kind);
            var lineLeft = new VisualElement();
            lineLeft.style.height = 2;
            lineLeft.style.flexGrow = 1;
            lineLeft.style.backgroundColor = color;
            connector.Add(lineLeft);
            connector.Add(CreateCompactBadge(
                "-> " + FormatBehaviorCardBranchChip(branch),
                new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f),
                160f));
            var lineRight = new VisualElement();
            lineRight.style.height = 2;
            lineRight.style.flexGrow = 1;
            lineRight.style.backgroundColor = color;
            connector.Add(lineRight);
            return connector;
        }

        private static VisualElement BuildBehaviorCardFlowBoardEnd(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardViewModel board)
        {
            var end = new VisualElement();
            end.name = BehaviorCardFlowBoardEndName;
            end.style.flexDirection = FlexDirection.Row;
            end.style.alignItems = Align.Center;
            end.style.justifyContent = Justify.Center;
            end.style.paddingLeft = 8;
            end.style.paddingRight = 8;
            end.style.paddingTop = 6;
            end.style.paddingBottom = 6;
            end.style.marginTop = 4;
            end.style.backgroundColor = new Color(0.09f, 0.10f, 0.12f);
            end.style.borderTopWidth = 1;
            end.style.borderBottomWidth = 1;
            end.style.borderLeftWidth = 1;
            end.style.borderRightWidth = 1;
            end.style.borderTopColor = new Color(0.40f, 0.46f, 0.52f);
            end.style.borderBottomColor = new Color(0.40f, 0.46f, 0.52f);
            end.style.borderLeftColor = new Color(0.40f, 0.46f, 0.52f);
            end.style.borderRightColor = new Color(0.40f, 0.46f, 0.52f);

            var marker = new VisualElement();
            marker.name = BehaviorCardTerminalPanelName;
            marker.style.display = DisplayStyle.None;
            end.Add(marker);

            end.Add(CreateCompactBadge("End", new Color(0.18f, 0.20f, 0.23f), 58f));
            Label text = CreateSingleLineLabel("交回行为组重新决策", 11, new Color(0.86f, 0.90f, 0.94f));
            text.style.marginLeft = 8;
            text.style.marginRight = 8;
            end.Add(text);
            int incoming = board != null ? board.EndIncomingBranchCount : 0;
            end.Add(CreateCompactBadge(
                "入边 " + incoming.ToString(CultureInfo.InvariantCulture),
                new Color(0.16f, 0.18f, 0.21f),
                74f));
            return end;
        }

        private static Label CreateFlowBoardLaneTitle(string text, float width)
        {
            Label label = CreateSingleLineLabel(text, 10, new Color(0.64f, 0.72f, 0.78f));
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (width > 0f)
                label.style.width = width;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }

        private VisualElement BuildBehaviorCardGraphCanvas(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            CharacterTestRuntimeAiPlannerBehaviorCardGraphViewModel graph)
        {
            var canvas = new VisualElement();
            canvas.name = BehaviorCardGraphCanvasName;
            canvas.style.position = Position.Relative;
            canvas.style.height = graph != null ? graph.Height : 360f;
            canvas.style.minHeight = graph != null ? graph.Height : 360f;
            if (graph != null)
                canvas.style.width = graph.Width;
            else
                canvas.style.width = Length.Percent(100f);
            canvas.style.maxWidth = Length.Percent(100f);
            canvas.style.minWidth = 0;
            canvas.style.alignSelf = Align.Center;
            canvas.style.overflow = Overflow.Hidden;
            canvas.style.marginTop = 8;
            canvas.style.marginBottom = 8;
            canvas.style.backgroundColor = new Color(0.045f, 0.055f, 0.065f);
            canvas.style.borderTopWidth = 1;
            canvas.style.borderBottomWidth = 1;
            canvas.style.borderLeftWidth = 1;
            canvas.style.borderRightWidth = 1;
            canvas.style.borderTopColor = new Color(0.18f, 0.23f, 0.28f);
            canvas.style.borderBottomColor = new Color(0.18f, 0.23f, 0.28f);
            canvas.style.borderLeftColor = new Color(0.18f, 0.23f, 0.28f);
            canvas.style.borderRightColor = new Color(0.18f, 0.23f, 0.28f);

            var edgeLayer = new VisualElement();
            edgeLayer.name = BehaviorCardGraphEdgeLayerName;
            edgeLayer.style.position = Position.Absolute;
            edgeLayer.style.left = 0;
            edgeLayer.style.top = 0;
            edgeLayer.style.right = 0;
            edgeLayer.style.bottom = 0;
            canvas.Add(edgeLayer);

            for (int i = 0; graph != null && i < graph.Edges.Count; i++)
                edgeLayer.Add(BuildBehaviorCardGraphEdge(graph.Edges[i]));

            var nodeLayer = new VisualElement();
            nodeLayer.name = BehaviorCardGraphNodeLayerName;
            nodeLayer.style.position = Position.Absolute;
            nodeLayer.style.left = 0;
            nodeLayer.style.top = 0;
            nodeLayer.style.right = 0;
            nodeLayer.style.bottom = 0;
            canvas.Add(nodeLayer);

            for (int i = 0; graph != null && i < graph.Nodes.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardGraphNode node = graph.Nodes[i];
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step =
                    FindBehaviorCardStepById(draft, node.StepId);
                CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing =
                    FindBehaviorCardStepTiming(preview, node.StepId);
                nodeLayer.Add(BuildBehaviorCardGraphStepNode(step, timing, node, i));
            }

            if (graph != null)
                nodeLayer.Add(BuildBehaviorCardGraphTerminalNode(graph.TerminalNode, preview));

            return canvas;
        }

        private VisualElement BuildBehaviorCardGraphStepNode(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing,
            CharacterTestRuntimeAiPlannerBehaviorCardGraphNode node,
            int stepIndex)
        {
            bool isSelected = stepIndex == _selectedBehaviorCardStepIndex;
            var card = CreateBehaviorCardSurface(
                isSelected
                    ? new Color(0.08f, 0.14f, 0.20f)
                    : new Color(0.075f, 0.095f, 0.115f),
                isSelected
                    ? new Color(0.24f, 0.52f, 0.90f)
                    : new Color(0.22f, 0.26f, 0.31f));
            card.name = BehaviorCardStepCardName;
            card.style.position = Position.Absolute;
            card.style.left = node.X;
            card.style.top = node.Y;
            card.style.width = node.Width;
            card.style.height = node.Height;
            card.style.marginBottom = 0;
            card.style.paddingLeft = 7;
            card.style.paddingRight = 7;
            card.style.paddingTop = 7;
            card.style.paddingBottom = 7;
            card.RegisterCallback<MouseDownEvent>(_ =>
            {
                _selectedBehaviorCardStepIndex = stepIndex;
                _selectedBehaviorCardBranchId = string.Empty;
                RefreshBehaviorCardViewPreservingScroll();
            });

            if (step == null)
            {
                card.Add(CreateWrappedLabel("Missing step"));
                return card;
            }

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;
            card.Add(header);

            Label index = CreateWrappedLabel((stepIndex + 1).ToString(CultureInfo.InvariantCulture));
            index.style.width = 20;
            index.style.height = 20;
            index.style.unityTextAlign = TextAnchor.MiddleCenter;
            index.style.unityFontStyleAndWeight = FontStyle.Bold;
            index.style.backgroundColor = new Color(0.11f, 0.16f, 0.20f);
            index.style.marginRight = 8;
            header.Add(index);

            Label title = CreateWrappedLabel(ShortenForUi(step.DisplayTitle, 24));
            title.tooltip = step.DisplayTitle;
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            header.Add(title);

            card.Add(BuildBehaviorCardStepSummaryRow(step, timing));
            BuildBehaviorCardStepTimeline(card, step, timing, expanded: false);
            return card;
        }

        private static VisualElement BuildBehaviorCardGraphTerminalNode(
            CharacterTestRuntimeAiPlannerBehaviorCardGraphNode node,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview)
        {
            var terminal = CreateBehaviorCardSurface(
                new Color(0.09f, 0.10f, 0.12f),
                new Color(0.40f, 0.46f, 0.52f));
            terminal.name = BehaviorCardTerminalPanelName;
            terminal.style.position = Position.Absolute;
            terminal.style.left = node.X;
            terminal.style.top = node.Y;
            terminal.style.width = node.Width;
            terminal.style.height = node.Height;
            terminal.style.marginBottom = 0;
            terminal.style.alignItems = Align.Center;

            terminal.Add(CreateSectionTitle("End"));
            string incoming = preview != null && preview.Terminals.Count > 0
                ? preview.Terminals[0].IncomingBranchCount.ToString(CultureInfo.InvariantCulture)
                : "0";
            terminal.Add(CreateWrappedLabel("交回行为组重新决策 | incoming branches " + incoming));
            return terminal;
        }

        private VisualElement BuildBehaviorCardGraphEdge(
            CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge edge)
        {
            var group = new VisualElement();
            group.name = BehaviorCardGraphEdgeName;
            group.style.position = Position.Absolute;
            group.style.left = 0;
            group.style.top = 0;
            group.style.right = 0;
            group.style.bottom = 0;

            Color color = GetBehaviorCardBranchColor(edge.Kind);
            AddBehaviorCardGraphSegment(group, edge.StartX, edge.StartY, edge.LaneX, edge.StartY, color, edge);
            AddBehaviorCardGraphSegment(group, edge.LaneX, edge.StartY, edge.LaneX, edge.EndY, color, edge);
            AddBehaviorCardGraphSegment(group, edge.LaneX, edge.EndY, edge.EndX, edge.EndY, color, edge);

            Label label = CreateMiniBadge(FormatBehaviorCardGraphEdgeLabel(edge), color);
            label.name = BehaviorCardGraphEdgeLabelName;
            label.tooltip = FormatBehaviorCardGraphEdgeTooltip(edge);
            label.userData = edge;
            label.style.position = Position.Absolute;
            label.style.left = edge.LabelX;
            label.style.top = edge.LabelY;
            label.style.maxWidth = 132;
            label.style.fontSize = 10;
            label.pickingMode = PickingMode.Position;
            label.RegisterCallback<MouseDownEvent>(_ => SelectBehaviorCardGraphEdge(edge));
            group.Add(label);
            return group;
        }

        private void AddBehaviorCardGraphSegment(
            VisualElement root,
            float x1,
            float y1,
            float x2,
            float y2,
            Color color,
            CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge edge)
        {
            var segment = new VisualElement();
            segment.style.position = Position.Absolute;
            segment.style.backgroundColor = color;
            segment.pickingMode = PickingMode.Position;
            segment.RegisterCallback<MouseDownEvent>(_ => SelectBehaviorCardGraphEdge(edge));

            float left = Math.Min(x1, x2);
            float top = Math.Min(y1, y2);
            float width = Math.Abs(x2 - x1);
            float height = Math.Abs(y2 - y1);
            bool horizontal = width >= height;
            segment.style.left = left;
            segment.style.top = top;
            segment.style.width = horizontal ? Math.Max(2f, width) : 3f;
            segment.style.height = horizontal ? 3f : Math.Max(2f, height);
            root.Add(segment);
        }

        private static VisualElement BuildBehaviorCardChainConnector()
        {
            var connector = new VisualElement();
            connector.name = BehaviorCardChainConnectorName;
            connector.style.height = 38;
            connector.style.minHeight = 38;
            connector.style.alignSelf = Align.Stretch;
            connector.style.alignItems = Align.Center;
            connector.style.justifyContent = Justify.Center;
            Label arrow = CreateWrappedLabel("v");
            arrow.style.fontSize = 24;
            arrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            arrow.style.color = new Color(0.72f, 0.80f, 0.86f);
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            connector.Add(arrow);
            return connector;
        }


        private VisualElement BuildBehaviorCardStepCard(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing,
            int stepIndex)
        {
            bool isSelected = stepIndex == _selectedBehaviorCardStepIndex;
            var card = CreateBehaviorCardSurface(
                isSelected
                    ? new Color(0.08f, 0.14f, 0.20f)
                    : new Color(0.08f, 0.10f, 0.12f),
                isSelected
                    ? new Color(0.24f, 0.52f, 0.90f)
                    : new Color(0.22f, 0.26f, 0.31f));
            card.name = BehaviorCardStepCardName;
            card.style.width = Length.Percent(100);
            card.style.minWidth = 0;
            card.style.minHeight = isSelected ? 170 : 94;
            card.style.alignSelf = Align.Stretch;
            card.style.marginRight = 0;
            card.style.marginBottom = 0;
            card.RegisterCallback<MouseDownEvent>(_ =>
            {
                _selectedBehaviorCardStepIndex = stepIndex;
                RefreshBehaviorCardViewPreservingScroll();
            });

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;
            card.Add(header);

            Label index = CreateWrappedLabel((stepIndex + 1).ToString(CultureInfo.InvariantCulture));
            index.style.width = 26;
            index.style.height = 24;
            index.style.unityTextAlign = TextAnchor.MiddleCenter;
            index.style.unityFontStyleAndWeight = FontStyle.Bold;
            index.style.backgroundColor = new Color(0.11f, 0.16f, 0.20f);
            index.style.marginRight = 8;
            header.Add(index);

            Label title = CreateWrappedLabel(ShortenForUi(step.DisplayTitle, 16));
            title.tooltip = step.DisplayTitle;
            title.style.fontSize = 15;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            header.Add(title);

            Label menu = CreateWrappedLabel("...");
            menu.style.width = 24;
            menu.style.unityTextAlign = TextAnchor.MiddleCenter;
            menu.style.color = new Color(0.65f, 0.70f, 0.76f);
            header.Add(menu);

            card.Add(BuildBehaviorCardStepSummaryRow(step, timing));
            BuildBehaviorCardStepTimeline(card, step, timing, isSelected);

            if (isSelected)
                card.Add(BuildBehaviorCardBranchChipRow(preview, step.StepId));

            return card;
        }

        private static VisualElement BuildBehaviorCardStepSummaryRow(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing)
        {
            var row = new VisualElement();
            row.name = BehaviorCardStepBranchPanelName;
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;

            row.Add(CreateMiniBadge(
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft.FormatKind(step.Kind),
                new Color(0.10f, 0.24f, 0.38f)));
            row.Add(CreateMiniBadge(
                ShortenForUi(FormatBehaviorCardOutput(step), 28),
                new Color(0.10f, 0.13f, 0.16f)));
            row.Add(CreateMiniBadge(
                FormatBehaviorCardDurationChip(timing),
                new Color(0.16f, 0.18f, 0.21f)));

            if (!string.IsNullOrWhiteSpace(timing.WarningText))
            {
                Label warning = CreateMiniBadge("提示", new Color(0.35f, 0.24f, 0.10f));
                warning.name = BehaviorCardStepWarningChipName;
                warning.tooltip = timing.WarningText;
                row.Add(warning);
            }

            return row;
        }

        private static VisualElement BuildBehaviorCardBranchChipRow(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 4;
            bool added = false;
            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (!string.Equals(branch.FromStepId, stepId, StringComparison.Ordinal))
                    continue;

                row.Add(BuildBehaviorCardMainChainBranchChip(branch));
                added = true;
            }

            if (!added)
                row.Add(BuildBehaviorCardMainChainBranchChip(default));
            return row;
        }

        private static Label BuildBehaviorCardMainChainBranchChip(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            if (string.IsNullOrWhiteSpace(branch.FromStepId))
            {
                Label missing = CreateMiniBadge("分支未配置", new Color(0.28f, 0.28f, 0.28f));
                missing.name = BehaviorCardStepBranchChipName;
                return missing;
            }

            Label chip = CreateMiniBadge(
                FormatBehaviorCardBranchChip(branch),
                GetBehaviorCardBranchColor(branch.Kind));
            chip.name = BehaviorCardStepBranchChipName;
            chip.tooltip = FormatBehaviorCardBranchLine(branch);
            return chip;
        }


        private static string FormatBehaviorCardGraphEdgeLabel(
            CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge edge)
        {
            return edge.Label;
        }

        private static string FormatBehaviorCardGraphEdgeTooltip(
            CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge edge)
        {
            return edge.BranchId
                + " | "
                + edge.FromStepId
                + " -> "
                + (edge.TargetIsEnd ? "End" : edge.TargetStepId);
        }

        private void SelectBehaviorCardGraphEdge(
            CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge edge)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = GetSelectedBehaviorCardDraft();
            if (draft == null)
                return;

            int sourceIndex = FindBehaviorCardStepIndexById(draft, edge.FromStepId);
            if (sourceIndex >= 0)
                _selectedBehaviorCardStepIndex = sourceIndex;
            _selectedBehaviorCardBranchId = edge.BranchId ?? string.Empty;
            RefreshBehaviorCardViewPreservingScroll();
        }

        private void SelectBehaviorCardFlowBoardBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = GetSelectedBehaviorCardDraft();
            if (draft == null)
                return;

            int sourceIndex = FindBehaviorCardStepIndexById(draft, branch.FromStepId);
            if (sourceIndex >= 0)
                _selectedBehaviorCardStepIndex = sourceIndex;
            _selectedBehaviorCardBranchId = branch.BranchId ?? string.Empty;
            RefreshBehaviorCardViewPreservingScroll();
        }

    }
}
