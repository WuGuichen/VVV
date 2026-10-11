using System;
using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup
{
    public enum ApplicationStartupState
    {
        Created = 0,
        Running = 1,
        Draining = 2,
        RollingBack = 3,
        Succeeded = 4,
        Failed = 5,
        Cancelled = 6,
        TimedOut = 7,
        RollbackFailed = 8,
        Disposed = 9
    }

    public enum ApplicationStartupErrorCode
    {
        None = 0,
        InvalidPlan = 1,
        DuplicateStep = 2,
        MissingDependency = 3,
        DependencyCycle = 4,
        InvalidWeight = 5,
        OwnerConflict = 6,
        StepBeginFailed = 100,
        StepException = 101,
        StepFailed = 102,
        StepCancelled = 103,
        StepTimedOut = 104,
        PublicationConflict = 200,
        CommitFailed = 201,
        RollbackBeginFailed = 300,
        RollbackFailed = 301,
        RollbackException = 302,
        CleanupIncomplete = 303,
        Cancelled = 400,
        Timeout = 401
    }

    public enum ApplicationStartupPhase
    {
        None = 0,
        Validation = 1,
        Forward = 2,
        Commit = 3,
        Rollback = 4,
        Cleanup = 5
    }

    /// <summary>Immutable structured startup error. Construction is LowFreqAlloc only when a message or exception is supplied.</summary>
    public readonly struct ApplicationStartupError
    {
        public ApplicationStartupError(
            ApplicationStartupErrorCode code,
            ApplicationStartupPhase phase,
            ApplicationStartupStepId stepId,
            RuntimeOperationError innerError,
            IApplicationStartupInnerErrorDetail innerDetail,
            string message,
            Exception exception)
        {
            Code = code;
            Phase = phase;
            StepId = stepId;
            InnerError = innerError;
            InnerDetail = innerDetail;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public ApplicationStartupErrorCode Code { get; }
        public ApplicationStartupPhase Phase { get; }
        public ApplicationStartupStepId StepId { get; }
        public RuntimeOperationError InnerError { get; }
        public IApplicationStartupInnerErrorDetail InnerDetail { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public bool IsNone => Code == ApplicationStartupErrorCode.None;

        public static ApplicationStartupError None => default;
    }

    public interface IApplicationStartupInnerErrorDetail
    {
        string DomainId { get; }
        string StableCode { get; }
    }

    public readonly struct ApplicationStartupTickContext
    {
        public ApplicationStartupTickContext(double deltaSeconds, double elapsedSeconds)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            DeltaSeconds = deltaSeconds;
            ElapsedSeconds = elapsedSeconds;
        }

        public double DeltaSeconds { get; }
        public double ElapsedSeconds { get; }
    }

    public readonly struct ApplicationStartupStepContext
    {
        public ApplicationStartupStepContext(string runId, IApplicationStartupTransaction transaction)
        {
            RunId = runId ?? string.Empty;
            Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        public string RunId { get; }
        public IApplicationStartupTransaction Transaction { get; }
    }

    public readonly struct ApplicationStartupRollbackContext
    {
        public ApplicationStartupRollbackContext(string runId, IApplicationStartupTransaction transaction, ApplicationStartupError forwardError)
        {
            RunId = runId ?? string.Empty;
            Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            ForwardError = forwardError;
        }

        public string RunId { get; }
        public IApplicationStartupTransaction Transaction { get; }
        public ApplicationStartupError ForwardError { get; }
    }

    public interface IApplicationStartupStepOperation : IRuntimeOperation
    {
        bool CanCancel { get; }
        IApplicationStartupInnerErrorDetail InnerErrorDetail { get; }
        void Tick(in ApplicationStartupTickContext context);
        void RequestCancel();
    }

    /// <summary>
    /// Optional ownership-preserving drain path for operations whose normal Tick throws after
    /// potentially entering external or asynchronous work.
    /// </summary>
    /// <remarks>
    /// RequestFaultDrain and TickFaultDrain must not relinquish ownership before Status is terminal.
    /// Implementations that do not implement this interface must provide a strong exception guarantee:
    /// a throwing Tick must already expose a truthful terminal Status. Fault draining is NoAlloc after
    /// operation initialization outside implementation-owned diagnostics.
    /// </remarks>
    public interface IApplicationStartupFaultDrainOperation
    {
        void RequestFaultDrain(Exception exception);
        void TickFaultDrain(in ApplicationStartupTickContext context);
    }

    public interface IApplicationStartupStep
    {
        ApplicationStartupStepId StepId { get; }
        IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context);
        IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context);
    }

    public sealed class ApplicationStartupStepDescriptor
    {
        private readonly ApplicationStartupStepId[] _dependencies;

        public ApplicationStartupStepDescriptor(
            ApplicationStartupStepId stepId,
            ApplicationStartupStepId[] dependencies,
            int order,
            float progressWeight,
            double timeoutSeconds = 0d,
            string exclusiveOwnerId = null)
        {
            if (!stepId.IsValid)
            {
                throw new ArgumentException("Step id must be valid.", nameof(stepId));
            }

            StepId = stepId;
            _dependencies = dependencies == null ? Array.Empty<ApplicationStartupStepId>() : (ApplicationStartupStepId[])dependencies.Clone();
            Order = order;
            ProgressWeight = progressWeight;
            TimeoutSeconds = timeoutSeconds;
            ExclusiveOwnerId = exclusiveOwnerId ?? string.Empty;
        }

        public ApplicationStartupStepId StepId { get; }
        public int DependencyCount => _dependencies.Length;
        public int Order { get; }
        public float ProgressWeight { get; }
        public double TimeoutSeconds { get; }
        public string ExclusiveOwnerId { get; }
        public ApplicationStartupStepId GetDependency(int index) => _dependencies[index];
    }
}
