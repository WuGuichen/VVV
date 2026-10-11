using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal static class SessionSnapshotCheck
    {
        internal static void Require(bool valid,string reason) { if(!valid) throw new InvalidOperationException("snapshot:"+reason); }
        internal static bool EnumValue<T>(T value) where T:struct => Enum.IsDefined(typeof(T),value);
    }
    public sealed partial class MomentumLedger
    {
        internal MomentumSnapshot CaptureSessionState() => new MomentumSnapshot {Current=Current,Reserved=Reserved,NextToken=_nextReservationId,ActiveToken=_reservationId};
        internal void RestoreSessionState(MomentumSnapshot s)
        {
            SessionSnapshotCheck.Require(s!=null && s.Current>=0 && s.Current<=Maximum && s.Reserved>=0 && s.Reserved<=s.Current &&
                s.ActiveToken<=s.NextToken && (s.ActiveToken!=0 || s.Reserved==0),"ledger");
            Current=s.Current;Reserved=s.Reserved;_nextReservationId=s.NextToken;_reservationId=s.ActiveToken;
        }
    }
    public sealed partial class TacticalCardInstance
    {
        internal CardSnapshot CaptureSessionState(TacticalCardDefinition[] definitions)
        {
            int index=Array.FindIndex(definitions,d=>ReferenceEquals(d,Definition));
            SessionSnapshotCheck.Require(index>=0,"unknown-card-definition");
            return new CardSnapshot {Instance=InstanceId,Definition=index,Zone=Zone,Version=Version,Upgrade=UpgradeLevel};
        }
        internal static TacticalCardInstance RestoreSessionState(CardSnapshot s,TacticalCardDefinition[] definitions,CardZone expected)
        {
            SessionSnapshotCheck.Require(s!=null && s.Instance!=0 && s.Definition>=0 && s.Definition<definitions.Length && s.Upgrade>=0 && s.Zone==expected &&
                (s.Zone==CardZone.DrawPile || s.Version>0),"card");
            return new TacticalCardInstance(s.Instance,definitions[s.Definition],s.Upgrade) {Zone=s.Zone,Version=s.Version};
        }
    }
    public sealed partial class TacticalCooldownTracker
    {
        internal float SessionTickRate => _tickRate;
        internal CardCooldownSnapshot CaptureSessionState() => new CardCooldownSnapshot {TickRate=_tickRate,Entries=_endTicks.OrderBy(p=>p.Key.CharacterId).ThenBy(p=>p.Key.Identifier,StringComparer.Ordinal)
            .Select(p=>new CardCooldownEntry {Character=p.Key.CharacterId,Key=p.Key.Identifier,End=p.Value}).ToArray()};
        internal static TacticalCooldownTracker RestoreSessionState(CardCooldownSnapshot s,TacticalCardDefinition[] definitions,int character,long frame)
        {
            SessionSnapshotCheck.Require(s!=null && s.Entries!=null,"card-cooldown");
            var c=new TacticalCooldownTracker(s.TickRate);
            var allowed=new Dictionary<string,long>(StringComparer.Ordinal);
            foreach(var d in definitions.OfType<TacticalActionCardDefinition>()) {if(d.UsesCardCooldown){
                double duration=Math.Ceiling(d.CooldownSeconds*s.TickRate);
                SessionSnapshotCheck.Require(!double.IsInfinity(duration) && duration>0 && duration<long.MaxValue,"unsupported-card-cooldown-duration");
                long ticks=(long)duration;allowed[d.Id]=Math.Max(allowed.TryGetValue(d.Id,out var existing)?existing:0,ticks);
                if(!string.IsNullOrEmpty(d.SharedCooldownGroup))allowed[d.SharedCooldownGroup]=Math.Max(allowed.TryGetValue(d.SharedCooldownGroup,out var shared)?shared:0,ticks);}}
            foreach(var e in s.Entries) {
                long duration=0;
                SessionSnapshotCheck.Require(e!=null && e.Character==character && e.End>=0 && allowed.TryGetValue(e.Key,out duration),"card-cooldown-owner");
                SessionSnapshotCheck.Require(e.End<=frame || e.End-frame<=duration,"card-cooldown-deadline");
                SessionSnapshotCheck.Require(c._endTicks.TryAdd(new TacticalCooldownKey(e.Character,e.Key),e.End),"duplicate-card-cooldown");
            }
            return c;
        }
    }
    public sealed partial class TacticalDeckManager
    {
        internal void ValidateFrozenCooldownBinding(TacticalSessionRestoreContext c)
        {
            SessionSnapshotCheck.Require(CharacterId==c.DeckCharacter && (CooldownTracker!=null)==c.CardCooldownBound &&
                (CooldownTracker==null || CooldownTracker.SessionTickRate==c.CardCooldownTickRate),"frozen-card-cooldown-binding");
        }
        internal TacticalDeckSnapshot CaptureSessionState(TacticalCardDefinition[] d) => new TacticalDeckSnapshot {
            Capacity=HandCapacity,Character=CharacterId,Initialized=_initialized,Replenishment=_replenishmentAvailable,TestPreparation=SupportsTestPreparation,
            NextInstance=_nextInstanceId,RandomState=_randomState,PendingHandIndex=_pendingHandIndex,Paid=PaidCardMomentum,
            PendingToken=_reservationId,ResolvingToken=_resolvingReservationId,
            Draw=_drawPile.Select(c=>c.CaptureSessionState(d)).ToArray(),Hand=_hand.Select(c=>c.CaptureSessionState(d)).ToArray(),
            Discard=_discardPile.Select(c=>c.CaptureSessionState(d)).ToArray(),Exhaust=_exhaustPile.Select(c=>c.CaptureSessionState(d)).ToArray(),
            Pending=PendingAction?.CaptureSessionState(d),Resolving=ResolvingAction?.CaptureSessionState(d),Input=SessionInputSnapshot.Capture(PendingExecutionInput),Cooldown=CooldownTracker?.CaptureSessionState() };

        // Cold restore, exclusively on an unpublished candidate. Never initialize/deal/shuffle.
        internal void RestoreSessionState(TacticalDeckSnapshot s,TacticalCardDefinition[] d,MomentumLedger ledger,Func<long> frame,long savedFrame)
        {
            SessionSnapshotCheck.Require(s!=null && s.Capacity==HandCapacity && s.TestPreparation==SupportsTestPreparation && s.Initialized && s.RandomState!=0 && s.Paid>=0 &&
                s.Draw!=null && s.Hand!=null && s.Discard!=null && s.Exhaust!=null && s.Input!=null && s.Hand.Length<=HandCapacity && !(s.Pending!=null && s.Resolving!=null),"deck");
            var ids=new HashSet<ulong>();
            Func<CardSnapshot[],CardZone,List<TacticalCardInstance>> load=(cards,zone)=>cards.Select(c=>{
                var restored=TacticalCardInstance.RestoreSessionState(c,d,zone);
                SessionSnapshotCheck.Require(c.Instance<=s.NextInstance && ids.Add(c.Instance),"duplicate-card"); return restored; }).ToList();
            var draw=load(s.Draw,CardZone.DrawPile);var hand=load(s.Hand,CardZone.Hand);var discard=load(s.Discard,CardZone.DiscardPile);var exhaust=load(s.Exhaust,CardZone.ExhaustPile);
            var pending=s.Pending==null ? null : load(new[]{s.Pending},CardZone.PendingAction).Single();
            var resolving=s.Resolving==null ? null : load(new[]{s.Resolving},CardZone.Resolving).Single();
            SessionSnapshotCheck.Require(ids.Count==d.Length,"card-conservation");
            var all=draw.Concat(hand).Concat(discard).Concat(exhaust).Concat(pending==null?Array.Empty<TacticalCardInstance>():new[]{pending}).Concat(resolving==null?Array.Empty<TacticalCardInstance>():new[]{resolving}).ToArray();
            foreach(var def in d.Distinct()) SessionSnapshotCheck.Require(all.Count(card=>ReferenceEquals(card.Definition,def))==d.Count(entry=>ReferenceEquals(entry,def)),"definition-multiplicity");
            var l=ledger.CaptureSessionState();
            ulong token=pending!=null?s.PendingToken:resolving!=null?s.ResolvingToken:0;
            SessionSnapshotCheck.Require((pending!=null)==(s.PendingToken!=0) && (resolving!=null || s.ResolvingToken==0) &&
                token==l.ActiveToken && (token==0 || l.Reserved==(pending??resolving).Definition.BaseMomentumCost) &&
                s.PendingHandIndex>=0 && (pending==null || s.PendingHandIndex<=hand.Count) &&
                (s.Input.Kind==TacticalExecutionInputKind.None || pending!=null),"reservation-join");
            var input=s.Input.Restore();
            if(input.Kind!=TacticalExecutionInputKind.None) SessionSnapshotCheck.Require(input.Binding.CardInstance==pending.InstanceId && input.Binding.CardVersion==pending.Version,"pending-input-card");
            var tracker=s.Cooldown==null?null:TacticalCooldownTracker.RestoreSessionState(s.Cooldown,d,s.Character,savedFrame);
            _drawPile=draw;DrawPile=draw.AsReadOnly();_hand.Clear();_hand.AddRange(hand);_discardPile.Clear();_discardPile.AddRange(discard);_exhaustPile.Clear();_exhaustPile.AddRange(exhaust);
            PendingAction=pending;ResolvingAction=resolving;_pendingLedger=pending==null?null:ledger;_resolvingLedger=s.ResolvingToken==0?null:ledger;
            _reservationId=s.PendingToken;_resolvingReservationId=s.ResolvingToken;_nextInstanceId=s.NextInstance;_randomState=s.RandomState;_pendingHandIndex=s.PendingHandIndex;
            PaidCardMomentum=s.Paid;_initialized=s.Initialized;_replenishmentAvailable=s.Replenishment;PendingExecutionInput=input;BindCooldownTracker(tracker,s.Character,frame);
        }
    }
    public sealed partial class SettleDetector
    {
        internal void ValidateCanonicalConfig(int stableTicks)
        {
            SessionSnapshotCheck.Require(_config.RequiredStableTicks==stableTicks && _config.EnterLinearSpeed==0.08f && _config.ExitLinearSpeed==0.15f &&
                _config.EnterAngularSpeed==0.15f && _config.ExitAngularSpeed==0.30f,"unsupported-settle-config");
        }
        internal SettleSnapshot CaptureSessionState() => new SettleSnapshot {Last=_lastTick,HasTick=_hasTick,Stable=_stableTicks};
        internal void RestoreSessionState(SettleSnapshot s,long frame)
        {
            SessionSnapshotCheck.Require(s!=null && s.Last>=0 && s.Last<=frame && s.Stable>=0 && s.Stable<=_config.RequiredStableTicks &&
                (s.HasTick || s.Stable==0),"settle-window");
            _lastTick=s.Last;_hasTick=s.HasTick;_stableTicks=s.Stable;
        }
    }
    public sealed partial class TacticalTimeCoordinator
    {
        internal void ValidateCanonicalComposition()
        {
            SessionSnapshotCheck.Require(_adapter is TacticalSnapshotTimeGate &&
                (_detector is SettleDetector || _detector is TacticalTurnPauseDetector),"unsupported-time-composition");
            if (_detector is SettleDetector settle) settle.ValidateCanonicalConfig(8);
        }
        internal TimeSnapshot CaptureSessionState() => new TimeSnapshot {Pause=_pauseReasons,Suspend=_suspendReasons,Initialized=_initialized,StoryTransfer=_storyTransfer,
            ActionStarted=_actionStarted,Paused=IsWorldPaused,Phase=Phase,ResumePhase=_resumePhase,Turn=CurrentTurnIndex,
            CommittedCard=_committedId,CommittedVersion=_committedVersion,Settle=(_detector as SettleDetector)?.CaptureSessionState()};
        internal void RestorePlanningSessionState(TimeSnapshot s,long frame)
        {
            SessionSnapshotCheck.Require(s!=null && s.Initialized && s.Turn!=0 && s.Phase==TacticalTurnPhase.ReadyPaused && s.Paused && (s.Pause&1)!=0 &&
                (s.Pause&~31u)==0 && s.Suspend==0 && !s.StoryTransfer && !s.ActionStarted && s.CommittedCard==0 && s.CommittedVersion==0 &&
                SessionSnapshotCheck.EnumValue(s.ResumePhase),"unsupported-time-phase");
            if (_detector is SettleDetector settle) settle.RestoreSessionState(s.Settle,frame);
            else {
                // Main's normal-turn pause detector is stateless; a settle window would invent authority.
                SessionSnapshotCheck.Require(_detector is TacticalTurnPauseDetector && s.Settle == null,"unsupported-settle-provider");
            }
            _pauseReasons=s.Pause;_suspendReasons=s.Suspend;_initialized=s.Initialized;_storyTransfer=s.StoryTransfer;_actionStarted=s.ActionStarted;
            Phase=s.Phase;_resumePhase=s.ResumePhase;CurrentTurnIndex=s.Turn;_committedDeck=null;_committedId=s.CommittedCard;_committedVersion=s.CommittedVersion;
            // The factory must supply a detached gate. No Unity/global time mutation while preparing.
            _adapter.SetSimulationPaused(s.Paused);
        }
    }
    public sealed partial class TacticalSingleCardDriver
    {
        internal void ValidateCanonicalComposition()
            => SessionSnapshotCheck.Require(_preActionBudget==240 && _executionBudget==300,"unsupported-driver-budgets");
        internal DriverSnapshot CaptureSessionState() => new DriverSnapshot {LastTick=_lastTick,Elapsed=_elapsedTicks,Started=_started,PhysicalStarted=_physicalStarted,
            Deferred=_deferPayment,Timeout=_timeoutRaised,Execution=ExecutionId,Issue=LastIssue};
        internal void RestoreIdleSessionState(DriverSnapshot s,long frame)
        {
            SessionSnapshotCheck.Require(s!=null && s.Execution==0 && s.LastTick>=0 && s.LastTick<=frame && s.Elapsed>=0,"unsupported-execution-provider");
            _lastTick=s.LastTick;_elapsedTicks=s.Elapsed;_started=s.Started;_physicalStarted=s.PhysicalStarted;_deferPayment=s.Deferred;_timeoutRaised=s.Timeout;
            ExecutionId=s.Execution;LastIssue=s.Issue??string.Empty;
        }
    }
}
