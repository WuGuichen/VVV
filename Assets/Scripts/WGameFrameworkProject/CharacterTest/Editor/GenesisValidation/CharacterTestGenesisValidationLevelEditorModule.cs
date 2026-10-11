using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestGenesisValidationLevelEditorModule :
        IMxLevelEditorModule,
        IMxLevelEditorStructuredModule,
        IMxLevelEditorSelectionSyncModule,
        IMxLevelEditorSceneViewModule,
        IMxLevelEditorDirtyDiagnostics,
        IMxLevelEditorSceneChangeAwareModule,
        IMxLevelEditorSceneScope,
        IMxLevelEditorSaveParticipant
    {
        private enum SaveCompletionState
        {
            Idle = 0,
            Prepared = 1,
            Written = 2,
            Committed = 3,
            TailFailed = 4,
            PreCommitFailed = 5
        }

        private const string ModuleName = "创世验证";
        private MxLevelAuthoringDocument _envelope;
        private CharacterTestGenesisValidationAuthoringDocument _document;
        private CharacterTestSceneCharacterAuthoringDocument _characters;
        private LevelWorldObjectAuthoringDocument _worldObjects;
        private CharacterTestGenesisValidationAuthoringReport _report;
        private CharacterTestLevelEditorDocumentUndoSession _undoSession;
        private MxLevelEditorContext _activeContext;
        private CharacterTestGenesisValidationStation _selectedStation;
        private string _activeTreeNodeId = "document";
        private bool _dirty;
        private string _status = string.Empty;
        private SaveCompletionState _saveState;
        private int _saveGeneration;
        private int _activeSaveGeneration;
        private int _committedSaveGeneration;
        private bool _dirtyBeforePreparedSave;
        private string _documentPathOverride;
        private readonly CharacterTestLevelEditorModuleDocumentIdentity _documentIdentity = new();
        private readonly CharacterTestWorldObjectLevelEditorModule _worldObjectModule;
        private string _resolvedSpatialInputHash = string.Empty;

        internal CharacterTestGenesisValidationLevelEditorModule()
            : this(null)
        {
        }

        internal CharacterTestGenesisValidationLevelEditorModule(
            CharacterTestWorldObjectLevelEditorModule worldObjectModule)
        {
            _worldObjectModule = worldObjectModule;
        }

        public string ModuleId => CharacterTestGenesisValidationAuthoringAdapter.ModuleId;
        public string DisplayName => ModuleName;
        public string DocumentPath => string.IsNullOrWhiteSpace(_documentPathOverride)
            ? CharacterTestWorldObjectLevelEditorModule.ResolveActiveSceneDocumentPath()
            : _documentPathOverride;
        public bool IsDirty => _dirty;
        public bool IsDocumentDirty => _dirty;
        public bool IsManagedSceneDirty => false;
        public string Status => _status;
        public bool SupportsSceneViewEditing => true;
        public Transform ManagedSceneRoot => FindGameManager()?.WorldObjectSceneRoot;

        public bool AppliesToActiveScene =>
            CharacterTestWorldObjectLevelEditorModule.HasActiveSceneAuthoringTarget(_documentPathOverride);

        internal string SaveStateForTests => _saveState.ToString();
        internal int SaveGenerationForTests => _saveGeneration;
        internal int ActiveSaveGenerationForTests => _activeSaveGeneration;
        internal int CommittedSaveGenerationForTests => _committedSaveGeneration;
        internal CharacterTestGenesisValidationAuthoringDocument DocumentForTests => _document;

        internal void SetDocumentPathForTests(string documentPath)
        {
            if (_document != null)
                throw new InvalidOperationException("Test document path must be set before loading.");
            _documentPathOverride = documentPath;
        }

        internal void MutateForTests(
            string label,
            Action<CharacterTestGenesisValidationAuthoringDocument> mutation)
        {
            Mutate(label, () => mutation(_document));
        }

        internal void AddStationForTests() => AddStation();
        internal void DuplicateSelectedStationForTests() => DuplicateSelectedStation();
        internal void DeleteSelectedStationForTests() => DeleteSelectedStation();
        internal void MoveSelectedStationForTests(int delta) => MoveSelectedStation(delta);

        public void OnEnable()
        {
            EnsureLoaded();
            EnsureUndoSession();
        }

        public VisualElement CreateContent(MxLevelEditorWindow window) => null;

        public void BuildModuleToolbar(MxLevelEditorWindow window, VisualElement toolbar)
        {
            EnsureLoaded();
            toolbar.Add(CreateButton("新增验证站", AddStation));
            toolbar.Add(CreateButton("复制验证站", DuplicateSelectedStation));
            toolbar.Add(CreateButton("删除验证站", DeleteSelectedStation));
            toolbar.Add(CreateButton("上移", () => MoveSelectedStation(-1)));
            toolbar.Add(CreateButton("下移", () => MoveSelectedStation(1)));
            toolbar.Add(CreateButton("新增分组", AddGroup));
            toolbar.Add(CreateButton("创建基础场景框架", CreateFoundationSceneScaffold));
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
            _activeSaveGeneration = 0;
            EnsureUndoSession();
            _undoSession.ResetBaseline();
            RefreshAll("已重新加载创世验证 Authoring。");
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
            RefreshOwnerDocuments();
            _report = ValidateDocument();
            _status = "校验完成：错误=" + _report.ErrorCount + "。";
            _activeContext?.RequestRefresh?.Invoke();
        }

        public void ApplyToScene()
        {
            _status = "P1 只保存 Authoring；运行缓存构建与应用属于 P2，当前未执行任何 Scene cache 操作。";
        }

        public void SelectRootSource()
        {
            GameManager gameManager = FindGameManager();
            if (gameManager == null)
            {
                _status = "当前场景中未找到 CharacterTest GameManager。";
                return;
            }

            Selection.activeObject = gameManager.WorldObjectSceneRoot == null
                ? gameManager.gameObject
                : gameManager.WorldObjectSceneRoot.gameObject;
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        public IReadOnlyList<MxLevelEditorTreeNode> BuildTree(MxLevelEditorContext context)
        {
            _activeContext = context;
            EnsureLoaded();
            var nodes = new List<MxLevelEditorTreeNode>
            {
                new MxLevelEditorTreeNode("document", "全部验证站", 0, _document.Stations.Count)
            };
            for (int i = 0; i < _document.Groups.Count; i++)
            {
                CharacterTestGenesisValidationGroup group = _document.Groups[i];
                nodes.Add(new MxLevelEditorTreeNode(
                    "group:" + group.StableId,
                    string.IsNullOrWhiteSpace(group.DisplayName) ? group.StableId : group.DisplayName,
                    0,
                    CountStations(group.StableId)));
            }
            return nodes;
        }

        public IReadOnlyList<MxLevelEditorListRow> BuildRows(
            MxLevelEditorContext context,
            string selectedTreeNodeId)
        {
            _activeContext = context;
            _activeTreeNodeId = string.IsNullOrWhiteSpace(selectedTreeNodeId)
                ? "document"
                : selectedTreeNodeId;
            EnsureLoaded();
            var rows = new List<MxLevelEditorListRow>();
            for (int i = 0; i < _document.Stations.Count; i++)
            {
                CharacterTestGenesisValidationStation station = _document.Stations[i];
                if (_activeTreeNodeId.StartsWith("group:", StringComparison.Ordinal)
                    && !string.Equals(station.GroupId, _activeTreeNodeId.Substring(6), StringComparison.Ordinal))
                {
                    continue;
                }

                rows.Add(new MxLevelEditorListRow(
                    "station:" + station.StableId,
                    "验证站",
                    ResolveGroupName(station.GroupId),
                    ResolveScenarioName(station.Scenario),
                    station.StableId,
                    station.Intent,
                    station.Enabled ? "Authoring" : "disabled",
                    station.DisplayName + " " + station.Intent + " " + station.StableId,
                    station));
            }
            return rows;
        }

        public void BuildInspector(
            MxLevelEditorContext context,
            MxLevelEditorListRow selectedRow,
            VisualElement inspector)
        {
            _activeContext = context;
            _selectedStation = selectedRow?.Payload as CharacterTestGenesisValidationStation;
            inspector.Clear();
            if (_selectedStation == null)
            {
                BuildScenePlanInspector(inspector);
                return;
            }

            BuildStationInspector(inspector, _selectedStation);
        }

        public void BuildDiagnostics(MxLevelEditorContext context, VisualElement diagnostics)
        {
            _activeContext = context;
            diagnostics.Clear();
            diagnostics.Add(new Label("Authority / Scene readiness / Cache health"));
            CharacterTestGenesisValidationStation station = _selectedStation;
            if (station != null)
            {
                diagnostics.Add(new Label(
                    "Authority: " + CharacterTestGenesisValidationCapabilityResolver.ResolveAuthority(station)));
                diagnostics.Add(new Label(
                    "Scene readiness: " + CharacterTestGenesisValidationCapabilityResolver.ResolveSceneReadiness(station)
                    + "（P1 无 executable adapter）"));
            }
            diagnostics.Add(new Label(
                "Cache health: " + CharacterTestGenesisValidationCapabilityResolver.ResolveCacheHealth()));
            diagnostics.Add(new Label(
                "Resolved spatial inputs: "
                + (string.IsNullOrWhiteSpace(_resolvedSpatialInputHash)
                    ? "none"
                    : _resolvedSpatialInputHash)));

            CharacterTestGenesisValidationFixtureReadinessReport fixtureReadiness =
                EvaluateFixtureReadiness();
            diagnostics.Add(new Label(
                "Fixture readiness: 已应用="
                + fixtureReadiness.AppliedCount
                + "，未应用="
                + fixtureReadiness.UnappliedCount
                + "，引用失配="
                + fixtureReadiness.UnresolvedCount));
            if (fixtureReadiness.BlocksPlay)
            {
                diagnostics.Add(new HelpBox(
                    "存在只有外观、没有 owner runtime cache 投影的验证夹具。"
                    + "未应用夹具以橙色线框显示，引用失配以洋红色显示；修复并保存前会阻止进入 Play。",
                    HelpBoxMessageType.Error));
            }

            if (_report == null || _report.Issues.Count == 0)
            {
                diagnostics.Add(new HelpBox("Authoring 结构与引用校验通过。", HelpBoxMessageType.Info));
                return;
            }

            for (int i = 0; i < _report.Issues.Count; i++)
            {
                CharacterTestGenesisValidationAuthoringIssue issue = _report.Issues[i];
                diagnostics.Add(new HelpBox(
                    issue.Code + " | " + issue.Path + "\n" + issue.Message,
                    issue.Severity == LevelWorldObjectAuthoringSeverity.Error
                        ? HelpBoxMessageType.Error
                        : HelpBoxMessageType.Warning));
                string issuePath = issue.Path;
                diagnostics.Add(new Button(() => {
                    if (!RevealLevelIssue(_activeContext.Window, issuePath)) _status = "该路径暂未支持自动字段定位：" + issuePath;
                    _activeContext.RequestRefresh?.Invoke(); }) { text = "定位验证站与已支持字段", name = "mx-genesis-locate-" + i });
            }
        }

        public bool TryResolveRowObject(
            MxLevelEditorContext context,
            MxLevelEditorListRow row,
            out Object target)
        {
            target = null;
            CharacterTestGenesisValidationStation station =
                row?.Payload as CharacterTestGenesisValidationStation;
            if (station == null || !TryResolvePrimaryTransform(station, out Transform transform))
                return false;
            target = transform.gameObject;
            return true;
        }

        public bool TryResolveSelection(
            MxLevelEditorContext context,
            Object selectedObject,
            out MxLevelEditorSelectionTarget target)
        {
            target = null;
            Transform selected = selectedObject is GameObject gameObject
                ? gameObject.transform
                : (selectedObject as Component)?.transform;
            if (selected == null)
                return false;

            for (int i = 0; i < _document.Stations.Count; i++)
            {
                CharacterTestGenesisValidationStation station = _document.Stations[i];
                if (TryResolvePrimaryTransform(station, out Transform transform) && transform == selected)
                {
                    target = new MxLevelEditorSelectionTarget(
                        "group:" + station.GroupId,
                        "station:" + station.StableId);
                    return true;
                }
            }
            return false;
        }

        public void DrawSceneView(MxLevelEditorSceneViewContext context, SceneView sceneView)
        {
            DrawScenePlan(sceneView);
            DrawFixtureReadinessOverlay();
            if (_selectedStation == null)
                return;

            var targets = new List<Transform>();
            CollectStationTransforms(_selectedStation, targets);
            Handles.color = new Color(0.25f, 0.82f, 1f, 0.9f);
            for (int i = 0; i < targets.Count; i++)
            {
                Transform transform = targets[i];
                if (transform == null)
                    continue;
                Handles.DrawWireDisc(transform.position, Vector3.up, 0.45f);
                Handles.Label(transform.position + Vector3.up * 0.55f, _selectedStation.DisplayName);
                if (i > 0 && targets[0] != null)
                    Handles.DrawDottedLine(targets[0].position, transform.position, 4f);
            }
        }

        public void CollectManagedSceneTransforms(List<Transform> output)
        {
            // Validation owns no Scene object. Owner modules already register these transforms.
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
            RefreshOwnerDocuments();
            _report = ValidateDocument();
            _activeSaveGeneration = ++_saveGeneration;
            _dirtyBeforePreparedSave = _dirty;
            if (_report.HasErrors)
            {
                _saveState = SaveCompletionState.PreCommitFailed;
                failureMessage = "创世验证 Authoring 校验失败，未写入文档。错误=" + _report.ErrorCount + "。";
                return false;
            }

            GameManager manager = FindGameManager();
            if (manager == null || manager.WorldObjectSceneRoot == null)
            {
                _saveState = SaveCompletionState.PreCommitFailed;
                failureMessage = "当前受管场景中未找到 CharacterTest 场景根。";
                return false;
            }

            _saveState = SaveCompletionState.Prepared;
            failureMessage = string.Empty;
            return true;
        }

        void IMxLevelEditorSaveParticipant.SavePreparedChanges()
        {
            if (_saveState != SaveCompletionState.Prepared)
                throw new InvalidOperationException("创世验证保存参与者尚未完成 prepare。 ");

            string fullPath = ToFullPath(DocumentPath);
            MxLevelAuthoringDocument latest = File.Exists(fullPath)
                ? MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath))
                : _envelope;
            _envelope = CharacterTestGenesisValidationAuthoringAdapter.Upsert(latest, _document);
            WriteEnvelope(DocumentPath, _envelope);
            AssetDatabase.ImportAsset(DocumentPath);
            _saveState = SaveCompletionState.Written;
        }

        void IMxLevelEditorSaveParticipant.CompleteCoordinatedSave(bool succeeded, string message)
        {
            if (succeeded)
            {
                if (_saveState == SaveCompletionState.Committed
                    && _activeSaveGeneration == _committedSaveGeneration
                    && _activeSaveGeneration > 0)
                    return;
                if (_saveState != SaveCompletionState.Written
                    || _activeSaveGeneration <= 0)
                    return;

                _saveState = SaveCompletionState.Committed;
                _committedSaveGeneration = _activeSaveGeneration;
                _dirty = false;
                _documentIdentity.AcknowledgeSource(File.ReadAllText(ToFullPath(DocumentPath)));
                EnsureUndoSession();
                _undoSession.ResetBaseline();
                RefreshAll(message);
                return;
            }

            if ((_saveState == SaveCompletionState.Committed
                    || _saveState == SaveCompletionState.TailFailed)
                && _activeSaveGeneration > 0
                && _activeSaveGeneration == _committedSaveGeneration)
            {
                _saveState = SaveCompletionState.TailFailed;
                _dirty = false;
                RefreshAll(message + " 文档已提交；按 post-commit tail failure 边界不回滚文件。 ");
                return;
            }

            if (_saveState == SaveCompletionState.Idle)
            {
                RefreshAll(message);
                return;
            }

            _saveState = SaveCompletionState.PreCommitFailed;
            _dirty = _dirtyBeforePreparedSave;
            _activeSaveGeneration = 0;
            RefreshAll(message);
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
            string path = DocumentPath;
            _documentIdentity.RequireCanLoad(path, _document != null, _dirty, ModuleName);
            string fullPath = ToFullPath(path);
            string source = File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
            if (!_documentIdentity.IsFor(path)) { _undoSession?.Retire(); _undoSession = null; }
            _envelope = source != null
                ? MxLevelAuthoringJson.LoadFromJson(source)
                : new MxLevelAuthoringDocument();
            _documentIdentity.Bind(path, _envelope, source);
            _document = CharacterTestGenesisValidationAuthoringAdapter.Extract(_envelope);
            RefreshOwnerDocuments();
            _report = ValidateDocument();
        }

        private void RefreshOwnerDocuments()
        {
            bool joinedSave = CharacterTestJoinedLevelSaveContext.TryGet(DocumentPath, out var joined);
            if (joinedSave) _envelope = joined;
            _characters = CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(
                _envelope,
                out CharacterTestSceneCharacterAuthoringDocument characters,
                out _)
                ? characters
                : new CharacterTestSceneCharacterAuthoringDocument();
            _worldObjects = CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                _envelope,
                out LevelWorldObjectAuthoringDocument worldObjects,
                out _)
                ? worldObjects
                : new LevelWorldObjectAuthoringDocument();
            if (_worldObjectModule != null && !joinedSave)
            {
                _worldObjects = _worldObjectModule
                    .GetWorldObjectDocumentSnapshotForComposition();
            }
        }

        private CharacterTestGenesisValidationAuthoringReport ValidateDocument()
        {
            GameManager manager = FindGameManager();
            CharacterTestGenesisValidationOwnerCatalog owners =
                CharacterTestGenesisValidationOwnerCatalog.Build(
                    _characters,
                    _worldObjects,
                    manager?.WorldObjectSceneCache.StaticColliderInstances);
            CharacterTestGenesisValidationAuthoringReport authoring =
                CharacterTestGenesisValidationAuthoringValidator.Validate(_document, owners);
            IReadOnlyList<CharacterTestGenesisValidationAuthoringIssue> layout =
                CharacterTestGenesisValidationEditorLayoutValidator.Validate(
                    _document,
                    _worldObjects,
                    manager?.WorldObjectSceneRoot,
                    out _resolvedSpatialInputHash);
            if (layout.Count == 0)
                return authoring;
            var combined = new List<CharacterTestGenesisValidationAuthoringIssue>(
                authoring.Issues.Count + layout.Count);
            for (int i = 0; i < authoring.Issues.Count; i++)
                combined.Add(authoring.Issues[i]);
            for (int i = 0; i < layout.Count; i++)
                combined.Add(layout[i]);
            return new CharacterTestGenesisValidationAuthoringReport(combined);
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
                : JObject.FromObject(_document).ToString(Newtonsoft.Json.Formatting.None);
        }

        private void RestoreSnapshot(string snapshot)
        {
            string selectedId = _selectedStation?.StableId;
            _document = string.IsNullOrWhiteSpace(snapshot)
                ? CharacterTestGenesisValidationAuthoringAdapter.CreateDefaultDocument()
                : JObject.Parse(snapshot).ToObject<CharacterTestGenesisValidationAuthoringDocument>();
            _document = _document.Normalize();
            _selectedStation = FindStationById(selectedId);
        }

        private void RefreshAfterUndo()
        {
            InvalidateSaveCompletionForEdit();
            _dirty = !_undoSession.IsAtSavedBaseline;
            _report = ValidateDocument();
            RefreshAll("已应用 Undo / Redo。 ");
        }

        private void Mutate(string label, Action mutation)
        {
            EnsureUndoSession();
            using CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                _undoSession.BeginTransaction(label);
            mutation();
            string selectedId = _selectedStation?.StableId;
            _document = _document.Normalize();
            _selectedStation = FindStationById(selectedId);
            transaction.Commit();
            InvalidateSaveCompletionForEdit();
            _dirty = true;
            _report = ValidateDocument();
            RefreshAll(label + "，尚未保存。 ");
        }

        private void AddGroup()
        {
            int next = _document.Groups.Count + 1;
            string id = CreateUniqueGroupId("group-" + next.ToString("00"));
            Mutate("新增创世验证分组", () => _document.Groups.Add(
                new CharacterTestGenesisValidationGroup
                {
                    StableId = id,
                    DisplayName = "新分组 " + next,
                    SortOrder = next * 10
                }));
        }

        private void AddStation()
        {
            if (_document.Groups.Count == 0)
                AddGroup();
            ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor =
                CharacterTestGenesisValidationAuthoringScenarioRegistry.All[0];
            int next = _document.Stations.Count + 1;
            var station = new CharacterTestGenesisValidationStation
            {
                StableId = CreateUniqueStationId("station-" + next.ToString("00")),
                DisplayName = "新验证站 " + next,
                GroupId = _document.Groups[0].StableId,
                SortOrder = next * 10,
                Intent = "说明要验证的身体事实",
                Scenario = descriptor.CreateDefaultScenario(),
                Observation = new CharacterTestGenesisValidationObservation
                {
                    BodyPrompt = "记录身体反馈与 typed 读数。",
                    RequiredSignalIds = new List<string>()
                }
            };
            Mutate("新增创世验证站", () => _document.Stations.Add(station));
            _selectedStation = FindStationById(station.StableId);
        }

        private void DuplicateSelectedStation()
        {
            if (_selectedStation == null)
                return;
            CharacterTestGenesisValidationStation copy = JObject.FromObject(_selectedStation)
                .ToObject<CharacterTestGenesisValidationStation>();
            copy.StableId = CreateUniqueStationId(_selectedStation.StableId + "-copy");
            copy.DisplayName = _selectedStation.DisplayName + "（副本）";
            copy.Observation.EvidenceNote = string.Empty;
            copy.SortOrder = _selectedStation.SortOrder + 1;
            Mutate("复制创世验证站", () => _document.Stations.Add(copy));
            _selectedStation = FindStationById(copy.StableId);
        }

        private void DeleteSelectedStation()
        {
            if (_selectedStation == null)
                return;
            CharacterTestGenesisValidationStation target = _selectedStation;
            Mutate("删除创世验证站", () => _document.Stations.Remove(target));
            _selectedStation = null;
        }

        private void MoveSelectedStation(int delta)
        {
            if (_selectedStation == null || delta == 0)
                return;
            int index = _document.Stations.IndexOf(_selectedStation);
            int targetIndex = index + delta;
            if (index < 0 || targetIndex < 0 || targetIndex >= _document.Stations.Count)
                return;
            CharacterTestGenesisValidationStation other = _document.Stations[targetIndex];
            if (!string.Equals(other.GroupId, _selectedStation.GroupId, StringComparison.Ordinal))
                return;
            Mutate("调整创世验证站顺序", () =>
            {
                int order = _selectedStation.SortOrder;
                _selectedStation.SortOrder = other.SortOrder;
                other.SortOrder = order;
            });
        }

        private void BuildStationInspector(
            VisualElement root,
            CharacterTestGenesisValidationStation station)
        {
            root.Add(CreateTextField("Stable ID", station.StableId, value => station.StableId = value, "修改验证站 Stable ID"));
            root.Add(CreateTextField("名称", station.DisplayName, value => station.DisplayName = value, "修改验证站名称"));
            root.Add(CreateToggle("启用", station.Enabled, value => station.Enabled = value, "切换验证站启用状态"));
            root.Add(CreateGroupField(station));
            root.Add(CreateIntegerField("排序", station.SortOrder, value => station.SortOrder = value, "修改验证站排序"));
            root.Add(CreateMultilineField("验证意图", station.Intent, value => station.Intent = value, "修改验证意图"));

            BuildSpatialProgramInspector(root, station);

            Foldout canon = CreateFoldout(root, "Canon 追溯", true);
            canon.Add(CreateTextField("来源路径", station.CanonTrace.SourcePath, value => station.CanonTrace.SourcePath = value, "修改 Canon 来源"));
            canon.Add(CreateTextField("章节", station.CanonTrace.SectionTitle, value => station.CanonTrace.SectionTitle = value, "修改 Canon 章节"));
            canon.Add(CreateMultilineField("设计备注", station.CanonTrace.DesignNote, value => station.CanonTrace.DesignNote = value, "修改 Canon 备注"));

            Foldout bindings = CreateFoldout(root, "场景绑定", true);
            bindings.Add(CreateStringPicker(
                "受体角色",
                CollectCharacterIds(),
                station.Bindings.SubjectCharacter?.StableId,
                value => station.Bindings.SubjectCharacter = CreateCharacterRef(
                    value,
                    station.Bindings.SubjectCharacter),
                "绑定角色"));
            bindings.Add(CreateStringPicker(
                "可动物体",
                CollectMovableIds(),
                station.Bindings.TargetMovableObject?.InstanceStableId,
                value => station.Bindings.TargetMovableObject = CreateMovableRef(
                    value,
                    station.Bindings.TargetMovableObject),
                "绑定可动物体"));
            bindings.Add(CreateStaticPicker(station));
            bindings.Add(CreateStaticSamplesEditor(station));
            bindings.Add(CreateStringPicker(
                "开始点",
                CollectPointIds(),
                station.Bindings.StartPoint?.StableId,
                value => station.Bindings.StartPoint = CreatePointRef(
                    value,
                    station.Bindings.StartPoint),
                "绑定开始点"));
            bindings.Add(CreateStringPicker(
                "重置点",
                CollectPointIds(),
                station.Bindings.ResetPoint?.StableId,
                value => station.Bindings.ResetPoint = CreatePointRef(
                    value,
                    station.Bindings.ResetPoint),
                "绑定重置点"));
            bindings.Add(CreateStringPicker(
                "观察区域",
                CollectRegionIds(),
                station.Bindings.ObservationRegion?.StableId,
                value => station.Bindings.ObservationRegion = CreateRegionRef(
                    value,
                    station.Bindings.ObservationRegion),
                "绑定观察区域"));

            Foldout scenario = CreateFoldout(root, "Typed Scenario", true);
            scenario.Add(CreateScenarioTypeField(station));
            scenario.Add(CreateScenarioBindingMetadata(station));
            BuildScenarioPayloadFields(scenario, station);

            Foldout observation = CreateFoldout(root, "观察项", true);
            observation.Add(CreateMultilineField("身体提示", station.Observation.BodyPrompt, value => station.Observation.BodyPrompt = value, "修改身体提示"));
            observation.Add(CreateSignalEditor(station));
            observation.Add(CreateMultilineField("人工证据", station.Observation.EvidenceNote, value => station.Observation.EvidenceNote = value, "修改人工证据"));
            root.Add(new Label("Reset: SessionRebuild（schema v1 固定）"));
            root.Add(CreateMultilineField("备注", station.Notes, value => station.Notes = value, "修改验证站备注"));
        }

        private VisualElement CreateGroupField(CharacterTestGenesisValidationStation station)
        {
            var choices = new List<string>();
            for (int i = 0; i < _document.Groups.Count; i++)
                choices.Add(_document.Groups[i].StableId);
            return CreateStringPicker("分组", choices, station.GroupId, value => station.GroupId = value, "修改验证站分组", false);
        }

        private VisualElement CreateScenarioTypeField(CharacterTestGenesisValidationStation station)
        {
            var choices = new List<string>();
            for (int i = 0; i < CharacterTestGenesisValidationAuthoringScenarioRegistry.All.Count; i++)
                choices.Add(CharacterTestGenesisValidationAuthoringScenarioRegistry.All[i].TypeId);
            return CreateStringPicker(
                "类型",
                choices,
                station.Scenario.TypeId,
                value =>
                {
                    for (int i = 0; i < CharacterTestGenesisValidationAuthoringScenarioRegistry.All.Count; i++)
                    {
                        ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor =
                            CharacterTestGenesisValidationAuthoringScenarioRegistry.All[i];
                        if (string.Equals(descriptor.TypeId, value, StringComparison.Ordinal))
                        {
                            station.Scenario = descriptor.CreateDefaultScenario();
                            station.Observation.RequiredSignalIds.Clear();
                            return;
                        }
                    }
                },
                "修改 Scenario 类型",
                false);
        }

        private static VisualElement CreateScenarioBindingMetadata(
            CharacterTestGenesisValidationStation station)
        {
            if (!CharacterTestGenesisValidationAuthoringScenarioRegistry.TryGet(
                    station.Scenario.TypeId,
                    station.Scenario.SchemaVersion,
                    out ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor))
                return new Label("Binding metadata: unavailable");
            var values = new List<string>();
            for (int i = 0; i < descriptor.BindingSlots.Count; i++)
            {
                CharacterTestGenesisValidationBindingSlot slot = descriptor.BindingSlots[i];
                values.Add(slot.SlotName + " (" + slot.ValueKind + ")");
            }
            return new Label("Required bindings: " + string.Join(", ", values));
        }

        private void BuildScenarioPayloadFields(
            VisualElement root,
            CharacterTestGenesisValidationStation station)
        {
            station.Scenario.Payload ??= new JObject();
            JObject payload = station.Scenario.Payload;
            if (!CharacterTestGenesisValidationAuthoringScenarioRegistry.TryGet(
                    station.Scenario.TypeId,
                    station.Scenario.SchemaVersion,
                    out ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor))
            {
                root.Add(new HelpBox(
                    "未知 Scenario descriptor，typed payload 只读且禁止保存。",
                    HelpBoxMessageType.Error));
                return;
            }

            for (int i = 0; i < descriptor.InspectorFields.Count; i++)
            {
                CharacterTestGenesisValidationInspectorField field = descriptor.InspectorFields[i];
                switch (field.Kind)
                {
                    case CharacterTestGenesisValidationInspectorFieldKind.String:
                        root.Add(CreateTextField(
                            field.DisplayName,
                            payload.Value<string>(field.PropertyName) ?? string.Empty,
                            value => payload[field.PropertyName] = value,
                            "修改 Scenario " + field.DisplayName));
                        break;
                    case CharacterTestGenesisValidationInspectorFieldKind.PositiveInteger:
                        root.Add(CreateIntegerField(
                            field.DisplayName,
                            payload.Value<int?>(field.PropertyName) ?? 1,
                            value => payload[field.PropertyName] = value,
                            "修改 Scenario " + field.DisplayName));
                        break;
                    case CharacterTestGenesisValidationInspectorFieldKind.Boolean:
                        root.Add(CreateToggle(
                            field.DisplayName,
                            payload.Value<bool?>(field.PropertyName) ?? false,
                            value => payload[field.PropertyName] = value,
                            "修改 Scenario " + field.DisplayName));
                        break;
                    case CharacterTestGenesisValidationInspectorFieldKind.OrderedStringList:
                        root.Add(CreateScenarioStringListEditor(station, field));
                        break;
                }
            }
        }

        private VisualElement CreateScenarioStringListEditor(
            CharacterTestGenesisValidationStation station,
            CharacterTestGenesisValidationInspectorField field)
        {
            var root = new VisualElement();
            root.Add(new Label(field.DisplayName));
            List<string> values = ReadStringList(station.Scenario.Payload[field.PropertyName]);
            for (int i = 0; i < values.Count; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var valueField = new TextField { value = values[i] };
                valueField.style.flexGrow = 1f;
                valueField.RegisterValueChangedCallback(evt => Mutate(
                    "修改 Scenario " + field.DisplayName,
                    () => SetJsonStringListValue(
                        station.Scenario.Payload,
                        field.PropertyName,
                        index,
                        evt.newValue)));
                row.Add(valueField);
                row.Add(CreateButton("↑", () => MoveJsonStringListValue(
                    station,
                    field.PropertyName,
                    index,
                    -1,
                    field.DisplayName)));
                row.Add(CreateButton("↓", () => MoveJsonStringListValue(
                    station,
                    field.PropertyName,
                    index,
                    1,
                    field.DisplayName)));
                row.Add(CreateButton("移除", () => Mutate(
                    "移除 Scenario " + field.DisplayName,
                    () => RemoveJsonStringListValue(
                        station.Scenario.Payload,
                        field.PropertyName,
                        index))));
                root.Add(row);
            }

            var appendRow = new VisualElement();
            appendRow.style.flexDirection = FlexDirection.Row;
            var appendField = new TextField("追加") { value = string.Empty };
            appendField.style.flexGrow = 1f;
            appendRow.Add(appendField);
            appendRow.Add(CreateButton("添加", () =>
            {
                string value = appendField.value;
                if (string.IsNullOrWhiteSpace(value))
                    return;
                Mutate("追加 Scenario " + field.DisplayName, () =>
                {
                    List<string> current = ReadStringList(
                        station.Scenario.Payload[field.PropertyName]);
                    current.Add(value.Trim());
                    station.Scenario.Payload[field.PropertyName] = new JArray(current);
                });
            }));
            root.Add(appendRow);
            return root;
        }

        private VisualElement CreateSignalEditor(CharacterTestGenesisValidationStation station)
        {
            CharacterTestGenesisValidationAuthoringScenarioRegistry.TryGet(
                station.Scenario.TypeId,
                station.Scenario.SchemaVersion,
                out ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor);
            var choices = new List<string> { string.Empty };
            if (descriptor != null)
            {
                for (int i = 0; i < descriptor.AllowedSignalIds.Count; i++)
                    choices.Add(descriptor.AllowedSignalIds[i]);
            }

            var root = new VisualElement();
            root.Add(new Label("Required typed signals"));
            for (int i = 0; i < station.Observation.RequiredSignalIds.Count; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var rowChoices = new List<string>(choices);
                if (!rowChoices.Contains(station.Observation.RequiredSignalIds[i]))
                    rowChoices.Add(station.Observation.RequiredSignalIds[i]);
                var field = new DropdownField(
                    string.Empty,
                    rowChoices,
                    station.Observation.RequiredSignalIds[i]);
                field.style.flexGrow = 1f;
                field.RegisterValueChangedCallback(evt => Mutate(
                    "修改观察 signal",
                    () => station.Observation.RequiredSignalIds[index] = evt.newValue));
                row.Add(field);
                row.Add(CreateButton("↑", () => MoveSignal(station, index, -1)));
                row.Add(CreateButton("↓", () => MoveSignal(station, index, 1)));
                row.Add(CreateButton("移除", () => Mutate(
                    "移除观察 signal",
                    () => station.Observation.RequiredSignalIds.RemoveAt(index))));
                root.Add(row);
            }

            root.Add(CreateStringPicker("追加 typed signal", choices, string.Empty, value =>
            {
                if (!string.IsNullOrEmpty(value)
                    && !station.Observation.RequiredSignalIds.Contains(value))
                    station.Observation.RequiredSignalIds.Add(value);
            }, "追加观察 signal", false));
            return root;
        }

        private static List<string> ReadStringList(JToken token)
        {
            var values = new List<string>();
            if (!(token is JArray array))
                return values;
            for (int i = 0; i < array.Count; i++)
            {
                string value = array[i]?.Value<string>();
                if (value != null)
                    values.Add(value);
            }
            return values;
        }

        private static void SetJsonStringListValue(
            JObject payload,
            string propertyName,
            int index,
            string value)
        {
            List<string> values = ReadStringList(payload[propertyName]);
            if (index < 0 || index >= values.Count)
                return;
            values[index] = value ?? string.Empty;
            payload[propertyName] = new JArray(values);
        }

        private static void RemoveJsonStringListValue(
            JObject payload,
            string propertyName,
            int index)
        {
            List<string> values = ReadStringList(payload[propertyName]);
            if (index < 0 || index >= values.Count)
                return;
            values.RemoveAt(index);
            payload[propertyName] = new JArray(values);
        }

        private void MoveJsonStringListValue(
            CharacterTestGenesisValidationStation station,
            string propertyName,
            int index,
            int offset,
            string displayName)
        {
            List<string> values = ReadStringList(station.Scenario.Payload[propertyName]);
            int target = index + offset;
            if (index < 0 || index >= values.Count || target < 0 || target >= values.Count)
                return;
            Mutate("调整 Scenario " + displayName + "顺序", () =>
            {
                List<string> current = ReadStringList(station.Scenario.Payload[propertyName]);
                string value = current[index];
                current[index] = current[target];
                current[target] = value;
                station.Scenario.Payload[propertyName] = new JArray(current);
            });
        }

        private void MoveSignal(
            CharacterTestGenesisValidationStation station,
            int index,
            int offset)
        {
            int target = index + offset;
            if (index < 0
                || index >= station.Observation.RequiredSignalIds.Count
                || target < 0
                || target >= station.Observation.RequiredSignalIds.Count)
                return;
            Mutate("调整观察 signal 顺序", () =>
            {
                string value = station.Observation.RequiredSignalIds[index];
                station.Observation.RequiredSignalIds[index] =
                    station.Observation.RequiredSignalIds[target];
                station.Observation.RequiredSignalIds[target] = value;
            });
        }

        private VisualElement CreateStaticPicker(
            CharacterTestGenesisValidationStation station)
        {
            var refs = CollectSingleStaticRefs();
            var choices = new List<string> { string.Empty };
            for (int i = 0; i < refs.Count; i++)
                choices.Add(FormatStatic(refs[i]));
            CharacterTestGenesisValidationStaticColliderRef current =
                station.Bindings.TargetStaticCollider;
            return CreateStringPicker(
                "目标静态碰撞体",
                choices,
                current == null ? string.Empty : FormatStatic(current),
                value =>
                {
                    CharacterTestGenesisValidationStaticColliderRef selected = null;
                    for (int i = 0; i < refs.Count; i++)
                    {
                        if (string.Equals(FormatStatic(refs[i]), value, StringComparison.Ordinal))
                        {
                            selected = refs[i];
                            break;
                        }
                    }
                    if (selected != null && station.Bindings.TargetStaticCollider != null)
                    {
                        selected.ExtensionData = CharacterTestGenesisValidationAuthoringDocument
                            .CloneExtensionData(station.Bindings.TargetStaticCollider.ExtensionData);
                    }
                    station.Bindings.TargetStaticCollider = selected;
                },
                "绑定静态碰撞体",
                false);
        }

        private VisualElement CreateStaticSamplesEditor(
            CharacterTestGenesisValidationStation station)
        {
            var root = new VisualElement();
            root.Add(new Label("有序表面样本"));
            for (int i = 0; i < station.Bindings.SampleStaticColliders.Count; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var label = new Label((i + 1) + ". " + FormatStatic(
                    station.Bindings.SampleStaticColliders[i]));
                label.style.flexGrow = 1f;
                row.Add(label);
                row.Add(CreateButton("↑", () => MoveStaticSample(station, index, -1)));
                row.Add(CreateButton("↓", () => MoveStaticSample(station, index, 1)));
                row.Add(CreateButton("移除", () => Mutate(
                    "移除表面样本",
                    () => station.Bindings.SampleStaticColliders.RemoveAt(index))));
                root.Add(row);
            }

            List<CharacterTestGenesisValidationStaticColliderRef> refs = CollectSingleStaticRefs();
            var choices = new List<string> { string.Empty };
            for (int i = 0; i < refs.Count; i++)
                choices.Add(FormatStatic(refs[i]));
            root.Add(CreateStringPicker(
                "追加表面样本",
                choices,
                string.Empty,
                value =>
                {
                    for (int i = 0; i < refs.Count; i++)
                    {
                        if (!string.Equals(FormatStatic(refs[i]), value, StringComparison.Ordinal))
                            continue;
                        station.Bindings.SampleStaticColliders.Add(refs[i]);
                        return;
                    }
                },
                "追加表面样本",
                false));
            return root;
        }

        private void MoveStaticSample(
            CharacterTestGenesisValidationStation station,
            int index,
            int offset)
        {
            int target = index + offset;
            if (index < 0
                || index >= station.Bindings.SampleStaticColliders.Count
                || target < 0
                || target >= station.Bindings.SampleStaticColliders.Count)
                return;
            Mutate("调整表面样本顺序", () =>
            {
                CharacterTestGenesisValidationStaticColliderRef value =
                    station.Bindings.SampleStaticColliders[index];
                station.Bindings.SampleStaticColliders[index] =
                    station.Bindings.SampleStaticColliders[target];
                station.Bindings.SampleStaticColliders[target] = value;
            });
        }

        private VisualElement CreateStringPicker(
            string label,
            List<string> choices,
            string current,
            Action<string> assign,
            string undoLabel,
            bool includeEmpty = true)
        {
            choices ??= new List<string>();
            if (includeEmpty && !choices.Contains(string.Empty))
                choices.Insert(0, string.Empty);
            current ??= string.Empty;
            if (!choices.Contains(current))
                choices.Add(current);
            var field = new DropdownField(label, choices, current);
            field.RegisterValueChangedCallback(evt => Mutate(undoLabel, () => assign(evt.newValue)));
            return field;
        }

        private VisualElement CreateTextField(
            string label,
            string value,
            Action<string> assign,
            string undoLabel)
        {
            var field = new TextField(label) { value = value ?? string.Empty, name = "mx-genesis-field-" + label };
            field.RegisterValueChangedCallback(evt => Mutate(undoLabel, () => assign(evt.newValue)));
            return field;
        }

        private VisualElement CreateMultilineField(
            string label,
            string value,
            Action<string> assign,
            string undoLabel)
        {
            var field = new TextField(label) { value = value ?? string.Empty, multiline = true, name = "mx-genesis-field-" + label };
            field.RegisterValueChangedCallback(evt => Mutate(undoLabel, () => assign(evt.newValue)));
            return field;
        }

        private VisualElement CreateIntegerField(
            string label,
            int value,
            Action<int> assign,
            string undoLabel)
        {
            var field = new IntegerField(label) { value = value };
            field.RegisterValueChangedCallback(evt => Mutate(undoLabel, () => assign(evt.newValue)));
            return field;
        }

        private VisualElement CreateToggle(
            string label,
            bool value,
            Action<bool> assign,
            string undoLabel)
        {
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => Mutate(undoLabel, () => assign(evt.newValue)));
            return field;
        }

        private static Foldout CreateFoldout(VisualElement root, string text, bool value)
        {
            var foldout = new Foldout { text = text, value = value };
            root.Add(foldout);
            return foldout;
        }

        private static Button CreateButton(string text, Action action)
        {
            return new Button(action) { text = text };
        }

        private List<string> CollectCharacterIds()
        {
            var result = new List<string>();
            if (_characters?.Characters == null) return result;
            for (int i = 0; i < _characters.Characters.Count; i++)
            {
                CharacterTestSceneCharacterDefinition item = _characters.Characters[i];
                if (item.Enabled && !string.IsNullOrWhiteSpace(item.StableId)) result.Add(item.StableId);
            }
            return result;
        }

        private List<string> CollectMovableIds()
        {
            var result = new List<string>();
            if (_worldObjects?.MovableInstances == null) return result;
            for (int i = 0; i < _worldObjects.MovableInstances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition item = _worldObjects.MovableInstances[i];
                if (item.Enabled && !string.IsNullOrWhiteSpace(item.InstanceStableId)) result.Add(item.InstanceStableId);
            }
            return result;
        }

        private List<string> CollectPointIds()
        {
            var result = new List<string>();
            if (_worldObjects?.Points == null) return result;
            for (int i = 0; i < _worldObjects.Points.Count; i++)
            {
                LevelWorldObjectPointDefinition item = _worldObjects.Points[i];
                if (item.Enabled && !string.IsNullOrWhiteSpace(item.StableId)) result.Add(item.StableId);
            }
            return result;
        }

        private List<string> CollectRegionIds()
        {
            var result = new List<string>();
            if (_worldObjects?.Regions == null) return result;
            for (int i = 0; i < _worldObjects.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition item = _worldObjects.Regions[i];
                if (item.Enabled && !string.IsNullOrWhiteSpace(item.StableId)) result.Add(item.StableId);
            }
            return result;
        }

        private List<CharacterTestGenesisValidationStaticColliderRef> CollectSingleStaticRefs()
        {
            var result = new List<CharacterTestGenesisValidationStaticColliderRef>();
            if (_worldObjects?.SingleStaticColliders == null) return result;
            for (int i = 0; i < _worldObjects.SingleStaticColliders.Count; i++)
            {
                LevelWorldObjectInstanceDefinition item = _worldObjects.SingleStaticColliders[i];
                if (!item.Enabled) continue;
                result.Add(new CharacterTestGenesisValidationStaticColliderRef
                {
                    SourceKind = CharacterTestGenesisValidationStaticSourceKind.SingleStatic,
                    BodyId = item.BodyId,
                    ColliderId = item.ColliderId > 0 ? item.ColliderId : 1,
                    ExpectedDefinitionId = item.DefinitionId
                });
            }
            return result;
        }

        private static string FormatStatic(CharacterTestGenesisValidationStaticColliderRef value)
        {
            return value == null
                ? string.Empty
                : value.BodyId + ":" + value.ColliderId + " / definition " + value.ExpectedDefinitionId;
        }

        private static CharacterTestGenesisValidationCharacterRef CreateCharacterRef(
            string value,
            CharacterTestGenesisValidationCharacterRef current)
        {
            return string.IsNullOrEmpty(value)
                ? null
                : new CharacterTestGenesisValidationCharacterRef
                {
                    StableId = value,
                    ExtensionData = CharacterTestGenesisValidationAuthoringDocument
                        .CloneExtensionData(current?.ExtensionData)
                };
        }

        private static CharacterTestGenesisValidationMovableObjectRef CreateMovableRef(
            string value,
            CharacterTestGenesisValidationMovableObjectRef current)
        {
            return string.IsNullOrEmpty(value)
                ? null
                : new CharacterTestGenesisValidationMovableObjectRef
                {
                    InstanceStableId = value,
                    ExtensionData = CharacterTestGenesisValidationAuthoringDocument
                        .CloneExtensionData(current?.ExtensionData)
                };
        }

        private static CharacterTestGenesisValidationPointRef CreatePointRef(
            string value,
            CharacterTestGenesisValidationPointRef current)
        {
            return string.IsNullOrEmpty(value)
                ? null
                : new CharacterTestGenesisValidationPointRef
                {
                    StableId = value,
                    ExtensionData = CharacterTestGenesisValidationAuthoringDocument
                        .CloneExtensionData(current?.ExtensionData)
                };
        }

        private static CharacterTestGenesisValidationRegionRef CreateRegionRef(
            string value,
            CharacterTestGenesisValidationRegionRef current)
        {
            return string.IsNullOrEmpty(value)
                ? null
                : new CharacterTestGenesisValidationRegionRef
                {
                    StableId = value,
                    ExtensionData = CharacterTestGenesisValidationAuthoringDocument
                        .CloneExtensionData(current?.ExtensionData)
                };
        }

        private bool TryResolvePrimaryTransform(
            CharacterTestGenesisValidationStation station,
            out Transform transform)
        {
            var transforms = new List<Transform>();
            CollectStationTransforms(station, transforms);
            transform = transforms.Count == 0 ? null : transforms[0];
            return transform != null;
        }

        private void CollectStationTransforms(
            CharacterTestGenesisValidationStation station,
            List<Transform> output)
        {
            output.Clear();
            GameManager manager = FindGameManager();
            Transform root = manager?.WorldObjectSceneRoot;
            if (root == null || station == null)
                return;

            AddCharacterTransform(root, station.Bindings.SubjectCharacter?.StableId, output);
            AddMovableTransform(root, station.Bindings.TargetMovableObject?.InstanceStableId, output);
            AddStaticTransform(root, station.Bindings.TargetStaticCollider, output);
            if (station.Bindings.SampleStaticColliders != null)
            {
                for (int i = 0; i < station.Bindings.SampleStaticColliders.Count; i++)
                    AddStaticTransform(root, station.Bindings.SampleStaticColliders[i], output);
            }
            AddPointTransform(root, station.Bindings.StartPoint?.StableId, output);
            AddPointTransform(root, station.Bindings.ResetPoint?.StableId, output);
            AddRegionTransform(root, station.Bindings.ObservationRegion?.StableId, output);
            AddRegionTransform(root, station.SpatialProgram?.FootprintRegion?.StableId, output);
            AddPointTransform(root, station.SpatialProgram?.EntryPoint?.StableId, output);
            AddPointTransform(root, station.SpatialProgram?.ObservationPoint?.StableId, output);
            AddPointTransform(root, station.SpatialProgram?.ExitPoint?.StableId, output);
        }

        private void AddCharacterTransform(Transform root, string id, List<Transform> output)
        {
            if (string.IsNullOrEmpty(id) || _characters?.Characters == null) return;
            for (int i = 0; i < _characters.Characters.Count; i++)
            {
                CharacterTestSceneCharacterDefinition item = _characters.Characters[i];
                if (string.Equals(item.StableId, id, StringComparison.Ordinal)
                    && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(root, item.TransformRef, out Transform transform))
                {
                    AddUnique(transform, output);
                    return;
                }
            }
        }

        private void AddMovableTransform(Transform root, string id, List<Transform> output)
        {
            if (string.IsNullOrEmpty(id) || _worldObjects?.MovableInstances == null) return;
            for (int i = 0; i < _worldObjects.MovableInstances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition item = _worldObjects.MovableInstances[i];
                if (string.Equals(item.InstanceStableId, id, StringComparison.Ordinal)
                    && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(root, item.TransformRef, out Transform transform))
                {
                    AddUnique(transform, output);
                    return;
                }
            }
        }

        private void AddStaticTransform(
            Transform root,
            CharacterTestGenesisValidationStaticColliderRef reference,
            List<Transform> output)
        {
            if (reference == null || _worldObjects?.SingleStaticColliders == null) return;
            for (int i = 0; i < _worldObjects.SingleStaticColliders.Count; i++)
            {
                LevelWorldObjectInstanceDefinition item = _worldObjects.SingleStaticColliders[i];
                if (item.BodyId == reference.BodyId
                    && (item.ColliderId > 0 ? item.ColliderId : 1) == reference.ColliderId
                    && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(root, item.TransformRef, out Transform transform))
                {
                    AddUnique(transform, output);
                    return;
                }
            }
        }

        private void AddPointTransform(Transform root, string id, List<Transform> output)
        {
            if (string.IsNullOrEmpty(id) || _worldObjects?.Points == null) return;
            for (int i = 0; i < _worldObjects.Points.Count; i++)
            {
                LevelWorldObjectPointDefinition item = _worldObjects.Points[i];
                if (string.Equals(item.StableId, id, StringComparison.Ordinal)
                    && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(root, item.TransformRef, out Transform transform))
                {
                    AddUnique(transform, output);
                    return;
                }
            }
        }

        private void AddRegionTransform(Transform root, string id, List<Transform> output)
        {
            if (string.IsNullOrEmpty(id) || _worldObjects?.Regions == null) return;
            for (int i = 0; i < _worldObjects.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition item = _worldObjects.Regions[i];
                if (string.Equals(item.StableId, id, StringComparison.Ordinal)
                    && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(root, item.TransformRef, out Transform transform))
                {
                    AddUnique(transform, output);
                    return;
                }
            }
        }

        private static void AddUnique(Transform transform, List<Transform> output)
        {
            if (transform != null && !output.Contains(transform)) output.Add(transform);
        }

        private int CountStations(string groupId)
        {
            int count = 0;
            for (int i = 0; i < _document.Stations.Count; i++)
                if (string.Equals(_document.Stations[i].GroupId, groupId, StringComparison.Ordinal)) count++;
            return count;
        }

        private string ResolveGroupName(string groupId)
        {
            for (int i = 0; i < _document.Groups.Count; i++)
                if (string.Equals(_document.Groups[i].StableId, groupId, StringComparison.Ordinal))
                    return _document.Groups[i].DisplayName;
            return groupId;
        }

        private static string ResolveScenarioName(CharacterTestGenesisValidationScenario scenario)
        {
            return scenario != null
                && CharacterTestGenesisValidationAuthoringScenarioRegistry.TryGet(
                    scenario.TypeId,
                    scenario.SchemaVersion,
                    out ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor)
                ? descriptor.DisplayName
                : scenario?.TypeId ?? string.Empty;
        }

        private string CreateUniqueStationId(string seed)
        {
            string candidate = seed;
            int suffix = 2;
            while (ContainsStationId(candidate)) candidate = seed + "-" + suffix++;
            return candidate;
        }

        private bool ContainsStationId(string id)
        {
            for (int i = 0; i < _document.Stations.Count; i++)
                if (string.Equals(_document.Stations[i].StableId, id, StringComparison.Ordinal)) return true;
            return false;
        }

        private CharacterTestGenesisValidationStation FindStationById(string id)
        {
            if (string.IsNullOrEmpty(id) || _document?.Stations == null)
                return null;
            for (int i = 0; i < _document.Stations.Count; i++)
            {
                if (string.Equals(_document.Stations[i].StableId, id, StringComparison.Ordinal))
                    return _document.Stations[i];
            }
            return null;
        }

        private void InvalidateSaveCompletionForEdit()
        {
            _activeSaveGeneration = 0;
            _saveState = SaveCompletionState.Idle;
        }

        private string CreateUniqueGroupId(string seed)
        {
            string candidate = seed;
            int suffix = 2;
            while (ContainsGroupId(candidate)) candidate = seed + "-" + suffix++;
            return candidate;
        }

        private bool ContainsGroupId(string id)
        {
            for (int i = 0; i < _document.Groups.Count; i++)
                if (string.Equals(_document.Groups[i].StableId, id, StringComparison.Ordinal)) return true;
            return false;
        }

        private void RefreshAll(string status)
        {
            _status = status;
            SceneView.RepaintAll();
            _activeContext?.RequestRefresh?.Invoke();
        }

        private static GameManager FindGameManager()
        {
            GameManager[] managers = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Exclude);
            if (managers == null || managers.Length == 0) return null;
            string activePath = EditorSceneManager.GetActiveScene().path;
            for (int i = 0; i < managers.Length; i++)
                if (managers[i] != null && string.Equals(managers[i].gameObject.scene.path, activePath, StringComparison.Ordinal)) return managers[i];
            return managers[0];
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static void WriteEnvelope(string assetPath, MxLevelAuthoringDocument envelope)
        {
            string fullPath = ToFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, MxLevelAuthoringJson.SaveToJson(envelope), System.Text.Encoding.UTF8);
        }
    }
}
