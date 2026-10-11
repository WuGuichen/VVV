using System;
using System.Globalization;
using System.IO;
using System.Text;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Runtime;
using Newtonsoft.Json;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestNetworkSyncProbeResult
    {
        Idle = 0,
        Running = 1,
        Pass = 2,
        Warning = 3,
        Fail = 4,
        Rejected = 5,
        Cancelled = 6
    }

    public enum CharacterTestNetworkSyncProbeInputPattern
    {
        MoveStopLoop = 0
    }

    public sealed class CharacterTestNetworkSyncProbeStatus
    {
        public CharacterTestNetworkSyncProbeResult Result { get; set; } =
            CharacterTestNetworkSyncProbeResult.Idle;
        public bool IsRunning { get; set; }
        public bool CanStart { get; set; }
        public bool AutoRunEnabled { get; set; }
        public bool AutoRunPending { get; set; }
        public string ResultCode { get; set; } = "IDLE";
        public string Summary { get; set; } = "Sync Probe: not run.";
        public string Detail { get; set; } = string.Empty;
        public double ElapsedSeconds { get; set; }
        public int ElapsedFrames { get; set; }
        public int SubmittedCommandCount { get; set; }
        public int RejectedCommandCount { get; set; }
        public int DeltaAuthoritativeCommandReceived { get; set; }
        public int DeltaAuthoritativeCommandEcho { get; set; }
        public int DeltaAuthoritativeCommandEnqueued { get; set; }
        public int DeltaAuthoritativeCommandLate { get; set; }
        public int DeltaAuthoritativeCommandDropped { get; set; }
        public int DeltaSnapshotRequest { get; set; }
        public int DeltaFullSnapshot { get; set; }
        public int DeltaDigestSnapshot { get; set; }
        public long DeltaRuntimeCommandBytes { get; set; }
        public long DeltaSnapshotBytes { get; set; }
        public int MaxPendingTransportMessages { get; set; }
        public int MaxSentPayloadBytes { get; set; }
        public int MaxFrameDriftFrames { get; set; }
        public CharacterTestNetworkSyncProbeInputPattern InputPattern { get; set; } =
            CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop;
        public int ZeroIntentCommandCount { get; set; }
        public int NonZeroIntentCommandCount { get; set; }
        public long LastZeroIntentCommandFrame { get; set; } = -1L;
        public long LastNonZeroIntentCommandFrame { get; set; } = -1L;
        public string LogPath { get; set; } = string.Empty;
        public bool LogWriteFailed { get; set; }
        public string LastError { get; set; } = string.Empty;

        public static CharacterTestNetworkSyncProbeStatus Idle(
            bool canStart,
            string summary = null,
            bool autoRunEnabled = false,
            bool autoRunPending = false)
        {
            return new CharacterTestNetworkSyncProbeStatus
            {
                CanStart = canStart,
                AutoRunEnabled = autoRunEnabled,
                AutoRunPending = autoRunPending,
                Summary = string.IsNullOrWhiteSpace(summary) ? "Sync Probe: not run." : summary.Trim()
            };
        }

        public static CharacterTestNetworkSyncProbeStatus Rejected(string summary)
        {
            return new CharacterTestNetworkSyncProbeStatus
            {
                Result = CharacterTestNetworkSyncProbeResult.Rejected,
                ResultCode = "REJECTED",
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? "Sync Probe rejected."
                    : summary.Trim()
            };
        }
    }

    internal sealed class CharacterTestNetworkSyncProbe
    {
        public static readonly RuntimeTraceIdentity TraceId = new RuntimeTraceIdentity(1015);
        public const int DefaultWarmupFrames = 30;
        public const double DefaultDurationSeconds = 15d;
        public const double DefaultProviderModeTimeoutSeconds = 30d;
        public const int DefaultMaxFrames = 900;
        public const int MoveStopLoopCycleFrames = 240;
        public const int MaxPayloadWarningBytes = 8 * 1024;
        public const int PendingTransportWarningMessages = 8;
        public const int FrameDriftWarningFrames = 12;
        public const int SnapshotRequestStormThreshold = 1;
        public const int FullSnapshotStormThreshold = 2;

        private CharacterTestNetworkSyncProbeStatus _lastStatus =
            CharacterTestNetworkSyncProbeStatus.Idle(false);
        private CharacterTestNetworkLoopbackDiagnostics _startDiagnostics =
            CharacterTestNetworkLoopbackDiagnostics.Unavailable;
        private CharacterTestNetworkLoopbackDiagnostics _lastDiagnostics =
            CharacterTestNetworkLoopbackDiagnostics.Unavailable;
        private bool _running;
        private double _elapsedSeconds;
        private int _elapsedFrames;
        private int _submittedCommandCount;
        private int _rejectedCommandCount;
        private int _targetEntityId;
        private int _maxPendingTransportMessages;
        private int _maxSentPayloadBytes;
        private int _maxFrameDriftFrames;
        private int _warmupFramesRemaining;
        private long _lastSubmittedCommandFrame = long.MinValue;
        private CharacterTestNetworkSyncProbeInputPattern _inputPattern =
            CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop;
        private int _zeroIntentCommandCount;
        private int _nonZeroIntentCommandCount;
        private long _lastZeroIntentCommandFrame = -1L;
        private long _lastNonZeroIntentCommandFrame = -1L;
        private CharacterTestNetworkSyncProbeLogRecorder _logRecorder;
        private string _lastError = string.Empty;

        public CharacterTestNetworkSyncProbeStatus CaptureStatus(
            bool canStart,
            bool autoRunEnabled = false,
            bool autoRunPending = false)
        {
            CharacterTestNetworkSyncProbeStatus status = _lastStatus ?? CharacterTestNetworkSyncProbeStatus.Idle(canStart);
            status.CanStart = canStart;
            status.AutoRunEnabled = autoRunEnabled;
            status.AutoRunPending = autoRunPending;
            return status;
        }

        public bool IsRunning => _running;

        public CharacterTestNetworkSyncProbeStatus Start(
            GameSlice slice,
            CharacterTestNetworkLoopbackDiagnostics diagnostics,
            CharacterTestNetworkSessionStatus sessionStatus)
        {
            if (_running)
                return _lastStatus;

            string rejection = ValidateStart(slice, diagnostics, sessionStatus);
            if (!string.IsNullOrWhiteSpace(rejection))
            {
                _lastStatus = CharacterTestNetworkSyncProbeStatus.Rejected(rejection);
                return _lastStatus;
            }

            _running = true;
            _elapsedSeconds = 0d;
            _elapsedFrames = 0;
            _submittedCommandCount = 0;
            _rejectedCommandCount = 0;
            _targetEntityId = diagnostics.OwnedEntityId;
            _startDiagnostics = diagnostics;
            _lastDiagnostics = diagnostics;
            _maxPendingTransportMessages = Math.Max(0, diagnostics.PendingTransportMessages);
            _maxSentPayloadBytes = Math.Max(0, diagnostics.MaxSentPayloadBytes);
            _maxFrameDriftFrames = Math.Abs(ResolveCurrentFrameDriftFrames(diagnostics));
            _warmupFramesRemaining = DefaultWarmupFrames;
            _lastSubmittedCommandFrame = long.MinValue;
            _inputPattern = CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop;
            _zeroIntentCommandCount = 0;
            _nonZeroIntentCommandCount = 0;
            _lastZeroIntentCommandFrame = -1L;
            _lastNonZeroIntentCommandFrame = -1L;
            _lastError = string.Empty;
            CloseLogRecorder();
            _logRecorder = CharacterTestNetworkSyncProbeLogRecorder.Start(
                sessionStatus,
                diagnostics,
                _inputPattern,
                _targetEntityId,
                DefaultMaxFrames,
                DefaultDurationSeconds);
            _lastStatus = CreateRunningStatus();
            return _lastStatus;
        }

        public CharacterTestNetworkSyncProbeStatus Stop(bool cancelled)
        {
            if (!_running)
                return _lastStatus;

            _running = false;
            _lastStatus = cancelled
                ? CreateStatus(
                    CharacterTestNetworkSyncProbeResult.Cancelled,
                    "CANCELLED",
                    "Sync Probe CANCELLED: submitted="
                    + _submittedCommandCount
                    + " frames="
                    + _elapsedFrames,
                    BuildRunDetail(),
                    _elapsedFrames,
                    _elapsedSeconds,
                    _submittedCommandCount,
                    _rejectedCommandCount,
                    Delta(_lastDiagnostics.AuthoritativeCommandReceivedCount, _startDiagnostics.AuthoritativeCommandReceivedCount),
                    Delta(_lastDiagnostics.AuthoritativeCommandEchoCount, _startDiagnostics.AuthoritativeCommandEchoCount),
                    Delta(_lastDiagnostics.AuthoritativeCommandEnqueuedCount, _startDiagnostics.AuthoritativeCommandEnqueuedCount),
                    Delta(_lastDiagnostics.AuthoritativeCommandLateCount, _startDiagnostics.AuthoritativeCommandLateCount),
                    Delta(_lastDiagnostics.AuthoritativeCommandDroppedCount, _startDiagnostics.AuthoritativeCommandDroppedCount),
                    Delta(_lastDiagnostics.SnapshotRequestCount, _startDiagnostics.SnapshotRequestCount),
                    Delta(
                        _lastDiagnostics.FullSnapshotSentCount + _lastDiagnostics.FullSnapshotReceivedCount,
                        _startDiagnostics.FullSnapshotSentCount + _startDiagnostics.FullSnapshotReceivedCount),
                    Delta(
                        _lastDiagnostics.DigestSnapshotSentCount + _lastDiagnostics.DigestSnapshotReceivedCount,
                        _startDiagnostics.DigestSnapshotSentCount + _startDiagnostics.DigestSnapshotReceivedCount),
                    Delta(_lastDiagnostics.RuntimeCommandSentBytes, _startDiagnostics.RuntimeCommandSentBytes),
                    Delta(_lastDiagnostics.SnapshotSentBytes, _startDiagnostics.SnapshotSentBytes),
                    _maxPendingTransportMessages,
                    _maxSentPayloadBytes,
                    _maxFrameDriftFrames,
                    _lastError,
                    _inputPattern,
                    _zeroIntentCommandCount,
                    _nonZeroIntentCommandCount,
                    _lastZeroIntentCommandFrame,
                    _lastNonZeroIntentCommandFrame,
                    _logRecorder != null ? _logRecorder.FilePath : string.Empty,
                    _logRecorder != null && _logRecorder.WriteFailed)
                : CharacterTestNetworkSyncProbeStatus.Idle(false);
            _logRecorder?.RecordComplete(
                cancelled ? "cancelled" : "stopped",
                _elapsedFrames,
                _elapsedSeconds,
                _lastStatus);
            CloseLogRecorder();
            return _lastStatus;
        }

        public void Reset()
        {
            _running = false;
            _elapsedSeconds = 0d;
            _elapsedFrames = 0;
            _submittedCommandCount = 0;
            _rejectedCommandCount = 0;
            _targetEntityId = 0;
            _startDiagnostics = CharacterTestNetworkLoopbackDiagnostics.Unavailable;
            _lastDiagnostics = CharacterTestNetworkLoopbackDiagnostics.Unavailable;
            _maxPendingTransportMessages = 0;
            _maxSentPayloadBytes = 0;
            _maxFrameDriftFrames = 0;
            _warmupFramesRemaining = 0;
            _lastSubmittedCommandFrame = long.MinValue;
            _inputPattern = CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop;
            _zeroIntentCommandCount = 0;
            _nonZeroIntentCommandCount = 0;
            _lastZeroIntentCommandFrame = -1L;
            _lastNonZeroIntentCommandFrame = -1L;
            CloseLogRecorder();
            _lastError = string.Empty;
            _lastStatus = CharacterTestNetworkSyncProbeStatus.Idle(false);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            double deltaTime,
            GameSlice slice,
            CharacterTestNetworkLoopbackDiagnostics diagnostics,
            IRuntimeLogger logger,
            bool submitProbeCommands = true)
        {
            if (!_running)
                return;

            if (_warmupFramesRemaining > 0)
            {
                _warmupFramesRemaining--;
                if (diagnostics.Available)
                {
                    _startDiagnostics = diagnostics;
                    _lastDiagnostics = diagnostics;
                    _maxPendingTransportMessages = Math.Max(0, diagnostics.PendingTransportMessages);
                    _maxSentPayloadBytes = Math.Max(
                        0,
                        Math.Max(diagnostics.MaxSentPayloadBytes, diagnostics.LastSentPayloadBytes));
                    _maxFrameDriftFrames = Math.Abs(ResolveCurrentFrameDriftFrames(diagnostics));
                }

                _lastStatus = CreateRunningStatus();
                return;
            }

            _elapsedSeconds += Math.Max(0d, deltaTime);
            _lastDiagnostics = diagnostics.Available ? diagnostics : _lastDiagnostics;
            SampleMaxima(diagnostics);

            if (submitProbeCommands)
            {
                _elapsedFrames++;
                SubmitProbeCommandIfFrameAdvanced(slice, diagnostics);
            }
            else
            {
                int nextFrameIndex = _elapsedFrames + 1;
                if (RecordProvidedInputFrameIfFrameAdvanced(slice, diagnostics, nextFrameIndex))
                    _elapsedFrames++;
            }

            _lastStatus = CreateRunningStatus();
            bool completed = _elapsedFrames >= DefaultMaxFrames
                || (submitProbeCommands && _elapsedSeconds >= DefaultDurationSeconds)
                || (!submitProbeCommands && _elapsedSeconds >= DefaultProviderModeTimeoutSeconds);
            if (!completed)
                return;

            _running = false;
            _lastStatus = Evaluate(
                _startDiagnostics,
                _lastDiagnostics,
                _submittedCommandCount,
                _rejectedCommandCount,
                _elapsedFrames,
                _elapsedSeconds,
                _maxPendingTransportMessages,
                _maxSentPayloadBytes,
                _maxFrameDriftFrames,
                _lastError,
                _inputPattern,
                _zeroIntentCommandCount,
                _nonZeroIntentCommandCount,
                _lastZeroIntentCommandFrame,
                _lastNonZeroIntentCommandFrame,
                _logRecorder != null ? _logRecorder.FilePath : string.Empty,
                _logRecorder != null && _logRecorder.WriteFailed);
            _logRecorder?.RecordComplete("completed", _elapsedFrames, _elapsedSeconds, _lastStatus);
            CloseLogRecorder();
            if (_lastStatus.Result == CharacterTestNetworkSyncProbeResult.Fail)
                logger?.Warning("CharacterTest.Network.SyncProbe", _lastStatus.Summary + " " + _lastStatus.Detail);
            else
                logger?.Info("CharacterTest.Network.SyncProbe", _lastStatus.Summary + " " + _lastStatus.Detail);
        }

        public bool TryCreateFixedInputSnapshot(out InputSnapshot snapshot)
        {
            snapshot = InputSnapshot.Empty;
            if (!_running)
                return false;

            if (_warmupFramesRemaining > 0)
                return true;

            CharacterTestNetworkSyncProbeInputSample sample =
                CreateProbeInputSample(_elapsedFrames + 1, _inputPattern);
            snapshot = CreateInputSnapshot(sample);
            return true;
        }

        internal static RuntimeCommand CreateProbeCommand(
            RuntimeFrame frame,
            int targetEntityId,
            int frameIndex)
        {
            CharacterTestNetworkSyncProbeInputSample sample =
                CreateProbeInputSample(
                    frameIndex,
                    CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop);
            return CharacterTestRuntimeCommandFactory.SetControlIntent(
                frame,
                CharacterTestRuntimeCommandSources.PlayerInput,
                targetEntityId,
                sample.Move,
                sample.Move,
                sample.JumpPressed,
                sample.JumpHeld,
                sample.JumpReleased,
                sample.AttackPrimaryPressed,
                attackSecondaryPressed: false,
                traceId: TraceId);
        }

        internal static CharacterTestNetworkSyncProbeInputSample CreateProbeInputSample(
            int frameIndex,
            CharacterTestNetworkSyncProbeInputPattern pattern)
        {
            switch (pattern)
            {
                case CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop:
                default:
                    return CreateMoveStopLoopSample(frameIndex);
            }
        }

        internal static InputSnapshot CreateInputSnapshot(CharacterTestNetworkSyncProbeInputSample sample)
        {
            return new InputSnapshot(
                sample.Move,
                look: Vector2.zero,
                navigate: Vector2.zero,
                point: Vector2.zero,
                scroll: Vector2.zero,
                throttle: 0f,
                jumpPressed: sample.JumpPressed,
                jumpHeld: sample.JumpHeld,
                jumpReleased: sample.JumpReleased,
                attackPrimaryPressed: sample.AttackPrimaryPressed,
                attackPrimaryHeld: false,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: false,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false);
        }

        private static CharacterTestNetworkSyncProbeInputSample CreateMoveStopLoopSample(int frameIndex)
        {
            int normalized = Math.Abs(frameIndex);
            int phase = normalized % MoveStopLoopCycleFrames;
            int cycleFrame = phase;
            Vector2 move;
            string segment;
            if (phase < 60)
            {
                move = new Vector2(0f, 0.75f);
                segment = "Forward";
            }
            else if (phase < 120)
            {
                move = Vector2.zero;
                segment = "StopAfterForward";
            }
            else if (phase < 180)
            {
                move = new Vector2(0f, -0.75f);
                segment = "Backward";
            }
            else
            {
                move = Vector2.zero;
                segment = "StopAfterBackward";
            }

            bool jumpPressed = cycleFrame == 10;
            bool jumpHeld = cycleFrame > 10 && cycleFrame < 24;
            bool jumpReleased = cycleFrame == 24;
            bool attackPressed = cycleFrame == 132;
            return new CharacterTestNetworkSyncProbeInputSample(
                move,
                segment,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPressed);
        }

        private void SubmitProbeCommandIfFrameAdvanced(
            GameSlice slice,
            CharacterTestNetworkLoopbackDiagnostics diagnostics)
        {
            RuntimeFrame commandFrame = slice != null ? slice.CurrentFrame : RuntimeFrame.Zero;
            if (slice != null && commandFrame.Value == _lastSubmittedCommandFrame)
                return;

            _lastSubmittedCommandFrame = commandFrame.Value;
            CharacterTestNetworkSyncProbeInputSample sample =
                CreateProbeInputSample(_elapsedFrames, _inputPattern);
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                commandFrame,
                CharacterTestRuntimeCommandSources.PlayerInput,
                _targetEntityId,
                sample.Move,
                sample.Move,
                sample.JumpPressed,
                sample.JumpHeld,
                sample.JumpReleased,
                sample.AttackPrimaryPressed,
                attackSecondaryPressed: false,
                traceId: TraceId);
            RuntimeCommandValidationResult result = slice?.Services != null
                ? slice.Services.Commands.EnqueueRuntimeCommand(command)
                : RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    command,
                    RuntimeFrame.Zero,
                    "Sync Probe has no active GameSlice."));
            if (result.Success)
            {
                _submittedCommandCount++;
                if (sample.IsZeroIntent)
                {
                    _zeroIntentCommandCount++;
                    _lastZeroIntentCommandFrame = commandFrame.Value;
                }
                else
                {
                    _nonZeroIntentCommandCount++;
                    _lastNonZeroIntentCommandFrame = commandFrame.Value;
                }
            }
            else
            {
                _rejectedCommandCount++;
                _lastError = result.Error.Message;
            }

            _logRecorder?.RecordFrame(
                _elapsedFrames,
                _elapsedSeconds,
                command,
                sample,
                result,
                _startDiagnostics,
                diagnostics.Available ? diagnostics : _lastDiagnostics);
        }

        private bool RecordProvidedInputFrameIfFrameAdvanced(
            GameSlice slice,
            CharacterTestNetworkLoopbackDiagnostics diagnostics,
            int frameIndex)
        {
            RuntimeFrame commandFrame = slice != null ? slice.CurrentFrame : RuntimeFrame.Zero;
            if (slice != null && commandFrame.Value == _lastSubmittedCommandFrame)
                return false;

            _lastSubmittedCommandFrame = commandFrame.Value;
            CharacterTestNetworkSyncProbeInputSample sample =
                CreateProbeInputSample(frameIndex, _inputPattern);
            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetControlIntent(
                commandFrame,
                CharacterTestRuntimeCommandSources.PlayerInput,
                _targetEntityId,
                sample.Move,
                sample.Move,
                sample.JumpPressed,
                sample.JumpHeld,
                sample.JumpReleased,
                sample.AttackPrimaryPressed,
                attackSecondaryPressed: false,
                traceId: TraceId);
            RuntimeCommandValidationResult result = RuntimeCommandValidationResult.Accepted(command);
            _submittedCommandCount++;
            if (sample.IsZeroIntent)
            {
                _zeroIntentCommandCount++;
                _lastZeroIntentCommandFrame = commandFrame.Value;
            }
            else
            {
                _nonZeroIntentCommandCount++;
                _lastNonZeroIntentCommandFrame = commandFrame.Value;
            }

            _logRecorder?.RecordFrame(
                frameIndex,
                _elapsedSeconds,
                command,
                sample,
                result,
                _startDiagnostics,
                diagnostics.Available ? diagnostics : _lastDiagnostics);
            return true;
        }

        internal static CharacterTestNetworkSyncProbeStatus Evaluate(
            CharacterTestNetworkLoopbackDiagnostics start,
            CharacterTestNetworkLoopbackDiagnostics end,
            int submittedCommandCount,
            int rejectedCommandCount,
            int elapsedFrames,
            double elapsedSeconds,
            int maxPendingTransportMessages,
            int maxSentPayloadBytes,
            int maxFrameDriftFrames,
            string lastError,
            CharacterTestNetworkSyncProbeInputPattern inputPattern = CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop,
            int zeroIntentCommandCount = 1,
            int nonZeroIntentCommandCount = 1,
            long lastZeroIntentCommandFrame = 0L,
            long lastNonZeroIntentCommandFrame = 0L,
            string logPath = "",
            bool logWriteFailed = false)
        {
            int received = Delta(end.AuthoritativeCommandReceivedCount, start.AuthoritativeCommandReceivedCount);
            int echo = Delta(end.AuthoritativeCommandEchoCount, start.AuthoritativeCommandEchoCount);
            int enqueued = Delta(end.AuthoritativeCommandEnqueuedCount, start.AuthoritativeCommandEnqueuedCount);
            int late = Delta(end.AuthoritativeCommandLateCount, start.AuthoritativeCommandLateCount);
            int dropped = Delta(end.AuthoritativeCommandDroppedCount, start.AuthoritativeCommandDroppedCount);
            int snapshotRequests = Delta(end.SnapshotRequestCount, start.SnapshotRequestCount);
            int fullSnapshots = Delta(
                end.FullSnapshotSentCount + end.FullSnapshotReceivedCount,
                start.FullSnapshotSentCount + start.FullSnapshotReceivedCount);
            int digestSnapshots = Delta(
                end.DigestSnapshotSentCount + end.DigestSnapshotReceivedCount,
                start.DigestSnapshotSentCount + start.DigestSnapshotReceivedCount);
            long commandBytes = Delta(end.RuntimeCommandSentBytes, start.RuntimeCommandSentBytes);
            long snapshotBytes = Delta(end.SnapshotSentBytes, start.SnapshotSentBytes);
            bool bootstrapOnly = end.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly
                || start.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly;

            CharacterTestNetworkSyncProbeResult result = CharacterTestNetworkSyncProbeResult.Pass;
            string code = "PASS";
            if (rejectedCommandCount > 0)
            {
                result = CharacterTestNetworkSyncProbeResult.Fail;
                code = "PROBE_COMMAND_REJECTED";
            }
            else if (nonZeroIntentCommandCount <= 0)
            {
                result = CharacterTestNetworkSyncProbeResult.Fail;
                code = "NO_MOVE_INTENT";
            }
            else if (zeroIntentCommandCount <= 0)
            {
                result = CharacterTestNetworkSyncProbeResult.Fail;
                code = "NO_STOP_INTENT";
            }
            else if (late > 0)
            {
                result = CharacterTestNetworkSyncProbeResult.Fail;
                code = "AUTH_LATE_GROWING";
            }
            else if (dropped > 0)
            {
                result = CharacterTestNetworkSyncProbeResult.Fail;
                code = "AUTH_DROPPED";
            }
            else if (bootstrapOnly
                && (snapshotRequests > 0 || fullSnapshots > 0 || digestSnapshots > 0))
            {
                result = CharacterTestNetworkSyncProbeResult.Fail;
                code = "STATE_SYNC_ACTIVE";
            }
            else if (snapshotRequests > SnapshotRequestStormThreshold
                || fullSnapshots > FullSnapshotStormThreshold)
            {
                result = CharacterTestNetworkSyncProbeResult.Fail;
                code = "FULL_SNAPSHOT_STORM";
            }
            else if (received > 0 && echo + enqueued <= 0)
            {
                result = CharacterTestNetworkSyncProbeResult.Fail;
                code = "NO_AUTH_ECHO";
            }
            else if (submittedCommandCount > 0 && received + echo + enqueued <= 0)
            {
                result = CharacterTestNetworkSyncProbeResult.Fail;
                code = "NO_AUTH_ECHO";
            }
            else if (maxPendingTransportMessages > PendingTransportWarningMessages)
            {
                result = CharacterTestNetworkSyncProbeResult.Warning;
                code = "RELIABLE_QUEUE_BLOCKED";
            }
            else if (maxSentPayloadBytes > MaxPayloadWarningBytes)
            {
                result = CharacterTestNetworkSyncProbeResult.Warning;
                code = "LARGE_RELIABLE_PAYLOAD";
            }
            else if (Math.Abs(maxFrameDriftFrames) > FrameDriftWarningFrames)
            {
                result = CharacterTestNetworkSyncProbeResult.Warning;
                code = "FRAME_DRIFT_HIGH";
            }
            else if (commandBytes > 0L && snapshotBytes > commandBytes * 2L)
            {
                result = CharacterTestNetworkSyncProbeResult.Warning;
                code = "SNAPSHOT_BYTES_DOMINATE";
            }

            string summary = "Sync Probe "
                + ResultLabel(result)
                + " "
                + code
                + ": submitted="
                + submittedCommandCount
                + " auth recv="
                + received
                + " echo="
                + echo
                + " enq="
                + enqueued
                + " late="
                + late
                + " drop="
                + dropped
                + " full="
                + fullSnapshots
                + " digest="
                + digestSnapshots
                + " snapReq="
                + snapshotRequests
                + " zero="
                + zeroIntentCommandCount
                + " move="
                + nonZeroIntentCommandCount;
            string detail = "frames="
                + elapsedFrames
                + " seconds="
                + elapsedSeconds.ToString("0.0")
                + " pattern="
                + inputPattern
                + " lastZeroFrame="
                + lastZeroIntentCommandFrame
                + " lastMoveFrame="
                + lastNonZeroIntentCommandFrame
                + " pendingMax="
                + maxPendingTransportMessages
                + " maxPayload="
                + maxSentPayloadBytes
                + " driftMax="
                + maxFrameDriftFrames
                + " cmdBytes="
                + commandBytes
                + " snapshotBytes="
                + snapshotBytes
                + (string.IsNullOrWhiteSpace(logPath) ? string.Empty : " log=" + logPath.Trim())
                + (logWriteFailed ? " logWriteFailed=true" : string.Empty)
                + (string.IsNullOrWhiteSpace(lastError) ? string.Empty : " lastError=" + lastError.Trim());
            return CreateStatus(
                result,
                code,
                summary,
                detail,
                elapsedFrames,
                elapsedSeconds,
                submittedCommandCount,
                rejectedCommandCount,
                received,
                echo,
                enqueued,
                late,
                dropped,
                snapshotRequests,
                fullSnapshots,
                digestSnapshots,
                commandBytes,
                snapshotBytes,
                maxPendingTransportMessages,
                maxSentPayloadBytes,
                maxFrameDriftFrames,
                lastError,
                inputPattern,
                zeroIntentCommandCount,
                nonZeroIntentCommandCount,
                lastZeroIntentCommandFrame,
                lastNonZeroIntentCommandFrame,
                logPath,
                logWriteFailed);
        }

        private static string ValidateStart(
            GameSlice slice,
            CharacterTestNetworkLoopbackDiagnostics diagnostics,
            CharacterTestNetworkSessionStatus sessionStatus)
        {
            if (slice == null)
                return "Sync Probe rejected: CharacterTest runtime is not available.";
            if (sessionStatus == null || !sessionStatus.IsNetworkActive)
                return "Sync Probe rejected: network session is offline.";
            if (!diagnostics.Available)
                return "Sync Probe rejected: network diagnostics are unavailable.";
            if (sessionStatus.ClientStatus != NetworkSessionStatus.Connected)
                return "Sync Probe rejected: gameplay client is not connected.";
            if (diagnostics.OwnedEntityId <= 0)
                return "Sync Probe rejected: no owned player entity is assigned.";
            if (!sessionStatus.CanEnterGameplay)
                return "Sync Probe rejected: gameplay handshake is not verified yet.";

            return string.Empty;
        }

        private void SampleMaxima(CharacterTestNetworkLoopbackDiagnostics diagnostics)
        {
            if (!diagnostics.Available)
                return;

            _maxPendingTransportMessages = Math.Max(
                _maxPendingTransportMessages,
                diagnostics.PendingTransportMessages);
            _maxSentPayloadBytes = Math.Max(
                _maxSentPayloadBytes,
                Math.Max(diagnostics.MaxSentPayloadBytes, diagnostics.LastSentPayloadBytes));
            _maxFrameDriftFrames = Math.Max(
                _maxFrameDriftFrames,
                Math.Abs(ResolveCurrentFrameDriftFrames(diagnostics)));
        }

        internal static int ResolveCurrentFrameDriftFrames(CharacterTestNetworkLoopbackDiagnostics diagnostics)
        {
            if (!diagnostics.Available)
                return 0;

            if (diagnostics.ServerFrame.Value != 0L || diagnostics.ClientLastServerFrame.Value != 0L)
                return ClampToInt(diagnostics.ClientLastServerFrame.Value - diagnostics.ServerFrame.Value);

            return diagnostics.FrameDriftFrames;
        }

        private static int ClampToInt(long value)
        {
            if (value > int.MaxValue)
                return int.MaxValue;
            if (value < int.MinValue)
                return int.MinValue;
            return (int)value;
        }

        private CharacterTestNetworkSyncProbeStatus CreateRunningStatus()
        {
            string summary = _warmupFramesRemaining > 0
                ? "Sync Probe RUNNING: warming up "
                    + _warmupFramesRemaining
                    + " frames before diagnostic input."
                : "Sync Probe RUNNING: "
                    + _elapsedFrames
                    + "/"
                    + DefaultMaxFrames
                    + " frames, pattern="
                    + _inputPattern
                    + ", submitted="
                    + _submittedCommandCount
                    + ", zero="
                    + _zeroIntentCommandCount
                    + ", move="
                    + _nonZeroIntentCommandCount
                    + ", rejected="
                    + _rejectedCommandCount
                    + ". Diagnostic input is active.";
            return CreateStatus(
                CharacterTestNetworkSyncProbeResult.Running,
                "RUNNING",
                summary,
                BuildRunDetail(),
                _elapsedFrames,
                _elapsedSeconds,
                _submittedCommandCount,
                _rejectedCommandCount,
                Delta(_lastDiagnostics.AuthoritativeCommandReceivedCount, _startDiagnostics.AuthoritativeCommandReceivedCount),
                Delta(_lastDiagnostics.AuthoritativeCommandEchoCount, _startDiagnostics.AuthoritativeCommandEchoCount),
                Delta(_lastDiagnostics.AuthoritativeCommandEnqueuedCount, _startDiagnostics.AuthoritativeCommandEnqueuedCount),
                Delta(_lastDiagnostics.AuthoritativeCommandLateCount, _startDiagnostics.AuthoritativeCommandLateCount),
                Delta(_lastDiagnostics.AuthoritativeCommandDroppedCount, _startDiagnostics.AuthoritativeCommandDroppedCount),
                Delta(_lastDiagnostics.SnapshotRequestCount, _startDiagnostics.SnapshotRequestCount),
                Delta(
                    _lastDiagnostics.FullSnapshotSentCount + _lastDiagnostics.FullSnapshotReceivedCount,
                    _startDiagnostics.FullSnapshotSentCount + _startDiagnostics.FullSnapshotReceivedCount),
                Delta(
                    _lastDiagnostics.DigestSnapshotSentCount + _lastDiagnostics.DigestSnapshotReceivedCount,
                    _startDiagnostics.DigestSnapshotSentCount + _startDiagnostics.DigestSnapshotReceivedCount),
                Delta(_lastDiagnostics.RuntimeCommandSentBytes, _startDiagnostics.RuntimeCommandSentBytes),
                Delta(_lastDiagnostics.SnapshotSentBytes, _startDiagnostics.SnapshotSentBytes),
                _maxPendingTransportMessages,
                _maxSentPayloadBytes,
                _maxFrameDriftFrames,
                _lastError,
                _inputPattern,
                _zeroIntentCommandCount,
                _nonZeroIntentCommandCount,
                _lastZeroIntentCommandFrame,
                _lastNonZeroIntentCommandFrame,
                _logRecorder != null ? _logRecorder.FilePath : string.Empty,
                _logRecorder != null && _logRecorder.WriteFailed);
        }

        private string BuildRunDetail()
        {
            return BuildDetail(_startDiagnostics, _lastDiagnostics)
                + " pattern="
                + _inputPattern
                + " lastZeroFrame="
                + _lastZeroIntentCommandFrame
                + " lastMoveFrame="
                + _lastNonZeroIntentCommandFrame
                + (_logRecorder == null || string.IsNullOrWhiteSpace(_logRecorder.FilePath)
                    ? string.Empty
                    : " log=" + _logRecorder.FilePath)
                + (_logRecorder != null && _logRecorder.WriteFailed ? " logWriteFailed=true" : string.Empty);
        }

        private static string BuildDetail(
            CharacterTestNetworkLoopbackDiagnostics start,
            CharacterTestNetworkLoopbackDiagnostics end)
        {
            var builder = new StringBuilder(160);
            builder.Append("auth recv=")
                .Append(Delta(end.AuthoritativeCommandReceivedCount, start.AuthoritativeCommandReceivedCount))
                .Append(" echo=")
                .Append(Delta(end.AuthoritativeCommandEchoCount, start.AuthoritativeCommandEchoCount))
                .Append(" enq=")
                .Append(Delta(end.AuthoritativeCommandEnqueuedCount, start.AuthoritativeCommandEnqueuedCount))
                .Append(" late=")
                .Append(Delta(end.AuthoritativeCommandLateCount, start.AuthoritativeCommandLateCount))
                .Append(" drop=")
                .Append(Delta(end.AuthoritativeCommandDroppedCount, start.AuthoritativeCommandDroppedCount))
                .Append(" snapReq=")
                .Append(Delta(end.SnapshotRequestCount, start.SnapshotRequestCount))
                .Append(" full=")
                .Append(Delta(
                    end.FullSnapshotSentCount + end.FullSnapshotReceivedCount,
                    start.FullSnapshotSentCount + start.FullSnapshotReceivedCount))
                .Append(" digest=")
                .Append(Delta(
                    end.DigestSnapshotSentCount + end.DigestSnapshotReceivedCount,
                    start.DigestSnapshotSentCount + start.DigestSnapshotReceivedCount))
                .Append(" cmdBytes=")
                .Append(Delta(end.RuntimeCommandSentBytes, start.RuntimeCommandSentBytes))
                .Append(" snapshotBytes=")
                .Append(Delta(end.SnapshotSentBytes, start.SnapshotSentBytes));
            return builder.ToString();
        }

        private static CharacterTestNetworkSyncProbeStatus CreateStatus(
            CharacterTestNetworkSyncProbeResult result,
            string code,
            string summary,
            string detail,
            int elapsedFrames = 0,
            double elapsedSeconds = 0d,
            int submittedCommandCount = 0,
            int rejectedCommandCount = 0,
            int received = 0,
            int echo = 0,
            int enqueued = 0,
            int late = 0,
            int dropped = 0,
            int snapshotRequests = 0,
            int fullSnapshots = 0,
            int digestSnapshots = 0,
            long commandBytes = 0L,
            long snapshotBytes = 0L,
            int maxPendingTransportMessages = 0,
            int maxSentPayloadBytes = 0,
            int maxFrameDriftFrames = 0,
            string lastError = "",
            CharacterTestNetworkSyncProbeInputPattern inputPattern = CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop,
            int zeroIntentCommandCount = 0,
            int nonZeroIntentCommandCount = 0,
            long lastZeroIntentCommandFrame = -1L,
            long lastNonZeroIntentCommandFrame = -1L,
            string logPath = "",
            bool logWriteFailed = false)
        {
            return new CharacterTestNetworkSyncProbeStatus
            {
                Result = result,
                IsRunning = result == CharacterTestNetworkSyncProbeResult.Running,
                ResultCode = string.IsNullOrWhiteSpace(code) ? result.ToString().ToUpperInvariant() : code,
                Summary = summary ?? string.Empty,
                Detail = detail ?? string.Empty,
                ElapsedFrames = elapsedFrames,
                ElapsedSeconds = elapsedSeconds,
                SubmittedCommandCount = submittedCommandCount,
                RejectedCommandCount = rejectedCommandCount,
                DeltaAuthoritativeCommandReceived = received,
                DeltaAuthoritativeCommandEcho = echo,
                DeltaAuthoritativeCommandEnqueued = enqueued,
                DeltaAuthoritativeCommandLate = late,
                DeltaAuthoritativeCommandDropped = dropped,
                DeltaSnapshotRequest = snapshotRequests,
                DeltaFullSnapshot = fullSnapshots,
                DeltaDigestSnapshot = digestSnapshots,
                DeltaRuntimeCommandBytes = commandBytes,
                DeltaSnapshotBytes = snapshotBytes,
                MaxPendingTransportMessages = maxPendingTransportMessages,
                MaxSentPayloadBytes = maxSentPayloadBytes,
                MaxFrameDriftFrames = maxFrameDriftFrames,
                InputPattern = inputPattern,
                ZeroIntentCommandCount = zeroIntentCommandCount,
                NonZeroIntentCommandCount = nonZeroIntentCommandCount,
                LastZeroIntentCommandFrame = lastZeroIntentCommandFrame,
                LastNonZeroIntentCommandFrame = lastNonZeroIntentCommandFrame,
                LogPath = logPath ?? string.Empty,
                LogWriteFailed = logWriteFailed,
                LastError = lastError ?? string.Empty
            };
        }

        private static string ResultLabel(CharacterTestNetworkSyncProbeResult result)
        {
            switch (result)
            {
                case CharacterTestNetworkSyncProbeResult.Warning:
                    return "WARNING";
                case CharacterTestNetworkSyncProbeResult.Fail:
                    return "FAIL";
                case CharacterTestNetworkSyncProbeResult.Pass:
                    return "PASS";
                default:
                    return result.ToString().ToUpperInvariant();
            }
        }

        private static int Delta(int end, int start)
        {
            return Math.Max(0, end - start);
        }

        private static long Delta(long end, long start)
        {
            return Math.Max(0L, end - start);
        }

        private void CloseLogRecorder()
        {
            if (_logRecorder == null)
                return;

            _logRecorder.Dispose();
            _logRecorder = null;
        }
    }

    internal readonly struct CharacterTestNetworkSyncProbeInputSample
    {
        public CharacterTestNetworkSyncProbeInputSample(
            Vector2 move,
            string segment,
            bool jumpPressed,
            bool jumpHeld,
            bool jumpReleased,
            bool attackPrimaryPressed)
        {
            Move = move;
            Segment = string.IsNullOrWhiteSpace(segment) ? "Unknown" : segment;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            JumpReleased = jumpReleased;
            AttackPrimaryPressed = attackPrimaryPressed;
        }

        public Vector2 Move { get; }
        public string Segment { get; }
        public bool JumpPressed { get; }
        public bool JumpHeld { get; }
        public bool JumpReleased { get; }
        public bool AttackPrimaryPressed { get; }
        public bool IsZeroIntent =>
            Move.sqrMagnitude <= 0.0001f
            && !JumpPressed
            && !JumpHeld
            && !JumpReleased
            && !AttackPrimaryPressed;
    }

    internal sealed class CharacterTestNetworkSyncProbeLogRecorder : IDisposable
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly StreamWriter _writer;
        private bool _disposed;

        private CharacterTestNetworkSyncProbeLogRecorder(string filePath, StreamWriter writer)
        {
            FilePath = filePath ?? string.Empty;
            _writer = writer;
            WriteFailed = writer == null;
        }

        public string FilePath { get; }
        public bool WriteFailed { get; private set; }

        public static CharacterTestNetworkSyncProbeLogRecorder Start(
            CharacterTestNetworkSessionStatus sessionStatus,
            CharacterTestNetworkLoopbackDiagnostics diagnostics,
            CharacterTestNetworkSyncProbeInputPattern pattern,
            int targetEntityId,
            int maxFrames,
            double maxSeconds)
        {
            string filePath = CreateDefaultFilePath(
                Application.persistentDataPath,
                sessionStatus,
                Environment.GetCommandLineArgs());
            StreamWriter writer = TryOpenWriter(filePath);
            var recorder = new CharacterTestNetworkSyncProbeLogRecorder(filePath, writer);
            recorder.Write(new
            {
                eventType = "start",
                utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                pattern = pattern.ToString(),
                targetEntityId,
                maxFrames,
                maxSeconds,
                kind = sessionStatus != null ? sessionStatus.Kind.ToString() : string.Empty,
                launchMode = sessionStatus != null ? sessionStatus.LaunchMode.ToString() : string.Empty,
                networkMode = sessionStatus != null ? sessionStatus.NetworkMode.ToString() : string.Empty,
                serverPeerId = sessionStatus != null ? sessionStatus.ServerPeerId : string.Empty,
                clientPeerId = sessionStatus != null ? sessionStatus.ClientPeerId : string.Empty,
                localPeerId = diagnostics.LocalPeerId,
                transportMode = diagnostics.TransportMode,
                transportId = diagnostics.TransportId,
                stateSyncPolicy = diagnostics.StateSyncPolicy.ToString()
            });
            return recorder;
        }

        public void RecordFrame(
            int elapsedFrame,
            double elapsedSeconds,
            RuntimeCommand command,
            CharacterTestNetworkSyncProbeInputSample sample,
            RuntimeCommandValidationResult validation,
            CharacterTestNetworkLoopbackDiagnostics start,
            CharacterTestNetworkLoopbackDiagnostics current)
        {
            Write(new
            {
                eventType = "frame",
                utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                elapsedFrame,
                elapsedSeconds,
                runtimeFrame = command.Frame.Value,
                targetEntityId = command.TargetId,
                pattern = CharacterTestNetworkSyncProbeInputPattern.MoveStopLoop.ToString(),
                segment = sample.Segment,
                moveX = sample.Move.x,
                moveY = sample.Move.y,
                zeroIntent = sample.IsZeroIntent,
                jumpPressed = sample.JumpPressed,
                jumpHeld = sample.JumpHeld,
                jumpReleased = sample.JumpReleased,
                attackPrimaryPressed = sample.AttackPrimaryPressed,
                submitted = validation.Success,
                rejected = !validation.Success,
                errorCode = validation.Success ? string.Empty : validation.Error.Code.ToString(),
                error = validation.Success ? string.Empty : validation.Error.Message,
                authReceivedDelta = Delta(current.AuthoritativeCommandReceivedCount, start.AuthoritativeCommandReceivedCount),
                authEchoDelta = Delta(current.AuthoritativeCommandEchoCount, start.AuthoritativeCommandEchoCount),
                authEnqueuedDelta = Delta(current.AuthoritativeCommandEnqueuedCount, start.AuthoritativeCommandEnqueuedCount),
                authLateDelta = Delta(current.AuthoritativeCommandLateCount, start.AuthoritativeCommandLateCount),
                authDroppedDelta = Delta(current.AuthoritativeCommandDroppedCount, start.AuthoritativeCommandDroppedCount),
                fullSnapshotDelta = Delta(
                    current.FullSnapshotSentCount + current.FullSnapshotReceivedCount,
                    start.FullSnapshotSentCount + start.FullSnapshotReceivedCount),
                digestSnapshotDelta = Delta(
                    current.DigestSnapshotSentCount + current.DigestSnapshotReceivedCount,
                    start.DigestSnapshotSentCount + start.DigestSnapshotReceivedCount),
                snapshotRequestDelta = Delta(current.SnapshotRequestCount, start.SnapshotRequestCount),
                pendingRuntimeCommands = current.PendingRuntimeCommands,
                pendingTransportMessages = current.PendingTransportMessages,
                frameDriftFrames = CharacterTestNetworkSyncProbe.ResolveCurrentFrameDriftFrames(current),
                rttEstimateFrames = current.RttEstimateFrames,
                lastAckedInputFrame = current.LastAckedInputFrame.Value,
                serverFrame = current.ServerFrame.Value,
                clientLastServerFrame = current.ClientLastServerFrame.Value,
                sentBytes = current.SentBytes,
                runtimeCommandSentBytes = current.RuntimeCommandSentBytes,
                snapshotSentBytes = current.SnapshotSentBytes,
                lastSentPayloadBytes = current.LastSentPayloadBytes,
                maxSentPayloadBytes = current.MaxSentPayloadBytes,
                recentDiagnostics = current.RecentDiagnostics
            });
        }

        public void RecordComplete(
            string eventType,
            int elapsedFrames,
            double elapsedSeconds,
            CharacterTestNetworkSyncProbeStatus status)
        {
            Write(new
            {
                eventType = string.IsNullOrWhiteSpace(eventType) ? "complete" : eventType,
                utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                elapsedFrames,
                elapsedSeconds,
                result = status != null ? status.Result.ToString() : string.Empty,
                resultCode = status != null ? status.ResultCode : string.Empty,
                summary = status != null ? status.Summary : string.Empty,
                detail = status != null ? status.Detail : string.Empty,
                zeroIntentCommandCount = status != null ? status.ZeroIntentCommandCount : 0,
                nonZeroIntentCommandCount = status != null ? status.NonZeroIntentCommandCount : 0,
                lastZeroIntentCommandFrame = status != null ? status.LastZeroIntentCommandFrame : -1L,
                lastNonZeroIntentCommandFrame = status != null ? status.LastNonZeroIntentCommandFrame : -1L
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            try
            {
                _writer?.Dispose();
            }
            catch (IOException)
            {
                WriteFailed = true;
            }
            catch (UnauthorizedAccessException)
            {
                WriteFailed = true;
            }
        }

        internal static string CreateDefaultFilePath(
            string persistentDataPath,
            CharacterTestNetworkSessionStatus sessionStatus,
            string[] commandLineArgs)
        {
            string directory = Path.Combine(
                persistentDataPath ?? string.Empty,
                "MxFramework",
                "CharacterTest",
                "SyncProbe");
            string player = ResolveMultiplayerPlayModePlayerName(commandLineArgs);
            if (string.IsNullOrWhiteSpace(player))
                player = sessionStatus != null && !string.IsNullOrWhiteSpace(sessionStatus.ClientPeerId)
                    ? sessionStatus.ClientPeerId
                    : sessionStatus != null
                        ? sessionStatus.Kind.ToString()
                        : "player";
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            string fileName = "sync-probe-"
                + SanitizeFileNameSegment(player)
                + "-"
                + stamp
                + "-pid"
                + ResolveCurrentProcessId().ToString(CultureInfo.InvariantCulture)
                + ".jsonl";
            return Path.Combine(directory, fileName);
        }

        private static StreamWriter TryOpenWriter(string filePath)
        {
            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                return new StreamWriter(stream);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private void Write(object record)
        {
            if (_writer == null || WriteFailed)
                return;

            try
            {
                _writer.WriteLine(JsonConvert.SerializeObject(record, JsonSettings));
                _writer.Flush();
            }
            catch (IOException)
            {
                WriteFailed = true;
            }
            catch (UnauthorizedAccessException)
            {
                WriteFailed = true;
            }
        }

        private static string ResolveMultiplayerPlayModePlayerName(string[] commandLineArgs)
        {
            string[] args = commandLineArgs ?? Array.Empty<string>();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], "-name", StringComparison.OrdinalIgnoreCase))
                    continue;

                string value = args[i + 1];
                if (!string.IsNullOrWhiteSpace(value) && value.StartsWith("Player", StringComparison.OrdinalIgnoreCase))
                    return value;
            }

            return string.Empty;
        }

        private static string SanitizeFileNameSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "player";

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0 || char.IsWhiteSpace(chars[i]))
                    chars[i] = '-';
            }

            return new string(chars);
        }

        private static int ResolveCurrentProcessId()
        {
            try
            {
                return System.Diagnostics.Process.GetCurrentProcess().Id;
            }
            catch
            {
                return 0;
            }
        }

        private static int Delta(int end, int start)
        {
            return Math.Max(0, end - start);
        }

        private static long Delta(long end, long start)
        {
            return Math.Max(0L, end - start);
        }
    }
}
