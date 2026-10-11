using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    // Candidate schema only. No production numeric schema/field registration or network publication.
    internal sealed class TacticalSessionSnapshot
    {
        public string Format = "character-test.tactical-session.candidate";
        public int Version = 2;
        public TacticalCompositionSnapshot Composition;
        public string Scope, Profile, Battle, CatalogHash;
        public long Frame;
        public int TickRate;
        public SessionActorSnapshot[] Actors;
        public SessionMomentumSnapshot[] Motion;
        public TacticalDeckSnapshot Deck;
        public TacticalStageSnapshot Stage;
        public TacticalPumpSnapshot Controls;
        public RuntimeCommandsSnapshot Commands;
        public string AbilityModule, AbilityPayload;
    }
    internal sealed class SessionActorSnapshot
    {
        public int Entity, Generation, Item, Owner;
        public long Revision;
        internal GameplayEntityId Id => new GameplayEntityId(Entity, Generation);
    }
    internal sealed class MomentumSnapshot { public int Current, Reserved; public ulong NextToken, ActiveToken; }
    internal sealed class SettleConfigSnapshot
    {
        public int StableTicks;
        public float EnterLinearSpeed,ExitLinearSpeed,EnterAngularSpeed,ExitAngularSpeed;
        internal static SettleConfigSnapshot Canonical(int stable)=>new SettleConfigSnapshot {StableTicks=stable,EnterLinearSpeed=0.08f,ExitLinearSpeed=0.15f,EnterAngularSpeed=0.15f,ExitAngularSpeed=0.30f};
        internal bool IsCanonical(int stable)=>StableTicks==stable && EnterLinearSpeed==0.08f && ExitLinearSpeed==0.15f && EnterAngularSpeed==0.15f && ExitAngularSpeed==0.30f;
    }
    internal sealed class TacticalCompositionSnapshot
    {
        public int PreActionBudget,ExecutionBudget,Seed,InitialMomentum,IngressBudget,StoryBudget,OpeningHand,CompletionCount;
        public bool RestartBound,PointBound,StoryBound,DetachedGate;
        public SettleConfigSnapshot TurnSettle,PlayerIngress,OpponentIngress;
        internal static TacticalCompositionSnapshot Canonical()=>new TacticalCompositionSnapshot {PreActionBudget=240,ExecutionBudget=300,Seed=42,InitialMomentum=50,
            IngressBudget=300,StoryBudget=60,DetachedGate=true,TurnSettle=SettleConfigSnapshot.Canonical(8),PlayerIngress=SettleConfigSnapshot.Canonical(3),OpponentIngress=SettleConfigSnapshot.Canonical(3)};
        internal bool IsCanonical()=>PreActionBudget==240 && ExecutionBudget==300 && Seed==42 && InitialMomentum==50 && IngressBudget==300 && StoryBudget==60 &&
            OpeningHand==0 && CompletionCount==0 && !RestartBound && !PointBound && !StoryBound && DetachedGate &&
            TurnSettle!=null && TurnSettle.IsCanonical(8) && PlayerIngress!=null && PlayerIngress.IsCanonical(3) && OpponentIngress!=null && OpponentIngress.IsCanonical(3);
    }
    internal sealed class SessionMomentumSnapshot { public int Actor, Generation; public MomentumSnapshot Ledger; }
    internal sealed class CardSnapshot { public ulong Instance; public int Definition, Upgrade; public uint Version; public CardZone Zone; }
    internal sealed class TacticalDeckSnapshot
    {
        public int Capacity, Character;
        public bool Initialized, Replenishment, TestPreparation;
        public ulong NextInstance, PendingToken, ResolvingToken;
        public uint RandomState;
        public int PendingHandIndex;
        public long Paid;
        public CardSnapshot[] Draw, Hand, Discard, Exhaust;
        public CardSnapshot Pending, Resolving;
        public SessionInputSnapshot Input;
        public CardCooldownSnapshot Cooldown;
    }
    internal sealed class CardCooldownSnapshot { public float TickRate; public CardCooldownEntry[] Entries; }
    internal sealed class CardCooldownEntry { public int Character; public string Key; public long End; }
    internal sealed class SettleSnapshot { public long Last; public bool HasTick; public int Stable; }
    internal sealed class TimeSnapshot
    {
        public uint Pause, Suspend;
        public bool Initialized, StoryTransfer, ActionStarted, Paused;
        public TacticalTurnPhase Phase, ResumePhase;
        public ulong Turn, CommittedCard;
        public uint CommittedVersion;
        public SettleSnapshot Settle;
    }
    internal sealed class DriverSnapshot
    {
        public long LastTick; public int Elapsed;
        public bool Started, PhysicalStarted, Deferred, Timeout;
        public ulong Execution; public string Issue;
    }
    internal sealed class PreparationSnapshot
    {
        public ulong Stage, Card, Activation; public uint SourceVersion;
        public int Actor, Generation, Paid; public long Frame;
    }
    internal sealed class TacticalStageSnapshot
    {
        public ulong Stage, Revision;
        public TacticalStagePhase Phase;
        public TacticalStageHealth Health;
        public TacticalStageRejection LastRejection, Fault;
        public long LastFrame, PlayerBound, OpponentBound, TransferStart;
        public int Player, PlayerGeneration, Opponent, OpponentGeneration, OpeningBeat, IngressElapsed;
        public bool Initialized, CompletionPending, Released, AwaitingPause, Leaving;
        public string Issue;
        public TimeSnapshot Time;
        public DriverSnapshot Driver;
        public SettleSnapshot PlayerIngress, OpponentIngress;
        public PreparationSnapshot Preparation;
    }
    internal sealed class SessionInputSnapshot
    {
        public TacticalExecutionInputKind Kind;
        public TacticalExecutionInputSource Source;
        public ulong Stage, Turn, Card; public uint Version;
        public int Actor, Generation, Target, TargetGeneration, X, Y, Z, OriginX, OriginY, OriginZ;
        internal static SessionInputSnapshot Capture(in TacticalExecutionInput i) => new SessionInputSnapshot {
            Kind=i.Kind, Source=i.Source, Stage=i.Binding.StageRun, Turn=i.Binding.Turn, Card=i.Binding.CardInstance,
            Version=i.Binding.CardVersion, Actor=i.Binding.Actor.Index, Generation=i.Binding.Actor.Generation,
            Target=i.TargetCharacter.Index, TargetGeneration=i.TargetCharacter.Generation,
            X=i.Point.X,Y=i.Point.Y,Z=i.Point.Z,OriginX=i.SelectionOrigin.X,OriginY=i.SelectionOrigin.Y,OriginZ=i.SelectionOrigin.Z };
        internal TacticalExecutionInput Restore()
        {
            if(Kind==TacticalExecutionInputKind.None) {
                SessionSnapshotCheck.Require(Source==0 && Stage==0 && Turn==0 && Card==0 && Version==0 && Actor==0 && Generation==0 && Target==0 && TargetGeneration==0 &&
                    X==0 && Y==0 && Z==0 && OriginX==0 && OriginY==0 && OriginZ==0,"empty-input");return default;
            }
            SessionSnapshotCheck.Require((Kind==TacticalExecutionInputKind.WorldPoint || Kind==TacticalExecutionInputKind.Character) &&
                (Source==TacticalExecutionInputSource.Manual || Source==TacticalExecutionInputSource.Automatic),"input-discriminator");
            var b=new TacticalExecutionInputBinding(Stage,Turn,Card,Version,new GameplayEntityId(Actor,Generation));
            if(Kind==TacticalExecutionInputKind.Character) {
                SessionSnapshotCheck.Require(X==0 && Y==0 && Z==0 && OriginX==0 && OriginY==0 && OriginZ==0,"character-input-extra");
                return new TacticalExecutionInput(b,Source,new GameplayEntityId(Target,TargetGeneration));}
            SessionSnapshotCheck.Require(Target==0 && TargetGeneration==0,"point-input-extra");
            return new TacticalExecutionInput(b,Source,new TacticalWorldPoint(X,Y,Z),new TacticalWorldPoint(OriginX,OriginY,OriginZ));
        }
    }
    internal sealed class StageCommandSnapshot
    {
        public ulong Stage, Turn, Card, Revision; public uint Version;
        public TacticalStageCommandKind Kind; public SessionInputSnapshot Input;
        internal static StageCommandSnapshot Capture(in TacticalStageCommand c) => new StageCommandSnapshot {
            Stage=c.StageRunId,Turn=c.TurnIndex,Card=c.CardInstanceId,Revision=c.SnapshotRevision,Version=c.CardVersion,Kind=c.Kind,Input=SessionInputSnapshot.Capture(c.ExecutionInput) };
        internal TacticalStageCommand Restore() => new TacticalStageCommand(Stage,Turn,Card,Version,Revision,Kind,Input.Restore());
    }
    internal sealed class QueuedControlSnapshot
    {
        public long Boundary; public int Source; public ulong Sequence; public StageCommandSnapshot Command;
        internal static QueuedControlSnapshot Capture(in TacticalQueuedCommand q) => new QueuedControlSnapshot {Boundary=q.WorldBoundary,Source=q.Source,Sequence=q.Sequence,Command=StageCommandSnapshot.Capture(q.Command)};
        internal TacticalQueuedCommand Restore(){var c=Command.Restore(); return new TacticalQueuedCommand(Boundary,Source,Sequence,in c);}
    }
    internal sealed class ControlReceiptSnapshot { public QueuedControlSnapshot Submitted; public TacticalStageRejection Rejection; public ulong Revision; }
    internal sealed class TacticalPumpSnapshot
    {
        public int Capacity, SourceCapacity, ReceiptHead; public ulong[] LastSequence;
        public QueuedControlSnapshot[] Pending; public ControlReceiptSnapshot[] Receipts;
    }
    internal sealed class RuntimeCommandsSnapshot
    {
        public long Frame, NextSequence; public int Capacity;
        public RuntimeCommandSnapshot[] Pending;
    }
    internal sealed class RuntimeCommandSnapshot
    {
        public long Frame, Sequence; public int Source, Command, Target, P0, P1, P2;
        public int TraceKind, TraceSource; public long TraceExecution, TraceSequence;
        public int EnvelopeType, EnvelopeVersion, Provenance;
        public long[] Data;
        internal static RuntimeCommandSnapshot Capture(in RuntimeCommand c) => new RuntimeCommandSnapshot {
            Frame=c.Frame.Value,Sequence=c.Sequence,Source=c.SourceId,Command=c.CommandId,Target=c.TargetId,P0=c.Payload0,P1=c.Payload1,P2=c.Payload2,
            TraceKind=c.TraceId.KindId,TraceSource=c.TraceId.SourceId,TraceExecution=c.TraceId.ExecutionId,TraceSequence=c.TraceId.Sequence,
            EnvelopeType=c.Envelope.TypeId,EnvelopeVersion=c.Envelope.SchemaVersion,Provenance=(int)c.Envelope.Provenance,
            Data=new[]{c.Envelope.Data0,c.Envelope.Data1,c.Envelope.Data2,c.Envelope.Data3,c.Envelope.Data4,c.Envelope.Data5,c.Envelope.Data6,c.Envelope.Data7} };
        internal RuntimeCommand Restore()
        {
            var envelope=EnvelopeType==0 ? default : new RuntimeCommandEnvelope(EnvelopeType,EnvelopeVersion,(RuntimeCommandProvenanceKind)Provenance,Data[0],Data[1],Data[2],Data[3],Data[4],Data[5],Data[6],Data[7]);
            return new RuntimeCommand(new RuntimeFrame(Frame),Source,Command,Target,P0,P1,P2,new RuntimeTraceIdentity(TraceKind,TraceSource,TraceExecution,TraceSequence),Sequence,envelope);
        }
    }
}
