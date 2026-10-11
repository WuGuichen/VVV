using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed partial class CharacterActionTimelineView
    {
        private VisualElement BuildViewportControl()
        {
            var root = new VisualElement();
            root.AddToClassList("ca-tl-viewport");

            var spacer = new VisualElement();
            spacer.AddToClassList("ca-tl-viewport-spacer");
            spacer.style.width = CharacterActionTimelineLayout.LabelColumnWidth;
            spacer.style.flexShrink = 0;
            root.Add(spacer);

            _viewportSlider = new MinMaxSlider(0f, 1f, 0f, 1f)
            {
                tooltip = "拖动两端调整缩放，拖动区间调整时间轴视图",
            };
            _viewportSlider.AddToClassList("ca-tl-viewport-slider");
            _viewportSlider.RegisterCallback<PointerDownEvent>(BeginViewportRangeInteraction, TrickleDown.TrickleDown);
            _viewportSlider.RegisterCallback<PointerUpEvent>(EndViewportRangeInteraction, TrickleDown.TrickleDown);
            _viewportSlider.RegisterCallback<PointerCancelEvent>(EndViewportRangeInteraction, TrickleDown.TrickleDown);
            _viewportSlider.RegisterCallback<PointerCaptureOutEvent>(EndViewportRangeInteraction, TrickleDown.TrickleDown);
            _viewportSlider.RegisterValueChangedCallback(OnViewportRangeChanged);
            root.Add(_viewportSlider);

            _viewportLabel = new Label("12 px/f");
            _viewportLabel.AddToClassList("ca-tl-viewport-label");
            root.Add(_viewportLabel);

            return root;
        }

        private void OnViewportRangeChanged(ChangeEvent<Vector2> evt)
        {
            if (_isApplyingViewportRange
                || !_isInteractingWithViewportRange
                || evt == null
                || _snapshot == null)
            {
                return;
            }

            ApplyViewportRange(evt.newValue);
        }

        private void BeginViewportRangeInteraction(PointerDownEvent evt)
        {
            if (evt == null || evt.button != 0)
                return;

            _isInteractingWithViewportRange = true;
        }

        private void EndViewportRangeInteraction(PointerUpEvent evt)
        {
            EndViewportRangeInteraction();
        }

        private void EndViewportRangeInteraction(PointerCancelEvent evt)
        {
            EndViewportRangeInteraction();
        }

        private void EndViewportRangeInteraction(PointerCaptureOutEvent evt)
        {
            EndViewportRangeInteraction();
        }

        private void EndViewportRangeInteraction()
        {
            _isInteractingWithViewportRange = false;
            UpdateViewportControl();
        }

        private void ApplyViewportRange(Vector2 range)
        {
            if (_snapshot == null || _scrollView == null)
                return;

            int duration = Math.Max(1, _snapshot.DurationFrames);
            float controlLimit = GetViewportControlFrameLimit(duration);
            float startFrame = Mathf.Clamp(range.x, 0f, Math.Max(0f, controlLimit - 1f));
            float endFrame = Mathf.Clamp(range.y, startFrame + 1f, controlLimit);
            float visibleFrames = Math.Max(1f, endFrame - startFrame);
            float viewportWidth = GetViewportTrackWidth();
            float nextPixelsPerFrame = Mathf.Clamp(
                viewportWidth / visibleFrames,
                CharacterActionTimelineLayout.MinPixelsPerFrame,
                CharacterActionTimelineLayout.MaxPixelsPerFrame);

            _state.PixelsPerFrame = nextPixelsPerFrame;
            RebuildCanvas(updateViewport: false);

            float nextScrollX = CharacterActionTimelineLayout.ContentPadding
                + startFrame * _state.PixelsPerFrame;
            _scrollView.schedule.Execute(() =>
            {
                SetScrollX(nextScrollX);
                UpdateViewportControl();
            });
        }

        private void UpdateViewportControl()
        {
            if (_viewportSlider == null || _snapshot == null)
                return;

            int duration = Math.Max(1, _snapshot.DurationFrames);
            _state.PixelsPerFrame = Mathf.Clamp(
                _state.PixelsPerFrame,
                CharacterActionTimelineLayout.MinPixelsPerFrame,
                CharacterActionTimelineLayout.MaxPixelsPerFrame);

            float viewportWidth = GetViewportTrackWidth();
            float controlLimit = GetViewportControlFrameLimit(duration);
            float visibleFrames = Mathf.Clamp(viewportWidth / Math.Max(1f, _state.PixelsPerFrame), 1f, controlLimit);
            float scrollFrame = _scrollView == null
                ? 0f
                : (_scrollView.scrollOffset.x - CharacterActionTimelineLayout.ContentPadding)
                    / Math.Max(1f, _state.PixelsPerFrame);
            float startFrame = Mathf.Clamp(scrollFrame, 0f, Math.Max(0f, controlLimit - visibleFrames));
            float endFrame = Mathf.Min(controlLimit, startFrame + visibleFrames);

            _isApplyingViewportRange = true;
            _viewportSlider.lowLimit = 0f;
            _viewportSlider.highLimit = controlLimit;
            _viewportSlider.SetValueWithoutNotify(new Vector2(startFrame, endFrame));
            _isApplyingViewportRange = false;

            if (_viewportLabel != null)
            {
                _viewportLabel.text = _state.PixelsPerFrame.ToString("0.#")
                    + " px/f  "
                    + Mathf.FloorToInt(startFrame)
                    + "-"
                    + Mathf.CeilToInt(endFrame);
            }
        }

        private float GetViewportTrackWidth()
        {
            if (_scrollView == null)
                return 1f;

            float width = _scrollView.contentViewport != null
                ? _scrollView.contentViewport.resolvedStyle.width
                : _scrollView.resolvedStyle.width;
            return Math.Max(1f, width);
        }

        private float GetViewportControlFrameLimit(int durationFrames)
        {
            return Math.Max(1, durationFrames);
        }

        private void SetScrollX(float scrollX)
        {
            if (_scrollView == null || _snapshot == null)
                return;

            float contentWidth = CharacterActionTimelineLayout.ContentWidth(
                _snapshot.DurationFrames,
                _state.PixelsPerFrame);
            float maxScroll = Math.Max(0f, contentWidth - GetViewportTrackWidth());
            float clamped = Mathf.Clamp(scrollX, 0f, maxScroll);
            _scrollView.scrollOffset = new Vector2(clamped, _scrollView.scrollOffset.y);
        }
    }
}
