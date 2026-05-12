using System;

namespace MxFramework.Events
{
    /// <summary>
    /// Type-safe synchronous event bus.
    /// </summary>
    /// <remarks>
    /// Subscribe is LowFreqAlloc. Publish is NoAlloc after the handler table has reached steady state.
    /// </remarks>
    public interface IEventBus<T> where T : struct
    {
        IDisposable Subscribe(Action<T> handler);
        bool Unsubscribe(Action<T> handler);
        void Publish(in T args);
    }
}
