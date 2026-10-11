using System;
using System.Collections.Generic;
using MxFramework.AI;
using NUnit.Framework;

namespace MxFramework.Tests.AI
{
    public sealed class AiPlannerV2Tests
    {
        [Test]
        public void AiBlackboard_ReadsParentOverridesLocallyAndReportsChanges()
        {
            var key = new AiFactKey("target.visible");
            var shared = new SharedAiBlackboard();
            shared.SetValue(key, false);
            var local = new AiBlackboard(shared);
            var events = new List<AiBlackboardFactChangedEvent>();
            local.FactChanged += events.Add;

            Assert.IsTrue(local.TryGetValue(key, out bool inherited));
            Assert.IsFalse(inherited);

            local.SetValue(key, true);

            Assert.IsTrue(local.TryGetValue(key, out bool localValue));
            Assert.IsTrue(localValue);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(AiBlackboardFactChangeKind.Set, events[0].Kind);
            Assert.IsTrue(events[0].HadPreviousValue);
            Assert.AreEqual(false, events[0].PreviousValue);
            Assert.AreEqual(true, events[0].CurrentValue);

            Assert.IsTrue(local.RemoveLocal(key));

            Assert.IsTrue(local.TryGetValue(key, out bool restored));
            Assert.IsFalse(restored);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(AiBlackboardFactChangeKind.Removed, events[1].Kind);
            Assert.IsTrue(events[1].HasCurrentValue);
        }

        [Test]
        public void AiBlackboard_ToWorldStateCopiesFlattenedSnapshot()
        {
            var inherited = new AiFactKey("inherited");
            var localOnly = new AiFactKey("local");
            var shared = new SharedAiBlackboard();
            shared.SetValue(inherited, 10);
            var local = new AiBlackboard(shared);
            local.SetValue(localOnly, 20);

            AiWorldState worldState = local.ToWorldState();
            local.SetValue(localOnly, 30);

            Assert.IsTrue(worldState.TryGetValue(inherited, out int inheritedValue));
            Assert.AreEqual(10, inheritedValue);
            Assert.IsTrue(worldState.TryGetValue(localOnly, out int copiedLocal));
            Assert.AreEqual(20, copiedLocal);
        }

        [Test]
        public void AiBlackboard_ClearLocalRestoresParentFactsAndReportsClearedEvents()
        {
            var inherited = new AiFactKey("inherited");
            var localOnly = new AiFactKey("local");
            var shared = new SharedAiBlackboard();
            shared.SetValue(inherited, 1);
            var local = new AiBlackboard(shared);
            local.SetValue(inherited, 2);
            local.SetValue(localOnly, 3);
            var events = new List<AiBlackboardFactChangedEvent>();
            local.FactChanged += events.Add;

            local.ClearLocal();

            Assert.IsFalse(local.ContainsLocal(inherited));
            Assert.IsTrue(local.TryGetValue(inherited, out int restored));
            Assert.AreEqual(1, restored);
            Assert.IsFalse(local.Contains(localOnly));
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(AiBlackboardFactChangeKind.Cleared, events[0].Kind);
            Assert.IsTrue(events[0].HasCurrentValue);
            Assert.AreEqual(AiBlackboardFactChangeKind.Cleared, events[1].Kind);
            Assert.IsFalse(events[1].HasCurrentValue);
        }

        [Test]
        public void AiBlackboard_InvalidKeysAndTypeMismatchAreRejected()
        {
            var invalid = new AiFactKey(string.Empty);
            var typed = new AiFactKey("typed");
            var blackboard = new AiBlackboard();

            Assert.Throws<ArgumentException>(() => blackboard.SetValue(invalid, true));
            Assert.IsFalse(blackboard.Contains(invalid));
            Assert.IsFalse(blackboard.TryGetValue(invalid, out bool invalidValue));
            Assert.IsNull(blackboard.GetRawValue(invalid));
            Assert.IsFalse(blackboard.Remove(invalid));

            blackboard.SetValue(typed, 10);

            Assert.IsFalse(blackboard.TryGetValue(typed, out string wrongType));
            Assert.IsNull(wrongType);
            Assert.AreEqual(10, blackboard.GetRawValue(typed));
        }

