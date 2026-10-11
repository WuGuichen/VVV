using System;
using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestRuntimeAiPlannerFlowPreview
    {
        private const int MaxPlacementSearchRadius = 48;

        private LayoutInput BuildGraphLayout(FlowMetrics metrics, float previewHeight)
        {
            Dictionary<string, int> rows = AssignStateRows();
            Dictionary<int, int> globalRuleCountsByRow = BuildGlobalRuleCountsByRow(rows);
            Dictionary<string, Rect> nodeRects = BuildStateRects(
                metrics,
                rows,
                globalRuleCountsByRow,
                out int maxDepth,
                out float stateRowsBottom);
            Rect globalRect = CreateGlobalLegendRect(metrics);

            var occupied = new List<Rect>(nodeRects.Count + _model.Transitions.Count + 1);
            foreach (KeyValuePair<string, Rect> pair in nodeRects)
                occupied.Add(pair.Value);

            bool hasMissingTarget = HasMissingTarget();
            Rect missingTargetRect = Rect.zero;
            if (hasMissingTarget)
            {
                Rect desiredMissing = new Rect(
                    metrics.Margin + metrics.ColumnWidth * (maxDepth + 2),
                    stateRowsBottom + metrics.EdgeLabelGap * 3f,
                    metrics.NodeWidth,
                    metrics.NodeHeight);
                missingTargetRect = FindFreeRect(
                    metrics,
                    desiredMissing,
                    CreateSingleCandidate(desiredMissing.position),
                    occupied);
                occupied.Add(missingTargetRect);
            }

            List<RuleDrawData> rules = BuildRuleNodes(
                metrics,
                nodeRects,
                missingTargetRect,
                hasMissingTarget,
                occupied);
            List<EdgeDrawData> edges = BuildGraphEdges(
                metrics,
                nodeRects,
                globalRect,
                missingTargetRect,
                hasMissingTarget,
                rules,
                occupied);
            Rect bounds = CalculateBounds(
                metrics,
                nodeRects,
                globalRect,
                hasMissingTarget,
                missingTargetRect,
                rules,
                edges);

            float canvasWidth = _mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview
                ? _viewportWidth
                : Math.Max(bounds.xMax, _viewportWidth);
            float canvasHeight = _mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview
                ? previewHeight
                : Math.Max(bounds.yMax, previewHeight);
            Vector2 contentOffset = CalculateContentOffset(bounds, canvasWidth, canvasHeight);

            return new LayoutInput(
                metrics,
                OffsetRects(nodeRects, contentOffset),
                OffsetRect(globalRect, contentOffset),
                hasMissingTarget,
                OffsetRect(missingTargetRect, contentOffset),
                OffsetEdges(edges, contentOffset),
                OffsetRules(rules, contentOffset),
                OffsetRectList(occupied, contentOffset),
                bounds,
                new Rect(0f, 0f, canvasWidth, canvasHeight));
        }

        private Dictionary<int, int> BuildGlobalRuleCountsByRow(IReadOnlyDictionary<string, int> rows)
        {
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < _model.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition = _model.Transitions[i];
                if (transition == null
                    || !string.IsNullOrWhiteSpace(transition.SourceStateId)
                    || string.IsNullOrWhiteSpace(transition.TargetStateId)
                    || !rows.TryGetValue(transition.TargetStateId, out int row))
                {
                    continue;
                }

                counts.TryGetValue(row, out int count);
                counts[row] = count + 1;
            }

            return counts;
        }

        internal FlowGraphLayoutDebugSnapshot CreateDebugLayoutSnapshotForTests(
            float zoom,
            float viewportHeight)
        {
                LayoutInput layout = BuildGraphLayout(FlowMetrics.Create(_mode, zoom), viewportHeight);
            return new FlowGraphLayoutDebugSnapshot(
                layout.NodeRects,
                layout.GlobalRect,
                layout.HasMissingTarget,
                layout.MissingTargetRect,
                layout.Rules,
                layout.Edges,
                layout.OccupiedRects);
        }

        private Dictionary<string, int> AssignStateRows()
        {
            var rows = new Dictionary<string, int>(StringComparer.Ordinal);
            HashSet<string> initialLineage = BuildInitialLineage();
            int nextRow = 0;

            for (int i = 0; i < _model.States.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft state = _model.States[i];
                if (state == null
                    || string.IsNullOrWhiteSpace(state.StateId)
                    || !string.IsNullOrWhiteSpace(state.ParentStateId)
                    || !initialLineage.Contains(state.StateId))
                {
                    continue;
                }

                AssignStateSubtreeRows(state.StateId, 0, rows, initialLineage, ref nextRow);
                nextRow = Math.Max(nextRow, 1);
            }

            for (int i = 0; i < _model.States.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft state = _model.States[i];
                if (state == null
                    || string.IsNullOrWhiteSpace(state.StateId)
                    || rows.ContainsKey(state.StateId)
                    || !string.IsNullOrWhiteSpace(state.ParentStateId))
                {
                    continue;
                }

                AssignStateSubtreeRows(state.StateId, nextRow, rows, initialLineage, ref nextRow);
                nextRow++;
            }

            for (int i = 0; i < _model.States.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft state = _model.States[i];
                if (state == null
                    || string.IsNullOrWhiteSpace(state.StateId)
                    || rows.ContainsKey(state.StateId))
                {
                    continue;
                }

                AssignStateSubtreeRows(state.StateId, nextRow, rows, initialLineage, ref nextRow);
                nextRow++;
            }

            return rows;
        }

        private void AssignStateSubtreeRows(
            string stateId,
            int row,
            Dictionary<string, int> rows,
            HashSet<string> initialLineage,
            ref int nextRow)
        {
            if (string.IsNullOrWhiteSpace(stateId) || rows.ContainsKey(stateId))
                return;

            rows[stateId] = row;
            nextRow = Math.Max(nextRow, row + 1);

            string initialChild = string.Empty;
            for (int i = 0; i < _model.States.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft child = _model.States[i];
                if (child != null
                    && string.Equals(child.ParentStateId, stateId, StringComparison.Ordinal)
                    && initialLineage.Contains(child.StateId))
                {
                    initialChild = child.StateId;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(initialChild))
                AssignStateSubtreeRows(initialChild, row, rows, initialLineage, ref nextRow);

            for (int i = 0; i < _model.States.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft child = _model.States[i];
                if (child == null
                    || string.IsNullOrWhiteSpace(child.StateId)
                    || !string.Equals(child.ParentStateId, stateId, StringComparison.Ordinal)
                    || string.Equals(child.StateId, initialChild, StringComparison.Ordinal)
                    || rows.ContainsKey(child.StateId))
                {
                    continue;
                }

                AssignStateSubtreeRows(child.StateId, nextRow, rows, initialLineage, ref nextRow);
            }
        }

        private Dictionary<string, Rect> BuildStateRects(
            FlowMetrics metrics,
            IReadOnlyDictionary<string, int> rows,
            IReadOnlyDictionary<int, int> globalRuleCountsByRow,
            out int maxDepth,
            out float stateRowsBottom)
        {
            var nodeRects = new Dictionary<string, Rect>(StringComparer.Ordinal);
            maxDepth = 0;
            int maxRows = 0;

            for (int i = 0; i < _model.States.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft state = _model.States[i];
                if (state == null || string.IsNullOrWhiteSpace(state.StateId))
                    continue;

                int depth = Math.Max(0, _model.GetStateDepth(state.StateId));
                rows.TryGetValue(state.StateId, out int row);
                maxDepth = Math.Max(maxDepth, depth);
                maxRows = Math.Max(maxRows, row + 1);
            }

            var rowTops = new Dictionary<int, float>();
            float cursor = metrics.Margin;
            for (int row = 0; row < maxRows; row++)
            {
                globalRuleCountsByRow.TryGetValue(row, out int globalRuleCount);
                cursor += CalculateGlobalRuleBand(metrics, globalRuleCount);
                rowTops[row] = cursor;
                cursor += metrics.NodeHeight + metrics.RowGap;
            }

            stateRowsBottom = cursor;

            for (int i = 0; i < _model.States.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft state = _model.States[i];
                if (state == null || string.IsNullOrWhiteSpace(state.StateId))
                    continue;

                int depth = Math.Max(0, _model.GetStateDepth(state.StateId));
                rows.TryGetValue(state.StateId, out int row);
                rowTops.TryGetValue(row, out float rowTop);
                nodeRects[state.StateId] = new Rect(
                    metrics.Margin + metrics.ColumnWidth * (depth + 1),
                    rowTop,
                    metrics.NodeWidth,
                    metrics.NodeHeight);
            }

            return nodeRects;
        }

        private static Rect CreateGlobalLegendRect(FlowMetrics metrics)
        {
            return new Rect(
                metrics.Margin,
                metrics.Margin,
                metrics.LegendWidth,
                metrics.LegendHeight);
        }

        private HashSet<string> BuildInitialLineage()
        {
            var lineage = new HashSet<string>(StringComparer.Ordinal);
            string current = _model.InitialStateId;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft state = _model.FindState(current);
                if (state == null)
                    break;

                lineage.Add(state.StateId);
                current = state.ParentStateId;
            }

            return lineage;
        }

        private bool HasMissingTarget()
        {
            for (int i = 0; i < _model.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition = _model.Transitions[i];
                if (transition == null
                    || string.IsNullOrWhiteSpace(transition.TargetStateId)
                    || !_model.HasState(transition.TargetStateId))
                {
                    return true;
                }
            }

            return false;
        }

        private List<RuleDrawData> BuildRuleNodes(
            FlowMetrics metrics,
            IReadOnlyDictionary<string, Rect> nodeRects,
            Rect missingTargetRect,
            bool hasMissingTarget,
            List<Rect> occupied)
        {
            var indices = new List<int>(_model.Transitions.Count);
            for (int i = 0; i < _model.Transitions.Count; i++)
                indices.Add(i);
            indices.Sort(CompareTransitionIndices);

            var rules = new List<RuleDrawData>(indices.Count);
            var lanesByTarget = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition = _model.Transitions[index];
                if (transition == null)
                    continue;

                Rect targetRect = Rect.zero;
                bool targetMissing = string.IsNullOrWhiteSpace(transition.TargetStateId)
                    || !nodeRects.TryGetValue(transition.TargetStateId, out targetRect);
                if (targetMissing)
                    targetRect = missingTargetRect;

                Rect sourceRect = Rect.zero;
                bool globalTransition = string.IsNullOrWhiteSpace(transition.SourceStateId);
                bool sourceMissing = !globalTransition
                    && !nodeRects.TryGetValue(transition.SourceStateId, out sourceRect);
                string laneKey = (globalTransition ? "<global>" : transition.SourceStateId)
                    + "->"
                    + (targetMissing ? "<missing>" : transition.TargetStateId);
                lanesByTarget.TryGetValue(laneKey, out int lane);
                lanesByTarget[laneKey] = lane + 1;

                bool useGlobalEntryLayout = globalTransition || sourceMissing;
                Rect desired = useGlobalEntryLayout
                    ? CreateGlobalRuleDesired(metrics, targetRect, lane)
                    : new Rect(
                        targetRect.xMax + metrics.EdgeLabelGap * 3f,
                        targetRect.yMin + lane * (metrics.EdgeLabelHeight + metrics.EdgeLabelGap),
                        metrics.EdgeLabelWidth,
                        metrics.EdgeLabelHeight);
                Rect ruleRect = useGlobalEntryLayout
                    ? desired
                    : FindFreeRect(
                        metrics,
                        desired,
                        CreateSourceRuleCandidates(metrics, sourceRect, targetRect, lane),
                        occupied);
                occupied.Add(ruleRect);

                bool selected = index == _selectedTransitionIndex;
                CharacterTestRuntimeAiPlannerDiagnosticSeverity severity =
                    targetMissing
                        ? CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error
                        : GetTransitionSeverity(transition);
                CharacterTestRuntimeAiPlannerDiagnostic ruleDiagnostic = default;
                bool hasRuleDiagnostic =
                    CharacterTestRuntimeAiPlannerEditorRuleDiagnostics.TryCreateGlobalCoverageDiagnostic(
                        _model.Transitions,
                        transition,
                        out ruleDiagnostic);
                if (hasRuleDiagnostic)
                    severity = MaxSeverity(severity, ruleDiagnostic.Severity);
                Color color = ResolveLineColor(severity, selected);
                string label = FormatTransitionLabel(transition);
                if (severity == CharacterTestRuntimeAiPlannerDiagnosticSeverity.Warning)
                    label = "! " + label;
                else if (severity == CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error)
                    label = "!! " + label;
                string tooltip = CreateTransitionTooltip(transition);
                if (hasRuleDiagnostic)
                    tooltip = tooltip + "\n" + ruleDiagnostic.Code + ": " + ruleDiagnostic.Message;
                rules.Add(new RuleDrawData(
                    index,
                    ruleRect,
                    label,
                    tooltip,
                    color,
                    selected,
                    severity));
            }

            return rules;
        }

        private static Rect CreateGlobalRuleDesired(
            FlowMetrics metrics,
            Rect targetRect,
            int lane)
        {
            float width = metrics.EdgeLabelWidth;
            float height = metrics.EdgeLabelHeight;
            float offset = lane * CalculateGlobalRuleSlotStride(metrics);
            return new Rect(
                targetRect.center.x - width * 0.5f,
                targetRect.yMin - height - CalculateGlobalRuleTargetGap(metrics) - offset,
                width,
                height);
        }

        private static List<Vector2> CreateSourceRuleCandidates(
            FlowMetrics metrics,
            Rect sourceRect,
            Rect targetRect,
            int lane)
        {
            float width = metrics.EdgeLabelWidth;
            float height = metrics.EdgeLabelHeight;
            float gap = metrics.EdgeLabelGap * 3f;
            float offset = lane * (height + metrics.EdgeLabelGap);
            float centerX = (sourceRect.center.x + targetRect.center.x) * 0.5f - width * 0.5f;
            float centerY = (sourceRect.center.y + targetRect.center.y) * 0.5f - height * 0.5f;
            return new List<Vector2>
            {
                new Vector2(centerX, centerY + offset),
                new Vector2(sourceRect.xMax + gap, centerY + offset),
                new Vector2(targetRect.xMin - width - gap, centerY + offset),
                new Vector2(targetRect.xMax + gap, targetRect.yMin + offset),
                new Vector2(sourceRect.xMin - width - gap, sourceRect.yMin + offset),
                new Vector2(centerX, Math.Max(sourceRect.yMax, targetRect.yMax) + gap + offset)
            };
        }

        private List<EdgeDrawData> BuildGraphEdges(
            FlowMetrics metrics,
            IReadOnlyDictionary<string, Rect> nodeRects,
            Rect globalRect,
            Rect missingTargetRect,
            bool hasMissingTarget,
            IReadOnlyList<RuleDrawData> rules,
            IReadOnlyList<Rect> occupied)
        {
            var edges = new List<EdgeDrawData>(_model.States.Count + rules.Count * 2);
            HashSet<string> initialLineage = BuildInitialLineage();
            BuildHierarchyEdges(metrics, nodeRects, initialLineage, occupied, edges);

            for (int i = 0; i < rules.Count; i++)
            {
                RuleDrawData rule = rules[i];
                CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition =
                    _model.Transitions[rule.TransitionIndex];
                Rect targetRect = Rect.zero;
                bool targetMissing = transition == null
                    || string.IsNullOrWhiteSpace(transition.TargetStateId)
                    || !nodeRects.TryGetValue(transition.TargetStateId, out targetRect);
                if (targetMissing)
                    targetRect = missingTargetRect;

                bool selected = rule.TransitionIndex == _selectedTransitionIndex;
                Color color = ResolveLineColor(rule.Severity, selected);
                bool globalTransition = transition == null
                    || string.IsNullOrWhiteSpace(transition.SourceStateId);
                if (!globalTransition && nodeRects.TryGetValue(transition.SourceStateId, out Rect sourceRect))
                {
                    edges.Add(CreateRoutedEdge(
                        FlowEdgeKind.Transition,
                        rule.TransitionIndex,
                        sourceRect,
                        rule.Rect,
                        occupied,
                        color,
                        selected,
                        false,
                        metrics,
                        i));
                }

                edges.Add(globalTransition
                    ? CreateGlobalRuleEdge(
                        rule.TransitionIndex,
                        rule.Rect,
                        targetRect,
                        occupied,
                        color,
                        selected,
                        metrics,
                        i)
                    : CreateRoutedEdge(
                        FlowEdgeKind.Transition,
                        rule.TransitionIndex,
                        rule.Rect,
                        targetRect,
                        occupied,
                        color,
                        selected,
                        true,
                        metrics,
                        i));
            }

            return edges;
        }

        private void BuildHierarchyEdges(
            FlowMetrics metrics,
            IReadOnlyDictionary<string, Rect> nodeRects,
            HashSet<string> initialLineage,
            IReadOnlyList<Rect> occupied,
            List<EdgeDrawData> edges)
        {
            for (int i = 0; i < _model.States.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft state = _model.States[i];
                if (state == null
                    || string.IsNullOrWhiteSpace(state.StateId)
                    || string.IsNullOrWhiteSpace(state.ParentStateId)
                    || !nodeRects.TryGetValue(state.StateId, out Rect childRect)
                    || !nodeRects.TryGetValue(state.ParentStateId, out Rect parentRect))
                {
                    continue;
                }

                bool initialPath = initialLineage.Contains(state.StateId)
                    && initialLineage.Contains(state.ParentStateId);
                edges.Add(CreateRoutedEdge(
                    initialPath ? FlowEdgeKind.InitialPath : FlowEdgeKind.Hierarchy,
                    -1,
                    parentRect,
                    childRect,
                    occupied,
                    initialPath
                        ? new Color(0.42f, 0.74f, 0.42f)
                        : new Color(0.36f, 0.38f, 0.42f),
                    false,
                    initialPath,
                    metrics,
                    i));
            }
        }

        private EdgeDrawData CreateRoutedEdge(
            FlowEdgeKind kind,
            int transitionIndex,
            Rect sourceRect,
            Rect targetRect,
            IReadOnlyList<Rect> occupied,
            Color color,
            bool selected,
            bool drawArrow,
            FlowMetrics metrics,
            int lane)
        {
            Vector2 start = CalculatePort(sourceRect, targetRect);
            Vector2 end = CalculatePort(targetRect, sourceRect);
            List<Vector2> route = RouteOrthogonal(start, end, sourceRect, targetRect, occupied, metrics, lane);
            return new EdgeDrawData(
                kind,
                transitionIndex,
                start,
                end,
                route,
                drawArrow,
                color,
                selected,
                CharacterTestRuntimeAiPlannerDiagnosticSeverity.Info);
        }

        private EdgeDrawData CreateGlobalRuleEdge(
            int transitionIndex,
            Rect ruleRect,
            Rect targetRect,
            IReadOnlyList<Rect> occupied,
            Color color,
            bool selected,
            FlowMetrics metrics,
            int lane)
        {
            Vector2 start = new Vector2(ruleRect.center.x, ruleRect.yMax);
            Vector2 end = new Vector2(targetRect.center.x, targetRect.yMin);
            List<Vector2> route = RouteOrthogonal(start, end, ruleRect, targetRect, occupied, metrics, lane);
            return new EdgeDrawData(
                FlowEdgeKind.Transition,
                transitionIndex,
                start,
                end,
                route,
                true,
                color,
                selected,
                CharacterTestRuntimeAiPlannerDiagnosticSeverity.Info);
        }

        private static Vector2 CalculatePort(Rect source, Rect target)
        {
            Vector2 delta = target.center - source.center;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x >= 0f
                    ? new Vector2(source.xMax, source.center.y)
                    : new Vector2(source.xMin, source.center.y);
            }

            return delta.y >= 0f
                ? new Vector2(source.center.x, source.yMax)
                : new Vector2(source.center.x, source.yMin);
        }

        private static Rect FindFreeRect(
            FlowMetrics metrics,
            Rect desired,
            IReadOnlyList<Vector2> candidates,
            IReadOnlyList<Rect> occupied)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                Rect candidate = CreatePlacementRect(metrics, candidates[i], desired.width, desired.height);
                if (IsFree(candidate, occupied, metrics.EdgeLabelGap))
                    return candidate;
            }

            float stepX = Math.Max(desired.width + metrics.EdgeLabelGap * 3f, metrics.ColumnWidth * 0.5f);
            float stepY = Math.Max(desired.height + metrics.EdgeLabelGap * 3f, metrics.RowHeight * 0.5f);
            for (int radius = 1; radius <= MaxPlacementSearchRadius; radius++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (Math.Abs(x) != radius && Math.Abs(y) != radius)
                            continue;

                        Rect candidate = CreatePlacementRect(
                            metrics,
                            new Vector2(desired.x + x * stepX, desired.y + y * stepY),
                            desired.width,
                            desired.height);
                        if (IsFree(candidate, occupied, metrics.EdgeLabelGap))
                            return candidate;
                    }
                }
            }

            float fallbackY = metrics.Margin;
            for (int i = 0; i < occupied.Count; i++)
                fallbackY = Math.Max(fallbackY, occupied[i].yMax + metrics.EdgeLabelGap * 3f);

            return new Rect(Math.Max(metrics.Margin, desired.x), fallbackY, desired.width, desired.height);
        }

        private static Rect CreatePlacementRect(
            FlowMetrics metrics,
            Vector2 position,
            float width,
            float height)
        {
            return new Rect(
                Math.Max(metrics.Margin, position.x),
                Math.Max(metrics.Margin, position.y),
                width,
                height);
        }

        private static List<Vector2> CreateSingleCandidate(Vector2 candidate)
        {
            return new List<Vector2> { candidate };
        }

        private static float CalculateGlobalRuleBand(FlowMetrics metrics, int ruleCount)
        {
            if (ruleCount <= 0)
                return 0f;

            return (ruleCount - 1) * CalculateGlobalRuleSlotStride(metrics)
                + metrics.EdgeLabelHeight
                + CalculateGlobalRuleTargetGap(metrics)
                + CalculateGlobalRuleTopPadding(metrics);
        }

        private static float CalculateGlobalRuleSlotStride(FlowMetrics metrics)
        {
            return metrics.EdgeLabelHeight + metrics.EdgeLabelGap * 2f;
        }

        private static float CalculateGlobalRuleTargetGap(FlowMetrics metrics)
        {
            return metrics.EdgeLabelGap * 2f;
        }

        private static float CalculateGlobalRuleTopPadding(FlowMetrics metrics)
        {
            return metrics.EdgeLabelGap * 2f;
        }

        private static List<Vector2> RouteOrthogonal(
            Vector2 start,
            Vector2 end,
            Rect sourceRect,
            Rect targetRect,
            IReadOnlyList<Rect> occupied,
            FlowMetrics metrics,
            int lane)
        {
            List<Rect> obstacles = CreateRouteObstacles(occupied, sourceRect, targetRect, metrics.EdgeLabelGap);
            List<float> xs = CreateRouteCoordinates(start.x, end.x, obstacles, true, metrics, lane);
            List<float> ys = CreateRouteCoordinates(start.y, end.y, obstacles, false, metrics, lane);
            int startX = IndexOfCoordinate(xs, start.x);
            int startY = IndexOfCoordinate(ys, start.y);
            int endX = IndexOfCoordinate(xs, end.x);
            int endY = IndexOfCoordinate(ys, end.y);
            int xCount = xs.Count;
            int yCount = ys.Count;
            int nodeCount = xCount * yCount;
            var previous = new int[nodeCount];
            var visited = new bool[nodeCount];
            for (int i = 0; i < previous.Length; i++)
                previous[i] = -1;

            int startIndex = startY * xCount + startX;
            int endIndex = endY * xCount + endX;
            var queue = new Queue<int>();
            queue.Enqueue(startIndex);
            visited[startIndex] = true;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == endIndex)
                    break;

                int x = current % xCount;
                int y = current / xCount;
                TryVisitRouteNeighbor(current, x - 1, y, xs, ys, obstacles, visited, previous, queue);
                TryVisitRouteNeighbor(current, x + 1, y, xs, ys, obstacles, visited, previous, queue);
                TryVisitRouteNeighbor(current, x, y - 1, xs, ys, obstacles, visited, previous, queue);
                TryVisitRouteNeighbor(current, x, y + 1, xs, ys, obstacles, visited, previous, queue);
            }

            if (!visited[endIndex])
            {
                float outsideY = ys[ys.Count - 1];
                return CompressRoute(new List<Vector2>
                {
                    start,
                    new Vector2(start.x, outsideY),
                    new Vector2(end.x, outsideY),
                    end
                });
            }

            var route = new List<Vector2>();
            int cursor = endIndex;
            while (cursor >= 0)
            {
                int x = cursor % xCount;
                int y = cursor / xCount;
                route.Add(new Vector2(xs[x], ys[y]));
                if (cursor == startIndex)
                    break;
                cursor = previous[cursor];
            }

            route.Reverse();
            return CompressRoute(route);
        }

        private static List<Rect> CreateRouteObstacles(
            IReadOnlyList<Rect> occupied,
            Rect sourceRect,
            Rect targetRect,
            float padding)
        {
            var obstacles = new List<Rect>(occupied.Count);
            for (int i = 0; i < occupied.Count; i++)
            {
                Rect rect = occupied[i];
                if (RectsApproximatelyEqual(rect, sourceRect)
                    || RectsApproximatelyEqual(rect, targetRect))
                {
                    continue;
                }

                obstacles.Add(InflateRect(rect, padding));
            }

            return obstacles;
        }

        private static List<float> CreateRouteCoordinates(
            float start,
            float end,
            IReadOnlyList<Rect> obstacles,
            bool horizontalAxis,
            FlowMetrics metrics,
            int lane)
        {
            var values = new List<float>
            {
                start,
                end,
                metrics.Margin
            };
            float max = Math.Max(start, end);
            for (int i = 0; i < obstacles.Count; i++)
            {
                Rect obstacle = obstacles[i];
                if (horizontalAxis)
                {
                    values.Add(Math.Max(metrics.Margin, obstacle.xMin - metrics.EdgeLabelGap * 2f));
                    values.Add(obstacle.xMax + metrics.EdgeLabelGap * 2f);
                    max = Math.Max(max, obstacle.xMax);
                }
                else
                {
                    values.Add(Math.Max(metrics.Margin, obstacle.yMin - metrics.EdgeLabelGap * 2f));
                    values.Add(obstacle.yMax + metrics.EdgeLabelGap * 2f);
                    max = Math.Max(max, obstacle.yMax);
                }
            }

            values.Add(max + metrics.EdgeLabelGap * (6f + lane));
            SortUnique(values);
            return values;
        }

        private static void TryVisitRouteNeighbor(
            int current,
            int x,
            int y,
            IReadOnlyList<float> xs,
            IReadOnlyList<float> ys,
            IReadOnlyList<Rect> obstacles,
            bool[] visited,
            int[] previous,
            Queue<int> queue)
        {
            if (x < 0 || x >= xs.Count || y < 0 || y >= ys.Count)
                return;

            int index = y * xs.Count + x;
            if (visited[index])
                return;

            Vector2 point = new Vector2(xs[x], ys[y]);
            if (PointInsideAny(point, obstacles))
                return;

            int currentX = current % xs.Count;
            int currentY = current / xs.Count;
            Vector2 currentPoint = new Vector2(xs[currentX], ys[currentY]);
            if (SegmentIntersectsAny(currentPoint, point, obstacles))
                return;

            visited[index] = true;
            previous[index] = current;
            queue.Enqueue(index);
        }

        private static bool PointInsideAny(Vector2 point, IReadOnlyList<Rect> obstacles)
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                Rect obstacle = obstacles[i];
                if (point.x > obstacle.xMin
                    && point.x < obstacle.xMax
                    && point.y > obstacle.yMin
                    && point.y < obstacle.yMax)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool SegmentIntersectsAny(
            Vector2 start,
            Vector2 end,
            IReadOnlyList<Rect> obstacles)
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                if (SegmentIntersectsRect(start, end, obstacles[i]))
                    return true;
            }

            return false;
        }

        internal static bool SegmentIntersectsRect(Vector2 start, Vector2 end, Rect rect)
        {
            Rect segment = CreateSegmentRect(start, end, 0.5f);
            return RectsOverlap(segment, rect);
        }

        private static Rect CreateSegmentRect(Vector2 start, Vector2 end, float padding)
        {
            float minX = Math.Min(start.x, end.x) - padding;
            float minY = Math.Min(start.y, end.y) - padding;
            float maxX = Math.Max(start.x, end.x) + padding;
            float maxY = Math.Max(start.y, end.y) + padding;
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static List<Vector2> CompressRoute(List<Vector2> route)
        {
            if (route.Count <= 2)
                return route;

            var result = new List<Vector2> { route[0] };
            for (int i = 1; i < route.Count - 1; i++)
            {
                Vector2 previous = result[result.Count - 1];
                Vector2 current = route[i];
                Vector2 next = route[i + 1];
                bool sameX = Mathf.Abs(previous.x - current.x) < 0.01f
                    && Mathf.Abs(current.x - next.x) < 0.01f;
                bool sameY = Mathf.Abs(previous.y - current.y) < 0.01f
                    && Mathf.Abs(current.y - next.y) < 0.01f;
                if (!sameX && !sameY)
                    result.Add(current);
            }

            result.Add(route[route.Count - 1]);
            return result;
        }

        private int CompareTransitionIndices(int leftIndex, int rightIndex)
        {
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft left = _model.Transitions[leftIndex];
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft right = _model.Transitions[rightIndex];
            int target = string.Compare(
                left != null ? left.TargetStateId : string.Empty,
                right != null ? right.TargetStateId : string.Empty,
                StringComparison.Ordinal);
            if (target != 0)
                return target;

            int source = string.Compare(
                left != null ? left.SourceStateId : string.Empty,
                right != null ? right.SourceStateId : string.Empty,
                StringComparison.Ordinal);
            if (source != 0)
                return source;

            int priority = (right != null ? right.Priority : 0)
                .CompareTo(left != null ? left.Priority : 0);
            return priority != 0 ? priority : leftIndex.CompareTo(rightIndex);
        }

        private static bool IsFree(Rect rect, IReadOnlyList<Rect> occupied, float padding)
        {
            Rect padded = InflateRect(rect, padding);
            for (int i = 0; i < occupied.Count; i++)
            {
                if (RectsOverlap(padded, InflateRect(occupied[i], padding)))
                    return false;
            }

            return true;
        }

        internal static bool RectsOverlap(Rect left, Rect right)
        {
            return left.xMin < right.xMax
                && left.xMax > right.xMin
                && left.yMin < right.yMax
                && left.yMax > right.yMin;
        }

        private static Rect InflateRect(Rect rect, float padding)
        {
            return Rect.MinMaxRect(
                rect.xMin - padding,
                rect.yMin - padding,
                rect.xMax + padding,
                rect.yMax + padding);
        }

        private static bool RectsApproximatelyEqual(Rect left, Rect right)
        {
            return Mathf.Abs(left.x - right.x) < 0.01f
                && Mathf.Abs(left.y - right.y) < 0.01f
                && Mathf.Abs(left.width - right.width) < 0.01f
                && Mathf.Abs(left.height - right.height) < 0.01f;
        }

        private static int IndexOfCoordinate(IReadOnlyList<float> values, float value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (Mathf.Abs(values[i] - value) < 0.01f)
                    return i;
            }

            return 0;
        }

        private static void SortUnique(List<float> values)
        {
            values.Sort();
            for (int i = values.Count - 1; i > 0; i--)
            {
                if (Mathf.Abs(values[i] - values[i - 1]) < 0.01f)
                    values.RemoveAt(i);
            }
        }

        private static Rect CalculateBounds(
            FlowMetrics metrics,
            IReadOnlyDictionary<string, Rect> nodeRects,
            Rect globalRect,
            bool hasMissingTarget,
            Rect missingTargetRect,
            IReadOnlyList<RuleDrawData> rules,
            IReadOnlyList<EdgeDrawData> edges)
        {
            float maxX = 0f;
            float maxY = 0f;
            foreach (KeyValuePair<string, Rect> pair in nodeRects)
            {
                maxX = Math.Max(maxX, pair.Value.xMax);
                maxY = Math.Max(maxY, pair.Value.yMax);
            }

            if (hasMissingTarget)
            {
                maxX = Math.Max(maxX, missingTargetRect.xMax);
                maxY = Math.Max(maxY, missingTargetRect.yMax);
            }

            for (int i = 0; i < rules.Count; i++)
            {
                maxX = Math.Max(maxX, rules[i].Rect.xMax);
                maxY = Math.Max(maxY, rules[i].Rect.yMax);
            }

            for (int i = 0; i < edges.Count; i++)
            {
                EdgeDrawData edge = edges[i];
                for (int pointIndex = 0; pointIndex < edge.RoutePoints.Count; pointIndex++)
                {
                    Vector2 point = edge.RoutePoints[pointIndex];
                    maxX = Math.Max(maxX, point.x);
                    maxY = Math.Max(maxY, point.y);
                }
            }

            return new Rect(0f, 0f, maxX + metrics.Margin, maxY + metrics.Margin);
        }
    }
}
