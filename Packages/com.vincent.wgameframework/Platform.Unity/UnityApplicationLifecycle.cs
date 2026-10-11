using System;
using UnityEngine;

namespace MxFramework.Platform.Unity
{
    /// <summary>
    /// Enforces main-thread lifecycle reporting and exposes the normalized bounded source.
    /// </summary>
    public sealed class UnityApplicationLifecycleReporter :
        IApplicationLifecycleReporter,
        IApplicationLifecycleSource
    {
        private sealed class DrainOwner : IApplicationLifecycleDrainOwner
        {
            private readonly UnityApplicationLifecycleReporter _reporter;
            private readonly IApplicationLifecycleDrainOwner _inner;

            public DrainOwner(UnityApplicationLifecycleReporter reporter, IApplicationLifecycleDrainOwner inner)
            {
                _reporter = reporter;
                _inner = inner;
            }

            public ApplicationLifecycleSnapshot Current =>
                _reporter.IsOwnerThread() ? _inner.Current : default;
            public long DroppedChangeCount =>
                _reporter.IsOwnerThread() ? _inner.DroppedChangeCount : 0L;
            public bool TryDequeue(out ApplicationLifecycleChange change)
            {
                if (!_reporter.IsOwnerThread())
                {
                    change = default;
                    return false;
                }
                return _inner.TryDequeue(out change);
            }
        }

        private readonly ApplicationLifecycleBuffer _buffer;
        private readonly int _ownerThreadId;
        private DrainOwner _drainOwner;

        /// <summary>
        /// Creates a revision-zero lifecycle reporter on the calling owner thread.
        /// </summary>
        /// <remarks>AllocByDesign for the fixed lifecycle buffer. Warm report/read/drain is NoAlloc.</remarks>
        public UnityApplicationLifecycleReporter(
            bool initialHasFocus,
            bool initialIsSuspended,
            int capacity = 16)
        {
            _ownerThreadId = Environment.CurrentManagedThreadId;
            _buffer = new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(
                    initialHasFocus,
                    initialIsSuspended,
                    isQuitting: false,
                    revision: 0),
                capacity);
        }

        /// <summary>
        /// Creates a lifecycle reporter from the current Unity focus fact.
        /// </summary>
        /// <remarks>AllocByDesign during composition.</remarks>
        public static UnityApplicationLifecycleReporter CreateForCurrentApplication(int capacity = 16)
        {
            return new UnityApplicationLifecycleReporter(
                Application.isFocused,
                initialIsSuspended: false,
                capacity);
        }

        /// <summary>
        /// Gets the owner managed-thread id captured at construction.
        /// </summary>
        public int OwnerThreadId => _ownerThreadId;

        /// <inheritdoc />
        /// <inheritdoc />
        public bool ReportFocus(bool hasFocus)
        {
            return IsOwnerThread() && _buffer.ReportFocus(hasFocus);
        }

        /// <inheritdoc />
        public bool ReportSuspend(bool isSuspended)
        {
            return IsOwnerThread() && _buffer.ReportSuspend(isSuspended);
        }

        /// <inheritdoc />
        public bool ReportQuitting()
        {
            return IsOwnerThread() && _buffer.ReportQuitting();
        }

        /// <inheritdoc />
        public bool TryClaimDrain(out IApplicationLifecycleDrainOwner drainOwner)
        {
            if (!IsOwnerThread() || _drainOwner != null || !_buffer.TryClaimDrain(out IApplicationLifecycleDrainOwner inner))
            {
                drainOwner = null;
                return false;
            }

            var owner = new DrainOwner(this, inner);
            _drainOwner = owner;
            drainOwner = owner;
            return true;
        }

        private bool IsOwnerThread()
        {
            return Environment.CurrentManagedThreadId == _ownerThreadId;
        }
    }

}
