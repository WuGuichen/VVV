using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestRuntimeAiPlannerFlowPreview
    {
        internal enum FlowEdgeKind
        {
            Hierarchy,
            InitialPath,
            Transition
        }

        private readonly struct FlowMetrics
        {
            private FlowMetrics(CharacterTestRuntimeAiPlannerFlowPreviewMode mode, float zoom)
            {
                if (mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview)
                {
                    Margin = 12f * zoom;
                    ColumnWidth = 148f * zoom;
                    RowHeight = 66f * zoom;
                    RowGap = 12f * zoom;
                    NodeWidth = 132f * zoom;
                    NodeHeight = 54f * zoom;
                    EdgeLabelWidth = 116f * zoom;
                    EdgeLabelHeight = 20f * zoom;
                    EdgeLabelGap = 4f * zoom;
                    LegendWidth = 108f * zoom;
                    LegendHeight = 30f * zoom;
                    NodePaddingX = 6f * zoom;
                    NodePaddingY = 5f * zoom;
                    TitleFontSize = Mathf.Max(7f, 11f * zoom);
                    SmallFontSize = Mathf.Max(7f, 9f * zoom);
                    return;
                }

                Margin = 18f * zoom;
                ColumnWidth = 250f * zoom;
                RowHeight = 104f * zoom;
                RowGap = 18f * zoom;
                NodeWidth = 210f * zoom;
                NodeHeight = 78f * zoom;
                EdgeLabelWidth = 320f * zoom;
                EdgeLabelHeight = 36f * zoom;
                EdgeLabelGap = 6f * zoom;
                LegendWidth = 150f * zoom;
                LegendHeight = 36f * zoom;
                NodePaddingX = 9f * zoom;
                NodePaddingY = 7f * zoom;
                TitleFontSize = Mathf.Max(10f, 13f * zoom);
                SmallFontSize = Mathf.Max(9f, 11f * zoom);
            }

            public float Margin { get; }
            public float ColumnWidth { get; }
            public float RowHeight { get; }
            public float RowGap { get; }
            public float NodeWidth { get; }
            public float NodeHeight { get; }
            public float EdgeLabelWidth { get; }
            public float EdgeLabelHeight { get; }
            public float EdgeLabelGap { get; }
            public float LegendWidth { get; }
            public float LegendHeight { get; }
            public float NodePaddingX { get; }
            public float NodePaddingY { get; }
            public float TitleFontSize { get; }
            public float SmallFontSize { get; }

            public static FlowMetrics Create(CharacterTestRuntimeAiPlannerFlowPreviewMode mode, float zoom)
            {
                return new FlowMetrics(mode, ClampZoom(zoom, mode));
            }
        }

        private readonly struct LayoutInput
        {
            public LayoutInput(
                FlowMetrics metrics,
                IReadOnlyDictionary<string, Rect> nodeRects,
                Rect globalRect,
                bool hasMissingTarget,
                Rect missingTargetRect,
                IReadOnlyList<EdgeDrawData> edges,
                IReadOnlyList<RuleDrawData> rules,
                IReadOnlyList<Rect> occupiedRects,
                Rect contentBounds,
                Rect canvasBounds)
            {
                Metrics = metrics;
                NodeRects = nodeRects;
                GlobalRect = globalRect;
                HasMissingTarget = hasMissingTarget;
                MissingTargetRect = missingTargetRect;
                Edges = edges;
                Rules = rules;
                OccupiedRects = occupiedRects;
                ContentBounds = contentBounds;
                CanvasBounds = canvasBounds;
            }

            public FlowMetrics Metrics { get; }
            public IReadOnlyDictionary<string, Rect> NodeRects { get; }
            public Rect GlobalRect { get; }
            public bool HasMissingTarget { get; }
            public Rect MissingTargetRect { get; }
            public IReadOnlyList<EdgeDrawData> Edges { get; }
            public IReadOnlyList<RuleDrawData> Rules { get; }
            public IReadOnlyList<Rect> OccupiedRects { get; }
            public Rect ContentBounds { get; }
            public Rect CanvasBounds { get; }
        }

        internal readonly struct FlowGraphLayoutDebugSnapshot
        {
            public FlowGraphLayoutDebugSnapshot(
                IReadOnlyDictionary<string, Rect> nodeRects,
                Rect globalRect,
                bool hasMissingTarget,
                Rect missingTargetRect,
                IReadOnlyList<RuleDrawData> rules,
                IReadOnlyList<EdgeDrawData> edges,
                IReadOnlyList<Rect> occupiedRects)
            {
                NodeRects = nodeRects;
                GlobalRect = globalRect;
                HasMissingTarget = hasMissingTarget;
                MissingTargetRect = missingTargetRect;
                Rules = rules;
                Edges = edges;
                OccupiedRects = occupiedRects;
            }

            public IReadOnlyDictionary<string, Rect> NodeRects { get; }
            public Rect GlobalRect { get; }
            public bool HasMissingTarget { get; }
            public Rect MissingTargetRect { get; }
            public IReadOnlyList<RuleDrawData> Rules { get; }
            public IReadOnlyList<EdgeDrawData> Edges { get; }
            public IReadOnlyList<Rect> OccupiedRects { get; }
        }

        internal readonly struct EdgeDrawData
        {
            public EdgeDrawData(
                FlowEdgeKind kind,
                int transitionIndex,
                Vector2 start,
                Vector2 end,
                IReadOnlyList<Vector2> routePoints,
                bool drawArrow,
                Color color,
                bool selected,
                CharacterTestRuntimeAiPlannerDiagnosticSeverity severity)
            {
                Kind = kind;
                TransitionIndex = transitionIndex;
                Start = start;
                End = end;
                RoutePoints = routePoints ?? Array.Empty<Vector2>();
                DrawArrow = drawArrow;
                Color = color;
                Selected = selected;
                Severity = severity;
            }

            public FlowEdgeKind Kind { get; }
            public int TransitionIndex { get; }
            public Vector2 Start { get; }
            public Vector2 End { get; }
            public IReadOnlyList<Vector2> RoutePoints { get; }
            public bool DrawArrow { get; }
            public Color Color { get; }
            public bool Selected { get; }
            public CharacterTestRuntimeAiPlannerDiagnosticSeverity Severity { get; }
        }

        internal readonly struct RuleDrawData
        {
            public RuleDrawData(
                int transitionIndex,
                Rect rect,
                string label,
                string tooltip,
                Color color,
                bool selected,
                CharacterTestRuntimeAiPlannerDiagnosticSeverity severity)
            {
                TransitionIndex = transitionIndex;
                Rect = rect;
                Label = label ?? string.Empty;
                Tooltip = tooltip ?? string.Empty;
                Color = color;
                Selected = selected;
                Severity = severity;
            }

            public int TransitionIndex { get; }
            public Rect Rect { get; }
            public string Label { get; }
            public string Tooltip { get; }
            public Color Color { get; }
            public bool Selected { get; }
            public CharacterTestRuntimeAiPlannerDiagnosticSeverity Severity { get; }
        }

        private sealed class EdgeLineLayer : VisualElement
        {
            private readonly IReadOnlyList<EdgeDrawData> _edges;

            public EdgeLineLayer(IReadOnlyList<EdgeDrawData> edges)
            {
                _edges = edges ?? Array.Empty<EdgeDrawData>();
                style.position = Position.Absolute;
                style.left = 0;
                style.right = 0;
                style.top = 0;
                style.bottom = 0;
                generateVisualContent += OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                for (int i = 0; i < _edges.Count; i++)
                {
                    EdgeDrawData edge = _edges[i];
                    painter.strokeColor = edge.Color;
                    painter.lineWidth = ResolveLineWidth(edge);
                    painter.BeginPath();
                    IReadOnlyList<Vector2> route = edge.RoutePoints;
                    if (route.Count >= 2)
                    {
                        painter.MoveTo(route[0]);
                        for (int pointIndex = 1; pointIndex < route.Count; pointIndex++)
                            painter.LineTo(route[pointIndex]);
                    }
                    else
                    {
                        painter.MoveTo(edge.Start);
                        painter.LineTo(edge.End);
                    }

                    painter.Stroke();
                    if (edge.DrawArrow)
                        DrawArrow(painter, edge);
                }
            }

            private static float ResolveLineWidth(EdgeDrawData edge)
            {
                if (edge.Kind == FlowEdgeKind.InitialPath)
                    return 3f;
                if (edge.Kind == FlowEdgeKind.Hierarchy)
                    return 1.4f;

                return edge.Selected ? 3.2f : 1.8f;
            }

            private static void DrawArrow(Painter2D painter, EdgeDrawData edge)
            {
                Vector2 start = edge.Start;
                Vector2 end = edge.End;
                IReadOnlyList<Vector2> route = edge.RoutePoints;
                if (route.Count >= 2)
                {
                    start = route[route.Count - 2];
                    end = route[route.Count - 1];
                }

                Vector2 direction = end - start;
                if (direction.sqrMagnitude < 0.01f)
                    return;

                direction.Normalize();
                Vector2 normal = new Vector2(-direction.y, direction.x);
                const float arrow = 7f;
                Vector2 tip = end;
                Vector2 a = tip - direction * arrow + normal * (arrow * 0.55f);
                Vector2 b = tip - direction * arrow - normal * (arrow * 0.55f);

                painter.BeginPath();
                painter.MoveTo(a);
                painter.LineTo(tip);
                painter.LineTo(b);
                painter.Stroke();
            }

        }
    }
}
