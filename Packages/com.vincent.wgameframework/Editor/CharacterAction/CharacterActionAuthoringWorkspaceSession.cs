using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor.CharacterAction
{
    public enum CharacterActionAuthoringWorkspaceCommandKind
    {
        SelectAction,
        CreateAction,
        DuplicateAction,
        DeleteAction,
        SelectTimelineItem,
        AddTimelineItem,
        MoveResizeTimelineItem,
        DeleteTimelineItem,
        SetField,
        BeginGesture,
        EndGesture,
        Undo,
        Redo,
        Validate,
        SaveAll,
        AddArrayElement,
        DeleteArrayElement,
    }

    public enum CharacterActionAuthoringInspectorFieldKind
    {
        Text,
        Integer,
        Float,
        Toggle,
        Enum,
        Flags,
        Reference,
        ReadOnly,
        Array,
    }

    public sealed class CharacterActionAuthoringWorkspaceCommand
    {
        public CharacterActionAuthoringWorkspaceCommandKind Kind { get; set; }
        public long ExpectedRevision { get; set; }
        public string Text { get; set; } = string.Empty;
        public string SecondaryText { get; set; } = string.Empty;
        public int IntValue { get; set; }
        public int SecondaryIntValue { get; set; }
    }

    public sealed class CharacterActionAuthoringActionRow
    {
        public int ActionId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string LibraryId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int DurationFrames { get; set; }
        public bool Selected { get; set; }
        public bool Dirty { get; set; }
        public int ProblemCount { get; set; }
    }

    public sealed class CharacterActionAuthoringTimelineItemSnapshot
    {
        public string ItemId { get; set; } = string.Empty;
        public string TrackKind { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
        public bool CanEdit { get; set; }
        public bool CanResize { get; set; }
        public bool Selected { get; set; }
        public uint ColorRgba { get; set; }
    }

    public sealed class CharacterActionAuthoringTimelineTrackSnapshot
    {
        public string Kind { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public CharacterActionAuthoringTimelineItemSnapshot[] Items { get; set; } =
            Array.Empty<CharacterActionAuthoringTimelineItemSnapshot>();
    }

    public sealed class CharacterActionAuthoringInspectorFieldDescriptor
    {
        public string Path { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public CharacterActionAuthoringInspectorFieldKind Kind { get; set; }
        public bool Editable { get; set; }
        public string[] Options { get; set; } = Array.Empty<string>();
        public string[] OptionLabels { get; set; } = Array.Empty<string>();
    }

    public sealed class CharacterActionAuthoringWorkspaceSnapshot
    {
        public string SessionId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public string ProfileId { get; set; } = string.Empty;
        public string ActionSetId { get; set; } = string.Empty;
        public string LibraryId { get; set; } = string.Empty;
        public int SelectedActionId { get; set; }
        public string SelectedTimelineItemId { get; set; } = string.Empty;
        public int DurationFrames { get; set; }
        public bool ReadOnly { get; set; }
        public bool Dirty { get; set; }
        public bool CanUndo { get; set; }
        public bool CanRedo { get; set; }
        public bool HasErrors { get; set; }
        public string Status { get; set; } = string.Empty;
        public CharacterActionAuthoringActionRow[] Actions { get; set; } =
            Array.Empty<CharacterActionAuthoringActionRow>();
        public CharacterActionAuthoringTimelineTrackSnapshot[] Tracks { get; set; } =
            Array.Empty<CharacterActionAuthoringTimelineTrackSnapshot>();
        public CharacterActionAuthoringInspectorFieldDescriptor[] InspectorFields { get; set; } =
            Array.Empty<CharacterActionAuthoringInspectorFieldDescriptor>();
        public string[] Problems { get; set; } = Array.Empty<string>();
        public string[] DirtyPaths { get; set; } = Array.Empty<string>();
    }

    public sealed class CharacterActionAuthoringWorkspaceSession : IDisposable
    {
        private const int MaxHistory = 64;
        private readonly string _sessionId = Guid.NewGuid().ToString("N");
        private readonly List<WorkspaceState> _undo = new List<WorkspaceState>();
        private readonly List<WorkspaceState> _redo = new List<WorkspaceState>();
        private readonly Dictionary<string, string> _workspaceBaselineCanonicalByPath =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private CharacterActionAuthoringDocumentSession _documents;
        private CharacterActionAuthoringModel _model;
        private CharacterActionAuthoringExportResult _validation;
        private CharacterActionAuthoringWorkspaceSnapshot _lastSnapshot;
        private bool _gestureActive;
        private long _revision = 1;
        private string _status = string.Empty;

        private CharacterActionAuthoringWorkspaceSession()
        {
        }

        public string SessionId => _sessionId;
        public long Revision => _revision;

        public static CharacterActionAuthoringWorkspaceSession Open(
            CharacterActionAuthoringOpenRequest request)
        {
            request ??= new CharacterActionAuthoringOpenRequest();
            var profileSource = new CharacterActionAuthoringSourceEntry(
                request.ProfileSourceDirectory,
                Path.GetFileName(request.ProfileSourceDirectory));
            var result = new CharacterActionAuthoringWorkspaceSession
            {
                _documents = CharacterActionAuthoringDocumentSession.Load(
                    profileSource,
                    request.ProfileId),
            };
            if (!string.IsNullOrWhiteSpace(request.ActionSetStableId))
                result._documents.SelectActionSet(request.ActionSetStableId);
            if (!string.IsNullOrWhiteSpace(request.ActionLibraryId))
                result._documents.SelectActionLibrary(request.ActionLibraryId);
            if (request.ActionId > 0)
                result._documents.SelectAction(request.ActionId);
            result.LoadModelFromCurrentLibrary();
            result.Validate();
            result.CaptureWorkspaceBaseline();
            return result;
        }

        public CharacterActionAuthoringWorkspaceSnapshot Execute(
            CharacterActionAuthoringWorkspaceCommand command)
        {
            if (command == null)
                return BuildSnapshot("命令为空。");
            if (command.ExpectedRevision > 0 && command.ExpectedRevision != _revision)
                return BuildSnapshot("编辑状态已更新，请刷新后重试。");

            bool selectionOnly = false;
            try
            {
                switch (command.Kind)
                {
                    case CharacterActionAuthoringWorkspaceCommandKind.SelectAction:
                        SelectAction(command.IntValue);
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.CreateAction:
                        Mutate(() => CreateAction(command.Text));
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.DuplicateAction:
                        Mutate(DuplicateAction);
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.DeleteAction:
                        Mutate(DeleteAction);
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.SelectTimelineItem:
                        _documents.SelectTimelineItem(command.Text);
                        _revision++;
                        selectionOnly = true;
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.AddTimelineItem:
                        Mutate(() => AddTimelineItem(command.Text, command.IntValue));
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.MoveResizeTimelineItem:
                        Mutate(() => MoveResizeTimelineItem(
                            command.Text,
                            command.IntValue,
                            command.SecondaryIntValue));
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.DeleteTimelineItem:
                        Mutate(() => DeleteTimelineItem(command.Text));
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.SetField:
                        Mutate(() => SetField(command.Text, command.SecondaryText));
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.BeginGesture:
                        BeginGesture();
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.EndGesture:
                        EndGesture();
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.Undo:
                        Undo();
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.Redo:
                        Redo();
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.Validate:
                        Validate();
                        _revision++;
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.SaveAll:
                        SaveAll();
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.AddArrayElement:
                        Mutate(() => MutateArray(command.Text, add: true));
                        break;
                    case CharacterActionAuthoringWorkspaceCommandKind.DeleteArrayElement:
                        Mutate(() => MutateArray(command.Text, add: false));
                        break;
                }
            }
            catch (Exception ex)
            {
                _status = ex.Message;
            }
            return selectionOnly
                ? BuildSelectionSnapshot(_status)
                : BuildSnapshot(_status);
        }

        public CharacterActionAuthoringWorkspaceSnapshot BuildSnapshot(string status = "")
        {
            SyncModelToCurrentLibrary();
            CharacterActionJsonDto action = _model?.SelectedAction;
            CharacterActionTimelineSnapshot timeline = action == null
                ? null
                : CharacterActionTimelineSnapshot.FromAction(action, 8);
            string selectedItem = _documents.Selection.TimelineItemStableId ?? string.Empty;
            var tracks = new List<CharacterActionAuthoringTimelineTrackSnapshot>();
            if (timeline != null)
            {
                for (int i = 0; i < timeline.Tracks.Count; i++)
                {
                    TimelineTrackDescriptor descriptor = timeline.Tracks[i];
                    IReadOnlyList<ITimelineItemAdapter> sourceItems =
                        timeline.ItemsByTrack.TryGetValue(descriptor.Kind, out IReadOnlyList<ITimelineItemAdapter> found)
                            ? found
                            : Array.Empty<ITimelineItemAdapter>();
                    var items = new CharacterActionAuthoringTimelineItemSnapshot[sourceItems.Count];
                    for (int j = 0; j < sourceItems.Count; j++)
                    {
                        ITimelineItemAdapter item = sourceItems[j];
                        items[j] = new CharacterActionAuthoringTimelineItemSnapshot
                        {
                            ItemId = item.Id,
                            TrackKind = item.TrackKind.ToString(),
                            Title = item.FullTitle,
                            StartFrame = item.StartFrame,
                            EndFrame = item.EndFrame,
                            CanEdit = item.CanEdit,
                            CanResize = item.CanEdit
                                && item.ItemKind == TimelineItemKind.Clip
                                && item is not RootMotionTimelineAdapter,
                            Selected = string.Equals(item.Id, selectedItem, StringComparison.Ordinal),
                            ColorRgba = PackColor(item.Color),
                        };
                    }
                    tracks.Add(new CharacterActionAuthoringTimelineTrackSnapshot
                    {
                        Kind = descriptor.Kind.ToString(),
                        Label = descriptor.Title,
                        Items = items,
                    });
                }
            }

            var dirtyByDocument = new Dictionary<CharacterActionAuthoringDocumentState, bool>();
            for (int i = 0; i < _documents.DocumentStates.Count; i++)
            {
                CharacterActionAuthoringDocumentState state = _documents.DocumentStates[i];
                if (state != null)
                    dirtyByDocument[state] = IsWorkspaceDocumentDirty(state);
            }
            CharacterActionAuthoringActionRow[] actions = _documents.ProfileActions
                .Select(reference => new CharacterActionAuthoringActionRow
                {
                    ActionId = reference.Action?.ActionId ?? 0,
                    DisplayName = reference.Action?.DisplayName
                        ?? "Action #" + (reference.Action?.ActionId ?? 0),
                    LibraryId = reference.LibraryId,
                    Category = reference.Action?.Category ?? string.Empty,
                    DurationFrames = Math.Max(0, reference.Action?.DurationFrames ?? 0),
                    Selected = reference.Action?.ActionId == _documents.Selection.ActionId,
                    Dirty = reference.Library?.State != null
                        && dirtyByDocument.TryGetValue(reference.Library.State, out bool isDirty)
                        && isDirty,
                    ProblemCount = CountProblems(reference.Action?.ActionId ?? 0),
                })
                .OrderBy(row => row.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.ActionId)
                .ToArray();

            string[] problems = SplitProblems(_validation?.DiagnosticsText);
            var snapshot = new CharacterActionAuthoringWorkspaceSnapshot
            {
                SessionId = _sessionId,
                Revision = _revision,
                ProfileId = _documents.Selection.ProfileStableId,
                ActionSetId = _documents.Selection.ActionSetStableId,
                LibraryId = _documents.Selection.ActionLibraryId,
                SelectedActionId = _documents.Selection.ActionId,
                SelectedTimelineItemId = selectedItem,
                DurationFrames = timeline?.ActionDurationFrames ?? 0,
                ReadOnly = timeline?.ReadOnly == true,
                Dirty = dirtyByDocument.Values.Any(value => value),
                CanUndo = _undo.Count > 0,
                CanRedo = _redo.Count > 0,
                HasErrors = _validation?.HasErrors != false,
                Status = string.IsNullOrEmpty(status) ? _status : status,
                Actions = actions,
                Tracks = tracks.ToArray(),
                InspectorFields = BuildInspectorFields(action, timeline, selectedItem),
                Problems = problems,
                DirtyPaths = _documents.DocumentStates
                    .Where(state => state != null
                        && dirtyByDocument.TryGetValue(state, out bool isDirty)
                        && isDirty)
                    .Select(state => state.Key.SourcePath)
                    .ToArray(),
            };
            _lastSnapshot = snapshot;
            return snapshot;
        }

        private CharacterActionAuthoringWorkspaceSnapshot BuildSelectionSnapshot(string status)
        {
            if (_lastSnapshot == null || _model?.SelectedAction == null)
                return BuildSnapshot(status);
            string selectedItem = _documents.Selection.TimelineItemStableId ?? string.Empty;
            for (int trackIndex = 0; trackIndex < _lastSnapshot.Tracks.Length; trackIndex++)
            {
                CharacterActionAuthoringTimelineItemSnapshot[] items =
                    _lastSnapshot.Tracks[trackIndex]?.Items
                    ?? Array.Empty<CharacterActionAuthoringTimelineItemSnapshot>();
                for (int itemIndex = 0; itemIndex < items.Length; itemIndex++)
                {
                    if (items[itemIndex] != null)
                    {
                        items[itemIndex].Selected = string.Equals(
                            items[itemIndex].ItemId,
                            selectedItem,
                            StringComparison.Ordinal);
                    }
                }
            }
            CharacterActionTimelineSnapshot timeline =
                CharacterActionTimelineSnapshot.FromAction(_model.SelectedAction, 8);
            _lastSnapshot.Revision = _revision;
            _lastSnapshot.SelectedTimelineItemId = selectedItem;
            _lastSnapshot.InspectorFields = BuildInspectorFields(
                _model.SelectedAction,
                timeline,
                selectedItem);
            _lastSnapshot.Status = string.IsNullOrEmpty(status) ? _status : status;
            return _lastSnapshot;
        }

        public string ExportCurrentDraftActionsJson()
        {
            SyncModelToCurrentLibrary();
            CharacterActionAuthoringJsonService.ExportJsonUnchecked(
                _model,
                out _,
                out string actionsJson,
                out _,
                out _);
            return actionsJson;
        }

        public string ExportMergedDraftActionsJson()
        {
            SyncModelToCurrentLibrary();
            var mergedActions = new JArray();
            var mergedAnimationSources = new JArray();
            var seenActions = new HashSet<int>();
            for (int i = 0; i < _documents.ActionLibraries.Count; i++)
            {
                CharacterActionAuthoringActionLibraryDocument library = _documents.ActionLibraries[i];
                JObject root = JObject.Parse(library.Json);
                foreach (JToken source in root["animationSources"] as JArray ?? new JArray())
                    mergedAnimationSources.Add(source.DeepClone());
                foreach (JObject action in root["actions"] as JArray ?? new JArray())
                {
                    int actionId = (int?)action["actionId"] ?? 0;
                    if (actionId > 0 && seenActions.Add(actionId))
                        mergedActions.Add(action.DeepClone());
                }
            }
            return new JObject
            {
                ["schemaVersion"] = CharacterActionJsonImporter.CurrentActionLibrarySchemaVersion,
                ["animationSources"] = mergedAnimationSources,
                ["actions"] = mergedActions,
            }.ToString(Formatting.Indented);
        }

        public void Dispose()
        {
            _undo.Clear();
            _redo.Clear();
            _model = null;
            _documents = null;
        }

        private void SelectAction(int actionId)
        {
            SyncModelToCurrentLibrary();
            if (!_documents.SelectAction(actionId))
                throw new InvalidOperationException("动作不存在：" + actionId);
            LoadModelFromCurrentLibrary();
            _documents.SelectTimelineItem(string.Empty);
            _revision++;
            Validate();
        }

        private void CreateAction(string requestedDisplayName)
        {
            _model.AddAction(CharacterActionAuthoringModel.CreateBlankAction(
                AllocateActionId(),
                string.IsNullOrWhiteSpace(requestedDisplayName)
                    ? "New Action"
                    : requestedDisplayName.Trim()));
            SyncModelToCurrentLibrary();
            _documents.SelectAction(_model.SelectedAction.ActionId);
        }

        private void DuplicateAction()
        {
            CharacterActionJsonDto copy = _model.DuplicateSelectedAction(AllocateActionId());
            if (copy == null)
                throw new InvalidOperationException("当前没有可复制的动作。");
            SyncModelToCurrentLibrary();
            _documents.SelectAction(copy.ActionId);
        }

        private void DeleteAction()
        {
            int retiredActionId = _model.SelectedAction?.ActionId ?? 0;
            if (!_model.DeleteSelectedAction())
                throw new InvalidOperationException("当前没有可删除的动作。");
            CharacterActionIdCatalog.Retire(retiredActionId);
            SyncModelToCurrentLibrary();
            _documents.RefreshActionIndex();
            if (_model.SelectedAction != null)
                _documents.SelectAction(_model.SelectedAction.ActionId);
        }

        private void AddTimelineItem(string trackKind, int frame)
        {
            if (!Enum.TryParse(trackKind, out TimelineTrackKind kind))
                throw new InvalidOperationException("未知动作轨道：" + trackKind);
            CharacterActionJsonDto action = _model.SelectedAction
                ?? throw new InvalidOperationException("当前没有选中动作。");
            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            frame = CharacterActionTimelineLayout.ClampFrame(frame, snapshot.ActionDurationFrames);
            switch (kind)
            {
                case TimelineTrackKind.Phases:
                    CharacterActionPhaseTimelineUtility.InsertPhaseAt(
                        action, frame, snapshot.ActionDurationFrames, CharacterActionPhaseKind.Active.ToString());
                    break;
                case TimelineTrackKind.TransitionWindow:
                    action.CancelRules = Append(
                        action.CancelRules,
                        TransitionWindowTimelineAdapter.CreateDefaultRule(
                            action, frame, snapshot.ActionDurationFrames));
                    break;
                case TimelineTrackKind.Motion:
                    action.MotionTrack ??= new MotionTrackJsonDto();
                    action.MotionTrack.Events = Append(action.MotionTrack.Events, new MotionTrackEventJsonDto
                    {
                        Frame = frame,
                        Kind = CharacterActionTrackEventKind.ApplyImpulse.ToString(),
                        MovementMode = CharacterMovementMode.Airborne.ToString(),
                        Space = CharacterActionMotionVectorSpace.World.ToString(),
                        Y = 8f,
                        StableEventId = CharacterActionAuthoringIds.Resolve(
                            CharacterActionTimelineIdentity.MakeStableEventId(action, "impulse"), 401),
                    });
                    break;
                case TimelineTrackKind.Animation:
                    action.AnimationTrack ??= new AnimationTrackJsonDto();
                    action.AnimationTrack.Events = Append(action.AnimationTrack.Events, new AnimationTrackEventJsonDto
                    {
                        Frame = frame,
                        EndFrame = frame,
                        Kind = CharacterActionTrackEventKind.CrossFadeAnimation.ToString(),
                        AnimationActionKey = "anim.action." + action.ActionId,
                        TransitionSeconds = 0.08f,
                        PlaybackSpeed = 1f,
                        StableEventId = CharacterActionAuthoringIds.Resolve(
                            CharacterActionTimelineIdentity.MakeStableEventId(action, "animation"), 401),
                    });
                    break;
                case TimelineTrackKind.Combat:
                    action.CombatTrack ??= new CombatTrackJsonDto();
                    action.CombatTrack.Events = Append(action.CombatTrack.Events, new CombatTrackEventJsonDto
                    {
                        Frame = frame,
                        Kind = CharacterActionTrackEventKind.StartCombatAction.ToString(),
                        CombatActionId = CharacterActionAuthoringIds.Resolve("combat.action." + action.ActionId, 412),
                        StableEventId = CharacterActionAuthoringIds.Resolve(
                            CharacterActionTimelineIdentity.MakeStableEventId(action, "combat"), 401),
                    });
                    break;
                case TimelineTrackKind.Gameplay:
                    action.GameplayTrack ??= new GameplayTrackJsonDto();
                    action.GameplayTrack.Events = Append(action.GameplayTrack.Events, new GameplayTrackEventJsonDto
                    {
                        Frame = frame,
                        Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                        RequestId = CharacterActionAuthoringIds.Resolve("gameplay.action." + action.ActionId, 408),
                        StableEventId = CharacterActionAuthoringIds.Resolve(
                            CharacterActionTimelineIdentity.MakeStableEventId(action, "gameplay"), 401),
                    });
                    break;
                case TimelineTrackKind.Presentation:
                    action.PresentationTrack ??= new PresentationTrackJsonDto();
                    action.PresentationTrack.Events = Append(action.PresentationTrack.Events, new PresentationTrackEventJsonDto
                    {
                        Frame = frame,
                        Kind = CharacterActionTrackEventKind.PlayAudioCue.ToString(),
                        CueId = CharacterActionAuthoringIds.Resolve("sfx.action." + action.ActionId, 401),
                        StableEventId = CharacterActionAuthoringIds.Resolve(
                            CharacterActionTimelineIdentity.MakeStableEventId(action, "sfx"), 401),
                    });
                    break;
            }
            CharacterActionEventIdentityAuthoring.AssignMissingEventOrders(action);
        }

        private void MoveResizeTimelineItem(string itemId, int startFrame, int endFrame)
        {
            ITimelineItemAdapter adapter = FindAdapter(itemId)
                ?? throw new InvalidOperationException("时间轴条目不存在：" + itemId);
            if (!adapter.CanEdit)
                throw new InvalidOperationException("当前时间轴条目为只读。");
            adapter.SetRange(startFrame, Math.Max(startFrame, endFrame));
        }

        private void DeleteTimelineItem(string itemId)
        {
            ITimelineItemAdapter adapter = FindAdapter(itemId)
                ?? throw new InvalidOperationException("时间轴条目不存在：" + itemId);
            if (!adapter.CanEdit)
                throw new InvalidOperationException("当前时间轴条目为只读。");
            adapter.Remove();
            _documents.SelectTimelineItem(string.Empty);
        }

        private void SetField(string path, string value)
        {
            CharacterActionJsonDto action = _model.SelectedAction
                ?? throw new InvalidOperationException("当前没有选中动作。");
            JObject root = JObject.FromObject(action, JsonSerializer.Create(
                CharacterActionAuthoringJsonService.JsonSettings));
            JToken token = root.SelectToken(path, errorWhenNoMatch: false)
                ?? throw new InvalidOperationException("字段不存在：" + path);
            if (token is not JValue current)
                throw new InvalidOperationException("字段不是可直接编辑的标量：" + path);
            current.Value = ConvertValue(value, current.Type);
            CharacterActionJsonDto replacement = root.ToObject<CharacterActionJsonDto>(
                JsonSerializer.Create(CharacterActionAuthoringJsonService.JsonSettings));
            replacement = CharacterActionAuthoringModel.NormalizeAction(replacement);
            _model.Actions[_model.SelectedActionIndex] = replacement;
            _documents.Selection.ActionId = replacement.ActionId;
        }

        private void BeginGesture()
        {
            if (_gestureActive)
                return;
            PushUndo();
            _redo.Clear();
            _gestureActive = true;
        }

        private void EndGesture()
        {
            if (!_gestureActive)
                return;
            _gestureActive = false;
            SyncModelToCurrentLibrary();
            Validate();
            _revision++;
        }

        private void Mutate(Action action)
        {
            if (!_gestureActive)
            {
                PushUndo();
                _redo.Clear();
            }
            action();
            SyncModelToCurrentLibrary();
            Validate();
            _revision++;
        }

        private void Undo()
        {
            if (_undo.Count == 0)
                return;
            WorkspaceState current = CaptureState();
            WorkspaceState target = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(current);
            RestoreState(target);
        }

        private void Redo()
        {
            if (_redo.Count == 0)
                return;
            WorkspaceState current = CaptureState();
            WorkspaceState target = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(current);
            RestoreState(target);
        }

        private void SaveAll()
        {
            SyncModelToCurrentLibrary();
            if (!_documents.DocumentStates.Any(IsWorkspaceDocumentDirty))
            {
                _status = "没有需要保存的动作修改。";
                return;
            }
            Validate();
            if (_validation == null || _validation.HasErrors)
                throw new InvalidOperationException("存在阻断错误，不能保存正式动作源。");
            ValidateDiskHashesBeforeSave();
            string[] paths = _documents.SaveAllDirty();
            CaptureWorkspaceBaseline();
            _status = paths.Length == 0
                ? "没有需要保存的动作修改。"
                : "已保存 " + paths.Length + " 个正式动作文档。";
            _undo.Clear();
            _redo.Clear();
            _revision++;
        }

        private void CaptureWorkspaceBaseline()
        {
            _workspaceBaselineCanonicalByPath.Clear();
            for (int i = 0; _documents != null && i < _documents.DocumentStates.Count; i++)
            {
                CharacterActionAuthoringDocumentState state = _documents.DocumentStates[i];
                if (state == null)
                    continue;
                _workspaceBaselineCanonicalByPath[state.Key.SourcePath] =
                    CharacterActionAuthoringDocumentState.CanonicalizeJson(
                        state.SerializeCurrentState());
            }
        }

        private bool IsWorkspaceDocumentDirty(CharacterActionAuthoringDocumentState state)
        {
            if (state == null)
                return false;
            string path = state.Key.SourcePath ?? string.Empty;
            if (!_workspaceBaselineCanonicalByPath.TryGetValue(path, out string baseline))
                return state.IsDirty;
            string current = CharacterActionAuthoringDocumentState.CanonicalizeJson(
                state.SerializeCurrentState());
            return !string.Equals(baseline, current, StringComparison.Ordinal);
        }

        private void Validate()
        {
            _model.Normalize();
            CharacterActionAuthoringModel validationModel = BuildProfileValidationModel();
            _validation = CharacterActionAuthoringJsonService.Validate(validationModel);
        }

        private CharacterActionAuthoringModel BuildProfileValidationModel()
        {
            SyncModelToCurrentLibrary();
            var result = new CharacterActionAuthoringModel
            {
                Profile = Clone(_documents.CurrentProfile),
                ActionSet = Clone(_documents.CurrentActionSet),
                ReferenceContext = _model?.ReferenceContext ?? CharacterActionAuthoringReferenceContext.CreateDefault(),
            };
            var seen = new HashSet<int>();
            for (int libraryIndex = 0; libraryIndex < _documents.ActionLibraries.Count; libraryIndex++)
            {
                CharacterActionJsonDto[] actions = _documents.ActionLibraries[libraryIndex]
                    .Document.Actions ?? Array.Empty<CharacterActionJsonDto>();
                for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
                {
                    CharacterActionJsonDto action = actions[actionIndex];
                    if (action != null && action.ActionId > 0 && seen.Add(action.ActionId))
                        result.Actions.Add(Clone(action));
                }
            }
            CharacterActionAuthoringActionSetBundleDocument bundle =
                _documents.FindActionSetBundle(_documents.Selection.ActionSetStableId);
            CharacterReactionProfileJsonDto[] reactions =
                bundle?.ReactionProfilesDocument.ReactionProfiles ?? Array.Empty<CharacterReactionProfileJsonDto>();
            for (int i = 0; i < reactions.Length; i++)
                result.ReactionProfiles.Add(Clone(reactions[i]));
            CharacterLandingProfileJsonDto[] landings =
                bundle?.LandingProfilesDocument.LandingProfiles ?? Array.Empty<CharacterLandingProfileJsonDto>();
            for (int i = 0; i < landings.Length; i++)
                result.LandingProfiles.Add(Clone(landings[i]));
            result.SelectedActionIndex = Math.Max(
                0,
                result.Actions.FindIndex(action => action?.ActionId == _documents.Selection.ActionId));
            result.Normalize();
            return result;
        }

        private void ValidateDiskHashesBeforeSave()
        {
            for (int i = 0; i < _documents.DocumentStates.Count; i++)
            {
                CharacterActionAuthoringDocumentState state = _documents.DocumentStates[i];
                if (!state.IsDirty)
                    continue;
                string path = state.Key.SourcePath;
                string currentCanonical = File.Exists(path)
                    ? CharacterActionAuthoringDocumentState.CanonicalizeJson(File.ReadAllText(path))
                    : string.Empty;
                string currentHash = ComputeCanonicalSha256(currentCanonical);
                if (!string.Equals(currentHash, state.BaselineSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "正式动作源已在磁盘上变化，拒绝覆盖：" + path);
                }
            }
        }

        private static string ComputeCanonicalSha256(string canonicalJson)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonicalJson ?? string.Empty));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private void LoadModelFromCurrentLibrary(bool normalize = true)
        {
            CharacterActionAuthoringActionLibraryDocument library = _documents.CurrentActionLibrary
                ?? throw new InvalidOperationException("当前 Profile 没有可用动作库。");
            _model = new CharacterActionAuthoringModel
            {
                Profile = Clone(_documents.CurrentProfile),
                ActionSet = Clone(_documents.CurrentActionSet),
                ReferenceContext = CharacterActionAuthoringReferenceContext.CreateDefault(),
            };
            CharacterActionJsonDto[] actions = library.Document.Actions ?? Array.Empty<CharacterActionJsonDto>();
            for (int i = 0; i < actions.Length; i++)
                _model.Actions.Add(actions[i]);
            CharacterActionAuthoringActionSetBundleDocument bundle =
                _documents.FindActionSetBundle(_documents.Selection.ActionSetStableId);
            CharacterReactionProfileJsonDto[] reactions =
                bundle?.ReactionProfilesDocument.ReactionProfiles ?? Array.Empty<CharacterReactionProfileJsonDto>();
            for (int i = 0; i < reactions.Length; i++)
                _model.ReactionProfiles.Add(Clone(reactions[i]));
            _model.SelectedActionIndex = Math.Max(
                0,
                _model.Actions.FindIndex(action =>
                    action?.ActionId == _documents.Selection.ActionId));
            if (normalize)
                _model.Normalize();
        }

        private void SyncModelToCurrentLibrary()
        {
            if (_model == null || _documents?.CurrentActionLibrary == null)
                return;
            _documents.CurrentActionLibrary.Document.Actions = _model.Actions
                .ToArray();
            _documents.RefreshActionIndex();
            if (_model.SelectedAction != null)
                _documents.SelectAction(_model.SelectedAction.ActionId);
        }

        private WorkspaceState CaptureState()
        {
            SyncModelToCurrentLibrary();
            ITimelineItemAdapter selectedAdapter = FindAdapter(
                _documents.Selection.TimelineItemStableId);
            int selectedItemIndex = -1;
            selectedAdapter?.TryGetCurrentIndex(out selectedItemIndex);
            Dictionary<string, CharacterActionJsonDto[]> libraryActions =
                _documents.ActionLibraries.ToDictionary(
                    library => library.LibraryId,
                    library => (library.Document.Actions ?? Array.Empty<CharacterActionJsonDto>())
                        .ToArray(),
                    StringComparer.Ordinal);
            string currentLibraryId = _documents.Selection.ActionLibraryId;
            if (libraryActions.TryGetValue(
                    currentLibraryId,
                    out CharacterActionJsonDto[] currentActions))
            {
                int selectedActionIndex = Array.FindIndex(
                    currentActions,
                    action => action?.ActionId == _documents.Selection.ActionId);
                if (selectedActionIndex >= 0)
                    currentActions[selectedActionIndex] = Clone(currentActions[selectedActionIndex]);
            }
            return new WorkspaceState
            {
                LibraryActions = libraryActions,
                ActionId = _documents.Selection.ActionId,
                TimelineItemId = _documents.Selection.TimelineItemStableId,
                TimelineTrackKind = selectedAdapter?.TrackKind,
                TimelineItemIndex = selectedItemIndex,
                Validation = _validation,
            };
        }

        private void RestoreState(WorkspaceState state)
        {
            foreach (CharacterActionAuthoringActionLibraryDocument library in _documents.ActionLibraries)
            {
                if (state.LibraryActions.TryGetValue(
                        library.LibraryId,
                        out CharacterActionJsonDto[] actions))
                {
                    library.Document.Actions = actions;
                }
            }
            _documents.RefreshActionIndex();
            _documents.SelectAction(state.ActionId);
            LoadModelFromCurrentLibrary(normalize: false);
            string timelineItemId = state.TimelineItemId;
            if (state.TimelineTrackKind.HasValue && state.TimelineItemIndex >= 0)
            {
                CharacterActionTimelineSnapshot timeline =
                    CharacterActionTimelineSnapshot.FromAction(_model.SelectedAction, 8);
                IReadOnlyList<ITimelineItemAdapter> items =
                    timeline.ItemsByTrack.TryGetValue(
                        state.TimelineTrackKind.Value,
                        out IReadOnlyList<ITimelineItemAdapter> found)
                        ? found
                        : Array.Empty<ITimelineItemAdapter>();
                ITimelineItemAdapter matching = items.FirstOrDefault(item =>
                    item.TryGetCurrentIndex(out int index)
                    && index == state.TimelineItemIndex);
                if (matching != null)
                    timelineItemId = matching.Id;
            }
            _documents.SelectTimelineItem(timelineItemId);
            if (state.Validation != null)
                _validation = state.Validation;
            else
                Validate();
            _revision++;
        }

        private void PushUndo()
        {
            _undo.Add(CaptureState());
            if (_undo.Count > MaxHistory)
                _undo.RemoveAt(0);
        }

        private ITimelineItemAdapter FindAdapter(string itemId)
        {
            CharacterActionJsonDto action = _model.SelectedAction;
            if (action == null)
                return null;
            return CharacterActionTimelineSnapshot.FromAction(action, 8).FindItem(itemId);
        }

        private CharacterActionAuthoringInspectorFieldDescriptor[] BuildInspectorFields(
            CharacterActionJsonDto action,
            CharacterActionTimelineSnapshot timeline,
            string selectedItemId)
        {
            if (action == null)
                return Array.Empty<CharacterActionAuthoringInspectorFieldDescriptor>();
            JObject root = JObject.FromObject(action, JsonSerializer.Create(
                CharacterActionAuthoringJsonService.JsonSettings));
            JToken target = root;
            string prefix = string.Empty;
            ITimelineItemAdapter adapter = timeline?.FindItem(selectedItemId);
            if (adapter != null && adapter.TryGetCurrentIndex(out int index))
            {
                prefix = adapter.TrackKind switch
                {
                    TimelineTrackKind.Phases => "phases[" + index + "]",
                    TimelineTrackKind.TransitionWindow => "cancelRules[" + index + "]",
                    TimelineTrackKind.Motion => "motionTrack.events[" + index + "]",
                    TimelineTrackKind.MovementControl => "motionTrack.movementControlKeys[" + index + "]",
                    TimelineTrackKind.RotationControl => "motionTrack.rotationControlKeys[" + index + "]",
                    TimelineTrackKind.Animation => "animationTrack.events[" + index + "]",
                    TimelineTrackKind.Combat => "combatTrack.events[" + index + "]",
                    TimelineTrackKind.Gameplay => "gameplayTrack.events[" + index + "]",
                    TimelineTrackKind.Presentation => "presentationTrack.events[" + index + "]",
                    _ => string.Empty,
                };
                target = prefix.Length == 0 ? root : root.SelectToken(prefix) ?? root;
            }
            else
            {
                string[] topLevel =
                {
                    "id", "stableId", "displayName", "category", "timelineAuthority",
                    "priority", "durationFrames", "tags", "requirements",
                };
                var fields = new List<CharacterActionAuthoringInspectorFieldDescriptor>();
                for (int i = 0; i < topLevel.Length; i++)
                {
                    JToken token = root[topLevel[i]];
                    if (token != null)
                        Flatten(fields, token, topLevel[i], timeline?.ReadOnly != true);
                }
                return fields.ToArray();
            }

            var result = new List<CharacterActionAuthoringInspectorFieldDescriptor>();
            Flatten(result, target, prefix, adapter?.CanEdit == true);
            return result.ToArray();
        }

        private void Flatten(
            List<CharacterActionAuthoringInspectorFieldDescriptor> fields,
            JToken token,
            string path,
            bool editable)
        {
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties())
                    Flatten(fields, property.Value, Join(path, property.Name), editable);
                return;
            }
            if (token is JArray array)
            {
                fields.Add(new CharacterActionAuthoringInspectorFieldDescriptor
                {
                    Path = path,
                    Label = path.Split('.').Last(),
                    Value = array.Count.ToString(CultureInfo.InvariantCulture) + " items",
                    Kind = CharacterActionAuthoringInspectorFieldKind.Array,
                    Editable = editable,
                });
                for (int i = 0; i < array.Count; i++)
                    Flatten(fields, array[i], path + "[" + i + "]", editable);
                return;
            }
            if (token is not JValue value)
                return;
            var descriptor = new CharacterActionAuthoringInspectorFieldDescriptor
            {
                Path = path,
                Label = path.Split('.').Last(),
                Value = Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                Kind = InferFieldKind(path, value.Type),
                Editable = editable && !IsIdentityReadOnly(path),
            };
            ConfigureInspectorField(descriptor, path, value.Type);
            fields.Add(descriptor);
        }

        private void ConfigureInspectorField(
            CharacterActionAuthoringInspectorFieldDescriptor field,
            string path,
            JTokenType tokenType)
        {
            string leaf = InspectorLeafName(path);
            if (IsIdentityReadOnly(path)
                || string.Equals(leaf, "traceProfileId", StringComparison.Ordinal))
            {
                field.Kind = CharacterActionAuthoringInspectorFieldKind.ReadOnly;
                field.Editable = false;
                return;
            }

            if (TryResolveInspectorEnum(path, leaf, out Type enumType, out string[] allowedValues))
            {
                SetEnumChoices(field, enumType, allowedValues);
                if (leaf.StartsWith("targetTypes[", StringComparison.Ordinal))
                    field.Kind = CharacterActionAuthoringInspectorFieldKind.Enum;
                return;
            }

            CharacterActionAuthoringReferenceContext references = _model?.ReferenceContext;
            switch (leaf)
            {
                case "combatActionId":
                    SetReferenceChoices(field, references?.CombatActionIds?.Select(id => CharacterActionAuthoringIds.Label(id, 412)), "未设置");
                    return;
                case "equipmentSlotId":
                    SetReferenceChoices(field, references?.EquipmentSlotIds?.Select(id => CharacterActionAuthoringIds.Label(id, 405)), "未设置");
                    return;
                case "animationActionKey":
                case "animationClipResourceKey":
                    SetReferenceChoices(field, references?.AnimationActionKeys, "未设置");
                    return;
                case "resourceKey":
                    SetReferenceChoices(field, references?.PresentationResourceKeys, "未设置");
                    return;
                case "cueId":
                    SetReferenceChoices(field, references?.AudioCueIds?.Select(id => CharacterActionAuthoringIds.Label(id, 401)), "未设置");
                    return;
                case "characterStableId":
                    SetReferenceChoices(field, references?.CharacterStableIds, "未设置");
                    return;
                case "equipmentStateStableId":
                    SetReferenceChoices(field, references?.EquipmentStateStableIds, "未设置");
                    return;
                case "equipmentTag":
                    SetReferenceChoices(field, references?.EquipmentTags, "未设置");
                    return;
                case "damageTypeId":
                {
                    List<(string Value, string Label)> choices =
                        CharacterActionDamageAuthoringText.BuildDamageTypeChoices(field.Value);
                    SetChoices(
                        field,
                        CharacterActionAuthoringInspectorFieldKind.Reference,
                        choices.Select(choice => choice.Value),
                        choices.Select(choice => choice.Label));
                    return;
                }
                case "formulaId":
                    SetChoices(
                        field,
                        CharacterActionAuthoringInspectorFieldKind.Reference,
                        new[]
                        {
                            DamageFormulaIds.Flat,
                            DamageFormulaIds.AttackMinusDefense,
                            DamageFormulaIds.AttackScaledMinusDefense,
                            DamageFormulaIds.CharacterTestSoftDefenseV1,
                        }.Select(id => CharacterActionAuthoringIds.Label(id, 410)),
                        new[]
                        {
                            "固定值 (Flat)",
                            "攻击减防御 (AttackMinusDefense)",
                            "攻击倍率减防御 (AttackScaledMinusDefense)",
                            "CharacterTest 软防御 (ct.soft_defense_v1)",
                        });
                    return;
                case "attributeId":
                    SetChoices(
                        field,
                        CharacterActionAuthoringInspectorFieldKind.Reference,
                        new[] { "2", DamageFormulaIds.CharacterTestDefenseAttributeId.ToString(CultureInfo.InvariantCulture) },
                        new[] { "攻击力 (2)", "防御力 (3)" });
                    return;
            }

            if (tokenType == JTokenType.Boolean)
            {
                SetChoices(
                    field,
                    CharacterActionAuthoringInspectorFieldKind.Toggle,
                    new[] { "true", "false" },
                    new[] { "是", "否" });
            }
        }

        private static bool TryResolveInspectorEnum(
            string path,
            string leaf,
            out Type enumType,
            out string[] allowedValues)
        {
            enumType = null;
            allowedValues = null;
            if (string.Equals(leaf, "category", StringComparison.Ordinal))
                enumType = typeof(CharacterActionCategory);
            else if (string.Equals(leaf, "timelineAuthority", StringComparison.Ordinal)
                || string.Equals(leaf, "timelineAuthoringMode", StringComparison.Ordinal))
                enumType = typeof(CharacterActionTimelineAuthority);
            else if (string.Equals(leaf, "environmentHitPolicy", StringComparison.Ordinal))
                enumType = typeof(CombatEnvironmentHitPolicy);
            else if (string.Equals(leaf, "hitSameTargetPolicy", StringComparison.Ordinal))
                enumType = typeof(CombatHitSameTargetPolicy);
            else if (leaf.StartsWith("targetTypes[", StringComparison.Ordinal))
            {
                enumType = typeof(CombatHitTargetTypes);
                allowedValues = new[]
                {
                    nameof(CombatHitTargetTypes.HostileCharacter),
                    nameof(CombatHitTargetTypes.FriendlyCharacter),
                    nameof(CombatHitTargetTypes.Self),
                    nameof(CombatHitTargetTypes.ThirdPartyCharacter),
                    nameof(CombatHitTargetTypes.DamageableObject),
                    nameof(CombatHitTargetTypes.WorldWall),
                    nameof(CombatHitTargetTypes.WorldGround),
                    nameof(CombatHitTargetTypes.WorldStatic),
                };
            }
            else if (string.Equals(leaf, "kind", StringComparison.Ordinal))
            {
                if (path.StartsWith("phases[", StringComparison.Ordinal))
                    enumType = typeof(CharacterActionPhaseKind);
                else if (path.StartsWith("requirements[", StringComparison.Ordinal))
                    enumType = typeof(CharacterActionRequirementKind);
                else
                {
                    enumType = typeof(CharacterActionTrackEventKind);
                    allowedValues = ResolveTrackEventKindOptions(path);
                }
            }
            return enumType != null;
        }

        private static string[] ResolveTrackEventKindOptions(string path)
        {
            if (path.StartsWith("motionTrack.events[", StringComparison.Ordinal))
            {
                return new[]
                {
                    nameof(CharacterActionTrackEventKind.SetMovementMode),
                    nameof(CharacterActionTrackEventKind.ApplyImpulse),
                    nameof(CharacterActionTrackEventKind.LockMovement),
                    nameof(CharacterActionTrackEventKind.SetCollisionProfile),
                    nameof(CharacterActionTrackEventKind.SetVerticalVelocity),
                    nameof(CharacterActionTrackEventKind.IgnoreGravity),
                    nameof(CharacterActionTrackEventKind.SampleRootMotionY),
                };
            }
            if (path.StartsWith("animationTrack.events[", StringComparison.Ordinal))
            {
                return new[]
                {
                    nameof(CharacterActionTrackEventKind.PlayAnimation),
                    nameof(CharacterActionTrackEventKind.CrossFadeAnimation),
                    nameof(CharacterActionTrackEventKind.SetAnimationBlend),
                };
            }
            if (path.StartsWith("combatTrack.events[", StringComparison.Ordinal))
            {
                return new[]
                {
                    nameof(CharacterActionTrackEventKind.StartCombatAction),
                    nameof(CharacterActionTrackEventKind.HitTraceWindow),
                };
            }
            if (path.StartsWith("gameplayTrack.events[", StringComparison.Ordinal))
            {
                return new[]
                {
                    nameof(CharacterActionTrackEventKind.SendGameplayRequest),
                    nameof(CharacterActionTrackEventKind.CastAbility),
                    nameof(CharacterActionTrackEventKind.ApplyGameplayEffect),
                    nameof(CharacterActionTrackEventKind.StartEffectTimeline),
                };
            }
            if (path.StartsWith("presentationTrack.events[", StringComparison.Ordinal))
            {
                return new[]
                {
                    nameof(CharacterActionTrackEventKind.PlayAudioCue),
                    nameof(CharacterActionTrackEventKind.SpawnVisualCue),
                    nameof(CharacterActionTrackEventKind.CameraImpulse),
                    nameof(CharacterActionTrackEventKind.UiFeedback),
                };
            }
            return Enum.GetNames(typeof(CharacterActionTrackEventKind));
        }

        private static void SetEnumChoices(
            CharacterActionAuthoringInspectorFieldDescriptor field,
            Type enumType,
            string[] allowedValues)
        {
            string[] values = allowedValues ?? Enum.GetNames(enumType);
            SetChoices(
                field,
                enumType.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0
                    ? CharacterActionAuthoringInspectorFieldKind.Flags
                    : CharacterActionAuthoringInspectorFieldKind.Enum,
                values,
                values.Select(value => CharacterActionAuthoringText.EnumDisplayLabel(enumType, value)));
        }

        private static void SetReferenceChoices(
            CharacterActionAuthoringInspectorFieldDescriptor field,
            IEnumerable<string> source,
            string emptyLabel)
        {
            var values = new List<string> { string.Empty };
            if (source != null)
            {
                values.AddRange(source
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal));
            }
            SetChoices(
                field,
                CharacterActionAuthoringInspectorFieldKind.Reference,
                values,
                values.Select(value => value.Length == 0 ? emptyLabel : value));
        }

        private static void SetChoices(
            CharacterActionAuthoringInspectorFieldDescriptor field,
            CharacterActionAuthoringInspectorFieldKind kind,
            IEnumerable<string> values,
            IEnumerable<string> labels)
        {
            var optionValues = values?.ToList() ?? new List<string>();
            var optionLabels = labels?.ToList() ?? new List<string>();
            while (optionLabels.Count < optionValues.Count)
                optionLabels.Add(optionValues[optionLabels.Count]);
            if (!optionValues.Contains(field.Value, StringComparer.Ordinal))
            {
                optionValues.Add(field.Value ?? string.Empty);
                optionLabels.Add(string.IsNullOrWhiteSpace(field.Value)
                    ? "未设置"
                    : "保留旧值：" + field.Value);
            }
            field.Kind = kind;
            field.Options = optionValues.ToArray();
            field.OptionLabels = optionLabels.ToArray();
        }

        private static string InspectorLeafName(string path)
        {
            string leaf = path ?? string.Empty;
            int dot = leaf.LastIndexOf('.');
            return dot >= 0 ? leaf.Substring(dot + 1) : leaf;
        }

        private int CountProblems(int actionId)
        {
            if (actionId <= 0 || string.IsNullOrWhiteSpace(_validation?.DiagnosticsText))
                return 0;
            string token = actionId.ToString(CultureInfo.InvariantCulture);
            return _validation.DiagnosticsText.Split('\n')
                .Count(line => line.IndexOf(token, StringComparison.Ordinal) >= 0);
        }

        private int AllocateActionId()
        {
            CharacterActionAuthoringActionLibraryDocument library = _documents.CurrentActionLibrary
                ?? throw new InvalidOperationException("当前没有可用的 CharacterAction Library。");
            return CharacterActionIdCatalog.Allocate(
                library.LibraryId,
                library.Source.ActionsPath);
        }

        private static CharacterActionAuthoringInspectorFieldKind InferFieldKind(
            string path,
            JTokenType type)
        {
            if (path.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                || path.IndexOf("resource", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CharacterActionAuthoringInspectorFieldKind.Reference;
            }
            return type switch
            {
                JTokenType.Boolean => CharacterActionAuthoringInspectorFieldKind.Toggle,
                JTokenType.Integer => CharacterActionAuthoringInspectorFieldKind.Integer,
                JTokenType.Float => CharacterActionAuthoringInspectorFieldKind.Float,
                _ => CharacterActionAuthoringInspectorFieldKind.Text,
            };
        }

        private static bool IsIdentityReadOnly(string path) =>
            string.Equals(path, "id", StringComparison.Ordinal)
            || path.EndsWith("stableEventId", StringComparison.OrdinalIgnoreCase);

        private static object ConvertValue(string text, JTokenType type)
        {
            return type switch
            {
                JTokenType.Boolean => bool.Parse(text),
                JTokenType.Integer => long.Parse(text, CultureInfo.InvariantCulture),
                JTokenType.Float => double.Parse(text, CultureInfo.InvariantCulture),
                JTokenType.Null => text,
                _ => text ?? string.Empty,
            };
        }

        private void MutateArray(string path, bool add)
        {
            CharacterActionJsonDto action = _model.SelectedAction
                ?? throw new InvalidOperationException("当前没有选中动作。");
            JObject root = JObject.FromObject(action, JsonSerializer.Create(
                CharacterActionAuthoringJsonService.JsonSettings));
            JArray array = root.SelectToken(path, errorWhenNoMatch: false) as JArray
                ?? throw new InvalidOperationException("数组字段不存在：" + path);
            if (add)
                array.Add(CreateDefaultArrayElement(path, array));
            else if (array.Count > 0)
                array.RemoveAt(array.Count - 1);
            CharacterActionJsonDto replacement = root.ToObject<CharacterActionJsonDto>(
                JsonSerializer.Create(CharacterActionAuthoringJsonService.JsonSettings));
            _model.Actions[_model.SelectedActionIndex] =
                CharacterActionAuthoringModel.NormalizeAction(replacement);
        }

        private static JToken CreateDefaultArrayElement(string path, JArray array)
        {
            if (array.Count > 0)
            {
                return array[0].Type == JTokenType.String
                    ? new JValue(string.Empty)
                    : new JObject();
            }
            if (path.EndsWith("tags", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("requiredTags", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("forbiddenTags", StringComparison.OrdinalIgnoreCase))
            {
                return new JValue(string.Empty);
            }
            if (path.EndsWith("requirements", StringComparison.OrdinalIgnoreCase))
            {
                return JObject.FromObject(new CharacterActionRequirementJsonDto
                {
                    Kind = CharacterActionRequirementKind.Grounded.ToString(),
                });
            }
            return new JObject();
        }

        private static string[] SplitProblems(string text) => string.IsNullOrWhiteSpace(text)
            ? Array.Empty<string>()
            : text.Replace("\r", string.Empty).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        private static string Join(string prefix, string name) =>
            string.IsNullOrEmpty(prefix) ? name : prefix + "." + name;

        private static uint PackColor(UnityEngine.Color color)
        {
            UnityEngine.Color32 c = color;
            return (uint)(c.r << 24 | c.g << 16 | c.b << 8 | c.a);
        }

        private static T Clone<T>(T value)
        {
            if (value == null)
                return default;
            return JsonConvert.DeserializeObject<T>(
                JsonConvert.SerializeObject(value, CharacterActionAuthoringJsonService.JsonSettings),
                CharacterActionAuthoringJsonService.JsonSettings);
        }

        private static T[] Append<T>(T[] source, T item)
        {
            source ??= Array.Empty<T>();
            var result = new T[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[^1] = item;
            return result;
        }

        private sealed class WorkspaceState
        {
            internal Dictionary<string, CharacterActionJsonDto[]> LibraryActions =
                new Dictionary<string, CharacterActionJsonDto[]>();
            internal int ActionId;
            internal string TimelineItemId = string.Empty;
            internal TimelineTrackKind? TimelineTrackKind;
            internal int TimelineItemIndex = -1;
            internal CharacterActionAuthoringExportResult Validation;
        }
    }
}
