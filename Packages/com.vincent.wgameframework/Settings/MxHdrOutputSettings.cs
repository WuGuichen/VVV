using System;

namespace MxFramework.Settings
{
    public enum MxHdrOutputMode
    {
        Default = 0,
        Disabled = 1,
        Enabled = 2
    }

    /// <summary>Local display preferences. Zero luminance uses the display recommendation.</summary>
    public sealed class MxHdrOutputSettings
    {
        public MxHdrOutputSettings(MxHdrOutputMode mode = MxHdrOutputMode.Default,
            float paperWhiteNits = 0, float peakNits = 0)
        {
            Mode = Enum.IsDefined(typeof(MxHdrOutputMode), mode) ? mode : MxHdrOutputMode.Default;
            PaperWhiteNits = Normalize(paperWhiteNits, 80, 400);
            PeakNits = Normalize(peakNits, 400, 5000);
        }

        public MxHdrOutputMode Mode { get; }
        public float PaperWhiteNits { get; }
        public float PeakNits { get; }
        public static MxHdrOutputSettings Default { get; } = new MxHdrOutputSettings();

        private static float Normalize(float value, float min, float max) =>
            float.IsNaN(value) || float.IsInfinity(value) || value <= 0 ? 0 : Math.Min(max, Math.Max(min, value));
    }

    public enum MxHdrOutputState
    {
        Unavailable = 0,
        Sdr = 1,
        Switching = 2,
        Hdr = 3,
        Failed = 4
    }

    public readonly struct MxHdrOutputStatus
    {
        public MxHdrOutputStatus(MxHdrOutputState state, bool available = false, float paperWhiteNits = 0,
            float peakNits = 0, string reason = null, bool active = false)
        { State = state; Available = available; Active = active || state == MxHdrOutputState.Hdr; PaperWhiteNits = paperWhiteNits; PeakNits = peakNits; Reason = reason ?? string.Empty; }
        public MxHdrOutputState State { get; }
        public bool Available { get; }
        public bool Active { get; }
        public float PaperWhiteNits { get; }
        public float PeakNits { get; }
        public string Reason { get; }
    }
}
