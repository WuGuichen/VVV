namespace MxFramework.UI
{
    public enum MxUiLifecycleState
    {
        Created = 0,
        Visible = 10,
        Hidden = 20,
        Disposed = 30
    }

    public sealed class MxUiLifecycle
    {
        public MxUiLifecycle()
        {
            State = MxUiLifecycleState.Created;
        }

        public MxUiLifecycleState State { get; private set; }
        public bool IsVisible => State == MxUiLifecycleState.Visible;
        public bool IsDisposed => State == MxUiLifecycleState.Disposed;

        public bool Show()
        {
            if (State == MxUiLifecycleState.Disposed || State == MxUiLifecycleState.Visible)
            {
                return false;
            }

            State = MxUiLifecycleState.Visible;
            return true;
        }

        public bool Hide()
        {
            if (State == MxUiLifecycleState.Disposed || State == MxUiLifecycleState.Hidden)
            {
                return false;
            }

            State = MxUiLifecycleState.Hidden;
            return true;
        }

        public bool Dispose()
        {
            if (State == MxUiLifecycleState.Disposed)
            {
                return false;
            }

            State = MxUiLifecycleState.Disposed;
            return true;
        }
    }
}
