using System;

namespace MxFramework.Runtime
{
    public enum RuntimeHostOperation
    {
        Register = 0,
        Initialize = 1,
        Start = 2,
        Tick = 3,
        Stop = 4,
        Dispose = 5
    }

    public sealed class RuntimeHostError
    {
        public RuntimeHostError(
            string moduleId,
            RuntimeHostOperation operation,
            RuntimeLifecycleState lifecycleState,
            Exception exception,
            long frameIndex = -1,
            RuntimeTickStage? tickStage = null)
        {
            ModuleId = moduleId ?? string.Empty;
            Operation = operation;
            LifecycleState = lifecycleState;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            FrameIndex = frameIndex;
            TickStage = tickStage;
        }

        public string ModuleId { get; }
        public RuntimeHostOperation Operation { get; }
        public RuntimeLifecycleState LifecycleState { get; }
        public Exception Exception { get; }
        public long FrameIndex { get; }
        public RuntimeTickStage? TickStage { get; }
        public string Message => Exception.Message;
    }

    public sealed class RuntimeHostException : Exception
    {
        public RuntimeHostException(RuntimeHostError error)
            : base(CreateMessage(error), error != null ? error.Exception : null)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public RuntimeHostError Error { get; }

        private static string CreateMessage(RuntimeHostError error)
        {
            if (error == null)
            {
                return "Runtime host error.";
            }

            string module = string.IsNullOrEmpty(error.ModuleId) ? "<host>" : error.ModuleId;
            return "Runtime host " + error.Operation + " failed for module '" + module + "': " + error.Exception.Message;
        }
    }
}
