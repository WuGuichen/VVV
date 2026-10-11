using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.Animation
{
    internal enum MxAnimationClipNameNormalizationStatus
    {
        Renamed,
        Unchanged,
        Conflict,
        Failed
    }

    internal sealed class MxAnimationClipNameNormalizationRecord
    {
        public string AssetPath { get; set; }
        public string OriginalName { get; set; }
        public string NormalizedName { get; set; }
        public string TargetAssetPath { get; set; }
        public MxAnimationClipNameNormalizationStatus Status { get; set; }
        public string Error { get; set; }
    }

    internal sealed class MxAnimationClipNameNormalizationReport
    {
        private readonly List<MxAnimationClipNameNormalizationRecord> _records = new List<MxAnimationClipNameNormalizationRecord>();

        public IReadOnlyList<MxAnimationClipNameNormalizationRecord> Records => _records;

        public int RenamedCount { get; private set; }
        public int UnchangedCount { get; private set; }
        public int ConflictCount { get; private set; }
        public int FailedCount { get; private set; }

        public void Add(MxAnimationClipNameNormalizationRecord record)
        {
            if (record == null)
                return;

            _records.Add(record);
            switch (record.Status)
            {
                case MxAnimationClipNameNormalizationStatus.Renamed:
                    RenamedCount++;
                    break;
                case MxAnimationClipNameNormalizationStatus.Unchanged:
                    UnchangedCount++;
                    break;
                case MxAnimationClipNameNormalizationStatus.Conflict:
                    ConflictCount++;
                    break;
                case MxAnimationClipNameNormalizationStatus.Failed:
                    FailedCount++;
                    break;
            }
        }
    }

    internal static class MxAnimationClipNameNormalizer
    {
        private const string MenuPath = "MxFramework/Assets/Normalize Selected Animation Clip Names";

        [MenuItem(MenuPath, priority = 131)]
        public static void NormalizeSelectedAnimationClipNames()
        {
            string[] paths = CollectSelectedAnimationClipAssetPaths();
            if (paths.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Normalize Animation Clip Names",
                    "Select one or more .anim assets, or folders containing .anim assets.",
                    "OK");
                return;
            }

            MxAnimationClipNameNormalizationReport report = NormalizeClipAssetNames(paths);
            if (report.RenamedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string reportText = CreateReportText(report);
            Debug.Log(reportText);
            EditorUtility.DisplayDialog(
                "Normalize Animation Clip Names",
                "Renamed: " + report.RenamedCount +
                "\nUnchanged: " + report.UnchangedCount +
                "\nConflicts: " + report.ConflictCount +
                "\nFailed: " + report.FailedCount,
                "OK");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool CanNormalizeSelectedAnimationClipNames()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                HasSelectedAnimationClipAssetOrFolder();
        }

        public static MxAnimationClipNameNormalizationReport NormalizeClipAssetNames(IEnumerable<string> assetPaths)
        {
            var report = new MxAnimationClipNameNormalizationReport();
            if (assetPaths == null)
                return report;

            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string assetPath in assetPaths)
                AddAnimationClipAssetPath(paths, seen, assetPath);
            paths.Sort(StringComparer.Ordinal);

            for (int i = 0; i < paths.Count; i++)
                NormalizeClipAssetName(paths[i], report);

            return report;
        }

        public static string CreateReportText(MxAnimationClipNameNormalizationReport report)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Clip Name Normalization Report\n");
            builder.Append("records: ").Append(report != null ? report.Records.Count : 0).Append('\n');
            builder.Append("renamed: ").Append(report != null ? report.RenamedCount : 0).Append('\n');
            builder.Append("unchanged: ").Append(report != null ? report.UnchangedCount : 0).Append('\n');
            builder.Append("conflict: ").Append(report != null ? report.ConflictCount : 0).Append('\n');
            builder.Append("failed: ").Append(report != null ? report.FailedCount : 0).Append('\n');
            builder.Append("entries:\n");

            if (report == null || report.Records.Count == 0)
            {
                builder.Append("- none\n");
                return builder.ToString();
            }

            for (int i = 0; i < report.Records.Count; i++)
            {
                MxAnimationClipNameNormalizationRecord record = report.Records[i];
                builder.Append("- assetPath: ").Append(record.AssetPath).Append('\n');
                builder.Append("  originalName: ").Append(record.OriginalName).Append('\n');
                builder.Append("  normalizedName: ").Append(record.NormalizedName).Append('\n');
                builder.Append("  targetAssetPath: ").Append(record.TargetAssetPath).Append('\n');
                builder.Append("  status: ").Append(record.Status).Append('\n');
                builder.Append("  error: ").Append(record.Error).Append('\n');
            }

            return builder.ToString();
        }

        private static string[] CollectSelectedAnimationClipAssetPaths()
        {
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            string[] guids = Selection.assetGUIDs ?? Array.Empty<string>();
            for (int i = 0; i < guids.Length; i++)
                AddSelectedAssetPath(paths, seen, AssetDatabase.GUIDToAssetPath(guids[i]));

            UnityEngine.Object[] objects = Selection.objects ?? Array.Empty<UnityEngine.Object>();
            for (int i = 0; i < objects.Length; i++)
                AddSelectedAssetPath(paths, seen, AssetDatabase.GetAssetPath(objects[i]));

            paths.Sort(StringComparer.Ordinal);
            return paths.ToArray();
        }

        private static bool HasSelectedAnimationClipAssetOrFolder()
        {
            string[] guids = Selection.assetGUIDs ?? Array.Empty<string>();
            for (int i = 0; i < guids.Length; i++)
            {
                if (IsAnimationClipAssetOrFolder(AssetDatabase.GUIDToAssetPath(guids[i])))
                    return true;
            }

            UnityEngine.Object[] objects = Selection.objects ?? Array.Empty<UnityEngine.Object>();
            for (int i = 0; i < objects.Length; i++)
            {
                if (IsAnimationClipAssetOrFolder(AssetDatabase.GetAssetPath(objects[i])))
                    return true;
            }

            return false;
        }

        private static bool IsAnimationClipAssetOrFolder(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            return AssetDatabase.IsValidFolder(assetPath) ||
                assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddSelectedAssetPath(List<string> paths, HashSet<string> seen, string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { assetPath });
                Array.Sort(guids, StringComparer.Ordinal);
                for (int i = 0; i < guids.Length; i++)
                    AddAnimationClipAssetPath(paths, seen, AssetDatabase.GUIDToAssetPath(guids[i]));
                return;
            }

            AddAnimationClipAssetPath(paths, seen, assetPath);
        }

        private static void AddAnimationClipAssetPath(List<string> paths, HashSet<string> seen, string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                return;
            if (!assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                return;
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath) == null)
                return;
            if (seen.Add(assetPath))
                paths.Add(assetPath);
        }

        private static void NormalizeClipAssetName(string assetPath, MxAnimationClipNameNormalizationReport report)
        {
            string originalName = Path.GetFileNameWithoutExtension(assetPath);
            string normalizedName = MxAnimationClipFbxExtractor.NormalizeClipName(originalName);
            string targetAssetPath = GetTargetAssetPath(assetPath, normalizedName);
            if (string.Equals(originalName, normalizedName, StringComparison.Ordinal))
            {
                report.Add(CreateRecord(
                    assetPath,
                    originalName,
                    normalizedName,
                    targetAssetPath,
                    MxAnimationClipNameNormalizationStatus.Unchanged,
                    string.Empty));
                return;
            }

            if (!string.Equals(assetPath, targetAssetPath, StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetAssetPath) != null)
            {
                report.Add(CreateRecord(
                    assetPath,
                    originalName,
                    normalizedName,
                    targetAssetPath,
                    MxAnimationClipNameNormalizationStatus.Conflict,
                    "Target asset path already exists."));
                return;
            }

            string error = AssetDatabase.RenameAsset(assetPath, normalizedName);
            report.Add(CreateRecord(
                assetPath,
                originalName,
                normalizedName,
                targetAssetPath,
                string.IsNullOrEmpty(error) ? MxAnimationClipNameNormalizationStatus.Renamed : MxAnimationClipNameNormalizationStatus.Failed,
                error ?? string.Empty));
        }

        private static MxAnimationClipNameNormalizationRecord CreateRecord(
            string assetPath,
            string originalName,
            string normalizedName,
            string targetAssetPath,
            MxAnimationClipNameNormalizationStatus status,
            string error)
        {
            return new MxAnimationClipNameNormalizationRecord
            {
                AssetPath = assetPath ?? string.Empty,
                OriginalName = originalName ?? string.Empty,
                NormalizedName = normalizedName ?? string.Empty,
                TargetAssetPath = targetAssetPath ?? string.Empty,
                Status = status,
                Error = error ?? string.Empty
            };
        }

        private static string GetTargetAssetPath(string assetPath, string normalizedName)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            return string.IsNullOrWhiteSpace(directory)
                ? normalizedName + ".anim"
                : directory + "/" + normalizedName + ".anim";
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
