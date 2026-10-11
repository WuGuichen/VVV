using System;
using System.Collections.Generic;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private const float BehaviorLogicFlowSpeed = 260f;
        private const float BehaviorLogicFlowGlowLength = 22f;
        private const float BehaviorLogicFlowGlowWidth = 8f;
        private const float BehaviorLogicFlowCoreLength = 16f;
        private const float BehaviorLogicFlowCoreWidth = 3f;
        private const float BehaviorLogicArrowLength = 10f;
        private const float BehaviorLogicArrowWidth = 8f;
        private const float BehaviorLogicArrowOutlineWidth = 0f;
        private static readonly float[] BehaviorLogicFlowGlowCorners = { 4f, 4f, 4f, 4f };
        private static readonly float[] BehaviorLogicFlowCoreCorners = { 1.5f, 1.5f, 1.5f, 1.5f };

        private sealed class TransitionFlowSlot
        {
            internal Fgui.GGraph ArrowFill;
            internal Fgui.GGraph FlowGlow;
            internal Fgui.GGraph FlowCore;
            internal readonly Vector2[] ArrowFillPoints = new Vector2[3];
            internal CharacterTestRuntimeAiLabLogicTransitionViewModel ViewModel;
            internal string StableId = string.Empty;
            internal bool Active;
        }

        private readonly List<TransitionFlowSlot> _transitionFlowPool =
            new List<TransitionFlowSlot>(24);

        internal void RefreshBehaviorLogicFlowAnimation()
        {
            if (!_behaviorLogicMode
                || !_compactBehaviorLogicCard
                || _root?.visible != true
                || _contentLayer?.visible != true)
            {
                return;
            }

            float time = Time.unscaledTime;
            int count = Math.Min(_boundTransitionCount, _transitionFlowPool.Count);
            for (int orderIndex = 0; orderIndex < count; orderIndex++)
            {
                TransitionFlowSlot slot = _transitionFlowPool[orderIndex];
                if (!slot.Active || slot.ViewModel == null)
                    continue;

                int connectorStart = orderIndex < _transitionConnectorStart.Length
                    ? _transitionConnectorStart[orderIndex]
                    : 0;
                int connectorCount = orderIndex < _transitionConnectorCount.Length
                    ? _transitionConnectorCount[orderIndex]
                    : 0;
                float totalLength = ResolveBehaviorLogicFlowLength(
                    connectorStart,
                    connectorCount);
                if (totalLength <= 0.5f)
                {
                    SetTransitionFlowPulseVisible(slot, false);
                    continue;
                }

                float phaseOffset = ResolveBehaviorLogicFlowPhase(slot.StableId);
                float distance = Mathf.Repeat(
                    time * BehaviorLogicFlowSpeed + phaseOffset * totalLength,
                    totalLength);
                if (!TryResolveBehaviorLogicFlowPose(
                        connectorStart,
                        connectorCount,
                        distance,
                        out Vector2 position,
                        out float rotation))
                {
                    SetTransitionFlowPulseVisible(slot, false);
                    continue;
                }

                SetTransitionFlowPulseVisible(slot, true);
                slot.FlowGlow.SetXY(position.x, position.y);
                slot.FlowGlow.rotation = rotation;
                slot.FlowCore.SetXY(position.x, position.y);
                slot.FlowCore.rotation = rotation;

                CharacterTestRuntimeAiLabLogicTransitionViewModel transition = slot.ViewModel;
                Color routeColor = TransitionColor(transition);
                float stateAlpha = ResolveBehaviorLogicFlowStateAlpha(transition);
                float edgeDistance = Mathf.Min(distance, totalLength - distance);
                float travelFade = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(edgeDistance / 22f));
                float normalizedDistance = distance / totalLength;
                float arrival = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.82f, 1f, normalizedDistance));

                slot.FlowGlow.color = routeColor;
                slot.FlowGlow.alpha = 0.24f * stateAlpha * travelFade;
                slot.FlowCore.color = Color.Lerp(routeColor, Color.white, 0.42f);
                slot.FlowCore.alpha = 0.92f * stateAlpha * travelFade;
                slot.ArrowFill.color = routeColor;
                slot.ArrowFill.alpha = Mathf.Clamp01(stateAlpha * (0.82f + arrival * 0.18f));
            }
        }

        private void ConfigureBehaviorLogicFlowArrow(
            int flowIndex,
            float targetX,
            float targetY,
            float directionX,
            float directionY,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            TransitionFlowSlot slot = GetTransitionFlowSlot(flowIndex);
            Vector2 direction = new Vector2(directionX, directionY);
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.right;
            else
                direction.Normalize();

            Vector2 target = new Vector2(targetX, targetY);
            DrawBehaviorLogicArrowTriangle(
                slot.ArrowFill,
                slot.ArrowFillPoints,
                target,
                direction,
                BehaviorLogicArrowLength,
                BehaviorLogicArrowWidth,
                Color.white,
                BehaviorLogicArrowOutlineWidth,
                Color.clear);

            slot.ViewModel = transition;
            slot.StableId = transition?.TransitionId ?? string.Empty;
            slot.Active = true;
            SetTransitionFlowArrowVisible(slot, true);
            SetTransitionFlowPulseVisible(slot, true);
            UpdateTransitionFlowState(flowIndex, transition);
        }

        private TransitionFlowSlot GetTransitionFlowSlot(int index)
        {
            while (_transitionFlowPool.Count <= index)
            {
                int visualIndex = _transitionFlowPool.Count;
                var slot = new TransitionFlowSlot
                {
                    ArrowFill = CreateTransitionFlowGraph(
                        "runtimeTimelineArrowFill" + visualIndex,
                        ConnectorSortingOrder + 1),
                    FlowGlow = CreateTransitionFlowGraph(
                        "runtimeTimelineFlowGlow" + visualIndex,
                        ConnectorSortingOrder + 2),
                    FlowCore = CreateTransitionFlowGraph(
                        "runtimeTimelineFlowCore" + visualIndex,
                        ConnectorSortingOrder + 3)
                };
                slot.FlowGlow.SetPivot(0.5f, 0.5f, true);
                slot.FlowGlow.DrawRoundRect(
                    BehaviorLogicFlowGlowLength,
                    BehaviorLogicFlowGlowWidth,
                    Color.white,
                    BehaviorLogicFlowGlowCorners);
                slot.FlowCore.SetPivot(0.5f, 0.5f, true);
                slot.FlowCore.DrawRoundRect(
                    BehaviorLogicFlowCoreLength,
                    BehaviorLogicFlowCoreWidth,
                    Color.white,
                    BehaviorLogicFlowCoreCorners);
                _transitionFlowPool.Add(slot);
            }

            return _transitionFlowPool[index];
        }

        private Fgui.GGraph CreateTransitionFlowGraph(string name, int sortingOrder)
        {
            var graph = new Fgui.GGraph
            {
                name = name,
                touchable = false,
                sortingOrder = sortingOrder
            };
            _contentLayer.AddChild(graph);
            return graph;
        }

        private static void DrawBehaviorLogicArrowTriangle(
            Fgui.GGraph graph,
            Vector2[] points,
            Vector2 target,
            Vector2 direction,
            float length,
            float width,
            Color color,
            float outlineWidth,
            Color outlineColor)
        {
            Vector2 side = new Vector2(-direction.y, direction.x);
            Vector2 baseCenter = target - direction * length;
            Vector2 left = baseCenter + side * (width * 0.5f);
            Vector2 right = baseCenter - side * (width * 0.5f);
            float padding = Mathf.Max(1f, outlineWidth + 0.5f);
            float minimumX = Mathf.Min(target.x, Mathf.Min(left.x, right.x)) - padding;
            float minimumY = Mathf.Min(target.y, Mathf.Min(left.y, right.y)) - padding;
            float maximumX = Mathf.Max(target.x, Mathf.Max(left.x, right.x)) + padding;
            float maximumY = Mathf.Max(target.y, Mathf.Max(left.y, right.y)) + padding;
            points[0] = left - new Vector2(minimumX, minimumY);
            points[1] = target - new Vector2(minimumX, minimumY);
            points[2] = right - new Vector2(minimumX, minimumY);
            graph.SetXY(minimumX, minimumY);
            graph.DrawPolygon(
                Math.Max(2f, maximumX - minimumX),
                Math.Max(2f, maximumY - minimumY),
                points,
                color,
                outlineWidth,
                outlineColor);
        }

        private float ResolveBehaviorLogicFlowLength(int connectorStart, int connectorCount)
        {
            int end = Math.Min(_connectorPool.Count, connectorStart + connectorCount);
            float result = 0f;
            for (int connector = Math.Max(0, connectorStart); connector < end; connector++)
            {
                Fgui.GGraph graph = _connectorPool[connector];
                if (graph?.visible == true)
                    result += Math.Max(0f, graph.width);
            }
            return result;
        }

        private bool TryResolveBehaviorLogicFlowPose(
            int connectorStart,
            int connectorCount,
            float distance,
            out Vector2 position,
            out float rotation)
        {
            int end = Math.Min(_connectorPool.Count, connectorStart + connectorCount);
            float remaining = Math.Max(0f, distance);
            for (int connector = Math.Max(0, connectorStart); connector < end; connector++)
            {
                Fgui.GGraph graph = _connectorPool[connector];
                if (graph?.visible != true)
                    continue;
                float length = Math.Max(0f, graph.width);
                if (remaining > length && connector + 1 < end)
                {
                    remaining -= length;
                    continue;
                }

                float radians = graph.rotation * Mathf.Deg2Rad;
                float localDistance = Mathf.Clamp(remaining, 0f, length);
                position = new Vector2(
                    graph.x + Mathf.Cos(radians) * localDistance,
                    graph.y + Mathf.Sin(radians) * localDistance);
                rotation = graph.rotation;
                return true;
            }

            position = default;
            rotation = 0f;
            return false;
        }

        private void UpdateTransitionFlowState(
            int orderIndex,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (orderIndex < 0 || orderIndex >= _transitionFlowPool.Count)
                return;
            TransitionFlowSlot slot = _transitionFlowPool[orderIndex];
            slot.ViewModel = transition;
            slot.StableId = transition?.TransitionId ?? slot.StableId;
        }

        private static float ResolveBehaviorLogicFlowStateAlpha(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (transition == null || transition.Invalid)
                return 0.42f;
            if (transition.PreviousCommitted)
                return 0.58f;
            if (transition.Pending)
                return 0.72f;
            if (transition.Selected || transition.RuntimeFrame >= 0)
                return 1f;
            return 0.86f;
        }

        private static float ResolveBehaviorLogicFlowPhase(string stableId)
        {
            int hash = StableStringHash(stableId ?? string.Empty) & 0x7fffffff;
            return (hash % 997) / 997f;
        }

        private void HideTransitionFlowSlot(int index)
        {
            if (index < 0 || index >= _transitionFlowPool.Count)
                return;
            TransitionFlowSlot slot = _transitionFlowPool[index];
            slot.Active = false;
            slot.ViewModel = null;
            SetTransitionFlowArrowVisible(slot, false);
            SetTransitionFlowPulseVisible(slot, false);
        }

        private void HideTransitionFlowSlots(int used)
        {
            for (int index = Math.Max(0, used); index < _transitionFlowPool.Count; index++)
                HideTransitionFlowSlot(index);
        }

        private static void SetTransitionFlowArrowVisible(
            TransitionFlowSlot slot,
            bool visible)
        {
            slot.ArrowFill.visible = visible;
        }

        private static void SetTransitionFlowPulseVisible(
            TransitionFlowSlot slot,
            bool visible)
        {
            slot.FlowGlow.visible = visible;
            slot.FlowCore.visible = visible;
        }

        private void ResetTransitionFlowVisuals()
        {
            _transitionFlowPool.Clear();
        }
    }
}
