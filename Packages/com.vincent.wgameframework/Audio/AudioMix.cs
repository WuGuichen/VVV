using System;

namespace MxFramework.Audio
{
    public static class AudioMixBusIds
    {
        public const int Master = 500001;
        public const int Music = 500002;
        public const int Sfx = 500003;
        public const int Combat = 500004;
        public const int Ability = 500005;
        public const int Physical = 500006;
        public const int Ui = 500007;
        public const int Ambience = 500008;
        public const int Voice = 500009;

        public static readonly int[] PlayerAdjustable =
        {
            Master, Music, Sfx, Ui, Ambience, Voice
        };
    }

    public enum AudioMixState
    {
        Standard = 0,
        Paused = 1,
        Menu = 2,
        Combat = 3,
        Dialogue = 4
    }

    public readonly struct AudioMixSetting
    {
        public AudioMixSetting(int busId, float volume, bool muted = false)
        {
            BusId = busId;
            Volume = Clamp01(volume);
            Muted = muted;
        }

        public int BusId { get; }
        public float Volume { get; }
        public bool Muted { get; }

        private static float Clamp01(float value) => value <= 0f ? 0f : value >= 1f ? 1f : value;
    }

    /// <summary>Applies stable player volumes and low-frequency mix-state ducking without changing saved settings.</summary>
    public sealed class AudioMixController
    {
        private readonly IAudioService _service;
        private readonly Slot[] _slots;
        private AudioMixState _state;
        private float _transitionDuration;
        private float _transitionElapsed;
        private bool _transitioning;

        /// <remarks>AllocByDesign during composition.</remarks>
        public AudioMixController(IAudioService service, AudioMixSetting[] defaults)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            defaults = defaults ?? Array.Empty<AudioMixSetting>();
            _slots = new Slot[defaults.Length];
            for (int i = 0; i < defaults.Length; i++)
                _slots[i] = new Slot(defaults[i]);
        }

        public AudioMixState State => _state;
        public bool IsTransitioning => _transitioning;

        /// <remarks>NoAlloc on successful backend paths.</remarks>
        public AudioResult ApplyUserSettings(ReadOnlySpan<AudioMixSetting> settings)
        {
            _transitioning = false;
            for (int i = 0; i < settings.Length; i++)
            {
                int index = Find(settings[i].BusId);
                if (index < 0)
                    continue;
                Slot slot = _slots[index];
                slot.UserVolume = settings[i].Volume;
                slot.Muted = settings[i].Muted;
                _slots[index] = slot;
            }
            return ApplyCurrentState();
        }

        /// <remarks>NoAlloc on successful backend paths.</remarks>
        public AudioResult SetState(AudioMixState state)
        {
            _state = state;
            _transitioning = false;
            return ApplyCurrentState();
        }

        /// <remarks>NoAlloc on successful backend paths. Call <see cref="Tick"/> from the presentation clock.</remarks>
        public AudioResult TransitionToState(AudioMixState state, float durationSeconds)
        {
            if (durationSeconds <= 0f)
                return SetState(state);
            _state = state;
            _transitionDuration = durationSeconds;
            _transitionElapsed = 0f;
            _transitioning = true;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                slot.TransitionStart = slot.EffectiveVolume;
                slot.TransitionTarget = slot.UserVolume * Multiplier(_state, slot.BusId);
                _slots[i] = slot;
                AudioResult mute = _service.SetBusMuted(slot.BusId, slot.Muted);
                if (mute.Failed)
                {
                    _transitioning = false;
                    return mute;
                }
            }
            return AudioResult.Ok();
        }

        /// <remarks>NoAlloc high-frequency presentation path.</remarks>
        public AudioResult Tick(float deltaSeconds)
        {
            if (!_transitioning)
                return AudioResult.Ok();
            if (deltaSeconds > 0f)
                _transitionElapsed += deltaSeconds;
            float progress = _transitionElapsed >= _transitionDuration
                ? 1f
                : _transitionElapsed / _transitionDuration;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                float effective = slot.TransitionStart + ((slot.TransitionTarget - slot.TransitionStart) * progress);
                AudioResult volume = _service.SetBusVolume(slot.BusId, effective);
                if (volume.Failed)
                    return volume;
                slot.EffectiveVolume = effective;
                _slots[i] = slot;
            }
            if (progress >= 1f)
                _transitioning = false;
            return AudioResult.Ok();
        }

        private AudioResult ApplyCurrentState()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                float effective = slot.UserVolume * Multiplier(_state, slot.BusId);
                AudioResult volume = _service.SetBusVolume(slot.BusId, effective);
                if (volume.Failed)
                    return volume;
                AudioResult mute = _service.SetBusMuted(slot.BusId, slot.Muted);
                if (mute.Failed)
                    return mute;
                slot.EffectiveVolume = effective;
                _slots[i] = slot;
            }
            return AudioResult.Ok();
        }

        private int Find(int busId)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].BusId == busId)
                    return i;
            }
            return -1;
        }

        private static float Multiplier(AudioMixState state, int busId)
        {
            switch (state)
            {
                case AudioMixState.Paused:
                    if (busId == AudioMixBusIds.Music) return 0.7f;
                    if (busId == AudioMixBusIds.Sfx || busId == AudioMixBusIds.Combat || busId == AudioMixBusIds.Ability || busId == AudioMixBusIds.Physical) return 0.15f;
                    if (busId == AudioMixBusIds.Ambience) return 0.4f;
                    return 1f;
                case AudioMixState.Menu:
                    if (busId == AudioMixBusIds.Sfx || busId == AudioMixBusIds.Combat || busId == AudioMixBusIds.Ability || busId == AudioMixBusIds.Physical) return 0.5f;
                    if (busId == AudioMixBusIds.Ambience) return 0.7f;
                    return 1f;
                case AudioMixState.Combat:
                    if (busId == AudioMixBusIds.Ambience) return 0.65f;
                    if (busId == AudioMixBusIds.Voice) return 0.9f;
                    return 1f;
                case AudioMixState.Dialogue:
                    if (busId == AudioMixBusIds.Music) return 0.45f;
                    if (busId == AudioMixBusIds.Sfx || busId == AudioMixBusIds.Combat || busId == AudioMixBusIds.Ability || busId == AudioMixBusIds.Physical) return 0.35f;
                    if (busId == AudioMixBusIds.Ui) return 0.7f;
                    if (busId == AudioMixBusIds.Ambience) return 0.3f;
                    return 1f;
                default:
                    return 1f;
            }
        }

        private struct Slot
        {
            public Slot(in AudioMixSetting setting)
            {
                BusId = setting.BusId;
                UserVolume = setting.Volume;
                Muted = setting.Muted;
                EffectiveVolume = setting.Volume;
                TransitionStart = setting.Volume;
                TransitionTarget = setting.Volume;
            }

            public int BusId;
            public float UserVolume;
            public bool Muted;
            public float EffectiveVolume;
            public float TransitionStart;
            public float TransitionTarget;
        }
    }
}
