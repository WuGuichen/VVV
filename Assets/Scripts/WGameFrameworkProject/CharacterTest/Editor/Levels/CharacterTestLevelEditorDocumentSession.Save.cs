using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestLevelEditorDocumentSession :
        IMxLevelEditorModule, IMxLevelEditorSaveParticipant,
        IMxLevelEditorSceneChangeAwareModule, IMxLevelEditorSceneScope,
        IMxLevelEditorSceneSaveSnapshotProvider
    {
        private MxLevelAuthoringDocument _preparedMetadata;
        private bool _metadataCommitted;
        string IMxLevelEditorModule.ModuleId => "characterTest.tacticalStage";
        string IMxLevelEditorModule.DisplayName => "关卡规则";
        string IMxLevelEditorModule.DocumentPath => CurrentPath;
        string IMxLevelEditorModule.Status => _status;
        bool IMxLevelEditorSceneScope.AppliesToActiveScene => !string.IsNullOrEmpty(CurrentPath) && FindManager() != null;
        public Transform ManagedSceneRoot => FindManager()?.WorldObjectSceneRoot;
        void IMxLevelEditorModule.OnEnable() => EnsureLoaded();
        void IMxLevelEditorModule.BuildModuleToolbar(MxLevelEditorWindow window, VisualElement toolbar) { }
        VisualElement IMxLevelEditorModule.CreateContent(MxLevelEditorWindow window)
        {
            var host = new VisualElement(); EnsureLoaded(); BuildRules(window, host); return host;
        }
        void IMxLevelEditorModule.OpenJson() => AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CurrentPath));
        void IMxLevelEditorModule.Reload() => ReloadSavedDocument();
        void IMxLevelEditorModule.Save() => TrySave(MxLevelEditorModuleRegistry.Modules, out _status);
        void IMxLevelEditorModule.Validate() => TryCheck(out _status);
        void IMxLevelEditorModule.ApplyToScene() => TrySave(MxLevelEditorModuleRegistry.Modules, out _status);
        void IMxLevelEditorModule.SelectRootSource() { if (FindManager() != null) Selection.activeObject = FindManager(); }
        public void CollectManagedSceneTransforms(List<Transform> output) { }
        public bool NotifySceneTransformChanged(Transform transform) => false;
        public void RefreshUnregisteredSceneObjects(IReadOnlyList<Transform> transforms) { }
        public void AcknowledgeManagedSceneSaved() { }
        public void CollectSceneSaveObjects(List<UnityEngine.Object> output)
        {
            var manager = FindManager(); if (manager != null) output.Add(manager);
        }

        bool IMxLevelEditorSaveParticipant.TryPrepareSave(out string failureMessage)
        {
            _preparedMetadata = null; _metadataCommitted = false;
            try
            {
                RequireSameSource(requireWritable: true);
                _preparedMetadata = CharacterTestJoinedLevelSaveContext.TryGet(CurrentPath, out var joined)
                    ? joined : MxLevelAuthoringJson.LoadFromJson(MxLevelAuthoringJson.SaveToJson(_draft));
                failureMessage = string.Empty; return true;
            }
            catch (Exception error) { failureMessage = error.Message; return false; }
        }

        void IMxLevelEditorSaveParticipant.SavePreparedChanges()
        {
            if (_preparedMetadata == null) throw new InvalidOperationException("关卡规则尚未完成保存准备。");
            string full = CharacterTestLevelCatalog.SafePath(CharacterTestLevelAuthoringOperations.ProjectRoot, CurrentPath);
            var latest = MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(full));
            if (latest.LevelId != _preparedMetadata.LevelId || latest.ScenePath != _preparedMetadata.ScenePath)
                throw new InvalidOperationException("保存过程中关卡身份发生变化，未写入规则。");
            // Commit one complete candidate, then rebuild caches from the exact persisted bytes.
            // This finalizer runs inside the coordinator's file/object rollback boundary.
            CharacterTestLevelCatalog.AtomicWrite(full, MxLevelAuthoringJson.SaveToJson(_preparedMetadata));
            AssetDatabase.ImportAsset(CurrentPath);
            var final = MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(full));
            foreach (var module in MxLevelEditorModuleRegistry.Modules)
            {
                if (module.DocumentPath != CurrentPath || module is IMxLevelEditorSceneScope scope && !scope.AppliesToActiveScene) continue;
                if (module is CharacterTestWorldObjectLevelEditorModule world) world.ApplyFinalJoinedCache(final);
                if (module is CharacterTestSceneCharacterLevelEditorModule characters) characters.ApplyFinalJoinedCache(final);
            }
        }

        void IMxLevelEditorSaveParticipant.CompleteCoordinatedSave(bool succeeded, string message)
        {
            _status = message; _preview = null;
            if (succeeded && !_metadataCommitted)
            {
                string full = CharacterTestLevelCatalog.SafePath(CharacterTestLevelAuthoringOperations.ProjectRoot, CurrentPath);
                string source = File.ReadAllText(full);
                _draft = MxLevelAuthoringJson.LoadFromJson(source);
                _sourceHash = CharacterTestLevelCatalog.Hash(source);
                _savedMetadata = MxLevelAuthoringJson.SaveToJson(_draft); _metadataDirty = false; _undo.ResetBaseline(); _metadataCommitted = true;
                _sceneBaseline = CharacterTestLevelEditorOwnedSceneSnapshot.Create(MxLevelEditorModuleRegistry.Modules);
            }
            _preparedMetadata = null;
        }
    }
}
