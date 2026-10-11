using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestGuidanceLevelEditorModule :
        IMxLevelEditorModule,
        IMxLevelEditorDirtyDiagnostics,
        IMxLevelEditorSceneChangeAwareModule,
        IMxLevelEditorSceneScope,
        IMxLevelEditorSaveParticipant
    {
        private enum SaveCompletionState : byte
        {
            Idle = 0,
            Prepared = 1,
            Written = 2,
            Committed = 3,
            TailFailed = 4,
            PreCommitFailed = 5
        }

        private enum PreviewState : byte
        {
            Waiting = 0,
            Completed = 1,
            Rejected = 2
        }

        private enum PreviewLocale : byte
        {
            SourceZhCn = 0,
            English = 1
        }

        private const string ModuleName = "教学引导";
        private static readonly Color PanelBackground = new Color32(31, 34, 38, 255);
        private static readonly Color PanelHeaderBackground = new Color32(40, 43, 48, 255);
        private static readonly Color BorderColor = new Color32(17, 19, 22, 255);
        private static readonly Color MutedText = new Color32(143, 149, 157, 255);
        private static readonly Color Accent = new Color32(66, 139, 190, 255);
        private static readonly Color Success = new Color32(88, 184, 111, 255);
        private static readonly Color Warning = new Color32(221, 166, 72, 255);

        private const string InputActionsAssetPath =
            "Assets/Input/MxFramework/Config/MxFrameworkInputActions.inputactions";

        private readonly List<string> _actionChoices = LoadGameplayActionChoices();

        private MxLevelAuthoringDocument _envelope;
        private CharacterTestGuidanceAuthoringDocument _document;
        private CharacterTestGuidanceAuthoringReport _report;
        private CharacterTestLevelEditorDocumentUndoSession _undoSession;
        private MxLevelEditorWindow _window;
        private VisualElement _moduleToolbarRow;
        private VisualElement _authoringLayout;
        private HelpBox _readOnlyHelp;
        private VisualElement _editorHost;
        private VisualElement _previewHost;
        private ListView _stepList;
        private string _selectedStableId = string.Empty;
        private PreviewState _previewState;
        private PreviewLocale _previewLocale;
        private LocalizationTsvLoadResult _localization;
        private bool _dirty;
        private bool _authoringEditable = true;
        private CharacterTestGuidanceRuntimeDiagnostic _loadDiagnostic;
        private string _status = string.Empty;
        private string _documentPathOverride;
        private readonly CharacterTestLevelEditorModuleDocumentIdentity _documentIdentity = new();
        private SaveCompletionState _saveState;
        private bool _dirtyBeforePreparedSave;
        private CharacterTestGuidancePlan _preparedPlan;
        private CharacterTestGuidanceSceneCache _cacheBeforePreparedSave;
        private bool _cacheApplied;

        public string ModuleId => CharacterTestGuidanceAuthoringAdapter.ModuleId;
        public string DisplayName => ModuleName;
        public string DocumentPath => string.IsNullOrWhiteSpace(_documentPathOverride)
            ? CharacterTestWorldObjectLevelEditorModule.ResolveActiveSceneDocumentPath()
            : _documentPathOverride;
        public bool IsDirty => _dirty;
        public bool IsDocumentDirty => _dirty;
        public bool IsManagedSceneDirty => false;
        public string Status => _status;
        public Transform ManagedSceneRoot => FindGameManager()?.WorldObjectSceneRoot;

        public bool AppliesToActiveScene =>
            CharacterTestWorldObjectLevelEditorModule.HasActiveSceneAuthoringTarget(_documentPathOverride);

        internal CharacterTestGuidanceAuthoringDocument DocumentForTests => _document;
        internal bool AuthoringEditableForTests => _authoringEditable;
        internal string SelectedStableIdForTests => _selectedStableId;
        internal string PreviewStateForTests => _previewState.ToString();
        internal string SaveStateForTests => _saveState.ToString();

        internal void SetDocumentPathForTests(string documentPath)
        {
            if (_document != null)
                throw new InvalidOperationException("Test document path must be set before loading.");
            _documentPathOverride = documentPath;
        }

        internal void MutateForTests(
            string label,
            Action<CharacterTestGuidanceAuthoringDocument> mutation)
        {
            EnsureLoaded();
            Mutate(label, () => mutation(_document));
        }

        internal void ResetUndoBaselineForTests()
        {
            EnsureLoaded();
            EnsureUndoSession();
            _undoSession.ResetBaseline();
            _dirty = false;
        }

        internal void SelectStepForTests(string stableId)
        {
            _selectedStableId = stableId ?? string.Empty;
        }

        internal void SelectCompletedPreviewForTests()
        {
            _previewState = PreviewState.Completed;
            RebuildPreview();
        }

        internal void SelectEnglishPreviewForTests()
        {
            _previewLocale = PreviewLocale.English;
            RebuildPreview();
        }

        internal void AddBlankStepForTests() => AddBlankStep();
        internal void AddPresetStepForTests(string presetId) => AddPresetStep(presetId);
        internal void ChangeSelectedCompletionTypeForTests(string typeId) =>
            ChangeSelectedCompletionType(typeId);
        internal void DuplicateSelectedStepForTests() => DuplicateSelectedStep();
        internal void DeleteSelectedStepForTests() => DeleteSelectedStep();
        internal void MoveSelectedStepForTests(int delta) => MoveSelectedStep(delta);

        public void OnEnable()
        {
            EnsureLoaded();
            EnsureUndoSession();
        }

        public void BuildModuleToolbar(MxLevelEditorWindow window, VisualElement toolbar)
        {
            EnsureLoaded();
            var row = new VisualElement
            {
                name = "character-test-guidance-module-toolbar"
            };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0;
            row.style.minHeight = 34;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.backgroundColor = PanelHeaderBackground;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = BorderColor;

            var enabled = new Toggle("启用教学") { value = _document.Enabled };
            enabled.tooltip = "关闭后本关不会显示教学引导；允许零步骤，但未配置草稿仍会阻止保存。";
            enabled.style.width = 118;
            enabled.style.marginRight = 10;
            enabled.RegisterValueChangedCallback(evt =>
                Mutate("切换教学引导启用状态", () => _document.Enabled = evt.newValue));
            row.Add(enabled);

            row.Add(CreateBlankStepButton("module-toolbar", 94f));
            row.Add(CreatePresetMenu("module-toolbar", 112f));
            row.Add(CreateToolbarButton("复制", DuplicateSelectedStep));
            row.Add(CreateToolbarButton("删除", DeleteSelectedStep));
            row.Add(CreateToolbarButton("上移", () => MoveSelectedStep(-1)));
            row.Add(CreateToolbarButton("下移", () => MoveSelectedStep(1)));
            _moduleToolbarRow = row;
            row.SetEnabled(_authoringEditable);
            toolbar.Add(row);
        }

        public VisualElement CreateContent(MxLevelEditorWindow window)
        {
            _window = window;
            EnsureLoaded();
            EnsureSelectedStep();

            var root = new VisualElement { name = "character-test-guidance-authoring" };
            root.style.flexGrow = 1;
            root.style.minWidth = 0;
            root.style.minHeight = 0;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = (Color)new Color32(28, 31, 34, 255);

            _readOnlyHelp = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            _readOnlyHelp.name = "character-test-guidance-read-only-diagnostic";
            root.Add(_readOnlyHelp);

            var outer = new TwoPaneSplitView(
                0,
                245f,
                TwoPaneSplitViewOrientation.Horizontal);
            outer.style.flexGrow = 1;
            outer.style.minWidth = 0;
            outer.style.minHeight = 0;
            root.Add(outer);
            _authoringLayout = outer;
            RefreshEditabilityProjection();

            outer.Add(BuildStepListPane());
            var right = new TwoPaneSplitView(
                1,
                355f,
                TwoPaneSplitViewOrientation.Horizontal);
            right.style.flexGrow = 1;
            right.style.minWidth = 650f;
            right.style.minHeight = 0;
            outer.Add(right);

            _editorHost = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "character-test-guidance-step-editor",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            StylePane(_editorHost, 390f);
            right.Add(_editorHost);

            _previewHost = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "character-test-guidance-semantic-preview",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            StylePane(_previewHost, 310f);
            right.Add(_previewHost);

            RebuildEditorAndPreview();
            return root;
        }

        public void OpenJson()
        {
            string fullPath = ToFullPath(DocumentPath);
            if (!File.Exists(fullPath))
            {
                _status = "文档不存在：" + DocumentPath;
                return;
            }
            Object asset = AssetDatabase.LoadAssetAtPath<TextAsset>(DocumentPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else
            {
                EditorUtility.RevealInFinder(fullPath);
            }
        }

        public void Reload()
        {
            LoadFromDisk();
            _dirty = false;
            _saveState = SaveCompletionState.Idle;
            EnsureUndoSession();
            _undoSession.ResetBaseline();
            EnsureSelectedStep();
            RefreshAll("已重新加载教学引导 Authoring。");
        }

        public void Save()
        {
            MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                string.IsNullOrWhiteSpace(_documentPathOverride)
                    ? MxLevelEditorModuleRegistry.Modules
                    : new IMxLevelEditorModule[] { this },
                out _status);
        }

        public void Validate()
        {
            if (!_authoringEditable)
            {
                _status = "教学引导 Authoring 无法编辑：" + _loadDiagnostic.Code + " · "
                    + _loadDiagnostic.Message;
                RebuildPreview();
                return;
            }
            _report = CharacterTestGuidanceAuthoringValidator.Validate(_document);
            _status = "校验完成：错误=" + _report.ErrorCount + "，警告=" + _report.WarningCount + "。";
            RebuildPreview();
        }

        public void ApplyToScene()
        {
            _status = "教学引导缓存由“保存全部”统一提交；未执行独立 Scene 写入。";
        }

        public void SelectRootSource()
        {
            GameManager manager = FindGameManager();
            if (manager == null)
            {
                _status = "当前场景中未找到 CharacterTest GameManager。";
                return;
            }
            Selection.activeObject = manager.gameObject;
            EditorGUIUtility.PingObject(manager.gameObject);
        }

        public void CollectManagedSceneTransforms(List<Transform> output)
        {
            // Guidance owns no Transform. Its cache is serialized on GameManager.
        }

        public bool NotifySceneTransformChanged(Transform transform) => false;
        public void RefreshUnregisteredSceneObjects(IReadOnlyList<Transform> allManagedTransforms) { }
        public void AcknowledgeManagedSceneSaved() { }

        bool IMxLevelEditorSaveParticipant.TryPrepareSave(out string failureMessage)
        {
            EnsureLoaded();
            string sourcePath = ToFullPath(DocumentPath);
            _documentIdentity.RequireSave(DocumentPath, EditorSceneManager.GetActiveScene().path, _envelope,
                File.Exists(sourcePath) ? File.ReadAllText(sourcePath) : null, ModuleName);
            if (CharacterTestJoinedLevelSaveContext.TryGet(DocumentPath, out var joined)) _envelope = joined;
            _dirtyBeforePreparedSave = _dirty;
            _cacheApplied = false;
            if (!_authoringEditable)
            {
                _saveState = SaveCompletionState.PreCommitFailed;
                failureMessage = "教学引导 Authoring 无法保存：" + _loadDiagnostic.Code + " · "
                    + _loadDiagnostic.Message;
                return false;
            }
            if (!CharacterTestGuidanceCompiler.TryCompile(
                    _document,
                    out _preparedPlan,
                    out _report))
            {
                _saveState = SaveCompletionState.PreCommitFailed;
                failureMessage = "教学引导 Authoring 校验失败，未写入文档。错误="
                    + _report.ErrorCount + "。";
                return false;
            }
            GameManager manager = FindGameManager();
            if (manager == null || manager.WorldObjectSceneRoot == null)
            {
                _saveState = SaveCompletionState.PreCommitFailed;
                failureMessage = "当前受管场景中未找到 CharacterTest GameManager 场景根。";
                return false;
            }
            _cacheBeforePreparedSave = manager.GuidanceSceneCache.Clone();
            _saveState = SaveCompletionState.Prepared;
            failureMessage = string.Empty;
            return true;
        }

        void IMxLevelEditorSaveParticipant.SavePreparedChanges()
        {
            if (_saveState != SaveCompletionState.Prepared || _preparedPlan == null)
                throw new InvalidOperationException("教学引导保存参与者尚未完成 prepare。");

            string fullPath = ToFullPath(DocumentPath);
            MxLevelAuthoringDocument latest = File.Exists(fullPath)
                ? MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath))
                : _envelope;
            _envelope = CharacterTestGuidanceAuthoringAdapter.Upsert(latest, _document);
            WriteEnvelope(DocumentPath, _envelope);
            AssetDatabase.ImportAsset(DocumentPath);

            GameManager manager = FindGameManager()
                ?? throw new InvalidOperationException("CharacterTest GameManager disappeared during save.");
            Undo.RecordObject(manager, "更新教学引导 Scene Cache");
            var cache = new CharacterTestGuidanceSceneCache();
            cache.Configure(_preparedPlan);
            manager.SetGuidanceSceneCache(cache);
            EditorUtility.SetDirty(manager);
            _cacheApplied = true;
            _saveState = SaveCompletionState.Written;
        }

        void IMxLevelEditorSaveParticipant.CompleteCoordinatedSave(bool succeeded, string message)
        {
            if (succeeded)
            {
                if (_saveState != SaveCompletionState.Written)
                    return;
                _saveState = SaveCompletionState.Committed;
                _dirty = false;
                _cacheApplied = false;
                _documentIdentity.AcknowledgeSource(File.ReadAllText(ToFullPath(DocumentPath)));
                EnsureUndoSession();
                _undoSession.ResetBaseline();
                RefreshAll(message);
                return;
            }

            if (_saveState == SaveCompletionState.Committed
                || _saveState == SaveCompletionState.TailFailed)
            {
                _saveState = SaveCompletionState.TailFailed;
                _dirty = false;
                RefreshAll(message + " 教学数据与缓存已提交；未执行 post-commit 回滚。");
                return;
            }

            if (_cacheApplied)
            {
                GameManager manager = FindGameManager();
                if (manager != null)
                {
                    manager.SetGuidanceSceneCache(
                        _cacheBeforePreparedSave?.Clone() ?? new CharacterTestGuidanceSceneCache());
                    EditorUtility.SetDirty(manager);
                }
            }
            _cacheApplied = false;
            _preparedPlan = null;
            _saveState = SaveCompletionState.PreCommitFailed;
            _dirty = _dirtyBeforePreparedSave;
            RefreshAll(message);
        }

        private VisualElement BuildStepListPane()
        {
            var pane = new VisualElement { name = "character-test-guidance-step-list-pane" };
            StylePane(pane, 220f);
            pane.style.flexGrow = 1;
            pane.Add(CreatePanelHeader(
                "有序步骤",
                _document.Steps.Count + (_document.Enabled ? " · 根启用" : " · 根关闭")));

            var tools = new VisualElement();
            tools.style.flexDirection = FlexDirection.Row;
            tools.style.paddingLeft = 7;
            tools.style.paddingRight = 7;
            tools.style.paddingTop = 7;
            tools.style.paddingBottom = 7;
            tools.style.borderBottomWidth = 1;
            tools.style.borderBottomColor = BorderColor;
            tools.Add(CreateBlankStepButton("step-list", 90f));
            tools.Add(CreatePresetMenu("step-list", 106f));
            pane.Add(tools);

            _stepList = new ListView(
                _document.Steps,
                52f,
                MakeStepRow,
                BindStepRow)
            {
                name = "character-test-guidance-step-list",
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight
            };
            _stepList.style.flexGrow = 1;
            _stepList.style.minHeight = 0;
            _stepList.selectionChanged += OnStepSelectionChanged;
            int selectedIndex = IndexOfStep(_selectedStableId);
            if (selectedIndex >= 0)
                _stepList.SetSelectionWithoutNotify(new[] { selectedIndex });
            pane.Add(_stepList);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.paddingLeft = 6;
            footer.style.paddingRight = 6;
            footer.style.paddingTop = 6;
            footer.style.paddingBottom = 6;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = BorderColor;
            footer.Add(CreateCompactButton("复制", DuplicateSelectedStep));
            footer.Add(CreateCompactButton("删除", DeleteSelectedStep));
            footer.Add(CreateCompactButton("↑", () => MoveSelectedStep(-1)));
            footer.Add(CreateCompactButton("↓", () => MoveSelectedStep(1)));
            pane.Add(footer);
            return pane;
        }

        private VisualElement MakeStepRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = (Color)new Color32(255, 255, 255, 9);
            var index = new Label { name = "index" };
            index.style.width = 26;
            index.style.color = MutedText;
            row.Add(index);
            var text = new VisualElement();
            text.style.flexGrow = 1;
            text.style.minWidth = 0;
            var title = new Label { name = "title" };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            var detail = new Label { name = "detail" };
            detail.style.fontSize = 10;
            detail.style.color = MutedText;
            text.Add(title);
            text.Add(detail);
            row.Add(text);
            var enabled = new Label { name = "enabled" };
            enabled.style.width = 14;
            enabled.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(enabled);
            return row;
        }

        private void BindStepRow(VisualElement row, int index)
        {
            if (index < 0 || index >= _document.Steps.Count)
                return;
            CharacterTestGuidanceStepAuthoring step = _document.Steps[index];
            row.Q<Label>("index").text = (index + 1).ToString("00");
            row.Q<Label>("title").text = string.IsNullOrWhiteSpace(step.Content?.Title?.Fallback)
                ? "未命名步骤"
                : step.Content.Title.Fallback;
            row.Q<Label>("detail").text = CompletionDisplayName(step.Completion?.TypeId)
                + "  ·  " + step.StableId;
            Label enabled = row.Q<Label>("enabled");
            enabled.text = step.Enabled ? "●" : "○";
            enabled.style.color = step.Enabled ? Success : MutedText;
        }

        private void OnStepSelectionChanged(IEnumerable<object> selected)
        {
            foreach (object item in selected)
            {
                if (item is CharacterTestGuidanceStepAuthoring step)
                    _selectedStableId = step.StableId;
                break;
            }
            _previewState = PreviewState.Waiting;
            RebuildEditorAndPreview();
        }

        private void RebuildEditorAndPreview()
        {
            RebuildEditor();
            RebuildPreview();
        }

        private void RebuildEditor()
        {
            if (_editorHost == null)
                return;
            _editorHost.Clear();
            _editorHost.Add(CreatePanelHeader("步骤编辑", SelectedStepIndexLabel()));
            CharacterTestGuidanceStepAuthoring step = SelectedStep();
            if (step == null)
            {
                _editorHost.Add(CreateEmptyState("选择左侧步骤开始编辑。"));
                return;
            }

            VisualElement header = CreateCard();
            var title = new Label(string.IsNullOrWhiteSpace(step.Content.Title.Fallback)
                ? "未命名步骤"
                : step.Content.Title.Fallback);
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);
            var identity = new Label(step.StableId + "  ·  "
                + CompletionDisplayName(step.Completion.TypeId));
            identity.style.color = MutedText;
            identity.style.marginTop = 3;
            header.Add(identity);
            _editorHost.Add(header);

            VisualElement identitySection = CreateSection("步骤设置");
            identitySection.Add(CreateDelayedTextField(
                "Stable ID", step.StableId,
                value => CharacterTestGuidanceAuthoringAdapter.ChangeStableId(step, value),
                "修改教学步骤 Stable ID"));
            identitySection.Add(CreateCompletionField(step));
            identitySection.Add(CreateToggle(
                "启用此步骤", step.Enabled,
                value => step.Enabled = value,
                "切换教学步骤启用状态"));
            identitySection.Add(CreateActionField(step));
            var advance = new DoubleField("完成后等待（秒）") { value = step.AutoAdvanceSeconds };
            advance.RegisterValueChangedCallback(evt => Mutate(
                "修改教学步骤自动前进时间",
                () => step.AutoAdvanceSeconds = evt.newValue));
            identitySection.Add(advance);
            _editorHost.Add(identitySection);

            VisualElement content = CreateSection("Guidance Card 文案");
            AddLocalizedTextEditor(content, "标题", step.Content.Title, "修改教学标题");
            AddLocalizedTextEditor(content, "操作说明", step.Content.ActionInstruction, "修改教学操作说明", true);
            AddLocalizedTextEditor(content, "观察重点", step.Content.Focus, "修改教学观察重点", true);
            AddLocalizedTextEditor(content, "等待状态", step.Content.WaitingStatus, "修改教学等待状态", true);
            AddLocalizedTextEditor(content, "完成状态", step.Content.CompletedStatus, "修改教学完成状态", true);
            AddLocalizedTextEditor(content, "成功通知", step.Content.SuccessNotice, "修改教学成功通知", true);
            content.Add(CreateLocalizationAdvancedFoldout(step));
            _editorHost.Add(content);

            VisualElement parameters = CreateSection("Typed Completion 参数");
            parameters.Add(CreateReadOnlyRow(
                "Type ID",
                string.IsNullOrWhiteSpace(step.Completion.TypeId)
                    ? "<未选择>"
                    : step.Completion.TypeId));
            parameters.Add(CreateReadOnlyRow("Schema", "v" + step.Completion.SchemaVersion));
            BuildCompletionFields(parameters, step);
            var typeNote = new HelpBox(
                "完成条件可随时切换；切换只重置 typed 参数，保留文案、Input Action、等待时间、Stable ID 与步骤位置。",
                HelpBoxMessageType.Info);
            typeNote.style.marginTop = 7;
            parameters.Add(typeNote);
            _editorHost.Add(parameters);
        }

        private void RebuildPreview()
        {
            if (_previewHost == null)
                return;
            _previewHost.Clear();
            _previewHost.Add(CreatePanelHeader("语义预览", "不加载 FairyGUI"));

            CharacterTestGuidanceStepAuthoring step = SelectedStep();
            if (step == null)
            {
                _previewHost.Add(CreateEmptyState("选择步骤后显示 Guidance Card 语义结构。"));
                return;
            }

            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.paddingLeft = 8;
            tabs.style.paddingRight = 8;
            tabs.style.paddingTop = 8;
            tabs.style.paddingBottom = 4;
            tabs.Add(CreatePreviewStateButton("等待", PreviewState.Waiting));
            tabs.Add(CreatePreviewStateButton("完成", PreviewState.Completed));
            if (string.Equals(
                    step.Completion.TypeId,
                    CharacterTestGuidanceCompletionRegistry.TargetLockState,
                    StringComparison.Ordinal))
            {
                tabs.Add(CreatePreviewStateButton("拒绝警告", PreviewState.Rejected));
            }
            else if (_previewState == PreviewState.Rejected)
            {
                _previewState = PreviewState.Waiting;
            }
            _previewHost.Add(tabs);

            var localeTabs = new VisualElement();
            localeTabs.style.flexDirection = FlexDirection.Row;
            localeTabs.style.paddingLeft = 8;
            localeTabs.style.paddingRight = 8;
            localeTabs.style.paddingBottom = 4;
            localeTabs.Add(CreatePreviewLocaleButton("简中源文案", PreviewLocale.SourceZhCn));
            localeTabs.Add(CreatePreviewLocaleButton("English", PreviewLocale.English));
            _previewHost.Add(localeTabs);

            VisualElement card = CreateGuidancePreviewCard(step);
            _previewHost.Add(card);
            _previewHost.Add(CreatePreviewLegend());
            BuildDiagnostics(_previewHost, step);
        }

        private VisualElement CreateGuidancePreviewCard(CharacterTestGuidanceStepAuthoring step)
        {
            VisualElement card = CreateCard();
            card.style.marginTop = 4;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = _previewState == PreviewState.Completed
                ? Success
                : _previewState == PreviewState.Rejected
                    ? Warning
                    : Accent;
            var eyebrow = new Label("GUIDANCE CARD  ·  " + SelectedStepIndexLabel());
            eyebrow.style.fontSize = 9;
            eyebrow.style.color = MutedText;
            eyebrow.style.letterSpacing = 0.8f;
            card.Add(eyebrow);
            string titleFormat = ResolveCatalogText(
                "ui.character_test.guidance.sequence_title.format",
                "测试 {0}/{1}：{2}");
            var title = new Label(SafeFormat(
                titleFormat,
                (IndexOfStep(step.StableId) + 1).ToString(),
                _document.Steps.Count.ToString(),
                ResolvePreviewText(step.Content.Title)));
            title.style.fontSize = 15;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginTop = 5;
            title.style.marginBottom = 8;
            card.Add(title);

            string binding = _previewLocale == PreviewLocale.English
                ? "<current binding: " + step.InputActionId + ">"
                : "〈" + step.InputActionId + " 当前绑定〉";
            string action = SafeFormat(
                ResolveCatalogText(
                    "ui.character_test.guidance.action_prompt.format",
                    "按 {0}：{1}；{2} 上一项，{3} 下一项"),
                binding,
                ResolvePreviewText(step.Content.ActionInstruction),
                "[",
                "]");
            card.Add(CreatePreviewLine(
                ResolveCatalogText("ui.guidance.label.action", "步骤：").TrimEnd('：', ':'),
                action,
                Accent));
            card.Add(CreatePreviewLine(
                ResolveCatalogText("ui.guidance.label.focus", "观察：").TrimEnd('：', ':'),
                ResolvePreviewText(step.Content.Focus),
                new Color32(107, 176, 204, 255)));
            string stateText;
            Color stateColor;
            if (_previewState == PreviewState.Completed)
            {
                stateText = ResolvePreviewText(step.Content.CompletedStatus);
                stateColor = Success;
            }
            else if (_previewState == PreviewState.Rejected)
            {
                stateText = ResolveCatalogText(
                    "ui.character_test.guidance.target_test.rejected",
                    "未完成：当前目标不可锁定，请更换目标重试");
                stateColor = Warning;
            }
            else
            {
                stateText = ResolvePreviewText(step.Content.WaitingStatus);
                stateColor = MutedText;
            }
            card.Add(CreatePreviewLine(
                ResolveCatalogText("ui.guidance.label.status", "状态：").TrimEnd('：', ':'),
                stateText,
                stateColor));

            if (_previewState == PreviewState.Completed)
            {
                var notice = new Label("✓  " + ResolvePreviewText(step.Content.SuccessNotice));
                notice.style.marginTop = 10;
                notice.style.paddingLeft = 9;
                notice.style.paddingRight = 9;
                notice.style.paddingTop = 7;
                notice.style.paddingBottom = 7;
                notice.style.backgroundColor = new Color(0.18f, 0.42f, 0.24f, 0.45f);
                notice.style.color = (Color)new Color32(183, 235, 195, 255);
                notice.style.borderLeftWidth = 2;
                notice.style.borderLeftColor = Success;
                card.Add(notice);
            }
            else if (_previewState == PreviewState.Rejected)
            {
                var notice = new Label("!  " + ResolveCatalogText(
                    "ui.character_test.guidance.target_rejected",
                    "当前无法锁定目标"));
                notice.style.marginTop = 10;
                notice.style.paddingLeft = 9;
                notice.style.paddingRight = 9;
                notice.style.paddingTop = 7;
                notice.style.paddingBottom = 7;
                notice.style.backgroundColor = new Color(0.46f, 0.31f, 0.12f, 0.45f);
                notice.style.color = (Color)new Color32(244, 211, 149, 255);
                notice.style.borderLeftWidth = 2;
                notice.style.borderLeftColor = Warning;
                card.Add(notice);
            }
            return card;
        }

        private void BuildDiagnostics(
            VisualElement root,
            CharacterTestGuidanceStepAuthoring selectedStep)
        {
            VisualElement section = CreateSection("诊断");
            CharacterTestGuidanceAuthoringReport report =
                CharacterTestGuidanceAuthoringValidator.Validate(_document);
            _report = report;
            if (report.Issues.Count == 0)
            {
                section.Add(new HelpBox(
                    "Authoring schema、typed completion 与启用序列校验通过。",
                    HelpBoxMessageType.Info));
            }
            else
            {
                for (int i = 0; i < report.Issues.Count; i++)
                {
                    CharacterTestGuidanceAuthoringIssue issue = report.Issues[i];
                    if (selectedStep != null
                        && issue.Path.StartsWith("steps[", StringComparison.Ordinal)
                        && !issue.Path.StartsWith(
                            "steps[" + IndexOfStep(selectedStep.StableId) + "]",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    section.Add(new HelpBox(
                        issue.Code + " | " + issue.Path + "\n" + issue.Message,
                        issue.Severity == CharacterTestGuidanceAuthoringSeverity.Error
                            ? HelpBoxMessageType.Error
                            : HelpBoxMessageType.Warning));
                    string issuePath = issue.Path;
                    section.Add(new Button(() => {
                        if (!RevealLevelIssue(_window, issuePath)) _status = "该路径暂未支持自动字段定位：" + issuePath;
                        _window?.RepaintRequested(); }) { text = "定位步骤与已支持字段", name = "mx-guidance-locate-" + i });
                }
            }

            AddLocalizationDiagnostics(section, selectedStep);

            GameManager manager = FindGameManager();
            string cacheState = "GameManager 未找到";
            if (manager != null)
            {
                if (CharacterTestGuidanceCompiler.TryCompile(
                        _document,
                        out CharacterTestGuidancePlan plan,
                        out _))
                {
                    cacheState = string.Equals(
                        manager.GuidanceSceneCache.GuidanceModuleSourceHash,
                        plan.SourceHash,
                        StringComparison.Ordinal)
                            ? "最新"
                            : "待保存 / hash 不一致";
                }
                else
                {
                    cacheState = "Authoring 非法，未编译";
                }
            }
            var cache = new Label("Scene Cache：" + cacheState);
            cache.style.marginTop = 6;
            cache.style.color = cacheState == "最新" ? Success : Warning;
            section.Add(cache);
            var boundary = new Label("预览边界：只验证信息结构，不加载 FairyGUI package，不推进 Gameplay。 ");
            boundary.style.marginTop = 5;
            boundary.style.color = MutedText;
            boundary.style.whiteSpace = WhiteSpace.Normal;
            section.Add(boundary);
            root.Add(section);
        }

        private void BuildCompletionFields(
            VisualElement root,
            CharacterTestGuidanceStepAuthoring step)
        {
            JObject payload = step.Completion?.Payload ?? new JObject();
            switch (step.Completion.TypeId)
            {
                case CharacterTestGuidanceCompletionRegistry.MoveDistanceAndStop:
                    root.Add(CreateIntegerPayloadField(
                        "最小距离（mm）", payload, "minimumDistanceMillimeters", 2000,
                        "修改移动完成距离"));
                    root.Add(CreateIntegerPayloadField(
                        "最大停步速度（mm/s）", payload,
                        "maximumStopSpeedMillimetersPerSecond", 150,
                        "修改停步速度阈值"));
                    root.Add(CreateBooleanPayloadField(
                        "要求 Grounded", payload, "requireGrounded", true,
                        "修改移动 Grounded 要求"));
                    break;
                case CharacterTestGuidanceCompletionRegistry.TargetLockState:
                    root.Add(CreateBooleanPayloadField(
                        "期望已锁定", payload, "expectedLocked", true,
                        "修改目标锁定预期"));
                    break;
                case CharacterTestGuidanceCompletionRegistry.BasicAttackDamage:
                    root.Add(CreateIntegerPayloadField(
                        "CharacterAction ID", payload, "actionId",
                        CharacterTestCharacterActionConfigSource.LightAttackActionId.Value,
                        "修改攻击 CharacterAction ID"));
                    root.Add(CreateIntegerPayloadField(
                        "最小最终伤害", payload, "minimumFinalDamage", 1,
                        "修改最小最终伤害"));
                    break;
                case CharacterTestGuidanceCompletionRegistry.JumpAndLand:
                case CharacterTestGuidanceCompletionRegistry.CameraModeChanged:
                    root.Add(CreateReadOnlyRow("参数", "无"));
                    break;
                default:
                    root.Add(new HelpBox(
                        string.IsNullOrWhiteSpace(step.Completion.TypeId)
                            ? "尚未选择完成条件；空白草稿补全前不能正式保存。"
                            : "当前完成条件不受支持；请从上方下拉框选择一个正式条件进行修复。",
                        HelpBoxMessageType.Error));
                    break;
            }
        }

        private DropdownField CreateCompletionField(CharacterTestGuidanceStepAuthoring step)
        {
            var choices = new List<string>();
            for (int i = 0; i < CharacterTestGuidanceCompletionRegistry.TypeIds.Count; i++)
            {
                choices.Add(CompletionDisplayName(
                    CharacterTestGuidanceCompletionRegistry.TypeIds[i]));
            }
            string currentDisplayName = CompletionDisplayName(step.Completion?.TypeId);
            var field = new DropdownField(
                "完成条件",
                choices,
                0)
            {
                name = "character-test-guidance-completion-condition"
            };
            int currentIndex = choices.IndexOf(currentDisplayName);
            field.index = currentIndex;
            if (currentIndex < 0)
            {
                field.formatSelectedValueCallback = value =>
                    string.IsNullOrWhiteSpace(value) ? currentDisplayName : value;
            }
            field.tooltip = "每个步骤使用一个 Runtime 可验证的完成条件；可随时切换。";
            field.RegisterValueChangedCallback(evt =>
            {
                if (!TryGetCompletionTypeId(evt.newValue, out string typeId)
                    || string.Equals(step.Completion?.TypeId, typeId, StringComparison.Ordinal))
                {
                    return;
                }
                _previewState = PreviewState.Waiting;
                Mutate(
                    "切换教学步骤完成条件",
                    () => CharacterTestGuidanceAuthoringAdapter.ChangeCompletionType(step, typeId));
            });
            return field;
        }

        private VisualElement CreateActionField(CharacterTestGuidanceStepAuthoring step)
        {
            var choices = new List<string>(_actionChoices);
            if (!choices.Contains(step.InputActionId))
                choices.Add(step.InputActionId);
            var field = new DropdownField("Gameplay Input Action", choices, step.InputActionId);
            field.name = "character-test-guidance-input-action";
            field.RegisterValueChangedCallback(evt => Mutate(
                "修改教学步骤输入动作",
                () => step.InputActionId = evt.newValue));
            return field;
        }

        private void AddLocalizedTextEditor(
            VisualElement root,
            string label,
            CharacterTestGuidanceTextAuthoring text,
            string undoLabel,
            bool multiline = false)
        {
            var group = new VisualElement();
            group.style.marginTop = 5;
            group.style.paddingLeft = 7;
            group.style.paddingRight = 7;
            group.style.paddingTop = 6;
            group.style.paddingBottom = 7;
            group.style.backgroundColor = (Color)new Color32(24, 27, 30, 255);
            group.style.borderLeftWidth = 2;
            group.style.borderLeftColor = (Color)new Color32(54, 64, 72, 255);
            var heading = new Label(label);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginBottom = 3;
            group.Add(heading);
            var fallback = new TextField("简中源文案")
            {
                value = text.Fallback ?? string.Empty,
                multiline = multiline,
                isDelayed = true
            };
            fallback.RegisterValueChangedCallback(evt => Mutate(
                undoLabel,
                () => text.Fallback = evt.newValue));
            group.Add(fallback);
            root.Add(group);
        }

        private VisualElement CreateLocalizationAdvancedFoldout(
            CharacterTestGuidanceStepAuthoring step)
        {
            var foldout = new Foldout
            {
                text = "本地化 / 高级（Key 由系统管理）",
                value = false
            };
            foldout.style.marginTop = 8;
            foldout.Add(new HelpBox(
                "简中源文案保存在关卡 Authoring；Key 由 Stable ID 确定生成。英文由正式 TSV 翻译表维护，作者无需手填 Key。",
                HelpBoxMessageType.Info));
            KeyValuePair<string, CharacterTestGuidanceTextAuthoring>[] texts = LocalizedTexts(step);
            for (int i = 0; i < texts.Length; i++)
            {
                CharacterTestGuidanceTextAuthoring text = texts[i].Value;
                foldout.Add(CreateReadOnlyRow(texts[i].Key + " Key", text.Key));
                bool hasEnglish = TryGetCatalogText(
                    text.Key,
                    CharacterTestGuidanceAuthoringAdapter.TranslationLocaleId,
                    out string english);
                foldout.Add(CreateReadOnlyRow(
                    texts[i].Key + " English",
                    hasEnglish ? english : "缺失（保存允许，正式交付前需补译）"));
            }
            return foldout;
        }

        private void AddLocalizationDiagnostics(
            VisualElement section,
            CharacterTestGuidanceStepAuthoring selectedStep)
        {
            if (_localization == null)
            {
                section.Add(new HelpBox(
                    "本地化表不可用，无法验证英文翻译。",
                    HelpBoxMessageType.Warning));
                return;
            }
            if (_localization.HasErrors)
            {
                section.Add(new HelpBox(
                    "正式本地化 TSV 自身存在错误；请先修复本地化表。",
                    HelpBoxMessageType.Error));
            }
            if (selectedStep == null)
                return;

            KeyValuePair<string, CharacterTestGuidanceTextAuthoring>[] texts =
                LocalizedTexts(selectedStep);
            for (int i = 0; i < texts.Length; i++)
            {
                CharacterTestGuidanceTextAuthoring text = texts[i].Value;
                if (!TryGetCatalogText(
                        text.Key,
                        CharacterTestGuidanceAuthoringAdapter.TranslationLocaleId,
                        out _))
                {
                    section.Add(new HelpBox(
                        "MissingTranslation | " + texts[i].Key + "\n缺少 en-US 翻译：" + text.Key,
                        HelpBoxMessageType.Warning));
                    continue;
                }
                if (!TryGetCatalogText(
                        text.Key,
                        CharacterTestGuidanceAuthoringAdapter.SourceLocaleId,
                        out string translatedSource)
                    || !string.Equals(
                        translatedSource,
                        text.Fallback,
                        StringComparison.Ordinal))
                {
                    section.Add(new HelpBox(
                        "TranslationSourceChanged | " + texts[i].Key
                            + "\n简中源文案已变化；英文翻译需要复核。",
                        HelpBoxMessageType.Warning));
                }
            }
        }

        private string ResolvePreviewText(CharacterTestGuidanceTextAuthoring text)
        {
            if (text == null)
                return string.Empty;
            if (_previewLocale == PreviewLocale.SourceZhCn)
                return text.Fallback ?? string.Empty;
            return TryGetCatalogText(
                    text.Key,
                    CharacterTestGuidanceAuthoringAdapter.TranslationLocaleId,
                    out string translated)
                ? translated
                : "[Missing en-US] " + (text.Fallback ?? string.Empty);
        }

        private string ResolveCatalogText(string key, string fallback)
        {
            string localeId = _previewLocale == PreviewLocale.English
                ? CharacterTestGuidanceAuthoringAdapter.TranslationLocaleId
                : CharacterTestGuidanceAuthoringAdapter.SourceLocaleId;
            return TryGetCatalogText(key, localeId, out string translated)
                ? translated
                : fallback ?? string.Empty;
        }

        private bool TryGetCatalogText(string key, string localeId, out string text)
        {
            text = string.Empty;
            return _localization?.Provider != null
                && _localization.Provider.TryGetText(
                    new LocalizedTextKey(key),
                    new LocaleId(localeId),
                    out text);
        }

        private static string SafeFormat(string format, params object[] arguments)
        {
            try
            {
                return string.Format(format ?? string.Empty, arguments ?? Array.Empty<object>());
            }
            catch (FormatException)
            {
                return format ?? string.Empty;
            }
        }

        private static KeyValuePair<string, CharacterTestGuidanceTextAuthoring>[] LocalizedTexts(
            CharacterTestGuidanceStepAuthoring step)
        {
            CharacterTestGuidanceContentAuthoring content =
                step?.Content ?? new CharacterTestGuidanceContentAuthoring();
            return new[]
            {
                new KeyValuePair<string, CharacterTestGuidanceTextAuthoring>("标题", content.Title),
                new KeyValuePair<string, CharacterTestGuidanceTextAuthoring>("操作说明", content.ActionInstruction),
                new KeyValuePair<string, CharacterTestGuidanceTextAuthoring>("观察重点", content.Focus),
                new KeyValuePair<string, CharacterTestGuidanceTextAuthoring>("等待状态", content.WaitingStatus),
                new KeyValuePair<string, CharacterTestGuidanceTextAuthoring>("完成状态", content.CompletedStatus),
                new KeyValuePair<string, CharacterTestGuidanceTextAuthoring>("成功通知", content.SuccessNotice)
            };
        }

        private VisualElement CreatePreviewLegend()
        {
            var box = new HelpBox(
                "按键提示由 Runtime 使用 inputActionId 与 GuidancePrevious / GuidanceNext 动态组合；这里故意显示语义占位。",
                HelpBoxMessageType.Info);
            box.style.marginLeft = 8;
            box.style.marginRight = 8;
            box.style.marginTop = 4;
            return box;
        }

        private Button CreatePreviewStateButton(string text, PreviewState state)
        {
            var button = new Button(() =>
            {
                _previewState = state;
                RebuildPreview();
            }) { text = text };
            button.style.flexGrow = 1;
            button.style.marginRight = 3;
            if (_previewState == state)
            {
                button.style.backgroundColor = new Color(0.18f, 0.42f, 0.57f, 0.7f);
                button.style.color = Color.white;
            }
            return button;
        }

        private Button CreatePreviewLocaleButton(string text, PreviewLocale locale)
        {
            var button = new Button(() =>
            {
                _previewLocale = locale;
                RebuildPreview();
            }) { text = text };
            button.style.flexGrow = 1;
            button.style.marginRight = 3;
            if (_previewLocale == locale)
            {
                button.style.backgroundColor = new Color(0.32f, 0.34f, 0.38f, 0.9f);
                button.style.color = Color.white;
            }
            return button;
        }

        private static VisualElement CreatePreviewLine(string label, string body, Color color)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 5;
            var left = new Label(label + "：");
            left.style.width = 48;
            left.style.color = color;
            left.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(left);
            var right = new Label(body ?? string.Empty);
            right.style.flexGrow = 1;
            right.style.whiteSpace = WhiteSpace.Normal;
            row.Add(right);
            return row;
        }

        private static VisualElement CreatePanelHeader(string title, string trailing)
        {
            var header = new VisualElement();
            header.style.height = 34;
            header.style.minHeight = 34;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 10;
            header.style.paddingRight = 10;
            header.style.backgroundColor = PanelHeaderBackground;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = BorderColor;
            var label = new Label(title);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(label);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);
            var suffix = new Label(trailing ?? string.Empty);
            suffix.style.color = MutedText;
            suffix.style.fontSize = 10;
            header.Add(suffix);
            return header;
        }

        private static VisualElement CreateSection(string title)
        {
            var section = CreateCard();
            var heading = new Label(title);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.fontSize = 12;
            heading.style.marginBottom = 5;
            heading.style.color = (Color)new Color32(227, 231, 235, 255);
            section.Add(heading);
            return section;
        }

        private static VisualElement CreateCard()
        {
            var card = new VisualElement();
            card.style.marginLeft = 8;
            card.style.marginRight = 8;
            card.style.marginTop = 8;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 9;
            card.style.paddingBottom = 10;
            card.style.backgroundColor = (Color)new Color32(35, 38, 42, 255);
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = (Color)new Color32(57, 61, 66, 255);
            card.style.borderBottomColor = BorderColor;
            card.style.borderLeftColor = BorderColor;
            card.style.borderRightColor = BorderColor;
            card.style.borderTopLeftRadius = 4;
            card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = 4;
            card.style.borderBottomRightRadius = 4;
            return card;
        }

        private static VisualElement CreateEmptyState(string message)
        {
            var label = new Label(message);
            label.style.marginLeft = 16;
            label.style.marginRight = 16;
            label.style.marginTop = 24;
            label.style.color = MutedText;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static VisualElement CreateReadOnlyRow(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 4;
            var key = new Label(label);
            key.style.width = 150;
            key.style.color = MutedText;
            row.Add(key);
            var text = new Label(value ?? string.Empty);
            text.style.flexGrow = 1;
            text.style.whiteSpace = WhiteSpace.Normal;
            row.Add(text);
            return row;
        }

        private static void StylePane(VisualElement pane, float minWidth)
        {
            pane.style.minWidth = minWidth;
            pane.style.minHeight = 0;
            pane.style.backgroundColor = PanelBackground;
            pane.style.borderRightWidth = 1;
            pane.style.borderRightColor = BorderColor;
        }

        private static Button CreateButton(string text, Action action)
        {
            return new Button(action) { text = text };
        }

        private static Button CreateToolbarButton(string text, Action action)
        {
            Button button = CreateButton(text, action);
            button.style.width = 62;
            button.style.height = 24;
            button.style.marginLeft = 1;
            button.style.marginRight = 1;
            return button;
        }

        private static Button CreateCompactButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.flexGrow = 1;
            button.style.minWidth = 34;
            button.style.marginLeft = 1;
            button.style.marginRight = 1;
            return button;
        }

        private TextField CreateDelayedTextField(
            string label,
            string value,
            Action<string> assign,
            string undoLabel)
        {
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true,
                name = "character-test-guidance-field-" + label
            };
            field.RegisterValueChangedCallback(evt => Mutate(
                undoLabel,
                () => assign(evt.newValue)));
            return field;
        }

        private Toggle CreateToggle(
            string label,
            bool value,
            Action<bool> assign,
            string undoLabel)
        {
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => Mutate(
                undoLabel,
                () => assign(evt.newValue)));
            return field;
        }

        private IntegerField CreateIntegerPayloadField(
            string label,
            JObject payload,
            string property,
            int fallback,
            string undoLabel)
        {
            var field = new IntegerField(label)
            {
                value = payload.Value<int?>(property) ?? fallback
            };
            field.RegisterValueChangedCallback(evt => Mutate(
                undoLabel,
                () => payload[property] = evt.newValue));
            return field;
        }

        private Toggle CreateBooleanPayloadField(
            string label,
            JObject payload,
            string property,
            bool fallback,
            string undoLabel)
        {
            var field = new Toggle(label)
            {
                value = payload.Value<bool?>(property) ?? fallback
            };
            field.RegisterValueChangedCallback(evt => Mutate(
                undoLabel,
                () => payload[property] = evt.newValue));
            return field;
        }

        private void AddBlankStep()
        {
            EnsureLoaded();
            string stableId = CreateUniqueStepId("guidance-step");
            CharacterTestGuidanceStepAuthoring step =
                CharacterTestGuidanceAuthoringAdapter.CreateBlankStep(stableId);
            int insertionIndex = StepInsertionIndex();
            _previewState = PreviewState.Waiting;
            Mutate(
                "新增空白教学步骤",
                () => _document.Steps.Insert(insertionIndex, step),
                stableId);
        }

        private void AddPresetStep(string presetId)
        {
            EnsureLoaded();
            if (!CharacterTestGuidanceStepPresetCatalog.TryGet(
                    presetId,
                    out CharacterTestGuidanceStepPreset preset))
            {
                _status = "未找到教学步骤预设：" + presetId;
                return;
            }
            string stableId = CreateUniqueStepId(preset.StableIdSeed);
            CharacterTestGuidanceStepAuthoring step =
                CharacterTestGuidanceAuthoringAdapter.CreateStepFromPreset(presetId, stableId);
            int insertionIndex = StepInsertionIndex();
            _previewState = PreviewState.Waiting;
            Mutate(
                "从预设新建教学步骤",
                () => _document.Steps.Insert(insertionIndex, step),
                stableId);
        }

        private void ChangeSelectedCompletionType(string typeId)
        {
            CharacterTestGuidanceStepAuthoring step = SelectedStep();
            if (step == null
                || string.Equals(step.Completion?.TypeId, typeId, StringComparison.Ordinal)
                    && step.Completion.SchemaVersion == 1)
            {
                return;
            }
            _previewState = PreviewState.Waiting;
            Mutate(
                "切换教学步骤完成条件",
                () => CharacterTestGuidanceAuthoringAdapter.ChangeCompletionType(step, typeId));
        }

        private void DuplicateSelectedStep()
        {
            CharacterTestGuidanceStepAuthoring selected = SelectedStep();
            if (selected == null)
                return;
            CharacterTestGuidanceStepAuthoring copy = JObject.FromObject(selected)
                .ToObject<CharacterTestGuidanceStepAuthoring>();
            CharacterTestGuidanceAuthoringAdapter.ChangeStableId(
                copy,
                CreateUniqueStepId(selected.StableId + "-copy"));
            int index = IndexOfStep(selected.StableId);
            Mutate(
                "复制教学步骤",
                () => _document.Steps.Insert(index + 1, copy),
                copy.StableId);
        }

        private void DeleteSelectedStep()
        {
            CharacterTestGuidanceStepAuthoring selected = SelectedStep();
            if (selected == null)
                return;
            int index = IndexOfStep(selected.StableId);
            Mutate("删除教学步骤", () => _document.Steps.Remove(selected));
            if (_document.Steps.Count == 0)
                _selectedStableId = string.Empty;
            else
                _selectedStableId = _document.Steps[Mathf.Clamp(index, 0, _document.Steps.Count - 1)].StableId;
            RefreshAll("已删除教学步骤，尚未保存。");
        }

        private void MoveSelectedStep(int delta)
        {
            CharacterTestGuidanceStepAuthoring selected = SelectedStep();
            int index = selected == null ? -1 : IndexOfStep(selected.StableId);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= _document.Steps.Count)
                return;
            Mutate("调整教学步骤顺序", () =>
            {
                _document.Steps.RemoveAt(index);
                _document.Steps.Insert(target, selected);
            });
        }

        private void EnsureLoaded()
        {
            string path = DocumentPath;
            if (_document != null && _documentIdentity.IsFor(path))
                return;
            _documentIdentity.RequireCanLoad(path, _document != null, _dirty, ModuleName);
            if (_document != null) { Reload(); return; }
            LoadFromDisk();
            _dirty = false;
        }

        private void LoadFromDisk()
        {
            string localizationPath = ToFullPath(
                CharacterTestGuidanceAuthoringAdapter.DefaultLocalizationPath);
            _localization = LocalizationTsvLoader.LoadFromText(
                File.Exists(localizationPath) ? File.ReadAllText(localizationPath) : string.Empty);
            string path = DocumentPath;
            string fullPath = ToFullPath(path);
            _documentIdentity.RequireCanLoad(path, _document != null, _dirty, ModuleName);
            string source = File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
            if (!_documentIdentity.IsFor(path)) { _undoSession?.Retire(); _undoSession = null; }
            _envelope = source != null
                ? MxLevelAuthoringJson.LoadFromJson(source)
                : new MxLevelAuthoringDocument();
            _documentIdentity.Bind(path, _envelope, source);
            if (CharacterTestGuidanceAuthoringAdapter.TryExtract(
                    _envelope,
                    out CharacterTestGuidanceAuthoringDocument loaded,
                    out _loadDiagnostic))
            {
                _document = loaded.Normalize();
                _authoringEditable = true;
            }
            else if (_loadDiagnostic.Code == CharacterTestGuidanceRuntimeDiagnosticCode.ModuleMissing)
            {
                _document = CharacterTestGuidanceAuthoringAdapter.CreateEmptyDocument();
                _authoringEditable = true;
                _status = "当前 envelope 尚无教学引导模块；可从空白步骤或预设开始创作。";
            }
            else
            {
                _document = new CharacterTestGuidanceAuthoringDocument { Enabled = false };
                _authoringEditable = false;
                _status = "教学引导 Authoring 已只读：" + _loadDiagnostic.Code + " · "
                    + _loadDiagnostic.Message;
            }
            _report = CharacterTestGuidanceAuthoringValidator.Validate(_document);
            EnsureSelectedStep();
            RefreshEditabilityProjection();
        }

        private void EnsureUndoSession()
        {
            if (_undoSession != null || _document == null)
                return;
            _undoSession = new CharacterTestLevelEditorDocumentUndoSession(
                ModuleName,
                CaptureSnapshot,
                RestoreSnapshot,
                RefreshAfterUndo);
            _undoSession.ResetBaseline();
        }

        private string CaptureSnapshot()
        {
            return _document == null
                ? string.Empty
                : JObject.FromObject(_document).ToString(Formatting.None);
        }

        private void RestoreSnapshot(string snapshot)
        {
            _document = string.IsNullOrWhiteSpace(snapshot)
                ? CharacterTestGuidanceAuthoringAdapter.CreateEmptyDocument()
                : JObject.Parse(snapshot).ToObject<CharacterTestGuidanceAuthoringDocument>();
            _document = _document.Normalize();
            EnsureSelectedStep();
        }

        private void RefreshAfterUndo()
        {
            InvalidateSaveCompletionForEdit();
            _dirty = !_undoSession.IsAtSavedBaseline;
            _report = CharacterTestGuidanceAuthoringValidator.Validate(_document);
            RefreshAll("已应用 Undo / Redo。");
        }

        private void Mutate(
            string label,
            Action mutation,
            string selectStableIdAfterMutation = null)
        {
            if (!_authoringEditable)
            {
                _status = "教学引导 Authoring 已只读：" + _loadDiagnostic.Code + " · "
                    + _loadDiagnostic.Message;
                return;
            }
            EnsureUndoSession();
            string selectedId = _selectedStableId;
            int selectedIndex = IndexOfStep(selectedId);
            using CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                _undoSession.BeginTransaction(label);
            mutation();
            for (int i = 0; i < (_document?.Steps?.Count ?? 0); i++)
                CharacterTestGuidanceAuthoringAdapter.AssignManagedTextKeys(_document.Steps[i]);
            _document = _document.Normalize();
            if (!string.IsNullOrWhiteSpace(selectStableIdAfterMutation)
                && IndexOfStep(selectStableIdAfterMutation) >= 0)
            {
                _selectedStableId = selectStableIdAfterMutation;
            }
            else
            {
                _selectedStableId = IndexOfStep(selectedId) >= 0
                    ? selectedId
                    : SelectStableIdAtIndex(selectedIndex);
            }
            EnsureSelectedStep();
            transaction.Commit();
            InvalidateSaveCompletionForEdit();
            _dirty = true;
            _report = CharacterTestGuidanceAuthoringValidator.Validate(_document);
            RefreshAll(label + "，尚未保存。");
        }

        private string SelectStableIdAtIndex(int index)
        {
            if (_document?.Steps == null || _document.Steps.Count == 0)
                return string.Empty;
            return _document.Steps[Mathf.Clamp(index, 0, _document.Steps.Count - 1)].StableId;
        }

        private void InvalidateSaveCompletionForEdit()
        {
            _saveState = SaveCompletionState.Idle;
            _preparedPlan = null;
            _cacheBeforePreparedSave = null;
            _cacheApplied = false;
        }

        private void EnsureSelectedStep()
        {
            if (SelectedStep() != null)
                return;
            _selectedStableId = _document?.Steps != null && _document.Steps.Count > 0
                ? _document.Steps[0].StableId
                : string.Empty;
        }

        private CharacterTestGuidanceStepAuthoring SelectedStep()
        {
            int index = IndexOfStep(_selectedStableId);
            return index >= 0 ? _document.Steps[index] : null;
        }

        private int IndexOfStep(string stableId)
        {
            if (_document?.Steps == null || string.IsNullOrWhiteSpace(stableId))
                return -1;
            for (int i = 0; i < _document.Steps.Count; i++)
            {
                if (string.Equals(_document.Steps[i].StableId, stableId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private string SelectedStepIndexLabel()
        {
            int index = IndexOfStep(_selectedStableId);
            return index < 0 ? "未选择" : (index + 1) + " / " + _document.Steps.Count;
        }

        private string CreateUniqueStepId(string seed)
        {
            string normalized = string.IsNullOrWhiteSpace(seed) ? "guidance-step" : seed.Trim();
            string candidate = normalized;
            int suffix = 2;
            while (IndexOfStep(candidate) >= 0)
                candidate = normalized + "-" + suffix++;
            return candidate;
        }

        private int StepInsertionIndex()
        {
            int selectedIndex = IndexOfStep(_selectedStableId);
            return selectedIndex < 0 ? _document.Steps.Count : selectedIndex + 1;
        }

        private static string CompletionDisplayName(string typeId)
        {
            switch (typeId)
            {
                case CharacterTestGuidanceCompletionRegistry.MoveDistanceAndStop: return "移动距离并停步";
                case CharacterTestGuidanceCompletionRegistry.JumpAndLand: return "跳跃并落地";
                case CharacterTestGuidanceCompletionRegistry.CameraModeChanged: return "相机模式切换";
                case CharacterTestGuidanceCompletionRegistry.TargetLockState: return "目标锁定状态";
                case CharacterTestGuidanceCompletionRegistry.BasicAttackDamage: return "普通攻击伤害";
                default: return string.IsNullOrWhiteSpace(typeId)
                    ? "未选择完成条件"
                    : "不支持的完成条件：" + typeId;
            }
        }

        private static bool TryGetCompletionTypeId(string displayName, out string typeId)
        {
            for (int i = 0; i < CharacterTestGuidanceCompletionRegistry.TypeIds.Count; i++)
            {
                string candidate = CharacterTestGuidanceCompletionRegistry.TypeIds[i];
                if (string.Equals(
                        CompletionDisplayName(candidate),
                        displayName,
                        StringComparison.Ordinal))
                {
                    typeId = candidate;
                    return true;
                }
            }
            typeId = string.Empty;
            return false;
        }

        private Button CreateBlankStepButton(string location, float width)
        {
            var button = new Button(AddBlankStep)
            {
                name = "character-test-guidance-add-blank-" + location,
                text = "＋ 空白步骤"
            };
            button.style.width = width;
            button.style.height = 24;
            button.style.marginRight = 4;
            return button;
        }

        private ToolbarMenu CreatePresetMenu(string location, float width)
        {
            var menu = new ToolbarMenu
            {
                name = "character-test-guidance-add-preset-" + location,
                text = "从预设新建"
            };
            menu.style.width = width;
            menu.style.height = 24;
            for (int i = 0; i < CharacterTestGuidanceStepPresetCatalog.Presets.Count; i++)
            {
                CharacterTestGuidanceStepPreset preset =
                    CharacterTestGuidanceStepPresetCatalog.Presets[i];
                string presetId = preset.PresetId;
                menu.menu.AppendAction(preset.DisplayName, _ => AddPresetStep(presetId));
            }
            return menu;
        }

        private static List<string> LoadGameplayActionChoices()
        {
            var choices = new List<string>();
            try
            {
                string fullPath = ToFullPath(InputActionsAssetPath);
                if (File.Exists(fullPath))
                {
                    JObject asset = JObject.Parse(File.ReadAllText(fullPath));
                    JArray maps = asset["maps"] as JArray;
                    for (int mapIndex = 0; mapIndex < (maps?.Count ?? 0); mapIndex++)
                    {
                        if (!(maps[mapIndex] is JObject map)
                            || !string.Equals(
                                map.Value<string>("name"),
                                "Gameplay",
                                StringComparison.Ordinal)
                            || !(map["actions"] is JArray actions))
                        {
                            continue;
                        }

                        for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                        {
                            string actionName = actions[actionIndex]?.Value<string>("name");
                            if (!string.IsNullOrWhiteSpace(actionName))
                                choices.Add("Gameplay/" + actionName.Trim());
                        }
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // The validator remains authoritative. Keep the editor usable if the source
                // Input Actions asset is temporarily malformed while another import is running.
            }

            if (choices.Count == 0)
            {
                AddFallback("Gameplay/Move");
                AddFallback("Gameplay/Jump");
                AddFallback("Gameplay/CameraModeNext");
                AddFallback("Gameplay/TargetLock");
                AddFallback("Gameplay/AttackPrimary");
            }
            return choices;

            void AddFallback(string actionId)
            {
                if (!choices.Contains(actionId))
                    choices.Add(actionId);
            }
        }

        private void RefreshAll(string status)
        {
            _status = status ?? string.Empty;
            RefreshEditabilityProjection();
            RebuildEditorAndPreview();
            if (_stepList != null)
            {
                _stepList.itemsSource = _document?.Steps;
                _stepList.Rebuild();
                int selectedIndex = IndexOfStep(_selectedStableId);
                if (selectedIndex >= 0)
                    _stepList.SetSelectionWithoutNotify(new[] { selectedIndex });
                else
                    _stepList.ClearSelection();
            }
            _window?.RepaintRequested();
        }

        private void RefreshEditabilityProjection()
        {
            _moduleToolbarRow?.SetEnabled(_authoringEditable);
            _authoringLayout?.SetEnabled(_authoringEditable);
            if (_readOnlyHelp == null)
                return;

            _readOnlyHelp.text = "教学引导 Authoring 已只读：" + _loadDiagnostic.Code + " · "
                + _loadDiagnostic.Message;
            _readOnlyHelp.style.display = _authoringEditable
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private static GameManager FindGameManager()
        {
            GameManager[] managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include);
            if (managers == null || managers.Length == 0)
                return null;
            string activePath = EditorSceneManager.GetActiveScene().path;
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null
                    && string.Equals(
                        managers[i].gameObject.scene.path,
                        activePath,
                        StringComparison.Ordinal))
                    return managers[i];
            }
            return managers[0];
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static void WriteEnvelope(
            string assetPath,
            MxLevelAuthoringDocument envelope)
        {
            string fullPath = ToFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(
                fullPath,
                MxLevelAuthoringJson.SaveToJson(envelope),
                new System.Text.UTF8Encoding(true));
        }
    }
}
