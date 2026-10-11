using System;
using System.Collections.Generic;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal enum TimelineWorkspaceMode
    {
        Hidden = 0,
        RuntimeFrame = 1,
        BehaviorPlan = 2,
        BehaviorLogic = 3
    }

    /// <summary>Owns page-mode and root-scoped state for the shared timeline workspace.</summary>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceSession
    {
        internal Fgui.GComponent Root { get; private set; }
        internal CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel ViewModel { get; private set; }
        internal TimelineWorkspaceMode Mode { get; private set; } = TimelineWorkspaceMode.Hidden;

        internal void Bind(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Root = root;
            ViewModel = viewModel;
        }

        internal bool TrySwitchMode(TimelineWorkspaceMode nextMode)
        {
            if (Mode == nextMode)
                return false;

            Mode = nextMode;
            return true;
        }

        internal void ResetForRoot(Fgui.GComponent root)
        {
            Root = root;
            ViewModel = null;
            Mode = TimelineWorkspaceMode.Hidden;
        }
    }

    internal static class CharacterTestFairyGuiRuntimeAiLabVisualBinding
    {
        /// <remarks>NoAlloc: presentation-only color conversion.</remarks>
        internal static Color RuntimeFrameSrgb(
            byte red,
            byte green,
            byte blue,
            byte alpha = 255)
        {
            return new Color(
                Mathf.GammaToLinearSpace(red / 255f),
                Mathf.GammaToLinearSpace(green / 255f),
                Mathf.GammaToLinearSpace(blue / 255f),
                alpha / 255f);
        }

        internal static void SetRuntimeFrameGraphColor(
            Fgui.GObject target,
            byte red,
            byte green,
            byte blue,
            byte alpha = 255)
        {
            Fgui.GGraph graph = target?.asGraph;
            if (graph != null)
                graph.color = RuntimeFrameSrgb(red, green, blue, alpha);
        }

        internal static void SetObjectGeometry(
            Fgui.GObject target,
            float x,
            float y,
            float width,
            float height)
        {
            if (target == null)
                return;
            target.SetSize(width, height);
            target.SetXY(x, y);
        }

        internal static void SetTimelineFontSize(Fgui.GTextField label, int size)
        {
            if (label == null)
                return;
            Fgui.TextFormat format = label.textFormat;
            if (format.size == size)
                return;
            format.size = size;
            label.textFormat = format;
        }

        internal static void SetTimelineChildVisible(
            Fgui.GComponent root,
            string childName,
            bool visible)
        {
            Fgui.GObject child = root?.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }
    }

    internal enum RuntimeFrameTimelineVisualDensity
    {
        Dense = 0,
        Compact = 1,
        Detailed = 2
    }

    internal readonly struct RuntimeFrameTimelineVisualMetrics
    {
        internal RuntimeFrameTimelineVisualMetrics(
            RuntimeFrameTimelineVisualDensity density,
            float spanHeight,
            float markerSize,
            float markerGlyphSize,
            float minimumLabelWidth,
            int labelFontSize)
        {
            Density = density;
            SpanHeight = spanHeight;
            MarkerSize = markerSize;
            MarkerGlyphSize = markerGlyphSize;
            MinimumLabelWidth = minimumLabelWidth;
            LabelFontSize = labelFontSize;
        }

        internal RuntimeFrameTimelineVisualDensity Density { get; }
        internal float SpanHeight { get; }
        internal float MarkerSize { get; }
        internal float MarkerGlyphSize { get; }
        internal float MinimumLabelWidth { get; }
        internal int LabelFontSize { get; }
        internal bool ShowsLabels => Density != RuntimeFrameTimelineVisualDensity.Dense;
    }

    /// <summary>Pure timeline geometry with no FairyGUI object dependency.</summary>
    internal static class CharacterTestRuntimeAiLabTimelineGeometry
    {
        internal const float RulerX = CharacterTestRuntimeAiLabTimelineWorkbenchLayout.LabelWidth;
        internal const float DefaultWidth = CharacterTestRuntimeAiLabTimelineWorkbenchLayout.CanvasWidth;
        internal const float CausalCardWidth = 464f;
        internal const float CausalCardHeight = 88f;
        internal const float CausalIconSize = 52f;
        internal const int CausalTitleFontSize = 18;
        internal const int CausalValueFontSize = 16;

        internal static RuntimeFrameTimelineVisualMetrics ResolveVisualMetrics(
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            float timelineWidth)
        {
            float pixelsPerFrame = Mathf.Max(1f, timelineWidth)
                / Math.Max(1, viewport.FrameSpan);
            if (pixelsPerFrame < 4f)
            {
                return new RuntimeFrameTimelineVisualMetrics(
                    RuntimeFrameTimelineVisualDensity.Dense,
                    48f,
                    18f,
                    12f,
                    float.PositiveInfinity,
                    18);
            }
            if (pixelsPerFrame < 10f)
            {
                return new RuntimeFrameTimelineVisualMetrics(
                    RuntimeFrameTimelineVisualDensity.Compact,
                    48f,
                    18f,
                    12f,
                    72f,
                    18);
            }
            return new RuntimeFrameTimelineVisualMetrics(
                RuntimeFrameTimelineVisualDensity.Detailed,
                48f,
                18f,
                12f,
                104f,
                18);
        }

        internal static void ResolveSpan(
            int startRuntimeFrame,
            int endRuntimeFrame,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            float timelineWidth,
            out float x,
            out float width)
        {
            float safeWidth = Mathf.Max(1f, timelineWidth);
            int frameSpan = Math.Max(1, viewport.FrameSpan);
            int clippedStart = Math.Max(
                viewport.StartFrame,
                Math.Min(viewport.EndFrame, startRuntimeFrame));
            int clippedEndExclusive = Math.Max(
                clippedStart + 1,
                Math.Min(
                    viewport.EndFrame + 1,
                    Math.Max(startRuntimeFrame, endRuntimeFrame) + 1));
            x = (clippedStart - viewport.StartFrame) * safeWidth / frameSpan;
            float endX = (clippedEndExclusive - viewport.StartFrame) * safeWidth / frameSpan;
            width = Mathf.Max(0f, Mathf.Min(safeWidth, endX) - x);
        }

        internal static float ClampPlayheadX(float x, float timelineWidth = DefaultWidth)
        {
            return Mathf.Clamp(x, RulerX + 3f, RulerX + timelineWidth - 3f);
        }

        internal static float ClampLabelX(
            float playheadX,
            float labelWidth,
            float timelineWidth = DefaultWidth)
        {
            return Mathf.Clamp(
                playheadX - labelWidth * 0.5f,
                RulerX,
                RulerX + timelineWidth - labelWidth);
        }

        internal static float RuntimeFrameToX(
            int runtimeFrame,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            float timelineWidth = DefaultWidth)
        {
            int range = Math.Max(1, viewport.EndFrame - viewport.StartFrame);
            return Mathf.Clamp01((runtimeFrame - viewport.StartFrame) / (float)range)
                * timelineWidth;
        }

        internal static int NiceFrameStep(int minimum)
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

        internal static float ResolveDetailHeight(CharacterTestRuntimeAiLabTimelineItem item)
        {
            return CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelHeight;
        }
    }

    /// <summary>Pure text projection for RuntimeFrame event details.</summary>
    internal static class CharacterTestRuntimeAiLabTimelineDetailFormatter
    {
        internal static string BuildCausalStageTitle(int index)
        {
            switch (index)
            {
                case 0: return "感知";
                case 1: return "判断";
                case 2: return "行为";
                default: return "结果";
            }
        }

        internal static string BuildMeta(CharacterTestRuntimeAiLabTimelineItem item)
        {
            return CharacterTestRuntimeAiLabTimelineDetailProjector.BuildMetaText(item);
        }

        internal static string BuildFrame(CharacterTestRuntimeAiLabTimelineItem item)
        {
            return CharacterTestRuntimeAiLabTimelineDetailProjector.BuildFrameText(item);
        }

        internal static string BuildSummary(CharacterTestRuntimeAiLabTimelineItem item)
        {
            return CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null)
                ?.DetailLine1 ?? string.Empty;
        }

        internal static string BuildParticipants(CharacterTestRuntimeAiLabTimelineItem item)
        {
            return CharacterTestRuntimeAiLabTimelineDetailProjector.BuildParticipantsText(item);
        }

        internal static string BuildEvidence(
            CharacterTestRuntimeAiLabTimelineItem item,
            bool full)
        {
            CharacterTestRuntimeAiLabTimelineDetailViewModel detail =
                CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null);
            return full ? detail?.TooltipText ?? string.Empty : detail?.DetailLine3 ?? string.Empty;
        }
    }

    /// <summary>Owns one timeline surface's reusable FairyGUI visuals.</summary>
    /// <remarks>LowFreqAlloc while a pool reaches a new high-water mark; NoAlloc afterward.</remarks>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabTimelineVisualPool
    {
        internal readonly List<Fgui.GComponent> Markers = new List<Fgui.GComponent>(64);
        internal readonly List<Fgui.GComponent> Spans = new List<Fgui.GComponent>(32);
        internal readonly List<Fgui.GComponent> Ticks = new List<Fgui.GComponent>(24);

        internal bool Preheated { get; set; }

        internal bool CanRender(
            CharacterTestRuntimeAiLabTimelineProjection projection,
            int markerCapacity,
            int spanCapacity)
        {
            int markerCount = 0;
            int spanCount = 0;
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> items =
                    projection.Tracks[trackIndex].Items;
                for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    if (items[itemIndex].ItemKind == CharacterTestRuntimeAiLabTimelineItemKind.Span)
                        spanCount++;
                    else
                        markerCount++;
                }
            }
            return markerCount <= markerCapacity && spanCount <= spanCapacity;
        }

        internal void EnsurePreheated(
            int markerCapacity,
            int spanCapacity,
            int tickCapacity,
            Action<Fgui.GComponent> bindInteractiveVisual)
        {
            if (Preheated)
                return;
            Preheat(
                Markers,
                markerCapacity,
                "RuntimeAiLabTimelineMarker",
                bindInteractiveVisual);
            Preheat(
                Spans,
                spanCapacity,
                "RuntimeAiLabTimelineSpan",
                bindInteractiveVisual);
            Preheat(Ticks, tickCapacity, "RuntimeAiLabTimelineTick", null);
            Preheated = true;
        }

        internal void HideAll()
        {
            for (int index = 0; index < Markers.Count; index++)
                ResetVisual(Markers[index]);
            for (int index = 0; index < Spans.Count; index++)
                ResetVisual(Spans[index]);
            for (int index = 0; index < Ticks.Count; index++)
                ResetVisual(Ticks[index]);
        }

        internal void ResetForRoot()
        {
            Markers.Clear();
            Spans.Clear();
            Ticks.Clear();
            Preheated = false;
        }

        internal static void ResetVisual(Fgui.GComponent visual)
        {
            if (visual == null)
                return;
            visual.visible = false;
            visual.data = null;
            visual.tooltips = string.Empty;
            visual.SetXY(0f, 0f);
            visual.alpha = 1f;
            visual.rotation = 0f;
            visual.scaleX = 1f;
            visual.scaleY = 1f;
            visual.grayed = false;
            visual.touchable = true;
            Fgui.GObject selectionOutline = visual.GetChild("selectionOutline");
            if (selectionOutline != null)
            {
                selectionOutline.visible = false;
                selectionOutline.SetXY(0f, 0f);
                selectionOutline.width = 18f;
            }
            Fgui.GTextField markerCount = visual.GetChild("markerCount")?.asTextField;
            if (markerCount != null)
                markerCount.text = string.Empty;
            Fgui.GTextField spanLabel = visual.GetChild("spanLabel")?.asTextField;
            if (spanLabel != null)
            {
                spanLabel.text = string.Empty;
                spanLabel.width = 0f;
            }
            Fgui.GTextField tickLabel = visual.GetChild("tickLabel")?.asTextField;
            if (tickLabel != null)
            {
                tickLabel.text = string.Empty;
                tickLabel.width = 0f;
            }
            Fgui.GGraph markerGlyph = visual.GetChild("markerGlyph")?.asGraph;
            if (markerGlyph != null)
            {
                markerGlyph.color =
                    CharacterTestFairyGuiRuntimeAiLabVisualBinding.RuntimeFrameSrgb(
                        78,
                        219,
                        213);
            }
            Fgui.GGraph spanFill = visual.GetChild("spanFill")?.asGraph;
            if (spanFill != null)
            {
                spanFill.color =
                    CharacterTestFairyGuiRuntimeAiLabVisualBinding.RuntimeFrameSrgb(
                        41,
                        72,
                        93,
                        218);
                spanFill.width = 0f;
            }
            Fgui.GGraph spanContinuation = visual.GetChild("spanContinuation")?.asGraph;
            if (spanContinuation != null)
            {
                spanContinuation.color =
                    CharacterTestFairyGuiRuntimeAiLabVisualBinding.RuntimeFrameSrgb(
                        78,
                        219,
                        213,
                        96);
                spanContinuation.width = 0f;
                spanContinuation.visible = false;
            }
            visual.RemoveFromParent();
        }

        private static void Preheat(
            List<Fgui.GComponent> pool,
            int capacity,
            string componentName,
            Action<Fgui.GComponent> bindInteractiveVisual)
        {
            for (int index = pool.Count; index < capacity; index++)
            {
                Fgui.GComponent created = Fgui.UIPackage.CreateObject(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    componentName)?.asCom;
                if (created == null)
                {
                    throw new InvalidOperationException(
                        componentName + " is missing from the Runtime AI Lab package.");
                }
                ResetVisual(created);
                bindInteractiveVisual?.Invoke(created);
                pool.Add(created);
            }
        }
    }

    /// <summary>
    /// Owns renderer-local cache state. Live RuntimeFrame and Replay always use distinct instances.
    /// </summary>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder
    {
        internal CharacterTestFairyGuiRuntimeAiLabTimelineVisualPool VisualPool { get; } =
            new CharacterTestFairyGuiRuntimeAiLabTimelineVisualPool();

        internal CharacterTestRuntimeAiLabTimelineProjection LastProjection { get; set; }
        internal string LastSelectedStableKey { get; set; } = string.Empty;

        internal void ResetForRoot()
        {
            VisualPool.ResetForRoot();
            LastProjection = null;
            LastSelectedStableKey = string.Empty;
        }
    }

    /// <summary>Owns the live RuntimeFrame timeline session and scrub gesture lifecycle.</summary>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineBinder
    {
        internal CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder Renderer { get; } =
            new CharacterTestFairyGuiRuntimeAiLabFiveTrackTimelineBinder();
        internal CharacterTestRuntimeAiLabTimelineViewportInputBinder Input { get; } =
            new CharacterTestRuntimeAiLabTimelineViewportInputBinder();
        internal bool ScrubActive { get; set; }

        internal void ResetForRoot()
        {
            Renderer.ResetForRoot();
            ScrubActive = false;
            Input.Reset();
        }

        internal void Hide()
        {
            ScrubActive = false;
            Input.Reset();
        }

        internal static bool ShouldKeepScrubEnabled(
            bool canJumpRuntimeFrame,
            bool scrubLatched,
            bool pointerGestureActive)
        {
            return canJumpRuntimeFrame || scrubLatched || pointerGestureActive;
        }

        internal static bool ShouldKeepPreview(bool scrubLatched, bool pointerGestureActive)
        {
            return scrubLatched || pointerGestureActive;
        }
    }

    internal interface ICharacterTestRuntimeAiLabTimelineCommandSink
    {
        void Enqueue(string commandId);
        void Enqueue(string commandId, object payload);
        void JumpToRuntimeFrame(object runtimeFramePayload);
    }

    /// <summary>Maps typed timeline presentation intent onto the existing panel command port.</summary>
    internal sealed class CharacterTestRuntimeAiLabTimelineCommandAdapter :
        ICharacterTestRuntimeAiLabTimelineCommandSink
    {
        private readonly IMxUiCommandSink _sink;
        private readonly MxUiViewId _viewId;

        internal CharacterTestRuntimeAiLabTimelineCommandAdapter(
            IMxUiCommandSink sink,
            MxUiViewId viewId)
        {
            _sink = sink;
            _viewId = viewId;
        }

        public void Enqueue(string commandId)
        {
            _sink?.Enqueue(new MxUiCommand(_viewId, commandId, null));
        }

        public void Enqueue(string commandId, object payload)
        {
            _sink?.Enqueue(new MxUiCommand(_viewId, commandId, payload));
        }

        public void JumpToRuntimeFrame(object runtimeFramePayload)
        {
            _sink?.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.JumpFrame,
                runtimeFramePayload));
        }
    }

    /// <summary>Owns Behavior Logic decision actions exposed from the shared details surface.</summary>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabBehaviorLogicDecisionBinder
    {
        private readonly ICharacterTestRuntimeAiLabTimelineCommandSink _commands;
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorPlanDecisionBinder _planDecision;

        internal CharacterTestFairyGuiRuntimeAiLabBehaviorLogicDecisionBinder(
            ICharacterTestRuntimeAiLabTimelineCommandSink commands,
            CharacterTestFairyGuiRuntimeAiLabBehaviorPlanDecisionBinder planDecision)
        {
            _commands = commands;
            _planDecision = planDecision;
        }

        internal bool TryOpenRepair(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel?.BehaviorCardWorkspace?.Timeline;
            string detectorId = timeline?.CurrentBehaviorRepairDetectorStableId;
            if (string.IsNullOrEmpty(detectorId))
                return false;

            _planDecision.TechnicalDetailsVisible = false;
            _commands.Enqueue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorLogic);
            _commands.Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectDetector,
                new CharacterTestRuntimeAiLabBehaviorCardElementCommand(
                    detectorId,
                    0,
                    CharacterTestRuntimeAiLabLogicElementKind.Detector));
            return true;
        }
    }
}
