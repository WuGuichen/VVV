using System;
using System.Globalization;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Creation-time compatibility identity for the two-result session hash.</summary>
    internal static class CharacterTestSessionHashContract
    {
        internal const int SchemaId = 1001;
        internal const int LayoutVersion = 1;
        internal const ulong DomainSeed = ((ulong)SchemaId << 32) | LayoutVersion;
        internal const string MarkerPrefix = "|ct.session-hash=";
        internal const string RuntimeHashMarkerPrefix = "|ct.runtime-hash=";
        internal const string PlayableCatalogMarkerPrefix = "|ct.playable-catalog=";
        internal static readonly string Marker = MarkerPrefix
            + RuntimeFixedLayoutHash64.AlgorithmId.ToString(CultureInfo.InvariantCulture) + ":"
            + SchemaId.ToString(CultureInfo.InvariantCulture) + ":"
            + LayoutVersion.ToString(CultureInfo.InvariantCulture);
        internal static readonly string RuntimeHashMarker = RuntimeHashMarkerPrefix
            + RuntimeHashAccumulator.EncodingVersion.ToString(CultureInfo.InvariantCulture) + ":"
            + RuntimeHashAccumulator.AlgorithmId.ToString(CultureInfo.InvariantCulture);
        internal static readonly string PlayableCatalogMarker = PlayableCatalogMarkerPrefix
            + CharacterTestAuthoredContentIds.Revision.ToString(CultureInfo.InvariantCulture) + ":"
            + CharacterTestAuthoredContentIds.CatalogHash;

        /// <remarks>Initialization only. Never called by Tick or the fixed hash kernel.</remarks>
        internal static NetworkSessionConfig Normalize(NetworkSessionConfig config)
        {
            string original = config.ConfigHash ?? string.Empty;
            bool hasSessionMarker = original.IndexOf(MarkerPrefix, StringComparison.Ordinal) >= 0;
            bool hasRuntimeHashMarker = original.IndexOf(RuntimeHashMarkerPrefix, StringComparison.Ordinal) >= 0;
            bool hasPlayableCatalogMarker = original.IndexOf(PlayableCatalogMarkerPrefix, StringComparison.Ordinal) >= 0;

            if (hasSessionMarker)
            {
                RejectRepeatedMarker(original, MarkerPrefix, "session hash");
                string expectedSuffix = Marker
                    + (hasRuntimeHashMarker ? RuntimeHashMarker : string.Empty)
                    + (hasPlayableCatalogMarker ? PlayableCatalogMarker : string.Empty);
                bool hasCanonicalSuffix = original.EndsWith(expectedSuffix, StringComparison.Ordinal);
                if (!hasCanonicalSuffix)
                    throw new ArgumentException("CharacterTest session hash algorithm/layout marker is incompatible.", nameof(config));
            }

            if (hasRuntimeHashMarker)
            {
                RejectRepeatedMarker(original, RuntimeHashMarkerPrefix, "runtime hash");
                if (!original.EndsWith(
                        RuntimeHashMarker + (hasPlayableCatalogMarker ? PlayableCatalogMarker : string.Empty),
                        StringComparison.Ordinal))
                    throw new ArgumentException("CharacterTest runtime hash encoding/algorithm marker is incompatible.", nameof(config));
            }

            if (hasPlayableCatalogMarker)
            {
                RejectRepeatedMarker(original, PlayableCatalogMarkerPrefix, "playable catalog");
                if (!original.EndsWith(PlayableCatalogMarker, StringComparison.Ordinal))
                    throw new ArgumentException("CharacterTest playable catalog revision/hash marker is incompatible.", nameof(config));
            }

            if (hasSessionMarker && hasRuntimeHashMarker && hasPlayableCatalogMarker)
            {
                if (!original.EndsWith(Marker + RuntimeHashMarker + PlayableCatalogMarker, StringComparison.Ordinal))
                    throw new ArgumentException("CharacterTest session hash markers must use their canonical order.", nameof(config));
                return config;
            }

            // A runtime marker without the fixed session marker is never a configuration
            // produced by this contract. Reject it instead of silently reordering an
            // externally supplied identity.
            if (hasRuntimeHashMarker && !hasSessionMarker)
                throw new ArgumentException("CharacterTest runtime hash marker requires the fixed session hash marker.", nameof(config));

            if (hasPlayableCatalogMarker && (!hasSessionMarker || !hasRuntimeHashMarker))
                throw new ArgumentException("CharacterTest playable catalog marker requires the fixed session and runtime hash markers.", nameof(config));

            string normalizedHash = original
                + (hasSessionMarker ? string.Empty : Marker)
                + (hasRuntimeHashMarker ? string.Empty : RuntimeHashMarker)
                + (hasPlayableCatalogMarker ? string.Empty : PlayableCatalogMarker);
            return new NetworkSessionConfig(config.ProtocolVersion, config.TickRate, config.InputDelayFrames,
                config.SnapshotIntervalFrames, normalizedHash, config.ResourceCatalogHash,
                config.StateSyncPolicy, config.HeartbeatIntervalFrames);
        }

        private static void RejectRepeatedMarker(string value, string prefix, string markerName)
        {
            int first = value.IndexOf(prefix, StringComparison.Ordinal);
            if (first != value.LastIndexOf(prefix, StringComparison.Ordinal))
                throw new ArgumentException("CharacterTest " + markerName + " marker is repeated.", nameof(value));
        }

        internal static NetworkRoomConfig Normalize(NetworkRoomConfig room)
        {
            var original = room.RoomId == null ? NetworkRoomConfig.Default : room;
            return new NetworkRoomConfig(original.RoomId, original.RoomName, original.MaxMembers,
                original.MinReadyMembers, original.RequireAllReady, original.AllowLateJoin,
                Normalize(original.GameplaySessionConfig));
        }
    }
}
