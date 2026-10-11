using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Config.Levels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    // Explicit acceptance owner. No player inventory, Lab draft, scene save or build-setting write.
    public static class CharacterTestLevelAcceptance
    {
        private const string ScenePath = "Assets/Scenes/TacticalCardDemoScene.unity";
        private const string Source = "Assets/Config/MxFramework/Levels/TacticalCardDemo/tactical_card_demo.level.json";
        private const string Folder = "Assets/Config/MxFramework/Levels/FormalExamples";
        public static string ReceiptDirectory { get; set; }
        [MenuItem("MxFramework/Character Test/Levels/Create Formal Same Scene Examples")]
        public static void PrepareExamples()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Editor must be idle.");
            for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or preserve dirty Scene before acceptance.");
            var setup = EditorSceneManager.GetSceneManagerSetup(); float scale=Time.timeScale;
            try {
                EditorSceneManager.OpenScene(ScenePath);
                var catalog=CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(); if(!catalog.Success) throw new InvalidOperationException(catalog.Diagnostic);
                if(!catalog.GroupCatalog.TryGetExact("bg_06c30f0b24",2,out var group)) throw new InvalidOperationException("Formal sword group r2 missing.");
                var manager=CharacterTestLevelAuthoringOperations.Manager;
                var profile=manager.ConfiguredProfile;
                var builder=new CharacterTestRuntimeSessionBuilder(manager,new CharacterTestRuntimeContext(manager,profile),profile);
                var directory=new CharacterTestLevelCatalog {DefaultLevelId="formal.sword.courtyard"};
                var receipts=new List<object>();
                foreach(bool alternate in new[]{false,true}) {
                    var document=MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(Source));
                    document.LevelId=alternate?"formal.sword.crossfire":"formal.sword.courtyard";
                    CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(document,out var characters,out _);
                    characters.LevelId=document.LevelId;
                    foreach(var module in document.Modules) if(module.Payload["levelId"] != null) module.Payload["levelId"] = document.LevelId;
                    if(document.TryGetModule("characterTest.worldObjects",out var worldModule)) {
                        var ground=((JArray)worldModule.Payload["singleStaticColliders"]).Single(c=>(int)c["bodyId"]==10000);
                        worldModule.Payload["regions"]=new JArray(new JObject { ["enabled"]=true,["definitionId"]=3002,
                            ["stableId"]=document.LevelId+".navigation.bounds",["regionKind"]="TestRegion",
                            ["transformRef"]=ground["transformRef"].DeepClone(),["scaleSource"]="SceneTransform",
                            ["group"]="Navigation",["notes"]="Navigation footprint uses the authored Ground transform." });
                        if(alternate) ((JArray)worldModule.Payload["singleStaticColliders"])[0]["enabled"]=false;
                    }
                    foreach(var actor in characters.Characters) {
                        actor.StableId=document.LevelId+"."+(actor.ControlOwner==CharacterTestCharacterControlOwner.LocalPlayer?"player":"enemy.left");
                        actor.RuntimeAiPlannerProfilePath="";actor.BehaviorBinding=CharacterTestLevelBehaviorCompiler.Select(group);
                        foreach(var card in group.Cards.Where(c=>c.Enabled)) {
                            catalog.CardCatalog.TryGetExact(card.StableCardId,card.CardRevision,out var published);
                            foreach(string slot in CharacterTestLevelBehaviorCompiler.Slots(published.TacticalSource))
                                actor.BehaviorBinding.Actions.Add(new CharacterTestLevelActionBinding {EntryId=card.EntryId,SlotPath=slot,ActionId=455});
                        }
                    }
                    if(alternate) {
                        var enemy=characters.Characters.Single(a=>a.ControlOwner==CharacterTestCharacterControlOwner.AiControlled).Normalize();
                        enemy.StableId=document.LevelId+".enemy.right"; enemy.DisplayName="剑士 · 右侧";
                        enemy.SpawnPose.Position=new Vector3(2,0,2.5f);
                        foreach(var binding in enemy.BehaviorBinding.Actions)binding.ActionId=456;
                        characters.Characters.Add(enemy);
                    }
                    CharacterTestSceneCharacterAuthoringAdapter.UpsertCharactersModule(document,characters);
                    if(document.TryGetModule("characterTest.tacticalStage",out var stage)) {
                        stage.Payload["playerSpawnStableId"]=characters.Characters.Single(a=>a.ControlOwner==CharacterTestCharacterControlOwner.LocalPlayer).StableId;
                        stage.Payload["opponentSpawnStableId"]=characters.Characters.First(a=>a.ControlOwner==CharacterTestCharacterControlOwner.AiControlled).StableId;
                    }
                    using var prepared=builder.PrepareLevelSession(document,Source,publishPins:true,createViews:false);
                    string path=Folder+"/"+document.LevelId+".level.json";
                    CharacterTestLevelCatalog.AtomicWrite(path,MxLevelAuthoringJson.SaveToJson(prepared.Document));
                    CharacterTestLevelCatalog.Publish(prepared.Document,alternate?"单手剑 · 交叉阵位":"单手剑 · 庭院",CharacterTestLevelAuthoringOperations.RuntimeRoot);
                    directory.Levels.Add(new CharacterTestLevelEntry {LevelId=document.LevelId,DisplayName=alternate?"单手剑 · 交叉阵位":"单手剑 · 庭院",Path=path});
                    receipts.Add(new {document.LevelId,document.ScenePath,actors=prepared.Slice.Services.CharacterEntities.Count,
                        instanceBindings=characters.Characters.Select(a=>new{a.StableId,a.CharacterId,a.BehaviorBinding.GroupId,a.BehaviorBinding.GroupRevision,
                            actions=a.BehaviorBinding.Actions.Select(b=>b.ActionId).ToArray()}),runId=prepared.RunId});
                }
                if(File.Exists(CharacterTestLevelAuthoringOperations.DirectoryPath)) {
                    var existing=CharacterTestLevelAuthoringOperations.Catalog;
                    foreach(var entry in existing.Levels.Where(e=>!directory.Levels.Any(n=>n.LevelId==e.LevelId)))directory.Levels.Add(entry);
                }
                CharacterTestLevelCatalog.AtomicWrite(CharacterTestLevelAuthoringOperations.DirectoryPath,JsonConvert.SerializeObject(directory,Formatting.Indented));
                WriteReceipt("examples.json",new {status="PreparedAndPublished",receipts}); AssetDatabase.Refresh();
            } finally { Time.timeScale=scale; EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static object RunPlayAcceptance()
        {
            if(!EditorApplication.isPlaying || !EditorApplication.isPaused)throw new InvalidOperationException("Acceptance requires paused Play.");
            var manager=CharacterTestLevelAuthoringOperations.Manager;
            if(manager==null||manager.gameObject.scene.path!=ScenePath)throw new InvalidOperationException("Tactical Scene required.");
            var context=manager.RuntimeContext; var steps=new List<object>();
            manager.LoadLevel("formal.sword.courtyard"); var first=context.Slice;
            Check(manager.ActiveLevelId=="formal.sword.courtyard","first identity");
            Check(first.Services.CharacterEntities.Count==2,"first roster");
            var firstRun=first.Services.Modules.MixedPlayerTacticalStage.Stage.StageRunId;
            var oldCommands=first.Services.Modules.MixedPlayerTacticalStage.Commands;
            // Reproduce the legacy mixed boundary: live run is ahead of the builder counter.
            typeof(CharacterTestRuntimeSessionBuilder).GetField("_tacticalStageRunId",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(context.SessionBuilder,0UL);
            var originalNavigation=manager.NavigationSettings;
            var navigationField=typeof(GameManager).GetField("_navigationSettings",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            bool badNavigationRejected=false;
            try {
                navigationField.SetValue(manager,CharacterTestNavigationSceneSettings.CreateForTests(cellSizeMeters:0));
                var authored=CharacterTestLevelAuthoringOperations.Catalog.Load("formal.sword.courtyard",
                    CharacterTestLevelAuthoringOperations.ProjectRoot,false,out var authoredPath);
                try { using var invalid=context.SessionBuilder.PrepareLevelSession(authored,authoredPath,createViews:false); }
                catch(InvalidOperationException error) { badNavigationRejected=error.Message.Contains("InvalidCellSize"); }
            } finally { navigationField.SetValue(manager,originalNavigation); }
            Check(badNavigationRejected&&ReferenceEquals(context.Slice,first)&&!first.IsLevelDisposed,
                "bad enabled navigation preserves current session");
            steps.Add(new {step="invalid navigation retains A",badNavigationRejected,firstRun});
            manager.LoadLevel("formal.sword.crossfire");var second=context.Slice;
            Check(first.IsLevelDisposed,"previous session disposed");
            bool oldTickRejected=false;try{first.Tick(1.0/60.0);}catch(ObjectDisposedException){oldTickRejected=true;} Check(oldTickRejected,"retired host cannot produce callbacks");
            Check(first.CharacterRuntime.PhysicsRuntime.StaticColliderCount != second.CharacterRuntime.PhysicsRuntime.StaticColliderCount,"selected world module replaces geometry");
            Check(second.Services.CharacterEntities.Count==3,"second roster");
            Check(context.ActiveLevelWorldCache!=null&&context.ActiveLevelWorldCache.AuthoringContentHash.Length>0,
                "selected level cache owns world presentation");
            var left=second.CharacterRuntime.FindLevelActor("formal.sword.crossfire.enemy.left");
            var right=second.CharacterRuntime.FindLevelActor("formal.sword.crossfire.enemy.right");
            Check(left!=null&&right!=null&&left.Id.Value!=right.Id.Value,"same template distinct instances");
            Check(left.Identity.CharacterId==right.Identity.CharacterId,"same template fixture");
            Check(left.Control.RuntimeAiPlannerProfileSlot!=right.Control.RuntimeAiPlannerProfileSlot,"distinct profile slots");
            var requests=second.Services.Modules.RuntimeAiPlannerCommands.CaptureReplayOverlayInstallRequests();
            var leftRequest=requests.Single(r=>r.ProfileSlot==left.Control.RuntimeAiPlannerProfileSlot);
            var rightRequest=requests.Single(r=>r.ProfileSlot==right.Control.RuntimeAiPlannerProfileSlot);
            Check(leftRequest.LogicPrograms.Where(p=>p.TacticalSource!=null).SelectMany(p=>p.TacticalSource.ConfiguredActionIds).Contains(455),"left explicit action");
            Check(rightRequest.LogicPrograms.Where(p=>p.TacticalSource!=null).SelectMany(p=>p.TacticalSource.ConfiguredActionIds).Contains(456),"right explicit action");
            ulong secondRun=second.Services.Modules.MixedPlayerTacticalStage.Stage.StageRunId;Check(secondRun>firstRun,"new StageRunId");
            steps.Add(new {step="A→B",firstRun,secondRun,actors=3,left=left.Id.Value,right=right.Id.Value,slots=new[]{left.Control.RuntimeAiPlannerProfileSlot,right.Control.RuntimeAiPlannerProfileSlot},actions=new[]{455,456}});
            var directory=CharacterTestLevelCatalog.Read(Path.Combine(CharacterTestLevelAuthoringOperations.RuntimeRoot,"directory.json"));
            var bad=directory.Load("formal.sword.crossfire",CharacterTestLevelAuthoringOperations.RuntimeRoot,true,out var path);
            CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(bad,out var actors,out _);actors.Characters[0].BehaviorBinding.GroupContentHash++;
            CharacterTestSceneCharacterAuthoringAdapter.UpsertCharactersModule(bad,actors);
            bool rejected=false;try{using var candidate=context.SessionBuilder.PrepareLevelSession(bad,path);}catch(InvalidOperationException){rejected=true;}
            Check(rejected&&ReferenceEquals(context.Slice,second)&&!second.IsLevelDisposed,"invalid reference preserves old session");
            var commandOwnerBefore=context.Slice.Services.Modules.MixedPlayerTacticalStage.Commands;
            oldCommands.Drain();Check(ReferenceEquals(context.Slice.Services.Modules.MixedPlayerTacticalStage.Commands,commandOwnerBefore),"retired commands cannot rebind new owner");
            manager.LoadLevel("formal.sword.courtyard");var third=context.Slice;
            Check(second.IsLevelDisposed&&third.Services.CharacterEntities.Count==2,"B→A retires old roster");
            Check(third.CharacterRuntime.FindLevelActor("formal.sword.crossfire.enemy.right")==null,"old instance cannot leak");
            Check(third.Services.Modules.MixedPlayerTacticalStage.Stage.StageRunId>secondRun,"return StageRunId");
            steps.Add(new {step="B invalid → B retained → A",oldDisposed=second.IsLevelDisposed,actors=2,invalidRejected=rejected,
                finalRun=third.Services.Modules.MixedPlayerTacticalStage.Stage.StageRunId});
            var result=new {status="Passed",sameScene=ScenePath,steps,active=manager.ActiveLevelId};WriteReceipt("play-acceptance.json",result);return result;
        }
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("Level acceptance: "+message);}
        private static void WriteReceipt(string name,object value)
        {
            string root = string.IsNullOrWhiteSpace(ReceiptDirectory)
                ? Path.Combine(CharacterTestLevelAuthoringOperations.ProjectRoot, "Temp/MxFramework/Validation/Levels")
                : Path.GetFullPath(ReceiptDirectory);
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root,name),JsonConvert.SerializeObject(value,Formatting.Indented));
        }
    }
}
