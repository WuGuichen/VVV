using MxFramework.Events;

namespace MxFramework.Modifiers
{
    public interface ICounterStore
    {
        IEventBus<CounterChangedEvent> OnCounterChanged { get; }

        int GetCounter(int counterId);
        bool TryGetCounter(int counterId, out int value);
        void SetCounter(int counterId, int value, object source = null);
        void AddCounter(int counterId, int delta, object source = null);
        void ResetCounter(int counterId, object source = null);
        void Clear();
    }
}
