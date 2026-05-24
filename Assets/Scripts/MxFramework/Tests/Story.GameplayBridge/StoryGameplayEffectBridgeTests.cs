using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Story.GameplayBridge;
using NUnit.Framework;

namespace MxFramework.Tests.StoryGameplayBridge
{
    public sealed class StoryGameplayEffectBridgeTests
    {
        private const int SourceId = 1003102;
        private const int HpAttributeId = 100;

        [Test]
        public void EnqueuesExistingGameplayCommand()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            var buffer = new RuntimeCommandBuffer();
            var bridge = new StoryGameplayEffectBridge(buffer, world);

            StoryGameplayEffectResult result = bridge.EnqueueGameplayEffect(
                StoryGameplayEffectIntent.AddComponentAttribute(
                    StoryGameplayEntityRef.ComponentEntity(entity),
                    SourceId,
                    HpAttributeId,
                    -10,
                    traceId: "story-hit"),
                new RuntimeFrame(5));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, buffer.PendingCount);
            Assert.AreEqual(new RuntimeFrame(5), result.Command.Frame);
            Assert.AreEqual(SourceId, result.Command.SourceId);
            Assert.AreEqual(GameplayRuntimeCommandIds.AddComponentAttribute, result.Command.CommandId);
            Assert.AreEqual(entity.Index, result.Command.TargetId);
            Assert.AreEqual(entity.Generation, result.Command.Payload0);
            Assert.AreEqual(HpAttributeId, result.Command.Payload1);
            Assert.AreEqual(-10, result.Command.Payload2);
            Assert.AreEqual("story-hit", result.Command.TraceId);
        }

        [Test]
        public void RejectsUnsupportedBuffGrantWithoutMutation()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            var buffer = new RuntimeCommandBuffer();
            var bridge = new StoryGameplayEffectBridge(buffer, world);

            StoryGameplayEffectResult result = bridge.EnqueueGameplayEffect(
                StoryGameplayEffectIntent.BuffGrant(
                    StoryGameplayEntityRef.ComponentEntity(entity),
                    SourceId,
                    buffId: 2001),
                RuntimeFrame.Zero);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(StoryGameplayBridgeDiagnosticCode.UnsupportedBuffEffect, result.Diagnostic.Code);
            Assert.AreEqual(0, buffer.PendingCount);
        }

        [Test]
        public void RejectsUnsupportedBuffRemoveWithoutMutation()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            var buffer = new RuntimeCommandBuffer();
            var bridge = new StoryGameplayEffectBridge(buffer, world);

            StoryGameplayEffectResult result = bridge.EnqueueGameplayEffect(
                StoryGameplayEffectIntent.BuffRemove(
                    StoryGameplayEntityRef.ComponentEntity(entity),
                    SourceId,
                    buffId: 2001),
                RuntimeFrame.Zero);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(StoryGameplayBridgeDiagnosticCode.UnsupportedBuffEffect, result.Diagnostic.Code);
            Assert.AreEqual(0, buffer.PendingCount);
        }

        [Test]
        public void DelayFramesControlsTargetCommandFrame()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            var buffer = new RuntimeCommandBuffer();
            var bridge = new StoryGameplayEffectBridge(buffer, world);

            StoryGameplayEffectResult sameFrame = bridge.EnqueueGameplayEffect(
                StoryGameplayEffectIntent.SetComponentAttribute(
                    StoryGameplayEntityRef.ComponentEntity(entity),
                    SourceId,
                    HpAttributeId,
                    90,
                    delayFrames: 0),
                new RuntimeFrame(10));
            StoryGameplayEffectResult delayed = bridge.EnqueueGameplayEffect(
                StoryGameplayEffectIntent.SetComponentAttribute(
                    StoryGameplayEntityRef.ComponentEntity(entity),
                    SourceId,
                    HpAttributeId,
                    80,
                    delayFrames: 3),
                new RuntimeFrame(10));

            Assert.IsTrue(sameFrame.Success);
            Assert.IsTrue(delayed.Success);
            Assert.AreEqual(new RuntimeFrame(10), sameFrame.Command.Frame);
            Assert.AreEqual(new RuntimeFrame(13), delayed.Command.Frame);

            IReadOnlyList<RuntimeCommand> drained = buffer.DrainForFrame(new RuntimeFrame(10));
            Assert.AreEqual(1, drained.Count);
            Assert.AreEqual(new RuntimeFrame(10), drained[0].Frame);
            Assert.AreEqual(1, buffer.PendingCount);
        }

        [Test]
        public void NegativeDelayFramesClampToCurrentFrame()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            var buffer = new RuntimeCommandBuffer();
            var bridge = new StoryGameplayEffectBridge(buffer, world);

            StoryGameplayEffectResult result = bridge.EnqueueGameplayEffect(
                StoryGameplayEffectIntent.SetComponentAttribute(
                    StoryGameplayEntityRef.ComponentEntity(entity),
                    SourceId,
                    HpAttributeId,
                    90,
                    delayFrames: -5),
                new RuntimeFrame(10));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(new RuntimeFrame(10), result.Command.Frame);
        }

        [Test]
        public void DoesNotDrainGameplayCommandBuffer()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            var buffer = new RuntimeCommandBuffer();
            buffer.Enqueue(GameplayRuntimeCommandFactory.SetComponentAttribute(
                RuntimeFrame.Zero,
                entity,
                HpAttributeId,
                100));
            var bridge = new StoryGameplayEffectBridge(buffer, world);

            StoryGameplayEffectResult result = bridge.EnqueueGameplayEffect(
                StoryGameplayEffectIntent.AddComponentAttribute(
                    StoryGameplayEntityRef.ComponentEntity(entity),
                    SourceId,
                    HpAttributeId,
                    -5),
                RuntimeFrame.Zero);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, buffer.PendingCount);
        }
    }
}
