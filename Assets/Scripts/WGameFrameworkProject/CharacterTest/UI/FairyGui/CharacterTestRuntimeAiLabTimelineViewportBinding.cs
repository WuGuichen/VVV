using System;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabTimelineViewportScrubPhase
    {
        Begin,
        Update,
        End
    }

    internal interface ICharacterTestRuntimeAiLabTimelineViewportIntentSink
    {
        void Select(CharacterTestRuntimeAiLabTimelineItemLocator locator);
        void Navigate(CharacterTestRuntimeAiLabTimelineItemLocator locator, bool useEndFrame);
        void ClearSelection();
        void Scrub(CharacterTestRuntimeAiLabTimelineViewportScrubPhase phase, int frame);
    }

    internal sealed class CharacterTestRuntimeAiLabLiveTimelineViewportIntentSink :
        ICharacterTestRuntimeAiLabTimelineViewportIntentSink
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;

        internal CharacterTestRuntimeAiLabLiveTimelineViewportIntentSink(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId)
        {
            _commandSink = commandSink;
            _viewId = viewId;
        }

        public void Select(CharacterTestRuntimeAiLabTimelineItemLocator locator)
        {
            Enqueue(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineSelectItem, locator);
        }

        public void Navigate(CharacterTestRuntimeAiLabTimelineItemLocator locator, bool useEndFrame)
        {
            Enqueue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineNavigateItem,
                new CharacterTestRuntimeAiLabTimelineNavigationCommand(locator, useEndFrame));
        }

        public void ClearSelection()
        {
            Enqueue(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineClearItemSelection, null);
        }

        public void Scrub(CharacterTestRuntimeAiLabTimelineViewportScrubPhase phase, int frame)
        {
            string commandId = phase == CharacterTestRuntimeAiLabTimelineViewportScrubPhase.Begin
                ? CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubBegin
                : phase == CharacterTestRuntimeAiLabTimelineViewportScrubPhase.Update
                    ? CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubUpdate
                    : CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubEnd;
            Enqueue(commandId, frame);
        }

        private void Enqueue(string commandId, object payload)
        {
            _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, payload));
        }
    }

    internal sealed class CharacterTestRuntimeAiLabReplayTimelineViewportIntentSink :
        ICharacterTestRuntimeAiLabTimelineViewportIntentSink
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;

        internal CharacterTestRuntimeAiLabReplayTimelineViewportIntentSink(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId)
        {
            _commandSink = commandSink;
            _viewId = viewId;
        }

        public void Select(CharacterTestRuntimeAiLabTimelineItemLocator locator)
        {
            Enqueue(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplaySelectItem, locator);
        }

        public void Navigate(CharacterTestRuntimeAiLabTimelineItemLocator locator, bool useEndFrame)
        {
            Enqueue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayNavigateItem,
                new CharacterTestRuntimeAiLabTimelineNavigationCommand(locator, useEndFrame));
        }

        public void ClearSelection()
        {
            Enqueue(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayClearItemSelection, null);
        }

        public void Scrub(CharacterTestRuntimeAiLabTimelineViewportScrubPhase phase, int frame)
        {
        }

        private void Enqueue(string commandId, object payload)
        {
            _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, payload));
        }
    }

    /// <summary>
    /// Binds the controller-neutral five-track viewport input surface. RuntimeFrame and Replay
    /// provide different intent sinks; this binder never reads a live controller.
    /// </summary>
    /// <remarks>
    /// NoAlloc for listener dispatch and pointer-to-frame mapping after initialization.
    /// Preview-label formatting is LowFreqAlloc and throttled to at most 10 Hz.
    /// </remarks>
    internal sealed class CharacterTestRuntimeAiLabTimelineViewportInputBinder
    {
        private const float RulerX = 168f;
        private const float SeekIntervalSeconds = 0.1f;
        private readonly Fgui.GObject[] _boundSurfaces = new Fgui.GObject[10];
        private int _boundSurfaceCount;
        private Fgui.GComponent _viewport;
        private CharacterTestRuntimeAiLabTimelineProjection _projection;
        private ICharacterTestRuntimeAiLabTimelineViewportIntentSink _intentSink;
        private bool _enabled;
        private bool _active;
        private bool _suppressClick;
        private bool _hasPreview;
        private float _previewX;
        private string _previewFrameText = string.Empty;
        private float _lastDispatchTime = float.NegativeInfinity;

        internal bool Active => _active;

        internal void Bind(
            Fgui.GComponent viewport,
            CharacterTestRuntimeAiLabTimelineProjection projection,
            bool enabled,
            ICharacterTestRuntimeAiLabTimelineViewportIntentSink intentSink)
        {
            if (!ReferenceEquals(_viewport, viewport))
            {
                for (int i = 0; i < _boundSurfaceCount; i++)
                    _boundSurfaces[i] = null;
                _boundSurfaceCount = 0;
            }
            _viewport = viewport;
            _projection = projection;
            _enabled = enabled;
            _intentSink = intentSink;
            BindSurface(viewport?.GetChild("timelineRuler"));
            BindSurface(viewport?.GetChild("timelineScrubHitArea"));
            BindSurface(viewport?.GetChild("timelinePlayheadHitArea"));
            BindSurface(viewport?.GetChild("timelineSelectedFrameBadge"));
            BindSurface(viewport?.GetChild("timelineSelectedFrameLabel"));
            BindTrackSurface(viewport, "trackRevision");
            BindTrackSurface(viewport, "trackDecision");
            BindTrackSurface(viewport, "trackAction");
            BindTrackSurface(viewport, "trackCombat");
            BindTrackSurface(viewport, "trackWarnings");
            for (int i = 0; i < _boundSurfaceCount; i++)
                _boundSurfaces[i].enabled = enabled;
            if (_active && _hasPreview)
                RenderPreview(_previewX, updateFrameText: false, frame: 0);
        }

        internal void Reset()
        {
            _projection = null;
            _intentSink = null;
            _enabled = false;
            _active = false;
            _suppressClick = false;
            ClearPreview();
        }

        private void BindTrackSurface(Fgui.GComponent viewport, string trackName)
        {
            Fgui.GComponent track = viewport?.GetChild(trackName)?.asCom;
            Fgui.GComponent content = track?.GetChild("trackContent")?.asCom;
            BindSurface(content?.GetChild("trackBackground"));
        }

        private void BindSurface(Fgui.GObject surface)
        {
            if (surface == null || IsBound(surface))
                return;
            if (_boundSurfaceCount >= _boundSurfaces.Length)
                return;
            _boundSurfaces[_boundSurfaceCount++] = surface;
            surface.onTouchBegin.Set(OnTouchBegin);
            surface.onTouchMove.Set(OnTouchMove);
            surface.onTouchEnd.Set(OnTouchEnd);
            surface.onClick.Set(OnClick);
        }

        private bool IsBound(Fgui.GObject surface)
        {
            for (int i = 0; i < _boundSurfaceCount; i++)
            {
                if (ReferenceEquals(_boundSurfaces[i], surface))
                    return true;
            }
            return false;
        }

        private void OnTouchBegin(Fgui.EventContext context)
        {
            _active = true;
            _suppressClick = true;
            context.CaptureTouch();
            PreviewAndDispatch(context, CharacterTestRuntimeAiLabTimelineViewportScrubPhase.Begin, true);
        }

        private void OnTouchMove(Fgui.EventContext context)
        {
            PreviewAndDispatch(context, CharacterTestRuntimeAiLabTimelineViewportScrubPhase.Update, false);
        }

        private void OnTouchEnd(Fgui.EventContext context)
        {
            PreviewAndDispatch(context, CharacterTestRuntimeAiLabTimelineViewportScrubPhase.End, true);
            _active = false;
            ClearPreview();
        }

        private void OnClick(Fgui.EventContext context)
        {
            if (_suppressClick)
            {
                _suppressClick = false;
                return;
            }
            PreviewAndDispatch(context, CharacterTestRuntimeAiLabTimelineViewportScrubPhase.End, true);
            ClearPreview();
        }

        private void PreviewAndDispatch(
            Fgui.EventContext context,
            CharacterTestRuntimeAiLabTimelineViewportScrubPhase phase,
            bool force)
        {
            if (!TryResolve(context, out int frame, out float x))
                return;
            bool dispatch = force || Time.unscaledTime - _lastDispatchTime >= SeekIntervalSeconds;
            RenderPreview(x, dispatch, frame);
            if (!dispatch)
                return;
            _lastDispatchTime = Time.unscaledTime;
            _intentSink?.Scrub(phase, frame);
        }

        private void RenderPreview(float x, bool updateFrameText, int frame)
        {
            _hasPreview = true;
            _previewX = x;
            if (updateFrameText)
                _previewFrameText = frame.ToString();
            Fgui.GObject playhead = _viewport?.GetChild("timelinePreviewPlayhead");
            Fgui.GObject badge = _viewport?.GetChild("timelinePreviewFrameBadge");
            Fgui.GTextField label = _viewport?.GetChild("timelinePreviewFrameLabel") as Fgui.GTextField;
            float rulerWidth = ResolveRulerWidth();
            if (playhead != null)
            {
                playhead.x = x;
                playhead.visible = true;
            }
            if (label != null)
            {
                label.x = Mathf.Clamp(x - label.width * 0.5f, RulerX, RulerX + rulerWidth - label.width);
                label.visible = true;
                if (updateFrameText || string.IsNullOrEmpty(label.text))
                    label.text = _previewFrameText;
                if (badge != null)
                {
                    badge.x = label.x;
                    badge.visible = true;
                }
            }
        }

        private bool TryResolve(Fgui.EventContext context, out int frame, out float x)
        {
            frame = 0;
            x = 0f;
            Fgui.GObject surface = context?.sender as Fgui.GObject;
            if (!_enabled || _projection == null || surface == null || context.inputEvent == null)
                return false;
            float rulerWidth = ResolveRulerWidth();
            Vector2 local = _viewport.GlobalToLocal(
                new Vector2(context.inputEvent.x, context.inputEvent.y));
            CharacterTestRuntimeAiLabTimelineViewport viewport = _projection.Viewport;
            frame = ResolveScrubFrame(
                viewport,
                _projection.Domain,
                local.x,
                rulerWidth);
            float position = (frame - viewport.StartFrame)
                / (float)Math.Max(1, viewport.EndFrame - viewport.StartFrame);
            x = Mathf.Clamp(
                RulerX + position * rulerWidth,
                RulerX + 3f,
                RulerX + rulerWidth - 3f);
            return true;
        }

        internal static int ResolveScrubFrame(
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            float localX,
            float rulerWidth)
        {
            float safeWidth = Math.Max(1f, rulerWidth);
            float t = (localX - RulerX) / safeWidth;
            int raw = viewport.StartFrame
                + Mathf.RoundToInt((viewport.EndFrame - viewport.StartFrame) * t);
            return Mathf.Clamp(raw, domain.RetainedStartFrame, domain.SessionEndFrame);
        }

        private float ResolveRulerWidth()
        {
            Fgui.GObject ruler = _viewport?.GetChild("timelineRuler");
            return ruler != null && ruler.width > 0f ? ruler.width : 1f;
        }

        private void ClearPreview()
        {
            _hasPreview = false;
            _previewX = 0f;
            _previewFrameText = string.Empty;
            Fgui.GObject playhead = _viewport?.GetChild("timelinePreviewPlayhead");
            Fgui.GObject badge = _viewport?.GetChild("timelinePreviewFrameBadge");
            Fgui.GObject label = _viewport?.GetChild("timelinePreviewFrameLabel");
            if (playhead != null)
                playhead.visible = false;
            if (badge != null)
                badge.visible = false;
            if (label != null)
                label.visible = false;
        }
    }
}
