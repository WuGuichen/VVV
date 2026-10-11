using System;
using UnityEditor;

namespace MxFramework.Editor.Authoring
{
    /// <summary>Shared fixed-frame playback contract for Editor authoring timelines.</summary>
    public interface IAuthoringTimelinePlayback
    {
        int CurrentFrame { get; }
        int DurationFrames { get; }
        bool IsPlaying { get; }
        bool LoopPlayback { get; set; }
        float PlaybackSpeed { get; set; }
        event Action<int> FrameChanged;
    }

    /// <summary>Shared fixed-frame playback controller used by authoring surfaces.</summary>
    /// <remarks>NoAlloc after initialization while playing, stepping, or seeking.</remarks>
    public class AuthoringTimelinePlaybackController : IAuthoringTimelinePlayback, IDisposable
    {
        internal const int MaxAdvanceStepsPerUpdate = 240;

        private readonly float _framesPerSecond;
        private int _currentFrame;
        private int _durationFrames = 1;
        private bool _isPlaying;
        private float _playbackSpeed = 1f;
        private double _lastPlayTime;
        private float _playAccumulator;
        private bool _disposed;

        public AuthoringTimelinePlaybackController(float framesPerSecond = 60f)
        {
            _framesPerSecond = framesPerSecond > 0.0001f ? framesPerSecond : 60f;
        }

        public event Action<int> FrameChanged;
        public int CurrentFrame => _currentFrame;
        public int DurationFrames => _durationFrames;
        public bool IsPlaying => _isPlaying;
        public bool LoopPlayback { get; set; } = true;
        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = IsFinite(value) && value > 0.0001f ? value : 1f;
        }

        public void SetDuration(int durationFrames)
        {
            _durationFrames = Math.Max(1, durationFrames);
            Seek(_currentFrame);
        }

        public void Seek(int frame)
        {
            if (_disposed)
                return;
            Pause();
            int clamped = Math.Max(0, Math.Min(frame, _durationFrames - 1));
            if (clamped == _currentFrame)
                return;
            _currentFrame = clamped;
            FrameChanged?.Invoke(_currentFrame);
        }

        public void Step(int delta) => Seek(_currentFrame + delta);

        public void Restart()
        {
            Pause();
            if (_currentFrame == 0)
                FrameChanged?.Invoke(0);
            else
                Seek(0);
        }

        public void Play()
        {
            if (_disposed || _isPlaying || _durationFrames <= 1)
                return;
            _isPlaying = true;
            _lastPlayTime = EditorApplication.timeSinceStartup;
            _playAccumulator = 0f;
            EditorApplication.update += OnEditorUpdate;
        }

        public void Pause()
        {
            if (!_isPlaying)
                return;
            _isPlaying = false;
            EditorApplication.update -= OnEditorUpdate;
        }

        public void TogglePlay()
        {
            if (_isPlaying) Pause(); else Play();
        }

        public void CyclePlaybackSpeed()
        {
            PlaybackSpeed = _playbackSpeed >= 4f ? 0.5f
                : _playbackSpeed >= 2f ? 4f
                : _playbackSpeed >= 1f ? 2f
                : 1f;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            Pause();
            _disposed = true;
            FrameChanged = null;
        }

        internal void AdvanceForTests(float deltaSeconds)
        {
            if (!_disposed)
                AdvancePlayback(deltaSeconds);
        }

        private void OnEditorUpdate()
        {
            if (!_isPlaying || _disposed)
                return;
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastPlayTime);
            _lastPlayTime = now;
            AdvancePlayback(delta);
        }

        private void AdvancePlayback(float deltaSeconds)
        {
            if (_durationFrames <= 1)
            {
                Pause();
                return;
            }

            if (!IsFinite(deltaSeconds) || deltaSeconds <= 0f)
                return;

            _playAccumulator += deltaSeconds;
            float frameInterval = 1f / _framesPerSecond / _playbackSpeed;
            if (!IsFinite(_playAccumulator)
                || !IsFinite(frameInterval)
                || frameInterval <= 0f)
            {
                _playAccumulator = 0f;
                PlaybackSpeed = 1f;
                return;
            }

            int advancedSteps = 0;
            while (_playAccumulator >= frameInterval
                && advancedSteps < MaxAdvanceStepsPerUpdate)
            {
                _playAccumulator -= frameInterval;
                advancedSteps++;
                int next = _currentFrame + 1;
                if (next >= _durationFrames)
                {
                    if (LoopPlayback) next = 0;
                    else
                    {
                        next = _durationFrames - 1;
                        Pause();
                        _playAccumulator = 0f;
                    }
                }
                if (next == _currentFrame)
                    break;
                _currentFrame = next;
                FrameChanged?.Invoke(_currentFrame);
            }

            if (advancedSteps == MaxAdvanceStepsPerUpdate
                && _playAccumulator >= frameInterval)
            {
                _playAccumulator %= frameInterval;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
