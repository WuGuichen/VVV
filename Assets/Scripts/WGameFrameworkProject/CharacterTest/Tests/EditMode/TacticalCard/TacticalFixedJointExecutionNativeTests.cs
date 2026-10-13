using System;
using System.Linq;
using MxFramework.Runtime;
using MxFramework.Gameplay;
using MxFramework.Tests.Items;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Actual detached GameSlice/Driver/Host/command queue. No player save or scene writes.</summary>
    public sealed class TacticalFixedJointExecutionNativeTests
    {
        private float _scale;
        [SetUp] public void Setup() => _scale = UnityEngine.Time.timeScale;
        [TearDown] public void Teardown() => UnityEngine.Time.timeScale = _scale;
        private static JointInteractionPausedSessionNativeTests.Fixture Fixture()
            => new JointInteractionPausedSessionNativeTests.Fixture(transformPlayerDefinition: source => source.Kind != TacticalCardKind.Action ? source :
                new TacticalActionCardDefinition(source.Id, source.Name, source.Description, source.BaseMomentumCost,
                    source.PublishedCardId, source.PublishedRevision, source.PublishedContentHash,
                    source.DeckEntryInstanceId == 0 ? 90 : source.DeckEntryInstanceId, source.IconKey));
        private static TacticalCardInstance Attach(JointInteractionPausedSessionNativeTests.Fixture f, bool empty = false)
        {
            var card = f.Mixed.Stage.Deck.Hand.Single(c => c.Definition.Kind == TacticalCardKind.Action);
            var definition = card.Definition;
            var snapshot = TacticalFixedAssemblyManifestTests.Snapshot();
            snapshot.Assembly.MainCard.CardId = definition.PublishedCardId;
            snapshot.Assembly.MainCard.Revision = definition.PublishedRevision;
            snapshot.Assembly.MainCard.ContentHash = definition.PublishedContentHash;
            if (empty) { snapshot.Assembly.Accessories.Clear(); snapshot.Accessories = Array.Empty<CharacterTacticalAccessoryDefinition>(); }
            Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(snapshot, definition, 11, 7, 3,
                f.Deployment.Participants.Single(p => p.IsPlayerControlled).Bindings[definition.DeckEntryInstanceId], out var assembly, out var issue), issue);
            definition.AttachFixedAssembly(assembly); return card;
        }

        [TestCase(false)] [TestCase(true)]
        public void RealConfirmationPublishesPaidJournalHostDeckAndTimeTogether(bool empty)
        {
            using var f = Fixture(); var card = Attach(f, empty); var s = f.Mixed.Stage;
            int initial = s.Ledger.Current;
            f.Apply(TacticalStageCommandKind.SelectAction, card);
            Assert.AreEqual(initial, s.Ledger.Current); Assert.AreEqual(0, f.Mixed.Player.InteractionEffects.AccessoryActivationCount);
            f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            var journal = f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly;
            Assert.NotNull(journal); Assert.AreEqual(1, journal.CommitCount);
            var paid = journal.CommitAt(0);
            Assert.AreEqual(initial - card.Definition.BaseMomentumCost - (empty ? 0 : 2), s.Ledger.Current);
            Assert.AreEqual(paid.Fees.MaximumActionBudget, s.Ledger.Reserved);
            Assert.AreEqual(paid.ExecutionId, s.Driver.ExecutionId);
            Assert.AreSame(card, s.Deck.ResolvingAction); Assert.IsNull(s.Deck.PendingAction);
            Assert.AreEqual(TacticalTurnPhase.Executing, s.Time.Phase);
            Assert.AreEqual(empty ? 0 : 2, f.Mixed.Player.InteractionEffects.AccessoryActivationCount);
        }

        [Test] public void InsufficientTotalKeepsActualPendingCardLedgerAndHostUnpublished()
        {
            using var f = Fixture(); var card = Attach(f); var s = f.Mixed.Stage;
            f.Apply(TacticalStageCommandKind.SelectAction, card);
            Assert.IsTrue(s.Ledger.TrySpend(s.Ledger.Spendable));
            int current = s.Ledger.Current, reserved = s.Ledger.Reserved; uint version = card.Version;
            Assert.IsFalse(s.Driver.TryCommit(s.LastCompletedFrame, out var issue)); Assert.IsNotEmpty(issue);
            Assert.AreEqual(current, s.Ledger.Current); Assert.AreEqual(reserved, s.Ledger.Reserved);
            Assert.AreEqual(version, card.Version); Assert.AreSame(card, s.Deck.PendingAction);
            Assert.AreEqual(0ul, s.Driver.ExecutionId); Assert.IsNull(s.Deck.ResolvingAction);
            Assert.IsNull(f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly);
            Assert.AreEqual(TacticalTurnPhase.ReadyPaused, s.Time.Phase);
        }

        [Test] public void RepeatedRealConfirmationCannotPayOrActivateTwice()
        {
            using var f = Fixture(); var card = Attach(f); var s = f.Mixed.Stage;
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            int current = s.Ledger.Current; ulong execution = s.Driver.ExecutionId;
            Assert.IsFalse(s.Driver.TryCommit(s.LastCompletedFrame, out _));
            Assert.AreEqual(current, s.Ledger.Current); Assert.AreEqual(execution, s.Driver.ExecutionId);
            Assert.AreEqual(1, f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly.CommitCount);
            Assert.AreEqual(2, f.Mixed.Player.InteractionEffects.AccessoryActivationCount);
        }

        [Test] public void ActualActionCommandConsumesReservationAndNaturalTerminalReleasesRemainder()
        {
            using var f = Fixture(); var card = Attach(f); var s = f.Mixed.Stage;
            ulong turn = s.TurnIndex;
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            var paid = f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly.CommitAt(0);
            int afterConfirmation = s.Ledger.Current;
            bool observedStart = false;
            for (int i = 0; i < 1200 && s.TurnIndex == turn; i++)
            {
                f.Tick();
                if (s.Driver.ExecutionId != 0 && s.Driver.ObserveOwnedExecution(s.Driver.ExecutionId).ExecutionStarted)
                    observedStart = true;
            }
            Assert.IsTrue(observedStart, s.ExecutionIssue); Assert.Greater(s.TurnIndex, turn, s.ExecutionIssue);
            Assert.AreEqual(0, s.Ledger.Reserved); Assert.AreEqual(0ul, s.Driver.ExecutionId);
            Assert.IsTrue(s.Ledger.Current <= afterConfirmation + 2 + ((TacticalActionCardDefinition)card.Definition).MomentumReward);
            Assert.AreEqual(paid.Digest, f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly.CommitAt(0).Digest);
        }

        [Test] public void PreStartCancellationKeepsFeesAndEffectsAndWaitsForActualCleanupAck()
        {
            using var f = Fixture(); var card = Attach(f); var s = f.Mixed.Stage;
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            int paidCurrent = s.Ledger.Current; ulong id = s.Driver.ExecutionId;
            s.Driver.ReleaseControl();
            var initial = s.Driver.ObserveOwnedExecution(id);
            if (!initial.CommandsAcknowledged || !initial.OwnedIntentsReleased)
                Assert.AreEqual(f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly.CommitAt(0).Fees.MaximumActionBudget, s.Ledger.Reserved);
            for (int i = 0; i < 1200 && s.Driver.ExecutionId != 0; i++) f.Tick();
            Assert.AreEqual(0ul, s.Driver.ExecutionId, s.ExecutionIssue);
            Assert.AreEqual(0, s.Ledger.Reserved); Assert.AreEqual(paidCurrent, s.Ledger.Current);
            Assert.AreEqual(2, f.Mixed.Player.InteractionEffects.AccessoryActivationCount);
            Assert.IsTrue(f.Mixed.Player.InteractionEffects.AccessoryAt(0).Status == TacticalAccessoryCombatStatus.Armed);
        }
        [Test] public void RepeatedFeeReadoutCannotPayActivateStartHostOrMovePendingCard()
        {
            using var f = Fixture(); var card = Attach(f); var s = f.Mixed.Stage;
            f.Apply(TacticalStageCommandKind.SelectAction, card);
            int current = s.Ledger.Current, reserved = s.Ledger.Reserved; uint version = card.Version;
            ulong revision = s.SnapshotRevision;
            for (int i = 0; i < 3; i++) {
                Assert.IsTrue(s.Driver.TryReadFixedFees(card, out var fees, out var issue), issue);
                Assert.AreSame(card.Definition.FixedAssembly, fees.Assembly);
                Assert.GreaterOrEqual(fees.MaximumAdmissionCost, fees.ConfirmationCost);
            }
            Assert.AreEqual(current, s.Ledger.Current); Assert.AreEqual(reserved, s.Ledger.Reserved);
            Assert.AreEqual(version, card.Version); Assert.AreEqual(revision, s.SnapshotRevision);
            Assert.AreEqual(0ul, s.Driver.ExecutionId); Assert.AreSame(card, s.Deck.PendingAction);
            Assert.IsNull(f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly);
            Assert.AreEqual(0, f.Mixed.Player.InteractionEffects.AccessoryActivationCount);
        }

        [Test] public void ProjectedFeesMatchActualConfirmationAndPaidReceiptReadout()
        {
            using var f = Fixture(); var card = Attach(f); var s = f.Mixed.Stage;
            f.Apply(TacticalStageCommandKind.SelectAction, card);
            Assert.IsTrue(s.Driver.TryReadFixedFees(card, out var projected, out var issue), issue);
            f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            var committed = f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly.CommitAt(0).Fees;
            Assert.AreEqual(projected.MaximumAdmissionCost, committed.MaximumAdmissionCost);
            Assert.AreEqual(projected.ConfirmationCost, committed.ConfirmationCost);
            Assert.AreEqual(projected.MaximumActionBudget, committed.MaximumActionBudget);
            Assert.IsTrue(s.Driver.TryReadFixedFees(card, out var actual, out issue), issue);
            Assert.AreSame(committed, actual);
        }

        [TestCase(false)] [TestCase(true)]
        public void ExpiredSummaryStaysReadOnlyForFeesButIsRebuiltBeforeFixedConfirmation(bool missing)
        {
            using var f = Fixture(); var card = Attach(f); var s = f.Mixed.Stage;
            f.Apply(TacticalStageCommandKind.SelectAction, card);
            Assert.IsTrue(s.Driver.TryReadFixedFees(card, out _, out var issue), issue);
            var store = new CharacterActionTacticalMetricsStore();
            int character = f.Mixed.Player.Identity.CharacterId;
            int action = card.Definition.FixedAssembly.Bindings.Values.First();
            var paths = new[] { store.GetPath(character, new MxFramework.CharacterAction.CharacterActionId(action)),
                store.GetPath(CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    new MxFramework.CharacterAction.CharacterActionId(action)) }.Distinct().ToArray();
            var originals = paths.ToDictionary(path => path,
                path => System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null);
            try
            {
                foreach (string path in paths)
                {
                    if (originals[path] == null) continue;
                    if (missing) System.IO.File.Delete(path);
                    else
                    {
                        var json = Newtonsoft.Json.Linq.JObject.Parse(CharacterActionTacticalSummaryJson.Write(
                            CharacterActionTacticalSummaryBinary.Read(originals[path])));
                        json["actionContentHash"] = new string('0', 64);
                        System.IO.File.WriteAllBytes(path, CharacterActionTacticalSummaryBinary.Write(
                            CharacterActionTacticalSummaryJson.Read(json.ToString())));
                    }
                }
                var expired = paths.ToDictionary(path => path,
                    path => System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null);
                int current = s.Ledger.Current, reserved = s.Ledger.Reserved;
                long frame = f.Slice.CurrentFrame.Value; uint version = card.Version;
                Assert.IsFalse(s.Driver.TryReadFixedFees(card, out _, out issue));
                StringAssert.Contains("ActionSummary:", issue);
                foreach (string path in paths)
                    CollectionAssert.AreEqual(expired[path], System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null);
                Assert.AreEqual(current, s.Ledger.Current); Assert.AreEqual(reserved, s.Ledger.Reserved);
                Assert.AreEqual(version, card.Version); Assert.AreSame(card, s.Deck.PendingAction);
                Assert.IsNull(f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly);
                Assert.IsTrue(s.Driver.TryCommit(s.LastCompletedFrame, out issue), issue);
                Assert.AreEqual(frame, f.Slice.CurrentFrame.Value, "Repair must not step the active world.");
                Assert.AreEqual(1, f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly.CommitCount);
                Assert.AreSame(card, s.Deck.ResolvingAction);
                Assert.IsTrue(System.IO.File.Exists(paths[0]));
                Assert.AreNotEqual(new string('0', 64),
                    CharacterActionTacticalSummaryBinary.ReadFile(paths[0]).ActionContentHash);
            }
            finally
            {
                foreach (string path in paths)
                    if (originals[path] != null) System.IO.File.WriteAllBytes(path, originals[path]);
                    else if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void RealPaidFixedIdleSessionReopensWithoutPayingOrReactivating(bool empty)
        {
            using var f = Fixture(); var card = Attach(f, empty); var s = f.Mixed.Stage;
            ulong turn = s.TurnIndex;
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            for (int i = 0; i < 1200 && (s.TurnIndex == turn || s.Time.Phase != TacticalTurnPhase.ReadyPaused
                || f.Mixed.PausedWorldTime.IsTransitioning || !f.Mixed.PausedWorldTime.IsSimulationPaused); i++) f.Tick();
            Assert.Greater(s.TurnIndex, turn, s.ExecutionIssue); Assert.AreEqual(0ul, s.Driver.ExecutionId);
            Assert.AreEqual(TacticalTurnPhase.ReadyPaused, s.Time.Phase);
            Assert.IsFalse(f.Mixed.PausedWorldTime.IsTransitioning); Assert.IsTrue(f.Mixed.PausedWorldTime.IsSimulationPaused);
            // This headless fixture has no Gameplay event presentation consumer. Consume the real
            // completed-frame notifications through its public queue port before taking the bounded
            // idle snapshot. This does not supply combat results or prove a graphical restore entry.
            var notifications = new System.Collections.Generic.List<GameplayRuntimeEvent>();
            int pending = f.Slice.CharacterRuntime.World.GameplayComponentWorld.PendingEventCount;
            Assert.AreEqual(pending, f.Slice.CharacterRuntime.World.GameplayComponentWorld.DrainEvents(
                new RuntimeFrame(s.LastCompletedFrame), notifications));
            Assert.IsTrue(notifications.All(evt => evt.Frame.Value <= s.LastCompletedFrame));
            f.Freeze(true);
            string json = f.Owner.Capture(); string hash = CharacterTestPausedSessionCodec.Read(json).StateHash;
            var before = f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly.CommitAt(0);
            int balance = f.Mixed.Stage.Ledger.Current;
            for (int i = 0; i < 2; i++) {
                var old = f.Slice;
                Assert.IsTrue(f.Owner.TryRestore(json, out var issue), issue);
                Assert.AreNotSame(old, f.Slice);
                Assert.AreEqual(hash, CharacterTestPausedSessionCodec.Read(f.Owner.Capture()).StateHash);
                Assert.AreEqual(balance, f.Mixed.Stage.Ledger.Current); Assert.AreEqual(0, f.Mixed.Stage.Ledger.Reserved);
                var journal = f.Mixed.Player.InteractionEffects.ConfirmedFixedAssembly;
                Assert.AreEqual(1, journal.CommitCount); Assert.AreEqual(before.Digest, journal.CommitAt(0).Digest);
                Assert.AreEqual(empty ? 0 : 2, f.Mixed.Player.InteractionEffects.AccessoryActivationCount);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualStageExitCleansActiveHostWithoutCallingReleasedDriverTick(bool waitForAction)
        {
            using var f = new TacticalFixedMultiActionNativeTests.PaidExitFixture(); var card = f.Card; var s = f.Mixed.Stage;
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            bool active = false;
            for (int i = 0; i < 1200 && s.Driver.ExecutionId != 0; i++) {
                f.Tick();
                active = waitForAction ? s.Driver.ObserveOwnedExecution(s.Driver.ExecutionId).ActionStarted
                    : f.Mixed.Player.Input.BehaviorControl.IsPresent && !f.Mixed.Player.Input.BehaviorControl.IsRelease;
                if (active) break;
            }
            Assert.IsTrue(active); int paid = s.Ledger.Current;
            Assert.Greater(s.Ledger.Reserved, 0, "The stopped Stage must still own a positive future-action budget.");
            s.ReleaseControl(); Assert.IsTrue(s.IsReleased);
            for (int i = 0; i < 1200 && s.HasReleasedCleanup; i++) f.Tick();
            Assert.IsFalse(s.HasReleasedCleanup); Assert.AreEqual(0ul, s.Driver.ExecutionId);
            Assert.AreEqual(0, s.Ledger.Reserved); Assert.AreEqual(paid, s.Ledger.Current);
            Assert.AreEqual(1ul, s.Time.CurrentTurnIndex);
        }

        [Test] public void ActualHostShutdownRetirementReleasesFixedBudgetWithoutAnotherWorldTick()
        {
            using var f = new TacticalFixedMultiActionNativeTests.PaidExitFixture(); var card = f.Card; var s = f.Mixed.Stage;
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            f.Tick(); int paid = s.Ledger.Current;
            Assert.Greater(s.Ledger.Reserved, 0, "Shutdown must clean a positive fixed action reservation.");
            f.Slice.Dispose();
            Assert.AreEqual(0ul, s.Driver.ExecutionId); Assert.AreEqual(0, s.Ledger.Reserved);
            Assert.IsFalse(s.HasReleasedCleanup); Assert.AreEqual(paid, s.Ledger.Current);
        }

        [Test] public void RealDamageDeathCleansStartedFixedHostThroughProductionExitLane()
        {
            using var f = new TacticalFixedMultiActionNativeTests.PaidExitFixture(); var card = f.Card; var s = f.Mixed.Stage;
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            bool started = false;
            for (int i = 0; i < 1200 && s.Driver.ExecutionId != 0; i++) {
                f.Tick(); if (s.Driver.ObserveOwnedExecution(s.Driver.ExecutionId).ActionStarted) { started = true; break; }
            }
            Assert.IsTrue(started);
            Assert.Greater(s.Ledger.Reserved, 0, "Death must occur while the second action still has reserved funds.");
            var player = f.Mixed.Player; var attacker = f.Mixed.CurrentTarget;
            var hit = new MxFramework.Combat.Hit.HitResolveResult(
                new MxFramework.Combat.Core.CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + attacker.Id.Value),
                new MxFramework.Combat.Core.CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + player.Id.Value),
                0, 999, 1, new MxFramework.Combat.Core.CombatFrame(checked((int)f.Slice.CurrentFrame.Value)),
                MxFramework.Combat.Hit.HitResolveKind.Damage, 1000000, 0, MxFramework.Core.Math.FixVector3.Zero);
            var applier = new CharacterTestCombatDamageApplier();
            applier.Apply(f.Slice.CharacterRuntime.World, null, f.Slice.CurrentFrame, new[] { hit }, value =>
                new MxFramework.Combat.Damage.DamageProfileResolveResult(true, "", 0, 0, MxFramework.Combat.Damage.DamageFormulaIds.Flat,
                    value.Damage, 0, 0, MxFramework.Core.Math.FixVector3.Zero, 0, 0, 0,
                    new MxFramework.Combat.Damage.DamageProfileUsage(MxFramework.Combat.Damage.DamageProfileSourceKind.WeaponTrace,
                        actionInstanceId: value.ActionInstanceId, sourceEntityId: value.AttackerId.Value, targetEntityId: value.TargetId.Value)));
            Assert.IsFalse(player.CombatState.IsAlive); int paid = s.Ledger.Current;
            for (int i = 0; i < 1200 && (s.Driver.ExecutionId != 0 || s.HasReleasedCleanup); i++) f.Tick();
            Assert.IsTrue(f.Mixed.OutcomeReady || f.Mixed.PlayerDefeated); Assert.IsFalse(s.HasReleasedCleanup);
            Assert.AreEqual(0ul, s.Driver.ExecutionId); Assert.AreEqual(0, s.Ledger.Reserved); Assert.AreEqual(paid, s.Ledger.Current);
        }

    }
}
