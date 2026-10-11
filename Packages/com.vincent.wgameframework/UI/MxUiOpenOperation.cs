using System;

namespace MxFramework.UI
{
    public interface IMxUiOpenOperationDriver
    {
        /// <remarks>NoAlloc; advances an already-created low-frequency UI open operation.</remarks>
        void Pump(MxUiOpenOperation operation);

        /// <remarks>NoAlloc; releases any pending work owned by the driver.</remarks>
        void Cancel();
    }

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
        private IMxUiOpenOperationDriver _driver;
        private MxUiOpenOperationStatus _status;
        private MxUiOpenResult _result;
        private bool _isPumping;

        public MxUiOpenOperation()
        {
            _status = MxUiOpenOperationStatus.Pending;
            _result = MxUiOpenResult.Fail(MxUiOpenErrorCode.ResourcesPending, "UI open operation is pending.");
        }

        public MxUiOpenOperation(IMxUiOpenOperationDriver driver)
            : this()
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        private MxUiOpenOperation(MxUiOpenResult result)
        {
            Complete(result);
        }

        public MxUiOpenOperationStatus Status
        {
            get
            {
                Pump();
                return _status;
            }
        }

        public MxUiOpenResult Result
        {
            get
            {
                Pump();
                return _result;
            }
        }

        public bool IsCompleted
        {
            get
            {
                Pump();
                return _status != MxUiOpenOperationStatus.Pending;
            }
        }

        public event Action<MxUiOpenResult> Completed
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                Pump();
                if (_status != MxUiOpenOperationStatus.Pending)
                {
                    value(_result);
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
            if (_status != MxUiOpenOperationStatus.Pending)
            {
                return false;
            }

            _driver = null;
            _result = NormalizeResult(result);
            _status = _result.Success ? MxUiOpenOperationStatus.Succeeded : MxUiOpenOperationStatus.Failed;
            Action<MxUiOpenResult> completed = _completed;
            _completed = null;
            if (completed != null)
            {
                completed(_result);
            }

            return true;
        }

        private static MxUiOpenResult NormalizeResult(MxUiOpenResult result)
        {
            if (result.Success)
            {
                return result;
            }

            if (result.ErrorCode != MxUiOpenErrorCode.None)
            {
                return result;
            }

            return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, "UI open operation completed with an invalid failure result.");
        }

        public bool Cancel(string message = "")
        {
            if (_status != MxUiOpenOperationStatus.Pending)
            {
                return false;
            }

            IMxUiOpenOperationDriver driver = _driver;
            _driver = null;
            try
            {
                driver?.Cancel();
            }
            finally
            {
                _result = MxUiOpenResult.Fail(MxUiOpenErrorCode.OperationCancelled, message);
                _status = MxUiOpenOperationStatus.Cancelled;
                Action<MxUiOpenResult> completed = _completed;
                _completed = null;
                if (completed != null)
                    completed(_result);
            }

            return true;
        }

        /// <remarks>NoAlloc; polls the underlying asynchronous driver once.</remarks>
        public void Pump()
        {
            if (_status != MxUiOpenOperationStatus.Pending || _driver == null || _isPumping)
                return;

            _isPumping = true;
            try
            {
                _driver.Pump(this);
            }
            finally
            {
                _isPumping = false;
            }
        }
    }
}
