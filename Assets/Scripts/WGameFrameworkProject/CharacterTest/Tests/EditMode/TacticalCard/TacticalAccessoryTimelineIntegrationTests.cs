using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalAccessoryTimelineIntegrationTests
    {
        private static AbilityEffectTimelineContext Context(int id)
            => new AbilityEffectTimelineContext(9, id, 1, 1, 2, AbilityEffectTimelineTriggerKind.ProjectileImpact,
                FixVector3.Zero, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero));
        private static AbilityEffectTimelineDefinition Definition(int id, int frames = 1)
            => new AbilityEffectTimelineDefinition(id, frames, new[] { new AbilityEffectTimelineEvent(id + 100,
                AbilityEffectTimelineTrackKind.Gameplay, AbilityEffectTimelineEventKind.EmitGameplayEvent, 0) });
        private sealed class Sink : IAbilityEffectTimelineEventSink, IAbilityEffectTimelineStartGate
        {
            internal bool Accept = true;
            internal readonly List<AbilityEffectTimelineEventDispatch> Events = new List<AbilityEffectTimelineEventDispatch>();
            public bool TryAcceptTimelineStart(int id, in AbilityEffectTimelineContext context, RuntimeFrame frame) => Accept;
            public void OnAbilityEffectTimelineEvent(AbilityEffectTimelineEventDispatch e) => Events.Add(e);
        }
        [Test] public void FrameZeroPumpAdvancesNewChildButNotAnOlderTimeline()
        {
            var sink = new Sink(); var registry = new AbilityEffectTimelineRegistry(new[] { Definition(1, 10), Definition(2) });
            var runtime = new AbilityEffectTimelineRuntimeModule(registry, sink);
            Assert.IsTrue(runtime.StartTimeline(Context(1), new RuntimeFrame(100)).Success);
            runtime.Tick(new RuntimeTickContext(100, 0, 0, RuntimeTickStage.Simulation));
            int oldFrame = runtime.CreateSnapshot()[0].LocalFrame;
            Assert.IsTrue(runtime.StartTimeline(Context(2), new RuntimeFrame(100)).Success);
            Assert.AreEqual(1, runtime.PumpFrameZero(new RuntimeFrame(100)));
            Assert.AreEqual(oldFrame, runtime.CreateSnapshot()[0].LocalFrame);
            Assert.AreEqual(2, sink.Events.Count); Assert.AreEqual(0, runtime.PumpFrameZero(new RuntimeFrame(100)));
        }
        [Test] public void GateRejectsBeforeAllocatorOrPendingMutationAndRestoreDoesNotRewind()
        {
            var sink = new Sink { Accept = false }; var registry = new AbilityEffectTimelineRegistry(new[] { Definition(1) });
            var runtime = new AbilityEffectTimelineRuntimeModule(registry, sink);
            Assert.IsFalse(runtime.StartTimeline(Context(1), new RuntimeFrame(100)).Success);
            Assert.AreEqual(1, runtime.NextInstanceId); Assert.IsTrue(runtime.IsIdle);
            Assert.IsTrue(runtime.TryRestoreIdleIdentity(9)); sink.Accept = true;
            Assert.AreEqual(9, runtime.StartTimeline(Context(1), new RuntimeFrame(100)).InstanceId);
            Assert.IsFalse(runtime.TryRestoreIdleIdentity(1));
            runtime.PumpFrameZero(new RuntimeFrame(100)); Assert.IsTrue(runtime.IsIdle);
            Assert.IsFalse(runtime.TryRestoreIdleIdentity(1)); Assert.AreEqual(10, runtime.NextInstanceId);
        }
        [Test] public void PumpRunsFrameZeroGrandchildrenInSameFrameWithoutAnotherTick()
        {
            var parent = new AbilityEffectTimelineDefinition(1, 1, new[] { new AbilityEffectTimelineEvent(101,
                AbilityEffectTimelineTrackKind.Gameplay, AbilityEffectTimelineEventKind.StartEffectTimeline, 0, targetTimelineId: 2) });
            var sink = new Sink(); var runtime = new AbilityEffectTimelineRuntimeModule(
                new AbilityEffectTimelineRegistry(new[] { parent, Definition(2) }), sink);
            Assert.IsTrue(runtime.StartTimeline(Context(1), new RuntimeFrame(100)).Success);
            Assert.AreEqual(2, runtime.PumpFrameZero(new RuntimeFrame(100)));
            Assert.IsTrue(runtime.IsIdle); Assert.AreEqual(2, sink.Events.Count);
            Assert.AreEqual(100, sink.Events[1].RuntimeFrame.Value); Assert.AreEqual(1, sink.Events[1].Context.ParentInstanceId);
        }
    }
}
