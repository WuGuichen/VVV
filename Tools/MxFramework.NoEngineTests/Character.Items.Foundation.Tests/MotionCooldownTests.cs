using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    // Fixture definition identities only. Actual production module and ledger are linked.
    public sealed class MotionCooldownAdmissionTests
    {
        private static AbilityDefinitionConfig Definition(long frames = 60, int cost = 10)
            => new AbilityDefinitionConfig(71001, "fixture.action-owner", "Fixture", null,
                new AbilityCooldownConfig(frames, 0), new[] { new AbilityCostConfig(71002, cost) },
                0, new AbilityActionBindingConfig(new CharacterActionId(302), 1), default, 0, 0);

        private sealed class Costs : IAbilityCostAuthority
        {
            internal readonly Dictionary<int, MomentumLedger> Ledgers = new Dictionary<int, MomentumLedger>();
            internal int Commits;
            public bool CanAfford(int actor, IReadOnlyList<AbilityCostConfig> costs, out int missing)
            { missing = 71002; return Ledgers.TryGetValue(actor, out var ledger) && costs[0].Amount <= ledger.Spendable; }
            public bool TryCommitCosts(int actor, IReadOnlyList<AbilityCostConfig> costs, out int missing)
            { if (!CanAfford(actor, costs, out missing)) return false; Commits++; return Ledgers[actor].TrySpend(costs[0].Amount); }
        }
        private static (AbilityRuntimeModule Runtime, AbilityDefinitionConfig Definition, Costs Costs) Fixture(long frames = 60, int cost = 10)
        {
            var definition = Definition(frames, cost); var registry = new AbilityDefinitionRegistry();
            Assert.IsTrue(registry.TryRegister(definition, out var issue), issue);
            var costs = new Costs(); costs.Ledgers.Add(1, new MomentumLedger(100)); costs.Ledgers.Add(2, new MomentumLedger(100));
            return (new AbilityRuntimeModule(registry, costAuthority: costs), definition, costs);
        }
        [Test] public void SameActorActionCooldownIsSharedAcrossCardQueriesAndOtherActorIsIndependent()
        {
            var f = Fixture(); var frame = new RuntimeFrame(7);
            Assert.IsTrue(f.Runtime.TryCommitActivation(1, f.Definition, frame, out _));
            for (int card = 0; card < 2; card++)
            {
                Assert.IsFalse(f.Runtime.TryEvaluate(1, f.Definition, frame, out var blocked));
                Assert.AreEqual(AbilityActivationReason.OnCooldown, blocked.ReasonCode);
                Assert.AreEqual(60, blocked.RemainingCooldownFrames);
            }
            Assert.IsTrue(f.Runtime.TryEvaluate(2, f.Definition, frame, out _));
            Assert.AreEqual(90, f.Costs.Ledgers[1].Current); Assert.AreEqual(100, f.Costs.Ledgers[2].Current);
            Assert.AreEqual(1, f.Costs.Commits);
        }
        [Test] public void FinalCooldownRecheckRejectsWithoutPayingAgainOrExtendingDeadline()
        {
            var f = Fixture(); Assert.IsTrue(f.Runtime.TryEvaluate(1, f.Definition, new RuntimeFrame(5), out _));
            Assert.IsTrue(f.Runtime.TryCommitActivation(1, f.Definition, new RuntimeFrame(5), out _));
            Assert.IsFalse(f.Runtime.TryCommitActivation(1, f.Definition, new RuntimeFrame(6), out var failure));
            Assert.AreEqual(AbilityActivationReason.OnCooldown, failure.ReasonCode);
            Assert.AreEqual(59, f.Runtime.GetRemainingCooldownFrames(1, f.Definition, new RuntimeFrame(6)));
            Assert.AreEqual(1, f.Costs.Commits); Assert.AreEqual(90, f.Costs.Ledgers[1].Current);
        }
        [Test] public void ExactDeadlineAllowsOneNewPaidActivation()
        {
            var f = Fixture(); Assert.IsTrue(f.Runtime.TryCommitActivation(1, f.Definition, RuntimeFrame.Zero, out _));
            Assert.IsFalse(f.Runtime.TryCommitActivation(1, f.Definition, new RuntimeFrame(59), out _));
            Assert.IsTrue(f.Runtime.TryCommitActivation(1, f.Definition, new RuntimeFrame(60), out _));
            Assert.AreEqual(80, f.Costs.Ledgers[1].Current); Assert.AreEqual(2, f.Costs.Commits);
            Assert.AreEqual(60, f.Runtime.GetRemainingCooldownFrames(1, f.Definition, new RuntimeFrame(60)));
        }
        [Test] public void FrameOverflowRejectsBeforeAnyCostOrCooldownMutation()
        {
            var f = Fixture();
            Assert.IsFalse(f.Runtime.TryCommitActivation(1, f.Definition, new RuntimeFrame(long.MaxValue - 59), out var failure));
            Assert.AreEqual(AbilityActivationReason.InvalidActivation, failure.ReasonCode);
            Assert.AreEqual(100, f.Costs.Ledgers[1].Current); Assert.AreEqual(0, f.Costs.Commits); Assert.AreEqual(0, f.Runtime.ActiveCooldownCount);
        }
        [Test] public void MaximumValidEndFrameCommitsAndExpiresWithoutWrap()
        {
            var f = Fixture(); var frame = new RuntimeFrame(long.MaxValue - 60);
            Assert.IsTrue(f.Runtime.TryCommitActivation(1, f.Definition, frame, out _));
            Assert.AreEqual(60, f.Runtime.GetRemainingCooldownFrames(1, f.Definition, frame));
            Assert.AreEqual(0, f.Runtime.GetRemainingCooldownFrames(1, f.Definition, new RuntimeFrame(long.MaxValue)));
            Assert.AreEqual(90, f.Costs.Ledgers[1].Current);
        }
        [Test] public void ReadOnlyCooldownQueriesAtFrozenWorldFrameDoNotAdvanceOrPay()
        {
            var f = Fixture(); var frame = new RuntimeFrame(10);
            Assert.IsTrue(f.Runtime.TryCommitActivation(1, f.Definition, frame, out _));
            for (int presentationFrame = 0; presentationFrame < 100; presentationFrame++)
            {
                Assert.IsFalse(f.Runtime.TryEvaluate(1, f.Definition, frame, out _));
                Assert.AreEqual(60, f.Runtime.GetRemainingCooldownFrames(1, f.Definition, frame));
            }
            Assert.AreEqual(1, f.Costs.Commits); Assert.AreEqual(90, f.Costs.Ledgers[1].Current);
        }
        [Test] public void LateResourceFailureAndOwnedReservationKeepAllStateUnchanged()
        {
            var f = Fixture(cost: 70); var ledger = f.Costs.Ledgers[1];
            Assert.IsTrue(f.Runtime.TryEvaluate(1, f.Definition, RuntimeFrame.Zero, out _));
            Assert.IsTrue(ledger.TryReserve(40, out var token));
            Assert.IsFalse(f.Runtime.TryCommitActivation(1, f.Definition, RuntimeFrame.Zero, out var failure));
            Assert.AreEqual(AbilityActivationReason.InsufficientResource, failure.ReasonCode);
            Assert.AreEqual(100, ledger.Current); Assert.AreEqual(40, ledger.Reserved); Assert.AreEqual(0, f.Runtime.ActiveCooldownCount);
            Assert.IsTrue(ledger.ReleaseReservation(token)); Assert.IsFalse(ledger.ReleaseReservation(token));
            Assert.IsTrue(f.Runtime.TryCommitActivation(1, f.Definition, RuntimeFrame.Zero, out _)); Assert.AreEqual(30, ledger.Current);
        }
        [Test] public void ExistingAbilitySaveProviderRestoresSameBattleCooldownBeforeReplayedQueries()
        {
            var f = Fixture(); Assert.IsTrue(f.Runtime.TryCommitActivation(1, f.Definition, new RuntimeFrame(9), out _));
            var provider = new AbilityRuntimeSaveStateProvider(f.Runtime); var saved = provider.CaptureSaveState(); Assert.IsTrue(saved.Success);
            f.Runtime.Tick(new RuntimeTickContext(100, 1d / 60d, 0, RuntimeTickStage.PreSimulation)); Assert.AreEqual(0, f.Runtime.ActiveCooldownCount);
            Assert.IsTrue(provider.RestoreSaveState(saved.Value).Success);
            Assert.IsFalse(f.Runtime.TryCommitActivation(1, f.Definition, new RuntimeFrame(10), out _));
            Assert.AreEqual(59, f.Runtime.GetRemainingCooldownFrames(1, f.Definition, new RuntimeFrame(10)));
            Assert.AreEqual(1, f.Costs.Commits); Assert.AreEqual(90, f.Costs.Ledgers[1].Current);
        }
    }
}
