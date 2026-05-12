using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentSchemaRegistryTests
    {
        [Test]
        public void Schema_ValidatesStableIdVersionAndComponentType()
        {
            Assert.Throws<ArgumentException>(() => new GameplayComponentSchema(string.Empty, 1, typeof(TestComponent)));
            Assert.Throws<ArgumentException>(() => new GameplayComponentSchema(" test.component", 1, typeof(TestComponent)));
            Assert.Throws<ArgumentException>(() => new GameplayComponentSchema("Test.Component", 1, typeof(TestComponent)));
            Assert.Throws<ArgumentException>(() => new GameplayComponentSchema("test..component", 1, typeof(TestComponent)));
            Assert.Throws<ArgumentException>(() => new GameplayComponentSchema("test component", 1, typeof(TestComponent)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplayComponentSchema("test.component", 0, typeof(TestComponent)));
            Assert.Throws<ArgumentNullException>(() => new GameplayComponentSchema("test.component", 1, null));
            Assert.Throws<ArgumentException>(() => new GameplayComponentSchema("test.component", 1, typeof(string)));
            Assert.Throws<ArgumentException>(() => new GameplayComponentSchema("test.component", 1, typeof(ReferenceComponent)));

            var schema = new GameplayComponentSchema("test.component", 1, typeof(TestComponent));

            Assert.AreEqual("test.component", schema.StableId);
            Assert.AreEqual("test.component", schema.DisplayName);
            Assert.AreEqual(typeof(TestComponent), schema.ComponentType);
        }

        [Test]
        public void Registry_RegistersSchemaAndCreatesStableSnapshot()
        {
            var registry = new GameplayComponentSchemaRegistry();

            registry.Register(new TestSchemaOnlyDescriptor(
                new GameplayComponentSchema("test.z", 1, typeof(SecondComponent))));
            registry.Register(new TestSchemaOnlyDescriptor(
                new GameplayComponentSchema("test.a", 1, typeof(TestComponent))));

            Assert.AreEqual(2, registry.Count);
            Assert.IsTrue(registry.TryGetByStableId("test.a", out GameplayComponentSchema byId));
            Assert.AreEqual(typeof(TestComponent), byId.ComponentType);
            Assert.IsTrue(registry.TryGetByType(typeof(SecondComponent), out GameplayComponentSchema byType));
            Assert.AreEqual("test.z", byType.StableId);

            GameplayComponentSchema[] snapshot = registry.CreateSnapshot();

            Assert.AreEqual("test.a", snapshot[0].StableId);
            Assert.AreEqual("test.z", snapshot[1].StableId);
        }

        [Test]
        public void Registry_RejectsDuplicateStableIdTypeAndConflictingMetadata()
        {
            var registry = new GameplayComponentSchemaRegistry();
            var schema = new GameplayComponentSchema("test.component", 1, typeof(TestComponent), "Test");
            registry.Register(new TestSchemaOnlyDescriptor(schema));

            Assert.Throws<InvalidOperationException>(() => registry.Register(new TestSchemaOnlyDescriptor(schema)));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new TestSchemaOnlyDescriptor(
                new GameplayComponentSchema("test.component", 1, typeof(SecondComponent), "Other"))));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new TestSchemaOnlyDescriptor(
                new GameplayComponentSchema("test.other", 1, typeof(TestComponent), "Other"))));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new TestSchemaOnlyDescriptor(
                new GameplayComponentSchema("test.component", 2, typeof(TestComponent), "Test"))));
        }

        [Test]
        public void Registry_AttachesMultipleCapabilitiesToOneSchemaEntry()
        {
            var registry = new GameplayComponentSchemaRegistry();
            var schema = new GameplayComponentSchema(
                "test.component",
                1,
                typeof(TestComponent),
                supportsDiagnostics: true,
                supportsHash: true);

            registry.Register(new TestDiagnosticDescriptor(schema));
            registry.Register(new TestHashDescriptor(schema));

            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(registry.TryGetDiagnosticWriter(out IGameplayComponentDiagnosticWriter<TestComponent> diagnosticWriter));
            Assert.IsTrue(registry.TryGetHashWriter(out IGameplayComponentHashWriter<TestComponent> hashWriter));
            Assert.IsFalse(registry.TryGetSaveStateAdapter(out IGameplayComponentSaveStateAdapter<TestComponent> saveAdapter));
            Assert.IsNotNull(diagnosticWriter);
            Assert.IsNotNull(hashWriter);
            Assert.IsNull(saveAdapter);
        }

        [Test]
        public void Registry_RejectsDuplicateCapability()
        {
            var registry = new GameplayComponentSchemaRegistry();
            var schema = new GameplayComponentSchema(
                "test.component",
                1,
                typeof(TestComponent),
                supportsDiagnostics: true);

            registry.Register(new TestDiagnosticDescriptor(schema));

            Assert.Throws<InvalidOperationException>(() => registry.Register(new TestDiagnosticDescriptor(schema)));
        }

        [Test]
        public void Registry_RejectsCapabilityWhenSchemaDoesNotDeclareSupport()
        {
            var registry = new GameplayComponentSchemaRegistry();

            Assert.Throws<InvalidOperationException>(() => registry.Register(new TestDiagnosticDescriptor(
                new GameplayComponentSchema("test.diagnostic", 1, typeof(TestComponent)))));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new TestHashDescriptor(
                new GameplayComponentSchema("test.hash", 1, typeof(TestComponent)))));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new TestSaveDescriptor(
                new GameplayComponentSchema("test.save", 1, typeof(TestComponent)))));
        }

        [Test]
        public void Registry_RejectsCapabilityWithMismatchedSchemaComponentType()
        {
            var registry = new GameplayComponentSchemaRegistry();

            Assert.Throws<InvalidOperationException>(() => registry.Register(new MismatchedDiagnosticDescriptor(
                new GameplayComponentSchema(
                    "test.mismatch",
                    1,
                    typeof(TestComponent),
                    supportsDiagnostics: true))));
        }

        [Test]
        public void CoreDiagnostics_RegisterSchemasAndWriteStableFields()
        {
            var registry = new GameplayComponentSchemaRegistry();
            GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(registry);

            GameplayComponentSchema[] snapshot = registry.CreateSnapshot();

            Assert.AreEqual(5, snapshot.Length);
            Assert.AreEqual(GameplayCoreComponentSchemaDescriptors.IdentityStableId, snapshot[0].StableId);
            Assert.IsTrue(registry.TryGetDiagnosticWriter(out IGameplayComponentDiagnosticWriter<GameplayIdentityComponent> identityWriter));
            Assert.IsFalse(registry.TryGetHashWriter(out IGameplayComponentHashWriter<GameplayIdentityComponent> hashWriter));
            Assert.IsFalse(registry.TryGetSaveStateAdapter(out IGameplayComponentSaveStateAdapter<GameplayIdentityComponent> saveAdapter));

            var writer = new GameplayComponentDiagnosticWriter();
            identityWriter.WriteDiagnostics(
                new GameplayEntityId(3, 2),
                new GameplayIdentityComponent(1001, 4),
                writer);

            GameplayComponentDiagnosticField[] fields = writer.CreateSnapshot();

            Assert.AreEqual("entity.index", fields[0].Key);
            Assert.AreEqual("3", fields[0].Value);
            Assert.AreEqual("entity.generation", fields[1].Key);
            Assert.AreEqual("2", fields[1].Value);
            Assert.AreEqual("definitionId", fields[2].Key);
            Assert.AreEqual("1001", fields[2].Value);
            Assert.AreEqual("variantId", fields[3].Key);
            Assert.AreEqual("4", fields[3].Value);
            Assert.IsNull(hashWriter);
            Assert.IsNull(saveAdapter);
        }

        [Test]
        public void ComponentWorld_OwnsSchemaRegistry()
        {
            var schemas = new GameplayComponentSchemaRegistry();
            var world = new GameplayComponentWorld(null, null, schemas);

            Assert.AreSame(schemas, world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            Assert.AreEqual(5, world.Schemas.Count);
        }

        private readonly struct TestComponent : IGameplayComponent
        {
            public TestComponent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private readonly struct SecondComponent : IGameplayComponent
        {
        }

        private sealed class ReferenceComponent : IGameplayComponent
        {
        }

        private sealed class TestSchemaOnlyDescriptor : IGameplayComponentSchemaDescriptor
        {
            public TestSchemaOnlyDescriptor(GameplayComponentSchema schema)
            {
                Schema = schema;
            }

            public GameplayComponentSchema Schema { get; }
        }

        private sealed class TestDiagnosticDescriptor : IGameplayComponentDiagnosticWriter<TestComponent>
        {
            public TestDiagnosticDescriptor(GameplayComponentSchema schema)
            {
                Schema = schema;
            }

            public GameplayComponentSchema Schema { get; }

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in TestComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                writer.AddInt("value", component.Value);
            }
        }

        private sealed class TestHashDescriptor : IGameplayComponentHashWriter<TestComponent>
        {
            public TestHashDescriptor(GameplayComponentSchema schema)
            {
                Schema = schema;
            }

            public GameplayComponentSchema Schema { get; }

            public void WriteHash(
                GameplayEntityId entityId,
                in TestComponent component,
                RuntimeHashAccumulator accumulator)
            {
                accumulator.AddInt("value", component.Value);
            }
        }

        private sealed class TestSaveDescriptor : IGameplayComponentSaveStateAdapter<TestComponent>
        {
            public TestSaveDescriptor(GameplayComponentSchema schema)
            {
                Schema = schema;
            }

            public GameplayComponentSchema Schema { get; }

            public RuntimeCustomState WriteSaveState(GameplayEntityId entityId, in TestComponent component)
            {
                return new RuntimeCustomState(Schema.StableId, Schema.Version, "{\"value\":" + component.Value + "}");
            }

            public RuntimeSaveStateResult<TestComponent> ReadSaveState(GameplayEntityId entityId, RuntimeCustomState payload)
            {
                return RuntimeSaveStateResult<TestComponent>.Succeeded(new TestComponent(0));
            }
        }

        private sealed class MismatchedDiagnosticDescriptor : IGameplayComponentDiagnosticWriter<SecondComponent>
        {
            public MismatchedDiagnosticDescriptor(GameplayComponentSchema schema)
            {
                Schema = schema;
            }

            public GameplayComponentSchema Schema { get; }

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in SecondComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
            }
        }
    }
}