        [Test]
        public void AiSensorManager_TicksByPriorityAndInterval()
        {
            var log = new List<string>();
            var manager = new AiSensorManager();
            var slow = new RecordingSensor("slow", log);
            var fast = new RecordingSensor("fast", log);
            manager.Register(slow, intervalFrames: 2, priority: 0, sensorId: "slow");
            manager.Register(fast, intervalFrames: 1, priority: 10, sensorId: "fast");
            var agent = new TestAgent(1, new AiWorldState());

            manager.Tick(agent, agent.WorldState, 0);
            manager.Tick(agent, agent.WorldState, 1);
            AiSensorManagerDiagnostics diagnostics = manager.Tick(agent, agent.WorldState, 2);

            CollectionAssert.AreEqual(
                new[] { "fast", "slow", "fast", "fast", "slow" },
                log);
            Assert.AreEqual(2, diagnostics.ExecutedCount);
            Assert.AreEqual(0, diagnostics.SkippedCount);
            Assert.AreEqual(0, diagnostics.FailedCount);
        }

        [Test]
        public void AiSensorManager_RecordsSensorFailureAndContinues()
        {
            var log = new List<string>();
            var manager = new AiSensorManager();
            manager.Register(new ThrowingSensor(), sensorId: "bad", priority: 10);
            manager.Register(new RecordingSensor("after", log), sensorId: "after");
            var agent = new TestAgent(1, new AiWorldState());

            AiSensorManagerDiagnostics diagnostics = manager.Tick(agent, agent.WorldState, 0);

            Assert.AreEqual(2, diagnostics.ExecutedCount);
            Assert.AreEqual(1, diagnostics.FailedCount);
            Assert.AreEqual("bad", diagnostics.LastFailureSensorId);
            CollectionAssert.AreEqual(new[] { "after" }, log);
        }

        [Test]
        public void AiSensorManager_UnregisterAndClearUpdateRegistrations()
        {
            var log = new List<string>();
            var manager = new AiSensorManager();
            var first = new RecordingSensor("first", log);
            var second = new RecordingSensor("second", log);
            manager.Register(first, sensorId: "first");
            manager.Register(second, sensorId: "second");

            Assert.IsTrue(manager.Unregister(first));
            Assert.IsFalse(manager.Unregister(first));
            manager.Tick(new TestAgent(1, new AiWorldState()), new AiWorldState(), 0);

            CollectionAssert.AreEqual(new[] { "second" }, log);
            Assert.AreEqual(1, manager.Count);
            Assert.AreEqual("second", manager.Registrations[0].SensorId);

            manager.Clear();

            Assert.AreEqual(0, manager.Count);
            Assert.AreEqual(0, manager.Registrations.Count);
        }

        [Test]
        public void AiSensorManager_ThrowPolicyPropagatesSensorException()
        {
            var manager = new AiSensorManager(throwOnSensorException: true);
            manager.Register(new ThrowingSensor(), sensorId: "bad");
            var agent = new TestAgent(1, new AiWorldState());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                manager.Tick(agent, agent.WorldState, 0));

            Assert.AreEqual("sensor failed", exception.Message);
        }

        [Test]
        public void AiSensorManager_SkipsSensorsUntilIntervalIsDue()
        {
            var log = new List<string>();
            var manager = new AiSensorManager();
            manager.Register(new RecordingSensor("slow", log), intervalFrames: 3, sensorId: "slow");
            var agent = new TestAgent(1, new AiWorldState());

            manager.Tick(agent, agent.WorldState, 0);
            AiSensorManagerDiagnostics skipped = manager.Tick(agent, agent.WorldState, 2);
            AiSensorManagerDiagnostics due = manager.Tick(agent, agent.WorldState, 3);

            CollectionAssert.AreEqual(new[] { "slow", "slow" }, log);
            Assert.AreEqual(0, skipped.ExecutedCount);
            Assert.AreEqual(1, skipped.SkippedCount);
            Assert.AreEqual(1, due.ExecutedCount);
            Assert.AreEqual(0, due.SkippedCount);
        }

        [Test]
        public void AiSensorManager_TickDoesNotAllocateAfterRegistration()
        {
            var manager = new AiSensorManager();
            var low = new CountingSensor();
            var high = new CountingSensor();
            manager.Register(low, priority: 0, sensorId: "low");
            manager.Register(high, priority: 10, sensorId: "high");
            var agent = new TestAgent(1, new AiWorldState());

            manager.Tick(agent, agent.WorldState, 0);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 64; i++)
            {
                manager.Tick(agent, agent.WorldState, i);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(65, low.Count);
            Assert.AreEqual(65, high.Count);
        }

