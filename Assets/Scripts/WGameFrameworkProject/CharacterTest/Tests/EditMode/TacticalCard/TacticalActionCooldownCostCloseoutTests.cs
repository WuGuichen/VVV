using System;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Pure owner/driver regressions; real physics and actor admission require native integration tests.</summary>
    public sealed class TacticalActionCooldownCostCloseoutTests
    {
        private static TacticalActionCardDefinition Point(float cooldown = 2f, int cost = 10)
            => TacticalActionCardDefinition.CreatePointMove("point", "Point", "", cost,
                new TacticalPointMoveRules(5000, 200, 60, 1000, true, TacticalPointMoveStartPolicy.OwnedMoveAccepted),
                cooldown, CardDestination.Discard);

        private static TacticalDeckManager Deck(TacticalActionCardDefinition definition, TacticalCooldownTracker tracker)
        {
            var deck = new TacticalDeckManager();
            deck.InitializeDeck(new[] { definition }, 17);
            deck.BindCooldownTracker(tracker, 7);
            return deck;
        }

        private sealed class Gate : ITacticalTimeAdapter
        {
            public bool IsSimulationPaused { get; private set; }
            public void SetSimulationPaused(bool paused) => IsSimulationPaused = paused;
        }

        private sealed class Port : ITacticalExecutionPort
        {
            internal ExecutionObservation Observation = new ExecutionObservation(1, false, false, false, false, true);
            public bool TryPrepare(TacticalCardInstance card, out string issue) { issue = ""; return true; }
            public bool TryAdmit(TacticalCardInstance card, out ulong id, out string issue) { id = 1; issue = ""; return true; }
            public ExecutionObservation Observe(ulong id) => Observation;
            public bool TryAbortPreAction(ulong id, string reason) => true;
        }

        private static SettleSample Stable(long tick)
            => new SettleSample(tick, false, true, true, false, false, false, true, true, true, false, 0, 0, true);

        [Test]
        public void PhysicalAndPublishedCardsCannotCreateLegacyTimers()
        {
            var physical = new TacticalActionCardDefinition("physical", "Physical", "", 10,
                new CharacterActionId(302), 2f, sharedCooldownGroup: "old.group");
            var published = new TacticalActionCardDefinition("published", "Published", "", 25,
                "fixture.published", 1, "fixture.hash", 1, 6f);
            var tracker = new TacticalCooldownTracker();
            foreach (var card in new[] { physical, published })
            {
                tracker.TriggerCooldown(7, card, 5);
                Assert.IsFalse(tracker.IsOnCooldown(7, card, 6));
                Assert.AreEqual(0f, tracker.GetRemainingCooldownSeconds(7, card, 6));
            }
            Assert.AreEqual(0, tracker.CaptureSessionState().Entries.Length);
            Assert.AreEqual(10, physical.BaseMomentumCost);
            Assert.AreEqual(25, published.BaseMomentumCost);
        }

        [Test]
        public void PointMoveRetainsConfiguredDurationAndActorIsolation()
        {
            var point = Point();
            var tracker = new TacticalCooldownTracker();
            Assert.AreEqual(2f, point.CooldownSeconds);
            Assert.IsFalse(point.HasActionId);
            tracker.TriggerCooldown(7, point, 5);
            Assert.IsTrue(tracker.IsOnCooldown(7, point, 124));
            Assert.IsFalse(tracker.IsOnCooldown(7, point, 125));
            Assert.IsFalse(tracker.IsOnCooldown(8, point, 5));
            Assert.AreEqual(2f, tracker.GetRemainingCooldownSeconds(7, point, 5));
            tracker.ClearCooldown(7, point);
            Assert.IsFalse(tracker.IsOnCooldown(7, point, 5));
        }

        [Test]
        public void PublishedPointMoveRetainsExactIdentityAndConfiguredDuration()
        {
            var rules = Point().PointMove;
            var card = TacticalActionCardDefinition.CreatePublishedPointMove("published.point", "Point", "", 10,
                rules, 2f, "fixture.point", 3, "fixture.hash", 41, "fixture.icon");
            Assert.AreEqual(2f, card.CooldownSeconds);
            Assert.AreEqual("fixture.point", card.PublishedCardId);
            Assert.AreEqual(3, card.PublishedRevision);
            Assert.AreEqual("fixture.hash", card.PublishedContentHash);
            Assert.AreEqual(41ul, card.DeckEntryInstanceId);
        }

        [Test]
        public void ZeroMechanismDurationCreatesNoCooldown()
        {
            var tracker = new TacticalCooldownTracker();
            var point = Point(0);
            tracker.TriggerCooldown(7, point, 5);
            Assert.IsFalse(tracker.IsOnCooldown(7, point, 5));
            Assert.AreEqual(0, tracker.CaptureSessionState().Entries.Length);
        }

        [Test]
        public void PointFeeAndCooldownCommitOnceAtOriginalAcceptedFrame()
        {
            var tracker = new TacticalCooldownTracker();
            var point = Point();
            var deck = Deck(point, tracker);
            var ledger = new MomentumLedger(50);
            var time = new TacticalTimeCoordinator(new Gate());
            time.Initialize();
            var port = new Port();
            var driver = new TacticalSingleCardDriver(port, time, deck, ledger);
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));
            Assert.IsTrue(driver.TryCommit(3, out _));
            Assert.AreEqual(50, ledger.Current);
            Assert.AreEqual(10, ledger.Reserved);
            var beforeStart = Stable(4);
            driver.OnCompletedSimulationTick(in beforeStart);
            Assert.AreEqual(0, tracker.CaptureSessionState().Entries.Length);
            port.Observation = new ExecutionObservation(1, false, false, false, false, true, true, executionStartedTick: 5);
            var started = Stable(5);
            driver.OnCompletedSimulationTick(in started);
            Assert.AreEqual(40, ledger.Current);
            Assert.AreEqual(0, ledger.Reserved);
            Assert.AreEqual(10, deck.PaidCardMomentum);
            Assert.AreEqual(125, tracker.CaptureSessionState().Entries[0].End);
            var repeated = Stable(6);
            driver.OnCompletedSimulationTick(in repeated);
            driver.ReleaseControl();
            driver.ReleaseControl();
            Assert.AreEqual(40, ledger.Current);
            Assert.AreEqual(10, deck.PaidCardMomentum);
            Assert.AreEqual(125, tracker.CaptureSessionState().Entries[0].End);
        }

        [Test]
        public void PointFailureBeforeAcceptanceReleasesOnceWithoutPaymentOrCooldown()
        {
            var tracker = new TacticalCooldownTracker();
            var deck = Deck(Point(), tracker);
            var ledger = new MomentumLedger(50);
            var time = new TacticalTimeCoordinator(new Gate());
            time.Initialize();
            var port = new Port();
            var driver = new TacticalSingleCardDriver(port, time, deck, ledger);
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));
            Assert.IsTrue(driver.TryCommit(0, out _));
            port.Observation = new ExecutionObservation(1, false, false, true, true, true);
            bool settled = false;
            for (long tick = 1; tick <= 8; tick++) { var sample = Stable(tick); settled = driver.OnCompletedSimulationTick(in sample); }
            Assert.IsTrue(settled);
            Assert.AreEqual(50, ledger.Current);
            Assert.AreEqual(0, ledger.Reserved);
            Assert.AreEqual(0, deck.PaidCardMomentum);
            Assert.AreEqual(0, tracker.CaptureSessionState().Entries.Length);
            var again = Stable(9);
            Assert.IsFalse(driver.OnCompletedSimulationTick(in again));
            driver.ReleaseControl();
            Assert.AreEqual(50, ledger.Current);
        }

        [Test]
        public void OrdinaryPreStartNormalSettlementRefundsOnce()
        {
            var tracker = new TacticalCooldownTracker();
            var card = new TacticalActionCardDefinition("ordinary", "Ordinary", "", 10, new CharacterActionId(302), 2f);
            var deck = Deck(card, tracker);
            var ledger = new MomentumLedger(50);
            var time = new TacticalTimeCoordinator(new Gate());
            time.Initialize();
            var port = new Port();
            var driver = new TacticalSingleCardDriver(port, time, deck, ledger);
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));
            Assert.IsTrue(driver.TryCommit(0, out _));
            Assert.AreEqual(40, ledger.Current);
            port.Observation = new ExecutionObservation(1, false, false, true, true, true);
            bool settled = false;
            for (long tick = 1; tick <= 8; tick++) { var sample = Stable(tick); settled = driver.OnCompletedSimulationTick(in sample); }
            Assert.IsTrue(settled);
            Assert.AreEqual(50, ledger.Current);
            var again = Stable(9);
            Assert.IsFalse(driver.OnCompletedSimulationTick(in again));
            Assert.AreEqual(50, ledger.Current);
            Assert.AreEqual(0, tracker.CaptureSessionState().Entries.Length);
        }

        [Test]
        public void PauseDoesNotProcessAcceptedReceiptOrAlterBalance()
        {
            var tracker = new TacticalCooldownTracker();
            var deck = Deck(Point(), tracker);
            var ledger = new MomentumLedger(50);
            var time = new TacticalTimeCoordinator(new Gate());
            time.Initialize();
            var port = new Port();
            var driver = new TacticalSingleCardDriver(port, time, deck, ledger);
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));
            Assert.IsTrue(driver.TryCommit(3, out _));
            time.RequestPause(TacticalPauseReason.UserMenu);
            port.Observation = new ExecutionObservation(1, false, false, false, false, true, true, executionStartedTick: 5);
            var sample = Stable(5);
            Assert.IsFalse(driver.OnCompletedSimulationTick(in sample));
            Assert.AreEqual(50, ledger.Current);
            Assert.AreEqual(10, ledger.Reserved);
            Assert.AreEqual(0, tracker.CaptureSessionState().Entries.Length);
            time.RequestResume(TacticalPauseReason.UserMenu);
            driver.OnCompletedSimulationTick(in sample);
            Assert.AreEqual(40, ledger.Current);
            Assert.AreEqual(125, tracker.CaptureSessionState().Entries[0].End);
        }

        [Test]
        public void MechanismSnapshotRestoresItsExactDeadline()
        {
            var tracker = new TacticalCooldownTracker();
            var point = Point();
            tracker.TriggerCooldown(7, point, 5);
            var restored = TacticalCooldownTracker.RestoreSessionState(tracker.CaptureSessionState(), new[] { point }, 7, 5);
            Assert.IsTrue(restored.IsOnCooldown(7, point, 124));
            Assert.IsFalse(restored.IsOnCooldown(7, point, 125));
        }

        [Test]
        public void PhysicalCardCooldownSnapshotIsRejected()
        {
            var physical = new TacticalActionCardDefinition("physical", "Physical", "", 10, new CharacterActionId(302), 2f);
            var invalid = new CardCooldownSnapshot { TickRate = 60, Entries = new[] { new CardCooldownEntry { Character = 7, Key = "physical", End = 125 } } };
            Assert.Throws<InvalidOperationException>(() => TacticalCooldownTracker.RestoreSessionState(invalid, new[] { physical }, 7, 5));
        }

        [Test]
        public void SharedActionAbilityCooldownStaysIndependentAcrossActors()
        {
            var definitions = new AbilityDefinitionRegistry();
            var ability = new AbilityDefinitionConfig(71001, "fixture.action", "Fixture", null,
                new AbilityCooldownConfig(60, 0), Array.Empty<AbilityCostConfig>(), 0,
                new AbilityActionBindingConfig(new CharacterActionId(302), 1), default, 0, 0);
            Assert.IsTrue(definitions.TryRegister(ability, out _));
            var runtime = new AbilityRuntimeModule(definitions);
            Assert.IsTrue(runtime.TryCommitActivation(1, ability, new RuntimeFrame(5), out _));
            Assert.IsFalse(runtime.TryEvaluate(1, ability, new RuntimeFrame(6), out var blocked));
            Assert.AreEqual(AbilityActivationReason.OnCooldown, blocked.ReasonCode);
            Assert.AreEqual(59, blocked.RemainingCooldownFrames);
            Assert.IsTrue(runtime.TryEvaluate(2, ability, new RuntimeFrame(6), out _));
            Assert.IsTrue(runtime.TryEvaluate(1, ability, new RuntimeFrame(65), out _));
        }

        [Test]
        public void CombinedBudgetCreditsOnlyTheExistingReservationAndQueryDoesNotSpend()
        {
            var world = new GameplayComponentWorld();
            var actor = world.CreateEntity();
            var ledger = new MomentumLedger(40);
            Assert.IsTrue(ledger.TryReserve(10, out var token));
            var costs = new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion, 25) };
            Assert.IsFalse(CharacterTestAbilityCosts.CanAfford(world, actor, ledger, costs, 10, out _));
            Assert.IsTrue(CharacterTestAbilityCosts.CanAfford(world, actor, ledger, costs, 10, out _, 10));
            Assert.IsFalse(CharacterTestAbilityCosts.CanAfford(world, actor, ledger, costs, 10, out _, 11));
            Assert.AreEqual(40, ledger.Current);
            Assert.AreEqual(10, ledger.Reserved);
            Assert.IsTrue(CharacterTestAbilityCosts.TryCommit(world, actor, ledger, costs, out _));
            Assert.AreEqual(15, ledger.Current);
            Assert.AreEqual(10, ledger.Reserved);
            Assert.IsTrue(ledger.CommitReservation(token));
            Assert.AreEqual(5, ledger.Current);
        }

        [Test]
        public void ActualActionCannotSpendAnotherPendingCardsReservedMotion()
        {
            var world = new GameplayComponentWorld();
            var actor = world.CreateEntity();
            var ledger = new MomentumLedger(40);
            Assert.IsTrue(ledger.TryReserve(10, out var token));
            var costs = new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion, 31) };
            Assert.IsFalse(CharacterTestAbilityCosts.TryCommit(world, actor, ledger, costs, out var missing));
            Assert.AreEqual(CharacterTestCharacterAttributeIds.Motion, missing);
            Assert.AreEqual(40, ledger.Current);
            Assert.AreEqual(10, ledger.Reserved);
            Assert.IsTrue(ledger.ReleaseReservation(token));
        }

        [Test]
        public void MissingAttributeFundsRejectBeforeAnyMotionWrite()
        {
            var world = new GameplayComponentWorld();
            var actor = world.CreateEntity();
            var store = world.GetOrCreateStore<GameplayAttributeSetComponent>();
            store.Set(actor, new GameplayAttributeSetComponent(new GameplayAttributeValue(CharacterTestCharacterAttributeIds.Hp, 20, 20)));
            var ledger = new MomentumLedger(50);
            var costs = new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion, 20),
                new AbilityCostConfig(CharacterTestCharacterAttributeIds.Hp, 30) };
            Assert.IsFalse(CharacterTestAbilityCosts.TryCommit(world, actor, ledger, costs, out var missing));
            Assert.AreEqual(CharacterTestCharacterAttributeIds.Hp, missing);
            Assert.AreEqual(50, ledger.Current);
            Assert.IsTrue(store.TryGet(actor, out var attributes));
            Assert.IsTrue(attributes.TryGet(CharacterTestCharacterAttributeIds.Hp, out var hp));
            Assert.AreEqual(20, hp.CurrentValue);
        }

        [Test]
        public void RepeatedMotionCostsSumAndOverflowRejectsWithoutSpending()
        {
            var world = new GameplayComponentWorld();
            var actor = world.CreateEntity();
            var ledger = new MomentumLedger(50);
            var costs = new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion, 12),
                new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion, 13) };
            Assert.IsTrue(CharacterTestAbilityCosts.TryGetMotionCost(costs, out var total));
            Assert.AreEqual(25, total);
            Assert.IsTrue(CharacterTestAbilityCosts.TryCommit(world, actor, ledger, costs, out _));
            Assert.AreEqual(25, ledger.Current);
            var overflow = new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion, int.MaxValue),
                new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion, 1) };
            Assert.IsFalse(CharacterTestAbilityCosts.TryGetMotionCost(overflow, out _));
            Assert.IsFalse(CharacterTestAbilityCosts.TryCommit(world, actor, ledger, overflow, out _));
            Assert.AreEqual(25, ledger.Current);
        }

        [Test]
        public void StaminaCostRemainsAnAttributeAlongsideMotionCost()
        {
            const int staminaId = 920002;
            var world = new GameplayComponentWorld();
            var actor = world.CreateEntity();
            var store = world.GetOrCreateStore<GameplayAttributeSetComponent>();
            store.Set(actor, new GameplayAttributeSetComponent(new GameplayAttributeValue(staminaId, 20, 20)));
            var ledger = new MomentumLedger(50);
            var costs = new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion, 20), new AbilityCostConfig(staminaId, 15) };
            Assert.IsTrue(CharacterTestAbilityCosts.TryCommit(world, actor, ledger, costs, out _));
            Assert.AreEqual(30, ledger.Current);
            Assert.IsTrue(store.TryGet(actor, out var attributes));
            Assert.IsTrue(attributes.TryGet(staminaId, out var stamina));
            Assert.AreEqual(5, stamina.CurrentValue);
            Assert.AreEqual(20, stamina.BaseValue);
        }

        [Test]
        public void PreviousActorGenerationCannotSpendCurrentLedger()
        {
            var world = new GameplayComponentWorld();
            var actor = world.CreateEntity();
            Assert.IsTrue(world.DestroyEntity(actor));
            var current = world.CreateEntity();
            Assert.IsFalse(world.IsAlive(actor));
            Assert.IsTrue(world.IsAlive(current));
            var ledger = new MomentumLedger(50);
            var costs = new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion, 20) };
            Assert.IsFalse(CharacterTestAbilityCosts.TryCommit(world, actor, ledger, costs, out _));
            Assert.AreEqual(50, ledger.Current);
        }
    }
}
