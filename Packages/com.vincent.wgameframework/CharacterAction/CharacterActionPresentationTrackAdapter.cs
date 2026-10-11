using System;
using static MxFramework.CharacterAction.CharacterActionAdapterShared;

namespace MxFramework.CharacterAction
{
    public sealed class CharacterActionPresentationTrackAdapter
    {
        private readonly ICharacterActionAnimationRequestSink _animationSink;
        private readonly ICharacterActionAudioCueRequestSink _audioCueSink;
        private readonly ICharacterActionVfxRequestSink _vfxSink;
        private readonly ICharacterActionCameraRequestSink _cameraSink;
        private readonly ICharacterActionUiFeedbackRequestSink _uiFeedbackSink;

        public CharacterActionPresentationTrackAdapter(
            ICharacterActionAnimationRequestSink animationSink = null,
            ICharacterActionAudioCueRequestSink audioCueSink = null,
            ICharacterActionVfxRequestSink vfxSink = null,
            ICharacterActionCameraRequestSink cameraSink = null,
            ICharacterActionUiFeedbackRequestSink uiFeedbackSink = null)
        {
            _animationSink = animationSink;
            _audioCueSink = audioCueSink;
            _vfxSink = vfxSink;
            _cameraSink = cameraSink;
            _uiFeedbackSink = uiFeedbackSink;
        }

        /// <remarks>NoAlloc after the caller-owned buffer and downstream sinks are warm.</remarks>
        public CharacterActionPresentationAdapterResult Adapt(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionPresentationAdapterContext context,
            CharacterActionDispatchBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            buffer.Clear();

            int animationCount = 0;
            int audioCount = 0;
            int vfxCount = 0;
            int cameraCount = 0;
            int uiCount = 0;
            AdaptOne(
                runnerEvent,
                context,
                buffer,
                ref animationCount,
                ref audioCount,
                ref vfxCount,
                ref cameraCount,
                ref uiCount);
            return new CharacterActionPresentationAdapterResult(
                animationCount,
                audioCount,
                vfxCount,
                cameraCount,
                uiCount,
                buffer);
        }

        /// <remarks>NoAlloc after the caller-owned buffer and downstream sinks are warm.</remarks>
        public CharacterActionPresentationAdapterResult AdaptMany(
            CharacterActionRunnerEvent[] runnerEvents,
            CharacterActionPresentationAdapterContext context,
            CharacterActionDispatchBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            buffer.Clear();
            runnerEvents = runnerEvents ?? Array.Empty<CharacterActionRunnerEvent>();

            int animationCount = 0;
            int audioCount = 0;
            int vfxCount = 0;
            int cameraCount = 0;
            int uiCount = 0;
            for (int i = 0; i < runnerEvents.Length; i++)
            {
                AdaptOne(
                    runnerEvents[i],
                    context,
                    buffer,
                    ref animationCount,
                    ref audioCount,
                    ref vfxCount,
                    ref cameraCount,
                    ref uiCount);
            }

            return new CharacterActionPresentationAdapterResult(
                animationCount,
                audioCount,
                vfxCount,
                cameraCount,
                uiCount,
                buffer);
        }

        private void AdaptOne(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionPresentationAdapterContext context,
            CharacterActionDispatchBuffer buffer,
            ref int animationCount,
            ref int audioCount,
            ref int vfxCount,
            ref int cameraCount,
            ref int uiCount)
        {
            if ((runnerEvent.Kind != CharacterActionRunnerEventKind.TrackEventFired
                    && runnerEvent.Kind != CharacterActionRunnerEventKind.TrackLifecycle)
                || !runnerEvent.TrackDispatch.HasEvent)
                return;

            CharacterActionTrackDispatchEvent dispatch = runnerEvent.TrackDispatch;
            if (dispatch.TrackKind == CharacterActionTrackKind.Animation)
            {
                if (TryAdaptAnimation(runnerEvent, dispatch, context, buffer)) animationCount++;
                return;
            }

            if (dispatch.TrackKind != CharacterActionTrackKind.Presentation)
                return;
            switch (dispatch.EventKind)
            {
                case CharacterActionTrackEventKind.PlayAudioCue:
                    if (TryAdaptAudioCue(runnerEvent, dispatch, context, buffer)) audioCount++;
                    break;
                case CharacterActionTrackEventKind.SpawnVisualCue:
                    if (TryAdaptVfx(runnerEvent, dispatch, context, buffer)) vfxCount++;
                    break;
                case CharacterActionTrackEventKind.CameraImpulse:
                    if (TryAdaptCamera(runnerEvent, dispatch, context, buffer)) cameraCount++;
                    break;
                case CharacterActionTrackEventKind.UiFeedback:
                    if (TryAdaptUiFeedback(runnerEvent, dispatch, context, buffer)) uiCount++;
                    break;
            }
        }

