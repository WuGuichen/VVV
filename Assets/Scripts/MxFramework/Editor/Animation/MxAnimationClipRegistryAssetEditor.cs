using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MxFramework.Animation;
using MxFramework.Resources;
using UnityEditor;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MxFramework.Tests")]

namespace MxFramework.Editor.Animation
{
    [CustomEditor(typeof(MxAnimationClipRegistryAsset))]
    public sealed class MxAnimationClipRegistryAssetEditor : UnityEditor.Editor
    {
        private string _lastReport = string.Empty;
        private bool _showEventTimeline = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8f);
            MxAnimationClipRegistryAsset registry = (MxAnimationClipRegistryAsset)target;
            if (GUILayout.Button("Validate Mapping Structure"))
            {
                MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.ExportStructureOnly(registry);
                _lastReport = MxAnimationClipRegistryExporter.CreateReportText(result);
                Debug.Log(_lastReport, registry);
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_lastReport)))
            {
                if (GUILayout.Button("Copy Validation Report"))
                    EditorGUIUtility.systemCopyBuffer = _lastReport;
            }

            if (!string.IsNullOrEmpty(_lastReport))
                EditorGUILayout.HelpBox(_lastReport, MessageType.Info);

            DrawEventTimeline(registry);

            if (GUILayout.Button("Open Workstation"))
                MxAnimationWorkstationWindow.Open(registry);

            if (GUILayout.Button("Open Timeline Scrubber Preview"))
                MxAnimationTimelineScrubberPreviewWindow.Open(registry);
        }

        private void DrawEventTimeline(MxAnimationClipRegistryAsset registry)
        {
            GUILayout.Space(8f);
            _showEventTimeline = EditorGUILayout.Foldout(_showEventTimeline, "Event Timeline Preview", true);
            if (!_showEventTimeline)
                return;

            MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.ExportStructureOnly(registry);
            IReadOnlyList<MxAnimationEventTimelineRow> rows =
                result.Definition != null
                    ? MxAnimationEventTimelineBuilder.BuildRows(result.Definition)
                    : new List<MxAnimationEventTimelineRow>();

            using (new EditorGUI.DisabledScope(rows.Count == 0))
            {
                if (GUILayout.Button("Copy Event Timeline Summary"))
                    EditorGUIUtility.systemCopyBuffer = CreateTimelineSummary(result, rows);
            }

            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No presentation events are defined.", MessageType.None);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                MxAnimationEventTimelineRow row = rows[i];
                string owner = row.SetLevel
                    ? "Set"
                    : (string.IsNullOrWhiteSpace(row.BindingId) ? row.ActionKey : row.BindingId);
                string label = owner
                    + " | "
                    + row.TimeDomain
                    + " "
                    + row.Time.ToString("0.###")
                    + " | "
                    + row.EventKind
                    + " | "
                    + row.EventId;

                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Payload", row.PayloadKey.IsValid ? row.PayloadKey.ToString() : "(none)");
                EditorGUILayout.LabelField("Replay Policy", row.ReplayPolicy.ToString());
                if (row.HasDeterministicCorrelation)
                    EditorGUILayout.LabelField("Correlation", row.CorrelationLabel);
                EditorGUI.indentLevel--;
            }
        }

        private static string CreateTimelineSummary(
            MxAnimationClipRegistryExportResult result,
            IReadOnlyList<MxAnimationEventTimelineRow> rows)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Event Timeline\n");
            builder.Append("setId: ").Append(result != null && result.Definition != null ? result.Definition.SetId : string.Empty).Append('\n');
            builder.Append("rows: ").Append(rows != null ? rows.Count : 0).Append('\n');

            if (rows == null)
                return builder.ToString();

            for (int i = 0; i < rows.Count; i++)
            {
                MxAnimationEventTimelineRow row = rows[i];
                builder.Append("- ")
                    .Append(row.SetLevel ? "set" : "binding")
                    .Append(" binding=")
                    .Append(row.BindingId)
                    .Append(" action=")
                    .Append(row.ActionKey)
                    .Append(" domain=")
                    .Append(row.TimeDomain)
                    .Append(" time=")
                    .Append(row.Time.ToString("R"))
                    .Append(" event=")
                    .Append(row.EventId)
                    .Append(" payload=")
                    .Append(row.PayloadKey)
                    .Append(" policy=")
                    .Append(row.ReplayPolicy)
                    .Append(" correlation=")
                    .Append(row.CorrelationLabel)
                    .Append('\n');
            }

            return builder.ToString();
        }
    }

    internal static class MxAnimationTimelineEventTimeUtility
    {
        public static float FrameToEventTime(
            MxAnimationEventTimeDomain timeDomain,
            int frame,
            int previewMaxFrame,
            int sampleRate)
        {
            int clampedFrame = Math.Max(0, frame);
            switch (timeDomain)
            {
                case MxAnimationEventTimeDomain.Seconds:
                    return sampleRate > 0 ? clampedFrame / (float)sampleRate : 0f;
                case MxAnimationEventTimeDomain.NormalizedTime:
                    return previewMaxFrame > 0 ? clampedFrame / (float)previewMaxFrame : 0f;
                default:
                    return clampedFrame;
            }
        }

        public static int ResolvePreviewMaxFrame(float clipLengthSeconds, int sampleRate, int combatTotalFrames)
        {
            int maxFrame = -1;
            if (clipLengthSeconds > 0f && sampleRate > 0)
                maxFrame = Math.Max(maxFrame, (int)Math.Ceiling(Math.Max(clipLengthSeconds, 0.0001f) * sampleRate));
            if (combatTotalFrames > 0)
                maxFrame = Math.Max(maxFrame, combatTotalFrames - 1);
            return maxFrame;
        }
    }

    internal sealed class MxAnimationWorkstationWindow : EditorWindow
    {
        private const string MenuPath = "MxFramework/MxAnimation/Workstation";

        [SerializeField] private MxAnimationClipRegistryAsset _registry;
        private SerializedObject _serializedRegistry;
        private Vector2 _scroll;
        private string _lastReport = string.Empty;
        private bool _showClipRows = true;
        private bool _showBatchBake = true;
        private bool _showBindingRows = true;
        private bool _showLayerRows = true;
        private bool _showBlendRows = true;
        private bool _showCompatibilityPanel = true;
        private bool[] _batchClipSelected = Array.Empty<bool>();
        private string _batchOutputRoot = MxAnimationBakeEditorTool.DefaultOutputRoot;
        private MxAnimationBatchBakeReport _lastBatchBakeReport;
        private GameObject _compatibilitySkeletonRoot;
        private string _compatibilityProfileId = "skeleton";
        private string _compatibilitySocketPaths = "WeaponSocket\nWeaponTip";
        private MxAnimationCompatibilityWorkstationReport _lastCompatibilityReport;
        private bool _showPackageBuilder = true;
        private MxAnimationPackageProviderSampleKind _packageProviderSampleKind = MxAnimationPackageProviderSampleKind.LocalAssetBundle;
        private int _packageVersion = 1;
        private string _packageCatalogId = string.Empty;
        private string _packageBundleName = string.Empty;
        private string _packageRemoteUrl = string.Empty;
        private string _packageRemoteCacheKey = string.Empty;
        private string _packageRemoteHash = string.Empty;
        private MxAnimationPackageBuildResult _lastPackageBuildResult;
        private bool _showTimelineEditor = true;
        private int _timelineBindingIndex;
        private int _timelineFrame;
        private int _timelineRangeStart;
        private int _timelineRangeEnd = 30;
        private UnityEngine.Object _timelineCombatSource;
        private bool _timelineAutoBakeSelectedClip = true;
        private MxAnimationTimelineScrubberPreview _timelinePreview;
        private string _timelineSummary = string.Empty;
        private MxAnimationClipRegistryExportResult _timelineExport;
        private MxAnimationBakeArtifact _timelineBake;
        private AnimationClip _timelineClip;
        private string _timelineSelector = string.Empty;
        private bool _timelinePreviewDirty = true;

        [MenuItem(MenuPath, priority = 130)]
        public static void Open()
        {
            Open(null);
        }

        public static void Open(MxAnimationClipRegistryAsset registry)
        {
            var window = GetWindow<MxAnimationWorkstationWindow>("MxAnimation Workstation");
            if (registry != null)
                window.SetRegistry(registry);
            window.minSize = new Vector2(720f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_registry == null)
                SetRegistry(Selection.activeObject as MxAnimationClipRegistryAsset);
        }

        private void OnSelectionChange()
        {
            if (_registry != null)
                return;

            MxAnimationClipRegistryAsset selected = Selection.activeObject as MxAnimationClipRegistryAsset;
            if (selected != null)
            {
                SetRegistry(selected);
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_registry == null)
            {
                EditorGUILayout.HelpBox("Select or create a MxAnimationClipRegistryAsset to begin editing.", MessageType.Info);
                return;
            }

            EnsureSerializedObject();
            _serializedRegistry.Update();

            EditorGUI.BeginChangeCheck();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawRegistryHeader();
            DrawClipRows();
            DrawBatchBakePanel();
            DrawBindingRows();
            DrawTimelineEventEditor();
            DrawLayerRows();
            DrawBlendRows();
            DrawCompatibilityPanel();
            DrawPackageBuilderPanel();
            DrawDiagnostics();
            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
                ApplySerializedChanges("Edit MxAnimation Registry");
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                MxAnimationClipRegistryAsset selected = (MxAnimationClipRegistryAsset)EditorGUILayout.ObjectField(
                    _registry,
                    typeof(MxAnimationClipRegistryAsset),
                    false,
                    GUILayout.MinWidth(240f));
                if (EditorGUI.EndChangeCheck())
                    SetRegistry(selected);

                if (GUILayout.Button("Create", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    CreateRegistryAsset();

                using (new EditorGUI.DisabledScope(_registry == null))
                {
                    if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                        EditorGUIUtility.PingObject(_registry);

                    if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                        RefreshReport(logToConsole: true);

                    if (GUILayout.Button("Copy Report", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                        EditorGUIUtility.systemCopyBuffer = _lastReport;
                }
            }
        }

        private void DrawRegistryHeader()
        {
            EditorGUILayout.LabelField("Registry", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawProperty("animationSetId", "Set Id");
                DrawProperty("version", "Version");
                DrawProperty("packageId", "Package Id");

                string path = AssetDatabase.GetAssetPath(_registry);
                EditorGUILayout.LabelField("Asset Path", string.IsNullOrEmpty(path) ? "(unsaved)" : path);
            }
        }

        private void DrawClipRows()
        {
            SerializedProperty clips = _serializedRegistry.FindProperty("clips");
            _showClipRows = DrawSectionHeader(_showClipRows, "Clip Registry", clips);
            if (!_showClipRows || clips == null)
                return;

            if (clips.arraySize == 0)
                EditorGUILayout.HelpBox("No clips are registered. Add clips before binding actions to clip ids.", MessageType.None);

            for (int i = 0; i < clips.arraySize; i++)
            {
                SerializedProperty row = clips.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawRowActions(clips, i, "Clip", InitializeClipRow);
                    DrawRelative(row, "clipId", "Clip Id");
                    DrawRelative(row, "clip", "Animation Clip");
                    DrawRelative(row, "resourceId", "Resource Id");
                    DrawRelative(row, "variant", "Variant");
                    DrawRelative(row, "packageId", "Package Id");
                    DrawRelative(row, "isDefault", "Default");
                    DrawRelative(row, "isFallback", "Fallback");
                }
            }

            if (GUILayout.Button("Add Clip"))
                AddRow(clips, "Add MxAnimation Clip", InitializeClipRow);
        }

        private void DrawBatchBakePanel()
        {
            MxAnimationClipRegistryClipEntry[] clips = _registry.Clips;
            EnsureBatchSelectionState(clips.Length);
            _showBatchBake = EditorGUILayout.Foldout(
                _showBatchBake,
                "Batch Bake (" + clips.Length.ToString(CultureInfo.InvariantCulture) + ")",
                true);
            if (!_showBatchBake)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _batchOutputRoot = EditorGUILayout.TextField("Output Root", string.IsNullOrWhiteSpace(_batchOutputRoot) ? MxAnimationBakeEditorTool.DefaultOutputRoot : _batchOutputRoot);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select All", GUILayout.Width(88f)))
                        SetBatchSelection(clips.Length, true);
                    if (GUILayout.Button("Select None", GUILayout.Width(92f)))
                        SetBatchSelection(clips.Length, false);
                }

                if (clips.Length == 0)
                    EditorGUILayout.HelpBox("No registry clips are available for batch bake.", MessageType.Info);

                for (int i = 0; i < clips.Length; i++)
                {
                    MxAnimationClipRegistryClipEntry clip = clips[i];
                    string label = clip.ClipId
                        + " | "
                        + (clip.Clip != null ? clip.Clip.name : "(missing clip)")
                        + " | "
                        + clip.CreateResourceKey(_registry.PackageId);
                    _batchClipSelected[i] = EditorGUILayout.ToggleLeft(label, _batchClipSelected[i]);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(clips.Length == 0))
                    {
                        if (GUILayout.Button("Bake Selected"))
                            RunBatchBake(SelectedBatchIndices());
                        if (GUILayout.Button("Bake All"))
                            RunBatchBake(null);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_lastBatchBakeReport == null || string.IsNullOrEmpty(_lastBatchBakeReport.ReportText)))
                    {
                        if (GUILayout.Button("Copy Batch Report"))
                            EditorGUIUtility.systemCopyBuffer = _lastBatchBakeReport.ReportText;
                        if (GUILayout.Button("Export Batch Report"))
                            ExportTextFile("Export MxAnimation Batch Bake Report", "MxAnimationBatchBakeReport.txt", _lastBatchBakeReport.ReportText);
                    }
                }

                if (_lastBatchBakeReport != null)
                {
                    EditorGUILayout.HelpBox(
                        "Baked "
                        + _lastBatchBakeReport.BakedCount.ToString(CultureInfo.InvariantCulture)
                        + " clip(s). Errors: "
                        + _lastBatchBakeReport.ErrorCount.ToString(CultureInfo.InvariantCulture)
                        + ", warnings: "
                        + _lastBatchBakeReport.WarningCount.ToString(CultureInfo.InvariantCulture)
                        + ".",
                        _lastBatchBakeReport.Success ? MessageType.Info : MessageType.Warning);
                    EditorGUILayout.TextArea(_lastBatchBakeReport.ReportText, GUILayout.MinHeight(120f));
                }
            }
        }

        private void DrawBindingRows()
        {
            SerializedProperty bindings = _serializedRegistry.FindProperty("bindings");
            _showBindingRows = DrawSectionHeader(_showBindingRows, "Action Bindings", bindings);
            if (!_showBindingRows || bindings == null)
                return;

            if (bindings.arraySize == 0)
                EditorGUILayout.HelpBox("No action bindings are registered.", MessageType.None);

            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty row = bindings.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawRowActions(bindings, i, "Binding", InitializeBindingRow);
                    DrawRelative(row, "bindingId", "Binding Id");
                    DrawRelative(row, "actionId", "Action Id");
                    DrawRelative(row, "actionKey", "Action Key");
                    DrawRelative(row, "clipId", "Clip Id");
                    DrawRelative(row, "layerId", "Layer Id");
                    DrawRelative(row, "loop", "Loop");
                    DrawRelative(row, "playbackSpeed", "Speed");
                    DrawRelative(row, "fadeDurationSeconds", "Fade Seconds");
                    DrawRelative(row, "alignmentPolicy", "Alignment Policy");
                    DrawRelative(row, "events", "Binding Events", includeChildren: true);
                }
            }

            if (GUILayout.Button("Add Binding"))
                AddRow(bindings, "Add MxAnimation Binding", InitializeBindingRow);
        }

        private void DrawTimelineEventEditor()
        {
            SerializedProperty bindings = _serializedRegistry.FindProperty("bindings");
            int bindingCount = bindings != null ? bindings.arraySize : 0;
            _showTimelineEditor = EditorGUILayout.Foldout(
                _showTimelineEditor,
                "Timeline Event Editor + Scrubber (" + bindingCount.ToString(CultureInfo.InvariantCulture) + ")",
                true);
            if (!_showTimelineEditor)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (bindings == null || bindings.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("Add an action binding before editing timeline presentation events.", MessageType.Info);
                    return;
                }

                EditorGUI.BeginChangeCheck();
                string[] labels = CreateTimelineBindingLabels(_registry.Bindings);
                _timelineBindingIndex = Mathf.Clamp(
                    EditorGUILayout.Popup("Action Binding", _timelineBindingIndex, labels),
                    0,
                    Math.Max(0, labels.Length - 1));
                _timelineCombatSource = EditorGUILayout.ObjectField(
                    "Read-only Combat Source",
                    _timelineCombatSource,
                    typeof(UnityEngine.Object),
                    false);
                _timelineAutoBakeSelectedClip = EditorGUILayout.Toggle(
                    "Bake From Selected Clip",
                    _timelineAutoBakeSelectedClip);
                if (EditorGUI.EndChangeCheck())
                    MarkTimelinePreviewDirty();

                _timelineBindingIndex = Mathf.Clamp(_timelineBindingIndex, 0, bindings.arraySize - 1);
                SerializedProperty binding = bindings.GetArrayElementAtIndex(_timelineBindingIndex);
                string selector = ResolveTimelineBindingSelector(_registry.Bindings);
                AnimationClip selectedClip = ResolveTimelineSelectedClip(_registry, selector);
                int previewMaxFrame = ResolveTimelinePreviewMaxFrame(selectedClip);
                int scrubMaxFrame = ResolveTimelineScrubMaxFrame(selectedClip, previewMaxFrame);

                DrawTimelineFrameControls(scrubMaxFrame);
                DrawTimelineSourceStatus(selectedClip);
                DrawTimelineEventRows(binding.FindPropertyRelative("events"), previewMaxFrame);
                DrawTimelinePreview();
            }
        }

        private void DrawTimelineFrameControls(int maxFrame)
        {
            _timelineRangeStart = Mathf.Max(0, _timelineRangeStart);
            _timelineRangeEnd = Mathf.Max(_timelineRangeStart, _timelineRangeEnd <= 0 ? maxFrame : _timelineRangeEnd);
            _timelineFrame = Mathf.Clamp(_timelineFrame, _timelineRangeStart, _timelineRangeEnd);

            EditorGUILayout.LabelField("Scrubber", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _timelineRangeStart = Mathf.Max(0, EditorGUILayout.IntField("Range Start", _timelineRangeStart));
                _timelineRangeEnd = Mathf.Max(_timelineRangeStart, EditorGUILayout.IntField("Range End", _timelineRangeEnd));
                if (GUILayout.Button("Full", GUILayout.Width(52f)))
                {
                    _timelineRangeStart = 0;
                    _timelineRangeEnd = Math.Max(1, maxFrame);
                }

                if (EditorGUI.EndChangeCheck())
                    MarkTimelinePreviewDirty();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_timelineFrame <= _timelineRangeStart))
                {
                    if (GUILayout.Button("Previous Frame", GUILayout.Width(116f)))
                    {
                        _timelineFrame = Mathf.Max(_timelineRangeStart, _timelineFrame - 1);
                        MarkTimelinePreviewDirty();
                    }
                }

                EditorGUI.BeginChangeCheck();
                _timelineFrame = EditorGUILayout.IntSlider(
                    "Frame",
                    _timelineFrame,
                    _timelineRangeStart,
                    Math.Max(_timelineRangeStart, _timelineRangeEnd));
                if (EditorGUI.EndChangeCheck())
                    MarkTimelinePreviewDirty();

                using (new EditorGUI.DisabledScope(_timelineFrame >= _timelineRangeEnd))
                {
                    if (GUILayout.Button("Next Frame", GUILayout.Width(96f)))
                    {
                        _timelineFrame = Mathf.Min(_timelineRangeEnd, _timelineFrame + 1);
                        MarkTimelinePreviewDirty();
                    }
                }
            }
        }

        private void DrawTimelineSourceStatus(AnimationClip selectedClip)
        {
            string clipStatus = selectedClip != null
                ? selectedClip.name + " (" + selectedClip.length.ToString("0.###", CultureInfo.InvariantCulture) + "s)"
                : "(missing clip reference)";
            EditorGUILayout.LabelField("Selected Clip", clipStatus);
            EditorGUILayout.LabelField(
                "Bake Rows",
                _timelineAutoBakeSelectedClip
                    ? "Generated in memory from selected AnimationClip when available; no serialized bake artifact assignment in v1."
                    : "Disabled; enable Bake From Selected Clip or assign a future bake artifact source.");
            EditorGUILayout.LabelField(
                "Combat Rows",
                _timelineCombatSource != null
                    ? "Reading reflected frame ranges, windows, traces, and frame events when exposed by the source."
                    : "No read-only Combat source assigned.");
        }

        private void DrawTimelineEventRows(SerializedProperty events, int maxFrame)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Presentation Events", EditorStyles.boldLabel);
            if (events == null)
            {
                EditorGUILayout.HelpBox("Selected binding does not expose a presentation event array.", MessageType.Warning);
                return;
            }

            if (events.arraySize == 0)
                EditorGUILayout.HelpBox("No presentation events are defined for this binding.", MessageType.None);

            for (int i = 0; i < events.arraySize; i++)
            {
                SerializedProperty row = events.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Event " + i.ToString(CultureInfo.InvariantCulture), EditorStyles.boldLabel);
                        if (GUILayout.Button("Set To Frame", GUILayout.Width(96f)))
                            SetTimelineEventTime(row, maxFrame);
                        if (GUILayout.Button("Duplicate", GUILayout.Width(82f)))
                            DuplicateRow(events, i, "Presentation Event", InitializeTimelineEventRow);
                        if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                            RemoveRow(events, i, "Remove MxAnimation Presentation Event");
                    }

                    DrawRelative(row, "eventId", "Event Id");
                    DrawRelative(row, "timeDomain", "Time Domain");
                    DrawRelative(row, "time", "Time");
                    DrawRelative(row, "eventKind", "Kind");
                    DrawRelative(row, "payloadResourceId", "Payload Resource Id");
                    DrawRelative(row, "payloadTypeId", "Payload Type Id");
                    DrawRelative(row, "payloadVariant", "Payload Variant");
                    DrawRelative(row, "payloadPackageId", "Payload Package Id");
                    DrawRelative(row, "socket", "Socket");
                    DrawRelative(row, "tag", "Tag");
                    DrawRelative(row, "replayPolicy", "Replay Policy");
                }
            }

            if (GUILayout.Button("Add Presentation Event"))
                AddTimelineEvent(events, maxFrame);
        }

        private void DrawTimelinePreview()
        {
            EnsureTimelinePreview();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Preview Rows", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Timeline Preview"))
                    RefreshTimelinePreviewInputs();
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_timelineSummary)))
                {
                    if (GUILayout.Button("Copy Timeline Text"))
                        EditorGUIUtility.systemCopyBuffer = _timelineSummary;
                    if (GUILayout.Button("Export Timeline Text"))
                        ExportTimelineSummaryText();
                }
            }

            if (_timelinePreview == null)
            {
                EditorGUILayout.HelpBox("Timeline preview is unavailable.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Set", _timelinePreview.SetId);
            EditorGUILayout.LabelField("Binding", _timelinePreview.BindingId);
            EditorGUILayout.LabelField("Clip Key", _timelinePreview.ClipKey);
            EditorGUILayout.LabelField(
                "Time",
                _timelinePreview.Seconds.ToString("0.###", CultureInfo.InvariantCulture)
                + "s / normalized "
                + _timelinePreview.NormalizedTime.ToString("0.###", CultureInfo.InvariantCulture));

            if (_timelinePreview.Rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No presentation, Combat, or bake rows intersect the selected frame.", MessageType.None);
            }
            else
            {
                for (int i = 0; i < _timelinePreview.Rows.Count; i++)
                {
                    MxAnimationTimelineScrubberRow row = _timelinePreview.Rows[i];
                    EditorGUILayout.LabelField(row.Kind + " | " + row.Label, row.Details);
                }
            }

            EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);
            if (_timelinePreview.Diagnostics.Count == 0)
            {
                EditorGUILayout.LabelField("none");
            }
            else
            {
                for (int i = 0; i < _timelinePreview.Diagnostics.Count; i++)
                {
                    MxAnimationTimelineScrubberDiagnostic diagnostic = _timelinePreview.Diagnostics[i];
                    EditorGUILayout.HelpBox(
                        diagnostic.Code + ": " + diagnostic.Message,
                        diagnostic.Severity == MxAnimationTimelineScrubberDiagnosticSeverity.Error ? MessageType.Error : MessageType.Warning);
                }
            }
        }

        private void DrawLayerRows()
        {
            SerializedProperty layers = _serializedRegistry.FindProperty("layers");
            _showLayerRows = DrawSectionHeader(_showLayerRows, "Layers", layers);
            if (!_showLayerRows || layers == null)
                return;

            if (layers.arraySize == 0)
                EditorGUILayout.HelpBox("No explicit layers are registered. Bindings can still target the default base layer.", MessageType.None);

            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty row = layers.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawRowActions(layers, i, "Layer", InitializeLayerRow);
                    DrawRelative(row, "layerId", "Layer Id");
                    DrawRelative(row, "profileId", "Profile Id");
                    DrawRelative(row, "defaultWeight", "Default Weight");
                    DrawRelative(row, "blendMode", "Blend Mode");
                    DrawRelative(row, "avatarMask", "Avatar Mask");
                    DrawRelative(row, "avatarMaskResourceId", "AvatarMask Resource Id");
                    DrawRelative(row, "avatarMaskVariant", "AvatarMask Variant");
                    DrawRelative(row, "avatarMaskPackageId", "AvatarMask Package Id");
                }
            }

            if (GUILayout.Button("Add Layer"))
                AddRow(layers, "Add MxAnimation Layer", InitializeLayerRow);
        }

        private void DrawBlendRows()
        {
            SerializedProperty blend1D = _serializedRegistry.FindProperty("blend1DDefinitions");
            SerializedProperty blend2D = _serializedRegistry.FindProperty("blend2DDefinitions");
            int blendCount = (blend1D != null ? blend1D.arraySize : 0) + (blend2D != null ? blend2D.arraySize : 0);
            _showBlendRows = EditorGUILayout.Foldout(
                _showBlendRows,
                "Blend Definitions (" + blendCount.ToString(CultureInfo.InvariantCulture) + ")",
                true);
            if (!_showBlendRows || (blend1D == null && blend2D == null))
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawBlend1DRows(blend1D);
                DrawBlend2DRows(blend2D);
            }
        }

        private void DrawBlend1DRows(SerializedProperty blends)
        {
            if (blends == null)
                return;

            EditorGUILayout.LabelField("1D Blends", EditorStyles.boldLabel);
            if (blends.arraySize == 0)
                EditorGUILayout.HelpBox("No 1D blends are registered.", MessageType.None);

            for (int i = 0; i < blends.arraySize; i++)
            {
                SerializedProperty row = blends.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawRowActions(blends, i, "1D Blend", InitializeBlend1DRow);
                    DrawRelative(row, "blendId", "Blend Id");
                    DrawRelative(row, "parameterId", "Parameter Id");
                    DrawRelative(row, "layerId", "Layer Id");
                    DrawRelative(row, "parameterScale", "Parameter Scale");
                    DrawRelative(row, "fadeDurationSeconds", "Fade Seconds");
                    DrawBlend1DPointRows(row.FindPropertyRelative("points"));
                }
            }

            if (GUILayout.Button("Add 1D Blend"))
                AddRow(blends, "Add MxAnimation 1D Blend", InitializeBlend1DRow);
        }

        private void DrawBlend1DPointRows(SerializedProperty points)
        {
            if (points == null)
                return;

            EditorGUILayout.LabelField("Points", EditorStyles.miniBoldLabel);
            if (points.arraySize == 0)
                EditorGUILayout.HelpBox("No 1D points are registered.", MessageType.None);

            for (int i = 0; i < points.arraySize; i++)
            {
                SerializedProperty point = points.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawRowActions(points, i, "1D Point", InitializeBlend1DPointRow);
                    DrawRelative(point, "threshold", "Threshold");
                    DrawRelative(point, "clipId", "Clip Id");
                    DrawRelative(point, "playbackSpeed", "Speed");
                    DrawRelative(point, "loop", "Loop");
                }
            }

            if (GUILayout.Button("Add 1D Point"))
                AddRow(points, "Add MxAnimation 1D Blend Point", InitializeBlend1DPointRow);
        }

        private void DrawBlend2DRows(SerializedProperty blends)
        {
            if (blends == null)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("2D Blends", EditorStyles.boldLabel);
            if (blends.arraySize == 0)
                EditorGUILayout.HelpBox("No 2D blends are registered.", MessageType.None);

            for (int i = 0; i < blends.arraySize; i++)
            {
                SerializedProperty row = blends.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawRowActions(blends, i, "2D Blend", InitializeBlend2DRow);
                    DrawRelative(row, "blendId", "Blend Id");
                    DrawRelative(row, "parameterXId", "Parameter X Id");
                    DrawRelative(row, "parameterYId", "Parameter Y Id");
                    DrawRelative(row, "layerId", "Layer Id");
                    DrawRelative(row, "parameterXScale", "Parameter X Scale");
                    DrawRelative(row, "parameterYScale", "Parameter Y Scale");
                    DrawRelative(row, "fadeDurationSeconds", "Fade Seconds");
                    DrawBlend2DPointRows(row.FindPropertyRelative("points"));
                }
            }

            if (GUILayout.Button("Add 2D Blend"))
                AddRow(blends, "Add MxAnimation 2D Blend", InitializeBlend2DRow);
        }

        private void DrawBlend2DPointRows(SerializedProperty points)
        {
            if (points == null)
                return;

            EditorGUILayout.LabelField("Points", EditorStyles.miniBoldLabel);
            if (points.arraySize == 0)
                EditorGUILayout.HelpBox("No 2D points are registered.", MessageType.None);

            for (int i = 0; i < points.arraySize; i++)
            {
                SerializedProperty point = points.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawRowActions(points, i, "2D Point", InitializeBlend2DPointRow);
                    DrawRelative(point, "x", "X");
                    DrawRelative(point, "y", "Y");
                    DrawRelative(point, "clipId", "Clip Id");
                    DrawRelative(point, "playbackSpeed", "Speed");
                    DrawRelative(point, "loop", "Loop");
                }
            }

            if (GUILayout.Button("Add 2D Point"))
                AddRow(points, "Add MxAnimation 2D Blend Point", InitializeBlend2DPointRow);
        }

        private void DrawCompatibilityPanel()
        {
            _showCompatibilityPanel = EditorGUILayout.Foldout(_showCompatibilityPanel, "Compatibility Profile", true);
            if (!_showCompatibilityPanel)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                _compatibilitySkeletonRoot = (GameObject)EditorGUILayout.ObjectField(
                    "Skeleton Root",
                    _compatibilitySkeletonRoot,
                    typeof(GameObject),
                    true);
                _compatibilityProfileId = EditorGUILayout.TextField("Profile Id", _compatibilityProfileId);
                EditorGUILayout.LabelField("Socket Paths");
                _compatibilitySocketPaths = EditorGUILayout.TextArea(_compatibilitySocketPaths, GUILayout.MinHeight(48f));
                if (EditorGUI.EndChangeCheck())
                {
                    _lastBatchBakeReport = null;
                    _lastCompatibilityReport = null;
                    _lastPackageBuildResult = null;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Compatibility"))
                        RefreshCompatibilityReport(logToConsole: true);

                    using (new EditorGUI.DisabledScope(_lastCompatibilityReport == null || string.IsNullOrEmpty(_lastCompatibilityReport.ReportText)))
                    {
                        if (GUILayout.Button("Copy Compatibility Report"))
                            EditorGUIUtility.systemCopyBuffer = _lastCompatibilityReport.ReportText;
                        if (GUILayout.Button("Export Compatibility Report"))
                            ExportTextFile("Export MxAnimation Compatibility Report", "MxAnimationCompatibilityReport.txt", _lastCompatibilityReport.ReportText);
                    }
                }

                if (_lastCompatibilityReport == null)
                    RefreshCompatibilityReport(logToConsole: false);

                if (_lastCompatibilityReport != null)
                {
                    EditorGUILayout.HelpBox(
                        _lastCompatibilityReport.Success
                            ? "Compatibility profile matches the current skeleton, clip, AvatarMask, and bake freshness inputs."
                            : "Compatibility issues or stale bake inputs were found. See the copyable report below.",
                        _lastCompatibilityReport.Success ? MessageType.Info : MessageType.Warning);
                    EditorGUILayout.TextArea(_lastCompatibilityReport.ReportText, GUILayout.MinHeight(140f));
                }
            }
        }

        private void DrawPackageBuilderPanel()
        {
            _showPackageBuilder = EditorGUILayout.Foldout(_showPackageBuilder, "Animation Package Builder", true);
            if (!_showPackageBuilder)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                _packageProviderSampleKind = (MxAnimationPackageProviderSampleKind)EditorGUILayout.EnumPopup(
                    "Provider Sample",
                    _packageProviderSampleKind);
                _packageVersion = Math.Max(1, EditorGUILayout.IntField("Package Version", _packageVersion <= 0 ? Math.Max(1, _registry.Version) : _packageVersion));
                _packageCatalogId = EditorGUILayout.TextField("Catalog Id Override", _packageCatalogId);
                _packageBundleName = EditorGUILayout.TextField("Bundle Name Override", _packageBundleName);
                if (_packageProviderSampleKind == MxAnimationPackageProviderSampleKind.RemoteBundle)
                {
                    _packageRemoteUrl = EditorGUILayout.TextField("Remote Bundle URL", _packageRemoteUrl);
                    _packageRemoteCacheKey = EditorGUILayout.TextField("Remote Cache Key", _packageRemoteCacheKey);
                    _packageRemoteHash = EditorGUILayout.TextField("Remote Bundle SHA-256", _packageRemoteHash);
                }
                if (EditorGUI.EndChangeCheck())
                    _lastPackageBuildResult = null;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Preview Package Build"))
                        RefreshPackageBuildReport(logToConsole: true);

                    using (new EditorGUI.DisabledScope(_lastPackageBuildResult == null || string.IsNullOrEmpty(_lastPackageBuildResult.ReportText)))
                    {
                        if (GUILayout.Button("Copy Package Report"))
                            EditorGUIUtility.systemCopyBuffer = _lastPackageBuildResult.ReportText;
                        if (GUILayout.Button("Export Package Report"))
                            ExportTextFile("Export MxAnimation Package Build Report", "MxAnimationPackageBuildReport.txt", _lastPackageBuildResult.ReportText);
                    }
                }

                if (_lastPackageBuildResult == null)
                    RefreshPackageBuildReport(logToConsole: false);

                if (_lastPackageBuildResult != null)
                {
                    EditorGUILayout.HelpBox(
                        _lastPackageBuildResult.Success
                            ? "Package expectation and catalog snapshot validate. The report can be passed to warmup with the generated PackageCatalog."
                            : "Package build validation found issues. See the copyable report below.",
                        _lastPackageBuildResult.Success ? MessageType.Info : MessageType.Warning);
                    EditorGUILayout.TextArea(_lastPackageBuildResult.ReportText, GUILayout.MinHeight(150f));
                }
            }
        }

        private void DrawDiagnostics()
        {
            EditorGUILayout.LabelField("Validation / Export Diagnostics", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("Refresh Export Diagnostics"))
                    RefreshReport(logToConsole: true);

                if (string.IsNullOrEmpty(_lastReport))
                    RefreshReport(logToConsole: false);

                EditorGUILayout.HelpBox(_lastReport, MessageType.Info);

                if (GUILayout.Button("Open Timeline Scrubber Preview"))
                    MxAnimationTimelineScrubberPreviewWindow.Open(_registry);
            }
        }

        private void AddTimelineEvent(SerializedProperty events, int maxFrame)
        {
            Undo.RecordObject(_registry, "Add MxAnimation Presentation Event");
            int index = events.arraySize;
            events.InsertArrayElementAtIndex(index);
            SerializedProperty row = events.GetArrayElementAtIndex(index);
            InitializeTimelineEventRow(row, index);
            SetTimelineEventTime(row, maxFrame, applyChanges: false);
            ApplySerializedChanges("Add MxAnimation Presentation Event");
            GUIUtility.ExitGUI();
        }

        private void SetTimelineEventTime(SerializedProperty row, int maxFrame, bool applyChanges = true)
        {
            if (row == null)
                return;

            Undo.RecordObject(_registry, "Set MxAnimation Presentation Event Frame");
            SerializedProperty domain = row.FindPropertyRelative("timeDomain");
            SerializedProperty time = row.FindPropertyRelative("time");
            if (time != null)
            {
                MxAnimationEventTimeDomain timeDomain = domain != null
                    ? (MxAnimationEventTimeDomain)domain.enumValueIndex
                    : MxAnimationEventTimeDomain.PresentationFrame;
                time.floatValue = MxAnimationTimelineEventTimeUtility.FrameToEventTime(
                    timeDomain,
                    _timelineFrame,
                    maxFrame,
                    MxAnimationBakeEditorTool.DefaultSampleTickRate);
            }

            if (applyChanges)
                ApplySerializedChanges("Set MxAnimation Presentation Event Frame");
        }

        private void EnsureTimelinePreview()
        {
            if (_timelinePreviewDirty || _timelinePreview == null)
                RefreshTimelinePreviewInputs();
        }

        private void RefreshTimelinePreviewInputs()
        {
            if (_registry == null)
            {
                _timelineExport = null;
                _timelineBake = null;
                _timelineClip = null;
                _timelineSelector = string.Empty;
                _timelinePreview = null;
                _timelineSummary = string.Empty;
                _timelinePreviewDirty = false;
                return;
            }

            _timelineExport = MxAnimationClipRegistryExporter.ExportStructureOnly(_registry);
            _timelineSelector = ResolveTimelineBindingSelector(_registry.Bindings);
            _timelineClip = ResolveTimelineSelectedClip(_registry, _timelineSelector);
            ResourceKey timelineClipKey = ResolveTimelineSelectedClipKey(_timelineExport.Definition, _timelineSelector);
            _timelineBake = _timelineAutoBakeSelectedClip && _timelineClip != null
                ? MxAnimationBakeEditorTool.BakeClip(_timelineClip, timelineClipKey).Artifact
                : null;
            RebuildTimelinePreview();
            _timelinePreviewDirty = false;
        }

        private void RebuildTimelinePreview()
        {
            if (_timelineExport == null)
            {
                _timelinePreview = null;
                _timelineSummary = string.Empty;
                return;
            }

            _timelinePreview = MxAnimationTimelineScrubberPreviewBuilder.Build(
                _timelineExport.Definition,
                _timelineSelector,
                _timelineFrame,
                _timelineBake,
                _timelineCombatSource,
                _timelineExport.ValidationReport,
                _timelineClip != null);
            _timelineSummary = MxAnimationTimelineScrubberPreviewBuilder.CreateSummary(_timelinePreview);
        }

        private void MarkTimelinePreviewDirty()
        {
            _timelinePreviewDirty = true;
        }

        private void RunBatchBake(IReadOnlyList<int> selectedIndices)
        {
            _lastBatchBakeReport = MxAnimationWorkstationBakeUtility.BakeRegistryClipsToFiles(
                _registry,
                selectedIndices,
                _batchOutputRoot,
                CreateCurrentSkeletonProfile());
            _lastCompatibilityReport = null;
            _lastPackageBuildResult = null;
            Debug.Log(_lastBatchBakeReport.ReportText, _registry);
        }

        private void RefreshCompatibilityReport(bool logToConsole)
        {
            _lastCompatibilityReport = MxAnimationWorkstationBakeUtility.BuildCompatibilityReport(
                _registry,
                _compatibilitySkeletonRoot,
                _compatibilityProfileId,
                ParseSocketPaths(_compatibilitySocketPaths),
                _lastBatchBakeReport);
            if (logToConsole && _lastCompatibilityReport != null)
                Debug.Log(_lastCompatibilityReport.ReportText, _registry);
        }

        private void RefreshPackageBuildReport(bool logToConsole)
        {
            var options = new MxAnimationPackageBuilderOptions(
                packageVersion: _packageVersion <= 0 ? Math.Max(1, _registry.Version) : _packageVersion,
                catalogId: _packageCatalogId,
                providerSampleKind: _packageProviderSampleKind,
                bundleName: _packageBundleName,
                remoteBundleUrl: _packageRemoteUrl,
                remoteCacheKey: _packageRemoteCacheKey,
                remoteBundleHash: _packageRemoteHash);
            RefreshCompatibilityReport(logToConsole: false);
            _lastPackageBuildResult = MxAnimationPackageBuilder.Build(
                _registry,
                options,
                _lastBatchBakeReport,
                _lastCompatibilityReport);
            if (logToConsole && _lastPackageBuildResult != null)
                Debug.Log(_lastPackageBuildResult.ReportText, _registry);
        }

        private MxAnimationSkeletonCompatibilityProfile CreateCurrentSkeletonProfile()
        {
            return _compatibilitySkeletonRoot != null
                ? MxAnimationCompatibilityEditorExtractor.CreateSkeletonProfile(
                    _compatibilitySkeletonRoot,
                    string.IsNullOrWhiteSpace(_compatibilityProfileId) ? "skeleton" : _compatibilityProfileId,
                    ParseSocketPaths(_compatibilitySocketPaths))
                : null;
        }

        private IReadOnlyList<int> SelectedBatchIndices()
        {
            var indices = new List<int>();
            for (int i = 0; i < _batchClipSelected.Length; i++)
            {
                if (_batchClipSelected[i])
                    indices.Add(i);
            }

            return indices;
        }

        private void EnsureBatchSelectionState(int clipCount)
        {
            if (_batchClipSelected != null && _batchClipSelected.Length == clipCount)
                return;

            var next = new bool[Math.Max(0, clipCount)];
            for (int i = 0; i < next.Length; i++)
                next[i] = _batchClipSelected == null || i >= _batchClipSelected.Length || _batchClipSelected[i];
            _batchClipSelected = next;
        }

        private void SetBatchSelection(int clipCount, bool selected)
        {
            EnsureBatchSelectionState(clipCount);
            for (int i = 0; i < _batchClipSelected.Length; i++)
                _batchClipSelected[i] = selected;
        }

        private int ResolveTimelinePreviewMaxFrame(AnimationClip selectedClip)
        {
            int maxFrame = MxAnimationTimelineEventTimeUtility.ResolvePreviewMaxFrame(
                selectedClip != null ? selectedClip.length : 0f,
                MxAnimationBakeEditorTool.DefaultSampleTickRate,
                TryGetTimelineIntProperty(_timelineCombatSource, "TotalFrames", -1));
            return Math.Max(1, maxFrame);
        }

        private int ResolveTimelineScrubMaxFrame(AnimationClip selectedClip, int previewMaxFrame)
        {
            int maxFrame = Math.Max(1, _timelineRangeEnd);
            if (selectedClip != null)
            {
                maxFrame = Math.Max(
                    maxFrame,
                    Mathf.CeilToInt(Mathf.Max(selectedClip.length, 0.0001f) * MxAnimationBakeEditorTool.DefaultSampleTickRate));
            }

            return Math.Max(maxFrame, previewMaxFrame);
        }

        private void ExportTimelineSummaryText()
        {
            ExportTextFile("Export MxAnimation Timeline Text", "MxAnimationTimeline.txt", _timelineSummary);
        }

        private static void ExportTextFile(string title, string defaultName, string text)
        {
            string path = EditorUtility.SaveFilePanel(
                title,
                Application.dataPath,
                defaultName,
                "txt");
            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllText(path, text ?? string.Empty, Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static IReadOnlyList<string> ParseSocketPaths(string text)
        {
            var paths = new List<string>();
            string[] tokens = (text ?? string.Empty).Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string path = tokens[i].Replace('\\', '/').Trim().Trim('/');
                if (string.IsNullOrWhiteSpace(path) || paths.Contains(path))
                    continue;

                paths.Add(path);
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        private string ResolveTimelineBindingSelector(MxAnimationClipRegistryBindingEntry[] bindings)
        {
            if (bindings == null || bindings.Length == 0)
                return string.Empty;

            _timelineBindingIndex = Mathf.Clamp(_timelineBindingIndex, 0, bindings.Length - 1);
            MxAnimationClipRegistryBindingEntry binding = bindings[_timelineBindingIndex];
            return !string.IsNullOrWhiteSpace(binding.BindingId) ? binding.BindingId : binding.ResolveActionKey();
        }

        private static string[] CreateTimelineBindingLabels(MxAnimationClipRegistryBindingEntry[] bindings)
        {
            if (bindings == null || bindings.Length == 0)
                return new[] { "(none)" };

            var labels = new string[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                string action = bindings[i].ResolveActionKey();
                labels[i] = (string.IsNullOrWhiteSpace(bindings[i].BindingId) ? action : bindings[i].BindingId)
                    + " | "
                    + bindings[i].ClipId;
            }

            return labels;
        }

        private static AnimationClip ResolveTimelineSelectedClip(MxAnimationClipRegistryAsset registry, string selector)
        {
            if (registry == null)
                return null;

            MxAnimationClipRegistryBindingEntry binding = default;
            bool foundBinding = false;
            MxAnimationClipRegistryBindingEntry[] bindings = registry.Bindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                if (string.Equals(bindings[i].BindingId, selector, StringComparison.Ordinal)
                    || string.Equals(bindings[i].ResolveActionKey(), selector, StringComparison.Ordinal))
                {
                    binding = bindings[i];
                    foundBinding = true;
                    break;
                }
            }

            if (!foundBinding)
                return null;

            MxAnimationClipRegistryClipEntry[] clips = registry.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (string.Equals(clips[i].ClipId, binding.ClipId, StringComparison.Ordinal))
                    return clips[i].Clip;
            }

            return null;
        }

        private static ResourceKey ResolveTimelineSelectedClipKey(MxAnimationSetDefinition definition, string selector)
        {
            if (definition == null)
                return default;

            IReadOnlyList<MxAnimationActionBinding> actions = definition.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                MxAnimationActionBinding binding = actions[i];
                if (binding == null)
                    continue;
                if (string.Equals(binding.BindingId, selector, StringComparison.Ordinal)
                    || string.Equals(binding.ActionKey, selector, StringComparison.Ordinal))
                {
                    return binding.Clip;
                }
            }

            return default;
        }

        private static int TryGetTimelineIntProperty(object target, string propertyName, int fallback)
        {
            if (target == null)
                return fallback;

            PropertyInfo property = target.GetType().GetProperty(propertyName);
            if (property == null)
                return fallback;

            object value = property.GetValue(target, null);
            return value is int intValue ? intValue : fallback;
        }

        private static bool DrawSectionHeader(bool expanded, string title, SerializedProperty array)
        {
            string count = array != null ? " (" + array.arraySize.ToString(CultureInfo.InvariantCulture) + ")" : " (unavailable)";
            return EditorGUILayout.Foldout(expanded, title + count, true);
        }

        private void DrawRowActions(
            SerializedProperty array,
            int index,
            string label,
            Action<SerializedProperty, int> initializer)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label + " " + index.ToString(CultureInfo.InvariantCulture), EditorStyles.boldLabel);
                if (GUILayout.Button("Duplicate", GUILayout.Width(82f)))
                    DuplicateRow(array, index, label, initializer);
                if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                    RemoveRow(array, index, "Remove MxAnimation " + label);
            }
        }

        private void DrawProperty(string propertyName, string label)
        {
            SerializedProperty property = _serializedRegistry.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private static void DrawRelative(
            SerializedProperty row,
            string propertyName,
            string label,
            bool includeChildren = false)
        {
            SerializedProperty property = row.FindPropertyRelative(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
        }

        private void AddRow(
            SerializedProperty array,
            string undoName,
            Action<SerializedProperty, int> initializer)
        {
            Undo.RecordObject(_registry, undoName);
            int index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            initializer?.Invoke(array.GetArrayElementAtIndex(index), index);
            ApplySerializedChanges(undoName);
            GUIUtility.ExitGUI();
        }

        private void DuplicateRow(
            SerializedProperty array,
            int index,
            string label,
            Action<SerializedProperty, int> initializer)
        {
            Undo.RecordObject(_registry, "Duplicate MxAnimation " + label);
            int insertIndex = Mathf.Clamp(index + 1, 0, array.arraySize);
            array.InsertArrayElementAtIndex(insertIndex);
            if (insertIndex == index)
                initializer?.Invoke(array.GetArrayElementAtIndex(insertIndex), insertIndex);
            ApplySerializedChanges("Duplicate MxAnimation " + label);
            GUIUtility.ExitGUI();
        }

        private void RemoveRow(SerializedProperty array, int index, string undoName)
        {
            Undo.RecordObject(_registry, undoName);
            array.DeleteArrayElementAtIndex(index);
            ApplySerializedChanges(undoName);
            GUIUtility.ExitGUI();
        }

        private void ApplySerializedChanges(string undoName)
        {
            if (_serializedRegistry == null)
                return;

            if (_serializedRegistry.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_registry);
                RefreshReport(logToConsole: false);
                MarkTimelinePreviewDirty();
                _lastBatchBakeReport = null;
                _lastCompatibilityReport = null;
                _lastPackageBuildResult = null;
            }
            Undo.SetCurrentGroupName(undoName);
        }

        private void SetRegistry(MxAnimationClipRegistryAsset registry)
        {
            _registry = registry;
            _serializedRegistry = registry != null ? new SerializedObject(registry) : null;
            _lastReport = string.Empty;
            _lastBatchBakeReport = null;
            _lastCompatibilityReport = null;
            _lastPackageBuildResult = null;
            if (_registry != null)
            {
                _packageVersion = Math.Max(1, _registry.Version);
                RefreshReport(logToConsole: false);
            }
        }

        private void EnsureSerializedObject()
        {
            if (_registry != null && (_serializedRegistry == null || _serializedRegistry.targetObject != _registry))
                _serializedRegistry = new SerializedObject(_registry);
        }

        private void RefreshReport(bool logToConsole)
        {
            if (_registry == null)
            {
                _lastReport = string.Empty;
                return;
            }

            MxAnimationClipRegistryExportResult result = MxAnimationClipRegistryExporter.ExportStructureOnly(_registry);
            _lastReport = MxAnimationClipRegistryExporter.CreateReportText(result);
            if (logToConsole)
                Debug.Log(_lastReport, _registry);
        }

        private void CreateRegistryAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create MxAnimation Clip Registry",
                "MxAnimationClipRegistry",
                "asset",
                "Choose a project asset path for the new clip registry.");
            if (string.IsNullOrEmpty(path))
                return;

            MxAnimationClipRegistryAsset asset = ScriptableObject.CreateInstance<MxAnimationClipRegistryAsset>();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SetRegistry(asset);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void InitializeClipRow(SerializedProperty row, int index)
        {
            SetString(row, "clipId", "clip" + (index + 1).ToString(CultureInfo.InvariantCulture));
            SetString(row, "resourceId", "animation.clip." + (index + 1).ToString(CultureInfo.InvariantCulture));
            SetBool(row, "isDefault", index == 0);
            SetBool(row, "isFallback", index == 0);
        }

        private static void InitializeBindingRow(SerializedProperty row, int index)
        {
            SetString(row, "bindingId", "binding" + (index + 1).ToString(CultureInfo.InvariantCulture));
            SetString(row, "actionKey", "action:" + (index + 1).ToString(CultureInfo.InvariantCulture));
            SetString(row, "clipId", "clip" + (index + 1).ToString(CultureInfo.InvariantCulture));
            SetString(row, "layerId", index == 0 ? "base" : string.Empty);
            SetFloat(row, "playbackSpeed", 1f);
            SetBool(row, "loop", true);
            SetFloat(row, "fadeDurationSeconds", 0.1f);
        }

        private static void InitializeLayerRow(SerializedProperty row, int index)
        {
            SetString(row, "layerId", index == 0 ? "base" : "layer" + (index + 1).ToString(CultureInfo.InvariantCulture));
            SetFloat(row, "defaultWeight", index == 0 ? 1f : 0f);
        }

        private static void InitializeBlend1DRow(SerializedProperty row, int index)
        {
            SetString(row, "blendId", "blend1d" + (index + 1).ToString(CultureInfo.InvariantCulture));
            SetString(row, "parameterId", "locomotion.speed");
            SetString(row, "layerId", "base");
            SetInt(row, "parameterScale", 1000);
            SetFloat(row, "fadeDurationSeconds", 0.1f);
            ClearArray(row, "points");
        }

        private static void InitializeBlend1DPointRow(SerializedProperty row, int index)
        {
            SetInt(row, "threshold", index * 500);
            SetString(row, "clipId", "clip" + (index + 1).ToString(CultureInfo.InvariantCulture));
            SetFloat(row, "playbackSpeed", 1f);
            SetBool(row, "loop", true);
        }

        private static void InitializeBlend2DRow(SerializedProperty row, int index)
        {
            SetString(row, "blendId", "blend2d" + (index + 1).ToString(CultureInfo.InvariantCulture));
            SetString(row, "parameterXId", "move.x");
            SetString(row, "parameterYId", "move.y");
            SetString(row, "layerId", "base");
            SetInt(row, "parameterXScale", 1000);
            SetInt(row, "parameterYScale", 1000);
            SetFloat(row, "fadeDurationSeconds", 0.1f);
            ClearArray(row, "points");
        }

        private static void InitializeBlend2DPointRow(SerializedProperty row, int index)
        {
            SetInt(row, "x", index % 2 == 0 ? 0 : 1000);
            SetInt(row, "y", index < 2 ? 0 : 1000);
            SetString(row, "clipId", "clip" + (index + 1).ToString(CultureInfo.InvariantCulture));
            SetFloat(row, "playbackSpeed", 1f);
            SetBool(row, "loop", true);
        }

        private static void InitializeTimelineEventRow(SerializedProperty row, int index)
        {
            SetString(row, "eventId", "event:" + (index + 1).ToString(CultureInfo.InvariantCulture));
            SetEnum(row, "timeDomain", (int)MxAnimationEventTimeDomain.PresentationFrame);
            SetFloat(row, "time", 0f);
            SetString(row, "eventKind", "VFX");
            SetString(row, "payloadResourceId", string.Empty);
            SetString(row, "payloadTypeId", string.Empty);
            SetString(row, "payloadVariant", string.Empty);
            SetString(row, "payloadPackageId", string.Empty);
            SetString(row, "socket", string.Empty);
            SetString(row, "tag", string.Empty);
            SetEnum(row, "replayPolicy", (int)MxAnimationPresentationEventReplayPolicy.OneShot);
        }

        private static void SetString(SerializedProperty row, string propertyName, string value)
        {
            SerializedProperty property = row.FindPropertyRelative(propertyName);
            if (property != null)
                property.stringValue = value;
        }

        private static void SetFloat(SerializedProperty row, string propertyName, float value)
        {
            SerializedProperty property = row.FindPropertyRelative(propertyName);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetInt(SerializedProperty row, string propertyName, int value)
        {
            SerializedProperty property = row.FindPropertyRelative(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void SetBool(SerializedProperty row, string propertyName, bool value)
        {
            SerializedProperty property = row.FindPropertyRelative(propertyName);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetEnum(SerializedProperty row, string propertyName, int value)
        {
            SerializedProperty property = row.FindPropertyRelative(propertyName);
            if (property != null)
                property.enumValueIndex = value;
        }

        private static void ClearArray(SerializedProperty row, string propertyName)
        {
            SerializedProperty property = row.FindPropertyRelative(propertyName);
            if (property != null && property.isArray)
                property.ClearArray();
        }
    }

    public enum MxAnimationTimelineScrubberDiagnosticSeverity
    {
        Error = 0,
        Warning = 1
    }

    public enum MxAnimationTimelineScrubberRowKind
    {
        PresentationEvent = 0,
        CombatFrameEvent = 1,
        CombatWindow = 2,
        RootMotion = 3,
        Socket = 4,
        WeaponTrace = 5
    }

    public sealed class MxAnimationTimelineScrubberDiagnostic
    {
        public MxAnimationTimelineScrubberDiagnostic(
            MxAnimationTimelineScrubberDiagnosticSeverity severity,
            string code,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public MxAnimationTimelineScrubberDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
    }

    public sealed class MxAnimationTimelineScrubberRow
    {
        public MxAnimationTimelineScrubberRow(
            MxAnimationTimelineScrubberRowKind kind,
            int localFrame,
            string label,
            string details)
        {
            Kind = kind;
            LocalFrame = localFrame;
            Label = label ?? string.Empty;
            Details = details ?? string.Empty;
        }

        public MxAnimationTimelineScrubberRowKind Kind { get; }
        public int LocalFrame { get; }
        public string Label { get; }
        public string Details { get; }
    }

    public sealed class MxAnimationTimelineScrubberPreview
    {
        public MxAnimationTimelineScrubberPreview(
            string setId,
            string bindingId,
            string actionKey,
            string clipKey,
            int localFrame,
            float seconds,
            float normalizedTime,
            int combatFrame,
            int presentationFrame,
            IReadOnlyList<MxAnimationTimelineScrubberRow> rows,
            IReadOnlyList<MxAnimationTimelineScrubberDiagnostic> diagnostics)
        {
            SetId = setId ?? string.Empty;
            BindingId = bindingId ?? string.Empty;
            ActionKey = actionKey ?? string.Empty;
            ClipKey = clipKey ?? string.Empty;
            LocalFrame = localFrame;
            Seconds = seconds;
            NormalizedTime = normalizedTime;
            CombatFrame = combatFrame;
            PresentationFrame = presentationFrame;
            Rows = rows ?? Array.Empty<MxAnimationTimelineScrubberRow>();
            Diagnostics = diagnostics ?? Array.Empty<MxAnimationTimelineScrubberDiagnostic>();
        }

        public string SetId { get; }
        public string BindingId { get; }
        public string ActionKey { get; }
        public string ClipKey { get; }
        public int LocalFrame { get; }
        public float Seconds { get; }
        public float NormalizedTime { get; }
        public int CombatFrame { get; }
        public int PresentationFrame { get; }
        public IReadOnlyList<MxAnimationTimelineScrubberRow> Rows { get; }
        public IReadOnlyList<MxAnimationTimelineScrubberDiagnostic> Diagnostics { get; }
        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Diagnostics.Count; i++)
                {
                    if (Diagnostics[i].Severity == MxAnimationTimelineScrubberDiagnosticSeverity.Error)
                        return true;
                }

                return false;
            }
        }
    }

    public static class MxAnimationTimelineScrubberPreviewBuilder
    {
        public static MxAnimationTimelineScrubberPreview Build(
            MxAnimationSetDefinition definition,
            string actionKeyOrBindingId,
            int localFrame,
            MxAnimationBakeArtifact bakeArtifact = null,
            object combatTimeline = null,
            ResourceCatalogValidationReport exportValidation = null,
            bool selectedClipReferenceAvailable = true)
        {
            var rows = new List<MxAnimationTimelineScrubberRow>();
            var diagnostics = new List<MxAnimationTimelineScrubberDiagnostic>();
            int frame = Math.Max(0, localFrame);
            MxAnimationActionBinding binding = FindBinding(definition, actionKeyOrBindingId);
            int sampleRate = bakeArtifact != null && bakeArtifact.Profile.SampleTickRate > 0
                ? bakeArtifact.Profile.SampleTickRate
                : MxAnimationBakeEditorTool.DefaultSampleTickRate;
            int maxFrame = ResolveMaxFrame(bakeArtifact, combatTimeline);
            float seconds = frame / (float)sampleRate;
            float normalizedTime = maxFrame > 0 ? frame / (float)maxFrame : 0f;

            if (definition == null)
                diagnostics.Add(Error("MissingAnimationSet", "Animation set definition is missing."));
            AddExportDiagnostics(exportValidation, diagnostics);
            if (binding == null)
                diagnostics.Add(Error("MissingActionBinding", "Action binding is missing: " + (actionKeyOrBindingId ?? string.Empty) + "."));
            else if (!binding.Clip.IsValid)
                diagnostics.Add(Error("MissingClip", "Action binding clip key is missing: " + binding.BindingId + "."));
            if (!selectedClipReferenceAvailable)
                diagnostics.Add(Error("MissingClip", "Selected binding has no AnimationClip reference for preview bake: " + (binding != null ? binding.BindingId : actionKeyOrBindingId ?? string.Empty) + "."));
            if (maxFrame >= 0 && frame > maxFrame)
                diagnostics.Add(Warning("FrameOutOfRange", "Scrub frame is outside known timeline range: " + frame.ToString(CultureInfo.InvariantCulture) + " > " + maxFrame.ToString(CultureInfo.InvariantCulture) + "."));

            AddAnimationEvents(definition, binding, frame, sampleRate, maxFrame, rows, diagnostics);
            List<int> combatEventIds = AddCombatTimelineRows(combatTimeline, frame, rows, diagnostics);
            AddBakeRows(binding, bakeArtifact, frame, rows, diagnostics);
            AddTimelineMismatchDiagnostics(rows, combatEventIds, frame, diagnostics);
            rows.Sort(CompareRows);

            return new MxAnimationTimelineScrubberPreview(
                definition != null ? definition.SetId : string.Empty,
                binding != null ? binding.BindingId : string.Empty,
                binding != null ? binding.ActionKey : actionKeyOrBindingId ?? string.Empty,
                binding != null ? binding.Clip.ToString() : string.Empty,
                frame,
                seconds,
                normalizedTime,
                frame,
                frame,
                rows,
                diagnostics);
        }

        private static void AddExportDiagnostics(
            ResourceCatalogValidationReport exportValidation,
            List<MxAnimationTimelineScrubberDiagnostic> diagnostics)
        {
            if (exportValidation == null)
                return;

            for (int i = 0; i < exportValidation.Issues.Count; i++)
            {
                ResourceCatalogValidationIssue issue = exportValidation.Issues[i];
                string code = string.Equals(issue.Code, "ClipReferenceMissing", StringComparison.Ordinal)
                    ? "MissingClip"
                    : issue.Code;
                if (issue.Severity == ResourceCatalogValidationSeverity.Error)
                    diagnostics.Add(Error(code, issue.Message));
                else
                    diagnostics.Add(Warning(code, issue.Message));
            }
        }

        public static string CreateSummary(MxAnimationTimelineScrubberPreview preview)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Timeline Scrubber Preview\n");
            builder.Append("setId: ").Append(preview != null ? preview.SetId : string.Empty).Append('\n');
            builder.Append("binding: ").Append(preview != null ? preview.BindingId : string.Empty).Append('\n');
            builder.Append("action: ").Append(preview != null ? preview.ActionKey : string.Empty).Append('\n');
            builder.Append("frame: ").Append(preview != null ? preview.LocalFrame : 0).Append('\n');
            builder.Append("seconds: ").Append(preview != null ? preview.Seconds.ToString("0.###", CultureInfo.InvariantCulture) : "0").Append('\n');
            builder.Append("normalized: ").Append(preview != null ? preview.NormalizedTime.ToString("0.###", CultureInfo.InvariantCulture) : "0").Append('\n');
            builder.Append("rows:\n");
            if (preview != null)
            {
                for (int i = 0; i < preview.Rows.Count; i++)
                {
                    MxAnimationTimelineScrubberRow row = preview.Rows[i];
                    builder.Append("- ").Append(row.Kind)
                        .Append(" frame=").Append(row.LocalFrame)
                        .Append(" label=").Append(row.Label)
                        .Append(" details=").Append(row.Details)
                        .Append('\n');
                }

                builder.Append("diagnostics:\n");
                if (preview.Diagnostics.Count == 0)
                {
                    builder.Append("- none\n");
                }
                else
                {
                    for (int i = 0; i < preview.Diagnostics.Count; i++)
                    {
                        MxAnimationTimelineScrubberDiagnostic diagnostic = preview.Diagnostics[i];
                        builder.Append("- ").Append(diagnostic.Severity)
                            .Append(' ').Append(diagnostic.Code)
                            .Append(" message=").Append(diagnostic.Message)
                            .Append('\n');
                    }
                }
            }

            return builder.ToString();
        }

        private static MxAnimationActionBinding FindBinding(MxAnimationSetDefinition definition, string actionKeyOrBindingId)
        {
            if (definition == null || definition.Actions.Count == 0)
                return null;

            if (string.IsNullOrWhiteSpace(actionKeyOrBindingId))
                return definition.Actions[0];

            for (int i = 0; i < definition.Actions.Count; i++)
            {
                MxAnimationActionBinding binding = definition.Actions[i];
                if (binding == null)
                    continue;
                if (string.Equals(binding.ActionKey, actionKeyOrBindingId, StringComparison.Ordinal)
                    || string.Equals(binding.BindingId, actionKeyOrBindingId, StringComparison.Ordinal))
                {
                    return binding;
                }
            }

            return null;
        }

        private static int ResolveMaxFrame(MxAnimationBakeArtifact bakeArtifact, object combatTimeline)
        {
            int maxFrame = -1;
            int combatTotalFrames = TryGetIntProperty(combatTimeline, "TotalFrames", -1);
            if (combatTotalFrames > 0)
                maxFrame = Math.Max(maxFrame, combatTotalFrames - 1);
            if (bakeArtifact != null)
            {
                if (bakeArtifact.RootMotionFrames.Count > 0)
                    maxFrame = Math.Max(maxFrame, bakeArtifact.RootMotionFrames[bakeArtifact.RootMotionFrames.Count - 1].LocalFrame);
                if (bakeArtifact.SocketFrames.Count > 0)
                    maxFrame = Math.Max(maxFrame, bakeArtifact.SocketFrames[bakeArtifact.SocketFrames.Count - 1].LocalFrame);
                if (bakeArtifact.WeaponTraceFrames.Count > 0)
                    maxFrame = Math.Max(maxFrame, bakeArtifact.WeaponTraceFrames[bakeArtifact.WeaponTraceFrames.Count - 1].LocalFrame);
            }

            return maxFrame;
        }

        private static void AddAnimationEvents(
            MxAnimationSetDefinition definition,
            MxAnimationActionBinding binding,
            int frame,
            int sampleRate,
            int maxFrame,
            List<MxAnimationTimelineScrubberRow> rows,
            List<MxAnimationTimelineScrubberDiagnostic> diagnostics)
        {
            if (definition == null)
                return;

            IReadOnlyList<MxAnimationEventTimelineRow> eventRows = MxAnimationEventTimelineBuilder.BuildRows(definition);
            for (int i = 0; i < eventRows.Count; i++)
            {
                MxAnimationEventTimelineRow row = eventRows[i];
                if (!row.SetLevel && binding != null && !string.Equals(row.BindingId, binding.BindingId, StringComparison.Ordinal))
                    continue;

                int resolvedFrame = ResolveEventFrame(row, sampleRate, maxFrame);
                if (maxFrame >= 0 && resolvedFrame > maxFrame)
                    diagnostics.Add(Warning("EventOutOfRange", "Presentation event is outside known range: " + row.EventId + " frame=" + resolvedFrame.ToString(CultureInfo.InvariantCulture) + "."));
                if (resolvedFrame != frame)
                    continue;

                rows.Add(new MxAnimationTimelineScrubberRow(
                    MxAnimationTimelineScrubberRowKind.PresentationEvent,
                    frame,
                    row.EventId,
                    "domain=" + row.TimeDomain + " kind=" + row.EventKind + " payload=" + row.PayloadKey));
            }
        }

        private static int ResolveEventFrame(MxAnimationEventTimelineRow row, int sampleRate, int maxFrame)
        {
            switch (row.TimeDomain)
            {
                case MxAnimationEventTimeDomain.NormalizedTime:
                    return Math.Max(0, (int)Math.Round(row.Time * Math.Max(0, maxFrame), MidpointRounding.AwayFromZero));
                case MxAnimationEventTimeDomain.Seconds:
                    return Math.Max(0, (int)Math.Round(row.Time * sampleRate, MidpointRounding.AwayFromZero));
                default:
                    return Math.Max(0, (int)Math.Round(row.Time, MidpointRounding.AwayFromZero));
            }
        }

        private static List<int> AddCombatTimelineRows(
            object combatTimeline,
            int frame,
            List<MxAnimationTimelineScrubberRow> rows,
            List<MxAnimationTimelineScrubberDiagnostic> diagnostics)
        {
            var combatEventIds = new List<int>();
            if (combatTimeline == null)
            {
                diagnostics.Add(Warning("MissingCombatTimeline", "CombatActionTimeline is missing."));
                return combatEventIds;
            }

            try
            {
                int actionId = TryGetIntProperty(combatTimeline, "ActionId", 0);
                AddCombatRangeRow(combatTimeline, "Startup", frame, rows, diagnostics);
                AddCombatRangeRow(combatTimeline, "Active", frame, rows, diagnostics);
                AddCombatRangeRow(combatTimeline, "Recovery", frame, rows, diagnostics);
                AddCombatWindowRows(combatTimeline, frame, actionId, rows, diagnostics);
                AddCombatAuthoringTraceRows(combatTimeline, frame, actionId, rows);
                AddCombatEventRows(combatTimeline, frame, actionId, rows, combatEventIds, diagnostics);
            }
            catch (Exception ex)
            {
                diagnostics.Add(Warning("CombatTimelineReflectionFailed", "Combat timeline reflection failed: " + ex.GetType().Name + " " + ex.Message));
            }

            return combatEventIds;
        }

        private static void AddCombatRangeRow(
            object combatTimeline,
            string propertyName,
            int frame,
            List<MxAnimationTimelineScrubberRow> rows,
            List<MxAnimationTimelineScrubberDiagnostic> diagnostics)
        {
            object range = GetPropertyValue(combatTimeline, propertyName);
            if (range == null)
            {
                diagnostics.Add(Warning("CombatTimelineRangeMissing", "Combat timeline range is missing: " + propertyName + "."));
                return;
            }

            if (!TryReadRange(range, out int start, out int end) || frame < start || frame > end)
                return;

            rows.Add(new MxAnimationTimelineScrubberRow(
                MxAnimationTimelineScrubberRowKind.CombatWindow,
                frame,
                propertyName,
                "range=" + start.ToString(CultureInfo.InvariantCulture) + "-" + end.ToString(CultureInfo.InvariantCulture)));
        }

        private static void AddCombatWindowRows(
            object combatTimeline,
            int frame,
            int actionId,
            List<MxAnimationTimelineScrubberRow> rows,
            List<MxAnimationTimelineScrubberDiagnostic> diagnostics)
        {
            bool hasWindowCount = HasProperty(combatTimeline, "WindowCount");
            int count = TryGetIntProperty(combatTimeline, "WindowCount", 0);
            MethodInfo getWindow = combatTimeline.GetType().GetMethod("GetWindow", new[] { typeof(int) });
            if (count > 0 && getWindow == null)
            {
                diagnostics.Add(Warning("CombatTimelineReflectionFailed", "CombatActionTimeline.GetWindow could not be reflected."));
                return;
            }

            if (!hasWindowCount && !HasProperty(combatTimeline, "WeaponTraces"))
                diagnostics.Add(Warning("CombatTimelineWindowsUnavailable", "Combat timeline source does not expose WindowCount/GetWindow or WeaponTraces."));

            for (int i = 0; i < count; i++)
            {
                object window = getWindow.Invoke(combatTimeline, new object[] { i });
                object range = GetPropertyValue(window, "Range");
                if (!TryReadRange(range, out int start, out int end) || frame < start || frame > end)
                    continue;

                string kind = Convert.ToString(GetPropertyValue(window, "Kind"), CultureInfo.InvariantCulture);
                int targetActionId = TryGetIntProperty(window, "TargetActionId", 0);
                rows.Add(new MxAnimationTimelineScrubberRow(
                    MxAnimationTimelineScrubberRowKind.CombatWindow,
                    frame,
                    string.IsNullOrEmpty(kind) ? "Window" : kind,
                    "action=" + actionId.ToString(CultureInfo.InvariantCulture)
                    + " range=" + start.ToString(CultureInfo.InvariantCulture) + "-" + end.ToString(CultureInfo.InvariantCulture)
                    + " target=" + targetActionId.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static void AddCombatAuthoringTraceRows(
            object combatTimeline,
            int frame,
            int actionId,
            List<MxAnimationTimelineScrubberRow> rows)
        {
            object traceSource = GetPropertyValue(combatTimeline, "WeaponTraces");
            if (!(traceSource is IEnumerable traces))
                return;

            foreach (object trace in traces)
            {
                object range = GetPropertyValue(trace, "FrameRange");
                if (!TryReadRange(range, out int start, out int end) || frame < start || frame > end)
                    continue;

                int traceId = TryGetIntProperty(trace, "TraceId", 0);
                int sourceOrder = TryGetIntProperty(trace, "SourceOrder", 0);
                rows.Add(new MxAnimationTimelineScrubberRow(
                    MxAnimationTimelineScrubberRowKind.CombatWindow,
                    frame,
                    "WeaponTrace:" + traceId.ToString(CultureInfo.InvariantCulture),
                    "action=" + actionId.ToString(CultureInfo.InvariantCulture)
                    + " range=" + start.ToString(CultureInfo.InvariantCulture) + "-" + end.ToString(CultureInfo.InvariantCulture)
                    + " sourceOrder=" + sourceOrder.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static void AddCombatEventRows(
            object combatTimeline,
            int frame,
            int actionId,
            List<MxAnimationTimelineScrubberRow> rows,
            List<int> combatEventIds,
            List<MxAnimationTimelineScrubberDiagnostic> diagnostics)
        {
            MethodInfo collectEvents = combatTimeline.GetType().GetMethod("CollectEvents");
            if (collectEvents == null)
            {
                diagnostics.Add(Warning("CombatFrameEventsUnavailable", "Combat timeline source does not expose CollectEvents(localFrame, results)."));
                return;
            }

            ParameterInfo[] parameters = collectEvents.GetParameters();
            if (parameters.Length != 2)
            {
                diagnostics.Add(Warning("CombatFrameEventsUnavailable", "Combat timeline CollectEvents signature is not supported."));
                return;
            }

            IList events;
            try
            {
                events = (IList)Activator.CreateInstance(parameters[1].ParameterType);
                collectEvents.Invoke(combatTimeline, new object[] { frame, events });
            }
            catch (Exception ex)
            {
                diagnostics.Add(Warning("CombatTimelineReflectionFailed", "Combat frame event reflection failed: " + ex.GetType().Name + " " + ex.Message));
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                object frameEvent = events[i];
                int eventId = TryGetIntProperty(frameEvent, "EventId", -1);
                int sourceOrder = TryGetIntProperty(frameEvent, "SourceOrder", 0);
                combatEventIds.Add(eventId);
                rows.Add(new MxAnimationTimelineScrubberRow(
                    MxAnimationTimelineScrubberRowKind.CombatFrameEvent,
                    frame,
                    "event:" + eventId.ToString(CultureInfo.InvariantCulture),
                    "action=" + actionId.ToString(CultureInfo.InvariantCulture)
                    + " sourceOrder=" + sourceOrder.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static void AddBakeRows(
            MxAnimationActionBinding binding,
            MxAnimationBakeArtifact bakeArtifact,
            int frame,
            List<MxAnimationTimelineScrubberRow> rows,
            List<MxAnimationTimelineScrubberDiagnostic> diagnostics)
        {
            if (bakeArtifact == null)
            {
                diagnostics.Add(Warning("MissingBakeArtifact", "Bake artifact is missing."));
                return;
            }

            MxAnimationBakeValidationReport report = MxAnimationBakeArtifactValidator.Validate(bakeArtifact);
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Severity == MxAnimationBakeIssueSeverity.Error)
                    diagnostics.Add(Error(report.Issues[i].Code, report.Issues[i].Message));
                else
                    diagnostics.Add(Warning(report.Issues[i].Code, report.Issues[i].Message));
            }

            if (binding != null && binding.Clip.IsValid && !binding.Clip.Equals(bakeArtifact.Profile.SourceClipKey))
            {
                diagnostics.Add(Warning(
                    "BakeSourceClipMismatch",
                    "Bake artifact source clip does not match selected binding clip. expected="
                    + binding.Clip
                    + " actual="
                    + bakeArtifact.Profile.SourceClipKey));
            }

            bool foundBakeFrame = false;
            for (int i = 0; i < bakeArtifact.RootMotionFrames.Count; i++)
            {
                MxAnimationBakedRootMotionFrame root = bakeArtifact.RootMotionFrames[i];
                if (root.LocalFrame != frame)
                    continue;
                foundBakeFrame = true;
                rows.Add(new MxAnimationTimelineScrubberRow(MxAnimationTimelineScrubberRowKind.RootMotion, frame, "root", "position=" + root.RootPosition + " delta=" + root.DeltaPosition));
            }

            for (int i = 0; i < bakeArtifact.SocketFrames.Count; i++)
            {
                MxAnimationBakedSocketFrame socket = bakeArtifact.SocketFrames[i];
                if (socket.LocalFrame != frame)
                    continue;
                foundBakeFrame = true;
                rows.Add(new MxAnimationTimelineScrubberRow(MxAnimationTimelineScrubberRowKind.Socket, frame, socket.SocketId, "path=" + socket.SocketPath + " position=" + socket.Position + " delta=" + socket.DeltaPosition));
            }

            for (int i = 0; i < bakeArtifact.WeaponTraceFrames.Count; i++)
            {
                MxAnimationBakedWeaponTraceFrame trace = bakeArtifact.WeaponTraceFrames[i];
                if (trace.LocalFrame != frame)
                    continue;
                foundBakeFrame = true;
                rows.Add(new MxAnimationTimelineScrubberRow(MxAnimationTimelineScrubberRowKind.WeaponTrace, frame, "trace:" + trace.TraceId.ToString(CultureInfo.InvariantCulture), "socket=" + trace.SocketId + " root=" + trace.RootNow + " tip=" + trace.TipNow));
            }

            if (!foundBakeFrame)
                diagnostics.Add(Warning("BakeFrameMissing", "Bake artifact does not contain samples for frame " + frame.ToString(CultureInfo.InvariantCulture) + "."));
        }

        private static void AddTimelineMismatchDiagnostics(
            IReadOnlyList<MxAnimationTimelineScrubberRow> rows,
            IReadOnlyList<int> combatEventIds,
            int frame,
            List<MxAnimationTimelineScrubberDiagnostic> diagnostics)
        {
            if (combatEventIds.Count == 0)
                return;

            for (int i = 0; i < combatEventIds.Count; i++)
            {
                string expectedA = "event:" + combatEventIds[i].ToString(CultureInfo.InvariantCulture);
                string expectedB = combatEventIds[i].ToString(CultureInfo.InvariantCulture);
                bool matched = false;
                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    MxAnimationTimelineScrubberRow row = rows[rowIndex];
                    if (row.Kind != MxAnimationTimelineScrubberRowKind.PresentationEvent)
                        continue;
                    if (string.Equals(row.Label, expectedA, StringComparison.Ordinal)
                        || string.Equals(row.Label, expectedB, StringComparison.Ordinal))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                    diagnostics.Add(Warning("TimelineFrameMismatch", "Combat frame event has no matching presentation event at frame " + frame.ToString(CultureInfo.InvariantCulture) + ": " + expectedA + "."));
            }
        }

        private static int CompareRows(MxAnimationTimelineScrubberRow left, MxAnimationTimelineScrubberRow right)
        {
            int result = left.LocalFrame.CompareTo(right.LocalFrame);
            if (result != 0)
                return result;
            result = ((int)left.Kind).CompareTo((int)right.Kind);
            if (result != 0)
                return result;
            return string.CompareOrdinal(left.Label, right.Label);
        }

        private static MxAnimationTimelineScrubberDiagnostic Error(string code, string message)
        {
            return new MxAnimationTimelineScrubberDiagnostic(MxAnimationTimelineScrubberDiagnosticSeverity.Error, code, message);
        }

        private static MxAnimationTimelineScrubberDiagnostic Warning(string code, string message)
        {
            return new MxAnimationTimelineScrubberDiagnostic(MxAnimationTimelineScrubberDiagnosticSeverity.Warning, code, message);
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null)
                return null;
            PropertyInfo property = target.GetType().GetProperty(propertyName);
            return property != null ? property.GetValue(target, null) : null;
        }

        private static bool HasProperty(object target, string propertyName)
        {
            return target != null && target.GetType().GetProperty(propertyName) != null;
        }

        private static int TryGetIntProperty(object target, string propertyName, int fallback)
        {
            object value = GetPropertyValue(target, propertyName);
            return value is int intValue ? intValue : fallback;
        }

        private static bool TryReadRange(object range, out int startFrame, out int endFrame)
        {
            startFrame = TryGetIntProperty(range, "StartFrame", 0);
            endFrame = TryGetIntProperty(range, "EndFrame", -1);
            return range != null && endFrame >= startFrame;
        }
    }

    public sealed class MxAnimationTimelineScrubberPreviewWindow : EditorWindow
    {
        private MxAnimationClipRegistryAsset _registry;
        private UnityEngine.Object _combatTimelineSource;
        private int _bindingIndex;
        private int _frame;
        private Vector2 _scroll;
        private MxAnimationTimelineScrubberPreview _preview;
        private string _summary = string.Empty;
        private MxAnimationClipRegistryExportResult _cachedExport;
        private MxAnimationBakeArtifact _cachedBake;
        private AnimationClip _cachedClip;
        private string _cachedSelector = string.Empty;

        [MenuItem("MxFramework/MxAnimation/Timeline Scrubber Preview MVP", priority = 134)]
        public static void Open()
        {
            Open(null);
        }

        public static void Open(MxAnimationClipRegistryAsset registry)
        {
            var window = GetWindow<MxAnimationTimelineScrubberPreviewWindow>("MxAnimation Scrubber");
            if (registry != null)
                window._registry = registry;
            window.RefreshPreviewInputs();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _registry = (MxAnimationClipRegistryAsset)EditorGUILayout.ObjectField("Registry", _registry, typeof(MxAnimationClipRegistryAsset), false);
            if (_registry == null)
            {
                EditorGUILayout.HelpBox("Select a clip registry asset.", MessageType.None);
                return;
            }

            MxAnimationClipRegistryBindingEntry[] bindings = _registry.Bindings;
            string[] labels = CreateBindingLabels(bindings);
            _bindingIndex = Mathf.Clamp(EditorGUILayout.Popup("Action", _bindingIndex, labels), 0, Math.Max(0, labels.Length - 1));
            _combatTimelineSource = EditorGUILayout.ObjectField("Combat Timeline Source", _combatTimelineSource, typeof(UnityEngine.Object), false);
            bool inputChanged = EditorGUI.EndChangeCheck();

            int previousFrame = _frame;
            _frame = Mathf.Max(0, EditorGUILayout.IntSlider("Frame", _frame, 0, ResolveWindowMaxFrame()));
            if (inputChanged)
                RefreshPreviewInputs();
            else if (previousFrame != _frame)
                RebuildPreview();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Preview"))
                    RefreshPreviewInputs();
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_summary)))
                {
                    if (GUILayout.Button("Copy Summary"))
                        EditorGUIUtility.systemCopyBuffer = _summary;
                }
            }

            if (_preview == null)
                RefreshPreviewInputs();
            DrawPreview();
        }

        private void RefreshPreviewInputs()
        {
            if (_registry == null)
            {
                _preview = null;
                _summary = string.Empty;
                _cachedExport = null;
                _cachedBake = null;
                _cachedClip = null;
                _cachedSelector = string.Empty;
                return;
            }

            _cachedExport = MxAnimationClipRegistryExporter.ExportStructureOnly(_registry);
            _cachedSelector = ResolveSelectedBindingSelector(_registry.Bindings);
            _cachedClip = ResolveSelectedClip(_registry, _cachedSelector);
            ResourceKey cachedClipKey = ResolveSelectedClipKey(_cachedExport.Definition, _cachedSelector);
            _cachedBake = _cachedClip != null ? MxAnimationBakeEditorTool.BakeClip(_cachedClip, cachedClipKey).Artifact : null;
            RebuildPreview();
        }

        private void RebuildPreview()
        {
            if (_cachedExport == null)
                return;

            _preview = MxAnimationTimelineScrubberPreviewBuilder.Build(
                _cachedExport.Definition,
                _cachedSelector,
                _frame,
                _cachedBake,
                _combatTimelineSource,
                _cachedExport.ValidationReport,
                _cachedClip != null);
            _summary = MxAnimationTimelineScrubberPreviewBuilder.CreateSummary(_preview);
        }

        private void DrawPreview()
        {
            if (_preview == null)
                return;

            EditorGUILayout.LabelField("Set", _preview.SetId);
            EditorGUILayout.LabelField("Clip", _preview.ClipKey);
            EditorGUILayout.LabelField("Time", _preview.Seconds.ToString("0.###", CultureInfo.InvariantCulture) + "s / " + _preview.NormalizedTime.ToString("0.###", CultureInfo.InvariantCulture));

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Rows", EditorStyles.boldLabel);
            for (int i = 0; i < _preview.Rows.Count; i++)
            {
                MxAnimationTimelineScrubberRow row = _preview.Rows[i];
                EditorGUILayout.LabelField(row.Kind + " | " + row.Label, row.Details);
            }

            EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);
            if (_preview.Diagnostics.Count == 0)
            {
                EditorGUILayout.LabelField("none");
            }
            else
            {
                for (int i = 0; i < _preview.Diagnostics.Count; i++)
                {
                    MxAnimationTimelineScrubberDiagnostic diagnostic = _preview.Diagnostics[i];
                    EditorGUILayout.HelpBox(diagnostic.Code + ": " + diagnostic.Message, diagnostic.Severity == MxAnimationTimelineScrubberDiagnosticSeverity.Error ? MessageType.Error : MessageType.Warning);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private int ResolveWindowMaxFrame()
        {
            AnimationClip clip = _cachedClip != null
                ? _cachedClip
                : ResolveSelectedClip(_registry, ResolveSelectedBindingSelector(_registry.Bindings));
            if (clip == null)
                return Math.Max(1, _frame);

            return Math.Max(1, Mathf.CeilToInt(Mathf.Max(clip.length, 0.0001f) * MxAnimationBakeEditorTool.DefaultSampleTickRate));
        }

        private string ResolveSelectedBindingSelector(MxAnimationClipRegistryBindingEntry[] bindings)
        {
            if (bindings == null || bindings.Length == 0)
                return string.Empty;

            _bindingIndex = Mathf.Clamp(_bindingIndex, 0, bindings.Length - 1);
            MxAnimationClipRegistryBindingEntry binding = bindings[_bindingIndex];
            return !string.IsNullOrWhiteSpace(binding.BindingId) ? binding.BindingId : binding.ResolveActionKey();
        }

        private static string[] CreateBindingLabels(MxAnimationClipRegistryBindingEntry[] bindings)
        {
            if (bindings == null || bindings.Length == 0)
                return new[] { "(none)" };

            var labels = new string[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                string action = bindings[i].ResolveActionKey();
                labels[i] = (string.IsNullOrWhiteSpace(bindings[i].BindingId) ? action : bindings[i].BindingId)
                    + " | "
                    + bindings[i].ClipId;
            }

            return labels;
        }

        private static AnimationClip ResolveSelectedClip(MxAnimationClipRegistryAsset registry, string selector)
        {
            if (registry == null)
                return null;

            MxAnimationClipRegistryBindingEntry binding = default;
            bool foundBinding = false;
            MxAnimationClipRegistryBindingEntry[] bindings = registry.Bindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                if (string.Equals(bindings[i].BindingId, selector, StringComparison.Ordinal)
                    || string.Equals(bindings[i].ResolveActionKey(), selector, StringComparison.Ordinal))
                {
                    binding = bindings[i];
                    foundBinding = true;
                    break;
                }
            }

            if (!foundBinding)
                return null;

            MxAnimationClipRegistryClipEntry[] clips = registry.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (string.Equals(clips[i].ClipId, binding.ClipId, StringComparison.Ordinal))
                    return clips[i].Clip;
            }

            return null;
        }

        private static ResourceKey ResolveSelectedClipKey(MxAnimationSetDefinition definition, string selector)
        {
            if (definition == null)
                return default;

            IReadOnlyList<MxAnimationActionBinding> actions = definition.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                MxAnimationActionBinding binding = actions[i];
                if (binding == null)
                    continue;
                if (string.Equals(binding.BindingId, selector, StringComparison.Ordinal)
                    || string.Equals(binding.ActionKey, selector, StringComparison.Ordinal))
                {
                    return binding.Clip;
                }
            }

            return default;
        }
    }
}
