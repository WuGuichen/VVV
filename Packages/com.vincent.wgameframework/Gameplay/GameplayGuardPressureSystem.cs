using MxFramework.Runtime;
using System;
using System.Collections.Generic;
using MxFramework.Events;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Applies queued guard pressure requests and publishes typed guard events.
    /// </summary>
    public sealed class GameplayGuardPressureSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.guard-pressure";
        public static readonly GameplayScatterPressureReason ApplyPressureReason = new GameplayScatterPressureReason(5);
        public static readonly GameplayScatterPressureReason RecoveryReason = new GameplayScatterPressureReason(6);
        public static readonly GameplayScatterPressureReason BreakReason = new GameplayScatterPressureReason(7);

        private readonly List<GameplayGuardPressureRequest> _requests =
            new List<GameplayGuardPressureRequest>();

        private readonly List<GameplayComponentSnapshot<GameplayGuardPressureComponent>> _snapshot =
            new List<GameplayComponentSnapshot<GameplayGuardPressureComponent>>();

        private readonly EventBus<PressureBandChangedEvent> _pressureBandChanged =
            new EventBus<PressureBandChangedEvent>();

        private readonly EventBus<GuardBreakEvent> _guardBreak =
            new EventBus<GuardBreakEvent>();

        private readonly GameplayStatusId _guardBrokenStatusId;

        public GameplayGuardPressureSystem(
            GameplayStatusId guardBrokenStatusId = default,
            string systemId = DefaultSystemId,
            int priority = 70)
        {
            _guardBrokenStatusId = guardBrokenStatusId;
            SystemId = systemId ?? string.Empty;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase => GameplaySystemPhase.Resolution;
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;
        public int PendingRequestCount => _requests.Count;
        public IEventBus<PressureBandChangedEvent> BandChangedEvents => _pressureBandChanged;
        public IEventBus<GuardBreakEvent> GuardBreakEvents => _guardBreak;
        public IEventBus<PressureBandChangedEvent> PressureBandChanged => _pressureBandChanged;
        public IEventBus<GuardBreakEvent> GuardBreak => _guardBreak;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        public void Enqueue(in GameplayGuardPressureRequest request)
        {
            _requests.Add(request);
        }

        public void ClearRequests()
        {
            _requests.Clear();
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            try
            {
                if (!IsEnabled)
                    return;

                GameplayComponentWorld componentWorld = context.ComponentWorld;
                if (componentWorld == null ||
                    !componentWorld.TryGetStore(out GameplayComponentStore<GameplayGuardPressureComponent> store))
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
            GameplayComponentStore<GameplayGuardPressureComponent> store,
            GameplayGuardPressureRequest request)
        {
            if (!componentWorld.IsAlive(request.EntityId))
                return;
            if (!store.TryGet(request.EntityId, out GameplayGuardPressureComponent current))
                return;
            if (!current.HasValidState())
                return;

            GameplayGuardPressureComponent updated = current.ApplyDelta(request.Delta, request.IsBlocking, context.Frame.Value);
            if (updated.Equals(current))
                return;

            store.Set(request.EntityId, updated);
            GameplayScatterPressureReason reason = request.Reason.IsNone
                ? ApplyPressureReason
                : request.Reason;
            PublishTransitions(context, componentWorld, request.EntityId, current, updated, request.Delta, request.SourceId, reason, request.TraceId);
        }

        private void Recover(
            GameplaySystemContext context,
            GameplayComponentStore<GameplayGuardPressureComponent> store)
        {
            _snapshot.Clear();
            store.CopyTo(_snapshot);
            for (int i = 0; i < _snapshot.Count; i++)
            {
                GameplayEntityId entityId = _snapshot[i].EntityId;
                GameplayGuardPressureComponent current = _snapshot[i].Component;
                if (!ShouldRecover(context.Frame.Value, current))
                    continue;

                GameplayGuardPressureComponent updated = current.Recover(current.RecoveryRate);
                if (updated.Equals(current))
                    continue;

                store.Set(entityId, updated);
                PublishTransitions(context, context.ComponentWorld, entityId, current, updated, -current.RecoveryRate, 0, RecoveryReason, default);
            }
        }

        private void PublishTransitions(
            GameplaySystemContext context,
            GameplayComponentWorld componentWorld,
            GameplayEntityId entityId,
            GameplayGuardPressureComponent current,
            GameplayGuardPressureComponent updated,
            int delta,
            int sourceId,
            GameplayScatterPressureReason reason,
            RuntimeTraceIdentity traceId)
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
                ApplyGuardBrokenStatus(componentWorld, entityId);
                var guardBreak = new GuardBreakEvent(
                    context.Frame,
                    entityId,
                    current.CurrentBand,
                    current.CurrentPressure,
                    updated.CurrentPressure,
                    updated.MaxPressure,
                    delta,
                    sourceId,
                    reason.IsNone ? BreakReason : reason,
                    traceId);
                _guardBreak.Publish(guardBreak);
            }
        }

        private void ApplyGuardBrokenStatus(GameplayComponentWorld componentWorld, GameplayEntityId entityId)
        {
            if (!_guardBrokenStatusId.IsValid || componentWorld == null || !componentWorld.IsAlive(entityId))
                return;

            GameplayComponentStore<GameplayStatusComponent> statusStore =
                componentWorld.GetOrCreateStore<GameplayStatusComponent>();
            if (!statusStore.TryGet(entityId, out GameplayStatusComponent statuses))
            {
                statusStore.Set(entityId, new GameplayStatusComponent(_guardBrokenStatusId));
                return;
            }

            if (statuses.Contains(_guardBrokenStatusId))
                return;

            GameplayStatusId[] existing = statuses.ToArray();
            var next = new GameplayStatusId[existing.Length + 1];
            Array.Copy(existing, next, existing.Length);
            next[next.Length - 1] = _guardBrokenStatusId;
            statusStore.Set(entityId, new GameplayStatusComponent(next));
        }

        private static bool ShouldRecover(long frame, GameplayGuardPressureComponent component)
        {
            if (!component.HasValidState() || component.CurrentPressure <= 0 || component.RecoveryRate <= 0)
                return false;

            long elapsedFrames = frame - component.LastPressureFrame;
            return elapsedFrames > component.RecoveryDelayFrames;
        }
    }
}
