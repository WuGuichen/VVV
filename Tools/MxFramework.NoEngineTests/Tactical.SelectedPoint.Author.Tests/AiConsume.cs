using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;

static class Program
{
    static int checks;
    static void Check(string name,bool yes){if(!yes)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Throws(string name,Action action,string expected){try{action();}catch(Exception e){Check(name,e.Message.Contains(expected));return;}throw new Exception(name);}
    static readonly GameplayEntityId Actor=new GameplayEntityId(1,1);
    static TacticalPointMoveAuthoring Rules()=>new TacticalPointMoveAuthoring{RangeMillimeters=5000,ArrivalMillimeters=200,MaximumFrames=180,SpeedMilli=1000,RequireReachable=true};
    static void Main()
    {
        var noProvider=new Fixture(null); Check("AI owner rejects absent Provider without reservation",!noProvider.Owner.CanBind(Actor,noProvider.Definition,out var issue)&&issue=="execution-input:UnsupportedProvider"&&noProvider.Stage.Ledger.Reserved==0);
        var brokenCapability=new Fixture(new Provider{ThrowCapability=true});
        Check("AI capability failure returns typed rejection before reservation",!brokenCapability.Owner.CanBind(Actor,brokenCapability.Definition,out issue)&&issue=="execution-input:PointUnavailable"&&brokenCapability.Stage.Ledger.Reserved==0);
        Check("AI selected consumer rejects absent binding",!CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.TryBeginAutomaticSelection(null,Actor,0,out var id,out issue)&&id==0&&issue=="execution-input:UnsupportedProvider");
        var provider=new Provider();var f=new Fixture(provider,0);
        Check("AI binding uses original Stage collaborators",f.Owner.CanBind(Actor,f.Definition,out issue));
        Check("AI binding rejects recycled actor generation",!f.Owner.CanBind(new GameplayEntityId(1,2),f.Definition,out _));
        var lookalike=TacticalActionCardDefinition.CreatePublishedPointMove("point","point","",4,Rules().Freeze(),0,"fixture.point",1,"hash",1,"");
        Check("AI owner rejects a lookalike definition outside its deck",!f.Owner.CanBind(Actor,lookalike,out _));
        Check("deferred contract has exact published identity and no selected position",f.Marker.TacticalSource.WorkspaceId=="published:fixture.point"&&f.Marker.TacticalSource.CardRevision==1&&f.Marker.TacticalSource.CloneLegacySource().ExecutionInput!=null&&f.Marker.TacticalSource.CloneLegacySource().Timelines.All(t=>t.Events.Count==0));
        Check("marker cannot run through normal frozen binding",!f.Marker.TacticalSource.ValidateBinding(null,null,out issue)&&issue=="execution-input:PointRequired");
        Check("normal published compile still requires this play's point",!f.Published.TryCompile("fixture.point","point",1,CharacterTestTacticalCardInputs.Unbound(1),out _,out issue)&&issue=="execution-input:PointRequired");
        Check("deferred binding verifies author rules",f.Binding.Matches(f.Marker.TacticalSource));
        var wrongRevision=Source();wrongRevision.CurrentRevision=2; CharacterTestTacticalAuthoringCompiler.TryCompile(wrongRevision.CloneTacticalSource(),CharacterTestTacticalCardInputs.Unbound(1),out var c,out _);
        var frozen=CharacterTestTacticalFrozenSource.CaptureLegacy(null,wrongRevision,CharacterTestTacticalCardInputs.Unbound(1),c);
        Check("deferred binding rejects another revision",!f.Binding.Matches(frozen));
        Check("stale execution frame does not acquire point or reserve",!f.Owner.TryBegin(Actor,f.Definition,f.Frame+1,out _,out issue)&&issue=="execution:StaleFrame"&&provider.Calls==0&&f.Stage.Ledger.Reserved==0);
        // Exercise the actual production Overlay selection and lifecycle with controlled execution receipts.
        var overlay=f.Overlay();var facts=new CharacterTestRuntimeAiPlannerFactState();
        Check("production arbitration delegates to original owner and emits no duplicate intent",overlay.TryTick(f.Entity,facts,new RuntimeFrame(f.Frame),out var intent)&&intent.Kind==CharacterTestRuntimeAiLabRuntimeIntentKind.None&&provider.Calls==1&&f.Port.Admissions==1);
        Check("production arbitration transfers only original reservation",f.Stage.Ledger.Current==50&&f.Stage.Ledger.Reserved==4&&f.Stage.Deck.PendingAction==null&&f.Stage.Deck.ResolvingAction!=null);
        Check("production AI reports owned execution",overlay.HasActiveExecution&&overlay.OwnsActiveCardExecution(1));
        Check("borrowed owner snapshot is explicitly refused",!overlay.TryApplyStateSnapshot(null,out issue)&&issue=="execution:AutomaticOwnerSnapshotUnsupported"&&overlay.HasActiveExecution);
        Throws("active Overlay capture cannot silently omit Ledger owner",()=>overlay.CreateStateSnapshot(1),"execution:AutomaticOwnerSnapshotUnsupported");
        overlay.TryTick(f.Entity,facts,new RuntimeFrame(f.Frame),out _);
        Check("active Overlay does not start a second admission",provider.Calls==1&&f.Port.Admissions==1&&f.Stage.Ledger.Reserved==4);
        var first=f.Port.Admitted; f.Port.Started=true; f.Tick();
        Check("production AI first acceptance charges original reservation",f.Stage.Ledger.Current==46&&f.Stage.Ledger.Reserved==0);
        f.Port.Terminal=true; f.Port.Released=false;f.Tick();overlay.TryTick(f.Entity,facts,new RuntimeFrame(f.Frame),out _);
        Check("AI does not reselect before original control release",overlay.HasActiveExecution&&provider.Calls==1);
        f.Port.Released=true;f.Finish();overlay.TryTick(f.Entity,facts,new RuntimeFrame(f.Frame),out _);
        Check("AI terminal follows original settle and refills original Deck",!overlay.HasActiveExecution&&overlay.HasTerminalCommit&&f.Stage.Phase==TacticalStagePhase.Planning&&f.Stage.Deck.Hand.Count==1&&f.Stage.Deck.ResolvingAction==null);
        provider.Point=new TacticalWorldPoint(2500,0,0);f.Port.Origin=new TacticalWorldPoint(500,0,0);f.Port.Started=f.Port.Terminal=false;
        overlay.TryTick(f.Entity,facts,new RuntimeFrame(f.Frame),out _);
        Check("next production selection reacquires and freezes a new input",provider.Calls==2&&f.Port.Admissions==2&&f.Port.Admitted.Point.Equals(provider.Point)&&f.Port.Admitted.SelectionOrigin.Equals(f.Port.Origin)&&!f.Port.Admitted.Binding.Equals(first.Binding));
        Check("old execution observation cannot pay or finish new execution",f.Owner.Observe(Actor,1).ExecutionId==0&&f.Owner.Observe(new GameplayEntityId(1,2),2).ExecutionId==0);
        var failures=new[]{TacticalInputStatus.PointUnavailable,TacticalInputStatus.OutOfRange,TacticalInputStatus.Unreachable};
        foreach(var status in failures)
        {
            var p=new Provider();var x=new Fixture(p);if(status==TacticalInputStatus.PointUnavailable)p.Status=status;else x.Port.Validation=status;
            var o=x.Overlay();Check("production AI returns failure "+status,o.TryTick(x.Entity,new CharacterTestRuntimeAiPlannerFactState(),new RuntimeFrame(x.Frame),out var result)&&result.Kind==CharacterTestRuntimeAiLabRuntimeIntentKind.None&&o.LastAutomaticExecutionIssue=="execution-input:"+status);
            Check("failed AI admission releases only original selection "+status,x.Stage.Ledger.Current==50&&x.Stage.Ledger.Reserved==0&&x.Stage.Deck.PendingAction==null&&x.Stage.Deck.Hand.Count==1&&x.Port.Admissions==0&&!o.HasActiveExecution);
        }
        var rejected=new Fixture(new Provider());rejected.Port.RejectAdmit=true;
        Check("prepare/admit failure rolls back automatic reservation",!rejected.Owner.TryBegin(Actor,rejected.Definition,rejected.Frame,out _,out issue)&&issue=="fixture:admit"&&rejected.Stage.Ledger.Reserved==0&&rejected.Stage.Deck.Hand.Count==1);
        foreach(bool start in new[]{false,true})
        {
            var x=new Fixture(new Provider());var o=x.Overlay();o.TryTick(x.Entity,new CharacterTestRuntimeAiPlannerFactState(),new RuntimeFrame(x.Frame),out _);
            x.Port.Started=start;x.Port.Terminal=true;x.Port.Issue="execution:Point:TimedOut";x.Finish();o.TryTick(x.Entity,new CharacterTestRuntimeAiPlannerFactState(),new RuntimeFrame(x.Frame),out _);
            Check("AI timeout reconciles original fee "+start,x.Stage.Ledger.Current==(start?46:50)&&x.Stage.Ledger.Reserved==0);
            Check("AI timeout preserves original cooldown contract "+start,x.Stage.Deck.CooldownTracker.IsOnCooldown(1,x.Definition,x.Stage.LastCompletedFrame)==start);
            Check("AI timeout returns real terminal issue "+start,!o.HasActiveExecution&&o.LastAutomaticExecutionIssue=="execution:Point:TimedOut"&&o.LastExecutionEndReason==CharacterTestBehaviorExecutionEndReason.TimedOut);
            if(start)
            {
                o.TryTick(x.Entity,new CharacterTestRuntimeAiPlannerFactState(),new RuntimeFrame(x.Frame),out _);
                Check("AI cooldown rejection does not reacquire or reserve",!x.Owner.TryBegin(Actor,x.Definition,x.Frame,out _,out issue)&&issue=="execution:CardOnCooldown"&&x.Port.Admissions==1&&x.Stage.Ledger.Reserved==0&&x.Stage.Ledger.Current==46);
            }
        }
        foreach(bool start in new[]{false,true})
        {
            var x=new Fixture(new Provider());var o=x.Overlay();o.TryTick(x.Entity,new CharacterTestRuntimeAiPlannerFactState(),new RuntimeFrame(x.Frame),out _);
            x.Port.Started=start;x.Stage.ReleaseControl();x.Port.Terminal=true;x.Port.Issue="execution:Point:Interrupted";
            o.TryTick(x.Entity,new CharacterTestRuntimeAiPlannerFactState(),new RuntimeFrame(x.Frame),out _);
            Check("released AI Stage reconciles accepted move before teardown "+start,x.Stage.Ledger.Current==(start?46:50)&&x.Stage.Ledger.Reserved==0&&x.Stage.Deck.CooldownTracker.IsOnCooldown(1,x.Definition,0)==start);
            Check("AI observes released owner's interrupt without fabricating new execution "+start,!o.HasActiveExecution&&o.LastAutomaticExecutionIssue=="execution:Point:Interrupted"&&x.Port.Admissions==1);
            Check("released owner cannot be rebound "+start,!x.Owner.CanBind(Actor,x.Definition,out _));
        }
        var manual=new Fixture(new Provider());var hand=manual.Stage.Deck.Hand[0];manual.Stage.Deck.SetPendingAction(hand,manual.Stage.Ledger);
        Check("AI does not steal interactive pending reservation",!manual.Owner.TryBegin(Actor,manual.Definition,manual.Frame,out _,out issue)&&issue=="execution:AutomaticIngressBusy"&&manual.Stage.Deck.PendingAction==hand&&manual.Stage.Ledger.Reserved==4);
        var queued=new Fixture(new Provider());var qcard=queued.Stage.Deck.Hand[0];var q=new TacticalStageCommand(queued.Stage.StageRunId,queued.Stage.TurnIndex,qcard.InstanceId,qcard.Version,queued.Stage.SnapshotRevision,TacticalStageCommandKind.SelectAction);
        queued.Commands.TrySubmit(q,1,1);
        Check("AI cannot drain another control source",!queued.Owner.TryBegin(Actor,queued.Definition,queued.Frame,out _,out issue)&&queued.Commands.PendingCount==1&&queued.Stage.Ledger.Reserved==0);
        Throws("AI source slot validated before command use",()=>new TacticalStageAutomaticCardExecutionPort(queued.Stage,queued.Commands,99),"source");
        var map=new Dictionary<string,TacticalAutomaticExecutionBinding>{{"point#1",f.Binding}};
        var request=new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(1,null,new[]{f.Marker},123,map);map.Clear();
        Check("installation freezes binding map against caller mutation",request.AutomaticExecutions.Count==1);
        Throws("replay export does not serialize a borrowed Stage owner",()=>request.ValidateReplayExport(),"execution:AutomaticOwnerSnapshotUnsupported");
        new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(1,null,null,123).ValidateReplayExport();
        Check("ordinary installation remains replay exportable",true);
        var beforeControl=f.Entity.Control;var beforeTransform=f.Entity.Transform;
        var participant=new TacticalDeploymentParticipant("fixture-ai",1,"fixture","fixture",2,false,
            new UnityEngine.Vector3(7,0,9),45,null,null);
        var placement=new TacticalAutoBattleRuntimeInstaller.ActorPlacement(f.Entity,3,participant,true);
        placement.Apply();
        Check("installation applies validated actor placement",f.Entity.Control.RuntimeAiPlannerProfileSlot==3&&f.Entity.Transform.X==7&&f.Entity.Transform.Z==9);
        placement.Restore();
        Check("failed installation restores actor control and position",f.Entity.Control.RuntimeAiPlannerProfileSlot==beforeControl.RuntimeAiPlannerProfileSlot&&f.Entity.Transform.X==beforeTransform.X&&f.Entity.Transform.Z==beforeTransform.Z&&f.Entity.Transform.YawDegrees==beforeTransform.YawDegrees);
        Console.WriteLine("PASS TOTAL "+checks+"; actual Overlay arbitration and original Stage/CommandPump/Deck/Ledger/Driver with controlled point/execution ports; no Editor/physics evidence");
    }
    static CharacterTestRuntimeAiLabBehaviorCardDraft Source()
    {
        var d=new CharacterTestRuntimeAiLabBehaviorCardDraft("fixture.point","fixture.point","point","Tactical",null);d.CurrentRevision=1;
        d.SetTacticalSource(CharacterTestTacticalAuthoringTemplates.CreatePointMove("point",Rules()));return d;
    }
    sealed class Fixture
    {
        static ulong run;
        public readonly Execution Port=new Execution();public readonly TacticalActionCardDefinition Definition;
        public readonly TacticalStageFlowCoordinator Stage;public readonly TacticalCommandPump Commands;public readonly TacticalStageAutomaticCardExecutionPort Owner;
        public readonly TacticalAutomaticExecutionBinding Binding;public readonly CharacterTestTacticalPublishedSource Published;
        public readonly CharacterTestRuntimeAiLabCompiledLogicProgram Marker;public readonly CharacterTestCharacterEntity Entity;
        public long Frame=>Stage.LastCompletedFrame+1;
        public Fixture(Provider provider,float cooldown=1.25f)
        {
            Definition=TacticalActionCardDefinition.CreatePublishedPointMove("point","point","",4,Rules().Freeze(),cooldown,"fixture.point",1,"hash",1,"");
            var time=new TacticalTimeCoordinator(new Time());var deck=new TacticalDeckManager(3);var ledger=new MomentumLedger();deck.BindCooldownTracker(new TacticalCooldownTracker(),1);
            var driver=new TacticalSingleCardDriver(Port,time,deck,ledger);Stage=new TacticalStageFlowCoordinator(++run,time,deck,ledger,driver,new[]{Definition},42,_=>true,new TacticalStoryStepBinding(1,1,1),pointProvider:provider);
            Commands=new TacticalCommandPump(Stage);var token=new TacticalStoryStepToken(Stage.StageRunId,1,1,1,1);Stage.OnStoryStepStarted(token);Stage.OnSpawnBound(Stage.StageRunId,0,Actor,0);Stage.OnSpawnBound(Stage.StageRunId,1,new GameplayEntityId(2,1),0);if(!Stage.TryInitializeMixedRosterPlanning())throw new Exception("init");
            Owner=new TacticalStageAutomaticCardExecutionPort(Stage,Commands);Binding=new TacticalAutomaticExecutionBinding(Definition,Owner);
            Published=CharacterTestTacticalPublishedSource.CaptureDraft(null,Source());if(!Published.TryCompileAutomaticContract("fixture.point","point",1,1,out var marker,out var issue))throw new Exception(issue);
            Marker=marker.WithSelectionDefinition(new CharacterTestBehaviorCardSelectionDefinition("fixture.point",10));
            Entity=new CharacterTestCharacterEntity(new CharacterTestCharacterEntityId(1),new CharacterTestCharacterIdentityComponent(1,"fixture",default,default),new CharacterTestCharacterTeamComponent(1,1),default,default,new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.AiControlled,1),Actor);
        }
        public CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay Overlay()
        {
            var profile=new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("fixture");var entry=new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,true,0){EntryId="point#1",NumericId=1,DefinitionNumericId=4};profile.Entries.Add(entry);
            var runtime=(CharacterTestCharacterRuntimeModule)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(CharacterTestCharacterRuntimeModule));
            return new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(profile,new[]{Marker},null,null,123,1,entities:new[]{Entity},characterRuntime:runtime,automaticExecutions:new Dictionary<string,TacticalAutomaticExecutionBinding>{{entry.EntryId,Binding}});
        }
        public void Tick(){long frame=Frame;var sample=new SettleSample(frame,false,false,true,false,false,false,false,false,true,false,0,0);Stage.OnCompletedSimulationTick(frame,sample,sample);}
        public void Finish(){for(int i=0;i<12&&Stage.Phase!=TacticalStagePhase.Planning;i++)Tick();if(Stage.Phase!=TacticalStagePhase.Planning)throw new Exception("settle");}
    }
    sealed class Provider:ITacticalPointProvider
    {
        public bool ThrowCapability;public TacticalPointProviderCapability Capability=>ThrowCapability?throw new InvalidOperationException("fixture:capability"):TacticalPointProviderCapability.WorldPoint; public int Calls;
        public TacticalInputStatus Status=TacticalInputStatus.Valid;public TacticalWorldPoint Point=new TacticalWorldPoint(2000,0,0);
        public TacticalInputStatus TryGetPoint(in TacticalPointProviderRequest request,out TacticalWorldPoint point){Calls++;point=Point;return Status;}
    }
    sealed class Time:ITacticalTimeAdapter{public bool IsSimulationPaused{get;private set;}public void SetSimulationPaused(bool p)=>IsSimulationPaused=p;}
    sealed class Execution:ITacticalExecutionInputPort,ITacticalPointSelectionPort
    {
        public int Admissions;public bool RejectAdmit,Started,Terminal,Released=true;public string Issue="";
        public TacticalInputStatus Validation=TacticalInputStatus.Valid;public TacticalWorldPoint Origin;public TacticalExecutionInput Admitted;
        public TacticalExecutionInputKind GetRequiredInputKind(TacticalCardInstance card)=>TacticalExecutionInputKind.WorldPoint;
        public TacticalInputStatus ValidateInput(TacticalCardInstance card,in TacticalExecutionInput input)=>Validation;
        public bool TryPrepare(TacticalCardInstance c,in TacticalExecutionInput input,out string issue){issue="";return true;}
        public bool TryAdmit(TacticalCardInstance c,in TacticalExecutionInput input,out ulong id,out string issue){id=0;issue="fixture:admit";if(RejectAdmit)return false;id=(ulong)++Admissions;Admitted=input;return true;}
        public bool TryPrepare(TacticalCardInstance c,out string issue){issue="input required";return false;}
        public bool TryAdmit(TacticalCardInstance c,out ulong id,out string issue){id=0;issue="input required";return false;}
        public bool TryAbortPreAction(ulong id,string reason)=>true;
        public ExecutionObservation Observe(ulong id)=>new ExecutionObservation(id,false,false,Terminal,Released,true,Started,Issue,0);
        public bool TryGetPointOrigin(out TacticalWorldPoint o){o=Origin;return true;}
        public TacticalInputStatus TryPickGround(TacticalWorldPoint o,TacticalWorldPoint d,out TacticalWorldPoint p){p=default;return TacticalInputStatus.GroundUnavailable;}
    }
}
