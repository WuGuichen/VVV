using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeCatalogLoadFormat
    {
        Binary,
        Json,
        Embedded
    }

    internal readonly struct CharacterTestRuntimeCatalogLoadRecord
    {
        public CharacterTestRuntimeCatalogLoadRecord(
            string catalogKind,
            CharacterTestRuntimeCatalogLoadFormat format,
            string sourcePath,
            int entryCount,
            string fallbackReason)
        {
            CatalogKind = catalogKind ?? string.Empty;
            Format = format;
            SourcePath = sourcePath ?? string.Empty;
            EntryCount = entryCount;
            FallbackReason = fallbackReason ?? string.Empty;
        }

        public string CatalogKind { get; }
        public CharacterTestRuntimeCatalogLoadFormat Format { get; }
        public string SourcePath { get; }
        public int EntryCount { get; }
        public string FallbackReason { get; }
    }

    internal static class CharacterTestRuntimeCatalogLoadDiagnostics
    {
        private static readonly Dictionary<string, CharacterTestRuntimeCatalogLoadRecord> Records =
            new Dictionary<string, CharacterTestRuntimeCatalogLoadRecord>(StringComparer.Ordinal);

        /// <remarks>LowFreqAlloc: called only during CharacterTest runtime catalog bootstrap.</remarks>
        public static void RecordBinary(string catalogKind, string sourcePath, int entryCount)
        {
            Record(
                catalogKind,
                CharacterTestRuntimeCatalogLoadFormat.Binary,
                sourcePath,
                entryCount,
                string.Empty);
        }

        /// <remarks>LowFreqAlloc: called only during CharacterTest runtime catalog bootstrap.</remarks>
        public static void RecordJson(string catalogKind, string sourcePath, int entryCount, string fallbackReason)
        {
            Record(
                catalogKind,
                CharacterTestRuntimeCatalogLoadFormat.Json,
                sourcePath,
                entryCount,
                fallbackReason);
        }

        /// <remarks>LowFreqAlloc: called only during CharacterTest runtime catalog bootstrap.</remarks>
        public static void RecordEmbedded(string catalogKind, int entryCount, string fallbackReason)
        {
            Record(
                catalogKind,
                CharacterTestRuntimeCatalogLoadFormat.Embedded,
                "embedded",
                entryCount,
                fallbackReason);
        }

        /// <remarks>AllocByDesign: explicit diagnostics/profile evidence capture for Player reports.</remarks>
        public static string CreateReportText()
        {
            var keys = new List<string>(Records.Keys);
            keys.Sort(StringComparer.Ordinal);
            var builder = new StringBuilder(256 + keys.Count * 160);
            AppendExpectedSummary(builder);
            builder.Append("runtimeCatalogLoad.count: ").AppendLine(keys.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (int i = 0; i < keys.Count; i++)
            {
                CharacterTestRuntimeCatalogLoadRecord record = Records[keys[i]];
                string prefix = "runtimeCatalogLoad." + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                builder.Append(prefix).Append(".kind: ").AppendLine(record.CatalogKind);
                builder.Append(prefix).Append(".format: ").AppendLine(record.Format.ToString());
                builder.Append(prefix).Append(".entryCount: ").AppendLine(
                    record.EntryCount >= 0
                        ? record.EntryCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : "unknown");
                builder.Append(prefix).Append(".sourcePath: ").AppendLine(record.SourcePath);
                if (!string.IsNullOrWhiteSpace(record.FallbackReason))
                    builder.Append(prefix).Append(".fallbackReason: ").AppendLine(record.FallbackReason);
            }

            return builder.ToString();
        }

        private static void AppendExpectedSummary(StringBuilder builder)
        {
            int binaryCount = 0;
            int jsonCount = 0;
            int embeddedCount = 0;
            var missingKinds = new List<string>();
            var nonBinaryKinds = new List<string>();
            IReadOnlyList<CharacterTestRuntimeCatalogArtifact> artifacts =
                CharacterTestRuntimeCatalogArtifactManifest.Artifacts;
            for (int i = 0; i < artifacts.Count; i++)
            {
                CharacterTestRuntimeCatalogArtifact artifact = artifacts[i];
                if (!artifact.RequiresPlayerBinary)
                    continue;

                if (!Records.TryGetValue(artifact.CatalogKind, out CharacterTestRuntimeCatalogLoadRecord record))
                {
                    missingKinds.Add(artifact.CatalogKind);
                    continue;
                }

                if (record.Format == CharacterTestRuntimeCatalogLoadFormat.Binary)
                {
                    binaryCount++;
                    continue;
                }

                if (record.Format == CharacterTestRuntimeCatalogLoadFormat.Json)
                    jsonCount++;
                else if (record.Format == CharacterTestRuntimeCatalogLoadFormat.Embedded)
                    embeddedCount++;

                nonBinaryKinds.Add(artifact.CatalogKind);
            }

            bool allExpectedBinary =
                missingKinds.Count == 0
                && nonBinaryKinds.Count == 0
                && binaryCount == CharacterTestRuntimeCatalogArtifactManifest.RequiredPlayerBinaryCount;
            builder.Append("runtimeCatalogLoad.expectedCount: ")
                .AppendLine(CharacterTestRuntimeCatalogArtifactManifest.RequiredPlayerBinaryCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append("runtimeCatalogLoad.binaryCount: ")
                .AppendLine(binaryCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append("runtimeCatalogLoad.jsonCount: ")
                .AppendLine(jsonCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append("runtimeCatalogLoad.embeddedCount: ")
                .AppendLine(embeddedCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append("runtimeCatalogLoad.missingExpectedKinds: ")
                .AppendLine(JoinKindsOrNone(missingKinds));
            builder.Append("runtimeCatalogLoad.nonBinaryExpectedKinds: ")
                .AppendLine(JoinKindsOrNone(nonBinaryKinds));
            builder.Append("runtimeCatalogLoad.allExpectedBinary: ")
                .AppendLine(allExpectedBinary ? "true" : "false");
        }

        private static string JoinKindsOrNone(List<string> values)
        {
            return values == null || values.Count == 0 ? "none" : string.Join(",", values);
        }

        private static void Record(
            string catalogKind,
            CharacterTestRuntimeCatalogLoadFormat format,
            string sourcePath,
            int entryCount,
            string fallbackReason)
        {
            string safeKind = string.IsNullOrWhiteSpace(catalogKind) ? "unknown" : catalogKind;
            Records[safeKind] = new CharacterTestRuntimeCatalogLoadRecord(
                safeKind,
                format,
                sourcePath,
                entryCount,
                fallbackReason);
        }
    }

    internal static class CharacterTestRuntimeCatalogBinaryFallback
    {
        public static string ToBinaryPath(string jsonPath)
        {
            return string.IsNullOrWhiteSpace(jsonPath)
                ? string.Empty
                : Path.ChangeExtension(jsonPath, ".bytes");
        }

        /// <remarks>LowFreqAlloc: CharacterTest runtime catalog bootstrap helper.</remarks>
        public static TCatalog LoadBinaryThenJsonOrEmpty<TCatalog>(
            string generatedJsonPath,
            string catalogKind,
            Func<string, RuntimeCatalogBinaryLoadResult<TCatalog>> binaryImporter,
            Func<string, TCatalog> jsonImporter,
            Func<TCatalog, int> entryCountSelector,
            Func<TCatalog> emptyFactory,
            bool acceptEmptyBinary = false)
        {
            if (string.IsNullOrWhiteSpace(generatedJsonPath))
                return CreateEmpty(emptyFactory);

            if (binaryImporter == null)
                throw new ArgumentNullException(nameof(binaryImporter));
            if (jsonImporter == null)
                throw new ArgumentNullException(nameof(jsonImporter));
            if (entryCountSelector == null)
                throw new ArgumentNullException(nameof(entryCountSelector));

            CharacterTestRuntimeCatalogPathCandidate[] candidates =
                CharacterTestRuntimeCatalogPathResolver.ResolveCandidates(generatedJsonPath);
            string fallbackReason = string.Empty;
            for (int i = 0; i < candidates.Length; i++)
            {
                CharacterTestRuntimeCatalogPathCandidate candidate = candidates[i];
                RuntimeCatalogBinaryLoadResult<TCatalog> binary = binaryImporter(candidate.BinaryPath);
                if (binary.Success && (acceptEmptyBinary || entryCountSelector(binary.Catalog) > 0))
                {
                    CharacterTestRuntimeCatalogLoadDiagnostics.RecordBinary(
                        catalogKind,
                        candidate.BinaryPath,
                        binary.Header.EntryCount >= 0
                            ? binary.Header.EntryCount
                            : entryCountSelector(binary.Catalog));
                    return binary.Catalog;
                }

                Report(candidate.BinaryPath, binary);
                if (binary != null
                    && !string.IsNullOrWhiteSpace(binary.FallbackReason)
                    && !string.Equals(binary.FallbackReason, "MissingFile", StringComparison.Ordinal))
                {
                    fallbackReason = binary.FallbackReason;
                }

                if (!File.Exists(candidate.JsonPath))
                    continue;

                TCatalog json = jsonImporter(candidate.JsonPath);
                CharacterTestRuntimeCatalogLoadDiagnostics.RecordJson(
                    catalogKind,
                    candidate.JsonPath,
                    entryCountSelector(json),
                    string.IsNullOrWhiteSpace(fallbackReason) && binary != null
                        ? binary.FallbackReason
                        : fallbackReason);
                return json;
            }

            return CreateEmpty(emptyFactory);
        }

        public static void Report<TCatalog>(
            string binaryPath,
            RuntimeCatalogBinaryLoadResult<TCatalog> result)
        {
            if (string.IsNullOrWhiteSpace(binaryPath)
                || !File.Exists(binaryPath)
                || result == null
                || result.Success
                || string.Equals(result.FallbackReason, "MissingFile", System.StringComparison.Ordinal))
            {
                return;
            }

            string diagnostic = result.Diagnostics.Count > 0
                ? result.Diagnostics[0]
                : result.FallbackReason;
            Debug.LogWarning(
                "CharacterTest runtime catalog binary fallback: "
                + binaryPath
                + " reason="
                + result.FallbackReason
                + " diagnostic="
                + diagnostic);
        }

        private static TCatalog CreateEmpty<TCatalog>(Func<TCatalog> emptyFactory)
        {
            return emptyFactory == null ? default : emptyFactory();
        }
    }
}
