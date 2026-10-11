using System;
using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup
{
    public enum ApplicationLiveRetirementStatus
    {
        Created = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3
    }

    /// <summary>Structured, ordered best-effort retirement warning. Value reads are NoAlloc.</summary>
    public readonly struct ApplicationLiveRetirementWarning
    {
        public ApplicationLiveRetirementWarning(
            string domainId,
            string stableCode,
            RuntimeOperationError innerError,
            string message,
            Exception exception)
        {
            DomainId = domainId ?? string.Empty;
            StableCode = stableCode ?? string.Empty;
            InnerError = innerError;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public string DomainId { get; }
        public string StableCode { get; }
        public RuntimeOperationError InnerError { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public bool IsNone => string.IsNullOrEmpty(DomainId) && string.IsNullOrEmpty(StableCode);
    }

    /// <summary>
    /// Truthful evidence that some retired ownership may remain after cleanup stopped.
    /// Value reads are NoAlloc.
    /// </summary>
    public readonly struct ApplicationLiveRetirementOwnershipDebt
    {
        public ApplicationLiveRetirementOwnershipDebt(
            string domainId,
            string stableCode,
            string message,
            Exception exception)
        {
            DomainId = domainId ?? string.Empty;
            StableCode = stableCode ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public string DomainId { get; }
        public string StableCode { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public bool IsNone => string.IsNullOrEmpty(DomainId) && string.IsNullOrEmpty(StableCode);
        public static ApplicationLiveRetirementOwnershipDebt None => default;
    }

    /// <summary>Immutable retirement context. Construction and value reads are NoAlloc.</summary>
    public readonly struct ApplicationLiveRetirementContext
    {
        public ApplicationLiveRetirementContext(long retiredRevision, string sessionId)
        {
            if (retiredRevision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(retiredRevision));
            }

            RetiredRevision = retiredRevision;
            SessionId = sessionId ?? string.Empty;
        }

        public long RetiredRevision { get; }
        public string SessionId { get; }
    }

    /// <summary>
    /// Cleanup operation owned exclusively by the retirement driver. Warning order is the stable
    /// zero-based order returned by GetWarning. Steady Status/Progress/Error/warning/debt reads and
    /// Tick must be NoAlloc after operation initialization, outside implementation-owned failures.
    /// </summary>
    public interface IApplicationLiveRetirementOperation : IRuntimeOperation
    {
        int WarningCount { get; }
        ApplicationLiveRetirementWarning GetWarning(int index);
        ApplicationLiveRetirementOwnershipDebt OwnershipDebt { get; }
        void Tick(in ApplicationLiveRetirementContext context);
    }

    /// <summary>
    /// Optional asynchronous cleanup factory implemented by a live session lease. BeginRetirement
    /// transfers cleanup execution to the returned operation and is AllocByDesign on the retirement path.
    /// </summary>
    public interface IApplicationLiveRetirementSource
    {
        IApplicationLiveRetirementOperation BeginRetirement(in ApplicationLiveRetirementContext context);
    }

    /// <summary>
    /// Drives the distinct LiveOwner -&gt; RetirementOwner lifecycle. Construction and Begin are
    /// LowFreqAlloc/AllocByDesign; steady Tick and observation are NoAlloc after initialization.
    /// A failed driver retains ownership debt and must keep its publisher reservation poisoned.
    /// </summary>
    public sealed class ApplicationLiveRetirementDriver
    {
        private const string DriverDomain = "mxframework.application.live-retirement";
        private const string BeginExceptionCode = "begin.exception";
        private const string BeginNullCode = "begin.null-operation";
        private const string PollExceptionCode = "operation.poll-exception";
        private const string TickExceptionCode = "operation.tick-exception";
        private const string InvalidStatusCode = "operation.invalid-status";
        private const string InvalidProgressCode = "operation.invalid-progress";
        private const string InvalidErrorCode = "operation.invalid-error";
        private const string TerminalFailureCode = "operation.terminal-failure";
        private const string SuccessWithDebtCode = "operation.success-with-debt";

        private readonly ApplicationLiveRetirementContext _context;
        private readonly IApplicationLiveSessionLease _ownerLease;
        private IApplicationLiveRetirementOperation _operation;
        private ApplicationLiveRetirementStatus _status;
        private float _progress;
        private RuntimeOperationError _error;
        private int _operationWarningCount;
        private ApplicationLiveRetirementOwnershipDebt _operationDebt;
        private ApplicationLiveRetirementWarning _localWarning;
        private ApplicationLiveRetirementOwnershipDebt _localDebt;
        private bool _hasLocalWarning;
        private bool _hasLocalDebt;
        private bool _hasObservedOperationStatus;
        private RuntimeOperationStatus _lastOperationStatus;

        public ApplicationLiveRetirementDriver(
            long retiredRevision,
            string sessionId,
            IApplicationLiveSessionLease ownerLease)
        {
            _context = new ApplicationLiveRetirementContext(retiredRevision, sessionId);
            _ownerLease = ownerLease ?? throw new ArgumentNullException(nameof(ownerLease));
            _status = ApplicationLiveRetirementStatus.Created;
            _error = RuntimeOperationError.None;
        }

        public ApplicationLiveRetirementStatus Status => _status;
        public float Progress => _progress;
        public RuntimeOperationError Error => _error;
        public bool IsTerminal => _status == ApplicationLiveRetirementStatus.Succeeded ||
                                  _status == ApplicationLiveRetirementStatus.Failed;
        public bool CanReleaseReservation => _status == ApplicationLiveRetirementStatus.Succeeded && OwnershipDebt.IsNone;

        public int WarningCount
        {
            get => _operationWarningCount + (_hasLocalWarning ? 1 : 0);
        }

        public ApplicationLiveRetirementOwnershipDebt OwnershipDebt =>
            _hasLocalDebt ? _localDebt : _operationDebt;

        public ApplicationLiveRetirementWarning GetWarning(int index)
        {
            if ((uint)index >= (uint)WarningCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return index < _operationWarningCount
                ? _operation.GetWarning(index)
                : _localWarning;
        }

        public void Begin()
        {
            if (_status != ApplicationLiveRetirementStatus.Created)
            {
                return;
            }

            _status = ApplicationLiveRetirementStatus.Running;
            try
            {
                if (_ownerLease is IApplicationLiveRetirementSource source)
                {
                    _operation = source.BeginRetirement(in _context);
                }
                else
                {
                    _operation = new DisposeLiveRetirementOperation(_ownerLease);
                }
            }
            catch (Exception exception)
            {
                FailLocally(BeginExceptionCode, "Beginning live session retirement threw an exception.", exception, RuntimeOperationError.None);
                return;
            }

            if (_operation == null)
            {
                FailLocally(BeginNullCode, "Live session retirement returned no cleanup operation.", null, RuntimeOperationError.None);
                return;
            }

            ObserveOperation();
        }

        /// <summary>Advances an initialized retirement operation. NoAlloc outside failure handling.</summary>
        public void Tick()
        {
            if (_status != ApplicationLiveRetirementStatus.Running || _operation == null)
            {
                return;
            }

            ObserveOperation();
            if (_status != ApplicationLiveRetirementStatus.Running)
            {
                return;
            }

            try
            {
                _operation.Tick(in _context);
            }
            catch (Exception exception)
            {
                FailLocally(TickExceptionCode, "Ticking live session retirement threw an exception.", exception, RuntimeOperationError.None);
                return;
            }

            ObserveOperation();
        }

        private void ObserveOperation()
        {
            RuntimeOperationStatus status;
            RuntimeOperationError error;
            float progress;
            int warningCount;
            ApplicationLiveRetirementOwnershipDebt debt;
            try
            {
                status = _operation.Status;
                error = _operation.Error;
                progress = _operation.Progress;
                warningCount = _operation.WarningCount;
                debt = _operation.OwnershipDebt;
            }
            catch (Exception exception)
            {
                FailLocally(PollExceptionCode, "Reading live session retirement evidence threw an exception.", exception, RuntimeOperationError.None);
                return;
            }

            if (warningCount < 0)
            {
                FailLocally(InvalidStatusCode, "Live session retirement exposed a negative warning count.", null, error);
                return;
            }

            if (!IsKnownStatus(status))
            {
                FailLocally(InvalidStatusCode, "Live session retirement exposed an unknown operation status.", null, error);
                return;
            }

            if (_hasObservedOperationStatus &&
                _lastOperationStatus == RuntimeOperationStatus.Running &&
                status == RuntimeOperationStatus.Pending)
            {
                FailLocally(InvalidStatusCode, "Live session retirement regressed from running to pending.", null, error);
                return;
            }

            if (float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0f || progress > 1f)
            {
                FailLocally(InvalidProgressCode, "Live session retirement exposed progress outside the finite 0..1 range.", null, error);
                return;
            }

            if (!HasConsistentError(status, error))
            {
                FailLocally(InvalidErrorCode, "Live session retirement exposed an error inconsistent with its operation status.", null, error);
                return;
            }

            if (status == RuntimeOperationStatus.Succeeded && progress != 1f)
            {
                FailLocally(InvalidProgressCode, "Successful live session retirement must report complete progress.", null, error);
                return;
            }

            _progress = progress;
            _error = error;
            _operationWarningCount = warningCount;
            _operationDebt = debt;
            _hasObservedOperationStatus = true;
            _lastOperationStatus = status;
            if (status == RuntimeOperationStatus.Pending || status == RuntimeOperationStatus.Running)
            {
                return;
            }

            if (status == RuntimeOperationStatus.Succeeded)
            {
                if (!debt.IsNone)
                {
                    _status = ApplicationLiveRetirementStatus.Failed;
                    AddLocalWarning(SuccessWithDebtCode, "Live session retirement reported success while retaining ownership debt.", debt.Exception, error);
                    return;
                }

                _status = ApplicationLiveRetirementStatus.Succeeded;
                _progress = 1f;
                _error = RuntimeOperationError.None;
                return;
            }

            if (status == RuntimeOperationStatus.Failed ||
                status == RuntimeOperationStatus.Cancelled ||
                status == RuntimeOperationStatus.TimedOut)
            {
                _status = ApplicationLiveRetirementStatus.Failed;
                if (debt.IsNone)
                {
                    FailLocally(TerminalFailureCode, "Live session retirement stopped before cleanup ownership was discharged.", null, error);
                }
                else if (warningCount == 0)
                {
                    AddLocalWarning(TerminalFailureCode, "Live session retirement stopped before cleanup ownership was discharged.", debt.Exception, error);
                }

                return;
            }

            FailLocally(InvalidStatusCode, "Live session retirement exposed an unknown operation status.", null, error);
        }

        private void FailLocally(string stableCode, string message, Exception exception, RuntimeOperationError innerError)
        {
            AddLocalWarning(stableCode, message, exception, innerError);
            _hasLocalDebt = true;
            _localDebt = new ApplicationLiveRetirementOwnershipDebt(DriverDomain, stableCode, message, exception);
            _status = ApplicationLiveRetirementStatus.Failed;
            _error = innerError.IsNone ? new RuntimeOperationError(stableCode, message) : innerError;
        }

        private void AddLocalWarning(string stableCode, string message, Exception exception, RuntimeOperationError innerError)
        {
            _hasLocalWarning = true;
            _localWarning = new ApplicationLiveRetirementWarning(DriverDomain, stableCode, innerError, message, exception);
        }

        private static bool IsKnownStatus(RuntimeOperationStatus status) =>
            status == RuntimeOperationStatus.Pending ||
            status == RuntimeOperationStatus.Running ||
            status == RuntimeOperationStatus.Succeeded ||
            status == RuntimeOperationStatus.Failed ||
            status == RuntimeOperationStatus.Cancelled ||
            status == RuntimeOperationStatus.TimedOut;

        private static bool HasConsistentError(RuntimeOperationStatus status, RuntimeOperationError error)
        {
            if (status == RuntimeOperationStatus.Failed)
            {
                return !error.IsNone;
            }

            return status == RuntimeOperationStatus.TimedOut || error.IsNone;
        }

        private sealed class DisposeLiveRetirementOperation : IApplicationLiveRetirementOperation
        {
            private readonly IApplicationLiveSessionLease _lease;
            private RuntimeOperationStatus _status;
            private RuntimeOperationError _error;
            private ApplicationLiveRetirementWarning _warning;
            private ApplicationLiveRetirementOwnershipDebt _debt;

            public DisposeLiveRetirementOperation(IApplicationLiveSessionLease lease)
            {
                _lease = lease;
                _status = RuntimeOperationStatus.Running;
                _error = RuntimeOperationError.None;
            }

            public string OperationId => "mxframework.application.live-retirement.dispose";
            public RuntimeOperationStatus Status => _status;
            public float Progress => _status == RuntimeOperationStatus.Succeeded ? 1f : 0f;
            public RuntimeOperationError Error => _error;
            public int WarningCount => _warning.IsNone ? 0 : 1;
            public ApplicationLiveRetirementOwnershipDebt OwnershipDebt => _debt;

            public ApplicationLiveRetirementWarning GetWarning(int index)
            {
                if (index != 0 || _warning.IsNone)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _warning;
            }

            /// <summary>Disposes the synchronous owner once. NoAlloc outside failure handling.</summary>
            public void Tick(in ApplicationLiveRetirementContext context)
            {
                if (_status != RuntimeOperationStatus.Running)
                {
                    return;
                }

                try
                {
                    _lease.Dispose();
                    _status = RuntimeOperationStatus.Succeeded;
                }
                catch (Exception exception)
                {
                    const string code = "lease.dispose-exception";
                    const string message = "Disposing the retired live session lease threw an exception.";
                    _warning = new ApplicationLiveRetirementWarning(DriverDomain, code, RuntimeOperationError.None, message, exception);
                    _debt = new ApplicationLiveRetirementOwnershipDebt(DriverDomain, code, message, exception);
                    _error = new RuntimeOperationError(code, message);
                    _status = RuntimeOperationStatus.Failed;
                }
            }
        }
    }
}
