using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    [InitializeOnLoad]
    internal sealed partial class CharacterTestLevelEditorDocumentSession : IMxLevelEditorDocumentOwner, IMxLevelEditorScopedDocumentOwner, IMxLevelEditorDocumentValidationOwner
    {
        internal static readonly CharacterTestLevelEditorDocumentSession Instance = new CharacterTestLevelEditorDocumentSession();
        private static string Key => "mx.character-test.level-editor.document.v1." + CharacterTestLevelCatalog.Hash(Application.dataPath) + ".";
        private MxLevelAuthoringDocument _draft;
        private CharacterTestLevelEditorDocumentUndoSession _undo;
        private string _loadedPath, _sourceHash, _savedMetadata, _status = "选择关卡后，所有模块编辑同一份 Authoring 文档。";
        private bool _metadataDirty;
        private JObject _preview;
        private string _lastFailure;
        private MxLevelEditorWindow _activeWindow;
        private CharacterTestLevelEditorOwnedSceneSnapshot _sceneBaseline;
        internal static string CurrentPath => SessionState.GetString(Key + "path", "");
        internal static string CurrentLevelId => SessionState.GetString(Key + "id", "");
        public bool IsDirty => _metadataDirty;
        public bool AppliesToActiveScene => !string.IsNullOrEmpty(CurrentPath) || FindManager() != null;

        static CharacterTestLevelEditorDocumentSession()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Instance.CaptureReloadRecovery;
        }

        internal static GameManager FindManager()
        {
            string path = EditorSceneManager.GetActiveScene().path;
            var managers = UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include)
                .Where(m => m.gameObject.scene.path == path).ToArray();
            return managers.Length == 1 ? managers[0] : null;
        }

        private void EnsureLoaded()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            if (_draft != null && _loadedPath == CurrentPath) return;
            if (TryRestoreReloadRecovery()) return;
            string full = CharacterTestLevelCatalog.SafePath(CharacterTestLevelAuthoringOperations.ProjectRoot, CurrentPath);
            var source = File.ReadAllText(full);
            _draft = MxLevelAuthoringJson.LoadFromJson(source);
            if (_draft.LevelId != CurrentLevelId) throw new InvalidOperationException("当前关卡身份与文档不一致，请重新选择关卡。");
            _loadedPath = CurrentPath;
            _sourceHash = CharacterTestLevelCatalog.Hash(source);
            _metadataDirty = false;
            _preview = null;
            _savedMetadata = MxLevelAuthoringJson.SaveToJson(_draft);
            CreateMetadataUndo();
        }

        private void CreateMetadataUndo()
        {
            _undo = new CharacterTestLevelEditorDocumentUndoSession("关卡规则", () => MxLevelAuthoringJson.SaveToJson(_draft),
                value => _draft = MxLevelAuthoringJson.LoadFromJson(value), () => {
                    _metadataDirty = !_undo.IsAtSavedBaseline; _preview = null;
                    _activeWindow?.RepaintRequested();
                });
            _undo.ResetBaseline();
            _undo.SetSavedBaseline(_savedMetadata);
        }

        internal bool TrySelect(string levelId, MxLevelEditorWindow window, MxLevelEditorUnsavedDecision decision, out string message, bool previousWorkAlreadyResolved = false)
        {
            message = string.Empty;
            string previousId = CurrentLevelId, previousPath = CurrentPath;
            var previousDraft = _draft; var previousUndo = _undo;
            string previousLoadedPath = _loadedPath, previousSourceHash = _sourceHash, previousSavedMetadata = _savedMetadata;
            bool previousDirty = _metadataDirty;
            var previousPreview = _preview;
            var previousSceneBaseline = _sceneBaseline;
            var previousScenes = EditorSceneManager.GetSceneManagerSetup();
            bool sceneOpened = false, identityChanged = false, previousWorkResolved = false; int switchUndoGroup = -1;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先结束试玩，再切换编辑关卡。");
                var catalog = CharacterTestLevelAuthoringOperations.Catalog;
                var next = catalog.Load(levelId, CharacterTestLevelAuthoringOperations.ProjectRoot, false, out _);
                string path = catalog.Levels.Single(l => l.LevelId == levelId).Path;
                if (CurrentPath == path) return true;
                if (!previousWorkAlreadyResolved && window != null && !window.TryPrepareDocumentSwitch(decision, out message)) return false;
                previousWorkResolved = true;
                previousDraft = _draft; previousUndo = _undo;
                previousLoadedPath = _loadedPath; previousSourceHash = _sourceHash; previousSavedMetadata = _savedMetadata;
                previousDirty = _metadataDirty; previousPreview = _preview; previousSceneBaseline = _sceneBaseline;
                if (EditorSceneManager.GetActiveScene().path != next.ScenePath)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { message = "已取消打开目标 Scene。"; return false; }
                    sceneOpened = true; EditorSceneManager.OpenScene(next.ScenePath);
                }
                var manager = FindManager();
                if (manager == null || manager.WorldObjectSceneRoot == null) throw new InvalidOperationException("关联 Scene 需要唯一的 GameManager 和关卡根节点。");
                Undo.IncrementCurrentGroup(); switchUndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("切换当前编辑关卡");
                // Identity is committed only after unsaved work and the target Scene have been resolved.
                SessionState.SetString(Key + "id", levelId); SessionState.SetString(Key + "path", path); identityChanged = true;
                ClearReloadRecovery();
                _undo?.Retire(); _draft = null; _loadedPath = null; _metadataDirty = false; _preview = null; _sceneBaseline = null;
                EnsureLoaded(); window?.ReloadDocumentModules();
                ApplySelectedActorPoses();
                _sceneBaseline = CharacterTestLevelEditorOwnedSceneSnapshot.Create(MxLevelEditorModuleRegistry.Modules);
                Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(switchUndoGroup);
                _status = "当前关卡：" + levelId + "；所有模块已切换到同一文档。";
                return true;
            }
            catch (Exception error)
            {
                if (switchUndoGroup >= 0) Undo.RevertAllDownToGroup(switchUndoGroup);
                if (identityChanged || sceneOpened)
                {
                    _undo?.Retire();
                    SessionState.SetString(Key + "id", previousId); SessionState.SetString(Key + "path", previousPath);
                    _draft = previousDraft; _undo = previousUndo; _loadedPath = previousLoadedPath;
                    _sourceHash = previousSourceHash; _savedMetadata = previousSavedMetadata; _metadataDirty = previousDirty; _preview = previousPreview; _sceneBaseline = previousSceneBaseline;
                }
                try
                {
                    if (sceneOpened) EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                    if (identityChanged && previousWorkResolved)
                    {
                        if (_draft != null) CreateMetadataUndo();
                        window?.ReloadDocumentModules();
                        if (sceneOpened) _sceneBaseline = CharacterTestLevelEditorOwnedSceneSnapshot.Create(MxLevelEditorModuleRegistry.Modules);
                    }
                }
                catch (Exception restoreError)
                {
                    message = _status = "切换失败，原关卡身份已恢复；模块重载仍失败：" + restoreError.Message + "；原错误：" + error.Message;
                    return false;
                }
                message = _status = "切换失败，原关卡身份与草稿保留：" + error.Message; return false;
            }
        }

        private MxLevelAuthoringDocument CaptureDraft(IReadOnlyList<IMxLevelEditorModule> modules)
        {
            EnsureLoaded();
            if (_draft == null) throw new InvalidOperationException("请先选择或新建关卡。");
            var document = MxLevelAuthoringJson.LoadFromJson(MxLevelAuthoringJson.SaveToJson(_draft));
            foreach (var owner in modules)
            {
                if (owner.DocumentPath != CurrentPath || owner is IMxLevelEditorSceneScope scope && !scope.AppliesToActiveScene
                    || !(owner is IMxLevelEditorDraftProvider module)) continue;
                foreach (var draft in module.CaptureDraftModules())
                {
                    document.Modules.RemoveAll(m => m.ModuleId == draft.ModuleId);
                    document.Modules.Add(draft.Normalize());
                }
            }
            return document;
        }

        private void RequireSameSource(bool requireWritable = false)
        {
            EnsureLoaded();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("试玩期间编辑操作只读。");
            if (EditorSceneManager.GetActiveScene().path != _draft.ScenePath)
                throw new InvalidOperationException("请打开当前关卡关联的 Scene：" + _draft.ScenePath);
            string full = CharacterTestLevelCatalog.SafePath(CharacterTestLevelAuthoringOperations.ProjectRoot, CurrentPath);
            if (CharacterTestLevelCatalog.Hash(File.ReadAllText(full)) != _sourceHash)
                throw new InvalidOperationException("源文件已被其他入口修改。当前草稿保留；请先处理草稿，再重新选择或重载关卡。");
            if (requireWritable && (File.GetAttributes(full) & FileAttributes.ReadOnly) != 0) throw new IOException("关卡源文件为只读，草稿保留：" + CurrentPath);
        }

        public bool TrySave(IReadOnlyList<IMxLevelEditorModule> modules, out string message)
        {
            if (string.IsNullOrEmpty(CurrentPath)) return MxLevelEditorSaveCoordinator.TrySaveModules(modules, out message);
            try
            {
                RequireSameSource(requireWritable: true);
                var candidate = CaptureDraft(modules);
                if (MxLevelAuthoringValidator.Validate(candidate).HasErrors)
                    throw new InvalidOperationException("关卡文档结构校验失败。草稿保留。");
                // Freeze the complete Runtime-preflight candidate before any file or Scene cache write.
                var preflight = CharacterTestLevelAuthoringOperations.Preflight(Request(candidate));
                candidate = MxLevelAuthoringJson.LoadFromJson(preflight["document"].ToString());
                using var saveContext = new CharacterTestJoinedLevelSaveContext(CurrentPath, candidate);
                // Metadata is a participant in the same transaction, never a separate pre-write.
                var joined = modules.Concat(new IMxLevelEditorModule[] { this }).ToArray();
                bool saved = MxLevelEditorSaveCoordinator.TrySaveModules(joined, out var outcome, out message);
                _preview = null;
                _status = message;
                if (!saved) return false;
                ClearReloadRecovery();
                message = _status = "当前关卡已保存；试玩会检查并发布这份文档，不使用旧版本。";
                return true;
            }
            catch (Exception error)
            {
                _preview = null;
                message = _status = "保存失败，编辑内容保留：" + error.Message;
                return false;
            }
        }

        private JObject Request(MxLevelAuthoringDocument document)
            => new JObject { ["levelId"] = CurrentLevelId, ["expectedSourceHash"] = _sourceHash,
                ["document"] = JObject.Parse(MxLevelAuthoringJson.SaveToJson(document)), ["previewToken"] = _preview?["previewToken"] };

        public bool TryValidateDocument(out string message) => TryCheck(out message);

        internal bool TryCheck(out string message)
        {
            try
            {
                RequireSameSource();
                _lastFailure = null;
                _preview = CharacterTestLevelAuthoringOperations.Preflight(Request(CaptureDraft(MxLevelEditorModuleRegistry.Modules)));
                message = _status = "当前编辑内容检查通过；预览 hash " + ((string)_preview["previewToken"]).Substring(0, 12);
                return true;
            }
            catch (Exception error) { _lastFailure = error.Message; _preview = null; message = _status = "检查未通过，编辑内容保留：" + error.Message; return false; }
        }

        internal bool TryPublish(out string message)
        {
            if (!TrySave(MxLevelEditorModuleRegistry.Modules, out message) || !TryCheck(out message)) return false;
            try
            {
                var frozen = MxLevelAuthoringJson.LoadFromJson(_preview["document"].ToString());
                var result = CharacterTestLevelAuthoringOperations.Publish(Request(frozen));
                _sourceHash = (string)result["sourceHash"];
                _draft = MxLevelAuthoringJson.LoadFromJson(result["document"].ToString());
                _savedMetadata = MxLevelAuthoringJson.SaveToJson(_draft); _undo.ResetBaseline(); _metadataDirty = false; _preview = null;
                message = _status = "当前关卡已发布：" + _sourceHash.Substring(0, 12);
                return true;
            }
            catch (Exception error) { message = _status = "发布失败，未启动旧版本：" + error.Message; return false; }
        }

        internal bool TryPlay(MxLevelEditorWindow window, out string message)
        {
            if (!TryPublish(out message)) return false;
            window?.ReloadDocumentModules();
            var manager = FindManager();
            SessionState.SetString(Key + "playScene", manager.gameObject.scene.path);
            SessionState.SetString(Key + "oldInitial", manager.InitialLevelId);
            SessionState.SetString(Key + "playId", CurrentLevelId);
            SessionState.SetString(Key + "playHash", _sourceHash);
            manager.SetInitialLevelId(CurrentLevelId);
            _status = "试玩当前关卡 " + CurrentLevelId + " / " + _sourceHash.Substring(0, 12);
            EditorApplication.isPlaying = true;
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || string.IsNullOrEmpty(SessionState.GetString(Key + "playId", ""))) return;
            var manager = FindManager();
            if (manager != null && manager.gameObject.scene.path == SessionState.GetString(Key + "playScene", "")
                && manager.InitialLevelId == SessionState.GetString(Key + "playId", ""))
                manager.SetInitialLevelId(SessionState.GetString(Key + "oldInitial", ""));
            SessionState.EraseString(Key + "playId");
        }

        internal void ResetForTests()
        {
            ClearReloadRecovery();
            _undo?.Retire(); _sceneBaseline = null;
            SessionState.EraseString(Key + "id"); SessionState.EraseString(Key + "path");
            _draft = null; _loadedPath = null; _metadataDirty = false; _preview = null;
        }

        public void ReloadSavedDocument()
        {
            // Validate an explicit discard/reload destination before dropping recovery.
            if (!string.IsNullOrEmpty(CurrentPath))
            {
                string full = CharacterTestLevelCatalog.SafePath(CharacterTestLevelAuthoringOperations.ProjectRoot, CurrentPath);
                var source = MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(full));
                if (source.LevelId != CurrentLevelId || source.ScenePath != EditorSceneManager.GetActiveScene().path)
                    throw new InvalidOperationException("来源身份或 Scene 已更改；恢复草稿保留，未自动覆盖或打开其他 Scene。");
            }
            ClearReloadRecovery();
            _undo?.Retire(); _sceneBaseline = null;
            _draft = null; _loadedPath = null; _metadataDirty = false; _preview = null;
            EnsureLoaded();
        }
    }
}
