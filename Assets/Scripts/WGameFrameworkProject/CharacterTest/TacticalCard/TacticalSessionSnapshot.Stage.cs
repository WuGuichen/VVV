using System;
using System.Linq;
using System.Collections.Generic;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalStageFlowCoordinator
    {
        internal void ValidateCanonicalComposition(TacticalSessionRestoreContext c,TacticalDeckManager deck,MomentumLedger ledger)
        {
            SessionSnapshotCheck.Require(c!=null && ReferenceEquals(_deck,deck) && ReferenceEquals(_ledger,ledger) && _driver.Uses(_time,deck,ledger) &&
                StageRunId==c.StageRun && _openingStep.Equals(c.Opening) && _definitions.Length==c.Definitions.Length &&
                _definitions.Select((definition,i)=>ReferenceEquals(definition,c.Definitions[i])).All(value=>value) &&
                _seed==42 && _initialMomentum==50 && _ingressBudgetTicks==300 && _storyTransferBudgetTicks==60 && _configuredInitialHandCount==0 &&
                _completionSteps.Length==0 && _requestStoryRestart==null && _pointProvider==null && _endTriggers==null,"unsupported-stage-composition");
            // A1 only rebuilds definition admission. Accepting an instance callback here would
            // drop its live fee/reservation gate on restore; never copy a previous session closure.
            SessionSnapshotCheck.Require(deck.InstanceEvaluation==null,"unsupported-instance-admission-binding");
            _time.ValidateCanonicalComposition();_driver.ValidateCanonicalComposition();
            SessionSnapshotCheck.Require(_playerIngressDetector is SettleDetector && _opponentIngressDetector is SettleDetector,"unsupported-ingress-provider");
            ((SettleDetector)_playerIngressDetector).ValidateCanonicalConfig(3);((SettleDetector)_opponentIngressDetector).ValidateCanonicalConfig(3);
        }
        internal TacticalCardDefinition[] SnapshotDefinitions() => (TacticalCardDefinition[])_definitions.Clone();
        internal TacticalStageSnapshot CaptureSessionState()
        {
            SessionSnapshotCheck.Require(_endTriggers==null,"story-provider-required");
            SessionSnapshotCheck.Require(Phase==TacticalStagePhase.Planning && Health==TacticalStageHealth.Running &&
                _driver.CaptureSessionState().Execution==0 && _deck.ResolvingAction==null,"unsupported-capture-phase");
            var p=_testPreparation;
            return new TacticalStageSnapshot {Stage=StageRunId,Revision=SnapshotRevision,Phase=Phase,Health=Health,LastRejection=LastRejection,Fault=Fault,
                LastFrame=LastCompletedFrame,PlayerBound=_playerBoundFrame,OpponentBound=_opponentBoundFrame,TransferStart=_storyTransferStartFrame,
                Player=_player.Index,PlayerGeneration=_player.Generation,Opponent=_opponent.Index,OpponentGeneration=_opponent.Generation,
                OpeningBeat=_openingBeatInstance,IngressElapsed=_ingressElapsedTicks,Initialized=_initialized,CompletionPending=_completionPending,
                Released=_released,AwaitingPause=_awaitingWorldPause,Leaving=IsLeavingSession,Issue=ExecutionIssue,
                Time=_time.CaptureSessionState(),Driver=_driver.CaptureSessionState(),
                PlayerIngress=(_playerIngressDetector as SettleDetector)?.CaptureSessionState(),OpponentIngress=(_opponentIngressDetector as SettleDetector)?.CaptureSessionState(),
                Preparation=new PreparationSnapshot {Stage=p.StageRunId,Actor=p.Actor.Index,Generation=p.Actor.Generation,Card=p.CardInstanceId,
                    SourceVersion=p.SourceCardVersion,Activation=p.ActivationRevision,Frame=p.CompletedWorldFrame,Paid=p.PaidMomentum} };
        }
        internal void RestorePlanningSessionState(TacticalStageSnapshot s,long frame)
        {
            SessionSnapshotCheck.Require(s!=null && s.Stage==StageRunId && s.LastFrame==frame && s.Revision>0 && s.Phase==TacticalStagePhase.Planning &&
                s.Health==TacticalStageHealth.Running && s.Fault==TacticalStageRejection.None && !s.Released && !s.CompletionPending && !s.AwaitingPause && !s.Leaving &&
                s.Initialized && s.TransferStart==-1 && s.PlayerBound>=0 && s.PlayerBound<=frame && s.OpponentBound>=0 && s.OpponentBound<=frame &&
                s.IngressElapsed>=0 && s.OpeningBeat>0 && SessionSnapshotCheck.EnumValue(s.LastRejection),"unsupported-stage-phase");
            var player=new GameplayEntityId(s.Player,s.PlayerGeneration);var opponent=new GameplayEntityId(s.Opponent,s.OpponentGeneration);
            SessionSnapshotCheck.Require(player.IsValid && opponent.IsValid && player!=opponent && _isEntityCurrent(player) && _isEntityCurrent(opponent),"actor-generation");
            SessionSnapshotCheck.Require(_deck.ResolvingAction==null,"active-card-provider-required");
            _time.RestorePlanningSessionState(s.Time,frame);_driver.RestoreIdleSessionState(s.Driver,frame);
            ((SettleDetector)_playerIngressDetector).RestoreSessionState(s.PlayerIngress,frame);((SettleDetector)_opponentIngressDetector).RestoreSessionState(s.OpponentIngress,frame);
            var p=s.Preparation;SessionSnapshotCheck.Require(p!=null,"preparation");
            TacticalTestPreparationState preparation=default;
            if(p.Stage!=0) {
                var card=_deck.ExhaustPile.SingleOrDefault(c=>c.InstanceId==p.Card);
                SessionSnapshotCheck.Require(p.Stage==StageRunId && p.Actor==player.Index && p.Generation==player.Generation && card!=null &&
                    TestPreparationPayload.Matches(card.Definition) && p.SourceVersion>0 && p.SourceVersion<uint.MaxValue && card.Version==p.SourceVersion+1 &&
                    p.Paid==card.Definition.BaseMomentumCost && p.Paid<=_deck.PaidCardMomentum && p.Activation>0 && p.Activation<=s.Revision && p.Frame>=0 && p.Frame<=frame,"preparation-card-join");
                preparation=new TacticalTestPreparationState(p.Stage,player,p.Card,p.SourceVersion,p.Activation,p.Frame,p.Paid);
            } else SessionSnapshotCheck.Require(p.Card==0 && p.Actor==0 && p.Generation==0 && p.Paid==0 && p.Activation==0 && p.SourceVersion==0 && p.Frame==0,"empty-preparation");
            var input=_deck.PendingExecutionInput;
            if(input.Kind!=TacticalExecutionInputKind.None) {
                SessionSnapshotCheck.Require(input.Binding.StageRun==StageRunId && input.Binding.Turn==s.Time.Turn && input.Binding.Actor==player &&
                    (input.Source==TacticalExecutionInputSource.Manual || input.Source==TacticalExecutionInputSource.Automatic) &&
                    (input.Kind==TacticalExecutionInputKind.WorldPoint || input.Kind==TacticalExecutionInputKind.Character),"pending-input-session");
                if(input.Kind==TacticalExecutionInputKind.Character) SessionSnapshotCheck.Require(input.TargetCharacter.IsValid && _isEntityCurrent(input.TargetCharacter),"pending-target");
            }
            _player=player;_opponent=opponent;_playerBoundFrame=s.PlayerBound;_opponentBoundFrame=s.OpponentBound;_openingBeatInstance=s.OpeningBeat;
            _ingressElapsedTicks=s.IngressElapsed;_initialized=s.Initialized;_completionPending=s.CompletionPending;_released=s.Released;
            _awaitingWorldPause=s.AwaitingPause;_storyTransferStartFrame=s.TransferStart;IsLeavingSession=s.Leaving;Phase=s.Phase;Health=s.Health;
            LastRejection=s.LastRejection;Fault=s.Fault;ExecutionIssue=s.Issue??string.Empty;SnapshotRevision=s.Revision;LastCompletedFrame=s.LastFrame;_testPreparation=preparation;
        }
    }
    internal sealed partial class TacticalCommandPump
    {
        internal TacticalPumpSnapshot CaptureSessionState()
        {
            SessionSnapshotCheck.Require(!_draining,"control-drain-in-progress");
            var receipts=new ControlReceiptSnapshot[ReceiptCount];
            for(int i=0;i<receipts.Length;i++){var r=_receipts[(_receiptHead+i)%Capacity];receipts[i]=new ControlReceiptSnapshot {Submitted=QueuedControlSnapshot.Capture(r.Submitted),Rejection=r.Rejection,Revision=r.SnapshotRevision};}
            return new TacticalPumpSnapshot {Capacity=Capacity,SourceCapacity=_lastAcceptedSequence.Length,LastSequence=(ulong[])_lastAcceptedSequence.Clone(),ReceiptHead=_receiptHead,
                Pending=_pending.Take(PendingCount).Select(q=>QueuedControlSnapshot.Capture(q)).ToArray(),Receipts=receipts};
        }
        internal void RestoreSessionState(TacticalPumpSnapshot s)
        {
            SessionSnapshotCheck.Require(s!=null && s.Capacity==Capacity && s.SourceCapacity==_lastAcceptedSequence.Length && s.LastSequence!=null &&
                s.LastSequence.Length==s.SourceCapacity && s.Pending!=null && s.Receipts!=null && s.Pending.Length<=Capacity && s.Receipts.Length<=Capacity &&
                s.ReceiptHead>=0 && s.ReceiptHead<Capacity,"control-buffer");
            var identities=new HashSet<string>(StringComparer.Ordinal);
            Action<QueuedControlSnapshot,bool> validate=(q,pending)=>{
                SessionSnapshotCheck.Require(q!=null && q.Command!=null && q.Command.Input!=null && q.Source>=0 && q.Source<s.SourceCapacity && q.Sequence>0 &&
                    q.Sequence<=s.LastSequence[q.Source] && identities.Add(q.Source+":"+q.Sequence) && q.Boundary>=0 &&
                    (!pending || q.Boundary>_coordinator.LastCompletedFrame) && q.Command.Stage==_coordinator.StageRunId &&
                    q.Command.Turn>0 && q.Command.Turn<=_coordinator.TurnIndex && q.Command.Revision>0 && q.Command.Revision<=_coordinator.SnapshotRevision &&
                    SessionSnapshotCheck.EnumValue(q.Command.Kind),"control-entry");
                if(q.Command.Input.Kind!=TacticalExecutionInputKind.None) SessionSnapshotCheck.Require(q.Command.Input.Stage==_coordinator.StageRunId &&
                    q.Command.Input.Turn==q.Command.Turn && q.Command.Input.Card==q.Command.Card && q.Command.Input.Version==q.Command.Version,"control-input");
                q.Restore(); // Typed constructor validation before assigning any target buffer.
            };
            for(int i=0;i<s.Pending.Length;i++){validate(s.Pending[i],true);if(i>0) {var a=s.Pending[i-1].Restore();var b=s.Pending[i].Restore();SessionSnapshotCheck.Require(Compare(in a,in b)<0,"control-order");}}
            foreach(var r in s.Receipts){SessionSnapshotCheck.Require(r!=null && SessionSnapshotCheck.EnumValue(r.Rejection) && r.Revision>0 && r.Revision<=_coordinator.SnapshotRevision,"receipt");validate(r.Submitted,false);}
            Array.Clear(_pending,0,_pending.Length);Array.Clear(_receipts,0,_receipts.Length);Array.Copy(s.LastSequence,_lastAcceptedSequence,s.LastSequence.Length);
            PendingCount=s.Pending.Length;for(int i=0;i<PendingCount;i++)_pending[i]=s.Pending[i].Restore();
            _receiptHead=s.ReceiptHead;ReceiptCount=s.Receipts.Length;for(int i=0;i<ReceiptCount;i++){var r=s.Receipts[i];var q=r.Submitted.Restore();_receipts[(_receiptHead+i)%Capacity]=new TacticalCommandReceipt(in q,r.Rejection,r.Revision);}
        }
    }
}
