using System;
using System.Linq;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    // Offline join/content tests use the real compiled Deck, Ledger and effect authority. No engine world is simulated.
    public sealed class JointInteractionSessionSnapshotTests
    {
        internal static TacticalUtilityCardDefinition PublishedPreparationForFixture(int kind) => Fixture.PublishedPreparation(kind);
        private sealed class Fixture
        {
            internal readonly GameplayEntityId Player = new GameplayEntityId(1, 2), Enemy = new GameplayEntityId(2, 1);
            internal readonly TacticalCardDefinition[] Definitions;
            internal readonly TacticalDeckManager Deck = new TacticalDeckManager(6, true);
            internal readonly MomentumLedger Ledger = new MomentumLedger(50);
            internal readonly TacticalInteractionEffectAuthority Effects = new TacticalInteractionEffectAuthority();
            internal readonly CharacterTestPausedActorMap[] Actors;
            internal long Frame = 10;
            internal static TacticalUtilityCardDefinition PublishedPreparation(int kind, string id = null)
            {
                id = id ?? "joint-fixture-preparation-" + kind;
                var source = CharacterTestTacticalAuthoringTemplates.CreateInteractionPreparation((TacticalInteractionKind)kind);
                var published = CharacterTestTacticalPublishedSource.Read(new JObject { ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                    ["legacy"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(source)) });
                var metadata = new CharacterTacticalCardDefinition { CardId = id, Revision = 2, ContentHash = "123456789", DisplayName = "Fixture preparation",
                    Description = "Fixture", MotionCost = TacticalInteractionTrialCards.Cost, CooldownSeconds = 0f, PresentationSource = published };
                return TacticalUtilityCardDefinition.CreatePublishedPreparation(new CharacterTacticalCardReference {
                    CardId = id, Revision = 2, ContentHash = metadata.ContentHash, InstanceId = (ulong)kind }, metadata);
            }
            internal Fixture(bool arm = true, bool published = false)
            {
                Definitions = TacticalInteractionTrialCards.Ids.Select((id, index) => { if (published) return PublishedPreparation(index + 1);
                    Assert.IsTrue(TacticalInteractionTrialCards.TryCreate(id, out var card));
                    return (TacticalCardDefinition)card; }).Concat(new[] {new TacticalActionCardDefinition("joint-action", "Action", "", 20,
                        "bc_mme0my92jb", 1, "8924602348978526995", 6)}).ToArray();
                Deck.InitializeDeck(Definitions, 73, Definitions.Length); Effects.Bind(7, 1, Player);
                Actors = new[] {new CharacterTestPausedActorMap {Runtime = 2, Index = Player.Index, Generation = Player.Generation, Participant = 0},
                    new CharacterTestPausedActorMap {Runtime = 3, Index = Enemy.Index, Generation = Enemy.Generation, Participant = 1}};
                if (arm) for (int kind = 1; kind <= 5; kind++) Arm(kind);
            }
            internal void Arm(int kind)
            {
                var card = Deck.Hand.Single(c => ReferenceEquals(c.Definition, Definitions[kind - 1]));
                Assert.AreEqual(CardPlayFailure.None, Deck.SetPendingAction(card, Ledger)); uint version = card.Version;
                Assert.AreEqual(CardPlayFailure.None, Deck.CommitPendingTestPreparation(Ledger));
                Effects.ActivateValidated((TacticalInteractionKind)kind, Frame, Enemy, 100, 0, 300, card.InstanceId, version);
            }
            internal TacticalInteractionReceipt Receipt(int trace = 1) => new TacticalInteractionReceipt(7, 1, Frame, Player, Enemy, 99, trace);
            internal CharacterTestPausedSessionDocument Capture()
            {
                var d = new CharacterTestPausedSessionDocument {Scope = "lab", CompletedFrame = Frame, NextFrame = Frame + 1, TickRate = 60,
                    PlayerRuntime = 2, TargetRuntime = 3, Actors = Actors, World = "{}", Network = "{}", PhysicsStatic = "{}", PhysicsWorld = "{}",
                    Motion = new[] {new SessionMomentumSnapshot {Actor = Player.Index, Generation = Player.Generation, Ledger = Ledger.CaptureSessionState()}},
                    Deck = Deck.CaptureSessionState(Definitions), Stage = new TacticalStageSnapshot {Stage = 7, LastFrame = Frame, Player = Player.Index,
                        PlayerGeneration = Player.Generation, Opponent = Enemy.Index, OpponentGeneration = Enemy.Generation, Time = new TimeSnapshot {Turn = 1}},
                    Interactions = new[] {new CharacterTestPausedInteractionSnapshot {Runtime = 2, Actor = Player.Index, Generation = Player.Generation, State = Effects.Capture()},
                        new CharacterTestPausedInteractionSnapshot {Runtime = 3, Actor = Enemy.Index, Generation = Enemy.Generation, State = new TacticalInteractionSnapshot()}}};
                d.StateHash = CharacterTestPausedSessionCodec.ComputeContentHash(d); return d;
            }
            internal (TacticalDeckManager deck, MomentumLedger ledger, TacticalInteractionEffectAuthority effects) Restore(CharacterTestPausedSessionDocument d)
            {
                var ledger = new MomentumLedger(); ledger.RestoreSessionState(d.Motion[0].Ledger);
                var deck = new TacticalDeckManager(6, true); deck.RestoreSessionState(d.Deck, Definitions, ledger, () => d.NextFrame, d.CompletedFrame);
                CharacterTestPausedSessionCodec.ValidateInteractionJoins(d.Interactions, d.Actors, deck, d.Stage, d.CompletedFrame, d.PlayerRuntime, d.Motion[0].Ledger);
                var effects = new TacticalInteractionEffectAuthority();
                Assert.IsTrue(effects.RestoreJoined(d.Interactions[0].State, deck, 7, 1, d.CompletedFrame, Player));
                return (deck, ledger, effects);
            }
        }

        [Test] public void BeforeTriggerColdRestoreRetainsFivePaidSourcesAndRewardsExactlyOnce()
        {
            var f = new Fixture(); var d = f.Capture(); var cold = f.Restore(d);
            Assert.AreEqual(31, cold.effects.ActiveMask); Assert.AreEqual(25, cold.ledger.Current); Assert.AreEqual(25, cold.deck.PaidCardMomentum);
            Assert.AreNotSame(f.Effects, cold.effects); Assert.AreNotSame(f.Ledger, cold.ledger);
            f.Frame++; var hit = f.Receipt();
            Assert.AreEqual(8, cold.effects.CommitReceipt(hit, 1, true, false, false, cold.ledger));
            Assert.AreEqual(28, cold.ledger.Current); Assert.IsTrue(cold.effects.ReturnPending);
            Assert.AreEqual(0, cold.effects.CommitReceipt(hit, 1, true, false, false, cold.ledger));
            Assert.AreEqual(28, cold.ledger.Current); Assert.AreEqual(25, f.Ledger.Current);
        }
        [TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        public void PublishedUtilityRestoresExactPaidSourceAndRejectsChangedFixedEffect(int kind)
        {
            var f = new Fixture(false, true); f.Arm(kind); var d = f.Capture();
            var restored = f.Restore(d);
            Assert.AreEqual(1 << (kind - 1), restored.effects.ActiveMask);
            Assert.AreEqual(45, restored.ledger.Current); Assert.AreEqual(5, restored.deck.PaidCardMomentum);
            Assert.IsTrue(CharacterTestPausedSessionCodec.IsInteractionDefinition(f.Definitions[kind - 1]));
            var changed = f.Definitions.ToArray(); changed[kind - 1] = Fixture.PublishedPreparation(kind % 5 + 1, changed[kind - 1].Id);
            var wrongLedger = new MomentumLedger(); wrongLedger.RestoreSessionState(d.Motion[0].Ledger);
            var wrongDeck = new TacticalDeckManager(6, true); wrongDeck.RestoreSessionState(d.Deck, changed, wrongLedger, () => d.NextFrame, d.CompletedFrame);
            Assert.Throws<InvalidOperationException>(() => CharacterTestPausedSessionCodec.ValidateInteractionJoins(d.Interactions, d.Actors,
                wrongDeck, d.Stage, d.CompletedFrame, d.PlayerRuntime, d.Motion[0].Ledger));
            Assert.AreEqual(d.StateHash, f.Capture().StateHash); Assert.AreEqual(45, f.Ledger.Current);
        }
        [Test] public void CompleteDigestIncludesCommonUtilityPublicationIdentityAndInternalEffect()
        {
            var f = new Fixture(false, true); var definition = f.Definitions[0];
            var config = CharacterTestPausedSessionCodec.CaptureDefinitionConfiguration(definition);
            Assert.AreEqual(definition.PublishedCardId, (string)config["PublishedCardId"]);
            Assert.AreEqual(2, (int)config["PublishedRevision"]); Assert.AreEqual("123456789", (string)config["PublishedContentHash"]);
            Assert.AreEqual(1UL, (ulong)config["DeckEntryInstanceId"]); Assert.AreEqual(1, (int)config["InteractionEffect"]);
            var d = f.Capture(); d.Config = config.ToString(Formatting.None); var hash = CharacterTestPausedSessionCodec.ComputeContentHash(d);
            foreach (string field in new[] {"PublishedCardId", "PublishedRevision", "PublishedContentHash", "DeckEntryInstanceId", "InteractionEffect", "BaseMomentumCost"}) {
                var changed = (JObject)config.DeepClone(); changed[field] = changed[field].Type == JTokenType.String ? (JToken)new JValue("different") : new JValue((long)changed[field] + 1);
                d.Config = changed.ToString(Formatting.None); Assert.AreNotEqual(hash, CharacterTestPausedSessionCodec.ComputeContentHash(d), field);
            }
        }
        [Test] public void AfterTriggerRestoreKeepsDeduplicationAndDoesNotPayOrGrantAgain()
        {
            var f = new Fixture(); f.Frame++; var hit = f.Receipt();
            Assert.AreEqual(8, f.Effects.CommitReceipt(hit, 1, true, false, false, f.Ledger));
            var d = f.Capture(); var cold = f.Restore(d);
            Assert.AreEqual(28, cold.ledger.Current); Assert.AreEqual(25, cold.deck.PaidCardMomentum);
            Assert.AreEqual(0, cold.effects.CommitReceipt(hit, 1, true, false, false, cold.ledger));
            Assert.AreEqual(0, cold.effects.CommitReceipt(f.Receipt(2), 1, true, false, false, cold.ledger));
            Assert.AreEqual(28, cold.ledger.Current); Assert.AreEqual(2, cold.effects.Capture().Receipts.Length);
        }
        [Test] public void ReturnPendingRestoresCoordinatesAndConsumesOneReportedAttempt()
        {
            var f = new Fixture(); f.Frame++; f.Effects.CommitReceipt(f.Receipt(), 1, true, false, false, f.Ledger);
            var d = f.Capture(); var cold = f.Restore(d);
            Assert.IsTrue(cold.effects.TryGetReturnPoint(f.Frame, out var point)); Assert.AreEqual(100, point.X); Assert.AreEqual(0, point.Y); Assert.AreEqual(300, point.Z);
            Assert.AreEqual(TacticalReturnResult.WaitingForRelease, cold.effects.ReturnResult);
            cold.effects.CompleteReturnAttempt(true); cold.effects.CompleteReturnAttempt(false);
            Assert.AreEqual(TacticalReturnResult.Returned, cold.effects.ReturnResult); Assert.IsFalse(cold.effects.TryGetReturnPoint(f.Frame, out _));
            Assert.AreEqual(0, cold.effects.Capture().Slots[3].X); Assert.AreEqual(28, cold.ledger.Current);
        }
        [TestCase((int)TacticalReturnResult.Returned)][TestCase((int)TacticalReturnResult.DestinationBlocked)][TestCase((int)TacticalReturnResult.Expired)]
        public void TerminalReturnCannotRearmEvenWithValidPaidExhaustSource(int resultValue)
        {
            var f = new Fixture(); f.Frame++; f.Effects.CommitReceipt(f.Receipt(), 1, true, false, false, f.Ledger);
            if ((TacticalReturnResult)resultValue == TacticalReturnResult.Expired) {f.Frame += 600; f.Effects.Expire(f.Frame);}
            else f.Effects.CompleteReturnAttempt((TacticalReturnResult)resultValue == TacticalReturnResult.Returned);
            var d = f.Capture(); Assert.AreEqual((TacticalReturnResult)resultValue, d.Interactions[0].State.ReturnResult);
            f.Restore(d); // The complete legitimate paid/deck/ledger/terminal baseline must first be accepted.
            string live = d.StateHash; d.Interactions[0].State.Slots[3].Active = true;
            Assert.Throws<InvalidOperationException>(() => f.Restore(d));
            Assert.AreEqual(live, f.Capture().StateHash); Assert.AreEqual(28, f.Ledger.Current);
        }
        [TestCase("card-instance")][TestCase("source-version")][TestCase("actor-generation")][TestCase("target-generation")]
        [TestCase("wrong-turn")][TestCase("future-frame")][TestCase("duplicate-receipt")][TestCase("foreign-receipt")]
        [TestCase("unpaid")][TestCase("ledger-token")][TestCase("missing-actor")][TestCase("return-without-slot")]
        public void InvalidJoinedFragmentRejectsWithoutChangingLiveDeckLedgerOrEffects(string mode)
        {
            var f = new Fixture(); f.Frame++; f.Effects.CommitReceipt(f.Receipt(), 1, true, false, false, f.Ledger);
            var d = f.Capture(); string live = d.StateHash; var s = d.Interactions[0].State;
            if (mode == "card-instance") s.Slots[0].CardInstance = 999;
            if (mode == "source-version") s.Slots[0].SourceVersion++;
            if (mode == "actor-generation") d.Interactions[0].Generation++;
            if (mode == "target-generation") s.Slots[0].Target = new GameplayEntityId(2, 2);
            if (mode == "wrong-turn") s.Turn++;
            if (mode == "future-frame") s.Slots[0].ArmedFrame = f.Frame + 1;
            if (mode == "duplicate-receipt") s.Receipts = new[] {s.Receipts[0], s.Receipts[0]};
            if (mode == "foreign-receipt") s.Receipts[0].Target = new GameplayEntityId(3, 1);
            if (mode == "unpaid") d.Deck.Paid = 0;
            if (mode == "ledger-token") d.Motion[0].Ledger.NextToken = 0;
            if (mode == "missing-actor") d.Interactions = new[] {d.Interactions[0]};
            if (mode == "return-without-slot") {s.Slots[3] = default; s.NextActivation--; s.UsedMask &= ~8;}
            Assert.Throws<InvalidOperationException>(() => f.Restore(d));
            Assert.AreEqual(live, f.Capture().StateHash); Assert.AreEqual(28, f.Ledger.Current); Assert.AreEqual(25, f.Deck.PaidCardMomentum);
        }
        [Test] public void RewardAfterRestorePreservesRealPendingReservationAndToken()
        {
            var f = new Fixture(); Assert.AreEqual(CardPlayFailure.None, f.Deck.SetPendingAction(f.Deck.Hand.Single(), f.Ledger));
            var d = f.Capture(); var cold = f.Restore(d); ulong token = cold.ledger.CaptureSessionState().ActiveToken;
            Assert.AreEqual(20, cold.ledger.Reserved); Assert.Greater(token, 0);
            f.Frame++; cold.effects.CommitReceipt(f.Receipt(), 1, true, false, false, cold.ledger);
            Assert.AreEqual(28, cold.ledger.Current); Assert.AreEqual(20, cold.ledger.Reserved); Assert.AreEqual(token, cold.ledger.CaptureSessionState().ActiveToken);
        }
        [Test] public void CompleteContentHashCoversResourcesCardZonesReceiptsReturnAndAllocator()
        {
            var f = new Fixture(); var d = f.Capture(); string hash = d.StateHash;
            foreach (Action<CharacterTestPausedSessionDocument> edit in new Action<CharacterTestPausedSessionDocument>[] {
                x => x.Motion[0].Ledger.Current++, x => x.Motion[0].Ledger.Reserved++, x => x.Deck.Exhaust[0].Version++,
                x => x.Interactions[0].State.UsedMask ^= 1, x => x.Interactions[0].State.Slots[3].X++,
                x => x.Interactions[0].State.ReturnPending = true, x => x.Interactions[0].State.LastReceiptFrame++,
                x => x.Interactions[0].State.Receipts = new[] {new TacticalInteractionReceiptData(new TacticalInteractionReceipt(7, 1, 10, f.Player, f.Enemy, 99, 1))},
                x => x.NextRuntimeEntity++, x => x.NextCommandSequence++, x => x.Network = "{\"abilityCooldown\":1}",
                x => x.PhysicsWorld = "{\"position\":1}", x => x.World = "{\"allocator\":1}", x => x.Stage.Time.Turn++}) {
                var copy = JsonConvert.DeserializeObject<CharacterTestPausedSessionDocument>(JsonConvert.SerializeObject(d)); edit(copy);
                Assert.AreNotEqual(hash, CharacterTestPausedSessionCodec.ComputeContentHash(copy));
            }
            d.StateHash = "ignored-self-field"; Assert.AreEqual(hash, CharacterTestPausedSessionCodec.ComputeContentHash(d));
        }
        [Test] public void PropertyOrderAndProviderWhitespaceDoNotChangeContentDigest()
        {
            var f = new Fixture(); var d = f.Capture(); d.Network = "{\"z\":2,\"a\":1}"; string hash = CharacterTestPausedSessionCodec.ComputeContentHash(d);
            d.Network = "{ \"a\" : 1, \"z\" : 2 }"; Assert.AreEqual(hash, CharacterTestPausedSessionCodec.ComputeContentHash(d));
            d.Network = "{\"a\":1,\"a\":2}"; Assert.Throws<JsonReaderException>(() => CharacterTestPausedSessionCodec.ComputeContentHash(d));
        }
        [Test] public void PrivateEnvelopeRoundtripRequiresAllEffectFieldsAndGenerationProperties()
        {
            var f = new Fixture(); var d = f.Capture(); var read = CharacterTestPausedSessionCodec.Read(JsonConvert.SerializeObject(d));
            Assert.AreEqual(f.Player, read.Interactions[0].State.Actor); Assert.AreEqual(d.StateHash, CharacterTestPausedSessionCodec.ComputeContentHash(read));
            var root = JObject.FromObject(d); ((JObject)root["Interactions"][0]["State"]).Remove("ReturnPending");
            Assert.Throws<InvalidOperationException>(() => CharacterTestPausedSessionCodec.Read(root.ToString()));
        }
    }
}
