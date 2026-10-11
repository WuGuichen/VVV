using System;

namespace MxFramework.Audio
{
    public static class AudioProductEventIds
    {
        public const int PausedSnapshot = 500400;
        public const int MenuSnapshot = 500401;
        public const int CombatSnapshot = 500402;
        public const int DialogueSnapshot = 500403;
        public const int MenuMusic = 500404;
        public const int ExplorationMusic = 500405;
        public const int CombatMusic = 500406;
        public const int UiClick = 500407;
        public const int UiConfirm = 500408;
        public const int UiBack = 500409;
        public const int WindAmbience = 500410;
        public const int VoiceReady = 500411;
    }

    /// <summary>Owns the single active FMOD mix snapshot for a product mix state.</summary>
    public sealed class AudioMixSnapshotController : IDisposable
    {
        private readonly IAudioService _service;
        private AudioHandle _activeHandle;
        private AudioMixState _state;

        public AudioMixSnapshotController(IAudioService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public AudioMixState State => _state;
        public AudioHandle ActiveHandle => _activeHandle;

        /// <remarks>NoAlloc on successful backend paths.</remarks>
        public AudioResult SetState(AudioMixState state)
        {
            if (_state == state && (_activeHandle.IsValid || state == AudioMixState.Standard))
                return AudioResult.Ok();

            AudioResult stop = StopActive(AudioStopMode.AllowFadeout);
            if (stop.Failed)
                return stop;

            _state = state;
            int eventId = ResolveEventId(state);
            if (eventId == 0)
                return AudioResult.Ok();

            AudioPlayResult play = _service.StartEvent(
                AudioPlayRequest.Create2D(eventId, priority: 100, traceId: new AudioTraceIdentity(AudioTraceKindIds.MixSnapshot, 0, 0, (int)state)),
                out _activeHandle);
            return play.Result;
        }

        public void Dispose()
        {
            StopActive(AudioStopMode.Immediate);
            _state = AudioMixState.Standard;
        }

        private AudioResult StopActive(AudioStopMode mode)
        {
            if (!_activeHandle.IsValid)
                return AudioResult.Ok();
            AudioResult result = _service.Stop(_activeHandle, mode);
            if (result.Success || result.ErrorCode == AudioErrorCode.InvalidHandle)
                _activeHandle = AudioHandle.Invalid;
            return result.ErrorCode == AudioErrorCode.InvalidHandle ? AudioResult.Ok() : result;
        }

        private static int ResolveEventId(AudioMixState state)
        {
            switch (state)
            {
                case AudioMixState.Paused: return AudioProductEventIds.PausedSnapshot;
                case AudioMixState.Menu: return AudioProductEventIds.MenuSnapshot;
                case AudioMixState.Combat: return AudioProductEventIds.CombatSnapshot;
                case AudioMixState.Dialogue: return AudioProductEventIds.DialogueSnapshot;
                default: return 0;
            }
        }
    }
}
