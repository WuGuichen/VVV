using System;
using System.Globalization;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns one timeline renderer's ruler ticks and runtime-domain overlays.</summary>
    /// <remarks>NoAlloc after the renderer visual pool is preheated.</remarks>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabTimelineRulerBinder
    {
        private readonly CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder _renderer;
        private const float FormalTimelineRulerX =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.LabelWidth;
        private const float FormalTimelineWidth =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.CanvasWidth;
        private const int TimelineTickCapacity = 64;

        internal CharacterTestFairyGuiRuntimeAiLabTimelineRulerBinder(
            CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder renderer)
        {
            _renderer = renderer;
        }
        internal void BindTicks(
            Fgui.GComponent timeline,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            int selectedFrame)
        {
            float timelineWidth = ResolveTimelineWidth(timeline);
            int rawStep = Math.Max(1, Mathf.CeilToInt(viewport.FrameSpan / 6f));
            int step = viewport.FrameSpan <= 120 ? 15 : NiceFrameStep(rawStep);
            int minorStep = Math.Max(1, step / 5);
            int first = ((viewport.StartFrame + step - 1) / step) * step;
            int tickIndex = 0;
            if (viewport.StartFrame < first && tickIndex < TimelineTickCapacity)
            {
                RenderTimelineTick(
                    timeline,
                    viewport,
                    timelineWidth,
                    viewport.StartFrame,
                    true,
                    viewport.StartFrame != selectedFrame,
                    tickIndex++);
            }
            int firstMinor = ((viewport.StartFrame + minorStep - 1) / minorStep) * minorStep;
            if (firstMinor == viewport.StartFrame && viewport.StartFrame < first)
                firstMinor += minorStep;
            for (int frame = firstMinor;
                 frame <= viewport.EndFrame && tickIndex < TimelineTickCapacity;
                 frame += minorStep)
            {
                bool major = frame == first || frame % step == 0;
                RenderTimelineTick(
                    timeline,
                    viewport,
                    timelineWidth,
                    frame,
                    major,
                    frame != selectedFrame,
                    tickIndex++);
            }
        }

        private void RenderTimelineTick(
            Fgui.GComponent timeline,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            float timelineWidth,
            int frame,
            bool major,
            bool showLabel,
            int tickIndex)
        {
            Fgui.GComponent tick = _renderer.VisualPool.Ticks[tickIndex];
            tick.RemoveFromParent();
            timeline.AddChild(tick);
            tick.SetXY(
                FormalTimelineRulerX + FrameToTimelineX(frame, viewport, timelineWidth) - 28f,
                0f);
            SetObjectGeometry(tick.GetChild("tickLabel"), 0f, 0f, 56f, 28f);
            SetObjectGeometry(
                tick.GetChild("tickLine"),
                27f,
                major ? 28f : 32f,
                1f,
                major ? 12f : 8f);
            Fgui.GTextField label = tick.GetChild("tickLabel")?.asTextField;
            SetTimelineFontSize(label, 20);
            if (label != null)
                label.color = RuntimeFrameSrgb(196, 210, 217);
            SetRuntimeFrameGraphColor(
                tick.GetChild("tickLine"),
                84,
                113,
                132);
            tick.touchable = false;
            tick.sortingOrder = 50;
            tick.visible = true;
            BindText(
                tick,
                "tickLabel",
                major && showLabel
                    ? frame.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);
        }

        private static void SetTimelineFontSize(Fgui.GTextField label, int size)
        {
            if (label == null)
                return;
            Fgui.TextFormat format = label.textFormat;
            if (format.size == size)
                return;
            format.size = size;
            label.textFormat = format;
        }

        internal static void BindDomain(
            Fgui.GComponent timeline,
            CharacterTestRuntimeAiLabTimelineProjection projection,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            CharacterTestRuntimeAiLabTimelineViewport viewport = projection.Viewport;
            float timelineWidth = ResolveTimelineWidth(timeline);
            float playheadX = ClampTimelinePlayheadX(
                FormalTimelineRulerX + FrameToTimelineX(
                    projection.Domain.SelectedFrame,
                    viewport,
                    timelineWidth),
                timelineWidth);
            Fgui.GObject playhead = timeline.GetChild("timelinePlayhead");
            if (playhead != null)
            {
                playhead.x = playheadX;
            }
            Fgui.GObject playheadHitArea = timeline.GetChild("timelinePlayheadHitArea");
            if (playheadHitArea != null)
            {
                playheadHitArea.x = playheadX - playheadHitArea.width * 0.5f;
                playheadHitArea.visible = true;
            }
            Fgui.GObject historyProgress = timeline.GetChild("timelineHistoryProgress");
            if (historyProgress != null)
            {
                historyProgress.x = FormalTimelineRulerX;
                historyProgress.width = Mathf.Max(1f, playheadX - FormalTimelineRulerX);
                historyProgress.visible = true;
            }
            Fgui.GObject label = timeline.GetChild("timelineSelectedFrameLabel");
            float labelX = FormalTimelineRulerX;
            if (label != null)
            {
                labelX = ClampTimelineLabelX(playheadX, label.width, timelineWidth);
                label.x = labelX;
                if (label is Fgui.GTextField text)
                    text.text = projection.Domain.SelectedFrame.ToString(CultureInfo.InvariantCulture);
            }
            Fgui.GObject badge = timeline.GetChild("timelineSelectedFrameBadge");
            if (badge != null)
            {
                badge.x = labelX;
                badge.visible = true;
            }

            Fgui.GObject future = timeline.GetChild("timelineFutureMask");
            if (future != null)
            {
                float x = FormalTimelineRulerX + FrameToTimelineX(
                    projection.Domain.LatestFrame + 1,
                    viewport,
                    timelineWidth);
                future.x = Mathf.Clamp(x, FormalTimelineRulerX, FormalTimelineRulerX + timelineWidth);
                future.width = Mathf.Max(0f, FormalTimelineRulerX + timelineWidth - future.x);
                future.visible = future.width > 0f;
            }

            Fgui.GObject retained = timeline.GetChild("timelineRetainedMask");
            if (retained != null)
            {
                float retainedX = FormalTimelineRulerX + FrameToTimelineX(
                    viewModel.TimelineRetainedStartFrame,
                    viewport,
                    timelineWidth);
                retained.x = FormalTimelineRulerX;
                retained.width = Mathf.Clamp(retainedX - FormalTimelineRulerX, 0f, timelineWidth);
                retained.visible = retained.width > 0f;
            }

            Fgui.GObject futureAdvance = timeline.GetChild("timelineFutureAdvanceFill");
            if (futureAdvance != null)
            {
                bool reviewingRecordedFuture =
                    projection.Domain.SelectedFrame < projection.Domain.LatestFrame;
                int futureStartFrame = viewModel.TimelineFutureAdvanceActive
                    ? viewModel.TimelineFutureAdvanceStartFrame
                    : projection.Domain.SelectedFrame;
                float startX = FormalTimelineRulerX + FrameToTimelineX(
                    futureStartFrame,
                    viewport,
                    timelineWidth);
                float currentX = FormalTimelineRulerX + FrameToTimelineX(
                    projection.Domain.LatestFrame + 1,
                    viewport,
                    timelineWidth);
                futureAdvance.x = Mathf.Clamp(startX, FormalTimelineRulerX, FormalTimelineRulerX + timelineWidth);
                futureAdvance.width = Mathf.Clamp(currentX - startX, 0f, timelineWidth);
                futureAdvance.visible = (viewModel.TimelineFutureAdvanceActive
                        || reviewingRecordedFuture)
                    && futureAdvance.width > 0f;
                Fgui.GGraph futureAdvanceGraph = futureAdvance.asGraph;
                if (futureAdvanceGraph != null)
                {
                    futureAdvanceGraph.DrawRect(
                        futureAdvance.width,
                        futureAdvance.height,
                        0,
                        Color.clear,
                        RuntimeFrameSrgb(27, 166, 170, 24));
                }
            }

            Fgui.GObject loop = timeline.GetChild("timelineLoopRange");
            if (loop != null)
            {
                float startX = FormalTimelineRulerX + FrameToTimelineX(
                    viewModel.TimelineLoopStartFrame,
                    viewport,
                    timelineWidth);
                float endX = FormalTimelineRulerX + FrameToTimelineX(
                    viewModel.TimelineLoopEndFrame,
                    viewport,
                    timelineWidth);
                loop.x = Mathf.Clamp(startX, FormalTimelineRulerX, FormalTimelineRulerX + timelineWidth);
                loop.width = Mathf.Clamp(endX - startX, 0f, timelineWidth);
                loop.visible = viewModel.TimelineHasLoopRange && loop.width > 0f;
            }
        }

        internal static float ClampTimelinePlayheadX(float x)
        {
            return Mathf.Clamp(
                x,
                FormalTimelineRulerX + 3f,
                FormalTimelineRulerX + FormalTimelineWidth - 3f);
        }

        private static float ClampTimelinePlayheadX(float x, float timelineWidth)
        {
            return Mathf.Clamp(
                x,
                FormalTimelineRulerX + 3f,
                FormalTimelineRulerX + timelineWidth - 3f);
        }

        internal static float ClampTimelineLabelX(float playheadX, float labelWidth)
        {
            return Mathf.Clamp(
                playheadX - labelWidth * 0.5f,
                FormalTimelineRulerX,
                FormalTimelineRulerX + FormalTimelineWidth - labelWidth);
        }

        private static float ClampTimelineLabelX(
            float playheadX,
            float labelWidth,
            float timelineWidth)
        {
            return Mathf.Clamp(
                playheadX - labelWidth * 0.5f,
                FormalTimelineRulerX,
                FormalTimelineRulerX + timelineWidth - labelWidth);
        }

        private static int NiceFrameStep(int minimum)
        {
            int power = 1;
            while (power * 10 < minimum)
                power *= 10;
            if (minimum <= power)
                return power;
            if (minimum <= power * 2)
                return power * 2;
            if (minimum <= power * 5)
                return power * 5;
            return power * 10;
        }

        private static float FrameToTimelineX(
            int frame,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            float timelineWidth = FormalTimelineWidth)
        {
            int range = Math.Max(1, viewport.EndFrame - viewport.StartFrame);
            return Mathf.Clamp01((frame - viewport.StartFrame) / (float)range) * timelineWidth;
        }

        private static float ResolveTimelineWidth(Fgui.GComponent timeline)
        {
            Fgui.GObject ruler = timeline?.GetChild("timelineRuler");
            return ruler != null && ruler.width > 0f ? ruler.width : FormalTimelineWidth;
        }

    }
}
