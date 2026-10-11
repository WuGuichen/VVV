using System.Collections.Generic;
using MxFramework.Events;

namespace MxFramework.Modifiers
{
    public sealed class CounterStore : ICounterStore
    {
        private readonly Dictionary<int, int> _values;
        private readonly EventBus<CounterChangedEvent> _changed;

        public CounterStore(int initialCapacity = 8)
        {
            if (initialCapacity <= 0)
                initialCapacity = 8;

            _values = new Dictionary<int, int>(initialCapacity);
            _changed = new EventBus<CounterChangedEvent>();
        }

        public IEventBus<CounterChangedEvent> OnCounterChanged => _changed;

        public int GetCounter(int counterId)
        {
            return _values.TryGetValue(counterId, out int value) ? value : 0;
        }

        public bool TryGetCounter(int counterId, out int value)
        {
            return _values.TryGetValue(counterId, out value);
        }

        public void SetCounter(int counterId, int value, object source = null)
        {
            int oldValue = GetCounter(counterId);
            if (oldValue == value)
                return;

            _values[counterId] = value;
            _changed.Publish(new CounterChangedEvent(counterId, oldValue, value, source));
        }

        public void AddCounter(int counterId, int delta, object source = null)
        {
            if (delta == 0)
                return;

            SetCounter(counterId, GetCounter(counterId) + delta, source);
        }

        public void ResetCounter(int counterId, object source = null)
        {
            SetCounter(counterId, 0, source);
        }

        public void Clear()
        {
            _values.Clear();
        }
    }
}
