using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.NoEngineTests.Determinism
{
    public sealed class TacticalDeterminismTests
    {
        [Test]
        public void SeedAndTurnInputs_MatchDeckOrderMomentumPhaseAndRandomAcrossReshuffles()
        {
            var first = new Fixture();
            var second = new Fixture();
            object initialRandom = Read(first.Deck, "_randomState");
            long frame = 0;
            FrameTrace.Equal(frame, first.Capture(), second.Capture(), "initialize seed=123");
            for (int turn = 0; turn < 24; turn++)
            {
                first.Select(turn); second.Select(turn);
                FrameTrace.Equal(frame, first.Capture(), second.Capture(), "select hand index=" + turn % 3);
                first.Commit(); second.Commit();
                FrameTrace.Equal(frame, first.Capture(), second.Capture(), "commit/start/terminal");
                for (int tick = 0; tick < 3; tick++)
                {
                    frame++;
                    bool a = first.Settle(frame);
                    bool b = second.Settle(frame);
                    Assert.AreEqual(a, b, "settle frame=" + frame);
                    Assert.AreEqual(tick == 2, a);
                    FrameTrace.Equal(frame, first.Capture(), second.Capture(), "settle");
                }
            }
            Assert.AreEqual(25UL, first.Time.CurrentTurnIndex);
            Assert.AreNotEqual(initialRandom, Read(first.Deck, "_randomState"), "Trace must exercise refill/reshuffle random draws.");
        }

        // Read-only test observation for state without a production capture API. No behavior hook or cloned shuffle.
        private static object Read(object owner, string field)
        {
            FieldInfo info = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, "Authority observation field changed: " + owner.GetType().Name + "." + field);
            return info.GetValue(owner);
        }

        private sealed class Fixture
        {
            internal readonly TacticalDeckManager Deck = new TacticalDeckManager();
            internal readonly MomentumLedger Ledger = new MomentumLedger(40);
            internal readonly TacticalTimeCoordinator Time;
            internal Fixture()
            {
                var definitions = new TacticalCardDefinition[7];
                for (int i = 0; i < definitions.Length; i++)
                    definitions[i] = new TacticalActionCardDefinition("action-" + i, "Action", "", 5,
                        TacticalActionSlot.PrimaryAttack, 0f, TacticalTargetMode.Enemy, TacticalApproachMode.OptimalRange);
                Deck.InitializeDeck(definitions, 123);
                Time = new TacticalTimeCoordinator(new Adapter(), new SettleDetector(new SettleDetectorConfig(requiredStableTicks: 3)));
                Time.Initialize();
            }
            internal void Select(int turn)
            {
                Ledger.Grant(5);
                Assert.AreEqual(CardPlayFailure.None, Deck.SetPendingAction(Deck.Hand[turn % Deck.Hand.Count], Ledger));
            }
            internal void Commit()
            {
                Assert.IsTrue(Time.TryCommitTurn(Deck, Ledger, out _));
                Time.NotifyActionStarted();
                Time.NotifyExecutionTerminal();
            }
            internal bool Settle(long frame)
            {
                var sample = new SettleSample(frame, true, true, true, false, false, false, true, true, true, false, 0f, 0f);
                return Time.EvaluateSettle(in sample, Deck);
            }
            internal RuntimeSnapshotValue[] Capture()
            {
                var values = new List<RuntimeSnapshotValue>
                {
                    FrameTrace.Value("momentum.current", Ledger.Current), FrameTrace.Value("momentum.reserved", Ledger.Reserved),
                    FrameTrace.Value("momentum.nextReservationId", Read(Ledger, "_nextReservationId")),
                    FrameTrace.Value("momentum.reservationId", Read(Ledger, "_reservationId")),
                    FrameTrace.Value("time.phase", Time.Phase), FrameTrace.Value("time.turnIndex", Time.CurrentTurnIndex),
                    FrameTrace.Value("time.paused", Time.IsWorldPaused), FrameTrace.Value("time.pauseReasons", Read(Time, "_pauseReasons")),
                    FrameTrace.Value("deck.randomState", Read(Deck, "_randomState")), FrameTrace.Value("deck.nextInstanceId", Read(Deck, "_nextInstanceId"))
                };
                AddZone(values, "hand", Deck.Hand); AddZone(values, "draw", Deck.DrawPile);
                AddZone(values, "discard", Deck.DiscardPile); AddZone(values, "exhaust", Deck.ExhaustPile);
                AddCard(values, "pending", Deck.PendingAction); AddCard(values, "resolving", Deck.ResolvingAction);
                return values.ToArray();
            }
            private static void AddZone(List<RuntimeSnapshotValue> values, string zone, IList<TacticalCardInstance> cards)
            {
                values.Add(FrameTrace.Value("deck." + zone + ".count", cards.Count));
                for (int i = 0; i < cards.Count; i++) AddCard(values, zone + "[" + i + "]", cards[i]);
            }
            private static void AddCard(List<RuntimeSnapshotValue> values, string prefix, TacticalCardInstance card)
            {
                prefix = "deck." + prefix;
                values.Add(FrameTrace.Value(prefix + ".id", card?.InstanceId ?? 0));
                if (card == null) return;
                values.Add(FrameTrace.Value(prefix + ".definition", card.Definition.Id));
                values.Add(FrameTrace.Value(prefix + ".zone", card.Zone));
                values.Add(FrameTrace.Value(prefix + ".version", card.Version));
                values.Add(FrameTrace.Value(prefix + ".upgrade", card.UpgradeLevel));
            }
        }

        private sealed class Adapter : ITacticalTimeAdapter
        {
            public bool IsSimulationPaused { get; private set; }
            public void SetSimulationPaused(bool paused) { IsSimulationPaused = paused; }
        }
    }
}
