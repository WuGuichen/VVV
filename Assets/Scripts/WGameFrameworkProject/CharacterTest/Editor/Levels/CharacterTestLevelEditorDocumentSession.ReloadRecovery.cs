using System;
using System.IO;
using System.Linq;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestLevelEditorDocumentSession
    {
        // SessionState is owned by the native Editor and survives assembly reload.
        // This is a same-Editor-session recovery record, not a Scene auto-save.
        private bool _restoringReloadRecovery;
        private const int ReloadRecoveryVersion = 1;

        private void CaptureReloadRecovery()
        {
            if (string.IsNullOrEmpty(CurrentPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                EnsureLoaded();
                if (_draft == null || _draft.ScenePath != EditorSceneManager.GetActiveScene().path) return;
                var modules = MxLevelEditorModuleRegistry.Modules;
                _sceneBaseline ??= CharacterTestLevelEditorOwnedSceneSnapshot.Create(modules);
                _sceneBaseline.TrackAddedObjects(modules);
                var complete = CaptureDraft(modules);
                var states = new JObject();
                foreach (var module in modules.Where(m => m.DocumentPath == CurrentPath))
                {
                    if (module is CharacterTestWorldObjectLevelEditorModule world) states[module.ModuleId] = world.CaptureReloadState();
                    if (module is CharacterTestSceneCharacterLevelEditorModule characters) states[module.ModuleId] = characters.CaptureReloadState();
                    if (module is CharacterTestGuidanceLevelEditorModule guidance) states[module.ModuleId] = guidance.CaptureReloadState();
                    if (module is CharacterTestGenesisValidationLevelEditorModule genesis) states[module.ModuleId] = genesis.CaptureReloadState();
                }
                var record = new JObject {
                    ["version"] = ReloadRecoveryVersion, ["path"] = CurrentPath, ["levelId"] = CurrentLevelId,
                    ["scenePath"] = _draft.ScenePath, ["sourceHash"] = _sourceHash,
                    ["savedMetadata"] = _savedMetadata, ["metadataDirty"] = _metadataDirty,
                    ["draft"] = MxLevelAuthoringJson.SaveToJson(complete), ["moduleStates"] = states,
                    ["sceneBaseline"] = _sceneBaseline.CaptureReloadState()
                };
                // Replace only after the whole payload was captured successfully.
                SessionState.SetString(Key + "reloadRecovery", record.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception error)
            {
                _status = "域重载恢复记录未能更新；先前恢复记录保留：" + error.Message;
                UnityEngine.Debug.LogWarning(_status);
            }
        }

        private bool TryRestoreReloadRecovery()
        {
            if (_restoringReloadRecovery) return false;
            string json = SessionState.GetString(Key + "reloadRecovery", "");
            if (string.IsNullOrEmpty(json)) return false;
            var record = JObject.Parse(json);
            if ((int?)record["version"] != ReloadRecoveryVersion || (string)record["path"] != CurrentPath
                || (string)record["levelId"] != CurrentLevelId) return false;
            if ((string)record["scenePath"] != EditorSceneManager.GetActiveScene().path)
                throw new InvalidOperationException("恢复草稿保留在当前 Editor 会话；请回到关联 Scene，未自动打开或保存其他 Scene。");
            CharacterTestLevelCatalog.SafePath(CharacterTestLevelAuthoringOperations.ProjectRoot, CurrentPath);
            var draft = MxLevelAuthoringJson.LoadFromJson((string)record["draft"]);
            if (draft.LevelId != CurrentLevelId || draft.ScenePath != (string)record["scenePath"])
                throw new InvalidOperationException("恢复载体身份不一致，恢复记录保留，未覆盖源文件。");
            var baseline = CharacterTestLevelEditorOwnedSceneSnapshot.RestoreReloadState((string)record["sceneBaseline"]);
            _restoringReloadRecovery = true;
            try
            {
                _draft = draft; _loadedPath = CurrentPath; _sourceHash = (string)record["sourceHash"];
                _savedMetadata = (string)record["savedMetadata"]; _metadataDirty = (bool)record["metadataDirty"];
                _preview = null; _sceneBaseline = baseline;
                CreateMetadataUndo();
                var states = (JObject)record["moduleStates"];
                var modules = MxLevelEditorModuleRegistry.Modules;
                // Restore World before Genesis composition reads its live draft.
                foreach (var module in modules.Where(m => m.DocumentPath == CurrentPath))
                    if (module is CharacterTestWorldObjectLevelEditorModule world && states[module.ModuleId] is JObject state)
                        world.RestoreReloadState(draft, state);
                foreach (var module in modules.Where(m => m.DocumentPath == CurrentPath))
                {
                    if (states[module.ModuleId] is not JObject state) continue;
                    if (module is CharacterTestSceneCharacterLevelEditorModule characters) characters.RestoreReloadState(draft, state);
                    if (module is CharacterTestGuidanceLevelEditorModule guidance) guidance.RestoreReloadState(draft, state);
                    if (module is CharacterTestGenesisValidationLevelEditorModule genesis) genesis.RestoreReloadState(draft, state);
                }
                string full = CharacterTestLevelCatalog.SafePath(CharacterTestLevelAuthoringOperations.ProjectRoot, CurrentPath);
                bool unchanged = File.Exists(full) && CharacterTestLevelCatalog.Hash(File.ReadAllText(full)) == _sourceHash;
                _lastFailure = unchanged ? null : "恢复草稿的来源已更改或缺失；恢复稿保留，保存/预检不会覆盖新来源。";
                _status = unchanged ? "已恢复域重载前完整关卡草稿；未保存更改与范围基线保留。" : _lastFailure;
                return true;
            }
            catch
            {
                // Fail closed; never silently read disk and mark the recovery draft clean.
                _draft = null; _loadedPath = null;
                throw;
            }
            finally { _restoringReloadRecovery = false; }
        }

        private void ClearReloadRecovery() => SessionState.EraseString(Key + "reloadRecovery");
    }
}
