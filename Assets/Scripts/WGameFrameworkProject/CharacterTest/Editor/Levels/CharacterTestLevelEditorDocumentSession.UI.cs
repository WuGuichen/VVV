using System;
using System.IO;
using System.Linq;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestLevelEditorDocumentSession
    {
        private bool _showCreate, _copy, _showRules;
        private string _newName = "新关卡", _newId;
        private SceneAsset _newScene;

        public void BuildToolbar(MxLevelEditorWindow window, VisualElement host)
        {
            _activeWindow = window;
            host.style.paddingLeft = 12; host.style.paddingRight = 12; host.style.paddingBottom = 6;
            try
            {
                EnsureLoaded();
                if (!string.IsNullOrEmpty(CurrentPath) && FindManager() != null)
                {
                    _sceneBaseline ??= CharacterTestLevelEditorOwnedSceneSnapshot.Create(MxLevelEditorModuleRegistry.Modules);
                    _sceneBaseline.TrackAddedObjects(MxLevelEditorModuleRegistry.Modules);
                }
                var catalog = CharacterTestLevelAuthoringOperations.Catalog;
                var labels = new[] { "当前 Scene 的原有文档" }.Concat(catalog.Levels.Select(l => l.DisplayName + " / " + l.LevelId)).ToList();
                int selected = catalog.Levels.FindIndex(l => l.LevelId == CurrentLevelId) + 1;
                var row = new VisualElement { name = "mx-level-current-document" }; row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap;
                var picker = new PopupField<string>("当前关卡", labels, Math.Max(0, selected)) { name = "mx-level-document-picker" };
                picker.style.flexGrow = 1;
                picker.RegisterValueChangedCallback(evt => Run(window, () =>
                {
                    int index = labels.IndexOf(evt.newValue);
                    var decision = Prompt(window);
                    if (index == 0)
                    {
                        if (!window.TryPrepareDocumentSwitch(decision, out _status)) return;
                        ResetForTests(); window.ReloadDocumentModules();
                    }
                    else TrySelect(catalog.Levels[index - 1].LevelId, window, decision, out _status);
                }));
                row.Add(picker);
                row.Add(Button("新建", "mx-level-new", () => ShowCreate(window, false)));
                row.Add(Button("复制", "mx-level-copy", () => ShowCreate(window, true)));
                row.Add(Button("检查", "mx-level-check", () => Run(window, () => TryCheck(out _status))));
                row.Add(Button("试玩", "mx-level-play", () => Run(window, () => TryPlay(window, out _status))));
                row.Add(Button("规则", "mx-level-rules", () => { _showRules = !_showRules; window.RepaintRequested(); }));
                row.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
                host.Add(row);
                if (_draft != null)
                    host.Add(new Label("Scene: " + _draft.ScenePath + "  ·  文档: " + CurrentPath + (_metadataDirty ? "  ·  未保存" : "")) { name = "mx-level-document-identity" });
                host.Add(new Label(_status) { name = "mx-level-document-status" });
                if (!string.IsNullOrEmpty(_lastFailure)) host.Add(Button("定位检查问题", "mx-level-locate-issue", () => {
                    foreach (var module in MxLevelEditorModuleRegistry.Modules)
                    {
                        if (module is CharacterTestSceneCharacterLevelEditorModule characters && characters.RevealLevelIssue(window, _lastFailure)) return;
                        if (module is CharacterTestWorldObjectLevelEditorModule world && world.RevealLevelIssue(window, _lastFailure)) return;
                        if (module is CharacterTestGuidanceLevelEditorModule guidance && guidance.RevealLevelIssue(window, _lastFailure)) return;
                        if (module is CharacterTestGenesisValidationLevelEditorModule genesis && genesis.RevealLevelIssue(window, _lastFailure)) return;
                    }
                    _status = "该问题路径暂未支持自动字段定位；已显示规则并打开原文档：" + _lastFailure;
                    _showRules = true; window.RepaintRequested();
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CurrentPath));
                }));
                if (_showCreate) BuildCreate(window, host);
                if (_showRules && _draft != null) BuildRules(window, host);
            }
            catch (Exception error) { host.Add(new HelpBox(error.Message, HelpBoxMessageType.Error)); }
        }

        private static Button Button(string label, string name, Action action)
            => new Button(action) { text = label, name = name };
        private static MxLevelEditorUnsavedDecision Prompt(MxLevelEditorWindow window)
        {
            if (!window.HasPendingDocumentChanges) return MxLevelEditorUnsavedDecision.Cancel;
            int choice = EditorUtility.DisplayDialogComplex("当前关卡有未保存内容", "保存后切换、放弃当前未保存内容，或继续编辑当前关卡。", "保存并继续", "取消", "放弃并继续");
            return choice == 0 ? MxLevelEditorUnsavedDecision.Save : choice == 2 ? MxLevelEditorUnsavedDecision.Discard : MxLevelEditorUnsavedDecision.Cancel;
        }

        private void Run(MxLevelEditorWindow window, Action action)
        {
            try { action(); }
            catch (Exception error) { _status = error.Message; }
            window.RepaintRequested();
        }

        private void ShowCreate(MxLevelEditorWindow window, bool copy)
        {
            if (copy && _draft == null) { _status = "请先选择要复制的关卡。"; window.RepaintRequested(); return; }
            _showCreate = true; _copy = copy; _newName = copy ? "关卡副本" : "新关卡";
            _newId = "level." + Guid.NewGuid().ToString("N").Substring(0, 12);
            _newScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(_draft?.ScenePath ?? EditorSceneManager.GetActiveScene().path);
            window.RepaintRequested();
        }

        private void BuildCreate(MxLevelEditorWindow window, VisualElement host)
        {
            var panel = new VisualElement { name = "mx-level-create-panel" }; panel.style.flexDirection = FlexDirection.Row; panel.style.flexWrap = Wrap.Wrap;
            var name = new TextField("名称") { value = _newName, name = "mx-level-create-name" };
            name.RegisterValueChangedCallback(evt => _newName = evt.newValue); panel.Add(name);
            var id = new TextField("稳定 ID") { value = _newId, name = "mx-level-create-id" };
            id.RegisterValueChangedCallback(evt => _newId = evt.newValue); panel.Add(id);
            var scene = new ObjectField("关联 Scene") { objectType = typeof(SceneAsset), allowSceneObjects = false, value = _newScene, name = "mx-level-create-scene" };
            scene.RegisterValueChangedCallback(evt => _newScene = evt.newValue as SceneAsset); panel.Add(scene);
            panel.Add(Button(_copy ? "创建副本" : "创建关卡", "mx-level-create-commit", () => Run(window, () =>
            {
                if (!window.TryPrepareDocumentSwitch(Prompt(window), out _status)) return;
                CreateDocument(_newId, _newName, _newScene == null ? "" : AssetDatabase.GetAssetPath(_newScene), !_copy);
                _showCreate = false;
                TrySelect(_newId, window, MxLevelEditorUnsavedDecision.Cancel, out _status, previousWorkAlreadyResolved: true);
            })));
            panel.Add(Button("取消", "mx-level-create-cancel", () => { _showCreate = false; window.RepaintRequested(); }));
            host.Add(panel);
            host.Add(new Label(_copy ? "保留正式行为组引用；关卡和实例身份独立。关联同一 Scene 时，共享其环境几何。" : "新建空角色列表，复用当前关卡环境；需布置角色并选择玩家／对手后才能试玩。"));
        }

        internal string CreateDocument(string id, string displayName, string scenePath, bool emptyCharacters)
        {
            CharacterTestLevelEditorDocumentFactory.ValidateId(id);
            if (string.IsNullOrWhiteSpace(scenePath) || !File.Exists(CharacterTestLevelCatalog.SafePath(CharacterTestLevelAuthoringOperations.ProjectRoot, scenePath))) throw new ArgumentException("请选择已保存的关联 Scene。");
            byte[] before = File.ReadAllBytes(CharacterTestLevelAuthoringOperations.DirectoryPath);
            var catalog = CharacterTestLevelAuthoringOperations.Catalog;
            if (!before.SequenceEqual(File.ReadAllBytes(CharacterTestLevelAuthoringOperations.DirectoryPath)))
                throw new IOException("关卡目录已变化，未创建或覆盖文档。");
            if (catalog.Levels.Any(l => l.LevelId == id)) throw new InvalidOperationException("关卡 ID 已存在，未覆盖任何文档。");
            MxLevelAuthoringDocument source;
            if (_draft != null) source = CaptureDraft(MxLevelEditorModuleRegistry.Modules);
            else
            {
                string path = FindManager()?.WorldObjectSceneCache?.AuthoringJsonPath;
                source = !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                    ? MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(path))
                    : catalog.Load(catalog.DefaultLevelId, CharacterTestLevelAuthoringOperations.ProjectRoot, false, out _);
            }
            var document = CharacterTestLevelEditorDocumentFactory.Clone(source, id, emptyCharacters);
            if (document.ScenePath != scenePath)
            {
                var preview = EditorSceneManager.OpenPreviewScene(scenePath);
                try
                {
                    var managers = preview.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<GameManager>(true)).ToArray();
                    if (managers.Length != 1 || managers[0].WorldObjectSceneRoot == null)
                        throw new InvalidOperationException("关联 Scene 需要唯一 GameManager 和关卡根节点。原 Scene 与草稿保持不变。");
                    Reassociate(document, scenePath, managers[0].WorldObjectSceneRoot.name);
                    if (emptyCharacters && document.TryGetModule("characterTest.worldObjects", out var world))
                        foreach (var property in world.Payload.Properties().Where(p => p.Value is JArray)) property.Value = new JArray();
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            string relative = "Assets/Config/MxFramework/Levels/Created/" + id + "/level.json";
            string full = CharacterTestLevelCatalog.SafePath(CharacterTestLevelAuthoringOperations.ProjectRoot, relative);
            if (File.Exists(full)) throw new IOException("目标文件已存在，未覆盖：" + relative);
            bool catalogWritten = false;
            string catalogCandidate = null;
            try
            {
                CharacterTestLevelCatalog.AtomicWrite(full, MxLevelAuthoringJson.SaveToJson(document));
                catalog.Levels.Add(new CharacterTestLevelEntry { LevelId = id, DisplayName = displayName, Path = relative });
                if (!before.SequenceEqual(File.ReadAllBytes(CharacterTestLevelAuthoringOperations.DirectoryPath)))
                    throw new IOException("关卡目录已被其他入口更新，未覆盖其修改。");
                catalogCandidate = JsonConvert.SerializeObject(catalog, Formatting.Indented);
                CharacterTestLevelCatalog.AtomicWrite(CharacterTestLevelAuthoringOperations.DirectoryPath, catalogCandidate);
                catalogWritten = true;
            }
            catch
            {
                if (File.Exists(full)) AssetDatabase.DeleteAsset(relative);
                if (catalogWritten && CharacterTestLevelCatalog.Hash(File.ReadAllText(CharacterTestLevelAuthoringOperations.DirectoryPath)) == CharacterTestLevelCatalog.Hash(catalogCandidate))
                    File.WriteAllBytes(CharacterTestLevelAuthoringOperations.DirectoryPath, before);
                throw;
            }
            AssetDatabase.Refresh();
            return relative;
        }

        private static void Reassociate(MxLevelAuthoringDocument document, string scenePath, string root)
        {
            document.ScenePath = scenePath; document.RootTransformRef = new LevelTransformReference { Path = root }; document.RootPath = root;
            foreach (var module in document.Modules)
            {
                if (module.Payload["scenePath"] != null) module.Payload["scenePath"] = scenePath;
                if (module.Payload["rootPath"] != null) module.Payload["rootPath"] = root;
                foreach (var property in module.Payload.Descendants().OfType<JProperty>().Where(p => p.Name == "globalObjectId")) property.Value = "";
            }
        }

        private void BuildRules(MxLevelEditorWindow window, VisualElement host)
        {
            var panel = new VisualElement { name = "mx-level-rules-panel" };
            panel.Add(new Label("复用现有关卡模块：世界对象中的区域／触发器、创世验证、教学引导。现有场景规则与资源设置仍由 GameManager 的 Scene Profile 提供。"));
            if (_draft.TryGetModule("characterTest.tacticalStage", out var stage))
            {
                BuildExistingStageSettings(window, panel);
                var characterModule = MxLevelEditorModuleRegistry.Modules.OfType<CharacterTestSceneCharacterLevelEditorModule>().Single();
                var actors = (JArray)characterModule.CaptureDraftModules().Single().Payload["characters"];
                foreach (string field in new[] { "playerSpawnStableId", "opponentSpawnStableId" })
                {
                    var ids = new[] { "" }.Concat(actors.OfType<JObject>().Select(a => (string)a["stableId"])).ToArray();
                    var names = new[] { "未指定" }.Concat(actors.OfType<JObject>().Select(a => (string)a["displayName"] + " / " + (string)a["stableId"])).ToList();
                    var picker = new PopupField<string>(field == "playerSpawnStableId" ? "玩家实例" : "战术对手实例", names, Math.Max(0, Array.IndexOf(ids, (string)stage.Payload[field]))) { name = "mx-level-rule-" + field };
                    picker.RegisterValueChangedCallback(evt => {
                        using var change = _undo.BeginTransaction("编辑关卡角色规则");
                        if (!_draft.TryGetModule("characterTest.tacticalStage", out var currentStage)) return;
                        currentStage.Payload[field] = ids[names.IndexOf(evt.newValue)];
                        _metadataDirty = true; _preview = null; change.Commit(); window.RepaintRequested(); });
                    panel.Add(picker);
                }
            }
            panel.Add(Button("选择现有 Scene Profile", "mx-level-scene-profile", () => {
                var manager = FindManager(); if (manager == null) return;
                Selection.activeObject = manager.ConfiguredProfile != null ? (UnityEngine.Object)manager.ConfiguredProfile : manager;
            }));
            panel.Add(Button("发布当前保存版本", "mx-level-publish", () => Run(window, () => TryPublish(out _status))));
            host.Add(panel);
        }
    }
}
