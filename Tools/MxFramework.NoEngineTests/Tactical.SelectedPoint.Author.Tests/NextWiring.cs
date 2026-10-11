using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;

static class Program
{
    static int count;
    static void Check(string name, bool success) { if (!success) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    static TacticalPointMoveAuthoring Rules() => new TacticalPointMoveAuthoring { RangeMillimeters=5000, ArrivalMillimeters=200, MaximumFrames=180, SpeedMilli=1000, RequireReachable=true };
    static void Main(string[] args)
    {
        var source=CharacterTestTacticalAuthoringTemplates.CreatePointMove("isolated point", Rules());
        var preview=CharacterTestTacticalAuthoringPreview.FromSource(source,null,"no equipment");
        Check("point Inspector preview validates contract without acquiring execution point", preview.Ready && preview.Source.ExecutionInput != null && preview.StatusText.Contains("每次执行"));
        var workspace=CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
        Check("import isolated author draft",workspace.TryImportTacticalSource(1,0,workspace.CurrentRevision,source,out var id,out _));
        var stable=workspace.Cards[id].PublishedStableId;
        var session=new CharacterTestTacticalEditingSession(null,id); session.Bind(workspace,workspace.Cards[id],preview); preview.Editor=session;
        var inspector=BehaviorRecipeTriggerEventInspectorProjection.BuildActivationOverview(preview,null);
        Check("point projection routes actual author Inspector before event-set branch", CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder.RequiresPointMoveForm(inspector));
        Check("missing preparation remains author contract preview",CharacterTestTacticalAuthoringPreview.FromPrepared(source,null,null,"no equipment").Ready);
        foreach(var pair in new[] { ("range","6000"), ("arrival","300"), ("frames","240"), ("speed","750"), ("cost","4"), ("cooldown","1.25") })
        {
            var card=workspace.Cards[id]; var original=card.TacticalSourceJson;
            var token=new CharacterTestTacticalEditToken(workspace,card);
            Check("Inspector parse "+pair.Item1,CharacterTestTacticalPointAuthoringFields.TryEdit(card.CloneTacticalSource(),card.Cost,card.CooldownSeconds,pair.Item1,pair.Item2,out var next,out var cost,out var cooldown,out _));
            Check("parse never mutates author snapshot "+pair.Item1,card.TacticalSourceJson==original);
            Check("Inspector atomic transaction "+pair.Item1, workspace.TryEditPointMove(token,next,cost,cooldown,out _) && card.PublishedStableId==stable);
            Check("stale field lease rejected "+pair.Item1,!workspace.TryEditPointMove(token,next,0,0,out _));
        }
        var d=workspace.Cards[id];
        Check("all fields survive later metadata edit",d.CloneTacticalSource().ExecutionInput.SpeedMilli==750 && d.Cost==4 && d.CooldownSeconds==1.25f);
        Check("undo field through original history",workspace.TryUndo(out _) && workspace.Cards[id].CooldownSeconds==0 && workspace.Cards[id].Cost==4);
        Check("redo field through original history",workspace.TryRedo(out _) && workspace.Cards[id].CooldownSeconds==1.25f && workspace.Cards[id].PublishedStableId==stable);
        foreach(var pair in new[] { ("range","1"), ("arrival","9000"), ("frames","1.5"), ("speed","99"), ("cost","11"), ("cooldown","NaN"), ("cooldown","Infinity"), ("cooldown","1,25"), ("unknown","3") })
            Check("Inspector rejects "+pair.Item1+"="+pair.Item2,!CharacterTestTacticalPointAuthoringFields.TryEdit(source,0,0,pair.Item1,pair.Item2,out _,out _,out _,out _));
        var previous=CultureInfo.CurrentCulture; CultureInfo.CurrentCulture=new CultureInfo("fr-FR");
        try { Check("Inspector cooldown uses invariant units", CharacterTestTacticalPointAuthoringFields.TryEdit(source,0,0,"cooldown","1.25",out _,out _,out var cd,out _) && cd==1.25f); }
        finally { CultureInfo.CurrentCulture=previous; }
        // Ensure every reused control is in the existing Composer-owned Inspector contract.
        string root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "../../../../../../.."));
        if(args.Length>1) root=args[1];
        var xml=XDocument.Load(Path.Combine(root,"FGUIProject/assets/MxCharacterTestRuntimeAiLabUi/Components/RuntimeAiLabBehaviorRecipeInspector.xml"));
        foreach(var name in new[]{"inputLocalFrame","inputDurationFrames","inputMaxExecutions","inputPriority","inputOperationParameter","inputCooldown","timeLabel","durationFramesLabel","repeatLabel","priorityLabel","operationParameterLabel","operationSecondaryParameterLabel","btnOutcomeReturn","btnDeleteEvent","btnLocateTarget"})
            Check("published Inspector control "+name,xml.Descendants().Any(e=>(string)e.Attribute("name")==name));
        Check("mixed player forwards typed input",typeof(ITacticalExecutionInputPort).IsAssignableFrom(typeof(TacticalMixedPlayerCardRuntime)));
        Check("mixed player forwards point picking",typeof(ITacticalPointSelectionPort).IsAssignableFrom(typeof(TacticalMixedPlayerCardRuntime)));
        var unbound=(TacticalMixedPlayerCardRuntime)RuntimeHelpers.GetUninitializedObject(typeof(TacticalMixedPlayerCardRuntime));
        Check("unbound mixed player rejects typed prepare",!unbound.TryPrepare(null,default,out var issue) && issue=="execution:ActorNotBound");
        Check("unbound mixed player rejects typed admit",!unbound.TryAdmit(null,default,out var exec,out issue) && exec==0);
        Check("unbound mixed player cannot invent ground",unbound.TryPickGround(default,default,out _)==TacticalInputStatus.GroundUnavailable && !unbound.TryGetPointOrigin(out _));
        Check("fixed-program AI installer explicitly refuses an unprovided per-play Provider",
            TacticalAutoBattleRuntimeInstaller.AutomaticInputIssue(new Fixture(null).Definition)=="execution-input:UnsupportedProvider");
        Check("fixed-program AI installer leaves ordinary cards on their existing path",TacticalAutoBattleRuntimeInstaller.AutomaticInputIssue(new TacticalActionCardDefinition("ordinary","ordinary","",1,new MxFramework.CharacterAction.CharacterActionId(1)))==string.Empty);
        Automatic(null,"null Provider rejects cached manual point",TacticalStageRejection.InvalidExecutionInput,"execution-input:UnsupportedProvider");
        var unsupported=new Provider{Capability=TacticalPointProviderCapability.Unsupported};
        Automatic(unsupported,"unsupported Provider rejects",TacticalStageRejection.InvalidExecutionInput,"execution-input:UnsupportedProvider");
        Check("unsupported capability not invoked",unsupported.Calls==0);
        var unavailable=new Provider{Status=TacticalInputStatus.PointUnavailable};
        Automatic(unavailable,"unavailable Provider rejects cached point",TacticalStageRejection.InvalidExecutionInput,"execution-input:PointUnavailable");
        var throwing=new Provider{Throw=true};
        Automatic(throwing,"Provider exception returns typed rejection",TacticalStageRejection.InvalidExecutionInput,"execution-input:PointUnavailable");
        var valid=new Provider{Point=new TacticalWorldPoint(2000,0,0)};
        var f=new Fixture(valid); f.Select();
        f.Port.RejectAdmit=true;
        Check("automatic preparation failure preserves original reservation",f.Send(TacticalStageCommandKind.ConfirmAutomaticExecution)==TacticalStageRejection.ExecutionRejected && f.Stage.Ledger.Reserved==4 && f.Stage.Ledger.Current==50 && valid.Calls==1);
        valid.Point=new TacticalWorldPoint(3000,0,0); f.Port.Origin=new TacticalWorldPoint(500,0,0); f.Port.RejectAdmit=false;
        Check("retry asks Provider again and freezes new authority",f.Send(TacticalStageCommandKind.ConfirmAutomaticExecution)==TacticalStageRejection.None && valid.Calls==2 && f.Port.Admitted.Point.Equals(valid.Point) && f.Port.Admitted.SelectionOrigin.Equals(f.Port.Origin) && f.Port.Admitted.Source==TacticalExecutionInputSource.Automatic);
        Check("automatic confirmation reuses deferred original reservation",f.Stage.Deck.PendingAction==null && f.Stage.Deck.ResolvingAction!=null && f.Stage.Ledger.Reserved==4 && f.Stage.Ledger.Current==50);
        Check("executing phase cannot call Provider or reserve again",f.Send(TacticalStageCommandKind.ConfirmAutomaticExecution)==TacticalStageRejection.WrongPhase && valid.Calls==2 && f.Stage.Ledger.Reserved==4);
        f.Port.Started=true;
        var sample=new SettleSample(1,false,false,true,false,false,false,false,false,true,false,0,0);
        f.Stage.OnCompletedSimulationTick(1,sample,sample);
        Check("automatic start settles through original Ledger and Cooldown",f.Stage.Ledger.Current==46 && f.Stage.Ledger.Reserved==0 && f.Stage.Deck.CooldownTracker.IsOnCooldown(1,f.Definition,1));
        var fresh=new Fixture(valid); fresh.Select();
        Check("next session execution reacquires point",fresh.Send(TacticalStageCommandKind.ConfirmAutomaticExecution)==TacticalStageRejection.None && valid.Calls==3 && !fresh.Port.Admitted.Binding.Equals(f.Port.Admitted.Binding));
        var stale=new Fixture(valid); stale.Select(); var old=stale.Command(TacticalStageCommandKind.ConfirmAutomaticExecution);
        stale.Send(TacticalStageCommandKind.SetExecutionInput);
        Check("stale automatic command rejected before Provider",stale.Stage.Apply(old)==TacticalStageRejection.StaleSnapshot && valid.Calls==3);
        var reentrant=new Provider(); var rf=new Fixture(reentrant); rf.Select();
        reentrant.Callback=()=>Check("Provider cannot reenter stage mutation",rf.Stage.Apply(rf.Command(TacticalStageCommandKind.CancelPending))==TacticalStageRejection.InvalidCommand);
        Check("outer automatic command retains sole reservation",rf.Send(TacticalStageCommandKind.ConfirmAutomaticExecution)==TacticalStageRejection.None && rf.Stage.Ledger.Reserved==4);
        Console.WriteLine("PASS TOTAL "+count+"; controlled Provider/execution ports; no Editor or Unity scene evidence");
    }
    static void Automatic(Provider provider,string name,TacticalStageRejection expected,string issue)
    {
        var f=new Fixture(provider); f.Select(); f.Send(TacticalStageCommandKind.SetExecutionInput);
        Check(name,f.Send(TacticalStageCommandKind.ConfirmAutomaticExecution)==expected && f.Stage.ExecutionIssue==issue && f.Port.Admissions==0);
        Check(name+" clears prior point but retains cancellable original reservation",f.Stage.Deck.PendingExecutionInput.Kind==TacticalExecutionInputKind.None && f.Stage.Ledger.Current==50 && f.Stage.Ledger.Reserved==4 && f.Send(TacticalStageCommandKind.CancelPending)==TacticalStageRejection.None && f.Stage.Ledger.Reserved==0);
    }
    sealed class Fixture
    {
        static ulong run;
        public readonly Port Port=new Port();
        public readonly TacticalActionCardDefinition Definition=TacticalActionCardDefinition.CreatePointMove("fixture","fixture","",4,Rules().Freeze(),1.25f,CardDestination.Discard);
        public readonly TacticalStageFlowCoordinator Stage;
        readonly TacticalCommandPump pump; ulong sequence;
        public Fixture(Provider provider)
        {
            var time=new TacticalTimeCoordinator(new Time()); var deck=new TacticalDeckManager(3); var ledger=new MomentumLedger();
            deck.BindCooldownTracker(new TacticalCooldownTracker(),1);
            var driver=new TacticalSingleCardDriver(Port,time,deck,ledger);
            Stage=new TacticalStageFlowCoordinator(++run,time,deck,ledger,driver,new[]{Definition},42,_=>true,new TacticalStoryStepBinding(1,1,1),pointProvider:provider);
            pump=new TacticalCommandPump(Stage);
            var token=new TacticalStoryStepToken(Stage.StageRunId,1,1,1,1); Stage.OnStoryStepStarted(token);
            Stage.OnSpawnBound(Stage.StageRunId,0,new GameplayEntityId(1,1),0); Stage.OnSpawnBound(Stage.StageRunId,1,new GameplayEntityId(2,1),0);
            Check("cold generic automatic stage initializes original collaborators",Stage.TryInitializeMixedRosterPlanning());
        }
        public TacticalStageCommand Command(TacticalStageCommandKind kind)
        {
            var card=Stage.Deck.PendingAction??Stage.Deck.ResolvingAction??Stage.Deck.Hand[0];
            var binding=new TacticalExecutionInputBinding(Stage.StageRunId,Stage.TurnIndex,card.InstanceId,card.Version,new GameplayEntityId(1,1));
            var input=new TacticalExecutionInput(binding,TacticalExecutionInputSource.Manual,new TacticalWorldPoint(1000,0,0),Port.Origin);
            return new TacticalStageCommand(Stage.StageRunId,Stage.TurnIndex,card.InstanceId,card.Version,Stage.SnapshotRevision,kind,input);
        }
        public TacticalStageRejection Send(TacticalStageCommandKind kind)
        {
            var command=Command(kind); var queued=pump.TrySubmit(command,1,++sequence); if(queued!=TacticalStageRejection.None) return queued;
            pump.Drain(); if(!pump.TryReadReceipt(out var receipt)) throw new Exception("receipt missing"); return receipt.Rejection;
        }
        public void Select()=>Check("generic selection uses original Deck reservation",Send(TacticalStageCommandKind.SelectAction)==TacticalStageRejection.None && Stage.Ledger.Reserved==4);
    }
    sealed class Provider:ITacticalPointProvider
    {
        public TacticalPointProviderCapability Capability{get;set;}=TacticalPointProviderCapability.WorldPoint;
        public TacticalInputStatus Status=TacticalInputStatus.Valid; public TacticalWorldPoint Point=new TacticalWorldPoint(2000,0,0);
        public int Calls; public bool Throw; public Action Callback;
        public TacticalInputStatus TryGetPoint(in TacticalPointProviderRequest request,out TacticalWorldPoint point) { Calls++; Callback?.Invoke(); if(Throw)throw new InvalidOperationException("fixture"); point=Point; return Status; }
    }
    sealed class Time:ITacticalTimeAdapter {public bool IsSimulationPaused{get;private set;} public void SetSimulationPaused(bool value)=>IsSimulationPaused=value;}
    sealed class Port:ITacticalExecutionInputPort,ITacticalPointSelectionPort
    {
        public int Admissions; public bool RejectAdmit,Started; public TacticalWorldPoint Origin; public TacticalExecutionInput Admitted;
        public TacticalExecutionInputKind GetRequiredInputKind(TacticalCardInstance card)=>TacticalExecutionInputKind.WorldPoint;
        public TacticalInputStatus ValidateInput(TacticalCardInstance card,in TacticalExecutionInput input)=>input.Kind==TacticalExecutionInputKind.WorldPoint?TacticalInputStatus.Valid:TacticalInputStatus.MissingInput;
        public bool TryPrepare(TacticalCardInstance card,in TacticalExecutionInput input,out string issue){issue="";return true;}
        public bool TryAdmit(TacticalCardInstance card,in TacticalExecutionInput input,out ulong executionId,out string issue){executionId=0;issue="fixture:admit";if(RejectAdmit)return false;Admissions++;Admitted=input;executionId=1;return true;}
        public bool TryPrepare(TacticalCardInstance card,out string issue){issue="input required";return false;}
        public bool TryAdmit(TacticalCardInstance card,out ulong executionId,out string issue){executionId=0;issue="input required";return false;}
        public bool TryAbortPreAction(ulong id,string reason)=>true;
        public ExecutionObservation Observe(ulong id)=>new ExecutionObservation(id,false,false,false,false,false,Started,executionStartedTick:1);
        public bool TryGetPointOrigin(out TacticalWorldPoint origin){origin=Origin;return true;}
        public TacticalInputStatus TryPickGround(TacticalWorldPoint origin,TacticalWorldPoint direction,out TacticalWorldPoint point){point=default;return TacticalInputStatus.GroundUnavailable;}
    }
}
