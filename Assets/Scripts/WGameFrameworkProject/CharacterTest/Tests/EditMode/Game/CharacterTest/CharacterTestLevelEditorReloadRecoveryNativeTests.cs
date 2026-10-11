using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelEditorReloadRecoveryNativeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private CharacterTestLevelEditorClosedLoopNativeTests _fixture;
        private MxLevelEditorWindow Window => EditorWindow.GetWindow<MxLevelEditorWindow>();
        private CharacterTestLevelEditorDocumentSession Session => CharacterTestLevelEditorDocumentSession.Instance;
        private CharacterTestSceneCharacterLevelEditorModule Characters
            => MxLevelEditorModuleRegistry.Modules.OfType<CharacterTestSceneCharacterLevelEditorModule>().Single();
        private CharacterTestSceneCharacterAuthoringDocument Draft
            => Characters.CaptureDraftModules().Single().Payload.ToObject<CharacterTestSceneCharacterAuthoringDocument>();

        [SetUp] public void SetUp() { _fixture = new CharacterTestLevelEditorClosedLoopNativeTests(); _fixture.SetUp(); }
        [TearDown] public void TearDown() { _fixture?.TearDown(); }
        private void Click(string name)
        {
            var button = Window.rootVisualElement.Q<Button>(name); Assert.IsNotNull(button, name);
            Assert.IsTrue(button.enabledInHierarchy);
            typeof(Clickable).GetMethod("Invoke", Private).Invoke(button.clickable, new object[] { null });
        }
        private void EditRulesAndBinding()
        {
            if (Window.rootVisualElement.Q<IntegerField>("mx-level-rule-initialMomentum") == null) Click("mx-level-rules");
            Window.rootVisualElement.Q<IntegerField>("mx-level-rule-initialMomentum").value = 68;
            var context = new MxLevelEditorContext(Window, Characters, null);
            var actor = Draft.Characters.Single(c => c.ControlOwner == CharacterTestCharacterControlOwner.AiControlled);
            var row = Characters.BuildRows(context, "document").Single(r => r.Key.StartsWith("character|" + actor.StableId + "|", StringComparison.Ordinal));
            Assert.IsTrue(Window.RevealDocumentRow(Characters.ModuleId, "document", row.Key));
            var picker = Window.rootVisualElement.Q<PopupField<string>>("mx-level-behavior-group");
            var group = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load().GroupCatalog.Groups
                .First(g => g.Tags.Contains("mx.level.upper-tactical.v1"));
            picker.value = picker.choices.Single(s => s.EndsWith(" / " + group.StableGroupId + " @" + group.Revision, StringComparison.Ordinal));
        }
        private void CaptureThenForgetManagedFields()
        {
            typeof(CharacterTestLevelEditorDocumentSession).GetMethod("CaptureReloadRecovery", Private).Invoke(Session, null);
            // Native references and SessionState survive; managed owner fields do not.
            typeof(CharacterTestLevelEditorDocumentSession).GetField("_draft", Private).SetValue(Session, null);
            typeof(CharacterTestLevelEditorDocumentSession).GetField("_loadedPath", Private).SetValue(Session, null);
            typeof(CharacterTestLevelEditorDocumentSession).GetField("_sceneBaseline", Private).SetValue(Session, null);
            Window.ReloadDocumentModules();
            typeof(CharacterTestLevelEditorDocumentSession).GetMethod("EnsureLoaded", Private).Invoke(Session, null);
        }

        [Test] public void CompleteRecoveryRetainsRulesBindingDirtyAndScopedDiscard()
        {
            string path = CharacterTestLevelEditorDocumentSession.CurrentPath;
            byte[] saved = File.ReadAllBytes(path);
            string originalGroup = Draft.Characters.Single(c => c.ControlOwner == CharacterTestCharacterControlOwner.AiControlled).BehaviorBinding.GroupId;
            EditRulesAndBinding();
            string group = Draft.Characters.Single(c => c.ControlOwner == CharacterTestCharacterControlOwner.AiControlled).BehaviorBinding.GroupId;
            var other = new GameObject("UnregisteredReloadLight", typeof(Light)); other.GetComponent<Light>().intensity = 19;
            CaptureThenForgetManagedFields();
            Assert.IsTrue(Session.IsDirty); Assert.IsTrue(Characters.IsDocumentDirty);
            Assert.AreEqual(group, Draft.Characters.Single(c => c.ControlOwner == CharacterTestCharacterControlOwner.AiControlled).BehaviorBinding.GroupId);
            var metadata = (MxLevelAuthoringDocument)typeof(CharacterTestLevelEditorDocumentSession).GetField("_draft", Private).GetValue(Session);
            Assert.AreEqual(68, (int)metadata.Modules.Single(m => m.ModuleId == "characterTest.tacticalStage").Payload["initialMomentum"]);
            CollectionAssert.AreEqual(saved, File.ReadAllBytes(path));
            Assert.IsTrue(Session.TryDiscardDraft(out var issue), issue); Window.ReloadDocumentModules();
            Assert.IsFalse(Session.IsDirty);
            Assert.AreEqual(originalGroup, Draft.Characters.Single(c => c.ControlOwner == CharacterTestCharacterControlOwner.AiControlled).BehaviorBinding.GroupId);
            Assert.IsTrue(other != null); Assert.AreEqual(19, other.GetComponent<Light>().intensity);
        }

        [Test] public void ChangedSourceKeepsRecoveryDraftAndRejectsWrite()
        {
            string path = CharacterTestLevelEditorDocumentSession.CurrentPath;
            EditRulesAndBinding();
            typeof(CharacterTestLevelEditorDocumentSession).GetMethod("CaptureReloadRecovery", Private).Invoke(Session, null);
            var foreign = MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(path));
            foreign.Modules.Single(m => m.ModuleId == "characterTest.tacticalStage").Payload["initialMomentum"] = 81;
            File.WriteAllText(path, MxLevelAuthoringJson.SaveToJson(foreign));
            byte[] changed = File.ReadAllBytes(path);
            // Do not capture again after the foreign edit: recover the original source generation.
            typeof(CharacterTestLevelEditorDocumentSession).GetField("_draft", Private).SetValue(Session, null);
            typeof(CharacterTestLevelEditorDocumentSession).GetField("_loadedPath", Private).SetValue(Session, null);
            Window.ReloadDocumentModules();
            Assert.IsTrue(Session.IsDirty);
            Assert.IsFalse(Session.TrySave(MxLevelEditorModuleRegistry.Modules, out var issue)); Assert.IsNotEmpty(issue);
            CollectionAssert.AreEqual(changed, File.ReadAllBytes(path));
            Assert.AreEqual(68, (int)((MxLevelAuthoringDocument)typeof(CharacterTestLevelEditorDocumentSession).GetField("_draft", Private)
                .GetValue(Session)).Modules.Single(m => m.ModuleId == "characterTest.tacticalStage").Payload["initialMomentum"]);
            Assert.IsTrue(Session.TryDiscardDraft(out issue), issue); Window.ReloadDocumentModules();
            Assert.IsFalse(Session.IsDirty); CollectionAssert.AreEqual(changed, File.ReadAllBytes(path));
        }

        [Test] public void DeepDeletedOwnedHierarchySurvivesReloadRecord()
        {
            var owned = new GameObject("DeepReloadOwned");
            var leaf = owned.transform;
            for (int i = 0; i < 14; i++)
            {
                var child = new GameObject("Depth" + i);
                child.transform.SetParent(leaf, false); leaf = child.transform;
            }
            leaf.gameObject.AddComponent<Light>().intensity = 7;
            var baseline = CharacterTestLevelEditorOwnedSceneSnapshot.CreateForOwnedTransforms(owned.transform);
            string json = baseline.CaptureReloadState();
            UnityEngine.Object.DestroyImmediate(owned);
            var recovered = CharacterTestLevelEditorOwnedSceneSnapshot.RestoreReloadState(json);
            Assert.IsTrue(recovered.TryRestore(MxLevelEditorModuleRegistry.Modules, out var issue), issue);
            var restored = GameObject.Find("DeepReloadOwned").transform;
            for (int i = 0; i < 14; i++) restored = restored.GetChild(0);
            Assert.AreEqual("Depth13", restored.name);
            Assert.AreEqual(7, restored.GetComponent<Light>().intensity);
        }

        [Test] public void SceneBaselineJsonRoundTripRestoresOnlyOwnedPose()
        {
            var owned = new GameObject("ReloadOwnedPose", typeof(Light));
            var baseline = CharacterTestLevelEditorOwnedSceneSnapshot.CreateForOwnedTransforms(owned.transform);
            owned.transform.position = new Vector3(4, 2, 7); owned.GetComponent<Light>().intensity = 23;
            var child = new GameObject("LaterUnregisteredChild"); child.transform.SetParent(owned.transform);
            var recovered = CharacterTestLevelEditorOwnedSceneSnapshot.RestoreReloadState(baseline.CaptureReloadState());
            Assert.IsTrue(recovered.TryRestore(MxLevelEditorModuleRegistry.Modules, out var issue), issue);
            Assert.AreEqual(Vector3.zero, owned.transform.position);
            Assert.AreEqual(23, owned.GetComponent<Light>().intensity); Assert.IsTrue(child != null);
        }
    }
}
