using System;
using System.Linq;
using System.Reflection;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    // Reuse the existing A1 fixture and its IDs/resources. This is the canonical tactical partition,
    // not an Engine adapter, full world restore, or actual staged Action execution test.
    public sealed class FeeSnapshotCompatibilityTests
    {
        private const string Unsupported = "snapshot:unsupported-instance-admission-binding";

        private static TacticalRestoredSession Fresh(int cardCost = 35, int actionCost = 20, int extraAttacks = 0)
        {
            var factory = typeof(TacticalSessionSnapshotTests).GetMethod("Fresh", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(factory);
            return (TacticalRestoredSession)factory.Invoke(null, new object[] { cardCost, extraAttacks, actionCost, false, null, 60L });
        }

        private static TacticalCardInstance Find(TacticalRestoredSession session, string id = "attack")
            => session.Deck.Hand.Single(card => card.Definition.Id == id);

        private static TacticalStageRejection Apply(TacticalRestoredSession session, TacticalStageCommandKind kind, TacticalCardInstance card = null)
        {
            card = card ?? session.Deck.PendingAction;
            var command = new TacticalStageCommand(session.Stage.StageRunId, session.Stage.TurnIndex,
                card?.InstanceId ?? 0, card?.Version ?? 0, session.Stage.SnapshotRevision, kind);
            return session.Stage.Apply(in command);
        }

        private static void BindInstanceGate(TacticalRestoredSession session)
        {
            Assert.IsTrue(session.Abilities.Definitions.TryGet(71001, out var owner));
            int actionMotion = owner.Costs.Sum(cost => cost.Amount);
            session.Deck.InstanceEvaluation = (card, credit) =>
            {
                if (!(card.Definition is TacticalActionCardDefinition))
                    return new CardPlayEvaluation(CardPlayFailure.None, card.Definition.BaseMomentumCost, session.Ledger.Spendable);
                int additional = card.Zone == CardZone.PendingAction ? 0 : card.Definition.BaseMomentumCost;
                return new CardPlayEvaluation(additional + actionMotion <= session.Ledger.Spendable + credit
                    ? CardPlayFailure.None : CardPlayFailure.InsufficientMomentum,
                    card.Definition.BaseMomentumCost + actionMotion, session.Ledger.Spendable);
            };
        }

        // Read the existing authorities independently: full Capture is intentionally unsupported
        // once the instance callback is bound. This check does not relax the capture contract.
        private static string StateBytes(TacticalRestoredSession session)
        {
            var ability = new MxFramework.Gameplay.AbilityRuntimeSaveStateProvider(session.Abilities).CaptureSaveState();
            Assert.IsTrue(ability.Success);
            return JsonConvert.SerializeObject(new
            {
                Motion = session.Context.ResourceActors.Select(actor => session.Ledgers[actor].CaptureSessionState()).ToArray(),
                Deck = session.Deck.CaptureSessionState(session.Context.Definitions),
                Stage = session.Stage.CaptureSessionState(),
                Controls = session.Controls.CaptureSessionState(),
                Commands = session.Commands.CaptureSnapshot(),
                Ability = ability.Value.ModuleStates.Single().CustomState.PayloadJson
            });
        }

        [TestCase(35, 20)]
        [TestCase(0, 0)]
        public void BoundInstanceGateRejectsCaptureAndCachedRestoreWithoutPublishing(int cardCost, int actionCost)
        {
            var source = Fresh(cardCost, actionCost);
            var cached = source.Capture();
            BindInstanceGate(source);
            var gate = source.Deck.InstanceEvaluation;
            var card = Find(source);
            var before = source.Stage.EvaluateHandCard(card);
            Assert.AreEqual(cardCost + actionCost <= 50, before.IsLegal);
            Assert.AreEqual(cardCost + actionCost, before.EffectiveCost);
            string bytes = StateBytes(source);

            Assert.AreEqual(Unsupported, Assert.Throws<InvalidOperationException>(() => source.Capture()).Message);
            var owner = new TacticalSessionSnapshotOwner(source);
            ulong lease = owner.Lease;
            Assert.IsFalse(owner.TryRestore(cached, source.Context, out string issue));
            Assert.AreEqual(Unsupported, issue);
            Assert.AreSame(source, owner.Current);
            Assert.AreEqual(lease, owner.Lease);
            Assert.AreSame(gate, source.Deck.InstanceEvaluation);
            Assert.AreEqual(bytes, StateBytes(source));
            var after = source.Stage.EvaluateHandCard(card);
            Assert.AreEqual(before.IsLegal, after.IsLegal);
            Assert.AreEqual(before.EffectiveCost, after.EffectiveCost);
            if (!after.IsLegal)
            {
                Assert.AreEqual(TacticalStageRejection.InsufficientMomentum, Apply(source, TacticalStageCommandKind.SelectAction, card));
                Assert.AreEqual(50, source.Ledger.Current);
                Assert.AreEqual(0, source.Ledger.Reserved);
                Assert.IsNull(source.Deck.PendingAction);
            }
            Console.WriteLine($"T1_RESULT captureRejected=true restoreRejected=true beforeLegal={before.IsLegal} afterLegal={after.IsLegal} cost={after.EffectiveCost} current={source.Ledger.Current} leasePreserved=true");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnsupportedSnapshotKeepsPendingTokenAndReplacementCreditInTheCurrentSession(bool restore)
        {
            var source = Fresh(30, 20, 1);
            // Make both action cards explicit fixture hand entries; a shuffled opening hand can
            // leave extra.0 in the reserve pile. Keep the existing definitions and deck owner.
            source.Deck.InitializeDeck(source.Context.Definitions.OrderBy(definition => definition is TacticalActionCardDefinition ? 0 : 1),
                42, configuredInitialHandCount: 3);
            Assert.AreEqual(TacticalStageRejection.None, Apply(source, TacticalStageCommandKind.SelectAction, Find(source)));
            var cached = source.Capture();
            BindInstanceGate(source);
            var pending = source.Deck.PendingAction;
            ulong token = source.Ledger.CaptureSessionState().ActiveToken;
            var replacement = Find(source, "extra.0");
            Assert.IsTrue(source.Deck.EvaluateAvailability(pending).IsLegal);
            Assert.IsFalse(source.Deck.EvaluateCard(replacement, source.Ledger).IsLegal);
            Assert.IsTrue(source.Stage.EvaluateHandCard(replacement).IsLegal);
            string bytes = StateBytes(source);
            var owner = new TacticalSessionSnapshotOwner(source);
            if (restore)
            {
                Assert.IsFalse(owner.TryRestore(cached, source.Context, out string issue));
                Assert.AreEqual(Unsupported, issue);
            }
            else
                Assert.AreEqual(Unsupported, Assert.Throws<InvalidOperationException>(() => source.Capture()).Message);

            Assert.AreSame(source, owner.Current);
            Assert.AreEqual(1UL, owner.Lease);
            Assert.AreSame(pending, source.Deck.PendingAction);
            Assert.AreEqual(token, source.Ledger.CaptureSessionState().ActiveToken);
            Assert.AreEqual(bytes, StateBytes(source));
            Assert.IsTrue(source.Deck.EvaluateAvailability(pending).IsLegal);
            Assert.IsTrue(source.Stage.EvaluateHandCard(replacement).IsLegal);
            Assert.AreEqual(TacticalStageRejection.None, Apply(source, TacticalStageCommandKind.SelectAction, replacement));
            Assert.AreEqual(50, source.Ledger.Current);
            Assert.AreEqual(1, source.Ledger.Reserved);
            Assert.IsFalse(source.Ledger.ReleaseReservation(token));
            Assert.AreEqual(0, source.Abilities.ActiveCooldownCount);
        }

        [Test]
        public void UnsupportedSourceRejectsBeforeAnyDetachedFactoryRuns()
        {
            var source = Fresh();
            var cached = source.Capture();
            BindInstanceGate(source);
            var c = source.Context;
            int calls = 0;
            var context = new TacticalSessionRestoreContext(c.Scope, c.Profile, c.Battle, c.CatalogHash, c.Frame, c.TickRate,
                c.Actors, c.ResourceActors, c.Definitions, c.Abilities, c.StageRun, c.Opening,
                c.HandCapacity, c.TestPreparation, c.CommandCapacity,
                map => { calls++; return c.CreateDetachedCosts(map); },
                (map, ability) => { calls++; return c.CreateDetachedExecution(map, ability); },
                (map, ability, frame) => { calls++; return c.CreateDetachedEvaluation(map, ability, frame); },
                c.ValidateCommand, c.DeckCharacter, c.CardCooldownBound, c.CardCooldownTickRate);
            string bytes = StateBytes(source);
            var owner = new TacticalSessionSnapshotOwner(source);
            Assert.IsFalse(owner.TryRestore(cached, context, out string issue));
            Assert.AreEqual(Unsupported, issue);
            Assert.AreEqual(0, calls);
            Assert.AreSame(source, owner.Current);
            Assert.AreEqual(1UL, owner.Lease);
            Assert.AreEqual(bytes, StateBytes(source));
        }

        [Test]
        public void SupportedDefinitionRestoreQueriesAndCommitsOnlyItsDetachedResources()
        {
            var source = Fresh(30, 20);
            Assert.IsNull(source.Deck.InstanceEvaluation);
            Assert.IsTrue(source.Stage.EvaluateHandCard(Find(source)).IsLegal);
            var owner = new TacticalSessionSnapshotOwner(source);
            Assert.IsTrue(owner.TryRestore(source.Capture(), source.Context, out string issue), issue);
            var restored = owner.Current;
            Assert.AreNotSame(source.Ledger, restored.Ledger);
            Assert.AreNotSame(source.Abilities, restored.Abilities);
            Assert.AreNotSame(source.Deck.DefinitionEvaluation, restored.Deck.DefinitionEvaluation);
            Assert.IsNull(restored.Deck.InstanceEvaluation);
            Assert.IsTrue(source.Ledger.TrySpend(50));
            Assert.IsFalse(source.Stage.EvaluateHandCard(Find(source)).IsLegal);
            Assert.IsTrue(restored.Stage.EvaluateHandCard(Find(restored)).IsLegal);
            Assert.AreEqual(TacticalStageRejection.None, Apply(restored, TacticalStageCommandKind.SelectAction, Find(restored)));
            Assert.AreEqual(50, restored.Ledger.Current);
            Assert.AreEqual(30, restored.Ledger.Reserved);
            Assert.AreEqual(TacticalStageRejection.None, Apply(restored, TacticalStageCommandKind.ConfirmExecution));
            Assert.AreEqual(20, restored.Ledger.Current);
            Assert.IsTrue(restored.Abilities.Definitions.TryGet(71001, out var ability));
            Assert.IsTrue(restored.Abilities.TryCommitActivation(1, ability, new RuntimeFrame(3), out _));
            Assert.AreEqual(0, restored.Ledger.Current);
            Assert.AreEqual(60, restored.Abilities.GetRemainingCooldownFrames(1, ability, new RuntimeFrame(3)));
            Assert.AreEqual(0, source.Ledger.Current);
            Assert.AreEqual(0, source.Abilities.ActiveCooldownCount);
        }

        [Test]
        public void FailedSupportedRestoreKeepsQueryAndTheNextPaymentOnTheCurrentSession()
        {
            var source = Fresh(30, 20);
            var invalid = source.Capture();
            invalid.Motion[0].Ledger.Current = 101;
            var evaluation = source.Deck.DefinitionEvaluation;
            string bytes = StateBytes(source);
            var owner = new TacticalSessionSnapshotOwner(source);
            Assert.IsFalse(owner.TryRestore(invalid, source.Context, out _));
            Assert.AreSame(source, owner.Current);
            Assert.AreEqual(1UL, owner.Lease);
            Assert.AreSame(evaluation, source.Deck.DefinitionEvaluation);
            Assert.IsNull(source.Deck.InstanceEvaluation);
            Assert.AreEqual(bytes, StateBytes(source));
            Assert.IsTrue(source.Stage.EvaluateHandCard(Find(source)).IsLegal);
            Assert.AreEqual(TacticalStageRejection.None, Apply(source, TacticalStageCommandKind.SelectAction, Find(source)));
            Assert.AreEqual(TacticalStageRejection.None, Apply(source, TacticalStageCommandKind.ConfirmExecution));
            Assert.IsTrue(source.Abilities.Definitions.TryGet(71001, out var ability));
            Assert.IsTrue(source.Abilities.TryCommitActivation(1, ability, new RuntimeFrame(3), out _));
            Assert.AreEqual(0, source.Ledger.Current);
            Assert.AreEqual(30, source.Deck.PaidCardMomentum);
            Assert.AreEqual(60, source.Abilities.GetRemainingCooldownFrames(1, ability, new RuntimeFrame(3)));
        }
    }
}
