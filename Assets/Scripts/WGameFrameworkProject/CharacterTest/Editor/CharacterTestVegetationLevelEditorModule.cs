using System;
using System.Collections.Generic;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Level Editor authoring and unified save over one independent vegetation data asset.</summary>
    internal sealed partial class CharacterTestVegetationLevelEditorModule : IMxLevelEditorModule,
        IMxLevelEditorStructuredModule, IMxLevelEditorPlacementModule, IMxLevelEditorSceneViewModule,
        IMxLevelEditorSceneScope, IMxLevelEditorSceneChangeAwareModule, IMxLevelEditorSaveParticipant,
        IMxLevelEditorDirtyDiagnostics, IMxLevelEditorAssetSavePaths, IMxLevelEditorWindowLifecycleModule
    {
        internal const string Id = "characterTest.vegetation";
        private const string PrefsKey = "MxFramework.LevelEditor.CharacterTest.VegetationBrush.v1";
        private CharacterTestVegetationSource _owner;
        private int _paintTypeId;
        private CharacterTestVegetationType PaintType
        {
            get
            {
                if (_owner == null || _owner.Types.Count == 0) return null;
                foreach (var type in _owner.Types) if (type.Id == _paintTypeId) return type;
                return _owner.Types[0];
            }
        }
        private MxLevelEditorWindow _window;
        private MxLevelEditorContext _context;
        private CharacterTestVegetationBrushSettings _settings = new CharacterTestVegetationBrushSettings();
        private readonly HashSet<int> _selected = new HashSet<int>();
        private readonly HashSet<Material> _editedMaterials = new HashSet<Material>();
        private string _status = "选择基础草，在 Scene 中点放或拖刷。";
        private Label _countLabel;
        private float _batchScale = 1f, _batchYaw;
        private double _lastStrokeMs;

        [InitializeOnLoadMethod]
        private static void Register() => MxLevelEditorModuleRegistry.Register(new CharacterTestVegetationLevelEditorModule());
        public string ModuleId => Id;
        public string DisplayName => "场景植被";
        public string DocumentPath => _owner != null ? _owner.gameObject.scene.path : SceneManager.GetActiveScene().path;
        public bool IsDirty => IsManagedSceneDirty || _editedMaterials.Count > 0 || (_owner != null && EditorUtility.IsDirty(_owner.UndoTarget));
        public bool IsDocumentDirty => false;
        public bool IsManagedSceneDirty => _owner != null && _owner.gameObject.scene.isDirty;
        public string Status => _status;
        public Transform ManagedSceneRoot => _owner != null ? _owner.transform : CharacterTestSceneRenderingLevelEditorModule.FindUniqueOwner(SceneManager.GetActiveScene())?.transform;
        public bool AppliesToActiveScene => CharacterTestSceneRenderingLevelEditorModule.FindUniqueOwner(SceneManager.GetActiveScene()) != null ||
            CharacterTestVegetationAssetAuthoring.Find(SceneManager.GetActiveScene()) != null;
        public bool SupportsScenePlacement => _owner != null && _owner.Types.Count > 0 && !EditorApplication.isPlayingOrWillChangePlaymode;
        public bool IsScenePlacementActive => _mode != BrushMode.Off;
        public bool SupportsSceneViewEditing => SupportsScenePlacement;

        public void OnEnable()
        {
            FinishGesture(false);
            try
            {
                var found = CharacterTestVegetationAssetAuthoring.Find(SceneManager.GetActiveScene());
                if (_owner != found) { _selected.Clear(); _mode = BrushMode.Off; }
                _owner = found;
            }
            catch (Exception e) { _owner = null; _status = e.Message; }
            if (EditorPrefs.HasKey(PrefsKey))
            {
                try { JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(PrefsKey), _settings); } catch (ArgumentException) { }
            }
            _editedMaterials.RemoveWhere(material => material == null || !EditorUtility.IsDirty(material));
            _settings.Sanitize();
            Undo.undoRedoPerformed -= OnUndoRedo; Undo.undoRedoPerformed += OnUndoRedo;
        }
        private void OnUndoRedo()
        {
            _stroke = null; _gestureActive = false;
            try { _owner = CharacterTestVegetationAssetAuthoring.Find(SceneManager.GetActiveScene()); }
            catch (Exception error) { _owner = null; _status = error.Message; }
            if (_owner?.Data != null) _owner.InvalidatePresentation();
            PruneSelection(); RefreshWindowState(rebuild: true); SceneView.RepaintAll();
        }
        public void DetachWindow(MxLevelEditorWindow window)
        {
            var attached = _context?.Window;
            if (attached == null) attached = _window;
            if (attached != null && !ReferenceEquals(attached, window)) return;
            Undo.undoRedoPerformed -= OnUndoRedo;
            _window = null; _context = null;
            // OnDestroy still needs _owner, the current gesture and dirty materials.
        }
        private void RefreshWindowState(bool rebuild = false)
        {
            if (rebuild && _context?.Window != null) _context.RequestRefresh?.Invoke();
            if (_window != null) _window.RefreshWorkspaceState();
        }
        public void OnDisable()
        {
            FinishGesture(false); Undo.undoRedoPerformed -= OnUndoRedo;
            _owner = null; _window = null; _context = null; _selected.Clear();
        }
        public void BuildModuleToolbar(MxLevelEditorWindow window, VisualElement toolbar) { _window = window; }
        public VisualElement CreateContent(MxLevelEditorWindow window)
        {
            _window = window; OnEnable(); var body = new ScrollView(); BuildControls(body); return body;
        }
        public IReadOnlyList<MxLevelEditorTreeNode> BuildTree(MxLevelEditorContext context)
        { _context = context; return new[] { new MxLevelEditorTreeNode("document", "基础草", count: _owner?.Instances.Count ?? 0) }; }
        public IReadOnlyList<MxLevelEditorListRow> BuildRows(MxLevelEditorContext context, string selectedTreeNodeId)
        {
            _context = context; var rows = new List<MxLevelEditorListRow>();
            if (_owner == null) return rows;
            var cells = new SortedDictionary<(int x, int z), int>();
            foreach (var item in _owner.Instances)
            {
                var key = (Mathf.FloorToInt(item.Position.x / _owner.ChunkSize), Mathf.FloorToInt(item.Position.z / _owner.ChunkSize));
                cells.TryGetValue(key, out int count); cells[key] = count + 1;
            }
            foreach (var cell in cells)
            {
                string label = cell.Key.x + ", " + cell.Key.z;
                rows.Add(new MxLevelEditorListRow("植被块", "基础草", "草地", label, cell.Value + " 簇", "", new Vector2Int(cell.Key.x, cell.Key.z)));
            }
            return rows;
        }
        public void BuildInspector(MxLevelEditorContext context, MxLevelEditorListRow selectedRow, VisualElement inspector)
        {
            _context = context; BuildControls(inspector);
            if (_owner != null && selectedRow?.Payload is Vector2Int cell)
                inspector.Add(new Button(() => SelectCell(cell)) { text = "选择这块草" });
        }
        public void BuildDiagnostics(MxLevelEditorContext context, VisualElement diagnostics)
        {
            if (_owner != null && !_owner.TryValidate(out string issue)) diagnostics.Add(new HelpBox(issue, HelpBoxMessageType.Error));
        }
        public void BuildPlacementPanel(MxLevelEditorPlacementContext context, MxLevelEditorPlacementPanel panel)
        { BuildControls(panel.FieldsHost); }

        private void BuildControls(VisualElement body)
        {
            if (!AppliesToActiveScene) { body.Add(new HelpBox("请打开正式场景或带植被数据 owner 的独立样区。", HelpBoxMessageType.Info)); return; }
            BuildDataBindingControls(body);
            if (_owner == null) { body.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode); return; }
            BuildTypeControls(body);
            if (_owner.Types.Count == 0)
            { body.Add(new HelpBox("先添加一个植被类型，再点放或刷放。", HelpBoxMessageType.Info)); body.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode); return; }
            _countLabel = new Label(); _countLabel.name = "vegetation-count"; body.Add(_countLabel); UpdateCount();
            var typeNames = new List<string>(); int typeIndex = 0;
            for (int i = 0; i < _owner.Types.Count; i++)
            { var type = _owner.Types[i]; typeNames.Add(type.Id + ": " + type.Name); if (type == PaintType) typeIndex = i; }
            var typePicker = new PopupField<string>("植被类型", typeNames, typeIndex) { name = "vegetation-paint-type" };
            typePicker.RegisterValueChangedCallback(e =>
            { FinishGesture(false); int index = typeNames.IndexOf(e.newValue);
                if (index >= 0 && index < _owner.Types.Count) _paintTypeId = _owner.Types[index].Id;
                _window?.RefreshWorkspaceState(); });
            body.Add(typePicker);
            body.Add(new Button(() =>
            {
                FinishGesture(false);
                try { int count = CharacterTestMeadowSampleAuthoring.AppendDefault(_owner); _status = "添加草花样区：" + count + " 株"; Changed(); }
                catch (Exception e) { _status = e.Message; _window?.RefreshWorkspaceState(); }
            }) { text = "添加草花样区", name = "vegetation-append-meadow" });
            var modes = new VisualElement(); modes.style.flexDirection = FlexDirection.Row;
            AddMode(modes, "点放", BrushMode.Point); AddMode(modes, "刷放", BrushMode.Paint);
            AddMode(modes, "擦除", BrushMode.Erase); AddMode(modes, "选区", BrushMode.Select);
            modes.Add(new Button(() => SetMode(BrushMode.Off)) { text = "结束" }); body.Add(modes);
            body.Add(new Label("左键操作／拖动刷放，Shift 临时擦除，Esc 取消当前整笔；B 往返 Scene。"));
            var ground = new ObjectField("地面范围") { objectType = typeof(Transform), allowSceneObjects = true, value = _owner.SurfaceRoot, name = "vegetation-ground" };
            ground.RegisterValueChangedCallback(e =>
            {
                FinishGesture(false);
                Transform value = e.newValue as Transform;
                if (value != null && (value.gameObject.scene != _owner.gameObject.scene || value == _owner.transform || value.IsChildOf(_owner.transform) || _owner.transform.IsChildOf(value)))
                { _status = "地面范围须在同一场景内，且不能包含植被数据根。"; ground.SetValueWithoutNotify(_owner.SurfaceRoot); return; }
                Undo.RecordObject(_owner.BindingOwner != null ? _owner.BindingOwner : _owner.UndoTarget, "修改植被地面范围"); _owner.SurfaceRoot = value; Changed();
            }); body.Add(ground);
            AddFloat(body, "半径（米）", _settings.Radius, x => _settings.Radius = x, "vegetation-radius");
            AddFloat(body, "密度（簇／平方米）", _settings.Density, x => _settings.Density = x, "vegetation-density");
            AddFloat(body, "最小间距（米）", _settings.MinimumSpacing, x => _settings.MinimumSpacing = x, "vegetation-spacing");
            var advanced = new Foldout { text = "坡度与随机变化", value = false };
            AddFloat(advanced, "最大坡度（度）", _settings.MaximumSlope, x => _settings.MaximumSlope = x);
            AddFloat(advanced, "最小缩放", _settings.MinimumScale, x => _settings.MinimumScale = x);
            AddFloat(advanced, "最大缩放", _settings.MaximumScale, x => _settings.MaximumScale = x);
            var seed = new IntegerField("随机种子") { value = _settings.Seed, name = "vegetation-seed" };
            seed.RegisterValueChangedCallback(e => { FinishGesture(false); _settings.Seed = e.newValue; SaveSettings(); }); advanced.Add(seed);
            advanced.Add(new Label("朝向随机 0–360°；同一种子和相同操作顺序可复现。")); body.Add(advanced);
            var selected = new Foldout { text = "选区与批量调整", value = true };
            selected.Add(new Button(() => { _selected.Clear(); foreach (var item in _owner.Instances) _selected.Add(item.Id); UpdateCount(); SceneView.RepaintAll(); }) { text = "选择全部草" });
            selected.Add(new Button(() => { _selected.Clear(); UpdateCount(); SceneView.RepaintAll(); }) { text = "清空选择" });
            var scale = new FloatField("缩放倍数") { value = _batchScale }; scale.RegisterValueChangedCallback(e => _batchScale = e.newValue); selected.Add(scale);
            var yaw = new FloatField("旋转增量（度）") { value = _batchYaw }; yaw.RegisterValueChangedCallback(e => _batchYaw = e.newValue); selected.Add(yaw);
            selected.Add(new Button(() =>
            {
                FinishGesture(false);
                try { int count = CharacterTestVegetationAuthoring.Adjust(_owner, _selected, _batchScale, _batchYaw); _status = "已调整 " + count + " 簇草。"; Changed(); }
                catch (Exception e) { _status = e.Message; _window?.RefreshWorkspaceState(); }
            }) { text = "应用到选中草", name = "vegetation-batch-apply" });
            selected.Add(new Button(DeleteSelection) { text = "删除选中草" }); body.Add(selected);
            var stats = new Foldout { text = "显示距离与统计", value = false };
            var drawSize = new IntegerField("单次绘制容量") { value = _owner.DrawBatchSize, name = "vegetation-draw-capacity" };
            drawSize.RegisterValueChangedCallback(e =>
            {
                FinishGesture(false);
                if (e.newValue <= 0) { drawSize.SetValueWithoutNotify(_owner.DrawBatchSize); return; }
                Undo.RecordObject(_owner.UndoTarget, "修改植被绘制容量"); _owner.SetDrawBatchSize(e.newValue); Changed();
            });
            stats.Add(drawSize);
            stats.Add(new Label("默认 Indirect 每次 8192 簇；空间块只负责裁剪。设备或 Shader 不支持时回退容量至多 256。"));
            if (_owner.Types.Count > 0)
            {
                var distance = new FloatField("显示距离（米）") { value = PaintType.DrawDistance };
                distance.RegisterValueChangedCallback(e =>
                { FinishGesture(false); Undo.RecordObject(_owner.UndoTarget, "修改植被显示距离"); PaintType.DrawDistance = float.IsFinite(e.newValue) ? Mathf.Max(1, e.newValue) : 100; Changed(); });
                stats.Add(distance);
            }
            var metrics = new Label(); stats.Add(metrics);
            stats.schedule.Execute(() =>
            {
                if (_owner == null || !stats.value) return;
                var r = _owner.Renderer; r.EnsureBuilt();
                metrics.text = "空间块 " + r.ChunkCount + "，类型裁剪组 " + r.Batches.Count +
                    "\n最近相机 " + (r.LastCamera != null ? r.LastCamera.name : "尚未提交") +
                    "：" + (r.UsingGpuClassification ? "GPU 逐实例分类／Indirect" : r.UsingIndirect ? "CPU 分类／Indirect" : "实例化回退") + "，实际容量 " + r.EffectiveBatchCapacity +
                    (r.CountsAvailable ? "\nAPI 提交 " + r.SubmittedBatches + " 次／" + r.VisibleInstances + " 簇（不含各 pass 重复绘制）"
                        : "\n预留 API 命令槽 " + r.SubmittedBatches + "，可见实例数待显式异步诊断（不含各 pass 重复绘制）") +
                    "\n最近 CPU 提交 " + r.CpuPreparationMilliseconds.ToString("0.00") + " ms，实例记录上传 " + r.UploadedInstances +
                    "\nGPU buffer " + (r.ResidentGpuBytes / 1024f).ToString("0.0") + " KiB，数据重建 " + r.RebuildMilliseconds.ToString("0.00") +
                    " ms，最近整笔 " + _lastStrokeMs.ToString("0.00") + " ms\n" + r.Issue;
            }).Every(500); body.Add(stats);
            body.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
        }
        private void AddMode(VisualElement row, string label, BrushMode mode)
        { var button = new Button(() => SetMode(mode)) { text = label, name = "vegetation-mode-" + mode }; row.Add(button); }
        private void AddFloat(VisualElement body, string label, float value, Action<float> set, string name = null)
        {
            var field = new FloatField(label) { value = value, name = name };
            field.RegisterValueChangedCallback(e => { FinishGesture(false); set(e.newValue); _settings.Sanitize(); SaveSettings(); }); body.Add(field);
        }
        private void SaveSettings() => EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(_settings));
        private void UpdateCount() { if (_countLabel != null) _countLabel.text = (_owner?.Instances.Count ?? 0) + " 簇草 · 已选 " + _selected.Count + " 簇"; }
        private void PruneSelection()
        {
            if (_owner == null) { _selected.Clear(); return; }
            var existing = new HashSet<int>(); foreach (var item in _owner.Instances) existing.Add(item.Id);
            _selected.IntersectWith(existing); UpdateCount();
        }
        private void SelectCell(Vector2Int cell)
        {
            _selected.Clear();
            foreach (var item in _owner.Instances)
                if (Mathf.FloorToInt(item.Position.x / _owner.ChunkSize) == cell.x && Mathf.FloorToInt(item.Position.z / _owner.ChunkSize) == cell.y) _selected.Add(item.Id);
            UpdateCount(); SceneView.RepaintAll();
        }
        private void DeleteSelection()
        {
            FinishGesture(false); if (_selected.Count == 0) return;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("删除选中植被");
            Undo.RegisterCompleteObjectUndo(_owner.UndoTarget, "删除选中植被");
            int count = _owner.EditableInstances.RemoveAll(item => _selected.Contains(item.Id));
            Undo.CollapseUndoOperations(group); _selected.Clear(); _status = "已删除 " + count + " 簇草。"; Changed();
        }
        private void Initialize()
        {
            try
            {
                _owner = CharacterTestVegetationAssetAuthoring.Create(SceneManager.GetActiveScene());
                EnableTypeInstancing();
                _status = "已创建并绑定独立植被数据资产；选择地面范围后开始编辑，点击顶部保存。";
                Changed(); RefreshWindowState(rebuild: true);
            }
            catch (Exception e) { _status = e.Message; RefreshWindowState(); }
        }
        private void Changed()
        {
            if (_owner != null) { EditorUtility.SetDirty(_owner.UndoTarget); EditorSceneManager.MarkSceneDirty(_owner.gameObject.scene); _owner.InvalidatePresentation(); }
            PruneSelection(); RefreshWindowState(); SceneView.RepaintAll();
        }
        public void OpenJson() => SelectRootSource();
        public void SelectRootSource() { if (_owner != null) Selection.activeGameObject = _owner.gameObject; }
        public void Reload() { OnEnable(); _owner?.InvalidatePresentation(); RefreshWindowState(rebuild: true); }
        public void ApplyToScene() { _owner?.InvalidatePresentation(); SceneView.RepaintAll(); }
        public void Validate() { _status = TryPrepareSave(out string issue) ? "植被数据可保存。" : issue; RefreshWindowState(); }
        public void Save() { FinishGesture(false); MxLevelEditorSaveCoordinator.TrySaveModules(MxLevelEditorModuleRegistry.Modules, out _status); RefreshWindowState(); }
        public void CollectManagedSceneTransforms(List<Transform> output) { if (_owner != null) output.Add(_owner.transform); }
        public bool NotifySceneTransformChanged(Transform transform)
        { if (_owner != null && transform == _owner.transform) { Changed(); return true; } return false; }
        public void RefreshUnregisteredSceneObjects(IReadOnlyList<Transform> allManagedTransforms) { }
        public void AcknowledgeManagedSceneSaved() { }
        public bool TryPrepareSave(out string failureMessage)
        {
            FinishGesture(false); failureMessage = null;
            if (_owner == null) return true; // Unconfigured workspace does not block other scene modules.
            if (EditorApplication.isPlayingOrWillChangePlaymode) { failureMessage = "请在 Edit Mode 保存植被。"; return false; }
            if (!_owner.TryValidate(out failureMessage)) return false;
            try { CharacterTestVegetationAuthoring.ValidateExclusion(_owner.transform, _owner.gameObject.scene); }
            catch (Exception e) { failureMessage = e.Message; return false; }
            return true;
        }
        public void SavePreparedChanges() { if (_owner?.Data != null) AssetDatabase.SaveAssetIfDirty(_owner.Data);  foreach (var material in _editedMaterials) if (material != null) AssetDatabase.SaveAssetIfDirty(material); }
        public void CompleteCoordinatedSave(bool succeeded, string message) { if (succeeded) _editedMaterials.Clear(); _status = message; }
        public void CollectAssetSavePaths(List<string> paths) { if (_owner?.Data != null) paths.Add(AssetDatabase.GetAssetPath(_owner.Data));  foreach (var material in _editedMaterials) if (material != null) paths.Add(AssetDatabase.GetAssetPath(material)); }
    }
}
