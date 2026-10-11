using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Editor.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Vfx;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed class CharacterTestRuntimeCatalogPlayerArtifactExportEntry
    {
        public CharacterTestRuntimeCatalogPlayerArtifactExportEntry(
            string catalogKind,
            string sourceJsonPath,
            string sourceBinaryPath,
            string mirrorJsonPath,
            string mirrorBinaryPath,
            string jsonStatus,
            string binaryStatus)
        {
            CatalogKind = catalogKind ?? string.Empty;
            SourceJsonPath = sourceJsonPath ?? string.Empty;
            SourceBinaryPath = sourceBinaryPath ?? string.Empty;
            MirrorJsonPath = mirrorJsonPath ?? string.Empty;
            MirrorBinaryPath = mirrorBinaryPath ?? string.Empty;
            JsonStatus = jsonStatus ?? string.Empty;
            BinaryStatus = binaryStatus ?? string.Empty;
        }

        public string CatalogKind { get; }
        public string SourceJsonPath { get; }
        public string SourceBinaryPath { get; }
        public string MirrorJsonPath { get; }
        public string MirrorBinaryPath { get; }
        public string JsonStatus { get; }
        public string BinaryStatus { get; }
    }

    internal sealed class CharacterTestRuntimeCatalogPlayerArtifactExportReport
    {
        private readonly List<CharacterTestRuntimeCatalogPlayerArtifactExportEntry> _entries =
            new List<CharacterTestRuntimeCatalogPlayerArtifactExportEntry>();
        private readonly List<string> _issues = new List<string>();

        public IReadOnlyList<CharacterTestRuntimeCatalogPlayerArtifactExportEntry> Entries => _entries;
        public IReadOnlyList<string> Issues => _issues;
        public int CopiedCount { get; private set; }
        public int UpdatedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int MissingCount { get; private set; }
        public int HashMismatchCount { get; private set; }
        public bool HasBlockingIssues => _issues.Count > 0;

        public void AddEntry(CharacterTestRuntimeCatalogPlayerArtifactExportEntry entry)
        {
            if (entry != null)
                _entries.Add(entry);
        }

        public void AddIssue(string issue)
        {
            if (!string.IsNullOrWhiteSpace(issue))
                _issues.Add(issue);
        }

        public void CountStatus(string status)
        {
            if (string.Equals(status, CharacterTestRuntimeCatalogPlayerArtifactExporter.StatusCopied, StringComparison.Ordinal))
                CopiedCount++;
            else if (string.Equals(status, CharacterTestRuntimeCatalogPlayerArtifactExporter.StatusUpdated, StringComparison.Ordinal))
                UpdatedCount++;
            else if (string.Equals(status, CharacterTestRuntimeCatalogPlayerArtifactExporter.StatusSkipped, StringComparison.Ordinal))
                SkippedCount++;
            else if (string.Equals(status, CharacterTestRuntimeCatalogPlayerArtifactExporter.StatusMissing, StringComparison.Ordinal))
                MissingCount++;
        }

        public void CountHashMismatch()
        {
            HashMismatchCount++;
        }

        public string ToReportText()
        {
            var builder = new StringBuilder(2048);
            builder.AppendLine("scenario: character-test.runtime-catalog.player-artifact-refresh");
            builder.Append("utc: ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            AppendTo(builder, "runtimeCatalogArtifacts");
            return builder.ToString();
        }

        public void AppendTo(StringBuilder builder, string prefix)
        {
            if (builder == null)
                return;

            string safePrefix = string.IsNullOrWhiteSpace(prefix) ? "runtimeCatalogArtifacts" : prefix;
            builder.Append(safePrefix).Append(".expectedCatalogCount: ")
                .AppendLine(CharacterTestRuntimeCatalogArtifactManifest.RequiredPlayerBinaryCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(safePrefix).Append(".entryCount: ").AppendLine(_entries.Count.ToString(CultureInfo.InvariantCulture));
            builder.Append(safePrefix).Append(".copied: ").AppendLine(CopiedCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(safePrefix).Append(".updated: ").AppendLine(UpdatedCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(safePrefix).Append(".skipped: ").AppendLine(SkippedCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(safePrefix).Append(".missing: ").AppendLine(MissingCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(safePrefix).Append(".hashMismatch: ")
                .AppendLine(HashMismatchCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(safePrefix).Append(".hasBlockingIssues: ").AppendLine(HasBlockingIssues ? "true" : "false");
            for (int i = 0; i < _issues.Count; i++)
            {
                builder.Append(safePrefix).Append(".issue.").Append(i.ToString(CultureInfo.InvariantCulture))
                    .Append(": ").AppendLine(_issues[i].Replace('\n', ' '));
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                CharacterTestRuntimeCatalogPlayerArtifactExportEntry entry = _entries[i];
                string entryPrefix = safePrefix + "." + i.ToString(CultureInfo.InvariantCulture);
                builder.Append(entryPrefix).Append(".kind: ").AppendLine(entry.CatalogKind);
                builder.Append(entryPrefix).Append(".jsonStatus: ").AppendLine(entry.JsonStatus);
                builder.Append(entryPrefix).Append(".bytesStatus: ").AppendLine(entry.BinaryStatus);
                builder.Append(entryPrefix).Append(".sourceJson: ").AppendLine(entry.SourceJsonPath);
                builder.Append(entryPrefix).Append(".sourceBytes: ").AppendLine(entry.SourceBinaryPath);
                builder.Append(entryPrefix).Append(".mirrorJson: ").AppendLine(entry.MirrorJsonPath);
                builder.Append(entryPrefix).Append(".mirrorBytes: ").AppendLine(entry.MirrorBinaryPath);
            }
        }
    }

    internal sealed class CharacterTestRuntimeCatalogMirrorValidationEntry
    {
        public CharacterTestRuntimeCatalogMirrorValidationEntry(
            string catalogKind,
            string sourceJsonPath,
            string mirrorJsonPath,
            string status)
            : this(catalogKind, sourceJsonPath, string.Empty, mirrorJsonPath, string.Empty, status, string.Empty)
        {
        }

        public CharacterTestRuntimeCatalogMirrorValidationEntry(
            string catalogKind,
            string sourceJsonPath,
            string sourceBinaryPath,
            string mirrorJsonPath,
            string mirrorBinaryPath,
            string status,
            string details)
        {
            CatalogKind = catalogKind ?? string.Empty;
            SourceJsonPath = sourceJsonPath ?? string.Empty;
            SourceBinaryPath = sourceBinaryPath ?? string.Empty;
            MirrorJsonPath = mirrorJsonPath ?? string.Empty;
            MirrorBinaryPath = mirrorBinaryPath ?? string.Empty;
            Status = status ?? string.Empty;
            Details = details ?? string.Empty;
        }

        public string CatalogKind { get; }
        public string SourceJsonPath { get; }
        public string SourceBinaryPath { get; }
        public string MirrorJsonPath { get; }
        public string MirrorBinaryPath { get; }
        public string Status { get; }
        public string Details { get; }
    }

    internal sealed class CharacterTestRuntimeCatalogMirrorValidationReport
    {
        private readonly List<CharacterTestRuntimeCatalogMirrorValidationEntry> _entries =
            new List<CharacterTestRuntimeCatalogMirrorValidationEntry>();

        public IReadOnlyList<CharacterTestRuntimeCatalogMirrorValidationEntry> Entries => _entries;
        public int MatchedCount { get; private set; }
        public int MissingCount { get; private set; }
        public int MismatchCount { get; private set; }
        public bool HasIssues => MissingCount > 0 || MismatchCount > 0;

        public void AddEntry(CharacterTestRuntimeCatalogMirrorValidationEntry entry)
        {
            if (entry == null)
                return;

            _entries.Add(entry);
            if (string.Equals(entry.Status, CharacterTestRuntimeCatalogMirrorValidator.StatusMatched, StringComparison.Ordinal))
                MatchedCount++;
            else if (string.Equals(entry.Status, CharacterTestRuntimeCatalogMirrorValidator.StatusMismatch, StringComparison.Ordinal)
                || string.Equals(entry.Status, CharacterTestRuntimeCatalogMirrorValidator.StatusBinaryMirrorMismatch, StringComparison.Ordinal)
                || string.Equals(entry.Status, CharacterTestRuntimeCatalogMirrorValidator.StatusBinarySemanticMismatch, StringComparison.Ordinal))
                MismatchCount++;
            else
                MissingCount++;
        }
    }

    internal static class CharacterTestRuntimeCatalogMirrorValidator
    {
        public const string StatusMatched = "matched";
        public const string StatusGeneratedMissing = "generatedMissing";
        public const string StatusMirrorMissing = "mirrorMissing";
        public const string StatusMismatch = "mismatch";
        public const string StatusBinaryGeneratedMissing = "binaryGeneratedMissing";
        public const string StatusBinaryMirrorMissing = "binaryMirrorMissing";
        public const string StatusBinaryMirrorMismatch = "binaryMirrorMismatch";
        public const string StatusBinarySemanticMismatch = "binarySemanticMismatch";

        /// <remarks>AllocByDesign: editor-only local validation of Player runtime catalog mirrors.</remarks>
        public static CharacterTestRuntimeCatalogMirrorValidationReport ValidateDefault()
        {
            return Validate(Directory.GetCurrentDirectory(), Application.streamingAssetsPath);
        }

        /// <remarks>AllocByDesign: editor-only local validation and tests.</remarks>
        public static CharacterTestRuntimeCatalogMirrorValidationReport Validate(
            string projectRootPath,
            string streamingAssetsPath)
        {
            return Validate(projectRootPath, streamingAssetsPath, validateGeneratedBinarySemantics: true);
        }

        /// <remarks>AllocByDesign: editor-only local validation and tests.</remarks>
        public static CharacterTestRuntimeCatalogMirrorValidationReport Validate(
            string projectRootPath,
            string streamingAssetsPath,
            bool validateGeneratedBinarySemantics)
        {
            var report = new CharacterTestRuntimeCatalogMirrorValidationReport();
            IReadOnlyList<CharacterTestRuntimeCatalogArtifact> artifacts =
                CharacterTestRuntimeCatalogArtifactManifest.Artifacts;
            CharacterTestRuntimeCatalogGeneratedBinaryValidationReport binaryReport =
                validateGeneratedBinarySemantics
                    ? CharacterTestRuntimeCatalogGeneratedBinaryValidator.Validate(projectRootPath)
                    : null;
            for (int i = 0; i < artifacts.Count; i++)
            {
                CharacterTestRuntimeCatalogArtifact artifact = artifacts[i];
                string sourceJsonPath = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedJsonPath,
                    projectRootPath);
                string sourceBinaryPath = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedBinaryPath,
                    projectRootPath);
                string mirrorJsonPath = CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                    artifact.PlayerMirrorRelativeJsonPath,
                    streamingAssetsPath);
                string mirrorBinaryPath = CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                    artifact.PlayerMirrorRelativeBinaryPath,
                    streamingAssetsPath);

                bool hasIssues = false;
                if (string.IsNullOrWhiteSpace(sourceJsonPath) || !File.Exists(sourceJsonPath))
                {
                    hasIssues = true;
                    AddEntry(report, artifact, sourceJsonPath, sourceBinaryPath, mirrorJsonPath, mirrorBinaryPath,
                        StatusGeneratedMissing, string.Empty);
                }
                else if (string.IsNullOrWhiteSpace(mirrorJsonPath) || !File.Exists(mirrorJsonPath))
                {
                    hasIssues = true;
                    AddEntry(report, artifact, sourceJsonPath, sourceBinaryPath, mirrorJsonPath, mirrorBinaryPath,
                        StatusMirrorMissing, string.Empty);
                }
                else
                {
                    bool jsonMatches = CharacterTestRuntimeCatalogPlayerArtifactExporter.FilesMatch(
                        sourceJsonPath,
                        mirrorJsonPath);
                    if (!jsonMatches)
                    {
                        hasIssues = true;
                        AddEntry(report, artifact, sourceJsonPath, sourceBinaryPath, mirrorJsonPath, mirrorBinaryPath,
                            StatusMismatch, string.Empty);
                    }
                }

                if (artifact.RequiresPlayerBinary)
                {
                    if (string.IsNullOrWhiteSpace(sourceBinaryPath) || !File.Exists(sourceBinaryPath))
                    {
                        hasIssues = true;
                        AddEntry(report, artifact, sourceJsonPath, sourceBinaryPath, mirrorJsonPath, mirrorBinaryPath,
                            StatusBinaryGeneratedMissing, string.Empty);
                    }
                    else if (string.IsNullOrWhiteSpace(mirrorBinaryPath) || !File.Exists(mirrorBinaryPath))
                    {
                        hasIssues = true;
                        AddEntry(report, artifact, sourceJsonPath, sourceBinaryPath, mirrorJsonPath, mirrorBinaryPath,
                            StatusBinaryMirrorMissing, string.Empty);
                    }
                    else
                    {
                        bool binaryMatches = CharacterTestRuntimeCatalogPlayerArtifactExporter.FilesMatch(
                            sourceBinaryPath,
                            mirrorBinaryPath);
                        if (!binaryMatches)
                        {
                            hasIssues = true;
                            AddEntry(report, artifact, sourceJsonPath, sourceBinaryPath, mirrorJsonPath, mirrorBinaryPath,
                                StatusBinaryMirrorMismatch, string.Empty);
                        }
                    }
                }

                CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry binaryEntry =
                    binaryReport?.FindEntry(artifact.CatalogKind);
                if (binaryEntry != null
                    && binaryEntry.HasIssues
                    && File.Exists(sourceJsonPath)
                    && File.Exists(sourceBinaryPath))
                {
                    hasIssues = true;
                    AddEntry(report, artifact, sourceJsonPath, sourceBinaryPath, mirrorJsonPath, mirrorBinaryPath,
                        StatusBinarySemanticMismatch, binaryEntry.Summary);
                }

                if (!hasIssues)
                {
                    AddEntry(report, artifact, sourceJsonPath, sourceBinaryPath, mirrorJsonPath, mirrorBinaryPath,
                        StatusMatched, string.Empty);
                }
            }

            return report;
        }

        private static void AddEntry(
            CharacterTestRuntimeCatalogMirrorValidationReport report,
            CharacterTestRuntimeCatalogArtifact artifact,
            string sourceJsonPath,
            string sourceBinaryPath,
            string mirrorJsonPath,
            string mirrorBinaryPath,
            string status,
            string details)
        {
            report.AddEntry(new CharacterTestRuntimeCatalogMirrorValidationEntry(
                artifact.CatalogKind,
                sourceJsonPath,
                sourceBinaryPath,
                mirrorJsonPath,
                mirrorBinaryPath,
                status,
                details));
        }
    }

    [InitializeOnLoad]
    internal static class CharacterTestRuntimeCatalogMirrorAuthoringValidationRegistration
    {
        static CharacterTestRuntimeCatalogMirrorAuthoringValidationRegistration()
        {
            CharacterActionAuthoringLocalValidationRegistry.Register(
                new CharacterTestRuntimeCatalogMirrorAuthoringValidationProvider());
        }
    }

    internal sealed class CharacterTestRuntimeCatalogMirrorAuthoringValidationProvider :
        ICharacterActionAuthoringLocalValidationProvider
    {
        private const string MismatchCode = "RUNTIME_CATALOG_MIRROR_MISMATCH";
        private const string BinaryMismatchCode = "RUNTIME_CATALOG_BINARY_MISMATCH";
        private const string BinaryMirrorMismatchCode = "RUNTIME_CATALOG_BINARY_MIRROR_MISMATCH";
        private const string BinaryMissingCode = "RUNTIME_CATALOG_BINARY_MISSING";
        private const string GeneratedMissingCode = "RUNTIME_CATALOG_GENERATED_MISSING";
        private const string MirrorMissingCode = "RUNTIME_CATALOG_MIRROR_MISSING";
        private const string StablePathPrefix = "$.characterTest.runtimeCatalogMirror.";

        /// <remarks>AllocByDesign: editor-only Character Action local validation provider.</remarks>
        public CharacterActionValidationDiagnostic[] Validate(CharacterActionAuthoringLocalValidationContext context)
        {
            CharacterTestRuntimeCatalogMirrorValidationReport report =
                CharacterTestRuntimeCatalogMirrorValidator.ValidateDefault();
            return ToDiagnostics(report);
        }

        /// <remarks>AllocByDesign: test helper and editor local validation formatting.</remarks>
        public static CharacterActionValidationDiagnostic[] ToDiagnostics(
            CharacterTestRuntimeCatalogMirrorValidationReport report)
        {
            if (report == null || !report.HasIssues)
                return Array.Empty<CharacterActionValidationDiagnostic>();

            var diagnostics = new CharacterActionValidationDiagnostic[
                report.MissingCount + report.MismatchCount];
            int count = 0;
            for (int i = 0; i < report.Entries.Count; i++)
            {
                CharacterTestRuntimeCatalogMirrorValidationEntry entry = report.Entries[i];
                if (entry == null
                    || string.Equals(entry.Status, CharacterTestRuntimeCatalogMirrorValidator.StatusMatched, StringComparison.Ordinal))
                {
                    continue;
                }

                string code = ResolveCode(entry.Status);
                diagnostics[count++] = new CharacterActionValidationDiagnostic(
                    entry.SourceJsonPath,
                    StablePathPrefix + entry.CatalogKind,
                    code,
                    CharacterActionDiagnosticSeverity.Warning,
                    BuildMessage(entry));
            }

            if (count == diagnostics.Length)
                return diagnostics;

            Array.Resize(ref diagnostics, count);
            return diagnostics;
        }

        private static string ResolveCode(string status)
        {
            if (string.Equals(status, CharacterTestRuntimeCatalogMirrorValidator.StatusMismatch, StringComparison.Ordinal))
                return MismatchCode;
            if (string.Equals(status, CharacterTestRuntimeCatalogMirrorValidator.StatusBinarySemanticMismatch, StringComparison.Ordinal))
                return BinaryMismatchCode;
            if (string.Equals(status, CharacterTestRuntimeCatalogMirrorValidator.StatusBinaryMirrorMismatch, StringComparison.Ordinal))
                return BinaryMirrorMismatchCode;
            if (string.Equals(status, CharacterTestRuntimeCatalogMirrorValidator.StatusBinaryGeneratedMissing, StringComparison.Ordinal)
                || string.Equals(status, CharacterTestRuntimeCatalogMirrorValidator.StatusBinaryMirrorMissing, StringComparison.Ordinal))
            {
                return BinaryMissingCode;
            }

            if (string.Equals(status, CharacterTestRuntimeCatalogMirrorValidator.StatusGeneratedMissing, StringComparison.Ordinal))
                return GeneratedMissingCode;
            return MirrorMissingCode;
        }

        private static string BuildMessage(CharacterTestRuntimeCatalogMirrorValidationEntry entry)
        {
            string reason;
            string generatedPath = entry.SourceJsonPath;
            string mirrorPath = entry.MirrorJsonPath;
            string fix = CharacterTestRuntimeCatalogArtifactRepairTool.MenuPath;
            if (string.Equals(entry.Status, CharacterTestRuntimeCatalogMirrorValidator.StatusMismatch, StringComparison.Ordinal))
            {
                reason = "Player runtime catalog JSON mirror is stale.";
            }
            else if (string.Equals(entry.Status, CharacterTestRuntimeCatalogMirrorValidator.StatusBinarySemanticMismatch, StringComparison.Ordinal))
            {
                reason = "Generated runtime catalog .bytes does not match generated JSON; Refresh Player Runtime Catalog Artifacts cannot fix this because it only copies existing artifacts.";
                generatedPath = entry.SourceBinaryPath;
                mirrorPath = entry.SourceJsonPath;
            }
            else if (string.Equals(entry.Status, CharacterTestRuntimeCatalogMirrorValidator.StatusBinaryMirrorMismatch, StringComparison.Ordinal))
            {
                reason = "Player runtime catalog .bytes mirror is stale.";
                generatedPath = entry.SourceBinaryPath;
                mirrorPath = entry.MirrorBinaryPath;
            }
            else if (string.Equals(entry.Status, CharacterTestRuntimeCatalogMirrorValidator.StatusBinaryGeneratedMissing, StringComparison.Ordinal))
            {
                reason = "Generated runtime catalog .bytes is missing; Refresh Player Runtime Catalog Artifacts cannot rebuild it.";
                generatedPath = entry.SourceBinaryPath;
                mirrorPath = entry.MirrorBinaryPath;
            }
            else if (string.Equals(entry.Status, CharacterTestRuntimeCatalogMirrorValidator.StatusBinaryMirrorMissing, StringComparison.Ordinal))
            {
                reason = "Player runtime catalog .bytes mirror is missing.";
                generatedPath = entry.SourceBinaryPath;
                mirrorPath = entry.MirrorBinaryPath;
            }
            else
            {
                reason = "Player runtime catalog mirror file set is incomplete.";
            }

            string message = reason
                + " catalogKind="
                + entry.CatalogKind
                + "; generated="
                + generatedPath
                + "; mirror="
                + mirrorPath;
            if (!string.IsNullOrWhiteSpace(entry.Details))
                message += "; details=" + entry.Details;

            return message + "; fix: run " + fix + ".";
        }
    }

    internal static class CharacterTestRuntimeCatalogPlayerArtifactExporter
    {
        public const string MenuPath =
            "MxFramework/CharacterTest/Performance/Refresh Player Runtime Catalog Artifacts";
        public const string DefaultReportPath =
            "Temp/CharacterTestRuntimeCatalogPlayerArtifactRefreshReport.txt";
        public const string StatusCopied = "copied";
        public const string StatusUpdated = "updated";
        public const string StatusSkipped = "skipped";
        public const string StatusMissing = "missing";

        [MenuItem(MenuPath)]
        public static void RefreshDefaultMenu()
        {
            CharacterTestRuntimeCatalogPlayerArtifactExportReport report = RefreshDefault();
            CharacterTestRuntimeCatalogGeneratedBinaryValidationReport binaryReport =
                CharacterTestRuntimeCatalogGeneratedBinaryValidator.ValidateDefault();
            string reportPath = Path.GetFullPath(DefaultReportPath);
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var builder = new StringBuilder(4096);
            builder.Append(report.ToReportText());
            binaryReport.AppendTo(builder, "generatedBinaryValidation");
            File.WriteAllText(reportPath, builder.ToString());
            if (report.HasBlockingIssues)
                Debug.LogError("CharacterTest runtime catalog Player artifact refresh has blocking issues: " + reportPath);
            else if (binaryReport.HasIssues)
            {
                Debug.LogError(
                    "CharacterTest runtime catalog Player artifacts were refreshed, but generated JSON/.bytes are still not Player-ready: "
                    + binaryReport.FirstIssueSummary()
                    + ". Run "
                    + CharacterTestRuntimeCatalogArtifactRepairTool.MenuPath
                    + ". Report: "
                    + reportPath);
            }
            else
                Debug.Log("CharacterTest runtime catalog Player artifacts refreshed: " + reportPath);
        }

        /// <remarks>AllocByDesign: editor-only build/menu artifact synchronization.</remarks>
        public static CharacterTestRuntimeCatalogPlayerArtifactExportReport RefreshDefault()
        {
            CharacterTestRuntimeCatalogPlayerArtifactExportReport report = Refresh(
                Directory.GetCurrentDirectory(),
                Application.streamingAssetsPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return report;
        }

        /// <remarks>AllocByDesign: editor-only build/menu artifact synchronization and tests.</remarks>
        public static CharacterTestRuntimeCatalogPlayerArtifactExportReport Refresh(
            string projectRootPath,
            string streamingAssetsPath)
        {
            var report = new CharacterTestRuntimeCatalogPlayerArtifactExportReport();
            IReadOnlyList<CharacterTestRuntimeCatalogArtifact> artifacts =
                CharacterTestRuntimeCatalogArtifactManifest.Artifacts;
            for (int i = 0; i < artifacts.Count; i++)
            {
                CharacterTestRuntimeCatalogArtifact artifact = artifacts[i];
                string sourceJsonPath = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedJsonPath,
                    projectRootPath);
                string sourceBinaryPath = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedBinaryPath,
                    projectRootPath);
                string mirrorJsonPath = CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                    artifact.PlayerMirrorRelativeJsonPath,
                    streamingAssetsPath);
                string mirrorBinaryPath = CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                    artifact.PlayerMirrorRelativeBinaryPath,
                    streamingAssetsPath);

                string jsonStatus = CopyArtifactFile(
                    sourceJsonPath,
                    mirrorJsonPath,
                    artifact.CatalogKind,
                    "json",
                    required: true,
                    report);
                string binaryStatus = artifact.RequiresPlayerBinary
                    ? CopyArtifactFile(
                        sourceBinaryPath,
                        mirrorBinaryPath,
                        artifact.CatalogKind,
                        "bytes",
                        required: true,
                        report)
                    : StatusSkipped;
                if (artifact.RequiresPlayerBinary
                    && File.Exists(sourceJsonPath)
                    && !File.Exists(sourceBinaryPath))
                {
                    report.AddIssue(
                        artifact.CatalogKind
                        + " generated JSON exists but .bytes is missing. Run ability export-runtime-binaries / buff export-runtime-binaries before building the Player.");
                }

                report.AddEntry(new CharacterTestRuntimeCatalogPlayerArtifactExportEntry(
                    artifact.CatalogKind,
                    sourceJsonPath,
                    sourceBinaryPath,
                    mirrorJsonPath,
                    mirrorBinaryPath,
                    jsonStatus,
                    binaryStatus));
            }

            return report;
        }

        private static string CopyArtifactFile(
            string sourcePath,
            string mirrorPath,
            string catalogKind,
            string artifactKind,
            bool required,
            CharacterTestRuntimeCatalogPlayerArtifactExportReport report)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                report.CountStatus(StatusMissing);
                if (required)
                {
                    report.AddIssue(
                        catalogKind
                        + " generated "
                        + artifactKind
                        + " source is missing: "
                        + (sourcePath ?? string.Empty));
                }

                return StatusMissing;
            }

            string mirrorDirectory = Path.GetDirectoryName(mirrorPath);
            if (!string.IsNullOrEmpty(mirrorDirectory))
                Directory.CreateDirectory(mirrorDirectory);

            if (!File.Exists(mirrorPath))
            {
                File.Copy(sourcePath, mirrorPath);
                report.CountStatus(StatusCopied);
                return StatusCopied;
            }

            if (FilesMatch(sourcePath, mirrorPath))
            {
                report.CountStatus(StatusSkipped);
                return StatusSkipped;
            }

            report.CountHashMismatch();
            File.Copy(sourcePath, mirrorPath, overwrite: true);
            report.CountStatus(StatusUpdated);
            return StatusUpdated;
        }

        internal static bool FilesMatch(string leftPath, string rightPath)
        {
            var leftInfo = new FileInfo(leftPath);
            var rightInfo = new FileInfo(rightPath);
            if (leftInfo.Length != rightInfo.Length)
                return false;

            byte[] leftHash = ComputeSha256(leftPath);
            byte[] rightHash = ComputeSha256(rightPath);
            if (leftHash.Length != rightHash.Length)
                return false;

            for (int i = 0; i < leftHash.Length; i++)
            {
                if (leftHash[i] != rightHash[i])
                    return false;
            }

            return true;
        }

        private static byte[] ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                return sha.ComputeHash(stream);
        }
    }

    internal sealed class CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry
    {
        public string CatalogKind;
        public string JsonPath;
        public string BinaryPath;
        public bool JsonSuccess;
        public bool BinarySuccess;
        public string JsonError;
        public string BinaryError;
        public int JsonEntryCount;
        public int BinaryEntryCount;
        public string JsonStableHash;
        public string BinaryStableHash;
        public bool EntryCountMatches;
        public bool StableHashMatches;

        public bool HasIssues
        {
            get
            {
                return !JsonSuccess
                    || !BinarySuccess
                    || !EntryCountMatches
                    || !StableHashMatches;
            }
        }

        public string Summary
        {
            get
            {
                if (!JsonSuccess)
                    return "JSON import failed: " + JsonError;
                if (!BinarySuccess)
                    return "binary import failed: " + BinaryError;
                if (!EntryCountMatches)
                    return "entry count mismatch json=" + JsonEntryCount.ToString(CultureInfo.InvariantCulture)
                        + " binary=" + BinaryEntryCount.ToString(CultureInfo.InvariantCulture);
                if (!StableHashMatches)
                    return "stable hash mismatch json=" + JsonStableHash + " binary=" + BinaryStableHash;
                return "matched";
            }
        }
    }

    internal sealed class CharacterTestRuntimeCatalogGeneratedBinaryValidationReport
    {
        private readonly List<CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry> _entries =
            new List<CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry>();

        public IReadOnlyList<CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry> Entries => _entries;
        public int IssueCount { get; private set; }
        public bool HasIssues => IssueCount > 0;

        public void AddEntry(CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry entry)
        {
            if (entry == null)
                return;

            _entries.Add(entry);
            if (entry.HasIssues)
                IssueCount++;
        }

        public CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry FindEntry(string catalogKind)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry entry = _entries[i];
                if (string.Equals(entry.CatalogKind, catalogKind, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        public string FirstIssueSummary()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry entry = _entries[i];
                if (entry.HasIssues)
                    return entry.CatalogKind + ": " + entry.Summary;
            }

            return string.Empty;
        }

        public string ToReportText()
        {
            var builder = new StringBuilder(2048);
            builder.AppendLine("scenario: character-test.runtime-catalog.generated-binary-validation");
            builder.Append("utc: ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            AppendTo(builder, "generatedBinaryValidation");
            return builder.ToString();
        }

        public void AppendTo(StringBuilder builder, string prefix)
        {
            if (builder == null)
                return;

            string safePrefix = string.IsNullOrWhiteSpace(prefix) ? "generatedBinaryValidation" : prefix;
            builder.Append(safePrefix).Append(".entryCount: ").AppendLine(_entries.Count.ToString(CultureInfo.InvariantCulture));
            builder.Append(safePrefix).Append(".issueCount: ").AppendLine(IssueCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(safePrefix).Append(".hasIssues: ").AppendLine(HasIssues ? "true" : "false");
            for (int i = 0; i < _entries.Count; i++)
            {
                CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry entry = _entries[i];
                string entryPrefix = safePrefix + "." + i.ToString(CultureInfo.InvariantCulture);
                builder.Append(entryPrefix).Append(".kind: ").AppendLine(entry.CatalogKind);
                builder.Append(entryPrefix).Append(".jsonPath: ").AppendLine(entry.JsonPath);
                builder.Append(entryPrefix).Append(".binaryPath: ").AppendLine(entry.BinaryPath);
                builder.Append(entryPrefix).Append(".jsonSuccess: ").AppendLine(entry.JsonSuccess ? "true" : "false");
                builder.Append(entryPrefix).Append(".binarySuccess: ").AppendLine(entry.BinarySuccess ? "true" : "false");
                builder.Append(entryPrefix).Append(".entryCountMatches: ")
                    .AppendLine(entry.EntryCountMatches ? "true" : "false");
                builder.Append(entryPrefix).Append(".stableHashMatches: ")
                    .AppendLine(entry.StableHashMatches ? "true" : "false");
                builder.Append(entryPrefix).Append(".summary: ").AppendLine(entry.Summary.Replace('\n', ' '));
            }
        }
    }

    internal static class CharacterTestRuntimeCatalogGeneratedBinaryValidator
    {
        /// <remarks>AllocByDesign: editor-only generated runtime catalog consistency validation.</remarks>
        public static CharacterTestRuntimeCatalogGeneratedBinaryValidationReport ValidateDefault()
        {
            return Validate(Directory.GetCurrentDirectory());
        }

        /// <remarks>AllocByDesign: editor-only generated runtime catalog consistency validation and tests.</remarks>
        public static CharacterTestRuntimeCatalogGeneratedBinaryValidationReport Validate(string projectRootPath)
        {
            List<CharacterTestRuntimeCatalogLoadBenchmarkRecord> records =
                CharacterTestRuntimeCatalogLoadBenchmark.Run(projectRootPath);
            var report = new CharacterTestRuntimeCatalogGeneratedBinaryValidationReport();
            for (int i = 0; i < records.Count; i++)
            {
                CharacterTestRuntimeCatalogLoadBenchmarkRecord record = records[i];
                report.AddEntry(new CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry
                {
                    CatalogKind = record.CatalogKind,
                    JsonPath = record.JsonPath,
                    BinaryPath = record.BinaryPath,
                    JsonSuccess = record.JsonSuccess,
                    BinarySuccess = record.BinarySuccess,
                    JsonError = record.JsonError,
                    BinaryError = record.BinaryError,
                    JsonEntryCount = record.JsonEntryCount,
                    BinaryEntryCount = record.BinaryEntryCount,
                    JsonStableHash = record.JsonStableHash,
                    BinaryStableHash = record.BinaryStableHash,
                    EntryCountMatches = record.EntryCountMatches,
                    StableHashMatches = record.StableHashMatches
                });
            }

            return report;
        }
    }

    internal sealed class CharacterTestRuntimeCatalogRepairCommandResult
    {
        public string CommandName;
        public string Arguments;
        public int ExitCode;
        public string Output;
        public string Error;
        public string LaunchError;

        public bool Succeeded => ExitCode == 0 && string.IsNullOrWhiteSpace(LaunchError);

        public void AppendTo(StringBuilder builder, string prefix)
        {
            if (builder == null)
                return;

            string safePrefix = string.IsNullOrWhiteSpace(prefix) ? "command" : prefix;
            builder.Append(safePrefix).Append(".name: ").AppendLine(CommandName ?? string.Empty);
            builder.Append(safePrefix).Append(".arguments: ").AppendLine((Arguments ?? string.Empty).Replace('\n', ' '));
            builder.Append(safePrefix).Append(".exitCode: ").AppendLine(ExitCode.ToString(CultureInfo.InvariantCulture));
            builder.Append(safePrefix).Append(".succeeded: ").AppendLine(Succeeded ? "true" : "false");
            if (!string.IsNullOrWhiteSpace(LaunchError))
                builder.Append(safePrefix).Append(".launchError: ").AppendLine(LaunchError.Replace('\n', ' '));
            if (!string.IsNullOrWhiteSpace(Output))
                builder.Append(safePrefix).Append(".stdout: ").AppendLine(Output.Replace('\n', ' '));
            if (!string.IsNullOrWhiteSpace(Error))
                builder.Append(safePrefix).Append(".stderr: ").AppendLine(Error.Replace('\n', ' '));
        }
    }

    internal sealed class CharacterTestRuntimeCatalogArtifactRepairReport
    {
        private readonly List<string> _issues = new List<string>();

        public IReadOnlyList<string> Issues => _issues;
        public CharacterTestRuntimeCatalogRepairCommandResult AbilityRuntimeBinaryExport;
        public CharacterTestRuntimeCatalogRepairCommandResult BuffRuntimeBinaryExport;
        public CharacterTestRuntimeCatalogPlayerArtifactExportReport RefreshReport;
        public CharacterTestRuntimeCatalogGeneratedBinaryValidationReport GeneratedBinaryValidationReport;
        public bool HasBlockingIssues
        {
            get
            {
                return _issues.Count > 0
                    || AbilityRuntimeBinaryExport == null
                    || !AbilityRuntimeBinaryExport.Succeeded
                    || BuffRuntimeBinaryExport == null
                    || !BuffRuntimeBinaryExport.Succeeded
                    || (RefreshReport != null && RefreshReport.HasBlockingIssues)
                    || (GeneratedBinaryValidationReport != null && GeneratedBinaryValidationReport.HasIssues);
            }
        }

        public void AddIssue(string issue)
        {
            if (!string.IsNullOrWhiteSpace(issue))
                _issues.Add(issue);
        }

        public string FirstIssueSummary()
        {
            if (_issues.Count > 0)
                return _issues[0];
            if (AbilityRuntimeBinaryExport == null || !AbilityRuntimeBinaryExport.Succeeded)
                return "ability runtime binary export failed.";
            if (BuffRuntimeBinaryExport == null || !BuffRuntimeBinaryExport.Succeeded)
                return "buff runtime binary export failed.";
            if (RefreshReport != null && RefreshReport.HasBlockingIssues)
                return "player mirror refresh failed: " + string.Join("; ", RefreshReport.Issues);
            if (GeneratedBinaryValidationReport != null && GeneratedBinaryValidationReport.HasIssues)
                return "generated binary validation failed: " + GeneratedBinaryValidationReport.FirstIssueSummary();
            return string.Empty;
        }

        public string ToReportText()
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine("scenario: character-test.runtime-catalog.artifact-repair");
            builder.Append("utc: ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            builder.Append("status: ").AppendLine(HasBlockingIssues ? "failed" : "ready");
            builder.Append("repairMenu: ").AppendLine(CharacterTestRuntimeCatalogArtifactRepairTool.MenuPath);
            builder.Append("issueCount: ").AppendLine(_issues.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < _issues.Count; i++)
                builder.Append("issue.").Append(i.ToString(CultureInfo.InvariantCulture)).Append(": ")
                    .AppendLine(_issues[i].Replace('\n', ' '));

            AbilityRuntimeBinaryExport?.AppendTo(builder, "abilityRuntimeBinaryExport");
            BuffRuntimeBinaryExport?.AppendTo(builder, "buffRuntimeBinaryExport");
            RefreshReport?.AppendTo(builder, "playerMirrorRefresh");
            GeneratedBinaryValidationReport?.AppendTo(builder, "generatedBinaryValidation");
            return builder.ToString();
        }
    }

    internal static class CharacterTestRuntimeCatalogArtifactRepairTool
    {
        public const string MenuPath =
            "MxFramework/CharacterTest/Performance/Repair Runtime Catalog Artifacts";
        public const string DefaultReportPath =
            "Temp/CharacterTestRuntimeCatalogRepairReport.txt";

        [MenuItem(MenuPath)]
        public static void RepairDefaultMenu()
        {
            CharacterTestRuntimeCatalogArtifactRepairReport report = RepairDefault();
            string reportPath = WriteReport(report, DefaultReportPath);
            if (report.HasBlockingIssues)
            {
                Debug.LogError(
                    "CharacterTest runtime catalog artifact repair failed: "
                    + report.FirstIssueSummary()
                    + " Report: "
                    + reportPath);
            }
            else
            {
                Debug.Log("CharacterTest runtime catalog artifacts repaired: " + reportPath);
            }
        }

        /// <remarks>AllocByDesign: editor-only repair command for generated runtime catalog artifacts.</remarks>
        public static CharacterTestRuntimeCatalogArtifactRepairReport RepairDefault()
        {
            string projectRoot = FindRepoRoot();
            var report = new CharacterTestRuntimeCatalogArtifactRepairReport();
            report.AbilityRuntimeBinaryExport = RunRuntimeBinaryExport(projectRoot, "ability");
            report.BuffRuntimeBinaryExport = RunRuntimeBinaryExport(projectRoot, "buff");

            if (!report.AbilityRuntimeBinaryExport.Succeeded || !report.BuffRuntimeBinaryExport.Succeeded)
            {
                report.AddIssue("Runtime binary export failed; Player mirror refresh was skipped.");
                return report;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            report.RefreshReport = CharacterTestRuntimeCatalogPlayerArtifactExporter.Refresh(
                projectRoot,
                Application.streamingAssetsPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            report.GeneratedBinaryValidationReport =
                CharacterTestRuntimeCatalogGeneratedBinaryValidator.Validate(projectRoot);
            if (report.GeneratedBinaryValidationReport.HasIssues)
            {
                report.AddIssue(
                    "Generated JSON/.bytes validation still has issues after repair: "
                    + report.GeneratedBinaryValidationReport.FirstIssueSummary());
            }

            return report;
        }

        public static string WriteReport(
            CharacterTestRuntimeCatalogArtifactRepairReport report,
            string reportPath)
        {
            string resolvedPath = Path.GetFullPath(
                string.IsNullOrWhiteSpace(reportPath) ? DefaultReportPath : reportPath);
            string directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(resolvedPath, (report ?? new CharacterTestRuntimeCatalogArtifactRepairReport()).ToReportText());
            return resolvedPath;
        }

        private static CharacterTestRuntimeCatalogRepairCommandResult RunRuntimeBinaryExport(
            string projectRoot,
            string domain)
        {
            string cliProject = Path.Combine(
                projectRoot,
                "Tools",
                "MxFramework.Authoring",
                "src",
                "MxFramework.Authoring.Cli",
                "MxFramework.Authoring.Cli.csproj");
            var result = new CharacterTestRuntimeCatalogRepairCommandResult
            {
                CommandName = domain + " export-runtime-binaries",
                ExitCode = -1
            };
            if (!File.Exists(cliProject))
            {
                result.LaunchError = "Authoring CLI project was not found: " + cliProject;
                return result;
            }

            var args = new StringBuilder();
            AppendArg(args, "run");
            AppendArg(args, "--project");
            AppendArg(args, cliProject);
            AppendArg(args, "--");
            AppendArg(args, domain);
            AppendArg(args, "export-runtime-binaries");
            AppendArg(args, "--project-root");
            AppendArg(args, projectRoot);
            result.Arguments = args.ToString();

            var startInfo = new ProcessStartInfo(FindDotnetExecutable(), result.Arguments)
            {
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        result.LaunchError = "Process.Start returned null.";
                        return result;
                    }

                    result.Output = process.StandardOutput.ReadToEnd();
                    result.Error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    result.ExitCode = process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                result.LaunchError = ex.GetType().Name + ": " + ex.Message;
            }

            return result;
        }

        private static string FindRepoRoot()
        {
            string current = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            while (!string.IsNullOrEmpty(current))
            {
                string candidate = Path.Combine(current, "Tools", "MxFramework.Authoring", "MxFramework.Authoring.sln");
                if (File.Exists(candidate))
                    return current;

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent != null ? parent.FullName : string.Empty;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string FindDotnetExecutable()
        {
            string dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrWhiteSpace(dotnetRoot))
            {
                string candidate = Path.Combine(dotnetRoot, "dotnet");
                if (File.Exists(candidate))
                    return candidate;
            }

            string[] candidates =
            {
                "/usr/local/share/dotnet/dotnet",
                "/opt/homebrew/bin/dotnet",
                "/usr/local/bin/dotnet"
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                    return candidates[i];
            }

            return "dotnet";
        }

        private static void AppendArg(StringBuilder builder, string value)
        {
            if (builder.Length > 0)
                builder.Append(' ');

            string safeValue = value ?? string.Empty;
            if (safeValue.IndexOfAny(new[] { ' ', '\t', '"', '\\' }) < 0)
            {
                builder.Append(safeValue);
                return;
            }

            builder.Append('"');
            for (int i = 0; i < safeValue.Length; i++)
            {
                char c = safeValue[i];
                if (c == '"' || c == '\\')
                    builder.Append('\\');
                builder.Append(c);
            }

            builder.Append('"');
        }
    }

    internal readonly struct CharacterTestRuntimeCatalogLoadBenchmarkStats
    {
        public CharacterTestRuntimeCatalogLoadBenchmarkStats(int entryCount, string stableHash)
        {
            EntryCount = entryCount;
            StableHash = stableHash ?? string.Empty;
        }

        public int EntryCount { get; }
        public string StableHash { get; }
    }

    internal sealed class CharacterTestRuntimeCatalogLoadBenchmarkRecord
    {
        public string CatalogKind;
        public string JsonPath;
        public string BinaryPath;
        public bool JsonSuccess;
        public bool BinarySuccess;
        public string JsonError;
        public string BinaryError;
        public int JsonEntryCount;
        public int BinaryEntryCount;
        public string JsonStableHash;
        public string BinaryStableHash;
        public double JsonElapsedMs;
        public double BinaryElapsedMs;
        public long JsonAllocatedBytes;
        public long BinaryAllocatedBytes;
        public bool EntryCountMatches;
        public bool StableHashMatches;
    }

    internal static class CharacterTestRuntimeCatalogLoadBenchmark
    {
        public const string MenuPath =
            "MxFramework/CharacterTest/Performance/Benchmark Runtime Catalog Load";
        public const string DefaultReportPath =
            "Temp/CharacterTestRuntimeCatalogLoadBenchmarkReport.txt";

        [MenuItem(MenuPath)]
        public static void WriteDefaultReport()
        {
            string reportPath = WriteReport(DefaultReportPath);
            Debug.Log("CharacterTest runtime catalog load benchmark written: " + reportPath);
        }

        /// <remarks>AllocByDesign: opt-in editor benchmark evidence, not a runtime hot path.</remarks>
        public static string WriteReport(string reportPath)
        {
            string resolvedPath = Path.GetFullPath(
                string.IsNullOrWhiteSpace(reportPath) ? DefaultReportPath : reportPath);
            string directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(resolvedPath, CreateReportText());
            return resolvedPath;
        }

        /// <remarks>AllocByDesign: opt-in editor benchmark evidence, not a runtime hot path.</remarks>
        public static string CreateReportText()
        {
            List<CharacterTestRuntimeCatalogLoadBenchmarkRecord> records = Run();
            var builder = new StringBuilder(4096);
            builder.AppendLine("scenario: character-test.runtime-catalog.load-benchmark");
            builder.Append("utc: ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            builder.Append("catalogCount: ").AppendLine(records.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < records.Count; i++)
            {
                CharacterTestRuntimeCatalogLoadBenchmarkRecord record = records[i];
                string prefix = "catalog." + i.ToString(CultureInfo.InvariantCulture);
                builder.Append(prefix).Append(".kind: ").AppendLine(record.CatalogKind);
                builder.Append(prefix).Append(".jsonPath: ").AppendLine(record.JsonPath);
                builder.Append(prefix).Append(".binaryPath: ").AppendLine(record.BinaryPath);
                builder.Append(prefix).Append(".jsonSuccess: ").AppendLine(record.JsonSuccess ? "true" : "false");
                builder.Append(prefix).Append(".binarySuccess: ").AppendLine(record.BinarySuccess ? "true" : "false");
                builder.Append(prefix).Append(".jsonElapsedMs: ")
                    .AppendLine(record.JsonElapsedMs.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(prefix).Append(".binaryElapsedMs: ")
                    .AppendLine(record.BinaryElapsedMs.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(prefix).Append(".jsonAllocatedBytes: ")
                    .AppendLine(record.JsonAllocatedBytes.ToString(CultureInfo.InvariantCulture));
                builder.Append(prefix).Append(".binaryAllocatedBytes: ")
                    .AppendLine(record.BinaryAllocatedBytes.ToString(CultureInfo.InvariantCulture));
                builder.Append(prefix).Append(".jsonEntryCount: ")
                    .AppendLine(record.JsonEntryCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(prefix).Append(".binaryEntryCount: ")
                    .AppendLine(record.BinaryEntryCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(prefix).Append(".entryCountMatches: ")
                    .AppendLine(record.EntryCountMatches ? "true" : "false");
                builder.Append(prefix).Append(".jsonStableHash: ").AppendLine(record.JsonStableHash);
                builder.Append(prefix).Append(".binaryStableHash: ").AppendLine(record.BinaryStableHash);
                builder.Append(prefix).Append(".stableHashMatches: ")
                    .AppendLine(record.StableHashMatches ? "true" : "false");
                if (!string.IsNullOrWhiteSpace(record.JsonError))
                    builder.Append(prefix).Append(".jsonError: ").AppendLine(record.JsonError.Replace('\n', ' '));
                if (!string.IsNullOrWhiteSpace(record.BinaryError))
                    builder.Append(prefix).Append(".binaryError: ").AppendLine(record.BinaryError.Replace('\n', ' '));
            }

            return builder.ToString();
        }

        /// <remarks>AllocByDesign: opt-in editor benchmark evidence, not a runtime hot path.</remarks>
        public static List<CharacterTestRuntimeCatalogLoadBenchmarkRecord> Run()
        {
            return Run(Directory.GetCurrentDirectory());
        }

        /// <remarks>AllocByDesign: opt-in editor benchmark evidence, not a runtime hot path.</remarks>
        public static List<CharacterTestRuntimeCatalogLoadBenchmarkRecord> Run(string projectRoot)
        {
            var records = new List<CharacterTestRuntimeCatalogLoadBenchmarkRecord>();
            IReadOnlyList<CharacterTestRuntimeCatalogArtifact> artifacts =
                CharacterTestRuntimeCatalogArtifactManifest.Artifacts;
            string safeProjectRoot = string.IsNullOrWhiteSpace(projectRoot)
                ? Directory.GetCurrentDirectory()
                : projectRoot;
            for (int i = 0; i < artifacts.Count; i++)
            {
                CharacterTestRuntimeCatalogArtifact artifact = artifacts[i];
                if (!artifact.RequiresPlayerBinary)
                    continue;

                string jsonPath = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedJsonPath,
                    safeProjectRoot);
                string binaryPath = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedBinaryPath,
                    safeProjectRoot);
                records.Add(RunOne(artifact.CatalogKind, jsonPath, binaryPath));
            }

            return records;
        }

        private static CharacterTestRuntimeCatalogLoadBenchmarkRecord RunOne(
            string catalogKind,
            string jsonPath,
            string binaryPath)
        {
            var record = new CharacterTestRuntimeCatalogLoadBenchmarkRecord
            {
                CatalogKind = catalogKind ?? string.Empty,
                JsonPath = jsonPath ?? string.Empty,
                BinaryPath = binaryPath ?? string.Empty
            };

            CharacterTestRuntimeCatalogLoadBenchmarkStats jsonStats = Measure(
                () => LoadJsonStats(catalogKind, jsonPath),
                out bool jsonSuccess,
                out string jsonError,
                out double jsonElapsedMs,
                out long jsonAllocatedBytes);
            CharacterTestRuntimeCatalogLoadBenchmarkStats binaryStats = Measure(
                () => LoadBinaryStats(catalogKind, binaryPath),
                out bool binarySuccess,
                out string binaryError,
                out double binaryElapsedMs,
                out long binaryAllocatedBytes);

            record.JsonSuccess = jsonSuccess;
            record.BinarySuccess = binarySuccess;
            record.JsonError = jsonError;
            record.BinaryError = binaryError;
            record.JsonElapsedMs = jsonElapsedMs;
            record.BinaryElapsedMs = binaryElapsedMs;
            record.JsonAllocatedBytes = jsonAllocatedBytes;
            record.BinaryAllocatedBytes = binaryAllocatedBytes;
            record.JsonEntryCount = jsonStats.EntryCount;
            record.BinaryEntryCount = binaryStats.EntryCount;
            record.JsonStableHash = jsonStats.StableHash;
            record.BinaryStableHash = binaryStats.StableHash;
            record.EntryCountMatches = jsonSuccess && binarySuccess && jsonStats.EntryCount == binaryStats.EntryCount;
            record.StableHashMatches = jsonSuccess
                && binarySuccess
                && string.Equals(jsonStats.StableHash, binaryStats.StableHash, StringComparison.Ordinal);
            return record;
        }

        private static CharacterTestRuntimeCatalogLoadBenchmarkStats Measure(
            Func<CharacterTestRuntimeCatalogLoadBenchmarkStats> action,
            out bool success,
            out string error,
            out double elapsedMs,
            out long allocatedBytes)
        {
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                CharacterTestRuntimeCatalogLoadBenchmarkStats result = action();
                stopwatch.Stop();
                allocatedBytes = Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - beforeBytes);
                elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                success = true;
                error = string.Empty;
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                allocatedBytes = Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - beforeBytes);
                elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                success = false;
                error = ex.GetType().Name + ": " + ex.Message;
                return new CharacterTestRuntimeCatalogLoadBenchmarkStats(-1, string.Empty);
            }
        }

        private static CharacterTestRuntimeCatalogLoadBenchmarkStats LoadJsonStats(
            string catalogKind,
            string path)
        {
            switch (catalogKind)
            {
                case RuntimeCatalogBinaryKinds.AbilityDefinitions:
                    AbilityDefinitionRegistry abilities = AbilityDefinitionJsonImporter.ImportFile(path);
                    return Stats(abilities.Count, abilities.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.AbilityEffectTimelines:
                    AbilityEffectTimelineRegistry timelines = AbilityEffectTimelineJsonImporter.ImportFile(path);
                    return Stats(timelines.Count, timelines.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.GameplayBuffDefinitions:
                    GameplayBuffDefinitionRegistry buffs = GameplayBuffDefinitionJsonImporter.ImportFile(path);
                    return Stats(buffs.Count, buffs.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.GameplayBuffPresentationDefinitions:
                    GameplayBuffPresentationDefinitionRegistry presentations =
                        GameplayBuffPresentationDefinitionJsonImporter.ImportFile(path);
                    return Stats(presentations.Count, ComputePresentationHash(presentations));
                case RuntimeCatalogBinaryKinds.CombatGameplayAuraBuffDefinitions:
                    CombatGameplayAuraBuffDefinitionRegistry auras =
                        CombatGameplayAuraBuffDefinitionJsonImporter.ImportFile(path);
                    return Stats(auras.Count, auras.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.ProjectileDefinitions:
                    CombatProjectileDefinitionRegistry projectiles = CombatProjectileConfigJsonImporter.ImportFile(path);
                    return Stats(projectiles.Count, projectiles.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.ProjectileSpawnProfiles:
                    CombatProjectileSpawnProfileRegistry spawnProfiles =
                        CombatProjectileSpawnProfileConfigJsonImporter.ImportFile(path);
                    return Stats(spawnProfiles.Count, spawnProfiles.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.ProjectilePresentationBindings:
                    CombatProjectileUnityPresentationRegistry projectilePresentation =
                        CombatProjectileUnityPresentationConfigJsonImporter.ImportFile(path);
                    return Stats(projectilePresentation.Count, ComputeProjectilePresentationHash(projectilePresentation));
                case RuntimeCatalogBinaryKinds.VfxDefinitions:
                    VfxDefinitionRegistry vfxDefinitions = VfxConfigJsonImporter.ImportDefinitionFile(path);
                    return Stats(vfxDefinitions.Count, vfxDefinitions.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.VfxTriggerMaps:
                    VfxTriggerMapResolver triggerMap = VfxConfigJsonImporter.ImportTriggerMapFile(path);
                    return Stats(CountJsonArray(path, "mappings"), ComputeVfxTriggerMapHash(triggerMap));
                default:
                    throw new InvalidOperationException("Unsupported catalog kind: " + catalogKind);
            }
        }

        private static CharacterTestRuntimeCatalogLoadBenchmarkStats LoadBinaryStats(
            string catalogKind,
            string path)
        {
            switch (catalogKind)
            {
                case RuntimeCatalogBinaryKinds.AbilityDefinitions:
                    RuntimeCatalogBinaryLoadResult<AbilityDefinitionRegistry> abilities =
                        AbilityDefinitionBinaryImporter.ImportFile(path);
                    AssertLoaded(abilities);
                    return Stats(abilities.Header.EntryCount, abilities.Catalog.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.AbilityEffectTimelines:
                    RuntimeCatalogBinaryLoadResult<AbilityEffectTimelineRegistry> timelines =
                        AbilityEffectTimelineBinaryImporter.ImportFile(path);
                    AssertLoaded(timelines);
                    return Stats(timelines.Header.EntryCount, timelines.Catalog.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.GameplayBuffDefinitions:
                    RuntimeCatalogBinaryLoadResult<GameplayBuffDefinitionRegistry> buffs =
                        GameplayBuffDefinitionBinaryImporter.ImportFile(path);
                    AssertLoaded(buffs);
                    return Stats(buffs.Header.EntryCount, buffs.Catalog.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.GameplayBuffPresentationDefinitions:
                    RuntimeCatalogBinaryLoadResult<GameplayBuffPresentationDefinitionRegistry> presentations =
                        GameplayBuffPresentationDefinitionBinaryImporter.ImportFile(path);
                    AssertLoaded(presentations);
                    return Stats(presentations.Header.EntryCount, ComputePresentationHash(presentations.Catalog));
                case RuntimeCatalogBinaryKinds.CombatGameplayAuraBuffDefinitions:
                    RuntimeCatalogBinaryLoadResult<CombatGameplayAuraBuffDefinitionRegistry> auras =
                        CombatGameplayAuraBuffDefinitionBinaryImporter.ImportFile(path);
                    AssertLoaded(auras);
                    return Stats(auras.Header.EntryCount, auras.Catalog.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.ProjectileDefinitions:
                    RuntimeCatalogBinaryLoadResult<CombatProjectileDefinitionRegistry> projectiles =
                        CombatProjectileConfigBinaryImporter.ImportFile(path);
                    AssertLoaded(projectiles);
                    return Stats(projectiles.Header.EntryCount, projectiles.Catalog.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.ProjectileSpawnProfiles:
                    RuntimeCatalogBinaryLoadResult<CombatProjectileSpawnProfileRegistry> spawnProfiles =
                        CombatProjectileSpawnProfileConfigBinaryImporter.ImportFile(path);
                    AssertLoaded(spawnProfiles);
                    return Stats(spawnProfiles.Header.EntryCount, spawnProfiles.Catalog.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.ProjectilePresentationBindings:
                    RuntimeCatalogBinaryLoadResult<CombatProjectileUnityPresentationRegistry> projectilePresentation =
                        CombatProjectileUnityPresentationConfigBinaryImporter.ImportFile(path);
                    AssertLoaded(projectilePresentation);
                    return Stats(
                        projectilePresentation.Header.EntryCount,
                        ComputeProjectilePresentationHash(projectilePresentation.Catalog));
                case RuntimeCatalogBinaryKinds.VfxDefinitions:
                    RuntimeCatalogBinaryLoadResult<VfxDefinitionRegistry> vfxDefinitions =
                        VfxConfigBinaryImporter.ImportDefinitionFile(path);
                    AssertLoaded(vfxDefinitions);
                    return Stats(vfxDefinitions.Header.EntryCount, vfxDefinitions.Catalog.ComputeStableHash());
                case RuntimeCatalogBinaryKinds.VfxTriggerMaps:
                    RuntimeCatalogBinaryLoadResult<VfxTriggerMapResolver> triggerMap =
                        VfxConfigBinaryImporter.ImportTriggerMapFile(path);
                    AssertLoaded(triggerMap);
                    return Stats(triggerMap.Header.EntryCount, ComputeVfxTriggerMapHash(triggerMap.Catalog));
                default:
                    throw new InvalidOperationException("Unsupported catalog kind: " + catalogKind);
            }
        }

        private static void AssertLoaded<TCatalog>(RuntimeCatalogBinaryLoadResult<TCatalog> result)
        {
            if (result == null || !result.Success)
            {
                string reason = result == null ? "null" : result.FallbackReason;
                string diagnostics = result == null ? string.Empty : string.Join("; ", result.Diagnostics);
                throw new InvalidOperationException(reason + ": " + diagnostics);
            }
        }

        private static CharacterTestRuntimeCatalogLoadBenchmarkStats Stats(int count, long hash)
        {
            return new CharacterTestRuntimeCatalogLoadBenchmarkStats(
                count,
                hash.ToString(CultureInfo.InvariantCulture));
        }

        private static long ComputePresentationHash(GameplayBuffPresentationDefinitionRegistry registry)
        {
            GameplayBuffPresentationDefinition[] definitions = registry.ToArray();
            var builder = new StringBuilder(definitions.Length * 128);
            for (int i = 0; i < definitions.Length; i++)
            {
                GameplayBuffPresentationDefinition definition = definitions[i];
                builder.Append(definition.BuffId).Append('|')
                    .Append(definition.DisplayName).Append('|')
                    .Append(definition.NameKey).Append('|')
                    .Append(definition.ShortName).Append('|')
                    .Append(definition.Description).Append('|')
                    .Append(definition.DescriptionMode).Append('|')
                    .Append(definition.IconResourceKey).Append('|')
                    .Append((int)definition.Polarity).Append('|')
                    .Append(definition.Priority).Append('|')
                    .Append(definition.ShowOnPlayerHud ? '1' : '0').Append('|')
                    .Append(definition.ShowOnTargetHud ? '1' : '0').Append('|')
                    .Append(definition.ShowOnHeadPad ? '1' : '0').Append('|')
                    .Append(definition.EffectSummary).Append('\n');
            }

            return ComputeStringHash(builder.ToString());
        }

        private static long ComputeProjectilePresentationHash(CombatProjectileUnityPresentationRegistry registry)
        {
            var bindings = new List<CombatProjectileUnityPresentationBindingV2>();
            registry.CopyBindingsV2To(bindings);
            bindings.Sort(CompareProjectileBindings);
            var builder = new StringBuilder(bindings.Count * 96);
            for (int i = 0; i < bindings.Count; i++)
            {
                CombatProjectileUnityPresentationBindingV2 binding = bindings[i];
                AppendResourceKey(builder, binding.FlightBodyResource);
                builder.Append(binding.ProjectileDefinitionId).Append('|')
                    .Append(binding.ViewNamePrefix).Append('\n');
            }

            return ComputeStringHash(builder.ToString());
        }

        private static long ComputeVfxTriggerMapHash(VfxTriggerMapResolver resolver)
        {
            var output = new List<VfxSpawnRequest>();
            int count = resolver.Resolve(
                new VfxTriggerContext(
                    new RuntimeFrame(1),
                    VfxSourceDomains.CombatHit,
                    95265724,
                    0,
                    0,
                    0L,
                    0,
                    0,
                    VfxAnchor.World(VfxPose.Identity),
                    VfxParameterBlock.Empty,
                    new RuntimeTraceIdentity(VfxSourceDomains.CombatHit, 95265724)),
                output);
            var builder = new StringBuilder(64);
            builder.Append(count);
            for (int i = 0; i < output.Count; i++)
            {
                builder.Append('|')
                    .Append(output[i].DefinitionId.Value)
                    .Append('|')
                    .Append(output[i].LifetimeFramesOverride);
            }

            return ComputeStringHash(builder.ToString());
        }

        private static int CountJsonArray(string path, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return -1;

            JObject root = JObject.Parse(File.ReadAllText(path));
            return root[propertyName] is JArray array ? array.Count : 0;
        }

        private static int CompareProjectileBindings(
            CombatProjectileUnityPresentationBindingV2 left,
            CombatProjectileUnityPresentationBindingV2 right)
        {
            return left.ProjectileDefinitionId.CompareTo(right.ProjectileDefinitionId);
        }

        private static void AppendResourceKey(StringBuilder builder, ResourceKey key)
        {
            builder.Append(key.Id).Append('|')
                .Append(key.TypeId).Append('|')
                .Append(key.Variant).Append('|')
                .Append(key.PackageId).Append('|');
        }

        private static long ComputeStringHash(string value)
        {
            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offsetBasis;
                string safeValue = value ?? string.Empty;
                for (int i = 0; i < safeValue.Length; i++)
                {
                    hash ^= safeValue[i];
                    hash *= prime;
                }

                return (long)hash;
            }
        }
    }
}
