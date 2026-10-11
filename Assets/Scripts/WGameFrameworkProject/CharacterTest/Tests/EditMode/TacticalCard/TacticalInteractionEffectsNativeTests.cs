using System;
using System.Linq;
using System.Reflection;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Levels;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    // Engine-native tests. No persistent services, formal IDs, scene edits or source stubs.
    // These exercise Lab.Freeze -> real GameSlice -> mixed Stage/CommandPump, not rendered UI clicks.
    [Parallelizable(ParallelScope.None)]
    public sealed class TacticalInteractionEffectsNativeTests
    {
        private float _scale;
        [SetUp] public void Setup() => _scale = Time.timeScale;
        [TearDown] public void Teardown() => Time.timeScale = _scale;

        private sealed class Fixture : IDisposable
        {
            internal readonly GameSlice Slice;
            private ulong _sequence;
            internal TacticalMixedPlayerCardRuntime Mixed => Slice.Services.Modules.MixedPlayerTacticalStage;
            internal CharacterTestCharacterRuntimeWorld World => Slice.CharacterRuntime.World;
            internal Fixture(bool formal = false)
            {
                var content = CharacterTestRuntimeAiLabCardTrialContent.Load(); var config = content.CreateDefault();
                config.InitialHand.Clear();
                foreach (string id in TacticalInteractionTrialCards.Ids)
                {
                    TacticalInteractionTrialCards.TryCreate(id,out var fallback);
                    var kind = ((InteractionPreparationPayload)fallback.Payload).Effect;
                    var published = content.Catalog.Cards.SingleOrDefault(c => c.InteractionPreparation?.Effect == kind);
                    if (formal) Assert.IsNotNull(published,"Formal fixed preparation must already be published: " + kind);
                    config.InitialHand.Add(new CharacterTestRuntimeAiLabCardTrialSelection(formal ? published.CardId : id));
                }
                config.InitialHand.Add(content.DefaultActionCard(config.PlayerCharacterId));
                var deployment = content.Freeze(config, 91);
                var specs = (CharacterTestCharacterSpawnSpec[])typeof(CharacterTestRuntimeSessionBuilder)
                    .GetMethod("BuildTacticalAutoBattleSpawnSpecs", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] {deployment});
                Slice = new GameSlice(new CharacterTestSliceOptions {EnableStoryBootstrap=false, CreateCharacterViews=false,
                    EnableLocalInputCommands=false, EnableTraceFileWrite=false, StartupCharacterSpawnSpecs=specs,
                    MixedPlayerTacticalStage=new CharacterTestTacticalStageAuthoringDocument(), TacticalDeployment=deployment, PhysicsRuntime=Floor()});
                for (int i=0;i<600 && Mixed.Stage.Phase!=TacticalStagePhase.Planning;i++) Tick();
                Assert.AreEqual(TacticalStagePhase.Planning, Mixed.Stage.Phase, Mixed.Stage.ExecutionIssue);
                Assert.AreEqual(6, Mixed.Stage.Deck.Hand.Count); Assert.AreEqual(50, Mixed.Stage.Ledger.Current);
            }
            internal void Tick() {Slice.Tick(1d/60d);Mixed.TickWorldTimeTransition(1f/60f);}
            internal TacticalCardInstance Card(string id) => Mixed.Stage.Deck.Hand.Single(c=>c.Definition.Id==id);
            internal void Play(string id)
            {
                var card=Card(id); Submit(TacticalStageCommandKind.SelectAction,card); Submit(TacticalStageCommandKind.ConfirmExecution,card);
            }
            internal void Submit(TacticalStageCommandKind kind,TacticalCardInstance card)
            {
                var s=Mixed.Stage; var c=new TacticalStageCommand(s.StageRunId,s.TurnIndex,card.InstanceId,card.Version,s.SnapshotRevision,kind);
                Assert.AreEqual(TacticalStageRejection.None,Mixed.Commands.TrySubmit(in c,0,++_sequence));
                Mixed.Commands.Drain();Assert.IsTrue(Mixed.Commands.TryReadReceipt(out var receipt));
                Assert.AreEqual(TacticalStageRejection.None,receipt.Rejection,s.ExecutionIssue);
            }
            public void Dispose()=>Slice.Dispose();
            private static CharacterTestCharacterPhysicsRuntime Floor()
            {
                var floor=new CombatLevelAabbColliderDefinition("interaction-native-floor",CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart,
                    CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart,1,1,CombatLevelFixedVector3.Zero,
                    CombatLevelFixedVector3.FromFixVector3(new FixVector3(Fix64.FromInt(-100),Fix64.FromInt(-1),Fix64.FromInt(-100))),
                    CombatLevelFixedVector3.FromFixVector3(new FixVector3(Fix64.FromInt(100),Fix64.Zero,Fix64.FromInt(100))),CombatLevelSurfaceMaterialDefinition.Default);
                var level=new CombatLevelDefinition(CombatLevelDefinition.CurrentSchemaVersion,"interaction-native-floor","Native fixture",new[]{floor},
                    new[]{new CombatLevelPointDefinition("interaction-native-spawn",CombatLevelFixedVector3.Zero)},Array.Empty<CombatLevelPointDefinition>());
                var world=CombatLevelWorldBuilder.BuildWorld(level);
                return new CharacterTestCharacterPhysicsRuntime(world,CombatPhysicsLayerMask.FromLayer(1),new FixVector3(Fix64.Half,Fix64.One,Fix64.Half),
                    Fix64.Zero,Fix64.Zero,staticColliderCount:world.TotalColliderCount,sourceDescription:"level:interaction-native-floor",groundSnapDistance:Fix64.FromRatio(2,1000));
            }
        }

        [TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        public void FormalPublishedPreparationUsesRealLabStageWithoutActionOrReserveDuplicate(int effect)
        {
            using var f = new Fixture(formal:true); var s = f.Mixed.Stage;
            var card = s.Deck.Hand.Single(c => (c.Definition as TacticalUtilityCardDefinition)?.Payload is InteractionPreparationPayload p && (int)p.Effect == effect);
            Assert.IsTrue(card.Definition.HasPublishedBehavior); Assert.AreEqual(TacticalCardKind.Utility,card.Definition.Kind);
            Assert.AreEqual(1,s.Deck.DrawPile.Count); long frame=f.Slice.CurrentFrame.Value; ulong turn=s.TurnIndex;
            f.Submit(TacticalStageCommandKind.SelectAction,card); f.Submit(TacticalStageCommandKind.ConfirmExecution,card);
            Assert.AreEqual(45,s.Ledger.Current); Assert.AreEqual(5,s.Deck.PaidCardMomentum); Assert.AreEqual(0,s.Ledger.Reserved);
            Assert.AreEqual(CardZone.ExhaustPile,card.Zone); Assert.AreEqual(5,s.Deck.Hand.Count);
            Assert.IsNull(s.Deck.ResolvingAction); Assert.IsNull(s.Deck.PendingAction);
            Assert.AreEqual(frame,f.Slice.CurrentFrame.Value); Assert.AreEqual(turn,s.TurnIndex); Assert.IsTrue(s.Time.IsWorldPaused);
            Assert.IsTrue((f.Mixed.Player.InteractionEffects.ActiveMask & (1 << (effect-1))) != 0);
        }

        [TestCase("lab.pursuit-mark")][TestCase("lab.momentum-recovery")][TestCase("lab.defense-preparation")]
        [TestCase("lab.return-mark")][TestCase("lab.hurt-charge")]
        public void LabFrozenPreparationPaysOnceWithoutActionTurnDrawOrClockAdvance(string id)
        {
            using var f=new Fixture(); var s=f.Mixed.Stage; long frame=f.Slice.CurrentFrame.Value; ulong turn=s.TurnIndex;
            int reserve=s.Deck.DrawPile.Count; var card=f.Card(id); f.Play(id);
            Assert.AreEqual(45,s.Ledger.Current); Assert.AreEqual(0,s.Ledger.Reserved); Assert.AreEqual(5,s.Deck.PaidCardMomentum);
            Assert.AreEqual(CardZone.ExhaustPile,card.Zone); Assert.IsNull(s.Deck.ResolvingAction); Assert.IsNull(s.Deck.PendingAction);
            Assert.AreEqual(turn,s.TurnIndex); Assert.AreEqual(frame,f.Slice.CurrentFrame.Value); Assert.AreEqual(reserve,s.Deck.DrawPile.Count);
            Assert.AreEqual(TacticalStagePhase.Planning,s.Phase); Assert.AreEqual(5,s.Deck.Hand.Count);
            Assert.IsTrue(TacticalInteractionEffectAuthority.ValidateSessionJoin(f.Mixed.Player.InteractionEffects.Capture(),s.Deck,s.StageRunId,s.TurnIndex,frame,f.Mixed.Player.GameplayEntityId));
        }

        [Test] public void FiveCoexistingPreparationsUseRealMomentumAndOrdinaryBlockAuthority()
        {
            using var f=new Fixture(); foreach(string id in TacticalInteractionTrialCards.Ids)f.Play(id);
            var actor=f.Mixed.Player; var target=f.Mixed.CurrentTarget; Assert.AreEqual(31,actor.InteractionEffects.ActiveMask);
            Assert.AreEqual(25,f.Mixed.Stage.Ledger.Current); long frame=f.Slice.CurrentFrame.Value+1;
            var applier=new CharacterTestCombatDamageApplier(); var blocked=Result(actor,target,frame,HitResolveKind.Blocked,10,1);
            f.World.TryGetScatterPressure(target,out var before);
            applier.Apply(f.World,null,new RuntimeFrame(frame),new[]{blocked},Payload);
            f.World.TryGetScatterPressure(target,out var after);
            Assert.AreEqual(Math.Min(before.MaxScatterDebt,before.CurrentScatterPressure+12),after.CurrentScatterPressure);
            Assert.AreEqual(27,actor.InteractionEffects.ActiveMask); Assert.AreEqual(25,f.Mixed.Stage.Ledger.Current);
            applier.ClearFrameOutputs(); applier.Apply(f.World,null,new RuntimeFrame(frame),new[]{blocked},Payload);
            f.World.TryGetScatterPressure(target,out var duplicate); Assert.AreEqual(after.CurrentScatterPressure,duplicate.CurrentScatterPressure);
        }

        [Test] public void CombatAuthorityDistinguishesEvadeZeroHitActualInjuryAndDerivedMark()
        {
            using var f=new Fixture(); foreach(string id in TacticalInteractionTrialCards.Ids)f.Play(id);
            var actor=f.Mixed.Player;var target=f.Mixed.CurrentTarget;long frame=f.Slice.CurrentFrame.Value+1;
            f.World.SetCurrentAttribute(actor.GameplayEntityId,CharacterTestCharacterAttributeIds.Hp,100);
            f.World.SetCurrentAttribute(target.GameplayEntityId,CharacterTestCharacterAttributeIds.Hp,100);
            var applier=new CharacterTestCombatDamageApplier();
            applier.Apply(f.World,null,new RuntimeFrame(frame),new[]{Result(actor,target,frame,HitResolveKind.Evaded,10,1),Result(actor,target,frame,HitResolveKind.Damage,0,2)},Payload);
            Assert.AreEqual(31,actor.InteractionEffects.ActiveMask);Assert.AreEqual(25,f.Mixed.Stage.Ledger.Current);
            applier.ClearFrameOutputs(); frame++;
            applier.Apply(f.World,null,new RuntimeFrame(frame),new[]{Result(actor,target,frame,HitResolveKind.Damage,10,3)},Payload);
            Assert.AreEqual(28,f.Mixed.Stage.Ledger.Current);Assert.IsTrue(actor.InteractionEffects.ReturnPending);
            Assert.AreEqual(20,actor.InteractionEffects.ActiveMask); // only pressure and hurt-charge survive
            Assert.AreEqual(2,applier.DamageResolvedEvents.Count);Assert.AreEqual(1,applier.HitPresentationEvents.Count);
            applier.ClearFrameOutputs(); frame++;
            applier.Apply(f.World,null,new RuntimeFrame(frame),new[]{Result(target,actor,frame,HitResolveKind.Damage,10,4)},Payload);
            Assert.AreEqual(31,f.Mixed.Stage.Ledger.Current);Assert.AreEqual(4,actor.InteractionEffects.ActiveMask);
        }

        [Test] public void LabPublishedActionUsesRealAdapterAfterPreparationAndReachesNextPlanningBoundary()
        {
            using var f=new Fixture();f.Play("lab.momentum-recovery");var stage=f.Mixed.Stage;
            var action=stage.Deck.Hand.Single(c=>c.Definition.Kind==TacticalCardKind.Action);
            int cost=stage.Deck.EvaluateCard(action,stage.Ledger).EffectiveCost;ulong turn=stage.TurnIndex;
            f.Submit(TacticalStageCommandKind.SelectAction,action);f.Submit(TacticalStageCommandKind.ConfirmExecution,action);
            Assert.AreEqual(TacticalStagePhase.Executing,stage.Phase);
            for(int i=0;i<1200 && (stage.TurnIndex==turn || stage.Phase!=TacticalStagePhase.Planning);i++)f.Tick();
            Assert.AreEqual(TacticalStageHealth.Running,stage.Health,stage.ExecutionIssue);
            Assert.Greater(stage.TurnIndex,turn);Assert.AreEqual(TacticalStagePhase.Planning,stage.Phase);
            Assert.GreaterOrEqual(stage.Deck.PaidCardMomentum,5+cost);Assert.AreEqual(0,f.Mixed.Player.InteractionEffects.UsedMask);
        }
        [Test] public void ReturnWaitsOnFirstAreaConfinedRuntimeTickThenReturnsOnceAfterRelease()
        {
            using var f=new Fixture();f.Play("lab.return-mark");var actor=f.Mixed.Player;var target=f.Mixed.CurrentTarget;
            var slot=actor.InteractionEffects.Capture().Slots[3];var point=new TacticalWorldPoint(slot.X,slot.Y,slot.Z);
            var motion=(CharacterTestCharacterMotionSystem)typeof(CharacterTestCharacterRuntimeModule)
                .GetField("_motionSystem",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f.Slice.CharacterRuntime);
            Assert.IsTrue(motion.TryCommitStoppedGroundRelocation(f.World,actor,new TacticalWorldPoint(point.X-3000,point.Y,point.Z)));
            long frame=f.Slice.CurrentFrame.Value+1;var applier=new CharacterTestCombatDamageApplier();
            applier.Apply(f.World,null,new RuntimeFrame(frame),new[]{Result(actor,target,frame,HitResolveKind.Damage,1,1)},Payload);
            Assert.IsTrue(actor.InteractionEffects.ReturnPending);Assert.AreEqual(0,actor.PendingMotionRequestCount);
            var statuses=f.World.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statuses.Set(actor.GameplayEntityId,new GameplayStatusComponent(new GameplayStatusId(CharacterTestAreaConfinementIds.AreaConfinedStatusId)));
            // Actual full Runtime Tick, not reflection into the return helper: mechanic lock is first produced in this frame.
            f.Slice.CharacterRuntime.Tick(new RuntimeTickContext(frame+1,1d/60d,0d,RuntimeTickStage.Simulation));
            Assert.IsTrue(actor.InteractionEffects.ReturnPending);Assert.AreEqual(TacticalReturnResult.WaitingForRelease,actor.InteractionEffects.ReturnResult);
            Assert.AreEqual(point.X-3000,actor.Transform.XMillimeters);Assert.AreEqual(0,actor.PendingMotionRequestCount);
            Assert.AreEqual(CharacterTestBattleStateDominantReason.ControlLock,actor.BattleState.DominantReason);
            statuses.Set(actor.GameplayEntityId,new GameplayStatusComponent(Array.Empty<GameplayStatusId>()));
            f.Slice.CharacterRuntime.Tick(new RuntimeTickContext(frame+2,1d/60d,0d,RuntimeTickStage.Simulation));
            // Assert the lock while it is active; after release the pending motion/support observations still delay return.
            Assert.IsTrue(actor.InteractionEffects.ReturnPending);
            Assert.AreEqual(TacticalReturnResult.WaitingForRelease,actor.InteractionEffects.ReturnResult);
            Assert.AreEqual(point.X-3000,actor.Transform.XMillimeters);
            f.Slice.CharacterRuntime.Tick(new RuntimeTickContext(frame+3,1d/60d,0d,RuntimeTickStage.Simulation));
            Assert.IsFalse(actor.InteractionEffects.ReturnPending);Assert.AreEqual(TacticalReturnResult.Returned,actor.InteractionEffects.ReturnResult);
            Assert.AreEqual(point.X,actor.Transform.XMillimeters);Assert.AreEqual(point.Z,actor.Transform.ZMillimeters);
            f.Slice.CharacterRuntime.Tick(new RuntimeTickContext(frame+4,1d/60d,0d,RuntimeTickStage.Simulation));
            Assert.AreEqual(TacticalReturnResult.Returned,actor.InteractionEffects.ReturnResult);Assert.IsFalse(actor.InteractionEffects.ReturnPending);
        }
        [TestCase(1)][TestCase(2)] public void RootBatchDeathPreventsDerivedArmorMutationAndEventsAtExecution(int initialHp)
        {
            using var f=new Fixture();f.Play("lab.pursuit-mark");var actor=f.Mixed.Player;var target=f.Mixed.CurrentTarget;
            f.World.SetCurrentAttribute(target.GameplayEntityId,CharacterTestCharacterAttributeIds.Hp,initialHp);f.World.SyncCombatStateHealth(target);
            var armor=f.World.GameplayComponentWorld.GetOrCreateStore<GameplayArmorIntegrityComponent>();
            armor.Set(target.GameplayEntityId,new GameplayArmorIntegrityComponent(200,100));long frame=f.Slice.CurrentFrame.Value+1;
            var applier=new CharacterTestCombatDamageApplier();
            applier.Apply(f.World,null,new RuntimeFrame(frame),new[]{Result(actor,target,frame,HitResolveKind.Damage,2,1),Result(actor,target,frame,HitResolveKind.Damage,2,2)},Payload);
            Assert.AreEqual(0,f.World.GetCurrentAttribute(target.GameplayEntityId,CharacterTestCharacterAttributeIds.Hp));
            Assert.IsTrue(armor.TryGet(target.GameplayEntityId,out var after));Assert.AreEqual(96,after.CurrentIntegrity);
            Assert.AreEqual(2,applier.DamageTelemetryRows.Count);Assert.AreEqual(2,applier.DamageResolvedEvents.Count);
            Assert.AreEqual(2,applier.HitPresentationEvents.Count);Assert.AreEqual(45,f.Mixed.Stage.Ledger.Current);
            Assert.IsFalse(actor.InteractionEffects.Capture().Slots[0].Active); // consumed, no refund or alternate target
        }
        [TestCase(false)][TestCase(true)] public void ReturnMarkUsesExistingMotionClearanceAndConsumesOneLegalAttempt(bool blocked)
        {
            using var f=new Fixture();f.Play("lab.return-mark");var actor=f.Mixed.Player;var target=f.Mixed.CurrentTarget;
            var slot=actor.InteractionEffects.Capture().Slots[3];var point=new TacticalWorldPoint(slot.X,slot.Y,slot.Z);
            var motion=(CharacterTestCharacterMotionSystem)typeof(CharacterTestCharacterRuntimeModule)
                .GetField("_motionSystem",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f.Slice.CharacterRuntime);
            Assert.IsTrue(motion.TryCommitStoppedGroundRelocation(f.World,actor,new TacticalWorldPoint(point.X-3000,point.Y,point.Z)));
            if(blocked)Assert.IsTrue(motion.TryCommitStoppedGroundRelocation(f.World,target,point));
            long frame=f.Slice.CurrentFrame.Value+1;var applier=new CharacterTestCombatDamageApplier();
            applier.Apply(f.World,null,new RuntimeFrame(frame),new[]{Result(actor,target,frame,HitResolveKind.Damage,1,1)},Payload);
            Assert.IsTrue(actor.InteractionEffects.ReturnPending);
            typeof(CharacterTestCharacterRuntimeModule).GetMethod("ProcessInteractionReturns",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(f.Slice.CharacterRuntime,new object[]{new RuntimeFrame(frame+1)});
            Assert.IsFalse(actor.InteractionEffects.ReturnPending);
            Assert.AreEqual(blocked?TacticalReturnResult.DestinationBlocked:TacticalReturnResult.Returned,actor.InteractionEffects.ReturnResult);
            Assert.AreEqual(blocked?point.X-3000:point.X,actor.Transform.XMillimeters);
            var cleared=actor.InteractionEffects.Capture().Slots[3];Assert.AreEqual(0,cleared.X);Assert.AreEqual(0,cleared.Y);Assert.AreEqual(0,cleared.Z);
        }
        private static HitResolveResult Result(CharacterTestCharacterEntity a,CharacterTestCharacterEntity b,long frame,HitResolveKind kind,int damage,int trace)
            => new HitResolveResult(new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart+a.Id.Value),new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart+b.Id.Value),0,99,trace,new CombatFrame(checked((int)frame)),kind,damage,0,FixVector3.Zero);
        private static DamageProfileResolveResult Payload(HitResolveResult r)
            => new DamageProfileResolveResult(true,"",0,0,DamageFormulaIds.Flat,r.Damage,0,0,FixVector3.Zero,0,0,1,
                new DamageProfileUsage(DamageProfileSourceKind.WeaponTrace,actionInstanceId:r.ActionInstanceId,sourceEntityId:r.AttackerId.Value,targetEntityId:r.TargetId.Value));
    }
}
