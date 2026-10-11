using MxFramework.Runtime;
using System;
using System.Collections.Generic;
using MxFramework.Events;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Applies queued scatter pressure requests and publishes typed pressure events.
    /// </summary>
    public sealed class GameplayScatterPressureSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.scatter-pressure";

        private readonly List<GameplayScatterPressureRequest> _requests =
            new List<GameplayScatterPressureRequest>();

        private readonly List<GameplayCohesionLimitRequest> _cohesionLimitRequests =
            new List<GameplayCohesionLimitRequest>();

        private readonly List<GameplayComponentSnapshot<GameplayScatterPressureComponent>> _snapshot =
            new List<GameplayComponentSnapshot<GameplayScatterPressureComponent>>();

        private readonly EventBus<ScatterPressureChangedEvent> _pressureChanged =
            new EventBus<ScatterPressureChangedEvent>();

        private readonly EventBus<ScatterImpactResolvedEvent> _scatterImpactResolved =
            new EventBus<ScatterImpactResolvedEvent>();

        private readonly EventBus<PressureBandChangedEvent> _pressureBandChanged =
            new EventBus<PressureBandChangedEvent>();

        private readonly EventBus<CohesionBreakEvent> _cohesionBreak =
            new EventBus<CohesionBreakEvent>();

        public GameplayScatterPressureSystem(
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
        public int PendingCohesionLimitRequestCount => _cohesionLimitRequests.Count;
        public IEventBus<ScatterPressureChangedEvent> PressureChangedEvents => _pressureChanged;
        public IEventBus<ScatterImpactResolvedEvent> ScatterImpactResolvedEvents => _scatterImpactResolved;
        public IEventBus<PressureBandChangedEvent> BandChangedEvents => _pressureBandChanged;
        public IEventBus<CohesionBreakEvent> CohesionBreakEvents => _cohesionBreak;
        public IEventBus<ScatterPressureChangedEvent> PressureChanged => _pressureChanged;
        public IEventBus<ScatterImpactResolvedEvent> ScatterImpactResolved => _scatterImpactResolved;
        public IEventBus<PressureBandChangedEvent> PressureBandChanged => _pressureBandChanged;
        public IEventBus<CohesionBreakEvent> CohesionBreak => _cohesionBreak;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        public void Enqueue(in GameplayScatterPressureRequest request)
        {
            _requests.Add(request);
        }

        public void Enqueue(in GameplayCohesionLimitRequest request)
        {
            _cohesionLimitRequests.Add(request);
        }

        public void ClearRequests()
        {
            _requests.Clear();
            _cohesionLimitRequests.Clear();
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            try
            {
                GameplayComponentWorld componentWorld = context.ComponentWorld;
                if (componentWorld == null ||
                    !componentWorld.TryGetStore(out GameplayComponentStore<GameplayScatterPressureComponent> store))
                {
                    return;
                }

                for (int i = 0; i < _cohesionLimitRequests.Count; i++)
                    ApplyCohesionLimitRequest(context, componentWorld, store, _cohesionLimitRequests[i]);

                for (int i = 0; i < _requests.Count; i++)
                    ApplyRequest(context, componentWorld, store, _requests[i]);

                Recover(context, store);
            }
            finally
            {
                _requests.Clear();
                _cohesionLimitRequests.Clear();
                _snapshot.Clear();
            }
        }

        private void ApplyCohesionLimitRequest(
            GameplaySystemContext context,
            GameplayComponentWorld componentWorld,
            GameplayComponentStore<GameplayScatterPressureComponent> store,
            GameplayCohesionLimitRequest request)
        {
            if (!componentWorld.IsAlive(request.EntityId))
                return;
            if (!store.TryGet(request.EntityId, out GameplayScatterPressureComponent current))
                return;
            if (!current.HasValidState())
                return;

            GameplayScatterPressureComponent updated = current.SetCurrentCohesionLimit(request.CohesionLimit, context.Frame.Value);
            if (updated.Equals(current))
                return;

            store.Set(request.EntityId, updated);
            GameplayScatterPressureReason reason = request.Reason.IsNone
                ? GameplayScatterPressureEvents.UpdateCohesionLimitReason
                : request.Reason;
            PublishTransitions(context, request.EntityId, current, updated, 0, request.SourceId, reason, request.TraceId);
        }

        private void ApplyRequest(
            GameplaySystemContext context,
            GameplayComponentWorld componentWorld,
            GameplayComponentStore<GameplayScatterPressureComponent> store,
            GameplayScatterPressureRequest request)
        {
            if (!componentWorld.IsAlive(request.EntityId))
                return;
            if (!store.TryGet(request.EntityId, out GameplayScatterPressureComponent current))
                return;
            if (!current.HasValidState())
                return;

            GameplayScatterPressureComponent updated = current.ApplyDelta(request.Delta, context.Frame.Value);
            if (updated.Equals(current))
                return;

            store.Set(request.EntityId, updated);
            GameplayScatterPressureReason reason = request.Reason.IsNone
                ? GameplayScatterPressureEvents.ApplyPressureReason
                : request.Reason;
            PublishPressureChanged(context, request.EntityId, current, updated, request.Delta, request.SourceId, reason, request.TraceId);
            if (request.Delta > 0)
                PublishScatterImpactResolved(context, request.EntityId, current, updated, request.Delta, request.SourceId, reason, request.TraceId);
            PublishTransitions(context, request.EntityId, current, updated, request.Delta, request.SourceId, reason, request.TraceId);
        }

        private void Recover(
            GameplaySystemContext context,
            GameplayComponentStore<GameplayScatterPressureComponent> store)
        {
            _snapshot.Clear();
            store.CopyTo(_snapshot);
            for (int i = 0; i < _snapshot.Count; i++)
            {
                GameplayEntityId entityId = _snapshot[i].EntityId;
                GameplayScatterPressureComponent current = _snapshot[i].Component;
                if (ShouldRecoverBrokenFully(context.Frame.Value, current))
                {
                    GameplayScatterPressureComponent recovered = current.RecoverBrokenFully();
                    if (recovered.Equals(current))
                        continue;

                    store.Set(entityId, recovered);
                    int recoveredDelta = -current.CurrentScatterPressure;
                    PublishPressureChanged(context, entityId, current, recovered, recoveredDelta, 0, GameplayScatterPressureEvents.RecoveryReason, default);
                    PublishTransitions(context, entityId, current, recovered, recoveredDelta, 0, GameplayScatterPressureEvents.RecoveryReason, default);
                    continue;
                }

                if (!ShouldRecover(context.Frame.Value, current))
                    continue;

                GameplayScatterPressureComponent updated = current.Recover(current.RecoveryRate);
                if (updated.Equals(current))
                    continue;

                store.Set(entityId, updated);
                PublishPressureChanged(context, entityId, current, updated, -current.RecoveryRate, 0, GameplayScatterPressureEvents.RecoveryReason, default);
                PublishTransitions(context, entityId, current, updated, -current.RecoveryRate, 0, GameplayScatterPressureEvents.RecoveryReason, default);
            }
        }

        private void PublishPressureChanged(
            GameplaySystemContext context,
            GameplayEntityId entityId,
            GameplayScatterPressureComponent current,
            GameplayScatterPressureComponent updated,
            int delta,
            int sourceId,
            GameplayScatterPressureReason reason,
            RuntimeTraceIdentity traceId)
        {
            _pressureChanged.Publish(new ScatterPressureChangedEvent(
                context.Frame,
                entityId,
                current.CurrentScatterPressure,
                updated.CurrentScatterPressure,
                updated.CurrentCohesionLimit,
                current.CurrentBand,
                updated.CurrentBand,
                delta,
                sourceId,
                reason,
                traceId));
        }

        private void PublishScatterImpactResolved(
            GameplaySystemContext context,
            GameplayEntityId entityId,
            GameplayScatterPressureComponent current,
            GameplayScatterPressureComponent updated,
            int scatterImpact,
            int sourceId,
            GameplayScatterPressureReason reason,
            RuntimeTraceIdentity traceId)
        {
            _scatterImpactResolved.Publish(new ScatterImpactResolvedEvent(
                context.Frame,
                entityId,
                scatterImpact,
                current.CurrentScatterPressure,
                updated.CurrentScatterPressure,
                updated.CurrentCohesionLimit,
                current.CurrentBand,
                updated.CurrentBand,
                sourceId,
                reason,
                traceId));
        }

        private void PublishTransitions(
            GameplaySystemContext context,
            GameplayEntityId entityId,
            GameplayScatterPressureComponent current,
            GameplayScatterPressureComponent updated,
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
                current.CurrentScatterPressure,
                updated.CurrentScatterPressure,
                delta,
                sourceId,
                reason,
                traceId,
                updated.CurrentCohesionLimit);
            _pressureBandChanged.Publish(bandChanged);

            if (!current.IsBroken && updated.IsBroken)
            {
                var cohesionBreak = new CohesionBreakEvent(
                    context.Frame,
                    entityId,
                    current.CurrentBand,
                    current.CurrentScatterPressure,
                    updated.CurrentScatterPressure,
                    updated.CurrentCohesionLimit,
                    delta,
                    sourceId,
                    reason,
                    traceId,
                    updated.CurrentCohesionLimit);
                _cohesionBreak.Publish(cohesionBreak);
            }
        }

        private static bool ShouldRecover(long frame, GameplayScatterPressureComponent component)
        {
            if (!component.HasValidState() || component.CurrentScatterPressure <= 0 || component.RecoveryRate <= 0)
                return false;
            if (component.IsBroken && component.BrokenRecoveryDelayFrames > 0)
                return false;

            long elapsedFrames = frame - component.LastPressureFrame;
            return elapsedFrames > component.RecoveryDelayFrames;
        }

        private static bool ShouldRecoverBrokenFully(long frame, GameplayScatterPressureComponent component)
        {
            if (!component.HasValidState()
                || !component.IsBroken
                || component.BrokenRecoveryDelayFrames <= 0
                || component.LastCohesionBreakFrame < 0L)
            {
                return false;
            }

            long elapsedFrames = frame - component.LastCohesionBreakFrame;
            return elapsedFrames >= component.BrokenRecoveryDelayFrames;
        }
    }
}