        [Test]
        public void AiUtilityPlanner_SelectsHighestScoringExecutableAction()
        {
            var canAttack = new AiFactKey("can.attack");
            var done = new AiFactKey("done");
            var world = new AiWorldState();
            world.SetValue(canAttack, true);
            world.SetValue(done, false);
            var actions = new IAiAction[]
            {
                new AiAction(10, 1f, effects: new[] { new AiSetFactEffect<bool>(done, true) }),
                new AiAction(
                    20,
                    2f,
                    new[] { new AiFactCondition<bool>(canAttack, true) },
                    new[] { new AiSetFactEffect<bool>(done, true) }),
                new AiAction(
                    30,
                    0.5f,
                    new[] { new AiFactCondition<bool>(canAttack, false) },
                    new[] { new AiSetFactEffect<bool>(done, true) })
            };
            var planner = new AiUtilityPlanner(new[]
            {
                new ScoreByActionId(new Dictionary<int, float>
                {
                    { 10, 0.4f },
                    { 20, 0.9f },
                    { 30, 2f }
                })
            });

            bool planned = planner.TryPlan(
                world,
                new[] { new AiFactGoal<bool>(1, 1f, done, true) },
                actions,
                out AiPlan plan);

            Assert.IsTrue(planned);
            Assert.AreEqual(20, plan.Actions[0].Id);
        }

        [Test]
        public void AiUtilityPlanner_TieBreaksByCostThenActionIdAndSanitizesInvalidScores()
        {
            var done = new AiFactKey("done");
            var world = new AiWorldState();
            world.SetValue(done, false);
            var planner = new AiUtilityPlanner(new[]
            {
                new ScoreByActionId(new Dictionary<int, float>
                {
                    { 10, float.NaN },
                    { 20, 1f },
                    { 30, 1f }
                })
            });

            bool planned = planner.TryPlan(
                world,
                new[] { new AiFactGoal<bool>(1, 1f, done, true) },
                new IAiAction[]
                {
                    new AiAction(10, 0.1f),
                    new AiAction(20, 5f),
                    new AiAction(30, 2f)
                },
                out AiPlan plan);

            Assert.IsTrue(planned);
            Assert.AreEqual(30, plan.Actions[0].Id);
        }

        [Test]
        public void AiUtilityPlanner_ReturnsFalseForEmptyActionsAndSatisfiedGoals()
        {
            var done = new AiFactKey("done");
            var world = new AiWorldState();
            world.SetValue(done, false);
            var planner = new AiUtilityPlanner();

            bool noActions = planner.TryPlan(
                world,
                new[] { new AiFactGoal<bool>(1, 1f, done, true) },
                Array.Empty<IAiAction>(),
                out AiPlan noActionPlan);
            world.SetValue(done, true);
            bool satisfiedGoal = planner.TryPlan(
                world,
                new[] { new AiFactGoal<bool>(1, 1f, done, true) },
                new[] { new AiAction(10, 1f) },
                out AiPlan satisfiedPlan);

            Assert.IsFalse(noActions);
            Assert.IsNull(noActionPlan);
            Assert.IsFalse(satisfiedGoal);
            Assert.IsNull(satisfiedPlan);
        }

        [Test]
        public void AiUtilityPlanner_SanitizesInfinityAndAllowsNegativeWeightedScores()
        {
            var done = new AiFactKey("done");
            var world = new AiWorldState();
            world.SetValue(done, false);
            var planner = new AiUtilityPlanner(new[]
            {
                new ScoreByActionId(new Dictionary<int, float>
                {
                    { 10, float.PositiveInfinity },
                    { 20, -1f },
                    { 30, -2f }
                })
            });

            bool planned = planner.TryPlan(
                world,
                new[] { new AiFactGoal<bool>(1, 1f, done, true) },
                new IAiAction[]
                {
                    new AiAction(10, 3f),
                    new AiAction(20, 1f),
                    new AiAction(30, 0.5f)
                },
                out AiPlan plan);

            Assert.IsTrue(planned);
            Assert.AreEqual(10, plan.Actions[0].Id);
        }

