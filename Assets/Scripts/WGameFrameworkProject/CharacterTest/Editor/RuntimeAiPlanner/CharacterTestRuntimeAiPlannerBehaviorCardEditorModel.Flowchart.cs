using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestRuntimeAiPlannerBehaviorCardEditorModel
    {
        public static CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult BuildFlowchartLayoutResult(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            float availableWidth)
        {
            var result = new CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult();
            if (draft == null || draft.Steps.Count == 0)
                return result;

            if (preview == null)
                preview = BuildFlowTimePreview(draft);

            float canvasWidth = ClampGraphValue(
                availableWidth > 0f ? availableWidth : 760f,
                560f,
                940f);
            float margin = canvasWidth < 680f ? 14f : 24f;
            float actionWidth = ClampGraphValue(canvasWidth * 0.28f, 184f, 238f);
            const float actionHeight = 78f;
            float decisionWidth = ClampGraphValue(actionWidth * 0.72f, 132f, 168f);
            const float decisionHeight = 50f;
            float endWidth = ClampGraphValue(canvasWidth * 0.34f, 216f, 300f);
            const float endHeight = 48f;
            float startWidth = ClampGraphValue(canvasWidth * 0.16f, 96f, 140f);
            const float startHeight = 30f;
            float leftX = margin + (canvasWidth < 720f ? 0f : 14f);
            float centerX = (canvasWidth - actionWidth) * 0.5f;
            float rightX = canvasWidth - margin - actionWidth - (canvasWidth < 720f ? 0f : 14f);
            const float top = 10f;
            const float startToActionGap = 16f;
            const float actionToDecisionGap = 8f;
            const float decisionToActionGap = 26f;
            const float stageGap = 18f;
            float rootY = top + startHeight + startToActionGap;

            CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardViewModel board =
                BuildFlowBoardViewModel(draft, preview);
            var actionXByStep = new Dictionary<string, float>(StringComparer.Ordinal);
            var actionYByStep = new Dictionary<string, float>(StringComparer.Ordinal);
            var decisionStepIds = new List<string>();

            result.Nodes.Add(new CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel(
                CreateFlowchartStartNodeId(),
                string.Empty,
                string.Empty,
                CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind.Start,
                "Start",
                string.Empty,
                (canvasWidth - startWidth) * 0.5f,
                top,
                startWidth,
                startHeight));

            if (board.Steps.Count == 0)
                return result;

            string rootStepId = board.Steps[0].StepId;
            SetFlowchartActionPlacement(actionXByStep, actionYByStep, rootStepId, centerX, rootY);

            float firstDecisionY = rootY + actionHeight + actionToDecisionGap;
            float branchStartY = firstDecisionY + decisionHeight + decisionToActionGap;
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview rootCondition;
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview rootDefault;
            bool hasRootCondition = TryFindFlowchartBranch(
                preview,
                rootStepId,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition,
                out rootCondition);
            bool hasRootDefault = TryFindFlowchartBranch(
                preview,
                rootStepId,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess,
                out rootDefault);
            if (hasRootCondition && !rootCondition.TargetIsEnd)
            {
                AssignFlowchartPath(
                    draft,
                    preview,
                    rootCondition.TargetStepId,
                    leftX,
                    branchStartY,
                    actionHeight,
                    decisionHeight,
                    actionToDecisionGap,
                    stageGap,
                    actionXByStep,
                    actionYByStep);
            }

            if (hasRootDefault && !rootDefault.TargetIsEnd)
            {
                AssignFlowchartPath(
                    draft,
                    preview,
                    rootDefault.TargetStepId,
                    rightX,
                    branchStartY,
                    actionHeight,
                    decisionHeight,
                    actionToDecisionGap,
                    stageGap,
                    actionXByStep,
                    actionYByStep);
            }

            for (int i = 0; i < board.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardStep boardStep = board.Steps[i];
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step =
                    FindStep(draft, boardStep.StepId);
                if (step == null)
                    continue;

                if (!actionXByStep.ContainsKey(step.StepId))
                {
                    float fallbackY = rootY + i * (actionHeight + decisionHeight + stageGap);
                    SetFlowchartActionPlacement(
                        actionXByStep,
                        actionYByStep,
                        step.StepId,
                        centerX,
                        fallbackY);
                }

                float actionX = actionXByStep[step.StepId];
                float actionY = actionYByStep[step.StepId];
                result.Nodes.Add(new CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel(
                    CreateFlowchartActionNodeId(step.StepId),
                    step.StepId,
                    string.Empty,
                    CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind.Action,
                    step.DisplayTitle,
                    FormatBehaviorCardStepDraftOutput(step),
                    actionX,
                    actionY,
                    actionWidth,
                    actionHeight));

                if (HasFlowchartDecision(preview, step.StepId))
                {
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview decisionBranch =
                        FindFlowchartPrimaryDecisionBranch(preview, step.StepId);
                    float decisionX = actionX + (actionWidth - decisionWidth) * 0.5f;
                    float decisionY = actionY + actionHeight + actionToDecisionGap;
                    result.Nodes.Add(new CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel(
                        CreateFlowchartDecisionNodeId(step.StepId),
                        step.StepId,
                        decisionBranch.BranchId,
                        CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind.Decision,
                        FormatFlowchartDecisionTitle(step, decisionBranch),
                        FormatFlowchartDecisionSubtitle(decisionBranch),
                        decisionX,
                        decisionY,
                        decisionWidth,
                        decisionHeight));
                    decisionStepIds.Add(step.StepId);
                }
            }

            float y = rootY;
            for (int i = 0; i < result.Nodes.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel node = result.Nodes[i];
                if (node.Kind == CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind.Action
                    || node.Kind == CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind.Decision)
                {
                    y = Math.Max(y, node.Y + node.Height + stageGap);
                }
            }

            float endX = (canvasWidth - endWidth) * 0.5f;
            result.Nodes.Add(new CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel(
                "end:End",
                "End",
                string.Empty,
                CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind.End,
                "End",
                "交回行为组重新决策",
                endX,
                y,
                endWidth,
                endHeight));
            result.Width = canvasWidth;
            result.Height = y + endHeight + 16f;

            AddFlowchartStartControlEdge(result, rootStepId);
            for (int i = 0; i < decisionStepIds.Count; i++)
                AddFlowchartDecisionControlEdge(result, decisionStepIds[i]);

            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (!ShouldDrawFlowchartBranch(preview, branch))
                    continue;

                AddFlowchartBranchEdge(result, branch, margin);
            }

            return result;
        }

        private static string CreateFlowchartActionNodeId(string stepId)
        {
            return "action:" + (stepId ?? string.Empty);
        }

        private static string CreateFlowchartStartNodeId()
        {
            return "start:Start";
        }

        private static string CreateFlowchartDecisionNodeId(string stepId)
        {
            return "decision:" + (stepId ?? string.Empty);
        }

        private static void SetFlowchartActionPlacement(
            Dictionary<string, float> actionXByStep,
            Dictionary<string, float> actionYByStep,
            string stepId,
            float x,
            float y)
        {
            if (string.IsNullOrWhiteSpace(stepId)
                || actionXByStep.ContainsKey(stepId))
            {
                return;
            }

            actionXByStep[stepId] = x;
            actionYByStep[stepId] = y;
        }

        private static void AssignFlowchartPath(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string startStepId,
            float x,
            float y,
            float actionHeight,
            float decisionHeight,
            float actionToDecisionGap,
            float stageGap,
            Dictionary<string, float> actionXByStep,
            Dictionary<string, float> actionYByStep)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string stepId = startStepId;
            float currentY = y;
            while (!string.IsNullOrWhiteSpace(stepId)
                && !string.Equals(stepId, "End", StringComparison.Ordinal)
                && visited.Add(stepId)
                && FindStep(draft, stepId) != null)
            {
                if (actionXByStep.ContainsKey(stepId))
                    return;

                SetFlowchartActionPlacement(actionXByStep, actionYByStep, stepId, x, currentY);

                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview next;
                if (!TryFindPreferredFlowchartContinuation(preview, stepId, out next)
                    || next.TargetIsEnd
                    || actionXByStep.ContainsKey(next.TargetStepId))
                {
                    return;
                }

                stepId = next.TargetStepId;
                currentY += actionHeight + decisionHeight + actionToDecisionGap + stageGap;
            }
        }

        private static string FormatBehaviorCardStepDraftOutput(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step == null)
                return string.Empty;

            switch (step.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility:
                    return step.AbilityId > 0
                        ? "DashStrikeAbilityId"
                        : "Ability?";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction:
                    return !step.CharacterActionId.IsValid
                        ? "CharacterAction?"
                        : step.CharacterActionId.ToString();
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget:
                    return "MoveToTarget";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait:
                    return "Wait";
                default:
                    return "End";
            }
        }

        private static float GetFlowchartActionX(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardViewModel board,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            int pathIndex,
            float leftX,
            float centerX,
            float rightX)
        {
            if (pathIndex <= 0 || board == null || pathIndex >= board.Steps.Count)
                return centerX;

            CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardStep previous = board.Steps[pathIndex - 1];
            CharacterTestRuntimeAiPlannerBehaviorCardFlowBoardStep current = board.Steps[pathIndex];
            if (previous == null || current == null)
                return centerX;

            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview incoming = previous.MainBranch;
            if (incoming.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess
                && SourceHasConditionBranchToDifferentTarget(preview, incoming.FromStepId, current.StepId))
            {
                return rightX;
            }

            return centerX;
        }

        private static bool SourceHasConditionBranchToDifferentTarget(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId,
            string mainTargetStepId)
        {
            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (branch.TriggerType != CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition
                    || branch.TargetIsEnd
                    || !string.Equals(branch.FromStepId, stepId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(branch.TargetStepId, mainTargetStepId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool TryFindFlowchartBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType triggerType,
            out CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            branch = default;
            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview candidate = preview.Branches[i];
                if (!string.Equals(candidate.FromStepId, stepId, StringComparison.Ordinal)
                    || candidate.TriggerType != triggerType)
                {
                    continue;
                }

                if (!IsConfiguredBranch(branch) || candidate.Priority < branch.Priority)
                    branch = candidate;
            }

            return IsConfiguredBranch(branch);
        }

        private static bool TryFindPreferredFlowchartContinuation(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId,
            out CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            if (TryFindFlowchartBranch(
                    preview,
                    stepId,
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition,
                    out branch)
                && !branch.TargetIsEnd)
            {
                return true;
            }

            return TryFindFlowchartBranch(
                preview,
                stepId,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess,
                out branch);
        }

        private static bool HasFlowchartDecision(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId)
        {
            bool hasCondition = false;
            bool hasNonEnd = false;
            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (!string.Equals(branch.FromStepId, stepId, StringComparison.Ordinal)
                    || branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure)
                {
                    continue;
                }

                if (branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition)
                    hasCondition = true;
                if (!branch.TargetIsEnd)
                    hasNonEnd = true;
            }

            return hasCondition && hasNonEnd;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview FindFlowchartPrimaryDecisionBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview best = default;
            for (int i = 0; preview != null && i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (!string.Equals(branch.FromStepId, stepId, StringComparison.Ordinal)
                    || branch.TriggerType != CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition)
                {
                    continue;
                }

                if (!IsConfiguredBranch(best) || branch.Priority < best.Priority)
                    best = branch;
            }

            return best;
        }

        private static string FormatFlowchartDecisionTitle(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            if (step == null)
                return "条件判断?";

            if (string.Equals(step.StepId, "dash", StringComparison.Ordinal))
                return "突进后距离 <= 1m?";
            if (string.Equals(step.StepId, "second_dash", StringComparison.Ordinal))
                return "二段后距离 <= 1m?";
            if (step.CharacterActionId == new CharacterActionId(301))
                return "连段窗口允许?";

            if (!string.IsNullOrWhiteSpace(branch.Label))
                return ShortenGraphLabel(branch.Label) + "?";

            return "条件判断?";
        }

        private static string FormatFlowchartDecisionSubtitle(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            if (!string.IsNullOrWhiteSpace(branch.ConditionSummary))
                return ShortenGraphLabel(branch.ConditionSummary);

            if (!string.IsNullOrWhiteSpace(branch.Label))
                return ShortenGraphLabel(branch.Label);

            return "判断条件";
        }

        private static void AddFlowchartDecisionControlEdge(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult result,
            string stepId)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel action =
                FindFlowchartNode(result, CreateFlowchartActionNodeId(stepId));
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel decision =
                FindFlowchartNode(result, CreateFlowchartDecisionNodeId(stepId));
            if (string.IsNullOrEmpty(action.NodeId) || string.IsNullOrEmpty(decision.NodeId))
                return;

            float startX = action.CenterX;
            float startY = action.Y + action.Height;
            float endX = decision.CenterX;
            float endY = decision.Y;
            result.Edges.Add(new CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel(
                "edge:control:" + stepId,
                string.Empty,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.ConditionMet,
                string.Empty,
                action.NodeId,
                decision.NodeId,
                stepId,
                stepId,
                targetIsEnd: false,
                startX,
                startY,
                startX,
                endX,
                endY,
                0f,
                0f));
        }

        private static void AddFlowchartStartControlEdge(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult result,
            string targetStepId)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel start =
                FindFlowchartNode(result, CreateFlowchartStartNodeId());
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel action =
                FindFlowchartNode(result, CreateFlowchartActionNodeId(targetStepId));
            if (string.IsNullOrEmpty(start.NodeId) || string.IsNullOrEmpty(action.NodeId))
                return;

            result.Edges.Add(new CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel(
                "edge:start:" + targetStepId,
                string.Empty,
                CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Success,
                string.Empty,
                start.NodeId,
                action.NodeId,
                string.Empty,
                targetStepId,
                targetIsEnd: false,
                start.CenterX,
                start.Y + start.Height,
                start.CenterX,
                action.CenterX,
                action.Y,
                0f,
                0f));
        }

        private static bool ShouldDrawFlowchartBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            if (!IsConfiguredBranch(branch))
                return false;

            if (branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess)
            {
                for (int i = 0; preview != null && i < preview.Branches.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview other = preview.Branches[i];
                    if (other.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition
                        && string.Equals(other.FromStepId, branch.FromStepId, StringComparison.Ordinal)
                        && string.Equals(other.TargetStepId, branch.TargetStepId, StringComparison.Ordinal)
                        && other.TargetIsEnd == branch.TargetIsEnd)
                    {
                        return false;
                    }
                }
            }

            if (branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition
                && branch.TargetIsEnd
                && string.Equals(branch.FromStepId, "attack.second", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static void AddFlowchartBranchEdge(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult result,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch,
            float margin)
        {
            string fromNodeId = branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure
                || !HasFlowchartDecisionNode(result, branch.FromStepId)
                    ? CreateFlowchartActionNodeId(branch.FromStepId)
                    : CreateFlowchartDecisionNodeId(branch.FromStepId);
            string targetNodeId = branch.TargetIsEnd
                ? "end:End"
                : CreateFlowchartActionNodeId(branch.TargetStepId);

            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel from =
                FindFlowchartNode(result, fromNodeId);
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel target =
                FindFlowchartNode(result, targetNodeId);
            if (string.IsNullOrEmpty(from.NodeId) || string.IsNullOrEmpty(target.NodeId))
                return;

            const float labelWidth = 92f;
            const float labelHeight = 20f;
            const float nodeEdgeGap = 8f;
            float startX;
            float startY;
            float endX;
            float endY;
            float laneX;
            if (branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure)
            {
                startY = from.CenterY;
                endY = target.CenterY;
                bool routeLeft = from.CenterX <= result.Width * 0.5f;
                startX = routeLeft ? from.X - nodeEdgeGap : from.X + from.Width + nodeEdgeGap;
                endX = routeLeft ? target.X - nodeEdgeGap : target.X + target.Width + nodeEdgeGap;
                laneX = routeLeft ? margin * 0.5f : result.Width - margin * 0.5f;
            }
            else if (branch.TargetIsEnd)
            {
                startY = from.CenterY;
                endY = target.CenterY;
                bool routeLeft = from.CenterX <= result.Width * 0.5f;
                startX = routeLeft ? from.X - nodeEdgeGap : from.X + from.Width + nodeEdgeGap;
                endX = routeLeft ? target.X - nodeEdgeGap : target.X + target.Width + nodeEdgeGap;
                laneX = routeLeft ? margin * 0.5f : result.Width - margin * 0.5f;
            }
            else if (target.X > from.X + from.Width)
            {
                startX = from.X + from.Width + nodeEdgeGap;
                startY = from.CenterY;
                endX = target.X - nodeEdgeGap;
                endY = target.CenterY;
                laneX = (startX + endX) * 0.5f;
            }
            else if (target.X + target.Width < from.X)
            {
                startX = from.X - nodeEdgeGap;
                startY = from.CenterY;
                endX = target.X + target.Width + nodeEdgeGap;
                endY = target.CenterY;
                laneX = (startX + endX) * 0.5f;
            }
            else
            {
                startX = from.CenterX;
                startY = from.Y + from.Height + nodeEdgeGap;
                endX = target.CenterX;
                endY = target.Y - nodeEdgeGap;
                laneX = startX;
            }

            startX = ClampGraphValue(startX, 0f, result.Width);
            endX = ClampGraphValue(endX, 0f, result.Width);
            startY = ClampGraphValue(startY, 0f, result.Height);
            endY = ClampGraphValue(endY, 0f, result.Height);
            laneX = ClampGraphValue(laneX, 0f, result.Width);

            CalculateFlowchartBranchLabelPosition(
                result,
                branch,
                from,
                target,
                startX,
                startY,
                endX,
                endY,
                laneX,
                labelWidth,
                labelHeight,
                out float labelX,
                out float labelY);

            result.Edges.Add(new CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel(
                "edge:" + branch.BranchId,
                branch.BranchId,
                branch.Kind,
                FormatFlowchartBranchLabel(branch),
                from.NodeId,
                target.NodeId,
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

        private static void CalculateFlowchartBranchLabelPosition(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult result,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel from,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel target,
            float startX,
            float startY,
            float endX,
            float endY,
            float laneX,
            float labelWidth,
            float labelHeight,
            out float labelX,
            out float labelY)
        {
            float minX = Math.Min(startX, endX);
            float maxX = Math.Max(startX, endX);
            float minY = Math.Min(startY, endY);
            bool routedLeft = laneX < from.CenterX;
            bool routedRight = laneX > from.CenterX;

            if (branch.TargetIsEnd
                || branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure)
            {
                labelX = routedLeft
                    ? laneX + 8f
                    : laneX - labelWidth - 8f;
                labelY = startY - labelHeight * 0.5f;
            }
            else if (target.X > from.X + from.Width)
            {
                labelX = maxX - labelWidth - 8f;
                labelY = minY - labelHeight - 4f;
            }
            else if (target.X + target.Width < from.X)
            {
                labelX = minX + 8f;
                labelY = minY - labelHeight - 4f;
            }
            else
            {
                labelX = routedRight
                    ? laneX + 8f
                    : laneX - labelWidth - 8f;
                labelY = (startY + endY) * 0.5f - labelHeight * 0.5f;
            }

            labelX = ClampGraphValue(labelX, 4f, result.Width - labelWidth - 4f);
            labelY = ClampGraphValue(labelY, 4f, result.Height - labelHeight - 4f);
        }

        private static bool HasFlowchartDecisionNode(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult result,
            string stepId)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel node =
                FindFlowchartNode(result, CreateFlowchartDecisionNodeId(stepId));
            return !string.IsNullOrEmpty(node.NodeId);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel FindFlowchartNode(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult result,
            string nodeId)
        {
            if (result == null || string.IsNullOrWhiteSpace(nodeId))
                return default;

            for (int i = 0; i < result.Nodes.Count; i++)
            {
                if (string.Equals(result.Nodes[i].NodeId, nodeId, StringComparison.Ordinal))
                    return result.Nodes[i];
            }

            return default;
        }

        private static string FormatFlowchartBranchLabel(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            if (branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Failure)
                return "失败/中断";

            if (string.Equals(branch.FromStepId, "dash", StringComparison.Ordinal)
                && branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition)
            {
                return "已进 1m";
            }

            if (string.Equals(branch.FromStepId, "dash", StringComparison.Ordinal)
                && branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess)
            {
                return "未进 1m";
            }

            if (string.Equals(branch.FromStepId, "second_dash", StringComparison.Ordinal)
                && branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition)
            {
                return "二段后进 1m";
            }

            if (string.Equals(branch.FromStepId, "second_dash", StringComparison.Ordinal)
                && branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess)
            {
                return "仍未进 1m";
            }

            if (string.Equals(branch.FromStepId, "attack.first", StringComparison.Ordinal)
                && branch.TriggerType == CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition)
            {
                return "窗口允许";
            }

            if (string.Equals(branch.FromStepId, "attack.second", StringComparison.Ordinal)
                && branch.TargetIsEnd)
            {
                return "完成";
            }

            if (!string.IsNullOrWhiteSpace(branch.Label))
                return ShortenGraphLabel(branch.Label);

            return FormatGraphBranchLabel(branch);
        }

    }
}
