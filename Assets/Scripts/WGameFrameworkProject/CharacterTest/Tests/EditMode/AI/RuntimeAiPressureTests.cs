using System.Collections.Generic;
using MxFramework.AI;
using NUnit.Framework;

namespace MxFramework.Tests.AI
{
    public class RuntimeAiPressureTests
    {
        [Test]
        public void ExploitScatterPressureWeaknessGoal_DefaultsToCrackedBandValue()
        {
            var goal = new ExploitScatterPressureWeaknessGoal();
            var world = new AiWorldState();
            world.SetValue(RuntimeAiPressureFactKeys.TargetScatterPressureBand, 1);

            Assert.AreEqual(2, goal.ActivationBand);
            Assert.IsFalse(goal.IsRelevant(world));

            world.SetValue(RuntimeAiPressureFactKeys.TargetScatterPressureBand, 2);

            Assert.IsTrue(goal.IsRelevant(world));
        }

        [Test]
        public void ExploitScatterPressureWeaknessGoal_UsesExplicitExploitedFactForSatisfiedState()
        {
            var goal = new ExploitScatterPressureWeaknessGoal();
            var world = new AiWorldState();

            Assert.IsFalse(goal.IsSatisfied(world));

            world.SetValue(RuntimeAiPressureFactKeys.TargetScatterPressureWeaknessExploited, 1);

            Assert.IsTrue(goal.IsSatisfied(world));

            world.SetValue(RuntimeAiPressureFactKeys.TargetScatterPressureWeaknessExploited, false);

            Assert.IsFalse(goal.IsSatisfied(world));

            world.SetValue(RuntimeAiPressureFactKeys.TargetScatterPressureWeaknessExploited, true);

            Assert.IsTrue(goal.IsSatisfied(world));
        }

        [Test]
        public void ScatterPressureWeightEvaluator_AppliesDeterministicPressureMultiplier()
        {
            var world = new AiWorldState();
            world.SetValue(RuntimeAiPressureFactKeys.SelfGuardBand, 1);
            world.SetValue(RuntimeAiPressureFactKeys.SelfScatterPressureBand, 2);
            world.SetValue(RuntimeAiPressureFactKeys.SelfArmorBroken, true);
            world.SetValue(RuntimeAiPressureFactKeys.TargetScatterPressureBand, 3);

            var impactData = new Dictionary<int, PressureImpactData>
            {
                { 1001, new PressureImpactData(impactForce: 20, isHighScatterDamage: true) }
            };

            float multiplier = ScatterPressureWeightEvaluator.GetActionWeightModifier(1001, world, impactData);

            Assert.AreEqual(1.49f, multiplier, 0.0001f);
        }

        [Test]
        public void ScatterPressureWeightEvaluator_ClampsAndTreatsMissingOrUnknownFactsAsNeutral()
        {
            var emptyWorld = new AiWorldState();
            var riskyWorld = new AiWorldState();
            riskyWorld.SetValue(RuntimeAiPressureFactKeys.SelfGuardBand, 9);
            riskyWorld.SetValue(RuntimeAiPressureFactKeys.SelfScatterPressureBand, 9);
            riskyWorld.SetValue(RuntimeAiPressureFactKeys.SelfArmorBroken, 1);

            var impactData = new Dictionary<int, PressureImpactData>
            {
                { 1001, new PressureImpactData(impactForce: 0, isHighScatterDamage: false) }
            };

            Assert.AreEqual(1f, ScatterPressureWeightEvaluator.GetActionWeightModifier(9999, emptyWorld, impactData), 0.0001f);
            Assert.AreEqual(1f, ScatterPressureWeightEvaluator.GetActionWeightModifier(1001, emptyWorld, impactData), 0.0001f);
            Assert.AreEqual(0.25f, ScatterPressureWeightEvaluator.GetActionWeightModifier(1001, riskyWorld, impactData), 0.0001f);
        }

        [Test]
        public void ScatterPressureWeightEvaluator_PrioritizesDefensiveRecoveryWhenSelfPressureIsHigh()
        {
            var world = new AiWorldState();
            world.SetValue(RuntimeAiPressureFactKeys.SelfGuardBand, 3);
            world.SetValue(RuntimeAiPressureFactKeys.SelfScatterPressureBand, 2);
            world.SetValue(RuntimeAiPressureFactKeys.SelfArmorBroken, true);
            world.SetValue(RuntimeAiPressureFactKeys.TargetScatterPressureBand, 3);
            var impactData = new Dictionary<int, PressureImpactData>
            {
                { 2001, new PressureImpactData(impactForce: 10, isHighScatterDamage: false, isDefensiveRecovery: true) }
            };

            float modifier = ScatterPressureWeightEvaluator.GetActionWeightModifier(2001, world, impactData);

            Assert.AreEqual(1.81f, modifier, 0.0001f);
        }
    }
}
