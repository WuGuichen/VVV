using System.Collections.Generic;
using MxFramework.AI;
using NUnit.Framework;

namespace MxFramework.Tests.AI
{
    public class RuntimeAiPressureTests
    {
        [Test]
        public void ExploitPostureWeaknessGoal_DefaultsToCrackedBandValue()
        {
            var goal = new ExploitPostureWeaknessGoal();
            var world = new AiWorldState();
            world.SetValue(RuntimeAiPressureFactKeys.TargetPostureBand, 1);

            Assert.AreEqual(2, goal.ActivationBand);
            Assert.IsFalse(goal.IsRelevant(world));

            world.SetValue(RuntimeAiPressureFactKeys.TargetPostureBand, 2);

            Assert.IsTrue(goal.IsRelevant(world));
        }

        [Test]
        public void ExploitPostureWeaknessGoal_UsesExplicitExploitedFactForSatisfiedState()
        {
            var goal = new ExploitPostureWeaknessGoal();
            var world = new AiWorldState();

            Assert.IsFalse(goal.IsSatisfied(world));

            world.SetValue(RuntimeAiPressureFactKeys.TargetPostureWeaknessExploited, 1);

            Assert.IsTrue(goal.IsSatisfied(world));

            world.SetValue(RuntimeAiPressureFactKeys.TargetPostureWeaknessExploited, false);

            Assert.IsFalse(goal.IsSatisfied(world));

            world.SetValue(RuntimeAiPressureFactKeys.TargetPostureWeaknessExploited, true);

            Assert.IsTrue(goal.IsSatisfied(world));
        }

        [Test]
        public void PostureWeightEvaluator_AppliesDeterministicPressureMultiplier()
        {
            var world = new AiWorldState();
            world.SetValue(RuntimeAiPressureFactKeys.SelfGuardBand, 1);
            world.SetValue(RuntimeAiPressureFactKeys.SelfPostureBand, 2);
            world.SetValue(RuntimeAiPressureFactKeys.SelfArmorBroken, true);
            world.SetValue(RuntimeAiPressureFactKeys.TargetPostureBand, 3);

            var impactData = new Dictionary<int, PressureImpactData>
            {
                { 1001, new PressureImpactData(impactForce: 20, isHighPoiseDamage: true) }
            };

            float multiplier = PostureWeightEvaluator.GetActionWeightModifier(1001, world, impactData);

            Assert.AreEqual(1.49f, multiplier, 0.0001f);
        }

        [Test]
        public void PostureWeightEvaluator_ClampsAndTreatsMissingOrUnknownFactsAsNeutral()
        {
            var emptyWorld = new AiWorldState();
            var riskyWorld = new AiWorldState();
            riskyWorld.SetValue(RuntimeAiPressureFactKeys.SelfGuardBand, 9);
            riskyWorld.SetValue(RuntimeAiPressureFactKeys.SelfPostureBand, 9);
            riskyWorld.SetValue(RuntimeAiPressureFactKeys.SelfArmorBroken, 1);

            var impactData = new Dictionary<int, PressureImpactData>
            {
                { 1001, new PressureImpactData(impactForce: 0, isHighPoiseDamage: false) }
            };

            Assert.AreEqual(1f, PostureWeightEvaluator.GetActionWeightModifier(9999, emptyWorld, impactData), 0.0001f);
            Assert.AreEqual(1f, PostureWeightEvaluator.GetActionWeightModifier(1001, emptyWorld, impactData), 0.0001f);
            Assert.AreEqual(0.25f, PostureWeightEvaluator.GetActionWeightModifier(1001, riskyWorld, impactData), 0.0001f);
        }

        [Test]
        public void PostureWeightEvaluator_PrioritizesDefensiveRecoveryWhenSelfPressureIsHigh()
        {
            var world = new AiWorldState();
            world.SetValue(RuntimeAiPressureFactKeys.SelfGuardBand, 3);
            world.SetValue(RuntimeAiPressureFactKeys.SelfPostureBand, 2);
            world.SetValue(RuntimeAiPressureFactKeys.SelfArmorBroken, true);
            world.SetValue(RuntimeAiPressureFactKeys.TargetPostureBand, 3);
            var impactData = new Dictionary<int, PressureImpactData>
            {
                { 2001, new PressureImpactData(impactForce: 10, isHighPoiseDamage: false, isDefensiveRecovery: true) }
            };

            float modifier = PostureWeightEvaluator.GetActionWeightModifier(2001, world, impactData);

            Assert.AreEqual(1.81f, modifier, 0.0001f);
        }
    }
}
