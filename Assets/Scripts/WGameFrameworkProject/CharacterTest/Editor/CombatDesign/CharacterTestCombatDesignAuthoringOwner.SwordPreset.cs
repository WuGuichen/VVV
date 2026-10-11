using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestCombatDesignAuthoringOwner
    {
        [MenuItem("MxFramework/CharacterTest/Tactical/Publish Single Sword Automatic Preset")]
        private static void PublishSwordPresetFromMenu()
        {
            try { Debug.Log(PublishSwordPreset().ToString(Formatting.Indented)); }
            catch (Exception error) { Debug.LogError("[SwordPreset] " + error.Message); }
        }
        internal static JObject PrepareSwordPreset()
        {
            Need(Ready,"EDITOR_BUSY"); var result = new JArray();
            using (var context = new AuthoringContext("main"))
                for (int kind=0;kind<3;kind++) {
                    var draft=context.EnsureSwordPresetCard(kind);
                    int numeric=CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AllocateForAuthoring(draft.PublishedStableId);
                    result.Add(new JObject { ["stableId"]=draft.PublishedStableId,["numericId"]=numeric,["active"]=CharacterTestAuthoredContentIds.IsActive(417,numeric) });
                }
            var entries=new JArray();
            for(int kind=0;kind<4;kind++) {
                int numeric=CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring(420,"behavior-entry/"+CharacterTestRuntimeAiPlannerSwordPreset.EntryIds[kind]);
                entries.Add(new JObject {["entryId"]=CharacterTestRuntimeAiPlannerSwordPreset.EntryIds[kind],["numericId"]=numeric,["active"]=CharacterTestAuthoredContentIds.IsActive(420,numeric)});
            }
            return new JObject {["cards"]=result,["entries"]=entries,["identitiesReady"]=result.All(row=>(bool)row["active"])&&entries.All(row=>(bool)row["active"])};
        }
        internal static JObject PublishSwordPreset()
        {
            var ready=PrepareSwordPreset(); Need((bool)ready["identitiesReady"],"SWORD_IDENTITY_RELOAD_REQUIRED");
            var results=new JArray();
            for(int kind=0;kind<3;kind++) using(var context=new AuthoringContext("main")) {
                var draft=context.EnsureSwordPresetCard(kind); context.ReconcileSwordPreset(draft);
                var request=new JObject { ["scope"]="main",["cardId"]=draft.CardDraftId,
                    ["snapshotToken"]=context.Library()["snapshotToken"],
                    ["configuration"]=new JObject {["characterId"]=CharacterTestRuntimeAiPlannerSwordPreset.CharacterId,["actions"]=kind==1?new JObject{["primary"]=455}:new JObject()} };
                var plan=context.Preflight(request);
                results.Add((bool)plan["hasChanges"] ? context.Publish(new JObject {["scope"]="main",["confirmed"]=true,["planToken"]=plan["planToken"]},Hash(Guid.NewGuid().ToString("N")))
                    : new JObject {["published"]=true,["stableId"]=draft.PublishedStableId,["revision"]=draft.SourcePublishedRevision});
            }
            using(var context=new AuthoringContext("main")) return new JObject {["cards"]=results,["group"]=context.EnsureSwordPresetGroup()};
        }
        [MenuItem("MxFramework/CharacterTest/Tactical/Save Single Sword 455 Trial Configuration")]
        private static void SaveSwordTrialFromMenu()
        {
            try { Debug.Log(EnsureSwordTrialConfiguration().ToString(Formatting.Indented)); }
            catch(Exception error) { Debug.LogError("[SwordPreset] " + error.Message); }
        }

        [MenuItem("MxFramework/CharacterTest/Tactical/Prepare Single Sword Automatic Preset Trial")]
        private static void PrepareSwordTrialFromMenu()
        {
            try { Debug.Log(PrepareSwordTrialInLab().ToString(Formatting.Indented)); }
            catch(Exception error) { Debug.LogError("[SwordPreset] " + error.Message); }
        }

        /// <summary>Explicit Editor trial on the existing transient Lab setup and exact saved 455 binding.</summary>
        /// <remarks>LowFreqAlloc: user-invoked preparation; no player inventory or persistent recovery pointer edits.</remarks>
        internal static JObject PrepareSwordTrialInLab()
        {
            Need(EditorApplication.isPlaying,"SWORD_TRIAL_REQUIRES_LAB_PLAY_MODE");
            var controller=UnityEngine.Object.FindFirstObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Need(controller!=null && controller.Phase==CharacterTestRuntimeAiPlannerDevScenePhase.Setup,"SWORD_TRIAL_REQUIRES_LAB_SETUP");
            var receipt=EnsureSwordTrialConfiguration();
            string role="character.definition."+CharacterTestRuntimeAiPlannerSwordPreset.CharacterId;
            var record=new CharacterTestConfiguredCardLibrary().List(role).Single(c=>c.Id==(string)receipt["configuredCardId"]);
            int action=record.Actions["primary"];
            controller.PrepareInitialSetupState();
            var config=controller.MatchConfig;
            var group=config.BehaviorGroups.Drafts.Single(g=>g.DisplayName==CharacterTestRuntimeAiPlannerSwordPreset.Name);
            for(int team=1;team<=2;team++) {
                var slots=team==1?config.Team1Slots:config.Team2Slots;
                for(int index=0;index<slots.Count;index++) {
                    Need(controller.SelectSetupSpawnSlot(team,index),controller.StatusMessage);
                    if(slots[index].Enabled!=(index==0)) Need(controller.ToggleSelectedSetupSlotEnabled(),controller.StatusMessage);
                    if(index!=0)continue;
                    Need(controller.SelectSetupCharacterId(CharacterTestRuntimeAiPlannerSwordPreset.CharacterId),controller.StatusMessage);
                    Need(controller.SelectSetupBehaviorGroupDraft(group.DraftId),controller.StatusMessage);
                    Need(controller.SetSetupSpawnPosition(team,0,new Vector2(0,team==1?-3:3)),controller.StatusMessage);
                    Need(controller.SelectTacticalTestAction(action),controller.StatusMessage);
                    var card=controller.DraftWorkspace.Cards.Values.Single(c=>c.PublishedStableId==CharacterTestRuntimeAiPlannerSwordPreset.CardIds[1]);
                    Need(controller.SetTacticalPreviewSlot(card.CardDraftId,"primary",CharacterTestRuntimeAiPlannerDevSceneController.AuthoringActionKey(action)),controller.StatusMessage);
                }
            }
            Need(controller.SelectSetupSpawnSlot(1,0),controller.StatusMessage);
            return new JObject {["configuration"]=receipt,["groupId"]=group.StableId,["groupRevision"]=group.SourceProjectRevision,
                ["workspaceId"]=controller.DraftWorkspace.WorkspaceId,["actors"]=2,["transientLabWorkspace"]=true,["playerProgressWritten"]=false};
        }

        // Existing configured-card format: project authoring only, never player progress/ownership.
        internal static JObject EnsureSwordTrialConfiguration()
        {
            string role="character.definition."+CharacterTestRuntimeAiPlannerSwordPreset.CharacterId;
            var catalog=CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(); Need(catalog.Success,catalog.Diagnostic);
            Need(catalog.CardCatalog.TryResolveLatestCompatible(CharacterTestRuntimeAiPlannerSwordPreset.CardIds[1],8,out var published),"SWORD_PUBLISHED_CARD_MISSING");
            var library=new CharacterTestConfiguredCardLibrary();
            var matches=library.List(role).Where(c=>c.Name==CharacterTestRuntimeAiPlannerSwordPreset.Name && c.BaseKey=="published:"+published.StableCardId).ToArray();
            Need(matches.Length<=1,"SWORD_TRIAL_CONFIGURATION_DUPLICATE");
            var record=matches.SingleOrDefault()??new CharacterTestConfiguredCard {Id=Guid.NewGuid().ToString("N"),Name=CharacterTestRuntimeAiPlannerSwordPreset.Name,OwnerCharacter=role};
            record.Source=published.TacticalSource.ToJson(); record.BaseKey="published:"+published.StableCardId;
            record.BaseRevision=published.Metadata.DisplayName+" r"+published.Revision;
            record.BaseFingerprint=Hash(record.Source.ToString(Formatting.None));
            record.Metadata=CharacterTestConfiguredCardMetadata.From(CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(published));
            record.Actions.Clear();record.Actions.Add("primary",455);
            Need(library.Save(record,record.Revision,record.FileHash,out string issue),issue);
            var loaded=library.List(role).Single(c=>c.Id==record.Id);
            Need(loaded.Actions.TryGetValue("primary",out int action)&&action==455 && loaded.OwnerCharacter==role,"SWORD_TRIAL_CONFIGURATION_READBACK_FAILED");
            return new JObject {["configuredCardId"]=loaded.Id,["revision"]=loaded.Revision,["fileHash"]=loaded.FileHash,
                ["path"]=System.IO.Path.GetFullPath("Authoring/RuntimeAiLab/TestCards/Characters/"+role+"/"+loaded.Id+".json"),
                ["characterStableId"]="char.000001",["runtimeCharacterId"]=CharacterTestRuntimeAiPlannerSwordPreset.CharacterId,
                ["publishedCardId"]=published.StableCardId,["publishedRevision"]=published.Revision,["publishedContentHash"]=published.ContentHash,
                ["actions"]=JObject.FromObject(loaded.Actions),["playerProgressWritten"]=false};
        }

        private sealed partial class AuthoringContext
        {
            internal void ReconcileSwordPreset(CharacterTestRuntimeAiLabBehaviorCardDraft card)
            {
                string path=JournalPath(card.CardDraftId); if(!System.IO.File.Exists(path))return;
                var journal=Read(path); Need((string)journal["stableId"]==card.PublishedStableId,"SWORD_RECOVERY_IDENTITY_CONFLICT");
                Reconcile(new JObject {["cardId"]=card.CardDraftId,["recoveryOperationId"]=journal["operationId"]});
            }
            internal CharacterTestRuntimeAiLabBehaviorCardDraft EnsureSwordPresetCard(int kind)
            {
                string stable=CharacterTestRuntimeAiPlannerSwordPreset.CardIds[kind];
                var existing=_workspace.Cards.Values.Where(card=>card.PublishedStableId==stable).ToArray();
                Need(existing.Length<=1,"SWORD_DUPLICATE_IDENTITY");
                if(existing.Length==1) {
                    var current=existing[0];
                    if(current.HasTacticalSource) {
                        var source=CharacterTestTacticalAuthoringJson.Write(CharacterTestRuntimeAiPlannerSwordPreset.CreateSource(kind,_catalog.CardCatalog));
                        Need(_workspace.TryEditStandaloneTacticalSource(new CharacterTestTacticalEditToken(_workspace,current),source,
                            CharacterTestRuntimeAiPlannerSwordPreset.CardName(kind),0,0,out string editIssue),editIssue);
                        Persist();
                    }
                    return current;
                }
                Need(!_catalog.CardCatalog.Cards.Any(card=>card.StableCardId==stable),"SWORD_PUBLISHED_SOURCE_NOT_IMPORTED");
                var draft=new CharacterTestRuntimeAiLabBehaviorCardDraft(Guid.NewGuid().ToString("D"),stable,
                    CharacterTestRuntimeAiPlannerSwordPreset.CardName(kind),"Tactical",null) {Cost=0,CooldownSeconds=0,IconKey="ui.behavior-card.custom"};
                draft.SetTacticalSource(CharacterTestRuntimeAiPlannerSwordPreset.CreateSource(kind,_catalog.CardCatalog));
                Need(_workspace.TryImportStandaloneTacticalDraft(draft,_workspace.CurrentRevision,out string issue),issue);
                Persist(); return _workspace.Cards[draft.CardDraftId];
            }
            internal JObject EnsureSwordPresetGroup()
            {
                var existing=_catalog.GroupCatalog.Groups.Where(group=>group.DisplayName==CharacterTestRuntimeAiPlannerSwordPreset.Name)
                    .OrderByDescending(group=>group.Revision).FirstOrDefault();
                var profile=new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("sword-preset-authoring") {DisplayName=CharacterTestRuntimeAiPlannerSwordPreset.Name};
                var group=new CharacterTestRuntimeAiLabBehaviorGroupDraft(Guid.NewGuid().ToString("D"),"",CharacterTestRuntimeAiPlannerSwordPreset.Name,profile);
                if(existing!=null) {group.PublishedStableId=existing.StableGroupId;group.SourceProjectRevision=existing.Revision;
                    group.SourceProjectContentHash=existing.ContentHash;group.SourceProjectPath=_catalog.GroupCatalogPath;group.SourceCatalogFileHash=_catalog.GroupFileHash;}
                var cards=new Dictionary<string,CharacterTestRuntimeAiLabBehaviorCardDraft>();
                for(int kind=0;kind<4;kind++) {
                    Need(_catalog.CardCatalog.TryResolveLatestCompatible(CharacterTestRuntimeAiPlannerSwordPreset.CardIds[kind],8,out var published),"SWORD_PUBLISHED_CARD_MISSING");
                    var draft=CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(published); cards.Add(draft.CardDraftId,draft);
                    var entry=new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,true,kind*10)
                        {EntryId=CharacterTestRuntimeAiPlannerSwordPreset.EntryIds[kind],
                            NumericId=CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring(420,"behavior-entry/"+CharacterTestRuntimeAiPlannerSwordPreset.EntryIds[kind]),
                            DefinitionNumericId=published.NumericId};
                    entry.SetResponsibilities(published.Metadata.Responsibilities);
                    CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ConfigureCanonicalArbitration(entry,published.Metadata);
                    group.Profile.Entries.Add(entry);
                    group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(entry.EntryId,draft.CardDraftId,published.StableCardId,kind*10,false,entry));
                }
                var session=Session();
                if(existing!=null) {
                    Need(session.TryHasUnpublishedGroupChanges(group,cards,out bool changed,out string issue),issue);
                    if(!changed)return new JObject {["stableGroupId"]=existing.StableGroupId,["revision"]=existing.Revision,["contentHash"]=existing.ContentHash};
                }
                var result=existing!=null?session.TryPublishGroup(group,cards):session.TryConfirmLocalGroupDraft(group,cards,CharacterTestRuntimeAiPlannerSwordPreset.Name,
                    "单手剑455自动交战：远距接敌，近距攻击后脱离，当前可验证威胁24帧内将命中则常规撤距，其余待机。行动/动能/冷却遵守原权威，不授予防御，不走抽弃牌。","单手剑,自动交战,正式预设");
                Need(result.Success,result.Diagnostic);
                return new JObject {["stableGroupId"]=result.Group.StableGroupId,["revision"]=result.Group.Revision,["contentHash"]=result.Group.ContentHash};
            }
        }
    }
}
