using System;
using System.Collections.Generic;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Preview;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow : EditorWindow
    {
        private const string MenuPath = "MxFramework/Character/Character Action Authoring";
        private const string ToggleFocusShortcutId =
            "MxFramework/Character Action Authoring/Toggle Focus";
        private const float ShortcutCollapsedWidth = 180f;
        private const float ShortcutCollapsedHeight = 28f;
        private const string StyleSheetPackageRelativePath =
            "Editor/CharacterAction/CharacterActionAuthoringWindow.uss";
        private const int DefaultDurationFrames = 8;
        private const int MaxEditHistory = 64;
        private const string CharacterTestHotReloadMenuPath =
            "MxFramework/CharacterTest/Runtime/Reload Character Actions";

        private CharacterActionAuthoringModel _model;
        private CharacterActionAuthoringExportResult _validation;
        private CharacterActionReferenceDiscoveryResult _referenceDiscovery;
        private AuthoringEditorMode _editorMode = AuthoringEditorMode.CharacterAction;
        private AuthoringTab _tab = AuthoringTab.ActionSet;
        private bool _validationDirty = true;
        private bool _profileDirty;
        private bool _actionSetDirty;
        private bool _actionLibraryDirty;
        private bool _abilityEffectTimelineDirty;
        private string _abilityAuthoringJsonPath =
            CharacterActionProjectileAuthoringStore.DefaultAbilityAuthoringPath;
        private string _selectedAbilityEffectTimelineId = string.Empty;
        private List<AbilityEffectTimelineDraft> _abilityEffectTimelines;
        private Newtonsoft.Json.Linq.JObject _abilityAuthoringRoot;
        private string _profileSourceDirectory = string.Empty;
        private string _actionSetSourceDirectory = string.Empty;
        private string _actionLibrarySourceDirectory = string.Empty;
        private CharacterActionAuthoringSourceEntry[] _profileSources = Array.Empty<CharacterActionAuthoringSourceEntry>();
        private CharacterActionAuthoringSourceEntry[] _actionSetSources = Array.Empty<CharacterActionAuthoringSourceEntry>();
        private CharacterActionAuthoringSourceEntry[] _actionLibrarySources = Array.Empty<CharacterActionAuthoringSourceEntry>();
        private readonly List<CharacterActionAuthoringHistorySnapshot> _undoHistory =
            new List<CharacterActionAuthoringHistorySnapshot>();
        private readonly List<CharacterActionAuthoringHistorySnapshot> _redoHistory =
            new List<CharacterActionAuthoringHistorySnapshot>();
        private readonly Dictionary<string, bool> _foldoutExpanded = new Dictionary<string, bool>();
        private readonly Dictionary<string, CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>
            _pendingMechanicTuningDrafts =
                new Dictionary<string, CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>(
                    StringComparer.Ordinal);
        private static EditorWindow s_lastFocusedGameView;

        private bool _shortcutCollapsed;
        private Rect _shortcutExpandedPosition;
        private Vector2 _shortcutExpandedMinSize;
        private Vector2 _shortcutExpandedMaxSize;
        private bool _shortcutExpandedMaximized;
        private GUIContent _shortcutExpandedTitleContent;

        private bool _shellBuilt;
        private VisualElement _chromeRoot;
        private VisualElement _contentFrame;
        private VisualElement _contentHost;
        private VisualElement _footerBar;
        private Label _dirtyStatusLabel;
        private Label _footerTabLabel;
        private Label _footerActionLabel;
        private Label _footerSourceLabel;

        private string _actionListFilter = string.Empty;

        private VisualElement _bindingsHost;
        private VisualElement _abilityBindingsHost;
        private VisualElement _focusedAbilityBindingElement;
        private int _focusedAbilityId;
        private string _focusedAbilityOpenMessage = string.Empty;
        private bool _focusedAbilityOpenConflict;
        private bool _openRequestReadOnly;
        private string _openRequestSourceHash = string.Empty;
        private string _openRequestId = string.Empty;
        private int _openRequestAbilityId;
        private int _openRequestActionId;
        private Action<CharacterActionAuthoringOpenResult> _openRequestStatusCallback;
        private VisualElement _tracksTabRoot;
        private CharacterActionTimelineView _timelineView;
        private CharacterActionTimelineView _abilityEffectTimelineView;
        private CharacterActionTimelinePlaybackController _timelinePlayback;
        private CharacterActionPreviewRuntimeSession _timelinePreviewSession;
        private VisualElement _timelineInspectorHost;
        private bool _timelineHostConfigured;
        private bool _lifecycleRestoreScheduled;
        private bool _isRestoringAbilityTimelineSelection;

        [MenuItem(MenuPath, priority = 560)]
        public static CharacterActionAuthoringWindow OpenWindow()
        {
            CharacterActionAuthoringWindow window = GetWindow<CharacterActionAuthoringWindow>();
            window.titleContent = new GUIContent("角色动作配置");
            window.minSize = new Vector2(1180f, 700f);
            return window;
        }

        [Shortcut(ToggleFocusShortcutId, KeyCode.C)]
        private static void ToggleFocusShortcut()
        {
            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            if (focusedWindow is CharacterActionAuthoringWindow focusedAuthoringWindow)
            {
                focusedAuthoringWindow.CollapseForShortcut();
                FocusLastGameView();
                return;
            }

            if (IsGameView(focusedWindow))
                s_lastFocusedGameView = focusedWindow;

            CharacterActionAuthoringWindow window = HasOpenInstances<CharacterActionAuthoringWindow>()
                ? GetWindow<CharacterActionAuthoringWindow>()
                : OpenWindow();
            window.RestoreFromShortcutCollapse();
            window.Show();
            window.Focus();
        }

        private void CollapseForShortcut()
        {
            if (_shortcutCollapsed)
                return;

            _shortcutCollapsed = true;
            _shortcutExpandedPosition = position;
            _shortcutExpandedMinSize = minSize;
            _shortcutExpandedMaxSize = maxSize;
            _shortcutExpandedMaximized = maximized;
            _shortcutExpandedTitleContent = titleContent;

            if (docked)
                return;

            maximized = false;
            rootVisualElement.style.display = DisplayStyle.None;
            titleContent = new GUIContent("动作编辑器（C 恢复）");

            Vector2 collapsedSize = new Vector2(ShortcutCollapsedWidth, ShortcutCollapsedHeight);
            minSize = collapsedSize;
            maxSize = collapsedSize;
            MxFramework.Editor.EditorWindowShortcutTray.Add(
                this,
                _shortcutExpandedPosition,
                collapsedSize);
        }

        private void RestoreFromShortcutCollapse()
        {
            if (!_shortcutCollapsed)
                return;

            _shortcutCollapsed = false;
            if (!docked)
            {
                MxFramework.Editor.EditorWindowShortcutTray.Remove(this);
                minSize = _shortcutExpandedMinSize;
                maxSize = _shortcutExpandedMaxSize;
                position = _shortcutExpandedPosition;
                rootVisualElement.style.display = DisplayStyle.Flex;
                titleContent = _shortcutExpandedTitleContent ?? new GUIContent("角色动作配置");
            }

            maximized = _shortcutExpandedMaximized;
        }

        private static void FocusLastGameView()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
                return;

            EditorWindow gameView = IsGameView(s_lastFocusedGameView)
                ? s_lastFocusedGameView
                : GetWindow(gameViewType);
            s_lastFocusedGameView = gameView;
            gameView.Show();
            gameView.Focus();
        }

        private static bool IsGameView(EditorWindow window)
        {
            return window != null
                && string.Equals(window.GetType().FullName, "UnityEditor.GameView", StringComparison.Ordinal);
        }

        public static CharacterActionAuthoringOpenResult OpenForAbilityBinding(CharacterActionAuthoringOpenRequest request)
        {
            return OpenForSelection(request);
        }

        public static CharacterActionAuthoringOpenResult OpenForSelection(CharacterActionAuthoringOpenRequest request)
        {
            CharacterActionAuthoringWindow window = OpenWindow();
            return window.ApplyAbilityBindingOpenRequest(request);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("角色动作配置");
            minSize = new Vector2(1180f, 700f);
            _sessionRestored = false;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            RestoreSessionState();
            ScheduleLifecycleRestore();
        }

        public void CreateGUI()
        {
            RestoreWindowContent(forceRebuildShell: !_shellBuilt);
        }

        private void OnDisable()
        {
            CancelTimelineGestureRefresh();
            MxFramework.Editor.EditorWindowShortcutTray.Remove(this);
            EditorApplication.delayCall -= RestoreWindowContentAfterLifecycle;
            _lifecycleRestoreScheduled = false;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            CancelWorkbenchValidationSchedule();
            SaveSessionState();
            ExitSceneEditMode(restoreEmbeddedPreview: false);
            DisposeTimelinePlayback();
            DisposeTimelinePreviewSession();
            DisposeAbilityEffectTimelineView();
            DisposeWorkbenchPreviewViewport();
            _shellBuilt = false;
            CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
        }

        private void OnBeforeAssemblyReload()
        {
            CancelTimelineGestureRefresh();
            CancelWorkbenchValidationSchedule();
            SaveSessionState();
            ExitSceneEditMode(restoreEmbeddedPreview: false);
            DisposeTimelinePlayback();
            DisposeTimelinePreviewSession();
            DisposeAbilityEffectTimelineView();
            DisposeWorkbenchPreviewViewport();
            _shellBuilt = false;
            CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                case PlayModeStateChange.ExitingPlayMode:
                    SaveSessionState();
                    ExitSceneEditMode(restoreEmbeddedPreview: false);
                    DisposeTimelinePlayback();
                    DisposeTimelinePreviewSession();
                    DisposeAbilityEffectTimelineView();
                    DisposeWorkbenchPreviewViewport();
                    _shellBuilt = false;
                    CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    ExitSceneEditMode(restoreEmbeddedPreview: false);
                    DisposeTimelinePlayback();
                    DisposeTimelinePreviewSession();
                    DisposeAbilityEffectTimelineView();
                    DisposeWorkbenchPreviewViewport();
                    _shellBuilt = false;
                    CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
                    RefreshChrome();
                    Repaint();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
                    ScheduleLifecycleRestore();
                    break;
            }
        }

        private void ScheduleLifecycleRestore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (_lifecycleRestoreScheduled)
                return;

            _lifecycleRestoreScheduled = true;
            EditorApplication.delayCall += RestoreWindowContentAfterLifecycle;
        }

        private void RestoreWindowContentAfterLifecycle()
        {
            _lifecycleRestoreScheduled = false;
            if (this == null)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RestoreWindowContent(forceRebuildShell: !_shellBuilt);
            Repaint();
        }

        private void RestoreWindowContent(bool forceRebuildShell)
        {
            RestoreSessionState();
            CharacterActionAuthoringModel restoredDraft = _model;
            bool hadRestoredDraft = _profileDirty || _actionSetDirty || _actionLibraryDirty;
            RefreshSources(_profileSourceDirectory, _actionSetSourceDirectory, _actionLibrarySourceDirectory);

            if (_documentSession == null && !string.IsNullOrWhiteSpace(_profileSourceDirectory))
            {
                int profileIndex = CharacterActionAuthoringSourceCatalog.FindIndexByDirectory(
                    _profileSources,
                    _profileSourceDirectory);
                if (profileIndex >= 0)
                {
                    string selectedProfile = SessionState.GetString(
                        SessionSelectedProfileStableIdKey,
                        restoredDraft?.Profile?.StableId ?? string.Empty);
                    if (TryLoadDocumentSession(_profileSources[profileIndex], selectedProfile))
                    {
                        bool restoredDocumentDrafts = RestoreDocumentSessionDraftsFromSession();
                        if (!restoredDocumentDrafts)
                        {
                            string selectedActionSet = SessionState.GetString(
                                SessionSelectedActionSetStableIdKey,
                                string.Empty);
                            if (!string.IsNullOrWhiteSpace(selectedActionSet))
                                _documentSession.SelectActionSet(selectedActionSet);
                            int selectedAction = SessionState.GetInt(
                                SessionSelectedActionKey,
                                restoredDraft?.SelectedAction?.ActionId ?? 0);
                            if (selectedAction > 0)
                                _documentSession.SelectAction(selectedAction);
                        }
                        SyncModelFromDocumentSession();
                        if (!restoredDocumentDrafts && hadRestoredDraft && restoredDraft != null)
                            ApplyHistoryModelToDocumentSession(restoredDraft);
                    }
                }
            }

            if (_model == null)
                InitializeModelFromSources();
            else if (_documentSession == null)
                MergeSessionModelWithCurrentSources();

            if (forceRebuildShell)
                _shellBuilt = false;

            if (!_shellBuilt)
                BuildShell();

            RefreshUi(UiRefreshScope.All);
        }

        private enum AuthoringTab
        {
            Profile,
            ActionSet,
            Actions,
            Tracks,
            Projectiles,
            References,
            Validation,
        }

        private enum AuthoringEditorMode
        {
            CharacterAction,
            AbilityEffectTimeline,
        }
    }
}
