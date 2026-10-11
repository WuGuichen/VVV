using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;
using Unity.Profiling;

namespace MxFramework.Tests.Ability
{
    public sealed class AbilityRuntimeCostAuthorityTests
    {
        [Test]
        public void ActionOnlyCost_IsCheckedAndCommittedOnlyAfterAcceptance()
        {
            AbilityDefinitionConfig ability = CreateAbility();
            var registry = new AbilityDefinitionRegistry();
            Assert.IsTrue(registry.TryRegister(ability, out string failure), failure);
            var authority = new TestCostAuthority(14);
            var runtime = new AbilityRuntimeModule(registry, costAuthority: authority);

            Assert.IsFalse(runtime.TryEvaluate(1, ability, RuntimeFrame.Zero, out AbilityActivationResult rejected));
            StringAssert.StartsWith("InsufficientAbilityResource", rejected.Reason);
            Assert.AreEqual(AbilityActivationReason.InsufficientResource, rejected.ReasonCode);
            Assert.AreEqual(920002, rejected.DetailId);
            Assert.AreEqual(14, authority.Current);

            authority.Current = 15;
            Assert.IsTrue(runtime.TryEvaluate(1, ability, RuntimeFrame.Zero, out AbilityActivationResult accepted));
            Assert.AreEqual(15, authority.Current, "Evaluation must remain side-effect free.");
            Assert.IsTrue(runtime.TryCommitActivation(1, accepted.Ability, RuntimeFrame.Zero, out AbilityActivationFailure commitFailure), commitFailure.ToString());
            Assert.AreEqual(0, authority.Current);
        }

        [Test]
        public void ConfiguredCostWithoutAuthority_IsRejected()
        {
            AbilityDefinitionConfig ability = CreateAbility();
            var registry = new AbilityDefinitionRegistry();
            Assert.IsTrue(registry.TryRegister(ability, out string failure), failure);
            var runtime = new AbilityRuntimeModule(registry);

            Assert.IsFalse(runtime.TryEvaluate(1, ability, RuntimeFrame.Zero, out AbilityActivationResult result));
            Assert.AreEqual("MissingAbilityCostAuthority", result.Reason);
            Assert.AreEqual(AbilityActivationReason.MissingCostAuthority, result.ReasonCode);
            Assert.AreEqual(0, result.DetailId);
        }

        [Test]
        public void EvaluationRetainsFixedFailureCodesAndCooldownDetails()
        {
            var registry = new AbilityDefinitionRegistry();
            AbilityDefinitionConfig ability = CreateAbility(cooldownFrames: 3);
            Assert.IsTrue(registry.TryRegister(ability, out _));
            var runtime = new AbilityRuntimeModule(registry, costAuthority: new TestCostAuthority(100));
            Assert.IsFalse(runtime.TryResolveInputIntent(1, int.MaxValue, RuntimeFrame.Zero, out var missing));
            Assert.AreEqual(AbilityActivationReason.MissingInputBinding, missing.ReasonCode);
            Assert.IsFalse(runtime.TryEvaluate(0, ability, RuntimeFrame.Zero, out var invalid));
            Assert.AreEqual(AbilityActivationReason.InvalidActivation, invalid.ReasonCode);
            Assert.IsFalse(runtime.TryEvaluate(1, CreateAbility(withAction: false), RuntimeFrame.Zero, out var noAction));
            Assert.AreEqual(AbilityActivationReason.MissingActionIntent, noAction.ReasonCode);
            Assert.IsTrue(runtime.TryEvaluate(1, ability, RuntimeFrame.Zero, out var accepted));
            Assert.AreEqual(AbilityActivationReason.None, accepted.ReasonCode);
            Assert.IsTrue(runtime.TryCommitActivation(1, ability, RuntimeFrame.Zero, out _));
            Assert.IsFalse(runtime.TryEvaluate(1, ability, new RuntimeFrame(1), out var cooldown));
            Assert.AreEqual(AbilityActivationReason.OnCooldown, cooldown.ReasonCode);
            Assert.AreEqual(2L, cooldown.RemainingCooldownFrames);
            Assert.AreEqual(0, cooldown.DetailId);
        }

        [Test]
        public void NumericAffordabilityFailuresDoNotAllocateBeforeFormatting()
        {
            var registry = new AbilityDefinitionRegistry();
            var ability = CreateAbility();
            Assert.IsTrue(registry.TryRegister(ability, out _));
            var runtime = new AbilityRuntimeModule(registry, costAuthority: new TestCostAuthority(0));
            for (int i = 0; i < 50; i++) runtime.TryEvaluate(1, ability, RuntimeFrame.Zero, out _);
            var options = ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            using (var positive = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024, options))
            {
                GC.KeepAlive(new byte[8192]);
                positive.Stop();
                Assert.Greater(positive.Count, 0, "Profiler allocation counter positive control.");
            }
            long totalDetails = 0;
            using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024, options);
            for (int i = 0; i < 500; i++)
            {
                runtime.TryEvaluate(1, ability, RuntimeFrame.Zero, out var result);
                totalDetails += result.DetailId;
            }
            allocations.Stop();
            Assert.AreEqual(500L * 920002, totalDetails);
            Assert.AreEqual(0, allocations.Count);
        }

        private static AbilityDefinitionConfig CreateAbility(long cooldownFrames = 0, bool withAction = true)
        {
            return new AbilityDefinitionConfig(
                900001,
                "ability.evade",
                "Evade",
                new AbilityInputBindingConfig(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id60, MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D405_Id35),
                new AbilityCooldownConfig(cooldownFrames, 0),
                new[] { new AbilityCostConfig(920002, 15) },
                920003,
                withAction ? new AbilityActionBindingConfig(new CharacterActionId(950003), MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id60) : AbilityActionBindingConfig.Empty,
                default,
                920004,
                920005,
                targetPolicyLabel: "target.self",
                presentationProfileLabel: "",
                effectTimelineLabel: "");
        }

        private sealed class TestCostAuthority : IAbilityCostAuthority
        {
            public TestCostAuthority(int current)
            {
                Current = current;
            }

            public int Current { get; set; }

            public bool CanAfford(
                int casterEntityId,
                IReadOnlyList<AbilityCostConfig> costs,
                out int missingAttributeId)
            {
                missingAttributeId = 920002;
                return casterEntityId > 0 && costs != null && costs.Count == 1
                    && Current >= costs[0].Amount;
            }

            public bool TryCommitCosts(
                int casterEntityId,
                IReadOnlyList<AbilityCostConfig> costs,
                out int missingAttributeId)
            {
                if (!CanAfford(casterEntityId, costs, out missingAttributeId))
                    return false;
                Current -= costs[0].Amount;
                return true;
            }
        }
    }
}