        private bool TryAdaptAnimation(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context,
            CharacterActionDispatchBuffer diagnostics)
        {
            if (_animationSink == null)
            {
                diagnostics.Add(MissingSink("Animation"));
                return false;
            }
            if (!context.HasTargetActor)
            {
                diagnostics.Add(PayloadMissing("Animation request requires a target actor id."));
                return false;
            }
            string animationKey = dispatch.EffectiveAnimationKey;
            if (string.IsNullOrEmpty(animationKey))
            {
                diagnostics.Add(PayloadMissing("Animation track event requires an animation clip resource key or action key."));
                return false;
            }

            var request = new CharacterActionAnimationRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.TargetActorId,
                dispatch.EventKind,
                dispatch.AnimationActionKey,
                context.AnimationLayerId,
                dispatch.TransitionSeconds,
                dispatch.AnimationClipResourceKey,
                dispatch.AnimationPlaybackSpeed,
                dispatch.AnimationStartOffsetFrames,
                dispatch.AnimationEndOffsetFrames,
                dispatch.EndFrame);
            CharacterActionAdapterSinkResult result;
            try { result = _animationSink.SubmitAnimationRequest(request); }
            catch (Exception ex) { result = SinkFailure("Animation", ex); }
            diagnostics.AddRange(in result);
            return result.Accepted;
        }

        private bool TryAdaptAudioCue(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context,
            CharacterActionDispatchBuffer diagnostics)
        {
            if (_audioCueSink == null)
            {
                diagnostics.Add(MissingSink("Audio"));
                return false;
            }
            if ((dispatch.PresentationCueId <= 0))
            {
                diagnostics.Add(PayloadMissing("Audio presentation event requires a cue id."));
                return false;
            }
            var request = new CharacterActionAudioCueRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.TargetActorId,
                dispatch.PresentationCueId);
            CharacterActionAdapterSinkResult result;
            try { result = _audioCueSink.SubmitAudioCueRequest(request); }
            catch (Exception ex) { result = SinkFailure("Audio", ex); }
            diagnostics.AddRange(in result);
            return result.Accepted;
        }

        private bool TryAdaptVfx(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context,
            CharacterActionDispatchBuffer diagnostics)
        {
            if (_vfxSink == null)
            {
                diagnostics.Add(MissingSink("VFX"));
                return false;
            }
            if (string.IsNullOrEmpty(dispatch.ResourceKey))
            {
                diagnostics.Add(PayloadMissing("VFX presentation event requires a resource key."));
                return false;
            }
            var request = new CharacterActionVfxRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.TargetActorId,
                dispatch.ResourceKey,
                dispatch.EndFrame,
                dispatch.VfxConfig);
            CharacterActionAdapterSinkResult result;
            try { result = _vfxSink.SubmitVfxRequest(request); }
            catch (Exception ex) { result = SinkFailure("VFX", ex); }
            diagnostics.AddRange(in result);
            return result.Accepted;
        }

        private bool TryAdaptCamera(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context,
            CharacterActionDispatchBuffer diagnostics)
        {
            if (_cameraSink == null)
            {
                diagnostics.Add(MissingSink("Camera"));
                return false;
            }
            if ((dispatch.PresentationCueId <= 0) && string.IsNullOrEmpty(dispatch.ResourceKey))
            {
                diagnostics.Add(PayloadMissing("Camera presentation event requires a request id or payload key."));
                return false;
            }
            var request = new CharacterActionCameraRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.TargetActorId,
                dispatch.PresentationCueId,
                dispatch.ResourceKey);
            CharacterActionAdapterSinkResult result;
            try { result = _cameraSink.SubmitCameraRequest(request); }
            catch (Exception ex) { result = SinkFailure("Camera", ex); }
            diagnostics.AddRange(in result);
            return result.Accepted;
        }

        private bool TryAdaptUiFeedback(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionPresentationAdapterContext context,
            CharacterActionDispatchBuffer diagnostics)
        {
            if (_uiFeedbackSink == null)
            {
                diagnostics.Add(MissingSink("UIFeedback"));
                return false;
            }
            if ((dispatch.PresentationCueId <= 0))
            {
                diagnostics.Add(PayloadMissing("UI feedback presentation event requires a feedback id."));
                return false;
            }
            var request = new CharacterActionUiFeedbackRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.TargetActorId,
                dispatch.PresentationCueId,
                dispatch.ResourceKey);
            CharacterActionAdapterSinkResult result;
            try { result = _uiFeedbackSink.SubmitUiFeedbackRequest(request); }
            catch (Exception ex) { result = SinkFailure("UIFeedback", ex); }
            diagnostics.AddRange(in result);
            return result.Accepted;
        }

        private static CharacterActionAdapterSinkResult SinkFailure(string sinkName, Exception exception)
        {
            return CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCode.AdapterSinkFailure,
                sinkName + " sink failed: " + exception.Message));
        }
    }
}
