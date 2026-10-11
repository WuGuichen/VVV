using System;
using System.IO;
using UnityEngine;

namespace MxFramework.Platform.Unity
{
    /// <summary>
    /// Resolves cached Unity application locations without erasing filesystem/URI semantics.
    /// </summary>
    public sealed class UnityPlatformLocations : IPlatformLocations
    {
        private static readonly PlatformError UnknownLocationError = new PlatformError(
            PlatformErrorCode.InvalidArgument,
            "Platform location id must be explicit.");

        private readonly PlatformLocationResult _persistentData;
        private readonly PlatformLocationResult _temporaryCache;
        private readonly PlatformLocationResult _streamingContent;

        /// <summary>
        /// Captures the current Unity application locations.
        /// </summary>
        /// <remarks>LowFreqAlloc during composition. Resolve only returns cached values.</remarks>
        public UnityPlatformLocations()
            : this(
                Application.persistentDataPath,
                Application.temporaryCachePath,
                Application.streamingAssetsPath)
        {
        }

        /// <summary>
        /// Creates an adapter from already captured Unity host values.
        /// </summary>
        /// <remarks>LowFreqAlloc while locations are validated and normalized.</remarks>
        public UnityPlatformLocations(
            string persistentDataPath,
            string temporaryCachePath,
            string streamingAssetsPath)
        {
            _persistentData = PlatformLocation.TryCreate(
                PlatformLocationKind.FileSystemPath,
                persistentDataPath,
                isReadable: true,
                isWritable: true,
                intendedForPersistentData: true);
            _temporaryCache = PlatformLocation.TryCreate(
                PlatformLocationKind.FileSystemPath,
                temporaryCachePath,
                isReadable: true,
                isWritable: true,
                intendedForPersistentData: false);
            _streamingContent = CreateStreamingContentLocation(streamingAssetsPath);
        }

        /// <inheritdoc />
        /// <remarks>NoAlloc after construction.</remarks>
        public PlatformLocationResult Resolve(PlatformLocationId id)
        {
            switch (id)
            {
                case PlatformLocationId.PersistentData:
                    return _persistentData;
                case PlatformLocationId.TemporaryCache:
                    return _temporaryCache;
                case PlatformLocationId.StreamingContent:
                    return _streamingContent;
                default:
                    return PlatformLocationResult.Fail(UnknownLocationError);
            }
        }

        internal static PlatformLocationResult CreateStreamingContentLocation(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value))
            {
                return PlatformLocation.TryCreate(
                    PlatformLocationKind.FileSystemPath,
                    value,
                    isReadable: true,
                    isWritable: false,
                    intendedForPersistentData: false);
            }

            if (!string.IsNullOrWhiteSpace(value)
                && Uri.TryCreate(value, UriKind.Absolute, out Uri absoluteUri))
            {
                return PlatformLocation.TryCreate(
                    PlatformLocationKind.Uri,
                    absoluteUri.AbsoluteUri,
                    isReadable: true,
                    isWritable: false,
                    intendedForPersistentData: false);
            }

            return PlatformLocationResult.Fail(new PlatformError(
                PlatformErrorCode.InvalidLocation,
                "Unity streaming content must be an absolute filesystem path or URI."));
        }
    }
}
