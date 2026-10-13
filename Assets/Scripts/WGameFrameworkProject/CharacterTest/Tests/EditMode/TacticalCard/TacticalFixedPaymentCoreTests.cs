using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Pure core fixtures over actual program/manifest/effect/ledger sources. No Editor, host or UI is run.</summary>
    public sealed class TacticalFixedPaymentCoreTests
    {
        private static readonly GameplayEntityId Actor = new GameplayEntityId(1, 1);
        internal static CharacterTestBehaviorExecutionEvent Event(int timeline, int frame, int rank,
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind kind, int parameter = -1, int condition = -1,
            int duration = 1, int maximum = 1, int cooldown = 0, bool shared = false,
            CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin clock = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline)
            => new CharacterTestBehaviorExecutionEvent(timeline, frame, duration, condition, parameter, maximum,
                cooldown, 0, rank, kind, CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant, clock, shared);
        internal static CharacterTestBehaviorExecutionProgram Program(CharacterTestBehaviorExecutionEvent[] events,
            CharacterTestBehaviorExecutionTimeline[] timelines = null, CharacterTestBehaviorExecutionFlow[] flows = null)
        {
            var order = new int[events.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            return new CharacterTestBehaviorExecutionProgram(0, -1, "pure.fixture", timelines ?? new[] {
                new CharacterTestBehaviorExecutionTimeline(0, events.Length, 4, -1) }, events, order,
                new[] { new CharacterTestBehaviorExecutionAction(17), new CharacterTestBehaviorExecutionAction(18), new CharacterTestBehaviorExecutionAction(19) },
                Array.Empty<CharacterTestBehaviorExecutionMovement>(), flows ?? Array.Empty<CharacterTestBehaviorExecutionFlow>(),
                new[] { new CharacterTestBehaviorExecutionCondition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Unknown, 0, 0, 0, -1, default) },
                new[] { new CharacterTestBehaviorExecutionPredicate(new CharacterTestRuntimeAiLabCardCondition()) }, Array.Empty<int>());
        }
        internal static CharacterTestBehaviorExecutionProgram OneAction()
            => Program(new[] { Event(0, 0, 0, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction, 0) });

        private sealed class Fixture
        {
            internal readonly TacticalCardInstance Card;
            internal readonly MomentumLedger Ledger;
            internal readonly TacticalInteractionEffectAuthority Effects = new TacticalInteractionEffectAuthority();
            internal readonly CharacterTestBehaviorExecutionProgram Program;
            internal readonly CharacterTestFixedActionBudgetProof Proof;
            internal TacticalFixedCardCommitManifest Prepared;
            internal Fixture(int current = 20, bool empty = false, int cost = 7)
            {
                var definition = TacticalFixedAssemblyManifestTests.Main();
                var snapshot = TacticalFixedAssemblyManifestTests.Snapshot();
                if (empty) { snapshot.Assembly.Accessories.Clear(); snapshot.Accessories = Array.Empty<CharacterTacticalAccessoryDefinition>(); }
                Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(snapshot, definition, 11, 7, 3,
                    new Dictionary<string, int> { { "main.action", 17 } }, out var assembly, out _));
                definition.AttachFixedAssembly(assembly);
                Card = new TacticalCardInstance(501, definition); Card.MoveTo(CardZone.Hand); Card.MoveTo(CardZone.PendingAction);
                Ledger = new MomentumLedger(current); Assert.IsTrue(Ledger.TryReserve(3, out ulong token));
                Program = cost == 0 ? TacticalFixedPaymentCoreTests.Program(Array.Empty<CharacterTestBehaviorExecutionEvent>()) : OneAction();
                Assert.IsTrue(CharacterTestFixedActionBudgetProof.TryCreate(Program, _ => cost, out Proof, out _));
                Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(assembly, TacticalFixedCardPaymentTiming.CardAndAccessoriesThenActualActions,
                    Proof.MaximumMomentum, out var fees));
                Effects.Bind(7, 2, Actor);
                Assert.IsTrue(TacticalFixedCardCommitManifest.TryPrepare(assembly, fees, Card, 7, 2, Actor, 1, 1, 10,
                    0, 1, token, default, out Prepared));
            }
            internal bool Prepare(out TacticalFixedCardCommitManifest.PreparedPaymentAndAccessories transaction, out string issue)
                => TacticalFixedCardCommitManifest.PreparedPaymentAndAccessories.TryPrepare(Prepared, Card, Ledger, Effects,
                    Program, Proof, out transaction, out issue);
        }

        [Test] public void ActualPaymentAndCompleteBatchPublishTogetherOnce()
        {
            var f = new Fixture();
            Assert.IsTrue(f.Prepare(out var transaction, out _));
            Assert.AreEqual(20, f.Ledger.Current); Assert.AreEqual(3, f.Ledger.Reserved);
            Assert.AreEqual(0, f.Effects.AccessoryActivationCount); Assert.IsNull(f.Effects.ConfirmedFixedAssembly);
            Assert.IsTrue(transaction.TryPublish(out var receipt));
            Assert.AreEqual(15, f.Ledger.Current); Assert.AreEqual(7, f.Ledger.Reserved);
            Assert.AreEqual(2, f.Effects.AccessoryActivationCount); Assert.IsTrue(receipt.Record.IsConfirmed);
            Assert.AreEqual(receipt.Record.Digest, f.Effects.ConfirmedFixedAssembly.CommitAt(0).Digest);
            Assert.IsFalse(transaction.TryPublish(out var duplicate)); Assert.IsNull(duplicate);
            Assert.AreEqual(15, f.Ledger.Current); Assert.AreEqual(2, f.Effects.AccessoryActivationCount);
            Assert.AreEqual(ulong.MaxValue, f.Effects.AccessoryAt(0).Identity.LastTurn);
        }
        [Test] public void ActualActionConsumesReservedBudgetAndUnusedBudgetReleaseDoesNotRefundCardFees()
        {
            var f = new Fixture(); Assert.IsTrue(f.Prepare(out var transaction, out _));
            Assert.IsTrue(transaction.TryPublish(out var receipt));
            Assert.IsFalse(receipt.TrySpendActualAction(999, 7)); Assert.IsFalse(receipt.TrySpendActualAction(17, 6));
            Assert.AreEqual(15, f.Ledger.Current); Assert.AreEqual(7, f.Ledger.Reserved);
            Assert.IsTrue(receipt.TrySpendActualAction(17, 7));
            Assert.AreEqual(8, f.Ledger.Current); Assert.AreEqual(0, f.Ledger.Reserved);
            Assert.IsTrue(receipt.HasReservation); Assert.IsFalse(receipt.TrySpendActualAction(17, 7));
            Assert.IsTrue(receipt.ReleaseUnexecutedBudget()); Assert.IsFalse(receipt.ReleaseUnexecutedBudget());
            Assert.AreEqual(8, f.Ledger.Current);
        }
        [Test] public void UnstartedExecutionReleasesBudgetOnlyAndKeepsIndependentEffects()
        {
            var f = new Fixture(); Assert.IsTrue(f.Prepare(out var transaction, out _));
            Assert.IsTrue(transaction.TryPublish(out var receipt)); Assert.IsTrue(receipt.ReleaseUnexecutedBudget());
            Assert.AreEqual(15, f.Ledger.Current); Assert.AreEqual(0, f.Ledger.Reserved);
            Assert.AreEqual(2, f.Effects.AccessoryActivationCount);
            f.Effects.AdvanceTurn(7, 99, Actor); Assert.AreEqual(TacticalAccessoryCombatStatus.Armed, f.Effects.AccessoryAt(0).Status);
        }
        [TestCase(true, 7, 17)] [TestCase(false, 0, 15)] [TestCase(true, 0, 17)]
        public void EmptySlotsAndZeroBudgetKeepCorrectConfirmationAndToken(bool empty, int cost, int remaining)
        {
            var f = new Fixture(empty: empty, cost: cost); Assert.IsTrue(f.Prepare(out var transaction, out _));
            Assert.IsTrue(transaction.TryPublish(out var receipt));
            Assert.AreEqual(remaining, f.Ledger.Current); Assert.AreEqual(cost, f.Ledger.Reserved);
            Assert.AreEqual(empty ? 0 : 2, f.Effects.AccessoryActivationCount);
            Assert.IsTrue(receipt.HasReservation); Assert.IsTrue(receipt.ReleaseUnexecutedBudget());
        }
        [Test] public void InsufficientTotalRejectsBeforeAnyFeeOrActivation()
        {
            var f = new Fixture(11); Assert.IsFalse(f.Prepare(out _, out string issue));
            Assert.AreEqual("fixed-payment:InsufficientOrChangedReservation", issue);
            Assert.AreEqual(11, f.Ledger.Current); Assert.AreEqual(3, f.Ledger.Reserved);
            Assert.IsNull(f.Effects.ConfirmedFixedAssembly); Assert.AreEqual(0, f.Effects.AccessoryActivationCount);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void StaleLedgerEffectOrCardRejectsBeforeFirstWrite(int mutation)
        {
            var f = new Fixture(); Assert.IsTrue(f.Prepare(out var transaction, out _));
            if (mutation == 0) f.Ledger.Grant(1);
            if (mutation == 1) f.Effects.AdvanceTurn(7, 3, Actor);
            if (mutation == 2) f.Card.MoveTo(CardZone.Hand);
            if (mutation == 3) f.Effects.Clear();
            int current = f.Ledger.Current, reserved = f.Ledger.Reserved;
            Assert.IsFalse(transaction.TryPublish(out var receipt)); Assert.IsNull(receipt);
            Assert.AreEqual(current, f.Ledger.Current); Assert.AreEqual(reserved, f.Ledger.Reserved);
            Assert.IsNull(f.Effects.ConfirmedFixedAssembly); Assert.AreEqual(0, f.Effects.AccessoryActivationCount);
        }
        [Test] public void ProofForDifferentProgramCannotPromotePreparedIdentity()
        {
            var f = new Fixture();
            Assert.IsFalse(TacticalFixedCardCommitManifest.PreparedPaymentAndAccessories.TryPrepare(f.Prepared, f.Card,
                f.Ledger, f.Effects, OneAction(), f.Proof, out _, out _));
            Assert.AreEqual(20, f.Ledger.Current); Assert.IsNull(f.Effects.ConfirmedFixedAssembly);
        }
        [Test] public void UndefinedArmedOverlapIsExplicitSupportGapBeforePayment()
        {
            var f = new Fixture(); Assert.IsTrue(f.Prepare(out var first, out _));
            Assert.IsTrue(first.TryPublish(out var receipt)); Assert.IsTrue(receipt.ReleaseUnexecutedBudget());
            Assert.IsTrue(f.Ledger.TryReserve(3, out var token)); f.Effects.AdvanceTurn(7, 3, Actor);
            Assert.IsTrue(TacticalFixedCardCommitManifest.TryPrepare(f.Prepared.Assembly, f.Prepared.Fees, f.Card,
                7, 3, Actor, 2, 2, 11, 0, 3, token, default, out f.Prepared));
            Assert.IsFalse(f.Prepare(out _, out string issue)); Assert.AreEqual("fixed-payment:OverlapPolicyUnspecified", issue);
            Assert.AreEqual(15, f.Ledger.Current); Assert.AreEqual(3, f.Ledger.Reserved);
            Assert.AreEqual(1, f.Effects.ConfirmedFixedAssembly.CommitCount); Assert.AreEqual(2, f.Effects.AccessoryActivationCount);
        }
        [Test] public void OldTokenCannotSpendOrReleaseANewReservation()
        {
            var f = new Fixture(); Assert.IsTrue(f.Prepare(out var first, out _)); Assert.IsTrue(first.TryPublish(out var receipt));
            Assert.IsTrue(receipt.ReleaseUnexecutedBudget()); Assert.IsTrue(f.Ledger.TryReserve(2, out var next));
            Assert.IsFalse(receipt.TrySpendActualAction(17, 7)); Assert.IsFalse(receipt.ReleaseUnexecutedBudget());
            Assert.AreEqual(2, f.Ledger.Reserved); Assert.IsTrue(f.Ledger.ReleaseReservation(next));
        }
        [Test] public void ReservedBudgetIsProtectedFromOrdinarySpending()
        {
            var f = new Fixture(); Assert.IsTrue(f.Prepare(out var transaction, out _)); Assert.IsTrue(transaction.TryPublish(out var receipt));
            Assert.IsFalse(f.Ledger.TrySpend(9)); Assert.IsTrue(f.Ledger.TrySpend(8));
            Assert.AreEqual(7, f.Ledger.Current); Assert.AreEqual(7, f.Ledger.Reserved);
            Assert.IsTrue(receipt.TrySpendActualAction(17, 7)); Assert.AreEqual(0, f.Ledger.Current);
            Assert.IsTrue(receipt.ReleaseUnexecutedBudget());
        }
        [Test] public void PreparedCorePublicationAllocatesNothing()
        {
            var warm = new Fixture(); Assert.IsTrue(warm.Prepare(out var first, out _)); Assert.IsTrue(first.TryPublish(out _));
            var f = new Fixture(); Assert.IsTrue(f.Prepare(out var transaction, out _));
            long before = GC.GetAllocatedBytesForCurrentThread(); bool success = transaction.TryPublish(out var receipt);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(success); Assert.IsNotNull(receipt); Assert.AreEqual(0, allocated);
        }
    }

    public sealed class TacticalFixedActionBudgetProofTests
    {
        private static CharacterTestBehaviorExecutionEvent Action(int timeline, int frame, int rank, int parameter,
            int duration = 1, int maximum = 1, int condition = -1, int cooldown = 0, bool shared = false,
            CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin clock = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline)
            => TacticalFixedPaymentCoreTests.Event(timeline, frame, rank, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                parameter, condition, duration, maximum, cooldown, shared, clock);
        private static void Budget(CharacterTestBehaviorExecutionProgram program, int expected)
        {
            Assert.IsTrue(CharacterTestFixedActionBudgetProof.TryCreate(program, action => action == 17 ? 10 : action == 18 ? 20 : 30,
                out var proof, out string issue), issue); Assert.AreEqual(expected, proof.MaximumMomentum);
        }
        [Test] public void PaidCommonPrefixPlusAlternativeTimelineUsesMaximum()
        {
            var program = TacticalFixedPaymentCoreTests.Program(new[] { Action(0, 0, 0, 0),
                TacticalFixedPaymentCoreTests.Event(0, 1, 1, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline, 0, 0),
                TacticalFixedPaymentCoreTests.Event(0, 1, 2, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline, 1, 0),
                Action(1, 0, 3, 1), Action(2, 0, 4, 2) }, new[] {
                new CharacterTestBehaviorExecutionTimeline(0, 3, 3, -1), new CharacterTestBehaviorExecutionTimeline(3, 1, 1, -1),
                new CharacterTestBehaviorExecutionTimeline(4, 1, 1, -1) }, new[] {
                new CharacterTestBehaviorExecutionFlow(1, 0), new CharacterTestBehaviorExecutionFlow(2, 0) });
            Budget(program, 40);
        }
        [Test] public void LastEligibleActionWinsRatherThanSummingSameFrameRows()
            => Budget(TacticalFixedPaymentCoreTests.Program(new[] { Action(0, 0, 0, 2), Action(0, 0, 1, 0) }), 10);
        [Test] public void ActionConditionDoesNotProveAnExclusion()
            => Budget(TacticalFixedPaymentCoreTests.Program(new[] { Action(0, 0, 0, 0, condition: 0) }), 10);
        [TestCase(0)] [TestCase(1)] [TestCase(10)]
        public void ScheduledInstantActionCountIsNotAWorldFrameRetryCap(int cap)
            => Budget(TacticalFixedPaymentCoreTests.Program(new[] { Action(0, 0, 0, 0, maximum: cap) }), 10);
        [TestCase(1, 20)] [TestCase(2, 30)] [TestCase(5, 60)]
        public void FiniteLoopQuotaIncludesFirstPass(int repeats, int expected)
        {
            var program = TacticalFixedPaymentCoreTests.Program(new[] { Action(0, 0, 0, 0),
                TacticalFixedPaymentCoreTests.Event(0, 0, 1, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion, 0, duration: 2) },
                new[] { new CharacterTestBehaviorExecutionTimeline(0, 2, 2, -1) }, new[] { new CharacterTestBehaviorExecutionFlow(-1, repeats) });
            Budget(program, expected);
        }
        [Test] public void OneFrameRewindDoesNotRefireTheSameScheduledClock()
        {
            var program = TacticalFixedPaymentCoreTests.Program(new[] { Action(0, 0, 0, 0),
                TacticalFixedPaymentCoreTests.Event(0, 0, 1, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion, 0) },
                new[] { new CharacterTestBehaviorExecutionTimeline(0, 2, 1, -1) }, new[] { new CharacterTestBehaviorExecutionFlow(-1, 5) });
            Budget(program, 10);
        }
        [Test] public void LoopDoesNotResetEventWhoseEndIsOutsideTheLoop()
        {
            var program = TacticalFixedPaymentCoreTests.Program(new[] { Action(0, 0, 0, 0), Action(0, 0, 1, 2, duration: 3),
                TacticalFixedPaymentCoreTests.Event(0, 0, 2, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion, 0, duration: 2) },
                new[] { new CharacterTestBehaviorExecutionTimeline(0, 3, 3, -1) }, new[] { new CharacterTestBehaviorExecutionFlow(-1, 2) });
            Budget(program, 50); // Last row pays 30 once; the reset earlier row pays 10 on each subsequent pass.
        }
        [Test] public void UnconditionalJumpSuppressesSameFrameActionAndLaterJumps()
        {
            var program = TacticalFixedPaymentCoreTests.Program(new[] {
                TacticalFixedPaymentCoreTests.Event(0, 0, 0, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline, 0),
                TacticalFixedPaymentCoreTests.Event(0, 0, 1, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline, 1),
                Action(0, 0, 2, 2), Action(1, 0, 3, 0), Action(2, 0, 4, 1) }, new[] {
                new CharacterTestBehaviorExecutionTimeline(0, 3, 1, -1), new CharacterTestBehaviorExecutionTimeline(3, 1, 1, -1),
                new CharacterTestBehaviorExecutionTimeline(4, 1, 1, -1) }, new[] {
                new CharacterTestBehaviorExecutionFlow(1, 0), new CharacterTestBehaviorExecutionFlow(2, 0) });
            Budget(program, 10);
        }
        [Test] public void ReturnEventStopsBeforeAQueuedJumpIsPublished()
        {
            var program = TacticalFixedPaymentCoreTests.Program(new[] {
                TacticalFixedPaymentCoreTests.Event(0, 0, 0, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline, 0),
                TacticalFixedPaymentCoreTests.Event(0, 0, 1, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner),
                Action(1, 0, 2, 2) }, new[] { new CharacterTestBehaviorExecutionTimeline(0, 2, 1, -1),
                new CharacterTestBehaviorExecutionTimeline(2, 1, 1, -1) }, new[] { new CharacterTestBehaviorExecutionFlow(1, 0) });
            Budget(program, 0);
        }
        [TestCase(0, "budget:UnsupportedSharedOrRecipeClock")]
        [TestCase(1, "budget:UnsupportedSharedOrRecipeClock")]
        [TestCase(2, "budget:UnsupportedActionEventCooldown")]
        public void UnsupportedTimingIsExplicitRatherThanAZeroBudget(int mutation, string expected)
        {
            var program = TacticalFixedPaymentCoreTests.Program(new[] { Action(0, 0, 0, 0, shared: mutation == 0,
                clock: mutation == 1 ? CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe : CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline,
                cooldown: mutation == 2 ? 1 : 0) });
            Assert.IsFalse(CharacterTestFixedActionBudgetProof.TryCreate(program, _ => 10, out var proof, out string issue));
            Assert.IsNull(proof); Assert.AreEqual(expected, issue);
        }
        [Test] public void UnboundedLoopAndTimelineReentryAreRejectedOnlyByCombinationProof()
        {
            var loop = TacticalFixedPaymentCoreTests.Program(new[] { Action(0, 0, 0, 0),
                TacticalFixedPaymentCoreTests.Event(0, 0, 1, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion, 0, duration: 2) },
                flows: new[] { new CharacterTestBehaviorExecutionFlow(-1, 0) });
            Assert.IsFalse(CharacterTestFixedActionBudgetProof.TryCreate(loop, _ => 10, out _, out string issue));
            Assert.AreEqual("budget:UnsupportedUnboundedLoop", issue);
            var reentry = TacticalFixedPaymentCoreTests.Program(new[] {
                TacticalFixedPaymentCoreTests.Event(0, 0, 0, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline, 0) },
                flows: new[] { new CharacterTestBehaviorExecutionFlow(0, 0) });
            Assert.IsFalse(CharacterTestFixedActionBudgetProof.TryCreate(reentry, _ => 10, out _, out issue));
            Assert.AreEqual("budget:UnsupportedTimelineReentry", issue);
        }
        [Test] public void MissingCostAndIntegerOverflowNeverBecomeZeroFee()
        {
            Assert.IsFalse(CharacterTestFixedActionBudgetProof.TryCreate(TacticalFixedPaymentCoreTests.OneAction(), _ => null, out _, out string issue));
            Assert.AreEqual("budget:UnsupportedActionCost", issue);
            var program = TacticalFixedPaymentCoreTests.Program(new[] { Action(0, 0, 0, 0), Action(0, 1, 1, 1) });
            Assert.IsFalse(CharacterTestFixedActionBudgetProof.TryCreate(program, _ => int.MaxValue, out _, out issue));
            Assert.AreEqual("budget:Overflow", issue);
        }
        [Test] public void OverlappingLoopProofIsUnsupportedWithoutChangingProgramLegality()
        {
            var program = TacticalFixedPaymentCoreTests.Program(new[] { Action(0, 0, 0, 0),
                TacticalFixedPaymentCoreTests.Event(0, 0, 1, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion, 0, duration: 3),
                TacticalFixedPaymentCoreTests.Event(0, 1, 2, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion, 1, duration: 3) },
                flows: new[] { new CharacterTestBehaviorExecutionFlow(-1, 2), new CharacterTestBehaviorExecutionFlow(-1, 1) });
            Assert.IsFalse(CharacterTestFixedActionBudgetProof.TryCreate(program, _ => 10, out _, out string issue));
            Assert.AreEqual("budget:UnsupportedNestedOrOverlappingLoop", issue);
        }
    }
}
