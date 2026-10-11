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
    public sealed partial class CharacterTestSceneCharacterLevelEditorModule :
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
        private const string ModuleName = "CharacterTest 场景角色";
        private const float OverlayRadius = 0.28f;
        internal const string EmptyStateRootName = "character-test-character-empty-state";
        internal const string EmptyStateDefaultButtonName = "character-test-character-empty-default";
        internal const string EmptyStatePlaceButtonName = "character-test-character-empty-place";
        internal const string PlacementStartButtonName = "character-test-character-placement-start";
        internal const string PlacementOwnerFieldName = "character-test-character-placement-owner";
        internal const string RuntimeAiPlannerProfileFieldName = "character-test-character-runtime-ai-planner-profile";
        internal const string RuntimeAiPlannerProfileChooseButtonName =
            "character-test-character-runtime-ai-planner-profile-choose";

        private static readonly CharacterTestCharacterControlOwner[] ControlOwnerValues =
        {
            CharacterTestCharacterControlOwner.None,
            CharacterTestCharacterControlOwner.LocalPlayer,
            CharacterTestCharacterControlOwner.AiControlled
        };

        private static readonly string[] ControlOwnerLabels =
        {
            "无控制",
            "本地玩家",
            "Runtime AI Planner"
        };

        private readonly List<Row> _rows = new List<Row>();
        private MxLevelAuthoringDocument _envelope;
        private CharacterTestSceneCharacterAuthoringDocument _document;
        private string _documentPathOverride;
        private string _loadedDocumentPath;
        private bool _synchronizingSpawnPoses;
        private LevelWorldObjectAuthoringValidationReport _report =
            LevelWorldObjectAuthoringValidationReport.Empty;
        private bool _dirty;
        private bool _sceneDirty;
        private bool _validationQueued;
        private string _status = "";
        private string _activeTreeNodeId = "document";
        private MxLevelEditorContext _activeContext;
        private Row _selectedRow;
        private bool _placementActive;
        private bool _showRuntimeInspector;
        private CharacterTestCharacterControlOwner _placementOwner =
            CharacterTestCharacterControlOwner.AiControlled;
        private CharacterTestCharacterDefinitionRegistry _characterDefinitions =
            new CharacterTestCharacterDefinitionRegistry();
        private int _placementCharacterId =
            CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId;
        private CharacterTestLevelEditorDocumentUndoSession _undoSession;
        private readonly List<Transform> _unregisteredSceneObjects = new List<Transform>();

        public string ModuleId => CharacterTestSceneCharacterAuthoringAdapter.CharactersModuleId;

        public string DisplayName => ModuleName;

        public string DocumentPath => !string.IsNullOrEmpty(_documentPathOverride)
            ? _documentPathOverride
            : CharacterTestWorldObjectLevelEditorModule.ResolveActiveSceneDocumentPath();

        public void SetDocumentPath(string path)
        {
            string normalized = string.IsNullOrWhiteSpace(path) ? null : path.Trim().Replace('\\', '/');
            if (string.Equals(_documentPathOverride, normalized, StringComparison.Ordinal))
                return;
            _documentPathOverride = normalized;
            _document = null;
            _envelope = null;
            _undoSession = null;
            _dirty = false;
            _sceneDirty = false;
            _rows.Clear();
        }

        public bool IsDirty => _dirty || _sceneDirty;

        public bool IsDocumentDirty => _dirty;

        public bool IsManagedSceneDirty => _sceneDirty;

        public string Status => _status;

        public Transform ManagedSceneRoot => FindGameManager()?.WorldObjectSceneRoot;

        public bool AppliesToActiveScene =>
            CharacterTestWorldObjectLevelEditorModule.HasActiveSceneAuthoringTarget(_documentPathOverride);

        internal int UnregisteredSceneObjectCountForTests => _unregisteredSceneObjects.Count;

        public bool SupportsSceneViewEditing => true;

        public bool SupportsScenePlacement => true;

        public bool IsScenePlacementActive => _placementActive;

        public void OnEnable()
        {
            EnsureLoaded();
            EnsureUndoSession();
        }

        public VisualElement CreateContent(MxLevelEditorWindow window)
        {
            return null;
        }

        public void BuildModuleToolbar(MxLevelEditorWindow window, VisualElement toolbar)
        {
            EnsureLoaded();
            if (_unregisteredSceneObjects.Count > 0)
            {
                var unmanaged = new ToolbarMenu
                {
                    name = "character-test-character-unregistered-scene-objects",
                    text = "未登记 " + _unregisteredSceneObjects.Count,
                    tooltip = "选择或登记未被关卡文档管理的场景对象。"
                };
                unmanaged.menu.AppendAction("选择第一个", _ =>
                {
                    SelectFirstUnregisteredSceneObject();
                    window?.RepaintRequested();
                });
                unmanaged.menu.AppendSeparator();
                unmanaged.menu.AppendAction("登记第一个为本地玩家", _ =>
                {
                    AddFirstUnregisteredCharacter(CharacterTestCharacterControlOwner.LocalPlayer);
                    window?.RepaintRequested();
                });
                unmanaged.menu.AppendAction("登记第一个为 Runtime AI Planner", _ =>
                {
                    AddFirstUnregisteredCharacter(CharacterTestCharacterControlOwner.AiControlled);
                    window?.RepaintRequested();
                });
                toolbar.Add(unmanaged);
            }

            if (IsDocumentEmpty())
            {
                toolbar.style.flexDirection = FlexDirection.Column;
                toolbar.style.flexWrap = Wrap.Wrap;
                toolbar.style.alignItems = Align.Stretch;
                toolbar.Add(CreateEmptyStatePanel(window));
            }
            else
            {
                toolbar.style.flexDirection = FlexDirection.Row;
                toolbar.style.flexWrap = Wrap.NoWrap;
                toolbar.style.alignItems = Align.Center;
                toolbar.Add(CreateCharacterActionRow(window));
            }
        }

        public void BuildToolsMenu(DropdownMenu menu, MxLevelEditorWindow window)
        {
            menu.AppendAction("打开 Runtime AI Planner", _ =>
            {
                EditorApplication.ExecuteMenuItem(CharacterTestRuntimeAiPlannerWindow.MenuPath);
            });
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
            if (_document != null && !string.Equals(_loadedDocumentPath, DocumentPath, StringComparison.OrdinalIgnoreCase) && _dirty)
                throw new InvalidOperationException("场景角色草稿仍属于 " + _loadedDocumentPath + "；当前文档为 "
                    + DocumentPath + "。未保存草稿保留，未自动重载到其他关卡。");
            if (!string.Equals(_loadedDocumentPath, DocumentPath, StringComparison.OrdinalIgnoreCase))
                { _undoSession?.Retire(); _undoSession = null; }
            _characterDefinitions = new CharacterTestCharacterDefinitionRegistry(
                CharacterTestPlayableCharacterCatalog.Refresh());
            _envelope = LoadOrCreateEnvelope();
            _document = ExtractDocument(_envelope);
            _loadedDocumentPath = DocumentPath;
            _report = ValidateLoadedDocument();
            _dirty = false;
            _sceneDirty = EditorSceneManager.GetActiveScene().isDirty;
            _status = "已重新加载：" + DocumentPath;
            RebuildRows();
            EnsureUndoSession();
            _undoSession.ResetBaseline();
        }

        public void Save()
        {
            MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { this },
                out _);
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

            GameManager gameManager = FindGameManager();
            if (gameManager == null || gameManager.WorldObjectSceneRoot == null)
            {
                failureMessage = "当前场景中未找到 CharacterTest GameManager 场景缓存目标。";
                _status = failureMessage;
                return false;
            }

            if (!gameManager.gameObject.scene.IsValid()
                || string.IsNullOrWhiteSpace(gameManager.gameObject.scene.path))
            {
                failureMessage = "CharacterTest 场景尚未保存到磁盘。";
                _status = failureMessage;
                return false;
            }

            failureMessage = string.Empty;
            return true;
        }

        void IMxLevelEditorSaveParticipant.SavePreparedChanges()
        {
            GameManager gameManager = FindGameManager();
            if (gameManager == null || gameManager.WorldObjectSceneRoot == null)
                throw new InvalidOperationException("CharacterTest 场景缓存目标在保存过程中丢失。");

            RefreshEnvelopeFromDiskPreservingCurrentModule();
            SyncEnvelopeFromDocument();
            WriteEnvelope(DocumentPath, _envelope);
            AssetDatabase.ImportAsset(DocumentPath);

            Undo.RecordObject(gameManager, "保存并应用 CharacterTest 场景角色 JSON");
            bool applied = CharacterTestSceneCharacterAuthoringAdapter.ApplyEnvelopeToCache(
                _envelope,
                gameManager.CharacterSceneCache,
                gameManager.WorldObjectSceneRoot,
                DocumentPath,
                out _report);
            if (!applied)
            {
                throw new InvalidOperationException(BuildValidationBlockedStatus("保存"));
            }

            EditorUtility.SetDirty(gameManager);
            EditorSceneManager.MarkSceneDirty(gameManager.gameObject.scene);
        }

        void IMxLevelEditorSaveParticipant.CompleteCoordinatedSave(
            bool succeeded,
            string message)
        {
            if (!succeeded)
            {
                _status = message;
                RebuildRows();
                _activeContext?.RequestRefresh?.Invoke();
                return;
            }

            GameManager gameManager = FindGameManager();
            _dirty = false;
            _sceneDirty = false;
            RebuildRows();
            EnsureUndoSession();
            _undoSession.ResetBaseline();
            SceneView.RepaintAll();
            _activeContext?.RequestRefresh?.Invoke();
            _status = gameManager == null
                ? message
                : message
                    + " 场景角色="
                    + gameManager.CharacterSceneCache.CharacterInstanceCount
                    + "。";
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
            if (_synchronizingSpawnPoses || transform == null)
                return false;
            EnsureLoaded();
            Transform root = ManagedSceneRoot;
            if (root == null || (transform != root && !transform.IsChildOf(root)))
                return false;

            var changed = new List<(CharacterTestSceneCharacterDefinition character, Transform anchor)>();
            bool managed = false;
            foreach (CharacterTestSceneCharacterDefinition character in _document.Characters)
            {
                if (!CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root, character.TransformRef, out Transform anchor))
                    continue;
                if (anchor != transform && !anchor.IsChildOf(transform) && !transform.IsChildOf(anchor))
                    continue;
                managed = true;
                if (character.SpawnPose == null
                    || !character.SpawnPose.Position.Equals(anchor.position)
                    || !character.SpawnPose.YawDegrees.Equals(anchor.eulerAngles.y))
                {
                    changed.Add((character, anchor));
                }
            }

            if (!managed)
                return false;
            if (changed.Count > 0)
            {
                EnsureUndoSession();
                _synchronizingSpawnPoses = true;
                try
                {
                    using (var transaction = _undoSession.BeginJoinedTransaction(
                               "移动 CharacterTest 出生锚点", Undo.GetCurrentGroup()))
                    {
                        foreach (var entry in changed)
                            entry.character.SpawnPose = CaptureSpawnPose(entry.anchor);
                        _document.Format = CharacterTestSceneCharacterAuthoringDocument.CurrentFormat;
                        _document.SchemaVersion = CharacterTestSceneCharacterAuthoringDocument.CurrentSchemaVersion;
                        MarkDirty();
                        transaction.Commit();
                    }
                }
                finally
                {
                    _synchronizingSpawnPoses = false;
                }
            }
            _sceneDirty = true;
            _status = "场景角色对象已修改，尚未保存场景：" + transform.name;
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
            Transform transform = GetFirstUnregisteredSceneObject();
            if (transform == null)
                return;

            Selection.activeGameObject = transform.gameObject;
            EditorGUIUtility.PingObject(transform.gameObject);
            _status = "已选择未登记对象：" + transform.name + "。";
        }

        private void AddFirstUnregisteredCharacter(CharacterTestCharacterControlOwner owner)
        {
            Transform transform = GetFirstUnregisteredSceneObject();
            if (transform == null)
                return;

            Selection.activeGameObject = transform.gameObject;
            AddTransform(transform, owner, GuessTeamId(owner));
        }

        private Transform GetFirstUnregisteredSceneObject()
        {
            for (int i = 0; i < _unregisteredSceneObjects.Count; i++)
            {
                if (_unregisteredSceneObjects[i] != null)
                    return _unregisteredSceneObjects[i];
            }

            return null;
        }

        public void SelectRootSource()
        {
            GameManager gameManager = FindGameManager();
            if (gameManager == null)
            {
                _status = "当前场景中未找到 CharacterTest GameManager。";
                return;
            }

            Selection.activeObject = gameManager.WorldObjectSceneRoot != null
                ? gameManager.WorldObjectSceneRoot.gameObject
                : gameManager.gameObject;
            EditorGUIUtility.PingObject(Selection.activeObject);
            _status = "已选择 CharacterTest 场景根对象。";
        }

        public IReadOnlyList<MxLevelEditorTreeNode> BuildTree(MxLevelEditorContext context)
        {
            _activeContext = context;
            EnsureLoaded();
            var nodes = new List<MxLevelEditorTreeNode>
            {
                new MxLevelEditorTreeNode("document", "角色"),
                new MxLevelEditorTreeNode("owner:LocalPlayer", "本地玩家", 0, CountOwner(CharacterTestCharacterControlOwner.LocalPlayer)),
                new MxLevelEditorTreeNode("owner:AiControlled", "Runtime AI Planner", 0, CountOwner(CharacterTestCharacterControlOwner.AiControlled)),
                new MxLevelEditorTreeNode("owner:None", "无控制", 0, CountOwner(CharacterTestCharacterControlOwner.None))
            };

            var groups = new SortedSet<string>(StringComparer.Ordinal);
            if (_document?.Characters != null)
            {
                for (int i = 0; i < _document.Characters.Count; i++)
                {
                    string group = _document.Characters[i]?.Group;
                    if (!string.IsNullOrWhiteSpace(group))
                        groups.Add(group.Trim());
                }
            }

            foreach (string group in groups)
                nodes.Add(new MxLevelEditorTreeNode("group:" + group, group, 0, CountGroup(group)));
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
            RebuildRows();
            var rows = new List<MxLevelEditorListRow>();
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (row == null || !RowMatchesTree(row, _activeTreeNodeId))
                    continue;

                rows.Add(new MxLevelEditorListRow(
                    row.Key,
                    row.Kind,
                    row.Group,
                    row.Definition,
                    row.Id,
                    row.Transform,
                    row.Status,
                    row.SearchText,
                    row));
            }

            return rows;
        }

        public void BuildInspector(
            MxLevelEditorContext context,
            MxLevelEditorListRow selectedRow,
            VisualElement inspector)
        {
            _activeContext = context;
            _selectedRow = selectedRow?.Payload as Row;
            inspector.Clear();
            if (_selectedRow == null)
            {
                DrawSummaryInspector(inspector);
                return;
            }

            if (_selectedRow.Index < 0
                || _document?.Characters == null
                || _selectedRow.Index >= _document.Characters.Count)
            {
                DrawSummaryInspector(inspector);
                return;
            }

            DrawCharacterInspector(inspector, _document.Characters[_selectedRow.Index]);
        }

        public void BuildDiagnostics(MxLevelEditorContext context, VisualElement diagnostics)
        {
            _activeContext = context;
            diagnostics.Clear();
            if (_unregisteredSceneObjects.Count > 0)
            {
                diagnostics.Add(new HelpBox(
                    "发现 " + _unregisteredSceneObjects.Count
                    + " 个未登记场景对象。请使用顶部“未登记”菜单选择或登记对象。",
                    HelpBoxMessageType.Warning));
            }
            if (_report == null)
            {
                diagnostics.Add(new HelpBox("暂无校验信息。", HelpBoxMessageType.Info));
                return;
            }

            diagnostics.Add(new Label(
                "摘要：角色="
                + Count(_document?.Characters)
                + "，错误="
                + _report.ErrorCount
                + "，警告="
                + _report.WarningCount));
            if (_report.Issues.Count == 0)
            {
                diagnostics.Add(new HelpBox("角色配置校验通过。", HelpBoxMessageType.Info));
                return;
            }

            if (IsDocumentEmpty() && ContainsOnlyEmptyDocumentWarnings(_report))
            {
                diagnostics.Add(new HelpBox(
                    "暂无角色。请先创建默认角色，或点击“开始放置角色”。",
                    HelpBoxMessageType.Info));
                return;
            }

            for (int i = 0; i < _report.Issues.Count; i++)
            {
                LevelWorldObjectAuthoringIssue issue = _report.Issues[i];
                HelpBoxMessageType type = issue.Severity == LevelWorldObjectAuthoringSeverity.Error
                    ? HelpBoxMessageType.Error
                    : HelpBoxMessageType.Warning;
                var helpBox = new HelpBox(
                    CharacterTestLevelEditorChineseText.DiagnosticText(issue),
                    type)
                {
                    tooltip = CharacterTestLevelEditorChineseText.DiagnosticTooltip(issue)
                };
                diagnostics.Add(helpBox);
                diagnostics.Add(new Button(() => RevealLevelIssue(context.Window, issue.Path + ": " + issue.Message)) { text = "定位角色与字段" });
            }
        }

        public bool TryResolveRowObject(
            MxLevelEditorContext context,
            MxLevelEditorListRow row,
            out Object target)
        {
            target = null;
            if (row?.Payload is not Row localRow || !TryResolveRowTransform(localRow, out Transform transform))
                return false;

            target = transform.gameObject;
            return target != null;
        }

        public bool TryResolveSelection(
            MxLevelEditorContext context,
            Object selectedObject,
            out MxLevelEditorSelectionTarget target)
        {
            target = null;
            Transform transform = ResolveSelectionTransform(selectedObject);
            if (transform == null)
                return false;

            EnsureLoaded();
            RebuildRows();
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (row == null || !TryResolveRowTransform(row, out Transform rowTransform))
                    continue;

                if (rowTransform == transform || transform.IsChildOf(rowTransform))
                {
                    target = new MxLevelEditorSelectionTarget(GetTreeNodeId(row), row.Key);
                    return true;
                }
            }

            return false;
        }

        public void DrawSceneView(MxLevelEditorSceneViewContext context, SceneView sceneView)
        {
            IReadOnlyList<SceneOverlayItem> items = BuildSceneOverlayItemsForTests(context);
            for (int i = 0; i < items.Count; i++)
                DrawSceneOverlayItem(items[i]);
            DrawSceneHintOverlay(BuildSceneHintForTests(context, items), sceneView);
        }

        public void BuildPlacementPanel(
            MxLevelEditorPlacementContext context,
            MxLevelEditorPlacementPanel panel)
        {
            PopupField<string> characterField = CreateCharacterDefinitionPopup(
                "角色定义",
                _placementCharacterId,
                characterId =>
                {
                    _placementCharacterId = characterId;
                    _status = "放置角色定义：" + ResolveCharacterDefinitionLabel(characterId) + "。";
                    SceneView.RepaintAll();
                });
            characterField.AddToClassList("mx-level-editor__placement-field");
            panel.FieldsHost.Add(characterField);

            PopupField<string> ownerField = CreateControlOwnerPopup(
                "控制来源",
                _placementOwner,
                owner =>
                {
                    _placementOwner = owner;
                    _status = "放置角色控制来源: " + GetControlOwnerLabel(owner) + "。";
                    SceneView.RepaintAll();
                },
                PlacementOwnerFieldName,
                false);
            ownerField.AddToClassList("mx-level-editor__placement-field");
            panel.FieldsHost.Add(ownerField);

            panel.AddIconToggle(
                "character-test-placement-grid-snap",
                "网格吸附：仅吸附世界坐标 X/Z；步长读取 Unity Grid and Snap 设置："
                    + CharacterTestLevelEditorPlacementUtility.FormatGridStep(),
                "网",
                EditorGUIUtility.IconContent("Grid.BoxTool")?.image as Texture2D,
                CharacterTestLevelEditorPlacementUtility.GridSnapEnabled,
                enabled =>
                {
                    CharacterTestLevelEditorPlacementUtility.GridSnapEnabled = enabled;
                    _status = enabled
                    ? "已开启网格吸附，步长="
                        + CharacterTestLevelEditorPlacementUtility.FormatGridStep()
                        + "。"
                    : "已关闭网格吸附。";
                    SceneView.RepaintAll();
                });

            Button start = CreateButton("开始放置", () =>
            {
                context?.ClosePlacementSurface();
                BeginScenePlacement(context?.EditorContext);
            });
            start.name = PlacementStartButtonName;
            start.AddToClassList("mx-level-editor__placement-action");
            start.SetEnabled(!_placementActive);
            panel.ActionsHost.Add(start);
        }

        private PopupField<string> CreateCharacterDefinitionPopup(
            string label,
            int selectedId,
            Action<int> setter)
        {
            var labels = new List<string>();
            var ids = new List<int>();
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                _characterDefinitions.Definitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                CharacterTestCharacterDefinition definition = definitions[i];
                ids.Add(definition.CharacterId);
                labels.Add(FormatCharacterDefinitionLabel(definition));
            }

            int index = Mathf.Clamp(ids.IndexOf(selectedId), 0, Math.Max(0, ids.Count - 1));
            var popup = new PopupField<string>(label, labels, index);
            popup.RegisterValueChangedCallback(evt =>
            {
                int selected = labels.IndexOf(evt.newValue);
                if (selected >= 0 && selected < ids.Count)
                    setter(ids[selected]);
            });
            return popup;
        }

        private static string FormatCharacterDefinitionLabel(CharacterTestCharacterDefinition definition)
        {
            string packageId = definition.PrefabKey.PackageId;
            return string.IsNullOrWhiteSpace(packageId)
                ? definition.CharacterId + " · " + definition.DisplayName
                : packageId + " · " + definition.DisplayName;
        }

        public void DrawScenePlacement(
            MxLevelEditorPlacementContext context,
            SceneView sceneView)
        {
            if (!_placementActive
                || sceneView == null
                || Event.current == null
                || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (!TryResolveCharacterPlacementPosition(
                    Event.current.mousePosition,
                    out Vector3 position))
                return;

            CharacterTestCharacterControlOwner previewOwner = ResolvePlacementPreviewOwner();
            if (_characterPlacementOperation != CharacterPlacementOperation.Create)
            {
                CharacterTestLevelEditorMeshPreview.DrawGameObjectAtPosition(
                    _characterPlacementSourceObject,
                    position);
            }
            Handles.color = ResolveOwnerColor(previewOwner);
            Handles.DrawWireDisc(position, Vector3.up, OverlayRadius);
            Handles.Label(
                position + Vector3.up * 0.25f,
                BuildPlacementPreviewLabel(previewOwner),
                SceneLabelStyle);
        }

        public void HandleScenePlacement(
            MxLevelEditorPlacementContext context,
            SceneView sceneView)
        {
            if (!_placementActive || Event.current == null)
                return;

            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
                return;
            }

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                CancelScenePlacement(context?.EditorContext);
                GUIUtility.hotControl = 0;
                current.Use();
                context?.EditorContext?.RequestRefresh?.Invoke();
                return;
            }

            if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
            {
                if (ShouldCaptureScenePlacementPointerEvent(current.type, current.button, current.alt))
                    current.Use();
                sceneView?.Repaint();
                return;
            }

            if (current.type == EventType.MouseUp
                && ShouldCaptureScenePlacementPointerEvent(current.type, current.button, current.alt))
            {
                if (GUIUtility.hotControl == controlId)
                    GUIUtility.hotControl = 0;
                current.Use();
                sceneView?.Repaint();
                return;
            }

            if (current.type != EventType.MouseDown
                || !ShouldCaptureScenePlacementPointerEvent(current.type, current.button, current.alt))
                return;

            GUIUtility.hotControl = controlId;
            current.Use();
            if (!TryResolveCharacterPlacementPosition(
                    current.mousePosition,
                    out Vector3 position))
            {
                _status = "无法解析放置位置。";
                sceneView?.Repaint();
                return;
            }

            CharacterPlacementResult result = CommitCharacterPlacement(position);
            _status = result.Status;
            if (result.Success)
            {
                _placementActive = false;
                ResetCharacterPlacementOperation();
                context?.EditorContext?.RequestRefresh?.Invoke();
            }

            sceneView?.Repaint();
        }

        public void CancelScenePlacement(MxLevelEditorContext context)
        {
            _placementActive = false;
            ResetCharacterPlacementOperation();
            _status = "已取消角色放置。";
            GUIUtility.hotControl = 0;
            SceneView.RepaintAll();
        }

        internal VisualElement CreateEmptyStatePanelForTests(MxLevelEditorWindow window)
        {
            return CreateEmptyStatePanel(window);
        }

        internal void SetDocumentForTests(CharacterTestSceneCharacterAuthoringDocument document)
        {
            _document = document == null
                ? CreateEmptyCharacterDocument()
                : document.Normalize();
            _envelope = CharacterTestSceneCharacterAuthoringAdapter.CreateEnvelopeFromDocument(_document);
            _loadedDocumentPath = DocumentPath;
            _report = ValidateLoadedDocument();
            _dirty = false;
            _sceneDirty = false;
            EnsureUndoSession();
            _undoSession.ResetBaseline();
            RebuildRows();
        }

        internal static string GetControlOwnerLabelForTests(CharacterTestCharacterControlOwner owner)
        {
            return GetControlOwnerLabel(owner);
        }

        internal static bool ShouldCaptureScenePlacementPointerEventForTests(
            EventType eventType,
            int button,
            bool alt)
        {
            return ShouldCaptureScenePlacementPointerEvent(eventType, button, alt);
        }

        internal void ConfigurePlacementForTests(
            CharacterTestCharacterControlOwner owner,
            bool active)
        {
            _placementOwner = owner;
            _placementActive = active;
        }

        private static bool ShouldCaptureScenePlacementPointerEvent(
            EventType eventType,
            int button,
            bool alt)
        {
            if (alt || button != 0)
                return false;

            return eventType == EventType.MouseDown
                || eventType == EventType.MouseUp
                || eventType == EventType.MouseDrag;
        }

        internal CharacterTestSceneCharacterAuthoringDocument CreateDefaultTemplateForTests()
        {
            CreateDefaultTemplate();
            return _document;
        }

        internal IReadOnlyList<SceneOverlayItem> BuildSceneOverlayItemsForTests(
            MxLevelEditorSceneViewContext context)
        {
            EnsureLoaded();
            var items = new List<SceneOverlayItem>();
            IReadOnlyList<MxLevelEditorListRow> rows = context?.ScopeRows == null
                ? Array.Empty<MxLevelEditorListRow>()
                : context.ScopeRows;
            string selectedKey = context?.SelectedRow?.Key ?? string.Empty;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i]?.Payload is not Row row)
                    continue;

                if (!TryResolveRowTransform(row, out Transform transform))
                    continue;

                items.Add(new SceneOverlayItem(
                    rows[i],
                    transform.position,
                    row.Title,
                    row.Owner,
                    row.Enabled,
                    string.Equals(rows[i].Key, selectedKey, StringComparison.Ordinal)));
            }

            return items;
        }

        internal SceneHintViewModel BuildSceneHintForTests(MxLevelEditorSceneViewContext context)
        {
            return BuildSceneHintForTests(context, BuildSceneOverlayItemsForTests(context));
        }

        internal SceneHintViewModel BuildSceneHintForTests(
            MxLevelEditorSceneViewContext context,
            IReadOnlyList<SceneOverlayItem> items)
        {
            EnsureLoaded();
            int totalCount = items == null ? 0 : items.Count;
            int enabledCount = 0;
            int aiCount = 0;
            for (int i = 0; i < totalCount; i++)
            {
                SceneOverlayItem item = items[i];
                if (item == null)
                    continue;

                if (item.Enabled)
                    enabledCount++;
                if (item.Owner == CharacterTestCharacterControlOwner.AiControlled)
                    aiCount++;
            }

            string summary = totalCount == 0
                ? "暂无 CharacterTest 场景角色；可在窗口中创建默认角色或开启角色放置。"
                : "可见角色 " + totalCount + " / 启用 " + enabledCount + " / Runtime AI Planner 控制 " + aiCount;
            string selected = BuildSceneHintSelectedText(context, items);
            string operation = _placementActive
                ? "放置中：左键放置 " + GetControlOwnerLabel(_placementOwner) + "，Esc 或“取消放置”退出。"
                : "选中列表行会高亮 Scene 标记；点击“开始放置角色”后左键放置。";
            const string ai =
                "Runtime AI Planner 控制的角色可单独指定行为配置；未指定时继承场景级配置，重建 CharacterTest 会话后生效。";
            CharacterTestSceneCharacterCacheSyncStatus syncStatus = BuildCacheSyncStatus();
            return new SceneHintViewModel(
                "CharacterTest 场景角色",
                summary,
                selected,
                operation,
                ai + "\n" + syncStatus.Message,
                totalCount == 0);
        }

        internal sealed class SceneHintViewModel
        {
            public SceneHintViewModel(
                string title,
                string summary,
                string selected,
                string operation,
                string ai,
                bool isEmpty)
            {
                Title = title ?? string.Empty;
                Summary = summary ?? string.Empty;
                Selected = selected ?? string.Empty;
                Operation = operation ?? string.Empty;
                Ai = ai ?? string.Empty;
                IsEmpty = isEmpty;
            }

            public string Title { get; }
            public string Summary { get; }
            public string Selected { get; }
            public string Operation { get; }
            public string Ai { get; }
            public bool IsEmpty { get; }
        }

        internal sealed class Row
        {
            public Row(
                int index,
                string kind,
                string group,
                string definition,
                string id,
                string transform,
                string status,
                string title,
                CharacterTestCharacterControlOwner owner,
                int teamId,
                bool enabled)
            {
                Index = index;
                Kind = kind ?? string.Empty;
                Group = group ?? string.Empty;
                Definition = definition ?? string.Empty;
                Id = id ?? string.Empty;
                Transform = transform ?? string.Empty;
                Status = status ?? string.Empty;
                Title = title ?? Id;
                Owner = owner;
                TeamId = teamId;
                Enabled = enabled;
                Key = "character|" + Id + "|" + Index;
                SearchText = (Title
                    + " "
                    + Id
                    + " "
                    + Group
                    + " "
                    + Definition
                    + " "
                    + Transform
                    + " "
                    + GetControlOwnerLabel(owner)
                    + " "
                    + owner).Trim();
            }

            public int Index { get; }
            public string Kind { get; }
            public string Group { get; }
            public string Definition { get; }
            public string Id { get; }
            public string Transform { get; }
            public string Status { get; }
            public string Title { get; }
            public CharacterTestCharacterControlOwner Owner { get; }
            public int TeamId { get; }
            public bool Enabled { get; }
            public string Key { get; }
            public string SearchText { get; }
        }

        internal sealed class SceneOverlayItem
        {
            public SceneOverlayItem(
                MxLevelEditorListRow row,
                Vector3 position,
                string label,
                CharacterTestCharacterControlOwner owner,
                bool enabled,
                bool isSelected)
            {
                Row = row;
                Position = position;
                Label = string.IsNullOrWhiteSpace(label) ? "场景角色" : label.Trim();
                Owner = owner;
                Enabled = enabled;
                IsSelected = isSelected;
            }

            public MxLevelEditorListRow Row { get; }
            public Vector3 Position { get; }
            public string Label { get; }
            public CharacterTestCharacterControlOwner Owner { get; }
            public bool Enabled { get; }
            public bool IsSelected { get; }
        }
    }
}
