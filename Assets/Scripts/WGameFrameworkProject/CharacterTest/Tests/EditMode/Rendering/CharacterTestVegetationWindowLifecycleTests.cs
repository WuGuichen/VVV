using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Rendering
{
    public sealed class CharacterTestVegetationWindowLifecycleTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private List<IMxLevelEditorModule> _registry;
        private IMxLevelEditorModule[] _previousModules;
        private CharacterTestVegetationLevelEditorModule _module;
        private CharacterTestVegetationData _data;
        private MxLevelEditorWindow _window, _otherWindow;
        private string _folder, _scenePath, _dataPath;
        private bool _ownsFixture;
        private Scene _previousScene;
        private Scene[] _preservedScenes;
        private bool[] _preservedDirty;
        private string[] _preservedContents;

        [SetUp]
        public void SetUp()
        {
            _ownsFixture = false; _registry = null; _previousModules = null;
            _module = null; _data = null; _window = null; _otherWindow = null; _folder = null;
            var active = SceneManager.GetActiveScene();
            if (UnityEngine.Resources.FindObjectsOfTypeAll<MxLevelEditorWindow>().Length != 0)
                Assert.Ignore("Close the existing Level Editor window before lifecycle validation.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    Assert.Ignore("Existing scenes must have saved paths before additive fixture creation.");
            _previousScene = active;
            _preservedScenes = new Scene[SceneManager.sceneCount];
            _preservedDirty = new bool[_preservedScenes.Length];
            _preservedContents = new string[_preservedScenes.Length];
            for (int i = 0; i < _preservedScenes.Length; i++)
            {
                _preservedScenes[i] = SceneManager.GetSceneAt(i);
                _preservedDirty[i] = _preservedScenes[i].isDirty;
                _preservedContents[i] = CaptureScene(_preservedScenes[i]);
            }
            _ownsFixture = true;
            _folder = "Assets/__VegetationWindowLifecycleTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring(7));
            _scenePath = _folder + "/Fixture.unity";
            _dataPath = _folder + "/Data.asset";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            new GameObject("Owned camera", typeof(UnityEngine.Camera));
            new GameObject("Owned light", typeof(Light));
            _data = ScriptableObject.CreateInstance<CharacterTestVegetationData>();
            _data.EditableTypes.Add(new CharacterTestVegetationType { Id = 1, Name = "saved",
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterTestVegetationAuthoring.GrassPrefabPath) });
            AssetDatabase.CreateAsset(_data, _dataPath);
            CharacterTestVegetationAssetAuthoring.Bind(scene, _data);
            AssetDatabase.SaveAssetIfDirty(_data);
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, _scenePath));
            _module = new CharacterTestVegetationLevelEditorModule();
            _registry = (List<IMxLevelEditorModule>)MxLevelEditorModuleRegistry.Modules;
            _previousModules = _registry.ToArray(); _registry.Clear(); _registry.Add(_module);
            _window = OpenOwnedWindow();
        }

        [TearDown]
        public void TearDown()
        {
            if (!_ownsFixture) return;
            CloseForCleanup(_otherWindow); CloseForCleanup(_window);
            _module?.OnDisable();
            if (_registry != null && _previousModules != null)
            { _registry.Clear(); _registry.AddRange(_previousModules); }
            if (_data != null) Undo.ClearUndo(_data);
            var scene = string.IsNullOrEmpty(_scenePath) ? default(Scene) : SceneManager.GetSceneByPath(_scenePath);
            if (scene.IsValid()) CharacterTestVegetationAssetAuthoring.ReleaseScene(scene);
            if (_previousScene.IsValid() && _previousScene.isLoaded) SceneManager.SetActiveScene(_previousScene);
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (!string.IsNullOrEmpty(_folder) && _folder.StartsWith("Assets/__VegetationWindowLifecycleTests_", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(_folder);
            for (int i = 0; i < _preservedScenes.Length; i++)
            {
                Assert.IsTrue(_preservedScenes[i].isLoaded, "An existing user scene was closed.");
                Assert.AreEqual(_preservedDirty[i], _preservedScenes[i].isDirty, "User scene dirty state changed.");
                Assert.AreEqual(_preservedContents[i], CaptureScene(_preservedScenes[i]), "User scene contents changed.");
            }
        }

        [UnityTest]
        public IEnumerator DisableDetachesUndoButKeepsDirtyOwnerForCloseTransaction()
        {
            yield return null;
            int refreshes = 0; AttachRefreshCounter(_window, () => refreshes++);
            var owner = _module.ManagedSceneRoot;
            EditName("unsaved");
            InvokeWindow("OnDisable");
            Assert.AreSame(owner, _module.ManagedSceneRoot);
            Assert.IsTrue(_module.IsDirty);
            Assert.AreEqual("unsaved", _data.Types[0].Name);
            Undo.PerformUndo();
            Assert.AreEqual("saved", _data.Types[0].Name);
            Assert.AreEqual(0, refreshes);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ClosedWindowUndoDoesNotRefreshDestroyedUi()
        {
            yield return null;
            int refreshes = 0; AttachRefreshCounter(_window, () => refreshes++);
            _window.Close();
            Assert.IsTrue(_window == null);
            EditName("after close"); Undo.PerformUndo();
            Assert.AreEqual(0, refreshes);
            Assert.AreEqual("saved", _data.Types[0].Name);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ReopenAndRepeatedEnableSubscribeUndoExactlyOnce()
        {
            yield return null;
            _window.Close(); _window = OpenOwnedWindow();
            yield return null;
            int refreshes = 0; AttachRefreshCounter(_window, () => refreshes++);
            _module.OnEnable(); _module.OnEnable();
            EditName("reopened"); Undo.PerformUndo();
            Assert.AreEqual(1, refreshes);
            Assert.AreEqual("saved", _data.Types[0].Name);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DestroyedWindowReferenceUsesUnityNullBeforeCallingUiDelegate()
        {
            yield return null;
            var destroyed = _window; _window.Close();
            Assert.IsTrue(destroyed == null);
            Assert.IsFalse(ReferenceEquals(destroyed, null));
            int refreshes = 0;
            _module.CreateContent(destroyed);
            _module.BuildTree(new MxLevelEditorContext(destroyed, _module, () => refreshes++));
            EditName("fake null"); Undo.PerformUndo();
            Assert.AreEqual(0, refreshes);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DetachFromOtherWindowKeepsCurrentWindowUndoSubscription()
        {
            yield return null;
            _otherWindow = OpenOwnedWindow();
            yield return null;
            int refreshes = 0; AttachRefreshCounter(_window, () => refreshes++);
            _otherWindow.Close();
            EditName("current owner");
            // Closing/editing can refresh chrome; count only the following Undo callback.
            refreshes = 0;
            Undo.PerformUndo();
            Assert.AreEqual(1, refreshes);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SaveAfterDetachWritesRetainedAssetAndSceneOwner()
        {
            yield return null;
            EditName("saved after detach"); InvokeWindow("OnDisable");
            _window.SaveChanges();
            Assert.That(File.ReadAllText(_dataPath), Does.Contain("saved after detach"));
            Assert.IsFalse(EditorUtility.IsDirty(_data));
            Assert.IsFalse(SceneManager.GetActiveScene().isDirty);
            Assert.IsFalse(_module.IsDirty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DiscardAfterDetachRestoresSavedAssetAndScene()
        {
            yield return null;
            EditName("discard me");
            _module.ManagedSceneRoot.localPosition = new Vector3(3, 0, 0);
            InvokeWindow("OnDisable"); _window.DiscardChanges();
            var restored = AssetDatabase.LoadAssetAtPath<CharacterTestVegetationData>(_dataPath);
            Assert.AreEqual("saved", restored.Types[0].Name);
            Assert.AreEqual(Vector3.zero, _module.ManagedSceneRoot.localPosition);
            Assert.IsFalse(EditorUtility.IsDirty(restored));
            Assert.IsFalse(SceneManager.GetActiveScene().isDirty);
            Assert.IsFalse(_module.IsDirty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DestroyFallbackRestoresBeforeDetachingReloadedModuleAgain()
        {
            yield return null;
            int refreshes = 0; AttachRefreshCounter(_window, () => refreshes++);
            EditName("fallback"); InvokeWindow("OnDisable"); InvokeWindow("OnDestroy");
            Assert.AreEqual("saved", AssetDatabase.LoadAssetAtPath<CharacterTestVegetationData>(_dataPath).Types[0].Name);
            Assert.IsFalse(_module.IsDirty);
            AttachCounterOnly(_window, () => refreshes++);
            EditName("after fallback"); Undo.PerformUndo();
            Assert.AreEqual(0, refreshes, "OnDestroy must detach the subscription made by Reload.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SceneSwitchReloadPreservesUnsavedDataInsteadOfDiscardingIt()
        {
            yield return null;
            EditName("keep on scene switch");
            var original = SceneManager.GetActiveScene();
            var other = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(other);
            _module.Reload();
            SceneManager.SetActiveScene(original); _module.Reload();
            EditorSceneManager.CloseScene(other, true);
            Assert.AreEqual("keep on scene switch", _data.Types[0].Name);
            Assert.IsTrue(EditorUtility.IsDirty(_data));
            LogAssert.NoUnexpectedReceived();
        }

        private MxLevelEditorWindow OpenOwnedWindow()
        {
            var window = ScriptableObject.CreateInstance<MxLevelEditorWindow>();
            window.titleContent = new GUIContent("Owned vegetation lifecycle validation");
            window.ShowUtility();
            return window;
        }
        private void AttachRefreshCounter(MxLevelEditorWindow window, Action refresh)
        {
            _module.CreateContent(window);
            AttachCounterOnly(window, refresh);
        }
        private void AttachCounterOnly(MxLevelEditorWindow window, Action refresh)
        { _module.BuildTree(new MxLevelEditorContext(window, _module, refresh)); }
        private void EditName(string name)
        {
            _data = AssetDatabase.LoadAssetAtPath<CharacterTestVegetationData>(_dataPath);
            Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(_data, "Owned lifecycle data edit");
            _data.EditableTypes[0].Name = name; EditorUtility.SetDirty(_data);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); Undo.FlushUndoRecordObjects();
        }
        private void InvokeWindow(string method)
        { typeof(MxLevelEditorWindow).GetMethod(method, PrivateInstance).Invoke(_window, null); }
        private static void CloseForCleanup(MxLevelEditorWindow window)
        {
            if (window == null) return;
            typeof(MxLevelEditorWindow).GetField("_skipCloseRestore", PrivateInstance).SetValue(window, true);
            typeof(EditorWindow).GetProperty("hasUnsavedChanges", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(window, false);
            window.Close();
        }
        private static string CaptureScene(Scene scene)
        {
            var values = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                    foreach (Component component in transform.GetComponents<Component>())
                        if (component != null)
                            values.Add(GlobalObjectId.GetGlobalObjectIdSlow(component) + ":" + EditorJsonUtility.ToJson(component));
            return string.Join("\n", values);
        }
    }
}
