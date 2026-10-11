using System;
using System.Diagnostics;
using System.Globalization;
using MxFramework.Runtime;
using MxFramework.Vfx;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private const int ImmediateReplayFrameLimit = 120;
        private const int MaxReplayFramesPerEditorUpdate = 512;
        private const double ReplayBudgetMilliseconds = 4d;

        private DeterministicReplayWork _deterministicReplayWork;
        private bool _lastSeekSucceeded;
        private bool _resumeLiveAfterReplay;
        private CharacterTestRuntimeAiLabSeekRequest _activeSeekRequest;
        private int _committedFrameBeforeSeek;
        private int _committedAgentBeforeSeek;
        private string _failedSeekDiagnostic = string.Empty;

        public bool ReplayInProgress => _deterministicReplayWork != null;

        internal int ReplayProcessedFrames => _deterministicReplayWork?.ProcessedFrames ?? 0;
        internal int ReplayTotalFrames => _deterministicReplayWork?.TotalFrames ?? 0;

        public bool ColdVerifySelectedFrame()
        {
            if (!TryResolveSelectedRecord(out CharacterTestRuntimeAiPlannerDevTimelineRecord target))
                return false;
            if (_seekInProgress || !_context.Paused || _loopPlaybackActive)
            {
                _statusMessage = "Pause on a recorded frame before running Cold Bootstrap Verify.";
                return false;
            }
            if (_initialReplayBaseline == null || _replayBootstrapConfig == null)
            {
                _statusMessage = "Cold Bootstrap Verify requires a valid initial replay baseline.";
                return false;
            }

            return BeginColdBootstrapVerification(target);
        }

        private bool BeginDeterministicReplay(
            CharacterTestRuntimeAiPlannerDevTimelineRecord target,
            bool wasPaused,
            CharacterTestRuntimeAiLabSeekRequest request)
        {
            _activeSeekRequest = request;
            _committedFrameBeforeSeek = _timeline.SelectedFrame;
            _committedAgentBeforeSeek = ResolveSelectedAgentEntityId();
            if (target.Frame < _timeline.LatestFrame)
                SetRuntimeAiLabHistoricalNetworkReviewActive(true);
            SetPaused(true);
            DisableDebugEditGate();
            _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Seek);
            _seekInProgress = true;
            _lastSeekSucceeded = false;
            CharacterTestSliceNetworkServices network = _context.Slice?.Services.Network;
            network?.SetNetworkRollbackReplayActive(true);
            CharacterTestNetworkSnapshotParticipant participant = _snapshotParticipant;
            if (participant == null)
            {
                network?.SetNetworkRollbackReplayActive(false);
                _seekInProgress = false;
                _frameSelection.AbortRestoreBeforeMutation();
                _statusMessage = "Runtime AI Lab snapshot participant is unavailable.";
                return false;
            }

            long resetStart = Stopwatch.GetTimestamp();
            RuntimeAiLabInitialReplayBaseline baseline = _initialReplayBaseline;
            if (!RestoreInitialReplayBaseline(network, participant, baseline, out string resetDiagnostic))
            {
                network?.SetNetworkRollbackReplayActive(false);
                _seekInProgress = false;
                _failedSeekDiagnostic = resetDiagnostic;
                _frameSelection.BeginRecovery();
                if (!BeginSeekRecovery(network, participant))
                {
                    _sessionContinuable = false;
                    _frameSelection.CompleteRecovery(success: false);
                    SetPaused(true);
                    _statusMessage = "历史定位预恢复失败，且无法恢复旧帧。当前会话只能重置或退出。原因："
                        + resetDiagnostic;
                }
                return false;
            }

            _deterministicReplayWork = new DeterministicReplayWork(
                DeterministicReplayMode.Warm,
                target,
                wasPaused,
                network,
                participant,
                (int)baseline.Frame.Value + 1,
                Math.Max(0, target.Frame - (int)baseline.Frame.Value),
                Stopwatch.GetTimestamp() - resetStart,
                _frameSelection.TransactionId,
                _frameSelection.ActiveRestoreTarget);

            if (_deterministicReplayWork.TotalFrames <= ImmediateReplayFrameLimit)
            {
                ProcessDeterministicReplay(ignoreBudget: true);
                return _lastSeekSucceeded;
            }

            UpdateReplayProgressStatus();
            return true;
        }

        private bool BeginColdBootstrapVerification(
            CharacterTestRuntimeAiPlannerDevTimelineRecord target)
        {
            SetPaused(true);
            DisableDebugEditGate();
            _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Seek);
            _seekInProgress = true;
            _lastSeekSucceeded = false;

            long bootstrapStart = Stopwatch.GetTimestamp();
            _sessionBuilder.RebuildRuntimeAiLabBattleSession(
                _replayBootstrapConfig,
                "Runtime AI Lab cold bootstrap verification.");
            if (!RestoreBaselineRevision(out string baselineDiagnostic))
            {
                _seekInProgress = false;
                _statusMessage = "Cold Bootstrap Verify could not restore the evaluator baseline: "
                    + baselineDiagnostic;
                return false;
            }
            _context.Slice?.Services.RuntimeCommands?.SetRuntimeAiBehaviorSelectionUnlocked(false);
            CharacterTestSliceNetworkServices network = _context.Slice?.Services.Network;
            network?.SetNetworkRollbackReplayActive(true);
            CharacterTestNetworkSnapshotParticipant participant = network?.CreateSnapshotParticipant();
            if (participant == null)
            {
                network?.SetNetworkRollbackReplayActive(false);
                _seekInProgress = false;
                _statusMessage = "Cold Bootstrap Verify snapshot participant is unavailable.";
                return false;
            }

            _snapshotParticipant = participant;
            int currentFrame = (int)_context.Slice.CurrentFrame.Value;
            _deterministicReplayWork = new DeterministicReplayWork(
                DeterministicReplayMode.ColdVerify,
                target,
                wasPaused: true,
                network,
                participant,
                currentFrame + 1,
                Math.Max(0, target.Frame - currentFrame),
                resetTicks: 0)
            {
                BootstrapTicks = Stopwatch.GetTimestamp() - bootstrapStart
            };
            UpdateReplayProgressStatus();
            return true;
        }

        private void ProcessDeterministicReplay(bool ignoreBudget)
        {
            DeterministicReplayWork work = _deterministicReplayWork;
            if (work == null)
                return;

            long sliceStart = Stopwatch.GetTimestamp();
            int processedThisUpdate = 0;
            while (work.NextFrame <= work.Target.Frame)
            {
                int frame = work.NextFrame;
                bool coldBootstrapFrame = work.Mode == DeterministicReplayMode.ColdVerify
                    && frame <= _replayBootstrapEndFrame;
                CharacterTestRuntimeAiPlannerDevTimelineRecord replayRecord = null;
                if (!coldBootstrapFrame
                    && !_timeline.TryGetFrame(frame, out replayRecord))
                {
                    FailDeterministicReplay(
                        "Deterministic replay is missing RuntimeFrame " + frame + ".");
                    return;
                }

                if (!coldBootstrapFrame
                    && !ApplyRevisionEventsForFrame(frame, out string revisionDiagnostic))
                {
                    FailDeterministicReplay(
                        "Deterministic replay revision apply failed at RuntimeFrame "
                        + frame
                        + ": "
                        + revisionDiagnostic);
                    return;
                }

                if (!coldBootstrapFrame)
                    EnqueueReplayCommands(replayRecord);
                if (_gameManager == null
                    || !_gameManager.StepRuntimeAiLabReplayFrameWithoutRendering(
                        replayRecord != null ? replayRecord.AnimationInputs : default))
                {
                    FailDeterministicReplay(
                        "Deterministic replay failed at RuntimeFrame " + frame + ".");
                    return;
                }

                RuntimeFrame replayedFrame = _context.Slice.CurrentFrame;
                long replayedHash = work.Participant.ComputeStateHash(replayedFrame);
                long expectedHash = coldBootstrapFrame
                    ? frame == _replayBootstrapEndFrame
                        ? _initialReplayBaseline.AuthorityHash
                        : replayedHash
                    : replayRecord.StateHash;
                bool mustCompareHash = !coldBootstrapFrame || frame == _replayBootstrapEndFrame;
                if (replayedFrame.Value != frame)
                {
                    CharacterTestRuntimeAiLabFrameSyncReport report =
                        CharacterTestRuntimeAiLabAuthorityProbe.Compare(
                            new CharacterTestRuntimeAiLabAuthoritySample(
                                new RuntimeFrame(frame),
                                expectedHash),
                            new CharacterTestRuntimeAiLabAuthoritySample(
                                replayedFrame,
                                replayedHash));
                    _lastReplayResult = new RuntimeAiLabDeterministicReplayResult(
                        replayedFrame,
                        report,
                        null);
                    FailDeterministicReplay(
                        "Frame Sync FAIL at RuntimeFrame "
                        + frame
                        + ": replay advanced to RuntimeFrame "
                        + replayedFrame.Value
                        + ".");
                    return;
                }

                if (mustCompareHash && replayedHash != expectedHash)
                {
                    CharacterTestRuntimeAiLabFrameSyncReport report =
                        CharacterTestRuntimeAiLabAuthorityProbe.Compare(
                            new CharacterTestRuntimeAiLabAuthoritySample(
                                new RuntimeFrame(frame),
                                expectedHash),
                            new CharacterTestRuntimeAiLabAuthoritySample(
                                replayedFrame,
                                replayedHash));
                    _lastReplayResult = new RuntimeAiLabDeterministicReplayResult(
                        replayedFrame,
                        report,
                        null);
                    string validationWarning =
                        "RuntimeFrame "
                        + frame
                        + " expected hash "
                        + expectedHash.ToString("X16", CultureInfo.InvariantCulture)
                        + ", actual "
                        + replayedHash.ToString("X16", CultureInfo.InvariantCulture)
                        + ".";
                    if (work.Mode == DeterministicReplayMode.ColdVerify)
                    {
                        FailDeterministicReplay("Frame Sync FAIL at " + validationWarning);
                        return;
                    }

                    // Warm navigation selects a historical frame first and audits
                    // determinism second. Preserve the warning without blocking the
                    // authority restore or the matching presentation refresh.
                    work.RecordValidationWarning(validationWarning);
                }

                work.NextFrame++;
                work.ProcessedFrames++;
                processedThisUpdate++;

                if (!ignoreBudget
                    && (processedThisUpdate >= MaxReplayFramesPerEditorUpdate
                        || ElapsedMilliseconds(sliceStart) >= ReplayBudgetMilliseconds))
                {
                    work.CpuTicks += Stopwatch.GetTimestamp() - sliceStart;
                    UpdateReplayProgressStatus();
                    return;
                }
            }

            work.CpuTicks += Stopwatch.GetTimestamp() - sliceStart;
            CompleteDeterministicReplay(work);
        }

        private void CompleteDeterministicReplay(DeterministicReplayWork work)
        {
            RuntimeFrame currentFrame = _context.Slice.CurrentFrame;
            if (currentFrame.Value != work.Target.Frame)
            {
                FailDeterministicReplay(
                    "Seek frame mismatch: expected "
                    + work.Target.Frame
                    + ", actual "
                    + currentFrame.Value
                    + ".");
                return;
            }

            CharacterTestRuntimeAiLabFrameSyncReport authorityReport =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(
                    new CharacterTestRuntimeAiLabAuthoritySample(
                        currentFrame,
                        work.Target.StateHash),
                    CharacterTestRuntimeAiLabAuthorityProbe.Capture(
                        _context.Slice,
                        work.Participant));
            if (!authorityReport.Passed)
            {
                if (work.Mode == DeterministicReplayMode.ColdVerify)
                {
                    FailDeterministicReplay(
                        "Cold Bootstrap Verify authority validation failed at RuntimeFrame "
                        + currentFrame.Value
                        + ".");
                    return;
                }
                work.RecordValidationWarning(
                    "Target RuntimeFrame "
                    + currentFrame.Value
                    + " authority hash differs from the recorded frame.");
            }

            int requestedAgent = work.Mode == DeterministicReplayMode.Recovery
                ? _committedAgentBeforeSeek
                : work.Mode == DeterministicReplayMode.Warm
                    ? work.SelectionTarget.AgentEntityId
                    : _activeSeekRequest.AgentEntityId;
            int resolvedAgent = ResolveAgentForRecord(work.Target, requestedAgent);
            CharacterTestRuntimeAiLabCommittedFrameBundle bundle = CreateCommittedFrameBundle(work.Target);
            if (work.Mode == DeterministicReplayMode.Warm
                && _frameSelection.IsActiveRestoreSuperseded(
                    work.SelectionTransactionId,
                    work.SelectionTarget))
            {
                CharacterTestRuntimeAiLabFrameSelectionCompletion superseded =
                    _frameSelection.CompleteRestore(
                        work.SelectionTransactionId,
                        work.SelectionTarget,
                        resolvedAgent,
                        bundle,
                        out _);
                if (superseded != CharacterTestRuntimeAiLabFrameSelectionCompletion.Superseded)
                {
                    FailDeterministicReplay("Frame selection supersession became stale.");
                    return;
                }
                FinishDeterministicReplay(work);
                ProcessTimelineScrubWork();
                return;
            }

            if (work.Mode == DeterministicReplayMode.Warm
                && _frameSelection.IsEquivalentToCommitted(
                    work.SelectionTarget.AgentEntityId,
                    resolvedAgent,
                    bundle))
            {
                CharacterTestRuntimeAiLabFrameSelectionCompletion unchanged =
                    _frameSelection.CompleteRestore(
                        work.SelectionTransactionId,
                        work.SelectionTarget,
                        resolvedAgent,
                        bundle,
                        out _);
                if (unchanged != CharacterTestRuntimeAiLabFrameSelectionCompletion.Unchanged)
                {
                    FailDeterministicReplay("Frame selection no-op became stale.");
                    return;
                }
                _lastSeekSucceeded = true;
                _statusMessage = "RuntimeFrame " + work.Target.Frame + " 已是当前提交帧。";
                FinishDeterministicReplay(work);
                return;
            }

            if (work.Mode == DeterministicReplayMode.Warm
                && !_frameSelection.CanCompleteRestore(
                    work.SelectionTransactionId,
                    work.SelectionTarget,
                    bundle))
            {
                FailDeterministicReplay("Frame selection transaction became stale before presentation.");
                return;
            }

            // The transaction and frozen bundle are validated before touching the formal preview.
            // If presentation verification fails, restore the previous committed presentation in
            // the same call so the Preview Camera never renders the rejected target.
            long presentationStart = Stopwatch.GetTimestamp();
            _frameBuffer.SelectFrame(work.Target.Frame);
            _timeline.SelectFrame(work.Target.Frame);
            _lastCapturedRuntimeFrame = work.Target.Frame;
            bool missingRequestedAgent = requestedAgent > 0 && !TrySelectAgentEntityId(requestedAgent);
            ClampSelectedAgentIndex();
            _gameManager?.RefreshPresentationForRuntimeAiPlannerDev(
                resubmitCharacterAnimation: false);
            work.PresentationTicks = Stopwatch.GetTimestamp() - presentationStart;
            CharacterTestRuntimeAiLabFrameSyncReport presentationReport =
                ComparePresentationWithRecordedCapture(work.Target.Frame);
            if (presentationReport != null && !presentationReport.Passed)
            {
                _lastReplayResult = new RuntimeAiLabDeterministicReplayResult(
                    currentFrame,
                    authorityReport,
                    presentationReport);
                string presentationWarning =
                    "RuntimeFrame " + work.Target.Frame + " presentation differs from its capture.";
                if (work.Mode == DeterministicReplayMode.ColdVerify)
                {
                    RestoreCommittedPresentationSurface();
                    FailDeterministicReplay("Presentation validation failed: " + presentationWarning);
                    return;
                }
                work.RecordValidationWarning(presentationWarning);
            }

            CharacterTestRuntimeAiLabFrameSelectionCompletion selectionCompletion =
                work.Mode != DeterministicReplayMode.Warm
                    ? CharacterTestRuntimeAiLabFrameSelectionCompletion.Committed
                    : _frameSelection.CompleteRestore(
                        work.SelectionTransactionId,
                        work.SelectionTarget,
                        resolvedAgent,
                        bundle,
                        out _);
            if (selectionCompletion == CharacterTestRuntimeAiLabFrameSelectionCompletion.Rejected)
            {
                RestoreCommittedPresentationSurface();
                FailDeterministicReplay("Frame selection transaction became stale before commit.");
                return;
            }
            if (selectionCompletion == CharacterTestRuntimeAiLabFrameSelectionCompletion.Superseded)
            {
                RestoreCommittedPresentationSurface();
                FailDeterministicReplay("Frame selection changed during commit.");
                return;
            }
            if (selectionCompletion == CharacterTestRuntimeAiLabFrameSelectionCompletion.Unchanged)
            {
                RestoreCommittedPresentationSurface();
                FailDeterministicReplay("Frame selection changed during presentation validation.");
                return;
            }

            _context?.Slice?.Services?.Modules?.RuntimeAiPlannerCommands
                ?.RestoreDecisionTraceHistory(_timeline, work.Target);

            _lastReplayResult = new RuntimeAiLabDeterministicReplayResult(
                currentFrame,
                authorityReport,
                presentationReport);
            if (work.Target.Frame < _timeline.LatestFrame)
                SetRuntimeAiLabHistoricalNetworkReviewActive(true);

            if (work.Mode == DeterministicReplayMode.Recovery)
            {
                _frameSelection.CompleteRecovery(success: true);
                SetPaused(true);
                _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Review);
                _statusMessage = "历史定位失败，已恢复到帧 "
                    + work.Target.Frame
                    + "。原因："
                    + _failedSeekDiagnostic;
                if (work.HasValidationWarning)
                    _statusMessage += " Validation warning: " + work.ValidationWarning;
                _lastSeekSucceeded = false;
                FinishDeterministicReplay(work);
                return;
            }

            bool resumeLive = _resumeLiveAfterReplay || !work.WasPaused;
            _resumeLiveAfterReplay = false;
            if (resumeLive)
                SetPaused(false);
            _timeline.SetMode(_loopPlaybackActive
                ? CharacterTestRuntimeAiPlannerDevTimelineMode.LoopReview
                : resumeLive
                    ? CharacterTestRuntimeAiPlannerDevTimelineMode.Live
                    : CharacterTestRuntimeAiPlannerDevTimelineMode.Review);

            double cpuMilliseconds = TicksToMilliseconds(work.CpuTicks);
            double microsecondsPerFrame = work.ProcessedFrames > 0
                ? cpuMilliseconds * 1000d / work.ProcessedFrames
                : 0d;
            string prefix = work.Mode == DeterministicReplayMode.ColdVerify
                ? "Cold Bootstrap Verify PASS"
                : "Warm Replay PASS";
            _statusMessage = prefix
                + " at RuntimeFrame "
                + work.Target.Frame
                + " | reset "
                + TicksToMilliseconds(work.ResetTicks + work.BootstrapTicks)
                    .ToString("0.00", CultureInfo.InvariantCulture)
                + " ms | replay "
                + cpuMilliseconds.ToString("0.00", CultureInfo.InvariantCulture)
                + " ms | presentation "
                + TicksToMilliseconds(work.PresentationTicks).ToString("0.00", CultureInfo.InvariantCulture)
                + " ms | "
                + microsecondsPerFrame.ToString("0.0", CultureInfo.InvariantCulture)
                + " us/frame.";
            if (missingRequestedAgent)
                _statusMessage += " 目标帧中不存在原智能体，已保留可用选择。";
            if (work.HasValidationWarning)
            {
                _statusMessage = "RuntimeFrame "
                    + work.Target.Frame
                    + " 已定位并刷新画面；重算校验存在差异："
                    + work.ValidationWarning;
            }
            _lastSeekSucceeded = true;
            FinishDeterministicReplay(work);
        }

        private void RestoreCommittedPresentationSurface()
        {
            int frameValue = _committedFrameBeforeSeek;
            if (frameValue <= 0)
                return;

            var frame = new RuntimeFrame(frameValue);
            _frameBuffer.SelectFrame(frameValue);
            _timeline.SelectFrame(frameValue);
            _lastCapturedRuntimeFrame = frameValue;
            TrySelectAgentEntityId(_committedAgentBeforeSeek);
            ClampSelectedAgentIndex();

            CharacterTestCharacterRuntimeModule characterRuntime = _context?.Slice?.CharacterRuntime;
            characterRuntime?.ForceSyncRuntimeAiLabPresentation(frame);
            _gameManager?.RefreshPresentationForRuntimeAiPlannerDev(
                resubmitCharacterAnimation: false);

            if (!_testStateCaptures.TryGet(frameValue, out RuntimeAiLabTestStateCapture capture))
                return;
            if (characterRuntime != null
                && !characterRuntime.RestoreRuntimeAiLabViewRootPoses(
                    capture.PresentationDiagnostics,
                    out string rootPoseDiagnostic))
            {
                _sessionContinuable = false;
                _statusMessage = "Committed preview restore failed. Reset or exit the session. Reason: "
                    + rootPoseDiagnostic;
                return;
            }
            characterRuntime?.RestoreAnimationPresentationCheckpoint(
                capture.Presentation?.Animation,
                frame,
                out _);
            if (capture.Presentation?.VfxState != null && _context?.Slice?.VfxRuntime != null)
            {
                RuntimeSaveStateResult<bool> vfxResult = new VfxRuntimeSaveStateProvider(_context.Slice.VfxRuntime)
                    .RestoreSaveState(capture.Presentation.VfxState);
                if (vfxResult.Success)
                    _context.Slice.Services.Modules.UnityVfxPresenter?.SynchronizeActiveInstances();
            }
        }

        private void FailDeterministicReplay(string diagnostic)
        {
            CancelTimelineScrub();
            DeterministicReplayWork work = _deterministicReplayWork;
            string failure = diagnostic ?? "确定性回放失败。";
            if (work != null && work.Mode == DeterministicReplayMode.Recovery)
            {
                work.Network?.SetNetworkRollbackReplayActive(false);
                _deterministicReplayWork = null;
                _seekInProgress = false;
                _sessionContinuable = false;
                _frameSelection.CompleteRecovery(success: false);
                SetPaused(true);
                _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Review);
                _statusMessage = "历史定位失败，且恢复旧帧也失败。当前会话只能重置或退出。原因：" + failure;
                _lastSeekSucceeded = false;
                _resumeLiveAfterReplay = false;
                return;
            }

            _statusMessage = failure;
            _lastSeekSucceeded = false;
            _resumeLiveAfterReplay = false;
            _loopPlaybackActive = false;
            _loopPlaybackAccumulatorSeconds = 0d;
            _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Review);
            if (work == null)
            {
                _seekInProgress = false;
                return;
            }

            _failedSeekDiagnostic = failure;
            work.Network?.SetNetworkRollbackReplayActive(false);
            _deterministicReplayWork = null;
            _seekInProgress = false;
            _frameSelection.BeginRecovery();
            if (!BeginSeekRecovery(work.Network, work.Participant))
            {
                _sessionContinuable = false;
                _frameSelection.CompleteRecovery(success: false);
                SetPaused(true);
                _statusMessage = "历史定位失败，且无法恢复旧帧。当前会话只能重置或退出。原因：" + failure;
            }
        }

        private void FinishDeterministicReplay(DeterministicReplayWork work)
        {
            work.Network?.SetNetworkRollbackReplayActive(false);
            _deterministicReplayWork = null;
            _seekInProgress = false;
            _activeSeekRequest = default;
        }

        private void SetRuntimeAiLabHistoricalNetworkReviewActive(bool active)
        {
            if (_context?.NetworkRunner is CharacterTestNetworkSessionRunner loopbackRunner)
                loopbackRunner.SetRuntimeAiLabHistoricalReviewActive(active);
        }

        private static int ResolveAgentForRecord(
            CharacterTestRuntimeAiPlannerDevTimelineRecord record,
            int requestedAgent)
        {
            var agents = record?.FrameSnapshot?.Agents;
            if (agents == null || agents.Count <= 0)
                return 0;
            if (requestedAgent > 0)
            {
                for (int i = 0; i < agents.Count; i++)
                {
                    if (agents[i].EntityId == requestedAgent)
                        return requestedAgent;
                }
            }
            return agents[0].EntityId;
        }

        private bool BeginSeekRecovery(
            CharacterTestSliceNetworkServices network,
            CharacterTestNetworkSnapshotParticipant participant)
        {
            if (_committedFrameBeforeSeek <= 0
                || !_timeline.TryGetFrame(_committedFrameBeforeSeek, out CharacterTestRuntimeAiPlannerDevTimelineRecord target)
                || network == null
                || participant == null)
            {
                return false;
            }

            network.SetNetworkRollbackReplayActive(true);
            long resetStart = Stopwatch.GetTimestamp();
            RuntimeAiLabInitialReplayBaseline baseline = _initialReplayBaseline;
            if (!RestoreInitialReplayBaseline(network, participant, baseline, out _))
            {
                network.SetNetworkRollbackReplayActive(false);
                return false;
            }

            _deterministicReplayWork = new DeterministicReplayWork(
                DeterministicReplayMode.Recovery,
                target,
                wasPaused: true,
                network,
                participant,
                (int)baseline.Frame.Value + 1,
                Math.Max(0, target.Frame - (int)baseline.Frame.Value),
                Stopwatch.GetTimestamp() - resetStart);
            _seekInProgress = true;
            ProcessDeterministicReplay(ignoreBudget: true);
            return !_sessionContinuable || _deterministicReplayWork != null || _timeline.SelectedFrame == target.Frame;
        }

        private bool TrySelectAgentEntityId(int entityId)
        {
            if (!_frameBuffer.TryGetSelected(out CharacterTestRuntimeAiPlannerDevFrameSnapshot frame))
                return false;
            for (int i = 0; i < frame.Agents.Count; i++)
            {
                if (frame.Agents[i].EntityId != entityId)
                    continue;
                _selectedAgentIndex = i;
                return true;
            }
            return false;
        }

        private void CancelDeterministicReplay(string statusMessage)
        {
            CancelTimelineScrub();
            DeterministicReplayWork work = _deterministicReplayWork;
            if (work == null)
                return;

            work.Network?.SetNetworkRollbackReplayActive(false);
            _deterministicReplayWork = null;
            _seekInProgress = false;
            _resumeLiveAfterReplay = false;
            SetPaused(true);
            _statusMessage = statusMessage ?? "Deterministic replay cancelled.";
        }

        private void UpdateReplayProgressStatus()
        {
            DeterministicReplayWork work = _deterministicReplayWork;
            if (work == null)
                return;

            _statusMessage = (work.Mode == DeterministicReplayMode.ColdVerify
                    ? "Cold Bootstrap Verify "
                    : "Warm Replay ")
                + work.ProcessedFrames
                + "/"
                + work.TotalFrames
                + " -> RuntimeFrame "
                + work.Target.Frame
                + ". Reset to cancel.";
        }

        private static double ElapsedMilliseconds(long startTimestamp)
        {
            return TicksToMilliseconds(Stopwatch.GetTimestamp() - startTimestamp);
        }

        private static double TicksToMilliseconds(long ticks)
        {
            return ticks * 1000d / Stopwatch.Frequency;
        }


        private enum DeterministicReplayMode
        {
            Warm = 0,
            ColdVerify = 1,
            Recovery = 2
        }

        private sealed class DeterministicReplayWork
        {
            public DeterministicReplayWork(
                DeterministicReplayMode mode,
                CharacterTestRuntimeAiPlannerDevTimelineRecord target,
                bool wasPaused,
                CharacterTestSliceNetworkServices network,
                CharacterTestNetworkSnapshotParticipant participant,
                int nextFrame,
                int totalFrames,
                long resetTicks,
                long selectionTransactionId = 0L,
                CharacterTestRuntimeAiLabFrameSelectionTarget selectionTarget = default)
            {
                Mode = mode;
                Target = target;
                WasPaused = wasPaused;
                Network = network;
                Participant = participant;
                NextFrame = nextFrame;
                TotalFrames = totalFrames;
                ResetTicks = resetTicks;
                SelectionTransactionId = selectionTransactionId;
                SelectionTarget = selectionTarget;
            }

            public DeterministicReplayMode Mode { get; }
            public CharacterTestRuntimeAiPlannerDevTimelineRecord Target { get; }
            public bool WasPaused { get; }
            public CharacterTestSliceNetworkServices Network { get; }
            public CharacterTestNetworkSnapshotParticipant Participant { get; }
            public int NextFrame { get; set; }
            public int TotalFrames { get; }
            public int ProcessedFrames { get; set; }
            public long CpuTicks { get; set; }
            public long ResetTicks { get; }
            public long BootstrapTicks { get; set; }
            public long PresentationTicks { get; set; }
            public long SelectionTransactionId { get; }
            public CharacterTestRuntimeAiLabFrameSelectionTarget SelectionTarget { get; }
            public string ValidationWarning { get; private set; } = string.Empty;
            public bool HasValidationWarning => !string.IsNullOrEmpty(ValidationWarning);

            public void RecordValidationWarning(string diagnostic)
            {
                if (string.IsNullOrEmpty(diagnostic) || HasValidationWarning)
                    return;
                ValidationWarning = diagnostic;
            }
        }
    }
}
