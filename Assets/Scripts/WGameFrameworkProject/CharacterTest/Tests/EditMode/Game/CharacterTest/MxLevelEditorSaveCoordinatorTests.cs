using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Editor.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class MxLevelEditorSaveCoordinatorTests
    {
        private const string TempRootPath = "Assets/Temp";
        private string _folderPath;
        private string _documentPath;
        private Scene _scene;
        private Transform _root;

        [SetUp]
        public void SetUp()
        {
            _folderPath = TempRootPath + "/MxLevelEditorSaveCoordinatorTests_"
                + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(ToFullPath(_folderPath));
            _documentPath = _folderPath + "/level.json";
            File.WriteAllText(ToFullPath(_documentPath), "original");
            AssetDatabase.Refresh();

            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(UnityEngine.Camera));
            var light = new GameObject("Directional Light", typeof(UnityEngine.Light));
            var root = new GameObject("Managed Level Root");
            _root = root.transform;

            string scenePath = _folderPath + "/SaveCoordinatorTest.unity";
            Assert.IsTrue(EditorSceneManager.SaveScene(_scene, scenePath));
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            if (!string.IsNullOrWhiteSpace(_folderPath))
                AssetDatabase.DeleteAsset(_folderPath);

            string tempRootFullPath = ToFullPath(TempRootPath);
            if (Directory.Exists(tempRootFullPath)
                && Directory.GetFileSystemEntries(tempRootFullPath).Length == 0)
            {
                AssetDatabase.DeleteAsset(TempRootPath);
            }
        }

        [Test]
        public void PreflightFailureDoesNotWriteDocumentOrScene()
        {
            MarkTempSceneDirty();
            var writer = new FakeSaveModule(
                "writer",
                _documentPath,
                _root,
                () => File.WriteAllText(ToFullPath(_documentPath), "updated"));
            var blocker = new FakeSaveModule(
                "blocker",
                _documentPath,
                _root,
                saveAction: null,
                prepareResult: false);

            bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { writer, blocker },
                out string message);

            Assert.IsFalse(saved);
            Assert.That(message, Does.Contain("blocker"));
            Assert.AreEqual("original", File.ReadAllText(ToFullPath(_documentPath)));
            Assert.IsTrue(_scene.isDirty);
            Assert.IsFalse(writer.SaveCalled);
            Assert.IsFalse(writer.CompletedSuccessfully);
        }

        [Test]
        public void DirtySaveExcludesCleanInvalidSiblingAndPersistsDirtyParticipant()
        {
            MarkTempSceneDirty();
            var writer = new FakeSaveModule(
                "writer",
                _documentPath,
                _root,
                () => File.WriteAllText(ToFullPath(_documentPath), "updated"));
            var cleanBlocker = new FakeSaveModule(
                "clean-blocker",
                _documentPath,
                _root,
                saveAction: null,
                prepareResult: false,
                isDirty: false);

            bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { writer, cleanBlocker },
                out string message);

            Assert.IsTrue(saved, message);
            Assert.AreEqual("updated", File.ReadAllText(ToFullPath(_documentPath)));
            Assert.IsTrue(writer.SaveCalled);
            Assert.IsTrue(writer.CompletedSuccessfully);
            Assert.IsFalse(cleanBlocker.SaveCalled);
            Assert.IsFalse(cleanBlocker.CompletedSuccessfully);
            Assert.IsFalse(_scene.isDirty);
        }

        [Test]
        public void ExplicitSaveSkipsModuleOwnedByAnotherScene()
        {
            var current = new FakeSaveModule(
                "current", _documentPath, _root,
                () => File.WriteAllText(ToFullPath(_documentPath), "current"));
            var foreign = new FakeSaveModule(
                "foreign", _documentPath, _root,
                saveAction: null, prepareResult: false,
                appliesToActiveScene: false);

            bool saved = MxLevelEditorSaveCoordinator.TrySaveModules(
                new IMxLevelEditorModule[] { foreign, current }, out string message);

            Assert.IsTrue(saved, message);
            Assert.AreEqual("current", File.ReadAllText(ToFullPath(_documentPath)));
            Assert.IsFalse(foreign.SaveCalled);
            Assert.IsFalse(foreign.CompletedSuccessfully);
        }

        [Test]
        public void WriteFailureRestoresDocumentAndLeavesSceneDirty()
        {
            MarkTempSceneDirty();
            var writer = new FakeSaveModule(
                "writer",
                _documentPath,
                _root,
                () => File.WriteAllText(ToFullPath(_documentPath), "updated"));
            var failing = new FakeSaveModule(
                "failing",
                _documentPath,
                _root,
                () => throw new IOException("simulated write failure"));

            bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { writer, failing },
                out string message);

            Assert.IsFalse(saved);
            Assert.That(message, Does.Contain("simulated write failure"));
            Assert.AreEqual("original", File.ReadAllText(ToFullPath(_documentPath)));
            Assert.IsTrue(_scene.isDirty);
            Assert.IsFalse(writer.CompletedSuccessfully);
            Assert.IsFalse(failing.CompletedSuccessfully);
        }

        [Test]
        public void WriteFailureAfterAssetImportRestoresDocumentSnapshot()
        {
            MarkTempSceneDirty();
            var writer = new FakeSaveModule(
                "writer",
                _documentPath,
                _root,
                () =>
                {
                    File.WriteAllText(
                        ToFullPath(_documentPath),
                        "updated");
                    AssetDatabase.ImportAsset(
                        _documentPath,
                        ImportAssetOptions.ForceUpdate);
                });
            var failing = new FakeSaveModule(
                "failing",
                _documentPath,
                _root,
                () => throw new IOException(
                    "simulated post-import failure"));

            bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { writer, failing },
                out string message);

            Assert.IsFalse(saved);
            Assert.That(
                message,
                Does.Contain("simulated post-import failure"));
            Assert.AreEqual(
                "original",
                File.ReadAllText(ToFullPath(_documentPath)));
            Assert.IsTrue(_scene.isDirty);
        }

        [Test]
        public void WriteFailureRestoresManagedAssetWhenModulesShareDocumentPath()
        {
            MarkTempSceneDirty();
            string assetPath = _folderPath + "/shared-look.txt";
            File.WriteAllText(ToFullPath(assetPath), "saved-look");
            AssetDatabase.ImportAsset(assetPath);
            var first = new FakeSaveModule("first", _documentPath, _root, saveAction: null);
            var lookWriter = new FakeSaveModule("look", _documentPath, _root,
                () => { File.WriteAllText(ToFullPath(assetPath), "edited-look"); AssetDatabase.ImportAsset(assetPath); },
                assetPaths: new[] { assetPath });
            var failing = new FakeSaveModule("failing", _documentPath, _root,
                () => throw new IOException("simulated failure after Look write"));

            bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { first, lookWriter, failing }, out string message);

            Assert.IsFalse(saved);
            Assert.That(message, Does.Contain("simulated failure after Look write"));
            Assert.AreEqual("saved-look", File.ReadAllText(ToFullPath(assetPath)));
            Assert.IsTrue(_scene.isDirty);
        }

        [Test]
        public void WriteFailureRemovesDocumentCreatedDuringTransaction()
        {
            MarkTempSceneDirty();
            string createdDocumentPath = _folderPath + "/created.json";
            var creator = new FakeSaveModule(
                "creator",
                createdDocumentPath,
                _root,
                () => File.WriteAllText(ToFullPath(createdDocumentPath), "created"));
            var failing = new FakeSaveModule(
                "failing",
                _documentPath,
                _root,
                () => throw new IOException("simulated write failure"));

            bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { creator, failing },
                out _);

            Assert.IsFalse(saved);
            Assert.IsFalse(File.Exists(ToFullPath(createdDocumentPath)));
            Assert.IsTrue(_scene.isDirty);
        }

        [Test]
        public void SuccessfulSavePersistsDocumentAndSceneTogether()
        {
            MarkTempSceneDirty();
            var writer = new FakeSaveModule(
                "writer",
                _documentPath,
                _root,
                () => File.WriteAllText(ToFullPath(_documentPath), "updated"));

            bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { writer },
                out string message);

            Assert.IsTrue(saved, message);
            Assert.AreEqual("updated", File.ReadAllText(ToFullPath(_documentPath)));
            Assert.IsFalse(_scene.isDirty);
            Assert.IsTrue(writer.CompletedSuccessfully);
            Assert.IsFalse(writer.IsDirty);
            Assert.IsFalse(MxLevelEditorSaveCoordinator.IsSaveInProgress);
        }

        [Test]
        public void SaveGeneratedSceneDirtyEventRemainsInsideTransaction()
        {
            bool observedDirtyOutsideSave = false;
            bool observedDirtyDuringSave = false;
            void OnSceneDirtied(Scene scene)
            {
                if (scene.handle != _scene.handle)
                    return;

                if (MxLevelEditorSaveCoordinator.IsSaveInProgress)
                    observedDirtyDuringSave = true;
                else
                    observedDirtyOutsideSave = true;
            }

            EditorSceneManager.sceneDirtied += OnSceneDirtied;
            try
            {
                var writer = new FakeSaveModule(
                    "writer",
                    _documentPath,
                    _root,
                    () =>
                    {
                        Assert.IsTrue(MxLevelEditorSaveCoordinator.IsSaveInProgress);
                        File.WriteAllText(ToFullPath(_documentPath), "updated");
                        EditorSceneManager.MarkSceneDirty(_scene);
                    });

                bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                    new IMxLevelEditorModule[] { writer },
                    out string message);

                Assert.IsTrue(saved, message);
                Assert.IsTrue(observedDirtyDuringSave);
                Assert.IsFalse(observedDirtyOutsideSave);
                Assert.IsFalse(MxLevelEditorSaveCoordinator.IsSaveInProgress);
                Assert.IsFalse(_scene.isDirty);
            }
            finally
            {
                EditorSceneManager.sceneDirtied -= OnSceneDirtied;
            }
        }

        [Test]
        public void SuccessfulSaveAcknowledgesCleanModulesSharingManagedScene()
        {
            var writer = new FakeSaveModule(
                "writer",
                _documentPath,
                _root,
                () => EditorSceneManager.MarkSceneDirty(_scene));
            var cleanObserver = new FakeSaveModule(
                "clean-observer",
                _documentPath,
                _root,
                saveAction: null,
                isDirty: false);

            bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { writer, cleanObserver },
                out string message);

            Assert.IsTrue(saved, message);
            Assert.IsTrue(writer.SceneSaveAcknowledged);
            Assert.IsTrue(cleanObserver.SceneSaveAcknowledged);
            Assert.IsFalse(cleanObserver.SaveCalled);
            Assert.IsFalse(cleanObserver.IsDirty);
        }

        [Test]
        public void SceneDirtiedDuringCompletionReceivesFinalSave()
        {
            var writer = new FakeSaveModule(
                "writer",
                _documentPath,
                _root,
                saveAction: null,
                completeAction: () => EditorSceneManager.MarkSceneDirty(_scene));

            bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { writer },
                out string message);

            Assert.IsTrue(saved, message);
            Assert.IsFalse(_scene.isDirty);
            Assert.IsTrue(writer.SceneSaveAcknowledged);
        }

        [Test]
        public void PendingUndoRecordIsFlushedBeforeSceneSave()
        {
            Vector3 savedPosition = new Vector3(2f, 3f, 4f);
            var writer = new FakeSaveModule(
                "writer",
                _documentPath,
                _root,
                () =>
                {
                    Undo.RecordObject(_root, "Move managed scene object while saving");
                    _root.localPosition = savedPosition;
                });

            bool saved = MxLevelEditorSaveCoordinator.TrySaveDirtyModules(
                new IMxLevelEditorModule[] { writer },
                out string message);

            // Simulate Unity's end-of-event Undo processing. It must not make the scene dirty
            // again after the coordinator has returned.
            Undo.FlushUndoRecordObjects();

            Assert.IsTrue(saved, message);
            Assert.AreEqual(savedPosition, _root.localPosition);
            Assert.IsFalse(_scene.isDirty);
        }

        [Test]
        public void ExplicitApplyIncludesCleanParticipantAndPersistsSceneCacheChange()
        {
            var cleanParticipant = new FakeSaveModule(
                "clean",
                _documentPath,
                _root,
                () =>
                {
                    File.WriteAllText(ToFullPath(_documentPath), "applied");
                    EditorSceneManager.MarkSceneDirty(_scene);
                },
                isDirty: false);

            bool saved = MxLevelEditorSaveCoordinator.TrySaveModules(
                new IMxLevelEditorModule[] { cleanParticipant },
                out string message);

            Assert.IsTrue(saved, message);
            Assert.IsTrue(cleanParticipant.SaveCalled);
            Assert.AreEqual("applied", File.ReadAllText(ToFullPath(_documentPath)));
            Assert.IsFalse(_scene.isDirty);
        }

        [Test]
        public void WriteFailureRestoresSerializedCacheAndPreservesUnrelatedSceneDraft()
        {
            MarkTempSceneDirty();
            var light = UnityEngine.Object.FindFirstObjectByType<Light>();
            light.intensity = 7f;
            Vector3 before = _root.localPosition;
            var writer = new FakeSaveModule("writer", _documentPath, _root, () => {
                File.WriteAllText(ToFullPath(_documentPath), "updated");
                _root.localPosition = new Vector3(9f, 8f, 7f);
            });
            var failure = new FakeSaveModule("failure", _documentPath, _root,
                () => throw new IOException("cache write failed"));
            bool saved = MxLevelEditorSaveCoordinator.TrySaveModules(
                new IMxLevelEditorModule[] { writer, failure }, out var outcome, out var message);
            Assert.IsFalse(saved, message);
            Assert.AreEqual(MxLevelEditorSaveOutcome.NotCommitted, outcome);
            Assert.AreEqual("original", File.ReadAllText(ToFullPath(_documentPath)));
            Assert.AreEqual(before, _root.localPosition);
            Assert.AreEqual(7f, light.intensity);
            Assert.IsTrue(_scene.isDirty);
        }

        [Test]
        public void AcknowledgementFailureKeepsCommittedDocumentAndBaseline()
        {
            MarkTempSceneDirty();
            var writer = new FakeSaveModule("writer", _documentPath, _root,
                () => File.WriteAllText(ToFullPath(_documentPath), "committed"),
                acknowledgeAction: () => throw new IOException("acknowledgement failed"));
            bool saved = MxLevelEditorSaveCoordinator.TrySaveModules(
                new IMxLevelEditorModule[] { writer }, out var outcome, out var message);
            Assert.IsFalse(saved);
            Assert.AreEqual(MxLevelEditorSaveOutcome.CommittedWithTailFailure, outcome);
            Assert.That(message, Does.Contain("acknowledgement failed"));
            Assert.AreEqual("committed", File.ReadAllText(ToFullPath(_documentPath)));
            Assert.IsFalse(_scene.isDirty);
            Assert.IsFalse(writer.IsDirty);
            Assert.IsTrue(writer.CompletedSuccessfully);
        }

        [Test]
        public void CompletionFailureKeepsCommittedDataAndCompletesOtherParticipants()
        {
            int completionCount = 0;
            var writer = new FakeSaveModule("writer", _documentPath, _root,
                () => { File.WriteAllText(ToFullPath(_documentPath), "committed"); EditorSceneManager.MarkSceneDirty(_scene); },
                completeAction: () => { completionCount++; throw new IOException("completion failed"); });
            var observer = new FakeSaveModule("observer", _documentPath, _root, null);
            bool saved = MxLevelEditorSaveCoordinator.TrySaveModules(
                new IMxLevelEditorModule[] { writer, observer }, out var outcome, out var message);
            Assert.IsFalse(saved);
            Assert.AreEqual(MxLevelEditorSaveOutcome.CommittedWithTailFailure, outcome);
            Assert.That(message, Does.Contain("completion failed"));
            Assert.AreEqual("committed", File.ReadAllText(ToFullPath(_documentPath)));
            Assert.AreEqual(1, completionCount);
            Assert.IsTrue(observer.CompletedSuccessfully);
            Assert.IsFalse(observer.IsDirty);
            Assert.IsFalse(MxLevelEditorSaveCoordinator.IsSaveInProgress);
        }

        private void MarkTempSceneDirty()
        {
            var child = new GameObject("Unsaved Scene Change");
            SceneManager.MoveGameObjectToScene(child, _scene);
            child.transform.SetParent(_root, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(_scene);
            Assert.IsTrue(_scene.isDirty);
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return Path.GetFullPath(Path.Combine(projectRoot ?? string.Empty, assetPath));
        }

        private sealed class FakeSaveModule :
            IMxLevelEditorModule,
            IMxLevelEditorSceneChangeAwareModule,
            IMxLevelEditorSceneScope,
            IMxLevelEditorSaveParticipant,
            IMxLevelEditorAssetSavePaths, IMxLevelEditorSceneSaveSnapshotProvider
        {
            private readonly Action _saveAction;
            private readonly Action _completeAction;
            private readonly Action _acknowledgeAction;
            private readonly bool _prepareResult;
            private readonly IReadOnlyList<string> _assetPaths;

            public FakeSaveModule(
                string moduleId,
                string documentPath,
                Transform root,
                Action saveAction,
                bool prepareResult = true,
                bool isDirty = true,
                Action completeAction = null,
                IReadOnlyList<string> assetPaths = null,
                bool appliesToActiveScene = true,
                Action acknowledgeAction = null)
            {
                ModuleId = moduleId;
                DisplayName = moduleId;
                DocumentPath = documentPath;
                ManagedSceneRoot = root;
                _saveAction = saveAction;
                _completeAction = completeAction;
                _acknowledgeAction = acknowledgeAction;
                _prepareResult = prepareResult;
                _assetPaths = assetPaths;
                IsDirty = isDirty;
                AppliesToActiveScene = appliesToActiveScene;
            }

            public string ModuleId { get; }

            public string DisplayName { get; }

            public string DocumentPath { get; }

            public bool IsDirty { get; private set; }

            public bool AppliesToActiveScene { get; }

            public string Status { get; private set; } = string.Empty;

            public Transform ManagedSceneRoot { get; }

            public bool SaveCalled { get; private set; }

            public bool CompletedSuccessfully { get; private set; }

            public bool SceneSaveAcknowledged { get; private set; }

            public void CollectSceneSaveObjects(List<UnityEngine.Object> output)
            {
                if (ManagedSceneRoot != null) output.Add(ManagedSceneRoot);
            }

            public void CollectAssetSavePaths(List<string> paths)
            {
                if (_assetPaths == null) return;
                foreach (string path in _assetPaths) paths.Add(path);
            }

            public void OnEnable()
            {
            }

            public void BuildModuleToolbar(MxLevelEditorWindow window, VisualElement toolbar)
            {
            }

            public VisualElement CreateContent(MxLevelEditorWindow window)
            {
                return new VisualElement();
            }

            public void OpenJson()
            {
            }

            public void Reload()
            {
            }

            public void Save()
            {
            }

            public void Validate()
            {
            }

            public void ApplyToScene()
            {
            }

            public void SelectRootSource()
            {
            }

            public bool TryPrepareSave(out string failureMessage)
            {
                failureMessage = _prepareResult
                    ? string.Empty
                    : "blocker rejected the save";
                return _prepareResult;
            }

            public void SavePreparedChanges()
            {
                SaveCalled = true;
                _saveAction?.Invoke();
            }

            public void CompleteCoordinatedSave(bool succeeded, string message)
            {
                if (succeeded) CompletedSuccessfully = true;
                IsDirty = !CompletedSuccessfully;
                Status = message;
                if (succeeded)
                    _completeAction?.Invoke();
            }

            public void CollectManagedSceneTransforms(List<Transform> output)
            {
                if (ManagedSceneRoot != null)
                    output?.Add(ManagedSceneRoot);
            }

            public bool NotifySceneTransformChanged(Transform transform)
            {
                return false;
            }

            public void RefreshUnregisteredSceneObjects(
                IReadOnlyList<Transform> allManagedTransforms)
            {
            }

            public void AcknowledgeManagedSceneSaved()
            {
                SceneSaveAcknowledged = true;
                _acknowledgeAction?.Invoke();
            }
        }
    }
}
