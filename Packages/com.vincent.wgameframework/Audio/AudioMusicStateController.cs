using System;

namespace MxFramework.Audio
{
    public enum AudioMusicState
    {
        None = 0,
        Menu = 1,
        Exploration = 2,
        Combat = 3,
        Victory = 4,
        Defeat = 5
    }

    public readonly struct AudioMusicStateBinding
    {
        public AudioMusicStateBinding(AudioMusicState state, int eventId, int priority = 80)
        {
            State = state;
            EventId = eventId;
            Priority = priority;
        }

        public AudioMusicState State { get; }
        public int EventId { get; }
        public int Priority { get; }
    }

    /// <summary>Owns stateful music Event transitions independently from saved Bus volume.</summary>
    public sealed class AudioMusicStateController : IDisposable
    {
        private readonly IAudioService _service;
        private readonly AudioMusicStateBinding[] _bindings;
        private AudioHandle _handle;
        private AudioMusicState _state;

        /// <remarks>AllocByDesign during composition.</remarks>
        public AudioMusicStateController(IAudioService service, AudioMusicStateBinding[] bindings)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _bindings = bindings == null
                ? Array.Empty<AudioMusicStateBinding>()
                : (AudioMusicStateBinding[])bindings.Clone();
        }

        public AudioMusicState State => _state;
        public AudioHandle ActiveHandle => _handle;

        /// <remarks>NoAlloc on successful backend paths.</remarks>
        public AudioResult TransitionTo(AudioMusicState state)
        {
            if (_state == state && (_handle.IsValid || state == AudioMusicState.None))
                return AudioResult.Ok();

            AudioResult stop = Stop(AudioStopMode.AllowFadeout);
            if (stop.Failed)
                return stop;
            _state = state;
            if (state == AudioMusicState.None)
                return AudioResult.Ok();

            if (!TryFind(state, out AudioMusicStateBinding binding))
            {
                return AudioResult.Fail(
                    AudioErrorCode.InvalidEvent,
                    "Music state has no Event binding: " + state + ".");
            }
            AudioPlayResult play = _service.StartEvent(
                AudioPlayRequest.Create2D(
                    binding.EventId,
                    priority: binding.Priority,
                    traceId: new AudioTraceIdentity(AudioTraceKindIds.MusicState, 0, 0, (int)state)),
                out _handle);
            return play.Result;
        }

        public void Dispose()
        {
            Stop(AudioStopMode.Immediate);
            _state = AudioMusicState.None;
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

        private bool TryFind(AudioMusicState state, out AudioMusicStateBinding binding)
        {
            for (int i = 0; i < _bindings.Length; i++)
            {
                if (_bindings[i].State == state)
                {
                    binding = _bindings[i];
                    return true;
                }
            }
            binding = default;
            return false;
        }
    }
}
