#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabDraftPersistence
    {
        internal Func<string, bool> AuthoringWorkspaceWriteFailureForTests { get; set; }

        internal string LatestWorkspacePointerPath =>
            Path.GetFullPath(Path.Combine(_draftsRoot, "latest-workspace.txt"));
        internal bool HasLatestWorkspacePointer => File.Exists(LatestWorkspacePointerPath);

        internal void DeactivateWorkspace(string status = null)
        {
            _workspace = null;
            _saveDueAt = double.MaxValue;
            LastStatus = status ?? "Runtime AI Lab authoring workspace deactivated.";
        }

        internal bool TryReadLatestWorkspaceId(out string workspaceId, out string message)
        {
            workspaceId = string.Empty;
            try
            {
                string value = File.ReadAllText(LatestWorkspacePointerPath).Trim();
                if (!Guid.TryParse(value, out Guid parsed))
                {
                    message = "The legacy Runtime AI Lab workspace pointer is invalid.";
                    return false;
                }
                workspaceId = parsed.ToString("D", CultureInfo.InvariantCulture);
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                message = "The legacy Runtime AI Lab workspace pointer could not be read: "
                    + exception.Message;
                return false;
            }
        }

        internal void ActivateLoadedWorkspace(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            string status)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            LastStatus = status ?? string.Empty;
            _saveDueAt = double.MaxValue;
            if (_workspace.Dirty)
                MarkDirty();
        }

        internal void SetLatestWorkspacePointerUpdateEnabled(bool enabled)
        {
            _updateLatestWorkspacePointerOnFlush = enabled;
        }

        internal bool TryWriteLatestWorkspacePointer(string workspaceId, out string message)
        {
            message = string.Empty;
            if (!Guid.TryParse(workspaceId, out Guid parsed))
            {
                message = "The legacy latest pointer target is not a workspace GUID.";
                return false;
            }
            if (!TryLoad(workspaceId, out _, out string loadMessage))
            {
                message = "The legacy latest pointer target is unavailable. " + loadMessage;
                return false;
            }

            string normalized = parsed.ToString("D", CultureInfo.InvariantCulture);
            if (AuthoringWorkspaceWriteFailureForTests?.Invoke(LatestWorkspacePointerPath) == true)
            {
                message = "Injected legacy latest pointer write failure.";
                return false;
            }
            string temporary = LatestWorkspacePointerPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(_draftsRoot);
                File.WriteAllText(temporary, normalized, new UTF8Encoding(false));
                if (File.Exists(LatestWorkspacePointerPath))
                    File.Replace(temporary, LatestWorkspacePointerPath, null);
                else
                    File.Move(temporary, LatestWorkspacePointerPath);
                message = "Legacy Runtime AI Lab latest pointer updated.";
                return true;
            }
            catch (Exception exception)
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
                message = "Legacy Runtime AI Lab latest pointer update failed: " + exception.Message;
                return false;
            }
        }

        internal bool TryDeleteUnreferencedWorkspace(
            string workspaceId,
            IReadOnlyCollection<string> referencedWorkspaceIds,
            out string message)
        {
            if (!Guid.TryParse(workspaceId, out Guid parsed))
            {
                message = "Only a workspace GUID can be cleaned up.";
                return false;
            }

            string normalized = parsed.ToString("D", CultureInfo.InvariantCulture);
            if (_workspace != null
                && string.Equals(_workspace.WorkspaceId, normalized, StringComparison.OrdinalIgnoreCase))
            {
                message = "The active workspace cannot be cleaned up.";
                return false;
            }
            foreach (string referenced in referencedWorkspaceIds ?? Array.Empty<string>())
            {
                if (Guid.TryParse(referenced, out Guid referencedGuid)
                    && referencedGuid == parsed)
                {
                    message = "A registered workspace cannot be cleaned up.";
                    return false;
                }
            }

            try
            {
                DeleteExactWorkspaceDirectory(_draftsRoot, normalized);
                if (!string.Equals(
                        Path.GetFullPath(_draftsRoot),
                        Path.GetFullPath(_recoveryRoot),
                        StringComparison.OrdinalIgnoreCase))
                {
                    DeleteExactWorkspaceDirectory(_recoveryRoot, normalized);
                }
                message = "Unreferenced Runtime AI Lab workspace cleaned up.";
                return true;
            }
            catch (Exception exception)
            {
                message = "Unreferenced Runtime AI Lab workspace cleanup failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Clones one persisted workspace by reusing its validated workspace/group/card snapshot.
        /// The source remains untouched and the legacy latest pointer is not changed.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit Main/Experiment copy, reset, or apply only.</remarks>
        internal bool TryCloneWorkspace(
            string sourceWorkspaceId,
            string targetWorkspaceId,
            string displayName,
            int minimumRevision,
            out CharacterTestRuntimeAiLabDraftWorkspace clone,
            out string message)
        {
            clone = null;
            if (!Guid.TryParse(sourceWorkspaceId, out Guid sourceGuid)
                || !Guid.TryParse(targetWorkspaceId, out Guid targetGuid)
                || sourceGuid == targetGuid)
            {
                message = "Workspace clone identities must be distinct GUIDs.";
                return false;
            }
            if (!TryLoad(sourceWorkspaceId, out CharacterTestRuntimeAiLabDraftWorkspace source, out message))
                return false;

            string targetDirectory = GetWorkspaceDirectory(targetWorkspaceId);
            string stagingDirectory = GetStagingDirectory(targetWorkspaceId);
            if (Directory.Exists(targetDirectory))
            {
                message = "The target workspace already exists.";
                return false;
            }

            try
            {
                if (Directory.Exists(stagingDirectory))
                    Directory.Delete(stagingDirectory, recursive: true);
                Directory.CreateDirectory(stagingDirectory);
                string sourceDirectory = source.RecoveredFromPrevious
                    ? GetPreviousDirectory(source.WorkspaceId)
                    : GetWorkspaceDirectory(source.WorkspaceId);
                CopySnapshot(sourceDirectory, stagingDirectory);

                string workspacePath = Path.Combine(stagingDirectory, "workspace.json");
                JObject manifest = JObject.Parse(File.ReadAllText(workspacePath));
                int revision = Math.Max(
                    Math.Max(1, (int?)manifest["currentRevision"] ?? 1),
                    Math.Max(0, minimumRevision));
                manifest["workspaceId"] = targetGuid.ToString("D", CultureInfo.InvariantCulture);
                manifest["displayName"] = string.IsNullOrWhiteSpace(displayName)
                    ? source.DisplayName
                    : displayName.Trim();
                manifest["createdUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                manifest["updatedUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                manifest["currentRevision"] = revision;
                manifest["lastAppliedRevision"] = 0;
                File.WriteAllText(workspacePath, manifest.ToString(), new UTF8Encoding(false));

                if (!TryLoadDirectory(stagingDirectory, out _, out message, markRecovered: false))
                    throw new InvalidDataException(message);
                CommitStaging(stagingDirectory, targetDirectory);
                if (!TryLoad(targetWorkspaceId, out clone, out message))
                    throw new InvalidDataException(message);
                message = "Runtime AI Lab workspace snapshot cloned.";
                return true;
            }
            catch (Exception exception)
            {
                try { if (Directory.Exists(stagingDirectory)) Directory.Delete(stagingDirectory, recursive: true); }
                catch { }
                clone = null;
                message = "Runtime AI Lab workspace clone failed: " + exception.Message;
                return false;
            }
        }

        private static void DeleteExactWorkspaceDirectory(string rootPath, string workspaceId)
        {
            string root = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string target = Path.GetFullPath(Path.Combine(root, workspaceId));
            DirectoryInfo parent = Directory.GetParent(target);
            if (parent == null
                || !string.Equals(parent.FullName, root, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFileName(target), workspaceId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Workspace cleanup target escaped its configured root.");
            }
            if (!Directory.Exists(target))
                return;
            var directory = new DirectoryInfo(target);
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Workspace cleanup does not follow directory links.");
            directory.Delete(recursive: true);
        }
    }
}
#endif
