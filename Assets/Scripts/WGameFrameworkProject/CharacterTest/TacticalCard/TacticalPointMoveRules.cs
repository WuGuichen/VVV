using System;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Author-owned per-play point contract. GroundTraction is a distinct mechanism, not walking.</summary>
    internal sealed class TacticalPointMoveAuthoring
    {
        [JsonProperty(Required = Required.Always)] public string Kind { get; set; } = "WorldPoint";
        [JsonProperty(Required = Required.Always)] public string RangeCenter { get; set; } = "Execution";
        [JsonProperty(Required = Required.Always)] public string StartPolicy { get; set; } = "OwnedMoveAccepted";
        [JsonProperty(Required = Required.Always)] public int RangeMillimeters { get; set; }
        [JsonProperty(Required = Required.Always)] public int ArrivalMillimeters { get; set; }
        [JsonProperty(Required = Required.Always)] public int MaximumFrames { get; set; }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public int SpeedMilli { get; set; }
        [JsonProperty(Required = Required.Always)] public bool RequireReachable { get; set; }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public string Mechanism { get; set; }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public int TractionSpeedMillimetersPerSecond { get; set; }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public int TractionAccelerationMillimetersPerSecondSquared { get; set; }

        internal TacticalPointMoveRules Freeze()
        {
            bool traction = Mechanism == "GroundTraction";
            if (Kind != "WorldPoint" || RangeCenter != "Execution" || !RequireReachable
                || (traction ? StartPolicy != "OwnedTractionAccepted" : StartPolicy != "OwnedMoveAccepted")
                || !traction && !string.IsNullOrEmpty(Mechanism) || traction && SpeedMilli != 0)
                throw new ArgumentException("Point execution requires an explicit supported mechanism and matching accepted-motion start policy.");
            if (MaximumFrames >= int.MaxValue - 1) throw new ArgumentOutOfRangeException(nameof(MaximumFrames));
            return new TacticalPointMoveRules(RangeMillimeters, ArrivalMillimeters, MaximumFrames,
                traction ? 1000 : SpeedMilli, RequireReachable, traction ? TacticalPointMoveStartPolicy.OwnedTractionAccepted : TacticalPointMoveStartPolicy.OwnedMoveAccepted,
                TractionSpeedMillimetersPerSecond, TractionAccelerationMillimetersPerSecondSquared);
        }
    }

    internal enum TacticalPointMoveStartPolicy { OwnedMoveAccepted = 1, OwnedTractionAccepted = 2 }

    /// <summary>Immutable point execution rules. The legacy PointMove property carries both explicit kinds.</summary>
    internal sealed class TacticalPointMoveRules
    {
        internal TacticalPointInputRules Input { get; }
        internal int ArrivalMillimeters { get; }
        internal int MaximumFrames { get; }
        internal int SpeedMilli { get; }
        internal TacticalPointMoveStartPolicy StartPolicy { get; }
        internal bool IsGroundTraction => StartPolicy == TacticalPointMoveStartPolicy.OwnedTractionAccepted;
        internal int TractionSpeedMillimetersPerSecond { get; }
        internal int TractionAccelerationMillimetersPerSecondSquared { get; }
        internal TacticalPointMoveRules(int rangeMillimeters, int arrivalMillimeters, int maximumFrames,
            int speedMilli, bool requireReachable, TacticalPointMoveStartPolicy startPolicy, int tractionSpeed = 0, int tractionAcceleration = 0)
        {
            if (arrivalMillimeters <= 0 || arrivalMillimeters >= rangeMillimeters) throw new ArgumentOutOfRangeException(nameof(arrivalMillimeters));
            if (maximumFrames < 1) throw new ArgumentOutOfRangeException(nameof(maximumFrames));
            if (speedMilli < 100 || speedMilli > 1000) throw new ArgumentOutOfRangeException(nameof(speedMilli));
            if (startPolicy != TacticalPointMoveStartPolicy.OwnedMoveAccepted && startPolicy != TacticalPointMoveStartPolicy.OwnedTractionAccepted)
                throw new ArgumentOutOfRangeException(nameof(startPolicy));
            bool traction = startPolicy == TacticalPointMoveStartPolicy.OwnedTractionAccepted;
            if (traction && (rangeMillimeters > 100000 || maximumFrames > 600 || tractionSpeed < 100 || tractionSpeed > 20000
                || tractionAcceleration < 100 || tractionAcceleration > 100000 || !requireReachable)
                || !traction && (tractionSpeed != 0 || tractionAcceleration != 0))
                throw new ArgumentException("Invalid ground traction motion contract.");
            Input = new TacticalPointInputRules(rangeMillimeters, TacticalPointRangeCenter.Execution, requireReachable);
            ArrivalMillimeters = arrivalMillimeters; MaximumFrames = maximumFrames; SpeedMilli = speedMilli; StartPolicy = startPolicy;
            TractionSpeedMillimetersPerSecond = tractionSpeed; TractionAccelerationMillimetersPerSecondSquared = tractionAcceleration;
        }
    }
}