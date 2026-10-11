using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline.Manipulators
{
    internal sealed class TimelinePanZoomManipulator : Manipulator
    {
        private readonly CharacterActionTimelineView _view;
        private readonly ScrollView _scrollView;
        private bool _panning;
        private Vector2 _panStart;

        public TimelinePanZoomManipulator(CharacterActionTimelineView view, ScrollView scrollView)
        {
            _view = view;
            _scrollView = scrollView;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<WheelEvent>(OnWheel);
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
        }

        private void OnWheel(WheelEvent evt)
        {
            if (evt.altKey)
            {
                float delta = evt.delta.y > 0 ? -1f : 1f;
                _view.Zoom(delta * 1.5f);
                evt.StopPropagation();
                return;
            }

            ScrollView verticalScroll = FindVerticalScrollTarget();
            if (verticalScroll == null)
            {
                evt.StopPropagation();
                return;
            }

            Vector2 offset = verticalScroll.scrollOffset;
            float nextY = Mathf.Max(0f, offset.y + evt.delta.y * CharacterActionTimelineLayout.RowHeight);
            verticalScroll.scrollOffset = new Vector2(offset.x, nextY);

            evt.StopPropagation();
        }

        private ScrollView FindVerticalScrollTarget()
        {
            VisualElement current = _scrollView;
            while (current != null)
            {
                if (current is ScrollView scrollView
                    && scrollView != _scrollView
                    && CanScrollVertically(scrollView))
                {
                    return scrollView;
                }

                current = current.parent;
            }

            return null;
        }

        private static bool CanScrollVertically(ScrollView scrollView)
        {
            if (scrollView == null
                || scrollView.verticalScrollerVisibility == ScrollerVisibility.Hidden
                || scrollView.contentContainer == null
                || scrollView.contentViewport == null)
            {
                return false;
            }

            return scrollView.contentContainer.worldBound.height
                > scrollView.contentViewport.worldBound.height + 0.5f;
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != (int)MouseButton.MiddleMouse
                && !(evt.button == (int)MouseButton.LeftMouse && evt.altKey))
            {
                return;
            }

            if (evt.target is VisualElement ve && ve.ClassListContains("ca-tl-canvas-row"))
            {
                _panning = true;
                _panStart = evt.mousePosition;
                target.CaptureMouse();
                evt.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_panning)
                return;

            Vector2 delta = evt.mousePosition - _panStart;
            _panStart = evt.mousePosition;
            _scrollView.scrollOffset += new Vector2(-delta.x, 0f);
            _view.NotifyViewportChanged();
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (!_panning)
                return;

            _panning = false;
            target.ReleaseMouse();
            evt.StopPropagation();
        }
    }
}
