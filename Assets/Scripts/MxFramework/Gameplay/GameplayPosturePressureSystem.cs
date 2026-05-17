using System;
using System.Collections.Generic;
using MxFramework.Events;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Applies queued posture pressure requests and publishes typed pressure events.
    /// </summary>
    public sealed class GameplayPosturePressureSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.posture-pressure";

        private readonly List<GameplayPosturePressureRequest> _requests =
            new List<GameplayPosturePressureRequest>();

        private readonly List<GameplayComponentSnapshot<GameplayPosturePressureComponent>> _snapshot =
            new List<GameplayComponentSnapshot<GameplayPosturePressureComponent>>();

        private readonly EventBus<PressureBandChangedEvent> _pressureBandChanged =
            new EventBus<PressureBandChangedEvent>();

        private readonly EventBus<PostureBreakEvent> _postureBreak =
            new EventBus<PostureBreakEvent>();

        public GameplayPosturePressureSystem(
            string systemId = DefaultSystemId,
            int priority = 70)
        {
            SystemId = systemId ?? string.Empty;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase => GameplaySystemPhase.Resolution;
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;
        public int PendingRequestCount => _requests.Count;
        public IEventBus<PressureBandChangedEvent> BandChangedEvents => _pressureBandChanged;
        public IEventBus<PostureBreakEvent> PostureBreakEvents => _postureBreak;
        public IEventBus<PressureBandChangedEvent> PressureBandChanged => _pressureBandChanged;
        public IEventBus<PostureBreakEvent> PostureBreak => _postureBreak;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        public void Enqueue(in GameplayPosturePressureRequest request)
        {
            _requests.Add(request);
        }

        public void ClearRequests()
        {
            _requests.Clear();
        }

        public void Tick(GameplaySystemContext context)
        {
            try
            {
                GameplayComponentWorld componentWorld = context.ComponentWorld;
                if (componentWorld == null ||
                    !componentWorld.TryGetStore(out GameplayComponentStore<GameplayPosturePressureComponent> store))
                {
                    return;
                }

                for (int i = 0; i < _requests.Count; i++)
                    ApplyRequest(context, componentWorld, store, _requests[i]);

                Recover(context, store);
            }
            finally
            {
                _requests.Clear();
                _snapshot.Clear();
            }
        }

        private void ApplyRequest(
            GameplaySystemContext context,
            GameplayComponentWorld componentWorld,
            GameplayComponentStore<GameplayPosturePressureComponent> store,
            GameplayPosturePressureRequest request)
        {
            if (!componentWorld.IsAlive(request.EntityId))
                return;
            if (!store.TryGet(request.EntityId, out GameplayPosturePressureComponent current))
                return;
            if (!current.HasValidState())
                return;

            GameplayPosturePressureComponent updated = current.ApplyDelta(request.Delta, context.Frame.Value);
            if (updated.Equals(current))
                return;

            store.Set(request.EntityId, updated);
            string reason = string.IsNullOrEmpty(request.Reason)
                ? GameplayPosturePressureEvents.ApplyPressureReason
                : request.Reason;
            PublishTransitions(context, request.EntityId, current, updated, request.Delta, request.SourceId, reason, request.TraceId);
        }

        private void Recover(
            GameplaySystemContext context,
            GameplayComponentStore<GameplayPosturePressureComponent> store)
        {
            _snapshot.Clear();
            store.CopyTo(_snapshot);
            for (int i = 0; i < _snapshot.Count; i++)
            {
                GameplayEntityId entityId = _snapshot[i].EntityId;
                GameplayPosturePressureComponent current = _snapshot[i].Component;
                if (!ShouldRecover(context.Frame.Value, current))
                    continue;

                GameplayPosturePressureComponent updated = current.Recover(current.RecoveryRate);
                if (updated.Equals(current))
                    continue;

                store.Set(entityId, updated);
                PublishTransitions(context, entityId, current, updated, -current.RecoveryRate, 0, GameplayPosturePressureEvents.RecoveryReason, string.Empty);
            }
        }

        private void PublishTransitions(
            GameplaySystemContext context,
            GameplayEntityId entityId,
            GameplayPosturePressureComponent current,
            GameplayPosturePressureComponent updated,
            int delta,
            int sourceId,
            string reason,
            string traceId)
        {
            if (current.CurrentBand == updated.CurrentBand)
                return;

            var bandChanged = new PressureBandChangedEvent(
                context.Frame,
                entityId,
                current.CurrentBand,
                updated.CurrentBand,
                current.CurrentPressure,
                updated.CurrentPressure,
                delta,
                sourceId,
                reason,
                traceId);
            _pressureBandChanged.Publish(bandChanged);

            if (!current.IsBroken && updated.IsBroken)
            {
                var postureBreak = new PostureBreakEvent(
                    context.Frame,
                    entityId,
                    current.CurrentBand,
                    current.CurrentPressure,
                    updated.CurrentPressure,
                    updated.MaxPressure,
                    delta,
                    sourceId,
                    reason,
                    traceId);
                _postureBreak.Publish(postureBreak);
            }
        }

        private static bool ShouldRecover(long frame, GameplayPosturePressureComponent component)
        {
            if (!component.HasValidState() || component.CurrentPressure <= 0 || component.RecoveryRate <= 0)
                return false;

            long elapsedFrames = frame - component.LastPressureFrame;
            return elapsedFrames > component.RecoveryDelayFrames;
        }
    }
}
