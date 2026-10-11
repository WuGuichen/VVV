using System;
using MxFramework.Story;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Subscribe before Story bootstrap. Copy callbacks now, deliver at the composition boundary.</summary>
    /// <remarks>Single-threaded and cross-frame safe. Never calls Director, Spawn, or the world from an event callback.</remarks>
    internal sealed class TacticalStoryBridge : IDisposable
    {
        private readonly TacticalStageFlowCoordinator _coordinator;
        private readonly TacticalStoryStepToken[] _pending;
        private readonly IDisposable _subscription;
        private int _head;
        private bool _overflow, _draining, _disposed;
        public int PendingCount { get; private set; }

        public TacticalStoryBridge(IStoryDirector director, TacticalStageFlowCoordinator coordinator, int capacity = 16)
        {
            if (director == null) throw new ArgumentNullException(nameof(director));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _pending = new TacticalStoryStepToken[capacity];
            _subscription = director.SubscribeEvents(OnStoryEvent);
        }

        private void OnStoryEvent(StoryEvent evt)
        {
            if (_disposed || evt.Kind != StoryEventKind.StepStarted) return;
            var token = new TacticalStoryStepToken(_coordinator.StageRunId, evt.GraphId,
                evt.BeatInstanceId, evt.StepId, evt.AuxId);
            if (!_coordinator.RecognizesStoryStep(in token)) return;
            for (int i = 0; i < PendingCount; i++)
            {
                var queued = _pending[(_head + i) % _pending.Length];
                if (queued.GraphId == token.GraphId && queued.BeatInstanceId == token.BeatInstanceId &&
                    queued.StepId == token.StepId && queued.ActionId == token.ActionId) return;
            }
            if (PendingCount == _pending.Length) { _overflow = true; return; }
            _pending[(_head + PendingCount) % _pending.Length] = token;
            PendingCount++;
        }

        /// <summary>NoAlloc. Call outside Director dispatch, before forwarding Spawn receipts and full-tick samples.</summary>
        public int Drain()
        {
            if (_disposed || _draining) return 0;
            if (_overflow)
            {
                _coordinator.ReportFault(TacticalStageRejection.StoryBufferOverflow);
                return 0;
            }
            _draining = true;
            try
            {
                int count = PendingCount;
                for (int i = 0; i < count; i++)
                {
                    var token = _pending[_head];
                    _pending[_head] = default;
                    _head = (_head + 1) % _pending.Length;
                    PendingCount--;
                    _coordinator.OnStoryStepStarted(in token);
                }
                return count;
            }
            finally { _draining = false; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription.Dispose();
            Array.Clear(_pending, 0, _pending.Length);
            PendingCount = 0;
        }
    }
}
