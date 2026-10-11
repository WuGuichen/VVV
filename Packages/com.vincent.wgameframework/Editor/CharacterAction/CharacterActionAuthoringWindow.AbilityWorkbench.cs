using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline.Preview;
using MxFramework.Gameplay;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private VisualElement _abilityNavigationListHost;
        private VisualElement _abilityTimelineHost;
        private ScrollView _abilityInspectorHost;
        private CharacterActionPreviewViewport _abilityPreviewViewport;
        private Label _abilityPreviewFrameLabel;
        private Button _abilityPreviewPlayPauseButton;
        private Button _abilityPreviewSpeedButton;
        private Toggle _abilityPreviewLoopToggle;
        private string _abilityListFilter = string.Empty;
        private string _abilityCategoryFilter = "全部";
        private CharacterActionAuthoringModel _abilityPreviewModel;
        private string _abilityPreviewTimelineId = string.Empty;
        private long _abilityPreviewModelRevision = long.MinValue;
        private string[] _abilityPreviewFrameLabels = Array.Empty<string>();
        private int _abilityPreviewFrameLabelsDuration = -1;

        private VisualElement BuildAbilityCompositionWorkspace()
        {
            var root = CreateWorkspaceRoot("character-action-ability-workspace");
            float maximumSideWidth = Mathf.Max(300f, (position.width - 420f) * 0.5f);
            float leftWidth = Mathf.Clamp(
                EditorPrefs.GetFloat(WorkbenchLeftWidthPreferenceKey, 260f),
                220f,
                maximumSideWidth);
            float inspectorWidth = Mathf.Clamp(
                EditorPrefs.GetFloat(WorkbenchInspectorWidthPreferenceKey, 360f),
                300f,
                maximumSideWidth);

            var outer = new TwoPaneSplitView(0, leftWidth, TwoPaneSplitViewOrientation.Horizontal)
            {
                name = "character-action-ability-left-split"
            };
            outer.AddToClassList("ca-workbench-split");
            root.Add(outer);
            outer.Add(BuildAbilityNavigationPane());

            var centerRight = new TwoPaneSplitView(1, inspectorWidth, TwoPaneSplitViewOrientation.Horizontal)
            {
                name = "character-action-ability-inspector-split"
            };
            centerRight.AddToClassList("ca-workbench-split");
            outer.Add(centerRight);
            centerRight.Add(BuildAbilityCenterPane());

            _abilityInspectorHost = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "character-action-ability-inspector"
            };
            _abilityInspectorHost.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _abilityInspectorHost.AddToClassList("ca-workbench-inspector");
            centerRight.Add(_abilityInspectorHost);
            return root;
        }

        private VisualElement BuildAbilityNavigationPane()
        {
            var pane = new VisualElement { name = "character-action-ability-navigation" };
            pane.AddToClassList("ca-workbench-navigation");
            pane.Add(new Label("技能") { name = "character-action-ability-title" });
            pane[pane.childCount - 1].AddToClassList("ca-workbench-pane-title");

            var search = new ToolbarSearchField { value = _abilityListFilter };
            search.name = "character-action-ability-search";
            search.RegisterValueChangedCallback(evt =>
            {
                _abilityListFilter = evt.newValue ?? string.Empty;
                RebuildAbilityNavigation();
            });
            pane.Add(search);

            var filters = new VisualElement();
            filters.AddToClassList("ca-workbench-filter-row");
            AddAbilityFilterButton(filters, "全部");
            AddAbilityFilterButton(filters, "战斗");
            AddAbilityFilterButton(filters, "表现");
            AddAbilityFilterButton(filters, "有问题");
            pane.Add(filters);

            _abilityNavigationListHost = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "character-action-ability-list"
            };
            _abilityNavigationListHost.AddToClassList("ca-workbench-action-list");
            pane.Add(_abilityNavigationListHost);

            var createRow = new VisualElement();
            createRow.AddToClassList("ca-workbench-create-row");
            var create = new Button(AddAbilityEffectTimeline) { text = "+ 新建技能" };
            create.AddToClassList("ca-workbench-primary-button");
            createRow.Add(create);
            var templates = new ToolbarMenu { text = "从模板创建" };
            templates.menu.AppendAction(
                "空技能时间线",
                _ => AddAbilityEffectTimeline(),
                DropdownMenuAction.AlwaysEnabled);
            createRow.Add(templates);
            pane.Add(createRow);
            return pane;
        }

        private void AddAbilityFilterButton(VisualElement parent, string label)
        {
            Button button = null;
            button = new Button(() =>
            {
                _abilityCategoryFilter = label;
                for (int i = 0; i < parent.childCount; i++)
                {
                    if (parent[i] is Button sibling)
                    {
                        sibling.EnableInClassList(
                            "ca-workbench-filter-button--selected",
                            ReferenceEquals(sibling, button));
                    }
                }
                RebuildAbilityNavigation();
            }) { text = label };
            button.AddToClassList("ca-workbench-filter-button");
            button.EnableInClassList(
                "ca-workbench-filter-button--selected",
                string.Equals(_abilityCategoryFilter, label, StringComparison.Ordinal));
            parent.Add(button);
        }

        private VisualElement BuildAbilityCenterPane()
        {
            var center = new VisualElement { name = "character-action-ability-center" };
            center.AddToClassList("ca-workbench-center");
            float defaultPreviewHeight = Mathf.Max(220f, (position.height - 130f) * 0.45f);
            float previewHeight = Mathf.Clamp(
                EditorPrefs.GetFloat(WorkbenchPreviewHeightPreferenceKey, defaultPreviewHeight),
                220f,
                Mathf.Max(220f, position.height - 360f));
            var split = new TwoPaneSplitView(0, previewHeight, TwoPaneSplitViewOrientation.Vertical)
            {
                name = "character-action-ability-preview-timeline-split"
            };
            split.AddToClassList("ca-workbench-split");
            center.Add(split);
            split.Add(BuildAbilityPreviewPanel());

            _abilityTimelineHost = new VisualElement { name = "character-action-ability-timeline-host" };
            _abilityTimelineHost.AddToClassList("ca-workbench-timeline");
            split.Add(_abilityTimelineHost);
            return center;
        }

        private VisualElement BuildAbilityPreviewPanel()
        {
            var panel = new VisualElement { name = "character-action-ability-preview" };
            panel.AddToClassList("ca-workbench-preview");
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ca-workbench-preview-toolbar");
            toolbar.Add(new Button(() => SeekPreviewFrame(0)) { text = "|◀" });
            toolbar.Add(new Button(() => SeekPreviewFrame((_timelinePlayback?.CurrentFrame ?? 0) - 1)) { text = "◀" });
            _abilityPreviewPlayPauseButton = new Button(ToggleWorkbenchPreviewPlayback);
            toolbar.Add(_abilityPreviewPlayPauseButton);
            toolbar.Add(new Button(() => SeekPreviewFrame((_timelinePlayback?.CurrentFrame ?? 0) + 1)) { text = "▶" });
            toolbar.Add(new Button(() => SeekPreviewFrame(Math.Max(0, (_timelinePlayback?.DurationFrames ?? 1) - 1)))
            {
                text = "▶|"
            });
            _abilityPreviewSpeedButton = new Button(CycleWorkbenchPreviewSpeed);
            toolbar.Add(_abilityPreviewSpeedButton);
            _abilityPreviewLoopToggle = new Toggle("循环") { value = _previewLoopPlayback };
            _abilityPreviewLoopToggle.RegisterValueChangedCallback(evt =>
            {
                _previewLoopPlayback = evt.newValue;
                EnsureTimelinePlayback();
                _timelinePlayback.LoopPlayback = evt.newValue;
            });
            toolbar.Add(_abilityPreviewLoopToggle);
            var spacer = new VisualElement();
            spacer.AddToClassList("ca-toolbar-spacer");
            toolbar.Add(spacer);
            _abilityPreviewFrameLabel = new Label("0 / 0 帧");
            _abilityPreviewFrameLabel.AddToClassList("ca-workbench-preview-frame");
            toolbar.Add(_abilityPreviewFrameLabel);
            panel.Add(toolbar);

            DisposeAbilityPreviewViewport();
            _abilityPreviewViewport = new CharacterActionPreviewViewport();
            _abilityPreviewViewport.name = "character-action-ability-preview-viewport";
            panel.Add(_abilityPreviewViewport);
            return panel;
        }

        private void RebuildAbilityCompositionWorkspace()
        {
            if (_abilityWorkspaceRoot == null)
                return;
            EnsureAbilityEffectTimelinesLoaded();
            RebuildAbilityNavigation();

            AbilityEffectTimelineDraft selected = GetSelectedAbilityEffectTimeline();
            if (_abilityTimelineHost != null)
            {
                _abilityTimelineHost.Clear();
                if (selected != null)
                    AddAbilityEffectTimelineTimelineView(_abilityTimelineHost, selected);
                else
                    _abilityTimelineHost.Add(new HelpBox("新建或选择一个技能时间线。", HelpBoxMessageType.Info));
            }

            RebuildAbilityInspector(selected);
            RefreshAbilityTransportState();
            UpdateAbilityPreviewFrameLabel(
                _timelinePlayback?.CurrentFrame ?? 0,
                Math.Max(1, selected?.DurationFrames ?? 1));
            UpdateAbilityTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
        }

        private void RebuildAbilityNavigation()
        {
            if (_abilityNavigationListHost == null)
                return;
            _abilityNavigationListHost.Clear();
            string search = (_abilityListFilter ?? string.Empty).Trim();
            List<string> diagnostics = string.Equals(_abilityCategoryFilter, "有问题", StringComparison.Ordinal)
                ? ValidateAbilityEffectTimelineDrafts()
                : null;
            List<AbilityEffectTimelineDraft> timelines = _abilityEffectTimelines
                ?? new List<AbilityEffectTimelineDraft>();
            for (int i = 0; i < timelines.Count; i++)
            {
                AbilityEffectTimelineDraft timeline = timelines[i];
                if (timeline == null || !AbilityTimelineMatchesFilter(timeline, search, diagnostics))
                    continue;
                string timelineId = timeline.TimelineId ?? string.Empty;
                var button = new Button(() => SelectAbilityTimelineFromWorkbench(timelineId));
                button.AddToClassList("ca-workbench-ability-list-item");
                button.EnableInClassList(
                    "ca-workbench-ability-list-item--selected",
                    string.Equals(timelineId, _selectedAbilityEffectTimelineId, StringComparison.Ordinal));
                var title = new Label(string.IsNullOrWhiteSpace(timelineId) ? "（未命名技能）" : timelineId);
                title.AddToClassList("ca-workbench-ability-list-item__title");
                button.Add(title);
                var subtitle = new Label(Math.Max(1, timeline.DurationFrames) + " 帧 · " + timeline.Events.Count + " 事件");
                subtitle.AddToClassList("ca-workbench-ability-list-item__subtitle");
                button.Add(subtitle);
                _abilityNavigationListHost.Add(button);
            }
        }

        private bool AbilityTimelineMatchesFilter(
            AbilityEffectTimelineDraft timeline,
            string search,
            List<string> diagnostics)
        {
            if (!string.IsNullOrWhiteSpace(search)
                && (timeline.TimelineId?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
            {
                return false;
            }
            if (string.Equals(_abilityCategoryFilter, "战斗", StringComparison.Ordinal))
                return ContainsAbilityTrack(timeline, AbilityEffectTimelineTrackKind.Combat.ToString());
            if (string.Equals(_abilityCategoryFilter, "表现", StringComparison.Ordinal))
                return ContainsAbilityTrack(timeline, AbilityEffectTimelineTrackKind.Presentation.ToString());
            if (string.Equals(_abilityCategoryFilter, "有问题", StringComparison.Ordinal))
            {
                string id = timeline.TimelineId ?? string.Empty;
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    if (diagnostics[i].IndexOf(id, StringComparison.Ordinal) >= 0)
                        return true;
                }
                return false;
            }
            return true;
        }

        private static bool ContainsAbilityTrack(AbilityEffectTimelineDraft timeline, string trackKind)
        {
            List<AbilityEffectTimelineEventDraft> events = timeline.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (string.Equals(events[i]?.TrackKind, trackKind, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private void SelectAbilityTimelineFromWorkbench(string timelineId)
        {
            if (string.Equals(_selectedAbilityEffectTimelineId, timelineId, StringComparison.Ordinal))
                return;
            _timelinePlayback?.Pause();
            _selectedAbilityEffectTimelineId = timelineId ?? string.Empty;
            _pendingAbilityEffectTimelineSelectedItemId = string.Empty;
            TouchWorkbenchPreviewRevision();
            SaveSessionState();
            RebuildAbilityCompositionWorkspace();
            RefreshChrome();
            RefreshFooter();
        }

        private void RebuildAbilityInspector(AbilityEffectTimelineDraft selected)
        {
            if (_abilityInspectorHost == null)
                return;
            _abilityInspectorHost.Clear();
            if (selected == null)
            {
                _abilityInspectorHost.Add(new HelpBox("未选择技能时间线。", HelpBoxMessageType.Info));
                return;
            }

            AbilityEffectTimelineEventDraft selectedEvent = GetSelectedAbilityEffectTimelineEvent(selected);
            if (selectedEvent != null)
            {
                AddAbilityEffectTimelineEventEditor(
                    _abilityInspectorHost,
                    selected,
                    selectedEvent,
                    selected.Events.IndexOf(selectedEvent));
                return;
            }

            VisualElement card = AddCard(_abilityInspectorHost, "技能时间线");
            AddAbilityTextField(card, "Timeline ID", selected.TimelineId, value =>
            {
                string oldId = selected.TimelineId;
                selected.TimelineId = value;
                if (string.Equals(_selectedAbilityEffectTimelineId, oldId, StringComparison.Ordinal))
                    _selectedAbilityEffectTimelineId = value;
            });
            AddAbilityIntegerField(
                card,
                "Duration Frames",
                selected.DurationFrames,
                value => selected.DurationFrames = Math.Max(1, value));
            AddAbilityEffectTimelineValidation(card);
            card.Add(new Button(DeleteSelectedAbilityEffectTimeline) { text = "删除技能时间线" });
        }

        private void RefreshAbilityTransportState()
        {
            if (_abilityPreviewPlayPauseButton != null)
            {
                bool playing = _timelinePlayback?.IsPlaying == true;
                _abilityPreviewPlayPauseButton.text = playing ? "Ⅱ" : "▶";
                _abilityPreviewPlayPauseButton.tooltip = playing ? "暂停预览" : "播放预览";
            }
            if (_abilityPreviewSpeedButton != null)
            {
                float speed = _timelinePlayback?.PlaybackSpeed ?? 1f;
                _abilityPreviewSpeedButton.text = speed == 0.5f ? "0.5x"
                    : speed >= 4f ? "4x"
                    : speed >= 2f ? "2x"
                    : "1x";
            }
            _abilityPreviewLoopToggle?.SetValueWithoutNotify(_previewLoopPlayback);
        }

        private void UpdateAbilityPreviewFrameLabel(int frame, int duration)
        {
            if (_abilityPreviewFrameLabel == null)
                return;
            duration = Math.Max(1, duration);
            if (_abilityPreviewFrameLabelsDuration != duration)
            {
                var labels = new string[duration];
                string suffix = " / " + (duration - 1) + " 帧";
                for (int i = 0; i < labels.Length; i++)
                    labels[i] = i + suffix;
                _abilityPreviewFrameLabels = labels;
                _abilityPreviewFrameLabelsDuration = duration;
            }
            _abilityPreviewFrameLabel.text =
                _abilityPreviewFrameLabels[Mathf.Clamp(frame, 0, duration - 1)];
        }

        private void UpdateAbilityTimelineRuntimePreview(int frame)
        {
            if (_workspace != AuthoringWorkspace.SharedResources
                || _sharedResourceSection != SharedResourceSection.AbilityEffectTimeline
                || _abilityPreviewViewport == null)
            {
                return;
            }
            AbilityEffectTimelineDraft timeline = GetSelectedAbilityEffectTimeline();
            if (timeline == null)
            {
                _abilityPreviewViewport.ClearPreview();
                return;
            }
            if (_abilityPreviewModel == null
                || _abilityPreviewModelRevision != _workbenchPreviewRevision
                || !string.Equals(_abilityPreviewTimelineId, timeline.TimelineId, StringComparison.Ordinal))
            {
                _abilityPreviewModel = BuildAbilityPreviewModel(timeline);
                _abilityPreviewModelRevision = _workbenchPreviewRevision;
                _abilityPreviewTimelineId = timeline.TimelineId ?? string.Empty;
            }
            if (!_sceneEditMode)
            {
                _abilityPreviewViewport.PreviewFrame(
                    _abilityPreviewModel,
                    frame,
                    _abilityPreviewModelRevision);
            }
            else
            {
                EnsureTimelinePreviewSession();
                _timelinePreviewSession.PreviewFrame(
                    _abilityPreviewModel,
                    frame,
                    _abilityPreviewModelRevision);
            }
        }

        private CharacterActionAuthoringModel BuildAbilityPreviewModel(AbilityEffectTimelineDraft timeline)
        {
            CharacterActionAuthoringModel preview = _model?.CloneForEditing()
                ?? CharacterActionAuthoringModel.CreateDefault();
            int duration = Math.Max(1, timeline?.DurationFrames ?? 1);
            const int actionId = int.MaxValue; // Editor-only transient preview identity; never persisted or registered.
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateBlankAction(
                actionId,
                timeline?.TimelineId ?? "技能预览");
            action.DisplayName = timeline?.TimelineId ?? "技能预览";
            action.DurationFrames = duration;
            action.Phases = new[]
            {
                new CharacterActionPhaseJsonDto
                {
                    Kind = CharacterActionPhaseKind.Active.ToString(),
                    StartFrame = 0,
                    EndFrame = duration - 1,
                },
            };

            var gameplay = new List<GameplayTrackEventJsonDto>();
            var presentation = new List<PresentationTrackEventJsonDto>();
            List<AbilityEffectTimelineEventDraft> events = timeline?.Events
                ?? new List<AbilityEffectTimelineEventDraft>();
            for (int i = 0; i < events.Count; i++)
            {
                AbilityEffectTimelineEventDraft evt = events[i];
                if (evt == null || !Enum.TryParse(evt.EventKind, out AbilityEffectTimelineEventKind kind))
                    continue;
                AddAbilityPreviewEvent(gameplay, presentation, evt, kind);
            }
            action.GameplayTrack = new GameplayTrackJsonDto { Events = gameplay.ToArray() };
            action.PresentationTrack = new PresentationTrackJsonDto { Events = presentation.ToArray() };
            CharacterActionEventIdentityAuthoring.AssignMissingEventOrders(action);
            preview.Actions.Add(action);
            preview.SelectedActionIndex = preview.Actions.Count - 1;
            preview.ActionSet.DefaultActionId = actionId;
            preview.Normalize();
            return preview;
        }

        private static void AddAbilityPreviewEvent(
            List<GameplayTrackEventJsonDto> gameplay,
            List<PresentationTrackEventJsonDto> presentation,
            AbilityEffectTimelineEventDraft evt,
            AbilityEffectTimelineEventKind kind)
        {
            switch (kind)
            {
                case AbilityEffectTimelineEventKind.SpawnProjectile:
                case AbilityEffectTimelineEventKind.EmitGameplayEvent:
                    gameplay.Add(new GameplayTrackEventJsonDto
                    {
                        Frame = evt.Frame,
                        Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                        RequestId = CharacterActionAuthoringIds.Resolve(evt.RequestId, 408),
                        StableEventId = CharacterActionAuthoringIds.Resolve(evt.EventId, 401),
                    });
                    break;
                case AbilityEffectTimelineEventKind.ApplyEffect:
                    gameplay.Add(new GameplayTrackEventJsonDto
                    {
                        Frame = evt.Frame,
                        Kind = CharacterActionTrackEventKind.ApplyGameplayEffect.ToString(),
                        RequestId = CharacterActionAuthoringIds.Resolve(evt.RequestId, 408),
                        StableEventId = CharacterActionAuthoringIds.Resolve(evt.EventId, 401),
                    });
                    break;
                case AbilityEffectTimelineEventKind.StartEffectTimeline:
                    gameplay.Add(new GameplayTrackEventJsonDto
                    {
                        Frame = evt.Frame,
                        Kind = CharacterActionTrackEventKind.StartEffectTimeline.ToString(),
                        EffectTimelineId = CharacterActionAuthoringIds.Resolve(evt.TargetTimelineId, 407),
                        StableEventId = CharacterActionAuthoringIds.Resolve(evt.EventId, 401),
                    });
                    break;
                case AbilityEffectTimelineEventKind.SpawnVfx:
                    presentation.Add(new PresentationTrackEventJsonDto
                    {
                        Frame = evt.Frame,
                        EndFrame = evt.EndFrame >= evt.Frame ? evt.EndFrame : evt.Frame,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = evt.PresentationProfileId ?? string.Empty,
                        StableEventId = CharacterActionAuthoringIds.Resolve(evt.EventId, 401),
                    });
                    break;
                case AbilityEffectTimelineEventKind.PlayAudioCue:
                    presentation.Add(new PresentationTrackEventJsonDto
                    {
                        Frame = evt.Frame,
                        EndFrame = evt.Frame,
                        Kind = CharacterActionTrackEventKind.PlayAudioCue.ToString(),
                        CueId = CharacterActionAuthoringIds.Resolve(evt.PresentationProfileId, 401),
                        StableEventId = CharacterActionAuthoringIds.Resolve(evt.EventId, 401),
                    });
                    break;
                case AbilityEffectTimelineEventKind.CameraImpulse:
                case AbilityEffectTimelineEventKind.UiFeedback:
                    presentation.Add(new PresentationTrackEventJsonDto
                    {
                        Frame = evt.Frame,
                        EndFrame = evt.Frame,
                        Kind = kind == AbilityEffectTimelineEventKind.CameraImpulse
                            ? CharacterActionTrackEventKind.CameraImpulse.ToString()
                            : CharacterActionTrackEventKind.UiFeedback.ToString(),
                        CueId = CharacterActionAuthoringIds.Resolve(evt.PresentationProfileId, 401),
                        StableEventId = CharacterActionAuthoringIds.Resolve(evt.EventId, 401),
                    });
                    break;
            }
        }

        private void DisposeAbilityPreviewViewport()
        {
            _abilityPreviewViewport?.Dispose();
            _abilityPreviewViewport = null;
            _abilityPreviewModel = null;
            _abilityPreviewModelRevision = long.MinValue;
            _abilityPreviewTimelineId = string.Empty;
        }
    }
}
