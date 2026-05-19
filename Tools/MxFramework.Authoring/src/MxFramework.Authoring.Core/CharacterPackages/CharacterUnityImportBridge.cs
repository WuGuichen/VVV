using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MxFramework.Authoring
{
    public static class CharacterUnityImportBridgeFormats
    {
        public const string ImportReport = "mx.characterUnityImportReport.v1";
    }

    public static class CharacterUnityImportBridgeWriteActions
    {
        public const string Added = "Added";
        public const string Updated = "Updated";
        public const string Skipped = "Skipped";
        public const string Conflict = "Conflict";
        public const string Error = "Error";
    }

    public sealed class CharacterUnityImportBridgeRequest
    {
        public CharacterAuthoringCompileResult CompileResult { get; set; }
        public string PackageRootPath { get; set; } = string.Empty;
        public string ProjectRootPath { get; set; } = string.Empty;
        public string SourcePackageVersion { get; set; } = string.Empty;
        public bool DryRun { get; set; }
        public List<CharacterUnityImportGeneratedArtifact> GeneratedArtifacts { get; set; } = new List<CharacterUnityImportGeneratedArtifact>();
        public List<CharacterUnityImportWriteInput> AdditionalWrites { get; set; } = new List<CharacterUnityImportWriteInput>();
    }

    public sealed class CharacterUnityImportGeneratedArtifact
    {
        public string SourcePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
    }

    public sealed class CharacterUnityImportWriteInput
    {
        public string Kind { get; set; } = string.Empty;
        public string Owner { get; set; } = CharacterAuthoringCompilerOwnerKinds.UnityImporter;
        public string SourcePath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string WritePolicy { get; set; } = "Recreate";
        public string ContentHash { get; set; } = string.Empty;
        public string SourceFilePath { get; set; } = string.Empty;
        public string Content { get; set; } = null;
    }

    public sealed class CharacterUnityImportReport
    {
        public string Format { get; set; } = CharacterUnityImportBridgeFormats.ImportReport;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string PackageStableId { get; set; } = string.Empty;
        public string SourcePackageVersion { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool DryRun { get; set; }
        public bool CanWriteToUnityProject { get; set; }
        public bool CanSpawnAfterImport { get; set; }
        public string TargetRootPath { get; set; } = string.Empty;
        public string ReportPath { get; set; } = string.Empty;
        public string SourcePackageHash { get; set; } = string.Empty;
        public string GeneratedConfigHash { get; set; } = string.Empty;
        public string GeometryBindingHash { get; set; } = string.Empty;
        public string ResourceMappingHash { get; set; } = string.Empty;
        public string WritePlanHash { get; set; } = string.Empty;
        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int ConflictCount { get; set; }
        public int ErrorCount { get; set; }
        public List<CharacterUnityImportOperation> Operations { get; set; } = new List<CharacterUnityImportOperation>();
        public List<CharacterAuthoringValidationIssue> Issues { get; set; } = new List<CharacterAuthoringValidationIssue>();
    }

    public sealed class CharacterUnityImportOperation
    {
        public string Kind { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string WritePolicy { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string ExistingHash { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public static class CharacterUnityImportBridge
    {
        public static CharacterUnityImportReport Execute(CharacterUnityImportBridgeRequest request)
        {
            if (request == null)
                request = new CharacterUnityImportBridgeRequest();

            CharacterAuthoringCompileResult compileResult = request.CompileResult;
            var report = CreateReportSkeleton(compileResult, request.DryRun, request.SourcePackageVersion);
            if (compileResult == null)
            {
                AddBridgeError(report, "CHARPKG_IMPORT_COMPILE_RESULT_MISSING", "compiler", "compileResult", "CompileResult", "Unity import bridge requires a compiler result.");
                FinalizeReportStatus(report, CharacterAuthoringCompilerStatus.ImportBlocked.ToString());
                return report;
            }

            CharacterUnityImportWritePlan writePlan = compileResult.UnityImportWritePlan;
            if (writePlan == null)
            {
                AddBridgeError(report, "CHARPKG_IMPORT_WRITE_PLAN_MISSING", "compiler", "unityImportWritePlan", "UnityImportWritePlan", "Compiler result did not include a Unity import/write plan.");
                FinalizeReportStatus(report, CharacterAuthoringCompilerStatus.ImportBlocked.ToString());
                return report;
            }

            CopyGateIssues(report, compileResult.GateReport);
            if (!writePlan.CanWriteToUnityProject)
            {
                report.CanWriteToUnityProject = false;
                report.CanSpawnAfterImport = false;
                FinalizeReportStatus(report, compileResult.Status.ToString());
                return report;
            }

            string projectRoot = string.IsNullOrWhiteSpace(request.ProjectRootPath)
                ? Directory.GetCurrentDirectory()
                : request.ProjectRootPath;
            string packageRoot = request.PackageRootPath ?? string.Empty;
            Dictionary<string, CharacterUnityImportGeneratedArtifact> generatedArtifacts = BuildArtifactLookup(request.GeneratedArtifacts);

            for (int i = 0; i < writePlan.Writes.Count; i++)
            {
                CharacterUnityImportWriteEntry write = writePlan.Writes[i];
                if (write == null)
                    continue;

                CharacterUnityImportWriteInput input = CreateWriteInput(write, generatedArtifacts, packageRoot);
                ExecuteWrite(projectRoot, packageRoot, input, request.DryRun, report);
            }

            if (request.AdditionalWrites != null)
            {
                for (int i = 0; i < request.AdditionalWrites.Count; i++)
                    ExecuteWrite(projectRoot, packageRoot, request.AdditionalWrites[i], request.DryRun, report);
            }

            FinalizeReportStatus(report, compileResult.Status.ToString());
            return report;
        }

        private static CharacterUnityImportReport CreateReportSkeleton(CharacterAuthoringCompileResult compileResult, bool dryRun, string sourcePackageVersion)
        {
            CharacterUnityImportWritePlan writePlan = compileResult != null ? compileResult.UnityImportWritePlan : null;
            CharacterCompilerHashSet hashes = compileResult != null ? compileResult.Hashes : null;
            var report = new CharacterUnityImportReport
            {
                PackageId = compileResult != null ? compileResult.PackageId : string.Empty,
                PackageStableId = compileResult != null ? compileResult.PackageStableId : string.Empty,
                SourcePackageVersion = sourcePackageVersion ?? string.Empty,
                Status = compileResult != null ? compileResult.Status.ToString() : string.Empty,
                DryRun = dryRun,
                CanWriteToUnityProject = writePlan != null && writePlan.CanWriteToUnityProject,
                CanSpawnAfterImport = writePlan != null && writePlan.CanSpawnAfterImport,
                TargetRootPath = writePlan != null ? writePlan.TargetRootPath : string.Empty,
                SourcePackageHash = hashes != null ? hashes.SourcePackageHash : string.Empty,
                GeneratedConfigHash = hashes != null ? hashes.GeneratedConfigHash : string.Empty,
                GeometryBindingHash = hashes != null ? hashes.GeometryBindingHash : string.Empty,
                ResourceMappingHash = hashes != null ? hashes.ResourceMappingHash : string.Empty,
                WritePlanHash = hashes != null ? hashes.WritePlanHash : string.Empty
            };

            return report;
        }

        private static void CopyGateIssues(CharacterUnityImportReport report, CharacterCompilerGateReport gateReport)
        {
            if (report == null || gateReport == null || gateReport.Issues == null)
                return;

            for (int i = 0; i < gateReport.Issues.Count; i++)
                report.Issues.Add(gateReport.Issues[i]);
        }

        private static Dictionary<string, CharacterUnityImportGeneratedArtifact> BuildArtifactLookup(List<CharacterUnityImportGeneratedArtifact> artifacts)
        {
            var lookup = new Dictionary<string, CharacterUnityImportGeneratedArtifact>(StringComparer.Ordinal);
            if (artifacts == null)
                return lookup;

            for (int i = 0; i < artifacts.Count; i++)
            {
                CharacterUnityImportGeneratedArtifact artifact = artifacts[i];
                if (artifact == null || string.IsNullOrWhiteSpace(artifact.SourcePath))
                    continue;

                lookup[NormalizeRelativePath(artifact.SourcePath)] = artifact;
            }

            return lookup;
        }

        private static CharacterUnityImportWriteInput CreateWriteInput(
            CharacterUnityImportWriteEntry write,
            Dictionary<string, CharacterUnityImportGeneratedArtifact> generatedArtifacts,
            string packageRoot)
        {
            var input = new CharacterUnityImportWriteInput
            {
                Kind = write.Kind,
                Owner = write.Owner,
                SourcePath = write.SourcePath,
                TargetPath = write.TargetPath,
                WritePolicy = write.WritePolicy,
                ContentHash = write.ContentHash
            };

            if (string.Equals(write.Kind, CharacterAuthoringCompilerWriteKinds.ResourceFile, StringComparison.Ordinal))
            {
                input.SourceFilePath = ResolvePackageSourcePath(packageRoot, write.SourcePath);
                return input;
            }

            CharacterUnityImportGeneratedArtifact artifact;
            if (generatedArtifacts.TryGetValue(NormalizeRelativePath(write.SourcePath), out artifact))
            {
                input.Content = artifact.Content ?? string.Empty;
                input.ContentHash = artifact.ContentHash;
            }

            return input;
        }

        private static void ExecuteWrite(
            string projectRoot,
            string packageRoot,
            CharacterUnityImportWriteInput input,
            bool dryRun,
            CharacterUnityImportReport report)
        {
            if (input == null)
                return;

            string targetFullPath;
            string normalizedTargetPath;
            string targetError;
            if (!TryResolveProjectAssetPath(projectRoot, input.TargetPath, out targetFullPath, out normalizedTargetPath, out targetError))
            {
                AddOperation(report, input, CharacterUnityImportBridgeWriteActions.Error, string.Empty, string.Empty, targetError);
                return;
            }

            byte[] bytes;
            string sourceError;
            if (!TryReadWriteBytes(packageRoot, input, out bytes, out sourceError))
            {
                AddOperation(report, input, CharacterUnityImportBridgeWriteActions.Error, normalizedTargetPath, string.Empty, sourceError);
                return;
            }

            string contentHash = CharacterPackageHashUtility.NormalizeSha256(input.ContentHash);
            if (string.IsNullOrWhiteSpace(contentHash))
                contentHash = CharacterPackageHashUtility.ComputeSha256(bytes);

            string existingHash = File.Exists(targetFullPath)
                ? CharacterPackageHashUtility.ComputeFileSha256(targetFullPath)
                : string.Empty;

            if (string.Equals(existingHash, contentHash, StringComparison.OrdinalIgnoreCase))
            {
                AddOperation(report, input, CharacterUnityImportBridgeWriteActions.Skipped, normalizedTargetPath, existingHash, "hash unchanged.");
                return;
            }

            if (!string.IsNullOrEmpty(existingHash) && IsConflictPolicy(input.WritePolicy))
            {
                AddOperation(report, input, CharacterUnityImportBridgeWriteActions.Conflict, normalizedTargetPath, existingHash, "target exists and write policy requires explicit conflict handling.");
                return;
            }

            string action = string.IsNullOrEmpty(existingHash)
                ? CharacterUnityImportBridgeWriteActions.Added
                : CharacterUnityImportBridgeWriteActions.Updated;

            if (!dryRun)
            {
                string directory = Path.GetDirectoryName(targetFullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(targetFullPath, bytes);
            }

            AddOperation(report, input, action, normalizedTargetPath, existingHash, dryRun ? "dry run; file was not written." : "written.");
        }

        private static bool TryReadWriteBytes(
            string packageRoot,
            CharacterUnityImportWriteInput input,
            out byte[] bytes,
            out string error)
        {
            bytes = Array.Empty<byte>();
            error = string.Empty;

            if (input.Content != null)
            {
                bytes = Encoding.UTF8.GetBytes(input.Content);
                return true;
            }

            string sourceFullPath = input.SourceFilePath;
            if (string.IsNullOrWhiteSpace(sourceFullPath))
                sourceFullPath = ResolvePackageSourcePath(packageRoot, input.SourcePath);

            string normalizedPackageRoot = GetFullPathWithSeparator(packageRoot);
            string fullPath = Path.GetFullPath(sourceFullPath);
            if (!fullPath.StartsWith(normalizedPackageRoot, StringComparison.Ordinal))
            {
                error = "source path escapes character package root: " + input.SourcePath;
                return false;
            }

            if (!File.Exists(fullPath))
            {
                error = "source file was not found: " + input.SourcePath;
                return false;
            }

            bytes = File.ReadAllBytes(fullPath);
            return true;
        }

        private static void AddOperation(
            CharacterUnityImportReport report,
            CharacterUnityImportWriteInput input,
            string action,
            string normalizedTargetPath,
            string existingHash,
            string message)
        {
            string contentHash = CharacterPackageHashUtility.NormalizeSha256(input.ContentHash);
            if (string.IsNullOrWhiteSpace(contentHash) && input.Content != null)
                contentHash = CharacterPackageHashUtility.ComputeTextSha256(input.Content);

            var operation = new CharacterUnityImportOperation
            {
                Kind = input.Kind ?? string.Empty,
                Owner = input.Owner ?? string.Empty,
                SourcePath = NormalizeRelativePath(input.SourcePath),
                TargetPath = string.IsNullOrWhiteSpace(normalizedTargetPath) ? NormalizeRelativePath(input.TargetPath) : normalizedTargetPath,
                WritePolicy = input.WritePolicy ?? string.Empty,
                Action = action ?? string.Empty,
                ContentHash = contentHash,
                ExistingHash = existingHash ?? string.Empty,
                Message = message ?? string.Empty
            };
            report.Operations.Add(operation);

            if (action == CharacterUnityImportBridgeWriteActions.Added)
                report.AddedCount++;
            else if (action == CharacterUnityImportBridgeWriteActions.Updated)
                report.UpdatedCount++;
            else if (action == CharacterUnityImportBridgeWriteActions.Skipped)
                report.SkippedCount++;
            else if (action == CharacterUnityImportBridgeWriteActions.Conflict)
                report.ConflictCount++;
            else if (action == CharacterUnityImportBridgeWriteActions.Error)
                report.ErrorCount++;
        }

        private static void AddBridgeError(
            CharacterUnityImportReport report,
            string code,
            string sourcePath,
            string sourceObjectPath,
            string field,
            string message)
        {
            report.Issues.Add(new CharacterAuthoringValidationIssue
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Gate = CharacterAuthoringValidationGate.ImportBlocked,
                Code = code,
                SourcePath = sourcePath,
                SourceObjectPath = sourceObjectPath,
                Field = field,
                Message = message,
                SuggestedFix = "Run the Character Authoring Compiler again and retry Unity import."
            });
            report.ErrorCount++;
        }

        private static void FinalizeReportStatus(CharacterUnityImportReport report, string fallbackStatus)
        {
            if (report.ErrorCount > 0 || report.ConflictCount > 0)
            {
                report.Status = "Failed";
                report.CanSpawnAfterImport = false;
                return;
            }

            report.Status = string.IsNullOrWhiteSpace(fallbackStatus)
                ? CharacterAuthoringCompilerStatus.Ready.ToString()
                : fallbackStatus;
        }

        private static bool TryResolveProjectAssetPath(
            string projectRoot,
            string targetPath,
            out string fullPath,
            out string projectRelativePath,
            out string error)
        {
            fullPath = string.Empty;
            projectRelativePath = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                error = "target path is missing.";
                return false;
            }

            string root = Path.GetFullPath(string.IsNullOrWhiteSpace(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot);
            string normalizedRoot = GetFullPathWithSeparator(root);
            string candidate = Path.IsPathRooted(targetPath)
                ? Path.GetFullPath(targetPath)
                : Path.GetFullPath(Path.Combine(root, targetPath));

            if (!candidate.StartsWith(normalizedRoot, StringComparison.Ordinal))
            {
                error = "target path escapes Unity project root: " + targetPath;
                return false;
            }

            projectRelativePath = candidate.Substring(normalizedRoot.Length).Replace('\\', '/');
            if (!projectRelativePath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "target path must be project-relative under Assets/: " + targetPath;
                return false;
            }

            fullPath = candidate;
            return true;
        }

        private static string ResolvePackageSourcePath(string packageRoot, string sourcePath)
        {
            return Path.Combine(packageRoot ?? string.Empty, NormalizeRelativePath(sourcePath));
        }

        private static string NormalizeRelativePath(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }

        private static string GetFullPathWithSeparator(string path)
        {
            string fullPath = Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? Directory.GetCurrentDirectory() : path);
            if (!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                fullPath += Path.DirectorySeparatorChar;
            return fullPath;
        }

        private static bool IsConflictPolicy(string writePolicy)
        {
            if (string.IsNullOrWhiteSpace(writePolicy))
                return false;

            return writePolicy.IndexOf("NeverOverwrite", StringComparison.OrdinalIgnoreCase) >= 0
                || writePolicy.IndexOf("ReportOnly", StringComparison.OrdinalIgnoreCase) >= 0
                || writePolicy.IndexOf("Conflict", StringComparison.OrdinalIgnoreCase) >= 0
                || writePolicy.IndexOf("RequireExplicitUpgrade", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
