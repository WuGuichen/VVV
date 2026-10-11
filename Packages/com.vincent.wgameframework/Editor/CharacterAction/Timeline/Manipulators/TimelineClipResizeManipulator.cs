using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline.Manipulators
{
    internal enum TimelineClipDragMode
    {
        Move,
        ResizeStart,
        ResizeEnd,
    }

    internal sealed class TimelineClipResizeManipulator : MouseManipulator
    {
        private readonly CharacterActionTimelineView _view;
        private readonly ITimelineItemAdapter _adapter;
        private readonly TimelineClipDragMode _mode;
        private bool _active;
        private bool _gestureStarted;
        private float _grabOffsetX;

        public TimelineClipResizeManipulator(
            CharacterActionTimelineView view,
            ITimelineItemAdapter adapter,
            TimelineClipDragMode mode)
        {
            _view = view;
            _adapter = adapter;
            _mode = mode;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOut);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || !_adapter.CanEdit || _view.Snapshot.ReadOnly)
                return;

            _active = true;
            _gestureStarted = false;
            float localX = _view.CanvasWorldToLocalX(evt.mousePosition.x);
            _grabOffsetX = ResolveGrabOffsetX(localX);
            _view.SelectItem(_adapter.Id);
            target.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_active)
                return;

            float localX = _view.CanvasWorldToLocalX(evt.mousePosition.x);
            float effectiveX = localX - _grabOffsetX;
            int actionDuration = _view.Snapshot.ActionDurationFrames;
            bool allowsTailBeyondAction =
                _adapter is IVfxTimelineClipAdapter vfxAdapter
                && vfxAdapter.AllowsTailBeyondAction;
            int nextStart = _adapter.StartFrame;
            int nextEnd = _adapter.EndFrame;

            switch (_mode)
            {
                case TimelineClipDragMode.ResizeStart:
                {
                    nextEnd = _adapter.EndFrame;
                    nextStart = CharacterActionTimelineLayout.ClampFrame(
                        _view.PixelToFrame(effectiveX),
                        actionDuration);
                    if (nextStart > nextEnd)
                        nextStart = nextEnd;
                    break;
                }
                case TimelineClipDragMode.ResizeEnd:
                {
                    nextStart = _adapter.StartFrame;
                    nextEnd = allowsTailBeyondAction
                        ? Math.Max(0, _view.ClipEndXToFrame(effectiveX))
                        : CharacterActionTimelineLayout.ClampFrame(
                            _view.ClipEndXToFrame(effectiveX),
                            actionDuration);
                    if (nextEnd < nextStart)
                        nextEnd = nextStart;
                    break;
                }
                case TimelineClipDragMode.Move:
                {
                    int length = _adapter.EndFrame - _adapter.StartFrame;
                    nextStart = CharacterActionTimelineLayout.ClampFrame(
                        _view.PixelToFrame(effectiveX),
                        actionDuration);
                    nextEnd = allowsTailBeyondAction
                        ? nextStart + length
                        : CharacterActionTimelineLayout.ClampFrame(nextStart + length, actionDuration);
                    nextStart = Math.Max(0, nextEnd - length);
                    break;
                }
            }

            if (nextStart == _adapter.StartFrame && nextEnd == _adapter.EndFrame)
            {
                evt.StopPropagation();
                return;
            }

            if (!_gestureStarted)
            {
                _view.BeginGesture();
                _gestureStarted = true;
            }

            if (_adapter is IAnimationTimelineClipAdapter editableAnimation)
            {
                editableAnimation.SetAnimationRange(
                    nextStart,
                    nextEnd,
                    evt.altKey,
                    actionDuration,
                    _view.CharacterStableId);
            }
            else if (_adapter is IVfxTimelineClipAdapter editableVfx)
                editableVfx.SetVfxRange(nextStart, nextEnd, evt.altKey);
            else
                _adapter.SetRange(nextStart, nextEnd);
            _view.RefreshItemPresentation(_adapter);
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (!_active || evt.button != 0)
                return;

            FinishGesture();
            evt.StopPropagation();
        }

        private void OnMouseCaptureOut(MouseCaptureOutEvent evt)
        {
            if (_active)
                FinishGesture();
        }

        private void FinishGesture()
        {
            _active = false;
            bool changed = _gestureStarted;
            _gestureStarted = false;
            target.ReleaseMouse();
            if (changed)
                _view.EndGesture();
        }

        private float ResolveGrabOffsetX(float localX)
        {
            switch (_mode)
            {
                case TimelineClipDragMode.ResizeStart:
                    return localX - _view.FrameStartX(_adapter.StartFrame);
                case TimelineClipDragMode.ResizeEnd:
                    return localX - _view.ClipEndX(_adapter.EndFrame);
                case TimelineClipDragMode.Move:
                default:
                    return localX - _view.FrameStartX(_adapter.StartFrame);
            }
        }
    }
}
