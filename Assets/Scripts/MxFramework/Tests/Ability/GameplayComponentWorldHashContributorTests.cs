using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplayComponentWorldHashContributorTests
    {
        [Test]
        public void ComponentWorldHashContributor_StableAcrossStoreAndSchemaRegistrationOrder()
        {
            GameplayComponentWorld first = CreateWorld(registerHashFirst: true);
            GameplayComponentWorld second = CreateWorld(registerHashFirst: false);

            PopulateWorld(first, setTeamFirst: true);
            PopulateWorld(second, setTeamFirst: false);

            Assert.AreEqual(ComputeHash(first), ComputeHash(second));
        }

        [Test]
        public void ComponentWorldHashContributor_ComponentValueChangeChangesHash()
        {
            GameplayComponentWorld world = CreateWorld(registerHashFirst: true);
            GameplayEntityId entity = world.CreateEntity();
            world.GetOrCreateStore<GameplayTeamComponent>().Set(entity, new GameplayTeamComponent(1));

            long before = ComputeHash(world);
            world.GetOrCreateStore<GameplayTeamComponent>().Set(entity, new GameplayTeamComponent(2));
            long after = ComputeHash(world);

            Assert.AreNotEqual(before, after);
        }

        [Test]
        public void ComponentWorldHashContributor_IgnoresUnsupportedAndUnregisteredComponents()
        {
            GameplayComponentWorld baseline = CreateWorld(registerHashFirst: true);
            GameplayComponentWorld withExtras = CreateWorld(registerHashFirst: true);

            GameplayEntityId baselineEntity = baseline.CreateEntity();
            baseline.GetOrCreateStore<GameplayTeamComponent>().Set(baselineEntity, new GameplayTeamComponent(1));

            GameplayEntityId extraEntity = withExtras.CreateEntity();
            withExtras.GetOrCreateStore<GameplayTeamComponent>().Set(extraEntity, new GameplayTeamComponent(1));
            withExtras.GetOrCreateStore<UnsupportedComponent>().Set(extraEntity, new UnsupportedComponent(99));
            withExtras.Schemas.Register(new UnsupportedDiagnostics());

            Assert.AreEqual(ComputeHash(baseline), ComputeHash(withExtras));
        }

        [Test]
        public void CoreRuntimeHash_RegistersHashWritersBesideDiagnostics()
        {
            var registry = new GameplayComponentSchemaRegistry();

            GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(registry);
            GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(registry);

            Assert.AreEqual(8, registry.Count);
            Assert.IsTrue(registry.TryGetDiagnosticWriter(out IGameplayComponentDiagnosticWriter<GameplayIdentityComponent> diagnosticWriter));
            Assert.IsTrue(registry.TryGetHashWriter(out IGameplayComponentHashWriter<GameplayIdentityComponent> hashWriter));
            Assert.IsTrue(registry.TryGetDiagnosticWriter(out IGameplayComponentDiagnosticWriter<GameplayPosturePressureComponent> postureDiagnosticWriter));
            Assert.IsTrue(registry.TryGetHashWriter(out IGameplayComponentHashWriter<GameplayPosturePressureComponent> postureHashWriter));
            Assert.IsNotNull(diagnosticWriter);
            Assert.IsNotNull(hashWriter);
            Assert.IsNotNull(postureDiagnosticWriter);
            Assert.IsNotNull(postureHashWriter);
        }

        private static GameplayComponentWorld CreateWorld(bool registerHashFirst)
        {
            var world = new GameplayComponentWorld();

            if (registerHashFirst)
            {
                GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
                GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            }
            else
            {
                GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
                GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            }

            return world;
        }

        private static void PopulateWorld(GameplayComponentWorld world, bool setTeamFirst)
        {
            GameplayEntityId player = world.CreateEntity();
            GameplayEntityId enemy = world.CreateEntity();

            if (setTeamFirst)
            {
                world.GetOrCreateStore<GameplayTeamComponent>().Set(player, new GameplayTeamComponent(1));
                world.GetOrCreateStore<GameplayIdentityComponent>().Set(player, new GameplayIdentityComponent(1001, 1));
            }
            else
            {
                world.GetOrCreateStore<GameplayIdentityComponent>().Set(player, new GameplayIdentityComponent(1001, 1));
                world.GetOrCreateStore<GameplayTeamComponent>().Set(player, new GameplayTeamComponent(1));
            }

            world.GetOrCreateStore<GameplayLifecycleComponent>().Set(player, GameplayLifecycleComponent.Alive);
            world.GetOrCreateStore<GameplayTagComponent>().Set(
                player,
                new GameplayTagComponent(new GameplayTagId(30), new GameplayTagId(10)));
            world.GetOrCreateStore<GameplayStatusComponent>().Set(
                player,
                new GameplayStatusComponent(new GameplayStatusId(200), new GameplayStatusId(100)));

            world.GetOrCreateStore<GameplayIdentityComponent>().Set(enemy, new GameplayIdentityComponent(2001, 0));
            world.GetOrCreateStore<GameplayTeamComponent>().Set(enemy, new GameplayTeamComponent(2));
            world.GetOrCreateStore<GameplayLifecycleComponent>().Set(enemy, GameplayLifecycleComponent.Alive);
        }

        private static long ComputeHash(GameplayComponentWorld world)
        {
            var contributor = new GameplayComponentWorldHashContributor(world);
            return RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new IRuntimeHashContributor[] { contributor });
        }

        private readonly struct UnsupportedComponent : IGameplayComponent
        {
            public UnsupportedComponent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed class UnsupportedDiagnostics : IGameplayComponentDiagnosticWriter<UnsupportedComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                "test.unsupported",
                1,
                typeof(UnsupportedComponent),
                supportsDiagnostics: true);

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in UnsupportedComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                writer.AddInt("value", component.Value);
            }
        }
    }
}
