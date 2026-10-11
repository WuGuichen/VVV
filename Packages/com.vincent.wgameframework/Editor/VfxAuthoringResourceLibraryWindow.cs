using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    public sealed class VfxAuthoringResourceLibraryWindow : EditorWindow
    {
        internal const string CatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        internal const string ThumbnailRoot = "Temp/MxFramework/VfxAuthoring/Thumbnails";
        private const string LayoutPackageRelativePath =
            "Editor/VfxAuthoringResourceLibraryWindow.uxml";
        private const string StylePackageRelativePath =
            "Editor/VfxAuthoringResourceLibraryWindow.uss";
        private const string ShowcasePath = "Assets/Scenes/MxVfxShowcase.unity";
        private const string ToggleFocusShortcutId = "MxFramework/VFX Authoring/Toggle Focus";
        private const float ShortcutCollapsedWidth = 180f;
        private const float ShortcutCollapsedHeight = 28f;

        private static EditorWindow s_lastFocusedGameView;

        private enum WindowMode
        {
            Library,
            CreationLab
        }

        private enum StudioMode
        {
            Manual,
            SourceImport,
            DevelopmentAgent
        }

        private readonly List<VfxLibraryCardRow> _rows = new List<VfxLibraryCardRow>();
        private VfxLibraryWorkspace _workspace;
        private VfxLibraryPreviewSession _preview;
        private VfxLibraryAuthoringStudioController _studio;
        private VfxManualCompositionStudioController _manualStudio;
        private bool _agentStudioInitializationScheduled;
        private bool _manualStudioInitializationScheduled;
        private ListView _grid;
        private VisualElement _categoryHost;
        private VisualElement _tabHost;
        private VisualElement _libraryWorkspace;
        private VisualElement _creativeWorkspace;
        private Label _coverage;
        private Label _productionCoverage;
        private Label _diagnostics;
        private Label _centerTitle;
        private Label _centerSubtitle;
        private ToolbarSearchField _search;
        private DropdownField _role;
        private DropdownField _theme;
        private DropdownField _backend;
        private DropdownField _review;
        private Button _libraryModeButton;
        private Button _creativeModeButton;
        private Button _manualStudioButton;
        private Button _sourceImportStudioButton;
        private Button _agentStudioButton;
        private VisualElement _studioModeSwitch;
        private Button _creativeResetButton;
        private VisualElement _manualStudioWorkspace;
        private VisualElement _sourceImportStudioWorkspace;
        private VisualElement _agentStudioWorkspace;
        private Button _openAssetButton;
        private Button _saveDraftButton;
        private Button _applyDraftButton;
        private string _activeTab = "组合";
        private string _quickFilter = string.Empty;
        private VfxLibraryItemKind? _activeKind = VfxLibraryItemKind.Effect;
        private WindowMode _mode = WindowMode.Library;
        private StudioMode _studioMode = StudioMode.Manual;
        private int _columns = 3;
        private IMGUIContainer _previewCanvas;
        private Button _playButton;
        private Button _pauseButton;
        private Button _stopButton;
        private Label _playbackStatus;
        private bool _thumbnailRecoveryAttempted;
        private bool _shortcutCollapsed;
        private Rect _shortcutExpandedPosition;
        private Vector2 _shortcutExpandedMinSize;
        private Vector2 _shortcutExpandedMaxSize;
        private bool _shortcutExpandedMaximized;
        private GUIContent _shortcutExpandedTitleContent;

        public string StatusForTests { get; private set; } = string.Empty;

        [MenuItem("MxFramework/VFX/VFX Resource Library")]
        public static void Open()
        {
            var window = GetWindow<VfxAuthoringResourceLibraryWindow>();
            window.titleContent = new GUIContent("VFX 创作工作台");
            window.minSize = new Vector2(1080f, 680f);
            window.Show();
        }

        [Shortcut(ToggleFocusShortcutId, KeyCode.V)]
        private static void ToggleFocusShortcut()
        {
            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            if (focusedWindow is VfxAuthoringResourceLibraryWindow focusedVfxWindow)
            {
                focusedVfxWindow.CollapseForShortcut();
                FocusLastGameView();
                return;
            }

            if (IsGameView(focusedWindow))
                s_lastFocusedGameView = focusedWindow;

            VfxAuthoringResourceLibraryWindow window;
            if (HasOpenInstances<VfxAuthoringResourceLibraryWindow>())
            {
                window = GetWindow<VfxAuthoringResourceLibraryWindow>();
            }
            else
            {
                Open();
                window = GetWindow<VfxAuthoringResourceLibraryWindow>();
            }

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
            titleContent = new GUIContent("VFX 工作台（V 恢复）");

            Vector2 collapsedSize = new Vector2(ShortcutCollapsedWidth, ShortcutCollapsedHeight);
            minSize = collapsedSize;
            maxSize = collapsedSize;
            EditorWindowShortcutTray.Add(this, _shortcutExpandedPosition, collapsedSize);
        }

        private void RestoreFromShortcutCollapse()
        {
            if (!_shortcutCollapsed)
                return;

            _shortcutCollapsed = false;
            if (!docked)
            {
                EditorWindowShortcutTray.Remove(this);
                minSize = _shortcutExpandedMinSize;
                maxSize = _shortcutExpandedMaxSize;
                position = _shortcutExpandedPosition;
                rootVisualElement.style.display = DisplayStyle.Flex;
                titleContent = _shortcutExpandedTitleContent ?? new GUIContent("VFX 创作工作台");
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

        public static void OpenManual()
        {
            Open();
            EditorApplication.delayCall += () =>
            {
                VfxAuthoringResourceLibraryWindow window = GetWindow<VfxAuthoringResourceLibraryWindow>();
                window.SetMode(WindowMode.CreationLab);
                window.SetStudioMode(StudioMode.Manual);
            };
        }

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeUiState;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeUiState;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        public void CreateGUI()
        {
            bool restoreSourceImport = ShouldRestoreSourceImportWorkspace(Application.dataPath);
            EditorApplication.update -= OnEditorUpdate;
            DisposeUiState();
            rootVisualElement.Clear();
            try
            {
                bool hasLayoutPath = MxFrameworkPackageAssetLocator.TryResolveAssetPath(
                    LayoutPackageRelativePath,
                    out string layoutPath);
                bool hasStylePath = MxFrameworkPackageAssetLocator.TryResolveAssetPath(
                    StylePackageRelativePath,
                    out string stylePath);
                VisualTreeAsset tree = hasLayoutPath
                    ? AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(layoutPath)
                    : null;
                StyleSheet style = hasStylePath
                    ? AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath)
                    : null;
                if (tree == null || style == null)
                {
                    rootVisualElement.Add(new HelpBox("VFX 创作工作台 UXML/USS 缺失。", HelpBoxMessageType.Error));
                    return;
                }
                tree.CloneTree(rootVisualElement);
                rootVisualElement.styleSheets.Add(style);
                BindControls();
                Reload();
                if (restoreSourceImport)
                {
                    SetMode(WindowMode.CreationLab);
                    SetStudioMode(StudioMode.SourceImport);
                }
                else
                {
                    SetMode(WindowMode.Library);
                }
            }
            finally
            {
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.update += OnEditorUpdate;
            }
        }

        private void OnDisable()
        {
            EditorWindowShortcutTray.Remove(this);
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeUiState;
            EditorApplication.update -= OnEditorUpdate;
            DisposeUiState();
        }

        private void DisposeUiState()
        {
            _studio?.Dispose();
            _studio = null;
            _agentStudioInitializationScheduled = false;
            _manualStudio?.Dispose();
            _manualStudio = null;
            _manualStudioInitializationScheduled = false;
            DisposePreview();
            _previewCanvas = null;
            _workspace?.Dispose();
            _workspace = null;
            _rows.Clear();
            _grid = null;
            _tabHost = null;
        }

        private void BindControls()
        {
            _libraryWorkspace = rootVisualElement.Q("library-workspace");
            _creativeWorkspace = rootVisualElement.Q("creative-workspace");
            _libraryModeButton = rootVisualElement.Q<Button>("library-mode");
            _creativeModeButton = rootVisualElement.Q<Button>("creative-mode");
            _manualStudioButton = rootVisualElement.Q<Button>("manual-studio-mode");
            _sourceImportStudioButton = rootVisualElement.Q<Button>("source-import-studio-mode");
            _agentStudioButton = rootVisualElement.Q<Button>("agent-studio-mode");
            _studioModeSwitch = rootVisualElement.Q("studio-mode-switch");
            _creativeResetButton = rootVisualElement.Q<Button>("creative-reset");
            _manualStudioWorkspace = rootVisualElement.Q("manual-studio-workspace");
            _sourceImportStudioWorkspace = rootVisualElement.Q("source-prefab-import-workspace");
            _agentStudioWorkspace = rootVisualElement.Q("agent-studio-workspace");
            _search = rootVisualElement.Q<ToolbarSearchField>("search");
            _role = rootVisualElement.Q<DropdownField>("role-filter");
            _theme = rootVisualElement.Q<DropdownField>("theme-filter");
            _backend = rootVisualElement.Q<DropdownField>("backend-filter");
            _review = rootVisualElement.Q<DropdownField>("review-filter");
            _coverage = rootVisualElement.Q<Label>("coverage-badge");
            _productionCoverage = rootVisualElement.Q<Label>("production-coverage-badge");
            _diagnostics = rootVisualElement.Q<Label>("diagnostic-badge");
            _centerTitle = rootVisualElement.Q<Label>("center-title");
            _centerSubtitle = rootVisualElement.Q<Label>("center-subtitle");
            _categoryHost = rootVisualElement.Q("category-host");
            _tabHost = rootVisualElement.Q("tab-content");
            _openAssetButton = rootVisualElement.Q<Button>("open-prefab");
            _saveDraftButton = rootVisualElement.Q<Button>("save-draft");
            _applyDraftButton = rootVisualElement.Q<Button>("apply-change");

            _grid = rootVisualElement.Q<ListView>("effect-grid");
            _grid.itemsSource = _rows;
            _grid.fixedItemHeight = 204f;
            _grid.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _grid.makeItem = MakeRow;
            _grid.bindItem = BindRow;

            _search.RegisterValueChangedCallback(_ => RefreshCards());
            foreach (DropdownField field in new[] { _role, _theme, _backend, _review })
                field.RegisterValueChangedCallback(_ => RefreshCards());

            _libraryModeButton.clicked += () => SetMode(WindowMode.Library);
            _creativeModeButton.clicked += () => SetMode(WindowMode.CreationLab);
            _manualStudioButton.clicked += () => SetStudioMode(StudioMode.Manual);
            _sourceImportStudioButton.clicked += () => SetStudioMode(StudioMode.SourceImport);
            _agentStudioButton.clicked += () => SetStudioMode(StudioMode.DevelopmentAgent);
            rootVisualElement.Q<Button>("reload").clicked += () => { _thumbnailRecoveryAttempted = false; Reload(); };
            rootVisualElement.Q<Button>("thumbnails").clicked += GenerateThumbnails;
            rootVisualElement.Q<Button>("showcase").clicked += OpenShowcase;
            _openAssetButton.clicked += OpenSelectedAsset;
            rootVisualElement.Q<Button>("open-showcase").clicked += OpenShowcase;
            rootVisualElement.Q<Button>("impact-analysis").clicked += CopyImpactCommand;
            _saveDraftButton.clicked += SaveDraft;
            _applyDraftButton.clicked += ApplyDraft;

            BindPreviewControls();
            foreach (Button tab in rootVisualElement.Query<Button>(className: "vfx-tab").ToList())
                tab.clicked += () => SelectTab(tab.text);

            SetStudioMode(StudioMode.Manual);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void BindPreviewControls()
        {
            _playButton = rootVisualElement.Q<Button>("play");
            _pauseButton = rootVisualElement.Q<Button>("pause");
            _stopButton = rootVisualElement.Q<Button>("stop");
            _playbackStatus = rootVisualElement.Q<Label>("playback-status");
            _playButton.clicked += () => { _preview?.Play(); RefreshPlaybackState(); };
            _pauseButton.clicked += () => { _preview?.Pause(); RefreshPlaybackState(); };
            _stopButton.clicked += () => { _preview?.Stop(); RefreshPlaybackState(); };
            rootVisualElement.Q<Button>("restart").clicked += () => { _preview?.Restart(); RefreshPlaybackState(); };
            rootVisualElement.Q<Toggle>("loop").RegisterValueChangedCallback(evt => _preview?.SetLoop(evt.newValue));
            rootVisualElement.Q<Slider>("speed").RegisterValueChangedCallback(evt => _preview?.SetSpeed(evt.newValue));
            rootVisualElement.Q<DropdownField>("fallback-mode").RegisterValueChangedCallback(evt => _preview?.SetFallbackMode(evt.newValue));
            VisualElement previewHost = rootVisualElement.Q("preview-host");
            _preview = new VfxLibraryPreviewSession();
            _previewCanvas = new IMGUIContainer(() => _preview?.Draw(previewHost.contentRect)) { name = "preview-canvas" };
            previewHost.Add(_previewCanvas);
        }

        private void Reload()
        {
            _workspace?.Dispose();
            if (!System.IO.File.Exists(CatalogPath))
            {
                StatusForTests =
                    "Unavailable: VFX authoring catalog is not present in this consumer project.";
                _workspace = VfxLibraryWorkspace.CreateUnavailable();
                SetChoices(_role, _workspace.Roles);
                SetChoices(_theme, _workspace.Themes);
                SetChoices(_backend, _workspace.Backends);
                SetChoices(_review, _workspace.Reviews);
                _coverage.text = "语义覆盖 不可用";
                _productionCoverage.text = "Production 不可用";
                _diagnostics.text = "0 条诊断";
                BuildCategories();
                RefreshCards();
                _centerSubtitle.text = StatusForTests;
                SetCatalogActionsEnabled(false);
                return;
            }

            StatusForTests = string.Empty;
            _workspace = VfxLibraryWorkspace.Load(CatalogPath, ThumbnailRoot);
            SetCatalogActionsEnabled(true);
            if (_workspace.Effects.Count > 0 && _workspace.ThumbnailCount == 0 && !_thumbnailRecoveryAttempted)
            {
                _thumbnailRecoveryAttempted = true;
                rootVisualElement.schedule.Execute(RebuildMissingThumbnails).ExecuteLater(1L);
            }
            SetChoices(_role, _workspace.Roles);
            SetChoices(_theme, _workspace.Themes);
            SetChoices(_backend, _workspace.Backends);
            SetChoices(_review, _workspace.Reviews);
            _coverage.text = "语义覆盖 " + _workspace.Effects.Count + "/" + _workspace.Effects.Count;
            _productionCoverage.text = "Production " + _workspace.ProductionEffectCount + "/" + _workspace.Effects.Count;
            _diagnostics.text = _workspace.DiagnosticCount + " 条诊断";
            BuildCategories();
            RefreshCards();
            _studio?.SetWorkspace(_workspace);
            _manualStudio?.SetWorkspace(_workspace);
        }

        private void SetCatalogActionsEnabled(bool enabled)
        {
            _creativeModeButton?.SetEnabled(enabled);
            _openAssetButton?.SetEnabled(enabled);
            _saveDraftButton?.SetEnabled(enabled);
            _applyDraftButton?.SetEnabled(enabled);
            rootVisualElement.Q<Button>("thumbnails")?.SetEnabled(enabled);
            rootVisualElement.Q<Button>("showcase")?.SetEnabled(enabled);
            rootVisualElement.Q<Button>("open-showcase")?.SetEnabled(enabled);
            rootVisualElement.Q<Button>("impact-analysis")?.SetEnabled(enabled);
        }

        private static void SetChoices(DropdownField field, IReadOnlyList<string> values)
        {
            field.choices = new List<string>();
            for (int i = 0; i < values.Count; i++)
                field.choices.Add(VfxLibraryLabels.Display(values[i]));
            if (string.IsNullOrEmpty(field.value) || !field.choices.Contains(field.value))
                field.SetValueWithoutNotify("全部");
        }

        private void BuildCategories()
        {
            _categoryHost.Clear();
            AddCategory("全部效果", _workspace.Effects.Count, VfxLibraryItemKind.Effect, string.Empty, string.Empty);
            foreach (KeyValuePair<string, int> pair in _workspace.RoleCounts)
                AddCategory(VfxLibraryLabels.Display(pair.Key), pair.Value, VfxLibraryItemKind.Effect, pair.Key, string.Empty);
            AddCategory("配方", _workspace.RecipeCount, VfxLibraryItemKind.Recipe, string.Empty, string.Empty);
            AddCategory("基础单元", _workspace.PrimitiveCount, VfxLibraryItemKind.Primitive, string.Empty, string.Empty);
            AddCategory("占位待替换", _workspace.LegacyPlaceholderCount, null, string.Empty, "placeholders");
            AddCategory("创作候选", _workspace.CandidateCount, null, string.Empty, "candidates");
            AddCategory("Production", _workspace.ProductionCount, null, string.Empty, "production");
            AddCategory("已标记样板", _workspace.ExemplarCount, null, string.Empty, "exemplars");
            AddCategory("复用缺口", _workspace.ReuseGaps.Count, null, string.Empty, "gaps");
            AddSectionBreak("本地视图");
            AddCategory("收藏", _workspace.FavoriteCount, VfxLibraryItemKind.Effect, string.Empty, "favorites");
            AddCategory("待评审", _workspace.NeedsReviewCount, null, string.Empty, "review");
            AddCategory("高预算", _workspace.HighBudgetCount, VfxLibraryItemKind.Effect, string.Empty, "budget");
        }

        private void AddSectionBreak(string text)
        {
            var label = new Label(text);
            label.AddToClassList("category-section");
            _categoryHost.Add(label);
        }

        private void AddCategory(string label, int count, VfxLibraryItemKind? kind, string role, string quickFilter)
        {
            var button = new Button(() => SelectCategory(kind, role, quickFilter));
            button.AddToClassList("category-button");
            bool selected = _activeKind == kind && string.Equals(_quickFilter, quickFilter, StringComparison.Ordinal) &&
                            (string.IsNullOrEmpty(role) || VfxLibraryLabels.Raw(_role.value) == role);
            button.EnableInClassList("selected-category", selected);
            var title = new Label(label);
            var badge = new Label(count.ToString());
            badge.AddToClassList("category-count");
            button.Add(title);
            button.Add(badge);
            _categoryHost.Add(button);
        }

        private void SelectCategory(VfxLibraryItemKind? kind, string role, string quickFilter)
        {
            _activeKind = kind;
            _quickFilter = quickFilter ?? string.Empty;
            _role.SetValueWithoutNotify(string.IsNullOrEmpty(role) ? "全部" : VfxLibraryLabels.Display(role));
            if (_quickFilter == "review")
                _review.SetValueWithoutNotify("待评审");
            else
                _review.SetValueWithoutNotify("全部");
            BuildCategories();
            RefreshCards();
        }

        private void RefreshCards()
        {
            if (_workspace == null || _grid == null)
                return;
            _rows.Clear();
            List<VfxLibraryItem> filtered;
            if (_quickFilter == "favorites")
                filtered = new List<VfxLibraryItem>();
            else if (_quickFilter == "exemplars" || _quickFilter == "gaps")
                filtered = _workspace.FilterSpecial(
                    _quickFilter,
                    _search.value,
                    VfxLibraryLabels.Raw(_role.value),
                    VfxLibraryLabels.Raw(_theme.value),
                    VfxLibraryLabels.Raw(_backend.value),
                    VfxLibraryLabels.Raw(_review.value));
            else if (_activeKind.HasValue)
                filtered = _workspace.Filter(
                    _activeKind.Value,
                    _search.value,
                    VfxLibraryLabels.Raw(_role.value),
                    VfxLibraryLabels.Raw(_theme.value),
                    VfxLibraryLabels.Raw(_backend.value),
                    VfxLibraryLabels.Raw(_review.value),
                    _quickFilter == "budget");
            else
            {
                filtered = new List<VfxLibraryItem>();
                foreach (VfxLibraryItemKind kind in Enum.GetValues(typeof(VfxLibraryItemKind)))
                    filtered.AddRange(_workspace.Filter(
                        kind,
                        _search.value,
                        VfxLibraryLabels.Raw(_role.value),
                        VfxLibraryLabels.Raw(_theme.value),
                        VfxLibraryLabels.Raw(_backend.value),
                        VfxLibraryLabels.Raw(_review.value),
                        false));
            }
            if (_quickFilter == "placeholders" || _quickFilter == "candidates" || _quickFilter == "production")
            {
                string maturity = _quickFilter == "production" ? "Production" : _quickFilter == "candidates" ? "Candidate" : "LegacyPlaceholder";
                filtered.RemoveAll(item => !string.Equals(item.VisualMaturity, maturity, StringComparison.Ordinal));
            }
            for (int i = 0; i < filtered.Count; i += _columns)
                _rows.Add(new VfxLibraryCardRow(filtered, i, _columns));
            _grid.Rebuild();
            UpdateCenterHeader(filtered.Count);
            if (filtered.Count == 0)
            {
                if (_workspace.SelectedItem != null)
                    SelectItem(null);
                return;
            }
            if (_workspace.SelectedItem == null || !filtered.Contains(_workspace.SelectedItem))
                SelectItem(filtered[0]);
        }

        private void UpdateCenterHeader(int count)
        {
            string title;
            string subtitle;
            if (_quickFilter == "favorites")
            {
                title = "收藏";
                subtitle = "编辑器本地集合 · 不进入语义目录";
            }
            else if (_quickFilter == "review")
            {
                title = "待评审资源";
                subtitle = "发布效果 / 配方 / 基础单元 · 自动流程不能批准为已就绪";
            }
            else if (_quickFilter == "exemplars")
            {
                title = "已标记样板";
                subtitle = "只有人工已就绪且明确标记的资源才能成为正向生成样板";
            }
            else if (_quickFilter == "gaps")
            {
                title = "复用缺口";
                subtitle = "按角色聚合尚无已就绪复用入口的资源";
            }
            else if (_quickFilter == "placeholders" || _quickFilter == "candidates" || _quickFilter == "production")
            {
                title = _quickFilter == "production" ? "Production 资源" : _quickFilter == "candidates" ? "创作候选" : "占位待替换";
                subtitle = _quickFilter == "placeholders" ? "仅保留触发与结构合同，禁止作为视觉复用来源" : "按视觉成熟度筛选资源";
            }
            else if (_activeKind == VfxLibraryItemKind.Recipe)
            {
                title = "配方";
                subtitle = "浏览有序基础单元组合与消费关系";
            }
            else if (_activeKind == VfxLibraryItemKind.Primitive)
            {
                title = "基础单元";
                subtitle = "浏览技术资产、参数合同与兼容锚点";
            }
            else
            {
                title = "发布效果";
                subtitle = "大图卡片 · 双击打开资源";
            }
            _centerTitle.text = title + "  " + count;
            _centerSubtitle.text = subtitle;
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("card-row");
            for (int i = 0; i < 4; i++)
                row.Add(new VfxLibraryEffectCard(SelectItem, OpenAsset));
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            VfxLibraryCardRow row = _rows[index];
            for (int i = 0; i < 4; i++)
                ((VfxLibraryEffectCard)element[i]).Bind(i < row.Items.Count ? row.Items[i] : null, _workspace.SelectedItem);
        }

        private void SelectItem(VfxLibraryItem item)
        {
            _workspace.Select(item);
            _grid.RefreshItems();
            _preview.Select(item?.AssetPath ?? string.Empty);
            RefreshPlaybackState();
            RefreshTab();
            rootVisualElement.Q<Label>("selection-title").text = item == null ? "未选择资源" : item.DisplayName;
            rootVisualElement.Q<Label>("selection-id").text = item?.StableId ?? string.Empty;
            Label status = rootVisualElement.Q<Label>("review-status");
            status.text = item == null ? string.Empty : (item.VisualMaturity == "LegacyPlaceholder" ? "占位待替换" : item.VisualMaturity == "Candidate" ? "候选 · " + VfxLibraryLabels.Display(item.ReviewStatus) : "Production · " + VfxLibraryLabels.Display(item.ReviewStatus));
            status.EnableInClassList("ready", item?.ReviewStatus == "Ready");
            _openAssetButton.text = item?.Kind == VfxLibraryItemKind.Effect ? "打开预制体" : item?.Kind == VfxLibraryItemKind.Primitive ? "打开技术资产" : "打开关联资产";
            bool hasAsset = item != null && !string.IsNullOrEmpty(item.AssetPath);
            _openAssetButton.SetEnabled(hasAsset);
            _saveDraftButton.SetEnabled(item is VfxLibraryEffect);
            _applyDraftButton.SetEnabled(item is VfxLibraryEffect && _workspace.Draft.HasChanges);
        }

        private void SelectTab(string tab)
        {
            _activeTab = tab;
            foreach (Button button in rootVisualElement.Query<Button>(className: "vfx-tab").ToList())
                button.EnableInClassList("active", string.Equals(button.text, tab, StringComparison.Ordinal));
            RefreshTab();
        }

        private void RefreshTab()
        {
            if (_tabHost == null)
                return;
            if (_workspace?.SelectedItem == null)
            {
                _tabHost.Clear();
                var empty = new Label("从左侧选择发布效果、配方或基础单元查看详情。");
                empty.AddToClassList("empty-state");
                _tabHost.Add(empty);
                return;
            }
            VfxLibraryDetailBuilder.Build(_tabHost, _activeTab, _workspace.SelectedItem, _workspace);
        }

        private void SetMode(WindowMode mode)
        {
            _mode = mode;
            bool library = mode == WindowMode.Library;
            if (library)
                EditorPrefs.SetBool(SourceImportActivePreferenceKey(Application.dataPath), false);
            _libraryWorkspace.style.display = library ? DisplayStyle.Flex : DisplayStyle.None;
            _creativeWorkspace.style.display = library ? DisplayStyle.None : DisplayStyle.Flex;
            _libraryModeButton.EnableInClassList("mode-selected", library);
            _creativeModeButton.EnableInClassList("mode-selected", !library);
            if (_studioModeSwitch != null)
                _studioModeSwitch.style.display = library ? DisplayStyle.None : DisplayStyle.Flex;
            if (_creativeResetButton != null)
                _creativeResetButton.style.display = !library && rootVisualElement.resolvedStyle.width >= 1500f ? DisplayStyle.Flex : DisplayStyle.None;
            if (library)
            {
                RefreshCards();
            }
            else
            {
                _studio?.SetWorkspace(_workspace);
                SetStudioMode(_studioMode);
            }
        }

        private void SetStudioMode(StudioMode mode)
        {
            _studioMode = mode;
            bool manual = mode == StudioMode.Manual;
            bool sourceImport = mode == StudioMode.SourceImport;
            bool developmentAgent = mode == StudioMode.DevelopmentAgent;
            EditorPrefs.SetBool(SourceImportActivePreferenceKey(Application.dataPath), sourceImport);
            if (_manualStudioWorkspace != null) _manualStudioWorkspace.style.display = manual ? DisplayStyle.Flex : DisplayStyle.None;
            if (_sourceImportStudioWorkspace != null) _sourceImportStudioWorkspace.style.display = sourceImport ? DisplayStyle.Flex : DisplayStyle.None;
            if (_agentStudioWorkspace != null) _agentStudioWorkspace.style.display = developmentAgent ? DisplayStyle.Flex : DisplayStyle.None;
            _manualStudioButton?.EnableInClassList("mode-selected", manual);
            _sourceImportStudioButton?.EnableInClassList("mode-selected", sourceImport);
            _agentStudioButton?.EnableInClassList("mode-selected", developmentAgent);
            if (!sourceImport)
                EditorPrefs.SetInt("MxFramework.Vfx.CreationStudioMode", manual ? 0 : 1);
            if ((manual || sourceImport) && _mode == WindowMode.CreationLab)
            {
                ScheduleManualStudioInitialization();
                if (sourceImport) _manualStudio?.EnterSourcePrefabImportWorkspace();
                else _manualStudio?.ExitSourcePrefabImportWorkspace();
            }
            else if (developmentAgent && _mode == WindowMode.CreationLab)
                ScheduleAgentStudioInitialization();
        }

        internal static bool ShouldRestoreSourceImportWorkspace(string dataPath)
        {
            return EditorPrefs.GetBool(SourceImportActivePreferenceKey(dataPath), false);
        }

        internal static string SourceImportActivePreferenceKey(string dataPath)
        {
            string normalized = (dataPath ?? string.Empty).Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
            return "MxFramework.Vfx.SourceImport.Active." + Hash128.Compute(normalized);
        }

        private void ScheduleAgentStudioInitialization()
        {
            if (_studio != null || _agentStudioInitializationScheduled)
                return;
            _agentStudioInitializationScheduled = true;
            Label status = rootVisualElement.Q<Label>("creative-status");
            if (status != null)
                status.text = "正在加载 Development Agent 工作区并校验最近任务…";
            rootVisualElement.schedule.Execute(() =>
            {
                _agentStudioInitializationScheduled = false;
                if (_studio != null || rootVisualElement.panel == null)
                    return;
                _studio = new VfxLibraryAuthoringStudioController(
                    rootVisualElement,
                    new VfxLibraryTempCreationPlanService(),
                    OpenLibraryItemFromStudio,
                    OpenReuseGapFromStudio);
                _studio.SetWorkspace(_workspace);
                _studio.UpdateResponsiveState(rootVisualElement.resolvedStyle.width);
            }).ExecuteLater(1L);
        }

        private void ScheduleManualStudioInitialization()
        {
            if (_manualStudio != null || _manualStudioInitializationScheduled)
                return;
            _manualStudioInitializationScheduled = true;
            Label summary = rootVisualElement.Q<Label>("element-summary");
            if (summary != null)
                summary.text = "正在加载精选 Element 与源索引…";
            rootVisualElement.schedule.Execute(() =>
            {
                _manualStudioInitializationScheduled = false;
                if (_manualStudio != null || rootVisualElement.panel == null)
                    return;
                _manualStudio = new VfxManualCompositionStudioController(
                    rootVisualElement,
                    () => SetStudioMode(StudioMode.SourceImport),
                    () => SetStudioMode(StudioMode.Manual));
                _manualStudio.SetWorkspace(_workspace);
                _manualStudio.UpdateResponsiveState(rootVisualElement.resolvedStyle.width);
                if (_studioMode == StudioMode.SourceImport)
                    _manualStudio.EnterSourcePrefabImportWorkspace();
            }).ExecuteLater(1L);
        }

        private void OpenLibraryItemFromStudio(VfxLibraryItem item)
        {
            SetMode(WindowMode.Library);
            _activeKind = item.Kind;
            _quickFilter = string.Empty;
            _role.SetValueWithoutNotify("全部");
            _review.SetValueWithoutNotify("全部");
            BuildCategories();
            RefreshCards();
            SelectItem(item);
        }

        private void OpenReuseGapFromStudio(string role)
        {
            SetMode(WindowMode.Library);
            _activeKind = null;
            _quickFilter = "gaps";
            _role.SetValueWithoutNotify(VfxLibraryLabels.Display(role));
            _review.SetValueWithoutNotify("全部");
            BuildCategories();
            RefreshCards();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateColumns();
            _studio?.UpdateResponsiveState(evt.newRect.width);
            _manualStudio?.UpdateResponsiveState(evt.newRect.width);
            rootVisualElement.EnableInClassList("window-compact", evt.newRect.width < 1280f);
            if (_creativeResetButton != null)
                _creativeResetButton.style.display = _mode == WindowMode.CreationLab && evt.newRect.width >= 1500f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateColumns()
        {
            if (_mode != WindowMode.Library)
                return;
            float width = rootVisualElement.Q("center-pane").resolvedStyle.width;
            int next = Mathf.Clamp(Mathf.FloorToInt(width / 235f), 1, 4);
            if (next == _columns)
                return;
            _columns = next;
            RefreshCards();
        }

        private void GenerateThumbnails()
        {
            VfxAuthoringThumbnailGenerator.GenerateAll();
            Reload();
        }

        private static void OpenShowcase()
        {
            EditorSceneManager.OpenScene(ShowcasePath, OpenSceneMode.Single);
        }

        private void OpenSelectedAsset()
        {
            if (_workspace?.SelectedItem != null)
                OpenAsset(_workspace.SelectedItem);
        }

        private static void OpenAsset(VfxLibraryItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.AssetPath))
                return;
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.AssetPath);
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            AssetDatabase.OpenAsset(asset);
        }

        private void CopyImpactCommand()
        {
            if (_workspace?.SelectedItem == null)
                return;
            EditorGUIUtility.systemCopyBuffer = "mx-authoring vfx impact --id " + _workspace.SelectedItem.StableId;
            ShowNotification(new GUIContent("影响分析命令已复制"));
        }

        private void SaveDraft()
        {
            if (!(_workspace?.SelectedItem is VfxLibraryEffect effect))
                return;
            _workspace.Draft.Capture(effect);
            _applyDraftButton.SetEnabled(_workspace.Draft.HasChanges);
            RefreshTab();
        }

        private void ApplyDraft()
        {
            if (_workspace == null || !_workspace.Draft.HasChanges)
                return;
            if (!_workspace.Draft.TryPrepareApply(CatalogPath, out string plan, out string preparationMessage))
            {
                EditorUtility.DisplayDialog("已阻止", preparationMessage, "确定");
                return;
            }
            if (!EditorUtility.DisplayDialog("确认应用 VFX 语义目录变更", plan + "\n\n该操作不会写入预制体、图形资产、材质或纹理。", "确认应用", "取消"))
                return;
            bool success = _workspace.Draft.CommitPrepared(CatalogPath, out string message);
            EditorUtility.DisplayDialog(success ? "已应用" : "已阻止", message, "确定");
            if (success)
                Reload();
        }

        private void RebuildMissingThumbnails()
        {
            try
            {
                VfxAuthoringThumbnailGenerator.GenerateAll();
                Reload();
            }
            catch (Exception exception)
            {
                Debug.LogError("VFX 资源库自动重建缩略图失败：" + exception);
            }
        }

        private void DisposePreview()
        {
            _preview?.Dispose();
            _preview = null;
        }

        private void OnEditorUpdate()
        {
            if (_mode == WindowMode.CreationLab)
            {
                if (_studioMode == StudioMode.Manual || _studioMode == StudioMode.SourceImport) _manualStudio?.Update();
                else _studio?.Update();
                return;
            }
            if (_preview == null || _preview.State != VfxLibraryPlaybackState.Playing)
                return;
            _previewCanvas?.MarkDirtyRepaint();
            Repaint();
        }

        private void RefreshPlaybackState()
        {
            VfxLibraryPlaybackState state = _preview?.State ?? VfxLibraryPlaybackState.Stopped;
            _playButton?.EnableInClassList("playback-active", state == VfxLibraryPlaybackState.Playing);
            _pauseButton?.EnableInClassList("playback-active", state == VfxLibraryPlaybackState.Paused);
            _stopButton?.EnableInClassList("playback-active", state == VfxLibraryPlaybackState.Stopped);
            if (_playbackStatus == null)
                return;
            _playbackStatus.text = state == VfxLibraryPlaybackState.Playing ? "播放中" : state == VfxLibraryPlaybackState.Paused ? "已暂停" : "已停止";
            _playbackStatus.EnableInClassList("playing", state == VfxLibraryPlaybackState.Playing);
            _playbackStatus.EnableInClassList("paused", state == VfxLibraryPlaybackState.Paused);
            _playbackStatus.EnableInClassList("stopped", state == VfxLibraryPlaybackState.Stopped);
        }
    }
}
