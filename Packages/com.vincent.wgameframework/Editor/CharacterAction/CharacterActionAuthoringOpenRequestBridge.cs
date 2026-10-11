using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    [InitializeOnLoad]
    internal static class CharacterActionAuthoringOpenRequestBridge
    {
        private const string RequestRelativePath = "Temp/MxFrameworkAuthoring/CharacterAction/open_request.json";
        private const string StatusDirectoryRelativePath = "Temp/MxFrameworkAuthoring/CharacterAction/status";
        private const double PollIntervalSeconds = 1.0d;

        private static double s_nextPollTime;
        private static string s_lastConsumedRequestId = string.Empty;

        static CharacterActionAuthoringOpenRequestBridge()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.delayCall += ConsumePendingRequest;
        }

        internal static bool TryReadRequestJson(
            string json,
            out CharacterActionAuthoringOpenRequest request,
            out string error)
        {
            request = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "open request json is empty.";
                return false;
            }

            CharacterActionOpenRequestFile file;
            try
            {
                file = JsonConvert.DeserializeObject<CharacterActionOpenRequestFile>(json);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (file == null)
            {
                error = "open request json is empty.";
                return false;
            }

            if (file.AbilityId <= 0 && file.ActionId <= 0)
            {
                error = "abilityId or actionId is required.";
                return false;
            }

            request = new CharacterActionAuthoringOpenRequest
            {
                RequestId = file.RequestId ?? string.Empty,
                Package = file.Package ?? string.Empty,
                AbilityId = file.AbilityId,
                ProfileSourceDirectory = file.ProfileSourceDirectory ?? string.Empty,
                ActionSetSourceDirectory = file.ActionSetSourceDirectory ?? string.Empty,
                ActionLibrarySourceDirectory = file.ActionLibrarySourceDirectory ?? string.Empty,
                ProfileId = file.ProfileId ?? string.Empty,
                ActionSetStableId = file.ActionSetStableId ?? string.Empty,
                ActionLibraryId = file.ActionLibraryId ?? string.Empty,
                ActionId = file.ActionId,
                SourceHash = file.SourceHash ?? string.Empty,
                Mode = string.IsNullOrWhiteSpace(file.Mode) ? "focusOnly" : file.Mode,
            };
            return true;
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

            if (!TryReadRequestJson(json, out CharacterActionAuthoringOpenRequest request, out string error))
            {
                Debug.LogWarning("Character Action open request ignored: " + error);
                return;
            }

            if (string.Equals(s_lastConsumedRequestId, request.RequestId, StringComparison.Ordinal))
            {
                TryDeleteRequestFile(requestPath);
                return;
            }

            s_lastConsumedRequestId = request.RequestId;
            TryDeleteRequestFile(requestPath);

            CharacterActionAuthoringOpenResult result;
            try
            {
                result = CharacterActionAuthoringWindow.OpenForSelection(request);
            }
            catch (Exception ex)
            {
                result = new CharacterActionAuthoringOpenResult
                {
                    RequestId = request.RequestId,
                    AbilityId = request.AbilityId,
                    Status = "failed",
                    Message = ex.Message,
                };
            }

            WriteStatus(result);
        }

        internal static void WriteStatus(CharacterActionAuthoringOpenResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.RequestId))
                return;

            string statusPath = ProjectPath(
                StatusDirectoryRelativePath + "/" + SanitizeRequestId(result.RequestId) + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath)!);
            var status = new CharacterActionOpenStatusFile
            {
                RequestId = result.RequestId,
                Status = string.IsNullOrWhiteSpace(result.Status) ? "opened" : result.Status,
                Message = result.Message ?? string.Empty,
                AbilityId = result.AbilityId,
                ActionId = result.ActionId,
                UpdatedUtc = DateTimeOffset.UtcNow.ToString("O"),
            };
            File.WriteAllText(statusPath, JsonConvert.SerializeObject(status, Formatting.Indented));
        }

        private static void TryDeleteRequestFile(string requestPath)
        {
            try
            {
                File.Delete(requestPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string ProjectPath(string relative)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string SanitizeRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return "unknown";

            char[] chars = requestId.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                if (!char.IsLetterOrDigit(ch) && ch != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private sealed class CharacterActionOpenRequestFile
        {
            [JsonProperty("requestId")]
            public string RequestId { get; set; } = string.Empty;

            [JsonProperty("package")]
            public string Package { get; set; } = string.Empty;

            [JsonProperty("abilityId")]
            public int AbilityId { get; set; }

            [JsonProperty("profileSourceDirectory")]
            public string ProfileSourceDirectory { get; set; } = string.Empty;

            [JsonProperty("actionSetSourceDirectory")]
            public string ActionSetSourceDirectory { get; set; } = string.Empty;

            [JsonProperty("actionLibrarySourceDirectory")]
            public string ActionLibrarySourceDirectory { get; set; } = string.Empty;

            [JsonProperty("actionSetStableId")]
            public string ActionSetStableId { get; set; } = string.Empty;

            [JsonProperty("profileId")]
            public string ProfileId { get; set; } = string.Empty;

            [JsonProperty("actionLibraryId")]
            public string ActionLibraryId { get; set; } = string.Empty;

            [JsonProperty("actionId")]
            public int ActionId { get; set; }

            [JsonProperty("sourceHash")]
            public string SourceHash { get; set; } = string.Empty;

            [JsonProperty("mode")]
            public string Mode { get; set; } = "focusOnly";
        }

        private sealed class CharacterActionOpenStatusFile
        {
            [JsonProperty("requestId")]
            public string RequestId { get; set; } = string.Empty;

            [JsonProperty("status")]
            public string Status { get; set; } = "opened";

            [JsonProperty("message")]
            public string Message { get; set; } = string.Empty;

            [JsonProperty("abilityId")]
            public int AbilityId { get; set; }

            [JsonProperty("actionId")]
            public int ActionId { get; set; }

            [JsonProperty("updatedUtc")]
            public string UpdatedUtc { get; set; } = string.Empty;
        }
    }
}
