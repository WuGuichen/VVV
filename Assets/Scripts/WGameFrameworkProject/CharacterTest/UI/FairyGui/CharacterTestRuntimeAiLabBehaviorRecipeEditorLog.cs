using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Low-frequency, append-only diagnostic log for Behavior Recipe authoring commands.
    /// This is intentionally separate from runtime gameplay logging so UI input, command routing,
    /// draft mutation, and projection refresh can be followed after the panel has refreshed.
    /// </summary>
    internal static class CharacterTestRuntimeAiLabBehaviorRecipeEditorLog
    {
        private static readonly object Sync = new object();
        private static readonly string SessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        private static readonly string ResolvedFilePath = ResolveFilePath();
        private static readonly byte[] Utf8Preamble = Encoding.UTF8.GetPreamble();
        private static int _sequence;
        private static bool _headerWritten;
        private static bool _saveRunHeaderWritten;
        private static bool _visibilityHeaderWritten;
        private static bool _workbenchHeaderWritten;

        internal static string FilePath => ResolvedFilePath;

        internal static void WriteSaveRun(
            in CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult operation)
        {
            try
            {
                lock (Sync)
                {
                    string directory = Path.GetDirectoryName(ResolvedFilePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    EnsureWindowsReadableUtf8Log();
                    var builder = new StringBuilder(512);
                    if (!_saveRunHeaderWritten)
                    {
                        builder.AppendLine("# Behavior Recipe save-and-run transactions");
                        builder.AppendLine("# utc\tsession\tseq\tphase\toperationId\tworkspaceRevision\tgroupDraftId\tcardDraftId\tdraftSemanticHash\tcandidateAuthorityHash\tselectedEntityId\truntimeProfileSlot\tinstalledAuthorityHash\tresultStage\tfailureCode\tstatus\tmessage");
                        _saveRunHeaderWritten = true;
                    }
                    builder.Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)).Append('\t')
                        .Append(SessionId).Append('\t')
                        .Append(++_sequence).Append('\t')
                        .Append("save-run").Append('\t')
                        .Append(Clean(operation.OperationId)).Append('\t')
                        .Append(operation.WorkspaceRevision).Append('\t')
                        .Append(Clean(operation.GroupDraftId)).Append('\t')
                        .Append(Clean(operation.CardDraftId)).Append('\t')
                        .Append(operation.DraftSemanticHash).Append('\t')
                        .Append(operation.CandidateAuthorityHash).Append('\t')
                        .Append(operation.SelectedEntityId).Append('\t')
                        .Append(operation.RuntimeProfileSlot).Append('\t')
                        .Append(operation.InstalledAuthorityHash).Append('\t')
                        .Append(Clean(operation.Stage)).Append('\t')
                        .Append(Clean(operation.FailureCode)).Append('\t')
                        .Append(operation.Status).Append('\t')
                        .Append(Clean(operation.Message))
                        .AppendLine();
                    File.AppendAllText(ResolvedFilePath, builder.ToString(), new UTF8Encoding(false));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Behavior Recipe save-run log write failed: " + exception.Message);
            }
        }

        internal static void Write(
            string phase,
            string action,
            string elementId,
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity identity,
            string result,
            string message,
            int beforeCount = -1,
            int afterCount = -1)
        {
            try
            {
                lock (Sync)
                {
                    string directory = Path.GetDirectoryName(ResolvedFilePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    EnsureWindowsReadableUtf8Log();
                    var builder = new StringBuilder(512);
                    if (!_headerWritten)
                    {
                        builder.AppendLine("# Runtime AI Lab Behavior Recipe Editor Log");
                        builder.AppendLine("# utc\tsession\tseq\tphase\taction\tentry\tworkspace\tgroup\tcard\trevision\telement\tbefore\tafter\tresult\tmessage");
                        _headerWritten = true;
                    }
                    builder.Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)).Append('\t')
                        .Append(SessionId).Append('\t')
                        .Append(++_sequence).Append('\t')
                        .Append(Clean(phase)).Append('\t')
                        .Append(Clean(action)).Append('\t')
                        .Append(entryIndex).Append('\t')
                        .Append(Clean(identity.WorkspaceId)).Append('\t')
                        .Append(Clean(identity.GroupDraftId)).Append('\t')
                        .Append(Clean(identity.CardDraftId)).Append('\t')
                        .Append(identity.DraftRevision).Append('\t')
                        .Append(Clean(elementId)).Append('\t')
                        .Append(beforeCount).Append('\t')
                        .Append(afterCount).Append('\t')
                        .Append(Clean(result)).Append('\t')
                        .Append(Clean(message))
                        .AppendLine();
                    File.AppendAllText(ResolvedFilePath, builder.ToString(), new UTF8Encoding(false));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Behavior Recipe editor log write failed: " + exception.Message);
            }
        }

        internal static void WriteVisibility(
            string source,
            string action,
            string operationId,
            CharacterTestRuntimeAiLabShellState shellState,
            bool? hostConfigured,
            bool? navigatorOpen,
            bool? rootResolved,
            bool? rootVisible,
            bool? rootOnStage,
            string result,
            string message)
        {
            try
            {
                lock (Sync)
                {
                    string directory = Path.GetDirectoryName(ResolvedFilePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    EnsureWindowsReadableUtf8Log();
                    var builder = new StringBuilder(512);
                    if (!_visibilityHeaderWritten)
                    {
                        builder.AppendLine("# Behavior Recipe UI visibility lifecycle");
                        builder.AppendLine("# utc\tsession\tseq\tphase\tsource\taction\toperationId\tshellGeneration\tshellRevision\tshellVisibility\tshellPresentation\tshellWorkspace\ttimelineMode\thostConfigured\tnavigatorOpen\trootResolved\trootVisible\trootOnStage\tresult\tmessage");
                        _visibilityHeaderWritten = true;
                    }

                    builder.Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)).Append('\t')
                        .Append(SessionId).Append('\t')
                        .Append(++_sequence).Append('\t')
                        .Append("visibility").Append('\t')
                        .Append(Clean(source)).Append('\t')
                        .Append(Clean(action)).Append('\t')
                        .Append(Clean(operationId)).Append('\t')
                        .Append(shellState?.Generation ?? 0L).Append('\t')
                        .Append(shellState?.Revision ?? 0L).Append('\t')
                        .Append(shellState?.Visibility.ToString() ?? "Unavailable").Append('\t')
                        .Append(shellState?.PresentationMode.ToString() ?? "Unavailable").Append('\t')
                        .Append(shellState?.ActiveWorkspace.ToString() ?? "Unavailable").Append('\t')
                        .Append(shellState?.PageContext?.TimelineMode.ToString() ?? "Unavailable").Append('\t')
                        .Append(FormatObserved(hostConfigured)).Append('\t')
                        .Append(FormatObserved(navigatorOpen)).Append('\t')
                        .Append(FormatObserved(rootResolved)).Append('\t')
                        .Append(FormatObserved(rootVisible)).Append('\t')
                        .Append(FormatObserved(rootOnStage)).Append('\t')
                        .Append(Clean(result)).Append('\t')
                        .Append(Clean(message))
                        .AppendLine();
                    File.AppendAllText(ResolvedFilePath, builder.ToString(), new UTF8Encoding(false));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Behavior Recipe visibility log write failed: " + exception.Message);
            }
        }

        internal static void WriteWorkbenchMutation(string message)
        {
            try
            {
                lock (Sync)
                {
                    string directory = Path.GetDirectoryName(ResolvedFilePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    EnsureWindowsReadableUtf8Log();
                    var builder = new StringBuilder(384);
                    if (!_workbenchHeaderWritten)
                    {
                        builder.AppendLine("# Behavior Recipe workbench mutations");
                        builder.AppendLine("# utc\tsession\tseq\tphase\tdetail");
                        _workbenchHeaderWritten = true;
                    }
                    builder.Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)).Append('\t')
                        .Append(SessionId).Append('\t')
                        .Append(++_sequence).Append('\t')
                        .Append("workbench").Append('\t')
                        .Append(Clean(message))
                        .AppendLine();
                    File.AppendAllText(ResolvedFilePath, builder.ToString(), new UTF8Encoding(false));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Behavior Recipe workbench log write failed: " + exception.Message);
            }
        }

        private static string ResolveFilePath()
        {
            string root = Application.persistentDataPath;
            if (Application.isEditor)
            {
                DirectoryInfo project = Directory.GetParent(Application.dataPath);
                if (project != null)
                    root = project.FullName;
            }
            return Path.Combine(
                root ?? string.Empty,
                "Logs",
                "CharacterTest",
                "runtime-ai-lab-behavior-recipe-editor.log");
        }

        private static void EnsureWindowsReadableUtf8Log()
        {
            if (!File.Exists(ResolvedFilePath))
            {
                File.WriteAllBytes(ResolvedFilePath, Utf8Preamble);
                return;
            }

            byte[] content = File.ReadAllBytes(ResolvedFilePath);
            if (content.Length >= Utf8Preamble.Length
                && content[0] == Utf8Preamble[0]
                && content[1] == Utf8Preamble[1]
                && content[2] == Utf8Preamble[2])
            {
                return;
            }

            byte[] upgraded = new byte[Utf8Preamble.Length + content.Length];
            Buffer.BlockCopy(Utf8Preamble, 0, upgraded, 0, Utf8Preamble.Length);
            Buffer.BlockCopy(content, 0, upgraded, Utf8Preamble.Length, content.Length);
            File.WriteAllBytes(ResolvedFilePath, upgraded);
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty)
                .Replace('\t', ' ')
                .Replace('\r', ' ')
                .Replace('\n', ' ');
        }

        private static string FormatObserved(bool? value)
        {
            return value.HasValue ? value.Value.ToString() : "Unobserved";
        }
    }
}
