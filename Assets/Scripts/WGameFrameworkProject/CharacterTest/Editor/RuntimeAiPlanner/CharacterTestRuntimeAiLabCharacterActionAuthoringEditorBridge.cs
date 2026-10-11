using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterImport;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    [InitializeOnLoad]
    internal static partial class CharacterTestRuntimeAiLabCharacterActionAuthoringEditorBridge
    {
        private static ResolvedRequest s_lastResolved;
        private static CharacterTestRuntimeAiLabCharacterActionAuthoringRequest s_lastRequest;
        private static EditorWindow s_returnWindow;
        private static bool s_applyScheduled;

        static CharacterTestRuntimeAiLabCharacterActionAuthoringEditorBridge()
        {
            CharacterTestRuntimeAiLabCharacterActionAuthoringBridge.Handler = Handle;
            CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.Handler = HandleWorkspace;
            CleanupStaleWorkspacePreviewSources();
        }

        private static CharacterTestRuntimeAiLabCharacterActionAuthoringResult Handle(
            CharacterTestRuntimeAiLabCharacterActionAuthoringRequest request)
        {
            if (request == null || !request.ActionId.IsValid)
                return Failed(request, string.Empty, "角色动作编辑请求缺少有效动作 ID。", false);
            if (request.Operation == CharacterTestRuntimeAiLabCharacterActionAuthoringOperation.RetryApply)
            {
                if (s_lastResolved == null
                    || s_lastRequest == null
                    || s_lastResolved.ActionId != request.ActionId)
                {
                    return Failed(request, string.Empty, "没有可重试的角色动作应用事务。", false);
                }
                s_lastRequest.StatusChanged = request.StatusChanged;
                ScheduleApply(s_lastRequest, s_lastResolved);
                return Publish(
                    request,
                    CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Importing,
                    s_lastResolved.PackagePath,
                    "正在重新导入角色动作正式源。",
                    true);
            }

            ResolvedRequest resolved;
            string error;
            try
            {
                if (!TryResolve(request, out resolved, out error))
                    return Failed(request, string.Empty, error, false);
            }
            catch (Exception ex)
            {
                return Failed(request, string.Empty, "解析角色动作正式源失败：" + ex.Message, false);
            }

            s_returnWindow = EditorWindow.focusedWindow;
            s_lastResolved = resolved;
            s_lastRequest = request;
            var openRequest = new CharacterActionAuthoringOpenRequest
            {
                RequestId = "runtime-ai-lab-" + Guid.NewGuid().ToString("N"),
                Package = resolved.PackagePath,
                ProfileSourceDirectory = resolved.ProfileSourceDirectory,
                ActionSetSourceDirectory = resolved.ActionSetSourceDirectory,
                ActionLibrarySourceDirectory = resolved.ActionLibrarySourceDirectory,
                ProfileId = resolved.ProfileId,
                ActionSetStableId = request.Context.ActionSetStableId,
                ActionLibraryId = resolved.ActionLibraryId,
                ActionId = request.ActionId.Value,
                SourceHash = ComputeSha256(resolved.ActionSourcePath),
                Mode = "focusOnly",
                StatusCallback = status => OnAuthoringWindowStatus(request, resolved, status),
            };

            CharacterActionAuthoringOpenResult open;
            try
            {
                open = CharacterActionAuthoringWindow.OpenForSelection(openRequest);
            }
            catch (Exception ex)
            {
                return Failed(request, resolved.PackagePath, "打开角色动作编辑器失败：" + ex.Message, false);
            }
            CharacterTestRuntimeAiLabCharacterActionAuthoringResult result = open.Status == "opened"
                ? new CharacterTestRuntimeAiLabCharacterActionAuthoringResult(
                    CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Opened,
                    request.ActionId,
                    resolved.PackagePath,
                    "已在角色动作编辑器中打开 " + request.ActionId + "。")
                : new CharacterTestRuntimeAiLabCharacterActionAuthoringResult(
                    CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Failed,
                    request.ActionId,
                    resolved.PackagePath,
                    open.Message);
            request.StatusChanged?.Invoke(result);
            return result;
        }

        private static void OnAuthoringWindowStatus(
            CharacterTestRuntimeAiLabCharacterActionAuthoringRequest request,
            ResolvedRequest resolved,
            CharacterActionAuthoringOpenResult status)
        {
            if (status == null)
                return;
            if (string.Equals(status.Status, "saved", StringComparison.OrdinalIgnoreCase))
            {
                Publish(
                    request,
                    CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Saved,
                    resolved.PackagePath,
                    "角色动作正式源已保存；正在准备 Generated 导入。",
                    true);
                ScheduleApply(request, resolved);
                return;
            }
            if (string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
                request.StatusChanged?.Invoke(Failed(request, resolved.PackagePath, status.Message, false));
        }

        private static void ScheduleApply(
            CharacterTestRuntimeAiLabCharacterActionAuthoringRequest request,
            ResolvedRequest resolved)
        {
            s_lastRequest = request;
            s_lastResolved = resolved;
            if (s_applyScheduled)
                return;
            s_applyScheduled = true;
            EditorApplication.delayCall += RunScheduledApply;
        }

        private static void RunScheduledApply()
        {
            s_applyScheduled = false;
            CharacterTestRuntimeAiLabCharacterActionAuthoringRequest request = s_lastRequest;
            ResolvedRequest resolved = s_lastResolved;
            if (request == null || resolved == null)
                return;
            Publish(
                request,
                CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Importing,
                resolved.PackagePath,
                "正式源已保存，正在生成并导入 CharacterAction 投影。",
                true);
            int exitCode;
            try
            {
                exitCode = CharacterPackageImportCommand.ImportPackage(resolved.PackagePath);
            }
            catch (Exception ex)
            {
                request.StatusChanged?.Invoke(Failed(
                    request,
                    resolved.PackagePath,
                    "正式源已保存，Generated/当前运行目录尚未更新；角色包导入异常：" + ex.Message,
                    true));
                FocusReturnWindow();
                return;
            }
            if (exitCode != 0)
            {
                request.StatusChanged?.Invoke(Failed(
                    request,
                    resolved.PackagePath,
                    "正式源已保存，Generated/当前运行目录尚未更新；角色包导入失败，退出码 "
                    + exitCode + "。",
                    true));
                FocusReturnWindow();
                return;
            }

            Publish(
                request,
                CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Reloading,
                resolved.PackagePath,
                "CharacterAction 投影已导入，正在热重载当前 CharacterTest。",
                true);
            CharacterTestDeveloperDebugPanel[] panels =
                UnityEngine.Object.FindObjectsByType<CharacterTestDeveloperDebugPanel>(
                    FindObjectsInactive.Include);
            CharacterTestCharacterActionReloadResult reload = default;
            bool found = false;
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null && panels[i].TryReloadCharacterActions(out reload))
                {
                    found = true;
                    break;
                }
            }
            if (!found || !reload.Success)
            {
                string message = found
                    ? reload.Message
                    : "当前 Play Mode 中没有已初始化的 CharacterTest 热重载入口。";
                request.StatusChanged?.Invoke(Failed(
                    request,
                    resolved.PackagePath,
                    "正式源和 Generated 已更新，但当前运行目录热重载失败：" + message,
                    true));
                FocusReturnWindow();
                return;
            }

            request.StatusChanged?.Invoke(new CharacterTestRuntimeAiLabCharacterActionAuthoringResult(
                CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Succeeded,
                request.ActionId,
                resolved.PackagePath,
                "角色动作已保存、导入并热重载。",
                sourceSaved: true));
            FocusReturnWindow();
        }

        internal static bool TryResolve(
            CharacterTestRuntimeAiLabCharacterActionAuthoringRequest request,
            out ResolvedRequest resolved,
            out string error)
        {
            resolved = null;
            error = string.Empty;
            if (!request.Context.IsValid)
            {
                error = "当前角色没有有效的 CharacterAction ActionSet 上下文。";
                return false;
            }
            string actionSetId = request.Context.ActionSetStableId;
            int separator = actionSetId.IndexOf('.');
            string packageId = separator > 0 ? actionSetId.Substring(0, separator) : actionSetId;
            string packagePath = FindPackagePath(packageId);
            if (packagePath.Length == 0)
            {
                error = "找不到 ActionSet " + actionSetId + " 对应的正式角色包。";
                return false;
            }

            string profileDirectory = "Assets/MxFrameworkGenerated/CharacterActions/Profiles/" + packageId;
            string actionSetDirectory = "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/" + packageId;
            string profilePath = Path.Combine(profileDirectory, "character_action_profile.json");
            string actionSetPath = Path.Combine(actionSetDirectory, "character_action_sets.json");
            if (!File.Exists(profilePath) || !File.Exists(actionSetPath))
            {
                error = "当前 ActionSet 的 Generated Profile/ActionSet 上下文不完整。";
                return false;
            }

            JObject profileRoot = JObject.Parse(File.ReadAllText(profilePath));
            JObject selectedProfile = null;
            foreach (JObject profile in profileRoot["profiles"] as JArray ?? new JArray())
            {
                if (ContainsString(profile["actionSetIds"] as JArray, actionSetId))
                {
                    selectedProfile = profile;
                    break;
                }
            }
            if (selectedProfile == null)
            {
                error = "Generated Profile 未引用当前 ActionSet：" + actionSetId;
                return false;
            }

            var candidates = new List<(string LibraryId, string Directory, string Path)>();
            JArray libraryIds = selectedProfile["actionLibraryIds"] as JArray ?? new JArray();
            if (ContainsString(libraryIds, "Common"))
            {
                AddCandidate(
                    candidates,
                    "Common",
                    "CharacterActionSource/Libraries/Common",
                    request.ActionId);
            }
            if (ContainsString(libraryIds, packageId))
                AddCandidate(candidates, packageId, packagePath + "/config", request.ActionId);
            if (candidates.Count != 1)
            {
                error = candidates.Count == 0
                    ? "当前 Profile 引用的正式动作库中找不到动作：" + request.ActionId
                    : "动作 ID 在当前 Profile 的多个正式动作库中重复：#" + request.ActionId;
                return false;
            }

            (string libraryId, string directory, string sourcePath) = candidates[0];
            resolved = new ResolvedRequest
            {
                ActionId = request.ActionId,
                PackagePath = packagePath,
                ProfileSourceDirectory = profileDirectory,
                ActionSetSourceDirectory = actionSetDirectory,
                ActionLibraryId = libraryId,
                ActionLibrarySourceDirectory = directory,
                ActionSourcePath = sourcePath,
                ProfileId = (string)selectedProfile["stableId"] ?? string.Empty,
            };
            return true;
        }

        private static void AddCandidate(
            List<(string LibraryId, string Directory, string Path)> candidates,
            string libraryId,
            string directory,
            CharacterActionId actionId)
        {
            string path = Path.Combine(directory, "character_actions.json");
            if (!File.Exists(path))
                return;
            JObject root = JObject.Parse(File.ReadAllText(path));
            int matches = 0;
            foreach (JObject action in root["actions"] as JArray ?? new JArray())
            {
                if ((int?)action["actionId"] == actionId.Value)
                    matches++;
            }
            if (matches == 1)
                candidates.Add((libraryId, directory.Replace('\\', '/'), path.Replace('\\', '/')));
            else if (matches > 1)
            {
                candidates.Add((libraryId, directory.Replace('\\', '/'), path.Replace('\\', '/')));
                candidates.Add((libraryId, directory.Replace('\\', '/'), path.Replace('\\', '/')));
            }
        }

        private static string FindPackagePath(string packageId)
        {
            string[] roots =
            {
                "Authoring/CharacterPackages",
                "Tools/MxFramework.Authoring/samples",
            };
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (!Directory.Exists(roots[rootIndex]))
                    continue;
                string[] manifests = Directory.GetFiles(
                    roots[rootIndex],
                    "manifest.json",
                    SearchOption.AllDirectories);
                Array.Sort(manifests, StringComparer.Ordinal);
                for (int i = 0; i < manifests.Length; i++)
                {
                    JObject manifest = JObject.Parse(File.ReadAllText(manifests[i]));
                    if (string.Equals((string)manifest["packageId"], packageId, StringComparison.Ordinal))
                        return (Path.GetDirectoryName(manifests[i]) ?? string.Empty).Replace('\\', '/');
                }
            }
            return string.Empty;
        }

        private static bool ContainsString(JArray values, string expected)
        {
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (string.Equals((string)values[i], expected, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(stream);
            return "sha256:" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static CharacterTestRuntimeAiLabCharacterActionAuthoringResult Publish(
            CharacterTestRuntimeAiLabCharacterActionAuthoringRequest request,
            CharacterTestRuntimeAiLabCharacterActionAuthoringStatus status,
            string packagePath,
            string message,
            bool sourceSaved)
        {
            var result = new CharacterTestRuntimeAiLabCharacterActionAuthoringResult(
                status,
                request?.ActionId ?? CharacterActionId.None,
                packagePath,
                message,
                sourceSaved);
            request?.StatusChanged?.Invoke(result);
            return result;
        }

        private static CharacterTestRuntimeAiLabCharacterActionAuthoringResult Failed(
            CharacterTestRuntimeAiLabCharacterActionAuthoringRequest request,
            string packagePath,
            string message,
            bool sourceSaved)
        {
            return new CharacterTestRuntimeAiLabCharacterActionAuthoringResult(
                CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Failed,
                request?.ActionId ?? CharacterActionId.None,
                packagePath,
                message,
                sourceSaved);
        }

        private static void FocusReturnWindow()
        {
            if (s_returnWindow == null)
                return;
            s_returnWindow.Show();
            s_returnWindow.Focus();
        }

        internal sealed class ResolvedRequest
        {
            internal CharacterActionId ActionId;
            internal string PackagePath = string.Empty;
            internal string ProfileSourceDirectory = string.Empty;
            internal string ActionSetSourceDirectory = string.Empty;
            internal string ActionLibrarySourceDirectory = string.Empty;
            internal string ActionSourcePath = string.Empty;
            internal string ProfileId = string.Empty;
            internal string ActionLibraryId = string.Empty;
        }
    }
}
