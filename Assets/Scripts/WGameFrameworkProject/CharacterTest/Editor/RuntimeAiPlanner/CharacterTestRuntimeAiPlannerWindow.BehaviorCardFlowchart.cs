using System;
using System.Globalization;
using MxFramework.CharacterAction;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private VisualElement BuildBehaviorCardFlowchartCanvas(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult flowchart)
        {
            var canvas = new VisualElement();
            canvas.name = BehaviorCardFlowchartCanvasName;
            canvas.style.position = Position.Relative;
            canvas.style.marginTop = 8;
            canvas.style.marginBottom = 8;
            canvas.style.height = flowchart != null ? flowchart.Height : 520f;
            canvas.style.minHeight = flowchart != null ? flowchart.Height : 520f;
            canvas.style.width = Length.Percent(100f);
            canvas.style.minWidth = 0;
            canvas.style.overflow = Overflow.Hidden;
            canvas.style.backgroundColor = new Color(0.045f, 0.055f, 0.065f);
            canvas.style.borderTopWidth = 1;
            canvas.style.borderBottomWidth = 1;
            canvas.style.borderLeftWidth = 1;
            canvas.style.borderRightWidth = 1;
            canvas.style.borderTopColor = new Color(0.18f, 0.23f, 0.28f);
            canvas.style.borderBottomColor = new Color(0.18f, 0.23f, 0.28f);
            canvas.style.borderLeftColor = new Color(0.18f, 0.23f, 0.28f);
            canvas.style.borderRightColor = new Color(0.18f, 0.23f, 0.28f);
            canvas.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.target != canvas)
                    return;

                ClearBehaviorCardFlowchartSelection();
                evt.StopPropagation();
            });

            var edgeLayer = new VisualElement();
            edgeLayer.name = BehaviorCardFlowchartEdgeLayerName;
            edgeLayer.pickingMode = PickingMode.Ignore;
            edgeLayer.style.position = Position.Absolute;
            edgeLayer.style.left = 0;
            edgeLayer.style.top = 0;
            edgeLayer.style.right = 0;
            edgeLayer.style.bottom = 0;
            canvas.Add(edgeLayer);

            for (int i = 0; flowchart != null && i < flowchart.Edges.Count; i++)
                edgeLayer.Add(BuildBehaviorCardFlowchartEdge(flowchart.Edges[i]));

            var nodeLayer = new VisualElement();
            nodeLayer.name = BehaviorCardFlowchartNodeLayerName;
            nodeLayer.pickingMode = PickingMode.Ignore;
            nodeLayer.style.position = Position.Absolute;
            nodeLayer.style.left = 0;
            nodeLayer.style.top = 0;
            nodeLayer.style.right = 0;
            nodeLayer.style.bottom = 0;
            canvas.Add(nodeLayer);

            for (int i = 0; flowchart != null && i < flowchart.Nodes.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel node =
                    flowchart.Nodes[i];
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step =
                    FindBehaviorCardStepById(draft, node.StepId);
                CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing =
                    FindBehaviorCardStepTiming(preview, node.StepId);
                nodeLayer.Add(BuildBehaviorCardFlowchartNode(node, step, timing));
            }

            return canvas;
        }

        private VisualElement BuildBehaviorCardFlowchartNode(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel node,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing)
        {
            if (node.Kind == CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind.Start)
                return BuildBehaviorCardFlowchartStartNode(node);
            if (node.Kind == CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind.Decision)
                return BuildBehaviorCardFlowchartDecisionNode(node);
            if (node.Kind == CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind.End)
                return BuildBehaviorCardFlowchartEndNode(node);

            int stepIndex = FindBehaviorCardStepIndexById(GetSelectedBehaviorCardDraft(), node.StepId);
            bool isSelected = stepIndex == _selectedBehaviorCardStepIndex
                && string.IsNullOrWhiteSpace(_selectedBehaviorCardBranchId);
            var card = CreateBehaviorCardSurface(
                isSelected
                    ? new Color(0.08f, 0.14f, 0.20f)
                    : new Color(0.075f, 0.095f, 0.115f),
                isSelected
                    ? new Color(0.24f, 0.52f, 0.90f)
                    : new Color(0.22f, 0.26f, 0.31f));
            card.name = BehaviorCardFlowchartActionNodeName;
            card.style.position = Position.Absolute;
            card.style.left = node.X;
            card.style.top = node.Y;
            card.style.width = node.Width;
            card.style.height = node.Height;
            card.style.marginBottom = 0;
            card.style.paddingLeft = 7;
            card.style.paddingRight = 7;
            card.style.paddingTop = 5;
            card.style.paddingBottom = 5;
            card.userData = node;
            card.RegisterCallback<MouseDownEvent>(evt =>
            {
                SelectBehaviorCardFlowchartNode(node);
                evt.StopPropagation();
            });

            if (step == null)
            {
                card.Add(CreateSingleLineLabel("Missing step", 12, new Color(0.86f, 0.90f, 0.94f)));
                return card;
            }

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 3;
            card.Add(header);

            Label index = CreateSingleLineLabel(
                (Math.Max(0, stepIndex) + 1).ToString(CultureInfo.InvariantCulture),
                11,
                new Color(0.86f, 0.90f, 0.94f));
            index.style.width = 22;
            index.style.height = 20;
            index.style.unityTextAlign = TextAnchor.MiddleCenter;
            index.style.unityFontStyleAndWeight = FontStyle.Bold;
            index.style.backgroundColor = new Color(0.11f, 0.16f, 0.20f);
            index.style.marginRight = 7;
            header.Add(index);

            Label title = CreateSingleLineLabel(step.DisplayTitle, 12, new Color(0.86f, 0.90f, 0.94f));
            title.tooltip = step.DisplayTitle;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            header.Add(title);

            card.Add(BuildBehaviorCardFlowchartActionSummary(step, timing));
            BuildBehaviorCardFlowchartCompactTiming(card, step, timing);
            return card;
        }

        private static VisualElement BuildBehaviorCardFlowchartActionSummary(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing)
        {
            var row = new VisualElement();
            row.name = BehaviorCardStepBranchPanelName;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;
            row.style.overflow = Overflow.Hidden;

            row.Add(CreateCompactBadge("执行", new Color(0.10f, 0.24f, 0.38f), 42f));

            CharacterTestRuntimeAiPlannerBehaviorCardActionDisplayView action =
                CharacterTestRuntimeAiPlannerBehaviorCardStandardInspector.BuildActionDisplay(step);
            Label actionName = CreateSingleLineLabel(action.DisplayName, 11, new Color(0.88f, 0.92f, 0.96f));
            actionName.tooltip = action.Tooltip;
            actionName.style.flexGrow = 1;
            actionName.style.minWidth = 0;
            row.Add(actionName);

            if (timing.DurationKnown)
            {
                row.Add(CreateCompactBadge(
                    FormatBehaviorCardFlowchartFrameRange(timing),
                    new Color(0.16f, 0.18f, 0.21f),
                    52f));
            }
            else if (!string.IsNullOrWhiteSpace(timing.WarningText))
            {
                Label warning = CreateCompactBadge("时长未知", new Color(0.35f, 0.24f, 0.10f), 64f);
                warning.name = BehaviorCardStepWarningChipName;
                warning.tooltip = timing.WarningText;
                row.Add(warning);
            }

            return row;
        }

        private static void BuildBehaviorCardFlowchartCompactTiming(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing)
        {
            if (!timing.DurationKnown)
                return;

            string summary = FormatBehaviorCardFlowchartTimingSummary(step, timing);
            if (string.IsNullOrWhiteSpace(summary))
                return;

            var track = new VisualElement();
            track.name = BehaviorCardStepTimelineTrackName;
            track.style.height = 18;
            track.style.minHeight = 18;
            track.style.marginTop = 1;
            track.style.backgroundColor = new Color(0.055f, 0.065f, 0.075f);
            track.style.borderTopWidth = 1;
            track.style.borderBottomWidth = 1;
            track.style.borderLeftWidth = 1;
            track.style.borderRightWidth = 1;
            track.style.borderTopColor = new Color(0.18f, 0.22f, 0.25f);
            track.style.borderBottomColor = new Color(0.18f, 0.22f, 0.25f);
            track.style.borderLeftColor = new Color(0.18f, 0.22f, 0.25f);
            track.style.borderRightColor = new Color(0.18f, 0.22f, 0.25f);
            root.Add(track);

            var compactMarker = new VisualElement();
            compactMarker.name = BehaviorCardStepCompactTimelineName;
            compactMarker.style.display = DisplayStyle.None;
            track.Add(compactMarker);

            Label summaryLabel = CreateSingleLineLabel(summary, 9, new Color(0.76f, 0.84f, 0.90f));
            summaryLabel.name = BehaviorCardStepTimelineEventName;
            summaryLabel.tooltip = summary;
            summaryLabel.style.paddingLeft = 5;
            summaryLabel.style.paddingRight = 5;
            summaryLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            track.Add(summaryLabel);
        }

        private static string FormatBehaviorCardFlowchartTimingSummary(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing)
        {
            string summary = FormatBehaviorCardFlowchartFrameRange(timing);
            if (step == null)
                return summary;

            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction
                && step.CharacterActionId == new CharacterActionId(301))
            {
                return summary + " | 命中 18-24 | 切换 62-72";
            }

            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction
                && step.CharacterActionId == new CharacterActionId(324))
            {
                return summary + " | 命中 20-26";
            }

            if (timing.HasConditionWindow)
                return summary + " | 判断 " + FormatFrameRange(timing);

            return summary;
        }

        private static string FormatBehaviorCardFlowchartFrameRange(
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing)
        {
            return "0"
                + "-"
                + timing.DurationFrames.ToString(CultureInfo.InvariantCulture)
                + "f";
        }

        private static VisualElement BuildBehaviorCardFlowchartStartNode(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel node)
        {
            var start = CreateBehaviorCardSurface(
                new Color(0.08f, 0.10f, 0.12f),
                new Color(0.42f, 0.48f, 0.56f));
            start.name = BehaviorCardFlowchartStartNodeName;
            start.style.position = Position.Absolute;
            start.style.left = node.X;
            start.style.top = node.Y;
            start.style.width = node.Width;
            start.style.height = node.Height;
            start.style.marginBottom = 0;
            start.style.alignItems = Align.Center;
            start.style.justifyContent = Justify.Center;
            start.style.paddingTop = 2;
            start.style.paddingBottom = 2;
            Label label = CreateSingleLineLabel("Start", 12, new Color(0.94f, 0.96f, 0.98f));
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            start.Add(label);
            return start;
        }

        private VisualElement BuildBehaviorCardFlowchartDecisionNode(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel node)
        {
            var decision = CreateBehaviorCardSurface(
                new Color(0.08f, 0.10f, 0.13f),
                string.Equals(node.BranchId, _selectedBehaviorCardBranchId, StringComparison.Ordinal)
                    ? new Color(0.30f, 0.62f, 1.0f)
                    : new Color(0.28f, 0.45f, 0.62f));
            decision.name = BehaviorCardFlowchartDecisionNodeName;
            decision.style.position = Position.Absolute;
            decision.style.left = node.X;
            decision.style.top = node.Y;
            decision.style.width = node.Width;
            decision.style.height = node.Height;
            decision.style.marginBottom = 0;
            decision.style.paddingLeft = 7;
            decision.style.paddingRight = 7;
            decision.style.paddingTop = 4;
            decision.style.paddingBottom = 4;
            decision.style.alignItems = Align.Center;
            decision.style.justifyContent = Justify.Center;
            decision.userData = node;
            decision.RegisterCallback<MouseDownEvent>(evt =>
            {
                SelectBehaviorCardFlowchartNode(node);
                evt.StopPropagation();
            });

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.Center;
            decision.Add(row);
            Label diamond = CreateSingleLineLabel("◇", 13, new Color(0.55f, 0.76f, 1.0f));
            diamond.style.width = 20;
            diamond.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(diamond);
            Label title = CreateSingleLineLabel(node.Title, 10, new Color(0.90f, 0.94f, 0.98f));
            title.tooltip = node.Title + " | " + node.Subtitle;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(title);
            return decision;
        }

        private static VisualElement BuildBehaviorCardFlowchartEndNode(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel node)
        {
            var end = CreateBehaviorCardSurface(
                new Color(0.09f, 0.10f, 0.12f),
                new Color(0.40f, 0.46f, 0.52f));
            end.name = BehaviorCardFlowchartEndNodeName;
            end.style.position = Position.Absolute;
            end.style.left = node.X;
            end.style.top = node.Y;
            end.style.width = node.Width;
            end.style.height = node.Height;
            end.style.marginBottom = 0;
            end.style.alignItems = Align.Center;
            end.style.paddingTop = 6;
            end.style.paddingBottom = 6;
            end.Add(CreateSingleLineLabel("End", 13, new Color(0.92f, 0.94f, 0.96f)));
            end.Add(CreateSingleLineLabel(node.Subtitle, 10, new Color(0.74f, 0.80f, 0.86f)));
            return end;
        }

        private VisualElement BuildBehaviorCardFlowchartEdge(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel edge)
        {
            var group = new VisualElement();
            group.name = BehaviorCardFlowchartEdgeName;
            group.pickingMode = PickingMode.Ignore;
            group.style.position = Position.Absolute;
            group.style.left = 0;
            group.style.top = 0;
            group.style.right = 0;
            group.style.bottom = 0;
            group.userData = edge;

            Color color = string.IsNullOrWhiteSpace(edge.BranchId)
                ? new Color(0.34f, 0.42f, 0.48f)
                : GetBehaviorCardBranchColor(edge.Kind);
            AddBehaviorCardFlowchartSegment(group, edge.StartX, edge.StartY, edge.LaneX, edge.StartY, color, edge);
            AddBehaviorCardFlowchartSegment(group, edge.LaneX, edge.StartY, edge.LaneX, edge.EndY, color, edge);
            AddBehaviorCardFlowchartSegment(group, edge.LaneX, edge.EndY, edge.EndX, edge.EndY, color, edge);

            if (!string.IsNullOrWhiteSpace(edge.Label))
            {
                Label label = CreateCompactBadge(edge.Label, color, 92f);
                label.name = BehaviorCardFlowchartEdgeLabelName;
                label.userData = edge;
                label.tooltip = FormatBehaviorCardFlowchartEdgeTooltip(edge);
                label.style.position = Position.Absolute;
                label.style.left = edge.LabelX;
                label.style.top = edge.LabelY;
                label.RegisterCallback<MouseDownEvent>(evt =>
                {
                    SelectBehaviorCardFlowchartEdge(edge);
                    evt.StopPropagation();
                });
                group.Add(label);
            }

            return group;
        }

        private void AddBehaviorCardFlowchartSegment(
            VisualElement root,
            float x1,
            float y1,
            float x2,
            float y2,
            Color color,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel edge)
        {
            float width = Math.Abs(x2 - x1);
            float height = Math.Abs(y2 - y1);
            if (width < 0.01f && height < 0.01f)
                return;

            var segment = new VisualElement();
            segment.style.position = Position.Absolute;
            segment.style.backgroundColor = color;
            segment.pickingMode = string.IsNullOrWhiteSpace(edge.BranchId)
                ? PickingMode.Ignore
                : PickingMode.Position;
            if (!string.IsNullOrWhiteSpace(edge.BranchId))
            {
                segment.RegisterCallback<MouseDownEvent>(evt =>
                {
                    SelectBehaviorCardFlowchartEdge(edge);
                    evt.StopPropagation();
                });
            }

            bool horizontal = width >= height;
            segment.style.left = Math.Min(x1, x2);
            segment.style.top = Math.Min(y1, y2);
            segment.style.width = horizontal ? Math.Max(2f, width) : 3f;
            segment.style.height = horizontal ? 3f : Math.Max(2f, height);
            root.Add(segment);
        }

        private static string FormatBehaviorCardFlowchartEdgeTooltip(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel edge)
        {
            if (string.IsNullOrWhiteSpace(edge.BranchId))
                return edge.FromNodeId + " -> " + edge.ToNodeId;

            return edge.BranchId
                + " | "
                + edge.FromStepId
                + " -> "
                + (edge.TargetIsEnd ? "End" : edge.TargetStepId);
        }

        private void SelectBehaviorCardFlowchartNode(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel node)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = GetSelectedBehaviorCardDraft();
            if (draft == null)
                return;

            int sourceIndex = FindBehaviorCardStepIndexById(draft, node.StepId);
            if (sourceIndex >= 0)
                _selectedBehaviorCardStepIndex = sourceIndex;
            _selectedBehaviorCardBranchId = node.BranchId ?? string.Empty;
            RefreshBehaviorCardViewPreservingScroll();
        }

        private void SelectBehaviorCardFlowchartEdge(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel edge)
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

        private void ClearBehaviorCardFlowchartSelection()
        {
            if (_selectedBehaviorCardStepIndex < 0 && string.IsNullOrWhiteSpace(_selectedBehaviorCardBranchId))
                return;

            _selectedBehaviorCardStepIndex = -1;
            _selectedBehaviorCardBranchId = string.Empty;
            RefreshBehaviorCardViewPreservingScroll();
        }

    }
}
