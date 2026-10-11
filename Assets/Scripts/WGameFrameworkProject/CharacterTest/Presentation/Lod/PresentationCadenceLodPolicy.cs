using System;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Consumer cadence with screen-size hysteresis. Configuration describes visual quality, not device cost.</summary>
    internal sealed class PresentationCadenceLodPolicy
    {
        internal float HighScreenHeight { get; set; } = 0.12f;
        internal float MediumScreenHeight { get; set; } = 0.04f;
        internal float Hysteresis { get; set; } = PresentationGeometryLodPolicy.DefaultHysteresis;
        internal double MediumInterval { get; set; } = 1d / 30d;
        internal double LowInterval { get; set; } = 1d / 15d;
        internal double SampleInterval { get; set; } = 0.25d;
        internal double ReductionDelay { get; set; } = 0.75d;
        internal int Tier { get; private set; }
        internal long Evaluations { get; private set; }
        internal long Holds { get; private set; }
        private double _nextSample, _nextPose, _candidateSince;
        private int _candidate = -1;
        private long _cameraVersion = -1;

        internal bool NeedsObservation(double now, long cameraVersion) => now >= _nextSample || cameraVersion != _cameraVersion;

        internal void Observe(double now, long cameraVersion, bool known, float screenHeight, bool protectedPose)
        {
            _cameraVersion = cameraVersion; _nextSample = now + SampleInterval;
            int wanted = !known || protectedPose ? 0 : PresentationGeometryLodPolicy.Select(
                screenHeight, HighScreenHeight, MediumScreenHeight, Tier, Hysteresis);
            if (wanted <= Tier)
            {
                if (wanted < Tier) _nextPose = 0;
                Tier = wanted; _candidate = -1; return;
            }
            if (_candidate != wanted) { _candidate = wanted; _candidateSince = now; }
            if (now - _candidateSince < ReductionDelay) return;
            Tier = wanted; _candidate = -1;
        }

        internal bool Evaluate(double now)
        {
            double interval = Tier == 1 ? MediumInterval : LowInterval;
            if (Tier == 0 || !(interval > 0) || double.IsInfinity(interval) || now >= _nextPose)
            { _nextPose = now + interval; Evaluations++; return true; }
            Holds++; return false;
        }

        internal void Invalidate()
        { Tier = 0; _candidate = -1; _nextSample = _nextPose = 0; _cameraVersion = -1; }
    }
}
