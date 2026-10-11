using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.Animation
{
    [InitializeOnLoad]
    internal static class MxAnimationClipExtractionRequestBridge
    {
        private const string RequestRelativePath = "Temp/MxFrameworkAuthoring/AnimationClipExtraction/extract_request.json";
        private const string StatusDirectoryRelativePath = "Temp/MxFrameworkAuthoring/AnimationClipExtraction/status";
        private const string StagingAssetRoot = "Assets/_TempImportedResources/AnimationClipExtraction";
        private const double PollIntervalSeconds = 1.0d;

        private static double s_nextPollTime;
        private static string s_lastConsumedRequestId = string.Empty;

        static MxAnimationClipExtractionRequestBridge()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.delayCall += ConsumePendingRequest;
        }

        private static void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < s_nextPollTime)
                return;

            s_nextPollTime = EditorApplication.timeSinceStartup + PollIntervalSeconds;
            ConsumePendingRequest();
        }

        private static void ConsumePendingRequest()
        {
            if (EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string requestPath = ProjectPath(RequestRelativePath);
            if (!File.Exists(requestPath))
                return;

            string json;
            try
            {
                json = File.ReadAllText(requestPath);
            }
            catch (IOException)
            {
                return;
            }

            if (!TryReadRequest(json, out AnimationClipExtractionRequest request, out string error))
            {
                Debug.LogWarning("MxFramework Animation Clip Extraction: request ignored: " + error);
                return;
            }

            if (string.Equals(s_lastConsumedRequestId, request.RequestId, StringComparison.Ordinal))
            {
                TryDeleteFile(requestPath);
                return;
            }

            s_lastConsumedRequestId = request.RequestId;
            TryDeleteFile(requestPath);
            WriteStatus(request, "running", "Unity Editor is importing FBX animation sources.", null, string.Empty);

            string stagingAssetPath = string.Empty;
            try
            {
                string sourceFullPath = ResolveProjectPath(request.SourceRoot);
                string sourceAssetPath;
                if (TryConvertToAssetPath(sourceFullPath, out string existingAssetPath))
                {
                    sourceAssetPath = existingAssetPath;
                }
                else
                {
                    stagingAssetPath = StagingAssetRoot + "/" + SanitizeRequestId(request.RequestId);
                    PrepareStagingDirectory(stagingAssetPath);
                    int sourceCount = CopyFbxSources(
                        sourceFullPath,
                        AssetPathToFullPath(stagingAssetPath),
                        request.CopyMetaFiles);
                    if (sourceCount == 0)
                        throw new InvalidOperationException("No FBX animation source was found under " + request.SourceRoot + ".");

                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    sourceAssetPath = stagingAssetPath;
                }

                var options = new MxAnimationClipExtractionOptions
                {
                    ResourceKeyPrefix = request.ResourceKeyPrefix,
                    BundleName = request.BundleName,
                    Labels = request.Labels
                };
                MxAnimationClipExtractionReport report = MxAnimationClipFbxExtractor.ExtractFromSources(
                    new[] { sourceAssetPath },
                    request.OutputRoot,
                    request.CollisionPolicy,
                    options);

                bool success = report != null
                    && report.Records.Count > 0
                    && report.FailedCount == 0
                    && report.ConflictCount == 0
                    && report.NoClipCount == 0;
                string message = success
                    ? "Unity Editor produced " + report.Records.Count + " standalone AnimationClip asset(s)."
                    : "Animation extraction completed with blocking records.";
                WriteStatus(request, success ? "completed" : "failed", message, report, string.Empty);
                if (success)
                {
                    Debug.Log("MxFramework Animation Clip Extraction: " + message
                        + " output=" + request.OutputRoot
                        + " requestId=" + request.RequestId);
                }
                else
                {
                    Debug.LogError(MxAnimationClipFbxExtractor.CreateCatalogReadyReportText(report));
                }
            }
            catch (Exception ex)
            {
                WriteStatus(request, "failed", "Animation extraction failed.", null, ex.Message);
                Debug.LogError("MxFramework Animation Clip Extraction: request failed: " + ex);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(stagingAssetPath))
                    CleanupStagingDirectory(stagingAssetPath);
            }
        }

        private static bool TryReadRequest(
            string json,
            out AnimationClipExtractionRequest request,
            out string error)
        {
            request = null;
            error = string.Empty;
            AnimationClipExtractionRequestFile file;
            try
            {
                file = JsonConvert.DeserializeObject<AnimationClipExtractionRequestFile>(json);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (file == null)
            {
                error = "request json is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(file.RequestId)
                || string.IsNullOrWhiteSpace(file.SourceRoot)
                || string.IsNullOrWhiteSpace(file.OutputRoot)
                || string.IsNullOrWhiteSpace(file.ResourceKeyPrefix))
            {
                error = "requestId, sourceRoot, outputRoot, and resourceKeyPrefix are required.";
                return false;
            }

            string outputRoot = NormalizeAssetPath(file.OutputRoot);
            if (!outputRoot.StartsWith("Assets/", StringComparison.Ordinal)
                || outputRoot.StartsWith(StagingAssetRoot + "/", StringComparison.Ordinal))
            {
                error = "outputRoot must be a persistent folder below Assets and outside the extraction staging root.";
                return false;
            }

            string sourceRoot;
            try
            {
                sourceRoot = ResolveProjectPath(file.SourceRoot);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (!Directory.Exists(sourceRoot))
            {
                error = "sourceRoot was not found: " + file.SourceRoot;
                return false;
            }

            MxAnimationClipCollisionPolicy collisionPolicy = MxAnimationClipCollisionPolicy.Skip;
            if (!string.IsNullOrWhiteSpace(file.CollisionPolicy)
                && !Enum.TryParse(file.CollisionPolicy, true, out collisionPolicy))
            {
                error = "collisionPolicy must be Skip, Overwrite, or Rename.";
                return false;
            }

            request = new AnimationClipExtractionRequest
            {
                RequestId = file.RequestId.Trim(),
                SourceRoot = file.SourceRoot.Trim(),
                OutputRoot = outputRoot,
                ResourceKeyPrefix = file.ResourceKeyPrefix.Trim(),
                BundleName = (file.BundleName ?? string.Empty).Trim(),
                Labels = NormalizeLabels(file.Labels),
                CollisionPolicy = collisionPolicy,
                CopyMetaFiles = file.CopyMetaFiles
            };
            return true;
        }

        private static string[] NormalizeLabels(IEnumerable<string> labels)
        {
            var result = new SortedSet<string>(StringComparer.Ordinal);
            if (labels != null)
            {
                foreach (string source in labels)
                {
                    string label = (source ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(label))
                        result.Add(label);
                }
            }

            var values = new string[result.Count];
            result.CopyTo(values);
            return values;
        }

        private static int CopyFbxSources(string sourceRoot, string targetRoot, bool copyMetaFiles)
        {
            Directory.CreateDirectory(targetRoot);
            int count = 0;
            string sourcePrefix = sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string[] sourceFiles = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
            Array.Sort(sourceFiles, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sourceFiles.Length; i++)
            {
                string sourcePath = sourceFiles[i];
                bool isFbx = string.Equals(Path.GetExtension(sourcePath), ".fbx", StringComparison.OrdinalIgnoreCase);
                bool isFbxMeta = copyMetaFiles
                    && sourcePath.EndsWith(".fbx.meta", StringComparison.OrdinalIgnoreCase);
                if (!isFbx && !isFbxMeta)
                    continue;

                string relative = sourcePath.Substring(sourcePrefix.Length);
                string targetPath = Path.Combine(targetRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? targetRoot);
                File.Copy(sourcePath, targetPath, true);
                if (isFbx)
                    count++;
            }

            return count;
        }

        private static void PrepareStagingDirectory(string stagingAssetPath)
        {
            CleanupStagingDirectory(stagingAssetPath);
            Directory.CreateDirectory(AssetPathToFullPath(stagingAssetPath));
        }

        private static void CleanupStagingDirectory(string stagingAssetPath)
        {
            if (string.IsNullOrWhiteSpace(stagingAssetPath)
                || !stagingAssetPath.StartsWith(StagingAssetRoot + "/", StringComparison.Ordinal))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(stagingAssetPath))
            {
                AssetDatabase.DeleteAsset(stagingAssetPath);
            }
            else
            {
                string fullPath = AssetPathToFullPath(stagingAssetPath);
                if (Directory.Exists(fullPath))
                    FileUtil.DeleteFileOrDirectory(fullPath);
                if (File.Exists(fullPath + ".meta"))
                    FileUtil.DeleteFileOrDirectory(fullPath + ".meta");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void WriteStatus(
            AnimationClipExtractionRequest request,
            string status,
            string message,
            MxAnimationClipExtractionReport report,
            string error)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RequestId))
                return;

            var records = new List<AnimationClipExtractionStatusRecord>();
            if (report != null)
            {
                for (int i = 0; i < report.Records.Count; i++)
                {
                    MxAnimationClipExtractionRecord source = report.Records[i];
                    AnimationClip clip = string.IsNullOrWhiteSpace(source.TargetAssetPath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<AnimationClip>(source.TargetAssetPath);
                    records.Add(new AnimationClipExtractionStatusRecord
                    {
                        SourceAssetPath = source.SourceAssetPath ?? string.Empty,
                        SourceClipName = source.SourceClipName ?? string.Empty,
                        TargetAssetPath = source.TargetAssetPath ?? string.Empty,
                        UnityGuid = string.IsNullOrWhiteSpace(source.TargetAssetPath)
                            ? string.Empty
                            : AssetDatabase.AssetPathToGUID(source.TargetAssetPath),
                        ResourceKey = source.ResourceKey ?? string.Empty,
                        ResourceTypeId = source.ResourceTypeId ?? string.Empty,
                        Status = source.Status.ToString(),
                        Reason = source.Reason ?? string.Empty,
                        Error = source.Error ?? string.Empty,
                        FrameRate = clip != null ? clip.frameRate : 0f,
                        DurationSeconds = clip != null ? clip.length : 0f,
                        Looping = clip != null && clip.isLooping
                    });
                }
            }

            var payload = new AnimationClipExtractionStatusFile
            {
                RequestId = request.RequestId,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                Error = error ?? string.Empty,
                SourceRoot = request.SourceRoot,
                OutputRoot = request.OutputRoot,
                ResourceKeyPrefix = request.ResourceKeyPrefix,
                BundleName = request.BundleName,
                Labels = request.Labels,
                SuccessCount = report != null ? report.SuccessCount : 0,
                SkippedCount = report != null ? report.SkippedCount : 0,
                FailedCount = report != null ? report.FailedCount : 0,
                NoClipCount = report != null ? report.NoClipCount : 0,
                ConflictCount = report != null ? report.ConflictCount : 0,
                WarningCount = report != null ? report.WarningCount : 0,
                Records = records.ToArray(),
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            string statusPath = ProjectPath(
                StatusDirectoryRelativePath + "/" + SanitizeRequestId(request.RequestId) + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath) ?? ProjectPath(StatusDirectoryRelativePath));
            File.WriteAllText(statusPath, JsonConvert.SerializeObject(payload, Formatting.Indented));
        }

        private static string ResolveProjectPath(string path)
        {
            string projectRoot = ProjectRoot();
            string fullPath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar)));
            string projectPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path is outside the Unity project: " + path);
            return fullPath;
        }

        private static bool TryConvertToAssetPath(string fullPath, out string assetPath)
        {
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            string assetsPrefix = assetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                assetPath = string.Empty;
                return false;
            }

            assetPath = "Assets/" + fullPath.Substring(assetsPrefix.Length).Replace('\\', '/');
            return true;
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected an Assets-relative path: " + assetPath);
            return Path.GetFullPath(Path.Combine(ProjectRoot(), normalized.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                ProjectRoot(),
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string SanitizeRequestId(string requestId)
        {
            char[] chars = (requestId ?? string.Empty).ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char value = chars[i];
                if (!char.IsLetterOrDigit(value) && value != '-' && value != '_')
                    chars[i] = '_';
            }

            return chars.Length > 0 ? new string(chars) : "unknown";
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private sealed class AnimationClipExtractionRequest
        {
            public string RequestId { get; set; } = string.Empty;
            public string SourceRoot { get; set; } = string.Empty;
            public string OutputRoot { get; set; } = string.Empty;
            public string ResourceKeyPrefix { get; set; } = string.Empty;
            public string BundleName { get; set; } = string.Empty;
            public string[] Labels { get; set; } = Array.Empty<string>();
            public MxAnimationClipCollisionPolicy CollisionPolicy { get; set; }
            public bool CopyMetaFiles { get; set; }
        }

        private sealed class AnimationClipExtractionRequestFile
        {
            [JsonProperty("requestId")]
            public string RequestId { get; set; } = string.Empty;

            [JsonProperty("sourceRoot")]
            public string SourceRoot { get; set; } = string.Empty;

            [JsonProperty("outputRoot")]
            public string OutputRoot { get; set; } = string.Empty;

            [JsonProperty("resourceKeyPrefix")]
            public string ResourceKeyPrefix { get; set; } = string.Empty;

            [JsonProperty("bundleName")]
            public string BundleName { get; set; } = string.Empty;

            [JsonProperty("labels")]
            public string[] Labels { get; set; } = Array.Empty<string>();

            [JsonProperty("collisionPolicy")]
            public string CollisionPolicy { get; set; } = string.Empty;

            [JsonProperty("copyMetaFiles")]
            public bool CopyMetaFiles { get; set; }
        }

        private sealed class AnimationClipExtractionStatusFile
        {
            [JsonProperty("requestId")]
            public string RequestId { get; set; } = string.Empty;

            [JsonProperty("status")]
            public string Status { get; set; } = string.Empty;

            [JsonProperty("message")]
            public string Message { get; set; } = string.Empty;

            [JsonProperty("error")]
            public string Error { get; set; } = string.Empty;

            [JsonProperty("sourceRoot")]
            public string SourceRoot { get; set; } = string.Empty;

            [JsonProperty("outputRoot")]
            public string OutputRoot { get; set; } = string.Empty;

            [JsonProperty("resourceKeyPrefix")]
            public string ResourceKeyPrefix { get; set; } = string.Empty;

            [JsonProperty("bundleName")]
            public string BundleName { get; set; } = string.Empty;

            [JsonProperty("labels")]
            public string[] Labels { get; set; } = Array.Empty<string>();

            [JsonProperty("successCount")]
            public int SuccessCount { get; set; }

            [JsonProperty("skippedCount")]
            public int SkippedCount { get; set; }

            [JsonProperty("failedCount")]
            public int FailedCount { get; set; }

            [JsonProperty("noClipCount")]
            public int NoClipCount { get; set; }

            [JsonProperty("conflictCount")]
            public int ConflictCount { get; set; }

            [JsonProperty("warningCount")]
            public int WarningCount { get; set; }

            [JsonProperty("records")]
            public AnimationClipExtractionStatusRecord[] Records { get; set; } = Array.Empty<AnimationClipExtractionStatusRecord>();

            [JsonProperty("updatedUtc")]
            public string UpdatedUtc { get; set; } = string.Empty;
        }

        private sealed class AnimationClipExtractionStatusRecord
        {
            [JsonProperty("sourceAssetPath")]
            public string SourceAssetPath { get; set; } = string.Empty;

            [JsonProperty("sourceClipName")]
            public string SourceClipName { get; set; } = string.Empty;

            [JsonProperty("targetAssetPath")]
            public string TargetAssetPath { get; set; } = string.Empty;

            [JsonProperty("unityGuid")]
            public string UnityGuid { get; set; } = string.Empty;

            [JsonProperty("resourceKey")]
            public string ResourceKey { get; set; } = string.Empty;

            [JsonProperty("resourceTypeId")]
            public string ResourceTypeId { get; set; } = string.Empty;

            [JsonProperty("status")]
            public string Status { get; set; } = string.Empty;

            [JsonProperty("reason")]
            public string Reason { get; set; } = string.Empty;

            [JsonProperty("error")]
            public string Error { get; set; } = string.Empty;

            [JsonProperty("frameRate")]
            public float FrameRate { get; set; }

            [JsonProperty("durationSeconds")]
            public float DurationSeconds { get; set; }

            [JsonProperty("looping")]
            public bool Looping { get; set; }
        }
    }
}
