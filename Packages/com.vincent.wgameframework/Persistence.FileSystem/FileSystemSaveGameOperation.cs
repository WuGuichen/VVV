using System;
using System.Threading;

namespace MxFramework.Persistence.FileSystem
{
    internal enum FileSystemPublicationGateState
    {
        Active = 0,
        CancelRequested = 1,
        Publishing = 2,
        Sealed = 3
    }

    internal sealed class FileSystemPublicationGate
    {
        private int _state;

        public FileSystemPublicationGateState State =>
            (FileSystemPublicationGateState)Volatile.Read(ref _state);

        public bool CanCancel => State == FileSystemPublicationGateState.Active;
        public bool IsCancellationRequested => State == FileSystemPublicationGateState.CancelRequested;

        public bool RequestCancel()
        {
            return Interlocked.CompareExchange(
                ref _state,
                (int)FileSystemPublicationGateState.CancelRequested,
                (int)FileSystemPublicationGateState.Active)
                == (int)FileSystemPublicationGateState.Active;
        }

        public bool TryEnterPublishing()
        {
            return Interlocked.CompareExchange(
                ref _state,
                (int)FileSystemPublicationGateState.Publishing,
                (int)FileSystemPublicationGateState.Active)
                == (int)FileSystemPublicationGateState.Active;
        }

        public bool Seal(out bool cancellationWon)
        {
            while (true)
            {
                FileSystemPublicationGateState observed = State;
                switch (observed)
                {
                    case FileSystemPublicationGateState.Active:
                        if (Interlocked.CompareExchange(
                                ref _state,
                                (int)FileSystemPublicationGateState.Sealed,
                                (int)FileSystemPublicationGateState.Active)
                            == (int)FileSystemPublicationGateState.Active)
                        {
                            cancellationWon = false;
                            return true;
                        }
                        break;
                    case FileSystemPublicationGateState.CancelRequested:
                        if (Interlocked.CompareExchange(
                                ref _state,
                                (int)FileSystemPublicationGateState.Sealed,
                                (int)FileSystemPublicationGateState.CancelRequested)
                            == (int)FileSystemPublicationGateState.CancelRequested)
                        {
                            cancellationWon = true;
                            return true;
                        }
                        break;
                    case FileSystemPublicationGateState.Publishing:
                        if (Interlocked.CompareExchange(
                                ref _state,
                                (int)FileSystemPublicationGateState.Sealed,
                                (int)FileSystemPublicationGateState.Publishing)
                            == (int)FileSystemPublicationGateState.Publishing)
                        {
                            cancellationWon = false;
                            return true;
                        }
                        break;
                    case FileSystemPublicationGateState.Sealed:
                        cancellationWon = false;
                        return false;
                    default:
                        cancellationWon = false;
                        return false;
                }
            }
        }
    }

    internal sealed class FileSystemWorkerCapacity
    {
        private readonly int _maximum;
        private int _active;

        public FileSystemWorkerCapacity(int maximum = SaveGameLimits.MaximumConcurrentOperations)
        {
            if (maximum <= 0 || maximum > SaveGameLimits.MaximumConcurrentOperations)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }

            _maximum = maximum;
        }

        public int Maximum => _maximum;
        public int ActiveCount => Volatile.Read(ref _active);

        public bool TryAcquire(out FileSystemWorkerCapacityLease lease)
        {
            while (true)
            {
                int observed = Volatile.Read(ref _active);
                if (observed >= _maximum)
                {
                    lease = null;
                    return false;
                }

                if (Interlocked.CompareExchange(ref _active, observed + 1, observed) == observed)
                {
                    lease = new FileSystemWorkerCapacityLease(this);
                    return true;
                }
            }
        }

