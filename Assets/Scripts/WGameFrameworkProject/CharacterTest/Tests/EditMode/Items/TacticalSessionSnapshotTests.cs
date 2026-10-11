using System;
using System.Linq;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    public sealed class TacticalSessionSnapshotTests
    {
        private sealed class Execution : ITacticalExecutionPort
        {
            public bool TryPrepare(TacticalCardInstance card,out string issue){issue="";return true;}
            public bool TryAdmit(TacticalCardInstance card,out ulong id,out string issue){id=1;issue="";return true;}
            public ExecutionObservation Observe(ulong id)=>new ExecutionObservation(id,false,false,false,true,true);
            public bool TryAbortPreAction(ulong id,string reason)=>true;
        }
        private sealed class ForeignSettle : ISettleDetector {public bool Sample(in SettleSample sample)=>false;public void Reset(){} }
        private sealed class ForeignGate : ITacticalTimeAdapter {public bool IsSimulationPaused {get;private set;}public void SetSimulationPaused(bool paused)=>IsSimulationPaused=paused;}
        private sealed class PointProvider : ITacticalPointProvider
        {public TacticalPointProviderCapability Capability=>TacticalPointProviderCapability.Unsupported;public TacticalInputStatus TryGetPoint(in TacticalPointProviderRequest request,out TacticalWorldPoint point){point=default;return TacticalInputStatus.UnsupportedProvider;}}
        private sealed class Costs : IAbilityCostAuthority
        {
            private readonly IReadOnlyDictionary<GameplayEntityId,MomentumLedger> _ledgers;
            internal Costs(IReadOnlyDictionary<GameplayEntityId,MomentumLedger> ledgers){_ledgers=ledgers;}
            private static int Total(IReadOnlyList<AbilityCostConfig> costs)
            {int total=0;foreach(var cost in costs){if(cost.AttributeId!=71002)return -1;total=checked(total+cost.Amount);}return total;}
            public bool CanAfford(int actor,IReadOnlyList<AbilityCostConfig> costs,out int missing)
            {missing=71002;int total=Total(costs);return _ledgers.TryGetValue(new GameplayEntityId(actor,1),out var ledger) && total>=0 && total<=ledger.Spendable;}
            public bool TryCommitCosts(int actor,IReadOnlyList<AbilityCostConfig> costs,out int missing)
            {return CanAfford(actor,costs,out missing) && _ledgers[new GameplayEntityId(actor,1)].TrySpend(Total(costs));}
        }
        private static Func<TacticalCardDefinition,CardPlayEvaluation> Evaluate(IReadOnlyDictionary<GameplayEntityId,MomentumLedger> ledgers,AbilityRuntimeModule ability,Func<long> frame)
        {var ledger=ledgers[new GameplayEntityId(1,1)];return definition=>{
                if(definition is TacticalActionCardDefinition action && action.ActionId.Value==302 && ability.Definitions.TryGet(71001,out var configured) &&
                    !ability.TryEvaluate(1,configured,new RuntimeFrame(frame()),out var blocked))
                    return new CardPlayEvaluation(blocked.ReasonCode==AbilityActivationReason.OnCooldown?CardPlayFailure.CardOnCooldown:CardPlayFailure.InsufficientMomentum,
                        definition.BaseMomentumCost,ledger.Spendable,302,blocked.RemainingCooldownFrames,60);
                return new CardPlayEvaluation(CardPlayFailure.None,definition.BaseMomentumCost,ledger.Spendable);
            };}
        private static TacticalRestoredSession Fresh(int actionCost=10,int extraAttacks=0,int abilityCost=0,bool boundTracker=false,string nonCanonical=null,long cooldownFrames=60)
        {
            var defs=new TacticalCardDefinition[]{boundTracker
                ? TacticalActionCardDefinition.CreatePointMove("attack","Point","",actionCost,
                    new TacticalPointMoveRules(5000,200,60,1000,true,TacticalPointMoveStartPolicy.OwnedMoveAccepted),1f,CardDestination.Discard)
                : new TacticalActionCardDefinition("attack","Attack","",actionCost,new CharacterActionId(302),0f),
                new TacticalUtilityCardDefinition("grant","Grant","",0,new AddMomentumPayload(3),CardDestination.Exhaust),
                new TacticalUtilityCardDefinition("prepare","Prepare","",5,new TestPreparationPayload(),CardDestination.Exhaust)};
            defs=defs.Concat(Enumerable.Range(0,extraAttacks).Select(i=>(TacticalCardDefinition)new TacticalActionCardDefinition("extra."+i,"Extra","",1,new CharacterActionId(302),0f))).ToArray();
            var abilities=new AbilityDefinitionRegistry();Assert.IsTrue(abilities.TryRegister(new AbilityDefinitionConfig(71001,"fixture.snapshot","Fixture",null,
                new AbilityCooldownConfig(cooldownFrames,0),abilityCost==0?Array.Empty<AbilityCostConfig>():new[]{new AbilityCostConfig(71002,abilityCost)},0,new AbilityActionBindingConfig(new CharacterActionId(302),1),default,0,0),out _));
            var actors=new[]{new SessionActorSnapshot {Entity=1,Generation=1,Item=11,Owner=1,Revision=2},new SessionActorSnapshot {Entity=2,Generation=1,Item=12,Owner=2,Revision=3}};
            var context=new TacticalSessionRestoreContext("profile","fixture-profile","fixture-battle","fixture-content-v1",3,60,actors,actors.Select(a=>a.Id).ToArray(),defs,abilities,1,
                new TacticalStoryStepBinding(11,21,31),3,true,8,l=>new Costs(l),(l,a)=>new Execution(),Evaluate,c=>c.CommandId==55 && c.SourceId==99 && (c.TargetId==1||c.TargetId==2) &&
                    (!c.Envelope.IsPresent || c.Envelope.TypeId==101&&c.Envelope.SchemaVersion==1),boundTracker?7:0,boundTracker,boundTracker?60f:0f);
            var ledger=new MomentumLedger();var deck=new TacticalDeckManager(3,true);
            var settle=nonCanonical=="settle-window"?new SettleDetector(new SettleDetectorConfig(7))
                :nonCanonical=="settle-speed"?new SettleDetector(new SettleDetectorConfig(8,0.07f)):null;
            ISettleDetector settleProvider=nonCanonical=="settle-provider"?new ForeignSettle():settle;
            ITacticalTimeAdapter gate=nonCanonical=="time-adapter"?new ForeignGate():new TacticalSnapshotTimeGate();
            var time=new TacticalTimeCoordinator(gate,settleProvider);
            var driver=new TacticalSingleCardDriver(new Execution(),time,deck,ledger,nonCanonical=="driver-pre"?77:240,nonCanonical=="driver-execution"?88:300);
            var stage=new TacticalStageFlowCoordinator(1,time,deck,ledger,driver,defs,nonCanonical=="seed"?7:42,context.IsCurrent,context.Opening,
                ingressBudgetTicks:nonCanonical=="ingress-budget"?77:300,
                completionSteps:nonCanonical=="completion"?new[]{new TacticalStoryStepBinding(11,22,32)}:null,
                requestStoryRestart:nonCanonical=="restart"?((run,frame)=>false):null,
                storyTransferBudgetTicks:nonCanonical=="story-budget"?88:60,
                initialMomentum:nonCanonical=="initial-momentum"?55:50,
                pointProvider:nonCanonical=="point-provider"?new PointProvider():null,
                allowConfigurableHandCapacity:nonCanonical=="opening-hand",configuredInitialHandCount:nonCanonical=="opening-hand"?1:0);
            var opening=new TacticalStoryStepToken(1,11,1,21,31);stage.OnStoryStepStarted(in opening);
            stage.OnSpawnBound(1,0,new GameplayEntityId(1,1),0);stage.OnSpawnBound(1,1,new GameplayEntityId(2,1),0);
            for(long i=1;i<=3;i++){var sample=new SettleSample(i,false,true,true,false,false,false,true,true,true,false,0,0,true);stage.OnCompletedSimulationTick(i,in sample,in sample);}
            Assert.AreEqual(TacticalStagePhase.Planning,stage.Phase);
            if(boundTracker){var tracker=new TacticalCooldownTracker(60);deck.BindCooldownTracker(tracker,7,()=>stage.LastCompletedFrame);
                tracker.TriggerCooldown(7,(TacticalActionCardDefinition)defs[0],3);}
            var map=new Dictionary<GameplayEntityId,MomentumLedger>{{actors[0].Id,ledger},{actors[1].Id,new MomentumLedger(30)}};
            var abilityRuntime=new AbilityRuntimeModule(abilities,costAuthority:context.CreateDetachedCosts(map));
            deck.DefinitionEvaluation=context.CreateDetachedEvaluation(map,abilityRuntime,()=>stage.LastCompletedFrame);
            return new TacticalRestoredSession(context,map,deck,stage,new TacticalCommandPump(stage),RuntimeCommandBuffer.CreateBounded(null,new RuntimeFrame(3),8),abilityRuntime);
        }
        private static TacticalCardInstance Find(TacticalRestoredSession s,string id)=>s.Deck.Hand.Single(c=>c.Definition.Id==id);
        private static TacticalStageCommand Command(TacticalRestoredSession s,TacticalStageCommandKind kind,TacticalCardInstance card=null)
        {card=card??s.Deck.PendingAction;return new TacticalStageCommand(s.Stage.StageRunId,s.Stage.TurnIndex,card?.InstanceId??0,card?.Version??0,s.Stage.SnapshotRevision,kind);}
        private static void Apply(TacticalRestoredSession s,TacticalStageCommandKind kind,TacticalCardInstance card=null){var c=Command(s,kind,card);Assert.AreEqual(TacticalStageRejection.None,s.Stage.Apply(in c));}
        private static string Capture(TacticalRestoredSession s)=>TacticalSessionSnapshotJson.Write(s.Capture());
        private static TacticalRestoredSession Restore(TacticalRestoredSession s)
        {var owner=new TacticalSessionSnapshotOwner(s);Assert.IsTrue(owner.TryRestore(TacticalSessionSnapshotJson.Read(Capture(s)),s.Context,out var issue),issue);return owner.Current;}

        [Test] public void CaptureRestoreRetainsPaidExhaustPreparationAndAbilityDeadlineWithoutGrantingAgain()
        {
            var s=Fresh();Apply(s,TacticalStageCommandKind.SelectAction,Find(s,"prepare"));Apply(s,TacticalStageCommandKind.ConfirmExecution);
            Assert.AreEqual(45,s.Ledger.Current);Assert.AreEqual(5,s.Deck.PaidCardMomentum);Assert.IsTrue(s.Stage.TestPreparation.IsActive);
            Assert.IsTrue(s.Abilities.Definitions.TryGet(71001,out var ability));Assert.IsTrue(s.Abilities.TryCommitActivation(1,ability,new RuntimeFrame(3),out _));
            string before=Capture(s);var restored=Restore(s);Assert.AreEqual(before,Capture(restored));
            Assert.AreEqual(60,restored.Abilities.GetRemainingCooldownFrames(1,ability,new RuntimeFrame(3)));Assert.AreEqual(0,restored.Abilities.GetRemainingCooldownFrames(2,ability,new RuntimeFrame(3)));
            Assert.AreEqual(0,restored.Abilities.GetRemainingCooldownFrames(1,ability,new RuntimeFrame(63)));
        }
        [TestCase(10)] [TestCase(0)] public void PendingTokenSurvivesRestoreAndCancellationExactlyOnce(int cost)
        {
            var s=Fresh(cost);var card=Find(s,"attack");Apply(s,TacticalStageCommandKind.SelectAction,card);var token=s.Ledger.CaptureSessionState().ActiveToken;
            Assert.Greater(token,0);var r=Restore(s);Assert.AreEqual(Capture(s),Capture(r));Assert.AreNotSame(s.Ledger,r.Ledger);
            Apply(r,TacticalStageCommandKind.CancelPending);Assert.AreEqual(50,r.Ledger.Current);Assert.AreEqual(0,r.Ledger.Reserved);
            Assert.IsFalse(r.Ledger.ReleaseReservation(token));Assert.AreEqual(card.InstanceId,r.Deck.Hand.Single(c=>c.Definition.Id=="attack").InstanceId);
        }
        [Test] public void AcceptedControlAndReceiptSequencesResumeOnceAndOldUiLeaseCannotSubmit()
        {
            var s=Fresh();var card=Find(s,"attack");var command=Command(s,TacticalStageCommandKind.SelectAction,card);
            Assert.AreEqual(TacticalStageRejection.None,s.Controls.TrySubmit(in command,0,1));var owner=new TacticalSessionSnapshotOwner(s);ulong oldLease=owner.Lease;
            Assert.IsTrue(owner.TryRestore(s.Capture(),s.Context,out var issue),issue);var r=owner.Current;
            Assert.AreEqual(TacticalStageRejection.StaleSnapshot,owner.Submit(oldLease,in command,0,2));Assert.AreEqual(0,r.Ledger.Reserved);
            Assert.AreEqual(1,r.Controls.Drain());Assert.AreEqual(10,r.Ledger.Reserved);Assert.AreEqual(0,r.Controls.Drain());
            Assert.AreEqual(TacticalStageRejection.InvalidSequence,r.Controls.TrySubmit(in command,0,1));
            var reread=Restore(r);Assert.IsTrue(reread.Controls.TryReadReceipt(out var receipt));Assert.AreEqual(1,receipt.Submitted.Sequence);Assert.IsFalse(reread.Controls.TryReadReceipt(out _));
        }
        [Test] public void RuntimeEnvelopeTraceWidthsAndNextSequenceResumeWithoutReallocation()
        {
            var s=Fresh();var c=new RuntimeCommand(new RuntimeFrame(4),99,55,1,-1,int.MaxValue,int.MinValue,
                new RuntimeTraceIdentity(1,99,long.MaxValue,long.MaxValue),envelope:new RuntimeCommandEnvelope(101,1,RuntimeCommandProvenanceKind.Derived,long.MinValue,long.MaxValue,3,4,5,6,7,8));
            Assert.IsTrue(s.Commands.Enqueue(c).Success);var r=Restore(s);Assert.AreEqual(Capture(s),Capture(r));
            Assert.IsTrue(r.Commands.Enqueue(c).Success);var q=r.Commands.DrainForFrame(new RuntimeFrame(4));Assert.AreEqual(2,q.Count);
            Assert.AreEqual(0,q[0].Sequence);Assert.AreEqual(1,q[1].Sequence);Assert.AreEqual(long.MinValue,q[0].Envelope.Data0);Assert.AreEqual(long.MaxValue,q[0].TraceId.Sequence);
        }
        [Test] public void SeededRefillAfterRestoreMatchesOriginalCardsOrderVersionsAndRandomState()
        {
            var s=Fresh(1,6);var r=Restore(s);uint initial=s.Deck.CaptureSessionState(s.Context.Definitions).RandomState;
            for(int i=0;i<12;i++)foreach(var current in new[]{s,r}) {var card=current.Deck.Hand.First(c=>c.Definition is TacticalActionCardDefinition);Assert.AreEqual(CardPlayFailure.None,current.Deck.SetPendingAction(card,current.Ledger));
                Assert.AreEqual(CardPlayFailure.None,current.Deck.CommitTurn(current.Ledger,out var committed));Assert.IsTrue(current.Deck.CompleteTurn(committed.InstanceId,committed.Version));}
            Assert.AreNotEqual(initial,s.Deck.CaptureSessionState(s.Context.Definitions).RandomState);
            Assert.AreEqual(TacticalSessionSnapshotJson.Write(s.Capture()),TacticalSessionSnapshotJson.Write(r.Capture()));
        }
        [TestCase("ledger")] [TestCase("card")] [TestCase("card-definition")] [TestCase("token")] [TestCase("rng")]
        [TestCase("ability-hash")] [TestCase("ability-duplicate")] [TestCase("actor-generation")] [TestCase("owner")]
        [TestCase("command-sequence")] [TestCase("command-type")] [TestCase("unknown-schema")] [TestCase("battle")]
        [TestCase("execution")] [TestCase("story")] public void AnyBadPartRejectsBeforePublicationAndPreservesOldReferencesAndBytes(string part)
        {
            var s=Fresh();Assert.IsTrue(s.Abilities.Definitions.TryGet(71001,out var ability));Assert.IsTrue(s.Abilities.TryCommitActivation(1,ability,new RuntimeFrame(3),out _));
            Assert.IsTrue(s.Commands.Enqueue(new RuntimeCommand(new RuntimeFrame(4),99,55,1)).Success);
            var snapshot=s.Capture();switch(part){
                case "ledger":snapshot.Motion[0].Ledger.Current=101;break;case "card":snapshot.Deck.Hand[1].Instance=snapshot.Deck.Hand[0].Instance;break;
                case "card-definition":snapshot.Deck.Hand[0].Definition=99;break;case "token":snapshot.Motion[0].Ledger.ActiveToken=1;break;case "rng":snapshot.Deck.RandomState=0;break;
                case "ability-hash":{var a=JObject.Parse(snapshot.AbilityPayload);a["definitionHash"]=0;snapshot.AbilityPayload=a.ToString();break;}
                case "ability-duplicate":{var a=JObject.Parse(snapshot.AbilityPayload);((JArray)a["cooldowns"]).Add(a["cooldowns"][0].DeepClone());snapshot.AbilityPayload=a.ToString();break;}
                case "actor-generation":snapshot.Actors[0].Generation++;break;case "owner":snapshot.Actors[0].Owner++;break;
                case "command-sequence":snapshot.Commands.NextSequence=0;break;case "command-type":snapshot.Commands.Pending[0].Command=999;break;
                case "unknown-schema":snapshot.Version=3;break;case "battle":snapshot.Battle="another-battle";break;
                case "execution":snapshot.Stage.Driver.Execution=1;break;case "story":snapshot.Stage.Health=TacticalStageHealth.AwaitingStoryTransfer;break;
            }
            string before=Capture(s);var owner=new TacticalSessionSnapshotOwner(s);ulong lease=owner.Lease;
            Assert.IsFalse(owner.TryRestore(snapshot,s.Context,out var issue));Assert.That(issue,Is.Not.Empty);Assert.AreSame(s,owner.Current);Assert.AreEqual(lease,owner.Lease);Assert.AreEqual(before,Capture(s));
        }
        [Test] public void StrictJsonRejectsDuplicatePropertiesUnknownFieldsAndTrailingDocuments()
        {
            var json=Capture(Fresh());Assert.Throws<Newtonsoft.Json.JsonReaderException>(()=>TacticalSessionSnapshotJson.Read(json.Replace("\"Version\":2","\"Version\":2,\"Version\":2")));
            Assert.Catch<Exception>(()=>TacticalSessionSnapshotJson.Read(json.Replace("\"Version\":2","\"Version\":2,\"surprise\":1")));
            Assert.Catch<Exception>(()=>TacticalSessionSnapshotJson.Read(json+" {}"));
        }
        [Test] public void RestoredAdmissionReadsRestoredCooldownAndNeverPreviousAbility()
        {
            var s=Fresh(10,1);Assert.IsTrue(s.Abilities.Definitions.TryGet(71001,out var ability));
            Assert.IsTrue(s.Abilities.TryCommitActivation(1,ability,new RuntimeFrame(3),out _));var r=Restore(s);
            s.Abilities.Tick(new RuntimeTickContext(63,1d/60d,0,RuntimeTickStage.PreSimulation));
            Assert.IsTrue(s.Deck.EvaluateCard(Find(s,"attack"),s.Ledger).IsLegal);
            foreach(var card in r.Deck.Hand.Where(x=>x.Definition is TacticalActionCardDefinition)){
                var blocked=r.Deck.EvaluateCard(card,r.Ledger);Assert.AreEqual(CardPlayFailure.CardOnCooldown,blocked.Failure);Assert.AreEqual(60,blocked.RemainingCooldownFrames);}
            Assert.AreEqual(50,r.Ledger.Current);Assert.AreEqual(CardPlayFailure.CardOnCooldown,r.Deck.SetPendingAction(Find(r,"attack"),r.Ledger));
        }
        [Test] public void RestoredAbilityCostsCommitOnlyRestoredLedgerAndRespectRestoredReservation()
        {
            var s=Fresh(40,0,10);var r=Restore(s);Assert.IsTrue(r.Abilities.Definitions.TryGet(71001,out var ability));
            Assert.IsTrue(s.Ledger.TrySpend(50));Assert.IsTrue(r.Abilities.TryCommitActivation(1,ability,new RuntimeFrame(3),out _));
            Assert.AreEqual(40,r.Ledger.Current);Assert.AreEqual(0,s.Ledger.Current);
            var pending=Fresh(45,0,10);Apply(pending,TacticalStageCommandKind.SelectAction,Find(pending,"attack"));var restored=Restore(pending);
            Assert.IsTrue(restored.Abilities.Definitions.TryGet(71001,out var pendingAbility));
            Assert.IsFalse(restored.Abilities.TryCommitActivation(1,pendingAbility,new RuntimeFrame(3),out var failure));
            Assert.AreEqual(AbilityActivationReason.InsufficientResource,failure.ReasonCode);Assert.AreEqual(50,restored.Ledger.Current);
            Assert.AreEqual(45,restored.Ledger.Reserved);Assert.AreEqual(0,restored.Abilities.ActiveCooldownCount);
        }
        [Test] public void EveryBoundActorLedgerRestoresIndependentlyAndWrongGenerationRejects()
        {
            var s=Fresh(10,0,10);var id=s.Context.Actors[1].Id;Assert.IsTrue(s.Ledgers[id].TryReserve(7,out var token));var r=Restore(s);
            Assert.AreEqual(30,r.Ledgers[id].Current);Assert.AreEqual(7,r.Ledgers[id].Reserved);Assert.AreEqual(50,r.Ledger.Current);
            Assert.IsTrue(r.Ledgers[id].ReleaseReservation(token));Assert.AreEqual(7,s.Ledgers[id].Reserved);
            Assert.IsTrue(r.Abilities.Definitions.TryGet(71001,out var ability));Assert.IsTrue(r.Abilities.TryCommitActivation(2,ability,new RuntimeFrame(3),out _));
            Assert.AreEqual(20,r.Ledgers[id].Current);Assert.AreEqual(50,r.Ledger.Current);Assert.AreEqual(30,s.Ledgers[id].Current);
            var invalid=s.Capture();invalid.Motion[1].Generation++;var owner=new TacticalSessionSnapshotOwner(s);Assert.IsFalse(owner.TryRestore(invalid,s.Context,out _));Assert.AreSame(s,owner.Current);
        }
        [TestCase("missing-root-version")] [TestCase("missing-current")] [TestCase("numeric-boolean")] [TestCase("float-frame")]
        [TestCase("overflow-token")] [TestCase("ability-duplicate-property")] [TestCase("empty-input-extra")]
        public void MalformedFieldsRejectWithoutDefaultingOrCoercingValues(string part)
        {
            var s=Fresh();var owner=new TacticalSessionSnapshotOwner(s);string before=Capture(s);var json=JObject.Parse(before);
            switch(part){case "missing-root-version":json.Remove("Version");break;case "missing-current":((JObject)json["Motion"][0]["Ledger"]).Remove("Current");break;
                case "numeric-boolean":json["Deck"]["Initialized"]=1;break;case "float-frame":json["Frame"]=3.5;break;
                case "overflow-token":json["Motion"][0]["Ledger"]["NextToken"]="18446744073709551616";break;
                case "ability-duplicate-property":json["AbilityPayload"]=((string)json["AbilityPayload"]).Insert(1,"\"schemaVersion\":"+AbilityRuntimeState.CurrentSchemaVersion+",");break;
                case "empty-input-extra":json["Deck"]["Input"]["X"]=1;break;}
            bool accepted=false;try{accepted=owner.TryRestore(TacticalSessionSnapshotJson.Read(json.ToString()),s.Context,out _);}catch(Exception){}
            Assert.IsFalse(accepted);Assert.AreSame(s,owner.Current);Assert.AreEqual(before,Capture(s));Assert.AreEqual(1,owner.Lease);
        }
        [Test] public void LateDetachedAdmissionFactoryFailureKeepsCurrentSessionAndQueuedControlUsable()
        {
            var s=Fresh();var c=s.Context;var bad=new TacticalSessionRestoreContext(c.Scope,c.Profile,c.Battle,c.CatalogHash,c.Frame,c.TickRate,c.Actors,c.ResourceActors,c.Definitions,c.Abilities,c.StageRun,
                c.Opening,c.HandCapacity,c.TestPreparation,c.CommandCapacity,c.CreateDetachedCosts,c.CreateDetachedExecution,(l,a,f)=>throw new InvalidOperationException("fixture-late-admission"),c.ValidateCommand);
            var command=Command(s,TacticalStageCommandKind.SelectAction,Find(s,"attack"));Assert.AreEqual(TacticalStageRejection.None,s.Controls.TrySubmit(in command,0,1));
            string before=Capture(s);var owner=new TacticalSessionSnapshotOwner(s);Assert.IsFalse(owner.TryRestore(s.Capture(),bad,out var issue));Assert.AreEqual("fixture-late-admission",issue);
            Assert.AreSame(s,owner.Current);Assert.AreEqual(before,Capture(s));Assert.AreEqual(1,s.Controls.Drain());Assert.AreEqual(10,s.Ledger.Reserved);
        }
        [TestCase(true)] [TestCase(false)] public void PendingInputRetainsExactTypedBindingAndIntegerCoordinates(bool worldPoint)
        {
            var s=Fresh();Apply(s,TacticalStageCommandKind.SelectAction,Find(s,"attack"));var card=s.Deck.PendingAction;
            var binding=new TacticalExecutionInputBinding(s.Stage.StageRunId,s.Stage.TurnIndex,card.InstanceId,card.Version,s.Context.Actors[0].Id);
            var input=worldPoint?new TacticalExecutionInput(binding,TacticalExecutionInputSource.Manual,new TacticalWorldPoint(int.MaxValue,-1,int.MinValue),new TacticalWorldPoint(100,200,300))
                :new TacticalExecutionInput(binding,TacticalExecutionInputSource.Automatic,s.Context.Actors[1].Id);
            Assert.AreEqual(TacticalInputStatus.Valid,s.Deck.SetPendingExecutionInput(in input,in binding));var r=Restore(s);
            Assert.AreEqual(Capture(s),Capture(r));Assert.AreEqual(binding,r.Deck.PendingExecutionInput.Binding);
            Assert.AreEqual(input.Point,r.Deck.PendingExecutionInput.Point);Assert.AreEqual(input.TargetCharacter,r.Deck.PendingExecutionInput.TargetCharacter);
            var invalid=s.Capture();if(worldPoint)invalid.Deck.Input.Target=2;else invalid.Deck.Input.TargetGeneration++;
            var owner=new TacticalSessionSnapshotOwner(s);Assert.IsFalse(owner.TryRestore(invalid,s.Context,out _));Assert.AreSame(s,owner.Current);
        }
        [TestCase("driver-pre")] [TestCase("driver-execution")] [TestCase("settle-window")] [TestCase("settle-speed")]
        [TestCase("seed")] [TestCase("ingress-budget")] [TestCase("story-budget")] [TestCase("initial-momentum")]
        [TestCase("completion")] [TestCase("restart")] [TestCase("opening-hand")]
        [TestCase("settle-provider")] [TestCase("time-adapter")] [TestCase("point-provider")]
        public void NoncanonicalActualCompositionRejectsCaptureInsteadOfResettingItsRules(string part)
        {
            var custom=Fresh(nonCanonical:part);int balance=custom.Ledger.Current;ulong revision=custom.Stage.SnapshotRevision;
            Assert.Catch<InvalidOperationException>(()=>custom.Capture());Assert.AreEqual(balance,custom.Ledger.Current);Assert.AreEqual(revision,custom.Stage.SnapshotRevision);
            var owner=new TacticalSessionSnapshotOwner(custom);var normal=Fresh();
            Assert.IsFalse(owner.TryRestore(normal.Capture(),custom.Context,out _));Assert.AreSame(custom,owner.Current);Assert.AreEqual(1,owner.Lease);
            var bad=normal.Capture();bad.Composition.PreActionBudget=77;
            Assert.IsFalse(owner.TryRestore(bad,custom.Context,out _));Assert.AreSame(custom,owner.Current);Assert.AreEqual(1,owner.Lease);
        }
        [TestCase("namespace")] [TestCase("missing-tracker")] [TestCase("tick-rate")] [TestCase("entry-owner")]
        [TestCase("future-end")] [TestCase("missing-composition")] [TestCase("budget")] [TestCase("old-version")]
        [TestCase("settle-config")] [TestCase("provider-flag")]
        public void FrozenBindingAndCompositionDamageRejectsBeforeAnyPublication(string part)
        {
            var s=Fresh(boundTracker:true);var queued=Command(s,TacticalStageCommandKind.SelectAction,Find(s,"prepare"));Assert.AreEqual(TacticalStageRejection.None,s.Controls.TrySubmit(in queued,0,1));
            var invalid=s.Capture();switch(part){case "namespace":invalid.Deck.Character=999;break;
                case "missing-tracker":invalid.Deck.Cooldown=null;break;case "tick-rate":invalid.Deck.Cooldown.TickRate=30;break;
                case "entry-owner":invalid.Deck.Cooldown.Entries[0].Character=999;break;case "future-end":invalid.Deck.Cooldown.Entries[0].End=long.MaxValue;break;
                case "missing-composition":invalid.Composition=null;break;case "budget":invalid.Composition.ExecutionBudget=88;break;case "old-version":invalid.Version=1;break;
                case "settle-config":invalid.Composition.TurnSettle.EnterLinearSpeed=0.07f;break;case "provider-flag":invalid.Composition.PointBound=true;break;}
            string before=Capture(s);var owner=new TacticalSessionSnapshotOwner(s);Assert.IsFalse(owner.TryRestore(invalid,s.Context,out _));
            Assert.AreSame(s,owner.Current);Assert.AreEqual(1,owner.Lease);Assert.AreEqual(before,Capture(s));
            Assert.IsTrue(s.Deck.IsCardOnCooldown(Find(s,"attack"),3));Assert.AreEqual(50,s.Ledger.Current);Assert.AreEqual(1,s.Controls.PendingCount);
            Assert.AreEqual(1,s.Controls.Drain());Assert.AreEqual(5,s.Ledger.Reserved);Assert.AreEqual(0,s.Controls.Drain());
            Apply(s,TacticalStageCommandKind.CancelPending);Assert.AreEqual(50,s.Ledger.Current);Assert.AreEqual(0,s.Ledger.Reserved);
        }
        [Test] public void BoundTrackerRoundtripRetainsNamespaceTickRateAndCurrentDeadline()
        {
            var s=Fresh(boundTracker:true);var r=Restore(s);Assert.AreEqual(Capture(s),Capture(r));Assert.AreEqual(7,r.Deck.CharacterId);
            Assert.AreEqual(60,r.Deck.CooldownTracker.SessionTickRate);Assert.IsTrue(r.Deck.IsCardOnCooldown(Find(r,"attack"),3));
            Assert.IsFalse(r.Deck.IsCardOnCooldown(Find(r,"attack"),63));Assert.AreEqual(CardPlayFailure.CardOnCooldown,r.Deck.EvaluateCard(Find(r,"attack"),r.Ledger).Failure);
        }
        [Test] public void UnexpectedTrackerOnUnboundCompositionAndMissingFieldsReject()
        {
            var s=Fresh();var invalid=s.Capture();invalid.Deck.Cooldown=new CardCooldownSnapshot {TickRate=60,Entries=Array.Empty<CardCooldownEntry>()};
            var owner=new TacticalSessionSnapshotOwner(s);Assert.IsFalse(owner.TryRestore(invalid,s.Context,out _));Assert.AreSame(s,owner.Current);
            var json=JObject.Parse(Capture(s));json.Remove("Composition");Assert.Catch<Exception>(()=>TacticalSessionSnapshotJson.Read(json.ToString()));
            json=JObject.Parse(Capture(s));((JObject)json["Composition"]).Remove("ExecutionBudget");Assert.Catch<Exception>(()=>TacticalSessionSnapshotJson.Read(json.ToString()));
            s.Deck.BindCooldownTracker(new TacticalCooldownTracker(),7);Assert.Catch<InvalidOperationException>(()=>s.Capture());
        }
        [TestCase(0,0,3)] [TestCase(60,1,3)] [TestCase(60,63,3)] [TestCase(60,long.MaxValue,long.MaxValue-60)]
        public void LegalExpiredExactAndMaximumFrameAbilityDeadlinesRemainRestorable(long duration,long end,long frame)
        {
            var s=Fresh(cooldownFrames:duration);var snapshot=s.Capture();var payload=JObject.Parse(snapshot.AbilityPayload);
            ((JArray)payload["cooldowns"]).Add(new JObject {{"casterEntityId",1},{"abilityId",71001},{"endFrame",end}});snapshot.AbilityPayload=payload.ToString();
            snapshot.Frame=frame;snapshot.Stage.LastFrame=frame;snapshot.Commands.Frame=frame;var c=ContextAt(s.Context,frame);
            var owner=new TacticalSessionSnapshotOwner(s);Assert.IsTrue(owner.TryRestore(snapshot,c,out var issue),issue);
            Assert.IsTrue(owner.Current.Abilities.Definitions.TryGet(71001,out var ability));Assert.AreEqual(end>frame?end-frame:0,owner.Current.Abilities.GetRemainingCooldownFrames(1,ability,new RuntimeFrame(frame)));
        }
        [TestCase(60,64)] [TestCase(60,long.MaxValue)] [TestCase(0,4)]
        public void ImpossibleAbilityDeadlineRejectsAndOriginalQueuedControlStillConsumesOnce(long duration,long end)
        {
            var s=Fresh(cooldownFrames:duration);var command=Command(s,TacticalStageCommandKind.SelectAction,Find(s,"attack"));Assert.AreEqual(TacticalStageRejection.None,s.Controls.TrySubmit(in command,0,1));
            var snapshot=s.Capture();var payload=JObject.Parse(snapshot.AbilityPayload);((JArray)payload["cooldowns"]).Add(new JObject {{"casterEntityId",1},{"abilityId",71001},{"endFrame",end}});snapshot.AbilityPayload=payload.ToString();
            string before=Capture(s);var owner=new TacticalSessionSnapshotOwner(s);Assert.IsFalse(owner.TryRestore(snapshot,s.Context,out _));Assert.AreSame(s,owner.Current);Assert.AreEqual(1,owner.Lease);
            Assert.AreEqual(before,Capture(s));Assert.AreEqual(1,s.Controls.Drain());Assert.AreEqual(10,s.Ledger.Reserved);Assert.AreEqual(0,s.Controls.Drain());
        }
        private static TacticalSessionRestoreContext ContextAt(TacticalSessionRestoreContext c,long frame)
            => new TacticalSessionRestoreContext(c.Scope,c.Profile,c.Battle,c.CatalogHash,frame,c.TickRate,c.Actors,c.ResourceActors,c.Definitions,c.Abilities,c.StageRun,
                c.Opening,c.HandCapacity,c.TestPreparation,c.CommandCapacity,c.CreateDetachedCosts,c.CreateDetachedExecution,c.CreateDetachedEvaluation,c.ValidateCommand,c.DeckCharacter,c.CardCooldownBound,c.CardCooldownTickRate);
        [Test] public void StoryBindingUnsupportedIngressWindowAndImpossibleCaptureDeadlineReject()
        {
            var story=Fresh();story.Stage.BindEndTriggers((TacticalStageEndTriggerBridge)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TacticalStageEndTriggerBridge)));Assert.Catch<InvalidOperationException>(()=>story.Capture());
            var ingress=Fresh();typeof(TacticalStageFlowCoordinator).GetField("_playerIngressDetector",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .SetValue(ingress.Stage,new SettleDetector(new SettleDetectorConfig(4)));Assert.Catch<InvalidOperationException>(()=>ingress.Capture());
            var abilityState=Fresh();Assert.IsTrue(abilityState.Abilities.Definitions.TryGet(71001,out var ability));Assert.IsTrue(abilityState.Abilities.TryCommitActivation(1,ability,new RuntimeFrame(long.MaxValue-60),out _));
            Assert.Catch<InvalidOperationException>(()=>abilityState.Capture());Assert.AreEqual(50,abilityState.Ledger.Current);
        }
    }
}
