using System;
using System.Collections.Generic;
using MxFramework.Editor.CharacterAction.Timeline.Manipulators;
using MxFramework.Editor.CharacterAction.Timeline.Preview;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed partial class CharacterActionTimelineView
    {
        private CharacterActionTimelinePlaybackController _playbackController;
        private VisualElement _playheadHandle;
        private TimelinePlayheadManipulator _playheadManipulator;
        private Button _playPauseButton;
        private Button _stopButton;
        private Button _speedButton;
        private Label _frameLabel;
        internal event Action<int> PlayheadScrubEnded;
        internal bool IsPlayheadScrubbing { get; private set; }

        public CharacterActionTimelinePlaybackController PlaybackController => _playbackController;

        public void SetPlaybackController(CharacterActionTimelinePlaybackController controller)
        {
            if (_playbackController != null)
                _playbackController.FrameChanged -= OnPlaybackFrameChanged;

            _playbackController = controller;
            _playback = controller;

            if (_playbackController != null)
                _playbackController.FrameChanged += OnPlaybackFrameChanged;

            UpdatePlaybackChrome();
            UpdatePlayhead();
            RefreshPlayheadHighlight();
        }

        private void OnPlaybackFrameChanged(int frame)
        {
            _state.PlayheadFrame = frame;
            UpdatePlayhead();
            UpdateFrameLabel();
            RefreshPlayheadHighlight();
        }

        private void UpdatePlaybackChrome()
        {
            bool hasPlayback = _playbackController != null;
            if (_playPauseButton != null)
            {
                _playPauseButton.SetEnabled(hasPlayback);
                UpdatePlayPauseButtonText();
            }

            if (_stopButton != null)
                _stopButton.SetEnabled(hasPlayback);

            if (_speedButton != null)
                _speedButton.SetEnabled(hasPlayback);

            UpdateFrameLabel();
        }

        private void UpdatePlayPauseButtonText()
        {
            if (_playPauseButton == null || _playbackController == null)
                return;

            _playPauseButton.text = _playbackController.IsPlaying ? "Ⅱ" : "▶";
            _playPauseButton.tooltip = _playbackController.IsPlaying ? "\u6682\u505c\u65f6\u95f4\u8f74\u64ad\u653e" : "\u6309 60fps \u64ad\u653e\u5f53\u524d\u52a8\u4f5c";
        }

        private void UpdateFrameLabel()
        {
            if (_frameLabel == null)
                return;

            int current = _playbackController?.CurrentFrame ?? _state.PlayheadFrame;
            int duration = _snapshot?.DurationFrames ?? Math.Max(1, _host?.DefaultDurationFrames ?? 8);
            _frameLabel.text = "\u5e27 " + current + " / " + duration;
        }

        internal void SeekPlayheadFromMouse(IMouseEvent evt)
        {
            if (_playbackController == null || _snapshot == null)
                return;

            float localX = CanvasWorldToLocalX(evt.mousePosition.x);
            int frame = CharacterActionTimelineLayout.ClampFrame(
                PixelToFrameCell(localX),
                _snapshot.ActionDurationFrames);
            _playbackController.Seek(frame);
        }

        internal void BeginPlayheadScrub()
        {
            IsPlayheadScrubbing = true;
        }

        internal void EndPlayheadScrub()
        {
            if (!IsPlayheadScrubbing)
                return;
            IsPlayheadScrubbing = false;
            PlayheadScrubEnded?.Invoke(_playbackController?.CurrentFrame ?? _state.PlayheadFrame);
        }

        internal void CancelPlayheadScrub()
        {
            IsPlayheadScrubbing = false;
        }

        private void RegisterRowScrubClick(VisualElement row)
        {
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != (int)MouseButton.LeftMouse || evt.altKey)
                    return;
                if (evt.clickCount != 1)
                    return;
                if (evt.target != row)
                    return;

                SeekPlayheadFromMouse(evt);
                Focus();
            });
        }

        private void BuildPlayheadVisual()
        {
            _playhead = new VisualElement();
            _playhead.AddToClassList("ca-tl-playhead");
            _playhead.pickingMode = PickingMode.Ignore;
            _playhead.tooltip = "拖动指针预览帧";

            _playheadHandle = new VisualElement();
            _playheadHandle.AddToClassList("ca-tl-playhead-handle");
            _playheadHandle.focusable = false;
            _playheadHandle.pickingMode = PickingMode.Position;
            _playheadHandle.tooltip = _playhead.tooltip;
            _playheadManipulator = new TimelinePlayheadManipulator(this, _playheadHandle);
            _playheadHandle.AddManipulator(_playheadManipulator);
        }

        internal bool HasPlayheadDragManipulatorForTests()
        {
            return _playheadManipulator != null;
        }

        private void BuildSelectionFrameGuideVisual()
        {
            _selectionFrameGuide = new VisualElement();
            _selectionFrameGuide.AddToClassList("ca-tl-selection-guide");
            _selectionFrameGuide.pickingMode = PickingMode.Ignore;
            _selectionFrameGuide.style.display = DisplayStyle.None;
        }

        private void UpdateSelectionFrameGuide(bool refreshTooltip = true)
        {
            if (_selectionFrameGuide == null || _snapshot == null || _canvasRoot == null)
                return;

            ITimelineItemAdapter adapter = string.IsNullOrEmpty(_state.SelectedItemId)
                ? null
                : _snapshot.FindItem(_state.SelectedItemId);
            if (adapter == null)
            {
                _selectionFrameGuide.style.display = DisplayStyle.None;
                return;
            }

            bool marker = adapter.ItemKind == TimelineItemKind.Marker;
            float x0;
            float width;
            if (marker)
            {
                width = 3f;
                x0 = CharacterActionTimelineLayout.ContentPadding
                    + CharacterActionTimelineLayout.FrameToX(adapter.StartFrame, _state.PixelsPerFrame)
                    + _state.PixelsPerFrame * 0.5f
                    - width * 0.5f;
            }
            else
            {
                x0 = CharacterActionTimelineLayout.ContentPadding
                    + CharacterActionTimelineLayout.FrameToX(adapter.StartFrame, _state.PixelsPerFrame);
                float x1 = CharacterActionTimelineLayout.ContentPadding
                    + CharacterActionTimelineLayout.FrameToX(adapter.EndFrame + 1, _state.PixelsPerFrame);
                width = Math.Max(1f, x1 - x0);
            }

            float height = _canvasRoot.layout.height > 1f ? _canvasRoot.layout.height : 200f;

            _selectionFrameGuide.style.display = DisplayStyle.Flex;
            _selectionFrameGuide.EnableInClassList("ca-tl-selection-guide-marker", marker);
            _selectionFrameGuide.EnableInClassList("ca-tl-selection-guide-clip", !marker);
            _selectionFrameGuide.style.left = x0;
            _selectionFrameGuide.style.top = 0f;
            _selectionFrameGuide.style.width = width;
            _selectionFrameGuide.style.height = height;
            if (refreshTooltip)
                _selectionFrameGuide.tooltip = adapter.FullTitle;
        }

        private void RefreshPlayheadHighlight()
        {
            if (_snapshot == null)
                return;

            int playhead = _playbackController?.CurrentFrame ?? _state.PlayheadFrame;
            foreach (KeyValuePair<string, VisualElement> pair in _itemElements)
            {
                ITimelineItemAdapter adapter = _snapshot.FindItem(pair.Key);
                if (adapter == null)
                    continue;

                bool active = playhead >= adapter.StartFrame && playhead <= adapter.EndFrame;
                pair.Value.EnableInClassList("ca-tl-at-playhead", active);
                pair.Value.EnableInClassList("ca-tl-not-at-playhead", !active);
            }
        }

        private void UpdatePlayhead()
        {
            if (_playhead == null || _snapshot == null)
                return;

            int frame = _playbackController != null
                ? _playbackController.CurrentFrame
                : _state.PlayheadFrame;
            frame = CharacterActionTimelineLayout.ClampFrame(frame, _snapshot.ActionDurationFrames);
            _state.PlayheadFrame = frame;

            float x = CharacterActionTimelineLayout.ContentPadding
                + CharacterActionTimelineLayout.FrameToX(frame, _state.PixelsPerFrame);
            float height = _canvasRoot.layout.height > 1f ? _canvasRoot.layout.height : 200f;
            float frameWidth = Math.Max(1f, _state.PixelsPerFrame);
            _playhead.style.left = x;
            _playhead.style.width = frameWidth;
            _playhead.style.height = height;

            if (_playheadHandle != null)
            {
                float handleWidth = Math.Max(8f, frameWidth);
                _playheadHandle.style.left = x - (handleWidth - frameWidth) * 0.5f;
                _playheadHandle.style.top = 0f;
                _playheadHandle.style.width = handleWidth;
                _playheadHandle.style.height = CharacterActionTimelineLayout.RulerHeight;
            }
        }

        private void UpdateSpeedButtonText()
        {
            if (_speedButton == null || _playbackController == null)
                return;

            float speed = _playbackController.PlaybackSpeed;
            _speedButton.text = speed == 0.5f ? "0.5x"
                : speed >= 4f ? "4x"
                : speed >= 2f ? "2x"
                : "1x";
        }
    }
}