        [Test]
        public void AiSequenceActionRunner_AppliesPlanInOrderAndCompletes()
        {
            var first = new AiFactKey("first");
            var second = new AiFactKey("second");
            var world = new AiWorldState();
            var runner = new AiSequenceActionRunner();
            var agent = new TestAgent(1, world);
            var plan = new AiPlan(
                new AlwaysGoal(1),
                new IAiAction[]
                {
                    new AiAction(10, 1f, effects: new[] { new AiSetFactEffect<bool>(first, true) }),
                    new AiAction(
                        20,
                        1f,
                        new[] { new AiFactCondition<bool>(first, true) },
                        new[] { new AiSetFactEffect<bool>(second, true) })
                },
                totalCost: 2f);

            Assert.IsTrue(runner.Start(plan, agent, world));
            Assert.AreEqual(AiActionRunnerState.Running, runner.Tick(agent, world).State);
            AiActionRunnerStatus final = runner.Tick(agent, world);

            Assert.AreEqual(AiActionRunnerState.Completed, final.State);
            Assert.IsTrue(world.TryGetValue(second, out bool secondValue));
            Assert.IsTrue(secondValue);
        }

        [Test]
        public void AiSequenceActionRunner_CancelEmptyPlanAndFailedPreconditionExposeStatus()
        {
            var required = new AiFactKey("required");
            var world = new AiWorldState();
            var agent = new TestAgent(1, world);
            var runner = new AiSequenceActionRunner();

            Assert.IsFalse(runner.Start(AiPlan.Empty(new AlwaysGoal(1)), agent, world));
            Assert.AreEqual(AiActionRunnerState.Failed, runner.Status.State);
            Assert.AreEqual("Plan is empty.", runner.Status.Reason);

            var runningPlan = new AiPlan(
                new AlwaysGoal(1),
                new[] { new AiAction(10, 1f), new AiAction(20, 1f) },
                totalCost: 2f);
            Assert.IsTrue(runner.Start(runningPlan, agent, world));
            Assert.AreEqual(AiActionRunnerState.Canceled, runner.Cancel("manual").State);
            Assert.AreEqual("manual", runner.Status.Reason);

            var blockedPlan = new AiPlan(
                new AlwaysGoal(1),
                new IAiAction[]
                {
                    new AiAction(
                        30,
                        1f,
                        new[] { new AiFactCondition<bool>(required, true) })
                },
                totalCost: 1f);
            Assert.IsTrue(runner.Start(blockedPlan, agent, world));
            AiActionRunnerStatus failed = runner.Tick(agent, world);

            Assert.AreEqual(AiActionRunnerState.Failed, failed.State);
            Assert.AreEqual(30, failed.ActiveActionId);
            Assert.AreEqual("Action preconditions are not satisfied.", failed.Reason);
        }

