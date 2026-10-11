using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;


namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private void RedrawRecipeTimeline()
        {
            if (_recipeLayer == null || _recipeSource == null)
                return;
            if (_recipeGestureSlot == null)
                HideRecipeDragGuideLines();
            _recipeProjection.Rebuild(_recipeSource);
            LocateWholeCardSelection();
            foreach (var label in _wholeTimelineHeaders) if (!label.isDisposed) label.visible = false;
            _recipeVisibleTrackCount = 0;
            _recipeVisibleGridCount = 0;
            _recipeVisibleTimelineEndCount = 0;
            _recipeVisibleRulerLabelCount = 0;
            float width = _recipeLayer.width;
            float viewportHeight = _recipeViewport.height;
            float contentWidth = Math.Max(1f, width - RecipeContentInset - 20f);
            int startFrame = Math.Max(0, _recipeSource.HorizontalStartFrame);
            int endFrame = Math.Max(startFrame + 1, _recipeSource.HorizontalEndFrame);
            var viewport = new BehaviorTimelineViewport(
                startFrame,
                endFrame,
                _recipeSource.ZoomLevel,
                _recipeVerticalOffset,
                _recipeSource.FollowCurrent,
                RecipeFrameSnap);
            var transform = new BehaviorTimelineTransform(viewport, 0f, contentWidth);
            DrawRecipeRuler(transform, startFrame, endFrame, contentWidth);

            float y = -_recipeVerticalOffset;
            float currentTrackTop = -1f;
            float currentTrackBottom = -1f;
            for (int timelineIndex = 0; timelineIndex < _recipeProjection.TimelineCount; timelineIndex++)
            {
                BehaviorRecipeTimelineViewModel timeline = _recipeProjection.GetTimeline(timelineIndex);
                if (ShowTacticalTimelineHeader)
                {
                    string title = _recipeSource.CompositionEditor?.EditingSourceTimeline == true
                        ? _recipeSource.TacticalPreview.FocusBlockId != null
                            ? _recipeSource.TacticalPreview.FocusBlock?.Title + " · 底层事件 · 局部帧"
                            : _recipeSource.TacticalPreview.Source.Name + " · 编辑封装 · 局部帧" : timeline.Title;
                    DrawWholeTimelineHeader(timelineIndex, title, y, contentWidth);
                    y += TacticalTimelineHeaderHeight;
                }
                float timelineTrackTop = y;
                for (int trackIndex = 0; trackIndex < timeline.TrackCount; trackIndex++)
                {
                    BehaviorRecipeTrackViewModel track = timeline.GetTrack(trackIndex);
                    BindRecipeTrack(
                        GetRecipeTrackSlot(_recipeVisibleTrackCount++),
                        timeline,
                        track,
                        timelineTrackTop + track.VerticalOrder * RecipeTrackHeight,
                        width,
                        contentWidth,
                        transform);
                }
                // A Timeline with no Events still owns one blank selectable row. The row is
                // not a persisted Track and contains no special Event; it is only the Timeline
                // background hit area.
                y += Math.Max(1, timeline.LaneCount) * RecipeTrackHeight;
                if (timeline.Current)
                {
                    currentTrackTop = timelineTrackTop;
                    currentTrackBottom = Math.Max(y, timelineTrackTop + 1f);
                }
                DrawRecipeTimelineEnd(
                    GetRecipeTimelineEndGraph(_recipeVisibleTimelineEndCount++),
                    timeline,
                    timelineTrackTop,
                    Math.Max(y, timelineTrackTop + RecipeTrackHeight),
                    transform,
                    contentWidth);
                y += RecipeTimelineGap;
            }
            _recipeContentExtent = Math.Max(viewportHeight, y + _recipeVerticalOffset);
            float maximumVertical = Math.Max(0f, _recipeContentExtent - viewportHeight);
            if (_recipeVerticalOffset > maximumVertical + 0.5f)
            {
                _recipeVerticalOffset = maximumVertical;
                _recipeSource.VerticalScrollOffset = _recipeVerticalOffset;
                RedrawRecipeTimeline();
                return;
            }
            HideUnusedRecipeSlots();
            DrawEventFocusRelations(transform);
            DrawTimingAnchors(transform);
            for (int i = _recipeVisibleTimelineEndCount; i < _recipeTimelineEndPool.Count; i++)
                _recipeTimelineEndPool[i].visible = false;
            DrawRecipePlayhead(transform, currentTrackTop, currentTrackBottom);
            UpdateRecipeScrollbars(startFrame, endFrame);
            _recipeRowsLayer.SetSize(width, Math.Max(viewportHeight, _recipeContentExtent));
        }

        private void BindRecipeTrack(
            RecipeTrackSlot slot,
            BehaviorRecipeTimelineViewModel timeline,
            BehaviorRecipeTrackViewModel track,
            float y,
            float width,
            float contentWidth,
            in BehaviorTimelineTransform transform)
        {
            slot.Timeline = timeline;
            slot.Track = track;
            slot.Event = track.Event;
            slot.Span.data = track.Event;
            slot.MarkerLabel.data = track.Event;
            slot.Visual.visible = y + RecipeTrackHeight >= 0f && y <= _recipeViewport.height;
            slot.Visual.SetXY(0f, y);
            slot.Visual.SetSize(width - 12f, RecipeTrackHeight);
            Fgui.GObject icon = slot.Visual.GetChild("trackIcon");
            if (icon != null)
                icon.visible = false;
            if (slot.Label != null)
            {
                slot.Label.visible = false;
                slot.Label.touchable = false;
            }
            slot.Content.SetXY(RecipeContentInset, 0f);
            slot.Content.SetSize(contentWidth, RecipeTrackHeight);
            if (slot.LaneBackground != null)
                slot.LaneBackground.visible = track.OwnsLaneBackground;
            DrawRecipeRuntimeLaneBackground(
                slot,
                track.OwnsLaneBackground && timeline.Current,
                contentWidth);
            Fgui.GObject unavailable = slot.Visual.GetChild("trackUnavailable");
            if (unavailable != null)
                unavailable.visible = false;
            BindRecipeEvent(slot, timeline, track.Event, contentWidth, transform);
            if (track.OwnsLaneBackground)
                DrawRecipeTrackGrid(y, width);
        }

        private void BindRecipeEvent(
            RecipeTrackSlot slot,
            BehaviorRecipeTimelineViewModel timeline,
            BehaviorRecipeEventViewModel recipeEvent,
            float contentWidth,
            in BehaviorTimelineTransform transform)
        {
            float eventX = transform.FrameToX(recipeEvent.StartFrame);
            float eventEndX = transform.FrameToX(recipeEvent.StartFrame + Math.Max(1, recipeEvent.DurationFrames));
            bool marker = recipeEvent.Primitive == BehaviorRecipeEventPrimitive.Marker;
            float temporalWidth = Math.Max(1f, eventEndX - eventX);
            float eventWidth = marker
                ? 18f
                : recipeEvent.Kind == BehaviorRecipeEventKind.Event
                    ? temporalWidth
                    : Math.Max(
                        recipeEvent.Kind is BehaviorRecipeEventKind.Branch
                        or BehaviorRecipeEventKind.LegacySharedBranch
                            ? recipeEvent.Primitive == BehaviorRecipeEventPrimitive.OpenSpan
                                ? RecipeBranchMinimumSpanWidth
                                : RecipeMinimumSpanWidth
                            : recipeEvent.Kind == BehaviorRecipeEventKind.PrimaryAction
                                ? RecipePrimaryActionMinimumSpanWidth
                            : recipeEvent.Primitive == BehaviorRecipeEventPrimitive.OpenSpan
                                ? RecipeOpenSpanMinimumWidth
                            : RecipeMinimumSpanWidth,
                        temporalWidth);
            eventWidth = Math.Min(Math.Max(18f, contentWidth - eventX), eventWidth);
            slot.Marker.visible = marker;
            slot.MarkerCardBackground.visible = marker;
            slot.MarkerLabel.visible = marker;
            slot.Span.visible = !marker;
            slot.SpanHitArea.visible = !marker;
            slot.ProgressFill.visible = !marker
                && recipeEvent.Primitive != BehaviorRecipeEventPrimitive.OpenSpan
                && recipeEvent.Running
                && recipeEvent.ProgressFrames >= 0;
            slot.ResizeHandle.visible = !marker && recipeEvent.CanResize && _canEdit;
            BindTimingStartHandle(slot, recipeEvent, eventX);
            slot.ResultBadge.visible = !marker && recipeEvent.Kind is BehaviorRecipeEventKind.Branch
                or BehaviorRecipeEventKind.LegacySharedBranch;
            slot.ResultBadgeBackground.visible = slot.ResultBadge.visible;
            if (marker)
            {
                slot.Marker.SetXY(eventX, RecipeEventY);
                slot.Marker.SetSize(2f, RecipeEventHeight);
                DrawRecipeMarker(slot.Marker, recipeEvent);
                float cardWidth = Math.Min(
                    RecipeInstantCardWidth,
                    Math.Max(72f, contentWidth - 8f));
                float cardX = eventX + 8f + cardWidth <= contentWidth
                    ? eventX + 8f
                    : Math.Max(0f, eventX - cardWidth - 8f);
                slot.MarkerCardBackground.SetXY(cardX, RecipeEventY);
                slot.MarkerCardBackground.SetSize(cardWidth, RecipeEventHeight);
                slot.MarkerCardBackground.DrawRect(
                    cardWidth,
                    RecipeEventHeight,
                    recipeEvent.Selected ? 2 : 1,
                    recipeEvent.Invalid
                        ? new Color32(244, 79, 88, 255)
                        : recipeEvent.Selected
                            ? new Color32(244, 249, 252, 255)
                            : recipeEvent.SourceHighlighted ? new Color32(66, 217, 188, 255) : RecipeEventAccent(recipeEvent),
                    recipeEvent.TacticalOperationKind == "WaitForCondition"
                        ? new Color32(58, 43, 15, 235)
                        : new Color32(32, 18, 46, recipeEvent.Running ? (byte)240 : (byte)215));
                slot.MarkerLabel.SetXY(cardX + 10f, RecipeEventY + 1f);
                slot.MarkerLabel.SetSize(Math.Max(52f, cardWidth - 20f), RecipeEventHeight - 2f);
                slot.MarkerLabel.singleLine = true;
                slot.MarkerLabel.autoSize = Fgui.AutoSizeType.None;
                slot.MarkerLabel.text = "⚡ " + recipeEvent.Title;
                slot.MarkerLabel.color = recipeEvent.Invalid
                    ? new Color32(255, 154, 162, 255)
                    : recipeEvent.TacticalOperationKind == "WaitForCondition"
                        ? RecipeEventAccent(recipeEvent) : new Color32(240, 235, 252, 255);
            }
            else
            {
                slot.Span.SetXY(eventX, RecipeEventY);
                slot.Span.SetSize(eventWidth, RecipeEventHeight);
                DrawRecipeSpan(slot.Span, recipeEvent, eventWidth);
                PositionRecipeSpanHitArea(slot, eventX, eventWidth);
                Fgui.GTextField spanLabel = slot.Span.GetChild("spanLabel")?.asTextField;
                if (spanLabel != null)
                {
                    spanLabel.SetXY(12f, 2f);
                    spanLabel.SetSize(Math.Max(24f, eventWidth - 24f), RecipeEventHeight - 4f);
                    bool nativeShortSpan = _recipeSource.InspectCompiledExecution && eventWidth < 80f;
                    spanLabel.touchable = nativeShortSpan;
                    if (nativeShortSpan) spanLabel.SetSize(Math.Max(24f, Math.Min(220f, contentWidth - eventX - 16f)), RecipeEventHeight - 4f);
                    spanLabel.text = recipeEvent.Title
                        + (recipeEvent.Primitive == BehaviorRecipeEventPrimitive.OpenSpan ? "  ∞" : string.Empty);
                    spanLabel.color = recipeEvent.Invalid ? new Color32(255, 154, 162, 255) : new Color32(240, 235, 252, 255);
                }
                if (slot.ProgressFill.visible)
                {
                    float progress = recipeEvent.DurationFrames > 0
                        ? Mathf.Clamp01(recipeEvent.ProgressFrames / (float)recipeEvent.DurationFrames)
                        : 0f;
                    slot.ProgressFill.SetXY(eventX + 2f, RecipeEventY + 2f);
                    slot.ProgressFill.DrawRect(
                        Math.Max(0f, (eventWidth - 4f) * progress),
                        RecipeEventHeight - 4f,
                        0,
                        Color.clear,
                        new Color32(76, 235, 221, 60));
                }
                slot.ResizeHandle.SetXY(
                    ResolveRecipeResizeHandleX(
                        eventX,
                        eventWidth,
                        contentWidth,
                        RecipeResizeHandleWidth),
                    RecipeEventY);
                slot.ResizeHandle.DrawRect(
                    RecipeResizeHandleWidth,
                    RecipeEventHeight,
                    0,
                    Color.clear,
                    new Color32(220, 244, 250, 75));
                if (slot.ResultBadge.visible)
                {
                    float badgeWidth = Math.Min(116f, Math.Max(84f, eventWidth * 0.32f));
                    float badgeX = eventX + eventWidth - badgeWidth - 8f;
                    float badgeY = RecipeEventY + 6f;
                    float badgeHeight = RecipeEventHeight - 12f;
                    Color32 resultColor = ResultColor(recipeEvent.ResultLabel);
                    slot.ResultBadgeBackground.SetXY(badgeX, badgeY);
                    slot.ResultBadgeBackground.DrawRect(
                        badgeWidth,
                        badgeHeight,
                        1,
                        resultColor,
                        new Color32((byte)(resultColor.r * 0.22f), (byte)(resultColor.g * 0.22f), (byte)(resultColor.b * 0.22f), 195));
                    slot.ResultBadge.SetXY(badgeX + 6f, badgeY);
                    slot.ResultBadge.SetSize(badgeWidth - 12f, badgeHeight);
                    slot.ResultBadge.text = recipeEvent.ResultLabel;
                    slot.ResultBadge.color = resultColor;
                    if (spanLabel != null)
                    {
                        spanLabel.text = recipeEvent.ConditionLabel + "  →";
                        spanLabel.SetSize(
                            Math.Max(40f, badgeX - eventX - 18f),
                            RecipeEventHeight - 4f);
                    }
                }
            }
            string tooltip = recipeEvent.Title + "\n" + recipeEvent.Detail;
            BindTooltip(slot.Span, tooltip);
            BindTooltip(slot.SpanHitArea, tooltip);
            BindTooltip(slot.Marker, tooltip);
            BindTooltip(slot.MarkerCardBackground, tooltip);
            BindTooltip(slot.MarkerLabel, tooltip);
        }

        private void DrawRecipeRuler(
            in BehaviorTimelineTransform transform,
            int startFrame,
            int endFrame,
            float contentWidth)
        {
            bool unresolvedSource = _recipeSource?.TacticalPreview != null && !_recipeSource.TacticalPreview.HasFramePositions;
            _recipeRulerTitle.visible = false;
            if (unresolvedSource)
            {
                foreach (var label in _recipeRulerLabelPool) label.visible = false;
                var caption = GetRecipeRulerLabel(_recipeVisibleRulerLabelCount++);
                caption.SetXY(16f, 4f);
                caption.SetSize(Math.Max(100f, contentWidth - 32f), 30f);
                caption.align = Fgui.AlignType.Left;
                string diagnostic = _recipeSource.TacticalPreview?.WholeCard != null
                    ? "整卡编译不可用：" + _recipeSource.TacticalPreview.WholeCard.Diagnostic : "源事件未求值 · 条目不表示执行时间";
                int actionId = _recipeSource.TacticalPreview.SummaryRequest?.ActionId ?? 0;
                caption.text = diagnostic.Contains("ActionSummary:Missing")
                    ? "动作 " + actionId + " 缺少测量摘要；点击主攻击槽菜单 → 准备当前所选动作摘要。"
                    : diagnostic.Contains("ActionSummary:Stale")
                        ? "动作 " + actionId + " 的测量摘要已失效；请在主攻击槽菜单中重新准备摘要。" : diagnostic;
                caption.tooltips = diagnostic;
                return;
            }
            int span = Math.Max(1, endFrame - startFrame);
            int step = ResolveRecipeRulerStep(span);
            int first = ((startFrame + step - 1) / step) * step;
            float viewportHeight = _recipeViewport?.height ?? 300f;
            for (int frame = first; frame <= endFrame; frame += step)
            {
                float localX = transform.FrameToX(frame);
                if (localX >= 0f && localX <= contentWidth + 4f)
                {
                    // Vertical guide grid line spanning through tracks
                    Fgui.GGraph grid = GetRecipeGridLine(_recipeVisibleGridCount++);
                    grid.SetXY(RecipeContentInset + localX, RecipeHeaderHeight + RecipeRulerHeight);
                    grid.DrawRect(1f, viewportHeight, 0, Color.clear, new Color32(49, 91, 110, 120));

                    // Ruler tick mark
                    Fgui.GGraph tick = GetRecipeGridLine(_recipeVisibleGridCount++);
                    tick.SetXY(RecipeContentInset + localX, RecipeHeaderHeight + RecipeRulerHeight - 8f);
                    tick.DrawRect(1f, 8f, 0, Color.clear, new Color32(65, 128, 138, 140));

                    // Frame label
                    Fgui.GTextField label = GetRecipeRulerLabel(_recipeVisibleRulerLabelCount++);
                    label.SetXY(RecipeContentInset + localX - 28f, RecipeHeaderHeight + 3f);
                    label.SetSize(56f, RecipeRulerHeight - 10f);
                    label.align = Fgui.AlignType.Center;
                    label.text = frame.ToString(CultureInfo.InvariantCulture) + "f";
                }

                // Minor half-step tick and subtle grid
                if (step >= 10 && frame + step / 2 <= endFrame)
                {
                    float halfX = transform.FrameToX(frame + step / 2);
                    if (halfX >= 0f && halfX <= contentWidth + 4f)
                    {
                        Fgui.GGraph halfGrid = GetRecipeGridLine(_recipeVisibleGridCount++);
                        halfGrid.SetXY(RecipeContentInset + halfX, RecipeHeaderHeight + RecipeRulerHeight);
                        halfGrid.DrawRect(1f, viewportHeight, 0, Color.clear, new Color32(34, 64, 79, 75));

                        Fgui.GGraph halfTick = GetRecipeGridLine(_recipeVisibleGridCount++);
                        halfTick.SetXY(RecipeContentInset + halfX, RecipeHeaderHeight + RecipeRulerHeight - 4f);
                        halfTick.DrawRect(1f, 4f, 0, Color.clear, new Color32(40, 82, 90, 80));
                    }
                }
            }
            for (int i = _recipeVisibleRulerLabelCount; i < _recipeRulerLabelPool.Count; i++)
                _recipeRulerLabelPool[i].visible = false;
        }

        private void DrawRecipeTrackGrid(float y, float width)
        {
            Fgui.GGraph separator = GetRecipeGridLine(_recipeVisibleGridCount++);
            separator.SetXY(RecipeContentInset, y + RecipeTrackHeight - 1f);
            separator.DrawRect(Math.Max(1f, width - RecipeContentInset - 20f), 1f, 0, Color.clear,
                new Color32(16, 38, 46, 40));
        }

        private Fgui.GGraph GetRecipeTimelineEndGraph(int index)
        {
            while (_recipeTimelineEndPool.Count <= index)
            {
                Fgui.GGraph graph = NewRecipeGraph("recipeTimelineEnd" + _recipeTimelineEndPool.Count, false);
                graph.sortingOrder = 420;
                BindTooltip(graph, "时间轴末端（非事件）：未发生跳转时，由执行器自然结束当前卡。");
                _recipeViewport.AddChild(graph);
                _recipeTimelineEndPool.Add(graph);
            }
            return _recipeTimelineEndPool[index];
        }

        private void DrawRecipeTimelineEnd(
            Fgui.GGraph graph,
            BehaviorRecipeTimelineViewModel timeline,
            float top,
            float bottom,
            in BehaviorTimelineTransform transform,
            float contentWidth)
        {
            if (graph == null)
                return;
            if (timeline == null || timeline.DurationFrames <= 0)
            {
                graph.visible = false;
                return;
            }
            if (_recipeSource?.TacticalPreview != null && !_recipeSource.TacticalPreview.HasFramePositions)
            { graph.visible = false; return; }
            float localX = transform.FrameToX(timeline.DurationFrames);
            bool visible = localX >= 0f && localX <= contentWidth && bottom >= 0f;
            graph.visible = visible;
            if (!visible)
                return;
            graph.SetXY(RecipeContentInset + localX, Math.Max(0f, top));
            graph.DrawRect(1f, Math.Max(1f, bottom - Math.Max(0f, top)), 0, Color.clear,
                new Color32(238, 174, 74, 210));
        }

        private void DrawRecipePlayhead(
            in BehaviorTimelineTransform transform,
            float currentTrackTop,
            float currentTrackBottom)
        {
            int frame = _recipeProjection.LocalPlayheadFrame;
            int visibleStart = Math.Max(0, _recipeSource?.HorizontalStartFrame ?? 0);
            int visibleEnd = Math.Max(visibleStart + 1, _recipeSource?.HorizontalEndFrame ?? 1);
            float clippedTop = Math.Max(0f, currentTrackTop);
            float clippedBottom = Math.Min(_recipeViewport.height, currentTrackBottom);
            bool visible = frame >= visibleStart
                && frame <= visibleEnd
                && clippedBottom > clippedTop;
            _recipePlayhead.visible = visible;
            _recipePlayheadHead.visible = visible;
            _recipePlayheadLabel.visible = visible;
            if (!visible)
                return;

            float x = RecipeContentInset + transform.FrameToX(frame);
            _recipePlayhead.SetXY(x, clippedTop);
            _recipePlayhead.DrawRect(2f, clippedBottom - clippedTop, 0, Color.clear,
                new Color32(83, 229, 222, 235));
            _recipePlayheadHeadPoints[0] = new Vector2(0f, 0f);
            _recipePlayheadHeadPoints[1] = new Vector2(10f, 0f);
            _recipePlayheadHeadPoints[2] = new Vector2(5f, 7f);
            _recipePlayheadHead.SetXY(x - 4f, RecipeHeaderHeight + RecipeRulerHeight - 8f);
            _recipePlayheadHead.DrawPolygon(
                10f,
                7f,
                _recipePlayheadHeadPoints,
                new Color32(83, 229, 222, 255));
            float labelX = Mathf.Clamp(x - 26f, RecipeContentInset, _recipeLayer.width - 62f);
            _recipePlayheadLabel.SetXY(labelX, RecipeHeaderHeight + 1f);
            _recipePlayheadLabel.SetSize(52f, RecipeRulerHeight - 10f);
            _recipePlayheadLabel.text = ResolveRecipePlayheadFrameText(frame);
        }

        internal void RefreshRecipeRuntimeOverlay(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel)
        {
            if (!_behaviorLogicMode
                || _recipeLayer?.visible != true
                || viewModel == null
                || viewModel.RecipePayload == null
                || !CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(
                    viewModel.RecipePayload))
            {
                return;
            }

            _recipeSource = viewModel;
            int previousTimelineIndex = _recipeProjection.CurrentTimelineIndex;
            _recipeProjection.UpdateRuntimeOverlay(viewModel);
            bool viewportChanged = viewModel.FollowCurrent && ApplyRecipeAutoFollow();
            bool timelineChanged = previousTimelineIndex != _recipeProjection.CurrentTimelineIndex;
            if (viewportChanged || timelineChanged)
            {
                RedrawRecipeTimeline();
            }
            else
            {
                RefreshRecipeTrackRuntimeVisuals();
                DrawRecipePlayheadForCurrentViewport();
            }
        }

        private void RefreshRecipeTrackRuntimeVisuals()
        {
            int count = Math.Min(_recipeVisibleTrackCount, _recipeTrackPool.Count);
            for (int i = 0; i < count; i++)
            {
                RecipeTrackSlot slot = _recipeTrackPool[i];
                BehaviorRecipeEventViewModel recipeEvent = slot.Event;
                if (slot.Visual?.visible != true || recipeEvent == null)
                    continue;
                DrawRecipeRuntimeLaneBackground(
                    slot,
                    slot.Track?.OwnsLaneBackground == true
                        && slot.Timeline?.Current == true,
                    slot.Content.width);
                if (recipeEvent.Primitive == BehaviorRecipeEventPrimitive.Marker)
                {
                    DrawRecipeMarker(slot.Marker, recipeEvent);
                    slot.ProgressFill.visible = false;
                    continue;
                }

                DrawRecipeSpan(slot.Span, recipeEvent, slot.Span.width);
                bool showProgress = recipeEvent.Running
                    && recipeEvent.Primitive != BehaviorRecipeEventPrimitive.OpenSpan
                    && recipeEvent.ProgressFrames >= 0;
                slot.ProgressFill.visible = showProgress;
                if (!showProgress)
                    continue;
                float progress = recipeEvent.DurationFrames > 0
                    ? Mathf.Clamp01(recipeEvent.ProgressFrames / (float)recipeEvent.DurationFrames)
                    : 0f;
                slot.ProgressFill.SetXY(slot.Span.x + 2f, RecipeEventY + 2f);
                slot.ProgressFill.DrawRect(
                    Math.Max(0f, (slot.Span.width - 4f) * progress),
                    RecipeEventHeight - 4f,
                    0,
                    Color.clear,
                    new Color32(76, 235, 221, 60));
            }
        }

        private void DrawRecipePlayheadForCurrentViewport()
        {
            float contentWidth = Math.Max(1f, _recipeLayer.width - RecipeContentInset - 20f);
            int startFrame = Math.Max(0, _recipeSource.HorizontalStartFrame);
            int endFrame = Math.Max(startFrame + 1, _recipeSource.HorizontalEndFrame);
            var viewport = new BehaviorTimelineViewport(
                startFrame,
                endFrame,
                _recipeSource.ZoomLevel,
                _recipeVerticalOffset,
                _recipeSource.FollowCurrent,
                RecipeFrameSnap);
            var transform = new BehaviorTimelineTransform(viewport, 0f, contentWidth);
            ResolveCurrentRecipeTimelineBounds(out float top, out float bottom);
            DrawRecipePlayhead(transform, top, bottom);
        }

        private bool ApplyRecipeAutoFollow()
        {
            if (_recipeSource == null || _recipeProjection.LocalPlayheadFrame < 0)
                return false;
            bool changed = false;
            int start = Math.Max(0, _recipeSource.HorizontalStartFrame);
            int end = Math.Max(start + 1, _recipeSource.HorizontalEndFrame);
            int span = end - start;
            int frame = _recipeProjection.LocalPlayheadFrame;
            int nextStart = ResolveRecipeAutoFollowStart(start, end, frame);
            if (nextStart != start)
            {
                _recipeSource.HorizontalStartFrame = nextStart;
                _recipeSource.HorizontalEndFrame = nextStart + span;
                changed = true;
            }

            if (TryResolveCurrentRecipeTimelineContentBounds(out float contentTop, out float contentBottom))
            {
                float nextOffset = _recipeVerticalOffset;
                if (contentTop < nextOffset)
                    nextOffset = contentTop;
                else if (contentBottom > nextOffset + _recipeViewport.height)
                    nextOffset = contentBottom - _recipeViewport.height;
                float maximum = Math.Max(0f, _recipeContentExtent - _recipeViewport.height);
                nextOffset = Mathf.Clamp(nextOffset, 0f, maximum);
                if (!Mathf.Approximately(nextOffset, _recipeVerticalOffset))
                {
                    _recipeVerticalOffset = nextOffset;
                    _recipeSource.VerticalScrollOffset = nextOffset;
                    changed = true;
                }
            }
            if (changed)
                CommitRecipeViewport();
            return changed;
        }

        internal static int ResolveRecipeAutoFollowStart(
            int startFrame,
            int endFrame,
            int localFrame)
        {
            int start = Math.Max(0, startFrame);
            int end = Math.Max(start + 1, endFrame);
            int span = end - start;
            int threshold = start + Math.Max(1, Mathf.FloorToInt(span * RecipeFollowThreshold));
            return localFrame < start || localFrame >= threshold
                ? Math.Max(0, localFrame - Mathf.FloorToInt(span * RecipeFollowTarget))
                : start;
        }

        private void ResolveCurrentRecipeTimelineBounds(out float top, out float bottom)
        {
            if (!TryResolveCurrentRecipeTimelineContentBounds(out float contentTop, out float contentBottom))
            {
                top = -1f;
                bottom = -1f;
                return;
            }
            top = contentTop - _recipeVerticalOffset;
            bottom = contentBottom - _recipeVerticalOffset;
        }

        private bool TryResolveCurrentRecipeTimelineContentBounds(out float top, out float bottom)
        {
            float y = 0f;
            for (int i = 0; i < _recipeProjection.TimelineCount; i++)
            {
                BehaviorRecipeTimelineViewModel timeline = _recipeProjection.GetTimeline(i);
                float height = Math.Max(1, timeline.LaneCount) * RecipeTrackHeight;
                if (timeline.Current)
                {
                    top = y;
                    bottom = y + height;
                    return true;
                }
                y += height + RecipeTimelineGap;
            }
            top = -1f;
            bottom = -1f;
            return false;
        }

        private static void DrawRecipeRuntimeLaneBackground(
            RecipeTrackSlot slot,
            bool current,
            float width)
        {
            slot.RuntimeLaneBackground.visible = current;
            if (!current)
                return;
            slot.RuntimeLaneBackground.SetXY(0f, 0f);
            slot.RuntimeLaneBackground.DrawRect(
                Math.Max(1f, width),
                RecipeTrackHeight,
                1,
                new Color32(45, 140, 145, 25),
                new Color32(24, 85, 90, 10));
        }

        private void UpdateRecipeScrollbars(int startFrame, int endFrame)
        {
            float horizontalWidth = Math.Max(1f, _recipeLayer.width - RecipeContentInset - 18f);
            int visibleFrames = Math.Max(1, endFrame - startFrame);
            int totalFrames = Math.Max(visibleFrames, _recipeProjection.MaximumLocalFrame + 30);
            int maximumStart = Math.Max(0, totalFrames - visibleFrames);
            float thumbWidth = maximumStart > 0
                ? Math.Max(100f, horizontalWidth * visibleFrames / totalFrames)
                : horizontalWidth;
            float normalized = maximumStart > 0 ? Mathf.Clamp01(startFrame / (float)maximumStart) : 0f;
            _recipeHorizontalScrollThumb.SetXY(
                RecipeContentInset + (horizontalWidth - thumbWidth) * normalized,
                _recipeLayer.height - RecipeScrollbarThickness - 2f);
            _recipeHorizontalScrollThumb.DrawRect(thumbWidth, RecipeScrollbarThickness, 0, Color.clear,
                new Color32(60, 184, 181, 245));
            _recipeHorizontalScrollThumb.draggable = maximumStart > 0;

            float viewportHeight = _recipeViewport.height;
            float maximumVertical = Math.Max(0f, _recipeContentExtent - viewportHeight);
            float thumbHeight = maximumVertical > 0f
                ? Math.Max(42f, viewportHeight * viewportHeight / _recipeContentExtent)
                : viewportHeight;
            float verticalNormalized = maximumVertical > 0f
                ? Mathf.Clamp01(_recipeVerticalOffset / maximumVertical)
                : 0f;
            _recipeVerticalScrollThumb.SetXY(
                _recipeLayer.width - RecipeScrollbarThickness - 2f,
                RecipeHeaderHeight + RecipeRulerHeight
                    + (viewportHeight - thumbHeight) * verticalNormalized);
            _recipeVerticalScrollThumb.DrawRect(RecipeScrollbarThickness, thumbHeight, 0, Color.clear,
                new Color32(60, 184, 181, 245));
            _recipeVerticalScrollThumb.draggable = maximumVertical > 0f;
        }

        private RecipeTrackSlot GetRecipeTrackSlot(int index)
        {
            while (_recipeTrackPool.Count <= index)
            {
                var slot = new RecipeTrackSlot();
                slot.Visual = CreateRequiredComponent(RecipeTrackComponentName);
                slot.Visual.name = "recipeTrack" + _recipeTrackPool.Count;
                slot.Label = slot.Visual.GetChild("trackLabel")?.asTextField;
                if (slot.Label == null)
                    throw new InvalidOperationException("RuntimeAiLabTimelineTrack is missing trackLabel.");
                slot.Content = slot.Visual.GetChild("trackContent")?.asCom;
                if (slot.Content == null)
                    throw new InvalidOperationException("RuntimeAiLabTimelineTrack is missing trackContent.");
                slot.LaneBackground = slot.Content.GetChild("trackBackground");
                slot.Span = CreateRequiredComponent(RecipeSpanComponentName);
                slot.Span.name = "eventSpan";
                slot.SpanHitArea = new Fgui.GGraph
                {
                    name = "eventSpanHitArea",
                    touchable = true
                };
                slot.Marker = CreateRequiredComponent(RecipeMarkerComponentName);
                slot.Marker.name = "eventMarker";
                slot.MarkerCardBackground = new Fgui.GGraph
                {
                    name = "instantEventCard",
                    touchable = true
                };
                slot.MarkerLabel = NewRecipeText("markerLabel", string.Empty, 15,
                    new Color32(104, 221, 214, 255), Fgui.AlignType.Left, true);
                slot.ProgressFill = new Fgui.GGraph { name = "progressFill", touchable = false };
                slot.ResizeHandle = new Fgui.GGraph { name = "resizeHandle", touchable = true };
                slot.ResultBadgeBackground = new Fgui.GGraph
                {
                    name = "resultBadgeBackground",
                    touchable = false
                };
                slot.ResultBadge = NewRecipeText("resultBadge", string.Empty, 14,
                    new Color32(244, 168, 75, 255), Fgui.AlignType.Center, true);
                slot.RuntimeLaneBackground = new Fgui.GGraph
                {
                    name = "runtimeLaneBackground",
                    touchable = false
                };
                slot.Content.AddChild(slot.RuntimeLaneBackground);
                slot.Content.AddChild(slot.ProgressFill);
                slot.Content.AddChild(slot.SpanHitArea);
                slot.Content.AddChild(slot.Span);
                slot.Content.AddChild(slot.Marker);
                slot.Content.AddChild(slot.MarkerCardBackground);
                slot.Content.AddChild(slot.MarkerLabel);
                slot.Content.AddChild(slot.ResultBadgeBackground);
                slot.Content.AddChild(slot.ResultBadge);
                slot.Content.AddChild(slot.ResizeHandle);
                ConfigureRecipeTrackInteractions(slot);
                _recipeTrackPool.Add(slot);
                _recipeRowsLayer.AddChild(slot.Visual);
            }
            RecipeTrackSlot result = _recipeTrackPool[index];
            result.Visual.visible = true;
            return result;
        }

        private void ConfigureRecipeTrackInteractions(RecipeTrackSlot slot)
        {
            void Select(Fgui.EventContext context)
            {
                SelectRecipeEvent(slot.Event, context?.inputEvent?.ctrlOrCmd == true);
                context?.StopPropagation();
            }
            slot.Span.onClick.Set(Select);
            slot.SpanHitArea.onClick.Set(Select);
            slot.Marker.onClick.Set(Select);
            slot.MarkerCardBackground.onClick.Set(Select);
            slot.MarkerLabel.onClick.Set(Select);
            slot.Label.touchable = false;
            slot.ResultBadge.onClick.Set(context =>
            {
                if (!ScrollToRecipeTarget(slot.Event))
                    SelectRecipeEvent(slot.Event);
                context.StopPropagation();
            });
            void Context(Fgui.EventContext context)
            {
                if (slot.Event?.Kind == BehaviorRecipeEventKind.PrimaryAction)
                    ShowRecipeTimelineContextMenu(FindRecipeTimeline(slot.Event.StageId));
                else
                    ShowRecipeEventContextMenu(slot.Event);
                context.StopPropagation();
            }
            slot.Span.onRightClick.Set(Context);
            slot.SpanHitArea.onRightClick.Set(Context);
            slot.Marker.onRightClick.Set(Context);
            slot.MarkerCardBackground.onRightClick.Set(Context);
            slot.MarkerLabel.onRightClick.Set(Context);
            slot.Content.onRightClick.Set(context =>
            {
                int frame = ResolveRecipePointerFrame(slot.Content);
                ShowRecipeFrameContextMenu(slot.Event?.StageId, frame);
                context.StopPropagation();
            });
            slot.Content.onClick.Set(context =>
            {
                SelectRecipeTimelineConditions(FindRecipeTimeline(slot.Event?.StageId));
                context.StopPropagation();
            });
            slot.Span.onTouchBegin.Set(context => BeginRecipeGesture(slot, resize: false, context));
            slot.Span.onTouchMove.Set(UpdateRecipeGesture);
            slot.Span.onTouchEnd.Set(EndRecipeGesture);
            slot.SpanHitArea.onTouchBegin.Set(context => BeginRecipeGesture(slot, resize: false, context));
            slot.SpanHitArea.onTouchMove.Set(UpdateRecipeGesture);
            slot.SpanHitArea.onTouchEnd.Set(EndRecipeGesture);
            slot.Marker.onTouchBegin.Set(context => BeginRecipeGesture(slot, resize: false, context));
            slot.Marker.onTouchMove.Set(UpdateRecipeGesture);
            slot.Marker.onTouchEnd.Set(EndRecipeGesture);
            slot.MarkerCardBackground.onTouchBegin.Set(context => BeginRecipeGesture(slot, resize: false, context));
            slot.MarkerCardBackground.onTouchMove.Set(UpdateRecipeGesture);
            slot.MarkerCardBackground.onTouchEnd.Set(EndRecipeGesture);
            slot.MarkerLabel.onTouchBegin.Set(context => BeginRecipeGesture(slot, resize: false, context));
            slot.MarkerLabel.onTouchMove.Set(UpdateRecipeGesture);
            slot.MarkerLabel.onTouchEnd.Set(EndRecipeGesture);
            slot.ResultBadge.onTouchBegin.Set(context => BeginRecipeGesture(slot, resize: false, context));
            slot.ResultBadge.onTouchMove.Set(UpdateRecipeGesture);
            slot.ResultBadge.onTouchEnd.Set(EndRecipeGesture);
            slot.ResizeHandle.onTouchBegin.Set(context => BeginRecipeGesture(slot, resize: true, context));
            slot.ResizeHandle.onTouchMove.Set(UpdateRecipeGesture);
            slot.ResizeHandle.onTouchEnd.Set(EndRecipeGesture);
        }

    }
}
