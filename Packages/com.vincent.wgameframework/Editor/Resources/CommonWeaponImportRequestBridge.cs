using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    [InitializeOnLoad]
    internal static class CommonWeaponImportRequestBridge
    {
        private const string RequestRelativePath = "Temp/MxFrameworkAuthoring/CommonWeapons/rebuild_request.json";
        private const string StatusDirectoryRelativePath = "Temp/MxFrameworkAuthoring/CommonWeapons/status";
        private const double PollIntervalSeconds = 0.5d;

        private static double s_nextPollTime;
        private static string s_lastConsumedRequestId = string.Empty;

        static CommonWeaponImportRequestBridge()
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

        public static void ConsumePendingRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

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

            if (!TryReadRequestJson(json, out WeaponRebuildRequest request, out string error))
            {
                Debug.LogWarning("MxFramework Weapon Import: rebuild request ignored: " + error);
                return;
            }

            if (string.Equals(s_lastConsumedRequestId, request.RequestId, StringComparison.Ordinal))
            {
                TryDeleteFile(requestPath);
                return;
            }

            s_lastConsumedRequestId = request.RequestId;
            TryDeleteFile(requestPath);
            WriteStatus(request, "running", "Unity Editor is rebuilding public weapon prefabs.");

            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                CommonWeaponWrapperPrefabBuildReport report =
                    CommonWeaponWrapperPrefabBuilder.SynchronizeFromGlobalProfile(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);

                AssetDatabase.SaveAssets();

                if (report.Errors.Count > 0)
                {
                    string errMessage = string.Join(" | ", report.Errors);
                    WriteStatus(request, "failed", errMessage, report);
                    Debug.LogError("MxFramework Weapon Import: rebuild failed: " + errMessage);
                }
                else
                {
                    string okMessage = $"Synchronized weapon prefabs. Built: {report.Built}, Total: {report.Total}, Materials: {report.MaterialBindings}.";
                    WriteStatus(request, "completed", okMessage, report);
                    Debug.Log("MxFramework Weapon Import: " + okMessage);
                }
            }
            catch (Exception ex)
            {
                WriteStatus(request, "failed", ex.Message);
                Debug.LogError("MxFramework Weapon Import: rebuild request exception: " + ex);
            }
        }

        public static void SynchronizeAllFromCommandLine()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CommonWeaponWrapperPrefabBuildReport report =
                CommonWeaponWrapperPrefabBuilder.SynchronizeFromGlobalProfile(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
            AssetDatabase.SaveAssets();

            if (report.Errors.Count > 0)
            {
                foreach (var err in report.Errors)
                    Debug.LogError("[CommandLine] " + err);
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log($"[CommandLine] Common weapon wrapper prefabs synchronized: {report.Built} built.");
                EditorApplication.Exit(0);
            }
        }

        private static bool TryReadRequestJson(string json, out WeaponRebuildRequest request, out string error)
        {
            request = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "empty payload";
                return false;
            }

            try
            {
                request = JsonConvert.DeserializeObject<WeaponRebuildRequest>(json);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.RequestId))
            {
                error = "missing requestId";
                return false;
            }

            return true;
        }

        private static void WriteStatus(WeaponRebuildRequest request, string status, string message, CommonWeaponWrapperPrefabBuildReport report = null)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RequestId))
                return;

            string statusDirectory = ProjectPath(StatusDirectoryRelativePath);
            Directory.CreateDirectory(statusDirectory);

            var statusObject = new WeaponRebuildStatus
            {
                RequestId = request.RequestId,
                WeaponId = request.WeaponId ?? string.Empty,
                Status = status,
                Message = message,
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                Built = report?.Built ?? 0,
                Total = report?.Total ?? 0,
                MaterialBindings = report?.MaterialBindings ?? 0
            };

            string statusFilePath = Path.Combine(statusDirectory, SanitizeFileName(request.RequestId) + ".json");
            File.WriteAllText(statusFilePath, JsonConvert.SerializeObject(statusObject, Formatting.Indented));
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static string ProjectPath(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        [Serializable]
        private sealed class WeaponRebuildRequest
        {
            [JsonProperty("requestId")]
            public string RequestId { get; set; } = string.Empty;

            [JsonProperty("weaponId")]
            public string WeaponId { get; set; } = string.Empty;

            [JsonProperty("createdUtc")]
            public string CreatedUtc { get; set; } = string.Empty;
        }

        [Serializable]
        private sealed class WeaponRebuildStatus
        {
            [JsonProperty("requestId")]
            public string RequestId { get; set; } = string.Empty;

            [JsonProperty("weaponId")]
            public string WeaponId { get; set; } = string.Empty;

            [JsonProperty("status")]
            public string Status { get; set; } = string.Empty;

            [JsonProperty("message")]
            public string Message { get; set; } = string.Empty;

            [JsonProperty("built")]
            public int Built { get; set; }

            [JsonProperty("total")]
            public int Total { get; set; }

            [JsonProperty("materialBindings")]
            public int MaterialBindings { get; set; }

            [JsonProperty("updatedUtc")]
            public string UpdatedUtc { get; set; } = string.Empty;
        }
    }
}
