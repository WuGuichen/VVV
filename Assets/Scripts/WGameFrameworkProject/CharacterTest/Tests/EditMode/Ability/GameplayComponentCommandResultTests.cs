using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentCommandResultTests
    {
        private const int SourceId = 7128;
        private const int AttributeId = 31;
        private const int AbilityId = 300031;

        [Test]
        public void Destroy_CorrelatedCommandPublishesExactSuccessOnce()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            RuntimeCommand command = Correlate(GameplayRuntimeCommandFactory.DestroyComponentEntity(
                RuntimeFrame.Zero,
                entity));
            var sink = new RecordingSink();
            var system = new GameplayComponentEntityCommandSystem(
                GameplayComponentEntityCommandSystem.DefaultSystemId,
                20,
                sink);

            system.Tick(CreateContext(world, command));

            Assert.AreEqual(1, sink.Results.Count);
            GameplayComponentCommandResult result = sink.Results[0];
            Assert.AreEqual(GameplayComponentCommandResultStatus.Succeeded, result.Status);
            Assert.AreEqual(GameplayComponentCommandResultReasonCode.ComponentEntityDestroyed, result.ReasonCode);
            Assert.AreEqual(0L, result.ResultPayload0);
            Assert.AreEqual(0L, result.ResultPayload1);
            Assert.AreEqual(0L, result.ResultPayload2);
            AssertCommandIdentity(command, result);
        }

        [Test]
        public void Destroy_UncorrelatedCommandDoesNotPublish()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            RuntimeCommand command = GameplayRuntimeCommandFactory.DestroyComponentEntity(RuntimeFrame.Zero, entity);
            var sink = new RecordingSink();
            var system = new GameplayComponentEntityCommandSystem(
                GameplayComponentEntityCommandSystem.DefaultSystemId,
                20,
                sink);

            system.Tick(CreateContext(world, command));

            Assert.AreEqual(0, sink.Results.Count);
        }

        [Test]
        public void Attribute_CorrelatedSetPublishesOldNewDelta()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(AttributeId, 100, 80)));
            RuntimeCommand command = Correlate(GameplayRuntimeCommandFactory.SetComponentAttribute(
                RuntimeFrame.Zero,
                entity,
                AttributeId,
                65));
            var sink = new RecordingSink();
            var system = new GameplayAttributeCommandSystem(
                GameplayAttributeCommandSystem.DefaultSystemId,
                40,
                sink);

            system.Tick(CreateContext(world, command));

            Assert.AreEqual(1, sink.Results.Count);
            GameplayComponentCommandResult result = sink.Results[0];
            Assert.AreEqual(GameplayComponentCommandResultStatus.Succeeded, result.Status);
            Assert.AreEqual(GameplayComponentCommandResultReasonCode.ComponentAttributeSet, result.ReasonCode);
            Assert.AreEqual(80L, result.ResultPayload0);
            Assert.AreEqual(65L, result.ResultPayload1);
            Assert.AreEqual(-15L, result.ResultPayload2);
            AssertCommandIdentity(command, result);
        }

        [Test]
        public void Attribute_CorrelatedMissingSetPublishesTypedRejection()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            RuntimeCommand command = Correlate(GameplayRuntimeCommandFactory.AddComponentAttribute(
                RuntimeFrame.Zero,
                entity,
                AttributeId,
                5));
            var sink = new RecordingSink();
            var system = new GameplayAttributeCommandSystem(
                GameplayAttributeCommandSystem.DefaultSystemId,
                40,
                sink);

            system.Tick(CreateContext(world, command));

            Assert.AreEqual(1, sink.Results.Count);
            GameplayComponentCommandResult result = sink.Results[0];
            Assert.AreEqual(GameplayComponentCommandResultStatus.Rejected, result.Status);
            Assert.AreEqual(GameplayComponentCommandResultReasonCode.MissingAttributeSet, result.ReasonCode);
            Assert.AreEqual(AttributeId, result.ResultPayload0);
            Assert.AreEqual(0L, result.ResultPayload1);
            Assert.AreEqual(0L, result.ResultPayload2);
        }

        [Test]
        public void Ability_CorrelatedCommandPublishesExactSuccessOnce()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId caster = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                caster,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(AttributeId, 100, 100)));
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(new GameplayComponentAttributeDeltaAbility(
                AbilityId,
                AttributeId,
                -10,
                GameplayComponentTargetMode.Self));
            RuntimeCommand command = Correlate(GameplayRuntimeCommandFactory.CastComponentAbility(
                RuntimeFrame.Zero,
                caster,
                AbilityId));
            var sink = new RecordingSink();
            var system = new GameplayComponentAbilityCommandSystem(
                registry,
                requestStore: null,
                targetingService: null,
                buffRequests: null,
                GameplayComponentAbilityCommandSystem.DefaultSystemId,
                50,
                sink);

            system.Tick(CreateContext(world, command));

            Assert.AreEqual(1, sink.Results.Count);
            GameplayComponentCommandResult result = sink.Results[0];
            Assert.AreEqual(GameplayComponentCommandResultStatus.Succeeded, result.Status);
            Assert.AreEqual(GameplayComponentCommandResultReasonCode.ComponentAbilityCastSucceeded, result.ReasonCode);
            Assert.AreEqual(AbilityId, result.ResultPayload0);
            Assert.AreEqual(0L, result.ResultPayload1);
            Assert.AreEqual(0L, result.ResultPayload2);
            AssertCommandIdentity(command, result);
        }

        [Test]
        public void Ability_CorrelatedRequestHandleCommandDoesNotPublish()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId caster = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                caster,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(AttributeId, 100, 100)));
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(new GameplayComponentAttributeDeltaAbility(
                AbilityId,
                AttributeId,
                -10,
                GameplayComponentTargetMode.Self));
            var requests = new GameplayComponentAbilityRequestStore();
            GameplayComponentAbilityRequestHandle handle = requests.Add(
                new GameplayComponentAbilityRequest(caster, AbilityId));
            RuntimeCommand command = Correlate(
                GameplayRuntimeCommandFactory.CastComponentAbilityRequest(
                    RuntimeFrame.Zero,
                    handle,
                    AbilityId));
            var sink = new RecordingSink();
            var system = new GameplayComponentAbilityCommandSystem(
                registry,
                requests,
                targetingService: null,
                buffRequests: null,
                GameplayComponentAbilityCommandSystem.DefaultSystemId,
                50,
                sink);

            system.Tick(CreateContext(world, command));

            Assert.AreEqual(0, sink.Results.Count);
        }

        private static GameplaySystemContext CreateContext(GameplayComponentWorld world, RuntimeCommand command)
        {
            return new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                new GameplayWorld(),
                new[] { command },
                world.Events,
                componentWorld: world);
        }

        private static RuntimeCommand Correlate(RuntimeCommand command)
        {
            return new RuntimeCommand(
                command.Frame,
                SourceId,
                command.CommandId,
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.Payload2,
                RuntimeTraceIdentity.None,
                sequence: 0L,
                envelope: new RuntimeCommandEnvelope(
                    typeId: 1002104,
                    schemaVersion: 1,
                    RuntimeCommandProvenanceKind.Derived,
                    data0: 1L,
                    data1: command.CommandId,
                    data2: 1001004L,
                    data3: 1L,
                    data4: 0L,
                    data5: 1L));
        }

        private static void AssertCommandIdentity(
            RuntimeCommand command,
            GameplayComponentCommandResult result)
        {
            Assert.AreEqual(command.Frame, result.Frame);
            Assert.AreEqual(command.SourceId, result.SourceId);
            Assert.AreEqual(command.CommandId, result.CommandId);
            Assert.AreEqual(command.TargetId, result.TargetId);
            Assert.AreEqual(command.Payload0, result.CommandPayload0);
            Assert.AreEqual(command.Payload1, result.CommandPayload1);
            Assert.AreEqual(command.Payload2, result.CommandPayload2);
            Assert.AreEqual(command.Sequence, result.CommandSequence);
            Assert.IsTrue(result.TraceIdIsEmpty);
            Assert.AreEqual(command.Envelope, result.Envelope);
        }

        private sealed class RecordingSink : IGameplayComponentCommandResultSink
        {
            public readonly List<GameplayComponentCommandResult> Results =
                new List<GameplayComponentCommandResult>();

            public void Publish(in GameplayComponentCommandResult result)
            {
                Results.Add(result);
            }
        }
    }
}
