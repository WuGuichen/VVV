using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestWorldObjectCopyNativeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<CharacterTestWorldObjectLevelEditorModule> _modules =
            new List<CharacterTestWorldObjectLevelEditorModule>();
        private Scene _previousActiveScene;
        private Scene _scene;
        private string _folder;
        private string _scenePath;
        private string _documentPath;
        private Transform _root;
        private GameObject _source;
        private GameObject _peer;
        private Object _previousSelection;
        private int _undoGroup = -1;
        private Scene _runnerScene;
        private string _runnerSceneFolder;
        private bool _runnerSceneHadDefaultObjects;

        [OneTimeSetUp]
        public void SaveUntitledRunnerSceneForAdditiveFixtures()
        {
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsTrue(string.IsNullOrEmpty(CharacterTestLevelEditorDocumentSession.CurrentPath),
                "Run in an isolated Editor with no selected level document.");
            Scene runnerScene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(runnerScene.path))
                return;

            Assert.IsFalse(runnerScene.isDirty, "Do not save an unrelated dirty scene.");
            GameObject[] roots = runnerScene.GetRootGameObjects();
            Assert.IsTrue(roots.Length == 0 || (roots.Length == 2
                && roots.Any(go => go.name == "Main Camera")
                && roots.Any(go => go.name == "Directional Light")),
                "Only the empty/default scene created by the isolated Runner may be saved.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Assert.IsFalse(SceneManager.GetSceneAt(i).isDirty);

            if (!AssetDatabase.IsValidFolder("Assets/Temp"))
                AssetDatabase.CreateFolder("Assets", "Temp");
            _runnerSceneFolder = "Assets/Temp/Issue770Runner_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/Temp", Path.GetFileName(_runnerSceneFolder));
            _runnerScene = runnerScene;
            _runnerSceneHadDefaultObjects = roots.Length == 2;
            Assert.IsTrue(EditorSceneManager.SaveScene(runnerScene, _runnerSceneFolder + "/Runner.unity"));
        }

        [OneTimeTearDown]
        public void RemoveOwnedRunnerScene()
        {
            if (string.IsNullOrEmpty(_runnerSceneFolder))
                return;
            if (_runnerScene.IsValid() && _runnerScene.isLoaded)
            {
                Assert.IsFalse(_runnerScene.isDirty);
                if (SceneManager.sceneCount > 1)
                    Assert.IsTrue(EditorSceneManager.CloseScene(_runnerScene, true));
                else
                    EditorSceneManager.NewScene(_runnerSceneHadDefaultObjects
                        ? NewSceneSetup.DefaultGameObjects : NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            Assert.IsTrue(AssetDatabase.DeleteAsset(_runnerSceneFolder));
            _runnerSceneFolder = null;
        }

        [SetUp]
        public void SetUp()
        {
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsTrue(string.IsNullOrEmpty(CharacterTestLevelEditorDocumentSession.CurrentPath),
                "Run in an isolated Editor with no selected level document; do not reset an unrelated session.");
            _previousActiveScene = SceneManager.GetActiveScene();
            _previousSelection = Selection.activeObject;
            if (!AssetDatabase.IsValidFolder("Assets/Temp"))
                AssetDatabase.CreateFolder("Assets", "Temp");
            _folder = "Assets/Temp/Issue770Copy_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/Temp", Path.GetFileName(_folder));
            _scenePath = _folder + "/Copy.unity";
            _documentPath = _folder + "/world.json";
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(_scene);
            Assert.AreEqual(_scene, SceneManager.GetActiveScene());
            var rootObject = new GameObject("Copy770Root");
            SceneManager.MoveGameObjectToScene(rootObject, _scene);
            _root = rootObject.transform;
            GameManager manager = rootObject.AddComponent<GameManager>();
            manager.WorldObjectSceneCache.ConfigureFromAuthoring(
                null, null, _documentPath, string.Empty, string.Empty);
            var sceneSource = rootObject.AddComponent<CharacterTestWorldObjectSceneSource>();
            var serializedSource = new SerializedObject(sceneSource);
            serializedSource.FindProperty("_authoringJsonPath").stringValue = _documentPath;
            serializedSource.ApplyModifiedPropertiesWithoutUndo();
            var group = new GameObject("Movables");
            group.transform.SetParent(_root, false);
            _source = new GameObject("Probe");
            _source.transform.SetParent(group.transform, false);
            _source.transform.rotation = Quaternion.Euler(7f, 23f, 11f);
            _source.transform.localScale = new Vector3(2f, 3f, 4f);
            _peer = new GameObject("ZZZPeer");
            _peer.transform.SetParent(group.transform, false);
            Assert.IsTrue(EditorSceneManager.SaveScene(_scene, _scenePath));
            Undo.IncrementCurrentGroup();
            _undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Issue770 isolated fixture");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (CharacterTestWorldObjectLevelEditorModule module in _modules)
                RetireFixtureModule(module);
            _modules.Clear();
            if (_undoGroup >= 0)
                Undo.RevertAllDownToGroup(_undoGroup);
            if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(_previousActiveScene);
            if (_scene.IsValid() && _scene.isLoaded)
                EditorSceneManager.CloseScene(_scene, true);
            if (!string.IsNullOrEmpty(_folder))
                AssetDatabase.DeleteAsset(_folder);
            Selection.activeObject = _previousSelection;
            _undoGroup = -1;
        }

        [TestCase("movables", false)]
        [TestCase("movables", true)]
        [TestCase("static.single", false)]
        [TestCase("static.single", true)]
        [TestCase("points", false)]
        [TestCase("points", true)]
        [TestCase("regions", false)]
        [TestCase("regions", true)]
        public void Duplicate_PreservesSelectedConfigurationWhenAddReordersRows(string tree, bool customGroup)
        {
            CharacterTestWorldObjectLevelEditorModule module = CreateModule(tree, customGroup);
            LevelWorldObjectAuthoringDocument before = Document(module);
            string sourcePath = PathOf(_source.transform);
            string peerPath = PathOf(_peer.transform);
            object source = Rows(before, tree).Single(row => PathOf(row) == sourcePath);
            object peer = Rows(before, tree).Single(row => PathOf(row) == peerPath);
            string sourceJson = JsonConvert.SerializeObject(source);
            string peerJson = JsonConvert.SerializeObject(peer);
            int originalIndex = Rows(before, tree).ToList().FindIndex(row => PathOf(row) == sourcePath);
            var position = new Vector3(13f, 5f, 17f);

            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult result =
                module.DuplicateRowForTests(SourceRow(module, tree), position);

            Assert.IsTrue(result.Success, result.Status);
            Assert.AreEqual("Probe Copy", result.GameObject.name);
            Assert.AreEqual(position, result.GameObject.transform.position);
            Assert.AreEqual(_source.transform.rotation, result.GameObject.transform.rotation);
            Assert.AreEqual(_source.transform.localScale, result.GameObject.transform.localScale);
            Assert.Less(string.CompareOrdinal(PathOf(result.GameObject.transform), sourcePath), 0,
                "The normal Copy name must sort before Probe in the original reproduction.");
            LevelWorldObjectAuthoringDocument after = Document(module);
            Assert.AreNotEqual(originalIndex,
                Rows(after, tree).ToList().FindIndex(row => PathOf(row) == sourcePath),
                "Exercise the captured row-index shift instead of renaming the copy to avoid sorting.");
            object target = Rows(after, tree).Single(row => PathOf(row) == PathOf(result.GameObject.transform));
            AssertConfiguration(source, target, tree);
            AssertIdentity(source, target);
            Assert.AreEqual(sourceJson, JsonConvert.SerializeObject(Rows(after, tree).Single(row => PathOf(row) == sourcePath)));
            Assert.AreEqual(peerJson, JsonConvert.SerializeObject(Rows(after, tree).Single(row => PathOf(row) == peerPath)));
            Assert.IsFalse(LevelWorldObjectAuthoringValidator.Validate(after).HasErrors);
        }

        [UnityTest]
        public IEnumerator Duplicate_OneStepUndoRedoAndTemporarySceneEnvelopeRoundtrip()
        {
            const string tree = "movables";
            CharacterTestWorldObjectLevelEditorModule module = CreateModule(tree, true);
            object source = Rows(Document(module), tree).Single(row => PathOf(row) == PathOf(_source.transform));
            string before = LevelWorldObjectAuthoringJson.SaveToJson(Document(module));
            CharacterTestWorldObjectLevelEditorModule.PlacementCreateResult result =
                module.DuplicateRowForTests(SourceRow(module, tree), new Vector3(13f, 5f, 17f));
            Assert.IsTrue(result.Success, result.Status);
            string copyPath = PathOf(result.GameObject.transform);
            yield return null;
            string copied = LevelWorldObjectAuthoringJson.SaveToJson(Document(module));
            AssertConfiguration(source, Rows(Document(module), tree).Single(row => PathOf(row) == copyPath), tree);

            Undo.PerformUndo();
            yield return null;
            Assert.AreEqual(before, LevelWorldObjectAuthoringJson.SaveToJson(Document(module)));
            Assert.IsNull(Resolve(copyPath));

            Undo.PerformRedo();
            yield return null;
            Assert.AreEqual(copied, LevelWorldObjectAuthoringJson.SaveToJson(Document(module)));
            GameObject restoredCopy = Resolve(copyPath);
            Assert.IsNotNull(restoredCopy);
            Assert.AreEqual(new Vector3(13f, 5f, 17f), restoredCopy.transform.position);

            string envelopeJson = module.EnvelopeJsonForTests;
            Assert.IsTrue(CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                MxLevelAuthoringJson.LoadFromJson(envelopeJson), out var envelopeDocument, out _));
            Assert.AreEqual(copied, LevelWorldObjectAuthoringJson.SaveToJson(envelopeDocument));
            File.WriteAllText(FullPath(_documentPath), envelopeJson);
            Assert.IsTrue(EditorSceneManager.SaveScene(_scene, _scenePath));
            RetireFixtureModule(module);
            Assert.IsTrue(EditorSceneManager.CloseScene(_scene, true));
            _scene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Additive);
            EditorSceneManager.SetActiveScene(_scene);
            Assert.AreEqual(_scene, SceneManager.GetActiveScene());
            _root = _scene.GetRootGameObjects().Single(go => go.name == "Copy770Root").transform;
            _source = Resolve(PathOf(source));
            _peer = _root.Find("Movables/ZZZPeer").gameObject;
            var reopened = new CharacterTestWorldObjectLevelEditorModule();
            _modules.Add(reopened);
            Assert.AreEqual(_documentPath, reopened.DocumentPath,
                "The fixture GameManager must route Reload to its owned temporary document.");
            reopened.Reload();
            Assert.AreEqual(copied, LevelWorldObjectAuthoringJson.SaveToJson(Document(reopened)));
            AssertConfiguration(source, Rows(Document(reopened), tree).Single(row => PathOf(row) == copyPath), tree);
            Assert.IsNotNull(Resolve(copyPath));
            GameObject reloadedCopy = Resolve(copyPath);
            Assert.AreEqual(new Vector3(13f, 5f, 17f), reloadedCopy.transform.position);
            Assert.AreEqual(_source.transform.rotation, reloadedCopy.transform.rotation);
            Assert.AreEqual(_source.transform.localScale, reloadedCopy.transform.localScale);
            Assert.IsTrue(reopened.TryResolveRowObject(
                new MxLevelEditorContext(null, reopened, () => { }), SourceRow(reopened, tree), out Object resolved));
            Assert.AreEqual(_source, resolved);
            yield return null;
        }

        private CharacterTestWorldObjectLevelEditorModule CreateModule(string tree, bool customGroup)
        {
            string group = customGroup ? "ZZZ authored group" : "Movables";
            var document = new LevelWorldObjectAuthoringDocument
            {
                LevelId = "test.issue770.copy",
                ScenePath = _scenePath,
                RootPath = _root.name
            };
            foreach (bool source in new[] { true, false })
            {
                LevelTransformReference reference = CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(
                    _root, (source ? _source : _peer).transform);
                string notes = source ? "preserve source notes" : "keep peer notes";
                if (tree == "movables" || tree == "static.single")
                {
                    bool movable = tree == "movables";
                    (movable ? document.MovableInstances : document.SingleStaticColliders).Add(
                        new LevelWorldObjectInstanceDefinition
                        {
                            Enabled = !source,
                            Role = movable ? LevelWorldObjectRole.Movable : LevelWorldObjectRole.StaticCollider,
                            DefinitionId = movable ? CharacterTestWorldObjectDefinitionIds.MovableSlickLight
                                : CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                            NumericInstanceId = movable ? (source ? 1 : 2) : 0,
                            BodyId = movable ? 0 : (source ? 10000 : 10001),
                            ColliderId = source ? 7 : 9,
                            InstanceStableId = source ? "source.instance" : "peer.instance",
                            ScaleSource = source ? LevelWorldObjectScaleSource.Definition : LevelWorldObjectScaleSource.SceneTransform,
                            Group = group, Notes = notes, TransformRef = reference, TransformPath = reference.Path
                        });
                }
                else if (tree == "points")
                {
                    document.Points.Add(new LevelWorldObjectPointDefinition
                    {
                        Enabled = !source,
                        DefinitionId = CharacterTestWorldObjectDefinitionIds.PointPlayerSpawn,
                        StableId = source ? "source.point" : "peer.point",
                        PointKind = source ? LevelWorldObjectPointKind.Marker : LevelWorldObjectPointKind.Checkpoint,
                        Group = group, Notes = notes, TransformRef = reference, TransformPath = reference.Path
                    });
                }
                else
                {
                    document.Regions.Add(new LevelWorldObjectRegionDefinition
                    {
                        Enabled = !source,
                        DefinitionId = CharacterTestWorldObjectDefinitionIds.RegionStartArea,
                        StableId = source ? "source.region" : "peer.region",
                        RegionKind = source ? LevelWorldObjectRegionKind.TriggerRegion : LevelWorldObjectRegionKind.TestRegion,
                        ScaleSource = source ? LevelWorldObjectScaleSource.Definition : LevelWorldObjectScaleSource.SceneTransform,
                        Group = group, Notes = notes, TransformRef = reference, TransformPath = reference.Path
                    });
                }
            }
            var module = new CharacterTestWorldObjectLevelEditorModule();
            _modules.Add(module);
            module.SetDocumentForTests(document);
            return module;
        }

        private MxLevelEditorListRow SourceRow(CharacterTestWorldObjectLevelEditorModule module, string tree)
        {
            var context = new MxLevelEditorContext(null, module, () => { });
            return module.BuildRows(context, tree).Single(row =>
                module.TryResolveRowObject(context, row, out Object sceneObject) && sceneObject == _source);
        }

        private static LevelWorldObjectAuthoringDocument Document(CharacterTestWorldObjectLevelEditorModule module)
            => (LevelWorldObjectAuthoringDocument)typeof(CharacterTestWorldObjectLevelEditorModule)
                .GetField("_document", Private).GetValue(module);

        private static IEnumerable<object> Rows(LevelWorldObjectAuthoringDocument document, string tree)
        {
            switch (tree)
            {
                case "movables": return document.MovableInstances.Cast<object>();
                case "static.single": return document.SingleStaticColliders.Cast<object>();
                case "points": return document.Points.Cast<object>();
                default: return document.Regions.Cast<object>();
            }
        }

        private string PathOf(Transform transform)
            => CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(_root, transform).Path;

        private static string PathOf(object row)
        {
            if (row is LevelWorldObjectInstanceDefinition instance) return instance.TransformRef.Path;
            if (row is LevelWorldObjectPointDefinition point) return point.TransformRef.Path;
            return ((LevelWorldObjectRegionDefinition)row).TransformRef.Path;
        }

        private static void AssertConfiguration(object source, object target, string tree)
        {
            string[] fields = tree == "points"
                ? new[] { "Enabled", "DefinitionId", "PointKind", "Group", "Notes" }
                : tree == "regions"
                    ? new[] { "Enabled", "DefinitionId", "RegionKind", "ScaleSource", "Group", "Notes" }
                    : new[] { "Enabled", "DefinitionId", "ScaleSource", "ColliderId", "Group", "Notes" };
            JObject expected = JObject.FromObject(source), actual = JObject.FromObject(target);
            foreach (string field in fields)
                Assert.IsTrue(JToken.DeepEquals(expected[field], actual[field]), tree + ": " + field);
        }

        private static void AssertIdentity(object source, object target)
        {
            Assert.AreNotEqual(PathOf(source), PathOf(target));
            if (source is LevelWorldObjectInstanceDefinition instance)
            {
                var copy = (LevelWorldObjectInstanceDefinition)target;
                if (instance.Role == LevelWorldObjectRole.Movable)
                    Assert.AreNotEqual(instance.NumericInstanceId, copy.NumericInstanceId);
                else
                    Assert.AreNotEqual(instance.BodyId, copy.BodyId);
                Assert.AreNotEqual(instance.InstanceStableId, copy.InstanceStableId);
            }
            else if (source is LevelWorldObjectPointDefinition point)
                Assert.AreNotEqual(point.StableId, ((LevelWorldObjectPointDefinition)target).StableId);
            else
                Assert.AreNotEqual(((LevelWorldObjectRegionDefinition)source).StableId,
                    ((LevelWorldObjectRegionDefinition)target).StableId);
        }

        private GameObject Resolve(string path)
            => CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                _root, new LevelTransformReference(path), out Transform transform)
                ? transform.gameObject : null;

        private static string FullPath(string assetPath)
            => Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

        private static void RetireFixtureModule(CharacterTestWorldObjectLevelEditorModule module)
        {
            foreach (string field in new[] { "_undoSession", "_definitionUndoSession" })
            {
                var session = (CharacterTestLevelEditorDocumentUndoSession)typeof(CharacterTestWorldObjectLevelEditorModule)
                    .GetField(field, Private).GetValue(module);
                session?.Retire();
            }
            var deferred = (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                typeof(EditorApplication.CallbackFunction), module,
                typeof(CharacterTestWorldObjectLevelEditorModule).GetMethod("RunDeferredValidation", Private));
            EditorApplication.delayCall -= deferred;
        }
    }
}
