using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using Newtonsoft.Json.Linq;
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

        private static CharacterTestWorldObjectDefinitionAuthoringDocument DefinitionDraft(CharacterTestWorldObjectLevelEditorModule module)
            => (CharacterTestWorldObjectDefinitionAuthoringDocument)typeof(CharacterTestWorldObjectLevelEditorModule)
                .GetField("_definitionDocument", Private).GetValue(module);

        private static MxLevelAuthoringDocument WorldEnvelope(CharacterTestWorldObjectLevelEditorModule module)
            => (MxLevelAuthoringDocument)typeof(CharacterTestWorldObjectLevelEditorModule).GetField("_envelope", Private).GetValue(module);

        private static void EditDefinitionDraft(CharacterTestWorldObjectLevelEditorModule module, string name)
        {
            DefinitionDraft(module).Definitions[0].DisplayName = name;
            typeof(CharacterTestWorldObjectLevelEditorModule).GetField("_definitionsDirty", Private).SetValue(module, true);
        }

        [Test] public void DefinitionRecoveryRetainsDirtyDraftAndOriginalUndoBaseline()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule(); module.OnEnable();
            string saved = CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(DefinitionDraft(module));
            EditDefinitionDraft(module, "未保存的定义名称");
            string draft = CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(DefinitionDraft(module));
            JObject state = module.CaptureReloadState();
            var restored = new CharacterTestWorldObjectLevelEditorModule();
            restored.RestoreReloadState(WorldEnvelope(module), state);
            Assert.AreEqual(draft, CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(DefinitionDraft(restored)));
            Assert.IsTrue(restored.IsDocumentDirty);
            var undo = (CharacterTestLevelEditorDocumentUndoSession)typeof(CharacterTestWorldObjectLevelEditorModule)
                .GetField("_definitionUndoSession", Private).GetValue(restored);
            Assert.AreEqual(saved, undo.SavedBaseline);
        }

        [Test] public void DefinitionRecoveryPreservesSemanticallyInvalidDraft()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule(); module.OnEnable();
            EditDefinitionDraft(module, string.Empty);
            JObject state = module.CaptureReloadState();
            var restored = new CharacterTestWorldObjectLevelEditorModule();
            restored.RestoreReloadState(WorldEnvelope(module), state);
            Assert.AreEqual(string.Empty, DefinitionDraft(restored).Definitions[0].DisplayName);
            Assert.IsTrue(restored.IsDocumentDirty);
        }

        [Test] public void RecoveredStaleDefinitionSourceHashBlocksSaveAndKeepsDraft()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule(); module.OnEnable();
            EditDefinitionDraft(module, "来源冲突草稿");
            JObject state = module.CaptureReloadState();
            state["definitions"]["sourceHash"] = "stale-source-generation";
            string path = CharacterTestWorldObjectDefinitionCatalogLoader.DefaultAuthoringPath;
            byte[] before = File.ReadAllBytes(path);
            var restored = new CharacterTestWorldObjectLevelEditorModule();
            restored.RestoreReloadState(WorldEnvelope(module), state);
            Assert.AreEqual("stale-source-generation", typeof(CharacterTestWorldObjectLevelEditorModule)
                .GetField("_definitionSourceHash", Private).GetValue(restored));
            Assert.IsFalse(MxLevelEditorSaveCoordinator.TrySaveModules(new IMxLevelEditorModule[] { restored }, out string issue));
            Assert.That(issue, Does.Contain(path));
            Assert.IsTrue(restored.IsDocumentDirty);
            Assert.AreEqual("来源冲突草稿", DefinitionDraft(restored).Definitions[0].DisplayName);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }

        [Test] public void LegacyWorldRecoveryWithoutDefinitionsKeepsDiskCatalogClean()
        {
            var module = new CharacterTestWorldObjectLevelEditorModule(); module.OnEnable();
            string saved = CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(DefinitionDraft(module));
            JObject state = module.CaptureReloadState(); state.Remove("definitions");
            var restored = new CharacterTestWorldObjectLevelEditorModule();
            restored.RestoreReloadState(WorldEnvelope(module), state);
            Assert.AreEqual(saved, CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(DefinitionDraft(restored)));
            Assert.IsFalse(restored.IsDocumentDirty);
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
