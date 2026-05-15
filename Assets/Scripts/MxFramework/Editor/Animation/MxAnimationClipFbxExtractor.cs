using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MxFramework.Resources;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.Animation
{
    public enum MxAnimationClipCollisionPolicy
    {
        Skip,
        Overwrite,
        Rename
    }

    public enum MxAnimationClipExtractionStatus
    {
        Success,
        Skipped,
        Failed,
        NoClip,
        Conflict,
        Warning
    }

    public sealed class MxAnimationClipExtractionRecord
    {
        public string SourceAssetPath { get; set; }
        public string SourceClipName { get; set; }
        public string TargetAssetPath { get; set; }
        public string ResourceKey { get; set; }
        public string ResourceTypeId { get; set; }
        public MxAnimationClipExtractionStatus Status { get; set; }
        public string Reason { get; set; }
        public string Error { get; set; }
        public MxAnimationClipCollisionPolicy CollisionPolicy { get; set; }
        public string[] Labels { get; set; }
        public string BundleName { get; set; }
    }

    public sealed class MxAnimationClipExtractionReport
    {
        private readonly List<MxAnimationClipExtractionRecord> _records = new List<MxAnimationClipExtractionRecord>();

        public IReadOnlyList<MxAnimationClipExtractionRecord> Records => _records;

        public int SuccessCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int FailedCount { get; private set; }
        public int NoClipCount { get; private set; }
        public int ConflictCount { get; private set; }
        public int WarningCount { get; private set; }

        public void Add(MxAnimationClipExtractionRecord record)
        {
            if (record == null)
                return;

            _records.Add(record);
            switch (record.Status)
            {
                case MxAnimationClipExtractionStatus.Success:
                    SuccessCount++;
                    break;
                case MxAnimationClipExtractionStatus.Skipped:
                    SkippedCount++;
                    break;
                case MxAnimationClipExtractionStatus.Failed:
                    FailedCount++;
                    break;
                case MxAnimationClipExtractionStatus.NoClip:
                    NoClipCount++;
                    break;
                case MxAnimationClipExtractionStatus.Conflict:
                    ConflictCount++;
                    break;
                case MxAnimationClipExtractionStatus.Warning:
                    WarningCount++;
                    break;
            }
        }
    }

    public static class MxAnimationClipFbxExtractor
    {
        public const string DefaultSourceRoot = "Assets/_TempImportedResources/Art/Animations";
        public const string DefaultOutputRoot = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips";
        public const string ResourceKeyPrefix = "art.character.skeleton.animation.";
        public const string BundleName = "mxframework.samples.art.characters.skeleton.animations";
        public const MxAnimationClipCollisionPolicy DefaultCollisionPolicy = MxAnimationClipCollisionPolicy.Skip;

        private const string MenuPath = "MxFramework/Assets/Extract Animation Clips From FBX";
        private static readonly string[] CatalogLabels =
        {
            "package.mxframework.samples",
            "domain.art",
            "sample.characters",
            "sample.skeleton",
            "asset.animation_clip"
        };

        [MenuItem(MenuPath, priority = 130)]
        public static void ExtractFromSelectionOrDefault()
        {
            MxAnimationClipExtractionReport report = ExtractFromSelectionOrDefault(DefaultCollisionPolicy);
            LogReport(report);
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool CanExtractFromSelectionOrDefault()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        public static void ExtractDefaultSourceForBatch()
        {
            MxAnimationClipExtractionReport report = ExtractFromSources(
                new[] { DefaultSourceRoot },
                DefaultOutputRoot,
                DefaultCollisionPolicy);
            LogReport(report);
        }

        public static MxAnimationClipExtractionReport ExtractFromSelectionOrDefault(
            MxAnimationClipCollisionPolicy collisionPolicy)
        {
            string[] sources = GetSelectedSources();
            if (sources.Length == 0)
                sources = new[] { DefaultSourceRoot };

            return ExtractFromSources(sources, DefaultOutputRoot, collisionPolicy);
        }

        public static MxAnimationClipExtractionReport ExtractFromSources(
            IEnumerable<string> sourcePaths,
            string outputRoot,
            MxAnimationClipCollisionPolicy collisionPolicy)
        {
            var report = new MxAnimationClipExtractionReport();
            string normalizedOutputRoot = NormalizeAssetPath(outputRoot);
            EnsureFolder(normalizedOutputRoot);

            string[] modelPaths = CollectModelPaths(sourcePaths);
            for (int i = 0; i < modelPaths.Length; i++)
                ExtractModel(modelPaths[i], normalizedOutputRoot, collisionPolicy, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report;
        }

        public static string NormalizeClipName(string clipName)
        {
            string value = (clipName ?? string.Empty).Trim();
            value = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2");
            value = Regex.Replace(value, "[^A-Za-z0-9]+", "_");
            value = Regex.Replace(value, "_+", "_").Trim('_').ToLowerInvariant();
            return string.IsNullOrEmpty(value) ? "clip" : value;
        }

        private static string CreateTargetClipName(string sourceAssetPath, string sourceClipName)
        {
            string normalizedClipName = NormalizeClipName(sourceClipName);
            if (!IsGenericImportedClipName(normalizedClipName))
                return normalizedClipName;

            return NormalizeClipName(Path.GetFileNameWithoutExtension(sourceAssetPath));
        }

        private static bool IsGenericImportedClipName(string normalizedClipName)
        {
            return string.Equals(normalizedClipName, "mixamo_com", StringComparison.Ordinal)
                || string.Equals(normalizedClipName, "take_001", StringComparison.Ordinal)
                || string.Equals(normalizedClipName, "default_take", StringComparison.Ordinal)
                || string.Equals(normalizedClipName, "animation", StringComparison.Ordinal)
                || string.Equals(normalizedClipName, "anim", StringComparison.Ordinal);
        }

        public static string CreateCatalogReadyReportText(MxAnimationClipExtractionReport report)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Clip Extraction Report\n");
            builder.Append("sourceDefault: ").Append(DefaultSourceRoot).Append('\n');
            builder.Append("outputRoot: ").Append(DefaultOutputRoot).Append('\n');
            builder.Append("typeId: ").Append(ResourceTypeIds.AnimationClip).Append('\n');
            builder.Append("bundleName: ").Append(BundleName).Append('\n');
            builder.Append("records: ").Append(report != null ? report.Records.Count : 0).Append('\n');
            builder.Append("success: ").Append(report != null ? report.SuccessCount : 0).Append('\n');
            builder.Append("skipped: ").Append(report != null ? report.SkippedCount : 0).Append('\n');
            builder.Append("failed: ").Append(report != null ? report.FailedCount : 0).Append('\n');
            builder.Append("noClip: ").Append(report != null ? report.NoClipCount : 0).Append('\n');
            builder.Append("conflict: ").Append(report != null ? report.ConflictCount : 0).Append('\n');
            builder.Append("warning: ").Append(report != null ? report.WarningCount : 0).Append('\n');
            builder.Append("entries:\n");

            if (report == null || report.Records.Count == 0)
            {
                builder.Append("- none\n");
                return builder.ToString();
            }

            for (int i = 0; i < report.Records.Count; i++)
            {
                MxAnimationClipExtractionRecord record = report.Records[i];
                builder.Append("- sourceAssetPath: ").Append(record.SourceAssetPath).Append('\n');
                builder.Append("  sourceClipName: ").Append(record.SourceClipName).Append('\n');
                builder.Append("  targetAssetPath: ").Append(record.TargetAssetPath).Append('\n');
                builder.Append("  resourceKey: ").Append(record.ResourceKey).Append('\n');
                builder.Append("  resourceTypeId: ").Append(record.ResourceTypeId).Append('\n');
                builder.Append("  status: ").Append(record.Status).Append('\n');
                builder.Append("  reason: ").Append(record.Reason).Append('\n');
                builder.Append("  error: ").Append(record.Error).Append('\n');
                builder.Append("  collisionPolicy: ").Append(record.CollisionPolicy).Append('\n');
                builder.Append("  labels: ").Append(string.Join(", ", record.Labels ?? Array.Empty<string>())).Append('\n');
                builder.Append("  bundleName: ").Append(record.BundleName).Append('\n');
            }

            return builder.ToString();
        }

        private static void ExtractModel(
            string sourceAssetPath,
            string outputRoot,
            MxAnimationClipCollisionPolicy collisionPolicy,
            MxAnimationClipExtractionReport report)
        {
            AnimationClip[] clips = LoadValidClips(sourceAssetPath);
            if (clips.Length == 0)
            {
                report.Add(CreateRecord(
                    sourceAssetPath,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    MxAnimationClipExtractionStatus.NoClip,
                    "No valid imported AnimationClip was found.",
                    string.Empty,
                    collisionPolicy));
                return;
            }

            for (int i = 0; i < clips.Length; i++)
                ExtractClip(sourceAssetPath, clips[i], outputRoot, collisionPolicy, report);
        }

        private static void ExtractClip(
            string sourceAssetPath,
            AnimationClip sourceClip,
            string outputRoot,
            MxAnimationClipCollisionPolicy collisionPolicy,
            MxAnimationClipExtractionReport report)
        {
            string clipName = CreateTargetClipName(sourceAssetPath, sourceClip.name);
            string targetAssetPath = outputRoot + "/" + clipName + ".anim";
            string resourceKey = ResourceKeyPrefix + clipName;

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(targetAssetPath) != null)
            {
                if (collisionPolicy == MxAnimationClipCollisionPolicy.Skip)
                {
                    report.Add(CreateRecord(
                        sourceAssetPath,
                        sourceClip.name,
                        targetAssetPath,
                        resourceKey,
                        MxAnimationClipExtractionStatus.Skipped,
                        "Target .anim already exists; default collision policy is Skip.",
                        string.Empty,
                        collisionPolicy));
                    return;
                }

                if (collisionPolicy == MxAnimationClipCollisionPolicy.Rename)
                {
                    targetAssetPath = AssetDatabase.GenerateUniqueAssetPath(targetAssetPath);
                    clipName = Path.GetFileNameWithoutExtension(targetAssetPath);
                    resourceKey = ResourceKeyPrefix + clipName;
                }
                else if (!AssetDatabase.DeleteAsset(targetAssetPath))
                {
                    report.Add(CreateRecord(
                        sourceAssetPath,
                        sourceClip.name,
                        targetAssetPath,
                        resourceKey,
                        MxAnimationClipExtractionStatus.Conflict,
                        "Target .anim exists and could not be removed for overwrite.",
                        string.Empty,
                        collisionPolicy));
                    return;
                }
            }

            try
            {
                CopyClipToAsset(sourceClip, targetAssetPath);
                report.Add(CreateRecord(
                    sourceAssetPath,
                    sourceClip.name,
                    targetAssetPath,
                    resourceKey,
                    MxAnimationClipExtractionStatus.Success,
                    "Created .anim through Unity Editor API. AnimationEvents are presentation-only metadata if present.",
                    string.Empty,
                    collisionPolicy));
            }
            catch (Exception exception)
            {
                report.Add(CreateRecord(
                    sourceAssetPath,
                    sourceClip.name,
                    targetAssetPath,
                    resourceKey,
                    MxAnimationClipExtractionStatus.Failed,
                    "Failed to copy imported clip curves/settings.",
                    exception.Message,
                    collisionPolicy));
            }
        }

        private static void CopyClipToAsset(AnimationClip sourceClip, string targetAssetPath)
        {
            var targetClip = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(targetAssetPath),
                frameRate = sourceClip.frameRate,
                legacy = sourceClip.legacy,
                wrapMode = sourceClip.wrapMode
            };

            EditorCurveBinding[] floatBindings = AnimationUtility.GetCurveBindings(sourceClip);
            for (int i = 0; i < floatBindings.Length; i++)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, floatBindings[i]);
                if (curve == null)
                    throw new InvalidOperationException("Float curve copy failed for binding " + floatBindings[i].propertyName + ".");

                var copiedCurve = new AnimationCurve(curve.keys)
                {
                    preWrapMode = curve.preWrapMode,
                    postWrapMode = curve.postWrapMode
                };
                AnimationUtility.SetEditorCurve(targetClip, floatBindings[i], copiedCurve);
            }

            EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(sourceClip);
            for (int i = 0; i < objectBindings.Length; i++)
            {
                ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(sourceClip, objectBindings[i]);
                if (curve == null)
                    throw new InvalidOperationException("Object reference curve copy failed for binding " + objectBindings[i].propertyName + ".");

                AnimationUtility.SetObjectReferenceCurve(targetClip, objectBindings[i], curve);
            }

            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(sourceClip);
            if (events != null && events.Length > 0)
                AnimationUtility.SetAnimationEvents(targetClip, events);

            AnimationUtility.SetAnimationClipSettings(targetClip, AnimationUtility.GetAnimationClipSettings(sourceClip));
            AssetDatabase.CreateAsset(targetClip, targetAssetPath);
        }

        private static AnimationClip[] LoadValidClips(string sourceAssetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(sourceAssetPath);
            var clips = new List<AnimationClip>();
            for (int i = 0; i < assets.Length; i++)
            {
                var clip = assets[i] as AnimationClip;
                if (IsValidSourceClip(clip))
                    clips.Add(clip);
            }

            return clips.ToArray();
        }

        private static bool IsValidSourceClip(AnimationClip clip)
        {
            if (clip == null || string.IsNullOrWhiteSpace(clip.name))
                return false;

            if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                return false;

            int floatCurveCount = AnimationUtility.GetCurveBindings(clip).Length;
            int objectCurveCount = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
            int eventCount = AnimationUtility.GetAnimationEvents(clip).Length;
            return floatCurveCount > 0 || objectCurveCount > 0 || eventCount > 0 || clip.length > 0f;
        }

        private static string[] GetSelectedSources()
        {
            UnityEngine.Object[] selectedAssets = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);
            var paths = new List<string>();
            for (int i = 0; i < selectedAssets.Length; i++)
            {
                string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(selectedAssets[i]));
                if (string.IsNullOrEmpty(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path) || IsModelPath(path))
                    paths.Add(path);
            }

            return paths.ToArray();
        }

        private static string[] CollectModelPaths(IEnumerable<string> sourcePaths)
        {
            var uniquePaths = new SortedSet<string>(StringComparer.Ordinal);
            if (sourcePaths == null)
                return Array.Empty<string>();

            foreach (string sourcePath in sourcePaths)
            {
                string normalizedPath = NormalizeAssetPath(sourcePath);
                if (string.IsNullOrEmpty(normalizedPath))
                    continue;

                if (AssetDatabase.IsValidFolder(normalizedPath))
                {
                    string[] guids = AssetDatabase.FindAssets("t:Model", new[] { normalizedPath });
                    for (int i = 0; i < guids.Length; i++)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                        if (IsModelPath(assetPath))
                            uniquePaths.Add(assetPath);
                    }
                }
                else if (IsModelPath(normalizedPath))
                {
                    uniquePaths.Add(normalizedPath);
                }
            }

            var paths = new string[uniquePaths.Count];
            uniquePaths.CopyTo(paths);
            return paths;
        }

        private static bool IsModelPath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase);
        }

        private static MxAnimationClipExtractionRecord CreateRecord(
            string sourceAssetPath,
            string sourceClipName,
            string targetAssetPath,
            string resourceKey,
            MxAnimationClipExtractionStatus status,
            string reason,
            string error,
            MxAnimationClipCollisionPolicy collisionPolicy)
        {
            return new MxAnimationClipExtractionRecord
            {
                SourceAssetPath = sourceAssetPath ?? string.Empty,
                SourceClipName = sourceClipName ?? string.Empty,
                TargetAssetPath = targetAssetPath ?? string.Empty,
                ResourceKey = resourceKey ?? string.Empty,
                ResourceTypeId = string.IsNullOrWhiteSpace(resourceKey)
                    ? string.Empty
                    : ResourceTypeIds.AnimationClip,
                Status = status,
                Reason = reason ?? string.Empty,
                Error = error ?? string.Empty,
                CollisionPolicy = collisionPolicy,
                Labels = CatalogLabels,
                BundleName = BundleName
            };
        }

        private static void LogReport(MxAnimationClipExtractionReport report)
        {
            string reportText = CreateCatalogReadyReportText(report);
            if (report != null && report.FailedCount > 0)
                Debug.LogError(reportText);
            else if (report != null && (report.WarningCount > 0 || report.ConflictCount > 0))
                Debug.LogWarning(reportText);
            else
                Debug.Log(reportText);
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }
    }
}
