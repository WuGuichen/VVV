using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Projectiles;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using MxFramework.Editor.CharacterAction.Timeline.Preview;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private CharacterActionAuthoringDocumentSession _runtimePreviewContextSession;
        private CharacterActionAuthoringExportResult _runtimePreviewContext;
        private long _runtimePreviewContextRevision = long.MinValue;
        private IVisualElementScheduledItem _timelineGestureRefresh;
        private bool _timelineGestureRefreshPending;
        private bool _timelinePointerHeld;
        private bool _timelineGestureUiRefreshed;
        private IVisualElementScheduledItem _timelineFieldRefresh;
        private IVisualElementScheduledItem _timelineScrubPreview;
        private int _timelineScrubPreviewFrame;

        private void EnsureTimelineView()
        {
            if (_timelineView == null)
            {
                _timelineView = new CharacterActionTimelineView();
                _timelineView.style.flexGrow = 1f;
                _timelineView.style.minHeight = 0f;
                _timelineView.RegisterCallback<MouseDownEvent>(evt =>
                {
                    _timelinePointerHeld = true;
                    _timelineGestureRefresh?.Pause();
                    if (_timelineGestureRefreshPending)
                        CancelWorkbenchValidationSchedule();
                }, TrickleDown.TrickleDown);
                _timelineView.RegisterCallback<MouseUpEvent>(evt =>
                {
                    _timelinePointerHeld = false;
                    ScheduleTimelineGestureRefresh();
                }, TrickleDown.TrickleDown);
                _timelineView.RegisterCallback<MouseCaptureOutEvent>(evt =>
                {
                    _timelinePointerHeld = false;
                    ScheduleTimelineGestureRefresh();
                });
            }
            _timelineView.PlayheadScrubEnded -= OnTimelinePlayheadScrubEnded;
            _timelineView.PlayheadScrubEnded += OnTimelinePlayheadScrubEnded;

            _abilityEffectTimelineView?.SetPlaybackController(null);

            if (_tracksTabRoot != null && !ReferenceEquals(_timelineView.parent, _tracksTabRoot))
            {
                _timelineView.RemoveFromHierarchy();
                _tracksTabRoot.Add(_timelineView);
            }

            if (_timelineInspectorHost == null)
            {
                _timelineInspectorHost = new VisualElement();
            }
            ConfigureTimelineHost();
        }

        private void ConfigureTimelineHost()
        {
            if (_timelineView == null)
                return;
            if (_timelineHostConfigured && _timelineView.HasHost)
                return;

            var host = new CharacterActionTimelineHost
            {
                DefaultDurationFrames = DefaultDurationFrames,
                CharacterStableIdProvider = () => _model?.ActionSet?.CharacterStableId ?? string.Empty,
                SaveRequested = SaveAllDirtyCharacterActionSources,
                CanSaveProvider = IsAnyDirty,
                DirtyStateProvider = BuildCharacterActionTimelineDirtyText,
                CurrentItemLabelProvider = BuildCharacterActionTimelinePickerLabel,
                CurrentItemIdProvider = CurrentCharacterActionTimelinePickerItemId,
                PickerItemsProvider = BuildCharacterActionTimelinePickerItems,
                PickerItemSelected = SelectCharacterActionTimelinePickerItem,
                PickerFavoriteChanged = SetCharacterActionTimelinePickerFavorite,
                PickerRecentRecorded = RecordCharacterActionTimelinePickerRecent,
                NavigateToItemProvider = NavigateToCharacterActionTimelineItem,
                AddMenuProvider = PopulateCharacterActionTimelineAddMenu,
                AddEventAtTrack = AddCharacterActionTimelineEventAtTrack,
                CommitFieldChange = (mutate, recordUndo) =>
                {
                    ApplyFieldChange(mutate, recordUndo);
                    _timelineView?.RefreshCommandState();
                    if (_workspace == AuthoringWorkspace.ActionComposition && _timelineView != null)
                    {
                        string selected = _timelineView.SelectedItemId;
                        if (!string.IsNullOrEmpty(selected))
                            _timelineView.RefreshItemPresentation(selected);
                    }
                },
                CommitStructuralChange = mutate => ApplyStructuralChange(mutate, UiRefreshScope.Tracks),
                SelectionChanged = itemId =>
                {
                    _pendingTimelineSelectedItemId = itemId ?? string.Empty;
                    SessionState.SetString(SessionSelectedTimelineItemKey, _pendingTimelineSelectedItemId);
                    using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
                        RebuildWorkbenchActionInspector();
                },
                RequestTracksRefresh = () => RefreshUi(UiRefreshScope.Tracks),
                BeginGesture = () =>
                {
                    CancelWorkbenchValidationSchedule();
                    _timelinePlayback?.Pause();
                    PushUndoSnapshot();
                },
                EndGesture = () =>
                {
                    MarkDirtyForCurrentTab(deferStatusRefresh: true);
                    _timelinePointerHeld = false;
                    _validationDirty = true;
                    _timelineView.RefreshAfterGesture();
                    _timelineGestureUiRefreshed = false;
                    _timelineGestureRefreshPending = true;
                    ScheduleTimelineGestureRefresh();
                },
            };
            _timelineView.SetHost(host);
            _timelineHostConfigured = true;
        }

        private void ScheduleTimelineGestureRefresh()
        {
            if (_timelinePointerHeld || !_timelineGestureRefreshPending || rootVisualElement?.panel == null)
                return;
            _timelineGestureRefresh?.Pause();
            _timelineGestureRefresh = rootVisualElement.schedule.Execute(() =>
            {
                _timelineGestureRefresh = null;
                if (_timelinePointerHeld || !_timelineGestureRefreshPending)
                    return;
                CancelWorkbenchValidationSchedule();
                if (!_timelineGestureUiRefreshed)
                {
                    using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
                    {
                        RefreshLegacyDirtyFlagsFromDocumentSession();
                        RebuildWorkbenchActionInspector();
                        RefreshDirtyChrome();
                        RefreshFooter();
                        _timelineView?.RefreshCommandState();
                        SaveSessionState();
                    }
                    _timelineGestureUiRefreshed = true;
                    // Yield to panel layout/render before validating and replaying.
                    ScheduleTimelineGestureRefresh();
                    return;
                }
                _timelineGestureRefreshPending = false;
                if (_documentSession != null)
                {
                    _validation = GetRuntimePreviewValidationContext();
                    _validationDirty = false;
                }
                else
                    EnsureValidation(includeSnapshots: false, force: true);
                UpdateTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
                RefreshDamageComparisonTable();
                RefreshProblemsWorkbench();
                RefreshFooter();
            });
        }

        private void CancelTimelineGestureRefresh()
        {
            CancelTimelineScrubPreview();
            _timelineFieldRefresh?.Pause();
            _timelineFieldRefresh = null;
            _timelineGestureRefresh?.Pause();
            _timelineGestureRefresh = null;
            _timelineGestureRefreshPending = false;
            _timelinePointerHeld = false;
            _timelineGestureUiRefreshed = false;
        }

        private void PopulateCharacterActionTimelineAddMenu(DropdownMenu menu, int frame)
        {
            AppendCharacterActionTimelineAddAction(menu, "Character Action/Phase", TimelineTrackKind.Phases, frame);
            AppendCharacterActionTimelineAddAction(menu, "Character Action/切换窗口 / Transition Window", TimelineTrackKind.TransitionWindow, frame);
            AppendCharacterActionTimelineAddAction(menu, "Character Action/Motion", TimelineTrackKind.Motion, frame);
            AppendCharacterActionTimelineAddAction(menu, "移动控制倍率标记", TimelineTrackKind.MovementControl, frame);
            AppendCharacterActionTimelineAddAction(menu, "旋转控制倍率标记", TimelineTrackKind.RotationControl, frame);
            AppendCharacterActionTimelineAddAction(menu, "Character Action/Animation", TimelineTrackKind.Animation, frame);
            AppendCharacterActionTimelineAddAction(menu, "Character Action/Combat", TimelineTrackKind.Combat, frame);
            AppendCharacterActionTimelineAddAction(menu, "Character Action/Gameplay", TimelineTrackKind.Gameplay, frame);
            AppendCharacterActionTimelineAddAction(menu, "Character Action/Presentation", TimelineTrackKind.Presentation, frame);
        }

        private void AppendCharacterActionTimelineAddAction(
            DropdownMenu menu,
            string label,
            TimelineTrackKind kind,
            int frame)
        {
            menu.AppendAction(
                label,
                _ => AddCharacterActionTimelineEventAtTrack(kind, frame),
                _ => _timelineView?.Snapshot != null && !_timelineView.Snapshot.ReadOnly
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
        }

        private void AddCharacterActionTimelineEventAtTrack(TimelineTrackKind kind, int frame)
        {
            if (kind == TimelineTrackKind.TransitionWindow)
            {
                AddTransitionWindowAtFrame(_model?.SelectedAction, frame);
                return;
            }

            _timelineView?.AddCharacterActionEventAtTrack(kind, frame);
        }

        private void RebuildTracks(bool rebuildInspector = true)
        {
            if (_timelineView == null || _model?.SelectedAction == null)
                return;

            EnsureTimelinePlayback();
            int savedFrame = _timelinePlayback?.CurrentFrame ?? 0;

            _abilityEffectTimelineView?.SetPlaybackController(null);
            _timelineView.SaveViewState();
            _timelineView.SetPlaybackController(_timelinePlayback);
            _timelineView.Bind(_model.SelectedAction);

            if (_timelinePlayback != null && _timelineView.Snapshot != null)
            {
                _timelinePlayback.SetDuration(_timelineView.Snapshot.DurationFrames);
                int maxFrame = Math.Max(0, _timelineView.Snapshot.DurationFrames - 1);
                _timelinePlayback.Seek(Math.Min(savedFrame, maxFrame));
            }

            _timelineView.RestoreViewState();
            RefreshDamageComparisonTable();
            if (!string.IsNullOrEmpty(_pendingTimelineSelectedItemId)
                && _timelineView.Snapshot?.FindItem(_pendingTimelineSelectedItemId) != null)
            {
                _timelineView.SelectItem(_pendingTimelineSelectedItemId, notify: false);
            }

            UpdateTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
            if (rebuildInspector)
                RebuildTimelineInspector();
        }

        private void EnsureTimelinePlayback()
        {
            if (_timelinePlayback != null)
                return;

            _timelinePlayback = new CharacterActionTimelinePlaybackController();
            _timelinePlayback.LoopPlayback = _previewLoopPlayback;
            _timelinePlayback.FrameChanged += OnTimelinePlaybackFrameChanged;
        }

        private void DisposeTimelinePlayback()
        {
            CancelTimelineScrubPreview();
            _timelineView?.CancelPlayheadScrub();
            if (_timelinePlayback == null)
                return;

            _timelinePlayback.FrameChanged -= OnTimelinePlaybackFrameChanged;
            _timelinePlayback.Dispose();
            _timelinePlayback = null;
            _timelineView?.SetPlaybackController(null);
            _abilityEffectTimelineView?.SetPlaybackController(null);
        }

        private void EnsureTimelinePreviewSession()
        {
            if (_timelinePreviewSession != null)
                return;

            CharacterActionPreviewViewport activeViewport = _editorMode == AuthoringEditorMode.AbilityEffectTimeline
                ? _abilityPreviewViewport
                : _previewViewport;
            if (!_sceneEditMode && activeViewport?.Session != null)
            {
                _timelinePreviewSession = activeViewport.Session;
                return;
            }

            _timelinePreviewSession = new CharacterActionPreviewRuntimeSession();
        }

        private void DisposeTimelinePreviewSession()
        {
            if (_timelinePreviewSession == null)
                return;

            if (!ReferenceEquals(_timelinePreviewSession, _previewViewport?.Session)
                && !ReferenceEquals(_timelinePreviewSession, _abilityPreviewViewport?.Session))
                _timelinePreviewSession.Dispose();
            _timelinePreviewSession = null;
        }

        private void OnTimelinePlaybackFrameChanged(int frame)
        {
            if (_timelineView?.IsPlayheadScrubbing == true)
                ScheduleTimelineScrubPreview(frame);
            else
                UpdateTimelineRuntimePreview(frame);
            UpdateWorkbenchPreviewFrameLabel();
            RefreshWorkbenchTransportState();
        }

        private void ScheduleTimelineScrubPreview(int frame)
        {
            _timelineScrubPreviewFrame = frame;
            if (_timelineScrubPreview != null)
                return;
            if (rootVisualElement?.panel == null)
            {
                UpdateTimelineRuntimePreview(frame);
                return;
            }
            _timelineScrubPreview = rootVisualElement.schedule.Execute(() =>
            {
                _timelineScrubPreview = null;
                if (_timelineView?.IsPlayheadScrubbing == true)
                    UpdateTimelineRuntimePreview(_timelineScrubPreviewFrame);
            });
            _timelineScrubPreview.ExecuteLater(40);
        }

        private void OnTimelinePlayheadScrubEnded(int frame)
        {
            CancelTimelineScrubPreview();
            UpdateTimelineRuntimePreview(frame);
        }

        private void CancelTimelineScrubPreview()
        {
            _timelineScrubPreview?.Pause();
            _timelineScrubPreview = null;
        }

        private void UpdateTimelineRuntimePreview(int frame)
        {
            NormalizeEditorModeForWorkspace();
            if (_editorMode == AuthoringEditorMode.AbilityEffectTimeline)
            {
                UpdateAbilityTimelineRuntimePreview(frame);
                return;
            }
            if (_model?.SelectedAction == null)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                DisposeTimelinePreviewSession();
                CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
                return;
            }

            EnsureTimelinePreviewSession();
            ApplyProjectilePreviewCatalogOverride();
            CharacterActionAuthoringExportResult previewContext = GetRuntimePreviewValidationContext();
            if (!_sceneEditMode && _previewViewport != null && _previewVisible)
                _previewViewport.PreviewFrame(
                    _model,
                    frame,
                    _workbenchPreviewRevision,
                    previewContext);
            else if (_sceneEditMode)
                _timelinePreviewSession.PreviewFrame(
                    _model,
                    frame,
                    _workbenchPreviewRevision,
                    previewContext);
        }

        private CharacterActionAuthoringExportResult GetRuntimePreviewValidationContext()
        {
            CharacterActionAuthoringDocumentSession session = _documentSession;
            if (session == null)
            {
                _runtimePreviewContextSession = null;
                _runtimePreviewContext = null;
                _runtimePreviewContextRevision = long.MinValue;
                return null;
            }

            if (ReferenceEquals(_runtimePreviewContextSession, session)
                && _runtimePreviewContextRevision == _workbenchPreviewRevision)
            {
                return _runtimePreviewContext;
            }

            _runtimePreviewContextSession = session;
            _runtimePreviewContextRevision = _workbenchPreviewRevision;
            _runtimePreviewContext = session.Validate(
                _model?.ReferenceContext,
                buildWorkstationSnapshots: false);
            return _runtimePreviewContext;
        }

        private void RebuildTimelineInspector()
        {
            if (_timelineInspectorHost == null)
                return;

            _timelineInspectorHost.Clear();
            CharacterActionJsonDto action = _model?.SelectedAction;
            if (action == null || _timelineView == null)
            {
                _timelineInspectorHost.Add(new Label("未选择动作。"));
                return;
            }

            string itemId = _timelineView.SelectedItemId;
            ITimelineItemAdapter adapter = string.IsNullOrEmpty(itemId)
                ? null
                : _timelineView.Snapshot?.FindItem(itemId);
            if (adapter == null)
            {
                AddTimelineSelectionSummary(_timelineInspectorHost, action, null);
                _timelineInspectorHost.Add(new Label("点击时间轴上的阶段条或事件标记以编辑属性。"));
                AddTimelinePreviewSections(_timelineInspectorHost);
                AddTimelineTrackListFoldout(_timelineInspectorHost, action);
                return;
            }

            AddTimelineSelectionSummary(_timelineInspectorHost, action, adapter);
            if (adapter is RootMotionTimelineAdapter)
            {
                BuildRootMotionInspector(_timelineInspectorHost, action, timelineInspector: true);
                AddTimelinePreviewSections(_timelineInspectorHost);
                AddTimelineTrackListFoldout(_timelineInspectorHost, action);
                return;
            }

            if (adapter is ControlScaleTimelineAdapter controlKey)
            {
                var card = AddCard(_timelineInspectorHost, controlKey.Rotation ? "旋转控制倍率标记" : "移动控制倍率标记");
                AddTimelineIntegerField(card, "帧", controlKey.StartFrame,
                    value => controlKey.SetRange(value, value), requestFullTracksRefresh: true);
                AddTimelineFloatField(card, "倍率（0–10）", controlKey.Key.Value,
                    value => controlKey.Key.Value = value, requestFullTracksRefresh: true);
                card.Add(new Label("从该帧保持到下一标记；动作退出后恢复 1。"));
                return;
            }

            if (adapter is HitTraceWindowTimelineAdapter hitTraceWindow)
            {
                BuildHitTraceWindowInspector(_timelineInspectorHost, action, hitTraceWindow);
                AddTimelinePreviewSections(_timelineInspectorHost);
                AddTimelineTrackListFoldout(_timelineInspectorHost, action);
                return;
            }

            if (adapter is TransitionWindowTimelineAdapter transitionWindow)
            {
                BuildTransitionWindowInspector(_timelineInspectorHost, action, transitionWindow);
                AddTimelinePreviewSections(_timelineInspectorHost);
                AddTimelineTrackListFoldout(_timelineInspectorHost, action);
                return;
            }

            if (adapter == null || !adapter.TryGetCurrentIndex(out int index))
            {
                _timelineInspectorHost.Add(new Label("选中的时间轴项目已不存在。"));
                AddTimelinePreviewSections(_timelineInspectorHost);
                AddTimelineTrackListFoldout(_timelineInspectorHost, action);
                return;
            }

            switch (adapter.TrackKind)
            {
                case Timeline.TimelineTrackKind.Phases:
                    BuildPhaseInspector(_timelineInspectorHost, action, index);
                    break;
                case Timeline.TimelineTrackKind.TransitionWindow:
                    BuildTransitionWindowInspector(_timelineInspectorHost, action, index);
                    break;
                case Timeline.TimelineTrackKind.Motion:
                    BuildMotionEventInspector(_timelineInspectorHost, action, index, timelineInspector: true);
                    break;
                case Timeline.TimelineTrackKind.Animation:
                    BuildAnimationEventInspector(_timelineInspectorHost, action, index, timelineInspector: true);
                    break;
                case Timeline.TimelineTrackKind.Combat:
                    BuildCombatEventInspector(_timelineInspectorHost, action, index, timelineInspector: true);
                    break;
                case Timeline.TimelineTrackKind.Gameplay:
                    BuildGameplayEventInspector(_timelineInspectorHost, action, index, timelineInspector: true);
                    break;
                case Timeline.TimelineTrackKind.Presentation:
                    BuildPresentationEventInspector(_timelineInspectorHost, action, index, timelineInspector: true);
                    break;
            }

            AddTimelinePreviewSections(_timelineInspectorHost);
            AddTimelineTrackListFoldout(_timelineInspectorHost, action);
        }

        private void AddTimelineSelectionSummary(VisualElement parent, CharacterActionJsonDto action, ITimelineItemAdapter adapter)
        {
            VisualElement summary = new VisualElement();
            summary.AddToClassList("ca-timeline-selection-summary");
            string actionId = action == null || action.ActionId <= 0
                ? "未选择动作"
                : "#" + action.ActionId;
            if (adapter == null)
            {
                summary.Add(new Label("动作：" + actionId + " | 当前选中：无"));
                parent.Add(summary);
                return;
            }

            if (adapter is HitTraceWindowTimelineAdapter hitWindow)
            {
                summary.Add(new Label(BuildHitTraceWindowStatusSummary(hitWindow)));
                parent.Add(summary);
                return;
            }

            string stableEventId = ResolveTimelineStableEventId(action, adapter);
            string identityLabel = adapter.TrackKind == Timeline.TimelineTrackKind.TransitionWindow
                ? "窗口"
                : "稳定事件 ID";
            summary.Add(new Label(
                "动作：" + actionId
                + " | 轨道：" + LocalizeTimelineTrackKind(adapter.TrackKind)
                + " | " + identityLabel + "：" + (string.IsNullOrWhiteSpace(stableEventId) ? "未设置" : stableEventId)));
            Label detail = new Label(adapter.FullTitle ?? adapter.Title ?? string.Empty);
            detail.AddToClassList("ca-timeline-selection-summary__detail");
            summary.Add(detail);
            parent.Add(summary);
        }

        private static string ResolveTimelineStableEventId(CharacterActionJsonDto action, ITimelineItemAdapter adapter)
        {
            if (action == null || adapter == null || !adapter.TryGetCurrentIndex(out int index))
                return string.Empty;

            switch (adapter.TrackKind)
            {
                case Timeline.TimelineTrackKind.Phases:
                    return action.Phases != null && index >= 0 && index < action.Phases.Length
                        ? action.Phases[index]?.Kind ?? string.Empty
                        : string.Empty;
                case Timeline.TimelineTrackKind.TransitionWindow:
                    return action.CancelRules != null && index >= 0 && index < action.CancelRules.Length
                        ? "requestTypeMask="
                            + (action.CancelRules[index]?.RequestTypeMask ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " actionSlotMask="
                            + (action.CancelRules[index]?.ActionSlotMask ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : string.Empty;
                case Timeline.TimelineTrackKind.Motion:
                    return action.MotionTrack?.Events != null && index >= 0 && index < action.MotionTrack.Events.Length
                        ? CharacterActionAuthoringIds.Label(action.MotionTrack.Events[index]?.StableEventId ?? 0, 401)
                        : string.Empty;
                case Timeline.TimelineTrackKind.Animation:
                    return action.AnimationTrack?.Events != null && index >= 0 && index < action.AnimationTrack.Events.Length
                        ? CharacterActionAuthoringIds.Label(action.AnimationTrack.Events[index]?.StableEventId ?? 0, 401)
                        : string.Empty;
                case Timeline.TimelineTrackKind.Combat:
                    return action.CombatTrack?.Events != null && index >= 0 && index < action.CombatTrack.Events.Length
                        ? CharacterActionAuthoringIds.Label(action.CombatTrack.Events[index]?.StableEventId ?? 0, 401)
                        : string.Empty;
                case Timeline.TimelineTrackKind.Gameplay:
                    return action.GameplayTrack?.Events != null && index >= 0 && index < action.GameplayTrack.Events.Length
                        ? CharacterActionAuthoringIds.Label(action.GameplayTrack.Events[index]?.StableEventId ?? 0, 401)
                        : string.Empty;
                case Timeline.TimelineTrackKind.Presentation:
                    return action.PresentationTrack?.Events != null && index >= 0 && index < action.PresentationTrack.Events.Length
                        ? CharacterActionAuthoringIds.Label(action.PresentationTrack.Events[index]?.StableEventId ?? 0, 401)
                        : string.Empty;
                default:
                    return string.Empty;
            }
        }

        private static string LocalizeTimelineTrackKind(Timeline.TimelineTrackKind trackKind)
        {
            switch (trackKind)
            {
                case Timeline.TimelineTrackKind.Phases:
                    return "阶段";
                case Timeline.TimelineTrackKind.TransitionWindow:
                    return "切换窗口 / Transition Window";
                case Timeline.TimelineTrackKind.Motion:
                    return "运动";
                case Timeline.TimelineTrackKind.Animation:
                    return "动画";
                case Timeline.TimelineTrackKind.Combat:
                    return "战斗";
                case Timeline.TimelineTrackKind.Gameplay:
                    return "玩法";
                case Timeline.TimelineTrackKind.Presentation:
                    return "表现";
                default:
                    return trackKind.ToString();
            }
        }

        private void AddTimelinePreviewSections(VisualElement parent)
        {
            Foldout preview = CreateSectionFoldout(
                parent,
                GetTracksFoldoutKey("preview"),
                "预览来源 / 诊断",
                defaultExpanded: false);
            bool built = false;
            void BuildContents()
            {
                if (built)
                    return;
                built = true;
                AddTimelinePreviewBindingControls(preview);
                AddTimelinePreviewDiagnostics(preview);
            }
            if (preview.value)
                BuildContents();
            preview.RegisterValueChangedCallback(evt =>
            {
                if (ReferenceEquals(evt.target, preview) && evt.newValue)
                    BuildContents();
            });
        }

        private void AddTimelineTrackListFoldout(VisualElement parent, CharacterActionJsonDto action)
        {
            Foldout foldout = CreateSectionFoldout(
                parent,
                GetTracksFoldoutKey("all-tracks"),
                "完整 Track 列表",
                defaultExpanded: false);
            bool built = false;
            void BuildContents()
            {
                if (built)
                    return;
                built = true;
                BuildTransitionWindowTrack(foldout, action);
                BuildMotionTrack(foldout, action);
                BuildAnimationTrack(foldout, action);
                BuildCombatTrack(foldout, action);
                BuildGameplayTrack(foldout, action);
                BuildPresentationTrack(foldout, action);
            }
            if (foldout.value)
                BuildContents();
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (ReferenceEquals(evt.target, foldout) && evt.newValue)
                    BuildContents();
            });
        }

        private void ApplyTimelineFieldChange(Action mutate, bool requestFullTracksRefresh = false)
        {
            using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
            using (CharacterActionPreviewBindingDiscovery.BeginReadScope())
            {
                if (!ApplyFieldMutation(mutate, deferStatusRefresh: true))
                    return;
                CancelWorkbenchValidationSchedule();
                CancelTimelineGestureRefresh();
                if (requestFullTracksRefresh)
                    RebuildTracks(rebuildInspector: false);
                else
                    _timelineView?.RefreshItemPresentation(_timelineView.SelectedItemId);
                RebuildWorkbenchActionInspector();

                // Let the panel present the edited controls before evaluating preview.
                // Repeated edits replace this pending projection, never the mutations.
                if (rootVisualElement?.panel != null)
                    _timelineFieldRefresh = rootVisualElement.schedule.Execute(CompleteTimelineFieldRefresh);
                else
                    CompleteTimelineFieldRefresh();
            }
        }

        private void CompleteTimelineFieldRefresh()
        {
            _timelineFieldRefresh = null;
            using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
            using (CharacterActionPreviewBindingDiscovery.BeginReadScope())
            {
                RefreshLegacyDirtyFlagsFromDocumentSession();
                if (_documentSession != null)
                {
                    _validation = GetRuntimePreviewValidationContext();
                    _validationDirty = false;
                }
                else
                    EnsureValidation(includeSnapshots: false, force: true);

                UpdateTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
                RefreshDamageComparisonTable();
                RefreshDirtyChrome();
                RefreshProblemsWorkbench();
                RefreshFooter();
                _timelineView?.RefreshCommandState();
                SaveSessionState();
            }
        }

        private void AddTimelinePreviewBindingControls(VisualElement parent)
        {
            CharacterActionSetJsonDto set = _model?.ActionSet;
            if (set == null)
                return;

            var foldout = new Foldout
            {
                text = "预览来源",
                value = string.IsNullOrWhiteSpace(set.CharacterStableId),
            };
            parent.Add(foldout);
            AddPreviewBindingFields(foldout, UiRefreshScope.Tracks);
            AddButtonRow(
                foldout,
                ("重新生成预览", RegenerateTimelineRuntimePreview));
        }

        private void RegenerateTimelineRuntimePreview()
        {
            if (_model?.SelectedAction == null)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                DisposeTimelinePreviewSession();
                CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
                return;
            }

            EnsureTimelinePreviewSession();
            _timelinePreviewSession.RegeneratePreview(refreshAssetDatabase: true);
            UpdateTimelineRuntimePreview(_timelinePlayback?.CurrentFrame ?? 0);
            RebuildTimelineInspector();
            Repaint();
        }

        private void AddTimelinePreviewDiagnostics(VisualElement parent)
        {
            if (_timelinePreviewSession != null && _timelinePreviewSession.WeaponLocatorPoseSnapshots.Count > 0)
            {
                AddTimelinePreviewInfoLine(
                    parent,
                    "武器定位点姿态：" + _timelinePreviewSession.WeaponLocatorPoseSnapshots.Count);
            }

            if (_timelinePreviewSession != null && _timelinePreviewSession.ActiveHitTraceCount > 0)
            {
                AddTimelinePreviewInfoLine(
                    parent,
                    "命中判定预览：活动窗口=" + _timelinePreviewSession.ActiveHitWindowCount
                    + "，武器轨迹=" + _timelinePreviewSession.DrawnWeaponTraceCount
                    + "，碰撞框=" + _timelinePreviewSession.DrawnHitVolumeCount);
            }

            if (_timelinePreviewSession != null && _timelinePreviewSession.ActiveVfxPreviewCount > 0)
            {
                AddTimelinePreviewInfoLine(
                    parent,
                    "特效预览：活动=" + _timelinePreviewSession.ActiveVfxPreviewCount);
                var snapshots = _timelinePreviewSession.VfxPreviewSnapshots;
                for (int i = 0; i < snapshots.Count; i++)
                {
                    CharacterActionPreviewRuntimeSession.CharacterActionPreviewVfxSnapshot snapshot = snapshots[i];
                    AddTimelinePreviewInfoLine(
                        parent,
                        "  特效资源=" + snapshot.ResourceId
                        + "，显示=" + (snapshot.HasPrefabView ? "预制体" : "辅助图形"));
                }
            }

            if (_timelinePreviewSession != null && _timelinePreviewSession.ActiveProjectilePreviewCount > 0)
            {
                AddTimelinePreviewInfoLine(
                    parent,
                    "投射物预览：活动=" + _timelinePreviewSession.ActiveProjectilePreviewCount);
                var snapshots = _timelinePreviewSession.ProjectilePreviewSnapshots;
                for (int i = 0; i < snapshots.Count; i++)
                {
                    CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot snapshot = snapshots[i];
                    AddTimelinePreviewInfoLine(parent, "  " + snapshot.OriginLabel);
                    AddTimelinePreviewInfoLine(
                        parent,
                        "  请求=" + snapshot.RequestId
                        + "，定义=" + snapshot.DefinitionId
                        + "，显示=" + (snapshot.HasPrefabView ? "预制体" : "辅助图形"));
                }
            }

            string text = _timelinePreviewSession?.DiagnosticsText;
            if (string.IsNullOrWhiteSpace(text))
                return;

            bool infoOnly = text.IndexOf("[Warning]", StringComparison.OrdinalIgnoreCase) < 0
                && text.IndexOf("[Error]", StringComparison.OrdinalIgnoreCase) < 0
                && text.StartsWith("[Info]", StringComparison.Ordinal);
            if (infoOnly)
                AddTimelinePreviewInfoLine(parent, text);
            else
                parent.Add(new HelpBox(text, HelpBoxMessageType.Warning));
        }

        private static void AddTimelinePreviewInfoLine(VisualElement parent, string text)
        {
            var label = new Label(text ?? string.Empty);
            label.AddToClassList("ca-preview-info-line");
            parent.Add(label);
        }
    }
}
