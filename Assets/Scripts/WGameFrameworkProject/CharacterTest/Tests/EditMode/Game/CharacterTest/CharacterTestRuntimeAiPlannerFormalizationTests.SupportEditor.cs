using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerFormalizationTests
    {
        private static void AssertHasDiagnostic(
            CharacterTestRuntimeAiPlannerValidationReport report,
            string code)
        {
            Assert.That(
                report.Diagnostics,
                Has.Some.Matches<CharacterTestRuntimeAiPlannerDiagnostic>(
                    diagnostic => diagnostic.Code == code),
                "Missing diagnostic: " + code);
        }

        private static void AssertNoDiagnostic(
            CharacterTestRuntimeAiPlannerValidationReport report,
            string code)
        {
            Assert.That(
                report.Diagnostics,
                Has.None.Matches<CharacterTestRuntimeAiPlannerDiagnostic>(
                    diagnostic => diagnostic.Code == code),
                "Unexpected diagnostic: " + code);
        }

        private static CharacterTestRuntimeAiPlannerProfilePickerEntry FindProfilePickerEntry(
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfilePickerEntry> entries,
            string projectPath)
        {
            string normalized = CharacterTestRuntimeAiPlannerProfilePicker.NormalizeProjectAssetPath(projectPath);
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].ProjectAssetPath, normalized, StringComparison.OrdinalIgnoreCase))
                    return entries[i];
            }

            Assert.Fail("Profile picker entry not found: " + normalized);
            return default;
        }

        private static void InvokeWindowMethod(
            CharacterTestRuntimeAiPlannerWindow window,
            string methodName,
            params object[] parameters)
        {
            Type[] parameterTypes = CreateParameterTypes(parameters);
            MethodInfo method = typeof(CharacterTestRuntimeAiPlannerWindow).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            Assert.NotNull(method, methodName);
            method.Invoke(window, parameters);
        }

        private static Rect GetElementRect(VisualElement element)
        {
            Assert.NotNull(element);
            return new Rect(
                element.style.left.value.value,
                element.style.top.value.value,
                element.style.width.value.value,
                element.style.height.value.value);
        }

        private static Rect GetNodeRect(
            CharacterTestRuntimeAiPlannerFlowPreview.FlowGraphLayoutDebugSnapshot snapshot,
            string stateId)
        {
            Assert.IsTrue(snapshot.NodeRects.TryGetValue(stateId, out Rect rect), stateId);
            return rect;
        }

        private static bool RectsOverlap(Rect left, Rect right)
        {
            return left.xMin < right.xMax
                && left.xMax > right.xMin
                && left.yMin < right.yMax
                && left.yMax > right.yMin;
        }

        private static bool RectsApproximatelyEqual(Rect left, Rect right)
        {
            return Mathf.Abs(left.x - right.x) < 0.01f
                && Mathf.Abs(left.y - right.y) < 0.01f
                && Mathf.Abs(left.width - right.width) < 0.01f
                && Mathf.Abs(left.height - right.height) < 0.01f;
        }

        private static void AssertGlobalLegendNotOccupied(
            CharacterTestRuntimeAiPlannerFlowPreview.FlowGraphLayoutDebugSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.OccupiedRects.Count; i++)
            {
                Assert.IsFalse(
                    RectsApproximatelyEqual(snapshot.GlobalRect, snapshot.OccupiedRects[i]),
                    "Global legend should not reserve graph layout space.");
            }
        }

        private static void AssertLayoutIsClear(
            CharacterTestRuntimeAiPlannerFlowPreview.FlowGraphLayoutDebugSnapshot snapshot)
        {
            for (int left = 0; left < snapshot.OccupiedRects.Count; left++)
            {
                for (int right = left + 1; right < snapshot.OccupiedRects.Count; right++)
                {
                    Assert.IsFalse(
                        RectsOverlap(snapshot.OccupiedRects[left], snapshot.OccupiedRects[right]),
                        "Occupied rects overlap: "
                        + left
                        + " "
                        + FormatRect(snapshot.OccupiedRects[left])
                        + " and "
                        + right
                        + " "
                        + FormatRect(snapshot.OccupiedRects[right]));
                }
            }

            for (int edgeIndex = 0; edgeIndex < snapshot.Edges.Count; edgeIndex++)
            {
                CharacterTestRuntimeAiPlannerFlowPreview.EdgeDrawData edge = snapshot.Edges[edgeIndex];
                for (int pointIndex = 1; pointIndex < edge.RoutePoints.Count; pointIndex++)
                {
                    Vector2 start = edge.RoutePoints[pointIndex - 1];
                    Vector2 end = edge.RoutePoints[pointIndex];
                    for (int rectIndex = 0; rectIndex < snapshot.OccupiedRects.Count; rectIndex++)
                    {
                        Rect rect = snapshot.OccupiedRects[rectIndex];
                        if (PointTouchesRect(start, rect) || PointTouchesRect(end, rect))
                            continue;

                        Assert.IsFalse(
                            CharacterTestRuntimeAiPlannerFlowPreview.SegmentIntersectsRect(start, end, rect),
                            "Edge " + edgeIndex + " crosses occupied rect " + rectIndex);
                    }
                }
            }
        }

        private static bool PointTouchesRect(Vector2 point, Rect rect)
        {
            return point.x >= rect.xMin - 0.01f
                && point.x <= rect.xMax + 0.01f
                && point.y >= rect.yMin - 0.01f
                && point.y <= rect.yMax + 0.01f;
        }

        private static string FormatRect(Rect rect)
        {
            return "x="
                + rect.x.ToString("0.##")
                + ", y="
                + rect.y.ToString("0.##")
                + ", w="
                + rect.width.ToString("0.##")
                + ", h="
                + rect.height.ToString("0.##");
        }

        private static bool ContainsParameterizedFact(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace)
        {
            for (int i = 0; i < trace.Facts.Count; i++)
            {
                if ((trace.Facts[i].StableId ?? string.Empty).IndexOf(
                        "/fact/",
                        StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsTraceCandidate(
            in CharacterTestRuntimeAiLabDecisionTraceAgentView trace,
            string stableId,
            CharacterTestRuntimeAiLabDecisionTraceLayer layer)
        {
            for (int i = 0; i < trace.Candidates.Count; i++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = trace.Candidates[i];
                if (candidate.Layer == layer
                    && string.Equals(candidate.StableId, stableId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static int CountTransitionEdges(
            CharacterTestRuntimeAiPlannerFlowPreview.FlowGraphLayoutDebugSnapshot snapshot,
            int transitionIndex)
        {
            int count = 0;
            for (int i = 0; i < snapshot.Edges.Count; i++)
            {
                if (snapshot.Edges[i].TransitionIndex == transitionIndex)
                    count++;
            }

            return count;
        }

        private static CharacterTestRuntimeAiPlannerFlowPreview.EdgeDrawData FindFirstTransitionEdge(
            CharacterTestRuntimeAiPlannerFlowPreview.FlowGraphLayoutDebugSnapshot snapshot,
            int transitionIndex)
        {
            for (int i = 0; i < snapshot.Edges.Count; i++)
            {
                if (snapshot.Edges[i].TransitionIndex == transitionIndex)
                    return snapshot.Edges[i];
            }

            Assert.Fail("Missing edge for transition " + transitionIndex);
            return default;
        }

        private static void AssertGlobalRuleSitsAboveTarget(Rect ruleRect, Rect targetRect, int transitionIndex)
        {
            Assert.LessOrEqual(
                ruleRect.yMax,
                targetRect.yMin + 0.01f,
                "Global transition rule " + transitionIndex + " should sit above its target.");
            Assert.Less(
                Mathf.Abs(ruleRect.center.x - targetRect.center.x),
                0.01f,
                "Global transition rule " + transitionIndex + " should be centered over its target.");
        }

        private static void AssertPointNear(Vector2 expected, Vector2 actual, string message)
        {
            Assert.LessOrEqual(Vector2.Distance(expected, actual), 0.01f, message);
        }

        private static void AssertTransitionDoesNotShareStructureLane(
            CharacterTestRuntimeAiPlannerFlowPreview.FlowGraphLayoutDebugSnapshot snapshot,
            CharacterTestRuntimeAiPlannerFlowPreview.EdgeDrawData transitionEdge)
        {
            for (int i = 0; i < snapshot.Edges.Count; i++)
            {
                CharacterTestRuntimeAiPlannerFlowPreview.EdgeDrawData edge = snapshot.Edges[i];
                if (edge.Kind == CharacterTestRuntimeAiPlannerFlowPreview.FlowEdgeKind.Transition)
                    continue;

                Assert.IsFalse(
                    RoutesShareLane(transitionEdge.RoutePoints, edge.RoutePoints),
                    "Transition rule edge should not share a lane with hierarchy or initial path edges.");
            }
        }

        private static bool RoutesShareLane(
            IReadOnlyList<Vector2> leftRoute,
            IReadOnlyList<Vector2> rightRoute)
        {
            for (int left = 1; left < leftRoute.Count; left++)
            {
                Vector2 leftStart = leftRoute[left - 1];
                Vector2 leftEnd = leftRoute[left];
                for (int right = 1; right < rightRoute.Count; right++)
                {
                    if (SegmentsShareLane(leftStart, leftEnd, rightRoute[right - 1], rightRoute[right]))
                        return true;
                }
            }

            return false;
        }

        private static bool SegmentsShareLane(
            Vector2 leftStart,
            Vector2 leftEnd,
            Vector2 rightStart,
            Vector2 rightEnd)
        {
            bool leftVertical = Mathf.Abs(leftStart.x - leftEnd.x) < 0.01f;
            bool rightVertical = Mathf.Abs(rightStart.x - rightEnd.x) < 0.01f;
            if (leftVertical && rightVertical && Mathf.Abs(leftStart.x - rightStart.x) < 0.01f)
                return RangesOverlap(leftStart.y, leftEnd.y, rightStart.y, rightEnd.y);

            bool leftHorizontal = Mathf.Abs(leftStart.y - leftEnd.y) < 0.01f;
            bool rightHorizontal = Mathf.Abs(rightStart.y - rightEnd.y) < 0.01f;
            return leftHorizontal
                && rightHorizontal
                && Mathf.Abs(leftStart.y - rightStart.y) < 0.01f
                && RangesOverlap(leftStart.x, leftEnd.x, rightStart.x, rightEnd.x);
        }

        private static bool RangesOverlap(float leftA, float leftB, float rightA, float rightB)
        {
            float leftMin = Math.Min(leftA, leftB);
            float leftMax = Math.Max(leftA, leftB);
            float rightMin = Math.Min(rightA, rightB);
            float rightMax = Math.Max(rightA, rightB);
            return Math.Min(leftMax, rightMax) - Math.Max(leftMin, rightMin) > 0.01f;
        }

        private static Rect FindRuleRect(
            CharacterTestRuntimeAiPlannerFlowPreview.FlowGraphLayoutDebugSnapshot snapshot,
            int transitionIndex)
        {
            for (int i = 0; i < snapshot.Rules.Count; i++)
            {
                if (snapshot.Rules[i].TransitionIndex == transitionIndex)
                    return snapshot.Rules[i].Rect;
            }

            Assert.Fail("Missing rule node for transition " + transitionIndex);
            return Rect.zero;
        }

        private static int FindGlobalTransitionIndex(
            CharacterTestRuntimeAiPlannerEditorModel model,
            string targetStateId)
        {
            for (int i = 0; i < model.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition = model.Transitions[i];
                if (transition != null
                    && string.IsNullOrWhiteSpace(transition.SourceStateId)
                    && string.Equals(transition.TargetStateId, targetStateId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            Assert.Fail("Missing global transition for " + targetStateId);
            return -1;
        }

        private static CharacterTestRuntimeAiPlannerAgentSnapshot CreatePlayMonitorAgentSnapshot(
            int entityId = 7,
            int targetEntityId = 3,
            string activeStatePath = "root/cast",
            bool targetExists = true,
            bool targetInAttackRange = true,
            float targetDistance = 2.053f,
            bool movePathAvailable = true,
            float selectedUtilityScore = 9.947f,
            float selectedActionCost = 1.2f,
            int selectedActionId = 120,
            string selectedActionName = "Cast ability",
            CharacterTestRuntimeAiPlannerActionKind selectedActionKind =
                CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot,
            string lastCommand = "SetControlIntent",
            string suppressedReason = "None",
            string rejectReason = "",
            int transitionFromStateId = 8,
            int transitionToStateId = 9,
            bool targetVisible = true,
            bool targetLineOfSightBlocked = false,
            int targetLastSeenAgeFrames = 0,
            float targetThreatScore = 9.947f,
            int selfSurfaceKind = 0,
            bool selfOnHardSlick = false,
            bool selfNearMovableObject = false,
            bool moveDirectPathClear = true,
            int moveCandidateIndex = -1,
            bool environmentNearObstacle = false,
            bool environmentNearHazard = false,
            bool environmentNearUsefulObject = false,
            float movementSpeedScale = 1f,
            int movementStrafeSide = 1,
            int movementDirectionHoldFrames = 7,
            int movementReactionDelayFrames = 0,
            bool hasScenePreviewGeometry = false,
            float scenePreviewSourceCenterX = 0f,
            float scenePreviewSourceCenterY = 0f,
            float scenePreviewSourceCenterZ = 0f,
            float scenePreviewTargetCenterX = 0f,
            float scenePreviewTargetCenterY = 0f,
            float scenePreviewTargetCenterZ = 0f,
            float scenePreviewTargetDirectionX = 0f,
            float scenePreviewTargetDirectionZ = 0f,
            int scenePreviewTargetBearingMilliDegrees = 0,
            int scenePreviewFacingDeltaMilliDegrees = 0,
            float scenePreviewFacingDot = 0f,
            float scenePreviewDecisionMoveX = 0f,
            float scenePreviewDecisionMoveZ = 0f,
            float scenePreviewAttackRange = 0f)
        {
            return new CharacterTestRuntimeAiPlannerAgentSnapshot(
                entityId,
                targetEntityId,
                activeStatePath,
                targetExists,
                targetInAttackRange,
                targetDistance,
                movePathAvailable,
                selectedUtilityScore,
                selectedActionCost,
                selectedActionId,
                selectedActionName,
                selectedActionKind,
                lastCommand,
                suppressedReason,
                rejectReason,
                transitionFromStateId,
                transitionToStateId,
                targetVisible,
                targetLineOfSightBlocked,
                targetLastSeenAgeFrames,
                targetThreatScore,
                selfSurfaceKind,
                selfOnHardSlick,
                selfNearMovableObject,
                moveDirectPathClear,
                moveCandidateIndex,
                environmentNearObstacle,
                environmentNearHazard,
                environmentNearUsefulObject,
                movementSpeedScale,
                movementStrafeSide,
                movementDirectionHoldFrames,
                movementReactionDelayFrames,
                hasScenePreviewGeometry,
                scenePreviewSourceCenterX,
                scenePreviewSourceCenterY,
                scenePreviewSourceCenterZ,
                scenePreviewTargetCenterX,
                scenePreviewTargetCenterY,
                scenePreviewTargetCenterZ,
                scenePreviewTargetDirectionX,
                scenePreviewTargetDirectionZ,
                scenePreviewTargetBearingMilliDegrees,
                scenePreviewFacingDeltaMilliDegrees,
                scenePreviewFacingDot,
                scenePreviewDecisionMoveX,
                scenePreviewDecisionMoveZ,
                scenePreviewAttackRange);
        }

        private static float RectDistance(Rect left, Rect right)
        {
            float dx = 0f;
            if (left.xMax < right.xMin)
                dx = right.xMin - left.xMax;
            else if (right.xMax < left.xMin)
                dx = left.xMin - right.xMax;

            float dy = 0f;
            if (left.yMax < right.yMin)
                dy = right.yMin - left.yMax;
            else if (right.yMax < left.yMin)
                dy = left.yMin - right.yMax;

            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static T GetWindowField<T>(
            CharacterTestRuntimeAiPlannerWindow window,
            string fieldName)
        {
            FieldInfo field = typeof(CharacterTestRuntimeAiPlannerWindow).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            return (T)field.GetValue(window);
        }

        private static void SetWindowField<T>(
            CharacterTestRuntimeAiPlannerWindow window,
            string fieldName,
            T value)
        {
            FieldInfo field = typeof(CharacterTestRuntimeAiPlannerWindow).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            field.SetValue(window, value);
        }

        private static T GetWindowField<T>(
            EditorWindow window,
            string fieldName)
        {
            Assert.NotNull(window, fieldName);
            FieldInfo field = window.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            return (T)field.GetValue(window);
        }

        private static void PressButton(Button button)
        {
            object clickable = typeof(Button)
                .GetProperty("clickable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(button);
            Assert.NotNull(clickable, "Button clickable was not available.");

            MethodInfo invoke = FindClickableInvoke(clickable.GetType());
            Assert.NotNull(invoke, "Clickable invoke method was not available.");

            ParameterInfo[] parameters = invoke.GetParameters();
            if (parameters.Length == 0)
            {
                invoke.Invoke(clickable, Array.Empty<object>());
                return;
            }

            using (ClickEvent click = ClickEvent.GetPooled())
            {
                invoke.Invoke(clickable, new object[] { click });
            }
        }

        private static MethodInfo FindClickableInvoke(Type clickableType)
        {
            MethodInfo[] methods = clickableType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo oneParameter = null;
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Invoke", StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0)
                    return method;

                if (parameters.Length == 1 && typeof(EventBase).IsAssignableFrom(parameters[0].ParameterType))
                    oneParameter = method;
            }

            return oneParameter;
        }

        private static void AssertDiagnosticContains(List<string> diagnostics, string text)
        {
            Assert.NotNull(diagnostics);
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i] != null
                    && diagnostics[i].IndexOf(text, StringComparison.Ordinal) >= 0)
                {
                    return;
                }
            }

            Assert.Fail("Missing diagnostic text: " + text);
        }

        private static void AssertGraphEdge(
            CharacterTestRuntimeAiPlannerBehaviorCardGraphViewModel graph,
            string branchId,
            string fromStepId,
            string targetStepId,
            string labelText)
        {
            Assert.NotNull(graph);
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge edge = graph.Edges[i];
                if (!string.Equals(edge.BranchId, branchId, StringComparison.Ordinal))
                    continue;

                Assert.AreEqual(fromStepId, edge.FromStepId);
                Assert.AreEqual(targetStepId, edge.TargetIsEnd ? "End" : edge.TargetStepId);
                StringAssert.Contains(labelText, edge.Label);
                return;
            }

            Assert.Fail("Missing graph edge: " + branchId);
        }

        private static void AssertFlowchartEdge(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult flowchart,
            string branchId,
            string fromStepId,
            string targetStepId,
            string labelText)
        {
            Assert.NotNull(flowchart);
            for (int i = 0; i < flowchart.Edges.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel edge =
                    flowchart.Edges[i];
                if (!string.Equals(edge.BranchId, branchId, StringComparison.Ordinal))
                    continue;

                Assert.AreEqual(fromStepId, edge.FromStepId);
                Assert.AreEqual(targetStepId, edge.TargetIsEnd ? "End" : edge.TargetStepId);
                StringAssert.Contains(labelText, edge.Label);
                return;
            }

            Assert.Fail("Missing flowchart edge: " + branchId);
        }

        private static int CountFlowchartNodes(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult flowchart,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeKind kind)
        {
            int count = 0;
            for (int i = 0; flowchart != null && i < flowchart.Nodes.Count; i++)
            {
                if (flowchart.Nodes[i].Kind == kind)
                    count++;
            }

            return count;
        }

        private static void AssertFlowBoardBranch(
            List<CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview> branches,
            string branchId,
            string targetStepId,
            string labelText,
            bool expectedPresent = true)
        {
            for (int i = 0; branches != null && i < branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = branches[i];
                if (!string.Equals(branch.BranchId, branchId, StringComparison.Ordinal))
                    continue;

                if (!expectedPresent)
                    Assert.Fail("Unexpected flow board branch: " + branchId);

                Assert.AreEqual(targetStepId, branch.TargetIsEnd ? "End" : branch.TargetStepId);
                StringAssert.Contains(labelText, branch.Label);
                return;
            }

            if (expectedPresent)
                Assert.Fail("Missing flow board branch: " + branchId);
        }

        private static void AssertGraphFitsBounds(
            CharacterTestRuntimeAiPlannerBehaviorCardGraphViewModel graph)
        {
            Assert.NotNull(graph);
            Assert.Greater(graph.Width, 0f);
            Assert.Greater(graph.Height, 0f);
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardGraphNode node = graph.Nodes[i];
                Assert.GreaterOrEqual(node.X, 0f, node.StepId);
                Assert.GreaterOrEqual(node.Y, 0f, node.StepId);
                Assert.LessOrEqual(node.X + node.Width, graph.Width + 0.01f, node.StepId);
                Assert.LessOrEqual(node.Y + node.Height, graph.Height + 0.01f, node.StepId);
            }

            CharacterTestRuntimeAiPlannerBehaviorCardGraphNode terminal = graph.TerminalNode;
            Assert.GreaterOrEqual(terminal.X, 0f, terminal.StepId);
            Assert.GreaterOrEqual(terminal.Y, 0f, terminal.StepId);
            Assert.LessOrEqual(terminal.X + terminal.Width, graph.Width + 0.01f, terminal.StepId);
            Assert.LessOrEqual(terminal.Y + terminal.Height, graph.Height + 0.01f, terminal.StepId);

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardGraphEdge edge = graph.Edges[i];
                Assert.GreaterOrEqual(edge.StartX, 0f, edge.EdgeId);
                Assert.GreaterOrEqual(edge.EndX, 0f, edge.EdgeId);
                Assert.GreaterOrEqual(edge.LaneX, 0f, edge.EdgeId);
                Assert.GreaterOrEqual(edge.LabelX, 0f, edge.EdgeId);
                Assert.GreaterOrEqual(edge.LabelY, 0f, edge.EdgeId);
                Assert.LessOrEqual(edge.StartX, graph.Width + 0.01f, edge.EdgeId);
                Assert.LessOrEqual(edge.EndX, graph.Width + 0.01f, edge.EdgeId);
                Assert.LessOrEqual(edge.LaneX, graph.Width + 0.01f, edge.EdgeId);
                Assert.LessOrEqual(edge.LabelX, graph.Width + 0.01f, edge.EdgeId);
                Assert.LessOrEqual(edge.LabelY, graph.Height + 0.01f, edge.EdgeId);
            }
        }

        private static void AssertFlowchartFitsBounds(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowchartLayoutResult flowchart)
        {
            const float edgeLabelReservedWidth = 92f;
            const float edgeLabelReservedHeight = 20f;
            Assert.NotNull(flowchart);
            Assert.Greater(flowchart.Width, 0f);
            Assert.Greater(flowchart.Height, 0f);
            for (int i = 0; i < flowchart.Nodes.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel node =
                    flowchart.Nodes[i];
                Assert.GreaterOrEqual(node.X, 0f, node.NodeId);
                Assert.GreaterOrEqual(node.Y, 0f, node.NodeId);
                Assert.LessOrEqual(node.X + node.Width, flowchart.Width + 0.01f, node.NodeId);
                Assert.LessOrEqual(node.Y + node.Height, flowchart.Height + 0.01f, node.NodeId);
            }

            for (int i = 0; i < flowchart.Edges.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel edge =
                    flowchart.Edges[i];
                Assert.GreaterOrEqual(edge.StartX, 0f, edge.EdgeId);
                Assert.GreaterOrEqual(edge.EndX, 0f, edge.EdgeId);
                Assert.GreaterOrEqual(edge.LaneX, 0f, edge.EdgeId);
                Assert.LessOrEqual(edge.StartX, flowchart.Width + 0.01f, edge.EdgeId);
                Assert.LessOrEqual(edge.EndX, flowchart.Width + 0.01f, edge.EdgeId);
                Assert.LessOrEqual(edge.LaneX, flowchart.Width + 0.01f, edge.EdgeId);
                if (!string.IsNullOrWhiteSpace(edge.Label))
                {
                    Assert.GreaterOrEqual(edge.LabelX, 0f, edge.EdgeId);
                    Assert.GreaterOrEqual(edge.LabelY, 0f, edge.EdgeId);
                    Assert.LessOrEqual(edge.LabelX + edgeLabelReservedWidth, flowchart.Width + 0.01f, edge.EdgeId);
                    Assert.LessOrEqual(edge.LabelY + edgeLabelReservedHeight, flowchart.Height + 0.01f, edge.EdgeId);
                }
            }
        }

        private static Type[] CreateParameterTypes(object[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return Type.EmptyTypes;

            var types = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                types[i] = parameters[i].GetType();
            return types;
        }

        private static CharacterTestCharacterRuntimeWorld CreateViewSyncWorld(
            CharacterTestCharacterControlOwner team1Owner,
            CharacterTestCharacterControlOwner team2Owner)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(
                registry.TryGet(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    out CharacterTestCharacterDefinition definition));
            var world = new CharacterTestCharacterRuntimeWorld();
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    CharacterTestArenaMatchConfig.Team1Id,
                    -1f,
                    1f,
                    0f,
                    team1Owner),
                definition);
            world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    CharacterTestArenaMatchConfig.Team2Id,
                    1f,
                    1f,
                    0f,
                    team2Owner),
                definition);
            return world;
        }

        private static void AssertTextPresent(VisualElement root, string text)
        {
            Assert.IsTrue(ContainsText(root, text), "Missing UI text: " + text);
        }

        private static void AssertTextAbsent(VisualElement root, string text)
        {
            Assert.IsFalse(ContainsText(root, text), "Unexpected UI text: " + text);
        }

        private static bool ContainsText(VisualElement root, string text)
        {
            if (root == null)
                return false;

            if (root is TextElement textElement
                && !string.IsNullOrEmpty(textElement.text)
                && textElement.text.IndexOf(text, StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                if (ContainsText(root[i], text))
                    return true;
            }

            return false;
        }

        private static int CountNamedElements(VisualElement root, string name)
        {
            if (root == null)
                return 0;

            int count = string.Equals(root.name, name, StringComparison.Ordinal) ? 1 : 0;
            for (int i = 0; i < root.childCount; i++)
                count += CountNamedElements(root[i], name);

            return count;
        }
    }
}
