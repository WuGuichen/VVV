using System;
using System.IO;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.RuntimeCatalog
{
    public sealed class CharacterTestRuntimeCatalogPhase2Tests
    {
        [Test]
        public void PathResolver_EditorPrefersGenerated_PlayerPrefersStreamingAssetsMirror()
        {
            var editorContext = new CharacterTestRuntimeCatalogPathResolverContext(
                preferPlayerMirror: false,
                projectRootPath: "/project",
                streamingAssetsPath: "/streaming");
            CharacterTestRuntimeCatalogPathCandidate[] editorCandidates =
                CharacterTestRuntimeCatalogPathResolver.ResolveCandidates(
                    "Assets/MxFrameworkGenerated/Abilities/ability_catalog.json",
                    editorContext);

            Assert.AreEqual(2, editorCandidates.Length);
            Assert.AreEqual("generated", editorCandidates[0].SourceKind);
            Assert.AreEqual("playerMirror", editorCandidates[1].SourceKind);
            StringAssert.EndsWith(
                "Assets/MxFrameworkGenerated/Abilities/ability_catalog.json",
                Normalize(editorCandidates[0].JsonPath));
            StringAssert.EndsWith(
                "MxFramework/CharacterTest/RuntimeCatalogs/Abilities/ability_catalog.json",
                Normalize(editorCandidates[1].JsonPath));

            var playerContext = new CharacterTestRuntimeCatalogPathResolverContext(
                preferPlayerMirror: true,
                projectRootPath: "/project",
                streamingAssetsPath: "/streaming");
            CharacterTestRuntimeCatalogPathCandidate[] playerCandidates =
                CharacterTestRuntimeCatalogPathResolver.ResolveCandidates(
                    "Assets/MxFrameworkGenerated/Abilities/ability_catalog.json",
                    playerContext);

            Assert.AreEqual(2, playerCandidates.Length);
            Assert.AreEqual("playerMirror", playerCandidates[0].SourceKind);
            Assert.AreEqual("generated", playerCandidates[1].SourceKind);
        }

        [Test]
        public void PlayerArtifactExporter_CopiesAllCatalogsAndReportsStaleOrMissingSources()
        {
            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                "MxFrameworkCharacterTestCatalogArtifacts_" + Guid.NewGuid().ToString("N"));
            string projectRoot = Path.Combine(tempRoot, "Project");
            string streamingAssetsPath = Path.Combine(projectRoot, "Assets", "StreamingAssets");
            try
            {
                CreateGeneratedCatalogFiles(projectRoot);

                CharacterTestRuntimeCatalogPlayerArtifactExportReport first =
                    CharacterTestRuntimeCatalogPlayerArtifactExporter.Refresh(projectRoot, streamingAssetsPath);
                Assert.IsFalse(first.HasBlockingIssues, string.Join("; ", first.Issues));
                Assert.AreEqual(CharacterTestRuntimeCatalogArtifactManifest.Artifacts.Count, first.Entries.Count);
                Assert.AreEqual(ExpectedPlayerMirrorFileCount(), first.CopiedCount);
                Assert.AreEqual(0, first.UpdatedCount);
                Assert.AreEqual(0, first.HashMismatchCount);
                AssertMirrorFilesExist(streamingAssetsPath);
                string sourceIdentity = CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureRuntime(
                    projectRoot, source: true);
                Assert.AreEqual(sourceIdentity,
                    CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureRuntime(
                        streamingAssetsPath, source: false));

                CharacterTestRuntimeCatalogPlayerArtifactExportReport second =
                    CharacterTestRuntimeCatalogPlayerArtifactExporter.Refresh(projectRoot, streamingAssetsPath);
                Assert.IsFalse(second.HasBlockingIssues, string.Join("; ", second.Issues));
                Assert.AreEqual(ExpectedPlayerMirrorFileCount(), second.SkippedCount);

                CharacterTestRuntimeCatalogArtifact firstArtifact =
                    CharacterTestRuntimeCatalogArtifactManifest.Artifacts[0];
                string generatedJson = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    firstArtifact.GeneratedJsonPath,
                    projectRoot);
                File.AppendAllText(generatedJson, "\nstale-check");
                Assert.AreNotEqual(sourceIdentity,
                    CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureRuntime(
                        projectRoot, source: true));
                CharacterTestRuntimeCatalogPlayerArtifactExportReport stale =
                    CharacterTestRuntimeCatalogPlayerArtifactExporter.Refresh(projectRoot, streamingAssetsPath);
                Assert.IsFalse(stale.HasBlockingIssues, string.Join("; ", stale.Issues));
                Assert.AreEqual(1, stale.UpdatedCount);
                Assert.AreEqual(1, stale.HashMismatchCount);

                string generatedBytes = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    firstArtifact.GeneratedBinaryPath,
                    projectRoot);
                File.Delete(generatedBytes);
                CharacterTestRuntimeCatalogPlayerArtifactExportReport missingBytes =
                    CharacterTestRuntimeCatalogPlayerArtifactExporter.Refresh(projectRoot, streamingAssetsPath);
                Assert.IsTrue(missingBytes.HasBlockingIssues);
                StringAssert.Contains("generated JSON exists but .bytes is missing", string.Join("\n", missingBytes.Issues));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void MirrorValidator_ReportsMismatchAndMissingJsonWithoutRefreshing()
        {
            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                "MxFrameworkCharacterTestCatalogMirrorValidation_" + Guid.NewGuid().ToString("N"));
            string projectRoot = Path.Combine(tempRoot, "Project");
            string streamingAssetsPath = Path.Combine(projectRoot, "Assets", "StreamingAssets");
            try
            {
                CreateGeneratedCatalogFiles(projectRoot);

                CharacterTestRuntimeCatalogPlayerArtifactExportReport refresh =
                    CharacterTestRuntimeCatalogPlayerArtifactExporter.Refresh(projectRoot, streamingAssetsPath);
                Assert.IsFalse(refresh.HasBlockingIssues, string.Join("; ", refresh.Issues));

                CharacterTestRuntimeCatalogMirrorValidationReport clean =
                    CharacterTestRuntimeCatalogMirrorValidator.Validate(
                        projectRoot,
                        streamingAssetsPath,
                        validateGeneratedBinarySemantics: false);
                Assert.IsFalse(clean.HasIssues);
                Assert.AreEqual(CharacterTestRuntimeCatalogArtifactManifest.Artifacts.Count, clean.MatchedCount);

                CharacterTestRuntimeCatalogArtifact firstArtifact =
                    CharacterTestRuntimeCatalogArtifactManifest.Artifacts[0];
                string mirrorJson = CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                    firstArtifact.PlayerMirrorRelativeJsonPath,
                    streamingAssetsPath);
                File.AppendAllText(mirrorJson, "\nstale-mirror");
                Assert.AreNotEqual(
                    CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureRuntime(
                        projectRoot, source: true),
                    CharacterTestPackageTestCommand.CharacterTestPackageInputSnapshot.CaptureRuntime(
                        streamingAssetsPath, source: false));

                CharacterTestRuntimeCatalogMirrorValidationReport mismatch =
                    CharacterTestRuntimeCatalogMirrorValidator.Validate(
                        projectRoot,
                        streamingAssetsPath,
                        validateGeneratedBinarySemantics: false);
                Assert.IsTrue(mismatch.HasIssues);
                Assert.AreEqual(1, mismatch.MismatchCount);
                Assert.AreEqual(0, mismatch.MissingCount);
                AssertStatus(
                    mismatch,
                    firstArtifact.CatalogKind,
                    CharacterTestRuntimeCatalogMirrorValidator.StatusMismatch);
                StringAssert.Contains("stale-mirror", File.ReadAllText(mirrorJson));

                File.Delete(mirrorJson);
                CharacterTestRuntimeCatalogMirrorValidationReport missing =
                    CharacterTestRuntimeCatalogMirrorValidator.Validate(
                        projectRoot,
                        streamingAssetsPath,
                        validateGeneratedBinarySemantics: false);
                Assert.IsTrue(missing.HasIssues);
                Assert.AreEqual(0, missing.MismatchCount);
                Assert.AreEqual(1, missing.MissingCount);
                AssertStatus(
                    missing,
                    firstArtifact.CatalogKind,
                    CharacterTestRuntimeCatalogMirrorValidator.StatusMirrorMissing);

                string generatedJson = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    firstArtifact.GeneratedJsonPath,
                    projectRoot);
                File.WriteAllText(mirrorJson, File.ReadAllText(generatedJson));
                string mirrorBytes = CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                    firstArtifact.PlayerMirrorRelativeBinaryPath,
                    streamingAssetsPath);
                File.AppendAllText(mirrorBytes, "\nstale-bytes");
                CharacterTestRuntimeCatalogMirrorValidationReport binaryMismatch =
                    CharacterTestRuntimeCatalogMirrorValidator.Validate(
                        projectRoot,
                        streamingAssetsPath,
                        validateGeneratedBinarySemantics: false);
                Assert.IsTrue(binaryMismatch.HasIssues);
                AssertStatus(
                    binaryMismatch,
                    firstArtifact.CatalogKind,
                    CharacterTestRuntimeCatalogMirrorValidator.StatusBinaryMirrorMismatch);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void MirrorAuthoringValidation_FormatsWarningWithCatalogKindAndPaths()
        {
            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                "MxFrameworkCharacterTestCatalogMirrorDiagnostics_" + Guid.NewGuid().ToString("N"));
            string projectRoot = Path.Combine(tempRoot, "Project");
            string streamingAssetsPath = Path.Combine(projectRoot, "Assets", "StreamingAssets");
            try
            {
                CreateGeneratedCatalogFiles(projectRoot);
                CharacterTestRuntimeCatalogPlayerArtifactExporter.Refresh(projectRoot, streamingAssetsPath);

                CharacterTestRuntimeCatalogArtifact artifact =
                    CharacterTestRuntimeCatalogArtifactManifest.Artifacts[0];
                string mirrorJson = CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                    artifact.PlayerMirrorRelativeJsonPath,
                    streamingAssetsPath);
                File.AppendAllText(mirrorJson, "\nstale-mirror");

                CharacterTestRuntimeCatalogMirrorValidationReport report =
                    CharacterTestRuntimeCatalogMirrorValidator.Validate(
                        projectRoot,
                        streamingAssetsPath,
                        validateGeneratedBinarySemantics: false);
                CharacterActionValidationDiagnostic[] diagnostics =
                    CharacterTestRuntimeCatalogMirrorAuthoringValidationProvider.ToDiagnostics(report);

                Assert.AreEqual(1, diagnostics.Length);
                Assert.AreEqual("RUNTIME_CATALOG_MIRROR_MISMATCH", diagnostics[0].Code);
                Assert.AreEqual(CharacterActionDiagnosticSeverity.Warning, diagnostics[0].Severity);
                StringAssert.Contains(artifact.CatalogKind, diagnostics[0].Message);
                StringAssert.Contains(Normalize(mirrorJson), Normalize(diagnostics[0].Message));
                StringAssert.Contains("Repair Runtime Catalog Artifacts", diagnostics[0].Message);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void MirrorAuthoringValidation_ReportsGeneratedBinarySemanticMismatch()
        {
            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                "MxFrameworkCharacterTestCatalogBinaryDiagnostics_" + Guid.NewGuid().ToString("N"));
            string projectRoot = Path.Combine(tempRoot, "Project");
            string streamingAssetsPath = Path.Combine(projectRoot, "Assets", "StreamingAssets");
            try
            {
                CopyCurrentGeneratedCatalogFiles(projectRoot);
                CharacterTestRuntimeCatalogPlayerArtifactExporter.Refresh(projectRoot, streamingAssetsPath);

                CharacterTestRuntimeCatalogArtifact projectileArtifact;
                Assert.IsTrue(CharacterTestRuntimeCatalogArtifactManifest.TryGetByCatalogKind(
                    RuntimeCatalogBinaryKinds.ProjectileDefinitions,
                    out projectileArtifact));
                string generatedJson = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    projectileArtifact.GeneratedJsonPath,
                    projectRoot);
                MutateFirstImpactPower(generatedJson);

                string mirrorJson = CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                    projectileArtifact.PlayerMirrorRelativeJsonPath,
                    streamingAssetsPath);
                File.Copy(generatedJson, mirrorJson, overwrite: true);

                CharacterTestRuntimeCatalogMirrorValidationReport report =
                    CharacterTestRuntimeCatalogMirrorValidator.Validate(projectRoot, streamingAssetsPath);
                CharacterActionValidationDiagnostic[] diagnostics =
                    CharacterTestRuntimeCatalogMirrorAuthoringValidationProvider.ToDiagnostics(report);

                AssertStatus(
                    report,
                    RuntimeCatalogBinaryKinds.ProjectileDefinitions,
                    CharacterTestRuntimeCatalogMirrorValidator.StatusBinarySemanticMismatch);
                Assert.AreEqual(1, diagnostics.Length);
                Assert.AreEqual("RUNTIME_CATALOG_BINARY_MISMATCH", diagnostics[0].Code);
                StringAssert.Contains("cannot fix this", diagnostics[0].Message);
                StringAssert.Contains("Repair Runtime Catalog Artifacts", diagnostics[0].Message);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void RepairReport_IncludesMenuArtifactsAndValidationSummary()
        {
            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                "MxFrameworkCharacterTestCatalogRepairReport_" + Guid.NewGuid().ToString("N"));
            string projectRoot = Path.Combine(tempRoot, "Project");
            string streamingAssetsPath = Path.Combine(projectRoot, "Assets", "StreamingAssets");
            try
            {
                CreateGeneratedCatalogFiles(projectRoot);
                CharacterTestRuntimeCatalogPlayerArtifactExportReport refresh =
                    CharacterTestRuntimeCatalogPlayerArtifactExporter.Refresh(projectRoot, streamingAssetsPath);
                CharacterTestRuntimeCatalogArtifact firstArtifact =
                    CharacterTestRuntimeCatalogArtifactManifest.Artifacts[0];
                var binaryReport = new CharacterTestRuntimeCatalogGeneratedBinaryValidationReport();
                binaryReport.AddEntry(new CharacterTestRuntimeCatalogGeneratedBinaryValidationEntry
                {
                    CatalogKind = firstArtifact.CatalogKind,
                    JsonPath = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                        firstArtifact.GeneratedJsonPath,
                        projectRoot),
                    BinaryPath = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                        firstArtifact.GeneratedBinaryPath,
                        projectRoot),
                    JsonSuccess = true,
                    BinarySuccess = true,
                    EntryCountMatches = true,
                    StableHashMatches = true
                });
                var report = new CharacterTestRuntimeCatalogArtifactRepairReport
                {
                    AbilityRuntimeBinaryExport = new CharacterTestRuntimeCatalogRepairCommandResult
                    {
                        CommandName = "ability export-runtime-binaries",
                        ExitCode = 0
                    },
                    BuffRuntimeBinaryExport = new CharacterTestRuntimeCatalogRepairCommandResult
                    {
                        CommandName = "buff export-runtime-binaries",
                        ExitCode = 0
                    },
                    RefreshReport = refresh,
                    GeneratedBinaryValidationReport = binaryReport
                };

                string text = Normalize(report.ToReportText());
                StringAssert.Contains(CharacterTestRuntimeCatalogArtifactRepairTool.MenuPath, text);
                StringAssert.Contains(firstArtifact.CatalogKind, text);
                StringAssert.Contains(firstArtifact.GeneratedJsonPath, text);
                StringAssert.Contains("generatedBinaryValidation.hasIssues: false", text);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void PlayerRunReportParser_RecognizesAllBinaryAndCatalogMisses()
        {
            string allBinaryReport = BuildRuntimeCatalogReport(null);
            CharacterTestRuntimeCatalogLoadReportSummary allBinary =
                CharacterTestRuntimeCatalogLoadReportParser.Parse(allBinaryReport);

            Assert.AreEqual(CharacterTestRuntimeCatalogArtifactManifest.RequiredPlayerBinaryCount, allBinary.ExpectedCount);
            Assert.AreEqual(CharacterTestRuntimeCatalogArtifactManifest.RequiredPlayerBinaryCount, allBinary.BinaryCount);
            Assert.AreEqual(0, allBinary.JsonCount);
            Assert.AreEqual(0, allBinary.EmbeddedCount);
            Assert.IsTrue(allBinary.AllExpectedBinary);
            Assert.IsEmpty(allBinary.MissingExpectedKinds);
            Assert.IsEmpty(allBinary.NonBinaryExpectedKinds);

            string jsonFallbackReport = BuildRuntimeCatalogReport(RuntimeCatalogBinaryKinds.VfxTriggerMaps);
            CharacterTestRuntimeCatalogLoadReportSummary jsonFallback =
                CharacterTestRuntimeCatalogLoadReportParser.Parse(jsonFallbackReport);

            Assert.IsFalse(jsonFallback.AllExpectedBinary);
            CollectionAssert.Contains(jsonFallback.NonBinaryExpectedKinds, RuntimeCatalogBinaryKinds.VfxTriggerMaps);

            string missingReport = BuildRuntimeCatalogReport(RuntimeCatalogBinaryKinds.VfxTriggerMaps, omitOverrideKind: true);
            CharacterTestRuntimeCatalogLoadReportSummary missing =
                CharacterTestRuntimeCatalogLoadReportParser.Parse(missingReport);

            Assert.IsFalse(missing.AllExpectedBinary);
            CollectionAssert.Contains(missing.MissingExpectedKinds, RuntimeCatalogBinaryKinds.VfxTriggerMaps);
        }

        [Test]
        public void PackageCatalogGate_RequiresManifestRecordsAndConsistentSummary()
        {
            string valid = BuildStrictRuntimeCatalogReport();
            Assert.IsTrue(CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(valid).AllExpectedBinary);

            CharacterTestRuntimeCatalogArtifact first = CharacterTestRuntimeCatalogArtifactManifest.Artifacts[0];
            string firstKind = first.CatalogKind;
            string missing = valid.Replace("runtimeCatalogLoad.0.kind: " + firstKind + Environment.NewLine, string.Empty);
            Assert.Throws<FormatException>(() => CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(missing));
            string json = valid.Replace("runtimeCatalogLoad.0.format: Binary", "runtimeCatalogLoad.0.format: Json");
            Assert.Throws<FormatException>(() => CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(json));
            string embedded = valid.Replace("runtimeCatalogLoad.0.format: Binary", "runtimeCatalogLoad.0.format: Embedded");
            Assert.Throws<FormatException>(() => CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(embedded));
            Assert.Throws<FormatException>(() => CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(
                valid.Replace("runtimeCatalogLoad.expectedCount: "
                    + CharacterTestRuntimeCatalogArtifactManifest.RequiredPlayerBinaryCount,
                    "runtimeCatalogLoad.expectedCount: 0")));
            Assert.Throws<FormatException>(() => CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(
                valid.Replace("runtimeCatalogLoad.binaryCount: "
                    + CharacterTestRuntimeCatalogArtifactManifest.RequiredPlayerBinaryCount,
                    "runtimeCatalogLoad.binaryCount: 0")));
            Assert.Throws<FormatException>(() => CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(
                valid.Replace("runtimeCatalogLoad.nonBinaryExpectedKinds: none",
                    "runtimeCatalogLoad.nonBinaryExpectedKinds: " + firstKind)));
            Assert.Throws<FormatException>(() => CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(
                valid + "runtimeCatalogLoad.0.format: Json\n"));
            Assert.Throws<FormatException>(() => CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(
                valid.Replace("runtimeCatalogLoad.1.kind: ", "runtimeCatalogLoad.1.kind: " + firstKind + "#")));
            Assert.Throws<FormatException>(() => CharacterTestRuntimeCatalogLoadReportParser.ParseStrict(
                valid.Replace("runtimeCatalogLoad.0.kind: " + firstKind,
                    "runtimeCatalogLoad.0.kind: unrelated.catalog")));
        }

        [Test]
        public void LoadBenchmark_AllGeneratedCatalogsMatchJsonAndBinary()
        {
            var records = CharacterTestRuntimeCatalogLoadBenchmark.Run();
            Assert.AreEqual(CharacterTestRuntimeCatalogArtifactManifest.RequiredPlayerBinaryCount, records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                Assert.IsTrue(records[i].JsonSuccess, records[i].CatalogKind + " json: " + records[i].JsonError);
                Assert.IsTrue(records[i].BinarySuccess, records[i].CatalogKind + " binary: " + records[i].BinaryError);
                Assert.IsTrue(records[i].EntryCountMatches, records[i].CatalogKind + " entry count mismatch");
                Assert.IsTrue(records[i].StableHashMatches, records[i].CatalogKind + " stable hash mismatch");
            }
        }

        private static void CreateGeneratedCatalogFiles(string projectRoot)
        {
            foreach (CharacterTestRuntimeCatalogArtifact artifact in CharacterTestRuntimeCatalogArtifactManifest.Artifacts)
            {
                string jsonPath = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedJsonPath,
                    projectRoot);
                string binaryPath = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedBinaryPath,
                    projectRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));
                File.WriteAllText(jsonPath, "json:" + artifact.CatalogKind);
                if (artifact.RequiresPlayerBinary)
                    File.WriteAllText(binaryPath, "bytes:" + artifact.CatalogKind);
            }
        }

        private static void CopyCurrentGeneratedCatalogFiles(string projectRoot)
        {
            string currentProjectRoot = Directory.GetCurrentDirectory();
            foreach (CharacterTestRuntimeCatalogArtifact artifact in CharacterTestRuntimeCatalogArtifactManifest.Artifacts)
            {
                string sourceJson = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedJsonPath,
                    currentProjectRoot);
                string targetJson = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedJsonPath,
                    projectRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(targetJson));
                File.Copy(sourceJson, targetJson, overwrite: true);

                if (!artifact.RequiresPlayerBinary)
                    continue;

                string sourceBinary = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedBinaryPath,
                    currentProjectRoot);
                string targetBinary = CharacterTestRuntimeCatalogPathResolver.ResolveProjectPath(
                    artifact.GeneratedBinaryPath,
                    projectRoot);
                File.Copy(sourceBinary, targetBinary, overwrite: true);
            }
        }

        private static void MutateFirstImpactPower(string jsonPath)
        {
            string json = File.ReadAllText(jsonPath);
            const string marker = "\"impactPower\":";
            int markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(markerIndex, 0, "Expected impactPower in " + jsonPath);
            int valueStart = markerIndex + marker.Length;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            int valueEnd = valueStart;
            while (valueEnd < json.Length && (char.IsDigit(json[valueEnd]) || json[valueEnd] == '-'))
                valueEnd++;

            string oldValue = json.Substring(valueStart, valueEnd - valueStart);
            string newValue = string.Equals(oldValue, "34", StringComparison.Ordinal) ? "35" : "34";
            File.WriteAllText(jsonPath, json.Substring(0, valueStart) + newValue + json.Substring(valueEnd));
        }

        private static void AssertMirrorFilesExist(string streamingAssetsPath)
        {
            foreach (CharacterTestRuntimeCatalogArtifact artifact in CharacterTestRuntimeCatalogArtifactManifest.Artifacts)
            {
                string jsonPath = CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                    artifact.PlayerMirrorRelativeJsonPath,
                    streamingAssetsPath);
                string binaryPath = CharacterTestRuntimeCatalogPathResolver.ResolveStreamingAssetsMirrorPath(
                    artifact.PlayerMirrorRelativeBinaryPath,
                    streamingAssetsPath);
                Assert.IsTrue(File.Exists(jsonPath), jsonPath);
                if (artifact.RequiresPlayerBinary)
                    Assert.IsTrue(File.Exists(binaryPath), binaryPath);
            }
        }

        private static int ExpectedPlayerMirrorFileCount()
        {
            return CharacterTestRuntimeCatalogArtifactManifest.Artifacts.Count
                + CharacterTestRuntimeCatalogArtifactManifest.RequiredPlayerBinaryCount;
        }

        private static void AssertStatus(
            CharacterTestRuntimeCatalogMirrorValidationReport report,
            string catalogKind,
            string status)
        {
            for (int i = 0; i < report.Entries.Count; i++)
            {
                CharacterTestRuntimeCatalogMirrorValidationEntry entry = report.Entries[i];
                if (string.Equals(entry.CatalogKind, catalogKind, StringComparison.Ordinal))
                {
                    Assert.AreEqual(status, entry.Status);
                    return;
                }
            }

            Assert.Fail("Expected catalog kind " + catalogKind + ".");
        }

        private static string BuildRuntimeCatalogReport(string overrideKind, bool omitOverrideKind = false)
        {
            var builder = new StringBuilder();
            int index = 0;
            foreach (CharacterTestRuntimeCatalogArtifact artifact in CharacterTestRuntimeCatalogArtifactManifest.Artifacts)
            {
                if (omitOverrideKind && string.Equals(artifact.CatalogKind, overrideKind, StringComparison.Ordinal))
                    continue;

                string prefix = "runtimeCatalogLoad." + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                builder.Append(prefix).Append(".kind: ").AppendLine(artifact.CatalogKind);
                builder.Append(prefix).Append(".format: ").AppendLine(
                    string.Equals(artifact.CatalogKind, overrideKind, StringComparison.Ordinal)
                        ? "Json"
                        : "Binary");
                builder.Append(prefix).Append(".entryCount: 1\n");
                builder.Append(prefix).Append(".sourcePath: memory\n");
                index++;
            }

            builder.Append("runtimeCatalogLoad.count: ")
                .AppendLine(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string BuildStrictRuntimeCatalogReport()
        {
            var builder = new StringBuilder();
            int index = 0;
            foreach (CharacterTestRuntimeCatalogArtifact artifact in CharacterTestRuntimeCatalogArtifactManifest.Artifacts)
            {
                if (!artifact.RequiresPlayerBinary)
                    continue;
                builder.Append("runtimeCatalogLoad.").Append(index).Append(".kind: ")
                    .AppendLine(artifact.CatalogKind);
                builder.Append("runtimeCatalogLoad.").Append(index).AppendLine(".format: Binary");
                index++;
            }
            builder.Append("runtimeCatalogLoad.count: ").AppendLine(index.ToString());
            builder.Append("runtimeCatalogLoad.expectedCount: ").AppendLine(index.ToString());
            builder.Append("runtimeCatalogLoad.binaryCount: ").AppendLine(index.ToString());
            builder.AppendLine("runtimeCatalogLoad.jsonCount: 0");
            builder.AppendLine("runtimeCatalogLoad.embeddedCount: 0");
            builder.AppendLine("runtimeCatalogLoad.missingExpectedKinds: none");
            builder.AppendLine("runtimeCatalogLoad.nonBinaryExpectedKinds: none");
            builder.AppendLine("runtimeCatalogLoad.allExpectedBinary: true");
            return builder.ToString();
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
