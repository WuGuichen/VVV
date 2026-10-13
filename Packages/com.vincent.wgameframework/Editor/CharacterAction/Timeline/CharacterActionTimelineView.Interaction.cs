using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed partial class CharacterActionTimelineView
    {
        public void RefreshAfterGesture()
        {
            if (_snapshot?.Action == null)
                return;
            CharacterActionTimelineSnapshot next = CharacterActionTimelineSnapshot.FromAction(
                _snapshot.Action, _host?.DefaultDurationFrames ?? 8);
            bool extentChanged = next.DurationFrames != _snapshot.DurationFrames;
            _snapshot = next;
            if (extentChanged)
            {
                // VFX tails can extend the ruler; ordinary moves retain all hit targets.
                RebuildCanvas(preserveScroll: true);
                return;
            }
            for (int i = 0; i < _snapshot.Tracks.Count; i++)
                RefreshTrackPresentation(_snapshot.Tracks[i].Kind);
        }

        public void RefreshItemPresentation(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || _snapshot == null)
                return;

            ITimelineItemAdapter adapter = _snapshot.FindItem(itemId);
            RefreshItemPresentation(adapter);
        }

        public void RefreshItemPresentation(ITimelineItemAdapter adapter)
        {
            if (adapter == null || !_itemElements.TryGetValue(adapter.Id, out VisualElement element))
                return;

            if (adapter is ControlScaleTimelineAdapter control && _rowCanvases.TryGetValue(adapter.TrackKind, out var controlRow))
                BuildControlScaleBand(controlRow, control.Rotation);

            // Keep the grabbed item in its lane until the host commits the gesture.
            // Phase edits also move adjacent phases, so they still refresh together.
            if (_isItemGestureActive && adapter.TrackKind != TimelineTrackKind.Phases)
            {
                RefreshSingleItemPresentation(adapter, element, refreshText: false);
                UpdateSelectionFrameGuide(refreshTooltip: false);
                return;
            }

            RefreshTrackPresentation(adapter.TrackKind);
        }

        private void RefreshTrackPresentation(TimelineTrackKind trackKind)
        {
            if (_snapshot == null)
                return;

            TimelineTrackDescriptor track = null;
            for (int i = 0; i < _snapshot.Tracks.Count; i++)
            {
                if (_snapshot.Tracks[i].Kind == trackKind)
                {
                    track = _snapshot.Tracks[i];
                    break;
                }
            }

            IReadOnlyList<ITimelineItemAdapter> items =
                _snapshot.ItemsByTrack.TryGetValue(trackKind, out IReadOnlyList<ITimelineItemAdapter> found)
                    ? found
                    : Array.Empty<ITimelineItemAdapter>();
            _trackLayouts[trackKind] = CharacterActionTimelineLaneLayout.Build(
                trackKind,
                track != null && track.IsEnabled ? items : Array.Empty<ITimelineItemAdapter>(),
                _state.PixelsPerFrame);

            float rowHeight = ResolveTrackRowHeight(track);
            if (_rowCanvases.TryGetValue(trackKind, out VisualElement row))
                row.style.height = rowHeight;
            if (_labelRows.TryGetValue(trackKind, out VisualElement labelRow))
                labelRow.style.height = rowHeight;

            for (int i = 0; i < items.Count; i++)
            {
                ITimelineItemAdapter adapter = items[i];
                if (adapter != null && _itemElements.TryGetValue(adapter.Id, out VisualElement element))
                    RefreshSingleItemPresentation(adapter, element);
            }

            UpdatePlayhead();
            UpdateSelectionFrameGuide();
        }

        private void RefreshSingleItemPresentation(
            ITimelineItemAdapter adapter,
            VisualElement element,
            bool refreshText = true)
        {
            if (refreshText)
            {
                string fullTitle = adapter.FullTitle;
                element.tooltip = fullTitle;

                Label clipLabel = element.Q<Label>(className: "ca-tl-clip-label");
                if (clipLabel != null)
                    clipLabel.text = adapter.Title;

                Label tooltipBubble = element.Q<Label>(className: "ca-tl-tooltip-bubble");
                if (tooltipBubble != null)
                    ApplyTooltipBubbleText(tooltipBubble, fullTitle);
            }
            ApplyItemGeometry(element, adapter);

            if (adapter is AnimationTrackTimelineAdapter animationAdapter)
            {
                VisualElement transition = element.Q(className: "ca-tl-anim-transition");
                if (transition != null)
                    ApplyAnimationTransitionGeometry(transition, animationAdapter);
            }
        }

        public void UpdateItemLayout(ITimelineItemAdapter adapter)
        {
            RefreshItemPresentation(adapter);
        }

        private void AttachRowContextMenu(VisualElement row, TimelineTrackDescriptor track, bool isRuler = false)
        {
            row.focusable = true;
            row.AddManipulator(new ContextualMenuManipulator(evt => PopulateRowContextMenu(evt, track, isRuler)));
        }

        private void AttachItemContextMenu(VisualElement element, ITimelineItemAdapter adapter)
        {
            element.focusable = true;
            AttachItemContextMenuHitTarget(element, adapter);
        }

        private void AttachItemContextMenuHitTarget(VisualElement element, ITimelineItemAdapter adapter)
        {
            if (element == null)
                return;

            element.AddManipulator(new ContextualMenuManipulator(
                evt =>
                {
                    PopulateItemContextMenu(evt, adapter);
                    evt.StopPropagation();
                }));
        }

        private void PopulateRowContextMenu(ContextualMenuPopulateEvent evt, TimelineTrackDescriptor track, bool isRuler)
        {
            if (_snapshot == null)
                return;

            int frame = ContextFrameFromEvent(evt);
            bool canEdit = !_snapshot.ReadOnly;
            bool characterActionMode = _snapshot.Action != null;

            if (characterActionMode && track != null &&
                (track.Kind == TimelineTrackKind.MovementControl || track.Kind == TimelineTrackKind.RotationControl))
            {
                evt.menu.AppendAction("插入倍率标记", _ => AddEventAtTrack(track.Kind, frame), MenuStatus(canEdit));
                return;
            }

            if (!characterActionMode)
            {
                evt.menu.AppendAction(
                    "Combat/AreaPulse",
                    _ => AddEventAtTrack(TimelineTrackKind.Combat, frame),
                    isRuler ? MenuStatus(canEdit) : CanAddEventAt(track, TimelineTrackKind.Combat, canEdit));
                evt.menu.AppendAction(
                    "Gameplay/Event",
                    _ => AddEventAtTrack(TimelineTrackKind.Gameplay, frame),
                    isRuler ? MenuStatus(canEdit) : CanAddEventAt(track, TimelineTrackKind.Gameplay, canEdit));
                evt.menu.AppendAction(
                    "Presentation/Event",
                    _ => AddEventAtTrack(TimelineTrackKind.Presentation, frame),
                    isRuler ? MenuStatus(canEdit) : CanAddEventAt(track, TimelineTrackKind.Presentation, canEdit));
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Fit to Width", _ => FitToWidth(), DropdownMenuAction.AlwaysEnabled);
                return;
            }

            evt.menu.AppendAction(
                "Add Phase",
                _ => AddEventAtTrack(TimelineTrackKind.Phases, frame),
                characterActionMode && canEdit && (isRuler || track.Kind == TimelineTrackKind.Phases)
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction(
                "Add Transition Window",
                _ => AddEventAtTrack(TimelineTrackKind.TransitionWindow, frame),
                characterActionMode && isRuler
                    ? MenuStatus(canEdit)
                    : CanAddEventAt(track, TimelineTrackKind.TransitionWindow, characterActionMode && canEdit));

            evt.menu.AppendAction(
                "Add Motion Event",
                _ => AddEventAtTrack(TimelineTrackKind.Motion, frame),
                characterActionMode && isRuler ? MenuStatus(canEdit) : CanAddEventAt(track, TimelineTrackKind.Motion, characterActionMode && canEdit));
            evt.menu.AppendAction(
                "Add RootMotion",
                _ => AddRootMotionAtFrame(frame),
                CanAddRootMotionAt(track, isRuler, characterActionMode && canEdit));
            evt.menu.AppendAction(
                "Add Animation Event",
                _ => AddEventAtTrack(TimelineTrackKind.Animation, frame),
                characterActionMode && isRuler ? MenuStatus(canEdit) : CanAddEventAt(track, TimelineTrackKind.Animation, characterActionMode && canEdit));
            evt.menu.AppendAction(
                "Add Combat Event",
                _ => AddEventAtTrack(TimelineTrackKind.Combat, frame),
                isRuler ? MenuStatus(canEdit) : CanAddEventAt(track, TimelineTrackKind.Combat, canEdit));
            evt.menu.AppendAction(
                "Add Hit Trace Window",
                _ => AddHitTraceWindowAtFrame(frame),
                characterActionMode && isRuler ? MenuStatus(canEdit) : CanAddEventAt(track, TimelineTrackKind.Combat, characterActionMode && canEdit));
            evt.menu.AppendAction(
                "Add Gameplay Event",
                _ => AddEventAtTrack(TimelineTrackKind.Gameplay, frame),
                isRuler ? MenuStatus(canEdit) : CanAddEventAt(track, TimelineTrackKind.Gameplay, canEdit));
            evt.menu.AppendAction(
                "Add Presentation Event",
                _ => AddEventAtTrack(TimelineTrackKind.Presentation, frame),
                isRuler ? MenuStatus(canEdit) : CanAddEventAt(track, TimelineTrackKind.Presentation, canEdit));

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Fit to Width", _ => FitToWidth(), DropdownMenuAction.AlwaysEnabled);
        }

        private void PopulateItemContextMenu(ContextualMenuPopulateEvent evt, ITimelineItemAdapter adapter)
        {
            if (_snapshot == null || adapter == null)
                return;

            bool canEdit = adapter.CanEdit && !_snapshot.ReadOnly;
            bool canRemove = !_snapshot.ReadOnly && (adapter.CanEdit || adapter is RootMotionTimelineAdapter);

            evt.menu.AppendAction(
                "Delete",
                _ =>
                {
                    string targetId = adapter.Id;
                    _host?.CommitStructural(() => RemoveItemById(targetId));
                    if (_state.SelectedItemId == targetId)
                    {
                        _state.SelectedItemId = string.Empty;
                        _host?.SelectionChanged?.Invoke(string.Empty);
                    }
                },
                canRemove ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction(
                "Select",
                _ =>
                {
                    SelectItem(adapter.Id);
                    Focus();
                },
                DropdownMenuAction.AlwaysEnabled);

            bool canFitActionDuration = CanFitActionDurationToItem(adapter);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction(
                "将动作长度匹配到此事件末尾",
                _ => FitActionDurationToItem(adapter),
                canFitActionDuration
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);

            if (adapter is AnimationTrackTimelineAdapter animationAdapter)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction(
                    "将动作时长延长到动画完整尾帧",
                    _ => ExtendActionDurationToFullAnimation(animationAdapter),
                    TryResolveFullAnimationDuration(animationAdapter, out _, out _)
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction(
                    "Recalculate Animation Span",
                    _ => RecalculateAnimationSpan(animationAdapter),
                    canEdit ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction(
                    "Fit EndFrame to Clip",
                    _ => FitAnimationEndToClip(animationAdapter),
                    canEdit ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction(
                    "Fit EndFrame to Action Duration",
                    _ => FitAnimationEndToActionDuration(animationAdapter),
                    canEdit ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
        }

        internal void FitActionDurationToItem(ITimelineItemAdapter adapter)
        {
            if (!CanFitActionDurationToItem(adapter))
                return;

            int durationFrames = adapter.EndFrame >= int.MaxValue
                ? int.MaxValue
                : adapter.EndFrame + 1;
            ApplyDurationFramesFromInput(durationFrames);
        }

        private bool TryResolveFullAnimationDuration(
            AnimationTrackTimelineAdapter adapter,
            out int durationFrames,
            out int sourceFrames)
        {
            durationFrames = 0;
            sourceFrames = 0;
            if (_snapshot?.Action == null || _snapshot.ReadOnly || adapter?.CanEdit != true)
                return false;

            AnimationTrackEventJsonDto animationEvent = adapter.GetEvent();
            if (animationEvent == null || animationEvent.Frame < 0
                || !CharacterActionAnimationTimelineResolver.TryResolveSourceClipFrames(
                    animationEvent, _host?.CharacterStableId ?? string.Empty, out sourceFrames))
            {
                return false;
            }

            int startOffset = Mathf.Clamp(animationEvent.AnimationStartOffsetFrames, 0, sourceFrames - 1);
            int endOffset = Mathf.Clamp(animationEvent.AnimationEndOffsetFrames, 0, sourceFrames - startOffset - 1);
            int playableFrames = sourceFrames - startOffset - endOffset;
            int span = CharacterActionAnimationTimelineResolver.ComputeTimelineSpanFrames(
                playableFrames, animationEvent.PlaybackSpeed);
            long requiredDuration = (long)animationEvent.Frame + span;
            if (requiredDuration <= _snapshot.ActionDurationFrames || requiredDuration > int.MaxValue)
                return false;

            durationFrames = (int)requiredDuration;
            return true;
        }

        private void ExtendActionDurationToFullAnimation(AnimationTrackTimelineAdapter adapter)
        {
            if (!TryResolveFullAnimationDuration(adapter, out int durationFrames, out int sourceFrames)
                || _host?.CommitFieldChange == null)
            {
                return;
            }

            CharacterActionJsonDto action = _snapshot.Action;
            AnimationTrackEventJsonDto animationEvent = adapter.GetEvent();
            _host.CommitField(() =>
            {
                action.DurationFrames = durationFrames;
                CharacterActionPhaseTimelineUtility.NormalizePhases(action, durationFrames);
                CharacterActionAnimationTimelineResolver.NormalizeOffsets(animationEvent, sourceFrames);
                animationEvent.EndFrame = durationFrames - 1;
            }, true);
            RebindSnapshotForDuration(action, durationFrames);
        }

        private bool CanFitActionDurationToItem(ITimelineItemAdapter adapter)
        {
            if (_snapshot?.Action == null
                || _snapshot.ReadOnly
                || adapter == null
                || !adapter.CanEdit
                || adapter is RootMotionTimelineAdapter
                || adapter.TrackKind == TimelineTrackKind.Phases
                || adapter.TrackKind == TimelineTrackKind.TransitionWindow)
            {
                return false;
            }

            int durationFrames = adapter.EndFrame >= int.MaxValue
                ? int.MaxValue
                : adapter.EndFrame + 1;
            return durationFrames != _snapshot.ActionDurationFrames;
        }

        private bool RemoveItemById(string itemId)
        {
            ITimelineItemAdapter current = _snapshot?.FindItem(itemId);
            if (current == null || (!current.CanEdit && !(current is RootMotionTimelineAdapter)))
                return false;

            current.Remove();
            return true;
        }

        private static DropdownMenuAction.Status MenuStatus(bool enabled)
        {
            return enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
        }

        private static DropdownMenuAction.Status CanAddEventAt(
            TimelineTrackDescriptor rowTrack,
            TimelineTrackKind targetKind,
            bool canEdit)
        {
            if (!canEdit || rowTrack == null || rowTrack.Kind != targetKind)
                return DropdownMenuAction.Status.Disabled;
            return DropdownMenuAction.Status.Normal;
        }

        private DropdownMenuAction.Status CanAddRootMotionAt(
            TimelineTrackDescriptor rowTrack,
            bool isRuler,
            bool canEdit)
        {
            if (!canEdit || _snapshot?.Action?.MotionTrack?.RootMotion != null)
                return DropdownMenuAction.Status.Disabled;
            if (isRuler)
                return DropdownMenuAction.Status.Normal;
            return rowTrack != null && rowTrack.Kind == TimelineTrackKind.Motion
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled;
        }

        private int ContextFrameFromEvent(ContextualMenuPopulateEvent evt)
        {
            if (_snapshot == null)
                return 0;

            float localX = CanvasWorldToLocalX(evt.mousePosition.x);
            return CharacterActionTimelineLayout.ClampFrame(PixelToFrame(localX), _snapshot.ActionDurationFrames);
        }

        private void RecalculateAnimationSpan(AnimationTrackTimelineAdapter adapter)
        {
            if (_snapshot?.Action == null || adapter == null)
                return;

            _host?.CommitField(() =>
            {
                AnimationTrackEventJsonDto evt = adapter.GetEvent();
                CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                    evt,
                    _snapshot.Action,
                    _host.DefaultDurationFrames,
                    _host.CharacterStableId);
            });
            RefreshItemPresentation(adapter);
            _host?.RequestTracksRefresh?.Invoke();
        }

        private void FitAnimationEndToClip(AnimationTrackTimelineAdapter adapter)
        {
            if (_snapshot?.Action == null || adapter == null)
                return;

            _host?.CommitField(() =>
            {
                CharacterActionAnimationTimelineResolver.FitEndToResolvedClip(
                    adapter.GetEvent(),
                    _snapshot.Action,
                    _host.DefaultDurationFrames,
                    _host.CharacterStableId);
            });
            RefreshItemPresentation(adapter);
            _host?.RequestTracksRefresh?.Invoke();
        }

        private void FitAnimationEndToActionDuration(AnimationTrackTimelineAdapter adapter)
        {
            if (_snapshot?.Action == null || adapter == null)
                return;

            _host?.CommitField(() =>
            {
                CharacterActionAnimationTimelineResolver.FitEndToActionDuration(
                    adapter.GetEvent(),
                    _snapshot.Action,
                    _host.DefaultDurationFrames,
                    _host.CharacterStableId);
            });
            RefreshItemPresentation(adapter);
            _host?.RequestTracksRefresh?.Invoke();
        }

        private void HandleAnimationShortcut(KeyDownEvent evt)
        {
            if (_snapshot == null || _snapshot.ReadOnly)
                return;

            if (string.IsNullOrEmpty(_state.SelectedItemId))
                return;

            ITimelineItemAdapter adapter = _snapshot.FindItem(_state.SelectedItemId);
            var animationAdapter = adapter as AnimationTrackTimelineAdapter;
            if (animationAdapter == null)
                return;

            bool ctrl = evt.ctrlKey || evt.commandKey;
            if (!ctrl || !evt.shiftKey)
                return;

            if (evt.keyCode == KeyCode.RightBracket)
            {
                FitAnimationEndToClip(animationAdapter);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.LeftBracket)
            {
                FitAnimationEndToActionDuration(animationAdapter);
                evt.StopPropagation();
            }
        }

        private void RefreshSelectionHighlight()
        {
            foreach (KeyValuePair<string, VisualElement> pair in _itemElements)
            {
                bool selected = pair.Key == _state.SelectedItemId;
                pair.Value.EnableInClassList("ca-tl-item-selected", selected);
            }

            UpdateSelectionFrameGuide();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape && IsItemPickerOpen())
            {
                CloseItemPicker();
                evt.StopPropagation();
                return;
            }

            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.K)
            {
                if (_host?.PickerItemsProvider != null && _host.PickerItemSelected != null)
                {
                    if (IsItemPickerOpen())
                        CloseItemPicker();
                    else
                        OpenItemPicker();
                    evt.StopPropagation();
                    return;
                }
            }

            HandleAnimationShortcut(evt);

            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
                return;

            if (string.IsNullOrEmpty(_state.SelectedItemId) || _snapshot == null || _snapshot.ReadOnly)
                return;

            ITimelineItemAdapter adapter = _snapshot.FindItem(_state.SelectedItemId);
            if (adapter == null || (!adapter.CanEdit && !(adapter is RootMotionTimelineAdapter)))
                return;

            string removedId = _state.SelectedItemId;
            _host?.CommitStructural(() => RemoveItemById(removedId));
            _state.SelectedItemId = string.Empty;
            _host?.SelectionChanged?.Invoke(string.Empty);
            evt.StopPropagation();
        }
    }
}
