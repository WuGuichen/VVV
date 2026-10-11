using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Tests.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Isolated prospective identity checks. No fixture promotes itself into a production payment owner.</summary>
    public sealed class TacticalFixedCardCommitManifestTests
    {
        private static readonly GameplayEntityId Actor = new GameplayEntityId(1, 1);
        private sealed class DetachedTime : ITacticalTimeAdapter
        {
            public bool IsSimulationPaused { get; private set; }
            public void SetSimulationPaused(bool value) => IsSimulationPaused = value;
        }
        private static TacticalCardInstance Card(out TacticalFixedAssemblyManifest assembly)
        {
            var definition = TacticalFixedAssemblyManifestTests.Main();
            Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(TacticalFixedAssemblyManifestTests.Snapshot(), definition,
                11, 7, 3, new Dictionary<string, int> { { "main.action", 17 } }, out assembly, out _));
            definition.AttachFixedAssembly(assembly);
            var card = new TacticalCardInstance(501, definition);
            card.MoveTo(CardZone.Hand); card.MoveTo(CardZone.PendingAction); return card;
        }
        private static TacticalFixedCardCommitManifest Prepare(ulong commitId = 1, ulong firstActivation = 1)
        {
            var card = Card(out var assembly);
            Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(assembly, TacticalFixedCardPaymentTiming.CardAndAccessoriesThenActualActions,
                7, out var fees));
            Assert.IsTrue(TacticalFixedCardCommitManifest.TryPrepare(assembly, fees, card, 7, 2, Actor, commitId,
                commitId, 10, 0, firstActivation, 30, default, out var prepared)); return prepared;
        }
        private static TacticalAccessoryAuthoritySnapshot Snapshot(TacticalFixedCardCommitManifest commit)
        {
            var result = new TacticalAccessoryAuthoritySnapshot { Activations = new TacticalAccessoryActivationData[commit.ActivationCount] };
            for (int i = 0; i < commit.ActivationCount; i++) result.Activations[i] = TacticalAccessoryActivationData.From(commit.ActivationAt(i));
            return result;
        }
        private static long Hash(TacticalFixedAccessoryAdmissionAuthority authority)
        { var hash = new RuntimeHashAccumulator(1, 1); authority.AddHash(ref hash); return hash.ToHash(); }

        [TestCase(1, 12, 0)]
        [TestCase(2, 5, 7)]
        public void ExplicitTimingComputesSameFullAdmissionBudget(int timing, int confirmation, int reserved)
        {
            Card(out var assembly);
            Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(assembly, (TacticalFixedCardPaymentTiming)timing, 7, out var plan));
            Assert.AreEqual(12, plan.MaximumAdmissionCost); Assert.AreEqual(confirmation, plan.ConfirmationCost);
            Assert.AreEqual(reserved, plan.ReservedAfterConfirmation);
        }
        [Test] public void MissingTimingNegativeBudgetAndOverflowRejectWithoutPartialPlan()
        {
            Card(out var assembly);
            foreach (var request in new[] { Tuple.Create(TacticalFixedCardPaymentTiming.Unspecified, 7),
                Tuple.Create(TacticalFixedCardPaymentTiming.AllAtConfirmation, -1),
                Tuple.Create(TacticalFixedCardPaymentTiming.AllAtConfirmation, int.MaxValue) })
            {
                Assert.IsFalse(TacticalFixedCardFeePlan.TryCreate(assembly, request.Item1, request.Item2, out var plan)); Assert.IsNull(plan);
            }
        }
        [Test] public void PreparationKeepsHeldAndBattleIdentitiesAndDoesNotPublishEffects()
        {
            var prepared = Prepare(); var owner = new TacticalInteractionEffectAuthority(); owner.Bind(7, 2, Actor);
            Assert.AreEqual(501, prepared.BattleCardInstance); Assert.AreEqual(40, prepared.ActivationAt(0).Identity.MainCardInstance);
            Assert.AreEqual(81, prepared.ActivationAt(0).Identity.AccessoryItemInstanceId);
            Assert.AreEqual(2, prepared.CardVersion); Assert.IsFalse(prepared.IsConfirmed);
            Assert.IsTrue(TacticalFixedAccessoryAdmissionAuthority.TryCreate(new[] { prepared }, false, out var authority));
            var states = new[] { prepared.ActivationAt(0), prepared.ActivationAt(1) };
            Assert.IsTrue(owner.TryPrepareAccessoryBatch(states, authority, out _));
            Assert.AreEqual(0, owner.AccessoryActivationCount);
        }
        [Test] public void StaleInputOrWrongZoneCannotPrepareCommit()
        {
            var card = Card(out var assembly);
            Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(assembly, TacticalFixedCardPaymentTiming.AllAtConfirmation, 7, out var fees));
            var stale = new TacticalExecutionInput(new TacticalExecutionInputBinding(7, 2, card.InstanceId, card.Version - 1, Actor),
                TacticalExecutionInputSource.Manual, new GameplayEntityId(2, 1));
            Assert.IsFalse(TacticalFixedCardCommitManifest.TryPrepare(assembly, fees, card, 7, 2, Actor, 1, 1, 10, 0, 1, 30, stale, out _));
            card.MoveTo(CardZone.Hand);
            Assert.IsFalse(TacticalFixedCardCommitManifest.TryPrepare(assembly, fees, card, 7, 2, Actor, 1, 1, 10, 0, 1, 30, default, out _));
        }
        [Test] public void UnconfirmedViewCanValidateProspectiveBatchButCannotRestore()
        {
            var prepared = Prepare();
            Assert.IsFalse(TacticalFixedAccessoryAdmissionAuthority.TryCreate(new[] { prepared }, true, out _));
            Assert.IsTrue(TacticalFixedAccessoryAdmissionAuthority.TryCreate(new[] { prepared }, false, out var authority));
            Assert.IsTrue(authority.ValidateActivation(prepared.ActivationAt(0)));
            Assert.IsFalse(authority.ValidateRestoration(Snapshot(prepared), 7, 2, Actor));
        }
        [Test] public void ExhaustedCardVersionRejectsBeforeFutureZoneChangeWrapsIdentity()
        {
            var ordinary = Card(out var assembly); var definitions = new[] { ordinary.Definition };
            var card = TacticalCardInstance.RestoreSessionState(new CardSnapshot { Instance = ordinary.InstanceId,
                Definition = 0, Zone = CardZone.PendingAction, Version = uint.MaxValue }, definitions, CardZone.PendingAction);
            Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(assembly, TacticalFixedCardPaymentTiming.AllAtConfirmation, 7, out var fees));
            Assert.IsFalse(TacticalFixedCardCommitManifest.TryPrepare(assembly, fees, card, 7, 2, Actor, 1, 1, 10, 0, 1, 30, default, out _));
        }
        [Test] public void PausedConfigurationAndReadoutUseFrozenFingerprintAndSameExplicitFeePlan()
        {
            var card = Card(out var assembly);
            var config = CharacterTestPausedSessionCodec.CaptureDefinitionConfiguration(card.Definition);
            Assert.AreEqual(assembly.Fingerprint, (string)config["FixedAssembly"]["Fingerprint"]);
            Assert.IsTrue(TacticalFixedAssemblyReadout.TryDescribe(card.Definition, null, out var summary, out var missing));
            StringAssert.Contains("2/2", summary); StringAssert.Contains("尚未核验", missing);
            Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(assembly, TacticalFixedCardPaymentTiming.CardAndAccessoriesThenActualActions, 7, out var fees));
            Assert.IsTrue(TacticalFixedAssemblyReadout.TryDescribe(card.Definition, fees, out _, out var detail));
            StringAssert.Contains("整牌准入需动势 12", detail); StringAssert.Contains("确认支付 5", detail);
            StringAssert.Contains("行动预留 7", detail); StringAssert.Contains("主行动结束不清除", detail);
            Card(out var other);
            Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(other, TacticalFixedCardPaymentTiming.AllAtConfirmation, 7, out var wrong));
            Assert.IsFalse(TacticalFixedAssemblyReadout.TryDescribe(card.Definition, wrong, out _, out _));
        }
        [TestCase(false)] [TestCase(true)]
        public void AllRealAdapterEntryPointsRejectUnconnectedFixedPointAssemblyAndKeepBareActionPath(bool traction)
        {
            // The existing fixture owns a detached simulation and its Library metrics cache. It never replaces a Scene.
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var adapter = new CharacterTestTacticalExecutionAdapter(fixture.Runtime, fixture.Commands, fixture.Card,
                    fixture.Definition, fixture.Source, new DetachedTime());
                adapter.SetContext(fixture.Facts, new RuntimeFrame(fixture.NextFrame));
                var rules = new TacticalPointMoveRules(5000, 200, 60, 1000, true,
                    traction ? TacticalPointMoveStartPolicy.OwnedTractionAccepted : TacticalPointMoveStartPolicy.OwnedMoveAccepted,
                    traction ? 4000 : 0, traction ? 10000 : 0);
                var definition = TacticalActionCardDefinition.CreatePublishedPointMove("fixture.main", "Fixture point", "", 3,
                    rules, 0, "fixture.main", 2, "main-exact", 90, "fixture.icon");
                Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(TacticalFixedAssemblyManifestTests.Snapshot(), definition,
                    11, 7, 3, new Dictionary<string, int>(), out var assembly, out _));
                definition.AttachFixedAssembly(assembly);
                var deck = new TacticalDeckManager(); deck.InitializeDeck(new[] { definition }, 17);
                var ledger = new MomentumLedger(50);
                Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(deck.Hand[0], ledger));
                var pending = deck.PendingAction;
                Assert.IsFalse(adapter.EvaluateCardAvailability(pending).IsLegal);
                Assert.IsFalse(adapter.TryPrepare(pending, out var issue));
                Assert.AreEqual("execution:FixedAssemblyJointCommitUnavailable", issue);
                Assert.IsFalse(adapter.TryAdmit(pending, out var execution, out issue)); Assert.AreEqual(0, execution);
                Assert.AreEqual("execution:FixedAssemblyJointCommitUnavailable", issue);
                Assert.IsFalse(adapter.TryPrepare(pending, default(TacticalExecutionInput), out issue));
                Assert.AreEqual("execution:FixedAssemblyJointCommitUnavailable", issue);
                Assert.IsFalse(adapter.TryAdmit(pending, default(TacticalExecutionInput), out execution, out issue));
                Assert.AreEqual(0, execution); Assert.AreEqual("execution:FixedAssemblyJointCommitUnavailable", issue);
                Assert.AreEqual(50, ledger.Current); Assert.AreEqual(3, ledger.Reserved); Assert.AreSame(pending, deck.PendingAction);
                Assert.IsFalse(fixture.Card.IsExecuting); Assert.AreEqual(0, fixture.Source.InteractionEffects.AccessoryActivationCount);

                var bare = new TacticalActionCardDefinition("fixture.bare", "Bare", "", 1, fixture.Probe.Action.ActionId);
                var bareDeck = new TacticalDeckManager(); bareDeck.InitializeDeck(new[] { bare }, 17);
                Assert.AreEqual(CardPlayFailure.None, bareDeck.SetPendingAction(bareDeck.Hand[0], new MomentumLedger(50)));
                Assert.IsTrue(adapter.TryPrepare(bareDeck.PendingAction, out issue), issue);
                Assert.IsTrue(adapter.TryAdmit(bareDeck.PendingAction, out execution, out issue), issue);
                Assert.Greater(execution, 0);
            }
        }
        [Test] public void PromotionRequiresExactTokenAndFeeAndProducesImmutableDifferentDigest()
        {
            var prepared = Prepare();
            Assert.IsFalse(prepared.TryConfirmPayment(31, prepared.Fees.ConfirmationCost, out _));
            Assert.IsFalse(prepared.TryConfirmPayment(30, prepared.Fees.ConfirmationCost + 1, out _));
            Assert.IsTrue(prepared.TryConfirmPayment(30, prepared.Fees.ConfirmationCost, out var confirmed));
            Assert.IsFalse(prepared.IsConfirmed); Assert.IsTrue(confirmed.IsConfirmed); Assert.AreNotEqual(prepared.Digest, confirmed.Digest);
            Assert.IsFalse(confirmed.TryConfirmPayment(30, confirmed.Fees.ConfirmationCost, out _));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void RestorationRejectsOmissionNullReorderOrMutatedImmutableContract(int mutation)
        {
            var prepared = Prepare(); Assert.IsTrue(prepared.TryConfirmPayment(30, prepared.Fees.ConfirmationCost, out var confirmed));
            Assert.IsTrue(TacticalFixedAccessoryAdmissionAuthority.TryCreate(new[] { confirmed }, true, out var authority));
            var snapshot = Snapshot(confirmed); Assert.IsTrue(authority.ValidateRestoration(snapshot, 7, 2, Actor));
            switch (mutation) {
                case 0: snapshot.Activations = new[] { snapshot.Activations[0] }; break;
                case 1: snapshot.Activations[0] = null; break;
                case 2: Array.Reverse(snapshot.Activations); break;
                case 3: snapshot.Activations[0].MomentumAmount++; break;
                case 4: snapshot.Activations[0].AccessoryItemInstanceId++; break;
                case 5: snapshot.Activations[0].LastTurn--; break; }
            Assert.IsFalse(authority.ValidateRestoration(snapshot, 7, 2, Actor));
        }
        [Test] public void TrustedViewCopiesCallerListAndRejectsDuplicateOrNonMonotonicRecords()
        {
            var a = Prepare(); var b = Prepare(2, 3);
            Assert.IsTrue(a.TryConfirmPayment(30, a.Fees.ConfirmationCost, out var confirmedA));
            Assert.IsTrue(b.TryConfirmPayment(30, b.Fees.ConfirmationCost, out var confirmedB));
            var list = new List<TacticalFixedCardCommitManifest> { confirmedA, confirmedB };
            Assert.IsTrue(TacticalFixedAccessoryAdmissionAuthority.TryCreate(list, true, out var authority)); var before = Hash(authority);
            list.Clear(); Assert.AreEqual(before, Hash(authority));
            Assert.IsFalse(TacticalFixedAccessoryAdmissionAuthority.TryCreate(new[] { confirmedA, confirmedA }, true, out _));
            Assert.IsFalse(TacticalFixedAccessoryAdmissionAuthority.TryCreate(new[] { confirmedB, confirmedA }, true, out _));
            Assert.IsFalse(TacticalFixedAccessoryAdmissionAuthority.TryCreate(new[] { confirmedA, Prepare(2, 2) }, false, out _));
        }
    }
}
