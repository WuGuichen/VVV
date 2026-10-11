using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule :
        IMxLevelEditorModule,
        IMxLevelEditorStructuredModule,
        IMxLevelEditorSelectionSyncModule,
        IMxLevelEditorSceneViewModule,
        IMxLevelEditorSceneContextMenuModule,
        IMxLevelEditorPlacementModule,
        IMxLevelEditorToolsModule,
        IMxLevelEditorDirtyDiagnostics,
        IMxLevelEditorSceneChangeAwareModule,
        IMxLevelEditorSceneScope,
        IMxLevelEditorSaveParticipant
    {
        private const string ModuleDisplayName = "CharacterTest 世界对象";
        private const string SerializedModuleName = "CharacterTest World Objects";
        private const float RowHeight = 32f;

        private readonly List<Row> _rows = new List<Row>();
        private MxLevelAuthoringDocument _envelope;
        private LevelWorldObjectAuthoringDocument _document;
        private string _loadedDocumentPath;
        private LevelWorldObjectAuthoringValidationReport _report =
            LevelWorldObjectAuthoringValidationReport.Empty;
        private bool _dirty;
        private bool _sceneDirty;
        private bool _validationQueued;
        private string _status = "";
        private int _selectedMovableDefinitionId = CharacterTestWorldObjectDefinitionIds.MovableSlickLight;
        private int _selectedStaticDefinitionId = CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary;
        private int _selectedPointDefinitionId = CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn;
        private int _selectedRegionDefinitionId = CharacterTestWorldObjectDefinitionIds.RegionStartArea;
        private ListView _listView;
        private VisualElement _inspector;
        private int _inspectorRefreshRevision;
        private VisualElement _diagnostics;
        private Row _selectedRow;
        private string _activeTreeNodeId = "document";
        private MxLevelEditorContext _activeContext;
        private bool _showRuntimeInspector;
        private CharacterTestLevelEditorDocumentUndoSession _undoSession;
        private CharacterTestLevelEditorDocumentUndoSession _definitionUndoSession;
        private CharacterTestWorldObjectDefinitionAuthoringDocument _definitionDocument;
        private CharacterTestWorldObjectDefinitionCompileResult _definitionCompileResult;
        private CharacterTestWorldObjectContentProjection _contentProjection;
        private DefinitionRow _selectedDefinitionRow;
        private bool _definitionsDirty;
        private readonly List<Transform> _unregisteredSceneObjects = new List<Transform>();

        public string ModuleId => CharacterTestWorldObjectAuthoringAdapter.WorldObjectsModuleId;

        public string DisplayName => ModuleDisplayName;

        public string DocumentPath => ResolveActiveSceneDocumentPath();

        internal static string ResolveActiveSceneDocumentPath()
        {
            if (CharacterTestLevelEditorDocumentSession.CurrentPath is { Length: > 0 } current) return current;
            SceneCacheTarget target = FindSceneCacheTarget();
            string path = target?.Cache?.AuthoringJsonPath;
            if (string.IsNullOrWhiteSpace(path))
                path = target?.GameManager?.CharacterSceneCache?.AuthoringJsonPath;
            if (string.IsNullOrWhiteSpace(path))
                path = target?.LegacySource?.AuthoringJsonPath;
            return string.IsNullOrWhiteSpace(path)
                ? CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath
                : path.Trim().Replace('\\', '/');
        }

        public bool IsDirty => _dirty || _sceneDirty || _definitionsDirty;

        public bool IsDocumentDirty => _dirty || _definitionsDirty;

        public bool IsManagedSceneDirty => _sceneDirty;

        public string Status => _status;

        public Transform ManagedSceneRoot => FindSceneCacheTarget()?.Root;

        public bool AppliesToActiveScene => HasActiveSceneAuthoringTarget();

        internal static bool HasActiveSceneAuthoringTarget(string documentPathOverride = null)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
                return false;
            if (!string.IsNullOrWhiteSpace(documentPathOverride))
                return true;

            SceneCacheTarget target = FindSceneCacheTarget();
            if (target?.Root == null
                || target.Root.gameObject.scene.handle != scene.handle)
                return false;

            if (string.Equals(scene.path,
                    CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                    StringComparison.OrdinalIgnoreCase))
                return true;

            string path = target.Cache?.AuthoringJsonPath;
            if (string.IsNullOrWhiteSpace(path))
                path = target.GameManager?.CharacterSceneCache?.AuthoringJsonPath;
            if (string.IsNullOrWhiteSpace(path))
                path = target.LegacySource?.AuthoringJsonPath;
            return !string.IsNullOrWhiteSpace(path)
                && !string.Equals(path.Trim().Replace('\\', '/'),
                    CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal int UnregisteredSceneObjectCountForTests => _unregisteredSceneObjects.Count;
        internal LevelWorldObjectAuthoringValidationReport ValidationReportForTests => _report;
        internal string EnvelopeJsonForTests => _envelope == null ? string.Empty : MxLevelAuthoringJson.SaveToJson(_envelope);
        internal bool ContentProjectionInitializedForTests => _contentProjection != null;
        internal bool DefinitionCompileSucceededForTests => _definitionCompileResult?.Success == true;

        public void OnEnable()
        {
            EnsureLoaded();
            EnsureUndoSession();
        }

        public VisualElement CreateContent(MxLevelEditorWindow window)
        {
            EnsureLoaded();
            RebuildRows();

            var root = new VisualElement { name = "character-test-world-object-level-editor" };
            root.style.flexGrow = 1;
            root.style.minHeight = 0;
            root.style.minWidth = 0;
            root.style.flexDirection = FlexDirection.Column;

            BuildModuleToolbar(window, root);

            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.minHeight = 0;
            root.Add(body);

            _listView = new ListView(_rows, RowHeight, MakeRow, BindRow)
            {
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight
            };
            _listView.style.width = 420;
            _listView.style.flexShrink = 0;
            _listView.style.minHeight = 0;
            _listView.selectionChanged += OnSelectionChanged;
            body.Add(_listView);

            _inspector = new ScrollView(ScrollViewMode.Vertical);
            _inspector.style.flexGrow = 1;
            _inspector.style.minWidth = 0;
            _inspector.style.minHeight = 0;
            _inspector.style.paddingLeft = 10;
            _inspector.style.paddingRight = 10;
            body.Add(_inspector);

            _diagnostics = new VisualElement();
            _diagnostics.style.flexShrink = 0;
            _diagnostics.style.maxHeight = 160;
            _diagnostics.style.paddingLeft = 8;
            _diagnostics.style.paddingRight = 8;
            _diagnostics.style.paddingTop = 6;
            root.Add(_diagnostics);

            RefreshInspector();
            RefreshDiagnostics();
            return root;
        }

        public void OpenJson()
        {
            EnsureLoaded();
            string fullPath = ToFullPath(DocumentPath);
            if (!File.Exists(fullPath))
            {
                _status = "JSON 文件不存在：" + DocumentPath;
                return;
            }

            Object asset = AssetDatabase.LoadAssetAtPath<TextAsset>(DocumentPath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
            else
            {
                EditorUtility.RevealInFinder(fullPath);
            }

            _status = "已打开：" + DocumentPath;
        }

        public void Reload()
        {
            if (_document != null && !string.Equals(_loadedDocumentPath, DocumentPath, StringComparison.OrdinalIgnoreCase)
                && (_dirty || _triggerDirty || _definitionsDirty))
                throw new InvalidOperationException("世界对象草稿仍属于 " + _loadedDocumentPath + "；当前文档为 "
                    + DocumentPath + "。未保存草稿保留，未自动重载到其他关卡。");
            if (!string.Equals(_loadedDocumentPath, DocumentPath, StringComparison.OrdinalIgnoreCase))
                { _undoSession?.Retire(); _undoSession = null; }
            _envelope = LoadOrCreateEnvelope();
            _document = ExtractDocument(_envelope);
            _loadedDocumentPath = DocumentPath;
            LoadTriggerDocumentFromEnvelope();
            LoadDefinitions();
            _report = ValidateLoadedDocument();
            _dirty = false;
            _triggerDirty = false;
            _definitionsDirty = false;
            _sceneDirty = EditorSceneManager.GetActiveScene().isDirty;
            _status = "已重新加载：" + DocumentPath;
            RebuildRows();
            EnsureUndoSession();
            _undoSession.ResetBaseline();
            EnsureDefinitionUndoSession();
            _definitionUndoSession.ResetBaseline();
        }

        public void Save()
        {
            MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { this },
                out _);
        }

        bool IMxLevelEditorSaveParticipant.TryPrepareSave(out string failureMessage)
        {
            EnsureLoaded();
            RefreshEnvelopeFromDiskPreservingCurrentModule();
            ForceValidate();
            if (_report.HasErrors)
            {
                failureMessage = BuildValidationBlockedStatus("保存");
                _status = failureMessage;
                return false;
            }

            CompileDefinitions();
            if (_definitionCompileResult == null || !_definitionCompileResult.Success)
            {
                failureMessage = "对象定义存在错误，无法保存。";
                _status = failureMessage;
                return false;
            }

            SyncEnvelopeFromDocument();
            SceneCacheTarget combatTarget = FindSceneCacheTarget();
            if (!CharacterTestWorldObjectAuthoringAdapter.TryCompileCombatLevelCandidate(
                    _envelope,
                    combatTarget == null ? null : combatTarget.Root,
                    ActiveDefinitionCatalog,
                    out _,
                    out LevelWorldObjectAuthoringValidationReport combatReport))
            {
                _report = _report.Append(combatReport.Issues);
                failureMessage = BuildValidationBlockedStatus("保存");
                _status = failureMessage;
                return false;
            }

            SceneCacheTarget target = FindSceneCacheTarget();
            if (!TryCompilePreparedSceneCaches(
                    target,
                    out _,
                    out failureMessage))
            {
                _status = failureMessage;
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        void IMxLevelEditorSaveParticipant.SavePreparedChanges()
        {
            RefreshEnvelopeFromDiskPreservingCurrentModule();
            SyncEnvelopeFromDocument();

            SceneCacheTarget target = FindSceneCacheTarget();
            if (!TryCompilePreparedSceneCaches(
                    target,
                    out CharacterTestGameplayTriggerSceneCacheData
                        triggerCacheData,
                    out string failureMessage))
            {
                throw new InvalidOperationException(failureMessage);
            }

            WriteEnvelope(DocumentPath, _envelope);
            AssetDatabase.ImportAsset(DocumentPath);
            if (_definitionsDirty)
                CharacterTestWorldObjectDefinitionArtifactsBuilder.Save(_definitionDocument);

            Undo.RecordObject(
                target.DirtyObject,
                "保存并同步 CharacterTest 世界对象");
            bool applied = target.IsGameManager
                ? CharacterTestWorldObjectAuthoringAdapter.ApplyEnvelopeToCache(
                    _envelope,
                    target.Cache,
                    target.Root,
                    ActiveDefinitionCatalog,
                    DocumentPath,
                    out _report)
                : CharacterTestWorldObjectAuthoringAdapter.ApplyEnvelopeToSource(
                    _envelope,
                    target.LegacySource,
                    ActiveDefinitionCatalog,
                    DocumentPath,
                    out _report);
            if (!applied)
                throw new InvalidOperationException(
                    BuildValidationBlockedStatus("保存"));

            if (target.IsGameManager)
            {
                target.GameManager.GameplayTriggerSceneCache.Configure(
                    triggerCacheData);
            }
            EditorUtility.SetDirty(target.DirtyObject);
            EditorSceneManager.MarkSceneDirty(target.GameObject.scene);
        }

        void IMxLevelEditorSaveParticipant.CompleteCoordinatedSave(
            bool succeeded,
            string message)
        {
            if (!succeeded)
            {
                _status = message;
                return;
            }

            _dirty = false;
            _triggerDirty = false;
            _definitionsDirty = false;
            _sceneDirty = false;
            _status = message;
            EnsureUndoSession();
            _undoSession.ResetBaseline();
            EnsureDefinitionUndoSession();
            _definitionUndoSession.ResetBaseline();
        }

        public void Validate()
        {
            ForceValidate();
            _status = "校验完成：错误="
                + _report.ErrorCount
                + "，警告="
                + _report.WarningCount;
        }

        public void ApplyToScene()
        {
            MxLevelEditorSaveCoordinator.TrySaveModules(
                new IMxLevelEditorModule[] { this },
                out _);
        }

        private bool TryCompilePreparedSceneCaches(
            SceneCacheTarget target,
            out CharacterTestGameplayTriggerSceneCacheData triggerCacheData,
            out string failureMessage,
            bool synchronizeEnvelope = true)
        {
            triggerCacheData = null;
            if (target == null
                || target.Root == null
                || target.GameObject == null
                || !target.GameObject.scene.IsValid()
                || string.IsNullOrWhiteSpace(target.GameObject.scene.path))
            {
                failureMessage =
                    "当前场景中未找到可保存的 CharacterTest 世界对象根节点。";
                return false;
            }

            if (synchronizeEnvelope) SyncEnvelopeFromDocument();
            if (target.IsGameManager)
            {
                if (CharacterTestGameplayTriggerSceneCacheCompiler.TryCompile(
                        _envelope,
                        _document,
                        target.Root,
                        ActiveDefinitionCatalog,
                        out triggerCacheData,
                        out LevelWorldObjectAuthoringValidationReport
                            triggerCacheReport))
                {
                    failureMessage = string.Empty;
                    return true;
                }

                _report = _report.Append(triggerCacheReport.Issues);
                failureMessage = BuildValidationBlockedStatus("保存");
                return false;
            }

            if ((_triggerDocument?.Entries?.Count ?? 0) == 0)
            {
                failureMessage = string.Empty;
                return true;
            }

            _report = _report.Append(new[]
            {
                new LevelWorldObjectAuthoringIssue(
                    LevelWorldObjectAuthoringSeverity.Error,
                    "CT_TRIGGER_CACHE_TARGET_UNSUPPORTED",
                    "$.modules."
                    + CharacterTestLevelGameplayTriggerAuthoringAdapter
                        .ModuleId,
                    "Gameplay Trigger 运行时缓存只能写入 CharacterTest GameManager。")
            });
            failureMessage = BuildValidationBlockedStatus("保存");
            return false;
        }

        private void EnsureLoaded()
        {
            string documentPath = DocumentPath;
            if (_document != null
                && string.Equals(_loadedDocumentPath, documentPath, StringComparison.OrdinalIgnoreCase))
                return;

            if (_document != null)
            {
                Reload();
                return;
            }

            _envelope = LoadOrCreateEnvelope();
            _document = ExtractDocument(_envelope);
            _loadedDocumentPath = documentPath;
            LoadTriggerDocumentFromEnvelope();
            LoadDefinitions();
            _report = ValidateLoadedDocument();
            _dirty = false;
            EnsureUndoSession();
            _undoSession.ResetBaseline();
            EnsureDefinitionUndoSession();
            _definitionUndoSession.ResetBaseline();
        }

        public void CollectManagedSceneTransforms(List<Transform> output)
        {
            if (output == null)
                return;

            EnsureLoaded();
            RebuildRows();
            for (int i = 0; i < _rows.Count; i++)
            {
                if (TryResolveRowTransform(_rows[i], out Transform transform)
                    && transform != null
                    && !output.Contains(transform))
                {
                    output.Add(transform);
                }
            }
        }

        public bool NotifySceneTransformChanged(Transform transform)
        {
            if (transform == null
                || !TryResolveSelection(null, transform.gameObject, out _))
            {
                return false;
            }

            _sceneDirty = true;
            _status = "场景对象已修改，尚未保存场景：" + transform.name;
            return true;
        }

        public void RefreshUnregisteredSceneObjects(
            IReadOnlyList<Transform> allManagedTransforms)
        {
            Transform root = ManagedSceneRoot;
            MxLevelEditorSceneCoverage.CollectUnregisteredRoots(
                root,
                allManagedTransforms,
                _unregisteredSceneObjects);
            if (root != null && root.gameObject.scene.isDirty)
                _sceneDirty = true;
        }

        public void AcknowledgeManagedSceneSaved()
        {
            _sceneDirty = false;
        }

        internal void RefreshUnregisteredSceneObjectsForTests(
            IReadOnlyList<Transform> allManagedTransforms)
        {
            RefreshUnregisteredSceneObjects(allManagedTransforms);
        }

        private void SelectFirstUnregisteredSceneObject()
        {
            for (int i = 0; i < _unregisteredSceneObjects.Count; i++)
            {
                Transform transform = _unregisteredSceneObjects[i];
                if (transform == null)
                    continue;

                Selection.activeGameObject = transform.gameObject;
                EditorGUIUtility.PingObject(transform.gameObject);
                _status = "已选择未登记对象：" + transform.name
                    + "。请使用“添加当前选中”指定它的类型。";
                return;
            }
        }

        private MxLevelAuthoringDocument LoadOrCreateEnvelope()
        {
            string fullPath = ToFullPath(DocumentPath);
            if (File.Exists(fullPath))
                return MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));

            SceneCacheTarget target = FindSceneCacheTarget();
            if (target != null)
            {
                if (target.IsGameManager)
                    return CharacterTestWorldObjectAuthoringAdapter.CreateEnvelopeFromCache(
                        target.Cache,
                        target.Root);

                return CharacterTestWorldObjectAuthoringAdapter.CreateEnvelopeFromSource(target.LegacySource);
            }

            var document = new LevelWorldObjectAuthoringDocument
            {
                LevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                RootPath = "CharacterTestSceneRoot"
            };
            return CharacterTestWorldObjectAuthoringAdapter.CreateEnvelopeFromDocument(document);
        }

        private static LevelWorldObjectAuthoringDocument ExtractDocument(MxLevelAuthoringDocument envelope)
        {
            if (CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                    envelope,
                    out LevelWorldObjectAuthoringDocument document,
                    out _))
            {
                return document;
            }

            return new LevelWorldObjectAuthoringDocument
            {
                LevelId = envelope?.LevelId ?? CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                ScenePath = envelope?.ScenePath ?? CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                RootPath = envelope?.RootTransformRef?.Path ?? envelope?.RootPath ?? "CharacterTestSceneRoot"
            };
        }

        private void SyncEnvelopeFromDocument()
        {
            SceneCacheTarget target = FindSceneCacheTarget();
            _envelope = CharacterTestLevelEditorEnvelopeMerge.MergeWorldObjects(
                _envelope,
                _document,
                target == null ? null : target.Root);
            SyncTriggerEnvelope();
        }

        private void RefreshEnvelopeFromDiskPreservingCurrentModule()
        {
            if (CharacterTestJoinedLevelSaveContext.TryGet(DocumentPath, out var joined))
            { _envelope = joined; SyncEnvelopeFromDocument(); return; }
            string fullPath = ToFullPath(DocumentPath);
            if (File.Exists(fullPath))
                _envelope = MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(fullPath));
            SyncEnvelopeFromDocument();
        }

        internal void SetDocumentForTests(LevelWorldObjectAuthoringDocument document)
        {
            _document = document == null
                ? new LevelWorldObjectAuthoringDocument
                {
                    LevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId,
                    ScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath,
                    RootPath = "CharacterTestSceneRoot"
                }
                : document.Normalize();
            _loadedDocumentPath = DocumentPath;
            SceneCacheTarget target = FindSceneCacheTarget();
            _envelope = CharacterTestWorldObjectAuthoringAdapter.CreateEnvelopeFromDocument(
                _document,
                target == null ? null : target.Root);
            ResetTriggerDocumentForTests();
            _report = ValidateLoadedDocument();
            _dirty = false;
            _triggerDirty = false;
            EnsureUndoSession();
            _undoSession.ResetBaseline();
            RebuildRows();
        }

        private LevelWorldObjectAuthoringValidationReport ValidateLoadedDocument()
        {
            SceneCacheTarget target = FindSceneCacheTarget();
            SyncEnvelopeFromDocument();
            LevelWorldObjectAuthoringValidationReport worldReport;
            if (target != null && target.IsGameManager)
            {
                worldReport = CharacterTestWorldObjectAuthoringAdapter.ValidateEnvelopeForCache(
                    _envelope,
                    target.Cache,
                    target.Root,
                    ActiveDefinitionCatalog);
            }
            else
            {
                worldReport =
                    CharacterTestWorldObjectAuthoringAdapter.ValidateEnvelopeForSource(
                        _envelope,
                        target == null ? null : target.LegacySource,
                        ActiveDefinitionCatalog);
            }

            return AppendTriggerValidation(worldReport, target?.Root);
        }

        private void ForceValidate()
        {
            EnsureLoaded();
            _report = ValidateLoadedDocument();
            _validationQueued = false;
            RefreshDiagnostics();
        }

        private void MarkDirty()
        {
            _dirty = true;
            _undoSession?.SynchronizeCurrentDocument();
            QueueValidation();
            RebuildRows();
            _listView?.Rebuild();
            RefreshDiagnostics();
            if (_listView == null
                && !IsTriggerWorkspaceAttached())
                _activeContext?.RequestRefresh?.Invoke();
        }

        private void EnsureUndoSession()
        {
            if (_undoSession != null || _document == null)
                return;

            _undoSession = new CharacterTestLevelEditorDocumentUndoSession(
                "CharacterTest 世界对象",
                CaptureDocumentSnapshot,
                RestoreDocumentSnapshot,
                RefreshAfterDocumentUndo);
            _undoSession.ResetBaseline();
        }

        private string CaptureDocumentSnapshot()
        {
            SceneCacheTarget target = FindSceneCacheTarget();
            MxLevelAuthoringDocument snapshot =
                CharacterTestWorldObjectAuthoringAdapter.CreateEnvelopeFromDocument(
                    _document,
                    target == null ? null : target.Root);
            if (_triggerModulePresent && _triggerDocument != null)
            {
                snapshot =
                    CharacterTestLevelGameplayTriggerAuthoringAdapter.Upsert(
                        snapshot,
                        _triggerDocument);
            }
            return MxLevelAuthoringJson.SaveToJson(snapshot);
        }

        private void RestoreDocumentSnapshot(string snapshot)
        {
            MxLevelAuthoringDocument envelope = MxLevelAuthoringJson.LoadFromJson(snapshot);
            _envelope = envelope;
            _document = ExtractDocument(envelope);
            LoadTriggerDocumentFromEnvelope();
        }

        private void RefreshAfterDocumentUndo()
        {
            _dirty = !_undoSession.IsAtSavedBaseline;
            _triggerDirty = _triggerModulePresent && _dirty;
            _report = ValidateLoadedDocument();
            RebuildRows();
            _listView?.Rebuild();
            RefreshInspector();
            RefreshDiagnostics();
            SceneView.RepaintAll();
            _activeContext?.RequestRefresh?.Invoke();
        }

        private void QueueValidation()
        {
            if (_validationQueued)
                return;

            _validationQueued = true;
            EditorApplication.delayCall += RunDeferredValidation;
        }

        private void RunDeferredValidation()
        {
            if (!_validationQueued)
                return;

            _validationQueued = false;
            _report = ValidateLoadedDocument();
            RefreshDiagnostics();
        }
    }
}
