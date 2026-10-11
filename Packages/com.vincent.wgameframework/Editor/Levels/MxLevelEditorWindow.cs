using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Editor.Levels
{
    public sealed partial class MxLevelEditorWindow : EditorWindow
    {
        private const string LeftPaneWidthPrefKey = "MxFramework.LevelEditor.LeftPaneWidth";
        private const string InspectorPaneWidthPrefKey = "MxFramework.LevelEditor.InspectorPaneWidth";
        private const string SceneMarkersEnabledPrefKey = "MxFramework.LevelEditor.SceneEditEnabled";
        private const string ToggleFocusShortcutId =
            "MxFramework/Level Editor/Toggle Focus";
        private const string UnsavedChangesMessage =
            "关卡编辑器中有未保存的更改。\n\n"
            + "选择“保存”会保存所有关卡模块 JSON 和当前 Unity 场景；"
            + "选择“不保存”会重新载入最近保存的场景与配置，"
            + "放弃当前活动场景中的全部未保存改动。";
        private const string StyleSheetPackageRelativePath =
            "Editor/Levels/MxLevelEditorWindow.uss";
        private const string EditorTextSettingsTypeName = "UnityEditor.EditorTextSettings";
        private const float DefaultLeftPaneWidth = 300f;
        private const float DefaultInspectorPaneWidth = 440f;
        private const float MinLeftPaneWidth = 220f;
        private const float MinListPaneWidth = 420f;
        private const float MinInspectorPaneWidth = 360f;
        private const float SceneContextClickMaxMovement = 6f;

        private static MxLevelEditorWindow s_openWindow;
        private static SceneView s_lastFocusedSceneView;

        private int _selectedModuleIndex;
        [SerializeField] private string _selectedWorkspaceId = string.Empty;
        private VisualElement _header;
        private VisualElement _commonToolbar;
        private VisualElement _contentHost;
        private PopupField<string> _modulePopup;
        private readonly List<MxLevelEditorTreeNode> _treeNodes = new List<MxLevelEditorTreeNode>();
        private readonly List<MxLevelEditorTreeNode> _navigationTreeNodes =
            new List<MxLevelEditorTreeNode>();
        private readonly List<MxLevelEditorTreeNode> _groupTreeNodes =
            new List<MxLevelEditorTreeNode>();
        private readonly List<MxLevelEditorListRow> _allListRows = new List<MxLevelEditorListRow>();
        private readonly List<MxLevelEditorListRow> _listRows = new List<MxLevelEditorListRow>();
        private string _selectedTreeNodeId = "document";
        private string _selectedListRowKey = string.Empty;
        private string _rowSearchText = string.Empty;
        private MxLevelEditorListRow _selectedListRow;
        private string _rowSearchStatusText = string.Empty;
        private IMxLevelEditorStructuredModule _structuredModule;
        private IMxLevelEditorSelectionSyncModule _selectionSyncModule;
        private IMxLevelEditorSceneViewModule _sceneViewModule;
        private IMxLevelEditorSceneContextMenuModule _sceneContextMenuModule;
        private IMxLevelEditorPlacementModule _placementModule;
        private IMxLevelEditorWorkspace _activeWorkspace;
        private MxLevelEditorContext _context;
        private ListView _treeView;
        private ListView _groupTreeView;
        private ListView _rowView;
        private TwoPaneSplitView _outerSplitView;
        private TwoPaneSplitView _rightSplitView;
        private TextField _rowSearchField;
        private Label _rowSearchStatusLabel;
        private VisualElement _rangeSummaryHost;
        private VisualElement _inspectorHost;
        private VisualElement _diagnosticsHost;
        private Foldout _diagnosticsFoldout;
        private bool _suppressUnitySelectionSync;
        private bool _suppressRowSelectionSync;
        private bool _sceneMarkersEnabled = true;
        private bool _showGroupNavigation;
        private bool _sceneViewCallbackRegistered;
        private bool _sceneContextClickArmed;
        private bool _suppressNextSceneContextClick;
        private Vector2 _sceneContextMouseDownPosition;
        private bool _externalSceneRefreshQueued;
        private bool _restoringSavedState;
        private bool _closeDecisionHandled;
        private bool _skipCloseRestore;
        private bool _windowEnabled;

        [MenuItem("MxFramework/Level Editor")]
        public static MxLevelEditorWindow OpenWindow()
        {
            MxLevelEditorWindow window = GetOrCreateWindow();
            window._selectedTreeNodeId = "document";
            window._selectedListRowKey = string.Empty;
            window._selectedListRow = null;
            window._rowSearchText = string.Empty;
            window.Show();
            return window;
        }

        [MenuItem("MxFramework/Level Editor/Open Level Editor", priority = 0)]
        public static void OpenWindowFromMxFrameworkSubmenu()
        {
            OpenWindow();
        }

        [MenuItem("Window/MxFramework/Level Editor", priority = 3000)]
        public static void OpenWindowFromWindowMenu()
        {
            OpenWindow();
        }

        [Shortcut(ToggleFocusShortcutId, KeyCode.B)]
        private static void ToggleFocusShortcut()
        {
            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            if (focusedWindow is MxLevelEditorWindow)
            {
                FocusLastSceneView();
                return;
            }

            if (focusedWindow is SceneView sceneView)
                s_lastFocusedSceneView = sceneView;

            MxLevelEditorWindow window = GetOrCreateWindow();
            window.Show();
            window.Focus();
        }

        private static MxLevelEditorWindow GetOrCreateWindow()
        {
            MxLevelEditorWindow window = s_openWindow;
            if (window == null)
                window = GetWindow<MxLevelEditorWindow>("Mx 关卡编辑器");

            s_openWindow = window;
            window.minSize = window._activeWorkspace == null
                ? new Vector2(1120f, 620f)
                : new Vector2(820f, 620f);
            return window;
        }

        private static void FocusLastSceneView()
        {
            SceneView sceneView = s_lastFocusedSceneView;
            if (sceneView == null)
                sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                sceneView = GetWindow<SceneView>();

            s_lastFocusedSceneView = sceneView;
            sceneView.Show();
            sceneView.Focus();
        }

        public void RepaintRequested()
        {
            if (this == null || !_windowEnabled)
                return;
            RefreshSceneCoverage();
            RefreshChrome();
        }

        public void RevealCurrentUnitySelection()
        {
            OnUnitySelectionChanged();
        }

        public static void ClearLayoutPreferencesForTests()
        {
            EditorPrefs.DeleteKey(LeftPaneWidthPrefKey);
            EditorPrefs.DeleteKey(InspectorPaneWidthPrefKey);
        }

        public static void CaptureLayoutPreferencesForTests(
            out bool hasLeftPaneWidth,
            out float leftPaneWidth,
            out bool hasInspectorPaneWidth,
            out float inspectorPaneWidth)
        {
            hasLeftPaneWidth = EditorPrefs.HasKey(LeftPaneWidthPrefKey);
            leftPaneWidth = EditorPrefs.GetFloat(LeftPaneWidthPrefKey, DefaultLeftPaneWidth);
            hasInspectorPaneWidth = EditorPrefs.HasKey(InspectorPaneWidthPrefKey);
            inspectorPaneWidth = EditorPrefs.GetFloat(InspectorPaneWidthPrefKey, DefaultInspectorPaneWidth);
        }

        public static void RestoreLayoutPreferencesForTests(
            bool hasLeftPaneWidth,
            float leftPaneWidth,
            bool hasInspectorPaneWidth,
            float inspectorPaneWidth)
        {
            if (hasLeftPaneWidth)
                EditorPrefs.SetFloat(LeftPaneWidthPrefKey, leftPaneWidth);
            else
                EditorPrefs.DeleteKey(LeftPaneWidthPrefKey);

            if (hasInspectorPaneWidth)
                EditorPrefs.SetFloat(InspectorPaneWidthPrefKey, inspectorPaneWidth);
            else
                EditorPrefs.DeleteKey(InspectorPaneWidthPrefKey);
        }

        public static void SetLayoutPreferencesForTests(float leftPaneWidth, float inspectorPaneWidth)
        {
            EditorPrefs.SetFloat(LeftPaneWidthPrefKey, leftPaneWidth);
            EditorPrefs.SetFloat(InspectorPaneWidthPrefKey, inspectorPaneWidth);
        }

        public static float ReadLeftPaneWidthPreferenceForTests(float availableWidth)
        {
            return ReadLeftPaneWidthPreference(availableWidth);
        }

        public static float ReadInspectorPaneWidthPreferenceForTests(
            float availableWidth,
            float leftPaneWidth)
        {
            return ReadInspectorPaneWidthPreference(availableWidth, leftPaneWidth);
        }

        public bool SceneViewCallbackRegisteredForTests => _sceneViewCallbackRegistered;

        public bool SceneMarkersEnabledForTests => _sceneMarkersEnabled;

        public bool ScenePlacementInteractionEnabledForTests =>
            ModuleSupportsScenePlacement
            && _context != null
            && _structuredModule != null;

        public static void CaptureSceneEditPreferenceForTests(out bool hasValue, out bool value)
        {
            hasValue = EditorPrefs.HasKey(SceneMarkersEnabledPrefKey);
            value = EditorPrefs.GetBool(SceneMarkersEnabledPrefKey, true);
        }

        public static void RestoreSceneEditPreferenceForTests(bool hasValue, bool value)
        {
            if (hasValue)
                EditorPrefs.SetBool(SceneMarkersEnabledPrefKey, value);
            else
                EditorPrefs.DeleteKey(SceneMarkersEnabledPrefKey);
        }

        public static void SetSceneEditPreferenceForTests(bool value)
        {
            EditorPrefs.SetBool(SceneMarkersEnabledPrefKey, value);
        }

        public static bool ReadSceneEditPreferenceForTests()
        {
            return EditorPrefs.GetBool(SceneMarkersEnabledPrefKey, true);
        }

        private void OnEnable()
        {
            _windowEnabled = true;
            s_openWindow = this;
            _closeDecisionHandled = false;
            _skipCloseRestore = false;
            saveChangesMessage = UnsavedChangesMessage;
            _sceneMarkersEnabled = EditorPrefs.GetBool(SceneMarkersEnabledPrefKey, true);
            IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
            for (int i = 0; i < modules.Count; i++)
                modules[i].OnEnable();
            _selectedModuleIndex = Mathf.Clamp(_selectedModuleIndex, 0, Math.Max(0, modules.Count - 1));
            Selection.selectionChanged -= OnUnitySelectionChanged;
            Selection.selectionChanged += OnUnitySelectionChanged;
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.postprocessModifications += OnPostprocessModifications;
            EditorApplication.hierarchyChanged -= OnSceneHierarchyChanged;
            EditorApplication.hierarchyChanged += OnSceneHierarchyChanged;
            EditorSceneManager.sceneDirtied -= OnSceneDirtied;
            EditorSceneManager.sceneDirtied += OnSceneDirtied;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChangedInEditMode;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChangedInEditMode;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
            RefreshSceneCoverage();
            SyncUnsavedChangesState();
        }

        private void OnDisable()
        {
            _windowEnabled = false;
            if (s_openWindow == this)
                s_openWindow = null;

            Selection.selectionChanged -= OnUnitySelectionChanged;
            Undo.postprocessModifications -= OnPostprocessModifications;
            EditorApplication.hierarchyChanged -= OnSceneHierarchyChanged;
            EditorSceneManager.sceneDirtied -= OnSceneDirtied;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChangedInEditMode;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.delayCall -= FlushExternalSceneRefresh;
            _externalSceneRefreshQueued = false;
            ClosePlacementPopover();
            UnregisterSceneViewCallback();
            DetachModuleWindows();
        }

        private void OnDestroy()
        {
            try
            {
                if (_closeDecisionHandled
                    || _skipCloseRestore
                    || _restoringSavedState
                    || !HasUnsavedChanges(MxLevelEditorModuleRegistry.Modules))
                {
                    return;
                }

                // A document owner retains its draft independently of the window. Unexpected
                // window destruction must not reopen the Scene or discard other owners' edits.
                if (DocumentOwner == null) RestoreSavedState(showFailureDialog: false, refreshWindow: false);
            }
            finally
            {
                // RestoreSavedState reloads modules and can enable their callbacks again.
                DetachModuleWindows();
            }
        }

        private void DetachModuleWindows()
        {
            foreach (IMxLevelEditorModule module in MxLevelEditorModuleRegistry.Modules)
                if (module is IMxLevelEditorWindowLifecycleModule lifecycle)
                    lifecycle.DetachWindow(this);
        }

        private void OnBeforeAssemblyReload()
        {
            _skipCloseRestore = true;
        }

        private void OnEditorQuitting()
        {
            _skipCloseRestore = true;
        }

        public override void SaveChanges()
        {
            if (!TrySaveAllModules(showFailureDialog: true))
                return;

            _closeDecisionHandled = true;
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            bool discarded = DocumentOwner != null
                ? TryPrepareDocumentSwitch(MxLevelEditorUnsavedDecision.Discard, out _)
                : RestoreSavedState(showFailureDialog: true, refreshWindow: true);
            if (discarded)
            {
                _closeDecisionHandled = true;
                base.DiscardChanges();
            }
        }

        private bool RestoreSavedState(bool showFailureDialog, bool refreshWindow)
        {
            if (_restoringSavedState)
                return false;

            _restoringSavedState = true;
            try
            {
                IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
                for (int i = 0; i < modules.Count; i++)
                {
                    if (modules[i] is IMxLevelEditorPlacementModule placement
                        && placement.IsScenePlacementActive)
                    {
                        placement.CancelScenePlacement(
                            new MxLevelEditorContext(this, modules[i], RepaintRequested));
                    }
                }

                string scenePath = EditorSceneManager.GetActiveScene().path;
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    hasUnsavedChanges = true;
                    if (showFailureDialog)
                    {
                        EditorUtility.DisplayDialog(
                            "无法恢复未保存场景",
                            "当前场景还没有保存路径，无法恢复到最近保存状态。"
                            + "请先保存场景，或手动处理改动后再关闭关卡编辑器。",
                            "知道了");
                    }
                    return false;
                }

                RestoreManagedAssets(modules);
                hasUnsavedChanges = false;
                Scene activeScene = EditorSceneManager.GetActiveScene();
                if (SceneManager.sceneCount == 1)
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                else
                {
                    // Restore this authoring scene without closing other loaded user scenes.
                    if (!EditorSceneManager.CloseScene(activeScene, removeScene: true))
                        throw new InvalidOperationException("Unable to close the managed scene for restore: " + scenePath);
                    Scene restoredScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    SceneManager.SetActiveScene(restoredScene);
                }

                for (int i = 0; i < modules.Count; i++)
                    modules[i].Reload();

                RefreshSceneCoverage();
                if (refreshWindow)
                    RefreshChrome();
                return true;
            }
            finally
            {
                _restoringSavedState = false;
            }
        }

        private static void RestoreManagedAssets(IReadOnlyList<IMxLevelEditorModule> modules)
        {
            var paths = new List<string>();
            var restored = new HashSet<string>(StringComparer.Ordinal);
            foreach (IMxLevelEditorModule module in modules)
            {
                if (!module.IsDirty
                    || (module is IMxLevelEditorSceneScope scope && !scope.AppliesToActiveScene)
                    || module is not IMxLevelEditorAssetSavePaths assets)
                    continue;
                assets.CollectAssetSavePaths(paths);
            }
            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    throw new InvalidOperationException("Managed restore asset has no saved path.");
                string assetPath = path.Replace('\\', '/');
                if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                    || !System.IO.File.Exists(assetPath))
                    throw new InvalidOperationException("Managed restore asset is not saved under Assets/: " + assetPath);
                restored.Add(assetPath);
            }
            foreach (string assetPath in restored)
            {
                // Import alone retains dirty loaded ScriptableObjects and materials.
                // Release only this explicit discard's managed assets before reading disk.
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    if ((asset is ScriptableObject || asset is Material) && EditorUtility.IsDirty(asset))
                    {
                        Undo.ClearUndo(asset);
                        UnityEngine.Resources.UnloadAsset(asset);
                    }
                }
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private UndoPropertyModification[] OnPostprocessModifications(
            UndoPropertyModification[] modifications)
        {
            if (MxLevelEditorSaveCoordinator.IsSaveInProgress)
                return modifications;

            bool changed = false;
            if (modifications != null)
            {
                IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
                for (int i = 0; i < modifications.Length; i++)
                {
                    PropertyModification current = modifications[i].currentValue;
                    if (current?.target is not Transform transform
                        || !IsTransformProperty(current.propertyPath))
                    {
                        continue;
                    }

                    for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
                    {
                        if (modules[moduleIndex] is IMxLevelEditorSceneChangeAwareModule aware
                            && aware.NotifySceneTransformChanged(transform))
                        {
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                SyncUnsavedChangesState();
                QueueExternalSceneRefresh();
            }
            return modifications;
        }

        private void OnSceneHierarchyChanged()
        {
            if (MxLevelEditorSaveCoordinator.IsSaveInProgress)
                return;

            RefreshSceneCoverage();
            SyncUnsavedChangesState();
            QueueExternalSceneRefresh();
        }

        private void OnSceneDirtied(Scene scene)
        {
            if (MxLevelEditorSaveCoordinator.IsSaveInProgress
                || !IsManagedScene(scene))
            {
                return;
            }

            RefreshSceneCoverage();
            SyncUnsavedChangesState();
            QueueExternalSceneRefresh();
        }

        private void OnSceneSaved(Scene scene)
        {
            if (!IsManagedScene(scene))
                return;

            IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is not IMxLevelEditorSceneChangeAwareModule aware
                    || aware.ManagedSceneRoot == null
                    || aware.ManagedSceneRoot.gameObject.scene.handle != scene.handle)
                {
                    continue;
                }

                aware.AcknowledgeManagedSceneSaved();
            }

            SyncUnsavedChangesState(modules);
            QueueExternalSceneRefresh();
        }

        private void OnActiveSceneChangedInEditMode(Scene previousScene, Scene nextScene)
        {
            if (_restoringSavedState)
                return;

            IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
            for (int i = 0; i < modules.Count; i++)
                modules[i].Reload();

            _selectedTreeNodeId = "document";
            _selectedListRowKey = string.Empty;
            _selectedListRow = null;
            RefreshSceneCoverage();
            SyncUnsavedChangesState();
            QueueExternalSceneRefresh();
        }

        private static bool IsManagedScene(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is IMxLevelEditorSceneChangeAwareModule aware
                    && aware.ManagedSceneRoot != null
                    && aware.ManagedSceneRoot.gameObject.scene.handle == scene.handle)
                {
                    return true;
                }
            }

            return false;
        }

        private void QueueExternalSceneRefresh()
        {
            if (_externalSceneRefreshQueued)
                return;

            _externalSceneRefreshQueued = true;
            EditorApplication.delayCall += FlushExternalSceneRefresh;
        }

        private void FlushExternalSceneRefresh()
        {
            _externalSceneRefreshQueued = false;
            if (this == null)
                return;

            RefreshSceneCoverage();
            RefreshChrome();
        }

        private static bool IsTransformProperty(string propertyPath)
        {
            return !string.IsNullOrWhiteSpace(propertyPath)
                && (propertyPath.StartsWith("m_LocalPosition", StringComparison.Ordinal)
                    || propertyPath.StartsWith("m_LocalRotation", StringComparison.Ordinal)
                    || propertyPath.StartsWith("m_LocalScale", StringComparison.Ordinal));
        }

        private static void RefreshSceneCoverage()
        {
            IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
            var managed = new List<Transform>();
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is IMxLevelEditorSceneChangeAwareModule aware)
                    aware.CollectManagedSceneTransforms(managed);
            }

            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is IMxLevelEditorSceneChangeAwareModule aware)
                    aware.RefreshUnregisteredSceneObjects(managed);
            }
        }

        public void CreateGUI()
        {
            EnableEditorFallbackFontMultiAtlas();
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("mx-level-editor");
            bool hasStyleSheetPath = MxFrameworkPackageAssetLocator.TryResolveAssetPath(
                StyleSheetPackageRelativePath,
                out string styleSheetPath);
            StyleSheet styleSheet = hasStyleSheetPath
                ? AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath)
                : null;
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.minWidth = 0;
            rootVisualElement.style.minHeight = 0;
            RegisterPlacementPopoverCallbacks();

            var topBar = new VisualElement { name = "mx-level-editor-top-bar" };
            topBar.AddToClassList("mx-level-editor__top-bar");
            rootVisualElement.Add(topBar);

            _header = new VisualElement { name = "mx-level-editor-header" };
            _header.AddToClassList("mx-level-editor__header");
            topBar.Add(_header);

            _commonToolbar = new VisualElement { name = "mx-level-editor-common-toolbar" };
            _commonToolbar.AddToClassList("mx-level-editor__common-toolbar");
            topBar.Add(_commonToolbar);

            _documentToolbar = new VisualElement { name = "mx-level-editor-document-toolbar" };
            _documentToolbar.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(_documentToolbar);

            _contentHost = new VisualElement { name = "mx-level-editor-content" };
            _contentHost.AddToClassList("mx-level-editor__content");
            _contentHost.style.flexGrow = 1;
            _contentHost.style.minHeight = 0;
            _contentHost.style.minWidth = 0;
            rootVisualElement.Add(_contentHost);

            RefreshChrome();
        }

        private static void EnableEditorFallbackFontMultiAtlas()
        {
            UnityEngine.TextCore.Text.TextSettings[] textSettings =
                UnityEngine.Resources.FindObjectsOfTypeAll<
                    UnityEngine.TextCore.Text.TextSettings>();
            for (int settingsIndex = 0; settingsIndex < textSettings.Length; settingsIndex++)
            {
                UnityEngine.TextCore.Text.TextSettings settings = textSettings[settingsIndex];
                if (settings == null
                    || !string.Equals(
                        settings.GetType().FullName,
                        EditorTextSettingsTypeName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                List<UnityEngine.TextCore.Text.FontAsset> fallbackFonts =
                    settings.fallbackFontAssets;
                if (fallbackFonts == null)
                    continue;

                for (int fontIndex = 0; fontIndex < fallbackFonts.Count; fontIndex++)
                {
                    UnityEngine.TextCore.Text.FontAsset fallbackFont =
                        fallbackFonts[fontIndex];
                    if (fallbackFont == null
                        || fallbackFont.isMultiAtlasTexturesEnabled
                        || fallbackFont.atlasWidth <= 0
                        || fallbackFont.atlasHeight <= 0)
                    {
                        continue;
                    }

                    // Unity's Editor fallback fonts are shared by every UI Toolkit window.
                    // CJK-heavy authoring surfaces can otherwise exhaust their single atlas
                    // and silently omit later glyphs while keeping the original layout boxes.
                    fallbackFont.isMultiAtlasTexturesEnabled = true;
                }
            }
        }

        private VisualElement _documentToolbar;

        private void RefreshChrome()
        {
            ClosePlacementPopover();
            IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
            IReadOnlyList<WorkspaceNavigationItem> workspaces =
                RebuildWorkspaceNavigation(modules);
            SyncUnsavedChangesState(modules);
            _header?.Clear();
            _commonToolbar?.Clear();
            _contentHost?.Clear();
            _documentToolbar?.Clear();
            if (_documentToolbar != null) DocumentOwner?.BuildToolbar(this, _documentToolbar);

            if (modules.Count == 0 || workspaces.Count == 0)
            {
                _header?.Add(new HelpBox("尚未注册任何关卡编辑模块。", HelpBoxMessageType.Info));
                return;
            }

            _selectedModuleIndex = Mathf.Clamp(
                _selectedModuleIndex,
                0,
                Math.Max(0, workspaces.Count - 1));
            WorkspaceNavigationItem selectedWorkspace =
                workspaces[_selectedModuleIndex];
            IMxLevelEditorModule module = selectedWorkspace.OwnerModule;
            _selectedWorkspaceId = selectedWorkspace.WorkspaceId;
            _activeWorkspace = selectedWorkspace.Workspace;
            _context = new MxLevelEditorContext(this, module, RepaintRequested, RevealStructuredRow);
            _structuredModule = module as IMxLevelEditorStructuredModule;
            _selectionSyncModule = module as IMxLevelEditorSelectionSyncModule;
            _sceneViewModule = module as IMxLevelEditorSceneViewModule;
            _sceneContextMenuModule = module as IMxLevelEditorSceneContextMenuModule;
            _placementModule = module as IMxLevelEditorPlacementModule;
            selectedWorkspace.Workspace?.OnEnable();
            minSize = _activeWorkspace == null
                ? new Vector2(1120f, 620f)
                : new Vector2(820f, 620f);
            BuildHeader(workspaces, modules, module);
            BuildCommonToolbar(module);
            if (_activeWorkspace != null)
                PrepareWorkspaceSceneContext();
            RefreshSceneViewCallbackRegistration();
            if (_activeWorkspace != null)
                BuildWorkspaceContent(_activeWorkspace);
            else if (_structuredModule != null)
                BuildStructuredContent(_structuredModule);
            else
                BuildUnstructuredContent(module);
        }

        private void BuildHeader(
            IReadOnlyList<WorkspaceNavigationItem> workspaces,
            IReadOnlyList<IMxLevelEditorModule> modules,
            IMxLevelEditorModule module)
        {
            var topRow = new VisualElement { name = "mx-level-editor-compact-header" };
            topRow.AddToClassList("mx-level-editor__header-row");
            _header.Add(topRow);

            var title = new Label("Mx 关卡编辑器");
            title.AddToClassList("mx-level-editor__app-title");
            topRow.Add(title);

            var names = new List<string>(workspaces.Count);
            for (int i = 0; i < workspaces.Count; i++)
                names.Add(workspaces[i].DisplayName);

            _modulePopup = new PopupField<string>(string.Empty, names, _selectedModuleIndex);
            _modulePopup.name = "mx-level-editor-module-popup";
            _modulePopup.AddToClassList("mx-level-editor__module-popup");
            _modulePopup.RegisterValueChangedCallback(_ =>
            {
                _selectedModuleIndex = Mathf.Clamp(
                    names.IndexOf(_modulePopup.value),
                    0,
                    workspaces.Count - 1);
                WorkspaceNavigationItem selected =
                    workspaces[_selectedModuleIndex];
                _selectedWorkspaceId = selected.WorkspaceId;
                _selectedTreeNodeId = "document";
                _selectedListRowKey = string.Empty;
                _selectedListRow = null;
                _rowSearchText = string.Empty;
                selected.OwnerModule.OnEnable();
                RefreshChrome();
            });
            topRow.Add(_modulePopup);

            Label scene = CreateHeaderLabel(EditorSceneManager.GetActiveScene().name);
            scene.name = "mx-level-editor-scene-name";
            scene.AddToClassList("mx-level-editor__scene-name");
            topRow.Add(scene);

            bool anyModuleDirty = HasUnsavedChanges(modules);
            var state = new Label(anyModuleDirty ? "● 有未保存更改" : "● 已保存");
            state.name = "mx-level-editor-document-state";
            state.AddToClassList(anyModuleDirty
                ? "mx-level-editor__state--dirty"
                : "mx-level-editor__state--saved");
            topRow.Add(state);

            Label status = CreateHeaderLabel(module.Status);
            status.name = "mx-level-editor-status-summary";
            status.AddToClassList("mx-level-editor__status-summary");
            status.tooltip = string.IsNullOrWhiteSpace(module.Status)
                ? module.DocumentPath
                : module.Status + "\n" + module.DocumentPath;
            topRow.Add(status);
        }

        private void SyncUnsavedChangesState()
        {
            SyncUnsavedChangesState(MxLevelEditorModuleRegistry.Modules);
        }

        private void SyncUnsavedChangesState(IReadOnlyList<IMxLevelEditorModule> modules)
        {
            saveChangesMessage = UnsavedChangesMessage;
            hasUnsavedChanges = !_restoringSavedState && HasUnsavedChanges(modules);
        }

        private static bool HasUnsavedChanges(IReadOnlyList<IMxLevelEditorModule> modules)
        {
            if (MxLevelEditorDocumentOwnerRegistry.Owner?.AppliesToActiveScene == true
                && MxLevelEditorDocumentOwnerRegistry.Owner.IsDirty) return true;
            if (modules == null)
                return false;

            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i]?.IsDirty == true
                    && (modules[i] is not IMxLevelEditorSceneScope scope
                        || scope.AppliesToActiveScene))
                    return true;
            }

            return false;
        }

        private static string ResolveModuleDisplayName(IMxLevelEditorModule module)
        {
            if (module == null)
                return string.Empty;

            string moduleId = module.ModuleId ?? string.Empty;
            if (moduleId.EndsWith(".worldObjects", StringComparison.OrdinalIgnoreCase)
                || moduleId.EndsWith(".world-objects", StringComparison.OrdinalIgnoreCase))
                return "世界对象";
            if (moduleId.EndsWith(".characters", StringComparison.OrdinalIgnoreCase))
                return "场景角色";
            return module.DisplayName;
        }

        private void BuildCommonToolbar(IMxLevelEditorModule module)
        {
            Button reload = AddToolbarButton(_commonToolbar, "重新加载", () =>
            {
                if (DocumentOwner != null)
                {
                    var decision = MxLevelEditorUnsavedDecision.Cancel;
                    if (HasPendingDocumentChanges)
                    {
                        int answer = EditorUtility.DisplayDialogComplex("重载当前关卡", "保存后重载、放弃当前关卡草稿，或取消。其他 Scene 编辑将保留。", "保存后重载", "取消", "放弃关卡草稿");
                        decision = answer == 0 ? MxLevelEditorUnsavedDecision.Save : answer == 2 ? MxLevelEditorUnsavedDecision.Discard : MxLevelEditorUnsavedDecision.Cancel;
                    }
                    if (!TryPrepareDocumentSwitch(decision, out string failure))
                    { if (!string.IsNullOrEmpty(failure)) EditorUtility.DisplayDialog("保留当前编辑", failure, "继续编辑"); return; }
                    DocumentOwner.ReloadSavedDocument(); ReloadDocumentModules();
                }
                else { module.Reload(); RefreshChrome(); }
            });
            reload.name = "mx-level-editor-reload";
            reload.AddToClassList("mx-level-editor__utility-action");

            Button validate = AddToolbarButton(_commonToolbar, "校验", () =>
            {
                if (DocumentOwner is IMxLevelEditorDocumentValidationOwner validation) validation.TryValidateDocument(out _);
                else module.Validate();
                RefreshChrome();
            });
            validate.name = "mx-level-editor-validate";
            validate.AddToClassList("mx-level-editor__utility-action");

            var tools = new ToolbarMenu
            {
                name = "mx-level-editor-tools-menu",
                text = "工具",
                tooltip = "更多工具。B：切换 Scene / 关卡编辑器"
            };
            tools.AddToClassList("mx-level-editor__tools-menu");
            tools.menu.AppendAction("打开 JSON", _ =>
            {
                module.OpenJson();
                RefreshChrome();
            });
            tools.menu.AppendAction("选择场景根节点", _ =>
            {
                module.SelectRootSource();
                RefreshChrome();
            });
            if (module is IMxLevelEditorToolsModule toolsModule)
            {
                tools.menu.AppendSeparator();
                toolsModule.BuildToolsMenu(tools.menu, this);
            }
            _commonToolbar.Add(tools);

            Button save = AddToolbarButton(_commonToolbar, "保存", () =>
            {
                TrySaveAllModules(showFailureDialog: true);
                RefreshChrome();
            });
            save.name = "mx-level-editor-save";
            save.tooltip =
                "保存全部关卡内容，通过校验后同步更新 Runtime 使用的场景缓存。";
            save.AddToClassList("mx-level-editor__primary-action");
        }

        private bool TrySaveAllModules(bool showFailureDialog)
        {
            string message;
            bool saved = DocumentOwner != null
                ? DocumentOwner.TrySave(MxLevelEditorModuleRegistry.Modules, out message)
                : MxLevelEditorSaveCoordinator.TrySaveModules(MxLevelEditorModuleRegistry.Modules, out message);
            SyncUnsavedChangesState();
            if (saved && !hasUnsavedChanges)
                return true;

            if (saved)
            {
                string dirtyModules = BuildDirtyModuleSummary(
                    MxLevelEditorModuleRegistry.Modules);
                if (!string.IsNullOrWhiteSpace(dirtyModules))
                    message += "\n仍未保存的模块：" + dirtyModules;
            }

            if (showFailureDialog)
            {
                EditorUtility.DisplayDialog(
                    "关卡仍有未保存内容",
                    string.IsNullOrWhiteSpace(message)
                        ? "至少一个关卡模块未能保存。请根据窗口中的校验信息修复后再试。"
                        : message,
                    "知道了");
            }

            RefreshChrome();
            return false;
        }

        private static string BuildDirtyModuleSummary(
            IReadOnlyList<IMxLevelEditorModule> modules)
        {
            if (modules == null)
                return string.Empty;

            var names = new List<string>();
            for (int i = 0; i < modules.Count; i++)
            {
                IMxLevelEditorModule module = modules[i];
                if (module?.IsDirty != true
                    || (module is IMxLevelEditorSceneScope scope
                        && !scope.AppliesToActiveScene))
                    continue;

                string reason = string.Empty;
                if (module is IMxLevelEditorDirtyDiagnostics diagnostics)
                {
                    if (diagnostics.IsDocumentDirty && diagnostics.IsManagedSceneDirty)
                        reason = "JSON、场景";
                    else if (diagnostics.IsDocumentDirty)
                        reason = "JSON";
                    else if (diagnostics.IsManagedSceneDirty)
                        reason = "场景";
                }

                names.Add(
                    string.IsNullOrWhiteSpace(reason)
                        ? ResolveModuleDisplayName(module)
                        : ResolveModuleDisplayName(module) + "（" + reason + "）");
            }

            return string.Join("、", names);
        }

        public static Button AddToolbarButton(VisualElement toolbar, string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 4;
            button.style.height = 22;
            toolbar.Add(button);
            return button;
        }

        private static float ReadLeftPaneWidthPreference(float availableWidth)
        {
            float safeAvailableWidth = NormalizeAvailableWidth(availableWidth);
            float maxWidth = Mathf.Max(
                MinLeftPaneWidth,
                safeAvailableWidth - MinListPaneWidth - MinInspectorPaneWidth);
            float preferred = EditorPrefs.GetFloat(LeftPaneWidthPrefKey, DefaultLeftPaneWidth);
            return Mathf.Clamp(preferred, MinLeftPaneWidth, maxWidth);
        }

        private static float ReadInspectorPaneWidthPreference(float availableWidth, float leftPaneWidth)
        {
            float safeAvailableWidth = NormalizeAvailableWidth(availableWidth);
            float maxWidth = Mathf.Max(
                MinInspectorPaneWidth,
                safeAvailableWidth - Mathf.Max(MinLeftPaneWidth, leftPaneWidth) - MinListPaneWidth);
            float preferred = EditorPrefs.GetFloat(InspectorPaneWidthPrefKey, DefaultInspectorPaneWidth);
            return Mathf.Clamp(preferred, MinInspectorPaneWidth, maxWidth);
        }

        private static float NormalizeAvailableWidth(float availableWidth)
        {
            float minimumWidth = MinLeftPaneWidth + MinListPaneWidth + MinInspectorPaneWidth;
            if (float.IsNaN(availableWidth) || float.IsInfinity(availableWidth) || availableWidth <= 0f)
                return DefaultLeftPaneWidth + MinListPaneWidth + DefaultInspectorPaneWidth;

            return Mathf.Max(minimumWidth, availableWidth);
        }

        private static void PersistPaneWidthPreference(string key, float width, float minWidth)
        {
            if (float.IsNaN(width) || float.IsInfinity(width) || width < minWidth)
                return;

            float previous = EditorPrefs.GetFloat(key, -1f);
            if (Mathf.Abs(previous - width) > 0.5f)
                EditorPrefs.SetFloat(key, width);
        }

        private bool ModuleSupportsSceneViewEditing =>
            _sceneViewModule != null && _sceneViewModule.SupportsSceneViewEditing;

        private bool ModuleSupportsScenePlacement =>
            _placementModule != null && _placementModule.SupportsScenePlacement;

        private void RefreshSceneViewCallbackRegistration()
        {
            bool shouldRegister = ModuleSupportsSceneViewEditing || ModuleSupportsScenePlacement;
            if (shouldRegister && !_sceneViewCallbackRegistered)
            {
                SceneView.beforeSceneGui -= OnSceneViewBeforeGui;
                SceneView.beforeSceneGui += OnSceneViewBeforeGui;
                SceneView.duringSceneGui -= OnSceneViewGui;
                SceneView.duringSceneGui += OnSceneViewGui;
                _sceneViewCallbackRegistered = true;
            }
            else if (!shouldRegister && _sceneViewCallbackRegistered)
            {
                UnregisterSceneViewCallback();
            }

            SceneView.RepaintAll();
        }

        private void UnregisterSceneViewCallback()
        {
            if (!_sceneViewCallbackRegistered)
                return;

            SceneView.beforeSceneGui -= OnSceneViewBeforeGui;
            SceneView.duringSceneGui -= OnSceneViewGui;
            _sceneViewCallbackRegistered = false;
            _sceneContextClickArmed = false;
            _suppressNextSceneContextClick = false;
            SceneView.RepaintAll();
        }

        private void OnSceneViewBeforeGui(SceneView sceneView)
        {
            if ((!ModuleSupportsSceneViewEditing && !ModuleSupportsScenePlacement)
                || _context == null
                || _structuredModule == null)
            {
                _sceneContextClickArmed = false;
                return;
            }

            HandleSceneContextMenuGesture();
        }

        private void OnSceneViewGui(SceneView sceneView)
        {
            if ((!ModuleSupportsSceneViewEditing && !ModuleSupportsScenePlacement)
                || _context == null
                || _structuredModule == null)
            {
                return;
            }

            if (_sceneMarkersEnabled && ModuleSupportsSceneViewEditing)
            {
                _sceneViewModule.DrawSceneView(
                    new MxLevelEditorSceneViewContext(
                        _context,
                        _selectedTreeNodeId,
                        _selectedListRow,
                        _allListRows),
                    sceneView);
            }

            if (ModuleSupportsScenePlacement)
            {
                var placementContext = new MxLevelEditorPlacementContext(
                    _context,
                    _selectedTreeNodeId,
                    _selectedListRow,
                    _allListRows);
                _placementModule.HandleScenePlacement(placementContext, sceneView);
                _placementModule.DrawScenePlacement(placementContext, sceneView);
            }
        }

        private bool HandleSceneContextMenuGesture()
        {
            Event current = Event.current;
            if (current == null)
                return false;
            if (_placementModule != null && _placementModule.IsScenePlacementActive)
            {
                _sceneContextClickArmed = false;
                _suppressNextSceneContextClick = false;
                return false;
            }

            switch (current.type)
            {
                case EventType.MouseDown when current.button == 1:
                    _sceneContextClickArmed = !current.alt;
                    _suppressNextSceneContextClick = false;
                    _sceneContextMouseDownPosition = current.mousePosition;
                    return false;

                case EventType.MouseDrag when current.button == 1:
                    if (_sceneContextClickArmed
                        && (current.mousePosition - _sceneContextMouseDownPosition).sqrMagnitude
                        > SceneContextClickMaxMovement * SceneContextClickMaxMovement)
                    {
                        _sceneContextClickArmed = false;
                    }
                    return false;

                case EventType.MouseUp when current.button == 1:
                    bool isClick = _sceneContextClickArmed
                        && (current.mousePosition - _sceneContextMouseDownPosition).sqrMagnitude
                        <= SceneContextClickMaxMovement * SceneContextClickMaxMovement;
                    _sceneContextClickArmed = false;
                    if (!isClick || !TryShowSceneContextMenu(current.mousePosition))
                        return false;

                    _suppressNextSceneContextClick = true;
                    current.Use();
                    return true;

                case EventType.ContextClick:
                    if (_suppressNextSceneContextClick)
                    {
                        _suppressNextSceneContextClick = false;
                        current.Use();
                        return true;
                    }

                    if (!TryShowSceneContextMenu(current.mousePosition))
                        return false;

                    current.Use();
                    return true;
            }

            return false;
        }

        private bool TryShowSceneContextMenu(Vector2 mousePosition)
        {
            GameObject picked = HandleUtility.PickGameObject(mousePosition, false);
            if (picked == null
                || !TryActivateSceneContextTarget(picked, out MxLevelEditorListRow row)
                || row == null
                || _sceneContextMenuModule == null)
            {
                return false;
            }

            SelectSceneContextTarget(row);
            var menu = new GenericMenu();
            if (HasUnsavedChanges(MxLevelEditorModuleRegistry.Modules))
            {
                menu.AddItem(
                    new GUIContent("保存全部"),
                    false,
                    () =>
                    {
                        TrySaveAllModules(showFailureDialog: true);
                        RefreshChrome();
                    });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("保存全部（无未保存更改）"));
            }

            menu.AddSeparator(string.Empty);
            _sceneContextMenuModule.BuildSceneContextMenu(
                new MxLevelEditorSceneViewContext(
                    _context,
                    _selectedTreeNodeId,
                    row,
                    _allListRows),
                menu);
            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("打开关卡编辑器"),
                false,
                () =>
                {
                    Show();
                    Focus();
                });
            menu.ShowAsContext();
            return true;
        }

        private bool TryActivateSceneContextTarget(
            UnityEngine.Object selectedObject,
            out MxLevelEditorListRow row)
        {
            row = null;
            IReadOnlyList<IMxLevelEditorModule> modules = MxLevelEditorModuleRegistry.Modules;
            if (modules.Count == 0 || selectedObject == null)
                return false;

            int firstIndex = 0;
            if (_context?.Module != null)
            {
                for (int moduleIndex = 0;
                     moduleIndex < modules.Count;
                     moduleIndex++)
                {
                    if (ReferenceEquals(
                            modules[moduleIndex],
                            _context.Module))
                    {
                        firstIndex = moduleIndex;
                        break;
                    }
                }
            }
            for (int offset = 0; offset < modules.Count; offset++)
            {
                int index = (firstIndex + offset) % modules.Count;
                IMxLevelEditorModule module = modules[index];
                if (module is not IMxLevelEditorSelectionSyncModule selectionModule
                    || module is not IMxLevelEditorStructuredModule
                    || module is not IMxLevelEditorSceneContextMenuModule)
                {
                    continue;
                }

                var candidateContext = new MxLevelEditorContext(
                    this,
                    module,
                    RepaintRequested,
                    RevealStructuredRow);
                if (!selectionModule.TryResolveSelection(
                        candidateContext,
                        selectedObject,
                        out MxLevelEditorSelectionTarget target)
                    || target == null
                    || string.IsNullOrWhiteSpace(target.RowKey))
                {
                    continue;
                }

                int workspaceIndex = FindWorkspaceIndex(
                    _workspaceNavigationItems,
                    module.ModuleId);
                if (workspaceIndex >= 0
                    && (workspaceIndex != _selectedModuleIndex
                        || _activeWorkspace != null))
                {
                    _selectedModuleIndex = workspaceIndex;
                    _selectedWorkspaceId = module.ModuleId;
                    _selectedTreeNodeId = "document";
                    _selectedListRowKey = string.Empty;
                    _selectedListRow = null;
                    _rowSearchText = string.Empty;
                    module.OnEnable();
                    RefreshChrome();
                }

                RevealStructuredRow(target.TreeNodeId, target.RowKey);
                row = _selectedListRow;
                return row != null;
            }

            return false;
        }

        private void SelectSceneContextTarget(MxLevelEditorListRow row)
        {
            if (_selectionSyncModule == null
                || row == null
                || !_selectionSyncModule.TryResolveRowObject(
                    _context,
                    row,
                    out UnityEngine.Object target)
                || target == null)
            {
                return;
            }

            _suppressUnitySelectionSync = true;
            try
            {
                Selection.activeObject = target;
            }
            finally
            {
                _suppressUnitySelectionSync = false;
            }
        }

        private void BuildUnstructuredContent(IMxLevelEditorModule module)
        {
            var root = new VisualElement { name = "mx-level-editor-unstructured-content" };
            root.style.flexGrow = 1;
            root.style.minHeight = 0;
            root.style.minWidth = 0;
            root.style.flexDirection = FlexDirection.Column;

            var moduleToolbar = CreateModuleToolbar();
            module.BuildModuleToolbar(this, moduleToolbar);
            if (moduleToolbar.childCount > 0)
                root.Add(moduleToolbar);

            VisualElement content = module.CreateContent(this);
            if (content != null)
                root.Add(content);
            _contentHost.Add(root);
        }
    }
}
