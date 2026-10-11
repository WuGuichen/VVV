using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public interface IGameplayRuntimeEventSink
    {
        void OnGameplayRuntimeEvents(RuntimeFrame frame, IReadOnlyList<GameplayRuntimeEvent> events);
    }

    public sealed class GameplayRuntimeEventFanout
    {
        private readonly GameplayRuntimeModule _gameplay;
        private readonly List<IGameplayRuntimeEventSink> _sinks = new List<IGameplayRuntimeEventSink>();
        private readonly List<GameplayRuntimeEvent> _events = new List<GameplayRuntimeEvent>();

        public GameplayRuntimeEventFanout(GameplayRuntimeModule gameplay)
        {
            _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
        }

        public int SinkCount => _sinks.Count;

        public bool AddSink(IGameplayRuntimeEventSink sink)
        {
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));

            if (_sinks.Contains(sink))
                return false;

            _sinks.Add(sink);
            return true;
        }

        public bool RemoveSink(IGameplayRuntimeEventSink sink)
        {
            if (sink == null)
                return false;

            return _sinks.Remove(sink);
        }

        public int DrainFrame(RuntimeFrame frame)
        {
            _events.Clear();
            int drained = _gameplay.DrainEvents(frame, _events);
            if (_events.Count == 0)
                return drained;

            for (int i = 0; i < _sinks.Count; i++)
                _sinks[i].OnGameplayRuntimeEvents(frame, _events);

            _events.Clear();
            return drained;
        }
    }
}
