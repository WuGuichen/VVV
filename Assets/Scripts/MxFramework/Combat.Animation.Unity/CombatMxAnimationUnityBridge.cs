using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Animation.Unity
{
    public enum CombatMxAnimationStartRequestKind
    {
        Play,
        CrossFade
    }

    public enum CombatMxAnimationEndRequestKind
    {
        Stop,
        CrossFade
    }

    public enum CombatMxAnimationBridgeEventKind
    {
        ActionStarted,
        ActionCanceled,
        ActionFinished,
        FramePresentationEvent
    }

    public sealed class CombatMxAnimationBridgeOptions
    {
        private readonly List<CombatMxAnimationFrameEventBinding> _frameEventBindings =
            new List<CombatMxAnimationFrameEventBinding>();

        public CombatMxAnimationStartRequestKind StartRequestKind { get; set; } = CombatMxAnimationStartRequestKind.CrossFade;

        public CombatMxAnimationEndRequestKind CanceledRequestKind { get; set; } = CombatMxAnimationEndRequestKind.Stop;

        public CombatMxAnimationEndRequestKind FinishedRequestKind { get; set; } = CombatMxAnimationEndRequestKind.Stop;

        public float StartCrossFadeDurationSeconds { get; set; } = 0.15f;

        public float StopFadeOutDurationSeconds { get; set; } = 0.1f;

        public float EndCrossFadeDurationSeconds { get; set; } = 0.15f;

        public string ActionKeyPrefix { get; set; } = "action:";

        public string FrameEventIdPrefix { get; set; } = "event:";

        public string CanceledCrossFadeBindingId { get; set; } = string.Empty;

        public string FinishedCrossFadeBindingId { get; set; } = string.Empty;

        public int MaxRecentDiagnostics { get; set; } = 32;

        public IList<CombatMxAnimationFrameEventBinding> FrameEventBindings => _frameEventBindings;

        public string BuildActionKey(int actionId)
        {
            return (ActionKeyPrefix ?? string.Empty) + actionId.ToString(CultureInfo.InvariantCulture);
        }

        public string BuildFrameEventId(int eventId)
        {
            return (FrameEventIdPrefix ?? string.Empty) + eventId.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class CombatMxAnimationFrameEventBinding
    {
        public string ActionKey { get; set; } = string.Empty;

        public int CombatEventId { get; set; } = -1;

        public int? IntPayload { get; set; }

        public int? SourceOrder { get; set; }

        public string PresentationEventId { get; set; } = string.Empty;

        public MxAnimationPresentationEvent PresentationEvent { get; set; }

        public bool Matches(string actionKey, CombatActionFrameEvent frameEvent)
        {
            if (!string.IsNullOrWhiteSpace(ActionKey) && !string.Equals(ActionKey, actionKey, StringComparison.Ordinal))
                return false;

            if (CombatEventId >= 0 && CombatEventId != frameEvent.EventId)
                return false;

            if (IntPayload.HasValue && IntPayload.Value != frameEvent.IntPayload)
                return false;

            if (SourceOrder.HasValue && SourceOrder.Value != frameEvent.SourceOrder)
                return false;

            return true;
        }
    }

    public readonly struct CombatMxAnimationPresentationEventDispatch
    {
        public CombatMxAnimationPresentationEventDispatch(
            CombatEntityId entityId,
            string targetActorId,
            int actionId,
            string actionKey,
            string bindingId,
            int actionInstanceId,
            CombatFrame worldFrame,
            int localFrame,
            CombatActionFrameEvent frameEvent,
            MxAnimationPresentationEvent presentationEvent,
            string correlationId)
        {
            EntityId = entityId;
            TargetActorId = targetActorId ?? string.Empty;
            ActionId = actionId;
            ActionKey = actionKey ?? string.Empty;
            BindingId = bindingId ?? string.Empty;
            ActionInstanceId = actionInstanceId;
            WorldFrame = worldFrame;
            LocalFrame = localFrame;
            FrameEvent = frameEvent;
            PresentationEvent = presentationEvent;
            CorrelationId = correlationId ?? string.Empty;
        }

        public CombatEntityId EntityId { get; }

        public string TargetActorId { get; }

        public int ActionId { get; }

        public string ActionKey { get; }

        public string BindingId { get; }

        public int ActionInstanceId { get; }

        public CombatFrame WorldFrame { get; }

        public int LocalFrame { get; }

        public CombatActionFrameEvent FrameEvent { get; }

        public MxAnimationPresentationEvent PresentationEvent { get; }

        public string CorrelationId { get; }
    }

    public interface ICombatMxAnimationPresentationEventSink
    {
        void Dispatch(CombatMxAnimationPresentationEventDispatch dispatch);
    }

    public sealed class NullCombatMxAnimationPresentationEventSink : ICombatMxAnimationPresentationEventSink
    {
        public static readonly NullCombatMxAnimationPresentationEventSink Instance = new NullCombatMxAnimationPresentationEventSink();

        private NullCombatMxAnimationPresentationEventSink()
        {
        }

        public void Dispatch(CombatMxAnimationPresentationEventDispatch dispatch)
        {
        }
    }

    public readonly struct CombatMxAnimationBridgeDiagnosticEntry
    {
        public CombatMxAnimationBridgeDiagnosticEntry(
            CombatMxAnimationBridgeEventKind eventKind,
            CombatEntityId entityId,
            string targetActorId,
            int actionId,
            string actionKey,
            string bindingId,
            int actionInstanceId,
            bool hasWorldFrame,
            CombatFrame worldFrame,
            bool hasLocalFrame,
            int localFrame,
            bool hasFrameEvent,
            CombatActionFrameEvent frameEvent,
            bool hasRequest,
            MxAnimationRequestKind requestKind,
            MxAnimationBackendResultCode resultCode,
            bool success,
            string presentationEventId,
            string correlationId,
            string message)
        {
            EventKind = eventKind;
            EntityId = entityId;
            TargetActorId = targetActorId ?? string.Empty;
            ActionId = actionId;
            ActionKey = actionKey ?? string.Empty;
            BindingId = bindingId ?? string.Empty;
            ActionInstanceId = actionInstanceId;
            HasWorldFrame = hasWorldFrame;
            WorldFrame = worldFrame;
            HasLocalFrame = hasLocalFrame;
            LocalFrame = localFrame;
            HasFrameEvent = hasFrameEvent;
            FrameEvent = frameEvent;
            HasRequest = hasRequest;
            RequestKind = requestKind;
            ResultCode = resultCode;
            Success = success;
            PresentationEventId = presentationEventId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CombatMxAnimationBridgeEventKind EventKind { get; }

        public CombatEntityId EntityId { get; }

        public string TargetActorId { get; }

        public int ActionId { get; }

        public string ActionKey { get; }

        public string BindingId { get; }

        public int ActionInstanceId { get; }

        public bool HasWorldFrame { get; }

        public CombatFrame WorldFrame { get; }

        public bool HasLocalFrame { get; }

        public int LocalFrame { get; }

        public bool HasFrameEvent { get; }

        public CombatActionFrameEvent FrameEvent { get; }

        public bool HasRequest { get; }

        public MxAnimationRequestKind RequestKind { get; }

        public MxAnimationBackendResultCode ResultCode { get; }

        public bool Success { get; }

        public string PresentationEventId { get; }

        public string CorrelationId { get; }

        public string Message { get; }
    }

    public sealed class CombatMxAnimationBridgeDiagnosticSnapshot
    {
        private readonly List<CombatMxAnimationBridgeDiagnosticEntry> _recentEntries;

        public CombatMxAnimationBridgeDiagnosticSnapshot(
            bool isInitialized,
            int actorCount,
            IEnumerable<CombatMxAnimationBridgeDiagnosticEntry> recentEntries)
        {
            IsInitialized = isInitialized;
            ActorCount = actorCount;
            _recentEntries = recentEntries != null
                ? new List<CombatMxAnimationBridgeDiagnosticEntry>(recentEntries)
                : new List<CombatMxAnimationBridgeDiagnosticEntry>();
        }

        public bool IsInitialized { get; }

        public int ActorCount { get; }

        public IReadOnlyList<CombatMxAnimationBridgeDiagnosticEntry> RecentEntries => _recentEntries;
    }

    public sealed class CombatMxAnimationUnityBridge : IDisposable
    {
        private readonly ICombatAnimationContext _animationContext;
        private readonly CombatMxAnimationBridgeOptions _options;
        private readonly ICombatMxAnimationPresentationEventSink _presentationEventSink;
        private readonly Dictionary<CombatEntityId, ActorRuntime> _actors =
            new Dictionary<CombatEntityId, ActorRuntime>();
        private readonly Dictionary<ActionInstanceKey, FrameCorrelation> _lastCorrelations =
            new Dictionary<ActionInstanceKey, FrameCorrelation>();
        private readonly Queue<CombatMxAnimationBridgeDiagnosticEntry> _recentDiagnostics =
            new Queue<CombatMxAnimationBridgeDiagnosticEntry>();
        private readonly List<MxAnimationPresentationEvent> _resolvedEvents =
            new List<MxAnimationPresentationEvent>();

        private CombatActionRunner _subscribedRunner;

        public CombatMxAnimationUnityBridge(
            ICombatAnimationContext animationContext,
            CombatMxAnimationBridgeOptions options = null,
            ICombatMxAnimationPresentationEventSink presentationEventSink = null)
        {
            _animationContext = animationContext ?? throw new ArgumentNullException(nameof(animationContext));
            _options = options ?? new CombatMxAnimationBridgeOptions();
            _presentationEventSink = presentationEventSink ?? NullCombatMxAnimationPresentationEventSink.Instance;
        }

        public bool IsInitialized => _subscribedRunner != null;

        public int ActorCount => _actors.Count;

        public void RegisterActor(
            CombatEntityId entityId,
            IMxAnimationBackend backend,
            MxAnimationSetDefinition animationSet,
            string targetActorId = "")
        {
            if (entityId.IsNone)
            {
                throw new ArgumentException("Combat entity id cannot be None.", nameof(entityId));
            }

            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            _actors[entityId] = new ActorRuntime(
                entityId,
                string.IsNullOrWhiteSpace(targetActorId) ? BuildDefaultActorId(entityId) : targetActorId,
                backend,
                animationSet ?? new MxAnimationSetDefinition(string.Empty, 0, default, default));
        }

        public bool UnregisterActor(CombatEntityId entityId)
        {
            return _actors.Remove(entityId);
        }

        public void Initialize()
        {
            CombatActionRunner runner = _animationContext.ActionRunner
                ?? throw new InvalidOperationException("Combat animation context has no ActionRunner.");

            if (ReferenceEquals(_subscribedRunner, runner))
            {
                return;
            }

            Shutdown();
            _subscribedRunner = runner;
            _subscribedRunner.ActionStarted += OnActionStarted;
            _subscribedRunner.ActionFrameEventRaised += OnActionFrameEventRaised;
            _subscribedRunner.ActionFinished += OnActionFinished;
            _subscribedRunner.ActionCanceled += OnActionCanceled;
        }

        public void Shutdown()
        {
            if (_subscribedRunner == null)
            {
                return;
            }

            _subscribedRunner.ActionStarted -= OnActionStarted;
            _subscribedRunner.ActionFrameEventRaised -= OnActionFrameEventRaised;
            _subscribedRunner.ActionFinished -= OnActionFinished;
            _subscribedRunner.ActionCanceled -= OnActionCanceled;
            _subscribedRunner = null;
        }

        public CombatMxAnimationBridgeDiagnosticSnapshot CreateSnapshot()
        {
            return new CombatMxAnimationBridgeDiagnosticSnapshot(IsInitialized, _actors.Count, _recentDiagnostics);
        }

        public void Dispose()
        {
            Shutdown();
        }

        private void OnActionStarted(ActionStartedEvent evt)
        {
            if (!_actors.TryGetValue(evt.EntityId, out ActorRuntime actor))
            {
                return;
            }

            string actionKey = _options.BuildActionKey(evt.ActionId);
            MxAnimationActionBinding binding = TryFindBinding(actor, actionKey);
            string correlationId = BuildCorrelationId(evt.EntityId, evt.ActionId, evt.ActionInstanceId, evt.Frame, 0, null);
            var key = new ActionInstanceKey(evt.EntityId, evt.ActionInstanceId);
            _lastCorrelations[key] = new FrameCorrelation(true, evt.Frame, true, 0);

            if (_options.StartRequestKind == CombatMxAnimationStartRequestKind.Play)
            {
                var request = CreatePlayRequest(actor, binding, actionKey, correlationId);
                MxAnimationBackendResult result = actor.Backend.Play(request);
                RecordRequest(
                    CombatMxAnimationBridgeEventKind.ActionStarted,
                    actor,
                    evt.ActionId,
                    actionKey,
                    request.BindingId,
                    evt.ActionInstanceId,
                    true,
                    evt.Frame,
                    true,
                    0,
                    false,
                    default,
                    MxAnimationRequestKind.Play,
                    result,
                    string.Empty,
                    correlationId);
                return;
            }

            var crossFade = CreateCrossFadeRequest(
                actor,
                binding,
                actionKey,
                correlationId,
                Math.Max(0f, _options.StartCrossFadeDurationSeconds));
            MxAnimationBackendResult crossFadeResult = actor.Backend.CrossFade(crossFade);
            RecordRequest(
                CombatMxAnimationBridgeEventKind.ActionStarted,
                actor,
                evt.ActionId,
                actionKey,
                crossFade.BindingId,
                evt.ActionInstanceId,
                true,
                evt.Frame,
                true,
                0,
                false,
                default,
                MxAnimationRequestKind.CrossFade,
                crossFadeResult,
                string.Empty,
                correlationId);
        }

        private void OnActionCanceled(ActionCanceledEvent evt)
        {
            HandleActionEnd(
                CombatMxAnimationBridgeEventKind.ActionCanceled,
                evt.EntityId,
                evt.ActionId,
                evt.ActionInstanceId,
                _options.CanceledRequestKind,
                _options.CanceledCrossFadeBindingId,
                evt.Reason);
        }

        private void OnActionFinished(ActionFinishedEvent evt)
        {
            HandleActionEnd(
                CombatMxAnimationBridgeEventKind.ActionFinished,
                evt.EntityId,
                evt.ActionId,
                evt.ActionInstanceId,
                _options.FinishedRequestKind,
                _options.FinishedCrossFadeBindingId,
                "Action finished.");
        }

        private void OnActionFrameEventRaised(ActionFrameEventRaisedEvent evt)
        {
            if (!_actors.TryGetValue(evt.EntityId, out ActorRuntime actor))
            {
                return;
            }

            string actionKey = _options.BuildActionKey(evt.ActionId);
            MxAnimationActionBinding binding = TryFindBinding(actor, actionKey);
            string bindingId = binding != null ? binding.BindingId : string.Empty;
            string correlationId = BuildCorrelationId(evt.EntityId, evt.ActionId, evt.ActionInstanceId, evt.WorldFrame, evt.LocalFrame, evt.FrameEvent);
            var key = new ActionInstanceKey(evt.EntityId, evt.ActionInstanceId);
            _lastCorrelations[key] = new FrameCorrelation(true, evt.WorldFrame, true, evt.LocalFrame);

            ResolvePresentationEvents(actor, binding, actionKey, evt.FrameEvent, evt.LocalFrame, _resolvedEvents);
            for (int i = 0; i < _resolvedEvents.Count; i++)
            {
                MxAnimationPresentationEvent presentationEvent = _resolvedEvents[i];
                var dispatch = new CombatMxAnimationPresentationEventDispatch(
                    evt.EntityId,
                    actor.TargetActorId,
                    evt.ActionId,
                    actionKey,
                    bindingId,
                    evt.ActionInstanceId,
                    evt.WorldFrame,
                    evt.LocalFrame,
                    evt.FrameEvent,
                    presentationEvent,
                    correlationId);
                _presentationEventSink.Dispatch(dispatch);

                RecordPresentationEvent(
                    actor,
                    evt.ActionId,
                    actionKey,
                    bindingId,
                    evt.ActionInstanceId,
                    evt.WorldFrame,
                    evt.LocalFrame,
                    evt.FrameEvent,
                    presentationEvent,
                    correlationId);
            }

            _resolvedEvents.Clear();
        }

        private void HandleActionEnd(
            CombatMxAnimationBridgeEventKind eventKind,
            CombatEntityId entityId,
            int actionId,
            int actionInstanceId,
            CombatMxAnimationEndRequestKind requestKind,
            string crossFadeBindingId,
            string reason)
        {
            if (!_actors.TryGetValue(entityId, out ActorRuntime actor))
            {
                return;
            }

            string actionKey = _options.BuildActionKey(actionId);
            MxAnimationActionBinding binding = TryFindBinding(actor, actionKey);
            string bindingId = binding != null ? binding.BindingId : string.Empty;
            var key = new ActionInstanceKey(entityId, actionInstanceId);
            _lastCorrelations.TryGetValue(key, out FrameCorrelation frameCorrelation);
            string correlationId = BuildCorrelationId(
                entityId,
                actionId,
                actionInstanceId,
                frameCorrelation.HasWorldFrame ? frameCorrelation.WorldFrame : CombatFrame.Zero,
                frameCorrelation.HasLocalFrame ? frameCorrelation.LocalFrame : 0,
                null);

            if (requestKind == CombatMxAnimationEndRequestKind.CrossFade)
            {
                var request = new MxAnimationCrossFadeRequest
                {
                    TargetActorId = actor.TargetActorId,
                    BindingId = crossFadeBindingId ?? string.Empty,
                    LayerId = binding != null ? binding.Layer : MxAnimationLayerId.Base,
                    FadeDurationSeconds = Math.Max(0f, _options.EndCrossFadeDurationSeconds),
                    CorrelationId = correlationId
                };
                MxAnimationBackendResult result = actor.Backend.CrossFade(request);
                RecordRequest(
                    eventKind,
                    actor,
                    actionId,
                    actionKey,
                    request.BindingId,
                    actionInstanceId,
                    frameCorrelation.HasWorldFrame,
                    frameCorrelation.WorldFrame,
                    frameCorrelation.HasLocalFrame,
                    frameCorrelation.LocalFrame,
                    false,
                    default,
                    MxAnimationRequestKind.CrossFade,
                    result,
                    string.Empty,
                    correlationId);
                _lastCorrelations.Remove(key);
                return;
            }

            var stop = new MxAnimationStopRequest
            {
                TargetActorId = actor.TargetActorId,
                BindingId = bindingId,
                LayerId = binding != null ? binding.Layer : MxAnimationLayerId.Base,
                FadeOutDurationSeconds = Math.Max(0f, _options.StopFadeOutDurationSeconds),
                StopReason = reason ?? string.Empty,
                CorrelationId = correlationId
            };
            MxAnimationBackendResult stopResult = actor.Backend.Stop(stop);
            RecordRequest(
                eventKind,
                actor,
                actionId,
                actionKey,
                bindingId,
                actionInstanceId,
                frameCorrelation.HasWorldFrame,
                frameCorrelation.WorldFrame,
                frameCorrelation.HasLocalFrame,
                frameCorrelation.LocalFrame,
                false,
                default,
                MxAnimationRequestKind.Stop,
                stopResult,
                string.Empty,
                correlationId);
            _lastCorrelations.Remove(key);
        }

        private MxAnimationPlayRequest CreatePlayRequest(
            ActorRuntime actor,
            MxAnimationActionBinding binding,
            string actionKey,
            string correlationId)
        {
            return new MxAnimationPlayRequest
            {
                TargetActorId = actor.TargetActorId,
                BindingId = binding != null ? binding.BindingId : string.Empty,
                ActionKey = actionKey,
                ClipKey = binding != null ? binding.Clip : default,
                LayerId = binding != null ? binding.Layer : MxAnimationLayerId.Base,
                PlaybackSpeed = binding != null ? binding.PlaybackSpeed : 1f,
                Loop = binding != null && binding.Loop,
                AlignmentPolicy = binding != null ? binding.AlignmentPolicy : MxAnimationAlignmentPolicy.StartAtZero,
                CorrelationId = correlationId
            };
        }

        private MxAnimationCrossFadeRequest CreateCrossFadeRequest(
            ActorRuntime actor,
            MxAnimationActionBinding binding,
            string actionKey,
            string correlationId,
            float fadeDurationSeconds)
        {
            return new MxAnimationCrossFadeRequest
            {
                TargetActorId = actor.TargetActorId,
                BindingId = binding != null ? binding.BindingId : string.Empty,
                ActionKey = actionKey,
                ClipKey = binding != null ? binding.Clip : default,
                LayerId = binding != null ? binding.Layer : MxAnimationLayerId.Base,
                FadeDurationSeconds = fadeDurationSeconds,
                PlaybackSpeed = binding != null ? binding.PlaybackSpeed : 1f,
                Loop = binding != null && binding.Loop,
                AlignmentPolicy = binding != null ? binding.AlignmentPolicy : MxAnimationAlignmentPolicy.StartAtZero,
                CorrelationId = correlationId
            };
        }

        private MxAnimationActionBinding TryFindBinding(ActorRuntime actor, string actionKey)
        {
            return actor.AnimationSet.TryFindBinding(string.Empty, actionKey, out MxAnimationActionBinding binding)
                ? binding
                : null;
        }

        private void ResolvePresentationEvents(
            ActorRuntime actor,
            MxAnimationActionBinding actionBinding,
            string actionKey,
            CombatActionFrameEvent frameEvent,
            int localFrame,
            List<MxAnimationPresentationEvent> results)
        {
            results.Clear();
            for (int i = 0; i < _options.FrameEventBindings.Count; i++)
            {
                CombatMxAnimationFrameEventBinding bridgeBinding = _options.FrameEventBindings[i];
                if (bridgeBinding == null || !bridgeBinding.Matches(actionKey, frameEvent))
                {
                    continue;
                }

                if (bridgeBinding.PresentationEvent != null)
                {
                    results.Add(bridgeBinding.PresentationEvent);
                    continue;
                }

                if (TryFindPresentationEvent(actor.AnimationSet, actionBinding, bridgeBinding.PresentationEventId, out MxAnimationPresentationEvent presentationEvent))
                {
                    results.Add(presentationEvent);
                }
            }

            if (results.Count > 0 || actionBinding == null)
            {
                return;
            }

            for (int i = 0; i < actionBinding.PresentationEvents.Count; i++)
            {
                MxAnimationPresentationEvent candidate = actionBinding.PresentationEvents[i];
                if (MatchesBindingPresentationEvent(candidate, frameEvent, localFrame))
                {
                    results.Add(candidate);
                }
            }
        }

        private bool TryFindPresentationEvent(
            MxAnimationSetDefinition animationSet,
            MxAnimationActionBinding actionBinding,
            string presentationEventId,
            out MxAnimationPresentationEvent presentationEvent)
        {
            if (!string.IsNullOrWhiteSpace(presentationEventId) && actionBinding != null)
            {
                for (int i = 0; i < actionBinding.PresentationEvents.Count; i++)
                {
                    MxAnimationPresentationEvent candidate = actionBinding.PresentationEvents[i];
                    if (string.Equals(candidate.EventId, presentationEventId, StringComparison.Ordinal))
                    {
                        presentationEvent = candidate;
                        return true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(presentationEventId))
            {
                for (int i = 0; i < animationSet.Events.Count; i++)
                {
                    MxAnimationPresentationEvent candidate = animationSet.Events[i];
                    if (string.Equals(candidate.EventId, presentationEventId, StringComparison.Ordinal))
                    {
                        presentationEvent = candidate;
                        return true;
                    }
                }
            }

            presentationEvent = null;
            return false;
        }

        private bool MatchesBindingPresentationEvent(
            MxAnimationPresentationEvent candidate,
            CombatActionFrameEvent frameEvent,
            int localFrame)
        {
            if (candidate == null)
                return false;

            if (candidate.TimeDomain != MxAnimationEventTimeDomain.CombatFrame
                && candidate.TimeDomain != MxAnimationEventTimeDomain.PresentationFrame)
            {
                return false;
            }

            if (Math.Abs(candidate.Time - localFrame) > 0.0001f)
                return false;

            return string.Equals(candidate.EventId, _options.BuildFrameEventId(frameEvent.EventId), StringComparison.Ordinal)
                || string.Equals(candidate.EventId, frameEvent.EventId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        private void RecordRequest(
            CombatMxAnimationBridgeEventKind eventKind,
            ActorRuntime actor,
            int actionId,
            string actionKey,
            string bindingId,
            int actionInstanceId,
            bool hasWorldFrame,
            CombatFrame worldFrame,
            bool hasLocalFrame,
            int localFrame,
            bool hasFrameEvent,
            CombatActionFrameEvent frameEvent,
            MxAnimationRequestKind requestKind,
            MxAnimationBackendResult result,
            string presentationEventId,
            string correlationId)
        {
            AddDiagnostic(new CombatMxAnimationBridgeDiagnosticEntry(
                eventKind,
                actor.EntityId,
                actor.TargetActorId,
                actionId,
                actionKey,
                bindingId,
                actionInstanceId,
                hasWorldFrame,
                worldFrame,
                hasLocalFrame,
                localFrame,
                hasFrameEvent,
                frameEvent,
                true,
                requestKind,
                result.Code,
                result.Success,
                presentationEventId,
                correlationId,
                result.Message));
        }

        private void RecordPresentationEvent(
            ActorRuntime actor,
            int actionId,
            string actionKey,
            string bindingId,
            int actionInstanceId,
            CombatFrame worldFrame,
            int localFrame,
            CombatActionFrameEvent frameEvent,
            MxAnimationPresentationEvent presentationEvent,
            string correlationId)
        {
            AddDiagnostic(new CombatMxAnimationBridgeDiagnosticEntry(
                CombatMxAnimationBridgeEventKind.FramePresentationEvent,
                actor.EntityId,
                actor.TargetActorId,
                actionId,
                actionKey,
                bindingId,
                actionInstanceId,
                true,
                worldFrame,
                true,
                localFrame,
                true,
                frameEvent,
                false,
                default,
                MxAnimationBackendResultCode.Success,
                true,
                presentationEvent != null ? presentationEvent.EventId : string.Empty,
                correlationId,
                "Presentation event dispatched."));
        }

        private void AddDiagnostic(CombatMxAnimationBridgeDiagnosticEntry entry)
        {
            int max = Math.Max(1, _options.MaxRecentDiagnostics);
            while (_recentDiagnostics.Count >= max)
            {
                _recentDiagnostics.Dequeue();
            }

            _recentDiagnostics.Enqueue(entry);
        }

        private static string BuildDefaultActorId(CombatEntityId entityId)
        {
            return "entity:" + entityId.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildCorrelationId(
            CombatEntityId entityId,
            int actionId,
            int actionInstanceId,
            CombatFrame worldFrame,
            int localFrame,
            CombatActionFrameEvent? frameEvent)
        {
            string correlation = "entity:" + entityId.Value.ToString(CultureInfo.InvariantCulture)
                + "|action:" + actionId.ToString(CultureInfo.InvariantCulture)
                + "|instance:" + actionInstanceId.ToString(CultureInfo.InvariantCulture)
                + "|world:" + worldFrame.Value.ToString(CultureInfo.InvariantCulture)
                + "|local:" + localFrame.ToString(CultureInfo.InvariantCulture);

            if (frameEvent.HasValue)
            {
                CombatActionFrameEvent value = frameEvent.Value;
                correlation += "|event:" + value.EventId.ToString(CultureInfo.InvariantCulture)
                    + "|order:" + value.SourceOrder.ToString(CultureInfo.InvariantCulture)
                    + "|payload:" + value.IntPayload.ToString(CultureInfo.InvariantCulture);
            }

            return correlation;
        }

        private readonly struct ActorRuntime
        {
            public ActorRuntime(
                CombatEntityId entityId,
                string targetActorId,
                IMxAnimationBackend backend,
                MxAnimationSetDefinition animationSet)
            {
                EntityId = entityId;
                TargetActorId = targetActorId ?? string.Empty;
                Backend = backend;
                AnimationSet = animationSet;
            }

            public CombatEntityId EntityId { get; }

            public string TargetActorId { get; }

            public IMxAnimationBackend Backend { get; }

            public MxAnimationSetDefinition AnimationSet { get; }
        }

        private readonly struct ActionInstanceKey : IEquatable<ActionInstanceKey>
        {
            public ActionInstanceKey(CombatEntityId entityId, int actionInstanceId)
            {
                EntityId = entityId;
                ActionInstanceId = actionInstanceId;
            }

            public CombatEntityId EntityId { get; }

            public int ActionInstanceId { get; }

            public bool Equals(ActionInstanceKey other)
            {
                return EntityId.Equals(other.EntityId) && ActionInstanceId == other.ActionInstanceId;
            }

            public override bool Equals(object obj)
            {
                return obj is ActionInstanceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EntityId.GetHashCode() * 397) ^ ActionInstanceId;
                }
            }
        }

        private readonly struct FrameCorrelation
        {
            public FrameCorrelation(bool hasWorldFrame, CombatFrame worldFrame, bool hasLocalFrame, int localFrame)
            {
                HasWorldFrame = hasWorldFrame;
                WorldFrame = worldFrame;
                HasLocalFrame = hasLocalFrame;
                LocalFrame = localFrame;
            }

            public bool HasWorldFrame { get; }

            public CombatFrame WorldFrame { get; }

            public bool HasLocalFrame { get; }

            public int LocalFrame { get; }
        }
    }
}
