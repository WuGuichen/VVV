using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestRuntimeAiPlannerFlowPreview
    {
        private static string MakeNameKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "empty";

            var chars = new char[value.Length];
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                chars[count++] = char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'
                    ? ch
                    : '_';
            }

            return new string(chars, 0, count);
        }

        private static bool ContainsOrdinal(string value, string part)
        {
            return !string.IsNullOrEmpty(value)
                && !string.IsNullOrEmpty(part)
                && value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float NormalizeViewportWidth(
            float viewportWidth,
            CharacterTestRuntimeAiPlannerFlowPreviewMode mode)
        {
            if (float.IsNaN(viewportWidth) || float.IsInfinity(viewportWidth) || viewportWidth < 240f)
            {
                return mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview
                    ? DefaultOverviewViewportWidth
                    : DefaultDetailViewportWidth;
            }

            return viewportWidth;
        }

        private static float NormalizeViewportHeight(
            float viewportHeight,
            CharacterTestRuntimeAiPlannerFlowPreviewMode mode)
        {
            if (float.IsNaN(viewportHeight) || float.IsInfinity(viewportHeight) || viewportHeight < 180f)
                return GetPreviewHeight(mode);

            return viewportHeight;
        }

        private static float NormalizeRequestedZoom(
            float zoom,
            CharacterTestRuntimeAiPlannerFlowPreviewMode mode)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom) || zoom <= 0f)
                return AutoZoom;

            return ClampZoom(zoom, mode);
        }

        private static float ClampZoom(
            float zoom,
            CharacterTestRuntimeAiPlannerFlowPreviewMode mode)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom))
                return 1f;

            float min = mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview
                ? MinOverviewZoom
                : MinDetailZoom;
            return Mathf.Clamp(zoom, min, MaxZoom);
        }

        private static float GetPreviewHeight(CharacterTestRuntimeAiPlannerFlowPreviewMode mode)
        {
            return mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview
                ? OverviewPreviewHeight
                : DetailPreviewHeight;
        }

        private static Vector2 CalculateContentOffset(Rect contentBounds, float canvasWidth, float canvasHeight)
        {
            float offsetX = Math.Max(0f, (canvasWidth - contentBounds.width) * 0.5f - contentBounds.x);
            float offsetY = Math.Max(0f, (canvasHeight - contentBounds.height) * 0.5f - contentBounds.y);
            return new Vector2(offsetX, offsetY);
        }

        private static Rect OffsetRect(Rect rect, Vector2 offset)
        {
            return new Rect(
                rect.x + offset.x,
                rect.y + offset.y,
                rect.width,
                rect.height);
        }

        private static Dictionary<string, Rect> OffsetRects(
            IReadOnlyDictionary<string, Rect> rects,
            Vector2 offset)
        {
            var result = new Dictionary<string, Rect>(rects.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, Rect> pair in rects)
                result[pair.Key] = OffsetRect(pair.Value, offset);

            return result;
        }

        private static List<EdgeDrawData> OffsetEdges(
            IReadOnlyList<EdgeDrawData> edges,
            Vector2 offset)
        {
            var result = new List<EdgeDrawData>(edges.Count);
            for (int i = 0; i < edges.Count; i++)
            {
                EdgeDrawData edge = edges[i];
                result.Add(new EdgeDrawData(
                    edge.Kind,
                    edge.TransitionIndex,
                    edge.Start + offset,
                    edge.End + offset,
                    OffsetPoints(edge.RoutePoints, offset),
                    edge.DrawArrow,
                    edge.Color,
                    edge.Selected,
                    edge.Severity));
            }

            return result;
        }

        private static List<RuleDrawData> OffsetRules(
            IReadOnlyList<RuleDrawData> rules,
            Vector2 offset)
        {
            var result = new List<RuleDrawData>(rules.Count);
            for (int i = 0; i < rules.Count; i++)
            {
                RuleDrawData rule = rules[i];
                result.Add(new RuleDrawData(
                    rule.TransitionIndex,
                    OffsetRect(rule.Rect, offset),
                    rule.Label,
                    rule.Tooltip,
                    rule.Color,
                    rule.Selected,
                    rule.Severity));
            }

            return result;
        }

        private static List<Rect> OffsetRectList(
            IReadOnlyList<Rect> rects,
            Vector2 offset)
        {
            var result = new List<Rect>(rects.Count);
            for (int i = 0; i < rects.Count; i++)
                result.Add(OffsetRect(rects[i], offset));

            return result;
        }

        private static List<Vector2> OffsetPoints(
            IReadOnlyList<Vector2> points,
            Vector2 offset)
        {
            var result = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
                result.Add(points[i] + offset);

            return result;
        }

    }
}
