using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class AbilityEffectTimelineTests
    {
        private const int DoubleExplosionTimelineId = 910001;
        private const int LongRunningTimelineId = 910002;
        private const int ParentTimelineId = 910003;
        private const int ChildTimelineId = 910004;
        private const int OwnerAnchorTimelineId = 910005;
        private const int RequiresOwnerTimelineId = 910006;
        private const int StartChildEventId = 910101;
        private const int ChildEventId = 910102;
        private const int OwnerPulseEventId = 910103;
        private const int OwnerOnlyEventId = 910104;
        private const int AbilityId = 910201;
        private const int TargetSelfPolicyId = 910202;
        private const int TimelinePresentationId = 910203;
        [Test]
        public void Runtime_DispatchesFrameZeroAndFrame120WithCapturedAnchor()
        {
            var sink = new RecordingSink();
            var module = new AbilityEffectTimelineRuntimeModule(
                new AbilityEffectTimelineRegistry(new[]
                {
                    new AbilityEffectTimelineDefinition(
                        DoubleExplosionTimelineId,
                        121,
                        new[]
                        {
                            Pulse("first", 0),
                            Pulse("second", 120)
                        })
                }),
                sink);

            FixVector3 anchor = Vec(10, 0, 3);
            AbilityEffectTimelineStartResult start = module.StartTimeline(
                new AbilityEffectTimelineContext(
                    91,
                    DoubleExplosionTimelineId,
                    ownerEntityId: 7,
                    sourceEntityId: 7,
                    triggerEntityId: 0,
                    AbilityEffectTimelineTriggerKind.CharacterActionEvent,
                    anchor,
                    Vec(0, 0, 1)),
                RuntimeFrame.Zero);

            Assert.IsTrue(start.Success);
            for (int frame = 0; frame <= 120; frame++)
                module.Tick(Tick(frame));

            Assert.AreEqual(2, sink.Dispatches.Count);
            Assert.AreEqual(0, sink.Dispatches[0].LocalFrame);
            Assert.AreEqual(120, sink.Dispatches[1].LocalFrame);
            Assert.AreEqual(anchor, sink.Dispatches[0].AnchorPosition);
            Assert.AreEqual(anchor, sink.Dispatches[1].AnchorPosition);
            Assert.AreEqual(0, module.ActiveInstanceCount);
        }

        [Test]
        public void Runtime_ContainsActiveInstanceTracksPendingAndCompletedInstances()
        {
            var module = new AbilityEffectTimelineRuntimeModule(
                new AbilityEffectTimelineRegistry(new[]
                {
                    new AbilityEffectTimelineDefinition(
                        LongRunningTimelineId,
                        2,
                        null)
                }));

            AbilityEffectTimelineStartResult start = module.StartTimeline(
                new AbilityEffectTimelineContext(
                    91,
                    LongRunningTimelineId,
                    ownerEntityId: 7,
                    sourceEntityId: 7,
                    triggerEntityId: 0,
                    AbilityEffectTimelineTriggerKind.CharacterActionEvent,
                    Vec(0, 0, 0),
                    Vec(0, 0, 1)),
                RuntimeFrame.Zero);

            Assert.IsTrue(start.Success);
            Assert.IsTrue(module.ContainsActiveInstance(start.InstanceId));
            module.Tick(Tick(0));
            Assert.IsTrue(module.ContainsActiveInstance(start.InstanceId));
            module.Tick(Tick(1));
            Assert.IsFalse(module.ContainsActiveInstance(start.InstanceId));
        }

        [Test]
        public void Runtime_StartEffectTimeline_PreservesSourceAndUsesParentAnchor()
        {
            var sink = new RecordingSink();
            var registry = new AbilityEffectTimelineRegistry(new[]
            {
                new AbilityEffectTimelineDefinition(
                    ParentTimelineId,
                    2,
                    new[]
                    {
                        new AbilityEffectTimelineEvent(
                            StartChildEventId,
                            AbilityEffectTimelineTrackKind.Gameplay,
                            AbilityEffectTimelineEventKind.StartEffectTimeline,
                            frame: 0,
                            localOffset: Vec(1, 0, 0),
                            targetTimelineId: ChildTimelineId,
                            eventLabel: "start_child",
                            targetTimelineLabel: "test.child")
                    }),
                new AbilityEffectTimelineDefinition(
                    ChildTimelineId,
                    1,
                    new[]
                    {
                        new AbilityEffectTimelineEvent(
                            ChildEventId,
                            AbilityEffectTimelineTrackKind.Gameplay,
                            AbilityEffectTimelineEventKind.EmitGameplayEvent,
                            frame: 0)
                    })
            });
            var module = new AbilityEffectTimelineRuntimeModule(registry, sink);

            AbilityEffectTimelineStartResult start = module.StartTimeline(
                new AbilityEffectTimelineContext(
                    91,
                    ParentTimelineId,
                    ownerEntityId: 7,
                    sourceEntityId: 42,
                    triggerEntityId: 1001,
                    AbilityEffectTimelineTriggerKind.ProjectileImpact,
                    Vec(5, 0, 0),
                    Vec(0, 0, 1)),
                RuntimeFrame.Zero);

            module.Tick(Tick(0));
            module.Tick(Tick(1));

            Assert.IsTrue(start.Success);
            Assert.AreEqual(2, sink.Dispatches.Count);
            AbilityEffectTimelineEventDispatch child = sink.Dispatches[1];
            Assert.AreEqual("child_event", child.Event.EventId);
            Assert.AreEqual(start.InstanceId, child.Context.ParentInstanceId);
            Assert.AreEqual(42, child.Context.SourceEntityId);
            Assert.AreEqual(Vec(6, 0, 0), child.AnchorPosition);
        }

        [Test]
        public void Runtime_AnchorResolverSupportsOwnerCurrentMode()
        {
            var sink = new RecordingSink();
            var resolver = new FixedAnchorResolver(Vec(20, 0, 0));
            var module = new AbilityEffectTimelineRuntimeModule(
                new AbilityEffectTimelineRegistry(new[]
                {
                    new AbilityEffectTimelineDefinition(
                        OwnerAnchorTimelineId,
                        1,
                        new[]
                        {
                            new AbilityEffectTimelineEvent(
                                OwnerPulseEventId,
                                AbilityEffectTimelineTrackKind.Gameplay,
                                AbilityEffectTimelineEventKind.EmitGameplayEvent,
                                frame: 0,
                                anchorMode: AbilityEffectTimelineAnchorMode.OwnerCurrent,
                                localOffset: Vec(2, 0, 0),
                                eventLabel: "owner_pulse")
                        })
                }),
                sink,
                anchorResolver: resolver);

            module.StartTimeline(
                new AbilityEffectTimelineContext(
                    91,
                    OwnerAnchorTimelineId,
                    ownerEntityId: 7,
                    sourceEntityId: 7,
                    triggerEntityId: 0,
                    AbilityEffectTimelineTriggerKind.CharacterActionEvent,
                    Vec(1, 0, 0),
                    Vec(0, 0, 1)),
                RuntimeFrame.Zero);
            module.Tick(Tick(0));

            Assert.AreEqual(1, sink.Dispatches.Count);
            Assert.AreEqual(Vec(22, 0, 0), sink.Dispatches[0].AnchorPosition);
            Assert.AreEqual(1, resolver.QueryCount);
        }

        [Test]
        public void Runtime_SkipsOwnerRequiredEventWhenOwnerContextMissing()
        {
            var sink = new RecordingSink();
            var module = new AbilityEffectTimelineRuntimeModule(
                new AbilityEffectTimelineRegistry(new[]
                {
                    new AbilityEffectTimelineDefinition(
                        RequiresOwnerTimelineId,
                        1,
                        new[]
                        {
                            new AbilityEffectTimelineEvent(
                                OwnerOnlyEventId,
                                AbilityEffectTimelineTrackKind.Gameplay,
                                AbilityEffectTimelineEventKind.EmitGameplayEvent,
                                frame: 0,
                                requiresOwner: true,
                                eventLabel: "owner_only")
                        })
                }),
                sink);

            module.StartTimeline(
                new AbilityEffectTimelineContext(
                    91,
                    RequiresOwnerTimelineId,
                    ownerEntityId: 0,
                    sourceEntityId: 7,
                    triggerEntityId: 0,
                    AbilityEffectTimelineTriggerKind.ProjectileExpire,
                    Vec(1, 0, 0),
                    Vec(0, 0, 1)),
                RuntimeFrame.Zero);
            module.Tick(Tick(0));

            Assert.AreEqual(0, sink.Dispatches.Count);
        }

        [Test]
        public void AbilityRuntime_StartTimelineDoesNotCommitCooldown()
        {
            AbilityDefinitionConfig ability = new AbilityDefinitionConfig(
                AbilityId,
                "ability.test.double_explosion",
                "Double Explosion",
                new AbilityInputBindingConfig(920021, 920022, "CastDoubleExplosion", "debug"),
                new AbilityCooldownConfig(30, 920023, "test"),
                new AbilityCostConfig[0],
                TargetSelfPolicyId,
                new AbilityActionBindingConfig(MxFramework.Tests.CharacterActionLegacyFixtureIds.From("cast_double_explosion"), 920024, "CastDoubleExplosion"),
                default,
                TimelinePresentationId,
                DoubleExplosionTimelineId,
                targetPolicyLabel: "target.self",
                effectTimelineLabel: "test.double_explosion");
            var abilities = new AbilityDefinitionRegistry();
            Assert.IsTrue(abilities.TryRegister(ability, out string failure), failure);
            var abilityRuntime = new AbilityRuntimeModule(abilities);
            var timelineRuntime = new AbilityEffectTimelineRuntimeModule(
                new AbilityEffectTimelineRegistry(new[]
                {
                    new AbilityEffectTimelineDefinition(
                        DoubleExplosionTimelineId,
                        1,
                        new[] { Pulse("first", 0) })
                }));

            Assert.IsTrue(abilityRuntime.TryResolveInputIntent(
                7,
                920021,
                RuntimeFrame.Zero,
                out AbilityActivationResult activation));
            timelineRuntime.StartTimeline(
                new AbilityEffectTimelineContext(
                    activation.Ability.AbilityId,
                    activation.Ability.EffectTimelineId,
                    ownerEntityId: 7,
                    sourceEntityId: 7,
                    triggerEntityId: 0,
                    AbilityEffectTimelineTriggerKind.CharacterActionEvent,
                    FixVector3.Zero,
                    Vec(0, 0, 1)),
                RuntimeFrame.Zero);

            Assert.AreEqual(0L, abilityRuntime.GetRemainingCooldownFrames(7, ability, RuntimeFrame.Zero));

            abilityRuntime.CommitActivation(7, ability, RuntimeFrame.Zero);
            timelineRuntime.StartTimeline(
                new AbilityEffectTimelineContext(
                    ability.AbilityId,
                    ability.EffectTimelineId,
                    ownerEntityId: 7,
                    sourceEntityId: 7,
                    triggerEntityId: 0,
                    AbilityEffectTimelineTriggerKind.ProjectileImpact,
                    FixVector3.Zero,
                    Vec(0, 0, 1)),
                new RuntimeFrame(1));

            Assert.AreEqual(29L, abilityRuntime.GetRemainingCooldownFrames(7, ability, new RuntimeFrame(1)));
        }

        private static AbilityEffectTimelineEvent Pulse(string eventId, int frame)
        {
            return new AbilityEffectTimelineEvent(
                eventId == "first" ? 910110 : 910111,
                AbilityEffectTimelineTrackKind.Combat,
                AbilityEffectTimelineEventKind.AreaPulse,
                frame,
                shapeKind: AbilityEffectTimelineShapeKind.Sphere,
                radius: Fix64.One,
                layerMask: 256,
                eventLabel: eventId);
        }

        private static RuntimeTickContext Tick(long frame)
        {
            return new RuntimeTickContext(frame, 0d, 0d, RuntimeTickStage.Simulation);
        }

        private static FixVector3 Vec(long x, long y, long z)
        {
            return new FixVector3(Fix64.FromLong(x), Fix64.FromLong(y), Fix64.FromLong(z));
        }

        private sealed class RecordingSink : IAbilityEffectTimelineEventSink
        {
            public List<AbilityEffectTimelineEventDispatch> Dispatches { get; } =
                new List<AbilityEffectTimelineEventDispatch>();

            public void OnAbilityEffectTimelineEvent(AbilityEffectTimelineEventDispatch dispatch)
            {
                Dispatches.Add(dispatch);
            }
        }

        private sealed class FixedAnchorResolver : IAbilityEffectTimelineAnchorResolver
        {
            private readonly FixVector3 _anchor;

            public FixedAnchorResolver(FixVector3 anchor)
            {
                _anchor = anchor;
            }

            public int QueryCount { get; private set; }

            public bool TryResolveAbilityEffectTimelineAnchor(
                AbilityEffectTimelineAnchorQuery query,
                out FixVector3 anchorPosition)
            {
                QueryCount++;
                anchorPosition = _anchor;
                return true;
            }
        }
    }
}
