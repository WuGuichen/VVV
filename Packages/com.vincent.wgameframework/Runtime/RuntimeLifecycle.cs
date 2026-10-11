namespace MxFramework.Runtime
{
    public enum RuntimeLifecycleState
    {
        /// <summary>The host accepts module registration and initialization.</summary>
        Created = 0,

        /// <summary>All registered modules completed initialization.</summary>
        Initialized = 1,

        /// <summary>All registered modules completed startup and the host accepts ticks.</summary>
        Started = 2,

        /// <summary>The host completed an explicit stop without a lifecycle cleanup error.</summary>
        Stopped = 3,

        /// <summary>The host completed final disposal.</summary>
        Disposed = 4,

        /// <summary>A lifecycle callback failed; the host is terminal and rejects reuse.</summary>
        Faulted = 5
    }
}
