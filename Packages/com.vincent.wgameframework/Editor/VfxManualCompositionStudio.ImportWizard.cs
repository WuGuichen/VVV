using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    /// <summary>Full-screen source Prefab import workspace.</summary>
    /// <remarks>Editor-only, AllocByDesign. Filtering, analysis, thumbnails and preview creation run only on explicit authoring input.</remarks>
    internal sealed partial class VfxManualCompositionStudioController
    {
        private const string RecentSourcePrefabPreference = "MxFramework.Vfx.SourceImport.Recents";
        private const int SourceImportWorkspaceStateVersion = 2;
        private const double SourceImportWorkspaceSaveDelaySeconds = 0.25d;
        private const int MaxRecentSourcePrefabs = 20;
        private readonly VfxLibraryPreviewSession _sourceImportPreview = new VfxLibraryPreviewSession();
        private readonly VfxLibraryPreviewSession _sourceImportDerivedPreview = new VfxLibraryPreviewSession();
        private readonly VfxSourcePrefabDraftImportService _sourcePrefabImporter = new VfxSourcePrefabDraftImportService();
        private readonly VfxAuthoredElementLibraryService _authoredElementLibrary = new VfxAuthoredElementLibraryService();
        private readonly List<VfxSourceEffectView> _sourceImportItems = new List<VfxSourceEffectView>();
        private readonly List<VfxSourceEffectView> _filteredSourceImportItems = new List<VfxSourceEffectView>();
        private readonly List<VfxSourceElementView> _sourceImportExistingElements = new List<VfxSourceElementView>();
        private readonly List<string> _recentSourcePrefabGuids = LoadRecentSourcePrefabGuids();
        private readonly List<TreeViewItemData<SourceImportFolderNode>> _sourceImportFolderRoots = new List<TreeViewItemData<SourceImportFolderNode>>();
        private readonly Dictionary<string, SourceImportFolderNode> _sourceImportFolders = new Dictionary<string, SourceImportFolderNode>(StringComparer.Ordinal);
        private readonly HashSet<string> _expandedSourceImportFolders = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, SourceImportMemberNode> _sourceImportMemberNodes = new Dictionary<string, SourceImportMemberNode>(StringComparer.Ordinal);
        private VisualElement _sourceImportWorkspace;
        private ToolbarSearchField _sourceImportSearch;
        private VisualElement _sourceImportFolderPanel;
        private VisualElement _sourceImportFolderTreeHost;
        private Button _sourceImportFolderCollapse;
        private TreeView _sourceImportFolderTree;
        private DropdownField _sourceImportTheme;
        private DropdownField _sourceImportFamily;
        private DropdownField _sourceImportRole;
        private DropdownField _sourceImportQuality;
        private ListView _sourceImportList;
        private ListView _sourceImportExistingList;
        private TreeView _sourceImportMemberTree;
        private readonly List<TreeViewItemData<SourceImportMemberNode>> _sourceImportMemberRoots = new List<TreeViewItemData<SourceImportMemberNode>>();
        private readonly HashSet<string> _selectedSourceImportMemberIds = new HashSet<string>(StringComparer.Ordinal);
        private SourceImportMemberNode _currentSourceImportMemberNode;
        private Label _sourceImportCount;
        private Label _sourceImportEmpty;
        private Label _sourceImportName;
        private Label _sourceImportPath;
        private Label _sourceImportState;
        private Label _sourceImportMeta;
        private Label _sourceImportDiagnostics;
        private Label _sourceImportDraft;
        private Label _sourceImportDirty;
        private Label _sourceImportIndexStatus;
        private Label _sourceImportFrameLabel;
        private Label _sourceImportMembersCount;
        private Label _sourceImportValidationIndex;
        private Label _sourceImportValidationHash;
        private Label _sourceImportValidationMembers;
        private Label _sourceImportValidationSubEmitters;
        private Label _sourceImportExistingCount;
        private Label _sourceImportExistingEmpty;
        private VisualElement _sourceImportDrop;
        private VisualElement _sourceImportSelectionIcon;
        private Button _sourceImportSourceAll;
        private Button _sourceImportSourceFavorite;
        private Button _sourceImportSourceRecent;
        private Button _sourceImportFavorite;
        private Button _sourceImportAdd;
        private Button _sourceImportAddLayer;
        private Button _sourceImportRebuild;
        private Button _sourceImportUpdateEvidence;
        private TextField _sourceImportDisplayName;
        private TextField _sourceImportSourceName;
        private Slider _sourceImportTimeline;
        private DropdownField _sourceImportSpeed;
        private DropdownField _sourceImportBackground;
        private Toggle _sourceImportLoop;
        private DropdownField _sourceImportMotionMode;
        private Slider _sourceImportMotionDirection;
        private Slider _sourceImportMotionDistance;
        private Slider _sourceImportMotionSpeed;
        private Slider _sourceImportMotionHeight;
        private DropdownField _sourceImportMotionCamera;
        private IMGUIContainer _sourceImportPreviewCanvas;
        private IMGUIContainer _sourceImportDerivedPreviewCanvas;
        private Label _sourceImportDerivedEmpty;
        private Toggle _sourceImportDerivedSolo;
        private VfxSourcePrefabDraftImportAnalysis _sourceImportAnalysis;
        private VfxSourcePrefabMemberSelectionAnalysis _sourceImportSelectionAnalysis;
        private VfxSourcePrefabDraftImportPlan _sourceImportPlan;
        private string _authoredElementLibraryHash = string.Empty;
        private string _sourceImportSource = "all";
        private string _selectedSourceImportFolder = string.Empty;
        private bool _sourceImportBinding;
        private bool _sourceImportExistingBinding;
        private bool _sourceImportFolderTreeBinding;
        private bool _sourceImportMemberTreeBinding;
        private bool _sourceImportThumbnailRefreshScheduled;
        private bool _sourceImportRestoring;
        private bool _sourceImportMotionConfigured;
        private bool _sourceImportHasEntered;
        private bool _sourceImportStateSavePending;
        private double _sourceImportStateSaveDueTime;
        private SourceImportWorkspaceState _sourceImportSavedState;

        private void InitializeSourcePrefabImportWizard()
        {
            _sourceImportWorkspace = _root.Q("source-prefab-import-workspace");
            _sourceImportSearch = _root.Q<ToolbarSearchField>("source-prefab-import-search");
            _sourceImportFolderPanel = _root.Q("source-prefab-import-folder-panel");
            _sourceImportFolderTreeHost = _root.Q("source-prefab-import-folder-tree-host");
            _sourceImportFolderCollapse = _root.Q<Button>("source-prefab-import-folder-collapse");
            _sourceImportTheme = _root.Q<DropdownField>("source-prefab-import-theme");
            _sourceImportFamily = _root.Q<DropdownField>("source-prefab-import-family");
            _sourceImportRole = _root.Q<DropdownField>("source-prefab-import-role");
            _sourceImportQuality = _root.Q<DropdownField>("source-prefab-import-quality");
            _sourceImportList = _root.Q<ListView>("source-prefab-import-list");
            _sourceImportExistingList = _root.Q<ListView>("source-prefab-import-existing-list");
            _sourceImportCount = _root.Q<Label>("source-prefab-import-count");
            _sourceImportEmpty = _root.Q<Label>("source-prefab-import-empty");
            _sourceImportName = _root.Q<Label>("source-prefab-import-name");
            _sourceImportPath = _root.Q<Label>("source-prefab-import-path");
            _sourceImportState = _root.Q<Label>("source-prefab-import-state");
            _sourceImportMeta = _root.Q<Label>("source-prefab-import-meta");
            _sourceImportDiagnostics = _root.Q<Label>("source-prefab-import-diagnostics");
            _sourceImportDraft = _root.Q<Label>("source-prefab-import-draft");
            _sourceImportDirty = _root.Q<Label>("source-prefab-import-dirty");
            _sourceImportIndexStatus = _root.Q<Label>("source-prefab-import-index-status");
            _sourceImportFrameLabel = _root.Q<Label>("source-prefab-import-frame-label");
            _sourceImportMembersCount = _root.Q<Label>("source-prefab-import-members-count");
            _sourceImportValidationIndex = _root.Q<Label>("source-prefab-import-validation-index");
            _sourceImportValidationHash = _root.Q<Label>("source-prefab-import-validation-hash");
            _sourceImportValidationMembers = _root.Q<Label>("source-prefab-import-validation-members");
            _sourceImportValidationSubEmitters = _root.Q<Label>("source-prefab-import-validation-subemitters");
            _sourceImportExistingCount = _root.Q<Label>("source-prefab-import-existing-count");
            _sourceImportExistingEmpty = _root.Q<Label>("source-prefab-import-existing-empty");
            _sourceImportDrop = _root.Q("source-prefab-import-drop");
            _sourceImportSelectionIcon = _root.Q("source-prefab-import-selection-icon");
            _sourceImportSourceAll = _root.Q<Button>("source-prefab-import-source-all");
            _sourceImportSourceFavorite = _root.Q<Button>("source-prefab-import-source-favorite");
            _sourceImportSourceRecent = _root.Q<Button>("source-prefab-import-source-recent");
            _sourceImportFavorite = _root.Q<Button>("source-prefab-import-favorite");
            _sourceImportAdd = _root.Q<Button>("source-prefab-import-add");
            _sourceImportAddLayer = _root.Q<Button>("source-prefab-import-add-layer");
            _sourceImportRebuild = _root.Q<Button>("source-prefab-import-rebuild");
            _sourceImportUpdateEvidence = _root.Q<Button>("source-prefab-import-update-evidence");
            _sourceImportDisplayName = _root.Q<TextField>("source-prefab-import-display-name");
            _sourceImportSourceName = _root.Q<TextField>("source-prefab-import-source-name");
            _sourceImportTimeline = _root.Q<Slider>("source-prefab-import-timeline");
            _sourceImportSpeed = _root.Q<DropdownField>("source-prefab-import-speed");
            _sourceImportBackground = _root.Q<DropdownField>("source-prefab-import-background");
            _sourceImportLoop = _root.Q<Toggle>("source-prefab-import-loop");
            _sourceImportMotionMode = _root.Q<DropdownField>("source-prefab-import-motion-mode");
            _sourceImportMotionDirection = _root.Q<Slider>("source-prefab-import-motion-direction");
            _sourceImportMotionDistance = _root.Q<Slider>("source-prefab-import-motion-distance");
            _sourceImportMotionSpeed = _root.Q<Slider>("source-prefab-import-motion-speed");
            _sourceImportMotionHeight = _root.Q<Slider>("source-prefab-import-motion-height");
            _sourceImportMotionCamera = _root.Q<DropdownField>("source-prefab-import-motion-camera");
            _sourceImportDerivedSolo = _root.Q<Toggle>("source-prefab-import-derived-solo");
            _sourceImportSourceName.SetEnabled(false);

            InitializeSourceImportFolderTree();

            VisualElement sourcePreviewHost = _root.Q("source-prefab-import-source-preview-host");
            VisualElement derivedPreviewHost = _root.Q("source-prefab-import-derived-preview-host");
            _sourceImportDerivedEmpty = _root.Q<Label>("source-prefab-import-derived-empty");
            _sourceImportPreviewCanvas = new IMGUIContainer(() => DrawSynchronizedSourceImportPreview(_sourceImportPreview, _sourceImportDerivedPreview, _sourceImportPreviewCanvas));
            _sourceImportPreviewCanvas.AddToClassList("source-prefab-import-preview-canvas");
            sourcePreviewHost.Add(_sourceImportPreviewCanvas);
            _sourceImportDerivedPreviewCanvas = new IMGUIContainer(() => DrawSynchronizedSourceImportPreview(_sourceImportDerivedPreview, _sourceImportPreview, _sourceImportDerivedPreviewCanvas));
            _sourceImportDerivedPreviewCanvas.AddToClassList("source-prefab-import-preview-canvas");
            derivedPreviewHost.Add(_sourceImportDerivedPreviewCanvas);

            _sourceImportList.fixedItemHeight = 76f;
            _sourceImportList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _sourceImportList.itemsSource = _filteredSourceImportItems;
            _sourceImportList.makeItem = CreateSourceImportRow;
            _sourceImportList.bindItem = BindSourceImportRow;
            _sourceImportList.selectionChanged += values =>
            {
                if (_sourceImportBinding) return;
                VfxSourceEffectView effect = values.OfType<VfxSourceEffectView>().FirstOrDefault();
                if (effect != null)
                {
                    AnalyzeSourceImportPath(effect.PrefabPath);
                    ScheduleSourceImportWorkspaceStateSave();
                }
            };

            InitializeSourceImportExistingElementsList();
            InitializeSourceImportMemberTree();

            SetChoices(_sourceImportSpeed, new[] { "0.25×", "0.5×", "1.0×", "2.0×" });
            _sourceImportSpeed.SetValueWithoutNotify("1.0×");
            SetChoices(_sourceImportBackground, new[] { "暗色", "中性灰", "浅色" });
            _sourceImportBackground.SetValueWithoutNotify("中性灰");
            SetChoices(_sourceImportMotionMode, new[] { "静止", "直线", "往返", "环绕", "抛物线" });
            SetChoices(_sourceImportMotionCamera, new[] { "固定", "跟随" });
            _sourceImportMotionMode.SetValueWithoutNotify("静止");
            _sourceImportMotionCamera.SetValueWithoutNotify("固定");
            _sourceImportPreview.SetBackground("中性灰");
            _sourceImportDerivedPreview.SetBackground("中性灰");
            _sourceImportSavedState = LoadSourceImportWorkspaceState();
            _sourceImportSearch.RegisterValueChangedCallback(_ => { FilterSourceImportItems(); ScheduleSourceImportWorkspaceStateSave(); });
            foreach (DropdownField field in new[] { _sourceImportTheme, _sourceImportFamily, _sourceImportRole, _sourceImportQuality })
                field.RegisterValueChangedCallback(_ => { FilterSourceImportItems(); ScheduleSourceImportWorkspaceStateSave(); });
            _sourceImportFolderCollapse.clicked += ToggleSourceImportFolderPanel;
            _sourceImportSourceAll.clicked += () => SetSourceImportSource("all");
            _sourceImportSourceFavorite.clicked += () => SetSourceImportSource("favorite");
            _sourceImportSourceRecent.clicked += () => SetSourceImportSource("recent");
            _sourceImportFavorite.clicked += ToggleSelectedSourceImportFavorite;
            _root.Q<Button>("source-prefab-import-back").clicked += ReturnToManualWorkspace;
            _root.Q<Button>("source-prefab-import-cancel").clicked += ReturnToManualWorkspace;
            _root.Q<Button>("source-prefab-import-use-selection").clicked += UseSelectedProjectPrefab;
            _root.Q<Button>("source-prefab-import-play").clicked += () => { BeginContinuousRefresh(); ApplyToSourceImportPreviews(value => value.Play()); RefreshSourceImportPreview(); };
            _root.Q<Button>("source-prefab-import-pause").clicked += () => { ApplyToSourceImportPreviews(value => value.Pause()); RefreshSourceImportPreview(); };
            _root.Q<Button>("source-prefab-import-step").clicked += () => { ApplyToSourceImportPreviews(value => value.StepFrame()); RefreshSourceImportPreview(); };
            _root.Q<Button>("source-prefab-import-restart").clicked += () => { BeginContinuousRefresh(); ApplyToSourceImportPreviews(value => value.Restart()); RefreshSourceImportPreview(); };
            _root.Q<Button>("source-prefab-import-frame").clicked += () => { ApplyToSourceImportPreviews(value => value.FrameCamera()); SynchronizeSourceImportCamera(_sourceImportPreview, _sourceImportDerivedPreview); RefreshSourceImportPreview(); };
            _sourceImportLoop.RegisterValueChangedCallback(evt =>
            {
                ApplyToSourceImportPreviews(value => value.SetLoop(evt.newValue));
                ScheduleSourceImportWorkspaceStateSave();
            });
            _sourceImportTimeline.RegisterValueChangedCallback(evt =>
            {
                if (_sourceImportBinding || _sourceImportAnalysis?.Valid != true) return;
                ApplyToSourceImportPreviews(value => value.Scrub(evt.newValue));
                RefreshSourceImportPreview();
                ScheduleSourceImportWorkspaceStateSave();
            });
            _sourceImportSpeed.RegisterValueChangedCallback(evt =>
            {
                ApplyToSourceImportPreviews(value => value.SetSpeed(SourceImportSpeed(evt.newValue)));
                ScheduleSourceImportWorkspaceStateSave();
            });
            _sourceImportBackground.RegisterValueChangedCallback(evt =>
            {
                ApplyToSourceImportPreviews(value => value.SetBackground(evt.newValue));
                RefreshSourceImportPreview();
                ScheduleSourceImportWorkspaceStateSave();
            });
            _sourceImportMotionMode.RegisterValueChangedCallback(_ => OnSourceImportMotionChanged());
            _sourceImportMotionDirection.RegisterValueChangedCallback(_ => OnSourceImportMotionChanged());
            _sourceImportMotionDistance.RegisterValueChangedCallback(_ => OnSourceImportMotionChanged());
            _sourceImportMotionSpeed.RegisterValueChangedCallback(_ => OnSourceImportMotionChanged());
            _sourceImportMotionHeight.RegisterValueChangedCallback(_ => OnSourceImportMotionChanged());
            _sourceImportMotionCamera.RegisterValueChangedCallback(_ => OnSourceImportMotionChanged());
            _sourceImportDerivedSolo.RegisterValueChangedCallback(_ =>
            {
                RefreshSourceImportDerivedPreview();
                ScheduleSourceImportWorkspaceStateSave();
            });
            _root.Q<Button>("source-prefab-import-member-all").clicked += SelectAllSourceImportMembers;
            _root.Q<Button>("source-prefab-import-member-clear").clicked += ClearSourceImportMembers;
            _root.Q<Button>("source-prefab-import-member-branch").clicked += SelectCurrentSourceImportBranch;
            _root.Q<Button>("source-prefab-import-member-group").clicked += SelectCurrentSourceImportDependencyGroup;
            _sourceImportDisplayName.RegisterValueChangedCallback(_ =>
            {
                RefreshSourceImportActionState();
                ScheduleSourceImportWorkspaceStateSave();
            });
            _sourceImportRebuild.clicked += RebuildSourceIndexForImport;
            _sourceImportUpdateEvidence.clicked += () => ApplySourcePrefabImport(false);
            _sourceImportAdd.clicked += () => ApplySourcePrefabImport(false);
            _sourceImportAddLayer.clicked += () => ApplySourcePrefabImport(true);
            _sourceImportWorkspace.RegisterCallback<KeyDownEvent>(OnSourceImportKeyDown);
            RegisterSourceImportDropZone();
            ResetSourceImportSelection();
            RefreshSourceImportContext();
        }

        private void InitializeSourceImportExistingElementsList()
        {
            _sourceImportExistingList.fixedItemHeight = 48f;
            _sourceImportExistingList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _sourceImportExistingList.selectionType = SelectionType.Single;
            _sourceImportExistingList.itemsSource = _sourceImportExistingElements;
            _sourceImportExistingList.makeItem = () =>
            {
                var row = new VisualElement();
                row.AddToClassList("source-prefab-import-existing-row");
                var heading = new VisualElement();
                heading.AddToClassList("source-prefab-import-existing-name-row");
                var name = new Label { name = "name" };
                name.AddToClassList("source-prefab-import-existing-name");
                var badge = new Label { name = "badge", text = "当前组合" };
                badge.AddToClassList("source-prefab-import-existing-badge");
                var meta = new Label { name = "meta" };
                meta.AddToClassList("source-prefab-import-existing-meta");
                heading.Add(name);
                heading.Add(badge);
                row.Add(heading);
                row.Add(meta);
                return row;
            };
            _sourceImportExistingList.bindItem = (element, index) =>
            {
                VfxSourceElementView existing = _sourceImportExistingElements[index];
                bool current = MatchesSourceImportMemberSelection(
                    existing,
                    _sourceImportSelectionAnalysis?.Members.Select(value => value.ComponentId));
                element.Q<Label>("name").text = existing.DisplayName;
                element.Q<Label>("meta").text = (existing.MemberComponentIds?.Count ?? 0)
                    + " 个成员 · " + RoleLabel(existing.Roles?.FirstOrDefault() ?? "detail");
                element.EnableInClassList("current-combination", current);
                element.tooltip = existing.ElementId;
            };
            _sourceImportExistingList.selectionChanged += values =>
            {
                if (_sourceImportExistingBinding) return;
                VfxSourceElementView element = values.OfType<VfxSourceElementView>().FirstOrDefault();
                if (element != null) LoadSourceImportExistingElement(element);
                ScheduleSourceImportWorkspaceStateSave();
            };
        }

        private void InitializeSourceImportMemberTree()
        {
            _sourceImportMemberTree = new TreeView
            {
                name = "source-prefab-import-member-tree",
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = 34f,
                autoExpand = false
            };
            _sourceImportMemberTree.AddToClassList("source-prefab-import-member-tree");
            _sourceImportMemberTree.makeItem = () =>
            {
                var row = new VisualElement();
                row.AddToClassList("source-prefab-import-member-row");
                var toggle = new Toggle { name = "selected" };
                toggle.AddToClassList("source-prefab-import-member-toggle");
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (_sourceImportMemberTreeBinding || toggle.userData is not SourceImportMemberNode node) return;
                    SetSourceImportMemberSelection(node.MemberComponentIds, evt.newValue);
                });
                var copy = new VisualElement();
                copy.AddToClassList("source-prefab-import-member-copy");
                copy.Add(new Label { name = "name" });
                copy.Add(new Label { name = "meta" });
                row.Add(toggle);
                row.Add(copy);
                return row;
            };
            _sourceImportMemberTree.bindItem = (element, index) =>
            {
                SourceImportMemberNode node = _sourceImportMemberTree.GetItemDataForIndex<SourceImportMemberNode>(index);
                Toggle toggle = element.Q<Toggle>("selected");
                int selected = node.MemberComponentIds.Count(_selectedSourceImportMemberIds.Contains);
                _sourceImportMemberTreeBinding = true;
                toggle.userData = node;
                toggle.showMixedValue = selected > 0 && selected < node.MemberComponentIds.Count;
                toggle.SetValueWithoutNotify(selected == node.MemberComponentIds.Count && selected > 0);
                _sourceImportMemberTreeBinding = false;
                element.Q<Label>("name").text = node.DisplayName;
                element.Q<Label>("meta").text = node.Member == null
                    ? node.MemberComponentIds.Count + " 个成员"
                    : SourceImportMemberMeta(node.Member);
                element.tooltip = node.FullPath;
            };
            _sourceImportMemberTree.selectionChanged += values =>
            {
                bool wasSolo = _sourceImportDerivedSolo?.value == true;
                _currentSourceImportMemberNode = values.OfType<SourceImportMemberNode>().FirstOrDefault();
                RefreshSourceImportSoloState();
                if (wasSolo) RefreshSourceImportDerivedPreview();
                ScheduleSourceImportWorkspaceStateSave();
            };
            _sourceImportMemberTree.itemExpandedChanged += _ => ScheduleSourceImportWorkspaceStateSave();
            _root.Q("source-prefab-import-member-tree-host").Add(_sourceImportMemberTree);
        }

        private static string SourceImportMemberMeta(VfxSourceComponentView member)
        {
            string role = member.Roles?.FirstOrDefault() ?? "detail";
            return RoleLabel(role) + " · 粒子预算 " + member.MaxParticles + " · " + member.ObjectPath;
        }

        private void BuildSourceImportMemberTree()
        {
            _sourceImportMemberRoots.Clear();
            _sourceImportMemberNodes.Clear();
            _currentSourceImportMemberNode = null;
            if (_sourceImportAnalysis?.Valid != true)
            {
                _sourceImportMemberTree.SetRootItems(_sourceImportMemberRoots);
                _sourceImportMemberTree.Rebuild();
                return;
            }

            var root = new SourceImportMemberNode(_sourceImportAnalysis.Effect.DisplayName, _sourceImportAnalysis.Effect.DisplayName);
            var nodesByPath = new Dictionary<string, SourceImportMemberNode>(StringComparer.Ordinal) { [string.Empty] = root };
            foreach (VfxSourceComponentView member in _sourceImportAnalysis.Members)
            {
                string parentPath = string.Empty;
                SourceImportMemberNode parent = root;
                string[] segments = (member.ObjectPath ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < segments.Length; i++)
                {
                    string path = string.IsNullOrEmpty(parentPath) ? segments[i] : parentPath + "/" + segments[i];
                    if (!nodesByPath.TryGetValue(path, out SourceImportMemberNode node))
                    {
                        node = new SourceImportMemberNode(segments[i], path);
                        nodesByPath[path] = node;
                        parent.Children.Add(node);
                    }
                    parent = node;
                    parentPath = path;
                }
                if (parent.Member != null)
                {
                    var componentNode = new SourceImportMemberNode(member.DisplayName, parent.FullPath + "#" + member.LocalFileId)
                    {
                        Member = member
                    };
                    parent.Children.Add(componentNode);
                }
                else
                {
                    parent.Member = member;
                    parent.DisplayName = member.DisplayName;
                }
            }
            PopulateSourceImportMemberDescendants(root);
            IndexSourceImportMemberNodes(root);
            _sourceImportMemberRoots.Add(ToSourceImportMemberTreeItem(root));
            _sourceImportMemberTree.SetRootItems(_sourceImportMemberRoots);
            _sourceImportMemberTree.Rebuild();
            _sourceImportMemberTree.ExpandItem(SourceImportStableTreeId(root.FullPath));
        }

        private void IndexSourceImportMemberNodes(SourceImportMemberNode node)
        {
            _sourceImportMemberNodes[node.FullPath] = node;
            for (int i = 0; i < node.Children.Count; i++)
                IndexSourceImportMemberNodes(node.Children[i]);
        }

        private static void PopulateSourceImportMemberDescendants(SourceImportMemberNode node)
        {
            node.MemberComponentIds.Clear();
            if (node.Member != null) node.MemberComponentIds.Add(node.Member.ComponentId);
            for (int i = 0; i < node.Children.Count; i++)
            {
                PopulateSourceImportMemberDescendants(node.Children[i]);
                node.MemberComponentIds.AddRange(node.Children[i].MemberComponentIds);
            }
        }

        private static TreeViewItemData<SourceImportMemberNode> ToSourceImportMemberTreeItem(SourceImportMemberNode node)
        {
            return new TreeViewItemData<SourceImportMemberNode>(
                SourceImportStableTreeId(node.FullPath),
                node,
                node.Children.Select(ToSourceImportMemberTreeItem).ToList());
        }

        private static int SourceImportStableTreeId(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++) hash = (hash ^ source[i]) * 16777619;
                return (int)(hash & 0x7fffffff);
            }
        }

        private void SetSourceImportMemberSelection(IEnumerable<string> memberIds, bool selected)
        {
            string[] changed = (memberIds ?? Array.Empty<string>()).ToArray();
            if (selected)
            {
                _selectedSourceImportMemberIds.UnionWith(changed);
            }
            else
            {
                var removal = new HashSet<string>(changed, StringComparer.Ordinal);
                if (_sourceImportSelectionAnalysis != null)
                {
                    foreach (VfxSourceMemberDependencyGroup group in _sourceImportSelectionAnalysis.DependencyGroups)
                        if (group.MemberComponentIds.Any(removal.Contains)) removal.UnionWith(group.MemberComponentIds);
                }
                _selectedSourceImportMemberIds.ExceptWith(removal);
            }
            RefreshSourceImportMemberSelection();
            ScheduleSourceImportWorkspaceStateSave();
        }

        private void SelectAllSourceImportMembers()
        {
            _selectedSourceImportMemberIds.Clear();
            if (_sourceImportAnalysis?.Valid == true)
                _selectedSourceImportMemberIds.UnionWith(_sourceImportAnalysis.Members.Select(value => value.ComponentId));
            RefreshSourceImportMemberSelection();
        }

        private void ClearSourceImportMembers()
        {
            _selectedSourceImportMemberIds.Clear();
            RefreshSourceImportMemberSelection();
        }

        private void SelectCurrentSourceImportBranch()
        {
            if (_currentSourceImportMemberNode == null) return;
            SetSourceImportMemberSelection(_currentSourceImportMemberNode.MemberComponentIds, true);
        }

        private void SelectCurrentSourceImportDependencyGroup()
        {
            if (_currentSourceImportMemberNode == null || _sourceImportSelectionAnalysis == null) return;
            string memberId = _currentSourceImportMemberNode.MemberComponentIds.FirstOrDefault();
            VfxSourceMemberDependencyGroup group = _sourceImportSelectionAnalysis.DependencyGroups
                .FirstOrDefault(value => value.MemberComponentIds.Contains(memberId));
            if (group == null) return;
            _selectedSourceImportMemberIds.Clear();
            _selectedSourceImportMemberIds.UnionWith(group.MemberComponentIds);
            RefreshSourceImportMemberSelection();
        }

        private void InitializeSourceImportFolderTree()
        {
            _sourceImportFolderTree = new TreeView
            {
                name = "source-prefab-import-folder-tree",
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = 28f,
                autoExpand = false
            };
            _sourceImportFolderTree.AddToClassList("source-prefab-import-folder-tree");
            _sourceImportFolderTree.makeItem = () =>
            {
                var row = new VisualElement();
                row.AddToClassList("source-prefab-import-folder-row");
                var name = new Label { name = "name" };
                name.AddToClassList("source-prefab-import-folder-name");
                var count = new Label { name = "count" };
                count.AddToClassList("source-prefab-import-folder-count");
                row.Add(name);
                row.Add(count);
                return row;
            };
            _sourceImportFolderTree.bindItem = (element, index) =>
            {
                SourceImportFolderNode node = _sourceImportFolderTree.GetItemDataForIndex<SourceImportFolderNode>(index);
                element.Q<Label>("name").text = node.DisplayName;
                element.Q<Label>("count").text = node.DescendantPrefabCount.ToString();
                element.tooltip = string.IsNullOrWhiteSpace(node.Path)
                    ? VfxSourceLibraryEditorService.SourceRoot
                    : VfxSourceLibraryEditorService.SourceRoot + "/" + node.Path;
            };
            _sourceImportFolderTree.selectionChanged += values =>
            {
                if (_sourceImportFolderTreeBinding) return;
                SourceImportFolderNode node = values.OfType<SourceImportFolderNode>().FirstOrDefault();
                if (node == null) return;
                _selectedSourceImportFolder = node.Path;
                FilterSourceImportItems();
                ScheduleSourceImportWorkspaceStateSave();
            };
            _sourceImportFolderTree.itemExpandedChanged += args =>
            {
                if (_sourceImportFolderTreeBinding) return;
                SourceImportFolderNode node = _sourceImportFolderTree.GetItemDataForId<SourceImportFolderNode>(args.id);
                if (node == null || string.IsNullOrWhiteSpace(node.Path)) return;
                if (args.isExpanded) _expandedSourceImportFolders.Add(node.Path);
                else _expandedSourceImportFolders.Remove(node.Path);
                PersistExpandedSourceImportFolders();
                ScheduleSourceImportWorkspaceStateSave();
            };
            _sourceImportFolderTreeHost.Add(_sourceImportFolderTree);
        }

        private void ToggleSourceImportFolderPanel()
        {
            bool collapsed = !_sourceImportFolderPanel.ClassListContains("collapsed");
            _sourceImportFolderPanel.EnableInClassList("collapsed", collapsed);
            _sourceImportFolderCollapse.text = collapsed ? "展开" : "收起";
            ScheduleSourceImportWorkspaceStateSave();
        }

        private void RebuildSourceImportFolderTree()
        {
            SourceImportFolderNode root = BuildSourceImportFolderTree(_sourceImportItems.Select(value => value.PrefabPath));
            _sourceImportFolders.Clear();
            IndexSourceImportFolderNodes(root, _sourceImportFolders);
            if (!_sourceImportFolders.ContainsKey(_selectedSourceImportFolder))
            {
                _selectedSourceImportFolder = string.Empty;
            }

            if (_expandedSourceImportFolders.Count == 0 && _sourceImportSavedState == null)
            {
                for (int i = 0; i < root.Children.Count; i++)
                    _expandedSourceImportFolders.Add(root.Children[i].Path);
                PersistExpandedSourceImportFolders();
            }

            _sourceImportFolderRoots.Clear();
            _sourceImportFolderRoots.Add(ToSourceImportFolderTreeItem(root));
            _sourceImportFolderTreeBinding = true;
            try
            {
                _sourceImportFolderTree.SetRootItems(_sourceImportFolderRoots);
                _sourceImportFolderTree.Rebuild();
                _sourceImportFolderTree.ExpandItem(root.TreeId);
                foreach (string path in _expandedSourceImportFolders)
                    if (_sourceImportFolders.TryGetValue(path, out SourceImportFolderNode node))
                        _sourceImportFolderTree.ExpandItem(node.TreeId);
                if (_sourceImportFolders.TryGetValue(_selectedSourceImportFolder, out SourceImportFolderNode selected))
                    _sourceImportFolderTree.SetSelectionByIdWithoutNotify(new[] { selected.TreeId });
            }
            finally { _sourceImportFolderTreeBinding = false; }
        }

        private void RevealSourceImportFolder(VfxSourceEffectView effect)
        {
            if (effect == null) return;
            string folder = SourceImportFolder(effect.PrefabPath);
            if (!_sourceImportFolders.TryGetValue(folder, out SourceImportFolderNode node)) return;

            _selectedSourceImportFolder = folder;
            for (SourceImportFolderNode current = node; current != null; current = ParentSourceImportFolder(current))
            {
                if (!string.IsNullOrWhiteSpace(current.Path)) _expandedSourceImportFolders.Add(current.Path);
                _sourceImportFolderTree.ExpandItem(current.TreeId);
            }
            PersistExpandedSourceImportFolders();
            _sourceImportFolderTreeBinding = true;
            try { _sourceImportFolderTree.SetSelectionByIdWithoutNotify(new[] { node.TreeId }); }
            finally { _sourceImportFolderTreeBinding = false; }

            _sourceImportSearch.SetValueWithoutNotify(string.Empty);
            foreach (DropdownField field in new[] { _sourceImportTheme, _sourceImportFamily, _sourceImportRole, _sourceImportQuality })
                field.SetValueWithoutNotify("全部");
            _sourceImportSource = "all";
            _sourceImportSourceAll.EnableInClassList("selected", true);
            _sourceImportSourceFavorite.EnableInClassList("selected", false);
            _sourceImportSourceRecent.EnableInClassList("selected", false);
            FilterSourceImportItems();
        }

        private SourceImportFolderNode ParentSourceImportFolder(SourceImportFolderNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Path)) return null;
            int separator = node.Path.LastIndexOf('/');
            string parentPath = separator < 0 ? string.Empty : node.Path.Substring(0, separator);
            return _sourceImportFolders.TryGetValue(parentPath, out SourceImportFolderNode parent) ? parent : null;
        }

        private static TreeViewItemData<SourceImportFolderNode> ToSourceImportFolderTreeItem(SourceImportFolderNode node)
        {
            var children = new List<TreeViewItemData<SourceImportFolderNode>>(node.Children.Count);
            for (int i = 0; i < node.Children.Count; i++)
                children.Add(ToSourceImportFolderTreeItem(node.Children[i]));
            return new TreeViewItemData<SourceImportFolderNode>(node.TreeId, node, children);
        }

        private static void IndexSourceImportFolderNodes(SourceImportFolderNode node, Dictionary<string, SourceImportFolderNode> index)
        {
            index[node.Path] = node;
            for (int i = 0; i < node.Children.Count; i++)
                IndexSourceImportFolderNodes(node.Children[i], index);
        }

        private void PersistExpandedSourceImportFolders()
        {
            ScheduleSourceImportWorkspaceStateSave();
        }

        private VisualElement CreateSourceImportRow()
        {
            var row = new VisualElement();
            row.AddToClassList("source-prefab-import-row");
            var thumbnail = new Image { name = "thumbnail", scaleMode = ScaleMode.ScaleToFit };
            thumbnail.AddToClassList("source-prefab-import-row-thumbnail");
            var copy = new VisualElement { name = "copy" };
            copy.AddToClassList("source-prefab-import-row-copy");
            var name = new Label { name = "name" };
            name.AddToClassList("source-prefab-import-row-name");
            var meta = new Label { name = "meta" };
            meta.AddToClassList("source-prefab-import-row-meta");
            var path = new Label { name = "path" };
            path.AddToClassList("source-prefab-import-row-path");
            copy.Add(name);
            copy.Add(meta);
            copy.Add(path);
            var favorite = new Button { name = "favorite", text = "☆", tooltip = "收藏源 Prefab" };
            favorite.AddToClassList("source-prefab-import-row-favorite");
            favorite.clicked += () =>
            {
                if (row.userData is VfxSourceEffectView effect)
                    ToggleSourceImportFavorite(effect);
            };
            row.Add(thumbnail);
            row.Add(copy);
            row.Add(favorite);
            return row;
        }

        private void BindSourceImportRow(VisualElement row, int index)
        {
            VfxSourceEffectView effect = _filteredSourceImportItems[index];
            row.userData = effect;
            row.Q<Label>("name").text = effect.DisplayName;
            row.Q<Label>("meta").text = ThemeLabel(effect.Theme).TrimEnd('·') + " · " + CategoryLabel(effect.Family)
                + " · PS: " + effect.ParticleSystemCount;
            row.Q<Label>("path").text = effect.PrefabPath;
            row.tooltip = effect.PrefabPath;
            bool favorite = IsSourceImportFavorite(effect);
            Button favoriteButton = row.Q<Button>("favorite");
            favoriteButton.text = favorite ? "★" : "☆";
            favoriteButton.EnableInClassList("favorite", favorite);
            Image image = row.Q<Image>("thumbnail");
            image.image = SourceImportThumbnail(effect, out bool loading);
            if (loading) ScheduleSourceImportThumbnailRefresh();
        }

        internal void EnterSourcePrefabImportWorkspace()
        {
            PopulateSourceImportItems();
            RefreshSourceImportContext();
            _sourceImportWorkspace.Focus();
            _sourceImportHasEntered = true;
            if (RestoreSourceImportWorkspaceState())
                return;
            string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            VfxSourceEffectView selected = FindIndexedSourceEffect(selectedPath);
            if (selected != null)
            {
                SelectSourceImportEffect(selected, true);
                return;
            }
            if (_filteredSourceImportItems.Count > 0) SelectSourceImportEffect(_filteredSourceImportItems[0]);
            else ResetSourceImportSelection();
        }

        internal void ExitSourcePrefabImportWorkspace()
        {
            SaveSourceImportWorkspaceStateNow();
            _sourceImportHasEntered = false;
            _sourceImportStateSavePending = false;
            ResetSourceImportSelection();
        }

        private void ReturnToManualWorkspace()
        {
            ExitSourcePrefabImportWorkspace();
            RefreshElements();
            _showManualWorkspace();
        }

        private void PopulateSourceImportItems()
        {
            _sourceImportItems.Clear();
            if (_catalog?.Index?.Effects != null)
                _sourceImportItems.AddRange(_catalog.Index.Effects
                    .Where(value => value != null && !string.IsNullOrWhiteSpace(value.PrefabPath))
                    .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(value => value.PrefabPath, StringComparer.Ordinal));

            _sourceImportBinding = true;
            try
            {
                _sourceImportTheme.choices = BuildSourceImportChoices(_sourceImportItems.Select(value => value.Theme));
                _sourceImportFamily.choices = BuildSourceImportChoices(_sourceImportItems.Select(value => value.Family));
                _sourceImportRole.choices = BuildSourceImportChoices(_sourceImportItems.SelectMany(value => value.Roles ?? new List<string>()));
                _sourceImportQuality.choices = BuildSourceImportChoices(_sourceImportItems.Select(value => value.QualityTier));
                foreach (DropdownField field in new[] { _sourceImportTheme, _sourceImportFamily, _sourceImportRole, _sourceImportQuality })
                    if (!field.choices.Contains(field.value)) field.SetValueWithoutNotify("全部");
            }
            finally { _sourceImportBinding = false; }
            RebuildSourceImportFolderTree();
            FilterSourceImportItems();
        }

        private static List<string> BuildSourceImportChoices(IEnumerable<string> values)
        {
            var result = new List<string> { "全部" };
            result.AddRange(values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal));
            return result;
        }

        internal static string SourceImportFolder(string prefabPath)
        {
            string normalized = (prefabPath ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            string root = VfxSourceLibraryEditorService.SourceRoot + "/";
            if (!normalized.StartsWith(root, StringComparison.Ordinal)) return string.Empty;
            int fileSeparator = normalized.LastIndexOf('/');
            if (fileSeparator < root.Length) return string.Empty;
            return normalized.Substring(root.Length, fileSeparator - root.Length);
        }

        internal static SourceImportFolderNode BuildSourceImportFolderTree(IEnumerable<string> prefabPaths)
        {
            var root = new SourceImportFolderNode(string.Empty, "全部文件夹");
            var nodes = new Dictionary<string, SourceImportFolderNode>(StringComparer.Ordinal)
            {
                [string.Empty] = root
            };
            foreach (string prefabPath in prefabPaths ?? Array.Empty<string>())
            {
                string folder = SourceImportFolder(prefabPath);
                if (string.IsNullOrWhiteSpace(folder)) continue;
                root.DescendantPrefabCount++;
                string currentPath = string.Empty;
                string[] segments = folder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                SourceImportFolderNode parent = root;
                for (int i = 0; i < segments.Length; i++)
                {
                    currentPath = string.IsNullOrWhiteSpace(currentPath) ? segments[i] : currentPath + "/" + segments[i];
                    if (!nodes.TryGetValue(currentPath, out SourceImportFolderNode node))
                    {
                        node = new SourceImportFolderNode(currentPath, segments[i]);
                        nodes.Add(currentPath, node);
                        parent.Children.Add(node);
                    }
                    node.DescendantPrefabCount++;
                    parent = node;
                }
            }
            SortSourceImportFolderChildren(root);
            AssignSourceImportFolderTreeIds(root, new HashSet<int>());
            return root;
        }

        private static void SortSourceImportFolderChildren(SourceImportFolderNode node)
        {
            node.Children.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < node.Children.Count; i++)
                SortSourceImportFolderChildren(node.Children[i]);
        }

        private static void AssignSourceImportFolderTreeIds(SourceImportFolderNode node, HashSet<int> usedIds)
        {
            int id = StableSourceImportFolderTreeId(node.Path);
            while (id == 0 || !usedIds.Add(id))
                id = id == int.MaxValue ? 1 : id + 1;
            node.TreeId = id;
            for (int i = 0; i < node.Children.Count; i++)
                AssignSourceImportFolderTreeIds(node.Children[i], usedIds);
        }

        private static int StableSourceImportFolderTreeId(string path)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string value = string.IsNullOrWhiteSpace(path) ? "<all-folders>" : path;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
                return (int)(hash & 0x7fffffffu);
            }
        }

        internal static bool MatchesSourceImportFolder(string prefabPath, string selectedFolder)
        {
            if (string.IsNullOrWhiteSpace(selectedFolder) || string.Equals(selectedFolder, "全部", StringComparison.Ordinal))
                return true;
            string folder = SourceImportFolder(prefabPath);
            return string.Equals(folder, selectedFolder, StringComparison.Ordinal)
                || folder.StartsWith(selectedFolder + "/", StringComparison.Ordinal);
        }

        private void SetSourceImportSource(string source)
        {
            _sourceImportSource = source;
            _sourceImportSourceAll.EnableInClassList("selected", source == "all");
            _sourceImportSourceFavorite.EnableInClassList("selected", source == "favorite");
            _sourceImportSourceRecent.EnableInClassList("selected", source == "recent");
            FilterSourceImportItems();
            ScheduleSourceImportWorkspaceStateSave();
        }

        private void FilterSourceImportItems()
        {
            if (_sourceImportBinding) return;
            string selectedPath = _sourceImportAnalysis?.PrefabPath ?? string.Empty;
            string query = (_sourceImportSearch.value ?? string.Empty).Trim();
            string theme = _sourceImportTheme.value;
            string family = _sourceImportFamily.value;
            string role = _sourceImportRole.value;
            string quality = _sourceImportQuality.value;
            _filteredSourceImportItems.Clear();
            for (int i = 0; i < _sourceImportItems.Count; i++)
            {
                VfxSourceEffectView effect = _sourceImportItems[i];
                if (_sourceImportSource == "favorite" && !IsSourceImportFavorite(effect)) continue;
                if (_sourceImportSource == "recent" && !_recentSourcePrefabGuids.Contains(effect.PrefabGuid)) continue;
                if (!string.IsNullOrWhiteSpace(query)
                    && !ContainsIgnoreCase(effect.DisplayName, query)
                    && !ContainsIgnoreCase(effect.PrefabPath, query)
                    && !ContainsIgnoreCase(effect.Theme, query)
                    && !ContainsIgnoreCase(effect.Family, query)
                    && !(effect.Roles ?? new List<string>()).Any(value => ContainsIgnoreCase(value, query))) continue;
                if (!MatchesSourceImportFolder(effect.PrefabPath, _selectedSourceImportFolder)) continue;
                if (theme != "全部" && !string.Equals(effect.Theme, theme, StringComparison.Ordinal)) continue;
                if (family != "全部" && !string.Equals(effect.Family, family, StringComparison.Ordinal)) continue;
                if (role != "全部" && !(effect.Roles ?? new List<string>()).Contains(role)) continue;
                if (quality != "全部" && !string.Equals(effect.QualityTier, quality, StringComparison.Ordinal)) continue;
                _filteredSourceImportItems.Add(effect);
            }
            if (_sourceImportSource == "recent")
                _filteredSourceImportItems.Sort((left, right) => _recentSourcePrefabGuids.IndexOf(left.PrefabGuid).CompareTo(_recentSourcePrefabGuids.IndexOf(right.PrefabGuid)));
            _sourceImportList.Rebuild();
            _sourceImportCount.text = "显示 " + _filteredSourceImportItems.Count + " / " + _sourceImportItems.Count + " · 虚拟列表";
            _sourceImportEmpty.style.display = _filteredSourceImportItems.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            VfxSourceEffectView selected = _filteredSourceImportItems.FirstOrDefault(value => string.Equals(value.PrefabPath, selectedPath, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(selectedPath) && selected == null) ResetSourceImportSelection();
        }

        private void SelectSourceImportEffect(VfxSourceEffectView effect, bool revealFolder = false)
        {
            if (revealFolder) RevealSourceImportFolder(effect);
            int index = _filteredSourceImportItems.IndexOf(effect);
            if (index >= 0)
            {
                _sourceImportBinding = true;
                try { _sourceImportList.SetSelection(index); }
                finally { _sourceImportBinding = false; }
                _sourceImportList.ScrollToItem(index);
            }
            AnalyzeSourceImportPath(effect.PrefabPath);
            ScheduleSourceImportWorkspaceStateSave();
        }

        private void AnalyzeSourceImportPath(string prefabPath)
        {
            _sourceImportAnalysis = _sourcePrefabImporter.Analyze(prefabPath, _catalog);
            _selectedSourceImportMemberIds.Clear();
            _sourceImportSelectionAnalysis = _sourcePrefabImporter.AnalyzeMemberSelection(_sourceImportAnalysis, _selectedSourceImportMemberIds);
            BuildSourceImportMemberTree();
            _sourceImportMembersCount.text = "0 / " + _sourceImportAnalysis.Members.Count;
            _sourceImportName.text = _sourceImportAnalysis.Effect?.DisplayName
                ?? AssetDatabase.LoadAssetAtPath<GameObject>(_sourceImportAnalysis.PrefabPath)?.name
                ?? "无法分析 Prefab";
            _sourceImportPath.text = _sourceImportAnalysis.PrefabPath;
            _sourceImportSourceName.SetValueWithoutNotify(_sourceImportName.text);

            if (_sourceImportAnalysis.Valid)
            {
                ApplySourceImportMotionSuggestion(_sourceImportAnalysis);
                _sourceImportPreview.Select(_sourceImportAnalysis.PrefabPath);
                _sourceImportPreview.SetTimelineDuration(Mathf.Max(1, _sourceImportAnalysis.EstimatedArtisticFrames) / (float)PreviewFramesPerSecond);
                _sourceImportPreview.SetLoop(_sourceImportLoop.value);
                _sourceImportPreview.SetSpeed(SourceImportSpeed(_sourceImportSpeed.value));
                _sourceImportPreview.SetPreviewMotion(CurrentSourceImportMotionSettings());
                _sourceImportPreview.Restart();
                _sourceImportPreview.FrameCamera();
            }
            else _sourceImportPreview.SelectInstance(null);

            string roleSummary = string.Join(" / ", _sourceImportAnalysis.Members
                .SelectMany(value => value.Roles ?? new List<string>()).Distinct(StringComparer.Ordinal).Take(4).Select(RoleLabel));
            _sourceImportMeta.text = "成员 " + _sourceImportAnalysis.Members.Count
                + " · 预估 " + _sourceImportAnalysis.EstimatedArtisticFrames + " 帧"
                + (_sourceImportAnalysis.Effect == null ? string.Empty : " · " + ThemeLabel(_sourceImportAnalysis.Effect.Theme).TrimEnd('·')
                    + " · " + CategoryLabel(_sourceImportAnalysis.Effect.Family)
                    + " · " + CategoryLabel(_sourceImportAnalysis.Effect.QualityTier))
                + (string.IsNullOrWhiteSpace(roleSummary) ? string.Empty : " · " + roleSummary);
            string displayName = _sourceImportAnalysis.Effect?.DisplayName ?? string.Empty;
            _sourceImportDisplayName.SetValueWithoutNotify(displayName);
            _sourceImportDisplayName.SetEnabled(_sourceImportAnalysis.Valid);
            Texture2D thumbnail = SourceImportThumbnail(_sourceImportAnalysis.Effect, out bool loading);
            _sourceImportSelectionIcon.style.backgroundImage = thumbnail == null ? StyleKeyword.None : new StyleBackground(thumbnail);
            if (loading) ScheduleSourceImportThumbnailRefresh();
            RefreshSourceImportValidation();
            RefreshSourceImportActionState();
            RefreshSourceImportFavoriteUi();
            RefreshSourceImportMemberSelection();
            ReloadSourceImportExistingElements();
        }

        private void ApplySourceImportMotionSuggestion(VfxSourcePrefabDraftImportAnalysis analysis)
        {
            bool trail = ShouldSuggestSourceImportMotion(analysis);
            _sourceImportMotionConfigured = false;
            _sourceImportMotionMode.SetValueWithoutNotify(trail ? "直线" : "静止");
            _sourceImportMotionDirection.SetValueWithoutNotify(0f);
            _sourceImportMotionDistance.SetValueWithoutNotify(8f);
            _sourceImportMotionSpeed.SetValueWithoutNotify(4f);
            _sourceImportMotionHeight.SetValueWithoutNotify(3f);
            _sourceImportMotionCamera.SetValueWithoutNotify("固定");
            RefreshSourceImportMotionControls();
        }

        internal static bool ShouldSuggestSourceImportMotion(VfxSourcePrefabDraftImportAnalysis analysis)
        {
            return analysis?.Effect?.Roles?.Any(value =>
                string.Equals(value, "trail", StringComparison.OrdinalIgnoreCase)) == true
                || analysis?.Members?.Any(member => member.Roles?.Any(value =>
                    string.Equals(value, "trail", StringComparison.OrdinalIgnoreCase)) == true) == true;
        }

        private void OnSourceImportMotionChanged()
        {
            if (_sourceImportBinding) return;
            _sourceImportMotionConfigured = true;
            RefreshSourceImportMotionControls();
            if (_sourceImportAnalysis?.Valid == true)
            {
                float normalized = Mathf.Clamp01(_sourceImportTimeline.value);
                ApplyToSourceImportPreviews(value =>
                {
                    value.SetPreviewMotion(CurrentSourceImportMotionSettings());
                    value.Scrub(normalized);
                    value.Pause();
                });
                RefreshSourceImportPreview();
            }
            ScheduleSourceImportWorkspaceStateSave();
        }

        private void RefreshSourceImportMotionControls()
        {
            bool moving = !string.Equals(_sourceImportMotionMode.value, "静止", StringComparison.Ordinal);
            _sourceImportMotionDirection.SetEnabled(moving);
            _sourceImportMotionDistance.SetEnabled(moving);
            _sourceImportMotionSpeed.SetEnabled(moving);
            _sourceImportMotionHeight.style.display = string.Equals(
                _sourceImportMotionMode.value,
                "抛物线",
                StringComparison.Ordinal)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private VfxPreviewMotionSettings CurrentSourceImportMotionSettings()
        {
            return new VfxPreviewMotionSettings(
                SourceImportMotionMode(_sourceImportMotionMode.value),
                _sourceImportMotionDirection.value,
                _sourceImportMotionDistance.value,
                _sourceImportMotionSpeed.value,
                _sourceImportMotionHeight.value,
                string.Equals(_sourceImportMotionCamera.value, "跟随", StringComparison.Ordinal));
        }

        private static VfxPreviewMotionMode SourceImportMotionMode(string value)
        {
            return value switch
            {
                "直线" => VfxPreviewMotionMode.Linear,
                "往返" => VfxPreviewMotionMode.PingPong,
                "环绕" => VfxPreviewMotionMode.Orbit,
                "抛物线" => VfxPreviewMotionMode.Parabola,
                _ => VfxPreviewMotionMode.Stationary
            };
        }

        private void RefreshSourceImportActionState()
        {
            bool awaitingMemberSelection = IsAwaitingSourceImportMemberSelection(
                _sourceImportAnalysis,
                _sourceImportSelectionAnalysis);
            bool displayNameValid = _sourceImportPlan?.Action != VfxSourcePrefabDraftImportAction.Add
                || IsValidSourceImportDisplayName(_sourceImportDisplayName.value);
            bool valid = _sourceImportSelectionAnalysis?.Valid == true && _sourceImportPlan?.CanApply == true && displayNameValid;
            bool blocked = !awaitingMemberSelection
                && (_sourceImportPlan?.Action == VfxSourcePrefabDraftImportAction.Blocked
                    || _sourceImportSelectionAnalysis?.Valid != true
                    || !displayNameValid);
            _sourceImportState.text = awaitingMemberSelection ? "请选择成员" : blocked ? "校验失败" : _sourceImportPlan?.Action switch
            {
                VfxSourcePrefabDraftImportAction.LocateExisting => "已存在",
                VfxSourcePrefabDraftImportAction.UpdateEvidence => "可更新",
                _ => "校验通过"
            };
            _sourceImportState.EnableInClassList("valid", valid && _sourceImportPlan.Action == VfxSourcePrefabDraftImportAction.Add);
            _sourceImportState.EnableInClassList("warning", valid && _sourceImportPlan.Action != VfxSourcePrefabDraftImportAction.Add);
            _sourceImportState.EnableInClassList("error", blocked);
            string diagnostics = awaitingMemberSelection
                ? "请从成员树中选择要派生为素材的 ParticleSystem。"
                : _sourceImportAnalysis?.Diagnostics.Count > 0
                ? string.Join("\n", _sourceImportAnalysis.Diagnostics)
                : _sourceImportSelectionAnalysis?.Diagnostics.Count > 0
                ? string.Join("\n", _sourceImportSelectionAnalysis.Diagnostics)
                : _sourceImportPlan?.Message ?? "请选择左侧 Prefab，或从 Project 拖入。";
            if (!awaitingMemberSelection && !displayNameValid) diagnostics = "卡片展示名必须为 1–32 个字符。";
            _sourceImportDiagnostics.text = diagnostics;
            _sourceImportDiagnostics.EnableInClassList("error", blocked);
            _sourceImportAdd.SetEnabled(valid && _sourceImportPlan.Action == VfxSourcePrefabDraftImportAction.Add);
            _sourceImportAddLayer.SetEnabled(valid && _session?.Draft != null);
            _sourceImportUpdateEvidence.SetEnabled(valid && _sourceImportPlan.Action == VfxSourcePrefabDraftImportAction.UpdateEvidence);
            _sourceImportAdd.text = _sourceImportPlan?.Action switch
            {
                VfxSourcePrefabDraftImportAction.LocateExisting => "已存在（右侧已选中）",
                VfxSourcePrefabDraftImportAction.UpdateEvidence => "已有素材需更新证据",
                _ => "创建全局素材  Enter"
            };
            _sourceImportRebuild.style.display = HasStaleIndexDiagnostic(_sourceImportAnalysis) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshSourceImportMemberSelection()
        {
            _sourceImportSelectionAnalysis = _sourcePrefabImporter.AnalyzeMemberSelection(
                _sourceImportAnalysis,
                _selectedSourceImportMemberIds);
            if (_sourceImportSelectionAnalysis.Members.Count > 0)
            {
                _selectedSourceImportMemberIds.Clear();
                _selectedSourceImportMemberIds.UnionWith(_sourceImportSelectionAnalysis.Members.Select(value => value.ComponentId));
            }
            _sourceImportMemberTree?.RefreshItems();
            int total = _sourceImportAnalysis?.Members.Count ?? 0;
            _sourceImportMembersCount.text = _selectedSourceImportMemberIds.Count + " / " + total;
            _sourceImportPlan = PlanForAuthoredElement(_sourceImportSelectionAnalysis);
            if (_sourceImportPlan.ExistingElement != null)
                _sourceImportDisplayName.SetValueWithoutNotify(_sourceImportPlan.ExistingElement.DisplayName);
            _sourceImportDisplayName.SetEnabled(_sourceImportPlan.Action == VfxSourcePrefabDraftImportAction.Add);

            RefreshSourceImportSoloState();
            RefreshSourceImportDerivedPreview();
            RefreshSourceImportValidation();
            RefreshSourceImportActionState();
            _sourceImportExistingList?.RefreshItems();
            RefreshSourceImportExistingSelection();
            ScheduleSourceImportWorkspaceStateSave();
        }

        private void ReloadSourceImportExistingElements(string preferredElementId = null)
        {
            _sourceImportExistingElements.Clear();
            string prefabGuid = _sourceImportAnalysis?.PrefabGuid ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(prefabGuid))
            {
                VfxAuthoredElementLibraryView library = _authoredElementLibrary.Load(out _);
                if (library != null)
                {
                    _sourceImportExistingElements.AddRange(library.Elements
                        .Where(value => IsSourceImportElementForPrefab(value, prefabGuid))
                        .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(value => value.ElementId, StringComparer.Ordinal));
                }
            }

            _sourceImportExistingBinding = true;
            try
            {
                _sourceImportExistingList.Rebuild();
            }
            finally
            {
                _sourceImportExistingBinding = false;
            }
            _sourceImportExistingCount.text = _sourceImportExistingElements.Count.ToString();
            _sourceImportExistingList.style.display = _sourceImportExistingElements.Count == 0
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _sourceImportExistingEmpty.style.display = _sourceImportExistingElements.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            RefreshSourceImportExistingSelection(preferredElementId);
        }

        private void RefreshSourceImportExistingSelection(string preferredElementId = null)
        {
            string elementId = !string.IsNullOrWhiteSpace(preferredElementId)
                ? preferredElementId
                : _sourceImportSelectionAnalysis?.ElementId;
            int index = string.IsNullOrWhiteSpace(elementId)
                ? -1
                : _sourceImportExistingElements.FindIndex(
                    value => string.Equals(value.ElementId, elementId, StringComparison.Ordinal));
            _sourceImportExistingBinding = true;
            try
            {
                if (index >= 0) _sourceImportExistingList.SetSelection(index);
                else _sourceImportExistingList.ClearSelection();
            }
            finally
            {
                _sourceImportExistingBinding = false;
            }
            if (index >= 0) _sourceImportExistingList.ScrollToItem(index);
            ScheduleSourceImportWorkspaceStateSave();
        }

        private void LoadSourceImportExistingElement(VfxSourceElementView element)
        {
            if (element?.MemberComponentIds == null || _sourceImportAnalysis?.Valid != true) return;
            _selectedSourceImportMemberIds.Clear();
            _selectedSourceImportMemberIds.UnionWith(element.MemberComponentIds);
            RefreshSourceImportMemberSelection();
            SetStatus("已在当前页面载入全局素材：" + element.DisplayName, false);
            ScheduleSourceImportWorkspaceStateSave();
        }

        internal static bool IsSourceImportElementForPrefab(VfxSourceElementView element, string prefabGuid)
        {
            return element?.SourcePrefabGuids?.Contains(prefabGuid, StringComparer.Ordinal) == true;
        }

        internal static bool MatchesSourceImportMemberSelection(
            VfxSourceElementView element,
            IEnumerable<string> selectedMemberIds)
        {
            return element?.MemberComponentIds != null
                && selectedMemberIds != null
                && element.MemberComponentIds.SequenceEqual(selectedMemberIds, StringComparer.Ordinal);
        }

        internal static bool ShouldReturnToManualAfterSourceImport(bool addLayer)
        {
            return addLayer;
        }

        private void RefreshSourceImportDerivedPreview()
        {
            RefreshSourceImportSoloState();
            VfxSourcePrefabMemberSelectionAnalysis previewSelection = SourceImportDerivedPreviewSelection();
            if (previewSelection?.Valid == true)
            {
                float normalizedFrame = _sourceImportPreview.EstimatedDurationSeconds <= 0f
                    ? 0f
                    : Mathf.Clamp01(_sourceImportPreview.ElapsedSeconds / _sourceImportPreview.EstimatedDurationSeconds);
                VfxSourceElementView temporary = _sourcePrefabImporter.BuildAuthoredElement(
                    previewSelection,
                    _sourceImportDisplayName.value);
                GameObject instance = VfxSourceElementInstantiation.Create(_catalog, temporary, "Derived Element Preview");
                _sourceImportDerivedPreview.SelectInstance(instance);
                float duration = Mathf.Max(
                    1f / PreviewFramesPerSecond,
                    _sourceImportPreview.EstimatedDurationSeconds);
                _sourceImportDerivedPreview.SetTimelineDuration(duration);
                _sourceImportDerivedPreview.SetLoop(_sourceImportLoop.value);
                _sourceImportDerivedPreview.SetSpeed(SourceImportSpeed(_sourceImportSpeed.value));
                _sourceImportDerivedPreview.SetBackground(_sourceImportBackground.value);
                _sourceImportDerivedPreview.SetPreviewMotion(CurrentSourceImportMotionSettings());
                _sourceImportDerivedPreview.Scrub(normalizedFrame);
                if (_sourceImportPreview.State == VfxLibraryPlaybackState.Playing) _sourceImportDerivedPreview.Play();
                else _sourceImportDerivedPreview.Pause();
                _sourceImportDerivedPreview.ApplyCameraState(_sourceImportPreview.CaptureCameraState());
                _sourceImportDerivedEmpty.style.display = DisplayStyle.None;
            }
            else
            {
                _sourceImportDerivedPreview.SelectInstance(null);
                _sourceImportDerivedEmpty.style.display = DisplayStyle.Flex;
            }
            RefreshSourceImportPreview();
        }

        private VfxSourcePrefabMemberSelectionAnalysis SourceImportDerivedPreviewSelection()
        {
            if (_sourceImportDerivedSolo?.value != true) return _sourceImportSelectionAnalysis;
            if (_currentSourceImportMemberNode == null) return null;
            string[] focusedMemberIds = _currentSourceImportMemberNode.MemberComponentIds
                .Where(_selectedSourceImportMemberIds.Contains)
                .ToArray();
            return _sourcePrefabImporter.AnalyzeMemberSelection(_sourceImportAnalysis, focusedMemberIds);
        }

        private void RefreshSourceImportSoloState()
        {
            if (_sourceImportDerivedSolo == null) return;
            bool canSolo = _sourceImportSelectionAnalysis?.Valid == true
                && _currentSourceImportMemberNode != null
                && _currentSourceImportMemberNode.MemberComponentIds.Any(_selectedSourceImportMemberIds.Contains);
            if (!canSolo && _sourceImportDerivedSolo.value)
                _sourceImportDerivedSolo.SetValueWithoutNotify(false);
            _sourceImportDerivedSolo.SetEnabled(canSolo);
            _sourceImportDerivedSolo.label = canSolo && _sourceImportDerivedSolo.value
                ? "Solo · " + _currentSourceImportMemberNode.DisplayName
                : "Solo 当前成员";
            _sourceImportDerivedSolo.EnableInClassList("active", canSolo && _sourceImportDerivedSolo.value);
        }

        private VfxSourcePrefabDraftImportPlan PlanForAuthoredElement(VfxSourcePrefabMemberSelectionAnalysis selection)
        {
            var plan = new VfxSourcePrefabDraftImportPlan { Action = VfxSourcePrefabDraftImportAction.Blocked };
            if (selection?.Valid != true)
            {
                plan.Message = selection == null ? "成员选择尚未分析。" : string.Join("\n", selection.Diagnostics);
                return plan;
            }
            VfxAuthoredElementLibraryView library = _authoredElementLibrary.Load(out string message);
            if (library == null)
            {
                plan.Message = message;
                return plan;
            }
            _authoredElementLibraryHash = library.LibraryHash;
            plan.ElementId = selection.ElementId;
            plan.ExistingElement = library.Elements.FirstOrDefault(value => string.Equals(value.ElementId, plan.ElementId, StringComparison.Ordinal));
            if (plan.ExistingElement == null)
            {
                plan.Action = VfxSourcePrefabDraftImportAction.Add;
                plan.Message = "将按当前成员组合创建一张全局素材卡片。";
                return plan;
            }
            IReadOnlyList<string> selectedIds = selection.Members.Select(value => value.ComponentId).ToList();
            if (!(plan.ExistingElement.MemberComponentIds ?? new List<string>()).SequenceEqual(selectedIds, StringComparer.Ordinal))
            {
                plan.MemberOrderStable = false;
                plan.Message = "成员身份、顺序或依赖组已变化，请重新完成成员选择。";
                return plan;
            }
            string previousDependencyGroups = plan.ExistingElement.AutoSemanticSuggestions?
                .FirstOrDefault(value => string.Equals(value.Field, "subEmitterDependencyGroups", StringComparison.Ordinal))?.Value;
            string currentDependencyGroups = VfxSourcePrefabDraftImportService.SelectedDependencyGroupSignature(selection);
            if (!string.Equals(previousDependencyGroups, currentDependencyGroups, StringComparison.Ordinal))
            {
                plan.MemberOrderStable = false;
                plan.Message = "SubEmitter 依赖组结构已变化，请重新完成成员选择，已阻止覆盖。";
                return plan;
            }
            bool evidenceMatches = string.Equals(plan.ExistingElement.SourcePrefabContentHash, selection.Source.Effect.ContentHash, StringComparison.Ordinal)
                && string.Equals(plan.ExistingElement.SourceDependencyHash, selection.Source.Effect.DependencyHash, StringComparison.Ordinal)
                && (plan.ExistingElement.SourceContentHashes ?? new List<string>()).SequenceEqual(
                    selection.Members.Select(value => value.SourceContentHash).Distinct(StringComparer.Ordinal), StringComparer.Ordinal);
            plan.Action = evidenceMatches
                ? VfxSourcePrefabDraftImportAction.LocateExisting
                : VfxSourcePrefabDraftImportAction.UpdateEvidence;
            plan.Message = evidenceMatches
                ? "相同成员组合已存在，已在右侧列表中选中。"
                : "成员与依赖组稳定，可更新来源证据并保留人工语义和展示名。";
            return plan;
        }

        private void RefreshSourceImportValidation()
        {
            SetValidationLabel(_sourceImportValidationIndex, "Source Index 身份匹配", _sourceImportAnalysis?.IndexIdentityMatched);
            bool? hash = _sourceImportAnalysis == null ? null : _sourceImportAnalysis.ContentHashMatched && _sourceImportAnalysis.DependencyHashMatched;
            SetValidationLabel(_sourceImportValidationHash, "Prefab 与依赖 Hash 匹配", hash);
            bool? members = _sourceImportAnalysis == null ? null : _sourceImportAnalysis.MemberIdentityMatched && (_sourceImportPlan?.MemberOrderStable ?? true);
            SetValidationLabel(_sourceImportValidationMembers, "成员身份与顺序稳定", members);
            bool awaitingMemberSelection = IsAwaitingSourceImportMemberSelection(
                _sourceImportAnalysis,
                _sourceImportSelectionAnalysis);
            bool? subEmitters = _sourceImportAnalysis == null || awaitingMemberSelection
                ? null
                : _sourceImportAnalysis.SubEmitterReferencesValid && _sourceImportSelectionAnalysis?.Valid == true;
            SetValidationLabel(_sourceImportValidationSubEmitters, "SubEmitter 依赖组完整", subEmitters);
        }

        internal static bool IsAwaitingSourceImportMemberSelection(
            VfxSourcePrefabDraftImportAnalysis analysis,
            VfxSourcePrefabMemberSelectionAnalysis selection)
        {
            return analysis?.Valid == true && selection != null && selection.Members.Count == 0;
        }

        private static void SetValidationLabel(Label label, string text, bool? value)
        {
            label.text = (value == true ? "✓ " : value == false ? "× " : "○ ") + text;
            label.EnableInClassList("passed", value == true);
            label.EnableInClassList("failed", value == false);
        }

        private static bool HasStaleIndexDiagnostic(VfxSourcePrefabDraftImportAnalysis analysis)
        {
            return analysis?.Diagnostics.Any(value => value.IndexOf("Index", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("索引", StringComparison.Ordinal) >= 0
                || value.IndexOf("重扫", StringComparison.Ordinal) >= 0) == true;
        }

        private void ResetSourceImportSelection()
        {
            _sourceImportAnalysis = null;
            _sourceImportSelectionAnalysis = null;
            _sourceImportPlan = null;
            _selectedSourceImportMemberIds.Clear();
            _sourceImportDerivedSolo?.SetValueWithoutNotify(false);
            _sourceImportPreview.SelectInstance(null);
            _sourceImportDerivedPreview.SelectInstance(null);
            _sourceImportName.text = "选择一个 Prefab";
            _sourceImportPath.text = "等待选择";
            _sourceImportState.text = "未分析";
            _sourceImportState.EnableInClassList("valid", false);
            _sourceImportState.EnableInClassList("warning", false);
            _sourceImportState.EnableInClassList("error", false);
            _sourceImportMeta.text = "成员 — · 预估 — 帧";
            _sourceImportDiagnostics.text = "请选择左侧 Prefab，或从 Project 拖入。";
            _sourceImportDiagnostics.EnableInClassList("error", false);
            BuildSourceImportMemberTree();
            RefreshSourceImportSoloState();
            _sourceImportMembersCount.text = "0 / 0";
            _sourceImportDisplayName.SetValueWithoutNotify(string.Empty);
            _sourceImportDisplayName.SetEnabled(false);
            _sourceImportSourceName.SetValueWithoutNotify(string.Empty);
            _sourceImportSelectionIcon.style.backgroundImage = StyleKeyword.None;
            _sourceImportFavorite.text = "☆";
            _sourceImportFavorite.SetEnabled(false);
            _sourceImportAdd.SetEnabled(false);
            _sourceImportAddLayer.SetEnabled(false);
            _sourceImportUpdateEvidence.SetEnabled(false);
            _sourceImportRebuild.style.display = DisplayStyle.None;
            _sourceImportTimeline.SetValueWithoutNotify(0f);
            _sourceImportFrameLabel.text = "Frame 0 / 0";
            if (_sourceImportDerivedEmpty != null) _sourceImportDerivedEmpty.style.display = DisplayStyle.Flex;
            ReloadSourceImportExistingElements();
            RefreshSourceImportValidation();
        }

        private void UseSelectedProjectPrefab()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrWhiteSpace(path))
            {
                AnalyzeSourceImportPath(string.Empty);
                return;
            }
            VfxSourceEffectView effect = FindIndexedSourceEffect(path);
            if (effect != null) SelectSourceImportEffect(effect, true);
            else AnalyzeSourceImportPath(path);
        }

        private VfxSourceEffectView FindIndexedSourceEffect(string path)
        {
            return _sourceImportItems.FirstOrDefault(value => string.Equals(value.PrefabPath, path, StringComparison.Ordinal));
        }

        private void RegisterSourceImportDropZone()
        {
            _sourceImportDrop.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                string path = SingleDraggedPrefabPath();
                DragAndDrop.visualMode = string.IsNullOrWhiteSpace(path) ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
                _sourceImportDrop.EnableInClassList("drag-active", !string.IsNullOrWhiteSpace(path));
                evt.StopPropagation();
            });
            _sourceImportDrop.RegisterCallback<DragLeaveEvent>(_ => _sourceImportDrop.EnableInClassList("drag-active", false));
            _sourceImportDrop.RegisterCallback<DragPerformEvent>(evt =>
            {
                _sourceImportDrop.EnableInClassList("drag-active", false);
                string path = SingleDraggedPrefabPath();
                if (string.IsNullOrWhiteSpace(path)) return;
                DragAndDrop.AcceptDrag();
                VfxSourceEffectView effect = FindIndexedSourceEffect(path);
                if (effect != null) SelectSourceImportEffect(effect, true);
                else AnalyzeSourceImportPath(path);
                evt.StopPropagation();
            });
        }

        private static string SingleDraggedPrefabPath()
        {
            if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length != 1) return string.Empty;
            string path = AssetDatabase.GetAssetPath(DragAndDrop.objectReferences[0]);
            return path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? path : string.Empty;
        }

        private void RebuildSourceIndexForImport()
        {
            string selectedPath = _sourceImportAnalysis?.PrefabPath ?? string.Empty;
            _sourceImportRebuild.SetEnabled(false);
            bool success = new VfxSourceLibraryEditorService().Scan(out string message);
            _sourceImportRebuild.SetEnabled(true);
            SetStatus(message, !success);
            if (!success)
            {
                _sourceImportDiagnostics.text = message;
                _sourceImportDiagnostics.EnableInClassList("error", true);
                return;
            }
            ReloadCatalog();
            PopulateSourceImportItems();
            RefreshSourceImportContext();
            VfxSourceEffectView effect = FindIndexedSourceEffect(selectedPath);
            if (effect != null) SelectSourceImportEffect(effect);
            else AnalyzeSourceImportPath(selectedPath);
        }

        private void ApplySourcePrefabImport(bool addLayer)
        {
            if (_sourceImportSelectionAnalysis?.Valid != true) return;
            _sourceImportPlan = PlanForAuthoredElement(_sourceImportSelectionAnalysis);
            if (!_sourceImportPlan.CanApply || !IsValidSourceImportDisplayNameForPlan())
            {
                RefreshSourceImportValidation();
                RefreshSourceImportActionState();
                return;
            }
            if (addLayer && _session?.Draft == null)
            {
                _sourceImportDiagnostics.text = "没有当前草稿；可以创建全局素材，但不能加入阶段。";
                _sourceImportDiagnostics.EnableInClassList("error", true);
                return;
            }

            VfxSourceElementView proposed = _sourcePrefabImporter.BuildAuthoredElement(
                _sourceImportSelectionAnalysis,
                _sourceImportDisplayName.value);
            VfxSourceElementView element = _sourceImportPlan.ExistingElement;
            string message = _sourceImportPlan.Message;
            if (_sourceImportPlan.Action != VfxSourcePrefabDraftImportAction.LocateExisting
                && !_authoredElementLibrary.SaveElement(
                    proposed,
                    _authoredElementLibraryHash,
                    _sourceImportPlan.Action == VfxSourcePrefabDraftImportAction.UpdateEvidence,
                    out element,
                    out _,
                    out message))
            {
                RefreshSourceImportActionState();
                _sourceImportDiagnostics.text = message;
                _sourceImportDiagnostics.EnableInClassList("error", true);
                return;
            }

            ReloadCatalog();
            BindDraftLocalElements();
            element = _catalog.Find(_sourceImportSelectionAnalysis.ElementId) ?? element;
            VfxCompositionLayerView layer = null;
            if (addLayer)
            {
                _session.RecordChange();
                string phase = ResolveAddPhase(_previewStage.value, element, _session.Draft.Layers.Count == 0);
                string role = element.Roles.FirstOrDefault() ?? "detail";
                string layerId = UniqueLayerId("layer_" + (_session.Draft.Layers.Count + 1).ToString("00") + "_" + role);
                layer = VfxCompositionDraftService.CreateLayerView(_catalog, element, layerId, phase);
                VfxSourcePrefabDraftImportService.ApplySourceTiming(layer, _sourceImportSelectionAnalysis.SourceSegments);
                _session.Draft.Layers.Add(layer);
                _session.SelectedLayerIndex = _session.Draft.Layers.Count - 1;
                message += " 已加入当前阶段。";
            }
            RecordRecentSourcePrefab(_sourceImportAnalysis.PrefabGuid);
            _sourceFilter.SetValueWithoutNotify("全部素材");
            RefreshElements();
            _elementList.RefreshItems();
            SetStatus(message + " 来源 Prefab 保持只读。", false);
            if (ShouldReturnToManualAfterSourceImport(addLayer))
            {
                SelectElement(element);
                RefreshDraftUi();
                ExitSourcePrefabImportWorkspace();
                _showManualWorkspace();
                return;
            }

            _sourceImportPlan = PlanForAuthoredElement(_sourceImportSelectionAnalysis);
            if (_sourceImportPlan.ExistingElement != null)
                _sourceImportDisplayName.SetValueWithoutNotify(_sourceImportPlan.ExistingElement.DisplayName);
            ReloadSourceImportExistingElements(element?.ElementId);
            RefreshSourceImportValidation();
            RefreshSourceImportActionState();
        }

        private void ToggleSelectedSourceImportFavorite()
        {
            if (_sourceImportAnalysis?.Effect != null) ToggleSourceImportFavorite(_sourceImportAnalysis.Effect);
        }

        private void ToggleSourceImportFavorite(VfxSourceEffectView effect)
        {
            string elementId = VfxSourcePrefabDraftImportService.ElementIdForPrefabGuid(effect.PrefabGuid);
            if (!_favoriteElementIds.Add(elementId)) _favoriteElementIds.Remove(elementId);
            EditorPrefs.SetString("MxFramework.Vfx.Manual.Favorites", string.Join("|", _favoriteElementIds.OrderBy(value => value, StringComparer.Ordinal)));
            RefreshSourceImportFavoriteUi();
            FilterSourceImportItems();
            _elementList.RefreshItems();
        }

        private bool IsSourceImportFavorite(VfxSourceEffectView effect)
        {
            return effect != null && _favoriteElementIds.Contains(VfxSourcePrefabDraftImportService.ElementIdForPrefabGuid(effect.PrefabGuid));
        }

        private void RefreshSourceImportFavoriteUi()
        {
            bool available = _sourceImportAnalysis?.Effect != null;
            bool favorite = available && IsSourceImportFavorite(_sourceImportAnalysis.Effect);
            _sourceImportFavorite.SetEnabled(available);
            _sourceImportFavorite.text = favorite ? "★" : "☆";
            _sourceImportFavorite.EnableInClassList("favorite", favorite);
            _sourceImportList.RefreshItems();
        }

        private void RecordRecentSourcePrefab(string prefabGuid)
        {
            if (string.IsNullOrWhiteSpace(prefabGuid)) return;
            _recentSourcePrefabGuids.Remove(prefabGuid);
            _recentSourcePrefabGuids.Insert(0, prefabGuid);
            if (_recentSourcePrefabGuids.Count > MaxRecentSourcePrefabs)
                _recentSourcePrefabGuids.RemoveRange(MaxRecentSourcePrefabs, _recentSourcePrefabGuids.Count - MaxRecentSourcePrefabs);
            EditorPrefs.SetString(RecentSourcePrefabPreference, string.Join("|", _recentSourcePrefabGuids));
        }

        private bool RestoreSourceImportWorkspaceState()
        {
            SourceImportWorkspaceState state = _sourceImportSavedState;
            if (state == null) return false;

            _sourceImportRestoring = true;
            try
            {
                _sourceImportSource = IsValidSourceImportSource(state.source) ? state.source : "all";
                _sourceImportSourceAll.EnableInClassList("selected", _sourceImportSource == "all");
                _sourceImportSourceFavorite.EnableInClassList("selected", _sourceImportSource == "favorite");
                _sourceImportSourceRecent.EnableInClassList("selected", _sourceImportSource == "recent");
                _sourceImportSearch.SetValueWithoutNotify(state.search ?? string.Empty);
                RestoreSourceImportChoice(_sourceImportTheme, state.theme);
                RestoreSourceImportChoice(_sourceImportFamily, state.family);
                RestoreSourceImportChoice(_sourceImportRole, state.role);
                RestoreSourceImportChoice(_sourceImportQuality, state.quality);
                _selectedSourceImportFolder = _sourceImportFolders.ContainsKey(state.selectedFolder ?? string.Empty)
                    ? state.selectedFolder ?? string.Empty
                    : string.Empty;
                _expandedSourceImportFolders.Clear();
                foreach (string folderPath in state.expandedFolders ?? new List<string>())
                    if (_sourceImportFolders.ContainsKey(folderPath)) _expandedSourceImportFolders.Add(folderPath);
                _sourceImportFolderPanel.EnableInClassList("collapsed", state.folderCollapsed);
                _sourceImportFolderCollapse.text = state.folderCollapsed ? "展开" : "收起";
                RebuildSourceImportFolderTree();
                FilterSourceImportItems();

                string path = !string.IsNullOrWhiteSpace(state.prefabGuid)
                    ? AssetDatabase.GUIDToAssetPath(state.prefabGuid)
                    : state.prefabPath;
                VfxSourceEffectView effect = FindIndexedSourceEffect(path);
                if (effect == null)
                {
                    if (!string.IsNullOrWhiteSpace(state.prefabGuid) || !string.IsNullOrWhiteSpace(state.prefabPath))
                    {
                        ResetSourceImportSelection();
                        _sourceImportDiagnostics.text = "上次选择的 Prefab 已不存在或已离开 Source Index，浏览条件已恢复。";
                        _sourceImportDiagnostics.EnableInClassList("error", true);
                    }
                    return !string.IsNullOrWhiteSpace(state.prefabGuid) || !string.IsNullOrWhiteSpace(state.prefabPath);
                }

                SelectSourceImportEffect(effect);
                if (!TryRestoreSourceImportMembers(state))
                {
                    _selectedSourceImportMemberIds.Clear();
                    RefreshSourceImportMemberSelection();
                    _sourceImportDiagnostics.text = "上次成员身份、顺序或 SubEmitter 依赖组已变化，请重新选择成员。";
                    _sourceImportDiagnostics.EnableInClassList("error", true);
                    ApplySourceImportPreviewState(state);
                    return true;
                }

                RestoreSourceImportMemberTreeState(state);
                if (_sourceImportPlan?.Action == VfxSourcePrefabDraftImportAction.Add
                    && IsValidSourceImportDisplayName(state.displayName))
                    _sourceImportDisplayName.SetValueWithoutNotify(state.displayName.Trim());
                RefreshSourceImportActionState();
                if (!string.IsNullOrWhiteSpace(state.existingElementId))
                    RefreshSourceImportExistingSelection(state.existingElementId);
                bool canRestoreSolo = state.solo
                    && _currentSourceImportMemberNode != null
                    && _currentSourceImportMemberNode.MemberComponentIds.Any(_selectedSourceImportMemberIds.Contains);
                _sourceImportDerivedSolo.SetValueWithoutNotify(canRestoreSolo);
                RefreshSourceImportSoloState();
                RefreshSourceImportDerivedPreview();
                ApplySourceImportPreviewState(state);
                return true;
            }
            finally
            {
                _sourceImportRestoring = false;
                _sourceImportStateSavePending = false;
            }
        }

        private bool TryRestoreSourceImportMembers(SourceImportWorkspaceState state)
        {
            List<string> saved = state.selectedMemberIds ?? new List<string>();
            if (saved.Count == 0) return true;
            var available = new HashSet<string>(
                _sourceImportAnalysis?.Members.Select(value => value.ComponentId) ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);
            if (saved.Any(value => !available.Contains(value))) return false;
            VfxSourcePrefabMemberSelectionAnalysis selection =
                _sourcePrefabImporter.AnalyzeMemberSelection(_sourceImportAnalysis, saved);
            if (!MatchesSourceImportRestoredMemberState(saved, state.dependencyGroupSignature, selection)) return false;
            _selectedSourceImportMemberIds.Clear();
            _selectedSourceImportMemberIds.UnionWith(saved);
            RefreshSourceImportMemberSelection();
            return true;
        }

        internal static bool MatchesSourceImportRestoredMemberState(
            IReadOnlyList<string> savedMemberIds,
            string savedDependencyGroupSignature,
            VfxSourcePrefabMemberSelectionAnalysis selection)
        {
            return selection?.Valid == true
                && savedMemberIds != null
                && savedMemberIds.SequenceEqual(
                    selection.Members.Select(value => value.ComponentId),
                    StringComparer.Ordinal)
                && string.Equals(
                    savedDependencyGroupSignature ?? string.Empty,
                    VfxSourcePrefabDraftImportService.SelectedDependencyGroupSignature(selection),
                    StringComparison.Ordinal);
        }

        private void RestoreSourceImportMemberTreeState(SourceImportWorkspaceState state)
        {
            foreach (string path in state.expandedMemberPaths ?? new List<string>())
                if (_sourceImportMemberNodes.ContainsKey(path))
                    _sourceImportMemberTree.ExpandItem(SourceImportStableTreeId(path));
            if (string.IsNullOrWhiteSpace(state.currentMemberPath)
                || !_sourceImportMemberNodes.TryGetValue(state.currentMemberPath, out SourceImportMemberNode current))
                return;
            _currentSourceImportMemberNode = current;
            _sourceImportMemberTreeBinding = true;
            try { _sourceImportMemberTree.SetSelectionByIdWithoutNotify(new[] { SourceImportStableTreeId(current.FullPath) }); }
            finally { _sourceImportMemberTreeBinding = false; }
        }

        private void ApplySourceImportPreviewState(SourceImportWorkspaceState state)
        {
            string background = _sourceImportBackground.choices.Contains(state.background) ? state.background : "中性灰";
            string speed = _sourceImportSpeed.choices.Contains(state.speed) ? state.speed : "1.0×";
            _sourceImportBackground.SetValueWithoutNotify(background);
            _sourceImportSpeed.SetValueWithoutNotify(speed);
            _sourceImportLoop.SetValueWithoutNotify(state.loop);
            if (state.motionConfigured)
            {
                _sourceImportMotionMode.SetValueWithoutNotify(
                    _sourceImportMotionMode.choices.Contains(state.motionMode) ? state.motionMode : "静止");
                _sourceImportMotionDirection.SetValueWithoutNotify(Mathf.Clamp(state.motionDirection, 0f, 360f));
                _sourceImportMotionDistance.SetValueWithoutNotify(Mathf.Clamp(state.motionDistance, 0.5f, 30f));
                _sourceImportMotionSpeed.SetValueWithoutNotify(Mathf.Clamp(state.motionSpeed, 0.1f, 20f));
                _sourceImportMotionHeight.SetValueWithoutNotify(Mathf.Clamp(state.motionHeight, 0f, 15f));
                _sourceImportMotionCamera.SetValueWithoutNotify(
                    string.Equals(state.motionCamera, "跟随", StringComparison.Ordinal) ? "跟随" : "固定");
                _sourceImportMotionConfigured = true;
            }
            RefreshSourceImportMotionControls();
            VfxPreviewMotionSettings motion = CurrentSourceImportMotionSettings();
            ApplyToSourceImportPreviews(value =>
            {
                value.SetBackground(background);
                value.SetSpeed(SourceImportSpeed(speed));
                value.SetLoop(state.loop);
                value.SetPreviewMotion(motion);
                value.Scrub(Mathf.Clamp01(state.normalizedTime));
                value.ApplyCameraState(new VfxPreviewCameraState(
                    new Vector2(state.orbitX, state.orbitY),
                    Mathf.Max(0.01f, state.cameraDistance),
                    new Vector3(state.targetX, state.targetY, state.targetZ)));
                value.Pause();
            });
            RefreshSourceImportPreview();
        }

        private static void RestoreSourceImportChoice(DropdownField field, string value)
        {
            field.SetValueWithoutNotify(field.choices.Contains(value) ? value : "全部");
        }

        private static bool IsValidSourceImportSource(string source)
        {
            return source == "all" || source == "favorite" || source == "recent";
        }

        private void ScheduleSourceImportWorkspaceStateSave()
        {
            if (_sourceImportRestoring || !_sourceImportHasEntered) return;
            _sourceImportStateSavePending = true;
            _sourceImportStateSaveDueTime = EditorApplication.timeSinceStartup + SourceImportWorkspaceSaveDelaySeconds;
        }

        internal void SaveSourceImportWorkspaceStateNow()
        {
            if (!_sourceImportHasEntered || _sourceImportWorkspace == null) return;
            SourceImportWorkspaceState state = CaptureSourceImportWorkspaceState();
            EditorPrefs.SetString(SourceImportWorkspaceStatePreferenceKey(Application.dataPath), JsonUtility.ToJson(state));
            _sourceImportSavedState = state;
            _sourceImportStateSavePending = false;
        }

        private SourceImportWorkspaceState CaptureSourceImportWorkspaceState()
        {
            VfxPreviewCameraState camera = _sourceImportPreview.CaptureCameraState();
            var state = new SourceImportWorkspaceState
            {
                version = SourceImportWorkspaceStateVersion,
                source = _sourceImportSource,
                search = _sourceImportSearch.value ?? string.Empty,
                theme = _sourceImportTheme.value,
                family = _sourceImportFamily.value,
                role = _sourceImportRole.value,
                quality = _sourceImportQuality.value,
                selectedFolder = _selectedSourceImportFolder,
                expandedFolders = _expandedSourceImportFolders.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                folderCollapsed = _sourceImportFolderPanel.ClassListContains("collapsed"),
                prefabGuid = _sourceImportAnalysis?.PrefabGuid ?? string.Empty,
                prefabPath = _sourceImportAnalysis?.PrefabPath ?? string.Empty,
                selectedMemberIds = _sourceImportSelectionAnalysis?.Members.Select(value => value.ComponentId).ToList()
                    ?? new List<string>(),
                dependencyGroupSignature = VfxSourcePrefabDraftImportService.SelectedDependencyGroupSignature(_sourceImportSelectionAnalysis),
                currentMemberPath = _currentSourceImportMemberNode?.FullPath ?? string.Empty,
                expandedMemberPaths = _sourceImportMemberNodes.Keys
                    .Where(path => _sourceImportMemberTree.IsExpanded(SourceImportStableTreeId(path)))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList(),
                displayName = _sourceImportDisplayName.value ?? string.Empty,
                existingElementId = (_sourceImportExistingList?.selectedItem as VfxSourceElementView)?.ElementId ?? string.Empty,
                solo = _sourceImportDerivedSolo?.value == true,
                background = _sourceImportBackground.value,
                speed = _sourceImportSpeed.value,
                loop = _sourceImportLoop.value,
                normalizedTime = Mathf.Clamp01(_sourceImportTimeline.value),
                orbitX = camera.Orbit.x,
                orbitY = camera.Orbit.y,
                cameraDistance = camera.Distance,
                targetX = camera.Target.x,
                targetY = camera.Target.y,
                targetZ = camera.Target.z,
                motionConfigured = _sourceImportMotionConfigured,
                motionMode = _sourceImportMotionMode.value,
                motionDirection = _sourceImportMotionDirection.value,
                motionDistance = _sourceImportMotionDistance.value,
                motionSpeed = _sourceImportMotionSpeed.value,
                motionHeight = _sourceImportMotionHeight.value,
                motionCamera = _sourceImportMotionCamera.value
            };
            return state;
        }

        private SourceImportWorkspaceState LoadSourceImportWorkspaceState()
        {
            string json = EditorPrefs.GetString(SourceImportWorkspaceStatePreferenceKey(Application.dataPath), string.Empty);
            return DeserializeSourceImportWorkspaceState(json);
        }

        internal static SourceImportWorkspaceState DeserializeSourceImportWorkspaceState(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                SourceImportWorkspaceState state = JsonUtility.FromJson<SourceImportWorkspaceState>(json);
                if (state == null || (state.version != 1 && state.version != SourceImportWorkspaceStateVersion)) return null;
                if (state.version == 1)
                {
                    state.version = SourceImportWorkspaceStateVersion;
                    state.motionConfigured = false;
                    state.motionMode = "静止";
                    state.motionDirection = 0f;
                    state.motionDistance = 8f;
                    state.motionSpeed = 4f;
                    state.motionHeight = 3f;
                    state.motionCamera = "固定";
                }
                state.selectedMemberIds ??= new List<string>();
                state.expandedFolders ??= new List<string>();
                state.expandedMemberPaths ??= new List<string>();
                return state;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static string SourceImportWorkspaceStatePreferenceKey(string dataPath)
        {
            string normalized = (dataPath ?? string.Empty).Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
            return "MxFramework.Vfx.SourceImport.WorkspaceState." + Hash128.Compute(normalized);
        }

        private static List<string> LoadRecentSourcePrefabGuids()
        {
            return EditorPrefs.GetString(RecentSourcePrefabPreference, string.Empty)
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .Take(MaxRecentSourcePrefabs)
                .ToList();
        }

        private Texture2D SourceImportThumbnail(VfxSourceEffectView effect, out bool loading)
        {
            loading = false;
            if (effect == null) return null;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(effect.PrefabPath);
            if (prefab == null) return null;
            Texture2D preview = AssetPreview.GetAssetPreview(prefab);
            loading = preview == null && AssetPreview.IsLoadingAssetPreviews();
            return preview != null ? preview : AssetPreview.GetMiniThumbnail(prefab);
        }

        private void ScheduleSourceImportThumbnailRefresh()
        {
            if (_sourceImportThumbnailRefreshScheduled) return;
            _sourceImportThumbnailRefreshScheduled = true;
            _sourceImportWorkspace.schedule.Execute(() =>
            {
                _sourceImportThumbnailRefreshScheduled = false;
                _sourceImportList.RefreshItems();
                if (_sourceImportAnalysis?.Effect != null)
                {
                    Texture2D thumbnail = SourceImportThumbnail(_sourceImportAnalysis.Effect, out bool loading);
                    _sourceImportSelectionIcon.style.backgroundImage = thumbnail == null ? StyleKeyword.None : new StyleBackground(thumbnail);
                    if (loading) ScheduleSourceImportThumbnailRefresh();
                }
            }).ExecuteLater(120L);
        }

        private void RefreshSourceImportContext()
        {
            _sourceImportDraft.text = _session?.Draft == null ? "尚未选择草稿" : DraftDisplayName(_session.Draft);
            bool dirty = _session?.Dirty == true;
            _sourceImportDirty.text = dirty ? "● 未保存修改" : "已保存";
            _sourceImportDirty.EnableInClassList("dirty", dirty);
            int count = _catalog?.Index?.Effects?.Count ?? 0;
            _sourceImportIndexStatus.text = count > 0 ? "Source Index：" + count + " 个 Prefab · 已加载" : "Source Index：未加载";
        }

        private void UpdateSourcePrefabImportWorkspace()
        {
            if (_sourceImportStateSavePending
                && EditorApplication.timeSinceStartup >= _sourceImportStateSaveDueTime)
                SaveSourceImportWorkspaceStateNow();
            if (_sourceImportWorkspace?.resolvedStyle.display == DisplayStyle.None) return;
            RefreshSourceImportContext();
            if (_sourceImportPreview.State == VfxLibraryPlaybackState.Playing)
            {
                _sourceImportPreview.UpdatePlayback();
                _sourceImportDerivedPreview.SynchronizePlaybackFrom(_sourceImportPreview);
                _sourceImportPreviewCanvas.MarkDirtyRepaint();
                _sourceImportDerivedPreviewCanvas.MarkDirtyRepaint();
            }
            RefreshSourceImportFrameUi();
        }

        private void RefreshSourceImportPreview()
        {
            _sourceImportPreviewCanvas.MarkDirtyRepaint();
            _sourceImportDerivedPreviewCanvas.MarkDirtyRepaint();
            RefreshSourceImportFrameUi();
        }

        private void ApplyToSourceImportPreviews(Action<VfxLibraryPreviewSession> action)
        {
            action?.Invoke(_sourceImportPreview);
            action?.Invoke(_sourceImportDerivedPreview);
        }

        private static void SynchronizeSourceImportCamera(VfxLibraryPreviewSession source, VfxLibraryPreviewSession target)
        {
            if (source == null || target == null) return;
            target.ApplyCameraState(source.CaptureCameraState());
        }

        private void DrawSynchronizedSourceImportPreview(
            VfxLibraryPreviewSession session,
            VfxLibraryPreviewSession peer,
            IMGUIContainer canvas)
        {
            VfxPreviewCameraState before = session.CaptureCameraState();
            session.DrawSynchronized(canvas.contentRect);
            VfxPreviewCameraState after = session.CaptureCameraState();
            if (SourceImportCameraStateApproximatelyEquals(before, after)) return;

            peer.ApplyCameraState(after);
            _sourceImportPreviewCanvas?.MarkDirtyRepaint();
            _sourceImportDerivedPreviewCanvas?.MarkDirtyRepaint();
            ScheduleSourceImportWorkspaceStateSave();
        }

        private static bool SourceImportCameraStateApproximatelyEquals(
            VfxPreviewCameraState left,
            VfxPreviewCameraState right)
        {
            const float epsilon = 0.000001f;
            return (left.Orbit - right.Orbit).sqrMagnitude <= epsilon
                && Mathf.Abs(left.Distance - right.Distance) <= epsilon
                && (left.Target - right.Target).sqrMagnitude <= epsilon;
        }

        private void RefreshSourceImportFrameUi()
        {
            int totalFrames = _sourceImportSelectionAnalysis?.Valid == true
                ? Math.Max(_sourceImportAnalysis?.EstimatedArtisticFrames ?? 0, _sourceImportSelectionAnalysis.EstimatedArtisticFrames)
                : _sourceImportAnalysis?.EstimatedArtisticFrames ?? 0;
            int frame = Mathf.Clamp(Mathf.RoundToInt(_sourceImportPreview.ElapsedSeconds * PreviewFramesPerSecond), 0, totalFrames);
            _sourceImportFrameLabel.text = "Frame " + frame + " / " + totalFrames;
            _sourceImportBinding = true;
            try
            {
                float duration = Mathf.Max(0.0001f, _sourceImportPreview.EstimatedDurationSeconds);
                _sourceImportTimeline.SetValueWithoutNotify(totalFrames == 0 ? 0f : Mathf.Clamp01(_sourceImportPreview.ElapsedSeconds / duration));
            }
            finally { _sourceImportBinding = false; }
        }

        private void OnSourceImportKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                ReturnToManualWorkspace();
                evt.StopPropagation();
                return;
            }
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
            if (IsSourceImportTextEditing()) return;
            if (_sourceImportPlan?.Action == VfxSourcePrefabDraftImportAction.Add
                && _sourceImportAdd.enabledSelf)
                ApplySourcePrefabImport(false);
            evt.StopPropagation();
        }

        private bool IsSourceImportTextEditing()
        {
            VisualElement focused = _sourceImportWorkspace.focusController?.focusedElement as VisualElement;
            return focused != null && (focused is TextField
                || focused is ToolbarSearchField
                || focused.GetFirstAncestorOfType<TextField>() != null
                || focused.GetFirstAncestorOfType<ToolbarSearchField>() != null);
        }

        private bool IsValidSourceImportDisplayNameForPlan()
        {
            return _sourceImportPlan?.Action != VfxSourcePrefabDraftImportAction.Add
                || IsValidSourceImportDisplayName(_sourceImportDisplayName.value);
        }

        private static bool IsValidSourceImportDisplayName(string value)
        {
            int length = (value ?? string.Empty).Trim().Length;
            return length >= 1 && length <= 32;
        }

        private static float SourceImportSpeed(string value)
        {
            return value switch
            {
                "0.25×" => 0.25f,
                "0.5×" => 0.5f,
                "2.0×" => 2f,
                _ => 1f
            };
        }
    }

    [Serializable]
    internal sealed class SourceImportWorkspaceState
    {
        public int version = 2;
        public string source = "all";
        public string search = string.Empty;
        public string theme = "全部";
        public string family = "全部";
        public string role = "全部";
        public string quality = "全部";
        public string selectedFolder = string.Empty;
        public List<string> expandedFolders = new List<string>();
        public bool folderCollapsed;
        public string prefabGuid = string.Empty;
        public string prefabPath = string.Empty;
        public List<string> selectedMemberIds = new List<string>();
        public string dependencyGroupSignature = string.Empty;
        public string currentMemberPath = string.Empty;
        public List<string> expandedMemberPaths = new List<string>();
        public string displayName = string.Empty;
        public string existingElementId = string.Empty;
        public bool solo;
        public string background = "中性灰";
        public string speed = "1.0×";
        public bool loop = true;
        public float normalizedTime;
        public float orbitX = 28f;
        public float orbitY = -34f;
        public float cameraDistance = 8f;
        public float targetX;
        public float targetY;
        public float targetZ;
        public bool motionConfigured;
        public string motionMode = "静止";
        public float motionDirection;
        public float motionDistance = 8f;
        public float motionSpeed = 4f;
        public float motionHeight = 3f;
        public string motionCamera = "固定";
    }

    internal sealed class SourceImportFolderNode
    {
        internal SourceImportFolderNode(string path, string displayName)
        {
            Path = path ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        internal string Path { get; }
        internal string DisplayName { get; }
        internal int DescendantPrefabCount { get; set; }
        internal int TreeId { get; set; }
        internal List<SourceImportFolderNode> Children { get; } = new List<SourceImportFolderNode>();
    }

    internal sealed class SourceImportMemberNode
    {
        internal SourceImportMemberNode(string displayName, string fullPath)
        {
            DisplayName = displayName ?? string.Empty;
            FullPath = fullPath ?? string.Empty;
        }

        internal string DisplayName { get; set; }
        internal string FullPath { get; }
        internal VfxSourceComponentView Member { get; set; }
        internal List<SourceImportMemberNode> Children { get; } = new List<SourceImportMemberNode>();
        internal List<string> MemberComponentIds { get; } = new List<string>();
    }
}
