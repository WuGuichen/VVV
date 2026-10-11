using System;
using System.Collections.Generic;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.Vfx;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private bool CaptureInitialReplayBaseline()
        {
            using var startupProfile = CharacterTestStartupProfiler.CaptureReplayBaseline.Auto();
            if (!TryCaptureReplayBaseline(out RuntimeAiLabInitialReplayBaseline baseline))
                return false;

            _initialReplayBaseline = baseline;
            return true;
        }

        private bool TryCaptureReplayBaseline(out RuntimeAiLabInitialReplayBaseline baseline)
        {
            baseline = null;
            GameSlice slice = _context?.Slice;
            if (slice == null || _snapshotParticipant == null)
            {
                _statusMessage = "Runtime AI Lab initial replay baseline is unavailable.";
                return false;
            }

            RuntimeFrame frame = slice.CurrentFrame;
            CharacterTestAuthorityRollbackCheckpoint authority =
                CharacterTestAuthorityRollbackCheckpoint.Capture(_snapshotParticipant, frame);
            CharacterTestAnimationPresentationCheckpoint animation =
                slice.CharacterRuntime?.CaptureAnimationPresentationCheckpoint(frame);
            RuntimeSaveState vfxState = slice.VfxRuntime != null
                ? new VfxRuntimeSaveStateProvider(slice.VfxRuntime).CaptureSaveState().Value
                : null;
            RuntimeAiLabGameplayTransientState gameplayTransientState =
                RuntimeAiLabGameplayTransientState.Capture(
                    slice.CharacterRuntime.World.GameplayComponentWorld);
            var presentation = new RuntimeAiLabPresentationCheckpoint(
                frame,
                animation,
                vfxState,
                animation?.Diagnostic);
            long authorityHash = _snapshotParticipant.ComputeStateHash(frame);
            baseline = new RuntimeAiLabInitialReplayBaseline(
                frame,
                slice.Services.ElapsedSeconds,
                authority,
                presentation,
                slice.Services.RuntimeCommands.CommandBuffer.CaptureSnapshot(),
                slice.StoryModule?.CommandBuffer?.CaptureSnapshot(),
                slice.VfxRuntime?.CommandBuffer?.CaptureSnapshot(),
                gameplayTransientState,
                slice.CombatTraceSystem?.CaptureRuntimeAiLabReplayState(),
                authorityHash,
                _replayConfigurationRevision);
            if (baseline.Available)
                return true;

            baseline = null;
            _statusMessage = "Runtime AI Lab could not capture a frame-aligned initial presentation baseline.";
            return false;
        }

        private bool RestoreInitialReplayBaseline(
            CharacterTestSliceNetworkServices network,
            CharacterTestNetworkSnapshotParticipant participant,
            RuntimeAiLabInitialReplayBaseline baseline,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (baseline == null || !baseline.Available)
            {
                diagnostic = "Warm Replay initial baseline is missing.";
                return false;
            }
            if (baseline.ConfigurationRevision != _replayConfigurationRevision)
            {
                diagnostic = "Warm Replay configuration revision changed; Reset and Start are required.";
                return false;
            }
            if (!RestoreBaselineRevision(out diagnostic))
            {
                diagnostic = "Warm Replay evaluator revision reset failed: " + diagnostic;
                return false;
            }

            network?.RestoreNetworkRollbackRuntimeState(baseline.Frame, baseline.ElapsedSeconds);
            NetworkSnapshotApplyResult authorityResult = baseline.Authority.Restore(
                participant,
                baseline.Frame,
                preserveLocalPredictedEntity: false);
            if (!authorityResult.Success)
            {
                diagnostic = "Warm Replay authority reset failed: " + authorityResult.Message;
                return false;
            }
            baseline.GameplayTransientState.Restore(
                _context.Slice.CharacterRuntime.World.GameplayComponentWorld);

            _context.Slice.CombatTraceSystem?.RestoreRuntimeAiLabReplayState(baseline.CombatTrace);


            CharacterTestRuntimeCommandModule runtimeCommands =
                _context.Slice.Services.RuntimeCommands;
            if (runtimeCommands == null)
            {
                diagnostic = "Warm Replay runtime command module is unavailable.";
                return false;
            }
            runtimeCommands.CommandBuffer.RestoreSnapshot(baseline.CommandBuffer);
            if (baseline.StoryCommandBuffer.HasValue
                && _context.Slice.StoryModule?.CommandBuffer != null)
            {
                _context.Slice.StoryModule.CommandBuffer.RestoreSnapshot(
                    baseline.StoryCommandBuffer.Value);
            }
            if (baseline.VfxCommandBuffer.HasValue
                && _context.Slice.VfxRuntime?.CommandBuffer != null)
            {
                _context.Slice.VfxRuntime.CommandBuffer.RestoreSnapshot(
                    baseline.VfxCommandBuffer.Value);
            }

            CharacterTestCharacterRuntimeModule characterRuntime = _context.Slice.CharacterRuntime;
            characterRuntime?.ResetRuntimeAiLabReplayTransientState();
            characterRuntime?.ForceSyncRuntimeAiLabPresentation(baseline.Frame);
            if (characterRuntime != null
                && !characterRuntime.RestoreAnimationPresentationCheckpoint(
                    baseline.Presentation.Animation,
                    baseline.Frame,
                    out diagnostic))
            {
                diagnostic = "Warm Replay animation reset failed: " + diagnostic;
                return false;
            }

            if (baseline.Presentation.VfxState != null && _context.Slice.VfxRuntime != null)
            {
                RuntimeSaveStateResult<bool> vfxResult =
                    new VfxRuntimeSaveStateProvider(_context.Slice.VfxRuntime)
                        .RestoreSaveState(baseline.Presentation.VfxState);
                if (!vfxResult.Success)
                {
                    diagnostic = "Warm Replay VFX reset failed.";
                    return false;
                }
                _context.Slice.Services.Modules.UnityVfxPresenter?.SynchronizeActiveInstances();
            }

            long restoredHash = participant.ComputeStateHash(baseline.Frame);
            if (restoredHash == baseline.AuthorityHash)
                return true;

            diagnostic = "Warm Replay baseline hash mismatch: expected "
                + baseline.AuthorityHash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)
                + ", actual "
                + restoredHash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)
                + ". "
                + BuildPlannerRestoreHashDiagnostic(baseline.Authority.Document.RuntimeAiPlannerState);
            return false;
        }

        private string BuildPlannerRestoreHashDiagnostic(
            CharacterTestRuntimeAiPlannerStateSnapshot expected)
        {
            var expectedHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerLabFrameState, RuntimeHashSchemaIds.Version1);
            var actualHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerLabFrameState, RuntimeHashSchemaIds.Version1);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(expectedHash, expected);
            CharacterTestRuntimeAiPlannerStateSnapshot actual =
                _context?.Slice?.Services.Modules.RuntimeAiPlannerCommands?.CreateStateSnapshot();
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(actualHash, actual);
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] expectedLogic =
                expected?.BehaviorLogicSlots ?? Array.Empty<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>();
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] actualLogic =
                actual?.BehaviorLogicSlots ?? Array.Empty<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>();
            return "planner expected "
                + expectedHash.ToHash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture)
                + ", actual "
                + actualHash.ToHash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture)
                + "; logic slots "
                + expectedLogic.Length
                + "/"
                + actualLogic.Length
                + ", authority "
                + (expectedLogic.Length > 0 ? expectedLogic[0].AuthorityHash : 0L)
                + "/"
                + (actualLogic.Length > 0 ? actualLogic[0].AuthorityHash : 0L)
                + ", agents "
                + (expectedLogic.Length > 0 ? expectedLogic[0].Agents?.Length ?? 0 : 0)
                + "/"
                + (actualLogic.Length > 0 ? actualLogic[0].Agents?.Length ?? 0 : 0)
                + ".";
        }

        public bool ToggleStateCapture()
        {
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running || _seekInProgress)
            {
                _statusMessage = "Start the Runtime AI Lab before changing state capture.";
                return false;
            }

            bool enabled = _testStateCaptures.Toggle();
            _statusMessage = enabled
                ? "Test state capture enabled. Authority and presentation checkpoints are Lab-only."
                : "Test state capture disabled.";
            if (enabled)
                CaptureTestStateIfNeeded(force: true);
            return true;
        }

        public bool ValidateRecordedState()
        {
            if (!TryResolveSelectedRecord(out CharacterTestRuntimeAiPlannerDevTimelineRecord record))
                return false;

            CharacterTestRuntimeAiLabAuthoritySample expected =
                new CharacterTestRuntimeAiLabAuthoritySample(new RuntimeFrame(record.Frame), record.StateHash);
            CharacterTestRuntimeAiLabAuthoritySample actual =
                CharacterTestRuntimeAiLabAuthorityProbe.Capture(_context.Slice, _snapshotParticipant);
            CharacterTestRuntimeAiLabFrameSyncReport authority =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(expected, actual);
            CharacterTestRuntimeAiLabFrameSyncReport presentation =
                ComparePresentationWithRecordedCapture(record.Frame);
            _lastReplayResult = new RuntimeAiLabDeterministicReplayResult(
                _context.Slice.CurrentFrame,
                authority,
                presentation);
            _statusMessage = authority.Passed
                ? "Frame Sync PASS at RuntimeFrame " + record.Frame + "."
                : authority.SummaryText;
            return authority.Passed;
        }

        public bool SyncRecordedState()
        {
            if (!TryResolveSelectedRecord(out CharacterTestRuntimeAiPlannerDevTimelineRecord record))
                return false;
            if (_context == null || !_context.Paused || _loopPlaybackActive || _seekInProgress)
            {
                _statusMessage = "Pause on a captured frame before synchronizing recorded test state.";
                return false;
            }
            if (!_testStateCaptures.TryGet(record.Frame, out RuntimeAiLabTestStateCapture capture))
            {
                _statusMessage = "No manually captured test state exists for RuntimeFrame " + record.Frame + ".";
                return false;
            }

            _seekInProgress = true;
            CharacterTestSliceNetworkServices network = _context.Slice?.Services.Network;
            network?.SetNetworkRollbackReplayActive(true);
            try
            {
                RuntimeFrame frame = new RuntimeFrame(record.Frame);
                network?.RestoreNetworkRollbackRuntimeState(frame, capture.ElapsedSeconds);
                NetworkSnapshotApplyResult result = capture.Authority.Restore(
                    _snapshotParticipant,
                    frame,
                    preserveLocalPredictedEntity: false);
                if (!result.Success)
                {
                    _statusMessage = "Manual authority restore failed: " + result.Message;
                    return false;
                }

                CharacterTestCharacterRuntimeModule characterRuntime = _context.Slice.CharacterRuntime;
                characterRuntime?.ForceSyncRuntimeAiLabPresentation(frame);
                _frameBuffer.SelectFrame(record.Frame);
                _timeline.SelectFrame(record.Frame);
                _gameManager?.RefreshPresentationForRuntimeAiPlannerDev();

                string presentationDiagnostic = string.Empty;
                bool presentationRestored = characterRuntime == null
                    || characterRuntime.RestoreAnimationPresentationCheckpoint(
                        capture.Presentation?.Animation,
                        frame,
                        out presentationDiagnostic);
                if (capture.Presentation?.VfxState != null && _context.Slice.VfxRuntime != null)
                {
                    RuntimeSaveStateResult<bool> vfxResult =
                        new VfxRuntimeSaveStateProvider(_context.Slice.VfxRuntime)
                            .RestoreSaveState(capture.Presentation.VfxState);
                    if (!vfxResult.Success)
                    {
                        presentationRestored = false;
                        presentationDiagnostic = "VFX presentation restore failed.";
                    }
                }

                ValidateRecordedState();
                _statusMessage = presentationRestored
                    ? "Recorded test state synchronized at RuntimeFrame " + record.Frame + "."
                    : "Authority synchronized; Presentation warning: " + presentationDiagnostic;
                return true;
            }
            finally
            {
                network?.SetNetworkRollbackReplayActive(false);
                _seekInProgress = false;
            }
        }

        private void CaptureTestStateIfNeeded(bool force)
        {
            if (!_testStateCaptures.Enabled || _seekInProgress || _loopPlaybackActive)
                return;
            GameSlice slice = _context?.Slice;
            if (slice == null || _snapshotParticipant == null)
                return;

            int frameValue = (int)slice.CurrentFrame.Value;
            if (!force && frameValue == _lastTestStateCapturedFrame)
                return;
            var frame = new RuntimeFrame(frameValue);
            CharacterTestAuthorityRollbackCheckpoint authority =
                CharacterTestAuthorityRollbackCheckpoint.Capture(_snapshotParticipant, frame);
            CharacterTestAnimationPresentationCheckpoint animation =
                slice.CharacterRuntime?.CaptureAnimationPresentationCheckpoint(frame);
            RuntimeSaveState vfxState = slice.VfxRuntime != null
                ? new VfxRuntimeSaveStateProvider(slice.VfxRuntime).CaptureSaveState().Value
                : null;
            CharacterTestRuntimeAiLabPresentationDiagnosticSample presentationDiagnostics =
                CapturePresentationDiagnostics();
            var presentation = new RuntimeAiLabPresentationCheckpoint(
                frame,
                animation,
                vfxState,
                animation?.Diagnostic);
            if (_testStateCaptures.TryAdd(new RuntimeAiLabTestStateCapture(
                frame,
                slice.Services.ElapsedSeconds,
                authority,
                presentation,
                presentationDiagnostics)))
            {
                _lastTestStateCapturedFrame = frameValue;
            }
            else if (_testStateCaptures.CapacityReached)
            {
                _statusMessage = "Presentation checkpoint budget reached; command/hash history continues.";
            }
        }

        private CharacterTestRuntimeAiLabFrameSyncReport ComparePresentationWithRecordedCapture(int frame)
        {
            if (!_testStateCaptures.TryGet(frame, out RuntimeAiLabTestStateCapture capture))
                return null;

            return CharacterTestRuntimeAiLabPresentationDiagnosticProbe.Compare(
                capture.PresentationDiagnostics,
                CapturePresentationDiagnostics());
        }

        private CharacterTestRuntimeAiLabPresentationDiagnosticSample CapturePresentationDiagnostics()
        {
            TryBuildSelectedPreview(out CharacterTestRuntimeAiPlannerDevPreviewItem preview);
            return CharacterTestRuntimeAiLabPresentationDiagnosticProbe.Capture(_context?.Slice, preview);
        }

        private bool TryResolveSelectedRecord(out CharacterTestRuntimeAiPlannerDevTimelineRecord record)
        {
            record = null;
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running || _context?.Slice == null)
            {
                _statusMessage = "Start the Runtime AI Lab before validating state.";
                return false;
            }
            if (!_timeline.TryGetSelected(out record))
            {
                _statusMessage = "Select a recorded RuntimeFrame first.";
                return false;
            }
            return true;
        }

    }
}
