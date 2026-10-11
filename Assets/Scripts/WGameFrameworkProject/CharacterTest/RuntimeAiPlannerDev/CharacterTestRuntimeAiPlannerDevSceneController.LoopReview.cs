using System;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private const int MaxLoopReviewAdvancesPerUpdate = 8;
        private const double DefaultLoopReviewFrameSeconds = 1d / 60d;

        private bool _loopPlaybackActive;
        private double _loopPlaybackAccumulatorSeconds;

        private bool StartLoopReview()
        {
            if (!_timeline.LoopEnabled)
            {
                _statusMessage = "Enable loop A-B before starting loop review.";
                return false;
            }

            if (!_timeline.TryValidateLoopRange(out string diagnostic))
            {
                _statusMessage = diagnostic;
                return false;
            }

            SetPaused(true);
            DisableDebugEditGate();
            RestoreUnityTimeScale();
            _loopPlaybackActive = true;
            _loopPlaybackAccumulatorSeconds = 0d;
            if (!SeekLoopFrame(_timeline.LoopStartFrame))
            {
                _loopPlaybackActive = false;
                _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Review);
                return false;
            }

            _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.LoopReview);
            _statusMessage = "Loop Review started: A "
                + _timeline.LoopStartFrame
                + " -> B "
                + _timeline.LoopEndFrame
                + ".";
            return true;
        }

        private void UpdateLoopReviewPlayback()
        {
            if (!_loopPlaybackActive)
                return;

            if (_seekInProgress)
                return;

            // UnityTimeBoundary: wall time only paces read-only historical frame presentation.
            _loopPlaybackAccumulatorSeconds += Math.Max(0d, Time.unscaledDeltaTime)
                * _timeline.PlaybackSpeed;
            int advances = 0;
            while (_loopPlaybackActive && advances < MaxLoopReviewAdvancesPerUpdate)
            {
                double frameSeconds = ResolveLoopReviewFrameSeconds();
                if (_loopPlaybackAccumulatorSeconds + 0.0000001d < frameSeconds)
                    break;

                _loopPlaybackAccumulatorSeconds -= frameSeconds;
                if (!AdvanceLoopReviewFrame())
                    return;
                advances++;
            }
        }

        internal bool AdvanceLoopReviewFrame()
        {
            if (!_loopPlaybackActive || !_timeline.HasLoopRange)
                return false;

            int selectedFrame = _timeline.SelectedFrame;
            if (selectedFrame >= _timeline.LoopEndFrame)
            {
                if (!SeekLoopFrame(_timeline.LoopStartFrame))
                    return FailLoopReview(_statusMessage);

                _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.LoopReview);
                return true;
            }

            int nextFrame = selectedFrame + 1;
            if (!_timeline.TryGetFrame(nextFrame, out _))
                return FailLoopReview("Loop review is missing RuntimeFrame " + nextFrame + ".");
            if (!SeekLoopFrame(nextFrame))
                return FailLoopReview(_statusMessage);

            _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.LoopReview);
            return true;
        }

        private bool SeekLoopFrame(int frame)
        {
            return SeekToFrame(new CharacterTestRuntimeAiLabSeekRequest(
                frame,
                CharacterTestRuntimeAiLabSeekOrigin.Loop,
                stayPaused: true));
        }

        private double ResolveLoopReviewFrameSeconds()
        {
            if (!_timeline.TryGetSelected(out CharacterTestRuntimeAiPlannerDevTimelineRecord selected))
                return DefaultLoopReviewFrameSeconds;

            int adjacentFrame = selected.Frame < _timeline.LoopEndFrame
                ? selected.Frame + 1
                : selected.Frame - 1;
            if (!_timeline.TryGetFrame(adjacentFrame, out CharacterTestRuntimeAiPlannerDevTimelineRecord adjacent))
                return DefaultLoopReviewFrameSeconds;

            double delta = Math.Abs(adjacent.ElapsedSeconds - selected.ElapsedSeconds);
            return delta > 0.000001d ? delta : DefaultLoopReviewFrameSeconds;
        }

        private bool FailLoopReview(string diagnostic)
        {
            _loopPlaybackActive = false;
            _loopPlaybackAccumulatorSeconds = 0d;
            SetPaused(true);
            RestoreUnityTimeScale();
            _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Review);
            _statusMessage = string.IsNullOrEmpty(diagnostic)
                ? "Loop review stopped because authority replay failed."
                : diagnostic;
            return false;
        }

        private void PauseLoopReview(string statusMessage)
        {
            _loopPlaybackActive = false;
            _loopPlaybackAccumulatorSeconds = 0d;
            SetPaused(true);
            RestoreUnityTimeScale();
            _timeline.SetMode(_timeline.SelectedFrame < _timeline.LatestFrame
                ? CharacterTestRuntimeAiPlannerDevTimelineMode.Review
                : CharacterTestRuntimeAiPlannerDevTimelineMode.Paused);
            _statusMessage = statusMessage;
        }

        private void ResetLoopPlaybackState()
        {
            _loopPlaybackActive = false;
            _loopPlaybackAccumulatorSeconds = 0d;
        }
    }
}
