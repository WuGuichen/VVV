using System;

namespace MxFramework.CharacterAction
{
    /// <summary>Frozen reference capsule geometry, in root-local millimeters. No live target authority.</summary>
    public sealed class CharacterActionReferenceDummy
    {
        /// <remarks>LowFreqAlloc. Height includes the capsule hemispheres.</remarks>
        public CharacterActionReferenceDummy(int version, int heightMillimeters, int radiusMillimeters,
            int centerXMillimeters, int centerYMillimeters, int centerZMillimeters)
        {
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (radiusMillimeters <= 0 || heightMillimeters < 2L * radiusMillimeters)
                throw new ArgumentOutOfRangeException(nameof(heightMillimeters));
            Version = version;
            HeightMillimeters = heightMillimeters;
            RadiusMillimeters = radiusMillimeters;
            CenterXMillimeters = centerXMillimeters;
            CenterYMillimeters = centerYMillimeters;
            CenterZMillimeters = centerZMillimeters;
        }

        public int Version { get; }
        public int HeightMillimeters { get; }
        public int RadiusMillimeters { get; }
        public int CenterXMillimeters { get; }
        public int CenterYMillimeters { get; }
        public int CenterZMillimeters { get; }
    }

    /// <summary>Exact deterministic scan grid. The final sample always includes SearchRangeMax.</summary>
    public sealed class CharacterActionSpatialMeasurement
    {
        /// <remarks>LowFreqAlloc. A non-divisible range ends with one shorter step.</remarks>
        public CharacterActionSpatialMeasurement(string proberVersion, int stepMillimeters,
            int searchMinMillimeters, int searchMaxMillimeters, CharacterActionReferenceDummy referenceDummy)
        {
            if (string.IsNullOrWhiteSpace(proberVersion)) throw new ArgumentException("Version is required.", nameof(proberVersion));
            if (stepMillimeters <= 0) throw new ArgumentOutOfRangeException(nameof(stepMillimeters));
            if (searchMinMillimeters < 0 || searchMaxMillimeters <= searchMinMillimeters)
                throw new ArgumentOutOfRangeException(nameof(searchMaxMillimeters));
            ReferenceDummy = referenceDummy ?? throw new ArgumentNullException(nameof(referenceDummy));
            ProberVersion = proberVersion;
            StepMillimeters = stepMillimeters;
            SearchMinMillimeters = searchMinMillimeters;
            SearchMaxMillimeters = searchMaxMillimeters;
        }

        public string ProberVersion { get; }
        public int StepMillimeters { get; }
        public int SearchMinMillimeters { get; }
        public int SearchMaxMillimeters { get; }
        public CharacterActionReferenceDummy ReferenceDummy { get; }
        public double StepSize => StepMillimeters / 1000d;
        public double SearchRangeMin => SearchMinMillimeters / 1000d;
        public double SearchRangeMax => SearchMaxMillimeters / 1000d;
    }

    public enum CharacterActionSectorSource { Fallback = 0, Authored = 1, Measured = 2 }

    /// <summary>Angle provenance is explicit; fallback is never a measured coverage claim.</summary>
    public readonly struct CharacterActionTacticalSector
    {
        public static CharacterActionTacticalSector Fallback =>
            new CharacterActionTacticalSector(0, 30000, CharacterActionSectorSource.Fallback);

        /// <remarks>NoAlloc.</remarks>
        public CharacterActionTacticalSector(int centerMilliDegrees, int toleranceMilliDegrees, CharacterActionSectorSource source)
        {
            if (centerMilliDegrees < -180000 || centerMilliDegrees > 180000)
                throw new ArgumentOutOfRangeException(nameof(centerMilliDegrees));
            if (toleranceMilliDegrees < 0 || toleranceMilliDegrees > 180000)
                throw new ArgumentOutOfRangeException(nameof(toleranceMilliDegrees));
            if (source < CharacterActionSectorSource.Fallback || source > CharacterActionSectorSource.Measured)
                throw new ArgumentOutOfRangeException(nameof(source));
            if (source == CharacterActionSectorSource.Fallback && (centerMilliDegrees != 0 || toleranceMilliDegrees != 30000))
                throw new ArgumentException("Fallback must be forward with +/-30 degree tolerance.");
            CenterMilliDegrees = centerMilliDegrees;
            ToleranceMilliDegrees = toleranceMilliDegrees;
            Source = source;
        }

        public int CenterMilliDegrees { get; }
        public int ToleranceMilliDegrees { get; }
        public CharacterActionSectorSource Source { get; }
        public double CenterDegrees => CenterMilliDegrees / 1000d;
        public double ToleranceDegrees => ToleranceMilliDegrees / 1000d;
    }
}
