using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeCommandRegistryTests
    {
        [Test]
        public void Register_ThenTryGet_ReturnsDefinition()
        {
            var registry = new RuntimeCommandRegistry();
            var schema = new RuntimeCommandPayloadSchema("payload0: action slot");
            var definition = new RuntimeCommandDefinition(10, "Cast", schema, "Casts a registered runtime command.");

            registry.Register(definition);

            RuntimeCommandDefinition found;
            Assert.IsTrue(registry.TryGet(10, out found));
            Assert.AreEqual(10, found.CommandId);
            Assert.AreEqual("Cast", found.Name);
            Assert.AreSame(schema, found.PayloadSchema);
            Assert.AreEqual("Casts a registered runtime command.", found.Description);
            Assert.IsTrue(found.HasPayloadSchema);
        }

        [Test]
        public void Register_DuplicateId_Throws()
        {
            var registry = new RuntimeCommandRegistry();
            registry.Register(new RuntimeCommandDefinition(10, "Cast"));

            Assert.Throws<ArgumentException>(() => registry.Register(new RuntimeCommandDefinition(10, "Move")));
        }

        [Test]
        public void Register_InvalidDefinition_Throws()
        {
            var registry = new RuntimeCommandRegistry();

            Assert.Throws<ArgumentOutOfRangeException>(() => registry.Register(new RuntimeCommandDefinition(-1, "Invalid")));
            Assert.Throws<ArgumentException>(() => registry.Register(new RuntimeCommandDefinition(1, "")));
            Assert.Throws<ArgumentException>(() => registry.Register(new RuntimeCommandDefinition(1, "   ")));
        }

        [Test]
        public void CreateSnapshot_ReturnsStableDefinitionsSortedByIdThenName()
        {
            var registry = new RuntimeCommandRegistry();
            registry.Register(new RuntimeCommandDefinition(20, "Move"));
            registry.Register(new RuntimeCommandDefinition(10, "Cast"));

            RuntimeCommandRegistrySnapshot snapshot = registry.CreateSnapshot();
            registry.Register(new RuntimeCommandDefinition(30, "Reset"));

            Assert.AreEqual(2, snapshot.Definitions.Count);
            Assert.AreEqual(10, snapshot.Definitions[0].CommandId);
            Assert.AreEqual("Cast", snapshot.Definitions[0].Name);
            Assert.AreEqual(20, snapshot.Definitions[1].CommandId);
            Assert.AreEqual("Move", snapshot.Definitions[1].Name);

            IReadOnlyList<RuntimeCommandDefinition> listed = registry.ListDefinitions();
            Assert.AreEqual(3, listed.Count);
            Assert.AreEqual(10, listed[0].CommandId);
            Assert.AreEqual(20, listed[1].CommandId);
            Assert.AreEqual(30, listed[2].CommandId);
        }

        [Test]
        public void Validator_AcceptsRegisteredCommandAndRejectsUnregisteredCommand()
        {
            var registry = new RuntimeCommandRegistry();
            registry.Register(new RuntimeCommandDefinition(10, "Cast"));
            var validator = new RuntimeCommandRegistryValidator(registry);

            RuntimeCommandValidationResult accepted = validator.Validate(Command(10));
            RuntimeCommandValidationResult rejected = validator.Validate(Command(20));

            Assert.IsTrue(accepted.Success);
            Assert.AreEqual(10, accepted.Command.CommandId);

            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.UnregisteredCommandId, rejected.Error.Code);
            Assert.AreEqual(20, rejected.Error.Command.CommandId);
        }

        [Test]
        public void Validator_RejectsPayloadWhenSchemaValidatorFails()
        {
            var registry = new RuntimeCommandRegistry();
            registry.Register(new RuntimeCommandDefinition(
                10,
                "Cast",
                new RuntimeCommandPayloadSchema("payload0 must be positive", command => command.Payload0 > 0)));
            var validator = new RuntimeCommandRegistryValidator(registry);

            RuntimeCommandValidationResult accepted = validator.Validate(Command(10, payload0: 1));
            RuntimeCommandValidationResult rejected = validator.Validate(Command(10, payload0: 0));

            Assert.IsTrue(accepted.Success);
            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.InvalidPayload, rejected.Error.Code);
        }

        private static RuntimeCommand Command(int commandId, int payload0 = 0)
        {
            return new RuntimeCommand(RuntimeFrame.Zero, sourceId: 1, commandId: commandId, targetId: 2, payload0: payload0);
        }
    }
}
