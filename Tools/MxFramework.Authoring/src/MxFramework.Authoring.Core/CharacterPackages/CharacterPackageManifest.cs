using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public enum CharacterResourcePackageKind
    {
        Unknown = 0,
        Character = 1
    }

    public enum CharacterCoordinateAxis
    {
        Unknown = 0,
        XPositive = 1,
        XNegative = 2,
        YPositive = 3,
        YNegative = 4,
        ZPositive = 5,
        ZNegative = 6
    }

    public enum CharacterCoordinateHandedness
    {
        Unknown = 0,
        LeftHanded = 1,
        RightHanded = 2
    }

    public enum CharacterRotationStorage
    {
        Unknown = 0,
        Quaternion = 1
    }

    public sealed class CharacterPackageCoordinateConvention
    {
        public CharacterCoordinateAxis UpAxis { get; set; } = CharacterCoordinateAxis.YPositive;
        public CharacterCoordinateAxis ForwardAxis { get; set; } = CharacterCoordinateAxis.ZPositive;
        public CharacterCoordinateHandedness Handedness { get; set; } = CharacterCoordinateHandedness.LeftHanded;
        public float UnitScaleMeters { get; set; } = 1f;
        public CharacterRotationStorage RotationStorage { get; set; } = CharacterRotationStorage.Quaternion;
        public float RoundTripPositionTolerance { get; set; } = 0.0001f;
        public float RoundTripRotationToleranceDegrees { get; set; } = 0.01f;
    }

    public sealed class CharacterPackageDependency
    {
        public string Kind { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string VersionRange { get; set; } = string.Empty;
        public bool Optional { get; set; }
    }

    public sealed class CharacterPackageHashSet
    {
        public string Algorithm { get; set; } = "sha256";
        public string SourcePackageHash { get; set; } = string.Empty;
        public string ResourceHash { get; set; } = string.Empty;
        public string GeneratedConfigHash { get; set; } = string.Empty;
    }

    public sealed class CharacterPackageManifest
    {
        public string PackageId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Version { get; set; } = "0.1.0";
        public CharacterResourcePackageKind Kind { get; set; } = CharacterResourcePackageKind.Character;
        public string PackageSchemaVersion { get; set; } = "1.0";
        public string SourceSchemaVersion { get; set; } = "1.0";
        public string AuthoringSchemaVersion { get; set; } = "1.0";
        public CharacterPackageCoordinateConvention CoordinateConvention { get; set; } = new CharacterPackageCoordinateConvention();
        public CharacterPackageHashSet Hashes { get; set; } = new CharacterPackageHashSet();
        public List<CharacterPackageDependency> Dependencies { get; set; } = new List<CharacterPackageDependency>();
        public List<string> ConfigFiles { get; set; } = new List<string>();
        public List<string> AuthoringOnlyFiles { get; set; } = new List<string>();
    }
}
