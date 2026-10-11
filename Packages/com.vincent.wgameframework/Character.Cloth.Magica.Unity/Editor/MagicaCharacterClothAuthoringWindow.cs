using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    public sealed partial class MagicaCharacterClothAuthoringWindow : EditorWindow
    {
        private const string LastPackageKey = "MxFramework.CharacterCloth.LastPackage";
        private const string RecoveryPackageKey = "MxFramework.CharacterCloth.Recovery.Package";
        private const string RecoveryHashKey = "MxFramework.CharacterCloth.Recovery.Hash";
        private const string RecoveryCleanHashKey = "MxFramework.CharacterCloth.Recovery.CleanHash";
        private const string RecoveryJsonKey = "MxFramework.CharacterCloth.Recovery.Json";
        private const string PrefabRebuildRequiredKeyPrefix = "MxFramework.CharacterCloth.PrefabRebuildRequired.";
        private const string WGamePresetRoot =
            "Packages/com.vincent.wgameframework/Character.Cloth.Magica.Unity/Presets";

        private MagicaCharacterClothAuthoringSession _session;
        private readonly MagicaCharacterClothPreviewSession _preview = new MagicaCharacterClothPreviewSession();
        private MagicaCharacterClothSceneViewEdit _sceneEdit;
        private MagicaCharacterClothBoneSceneOverlay _boneSceneOverlay;
        private readonly List<PackageOption> _packages = new List<PackageOption>();
        private string[] _presetPaths = Array.Empty<string>();
        private string[] _presetLabels = Array.Empty<string>();
        private int _packageIndex = -1;
        private int _clothIndex;
        private int _rootIndex;
        private int _colliderIndex;
        private Vector2 _clothScroll;
        private Vector2 _colliderScroll;
        private Vector2 _detailScroll;
        private bool _showOnlySelected = true;
        private bool _initializing;
        private bool _previewOpenPending;
        private string _lastMotionPreviewDraftHash = string.Empty;
        private bool _showBoneOverlay;
        private MagicaCharacterClothBonePickMode _bonePickMode;

        [MenuItem("MxFramework/Character/Cloth Authoring", priority = 230)]
        public static void Open()
        {
            MagicaCharacterClothAuthoringWindow window = GetWindow<MagicaCharacterClothAuthoringWindow>();
            window.titleContent = new GUIContent("Cloth Authoring");
            window.minSize = new Vector2(1180f, 620f);
            window.Show();
        }

        internal static void OpenPackage(string packagePath)
        {
            MagicaCharacterClothAuthoringWindow window = GetWindow<MagicaCharacterClothAuthoringWindow>();
            window.titleContent = new GUIContent("Cloth Authoring");
            window.minSize = new Vector2(1180f, 620f);
            window.Show();
            window.SelectPackage(packagePath);
        }

        private void SelectPackage(string packagePath)
        {
            RefreshPackages();
            int index = _packages.FindIndex(x => string.Equals(x.Path, Path.GetFullPath(packagePath), StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && ConfirmDiscardOrSave()) LoadPackage(index);
            Focus();
        }

        private void OnEnable()
        {
            minSize = new Vector2(1180f, 620f);
            _initializing = true;
            _session = CreateInstance<MagicaCharacterClothAuthoringSession>();
            _session.hideFlags = HideFlags.HideAndDontSave;
            _sceneEdit = new MagicaCharacterClothSceneViewEdit(
                _session,
                CurrentBodyModel,
                () => _clothIndex,
                () => _colliderIndex,
                () => _showOnlySelected,
                () => _bonePickMode != MagicaCharacterClothBonePickMode.None || _workspaceMode != 1,
                HandleDraftChanged);
            _sceneEdit.Begin();
            _boneSceneOverlay = new MagicaCharacterClothBoneSceneOverlay(
                CurrentBodyModel,
                () => _session?.document,
                () => _showBoneOverlay,
                SelectedRootPath,
                () => _bonePickMode,
                HandleSceneBonePicked,
                CancelSceneBonePick);
            _boneSceneOverlay.Begin();
            Undo.undoRedoPerformed += HandleUndoRedo;
            RefreshPresetOptions();
            RefreshPackages();
            if (!RestoreRecoveryDraft())
                LoadPreferredPackage();
            _initializing = false;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                SchedulePreviewOpen();
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= DelayedOpenPreview;
            _previewOpenPending = false;
            PersistRecoveryDraft();
            Undo.undoRedoPerformed -= HandleUndoRedo;
            _sceneEdit?.Dispose();
            _sceneEdit = null;
            _boneSceneOverlay?.Dispose();
            _boneSceneOverlay = null;
            _preview.Clear();
            if (_session != null) DestroyImmediate(_session);
            _session = null;
        }

        private void OnGUI()
        {
            if (_session == null) return;
            EnsureStyles();
            hasUnsavedChanges = _session.IsDirty;
            saveChangesMessage = "Character Cloth 草稿尚未保存到 Authoring JSON。";
            DrawToolbar();
            DrawStatus();
            DrawMotionPreviewControls();
            DrawUnifiedWorkspace();
            if (_workspaceMode != 2) DrawDiagnostics();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(40f)))
            {
                string[] labels = _packages.ConvertAll(x => x.DisplayName).ToArray();
                bool motionActive = MagicaCharacterClothMotionPreviewController.IsRequested;
                GUILayout.Space(8f);
                GUILayout.Label("Cloth Authoring  /", _toolbarTitleStyle, GUILayout.Width(116f), GUILayout.Height(30f));
                using (new EditorGUI.DisabledScope(motionActive))
                {
                    EditorGUI.BeginChangeCheck();
                    int next = EditorGUILayout.Popup(Mathf.Max(0, _packageIndex), labels, EditorStyles.toolbarPopup, GUILayout.Width(235f));
                    if (EditorGUI.EndChangeCheck() && next != _packageIndex && ConfirmDiscardOrSave()) LoadPackage(next);
                }
                GUILayout.Space(8f);
                bool hasErrors = _session.validation?.HasErrors ?? true;
                bool receiptReady = ReadReceiptReady();
                bool prefabSynced = IsPrefabSynced(receiptReady);
                string draftStatus = !_session.configExists ? "未配置" : _session.IsDirty ? "未保存" : "已保存";
                StatusChip(draftStatus, _session.configExists && !_session.IsDirty, 82f);
                bool baselineReady = receiptReady && !_session.prefabRebuildRequired;
                StatusChip(baselineReady ? "Prefab 已同步" : "Prefab 待重建", baselineReady, 116f);
                if (motionActive)
                {
                    bool magicaReady = MagicaCharacterClothMotionPreviewController.Status
                        .IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0;
                    StatusChip(magicaReady ? "Magica Ready" : "Magica Building", magicaReady, 108f);
                    StatusChip("实时应用 r" + MagicaCharacterClothMotionPreviewController.HotApplyRevision, true, 106f);
                }
                GUILayout.FlexibleSpace();
                bool packageReady = !string.IsNullOrWhiteSpace(_session.packagePath);
                bool canPublish = !motionActive && packageReady && !hasErrors && !prefabSynced;
                string publishLabel = !_session.configExists
                    ? "创建配置并重建"
                    : _session.IsDirty
                    ? "保存并重建"
                    : prefabSynced ? "Prefab 已同步" : "重建 Prefab";
                using (new EditorGUI.DisabledScope(!canPublish))
                using (BackgroundColor(new Color(0.30f, 0.55f, 0.95f)))
                    if (GUILayout.Button(publishLabel, _primaryButtonStyle, GUILayout.Width(116f))) SaveAndRebuildCurrent();
                using (new EditorGUI.DisabledScope(motionActive || !packageReady || hasErrors))
                    if (GUILayout.Button(new GUIContent("▼", "更多保存选项"), EditorStyles.toolbarDropDown, GUILayout.Width(24f)))
                        ShowPublishMenu();
                if (GUILayout.Button("校验", EditorStyles.toolbarButton, GUILayout.Width(58f))) Validate();
                using (new EditorGUI.DisabledScope(motionActive))
                    if (GUILayout.Button(new GUIContent("↻", "重新加载"), EditorStyles.toolbarButton, GUILayout.Width(28f)) && ConfirmDiscardOrSave()) ReloadCurrent();
                bool hasPreviewBaseline = receiptReady && !_session.prefabRebuildRequired;
                bool canPreviewMotion = motionActive
                    || (!EditorApplication.isPlayingOrWillChangePlaymode && hasPreviewBaseline && !hasErrors);
                string motionLabel = motionActive
                    ? MagicaCharacterClothMotionPreviewController.IsActive ? "■ 退出预览" : "正在进入…"
                    : "▶ 运动预览";
                using (new EditorGUI.DisabledScope(!canPreviewMotion))
                using (BackgroundColor(new Color(0.25f, 0.48f, 0.82f)))
                    if (GUILayout.Button(motionLabel, _primaryButtonStyle, GUILayout.Width(104f)))
                    {
                        if (motionActive) MagicaCharacterClothMotionPreviewController.Stop();
                        else StartMotionPreview();
                    }
                GUILayout.Space(6f);
            }
        }

        private void DrawStatus()
        {
            using (new EditorGUILayout.HorizontalScope(_panelStyle, GUILayout.Height(36f)))
            {
                using (ContentColor(new Color(0.48f, 0.90f, 0.52f))) GUILayout.Label("●", GUILayout.Width(16f));
                bool motionActive = MagicaCharacterClothMotionPreviewController.IsRequested;
                GUILayout.Label(
                    motionActive
                        ? "运动预览实时编辑 · Cloth/Collider 草稿自动热应用"
                        : _preview.Refresh()
                            ? "Preview Prefab 已打开 · Scene Handle 可用"
                            : "Preview Prefab 未打开 · Scene Handle 暂停",
                    EditorStyles.label);
                GUILayout.FlexibleSpace();
                _showOnlySelected = GUILayout.Toggle(_showOnlySelected, "只显示当前 Collider", GUILayout.Width(135f));
                if (!_showOnlySelected && GUILayout.Button("显示全部", GUILayout.Width(72f))) SceneView.RepaintAll();
                if (GUILayout.Button("聚焦", GUILayout.Width(58f))) FocusSelectedCollider();
                using (new EditorGUI.DisabledScope(motionActive))
                    if (GUILayout.Button("重新打开预览", GUILayout.Width(95f))) OpenPreview();
            }
            if (!string.IsNullOrWhiteSpace(_session.message)
                && !_session.message.StartsWith("Preview Prefab 已", StringComparison.Ordinal)
                && !_session.message.StartsWith("角色 Cloth 配置已", StringComparison.Ordinal))
            {
                bool failed = _session.message.IndexOf("失败", StringComparison.OrdinalIgnoreCase) >= 0
                    || _session.message.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0;
                EditorGUILayout.HelpBox(_session.message, failed ? MessageType.Error : MessageType.Info);
            }
        }

        private void DrawMotionPreviewControls()
        {
            if (!MagicaCharacterClothMotionPreviewController.IsRequested) return;
            using (new EditorGUILayout.VerticalScope(_panelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("运动预览", _sectionTitleStyle, GUILayout.Width(72f));
                    GUILayout.Label(MagicaCharacterClothMotionPreviewController.Status, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("退出预览", GUILayout.Width(76f)))
                        MagicaCharacterClothMotionPreviewController.Stop();
                }
                if (!MagicaCharacterClothMotionPreviewController.IsActive)
                {
                    EditorGUILayout.HelpBox("正在进入 Play Mode 并创建隔离预览环境…", MessageType.Info);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    string[] clips = MagicaCharacterClothMotionPreviewController.ClipLabels;
                    int nextClip = EditorGUILayout.Popup(
                        "动作",
                        MagicaCharacterClothMotionPreviewController.SelectedClipIndex,
                        clips,
                        GUILayout.MinWidth(220f));
                    if (nextClip != MagicaCharacterClothMotionPreviewController.SelectedClipIndex)
                        MagicaCharacterClothMotionPreviewController.SetSelectedClip(nextClip);

                    string[] presetLabels = { "原地", "前后往返", "左右横移", "启动/急停", "90° 转身", "180° 转身", "Z 字运动" };
                    int preset = EditorGUILayout.Popup(
                        "运动",
                        (int)MagicaCharacterClothMotionPreviewController.MotionPreset,
                        presetLabels,
                        GUILayout.MinWidth(180f));
                    if (preset != (int)MagicaCharacterClothMotionPreviewController.MotionPreset)
                        MagicaCharacterClothMotionPreviewController.SetMotionPreset((MagicaCharacterClothMotionPreset)preset);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                            MagicaCharacterClothMotionPreviewController.IsPlaying ? "Ⅱ 暂停" : "▶ 播放",
                            GUILayout.Width(68f)))
                        MagicaCharacterClothMotionPreviewController.TogglePlayback();
                    if (GUILayout.Button("↺ 重播", GUILayout.Width(68f)))
                        MagicaCharacterClothMotionPreviewController.Restart();
                    if (GUILayout.Button("重置 Cloth", GUILayout.Width(82f)))
                        MagicaCharacterClothMotionPreviewController.ResetCloth();

                    bool loop = GUILayout.Toggle(
                        MagicaCharacterClothMotionPreviewController.Loop,
                        "循环",
                        GUILayout.Width(52f));
                    if (loop != MagicaCharacterClothMotionPreviewController.Loop)
                        MagicaCharacterClothMotionPreviewController.SetLoop(loop);

                    GUILayout.Label("速度", GUILayout.Width(32f));
                    float speed = GUILayout.HorizontalSlider(
                        MagicaCharacterClothMotionPreviewController.PlaybackSpeed,
                        0.1f,
                        2f,
                        GUILayout.Width(130f));
                    if (!Mathf.Approximately(speed, MagicaCharacterClothMotionPreviewController.PlaybackSpeed))
                        MagicaCharacterClothMotionPreviewController.SetPlaybackSpeed(speed);
                    GUILayout.Label(speed.ToString("0.00") + "×", GUILayout.Width(42f));

                    bool colliders = GUILayout.Toggle(
                        MagicaCharacterClothMotionPreviewController.ShowColliders,
                        "Collider",
                        GUILayout.Width(72f));
                    if (colliders != MagicaCharacterClothMotionPreviewController.ShowColliders)
                        MagicaCharacterClothMotionPreviewController.SetShowColliders(colliders);

                    GUILayout.FlexibleSpace();
                    DrawMotionCameraButton("背", MagicaCharacterClothMotionCameraView.Back);
                    DrawMotionCameraButton("前", MagicaCharacterClothMotionCameraView.Front);
                    DrawMotionCameraButton("左", MagicaCharacterClothMotionCameraView.Left);
                    DrawMotionCameraButton("右", MagicaCharacterClothMotionCameraView.Right);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("镜头", GUILayout.Width(32f));
                    DrawMotionCameraModeButton("固定世界", MagicaCharacterClothMotionCameraMode.FixedWorld, 72f);
                    DrawMotionCameraModeButton("跟随角色", MagicaCharacterClothMotionCameraMode.FollowActor, 72f);
                    bool guides = GUILayout.Toggle(
                        MagicaCharacterClothMotionPreviewController.ShowMotionGuides,
                        "网格/路径/刻度",
                        GUILayout.Width(112f));
                    if (guides != MagicaCharacterClothMotionPreviewController.ShowMotionGuides)
                        MagicaCharacterClothMotionPreviewController.SetShowMotionGuides(guides);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("固定镜头用于观察世界位移；跟随镜头用于近看 Cloth。", EditorStyles.miniLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool windEnabled = GUILayout.Toggle(
                        MagicaCharacterClothMotionPreviewController.PreviewWindEnabled,
                        "预览风源",
                        GUILayout.Width(76f));
                    if (windEnabled != MagicaCharacterClothMotionPreviewController.PreviewWindEnabled)
                        MagicaCharacterClothMotionPreviewController.SetPreviewWindEnabled(windEnabled);
                    using (new EditorGUI.DisabledScope(!windEnabled))
                    {
                        float windStrength = EditorGUILayout.Slider(
                            "风速 m/s",
                            MagicaCharacterClothMotionPreviewController.PreviewWindStrength,
                            0f,
                            30f,
                            GUILayout.MinWidth(190f));
                        if (!Mathf.Approximately(windStrength, MagicaCharacterClothMotionPreviewController.PreviewWindStrength))
                            MagicaCharacterClothMotionPreviewController.SetPreviewWindStrength(windStrength);
                        float windYaw = EditorGUILayout.Slider(
                            "风向 Y",
                            MagicaCharacterClothMotionPreviewController.PreviewWindYaw,
                            -180f,
                            180f,
                            GUILayout.MinWidth(170f));
                        if (!Mathf.Approximately(windYaw, MagicaCharacterClothMotionPreviewController.PreviewWindYaw))
                            MagicaCharacterClothMotionPreviewController.SetPreviewWindYaw(windYaw);
                        float windTurbulence = EditorGUILayout.Slider(
                            "风源湍流",
                            MagicaCharacterClothMotionPreviewController.PreviewWindTurbulence,
                            0f,
                            1f,
                            GUILayout.MinWidth(170f));
                        if (!Mathf.Approximately(windTurbulence, MagicaCharacterClothMotionPreviewController.PreviewWindTurbulence))
                            MagicaCharacterClothMotionPreviewController.SetPreviewWindTurbulence(windTurbulence);
                    }
                    using (new EditorGUI.DisabledScope(MagicaCharacterClothMotionPreviewController.IsStressPulseActive))
                        if (GUILayout.Button("压力脉冲", GUILayout.Width(76f)))
                            MagicaCharacterClothMotionPreviewController.TriggerStressPulse();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    float distance = EditorGUILayout.Slider(
                        "位移幅度 (m)",
                        MagicaCharacterClothMotionPreviewController.MotionDistance,
                        0.1f,
                        2f,
                        GUILayout.MinWidth(210f));
                    if (!Mathf.Approximately(distance, MagicaCharacterClothMotionPreviewController.MotionDistance))
                        MagicaCharacterClothMotionPreviewController.SetMotionDistance(distance);
                    float period = EditorGUILayout.Slider(
                        "运动周期 (s)",
                        MagicaCharacterClothMotionPreviewController.MotionPeriod,
                        1f,
                        10f,
                        GUILayout.MinWidth(210f));
                    if (!Mathf.Approximately(period, MagicaCharacterClothMotionPreviewController.MotionPeriod))
                        MagicaCharacterClothMotionPreviewController.SetMotionPeriod(period);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    float maxStop = Mathf.Max(0.05f, MagicaCharacterClothMotionPreviewController.MotionPeriod * 0.45f);
                    float stop = EditorGUILayout.Slider(
                        "停顿时间 (s)",
                        Mathf.Min(MagicaCharacterClothMotionPreviewController.StopDuration, maxStop),
                        0f,
                        maxStop,
                        GUILayout.MinWidth(210f));
                    if (!Mathf.Approximately(stop, MagicaCharacterClothMotionPreviewController.StopDuration))
                        MagicaCharacterClothMotionPreviewController.SetStopDuration(stop);
                    float turn = EditorGUILayout.Slider(
                        "转向速度 (°/s)",
                        MagicaCharacterClothMotionPreviewController.TurnSpeed,
                        15f,
                        360f,
                        GUILayout.MinWidth(210f));
                    if (!Mathf.Approximately(turn, MagicaCharacterClothMotionPreviewController.TurnSpeed))
                        MagicaCharacterClothMotionPreviewController.SetTurnSpeed(turn);
                }

                Vector3 position = MagicaCharacterClothMotionPreviewController.MotionPosition;
                Vector3 velocity = MagicaCharacterClothMotionPreviewController.MotionVelocity;
                Vector3 acceleration = MagicaCharacterClothMotionPreviewController.MotionAcceleration;
                EditorGUILayout.LabelField(
                    $"世界位置 ({position.x:0.00}, {position.y:0.00}, {position.z:0.00}) m"
                    + $"   线速度 {velocity.magnitude:0.00} m/s"
                    + $"   加速度 {acceleration.magnitude:0.00} m/s²"
                    + $"   角速度 {MagicaCharacterClothMotionPreviewController.MotionAngularVelocity:0.0} °/s",
                    EditorStyles.miniLabel);
                if (!string.IsNullOrWhiteSpace(MagicaCharacterClothMotionPreviewController.HotApplyStatus))
                    EditorGUILayout.LabelField(
                        MagicaCharacterClothMotionPreviewController.HotApplyStatus,
                        EditorStyles.miniBoldLabel);
                if (!string.IsNullOrWhiteSpace(MagicaCharacterClothMotionPreviewController.StressPulseStatus))
                    EditorGUILayout.LabelField(
                        MagicaCharacterClothMotionPreviewController.StressPulseStatus,
                        EditorStyles.miniBoldLabel);
                Rect progress = EditorGUILayout.GetControlRect(false, 5f);
                EditorGUI.ProgressBar(progress, MagicaCharacterClothMotionPreviewController.NormalizedTime, string.Empty);
            }
        }

        private static void DrawMotionCameraButton(string label, MagicaCharacterClothMotionCameraView view)
        {
            bool selected = MagicaCharacterClothMotionPreviewController.CameraView == view;
            using (BackgroundColor(selected ? new Color(0.30f, 0.55f, 0.95f) : GUI.backgroundColor))
                if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(28f)))
                    MagicaCharacterClothMotionPreviewController.SetCameraView(view);
        }

        private static void DrawMotionCameraModeButton(
            string label,
            MagicaCharacterClothMotionCameraMode mode,
            float width)
        {
            bool selected = MagicaCharacterClothMotionPreviewController.CameraMode == mode;
            using (BackgroundColor(selected ? new Color(0.30f, 0.55f, 0.95f) : GUI.backgroundColor))
                if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(width)))
                    MagicaCharacterClothMotionPreviewController.SetCameraMode(mode);
        }

        private void DrawClothList()
        {
            using (new EditorGUILayout.VerticalScope(_panelStyle, GUILayout.Width(280f), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Cloth", _sectionTitleStyle);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label((_session.document?.cloths?.Length ?? 0).ToString(), EditorStyles.miniButton, GUILayout.Width(25f));
                }
                _clothSearch = EditorGUILayout.TextField(_clothSearch, EditorStyles.toolbarSearchField);
                _clothScroll = EditorGUILayout.BeginScrollView(_clothScroll);
                MagicaCharacterClothAuthoringEntry[] cloths = _session.document?.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();
                for (int i = 0; i < cloths.Length; i++)
                {
                    MagicaCharacterClothAuthoringEntry cloth = cloths[i];
                    string title = string.IsNullOrWhiteSpace(cloth?.id) ? "<未命名>" : cloth.id;
                    if (!string.IsNullOrWhiteSpace(_clothSearch)
                        && title.IndexOf(_clothSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    int roots = cloth?.rootBonePaths?.Length ?? 0;
                    string preset = PresetDisplayName(cloth?.presetAssetPath);
                    string label = $"<b>{title}</b>\n<size=10>{cloth?.clothType} · {roots} Root Bone · {preset} Preset</size>";
                    bool selected = _clothIndex == i;
                    using (BackgroundColor(selected ? new Color(0.28f, 0.50f, 0.78f) : GUI.backgroundColor))
                    {
                        if (GUILayout.Button(label, selected ? _selectedCardStyle : _cardStyle))
                        {
                            _clothIndex = i; _rootIndex = 0; _colliderIndex = 0; Validate(); SceneView.RepaintAll();
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("＋ Cloth")) AddCloth();
                    GUI.enabled = cloths.Length > 0;
                    if (GUILayout.Button("复制")) DuplicateCloth();
                    if (GUILayout.Button("删除")) DeleteCloth();
                    GUI.enabled = true;
                }
            }
        }

        private void DrawColliderList()
        {
            using (new EditorGUILayout.VerticalScope(_panelStyle, GUILayout.Width(365f), GUILayout.ExpandHeight(true)))
            {
                MagicaCharacterClothAuthoringEntry cloth = SelectedCloth();
                MagicaCharacterClothAuthoringCollider[] colliders = cloth?.colliders ?? Array.Empty<MagicaCharacterClothAuthoringCollider>();
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Colliders", _sectionTitleStyle);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(colliders.Length.ToString(), EditorStyles.miniButton, GUILayout.Width(25f));
                }
                _colliderScroll = EditorGUILayout.BeginScrollView(_colliderScroll);
                for (int i = 0; i < colliders.Length; i++)
                {
                    MagicaCharacterClothAuthoringCollider c = colliders[i];
                    string title = string.IsNullOrWhiteSpace(c?.id) ? "<未命名>" : c.id;
                    string icon = string.Equals(c?.type, "Capsule", StringComparison.Ordinal) ? "◇" : "○";
                    bool warning = string.Equals(c?.id, "head", StringComparison.Ordinal) && colliders.Length == 1;
                    string label = $"<b>{icon}  {title}</b>{(warning ? "      ⚠" : string.Empty)}\n<size=10>{c?.type}    {Leaf(c?.parentBonePath)}</size>";
                    bool selected = _colliderIndex == i;
                    using (BackgroundColor(selected ? new Color(0.28f, 0.50f, 0.78f) : GUI.backgroundColor))
                    {
                        if (GUILayout.Button(label, selected ? _selectedCardStyle : _cardStyle))
                        {
                            _colliderIndex = i; Validate(); SceneView.RepaintAll();
                        }
                    }
                }
                DrawColliderSuggestions();
                EditorGUILayout.EndScrollView();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = cloth != null;
                    if (GUILayout.Button("＋ Sphere")) AddCollider("Sphere");
                    if (GUILayout.Button("◇ Capsule")) AddCollider("Capsule");
                    GUI.enabled = true;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = colliders.Length > 0;
                    if (GUILayout.Button("复制")) DuplicateCollider();
                    if (GUILayout.Button("删除")) DeleteCollider();
                    if (GUILayout.Button("↑")) MoveCollider(-1);
                    if (GUILayout.Button("↓")) MoveCollider(1);
                    GUI.enabled = true;
                }
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("快捷模板", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = cloth != null;
                    if (GUILayout.Button("后脑")) AddColliderTemplate("head_back", "Sphere", "head.x", new Vector3(0f, 0.07f, -0.05f), 0.14f, 0f);
                    if (GUILayout.Button("脖子")) AddColliderTemplate("neck", "Sphere", "neck.x", new Vector3(0f, 0.02f, -0.02f), 0.09f, 0f);
                    if (GUILayout.Button("上背")) AddColliderTemplate("upper_back", "Capsule", "spine_03.x", new Vector3(0f, 0.06f, -0.04f), 0.13f, 0.25f);
                    GUI.enabled = true;
                }
            }
        }

        private void DrawDetails()
        {
            using (new EditorGUILayout.VerticalScope(_panelStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                MagicaCharacterClothAuthoringEntry cloth = SelectedCloth();
                if (cloth == null) EditorGUILayout.HelpBox("请选择或创建 Cloth。", MessageType.Info);
                else
                {
                    MagicaCharacterClothAuthoringCollider collider = SelectedCollider();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Collider Inspector", _sectionTitleStyle);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(collider != null ? cloth.id + " / " + collider.id : cloth.id, EditorStyles.miniButton);
                    }
                    EditorGUILayout.Space(4f);
                    if (collider == null) EditorGUILayout.HelpBox("选择或新增一个 Collider 开始编辑。", MessageType.Info);
                    else DrawOptimizedColliderInspector(collider);

                    EditorGUILayout.Space(6f);
                    _advancedClothExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_advancedClothExpanded, "Cloth 高级设置");
                    if (_advancedClothExpanded)
                    {
                        AssignText("Cloth Id", cloth.id, value => cloth.id = value, "Edit Cloth Id");
                        EditorGUILayout.LabelField("Type", cloth.clothType);
                        AssignText("Connection", cloth.connectionMode, value => cloth.connectionMode = value, "Edit Connection Mode");
                        DrawPresetSelector(cloth);
                        DrawRootBones(cloth);
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();

                    EditorGUILayout.Space(6f);
                    DrawClothParameterOverrides(cloth);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawOptimizedColliderInspector(MagicaCharacterClothAuthoringCollider collider)
        {
            _basicExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_basicExpanded, "基础");
            if (_basicExpanded)
            {
                AssignText("ID", collider.id, value => collider.id = value, "Edit Collider Id");
                int type = string.Equals(collider.type, "Capsule", StringComparison.Ordinal) ? 1 : 0;
                int nextType = EditorGUILayout.Popup("Type", type, new[] { "Sphere", "Capsule" });
                if (nextType != type) Mutate("Change Collider Type", () => collider.type = nextType == 1 ? "Capsule" : "Sphere");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Parent Bone");
                    GUILayout.Label(Leaf(collider.parentBonePath), EditorStyles.textField, GUILayout.MinWidth(110f));
                    if (GUILayout.Button(
                            new GUIContent("相邻骨骼 ▼", "选择当前骨骼的直接父骨或直接子骨；只有一个候选时直接切换。"),
                            GUILayout.Width(86f)))
                        SelectAdjacentColliderBone(collider);
                    if (GUILayout.Button(
                            new GUIContent("全部…", "从完整模型骨架中搜索并选择。"),
                            GUILayout.Width(54f)))
                        ShowBonePicker(value => SetColliderParentBone(collider, value));
                    if (GUILayout.Button("聚焦", GUILayout.Width(48f))) FocusSelectedCollider();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _shapeExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_shapeExpanded, "形状");
            if (_shapeExpanded)
            {
                float radius = EditorGUILayout.Slider("Radius", collider.radius, 0.001f, 0.5f);
                if (!Mathf.Approximately(radius, collider.radius)) Mutate("Edit Collider Radius", () => collider.radius = radius);
                if (string.Equals(collider.type, "Capsule", StringComparison.Ordinal))
                {
                    float endRadius = EditorGUILayout.Slider("End Radius", collider.endRadius, 0.001f, 0.5f);
                    if (!Mathf.Approximately(endRadius, collider.endRadius)) Mutate("Edit Capsule End Radius", () => collider.endRadius = endRadius);
                    AssignFloat("Length", collider.length, value => collider.length = Mathf.Max(0.001f, value), "Edit Capsule Length");
                    float effectiveEndRadius = collider.endRadius > 0f ? collider.endRadius : collider.radius;
                    if (MagicaCharacterClothCapsuleGeometry.HasCollapsedEnd(collider.length, collider.radius, effectiveEndRadius))
                    {
                        float minimum = MagicaCharacterClothCapsuleGeometry.MinimumSeparatedLength(collider.radius, effectiveEndRadius);
                        EditorGUILayout.HelpBox(
                            $"Length 太短，至少一个端部球心会与中心重合，实际形状接近球体。建议 Length 大于 {minimum:0.###}。",
                            MessageType.Warning);
                    }
                    int direction = collider.direction == "X" ? 0 : collider.direction == "Z" ? 2 : 1;
                    int next = EditorGUILayout.Popup("Direction", direction, new[] { "X", "Y", "Z" });
                    if (next != direction) Mutate("Edit Capsule Direction", () => collider.direction = next == 0 ? "X" : next == 2 ? "Z" : "Y");
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _transformExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_transformExpanded, "变换");
            if (_transformExpanded)
            {
                AssignVector("Local Position", collider.localPosition, false, "Edit Collider Position");
                AssignVector("Local Rotation", collider.localRotationEuler, true, "Edit Collider Rotation");
                EditorGUILayout.HelpBox("Scene View 中可直接拖拽位置、旋转；胶囊两侧方块调半径，轴向箭头调总长度。", MessageType.Info);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRootBones(MagicaCharacterClothAuthoringEntry cloth)
        {
            string[] roots = cloth.rootBonePaths ?? Array.Empty<string>();
            EditorGUILayout.LabelField("Root Bones", EditorStyles.boldLabel);
            for (int i = 0; i < roots.Length; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int index = i;
                    bool selected = _rootIndex == i;
                    using (BackgroundColor(selected ? new Color(0.20f, 0.50f, 0.42f) : GUI.backgroundColor))
                        if (GUILayout.Button(Leaf(roots[i]), selected ? _selectedObjectRowStyle : EditorStyles.miniButton, GUILayout.MinWidth(120f)))
                            SelectRootChain(index, roots[index]);
                    if (GUILayout.Button("聚焦", GUILayout.Width(48f))) SelectRootChain(index, roots[index]);
                    if (GUILayout.Button("替换", GUILayout.Width(48f))) ShowBonePicker(value => Mutate("Select Root Bone", () => { roots[index] = value; _rootIndex = index; }));
                    if (GUILayout.Button("-", GUILayout.Width(22f))) { RemoveRootBone(cloth, index); break; }
                }
            }
            if (GUILayout.Button("+ Root Bone", GUILayout.Width(105f))) AddRootBone(cloth);
        }

        private void DrawDiagnostics()
        {
            MagicaCharacterClothValidationResult validation = _session.validation;
            if (validation == null) return;
            using (new EditorGUILayout.VerticalScope(_panelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _diagnosticsExpanded = EditorGUILayout.Foldout(
                        _diagnosticsExpanded,
                        $"Diagnostics · {validation.ErrorCount} 错误 · {validation.WarningCount} 警告",
                        true,
                        _diagnosticHeaderStyle);
                    GUILayout.FlexibleSpace();
                    if (validation.Diagnostics.Count > 0 && GUILayout.Button("定位", GUILayout.Width(52f))) FocusSelectedCollider();
                    if (validation.Diagnostics.Count > 0)
                    {
                        using (BackgroundColor(new Color(0.30f, 0.55f, 0.95f)))
                            if (GUILayout.Button("一键添加建议 Collider", GUILayout.Width(145f))) AddSuggestedColliders();
                    }
                }
                if (_diagnosticsExpanded)
                {
                    int count = Mathf.Min(5, validation.Diagnostics.Count);
                    for (int i = 0; i < count; i++)
                    {
                        MagicaCharacterClothDiagnostic d = validation.Diagnostics[i];
                        string message = d.Code == "MAGCLOTH015"
                            ? "仅配置头部碰撞体，脖子和背部仍可能穿模。"
                            : d.Message;
                        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                        {
                            using (ContentColor(d.Severity == MagicaCharacterClothDiagnosticSeverity.Error
                                       ? new Color(1f, 0.45f, 0.38f)
                                       : new Color(1f, 0.76f, 0.2f)))
                                GUILayout.Label(d.Severity == MagicaCharacterClothDiagnosticSeverity.Error ? "●" : "▲", GUILayout.Width(18f));
                            GUILayout.Label(d.Code, EditorStyles.boldLabel, GUILayout.Width(110f));
                            GUILayout.Label(message, EditorStyles.wordWrappedLabel);
                        }
                    }
                }
            }
        }

        private void AddSuggestedColliders()
        {
            if (!HasCollider("neck"))
                AddColliderTemplate("neck", "Sphere", "neck.x", new Vector3(0f, 0.02f, -0.02f), 0.09f, 0f);
            if (!HasCollider("upper_back"))
                AddColliderTemplate("upper_back", "Capsule", "spine_03.x", new Vector3(0f, 0.06f, -0.04f), 0.13f, 0.25f);
        }

        private void RefreshPackages()
        {
            string previous = _session?.packagePath ?? string.Empty;
            _packages.Clear();
            AddPackageRoot(Path.GetFullPath("Authoring/CharacterPackages"), includeUnconfigured: true);
            AddPackageRoot(Path.GetFullPath("Tools/MxFramework.Authoring/samples"), includeUnconfigured: false);
            _packages.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            _packageIndex = _packages.FindIndex(x => string.Equals(x.Path, previous, StringComparison.OrdinalIgnoreCase));
            if (_packageIndex < 0 && _packages.Count > 0) _packageIndex = 0;
        }

        private void AddPackageRoot(string root, bool includeUnconfigured)
        {
            if (!Directory.Exists(root)) return;
            foreach (string directory in Directory.GetDirectories(root))
            {
                string manifest = Path.Combine(directory, "manifest.json");
                string cloth = Path.Combine(directory, MagicaCharacterClothAuthoringRepository.ConfigRelativePath);
                if (!File.Exists(manifest)) continue;
                bool hasClothConfig = File.Exists(cloth);
                if (!includeUnconfigured && !hasClothConfig) continue;
                try
                {
                    JObject json = JObject.Parse(File.ReadAllText(manifest));
                    string packageId = json["packageId"]?.Value<string>() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(packageId)) continue;
                    string displayName = json["displayName"]?.Value<string>() ?? packageId;
                    _packages.Add(new PackageOption(packageId, displayName, directory, hasClothConfig));
                }
                catch (Newtonsoft.Json.JsonException) { }
            }
        }

        private void LoadPreferredPackage()
        {
            string preferred = EditorPrefs.GetString(LastPackageKey, string.Empty);
            int index = _packages.FindIndex(x => string.Equals(x.Path, preferred, StringComparison.OrdinalIgnoreCase));
            LoadPackage(index >= 0 ? index : _packageIndex);
        }

        private void LoadPackage(int index)
        {
            if (index < 0 || index >= _packages.Count) return;
            try
            {
                PackageOption option = _packages[index];
                _packageIndex = index;
                _session.packagePath = option.Path;
                _session.packageId = option.PackageId;
                _session.configExists = option.HasClothConfig;
                if (option.HasClothConfig)
                {
                    MagicaCharacterClothAuthoringLoadResult load =
                        MagicaCharacterClothAuthoringRepository.Load(option.Path);
                    _session.packagePath = load.PackagePath;
                    _session.loadedHash = load.Hash;
                    _session.document = load.Document;
                    _session.cleanDocumentHash = DocumentHash(load.Document);
                    _session.message = "角色 Cloth 配置已加载。";
                }
                else
                {
                    _session.loadedHash = string.Empty;
                    _session.document = new MagicaCharacterClothAuthoringDocument();
                    _session.cleanDocumentHash = DocumentHash(_session.document);
                    _session.message = "该角色尚未配置 Cloth；已创建空草稿，保存时会写入角色包并登记到 manifest。";
                }
                InvalidateReceiptCache();
                _session.prefabRebuildRequired = SessionState.GetBool(PrefabRebuildRequiredKey(), false);
                _clothIndex = 0; _rootIndex = 0; _colliderIndex = 0;
                EditorPrefs.SetString(LastPackageKey, option.Path);
                Undo.ClearUndo(_session);
                if (!_initializing)
                {
                    OpenPreview();
                    if (!option.HasClothConfig)
                        _session.message = "该角色尚未配置 Cloth；已创建空草稿，保存时会写入角色包并登记到 manifest。";
                }
                Validate(); ClearRecovery(); Repaint();
            }
            catch (Exception exception) { _session.message = exception.Message; }
        }

        private void ReloadCurrent() => LoadPackage(_packageIndex);
        private void OpenPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _session.message = "运动预览期间不会打开 Prefab Stage。";
                return;
            }
            _session.message = _preview.Open(_session.packageId) ? "Preview Prefab 已在 Prefab Stage 打开。" : "Preview Prefab 不可用；可继续编辑 JSON，但 Scene Handle 暂停。";
            Validate();
        }

        private void Validate()
        {
            int repaired = MagicaCharacterClothAuthoringNormalization
                .RepairUnityInlineSerializationPollution(_session.document);
            if (repaired > 0)
            {
                _session.message = "已清理旧版窗口序列化产生的空 Cloth 参数 Override；请重新勾选需要调整的参数。";
                EditorUtility.SetDirty(_session);
                PersistRecoveryDraft();
            }
            Transform body = CurrentBodyModel();
            _session.validation = MagicaCharacterClothAuthoringValidator.Validate(_session.document, body);
            Repaint(); SceneView.RepaintAll();
        }

        private bool SaveCurrent()
        {
            bool changed = _session.IsDirty;
            MagicaCharacterClothAuthoringSaveResult result = MagicaCharacterClothAuthoringRepository.Save(_session.packagePath, _session.document, _session.loadedHash);
            bool recoveredExportReset = false;
            bool overwroteExternalChange = false;
            if (result.Status == MagicaCharacterClothSaveStatus.Conflict)
            {
                if (IsExportResetBaseline(result.CurrentHash))
                {
                    result = MagicaCharacterClothAuthoringRepository.Save(
                        _session.packagePath,
                        _session.document,
                        result.CurrentHash);
                    recoveredExportReset = result.Succeeded;
                }
                else
                {
                    int choice = EditorUtility.DisplayDialogComplex(
                        "Cloth 配置已在磁盘上变化",
                        "当前窗口打开后，角色包中的 Cloth 配置被其他导出或导入流程修改。\n\n"
                        + "选择“覆盖并继续”会用当前窗口中的草稿替换磁盘版本，然后继续重建 Prefab；"
                        + "选择“重新加载磁盘”会放弃当前未保存草稿。",
                        "覆盖并继续",
                        "取消",
                        "重新加载磁盘");
                    if (choice == 0)
                    {
                        result = MagicaCharacterClothAuthoringRepository.Save(
                            _session.packagePath,
                            _session.document,
                            result.CurrentHash);
                        overwroteExternalChange = result.Succeeded;
                    }
                    else if (choice == 2)
                    {
                        ReloadCurrent();
                        _session.message = "已重新加载磁盘上的 Cloth 配置；原窗口草稿未保存。";
                        ShowNotification(new GUIContent("已重新加载磁盘 Cloth 配置。"));
                        return false;
                    }
                    else
                    {
                        _session.message = "保存已取消；当前 Cloth 草稿仍未保存。";
                        ShowNotification(new GUIContent("保存已取消。"));
                        Repaint();
                        return false;
                    }
                }
            }
            _session.message = result.Message;
            if (result.Succeeded)
            {
                _session.configExists = true;
                _session.loadedHash = result.AcceptedHash;
                _session.cleanDocumentHash = DocumentHash(_session.document);
                RefreshPackages();
                ClearRecovery();
                if (changed)
                {
                    SetPrefabRebuildRequired(true);
                    _session.message = recoveredExportReset
                        ? "检测到 WGame 导出已重置磁盘 Cloth；当前草稿已接管新基准并保存，Prefab 尚未重建。"
                        : overwroteExternalChange
                            ? "已用当前 Cloth 草稿覆盖磁盘变化；Prefab 尚未重建。"
                            : "Cloth 配置已保存；Prefab 尚未重建。";
                }
            }
            else
            {
                string failure = result.Status == MagicaCharacterClothSaveStatus.Conflict
                    ? "保存冲突：磁盘配置再次变化，请重试。"
                    : "Cloth 配置保存失败，请查看窗口中的错误信息。";
                ShowNotification(new GUIContent(failure));
            }
            Validate();
            return result.Succeeded;
        }

        private bool IsExportResetBaseline(string expectedCurrentHash)
        {
            try
            {
                MagicaCharacterClothAuthoringLoadResult current =
                    MagicaCharacterClothAuthoringRepository.Load(_session.packagePath);
                MagicaCharacterClothAuthoringDocument document = current.Document;
                return string.Equals(current.Hash, expectedCurrentHash, StringComparison.Ordinal)
                    && document != null
                    && !document.enabled
                    && (document.cloths == null || document.cloths.Length == 0);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void SaveAndRebuildCurrent()
        {
            bool savedNow = _session.IsDirty;
            if (savedNow && !SaveCurrent()) return;
            RebuildCurrent(savedNow);
        }

        private bool RebuildCurrent(bool configurationSaved = false)
        {
            _preview.CloseGeneratedPrefabStage();
            _session.message = configurationSaved
                ? "Cloth 配置已保存，正在重建 Preview 与 Runtime Prefab…"
                : "正在重建 Preview 与 Runtime Prefab…";
            Repaint();

            MagicaCharacterClothRebuildResult result;
            try
            {
                EditorUtility.DisplayProgressBar(
                    "Cloth Authoring",
                    "正在导入角色包并重建 Preview / Runtime Prefab…",
                    0.5f);
                result = MagicaCharacterClothRebuildService.Rebuild(
                    _session.packagePath,
                    _session.packageId);
            }
            catch (Exception exception)
            {
                SetPrefabRebuildRequired(true);
                _session.message = configurationSaved
                    ? "Cloth 配置已保存，但 Prefab 重建发生异常：" + exception.Message
                    : "Prefab 重建发生异常：" + exception.Message;
                Debug.LogException(exception);
                Validate();
                ShowNotification(new GUIContent("Prefab 重建失败，请查看窗口中的错误信息。"));
                Repaint();
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            InvalidateReceiptCache();
            SetPrefabRebuildRequired(!result.Success);
            bool previewOpened = _preview.Open(_session.packageId);
            Validate();
            if (result.Success)
            {
                _session.message = configurationSaved
                    ? "配置已保存并完成 Prefab 重建。" + result.Message
                    : "Prefab 重建完成。" + result.Message;
            }
            else
            {
                _session.message = configurationSaved
                    ? "配置已保存，但 Prefab 重建失败：" + result.Message
                    : "Prefab 重建失败：" + result.Message;
            }
            if (!previewOpened) _session.message += " Preview Prefab 未能重新打开。";
            ShowNotification(new GUIContent(
                result.Success
                    ? $"保存并重建完成：{result.PreviewClothCount} Cloth / {result.PreviewColliderCount} Collider"
                    : "Prefab 重建失败，请查看窗口中的错误信息。"));
            Repaint();
            return result.Success;
        }

        private void ShowPublishMenu()
        {
            var menu = new GenericMenu();
            if (_session.IsDirty)
                menu.AddItem(new GUIContent("仅保存草稿"), false, () => SaveCurrent());
            else
                menu.AddDisabledItem(new GUIContent("仅保存草稿"));
            menu.AddSeparator(string.Empty);
            if (!_session.IsDirty)
                menu.AddItem(new GUIContent("强制重建 Prefab"), false, () => RebuildCurrent());
            else
                menu.AddDisabledItem(new GUIContent("强制重建 Prefab（请先保存）"));
            menu.AddSeparator(string.Empty);
            bool canValidate = !_session.IsDirty
                && IsPrefabSynced(ReadReceiptReady())
                && !MagicaCharacterClothPlayValidationLauncher.IsRunning;
            if (canValidate)
                menu.AddItem(new GUIContent("运行自动验收"), false, StartPlayValidation);
            else
                menu.AddDisabledItem(new GUIContent("运行自动验收（请先同步 Prefab）"));
            menu.ShowAsContext();
        }

        private void StartMotionPreview()
        {
            _lastMotionPreviewDraftHash = string.Empty;
            _preview.CloseGeneratedPrefabStage();
            if (!MagicaCharacterClothMotionPreviewController.Start(_session.packageId, out string error))
            {
                _session.message = "无法启动运动预览：" + error;
                OpenPreview();
                Repaint();
            }
        }

        private void StartPlayValidation()
        {
            _preview.CloseGeneratedPrefabStage();
            _session.message = "正在运行 Cloth PlayMode 验收...";
            try
            {
                MagicaCharacterClothPlayValidationLauncher.Start((_, message) =>
                {
                    if (this == null || _session == null) return;
                    _session.message = message;
                    Repaint();
                });
            }
            catch (Exception exception)
            {
                _session.message = "无法启动 PlayMode 验收：" + exception.Message;
            }
        }

        public override void SaveChanges()
        {
            SaveCurrent();
            if (!_session.IsDirty) base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            ClearRecovery();
            base.DiscardChanges();
        }

        private void HandleDraftChanged() { Validate(); EditorUtility.SetDirty(_session); PersistRecoveryDraft(); SynchronizeMotionPreviewDraft(); }
        private void HandleUndoRedo() { Validate(); PersistRecoveryDraft(); SynchronizeMotionPreviewDraft(); }

        private void OnInspectorUpdate()
        {
            if (!MagicaCharacterClothMotionPreviewController.IsRequested) return;
            SynchronizeMotionPreviewDraft();
            Repaint();
        }

        private void AddCloth()
        {
            Mutate("Add Character Cloth", () =>
            {
                var list = new List<MagicaCharacterClothAuthoringEntry>(_session.document.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>());
                list.Add(new MagicaCharacterClothAuthoringEntry { id = UniqueId("cloth", list.ConvertAll(x => x.id)), rootBonePaths = Array.Empty<string>(), colliders = Array.Empty<MagicaCharacterClothAuthoringCollider>() });
                _session.document.cloths = list.ToArray(); _clothIndex = list.Count - 1; _rootIndex = 0; _colliderIndex = 0;
            });
        }

        private void DuplicateCloth()
        {
            MagicaCharacterClothAuthoringEntry selected = SelectedCloth(); if (selected == null) return;
            Mutate("Duplicate Character Cloth", () =>
            {
                var doc = new MagicaCharacterClothAuthoringDocument { cloths = new[] { selected } };
                MagicaCharacterClothAuthoringEntry copy = doc.Clone().cloths[0];
                var list = new List<MagicaCharacterClothAuthoringEntry>(_session.document.cloths); copy.id = UniqueId(selected.id, list.ConvertAll(x => x.id)); list.Add(copy);
                _session.document.cloths = list.ToArray(); _clothIndex = list.Count - 1; _rootIndex = 0; _colliderIndex = 0;
            });
        }

        private void DeleteCloth()
        {
            if (SelectedCloth() == null || !EditorUtility.DisplayDialog("删除 Cloth", "删除当前 Cloth 草稿？", "删除", "取消")) return;
            Mutate("Delete Character Cloth", () => { var list = new List<MagicaCharacterClothAuthoringEntry>(_session.document.cloths); list.RemoveAt(_clothIndex); _session.document.cloths = list.ToArray(); _clothIndex = Mathf.Clamp(_clothIndex, 0, Math.Max(0, list.Count - 1)); _rootIndex = 0; _colliderIndex = 0; });
        }

        private void AddCollider(string type)
        {
            MagicaCharacterClothAuthoringEntry cloth = SelectedCloth(); if (cloth == null) return;
            Mutate("Add Character Cloth Collider", () =>
            {
                var list = new List<MagicaCharacterClothAuthoringCollider>(cloth.colliders ?? Array.Empty<MagicaCharacterClothAuthoringCollider>());
                string suggested = type == "Capsule" ? "upper_back" : "neck";
                list.Add(new MagicaCharacterClothAuthoringCollider { id = UniqueId(suggested, list.ConvertAll(x => x.id)), type = type, parentBonePath = SuggestBone(type), radius = type == "Capsule" ? 0.08f : 0.1f, endRadius = type == "Capsule" ? 0.08f : 0f, length = type == "Capsule" ? 0.2f : 0f, direction = "Y" });
                cloth.colliders = list.ToArray(); _colliderIndex = list.Count - 1;
            });
        }

        private void DuplicateCollider()
        {
            MagicaCharacterClothAuthoringEntry cloth = SelectedCloth(); MagicaCharacterClothAuthoringCollider selected = SelectedCollider(); if (cloth == null || selected == null) return;
            Mutate("Duplicate Character Cloth Collider", () => { var doc = new MagicaCharacterClothAuthoringDocument { cloths = new[] { new MagicaCharacterClothAuthoringEntry { colliders = new[] { selected } } } }; MagicaCharacterClothAuthoringCollider copy = doc.Clone().cloths[0].colliders[0]; var list = new List<MagicaCharacterClothAuthoringCollider>(cloth.colliders); copy.id = UniqueId(selected.id, list.ConvertAll(x => x.id)); list.Add(copy); cloth.colliders = list.ToArray(); _colliderIndex = list.Count - 1; });
        }

        private void DrawColliderSuggestions()
        {
            bool missingNeck = !HasCollider("neck");
            bool missingBack = !HasCollider("upper_back");
            if (!missingNeck && !missingBack) return;
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("建议补充", EditorStyles.miniBoldLabel);
                if (missingNeck)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("neck · Sphere"); GUILayout.FlexibleSpace();
                        if (GUILayout.Button("＋", GUILayout.Width(30f))) AddColliderTemplate("neck", "Sphere", "neck.x", new Vector3(0f, 0.02f, -0.02f), 0.09f, 0f);
                    }
                }
                if (missingBack)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("upper_back · Capsule"); GUILayout.FlexibleSpace();
                        if (GUILayout.Button("＋", GUILayout.Width(30f))) AddColliderTemplate("upper_back", "Capsule", "spine_03.x", new Vector3(0f, 0.06f, -0.04f), 0.13f, 0.25f);
                    }
                }
            }
        }

        private void AddColliderTemplate(string id, string type, string boneName, Vector3 position, float radius, float length)
        {
            MagicaCharacterClothAuthoringEntry cloth = SelectedCloth();
            Transform body = CurrentBodyModel();
            if (cloth == null || body == null) return;
            Transform bone = MagicaCharacterClothAuthoringValidator.FindTransform(body, boneName, out _);
            if (bone == null) { _session.message = "模板骨骼不存在：" + boneName; return; }
            string path = AnimationUtility.CalculateTransformPath(bone, body).Replace('\\', '/');
            Mutate("Add Character Cloth Collider Template", () =>
            {
                var list = new List<MagicaCharacterClothAuthoringCollider>(cloth.colliders ?? Array.Empty<MagicaCharacterClothAuthoringCollider>());
                list.Add(new MagicaCharacterClothAuthoringCollider
                {
                    id = UniqueId(id, list.ConvertAll(x => x.id)),
                    type = type,
                    parentBonePath = path,
                    localPosition = new MagicaCharacterClothAuthoringVector(position.x, position.y, position.z),
                    radius = radius,
                    endRadius = type == "Capsule" ? radius : 0f,
                    length = length,
                    direction = "Y",
                });
                cloth.colliders = list.ToArray();
                _colliderIndex = list.Count - 1;
            });
            FocusSelectedCollider();
        }

        private void DeleteCollider()
        {
            MagicaCharacterClothAuthoringEntry cloth = SelectedCloth(); if (SelectedCollider() == null || cloth == null || !EditorUtility.DisplayDialog("删除 Collider", "删除当前 Collider 草稿？", "删除", "取消")) return;
            Mutate("Delete Character Cloth Collider", () => { var list = new List<MagicaCharacterClothAuthoringCollider>(cloth.colliders); list.RemoveAt(_colliderIndex); cloth.colliders = list.ToArray(); _colliderIndex = Mathf.Clamp(_colliderIndex, 0, Math.Max(0, list.Count - 1)); });
        }

        private void MoveCollider(int delta)
        {
            MagicaCharacterClothAuthoringEntry cloth = SelectedCloth(); if (cloth?.colliders == null) return; int next = _colliderIndex + delta; if (next < 0 || next >= cloth.colliders.Length) return;
            Mutate("Reorder Character Cloth Collider", () => { (cloth.colliders[_colliderIndex], cloth.colliders[next]) = (cloth.colliders[next], cloth.colliders[_colliderIndex]); _colliderIndex = next; });
        }

        private void AddRootBone(MagicaCharacterClothAuthoringEntry cloth) { ShowBonePicker(value => Mutate("Add Root Bone", () => { var list = new List<string>(cloth.rootBonePaths ?? Array.Empty<string>()) { value }; cloth.rootBonePaths = list.ToArray(); _rootIndex = list.Count - 1; })); }
        private void RemoveRootBone(MagicaCharacterClothAuthoringEntry cloth, int index) => Mutate("Remove Root Bone", () => { var list = new List<string>(cloth.rootBonePaths); list.RemoveAt(index); cloth.rootBonePaths = list.ToArray(); _rootIndex = Mathf.Clamp(_rootIndex, 0, Math.Max(0, list.Count - 1)); });

        private void ShowBonePicker(Action<string> selected)
        {
            Transform body = CurrentBodyModel();
            if (body == null) { _session.message = "请先打开有效 Preview Prefab 或运动预览。"; return; }
            Transform[] all = body.GetComponentsInChildren<Transform>(true);
            var paths = new List<string>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                string path = AnimationUtility.CalculateTransformPath(all[i], body).Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(path)) continue;
                paths.Add(path);
            }
            paths.Sort(StringComparer.Ordinal);
            Vector2 screen = GUIUtility.GUIToScreenPoint(Event.current != null ? Event.current.mousePosition : Vector2.zero);
            PopupWindow.Show(new Rect(screen, Vector2.zero), new MagicaCharacterClothBonePicker(paths, selected));
        }

        private void DrawPresetSelector(MagicaCharacterClothAuthoringEntry cloth)
        {
            if (cloth == null) return;
            if (_presetPaths.Length == 0) RefreshPresetOptions();
            int currentIndex = Array.FindIndex(
                _presetPaths,
                value => string.Equals(value, cloth.presetAssetPath, StringComparison.OrdinalIgnoreCase));
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Preset", _sectionTitleStyle, GUILayout.Width(58f));
                    int shownIndex = Mathf.Max(0, currentIndex);
                    int nextIndex = EditorGUILayout.Popup(shownIndex, _presetLabels);
                    if (nextIndex != shownIndex && nextIndex > 0)
                        ApplyPreset(cloth, _presetPaths[nextIndex]);
                    if (GUILayout.Button(new GUIContent("刷新", "重新扫描 MagicaCloth2 Preset 目录。"), GUILayout.Width(48f)))
                    {
                        RefreshPresetOptions();
                        Repaint();
                    }
                }
                if (currentIndex < 0 && !string.IsNullOrWhiteSpace(cloth.presetAssetPath))
                    EditorGUILayout.HelpBox(
                        "当前 Preset 不在标准目录中：" + cloth.presetAssetPath,
                        MessageType.Warning);
                GUILayout.Label(
                    "Magica 原生 Preset 保留 Override；WGame 快速 Preset 使用完整基线并清除旧 Override。",
                    EditorStyles.miniLabel);
            }
        }

        private void RefreshPresetOptions()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var paths = new List<string> { string.Empty };
            var labels = new List<string> { "选择 Preset…" };
            string[] presetRoots =
            {
                WGamePresetRoot,
                "Assets/MagicaCloth2/Res/Preset",
            };
            for (int rootIndex = 0; rootIndex < presetRoots.Length; rootIndex++)
            {
                string presetRoot = presetRoots[rootIndex];
                string fullRoot = Path.GetFullPath(Path.Combine(projectRoot, presetRoot));
                if (!Directory.Exists(fullRoot)) continue;
                string[] files = Directory.GetFiles(
                    fullRoot,
                    "*.json",
                    SearchOption.TopDirectoryOnly);
                Array.Sort(files, ComparePresetFiles);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string path = presetRoot + "/" + Path.GetFileName(files[fileIndex]);
                    paths.Add(path.Replace('\\', '/'));
                    labels.Add(PresetDisplayName(path));
                }
            }
            _presetPaths = paths.ToArray();
            _presetLabels = labels.ToArray();
        }

        private void ApplyPreset(MagicaCharacterClothAuthoringEntry cloth, string assetPath)
        {
            if (cloth == null || string.IsNullOrWhiteSpace(assetPath)) return;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            if (!File.Exists(fullPath))
            {
                _session.message = "Preset 文件不存在：" + assetPath;
                ShowNotification(new GUIContent("Preset 文件不存在。"));
                return;
            }
            bool quickPreset = IsWGameQuickPreset(assetPath);
            if (quickPreset
                && cloth.parameterOverrides != null
                && !cloth.parameterOverrides.IsEmpty()
                && !EditorUtility.DisplayDialog(
                    "应用 WGame 快速 Preset",
                    "WGame 快速 Preset 已包含完整运动参数。为保证档位准确，将清除当前 Cloth 的参数 Override。\n\n"
                    + "Root、Collider 和 Cloth 分组不会改变。",
                    "应用并清除 Override",
                    "取消"))
                return;
            string hash = MagicaCharacterClothAuthoringRepository.ComputeFileHash(fullPath);
            Mutate("Change Character Cloth Preset", () =>
            {
                cloth.presetAssetPath = assetPath.Replace('\\', '/');
                cloth.presetSha256 = hash;
                if (quickPreset) cloth.parameterOverrides = null;
            });
            _session.message = "Cloth Preset 已切换为 "
                + PresetDisplayName(assetPath)
                + (quickPreset ? "；旧参数 Override 已清除。" : "；现有参数 Override 已保留。");
            ShowNotification(new GUIContent("Preset 已切换并更新 SHA-256。"));
            Repaint();
        }

        private static bool IsWGameQuickPreset(string assetPath) =>
            !string.IsNullOrWhiteSpace(assetPath)
            && assetPath.Replace('\\', '/').StartsWith(
                WGamePresetRoot + "/",
                StringComparison.OrdinalIgnoreCase);

        private static string PresetDisplayName(string assetPath)
        {
            string name = Path.GetFileNameWithoutExtension(assetPath ?? string.Empty);
            switch (name)
            {
                case "WGame_Cloth_Soft": return "WGame 柔软布料";
                case "WGame_Cloth_Medium": return "WGame 中等布料";
                case "WGame_Cloth_Firm": return "WGame 硬挺布料";
                default: return name.Replace("MC2_Preset_", string.Empty);
            }
        }

        private static int ComparePresetFiles(string left, string right)
        {
            int order = PresetSortOrder(left).CompareTo(PresetSortOrder(right));
            return order != 0 ? order : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static int PresetSortOrder(string path)
        {
            switch (Path.GetFileNameWithoutExtension(path ?? string.Empty))
            {
                case "WGame_Cloth_Soft": return 0;
                case "WGame_Cloth_Medium": return 1;
                case "WGame_Cloth_Firm": return 2;
                default: return 100;
            }
        }

        private void SelectAdjacentColliderBone(MagicaCharacterClothAuthoringCollider collider)
        {
            Transform body = CurrentBodyModel();
            if (body == null)
            {
                _session.message = "请先打开有效 Preview Prefab 或运动预览。";
                return;
            }

            Transform current = MagicaCharacterClothAuthoringValidator.FindTransform(
                body,
                collider.parentBonePath,
                out _);
            if (current == null)
            {
                _session.message = "当前 Parent Bone 无法定位，请从完整骨架重新选择。";
                ShowBonePicker(value => SetColliderParentBone(collider, value));
                return;
            }

            var skeleton = new HashSet<Transform>(
                MagicaCharacterClothBoneSceneOverlay.CollectSkeletonBones(body));
            var labels = new List<string>();
            var paths = new List<string>();
            if (current.parent != null && current.parent != body && skeleton.Contains(current.parent))
            {
                labels.Add("↑ 上一级 / " + current.parent.name);
                paths.Add(RelativeBonePath(current.parent, body));
            }
            for (int childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                Transform child = current.GetChild(childIndex);
                if (!skeleton.Contains(child)) continue;
                labels.Add("↓ 下一级 / " + child.name);
                paths.Add(RelativeBonePath(child, body));
            }

            if (paths.Count == 0)
            {
                _session.message = "当前骨骼没有可切换的直接父骨或子骨；请使用“全部…”选择。";
                ShowNotification(new GUIContent("没有相邻骨骼，请使用“全部…”选择。"));
                Repaint();
                return;
            }
            if (paths.Count == 1)
            {
                SetColliderParentBone(collider, paths[0]);
                return;
            }

            var menu = new GenericMenu();
            for (int optionIndex = 0; optionIndex < paths.Count; optionIndex++)
            {
                string optionPath = paths[optionIndex];
                string optionLabel = labels[optionIndex];
                menu.AddItem(
                    new GUIContent(optionLabel),
                    string.Equals(optionPath, collider.parentBonePath, StringComparison.Ordinal),
                    () => SetColliderParentBone(collider, optionPath));
            }
            menu.ShowAsContext();
        }

        private void SetColliderParentBone(
            MagicaCharacterClothAuthoringCollider collider,
            string path)
        {
            if (collider == null
                || string.IsNullOrWhiteSpace(path)
                || string.Equals(collider.parentBonePath, path, StringComparison.Ordinal))
                return;
            Mutate("Select Collider Bone", () => collider.parentBonePath = path);
            _session.message = "Collider Parent Bone 已切换到：" + Leaf(path);
            Repaint();
        }

        private static string RelativeBonePath(Transform bone, Transform body) =>
            AnimationUtility.CalculateTransformPath(bone, body).Replace('\\', '/');

        private void FocusSelectedCollider()
        {
            Transform body = CurrentBodyModel(); MagicaCharacterClothAuthoringCollider collider = SelectedCollider(); if (body == null || collider == null) return;
            Transform parent = MagicaCharacterClothAuthoringValidator.FindTransform(body, collider.parentBonePath, out _);
            if (parent == null) return; Vector3 local = collider.localPosition == null ? Vector3.zero : new Vector3(collider.localPosition.x, collider.localPosition.y, collider.localPosition.z); if (!MagicaCharacterClothMotionPreviewController.IsActive) _preview.Focus(parent); SceneView.lastActiveSceneView?.LookAt(parent.TransformPoint(local));
        }

        private string SuggestBone(string type)
        {
            Transform body = CurrentBodyModel(); if (body == null) return string.Empty;
            string[] candidates = type == "Capsule" ? new[] { "spine_03.x", "neck.x" } : new[] { "neck.x", "head.x" };
            for (int i = 0; i < candidates.Length; i++) { Transform bone = MagicaCharacterClothAuthoringValidator.FindTransform(body, candidates[i], out _); if (bone != null) return AnimationUtility.CalculateTransformPath(bone, body).Replace('\\', '/'); }
            return string.Empty;
        }

        private Transform CurrentBodyModel()
        {
            if (MagicaCharacterClothMotionPreviewController.IsActive)
                return MagicaCharacterClothMotionPreviewController.ActiveBodyModel;
            return _preview.Refresh() ? _preview.BodyModel : null;
        }

        private void SynchronizeMotionPreviewDraft()
        {
            if (!MagicaCharacterClothMotionPreviewController.IsActive || _session?.document == null)
                return;
            if (GUIUtility.hotControl != 0)
                return;
            string hash = DocumentHash(_session.document);
            if (string.Equals(hash, _lastMotionPreviewDraftHash, StringComparison.Ordinal))
                return;
            _lastMotionPreviewDraftHash = hash;
            MagicaCharacterClothMotionPreviewController.RequestHotApply(_session.document);
        }

        private void Mutate(string label, Action mutation) { Undo.RecordObject(_session, label); mutation(); EditorUtility.SetDirty(_session); HandleDraftChanged(); }
        private void AssignText(string label, string current, Action<string> assign, string undo) { string next = EditorGUILayout.TextField(label, current ?? string.Empty); if (!string.Equals(next, current, StringComparison.Ordinal)) Mutate(undo, () => assign(next)); }
        private void AssignFloat(string label, float current, Action<float> assign, string undo) { float next = EditorGUILayout.FloatField(label, current); if (!Mathf.Approximately(next, current)) Mutate(undo, () => assign(next)); }
        private void AssignVector(string label, MagicaCharacterClothAuthoringVector current, bool rotation, string undo)
        {
            current ??= new MagicaCharacterClothAuthoringVector();
            Vector3 value = new Vector3(current.x, current.y, current.z);
            Vector3 next;
            bool clean;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                next = EditorGUILayout.Vector3Field(label, value);
                bool fieldChanged = EditorGUI.EndChangeCheck();
                clean = GUILayout.Button(
                    new GUIContent("清理", "只将接近零的浮点误差归零，不改变有效偏移。"),
                    GUILayout.Width(46f));
                if (!fieldChanged && !clean) return;
            }

            if (clean) next = value;
            next = NormalizeVector(next, rotation);
            if (next == value) return;
            MagicaCharacterClothAuthoringVector target = current;
            Mutate(undo, () => { target.x = next.x; target.y = next.y; target.z = next.z; });
        }

        private static Vector3 NormalizeVector(Vector3 value, bool rotation)
        {
            return rotation
                ? new Vector3(
                    MagicaCharacterClothAuthoringNormalization.NormalizeRotation(value.x),
                    MagicaCharacterClothAuthoringNormalization.NormalizeRotation(value.y),
                    MagicaCharacterClothAuthoringNormalization.NormalizeRotation(value.z))
                : new Vector3(
                    MagicaCharacterClothAuthoringNormalization.NormalizePosition(value.x),
                    MagicaCharacterClothAuthoringNormalization.NormalizePosition(value.y),
                    MagicaCharacterClothAuthoringNormalization.NormalizePosition(value.z));
        }

        private MagicaCharacterClothAuthoringEntry SelectedCloth() { MagicaCharacterClothAuthoringEntry[] array = _session.document?.cloths; return array != null && _clothIndex >= 0 && _clothIndex < array.Length ? array[_clothIndex] : null; }
        private string SelectedRootPath()
        {
            string[] roots = SelectedCloth()?.rootBonePaths;
            return roots != null && _rootIndex >= 0 && _rootIndex < roots.Length
                ? roots[_rootIndex]
                : string.Empty;
        }
        private MagicaCharacterClothAuthoringCollider SelectedCollider() { MagicaCharacterClothAuthoringCollider[] array = SelectedCloth()?.colliders; return array != null && _colliderIndex >= 0 && _colliderIndex < array.Length ? array[_colliderIndex] : null; }

        private bool ConfirmDiscardOrSave()
        {
            if (!_session.IsDirty) return true;
            int choice = EditorUtility.DisplayDialogComplex("未保存的 Cloth 草稿", "当前角色 Cloth 有未保存修改。", "保存", "取消", "放弃");
            if (choice == 0) { SaveCurrent(); return !_session.IsDirty; }
            return choice == 2;
        }

        private void PersistRecoveryDraft()
        {
            if (_session == null || !_session.IsDirty) { ClearRecovery(); return; }
            SessionState.SetString(RecoveryPackageKey, _session.packagePath);
            SessionState.SetString(RecoveryHashKey, _session.loadedHash);
            SessionState.SetString(RecoveryCleanHashKey, _session.cleanDocumentHash);
            SessionState.SetString(RecoveryJsonKey, MagicaCharacterClothAuthoringSerializer.Serialize(_session.document));
        }

        private bool RestoreRecoveryDraft()
        {
            string path = SessionState.GetString(RecoveryPackageKey, string.Empty); string json = SessionState.GetString(RecoveryJsonKey, string.Empty); if (path.Length == 0 || json.Length == 0) return false;
            int index = _packages.FindIndex(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)); if (index < 0) return false;
            _packageIndex = index; _session.packagePath = path; _session.packageId = _packages[index].PackageId; _session.configExists = _packages[index].HasClothConfig; _session.loadedHash = SessionState.GetString(RecoveryHashKey, string.Empty); _session.cleanDocumentHash = SessionState.GetString(RecoveryCleanHashKey, string.Empty); _session.document = MagicaCharacterClothAuthoringSerializer.Deserialize(json); _session.prefabRebuildRequired = SessionState.GetBool(PrefabRebuildRequiredKey(), false); _session.message = "已恢复未保存的 Cloth 草稿。"; Validate(); return true;
        }

        private static void ClearRecovery() { SessionState.EraseString(RecoveryPackageKey); SessionState.EraseString(RecoveryHashKey); SessionState.EraseString(RecoveryCleanHashKey); SessionState.EraseString(RecoveryJsonKey); }
        private static string DocumentHash(MagicaCharacterClothAuthoringDocument document) => MagicaCharacterClothAuthoringRepository.ComputeTextHash(MagicaCharacterClothAuthoringSerializer.Serialize(document));

        private bool IsPrefabSynced(bool receiptReady) =>
            receiptReady && !_session.IsDirty && !_session.prefabRebuildRequired;

        private string PrefabRebuildRequiredKey() =>
            PrefabRebuildRequiredKeyPrefix + (_session?.packageId ?? string.Empty);

        private void SetPrefabRebuildRequired(bool required)
        {
            _session.prefabRebuildRequired = required;
            string key = PrefabRebuildRequiredKey();
            if (string.IsNullOrWhiteSpace(_session.packageId)) return;
            if (required) SessionState.SetBool(key, true);
            else SessionState.EraseBool(key);
        }

        private void SchedulePreviewOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || _previewOpenPending
                || string.IsNullOrWhiteSpace(_session?.packageId)) return;
            _previewOpenPending = true;
            EditorApplication.delayCall -= DelayedOpenPreview;
            EditorApplication.delayCall += DelayedOpenPreview;
        }

        private void DelayedOpenPreview()
        {
            EditorApplication.delayCall -= DelayedOpenPreview;
            _previewOpenPending = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || this == null
                || _session == null
                || string.IsNullOrWhiteSpace(_session.packageId)) return;
            OpenPreview();
            Repaint();
        }
        private static string UniqueId(string seed, List<string> existing) { string baseId = string.IsNullOrWhiteSpace(seed) ? "item" : seed; string value = baseId; int suffix = 2; while (existing.Contains(value)) value = baseId + "_" + suffix++; return value; }
        private static string ShortHash(string value) => string.IsNullOrWhiteSpace(value) ? "hash: -" : "hash: " + value.Substring(Mathf.Min(7, value.Length), Mathf.Min(12, Math.Max(0, value.Length - 7)));

        private readonly struct PackageOption
        {
            internal PackageOption(string packageId, string displayName, string path, bool hasClothConfig)
            {
                PackageId = packageId;
                DisplayNameValue = displayName;
                Path = path;
                HasClothConfig = hasClothConfig;
            }
            internal string PackageId { get; }
            internal string DisplayNameValue { get; }
            internal string Path { get; }
            internal bool HasClothConfig { get; }
            internal string DisplayName =>
                (string.Equals(DisplayNameValue, PackageId, StringComparison.Ordinal)
                    ? PackageId
                    : DisplayNameValue + "  (" + PackageId + ")")
                + (HasClothConfig ? string.Empty : "  [未配置]");
        }
    }
}
