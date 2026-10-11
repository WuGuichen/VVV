using System;
using System.Collections.Generic;

namespace MxFramework.Settings
{
    public readonly struct MxAudioBusSetting
    {
        public MxAudioBusSetting(int busId, float volume, bool muted)
        {
            BusId = busId;
            Volume = Clamp01(volume);
            Muted = muted;
        }

        public int BusId { get; }
        public float Volume { get; }
        public bool Muted { get; }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }
    }

    public sealed class MxAudioSettings
    {
        private readonly MxAudioBusSetting[] _busSettings;

        public MxAudioSettings(IReadOnlyList<MxAudioBusSetting> busSettings = null)
        {
            if (busSettings == null || busSettings.Count == 0)
            {
                _busSettings = Array.Empty<MxAudioBusSetting>();
                return;
            }

            _busSettings = new MxAudioBusSetting[busSettings.Count];
            for (int i = 0; i < busSettings.Count; i++)
                _busSettings[i] = busSettings[i];
        }

        public IReadOnlyList<MxAudioBusSetting> BusSettings => _busSettings;

        public static MxAudioSettings Empty { get; } = new MxAudioSettings();
    }
}
