using System;
using System.Collections.Generic;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Editor.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.ProjectileAuthoring
{
    public sealed partial class ProjectileAuthoringWindow
    {
        private AuthoringTimelinePlaybackController previewPlayback;
        private AuthoringPreviewRenderScene previewScene;
        private ProjectilePreviewSandbox previewSandbox;
        private ProjectilePreviewPresentationSession previewPresentation;
        private ProjectileSceneViewEdit sceneEdit;
        private IMGUIContainer previewViewport;
        private Label previewState;
        private IntegerField previewFrameField;
        private SliderInt previewScrubber;
        private PopupField<string> previewMode;
        private PopupField<string> previewScenario;
        private PopupField<string> previewTimelineMode;
        private VisualElement previewTimelineSection;
        private VisualElement previewTimelineTracks;
        private Label previewMarkerDetail;
        private bool previewPointerActive;
        private int previewFrameBeforeSceneEdit;
        private int previewLastRoutedFrame = -1;
        private bool previewSuppressOneShots;
        private bool previewSceneEditModeRequested;
        private Vector3 previewTargetFixture = new Vector3(6f, 0f, 0f);
        private ProjectilePreviewMarkerIdentity? previewSelectedMarker;
        private ProjectilePreviewPresentationIdentity? previewSelectedPresentation;

        public int PreviewFrameForTests => previewPlayback?.CurrentFrame ?? 0;
        public int PreviewFrameCountForTests => previewSandbox?.FrameCount ?? 0;
        public int PreviewMarkerCountForTests => previewSandbox?.MarkerCount ?? 0;
        public int PreviewPresentationTraceCountForTests => previewSandbox?.PresentationTraceCount ?? 0;
        public bool PreviewIsPlayingForTests => previewPlayback?.IsPlaying ?? false;
        public bool PreviewSlotAuditionActiveForTests => previewPresentation?.IsSlotAuditionActive ?? false;
        public bool PreviewSlotAuditionPlayingForTests => previewPresentation?.IsSlotAuditionPlaying ?? false;
        public string PreviewModeForTests => previewMode?.value ?? string.Empty;
        public ProjectilePreviewScenario PreviewScenarioForTests => ParseScenario(previewScenario?.value);
        public bool PreviewDisposedForTests => previewScene == null && previewSandbox == null && previewPlayback == null;
        public bool SceneEditRegisteredForTests => sceneEdit?.IsRegistered ?? false;
        public int PreviewTimelineTrackCountForTests => previewTimelineTracks?.childCount ?? 0;
        public bool PreviewTimelineVisibleForTests =>
            previewTimelineSection?.style.display.value == DisplayStyle.Flex;
        public bool PreviewHasSelectedMarkerForTests => previewSelectedMarker.HasValue || previewSelectedPresentation.HasValue;
        public void SelectPreviewMarkerForTests(int index) => SelectPreviewMarker(index);
        public void SetPreviewModeForTests(string mode)
        {
            previewMode?.SetValueWithoutNotify(mode);
            ApplyPreviewMode();
        }
        public void SetPreviewTimelineModeForTests(string mode)
        {
            previewTimelineMode?.SetValueWithoutNotify(mode);
            RebuildPreviewTimelineTracks();
        }
        public void SetPreviewScenarioForTests(ProjectilePreviewScenario scenario)
        {
            if (previewScenario == null) return;
            previewScenario.SetValueWithoutNotify(ScenarioName(scenario));
            RefreshPreviewForSelection();
        }
        internal void SelectGeneratedOnlyPreviewForTests(string stableId)
        {
            session.SelectedStableId = stableId ?? string.Empty;
            CurrentIsRequest = false;
            CurrentIsGeneratedOnly = true;
            RefreshPreviewForSelection();
        }
        internal void SelectSourcePreviewForTests(string stableId, bool isRequest = false)
        {
            session.SelectedStableId = stableId ?? string.Empty;
            CurrentIsRequest = isRequest;
            CurrentIsGeneratedOnly = false;
            RefreshPreviewForSelection();
        }
        internal void BeginSceneEditForTests()
        {
            if (sceneEdit?.IsRegistered != true)
                ToggleSceneEdit();
        }
        internal void CommitSceneEditForTests(ProjectileAuthoringFieldEditView edit)
        {
            sceneEdit?.StageForTests(edit);
            sceneEdit?.CommitPendingForTests();
        }
        internal void ClearPreviewSelectionForTests()
        {
            session.SelectedStableId = string.Empty;
            CurrentIsRequest = false;
            CurrentIsGeneratedOnly = false;
            RefreshPreviewForSelection();
        }

        private VisualElement BuildPreviewSurface()
        {
            EnsurePreviewOwners();
            var root = new VisualElement { name = "projectile-preview-surface" };
            root.AddToClassList("pa-preview");
            var toolbar = new Toolbar { name = "projectile-preview-toolbar" };
            previewMode = new PopupField<string>(new List<string> { "VFX Preview", "Runtime Validation" }, 0)
            {
                name = "projectile-preview-mode"
            };
            previewMode.formatListItemCallback = LocalizeUiValue;
            previewMode.formatSelectedValueCallback = LocalizeUiValue;
            previewMode.RegisterValueChangedCallback(_ => ApplyPreviewMode());
            toolbar.Add(previewMode);
            string[] scenarios = { "Miss", "Direct Hit", "Blocked", "Area Hit", "Expire", "Pierce", "Late Attach" };
            previewScenario = new PopupField<string>(new List<string>(scenarios), 0) { name = "projectile-preview-scenario" };
            previewScenario.formatListItemCallback = LocalizeUiValue;
            previewScenario.formatSelectedValueCallback = LocalizeUiValue;
            previewScenario.RegisterValueChangedCallback(_ => RefreshPreviewForSelection());
            previewScenario.SetEnabled(false);
            toolbar.Add(previewScenario);
            toolbar.Add(new Button(TogglePreviewPlayback) { text = "▶/Ⅱ", name = "projectile-preview-play" });
            toolbar.Add(new Button(RestartActivePreview) { text = "↺", name = "projectile-preview-restart" });
            toolbar.Add(new Button(() => StepPreview(-1)) { text = "◀", name = "projectile-preview-step-back" });
            toolbar.Add(new Button(() => StepPreview(1)) { text = "▶", name = "projectile-preview-step-forward" });
            toolbar.Add(new Button(CyclePreviewSpeed) { text = "速度", name = "projectile-preview-speed" });
            toolbar.Add(new Button(ToggleSceneEdit) { text = "场景编辑", name = "projectile-preview-scene-edit" });
            previewState = new Label("请选择投射物") { name = "projectile-preview-state" };
            toolbar.Add(previewState);
            root.Add(toolbar);
            previewViewport = new IMGUIContainer(DrawPreviewViewport) { name = "projectile-preview-viewport" };
            previewViewport.style.flexGrow = 1f;
            root.Add(previewViewport);
            return root;
        }

        private VisualElement BuildPreviewTimeline()
        {
            var root = new VisualElement { name = "projectile-preview-timeline" };
            root.style.flexDirection = FlexDirection.Column;
            var controls = new VisualElement { name = "projectile-preview-timeline-controls" };
            controls.AddToClassList("pa-row");
            previewFrameField = new IntegerField("帧") { name = "projectile-preview-frame" };
            previewFrameField.RegisterValueChangedCallback(evt => SeekPreview(evt.newValue));
            controls.Add(previewFrameField);
            previewScrubber = new SliderInt(0, 1) { name = "projectile-preview-scrubber" };
            previewScrubber.style.flexGrow = 1f;
            previewScrubber.RegisterValueChangedCallback(evt => SeekPreview(evt.newValue));
            controls.Add(previewScrubber);
            previewTimelineMode = new PopupField<string>(new List<string> { "Current Scenario", "All Branches" }, 0)
            {
                name = "projectile-preview-timeline-mode"
            };
            previewTimelineMode.formatListItemCallback = LocalizeUiValue;
            previewTimelineMode.formatSelectedValueCallback = LocalizeUiValue;
            previewTimelineMode.RegisterValueChangedCallback(_ => RebuildPreviewTimelineTracks());
            controls.Add(previewTimelineMode);
            root.Add(controls);
            var timelineScroll = new ScrollView(ScrollViewMode.Vertical) { name = "projectile-preview-timeline-scroll" };
            timelineScroll.AddToClassList("pa-preview-timeline-scroll");
            previewTimelineTracks = new VisualElement { name = "projectile-preview-timeline-tracks" };
            previewTimelineTracks.style.flexDirection = FlexDirection.Column;
            timelineScroll.Add(previewTimelineTracks);
            root.Add(timelineScroll);
            previewMarkerDetail = new Label("标记：无") { name = "projectile-preview-marker-detail" };
            root.Add(previewMarkerDetail);
            RebuildPreviewTimelineTracks();
            return root;
        }

        private void EnsurePreviewOwners()
        {
            previewPlayback ??= new AuthoringTimelinePlaybackController(60f);
            previewPlayback.FrameChanged -= OnPreviewFrameChanged;
            previewPlayback.FrameChanged += OnPreviewFrameChanged;
            previewSandbox ??= new ProjectilePreviewSandbox();
            previewScene ??= new AuthoringPreviewRenderScene(() => previewViewport?.MarkDirtyRepaint());
            sceneEdit ??= new ProjectileSceneViewEdit(CommitSceneEdit, () => CurrentPreviewDefinition(), () => CurrentPreviewRequest(),
                () => previewTargetFixture, SetPreviewTargetFixture, () => previewSandbox?.SpawnResolution.SceneLabel ?? string.Empty);
        }

        private void RefreshPreviewForSelection()
        {
            int frame = previewPlayback?.CurrentFrame ?? 0;
            bool restoreSceneEdit = previewSceneEditModeRequested && sceneEdit?.IsRegistered == true;
            ClearPreviewProblems();
            ClearPreviewSurfaceState();
            if (disposed || workspace == null || CurrentIsGeneratedOnly || string.IsNullOrEmpty(session.SelectedStableId))
            {
                previewSceneEditModeRequested = false;
                return;
            }
            EnsurePreviewOwners();
            previewPlayback.Pause();
            ProjectileDefinitionListItem definition = CurrentPreviewDefinition();
            if (definition?.detail == null)
            {
                AddPreviewProblem("PROJAUTH021", "Error", "Preview compile input is incomplete.", string.Empty,
                    "preview", "preview", session.SelectedStableId);
                RestoreSceneEditAfterRefresh(restoreSceneEdit);
                return;
            }
            ProjectileRequestVariantListItem request = CurrentPreviewRequest();
            FixVector3 start = FixVector3.Zero;
            FixVector3 target = new FixVector3(F(previewTargetFixture.x), F(previewTargetFixture.y), F(previewTargetFixture.z));
            int total = Math.Max(definition.detail.motion?.lifetimeFrames ?? 1, 1);
            var input = new ProjectilePreviewCompileInput(definition, request, workspace.workspaceHash, ParseScenario(previewScenario?.value), total, start, target,
                definition.sourcePaths != null && definition.sourcePaths.Length > 0 ? definition.sourcePaths[0] : request?.sourcePath,
                "definitions[" + definition.stableId + "]");
            if (!previewSandbox.Recompile(input))
            {
                AddPreviewProblem(previewSandbox.DiagnosticCode, "Error", "Preview compile input is incomplete.", input.SourcePath, input.ObjectPath, "preview", definition.stableId);
                UpdatePreviewLabels();
                RestoreSceneEditAfterRefresh(restoreSceneEdit);
                return;
            }
            start = previewSandbox.SpawnResolution.Origin;
            if (!string.IsNullOrEmpty(previewSandbox.SpawnResolution.DiagnosticCode))
            {
                previewSandbox.RecordDiagnosticMarker(0, start, previewSandbox.SpawnResolution.DiagnosticCode);
                AddPreviewProblem(previewSandbox.SpawnResolution.DiagnosticCode, "Warning",
                    previewSandbox.SpawnResolution.SyntheticProfile
                        ? "Definition-only preview uses an explicit synthetic root spawn fixture."
                        : "Preview socket pose is unavailable; the Request fallback root offset is used.",
                    input.SourcePath, input.ObjectPath, "preview", definition.stableId);
            }
            previewPresentation?.Dispose();
            previewPresentation = null;
            ProjectilePresentationBindingListItem binding = workspace.bindings == null ? null
                : Array.Find(workspace.bindings, value => value.projectileDefinitionId == definition.definitionId);
            previewSandbox.BuildPresentationTrace(binding);
            try
            {
                previewPresentation = new ProjectilePreviewPresentationSession(previewScene, binding);
                if (previewPresentation.MissingResourceCount > 0)
                {
                    previewSandbox.RecordDiagnosticMarker(0, start, "PROJAUTH024");
                    AddPreviewProblem("PROJAUTH024", "Warning", "One or more preview VFX bindings or prefab backends could not be resolved; gameplay trajectory remains available.",
                        input.SourcePath, input.ObjectPath, "presentationBinding", definition.stableId);
                }
            }
            catch (Exception exception)
            {
                previewSandbox.RecordDiagnosticMarker(0, start, "PROJAUTH024");
                AddPreviewProblem("PROJAUTH024", "Warning", "Preview VFX composite initialization failed: " + exception.Message,
                    input.SourcePath, input.ObjectPath, "presentationBinding", definition.stableId);
            }
            previewPlayback.SetDuration(previewSandbox.FrameCount);
            previewSuppressOneShots = true;
            previewPlayback.Seek(Math.Min(frame, previewSandbox.FrameCount - 1));
            if (previewMode?.value == "Runtime Validation")
            {
                previewPresentation?.RouteFrame(previewSandbox, previewPlayback.CurrentFrame, false);
                previewLastRoutedFrame = previewPlayback.CurrentFrame;
            }
            else
            {
                previewLastRoutedFrame = -1;
            }
            previewSuppressOneShots = false;
            if (previewScrubber != null) { previewScrubber.lowValue = 0; previewScrubber.highValue = Math.Max(0, previewSandbox.FrameCount - 1); }
            previewSelectedMarker = null;
            previewSelectedPresentation = null;
            if (previewMarkerDetail != null) previewMarkerDetail.text = "标记：无";
            RebuildPreviewTimelineTracks();
            UpdatePreviewLabels();
            previewViewport?.MarkDirtyRepaint();
            RestoreSceneEditAfterRefresh(restoreSceneEdit);
        }

        private void OnPreviewFrameChanged(int frame)
        {
            if (previewFrameField != null) previewFrameField.SetValueWithoutNotify(frame);
            if (previewScrubber != null) previewScrubber.SetValueWithoutNotify(frame);
            if (previewMode?.value != "Runtime Validation")
            {
                UpdatePreviewLabels();
                previewViewport?.MarkDirtyRepaint();
                return;
            }
            bool allowOneShots = !previewSuppressOneShots && frame == previewLastRoutedFrame + 1;
            previewPresentation?.RouteFrame(previewSandbox, frame, allowOneShots);
            previewLastRoutedFrame = frame;
            UpdatePreviewLabels();
            previewViewport?.MarkDirtyRepaint();
        }

        private void UpdatePreviewLabels()
        {
            if (previewState == null) return;
            if (previewMode?.value == "VFX Preview")
            {
                if (previewPresentation?.IsSlotAuditionActive != true)
                    previewState.text = "特效预览：点击生命周期卡片播放单个 VFX";
                return;
            }
            if (previewSandbox == null || previewSandbox.FrameCount == 0) { previewState.text = "无预览"; return; }
            previewState.text = previewSandbox.GetDisplayText(previewPlayback.CurrentFrame, previewPlayback.PlaybackSpeed);
        }

        private void RebuildPreviewTimelineTracks()
        {
            if (previewTimelineTracks == null) return;
            previewTimelineTracks.Clear();
            ProjectilePresentationBindingListItem binding = CurrentPreviewBinding();
            ProjectilePresentationSlotsView slots = binding?.detail?.slots;
            AddLifecycleTimelineTrack("spawn", "出生", slots?.spawn, CombatProjectileUnityPresentationSlotKind.Spawn, false, 0f);
            AddLifecycleTimelineTrack("flightBody", "飞行主体", slots?.flightBody, CombatProjectileUnityPresentationSlotKind.FlightBody, true, 0f);
            AddLifecycleTimelineTrack("trail", "拖尾", slots?.trail, CombatProjectileUnityPresentationSlotKind.Trail, true, 0f);
            AddLifecycleTimelineTrack("directImpact", "直接命中", slots?.directImpact, CombatProjectileUnityPresentationSlotKind.DirectImpact, false, 52f);
            AddLifecycleTimelineTrack("areaImpact", "范围命中", slots?.areaImpact, CombatProjectileUnityPresentationSlotKind.AreaImpact, false, 52f);
            AddLifecycleTimelineTrack("blockedImpact", "格挡命中", slots?.blockedImpact, CombatProjectileUnityPresentationSlotKind.BlockedImpact, false, 52f);
            AddLifecycleTimelineTrack("expire", "过期", slots?.expire, CombatProjectileUnityPresentationSlotKind.Expire, false, 92f);
            AddAuxiliaryTimelineTrack("projectile-preview-track-effect", "玩法效果引用", ProjectilePreviewMarkerKind.EffectTimeline);
            AddAuxiliaryTimelineTrack("projectile-preview-track-diagnostics", "诊断", ProjectilePreviewMarkerKind.Diagnostic);
        }

        private ProjectilePresentationBindingListItem CurrentPreviewBinding()
        {
            ProjectileDefinitionListItem definition = CurrentPreviewDefinition();
            return definition == null || workspace?.bindings == null
                ? null
                : Array.Find(workspace.bindings, value => value.projectileDefinitionId == definition.definitionId);
        }

        private void AddLifecycleTimelineTrack(string kind, string title, ProjectilePresentationSlotView slot,
            CombatProjectileUnityPresentationSlotKind slotKind, bool persistent, float branchPercent)
        {
            string name = "projectile-preview-track-" + kind;
            var row = new VisualElement { name = name };
            row.AddToClassList("pa-timeline-track");
            var titleButton = new Button(() => SelectPresentationSlot(kind)) { name = name + "-label", text = title };
            titleButton.AddToClassList("pa-timeline-track-label");
            row.Add(titleButton);
            var lane = new VisualElement { name = name + "-lane" };
            lane.AddToClassList("pa-timeline-lane");
            row.Add(lane);

            if (slot?.resource == null || string.IsNullOrWhiteSpace(slot.resource.id))
            {
                AddTimelinePlaceholder(lane, kind, "未配置", "pa-timeline-unconfigured", () => SelectPresentationSlot(kind));
                previewTimelineTracks.Add(row);
                return;
            }
            if (!ProjectilePreviewPresentationSession.TryCreateSlot(slot, slotKind, out _))
            {
                AddTimelinePlaceholder(lane, kind, "配置不可用", "pa-timeline-invalid", () => SelectPresentationSlot(kind));
                previewTimelineTracks.Add(row);
                return;
            }

            bool allBranches = previewTimelineMode?.value == "All Branches";
            if (allBranches)
            {
                if (persistent)
                {
                    Button interval = NewTimelineButton(() => SelectPresentationSlot(kind), kind + "-all-branches",
                        (slot.resource?.id ?? title) + " · 全生命周期", "pa-timeline-interval");
                    interval.style.left = Length.Percent(1f);
                    interval.style.width = Length.Percent(83f);
                    lane.Add(interval);
                    Button exit = NewTimelineButton(() => SelectPresentationSlot("exitPolicy"), kind + "-exit-policy",
                        "退出策略", "pa-timeline-exit-policy");
                    exit.style.left = Length.Percent(85f);
                    exit.tooltip = ExitPolicySummary(CurrentPreviewBinding()?.detail?.exitPolicy);
                    lane.Add(exit);
                }
                else
                {
                    Button branch = NewTimelineButton(() => SelectPresentationSlot(kind), kind + "-all-branches",
                        title + " · 分支事件", "pa-timeline-event");
                    branch.style.left = Length.Percent(Math.Min(88f, branchPercent));
                    lane.Add(branch);
                }
                previewTimelineTracks.Add(row);
                return;
            }

            var traceIndices = new List<int>(4);
            if (previewSandbox != null)
            {
                for (int index = 0; index < previewSandbox.PresentationTraceCount; index++)
                {
                    ref readonly ProjectilePreviewPresentationTrace trace = ref previewSandbox.GetPresentationTrace(index);
                    if (trace.Identity.SlotKind == (int)slotKind) traceIndices.Add(index);
                }
            }
            if (traceIndices.Count == 0)
            {
                AddTimelinePlaceholder(lane, kind, "当前场景未触发", "pa-timeline-inactive", () => SelectPresentationSlot(kind));
                previewTimelineTracks.Add(row);
                return;
            }

            if (persistent)
                AddPersistentTimelineTrace(lane, kind, traceIndices);
            else
                for (int index = 0; index < traceIndices.Count; index++) AddTimelineTraceNode(lane, kind, traceIndices[index]);
            previewTimelineTracks.Add(row);
        }

        private void AddPersistentTimelineTrace(VisualElement lane, string kind, List<int> traceIndices)
        {
            int firstIndex = traceIndices[0];
            int start = previewSandbox.GetPresentationTrace(firstIndex).Identity.Frame;
            int end = Math.Max(start, (previewSandbox?.FrameCount ?? 1) - 1);
            string exitMode = string.Empty;
            for (int index = 0; index < traceIndices.Count; index++)
            {
                ref readonly ProjectilePreviewPresentationTrace trace = ref previewSandbox.GetPresentationTrace(traceIndices[index]);
                if (trace.Phase == ProjectilePreviewPresentationPhase.Exit)
                {
                    end = trace.Identity.Frame;
                    exitMode = trace.ExitMode;
                }
            }
            Button interval = NewTimelineButton(() => SelectPreviewPresentationTrace(firstIndex), kind + "-interval",
                start + "–" + end + " 帧" + (string.IsNullOrEmpty(exitMode) ? string.Empty : " · " + LocalizeUiValue(exitMode)), "pa-timeline-interval");
            float startPercent = TimelinePercent(start);
            float endPercent = TimelinePercent(end);
            interval.style.left = Length.Percent(startPercent);
            interval.style.width = Length.Percent(Math.Max(8f, Math.Min(96f - startPercent, endPercent - startPercent)));
            lane.Add(interval);
            for (int index = 0; index < traceIndices.Count; index++)
            {
                ref readonly ProjectilePreviewPresentationTrace trace = ref previewSandbox.GetPresentationTrace(traceIndices[index]);
                if (trace.Phase == ProjectilePreviewPresentationPhase.Rebuild || trace.Phase == ProjectilePreviewPresentationPhase.Exit)
                    AddTimelineTraceNode(lane, kind, traceIndices[index]);
            }
        }

        private void AddTimelineTraceNode(VisualElement lane, string kind, int traceIndex)
        {
            ref readonly ProjectilePreviewPresentationTrace trace = ref previewSandbox.GetPresentationTrace(traceIndex);
            int captured = traceIndex;
            Button node = NewTimelineButton(() => SelectPreviewPresentationTrace(captured), kind + "-trace-" + traceIndex,
                LocalizeUiValue(trace.Phase.ToString()) + " @" + trace.Identity.Frame, "pa-timeline-event");
            node.style.left = Length.Percent(Math.Min(88f, TimelinePercent(trace.Identity.Frame)));
            lane.Add(node);
        }

        private void AddAuxiliaryTimelineTrack(string name, string title, ProjectilePreviewMarkerKind kind)
        {
            var row = new VisualElement { name = name };
            row.AddToClassList("pa-timeline-track");
            var label = new Label(title) { name = name + "-label" };
            label.AddToClassList("pa-timeline-track-label");
            row.Add(label);
            var lane = new VisualElement { name = name + "-lane" };
            lane.AddToClassList("pa-timeline-lane");
            row.Add(lane);
            int count = 0;
            if (previewSandbox != null)
            {
                for (int index = 0; index < previewSandbox.MarkerCount; index++)
                {
                    ref readonly ProjectilePreviewMarker marker = ref previewSandbox.GetMarker(index);
                    if (marker.Identity.Kind != kind) continue;
                    int captured = index;
                    Button node = NewTimelineButton(() => SelectPreviewMarker(captured), name + "-marker-" + index,
                        LocalizeUiValue(kind.ToString()) + " @" + marker.Identity.Frame, "pa-timeline-event");
                    node.style.left = Length.Percent(Math.Min(88f, TimelinePercent(marker.Identity.Frame)));
                    lane.Add(node);
                    count++;
                }
            }
            if (count == 0) AddTimelinePlaceholder(lane, name, "无", "pa-timeline-inactive", null);
            previewTimelineTracks.Add(row);
        }

        private static void AddTimelinePlaceholder(VisualElement lane, string name, string text, string className, Action click)
        {
            if (click == null)
            {
                var label = new Label(text) { name = "projectile-preview-" + name + "-placeholder" };
                label.AddToClassList("pa-timeline-placeholder");
                label.AddToClassList(className);
                lane.Add(label);
                return;
            }
            lane.Add(NewTimelineButton(click, name + "-placeholder", text, className));
        }

        private static Button NewTimelineButton(Action click, string name, string text, string className)
        {
            var button = new Button(click) { name = "projectile-preview-" + name, text = text };
            button.AddToClassList(className);
            return button;
        }

        private float TimelinePercent(int frame)
        {
            int last = Math.Max(1, (previewSandbox?.FrameCount ?? 1) - 1);
            return Math.Max(0f, Math.Min(100f, frame * 100f / last));
        }

        private static string ExitPolicySummary(ProjectilePresentationExitPolicyView exitPolicy)
        {
            return exitPolicy == null
                ? "默认退出策略"
                : "命中：" + LocalizeUiValue(exitPolicy.acceptedImpact)
                    + " · 格挡：" + LocalizeUiValue(exitPolicy.blockedImpact)
                    + " · 过期：" + LocalizeUiValue(exitPolicy.expiration)
                    + " · 外部移除：" + LocalizeUiValue(exitPolicy.externalRemoval);
        }

        private void SelectPreviewMarker(int index)
        {
            if (previewSandbox == null || index < 0 || index >= previewSandbox.MarkerCount) return;
            ref readonly ProjectilePreviewMarker marker = ref previewSandbox.GetMarker(index);
            previewSelectedMarker = marker.Identity;
            previewSelectedPresentation = null;
            SeekPreview(marker.Identity.Frame);
            if (previewMarkerDetail != null)
                previewMarkerDetail.text = "标记：" + LocalizeUiValue(marker.Identity.Kind.ToString()) + "  帧 " + marker.Identity.Frame
                    + "  投射物 " + marker.Identity.ProjectileId + "  序号 " + marker.Identity.Sequence
                    + (string.IsNullOrWhiteSpace(marker.EffectTimelineId) ? string.Empty : "  效果 " + marker.EffectTimelineId)
                    + (string.IsNullOrWhiteSpace(marker.DiagnosticCode) ? string.Empty : "  诊断 " + marker.DiagnosticCode);
        }

        private void SelectPreviewPresentationTrace(int index)
        {
            if (previewSandbox == null || index < 0 || index >= previewSandbox.PresentationTraceCount) return;
            ref readonly ProjectilePreviewPresentationTrace trace = ref previewSandbox.GetPresentationTrace(index);
            previewSelectedMarker = null;
            previewSelectedPresentation = trace.Identity;
            SeekPreview(trace.Identity.Frame);
            if (previewMarkerDetail != null)
                previewMarkerDetail.text = "表现：" + trace.DisplayText + "  资源 " + trace.ResourceKey;
        }

        private void DrawPreviewViewport()
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 10000f, 10f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            HandlePreviewInput(Event.current);
            previewScene?.Draw(rect, DrawPreviewOverlay);
            if (previewSandbox == null || previewSandbox.FrameCount == 0) GUI.Label(rect, "请选择源投射物定义或请求", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawPreviewOverlay(Camera camera)
        {
            if (previewSandbox == null || previewSandbox.FrameCount == 0) return;
            Handles.SetCamera(camera);
            Handles.color = new Color(.25f, .75f, 1f, .9f);
            int max = Math.Min(previewPlayback.CurrentFrame, previewSandbox.FrameCount - 1);
            for (int i = 1; i <= max; i++) Handles.DrawLine(ToVector3(previewSandbox.GetFrame(i - 1).Position), ToVector3(previewSandbox.GetFrame(i).Position), 2f);
            ref readonly ProjectilePreviewFrame current = ref previewSandbox.GetFrame(max);
            Handles.color = current.Active ? Color.cyan : Color.gray;
            Handles.SphereHandleCap(0, ToVector3(current.Position), Quaternion.identity, .12f, EventType.Repaint);
            ProjectileDefinitionDetailView detail = CurrentPreviewDefinition()?.detail;
            float collisionRadius = Math.Max(0f, detail?.collision?.radius ?? 0f);
            if (collisionRadius > 0f)
            {
                Handles.color = new Color(.2f, .8f, 1f, .75f);
                Handles.DrawWireDisc(ToVector3(current.Position), Vector3.up, collisionRadius, 2f);
            }
            for (int index = 0; index < previewSandbox.MarkerCount; index++)
            {
                ref readonly ProjectilePreviewMarker marker = ref previewSandbox.GetMarker(index);
                if (marker.Identity.Frame > max) continue;
                if (marker.Identity.Kind != ProjectilePreviewMarkerKind.Direct
                    && marker.Identity.Kind != ProjectilePreviewMarkerKind.Blocked
                    && marker.Identity.Kind != ProjectilePreviewMarkerKind.Area) continue;
                Vector3 point = ToVector3(marker.Position);
                Handles.color = marker.Identity.Kind == ProjectilePreviewMarkerKind.Blocked ? Color.red : Color.yellow;
                Handles.SphereHandleCap(0, point, Quaternion.identity, .08f, EventType.Repaint);
                Vector3 normal = ToVector3(marker.Normal);
                if (normal.sqrMagnitude > .000001f) Handles.DrawLine(point, point + normal.normalized * .5f, 2f);
                if (marker.Identity.Kind == ProjectilePreviewMarkerKind.Area && detail?.impact != null && detail.impact.areaRadius > 0f)
                    Handles.DrawWireDisc(point, Vector3.up, detail.impact.areaRadius, 2f);
            }
            Handles.color = Color.green;
            Handles.SphereHandleCap(0, ToVector3(previewSandbox.SpawnResolution.Origin), Quaternion.identity, .09f, EventType.Repaint);
            Handles.color = Color.yellow;
            Handles.SphereHandleCap(0, previewTargetFixture, Quaternion.identity, .09f, EventType.Repaint);
        }

        private void HandlePreviewInput(Event evt)
        {
            if (previewScene == null || evt == null) return;
            if (evt.type == EventType.MouseDown && (evt.button == 0 || evt.button == 2)) { previewPointerActive = true; evt.Use(); }
            else if (evt.type == EventType.MouseDrag && previewPointerActive)
            { if (evt.button == 2 || evt.shift) previewScene.Pan(evt.delta); else previewScene.Orbit(evt.delta); evt.Use(); }
            else if (evt.type == EventType.MouseUp) previewPointerActive = false;
            else if (evt.type == EventType.ScrollWheel) { previewScene.Zoom(evt.delta.y); evt.Use(); }
        }

        private void ToggleSceneEdit()
        {
            EnsurePreviewOwners();
            if (sceneEdit.IsRegistered)
            {
                previewSceneEditModeRequested = false;
                sceneEdit.End(cancelPending: false);
                SeekPreview(previewFrameBeforeSceneEdit);
            }
            else
            {
                previewSceneEditModeRequested = true;
                previewPlayback.Pause();
                previewFrameBeforeSceneEdit = previewPlayback.CurrentFrame;
                sceneEdit.Begin();
            }
        }

        private void RestoreSceneEditAfterRefresh(bool restore)
        {
            if (!restore || !previewSceneEditModeRequested || sceneEdit == null || sceneEdit.IsRegistered)
                return;
            previewPlayback?.Pause();
            sceneEdit.Begin();
        }

        private void CommitSceneEdit(ProjectileAuthoringFieldEditView edit) => CommitFieldEdit(edit);

        private void SetPreviewTargetFixture(Vector3 value, bool committed)
        {
            previewTargetFixture = value;
            previewViewport?.MarkDirtyRepaint();
            SceneView.RepaintAll();
            if (committed) RefreshPreviewForSelection();
        }

        private void SeekPreview(int frame)
        {
            if (previewPlayback == null) return;
            previewSuppressOneShots = true;
            previewPlayback.Seek(frame);
            previewSuppressOneShots = false;
        }

        private void StepPreview(int delta)
        {
            if (previewPlayback == null) return;
            previewSuppressOneShots = delta < 0;
            previewPlayback.Step(delta);
            previewSuppressOneShots = false;
        }

        private void RestartPreview()
        {
            if (previewPlayback == null) return;
            previewPlayback.Pause();
            previewSuppressOneShots = true;
            previewPlayback.Seek(0);
            previewPresentation?.Restart(previewSandbox);
            previewLastRoutedFrame = 0;
            previewSuppressOneShots = false;
            UpdatePreviewLabels();
            previewViewport?.MarkDirtyRepaint();
        }

        private void ApplyPreviewMode()
        {
            bool runtimeValidation = previewMode?.value == "Runtime Validation";
            previewScenario?.SetEnabled(runtimeValidation);
            if (previewTimelineSection != null)
                previewTimelineSection.style.display = runtimeValidation
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (runtimeValidation)
            {
                previewPresentation?.StopSlotAudition();
                RefreshPreviewForSelection();
            }
            else
            {
                previewPlayback?.Pause();
                if (previewState != null)
                    previewState.text = "特效预览：点击生命周期卡片播放单个 VFX";
            }
            previewViewport?.MarkDirtyRepaint();
        }

        private void TogglePreviewPlayback()
        {
            if (previewMode?.value == "VFX Preview" && previewPresentation?.IsSlotAuditionActive == true)
                previewPresentation.ToggleSlotAudition();
            else
                previewPlayback?.TogglePlay();
        }

        private void RestartActivePreview()
        {
            if (previewMode?.value == "VFX Preview" && previewPresentation?.IsSlotAuditionActive == true)
                previewPresentation.RestartSlotAudition();
            else
                RestartPreview();
        }

        private void CyclePreviewSpeed()
        {
            if (previewPlayback == null)
                return;
            previewPlayback.CyclePlaybackSpeed();
            previewPresentation?.SetSlotAuditionSpeed(previewPlayback.PlaybackSpeed);
            UpdatePreviewLabels();
        }

        private void SelectAndPreviewPresentationSlot(string kind)
        {
            SelectPresentationSlot(kind);
            if (previewMode?.value != "Runtime Validation")
            {
                PreviewPresentationSlotDirectly(kind);
                return;
            }
            if (!TryResolveRuntimeSlotPreview(kind, CurrentPreviewDefinition()?.detail, out ProjectilePreviewScenario scenario,
                    out CombatProjectileUnityPresentationSlotKind slotKind))
                return;

            previewScenario?.SetValueWithoutNotify(ScenarioName(scenario));
            RefreshPreviewForSelection();
            if (previewPlayback == null || previewSandbox == null || previewSandbox.FrameCount <= 1)
                return;

            int triggerFrame = FindFirstPresentationFrame(slotKind);
            if (triggerFrame <= 0)
                RestartPreview();
            else
                SeekPreview(triggerFrame - 1);
            previewPlayback.Play();
            UpdatePreviewLabels();
        }

        private void PreviewPresentationSlotDirectly(string kind)
        {
            previewPlayback?.Pause();
            if (!TryResolveSlotKind(kind, out CombatProjectileUnityPresentationSlotKind slotKind))
                return;
            if (!EnsurePresentationSessionForAudition())
                return;
            bool started = previewPresentation.TryAuditionSlot(slotKind);
            if (previewState != null)
                previewState.text = started
                    ? "特效预览：" + PresentationSlotLabel(slotKind) + " · 循环播放"
                    : "特效预览：当前槽位未配置或资源不可用";
            previewViewport?.MarkDirtyRepaint();
        }

        private bool EnsurePresentationSessionForAudition()
        {
            if (previewPresentation != null && previewPresentation.MissingResourceCount == 0)
                return true;
            ProjectilePresentationBindingListItem binding = CurrentPreviewBinding();
            if (binding?.detail == null)
                return false;
            EnsurePreviewOwners();
            try
            {
                previewPresentation?.Dispose();
                previewPresentation = new ProjectilePreviewPresentationSession(previewScene, binding);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Projectile Authoring] VFX audition could not initialize: " + exception.Message);
                return false;
            }
        }

        private int FindFirstPresentationFrame(CombatProjectileUnityPresentationSlotKind slotKind)
        {
            for (int index = 0; index < previewSandbox.PresentationTraceCount; index++)
            {
                ref readonly ProjectilePreviewPresentationTrace trace = ref previewSandbox.GetPresentationTrace(index);
                if (trace.Identity.SlotKind == (int)slotKind)
                    return trace.Identity.Frame;
            }
            return 0;
        }

        private static bool TryResolveRuntimeSlotPreview(string kind, ProjectileDefinitionDetailView definition,
            out ProjectilePreviewScenario scenario, out CombatProjectileUnityPresentationSlotKind slotKind)
        {
            scenario = ProjectilePreviewScenario.Miss;
            slotKind = default;
            if (definition == null || !TryResolveSlotKind(kind, out slotKind))
                return false;

            switch (kind)
            {
                case "spawn":
                case "flightBody":
                case "trail":
                    return true;
                case "directImpact":
                    scenario = ProjectilePreviewScenario.DirectHit;
                    return true;
                case "areaImpact":
                    if (!string.Equals(definition.impact?.policyKind, "AreaPulseOnImpact", StringComparison.Ordinal))
                        return false;
                    scenario = ProjectilePreviewScenario.AreaHit;
                    return true;
                case "blockedImpact":
                    if (!string.Equals(definition.impact?.hitEvent?.environmentHitPolicy, "BlockedPresentation", StringComparison.Ordinal))
                        return false;
                    scenario = ProjectilePreviewScenario.Blocked;
                    return true;
                case "expire":
                    scenario = ProjectilePreviewScenario.Expire;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryResolveSlotKind(string kind, out CombatProjectileUnityPresentationSlotKind slotKind)
        {
            switch (kind)
            {
                case "spawn": slotKind = CombatProjectileUnityPresentationSlotKind.Spawn; return true;
                case "flightBody": slotKind = CombatProjectileUnityPresentationSlotKind.FlightBody; return true;
                case "trail": slotKind = CombatProjectileUnityPresentationSlotKind.Trail; return true;
                case "directImpact": slotKind = CombatProjectileUnityPresentationSlotKind.DirectImpact; return true;
                case "areaImpact": slotKind = CombatProjectileUnityPresentationSlotKind.AreaImpact; return true;
                case "blockedImpact": slotKind = CombatProjectileUnityPresentationSlotKind.BlockedImpact; return true;
                case "expire": slotKind = CombatProjectileUnityPresentationSlotKind.Expire; return true;
                default: slotKind = default; return false;
            }
        }

        private static string PresentationSlotLabel(CombatProjectileUnityPresentationSlotKind kind)
        {
            switch (kind)
            {
                case CombatProjectileUnityPresentationSlotKind.Spawn: return "出生";
                case CombatProjectileUnityPresentationSlotKind.FlightBody: return "飞行主体";
                case CombatProjectileUnityPresentationSlotKind.Trail: return "拖尾";
                case CombatProjectileUnityPresentationSlotKind.DirectImpact: return "直接命中";
                case CombatProjectileUnityPresentationSlotKind.AreaImpact: return "范围命中";
                case CombatProjectileUnityPresentationSlotKind.BlockedImpact: return "格挡命中";
                case CombatProjectileUnityPresentationSlotKind.Expire: return "过期";
                default: return kind.ToString();
            }
        }

        private ProjectileDefinitionListItem CurrentPreviewDefinition()
        {
            if (workspace?.definitions == null) return null;
            if (!CurrentIsRequest) return Array.Find(workspace.definitions, value => value.stableId == session.SelectedStableId);
            ProjectileRequestVariantListItem request = CurrentPreviewRequest();
            return Array.Find(workspace.definitions, value => value.stableId == request?.definitionStableId);
        }

        private ProjectileRequestVariantListItem CurrentPreviewRequest()
            => workspace?.requests == null || !CurrentIsRequest ? null : Array.Find(workspace.requests, value => value.stableId == session.SelectedStableId);

        private void AddPreviewProblem(string code, string severity, string message, string sourcePath, string objectPath, string fieldPath, string stableId)
        {
            for (int index = 0; index < transientProblems.Length; index++)
            {
                ProjectileAuthoringProblemView existing = transientProblems[index];
                if (string.Equals(existing.code, code, StringComparison.Ordinal)
                    && string.Equals(existing.objectPath, objectPath, StringComparison.Ordinal)
                    && string.Equals(existing.fieldPath, fieldPath, StringComparison.Ordinal))
                    return;
            }
            var next = new ProjectileAuthoringProblemView[transientProblems.Length + 1];
            Array.Copy(transientProblems, next, transientProblems.Length);
            next[next.Length - 1] = new ProjectileAuthoringProblemView { code = code, severity = severity, message = message, sourcePath = sourcePath, objectPath = objectPath, fieldPath = fieldPath, relatedStableIds = new[] { stableId } };
            transientProblems = next;
            RenderProblems();
        }

        private void ClearPreviewProblems()
        {
            if (transientProblems == null || transientProblems.Length == 0)
                return;
            transientProblems = Array.FindAll(transientProblems, value => !IsPreviewProblem(value));
            RenderProblems();
        }

        private static bool IsPreviewProblem(ProjectileAuthoringProblemView value)
        {
            if (value == null || string.IsNullOrEmpty(value.code) || value.code.Length != 11
                || !value.code.StartsWith("PROJAUTH02", StringComparison.Ordinal))
                return false;
            char suffix = value.code[10];
            return suffix >= '1' && suffix <= '6'
                && (string.Equals(value.fieldPath, "preview", StringComparison.Ordinal)
                    || string.Equals(value.fieldPath, "presentationBinding", StringComparison.Ordinal));
        }

        private void ClearPreviewSurfaceState()
        {
            sceneEdit?.End(cancelPending: true);
            previewPlayback?.Pause();
            previewPresentation?.Dispose();
            previewPresentation = null;
            previewSandbox?.ClearPreview();
            if (previewPlayback != null)
            {
                previewPlayback.SetDuration(1);
                previewPlayback.Seek(0);
            }
            previewLastRoutedFrame = -1;
            previewSuppressOneShots = false;
            previewSelectedMarker = null;
            previewSelectedPresentation = null;
            if (previewFrameField != null) previewFrameField.SetValueWithoutNotify(0);
            if (previewScrubber != null)
            {
                previewScrubber.lowValue = 0;
                previewScrubber.highValue = 0;
                previewScrubber.SetValueWithoutNotify(0);
            }
            if (previewMarkerDetail != null) previewMarkerDetail.text = "标记：无";
            if (previewState != null) previewState.text = "无预览";
            RebuildPreviewTimelineTracks();
            previewViewport?.MarkDirtyRepaint();
        }

        private void DisposePreview()
        {
            sceneEdit?.End(cancelPending: true); sceneEdit = null;
            if (previewPlayback != null) previewPlayback.FrameChanged -= OnPreviewFrameChanged;
            previewPlayback?.Dispose(); previewPlayback = null;
            previewPresentation?.Dispose(); previewPresentation = null;
            previewSandbox?.Dispose(); previewSandbox = null;
            previewScene?.Dispose(); previewScene = null;
            previewPointerActive = false;
            previewLastRoutedFrame = -1;
            previewSceneEditModeRequested = false;
        }

        private static ProjectilePreviewScenario ParseScenario(string value)
        {
            switch (value) { case "Direct Hit": return ProjectilePreviewScenario.DirectHit; case "Blocked": return ProjectilePreviewScenario.Blocked; case "Area Hit": return ProjectilePreviewScenario.AreaHit; case "Expire": return ProjectilePreviewScenario.Expire; case "Pierce": return ProjectilePreviewScenario.Pierce; case "Late Attach": return ProjectilePreviewScenario.LateAttach; default: return ProjectilePreviewScenario.Miss; }
        }
        private static string ScenarioName(ProjectilePreviewScenario scenario) => scenario == ProjectilePreviewScenario.DirectHit ? "Direct Hit" : scenario == ProjectilePreviewScenario.AreaHit ? "Area Hit" : scenario == ProjectilePreviewScenario.LateAttach ? "Late Attach" : scenario.ToString();
        private static Fix64 F(float value) => Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        private static Vector3 ToVector3(FixVector3 value) => new Vector3((float)value.X.RawValue / Fix64.Scale, (float)value.Y.RawValue / Fix64.Scale, (float)value.Z.RawValue / Fix64.Scale);
    }
}
