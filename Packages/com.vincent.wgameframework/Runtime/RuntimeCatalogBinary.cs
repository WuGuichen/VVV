using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace MxFramework.Runtime
{
    /// <summary>
    /// Header for versioned runtime catalog binary artifacts.
    /// </summary>
    /// <remarks>LowFreqAlloc during catalog load; not used by Tick/Execute paths.</remarks>
    public readonly struct RuntimeCatalogBinaryHeader
    {
        public RuntimeCatalogBinaryHeader(
            int magic,
            int formatVersion,
            string catalogKind,
            string schemaVersion,
            string sourcePackageId,
            int entryCount,
            long stableHash)
        {
            Magic = magic;
            FormatVersion = formatVersion;
            CatalogKind = catalogKind ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            SourcePackageId = sourcePackageId ?? string.Empty;
            EntryCount = entryCount;
            StableHash = stableHash;
        }

        public int Magic { get; }
        public int FormatVersion { get; }
        public string CatalogKind { get; }
        public string SchemaVersion { get; }
        public string SourcePackageId { get; }
        public int EntryCount { get; }
        public long StableHash { get; }
    }

    /// <summary>
    /// Structured result for runtime catalog binary loading.
    /// </summary>
    /// <remarks>LowFreqAlloc during catalog load; diagnostics allocate only on load/fallback paths.</remarks>
    public sealed class RuntimeCatalogBinaryLoadResult<TCatalog>
    {
        private RuntimeCatalogBinaryLoadResult(
            bool success,
            RuntimeCatalogBinaryHeader header,
            TCatalog catalog,
            string fallbackReason,
            IReadOnlyList<string> diagnostics)
        {
            Success = success;
            Header = header;
            Catalog = catalog;
            FallbackReason = fallbackReason ?? string.Empty;
            Diagnostics = diagnostics ?? EmptyDiagnostics.Instance;
        }

        public bool Success { get; }
        public RuntimeCatalogBinaryHeader Header { get; }
        public TCatalog Catalog { get; }
        public string FallbackReason { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public static RuntimeCatalogBinaryLoadResult<TCatalog> Succeeded(
            TCatalog catalog,
            RuntimeCatalogBinaryHeader header)
        {
            return new RuntimeCatalogBinaryLoadResult<TCatalog>(
                true,
                header,
                catalog,
                string.Empty,
                EmptyDiagnostics.Instance);
        }

        public static RuntimeCatalogBinaryLoadResult<TCatalog> Failed(
            string fallbackReason,
            params string[] diagnostics)
        {
            IReadOnlyList<string> safeDiagnostics =
                diagnostics == null || diagnostics.Length == 0
                    ? EmptyDiagnostics.Instance
                    : new ReadOnlyCollection<string>(new List<string>(diagnostics));
            return new RuntimeCatalogBinaryLoadResult<TCatalog>(
                false,
                default,
                default,
                fallbackReason,
                safeDiagnostics);
        }

        private static class EmptyDiagnostics
        {
            public static readonly IReadOnlyList<string> Instance =
                new ReadOnlyCollection<string>(new List<string>(0));
        }
    }

    public delegate TCatalog RuntimeCatalogBinaryPayloadReader<TCatalog>(
        BinaryReader reader,
        RuntimeCatalogBinaryHeader header,
        string sourcePath,
        out int loadedEntryCount);

    public static class RuntimeCatalogBinary
    {
        public const int Magic = 0x4D584342; // MXCB
        public const int CurrentFormatVersion = 1;

        public static RuntimeCatalogBinaryLoadResult<TCatalog> Import<TCatalog>(
            byte[] bytes,
            string expectedCatalogKind,
            string expectedSchemaVersion,
            RuntimeCatalogBinaryPayloadReader<TCatalog> payloadReader,
            string sourcePath = "")
        {
            if (bytes == null || bytes.Length == 0)
            {
                return RuntimeCatalogBinaryLoadResult<TCatalog>.Failed(
                    "MissingBytes",
                    "Runtime catalog binary bytes are missing.");
            }

            if (payloadReader == null)
            {
                return RuntimeCatalogBinaryLoadResult<TCatalog>.Failed(
                    "InvalidImporter",
                    "Runtime catalog binary payload reader is missing.");
            }

            try
            {
                using (var stream = new MemoryStream(bytes, writable: false))
                using (var reader = new BinaryReader(stream))
                {
                    RuntimeCatalogBinaryHeader header = ReadHeader(reader);
                    string headerDiagnostic = ValidateHeader(
                        header,
                        expectedCatalogKind,
                        expectedSchemaVersion,
                        sourcePath);
                    if (!string.IsNullOrEmpty(headerDiagnostic))
                    {
                        return RuntimeCatalogBinaryLoadResult<TCatalog>.Failed(
                            "InvalidHeader",
                            headerDiagnostic);
                    }

                    int payloadOffset = checked((int)stream.Position);
                    long actualHash = ComputeStableHash(bytes, payloadOffset, bytes.Length - payloadOffset);
                    if (actualHash != header.StableHash)
                    {
                        return RuntimeCatalogBinaryLoadResult<TCatalog>.Failed(
                            "StableHashMismatch",
                            "Runtime catalog binary stableHash mismatch at "
                            + (sourcePath ?? string.Empty)
                            + ".");
                    }

                    int loadedEntryCount;
                    TCatalog catalog = payloadReader(reader, header, sourcePath ?? string.Empty, out loadedEntryCount);
                    if (loadedEntryCount != header.EntryCount)
                    {
                        return RuntimeCatalogBinaryLoadResult<TCatalog>.Failed(
                            "EntryCountMismatch",
                            "Runtime catalog binary entryCount mismatch at "
                            + (sourcePath ?? string.Empty)
                            + ": header="
                            + header.EntryCount
                            + " loaded="
                            + loadedEntryCount
                            + ".");
                    }

                    if (stream.Position != stream.Length)
                    {
                        return RuntimeCatalogBinaryLoadResult<TCatalog>.Failed(
                            "TrailingBytes",
                            "Runtime catalog binary contains trailing bytes at "
                            + (sourcePath ?? string.Empty)
                            + ".");
                    }

                    return RuntimeCatalogBinaryLoadResult<TCatalog>.Succeeded(catalog, header);
                }
            }
            catch (EndOfStreamException ex)
            {
                return RuntimeCatalogBinaryLoadResult<TCatalog>.Failed(
                    "TruncatedFile",
                    "Runtime catalog binary is truncated at "
                    + (sourcePath ?? string.Empty)
                    + ": "
                    + ex.Message);
            }
            catch (Exception ex)
            {
                return RuntimeCatalogBinaryLoadResult<TCatalog>.Failed(
                    "InvalidPayload",
                    "Runtime catalog binary could not be loaded at "
                    + (sourcePath ?? string.Empty)
                    + ": "
                    + ex.Message);
            }
        }

        public static RuntimeCatalogBinaryHeader ReadHeader(BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            return new RuntimeCatalogBinaryHeader(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadInt32(),
                reader.ReadInt64());
        }

        public static void WriteHeader(
            BinaryWriter writer,
            string catalogKind,
            string schemaVersion,
            string sourcePackageId,
            int entryCount,
            long stableHash)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            writer.Write(Magic);
            writer.Write(CurrentFormatVersion);
            writer.Write(catalogKind ?? string.Empty);
            writer.Write(schemaVersion ?? string.Empty);
            writer.Write(sourcePackageId ?? string.Empty);
            writer.Write(entryCount);
            writer.Write(stableHash);
        }

        public static long ComputeStableHash(byte[] bytes, int startIndex, int count)
        {
            if (bytes == null)
                return 0L;
            if (startIndex < 0 || count < 0 || startIndex + count > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offsetBasis;
                int end = startIndex + count;
                for (int i = startIndex; i < end; i++)
                {
                    hash ^= bytes[i];
                    hash *= prime;
                }

                return (long)hash;
            }
        }

        private static string ValidateHeader(
            RuntimeCatalogBinaryHeader header,
            string expectedCatalogKind,
            string expectedSchemaVersion,
            string sourcePath)
        {
            if (header.Magic != Magic)
                return "Runtime catalog binary magic mismatch at " + (sourcePath ?? string.Empty) + ".";
            if (header.FormatVersion != CurrentFormatVersion)
                return "Runtime catalog binary formatVersion mismatch at " + (sourcePath ?? string.Empty) + ".";
            if (!string.Equals(header.CatalogKind, expectedCatalogKind ?? string.Empty, StringComparison.Ordinal))
                return "Runtime catalog binary catalogKind mismatch at " + (sourcePath ?? string.Empty) + ".";
            if (!string.Equals(header.SchemaVersion, expectedSchemaVersion ?? string.Empty, StringComparison.Ordinal))
                return "Runtime catalog binary schemaVersion mismatch at " + (sourcePath ?? string.Empty) + ".";
            if (header.EntryCount < 0)
                return "Runtime catalog binary entryCount is negative at " + (sourcePath ?? string.Empty) + ".";
            return string.Empty;
        }
    }

    public static class RuntimeCatalogBinaryKinds
    {
        public const string AbilityDefinitions = "mxframework.gameplay.ability.definitions";
        public const string AbilityEffectTimelines = "mxframework.gameplay.ability.effect-timelines";
        public const string GameplayBuffDefinitions = "mxframework.gameplay.buff.definitions";
        public const string GameplayBuffPresentationDefinitions = "mxframework.gameplay.buff.presentations";
        public const string CombatGameplayAuraBuffDefinitions = "mxframework.combat.gameplay.aura-buffs";
        public const string DamageProfiles = "mxframework.combat.damage.profiles";
        public const string ProjectileDefinitions = "mxframework.combat.projectile.definitions";
        public const string ProjectileSpawnProfiles = "mxframework.combat.projectile.spawn-profiles";
        public const string ProjectilePresentationBindings = "mxframework.combat.projectile.presentation-bindings";
        public const string VfxDefinitions = "mxframework.vfx.definitions";
        public const string VfxTriggerMaps = "mxframework.vfx.trigger-maps";
    }
}
