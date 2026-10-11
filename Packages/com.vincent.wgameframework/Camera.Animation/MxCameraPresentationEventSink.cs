using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.Resources;

namespace MxFramework.Camera.Animation
{
    public enum MxCameraPresentationEffectKind
    {
        Shake,
        Focus,
        Impulse
    }

    public readonly struct MxCameraPresentationEventPayload
    {
        public MxCameraPresentationEventPayload(
            MxCameraPresentationEffectKind kind,
            float value,
            int durationFrames = 0,
            int priority = 0,
            MxCameraProfileId profileId = default,
            MxCameraTargetRef targetRef = default)
        {
            Kind = kind;
            Value = value;
            DurationFrames = Math.Max(0, durationFrames);
            Priority = priority;
            ProfileId = profileId;
            TargetRef = targetRef;
        }

        public MxCameraPresentationEffectKind Kind { get; }
        public float Value { get; }
        public int DurationFrames { get; }
        public int Priority { get; }
        public MxCameraProfileId ProfileId { get; }
        public MxCameraTargetRef TargetRef { get; }
    }

    public interface IMxCameraPresentationEventPayloadResolver
    {
        bool TryResolve(ResourceKey payloadKey, out MxCameraPresentationEventPayload payload);
    }

    public sealed class MxCameraDictionaryPresentationEventPayloadResolver : IMxCameraPresentationEventPayloadResolver
    {
        private readonly Dictionary<ResourceKey, MxCameraPresentationEventPayload> _payloads =
            new Dictionary<ResourceKey, MxCameraPresentationEventPayload>();

        public void Register(ResourceKey key, MxCameraPresentationEventPayload payload)
        {
            if (!key.IsValid)
                throw new ArgumentException("Camera presentation payload key is invalid.", nameof(key));

            _payloads[key] = payload;
        }

        public bool TryResolve(ResourceKey payloadKey, out MxCameraPresentationEventPayload payload)
        {
            return _payloads.TryGetValue(payloadKey, out payload);
        }
    }

    public sealed class MxCameraPresentationEventSink : IMxAnimationPresentationEventSink
    {
        private readonly IMxCameraRequestSink _requestSink;
        private readonly IMxCameraPresentationEventPayloadResolver _payloadResolver;
        private readonly Queue<MxCameraDiagnostic> _recentDiagnostics = new Queue<MxCameraDiagnostic>();
        private ulong _nextRequestId = 1UL;

        public MxCameraPresentationEventSink(
            IMxCameraRequestSink requestSink,
            IMxCameraPresentationEventPayloadResolver payloadResolver,
            string cameraEventKind = "Camera",
            int maxRecentDiagnostics = 32)
        {
            _requestSink = requestSink;
            _payloadResolver = payloadResolver;
            CameraEventKind = string.IsNullOrWhiteSpace(cameraEventKind) ? "Camera" : cameraEventKind;
            MaxRecentDiagnostics = Math.Max(1, maxRecentDiagnostics);
        }

        public string CameraEventKind { get; }
        public int MaxRecentDiagnostics { get; }
        public IReadOnlyCollection<MxCameraDiagnostic> RecentDiagnostics => _recentDiagnostics;

        public void Dispatch(MxAnimationPresentationEventDispatch dispatch)
        {
            if (dispatch == null || dispatch.PresentationEvent == null)
            {
                AddDiagnostic(new MxCameraDiagnostic(MxCameraDiagnosticCodes.EventPayloadMissing, "Camera presentation dispatch is missing."));
                return;
            }

            MxAnimationPresentationEvent evt = dispatch.PresentationEvent;
            if (!string.Equals(evt.EventKind, CameraEventKind, StringComparison.Ordinal))
                return;

            if (_requestSink == null)
            {
                AddDiagnostic(new MxCameraDiagnostic(MxCameraDiagnosticCodes.BackendUnavailable, "Camera request sink is unavailable."));
                return;
            }

            if (!evt.PayloadKey.IsValid || _payloadResolver == null || !_payloadResolver.TryResolve(evt.PayloadKey, out MxCameraPresentationEventPayload payload))
            {
                AddDiagnostic(new MxCameraDiagnostic(MxCameraDiagnosticCodes.EventPayloadMissing, "Camera presentation event payload could not be resolved."));
                return;
            }

            MxCameraRequestKind requestKind = ToRequestKind(payload.Kind);
            if (requestKind == MxCameraRequestKind.None)
            {
                AddDiagnostic(new MxCameraDiagnostic(MxCameraDiagnosticCodes.EventInvalidEffect, "Camera presentation event effect is unsupported."));
                return;
            }

            var request = new MxCameraRequest(
                _nextRequestId++,
                dispatch.WorldFrame,
                dispatch.SourceOrder,
                "animation:" + dispatch.ActorId,
                requestKind,
                payload.Priority,
                payload.TargetRef,
                profileId: payload.ProfileId,
                floatValue: payload.Value,
                durationFrames: payload.DurationFrames,
                payloadKey: evt.PayloadKey.ToString(),
                traceId: dispatch.CorrelationId);
            MxCameraResult result = _requestSink.EnqueueRequest(request);
            if (!result.Success)
                AddDiagnostic(new MxCameraDiagnostic(result.Code, result.Message, request.RequestId));
        }

        private static MxCameraRequestKind ToRequestKind(MxCameraPresentationEffectKind kind)
        {
            switch (kind)
            {
                case MxCameraPresentationEffectKind.Shake:
                    return MxCameraRequestKind.Shake;
                case MxCameraPresentationEffectKind.Focus:
                    return MxCameraRequestKind.Focus;
                case MxCameraPresentationEffectKind.Impulse:
                    return MxCameraRequestKind.Impulse;
                default:
                    return MxCameraRequestKind.None;
            }
        }

        private void AddDiagnostic(MxCameraDiagnostic diagnostic)
        {
            while (_recentDiagnostics.Count >= MaxRecentDiagnostics)
                _recentDiagnostics.Dequeue();

            _recentDiagnostics.Enqueue(diagnostic);
        }
    }
}
