using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal enum CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind
    {
        Start = 0,
        Action = 1,
        Decision = 2,
        End = 3
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel(
            string nodeId,
            string stepId,
            string branchId,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind kind,
            string title,
            string subtitle,
            float x,
            float y,
            float width,
            float height)
        {
            NodeId = nodeId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            BranchId = branchId ?? string.Empty;
            Kind = kind;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public string NodeId { get; }
        public string StepId { get; }
        public string BranchId { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind Kind { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float CenterX { get { return X + Width * 0.5f; } }
        public float CenterY { get { return Y + Height * 0.5f; } }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel(
            string edgeId,
            string branchId,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchKind kind,
            string label,
            string fromNodeId,
            string toNodeId,
            string fromStepId,
            string targetStepId,
            bool targetIsEnd,
            float startX,
            float startY,
            float laneX,
            float endX,
            float endY,
            float labelX,
            float labelY)
        {
            EdgeId = edgeId ?? string.Empty;
            BranchId = branchId ?? string.Empty;
            Kind = kind;
            Label = label ?? string.Empty;
            FromNodeId = fromNodeId ?? string.Empty;
            ToNodeId = toNodeId ?? string.Empty;
            FromStepId = fromStepId ?? string.Empty;
            TargetStepId = targetStepId ?? string.Empty;
            TargetIsEnd = targetIsEnd;
            StartX = startX;
            StartY = startY;
            LaneX = laneX;
            EndX = endX;
            EndY = endY;
            LabelX = labelX;
            LabelY = labelY;
        }

        public string EdgeId { get; }
        public string BranchId { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCardBranchKind Kind { get; }
        public string Label { get; }
        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public string FromStepId { get; }
        public string TargetStepId { get; }
        public bool TargetIsEnd { get; }
        public float StartX { get; }
        public float StartY { get; }
        public float LaneX { get; }
        public float EndX { get; }
        public float EndY { get; }
        public float LabelX { get; }
        public float LabelY { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult()
        {
            Nodes = new List<CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel>();
            Edges = new List<CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel>();
            Width = 760f;
            Height = 520f;
        }

        public List<CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel> Nodes { get; }
        public List<CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel> Edges { get; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardGraphNode
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardGraphNode(
            string nodeId,
            string stepId,
            string title,
            bool isEnd,
            float x,
            float y,
            float width,
            float height)
        {
            NodeId = nodeId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            Title = title ?? string.Empty;
            IsEnd = isEnd;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public string NodeId { get; }
        public string StepId { get; }
        public string Title { get; }
        public bool IsEnd { get; }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float CenterX { get { return X + Width * 0.5f; } }
        public float CenterY { get { return Y + Height * 0.5f; } }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge(
            string edgeId,
            string branchId,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchKind kind,
            string label,
            string fromStepId,
            string targetStepId,
            bool targetIsEnd,
            float startX,
            float startY,
            float laneX,
            float endX,
            float endY,
            float labelX,
            float labelY)
        {
            EdgeId = edgeId ?? string.Empty;
            BranchId = branchId ?? string.Empty;
            Kind = kind;
            Label = label ?? string.Empty;
            FromStepId = fromStepId ?? string.Empty;
            TargetStepId = targetStepId ?? string.Empty;
            TargetIsEnd = targetIsEnd;
            StartX = startX;
            StartY = startY;
            LaneX = laneX;
            EndX = endX;
            EndY = endY;
            LabelX = labelX;
            LabelY = labelY;
        }

        public string EdgeId { get; }
        public string BranchId { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCardBranchKind Kind { get; }
        public string Label { get; }
        public string FromStepId { get; }
        public string TargetStepId { get; }
        public bool TargetIsEnd { get; }
        public float StartX { get; }
        public float StartY { get; }
        public float LaneX { get; }
        public float EndX { get; }
        public float EndY { get; }
        public float LabelX { get; }
        public float LabelY { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorCardGraphViewModel
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardGraphViewModel()
        {
            Nodes = new List<CharacterTestRuntimeAiPlannerBehaviorCardGraphNode>();
            Edges = new List<CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge>();
            Width = 1040f;
            Height = 360f;
        }

        public List<CharacterTestRuntimeAiPlannerBehaviorCardGraphNode> Nodes { get; }
        public List<CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge> Edges { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCardGraphNode TerminalNode { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

}
