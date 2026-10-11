using System.Collections.Generic;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class AbilityRuntimeGraphExecutionTests
    {
        private const int AttrHp = 1;
        private const int AttrAttack = 2;
        private const int AttrDefense = 3;
        private const int AbilityStrike = 300001;
        private const int EffectDamage = 1001;
        private const int EffectFirst = 1002;
        private const int EffectSecond = 1003;
        private const int TagGrounded = 2001;
        private const int TagFlying = 2002;
        private const int StatusInvulnerable = 3001;

        [Test]
        public void Execute_SyntheticStrikeGraph_AppliesDamageAndEmitsTrace()
        {
            RuntimeEntity caster = CreateEntity(1, 1, hp: 1000, attack: 120, defense: 20);
            RuntimeEntity enemy = CreateEntity(2, 2, hp: 600, attack: 80, defense: 10);
            GameplayWorld world = CreateWorld(caster, enemy);
            AbilityGraphRuntimeEffectRegistry effects = CreateEffectRegistry();
            Assert.IsTrue(effects.TryRegister(EffectDamage, new DamageEffect(AttrAttack, AttrDefense, AttrHp), out string failure), failure);

            AbilityGraphExecutionResult result = Execute(
                CreateStrikeGraph(EffectDamage),
                world,
                caster.EntityId,
                effects);

            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.AreEqual(AbilityGraphExecutionFailureCode.None, result.FailureCode);
            Assert.AreEqual(1, result.SelectedTargets.Count);
            Assert.AreSame(enemy, result.SelectedTargets[0]);
            Assert.AreEqual(enemy.EntityId, result.TargetEntityIds[0]);
            Assert.AreEqual(490, enemy.Store.GetAttribute(AttrHp));
            Assert.AreEqual(1, result.EmittedEvents.Count);
            Assert.AreEqual(AbilityEventType.CastFinished, result.EmittedEvents[0].Type);
            AssertTrace(result, "entry", "target", "effect", "event");
        }

        [Test]
        public void Execute_TargetQuery_ReusesTargetingFilters()
        {
            RuntimeEntity caster = CreateEntity(1, 10);
            RuntimeEntity sameTeam = CreateEntity(2, 10);
            RuntimeEntity missingTag = CreateEntity(3, 20);
            RuntimeEntity blocked = CreateEntity(4, 20);
            RuntimeEntity valid = CreateEntity(5, 20);
            GameplayWorld world = CreateWorld(caster, sameTeam, missingTag, blocked, valid);
            var candidates = new[]
            {
                new GameplayTargetCandidate(sameTeam, tags: new[] { TagGrounded }, statuses: null),
                new GameplayTargetCandidate(missingTag, tags: new[] { TagFlying }, statuses: null),
                new GameplayTargetCandidate(blocked, tags: new[] { TagGrounded }, statuses: new[] { StatusInvulnerable }),
                new GameplayTargetCandidate(valid, tags: new[] { TagGrounded }, statuses: null),
            };

            var graph = new AbilityGraphDefinition(
                "target-filter",
                "entry",
                new[]
                {
                    Node("entry", AbilityGraphNodeKind.Entry),
                    Node("target", AbilityGraphNodeKind.TargetQuery, new AbilityGraphTargetQueryPayload(
                        GameplayTargetRelationFilter.Enemy,
                        requiredTags: new[] { TagGrounded },
                        blockedStatuses: new[] { StatusInvulnerable },
                        maxTargets: 1)),
                    Node("event", AbilityGraphNodeKind.EmitEvent, new AbilityGraphEmitEventPayload(AbilityEventType.TargetSelected)),
                },
                new[]
                {
                    Edge("entry", AbilityGraphPorts.Next, "target"),
                    Edge("target", AbilityGraphPorts.Next, "event"),
                });

            AbilityGraphExecutionResult result = Execute(
                graph,
                world,
                caster.EntityId,
                CreateEffectRegistry(),
                targetCandidates: candidates);

            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.AreEqual(1, result.SelectedTargets.Count);
            Assert.AreSame(valid, result.SelectedTargets[0]);
            Assert.AreEqual(3, result.RejectedTargets.Count);
            Assert.AreEqual(GameplayTargetRejectReason.SameTeam, result.RejectedTargets[0].Reason);
            Assert.AreEqual(GameplayTargetRejectReason.MissingRequiredTag, result.RejectedTargets[1].Reason);
            Assert.AreEqual(GameplayTargetRejectReason.BlockedStatus, result.RejectedTargets[2].Reason);
            Assert.AreEqual(AbilityEventType.TargetSelected, result.EmittedEvents[0].Type);
            Assert.AreSame(valid, result.EmittedEvents[0].Target);
        }

        [Test]
        public void Execute_Sequence_AppliesMultipleEffectsInStableEdgeOrder()
        {
            RuntimeEntity caster = CreateEntity(1, 1);
            RuntimeEntity enemy = CreateEntity(2, 2);
            GameplayWorld world = CreateWorld(caster, enemy);
            var order = new List<int>();
            AbilityGraphRuntimeEffectRegistry effects = CreateEffectRegistry();
            Assert.IsTrue(effects.TryRegister(EffectFirst, new RecordingEffect(EffectFirst, order), out string firstFailure), firstFailure);
            Assert.IsTrue(effects.TryRegister(EffectSecond, new RecordingEffect(EffectSecond, order), out string secondFailure), secondFailure);

            var graph = new AbilityGraphDefinition(
                "multi-effect",
                "entry",
                new[]
                {
                    Node("entry", AbilityGraphNodeKind.Entry),
                    Node("target", AbilityGraphNodeKind.TargetQuery, new AbilityGraphTargetQueryPayload(GameplayTargetRelationFilter.Enemy, maxTargets: 1)),
                    Node("sequence", AbilityGraphNodeKind.Sequence),
                    Node("effect-1", AbilityGraphNodeKind.ApplyEffect, new AbilityGraphApplyEffectPayload(EffectFirst)),
                    Node("effect-2", AbilityGraphNodeKind.ApplyEffect, new AbilityGraphApplyEffectPayload(EffectSecond)),
                },
                new[]
                {
                    Edge("entry", AbilityGraphPorts.Next, "target"),
                    Edge("target", AbilityGraphPorts.Next, "sequence"),
                    Edge("sequence", AbilityGraphPorts.Next, "effect-2"),
                    Edge("sequence", AbilityGraphPorts.Next, "effect-1"),
                });

            AbilityGraphExecutionResult result = Execute(graph, world, caster.EntityId, effects);

            Assert.IsTrue(result.Succeeded, result.FailureReason);
            CollectionAssert.AreEqual(new[] { EffectFirst, EffectSecond }, order);
            AssertTrace(result, "entry", "target", "sequence", "effect-1", "effect-2");
        }

        [Test]
        public void Execute_MissingCaster_ReturnsStructuredFailure()
        {
            RuntimeEntity enemy = CreateEntity(2, 2);
            GameplayWorld world = CreateWorld(enemy);

            AbilityGraphExecutionResult result = Execute(
                CreateStrikeGraph(EffectDamage),
                world,
                casterEntityId: 99,
                effects: CreateDamageRegistry());

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(AbilityGraphExecutionFailureCode.MissingCaster, result.FailureCode);
            Assert.AreEqual(AbilityGraphRuntimeExecutor.MissingCasterFailureReason, result.FailureReason);
            Assert.AreEqual(0, result.Trace.Count);
        }

        [Test]
        public void Execute_MissingTarget_ReturnsStructuredFailure()
        {
            RuntimeEntity caster = CreateEntity(1, 1);
            GameplayWorld world = CreateWorld(caster);

            AbilityGraphExecutionResult result = Execute(
                CreateStrikeGraph(EffectDamage),
                world,
                caster.EntityId,
                CreateDamageRegistry());

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(AbilityGraphExecutionFailureCode.MissingTarget, result.FailureCode);
            Assert.AreEqual(AbilityGraphRuntimeExecutor.MissingTargetFailureReason, result.FailureReason);
            Assert.AreEqual("target", result.Trace[result.Trace.Count - 1].NodeId);
        }

        [Test]
        public void Execute_MissingEffect_ReturnsStructuredFailure()
        {
            RuntimeEntity caster = CreateEntity(1, 1);
            RuntimeEntity enemy = CreateEntity(2, 2);
            GameplayWorld world = CreateWorld(caster, enemy);

            AbilityGraphExecutionResult result = Execute(
                CreateStrikeGraph(EffectDamage),
                world,
                caster.EntityId,
                CreateEffectRegistry());

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(AbilityGraphExecutionFailureCode.MissingEffect, result.FailureCode);
            Assert.AreEqual(AbilityGraphRuntimeExecutor.MissingEffectFailureReason, result.FailureReason);
            Assert.AreEqual(EffectDamage, result.Trace[result.Trace.Count - 1].EffectId);
        }

        [Test]
        public void Execute_ValidationFailure_BlocksExecution()
        {
            RuntimeEntity caster = CreateEntity(1, 1);
            RuntimeEntity enemy = CreateEntity(2, 2);
            GameplayWorld world = CreateWorld(caster, enemy);
            var invalidGraph = new AbilityGraphDefinition(
                "invalid",
                "entry",
                new[]
                {
                    Node("entry", AbilityGraphNodeKind.Entry),
                    Node("effect", AbilityGraphNodeKind.ApplyEffect, new AbilityGraphApplyEffectPayload(0)),
                },
                new[]
                {
                    Edge("entry", AbilityGraphPorts.Next, "effect"),
                });

            AbilityGraphExecutionResult result = Execute(invalidGraph, world, caster.EntityId, CreateDamageRegistry());

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(AbilityGraphExecutionFailureCode.ValidationFailed, result.FailureCode);
            Assert.IsNotNull(result.ValidationResult);
            Assert.IsTrue(result.ValidationResult.Contains(AbilityGraphValidationErrorCode.InvalidNodePayload));
            Assert.AreEqual(0, result.Trace.Count);
            Assert.AreEqual(600, enemy.Store.GetAttribute(AttrHp));
        }

        [Test]
        public void Execute_StepBudgetExceeded_ReturnsStructuredFailure()
        {
            RuntimeEntity caster = CreateEntity(1, 1);
            GameplayWorld world = CreateWorld(caster);
            var graph = new AbilityGraphDefinition(
                "budget",
                "entry",
                new[]
                {
                    Node("entry", AbilityGraphNodeKind.Entry),
                    Node("sequence", AbilityGraphNodeKind.Sequence),
                    Node("event", AbilityGraphNodeKind.EmitEvent, new AbilityGraphEmitEventPayload(AbilityEventType.CastFinished)),
                },
                new[]
                {
                    Edge("entry", AbilityGraphPorts.Next, "sequence"),
                    Edge("sequence", AbilityGraphPorts.Next, "event"),
                });

            var executor = new AbilityGraphRuntimeExecutor();
            var context = new AbilityGraphExecutionContext(world, caster.EntityId, AbilityStrike, CreateEffectRegistry());
            AbilityGraphExecutionResult result = executor.Execute(graph, context, stepBudget: 1);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(AbilityGraphExecutionFailureCode.StepBudgetExceeded, result.FailureCode);
            Assert.AreEqual(AbilityGraphRuntimeExecutor.StepBudgetExceededFailureReason, result.FailureReason);
            AssertTrace(result, "entry");
        }

        [Test]
        public void Execute_PhaseGate_InactiveUsesFailurePortWithoutPhaseTimelineDependency()
        {
            RuntimeEntity caster = CreateEntity(1, 1);
            GameplayWorld world = CreateWorld(caster);
            var graph = new AbilityGraphDefinition(
                "phase-gate",
                "entry",
                new[]
                {
                    Node("entry", AbilityGraphNodeKind.Entry),
                    Node("gate", AbilityGraphNodeKind.PhaseGate, new AbilityGraphPhaseGatePayload("windup")),
                    Node("failed", AbilityGraphNodeKind.EmitEvent, new AbilityGraphEmitEventPayload(AbilityEventType.CastFailed)),
                    Node("finished", AbilityGraphNodeKind.EmitEvent, new AbilityGraphEmitEventPayload(AbilityEventType.CastFinished)),
                },
                new[]
                {
                    Edge("entry", AbilityGraphPorts.Next, "gate"),
                    Edge("gate", AbilityGraphPorts.Success, "finished"),
                    Edge("gate", AbilityGraphPorts.Failure, "failed"),
                });
            var executor = new AbilityGraphRuntimeExecutor();
            var context = new AbilityGraphExecutionContext(
                world,
                caster.EntityId,
                AbilityStrike,
                CreateEffectRegistry(),
                phaseGate: new FixedPhaseGate(false));

            AbilityGraphExecutionResult result = executor.Execute(graph, context);

            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.AreEqual(AbilityEventType.CastFailed, result.EmittedEvents[0].Type);
            Assert.AreEqual(AbilityGraphRuntimeExecutor.PhaseGateInactiveFailureReason, result.EmittedEvents[0].FailureReason);
            Assert.AreEqual("gate", result.Trace[1].NodeId);
            Assert.AreEqual(AbilityGraphPorts.Failure, result.Trace[1].OutputPort);
            AssertTrace(result, "entry", "gate", "failed");
        }

        private static AbilityGraphExecutionResult Execute(
            AbilityGraphDefinition graph,
            GameplayWorld world,
            int casterEntityId,
            IAbilityGraphEffectResolver effects,
            IReadOnlyList<GameplayTargetCandidate> targetCandidates = null)
        {
            var executor = new AbilityGraphRuntimeExecutor();
            var context = new AbilityGraphExecutionContext(
                world,
                casterEntityId,
                AbilityStrike,
                effects,
                targetCandidates: targetCandidates);
            return executor.Execute(graph, context);
        }

        private static AbilityGraphDefinition CreateStrikeGraph(int effectId)
        {
            return new AbilityGraphDefinition(
                "strike",
                "entry",
                new[]
                {
                    Node("entry", AbilityGraphNodeKind.Entry),
                    Node("target", AbilityGraphNodeKind.TargetQuery, new AbilityGraphTargetQueryPayload(GameplayTargetRelationFilter.Enemy, maxTargets: 1)),
                    Node("effect", AbilityGraphNodeKind.ApplyEffect, new AbilityGraphApplyEffectPayload(effectId)),
                    Node("event", AbilityGraphNodeKind.EmitEvent, new AbilityGraphEmitEventPayload(AbilityEventType.CastFinished)),
                },
                new[]
                {
                    Edge("entry", AbilityGraphPorts.Next, "target"),
                    Edge("target", AbilityGraphPorts.Next, "effect"),
                    Edge("effect", AbilityGraphPorts.Next, "event"),
                });
        }

        private static GameplayWorld CreateWorld(params RuntimeEntity[] entities)
        {
            var world = new GameplayWorld();
            for (int i = 0; i < entities.Length; i++)
                world.Register(entities[i]);

            return world;
        }

        private static RuntimeEntity CreateEntity(
            int id,
            int team,
            int hp = 600,
            int attack = 100,
            int defense = 10)
        {
            var entity = new RuntimeEntity(id, team, AttrHp);
            entity.Store.RegisterAttribute(AttrHp, hp);
            entity.Store.RegisterAttribute(AttrAttack, attack);
            entity.Store.RegisterAttribute(AttrDefense, defense);
            return entity;
        }

        private static AbilityGraphRuntimeEffectRegistry CreateDamageRegistry()
        {
            AbilityGraphRuntimeEffectRegistry effects = CreateEffectRegistry();
            Assert.IsTrue(effects.TryRegister(EffectDamage, new DamageEffect(AttrAttack, AttrDefense, AttrHp), out string failure), failure);
            return effects;
        }

        private static AbilityGraphRuntimeEffectRegistry CreateEffectRegistry()
        {
            return new AbilityGraphRuntimeEffectRegistry();
        }

        private static AbilityGraphNode Node(string nodeId, AbilityGraphNodeKind kind, object payload = null)
        {
            return new AbilityGraphNode(nodeId, kind, payload);
        }

        private static AbilityGraphEdge Edge(string fromNodeId, string outputPort, string toNodeId)
        {
            return new AbilityGraphEdge(fromNodeId, outputPort, toNodeId);
        }

        private static void AssertTrace(AbilityGraphExecutionResult result, params string[] nodeIds)
        {
            Assert.AreEqual(nodeIds.Length, result.Trace.Count);
            for (int i = 0; i < nodeIds.Length; i++)
            {
                Assert.AreEqual(i, result.Trace[i].StepIndex);
                Assert.AreEqual(nodeIds[i], result.Trace[i].NodeId);
            }
        }

        private sealed class RecordingEffect : IAbilityEffect
        {
            private readonly int _effectId;
            private readonly List<int> _order;

            public RecordingEffect(int effectId, List<int> order)
            {
                _effectId = effectId;
                _order = order;
            }

            public void Apply(AbilityContext context, IRuntimeEntity target)
            {
                Assert.IsNotNull(context.Caster);
                Assert.IsNotNull(target);
                _order.Add(_effectId);
            }
        }

        private sealed class FixedPhaseGate : IAbilityGraphPhaseGate
        {
            private readonly bool _isActive;

            public FixedPhaseGate(bool isActive)
            {
                _isActive = isActive;
            }

            public bool IsPhaseActive(string phaseId)
            {
                Assert.AreEqual("windup", phaseId);
                return _isActive;
            }
        }
    }
}
