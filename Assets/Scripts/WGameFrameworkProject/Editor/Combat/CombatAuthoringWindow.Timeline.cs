using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Combat.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Combat.Editor
{
    public sealed partial class CombatAuthoringWindow
    {
        private void RefreshTimeline(bool refreshDetail = true)
        {
            bool hadSelection = _selectedTimelineRowIndex >= 0 && _selectedTimelineRowIndex < _timelineRows.Count;
            TimelineRow previousSelection = hadSelection ? _timelineRows[_selectedTimelineRowIndex] : default;

            _timelineRows.Clear();
            if (_actionAsset != null)
            {
                _timelineRows.Add(new TimelineRow("Action", "Startup", 0, _actionAsset.Startup, "startup", TimelineRowKind.ActionProperty));
                _timelineRows.Add(new TimelineRow("Action", "Active", 0, _actionAsset.Active, "active", TimelineRowKind.ActionProperty));
                _timelineRows.Add(new TimelineRow("Action", "Recovery", 0, _actionAsset.Recovery, "recovery", TimelineRowKind.ActionProperty));
                AddShapeRows("Hitbox", _actionAsset.Hitboxes, "hitboxes");
                AddShapeRows("Hurtbox", _actionAsset.Hurtboxes, "hurtboxes");
                AddTraceRows();
            }

            if (_timelineList != null)
            {
                _timelineList.RefreshItems();
            }

            _selectedTimelineRowIndex = hadSelection
                ? FindTimelineRow(previousSelection)
                : FindPersistentTimelineSelection();
            if (_timelineList != null)
            {
                if (_selectedTimelineRowIndex >= 0)
                {
                    _timelineList.SetSelectionWithoutNotify(new[] { _selectedTimelineRowIndex });
                }
                else
                {
                    _timelineList.SetSelectionWithoutNotify(Array.Empty<int>());
                }
            }

            RefreshTimelineStrip();
            RefreshFrameSlider();
            if (refreshDetail && !IsDetailEditableFieldFocused())
            {
                RefreshDetail(_selectedTimelineRowIndex >= 0 ? _timelineRows[_selectedTimelineRowIndex] : (TimelineRow?)null);
            }
        }

        private void RegisterKeyboardShortcuts()
        {
            if (_keyboardShortcutsRegistered)
            {
                return;
            }

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            _keyboardShortcutsRegistered = true;
        }

        private void UnregisterKeyboardShortcuts()
        {
            if (!_keyboardShortcutsRegistered)
            {
                return;
            }

            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            _keyboardShortcutsRegistered = false;
        }

        private void RegisterTimelineDragRecoveryCallbacks()
        {
            rootVisualElement.UnregisterCallback<PointerUpEvent>(OnTimelineDragRootPointerUp, TrickleDown.TrickleDown);
            rootVisualElement.UnregisterCallback<PointerCancelEvent>(OnTimelineDragRootPointerCancel, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerUpEvent>(OnTimelineDragRootPointerUp, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerCancelEvent>(OnTimelineDragRootPointerCancel, TrickleDown.TrickleDown);
        }

        private void OnTimelineDragRootPointerUp(PointerUpEvent evt)
        {
            if (!_timelineDrag.IsDragging || _timelineDrag.PointerId != evt.pointerId)
            {
                return;
            }

            CompleteTimelineRangeDrag(cancel: false);
            evt.StopPropagation();
        }

        private void OnTimelineDragRootPointerCancel(PointerCancelEvent evt)
        {
            if (!_timelineDrag.IsDragging || _timelineDrag.PointerId != evt.pointerId)
            {
                return;
            }

            CompleteTimelineRangeDrag(cancel: true);
            evt.StopPropagation();
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            if (evt.keyCode == KeyCode.Escape && _timelineDrag.IsDragging)
            {
                CompleteTimelineRangeDrag(cancel: true);
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
            {
                return;
            }

            if (IsKeyboardEventFromEditableField(evt))
            {
                return;
            }

            TryDeleteSelectedShape();
            evt.StopPropagation();
        }

        private void OnUndoRedoPerformed()
        {
            if (HasValidSerializedTarget(_actionSerialized))
            {
                _actionSerialized.Update();
            }

            RebuildActionFields();
            RefreshTimeline();
            RefreshValidation();
            SceneView.RepaintAll();
            Repaint();
        }

        private void RefreshTimelineStrip()
        {
            if (_timelineStripContent == null || _timelineStripEmptyRoot == null || _timelineStripScroll == null)
            {
                return;
            }

            _timelineStripContent.Clear();
            _timelinePlayhead = null;

            bool hasAction = _actionAsset != null;
            _timelineStripEmptyRoot.style.display = hasAction ? DisplayStyle.None : DisplayStyle.Flex;
            _timelineStripScroll.style.display = hasAction ? DisplayStyle.Flex : DisplayStyle.None;
            if (_timelineViewportSlider != null)
            {
                _timelineViewportSlider.style.display = hasAction ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_timelineViewportLabel != null)
            {
                _timelineViewportLabel.style.display = hasAction ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_timelineDragStatus != null)
            {
                _timelineDragStatus.style.display = hasAction ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!hasAction)
            {
                return;
            }

            int totalFrames = Math.Max(1, _actionAsset.TotalFrames);
            float trackWidth = GetTimelineTrackWidth(totalFrames);
            float contentWidth = TimelineStripLabelWidth + trackWidth;
            float contentHeight = TimelineStripHeaderHeight + Math.Max(1, _timelineRows.Count) * TimelineStripLaneHeight;
            _timelineStripContent.style.width = contentWidth;
            _timelineStripContent.style.height = contentHeight;

            AddTimelineRuler(totalFrames, trackWidth);
            if (_timelineRows.Count == 0)
            {
                AddTimelineEmptyLane(trackWidth);
            }
            else
            {
                for (int i = 0; i < _timelineRows.Count; i++)
                {
                    AddTimelineLane(i, totalFrames, trackWidth);
                }
            }

            _timelinePlayhead = new VisualElement();
            _timelinePlayhead.pickingMode = PickingMode.Ignore;
            _timelinePlayhead.style.position = Position.Absolute;
            _timelinePlayhead.style.top = 0;
            _timelinePlayhead.style.width = 2;
            _timelinePlayhead.style.height = contentHeight;
            _timelinePlayhead.style.backgroundColor = UiColors.TimelinePlayhead;
            _timelineStripContent.Add(_timelinePlayhead);
            UpdateTimelinePlayhead();
            // Delay viewport update to ensure ScrollView layout is complete
            EditorApplication.delayCall += () => UpdateTimelineViewportControl();
        }

        private float GetTimelineTrackWidth(int totalFrames)
        {
            int pixelsPerFrame = Clamp(
                _timelinePixelsPerFrame,
                TimelineStripMinPixelsPerFrame,
                TimelineStripMaxPixelsPerFrame);
            return Math.Max(1f, Math.Max(1, totalFrames) * pixelsPerFrame);
        }

        private void SetTimelinePixelsPerFrame(int pixelsPerFrame)
        {
            int clampedValue = Clamp(
                pixelsPerFrame,
                TimelineStripMinPixelsPerFrame,
                TimelineStripMaxPixelsPerFrame);
            if (_timelinePixelsPerFrame == clampedValue)
            {
                return;
            }

            _timelinePixelsPerFrame = clampedValue;
            UpdateTimelineViewportControl();
            RefreshTimelineStrip();
        }

        private void OnTimelineViewportRangeChanged(ChangeEvent<Vector2> evt)
        {
            if (_isApplyingTimelineViewport
                || !_isInteractingWithTimelineViewport
                || evt == null
                || _actionAsset == null)
            {
                return;
            }

            ApplyTimelineViewportRange(evt.newValue);
        }

        private void BeginTimelineViewportInteraction(PointerDownEvent evt)
        {
            if (evt == null || evt.button != 0)
            {
                return;
            }

            _isInteractingWithTimelineViewport = true;
        }

        private void EndTimelineViewportInteraction()
        {
            _isInteractingWithTimelineViewport = false;
            UpdateTimelineViewportControl();
        }

        private void EndTimelineViewportInteraction(PointerUpEvent evt)
        {
            EndTimelineViewportInteraction();
        }

        private void EndTimelineViewportInteraction(PointerCancelEvent evt)
        {
            EndTimelineViewportInteraction();
        }

        private void EndTimelineViewportInteraction(PointerCaptureOutEvent evt)
        {
            EndTimelineViewportInteraction();
        }

        private void ApplyTimelineViewportRange(Vector2 range)
        {
            if (_timelineStripScroll == null || _actionAsset == null)
            {
                return;
            }

            int totalFrames = Math.Max(1, _actionAsset.TotalFrames);
            float controlFrameLimit = GetTimelineViewportControlFrameLimit(totalFrames);
            float startFrame = Mathf.Clamp(range.x, 0f, Math.Max(0f, totalFrames - 1f));
            float endFrame = Mathf.Clamp(range.y, startFrame + 1f, controlFrameLimit);
            float visibleFrames = Math.Max(1f, endFrame - startFrame);
            float viewportWidth = GetTimelineViewportTrackWidth();
            int nextPixelsPerFrame = Clamp(
                Mathf.RoundToInt(viewportWidth / visibleFrames),
                TimelineStripMinPixelsPerFrame,
                TimelineStripMaxPixelsPerFrame);

            _timelinePixelsPerFrame = nextPixelsPerFrame;
            RefreshTimelineStrip();

            float nextScrollX = startFrame * nextPixelsPerFrame;
            _timelineStripScroll.schedule.Execute(() =>
            {
                if (_timelineStripScroll != null)
                {
                    _timelineStripScroll.scrollOffset = new Vector2(nextScrollX, _timelineStripScroll.scrollOffset.y);
                    UpdateTimelineViewportControl();
                }
            });
        }

        private void UpdateTimelineViewportControl()
        {
            if (_timelineViewportSlider == null || _actionAsset == null)
            {
                return;
            }

            int totalFrames = Math.Max(1, _actionAsset.TotalFrames);
            int value = Clamp(
                _timelinePixelsPerFrame,
                TimelineStripMinPixelsPerFrame,
                TimelineStripMaxPixelsPerFrame);
            if (_timelinePixelsPerFrame != value)
            {
                _timelinePixelsPerFrame = value;
            }

            float viewportWidth = GetTimelineViewportTrackWidth();
            float controlFrameLimit = GetTimelineViewportControlFrameLimit(totalFrames);
            float visibleFrames = Mathf.Clamp(viewportWidth / Math.Max(1, value), 1f, controlFrameLimit);
            float startFrame = _timelineStripScroll == null
                ? 0f
                : Mathf.Clamp(_timelineStripScroll.scrollOffset.x / Math.Max(1, value), 0f, Math.Max(0f, totalFrames - visibleFrames));
            float endFrame = Mathf.Min(controlFrameLimit, startFrame + visibleFrames);

            _isApplyingTimelineViewport = true;
            _timelineViewportSlider.lowLimit = 0f;
            _timelineViewportSlider.highLimit = controlFrameLimit;
            _timelineViewportSlider.SetValueWithoutNotify(new Vector2(startFrame, endFrame));
            _isApplyingTimelineViewport = false;

            if (_timelineViewportLabel != null)
            {
                _timelineViewportLabel.text = value + " px/f  |  " + Mathf.FloorToInt(startFrame) + "-" + Mathf.CeilToInt(endFrame);
            }
        }

        private float GetTimelineViewportTrackWidth()
        {
            if (_timelineStripScroll == null)
            {
                return 1f;
            }

            float width = _timelineStripScroll.resolvedStyle.width;
            return Math.Max(1f, width);
        }

        private float GetTimelineViewportControlFrameLimit(int totalFrames)
        {
            float minScaleVisibleFrames = GetTimelineViewportTrackWidth() / TimelineStripMinPixelsPerFrame;
            return Math.Max(Math.Max(1, totalFrames), minScaleVisibleFrames);
        }

        private void AddTimelineRuler(int totalFrames, float trackWidth)
        {
            var label = CreateTimelineCell("Frame", 0, 0, TimelineStripLabelWidth, TimelineStripHeaderHeight, UiColors.TimelineHeaderBg);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _timelineStripContent.Add(label);

            var track = CreateTimelineBlock(TimelineStripLabelWidth, 0, trackWidth, TimelineStripHeaderHeight, UiColors.SurfaceDeep);
            _timelineStripContent.Add(track);

            int tickStep = GetTimelineTickStep(totalFrames);
            for (int frame = 0; frame < totalFrames; frame += tickStep)
            {
                AddTimelineTick(frame, totalFrames, trackWidth, true);
            }

            if ((totalFrames - 1) % tickStep != 0)
            {
                AddTimelineTick(totalFrames - 1, totalFrames, trackWidth, true);
            }
        }

        private void AddTimelineTick(int frame, int totalFrames, float trackWidth, bool withLabel)
        {
            float x = TimelineStripLabelWidth + FrameToTimelineX(frame, totalFrames, trackWidth);
            var tick = CreateTimelineBlock(x, 0, 1, TimelineStripHeaderHeight, UiColors.TimelineTickLine);
            tick.pickingMode = PickingMode.Ignore;
            _timelineStripContent.Add(tick);

            if (!withLabel)
            {
                return;
            }

            var label = new Label(frame.ToString());
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.left = x + 3;
            label.style.top = 6;
            label.style.width = 46;
            label.style.height = 18;
            label.style.fontSize = 10;
            label.style.color = UiColors.TimelineTickLabel;
            _timelineStripContent.Add(label);
        }

        private void AddTimelineEmptyLane(float trackWidth)
        {
            float y = TimelineStripHeaderHeight;
            _timelineStripContent.Add(CreateTimelineCell("Timeline", 0, y, TimelineStripLabelWidth, TimelineStripLaneHeight, UiColors.TimelineLabelBg));
            _timelineStripContent.Add(CreateTimelineCell("当前 Action 没有 Timeline 条目。", TimelineStripLabelWidth, y, trackWidth, TimelineStripLaneHeight, UiColors.SurfaceDeep));
        }

        private void AddTimelineLane(int index, int totalFrames, float trackWidth)
        {
            TimelineRow row = _timelineRows[index];
            float y = TimelineStripHeaderHeight + index * TimelineStripLaneHeight;
            Color laneColor = index % 2 == 0 ? UiColors.TimelineLaneEven : UiColors.TimelineLaneOdd;

            string rowLabel = row.Section + " / " + row.Label;
            var header = CreateTimelineCell(rowLabel, 0, y, TimelineStripLabelWidth, TimelineStripLaneHeight, UiColors.TimelineLabelBg);
            RegisterTimelineRowClick(header, index);
            _timelineStripContent.Add(header);

            var lane = CreateTimelineBlock(TimelineStripLabelWidth, y, trackWidth, TimelineStripLaneHeight, laneColor);
            RegisterTimelineRowClick(lane, index);
            _timelineStripContent.Add(lane);

            int minorStep = GetTimelineTickStep(totalFrames);
            for (int frame = 0; frame < totalFrames; frame += minorStep)
            {
                float tickX = TimelineStripLabelWidth + FrameToTimelineX(frame, totalFrames, trackWidth);
                var grid = CreateTimelineBlock(tickX, y, 1, TimelineStripLaneHeight, UiColors.TimelineGridLine);
                grid.pickingMode = PickingMode.Ignore;
                _timelineStripContent.Add(grid);
            }

            if (row.FrameRange.IsEmpty)
            {
                var emptyLabel = new Label("empty");
                emptyLabel.pickingMode = PickingMode.Ignore;
                emptyLabel.style.position = Position.Absolute;
                emptyLabel.style.left = TimelineStripLabelWidth + 8;
                emptyLabel.style.top = y + 9;
                emptyLabel.style.width = 80;
                emptyLabel.style.height = 18;
                emptyLabel.style.fontSize = 10;
                emptyLabel.style.color = UiColors.TimelineEmptyLaneText;
                _timelineStripContent.Add(emptyLabel);
                return;
            }

            int startFrame = Math.Max(0, Math.Min(row.FrameRange.StartFrame, totalFrames - 1));
            int endFrame = Math.Max(startFrame, Math.Min(row.FrameRange.EndFrame, totalFrames - 1));
            float x = TimelineStripLabelWidth + FrameToTimelineX(startFrame, totalFrames, trackWidth);
            float width = Math.Max(4f, (endFrame - startFrame + 1) * GetTimelineFrameWidth(totalFrames, trackWidth));
            var bar = CreateTimelineBlock(x, y + 7, width, TimelineStripLaneHeight - 14, GetTimelineBarColor(row));
            bar.style.borderBottomLeftRadius = 5;
            bar.style.borderBottomRightRadius = 5;
            bar.style.borderTopLeftRadius = 5;
            bar.style.borderTopRightRadius = 5;
            bar.tooltip = BuildTimelineTooltip(row);
            bool draggable = IsTimelineRangeDraggable(row);
            var leftHandle = CreateTimelineRangeHandle(true);
            var rightHandle = CreateTimelineRangeHandle(false);
            leftHandle.style.display = draggable ? DisplayStyle.Flex : DisplayStyle.None;
            rightHandle.style.display = draggable ? DisplayStyle.Flex : DisplayStyle.None;
            bar.Add(leftHandle);
            bar.Add(rightHandle);

            if (index == _selectedTimelineRowIndex)
            {
                bar.style.borderBottomColor = UiColors.SelectionBorder;
                bar.style.borderLeftColor = UiColors.SelectionBorder;
                bar.style.borderRightColor = UiColors.SelectionBorder;
                bar.style.borderTopColor = UiColors.SelectionBorder;
                bar.style.borderBottomWidth = 1;
                bar.style.borderLeftWidth = 1;
                bar.style.borderRightWidth = 1;
                bar.style.borderTopWidth = 1;
                SetTimelineHandleOpacity(leftHandle, rightHandle, 1f);
            }
            else
            {
                SetTimelineHandleOpacity(leftHandle, rightHandle, 0.4f);
            }

            bar.RegisterCallback<ClickEvent>(OnTimelineStripBarClicked);
            var barLabel = new Label(FormatRange(row.FrameRange));
            barLabel.pickingMode = PickingMode.Ignore;
            barLabel.style.position = Position.Absolute;
            barLabel.style.left = x + 8;
            barLabel.style.top = y + 10;
            barLabel.style.width = Math.Max(36f, width - 10f);
            barLabel.style.height = 16;
            barLabel.style.fontSize = 11;
            barLabel.style.color = UiColors.SelectionBorder;
            bar.userData = new TimelineBarData(index, barLabel, leftHandle, rightHandle, totalFrames, trackWidth);
            if (draggable)
            {
                RegisterTimelineBarDragCallbacks(bar);
            }

            _timelineStripContent.Add(bar);
            _timelineStripContent.Add(barLabel);
        }

        private void RegisterTimelineRowClick(VisualElement element, int index)
        {
            element.userData = index;
            element.RegisterCallback<ClickEvent>(OnTimelineStripRowClicked);
        }

        private void OnTimelineStripRowClicked(ClickEvent evt)
        {
            if (evt.currentTarget is VisualElement element && element.userData is int index)
            {
                SelectTimelineRow(index);
                evt.StopPropagation();
            }
        }

        private void OnTimelineStripBarClicked(ClickEvent evt)
        {
            if (_ignoreNextTimelineBarClick)
            {
                _ignoreNextTimelineBarClick = false;
                evt.StopPropagation();
                return;
            }

            if (evt.currentTarget is VisualElement element && TryGetTimelineBarIndex(element, out int index))
            {
                SelectTimelineRow(index);
                evt.StopPropagation();
            }
        }

        private static bool TryGetTimelineBarIndex(VisualElement element, out int index)
        {
            if (element.userData is TimelineBarData data)
            {
                index = data.RowIndex;
                return true;
            }

            if (element.userData is int legacyIndex)
            {
                index = legacyIndex;
                return true;
            }

            index = -1;
            return false;
        }

        private static bool TryGetTimelineBarData(VisualElement element, out TimelineBarData data)
        {
            data = element.userData as TimelineBarData;
            return data != null;
        }

        private void RegisterTimelineBarDragCallbacks(VisualElement bar)
        {
            bar.RegisterCallback<PointerEnterEvent>(OnTimelineStripBarPointerEnter);
            bar.RegisterCallback<PointerLeaveEvent>(OnTimelineStripBarPointerLeave);
            bar.RegisterCallback<PointerDownEvent>(OnTimelineStripBarPointerDown);
            bar.RegisterCallback<PointerMoveEvent>(OnTimelineStripBarPointerMove);
            bar.RegisterCallback<PointerUpEvent>(OnTimelineStripBarPointerUp);
            bar.RegisterCallback<PointerCancelEvent>(OnTimelineStripBarPointerCancel);
            bar.RegisterCallback<PointerCaptureOutEvent>(OnTimelineStripBarPointerCaptureOut);
        }

        private void OnTimelineStripBarPointerEnter(PointerEnterEvent evt)
        {
            if (evt.currentTarget is VisualElement bar && TryGetTimelineBarData(bar, out TimelineBarData data))
            {
                SetTimelineHandleOpacity(data.LeftHandle, data.RightHandle, 1f);
            }
        }

        private void OnTimelineStripBarPointerLeave(PointerLeaveEvent evt)
        {
            if (_timelineDrag.IsDragging)
            {
                return;
            }

            if (evt.currentTarget is VisualElement bar && TryGetTimelineBarData(bar, out TimelineBarData data))
            {
                float opacity = data.RowIndex == _selectedTimelineRowIndex ? 1f : 0.4f;
                SetTimelineHandleOpacity(data.LeftHandle, data.RightHandle, opacity);
                SetTimelineDragStatus("拖动条块主体移动范围；拖动左右边缘调整 Start / End。", UiColors.TextSecondary);
            }
        }

        private void OnTimelineStripBarPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0
                || _actionAsset == null
                || _timelineStripContent == null
                || !(evt.currentTarget is VisualElement bar)
                || !TryGetTimelineBarData(bar, out TimelineBarData data)
                || data.RowIndex < 0
                || data.RowIndex >= _timelineRows.Count)
            {
                return;
            }

            TimelineRow row = _timelineRows[data.RowIndex];
            if (!IsTimelineRangeDraggable(row))
            {
                return;
            }

            int maxFrame = GetMaxActionFrame();
            int startFrame = Clamp(row.FrameRange.StartFrame, 0, maxFrame);
            int endFrame = Clamp(row.FrameRange.EndFrame, startFrame, maxFrame);
            if (endFrame < startFrame)
            {
                return;
            }

            TimelineDragMode mode = GetTimelineDragMode(bar, evt.position);
            int pointerFrame = PanelPositionToTimelineFrame(evt.position, data.TotalFrames, data.TrackWidth);
            _timelineDrag.Begin(data.RowIndex, row, mode, startFrame, endFrame, pointerFrame, data.TotalFrames, data.TrackWidth, evt.pointerId, bar, data.RangeLabel, data.LeftHandle, data.RightHandle);

            SelectTimelineRowForTimelineDrag(data.RowIndex, bar, data);
            bar.CapturePointer(evt.pointerId);
            UpdateTimelineDragPreview(startFrame, endFrame);
            evt.StopPropagation();
        }

        private void OnTimelineStripBarPointerMove(PointerMoveEvent evt)
        {
            if (!(evt.currentTarget is VisualElement bar) || !TryGetTimelineBarData(bar, out TimelineBarData data))
            {
                return;
            }

            if (!_timelineDrag.IsDragging)
            {
                TimelineDragMode hoverMode = GetTimelineDragMode(bar, evt.position);
                SetTimelineDragStatus(TimelineDragVerb(hoverMode) + "：" + FormatRange(_timelineRows[data.RowIndex].FrameRange), UiColors.TextSecondary);
                return;
            }

            if (_timelineDrag.PointerId != evt.pointerId)
            {
                return;
            }

            int targetFrame = PanelPositionToTimelineFrame(evt.position, _timelineDrag.TotalFrames, _timelineDrag.TrackWidth);
            CalculateTimelineDragRange(_timelineDrag.Mode, _timelineDrag.OriginalStartFrame, _timelineDrag.OriginalEndFrame, _timelineDrag.PointerStartFrame, targetFrame, GetMaxActionFrame(), out int previewStart, out int previewEnd);
            UpdateTimelineDragPreview(previewStart, previewEnd);
            evt.StopPropagation();
        }

        private void OnTimelineStripBarPointerUp(PointerUpEvent evt)
        {
            if (!_timelineDrag.IsDragging || _timelineDrag.PointerId != evt.pointerId)
            {
                return;
            }

            CompleteTimelineRangeDrag(cancel: false);
            evt.StopPropagation();
        }

        private void SuppressNextTimelineBarClick()
        {
            _ignoreNextTimelineBarClick = true;
            rootVisualElement.schedule.Execute(() => _ignoreNextTimelineBarClick = false).ExecuteLater(250);
        }

        private void OnTimelineStripBarPointerCancel(PointerCancelEvent evt)
        {
            if (!_timelineDrag.IsDragging || _timelineDrag.PointerId != evt.pointerId)
            {
                return;
            }

            CompleteTimelineRangeDrag(cancel: true);
            evt.StopPropagation();
        }

        private void OnTimelineStripBarPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_finishingTimelineDrag || !_timelineDrag.IsDragging || evt.currentTarget != _timelineDrag.Bar)
            {
                return;
            }

            CompleteTimelineRangeDrag(cancel: false);
        }

        private void CompleteTimelineRangeDrag(bool cancel)
        {
            bool changed = _timelineDrag.HasPreviewChange;
            TimelineRow row = _timelineDrag.Row;
            int rowIndex = _timelineDrag.RowIndex;
            int nextStart = _timelineDrag.PreviewStartFrame;
            int nextEnd = _timelineDrag.PreviewEndFrame;
            int originalStart = _timelineDrag.OriginalStartFrame;
            int originalEnd = _timelineDrag.OriginalEndFrame;
            int pointerId = _timelineDrag.PointerId;
            VisualElement capturedBar = _timelineDrag.Bar;

            _finishingTimelineDrag = true;
            if (capturedBar != null && capturedBar.HasPointerCapture(pointerId))
            {
                capturedBar.ReleasePointer(pointerId);
            }

            _timelineDrag.Reset();
            _finishingTimelineDrag = false;

            if (cancel)
            {
                RefreshTimelineStrip();
                CombatAuthoringSceneState.SetFrame(originalStart);
                SceneView.RepaintAll();
                SetTimelineDragStatus("已取消 timeline range 拖动。", UiColors.TextSecondary);
                return;
            }

            if (changed)
            {
                FinishTimelineRangeDrag(row, rowIndex, nextStart, nextEnd);
                SuppressNextTimelineBarClick();
            }
            else
            {
                SetTimelineDragStatus("范围未变化：" + nextStart + "-" + nextEnd, UiColors.TextSecondary);
            }
        }

        private void SelectTimelineRowForTimelineDrag(int index, VisualElement bar, TimelineBarData data)
        {
            _selectedTimelineRowIndex = index;
            TimelineRow row = _timelineRows[index];
            CombatAuthoringSceneState.SetSelection(new CombatAuthoringSelection(row.Section, row.TrackId, row.PropertyPath));
            RefreshDetail(row);

            bar.style.borderBottomColor = UiColors.SelectionBorder;
            bar.style.borderLeftColor = UiColors.SelectionBorder;
            bar.style.borderRightColor = UiColors.SelectionBorder;
            bar.style.borderTopColor = UiColors.SelectionBorder;
            bar.style.borderBottomWidth = 1;
            bar.style.borderLeftWidth = 1;
            bar.style.borderRightWidth = 1;
            bar.style.borderTopWidth = 1;
            SetTimelineHandleOpacity(data.LeftHandle, data.RightHandle, 1f);
        }

        private void UpdateTimelineDragPreview(int startFrame, int endFrame)
        {
            if (!_timelineDrag.IsDragging)
            {
                return;
            }

            int maxFrame = GetMaxActionFrame();
            startFrame = Clamp(startFrame, 0, maxFrame);
            endFrame = Clamp(endFrame, startFrame, maxFrame);
            bool previewChanged = startFrame != _timelineDrag.PreviewStartFrame || endFrame != _timelineDrag.PreviewEndFrame;
            _timelineDrag.SetPreview(startFrame, endFrame);

            float frameWidth = GetTimelineFrameWidth(_timelineDrag.TotalFrames, _timelineDrag.TrackWidth);
            float x = TimelineStripLabelWidth + FrameToTimelineX(startFrame, _timelineDrag.TotalFrames, _timelineDrag.TrackWidth);
            float width = Math.Max(4f, (endFrame - startFrame + 1) * frameWidth);
            _timelineDrag.Bar.style.left = x;
            _timelineDrag.Bar.style.width = width;
            _timelineDrag.RangeLabel.style.left = x + 5;
            _timelineDrag.RangeLabel.style.width = Math.Max(36f, width - 10f);
            _timelineDrag.RangeLabel.text = startFrame + "-" + endFrame;
            _timelineDrag.Bar.tooltip = BuildTimelineTooltip(_timelineDrag.Row) + "\n预览：" + startFrame + "-" + endFrame;
            if (previewChanged)
            {
                int previewFrame = _timelineDrag.Mode == TimelineDragMode.ResizeEnd ? endFrame : startFrame;
                CombatAuthoringSceneState.SetFrame(previewFrame);
                SceneView.RepaintAll();
            }

            SetTimelineDragStatus(TimelineDragVerb(_timelineDrag.Mode) + "：" + startFrame + "-" + endFrame, UiColors.TextPrimary);
        }

        private void ApplyTimelineRangeToAsset(
            TimelineRow row,
            int rowIndex,
            int nextStart,
            int nextEnd,
            bool recordUndo,
            bool refreshDetail)
        {
            if (_actionAsset == null || _actionSerialized == null)
            {
                return;
            }

            _actionSerialized.Update();
            SerializedProperty property = _actionSerialized.FindProperty(row.PropertyPath);
            if (property == null || !TryGetTimelineRangeProperties(property, row, out SerializedProperty start, out SerializedProperty end))
            {
                return;
            }

            if (recordUndo)
            {
                Undo.RecordObject(_actionAsset, "Drag Combat Timeline Range");
            }

            start.intValue = nextStart;
            end.intValue = nextEnd;
            if (recordUndo)
            {
                _actionSerialized.ApplyModifiedProperties();
            }
            else
            {
                _actionSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();
            UpdateTimelineRowFrameRange(rowIndex, nextStart, nextEnd);
            if (refreshDetail)
            {
                RefreshDetail(rowIndex >= 0 && rowIndex < _timelineRows.Count
                    ? _timelineRows[rowIndex]
                    : (TimelineRow?)null);
            }
        }

        private void FinishTimelineRangeDrag(TimelineRow row, int rowIndex, int nextStart, int nextEnd)
        {
            if (_actionAsset == null || _actionSerialized == null)
            {
                return;
            }

            ApplyTimelineRangeToAsset(row, rowIndex, nextStart, nextEnd, recordUndo: true, refreshDetail: false);
            CombatAuthoringSceneState.NotifyDataChanged();

            RefreshTimeline();
            SelectTimelineRow(FindTimelineRow(row));
            RefreshValidation();
            SceneView.RepaintAll();
            Repaint();
            SetTimelineDragStatus("已提交范围：" + nextStart + "-" + nextEnd, UiColors.TextSecondary);
        }

        private void UpdateTimelineRowFrameRange(int rowIndex, int startFrame, int endFrame)
        {
            if (rowIndex < 0 || rowIndex >= _timelineRows.Count)
            {
                return;
            }

            TimelineRow row = _timelineRows[rowIndex];
            var frameRange = new CombatAuthoringFrameRange(startFrame, endFrame);
            _timelineRows[rowIndex] = new TimelineRow(row.Section, row.Label, row.TrackId, frameRange, row.PropertyPath, row.Kind);
        }

        private int PanelPositionToTimelineFrame(Vector3 panelPosition, int totalFrames, float trackWidth)
        {
            if (_timelineStripContent == null)
            {
                return 0;
            }

            Vector2 contentPosition = _timelineStripContent.WorldToLocal(new Vector2(panelPosition.x, panelPosition.y));
            float frameWidth = GetTimelineFrameWidth(totalFrames, trackWidth);
            float relativeX = contentPosition.x - TimelineStripLabelWidth;
            return Clamp(Mathf.RoundToInt(relativeX / frameWidth), 0, Math.Max(0, totalFrames - 1));
        }

        private static void CalculateTimelineDragRange(
            TimelineDragMode mode,
            int originalStart,
            int originalEnd,
            int pointerStartFrame,
            int targetFrame,
            int maxFrame,
            out int startFrame,
            out int endFrame)
        {
            originalStart = Clamp(originalStart, 0, maxFrame);
            originalEnd = Clamp(originalEnd, originalStart, maxFrame);
            if (mode == TimelineDragMode.Move)
            {
                int length = originalEnd - originalStart;
                int delta = targetFrame - pointerStartFrame;
                startFrame = Clamp(originalStart + delta, 0, Math.Max(0, maxFrame - length));
                endFrame = startFrame + length;
                return;
            }

            if (mode == TimelineDragMode.ResizeStart)
            {
                startFrame = Clamp(targetFrame, 0, originalEnd);
                endFrame = originalEnd;
                return;
            }

            startFrame = originalStart;
            endFrame = Clamp(targetFrame, originalStart, maxFrame);
        }

        private static TimelineDragMode GetTimelineDragMode(float localX, float barWidth)
        {
            float edgeZone = TimelineRangeEdgeHandleWidth + 4f;
            if (barWidth <= edgeZone * 2f)
            {
                edgeZone = barWidth * 0.5f;
            }

            if (localX <= edgeZone)
            {
                return TimelineDragMode.ResizeStart;
            }

            if (localX >= barWidth - edgeZone)
            {
                return TimelineDragMode.ResizeEnd;
            }

            return TimelineDragMode.Move;
        }

        private static TimelineDragMode GetTimelineDragMode(VisualElement bar, Vector3 panelPosition)
        {
            if (bar == null)
            {
                return TimelineDragMode.Move;
            }

            Vector2 localPosition = bar.WorldToLocal(new Vector2(panelPosition.x, panelPosition.y));
            return GetTimelineDragMode(localPosition.x, bar.resolvedStyle.width);
        }

        private static string TimelineDragVerb(TimelineDragMode mode)
        {
            switch (mode)
            {
                case TimelineDragMode.ResizeStart:
                    return "调整 Start";
                case TimelineDragMode.ResizeEnd:
                    return "调整 End";
                default:
                    return "移动范围";
            }
        }

        private static bool TryGetTimelineRangeProperties(SerializedProperty property, TimelineRow row, out SerializedProperty start, out SerializedProperty end)
        {
            SerializedProperty range = string.Equals(row.Section, "Action", StringComparison.Ordinal)
                ? property
                : property.FindPropertyRelative("frameRange");
            start = range?.FindPropertyRelative("startFrame");
            end = range?.FindPropertyRelative("endFrame");
            return start != null && end != null;
        }

        private static bool IsTimelineRangeDraggable(TimelineRow row)
        {
            if (row.FrameRange.IsEmpty)
            {
                return false;
            }

            return string.Equals(row.Section, "Action", StringComparison.Ordinal)
                || string.Equals(row.Section, "Hitbox", StringComparison.Ordinal)
                || string.Equals(row.Section, "Hurtbox", StringComparison.Ordinal);
        }

        private void SetTimelineDragStatus(string text, Color color)
        {
            if (_timelineDragStatus == null)
            {
                return;
            }

            _timelineDragStatus.text = text;
            _timelineDragStatus.style.color = color;
        }

        private void AddShapeRows(string section, CombatShapeAuthoringData[] shapes, string propertyRoot)
        {
            if (shapes == null)
            {
                return;
            }

            for (int i = 0; i < shapes.Length; i++)
            {
                CombatShapeAuthoringData shape = shapes[i];
                _timelineRows.Add(new TimelineRow(
                    section,
                    shape.ShapeKind + " #" + shape.TrackId,
                    shape.TrackId,
                    shape.FrameRange,
                    propertyRoot + ".Array.data[" + i + "]",
                    TimelineRowKind.ActionProperty));
            }
        }

        private void AddTraceRows()
        {
            CombatWeaponTraceAuthoringData[] traces = _actionAsset.WeaponTraces;
            if (traces == null)
            {
                return;
            }

            for (int i = 0; i < traces.Length; i++)
            {
                CombatWeaponTraceAuthoringData trace = traces[i];
                _timelineRows.Add(new TimelineRow(
                    "WeaponTrace",
                    "Trace #" + trace.TraceId,
                    trace.TraceId,
                    trace.FrameRange,
                    "weaponTraces.Array.data[" + i + "]",
                    TimelineRowKind.ActionProperty));
            }
        }

        private void RefreshFrameSlider()
        {
            int maxFrame = Math.Max(0, _actionAsset == null ? 0 : _actionAsset.TotalFrames - 1);
            int frame = CombatAuthoringSceneState.Frame;
            if (_frameSlider != null)
            {
                _frameSlider.highValue = maxFrame;
                if (_frameSlider.value != frame)
                {
                    _frameSlider.SetValueWithoutNotify(frame);
                }
            }

            if (_frameLabel != null)
            {
                _frameLabel.text = "当前帧 " + frame;
            }

            CombatAuthoringSceneState.SetFrame(frame);
        }

    }
}
