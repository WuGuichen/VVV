using System;
using MxFramework.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalCardEngineTests
    {
        private static TacticalActionCardDefinition Action(int cost = 20,
            CardDestination destination = CardDestination.Discard)
        {
            return new TacticalActionCardDefinition("attack", "Attack", "", cost,
                TacticalActionSlot.PrimaryAttack, 0f,
                TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange,
                20, destination);
        }

        private static TacticalUtilityCardDefinition Utility(int cost = 0, int grant = 30,
            CardDestination destination = CardDestination.Discard)
        {
            return new TacticalUtilityCardDefinition("focus", "Focus", "", cost,
                new AddMomentumPayload(grant), destination);
        }

        private static TacticalCardInstance Find(TacticalDeckManager deck, TacticalCardKind kind)
        {
            for (int i = 0; i < deck.Hand.Count; i++)
                if (deck.Hand[i].Definition.Kind == kind) return deck.Hand[i];
            Assert.Fail("Expected card not found.");
            return null;
        }

        [Test]
        public void Initialize_DealsThreeUniqueInstances_AndReplaysSeed()
        {
            var action = Action();
            var definitions = new TacticalCardDefinition[] { action, action, action, Utility(), Action(0) };
            var first = new TacticalDeckManager();
            var second = new TacticalDeckManager();
            first.InitializeDeck(definitions, 123);
            second.InitializeDeck(definitions, 123);
            Assert.That(first.Hand.Count, Is.EqualTo(3));
            Assert.That(first.DrawPile.Count, Is.EqualTo(2));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(first.Hand[i].InstanceId, Is.EqualTo(second.Hand[i].InstanceId));
                Assert.That(first.Hand[i].Zone, Is.EqualTo(CardZone.Hand));
                for (int j = i + 1; j < 3; j++)
                    Assert.That(first.Hand[i].InstanceId, Is.Not.EqualTo(first.Hand[j].InstanceId));
            }
        }

        [Test]
        public void CompletedTurn_RetainsUnusedHandIdentityVersionAndOrder_RefillsToThree()
        {
            var deck = new TacticalDeckManager();
            var action = Action();
            deck.InitializeDeck(new[] { action, action, action, action, action }, 2);
            var retainedFirst = deck.Hand[0];
            var retainedSecond = deck.Hand[2];
            uint firstVersion = retainedFirst.Version;
            var played = deck.Hand[1];
            var ledger = new MomentumLedger(60);
            Assert.That(deck.SetPendingAction(played, ledger), Is.EqualTo(CardPlayFailure.None));
            Assert.That(deck.CommitTurn(ledger, out var committed), Is.EqualTo(CardPlayFailure.None));
            Assert.That(deck.Hand.Count, Is.EqualTo(2));
            Assert.That(committed.Zone, Is.EqualTo(CardZone.Resolving));
            Assert.That(deck.CompleteTurn(committed.InstanceId, committed.Version), Is.True);
            Assert.That(deck.Hand.Count, Is.EqualTo(3));
            Assert.That(deck.Hand[0], Is.SameAs(retainedFirst));
            Assert.That(deck.Hand[1], Is.SameAs(retainedSecond));
            Assert.That(retainedFirst.Version, Is.EqualTo(firstVersion));
            Assert.That(played.Zone, Is.EqualTo(CardZone.DiscardPile));
            Assert.That(ledger.Current, Is.EqualTo(40), "Completion must not fabricate a hit reward.");
        }

        [Test]
        public void PendingAction_ReservesWithoutSpending_CancelRestoresOrder()
        {
            var deck = new TacticalDeckManager();
            var action = Action(50);
            deck.InitializeDeck(new[] { action, action, action }, 1);
            var card = deck.Hand[1];
            var ledger = new MomentumLedger(60);
            Assert.That(deck.SetPendingAction(card, ledger), Is.EqualTo(CardPlayFailure.None));
            Assert.That(ledger.Current, Is.EqualTo(60));
            Assert.That(ledger.Reserved, Is.EqualTo(50));
            Assert.That(ledger.Spendable, Is.EqualTo(10));
            Assert.That(deck.CancelPendingAction(new MomentumLedger(60)), Is.EqualTo(CardPlayFailure.InvalidLedger));
            Assert.That(ledger.Reserved, Is.EqualTo(50));
            Assert.That(deck.CancelPendingAction(ledger), Is.EqualTo(CardPlayFailure.None));
            Assert.That(deck.Hand[1], Is.SameAs(card));
            Assert.That(ledger.Reserved, Is.Zero);
            Assert.That(ledger.Current, Is.EqualTo(60));
        }

        [Test]
        public void OneActionPerTurn_RejectsSecondSelectionAndRepeatedCommit()
        {
            var deck = new TacticalDeckManager();
            var action = Action(20);
            deck.InitializeDeck(new[] { action, action, action }, 1);
            var ledger = new MomentumLedger(100);
            var second = deck.Hand[1];
            Assert.That(deck.CanPlayCard(deck.Hand[0], ledger, true, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(CardPlayFailure.ActionAlreadyPending));
            Assert.That(deck.SetPendingAction(deck.Hand[0], ledger), Is.EqualTo(CardPlayFailure.None));
            Assert.That(deck.SetPendingAction(second, ledger), Is.EqualTo(CardPlayFailure.ActionAlreadyPending));
            Assert.That(deck.CommitTurn(ledger, out var committed), Is.EqualTo(CardPlayFailure.None));
            uint version = committed.Version;
            Assert.That(deck.CommitTurn(ledger, out var duplicate), Is.EqualTo(CardPlayFailure.WrongPhase));
            Assert.That(duplicate, Is.Null);
            Assert.That(deck.SetPendingAction(second, ledger), Is.EqualTo(CardPlayFailure.WrongPhase));
            Assert.That(ledger.Current, Is.EqualTo(80));
            Assert.That(deck.CompleteTurn(committed.InstanceId, version), Is.True);
            Assert.That(deck.CompleteTurn(committed.InstanceId, version), Is.False);
            Assert.That(deck.SetPendingAction(second, ledger), Is.EqualTo(CardPlayFailure.None));
        }

        [Test]
        public void Utility_UsesOnlySpendableMomentum_AndNeverRefillsMidTurn()
        {
            var deck = new TacticalDeckManager();
            deck.InitializeDeck(new TacticalCardDefinition[] { Action(50), Utility(15), Utility(0) }, 4);
            var ledger = new MomentumLedger(60);
            var action = Find(deck, TacticalCardKind.Action);
            Assert.That(deck.SetPendingAction(action, ledger), Is.EqualTo(CardPlayFailure.None));
            TacticalCardInstance costly = deck.Hand[0].Definition.BaseMomentumCost == 15 ? deck.Hand[0] : deck.Hand[1];
            TacticalCardInstance free = ReferenceEquals(costly, deck.Hand[0]) ? deck.Hand[1] : deck.Hand[0];
            Assert.That(deck.PlayUtilityCard(costly, ledger), Is.EqualTo(CardPlayFailure.InsufficientMomentum));
            Assert.That(deck.PlayUtilityCard(free, ledger), Is.EqualTo(CardPlayFailure.None));
            Assert.That(ledger.Current, Is.EqualTo(90));
            Assert.That(ledger.Reserved, Is.EqualTo(50));
            Assert.That(deck.PlayUtilityCard(costly, ledger), Is.EqualTo(CardPlayFailure.None));
            Assert.That(ledger.Current, Is.EqualTo(100));
            deck.ReplenishHand();
            Assert.That(deck.Hand.Count, Is.Zero);
            Assert.That(deck.CancelPendingAction(ledger), Is.EqualTo(CardPlayFailure.None));
            deck.ReplenishHand();
            Assert.That(deck.Hand.Count, Is.EqualTo(1));
            Assert.That(deck.PlayUtilityCard(free, ledger), Is.EqualTo(CardPlayFailure.CardNotInHand));
        }

        [Test]
        public void EmptyDrawPile_RecyclesDiscard_ExhaustNeverReturns()
        {
            var deck = new TacticalDeckManager();
            deck.InitializeDeck(new TacticalCardDefinition[] { Action(0), Utility(0, 1, CardDestination.Exhaust) }, 1);
            var ledger = new MomentumLedger();
            var utility = Find(deck, TacticalCardKind.Utility);
            var action = Find(deck, TacticalCardKind.Action);
            Assert.That(deck.PlayUtilityCard(utility, ledger), Is.EqualTo(CardPlayFailure.None));
            Assert.That(deck.SetPendingAction(action, ledger), Is.EqualTo(CardPlayFailure.None));
            Assert.That(deck.CommitTurn(ledger, out var committed), Is.EqualTo(CardPlayFailure.None));
            Assert.That(deck.CompleteTurn(committed.InstanceId, committed.Version), Is.True);
            Assert.That(deck.Hand.Count, Is.EqualTo(1));
            Assert.That(deck.Hand[0], Is.SameAs(action));
            Assert.That(deck.ExhaustPile[0], Is.SameAs(utility));
            Assert.That(utility.Zone, Is.EqualTo(CardZone.ExhaustPile));
            Assert.That(deck.DiscardPile.Count, Is.Zero);
        }

        [Test]
        public void StaleCompletion_CannotCompleteRecycledInstance()
        {
            var deck = new TacticalDeckManager();
            deck.InitializeDeck(new[] { Action(0) }, 1);
            var ledger = new MomentumLedger();
            var action = deck.Hand[0];
            deck.SetPendingAction(action, ledger);
            deck.CommitTurn(ledger, out _);
            uint oldVersion = action.Version;
            Assert.That(deck.CompleteTurn(action.InstanceId, oldVersion), Is.True);
            deck.SetPendingAction(action, ledger);
            deck.CommitTurn(ledger, out _);
            Assert.That(deck.CompleteTurn(action.InstanceId, oldVersion), Is.False);
            Assert.That(deck.ResolvingAction, Is.SameAs(action));
        }

        [Test]
        public void Ledger_ClampsBounds_ProtectsReservations_RejectsStaleTokens()
        {
            var ledger = new MomentumLedger(int.MaxValue);
            Assert.That(ledger.Current, Is.EqualTo(100));
            Assert.That(new MomentumLedger(-1).Current, Is.Zero);
            Assert.That(ledger.TryReserve(80, out ulong token), Is.True);
            Assert.That(ledger.TrySpend(21), Is.False);
            Assert.That(ledger.TrySpend(-1), Is.False);
            Assert.That(ledger.TrySpend(20), Is.True);
            ledger.Grant(int.MaxValue);
            ledger.Grant(int.MinValue);
            Assert.That(ledger.Current, Is.EqualTo(100));
            Assert.That(ledger.CommitReservation(token), Is.True);
            Assert.That(ledger.Current, Is.EqualTo(20));
            Assert.That(ledger.CommitReservation(token), Is.False);
            Assert.That(ledger.TryReserve(0, out ulong next), Is.True);
            Assert.That(ledger.TryReserve(0, out _), Is.False);
            Assert.That(ledger.ReleaseReservation(token), Is.False);
            Assert.That(ledger.ReleaseReservation(next), Is.True);
            Assert.That(ledger.TryReserve(-1, out _), Is.False);
            Assert.That(ledger.TryReserve(101, out _), Is.False);
        }

        [Test]
        public void InvalidInputs_DoNotMutateDeckOrSpend_AndUnsupportedStatusIsExplicit()
        {
            var deck = new TacticalDeckManager();
            var status = new TacticalUtilityCardDefinition("status", "Status", "", 10,
                new ApplyStatusPayload("guard", 1, TacticalEffectLifetime.NextActionStart));
            deck.InitializeDeck(new TacticalCardDefinition[] { Action(110), status }, 1);
            var ledger = new MomentumLedger(100);
            var action = Find(deck, TacticalCardKind.Action);
            Assert.That(deck.EvaluateCard(action, ledger).Failure, Is.EqualTo(CardPlayFailure.InsufficientMomentum));
            Assert.That(deck.EvaluateCard(action, ledger).EffectiveCost, Is.EqualTo(110));
            Assert.That(deck.PlayUtilityCard(Find(deck, TacticalCardKind.Utility), ledger), Is.EqualTo(CardPlayFailure.UnsupportedPayload));
            var foreign = new TacticalCardInstance(action.InstanceId, Action(0));
            Assert.That(deck.SetPendingAction(foreign, ledger), Is.EqualTo(CardPlayFailure.CardNotInHand));
            Assert.That(deck.CommitTurn(ledger, out _), Is.EqualTo(CardPlayFailure.NoPendingAction));
            Assert.Throws<ArgumentException>(() => deck.InitializeDeck(new TacticalCardDefinition[] { null }, 1));
            Assert.That(deck.Hand.Count, Is.EqualTo(2));
            Assert.That(ledger.Current, Is.EqualTo(100));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TacticalActionCardDefinition("bad", "Bad", "", 0,
                TacticalActionSlot.PrimaryAttack, 0f, (TacticalTargetMode)99, TacticalApproachMode.None));
        }

        [Test]
        public void EmptyDeckAndReinitialization_PreserveIdentitySafety()
        {
            var deck = new TacticalDeckManager();
            deck.InitializeDeck(Array.Empty<TacticalCardDefinition>(), 0);
            deck.ReplenishHand();
            Assert.That(deck.Hand.Count, Is.Zero);
            deck.InitializeDeck(new[] { Action(0) }, 1);
            var old = deck.Hand[0];
            var ledger = new MomentumLedger();
            deck.SetPendingAction(old, ledger);
            Assert.Throws<InvalidOperationException>(() => deck.InitializeDeck(new[] { Action() }, 1));
            deck.CancelPendingAction(ledger);
            deck.InitializeDeck(new[] { Action(0) }, 1);
            Assert.That(deck.Hand[0].InstanceId, Is.Not.EqualTo(old.InstanceId));
            Assert.That(deck.SetPendingAction(old, ledger), Is.EqualTo(CardPlayFailure.CardNotInHand));
        }

        [Test]
        public void WarmTurnLoop_IncludingRecycleAndEvaluation_AllocatesZeroBytes()
        {
            var deck = new TacticalDeckManager();
            deck.InitializeDeck(new TacticalCardDefinition[] { Action(0), Utility(), Utility() }, 23);
            var ledger = new MomentumLedger();
            RunTurns(deck, ledger, 10);
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool success = RunTurns(deck, ledger, 1000);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(success, Is.True);
            Assert.That(allocated, Is.Zero);
        }

        private static bool RunTurns(TacticalDeckManager deck, MomentumLedger ledger, int count)
        {
            for (int turn = 0; turn < count; turn++)
            {
                TacticalCardInstance action = null;
                for (int i = 0; i < deck.Hand.Count; i++)
                    if (deck.Hand[i].Definition.Kind == TacticalCardKind.Action) action = deck.Hand[i];
                if (!deck.EvaluateCard(action, ledger).IsLegal) return false;
                if (deck.SetPendingAction(action, ledger) != CardPlayFailure.None) return false;
                if (deck.CancelPendingAction(ledger) != CardPlayFailure.None) return false;
                if (deck.SetPendingAction(action, ledger) != CardPlayFailure.None) return false;
                while (deck.Hand.Count > 0)
                    if (deck.PlayUtilityCard(deck.Hand[0], ledger) != CardPlayFailure.None) return false;
                if (deck.CommitTurn(ledger, out var committed) != CardPlayFailure.None) return false;
                if (!deck.CompleteTurn(committed.InstanceId, committed.Version)) return false;
            }
            return true;
        }
    }
}
