using System;
using System.Collections.Generic;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelEditorOwnedSceneNativeTests
    {
        private Scene _scene;
        [SetUp] public void SetUp() => _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        [TearDown] public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        [Test] public void ScopedDiscardRestoresActorPoseAndRetainsWaterComponentDraft()
        {
            var root = new GameObject("Root"); var actor = new GameObject("Actor"); actor.transform.SetParent(root.transform);
            var waterProxy = actor.AddComponent<Light>(); waterProxy.intensity = 2f;
            var snapshot = CharacterTestLevelEditorOwnedSceneSnapshot.CreateForOwnedTransforms(actor.transform);
            actor.transform.position = new Vector3(3f, 0f, 4f); waterProxy.intensity = 7f;
            Assert.IsTrue(snapshot.TryRestore(Array.Empty<IMxLevelEditorModule>(), out var error), error);
            Assert.AreEqual(Vector3.zero, actor.transform.position); Assert.AreEqual(7f, waterProxy.intensity);
        }
        [Test] public void ScopedDiscardRetainsUnregisteredWaterObjectAndItsTransform()
        {
            var actor = new GameObject("Actor"); var water = new GameObject("Water");
            var snapshot = CharacterTestLevelEditorOwnedSceneSnapshot.CreateForOwnedTransforms(actor.transform);
            actor.transform.position = Vector3.one; water.transform.position = new Vector3(7f, 8f, 9f);
            Assert.IsTrue(snapshot.TryRestore(Array.Empty<IMxLevelEditorModule>(), out var error), error);
            Assert.AreEqual(Vector3.zero, actor.transform.position); Assert.AreEqual(new Vector3(7f, 8f, 9f), water.transform.position);
        }
        [Test] public void ScopedDiscardRecreatesDeletedRegisteredObjectWithChildren()
        {
            var actor = new GameObject("Actor"); var child = new GameObject("Child"); child.transform.SetParent(actor.transform);
            child.AddComponent<Light>().intensity = 7f;
            var snapshot = CharacterTestLevelEditorOwnedSceneSnapshot.CreateForOwnedTransforms(actor.transform);
            UnityEngine.Object.DestroyImmediate(actor);
            Assert.IsTrue(snapshot.TryRestore(Array.Empty<IMxLevelEditorModule>(), out var error), error);
            var restored = GameObject.Find("Actor"); Assert.IsNotNull(restored);
            Assert.AreEqual(7f, restored.GetComponentInChildren<Light>().intensity);
        }
        [Test] public void ScopedDiscardDeletesOnlyTrackedRegisteredAdditions()
        {
            var actor = new GameObject("Actor"); var water = new GameObject("Water");
            var snapshot = CharacterTestLevelEditorOwnedSceneSnapshot.CreateForOwnedTransforms(actor.transform);
            var added = new GameObject("New actor"); snapshot.TrackOwnedAddedTransform(added.transform);
            Assert.IsTrue(snapshot.TryRestore(Array.Empty<IMxLevelEditorModule>(), out var error), error);
            Assert.IsTrue(added == null); Assert.IsTrue(water != null); Assert.IsTrue(actor != null);
        }
        [Test] public void AmbiguousNewObjectDiscardFailsBeforeAnyPoseChanges()
        {
            var actor = new GameObject("Actor");
            var snapshot = CharacterTestLevelEditorOwnedSceneSnapshot.CreateForOwnedTransforms(actor.transform);
            var added = new GameObject("New actor"); snapshot.TrackOwnedAddedTransform(added.transform);
            var water = new GameObject("Water"); water.transform.SetParent(added.transform);
            actor.transform.position = Vector3.one;
            Assert.IsFalse(snapshot.TryRestore(Array.Empty<IMxLevelEditorModule>(), out var error));
            Assert.That(error, Does.Contain("其他编辑")); Assert.AreEqual(Vector3.one, actor.transform.position);
            Assert.IsTrue(added != null); Assert.IsTrue(water != null);
        }
        [Test] public void RetiredDocumentUndoDoesNotWriteIntoTheNextDocument()
        {
            string old = "A"; string current = "B";
            var undo = new CharacterTestLevelEditorDocumentUndoSession("A", () => old, value => current = value, null);
            undo.ResetBaseline(); using (var change = undo.BeginTransaction("A edit")) { old = "A edited"; change.Commit(); }
            undo.Retire(); Undo.PerformUndo(); Assert.AreEqual("B", current);
        }
    }
}
