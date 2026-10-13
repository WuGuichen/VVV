using System;
using System.Collections.Generic;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using MxFramework.Editor.CharacterAction.Timeline.Manipulators;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed partial class CharacterActionTimelineView
    {
        private void RebuildLabelColumn()
        {
            if (_labelColumn == null)
                return;

            _labelColumn.Clear();
            _labelRows.Clear();
            _labelColumn.Add(BuildLabelRow("\u6807\u5c3a", true, rowIndex: 0));
            if (_snapshot == null)
                return;

            for (int i = 0; i < _snapshot.Tracks.Count; i++)
            {
                TimelineTrackDescriptor track = _snapshot.Tracks[i];
                VisualElement row = BuildLabelRow(track.Title, false, track, rowIndex: i + 1);
                _labelRows[track.Kind] = row;
                _labelColumn.Add(row);
            }
        }

        private void RebuildTrackLayouts()
        {
            _trackLayouts.Clear();
            if (_snapshot == null)
                return;

            for (int i = 0; i < _snapshot.Tracks.Count; i++)
            {
                TimelineTrackDescriptor track = _snapshot.Tracks[i];
                IReadOnlyList<ITimelineItemAdapter> items =
                    _snapshot.ItemsByTrack.TryGetValue(track.Kind, out IReadOnlyList<ITimelineItemAdapter> found)
                        ? found
                        : Array.Empty<ITimelineItemAdapter>();
                _trackLayouts[track.Kind] = CharacterActionTimelineLaneLayout.Build(
                    track.Kind,
                    track.IsEnabled ? items : Array.Empty<ITimelineItemAdapter>(),
                    _state.PixelsPerFrame);
            }
        }

        private void RebuildCanvas(bool preserveScroll = false, bool updateViewport = true)
        {
            if (_canvasRoot == null || _snapshot == null)
                return;

            float savedScrollX = _scrollView != null ? _scrollView.scrollOffset.x : 0f;
            RebuildTrackLayouts();
            RebuildLabelColumn();
            _canvasRoot.Clear();
            _itemElements.Clear();
            _rowCanvases.Clear();

            float contentWidth = CharacterActionTimelineLayout.ContentWidth(
                _snapshot.DurationFrames,
                _state.PixelsPerFrame);

            _canvasRoot.style.width = contentWidth;
            _canvasRoot.style.minWidth = contentWidth;

            if (_snapshot.ReadOnly)
                UpdateReadOnlyBanner();

            _canvasRoot.Add(BuildRulerRow(contentWidth));
            for (int i = 0; i < _snapshot.Tracks.Count; i++)
            {
                TimelineTrackDescriptor track = _snapshot.Tracks[i];
                _canvasRoot.Add(BuildTrackRow(track, contentWidth, rowIndex: i + 1));
            }

            BuildSelectionFrameGuideVisual();
            _canvasRoot.Add(_selectionFrameGuide);

            BuildPlayheadVisual();
            _canvasRoot.Add(_playhead);
            _canvasRoot.Add(_playheadHandle);

            UpdatePlayhead();
            RefreshSelectionHighlight();
            RefreshPlayheadHighlight();
            if (!updateViewport)
                return;

            if (preserveScroll && _scrollView != null)
            {
                _scrollView.schedule.Execute(() =>
                {
                    SetScrollX(savedScrollX);
                    UpdateViewportControl();
                });
            }
            else
            {
                UpdateViewportControl();
            }
        }

        private VisualElement BuildRulerRow(float contentWidth)
        {
            var row = new VisualElement();
            row.AddToClassList("ca-tl-canvas-row");
            row.AddToClassList("ca-tl-ruler");
            row.style.width = contentWidth;
            row.style.height = CharacterActionTimelineLayout.RulerHeight;
            AppendFrameGridLines(row);
            AppendActionEndLine(row, showLabel: true);

            int duration = _snapshot.DurationFrames;
            int step = ResolveGridStep(duration);
            for (int frame = 0; frame < duration; frame += step)
            {
                var tick = new Label(frame.ToString());
                tick.AddToClassList("ca-tl-ruler-tick");
                float x = FrameStartX(frame);
                tick.style.left = x;
                row.Add(tick);
            }

            AttachRowContextMenu(row, null, isRuler: true);
            RegisterRowScrubClick(row);
            return row;
        }

        private VisualElement BuildTrackRow(TimelineTrackDescriptor track, float contentWidth, int rowIndex)
        {
            var row = new VisualElement();
            row.AddToClassList("ca-tl-canvas-row");
            row.style.width = contentWidth;
            row.style.height = ResolveTrackRowHeight(track);
            ApplyRowStripe(row, rowIndex);
            if (!track.IsEnabled)
                row.AddToClassList("ca-tl-row-disabled");

            AppendFrameGridLines(row);
            AppendActionEndLine(row, showLabel: false);

            _rowCanvases[track.Kind] = row;
            AttachRowContextMenu(row, track);

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2)
                    OnRowDoubleClick(track.Kind, evt);
            });
            RegisterRowScrubClick(row);

            if (track.Kind == TimelineTrackKind.MovementControl || track.Kind == TimelineTrackKind.RotationControl)
                BuildControlScaleBand(row, track.Kind == TimelineTrackKind.RotationControl);

            if (!track.IsEnabled || !_snapshot.ItemsByTrack.TryGetValue(track.Kind, out IReadOnlyList<ITimelineItemAdapter> items))
                return row;

            for (int i = 0; i < items.Count; i++)
            {
                ITimelineItemAdapter adapter = items[i];
                VisualElement itemElement = adapter is AnimationTrackTimelineAdapter animationAdapter
                    ? BuildAnimationClipElement(animationAdapter)
                    : adapter.ItemKind == TimelineItemKind.Clip
                        ? BuildClipElement(adapter)
                        : BuildMarkerElement(adapter);
                row.Add(itemElement);
                _itemElements[adapter.Id] = itemElement;
                AttachItemContextMenu(itemElement, adapter);
            }

            return row;
        }

        private VisualElement BuildAnimationClipElement(AnimationTrackTimelineAdapter adapter)
        {
            var clip = new VisualElement();
            clip.AddToClassList("ca-tl-clip");
            clip.AddToClassList("ca-tl-anim-clip");
            clip.tooltip = adapter.Title;
            ApplyItemGeometry(clip, adapter);
            clip.style.backgroundColor = adapter.Color;

            var transition = new VisualElement();
            transition.AddToClassList("ca-tl-anim-transition");
            clip.Add(transition);
            ApplyAnimationTransitionGeometry(transition, adapter);

            var label = new Label(adapter.Title);
            label.AddToClassList("ca-tl-clip-label");
            clip.Add(label);

            var moveZone = new VisualElement();
            moveZone.AddToClassList("ca-tl-clip-move-zone");
            clip.Add(moveZone);

            var leftHandle = new VisualElement();
            leftHandle.AddToClassList("ca-tl-clip-handle");
            leftHandle.AddToClassList("ca-tl-clip-handle-left");
            leftHandle.Add(BuildClipHandleGrip());
            clip.Add(leftHandle);

            var rightHandle = new VisualElement();
            rightHandle.AddToClassList("ca-tl-clip-handle");
            rightHandle.AddToClassList("ca-tl-clip-handle-right");
            rightHandle.Add(BuildClipHandleGrip());
            clip.Add(rightHandle);

            AttachItemContextMenuHitTarget(moveZone, adapter);
            AttachItemContextMenuHitTarget(leftHandle, adapter);
            AttachItemContextMenuHitTarget(rightHandle, adapter);

            clip.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 1)
                    SelectItem(adapter.Id);
            });

            if (adapter.CanEdit && !_snapshot.ReadOnly)
            {
                clip.AddManipulator(new TimelineClipResizeManipulator(this, adapter, TimelineClipDragMode.Move));
                moveZone.AddManipulator(new TimelineClipResizeManipulator(this, adapter, TimelineClipDragMode.Move));
                leftHandle.AddManipulator(new TimelineClipResizeManipulator(this, adapter, TimelineClipDragMode.ResizeStart));
                rightHandle.AddManipulator(new TimelineClipResizeManipulator(this, adapter, TimelineClipDragMode.ResizeEnd));
            }

            return clip;
        }

        private void ApplyAnimationTransitionGeometry(VisualElement transition, AnimationTrackTimelineAdapter adapter)
        {
            int transitionFrames = adapter.TransitionFrames;
            if (transitionFrames <= 0)
            {
                transition.style.display = DisplayStyle.None;
                return;
            }

            transition.style.display = DisplayStyle.Flex;
            float pps = _state.PixelsPerFrame;
            float pad = CharacterActionTimelineLayout.ContentPadding;
            float clipStart = FrameStartX(adapter.StartFrame);
            float width = transitionFrames * pps;
            float left = Math.Max(pad, clipStart - width);
            transition.style.left = left;
            transition.style.width = Math.Max(2f, Math.Min(width, clipStart - pad));
        }

        private VisualElement BuildClipElement(ITimelineItemAdapter adapter)
        {
            var clip = new VisualElement();
            clip.AddToClassList("ca-tl-clip");
            clip.tooltip = adapter.Title;
            ApplyItemGeometry(clip, adapter);
            clip.style.backgroundColor = adapter.Color;

            var label = new Label(adapter.Title);
            label.AddToClassList("ca-tl-clip-label");
            clip.Add(label);

            var moveZone = new VisualElement();
            moveZone.AddToClassList("ca-tl-clip-move-zone");
            clip.Add(moveZone);

            var leftHandle = new VisualElement();
            leftHandle.AddToClassList("ca-tl-clip-handle");
            leftHandle.AddToClassList("ca-tl-clip-handle-left");
            leftHandle.Add(BuildClipHandleGrip());
            clip.Add(leftHandle);

            var rightHandle = new VisualElement();
            rightHandle.AddToClassList("ca-tl-clip-handle");
            rightHandle.AddToClassList("ca-tl-clip-handle-right");
            rightHandle.Add(BuildClipHandleGrip());
            clip.Add(rightHandle);

            AttachItemContextMenuHitTarget(moveZone, adapter);
            AttachItemContextMenuHitTarget(leftHandle, adapter);
            AttachItemContextMenuHitTarget(rightHandle, adapter);

            clip.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 1)
                    SelectItem(adapter.Id);
            });

            if (adapter.CanEdit && !_snapshot.ReadOnly)
            {
                clip.AddManipulator(new TimelineClipResizeManipulator(this, adapter, TimelineClipDragMode.Move));
                moveZone.AddManipulator(new TimelineClipResizeManipulator(this, adapter, TimelineClipDragMode.Move));
                leftHandle.AddManipulator(new TimelineClipResizeManipulator(this, adapter, TimelineClipDragMode.ResizeStart));
                rightHandle.AddManipulator(new TimelineClipResizeManipulator(this, adapter, TimelineClipDragMode.ResizeEnd));
            }

            return clip;
        }

        private static VisualElement BuildClipHandleGrip()
        {
            var grip = new VisualElement();
            grip.AddToClassList("ca-tl-clip-handle-grip");
            return grip;
        }

        private VisualElement BuildMarkerElement(ITimelineItemAdapter adapter)
        {
            var marker = new VisualElement();
            marker.AddToClassList("ca-tl-marker");
            marker.tooltip = adapter.FullTitle;
            ApplyItemGeometry(marker, adapter);
            marker.style.backgroundColor = adapter.Color;

            var node = new VisualElement();
            node.AddToClassList("ca-tl-marker-node");
            marker.Add(node);

            var hitArea = new VisualElement();
            hitArea.AddToClassList("ca-tl-marker-hit-area");
            marker.Add(hitArea);
            AttachItemContextMenuHitTarget(hitArea, adapter);
            ApplyItemGeometry(marker, adapter);

            if (adapter is ControlScaleTimelineAdapter)
            {
                AttachItemContextMenuHitTarget(node, adapter);
                marker.RegisterCallback<MouseDownEvent>(evt => { if (evt.clickCount == 1) SelectItem(adapter.Id); });
                if (adapter.CanEdit && !_snapshot.ReadOnly)
                    marker.AddManipulator(new TimelineMarkerDragManipulator(this, adapter, marker));
                return marker;
            }

            var bubble = new Label("i")
            {
                tooltip = adapter.FullTitle,
            };
            bubble.AddToClassList("ca-tl-tooltip-bubble");
            ApplyTooltipBubbleText(bubble, adapter.FullTitle);
            marker.Add(bubble);
            ApplyItemGeometry(marker, adapter);

            AttachItemContextMenuHitTarget(node, adapter);
            AttachItemContextMenuHitTarget(bubble, adapter);

            marker.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 1)
                    SelectItem(adapter.Id);
            });

            if (adapter.CanEdit && !_snapshot.ReadOnly)
                marker.AddManipulator(new TimelineMarkerDragManipulator(this, adapter, marker));

            return marker;
        }

        private void ApplyItemGeometry(VisualElement element, ITimelineItemAdapter adapter)
        {
            float pps = _state.PixelsPerFrame;
            float pad = CharacterActionTimelineLayout.ContentPadding;
            CharacterActionTimelineItemPlacement placement = null;
            if (_trackLayouts.TryGetValue(adapter.TrackKind, out CharacterActionTimelineTrackLayout layout))
                layout.TryGetPlacement(adapter.Id, out placement);

            int lane = placement?.Lane ?? 0;
            if (adapter is ControlScaleTimelineAdapter) lane = 0;
            if (adapter.ItemKind == TimelineItemKind.Marker)
            {
                float centerX = pad
                    + CharacterActionTimelineLayout.FrameToX(adapter.StartFrame, pps)
                    + pps * 0.5f;
                float width = Math.Max(1f, pps);
                element.style.left = centerX - width * 0.5f;
                element.style.width = width;
                bool controlKey = adapter is ControlScaleTimelineAdapter;
                float markerHeight = controlKey ? ControlScaleItemHeight : CharacterActionTimelineLayout.MarkerHeight;
                element.style.top = controlKey
                    ? (ControlScaleRowHeight - markerHeight) * 0.5f
                    : CharacterActionTimelineLaneLayout.MarkerTop(lane);
                element.style.height = markerHeight;
                VisualElement hitArea = element.Q(className: "ca-tl-marker-hit-area");
                if (hitArea != null)
                {
                    float hitWidth = Math.Max(CharacterActionTimelineLayout.MarkerNodeWidth, width);
                    hitArea.style.left = (width - hitWidth) * 0.5f;
                    hitArea.style.width = hitWidth;
                    hitArea.style.height = markerHeight;
                }
                VisualElement markerNode = element.Q(className: "ca-tl-marker-node");
                if (markerNode != null)
                    markerNode.style.width = Math.Min(6f, Math.Max(0f, width - 4f));
                Label tooltipBubble = element.Q<Label>(className: "ca-tl-tooltip-bubble");
                if (tooltipBubble != null)
                    tooltipBubble.style.left = width + 2f;
            }
            else
            {
                float x0 = pad + CharacterActionTimelineLayout.FrameToX(adapter.StartFrame, pps);
                float x1 = pad + CharacterActionTimelineLayout.FrameToX(adapter.EndFrame + 1, pps);
                float width = Math.Max(CharacterActionTimelineLayout.MinClipWidth, x1 - x0);
                element.style.left = x0;
                element.style.width = width;
                element.style.top = CharacterActionTimelineLaneLayout.ClipTop(lane);
                element.style.height = CharacterActionTimelineLayout.ClipHeight;
                element.EnableInClassList("ca-tl-clip-narrow", width <= CharacterActionTimelineLayout.NarrowClipWidth);
                element.EnableInClassList("ca-tl-clip-micro", width <= CharacterActionTimelineLayout.MicroClipWidth);
            }

            element.style.bottom = new StyleLength(StyleKeyword.Auto);
        }

        private void RefreshMarkerTooltipMode()
        {
            if (_snapshot == null)
                return;

            foreach (KeyValuePair<string, VisualElement> pair in _itemElements)
            {
                ITimelineItemAdapter adapter = _snapshot.FindItem(pair.Key);
                if (adapter == null || adapter.ItemKind != TimelineItemKind.Marker)
                    continue;

                Label bubble = pair.Value.Q<Label>(className: "ca-tl-tooltip-bubble");
                if (bubble != null)
                    ApplyTooltipBubbleText(bubble, adapter.FullTitle);
            }
        }

        private void ApplyTooltipBubbleText(Label bubble, string title)
        {
            if (bubble == null)
                return;

            bubble.tooltip = title ?? string.Empty;
            bubble.text = _state.ShowMarkerTooltipText ? title ?? string.Empty : "i";
            bubble.EnableInClassList("ca-tl-tooltip-bubble-expanded", _state.ShowMarkerTooltipText);
        }

        private VisualElement BuildLabelRow(string title, bool isRuler, TimelineTrackDescriptor track = null, int rowIndex = -1)
        {
            var row = new VisualElement();
            row.AddToClassList("ca-tl-label-row");
            float rowHeight = isRuler
                ? CharacterActionTimelineLayout.RulerHeight
                : ResolveTrackRowHeight(track);
            row.style.height = rowHeight;
            if (rowIndex >= 0)
                ApplyRowStripe(row, rowIndex);

            var label = new Label(title);
            label.AddToClassList("ca-tl-label-text");
            row.Add(label);

            return row;
        }

        private void AppendFrameGridLines(VisualElement row)
        {
            if (_snapshot == null)
                return;

            int duration = _snapshot.DurationFrames;
            int step = ResolveGridStep(duration);
            for (int frame = step; frame < duration; frame += step)
            {
                var line = new VisualElement();
                line.AddToClassList("ca-tl-grid-line");
                line.pickingMode = PickingMode.Ignore;
                line.style.left = FrameStartX(frame);
                row.Add(line);
            }
        }

        private void AppendActionEndLine(VisualElement row, bool showLabel)
        {
            if (_snapshot == null || _snapshot.ActionDurationFrames >= _snapshot.DurationFrames)
                return;

            var line = new VisualElement();
            line.AddToClassList("ca-tl-action-end-line");
            line.pickingMode = PickingMode.Ignore;
            line.style.left = FrameStartX(_snapshot.ActionDurationFrames);
            if (showLabel)
            {
                var label = new Label("动作结束");
                label.AddToClassList("ca-tl-action-end-label");
                line.Add(label);
            }
            row.Add(line);
        }

        private static int ResolveGridStep(int duration)
        {
            if (duration <= 20)
                return 1;
            if (duration <= 60)
                return 5;
            return 10;
        }

        private static void ApplyRowStripe(VisualElement row, int rowIndex)
        {
            if (rowIndex > 0 && rowIndex % 2 == 0)
                row.AddToClassList("ca-tl-row-even");
        }

        private float ResolveTrackRowHeight(TimelineTrackDescriptor track)
        {
            if (track?.Kind == TimelineTrackKind.MovementControl || track?.Kind == TimelineTrackKind.RotationControl)
                return ControlScaleRowHeight;
            if (track == null)
                return CharacterActionTimelineLayout.RowHeight;

            if (_trackLayouts.TryGetValue(track.Kind, out CharacterActionTimelineTrackLayout layout))
                return layout.RowHeight;

            return track.Kind == TimelineTrackKind.Animation
                ? CharacterActionTimelineLayout.AnimationRowHeight
                : CharacterActionTimelineLayout.RowHeight;
        }

        private static string TrackTitle(TimelineTrackKind kind)
        {
            return kind switch
            {
                TimelineTrackKind.Phases => "\u9636\u6bb5",
                TimelineTrackKind.TransitionWindow => "\u5207\u6362\u7a97\u53e3 / Transition Window",
                TimelineTrackKind.Motion => "\u8fd0\u52a8",
                TimelineTrackKind.Animation => "\u52a8\u753b",
                TimelineTrackKind.Combat => "\u6218\u6597",
                TimelineTrackKind.Gameplay => "\u73a9\u6cd5",
                TimelineTrackKind.Presentation => "\u8868\u73b0",
                _ => kind.ToString(),
            };
        }
    }
}
