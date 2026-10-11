using System;
using System.Threading;

namespace MxFramework.UI
{
    public enum MxUiCommandOutcomeStatus
    {
        Success = 0,
        Failure = 1,
        Cancelled = 2
    }

    public readonly struct MxUiCommandOutcome
    {
        public MxUiCommandOutcome(
            long invocationId,
            MxUiCommandOutcomeStatus status,
            string errorCode = null)
        {
            if (invocationId <= 0L)
                throw new ArgumentOutOfRangeException(nameof(invocationId));
            if (status < MxUiCommandOutcomeStatus.Success ||
                status > MxUiCommandOutcomeStatus.Cancelled)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            InvocationId = invocationId;
            Status = status;
            ErrorCode = errorCode ?? string.Empty;
        }

        public long InvocationId { get; }
        public MxUiCommandOutcomeStatus Status { get; }
        public string ErrorCode { get; }
    }

    public interface IMxUiCommandOutcomeSink
    {
        /// <summary>
        /// Completes a routed UI invocation on the outcome source owner thread. NoAlloc.
        /// </summary>
        void Complete(in MxUiCommandOutcome outcome);
    }

    public interface IMxUiCommandOutcomeBinding : IDisposable
    {
    }

    public interface IMxUiCommandOutcomeSource
    {
        /// <summary>
        /// Establishes the source's only active outcome owner. LowFreqAlloc.
        /// </summary>
        IMxUiCommandOutcomeBinding BindOutcomeSink(IMxUiCommandOutcomeSink sink);
    }

    /// <summary>
    /// Optional composition helper for command handlers that publish outcomes.
    /// Binding and completion are owner-thread-only.
    /// </summary>
    public sealed class MxUiCommandOutcomeChannel : IMxUiCommandOutcomeSource
    {
        private readonly int _ownerThreadId;
        private IMxUiCommandOutcomeSink _sink;
        private int _generation;

        public MxUiCommandOutcomeChannel()
        {
            _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public bool HasActiveBinding
        {
            get
            {
                EnsureOwnerThread();
                return _sink != null;
            }
        }

        public IMxUiCommandOutcomeBinding BindOutcomeSink(IMxUiCommandOutcomeSink sink)
        {
            EnsureOwnerThread();
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));
            if (_sink != null)
                throw new InvalidOperationException("The outcome source already has an active binding.");

            _generation = NextGeneration(_generation);
            _sink = sink;
            return new Binding(this, _generation, _ownerThreadId);
        }

        /// <summary>
        /// Sends an outcome to the active owner. NoAlloc. Returns false after teardown.
        /// </summary>
        public bool TryComplete(in MxUiCommandOutcome outcome)
        {
            EnsureOwnerThread();
            IMxUiCommandOutcomeSink sink = _sink;
            if (sink == null)
                return false;

            sink.Complete(in outcome);
            return true;
        }

        private void Release(int generation)
        {
            EnsureOwnerThread();
            if (generation == _generation)
                _sink = null;
        }

        private void EnsureOwnerThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
                throw new InvalidOperationException("UI command outcomes must use their owner thread.");
        }

        private static int NextGeneration(int current)
        {
            return current == int.MaxValue ? 1 : current + 1;
        }

        private sealed class Binding : IMxUiCommandOutcomeBinding
        {
            private readonly MxUiCommandOutcomeChannel _owner;
            private readonly int _generation;
            private readonly int _ownerThreadId;
            private bool _disposed;

            public Binding(
                MxUiCommandOutcomeChannel owner,
                int generation,
                int ownerThreadId)
            {
                _owner = owner;
                _generation = generation;
                _ownerThreadId = ownerThreadId;
            }

            public void Dispose()
            {
                if (Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
                    throw new InvalidOperationException("UI outcome bindings must be disposed on their owner thread.");
                if (_disposed)
                    return;

                _disposed = true;
                _owner.Release(_generation);
            }
        }
    }
}
