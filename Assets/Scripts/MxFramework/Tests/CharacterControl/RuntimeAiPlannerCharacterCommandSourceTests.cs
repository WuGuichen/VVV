using System.Collections.Generic;
using MxFramework.AI;
using MxFramework.CharacterControl;
using MxFramework.CharacterControl.RuntimeAiPlannerBridge;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class RuntimeAiPlannerCharacterCommandSourceTests
    {
        [Test]
        public void SamePlanInput_ProducesStableMovementAndOneShotActionRequest()
        {
            var world = new AiWorldState();
            world.SetValue(RuntimeAiPressureFactKeys.TargetPostureBand, 2);
            var action = new TestAction(10);
            var source = CreateSource(world, action, new RuntimeAiCharacterCommandProfile(
                actionId: 10,
                moveDirection: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                facingBasis: CharacterFacingBasis.Identity,
                sprintHeld: true,
                actionKind: CharacterActionKind.Attack,
                combatActionId: 1001));

            Assert.IsTrue(source.TryGetCommand(RuntimeFrame.Zero, CreateEntity(), out CharacterCommand first));
            Assert.IsTrue(source.TryGetCommand(new RuntimeFrame(1), CreateEntity(), out CharacterCommand second));

            Assert.AreEqual(Fix64.One, first.MoveDirection.X);
            Assert.IsTrue(first.SprintHeld);
            Assert.AreEqual(1001, first.ActionRequest.CombatActionId);
            Assert.AreEqual(10, source.Diagnostics.LastActionId);
            Assert.AreEqual(Fix64.One, second.MoveDirection.X);
            Assert.AreEqual(CharacterActionKind.None, second.ActionRequest.Kind);
        }

        [Test]
        public void TargetFactsMissing_ClearsOldCommandAndSuppressesOutput()
        {
            var world = new AiWorldState();
            world.SetValue(RuntimeAiPressureFactKeys.TargetPostureBand, 2);
            var action = new TestAction(10);
            var source = CreateSource(world, action, new RuntimeAiCharacterCommandProfile(
                actionId: 10,
                moveDirection: FixVector3.Zero,
                facingBasis: CharacterFacingBasis.Identity));

            Assert.IsTrue(source.TryGetCommand(RuntimeFrame.Zero, CreateEntity(), out _));
            world.Remove(RuntimeAiPressureFactKeys.TargetPostureBand);

            Assert.IsFalse(source.TryGetCommand(new RuntimeFrame(1), CreateEntity(), out _));
            Assert.AreEqual(
                RuntimeAiPlannerCharacterCommandSuppressedReason.TargetFactsMissing,
                source.Diagnostics.SuppressedReason);
            Assert.AreEqual(0, source.Diagnostics.LastActionId);
        }

        [Test]
        public void ReactionDelay_SuppressesThenEmitsCommand()
        {
            var world = new AiWorldState();
            var action = new TestAction(20);
            var source = CreateSource(
                world,
                action,
                new RuntimeAiCharacterCommandProfile(
                    actionId: 20,
                    moveDirection: new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                    facingBasis: CharacterFacingBasis.Identity,
                    actionKind: CharacterActionKind.Attack,
                    combatActionId: 2001),
                new RuntimeAiPlannerCharacterCommandSourceOptions
                {
                    ReactionDelayFrames = 2
                });

            Assert.IsFalse(source.TryGetCommand(RuntimeFrame.Zero, CreateEntity(), out _));
            Assert.IsFalse(source.TryGetCommand(new RuntimeFrame(1), CreateEntity(), out _));
            Assert.IsTrue(source.TryGetCommand(new RuntimeFrame(2), CreateEntity(), out CharacterCommand command));

            Assert.AreEqual(Fix64.One, command.MoveDirection.Z);
            Assert.AreEqual(2001, command.ActionRequest.CombatActionId);
            Assert.AreEqual(RuntimeAiPlannerCharacterCommandSuppressedReason.None, source.Diagnostics.SuppressedReason);
        }

        [Test]
        public void MinDecisionInterval_ReusesPreviousProfile()
        {
            var world = new AiWorldState();
            var first = new TestAction(1);
            var second = new TestAction(2);
            var planner = new MutablePlanner(first);
            var registry = new RuntimeAiCharacterCommandProfileRegistry();
            registry.Register(new RuntimeAiCharacterCommandProfile(
                actionId: 1,
                moveDirection: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                facingBasis: CharacterFacingBasis.Identity,
                actionKind: CharacterActionKind.Attack,
                combatActionId: 1001));
            registry.Register(new RuntimeAiCharacterCommandProfile(
                actionId: 2,
                moveDirection: new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                facingBasis: CharacterFacingBasis.Identity,
                actionKind: CharacterActionKind.Attack,
                combatActionId: 1002));
            var source = new RuntimeAiPlannerCharacterCommandSource(
                world,
                planner,
                new[] { new TestGoal() },
                new IAiAction[] { first, second },
                registry,
                new RuntimeAiPlannerCharacterCommandSourceOptions
                {
                    MinDecisionIntervalFrames = 5
                });

            Assert.IsTrue(source.TryGetCommand(RuntimeFrame.Zero, CreateEntity(), out CharacterCommand initial));
            planner.CurrentAction = second;
            Assert.IsTrue(source.TryGetCommand(new RuntimeFrame(1), CreateEntity(), out CharacterCommand reused));

            Assert.AreEqual(Fix64.One, initial.MoveDirection.X);
            Assert.AreEqual(1001, initial.ActionRequest.CombatActionId);
            Assert.AreEqual(Fix64.One, reused.MoveDirection.X);
            Assert.AreEqual(Fix64.Zero, reused.MoveDirection.Z);
            Assert.AreEqual(CharacterActionKind.None, reused.ActionRequest.Kind);
        }

        [Test]
        public void Profile_AllowsExplicitZeroMoveSpeedScale()
        {
            var world = new AiWorldState();
            var action = new TestAction(10);
            var source = CreateSource(
                world,
                action,
                new RuntimeAiCharacterCommandProfile(
                    actionId: 10,
                    moveDirection: FixVector3.Zero,
                    facingBasis: CharacterFacingBasis.Identity,
                    moveSpeedScale: Fix64.Zero),
                new RuntimeAiPlannerCharacterCommandSourceOptions());

            Assert.IsTrue(source.TryGetCommand(RuntimeFrame.Zero, CreateEntity(), out CharacterCommand command));

            Assert.AreEqual(Fix64.Zero, command.MoveSpeedScale);
        }

        private static RuntimeAiPlannerCharacterCommandSource CreateSource(
            AiWorldState world,
            TestAction action,
            RuntimeAiCharacterCommandProfile profile,
            RuntimeAiPlannerCharacterCommandSourceOptions options = null)
        {
            var registry = new RuntimeAiCharacterCommandProfileRegistry();
            registry.Register(profile);
            options ??= new RuntimeAiPlannerCharacterCommandSourceOptions
            {
                RequireTargetFacts = true
            };

            return new RuntimeAiPlannerCharacterCommandSource(
                world,
                new MutablePlanner(action),
                new[] { new TestGoal() },
                new IAiAction[] { action },
                registry,
                options);
        }

        private static CharacterControlEntityRef CreateEntity()
        {
            return CharacterControlEntityRef.FromGameplay(new GameplayEntityId(10, 1), stableId: 1);
        }

        private sealed class MutablePlanner : IAiPlanner
        {
            public MutablePlanner(IAiAction action)
            {
                CurrentAction = action;
            }

            public IAiAction CurrentAction { get; set; }

            public bool TryPlan(IAiWorldState worldState, IEnumerable<IAiGoal> goals, IEnumerable<IAiAction> actions, out AiPlan plan)
            {
                plan = new AiPlan(new TestGoal(), new[] { CurrentAction }, CurrentAction.Cost);
                return true;
            }
        }

        private sealed class TestGoal : IAiGoal
        {
            public int Id => 1;

            public float Priority => 1f;

            public bool IsRelevant(IAiWorldState worldState)
            {
                return true;
            }

            public bool IsSatisfied(IAiWorldState worldState)
            {
                return false;
            }
        }

        private sealed class TestAction : IAiAction
        {
            public TestAction(int id)
            {
                Id = id;
            }

            public int Id { get; }

            public float Cost => 1f;

            public IReadOnlyList<IAiCondition> Preconditions => new IAiCondition[0];

            public IReadOnlyList<IAiEffect> Effects => new IAiEffect[0];

            public bool CanExecute(IAiWorldState worldState)
            {
                return true;
            }

            public void Apply(IAiWorldState worldState)
            {
            }
        }
    }
}
