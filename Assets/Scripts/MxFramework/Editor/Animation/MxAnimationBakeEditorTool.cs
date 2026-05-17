using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.Resources;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.Animation
{
    public sealed class MxAnimationBakeEditorResult
    {
        public MxAnimationBakeEditorResult(
            MxAnimationBakeArtifact artifact,
            MxAnimationBakeValidationReport validationReport,
            string outputPath,
            string reportText)
        {
            Artifact = artifact;
            ValidationReport = validationReport ?? new MxAnimationBakeValidationReport();
            OutputPath = outputPath ?? string.Empty;
            ReportText = reportText ?? string.Empty;
        }

        public MxAnimationBakeArtifact Artifact { get; }
        public MxAnimationBakeValidationReport ValidationReport { get; }
        public string OutputPath { get; }
        public string ReportText { get; }
        public bool Success => Artifact != null && !ValidationReport.HasErrors;
    }

    internal sealed class MxAnimationBatchBakeClipResult
    {
        public MxAnimationBatchBakeClipResult(
            int index,
            string clipId,
            ResourceKey sourceClipKey,
            AnimationClip clip,
            MxAnimationBakeEditorResult bakeResult)
        {
            Index = index;
            ClipId = clipId ?? string.Empty;
            SourceClipKey = sourceClipKey;
            Clip = clip;
            BakeResult = bakeResult;
        }

        public int Index { get; }
        public string ClipId { get; }
        public ResourceKey SourceClipKey { get; }
        public AnimationClip Clip { get; }
        public MxAnimationBakeEditorResult BakeResult { get; }
        public bool Success => BakeResult != null && BakeResult.Success;
    }

    internal sealed class MxAnimationBatchBakeReport
    {
        private readonly List<MxAnimationBatchBakeClipResult> _results;

        public MxAnimationBatchBakeReport(
            string setId,
            string definitionHash,
            IReadOnlyList<MxAnimationBatchBakeClipResult> results,
            string reportText)
        {
            SetId = setId ?? string.Empty;
            DefinitionHash = definitionHash ?? string.Empty;
            _results = results != null
                ? new List<MxAnimationBatchBakeClipResult>(results)
                : new List<MxAnimationBatchBakeClipResult>();
            ReportText = reportText ?? string.Empty;
        }

        public string SetId { get; }
        public string DefinitionHash { get; }
        public IReadOnlyList<MxAnimationBatchBakeClipResult> Results => _results;
        public string ReportText { get; }
        public int BakedCount { get; internal set; }
        public int ErrorCount { get; internal set; }
        public int WarningCount { get; internal set; }
        public bool Success => ErrorCount == 0 && BakedCount > 0;

        public void CountResult(MxAnimationBatchBakeClipResult result)
        {
            if (result == null || result.BakeResult == null)
                return;

            if (result.BakeResult.Artifact != null)
                BakedCount++;
            ErrorCount += result.BakeResult.ValidationReport.ErrorCount;
            WarningCount += result.BakeResult.ValidationReport.WarningCount;
        }
    }

    internal sealed class MxAnimationCompatibilityWorkstationReport
    {
        private readonly List<MxAnimationBakeValidationReport> _bakeValidationReports;

        public MxAnimationCompatibilityWorkstationReport(
            MxAnimationCompatibilityProfile profile,
            MxAnimationCompatibilityExpectation expectation,
            MxAnimationCompatibilityValidationReport compatibilityReport,
            IReadOnlyList<MxAnimationBakeValidationReport> bakeValidationReports,
            string reportText)
        {
            Profile = profile;
            Expectation = expectation;
            CompatibilityReport = compatibilityReport ?? new MxAnimationCompatibilityValidationReport();
            _bakeValidationReports = bakeValidationReports != null
                ? new List<MxAnimationBakeValidationReport>(bakeValidationReports)
                : new List<MxAnimationBakeValidationReport>();
            ReportText = reportText ?? string.Empty;
        }

        public MxAnimationCompatibilityProfile Profile { get; }
        public MxAnimationCompatibilityExpectation Expectation { get; }
        public MxAnimationCompatibilityValidationReport CompatibilityReport { get; }
        public IReadOnlyList<MxAnimationBakeValidationReport> BakeValidationReports => _bakeValidationReports;
        public string ReportText { get; }
        public bool Success => !CompatibilityReport.HasErrors && !HasBakeErrors();

        private bool HasBakeErrors()
        {
            for (int i = 0; i < _bakeValidationReports.Count; i++)
            {
                if (_bakeValidationReports[i] != null && _bakeValidationReports[i].HasErrors)
                    return true;
            }

            return false;
        }
    }

    internal static class MxAnimationWorkstationBakeUtility
    {
        public static MxAnimationBatchBakeReport BakeRegistryClips(
            MxAnimationClipRegistryAsset registry,
            IReadOnlyList<int> selectedIndices,
            MxAnimationSkeletonCompatibilityProfile skeletonProfile = null)
        {
            return BakeRegistryClipsInternal(registry, selectedIndices, string.Empty, writeFiles: false, skeletonProfile: skeletonProfile);
        }

        public static MxAnimationBatchBakeReport BakeRegistryClipsToFiles(
            MxAnimationClipRegistryAsset registry,
            IReadOnlyList<int> selectedIndices,
            string outputRoot,
            MxAnimationSkeletonCompatibilityProfile skeletonProfile = null)
        {
            return BakeRegistryClipsInternal(registry, selectedIndices, outputRoot, writeFiles: true, skeletonProfile: skeletonProfile);
        }

        public static MxAnimationCompatibilityWorkstationReport BuildCompatibilityReport(
            MxAnimationClipRegistryAsset registry,
            GameObject skeletonRoot,
            string skeletonProfileId,
            IEnumerable<string> socketPaths,
            MxAnimationBatchBakeReport batchReport = null)
        {
            var bakeReports = new List<MxAnimationBakeValidationReport>();
            if (registry == null)
            {
                var missing = new MxAnimationCompatibilityValidationReport();
                missing.AddError(
                    MxAnimationCompatibilityIssueCodes.CompatibilityProfileMissing,
                    default,
                    "registry",
                    "present",
                    "missing",
                    "Animation clip registry asset is required.");
                string missingText = CreateCompatibilityReportText(null, null, missing, bakeReports);
                return new MxAnimationCompatibilityWorkstationReport(null, null, missing, bakeReports, missingText);
            }

            MxAnimationSkeletonCompatibilityProfile skeletonProfile = skeletonRoot != null
                ? MxAnimationCompatibilityEditorExtractor.CreateSkeletonProfile(
                    skeletonRoot,
                    string.IsNullOrWhiteSpace(skeletonProfileId) ? "skeleton" : skeletonProfileId,
                    socketPaths)
                : null;

            var clipProfiles = new List<MxAnimationClipCompatibilityProfile>();
            var clipExpectations = new List<MxAnimationClipCompatibilityExpectation>();
            var requiredBonePaths = new List<string>();
            MxAnimationClipRegistryClipEntry[] clips = registry.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                MxAnimationClipRegistryClipEntry clipEntry = clips[i];
                ResourceKey clipKey = clipEntry.CreateResourceKey(registry.PackageId);
                IReadOnlyList<string> bindingPaths = MxAnimationCompatibilityEditorExtractor.ExtractClipBindingPaths(clipEntry.Clip);
                AddUniquePaths(requiredBonePaths, bindingPaths);
                clipExpectations.Add(new MxAnimationClipCompatibilityExpectation(
                    clipKey,
                    bindingPaths,
                    skeletonProfile != null ? skeletonProfile.ProfileId : string.Empty,
                    skeletonProfile != null ? skeletonProfile.ProfileHash : string.Empty));

                if (clipEntry.Clip != null && clipKey.IsValid)
                    clipProfiles.Add(MxAnimationCompatibilityEditorExtractor.CreateClipProfile(clipEntry.Clip, clipKey, skeletonProfile));
            }

            var maskProfiles = new List<MxAnimationAvatarMaskCompatibilityProfile>();
            var maskExpectations = new List<MxAnimationAvatarMaskCompatibilityExpectation>();
            MxAnimationClipRegistryLayerEntry[] layers = registry.Layers;
            for (int i = 0; i < layers.Length; i++)
            {
                MxAnimationClipRegistryLayerEntry layer = layers[i];
                ResourceKey maskKey = layer.CreateAvatarMaskKey(registry.PackageId);
                if (!maskKey.IsValid && layer.AvatarMask == null)
                    continue;

                IReadOnlyList<string> activePaths = MxAnimationCompatibilityEditorExtractor.ExtractAvatarMaskActivePaths(layer.AvatarMask);
                AddUniquePaths(requiredBonePaths, activePaths);
                maskExpectations.Add(new MxAnimationAvatarMaskCompatibilityExpectation(
                    maskKey,
                    activePaths,
                    skeletonProfile != null ? skeletonProfile.ProfileId : string.Empty,
                    skeletonProfile != null ? skeletonProfile.ProfileHash : string.Empty));

                if (layer.AvatarMask != null && maskKey.IsValid)
                    maskProfiles.Add(MxAnimationCompatibilityEditorExtractor.CreateAvatarMaskProfile(layer.AvatarMask, maskKey, skeletonProfile));
            }

            var bakeArtifacts = new List<MxAnimationBakeArtifact>();
            AppendBatchBakeArtifacts(batchReport, bakeArtifacts);

            MxAnimationCompatibilityProfile profile = skeletonProfile != null
                ? MxAnimationCompatibilityEditorExtractor.CreateProfile(skeletonProfile, clipProfiles, maskProfiles, bakeArtifacts)
                : null;
            var expectation = new MxAnimationCompatibilityExpectation(
                skeletonProfile != null ? skeletonProfile.ProfileId : string.Empty,
                skeletonProfile != null ? skeletonProfile.ProfileHash : string.Empty,
                requiredBonePaths,
                socketPaths,
                clipExpectations,
                maskExpectations);
            MxAnimationCompatibilityValidationReport compatibilityReport =
                MxAnimationCompatibilityEditorExtractor.Validate(profile, expectation);

            AppendBakeFreshnessReports(registry, skeletonProfile, batchReport, bakeReports);

            string reportText = CreateCompatibilityReportText(profile, expectation, compatibilityReport, bakeReports);
            return new MxAnimationCompatibilityWorkstationReport(profile, expectation, compatibilityReport, bakeReports, reportText);
        }

        private static MxAnimationBatchBakeReport BakeRegistryClipsInternal(
            MxAnimationClipRegistryAsset registry,
            IReadOnlyList<int> selectedIndices,
            string outputRoot,
            bool writeFiles,
            MxAnimationSkeletonCompatibilityProfile skeletonProfile)
        {
            var results = new List<MxAnimationBatchBakeClipResult>();
            MxAnimationClipRegistryExportResult export = MxAnimationClipRegistryExporter.ExportStructureOnly(registry);
            string setId = export.Definition != null ? export.Definition.SetId : string.Empty;
            string definitionHash = export.Definition != null ? export.Definition.DefinitionHash : string.Empty;

            if (registry == null)
            {
                var validation = new MxAnimationBakeValidationReport();
                validation.AddError("BatchBakeRegistryMissing", "registry", "present", "missing", "Animation clip registry asset is required.");
                results.Add(new MxAnimationBatchBakeClipResult(-1, string.Empty, default, null, new MxAnimationBakeEditorResult(null, validation, string.Empty, MxAnimationBakeEditorTool.CreateReportText(null, validation))));
                return CreateBatchReport(setId, definitionHash, results);
            }

            MxAnimationClipRegistryClipEntry[] clips = registry.Clips;
            if (clips.Length == 0)
            {
                var validation = new MxAnimationBakeValidationReport();
                validation.AddError("BatchBakeNoClips", "clips", "one or more", "0", "Registry has no clips to bake.");
                results.Add(new MxAnimationBatchBakeClipResult(-1, string.Empty, default, null, new MxAnimationBakeEditorResult(null, validation, string.Empty, MxAnimationBakeEditorTool.CreateReportText(null, validation))));
                return CreateBatchReport(setId, definitionHash, results);
            }

            var indices = ResolveIndices(selectedIndices, clips.Length);
            if (indices.Count == 0)
            {
                var validation = new MxAnimationBakeValidationReport();
                validation.AddError("BatchBakeSelectionEmpty", "selectedClips", "one or more", "0", "No registry clips are selected for batch bake.");
                results.Add(new MxAnimationBatchBakeClipResult(-1, string.Empty, default, null, new MxAnimationBakeEditorResult(null, validation, string.Empty, MxAnimationBakeEditorTool.CreateReportText(null, validation))));
                return CreateBatchReport(setId, definitionHash, results);
            }

            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                MxAnimationClipRegistryClipEntry clipEntry = clips[index];
                ResourceKey clipKey = clipEntry.CreateResourceKey(registry.PackageId);
                MxAnimationBakeEditorResult result;
                if (clipEntry.Clip == null)
                {
                    var validation = new MxAnimationBakeValidationReport();
                    validation.AddError("BatchBakeClipReferenceMissing", "clip", "AnimationClip", "missing", "Registry clip is missing an AnimationClip reference.");
                    result = new MxAnimationBakeEditorResult(null, validation, string.Empty, MxAnimationBakeEditorTool.CreateReportText(null, validation));
                }
                else if (!clipKey.IsValid)
                {
                    var validation = new MxAnimationBakeValidationReport();
                    validation.AddError("BatchBakeClipResourceKeyInvalid", "sourceClipKey", "valid ResourceKey", clipKey.ToString(), "Registry clip has an invalid ResourceKey.");
                    result = new MxAnimationBakeEditorResult(null, validation, string.Empty, MxAnimationBakeEditorTool.CreateReportText(null, validation));
                }
                else if (writeFiles)
                {
                    result = MxAnimationBakeEditorTool.BakeClipToFile(
                        clipEntry.Clip,
                        clipKey,
                        outputRoot,
                        CreateOutputStem(clipEntry, clipKey),
                        skeletonProfile);
                }
                else
                {
                    result = MxAnimationBakeEditorTool.BakeClip(clipEntry.Clip, clipKey, skeletonProfile);
                }

                results.Add(new MxAnimationBatchBakeClipResult(index, clipEntry.ClipId, clipKey, clipEntry.Clip, result));
            }

            return CreateBatchReport(setId, definitionHash, results);
        }

        private static MxAnimationBatchBakeReport CreateBatchReport(
            string setId,
            string definitionHash,
            IReadOnlyList<MxAnimationBatchBakeClipResult> results)
        {
            var report = new MxAnimationBatchBakeReport(setId, definitionHash, results, string.Empty);
            for (int i = 0; i < results.Count; i++)
                report.CountResult(results[i]);

            string reportText = CreateBatchReportText(report);
            return new MxAnimationBatchBakeReport(setId, definitionHash, results, reportText)
            {
                BakedCount = report.BakedCount,
                ErrorCount = report.ErrorCount,
                WarningCount = report.WarningCount
            };
        }

        private static string CreateBatchReportText(MxAnimationBatchBakeReport report)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Batch Bake Report\n");
            builder.Append("success: ").Append(report != null && report.Success ? "true" : "false").Append('\n');
            builder.Append("setId: ").Append(report != null ? report.SetId : string.Empty).Append('\n');
            builder.Append("definitionHash: ").Append(report != null ? report.DefinitionHash : string.Empty).Append('\n');
            builder.Append("clips: ").Append(report != null ? report.Results.Count : 0).Append('\n');
            builder.Append("baked: ").Append(report != null ? report.BakedCount : 0).Append('\n');
            builder.Append("errors: ").Append(report != null ? report.ErrorCount : 0).Append('\n');
            builder.Append("warnings: ").Append(report != null ? report.WarningCount : 0).Append('\n');
            builder.Append("results:\n");

            if (report == null || report.Results.Count == 0)
            {
                builder.Append("- none\n");
                return builder.ToString();
            }

            for (int i = 0; i < report.Results.Count; i++)
            {
                MxAnimationBatchBakeClipResult result = report.Results[i];
                builder.Append("- index: ").Append(result.Index.ToString(CultureInfo.InvariantCulture))
                    .Append(" clipId: ").Append(result.ClipId)
                    .Append(" sourceClip: ").Append(result.SourceClipKey)
                    .Append(" success: ").Append(result.Success ? "true" : "false")
                    .Append('\n');

                MxAnimationBakeEditorResult bake = result.BakeResult;
                if (bake == null)
                    continue;

                builder.Append("  outputPath: ").Append(bake.OutputPath).Append('\n');
                if (bake.Artifact != null)
                {
                    builder.Append("  artifactHash: ").Append(bake.Artifact.ArtifactHash).Append('\n');
                    builder.Append("  sourceClipHash: ").Append(bake.Artifact.Profile.SourceClipHash).Append('\n');
                    builder.Append("  profileHash: ").Append(bake.Artifact.Profile.ProfileHash).Append('\n');
                    builder.Append("  skeletonProfileHash: ").Append(bake.Artifact.Profile.SkeletonProfileHash).Append('\n');
                }

                builder.Append("  issues:\n");
                AppendBakeIssues(builder, bake.ValidationReport, "  ");
            }

            return builder.ToString();
        }

        private static string CreateCompatibilityReportText(
            MxAnimationCompatibilityProfile profile,
            MxAnimationCompatibilityExpectation expectation,
            MxAnimationCompatibilityValidationReport compatibilityReport,
            IReadOnlyList<MxAnimationBakeValidationReport> bakeReports)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Compatibility Report\n");
            builder.Append("success: ").Append(
                compatibilityReport != null
                && !compatibilityReport.HasErrors
                && !HasBakeErrors(bakeReports)
                    ? "true"
                    : "false").Append('\n');
            builder.Append("skeletonProfileId: ").Append(profile != null && profile.SkeletonProfile != null ? profile.SkeletonProfile.ProfileId : string.Empty).Append('\n');
            builder.Append("skeletonProfileHash: ").Append(profile != null && profile.SkeletonProfile != null ? profile.SkeletonProfile.ProfileHash : string.Empty).Append('\n');
            builder.Append("bones: ").Append(profile != null && profile.SkeletonProfile != null ? profile.SkeletonProfile.BonePaths.Count : 0).Append('\n');
            builder.Append("sockets: ").Append(profile != null && profile.SkeletonProfile != null ? profile.SkeletonProfile.SocketPaths.Count : 0).Append('\n');
            builder.Append("clipProfiles: ").Append(profile != null ? profile.ClipProfiles.Count : 0).Append('\n');
            builder.Append("avatarMaskProfiles: ").Append(profile != null ? profile.AvatarMaskProfiles.Count : 0).Append('\n');
            builder.Append("bakeArtifacts: ").Append(profile != null ? profile.BakeArtifacts.Count : 0).Append('\n');
            builder.Append("requiredBones: ").Append(expectation != null ? expectation.RequiredBonePaths.Count : 0).Append('\n');
            builder.Append("requiredSockets: ").Append(expectation != null ? expectation.RequiredSocketPaths.Count : 0).Append('\n');
            builder.Append("compatibilityIssues:\n");

            if (compatibilityReport == null || compatibilityReport.Issues.Count == 0)
            {
                builder.Append("- none\n");
            }
            else
            {
                for (int i = 0; i < compatibilityReport.Issues.Count; i++)
                {
                    MxAnimationCompatibilityIssue issue = compatibilityReport.Issues[i];
                    builder.Append("- ").Append(issue.Severity)
                        .Append(' ').Append(issue.Code)
                        .Append(" key=").Append(issue.Key)
                        .Append(" field=").Append(issue.Field)
                        .Append(" expected=").Append(issue.Expected)
                        .Append(" actual=").Append(issue.Actual)
                        .Append(" message=").Append(issue.Message)
                        .Append('\n');
                }
            }

            builder.Append("bakeFreshnessIssues:\n");
            if (bakeReports == null || bakeReports.Count == 0)
            {
                builder.Append("- none\n");
            }
            else
            {
                for (int i = 0; i < bakeReports.Count; i++)
                    AppendBakeIssues(builder, bakeReports[i], string.Empty);
            }

            return builder.ToString();
        }

        private static void AppendBakeFreshnessReports(
            MxAnimationClipRegistryAsset registry,
            MxAnimationSkeletonCompatibilityProfile skeletonProfile,
            MxAnimationBatchBakeReport batchReport,
            List<MxAnimationBakeValidationReport> bakeReports)
        {
            if (registry == null || batchReport == null)
                return;

            for (int i = 0; i < batchReport.Results.Count; i++)
            {
                MxAnimationBatchBakeClipResult result = batchReport.Results[i];
                if (result == null || result.BakeResult == null || result.BakeResult.Artifact == null)
                    continue;

                if (!TryResolveClipEntry(registry, result.ClipId, out MxAnimationClipRegistryClipEntry currentEntry)
                    || currentEntry.Clip == null)
                    continue;

                ResourceKey currentSourceClipKey = currentEntry.CreateResourceKey(registry.PackageId);
                MxAnimationBakeProfile expectedProfile = MxAnimationBakeEditorTool.CreateBakeProfile(
                    currentEntry.Clip,
                    currentSourceClipKey,
                    skeletonProfile);
                MxAnimationBakeValidationReport report = MxAnimationBakeArtifactValidator.Validate(
                    result.BakeResult.Artifact,
                    new MxAnimationBakeExpectation(
                        sourceClipHash: expectedProfile.SourceClipHash,
                        profileHash: expectedProfile.ProfileHash,
                        skeletonProfileHash: expectedProfile.SkeletonProfileHash,
                        artifactHash: result.BakeResult.Artifact.ArtifactHash));
                bakeReports.Add(report);
            }
        }

        private static void AppendBatchBakeArtifacts(
            MxAnimationBatchBakeReport batchReport,
            List<MxAnimationBakeArtifact> artifacts)
        {
            if (batchReport == null)
                return;

            for (int i = 0; i < batchReport.Results.Count; i++)
            {
                MxAnimationBatchBakeClipResult result = batchReport.Results[i];
                if (result != null && result.BakeResult != null && result.BakeResult.Artifact != null)
                    artifacts.Add(result.BakeResult.Artifact);
            }
        }

        private static void AppendBakeIssues(StringBuilder builder, MxAnimationBakeValidationReport report, string indent)
        {
            if (report == null || report.Issues.Count == 0)
            {
                builder.Append(indent).Append("- none\n");
                return;
            }

            for (int i = 0; i < report.Issues.Count; i++)
            {
                MxAnimationBakeIssue issue = report.Issues[i];
                builder.Append(indent).Append("- ").Append(issue.Severity)
                    .Append(' ').Append(issue.Code)
                    .Append(" field=").Append(issue.Field)
                    .Append(" expected=").Append(issue.Expected)
                    .Append(" actual=").Append(issue.Actual)
                    .Append(" location=").Append(issue.Location)
                    .Append(" message=").Append(issue.Message)
                    .Append('\n');
            }
        }

        private static bool HasBakeErrors(IReadOnlyList<MxAnimationBakeValidationReport> reports)
        {
            if (reports == null)
                return false;

            for (int i = 0; i < reports.Count; i++)
            {
                if (reports[i] != null && reports[i].HasErrors)
                    return true;
            }

            return false;
        }

        private static bool TryResolveClipEntry(
            MxAnimationClipRegistryAsset registry,
            string clipId,
            out MxAnimationClipRegistryClipEntry clipEntry)
        {
            MxAnimationClipRegistryClipEntry[] clips = registry.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (string.Equals(clips[i].ClipId, clipId, StringComparison.Ordinal))
                {
                    clipEntry = clips[i];
                    return true;
                }
            }

            clipEntry = default;
            return false;
        }

        private static List<int> ResolveIndices(IReadOnlyList<int> selectedIndices, int clipCount)
        {
            var indices = new List<int>();
            if (selectedIndices == null)
            {
                for (int i = 0; i < clipCount; i++)
                    indices.Add(i);
                return indices;
            }

            var seen = new HashSet<int>();
            for (int i = 0; i < selectedIndices.Count; i++)
            {
                int index = selectedIndices[i];
                if (index < 0 || index >= clipCount || !seen.Add(index))
                    continue;

                indices.Add(index);
            }

            return indices;
        }

        private static string CreateOutputStem(MxAnimationClipRegistryClipEntry clipEntry, ResourceKey clipKey)
        {
            if (!string.IsNullOrWhiteSpace(clipEntry.ClipId))
                return clipEntry.ClipId;
            if (!string.IsNullOrWhiteSpace(clipKey.Id))
                return clipKey.Id;
            return clipEntry.Clip != null ? clipEntry.Clip.name : "clip";
        }

        private static void AddUniquePaths(List<string> target, IEnumerable<string> source)
        {
            if (source == null)
                return;

            foreach (string path in source)
            {
                string normalized = NormalizePath(path);
                if (string.IsNullOrWhiteSpace(normalized) || target.Contains(normalized))
                    continue;

                target.Add(normalized);
            }

            target.Sort(StringComparer.Ordinal);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
        }
    }

    public static class MxAnimationBakeEditorTool
    {
        public const string DefaultOutputRoot = "Assets/Art/MxFramework/Samples/Characters/Skeleton/Bakes";
        public const int DefaultSampleTickRate = 30;
        public const int DefaultQuantizationScale = 1000;

        private const string MenuPath = "MxFramework/MxAnimation/Bake Selected Animation Clip MVP";
        private const string DefaultPackageId = "mxframework.samples";
        private const string RootSocketId = "root";
        private const string WeaponSocketId = "weapon";
        private const string WeaponSocketPath = "WeaponSocket";
        private const string WeaponTipPath = "WeaponTip";

        [MenuItem(MenuPath, priority = 132)]
        public static void BakeSelectedClipToDefaultArtifact()
        {
            AnimationClip clip = Selection.activeObject as AnimationClip;
            if (clip == null)
            {
                Debug.LogWarning("Select an AnimationClip before running MxAnimation bake.");
                return;
            }

            MxAnimationBakeEditorResult result = BakeClipToFile(clip, DefaultOutputRoot);
            if (result.Success)
                Debug.Log("MxAnimation bake artifact generated: " + result.OutputPath);
            else
                Debug.LogWarning(result.ReportText);
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool CanBakeSelectedClipToDefaultArtifact()
        {
            return Selection.activeObject is AnimationClip && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        public static MxAnimationBakeEditorResult BakeClipToFile(AnimationClip clip, string outputRoot)
        {
            MxAnimationBakeEditorResult result = BakeClip(clip);
            string normalizedRoot = NormalizeAssetPath(outputRoot);
            EnsureFolder(normalizedRoot);
            string outputPath = normalizedRoot + "/" + NormalizeName(clip != null ? clip.name : "clip") + ".mxbake.txt";
            if (result.Artifact != null)
                File.WriteAllText(outputPath, result.ReportText, Encoding.UTF8);

            AssetDatabase.ImportAsset(outputPath);
            AssetDatabase.SaveAssets();
            return new MxAnimationBakeEditorResult(result.Artifact, result.ValidationReport, outputPath, result.ReportText);
        }

        internal static MxAnimationBakeEditorResult BakeClipToFile(
            AnimationClip clip,
            ResourceKey sourceClipKey,
            string outputRoot,
            string outputFileStem,
            MxAnimationSkeletonCompatibilityProfile skeletonProfile)
        {
            MxAnimationBakeEditorResult result = BakeClip(clip, sourceClipKey, skeletonProfile);
            string normalizedRoot = NormalizeAssetPath(outputRoot);
            EnsureFolder(normalizedRoot);
            string stem = string.IsNullOrWhiteSpace(outputFileStem)
                ? (clip != null ? clip.name : "clip")
                : outputFileStem;
            string outputPath = normalizedRoot + "/" + NormalizeName(stem) + ".mxbake.txt";
            if (result.Artifact != null)
                File.WriteAllText(outputPath, result.ReportText, Encoding.UTF8);

            AssetDatabase.ImportAsset(outputPath);
            AssetDatabase.SaveAssets();
            return new MxAnimationBakeEditorResult(result.Artifact, result.ValidationReport, outputPath, result.ReportText);
        }

        public static MxAnimationBakeEditorResult BakeClip(AnimationClip clip)
        {
            return BakeClip(clip, default(ResourceKey));
        }

        public static MxAnimationBakeEditorResult BakeClip(AnimationClip clip, ResourceKey sourceClipKey)
        {
            return BakeClip(clip, sourceClipKey, null);
        }

        internal static MxAnimationBakeEditorResult BakeClip(
            AnimationClip clip,
            ResourceKey sourceClipKey,
            MxAnimationSkeletonCompatibilityProfile skeletonProfile)
        {
            if (clip == null)
            {
                var report = new MxAnimationBakeValidationReport();
                report.AddError("BakeSourceClipMissing", "clip", "non-null", "null", "AnimationClip is required.");
                return new MxAnimationBakeEditorResult(null, report, string.Empty, CreateReportText(null, report));
            }

            MxAnimationBakeProfile profile = CreateBakeProfile(clip, sourceClipKey, skeletonProfile);
            MxAnimationBakeArtifact artifact = BakeClip(clip, profile);
            MxAnimationBakeValidationReport validation = MxAnimationBakeArtifactValidator.Validate(
                artifact,
                new MxAnimationBakeExpectation(
                    sourceClipHash: profile.SourceClipHash,
                    profileHash: profile.ProfileHash,
                    skeletonProfileHash: profile.SkeletonProfileHash,
                    artifactHash: artifact.ArtifactHash));
            return new MxAnimationBakeEditorResult(artifact, validation, string.Empty, CreateReportText(artifact, validation));
        }

        internal static MxAnimationBakeProfile CreateBakeProfile(
            AnimationClip clip,
            ResourceKey sourceClipKey,
            MxAnimationSkeletonCompatibilityProfile skeletonProfile = null)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));

            string clipName = NormalizeName(clip.name);
            ResourceKey clipKey = sourceClipKey.IsValid
                ? sourceClipKey
                : new ResourceKey("art.character.skeleton.animation." + clipName, ResourceTypeIds.AnimationClip, packageId: DefaultPackageId);
            string profileSuffix = NormalizeName(sourceClipKey.IsValid ? clipKey.Id : clipName);
            string clipHash = ComputeClipHash(clip);
            string skeletonProfileId = skeletonProfile != null && !string.IsNullOrWhiteSpace(skeletonProfile.ProfileId)
                ? skeletonProfile.ProfileId
                : "skeleton";
            string skeletonProfileHash = skeletonProfile != null && !string.IsNullOrWhiteSpace(skeletonProfile.ProfileHash)
                ? skeletonProfile.ProfileHash
                : "clip:" + clipHash;

            return new MxAnimationBakeProfile(
                "mxanimation.bake." + profileSuffix,
                clipKey,
                clipHash,
                skeletonProfileId,
                skeletonProfileHash,
                DefaultSampleTickRate,
                DefaultQuantizationScale,
                MxAnimationBakeCoordinateSpace.Local,
                MxAnimationBakeRoundingPolicy.RoundNearest,
                CreateImportFingerprint(clip));
        }

        public static MxAnimationBakeArtifact BakeClip(AnimationClip clip, MxAnimationBakeProfile profile)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            int frameCount = Math.Max(1, (int)Math.Ceiling(Math.Max(clip.length, 0.0001f) * profile.SampleTickRate));
            var rootFrames = new List<MxAnimationBakedRootMotionFrame>(frameCount + 1);
            var socketFrames = new List<MxAnimationBakedSocketFrame>((frameCount + 1) * 2);
            var traceFrames = new List<MxAnimationBakedWeaponTraceFrame>(frameCount);
            MxAnimationBakedVector3 previousRoot = SampleVector(clip, string.Empty, 0f, profile);
            MxAnimationBakedVector3 previousSocket = SampleWeaponSocket(clip, 0f, profile, previousRoot);
            MxAnimationBakedVector3 previousTip = SampleWeaponTip(clip, 0f, profile, previousSocket);

            rootFrames.Add(new MxAnimationBakedRootMotionFrame(0, previousRoot, MxAnimationBakedVector3.Zero));
            AddSocketFrame(socketFrames, 0, RootSocketId, string.Empty, previousRoot, MxAnimationBakedVector3.Zero);
            AddSocketFrame(socketFrames, 0, WeaponSocketId, WeaponSocketPath, previousSocket, MxAnimationBakedVector3.Zero);
            for (int frame = 1; frame <= frameCount; frame++)
            {
                float time = Mathf.Min(clip.length, frame / (float)profile.SampleTickRate);
                MxAnimationBakedVector3 root = SampleVector(clip, string.Empty, time, profile);
                MxAnimationBakedVector3 socket = SampleWeaponSocket(clip, time, profile, root);
                MxAnimationBakedVector3 tip = SampleWeaponTip(clip, time, profile, socket);
                MxAnimationBakedVector3 rootDelta = Delta(previousRoot, root);
                MxAnimationBakedVector3 socketDelta = Delta(previousSocket, socket);
                rootFrames.Add(new MxAnimationBakedRootMotionFrame(frame, root, rootDelta));
                AddSocketFrame(socketFrames, frame, RootSocketId, string.Empty, root, rootDelta);
                AddSocketFrame(socketFrames, frame, WeaponSocketId, WeaponSocketPath, socket, socketDelta);
                traceFrames.Add(new MxAnimationBakedWeaponTraceFrame(frame, 0, WeaponSocketId, previousSocket, previousTip, socket, tip));
                previousRoot = root;
                previousSocket = socket;
                previousTip = tip;
            }

            return new MxAnimationBakeArtifact(
                profile,
                traceFrames,
                rootFrames,
                CreateEventMarkers(clip, profile),
                socketFrames: socketFrames);
        }

        public static string CreateReportText(MxAnimationBakeArtifact artifact, MxAnimationBakeValidationReport validation)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Bake Artifact\n");
            builder.Append("success: ").Append(validation != null && !validation.HasErrors && artifact != null ? "true" : "false").Append('\n');
            if (artifact != null)
            {
                builder.Append("artifactHash: ").Append(artifact.ArtifactHash).Append('\n');
                builder.Append("profileId: ").Append(artifact.Profile.ProfileId).Append('\n');
                builder.Append("profileHash: ").Append(artifact.Profile.ProfileHash).Append('\n');
                builder.Append("sourceClip: ").Append(artifact.Profile.SourceClipKey).Append('\n');
                builder.Append("sourceClipHash: ").Append(artifact.Profile.SourceClipHash).Append('\n');
                builder.Append("skeletonProfileId: ").Append(artifact.Profile.SkeletonProfileId).Append('\n');
                builder.Append("skeletonProfileHash: ").Append(artifact.Profile.SkeletonProfileHash).Append('\n');
                builder.Append("sampleTickRate: ").Append(artifact.Profile.SampleTickRate).Append('\n');
                builder.Append("quantizationScale: ").Append(artifact.Profile.QuantizationScale).Append('\n');
                builder.Append("coordinateSpace: ").Append(artifact.Profile.CoordinateSpace).Append('\n');
                builder.Append("roundingPolicy: ").Append(artifact.Profile.RoundingPolicy).Append('\n');
                builder.Append("importSettingsFingerprint: ").Append(artifact.Profile.ImportSettingsFingerprint).Append('\n');
                builder.Append("weaponTraceFrames: ").Append(artifact.WeaponTraceFrames.Count).Append('\n');
                builder.Append("rootMotionFrames: ").Append(artifact.RootMotionFrames.Count).Append('\n');
                builder.Append("socketTrajectoryFrames: ").Append(artifact.SocketFrames.Count).Append('\n');
                builder.Append("eventMarkers: ").Append(artifact.EventMarkers.Count).Append('\n');
                builder.Append("rootMotion:\n");
                for (int i = 0; i < artifact.RootMotionFrames.Count; i++)
                {
                    MxAnimationBakedRootMotionFrame frame = artifact.RootMotionFrames[i];
                    builder.Append("- frame: ").Append(frame.LocalFrame)
                        .Append(" position: ").Append(frame.RootPosition)
                        .Append(" delta: ").Append(frame.DeltaPosition).Append('\n');
                }

                builder.Append("socketTrajectory:\n");
                for (int i = 0; i < artifact.SocketFrames.Count; i++)
                {
                    MxAnimationBakedSocketFrame frame = artifact.SocketFrames[i];
                    builder.Append("- frame: ").Append(frame.LocalFrame)
                        .Append(" socket: ").Append(frame.SocketId)
                        .Append(" path: ").Append(string.IsNullOrEmpty(frame.SocketPath) ? "<root>" : frame.SocketPath)
                        .Append(" position: ").Append(frame.Position)
                        .Append(" delta: ").Append(frame.DeltaPosition).Append('\n');
                }

                builder.Append("weaponTrace:\n");
                for (int i = 0; i < artifact.WeaponTraceFrames.Count; i++)
                {
                    MxAnimationBakedWeaponTraceFrame frame = artifact.WeaponTraceFrames[i];
                    builder.Append("- frame: ").Append(frame.LocalFrame).Append(" trace: ").Append(frame.TraceId)
                        .Append(" rootNow: ").Append(frame.RootNow)
                        .Append(" tipNow: ").Append(frame.TipNow).Append('\n');
                }

                builder.Append("eventAlignment:\n");
                for (int i = 0; i < artifact.EventMarkers.Count; i++)
                {
                    MxAnimationBakedEventMarker marker = artifact.EventMarkers[i];
                    builder.Append("- frame: ").Append(marker.LocalFrame)
                        .Append(" presentationFrame: ").Append(marker.PresentationFrame)
                        .Append(" combatFrame: ").Append(marker.CombatFrame)
                        .Append(" event: ").Append(marker.EventId)
                        .Append(" kind: ").Append(marker.Kind)
                        .Append(" sourceOrder: ").Append(marker.SourceOrder).Append('\n');
                }
            }

            builder.Append("issues:\n");
            if (validation == null || validation.Issues.Count == 0)
            {
                builder.Append("- none\n");
            }
            else
            {
                for (int i = 0; i < validation.Issues.Count; i++)
                {
                    MxAnimationBakeIssue issue = validation.Issues[i];
                    builder.Append("- ").Append(issue.Severity)
                        .Append(' ').Append(issue.Code)
                        .Append(" field=").Append(issue.Field)
                        .Append(" expected=").Append(issue.Expected)
                        .Append(" actual=").Append(issue.Actual)
                        .Append(" location=").Append(issue.Location)
                        .Append(" message=").Append(issue.Message)
                        .Append('\n');
                }
            }

            return builder.ToString();
        }

        private static MxAnimationBakedVector3 SampleWeaponTip(
            AnimationClip clip,
            float time,
            MxAnimationBakeProfile profile,
            MxAnimationBakedVector3 fallbackSocket)
        {
            if (TrySampleVector(clip, WeaponTipPath, time, profile, out MxAnimationBakedVector3 tip))
                return tip;

            long forward = profile.QuantizationScale;
            return new MxAnimationBakedVector3(fallbackSocket.X, fallbackSocket.Y, fallbackSocket.Z + forward);
        }

        private static MxAnimationBakedVector3 SampleWeaponSocket(
            AnimationClip clip,
            float time,
            MxAnimationBakeProfile profile,
            MxAnimationBakedVector3 fallbackRoot)
        {
            return TrySampleVector(clip, WeaponSocketPath, time, profile, out MxAnimationBakedVector3 socket)
                ? socket
                : fallbackRoot;
        }

        private static MxAnimationBakedVector3 SampleVector(
            AnimationClip clip,
            string path,
            float time,
            MxAnimationBakeProfile profile)
        {
            return TrySampleVector(clip, path, time, profile, out MxAnimationBakedVector3 value)
                ? value
                : MxAnimationBakedVector3.Zero;
        }

        private static bool TrySampleVector(
            AnimationClip clip,
            string path,
            float time,
            MxAnimationBakeProfile profile,
            out MxAnimationBakedVector3 value)
        {
            bool x = TryEvaluateCurve(clip, path, "m_LocalPosition.x", time, out float fx)
                || TryEvaluateCurve(clip, path, "localPosition.x", time, out fx);
            bool y = TryEvaluateCurve(clip, path, "m_LocalPosition.y", time, out float fy)
                || TryEvaluateCurve(clip, path, "localPosition.y", time, out fy);
            bool z = TryEvaluateCurve(clip, path, "m_LocalPosition.z", time, out float fz)
                || TryEvaluateCurve(clip, path, "localPosition.z", time, out fz);

            if (!x && !y && !z)
            {
                value = MxAnimationBakedVector3.Zero;
                return false;
            }

            value = MxAnimationBakeQuantizer.QuantizeVector3(
                x ? fx : 0d,
                y ? fy : 0d,
                z ? fz : 0d,
                profile.QuantizationScale,
                profile.RoundingPolicy);
            return true;
        }

        private static bool TryEvaluateCurve(AnimationClip clip, string path, string propertyName, float time, out float value)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                if (!string.Equals(binding.path ?? string.Empty, path ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(binding.propertyName, propertyName, StringComparison.Ordinal))
                    continue;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                    break;

                value = curve.Evaluate(time);
                return true;
            }

            value = 0f;
            return false;
        }

        private static IReadOnlyList<MxAnimationBakedEventMarker> CreateEventMarkers(
            AnimationClip clip,
            MxAnimationBakeProfile profile)
        {
            var markers = new List<MxAnimationBakedEventMarker>();
            AnimationEvent[] events = clip.events ?? Array.Empty<AnimationEvent>();
            for (int i = 0; i < events.Length; i++)
            {
                AnimationEvent evt = events[i];
                int frame = Math.Max(0, (int)Math.Round(evt.time * profile.SampleTickRate, MidpointRounding.AwayFromZero));
                string eventId = string.IsNullOrWhiteSpace(evt.functionName) ? "event:" + i.ToString(CultureInfo.InvariantCulture) : evt.functionName;
                markers.Add(new MxAnimationBakedEventMarker(
                    frame,
                    eventId,
                    ClassifyEvent(eventId),
                    sourceOrder: i,
                    presentationFrame: frame,
                    combatFrame: -1));
            }

            return markers;
        }

        private static void AddSocketFrame(
            List<MxAnimationBakedSocketFrame> frames,
            int localFrame,
            string socketId,
            string socketPath,
            MxAnimationBakedVector3 position,
            MxAnimationBakedVector3 deltaPosition)
        {
            frames.Add(new MxAnimationBakedSocketFrame(localFrame, socketId, socketPath, position, deltaPosition));
        }

        private static MxAnimationBakeEventKind ClassifyEvent(string eventId)
        {
            return eventId != null && eventId.IndexOf("foot", StringComparison.OrdinalIgnoreCase) >= 0
                ? MxAnimationBakeEventKind.Footstep
                : MxAnimationBakeEventKind.Marker;
        }

        private static MxAnimationBakedVector3 Delta(MxAnimationBakedVector3 from, MxAnimationBakedVector3 to)
        {
            return new MxAnimationBakedVector3(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
        }

        private static string CreateImportFingerprint(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrWhiteSpace(path))
                return "memory:" + clip.name + ":" + clip.frameRate.ToString("R", CultureInfo.InvariantCulture);

            Hash128 hash = AssetDatabase.GetAssetDependencyHash(path);
            return path + ":" + hash;
        }

        private static string ComputeClipHash(AnimationClip clip)
        {
            var builder = new StringBuilder();
            builder.Append("clip=").Append(clip.name).Append('\n');
            builder.Append("length=").Append(clip.length.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("frameRate=").Append(clip.frameRate.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Array.Sort(bindings, CompareBinding);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                builder.Append("binding=").Append(binding.path).Append('|').Append(binding.type != null ? binding.type.FullName : string.Empty).Append('|').Append(binding.propertyName).Append('\n');
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                    continue;

                Keyframe[] keys = curve.keys;
                for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    Keyframe key = keys[keyIndex];
                    builder.Append("key=").Append(key.time.ToString("R", CultureInfo.InvariantCulture))
                        .Append(',').Append(key.value.ToString("R", CultureInfo.InvariantCulture))
                        .Append(',').Append(key.inTangent.ToString("R", CultureInfo.InvariantCulture))
                        .Append(',').Append(key.outTangent.ToString("R", CultureInfo.InvariantCulture))
                        .Append('\n');
                }
            }

            AnimationEvent[] events = clip.events ?? Array.Empty<AnimationEvent>();
            Array.Sort(events, CompareEvent);
            for (int i = 0; i < events.Length; i++)
            {
                AnimationEvent evt = events[i];
                builder.Append("event=").Append(evt.time.ToString("R", CultureInfo.InvariantCulture))
                    .Append('|').Append(evt.functionName ?? string.Empty)
                    .Append('|').Append(evt.stringParameter ?? string.Empty)
                    .Append('|').Append(evt.floatParameter.ToString("R", CultureInfo.InvariantCulture))
                    .Append('|').Append(evt.intParameter.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            return "sha256:" + Sha256Hex(builder.ToString());
        }

        private static int CompareBinding(EditorCurveBinding left, EditorCurveBinding right)
        {
            int result = string.CompareOrdinal(left.path, right.path);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.type != null ? left.type.FullName : string.Empty, right.type != null ? right.type.FullName : string.Empty);
            if (result != 0)
                return result;
            return string.CompareOrdinal(left.propertyName, right.propertyName);
        }

        private static int CompareEvent(AnimationEvent left, AnimationEvent right)
        {
            int result = left.time.CompareTo(right.time);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.functionName ?? string.Empty, right.functionName ?? string.Empty);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.stringParameter ?? string.Empty, right.stringParameter ?? string.Empty);
            if (result != 0)
                return result;
            result = left.floatParameter.CompareTo(right.floatParameter);
            if (result != 0)
                return result;
            return left.intParameter.CompareTo(right.intParameter);
        }

        private static string Sha256Hex(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static string NormalizeName(string value)
        {
            string name = (value ?? string.Empty).Trim().ToLowerInvariant();
            var builder = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
                builder.Append(valid ? c : '_');
            }

            string result = builder.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? "clip" : result;
        }

        private static string NormalizeAssetPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            return string.IsNullOrWhiteSpace(normalized) ? DefaultOutputRoot : normalized;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = NormalizeAssetPath(path).Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
                throw new InvalidOperationException("Output path must be under Assets/: " + path);

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    public static class MxAnimationCompatibilityEditorExtractor
    {
        public static MxAnimationSkeletonCompatibilityProfile CreateSkeletonProfile(
            GameObject root,
            string profileId,
            IEnumerable<string> socketPaths = null)
        {
            var bonePaths = new List<string>();
            if (root != null)
                CollectTransformPaths(root.transform, root.transform, bonePaths);

            return new MxAnimationSkeletonCompatibilityProfile(
                profileId,
                bonePaths: bonePaths,
                socketPaths: FilterKnownPaths(socketPaths, bonePaths));
        }

        public static MxAnimationClipCompatibilityProfile CreateClipProfile(
            AnimationClip clip,
            ResourceKey clipKey,
            MxAnimationSkeletonCompatibilityProfile skeletonProfile = null,
            string skeletonProfileId = "",
            string skeletonProfileHash = "")
        {
            IReadOnlyList<string> bindingPaths = ExtractClipBindingPaths(clip);
            return new MxAnimationClipCompatibilityProfile(
                clipKey,
                ResolveClipSkeletonProfileId(skeletonProfile, bindingPaths, skeletonProfileId),
                ResolveClipSkeletonProfileHash(skeletonProfile, bindingPaths, skeletonProfileHash),
                bindingPaths);
        }

        public static MxAnimationAvatarMaskCompatibilityProfile CreateAvatarMaskProfile(
            AvatarMask avatarMask,
            ResourceKey avatarMaskKey,
            MxAnimationSkeletonCompatibilityProfile skeletonProfile = null,
            string skeletonProfileId = "",
            string skeletonProfileHash = "")
        {
            return new MxAnimationAvatarMaskCompatibilityProfile(
                avatarMaskKey,
                ResolveSkeletonProfileId(skeletonProfile, skeletonProfileId),
                ResolveSkeletonProfileHash(skeletonProfile, skeletonProfileHash),
                ExtractAvatarMaskActivePaths(avatarMask));
        }

        public static MxAnimationCompatibilityProfile CreateProfile(
            MxAnimationSkeletonCompatibilityProfile skeletonProfile,
            IEnumerable<MxAnimationClipCompatibilityProfile> clipProfiles = null,
            IEnumerable<MxAnimationAvatarMaskCompatibilityProfile> avatarMaskProfiles = null,
            IEnumerable<MxAnimationBakeArtifact> bakeArtifacts = null)
        {
            return new MxAnimationCompatibilityProfile(
                skeletonProfile,
                clipProfiles,
                avatarMaskProfiles,
                bakeArtifacts);
        }

        public static MxAnimationCompatibilityValidationReport Validate(
            MxAnimationCompatibilityProfile profile,
            MxAnimationCompatibilityExpectation expectation)
        {
            return MxAnimationCompatibilityValidator.Validate(profile, expectation);
        }

        public static IReadOnlyList<string> ExtractClipBindingPaths(AnimationClip clip)
        {
            if (clip == null)
                return Array.Empty<string>();

            var paths = new List<string>();
            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < curveBindings.Length; i++)
                AddPath(paths, curveBindings[i].path);

            EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            for (int i = 0; i < objectBindings.Length; i++)
                AddPath(paths, objectBindings[i].path);

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        public static IReadOnlyList<string> ExtractAvatarMaskActivePaths(AvatarMask avatarMask)
        {
            if (avatarMask == null)
                return Array.Empty<string>();

            var paths = new List<string>();
            for (int i = 0; i < avatarMask.transformCount; i++)
            {
                if (!avatarMask.GetTransformActive(i))
                    continue;

                AddPath(paths, avatarMask.GetTransformPath(i));
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        private static void CollectTransformPaths(Transform root, Transform current, List<string> paths)
        {
            if (root == null || current == null)
                return;

            AddPath(paths, CreateRelativePath(root, current));
            for (int i = 0; i < current.childCount; i++)
                CollectTransformPaths(root, current.GetChild(i), paths);
        }

        private static IReadOnlyList<string> FilterKnownPaths(IEnumerable<string> paths, IReadOnlyList<string> knownPaths)
        {
            if (paths == null)
                return Array.Empty<string>();

            var filtered = new List<string>();
            foreach (string path in paths)
            {
                string normalized = NormalizePath(path);
                if (string.IsNullOrWhiteSpace(normalized) || !ContainsPath(knownPaths, normalized) || filtered.Contains(normalized))
                    continue;

                filtered.Add(normalized);
            }

            filtered.Sort(StringComparer.Ordinal);
            return filtered;
        }

        private static string CreateRelativePath(Transform root, Transform current)
        {
            if (current == root)
                return string.Empty;

            var names = new List<string>();
            Transform cursor = current;
            while (cursor != null && cursor != root)
            {
                names.Add(cursor.name);
                cursor = cursor.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static void AddPath(List<string> paths, string path)
        {
            string normalized = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            if (!paths.Contains(normalized))
                paths.Add(normalized);
        }

        private static string ResolveClipSkeletonProfileId(
            MxAnimationSkeletonCompatibilityProfile profile,
            IReadOnlyList<string> bindingPaths,
            string fallback)
        {
            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;

            return ClipBindingsMatchSkeleton(profile, bindingPaths) ? profile.ProfileId : string.Empty;
        }

        private static string ResolveClipSkeletonProfileHash(
            MxAnimationSkeletonCompatibilityProfile profile,
            IReadOnlyList<string> bindingPaths,
            string fallback)
        {
            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;

            return ClipBindingsMatchSkeleton(profile, bindingPaths) ? profile.ProfileHash : string.Empty;
        }

        private static string ResolveSkeletonProfileId(
            MxAnimationSkeletonCompatibilityProfile profile,
            string fallback)
        {
            return profile != null ? profile.ProfileId : fallback ?? string.Empty;
        }

        private static string ResolveSkeletonProfileHash(
            MxAnimationSkeletonCompatibilityProfile profile,
            string fallback)
        {
            return profile != null ? profile.ProfileHash : fallback ?? string.Empty;
        }

        private static bool ClipBindingsMatchSkeleton(
            MxAnimationSkeletonCompatibilityProfile profile,
            IReadOnlyList<string> bindingPaths)
        {
            if (profile == null)
                return false;

            for (int i = 0; i < bindingPaths.Count; i++)
            {
                string path = bindingPaths[i];
                if (!profile.ContainsBonePath(path) && !profile.ContainsSocketPath(path))
                    return false;
            }

            return true;
        }

        private static bool ContainsPath(IReadOnlyList<string> paths, string path)
        {
            string normalized = NormalizePath(path);
            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], normalized, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
        }
    }
}
