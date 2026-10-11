using System;
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
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelEditorClosedLoopNativeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private string _folder, _a, _b, _scenePath;
        private byte[] _catalogBefore, _groupCatalogBefore;
        private string _groupCatalogPath;
        private SceneSetup[] _setup;
        private MxLevelEditorWindow _window;
        private bool _hadWindow;
        private readonly System.Collections.Generic.List<string> _created = new System.Collections.Generic.List<string>();
        private CharacterTestLevelEditorDocumentSession Session => CharacterTestLevelEditorDocumentSession.Instance;
        private CharacterTestSceneCharacterLevelEditorModule Characters => MxLevelEditorModuleRegistry.Modules.OfType<CharacterTestSceneCharacterLevelEditorModule>().Single();
        private CharacterTestSceneCharacterAuthoringDocument CharacterDraft()
            => Characters.CaptureDraftModules().Single().Payload.ToObject<CharacterTestSceneCharacterAuthoringDocument>();
        private void Click(string name)
        {
            var button = _window.rootVisualElement.Q<Button>(name); Assert.IsNotNull(button, name);
            Assert.IsTrue(button.enabledInHierarchy, name);
            // Unity 6 keeps Clickable.Invoke protected. Exercise the actual button command,
            // after checking its real hierarchy enable state, without changing production UI.
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(invoke); invoke.Invoke(button.clickable, new object[] { null });
        }
        private static void Reassociate(MxLevelAuthoringDocument document, string scene, string root)
            => typeof(CharacterTestLevelEditorDocumentSession).GetMethod("Reassociate", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[]{document,scene,root});
        private void Rules()
        {
            if (_window.rootVisualElement.Q<IntegerField>("mx-level-rule-initialMomentum") == null) Click("mx-level-rules");
        }
        private void Select(string id, MxLevelEditorUnsavedDecision decision = MxLevelEditorUnsavedDecision.Discard)
            => Assert.IsTrue(Session.TrySelect(id, _window, decision, out var error), error);

        [SetUp] public void SetUp()
        {
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsFalse(EditorSceneManager.GetActiveScene().isDirty, "Do not replace an unrelated dirty Scene.");
            _setup = EditorSceneManager.GetSceneManagerSetup();
            _catalogBefore = File.ReadAllBytes(CharacterTestLevelAuthoringOperations.DirectoryPath);
            _groupCatalogPath = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load().GroupCatalogPath;
            _groupCatalogBefore = File.ReadAllBytes(_groupCatalogPath);
            string suffix = Guid.NewGuid().ToString("N").Substring(0,10);
            _folder = "Assets/Temp/MxLevelClosedLoop_" + suffix;
            Directory.CreateDirectory(_folder);
            _a = "test.mx.a." + suffix; _b = "test.mx.b." + suffix;
            _scenePath = _folder + "/Level.unity";
            Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/TacticalCardDemoScene.unity", _scenePath));
            AssetDatabase.Refresh(); EditorSceneManager.OpenScene(_scenePath);
            var catalog = CharacterTestLevelAuthoringOperations.Catalog;
            var original = catalog.Load("formal.sword.courtyard", CharacterTestLevelAuthoringOperations.ProjectRoot, false, out _);
            foreach (string id in new[]{_a,_b})
            {
                var document = CharacterTestLevelEditorDocumentFactory.Clone(original,id,false);
                Reassociate(document,_scenePath,CharacterTestLevelEditorDocumentSession.FindManager().WorldObjectSceneRoot.name);
                string path = _folder + "/" + id + ".json";
                CharacterTestLevelCatalog.AtomicWrite(path, MxLevelAuthoringJson.SaveToJson(document));
                catalog.Levels.Add(new CharacterTestLevelEntry{LevelId=id,DisplayName=id,Path=path});
            }
            CharacterTestLevelCatalog.AtomicWrite(CharacterTestLevelAuthoringOperations.DirectoryPath,JsonConvert.SerializeObject(catalog,Formatting.Indented));
            AssetDatabase.Refresh();
            _hadWindow = UnityEngine.Resources.FindObjectsOfTypeAll<MxLevelEditorWindow>().Length > 0;
            _window = EditorWindow.GetWindow<MxLevelEditorWindow>(); _window.Show();
            Session.ResetForTests(); Select(_a,MxLevelEditorUnsavedDecision.Cancel);
        }
        [TearDown] public void TearDown()
        {
            Session.ResetForTests();
            if (_window != null)
            {
                typeof(MxLevelEditorWindow).GetField("_skipCloseRestore",Private).SetValue(_window,true);
                if (!_hadWindow) _window.Close();
            }
            if (_catalogBefore != null) File.WriteAllBytes(CharacterTestLevelAuthoringOperations.DirectoryPath,_catalogBefore);
            if (_groupCatalogBefore != null) File.WriteAllBytes(_groupCatalogPath, _groupCatalogBefore);
            if (_setup != null && _setup.Any(s=>s.isActive && s.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(_setup);
            else EditorSceneManager.OpenScene("Assets/Scenes/CharacterTestRuntimeAiLabScene.unity");
            foreach (string path in _created) AssetDatabase.DeleteAsset(path);
            if (!string.IsNullOrEmpty(_folder)) AssetDatabase.DeleteAsset(_folder);
            AssetDatabase.Refresh();
        }

        [Test] public void SameSceneAtoBtoAKeepsIdentityAndWaterDraft()
        {
            var water = new GameObject("UnregisteredWaterDraft",typeof(Light)); water.GetComponent<Light>().intensity=19;
            Select(_b); Assert.AreEqual(_b,CharacterTestLevelEditorDocumentSession.CurrentLevelId);
            Select(_a); Assert.AreEqual(_a,CharacterTestLevelEditorDocumentSession.CurrentLevelId);
            Assert.IsTrue(water != null); Assert.AreEqual(19,water.GetComponent<Light>().intensity);
            Assert.IsTrue(Characters.DocumentPath.Contains(_a));
        }
        [Test] public void MetadataCancelReadOnlySaveAndCloseDirtyGuardRetainDraft()
        {
            Rules(); var field=_window.rootVisualElement.Q<IntegerField>("mx-level-rule-initialMomentum"); field.value += 1;
            Assert.IsTrue(Session.IsDirty);
            typeof(MxLevelEditorWindow).GetMethod("SyncUnsavedChangesState",Private,null,Type.EmptyTypes,null).Invoke(_window,null);
            Assert.IsTrue(_window.hasUnsavedChanges);
            Assert.IsFalse(Session.TrySelect(_b,_window,MxLevelEditorUnsavedDecision.Cancel,out var cancel));
            Assert.AreEqual(_a,CharacterTestLevelEditorDocumentSession.CurrentLevelId);
            string path=CharacterTestLevelEditorDocumentSession.CurrentPath; byte[] before=File.ReadAllBytes(path);
            var attributes=File.GetAttributes(path);
            try
            {
                File.SetAttributes(path,attributes|FileAttributes.ReadOnly);
                Assert.IsFalse(Session.TrySelect(_b,_window,MxLevelEditorUnsavedDecision.Save,out var error));
                Assert.IsTrue(Session.IsDirty); Assert.AreEqual(_a,CharacterTestLevelEditorDocumentSession.CurrentLevelId);
                CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            }
            finally {File.SetAttributes(path,attributes);}
        }
        [Test] public void JoinedSaveUsesFinalDocumentForAllSceneCacheHashesAndReload()
        {
            Rules(); _window.rootVisualElement.Q<IntegerField>("mx-level-rule-initialMomentum").value += 2;
            var context = new MxLevelEditorContext(_window, Characters, null);
            var row = Characters.BuildRows(context, "document")[0];
            Assert.IsTrue(_window.RevealDocumentRow(Characters.ModuleId, "document", row.Key));
            var name = _window.rootVisualElement.Query<TextField>().ToList().Single(f => f.label == "显示名");
            name.value = "Joined runtime preflight";
            Assert.IsTrue(Session.TrySave(MxLevelEditorModuleRegistry.Modules,out var error),error);
            string path=CharacterTestLevelEditorDocumentSession.CurrentPath;
            var final=MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(path));
            var manager=CharacterTestLevelEditorDocumentSession.FindManager(); string expected=MxLevelAuthoringJson.ComputeContentHash(final);
            Assert.AreEqual(expected,manager.WorldObjectSceneCache.AuthoringContentHash);
            Assert.AreEqual(expected,manager.CharacterSceneCache.AuthoringContentHash);
            Assert.AreEqual(expected,manager.GameplayTriggerSceneCache.AuthoringContentHash);
            Assert.IsFalse(Session.IsDirty); Assert.IsFalse(EditorSceneManager.GetActiveScene().isDirty);
            Session.ReloadSavedDocument(); _window.ReloadDocumentModules();
            var reloaded=(MxLevelAuthoringDocument)typeof(CharacterTestLevelEditorDocumentSession).GetField("_draft",Private).GetValue(Session);
            Assert.AreEqual(expected,MxLevelAuthoringJson.ComputeContentHash(reloaded));
            // Repeated validation/drawing must keep the persisted envelope, including
            // empty canonical root refs and long publication hashes represented as strings.
            var envelopeField = typeof(CharacterTestSceneCharacterLevelEditorModule).GetField("_envelope", Private);
            string beforeChecks = MxLevelAuthoringJson.SaveToJson((MxLevelAuthoringDocument)envelopeField.GetValue(Characters));
            Assert.AreEqual(expected, MxLevelAuthoringJson.ComputeContentHash((MxLevelAuthoringDocument)envelopeField.GetValue(Characters)));
            var cacheCheck = typeof(CharacterTestSceneCharacterLevelEditorModule).GetMethod("BuildCacheSyncStatus", Private,
                null, new[] { typeof(GameManager) }, null);
            for (int i = 0; i < 3; i++)
            {
                Characters.CaptureDraftModules(); // Rules rendering and joined reads must also remain read-only.
                typeof(CharacterTestSceneCharacterLevelEditorModule).GetMethod("ForceValidate", Private).Invoke(Characters, null);
                var status = cacheCheck.Invoke(Characters, new object[] { manager });
                Assert.IsTrue((bool)status.GetType().GetProperty("IsApplied").GetValue(status));
                Assert.AreEqual(beforeChecks, MxLevelAuthoringJson.SaveToJson((MxLevelAuthoringDocument)envelopeField.GetValue(Characters)));
            }
            var currentCharacters = (CharacterTestSceneCharacterAuthoringDocument)typeof(CharacterTestSceneCharacterLevelEditorModule)
                .GetField("_document", Private).GetValue(Characters);
            string savedName = currentCharacters.Characters[0].DisplayName;
            try
            {
                currentCharacters.Characters[0].DisplayName = savedName + " unsaved";
                var status = cacheCheck.Invoke(Characters, new object[] { manager });
                Assert.IsTrue((bool)status.GetType().GetProperty("NeedsApply").GetValue(status), "Changed draft must still be reported.");
                Assert.AreEqual(beforeChecks, MxLevelAuthoringJson.SaveToJson((MxLevelAuthoringDocument)envelopeField.GetValue(Characters)));
                Assert.AreEqual(expected, manager.CharacterSceneCache.AuthoringContentHash);
            }
            finally { currentCharacters.Characters[0].DisplayName = savedName; }
        }
        [Test] public void CompleteCandidatePreflightRejectsInvalidActorBeforeAnyDiskOrCacheWrite()
        {
            string path = CharacterTestLevelEditorDocumentSession.CurrentPath;
            byte[] jsonBefore = File.ReadAllBytes(path), sceneBefore = File.ReadAllBytes(_scenePath);
            var manager = CharacterTestLevelEditorDocumentSession.FindManager(); string cacheBefore = EditorJsonUtility.ToJson(manager);
            Rules(); _window.rootVisualElement.Q<IntegerField>("mx-level-rule-initialMomentum").value += 2;
            var context = new MxLevelEditorContext(_window, Characters, null);
            Assert.IsTrue(_window.RevealDocumentRow(Characters.ModuleId, "document", Characters.BuildRows(context, "document")[0].Key));
            var id = _window.rootVisualElement.Query<IntegerField>().ToList().Single(f => f.label == "角色 ID");
            id.value = int.MaxValue;
            Assert.IsFalse(Session.TrySave(MxLevelEditorModuleRegistry.Modules, out var error));
            Assert.IsNotEmpty(error); CollectionAssert.AreEqual(jsonBefore, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(sceneBefore, File.ReadAllBytes(_scenePath)); Assert.AreEqual(cacheBefore, EditorJsonUtility.ToJson(manager));
            Assert.IsTrue(Session.IsDirty); Assert.AreEqual(int.MaxValue, CharacterDraft().Characters[0].CharacterId);
        }
        [Test] public void UnsupportedNewAiGroupBindingKeepsDraftAndPublishedReferences()
        {
            var context = new MxLevelEditorContext(_window, Characters, null);
            var actor = CharacterDraft().Characters.First(c => c.ControlOwner == CharacterTestCharacterControlOwner.AiControlled && c.BehaviorBinding != null);
            var row = Characters.BuildRows(context, "document").Single(r => r.Key.StartsWith("character|" + actor.StableId + "|", StringComparison.Ordinal));
            Assert.IsTrue(_window.RevealDocumentRow(Characters.ModuleId, "document", row.Key));
            var picker = _window.rootVisualElement.Q<PopupField<string>>("mx-level-behavior-group");
            Assert.IsNotNull(picker);
            string existing = picker.value;
            byte[] catalogBeforeCopy = File.ReadAllBytes(_groupCatalogPath);
            string draftBeforeCopy = JsonConvert.SerializeObject(CharacterDraft());
            Click("mx-level-copy-behavior-group");
            CollectionAssert.AreEqual(catalogBeforeCopy, File.ReadAllBytes(_groupCatalogPath));
            Assert.AreEqual(draftBeforeCopy, JsonConvert.SerializeObject(CharacterDraft()));
            Assert.IsNotNull(_window.rootVisualElement.Q<HelpBox>("mx-level-ai-capability"));
            // Refresh rebuilds the visual tree; always reselect the same AI row and
            // retrieve its current picker before another simulated UI command.
            Assert.IsTrue(_window.RevealDocumentRow(Characters.ModuleId, "document", row.Key));
            picker = _window.rootVisualElement.Q<PopupField<string>>("mx-level-behavior-group");
            picker.value = "无正式行为组";
            Assert.IsNull(CharacterDraft().Characters.Single(c => c.StableId == actor.StableId).BehaviorBinding);
            string before = JsonConvert.SerializeObject(CharacterDraft());
            Assert.IsTrue(_window.RevealDocumentRow(Characters.ModuleId, "document", row.Key));
            picker = _window.rootVisualElement.Q<PopupField<string>>("mx-level-behavior-group");
            picker.value = existing;
            Assert.AreEqual("无正式行为组", picker.value);
            Assert.AreEqual(before, JsonConvert.SerializeObject(CharacterDraft()));
            Assert.IsTrue(Characters.Status.Contains("未绑定，原配置保留"));
        }
        [Test] public void NewLevelAddsAiSelectsRealUpperGroupCopiesAndSaves()
        {
            var source = CharacterDraft();
            var original = source.Characters.First(c => c.ControlOwner == CharacterTestCharacterControlOwner.LocalPlayer);
            var originalGroupId = original.BehaviorBinding.GroupId;
            var originalGroupRevision = original.BehaviorBinding.GroupRevision;
            var actions = original.BehaviorBinding.Actions.ToArray();
            var context = new MxLevelEditorContext(_window, Characters, null);
            Assert.IsTrue(_window.RevealDocumentRow(Characters.ModuleId, "document", Characters.BuildRows(context, "document")[0].Key));
            string id = _a + ".upper.new";
            string created = Session.CreateDocument(id, "New playable upper group", _scenePath, true);
            _created.Add(Path.GetDirectoryName(created).Replace('\\', '/'));
            Select(id);
            Assert.IsEmpty(CharacterDraft().Characters);
            Click(CharacterTestSceneCharacterLevelEditorModule.EmptyStateDefaultButtonName);
            var added = CharacterDraft();
            Assert.AreEqual(id, added.LevelId, "Default creation must retain the selected level identity.");
            Assert.AreEqual(_scenePath, added.ScenePath);
            Assert.AreEqual(2, added.Characters.Count);
            Assert.AreEqual(1, added.Characters.Count(c => c.ControlOwner == CharacterTestCharacterControlOwner.AiControlled));
            var ai = added.Characters.Single(c => c.ControlOwner == CharacterTestCharacterControlOwner.AiControlled);
            var aiRow = Characters.BuildRows(context, "document").Single(r => r.Key.StartsWith("character|" + ai.StableId + "|", StringComparison.Ordinal));
            Assert.IsTrue(_window.RevealDocumentRow(Characters.ModuleId, "document", aiRow.Key));
            Click("mx-level-prepare-upper-group");
            var state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            var upper = state.GroupCatalog.Groups.Single(g => g.Tags.Contains(CharacterTestLevelUpperTacticalGroupPreset.Tag));
            Assert.AreEqual(3, upper.Cards.Count);
            foreach (var card in upper.Cards)
            {
                Assert.IsTrue(state.CardCatalog.TryGetExact(card.StableCardId, card.CardRevision, out var published));
                Assert.IsNotNull(published.TacticalSource); Assert.AreEqual(0, published.Metadata.Cost);
            }
            foreach (var actor in added.Characters)
            {
                var row = Characters.BuildRows(context, "document").Single(r => r.Key.StartsWith("character|" + actor.StableId + "|", StringComparison.Ordinal));
                Assert.IsTrue(_window.RevealDocumentRow(Characters.ModuleId, "document", row.Key));
                var picker = _window.rootVisualElement.Q<PopupField<string>>("mx-level-behavior-group");
                string wantedId = actor.ControlOwner == CharacterTestCharacterControlOwner.AiControlled ? upper.StableGroupId : originalGroupId;
                int wantedRevision = actor.ControlOwner == CharacterTestCharacterControlOwner.AiControlled ? upper.Revision : originalGroupRevision;
                picker.value = picker.choices.Single(label => label.EndsWith(" / " + wantedId + " @" + wantedRevision, StringComparison.Ordinal));
                Assert.AreEqual(wantedId, CharacterDraft().Characters.Single(c => c.StableId == actor.StableId).BehaviorBinding.GroupId);
                foreach (var action in actions)
                {
                    var field = _window.rootVisualElement.Q<PopupField<string>>("mx-level-slot-" + action.EntryId + "-" + action.SlotPath);
                    if (field != null) field.value = field.choices.Single(label => label.EndsWith(" / " + action.ActionId, StringComparison.Ordinal));
                }
            }
            Assert.IsTrue(_window.RevealDocumentRow(Characters.ModuleId, "document", aiRow.Key));
            Click("mx-level-copy-behavior-group");
            var copied = CharacterDraft().Characters.Single(c => c.StableId == ai.StableId).BehaviorBinding;
            Assert.AreNotEqual(upper.StableGroupId, copied.GroupId, "Supported independent-copy path must actually bind the new group.");
            Assert.AreEqual(originalGroupId, CharacterDraft().Characters.Single(c => c.ControlOwner == CharacterTestCharacterControlOwner.LocalPlayer).BehaviorBinding.GroupId);
            Rules();
            foreach (string fieldName in new[] { "playerSpawnStableId", "opponentSpawnStableId" })
            {
                var actor = added.Characters.Single(c => fieldName == "playerSpawnStableId"
                    ? c.ControlOwner == CharacterTestCharacterControlOwner.LocalPlayer : c.ControlOwner == CharacterTestCharacterControlOwner.AiControlled);
                var field = _window.rootVisualElement.Q<PopupField<string>>("mx-level-rule-" + fieldName);
                field.value = field.choices.Single(label => label.EndsWith(" / " + actor.StableId, StringComparison.Ordinal));
            }
            Assert.IsTrue(Session.TrySave(MxLevelEditorModuleRegistry.Modules, out var issue), issue);
            Assert.IsTrue(Session.TryCheck(out issue), issue);
            Session.ReloadSavedDocument(); _window.ReloadDocumentModules();
            Assert.AreEqual(id, CharacterDraft().LevelId); Assert.AreEqual(2, CharacterDraft().Characters.Count);
            Assert.AreEqual(copied.GroupId, CharacterDraft().Characters.Single(c => c.ControlOwner == CharacterTestCharacterControlOwner.AiControlled).BehaviorBinding.GroupId);
            Assert.IsFalse(Session.IsDirty); Assert.IsFalse(EditorSceneManager.GetActiveScene().isDirty);
        }
        [Test] public void NewAndCopyAreIndependentAndPreserveFormalReferences()
        {
            string copyId=_a+".copy",newId=_a+".new";
            string copy=Session.CreateDocument(copyId,"Copy",_scenePath,false); _created.Add(Path.GetDirectoryName(copy).Replace('\\','/'));
            string blank=Session.CreateDocument(newId,"Blank",_scenePath,true); _created.Add(Path.GetDirectoryName(blank).Replace('\\','/'));
            var catalog=CharacterTestLevelAuthoringOperations.Catalog;
            var original=catalog.Load(_a,CharacterTestLevelAuthoringOperations.ProjectRoot,false,out _);
            var copied=catalog.Load(copyId,CharacterTestLevelAuthoringOperations.ProjectRoot,false,out _);
            CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(original,out var before,out _);
            CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(copied,out var after,out _);
            Assert.AreEqual(before.Characters.Count,after.Characters.Count);
            Assert.IsFalse(before.Characters.Select(c=>c.StableId).Intersect(after.Characters.Select(c=>c.StableId)).Any());
            Assert.AreEqual(before.Characters[0].BehaviorBinding.GroupId,after.Characters[0].BehaviorBinding.GroupId);
            var empty=catalog.Load(newId,CharacterTestLevelAuthoringOperations.ProjectRoot,false,out _);
            CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(empty,out var none,out _); Assert.IsEmpty(none.Characters);
        }
        [Test] public void ListSceneBatchSelectionAndDeleteUndoUseExistingWindow()
        {
            var context=new MxLevelEditorContext(_window,Characters,null); var rows=Characters.BuildRows(context,"document");
            Assert.AreEqual(2,rows.Count);
            Assert.IsTrue(_window.RevealDocumentRow(Characters.ModuleId,"document",rows[0].Key));
            Assert.IsNotNull(Selection.activeGameObject);
            var view=_window.rootVisualElement.Q<ListView>("mx-level-editor-row-view"); Assert.IsNotNull(view);
            view.SetSelection(new[]{0,1});
            var group=_window.rootVisualElement.Q<TextField>("mx-level-batch-group");Assert.IsNotNull(group);group.value="Native UI Batch";
            Click("mx-level-batch-apply-group"); Assert.IsTrue(CharacterDraft().Characters.All(c=>c.Group=="Native UI Batch"));
            Undo.PerformUndo(); Assert.IsTrue(CharacterDraft().Characters.All(c=>c.Group!="Native UI Batch"));
            rows=Characters.BuildRows(context,"document"); Assert.AreEqual(1,Characters.DeleteRowForTests(rows[1],out var deleted));
            Assert.IsTrue(deleted==null); Assert.AreEqual(1,CharacterDraft().Characters.Count);
            Undo.PerformUndo(); Assert.AreEqual(2,CharacterDraft().Characters.Count);
            Assert.IsTrue(_window.RevealDocumentRow(Characters.ModuleId,"document",Characters.BuildRows(context,"document")[1].Key));
            Assert.IsNotNull(Selection.activeGameObject);
        }
        [Test] public void MissingFormalActionSlotLocatesTheActualPickerWithoutDrawingMutation()
        {
            var context=new MxLevelEditorContext(_window,Characters,null);var row=Characters.BuildRows(context,"document")[0];
            var draft=(CharacterTestSceneCharacterAuthoringDocument)typeof(CharacterTestSceneCharacterLevelEditorModule).GetField("_document",Private).GetValue(Characters);
            var actor=draft.Characters[0];var binding=actor.BehaviorBinding;Assert.IsNotEmpty(binding.Actions);
            var action=binding.Actions[0];binding.Actions.RemoveAt(0);
            string error=_a+"/"+actor.StableId+"/"+action.EntryId+"/"+action.SlotPath+": missing slot";
            int count=binding.Actions.Count;
            Assert.IsTrue(Characters.RevealLevelIssue(_window,error));
            Assert.AreEqual(count,binding.Actions.Count,"Drawing must not manufacture an action binding.");
            Assert.IsNotNull(_window.rootVisualElement.Q<VisualElement>("mx-level-slot-"+action.EntryId+"-"+action.SlotPath));
        }
    }
}