        internal void Release()
        {
            Interlocked.Decrement(ref _active);
        }
    }

    internal sealed class FileSystemWorkerCapacityLease : IDisposable
    {
        private FileSystemWorkerCapacity _owner;

        public FileSystemWorkerCapacityLease(FileSystemWorkerCapacity owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public void Dispose()
        {
            FileSystemWorkerCapacity owner = Interlocked.Exchange(ref _owner, null);
            if (owner != null)
            {
                owner.Release();
            }
        }
    }

    internal sealed class FileSystemOperationProgress
    {
        private int _value;
        public int Value => Volatile.Read(ref _value);

        public void Report(int value)
        {
            if (value < 0) value = 0;
            if (value > 1000) value = 1000;
            while (true)
            {
                int observed = Volatile.Read(ref _value);
                if (value <= observed) return;
                if (Interlocked.CompareExchange(ref _value, value, observed) == observed) return;
            }
        }
    }

    internal sealed class FileSystemOperationWorkerContext
    {
        private readonly FileSystemOperationProgress _progress;

        public FileSystemOperationWorkerContext(
            FileSystemPublicationGate publicationGate,
            FileSystemOperationProgress progress)
        {
            PublicationGate = publicationGate ?? throw new ArgumentNullException(nameof(publicationGate));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public FileSystemPublicationGate PublicationGate { get; }
        public bool IsCancellationRequested => PublicationGate.IsCancellationRequested;
        public bool TryEnterPublishing() => PublicationGate.TryEnterPublishing();
        public void ReportProgress(int value) => _progress.Report(value);
    }

    internal sealed class FileSystemOperationCompletion<TResult>
    {
        private FileSystemOperationCompletion(
            SaveGameOperationStatus status,
            TResult result,
            SaveGameError error)
        {
            Status = status;
            Result = result;
            Error = error;
        }

        public SaveGameOperationStatus Status { get; }
        public TResult Result { get; }
        public SaveGameError Error { get; }

        public static FileSystemOperationCompletion<TResult> Success(TResult result) =>
            new FileSystemOperationCompletion<TResult>(
                SaveGameOperationStatus.Succeeded,
                result,
                SaveGameError.None);

        public static FileSystemOperationCompletion<TResult> Failure(SaveGameError error)
        {
            if (error.IsNone) throw new ArgumentException("A failed completion requires an error.", nameof(error));
            return new FileSystemOperationCompletion<TResult>(SaveGameOperationStatus.Failed, default, error);
        }

        public static FileSystemOperationCompletion<TResult> Cancelled() =>
            new FileSystemOperationCompletion<TResult>(
                SaveGameOperationStatus.Cancelled,
                default,
                SaveGameError.None);

        public static FileSystemOperationCompletion<TResult> TimedOut() =>
            new FileSystemOperationCompletion<TResult>(
                SaveGameOperationStatus.TimedOut,
                default,
                new SaveGameError(
                    SaveGameErrorCode.Timeout,
                    SaveGameOperationPhase.None,
                    "timeout",
                    "Operation timed out."));
    }

    /// <summary>Owner-thread terminal promotion for a worker-backed filesystem operation.</summary>
    /// <remarks>
    /// AllocByDesign at creation. Worker completion is immutable and releases capacity in worker finally. Property
    /// access, terminal Poll, TryGetResult and RequestCancel are NoAlloc after initialization.
    /// </remarks>
    internal sealed class FileSystemSaveGameOperation<TResult> : ISaveGameOperation<TResult>
    {
        private sealed class WorkerState
        {
            public WorkerState(
                FileSystemSaveGameOperation<TResult> operation,
                Func<FileSystemOperationWorkerContext, FileSystemOperationCompletion<TResult>> worker,
                FileSystemWorkerCapacityLease lease)
            {
                Operation = operation;
                Worker = worker;
                Lease = lease;
            }

            public FileSystemSaveGameOperation<TResult> Operation { get; }
            public Func<FileSystemOperationWorkerContext, FileSystemOperationCompletion<TResult>> Worker { get; }
            public FileSystemWorkerCapacityLease Lease { get; }
        }

        private static readonly WaitCallback WorkerCallback = ExecuteWorker;
        private readonly int _ownerThreadId;
        private readonly FileSystemPublicationGate _publicationGate;
        private readonly FileSystemOperationProgress _progress;
        private CancellationTokenRegistration _cancellationRegistration;
        private FileSystemOperationCompletion<TResult> _workerCompletion;
        private int _status;
        private TResult _result;
        private SaveGameError _error;

        private FileSystemSaveGameOperation(SaveGameOperationId operationId)
        {
            if (!operationId.IsValid) throw new ArgumentException("Operation id is invalid.", nameof(operationId));
            OperationId = operationId;
            _ownerThreadId = Thread.CurrentThread.ManagedThreadId;
            _publicationGate = new FileSystemPublicationGate();
            _progress = new FileSystemOperationProgress();
            _status = (int)SaveGameOperationStatus.Pending;
        }

        public SaveGameOperationId OperationId { get; }
        public SaveGameOperationStatus Status => (SaveGameOperationStatus)Volatile.Read(ref _status);
        public float Progress => _progress.Value * 0.001f;
        public bool IsDone => IsTerminal(Status);
        public bool CanCancel => Volatile.Read(ref _workerCompletion) == null && _publicationGate.CanCancel;
        public SaveGameError Error => _error;
        public bool HasResult => Status == SaveGameOperationStatus.Succeeded;
        public TResult Result => _result;
        internal int OwnerThreadId => _ownerThreadId;
        internal bool HasWorkerCompletion => Volatile.Read(ref _workerCompletion) != null;
        internal FileSystemPublicationGate PublicationGate => _publicationGate;

        public static FileSystemSaveGameOperation<TResult> Start(
            SaveGameOperationId operationId,
            FileSystemWorkerCapacity capacity,
            Func<FileSystemOperationWorkerContext, FileSystemOperationCompletion<TResult>> worker,
            CancellationToken cancellationToken = default)
        {
            if (capacity == null) throw new ArgumentNullException(nameof(capacity));
            if (worker == null) throw new ArgumentNullException(nameof(worker));

            var operation = new FileSystemSaveGameOperation<TResult>(operationId);
            FileSystemWorkerCapacityLease lease;
            if (!capacity.TryAcquire(out lease))
            {
                Volatile.Write(
                    ref operation._workerCompletion,
                    FileSystemOperationCompletion<TResult>.Failure(
                        new SaveGameError(
                            SaveGameErrorCode.Busy,
                            SaveGameOperationPhase.Validation,
                            "operations",
                            "Concurrent operation capacity is exhausted.")));
                return operation;
            }

            if (cancellationToken.CanBeCanceled)
            {
                operation._cancellationRegistration = cancellationToken.Register(
                    CancelFromToken,
                    operation._publicationGate);
            }

            var state = new WorkerState(operation, worker, lease);
            if (!ThreadPool.QueueUserWorkItem(WorkerCallback, state))
            {
                operation._cancellationRegistration.Dispose();
                lease.Dispose();
                Volatile.Write(
                    ref operation._workerCompletion,
                    FileSystemOperationCompletion<TResult>.Failure(
                        new SaveGameError(
                            SaveGameErrorCode.StoreUnavailable,
                            SaveGameOperationPhase.None,
                            "operations",
                            "Filesystem worker could not be queued.")));
            }

            return operation;
        }

        public bool TryGetResult(out TResult result)
        {
            if (!HasResult)
            {
                result = default;
                return false;
            }

            // Acquire the terminal status before copying the result published by the owner Poll.
            result = _result;
            return true;
        }

        public void Poll()
        {
            if (Thread.CurrentThread.ManagedThreadId != _ownerThreadId || IsDone)
            {
                return;
            }

            FileSystemOperationCompletion<TResult> completion = Volatile.Read(ref _workerCompletion);
            if (completion == null)
            {
                return;
            }

            _result = completion.Result;
            _error = completion.Error;
            if (completion.Status == SaveGameOperationStatus.Succeeded)
            {
                _progress.Report(1000);
            }

            Volatile.Write(ref _status, (int)completion.Status);
        }

        public void RequestCancel()
        {
            if (Volatile.Read(ref _workerCompletion) == null)
            {
                _publicationGate.RequestCancel();
            }
        }

        private static void ExecuteWorker(object stateObject)
        {
            var state = (WorkerState)stateObject;
            FileSystemSaveGameOperation<TResult> operation = state.Operation;
            FileSystemOperationCompletion<TResult> completion;
            Volatile.Write(ref operation._status, (int)SaveGameOperationStatus.Running);
            try
            {
                if (operation._publicationGate.IsCancellationRequested)
                {
                    completion = FileSystemOperationCompletion<TResult>.Cancelled();
                }
                else
                {
                    var context = new FileSystemOperationWorkerContext(
                        operation._publicationGate,
                        operation._progress);
                    completion = state.Worker(context);
                    if (completion == null)
                    {
                        completion = WorkerFailure();
                    }
                    else if (completion.Status == SaveGameOperationStatus.Succeeded
                        && operation._publicationGate.IsCancellationRequested)
                    {
                        completion = FileSystemOperationCompletion<TResult>.Cancelled();
                    }
                }
            }
            catch (Exception)
            {
                completion = WorkerFailure();
            }

            bool cancellationWon;
            operation._publicationGate.Seal(out cancellationWon);
            if (cancellationWon)
            {
                completion = FileSystemOperationCompletion<TResult>.Cancelled();
            }

            try
            {
                operation._cancellationRegistration.Dispose();
            }
            catch (Exception)
            {
                // Token registration cleanup cannot replace an already formed provider outcome.
            }

            try
            {
                Volatile.Write(ref operation._workerCompletion, completion);
            }
            finally
            {
                state.Lease.Dispose();
            }
        }

        private static void CancelFromToken(object state)
        {
            ((FileSystemPublicationGate)state).RequestCancel();
        }

        private static FileSystemOperationCompletion<TResult> WorkerFailure()
        {
            return FileSystemOperationCompletion<TResult>.Failure(
                new SaveGameError(
                    SaveGameErrorCode.IoFailure,
                    SaveGameOperationPhase.None,
                    "worker",
                    "Filesystem worker failed."));
        }

        private static bool IsTerminal(SaveGameOperationStatus status)
        {
            return status == SaveGameOperationStatus.Succeeded
                || status == SaveGameOperationStatus.Failed
                || status == SaveGameOperationStatus.Cancelled
                || status == SaveGameOperationStatus.TimedOut;
        }
    }
}
