using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public sealed class GameplaySystemPipeline
    {
        private readonly List<Entry> _systems = new List<Entry>();
        private long _nextSequence;

        public int Count => _systems.Count;

        public void Add(IGameplaySystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));
            if (string.IsNullOrWhiteSpace(system.SystemId))
                throw new ArgumentException("Gameplay system id cannot be empty.", nameof(system));
            if (Contains(system.SystemId))
                throw new InvalidOperationException($"Gameplay system '{system.SystemId}' is already registered.");

            _systems.Add(new Entry(system, _nextSequence));
            _nextSequence++;
            _systems.Sort(CompareEntries);
        }

        public bool Remove(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId))
                return false;

            for (int i = 0; i < _systems.Count; i++)
            {
                if (string.Equals(_systems[i].System.SystemId, systemId, StringComparison.Ordinal))
                {
                    _systems.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public bool Contains(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId))
                return false;

            for (int i = 0; i < _systems.Count; i++)
            {
                if (string.Equals(_systems[i].System.SystemId, systemId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public void Tick(GameplaySystemContext context)
        {
            for (int i = 0; i < _systems.Count; i++)
            {
                IGameplaySystem system = _systems[i].System;
                if (!system.IsEnabled)
                    continue;

                try
                {
                    system.Tick(context);
                }
                catch (Exception ex)
                {
                    throw new GameplaySystemPipelineException(system.SystemId, system.Phase, ex);
                }
            }
        }

        public GameplaySystemPipelineSnapshot CreateSnapshot()
        {
            int enabledCount = 0;
            for (int i = 0; i < _systems.Count; i++)
            {
                if (_systems[i].System.IsEnabled)
                    enabledCount++;
            }

            return new GameplaySystemPipelineSnapshot(_systems.Count, enabledCount);
        }

        public void Clear()
        {
            _systems.Clear();
            _nextSequence = 0L;
        }

        private static int CompareEntries(Entry left, Entry right)
        {
            int phaseComparison = left.System.Phase.CompareTo(right.System.Phase);
            if (phaseComparison != 0)
                return phaseComparison;

            int priorityComparison = left.System.Priority.CompareTo(right.System.Priority);
            if (priorityComparison != 0)
                return priorityComparison;

            return left.Sequence.CompareTo(right.Sequence);
        }

        private readonly struct Entry
        {
            public Entry(IGameplaySystem system, long sequence)
            {
                System = system;
                Sequence = sequence;
            }

            public IGameplaySystem System { get; }
            public long Sequence { get; }
        }
    }
}
