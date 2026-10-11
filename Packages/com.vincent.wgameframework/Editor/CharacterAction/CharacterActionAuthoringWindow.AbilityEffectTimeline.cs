using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using MxFramework.Gameplay;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private const int AbilityEffectTimelineMaxChildDepth = 8;

        private void BuildAbilityEffectTimelineMode(VisualElement root)
        {
            EnsureAbilityEffectTimelinesLoaded();

            if (_abilityEffectTimelines == null)
            {
                root.Add(new HelpBox("No ability_authoring.json loaded.", HelpBoxMessageType.Info));
                root.Add(new Button(LoadAbilityEffectTimelinesFromCurrentPath) { text = "Load ability_authoring.json" });
                return;
            }

            VisualElement summary = AddCard(root, "Ability Effect Timelines");
            AddAbilityEffectTimelineValidation(summary);

            AbilityEffectTimelineDraft selected = GetSelectedAbilityEffectTimeline();
            if (selected == null)
            {
                root.Add(new HelpBox("Create or select an effect timeline.", HelpBoxMessageType.Info));
                root.Add(new Button(AddAbilityEffectTimeline) { text = "New Timeline" });
                return;
            }

            VisualElement timelineViewCard = AddCard(root, "Timeline View");
            AddAbilityEffectTimelineTimelineView(timelineViewCard, selected);

            VisualElement timelineCard = AddCard(root, "Timeline");
            AddAbilityTextField(timelineCard, "Timeline ID", selected.TimelineId, value =>
            {
                string oldId = selected.TimelineId;
                selected.TimelineId = value;
                if (string.Equals(_selectedAbilityEffectTimelineId, oldId, StringComparison.Ordinal))
                    _selectedAbilityEffectTimelineId = value;
            });
            AddAbilityIntegerField(timelineCard, "Duration Frames", selected.DurationFrames, value =>
                selected.DurationFrames = Math.Max(1, value));

            VisualElement eventsCard = AddCard(root, "Events");
            if (selected.Events.Count == 0)
            {
                eventsCard.Add(new Label("No events."));
                return;
            }

            AbilityEffectTimelineEventDraft selectedEvent = GetSelectedAbilityEffectTimelineEvent(selected);
            if (selectedEvent != null)
            {
                int selectedIndex = selected.Events.IndexOf(selectedEvent);
                AddAbilityEffectTimelineEventEditor(eventsCard, selected, selectedEvent, selectedIndex);
                return;
            }

            eventsCard.Add(new Label("Select an event on the timeline to edit one item. Showing all events:"));
            for (int i = 0; i < selected.Events.Count; i++)
                AddAbilityEffectTimelineEventEditor(eventsCard, selected, selected.Events[i], i);
        }

        private void AddAbilityEffectTimelineTimelineView(VisualElement parent, AbilityEffectTimelineDraft selected)
        {
            if (_abilityEffectTimelineView == null)
            {
                _abilityEffectTimelineView = new CharacterActionTimelineView();
                _abilityEffectTimelineView.style.flexGrow = 1f;
                _abilityEffectTimelineView.style.minHeight = 260f;
            }
            _abilityEffectTimelineView.RemoveFromHierarchy();
            parent.Add(_abilityEffectTimelineView);

            ConfigureAbilityEffectTimelineHost(selected);
            EnsureTimelinePlayback();
            int savedFrame = _timelinePlayback?.CurrentFrame ?? 0;

            _timelineView?.SetPlaybackController(null);
            _abilityEffectTimelineView.SetPlaybackController(_timelinePlayback);
            _abilityEffectTimelineView.BindAbilityEffectTimeline(selected);

            if (_timelinePlayback != null)
            {
                _timelinePlayback.SetDuration(Math.Max(1, selected.DurationFrames));
                int maxFrame = Math.Max(0, selected.DurationFrames - 1);
                _timelinePlayback.Seek(Math.Min(savedFrame, maxFrame));
            }

            if (!string.IsNullOrEmpty(_pendingAbilityEffectTimelineSelectedItemId)
                && _abilityEffectTimelineView.Snapshot?.FindItem(
                    _pendingAbilityEffectTimelineSelectedItemId) != null)
            {
                _isRestoringAbilityTimelineSelection = true;
                try
                {
                    _abilityEffectTimelineView.SelectItem(
                        _pendingAbilityEffectTimelineSelectedItemId);
                }
                finally
                {
                    _isRestoringAbilityTimelineSelection = false;
                }
            }
        }

        private void ConfigureAbilityEffectTimelineHost(AbilityEffectTimelineDraft selected)
        {
            if (_abilityEffectTimelineView == null)
                return;

            var host = new CharacterActionTimelineHost
            {
                DefaultDurationFrames = DefaultDurationFrames,
                SaveRequested = SaveAllDirtyCharacterActionSources,
                CanSaveProvider = IsAnyDirty,
                DirtyStateProvider = BuildCharacterActionTimelineDirtyText,
                CurrentItemLabelProvider = BuildAbilityEffectTimelinePickerLabel,
                CurrentItemIdProvider = () => _selectedAbilityEffectTimelineId ?? string.Empty,
                PickerItemsProvider = BuildAbilityEffectTimelinePickerItems,
                PickerItemSelected = SelectAbilityEffectTimelinePickerItem,
                PickerFavoriteChanged = SetAbilityEffectTimelinePickerFavorite,
                PickerRecentRecorded = RecordAbilityEffectTimelinePickerRecent,
                PickerFooterBuilder = BuildAbilityEffectTimelinePickerFooter,
                AddMenuProvider = (menu, frame) => PopulateAbilityEffectTimelineAddMenu(menu, selected, frame),
                MoreMenuProvider = menu => PopulateAbilityEffectTimelineMoreMenu(menu, selected),
                CommitFieldChange = (mutate, recordUndo) =>
                {
                    if (recordUndo)
                    {
                        PushUndoSnapshot(restoreAuthoringFilesOnApply: false);
                        _redoHistory.Clear();
                    }
                    mutate?.Invoke();
                    NormalizeAbilityEffectTimelineDrafts();
                    MarkAbilityEffectTimelineDraftDirty();
                    _abilityEffectTimelineView?.RefreshCommandState();
                    RefreshUi(UiRefreshScope.Chrome);
                },
                CommitStructuralChange = mutate =>
                {
                    PushUndoSnapshot(restoreAuthoringFilesOnApply: false);
                    _redoHistory.Clear();
                    mutate?.Invoke();
                    NormalizeAbilityEffectTimelineDrafts();
                    MarkAbilityEffectTimelineDraftDirty();
                    RefreshUi(UiRefreshScope.Content);
                },
                SelectionChanged = itemId =>
                {
                    _pendingAbilityEffectTimelineSelectedItemId = itemId ?? string.Empty;
                    SaveSessionState();
                    if (_isRestoringAbilityTimelineSelection)
                        return;
                    RefreshUi(UiRefreshScope.Content);
                },
                RequestTracksRefresh = () => RefreshUi(UiRefreshScope.Content),
                AddEventAtTrack = (trackKind, frame) =>
                    AddAbilityEffectTimelineEventAtTrack(selected, trackKind, frame),
                DurationFramesChanged = frames =>
                    ApplyAbilityEffectTimelineStructuralChange(() =>
                        selected.DurationFrames = Math.Max(1, frames)),
                BeginGesture = () =>
                {
                    PushUndoSnapshot(restoreAuthoringFilesOnApply: false);
                    _redoHistory.Clear();
                },
                EndGesture = () =>
                {
                    NormalizeAbilityEffectTimelineDrafts();
                    MarkAbilityEffectTimelineDraftDirty();
                    RefreshUi(UiRefreshScope.Content);
                },
            };
            _abilityEffectTimelineView.SetHost(host);
        }

        private void DisposeAbilityEffectTimelineView()
        {
            if (_abilityEffectTimelineView == null)
                return;
            _abilityEffectTimelineView.SetPlaybackController(null);
            _abilityEffectTimelineView.RemoveFromHierarchy();
            _abilityEffectTimelineView = null;
        }

        private void PopulateAbilityEffectTimelineAddMenu(
            DropdownMenu menu,
            AbilityEffectTimelineDraft selected,
            int frame)
        {
            AppendAbilityEffectTimelineAddAction(menu, selected, "Combat/AreaPulse", AbilityEffectTimelineEventKind.AreaPulse, frame);
            AppendAbilityEffectTimelineAddAction(menu, selected, "Combat/ShapeQueryWindow", AbilityEffectTimelineEventKind.ShapeQueryWindow, frame);
            menu.AppendSeparator();
            AppendAbilityEffectTimelineAddAction(menu, selected, "Gameplay/ApplyEffect", AbilityEffectTimelineEventKind.ApplyEffect, frame);
            AppendAbilityEffectTimelineAddAction(menu, selected, "Gameplay/SpawnProjectile", AbilityEffectTimelineEventKind.SpawnProjectile, frame);
            AppendAbilityEffectTimelineAddAction(menu, selected, "Gameplay/StartEffectTimeline", AbilityEffectTimelineEventKind.StartEffectTimeline, frame);
            AppendAbilityEffectTimelineAddAction(menu, selected, "Gameplay/EmitGameplayEvent", AbilityEffectTimelineEventKind.EmitGameplayEvent, frame);
            menu.AppendSeparator();
            AppendAbilityEffectTimelineAddAction(menu, selected, "Presentation/SpawnVfx", AbilityEffectTimelineEventKind.SpawnVfx, frame);
            AppendAbilityEffectTimelineAddAction(menu, selected, "Presentation/PlayAudioCue", AbilityEffectTimelineEventKind.PlayAudioCue, frame);
            AppendAbilityEffectTimelineAddAction(menu, selected, "Presentation/CameraImpulse", AbilityEffectTimelineEventKind.CameraImpulse, frame);
            AppendAbilityEffectTimelineAddAction(menu, selected, "Presentation/UiFeedback", AbilityEffectTimelineEventKind.UiFeedback, frame);
        }

        private void AppendAbilityEffectTimelineAddAction(
            DropdownMenu menu,
            AbilityEffectTimelineDraft selected,
            string label,
            AbilityEffectTimelineEventKind kind,
            int frame)
        {
            menu.AppendAction(
                label,
                _ => AddAbilityEffectTimelineEventAtFrame(selected, kind, frame),
                _ => selected != null
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
        }

        private void PopulateAbilityEffectTimelineMoreMenu(
            DropdownMenu menu,
            AbilityEffectTimelineDraft selected)
        {
            menu.AppendAction("New Timeline", _ => AddAbilityEffectTimeline(), DropdownMenuAction.AlwaysEnabled);
            menu.AppendAction(
                "Delete Selected Timeline",
                _ => DeleteSelectedAbilityEffectTimeline(),
                _ => selected != null
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            menu.AppendSeparator();
            menu.AppendAction("New Document", _ => NewAbilityEffectTimelineDocument(), DropdownMenuAction.AlwaysEnabled);
            menu.AppendAction("Reload", _ => LoadAbilityEffectTimelinesFromCurrentPath(), DropdownMenuAction.AlwaysEnabled);
            menu.AppendAction(
                "Copy effectTimelines JSON",
                _ => CopyAbilityEffectTimelineJsonToClipboard(),
                _ => _abilityEffectTimelines != null
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            menu.AppendSeparator();
            menu.AppendAction(
                "Sort Events",
                _ => ApplyAbilityEffectTimelineStructuralChange(() =>
                    selected?.Events?.Sort(CompareAbilityTimelineEvents)),
                _ => selected != null
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
        }

        private void AddAbilityEffectTimelineHeader(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;

            List<string> ids = BuildAbilityEffectTimelineIdChoices();
            var popup = new PopupField<string>("Timeline", ids, SelectedAbilityEffectTimelinePopupIndex(ids));
            popup.style.minWidth = 320f;
            popup.RegisterValueChangedCallback(evt =>
            {
                _selectedAbilityEffectTimelineId = evt.newValue ?? string.Empty;
                SaveSessionState();
                RefreshUi(UiRefreshScope.Content);
            });
            row.Add(popup);

            row.Add(new Button(AddAbilityEffectTimeline) { text = "Add" });
            row.Add(new Button(DeleteSelectedAbilityEffectTimeline) { text = "Delete" });
            parent.Add(row);
        }

        private void AddAbilityEffectTimelineValidation(VisualElement parent)
        {
            List<string> diagnostics = ValidateAbilityEffectTimelineDrafts();
            var box = new VisualElement();
            box.style.marginTop = 4f;
            if (diagnostics.Count == 0)
            {
                box.Add(new Label("Validation: pass"));
            }
            else
            {
                box.Add(new Label("Validation: " + diagnostics.Count + " issue(s)"));
                for (int i = 0; i < diagnostics.Count; i++)
                    box.Add(new Label("- " + diagnostics[i]));
            }

            parent.Add(box);
        }

        private void AddAbilityEffectTimelineEventEditor(
            VisualElement parent,
            AbilityEffectTimelineDraft timeline,
            AbilityEffectTimelineEventDraft evt,
            int index)
        {
            string title = "#" + index + " f" + evt.Frame + " " + evt.TrackKind + "/" + evt.EventKind;
            VisualElement card = AddCard(parent, title);

            AddButtonRow(
                card,
                ("Duplicate", () => ApplyAbilityEffectTimelineStructuralChange(() =>
                    timeline.Events.Insert(index + 1, evt.Clone()))),
                ("Delete", () => ApplyAbilityEffectTimelineStructuralChange(() =>
                    timeline.Events.Remove(evt))));

            AddAbilityTextField(card, "Event ID", evt.EventId, value => evt.EventId = value);
            AddAbilityEnumPopup<AbilityEffectTimelineTrackKind>(card, "Track", evt.TrackKind, value => evt.TrackKind = value);
            AddAbilityEnumPopup<AbilityEffectTimelineEventKind>(card, "Event", evt.EventKind, value =>
            {
                evt.EventKind = value;
                evt.TrackKind = DefaultTrackKindForEffectEvent(value);
                ApplyDefaultsForAbilityEffectTimelineEvent(evt);
            });
            AddAbilityIntegerField(card, "Frame", evt.Frame, value => evt.Frame = Math.Max(0, value));
            AddAbilityIntegerField(card, "End Frame", evt.EndFrame, value => evt.EndFrame = value);
            AddAbilityEnumPopup<AbilityEffectTimelineShapeKind>(card, "Shape", evt.ShapeKind, value => evt.ShapeKind = value);
            AddAbilityEnumPopup<AbilityEffectTimelineAnchorMode>(card, "Anchor", evt.AnchorMode, value => evt.AnchorMode = value);
            AddAbilityVector3Fields(card, "Local Offset", evt.LocalOffset);
            AddAbilityVector3Fields(card, "Capsule Point B", evt.LocalPointB);
            AddAbilityFloatField(card, "Radius", evt.Radius, value => evt.Radius = Math.Max(0f, value));
            AddAbilityIntegerField(card, "Layer Mask", evt.LayerMask, value => evt.LayerMask = value);
            AddAbilityTextField(card, "Target Timeline ID", evt.TargetTimelineId, value => evt.TargetTimelineId = value);
            AddAbilityTextField(card, "Request ID", evt.RequestId, value => evt.RequestId = value);
            AddAbilityIntegerField(card, "Effect ID", evt.EffectId, value => evt.EffectId = value);
            AddAbilityTextField(card, "Presentation Profile ID", evt.PresentationProfileId, value =>
                evt.PresentationProfileId = value);
            AddAbilityToggle(card, "Requires Owner", evt.RequiresOwner, value => evt.RequiresOwner = value);
            AddAbilityToggle(card, "Requires Source", evt.RequiresSource, value => evt.RequiresSource = value);

            string preview = "Preview: " + evt.AnchorMode
                + " + offset(" + evt.LocalOffset.X + ", " + evt.LocalOffset.Y + ", " + evt.LocalOffset.Z + ")"
                + " | " + evt.ShapeKind
                + " r=" + evt.Radius
                + " layerMask=" + evt.LayerMask;
            card.Add(new Label(preview));
        }

        private void AddAbilityTextField(VisualElement parent, string label, string value, Action<string> setter)
        {
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true,
            };
            field.RegisterValueChangedCallback(evt => ApplyAbilityEffectTimelineFieldChange(() =>
                setter(evt.newValue ?? string.Empty)));
            parent.Add(field);
        }

        private void AddAbilityIntegerField(VisualElement parent, string label, int value, Action<int> setter)
        {
            var field = new IntegerField(label)
            {
                value = value,
                isDelayed = true,
            };
            field.RegisterValueChangedCallback(evt => ApplyAbilityEffectTimelineFieldChange(() => setter(evt.newValue)));
            parent.Add(field);
        }

        private void AddAbilityFloatField(VisualElement parent, string label, float value, Action<float> setter)
        {
            var field = new FloatField(label)
            {
                value = value,
                isDelayed = true,
            };
            field.RegisterValueChangedCallback(evt => ApplyAbilityEffectTimelineFieldChange(() => setter(evt.newValue)));
            parent.Add(field);
        }

        private void AddAbilityToggle(VisualElement parent, string label, bool value, Action<bool> setter)
        {
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => ApplyAbilityEffectTimelineFieldChange(() => setter(evt.newValue)));
            parent.Add(field);
        }

        private void AddAbilityVector3Fields(VisualElement parent, string label, AbilityEffectTimelineVector3Draft value)
        {
            value ??= new AbilityEffectTimelineVector3Draft();
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.Add(new Label(label));
            row.Add(CreateAbilityVectorComponentField("X", value.X, next => value.X = next));
            row.Add(CreateAbilityVectorComponentField("Y", value.Y, next => value.Y = next));
            row.Add(CreateAbilityVectorComponentField("Z", value.Z, next => value.Z = next));
            parent.Add(row);
        }

        private FloatField CreateAbilityVectorComponentField(string label, float value, Action<float> setter)
        {
            var field = new FloatField(label)
            {
                value = value,
                isDelayed = true,
            };
            field.style.width = 120f;
            field.RegisterValueChangedCallback(evt => ApplyAbilityEffectTimelineFieldChange(() => setter(evt.newValue)));
            return field;
        }

        private void AddAbilityEnumPopup<TEnum>(
            VisualElement parent,
            string label,
            string value,
            Action<string> setter)
            where TEnum : struct
        {
            string[] names = Enum.GetNames(typeof(TEnum));
            var choices = new List<string>(names);
            value = string.IsNullOrWhiteSpace(value) ? names[0] : value;
            int selected = choices.IndexOf(value);
            if (selected < 0)
            {
                choices.Insert(0, value);
                selected = 0;
            }

            var field = new PopupField<string>(label, choices, selected);
            field.RegisterValueChangedCallback(evt => ApplyAbilityEffectTimelineFieldChange(() =>
                setter(evt.newValue ?? string.Empty)));
            parent.Add(field);
        }

        private void ApplyAbilityEffectTimelineFieldChange(Action mutate)
        {
            PushUndoSnapshot(restoreAuthoringFilesOnApply: false);
            _redoHistory.Clear();
            mutate?.Invoke();
            NormalizeAbilityEffectTimelineDrafts();
            MarkAbilityEffectTimelineDraftDirty();
            RefreshUi(UiRefreshScope.Content);
        }

        private void ApplyAbilityEffectTimelineStructuralChange(Action mutate)
        {
            PushUndoSnapshot(restoreAuthoringFilesOnApply: false);
            _redoHistory.Clear();
            mutate?.Invoke();
            NormalizeAbilityEffectTimelineDrafts();
            MarkAbilityEffectTimelineDraftDirty();
            RefreshUi(UiRefreshScope.Content);
        }

        private void EnsureAbilityEffectTimelinesLoaded()
        {
            if (_abilityEffectTimelines != null)
                return;

            string absolute = AbsoluteProjectPath(_abilityAuthoringJsonPath);
            if (File.Exists(absolute))
                LoadAbilityEffectTimelinesFromCurrentPath(confirmDirty: false, refreshUi: false);
            else
                NewAbilityEffectTimelineDocument(confirmDirty: false);
        }

        private void BrowseAbilityAuthoringJson()
        {
            string defaultDirectory = Path.GetDirectoryName(AbsoluteProjectPath(_abilityAuthoringJsonPath));
            if (string.IsNullOrWhiteSpace(defaultDirectory) || !Directory.Exists(defaultDirectory))
                defaultDirectory = Directory.GetCurrentDirectory();

            string path = EditorUtility.OpenFilePanel("Open ability_authoring.json", defaultDirectory, "json");
            if (string.IsNullOrWhiteSpace(path))
                return;

            string targetPath = ProjectRelativePath(path);
            TrySwitchAbilityEffectTimelineSource(targetPath, confirmDirty: true, refreshUi: true);
        }

        private void NewAbilityEffectTimelineDocument()
        {
            NewAbilityEffectTimelineDocument(confirmDirty: true);
        }

        private void NewAbilityEffectTimelineDocument(bool confirmDirty)
        {
            if (confirmDirty
                && _abilityEffectTimelines != null
                && IsAnyDirty()
                && !ConfirmAbilityEffectTimelineSourceSwitch())
            {
                return;
            }

            var root = new JObject
            {
                ["schemaVersion"] = "1",
                ["packageId"] = string.Empty,
            };
            AdoptAbilityEffectTimelineDocument(
                _abilityAuthoringJsonPath,
                root,
                new List<AbilityEffectTimelineDraft>(),
                dirty: true,
                clearHistory: true);
            _selectedAbilityEffectTimelineId = string.Empty;
            MarkAbilityEffectTimelineDraftDirty();
            RefreshUi(UiRefreshScope.All);
        }

        private void LoadAbilityEffectTimelinesFromCurrentPath()
        {
            LoadAbilityEffectTimelinesFromCurrentPath(confirmDirty: true, refreshUi: true);
        }

        private bool LoadAbilityEffectTimelinesFromCurrentPath(bool confirmDirty, bool refreshUi)
        {
            return TrySwitchAbilityEffectTimelineSource(
                _abilityAuthoringJsonPath,
                confirmDirty,
                refreshUi);
        }

        private bool TrySwitchAbilityEffectTimelineSource(
            string targetPath,
            bool confirmDirty,
            bool refreshUi)
        {
            if (!TryStageAbilityEffectTimelineDocument(
                    targetPath,
                    out JObject stagedRoot,
                    out List<AbilityEffectTimelineDraft> stagedTimelines,
                    out string error))
            {
                ShowDocumentGraphSwitchError("Load ability_authoring.json", error);
                return false;
            }

            bool hadDirtyState = confirmDirty
                && _abilityEffectTimelines != null
                && IsAnyDirty();
            if (hadDirtyState && !ConfirmAbilityEffectTimelineSourceSwitch())
                return false;

            // Saving can change the current source after the first staging pass. Re-stage
            // every dirty-source transition so the adopted graph always reflects disk.
            if (hadDirtyState
                && !TryStageAbilityEffectTimelineDocument(
                    targetPath,
                    out stagedRoot,
                    out stagedTimelines,
                    out error))
            {
                ShowDocumentGraphSwitchError("Load ability_authoring.json", error);
                return false;
            }

            AdoptAbilityEffectTimelineDocument(
                targetPath,
                stagedRoot,
                stagedTimelines,
                dirty: false,
                clearHistory: true);
            if (refreshUi)
                RefreshUi(UiRefreshScope.All);
            return true;
        }

        private static bool TryStageAbilityEffectTimelineDocument(
            string sourcePath,
            out JObject root,
            out List<AbilityEffectTimelineDraft> timelines,
            out string error)
        {
            root = null;
            timelines = null;
            error = string.Empty;
            string absolute = AbsoluteProjectPath(sourcePath);
            if (!File.Exists(absolute))
            {
                error = "File not found:\n" + absolute;
                return false;
            }

            try
            {
                root = JObject.Parse(File.ReadAllText(absolute));
                JArray source = root["effectTimelines"] as JArray ?? new JArray();
                JsonSerializer serializer = JsonSerializer.Create(
                    CharacterActionAuthoringJsonService.JsonSettings);
                timelines = source.ToObject<List<AbilityEffectTimelineDraft>>(serializer)
                    ?? new List<AbilityEffectTimelineDraft>();
                NormalizeAbilityEffectTimelineDrafts(timelines);
                return true;
            }
            catch (Exception ex)
            {
                root = null;
                timelines = null;
                error = ex.Message;
                return false;
            }
        }

        private void AdoptAbilityEffectTimelineDocument(
            string sourcePath,
            JObject root,
            List<AbilityEffectTimelineDraft> timelines,
            bool dirty,
            bool clearHistory)
        {
            _abilityAuthoringJsonPath = sourcePath ?? string.Empty;
            _abilityAuthoringRoot = root ?? new JObject { ["schemaVersion"] = "1" };
            _abilityEffectTimelines = timelines ?? new List<AbilityEffectTimelineDraft>();
            NormalizeAbilityEffectTimelineDrafts();
            if (GetSelectedAbilityEffectTimeline() == null)
            {
                _selectedAbilityEffectTimelineId = _abilityEffectTimelines.Count > 0
                    ? _abilityEffectTimelines[0].TimelineId
                    : string.Empty;
            }
            _pendingAbilityEffectTimelineSelectedItemId = string.Empty;
            _abilityEffectTimelineDirty = dirty;
            ClearResourceDraftBaselinesForSession(
                CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline);
            if (clearHistory)
            {
                _undoHistory.Clear();
                _redoHistory.Clear();
            }
        }

        private bool ConfirmAbilityEffectTimelineSourceSwitch()
        {
            if (!IsAnyDirty())
                return true;
            if (_documentGraphSwitchConfirmationOverride != null)
                return _documentGraphSwitchConfirmationOverride();

            int choice = EditorUtility.DisplayDialogComplex(
                "存在未保存修改",
                "切换独立来源会替换当前会话草稿：\n" + CreateDirtyStatusText(),
                "保存并继续",
                "取消",
                "放弃修改");
            if (choice == 1)
                return false;
            if (choice == 0)
            {
                SaveAllDirtyCharacterActionSources();
                return !IsAnyDirty();
            }

            CharacterActionAuthoringDocumentSession stagedSession = null;
            if (_documentSession != null
                && !TryCreateDocumentSession(
                    _documentSession.ProfileDocument.Source,
                    _documentSession.Selection.ProfileStableId,
                    out stagedSession,
                    out string error))
            {
                EditorUtility.DisplayDialog("放弃修改失败", error, "OK");
                return false;
            }

            _pendingMechanicTuningDrafts.Clear();
            ClearResourceDraftBaselinesForSession(CharacterActionAuthoringResourceDraftKind.MechanicTuning);
            ClearResourceDraftBaselinesForSession(
                CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline);
            _abilityEffectTimelineDirty = false;
            _profileDirty = false;
            _actionSetDirty = false;
            _actionLibraryDirty = false;
            if (stagedSession != null)
                AdoptDocumentSession(stagedSession);
            _undoHistory.Clear();
            _redoHistory.Clear();
            return true;
        }

        private void SaveAbilityEffectTimelinesToCurrentPath()
        {
            SaveAbilityEffectTimelinesToCurrentPath(refreshUi: true);
        }

        private void SaveAbilityEffectTimelinesToCurrentPath(bool refreshUi)
        {
            if (_abilityAuthoringRoot == null)
                _abilityAuthoringRoot = new JObject { ["schemaVersion"] = "1" };
            if (_abilityEffectTimelines == null)
                _abilityEffectTimelines = new List<AbilityEffectTimelineDraft>();

            NormalizeAbilityEffectTimelineDrafts();
            _abilityAuthoringRoot["effectTimelines"] = SerializeAbilityEffectTimelinesToJArray();

            string absolute = AbsoluteProjectPath(_abilityAuthoringJsonPath);
            string directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                absolute,
                JsonConvert.SerializeObject(_abilityAuthoringRoot, CharacterActionAuthoringJsonService.JsonSettings));
            _abilityEffectTimelineDirty = false;
            ClearResourceDraftBaselinesForSession(
                CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline);
            if (refreshUi)
                RefreshUi(UiRefreshScope.All);
        }

        private void CopyAbilityEffectTimelineJsonToClipboard()
        {
            if (_abilityEffectTimelines == null)
                return;

            EditorGUIUtility.systemCopyBuffer = JsonConvert.SerializeObject(
                SerializeAbilityEffectTimelinesToJArray(),
                CharacterActionAuthoringJsonService.JsonSettings);
        }

        private void RestoreAbilityEffectTimelineDraftsFromSession(string json)
        {
            try
            {
                _abilityEffectTimelines = JsonConvert.DeserializeObject<List<AbilityEffectTimelineDraft>>(
                    json,
                    CharacterActionAuthoringJsonService.JsonSettings);
                NormalizeAbilityEffectTimelineDrafts();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to restore Ability Effect Timeline session drafts: " + ex.Message);
                _abilityEffectTimelines = null;
            }
        }

        private string SerializeAbilityEffectTimelineDraftsForSession()
        {
            if (_abilityEffectTimelines == null)
                return string.Empty;

            return JsonConvert.SerializeObject(
                _abilityEffectTimelines,
                CharacterActionAuthoringJsonService.JsonSettings);
        }

        private string SerializeAbilityEffectTimelineRootForHistory()
        {
            return _abilityAuthoringRoot?.ToString(Formatting.None) ?? string.Empty;
        }

        private void RestoreAbilityEffectTimelineHistorySnapshot(
            CharacterActionAuthoringHistorySnapshot snapshot)
        {
            _abilityAuthoringJsonPath = snapshot.AbilityAuthoringJsonPath;
            _abilityAuthoringRoot = string.IsNullOrWhiteSpace(snapshot.AbilityAuthoringRootJson)
                ? null
                : JObject.Parse(snapshot.AbilityAuthoringRootJson);
            if (string.IsNullOrWhiteSpace(snapshot.AbilityEffectTimelinesJson))
            {
                _abilityEffectTimelines = null;
            }
            else
            {
                _abilityEffectTimelines = JsonConvert.DeserializeObject<List<AbilityEffectTimelineDraft>>(
                    snapshot.AbilityEffectTimelinesJson,
                    CharacterActionAuthoringJsonService.JsonSettings)
                    ?? new List<AbilityEffectTimelineDraft>();
                NormalizeAbilityEffectTimelineDrafts();
            }
            _selectedAbilityEffectTimelineId = snapshot.SelectedAbilityEffectTimelineId;
            _pendingAbilityEffectTimelineSelectedItemId = snapshot.SelectedAbilityEffectTimelineItemId;
            _abilityEffectTimelineDirty = snapshot.AbilityEffectTimelineDirty;
        }

        private JArray SerializeAbilityEffectTimelinesToJArray()
        {
            var timelines = new JArray();
            List<AbilityEffectTimelineDraft> source = _abilityEffectTimelines ?? new List<AbilityEffectTimelineDraft>();
            for (int i = 0; i < source.Count; i++)
            {
                AbilityEffectTimelineDraft timeline = source[i];
                var timelineObject = new JObject
                {
                    ["timelineId"] = timeline.TimelineId ?? string.Empty,
                    ["durationFrames"] = Math.Max(1, timeline.DurationFrames),
                };

                var events = new JArray();
                List<AbilityEffectTimelineEventDraft> eventDrafts =
                    timeline.Events ?? new List<AbilityEffectTimelineEventDraft>();
                eventDrafts.Sort(CompareAbilityTimelineEvents);
                for (int eventIndex = 0; eventIndex < eventDrafts.Count; eventIndex++)
                    events.Add(SerializeAbilityEffectTimelineEvent(eventDrafts[eventIndex]));
                timelineObject["events"] = events;
                timelines.Add(timelineObject);
            }

            return timelines;
        }

        private static JObject SerializeAbilityEffectTimelineEvent(AbilityEffectTimelineEventDraft evt)
        {
            var result = new JObject
            {
                ["eventId"] = evt.EventId ?? string.Empty,
                ["trackKind"] = evt.TrackKind ?? AbilityEffectTimelineTrackKind.Gameplay.ToString(),
                ["eventKind"] = evt.EventKind ?? AbilityEffectTimelineEventKind.EmitGameplayEvent.ToString(),
                ["frame"] = Math.Max(0, evt.Frame),
            };
            if (evt.EndFrame >= 0)
                result["endFrame"] = evt.EndFrame;
            if (!string.IsNullOrWhiteSpace(evt.ShapeKind)
                && !string.Equals(evt.ShapeKind, AbilityEffectTimelineShapeKind.None.ToString(), StringComparison.Ordinal))
                result["shapeKind"] = evt.ShapeKind;
            if (!string.IsNullOrWhiteSpace(evt.AnchorMode))
                result["anchorMode"] = evt.AnchorMode;
            if (evt.LocalOffset != null && !evt.LocalOffset.IsZero)
                result["localOffset"] = evt.LocalOffset.ToJObject();
            if (evt.LocalPointB != null && !evt.LocalPointB.IsZero)
                result["localPointB"] = evt.LocalPointB.ToJObject();
            if (evt.Radius > 0f)
                result["radius"] = evt.Radius;
            if (evt.LayerMask != 0)
                result["layerMask"] = evt.LayerMask;
            if (!string.IsNullOrWhiteSpace(evt.TargetTimelineId))
                result["targetTimelineId"] = evt.TargetTimelineId;
            if (!string.IsNullOrWhiteSpace(evt.RequestId))
                result["requestId"] = evt.RequestId;
            if (evt.EffectId != 0)
                result["effectId"] = evt.EffectId;
            if (!string.IsNullOrWhiteSpace(evt.PresentationProfileId))
                result["presentationProfileId"] = evt.PresentationProfileId;
            if (evt.RequiresOwner)
                result["requiresOwner"] = true;
            if (evt.RequiresSource)
                result["requiresSource"] = true;
            return result;
        }

        private void AddAbilityEffectTimeline()
        {
            ApplyAbilityEffectTimelineStructuralChange(() =>
            {
                string baseId = "ability.effect.timeline";
                string id = MakeUniqueAbilityEffectTimelineId(baseId);
                var timeline = new AbilityEffectTimelineDraft
                {
                    TimelineId = id,
                    DurationFrames = 121,
                    Events = new List<AbilityEffectTimelineEventDraft>(),
                };
                _abilityEffectTimelines.Add(timeline);
                _selectedAbilityEffectTimelineId = id;
            });
        }

        private void DeleteSelectedAbilityEffectTimeline()
        {
            AbilityEffectTimelineDraft selected = GetSelectedAbilityEffectTimeline();
            if (selected == null)
                return;

            ApplyAbilityEffectTimelineStructuralChange(() =>
            {
                _abilityEffectTimelines.Remove(selected);
                _selectedAbilityEffectTimelineId = _abilityEffectTimelines.Count > 0
                    ? _abilityEffectTimelines[0].TimelineId
                    : string.Empty;
            });
        }

        private void AddAbilityEffectTimelineEvent(
            AbilityEffectTimelineDraft timeline,
            AbilityEffectTimelineEventKind kind)
        {
            AddAbilityEffectTimelineEventAtFrame(timeline, kind, 0);
        }

        private void AddAbilityEffectTimelineEventAtFrame(
            AbilityEffectTimelineDraft timeline,
            AbilityEffectTimelineEventKind kind,
            int frame)
        {
            if (timeline == null)
                return;

            ApplyAbilityEffectTimelineStructuralChange(() =>
            {
                AbilityEffectTimelineEventDraft evt = CreateAbilityEffectTimelineEvent(timeline, kind);
                evt.Frame = Math.Max(0, frame);
                if (kind == AbilityEffectTimelineEventKind.ShapeQueryWindow)
                    evt.EndFrame = Math.Max(evt.Frame, evt.Frame + 1);
                timeline.Events.Add(evt);
                timeline.Events.Sort(CompareAbilityTimelineEvents);
                _pendingAbilityEffectTimelineSelectedItemId = string.Empty;
            });
        }

        private void AddAbilityEffectTimelineEventAtTrack(
            AbilityEffectTimelineDraft timeline,
            TimelineTrackKind trackKind,
            int frame)
        {
            AbilityEffectTimelineEventKind kind;
            switch (trackKind)
            {
                case TimelineTrackKind.Combat:
                    kind = AbilityEffectTimelineEventKind.AreaPulse;
                    break;
                case TimelineTrackKind.Presentation:
                    kind = AbilityEffectTimelineEventKind.SpawnVfx;
                    break;
                case TimelineTrackKind.Gameplay:
                    kind = AbilityEffectTimelineEventKind.EmitGameplayEvent;
                    break;
                default:
                    return;
            }

            AddAbilityEffectTimelineEventAtFrame(timeline, kind, frame);
        }

        private AbilityEffectTimelineDraft GetSelectedAbilityEffectTimeline()
        {
            if (_abilityEffectTimelines == null || _abilityEffectTimelines.Count == 0)
                return null;

            for (int i = 0; i < _abilityEffectTimelines.Count; i++)
            {
                AbilityEffectTimelineDraft timeline = _abilityEffectTimelines[i];
                if (string.Equals(timeline.TimelineId, _selectedAbilityEffectTimelineId, StringComparison.Ordinal))
                    return timeline;
            }

            return _abilityEffectTimelines[0];
        }

        private AbilityEffectTimelineEventDraft GetSelectedAbilityEffectTimelineEvent(
            AbilityEffectTimelineDraft timeline)
        {
            if (timeline == null || _abilityEffectTimelineView?.Snapshot == null)
                return null;

            string itemId = _abilityEffectTimelineView.SelectedItemId;
            if (string.IsNullOrEmpty(itemId))
                itemId = _pendingAbilityEffectTimelineSelectedItemId;
            if (string.IsNullOrEmpty(itemId))
                return null;

            ITimelineItemAdapter adapter = _abilityEffectTimelineView.Snapshot.FindItem(itemId);
            return adapter is AbilityEffectTimelineEventTimelineAdapter abilityAdapter
                ? abilityAdapter.GetEvent()
                : null;
        }

        private List<string> BuildAbilityEffectTimelineIdChoices()
        {
            var ids = new List<string>();
            if (_abilityEffectTimelines != null)
            {
                for (int i = 0; i < _abilityEffectTimelines.Count; i++)
                {
                    string id = _abilityEffectTimelines[i]?.TimelineId ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(id))
                        ids.Add(id);
                }
            }

            if (ids.Count == 0)
                ids.Add(string.Empty);
            return ids;
        }

        private int SelectedAbilityEffectTimelinePopupIndex(List<string> ids)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], _selectedAbilityEffectTimelineId, StringComparison.Ordinal))
                    return i;
            }

            return 0;
        }

        private void NormalizeAbilityEffectTimelineDrafts()
        {
            NormalizeAbilityEffectTimelineDrafts(_abilityEffectTimelines);
        }

        private static void NormalizeAbilityEffectTimelineDrafts(
            List<AbilityEffectTimelineDraft> timelines)
        {
            if (timelines == null)
                return;

            for (int i = 0; i < timelines.Count; i++)
            {
                AbilityEffectTimelineDraft timeline = timelines[i];
                if (timeline == null)
                {
                    timelines[i] = new AbilityEffectTimelineDraft();
                    timeline = timelines[i];
                }

                timeline.TimelineId ??= string.Empty;
                timeline.DurationFrames = Math.Max(1, timeline.DurationFrames);
                timeline.Events ??= new List<AbilityEffectTimelineEventDraft>();
                for (int j = 0; j < timeline.Events.Count; j++)
                    NormalizeAbilityEffectTimelineEvent(timeline.Events[j]);
            }
        }

        private static void NormalizeAbilityEffectTimelineEvent(AbilityEffectTimelineEventDraft evt)
        {
            if (evt == null)
                return;

            evt.EventId ??= string.Empty;
            evt.TrackKind = NormalizeEnumName<AbilityEffectTimelineTrackKind>(
                evt.TrackKind,
                AbilityEffectTimelineTrackKind.Gameplay.ToString());
            evt.EventKind = NormalizeEnumName<AbilityEffectTimelineEventKind>(
                evt.EventKind,
                AbilityEffectTimelineEventKind.EmitGameplayEvent.ToString());
            evt.ShapeKind = NormalizeEnumName<AbilityEffectTimelineShapeKind>(
                evt.ShapeKind,
                AbilityEffectTimelineShapeKind.None.ToString());
            evt.AnchorMode = NormalizeEnumName<AbilityEffectTimelineAnchorMode>(
                evt.AnchorMode,
                AbilityEffectTimelineAnchorMode.CapturedAnchor.ToString());
            evt.LocalOffset ??= new AbilityEffectTimelineVector3Draft();
            evt.LocalPointB ??= new AbilityEffectTimelineVector3Draft();
            evt.TargetTimelineId ??= string.Empty;
            evt.RequestId ??= string.Empty;
            evt.PresentationProfileId ??= string.Empty;
            evt.Frame = Math.Max(0, evt.Frame);
        }

        private static string NormalizeEnumName<TEnum>(string value, string fallback)
            where TEnum : struct
        {
            if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, out TEnum _))
                return value;
            return fallback;
        }

        private List<string> ValidateAbilityEffectTimelineDrafts()
        {
            var diagnostics = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var byId = new Dictionary<string, AbilityEffectTimelineDraft>(StringComparer.Ordinal);
            List<AbilityEffectTimelineDraft> timelines = _abilityEffectTimelines ?? new List<AbilityEffectTimelineDraft>();
            for (int i = 0; i < timelines.Count; i++)
            {
                AbilityEffectTimelineDraft timeline = timelines[i];
                string path = "effectTimelines[" + i + "]";
                if (timeline == null)
                {
                    diagnostics.Add(path + " is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(timeline.TimelineId))
                    diagnostics.Add(path + ".timelineId is required.");
                else if (!ids.Add(timeline.TimelineId))
                    diagnostics.Add(path + ".timelineId duplicates " + timeline.TimelineId + ".");
                else
                    byId[timeline.TimelineId] = timeline;

                if (timeline.DurationFrames <= 0)
                    diagnostics.Add(path + ".durationFrames must be positive.");

                List<AbilityEffectTimelineEventDraft> events = timeline.Events ?? new List<AbilityEffectTimelineEventDraft>();
                for (int j = 0; j < events.Count; j++)
                    ValidateAbilityEffectTimelineEvent(diagnostics, byId, timeline, events[j], path + ".events[" + j + "]");
            }

            ValidateAbilityEffectTimelineGraph(diagnostics, byId);
            return diagnostics;
        }

        private static void ValidateAbilityEffectTimelineEvent(
            List<string> diagnostics,
            Dictionary<string, AbilityEffectTimelineDraft> knownTimelines,
            AbilityEffectTimelineDraft timeline,
            AbilityEffectTimelineEventDraft evt,
            string path)
        {
            if (evt == null)
            {
                diagnostics.Add(path + " is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(evt.EventId))
                diagnostics.Add(path + ".eventId is required.");
            if (evt.Frame < 0 || evt.Frame >= timeline.DurationFrames)
                diagnostics.Add(path + ".frame is outside timeline duration.");
            if (evt.EndFrame >= 0 && evt.EndFrame < evt.Frame)
                diagnostics.Add(path + ".endFrame is before frame.");
            if (!Enum.TryParse(evt.TrackKind, out AbilityEffectTimelineTrackKind trackKind))
                diagnostics.Add(path + ".trackKind is invalid.");
            if (!Enum.TryParse(evt.EventKind, out AbilityEffectTimelineEventKind eventKind))
                diagnostics.Add(path + ".eventKind is invalid.");
            else if (trackKind != DefaultTrackEnumForEffectEvent(eventKind))
                diagnostics.Add(path + ".trackKind does not match eventKind.");

            if (eventKind == AbilityEffectTimelineEventKind.AreaPulse
                || eventKind == AbilityEffectTimelineEventKind.ShapeQueryWindow)
            {
                if (!Enum.TryParse(evt.ShapeKind, out AbilityEffectTimelineShapeKind shape)
                    || (shape != AbilityEffectTimelineShapeKind.Sphere && shape != AbilityEffectTimelineShapeKind.Capsule))
                    diagnostics.Add(path + ".shapeKind must be Sphere or Capsule.");
                if (evt.Radius <= 0f)
                    diagnostics.Add(path + ".radius must be positive.");
                if (evt.LayerMask == 0)
                    diagnostics.Add(path + ".layerMask must be non-zero.");
            }

            if (eventKind == AbilityEffectTimelineEventKind.StartEffectTimeline)
            {
                if (string.IsNullOrWhiteSpace(evt.TargetTimelineId))
                    diagnostics.Add(path + ".targetTimelineId is required.");
                else if (!knownTimelines.ContainsKey(evt.TargetTimelineId))
                    diagnostics.Add(path + ".targetTimelineId is unknown: " + evt.TargetTimelineId + ".");
            }
        }

        private static void ValidateAbilityEffectTimelineGraph(
            List<string> diagnostics,
            Dictionary<string, AbilityEffectTimelineDraft> byId)
        {
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in byId.Keys)
                ValidateAbilityEffectTimelineGraphNode(diagnostics, id, byId, visiting, visited, 0);
        }

        private static void ValidateAbilityEffectTimelineGraphNode(
            List<string> diagnostics,
            string id,
            Dictionary<string, AbilityEffectTimelineDraft> byId,
            HashSet<string> visiting,
            HashSet<string> visited,
            int depth)
        {
            if (visited.Contains(id) || !byId.TryGetValue(id, out AbilityEffectTimelineDraft timeline))
                return;
            if (depth > AbilityEffectTimelineMaxChildDepth)
            {
                diagnostics.Add("Child timeline depth exceeds " + AbilityEffectTimelineMaxChildDepth + " at " + id + ".");
                return;
            }
            if (!visiting.Add(id))
            {
                diagnostics.Add("Child timeline cycle includes " + id + ".");
                return;
            }

            List<AbilityEffectTimelineEventDraft> events = timeline.Events ?? new List<AbilityEffectTimelineEventDraft>();
            for (int i = 0; i < events.Count; i++)
            {
                AbilityEffectTimelineEventDraft evt = events[i];
                if (evt == null
                    || !string.Equals(
                        evt.EventKind,
                        AbilityEffectTimelineEventKind.StartEffectTimeline.ToString(),
                        StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(evt.TargetTimelineId)
                    || !byId.ContainsKey(evt.TargetTimelineId))
                {
                    continue;
                }

                ValidateAbilityEffectTimelineGraphNode(
                    diagnostics,
                    evt.TargetTimelineId,
                    byId,
                    visiting,
                    visited,
                    depth + 1);
            }

            visiting.Remove(id);
            visited.Add(id);
        }

        private static AbilityEffectTimelineEventDraft CreateAbilityEffectTimelineEvent(
            AbilityEffectTimelineDraft timeline,
            AbilityEffectTimelineEventKind kind)
        {
            string timelineId = string.IsNullOrWhiteSpace(timeline?.TimelineId)
                ? "timeline"
                : timeline.TimelineId;
            var evt = new AbilityEffectTimelineEventDraft
            {
                EventId = timelineId + "." + kind.ToString().ToLowerInvariant(),
                TrackKind = DefaultTrackKindForEffectEvent(kind.ToString()),
                EventKind = kind.ToString(),
                Frame = 0,
                EndFrame = kind == AbilityEffectTimelineEventKind.ShapeQueryWindow ? 1 : -1,
                ShapeKind = AbilityEffectTimelineShapeKind.None.ToString(),
                AnchorMode = AbilityEffectTimelineAnchorMode.CapturedAnchor.ToString(),
                LocalOffset = new AbilityEffectTimelineVector3Draft(),
                LocalPointB = new AbilityEffectTimelineVector3Draft(),
                RequestId = timelineId + "." + kind.ToString().ToLowerInvariant(),
            };
            ApplyDefaultsForAbilityEffectTimelineEvent(evt);
            return evt;
        }

        private static void ApplyDefaultsForAbilityEffectTimelineEvent(AbilityEffectTimelineEventDraft evt)
        {
            if (!Enum.TryParse(evt.EventKind, out AbilityEffectTimelineEventKind kind))
                return;

            if (kind == AbilityEffectTimelineEventKind.AreaPulse
                || kind == AbilityEffectTimelineEventKind.ShapeQueryWindow)
            {
                evt.ShapeKind = AbilityEffectTimelineShapeKind.Sphere.ToString();
                if (evt.Radius <= 0f)
                    evt.Radius = 1f;
            }
            else if (string.IsNullOrWhiteSpace(evt.ShapeKind))
            {
                evt.ShapeKind = AbilityEffectTimelineShapeKind.None.ToString();
            }
        }

        private static string DefaultTrackKindForEffectEvent(string eventKind)
        {
            if (!Enum.TryParse(eventKind, out AbilityEffectTimelineEventKind kind))
                return AbilityEffectTimelineTrackKind.Gameplay.ToString();
            return DefaultTrackEnumForEffectEvent(kind).ToString();
        }

        private static AbilityEffectTimelineTrackKind DefaultTrackEnumForEffectEvent(
            AbilityEffectTimelineEventKind kind)
        {
            switch (kind)
            {
                case AbilityEffectTimelineEventKind.AreaPulse:
                case AbilityEffectTimelineEventKind.ShapeQueryWindow:
                    return AbilityEffectTimelineTrackKind.Combat;
                case AbilityEffectTimelineEventKind.SpawnVfx:
                case AbilityEffectTimelineEventKind.PlayAudioCue:
                case AbilityEffectTimelineEventKind.CameraImpulse:
                case AbilityEffectTimelineEventKind.UiFeedback:
                    return AbilityEffectTimelineTrackKind.Presentation;
                default:
                    return AbilityEffectTimelineTrackKind.Gameplay;
            }
        }

        private static int CompareAbilityTimelineEvents(
            AbilityEffectTimelineEventDraft left,
            AbilityEffectTimelineEventDraft right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0)
                return frame;
            return string.Compare(left.EventId, right.EventId, StringComparison.Ordinal);
        }

        private string MakeUniqueAbilityEffectTimelineId(string baseId)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            if (_abilityEffectTimelines != null)
            {
                for (int i = 0; i < _abilityEffectTimelines.Count; i++)
                {
                    string id = _abilityEffectTimelines[i]?.TimelineId;
                    if (!string.IsNullOrWhiteSpace(id))
                        used.Add(id);
                }
            }

            if (!used.Contains(baseId))
                return baseId;
            for (int i = 2; i < 1000; i++)
            {
                string id = baseId + "." + i;
                if (!used.Contains(id))
                    return id;
            }

            return baseId + "." + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static string AbsoluteProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Directory.GetCurrentDirectory();
            string normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            return Path.IsPathRooted(normalized)
                ? normalized
                : Path.Combine(Directory.GetCurrentDirectory(), normalized);
        }

        private static string ProjectRelativePath(string absolute)
        {
            if (string.IsNullOrWhiteSpace(absolute))
                return string.Empty;

            string root = Directory.GetCurrentDirectory().Replace('\\', '/').TrimEnd('/');
            string normalized = absolute.Replace('\\', '/');
            if (normalized.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(root.Length + 1);
            return normalized;
        }

    }

    internal sealed class AbilityEffectTimelineDraft
        {
            [JsonProperty("timelineId")]
            public string TimelineId { get; set; } = string.Empty;

            [JsonProperty("durationFrames")]
            public int DurationFrames { get; set; } = 1;

            [JsonProperty("events")]
            public List<AbilityEffectTimelineEventDraft> Events { get; set; } =
                new List<AbilityEffectTimelineEventDraft>();
        }

    internal sealed class AbilityEffectTimelineEventDraft
        {
            [JsonProperty("eventId")]
            public string EventId { get; set; } = string.Empty;

            [JsonProperty("trackKind")]
            public string TrackKind { get; set; } = AbilityEffectTimelineTrackKind.Gameplay.ToString();

            [JsonProperty("eventKind")]
            public string EventKind { get; set; } = AbilityEffectTimelineEventKind.EmitGameplayEvent.ToString();

            [JsonProperty("frame")]
            public int Frame { get; set; }

            [JsonProperty("endFrame")]
            public int EndFrame { get; set; } = -1;

            [JsonProperty("shapeKind")]
            public string ShapeKind { get; set; } = AbilityEffectTimelineShapeKind.None.ToString();

            [JsonProperty("anchorMode")]
            public string AnchorMode { get; set; } = AbilityEffectTimelineAnchorMode.CapturedAnchor.ToString();

            [JsonProperty("localOffset")]
            public AbilityEffectTimelineVector3Draft LocalOffset { get; set; } =
                new AbilityEffectTimelineVector3Draft();

            [JsonProperty("localPointB")]
            public AbilityEffectTimelineVector3Draft LocalPointB { get; set; } =
                new AbilityEffectTimelineVector3Draft();

            [JsonProperty("radius")]
            public float Radius { get; set; }

            [JsonProperty("layerMask")]
            public int LayerMask { get; set; }

            [JsonProperty("targetTimelineId")]
            public string TargetTimelineId { get; set; } = string.Empty;

            [JsonProperty("requestId")]
            public string RequestId { get; set; } = string.Empty;

            [JsonProperty("effectId")]
            public int EffectId { get; set; }

            [JsonProperty("presentationProfileId")]
            public string PresentationProfileId { get; set; } = string.Empty;

            [JsonProperty("requiresOwner")]
            public bool RequiresOwner { get; set; }

            [JsonProperty("requiresSource")]
            public bool RequiresSource { get; set; }

            public AbilityEffectTimelineEventDraft Clone()
            {
                string json = JsonConvert.SerializeObject(this, CharacterActionAuthoringJsonService.JsonSettings);
                return JsonConvert.DeserializeObject<AbilityEffectTimelineEventDraft>(
                    json,
                    CharacterActionAuthoringJsonService.JsonSettings);
            }
        }

    internal sealed class AbilityEffectTimelineVector3Draft
        {
            [JsonProperty("x")]
            public float X { get; set; }

            [JsonProperty("y")]
            public float Y { get; set; }

            [JsonProperty("z")]
            public float Z { get; set; }

            public bool IsZero => Math.Abs(X) < 0.0001f && Math.Abs(Y) < 0.0001f && Math.Abs(Z) < 0.0001f;

            public JObject ToJObject()
            {
                return new JObject
                {
                    ["x"] = X,
                    ["y"] = Y,
                    ["z"] = Z,
                };
            }
        }
}