        [Test]
        public void AiSequenceActionRunner_TickDoesNotAllocateForRunningPlan()
        {
            var action = new AiAction(10, 1f);
            var plan = new AiPlan(
                new AlwaysGoal(1),
                new[] { action, action, action, action, action },
                totalCost: 5f);
            var world = new AiWorldState();
            var agent = new TestAgent(1, world);
            var runner = new AiSequenceActionRunner();
            Assert.IsTrue(runner.Start(plan, agent, world));
            Assert.AreEqual(AiActionRunnerState.Running, runner.Tick(agent, world).State);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            while (runner.Status.State == AiActionRunnerState.Running)
            {
                runner.Tick(agent, world);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(AiActionRunnerState.Completed, runner.Status.State);
        }

        [Test]
        public void AiAgentBrain_TicksSensorsPlansAndRunsSelectedAction()
        {
            var sensed = new AiFactKey("target.sensed");
            var done = new AiFactKey("done");
            var world = new AiWorldState();
            world.SetValue(done, false);
            var manager = new AiSensorManager();
            manager.Register(new SetFactSensor(sensed, true), sensorId: "target");
            var brain = new AiAgentBrain(
                7,
                world,
                new SequentialPlanner(maxDepth: 2),
                new[] { new AiFactGoal<bool>(1, 1f, done, true) },
                new IAiAction[]
                {
                    new AiAction(
                        100,
                        1f,
                        new[] { new AiFactCondition<bool>(sensed, true) },
                        new[] { new AiSetFactEffect<bool>(done, true) })
                },
                sensors: manager);

            AiAgentBrainTickResult result = brain.Tick(0);

            Assert.IsTrue(result.Planned);
            Assert.AreEqual(100, result.SelectedActionId);
            Assert.AreEqual(AiActionRunnerState.Completed, result.RunnerStatus.State);
            Assert.AreEqual(1, result.SensorDiagnostics.ExecutedCount);
            Assert.IsTrue(world.TryGetValue(done, out bool doneValue));
            Assert.IsTrue(doneValue);
        }

        [Test]
        public void AiAgentBrain_RunningRunnerSuppressesReplanning()
        {
            var first = new AiFactKey("first");
            var second = new AiFactKey("second");
            var world = new AiWorldState();
            var plan = new AiPlan(
                new AlwaysGoal(1),
                new IAiAction[]
                {
                    new AiAction(10, 1f, effects: new[] { new AiSetFactEffect<bool>(first, true) }),
                    new AiAction(
                        20,
                        1f,
                        new[] { new AiFactCondition<bool>(first, true) },
                        new[] { new AiSetFactEffect<bool>(second, true) })
                },
                totalCost: 2f);
            var planner = new FixedPlanner(plan);
            var brain = new AiAgentBrain(
                7,
                world,
                planner,
                new[] { new AlwaysGoal(1) },
                Array.Empty<IAiAction>());

            AiAgentBrainTickResult firstTick = brain.Tick(0);
            AiAgentBrainTickResult secondTick = brain.Tick(1);

            Assert.IsTrue(firstTick.Planned);
            Assert.IsFalse(secondTick.Planned);
            Assert.AreEqual(1, planner.CallCount);
            Assert.AreEqual(20, secondTick.SelectedActionId);
            Assert.AreEqual(AiActionRunnerState.Completed, secondTick.RunnerStatus.State);
            Assert.IsTrue(world.TryGetValue(second, out bool secondValue));
            Assert.IsTrue(secondValue);
        }

        [Test]
        public void AiAgentBrain_ReportsPlanUnavailableAndEmptyPlan()
        {
            var world = new AiWorldState();
            var unavailable = new AiAgentBrain(
                7,
                world,
                new EmptyPlanner(),
                new[] { new AlwaysGoal(1) },
                Array.Empty<IAiAction>());

            AiAgentBrainTickResult unavailableResult = unavailable.Tick(0);

            Assert.IsFalse(unavailableResult.Planned);
            Assert.AreEqual("PlanUnavailable", unavailableResult.SuppressedReason);
            Assert.IsNull(unavailableResult.Plan);

            var emptyPlanBrain = new AiAgentBrain(
                8,
                new AiWorldState(),
                new FixedPlanner(AiPlan.Empty(new AlwaysGoal(1))),
                new[] { new AlwaysGoal(1) },
                Array.Empty<IAiAction>());

            AiAgentBrainTickResult emptyResult = emptyPlanBrain.Tick(0);

            Assert.IsFalse(emptyResult.Planned);
            Assert.AreEqual("EmptyPlan", emptyResult.SuppressedReason);
            Assert.NotNull(emptyResult.Plan);
            Assert.AreEqual(0, emptyResult.Plan.Actions.Count);
        }

        [Test]
        public void AiHfsmStateMachine_TransitionsWithinParentAndPreservesEnterExitOrder()
        {
            var shouldAttack = new AiFactKey("should.attack");
            var log = new List<string>();
            var world = new AiWorldState();
            world.SetValue(shouldAttack, false);
            var states = new IAiHfsmState[]
            {
                State("combat", string.Empty, log),
                State("chase", "combat", log),
                State("attack", "combat", log)
            };
            var machine = new AiHfsmStateMachine(
                states,
                3,
                new[]
                {
                    new AiHfsmTransition(
                        3,
                        4,
                        10,
                        context => context.WorldState.TryGetValue(shouldAttack, out bool value) && value)
                });
            var agent = new TestAgent(1, world);

            machine.Tick(agent, world, 0);
            world.SetValue(shouldAttack, true);
            machine.Tick(agent, world, 1);

            CollectionAssert.AreEqual(
                new[]
                {
                    "enter:combat",
                    "enter:chase",
                    "tick:chase",
                    "exit:chase",
                    "enter:attack",
                    "tick:attack"
                },
                log);
            Assert.AreEqual(4, machine.CurrentStateId);
        }

        [Test]
        public void AiHfsmStateMachine_TickDoesNotAllocateForCachedTransitionPath()
        {
            var root = new CountingHfsmState("root");
            var idle = new CountingHfsmState("idle", "root");
            var attack = new CountingHfsmState("attack", "root");
            var machine = new AiHfsmStateMachine(
                new IAiHfsmState[] { root, idle, attack },
                5,
                new[] { new FrameTransition(5, 4, minFrame: 2) });
            var agent = new TestAgent(1, new AiWorldState());

            machine.Tick(agent, agent.WorldState, 0);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            machine.Tick(agent, agent.WorldState, 2);
            machine.Tick(agent, agent.WorldState, 3);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(4, machine.CurrentStateId);
            Assert.AreEqual(1, idle.ExitCount);
            Assert.AreEqual(1, attack.EnterCount);
            Assert.AreEqual(2, attack.TickCount);
        }

        [Test]
        public void AiHfsmStateMachine_RejectsParentCycles()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                new AiHfsmStateMachine(
                    new IAiHfsmState[]
                    {
                        new AiHfsmState(7, parentId: 8),
                        new AiHfsmState(8, parentId: 7)
                    },
                    7));

