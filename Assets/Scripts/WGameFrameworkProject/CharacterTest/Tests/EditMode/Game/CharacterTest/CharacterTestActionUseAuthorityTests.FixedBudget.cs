using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestActionUseAuthorityTests
    {
        private static AbilityRuntimeModule FixedTransactionOwner(long cooldown)
        {
            var registry = new AbilityDefinitionRegistry();
            Assert.IsTrue(registry.TryRegister(new AbilityDefinitionConfig(ControlledAbilityId, OwnerId, "Fixed command fixture", null,
                new AbilityCooldownConfig(cooldown, 0), new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Motion, 20) },
                0, new AbilityActionBindingConfig(new CharacterActionId(301), MigrationFixtureIds.Id0030), default, 0, 0), out _));
            return new AbilityRuntimeModule(registry);
        }
        private sealed class FixedCommandContext : ICharacterTestBehaviorReceiptObserver, ICharacterTestFixedBehaviorActionContext
        {
            internal TacticalFixedCardPaymentCommit Payment;
            internal CharacterTestRuntimeAiPlannerFactState Facts;
            internal int Count, Entity;
            public bool TryGetFixedActionPayment(RuntimeFrame frame, int entity, int resource,
                out TacticalFixedCardPaymentCommit payment, out CharacterTestRuntimeAiPlannerFactState facts)
            { payment = Payment; facts = Facts; return entity == Entity && resource == 301; }
            public void OnBehaviorOrchestrationReceipt(in CharacterTestBehaviorOrchestrationReceipt receipt) { Count++; }
        }
        private static TacticalFixedCardPaymentCommit FixedPayment(CharacterTestTacticalCardRuntimeFixture f, out MomentumLedger ledger)
        {
            var definition = TacticalFixedAssemblyManifestTests.Main();
            Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(TacticalFixedAssemblyManifestTests.Snapshot(), definition, 11, 7, 3,
                new Dictionary<string, int> { { "main.action", 301 } }, out var assembly, out _));
            definition.AttachFixedAssembly(assembly);
            var card = new TacticalCardInstance(501, definition); card.MoveTo(CardZone.Hand); card.MoveTo(CardZone.PendingAction);
            ledger = new MomentumLedger(50); f.Runtime.World.BindTacticalMotion(f.Source.Id.Value, ledger);
            Assert.IsTrue(ledger.TryReserve(3, out var token));
            var program = new CharacterTestBehaviorExecutionProgram(0, -1, "fixed-actual-owner-fixture",
                new[] { new CharacterTestBehaviorExecutionTimeline(0, 1, 4, -1) },
                new[] { TacticalFixedPaymentCoreTests.Event(0, 0, 0, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction, 0) }, new[] { 0 },
                new[] { new CharacterTestBehaviorExecutionAction(301) }, Array.Empty<CharacterTestBehaviorExecutionMovement>(),
                Array.Empty<CharacterTestBehaviorExecutionFlow>(), Array.Empty<CharacterTestBehaviorExecutionCondition>(),
                Array.Empty<CharacterTestBehaviorExecutionPredicate>(), Array.Empty<int>());
            Assert.IsTrue(CharacterTestFixedActionBudgetProof.TryCreate(program,
                id => f.Runtime.ActionUseAuthority.TryGetFixedActionMomentumCost(f.Source.Id.Value, id, out var amount, out _) ? (int?)amount : null,
                out var proof, out _));
            Assert.AreEqual(20, proof.MaximumMomentum);
            Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(assembly, TacticalFixedCardPaymentTiming.CardAndAccessoriesThenActualActions, 20, out var fees));
            var effects = f.Source.InteractionEffects; effects.Bind(7, 2, f.Source.GameplayEntityId);
            Assert.IsTrue(TacticalFixedCardCommitManifest.TryPrepare(assembly, fees, card, 7, 2, f.Source.GameplayEntityId,
                1, 1, f.NextFrame, 0, 1, token, default, out var manifest));
            Assert.IsTrue(TacticalFixedCardCommitManifest.PreparedPaymentAndAccessories.TryPrepare(manifest, card, ledger, effects,
                program, proof, out var prepared, out var issue), issue);
            Assert.IsTrue(prepared.TryPublish(out var payment)); return payment;
        }
        private static RuntimeCommand FixedCommand(CharacterTestTacticalCardRuntimeFixture f, TacticalFixedCardPaymentCommit payment)
        {
            var frame = new RuntimeFrame(f.NextFrame);
            return payment.BindCommand(CharacterTestRuntimeCommandFactory.BehaviorRecipeOrchestration(frame,
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner, f.Source.Id.Value,
                CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction, 301,
                CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.ExecutionAction, f.Source.Id.Value, frame)));
        }
        private static bool ApplyFixed(CharacterTestTacticalCardRuntimeFixture f, RuntimeCommand c, out string issue)
            => f.Runtime.TryApplyBehaviorRecipeOrchestration(c.Frame, c.TargetId,
                (CharacterTestBehaviorRecipeOrchestrationOperation)c.Payload0, c.Payload1, c.TraceId,
                711, c.SourceId, out issue, c.Payload2, c.Envelope);

        [TestCase(0)] [TestCase(60)]
        public void FixedRealActionPaysReservedCostThroughAbilityOwnerAndDuplicateCommandPaysOnce(long cooldown)
        {
            var ability = FixedTransactionOwner(cooldown);
            using var f = new CharacterTestTacticalCardRuntimeFixture(actionId: 301, actionRuntime: RestrictedRuntime(), abilityRuntime: ability);
            var payment = FixedPayment(f, out var ledger);
            var context = new FixedCommandContext { Payment = payment, Facts = f.Facts, Entity = f.Source.Id.Value };
            f.Runtime.RegisterBehaviorReceiptObserver(context);
            var command = FixedCommand(f, payment);
            Assert.IsTrue(ApplyFixed(f, command, out var issue), issue);
            Assert.AreEqual(25, ledger.Current); Assert.AreEqual(0, ledger.Reserved); Assert.IsTrue(payment.HasReservation);
            Assert.IsTrue(ability.Definitions.TryGet(ControlledAbilityId, out var owner));
            Assert.AreEqual(cooldown, ability.GetRemainingCooldownFrames(f.Source.Id.Value, owner, command.Frame));
            Assert.IsTrue(f.Runtime.ActionRuntime.TryGetActiveActionPresentationState(f.Source.Id, out var instance, out _, out _, out _));
            long commits = f.Runtime.ActionUseAuthority.CommitCount;
            Assert.IsTrue(ApplyFixed(f, command, out _));
            Assert.AreEqual(commits, f.Runtime.ActionUseAuthority.CommitCount); Assert.AreEqual(25, ledger.Current); Assert.AreEqual(1, context.Count);
            Assert.IsTrue(f.Runtime.ActionRuntime.TryGetActiveActionPresentationState(f.Source.Id, out var again, out _, out _, out _));
            Assert.AreEqual(instance, again);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void WrongFixedExecutionGenerationTokenOrProgramCannotBorrowBudget(int changed)
        {
            var ability = FixedTransactionOwner(60);
            using var f = new CharacterTestTacticalCardRuntimeFixture(actionId: 301, actionRuntime: RestrictedRuntime(), abilityRuntime: ability);
            var payment = FixedPayment(f, out var ledger);
            f.Runtime.RegisterBehaviorReceiptObserver(new FixedCommandContext { Payment = payment, Facts = f.Facts, Entity = f.Source.Id.Value });
            var c = FixedCommand(f, payment); var e = c.Envelope;
            var bad = new RuntimeCommandEnvelope(e.TypeId, e.SchemaVersion, e.Provenance,
                e.Data0 + (changed == 0 ? 1 : 0), e.Data1, e.Data2 + (changed == 1 ? 1 : 0),
                e.Data3 + (changed == 2 ? 1 : 0), e.Data4, e.Data5 + (changed == 3 ? 1 : 0), e.Data6, e.Data7);
            c = new RuntimeCommand(c.Frame, c.SourceId, c.CommandId, c.TargetId, c.Payload0, c.Payload1, c.Payload2, traceId: c.TraceId, envelope: bad);
            Assert.IsFalse(ApplyFixed(f, c, out var issue)); Assert.AreEqual("BehaviorRecipe.FixedPaymentIdentityRejected", issue);
            Assert.AreEqual(45, ledger.Current); Assert.AreEqual(20, ledger.Reserved); Assert.AreEqual(0, ability.ActiveCooldownCount);
            Assert.AreEqual(0, f.Runtime.ActionUseAuthority.CommitCount);
        }

        [Test] public void OrdinaryActionCannotSpendFixedReservationButMatchingActionCan()
        {
            var ability = FixedTransactionOwner(60);
            using var f = new CharacterTestTacticalCardRuntimeFixture(actionId: 301, actionRuntime: RestrictedRuntime(), abilityRuntime: ability);
            var payment = FixedPayment(f, out var ledger); Assert.IsTrue(ledger.TrySpend(ledger.Spendable));
            Assert.AreEqual(20, ledger.Current); Assert.AreEqual(20, ledger.Reserved);
            var c = FixedCommand(f, payment);
            Assert.IsFalse(f.Runtime.TryApplyBehaviorRecipeOrchestration(c.Frame, c.TargetId,
                CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction, 301, c.TraceId, 710, c.SourceId, out _));
            Assert.AreEqual(20, ledger.Current); Assert.AreEqual(0, ability.ActiveCooldownCount);
            var context = new FixedCommandContext { Payment = payment, Facts = f.Facts, Entity = f.Source.Id.Value };
            f.Runtime.RegisterBehaviorReceiptObserver(context);
            Assert.IsTrue(ApplyFixed(f, c, out var issue), issue);
            Assert.AreEqual(0, ledger.Current); Assert.AreEqual(0, ledger.Reserved); Assert.AreEqual(1, ability.ActiveCooldownCount);
        }
    }
}
