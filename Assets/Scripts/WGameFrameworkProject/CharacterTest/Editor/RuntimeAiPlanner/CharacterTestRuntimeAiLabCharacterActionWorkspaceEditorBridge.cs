using System;
using System.IO;
using MxFramework.Editor.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestRuntimeAiLabCharacterActionAuthoringEditorBridge
    {
        private static CharacterActionAuthoringWorkspaceSession s_workspaceSession;
        private static CharacterTestRuntimeAiLabCharacterActionAuthoringRequest s_workspaceOpenRequest;
        private static ResolvedRequest s_workspaceResolved;
        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot s_workspaceMappedSnapshot;

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot HandleWorkspace(
            CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest request)
        {
            try
            {
                return request?.Kind switch
                {
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Open =>
                        OpenWorkspace(request.OpenRequest),
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Execute =>
                        ExecuteWorkspace(request.Command),
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Close =>
                        CloseWorkspace(),
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewPlay =>
                        PreviewWorkspace(request.PreviewFrame, play: true),
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewPause =>
                        PreviewWorkspace(request.PreviewFrame, play: false),
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewStep =>
                        PreviewWorkspace(request.PreviewFrame, play: false),
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewScrub =>
                        PreviewWorkspace(request.PreviewFrame, play: false),
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.PreviewStop =>
                        StopWorkspacePreview(),
                    CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind.Capture =>
                        CaptureWorkspace(),
                    _ => UnavailableWorkspace("不支持的 CharacterAction 工作区请求。"),
                };
            }
            catch (Exception ex)
            {
                return UnavailableWorkspace(ex.Message);
            }
        }

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot OpenWorkspace(
            CharacterTestRuntimeAiLabCharacterActionAuthoringRequest request)
        {
            string error = string.Empty;
            if (request == null
                || !TryResolve(request, out ResolvedRequest resolved, out error))
            {
                return UnavailableWorkspace(error ?? "无法解析动作正式源。");
            }
            s_workspaceSession?.Dispose();
            var open = new CharacterActionAuthoringOpenRequest
            {
                RequestId = "runtime-ai-lab-embedded-" + Guid.NewGuid().ToString("N"),
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
            };
            s_workspaceSession = CharacterActionAuthoringWorkspaceSession.Open(open);
            s_workspaceOpenRequest = request;
            s_workspaceResolved = resolved;
            return MapWorkspace(s_workspaceSession.BuildSnapshot("已打开完整动作编排工作区。"));
        }

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot ExecuteWorkspace(
            CharacterTestRuntimeAiLabCharacterActionWorkspaceCommand command)
        {
            if (s_workspaceSession == null)
                return UnavailableWorkspace("CharacterAction 动作编排会话尚未打开。");
            var editorCommand = new CharacterActionAuthoringWorkspaceCommand
            {
                Kind = Enum.Parse<CharacterActionAuthoringWorkspaceCommandKind>(command.Kind.ToString()),
                ExpectedRevision = command.ExpectedRevision,
                Text = command.Text,
                SecondaryText = command.SecondaryText,
                IntValue = command.IntValue,
                SecondaryIntValue = command.SecondaryIntValue,
            };
            CharacterActionAuthoringWorkspaceSnapshot editorSnapshot =
                s_workspaceSession.Execute(editorCommand);
            if (command.Kind
                == CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.SelectTimelineItem)
            {
                return MapSelectionWorkspace(editorSnapshot);
            }
            if (command.Kind == CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind.SaveAll
                && !editorSnapshot.HasErrors
                && s_workspaceOpenRequest != null
                && s_workspaceResolved != null)
            {
                ScheduleApply(s_workspaceOpenRequest, s_workspaceResolved);
            }
            return MapWorkspace(editorSnapshot);
        }

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot CloseWorkspace()
        {
            CleanupWorkspacePreviewSource();
            s_workspaceSession?.Dispose();
            s_workspaceSession = null;
            s_workspaceOpenRequest = null;
            s_workspaceResolved = null;
            s_workspaceMappedSnapshot = null;
            return new CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot
            {
                Status = "已关闭 CharacterAction 动作编排工作区。",
            };
        }

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot PreviewWorkspace(
            int frame,
            bool play)
        {
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot snapshot = CurrentWorkspace();
            snapshot.PreviewActive = true;
            snapshot.PreviewPlaying = play;
            snapshot.PreviewFrame = Math.Max(0, Math.Min(frame, Math.Max(0, snapshot.DurationFrames - 1)));
            snapshot.PreviewSourceDirectory = EnsureWorkspacePreviewSource();
            snapshot.Status = play
                ? "当前角色草稿预览正在播放。"
                : "当前角色草稿预览已暂停。";
            return snapshot;
        }

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot StopWorkspacePreview()
        {
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot snapshot = CurrentWorkspace();
            snapshot.PreviewActive = false;
            snapshot.PreviewPlaying = false;
            snapshot.PreviewFrame = 0;
            CleanupWorkspacePreviewSource();
            snapshot.Status = "预览已停止；测试将从第 0 帧重置。";
            return snapshot;
        }

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot CurrentWorkspace()
        {
            return s_workspaceSession == null
                ? UnavailableWorkspace("CharacterAction 动作编排会话尚未打开。")
                : MapWorkspace(s_workspaceSession.BuildSnapshot());
        }

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot CaptureWorkspace()
        {
            if (s_workspaceSession == null)
                return UnavailableWorkspace("CharacterAction 动作编排会话尚未打开。");
            return s_workspaceMappedSnapshot ?? CurrentWorkspace();
        }

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot MapWorkspace(
            CharacterActionAuthoringWorkspaceSnapshot source)
        {
            if (source == null)
                return UnavailableWorkspace("CharacterAction 动作编排快照不可用。");
            CharacterActionAuthoringActionRow[] sourceActions =
                source.Actions ?? Array.Empty<CharacterActionAuthoringActionRow>();
            var actions = new CharacterTestRuntimeAiLabCharacterActionWorkspaceActionRow[
                sourceActions.Length];
            for (int i = 0; i < sourceActions.Length; i++)
            {
                CharacterActionAuthoringActionRow action = sourceActions[i];
                actions[i] = new CharacterTestRuntimeAiLabCharacterActionWorkspaceActionRow
                {
                    ActionId = action?.ActionId ?? 0,
                    DisplayName = action?.DisplayName ?? string.Empty,
                    LibraryId = action?.LibraryId ?? string.Empty,
                    Category = action?.Category ?? string.Empty,
                    DurationFrames = action?.DurationFrames ?? 0,
                    Selected = action?.Selected == true,
                    Dirty = action?.Dirty == true,
                    ProblemCount = action?.ProblemCount ?? 0,
                };
            }

            CharacterActionAuthoringTimelineTrackSnapshot[] sourceTracks =
                source.Tracks ?? Array.Empty<CharacterActionAuthoringTimelineTrackSnapshot>();
            var tracks = new CharacterTestRuntimeAiLabCharacterActionWorkspaceTrack[
                sourceTracks.Length];
            for (int trackIndex = 0; trackIndex < sourceTracks.Length; trackIndex++)
            {
                CharacterActionAuthoringTimelineTrackSnapshot sourceTrack = sourceTracks[trackIndex];
                CharacterActionAuthoringTimelineItemSnapshot[] sourceItems =
                    sourceTrack?.Items ?? Array.Empty<CharacterActionAuthoringTimelineItemSnapshot>();
                var items = new CharacterTestRuntimeAiLabCharacterActionWorkspaceTimelineItem[
                    sourceItems.Length];
                for (int itemIndex = 0; itemIndex < sourceItems.Length; itemIndex++)
                {
                    CharacterActionAuthoringTimelineItemSnapshot item = sourceItems[itemIndex];
                    items[itemIndex] = new CharacterTestRuntimeAiLabCharacterActionWorkspaceTimelineItem
                    {
                        ItemId = item?.ItemId ?? string.Empty,
                        TrackKind = item?.TrackKind ?? string.Empty,
                        Title = item?.Title ?? string.Empty,
                        StartFrame = item?.StartFrame ?? 0,
                        EndFrame = item?.EndFrame ?? 0,
                        CanEdit = item?.CanEdit == true,
                        CanResize = item?.CanResize == true,
                        Selected = item?.Selected == true,
                        ColorRgba = item?.ColorRgba ?? 0,
                    };
                }
                tracks[trackIndex] = new CharacterTestRuntimeAiLabCharacterActionWorkspaceTrack
                {
                    Kind = sourceTrack?.Kind ?? string.Empty,
                    Label = sourceTrack?.Label ?? string.Empty,
                    Items = items,
                };
            }

            var mapped = new CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot
            {
                Available = true,
                SessionId = source.SessionId ?? string.Empty,
                Revision = source.Revision,
                ProfileId = source.ProfileId ?? string.Empty,
                ActionSetId = source.ActionSetId ?? string.Empty,
                LibraryId = source.LibraryId ?? string.Empty,
                SelectedActionId = source.SelectedActionId,
                SelectedTimelineItemId = source.SelectedTimelineItemId ?? string.Empty,
                DurationFrames = source.DurationFrames,
                ReadOnly = source.ReadOnly,
                Dirty = source.Dirty,
                CanUndo = source.CanUndo,
                CanRedo = source.CanRedo,
                HasErrors = source.HasErrors,
                Status = source.Status ?? string.Empty,
                ReturnSelectedAgentIndex = s_workspaceOpenRequest?.SelectedAgentIndex ?? 0,
                ReturnStableCardId = s_workspaceOpenRequest?.StableCardId ?? string.Empty,
                ReturnTimelineId = s_workspaceOpenRequest?.TimelineId ?? string.Empty,
                ReturnEventId = s_workspaceOpenRequest?.EventId ?? string.Empty,
                ReturnTimelineMode = s_workspaceOpenRequest?.TimelineMode
                    ?? CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                Actions = actions,
                Tracks = tracks,
                InspectorFields = MapInspectorFields(source.InspectorFields),
                Problems = source.Problems ?? Array.Empty<string>(),
                DirtyPaths = source.DirtyPaths ?? Array.Empty<string>(),
            };
            s_workspaceMappedSnapshot = mapped;
            return mapped;
        }

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot MapSelectionWorkspace(
            CharacterActionAuthoringWorkspaceSnapshot source)
        {
            if (source == null || s_workspaceMappedSnapshot == null)
                return MapWorkspace(source);
            s_workspaceMappedSnapshot.Revision = source.Revision;
            s_workspaceMappedSnapshot.SelectedTimelineItemId = source.SelectedTimelineItemId;
            s_workspaceMappedSnapshot.Status = source.Status;
            for (int trackIndex = 0;
                 trackIndex < s_workspaceMappedSnapshot.Tracks.Length;
                 trackIndex++)
            {
                CharacterTestRuntimeAiLabCharacterActionWorkspaceTimelineItem[] items =
                    s_workspaceMappedSnapshot.Tracks[trackIndex]?.Items
                    ?? Array.Empty<CharacterTestRuntimeAiLabCharacterActionWorkspaceTimelineItem>();
                for (int itemIndex = 0; itemIndex < items.Length; itemIndex++)
                {
                    if (items[itemIndex] != null)
                    {
                        items[itemIndex].Selected = string.Equals(
                            items[itemIndex].ItemId,
                            source.SelectedTimelineItemId,
                            StringComparison.Ordinal);
                    }
                }
            }
            s_workspaceMappedSnapshot.InspectorFields = MapInspectorFields(source.InspectorFields);
            return s_workspaceMappedSnapshot;
        }

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField[]
            MapInspectorFields(CharacterActionAuthoringInspectorFieldDescriptor[] sourceFields)
        {
            sourceFields ??= Array.Empty<CharacterActionAuthoringInspectorFieldDescriptor>();
            var fields = new CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField[
                sourceFields.Length];
            for (int i = 0; i < sourceFields.Length; i++)
            {
                CharacterActionAuthoringInspectorFieldDescriptor field = sourceFields[i];
                fields[i] = new CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField
                {
                    Path = field?.Path ?? string.Empty,
                    Label = field?.Label ?? string.Empty,
                    Value = field?.Value ?? string.Empty,
                    Kind = field?.Kind.ToString() ?? string.Empty,
                    Editable = field?.Editable == true,
                    Options = field?.Options ?? Array.Empty<string>(),
                    OptionLabels = field?.OptionLabels ?? Array.Empty<string>(),
                };
            }
            return fields;
        }

        private static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot UnavailableWorkspace(
            string status)
        {
            return new CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot
            {
                Status = status ?? string.Empty,
            };
        }

        private static string EnsureWorkspacePreviewSource()
        {
            if (s_workspaceSession == null || s_workspaceResolved == null)
                return string.Empty;
            string root = Path.GetFullPath(Path.Combine(
                "Temp",
                "RuntimeAiLab",
                "CharacterActionPreview",
                s_workspaceSession.SessionId));
            string allowedRoot = Path.GetFullPath(Path.Combine("Temp", "RuntimeAiLab", "CharacterActionPreview"));
            if (!root.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("CharacterAction preview path escaped Temp root.");
            Directory.CreateDirectory(root);
            CopyIfExists(
                Path.Combine(s_workspaceResolved.ProfileSourceDirectory, "character_action_profile.json"),
                Path.Combine(root, "character_action_profile.json"));
            CopyIfExists(
                Path.Combine(s_workspaceResolved.ActionSetSourceDirectory, "character_action_sets.json"),
                Path.Combine(root, "character_action_sets.json"));
            CopyIfExists(
                Path.Combine(s_workspaceResolved.ActionSetSourceDirectory, "character_reaction_profiles.json"),
                Path.Combine(root, "character_reaction_profiles.json"));
            CopyIfExists(
                Path.Combine(s_workspaceResolved.ActionSetSourceDirectory, "character_landing_profiles.json"),
                Path.Combine(root, "character_landing_profiles.json"));
            File.WriteAllText(
                Path.Combine(root, "character_actions.json"),
                s_workspaceSession.ExportMergedDraftActionsJson());
            RebindPreviewProfileLibrary(
                Path.Combine(root, "character_action_profile.json"),
                Path.GetFileName(root));
            return root.Replace('\\', '/');
        }

        private static void CleanupWorkspacePreviewSource()
        {
            if (s_workspaceSession == null)
                return;
            string root = Path.GetFullPath(Path.Combine(
                "Temp",
                "RuntimeAiLab",
                "CharacterActionPreview",
                s_workspaceSession.SessionId));
            string allowedRoot = Path.GetFullPath(Path.Combine("Temp", "RuntimeAiLab", "CharacterActionPreview"));
            if (root.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        private static void CleanupStaleWorkspacePreviewSources()
        {
            string root = Path.GetFullPath(Path.Combine("Temp", "RuntimeAiLab", "CharacterActionPreview"));
            string projectTemp = Path.GetFullPath("Temp");
            if (!root.StartsWith(projectTemp, StringComparison.OrdinalIgnoreCase))
                return;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        private static void CopyIfExists(string source, string destination)
        {
            if (File.Exists(source))
                File.Copy(source, destination, overwrite: true);
        }

        private static void RebindPreviewProfileLibrary(string profilePath, string libraryId)
        {
            if (!File.Exists(profilePath))
                return;
            JObject root = JObject.Parse(File.ReadAllText(profilePath));
            foreach (JObject profile in root["profiles"] as JArray ?? new JArray())
                profile["actionLibraryIds"] = new JArray(libraryId ?? string.Empty);
            File.WriteAllText(profilePath, root.ToString(Formatting.Indented));
        }
    }
}
