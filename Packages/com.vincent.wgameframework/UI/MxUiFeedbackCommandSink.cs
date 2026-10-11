using System;
using System.Diagnostics;
using System.Threading;

namespace MxFramework.UI
{
    public enum MxUiFeedbackDiagnosticCode
    {
        None = 0,
        PolicyMissing = 1,
        PlayRejected = 2,
        PendingOverflow = 3,
        OutcomeUnknown = 4,
        OutcomeExpired = 5,
        HandlerException = 6,
        WrongThread = 7,
        OutcomeAfterDispose = 8
    }

    public readonly struct MxUiFeedbackDebugSnapshot
    {
        public MxUiFeedbackDebugSnapshot(
            int pendingCount,
            int diagnosticCount,
            MxUiFeedbackDiagnosticCode lastDiagnostic)
        {
            PendingCount = pendingCount;
            DiagnosticCount = diagnosticCount;
            LastDiagnostic = lastDiagnostic;
        }

        public int PendingCount { get; }
        public int DiagnosticCount { get; }
        public MxUiFeedbackDiagnosticCode LastDiagnostic { get; }
    }

    public interface IMxUiFeedbackClock
    {
        long Timestamp { get; }
        long Frequency { get; }
    }

    /// <summary>
    /// Main-thread-only UI command decorator. Construction and binding are LowFreqAlloc;
    /// Enqueue, Complete, and CaptureDebugSnapshot are NoAlloc after initialization.
    /// </summary>
    public sealed class MxUiFeedbackCommandSink :
        IMxUiCommandSink,
        IMxUiCommandOutcomeSink,
        IDisposable
    {
        public const int PendingCapacity = 64;
        public const int OutcomeTimeoutSeconds = 30;

        private readonly IMxUiFeedbackPolicyProvider _policyProvider;
        private readonly IMxUiFeedbackClock _clock;
        private readonly PendingOutcome[] _pending;
        private readonly int _ownerThreadId;
        private readonly long _outcomeTimeoutTicks;
        private IMxUiCommandSink _innerSink;
        private IMxUiFeedbackPlayer _player;
        private IMxUiCommandOutcomeBinding _outcomeBinding;
        private long _nextInvocationId;
        private int _pendingCount;
        private int _diagnosticCount;
        private int _lastDiagnostic;
        private bool _disposed;

        public MxUiFeedbackCommandSink(
            IMxUiCommandSink innerSink,
            IMxUiFeedbackPolicyProvider policyProvider,
            IMxUiFeedbackPlayer player = null,
            IMxUiFeedbackClock clock = null)
        {
            _innerSink = innerSink ?? throw new ArgumentNullException(nameof(innerSink));
            _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
            _player = player;
            _clock = clock ?? StopwatchFeedbackClock.Instance;
            if (_clock.Frequency <= 0L ||
                _clock.Frequency > long.MaxValue / OutcomeTimeoutSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(clock));
            }

            _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
            _outcomeTimeoutTicks = _clock.Frequency * OutcomeTimeoutSeconds;
            _pending = new PendingOutcome[PendingCapacity];

            if (innerSink is IMxUiCommandOutcomeSource outcomeSource)
                _outcomeBinding = outcomeSource.BindOutcomeSink(this);
        }

        public void Enqueue(MxUiCommand command)
        {
            EnsureOwnerThread();
            if (_disposed)
                throw new ObjectDisposedException(nameof(MxUiFeedbackCommandSink));

            long now = _clock.Timestamp;
            PurgeExpired(now, targetInvocationId: 0L);

            long invocationId = NextInvocationId();
            MxUiCommand routedCommand = command.WithInvocationId(invocationId);
            if (!_policyProvider.TryGetPolicy(
                    command.SourceViewId,
                    command.CommandId,
                    out MxUiFeedbackPolicy policy))
            {
                RecordDiagnostic(MxUiFeedbackDiagnosticCode.PolicyMissing);
                policy = default;
            }

            bool pendingRegistered = false;
            if (policy.HasOutcomeFeedback)
                pendingRegistered = TryRegisterPending(in routedCommand, in policy, now);

            TryPlay(
                policy.Activation,
                in routedCommand,
                MxUiFeedbackPhase.Activation);

            try
            {
                _innerSink.Enqueue(routedCommand);
            }
            catch
            {
                if (pendingRegistered)
                    RemovePending(invocationId);
                RecordDiagnostic(MxUiFeedbackDiagnosticCode.HandlerException);
                throw;
            }
        }

        public void Complete(in MxUiCommandOutcome outcome)
        {
            EnsureOwnerThread();
            if (_disposed)
            {
                RecordDiagnostic(MxUiFeedbackDiagnosticCode.OutcomeAfterDispose);
                return;
            }

            long now = _clock.Timestamp;
            bool targetExpired = PurgeExpired(now, outcome.InvocationId);
            if (targetExpired)
                return;

            int index = FindPending(outcome.InvocationId);
            if (index < 0)
            {
                RecordDiagnostic(MxUiFeedbackDiagnosticCode.OutcomeUnknown);
                return;
            }

            PendingOutcome pending = _pending[index];
            ClearPending(index);

            switch (outcome.Status)
            {
                case MxUiCommandOutcomeStatus.Success:
                    TryPlay(
                        pending.Success,
                        pending.SourceViewId,
                        pending.CommandId,
                        pending.InvocationId,
                        MxUiFeedbackPhase.Success);
                    break;
                case MxUiCommandOutcomeStatus.Failure:
                    TryPlay(
                        pending.Failure,
                        pending.SourceViewId,
                        pending.CommandId,
                        pending.InvocationId,
                        MxUiFeedbackPhase.Failure);
                    break;
                case MxUiCommandOutcomeStatus.Cancelled:
                    break;
                default:
                    RecordDiagnostic(MxUiFeedbackDiagnosticCode.OutcomeUnknown);
                    break;
            }
        }

