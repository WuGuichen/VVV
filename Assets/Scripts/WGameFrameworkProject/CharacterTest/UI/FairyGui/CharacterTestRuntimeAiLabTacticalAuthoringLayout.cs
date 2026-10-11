using System;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Root-local geometry for tactical authoring. It owns no card data or runtime clock.</summary>
    internal readonly struct CharacterTestRuntimeAiLabTacticalAuthoringLayout
    {
        internal const float MinimumWidth = 1280f;
        internal const float MinimumHeight = 720f;
        internal CharacterTestRuntimeAiLabTacticalAuthoringLayout(Rect overview, Rect preview, Rect divider, Rect timeline, Rect inspector)
        { Overview = overview; Preview = preview; Divider = divider; Timeline = timeline; Inspector = inspector; }
        internal Rect Overview { get; }
        internal Rect Preview { get; }
        internal Rect Divider { get; }
        internal Rect Timeline { get; }
        internal Rect Inspector { get; }

        /// <summary>V5 keeps the event workspace above a central-only combat viewport.</summary>
        internal static bool TryCreateSequential(float width, float height, float previewRatio, bool expanded,
            out CharacterTestRuntimeAiLabTacticalAuthoringLayout layout, float inspectorWidth = 420f)
        {
            layout = default;
            if (!TryCreate(width, height, previewRatio, expanded, out var legacy)) return false;
            if (expanded) { layout = legacy; return true; }
            const float margin = 12f, top = 72f, gap = 12f, dividerHeight = 8f;
            float available = height - top - margin;
            float leftWidth = Mathf.Lerp(320f, 380f, Mathf.InverseLerp(MinimumWidth, 1920f, width));
            float rightWidth = Mathf.Min(inspectorWidth, width * 0.29f);
            float centerX = margin + leftWidth + gap;
            float centerWidth = width - margin * 2f - leftWidth - rightWidth - gap * 2f;
            // The camera surface is inset by eight pixels on each side.
            // Zero selects the default 16:9 surface; an explicit divider drag keeps its chosen split.
            float desiredPreviewHeight = previewRatio == 0f
                ? (centerWidth - 16f) * 9f / 16f + 16f
                : (available - dividerHeight) * previewRatio;
            float previewHeight = Mathf.Clamp(desiredPreviewHeight, 180f, available - dividerHeight - 360f);
            var timeline = new Rect(centerX, top, centerWidth, available - previewHeight - dividerHeight);
            var divider = new Rect(centerX, timeline.yMax, centerWidth, dividerHeight);
            var preview = new Rect(centerX, divider.yMax, centerWidth, previewHeight);
            layout = new CharacterTestRuntimeAiLabTacticalAuthoringLayout(
                new Rect(margin, top, leftWidth, available), preview, divider, timeline,
                new Rect(width - margin - rightWidth, top, rightWidth, available));
            return true;
        }

        internal static bool TryCreate(float width, float height, float split, bool expanded,
            out CharacterTestRuntimeAiLabTacticalAuthoringLayout layout, float minimumTimeline = 180f)
        {
            layout = default;
            if (float.IsNaN(width) || float.IsInfinity(width) || float.IsNaN(height) || float.IsInfinity(height)
                || width < MinimumWidth || height < MinimumHeight || float.IsNaN(split) || float.IsInfinity(split)) return false;
            const float margin = 16f, top = 88f, gap = 12f, dividerHeight = 8f, minimumPane = 180f;
            float availableHeight = height - top - margin;
            if (expanded)
            {
                layout = new CharacterTestRuntimeAiLabTacticalAuthoringLayout(default,
                    new Rect(margin, top, width - margin * 2f, availableHeight), default, default, default);
                return true;
            }
            float overviewWidth = Mathf.Lerp(260f, 320f, Mathf.InverseLerp(MinimumWidth, 1920f, width));
            float inspectorWidth = Mathf.Lerp(300f, 360f, Mathf.InverseLerp(MinimumWidth, 1920f, width));
            float centerX = margin + overviewWidth + gap;
            float centerWidth = width - margin * 2f - overviewWidth - inspectorWidth - gap * 2f;
            float usableHeight = availableHeight - dividerHeight;
            float previewHeight = Mathf.Clamp(usableHeight * split, minimumPane, usableHeight - Mathf.Max(minimumPane, minimumTimeline));
            var preview = new Rect(centerX, top, centerWidth, previewHeight);
            var divider = new Rect(centerX, preview.yMax, centerWidth, dividerHeight);
            var timeline = new Rect(centerX, divider.yMax, centerWidth, availableHeight - previewHeight - dividerHeight);
            layout = new CharacterTestRuntimeAiLabTacticalAuthoringLayout(
                new Rect(margin, top, overviewWidth, availableHeight), preview, divider, timeline,
                new Rect(width - margin - inspectorWidth, top, inspectorWidth, availableHeight));
            return true;
        }
    }

    /// <summary>Local layout preference; expand/restore never touches selection, authoring or simulation.</summary>
    internal sealed class CharacterTestRuntimeAiLabTacticalAuthoringLayoutState
    {
        internal float SplitRatio { get; private set; } = 0.58f;
        internal bool Expanded { get; private set; }
        internal bool Sequential { get; set; }
        internal float InspectorWidth { get; set; } = 420f;
        private float _sequentialPreviewRatio;
        internal float MinimumTimelineHeight { get; set; } = 180f;
        internal void SetExpanded(bool value) => Expanded = value;

        internal bool SetDividerPosition(Vector2 rootLocalPointer, float rootWidth, float rootHeight)
        {
            if (Sequential)
            {
                if (Expanded || float.IsNaN(rootLocalPointer.y) || float.IsInfinity(rootLocalPointer.y)
                    || !TryGet(rootWidth, rootHeight, out var sequential)) return false;
                float available = sequential.Preview.height + sequential.Timeline.height;
                _sequentialPreviewRatio = Mathf.Clamp((sequential.Preview.yMax - rootLocalPointer.y) / available,
                    180f / available, 1f - 360f / available);
                return true;
            }
            if (Expanded || !CharacterTestRuntimeAiLabTacticalAuthoringLayout.TryCreate(rootWidth, rootHeight,
                SplitRatio, false, out var current, MinimumTimelineHeight)) return false;
            float usableHeight = current.Preview.height + current.Timeline.height;
            if (float.IsNaN(rootLocalPointer.y) || float.IsInfinity(rootLocalPointer.y)) return false;
            SplitRatio = Mathf.Clamp((rootLocalPointer.y - current.Preview.y) / usableHeight,
                180f / usableHeight, 1f - MinimumTimelineHeight / usableHeight);
            return true;
        }

        internal bool TryGet(float width, float height, out CharacterTestRuntimeAiLabTacticalAuthoringLayout layout)
            => Sequential
                ? CharacterTestRuntimeAiLabTacticalAuthoringLayout.TryCreateSequential(width, height, _sequentialPreviewRatio, Expanded, out layout, InspectorWidth)
                : CharacterTestRuntimeAiLabTacticalAuthoringLayout.TryCreate(width, height, SplitRatio, Expanded, out layout, MinimumTimelineHeight);
    }
}
