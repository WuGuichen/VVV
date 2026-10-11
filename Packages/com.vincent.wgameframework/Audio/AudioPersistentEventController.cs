using System;

namespace MxFramework.Audio
{
    /// <summary>Owns one persistent controlled Event such as ambience or a scene bed.</summary>
    public sealed class AudioPersistentEventController : IDisposable
    {
        private readonly IAudioService _service;
        private AudioHandle _handle;
        private int _eventId;

        /// <summary>Creates a controller for a single persistent Event instance.</summary>
        /// <remarks>LowFreqAlloc: composition-time construction only; transition and stop paths remain NoAlloc.</remarks>
        public AudioPersistentEventController(IAudioService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public int EventId => _eventId;
        public AudioHandle ActiveHandle => _handle;

        /// <remarks>NoAlloc on successful backend paths when traceId is precomputed.</remarks>
        public AudioResult TransitionTo(int eventId, AudioTraceIdentity traceId, int priority = 40)
        {
            if (_eventId == eventId && (_handle.IsValid || eventId == 0))
                return AudioResult.Ok();

            AudioResult stop = Stop(AudioStopMode.AllowFadeout);
            if (stop.Failed)
                return stop;

            _eventId = eventId;
            if (eventId == 0)
                return AudioResult.Ok();

            AudioPlayResult play = _service.StartEvent(
                AudioPlayRequest.Create2D(
                    eventId,
                    priority: priority,
                    traceId: traceId),
                out _handle);
            if (play.Result.Failed)
                _eventId = 0;
            return play.Result;
        }

        public void Dispose()
        {
            Stop(AudioStopMode.Immediate);
            _eventId = 0;
        }

        private AudioResult Stop(AudioStopMode mode)
        {
            if (!_handle.IsValid)
                return AudioResult.Ok();
            AudioResult result = _service.Stop(_handle, mode);
            if (result.Success || result.ErrorCode == AudioErrorCode.InvalidHandle)
                _handle = AudioHandle.Invalid;
            return result.ErrorCode == AudioErrorCode.InvalidHandle ? AudioResult.Ok() : result;
        }
    }
}
