using System.Collections.Generic;
using MxFramework.AI;
using NUnit.Framework;

namespace MxFramework.Tests.AI
{
    public class AiPlannerTests
    {
        [Test]
        public void PriorityGoalSelector_SelectsHighestRelevantUnsatisfiedGoal()
        {
            var healthLow = new AiFactKey("health.low");
            var enemyVisible = new AiFactKey("enemy.visible");
            var world = new AiWorldState();
            world.SetValue(healthLow, false);
            world.SetValue(enemyVisible, false);
            var selector = new PriorityGoalSelector();
            var goals = new IAiGoal[]
            {
                new AiFactGoal<bool>(1, 10f, enemyVisible, true),
                new AiFactGoal<bool>(2, 50f, healthLow, true)
            };

            bool selected = selector.TrySelectGoal(world, goals, out IAiGoal goal);

            Assert.IsTrue(selected);
            Assert.AreEqual(2, goal.Id);
        }

        [Test]
        public void Action_WhenPreconditionMissing_CannotExecute()
        {
            var hasWeapon = new AiFactKey("has.weapon");
            var enemyVisible = new AiFactKey("enemy.visible");
            var world = new AiWorldState();
            world.SetValue(hasWeapon, false);
            var action = new AiAction(
                100,
                1f,
                new[] { new AiFactCondition<bool>(hasWeapon, true) },
                new[] { new AiSetFactEffect<bool>(enemyVisible, true) });

            Assert.IsFalse(action.CanExecute(world));
        }

        [Test]
        public void SequentialPlanner_FindsExecutableActionChain()
        {
            var hasWeapon = new AiFactKey("has.weapon");
            var enemyVisible = new AiFactKey("enemy.visible");
            var enemyDefeated = new AiFactKey("enemy.defeated");
            var world = new AiWorldState();
            world.SetValue(hasWeapon, false);
            world.SetValue(enemyVisible, false);
            world.SetValue(enemyDefeated, false);
            var goals = new[] { new AiFactGoal<bool>(1, 10f, enemyDefeated, true) };
            var actions = new IAiAction[]
            {
                new AiAction(
                    10,
                    1f,
                    effects: new[] { new AiSetFactEffect<bool>(hasWeapon, true) }),
                new AiAction(
                    20,
                    1f,
                    new[] { new AiFactCondition<bool>(hasWeapon, true) },
                    new[] { new AiSetFactEffect<bool>(enemyVisible, true) }),
                new AiAction(
                    30,
                    1f,
                    new[]
                    {
                        new AiFactCondition<bool>(hasWeapon, true),
                        new AiFactCondition<bool>(enemyVisible, true)
                    },
                    new[] { new AiSetFactEffect<bool>(enemyDefeated, true) })
            };
            var planner = new SequentialPlanner(maxDepth: 4);

            bool planned = planner.TryPlan(world, goals, actions, out AiPlan plan);

            Assert.IsTrue(planned);
            Assert.IsTrue(plan.IsValid);
            Assert.AreEqual(3, plan.Actions.Count);
            Assert.AreEqual(10, plan.Actions[0].Id);
            Assert.AreEqual(20, plan.Actions[1].Id);
            Assert.AreEqual(30, plan.Actions[2].Id);
            Assert.IsFalse(world.TryGetValue(enemyDefeated, out bool defeated) && defeated);
        }

        [Test]
        public void SequentialPlanner_WhenGoalAlreadySatisfied_ReturnsNoSelectedGoal()
        {
            var safe = new AiFactKey("self.safe");
            var world = new AiWorldState();
            world.SetValue(safe, true);
            var goals = new[] { new AiFactGoal<bool>(1, 10f, safe, true) };
            var planner = new SequentialPlanner();

            bool planned = planner.TryPlan(world, goals, new List<IAiAction>(), out AiPlan plan);

            Assert.IsFalse(planned);
            Assert.IsNull(plan);
        }
    }
}
