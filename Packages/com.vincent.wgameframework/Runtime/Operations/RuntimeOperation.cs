using System;

namespace MxFramework.Runtime
{
    public enum RuntimeOperationStatus
    {
        Pending = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3,
        Cancelled = 4,
        TimedOut = 5
    }

    public readonly struct RuntimeOperationError
    {
        public RuntimeOperationError(string code, string message)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string Message { get; }
        public bool IsNone => string.IsNullOrEmpty(Code) && string.IsNullOrEmpty(Message);

        public static RuntimeOperationError None => new RuntimeOperationError(string.Empty, string.Empty);

        public override string ToString()
        {
            return IsNone ? "None" : Code + " " + Message;
        }
    }

    public interface IRuntimeOperation
    {
        string OperationId { get; }
        RuntimeOperationStatus Status { get; }
        float Progress { get; }
        RuntimeOperationError Error { get; }
    }

    public sealed class RuntimeOperation : IRuntimeOperation
    {
        public RuntimeOperation(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                throw new ArgumentException("Operation id must be non-empty.", nameof(operationId));
            }

            OperationId = operationId;
            Status = RuntimeOperationStatus.Pending;
            Progress = 0f;
            Error = RuntimeOperationError.None;
        }

        public string OperationId { get; }
        public RuntimeOperationStatus Status { get; private set; }
        public float Progress { get; private set; }
        public RuntimeOperationError Error { get; private set; }
        public bool IsTerminal => IsTerminalStatus(Status);

        public void Start()
        {
            if (IsTerminal)
            {
                return;
            }

            if (Status == RuntimeOperationStatus.Running)
            {
                return;
            }

            Status = RuntimeOperationStatus.Running;
            Error = RuntimeOperationError.None;
        }

        public void ReportProgress(float progress)
        {
            if (IsTerminal)
            {
                return;
            }

            ValidateProgress(progress);
            RequireRunning(nameof(ReportProgress));
            Progress = progress;
        }

        public void Succeed()
        {
            if (IsTerminal)
            {
                return;
            }

            RequireRunning(nameof(Succeed));
            Status = RuntimeOperationStatus.Succeeded;
            Progress = 1f;
            Error = RuntimeOperationError.None;
        }

        public void Fail(RuntimeOperationError error)
        {
            if (IsTerminal)
            {
                return;
            }

            if (error.IsNone)
            {
                throw new ArgumentException("Failed operations must include an error.", nameof(error));
            }

            RequireRunning(nameof(Fail));
            Status = RuntimeOperationStatus.Failed;
            Error = error;
        }

        public void Cancel()
        {
            if (IsTerminal)
            {
                return;
            }

            RequireRunning(nameof(Cancel));
            Status = RuntimeOperationStatus.Cancelled;
            Error = RuntimeOperationError.None;
        }

        public void Timeout()
        {
            Timeout(RuntimeOperationError.None);
        }

        public void Timeout(RuntimeOperationError error)
        {
            if (IsTerminal)
            {
                return;
            }

            RequireRunning(nameof(Timeout));
            Status = RuntimeOperationStatus.TimedOut;
            Error = error;
        }

        private void RequireRunning(string operationName)
        {
            if (Status != RuntimeOperationStatus.Running)
            {
                throw new InvalidOperationException(operationName + " requires a running operation.");
            }
        }

        private static void ValidateProgress(float progress)
        {
            if (float.IsNaN(progress) || progress < 0f || progress > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "Progress must be between 0 and 1.");
            }
        }

        private static bool IsTerminalStatus(RuntimeOperationStatus status)
        {
            return status == RuntimeOperationStatus.Succeeded ||
                status == RuntimeOperationStatus.Failed ||
                status == RuntimeOperationStatus.Cancelled ||
                status == RuntimeOperationStatus.TimedOut;
        }
    }
}
