using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline.Manipulators
{
    internal sealed class TimelineMarkerDragManipulator : MouseManipulator
    {
        private readonly CharacterActionTimelineView _view;
        private readonly ITimelineItemAdapter _adapter;
        private readonly VisualElement _target;
        private bool _active;
        private bool _gestureStarted;
        private float _grabOffsetX;

        public TimelineMarkerDragManipulator(CharacterActionTimelineView view, ITimelineItemAdapter adapter, VisualElement target)
        {
            _view = view;
            _adapter = adapter;
            _target = target;
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
            _grabOffsetX = localX - _view.MarkerCenterX(_adapter.StartFrame);
            _view.SelectItem(_adapter.Id);
            _target.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_active)
                return;

            float localX = _view.CanvasWorldToLocalX(evt.mousePosition.x);
            float markerCenterX = localX - _grabOffsetX;
            int frame = CharacterActionTimelineLayout.ClampFrame(
                _view.MarkerCenterXToFrame(markerCenterX),
                _view.Snapshot.ActionDurationFrames);
            if (frame == _adapter.StartFrame)
            {
                evt.StopPropagation();
                return;
            }

            if (!_gestureStarted)
            {
                _view.BeginGesture();
                _gestureStarted = true;
            }

            _adapter.SetRange(frame, frame);
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
            _target.ReleaseMouse();
            if (changed)
                _view.EndGesture();
        }
    }
}
