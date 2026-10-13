using System;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Independent field lifetime, sampled from the existing stage/turn/simulation owners.</summary>
    internal readonly struct TacticalBlackHoleLifetime
    {
        private TacticalBlackHoleLifetime(ulong stage, ulong sourceTurn, long generatedFrame)
        { Stage = stage; SourceTurn = sourceTurn; LastTurn = sourceTurn + 1; GeneratedFrame = generatedFrame; }
        internal ulong Stage { get; }
        internal ulong SourceTurn { get; }
        internal ulong LastTurn { get; }
        internal long GeneratedFrame { get; }
        internal static bool TryCreate(ulong stage, ulong sourceTurn, long generatedFrame, out TacticalBlackHoleLifetime lifetime)
        {
            lifetime = default;
            if (stage == 0 || sourceTurn == 0 || sourceTurn == ulong.MaxValue || generatedFrame < 0 || generatedFrame == long.MaxValue) return false;
            lifetime = new TacticalBlackHoleLifetime(stage, sourceTurn, generatedFrame); return true;
        }
        /// <remarks>NoAlloc. First force is on a later real simulation step; pause/wall time and source death do not advance this value.</remarks>
        internal bool IsActive(ulong stage, ulong turn, long frame) => Stage != 0 && stage == Stage
            && turn >= SourceTurn && turn <= LastTurn && frame > GeneratedFrame;
    }

    /// <summary>Explicit trial inputs; this type supplies no published balance or immunity defaults.</summary>
    internal readonly struct TacticalBlackHolePullParameters
    {
        internal TacticalBlackHolePullParameters(int radius, int stopRadius, int acceleration, int referenceMass)
        { RadiusMillimeters = radius; StopRadiusMillimeters = stopRadius; AccelerationMillimetersPerSecondSquared = acceleration; ReferenceMassGrams = referenceMass; }
        internal int RadiusMillimeters { get; }
        internal int StopRadiusMillimeters { get; }
        internal int AccelerationMillimetersPerSecondSquared { get; }
        internal int ReferenceMassGrams { get; }
        // Bounds keep fixed-point length calculations representable; they are validation bounds, not a compatibility pool.
        internal bool IsValid => RadiusMillimeters > 0 && RadiusMillimeters <= 100000
            && StopRadiusMillimeters >= 0 && StopRadiusMillimeters < RadiusMillimeters
            && AccelerationMillimetersPerSecondSquared > 0 && ReferenceMassGrams > 0;
    }

    internal readonly struct TacticalBlackHoleAcceleration
    {
        internal TacticalBlackHoleAcceleration(int x, int z) { X = x; Z = z; }
        internal int X { get; }
        internal int Z { get; }
    }

    internal static class TacticalBlackHoleRules
    {
        /// <remarks>NoAlloc. Decimal is a fixed 96-bit integer decimal representation, not a binary float.
        /// Three positive Int32 factors fit its range; retaining them through vector addition avoids any per-field saturation.</remarks>
        internal static bool TrySampleWide(in TacticalBlackHolePullParameters parameters, long deltaX, long deltaY, long deltaZ,
            int mass, int pushScale, bool canPush, out decimal x, out decimal z)
        {
            x = z = 0;
            if (!parameters.IsValid || mass <= 0 || pushScale < 0) return false;
            if (!canPush || pushScale == 0) return true;
            long radius = parameters.RadiusMillimeters;
            if (deltaX < -radius || deltaX > radius || deltaY < -radius || deltaY > radius || deltaZ < -radius || deltaZ > radius) return true;
            long horizontalSquared = deltaX * deltaX + deltaZ * deltaZ;
            if (horizontalSquared + deltaY * deltaY > radius * radius
                || horizontalSquared <= (long)parameters.StopRadiusMillimeters * parameters.StopRadiusMillimeters
                || !TryDirection(deltaX, deltaZ, out var direction)) return true;
            decimal magnitude = (decimal)parameters.AccelerationMillimetersPerSecondSquared * parameters.ReferenceMassGrams
                * pushScale / mass / 1000;
            x = magnitude * ((decimal)direction.X.RawValue / Fix64.Scale);
            z = magnitude * ((decimal)direction.Z.RawValue / Fix64.Scale);
            return true;
        }

        /// <remarks>NoAlloc. Sum wide field contributions first. Common scaling preserves their resultant direction.</remarks>
        internal static bool TryLimitWideCombined(decimal sumX, decimal sumZ, int velocityX, int velocityZ,
            int maximumAcceleration, int terminalRadialSpeed, int simulationTickRate, out TacticalBlackHoleAcceleration acceleration)
        {
            decimal largest = Math.Max(Math.Abs(sumX), Math.Abs(sumZ));
            decimal ratio = largest > int.MaxValue ? (decimal)int.MaxValue / largest : 1m;
            return TryLimitCombined((long)(sumX * ratio), (long)(sumZ * ratio), velocityX, velocityZ,
                maximumAcceleration, terminalRadialSpeed, simulationTickRate, out acceleration);
        }
        /// <summary>One fixed spherical field's horizontal force. Geometry and motion remain in the real collision owner.</summary>
        /// <remarks>NoAlloc. Caller supplies authoritative displacement in millimeters and current physics permissions.
        /// Out-of-range/immune/center samples produce zero. Missing mass or invalid parameters reject the sample.</remarks>
        internal static bool TrySample(in TacticalBlackHolePullParameters parameters, long deltaX, long deltaY, long deltaZ,
            int effectiveMassGrams, int effectivePushScaleMilli, bool canBePushed, int maximumAcceleration,
            out TacticalBlackHoleAcceleration acceleration)
        {
            acceleration = default;
            if (!parameters.IsValid || effectiveMassGrams <= 0 || effectivePushScaleMilli < 0 || maximumAcceleration <= 0) return false;
            if (!canBePushed || effectivePushScaleMilli == 0) return true;
            long radius = parameters.RadiusMillimeters;
            if (deltaX < -radius || deltaX > radius || deltaY < -radius || deltaY > radius || deltaZ < -radius || deltaZ > radius) return true;
            long horizontalSquared = deltaX * deltaX + deltaZ * deltaZ;
            if (horizontalSquared + deltaY * deltaY > radius * radius
                || horizontalSquared <= (long)parameters.StopRadiusMillimeters * parameters.StopRadiusMillimeters) return true;
            if (!TryDirection(deltaX, deltaZ, out var direction)) return true;
            // Bound each multiplication before doing it. Extremely light bodies still honor the shared acceleration cap.
            long massAdjusted = (long)parameters.AccelerationMillimetersPerSecondSquared * parameters.ReferenceMassGrams / effectiveMassGrams;
            long scaledLimit = (long)maximumAcceleration * 1000;
            long magnitude = massAdjusted > scaledLimit / effectivePushScaleMilli ? maximumAcceleration
                : Math.Min(maximumAcceleration, massAdjusted * effectivePushScaleMilli / 1000);
            acceleration = new TacticalBlackHoleAcceleration((int)(direction.X.RawValue * magnitude / Fix64.Scale),
                (int)(direction.Z.RawValue * magnitude / Fix64.Scale));
            return true;
        }

        /// <summary>Caps the vector sum and its contribution to velocity along the resulting inward direction.</summary>
        /// <remarks>NoAlloc. Sum all active fields before calling once. Does not clamp existing/tangential velocity,
        /// root motion, impulses or other systems' external forces. Output is acceleration for ExternalForceRaw.</remarks>
        internal static bool TryLimitCombined(long sumX, long sumZ, int velocityX, int velocityZ,
            int maximumAcceleration, int terminalRadialSpeed, int simulationTickRate, out TacticalBlackHoleAcceleration acceleration)
        {
            acceleration = default;
            if (maximumAcceleration <= 0 || terminalRadialSpeed <= 0 || simulationTickRate <= 0
                || sumX < int.MinValue || sumX > int.MaxValue || sumZ < int.MinValue || sumZ > int.MaxValue) return false;
            if (!TryDirection(sumX, sumZ, out var direction)) return true;
            long normalX = direction.X.RawValue, normalZ = direction.Z.RawValue;
            long normalLength = FloorSquareRoot((ulong)(normalX * normalX + normalZ * normalZ));
            decimal limit = (decimal)terminalRadialSpeed * normalLength;
            decimal radial = (decimal)velocityX * normalX + (decimal)velocityZ * normalZ;
            if (radial >= limit) return true;
            int allowed = (int)Math.Min(maximumAcceleration, (limit - radial) * simulationTickRate / normalLength);
            ulong squared = (ulong)(sumX * sumX) + (ulong)(sumZ * sumZ);
            int x, z;
            if (squared <= (ulong)((long)allowed * allowed)) { x = (int)sumX; z = (int)sumZ; }
            else {
                long length = FloorSquareRoot(squared);
                if ((ulong)length * (ulong)length < squared) length++; // ceil norm guarantees integer output remains in the squared bound.
                x = (int)(sumX * allowed / length); z = (int)(sumZ * allowed / length);
            }
            bool Fits(int forceX, int forceZ) => ((decimal)velocityX + DividePerTick(forceX, simulationTickRate)) * normalX
                + ((decimal)velocityZ + DividePerTick(forceZ, simulationTickRate)) * normalZ <= limit;
            if (!Fits(x, z)) {
                long largest = Math.Max(Math.Abs((long)x), Math.Abs((long)z)), low = 0, high = largest;
                while (low < high) {
                    long middle = low + (high - low + 1) / 2;
                    if (Fits((int)((long)x * middle / largest), (int)((long)z * middle / largest))) low = middle;
                    else high = middle - 1;
                }
                x = (int)((long)x * low / largest); z = (int)((long)z * low / largest);
            }
            acceleration = new TacticalBlackHoleAcceleration(x, z);
            return true;
        }

        // Matches CharacterKinematicMotion.DividePerTick/DivideRoundToInt, including negative half ties.
        private static int DividePerTick(int acceleration, int tickRate)
            => (int)(((long)acceleration + (acceleration >= 0 ? tickRate / 2 : -(tickRate / 2))) / tickRate);

        private static long FloorSquareRoot(ulong value)
        {
            ulong remainder = value, root = 0, bit = 1UL << 62;
            while (bit > remainder) bit >>= 2;
            while (bit != 0) {
                if (remainder >= root + bit) { remainder -= root + bit; root = (root >> 1) + bit; }
                else root >>= 1;
                bit >>= 2;
            }
            return (long)root;
        }

        private static bool TryDirection(long x, long z, out FixVector3 direction)
        {
            direction = default;
            long largest = Math.Max(Math.Abs(x), Math.Abs(z));
            if (largest == 0) return false;
            return new FixVector3(Fix64.FromRatio(x, largest), Fix64.Zero, Fix64.FromRatio(z, largest)).TryNormalize(out direction);
        }
    }
}
