using System;
using System.Globalization;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;
using TimelineGeometry = MxFramework.Game.CharacterTest.CharacterTestRuntimeAiLabTimelineGeometry;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns one live or replay five-track item renderer and its stable interactions.</summary>
    /// <remarks>NoAlloc after the renderer visual pool is preheated.</remarks>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabTimelineItemBinder
    {
        private readonly CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder _renderer;
        private readonly CharacterTestFairyGuiRuntimeAiLabTimelineRulerBinder _ruler;
        private readonly ICharacterTestRuntimeAiLabTimelineViewportIntentSink _intentSink;
        private readonly Action<Fgui.GObject, string> _bindTooltip;
        private CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel _detailWorkspace;

        private const float FormalTimelineRulerX =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.LabelWidth;
        private const float FormalTimelineWidth =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.CanvasWidth;
        private const int TimelineMarkerCapacity = 256;
        private const int TimelineSpanCapacity = 128;
        private const int TimelineTickCapacity = 64;
        private const float TimelineSpanHeight = 48f;
        private const float TimelineSplitStateSpanHeight = 40f;
        private const float TimelineEventLaneHeight = 10f;
        private const float TimelineMarkerSize = 18f;
        private const float TimelineMarkerGlyphSize = 12f;
        private const int TimelineLabelFontSize = 18;
        private static string[] TimelineTrackNames =>
            CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder.TimelineTrackNames;

        internal CharacterTestFairyGuiRuntimeAiLabTimelineItemBinder(
            CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder renderer,
            CharacterTestFairyGuiRuntimeAiLabTimelineRulerBinder ruler,
            ICharacterTestRuntimeAiLabTimelineViewportIntentSink intentSink,
            Action<Fgui.GObject, string> bindTooltip)
        {
            _renderer = renderer;
            _ruler = ruler;
            _intentSink = intentSink;
            _bindTooltip = bindTooltip;
        }

        internal void Bind(
            Fgui.GComponent timeline,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel detailWorkspace)
        {
            _detailWorkspace = detailWorkspace;
            _renderer.VisualPool.EnsurePreheated(
                TimelineMarkerCapacity,
                TimelineSpanCapacity,
                TimelineTickCapacity,
                BindPooledTimelineVisualInteraction);
            CharacterTestRuntimeAiLabTimelineProjection projection = viewModel.TimelineProjection;
            if (projection == null || !projection.Domain.HasFrames)
            {
                Hide();
                _renderer.LastProjection = null;
                SetTimelineChildVisible(timeline, "timelineHistoryProgress", false);
                SetTimelineChildVisible(timeline, "timelinePlayhead", false);
                SetTimelineChildVisible(timeline, "timelinePlayheadHitArea", false);
                SetTimelineChildVisible(timeline, "timelineSelectedFrameBadge", false);
                SetTimelineChildVisible(timeline, "timelineSelectedFrameLabel", false);
                return;
            }

            SetTimelineChildVisible(timeline, "timelinePlayhead", true);
            SetTimelineChildVisible(timeline, "timelinePlayheadHitArea", true);
            SetTimelineChildVisible(timeline, "timelineSelectedFrameBadge", true);
            SetTimelineChildVisible(timeline, "timelineSelectedFrameLabel", true);
            bool projectionChanged = !ReferenceEquals(_renderer.LastProjection, projection)
                || !string.Equals(
                    _renderer.LastSelectedStableKey,
                    viewModel.TimelineSelectedVisualStableKey,
                    StringComparison.Ordinal);
            if (projectionChanged)
            {
                Hide();
                if (!_renderer.VisualPool.CanRender(
                        projection,
                        TimelineMarkerCapacity,
                        TimelineSpanCapacity))
                {
                    BindText(
                        timeline,
                        "timelineStatus",
                        "RUNTIME_FRAME_POOL_OVERFLOW · 投影超过冻结容量，五轨已停止绘制。请缩小视口或减少事件密度。");
                    _renderer.LastProjection = projection;
                    _renderer.LastSelectedStableKey = string.Empty;
                    return;
                }
                _ruler.BindTicks(
                    timeline,
                    projection.Viewport,
                    projection.Domain.SelectedFrame);
                RenderExpandedTimelineItems(
                    timeline,
                    projection,
                    viewModel.TimelineSelectedVisualStableKey);
                _renderer.LastProjection = projection;
                _renderer.LastSelectedStableKey =
                    viewModel.TimelineSelectedVisualStableKey ?? string.Empty;
            }
            CharacterTestFairyGuiRuntimeAiLabTimelineRulerBinder.BindDomain(
                timeline,
                projection,
                viewModel);
        }

        internal void Hide()
        {
            _renderer.VisualPool.HideAll();
        }

        internal void Invalidate()
        {
            Hide();
            _renderer.LastProjection = null;
            _renderer.LastSelectedStableKey = string.Empty;
        }

        internal void EnsurePreheated()
        {
            _renderer.VisualPool.EnsurePreheated(
                TimelineMarkerCapacity,
                TimelineSpanCapacity,
                TimelineTickCapacity,
                BindPooledTimelineVisualInteraction);
        }

        private void BindPooledTimelineVisualInteraction(Fgui.GComponent visual)
        {
            visual.onTouchBegin.Set(context => context.StopPropagation());
            visual.onTouchMove.Set(context => context.StopPropagation());
            visual.onTouchEnd.Set(context => context.StopPropagation());
            visual.onClick.Set(context =>
            {
                context.StopPropagation();
                CharacterTestRuntimeAiLabTimelineItem item =
                    visual.data as CharacterTestRuntimeAiLabTimelineItem;
                if (item == null)
                    return;
                CharacterTestRuntimeAiLabTimelineItemLocator locator = item.CreateLocator();
                if (context.inputEvent != null && context.inputEvent.isDoubleClick)
                    _intentSink?.Navigate(locator, false);
                else
                    _intentSink?.Select(locator);
            });
        }
        private void RenderExpandedTimelineItems(
            Fgui.GComponent timeline,
            CharacterTestRuntimeAiLabTimelineProjection projection,
            string selectedStableKey)
        {
            int markerIndex = 0;
            int spanIndex = 0;
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                CharacterTestRuntimeAiLabTimelineTrackProjection trackProjection = projection.Tracks[trackIndex];
                Fgui.GComponent track = timeline.GetChild(TrackChildName(trackProjection.Kind))?.asCom;
                Fgui.GComponent content = track?.GetChild("trackContent")?.asCom;
                if (track == null || content == null)
                    continue;
                BindText(track, "trackLabel", trackProjection.Label);
                BindText(track, "trackUnavailable", trackProjection.Available ? string.Empty : trackProjection.UnavailableMessage);
                bool splitSemanticLanes = HasStateAndEventEvidence(trackProjection);
                for (int itemIndex = 0; itemIndex < trackProjection.Items.Count; itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem item = trackProjection.Items[itemIndex];
                    if (projection.Domain.SelectedFrame < projection.Domain.LatestFrame
                        && item.StartFrame > projection.Domain.SelectedFrame)
                    {
                        // Recorded future is intentionally summarized by the evenly-spaced
                        // sample dots below. Full chips in that region compete with the
                        // selected-frame evidence and make the history/future boundary unclear.
                        continue;
                    }
                    if (item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.Span)
                    {
                        if (!item.IsOpen && item.EndFrame <= item.StartFrame)
                            continue;
                        Fgui.GComponent span = _renderer.VisualPool.Spans[spanIndex++];
                        PlaceTimelineItem(
                            content,
                            span,
                            item,
                            projection.Viewport,
                            spanItem: true,
                            splitSemanticLanes: splitSemanticLanes,
                            selectedStableKey: selectedStableKey);
                    }
                    else
                    {
                        Fgui.GComponent marker = _renderer.VisualPool.Markers[markerIndex++];
                        PlaceTimelineItem(
                            content,
                            marker,
                            item,
                            projection.Viewport,
                            spanItem: false,
                            splitSemanticLanes: splitSemanticLanes,
                            selectedStableKey: selectedStableKey);
                    }
                }
            }
            RenderRecordedFutureFrameSamples(
                timeline,
                projection,
                ref markerIndex);
        }

        private void RenderRecordedFutureFrameSamples(
            Fgui.GComponent timeline,
            CharacterTestRuntimeAiLabTimelineProjection projection,
            ref int markerIndex)
        {
            int selectedFrame = projection.Domain.SelectedFrame;
            int latestFrame = projection.Domain.LatestFrame;
            int distance = latestFrame - selectedFrame;
            if (distance <= 1)
                return;

            const int maximumSamples = 6;
            int sampleCount = Math.Min(maximumSamples, distance - 1);
            for (int trackIndex = 0;
                 trackIndex < projection.Tracks.Count
                    && trackIndex < TimelineTrackNames.Length - 1;
                 trackIndex++)
            {
                Fgui.GComponent track = timeline.GetChild(
                    TrackChildName(projection.Tracks[trackIndex].Kind))?.asCom;
                Fgui.GComponent content = track?.GetChild("trackContent")?.asCom;
                if (content == null)
                    continue;

                Color32 accent = RuntimeFrameTrackAccent(
                    projection.Tracks[trackIndex].Kind);
                float timelineWidth = Mathf.Max(1f, content.width);
                RuntimeFrameTimelineVisualMetrics metrics =
                    TimelineGeometry.ResolveVisualMetrics(projection.Viewport, timelineWidth);
                float markerSize = metrics.MarkerSize;
                float glyphSize = metrics.MarkerGlyphSize;
                for (int sampleIndex = 1;
                     sampleIndex <= sampleCount && markerIndex < TimelineMarkerCapacity;
                     sampleIndex++)
                {
                    int frame = selectedFrame
                        + Mathf.RoundToInt(distance * sampleIndex / (float)(sampleCount + 1));
                    if (frame <= selectedFrame || frame >= latestFrame)
                        continue;

                    Fgui.GComponent marker = _renderer.VisualPool.Markers[markerIndex++];
                    marker.RemoveFromParent();
                    content.AddChild(marker);
                    marker.SetSize(markerSize, markerSize);
                    marker.SetXY(
                        Mathf.Clamp(
                            TimelineGeometry.RuntimeFrameToX(frame, projection.Viewport, timelineWidth)
                                - markerSize * 0.5f,
                            0f,
                            Mathf.Max(0f, timelineWidth - markerSize)),
                        Mathf.Max(0f, (content.height - markerSize) * 0.5f));
                    marker.visible = true;
                    marker.touchable = false;
                    marker.data = null;
                    marker.alpha = 0.9f;
                    SetTimelineChildVisible(marker, "selectionOutline", false);
                    BindText(marker, "markerCount", string.Empty);
                    Fgui.GGraph glyph = marker.GetChild("markerGlyph")?.asGraph;
                    if (glyph != null)
                    {
                        float inset = (markerSize - glyphSize) * 0.5f;
                        glyph.SetXY(inset, inset);
                        glyph.SetSize(glyphSize, glyphSize);
                        glyph.DrawEllipse(
                            glyphSize,
                            glyphSize,
                            RuntimeFrameSrgb(accent.r, accent.g, accent.b, 235));
                    }
                }
            }
        }

        private static bool HasStateAndEventEvidence(
            CharacterTestRuntimeAiLabTimelineTrackProjection track)
        {
            bool hasRuntimeFrameSpan = false;
            bool hasEventEvidence = false;
            for (int i = 0; i < track.Items.Count; i++)
            {
                CharacterTestRuntimeAiLabTimelineItem item = track.Items[i];
                if (item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.Span
                    && item.Provenance == CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord)
                {
                    hasRuntimeFrameSpan = true;
                }
                else if (item.Provenance != CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord)
                {
                    hasEventEvidence = true;
                }
            }
            return hasRuntimeFrameSpan && hasEventEvidence;
        }

        private void PlaceTimelineItem(
            Fgui.GComponent content,
            Fgui.GComponent visual,
            CharacterTestRuntimeAiLabTimelineItem item,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            bool spanItem,
            bool splitSemanticLanes,
            string selectedStableKey)
        {
            visual.RemoveFromParent();
            content.AddChild(visual);
            float timelineWidth = Mathf.Max(1f, content.width);
            RuntimeFrameTimelineVisualMetrics metrics =
                TimelineGeometry.ResolveVisualMetrics(viewport, timelineWidth);
            bool eventSpanLane = spanItem
                && splitSemanticLanes
                && item.Provenance != CharacterTestRuntimeAiLabTimelineItemProvenance.RuntimeFrameRecord;
            float startX;
            float width;
            if (spanItem)
            {
                TimelineGeometry.ResolveSpan(
                    item.StartFrame,
                    item.EndFrame,
                    viewport,
                    timelineWidth,
                    out startX,
                    out width);
            }
            else
            {
                width = metrics.MarkerSize;
                startX = Mathf.Clamp(
                    TimelineGeometry.RuntimeFrameToX(item.StartFrame, viewport, timelineWidth)
                        - width * 0.5f,
                    0f,
                    Mathf.Max(0f, timelineWidth - width));
            }

            float height = spanItem
                ? eventSpanLane
                    ? TimelineEventLaneHeight
                    : splitSemanticLanes
                        ? TimelineSplitStateSpanHeight
                        : TimelineSpanHeight
                : metrics.MarkerSize;
            float y = splitSemanticLanes
                ? eventSpanLane || !spanItem
                    ? Mathf.Max(0f, content.height - height - 2f)
                    : 2f
                : Mathf.Max(0f, (content.height - height) * 0.5f);
            visual.SetXY(startX, y);
            visual.SetSize(width, height);
            visual.visible = true;
            visual.data = item;
            CharacterTestRuntimeAiLabTimelineDetailViewModel semanticDetail =
                ResolveTimelineDetail(item);
            _bindTooltip?.Invoke(
                visual,
                semanticDetail?.TooltipText
                    ?? CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null)?.TooltipText
                    ?? string.Empty);
            SetTimelineChildVisible(
                visual,
                "selectionOutline",
                string.Equals(item.StableKey, selectedStableKey, StringComparison.Ordinal));
            if (spanItem)
            {
                bool showLabel = !eventSpanLane
                    && metrics.ShowsLabels
                    && width >= metrics.MinimumLabelWidth;
                string displayLabel = showLabel
                    ? RuntimeFrameTimelineDisplayLabel(
                        item,
                        metrics.Density == RuntimeFrameTimelineVisualDensity.Detailed,
                        semanticDetail?.Title)
                    : string.Empty;
                BindText(visual, "spanLabel", displayLabel);
                Color32 accent = RuntimeFrameTrackAccent(item.TrackKind);
                Fgui.GObject continuation = visual.GetChild("spanContinuation");
                if (continuation != null)
                {
                    continuation.width = width;
                    continuation.visible = false;
                    if (continuation.asGraph != null)
                    {
                        continuation.asGraph.color =
                            RuntimeFrameSrgb(accent.r, accent.g, accent.b, 96);
                    }
                }
                Fgui.GObject fill = visual.GetChild("spanFill");
                if (fill != null)
                {
                    fill.width = width;
                    fill.height = Mathf.Max(0f, height - 2f);
                    if (fill.asGraph != null)
                    {
                        byte borderAlpha = metrics.Density == RuntimeFrameTimelineVisualDensity.Dense
                            ? (byte)145
                            : (byte)185;
                        byte fillAlpha = metrics.Density == RuntimeFrameTimelineVisualDensity.Dense
                            ? (byte)40
                            : (byte)58;
                        fill.asGraph.color = Color.white;
                        fill.asGraph.DrawRect(
                            width,
                            Mathf.Max(0f, height - 2f),
                            1,
                            RuntimeFrameSrgb(accent.r, accent.g, accent.b, borderAlpha),
                            RuntimeFrameSrgb(accent.r, accent.g, accent.b, fillAlpha));
                    }
                }
                Fgui.GObject label = visual.GetChild("spanLabel");
                if (label != null)
                {
                    label.visible = showLabel;
                    label.SetXY(8f, 2f);
                    label.width = Mathf.Max(0f, width - 16f);
                    label.height = Mathf.Max(0f, height - 2f);
                    if (label is Fgui.GTextField text)
                    {
                        SetTimelineFontSize(text, metrics.LabelFontSize);
                        text.color = RuntimeFrameSrgb(232, 243, 248);
                    }
                }
                Fgui.GObject outline = visual.GetChild("selectionOutline");
                if (outline != null)
                {
                    outline.width = width;
                    outline.height = height;
                }
            }
            else
            {
                Fgui.GGraph marker = visual.GetChild("markerGlyph")?.asGraph;
                if (marker != null)
                {
                    Color32 accent = RuntimeFrameTrackAccent(item.TrackKind);
                    float glyphSize = metrics.MarkerGlyphSize;
                    float inset = (metrics.MarkerSize - glyphSize) * 0.5f;
                    marker.SetXY(inset, inset);
                    marker.SetSize(glyphSize, glyphSize);
                    marker.DrawEllipse(
                        glyphSize,
                        glyphSize,
                        RuntimeFrameSrgb(accent.r, accent.g, accent.b));
                }
                Fgui.GTextField count = visual.GetChild("markerCount")?.asTextField;
                if (count != null)
                {
                    count.SetXY(0f, 0f);
                    count.SetSize(metrics.MarkerSize, metrics.MarkerSize);
                    SetTimelineFontSize(
                        count,
                        metrics.Density == RuntimeFrameTimelineVisualDensity.Dense ? 7 : 9);
                    count.text = item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ExactFrameStack
                        || item.ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.ZoomDensityAggregate
                        ? item.Count.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;
                }
                Fgui.GObject outline = visual.GetChild("selectionOutline");
                if (outline != null)
                {
                    outline.SetXY(0f, 0f);
                    outline.SetSize(metrics.MarkerSize, metrics.MarkerSize);
                }
            }
        }

        private static string RuntimeFrameTimelineDisplayLabel(
            CharacterTestRuntimeAiLabTimelineItem item,
            bool includeFrame,
            string semanticTitle = null)
        {
            string label = semanticTitle?.Trim() ?? string.Empty;
            if (item == null)
                return string.Empty;
            if (string.IsNullOrEmpty(label))
            {
                label = CharacterTestRuntimeAiLabTimelineDetailProjector
                    .Project(item, null)?.Title ?? string.Empty;
            }
            int maximumLength = includeFrame ? 12 : 8;
            if (label.Length > maximumLength)
                label = label.Substring(0, maximumLength);
            if (string.IsNullOrEmpty(label))
                label = CharacterTestRuntimeAiLabTimelineDetailFormatter.BuildCausalStageTitle(
                    (int)item.TrackKind);
            return includeFrame
                ? label + "  " + item.StartFrame.ToString(CultureInfo.InvariantCulture)
                : label;
        }

        private CharacterTestRuntimeAiLabTimelineDetailViewModel ResolveTimelineDetail(
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (_detailWorkspace != null
                && _detailWorkspace.TryGetTimelineDetail(
                    item,
                    out CharacterTestRuntimeAiLabTimelineDetailViewModel detail))
            {
                return detail;
            }
            return CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null);
        }

        private static Color32 RuntimeFrameTrackAccent(
            CharacterTestRuntimeAiLabTimelineTrackKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabTimelineTrackKind.Perception:
                    return new Color32(78, 219, 213, 255);
                case CharacterTestRuntimeAiLabTimelineTrackKind.Decision:
                    return new Color32(76, 190, 232, 255);
                case CharacterTestRuntimeAiLabTimelineTrackKind.Action:
                    return new Color32(234, 190, 55, 255);
                case CharacterTestRuntimeAiLabTimelineTrackKind.Result:
                    return new Color32(105, 199, 112, 255);
                default:
                    return new Color32(244, 178, 56, 255);
            }
        }

        internal static Color RuntimeFrameTrackAccentColor(
            CharacterTestRuntimeAiLabTimelineTrackKind kind,
            byte alpha = 255)
        {
            Color32 accent = RuntimeFrameTrackAccent(kind);
            return RuntimeFrameSrgb(accent.r, accent.g, accent.b, alpha);
        }

        private static float ResolveTimelineWidth(Fgui.GComponent timeline)
        {
            Fgui.GObject ruler = timeline?.GetChild("timelineRuler");
            return ruler != null && ruler.width > 0f ? ruler.width : FormalTimelineWidth;
        }

        private static string TrackChildName(CharacterTestRuntimeAiLabTimelineTrackKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabTimelineTrackKind.Perception: return "trackRevision";
                case CharacterTestRuntimeAiLabTimelineTrackKind.Decision: return "trackDecision";
                case CharacterTestRuntimeAiLabTimelineTrackKind.Action: return "trackAction";
                case CharacterTestRuntimeAiLabTimelineTrackKind.Result: return "trackCombat";
                default: return "trackWarnings";
            }
        }
    }
}
