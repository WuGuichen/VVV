using System;

namespace MxFramework.Persistence
{
    public enum SaveGameOperationStatus { Pending = 0, Running = 1, Succeeded = 2, Failed = 3, Cancelled = 4, TimedOut = 5 }

    /// <summary>Async-shaped persistence operation.</summary>
    /// <remarks>
    /// Property access, polling, and cancellation requests are NoAlloc after initialization. Cancellation is cooperative
    /// and only available before the provider's atomic publication boundary. Core does not apply a default timeout.
    /// </remarks>
    public interface ISaveGameOperation<TResult>
    {
        SaveGameOperationId OperationId { get; }
        SaveGameOperationStatus Status { get; }
        float Progress { get; }
        /// <summary>Gets whether the operation reached a terminal status.</summary>
        bool IsDone { get; }
        /// <summary>Gets whether a cancellation request can still take effect before publication.</summary>
        bool CanCancel { get; }
        SaveGameError Error { get; }
        bool HasResult { get; }
        TResult Result { get; }
        bool TryGetResult(out TResult result);
        void Poll();
        void RequestCancel();
    }

    internal sealed class CompletedSaveGameOperation<TResult> : ISaveGameOperation<TResult>
    {
        public CompletedSaveGameOperation(SaveGameOperationId operationId, SaveGameOperationStatus status, TResult result, SaveGameError error)
        {
            OperationId = operationId; Status = status; Result = result; Error = error;
        }
        public SaveGameOperationId OperationId { get; }
        public SaveGameOperationStatus Status { get; }
        public float Progress => Status == SaveGameOperationStatus.Succeeded ? 1f : 0f;
        public bool IsDone => true;
        public bool CanCancel => false;
        public SaveGameError Error { get; }
        public bool HasResult => Status == SaveGameOperationStatus.Succeeded;
        public TResult Result { get; }
        public bool TryGetResult(out TResult result) { result = Result; return HasResult; }
        public void Poll() { }
        public void RequestCancel() { }
    }

    internal static class SaveGameOperations
    {
        public static ISaveGameOperation<TResult> Success<TResult>(SaveGameOperationId id, TResult result) => new CompletedSaveGameOperation<TResult>(id, SaveGameOperationStatus.Succeeded, result, SaveGameError.None);
        public static ISaveGameOperation<TResult> Failure<TResult>(SaveGameOperationId id, SaveGameError error) => new CompletedSaveGameOperation<TResult>(id, SaveGameOperationStatus.Failed, default, error);
        public static ISaveGameOperation<TResult> Cancelled<TResult>(SaveGameOperationId id) => new CompletedSaveGameOperation<TResult>(id, SaveGameOperationStatus.Cancelled, default, SaveGameError.None);
    }
}