            StringAssert.Contains("parent cycle", exception.Message);
        }

        [Test]
        public void AiHfsmStateMachine_ChoosesHighestPriorityTransitionAndSupportsGlobalSource()
        {
            var log = new List<string>();
            var states = new IAiHfsmState[]
            {
                State("idle", string.Empty, log),
                State("patrol", string.Empty, log),
                State("attack", string.Empty, log)
            };
            var machine = new AiHfsmStateMachine(
                states,
                5,
                new[]
                {
                    new AiHfsmTransition(5, 6, 10, _ => true),
                    new AiHfsmTransition(0, 4, 20, _ => true)
                });
            var agent = new TestAgent(1, new AiWorldState());

            machine.Tick(agent, agent.WorldState, 0);

            CollectionAssert.AreEqual(
                new[] { "enter:idle", "exit:idle", "enter:attack", "tick:attack" },
                log);
            Assert.AreEqual(4, machine.CurrentStateId);
        }

        [Test]
        public void AiHfsmStateMachine_RejectsMissingParentAndIgnoresSelfTransition()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                new AiHfsmStateMachine(
                    new[] { new AiHfsmState(9, parentId: 10) },
                    9));
            StringAssert.Contains("missing parent", exception.Message);

            var log = new List<string>();
            var machine = new AiHfsmStateMachine(
                new[] { State("idle", string.Empty, log) },
                5);
            var agent = new TestAgent(1, new AiWorldState());

            bool transitioned = machine.TransitionTo(5, agent, agent.WorldState, 0);

            Assert.IsFalse(transitioned);
            CollectionAssert.AreEqual(new[] { "enter:idle" }, log);
            Assert.AreEqual(5, machine.CurrentStateId);
        }

        [Test]
        public void AiHfsmPlannerAdapter_ExposesCurrentStateActionAsPlan()
        {
            var done = new AiFactKey("done");
            var world = new AiWorldState();
            world.SetValue(done, false);
            var stateAction = new AiAction(501, 3f);
            var machine = new AiHfsmStateMachine(
                new[] { new AiHfsmState(4, action: stateAction) },
                4);
            var adapter = new AiHfsmPlannerAdapter(machine);

            bool planned = adapter.TryPlan(
                world,
                new[] { new AiFactGoal<bool>(1, 1f, done, true) },
                Array.Empty<IAiAction>(),
                out AiPlan plan);

            Assert.IsTrue(planned);
            Assert.AreEqual(501, plan.Actions[0].Id);
        }

        private static AiHfsmState State(string id, string parentId, List<string> log)
        {
            return new AiHfsmState(
                StateId(id),
                string.IsNullOrEmpty(parentId) ? 0 : StateId(parentId),
                onEnter: _ => log.Add("enter:" + id),
                onTick: _ => log.Add("tick:" + id),
                onExit: _ => log.Add("exit:" + id));
        }

        private static int StateId(string id)
        {
            switch (id)
            {
                case "combat": return 1;
                case "chase": return 3;
                case "attack": return 4;
                case "root": return 2;
                case "idle": return 5;
                case "patrol": return 6;
                default: return 100;
            }
        }

        private sealed class TestAgent : IAiAgent
        {
            public TestAgent(int id, IAiWorldState worldState)
            {
                Id = id;
                WorldState = worldState;
            }

            public int Id { get; }
            public IAiWorldState WorldState { get; }
        }

        private sealed class AlwaysGoal : IAiGoal
        {
            public AlwaysGoal(int id)
            {
                Id = id;
            }

            public int Id { get; }
            public float Priority => 1f;
            public bool IsRelevant(IAiWorldState worldState) => true;
            public bool IsSatisfied(IAiWorldState worldState) => false;
        }

        private sealed class FixedPlanner : IAiPlanner
        {
            private readonly AiPlan _plan;

            public FixedPlanner(AiPlan plan)
            {
                _plan = plan;
            }

            public int CallCount { get; private set; }

            public bool TryPlan(
                IAiWorldState worldState,
                IEnumerable<IAiGoal> goals,
                IEnumerable<IAiAction> actions,
                out AiPlan plan)
            {
                CallCount++;
                plan = _plan;
                return _plan != null;
            }
        }

        private sealed class EmptyPlanner : IAiPlanner
        {
            public bool TryPlan(
                IAiWorldState worldState,
                IEnumerable<IAiGoal> goals,
                IEnumerable<IAiAction> actions,
                out AiPlan plan)
            {
                plan = null;
                return false;
            }
        }

        private sealed class RecordingSensor : IAiSensor
        {
            private readonly string _value;
            private readonly List<string> _log;

            public RecordingSensor(string value, List<string> log)
            {
                _value = value;
                _log = log;
            }

            public void Sense(IAiAgent agent, IAiWorldState worldState)
            {
                _log.Add(_value);
            }
        }

        private sealed class SetFactSensor : IAiSensor
        {
            private readonly AiFactKey _key;
            private readonly bool _value;

            public SetFactSensor(AiFactKey key, bool value)
            {
                _key = key;
                _value = value;
            }

            public void Sense(IAiAgent agent, IAiWorldState worldState)
            {
                worldState.SetValue(_key, _value);
            }
        }

        private sealed class ThrowingSensor : IAiSensor
        {
            public void Sense(IAiAgent agent, IAiWorldState worldState)
            {
                throw new InvalidOperationException("sensor failed");
            }
        }

        private sealed class CountingSensor : IAiSensor
        {
            public int Count { get; private set; }

            public void Sense(IAiAgent agent, IAiWorldState worldState)
            {
                Count++;
            }
        }

        private sealed class CountingHfsmState : IAiHfsmState
        {
            public CountingHfsmState(string id, string parentId = "")
            {
                Id = StateId(id);
                ParentId = string.IsNullOrEmpty(parentId) ? 0 : StateId(parentId);
            }

            public int Id { get; }
            public int ParentId { get; }
            public IAiAction Action => null;
            public int EnterCount { get; private set; }
            public int TickCount { get; private set; }
            public int ExitCount { get; private set; }

            public void OnEnter(AiHfsmContext context)
            {
                EnterCount++;
            }

            public void OnTick(AiHfsmContext context)
            {
                TickCount++;
            }

            public void OnExit(AiHfsmContext context)
            {
                ExitCount++;
            }
        }

        private sealed class FrameTransition : IAiHfsmTransition
        {
            private readonly int _minFrame;

            public FrameTransition(int sourceStateId, int targetStateId, int minFrame)
            {
                SourceStateId = sourceStateId;
                TargetStateId = targetStateId;
                _minFrame = minFrame;
            }

            public int SourceStateId { get; }
            public int TargetStateId { get; }
            public int Priority => 0;

            public bool CanTransition(AiHfsmContext context)
            {
                return context.Frame >= _minFrame;
            }
        }

        private sealed class ScoreByActionId : IAiUtilityConsideration
        {
            private readonly Dictionary<int, float> _scores;

            public ScoreByActionId(Dictionary<int, float> scores)
            {
                _scores = scores;
            }

            public float Weight => 1f;

            public float Score(IAiAction action, IAiWorldState worldState, IAiGoal goal)
            {
                return _scores.TryGetValue(action.Id, out float score) ? score : 0f;
            }
        }
    }
}
