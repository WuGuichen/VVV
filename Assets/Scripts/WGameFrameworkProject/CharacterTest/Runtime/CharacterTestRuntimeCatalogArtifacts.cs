using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeCatalogArtifact
    {
        public CharacterTestRuntimeCatalogArtifact(
            string catalogKind,
            string generatedJsonPath,
            string playerMirrorRelativeJsonPath,
            bool requiresPlayerBinary)
        {
            CatalogKind = catalogKind ?? string.Empty;
            GeneratedJsonPath = CharacterTestRuntimeCatalogArtifactManifest.NormalizeProjectPath(generatedJsonPath);
            GeneratedBinaryPath = CharacterTestRuntimeCatalogArtifactManifest.ChangeExtension(
                GeneratedJsonPath,
                ".bytes");
            PlayerMirrorRelativeJsonPath =
                CharacterTestRuntimeCatalogArtifactManifest.NormalizeProjectPath(playerMirrorRelativeJsonPath);
            PlayerMirrorRelativeBinaryPath = CharacterTestRuntimeCatalogArtifactManifest.ChangeExtension(
                PlayerMirrorRelativeJsonPath,
                ".bytes");
            RequiresPlayerBinary = requiresPlayerBinary;
        }

        public string CatalogKind { get; }
        public string GeneratedJsonPath { get; }
        public string GeneratedBinaryPath { get; }
        public string PlayerMirrorRelativeJsonPath { get; }
        public string PlayerMirrorRelativeBinaryPath { get; }
        public bool RequiresPlayerBinary { get; }
    }

    internal static class CharacterTestRuntimeCatalogArtifactManifest
    {
        public const string GeneratedRootPath = "Assets/MxFrameworkGenerated";
        public const string StreamingAssetsMirrorRootRelativePath =
            "MxFramework/CharacterTest/RuntimeCatalogs";

        private static readonly CharacterTestRuntimeCatalogArtifact[] ArtifactEntries =
        {
            Entry(RuntimeCatalogBinaryKinds.AbilityDefinitions, "Abilities/ability_catalog.json"),
            Entry(RuntimeCatalogBinaryKinds.AbilityEffectTimelines, "Abilities/ability_effect_timelines.json"),
            Entry(RuntimeCatalogBinaryKinds.GameplayBuffDefinitions, "Gameplay/Buffs/buff_definitions.json"),
            Entry(RuntimeCatalogBinaryKinds.GameplayBuffPresentationDefinitions, "Gameplay/Buffs/buff_presentation_definitions.json"),
            Entry(RuntimeCatalogBinaryKinds.CombatGameplayAuraBuffDefinitions, "Combat/Auras/aura_buff_definitions.json"),
            Entry(RuntimeCatalogBinaryKinds.ProjectileDefinitions, "Combat/Projectiles/projectile_definitions.json"),
            Entry(RuntimeCatalogBinaryKinds.ProjectileSpawnProfiles, "Combat/Projectiles/projectile_spawn_profiles.json"),
            Entry(RuntimeCatalogBinaryKinds.ProjectilePresentationBindings, "Combat/Projectiles/projectile_presentation_bindings.json"),
            Entry(RuntimeCatalogBinaryKinds.VfxDefinitions, "Vfx/vfx_definitions.json"),
            Entry(RuntimeCatalogBinaryKinds.VfxTriggerMaps, "Vfx/vfx_trigger_maps.json"),
            new CharacterTestRuntimeCatalogArtifact(
                CharacterTestWorldObjectDefinitionFormats.RuntimeCatalogKind,
                "Assets/MxFrameworkGenerated/Levels/CharacterTest/world_object_definitions.runtime.json",
                "Levels/world_object_definitions.runtime.json",
                requiresPlayerBinary: false),
            JsonOnlyEntry("mxframework.characterTest.mechanicTunings", "Abilities/character_test_mechanic_tunings.json")
        };

        public static IReadOnlyList<CharacterTestRuntimeCatalogArtifact> Artifacts => ArtifactEntries;

        public static int RequiredPlayerBinaryCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < ArtifactEntries.Length; i++)
                {
                    if (ArtifactEntries[i].RequiresPlayerBinary)
                        count++;
                }

                return count;
            }
        }

        public static bool TryGetByCatalogKind(
            string catalogKind,
            out CharacterTestRuntimeCatalogArtifact artifact)
        {
            for (int i = 0; i < ArtifactEntries.Length; i++)
            {
                if (string.Equals(ArtifactEntries[i].CatalogKind, catalogKind, StringComparison.Ordinal))
                {
                    artifact = ArtifactEntries[i];
                    return true;
                }
            }

            artifact = default;
            return false;
        }

        public static bool TryGetByGeneratedJsonPath(
            string generatedJsonPath,
            out CharacterTestRuntimeCatalogArtifact artifact)
        {
            string normalizedPath = NormalizeGeneratedPathForLookup(generatedJsonPath);
            for (int i = 0; i < ArtifactEntries.Length; i++)
            {
                if (string.Equals(ArtifactEntries[i].GeneratedJsonPath, normalizedPath, StringComparison.Ordinal))
                {
                    artifact = ArtifactEntries[i];
                    return true;
                }
            }

            artifact = default;
            return false;
        }

        public static string NormalizeProjectPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimStart('/');
        }

        public static string ChangeExtension(string path, string extension)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return Path.ChangeExtension(path, extension).Replace('\\', '/');
        }

        private static CharacterTestRuntimeCatalogArtifact Entry(string catalogKind, string generatedRelativePath)
        {
            string generatedPath = GeneratedRootPath + "/" + NormalizeProjectPath(generatedRelativePath);
            string mirrorPath = StreamingAssetsMirrorRootRelativePath + "/" + NormalizeProjectPath(generatedRelativePath);
            return new CharacterTestRuntimeCatalogArtifact(
                catalogKind,
                generatedPath,
                mirrorPath,
                requiresPlayerBinary: true);
        }

        private static CharacterTestRuntimeCatalogArtifact JsonOnlyEntry(
            string catalogKind,
            string generatedRelativePath)
        {
            string generatedPath = GeneratedRootPath + "/" + NormalizeProjectPath(generatedRelativePath);
            string mirrorPath = StreamingAssetsMirrorRootRelativePath + "/" + NormalizeProjectPath(generatedRelativePath);
            return new CharacterTestRuntimeCatalogArtifact(
                catalogKind,
                generatedPath,
                mirrorPath,
                requiresPlayerBinary: false);
        }

        private static string NormalizeGeneratedPathForLookup(string path)
        {
            string normalized = NormalizeProjectPath(path);
            int generatedRootIndex = normalized.IndexOf(
                GeneratedRootPath + "/",
                StringComparison.Ordinal);
            return generatedRootIndex >= 0
                ? normalized.Substring(generatedRootIndex)
                : normalized;
        }
    }

    internal readonly struct CharacterTestRuntimeCatalogPathResolverContext
    {
        public CharacterTestRuntimeCatalogPathResolverContext(
            bool preferPlayerMirror,
            string projectRootPath,
            string streamingAssetsPath)
        {
            PreferPlayerMirror = preferPlayerMirror;
            ProjectRootPath = projectRootPath ?? string.Empty;
            StreamingAssetsPath = streamingAssetsPath ?? string.Empty;
        }

        public bool PreferPlayerMirror { get; }
        public string ProjectRootPath { get; }
        public string StreamingAssetsPath { get; }

        public static CharacterTestRuntimeCatalogPathResolverContext CreateDefault()
        {
            return new CharacterTestRuntimeCatalogPathResolverContext(
                preferPlayerMirror: !Application.isEditor,
                projectRootPath: Directory.GetCurrentDirectory(),
                streamingAssetsPath: Application.streamingAssetsPath);
        }
    }

    internal readonly struct CharacterTestRuntimeCatalogPathCandidate
    {
        public CharacterTestRuntimeCatalogPathCandidate(
            string jsonPath,
            string binaryPath,
            string sourceKind)
        {
            JsonPath = jsonPath ?? string.Empty;
            BinaryPath = binaryPath ?? string.Empty;
            SourceKind = sourceKind ?? string.Empty;
        }

        public string JsonPath { get; }
        public string BinaryPath { get; }
        public string SourceKind { get; }
    }

    internal static class CharacterTestRuntimeCatalogPathResolver
    {
        /// <remarks>LowFreqAlloc: used only during CharacterTest runtime catalog bootstrap.</remarks>
        public static CharacterTestRuntimeCatalogPathCandidate[] ResolveCandidates(string generatedJsonPath)
        {
            return ResolveCandidates(
                generatedJsonPath,
                CharacterTestRuntimeCatalogPathResolverContext.CreateDefault());
        }

        /// <remarks>AllocByDesign: test/editor helper for explicit path resolution checks.</remarks>
        public static CharacterTestRuntimeCatalogPathCandidate[] ResolveCandidates(
            string generatedJsonPath,
            CharacterTestRuntimeCatalogPathResolverContext context)
        {
            if (string.IsNullOrWhiteSpace(generatedJsonPath))
                return Array.Empty<CharacterTestRuntimeCatalogPathCandidate>();

            if (!CharacterTestRuntimeCatalogArtifactManifest.TryGetByGeneratedJsonPath(
                    generatedJsonPath,
                    out CharacterTestRuntimeCatalogArtifact artifact))
            {
                string resolvedPath = ResolvePath(context.ProjectRootPath, generatedJsonPath);
                return new[]
                {
                    new CharacterTestRuntimeCatalogPathCandidate(
                        resolvedPath,
                        CharacterTestRuntimeCatalogArtifactManifest.ChangeExtension(resolvedPath, ".bytes"),
                        "generated")
                };
            }

            CharacterTestRuntimeCatalogPathCandidate generated = CreateGeneratedCandidate(
                artifact,
                context.ProjectRootPath);
            CharacterTestRuntimeCatalogPathCandidate mirror = CreateMirrorCandidate(
                artifact,
                context.StreamingAssetsPath);

            return context.PreferPlayerMirror
                ? new[] { mirror, generated }
                : new[] { generated, mirror };
        }

        public static string ResolveStreamingAssetsMirrorPath(
            string playerMirrorRelativePath,
            string streamingAssetsPath)
        {
            return ResolvePath(streamingAssetsPath, playerMirrorRelativePath);
        }

        public static string ResolveProjectPath(string projectRelativePath, string projectRootPath)
        {
            return ResolvePath(projectRootPath, projectRelativePath);
        }

        private static CharacterTestRuntimeCatalogPathCandidate CreateGeneratedCandidate(
            CharacterTestRuntimeCatalogArtifact artifact,
            string projectRootPath)
        {
            string jsonPath = ResolvePath(projectRootPath, artifact.GeneratedJsonPath);
            string binaryPath = ResolvePath(projectRootPath, artifact.GeneratedBinaryPath);
            return new CharacterTestRuntimeCatalogPathCandidate(jsonPath, binaryPath, "generated");
        }

        private static CharacterTestRuntimeCatalogPathCandidate CreateMirrorCandidate(
            CharacterTestRuntimeCatalogArtifact artifact,
            string streamingAssetsPath)
        {
            string jsonPath = ResolvePath(streamingAssetsPath, artifact.PlayerMirrorRelativeJsonPath);
            string binaryPath = ResolvePath(streamingAssetsPath, artifact.PlayerMirrorRelativeBinaryPath);
            return new CharacterTestRuntimeCatalogPathCandidate(jsonPath, binaryPath, "playerMirror");
        }

        private static string ResolvePath(string rootPath, string relativeOrAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
                return string.Empty;

            if (Path.IsPathRooted(relativeOrAbsolutePath) || string.IsNullOrWhiteSpace(rootPath))
                return relativeOrAbsolutePath;

            return Path.GetFullPath(Path.Combine(rootPath, relativeOrAbsolutePath));
        }
    }

    internal readonly struct CharacterTestRuntimeCatalogLoadReportSummary
    {
        public CharacterTestRuntimeCatalogLoadReportSummary(
            int expectedCount,
            int binaryCount,
            int jsonCount,
            int embeddedCount,
            string[] missingExpectedKinds,
            string[] nonBinaryExpectedKinds,
            bool allExpectedBinary)
        {
            ExpectedCount = expectedCount;
            BinaryCount = binaryCount;
            JsonCount = jsonCount;
            EmbeddedCount = embeddedCount;
            MissingExpectedKinds = missingExpectedKinds ?? Array.Empty<string>();
            NonBinaryExpectedKinds = nonBinaryExpectedKinds ?? Array.Empty<string>();
            AllExpectedBinary = allExpectedBinary;
        }

        public int ExpectedCount { get; }
        public int BinaryCount { get; }
        public int JsonCount { get; }
        public int EmbeddedCount { get; }
        public string[] MissingExpectedKinds { get; }
        public string[] NonBinaryExpectedKinds { get; }
        public bool AllExpectedBinary { get; }
    }

    internal static class CharacterTestRuntimeCatalogLoadReportParser
    {
        // Packaging is deliberately stricter than the diagnostic parser below: a Player
        // report must prove each manifest-required load, not merely claim a good summary.
        public static CharacterTestRuntimeCatalogLoadReportSummary ParseStrict(string reportText)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var reader = new StringReader(reportText ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int separator = line.IndexOf(':');
                    if (separator <= 0)
                        continue;
                    string key = line.Substring(0, separator).Trim();
                    if (!key.StartsWith("runtimeCatalogLoad.", StringComparison.Ordinal))
                        continue;
                    if (!fields.TryAdd(key, line.Substring(separator + 1).Trim()))
                        throw new FormatException("Duplicate runtime catalog report field: " + key);
                }
            }

            int reportedCount = RequiredNonNegativeInt(fields, "runtimeCatalogLoad.count");
            int expected = RequiredNonNegativeInt(fields, "runtimeCatalogLoad.expectedCount");
            int binary = RequiredNonNegativeInt(fields, "runtimeCatalogLoad.binaryCount");
            int json = RequiredNonNegativeInt(fields, "runtimeCatalogLoad.jsonCount");
            int embedded = RequiredNonNegativeInt(fields, "runtimeCatalogLoad.embeddedCount");
            string[] reportedMissing = RequiredList(fields, "runtimeCatalogLoad.missingExpectedKinds");
            string[] reportedNonBinary = RequiredList(fields, "runtimeCatalogLoad.nonBinaryExpectedKinds");
            if (!fields.TryGetValue("runtimeCatalogLoad.allExpectedBinary", out string allText)
                || !bool.TryParse(allText, out bool reportedAll))
                throw new FormatException("Missing or invalid runtimeCatalogLoad.allExpectedBinary.");

            var kindsByIndex = new Dictionary<int, string>();
            var formatsByIndex = new Dictionary<int, string>();
            foreach (KeyValuePair<string, string> field in fields)
            {
                if (TryParseRecordKey(field.Key, ".kind", out int kindIndex))
                {
                    if (kindIndex < 0 || string.IsNullOrWhiteSpace(field.Value))
                        throw new FormatException("Invalid runtime catalog kind field: " + field.Key);
                    kindsByIndex.Add(kindIndex, field.Value);
                }
                else if (TryParseRecordKey(field.Key, ".format", out int formatIndex))
                {
                    if (formatIndex < 0 || (field.Value != "Binary" && field.Value != "Json" && field.Value != "Embedded"))
                        throw new FormatException("Invalid runtime catalog format field: " + field.Key);
                    formatsByIndex.Add(formatIndex, field.Value);
                }
            }
            if (reportedCount != kindsByIndex.Count || reportedCount != formatsByIndex.Count)
                throw new FormatException("Runtime catalog record count or kind/format fields are inconsistent.");

            var records = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < reportedCount; i++)
            {
                if (!kindsByIndex.TryGetValue(i, out string kind)
                    || !formatsByIndex.TryGetValue(i, out string format))
                    throw new FormatException("Incomplete runtime catalog record at index " + i + ".");
                if (!records.TryAdd(kind, format))
                    throw new FormatException("Duplicate runtime catalog kind: " + kind);
            }

            var missing = new List<string>();
            var nonBinary = new List<string>();
            int computedBinary = 0, computedJson = 0, computedEmbedded = 0;
            IReadOnlyList<CharacterTestRuntimeCatalogArtifact> artifacts = CharacterTestRuntimeCatalogArtifactManifest.Artifacts;
            for (int i = 0; i < artifacts.Count; i++)
            {
                if (!artifacts[i].RequiresPlayerBinary)
                    continue;
                string kind = artifacts[i].CatalogKind;
                if (!records.TryGetValue(kind, out string format))
                {
                    missing.Add(kind);
                    continue;
                }
                if (format == "Binary") computedBinary++;
                else
                {
                    nonBinary.Add(kind);
                    if (format == "Json") computedJson++;
                    else computedEmbedded++;
                }
            }
            bool computedAll = missing.Count == 0 && nonBinary.Count == 0;
            if (expected != CharacterTestRuntimeCatalogArtifactManifest.RequiredPlayerBinaryCount
                || binary != computedBinary || json != computedJson || embedded != computedEmbedded
                || reportedAll != computedAll
                || !SameSet(reportedMissing, missing) || !SameSet(reportedNonBinary, nonBinary))
                throw new FormatException("Runtime catalog summary contradicts manifest or individual records. missing="
                    + string.Join(",", missing) + " nonBinary=" + string.Join(",", nonBinary));
            if (!computedAll)
                throw new FormatException("Expected Binary runtime catalogs. missing=" + string.Join(",", missing)
                    + " nonBinary=" + string.Join(",", nonBinary));
            return new CharacterTestRuntimeCatalogLoadReportSummary(expected, computedBinary, computedJson,
                computedEmbedded, missing.ToArray(), nonBinary.ToArray(), computedAll);
        }

        private static int RequiredNonNegativeInt(Dictionary<string, string> fields, string key)
        {
            if (!fields.TryGetValue(key, out string value)
                || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                || parsed < 0)
                throw new FormatException("Missing or invalid " + key + ".");
            return parsed;
        }

        private static string[] RequiredList(Dictionary<string, string> fields, string key)
        {
            if (!fields.TryGetValue(key, out string value))
                throw new FormatException("Missing " + key + ".");
            return ParseList(value);
        }

        private static bool SameSet(string[] reported, List<string> computed)
        {
            var values = new HashSet<string>(reported, StringComparer.Ordinal);
            return values.Count == reported.Length && values.SetEquals(computed);
        }

        /// <remarks>AllocByDesign: editor/test-only report gate parsing.</remarks>
        public static CharacterTestRuntimeCatalogLoadReportSummary Parse(string reportText)
        {
            int expectedCount = -1;
            int binaryCount = -1;
            int jsonCount = -1;
            int embeddedCount = -1;
            bool? allExpectedBinary = null;
            string[] missingExpectedKinds = null;
            string[] nonBinaryExpectedKinds = null;
            var formatsByKind = new Dictionary<string, string>(StringComparer.Ordinal);
            var pendingKindsByIndex = new Dictionary<int, string>();
            var pendingFormatsByIndex = new Dictionary<int, string>();

            using (var reader = new StringReader(reportText ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int separatorIndex = line.IndexOf(':');
                    if (separatorIndex <= 0)
                        continue;

                    string key = line.Substring(0, separatorIndex).Trim();
                    string value = line.Substring(separatorIndex + 1).Trim();
                    if (string.Equals(key, "runtimeCatalogLoad.expectedCount", StringComparison.Ordinal))
                        expectedCount = ParseInt(value);
                    else if (string.Equals(key, "runtimeCatalogLoad.binaryCount", StringComparison.Ordinal))
                        binaryCount = ParseInt(value);
                    else if (string.Equals(key, "runtimeCatalogLoad.jsonCount", StringComparison.Ordinal))
                        jsonCount = ParseInt(value);
                    else if (string.Equals(key, "runtimeCatalogLoad.embeddedCount", StringComparison.Ordinal))
                        embeddedCount = ParseInt(value);
                    else if (string.Equals(key, "runtimeCatalogLoad.allExpectedBinary", StringComparison.Ordinal))
                        allExpectedBinary = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    else if (string.Equals(key, "runtimeCatalogLoad.missingExpectedKinds", StringComparison.Ordinal))
                        missingExpectedKinds = ParseList(value);
                    else if (string.Equals(key, "runtimeCatalogLoad.nonBinaryExpectedKinds", StringComparison.Ordinal))
                        nonBinaryExpectedKinds = ParseList(value);
                    else if (TryParseRecordKey(key, ".kind", out int kindIndex))
                        pendingKindsByIndex[kindIndex] = value;
                    else if (TryParseRecordKey(key, ".format", out int formatIndex))
                        pendingFormatsByIndex[formatIndex] = value;
                }
            }

            foreach (KeyValuePair<int, string> pair in pendingKindsByIndex)
            {
                if (pendingFormatsByIndex.TryGetValue(pair.Key, out string format))
                    formatsByKind[pair.Value] = format;
            }

            return BuildSummary(
                formatsByKind,
                expectedCount,
                binaryCount,
                jsonCount,
                embeddedCount,
                missingExpectedKinds,
                nonBinaryExpectedKinds,
                allExpectedBinary);
        }

        private static CharacterTestRuntimeCatalogLoadReportSummary BuildSummary(
            Dictionary<string, string> formatsByKind,
            int expectedCount,
            int binaryCount,
            int jsonCount,
            int embeddedCount,
            string[] missingExpectedKinds,
            string[] nonBinaryExpectedKinds,
            bool? allExpectedBinary)
        {
            var missing = missingExpectedKinds == null
                ? new List<string>()
                : new List<string>(missingExpectedKinds);
            var nonBinary = nonBinaryExpectedKinds == null
                ? new List<string>()
                : new List<string>(nonBinaryExpectedKinds);

            int computedBinary = 0;
            int computedJson = 0;
            int computedEmbedded = 0;
            IReadOnlyList<CharacterTestRuntimeCatalogArtifact> artifacts =
                CharacterTestRuntimeCatalogArtifactManifest.Artifacts;
            for (int i = 0; i < artifacts.Count; i++)
            {
                CharacterTestRuntimeCatalogArtifact artifact = artifacts[i];
                if (!artifact.RequiresPlayerBinary)
                    continue;

                string kind = artifact.CatalogKind;
                if (!formatsByKind.TryGetValue(kind, out string format))
                {
                    if (missingExpectedKinds == null)
                        missing.Add(kind);
                    continue;
                }

                if (string.Equals(format, CharacterTestRuntimeCatalogLoadFormat.Binary.ToString(), StringComparison.Ordinal))
                {
                    computedBinary++;
                    continue;
                }

                if (string.Equals(format, CharacterTestRuntimeCatalogLoadFormat.Json.ToString(), StringComparison.Ordinal))
                    computedJson++;
                else if (string.Equals(format, CharacterTestRuntimeCatalogLoadFormat.Embedded.ToString(), StringComparison.Ordinal))
                    computedEmbedded++;

                if (nonBinaryExpectedKinds == null)
                    nonBinary.Add(kind);
            }

            int safeExpectedCount = expectedCount >= 0
                ? expectedCount
                : CharacterTestRuntimeCatalogArtifactManifest.RequiredPlayerBinaryCount;
            int safeBinaryCount = binaryCount >= 0 ? binaryCount : computedBinary;
            int safeJsonCount = jsonCount >= 0 ? jsonCount : computedJson;
            int safeEmbeddedCount = embeddedCount >= 0 ? embeddedCount : computedEmbedded;
            bool safeAllExpectedBinary = allExpectedBinary.HasValue
                ? allExpectedBinary.Value
                : missing.Count == 0 && nonBinary.Count == 0 && safeBinaryCount == safeExpectedCount;

            return new CharacterTestRuntimeCatalogLoadReportSummary(
                safeExpectedCount,
                safeBinaryCount,
                safeJsonCount,
                safeEmbeddedCount,
                missing.ToArray(),
                nonBinary.ToArray(),
                safeAllExpectedBinary);
        }

        private static bool TryParseRecordKey(string key, string suffix, out int index)
        {
            index = -1;
            const string prefix = "runtimeCatalogLoad.";
            if (!key.StartsWith(prefix, StringComparison.Ordinal) || !key.EndsWith(suffix, StringComparison.Ordinal))
                return false;

            string indexText = key.Substring(
                prefix.Length,
                key.Length - prefix.Length - suffix.Length);
            return int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
        }

        private static string[] ParseList(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "none", StringComparison.Ordinal))
                return Array.Empty<string>();

            string[] parts = value.Split(',');
            var values = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (!string.IsNullOrEmpty(part))
                    values.Add(part);
            }

            return values.ToArray();
        }
    }
}
