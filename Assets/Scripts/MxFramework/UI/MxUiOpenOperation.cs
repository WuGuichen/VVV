using System;

namespace MxFramework.UI
{
    public enum MxUiOpenOperationStatus
    {
        Pending = 0,
        Succeeded = 10,
        Failed = 20,
        Cancelled = 30
    }

    public sealed class MxUiOpenOperation
    {
        private Action<MxUiOpenResult> _completed;

        public MxUiOpenOperation()
        {
            Status = MxUiOpenOperationStatus.Pending;
            Result = MxUiOpenResult.Fail(MxUiOpenErrorCode.ResourcesPending, "UI open operation is pending.");
        }

        private MxUiOpenOperation(MxUiOpenResult result)
        {
            Complete(result);
        }

        public MxUiOpenOperationStatus Status { get; private set; }
        public MxUiOpenResult Result { get; private set; }
        public bool IsCompleted => Status != MxUiOpenOperationStatus.Pending;

        public event Action<MxUiOpenResult> Completed
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                if (IsCompleted)
                {
                    value(Result);
                    return;
                }

                _completed += value;
            }
            remove
            {
                _completed -= value;
            }
        }

        public static MxUiOpenOperation CompletedWith(MxUiOpenResult result)
        {
            return new MxUiOpenOperation(result);
        }

        public bool Complete(MxUiOpenResult result)
        {
            if (IsCompleted)
            {
                return false;
            }

            Result = result;
            Status = result.Success ? MxUiOpenOperationStatus.Succeeded : MxUiOpenOperationStatus.Failed;
            Action<MxUiOpenResult> completed = _completed;
            _completed = null;
            if (completed != null)
            {
                completed(Result);
            }

            return true;
        }

        public bool Cancel(string message = "")
        {
            if (IsCompleted)
            {
                return false;
            }

            Result = MxUiOpenResult.Fail(MxUiOpenErrorCode.OperationCancelled, message);
            Status = MxUiOpenOperationStatus.Cancelled;
            Action<MxUiOpenResult> completed = _completed;
            _completed = null;
            if (completed != null)
            {
                completed(Result);
            }

            return true;
        }
    }
}
