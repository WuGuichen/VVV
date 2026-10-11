using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline.Manipulators
{
    internal sealed class TimelinePlayheadManipulator : MouseManipulator
    {
        private readonly CharacterActionTimelineView _view;
        private readonly VisualElement _handle;
        private bool _active;

        public TimelinePlayheadManipulator(
            CharacterActionTimelineView view,
            VisualElement handle)
        {
            _view = view;
            _handle = handle;
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
            if (evt.button != 0)
                return;
            _active = true;
            _view.BeginPlayheadScrub();
            _handle.CaptureMouse();
            SeekFromEvent(evt);
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_active)
                return;

            SeekFromEvent(evt);
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (!_active || evt.button != 0)
                return;

            _active = false;
            _handle.ReleaseMouse();
            _view.EndPlayheadScrub();
            evt.StopPropagation();
        }

        private void OnMouseCaptureOut(MouseCaptureOutEvent evt)
        {
            if (!_active)
                return;
            _active = false;
            _view.EndPlayheadScrub();
        }

        private void SeekFromEvent(IMouseEvent evt)
        {
            _view.SeekPlayheadFromMouse(evt);
        }
    }
}
