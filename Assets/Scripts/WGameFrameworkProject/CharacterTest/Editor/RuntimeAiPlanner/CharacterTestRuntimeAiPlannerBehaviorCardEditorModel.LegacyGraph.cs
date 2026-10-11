using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestRuntimeAiPlannerBehaviorCardEditorModel
    {
        public static CharacterTestRuntimeAiPlannerBehaviorCardGraphViewModel BuildGraphViewModel(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview)
        {
            return BuildGraphViewModel(draft, preview, 760f);
        }

        public static CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardViewModel BuildFlowBoardViewModel(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview)
        {
            var board = new CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardViewModel();
            if (draft == null || draft.Steps.Count == 0)
                return board;

            if (preview == null)
                preview = BuildFlowTimePreview(draft);

            string currentStepId = draft.Steps[0] != null ? draft.Steps[0].StepId : string.Empty;
            var visited = new List<string>();
            for (int guard = 0; guard < draft.Steps.Count && !string.IsNullOrWhiteSpace(currentStepId); guard++)
            {
                if (ContainsStepId(visited, currentStepId))
                    break;

                visited.Add(currentStepId);
                board.MainPathStepIds.Add(currentStepId);

                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview nextBranch =
                    FindFlowBoardPrimaryBranch(preview, currentStepId);
                if (!IsConfiguredBranch(nextBranch) || nextBranch.TargetIsEnd)
                    break;

                currentStepId = nextBranch.TargetStepId;
            }

            for (int i = 0; i < board.MainPathStepIds.Count; i++)
            {
                string stepId = board.MainPathStepIds[i];
                string nextStepId = i + 1 < board.MainPathStepIds.Count
                    ? board.MainPathStepIds[i + 1]
                    : "End";
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview mainBranch =
                    FindFlowBoardBranchToTarget(preview, stepId, nextStepId);
                if (!IsConfiguredBranch(mainBranch) && string.Equals(nextStepId, "End", StringComparison.Ordinal))
                    mainBranch = FindFlowBoardEndBranch(preview, stepId);

                var step = new CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardStep(
                    stepId,
                    Math.Max(0, FindDraftStepIndex(draft, stepId)),
                    mainBranch);
                AddFlowBoardSideBranches(preview, step, mainBranch);
                board.Steps.Add(step);
            }

            board.EndIncomingBranchCount = preview != null && preview.Terminals.Count > 0
                ? preview.Terminals[0].IncomingBranchCount
                : 0;
            return board;
        }

        public static CharacterTestRuntimeAiPlannerBehaviorCardGraphViewModel BuildGraphViewModel(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            float availableWidth)
        {
            var graph = new CharacterTestRuntimeAiPlannerBehaviorCardGraphViewModel();
            if (draft == null)
                return graph;

            if (preview == null)
                preview = BuildFlowTimePreview(draft);

            float canvasWidth = ClampGraphValue(
                availableWidth > 0f ? availableWidth : 760f,
                420f,
                940f);
            float margin = canvasWidth < 560f ? 8f : 12f;
            float laneGap = canvasWidth < 560f ? 8f : 14f;
            float nodeWidth = Math.Min(
                210f,
                Math.Max(126f, (canvasWidth - margin * 2f - laneGap * 2f) / 3f));
            float nodeHeight = canvasWidth < 560f ? 74f : 80f;
            const float terminalHeight = 46f;
            float leftX = margin;
            float mainX = (canvasWidth - nodeWidth) * 0.5f;
            float rightX = canvasWidth - margin - nodeWidth;
            const float top = 14f;
            float rowGap = nodeHeight + (canvasWidth < 560f ? 20f : 24f);
            float edgeSideOffset = canvasWidth < 560f ? 16f : 22f;
            float labelWidth = Math.Min(132f, Math.Max(88f, nodeWidth * 0.68f));
            graph.Width = canvasWidth;

            int[] lanes = BuildGraphStepLanes(draft, preview);
            int[] rows = BuildGraphStepRows(draft, preview, lanes);
            int maxRow = 0;
            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step == null)
                    continue;

                int row = i < rows.Length ? rows[i] : i;
                if (row > maxRow)
                    maxRow = row;
                float y = top + row * rowGap;
                graph.Nodes.Add(new CharacterTestRuntimeAiPlannerBehaviorCardGraphNode(
                    "step:" + step.StepId,
                    step.StepId,
                    step.DisplayTitle,
                    isEnd: false,
                    GetGraphLaneX(i < lanes.Length ? lanes[i] : 0, leftX, mainX, rightX),
                    y,
                    nodeWidth,
                    nodeHeight));
            }

            float terminalY = top + (maxRow + 1) * rowGap + 8f;
            var terminal = new CharacterTestRuntimeAiPlannerBehaviorCardGraphNode(
                "terminal:End",
                "End",
                "End / 交回行为组重新决策",
                isEnd: true,
                mainX,
                terminalY,
                nodeWidth,
                terminalHeight);
            graph.TerminalNode = terminal;
            graph.Height = terminalY + terminalHeight + 16f;

            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                CharacterTestRuntimeAiPlannerBehaviorCardGraphNode from =
                    FindGraphNode(graph, branch.FromStepId);
                if (from.NodeId.Length == 0)
                    continue;

                CharacterTestRuntimeAiPlannerBehaviorCardGraphNode target = branch.TargetIsEnd
                    ? terminal
                    : FindGraphNode(graph, branch.TargetStepId);
                if (target.NodeId.Length == 0)
                    target = terminal;

                float startX;
                float startY;
                float laneX;
                float endX;
                float endY;
                float labelX;
                float labelY;
                if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.ConditionMet)
                {
                    startX = from.X + from.Width;
                    startY = from.CenterY;
                    if (target.X > from.X)
                    {
                        endX = target.X;
                        laneX = (startX + endX) * 0.5f;
                    }
                    else
                    {
                        endX = target.X + target.Width;
                        laneX = Math.Min(
                            canvasWidth - margin,
                            from.X + from.Width + edgeSideOffset);
                    }

                    endY = target.CenterY;
                    labelX = ClampGraphValue(
                        Math.Max(startX + 4f, laneX + 4f),
                        4f,
                        canvasWidth - labelWidth - 4f);
                    labelY = ClampGraphValue(
                        (startY + endY) * 0.5f - 12f,
                        4f,
                        graph.Height - 24f);
                }
                else if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure)
                {
                    startX = from.X;
                    startY = from.CenterY;
                    if (target.X < from.X)
                    {
                        endX = target.X + target.Width;
                        laneX = (startX + endX) * 0.5f;
                    }
                    else
                    {
                        endX = target.X;
                        laneX = Math.Max(margin, from.X - edgeSideOffset);
                    }

                    endY = target.CenterY;
                    labelX = ClampGraphValue(
                        Math.Min(laneX + 4f, startX - labelWidth - 4f),
                        4f,
                        canvasWidth - labelWidth - 4f);
                    labelY = ClampGraphValue(
                        (startY + endY) * 0.5f - 12f,
                        4f,
                        graph.Height - 24f);
                }
                else
                {
                    startX = from.CenterX;
                    startY = from.Y + from.Height;
                    laneX = from.CenterX;
                    endX = target.CenterX;
                    endY = target.Y;
                    labelX = ClampGraphValue(
                        from.CenterX + 6f,
                        4f,
                        canvasWidth - labelWidth - 4f);
                    labelY = ClampGraphValue(
                        (startY + endY) * 0.5f - 12f,
                        4f,
                        graph.Height - 24f);
                }

                graph.Edges.Add(new CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge(
                    "edge:" + branch.BranchId,
                    branch.BranchId,
                    branch.Kind,
                    FormatGraphBranchLabel(branch),
                    branch.FromStepId,
                    branch.TargetStepId,
                    branch.TargetIsEnd,
                    startX,
                    startY,
                    laneX,
                    endX,
                    endY,
                    labelX,
                    labelY));
            }

            return graph;
        }

        private static float ClampGraphValue(float value, float min, float max)
        {
            if (max < min)
                return min;
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardGraphNode FindGraphNode(
            CharacterTestRuntimeAiPlannerBehaviorCardGraphViewModel graph,
            string stepId)
        {
            if (graph == null || string.IsNullOrWhiteSpace(stepId))
                return default;

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (string.Equals(graph.Nodes[i].StepId, stepId, StringComparison.Ordinal))
                    return graph.Nodes[i];
            }

            if (string.Equals(graph.TerminalNode.StepId, stepId, StringComparison.Ordinal))
                return graph.TerminalNode;

            return default;
        }

        private static bool ContainsStepId(List<string> stepIds, string stepId)
        {
            for (int i = 0; stepIds != null && i < stepIds.Count; i++)
            {
                if (string.Equals(stepIds[i], stepId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsConfiguredBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            return !string.IsNullOrWhiteSpace(branch.FromStepId)
                && !string.IsNullOrWhiteSpace(branch.BranchId);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview FindFlowBoardPrimaryBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview defaultBranch =
                FindFlowBoardBranchByTrigger(
                    preview,
                    stepId,
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess,
                    requireNonEnd: true);
            if (IsConfiguredBranch(defaultBranch))
                return defaultBranch;

            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview conditionBranch =
                FindFlowBoardBestConditionBranch(preview, stepId);
            if (IsConfiguredBranch(conditionBranch))
                return conditionBranch;

            return FindFlowBoardBranchByTrigger(
                preview,
                stepId,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess,
                requireNonEnd: false);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview FindFlowBoardBranchByTrigger(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType triggerType,
            bool requireNonEnd)
        {
            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (!string.Equals(branch.FromStepId, stepId, StringComparison.Ordinal)
                    || branch.TriggerType != triggerType
                    || (requireNonEnd && branch.TargetIsEnd))
                {
                    continue;
                }

                return branch;
            }

            return default;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview FindFlowBoardBestConditionBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview best = default;
            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (!string.Equals(branch.FromStepId, stepId, StringComparison.Ordinal)
                    || branch.TargetIsEnd
                    || branch.TriggerType != CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition)
                {
                    continue;
                }

                if (!IsConfiguredBranch(best) || branch.Priority < best.Priority)
                    best = branch;
            }

            return best;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview FindFlowBoardBranchToTarget(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId,
            string targetStepId)
        {
            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                string target = branch.TargetIsEnd ? "End" : branch.TargetStepId;
                if (string.Equals(branch.FromStepId, stepId, StringComparison.Ordinal)
                    && string.Equals(target, targetStepId, StringComparison.Ordinal))
                {
                    return branch;
                }
            }

            return default;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview FindFlowBoardEndBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview defaultEnd =
                FindFlowBoardBranchByTrigger(
                    preview,
                    stepId,
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess,
                    requireNonEnd: false);
            if (IsConfiguredBranch(defaultEnd) && defaultEnd.TargetIsEnd)
                return defaultEnd;

            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (string.Equals(branch.FromStepId, stepId, StringComparison.Ordinal)
                    && branch.TargetIsEnd)
                {
                    return branch;
                }
            }

            return default;
        }

        private static void AddFlowBoardSideBranches(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardStep step,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview mainBranch)
        {
            if (step == null)
                return;

            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (!string.Equals(branch.FromStepId, step.StepId, StringComparison.Ordinal)
                    || (IsConfiguredBranch(mainBranch)
                        && string.Equals(branch.BranchId, mainBranch.BranchId, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure)
                    step.LeftBranches.Add(branch);
                else
                    step.RightBranches.Add(branch);
            }
        }

        private static int[] BuildGraphStepLanes(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview)
        {
            int count = draft != null ? draft.Steps.Count : 0;
            var lanes = new int[count];
            for (int i = 0; i < lanes.Length; i++)
                lanes[i] = int.MinValue;
            if (lanes.Length == 0)
                return lanes;

            lanes[0] = 0;
            for (int pass = 0; pass < Math.Max(1, count); pass++)
            {
                bool changed = false;
                for (int i = 0; preview != null && i < preview.Branches.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                    if (branch.TargetIsEnd)
                        continue;

                    int fromIndex = FindDraftStepIndex(draft, branch.FromStepId);
                    int targetIndex = FindDraftStepIndex(draft, branch.TargetStepId);
                    if (fromIndex < 0
                        || targetIndex < 0
                        || fromIndex >= lanes.Length
                        || targetIndex >= lanes.Length
                        || lanes[fromIndex] == int.MinValue
                        || lanes[targetIndex] != int.MinValue)
                    {
                        continue;
                    }

                    int lane = lanes[fromIndex];
                    if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.ConditionMet)
                        lane = Math.Min(1, lane + 1);
                    else if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure)
                        lane = Math.Max(-1, lane - 1);
                    lanes[targetIndex] = lane;
                    changed = true;
                }

                if (!changed)
                    break;
            }

            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i] == int.MinValue)
                    lanes[i] = 0;
            }

            return lanes;
        }

        private static int[] BuildGraphStepRows(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            int[] lanes)
        {
            int count = draft != null ? draft.Steps.Count : 0;
            var rows = new int[count];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = int.MaxValue;
            if (rows.Length == 0)
                return rows;

            rows[0] = 0;
            for (int pass = 0; pass < Math.Max(1, count * 2); pass++)
            {
                bool changed = false;
                for (int i = 0; preview != null && i < preview.Branches.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                    if (branch.TargetIsEnd)
                        continue;

                    int fromIndex = FindDraftStepIndex(draft, branch.FromStepId);
                    int targetIndex = FindDraftStepIndex(draft, branch.TargetStepId);
                    if (fromIndex < 0
                        || targetIndex < 0
                        || fromIndex >= rows.Length
                        || targetIndex >= rows.Length
                        || rows[fromIndex] == int.MaxValue)
                    {
                        continue;
                    }

                    int candidate = rows[fromIndex] + 1;
                    if (rows[targetIndex] != int.MaxValue && rows[targetIndex] <= candidate)
                        continue;

                    rows[targetIndex] = candidate;
                    changed = true;
                }

                if (!changed)
                    break;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == int.MaxValue)
                    rows[i] = i == 0 ? 0 : rows[i - 1] + 1;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                bool adjusted;
                do
                {
                    adjusted = false;
                    for (int other = 0; other < i; other++)
                    {
                        int lane = i < lanes.Length ? lanes[i] : 0;
                        int otherLane = other < lanes.Length ? lanes[other] : 0;
                        if (rows[i] != rows[other] || lane != otherLane)
                            continue;

                        rows[i]++;
                        adjusted = true;
                        break;
                    }
                }
                while (adjusted);
            }

            return rows;
        }

        private static int FindDraftStepIndex(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            string stepId)
        {
            if (draft == null || string.IsNullOrWhiteSpace(stepId))
                return -1;

            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step != null && string.Equals(step.StepId, stepId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static float GetGraphLaneX(
            int lane,
            float leftX,
            float mainX,
            float rightX)
        {
            if (lane < 0)
                return leftX;
            if (lane > 0)
                return rightX;
            return mainX;
        }

        private static string FormatGraphBranchLabel(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            if (!string.IsNullOrWhiteSpace(branch.Label))
                return ShortenGraphLabel(branch.Label);

            if (!string.IsNullOrWhiteSpace(branch.ConditionSummary))
                return ShortenGraphLabel(branch.ConditionSummary);

            if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure)
                return "失败/中断";
            if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.ConditionMet)
                return "条件满足";

            return "默认";
        }

        private static string ShortenGraphLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            const int maxLength = 18;
            return value.Length <= maxLength
                ? value
                : value.Substring(0, maxLength - 3) + "...";
        }

    }
}
