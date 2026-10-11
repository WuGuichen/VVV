using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;

namespace MxFramework.Editor.CharacterAction
{
    public sealed class CharacterActionAuthoringOpenRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public string Package { get; set; } = string.Empty;
        public int AbilityId { get; set; }
        public string ProfileSourceDirectory { get; set; } = string.Empty;
        public string ActionSetSourceDirectory { get; set; } = string.Empty;
        public string ActionLibrarySourceDirectory { get; set; } = string.Empty;
        public string ProfileId { get; set; } = string.Empty;
        public string ActionSetStableId { get; set; } = string.Empty;
        public string ActionLibraryId { get; set; } = string.Empty;
        public int ActionId { get; set; }
        public string SourceHash { get; set; } = string.Empty;
        public string Mode { get; set; } = "focusOnly";
        [JsonIgnore]
        public Action<CharacterActionAuthoringOpenResult> StatusCallback { get; set; }
    }

    public sealed class CharacterActionAuthoringOpenResult
    {
        public string RequestId { get; set; } = string.Empty;
        public string Status { get; set; } = "opened";
        public string Message { get; set; } = string.Empty;
        public int AbilityId { get; set; }
        public int ActionId { get; set; }
    }

    public sealed partial class CharacterActionAuthoringWindow
    {
        private CharacterActionAuthoringOpenResult ApplyAbilityBindingOpenRequest(CharacterActionAuthoringOpenRequest request)
        {
            request ??= new CharacterActionAuthoringOpenRequest();
            _openRequestReadOnly = false;
            _openRequestSourceHash = string.Empty;
            _openRequestId = string.Empty;
            _openRequestAbilityId = 0;
            _openRequestActionId = 0;
            _openRequestStatusCallback = request?.StatusCallback;
            if (request.AbilityId <= 0 && request.ActionId <= 0)
                return BuildOpenResult(request, "failed", "abilityId 或 actionId 至少需要一个。");

            RestoreSessionState();
            _editorMode = AuthoringEditorMode.CharacterAction;
            PrepareAbilityBindingWorkspace();
            NormalizeOpenRequestSourceDirectories(request);
            if (!ValidateRequestedSourceHash(request, out string sourceHashError))
                return BuildOpenResult(request, "failed", sourceHashError);
            bool switchesSourceGraph = WouldSwitchRequestedSources(request);
            if (switchesSourceGraph && IsAnyDirty())
            {
                PrepareAbilityBindingWorkspace();
                SetFocusedAbilityBinding(
                    request.AbilityId,
                    "当前动作配置有未保存修改，已打开窗口但不会自动切换 source；请先保存或放弃修改后再从 CharacterStudio 打开。\n"
                    + CreateDirtyStatusText()
                    + DescribeDirtyDocumentPaths(),
                    conflict: true);
                RestoreWindowContent(forceRebuildShell: true);
                Focus();
                Repaint();
                return BuildOpenResult(request, "failed", _focusedAbilityOpenMessage);
            }

            // Refresh only the choices here. Applying the requested directories before the
            // dirty check would make WouldSwitchRequestedSources compare the request to
            // itself and could silently replace an unsaved document session.
            RefreshSourceCatalogs();
            if (!TryLoadRequestedSources(request, switchesSourceGraph, out string loadError))
            {
                PrepareAbilityBindingWorkspace();
                SetFocusedAbilityBinding(request.AbilityId, loadError, conflict: true);
                RestoreWindowContent(forceRebuildShell: true);
                Focus();
                Repaint();
                return BuildOpenResult(request, "failed", loadError);
            }

            PrepareAbilityBindingWorkspace();
            _openRequestReadOnly = string.Equals(request.Mode, "readOnly", StringComparison.OrdinalIgnoreCase);
            _openRequestSourceHash = request.SourceHash ?? string.Empty;
            _openRequestId = request.RequestId ?? string.Empty;
            _openRequestAbilityId = request.AbilityId;
            _openRequestActionId = request.ActionId;
            SetFocusedAbilityBinding(
                request.AbilityId,
                "来自 CharacterStudio 的定位请求；这里只高亮 abilityId，不自动创建或保存 abilityBindings。",
                conflict: false);
            RestoreWindowContent(forceRebuildShell: true);
            Focus();
            Repaint();
            string selection = request.ActionId > 0
                ? "动作 " + request.ActionId
                : "abilityId " + request.AbilityId;
            string access = _openRequestReadOnly ? "（只读）" : "（可编辑正式源）";
            return BuildOpenResult(request, "opened", "已打开角色动作配置，并精确定位到 " + selection + access + "。");
        }

        private string DescribeDirtyDocumentPaths()
        {
            if (_documentSession == null)
                return string.Empty;

            var paths = new System.Collections.Generic.List<string>();
            for (int i = 0; i < _documentSession.DocumentStates.Count; i++)
            {
                CharacterActionAuthoringDocumentState state = _documentSession.DocumentStates[i];
                if (state != null && state.IsDirty)
                    paths.Add(state.Key.SourcePath + " revision=" + state.Revision);
            }

            return paths.Count == 0 ? string.Empty : "\n" + string.Join("\n", paths);
        }

        private void PrepareAbilityBindingWorkspace()
        {
            _workspace = AuthoringWorkspace.Mappings;
            _mappingSection = MappingWorkspaceSection.AbilityBindings;
            _tab = AuthoringTab.ActionSet;
        }

        private static void NormalizeOpenRequestSourceDirectories(CharacterActionAuthoringOpenRequest request)
        {
            request.ProfileSourceDirectory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(
                request.ProfileSourceDirectory);
            request.ActionSetSourceDirectory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(
                request.ActionSetSourceDirectory);
            request.ActionLibrarySourceDirectory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(
                request.ActionLibrarySourceDirectory);
        }

        private bool WouldSwitchRequestedSources(CharacterActionAuthoringOpenRequest request)
        {
            if (request == null)
                return false;

            string currentProfileDirectory = _documentSession?.ProfileDocument.Source.DirectoryPath
                ?? _profileSourceDirectory;
            if (WouldSwitchSource(request.ProfileSourceDirectory, currentProfileDirectory))
                return true;

            if (_documentSession != null)
            {
                return !DocumentSessionContainsActionSetSource(request.ActionSetSourceDirectory)
                    || !DocumentSessionContainsActionLibrarySource(request.ActionLibrarySourceDirectory);
            }

            return WouldSwitchSource(request.ActionSetSourceDirectory, _actionSetSourceDirectory)
                || WouldSwitchSource(request.ActionLibrarySourceDirectory, _actionLibrarySourceDirectory);
        }

        private bool DocumentSessionContainsActionSetSource(string directory)
        {
            directory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(directory);
            if (string.IsNullOrWhiteSpace(directory))
                return true;
            for (int i = 0; i < _documentSession.ActionSetBundles.Count; i++)
            {
                if (string.Equals(
                        _documentSession.ActionSetBundles[i].Source.DirectoryPath,
                        directory,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private bool DocumentSessionContainsActionLibrarySource(string directory)
        {
            directory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(directory);
            if (string.IsNullOrWhiteSpace(directory))
                return true;
            for (int i = 0; i < _documentSession.ActionLibraries.Count; i++)
            {
                if (string.Equals(
                        _documentSession.ActionLibraries[i].Source.DirectoryPath,
                        directory,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool WouldSwitchSource(string requested, string current)
        {
            requested = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(requested);
            current = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(current);
            return !string.IsNullOrWhiteSpace(requested)
                && !string.Equals(requested, current, StringComparison.Ordinal);
        }

        private bool TryLoadRequestedSources(
            CharacterActionAuthoringOpenRequest request,
            bool switchesSourceGraph,
            out string error)
        {
            error = string.Empty;
            if (!switchesSourceGraph && _documentSession != null)
            {
                if (!TryApplyRequestedDocumentSelection(_documentSession, request, out error))
                    return false;
                SyncModelFromDocumentSession();
                return true;
            }

            int profileIndex = CharacterActionAuthoringSourceCatalog.FindIndexByDirectory(
                _profileSources,
                request.ProfileSourceDirectory);
            if (profileIndex < 0)
            {
                error = "Profile source not found: " + request.ProfileSourceDirectory;
                return false;
            }
            if (CharacterActionAuthoringSourceCatalog.FindIndexByDirectory(
                    _actionSetSources,
                    request.ActionSetSourceDirectory) < 0)
            {
                error = "ActionSet source not found: " + request.ActionSetSourceDirectory;
                return false;
            }
            if (CharacterActionAuthoringSourceCatalog.FindIndexByDirectory(
                    _actionLibrarySources,
                    request.ActionLibrarySourceDirectory) < 0)
            {
                error = "Action Library source not found: " + request.ActionLibrarySourceDirectory;
                return false;
            }

            CharacterActionAuthoringSourceEntry profileSource = _profileSources[profileIndex];
            if (!TryStageProfileSource(
                    profileSource,
                    out CharacterActionAuthoringDocumentSession staged,
                    out error))
            {
                return false;
            }
            if (!TryApplyRequestedDocumentSelection(staged, request, out error))
                return false;

            AdoptDocumentSession(staged);
            return true;
        }

        private static bool TryApplyRequestedDocumentSelection(
            CharacterActionAuthoringDocumentSession session,
            CharacterActionAuthoringOpenRequest request,
            out string error)
        {
            error = string.Empty;
            if (session == null)
            {
                error = "Character Action document session is unavailable.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.ProfileId) && !session.SelectProfile(request.ProfileId))
            {
                error = "Profile selection failed: " + request.ProfileId;
                return false;
            }

            string actionSetId = request.ActionSetStableId ?? string.Empty;
            CharacterActionAuthoringActionSetBundleDocument requestedBundle = null;
            string actionSetDirectory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(
                request.ActionSetSourceDirectory);
            if (!string.IsNullOrWhiteSpace(actionSetId))
            {
                requestedBundle = session.FindActionSetBundle(actionSetId);
                if (requestedBundle == null
                    || !ContainsId(session.CurrentProfile?.ActionSetIds, actionSetId)
                    || (!string.IsNullOrWhiteSpace(actionSetDirectory)
                        && !string.Equals(
                            requestedBundle.Source.DirectoryPath,
                            actionSetDirectory,
                            StringComparison.Ordinal)))
                {
                    error = "ActionSet is not part of the requested Profile source: " + actionSetId;
                    return false;
                }
            }
            else if (!string.IsNullOrWhiteSpace(actionSetDirectory))
            {
                string[] ids = session.CurrentProfile?.ActionSetIds ?? Array.Empty<string>();
                for (int i = 0; i < ids.Length; i++)
                {
                    CharacterActionAuthoringActionSetBundleDocument bundle = session.FindActionSetBundle(ids[i]);
                    if (bundle != null
                        && string.Equals(bundle.Source.DirectoryPath, actionSetDirectory, StringComparison.Ordinal))
                    {
                        actionSetId = ids[i];
                        requestedBundle = bundle;
                        break;
                    }
                }
                if (requestedBundle == null)
                {
                    error = "ActionSet source is not referenced by the requested Profile: " + actionSetDirectory;
                    return false;
                }
            }

            string libraryId = request.ActionLibraryId ?? string.Empty;
            string libraryDirectory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(
                request.ActionLibrarySourceDirectory);
            if (!string.IsNullOrWhiteSpace(libraryDirectory))
            {
                for (int i = 0; i < session.ActionLibraries.Count; i++)
                {
                    CharacterActionAuthoringActionLibraryDocument library = session.ActionLibraries[i];
                    if (string.Equals(library.Source.DirectoryPath, libraryDirectory, StringComparison.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(libraryId))
                            libraryId = library.LibraryId;
                        else if (!string.Equals(libraryId, library.LibraryId, StringComparison.Ordinal))
                            continue;
                        break;
                    }
                }
                if (string.IsNullOrWhiteSpace(libraryId)
                    || !ContainsId(session.CurrentProfile?.ActionLibraryIds, libraryId))
                {
                    error = "Action Library source is not referenced by the requested Profile: "
                        + libraryDirectory;
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(actionSetId) && !session.SelectActionSet(actionSetId))
            {
                error = "ActionSet selection failed: " + actionSetId;
                return false;
            }
            if (!string.IsNullOrWhiteSpace(libraryId) && !session.SelectActionLibrary(libraryId))
            {
                error = "Action Library selection failed: " + libraryId;
                return false;
            }
            if (request.ActionId > 0 && !session.SelectAction(request.ActionId))
            {
                error = "Action selection failed or the action is not in the requested Profile libraries: "
                    + request.ActionId;
                return false;
            }
            if (request.ActionId > 0
                && !string.IsNullOrWhiteSpace(libraryId)
                && !string.Equals(session.Selection.ActionLibraryId, libraryId, StringComparison.Ordinal))
            {
                error = "Action is not part of the requested Action Library: " + request.ActionId;
                return false;
            }

            return true;
        }

        private void SetFocusedAbilityBinding(int abilityId, string message, bool conflict)
        {
            _focusedAbilityId = abilityId;
            _focusedAbilityOpenMessage = message ?? string.Empty;
            _focusedAbilityOpenConflict = conflict;
            _focusedAbilityBindingElement = null;
        }

        private static CharacterActionAuthoringOpenResult BuildOpenResult(
            CharacterActionAuthoringOpenRequest request,
            string status,
            string message)
        {
            return new CharacterActionAuthoringOpenResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                AbilityId = request?.AbilityId ?? 0,
                ActionId = request?.ActionId ?? 0,
                Status = status ?? "opened",
                Message = message ?? string.Empty,
            };
        }

        private static bool ValidateRequestedSourceHash(
            CharacterActionAuthoringOpenRequest request,
            out string error)
        {
            error = string.Empty;
            if (request == null || string.IsNullOrWhiteSpace(request.SourceHash))
                return true;
            string path = CharacterActionAuthoringSourceEntry.CombineAssetPath(
                request.ActionLibrarySourceDirectory,
                global::MxFramework.CharacterAction.CharacterActionJsonImporter.DefaultActionsSourcePath);
            if (!File.Exists(path))
            {
                error = "Action Library source not found for source hash validation: " + path;
                return false;
            }
            string currentHash = ComputeFileSha256(path);
            if (string.Equals(currentHash, request.SourceHash, StringComparison.OrdinalIgnoreCase))
                return true;
            error = "Action Library source changed on disk. CharacterStudio hash=" + request.SourceHash
                + ", Unity hash=" + currentHash + ". Refresh CharacterStudio before opening.";
            return false;
        }

        private static string ComputeFileSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                return "sha256:" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private bool ValidateOpenRequestSourceHashBeforeSave(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(_openRequestId)
                || string.IsNullOrWhiteSpace(_openRequestSourceHash)
                || string.IsNullOrWhiteSpace(_actionLibrarySourceDirectory))
                return true;

            string path = CharacterActionAuthoringSourceEntry.CombineAssetPath(
                _actionLibrarySourceDirectory,
                global::MxFramework.CharacterAction.CharacterActionJsonImporter.DefaultActionsSourcePath);
            if (!File.Exists(path))
            {
                error = "Action Library source no longer exists: " + path;
                return false;
            }

            string currentHash = ComputeFileSha256(path);
            if (string.Equals(currentHash, _openRequestSourceHash, StringComparison.OrdinalIgnoreCase))
                return true;
            error = "Action Library source changed on disk after CharacterStudio opened it. Expected "
                + _openRequestSourceHash + ", current " + currentHash
                + ". Reload the action before saving; no file was overwritten.";
            return false;
        }

        private void ReportOpenRequestStatus(string status, string message)
        {
            if (string.IsNullOrWhiteSpace(_openRequestId))
                return;
            CharacterActionAuthoringOpenRequestBridge.WriteStatus(
                new CharacterActionAuthoringOpenResult
                {
                    RequestId = _openRequestId,
                    AbilityId = _openRequestAbilityId,
                    ActionId = _openRequestActionId,
                    Status = status ?? "opened",
                    Message = message ?? string.Empty,
                });
            try
            {
                _openRequestStatusCallback?.Invoke(new CharacterActionAuthoringOpenResult
                {
                    RequestId = _openRequestId,
                    AbilityId = _openRequestAbilityId,
                    ActionId = _openRequestActionId,
                    Status = status ?? "opened",
                    Message = message ?? string.Empty,
                });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }

        private void AcceptOpenRequestSave()
        {
            string actionSourcePath = CharacterActionAuthoringSourceEntry.CombineAssetPath(
                _actionLibrarySourceDirectory,
                global::MxFramework.CharacterAction.CharacterActionJsonImporter.DefaultActionsSourcePath);
            if (File.Exists(actionSourcePath))
                _openRequestSourceHash = ComputeFileSha256(actionSourcePath);
            ReportOpenRequestStatus(
                "saved",
                _openRequestActionId <= 0
                    ? "Character Action sources were saved successfully."
                    : "Action " + _openRequestActionId + " was saved to its formal source.");
        }
    }
}
