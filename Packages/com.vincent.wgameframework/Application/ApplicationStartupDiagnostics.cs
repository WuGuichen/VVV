using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup
{
    /// <summary>Low-frequency immutable diagnostic capture. Capture is AllocByDesign.</summary>
    public sealed class ApplicationStartupSnapshot
    {
        public ApplicationStartupSnapshot(
            string runId,
            ApplicationStartupPlanId planId,
            string transactionId,
            ApplicationStartupState state,
            ApplicationStartupStepId currentStepId,
            int completedStepCount,
            float forwardProgress,
            float rollbackProgress,
            bool cancelRequested,
            double elapsedSeconds,
            double stepElapsedSeconds,
            ApplicationStartupError forwardError,
            int rollbackErrorCount,
            int cleanupWarningCount,
            RuntimeOperationStatus currentOperationStatus,
            RuntimeOperationError currentOperationError,
            IApplicationStartupInnerErrorDetail currentInnerDetail,
            RuntimeOperationStatus drainedOperationStatus,
            RuntimeOperationError drainedOperationError,
            IApplicationStartupInnerErrorDetail drainedInnerDetail)
        {
            RunId = runId ?? string.Empty;
            PlanId = planId;
            TransactionId = transactionId ?? string.Empty;
            State = state;
            CurrentStepId = currentStepId;
            CompletedStepCount = completedStepCount;
            ForwardProgress = forwardProgress;
            RollbackProgress = rollbackProgress;
            CancelRequested = cancelRequested;
            ElapsedSeconds = elapsedSeconds;
            StepElapsedSeconds = stepElapsedSeconds;
            ForwardError = forwardError;
            RollbackErrorCount = rollbackErrorCount;
            CleanupWarningCount = cleanupWarningCount;
            CurrentOperationStatus = currentOperationStatus;
            CurrentOperationError = currentOperationError;
            CurrentInnerDetail = currentInnerDetail;
            DrainedOperationStatus = drainedOperationStatus;
            DrainedOperationError = drainedOperationError;
            DrainedInnerDetail = drainedInnerDetail;
        }

        public string RunId { get; }
        public ApplicationStartupPlanId PlanId { get; }
        public string TransactionId { get; }
        public ApplicationStartupState State { get; }
        public ApplicationStartupStepId CurrentStepId { get; }
        public int CompletedStepCount { get; }
        public float ForwardProgress { get; }
        public float RollbackProgress { get; }
        public bool CancelRequested { get; }
        public double ElapsedSeconds { get; }
        public double StepElapsedSeconds { get; }
        public ApplicationStartupError ForwardError { get; }
        public int RollbackErrorCount { get; }
        public int CleanupWarningCount { get; }
        public RuntimeOperationStatus CurrentOperationStatus { get; }
        public RuntimeOperationError CurrentOperationError { get; }
        public IApplicationStartupInnerErrorDetail CurrentInnerDetail { get; }
        public RuntimeOperationStatus DrainedOperationStatus { get; }
        public RuntimeOperationError DrainedOperationError { get; }
        public IApplicationStartupInnerErrorDetail DrainedInnerDetail { get; }
    }
}
