using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.ProjectileAuthoring
{
    public sealed partial class ProjectileAuthoringWindow : EditorWindow
    {
        public const string MenuPath = "MxFramework/Combat/Projectile Authoring";
        public const string WindowTitle = "投射物创作";

        private IProjectileAuthoringService service;
        private readonly ProjectileAuthoringDocumentSession session = new ProjectileAuthoringDocumentSession();
        private ProjectileAuthoringWorkspaceView workspace;
        private ProjectileAuthoringWorkspaceView diskWorkspace;
        private ProjectileAuthoringWorkspaceView pendingExternalWorkspace;
        private ProjectileAuthoringMutationRequestView pendingMutation;
        private ProjectileAuthoringMutationResultView pendingPlan;
        private ProjectileAuthoringMutationRequestView[] pendingSequence = Array.Empty<ProjectileAuthoringMutationRequestView>();
        private ProjectileAuthoringMutationRequestView[] externalReplayCommands = Array.Empty<ProjectileAuthoringMutationRequestView>();
        private ProjectileAuthoringMutationRequestView[] externalReplayPrefix = Array.Empty<ProjectileAuthoringMutationRequestView>();
        private int externalReplayIndex = -1;
        private ProjectileAuthoringProblemView[] validationProblems = Array.Empty<ProjectileAuthoringProblemView>();
        private ProjectileAuthoringProblemView[] transientProblems = Array.Empty<ProjectileAuthoringProblemView>();
        private readonly List<NavigationRow> rows = new List<NavigationRow>();
        private ListView list;
        private ToolbarSearchField search;
        private Label status;
        private Label dirty;
        private Label inspectorTitle;
        private Label inspectorBody;
        private VisualElement ruleInspectorRoot;
        private VisualElement presentationInspectorRoot;
        private VisualElement diagnosticsInspectorRoot;
        private ScrollView configInspectorScroll;
        private ScrollView presentationInspectorScroll;
        private ScrollView diagnosticsInspectorScroll;
        private Button configTabButton;
        private Button presentationTabButton;
        private Button diagnosticsTabButton;
        private Label lifecycleTitle;
        private VisualElement lifecycleCardsRoot;
        private Foldout problemsDrawer;
        private ScrollView problems;
        private Button saveButton;
        private Button undoButton;
        private Button redoButton;
        private TextField identityField;
        private VisualElement impactPanel;
        private Label impactSummary;
        private Label impactDetails;
        private Button impactConfirm;
        private VisualElement conflictActions;
        private PopupField<string> motionFilter;
        private PopupField<string> impactFilter;
        private PopupField<string> statusFilter;
        private PopupField<string> severityFilter;
        private bool usedOnly;
        private bool sharedOnly;
        private bool dirtyOnly;
        private bool invalidOnly;
        private bool lifecycleRegistered;
        private bool disposed;
        private bool restoredSession;
        private bool discardRequested;
        private string selectedInspectorTab = "config";
        private string selectedPresentationSlotKind = "flightBody";
        private string lastProblemFocusTarget = string.Empty;
        private string packagePath;
        private string packageSourceLabel;
        private TwoPaneSplitView outerSplit;
        private TwoPaneSplitView inspectorSplit;
        private Action refreshAssets = AssetDatabase.Refresh;
        private static string pendingDeepLinkStableId = string.Empty;

        [MenuItem(MenuPath)]
        public static ProjectileAuthoringWindow Open()
        {
            ProjectileAuthoringWindow window = GetWindow<ProjectileAuthoringWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(1280, 720);
            window.Show();
            return window;
        }

        /// <summary>Opens the authoritative Projectile Authoring workspace and selects a Request or Definition stable id.</summary>
        /// <remarks>LowFreqAlloc: Editor navigation may rebuild filtered rows and diagnostics.</remarks>
        public static ProjectileAuthoringWindow OpenByStableId(string stableId)
        {
            pendingDeepLinkStableId = (stableId ?? string.Empty).Trim();
            ProjectileAuthoringWindow window = Open();
            window.TryApplyPendingDeepLink();
            return window;
        }

        public void CreateGUI()
        {
            disposed = false;
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(1280, 720);
            service ??= new ProjectileAuthoringCliService();
            bool hasPackagePath = MxFrameworkEditorProjectPaths.TryResolve(
                MxFrameworkEditorProjectPathKind.ProjectileAuthoringPackage,
                out packageSourceLabel);
            packagePath = hasPackagePath
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", packageSourceLabel))
                : string.Empty;
            session.PackagePath = packagePath;
            RestoreSessionState();
            BuildVisualTree();
            RegisterLifecycle();
            LoadWorkspace(false);
        }

        public void SetServiceForTests(IProjectileAuthoringService value) { service?.Dispose(); service = value; }
        public void SetAssetRefreshForTests(Action value) { refreshAssets = value ?? throw new ArgumentNullException(nameof(value)); }
        public ProjectileAuthoringDocumentSession SessionForTests => session;
        public IReadOnlyList<string> VisibleStableIdsForTests => rows.Select(row => row.StableId).ToArray();
        public IReadOnlyList<string> VisibleBadgesForTests => rows.Select(row => row.Badges).ToArray();
        public int CurrentDefinitionCountForTests => workspace?.definitionCount ?? 0;
        public int DiskDefinitionCountForTests => diskWorkspace?.definitionCount ?? 0;
        public bool LifecycleRegisteredForTests => lifecycleRegistered;
        public string StatusForTests => status?.text ?? string.Empty;
        public string LastProblemFocusTargetForTests => lastProblemFocusTarget;
        public IReadOnlyList<string> ValidationProblemCodesForTests => (validationProblems ?? Array.Empty<ProjectileAuthoringProblemView>()).Select(value => value.code).ToArray();
        public IReadOnlyList<string> VisibleProblemCodesForTests => AllProblems().Select(value => value.code).ToArray();
        public void ApplyDeepLinkForTests(string stableId)
        {
            pendingDeepLinkStableId = stableId ?? string.Empty;
            TryApplyPendingDeepLink();
        }
        public void InvokeLifecycleForTests(string transition)
        {
            switch (transition)
            {
                case "before-reload": BeforeReload(); break;
                case "playmode": OnPlayModeChanged(PlayModeStateChange.ExitingEditMode); break;
                case "disable": OnDisable(); break;
                default: throw new ArgumentOutOfRangeException(nameof(transition));
            }
        }
        public void InvokeActionForTests(string action)
        {
            switch (action)
            {
                case "shared-filter": sharedOnly = !sharedOnly; session.SharedOnly = sharedOnly; RebuildRows(); break;
                case "toggle-expanded": ToggleExpanded(session.SelectedStableId); break;
                case "rename": RunMutation(CurrentIsRequest ? "RenameRequest" : "RenameDefinition"); break;
                case "create-definition": RunMutation("CreateDefinition"); break;
                case "create-request": RunMutation("CreateRequestForExistingDefinition"); break;
                case "duplicate": RunMutation(CurrentIsRequest ? "DuplicateRequest" : "DuplicateDefinition"); break;
                case "delete": RunMutation(CurrentIsRequest ? "DeleteRequest" : "DeleteDefinition"); break;
                case "reference-impact-confirm": ConfirmPendingMutation(); break;
                case "reference-impact-cancel": CancelPendingMutation(); break;
                case "refresh-button": LoadWorkspace(true); break;
                case "external-reload": ReloadExternal(); break;
                case "external-compare": CompareExternal(); break;
                case "external-reapply": ReapplyExternal(); break;
                case "external-cancel": CancelExternal(); break;
                case "save-button": Save(); break;
                case "validate-button": Validate(); break;
                case "undo": session.Undo(); RebuildDraftFromCurrentHistory(false); break;
                case "redo": session.Redo(); RebuildDraftFromCurrentHistory(false); break;
                case "rebuild": RebuildRows(); RefreshSessionUi(); break;
                default: throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown test action.");
            }
        }
        public void CopyProblemForTests(int index) { ProjectileAuthoringProblemView item = FilteredProblems().ElementAt(index); EditorGUIUtility.systemCopyBuffer = item.code + " " + item.message; }
        public void LocateProblemForTests(int index) { LocateProblem(FilteredProblems().ElementAt(index)); }
        public void SelectPresentationSlotForTests(string slotKind) { SelectPresentationSlot(slotKind); }
        public void PreviewPresentationSlotForTests(string slotKind) { SelectAndPreviewPresentationSlot(slotKind); }

        private void BuildVisualTree()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("pa-window");
            bool hasStyleSheetPath = MxFrameworkPackageAssetLocator.TryResolveAssetPath(
                "Editor/ProjectileAuthoring/ProjectileAuthoringWindow.uss",
                out string styleSheetPath);
            StyleSheet sheet = hasStyleSheetPath
                ? AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath)
                : null;
            if (sheet != null) rootVisualElement.styleSheets.Add(sheet);

            var toolbar = new Toolbar { name = "projectile-authoring-toolbar" };
            toolbar.Add(new Label("投射物创作") { name = "package-label" });
            toolbar.Add(new Label(
                string.IsNullOrWhiteSpace(packageSourceLabel)
                    ? "未配置项目投射物源"
                    : packageSourceLabel) { name = "source-root-label" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(new Button(Validate) { text = "校验", name = "validate-button" });
            saveButton = new Button(Save) { text = "保存", name = "save-button" }; toolbar.Add(saveButton);
            undoButton = new Button(() => { session.Undo(); RebuildDraftFromCurrentHistory(false); }) { text = "撤销", name = "undo-button" }; toolbar.Add(undoButton);
            redoButton = new Button(() => { session.Redo(); RebuildDraftFromCurrentHistory(false); }) { text = "重做", name = "redo-button" }; toolbar.Add(redoButton);
            dirty = new Label("0 项未保存") { name = "dirty-count" }; toolbar.Add(dirty);
            toolbar.Add(new Button(() => LoadWorkspace(true)) { text = "刷新", name = "refresh-button" });
            status = new Label("加载中…") { name = "external-conflict-status" }; toolbar.Add(status);
            rootVisualElement.Add(toolbar);

            conflictActions = new VisualElement { name = "external-conflict-actions" }; conflictActions.AddToClassList("pa-row");
            conflictActions.Add(new Button(ReloadExternal) { text = "从磁盘重新加载", name = "external-reload" });
            conflictActions.Add(new Button(CompareExternal) { text = "查看差异摘要", name = "external-compare" });
            conflictActions.Add(new Button(ReapplyExternal) { text = "重新应用草稿", name = "external-reapply" });
            conflictActions.Add(new Button(CancelExternal) { text = "取消", name = "external-cancel" });
            conflictActions.style.display = DisplayStyle.None;
            rootVisualElement.Add(conflictActions);

            var workspaceRoot = new VisualElement { name = "projectile-authoring-workspace" }; workspaceRoot.AddToClassList("pa-workspace");
            outerSplit = new TwoPaneSplitView(0, session.NavigationPaneSize, TwoPaneSplitViewOrientation.Horizontal) { name = "projectile-authoring-outer-split" };
            outerSplit.Add(BuildNavigation());
            inspectorSplit = new TwoPaneSplitView(1, session.InspectorPaneSize, TwoPaneSplitViewOrientation.Horizontal) { name = "projectile-authoring-inspector-split" };
            inspectorSplit.Add(BuildCenter()); inspectorSplit.Add(BuildInspector()); outerSplit.Add(inspectorSplit); workspaceRoot.Add(outerSplit);
            rootVisualElement.Add(workspaceRoot);
            rootVisualElement.Add(BuildProblems());
            RefreshSessionUi();
        }

        private VisualElement BuildNavigation()
        {
            var pane = new VisualElement { name = "definition-navigation" }; pane.AddToClassList("pa-pane");
            pane.Add(new Label("定义 / 请求变体") { name = "definition-pane-title" });
            search = new ToolbarSearchField { name = "definition-search" }; search.SetValueWithoutNotify(session.Filter ?? string.Empty); search.RegisterValueChangedCallback(evt => { session.Filter = evt.newValue; RebuildRows(); }); pane.Add(search);
            var filters = new VisualElement { name = "definition-filters" }; filters.AddToClassList("pa-row");
            motionFilter = NewFilter("motion-filter", new[] { "All Motion" }, RebuildRows); filters.Add(motionFilter);
            impactFilter = NewFilter("impact-filter", new[] { "All Impact" }, RebuildRows); filters.Add(impactFilter);
            statusFilter = NewFilter("status-filter", new[] { "All Status", "Source", "Generated-only", "External Changed", "Invalid" }, RebuildRows); filters.Add(statusFilter);
            usedOnly = session.UsedOnly; sharedOnly = session.SharedOnly; dirtyOnly = session.DirtyOnly; invalidOnly = session.InvalidOnly;
            filters.Add(NewToggle("used-filter", "已使用", usedOnly, () => { usedOnly = !usedOnly; session.UsedOnly = usedOnly; }));
            filters.Add(NewToggle("shared-filter", "共享", sharedOnly, () => { sharedOnly = !sharedOnly; session.SharedOnly = sharedOnly; }));
            filters.Add(NewToggle("dirty-filter", "未保存", dirtyOnly, () => { dirtyOnly = !dirtyOnly; session.DirtyOnly = dirtyOnly; }));
            filters.Add(NewToggle("invalid-filter", "无效", invalidOnly, () => { invalidOnly = !invalidOnly; session.InvalidOnly = invalidOnly; }));
            pane.Add(filters);
            list = new ListView(rows, 42, MakeRow, BindRow) { name = "definition-request-tree", selectionType = SelectionType.Single, virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight };
            list.selectionChanged += selection => Select(selection.FirstOrDefault() as NavigationRow);
            list.itemsChosen += selection => { NavigationRow row = selection.FirstOrDefault() as NavigationRow; if (row != null && !row.IsRequest && !row.IsGeneratedOnly) ToggleExpanded(row.StableId); };
            pane.Add(list);
            return pane;
        }

        private PopupField<string> NewFilter(string name, IEnumerable<string> choices, Action changed)
        {
            var field = new PopupField<string>(choices.ToList(), 0) { name = name };
            field.formatListItemCallback = LocalizeUiValue;
            field.formatSelectedValueCallback = LocalizeUiValue;
            field.RegisterValueChangedCallback(_ => changed()); return field;
        }

        private Button NewToggle(string name, string text, bool selected, Action toggle)
        {
            var button = new Button { name = name, text = text };
            button.EnableInClassList("pa-filter-selected", selected);
            button.clicked += () => { toggle(); button.EnableInClassList("pa-filter-selected", !button.ClassListContains("pa-filter-selected")); RebuildRows(); };
            return button;
        }

        private VisualElement BuildCenter()
        {
            var pane = new VisualElement { name = "preview-timeline-shell" }; pane.AddToClassList("pa-center");
            pane.Add(new Label("预览 / 生命周期") { name = "preview-title" });
            var preview = new VisualElement { name = "preview-placeholder" };
            preview.style.flexGrow = 1f;
            preview.Add(BuildPreviewSurface());
            pane.Add(preview);

            var dock = new VisualElement { name = "projectile-authoring-dock" }; dock.AddToClassList("pa-authoring-dock");
            var lifecycleSection = new VisualElement { name = "projectile-lifecycle-section" }; lifecycleSection.AddToClassList("pa-lifecycle-section");
            lifecycleTitle = new Label { name = "projectile-lifecycle-title" }; lifecycleSection.Add(lifecycleTitle);
            var lifecycleScroll = new ScrollView(ScrollViewMode.Horizontal) { name = "projectile-lifecycle-scroll" }; lifecycleScroll.AddToClassList("pa-lifecycle-scroll");
            lifecycleCardsRoot = new VisualElement { name = "projectile-lifecycle-cards" }; lifecycleCardsRoot.AddToClassList("pa-lifecycle-grid"); lifecycleScroll.Add(lifecycleCardsRoot); lifecycleSection.Add(lifecycleScroll);
            dock.Add(lifecycleSection);

            previewTimelineSection = new VisualElement { name = "timeline-placeholder" };
            previewTimelineSection.AddToClassList("pa-timeline");
            previewTimelineSection.style.display = DisplayStyle.None;
            previewTimelineSection.Add(BuildPreviewTimeline());
            dock.Add(previewTimelineSection);
            pane.Add(dock);
            RenderLifecycleCards(null);
            return pane;
        }

        private void RenderLifecycleCards(ProjectileDefinitionListItem definition)
        {
            if (lifecycleCardsRoot == null) return;
            lifecycleCardsRoot.Clear();
            ProjectilePresentationBindingListItem binding = workspace?.bindings?.FirstOrDefault(value => value.projectileDefinitionId == definition?.definitionId);
            lifecycleTitle.text = binding?.legacyV1 == true ? "生命周期表现 · Legacy V1（显式迁移后可编辑七槽）" : "生命周期表现 · Presentation Binding v2";
            ProjectilePresentationSlotsView slots = binding?.detail?.slots;
            AddLifecycleCard("spawn", "出生", "投射物原点 · 单次", slots?.spawn);
            AddLifecycleCard("flightBody", "飞行主体", "投射物变换 · 持续", slots?.flightBody);
            AddLifecycleCard("trail", "拖尾", "投射物变换 · 持续", slots?.trail);
            AddLifecycleCard("directImpact", "直接命中", "命中点 · 单次", slots?.directImpact);
            AddLifecycleCard("areaImpact", "范围命中", "范围脉冲中心 · 单次", slots?.areaImpact);
            AddLifecycleCard("blockedImpact", "格挡命中", "命中点 · 单次", slots?.blockedImpact);
            AddLifecycleCard("expire", "过期", "最终位置 · 单次", slots?.expire);
            ProjectilePresentationExitPolicyView exit = binding?.detail?.exitPolicy;
            var exitCard = new Button(() => SelectPresentationSlot("exitPolicy")) { name = "lifecycle-card-exitPolicy", text = "退出策略\n" + (exit == null ? "默认策略" : LocalizeUiValue(exit.acceptedImpact) + " · " + LocalizeUiValue(exit.expiration)) };
            exitCard.AddToClassList("pa-lifecycle-card"); lifecycleCardsRoot.Add(exitCard);
            exitCard.EnableInClassList("pa-lifecycle-selected", selectedPresentationSlotKind == "exitPolicy");
        }

        private void AddLifecycleCard(string kind, string title, string contract, ProjectilePresentationSlotView slot)
        {
            string serializedKey = SerializeResourceKey(slot?.resource);
            bool hasResource = !string.IsNullOrWhiteSpace(serializedKey);
            ProjectileAuthoringVfxTransientView snapshot = PresentationVfxBridge.Resolve(serializedKey);
            string contractDiagnostic = hasResource
                ? ProjectileAuthoringVfxSelectionBridge.ContractDiagnostic(
                    kind, snapshot.Role, snapshot.Anchor, snapshot.Space, snapshot.Lifecycle)
                : string.Empty;
            var card = new Button(() => SelectAndPreviewPresentationSlot(kind))
            {
                name = "lifecycle-card-" + kind,
                text = string.Empty
            };
            if (snapshot.Thumbnail != null)
            {
                var thumbnail = new Image { name = "lifecycle-card-" + kind + "-thumbnail", image = snapshot.Thumbnail, scaleMode = ScaleMode.ScaleAndCrop };
                thumbnail.style.width = 38f;
                thumbnail.style.height = 28f;
                card.Add(thumbnail);
            }
            string state = snapshot.IsStale ? "Stale"
                : snapshot.IsCandidate ? "Candidate"
                : !hasResource ? "Missing"
                : snapshot.Status;
            var titleLabel = new Label(title) { name = "lifecycle-card-title" }; titleLabel.AddToClassList("pa-lifecycle-card-title"); card.Add(titleLabel);
            var resourceLabel = new Label(slot?.resource?.id ?? "未配置") { name = "lifecycle-card-resource" }; resourceLabel.AddToClassList("pa-lifecycle-card-resource"); card.Add(resourceLabel);
            var stateLabel = new Label(LocalizeUiValue(state)) { name = "lifecycle-card-state" }; stateLabel.AddToClassList("pa-lifecycle-card-state"); card.Add(stateLabel);
            card.tooltip = contract + "\n" + LocalizeUiValue(snapshot.Role) + " · "
                + LocalizeUiValue(snapshot.Lifecycle) + " · " + LocalizeUiValue(snapshot.Review)
                + (string.IsNullOrWhiteSpace(contractDiagnostic)
                    ? string.Empty
                    : "\n语义建议：" + contractDiagnostic);
            card.AddToClassList("pa-lifecycle-card");
            if (!hasResource) card.AddToClassList("pa-lifecycle-missing");
            card.EnableInClassList("pa-lifecycle-selected", selectedPresentationSlotKind == kind);
            lifecycleCardsRoot.Add(card);
        }

        private void SelectPresentationSlot(string kind)
        {
            selectedPresentationSlotKind = kind;
            SetInspectorTab("presentation");
            RenderLifecycleCards(SelectedDefinition());
            RenderPresentationInspector();
            string targetName = kind == "exitPolicy" ? "presentation-exit-policy" : "presentation-slot-" + kind;
            lastProblemFocusTarget = targetName;
            presentationInspectorRoot?.schedule.Execute(() =>
            {
                VisualElement target = presentationInspectorRoot.Q(targetName);
                target?.Focus();
                if (target != null) presentationInspectorScroll?.ScrollTo(target);
            });
        }

        private VisualElement BuildInspector()
        {
            var pane = new VisualElement { name = "identity-inspector" }; pane.AddToClassList("pa-pane"); pane.AddToClassList("pa-inspector-pane");
            inspectorTitle = new Label("未选择") { name = "inspector-title" }; pane.Add(inspectorTitle);
            inspectorBody = new Label("选择投射物定义、请求变体或仅生成条目，查看标识、来源与共享影响。") { name = "inspector-summary" }; pane.Add(inspectorBody);

            var tabs = new VisualElement { name = "inspector-tabs" }; tabs.AddToClassList("pa-inspector-tabs");
            configTabButton = NewInspectorTab("inspector-tab-config", "配置", "config"); tabs.Add(configTabButton);
            presentationTabButton = NewInspectorTab("inspector-tab-presentation", "表现 VFX", "presentation"); tabs.Add(presentationTabButton);
            diagnosticsTabButton = NewInspectorTab("inspector-tab-diagnostics", "诊断", "diagnostics"); tabs.Add(diagnosticsTabButton);
            pane.Add(tabs);

            configInspectorScroll = new ScrollView { name = "inspector-config-scroll" }; configInspectorScroll.AddToClassList("pa-inspector-content");
            var configRoot = new VisualElement { name = "inspector-config-root" }; configInspectorScroll.Add(configRoot);
            identityField = new TextField("新稳定 ID") { name = "identity-input" }; configRoot.Add(identityField);
            var buttons = new VisualElement { name = "crud-actions" }; buttons.AddToClassList("pa-crud");
            buttons.Add(new Button(() => RunMutation("CreateDefinition")) { text = "新建定义", name = "create-definition" });
            buttons.Add(new Button(() => RunMutation("CreateRequestForExistingDefinition")) { text = "新建请求", name = "create-request" });
            buttons.Add(new Button(() => RunMutation(CurrentIsRequest ? "DuplicateRequest" : "DuplicateDefinition")) { text = "复制", name = "duplicate" });
            buttons.Add(new Button(() => RunMutation(CurrentIsRequest ? "RenameRequest" : "RenameDefinition")) { text = "重命名", name = "rename" });
            buttons.Add(new Button(() => RunMutation(CurrentIsRequest ? "DeleteRequest" : "DeleteDefinition")) { text = "删除", name = "delete" });
            buttons.Add(new Button(() => RunMutation("AdoptGeneratedOnly")) { text = "接管生成条目", name = "adopt-generated-button" });
            configRoot.Add(buttons);
            impactPanel = new VisualElement { name = "reference-impact-panel" }; impactPanel.AddToClassList("pa-impact");
            impactSummary = new Label { name = "reference-impact-summary" }; impactDetails = new Label { name = "reference-impact-details" };
            impactConfirm = new Button(ConfirmPendingMutation) { text = "确认生成草稿", name = "reference-impact-confirm" };
            impactPanel.Add(new Label("引用影响")); impactPanel.Add(impactSummary); impactPanel.Add(impactDetails); impactPanel.Add(impactConfirm); impactPanel.Add(new Button(CancelPendingMutation) { text = "取消", name = "reference-impact-cancel" });
            impactPanel.style.display = DisplayStyle.None; configRoot.Add(impactPanel);
            configRoot.Add(new HelpBox("共享或破坏性操作会先生成引用影响。阻塞使用方无法通过确认绕过；权威写入只经 M1 apply。", HelpBoxMessageType.Info));
            ruleInspectorRoot = new VisualElement { name = "projectile-rule-inspector" };
            configRoot.Add(ruleInspectorRoot); pane.Add(configInspectorScroll);

            presentationInspectorScroll = new ScrollView { name = "inspector-presentation-scroll" }; presentationInspectorScroll.AddToClassList("pa-inspector-content");
            presentationInspectorRoot = new VisualElement { name = "projectile-presentation-inspector" }; presentationInspectorScroll.Add(presentationInspectorRoot); pane.Add(presentationInspectorScroll);

            diagnosticsInspectorScroll = new ScrollView { name = "inspector-diagnostics-scroll" }; diagnosticsInspectorScroll.AddToClassList("pa-inspector-content");
            diagnosticsInspectorRoot = new VisualElement { name = "projectile-diagnostics-inspector" }; diagnosticsInspectorScroll.Add(diagnosticsInspectorRoot); pane.Add(diagnosticsInspectorScroll);

            SetInspectorTab(selectedInspectorTab);
            return pane;
        }

        private Button NewInspectorTab(string name, string text, string tab)
        {
            var button = new Button(() => SetInspectorTab(tab)) { name = name, text = text };
            button.AddToClassList("pa-inspector-tab");
            return button;
        }

        private void SetInspectorTab(string tab)
        {
            selectedInspectorTab = tab;
            if (configInspectorScroll != null) configInspectorScroll.style.display = tab == "config" ? DisplayStyle.Flex : DisplayStyle.None;
            if (presentationInspectorScroll != null) presentationInspectorScroll.style.display = tab == "presentation" ? DisplayStyle.Flex : DisplayStyle.None;
            if (diagnosticsInspectorScroll != null) diagnosticsInspectorScroll.style.display = tab == "diagnostics" ? DisplayStyle.Flex : DisplayStyle.None;
            configTabButton?.EnableInClassList("pa-inspector-tab-selected", tab == "config");
            presentationTabButton?.EnableInClassList("pa-inspector-tab-selected", tab == "presentation");
            diagnosticsTabButton?.EnableInClassList("pa-inspector-tab-selected", tab == "diagnostics");
        }

        private VisualElement BuildProblems()
        {
            problemsDrawer = new Foldout { name = "problems-drawer", text = "问题 0", value = false }; problemsDrawer.AddToClassList("pa-problems");
            severityFilter = NewFilter("problem-severity", new[] { "All", "Error", "Warning", "Info" }, () => RenderProblems()); problemsDrawer.Add(severityFilter);
            problems = new ScrollView { name = "problems-list" }; problemsDrawer.Add(problems); return problemsDrawer;
        }

        private VisualElement MakeRow()
        {
            var root = new VisualElement(); root.AddToClassList("pa-navigation-row"); root.Add(new Label { name = "row-title" }); root.Add(new Label { name = "row-badges" }); return root;
        }

        private void BindRow(VisualElement element, int index)
        {
            NavigationRow row = rows[index]; element.Q<Label>("row-title").text = row.IsRequest ? "    ↳ " + row.StableId : row.IsGeneratedOnly ? "◇ " + row.StableId : "◆ " + row.StableId;
            element.Q<Label>("row-badges").text = LocalizeBadges(row.Badges);
        }

        private void LoadWorkspace(bool compare)
        {
            if (service == null || service.IsBusy) return;
            status.text = "加载中…";
            service.LoadWorkspace(packagePath, (view, error) =>
            {
                if (disposed) return;
                if (ProjectileAuthoringCliService.IsUnavailableError(error))
                {
                    status.text = "不可用 — 未配置 Authoring CLI";
                    return;
                }
                if (!string.IsNullOrEmpty(error)) { status.text = "加载失败"; ShowError(error); return; }
                if (compare && workspace != null && !string.Equals(session.WorkspaceHash, view.workspaceHash, StringComparison.Ordinal) && session.DirtySources.Count > 0)
                {
                    pendingExternalWorkspace = view; session.MarkExternalChanged(true); status.text = "外部文件已更改 — 已阻止保存"; conflictActions.style.display = DisplayStyle.Flex; RebuildRows(); RefreshSessionUi(); return;
                }
                AcceptWorkspace(view, true);
            });
        }

        private void AcceptWorkspace(ProjectileAuthoringWorkspaceView view, bool resetSession)
        {
            diskWorkspace = view; workspace = view;
            bool restoredExternalChange = resetSession && restoredSession && session.PendingCommands.Count > 0 && !string.Equals(session.WorkspaceHash, view.workspaceHash, StringComparison.Ordinal);
            bool rebuildRestoredDraft = resetSession && restoredSession && session.PendingCommands.Count > 0;
            if (resetSession && !rebuildRestoredDraft) session.Reset(view.workspaceHash);
            restoredSession = false;
            status.text = "就绪"; validationProblems = view.diagnostics ?? Array.Empty<ProjectileAuthoringProblemView>(); transientProblems = Array.Empty<ProjectileAuthoringProblemView>(); PopulateFilterChoices(); RebuildRows(); RestoreSelectionAfterWorkspaceLoad(); RenderProblems(); RefreshSessionUi();
            if (restoredExternalChange) { pendingExternalWorkspace = view; session.MarkExternalChanged(true); status.text = "外部文件已更改 — 恢复的草稿需要检查"; conflictActions.style.display = DisplayStyle.Flex; RebuildRows(); RefreshSessionUi(); return; }
            if (rebuildRestoredDraft) { status.text = "正在恢复待处理命令"; RebuildDraftFromCurrentHistory(false); }
            TryApplyPendingDeepLink();
        }

        private void TryApplyPendingDeepLink()
        {
            if (workspace == null || string.IsNullOrWhiteSpace(pendingDeepLinkStableId))
                return;

            string stableId = pendingDeepLinkStableId;
            pendingDeepLinkStableId = string.Empty;
            ProjectileRequestVariantListItem request = workspace.requests?.FirstOrDefault(value =>
                string.Equals(value.stableId, stableId, StringComparison.Ordinal));
            ProjectileDefinitionListItem definition = workspace.definitions?.FirstOrDefault(value =>
                string.Equals(value.stableId, stableId, StringComparison.Ordinal));
            ProjectileGeneratedOnlyListItem generated = workspace.generatedOnly?.FirstOrDefault(value =>
                string.Equals(value.stableId, stableId, StringComparison.Ordinal));
            if (request == null && definition == null && generated == null)
            {
                transientProblems = (transientProblems ?? Array.Empty<ProjectileAuthoringProblemView>())
                    .Concat(new[]
                    {
                        new ProjectileAuthoringProblemView
                        {
                            code = "PROJAUTH027",
                            severity = "Warning",
                            message = "Deep-link stable id was not found: " + stableId,
                            objectPath = "request:" + stableId,
                            fieldPath = "projectileStableId",
                            relatedStableIds = new[] { stableId },
                            suggestedFix = "Choose a compiled Request or repair the CharacterAction reference.",
                        },
                    })
                    .ToArray();
                RenderProblems();
                status.text = "深层链接目标不存在 — 请查看问题列表";
                return;
            }

            if (request != null)
            {
                ProjectileDefinitionListItem owner = workspace.definitions.FirstOrDefault(value =>
                    value.requestIds?.Contains(stableId) == true);
                if (owner != null)
                {
                    var expanded = new HashSet<string>(session.ExpandedStableIds ?? Array.Empty<string>(), StringComparer.Ordinal)
                    {
                        owner.stableId,
                    };
                    session.ExpandedStableIds = expanded.ToArray();
                }
            }
            session.SelectedStableId = stableId;
            CurrentIsRequest = request != null;
            CurrentIsGeneratedOnly = generated != null;
            RebuildRows();
            int rowIndex = rows.FindIndex(value => value.StableId == stableId
                && value.IsRequest == (request != null)
                && value.IsGeneratedOnly == (generated != null));
            if (rowIndex >= 0)
            {
                list?.SetSelection(rowIndex);
                list?.ScrollToItem(rowIndex);
                Select(rows[rowIndex]);
            }
        }

        private void PopulateFilterChoices()
        {
            string motion = session.MotionFilter; string impact = session.ImpactFilter;
            motionFilter.choices = new[] { "All Motion" }.Concat((workspace.definitions ?? Array.Empty<ProjectileDefinitionListItem>()).Select(value => value.motion).Where(value => !string.IsNullOrEmpty(value)).Distinct().OrderBy(value => value)).ToList();
            impactFilter.choices = new[] { "All Impact" }.Concat((workspace.definitions ?? Array.Empty<ProjectileDefinitionListItem>()).Select(value => value.impact).Where(value => !string.IsNullOrEmpty(value)).Distinct().OrderBy(value => value)).ToList();
            motionFilter.SetValueWithoutNotify(motionFilter.choices.Contains(motion) ? motion : "All Motion"); impactFilter.SetValueWithoutNotify(impactFilter.choices.Contains(impact) ? impact : "All Impact");
            statusFilter.SetValueWithoutNotify(statusFilter.choices.Contains(session.StatusFilter) ? session.StatusFilter : "All Status");
            session.MotionFilter = motionFilter.value; session.ImpactFilter = impactFilter.value; session.StatusFilter = statusFilter.value;
        }

        private void RebuildRows()
        {
            if (motionFilter != null) session.MotionFilter = motionFilter.value;
            if (impactFilter != null) session.ImpactFilter = impactFilter.value;
            if (statusFilter != null) session.StatusFilter = statusFilter.value;
            string selected = session.SelectedStableId; rows.Clear();
            if (workspace != null)
            {
                string query = search?.value ?? string.Empty;
                foreach (ProjectileDefinitionListItem definition in workspace.definitions ?? Array.Empty<ProjectileDefinitionListItem>())
                {
                    bool invalid = HasProblemFor(definition.stableId, null); bool isDirty = DefinitionIsDirty(definition); bool external = session.HasExternalChanges;
                    bool matches = TextMatches(definition.stableId, definition.requestIds, query)
                        && (motionFilter.value == "All Motion" || definition.motion == motionFilter.value)
                        && (impactFilter.value == "All Impact" || definition.impact == impactFilter.value)
                        && (!usedOnly || (definition.requestIds?.Length ?? 0) > 0) && (!sharedOnly || definition.shared) && (!dirtyOnly || isDirty) && (!invalidOnly || invalid)
                        && StatusMatches("Source", invalid, external);
                    if (!matches) continue;
                    rows.Add(new NavigationRow(definition.stableId, false, false, BuildBadges("Source", definition.shared, isDirty, external, invalid, definition.motion, definition.impact)));
                    if (!IsExpanded(definition.stableId)) continue;
                    foreach (string requestId in definition.requestIds ?? Array.Empty<string>())
                    {
                        ProjectileRequestVariantListItem request = workspace.requests.FirstOrDefault(value => value.stableId == requestId);
                        bool requestDirty = request != null && session.DirtySources.Contains(request.sourcePath); bool requestInvalid = HasProblemFor(requestId, request?.sourcePath);
                        if ((!dirtyOnly || requestDirty) && (!invalidOnly || requestInvalid) && StatusMatches("Source", requestInvalid, external))
                            rows.Add(new NavigationRow(requestId, true, false, BuildBadges("Request", false, requestDirty, external, requestInvalid, string.Empty, string.Empty)));
                    }
                }
                foreach (ProjectileGeneratedOnlyListItem generated in workspace.generatedOnly ?? Array.Empty<ProjectileGeneratedOnlyListItem>())
                {
                    bool invalid = !generated.complete || string.Equals(generated.status, "Invalid", StringComparison.OrdinalIgnoreCase);
                    if (!TextMatches(generated.stableId, null, query) || usedOnly || sharedOnly || dirtyOnly || (invalidOnly && !invalid) || !StatusMatches("Generated-only", invalid, false)) continue;
                    if (motionFilter.value != "All Motion" && generated.motion != motionFilter.value) continue;
                    if (impactFilter.value != "All Impact" && generated.impact != impactFilter.value) continue;
                    rows.Add(new NavigationRow(generated.stableId, false, true, BuildBadges("Generated-only", false, false, false, invalid, generated.motion, generated.impact)));
                }
            }
            list?.Rebuild();
            int index = rows.FindIndex(row => row.StableId == selected);
            if (index >= 0)
            {
                CurrentIsRequest = rows[index].IsRequest;
                CurrentIsGeneratedOnly = rows[index].IsGeneratedOnly;
                list?.SetSelection(index);
            }
            else if (rows.Count > 0 && !string.IsNullOrEmpty(selected))
            {
                session.SelectedStableId = string.Empty;
                CurrentIsRequest = false;
                CurrentIsGeneratedOnly = false;
            }
        }

        private void RestoreSelectionAfterWorkspaceLoad()
        {
            if (string.IsNullOrEmpty(session.SelectedStableId))
            {
                RefreshPreviewForSelection();
                return;
            }

            int index = rows.FindIndex(row => row.StableId == session.SelectedStableId);
            if (index < 0)
            {
                session.SelectedStableId = string.Empty;
                CurrentIsRequest = false;
                CurrentIsGeneratedOnly = false;
                RefreshPreviewForSelection();
                return;
            }

            NavigationRow row = rows[index];
            CurrentIsRequest = row.IsRequest;
            CurrentIsGeneratedOnly = row.IsGeneratedOnly;
            list?.SetSelection(index);
            list?.ScrollToItem(index);
            Select(row);
        }

        private bool IsExpanded(string stableId)
        {
            string[] expanded = session.ExpandedStableIds ?? Array.Empty<string>();
            return expanded.Length == 0 || expanded.Contains(stableId);
        }

        private void ToggleExpanded(string stableId)
        {
            if (string.IsNullOrEmpty(stableId) || workspace == null || workspace.definitions.All(value => value.stableId != stableId)) return;
            var expanded = new HashSet<string>(session.ExpandedStableIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (expanded.Count == 0) foreach (ProjectileDefinitionListItem item in workspace.definitions) expanded.Add(item.stableId);
            if (!expanded.Remove(stableId)) expanded.Add(stableId);
            session.ExpandedStableIds = expanded.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            RebuildRows();
        }

        private bool StatusMatches(string sourceState, bool invalid, bool external)
        {
            string value = statusFilter?.value ?? "All Status";
            return value == "All Status" || value == sourceState || (value == "Invalid" && invalid) || (value == "External Changed" && external);
        }

        private bool HasProblemFor(string stableId, string sourcePath) => AllProblems().Any(problem => (!string.IsNullOrEmpty(sourcePath) && problem.sourcePath == sourcePath) || (!string.IsNullOrEmpty(stableId) && ((problem.message?.Contains(stableId) ?? false) || (problem.sourcePath?.Contains(stableId) ?? false))));
        private bool DefinitionIsDirty(ProjectileDefinitionListItem definition)
        {
            IEnumerable<string> requestPaths = (definition.requestIds ?? Array.Empty<string>()).Select(id => workspace.requests.FirstOrDefault(value => value.stableId == id)?.sourcePath);
            return (definition.sourcePaths ?? Array.Empty<string>()).Concat(definition.presentationSourcePaths ?? Array.Empty<string>()).Concat(requestPaths).Any(path => !string.IsNullOrEmpty(path) && session.DirtySources.Contains(path));
        }
        private static bool TextMatches(string id, string[] children, string query) => string.IsNullOrEmpty(query) || (id?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (children?.Any(value => value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ?? false);
        private static string BuildBadges(string state, bool shared, bool dirtyValue, bool external, bool invalid, string motion, string impact) => string.Join(" · ", new[] { state, motion, impact, shared ? "Shared" : null, dirtyValue ? "Dirty" : null, external ? "External Changed" : null, invalid ? "Invalid" : null }.Where(value => !string.IsNullOrEmpty(value)));

        private static string LocalizeBadges(string badges) => string.Join(" · ", (badges ?? string.Empty).Split(new[] { " · " }, StringSplitOptions.None).Select(LocalizeUiValue));

        private ProjectileDefinitionListItem SelectedDefinition()
        {
            if (workspace?.definitions == null || CurrentIsGeneratedOnly || string.IsNullOrEmpty(session.SelectedStableId)) return null;
            return workspace.definitions.FirstOrDefault(value => CurrentIsRequest
                ? value.requestIds?.Contains(session.SelectedStableId) ?? false
                : value.stableId == session.SelectedStableId);
        }

        private static string LocalizeUiValue(string value)
        {
            switch (value)
            {
                case "All": return "全部";
                case "All Motion": return "全部运动类型";
                case "All Impact": return "全部命中类型";
                case "All Status": return "全部状态";
                case "Source": return "源配置";
                case "Generated-only": return "仅生成";
                case "External Changed": return "外部已更改";
                case "Invalid": return "无效";
                case "Shared": return "共享";
                case "Dirty": return "未保存";
                case "Error": return "错误";
                case "Warning": return "警告";
                case "Info": return "信息";
                case "Missing": return "缺失";
                case "Stale": return "已过期";
                case "Incompatible": return "不兼容";
                case "Candidate": return "候选资源";
                case "Ready": return "就绪";
                case "Complete": return "完成";
                case "Dissipate": return "消散";
                case "Immediate": return "立即移除";
                case "Persistent": return "持续";
                case "OneShot": return "单次";
                case "Flight": return "飞行";
                case "Trail": return "拖尾";
                case "Spawn": return "出生";
                case "Impact": return "命中";
                case "Expire": return "过期";
                case "Miss": return "未命中";
                case "Direct Hit": return "直接命中";
                case "Blocked": return "被格挡";
                case "Direct": return "直接命中";
                case "Area": return "范围命中";
                case "Area Hit": return "范围命中";
                case "Pierce": return "穿透";
                case "Late Attach": return "延迟挂接";
                case "VFX Preview": return "特效预览";
                case "Runtime Validation": return "运行时验证";
                case "Current Scenario": return "当前场景";
                case "All Branches": return "全部分支";
                case "Rebuild": return "重建";
                case "Exit": return "退出";
                case "EffectTimeline": return "效果时间轴";
                case "Diagnostic": return "诊断";
                case "Linear": return "直线";
                case "ArcToTarget": return "弧线飞向目标";
                case "BoomerangReturn": return "回旋返回";
                case "SwayForward": return "摆动前进";
                case "HomingToTarget": return "追踪目标";
                case "BezierToTarget": return "贝塞尔飞向目标";
                case "BallisticArcToTarget": return "抛物线飞向目标";
                case "DirectHit": return "直接命中";
                case "AreaPulseOnImpact": return "命中时范围脉冲";
                case "Constant": return "恒定";
                case "EaseInQuadratic": return "二次缓入";
                case "EaseOutQuadratic": return "二次缓出";
                case "EaseInOutSmoothstep": return "平滑缓入缓出";
                case "CasterForwardRange": return "施法者前方范围";
                case "LockedTargetCenter": return "锁定目标中心";
                case "LockedTargetOrCasterForwardRange": return "锁定目标或施法者前方范围";
                case "AllowRepeat": return "允许重复";
                case "OncePerProjectile": return "每个投射物一次";
                case "OncePerEvent": return "每个事件一次";
                case "BlockedPresentation": return "播放格挡表现";
                case "None": return "无";
                case "Socket": return "挂点";
                case "ProjectileForward": return "投射物前方";
                case "WorldUp": return "世界上方";
                case "SurfaceNormal": return "表面法线";
                case "ImpactNormalOrWorldUp": return "命中法线或世界上方";
                case "HostileCharacter": return "敌对角色";
                case "FriendlyCharacter": return "友方角色";
                case "Self": return "自身";
                case "ThirdPartyCharacter": return "第三方角色";
                case "DamageableObject": return "可伤害物体";
                case "WorldWall": return "世界墙体";
                case "WorldGround": return "世界地面";
                case "WorldStatic": return "世界静态物体";
                default: return value ?? string.Empty;
            }
        }

        private void Select(NavigationRow row)
        {
            if (row == null) return;
            session.SelectedStableId = row.StableId; CurrentIsRequest = row.IsRequest; CurrentIsGeneratedOnly = row.IsGeneratedOnly;
            if (row.IsGeneratedOnly)
            {
                ProjectileGeneratedOnlyListItem generated = workspace.generatedOnly.First(value => value.stableId == row.StableId); inspectorTitle.text = "仅生成条目（只读）"; inspectorBody.text = generated.stableId + "\n数字 ID：" + generated.definitionId + "\n状态：" + LocalizeUiValue(generated.status) + "\n完整：" + generated.complete;
                configTabButton.text = "配置"; presentationTabButton.SetEnabled(false); SetInspectorTab("config"); RenderLifecycleCards(null); RenderRuleInspector(); RefreshPreviewForSelection(); return;
            }
            inspectorTitle.text = row.IsRequest ? "请求变体" : "投射物定义";
            ProjectileDefinitionListItem definition = workspace.definitions.FirstOrDefault(value => row.IsRequest ? (value.requestIds?.Contains(row.StableId) ?? false) : value.stableId == row.StableId);
            ProjectileRequestVariantListItem request = workspace.requests.FirstOrDefault(value => value.stableId == row.StableId);
            inspectorBody.text = row.StableId + "\n" + (definition == null ? "" : "数字 ID：" + definition.definitionId + "\n引用数：" + (definition.requestIds?.Length ?? 0) + "\n共享：" + definition.shared + "\n配置来源：" + string.Join(", ", definition.sourcePaths ?? Array.Empty<string>()) + "\n表现来源：" + string.Join(", ", definition.presentationSourcePaths ?? Array.Empty<string>())) + (request == null ? "" : "\n配置来源：" + request.sourcePath + "\n引用定义：" + request.definitionStableId);
            configTabButton.text = row.IsRequest ? "请求配置" : "定义配置";
            presentationTabButton.SetEnabled(definition != null);
            SetInspectorTab("config");
            RenderLifecycleCards(definition);
            RenderRuleInspector();
            RefreshPreviewForSelection();
        }

        public bool CurrentIsRequest { get; private set; }
        public bool CurrentIsGeneratedOnly { get; private set; }

        private void RunMutation(string kind)
        {
            if (workspace == null || service.IsBusy) return;
            string newId = identityField.value?.Trim() ?? string.Empty;
            bool needsNewId = kind != "DeleteRequest" && kind != "DeleteDefinition" && kind != "AdoptGeneratedOnly";
            if (needsNewId && newId.Length == 0) { ShowError("请输入新的 stable ID。"); return; }
            if (kind == "AdoptGeneratedOnly" && !CurrentIsGeneratedOnly) { ShowError("请先选择 Generated-only 条目。"); return; }
            ProjectileDefinitionListItem selectedDefinition = workspace.definitions.FirstOrDefault(value => CurrentIsRequest ? (value.requestIds?.Contains(session.SelectedStableId) ?? false) : value.stableId == session.SelectedStableId);
            string definitionStableId = kind == "CreateDefinition" || kind == "DuplicateDefinition" || kind == "RenameDefinition" ? newId : selectedDefinition?.stableId ?? string.Empty;
            var request = new ProjectileAuthoringMutationRequestView { kind = kind, sourceStableId = session.SelectedStableId, stableId = newId, definitionStableId = definitionStableId, generatedOnlyStableId = CurrentIsGeneratedOnly ? session.SelectedStableId : string.Empty, createRequest = kind != "CreateDefinition", planOnly = true };
            PreviewMutation(request);
        }

        private void PreviewMutation(ProjectileAuthoringMutationRequestView request)
        {
            ProjectileAuthoringMutationRequestView[] sequence = session.PendingCommands.Select(CloneMutation).Concat(new[] { CloneMutation(request) }).ToArray();
            PreviewMutationSequence(sequence);
        }

        private void PreviewMutationSequence(ProjectileAuthoringMutationRequestView[] sequence)
        {
            service.MutateSequence(packagePath, sequence, (result, _, error) =>
            {
                if (disposed) return;
                if (!string.IsNullOrEmpty(error)) { ShowError(error); return; }
                RenderProblems(result?.diagnostics ?? Array.Empty<ProjectileAuthoringProblemView>());
                RenderRuleInspector();
                pendingSequence = sequence.Select(CloneMutation).ToArray();
                pendingMutation = pendingSequence.LastOrDefault(); pendingPlan = result;
                if (IsFieldEdit(pendingMutation)
                    && result != null
                    && result.status == "Ready"
                    && (result.impact == null || !result.impact.blocking)
                    && (result.impact?.requestStableIds?.Length ?? 0) <= 1)
                {
                    ConfirmPendingMutation();
                    return;
                }
                ShowImpact(result?.impact);
            });
        }

        private static bool IsFieldEdit(ProjectileAuthoringMutationRequestView request)
            => request != null && (request.kind == "EditDefinitionFields"
                || request.kind == "EditRequestFields"
                || request.kind == "SetPresentationSlot"
                || request.kind == "ClearPresentationSlot"
                || request.kind == "EditPresentationExitPolicy"
                || request.kind == "MigratePresentationBindingV1ToV2");

        private void ShowImpact(ProjectileAuthoringReferenceImpactView impact)
        {
            impact ??= new ProjectileAuthoringReferenceImpactView(); impactPanel.style.display = DisplayStyle.Flex;
            impactSummary.text = impact.summary ?? "没有引用影响。";
            impactDetails.text = "请求：\n" + string.Join("\n", impact.requestStableIds ?? Array.Empty<string>()) + "\n字段：\n" + string.Join("\n", impact.fieldPaths ?? Array.Empty<string>()) + "\n工作区令牌：\n" + (impact.workspaceToken ?? string.Empty) + "\n使用方：\n" + string.Join("\n", impact.consumerPaths ?? Array.Empty<string>()) + "\n需要授权：\n" + string.Join("\n", impact.authorizationRequiredPaths ?? Array.Empty<string>()) + "\n阻塞原因：\n" + string.Join("\n", impact.blockingReasons ?? Array.Empty<string>());
            impactConfirm.SetEnabled(!HasHardBlocking(impact) && pendingPlan != null && pendingPlan.status != "Blocked");
        }

        private void ConfirmPendingMutation()
        {
            if (pendingMutation == null || HasHardBlocking(pendingPlan?.impact)) return;
            ProjectileAuthoringMutationRequestView request = CloneMutation(pendingMutation); request.planOnly = false; request.confirmed = true; request.confirmDestructive = true; request.confirmationToken = pendingPlan?.impact?.confirmationToken; request.authorizedConsumerPaths = pendingPlan?.impact?.authorizationRequiredPaths ?? Array.Empty<string>();
            if (IsFieldEdit(request)) request.workspaceToken = pendingPlan?.impact?.workspaceToken ?? string.Empty;
            ProjectileAuthoringMutationRequestView[] confirmedSequence = pendingSequence.Select(CloneMutation).ToArray();
            confirmedSequence[confirmedSequence.Length - 1] = request;
            service.MutateSequence(packagePath, confirmedSequence, (result, draftPath, error) =>
            {
                if (disposed) return;
                if (!string.IsNullOrEmpty(error)) { ShowError(error); return; }
                RenderProblems(result?.diagnostics ?? Array.Empty<ProjectileAuthoringProblemView>());
                RenderRuleInspector();
                if (result == null || result.status != "Ready") { ShowImpact(result?.impact); return; }
                transientProblems = Array.Empty<ProjectileAuthoringProblemView>();
                string[] dirtyPaths = (result.dirtySources ?? Array.Empty<ProjectileAuthoringDirtySourceView>()).Select(value => value.relativePath).Where(path => !string.IsNullOrEmpty(path)).Distinct().ToArray();
                if (ExternalReplayActive)
                {
                    CompleteExternalReplayStep(result, draftPath, dirtyPaths, confirmedSequence, request);
                    return;
                }
                session.PushComposite(draftPath, dirtyPaths, confirmedSequence); ApplyProjectedWorkspace(result.projectedWorkspace); UpdateSelectionAfterMutation(request); status.text = result.impact?.summary ?? "草稿已就绪"; CancelPendingMutation(); RebuildRows(); RefreshSessionUi();
            });
        }

        private void UpdateSelectionAfterMutation(ProjectileAuthoringMutationRequestView request)
        {
            switch (request.kind)
            {
                case "CreateDefinition": case "DuplicateDefinition": case "RenameDefinition":
                    session.SelectedStableId = string.IsNullOrEmpty(request.definitionStableId) ? request.stableId : request.definitionStableId; CurrentIsRequest = false; CurrentIsGeneratedOnly = false; break;
                case "CreateRequestForExistingDefinition": case "DuplicateRequest": case "RenameRequest":
                    session.SelectedStableId = request.stableId; CurrentIsRequest = true; CurrentIsGeneratedOnly = false; break;
                case "DeleteDefinition": case "DeleteRequest":
                    session.SelectedStableId = string.Empty; CurrentIsRequest = false; CurrentIsGeneratedOnly = false; break;
            }
        }

        private static ProjectileAuthoringMutationRequestView CloneMutation(ProjectileAuthoringMutationRequestView value) => JsonUtility.FromJson<ProjectileAuthoringMutationRequestView>(JsonUtility.ToJson(value));
        private static bool HasHardBlocking(ProjectileAuthoringReferenceImpactView impact)
        {
            if (impact == null || !impact.blocking) return false;
            return (impact.blockingReasons ?? Array.Empty<string>()).Any(reason => !reason.StartsWith("unauthorized-consumer:", StringComparison.Ordinal)
                && !string.Equals(reason, "confirmation-required", StringComparison.Ordinal)
                && !string.Equals(reason, "shared-definition-confirmation-required", StringComparison.Ordinal));
        }
        private void CancelPendingMutation()
        {
            bool cancelledExternalReplay = ExternalReplayActive;
            pendingMutation = null; pendingPlan = null; pendingSequence = Array.Empty<ProjectileAuthoringMutationRequestView>();
            externalReplayCommands = Array.Empty<ProjectileAuthoringMutationRequestView>(); externalReplayPrefix = Array.Empty<ProjectileAuthoringMutationRequestView>(); externalReplayIndex = -1;
            if (impactPanel != null) impactPanel.style.display = DisplayStyle.None;
            if (cancelledExternalReplay)
            {
                session.MarkExternalChanged(true);
                status.text = "外部文件已更改 — 已取消变基并保留原草稿";
                if (conflictActions != null) conflictActions.style.display = DisplayStyle.Flex;
                RefreshSessionUi();
            }
        }

        private void Validate()
        {
            if (service.IsBusy) return;
            service.Validate(packagePath, (items, error) => { if (disposed) return; if (!string.IsNullOrEmpty(error)) ShowError(error); else { validationProblems = items ?? Array.Empty<ProjectileAuthoringProblemView>(); transientProblems = Array.Empty<ProjectileAuthoringProblemView>(); RenderProblems(); RebuildRows(); RenderRuleInspector(); } });
        }

        private void Save()
        {
            if (!session.CanSave || service.IsBusy) return;
            service.Apply(packagePath, session.DraftPath, (result, error) =>
            {
                if (disposed) return;
                if (!string.IsNullOrEmpty(error) || result == null || result.status != "Committed") { ShowError(error ?? "应用失败。"); RefreshSessionUi(); return; }
                if (!string.Equals(result.playerMirrorFreshness, "UpdatedAtomically", StringComparison.Ordinal))
                {
                    ShowError("保存未完成：Unity 与玩家端运行时数据没有通过原子同步校验。");
                    RefreshSessionUi();
                    return;
                }
                status.text = "已保存并同步到游戏";
                Debug.Log("[Projectile Authoring] 已保存源配置，并原子同步 Tools generated、Unity Generated 与 Player mirror。");
                refreshAssets(); LoadWorkspace(false);
            });
        }

        private void ReloadExternal()
        {
            if (pendingExternalWorkspace == null) return; session.MarkExternalChanged(false); session.Reset(pendingExternalWorkspace.workspaceHash); AcceptWorkspace(pendingExternalWorkspace, false); pendingExternalWorkspace = null; conflictActions.style.display = DisplayStyle.None; status.text = "已从磁盘重新加载";
        }

        private void CompareExternal()
        {
            if (pendingExternalWorkspace == null) return;
            var before = (diskWorkspace?.sources ?? Array.Empty<ProjectileAuthoringSourceView>()).ToDictionary(value => value.relativePath ?? string.Empty, value => value.expectedSha256 ?? string.Empty, StringComparer.Ordinal);
            var after = (pendingExternalWorkspace.sources ?? Array.Empty<ProjectileAuthoringSourceView>()).ToDictionary(value => value.relativePath ?? string.Empty, value => value.expectedSha256 ?? string.Empty, StringComparer.Ordinal);
            string[] changed = before.Keys.Intersect(after.Keys).Where(path => before[path] != after[path]).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] added = after.Keys.Except(before.Keys).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] removed = before.Keys.Except(after.Keys).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            status.text = "差异：已更改=" + string.Join(",", changed) + " 新增=" + string.Join(",", added) + " 删除=" + string.Join(",", removed) + " 待处理=" + string.Join(",", session.DirtySources.OrderBy(path => path, StringComparer.Ordinal));
        }

        private void ReapplyExternal()
        {
            if (pendingExternalWorkspace == null || session.PendingCommands.Count == 0) return;
            ProjectileAuthoringMutationRequestView[] replay = session.PendingCommands.Select(CloneMutation).ToArray();
            if (!replay.Any(IsFieldEdit))
            {
                session.MarkExternalChanged(false);
                session.Reset(pendingExternalWorkspace.workspaceHash);
                AcceptWorkspace(pendingExternalWorkspace, false);
                pendingExternalWorkspace = null;
                conflictActions.style.display = DisplayStyle.None;
                ReplayLegacyExternalSequence(replay);
                return;
            }
            externalReplayCommands = replay;
            externalReplayPrefix = Array.Empty<ProjectileAuthoringMutationRequestView>();
            externalReplayIndex = 0;
            status.text = "正在基于最新工作区重新应用待处理命令";
            ReplayNextExternalCommand();
        }

        private void ReplayLegacyExternalSequence(ProjectileAuthoringMutationRequestView[] commands)
        {
            service.MutateSequence(packagePath, commands, (result, draftPath, error) =>
            {
                if (disposed) return;
                if (!string.IsNullOrEmpty(error)) { session.MarkExternalChanged(true); ShowError(error); RefreshSessionUi(); return; }
                if (result == null || result.status != "Ready")
                {
                    pendingSequence = commands.Select(CloneMutation).ToArray(); pendingMutation = pendingSequence.LastOrDefault(); pendingPlan = result; session.MarkExternalChanged(true); ShowImpact(result?.impact); RefreshSessionUi(); return;
                }
                string[] dirtyPaths = (result.dirtySources ?? Array.Empty<ProjectileAuthoringDirtySourceView>()).Select(value => value.relativePath).Where(path => !string.IsNullOrEmpty(path)).Distinct().ToArray();
                session.PushComposite(draftPath, dirtyPaths, commands); ApplyProjectedWorkspace(result.projectedWorkspace); status.text = "已重新应用全部待处理命令"; RebuildRows(); RefreshSessionUi();
            });
        }

        private bool ExternalReplayActive => externalReplayIndex >= 0 && externalReplayIndex < externalReplayCommands.Length;

        private void ReplayNextExternalCommand()
        {
            if (!ExternalReplayActive) return;
            ProjectileAuthoringMutationRequestView current = CloneForExternalReplay(externalReplayCommands[externalReplayIndex]);
            ProjectileAuthoringMutationRequestView[] plan = externalReplayPrefix.Select(CloneMutation).Concat(new[] { current }).ToArray();
            PreviewMutationSequence(plan);
        }

        private void CompleteExternalReplayStep(ProjectileAuthoringMutationResultView result, string draftPath, string[] dirtyPaths, ProjectileAuthoringMutationRequestView[] confirmedSequence, ProjectileAuthoringMutationRequestView request)
        {
            externalReplayPrefix = confirmedSequence.Select(CloneMutation).ToArray();
            externalReplayIndex++;
            if (externalReplayIndex < externalReplayCommands.Length)
            {
                pendingMutation = null; pendingPlan = null; pendingSequence = Array.Empty<ProjectileAuthoringMutationRequestView>();
                if (impactPanel != null) impactPanel.style.display = DisplayStyle.None;
                status.text = "已处理 " + externalReplayIndex + "/" + externalReplayCommands.Length + " 条命令；正在校验下一条";
                ReplayNextExternalCommand();
                return;
            }

            ProjectileAuthoringWorkspaceView latest = pendingExternalWorkspace;
            externalReplayIndex = -1;
            externalReplayCommands = Array.Empty<ProjectileAuthoringMutationRequestView>();
            externalReplayPrefix = Array.Empty<ProjectileAuthoringMutationRequestView>();
            session.Reset(latest?.workspaceHash ?? string.Empty);
            session.PushComposite(draftPath, dirtyPaths, confirmedSequence);
            diskWorkspace = latest ?? diskWorkspace;
            pendingExternalWorkspace = null;
            session.MarkExternalChanged(false);
            if (conflictActions != null) conflictActions.style.display = DisplayStyle.None;
            ApplyProjectedWorkspace(result.projectedWorkspace);
            UpdateSelectionAfterMutation(request);
            status.text = "已重新应用全部待处理命令";
            CancelPendingMutation();
            RebuildRows();
            RefreshSessionUi();
        }

        private void RebuildDraftFromCurrentHistory(bool requireConfirmation)
        {
            ProjectileAuthoringMutationRequestView[] commands = session.PendingCommands.Select(CloneMutation).ToArray();
            if (commands.Length == 0)
            {
                session.UpdateCurrentDraft(string.Empty, Array.Empty<string>()); workspace = diskWorkspace; PopulateFilterChoices(); RebuildRows(); RenderProblems(); RefreshSessionUi(); return;
            }
            if (requireConfirmation) { PreviewMutationSequence(commands.Select(CloneForReplay).ToArray()); return; }
            service.MutateSequence(packagePath, commands, (result, draftPath, error) =>
            {
                if (disposed) return;
                if (!string.IsNullOrEmpty(error) || result == null || result.status != "Ready") { session.MarkExternalChanged(true); ShowError(error ?? "无法重建待处理命令序列。"); RefreshSessionUi(); return; }
                string[] dirtyPaths = (result.dirtySources ?? Array.Empty<ProjectileAuthoringDirtySourceView>()).Select(value => value.relativePath).Where(path => !string.IsNullOrEmpty(path)).Distinct().ToArray();
                session.UpdateCurrentDraft(draftPath, dirtyPaths); ApplyProjectedWorkspace(result.projectedWorkspace); status.text = "已恢复待处理命令"; RebuildRows(); RefreshSessionUi();
            });
        }

        private void ApplyProjectedWorkspace(ProjectileAuthoringWorkspaceView projected)
        {
            if (projected == null) return;
            workspace = projected; validationProblems = projected.diagnostics ?? Array.Empty<ProjectileAuthoringProblemView>(); transientProblems = Array.Empty<ProjectileAuthoringProblemView>(); PopulateFilterChoices(); RenderProblems(); RenderRuleInspector(); RefreshPreviewForSelection();
        }

        private static ProjectileAuthoringMutationRequestView CloneForReplay(ProjectileAuthoringMutationRequestView value)
        {
            ProjectileAuthoringMutationRequestView clone = CloneMutation(value); clone.planOnly = true; clone.confirmed = false; clone.confirmDestructive = false; clone.confirmationToken = string.Empty; clone.authorizedConsumerPaths = Array.Empty<string>(); return clone;
        }

        private static ProjectileAuthoringMutationRequestView CloneForExternalReplay(ProjectileAuthoringMutationRequestView value)
        {
            ProjectileAuthoringMutationRequestView clone = CloneForReplay(value);
            clone.workspaceToken = string.Empty;
            return clone;
        }

        private void CancelExternal() { status.text = "外部文件已更改 — 已保留草稿并阻止保存"; conflictActions.style.display = DisplayStyle.Flex; }

        private void RenderProblems(ProjectileAuthoringProblemView[] transient = null)
        {
            if (transient != null) transientProblems = transient;
            ProjectileAuthoringProblemView[] all = AllProblems().ToArray();
            if (problemsDrawer != null)
            {
                int errors = all.Count(item => string.Equals(item.severity, "Error", StringComparison.OrdinalIgnoreCase));
                problemsDrawer.text = "问题 " + all.Length + (errors > 0 ? " · " + errors + " 个错误" : string.Empty);
            }
            if (problems == null) return; problems.Clear(); string severity = severityFilter?.value ?? "All";
            foreach (ProjectileAuthoringProblemView item in all.Where(item => severity == "All" || string.Equals(item.severity, severity, StringComparison.OrdinalIgnoreCase)))
            {
                var row = new VisualElement { name = "problem-row" }; row.AddToClassList("pa-row"); row.Add(new Label(LocalizeUiValue(item.severity) + "  " + item.code + "  " + item.message + "  " + item.sourcePath + "  " + item.objectPath + "  " + item.fieldPath));
                row.Add(new Button(() => LocateProblem(item)) { text = "定位", name = "problem-locate" }); row.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = item.code + " " + item.message) { text = "复制", name = "problem-copy" }); problems.Add(row);
            }
            if (problems.childCount == 0) problems.Add(new Label("没有问题。"));
            RenderDiagnosticsInspector();
        }

        private void RenderDiagnosticsInspector()
        {
            if (diagnosticsInspectorRoot == null) return;
            diagnosticsInspectorRoot.Clear();
            string stableId = session.SelectedStableId ?? string.Empty;
            ProjectileAuthoringProblemView[] items = AllProblems().Where(item => string.IsNullOrEmpty(stableId)
                || (item.relatedStableIds?.Contains(stableId) ?? false)
                || (item.objectPath?.IndexOf(stableId, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0).ToArray();
            diagnosticsInspectorRoot.Add(new Label("当前条目诊断 · " + items.Length) { name = "diagnostics-inspector-title" });
            if (items.Length == 0)
            {
                diagnosticsInspectorRoot.Add(new HelpBox("当前条目没有诊断问题。", HelpBoxMessageType.Info));
                return;
            }
            foreach (ProjectileAuthoringProblemView item in items)
            {
                HelpBoxMessageType type = string.Equals(item.severity, "Error", StringComparison.OrdinalIgnoreCase)
                    ? HelpBoxMessageType.Error
                    : string.Equals(item.severity, "Warning", StringComparison.OrdinalIgnoreCase) ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
                diagnosticsInspectorRoot.Add(new HelpBox(item.code + "：" + item.message, type));
            }
        }

        private IEnumerable<ProjectileAuthoringProblemView> FilteredProblems()
        {
            string severity = severityFilter?.value ?? "All";
            return AllProblems().Where(item => severity == "All" || string.Equals(item.severity, severity, StringComparison.OrdinalIgnoreCase));
        }

        private IEnumerable<ProjectileAuthoringProblemView> AllProblems()
        {
            return (validationProblems ?? Array.Empty<ProjectileAuthoringProblemView>())
                .Concat(transientProblems ?? Array.Empty<ProjectileAuthoringProblemView>())
                .GroupBy(value => string.Join("\u001f", value.code ?? string.Empty, value.severity ?? string.Empty, value.message ?? string.Empty, value.sourcePath ?? string.Empty, value.objectPath ?? string.Empty, value.fieldPath ?? string.Empty, string.Join("\u001e", value.relatedStableIds ?? Array.Empty<string>())), StringComparer.Ordinal)
                .Select(group => group.First());
        }

        private void LocateProblem(ProjectileAuthoringProblemView problem)
        {
            (string stableId, bool? isRequest) = ResolveProblemEntity(problem);
            if (string.IsNullOrEmpty(stableId)) return;
            if (isRequest == true) EnsureRequestVisible(stableId);
            int index = rows.FindIndex(row => row.StableId == stableId && (!isRequest.HasValue || row.IsRequest == isRequest.Value));
            if (index < 0) return;
            list.SetSelection(index);
            list.ScrollToItem(index);
            FocusProblemField(problem.fieldPath);
        }

        private (string stableId, bool? isRequest) ResolveProblemEntity(ProjectileAuthoringProblemView problem)
        {
            bool? preferredRequest = ProblemFieldEntityKind(problem?.fieldPath);
            IEnumerable<string> exactIds = new[] { NormalizeProblemObjectPath(problem?.objectPath) }
                .Concat(problem?.relatedStableIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal);
            foreach (string id in exactIds)
            {
                bool hasDefinition = workspace?.definitions?.Any(value => string.Equals(value.stableId, id, StringComparison.Ordinal)) == true;
                bool hasRequest = workspace?.requests?.Any(value => string.Equals(value.stableId, id, StringComparison.Ordinal)) == true;
                if (preferredRequest == true && hasRequest) return (id, true);
                if (preferredRequest == false && hasDefinition) return (id, false);
                if (hasDefinition != hasRequest) return (id, hasRequest);
                if (hasDefinition) return (id, preferredRequest);
            }

            string sourcePath = problem?.sourcePath ?? string.Empty;
            ProjectileRequestVariantListItem request = workspace?.requests?.FirstOrDefault(value => string.Equals(value.sourcePath, sourcePath, StringComparison.Ordinal));
            ProjectileDefinitionListItem definition = workspace?.definitions?.FirstOrDefault(value => (value.sourcePaths ?? Array.Empty<string>()).Contains(sourcePath));
            if (preferredRequest == true && request != null) return (request.stableId, true);
            if (preferredRequest == false && definition != null) return (definition.stableId, false);
            if (request != null && definition == null) return (request.stableId, true);
            if (definition != null) return (definition.stableId, false);

            request = workspace?.requests?.FirstOrDefault(value => problem?.sourcePath?.Contains(value.stableId) == true);
            if (request != null) return (request.stableId, true);
            definition = workspace?.definitions?.FirstOrDefault(value => problem?.sourcePath?.Contains(value.stableId) == true);
            return definition == null ? (string.Empty, null) : (definition.stableId, false);
        }

        private static string NormalizeProblemObjectPath(string objectPath)
        {
            const string DefinitionPrefix = "definition:";
            const string RequestPrefix = "request:";
            if (objectPath?.StartsWith(DefinitionPrefix, StringComparison.Ordinal) == true) return objectPath.Substring(DefinitionPrefix.Length);
            if (objectPath?.StartsWith(RequestPrefix, StringComparison.Ordinal) == true) return objectPath.Substring(RequestPrefix.Length);
            return objectPath;
        }

        private static bool? ProblemFieldEntityKind(string fieldPath)
        {
            if (string.Equals(fieldPath, "projectileStableId", StringComparison.Ordinal)
                || (fieldPath?.StartsWith("origin.", StringComparison.Ordinal) ?? false)
                || (fieldPath?.StartsWith("aim.", StringComparison.Ordinal) ?? false)) return true;
            if (string.Equals(fieldPath, "physicalEventsEnabled", StringComparison.Ordinal)
                || (fieldPath?.StartsWith("motion.", StringComparison.Ordinal) ?? false)
                || (fieldPath?.StartsWith("collision.", StringComparison.Ordinal) ?? false)
                || (fieldPath?.StartsWith("impact.", StringComparison.Ordinal) ?? false)) return false;
            return null;
        }

        private void EnsureRequestVisible(string requestStableId)
        {
            ProjectileDefinitionListItem owner = workspace?.definitions?.FirstOrDefault(value => (value.requestIds ?? Array.Empty<string>()).Contains(requestStableId));
            if (owner == null || IsExpanded(owner.stableId)) return;
            var expanded = new HashSet<string>(session.ExpandedStableIds ?? Array.Empty<string>(), StringComparer.Ordinal) { owner.stableId };
            session.ExpandedStableIds = expanded.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            RebuildRows();
        }

        private void FocusProblemField(string fieldPath)
        {
            (string sectionName, string controlName) = ResolveProblemTarget(fieldPath);
            Foldout section = string.IsNullOrEmpty(sectionName) ? null : ruleInspectorRoot?.Q<Foldout>(sectionName);
            if (section != null) section.value = true;
            VisualElement control = string.IsNullOrEmpty(controlName) ? null : ruleInspectorRoot?.Q(controlName);
            for (VisualElement ancestor = control?.parent; ancestor != null; ancestor = ancestor.parent)
                if (ancestor is Foldout foldout) foldout.value = true;
            lastProblemFocusTarget = control?.name ?? section?.name ?? string.Empty;
            control?.Focus();
        }

        private static (string sectionName, string controlName) ResolveProblemTarget(string fieldPath)
        {
            switch (fieldPath ?? string.Empty)
            {
                case "physicalEventsEnabled": return ("definition-identity-section", "definition-physical-events");
                case "motion.kind": return ("definition-motion-section", "motion-kind");
                case "motion.speedPerFrame": return ("definition-motion-section", "motion-speed");
                case "motion.lifetimeFrames": return ("definition-motion-section", "motion-lifetime");
                case "motion.speedProfile.kind": return ("definition-motion-section", "motion-speed-profile");
                case "motion.arcHeight": return ("definition-motion-section", "motion-arc-height");
                case "motion.gravityPerFrameSquared": return ("definition-motion-section", "motion-gravity");
                case "motion.maxLaunchAngleDegrees": return ("definition-motion-section", "motion-max-launch-angle");
                case "motion.lateralAmplitude": return ("definition-motion-section", "motion-lateral-amplitude");
                case "motion.lateralCycles": return ("definition-motion-section", "motion-lateral-cycles");
                case "motion.bezierControlOffsetA": return ("definition-motion-section", "motion-bezier-a");
                case "motion.bezierControlOffsetB": return ("definition-motion-section", "motion-bezier-b");
                case "motion.homing.turnRatePerFrame": return ("definition-motion-section", "motion-homing-turn-rate");
                case "collision.radius": return ("definition-collision-section", "collision-radius");
                case "collision.maxAcceptedImpacts": return ("definition-collision-section", "collision-max-impacts");
                case "collision.hitSameTargetPolicy": return ("definition-collision-section", "collision-same-target");
                case "impact.policyKind": return ("definition-impact-section", "impact-policy");
                case "impact.areaRadius": return ("definition-impact-section", "impact-area-radius");
                case "impact.triggerOnExpire": return ("definition-impact-section", "impact-trigger-expire");
                case "impact.effectTimelineId": return ("definition-effect-timeline-section", "impact-effect-timeline");
                case "projectileStableId": return ("request-definition-section", "request-definition-reference");
                case "origin.kind": return ("request-origin-section", "request-origin-kind");
                case "origin.socketId": return ("request-origin-section", "request-known-socket");
                case "origin.localOffset": return ("request-origin-section", "request-local-offset");
                case "origin.fallbackRootOffset": return ("request-origin-section", "request-fallback-offset");
                case "aim.kind": return ("request-aim-section", "request-aim-kind");
                case "aim.maxRange": return ("request-aim-section", "request-aim-max-range");
                case "aim.targetHeightOffset": return ("request-aim-section", "request-target-height");
                case "aim.fallbackKind": return ("request-aim-section", "request-aim-fallback");
            }
            if ((fieldPath ?? string.Empty).StartsWith("motion.", StringComparison.Ordinal)) return ("definition-motion-section", string.Empty);
            if ((fieldPath ?? string.Empty).StartsWith("collision.", StringComparison.Ordinal)) return ("definition-collision-section", string.Empty);
            if ((fieldPath ?? string.Empty).StartsWith("impact.hitEvent.", StringComparison.Ordinal)) return ("direct-hit-section", ResolveHitEventControl(fieldPath, "impact.hitEvent.", "direct-hit"));
            if ((fieldPath ?? string.Empty).StartsWith("impact.areaHitEvent.", StringComparison.Ordinal)) return ("area-hit-section", ResolveHitEventControl(fieldPath, "impact.areaHitEvent.", "area-hit"));
            if ((fieldPath ?? string.Empty).StartsWith("impact.", StringComparison.Ordinal)) return ("definition-impact-section", string.Empty);
            const string SlotPrefix = "presentationBinding.slots.";
            if ((fieldPath ?? string.Empty).StartsWith(SlotPrefix, StringComparison.Ordinal))
            {
                string suffix = fieldPath.Substring(SlotPrefix.Length);
                int dot = suffix.IndexOf('.');
                string slot = dot < 0 ? suffix : suffix.Substring(0, dot);
                string leaf = dot < 0 ? string.Empty : suffix.Substring(dot + 1);
                string control = leaf == "resource" ? "resource" : leaf == "orientation" ? "orientation" : leaf == "localOffset" ? "offset" : leaf == "lifetimeFramesOverride" ? "lifetime" : string.Empty;
                return ("presentation-slot-" + slot, string.IsNullOrEmpty(control) ? string.Empty : "presentation-slot-" + slot + "-" + control);
            }
            const string ExitPrefix = "presentationBinding.exitPolicy.";
            if ((fieldPath ?? string.Empty).StartsWith(ExitPrefix, StringComparison.Ordinal))
            {
                string exit = fieldPath.Substring(ExitPrefix.Length);
                string control = exit == "acceptedImpact" ? "presentation-exit-accepted" : exit == "blockedImpact" ? "presentation-exit-blocked" : exit == "expiration" ? "presentation-exit-expiration" : exit == "externalRemoval" ? "presentation-exit-external" : "presentation-exit-reset";
                return ("presentation-exit-policy", control);
            }
            if (string.Equals(fieldPath, "presentationBinding", StringComparison.Ordinal)) return ("definition-presentation-section", "presentation-migrate-v1");
            if ((fieldPath ?? string.Empty).StartsWith("origin.", StringComparison.Ordinal)) return ("request-origin-section", string.Empty);
            if ((fieldPath ?? string.Empty).StartsWith("aim.", StringComparison.Ordinal)) return ("request-aim-section", string.Empty);
            return (string.Empty, string.Empty);
        }

        private static string ResolveHitEventControl(string fieldPath, string prefix, string controlPrefix)
        {
            string suffix = fieldPath.Substring(prefix.Length);
            switch (suffix)
            {
                case "targetTypes": return controlPrefix + "-targets";
                case "environmentHitPolicy": return controlPrefix + "-environment";
                case "stableEventId": return controlPrefix + "-stable-event";
                case "maxAcceptedHits": return controlPrefix + "-max-hits";
                case "hitSameTargetPolicy": return controlPrefix + "-same-target";
                case "damage.damageTypeId": return controlPrefix + "-damage-type";
                case "damage.baseDamage": return controlPrefix + "-base-damage";
                case "damage.impactPower": return controlPrefix + "-impact-power";
                case "damage.launchPower": return controlPrefix + "-launch-power";
                case "damage.analysisCategory": return controlPrefix + "-analysis-category";
                case "damage.tags": return controlPrefix + "-damage-tags";
                case "onHitBuffs": return controlPrefix + "-buffs";
                default: return string.Empty;
            }
        }

        private void RefreshSessionUi()
        {
            int count = session.DirtySources.Count; dirty.text = count + " 项未保存"; hasUnsavedChanges = count > 0; saveChangesMessage = "投射物创作中有未保存的草稿。";
            saveButton?.SetEnabled(session.CanSave && !(service?.IsBusy ?? false)); undoButton?.SetEnabled(session.CanUndo); redoButton?.SetEnabled(session.CanRedo);
        }

        public override void SaveChanges() { Save(); }
        public override void DiscardChanges()
        {
            discardRequested = true; hasUnsavedChanges = false; session.Reset(workspace?.workspaceHash ?? string.Empty); service?.Cancel();
            SessionState.EraseString("MxFramework.ProjectileAuthoring.State"); base.DiscardChanges();
        }

        private void RegisterLifecycle()
        {
            if (lifecycleRegistered) return; lifecycleRegistered = true; AssemblyReloadEvents.beforeAssemblyReload += BeforeReload; EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void UnregisterLifecycle()
        {
            if (!lifecycleRegistered) return; lifecycleRegistered = false; AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload; EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void BeforeReload() { SaveSessionState(); DisposePreview(); service?.Cancel(); }
        private void OnPlayModeChanged(PlayModeStateChange _) { SaveSessionState(); DisposePreview(); service?.Cancel(); }
        private void OnDisable() { if (!discardRequested) SaveSessionState(); DisposePreview(); UnregisterLifecycle(); service?.Cancel(); }
        private void OnDestroy() { disposed = true; DisposePreview(); UnregisterLifecycle(); service?.Dispose(); service = null; }
        private static void ShowError(string message) { Debug.LogError("[Projectile Authoring] " + message); }

        private void SaveSessionState()
        {
            session.Filter = search?.value ?? session.Filter ?? string.Empty;
            session.MotionFilter = motionFilter?.value ?? session.MotionFilter; session.ImpactFilter = impactFilter?.value ?? session.ImpactFilter; session.StatusFilter = statusFilter?.value ?? session.StatusFilter;
            float navigationWidth = outerSplit?.fixedPane?.resolvedStyle.width ?? 0f; if (navigationWidth > 0f) session.NavigationPaneSize = navigationWidth;
            float inspectorWidth = inspectorSplit?.fixedPane?.resolvedStyle.width ?? 0f; if (inspectorWidth > 0f) session.InspectorPaneSize = inspectorWidth;
            SessionState.SetString("MxFramework.ProjectileAuthoring.State", JsonUtility.ToJson(session.CaptureState()));
        }

        private void RestoreSessionState()
        {
            string json = SessionState.GetString("MxFramework.ProjectileAuthoring.State", string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            session.RestoreState(JsonUtility.FromJson<ProjectileAuthoringDocumentSession.SessionState>(json)); restoredSession = true;
        }

        private sealed class NavigationRow
        {
            public NavigationRow(string stableId, bool isRequest, bool isGeneratedOnly, string badges) { StableId = stableId; IsRequest = isRequest; IsGeneratedOnly = isGeneratedOnly; Badges = badges; }
            public string StableId { get; } public bool IsRequest { get; } public bool IsGeneratedOnly { get; } public string Badges { get; }
        }
    }
}
