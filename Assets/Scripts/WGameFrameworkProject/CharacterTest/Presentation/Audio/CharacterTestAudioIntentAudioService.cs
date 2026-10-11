using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestAudioIntentAudioService : IAudioService
    {
        private readonly CharacterTestAudioIntentQueue _queue;
        private readonly CharacterTestAudioIntentSource _source;

        public CharacterTestAudioIntentAudioService(
            CharacterTestAudioIntentQueue queue,
            CharacterTestAudioIntentSource source)
        {
            _queue = queue;
            _source = source;
        }

        public AudioPlayResult PlayOneShot(in AudioPlayRequest request)
        {
            if (_queue == null)
            {
                return AudioPlayResult.Fail(
                    AudioErrorCode.NotInitialized,
                    "Audio intent queue is not configured.");
            }

            bool accepted = _queue.Enqueue(CharacterTestAudioIntent.CreateEvent(
                _source,
                request.EventId,
                request.Transform,
                request.EmitterId,
                new RuntimeFrame(request.Frame),
                request.TraceId,
                request.Parameters,
                request.Priority,
                is3D: true));
            return accepted
                ? AudioPlayResult.Ok(AudioHandle.Invalid)
                : AudioPlayResult.Fail(AudioErrorCode.RequestRejected, "Audio intent queue is full.");
        }

        public AudioPlayResult StartEvent(in AudioPlayRequest request, out AudioHandle handle)
        {
            handle = AudioHandle.Invalid;
            return AudioPlayResult.Fail(
                AudioErrorCode.RequestRejected,
                "Audio intent queue only supports one-shot playback in CharacterTest.");
        }

        public AudioResult Stop(AudioHandle handle, AudioStopMode stopMode)
        {
            return AudioResult.Fail(
                AudioErrorCode.RequestRejected,
                "Audio intent queue does not own audio handles.");
        }

        public AudioResult SetParameter(AudioHandle handle, int parameterId, float value)
        {
            return AudioResult.Fail(
                AudioErrorCode.RequestRejected,
                "Audio intent queue does not own audio handles.");
        }

        public AudioResult SetBusVolume(int busId, float volume)
        {
            return AudioResult.Fail(
                AudioErrorCode.RequestRejected,
                "Audio intent queue does not own audio buses.");
        }

        public AudioResult SetBusMuted(int busId, bool muted)
        {
            return AudioResult.Fail(
                AudioErrorCode.RequestRejected,
                "Audio intent queue does not own audio buses.");
        }

        public AudioDebugSnapshot CaptureSnapshot()
        {
            return AudioDebugSnapshot.Empty;
        }

        /// <remarks>NoAlloc.</remarks>
        public void Tick(float deltaTime)
        {
        }
    }
}
