using System;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestCharacterActionTimelineEvent
    {
        public CharacterTestCharacterActionTimelineEvent(
            long frame,
            int entityId,
            CharacterActionRunnerEventKind kind,
            long instanceId,
            CharacterActionId actionId,
            RuntimeTraceIdentity traceId,
            string diagnosticCode)
        {
            Frame = frame;
            EntityId = entityId;
            Kind = kind;
            InstanceId = instanceId;
            ActionId = actionId;
            TraceId = traceId;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public long Frame { get; }
        public int EntityId { get; }
        public CharacterActionRunnerEventKind Kind { get; }
        public long InstanceId { get; }
        public CharacterActionId ActionId { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public string DiagnosticCode { get; }
    }

    internal sealed class CharacterTestCharacterActionTimelineEventBuffer
    {
        private readonly CharacterTestCharacterActionTimelineEvent[] _events;
        private int _start;
        private int _count;

        public CharacterTestCharacterActionTimelineEventBuffer(int capacity)
        {
            _events = new CharacterTestCharacterActionTimelineEvent[Math.Max(1, capacity)];
        }

        public int Count => _count;

        public CharacterTestCharacterActionTimelineEvent this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _events[(_start + index) % _events.Length];
            }
        }

        public void Clear()
        {
            Array.Clear(_events, 0, _events.Length);
            _start = 0;
            _count = 0;
        }

        public void Add(in CharacterTestCharacterActionTimelineEvent evt)
        {
            if (_count < _events.Length)
            {
                _events[(_start + _count) % _events.Length] = evt;
                _count++;
                return;
            }

            _events[_start] = evt;
            _start = (_start + 1) % _events.Length;
        }
    }
}
