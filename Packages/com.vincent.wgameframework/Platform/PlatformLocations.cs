using System;
using System.IO;

namespace MxFramework.Platform
{
    /// <summary>
    /// Identifies how a platform location must be consumed.
    /// </summary>
    public enum PlatformLocationKind
    {
        Unknown = 0,
        FileSystemPath = 1,
        Uri = 2,
        ProviderOwned = 3
    }

    /// <summary>
    /// Identifies a framework-known logical platform location.
    /// </summary>
    public enum PlatformLocationId
    {
        Unknown = 0,
        PersistentData = 1,
        TemporaryCache = 2,
        StreamingContent = 3
    }

    /// <summary>
    /// Contains a validated platform location and its declared access facts.
    /// </summary>
    /// <remarks>NoAlloc when copied or inspected. Create through <see cref="TryCreate"/>.</remarks>
    public readonly struct PlatformLocation : IEquatable<PlatformLocation>
    {
        private readonly string _value;

        private PlatformLocation(
            PlatformLocationKind kind,
            string value,
            bool isReadable,
            bool isWritable,
            bool intendedForPersistentData)
        {
            Kind = kind;
            _value = value;
            IsReadable = isReadable;
            IsWritable = isWritable;
            IntendedForPersistentData = intendedForPersistentData;
        }

        public PlatformLocationKind Kind { get; }
        public string Value => _value ?? string.Empty;
        public bool IsReadable { get; }
        public bool IsWritable { get; }
        public bool IntendedForPersistentData { get; }

        /// <summary>
        /// Validates and normalizes a location value.
        /// </summary>
        /// <remarks>LowFreqAlloc. File path and URI normalization may allocate.</remarks>
        public static PlatformLocationResult TryCreate(
            PlatformLocationKind kind,
            string value,
            bool isReadable,
            bool isWritable,
            bool intendedForPersistentData)
        {
            if (kind == PlatformLocationKind.Unknown)
            {
                return PlatformLocationResult.Fail(
                    new PlatformError(PlatformErrorCode.InvalidLocation, "Location kind must be explicit."));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return PlatformLocationResult.Fail(
                    new PlatformError(PlatformErrorCode.InvalidLocation, "Location value must not be empty."));
            }

            switch (kind)
            {
                case PlatformLocationKind.FileSystemPath:
                    return CreateFileSystemPath(value, isReadable, isWritable, intendedForPersistentData);
                case PlatformLocationKind.Uri:
                    return CreateUri(value, isReadable, isWritable, intendedForPersistentData);
                case PlatformLocationKind.ProviderOwned:
                    return PlatformLocationResult.Ok(
                        new PlatformLocation(kind, value, isReadable, isWritable, intendedForPersistentData));
                default:
                    return PlatformLocationResult.Fail(
                        new PlatformError(PlatformErrorCode.InvalidLocation, "Location kind is not supported."));
            }
        }

        private static PlatformLocationResult CreateFileSystemPath(
            string value,
            bool isReadable,
            bool isWritable,
            bool intendedForPersistentData)
        {
            if (!Path.IsPathFullyQualified(value))
            {
                return PlatformLocationResult.Fail(
                    new PlatformError(PlatformErrorCode.InvalidLocation, "Filesystem locations must be absolute paths."));
            }

            try
            {
                string normalized = Path.GetFullPath(value);
                return PlatformLocationResult.Ok(
                    new PlatformLocation(
                        PlatformLocationKind.FileSystemPath,
                        normalized,
                        isReadable,
                        isWritable,
                        intendedForPersistentData));
            }
            catch (Exception exception)
            {
                return PlatformLocationResult.Fail(
                    new PlatformError(
                        PlatformErrorCode.InvalidLocation,
                        "Filesystem location could not be normalized.",
                        exception));
            }
        }

        private static PlatformLocationResult CreateUri(
            string value,
            bool isReadable,
            bool isWritable,
            bool intendedForPersistentData)
        {
            if (!System.Uri.TryCreate(value, UriKind.Absolute, out System.Uri uri))
            {
                return PlatformLocationResult.Fail(
                    new PlatformError(PlatformErrorCode.InvalidLocation, "URI locations must be absolute URIs."));
            }

            return PlatformLocationResult.Ok(
                new PlatformLocation(
                    PlatformLocationKind.Uri,
                    uri.AbsoluteUri,
                    isReadable,
                    isWritable,
                    intendedForPersistentData));
        }

        public bool Equals(PlatformLocation other)
        {
            return Kind == other.Kind
                && string.Equals(Value, other.Value, StringComparison.Ordinal)
                && IsReadable == other.IsReadable
                && IsWritable == other.IsWritable
                && IntendedForPersistentData == other.IntendedForPersistentData;
        }

        public override bool Equals(object obj)
        {
            return obj is PlatformLocation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Value);
                hashCode = (hashCode * 397) ^ IsReadable.GetHashCode();
                hashCode = (hashCode * 397) ^ IsWritable.GetHashCode();
                hashCode = (hashCode * 397) ^ IntendedForPersistentData.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(PlatformLocation left, PlatformLocation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlatformLocation left, PlatformLocation right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Contains either a validated platform location or a structured failure.
    /// </summary>
    /// <remarks>NoAlloc when copied or inspected.</remarks>
    public readonly struct PlatformLocationResult
    {
        private PlatformLocationResult(bool success, PlatformLocation location, PlatformError error)
        {
            Success = success;
            Location = location;
            Error = error;
        }

        public bool Success { get; }
        public PlatformLocation Location { get; }
        public PlatformError Error { get; }

        /// <summary>
        /// Creates a successful location result.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public static PlatformLocationResult Ok(PlatformLocation location)
        {
            if (location.Kind == PlatformLocationKind.Unknown || string.IsNullOrEmpty(location.Value))
            {
                return Fail(new PlatformError(
                    PlatformErrorCode.InvalidLocation,
                    "A successful location result requires a validated location."));
            }

            return new PlatformLocationResult(true, location, PlatformError.None);
        }

        /// <summary>
        /// Creates a failed location result.
        /// </summary>
        /// <remarks>NoAlloc when the supplied error is already constructed.</remarks>
        public static PlatformLocationResult Fail(PlatformError error)
        {
            if (error.IsNone)
            {
                error = new PlatformError(PlatformErrorCode.InvalidLocation, "Location resolution failed.");
            }

            return new PlatformLocationResult(false, default, error);
        }
    }

    /// <summary>
    /// Resolves logical platform location identifiers.
    /// </summary>
    public interface IPlatformLocations
    {
        /// <summary>
        /// Resolves a logical location to a typed host location.
        /// </summary>
        /// <remarks>LowFreqAlloc when normalization or failure diagnostics allocate.</remarks>
        PlatformLocationResult Resolve(PlatformLocationId id);
    }
}
