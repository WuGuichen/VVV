using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Editor.Levels
{
    /// <summary>
    /// Coordinates low-frequency Level Editor saves so validation and document writes complete
    /// before the managed Unity scene is persisted.
    /// </summary>
    public static class MxLevelEditorSaveCoordinator
    {
        private static int _saveDepth;

        /// <summary>
        /// Indicates that a coordinated Level Editor save is currently writing documents or the
        /// managed scene. Scene-change observers use this to ignore transient dirty notifications
        /// produced by the save itself.
        /// Allocation behavior: NoAlloc.
        /// </summary>
        public static bool IsSaveInProgress => _saveDepth > 0;

        /// <summary>
        /// Saves every dirty module as one editor transaction. Document files are restored when a
        /// participant fails before the final scene save.
        /// Allocation behavior: LowFreqAlloc; explicit editor save operations only.
        /// </summary>
        public static bool TrySaveDirtyModules(
            IReadOnlyList<IMxLevelEditorModule> modules,
            out string message)
        {
            return TrySaveModules(modules, dirtyOnly: true, out message);
        }

        /// <summary>
        /// Saves the supplied modules even when they currently match their saved baseline. The
        /// Level Editor's explicit Save command uses this path so Authoring and scene caches always
        /// finish on the same baseline, including recovery from a previously stale cache.
        /// Allocation behavior: LowFreqAlloc; explicit editor save operations only.
        /// </summary>
        public static bool TrySaveModules(
            IReadOnlyList<IMxLevelEditorModule> modules,
            out string message)
        {
            return TrySaveModules(modules, dirtyOnly: false, out message);
        }

        /// <summary>
        /// Reports the persistent commit boundary separately from completion success.
        /// A tail failure must never be followed by restoring pre-save document bytes.
        /// Allocation behavior: LowFreqAlloc; explicit editor saves only.
        /// </summary>
        public static bool TrySaveModules(
            IReadOnlyList<IMxLevelEditorModule> modules,
            out MxLevelEditorSaveOutcome outcome,
            out string message)
            => TrySaveModules(modules, false, out outcome, out message);

        private static bool TrySaveModules(
            IReadOnlyList<IMxLevelEditorModule> modules,
            bool dirtyOnly,
            out string message)
            => TrySaveModules(modules, dirtyOnly, out _, out message);

        private static bool TrySaveModules(
            IReadOnlyList<IMxLevelEditorModule> modules,
            bool dirtyOnly,
            out MxLevelEditorSaveOutcome outcome,
            out string message)
        {
            _saveDepth++;
            try
            {
                return TrySaveModulesCore(modules, dirtyOnly, out outcome, out message);
            }
            finally
            {
                _saveDepth--;
            }
        }

        private static bool TrySaveModulesCore(
            IReadOnlyList<IMxLevelEditorModule> modules,
            bool dirtyOnly,
            out MxLevelEditorSaveOutcome outcome,
            out string message)
        {
            outcome = MxLevelEditorSaveOutcome.NotCommitted;
            var participants = new List<IMxLevelEditorSaveParticipant>();
            Scene managedScene = default;
            bool hasManagedScene = false;

            if (modules != null)
            {
                for (int i = 0; i < modules.Count; i++)
                {
                    IMxLevelEditorModule module = modules[i];
                    if (module == null
                        || (module is IMxLevelEditorSceneScope scope && !scope.AppliesToActiveScene)
                        || (dirtyOnly && !module.IsDirty))
                        continue;

                    if (module is not IMxLevelEditorSaveParticipant participant)
                    {
                        message = "模块“" + module.DisplayName + "”尚未接入统一保存，未写入任何更改。";
                        return false;
                    }

                    if (module is not IMxLevelEditorSceneChangeAwareModule sceneAware
                        || sceneAware.ManagedSceneRoot == null)
                    {
                        message = "模块“" + module.DisplayName + "”没有可保存的场景根节点。";
                        participant.CompleteCoordinatedSave(false, message);
                        return false;
                    }

                    Scene currentScene = sceneAware.ManagedSceneRoot.gameObject.scene;
                    if (!currentScene.IsValid() || string.IsNullOrWhiteSpace(currentScene.path))
                    {
                        message = "模块“" + module.DisplayName + "”所在场景尚未保存到磁盘。";
                        participant.CompleteCoordinatedSave(false, message);
                        return false;
                    }

                    if (hasManagedScene && currentScene.handle != managedScene.handle)
                    {
                        message = "统一保存暂不支持同时写入多个 Unity 场景，未写入任何更改。";
                        participant.CompleteCoordinatedSave(false, message);
                        return false;
                    }

                    managedScene = currentScene;
                    hasManagedScene = true;
                    participants.Add(participant);
                }
            }

            if (participants.Count == 0)
            {
                outcome = MxLevelEditorSaveOutcome.Committed;
                message = "没有需要保存的更改。";
                return true;
            }

            List<FileSnapshot> snapshots;
            List<ObjectSnapshot> objectSnapshots;
            try
            {
                for (int i = 0; i < participants.Count; i++)
                {
                    if (participants[i].TryPrepareSave(out string failureMessage)) continue;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(failureMessage)
                        ? "保存前校验失败，未写入任何更改。" : failureMessage);
                }
                snapshots = CaptureDocumentSnapshots(modules, dirtyOnly);
                objectSnapshots = CaptureSceneObjectSnapshots(modules, dirtyOnly);
            }
            catch (Exception exception)
            {
                message = "保存前准备失败，未写入任何更改：" + exception.Message;
                NotifyCompleted(participants, false, message);
                return false;
            }

            Undo.IncrementCurrentGroup();
            int saveUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("保存所有关卡模块");
            bool committed = false;
            bool completionAttempted = false;
            try
            {
                for (int i = 0; i < participants.Count; i++)
                    participants[i].SavePreparedChanges();

                Undo.FlushUndoRecordObjects();
                if (managedScene.isDirty && !EditorSceneManager.SaveScene(managedScene))
                    throw new IOException("Unity 场景保存失败。");

                // The first successful scene save is the irreversible transaction boundary.
                // Observer and completion callbacks are tail work, even when they throw.
                committed = true;
                outcome = MxLevelEditorSaveOutcome.Committed;
                AcknowledgeManagedSceneSaved(modules, managedScene);
                message = "已保存所有关卡模块与 Unity 场景。";
                completionAttempted = true;
                string callbackFailure = NotifyCompleted(participants, true, message);
                if (!string.IsNullOrEmpty(callbackFailure)) throw new InvalidOperationException(callbackFailure);
                Undo.FlushUndoRecordObjects();
                if (managedScene.isDirty && !EditorSceneManager.SaveScene(managedScene))
                    throw new IOException("Unity 场景收尾保存失败。");
                AcknowledgeManagedSceneSaved(modules, managedScene);
                Undo.CollapseUndoOperations(saveUndoGroup);
                return true;
            }
            catch (Exception exception)
            {
                if (committed)
                {
                    outcome = MxLevelEditorSaveOutcome.CommittedWithTailFailure;
                    message = "配置与场景已经提交，收尾失败；保留已提交版本：" + exception.Message;
                    // Complete all baselines, including observers skipped by a throwing acknowledgement.
                    if (!completionAttempted) NotifyCompleted(participants, true, message);
                    NotifyCompleted(participants, false, message);
                    return false;
                }

                bool undoRestored = true;
                try { Undo.FlushUndoRecordObjects(); Undo.RevertAllDownToGroup(saveUndoGroup); }
                catch (Exception rollbackError) { undoRestored = false; Debug.LogError(rollbackError); }
                bool objectsRestored = RestoreSceneObjectSnapshots(objectSnapshots);
                bool filesRestored = RestoreDocumentSnapshots(snapshots);
                EditorSceneManager.MarkSceneDirty(managedScene);
                message = undoRestored && objectsRestored && filesRestored
                    ? "保存失败，配置与场景缓存已恢复，草稿保留：" + exception.Message
                    : "保存失败，至少一个快照未能恢复；草稿保留，请检查 Console：" + exception.Message;
                NotifyCompleted(participants, false, message);
                return false;
            }
        }

        private static void AcknowledgeManagedSceneSaved(
            IReadOnlyList<IMxLevelEditorModule> modules,
            Scene managedScene)
        {
            if (modules == null || !managedScene.IsValid())
                return;

            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is not IMxLevelEditorSceneChangeAwareModule sceneAware
                    || sceneAware.ManagedSceneRoot == null
                    || sceneAware.ManagedSceneRoot.gameObject.scene.handle != managedScene.handle)
                {
                    continue;
                }

                sceneAware.AcknowledgeManagedSceneSaved();
            }
        }

        private static List<FileSnapshot> CaptureDocumentSnapshots(
            IReadOnlyList<IMxLevelEditorModule> modules,
            bool dirtyOnly)
        {
            var snapshots = new List<FileSnapshot>();
            var capturedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (modules == null)
                return snapshots;

            for (int i = 0; i < modules.Count; i++)
            {
                IMxLevelEditorModule module = modules[i];
                if (module == null
                    || (module is IMxLevelEditorSceneScope scope && !scope.AppliesToActiveScene)
                    || (dirtyOnly && !module.IsDirty))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(module.DocumentPath))
                {
                    string fullPath = ToFullPath(module.DocumentPath);
                    if (capturedPaths.Add(fullPath))
                    {
                        bool existed = File.Exists(fullPath);
                        snapshots.Add(new FileSnapshot(
                            module.DocumentPath, fullPath, existed,
                            existed ? File.ReadAllBytes(fullPath) : null));
                    }
                }
                if (module is IMxLevelEditorAssetSavePaths assetOwner)
                {
                    var assetPaths = new List<string>();
                    assetOwner.CollectAssetSavePaths(assetPaths);
                    foreach (string assetPath in assetPaths)
                    {
                        if (string.IsNullOrWhiteSpace(assetPath) ||
                            !assetPath.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal))
                            throw new InvalidOperationException("Managed save asset must be under Assets/: " + assetPath);
                        string assetFullPath = ToFullPath(assetPath);
                        if (!capturedPaths.Add(assetFullPath)) continue;
                        bool assetExists = File.Exists(assetFullPath);
                        snapshots.Add(new FileSnapshot(assetPath, assetFullPath, assetExists,
                            assetExists ? File.ReadAllBytes(assetFullPath) : null));
                    }
                }
            }

            return snapshots;
        }

        private static bool RestoreDocumentSnapshots(List<FileSnapshot> snapshots)
        {
            if (snapshots == null)
                return true;

            // Save participants import their authoring assets immediately after writing them.
            // When a later participant or the scene save fails, Unity can still hold a mapped
            // read handle for that freshly imported file on Windows (ERROR_USER_MAPPED_FILE /
            // Win32 1224). Release those editor-owned handles before restoring the transaction
            // snapshot so a failed save cannot strand partially written authoring data.
            AssetDatabase.ReleaseCachedFileHandles();

            bool restored = true;
            for (int i = 0; i < snapshots.Count; i++)
            {
                FileSnapshot snapshot = snapshots[i];
                try
                {
                    if (snapshot.Existed)
                    {
                        string directory = Path.GetDirectoryName(snapshot.FullPath);
                        if (!string.IsNullOrWhiteSpace(directory))
                            Directory.CreateDirectory(directory);
                        File.WriteAllBytes(snapshot.FullPath, snapshot.Contents);
                    }
                    else if (snapshot.AssetPath.StartsWith(
                                 "Assets/",
                                 StringComparison.OrdinalIgnoreCase)
                        || snapshot.AssetPath.StartsWith(
                            "Assets\\",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (!AssetDatabase.DeleteAsset(snapshot.AssetPath.Replace('\\', '/'))
                            && File.Exists(snapshot.FullPath))
                        {
                            File.Delete(snapshot.FullPath);
                        }
                    }
                    else if (File.Exists(snapshot.FullPath))
                    {
                        File.Delete(snapshot.FullPath);
                    }

                    if (snapshot.AssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                        || snapshot.AssetPath.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
                    {
                        AssetDatabase.ImportAsset(snapshot.AssetPath.Replace('\\', '/'));
                    }
                }
                catch (Exception exception)
                {
                    restored = false;
                    Debug.LogError(
                        "Level Editor 无法恢复配置快照“"
                        + snapshot.AssetPath
                        + "”："
                        + exception.Message);
                }
            }

            return restored;
        }

        private static string NotifyCompleted(
            List<IMxLevelEditorSaveParticipant> participants,
            bool succeeded,
            string message)
        {
            var failures = new List<string>();
            for (int i = 0; i < participants.Count; i++)
            {
                try { participants[i].CompleteCoordinatedSave(succeeded, message); }
                catch (Exception error) { failures.Add(error.Message); }
            }
            return string.Join("; ", failures);
        }

        private static List<ObjectSnapshot> CaptureSceneObjectSnapshots(
            IReadOnlyList<IMxLevelEditorModule> modules, bool dirtyOnly)
        {
            var snapshots = new List<ObjectSnapshot>();
            var objects = new List<UnityEngine.Object>();
            var captured = new HashSet<UnityEngine.Object>();
            if (modules == null) return snapshots;
            foreach (var module in modules)
            {
                if (module == null || (dirtyOnly && !module.IsDirty)
                    || (module is IMxLevelEditorSceneScope scope && !scope.AppliesToActiveScene)) continue;
                objects.Clear();
                if (module is IMxLevelEditorSceneSaveSnapshotProvider provider)
                    provider.CollectSceneSaveObjects(objects);
                foreach (var value in objects)
                    if (value != null && captured.Add(value))
                        snapshots.Add(new ObjectSnapshot(value, EditorJsonUtility.ToJson(value)));
            }
            return snapshots;
        }

        private static bool RestoreSceneObjectSnapshots(List<ObjectSnapshot> snapshots)
        {
            bool restored = true;
            foreach (var snapshot in snapshots)
            {
                try
                {
                    if (snapshot.Value == null) throw new InvalidOperationException("保存对象已经销毁。");
                    EditorJsonUtility.FromJsonOverwrite(snapshot.Json, snapshot.Value);
                    EditorUtility.SetDirty(snapshot.Value);
                }
                catch (Exception error) { restored = false; Debug.LogError(error); }
            }
            return restored;
        }

        private sealed class ObjectSnapshot
        {
            internal readonly UnityEngine.Object Value;
            internal readonly string Json;
            internal ObjectSnapshot(UnityEngine.Object value, string json) { Value = value; Json = json; }
        }

        private static string ToFullPath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, path));
        }

        private sealed class FileSnapshot
        {
            public FileSnapshot(
                string assetPath,
                string fullPath,
                bool existed,
                byte[] contents)
            {
                AssetPath = assetPath;
                FullPath = fullPath;
                Existed = existed;
                Contents = contents;
            }

            public string AssetPath { get; }

            public string FullPath { get; }

            public bool Existed { get; }

            public byte[] Contents { get; }
        }
    }
}