        public MxUiFeedbackDebugSnapshot CaptureDebugSnapshot()
        {
            EnsureOwnerThread();
            return new MxUiFeedbackDebugSnapshot(
                _pendingCount,
                Volatile.Read(ref _diagnosticCount),
                (MxUiFeedbackDiagnosticCode)Volatile.Read(ref _lastDiagnostic));
        }

        public void Dispose()
        {
            EnsureOwnerThread();
            if (_disposed)
                return;

            _disposed = true;
            IMxUiCommandOutcomeBinding binding = _outcomeBinding;
            _outcomeBinding = null;
            binding?.Dispose();

            Array.Clear(_pending, 0, _pending.Length);
            _pendingCount = 0;
            _player = null;
            _innerSink = null;
        }

        private bool TryRegisterPending(
            in MxUiCommand command,
            in MxUiFeedbackPolicy policy,
            long timestamp)
        {
            for (int i = 0; i < _pending.Length; i++)
            {
                if (_pending[i].IsOccupied)
                    continue;

                _pending[i] = new PendingOutcome(
                    command.InvocationId,
                    command.SourceViewId,
                    command.CommandId,
                    policy.Success,
                    policy.Failure,
                    timestamp);
                _pendingCount++;
                return true;
            }

            RecordDiagnostic(MxUiFeedbackDiagnosticCode.PendingOverflow);
            return false;
        }

        private bool PurgeExpired(long now, long targetInvocationId)
        {
            bool targetExpired = false;
            for (int i = 0; i < _pending.Length; i++)
            {
                PendingOutcome pending = _pending[i];
                if (!pending.IsOccupied)
                    continue;

                long elapsed = now - pending.Timestamp;
                if (elapsed < _outcomeTimeoutTicks || elapsed < 0L)
                    continue;

                if (pending.InvocationId == targetInvocationId)
                    targetExpired = true;
                ClearPending(i);
                RecordDiagnostic(MxUiFeedbackDiagnosticCode.OutcomeExpired);
            }

            return targetExpired;
        }

        private int FindPending(long invocationId)
        {
            if (invocationId <= 0L)
                return -1;

            for (int i = 0; i < _pending.Length; i++)
            {
                if (_pending[i].IsOccupied &&
                    _pending[i].InvocationId == invocationId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void RemovePending(long invocationId)
        {
            int index = FindPending(invocationId);
            if (index >= 0)
                ClearPending(index);
        }

        private void ClearPending(int index)
        {
            _pending[index] = default;
            _pendingCount--;
        }

        private void TryPlay(
            MxUiFeedbackKind kind,
            in MxUiCommand command,
            MxUiFeedbackPhase phase)
        {
            TryPlay(
                kind,
                command.SourceViewId,
                command.CommandId,
                command.InvocationId,
                phase);
        }

        private void TryPlay(
            MxUiFeedbackKind kind,
            MxUiViewId sourceViewId,
            string commandId,
            long invocationId,
            MxUiFeedbackPhase phase)
        {
            if (kind == MxUiFeedbackKind.None || _player == null)
                return;

            var context = new MxUiFeedbackContext(
                sourceViewId,
                commandId,
                invocationId,
                phase);
            try
            {
                if (!_player.TryPlay(kind, in context))
                    RecordDiagnostic(MxUiFeedbackDiagnosticCode.PlayRejected);
            }
            catch
            {
                RecordDiagnostic(MxUiFeedbackDiagnosticCode.PlayRejected);
            }
        }

        private long NextInvocationId()
        {
            _nextInvocationId = _nextInvocationId == long.MaxValue
                ? 1L
                : _nextInvocationId + 1L;
            return _nextInvocationId;
        }

        private void EnsureOwnerThread()
        {
            if (Thread.CurrentThread.ManagedThreadId == _ownerThreadId)
                return;

            RecordDiagnostic(MxUiFeedbackDiagnosticCode.WrongThread);
            throw new InvalidOperationException(
                "UI feedback routing must run on its owner thread.");
        }

        private void RecordDiagnostic(MxUiFeedbackDiagnosticCode code)
        {
            Interlocked.Exchange(ref _lastDiagnostic, (int)code);
            Interlocked.Increment(ref _diagnosticCount);
        }

        private readonly struct PendingOutcome
        {
            public PendingOutcome(
                long invocationId,
                MxUiViewId sourceViewId,
                string commandId,
                MxUiFeedbackKind success,
                MxUiFeedbackKind failure,
                long timestamp)
            {
                InvocationId = invocationId;
                SourceViewId = sourceViewId;
                CommandId = commandId;
                Success = success;
                Failure = failure;
                Timestamp = timestamp;
            }

            public long InvocationId { get; }
            public MxUiViewId SourceViewId { get; }
            public string CommandId { get; }
            public MxUiFeedbackKind Success { get; }
            public MxUiFeedbackKind Failure { get; }
            public long Timestamp { get; }
            public bool IsOccupied => InvocationId != 0L;
        }

        private sealed class StopwatchFeedbackClock : IMxUiFeedbackClock
        {
            public static readonly StopwatchFeedbackClock Instance =
                new StopwatchFeedbackClock();

            private StopwatchFeedbackClock()
            {
            }

            public long Timestamp => Stopwatch.GetTimestamp();
            public long Frequency => Stopwatch.Frequency;
        }
    }
}
