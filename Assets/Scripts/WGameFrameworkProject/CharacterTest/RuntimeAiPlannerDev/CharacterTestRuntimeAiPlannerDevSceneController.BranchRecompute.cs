using System;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabPresentationPolicy
    {
        Immediate,
        Deferred
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private const int MaxHistoryPlaybackAdvancesPerUpdate = 8;
        private const double DefaultHistoryPlaybackFrameSeconds = 1d / 60d;

        private bool _branchRecomputeArmed;
        private bool _branchConfirmationPending;
        private long _branchConfirmationGeneration;
        private long _branchConfirmationSessionGeneration;
        private long _branchConfirmationBaseAuthorityHash;
        private bool _branchTruncatePending;
        private int _branchBaseFrame;
        private int _branchOriginalLatestFrame;
        private bool _historyPlaybackActive;
        private double _historyPlaybackAccumulatorSeconds;

        internal bool BranchRecomputeArmed => _branchRecomputeArmed;
        internal bool BranchConfirmationPending => _branchConfirmationPending;
        internal long BranchConfirmationGeneration => _branchConfirmationGeneration;
        internal int BranchRecomputeBaseFrame => _branchBaseFrame;
        internal int BranchRecomputeOriginalLatestFrame => _branchOriginalLatestFrame;
        internal bool HistoryPlaybackActive => _historyPlaybackActive;

        public bool ToggleBranchRecompute()
        {
            if (!EnsureRuntimeReady() || !EnsureTimelineSessionContinuable())
                return false;
            if (_frameSelection.IsBusy)
            {
                _statusMessage = "SEEK_BUSY: wait for the committed RuntimeFrame before changing the branch.";
                return false;
            }
            if (_branchRecomputeArmed)
            {
                CancelBranchRecompute("已取消从选中帧重算。", true);
                return true;
            }

            if (_branchConfirmationPending)
                return ConfirmBranchRecompute();

            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || !_context.Paused
                || _seekInProgress
                || _loopPlaybackActive
                || _historyPlaybackActive
                || FutureAdvanceActive)
            {
                _statusMessage = "请先暂停并完成当前定位、循环或模拟任务。";
                return false;
            }
            if (!_timeline.HasSelection || _timeline.SelectedFrame >= _timeline.LatestFrame)
            {
                _statusMessage = "请选择一个已校验的历史帧，再从此重算。";
                return false;
            }

            int authorityFrame = _context.Slice.CurrentFrame.Value > int.MaxValue
                ? int.MaxValue
                : (int)_context.Slice.CurrentFrame.Value;
            if (!_lastSeekSucceeded || authorityFrame != _timeline.SelectedFrame)
            {
                _statusMessage = "选中帧尚未通过 Warm Replay 校验，不能从此重算。";
                return false;
            }

            _branchConfirmationPending = true;
            _branchConfirmationGeneration++;
            _branchBaseFrame = _timeline.SelectedFrame;
            _branchOriginalLatestFrame = _timeline.LatestFrame;
            _branchConfirmationSessionGeneration = _timeline.SessionIdentity.Generation;
            _branchConfirmationBaseAuthorityHash = _frameSelection.CommittedReceipt.AuthorityHash;
            _statusMessage = BuildBranchRiskMessage();
            return true;
        }

        internal bool CancelBranchRecomputeConfirmation()
        {
            if (!EnsureRuntimeReady()
                || !EnsureTimelineSessionContinuable()
                || _frameSelection.IsBusy)
            {
                return false;
            }
            if (!_branchConfirmationPending)
                return false;
            CancelBranchRecompute("已取消建立新未来。", true);
            return true;
        }

        private bool ConfirmBranchRecompute()
        {
            CharacterTestRuntimeAiLabCommittedFrameReceipt receipt = _frameSelection.CommittedReceipt;
            bool stale = !_branchConfirmationPending
                || !_timeline.HasSelection
                || _timeline.SelectedFrame != _branchBaseFrame
                || _timeline.LatestFrame != _branchOriginalLatestFrame
                || _timeline.SessionIdentity.Generation != _branchConfirmationSessionGeneration
                || !receipt.IsValid
                || receipt.Frame != _branchBaseFrame
                || receipt.AuthorityHash != _branchConfirmationBaseAuthorityHash;
            if (stale)
            {
                CancelBranchRecompute();
                _statusMessage = "分支确认已过期，请重新选择历史帧。";
                return false;
            }

            _branchConfirmationPending = false;
            _branchRecomputeArmed = true;
            _statusMessage = "已确认从帧 " + _branchBaseFrame + " 建立新未来；推进一帧后原子提交。";
            return true;
        }

        internal void CancelBranchRecompute()
        {
            CancelBranchRecompute(null, false);
        }

        private void CancelBranchRecompute(string statusMessage, bool updateStatus)
        {
            _branchRecomputeArmed = false;
            _branchConfirmationPending = false;
            _branchTruncatePending = false;
            _branchBaseFrame = 0;
            _branchOriginalLatestFrame = 0;
            _branchConfirmationSessionGeneration = 0L;
            _branchConfirmationBaseAuthorityHash = 0L;
            if (updateStatus && !string.IsNullOrEmpty(statusMessage))
                _statusMessage = statusMessage;
        }

        private string BuildBranchRiskMessage()
        {
            return "基于帧 " + _branchBaseFrame
                + "，首次生成将删除 " + (_branchBaseFrame + 1)
                + "…" + _branchOriginalLatestFrame + "。";
        }

        private bool AdvanceOneAuthorityFrame(CharacterTestRuntimeAiLabPresentationPolicy presentationPolicy)
        {
            if (TacticalCardTestMode && !TacticalCardTestInFlight)
            { _statusMessage = "请先选择并打出下一张战术卡。"; return false; }
            bool firstBranchFrame = _branchRecomputeArmed
                && _timeline.HasSelection
                && _timeline.SelectedFrame == _branchBaseFrame
                && _timeline.LatestFrame == _branchOriginalLatestFrame;
            SessionStartRecoverySnapshot branchRecovery = firstBranchFrame
                ? CaptureSessionStartRecoverySnapshot()
                : null;
            int branchBaseFrame = _branchBaseFrame;
            int branchOriginalLatestFrame = _branchOriginalLatestFrame;
            if (firstBranchFrame)
            {
                if (!_timeline.BeginBranchDecisionTraceTransaction(branchBaseFrame))
                {
                    return RollBackFailedBranchFirstFrame(
                        branchRecovery,
                        "Decision trace branch transaction could not pin the retained future.");
                }
                _branchTruncatePending = true;
            }

            bool advanced = presentationPolicy == CharacterTestRuntimeAiLabPresentationPolicy.Immediate
                ? _gameManager != null && _gameManager.StepSimulationOnceForRuntimeAiPlannerDev()
                : _gameManager != null && _gameManager.StepSimulationDataOnlyForRuntimeAiPlannerDev();
            if (advanced)
            {
                // The simulation callback can publish behavior logic events after the regular
                // panel capture hook. Re-capture the same RuntimeFrame so typed markers and the
                // post-transition logic snapshot share the exact committed frame.
                CaptureLatestFrameIfNeeded(force: true);
                if (firstBranchFrame
                    && !ValidateBranchFirstFrameCommit(
                        branchBaseFrame,
                        branchOriginalLatestFrame,
                        out string diagnostic))
                {
                    return RollBackFailedBranchFirstFrame(branchRecovery, diagnostic);
                }
                if (firstBranchFrame && !_timeline.CommitBranchDecisionTraceTransaction())
                {
                    return RollBackFailedBranchFirstFrame(
                        branchRecovery,
                        "Decision trace branch transaction could not install the staged frame mapping.");
                }
                if (firstBranchFrame)
                    RestartReplayRecordingForCommittedBranch();
                return true;
            }

            _branchTruncatePending = false;
            if (firstBranchFrame)
                return RollBackFailedBranchFirstFrame(
                    branchRecovery,
                    "候选首帧未能推进。 ");
            return false;
        }

        private void RestartReplayRecordingForCommittedBranch()
        {
            ResetReplayValidationWorkspace(clearCapture: true);
            CharacterTestReplayPlaybackStatus status =
                _context?.NetworkCoordinator?.BeginNetworkReplayRecording();
            if (status == null || !status.Available || !status.Recording)
            {
                _statusMessage = "新未来已提交，但 Replay 新世代启动失败："
                    + (status?.Summary ?? "Replay recorder unavailable.");
                return;
            }

            _statusMessage = "新未来已提交；Replay 已从分支首帧建立新的录制世代。";
        }

        private bool ValidateBranchFirstFrameCommit(
            int baseFrame,
            int originalLatestFrame,
            out string diagnostic)
        {
            int expectedFrame = baseFrame + 1;
            if (_timeline == null
                || !_timeline.TryGetFrame(expectedFrame, out CharacterTestRuntimeAiPlannerDevTimelineRecord record)
                || _timeline.LatestFrame != expectedFrame
                || _frameBuffer == null
                || _frameBuffer.Latest.Frame != expectedFrame
                || originalLatestFrame <= baseFrame)
            {
                diagnostic = "候选首帧没有形成完整的新时间轴记录。";
                return false;
            }

            CharacterTestNetworkSnapshotParticipant participant =
                _context?.Slice?.Services.Network.CreateSnapshotParticipant();
            RuntimeFrame authorityFrame = _context?.Slice != null
                ? _context.Slice.CurrentFrame
                : default;
            long authorityHash = participant != null
                ? participant.ComputeStateHash(authorityFrame)
                : 0L;
            CharacterTestRuntimeAiLabCommittedFrameBundle candidateBundle =
                CreateCommittedFrameBundle(record);
            if (authorityFrame.Value != expectedFrame
                || authorityHash != record.StateHash
                || candidateBundle == null
                || candidateBundle.Record.Frame != expectedFrame
                || candidateBundle.AuthorityHash != authorityHash)
            {
                diagnostic = "候选首帧 authority、记录或私有候选包不一致。";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private bool RollBackFailedBranchFirstFrame(
            SessionStartRecoverySnapshot recovery,
            string diagnostic)
        {
            string recoveryDiagnostic = string.Empty;
            if (recovery != null && recovery.TryRestore(this, out recoveryDiagnostic))
            {
                _statusMessage = "重算首帧失败，已保留原未来时间线。原因：" + diagnostic;
                return false;
            }

            _sessionContinuable = false;
            _frameSelection.BeginRecovery();
            _frameSelection.CompleteRecovery(success: false);
            SetPaused(true);
            _statusMessage = "重算首帧失败且旧未来恢复失败，只能重置或退出。"
                + (string.IsNullOrEmpty(recoveryDiagnostic) ? string.Empty : " 原因：" + recoveryDiagnostic);
            return false;
        }

        private bool StartHistoryPlayback()
        {
            if (!_timeline.HasSelection || _timeline.SelectedFrame >= _timeline.LatestFrame)
                return false;

            SetPaused(true);
            DisableDebugEditGate();
            RestoreUnityTimeScale();
            _historyPlaybackActive = true;
            _historyPlaybackAccumulatorSeconds = 0d;
            _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Review);
            _statusMessage = "正从选中帧顺序播放已有历史。";
            return true;
        }

        private void UpdateHistoryPlayback(double unscaledDeltaTime)
        {
            if (!_historyPlaybackActive || _seekInProgress)
                return;

            if (_timeline.SelectedFrame >= _timeline.LatestFrame)
            {
                FinishHistoryPlaybackAtLatest();
                return;
            }

            _historyPlaybackAccumulatorSeconds += Math.Max(0d, unscaledDeltaTime)
                * _timeline.PlaybackSpeed;
            int advances = 0;
            while (_historyPlaybackActive
                && !_seekInProgress
                && advances < MaxHistoryPlaybackAdvancesPerUpdate
                && _historyPlaybackAccumulatorSeconds + 0.0000001d >= DefaultHistoryPlaybackFrameSeconds)
            {
                if (_timeline.SelectedFrame >= _timeline.LatestFrame)
                {
                    FinishHistoryPlaybackAtLatest();
                    return;
                }

                _historyPlaybackAccumulatorSeconds -= DefaultHistoryPlaybackFrameSeconds;
                int selectedIndex = _timeline.FindIndexByFrame(_timeline.SelectedFrame);
                int nextIndex = selectedIndex + 1;
                if (selectedIndex < 0
                    || nextIndex >= _timeline.Records.Count
                    || !SeekToFrame(new CharacterTestRuntimeAiLabSeekRequest(
                        _timeline.Records[nextIndex].Frame,
                        CharacterTestRuntimeAiLabSeekOrigin.Playback,
                        true)))
                {
                    PauseHistoryPlayback("历史顺序播放已停止：下一帧不可用。");
                    return;
                }
                advances++;
            }
        }

        private void FinishHistoryPlaybackAtLatest()
        {
            _historyPlaybackActive = false;
            _historyPlaybackAccumulatorSeconds = 0d;
            _timeline.SelectLatest();
            SetPaused(false);
            _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Live);
            _statusMessage = "已有历史播放完毕，继续实时模拟。";
        }

        private void PauseHistoryPlayback(string statusMessage)
        {
            _historyPlaybackActive = false;
            _historyPlaybackAccumulatorSeconds = 0d;
            SetPaused(true);
            RestoreUnityTimeScale();
            _timeline.SetMode(_timeline.SelectedFrame < _timeline.LatestFrame
                ? CharacterTestRuntimeAiPlannerDevTimelineMode.Review
                : CharacterTestRuntimeAiPlannerDevTimelineMode.Paused);
            _statusMessage = statusMessage;
        }

        private void ResetHistoryPlaybackState()
        {
            _historyPlaybackActive = false;
            _historyPlaybackAccumulatorSeconds = 0d;
            SetRuntimeAiLabHistoricalNetworkReviewActive(false);
        }
    }
}
