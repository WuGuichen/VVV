using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Tests.Items;
using MxFramework.Runtime;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Prepared real Host/ACK/deck/paused-owner checks. Only the unified Unity coordinator runs these.</summary>
    [Category("DoubleEdgeNative")][Parallelizable(ParallelScope.None)]
    public sealed class TacticalDoubleEdgeNativeTests
    {
        private float _scale;
        [SetUp] public void Setup() => _scale = UnityEngine.Time.timeScale;
        [TearDown] public void Teardown() => UnityEngine.Time.timeScale = _scale;
        private static JointInteractionPausedSessionNativeTests.Fixture Fixture(int? executionBudgetTicks = null)
            => new JointInteractionPausedSessionNativeTests.Fixture(executionBudgetTicks: executionBudgetTicks,
                transformPlayerDefinition: source => source.Kind != TacticalCardKind.Action ? source :
                new TacticalActionCardDefinition(source.Id, source.Name, source.Description, source.BaseMomentumCost,
                    source.PublishedCardId, source.PublishedRevision, source.PublishedContentHash,
                    source.DeckEntryInstanceId == 0 ? 90 : source.DeckEntryInstanceId, source.IconKey));
        private static TacticalCardInstance Attach(JointInteractionPausedSessionNativeTests.Fixture f)
        {
            var card = f.Mixed.Stage.Deck.Hand.Single(c => c.Definition.Kind == TacticalCardKind.Action); var d = card.Definition;
            var snapshot = TacticalFixedAssemblyManifestTests.Snapshot();
            snapshot.Quality = CharacterTacticalCardQuality.Rainbow; snapshot.AccessorySlotCount = 3;
            snapshot.Assembly.MainCard.CardId = d.PublishedCardId; snapshot.Assembly.MainCard.Revision = d.PublishedRevision;
            snapshot.Assembly.MainCard.ContentHash = d.PublishedContentHash;
            snapshot.Accessories = TacticalDoubleEdgePublication.Create(null).Configuration.Accessories;
            snapshot.Assembly.Accessories = snapshot.Accessories.Select((a, i) => new CharacterTacticalAccessorySlot {
                SlotIndex = i, AccessoryItemInstanceId = 81 + i, Reference = a.Reference }).ToList();
            Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(snapshot, d, 11, 7, 3,
                f.Deployment.Participants.Single(p => p.IsPlayerControlled).Bindings[d.DeckEntryInstanceId], out var assembly, out var issue), issue);
            d.AttachFixedAssembly(assembly); return card;
        }

        [Test] public void FreshRealMixedStageIngressReachesPlanningWithoutCleanupOrFault()
        {
            using var f = Fixture(); var stage = f.Mixed.Stage;
            Assert.AreEqual(TacticalStagePhase.Planning, stage.Phase);
            Assert.AreEqual(TacticalStageHealth.Running, stage.Health, stage.ExecutionIssue);
            Assert.AreEqual(TacticalStageRejection.None, stage.Fault); Assert.IsFalse(stage.HasReleasedCleanup);
            Assert.AreEqual(0UL, stage.Driver.ExecutionId); Assert.AreEqual(1UL, stage.TurnIndex);
            Assert.IsFalse(stage.Time.HasPauseReason(TacticalPauseReason.ExecutionFault));
            Assert.IsFalse(stage.Time.HasPauseReason(TacticalPauseReason.RecoveryTimeout));
        }

        [Test] public void ActualJointAckNaturalTurnExhaustAndFrozenPausedRestore()
        {
            using var f = Fixture(); var card = Attach(f); var s = f.Mixed.Stage; ulong turn = s.TurnIndex;
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            for (int i = 0; i < 1200 && (s.TurnIndex == turn || s.Phase != TacticalStagePhase.Planning); i++) f.Tick();
            Assert.AreEqual(TacticalStageHealth.Running, s.Health, s.ExecutionIssue); Assert.Greater(s.TurnIndex, turn, s.ExecutionIssue);
            Assert.AreEqual(CardZone.ExhaustPile, card.Zone); Assert.AreEqual(0, s.Ledger.Reserved);
            var state = f.Slice.CharacterRuntime.World.DoubleEdgeState; var rows = state.CaptureProjection();
            Assert.IsTrue(rows.Any(row => row[0] == 3), "The real CharacterAction ACK must have frozen a source.");
            Assert.AreEqual(1, state.OccupiedFieldSlots); Assert.AreEqual(1, rows.Single(row => row[0] == 2)[24]);
            long hash = f.Slice.Services.Network.CreateSnapshotParticipant().ComputeStateHash(new RuntimeFrame(s.LastCompletedFrame));
            f.Freeze(); string json = f.Owner.Capture();
            Assert.IsTrue(f.Owner.TryRestore(json, out var issue), issue);
            Assert.AreNotSame(state, f.Slice.CharacterRuntime.World.DoubleEdgeState);
            Assert.AreEqual(1, f.Slice.CharacterRuntime.World.DoubleEdgeState.OccupiedFieldSlots);
            Assert.AreEqual(hash, f.Slice.Services.Network.CreateSnapshotParticipant().ComputeStateHash(new RuntimeFrame(s.LastCompletedFrame)));
            var tampered = Newtonsoft.Json.Linq.JObject.Parse(json);
            var network = Newtonsoft.Json.Linq.JObject.Parse((string)tampered["Network"]);
            var projection = (Newtonsoft.Json.Linq.JArray)(network["DoubleEdgeStage"] ?? network["doubleEdgeStage"]);
            projection[1][27] = 123456; // terminal center; incoming transport cannot grant a different field.
            tampered["Network"] = network.ToString(Newtonsoft.Json.Formatting.None);
            Assert.IsFalse(f.Owner.TryRestore(tampered.ToString(), out issue));
            Assert.IsTrue(issue.Contains("double-edge"), issue);
        }

        [Test] public void ActualPreStartCancellationExhaustsWithoutFieldRefillOrTurnAdvance()
        {
            using var f = Fixture(); var card = Attach(f); var s = f.Mixed.Stage; ulong turn = s.TurnIndex;
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            int paid = s.Ledger.Current, hand = s.Deck.Hand.Count;
            s.Driver.ReleaseControl(); for (int i = 0; i < 1200 && s.Driver.ExecutionId != 0; i++) f.Tick();
            Assert.AreEqual(0UL, s.Driver.ExecutionId, s.ExecutionIssue); Assert.AreEqual(CardZone.ExhaustPile, card.Zone);
            Assert.AreEqual(paid, s.Ledger.Current); Assert.AreEqual(0, s.Ledger.Reserved);
            Assert.AreEqual(turn, s.TurnIndex); Assert.AreEqual(hand, s.Deck.Hand.Count);
            Assert.AreEqual(0, f.Slice.CharacterRuntime.World.DoubleEdgeState.OccupiedFieldSlots);
        }

        [TestCase(false)][TestCase(true)]
        public void ActualDriverTimeoutOrAbnormalTerminalConsumesRuntimeStopAckBeforeFaultPause(bool abnormalTerminal)
            => CompleteDriverFault(abnormalTerminal);

        private static void CompleteDriverFault(bool abnormalTerminal)
        {
            using var f = Fixture(abnormalTerminal ? 300 : 1); var card = Attach(f); var s = f.Mixed.Stage;
            ulong turn = s.TurnIndex;
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            int hand = s.Deck.Hand.Count, paidAtConfirmation = s.Ledger.Current;
            ulong execution = s.Driver.ExecutionId; Assert.AreNotEqual(0UL, execution);
            TestContext.WriteLine("confirmed: " + CleanupState(f, card));
            if (abnormalTerminal)
                Assert.IsTrue(f.Mixed.PausedAdapter.TryAbortPreAction(execution, "fixture:abnormal-terminal"));
            TestContext.WriteLine("triggered: " + CleanupState(f, card));
            // The Driver owns ReleaseControl. An already consumed stop ACK can retire it synchronously.
            for (int i = 0; i < 30 && !s.HasReleasedCleanup && s.Driver.ExecutionId != 0; i++) {
                f.Tick(); TestContext.WriteLine("fault-observation: " + CleanupState(f, card));
            }
            Assert.IsTrue(s.HasReleasedCleanup || s.Driver.ExecutionId == 0, CleanupState(f, card));
            if (s.HasReleasedCleanup) {
                Assert.IsTrue(s.CanStep, "Pending runtime stop commands must retain their next simulation boundary.");
                Assert.AreEqual(CardZone.Resolving, card.Zone); Assert.AreEqual(0, s.Deck.ExhaustPile.Count);
                Assert.IsFalse(s.Time.HasPauseReason(TacticalPauseReason.ExecutionFault));
            }
            int paid = s.Ledger.Current;
            for (int i = 0; i < 1200 && s.HasReleasedCleanup; i++) {
                Assert.IsTrue(s.CanStep, CleanupState(f, card)); f.Tick();
            }
            TestContext.WriteLine("cleanup-final: " + CleanupState(f, card));
            Assert.IsFalse(s.HasReleasedCleanup, CleanupState(f, card)); Assert.AreEqual(0UL, s.Driver.ExecutionId);
            Assert.AreEqual(0UL, f.Mixed.PausedAdapter.CurrentExecutionId);
            Assert.AreEqual(0, f.Slice.RuntimeCommands.PendingCommandCount);
            Assert.AreEqual(0, f.Slice.RuntimeCommands.LastCommandErrors.Count);
            Assert.IsFalse(f.Mixed.Player.Input.HasActiveMove);
            Assert.IsTrue(!f.Mixed.Player.Input.BehaviorControl.IsPresent || f.Mixed.Player.Input.BehaviorControl.IsRelease);
            Assert.IsFalse(s.CanStep); Assert.AreEqual(TacticalStageHealth.SuspendedByFault, s.Health);
            Assert.AreEqual(CardZone.ExhaustPile, card.Zone); Assert.IsNull(s.Deck.ResolvingAction);
            Assert.IsNull(s.Deck.PendingAction); Assert.AreEqual(1, s.Deck.ExhaustPile.Count);
            Assert.AreEqual(0, s.Ledger.Reserved); Assert.AreEqual(paid, s.Ledger.Current);
            if (abnormalTerminal) Assert.AreEqual(paidAtConfirmation, s.Ledger.Current, "A pre-action abort neither pays an action nor refunds confirmation fees.");
            Assert.AreEqual(turn, s.TurnIndex); Assert.AreEqual(hand, s.Deck.Hand.Count);
            Assert.AreEqual(0, f.Slice.CharacterRuntime.World.DoubleEdgeState.OccupiedFieldSlots);
            Assert.IsTrue(s.Time.HasPauseReason(TacticalPauseReason.ExecutionFault)); Assert.IsFalse(s.CanRequestRecovery);
            if (!abnormalTerminal) Assert.IsTrue(s.Time.HasPauseReason(TacticalPauseReason.RecoveryTimeout));

            var sequence = f.Slice.RuntimeCommands.CommandBuffer.CaptureSnapshot().NextSequence;
            uint version = card.Version;
            for (int i = 0; i < 3; i++) { s.ReconcileReleasedCleanup(); f.Tick(); }
            foreach (var kind in new[] { TacticalStageCommandKind.RequestRecovery,
                TacticalStageCommandKind.SelectAction, TacticalStageCommandKind.ConfirmExecution }) {
                var command = new TacticalStageCommand(s.StageRunId, s.TurnIndex, card.InstanceId, card.Version, s.SnapshotRevision, kind);
                Assert.AreEqual(kind == TacticalStageCommandKind.RequestRecovery ? TacticalStageRejection.RecoveryRejected
                    : TacticalStageRejection.FaultSuspended, s.Apply(in command));
            }
            Assert.IsFalse(s.Driver.TryCommit(s.LastCompletedFrame, out var issue)); Assert.AreEqual("execution:NotReady", issue);
            Assert.IsFalse(f.Mixed.PausedAdapter.TryAbortPreAction(execution, "fixture:retired-execution"));
            Assert.AreEqual(sequence, f.Slice.RuntimeCommands.CommandBuffer.CaptureSnapshot().NextSequence);
            Assert.AreEqual(version, card.Version); Assert.AreEqual(1, s.Deck.ExhaustPile.Count);
            Assert.AreEqual(0, s.Ledger.Reserved); Assert.AreEqual(paid, s.Ledger.Current);
            Assert.AreEqual(turn, s.TurnIndex); Assert.AreEqual(hand, s.Deck.Hand.Count);
            Assert.IsNull(s.Deck.PendingAction); Assert.IsNull(s.Deck.ResolvingAction);
            Assert.AreEqual(0UL, s.Driver.ExecutionId); Assert.IsFalse(s.HasReleasedCleanup); Assert.IsFalse(s.CanStep);
            TestContext.WriteLine("readmission-rejected: " + CleanupState(f, card));
        }

        private static string CleanupState(JointInteractionPausedSessionNativeTests.Fixture f, TacticalCardInstance card)
        {
            var s = f.Mixed.Stage;
            return $"frame={f.Slice.CurrentFrame.Value}, execution={s.Driver.ExecutionId}, adapter={f.Mixed.PausedAdapter.CurrentExecutionId}, "
                + $"cleanup={s.HasReleasedCleanup}, step={s.CanStep}, phase={s.Time.Phase}, health={s.Health}, "
                + $"commands={f.Slice.RuntimeCommands.PendingCommandCount}, errors={f.Slice.RuntimeCommands.LastCommandErrors.Count}, "
                + $"zone={card.Zone}, exhaust={s.Deck.ExhaustPile.Count}, resolving={s.Deck.ResolvingAction?.InstanceId}, "
                + $"momentum={s.Ledger.Current}, reserved={s.Ledger.Reserved}, turn={s.TurnIndex}, hand={s.Deck.Hand.Count}, issue={s.Driver.LastIssue}";
        }

        [Test] public void FreshMixedStageExecutesARealTurnAfterAbnormalCleanupOfPreviousStage()
        {
            CompleteDriverFault(true);
            using var fresh = Fixture(); var card = Attach(fresh);
            CompleteNaturalTurn(fresh, card);
            Assert.AreEqual(CardZone.ExhaustPile, card.Zone);
            Assert.IsTrue(fresh.Slice.CharacterRuntime.World.DoubleEdgeState.CaptureProjection().Any(row => row[0] == 3),
                "The replacement stage must accept a real CharacterAction ACK, not merely initialize another hand.");
            Assert.AreEqual(1, fresh.Slice.CharacterRuntime.World.DoubleEdgeState.OccupiedFieldSlots);
        }

        [Test] public void StandaloneMixedStageNaturalTerminalTraversesTheActualExecutionPort()
        {
            using var fresh = Fixture(); var card = Attach(fresh);
            CompleteNaturalTurn(fresh, card);
            Assert.AreEqual(CardZone.ExhaustPile, card.Zone);
            Assert.IsTrue(fresh.Slice.CharacterRuntime.World.DoubleEdgeState.CaptureProjection().Any(row => row[0] == 3));
            Assert.AreEqual(1, fresh.Slice.CharacterRuntime.World.DoubleEdgeState.OccupiedFieldSlots);
        }

        private static void CompleteNaturalTurn(JointInteractionPausedSessionNativeTests.Fixture f, TacticalCardInstance card)
        {
            var s = f.Mixed.Stage; ulong turn = s.TurnIndex;
            Assert.IsTrue(f.Mixed.PausedAdapter.EvaluateCardAvailability(card, s.Ledger.Reserved).IsLegal);
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            ulong execution = s.Driver.ExecutionId;
            Assert.AreNotEqual(0UL, execution); Assert.AreEqual(TacticalStagePhase.Executing, s.Phase);
            var terminal = f.Mixed as ITacticalFixedTerminalPort;
            Assert.NotNull(terminal, "The Driver's actual Stage port must expose natural terminal qualification.");
            bool sawNatural = false;
            TestContext.WriteLine("natural-confirmed: " + CleanupState(f, card));
            for (int i = 0; i < 1200 && (s.TurnIndex == turn || s.Phase != TacticalStagePhase.Planning); i++) {
                f.Tick();
                bool actual = f.Mixed.PausedAdapter.IsNaturalFixedCompletion(execution);
                Assert.AreEqual(actual, terminal.IsNaturalFixedCompletion(execution), "Stage must forward the same execution receipt.");
                Assert.IsFalse(terminal.IsNaturalFixedCompletion(execution + 1), "A different execution cannot borrow the receipt.");
                if (actual && !sawNatural) TestContext.WriteLine("natural-receipt: " + CleanupState(f, card));
                sawNatural |= actual;
                if (s.Health != TacticalStageHealth.Running) break;
            }
            string state = CleanupState(f, card) + ", adapterIssue=" + f.Mixed.PausedAdapter.LastIssue
                + ", stageIssue=" + s.ExecutionIssue;
            TestContext.WriteLine("natural-final: " + state);
            Assert.AreEqual(TacticalStageHealth.Running, s.Health, state);
            Assert.Greater(s.TurnIndex, turn, state); Assert.AreEqual(TacticalStagePhase.Planning, s.Phase, state);
            Assert.AreEqual(0, s.Ledger.Reserved); Assert.IsTrue(sawNatural, state);
            Assert.IsFalse(s.HasReleasedCleanup); Assert.IsFalse(s.Time.HasPauseReason(TacticalPauseReason.ExecutionFault));
        }

        [Test] public void CapacityRejectsBeforeRealPaymentAndAutomaticOwnerCannotWaiveDeckCost()
        {
            using var f = Fixture(); var card = Attach(f); var s = f.Mixed.Stage; var state = f.Slice.CharacterRuntime.World.DoubleEdgeState;
            var actor = f.Mixed.Player.GameplayEntityId;
            var parameters = new TacticalDoubleEdgeParameters(1000, 1000, true,
                new TacticalBlackHolePullParameters(3000, 250, 3000, 80000), 6000, 2000);
            for (ulong id = 100; id < 104; id++) {
                var key = new TacticalDoubleEdgeCommitKey(s.StageRunId, id, id, actor, id, s.LastCompletedFrame);
                Assert.IsTrue(state.TryPrepare(key, s.TurnIndex, default, parameters, false, true, out var prepared)); Assert.IsTrue(prepared.TryPublish());
            }
            f.Apply(TacticalStageCommandKind.SelectAction, card); int current = s.Ledger.Current, reserved = s.Ledger.Reserved;
            Assert.IsFalse(s.Driver.TryCommit(s.LastCompletedFrame, out var issue)); Assert.IsNotEmpty(issue);
            Assert.AreEqual(current, s.Ledger.Current); Assert.AreEqual(reserved, s.Ledger.Reserved);
            Assert.AreSame(card, s.Deck.PendingAction); Assert.IsNull(s.Deck.ResolvingAction);
            Assert.AreEqual("execution:ShatteredRequiresRealPlayerDeck", TacticalAutoBattleRuntimeInstaller.AutomaticInputIssue((TacticalActionCardDefinition)card.Definition));
        }

        [TestCase(false, false, false)][TestCase(true, false, false)][TestCase(false, true, false)][TestCase(false, true, true)]
        public void RealHpPathCombinesOnceAndAcceptedProjectileSurvivesShooterDeath(bool deadShooter, bool trace, bool armor)
        {
            using var f = Fixture(); var world = f.Slice.CharacterRuntime.World;
            var a = f.Mixed.Player; var b = f.Mixed.CurrentTarget; var stage = world.DoubleEdgeState;
            long frame = f.Mixed.Stage.LastCompletedFrame;
            var key = new TacticalDoubleEdgeCommitKey(stage.Stage, 91, 91, a.GameplayEntityId, 91, frame);
            var p = new TacticalDoubleEdgeParameters(1500, 2000, false, default, 0, 0);
            Assert.IsTrue(stage.TryPrepare(key, stage.Turn, b.GameplayEntityId, p, true, true, out var prepared)); Assert.IsTrue(prepared.TryPublish());
            Assert.IsTrue(stage.AcknowledgeAction(key, 99, 1, frame, out var source)); Assert.IsTrue(stage.BindProjectile(71, 0, source));
            if (deadShooter) { world.SetCurrentAttribute(a.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 0); stage.RevokePair(a.GameplayEntityId); }
            world.SetCurrentAttribute(b.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 100);
            if (armor) world.GameplayComponentWorld.GetOrCreateStore<GameplayArmorIntegrityComponent>()
                .Set(b.GameplayEntityId, new GameplayArmorIntegrityComponent(100));
            var action = new CharacterActionId(17);
            var hit = new HitResolveResult(new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + a.Id.Value),
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + b.Id.Value), 17, trace ? 701 : 99, 1,
                new CombatFrame(checked((int)frame)), HitResolveKind.Damage, 2, 0, FixVector3.Zero);
            var payload = new DamageProfileResolveResult(true, "", 0, 0, DamageFormulaIds.Flat, 2, 0, 0, FixVector3.Zero, 0, 0, 0,
                new DamageProfileUsage(trace ? DamageProfileSourceKind.WeaponTrace : DamageProfileSourceKind.ProjectileImpact, action, hit.ActionInstanceId,
                    sourceEntityId: hit.AttackerId.Value, targetEntityId: hit.TargetId.Value));
            var applier = new CharacterTestCombatDamageApplier();
            applier.Apply(world, null, new RuntimeFrame(frame), new[] { hit }, _ => payload,
                resolveAccessoryEvidence: _ => trace ? CharacterTestAccessoryHitEvidence.DirectTrace(action,
                    new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), source, 99)
                    : CharacterTestAccessoryHitEvidence.DirectProjectile(action,
                        new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), stage.ProjectileSource(71, 0)));
            Assert.AreEqual(armor ? 100 : deadShooter ? 96 : 94, world.GetCurrentAttribute(b.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp));
            if (armor) {
                Assert.IsTrue(world.GameplayComponentWorld.GetOrCreateStore<GameplayArmorIntegrityComponent>().TryGet(b.GameplayEntityId, out var remaining));
                Assert.AreEqual(94, remaining.CurrentIntegrity, "Armor consumes the combined six damage once.");
            }
            Assert.AreEqual(1, applier.DamageResolvedEvents.Count, "One existing HP owner, no second supplementary damage.");
        }
    }
}
